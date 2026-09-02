// =============================================================================
// StructureContentWarmer — the ASYNC side of the structure art seam, and the
// reason StructureAssetLoader can no longer hang the game.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/Addressables). References Addressables only — no
// Village/HUD types, so any module can warm/read it.
//
// ⛔ THE P0 THIS EXISTS FOR (captured 2026-08-20, Seeker device, dungeon -> town):
//   08-20 10:25:14.917 [Flow:VisualFactory] -> Skin('Structures/barracks')
//     ... HubStructureVisualInjector:OnSceneLoaded(Scene, LoadSceneMode)
//   ...and then NOTHING, ever again. Device clock reached 10:28:35 while Unity's
//   last log line was 10:25:14.917 — THREE MINUTES of total silence from the
//   process. Alive, foregrounded, stale frame, no ANR (Android only raises those
//   on input-dispatch timeout, and nothing was dispatching).
//
// ⛔ IT WAS NOT A SLOW NETWORK. The owner tested the device WHILE IT WAS HUNG:
//   Wi-Fi associated (SSID Casa-Denelle), ping to the R2 CDN host = 2/2 packets,
//   0% loss, 31.5 ms avg. The CDN was healthy at the exact moment the main thread
//   was dead. A TIMEOUT WOULD NOT HAVE HELPED — the operation was not slow, it was
//   UNABLE TO PROGRESS AT ALL.
//
// ⛔ THE ACTUAL MECHANISM, verified against the Addressables 2.9.1 source that
// ships in this project (Library/PackageCache/com.unity.addressables@8460f1c9c927),
// NOT from memory or from a doc:
//
//   1. AsyncOperationBase.WaitForCompletion (AsyncOperations/AsyncOperationBase.cs:171)
//      is a BARE, UNINTERRUPTIBLE, UNBOUNDED BUSY-SPIN:
//          while (!InvokeWaitForCompletion()) { }
//      No timeout parameter. No yield. No exit condition other than the operation
//      completing. THERE IS NO BOUNDED-SYNCHRONOUS-WAIT API IN THIS PACKAGE — that
//      is a fact about the vendor code, not a design preference of ours.
//
//   2. ProviderOperation.InvokeWaitForCompletion (AsyncOperations/ProviderOperation.cs:66)
//      can return FALSE FOREVER: it pumps m_RM.Update(...) and then
//          if (m_WaitForCompletionCallback == null) return false;
//      so any operation whose provider has not installed a completion callback
//      spins the loop above until the process dies.
//
//   3. AssetBundleResource.WaitForCompletionHandler (ResourceProviders/AssetBundleProvider.cs:543)
//      — the remote-bundle path — does, ON THE MAIN THREAD:
//          if (m_RequestOperation == null) { if (m_WebRequestQueueOperation == null) return false; ... }
//          while (!UnityWebRequestUtilities.IsAssetBundleDownloaded(op))
//              System.Threading.Thread.Sleep(k_WaitForWebRequestMainThreadSleep);
//      i.e. it SLEEPS the main thread waiting on progress that the engine's own
//      player loop is responsible for driving. It also defers BeginOperation()
//      behind m_UnloadOperation — the bundle-unload op — which is exactly what a
//      dungeon->town transition creates.
//
//   Put (1)+(2)+(3) inside a SceneManager.sceneLoaded engine callback — a nested
//   engine call the player loop cannot re-enter — and the thread that would drive
//   the operation to completion is the thread blocked waiting for it. That is a
//   textbook deadlock, and it explains every observed property: silent, permanent,
//   intermittent (a bundle already resident in memory returns without needing to
//   pump), and correlated with the owner's constant town -> dungeon -> town loop
//   (the dungeon load is what evicts the structure content).
//
// -----------------------------------------------------------------------------
// SO THE FIX IS NOT A TIMEOUT. IT IS: NEVER BLOCK, AND NEVER BLOCK FROM A CALLBACK.
//
//   A. This warmer does ALL Addressables work ASYNCHRONOUSLY, from a coroutine on
//      a DontDestroyOnLoad host — i.e. from the player loop, the one place the
//      ResourceManager can actually be pumped.
//   B. Everything it resolves is RETAINED FOREVER (s_retained is never released).
//      That is deliberate and is the anti-eviction measure: the owner runs
//      town -> dungeon -> town constantly, and a released handle lets the bundle
//      unload and re-download on every single cycle — which is what puts the game
//      back on the cold path where the deadlock lives.
//   C. Callers keep their SYNCHRONOUS shape via TryGet, which is a dictionary
//      lookup and cannot block, ever, for any reason.
//   D. Defer() exists so a scene-load callback can hand work to the NEXT FRAME
//      instead of doing it inside the engine callback. Getting off the callback is
//      half the fix; the other half is C.
//
// ⛔ DO NOT ADD WaitForCompletion() TO THIS FILE OR TO StructureAssetLoader.
// Assets/Editor/Regression/StructureLoadBoundedRegression.cs fails the build if you
// do. The ONE allowlisted site is StructureEditorSyncResolver.cs, which is entirely
// inside #if UNITY_EDITOR: the editor uses the AssetDatabase play-mode provider, so
// there is no bundle, no UnityWebRequest and no deadlock — and the batchmode gates
// need a synchronous answer with no player loop to warm from.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DeNelle.Core
{
    /// <summary>Where the structure-art warm pass has got to this launch.</summary>
    public enum StructureContentState
    {
        /// <summary>Nothing started yet (edit mode, or before the boot hook ran).</summary>
        Cold = 0,
        /// <summary>The warm pass is running.</summary>
        Warming = 1,
        /// <summary>The warm pass finished; content bundles are local.</summary>
        Warm = 2,
        /// <summary>The warm pass gave up (deadline / failure). Loads still work on demand,
        /// they just have no head start. Never a hang — only a degraded look.</summary>
        Degraded = 3,
    }

    /// <summary>
    /// Async residency cache for structure art. Warms the Addressables content set off the
    /// player loop, retains what it loads for the whole process, and answers
    /// <see cref="TryGet{T}"/> from memory so synchronous call sites never block.
    /// </summary>
    public static class StructureContentWarmer
    {
        /// <summary>FlowTrace system tag. Shared with StructureAssetLoader on purpose — a
        /// reader chasing a missing building wants one tag, not two.</summary>
        public const string System = "StructureAssets";

        /// <summary>Every structure address carries this prefix (see StructureAssetLoader).</summary>
        public const string AddressPrefix = "Structures/";

        /// <summary>
        /// Wall-clock budget for the whole warm pass. This is a REPORTING deadline, not a
        /// timeout on a blocking call — nothing is blocked, so exceeding it costs a log line
        /// and a degraded look, never a frame. (There is no such thing as a timeout on
        /// WaitForCompletion; see the header.)
        /// </summary>
        public const float WarmDeadlineSeconds = 45f;

        /// <summary>How long an address may sit unresolved before a Warn escalates to a Fail.</summary>
        public const float MissEscalateSeconds = 10f;

        // Resident assets, keyed by type+address. A hit here is the ONLY Addressables-backed
        // answer a synchronous caller can get at runtime, and it costs a dictionary probe.
        private static readonly Dictionary<string, Object> s_resident = new Dictionary<string, Object>();

        // ⛔ NEVER RELEASED, BY DESIGN. See (B) in the header: releasing is what lets the
        // dungeon load evict structure content and put the next town load back on the cold
        // path. The memory is the price of not deadlocking, and it is the right trade.
        private static readonly List<AsyncOperationHandle> s_retained = new List<AsyncOperationHandle>();

        // Addresses with an in-flight async request (dedupe — a skipped skin re-asks every reapply).
        private static readonly HashSet<string> s_inFlight = new HashSet<string>();
        private static readonly Queue<string> s_piQueue = new Queue<string>();
        private static bool s_piRequestActive;

        // Addresses that resolved to nothing even asynchronously; stop re-requesting them.
        private static readonly HashSet<string> s_deadAddresses = new HashSet<string>();

        // First time each address was asked for and could not be served. Powers the
        // "how long did it wait" number in the Warn/Fail lines.
        private static readonly Dictionary<string, float> s_firstMissAt = new Dictionary<string, float>();

        private static readonly List<Action> s_settleCallbacks = new List<Action>();
        private static readonly List<Action> s_deferred = new List<Action>();

        // Every string key Addressables knows about, harvested once during the warm pass. Used by
        // DependencyClosureTrace to tell a REAL double-ship (address registered AND Resources
        // answered) from content that legitimately still lives in Resources.
        private static readonly HashSet<string> s_registeredKeys = new HashSet<string>();

        private static Host s_host;
        private static bool s_warmStarted;
        private static int s_discovered;

        /// <summary>Where the warm pass has got to.</summary>
        public static StructureContentState State { get; private set; } = StructureContentState.Cold;

        /// <summary>True once the warm pass has finished or given up — either way it will not change again.</summary>
        public static bool IsSettled => State == StructureContentState.Warm || State == StructureContentState.Degraded;

        /// <summary>Addresses currently being fetched asynchronously.</summary>
        public static int PendingRequests => s_inFlight.Count;

        /// <summary>How many assets are resident (diagnostics + regression).</summary>
        public static int ResidentCount => s_resident.Count;

        /// <summary>How many handles are being retained for the life of the process.</summary>
        public static int RetainedHandleCount => s_retained.Count;

        /// <summary>How many structure addresses the warm pass found in the catalog.</summary>
        public static int DiscoveredAddressCount => s_discovered;

        /// <summary>
        /// True when Addressables has a key for this exact address. Answered from a set harvested
        /// once during the warm pass -- no catalog probe, no handle, no blocking.
        /// <para>This exists to keep the Resources-fallback report HONEST. A fallback is only a
        /// double-ship when the address is ALSO registered; content that was never migrated (e.g.
        /// the four ~33-156 KB Harvest/* FBXs, deliberately Resources-resident per the note at
        /// HarvestSite.cs:274) is answering from the only place it lives.</para>
        /// </summary>
        public static bool IsRegisteredAddress(string address) =>
            !string.IsNullOrEmpty(address) && s_registeredKeys.Contains(address);

        // =====================================================================
        //  Synchronous read — a dictionary probe. CANNOT BLOCK.
        // =====================================================================

        /// <summary>
        /// True when <paramref name="address"/> is already resident as <typeparamref name="T"/>.
        /// This is a dictionary lookup and nothing else: no catalog probe, no handle, no
        /// pumping, no possibility of a wait. It is the whole reason the synchronous call-site
        /// shape survived this fix.
        /// </summary>
        public static bool TryGet<T>(string address, out T asset) where T : Object
        {
            asset = null;
            if (string.IsNullOrWhiteSpace(address)) return false;

            if (s_resident.TryGetValue(Key(typeof(T), address), out var typed) && typed != null)
            {
                asset = typed as T;
                if (asset != null) return true;
            }

            // The warm pass stores under Object (it loads by location, where the concrete type
            // is not known up front). A GameObject parked there is still a valid GameObject
            // answer — resolve it here rather than making the warm pass type-aware.
            if (s_resident.TryGetValue(Key(typeof(Object), address), out var loose) && loose is T hit)
            {
                asset = hit;
                return true;
            }

            return false;
        }

        /// <summary>True when this address has already been proven absent from Addressables.</summary>
        public static bool IsKnownAbsent(string address) =>
            !string.IsNullOrEmpty(address) && s_deadAddresses.Contains(address);

        /// <summary>
        /// Seconds since this address was FIRST asked for and could not be served, or 0 the
        /// first time. Callers put this straight into their log line so a skip always says how
        /// long it has been waiting — the three minutes of silence happened because nothing
        /// ever said anything.
        /// </summary>
        public static float SecondsWaiting(string address)
        {
            if (string.IsNullOrEmpty(address)) return 0f;
            float now = Now();
            if (s_firstMissAt.TryGetValue(address, out float first)) return Mathf.Max(0f, now - first);
            s_firstMissAt[address] = now;
            return 0f;
        }

        // =====================================================================
        //  Asynchronous fetch — the ONLY way content enters the cache at runtime
        // =====================================================================

        /// <summary>
        /// Ask for <paramref name="address"/> without waiting for it. Idempotent (a repeat
        /// while in flight is a no-op) and silent about the common "not registered" case.
        /// The asset lands in the resident cache when it arrives; the caller finds it on a
        /// later attempt. Nothing here can block the calling frame.
        /// </summary>
        public static void Request(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            if (s_deadAddresses.Contains(address)) return;
            if (s_resident.ContainsKey(Key(typeof(Object), address))) return;
            if (!s_inFlight.Add(address)) return;

            EnsureHost();

            if (WebGLPiPlatform.IsPiBrowserEnvironment)
            {
                s_piQueue.Enqueue(address);
                StartNextPiRequest();
                return;
            }

            StartRequest(address);
        }

        private static void StartNextPiRequest()
        {
            if (s_piRequestActive || s_piQueue.Count == 0) return;
            s_piRequestActive = true;
            StartRequest(s_piQueue.Dequeue());
        }

        private static void StartRequest(string address)
        {

            bool started = Guard.Try(System, $"async request '{address}'", () =>
            {
                var handle = Addressables.LoadAssetAsync<Object>(address);
                handle.Completed += h => OnRequestCompleted(address, h);
            });

            if (!started)
            {
                // Guard already reported via FlowTrace.Fail. An address Addressables refuses
                // outright (InvalidKeyException) is dead for this launch; stop asking so the
                // reapply loop cannot spin on it.
                s_inFlight.Remove(address);
                s_deadAddresses.Add(address);
                if (WebGLPiPlatform.IsPiBrowserEnvironment)
                {
                    s_piRequestActive = false;
                    StartNextPiRequest();
                }
            }
        }

        private static void OnRequestCompleted(string address, AsyncOperationHandle<Object> handle)
        {
            s_inFlight.Remove(address);

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                s_resident[Key(typeof(Object), address)] = handle.Result;
                s_retained.Add(handle);          // ⛔ retained for the process; see header (B)
                float waited = SecondsWaiting(address);
                FlowTrace.Step(System,
                    $"'{address}' arrived ASYNC after {waited:F1}s and is now RESIDENT " +
                    $"(retained={s_retained.Count}) — the next skin attempt will use it. " +
                    "It is never released, so the dungeon->town cycle cannot evict it.");
            }
            else
            {
                s_deadAddresses.Add(address);
                FlowTrace.Fail(System,
                    $"async load of '{address}' FAILED ({handle.Status}) after {SecondsWaiting(address):F1}s — " +
                    "this structure will keep its baked twin for the rest of the launch. " +
                    "Check the address exists in the Structure_Art group. NOTE: this is a visual " +
                    "defect only; the game did NOT stall, which is the whole point of this path.");
                Guard.Try(System, $"release failed handle '{address}'", () => Addressables.Release(handle));
            }

            MaybeNotifySettled();
            if (WebGLPiPlatform.IsPiBrowserEnvironment)
            {
                s_piRequestActive = false;
                StartNextPiRequest();
            }
        }

        // =====================================================================
        //  Settle notification + frame deferral
        // =====================================================================

        /// <summary>
        /// Run <paramref name="onSettled"/> once, the next time the warm pass has finished AND
        /// no requests are in flight. Used by HubStructureVisualInjector to re-apply skins that
        /// it had to skip. Fires on the player loop, never from an engine callback.
        /// </summary>
        public static void WhenSettled(Action onSettled)
        {
            if (onSettled == null) return;
            EnsureHost();
            s_settleCallbacks.Add(onSettled);
            // ⛔ DELIBERATELY NOT fired synchronously, even when the condition already holds.
            // Callers register from INSIDE the work the callback re-runs (ApplyAll arms it), so a
            // synchronous fire would re-enter that work mid-pass. Host.Update drains it on a later
            // frame, which is also the only place it is safely off any engine callback.
        }

        /// <summary>
        /// Run <paramref name="work"/> on the NEXT frame. The point is to get OFF an engine
        /// callback (SceneManager.sceneLoaded) before touching content: the captured hang
        /// happened inside Internal_SceneLoaded, where the player loop cannot re-enter to drive
        /// the very operation the callback is waiting on. If no host exists yet (edit mode,
        /// batchmode with no player loop) the work runs INLINE — an editor call has no deadlock
        /// to avoid and a silently-dropped action would be worse.
        /// </summary>
        public static void Defer(Action work)
        {
            if (work == null) return;
            EnsureHost();
            if (s_host == null)
            {
                Guard.Try(System, "run deferred work inline (no host)", work);
                return;
            }
            s_deferred.Add(work);
        }

        private static void MaybeNotifySettled()
        {
            if (s_settleCallbacks.Count == 0) return;
            if (!IsSettled) return;
            if (s_inFlight.Count > 0) return;

            var due = s_settleCallbacks.ToArray();
            s_settleCallbacks.Clear();
            foreach (var cb in due)
                Guard.Try(System, "settle callback", () => cb());
        }

        // =====================================================================
        //  The warm pass
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Pi Browser runs inside an Android WebView with a much tighter practical
            // memory ceiling than desktop WebGL. Loading and retaining all 35 prefabs at
            // boot can kill the renderer, which the host recreates as an apparent game
            // reset. On Pi, leave structures strictly on-demand and serialize requests.
            if (WebGLPiPlatform.IsPiBrowserEnvironment)
            {
                s_warmStarted = true;
                State = StructureContentState.Degraded;
                Addressables.WebRequestOverride = request => request.timeout = 20;
                FlowTrace.Step(System,
                    "Pi Browser policy: eager structure download/residency disabled; " +
                    "20s Addressables request timeout installed; assets load on demand.");
                MaybeNotifySettled();
                return;
            }
            // AfterSceneLoad, matching OfflineContentBootstrap: coroutines need a live scene.
            // This is deliberately NOT a barrier — blocking the boot on content is the family
            // of bug this file exists to end.
            EnsureHost();
            StartWarm();
        }

        /// <summary>Start the warm pass if it has not run. Safe to call repeatedly.</summary>
        public static void StartWarm()
        {
            if (s_warmStarted) return;
            s_warmStarted = true;
            EnsureHost();
            if (s_host == null)
            {
                // No player loop (edit mode / batchmode). Nothing to warm from; the editor
                // resolver serves those callers synchronously and safely.
                State = StructureContentState.Degraded;
                FlowTrace.Step(System, "no coroutine host (edit mode) — warm pass skipped; " +
                                       "the editor sync resolver serves editor callers.");
                MaybeNotifySettled();
                return;
            }
            Guard.Try(System, "start structure warm pass", () => s_host.StartCoroutine(WarmRoutine()));
        }

        private static IEnumerator WarmRoutine()
        {
            using var _ = FlowTrace.Enter(System, "WarmRoutine");
            State = StructureContentState.Warming;
            float t0 = Now();

            // --- 1. Addressables init. THIS is where the remote catalog is fetched, and doing
            // it here (async, on the player loop) is what stops it happening inside a scene-load
            // callback via a WaitForCompletion. The old AddressableRegistered<T> probe triggered
            // exactly this initialisation synchronously and never said a word before it did.
            AsyncOperationHandle<IResourceLocator> init = default;
            bool initStarted = Guard.Try(System, "Addressables.InitializeAsync",
                () => { init = Addressables.InitializeAsync(false); });

            if (initStarted)
            {
                while (!init.IsDone && Now() - t0 < WarmDeadlineSeconds) yield return null;
                if (!init.IsDone)
                {
                    // Deliberately NOT released while it is still running, and deliberately NOT
                    // waited on. Leaking one handle beats either alternative.
                    FlowTrace.Warn(System,
                        $"Addressables init still running after {Now() - t0:F1}s — continuing anyway. " +
                        "Nothing is blocked; the town will simply show baked twins until it lands.");
                }
                else
                {
                    FlowTrace.Step(System, $"Addressables init {init.Status} in {Now() - t0:F1}s.");
                    Guard.Try(System, "release init handle", () => Addressables.Release(init));
                }
            }

            // --- 2. Discover every structure address from the in-memory locators. No labels are
            // authored on the Structure_Art group (verified in StructureAddressablesMigrator —
            // it sets a GROUP, and a group name is not an Addressables key), so a label query
            // would silently match nothing. Reading the locators needs no network at all.
            var keys = new List<string>();
            Guard.Try(System, "enumerate structure addresses", () =>
            {
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator?.Keys == null) continue;
                    foreach (var k in locator.Keys)
                    {
                        if (!(k is string s)) continue;
                        s_registeredKeys.Add(s);   // full key set -- powers IsRegisteredAddress
                        if (s.StartsWith(AddressPrefix, StringComparison.Ordinal) && !keys.Contains(s))
                            keys.Add(s);
                    }
                }
            });

            FlowTrace.Step(System, $"warm pass found {keys.Count} structure address(es) under '{AddressPrefix}'.");

            // --- 3. Pull the bundles local. DownloadDependenciesAsync costs disk, not memory,
            // so this is cheap next to loading every prefab — and once the bundle is local the
            // on-demand Request() above resolves from a file instead of the CDN.
            if (keys.Count > 0)
            {
                AsyncOperationHandle dl = default;
                bool dlStarted = Guard.Try(System, "DownloadDependenciesAsync(structures)",
                    () => { dl = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false); });

                if (dlStarted)
                {
                    while (!dl.IsDone && Now() - t0 < WarmDeadlineSeconds) yield return null;

                    if (!dl.IsDone)
                    {
                        FlowTrace.Warn(System,
                            $"structure content still downloading after {Now() - t0:F1}s (deadline {WarmDeadlineSeconds}s) — " +
                            "reporting DEGRADED and moving on. The download is NOT cancelled and NOT waited on; " +
                            "assets that land later still become resident.");
                    }
                    else
                    {
                        FlowTrace.Step(System,
                            $"structure content download {dl.Status} in {Now() - t0:F1}s.");
                        Guard.Try(System, "release download handle", () => Addressables.Release(dl));
                    }
                }
            }

            // --- 4. LOAD AND RETAIN.
            // THIS PHASE IS THE WHOLE POINT AND IT WAS MISSING IN THE FIRST VERSION OF THIS FILE,
            // which shipped to the owner's device on 2026-08-20 and logged, verbatim:
            //     structure content download Succeeded in 2.8s.
            //     warm pass settled as Warm in 2.8s (resident=0, inFlight=0, retained=0).
            // DownloadDependenciesAsync makes the BUNDLES local. It does not put a single ASSET in
            // the resident dictionary. So TryGet missed for every address, the loader fell through
            // to a Resources copy that the CDN migration had DELETED, and build-mode placement
            // ghosts rendered as placeholder PILLS. Warm was a marker claiming a state it had not
            // reached -- the same overclaiming defect class we spent the day removing.
            //
            // COST, MEASURED NOT GUESSED: the Structure_Art group holds exactly 35 addresses
            // (24 of them the catalog's complete visualPrefabPath/upgradeVisualPath set -- full
            // coverage, no gaps) totalling 28.9 MB of source bytes on disk. That is the SAME set
            // DownloadDependenciesAsync already pulled above in 2.8 s on the device. Loading all
            // 35 is therefore the cheap option, not the expensive one, and it removes every reason
            // for a synchronous caller with no retry seam (BuildModeController's ghost) to miss.
            //
            // Request() is reused deliberately: it already dedupes, hooks Completed, indexes into
            // s_resident and retains the handle. One code path, so a late arrival after the
            // deadline still lands instead of being dropped.
            if (keys.Count > 0)
            {
                for (int i = 0; i < keys.Count; i++) Request(keys[i]);
                FlowTrace.Step(System,
                    $"warm pass requested {keys.Count} structure asset(s) for RESIDENCY " +
                    "(downloading a bundle is not the same as holding an asset).");

                while (s_inFlight.Count > 0 && Now() - t0 < WarmDeadlineSeconds) yield return null;

                if (s_inFlight.Count > 0)
                    FlowTrace.Warn(System,
                        $"{s_inFlight.Count} structure asset(s) still loading after {Now() - t0:F1}s -- " +
                        "NOT waited on and NOT cancelled; they still become resident when they land.");
            }

            s_discovered = keys.Count;
            State = DecideState(s_discovered, s_resident.Count);
            FlowTrace.Step(System,
                $"warm pass settled as {State} in {Now() - t0:F1}s (discovered={s_discovered}, " +
                $"resident={s_resident.Count}, inFlight={s_inFlight.Count}, retained={s_retained.Count}).");
            MaybeNotifySettled();
        }

        /// <summary>
        /// The ONE place <see cref="StructureContentState.Warm"/> can be decided, so the reported
        /// state cannot drift from the achieved one.
        /// <para>Warm REQUIRES resident content. On 2026-08-20 this pass reported Warm with
        /// resident=0 and the owner got placeholder pills instead of buildings; nothing checked
        /// whether the marker was true. A state that cannot be falsified is not a state, it is a
        /// wish. StructureLoadBoundedRegression fails the suite if this guard is removed.</para>
        /// </summary>
        private static StructureContentState DecideState(int discovered, int resident)
        {
            if (discovered == 0)
            {
                FlowTrace.Warn(System,
                    $"warm pass found NO '{AddressPrefix}' addresses in the catalog. That is the expected " +
                    "state pre-migration and a DEFECT afterwards (35 are authored in the Structure_Art " +
                    "group today). Reporting DEGRADED -- callers fall back to Resources or keep their " +
                    "current visual.");
                return StructureContentState.Degraded;
            }

            if (resident == 0)
            {
                FlowTrace.Fail(System,
                    $"warm pass discovered {discovered} structure address(es) but NOT ONE is resident. " +
                    "Reporting DEGRADED, never Warm. This is the exact 2026-08-20 'pills loading' " +
                    "regression: bundles downloaded, assets never loaded, TryGet missed everything, " +
                    "and Assets/Resources/Structures no longer exists to catch it. Buildings and " +
                    "placement ghosts render as placeholders until this is fixed.");
                return StructureContentState.Degraded;
            }

            if (resident < discovered)
            {
                FlowTrace.Warn(System,
                    $"warm pass is only PARTIALLY resident ({resident}/{discovered}). The missing " +
                    "addresses render as placeholders or keep their baked twin; each one failed " +
                    "with its own line above. Reporting Warm because the fast path does exist.");
            }

            return StructureContentState.Warm;
        }

        // =====================================================================
        //  Plumbing
        // =====================================================================

        private static string Key(Type t, string address) => t.Name + ":" + address;

        private static float Now() => Time.realtimeSinceStartup;

        private static void EnsureHost()
        {
            if (s_host != null) return;
            if (!Application.isPlaying) return;   // no player loop to host a coroutine on

            Guard.Try(System, "create warm host", () =>
            {
                var go = new GameObject("StructureContentWarmer");
                UnityEngine.Object.DontDestroyOnLoad(go);
                s_host = go.AddComponent<Host>();
            });
        }

        /// <summary>
        /// DontDestroyOnLoad coroutine host. Also drains <see cref="Defer"/> work each frame —
        /// this is the "get off the engine callback" seam, and it must run from Update (the
        /// player loop) for that to mean anything.
        /// </summary>
        private sealed class Host : MonoBehaviour
        {
            private readonly List<Action> _drain = new List<Action>();

            private void Update()
            {
                if (s_deferred.Count > 0)
                {
                    _drain.Clear();
                    _drain.AddRange(s_deferred);
                    s_deferred.Clear();
                    for (int i = 0; i < _drain.Count; i++)
                    {
                        var work = _drain[i];
                        Guard.Try(System, "deferred work", () => work());
                    }
                }

                MaybeNotifySettled();
            }

            private void OnDestroy()
            {
                if (s_host == this) s_host = null;
            }
        }
    }
}
