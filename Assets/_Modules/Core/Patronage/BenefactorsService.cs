// =============================================================================
// BenefactorsService - WO-1073, transport for the Benefactors of the Realm wall.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Patronage
//
// Owns transport and ONLY transport. All state, parse and policy live in
// BenefactorsCatalog, which is why that file can be driven headlessly by the
// oracle with no network. Same split MaintenanceService / MaintenanceCatalog
// keeps (WO-1243), for the same reason.
//
// -----------------------------------------------------------------------------
// THIS DOES NOT POLL. That is a decision, not an omission.
// -----------------------------------------------------------------------------
// MaintenanceService polls because its subject is an EXPOSURE WINDOW - a player
// inside a sealed area needs telling within seconds. This subject is an honour
// roll that changes when somebody spends $500, which is measured in days. So the
// wall is fetched ON DEMAND, when the player opens it, plus a small
// anti-hammer cooldown so a player tapping the monument repeatedly does not
// issue a request per tap. A poll here would be a request per player per
// interval, forever, for a list that almost never changes.
//
// -----------------------------------------------------------------------------
// NO AUTH. The endpoint is public and unauthenticated by REQUIREMENT: "every
// kingdom can see it" (owner ruling 2026-08-27). Sending a signed request here
// would make the wall per-player, which is the exact defect the ruling exists to
// fix. Do NOT call BackendRequestSigner from this file.
// -----------------------------------------------------------------------------
//
// THREE HTTP IDIOMS, all three because of real production bugs in this repo
// (documented at length in DungeonStatusService and MaintenanceService):
//   1. `using var req` - dispose the handler.
//   2. `req.timeout`   - without it a captive-portal socket never completes.
//   3. try/catch around the await AND a separate `req.result` check: the UniTask
//      awaiter THROWS on non-2xx (WO-769), so checking only one is the bug.
//
// NO CACHE ON DEVICE. The wall is public, tiny and non-authoritative; a cached
// copy would let a stale honour roll outlive a name edit or a moderation action,
// on a surface whose whole point is that it is the same in every kingdom.
//
// ASCII only. Instrumentation: FlowTrace tag "Benefactors". Never strip it.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Patronage
{
    /// <summary>Fetches the global wall on demand. Never blocks, never throws outward.</summary>
    public static class BenefactorsService
    {
        private const string Sys = BenefactorsCatalog.Sys;

        /// <summary>House pattern: the backend base is a private const per file
        /// (GameStateService.cs, BackendRequestSigner.cs, MaintenanceService.cs and
        /// others). Do NOT refactor the duplicates as part of WO-1073.</summary>
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";

        private const string EndpointPath = "/api/patronage/benefactors";

        /// <summary>Without a timeout a captive-portal socket never completes and the
        /// request hangs for the whole session.</summary>
        private const int RequestTimeoutSeconds = 10;

        /// <summary>Anti-hammer floor: repeated opens inside this window reuse the standing
        /// wall instead of issuing another request. Small enough that a player who walks
        /// away and comes back gets fresh data.</summary>
        public const int MinSecondsBetweenFetches = 20;

        /// <summary>The live endpoint. Public so the dev menu / oracle can name it.</summary>
        public static string Endpoint => BackendBase + EndpointPath;

        /// <summary>Realtime seconds of the last ACCEPTED payload, or 0.</summary>
        public static float LastPayloadAt { get; private set; }

        /// <summary>Consecutive failed fetches. Reset by any accepted payload.</summary>
        public static int ConsecutiveFailures { get; private set; }

        private static float s_lastAttemptAt;
        private static bool s_inFlight;

        /// <summary>
        /// The call the panel makes when it opens. Fire-and-forget: nothing waits on the
        /// network, the panel renders the standing wall immediately and re-renders off
        /// BenefactorsCatalog.Changed when the answer lands.
        /// </summary>
        public static void RequestRefresh()
        {
            if (s_inFlight)
            {
                FlowTrace.Step(Sys, "refresh already in flight - not issuing a second request.");
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (BenefactorsCatalog.EverRead && s_lastAttemptAt > 0f &&
                now - s_lastAttemptAt < MinSecondsBetweenFetches)
            {
                FlowTrace.Step(Sys, "refresh suppressed: last attempt was " +
                                    (now - s_lastAttemptAt).ToString("F1") + "s ago, floor is " +
                                    MinSecondsBetweenFetches + "s. Showing the standing wall (rows=" +
                                    BenefactorsCatalog.Count + ").");
                return;
            }

            RefreshOnceAsync().Forget();
        }

        /// <summary>
        /// One fetch. Public so a headless probe or the dev menu can force one without
        /// waiting out the cooldown. Never throws.
        /// </summary>
        public static async UniTask RefreshOnceAsync()
        {
            if (s_inFlight) return;
            s_inFlight = true;
            s_lastAttemptAt = Time.realtimeSinceStartup;

            string url = Endpoint + "?limit=" + BenefactorsCatalog.DefaultRowLimit;
            float startedAt = Time.realtimeSinceStartup;

            try
            {
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
                        Fail("fetch threw (" + req.responseCode + ") " + ex.GetType().Name);
                        return;
                    }

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        // Covers the TIMEOUT path: req.timeout expiring surfaces here as
                        // Result.ConnectionError, not as an exception.
                        Fail("fetch failed (" + req.responseCode + ") " + req.result + ": " +
                             (req.error ?? "no error text"));
                        return;
                    }

                    string body = req.downloadHandler != null ? req.downloadHandler.text : null;
                    int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - startedAt) * 1000f);

                    if (!BenefactorsCatalog.ApplyPayload(body))
                    {
                        Fail("payload rejected by the catalog after a 200 in " + ms + " ms");
                        return;
                    }

                    LastPayloadAt = Time.realtimeSinceStartup;
                    ConsecutiveFailures = 0;
                    FlowTrace.Step(Sys, "wall fetched in " + ms + " ms from " + EndpointPath +
                                        " - rows=" + BenefactorsCatalog.Count + ".");
                }
            }
            finally
            {
                s_inFlight = false;
            }
        }

        /// <summary>
        /// One place that records a transport failure, so the count and the trace can
        /// never disagree. NEVER silent (CLAUDE.md section 12): a wall that quietly stops
        /// updating is indistinguishable from a wall with nobody on it.
        /// </summary>
        private static void Fail(string why)
        {
            ConsecutiveFailures++;
            BenefactorsCatalog.MarkFetchFailed(why + " [consecutive=" + ConsecutiveFailures + "]");
        }
    }
}
