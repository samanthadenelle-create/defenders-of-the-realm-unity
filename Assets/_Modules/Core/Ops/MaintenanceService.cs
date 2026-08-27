// =============================================================================
// MaintenanceService - WO-1243, the fetch loop for the operator kill switches.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ops
//
// SPLIT OF DUTIES, the same one DungeonStatusService keeps: MaintenanceCatalog
// is pure state plus parse and knows nothing about transport. THIS file owns
// transport and ONLY transport, so the catalog stays headlessly drivable by the
// regression oracle with no network and no PlayMode.
//
// -----------------------------------------------------------------------------
// THE POLL INTERVAL IS AN EXPOSURE WINDOW. That is the whole design.
// -----------------------------------------------------------------------------
// Owner ruling 2026-08-27: "mine allows if we see someone finds a hack, we seal
// that area and patch". A boot-time read seals NOTHING for a player already in
// session - which is precisely the person exploiting the area. So this polls.
//
// PollSeconds = 30. Reasoning, stated so it can be argued with rather than
// guessed at later:
//   * Worst case an honest client keeps playing a sealed area for 30 s (poll)
//     plus up to 10 s of edge cache on /api/maintenance, so about 40 s.
//   * Six small rows behind a 10 s s-maxage edge cache: at any plausible
//     concurrent-player count for this game the origin sees a handful of reads
//     per minute, not one per player per poll.
//   * Faster buys little, because the number that actually matters is the
//     SERVER side one - api/_lib/maintenance.js memoises for 5 s, so the real
//     seal lags at most about 5 s no matter what this client does. The client
//     poll only decides how quickly honest players are TOLD.
// If the owner ever wants the sign to catch up faster, this constant is the
// knob, and it is one number in one place.
//
// DO NOT: NO CACHE. Owner-ruled and she was shown the consequence: an offline player
// falls back to the default, which under fail-open means everything is open.
// This file deliberately has NO CachePath, NO file write and NO PlayerPrefs
// mirror - unlike DungeonStatusService, which caches because it fails CLOSED and
// needs offline continuity. Do NOT add one "to be safe".
//
// FAIL-OPEN on every transport failure: threw, non-2xx, TIMED OUT, unparseable.
// The standing table is left alone (or, at boot, never established), which means
// every area is open. See MaintenanceCatalog's header for the owner's reasoning
// in her own words.
//
// THREE HTTP IDIOMS, all three because of real production bugs (the same three
// DungeonStatusService documents at length):
//   1. `using var req` - dispose the handler.
//   2. `req.timeout`   - without it a captive-portal socket never completes.
//   3. try/catch around the await AND a separate `req.result` check: the UniTask
//      awaiter THROWS on non-2xx (WO-769), so checking only one is the bug.
//
// DO NOT: NO AUTH. /api/maintenance is public read and must resolve before sign-in - a
// full `server` window has to be announceable at the title screen. Do not call
// BackendRequestSigner from here.
//
// ASCII only. Instrumentation: FlowTrace tag "Maintenance". Never strip it.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Ops
{
    /// <summary>
    /// Boots and maintains the operator kill-switch table: one fetch at load,
    /// then a poll forever. Never blocks, never throws outward, never caches.
    /// </summary>
    public static class MaintenanceService
    {
        private const string Sys = MaintenanceCatalog.Sys;

        /// <summary>House pattern: the backend base is a private const per file
        /// (GameStateService.cs, BackendRequestSigner.cs, DungeonStatusService.cs and
        /// nine others). Do NOT refactor the duplicates as part of WO-1243.</summary>
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";

        private const string EndpointPath = "/api/maintenance";

        /// <summary>Without a timeout a captive-portal socket never completes and the
        /// request hangs for the whole session.</summary>
        private const int RequestTimeoutSeconds = 10;

        /// <summary>THE EXPOSURE WINDOW. See the header for why it is 30 and not 5.</summary>
        public const int PollSeconds = 30;

        /// <summary>Lets scene Awake()s run before the first network yield.</summary>
        private const int FirstYieldDelayMs = 200;

        /// <summary>Name of the kill-switch key, for logs only. The VALUE is read
        /// through <see cref="FeatureFlags.Maintenance"/> and nowhere else - a second
        /// reader of one key is CLAUDE.md's duplicated-state failure.</summary>
        private const string FlagKey = "ff.maintenance";

        /// <summary>False stops the poll and leaves every area open on the client.
        /// It does NOT reopen a server-side seal - see FeatureFlags.Maintenance.</summary>
        public static bool Enabled => FeatureFlags.Maintenance;

        /// <summary>The live endpoint. Public so the dev menu / oracle can name it.</summary>
        public static string Endpoint => BackendBase + EndpointPath;

        /// <summary>Realtime seconds of the last ACCEPTED payload, or 0. Read by the
        /// oracle and by the dev menu; a stale value here means the poll has stopped.</summary>
        public static float LastPayloadAt { get; private set; }

        /// <summary>Consecutive failed fetches. Reset by any accepted payload. Surfaced
        /// so a support question ("is the sign even updating?") has an answer.</summary>
        public static int ConsecutiveFailures { get; private set; }

        private static bool s_booted;

        // ---------------------------------------------------------------------
        //  Boot
        // ---------------------------------------------------------------------

        /// <summary>
        /// AfterSceneLoad, NOT BeforeSceneLoad - the latter runs before any scene
        /// exists. Mirrors DungeonStatusService.Bootstrap so both arrive on the same
        /// tick ordering the rest of the system assumes.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_booted) return;
            s_booted = true;

            if (!Enabled)
            {
                MaintenanceCatalog.Clear(MaintenanceCatalog.ProvenanceFlagOff);
                FlowTrace.Step(Sys, "kill switch off (" + FlagKey + "=0) - the client courtesy gate " +
                                    "and banner are suppressed and no poll runs. The SERVER-SIDE seal " +
                                    "(api/_lib/maintenance.js) is unaffected by this flag.");
                return;
            }

            // NOTHING AWAITS THIS. The title screen must never wait on a network call;
            // non-blocking here is STRUCTURAL (no await at the call site), not a comment.
            PollForeverAsync().Forget();
        }

        // ---------------------------------------------------------------------
        //  The poll
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fetch, then fetch again every <see cref="PollSeconds"/>, forever. Fire and
        /// forget. Every failure is fail-OPEN and every failure is logged.
        /// </summary>
        public static async UniTaskVoid PollForeverAsync()
        {
            await UniTask.Delay(FirstYieldDelayMs);

            while (Enabled)
            {
                await RefreshOnceAsync();
                await UniTask.Delay(PollSeconds * 1000);
            }

            FlowTrace.Warn(Sys, "poll stopped: " + FlagKey + " was turned off mid-session. Every area " +
                                "is OPEN on this client from here on; the server-side seal is unaffected.");
        }

        /// <summary>
        /// One fetch. Public so the dev menu and a headless probe can force a refresh
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
                    // The UniTask awaiter THROWS on non-2xx (WO-769). Both this catch
                    // AND the result check below are required.
                    await req.SendWebRequest();
                }
                catch (Exception ex)
                {
                    LogFetchFailure("fetch threw (" + req.responseCode + ") " + ex.GetType().Name);
                    return;
                }

                // A 404 IS NOT AN OUTAGE. The endpoint answered, and it said this feature
                // is not deployed here - so no toggle row exists, nothing was ever sealed,
                // and everything is OPEN including the store. Treating it as
                // unreachable sealed a live store on 2026-08-27, permanently for that
                // build, because a 404 never stops being a 404. See
                // MaintenanceCatalog.MarkFeatureAbsent for the full reasoning and why this
                // does not weaken the seal (the store's real enforcement is server-side in
                // api/purchases/quote.js).
                if (req.responseCode == 404)
                {
                    MaintenanceCatalog.MarkFeatureAbsent();
                    FlowTrace.Warn(Sys, "GET " + EndpointPath + " -> 404. The toggle endpoint is NOT DEPLOYED. " +
                                        "Every area resolves OPEN, the store included - a 404 is the server " +
                                        "saying the feature is absent, not that it is unreachable, and an " +
                                        "absent toggle table holds no seals. A TIMEOUT or 5xx is different " +
                                        "and still fails the store CLOSED. Deploy api/maintenance.js to " +
                                        "restore operator control.");
                    return;
                }

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

                if (!MaintenanceCatalog.ApplyPayload(body, MaintenanceCatalog.ProvenanceLive))
                {
                    LogFetchFailure("live payload rejected after " + ms + " ms");
                    return;
                }

                ConsecutiveFailures = 0;
                LastPayloadAt = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// One voice for every fetch failure (threw / non-2xx / TIMED OUT / rejected).
        /// <para>
        /// Warn, not Fail, and the severity is the ruling: under fail-open a failed
        /// fetch does not close anything, so it is not an outage for the player. It IS
        /// worth saying out loud, because it means the owner's seal would not reach
        /// this client - which is exactly the trade she accepted knowingly.
        /// </para>
        /// CLAUDE.md section 12 - never let this path go quiet.
        /// </summary>
        private static void LogFetchFailure(string what)
        {
            ConsecutiveFailures++;
            FlowTrace.Warn(Sys, what + " - every area stays OPEN on this client (fail-open, owner ruling " +
                                "2026-08-27: \"i cannot help if server is unreachable\"). " +
                                "consecutiveFailures=" + ConsecutiveFailures +
                                " standingRows=" + MaintenanceCatalog.RowCount +
                                " provenance=" + MaintenanceCatalog.Provenance +
                                ". NOTE: the server-side seal in api/_lib/maintenance.js is unaffected " +
                                "by this client's ability to read the sign.");
        }
    }
}
