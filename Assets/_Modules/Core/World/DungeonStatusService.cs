// =============================================================================
// DungeonStatusService — WO-1114, the fetch/cache lifecycle for the door state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// SPLIT OF DUTIES: DungeonStatusCatalog is pure state + parse and knows nothing
//   about transport. THIS file owns transport and ONLY transport, so the
//   catalog stays headlessly drivable by the regression oracle.
//
// RESOLUTION ORDER — live -> cached -> ALL-CLOSED (owner ruling 2026-08-26, WO-1223,
//   which inverted WO-1114 §4d's all-open tail), realised as:
//
//   Bootstrap()  [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
//     |
//     kill switch off? -> Catalog.Clear("flag-off"); ALL OPEN; return (no fetch)
//     |
//     (1) LOAD CACHE SYNCHRONOUSLY  - one small local file. Table is populated
//         at frame 0, so a device that has NEVER reached the network still gets
//         the last good answer.
//         ⛔ CORRECTED 2026-08-26 (owner ruling, WO-1223). This line used to end
//         "a miss leaves the table empty = ALL OPEN, which is exactly the game as
//         it ships today." That is now the OPPOSITE of the truth: a cache miss
//         leaves NO table, and with no table every GATED dungeon resolves CLOSED
//         (DungeonStatusCatalog.For branch d). The cache is therefore no longer a
//         nicety - it is the offline continuity path. The kill switch
//         (FeatureFlags.DungeonStatus = 0) is the lever that reopens everything.
//     |
//     (2) RefreshAsync().Forget()   <-- NOTHING AWAITS THIS.
//         The title screen must never wait on a network call. Non-blocking here
//         is STRUCTURAL (no await at the call site), not a comment.
//
// ⚠ WRITE-AFTER-PARSE IS LOAD-BEARING. The cache file is written only AFTER
//   ApplyPayload has accepted the body. A malformed live payload therefore can
//   never poison the cache and brick the door state across restarts.
//
// ⚠ THREE HTTP IDIOMS, ALL THREE BECAUSE OF REAL PRODUCTION BUGS:
//   1. `using var req` — dispose the handler.
//   2. `req.timeout` — without it a captive-portal socket never completes.
//      Reasoning written out at GameStateService.cs:1152-1165.
//   3. try/catch around `await req.SendWebRequest()` AND a separate
//      `req.result != Success` check. The UniTask awaiter THROWS on non-2xx
//      (WO-769, GameStateService.cs:1421-1431) — checking only one is the bug.
//
// ⛔ NO AUTH. This endpoint is public read and must resolve before sign-in
//   (WO-1114 §5). Do not call BackendRequestSigner from here.
//
// Instrumentation: FlowTrace tag "DungeonStatus". No silent catches (§12) —
//   every failure path below logs. ⛔ Never strip these calls.
// =============================================================================

using System;
using System.IO;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.World
{
    /// <summary>
    /// Boots the dungeon door state: cache first (synchronous, local), then a
    /// fire-and-forget live refresh. Never blocks, never throws outward.
    /// </summary>
    public static class DungeonStatusService
    {
        private const string Sys = DungeonStatusCatalog.Sys;

        /// <summary>House pattern: the backend base is a private const per file
        /// (GameStateService.cs:1145, BackendRequestSigner.cs:50, and nine others).
        /// ⛔ Do NOT refactor the eleven duplicates as part of WO-1114.</summary>
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";

        private const string EndpointPath = "/api/dungeon-status";

        /// <summary>Matches GameStateService.cs:1166. Without a timeout a captive-portal
        /// socket never completes and the request hangs for the whole session.</summary>
        private const int RequestTimeoutSeconds = 15;

        private const string CacheFileName = "dungeon-status-cache.json";

        /// <summary>Lets scene Awake()s run before the first network yield
        /// (PersistenceBridge.cs:117-129 idiom).</summary>
        private const int FirstYieldDelayMs = 200;

        // ─────────────────────────────────────────────────────────────────────
        //  Kill switch — ONE authority, and it is FeatureFlags
        // ─────────────────────────────────────────────────────────────────────
        //
        // 2026-08-21: the flag reached its sanctioned home. This file used to carry
        // an INLINED PlayerPrefs read of "ff.dungeonstatus" because FeatureFlags.cs
        // was lane-fenced and FeatureFlags.Get is private. That copy is DELETED —
        // two readers of one key is the duplicated-state failure CLAUDE.md
        // catalogues three times over (the stale WO block, the retired dependency
        // table, the hardcoded repo root). ⛔ Never re-inline it.
        //
        // Semantics are unchanged: PlayerPrefs "ff.dungeonstatus" — 0 = off,
        // 1 = on, -1/absent = the compiled default (ON).

        /// <summary>Name of the key, for logs only. The VALUE is read through
        /// <see cref="FeatureFlags.DungeonStatus"/> and nowhere else.</summary>
        private const string FlagKey = "ff.dungeonstatus";

        /// <summary>False forces every door OPEN with no rebuild — the kill switch
        /// if a bad payload ever locks content.</summary>
        public static bool Enabled => FeatureFlags.DungeonStatus;

        /// <summary>Absolute path of the last-good payload cache.</summary>
        public static string CachePath => Path.Combine(Application.persistentDataPath, CacheFileName);

        /// <summary>The live endpoint. Public so the dev menu / oracle can name it.</summary>
        public static string Endpoint => BackendBase + EndpointPath;

        private static bool s_booted;

        // ─────────────────────────────────────────────────────────────────────
        //  Boot
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mirrors DungeonWorldPortalSpawner.Bootstrap (:221) so the portal and the
        /// status arrive on the same tick ordering the rest of the system assumes.
        /// AfterSceneLoad, NOT BeforeSceneLoad — the latter runs before any scene exists.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_booted) return;
            s_booted = true;

            if (!Enabled)
            {
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceFlagOff);
                FlowTrace.Step(Sys, "kill switch off (" + FlagKey + "=0) - every dungeon door resolves OPEN, no fetch.");
                return;
            }

            LoadCache();                 // (1) synchronous, local
            RefreshAsync().Forget();     // (2) NOTHING awaits this
        }

        // ─────────────────────────────────────────────────────────────────────
        //  (1) Cache read
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Read the last good payload off disk. A miss is not an exception — but since
        /// the fail-closed ruling (2026-08-26) it is no longer harmless either: with no
        /// table every gated dungeon reads CLOSED until the live fetch lands. Traced as
        /// such below. A CORRUPT cache is deleted so the next boot is clean.
        /// </summary>
        public static void LoadCache()
        {
            string path = CachePath;
            string json = Guard.Try<string>(Sys, "read cache", () =>
                File.Exists(path) ? File.ReadAllText(path) : null, null);

            if (string.IsNullOrWhiteSpace(json))
            {
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                FlowTrace.Warn(Sys, "no cached payload at '" + path + "' - every GATED dungeon door is " +
                                    "CLOSED until the live fetch lands (fail-closed, owner ruling 2026-08-26). " +
                                    "Ungated ids (crossroads/fixtures) are unaffected.");
                return;
            }

            if (!DungeonStatusCatalog.ApplyPayload(json, DungeonStatusCatalog.ProvenanceCache))
            {
                FlowTrace.Fail(Sys, "cache rejected + deleted; NO table stands, so every gated door is " +
                                    "CLOSED until a live payload lands (fail-closed, owner ruling 2026-08-26). " +
                                    "path='" + path + "'");
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                Guard.Try(Sys, "delete corrupt cache", () => { if (File.Exists(path)) File.Delete(path); });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  (2) Live refresh — fire and forget
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetch the live payload. Fire-and-forget: call it as
        /// <c>RefreshAsync().Forget()</c>. On ANY failure the standing table is
        /// left exactly as the cache read left it — the network never closes a door.
        /// </summary>
        public static async UniTaskVoid RefreshAsync()
        {
            if (!Enabled) return;

            await UniTask.Delay(FirstYieldDelayMs);

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
                    // ⚠ "keeping" is the whole story on a device with a cache, and the whole
                    // PROBLEM on one without: no cache means no table means every gated door
                    // stays CLOSED. Fail, not Warn, when there is nothing standing.
                    LogFetchFailure("fetch threw (" + req.responseCode + ") " + ex.GetType().Name);
                    return;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // Covers the TIMEOUT path too: req.timeout expiring surfaces here as
                    // Result.ConnectionError, not as an exception.
                    LogFetchFailure("fetch failed (" + req.responseCode + ") " + req.result +
                                    ": " + (req.error ?? "no error text"));
                    return;
                }

                string body = req.downloadHandler != null ? req.downloadHandler.text : null;
                int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - startedAt) * 1000f);

                if (!DungeonStatusCatalog.ApplyPayload(body, DungeonStatusCatalog.ProvenanceLive))
                {
                    // Rejected: table untouched, and CRUCIALLY the cache is NOT written.
                    FlowTrace.Fail(Sys, "live payload rejected after " + ms + " ms - cache NOT overwritten, " +
                                        "keeping provenance=" + DungeonStatusCatalog.Provenance);
                    return;
                }

                FlowTrace.Step(Sys, "live payload landed after " + ms + " ms, provenance live " +
                                    "(boot was never blocked on it).");
                WriteCache(body);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cache write — only ever called after a SUCCESSFUL parse
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One voice for every live-fetch failure (threw / non-2xx / TIMED OUT). The
        /// severity is DERIVED from whether a table is standing, because that is what
        /// decides whether the player notices: with a cache the doors keep the last good
        /// answer; with none, the fail-closed default (owner ruling 2026-08-26, WO-1223)
        /// shuts every gated dungeon and the operator needs to see it as a failure.
        /// ⛔ CLAUDE.md §12 - never let this path go quiet.
        /// </summary>
        private static void LogFetchFailure(string what)
        {
            if (DungeonStatusCatalog.Loaded)
            {
                FlowTrace.Warn(Sys, what + " - the standing table survives (provenance=" +
                                    DungeonStatusCatalog.Provenance + ", rows=" +
                                    DungeonStatusCatalog.RowCount + "); doors keep their last good state.");
                return;
            }
            FlowTrace.Fail(Sys, what + " - and NO table is standing (provenance=" +
                                DungeonStatusCatalog.Provenance + "). Every GATED dungeon door is CLOSED " +
                                "for this session (fail-closed, owner ruling 2026-08-26). Kill switch: " +
                                FlagKey + "=0.");
        }

        private static void WriteCache(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            string path = CachePath;
            Guard.Try(Sys, "write cache", () =>
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            });
        }

        /// <summary>Test/dev hook: drop the cache file and reset to all-open.</summary>
        public static void ClearCache()
        {
            string path = CachePath;
            Guard.Try(Sys, "clear cache", () => { if (File.Exists(path)) File.Delete(path); });
            DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
            FlowTrace.Step(Sys, "cache cleared by hand - no table stands, so every GATED dungeon door " +
                                "is CLOSED until a live payload lands (fail-closed, owner ruling 2026-08-26).");
        }
    }
}
