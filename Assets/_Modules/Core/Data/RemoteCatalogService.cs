// =============================================================================
// RemoteCatalogService - WO-1331. Transport for the remote canonical-catalog seam,
// and the ONE place CanonicalJson.Source is ever assigned.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// SPLIT OF DUTIES, the same one RemoteTunables / RemoteTunablesService keeps:
// RemoteCatalogOverrides is pure state plus parse plus validation and knows
// nothing about transport. THIS file owns transport and the install, so the
// override table stays headlessly drivable by a regression oracle with no network
// and no PlayMode.
//
// -----------------------------------------------------------------------------
// ⛔ FLAG-GATED AND OFF. THIS CHANGES HOW EVERY CATALOG IN THE GAME LOADS.
// -----------------------------------------------------------------------------
// The blast radius is the whole product and the game is live on a store taking
// real money. So the seam is DISARMED by default, and disarmed means ABSENT:
// Install() returns BEFORE assigning CanonicalJson.Source, and Bootstrap()
// returns BEFORE starting any fetch. A default build therefore does not merely
// behave like today - it executes today's code path exactly, with
// CanonicalJson.Source still holding the LocalJsonCatalogSource from its own
// field initializer. Nothing is constructed, nothing is polled, no byte moves.
//
// HOW IT IS ARMED (see Enabled below):
//   * LOCAL, a human at the device: PlayerPrefs "ff.catalogremote" = 1
//     (FeatureFlags.RemoteCatalogs).
//   * REMOTE, the owner at the console: the tunables-rail knob
//     "catalog.remoteEnabled" - read ONLY IF it is registered in
//     RemoteTunables.Registry, so this file never emits the rail's
//     "UNREGISTERED tunable key" warning and needs NO edit the day the knob is
//     added. That four-file registry edit is deliberately NOT part of WO-1331:
//     all four of its files were under live modification by the WO-1330 lane, and
//     two lanes minting into one file is how this repo has lost work before.
//
// -----------------------------------------------------------------------------
// ⛔ THE INVARIANT OUTRANKS THE FEATURE:
//     NO ROW, NO NETWORK, NO SERVER, NO PARSE  =>  TODAY'S BEHAVIOUR, EXACTLY.
// -----------------------------------------------------------------------------
// Unreachable, timed out, 404, non-2xx, malformed, truncated, empty: every one
// falls through to the COMPILED catalog and says so. There is no path here that
// can blank a catalog, and no path that can half-apply one (the payload is
// accepted whole or rejected whole, in RemoteCatalogOverrides.ApplyPayload).
//
// -----------------------------------------------------------------------------
// THE FETCH CANNOT BLOCK OR DELAY BOOT, AND THAT IS STRUCTURAL, NOT A COMMENT:
//   * Bootstrap() calls PollForeverAsync().Forget() - there is NO await at the
//     call site, so nothing downstream of boot can wait on it.
//   * There is no barrier, no WaitForCompletion, no "wait for the first payload".
//   * Every catalog answers instantly, and its answer with no data is the
//     COMPILED copy. Nothing ever waits for a value.
//   * req.timeout is set, because without it a captive-portal socket never
//     completes and the request hangs for the whole session.
//
// -----------------------------------------------------------------------------
// WHY IT CACHES (same reason RemoteTunablesService does, and the same divergence
// from MaintenanceService, which deliberately does not).
// -----------------------------------------------------------------------------
// Catalogs are read DURING BOOT and most readers cache their parse in memory for
// the session. A value that only arrived after a network round trip would be a
// launch too late, every launch, forever. So the last accepted payload is
// mirrored into PlayerPrefs and read back at BeforeSceneLoad, which Unity
// guarantees runs before every AfterSceneLoad hook and before scene Awake()s.
// Consequences, all deliberate:
//   * The cache can only ever hold values that CAME FROM the database.
//   * A fresh payload REPLACES it wholesale, so it cannot resurrect a catalog the
//     owner cleared.
//   * A 404 (endpoint not deployed) CLEARS it - an absent feature holds no data.
//   * A corrupt cache is rejected by the same Guard-wrapped, validating parse as a
//     live payload, DISCARDED, and every catalog falls to the compiled copy.
//   * A payload that lands MID-SESSION reaches readers that have not parsed yet;
//     readers that already cached their parse pick it up on the next launch. That
//     is stated rather than hidden - it is a data rail, not a hot-reload.
//
// THREE HTTP IDIOMS, all three because of real production bugs (the same three
// MaintenanceService, DungeonStatusService and RemoteTunablesService document):
//   1. `using var req` - dispose the handler.
//   2. `req.timeout`   - without it a captive-portal socket never completes.
//   3. try/catch around the await AND a separate `req.result` check: the UniTask
//      awaiter THROWS on non-2xx (WO-769), so checking only one is the bug.
//
// NO AUTH. The endpoint is public read and must resolve before sign-in - catalogs
// are needed long before any identity exists. Do not call BackendRequestSigner.
//
// ⛔ SERVER-AUTHORITATIVE MONEY DATA IS PERMANENTLY OUT OF SCOPE. The boundary is
// enforced in RemoteCatalogOverrides.Denylist (checked before the allowlist, and
// a denied path rejects the whole payload), not in prose.
//
// ASCII only. Instrumentation: FlowTrace tag "CatalogRemote". Never strip it.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Ops;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core
{
    /// <summary>
    /// Arms (or, by default, does not arm) the remote canonical-catalog seam: a cached
    /// read at frame zero, one fetch shortly after load, then a poll forever. Never
    /// blocks, never throws outward, and every failure resolves the compiled catalog.
    /// </summary>
    public static class RemoteCatalogService
    {
        private const string Sys = RemoteCatalogOverrides.Sys;

        /// <summary>House pattern: the backend base is a private const per file
        /// (MaintenanceService.cs, RemoteTunablesService.cs and eleven others).
        /// Do NOT refactor the duplicates as part of WO-1331.</summary>
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";

        private const string EndpointPath = "/api/client-catalogs";

        /// <summary>Without a timeout a captive-portal socket never completes and the
        /// request hangs for the whole session.</summary>
        private const int RequestTimeoutSeconds = 15;

        /// <summary>How often the override table is re-read. Deliberately SLOWER than the
        /// tunables rail's 30 s: a catalog body is orders of magnitude larger than a knob
        /// table, and nobody bisects a crash loop with it.</summary>
        public const int PollSeconds = 120;

        /// <summary>Lets scene Awake()s run before the first network yield.</summary>
        private const int FirstYieldDelayMs = 500;

        /// <summary>PlayerPrefs key holding the last accepted payload.</summary>
        public const string CacheKey = "catalogs.cache.v1";

        /// <summary>The tunables-rail knob that arms this seam remotely. Read ONLY when it
        /// is actually registered - see the header for why the registry edit is not part
        /// of this ticket.</summary>
        public const string RailKey = "catalog.remoteEnabled";

        /// <summary>The live endpoint. Public so a dev menu / oracle can name it.</summary>
        public static string Endpoint => BackendBase + EndpointPath;

        /// <summary>Realtime seconds of the last ACCEPTED live payload, or 0.</summary>
        public static float LastPayloadAt { get; private set; }

        /// <summary>Consecutive failed fetches. Reset by any accepted payload.</summary>
        public static int ConsecutiveFailures { get; private set; }

        /// <summary>True once <see cref="CanonicalJson.Source"/> has been wrapped. False in
        /// a default build, forever - the seam is never installed while disarmed.</summary>
        public static bool Installed { get; private set; }

        private static bool s_booted;

        // ---------------------------------------------------------------------
        //  ARMING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Is the seam armed? DEFAULT FALSE, and false means the seam is absent, not merely
        /// idle.
        /// <para>
        /// Precedence matches the rail (docs/PROD022_TUNABLE_FLAGS.md): a human at the
        /// device (PlayerPrefs) beats the owner at the console (a database row) beats the
        /// build default. Both layers default OFF, so an unset device and an empty table
        /// agree on "today's behaviour".
        /// </para>
        /// <para>
        /// The rail knob is consulted ONLY when it is registered, so this never emits the
        /// rail's "UNREGISTERED tunable key" line and needs no edit when the knob lands.
        /// </para>
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (FeatureFlags.RemoteCatalogs) return true;
                return RemoteTunables.SpecFor(RailKey) != null && RemoteTunables.Bool(RailKey);
            }
        }

        // ---------------------------------------------------------------------
        //  Boot - two hooks, and the ORDER between them is the design
        // ---------------------------------------------------------------------

        /// <summary>
        /// BeforeSceneLoad: arm the seam, if armed, and read the cached payload off the
        /// device. NO NETWORK.
        /// <para>
        /// ⭐ THE ORDERING IS LOAD-BEARING. Unity runs every BeforeSceneLoad hook before
        /// every AfterSceneLoad one and before every scene Awake(), so a catalog set
        /// yesterday is already resolvable by the time the first reader asks. Without this
        /// hook every override would be a launch too late, permanently.
        /// </para>
        /// <para>
        /// ⚠ Relative order WITHIN BeforeSceneLoad is undefined, so the rail's own cache
        /// may not have loaded yet when <see cref="Enabled"/> is first asked. That is why
        /// <see cref="Bootstrap"/> re-attempts the install at AfterSceneLoad: today's arming
        /// path (PlayerPrefs) has no such dependency, and the future rail path gets a
        /// guaranteed second look one hook later. Install is idempotent.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Installed) return;

            if (!Enabled)
            {
                // ⛔ THE DEFAULT PATH. Nothing is assigned, nothing is constructed, nothing
                // is fetched. CanonicalJson.Source keeps the LocalJsonCatalogSource from its
                // own field initializer, so catalog loading is byte-identical to a build
                // without this file at all.
                FlowTrace.Step(Sys, "remote catalog seam is DISARMED (ff.catalogremote unset and no " +
                                    "'" + RailKey + "' row). CanonicalJson.Source is untouched and " +
                                    "every catalog resolves its COMPILED copy exactly as it always " +
                                    "has - this is TODAY'S BEHAVIOUR, byte for byte.");
                return;
            }

            LoadCache();

            // The ONE assignment in the whole tree. Idempotent: never double-wrap.
            Guard.Try(Sys, "install remote catalog seam", () =>
            {
                if (CanonicalJson.Source is RemoteCatalogSource) { Installed = true; return; }
                CanonicalJson.Source = new RemoteCatalogSource(
                    CanonicalJson.Source ?? new LocalJsonCatalogSource());
                Installed = true;
            });

            FlowTrace.Warn(Sys, "remote catalog seam ARMED and INSTALLED on CanonicalJson.Source " +
                                "(rows=" + RemoteCatalogOverrides.RowCount + ", provenance=" +
                                RemoteCatalogOverrides.TableProvenance + "). This build can resolve " +
                                "canonical catalogs from the database instead of its compiled copies. " +
                                "Quote this line in any felt-test report - an armed build is NOT the " +
                                "shipping build.");
            RemoteCatalogOverrides.LogConfiguration("seam installed");
        }

        /// <summary>
        /// Read and apply the on-device cache. Separate and public SO THE ORACLE CAN DRIVE
        /// IT: the corrupt-cache path is one of the failure modes that must land on the
        /// compiled catalog, and a path reachable only from a
        /// <c>[RuntimeInitializeOnLoadMethod]</c> hook can be tested once by hand and never
        /// again (CLAUDE.md section 12 - a claim nothing can falsify is not a claim).
        /// </summary>
        public static void LoadCache()
        {
            string cached = Guard.Try<string>(Sys, "read catalog cache",
                () => PlayerPrefs.GetString(CacheKey, null), null);

            if (string.IsNullOrWhiteSpace(cached))
            {
                FlowTrace.Step(Sys, "no cached catalog payload on this device - every catalog starts " +
                                    "on its COMPILED copy (today's behaviour). The first live fetch, " +
                                    "if it lands, may override some of them.");
                return;
            }

            if (ApplyCachedPayload(cached))
            {
                FlowTrace.Step(Sys, "cached catalog payload applied at BeforeSceneLoad (rows=" +
                                    RemoteCatalogOverrides.RowCount + "). A live payload arriving " +
                                    "later replaces it wholesale.");
                return;
            }

            // Reject AND discard: a cache we cannot parse or validate is worse than none,
            // because it would be re-rejected on every launch until something overwrites it.
            Guard.Try(Sys, "discard unusable catalog cache", () =>
            {
                PlayerPrefs.DeleteKey(CacheKey);
                PlayerPrefs.Save();
            });
            FlowTrace.Warn(Sys, "cached catalog payload was UNPARSEABLE or failed validation and has " +
                                "been DISCARDED. Every catalog resolves its COMPILED copy for this " +
                                "launch. NOTHING IS BROKEN meanwhile.");
        }

        /// <summary>Apply a cached payload string. PURE: no PlayerPrefs, no network, no side
        /// effect beyond the standing table. Returns false when the string is unusable.</summary>
        public static bool ApplyCachedPayload(string cached)
        {
            return RemoteCatalogOverrides.ApplyPayload(cached, RemoteCatalogOverrides.ProvenanceCache);
        }

        /// <summary>
        /// AfterSceneLoad: start the poll, and take the guaranteed second look at arming
        /// (see <see cref="Install"/>). Returns immediately.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_booted) return;

            if (!Enabled) return;      // Disarmed: no poll, no install, no allocation.

            if (!Installed) Install(); // Idempotent; covers the BeforeSceneLoad ordering note.

            s_booted = true;

            // NOTHING AWAITS THIS. Non-blocking here is STRUCTURAL (no await at the call
            // site), not a comment.
            PollForeverAsync().Forget();
        }

        // ---------------------------------------------------------------------
        //  The poll
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fetch, then fetch again every <see cref="PollSeconds"/>, forever. Fire and
        /// forget. Every failure resolves the compiled catalog and every failure is logged.
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
        /// One fetch. Public so a dev menu or a headless probe can force a refresh without
        /// waiting out the poll interval. Never throws.
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

                if (!RemoteCatalogOverrides.ApplyPayload(body, RemoteCatalogOverrides.ProvenanceRemote))
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
        /// Persist the accepted table so the NEXT launch resolves overrides at frame zero.
        /// Wholesale replace, never a merge - a catalog the owner cleared must disappear.
        /// Never throws: PlayerPrefs on a hardened WebGL host can refuse.
        /// </summary>
        private static void WriteCache()
        {
            string json = RemoteCatalogOverrides.SerializeStandingTable();
            if (string.IsNullOrEmpty(json)) return;

            if (json.Length > RemoteCatalogOverrides.MaxPayloadBytes)
            {
                FlowTrace.Warn(Sys, "accepted payload is " + json.Length + " chars, over the " +
                                    RemoteCatalogOverrides.MaxPayloadBytes + " cache ceiling - NOT " +
                                    "cached. It is live for this session; the next launch starts on " +
                                    "the compiled catalogs until a fetch lands.");
                return;
            }

            Guard.Try(Sys, "write catalog cache", () =>
            {
                PlayerPrefs.SetString(CacheKey, json);
                PlayerPrefs.Save();   // WebGL flushes to IndexedDB only on Save().
            });
        }

        /// <summary>
        /// A 404 is a completed HTTP conversation: the endpoint is not deployed. That is NOT
        /// unreachability, and the two must not be conflated - the identical mistake sealed a
        /// live store on 2026-08-27 (see MaintenanceCatalog.MarkFeatureAbsent).
        /// <para>
        /// An absent endpoint means NO catalog is overridden anywhere, so the standing table
        /// is cleared AND the on-device cache dropped: keeping a cache for a feature the
        /// server says does not exist would let an override outlive the system that set it.
        /// Every catalog then resolves its COMPILED copy, i.e. today's behaviour.
        /// </para>
        /// Call from BOTH the UniTask throw catch and the success-path status check.
        /// </summary>
        private static bool AcceptAbsent404(UnityWebRequest req)
        {
            if (req == null || req.responseCode != 404) return false;

            RemoteCatalogOverrides.Clear(RemoteCatalogOverrides.ProvenanceDefault);
            Guard.Try(Sys, "drop catalog cache (endpoint absent)", () =>
            {
                PlayerPrefs.DeleteKey(CacheKey);
                PlayerPrefs.Save();
            });

            ConsecutiveFailures = 0;
            LastPayloadAt = Time.realtimeSinceStartup;
            FlowTrace.Warn(Sys, "GET " + EndpointPath + " -> 404. The catalog endpoint is NOT DEPLOYED. " +
                                "Every catalog resolves its COMPILED copy and the on-device cache has " +
                                "been dropped - a 404 is the server saying the feature is absent, not " +
                                "that it is unreachable, and an absent table holds no override. " +
                                "NOTHING IS BROKEN meanwhile: this is exactly today's behaviour.");
            RemoteCatalogOverrides.LogConfiguration("endpoint 404");
            return true;
        }

        /// <summary>
        /// One voice for every fetch failure (threw / non-2xx / TIMED OUT / rejected).
        /// <para>
        /// Warn, not Fail, and the severity is the ruling: a failed fetch changes NOTHING
        /// about how the game behaves - every catalog was already resolving, and it keeps
        /// resolving the same text. It is worth saying out loud only because it means an
        /// edit the owner made would not reach this client.
        /// </para>
        /// CLAUDE.md section 12 - never let this path go quiet.
        /// </summary>
        private static void LogFetchFailure(string what)
        {
            ConsecutiveFailures++;
            FlowTrace.Warn(Sys, what + " - every catalog keeps its current resolved text; with no " +
                                "accepted payload that is the COMPILED copy, i.e. today's behaviour. " +
                                "consecutiveFailures=" + ConsecutiveFailures +
                                " rows=" + RemoteCatalogOverrides.RowCount +
                                " tableProvenance=" + RemoteCatalogOverrides.TableProvenance +
                                ". A catalog edited in the database will NOT reach this client until " +
                                "a fetch succeeds.");
        }
    }
}
