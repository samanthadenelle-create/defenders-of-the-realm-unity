// =============================================================================
// DungeonStatusService — WO-1114, the fetch/cache lifecycle for the door state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// SPLIT OF DUTIES: DungeonStatusCatalog is pure state + parse and knows nothing
//   about transport. THIS file owns transport and ONLY transport, so the
//   catalog stays headlessly drivable by the regression oracle.
//
// RESOLUTION ORDER — live -> cached -> all-open (WO-1114 §4d), realised as:
//
//   Bootstrap()  [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
//     |
//     kill switch off? -> Catalog.Clear("flag-off"); ALL OPEN; return (no fetch)
//     |
//     (1) LOAD CACHE SYNCHRONOUSLY  - one small local file. Table is populated
//         at frame 0, so a device that has NEVER reached the network still gets
//         the last good answer. A miss leaves the table empty = ALL OPEN, which
//         is exactly the game as it ships today.
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
        //  Kill switch
        // ─────────────────────────────────────────────────────────────────────
        //
        // ⚠ TEMPORARY HOME — WO-1114 lane note, 2026-08-21.
        // The sanctioned home for this is `FeatureFlags.DungeonStatus =>
        // Get("dungeonstatus", defaultOn: true)` (FeatureFlags.cs, house pattern
        // at :137/:873). FeatureFlags.cs was lane-fenced when this landed, and
        // FeatureFlags.Get is private, so the read is inlined here with IDENTICAL
        // semantics: PlayerPrefs "ff.dungeonstatus" — 0 = off, 1 = on, -1/absent =
        // the compiled default (FeatureFlags.cs:8-14).
        // ⛔ When the flag moves to FeatureFlags, DELETE this helper — do not leave
        // two readers of the same key (the duplicated-state failure CLAUDE.md
        // catalogues three times over).
        // ⛔ Do NOT add "dungeonstatus" to FeatureFlags.s_urlActivatableFlags. That
        // allow-list is deliberately restricted to read-only presentation flags; a
        // URL-flippable content gate is a security regression.

        private const string FlagKey = "ff.dungeonstatus";
        private const bool FlagDefaultOn = true;

        /// <summary>False forces every door OPEN with no rebuild — the kill switch
        /// if a bad payload ever locks content.</summary>
        public static bool Enabled
        {
            get
            {
                int v = PlayerPrefs.GetInt(FlagKey, -1);
                if (v == 0) return false;
                if (v == 1) return true;
                return FlagDefaultOn;
            }
        }

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
        /// Read the last good payload off disk. A miss is NOT an error — it is the
        /// all-open ground state. A CORRUPT cache is deleted so the next boot is clean.
        /// </summary>
        public static void LoadCache()
        {
            string path = CachePath;
            string json = Guard.Try<string>(Sys, "read cache", () =>
                File.Exists(path) ? File.ReadAllText(path) : null, null);

            if (string.IsNullOrWhiteSpace(json))
            {
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                FlowTrace.Step(Sys, "no cached payload at '" + path + "' - all dungeon doors OPEN (ground state).");
                return;
            }

            if (!DungeonStatusCatalog.ApplyPayload(json, DungeonStatusCatalog.ProvenanceCache))
            {
                FlowTrace.Fail(Sys, "cache rejected + deleted; falling back to all-open. path='" + path + "'");
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
                    FlowTrace.Warn(Sys, "fetch threw (" + req.responseCode + ") " + ex.GetType().Name +
                                        " - keeping provenance=" + DungeonStatusCatalog.Provenance);
                    return;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    FlowTrace.Warn(Sys, "fetch failed (" + req.responseCode + ") " + req.result +
                                        ": " + (req.error ?? "no error text") +
                                        " - keeping provenance=" + DungeonStatusCatalog.Provenance);
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
            FlowTrace.Step(Sys, "cache cleared by hand - all dungeon doors OPEN.");
        }
    }
}
