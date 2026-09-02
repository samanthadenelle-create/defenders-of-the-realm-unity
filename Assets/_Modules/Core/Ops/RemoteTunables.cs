// =============================================================================
// RemoteTunables - PROD-022, the database-backed knobs the Pi crash loop is
// bisected with. THE STATE AND THE PARSE. Transport lives in
// RemoteTunablesService.cs, exactly the way MaintenanceCatalog / MaintenanceService
// are split, and for exactly the same reason: this half stays headlessly drivable
// by a regression oracle with no network and no PlayMode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ops
//
// -----------------------------------------------------------------------------
// WHY THIS EXISTS AT ALL. Owner ruling 2026-09-02, verbatim:
//     "make the testing as robust as possible with as many solutions as
//      possible... all we really have to do is just flip a flag and possibly
//      redeploy"
// A WebGL rebuild costs about thirty minutes. PROD-022 is a P0 crash loop that
// reproduces on the owner's iPhone inside Pi Browser and NOWHERE ELSE - desktop
// Chrome ran the identical build for 62 minutes. So every candidate mitigation
// ships in ONE build, each behind its OWN independent knob, all defaulting to
// today's behaviour. The bisect is then flag flips against the database, at
// seconds per hypothesis instead of half an hour.
//
// -----------------------------------------------------------------------------
// ⛔ THE INVARIANT THAT OUTRANKS EVERYTHING ELSE IN THIS FILE:
//     NO ROW, NO NETWORK, NO PARSE, NO SERVER  =>  TODAY'S BEHAVIOUR, EXACTLY.
// -----------------------------------------------------------------------------
// Every default in Registry below is the value that is hardcoded in the shipping
// code TODAY. A player who cannot reach the API, whose fetch times out, who gets
// a 404, or who receives malformed JSON resolves EVERY knob to that default. The
// remote read is an OVERRIDE and never a dependency. This is the same fail-to-the-
// safe-ground-state shape as MaintenanceCatalog, and it is asserted rather than
// asserted-in-a-comment: RemoteTunablesService never blocks, never awaits at a
// call site, and every parse goes through Guard.
//
// -----------------------------------------------------------------------------
// PRECEDENCE, and it composes with FeatureFlags rather than fighting it:
//     LOCAL PlayerPrefs "ff.tun.<key>"   (most specific - a human at the device)
//         beats REMOTE payload           (the owner at the database)
//             beats DEFAULT              (what this build hardcodes = today)
// FeatureFlags.Get already resolves PlayerPrefs-over-default for the ff.* family;
// this file inserts the remote layer BETWEEN those two and leaves ff.* untouched.
// The prefix is "ff.tun." and NOT plain "ff." on purpose - a tunable key and a
// FeatureFlags name must never be able to collide in one PlayerPrefs namespace.
//
// -----------------------------------------------------------------------------
// THE OWNER-FACING LIST IS docs/PROD022_TUNABLE_FLAGS.md. The Registry array
// below is the MACHINE-READABLE source of truth (key, kind, default, what ON
// does, which hypothesis it tests) and the doc is written from it. If you change
// one, change the other in the same commit - CLAUDE.md section 15.
//
// -----------------------------------------------------------------------------
// NO SILENT ANYTHING (CLAUDE.md section 12). Every resolve is traced ONCE per key
// with its value AND its provenance, and the whole configuration is printed on one
// line at service boot and again on every accepted payload - so a felt-test
// capture always says which configuration produced it. A session whose config
// cannot be reconstructed afterwards is a wasted session.
//
// ASCII only. FlowTrace tag "Tunables". Never strip it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;

namespace DeNelle.Core.Ops
{
    /// <summary>What a knob's value means on the wire.</summary>
    public enum TunableKind
    {
        /// <summary>0 / 1. Read with <see cref="RemoteTunables.Bool"/>.</summary>
        Bool = 0,
        /// <summary>A whole number. Read with <see cref="RemoteTunables.Int"/>.</summary>
        Int = 1,
    }

    /// <summary>
    /// One knob's contract. Immutable, authored in <see cref="RemoteTunables.Registry"/>,
    /// and printed verbatim into the trace so a reader never has to open this file to
    /// know what a flag does.
    /// </summary>
    public sealed class TunableSpec
    {
        /// <summary>Wire key. Lower camel, dotted, ASCII. Matches the client_tunables PK.</summary>
        public readonly string Key;

        /// <summary>Bool or Int.</summary>
        public readonly TunableKind Kind;

        /// <summary>THE SHIPPING VALUE. Bools are 0/1. This is today's behaviour, always.</summary>
        public readonly int Default;

        /// <summary>What turning it on (or raising it) actually does, in one sentence.</summary>
        public readonly string WhatOnDoes;

        /// <summary>Which PROD-022 hypothesis flipping it tests.</summary>
        public readonly string Hypothesis;

        public TunableSpec(string key, TunableKind kind, int def, string whatOnDoes, string hypothesis)
        {
            Key = key;
            Kind = kind;
            Default = def;
            WhatOnDoes = whatOnDoes;
            Hypothesis = hypothesis;
        }
    }

    /// <summary>
    /// Static, transport-free knob table. Always answers, never throws, and answers
    /// the shipping default for every question it cannot answer from data.
    /// </summary>
    public static class RemoteTunables
    {
        /// <summary>FlowTrace system tag for the whole tunables lane.</summary>
        public const string Sys = "Tunables";

        /// <summary>Payload schema version this build was written against.</summary>
        public const int PayloadVersion = 1;

        /// <summary>PlayerPrefs prefix for a LOCAL override. Deliberately not plain "ff.".</summary>
        public const string LocalPrefix = "ff.tun.";

        // Provenance literals. Also the values the oracle asserts, and the words that
        // appear in every trace line - "default" vs "remote" must never need inferring.
        public const string ProvenanceDefault = "default";
        public const string ProvenanceRemote = "remote";
        public const string ProvenanceLocal = "local-playerprefs";
        public const string ProvenanceCache = "remote-cached";

        // =====================================================================
        //  THE KEYS. One const per knob so no call site ever types a string.
        // =====================================================================

        /// <summary>Bool. Pi Browser runs the full desktop warm pass instead of on-demand.</summary>
        public const string KeyPiEagerStructureWarm = "pi.eagerStructureWarm";

        /// <summary>Bool. Pi awaits Addressables init + harvests keys before the first on-demand load.</summary>
        public const string KeyPiAwaitInitBeforeFirstLoad = "pi.awaitInitBeforeFirstLoad";

        /// <summary>Bool. Pi issues NO remote structure-art requests at all. The big hammer.</summary>
        public const string KeyPiDisableRemoteStructureArt = "pi.disableRemoteStructureArt";

        /// <summary>Int. Ceiling on concurrent residency requests. 0 = today (no explicit cap).</summary>
        public const string KeyAssetsMaxConcurrentRequests = "assets.maxConcurrentRequests";

        /// <summary>Int. The Pi Addressables per-request timeout, seconds.</summary>
        public const string KeyPiRequestTimeoutSeconds = "pi.requestTimeoutSeconds";

        /// <summary>Int. Async fetch attempts allowed per address per launch.</summary>
        public const string KeyAssetsMaxRequestAttempts = "assets.maxRequestAttempts";

        /// <summary>Int. VisualFactory resolve-miss escalate-then-throttle cap.</summary>
        public const string KeyVisualsMissLogCap = "visuals.missLogCap";

        /// <summary>Int. Verbosity of the [Flow:StructureAssets] / [Flow:VisualFactory] families.</summary>
        public const string KeyTraceAssetVerbosity = "trace.assetVerbosity";

        // ---------------------------------------------------------------------
        //  Verbosity levels for KeyTraceAssetVerbosity.
        //
        //  ⛔ THERE IS NO "OFF". CLAUDE.md section 12 is binding: instrumentation is
        //  PERMANENT, and a Warn or a Fail that stops being emitted turns a logged
        //  failure back into a silent one. This knob only ever moves the STEP lines -
        //  the narration - and every level below still emits Warn and Fail in full.
        // ---------------------------------------------------------------------

        /// <summary>Failures and warnings only. No Step narration.</summary>
        public const int VerbosityQuiet = 0;

        /// <summary>Failures, warnings, and the lifecycle Steps that name a decision.</summary>
        public const int VerbosityNormal = 1;

        /// <summary>TODAY'S BEHAVIOUR. Every Step, including the per-request narration.</summary>
        public const int VerbosityVerbose = 2;

        /// <summary>
        /// THE REGISTRY. Every knob, its shipping default, what turning it on does, and
        /// which PROD-022 hypothesis it tests.
        /// <para>
        /// ⭐ EVERY Default HERE IS THE VALUE THE SHIPPING CODE USED BEFORE PROD-022
        /// TOUCHED IT. That is not a convention, it is the acceptance criterion: a build
        /// with an empty client_tunables table must behave byte-for-byte like the build
        /// before this work. The pairs are checked against their real owners in
        /// StructureContentWarmer.cs and VisualFactory.cs, which read them through this
        /// file and nowhere else.
        /// </para>
        /// </summary>
        public static readonly TunableSpec[] Registry =
        {
            new TunableSpec(KeyPiEagerStructureWarm, TunableKind.Bool, 0,
                "Pi Browser runs the FULL desktop warm pass (await Addressables init, harvest keys, " +
                "DownloadDependenciesAsync, then load and retain all 35 structure prefabs) instead of " +
                "the on-demand policy.",
                "That on-demand streaming is itself the problem and eager residency is the healthier " +
                "shape on this webview. Deliberately shipped OFF: WO-PROD-022 forbids re-enabling eager " +
                "residency WITHOUT PROOF, and this knob is how the proof is gathered rather than assumed."),

            new TunableSpec(KeyPiAwaitInitBeforeFirstLoad, TunableKind.Bool, 0,
                "Pi Browser awaits Addressables.InitializeAsync and harvests every registered key BEFORE " +
                "the first on-demand LoadAssetAsync is issued; requests raised in the meantime are queued " +
                "and drained when init lands. Residency policy is otherwise untouched (this is NOT the " +
                "eager warm).",
                "PRIME SUSPECT. Today the Pi branch of StructureContentWarmer.Boot returns without ever " +
                "awaiting init and without harvesting keys, so the FIRST on-demand request is the first " +
                "thing that touches the catalog, and State is Degraded from frame one - which makes " +
                "IsSettled TRUE immediately, so a WhenSettled retry can fire before a single location " +
                "exists. That is the shape of the observed 'model not found' storm."),

            new TunableSpec(KeyPiDisableRemoteStructureArt, TunableKind.Bool, 0,
                "Pi Browser issues NO remote structure-art request at all. Every caller keeps the path it " +
                "already takes when an asset is not resident - the baked twin or the pending-art proxy - " +
                "so the town still renders and nothing stalls or blanks.",
                "THE BIG HAMMER, and it is diagnostically decisive in BOTH directions. If the crash loop " +
                "STOPS with this on, asset streaming is implicated beyond argument. If it CONTINUES, " +
                "streaming is exonerated and the cause is elsewhere - which is worth just as much. It " +
                "trades visual fidelity for a clean signal, on purpose."),

            new TunableSpec(KeyAssetsMaxConcurrentRequests, TunableKind.Int, 0,
                "Caps how many residency fetches may be in flight at once, on every host. 0 = TODAY: Pi " +
                "serialises through its own latch and desktop is unbounded. 1 or more installs an explicit " +
                "shared queue with that ceiling.",
                "That several simultaneous multi-MB bundle downloads plus decompression blow a memory " +
                "ceiling that lives OUTSIDE the managed heap - which is exactly how the captured sessions " +
                "look, dying with mem=247MB flat and no exception."),

            new TunableSpec(KeyPiRequestTimeoutSeconds, TunableKind.Int, 20,
                "The UnityWebRequest timeout installed by the Pi Addressables WebRequestOverride.",
                "That 20s is the wrong bound - too long, so a stalled fetch holds the queue past the " +
                "30-60s lifetime we are trying to survive; or too short, so a slow-but-healthy fetch is " +
                "killed and retried. Untunable today, and the WO forbids GUESSING a new constant - so it " +
                "ships at 20 and moves only on data."),

            new TunableSpec(KeyAssetsMaxRequestAttempts, TunableKind.Int, 3,
                "How many async fetch attempts one address gets before it is retired for the launch.",
                "That the retry budget is mis-sized: too high and the retry storm itself is the load that " +
                "kills the tab; too low and one transient webview stall costs a building its art for the " +
                "whole session."),

            new TunableSpec(KeyVisualsMissLogCap, TunableKind.Int, 3,
                "How many full resolve-miss Fail lines VisualFactory emits per address before it " +
                "announces its cap and drops to a throttled line. It NEVER goes silent.",
                "That trace VOLUME is itself a contributor - the observed final seconds were nothing but " +
                "the same four addresses cycling, and every line is a remote trace POST from a device " +
                "that is already the suspect."),

            new TunableSpec(KeyTraceAssetVerbosity, TunableKind.Int, VerbosityVerbose,
                "Narration level for the [Flow:StructureAssets] and [Flow:VisualFactory] families. " +
                "2 = today (every Step). 1 = lifecycle Steps only. 0 = no Steps. Warn and Fail are " +
                "emitted at EVERY level and cannot be turned off.",
                "Same volume hypothesis as the miss-log cap, but separable: this one silences the " +
                "SUCCESS narration while leaving every failure line intact, so a quiet-but-still-" +
                "diagnostic session can be compared against a loud one."),
        };

        // Swapped atomically by ApplyPayload. Never mutated in place.
        private static Dictionary<string, string> s_remote;
        private static string s_provenance = ProvenanceDefault;

        /// <summary>Where the standing table came from: "default" | "remote" | "remote-cached".</summary>
        public static string TableProvenance => s_provenance;

        /// <summary>True once any payload (live or cached) has been accepted.</summary>
        public static bool Loaded => s_remote != null;

        /// <summary>Keys in the standing table. 0 means every knob answers its default.</summary>
        public static int RowCount => s_remote == null ? 0 : s_remote.Count;

        /// <summary>Bumped on every accepted payload. Lets a reader see the config change mid-session.</summary>
        public static int Generation { get; private set; }

        // =====================================================================
        //  READ SIDE - always answers, never throws, ABSENCE => the DEFAULT.
        // =====================================================================

        /// <summary>Find a knob's contract, or null for an unregistered key (a caller bug).</summary>
        public static TunableSpec SpecFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < Registry.Length; i++)
                if (string.Equals(Registry[i].Key, key, StringComparison.Ordinal))
                    return Registry[i];
            return null;
        }

        /// <summary>
        /// Resolve a BOOL knob. NEVER throws. An unregistered key, an absent row, an
        /// unparseable value and an unreachable server ALL resolve to the shipping default.
        /// </summary>
        public static bool Bool(string key) => Int(key) != 0;

        /// <summary>
        /// Resolve an INT knob. NEVER throws, and the failure answer is always the default.
        /// <para>
        /// Traced ONCE per key per provenance-and-value, so a capture states which value was
        /// used AND where it came from without a reader having to guess (CLAUDE.md section 12).
        /// Re-traced when a later payload changes the answer, because a knob that changed
        /// mid-session is precisely the thing a felt-test needs to see.
        /// </para>
        /// </summary>
        public static int Int(string key)
        {
            var spec = SpecFor(key);
            if (spec == null)
            {
                // A key nobody registered is a CALLER bug, not a data problem. Say so loudly
                // and answer 0 - there is no default to fall back to because there is no knob.
                FlowTrace.Once(Sys, "unregistered:" + key,
                    "UNREGISTERED tunable key '" + (key ?? "null") + "' was read. There is no spec and " +
                    "therefore no default; answering 0. Add it to RemoteTunables.Registry and to " +
                    "docs/PROD022_TUNABLE_FLAGS.md in the same commit.");
                return 0;
            }

            int value = spec.Default;
            string provenance = ProvenanceDefault;

            // (1) REMOTE. The owner at the database.
            var table = s_remote;
            if (table != null && table.TryGetValue(spec.Key, out string raw))
            {
                if (TryParseValue(raw, spec, out int parsed))
                {
                    value = parsed;
                    provenance = s_provenance == ProvenanceCache ? ProvenanceCache : ProvenanceRemote;
                }
                else
                {
                    // Malformed row. It does NOT poison the knob - it falls to the default and
                    // says so. Throttled rather than Once: the row can be corrected live, and a
                    // reader needs to see that the bad value is STILL there.
                    FlowTrace.Throttle(Sys, "badvalue:" + spec.Key, 30f,
                        "row '" + spec.Key + "' carries an unusable value '" + Flatten(raw) + "' for kind " +
                        spec.Kind + " - IGNORED, this knob resolves to its shipping default " +
                        Describe(spec, spec.Default) + ". Fix the row; nothing is broken meanwhile.");
                }
            }

            // (2) LOCAL PlayerPrefs. The human at the device. Most specific, so it wins last.
            int local = ReadLocalOverride(spec);
            if (local != int.MinValue)
            {
                value = local;
                provenance = ProvenanceLocal;
            }

            FlowTrace.Once(Sys, "resolve:" + spec.Key + "=" + value + "@" + provenance,
                "KNOB " + spec.Key + " = " + Describe(spec, value) + "  provenance=" + provenance +
                "  (shipping default " + Describe(spec, spec.Default) + ", generation=" + Generation + "). " +
                (provenance == ProvenanceDefault
                    ? "No database row and no local override - this is TODAY'S BEHAVIOUR, unchanged."
                    : "This is an OVERRIDE of the shipping default."));

            return value;
        }

        /// <summary>
        /// PlayerPrefs override for one knob, or <c>int.MinValue</c> when absent.
        /// Guarded: PlayerPrefs on a hardened WebGL host can throw on access, and a
        /// diagnostic knob must never be the thing that takes the app down.
        /// </summary>
        private static int ReadLocalOverride(TunableSpec spec)
        {
            const int absent = int.MinValue;
            return Guard.Try(Sys, "read local override " + spec.Key, () =>
            {
                int v = UnityEngine.PlayerPrefs.GetInt(LocalPrefix + spec.Key, absent);
                if (v == absent) return absent;
                if (spec.Kind == TunableKind.Bool) return v != 0 ? 1 : 0;
                return v;
            }, absent);
        }

        /// <summary>Parse one wire value. Accepts 0/1 and true/false for bools.</summary>
        private static bool TryParseValue(string raw, TunableSpec spec, out int value)
        {
            value = 0;
            if (raw == null) return false;
            string s = raw.Trim();
            if (s.Length == 0) return false;

            if (spec.Kind == TunableKind.Bool)
            {
                if (s.Equals("1", StringComparison.Ordinal) ||
                    s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("on", StringComparison.OrdinalIgnoreCase)) { value = 1; return true; }
                if (s.Equals("0", StringComparison.Ordinal) ||
                    s.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("off", StringComparison.OrdinalIgnoreCase)) { value = 0; return true; }
                return false;
            }

            return int.TryParse(s, System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Human wording for a value: ON/OFF for bools, the number for ints.</summary>
        public static string Describe(TunableSpec spec, int value)
        {
            if (spec == null) return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (spec.Kind == TunableKind.Bool) return value != 0 ? "ON" : "OFF";
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // =====================================================================
        //  THE CONFIGURATION LINE - one line, every knob, every session
        // =====================================================================

        /// <summary>
        /// Print the WHOLE configuration on one line: every knob, its resolved value and its
        /// provenance.
        /// <para>
        /// ⭐ THIS IS WHY A FELT-TEST IS NOT WASTED. The owner will flip knobs between runs and
        /// report "that one felt better". Without this line the capture cannot say which
        /// configuration produced it, and the run proves nothing. Emitted at service boot AND
        /// again on every accepted payload, so a mid-session change is visible too.
        /// </para>
        /// Never throws. Uses Warn deliberately when ANY knob is overridden - an overridden build
        /// is not the shipping build, and that must not read as ordinary narration.
        /// </summary>
        public static void LogConfiguration(string why)
        {
            Guard.Try(Sys, "log tunable configuration", () =>
            {
                var sb = new StringBuilder(512);
                int overridden = 0;
                sb.Append("CONFIG (").Append(why ?? "?").Append("): generation=").Append(Generation)
                  .Append(" tableProvenance=").Append(s_provenance)
                  .Append(" rows=").Append(RowCount).Append(" | ");

                for (int i = 0; i < Registry.Length; i++)
                {
                    var spec = Registry[i];
                    int v = Int(spec.Key);
                    if (v != spec.Default) overridden++;
                    if (i > 0) sb.Append("  ");
                    sb.Append(spec.Key).Append('=').Append(Describe(spec, v));
                    if (v != spec.Default) sb.Append("(OVERRIDDEN, default ").Append(Describe(spec, spec.Default)).Append(')');
                }

                if (overridden == 0)
                {
                    FlowTrace.Step(Sys, sb.ToString() +
                        " || EVERY knob is at its shipping default - this session is TODAY'S BEHAVIOUR, " +
                        "unchanged. Nothing was overridden by the database or by PlayerPrefs.");
                }
                else
                {
                    FlowTrace.Warn(Sys, sb.ToString() +
                        " || " + overridden + " knob(s) are OVERRIDDEN. This session is NOT the shipping " +
                        "default configuration - quote this line in any felt-test report, because it is " +
                        "the only record of what produced the run. See docs/PROD022_TUNABLE_FLAGS.md.");
                }
            });
        }

        // =====================================================================
        //  WRITE SIDE
        // =====================================================================

        /// <summary>
        /// Drop the standing table. Every knob answers its shipping default afterwards,
        /// which is the correct resting state and the one this system must always be able
        /// to fall back to.
        /// </summary>
        public static void Clear(string provenance = ProvenanceDefault)
        {
            s_remote = null;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;
            Generation++;
        }

        /// <summary>
        /// Parse a payload and ATOMICALLY swap it in. Returns false on a hard parse failure,
        /// and on that path the EXISTING table is left exactly as it was - a malformed live
        /// payload must never half-apply over a good standing table.
        /// <para>
        /// A payload whose readOk is false is the SERVER saying it could not read its own
        /// table. That clears to defaults rather than being mistaken for "no knobs are set".
        /// </para>
        /// </summary>
        public static bool ApplyPayload(string json, string provenance)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                FlowTrace.Warn(Sys, "payload rejected: empty body from provenance='" +
                                    (provenance ?? "null") + "'. Every knob keeps whatever it already " +
                                    "resolved (tableProvenance=" + s_provenance + ").");
                return false;
            }

            TunablePayload dto = Guard.Try<TunablePayload>(
                Sys, "parse tunables payload (" + (provenance ?? "null") + ")",
                () => JsonConvert.DeserializeObject<TunablePayload>(json),
                null);

            if (dto == null)
            {
                FlowTrace.Warn(Sys, "payload rejected: unparseable (provenance='" + (provenance ?? "null") +
                                    "'). Every knob resolves to its SHIPPING DEFAULT - the remote read is " +
                                    "an override, never a dependency.");
                return false;
            }

            if (dto.Version != PayloadVersion)
            {
                FlowTrace.Warn(Sys, "payload version " + dto.Version + " != expected " + PayloadVersion +
                                    " - parsing anyway (forward-compatible).");
            }

            if (!dto.ReadOk)
            {
                s_remote = null;
                s_provenance = ProvenanceDefault;
                Generation++;
                FlowTrace.Warn(Sys, "server reported readOk=false (reason='" + (dto.Reason ?? "?") +
                                    "') - the tunables table is unreadable ON THE SERVER. Every knob is " +
                                    "back at its shipping default, i.e. today's behaviour. No knob can be " +
                                    "changed until the table reads again.");
                LogConfiguration("server readOk=false");
                return true;
            }

            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            int unknown = 0;

            if (dto.Values != null)
            {
                foreach (var pair in dto.Values)
                {
                    string key = pair.Key;
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (SpecFor(key) == null)
                    {
                        // Forward compatibility, and it is deliberately not an error: the
                        // database may carry a key a NEWER build understands. Say so and move on.
                        unknown++;
                        FlowTrace.Step(Sys, "payload carries unregistered key '" + key +
                                            "' - ignored by this build (it may belong to a newer one).");
                        continue;
                    }
                    next[key] = pair.Value;
                }
            }

            // ATOMIC: one assignment, whole table.
            s_remote = next;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceRemote : provenance;
            Generation++;

            LogConfiguration("payload accepted, rows=" + next.Count + " unknown=" + unknown);
            return true;
        }

        /// <summary>
        /// Serialise the standing table back to the wire shape, for the on-device cache.
        /// Returns null when there is nothing to cache. Never throws.
        /// </summary>
        public static string SerializeStandingTable()
        {
            var table = s_remote;
            if (table == null) return null;
            return Guard.Try<string>(Sys, "serialize standing tunables", () =>
            {
                var dto = new TunablePayload
                {
                    Version = PayloadVersion,
                    ReadOk = true,
                    Reason = "cache",
                    Values = new Dictionary<string, string>(table, StringComparer.Ordinal),
                };
                return JsonConvert.SerializeObject(dto);
            }, null);
        }

        /// <summary>Flatten a value for one-line logging. Bounded - a row is operator data.</summary>
        private static string Flatten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            string t = s.Replace('\r', ' ').Replace('\n', ' ');
            return t.Length <= 64 ? t : t.Substring(0, 64) + "...";
        }

        // ---------------------------------------------------------------------
        //  Wire DTO - Newtonsoft. JsonUtility cannot express the 'values' map.
        // ---------------------------------------------------------------------

        [Serializable]
        internal sealed class TunablePayload
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("readOk")] public bool ReadOk { get; set; }
            [JsonProperty("reason")] public string Reason { get; set; }
            [JsonProperty("values")] public Dictionary<string, string> Values { get; set; }
        }
    }
}
