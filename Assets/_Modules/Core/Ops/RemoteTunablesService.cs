// =============================================================================
// RemoteTunablesService - PROD-022, the fetch loop for the database-backed knobs.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ops
//
// SPLIT OF DUTIES, the same one MaintenanceCatalog / MaintenanceService keeps:
// RemoteTunables is pure state plus parse and knows nothing about transport. THIS
// file owns transport and ONLY transport, so the knob table stays headlessly
// drivable by a regression oracle with no network and no PlayMode.
//
// -----------------------------------------------------------------------------
// ⛔ THIS IS A CRASH-LOOP TICKET. ADDING A BOOT FAILURE MODE WOULD BE SELF-DEFEATING.
// -----------------------------------------------------------------------------
// So the non-blocking property is STRUCTURAL, not a comment:
//   * Bootstrap() calls PollForeverAsync().Forget() - there is NO await at the
//     call site, so nothing downstream of boot can wait on this.
//   * There is no barrier, no WaitForCompletion, no "wait for the first payload"
//     anywhere in the codebase. Grep for RemoteTunablesService and you will find
//     one Forget() and nothing that yields on it.
//   * Every knob answers instantly from RemoteTunables, whose answer with no data
//     is the SHIPPING DEFAULT. Nothing ever waits for a value.
//   * req.timeout is set, because without it a captive-portal socket never
//     completes and the request hangs for the whole session.
//
// -----------------------------------------------------------------------------
// ⭐ THIS ONE *DOES* CACHE, AND THAT IS A DELIBERATE DIVERGENCE FROM MaintenanceService.
// -----------------------------------------------------------------------------
// MaintenanceService has NO cache, owner-ruled, because a stale kill switch is a
// safety question and an offline player must fall back to "everything is open".
// That ruling is about SEALS. It does not transfer here, and copying it would
// break the one thing this file exists for:
//
//   The knobs that matter most to PROD-022 are read DURING BOOT (the Pi
//   Addressables policy is decided in StructureContentWarmer.Boot, at
//   AfterSceneLoad). A value that only arrives after a network round trip would
//   therefore be too late on the very launch it was set for, on every launch,
//   forever. And PROD-022's symptom is that the app RELAUNCHES every 30-60s -
//   so the cache is not a nicety here, it is what makes a boot-time knob
//   testable at all: flip it, the next crash-relaunch reads it at frame zero.
//
// The cache can only ever hold values that CAME FROM the database, and a fresh
// payload REPLACES it wholesale, so it cannot resurrect a knob the owner turned
// off. A 404 (the feature is not deployed) CLEARS it. A corrupt cache is rejected
// by the same Guard-wrapped parse as a live payload and leaves every knob at its
// shipping default.
//
// The cache is read at BeforeSceneLoad and the poll starts at AfterSceneLoad.
// That ordering is load-bearing: Unity runs every BeforeSceneLoad hook before
// every AfterSceneLoad one, so cached knobs are resolved BEFORE
// StructureContentWarmer.Boot asks for them. The cache read is a PlayerPrefs
// string and a JSON parse - no network, nothing that can stall.
//
// -----------------------------------------------------------------------------
// FAIL-TO-DEFAULT on every transport failure: threw, non-2xx, TIMED OUT,
// unparseable, offline. Not "fail-open" and not "fail-closed" - there is no seal
// here. The safe ground state is simply TODAY'S SHIPPING BEHAVIOUR, and it is
// what every failure path resolves to.
//
// THREE HTTP IDIOMS, all three because of real production bugs (the same three
// MaintenanceService and DungeonStatusService document at length):
//   1. `using var req` - dispose the handler.
//   2. `req.timeout`   - without it a captive-portal socket never completes.
//   3. try/catch around the await AND a separate `req.result` check: the UniTask
//      awaiter THROWS on non-2xx (WO-769), so checking only one is the bug.
//
// NO AUTH. /api/client-tunables is public read and must resolve before sign-in -
// these knobs govern boot-time asset policy, which happens long before any
// identity exists. Do not call BackendRequestSigner from here.
//
// ASCII only. Instrumentation: FlowTrace tag "Tunables". Never strip it.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Ops
{
    /// <summary>
    /// Boots and maintains the PROD-022 knob table: a cached read at frame zero,
    /// one fetch shortly after load, then a poll forever. Never blocks, never
    /// throws outward, and every failure resolves to the shipping default.
    /// </summary>
    public static class RemoteTunablesService
    {
        private const string Sys = RemoteTunables.Sys;

        /// <summary>House pattern: the backend base is a private const per file
        /// (MaintenanceService.cs, GameStateService.cs, DungeonStatusService.cs and
        /// nine others). Do NOT refactor the duplicates as part of PROD-022.</summary>
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";

        private const string EndpointPath = "/api/client-tunables";

        /// <summary>Without a timeout a captive-portal socket never completes and the
        /// request hangs for the whole session.</summary>
        private const int RequestTimeoutSeconds = 10;

        /// <summary>
        /// How often the knob table is re-read. 30 s, matching MaintenanceService.
        /// <para>These knobs are flipped by a human during a bisect, so this is the
        /// TURNAROUND on "flip it and tell me when to look" - and 30 s plus the 10 s
        /// edge cache keeps that under a minute. It is one number in one place.</para>
        /// </summary>
        public const int PollSeconds = 30;

        /// <summary>Lets scene Awake()s run before the first network yield.</summary>
        private const int FirstYieldDelayMs = 200;

        /// <summary>PlayerPrefs key holding the last accepted payload. See the header
        /// for why this file caches and MaintenanceService deliberately does not.</summary>
        public const string CacheKey = "tunables.cache.v1";

        /// <summary>The live endpoint. Public so a dev menu / oracle can name it.</summary>
        public static string Endpoint => BackendBase + EndpointPath;

        /// <summary>Realtime seconds of the last ACCEPTED live payload, or 0.</summary>
        public static float LastPayloadAt { get; private set; }

        /// <summary>Consecutive failed fetches. Reset by any accepted payload.</summary>
        public static int ConsecutiveFailures { get; private set; }

        private static bool s_cacheLoaded;
        private static bool s_booted;

        // ---------------------------------------------------------------------
        //  Boot - two hooks, and the ORDER between them is the design
        // ---------------------------------------------------------------------

        /// <summary>
        /// BeforeSceneLoad: read the cached payload off the device. NO NETWORK.
        /// <para>
        /// ⭐ THE ORDERING IS LOAD-BEARING. Unity runs every BeforeSceneLoad hook before
        /// every AfterSceneLoad one, and StructureContentWarmer.Boot - which decides the
        /// whole Pi Addressables policy - is AfterSceneLoad. So a knob set yesterday is
        /// already resolved by the time that decision is made. Without this hook every
        /// boot-time knob would be a launch too late, permanently.
        /// </para>
        /// <para>
        /// Cost is one PlayerPrefs string read plus one JSON parse, both Guarded. There is
        /// nothing here that can stall, and a corrupt cache resolves every knob to its
        /// shipping default rather than to anything at all.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadCache()
        {
            if (s_cacheLoaded) return;
            s_cacheLoaded = true;

            string cached = Guard.Try<string>(Sys, "read tunables cache",
                () => PlayerPrefs.GetString(CacheKey, null), null);

            if (string.IsNullOrWhiteSpace(cached))
            {
                FlowTrace.Step(Sys, "no cached tunables payload on this device - every knob starts at " +
                                    "its SHIPPING DEFAULT (today's behaviour). The first live fetch, if " +
                                    "it lands, may override some of them.");
                RemoteTunables.LogConfiguration("boot, no cache");
                return;
            }

            if (ApplyCachedPayload(cached))
            {
                FlowTrace.Step(Sys, "cached tunables payload applied at BeforeSceneLoad (rows=" +
                                    RemoteTunables.RowCount + "). This is what boot-time knobs read; a " +
                                    "live payload arriving later replaces it wholesale.");
            }
            else
            {
                // Reject AND discard: a cache we cannot parse is worse than none, because it
                // would be re-rejected on every launch until something overwrites it.
                Guard.Try(Sys, "discard unparseable tunables cache", () =>
                {
                    PlayerPrefs.DeleteKey(CacheKey);
                    PlayerPrefs.Save();
                });
                FlowTrace.Warn(Sys, "cached tunables payload was UNPARSEABLE and has been DISCARDED. " +
                                    "Every knob resolves to its shipping default for this launch.");
                RemoteTunables.LogConfiguration("boot, cache discarded");
            }
        }

        /// <summary>
        /// Apply a cached payload string. PURE: no PlayerPrefs, no network, no side effect
        /// beyond the standing table. Returns false when the string is unusable.
        /// <para>
        /// ⭐ IT IS A SEPARATE, PUBLIC METHOD SO THE ORACLE CAN DRIVE IT. The corrupt-cache
        /// path is one of the failure modes that must land on the shipping default, and a
        /// path only reachable from a <c>[RuntimeInitializeOnLoadMethod]</c> hook can be
        /// tested once by hand and never again. RemoteTunablesDefaultsRegression case
        /// [failure-modes] drives this directly, with no PlayerPrefs and no network — see
        /// CLAUDE.md §12: a claim nothing can falsify is not a claim.
        /// </para>
        /// </summary>
        public static bool ApplyCachedPayload(string cached)
        {
            return RemoteTunables.ApplyPayload(cached, RemoteTunables.ProvenanceCache);
        }

        /// <summary>
        /// AfterSceneLoad, matching MaintenanceService so both arrive on the same tick
        /// ordering the rest of the system assumes. Starts the poll and returns.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_booted) return;
            s_booted = true;

            // NOTHING AWAITS THIS. Non-blocking here is STRUCTURAL (no await at the call
            // site), not a comment - and on a crash-loop ticket that property is the whole
            // reason this is safe to ship.
            PollForeverAsync().Forget();
        }

        // ---------------------------------------------------------------------
        //  The poll
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fetch, then fetch again every <see cref="PollSeconds"/>, forever. Fire and
        /// forget. Every failure resolves to the shipping default and every failure is logged.
        /// </summary>
        public static async UniTaskVoid PollForeverAsync()
        {
            await UniTask.Delay(FirstYieldDelayMs);

            while (true)
            {
                await RefreshOnceAsync();
                await UniTask.Delay(PollSeconds * 1000);
            }
        }

        /// <summary>
        /// One fetch. Public so a dev menu or a headless probe can force a refresh
        /// without waiting out the poll interval. Never throws.
        /// </summary>
        public static async UniTask RefreshOnceAsync()
        {
            string url = Endpoint;
            float startedAt = Time.realtimeSinceStartup;

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = RequestTimeoutSeconds;

                try
                {
                    // The UniTask awaiter THROWS on non-2xx (WO-769). Both this catch AND
                    // the result check below are required; checking one is the bug.
                    await req.SendWebRequest();
                }
                catch (Exception ex)
                {
                    if (AcceptAbsent404(req)) return;
                    LogFetchFailure("fetch threw (" + req.responseCode + ") " + ex.GetType().Name);
                    return;
                }

                if (AcceptAbsent404(req)) return;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // Covers the TIMEOUT path: req.timeout expiring surfaces here as
                    // Result.ConnectionError, not as an exception.
                    LogFetchFailure("fetch failed (" + req.responseCode + ") " + req.result +
                                    ": " + (req.error ?? "no error text"));
                    return;
                }

                string body = req.downloadHandler != null ? req.downloadHandler.text : null;
                int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - startedAt) * 1000f);

                if (!RemoteTunables.ApplyPayload(body, RemoteTunables.ProvenanceRemote))
                {
                    LogFetchFailure("live payload rejected after " + ms + " ms");
                    return;
                }

                ConsecutiveFailures = 0;
                LastPayloadAt = Time.realtimeSinceStartup;
                WriteCache();
            }
        }

        /// <summary>
        /// Persist the accepted table so the NEXT launch resolves boot-time knobs at frame
        /// zero. Wholesale replace, never a merge - a knob the owner cleared must disappear.
        /// Never throws: PlayerPrefs on a hardened WebGL host can refuse.
        /// </summary>
        private static void WriteCache()
        {
            string json = RemoteTunables.SerializeStandingTable();
            if (string.IsNullOrEmpty(json)) return;

            Guard.Try(Sys, "write tunables cache", () =>
            {
                PlayerPrefs.SetString(CacheKey, json);
                PlayerPrefs.Save();   // WebGL flushes to IndexedDB only on Save().
            });
        }

        /// <summary>
        /// A 404 is a completed HTTP conversation: the tunables endpoint is not deployed.
        /// That is NOT unreachability, and the two must not be conflated - the identical
        /// mistake sealed a live store on 2026-08-27 (see MaintenanceCatalog.MarkFeatureAbsent).
        /// <para>
        /// The endpoint being absent means NO knob is set anywhere, so the standing table is
        /// cleared AND the on-device cache is dropped: keeping a cache for a feature the
        /// server says does not exist would let a knob outlive the system that set it.
        /// Every knob then resolves to its shipping default, i.e. today's behaviour.
        /// </para>
        /// Call from BOTH the UniTask throw catch and the success-path status check - WO-769
        /// throws on non-2xx, so a check placed only after the await never runs on device.
        /// </summary>
        private static bool AcceptAbsent404(UnityWebRequest req)
        {
            if (req == null || req.responseCode != 404) return false;

            RemoteTunables.Clear(RemoteTunables.ProvenanceDefault);
            Guard.Try(Sys, "drop tunables cache (endpoint absent)", () =>
            {
                PlayerPrefs.DeleteKey(CacheKey);
                PlayerPrefs.Save();
            });

            ConsecutiveFailures = 0;
            LastPayloadAt = Time.realtimeSinceStartup;
            FlowTrace.Warn(Sys, "GET " + EndpointPath + " -> 404. The tunables endpoint is NOT DEPLOYED. " +
                                "Every knob resolves to its SHIPPING DEFAULT and the on-device cache has " +
                                "been dropped - a 404 is the server saying the feature is absent, not " +
                                "that it is unreachable, and an absent table holds no knob. Deploy " +
                                "api/client-tunables.js to restore remote control. NOTHING IS BROKEN " +
                                "meanwhile: this is exactly today's behaviour.");
            RemoteTunables.LogConfiguration("endpoint 404");
            return true;
        }

        /// <summary>
        /// One voice for every fetch failure (threw / non-2xx / TIMED OUT / rejected).
        /// <para>
        /// Warn, not Fail, and the severity is the ruling: a failed fetch changes NOTHING
        /// about how the game behaves - every knob was already answering, and it keeps
        /// answering the same value. It is worth saying out loud only because it means the
        /// owner's flag flip would not reach this client, which matters during a bisect.
        /// </para>
        /// CLAUDE.md section 12 - never let this path go quiet.
        /// </summary>
        private static void LogFetchFailure(string what)
        {
            ConsecutiveFailures++;
            FlowTrace.Warn(Sys, what + " - every knob keeps its current resolved value; with no accepted " +
                                "payload that is the SHIPPING DEFAULT, i.e. today's behaviour. " +
                                "consecutiveFailures=" + ConsecutiveFailures +
                                " rows=" + RemoteTunables.RowCount +
                                " tableProvenance=" + RemoteTunables.TableProvenance +
                                ". A flag flipped in the database will NOT reach this client until a " +
                                "fetch succeeds.");
        }
    }
}
