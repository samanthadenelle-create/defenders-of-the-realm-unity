// =============================================================================
// AddressableUIManager — async UI load / unload via Unity Addressables
// -----------------------------------------------------------------------------
// Manages four label groups:  UI-Core | UI-Debug | UI-Menus | UI-Tower
//
// SETUP (one-time in Unity Editor):
//   1. Window → Asset Management → Addressables → Groups
//   2. Create four groups:  UI-Core, UI-Debug, UI-Menus, UI-Tower
//   3. Assign the label matching the group name to each asset
//   4. Mark DebugCanvas.prefab as Addressable with address "UI/DebugCanvas"
//      and label "UI-Debug"
//   5. Build Addressables (Build → New Build → Default Build Script)
//
// Usage:
//   await AddressableUIManager.Instance.ShowAsync("UI/DebugCanvas", parent);
//   AddressableUIManager.Instance.Hide("UI/DebugCanvas");
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Singleton — async load / unload of UI prefabs via Addressables.
    /// Caches loaded handles so each address is only fetched once per session.
    /// </summary>
    public sealed class AddressableUIManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static AddressableUIManager _instance;
        public  static AddressableUIManager Instance => _instance;

        // ── Addressable group labels — match the names in the Addressables window
        public const string LabelCore  = "UI-Core";
        public const string LabelDebug = "UI-Debug";
        public const string LabelMenus = "UI-Menus";
        public const string LabelTower = "UI-Tower";

        // ── Cached handles: address → (handle, instantiated root GO) ─────────
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _handles =
            new Dictionary<string, AsyncOperationHandle<GameObject>>();
        private readonly Dictionary<string, GameObject> _instances =
            new Dictionary<string, GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseAll();
            if (_instance == this) _instance = null;
        }

        // ── Core API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Loads (first call) or shows (subsequent calls) the prefab at
        /// <paramref name="address"/>. Instantiated under <paramref name="parent"/>
        /// if provided, otherwise under this manager's transform.
        /// Returns the instantiated root, or null on failure.
        /// </summary>
        public async UniTask<GameObject> ShowAsync(string address, Transform parent = null)
        {
            if (_instances.TryGetValue(address, out var existing))
            {
                existing.SetActive(true);
                // WO-976: the RE-SHOW path is where a panel most plausibly comes back buried,
                // zero-sized by a stale layout, or under a scrim that outlived the last screen —
                // and it never touched the verify at all. Measure it too (fire-and-forget, so the
                // cached fast path stays a same-frame return).
                VerifyRendersMeasured(existing, address).Forget();
                return existing;
            }

            return await LoadAndInstantiate(address, parent ?? transform);
        }

        /// <summary>
        /// Deactivates (does NOT unload) the prefab at <paramref name="address"/>.
        /// The handle and instance remain cached for fast re-show.
        /// </summary>
        public void Hide(string address)
        {
            if (_instances.TryGetValue(address, out var go) && go != null)
                go.SetActive(false);
        }

        /// <summary>
        /// Destroys the instance and releases the Addressable handle for
        /// <paramref name="address"/>. Next <see cref="ShowAsync"/> re-fetches.
        /// </summary>
        public void Release(string address)
        {
            if (_instances.TryGetValue(address, out var go))
            {
                if (go != null) Destroy(go);
                _instances.Remove(address);
            }
            if (_handles.TryGetValue(address, out var handle))
            {
                // GUARD (WO-465): a release on an already-invalid handle throws — never let a teardown
                // throw out of Release. Self-report instead of crashing the caller.
                Guard.Try("UI", $"AddressableUIManager.Release '{address}'", () =>
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                });
                _handles.Remove(address);
            }
        }

        /// <summary>Releases all cached handles. Called in OnDestroy.</summary>
        public void ReleaseAll()
        {
            foreach (var go in _instances.Values)
                if (go != null) Destroy(go);
            _instances.Clear();

            // GUARD each release independently (WO-465) so one invalid handle can't abort the rest
            // of the teardown and leak the others.
            foreach (var h in _handles.Values)
            {
                var handle = h;
                Guard.Try("UI", "AddressableUIManager.ReleaseAll handle", () =>
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                });
            }
            _handles.Clear();
        }

        // ── Debug canvas convenience ──────────────────────────────────────────

        /// <summary>
        /// Loads and shows the debug overlay (address <c>"UI/DebugCanvas"</c>,
        /// label <c>UI-Debug</c>). Respects #if UNITY_EDITOR / DEVELOPMENT_BUILD.
        /// </summary>
        public async UniTask<GameObject> ShowDebugCanvasAsync()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return await ShowAsync("UI/DebugCanvas");
#else
            return null;
#endif
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private async UniTask<GameObject> LoadAndInstantiate(string address, Transform parent)
        {
            using var _ = FlowTrace.Enter("UI", $"AddressableUIManager.LoadAndInstantiate '{address}'");

            // GUARD the load (WO-465): a throwing/failed load self-reports via FlowTrace.Fail rather
            // than a Debug.LogError the harness never captures — so a missing UI address is pinpointed.
            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
                await handle;
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: load THREW for '{address}': {ex.GetType().Name}: {ex.Message} — returning null.");
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: load FAILED for '{address}' (status={handle.Status}, result={(handle.Result == null ? "<null>" : "ok")}) — returning null.");
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            GameObject go = null;
            FlowTrace.Try("UI", $"instantiate UI '{address}'", () =>
            {
                go = Instantiate(handle.Result, parent);
                go.name = handle.Result.name;
            });
            if (go == null)
            {
                // Instantiate threw / produced nothing — release the handle and report; do NOT
                // cache a null instance (a blank entry would masquerade as a loaded UI).
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: Instantiate returned null for '{address}' — returning null, releasing handle.");
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            _handles[address]   = handle;
            _instances[address] = go;

            // WIRING VERIFY (WO-465 invisible-scrim class; renamed from "VISIBILITY VERIFY" by WO-976,
            // because that is not what it does): a loaded+instantiated UI can STILL render nothing —
            // inactive root, or no usable draw surface (no UIDocument+PanelSettings for UI Toolkit, and
            // no Canvas for uGUI). "Instantiated" != "wired". Fail-loud (the instance is kept so the
            // caller's own fallback decides, but the run self-reports the surface-less UI).
            // ⚠ This proves CONSTRUCTION only. The visibility claim lives in the MEASURED verify below.
            VerifyInstantiatedRenders(go, address);

            // WO-976: the wiring verify above proves the OBJECTS EXIST — nothing more. The MEASURED
            // verify below is the one that can actually fail on a 0x0 / transparent / offscreen /
            // buried panel. It is deliberately FIRE-AND-FORGET: it waits up to 8 frames for layout to
            // settle, and no caller of ShowAsync should pay that latency to get its GameObject back.
            // (Caller audit, WO-976: there are currently ZERO in-tree callers of ShowAsync /
            // ShowDebugCanvasAsync outside this file, so nothing is being slowed today either way —
            // .Forget() keeps it that way for whoever wires the first one.)
            VerifyRendersMeasured(go, address).Forget();

            FlowTrace.Step("UI", $"AddressableUIManager: loaded + instantiated '{address}'.");
            return go;
        }

        // ── EMIT 1 of 3 (WO-976): WIRING verify — honest language, honest scope ───────────
        // Post-instantiate WIRING verify (WO-465, retokened WO-976). This checks that the surface
        // OBJECTS were constructed: an active root, plus either a UI Toolkit UIDocument bound to a
        // PanelSettings or a uGUI Canvas. That is a real and useful fact — a missing PanelSettings
        // IS a bug — but it is a statement about WIRING, not about anything a player can see.
        //
        // ⚠ WO-976: this emit used to end in `=> hasSurface={hasSurface}` and gate its Fail on that
        // flag, which made it a FALSE GREEN: a panel that is 0x0, fully transparent, entirely
        // offscreen, or buried behind an opaque higher-sorted surface satisfies every check here and
        // printed `hasSurface=True`, SUPPRESSING the Fail below. The token is now `surfaceWired`,
        // which is exactly what the two non-null checks prove and no more; the visibility claim moved
        // to VerifyRendersMeasured, where it is measured against thresholds and can fail.
        private static void VerifyInstantiatedRenders(GameObject go, string address)
        {
            if (go == null)
            {
                FlowTrace.Fail("UI", $"AddressableUIManager: '{address}' instance is null at verify.");
                return;
            }

            bool active = go.activeInHierarchy;

            var doc = go.GetComponentInChildren<UnityEngine.UIElements.UIDocument>(true);
            bool docOk = doc != null && doc.panelSettings != null;

            var canvas = go.GetComponentInChildren<Canvas>(true);
            bool canvasOk = canvas != null;

            // NOT "hasSurface". These are non-null reference checks: the surface components EXIST.
            bool surfaceWired = docOk || canvasOk;

            FlowTrace.Step("UI",
                $"AddressableUIManager WIRING verify '{address}': active={active} uiDocument={(doc == null ? "<none>" : "present")} " +
                $"panelSettings={(docOk ? "present" : "<missing>")} canvas={(canvasOk ? "present" : "<none>")} " +
                $"=> surfaceWired={surfaceWired} (references only — proves NOTHING about visibility; see the MEASURED verify).");

            if (!active || !surfaceWired)
            {
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: '{address}' instantiated but has NO USABLE DRAW SURFACE (active={active}, surfaceWired={surfaceWired}) " +
                    "— no PanelSettings-bound UIDocument and no Canvas (WO-465 invisible-scrim class). Wiring failure, not a layout failure.");
            }
        }

        // ── EMITS 2 and 3 of 3 (WO-976): MEASURED verify, and the MANDATORY NAMED SKIP ────
        // Waits for layout to settle, then measures the values that decide whether a human sees the
        // panel — resolved rect px, resolved opacity, sorting order, viewport intersection — via the
        // shared DeNelle.Core.Diagnostics.UiSurfaceProbe. Each failure class (ZERO_SIZE / TRANSPARENT
        // / OFFSCREEN / BEHIND) emits its OWN named Fail: they are four different bugs with four
        // different fixes and must never collapse into one "panel not visible" line.
        //
        // ⚠ THE BATCHMODE SKIP IS NOT OPTIONAL. Batchmode runs no layout or render pass, so every
        // measurement would read 0 and every headless run would emit four spurious failures — and the
        // next person to see that "fixes" it by weakening the thresholds, which lands us straight back
        // on a hollow line. The skip is therefore NAMED and LOGGED (a Warn saying SKIPPED), never
        // silent and never a pass.
        private static async UniTaskVoid VerifyRendersMeasured(GameObject go, string address)
        {
            string label = $"AddressableUIManager MEASURED verify '{address}'";

            // Named skip BEFORE the frame wait — no point spinning 8 frames in a headless run.
            if (UiSurfaceProbe.IsUnmeasurableEnvironment(out string envReason))
            {
                FlowTrace.Warn("UI",
                    $"{label}: **SKIPPED** — {envReason}. Named skip, not a pass: this run asserts NOTHING about " +
                    "whether the panel is visible. Do NOT weaken the thresholds to make this line go green.");
                return;
            }

            // Layout settles a few frames after instantiate (WandererBubble needed 3; 8 is the ceiling
            // observed in this tree). Poll so a healthy panel costs 1-2 frames and only a genuinely
            // zero-sized one pays the full wait.
            const int MaxSettleFrames = 8;
            UiSurfaceProbe.UiSurfaceMeasure m = default;
            for (int frame = 0; frame < MaxSettleFrames; frame++)
            {
                await UniTask.NextFrame();
                if (go == null)
                {
                    FlowTrace.Warn("UI",
                        $"{label}: **SKIPPED** — the instance was destroyed after {frame + 1} frame(s), before layout settled. " +
                        "Named skip, not a pass.");
                    return;
                }
                m = UiSurfaceProbe.Measure(go);
                if (m.Measurable && !m.ZeroSize) break;   // settled — stop early, don't tax the frame budget
            }

            // Report() emits exactly one of: a named SKIP (Warn), one Fail per failing class, or a
            // MEASURED VISIBLE Step that states the thresholds it cleared.
            UiSurfaceProbe.Report("UI", label, in m);
        }
    }
}
