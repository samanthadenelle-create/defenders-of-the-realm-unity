// =============================================================================
// RemoteCatalogOverrides - WO-1331. THE STATE AND THE PARSE for the remote
// canonical-catalog seam. Transport lives in RemoteCatalogService.cs, exactly the
// way RemoteTunables / RemoteTunablesService are split, and for exactly the same
// reason: this half stays headlessly drivable by a regression oracle with no
// network and no PlayMode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// -----------------------------------------------------------------------------
// WHY THIS EXISTS AT ALL. Owner ruling 2026-09-02, verbatim:
//     "be smart, dont make it need a code change, make it tweakable from a db
//      call" ... "i have been screaming this for months."
//
// docs/reference/TUNABLE_LEVER_INVENTORY.md section 2 found why the screaming
// never worked. "DATA-DRIVEN" IN THIS REPO HAS NEVER MEANT "TUNABLE WITHOUT A
// REBUILD": LocalJsonCatalogSource resolves Resources.Load<TextAsset> FIRST on
// every platform, and Assets/Resources/ is COMPILED INTO THE PLAYER. So editing
// any of the 71 canonical JSONs still costs a full player build (~10 min APK /
// ~30 min WebGL), and editing the StreamingAssets twin changes nothing at all.
// Every past attempt to fix this by moving numbers into JSON was working on the
// wrong axis.
//
// CanonicalJson.Source has been a settable ICatalogSource the whole time and was
// assigned NOWHERE. This file plus RemoteCatalogSource.cs plus
// RemoteCatalogService.cs assign it - which converts authored canonical data into
// remotely updatable content WITH NO CALL-SITE CHANGE ANYWHERE IN THE GAME.
//
// -----------------------------------------------------------------------------
// THE INVARIANT THAT OUTRANKS THE FEATURE (the tunables rail's own, inherited):
//     NO ROW, NO NETWORK, NO SERVER, NO PARSE  =>  TODAY'S BEHAVIOUR, EXACTLY.
// -----------------------------------------------------------------------------
// An unreachable, 404ing, stale, truncated or malformed remote catalog resolves
// the COMPILED copy. Never a blank catalog. Never a hang. NEVER A PARTIAL MERGE:
// a payload is accepted whole or rejected whole, because a half-applied catalog
// overwriting a good one is strictly worse than no feature at all.
//
// VALIDATION HAPPENS BEFORE ANYTHING IS REPLACED. Every candidate must:
//   1. name a path on the ALLOWLIST below (deny list checked FIRST - see next
//      block), and one that actually exists in the compiled build;
//   2. be non-empty and under MaxCatalogBytes (the truncation/bloat guard);
//   3. parse as JSON through Guard.Try (rejects malformed AND truncated text);
//   4. have the SAME ROOT KIND as the compiled copy (object vs array), and, for
//      an object root, carry every top-level key the compiled copy has. A
//      catalog that silently lost a whole section is the "half a catalog"
//      failure this rule exists to stop.
// One failure rejects the WHOLE payload with FlowTrace.Fail and changes nothing.
//
// -----------------------------------------------------------------------------
// SERVER-AUTHORITATIVE DATA IS PERMANENTLY OUT OF SCOPE, AND THE BOUNDARY IS
// ENFORCED HERE IN CODE, NOT IN PROSE.
// -----------------------------------------------------------------------------
// Prices, entitlements, grants, base-unit amounts, token decimals and quote TTL
// are decided SERVER-SIDE in api/_lib/purchase-catalog.js. The client does no
// pricing arithmetic by design and /verify runs AFTER settlement, so a
// client-side override there is money gone with nothing granted. The game takes
// real money on mainnet.
//
// So Denylist is checked BEFORE Allowlist, a denied path rejects the ENTIRE
// payload rather than being quietly skipped, and the oracle asserts the two
// lists are disjoint. Widening the allowlist later can therefore never
// accidentally admit a money file.
//
// ASCII only. Instrumentation: FlowTrace tag "CatalogRemote". Never strip it.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeNelle.Core
{
    /// <summary>
    /// The standing table of remote canonical-catalog overrides, plus the parse and
    /// the validation that decides whether a payload is ever allowed to become one.
    /// Pure state: no transport, no PlayerPrefs, no network - so every failure mode
    /// is drivable by handing this class a STRING.
    /// </summary>
    public static class RemoteCatalogOverrides
    {
        /// <summary>FlowTrace system tag for everything in the remote catalog seam.</summary>
        public const string Sys = "CatalogRemote";

        /// <summary>Wire shape version. A mismatch is a Warn, not a rejection.</summary>
        public const int PayloadVersion = 1;

        /// <summary>Hard ceiling on ONE catalog's text. The truncation/bloat guard - a
        /// body larger than this is not a catalog anyone authored.</summary>
        public const int MaxCatalogBytes = 512 * 1024;

        /// <summary>Hard ceiling on the whole payload, checked before any parse so a
        /// hostile or broken body cannot cost anything but a length check.</summary>
        public const int MaxPayloadBytes = 1024 * 1024;

        /// <summary>Only paths under this prefix can ever be overridden. Belt to the
        /// allowlist's braces: a path outside the canonical tree is not catalog data.</summary>
        public const string RequiredPrefix = "Data/Canonical/";

        // =====================================================================
        //  THE TWO LISTS. DENY IS CHECKED FIRST, ALWAYS.
        // =====================================================================

        /// <summary>
        /// ⛔ NEVER overridable, whatever else is added below. These carry real-money
        /// shape (store packs, wallet/currency identity) and are decided server-side.
        /// A payload naming one of these rejects WHOLESALE and logs Fail - it is not
        /// quietly skipped, because a server trying to push a price to a client is a
        /// thing an operator must SEE, not a thing to shrug off.
        /// </summary>
        public static readonly string[] Denylist =
        {
            "Data/Canonical/packs.json",
            "Data/Canonical/wallets.json",
        };

        /// <summary>
        /// THE PROVEN SET. Deliberately FIVE, not all 71 (WO-1331: "land the seam and
        /// prove it on a SMALL number of catalogs - widening later is a data decision
        /// once the mechanism is proven"). Chosen as the highest-ranked felt-test
        /// levers in docs/reference/TUNABLE_LEVER_INVENTORY.md section 4 that already
        /// have a real runtime reader:
        ///   enemies.json      - enemy stats, the biggest difficulty surface with a reader
        ///   waves.json        - wave pacing and composition inputs
        ///   echoes-balance.json - Echo income, inventory row 14
        ///   kill-rewards.json - the grind-to-repair economy, owner-ruled 2026-08-26
        ///   siege-stakes.json - siege stakes, whose own note calls its numbers provisional
        /// <para>
        /// ⚠ waves.json note: the [wave-authoring] regression fails the gate if
        /// enemies[] batches reappear in the FILE. A remote payload does not pass
        /// through that gate. It is safe because those batches are INERT at runtime
        /// (_smartComposition:1 - WaveManager generates rosters, CLAUDE.md section 8),
        /// but anyone widening this list should know the gate covers the file, not the
        /// payload.
        /// </para>
        /// </summary>
        public static readonly string[] Allowlist =
        {
            "Data/Canonical/enemies.json",
            "Data/Canonical/waves.json",
            "Data/Canonical/echoes-balance.json",
            "Data/Canonical/kill-rewards.json",
            "Data/Canonical/siege-stakes.json",
        };

        /// <summary>Provenance of the standing table: "default" | "remote" | "remote-cached".</summary>
        public const string ProvenanceDefault = "default";
        public const string ProvenanceRemote = "remote";
        public const string ProvenanceCache = "remote-cached";

        // Swapped atomically by ApplyPayload. Never mutated in place.
        private static Dictionary<string, string> s_table;
        private static string s_provenance = ProvenanceDefault;

        /// <summary>Where the standing table came from.</summary>
        public static string TableProvenance => s_provenance;

        /// <summary>Overridden catalogs. 0 means EVERY catalog resolves the compiled copy.</summary>
        public static int RowCount => s_table == null ? 0 : s_table.Count;

        /// <summary>Bumped on every accepted payload and every clear.</summary>
        public static int Generation { get; private set; }

        // =====================================================================
        //  READ SIDE - never throws, ABSENCE => the compiled catalog.
        // =====================================================================

        /// <summary>
        /// True (with <paramref name="json"/> set) only when a VALIDATED remote override
        /// stands for this exact path. False on every other input, including null, an
        /// unknown path, and a path whose override was rejected.
        /// </summary>
        public static bool TryGet(string relativePath, out string json)
        {
            json = null;
            if (string.IsNullOrEmpty(relativePath)) return false;
            var table = s_table;
            if (table == null) return false;
            return table.TryGetValue(Normalize(relativePath), out json) && !string.IsNullOrEmpty(json);
        }

        /// <summary>Normalize a logical catalog path for comparison: backslashes to
        /// forward slashes, no leading slash. Case is preserved - Resources paths are
        /// case-sensitive on device.</summary>
        public static string Normalize(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return string.Empty;
            string p = relativePath.Replace('\\', '/').Trim();
            while (p.StartsWith("/", StringComparison.Ordinal)) p = p.Substring(1);
            return p;
        }

        /// <summary>⛔ Denied paths are checked FIRST. Real-money shape is never overridable.</summary>
        public static bool IsDenied(string relativePath)
        {
            string p = Normalize(relativePath);
            for (int i = 0; i < Denylist.Length; i++)
                if (string.Equals(Denylist[i], p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>True only for a path that is NOT denied, sits under the canonical
        /// prefix, and is named in <see cref="Allowlist"/>.</summary>
        public static bool IsAllowed(string relativePath)
        {
            string p = Normalize(relativePath);
            if (p.Length == 0) return false;
            if (IsDenied(p)) return false;
            if (!p.StartsWith(RequiredPrefix, StringComparison.Ordinal)) return false;
            for (int i = 0; i < Allowlist.Length; i++)
                if (string.Equals(Allowlist[i], p, StringComparison.Ordinal)) return true;
            return false;
        }

        // =====================================================================
        //  WRITE SIDE
        // =====================================================================

        /// <summary>
        /// Drop the standing table. Every catalog resolves its COMPILED copy afterwards,
        /// which is the correct resting state and the one this system must always be
        /// able to fall back to.
        /// </summary>
        public static void Clear(string provenance = ProvenanceDefault)
        {
            s_table = null;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;
            Generation++;
        }

        /// <summary>
        /// Parse, VALIDATE, and only then atomically swap in a payload. Returns false on
        /// any rejection, and on that path the EXISTING table is left exactly as it was.
        /// <para>
        /// A payload whose readOk is false is the SERVER saying it could not read its own
        /// table. That CLEARS to compiled defaults rather than being mistaken for "no
        /// catalogs are overridden" - the same discrimination the tunables rail makes.
        /// </para>
        /// <para>Never throws. Every parse is Guard-wrapped (CLAUDE.md section 12).</para>
        /// </summary>
        public static bool ApplyPayload(string json, string provenance)
        {
            string prov = string.IsNullOrEmpty(provenance) ? ProvenanceRemote : provenance;

            if (string.IsNullOrWhiteSpace(json))
            {
                FlowTrace.Warn(Sys, "payload rejected: EMPTY body from provenance='" + prov +
                                    "'. Nothing changed - every catalog keeps resolving whatever it " +
                                    "already resolved (tableProvenance=" + s_provenance + ", rows=" +
                                    RowCount + ").");
                return false;
            }

            if (json.Length > MaxPayloadBytes)
            {
                FlowTrace.Fail(Sys, "payload rejected: " + json.Length + " chars exceeds MaxPayloadBytes " +
                                    MaxPayloadBytes + ". Nothing changed; every catalog resolves its " +
                                    "COMPILED copy or its previously accepted override.");
                return false;
            }

            CatalogPayload dto = Guard.Try<CatalogPayload>(
                Sys, "parse catalog payload (" + prov + ")",
                () => JsonConvert.DeserializeObject<CatalogPayload>(json),
                null);

            if (dto == null)
            {
                FlowTrace.Fail(Sys, "payload rejected: UNPARSEABLE (provenance='" + prov + "'). Every " +
                                    "catalog resolves its COMPILED copy - the remote read is an " +
                                    "override, never a dependency.");
                return false;
            }

            if (dto.Version != PayloadVersion)
            {
                FlowTrace.Warn(Sys, "payload version " + dto.Version + " != expected " + PayloadVersion +
                                    " - parsing anyway (forward-compatible).");
            }

            if (!dto.ReadOk)
            {
                s_table = null;
                s_provenance = ProvenanceDefault;
                Generation++;
                FlowTrace.Warn(Sys, "server reported readOk=false (reason='" + (dto.Reason ?? "?") +
                                    "') - the catalog override table is unreadable ON THE SERVER. Every " +
                                    "catalog is back on its COMPILED copy, i.e. today's behaviour. " +
                                    "NOTHING IS BROKEN meanwhile.");
                LogConfiguration("server readOk=false");
                return true;
            }

            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            int ignored = 0;

            if (dto.Catalogs != null)
            {
                foreach (var pair in dto.Catalogs)
                {
                    string path = Normalize(pair.Key);
                    if (path.Length == 0) continue;

                    // (1) MONEY BOUNDARY, checked before anything else. Rejects the payload.
                    if (IsDenied(path))
                    {
                        FlowTrace.Fail(Sys, "payload REJECTED WHOLESALE: it names '" + path + "', which is " +
                                            "on the permanent DENY list. Prices, entitlements, grants and " +
                                            "purchase amounts are decided server-side in " +
                                            "api/_lib/purchase-catalog.js and can never be overridden from " +
                                            "here - the game takes real money on mainnet. Nothing changed. " +
                                            "Remove that key from the payload.");
                        return false;
                    }

                    // (2) Not on this build's allowlist: forward compatibility, not an error.
                    //     A newer build may prove more catalogs. Skipped, said out loud.
                    if (!IsAllowed(path))
                    {
                        ignored++;
                        FlowTrace.Step(Sys, "payload carries '" + path + "', which is not on THIS build's " +
                                            "allowlist (" + Allowlist.Length + " proven catalogs) - ignored. " +
                                            "It may belong to a newer build.");
                        continue;
                    }

                    // (3) FULL VALIDATION against the compiled copy. Any failure rejects
                    //     the WHOLE payload - never a partial merge.
                    if (!Validate(path, pair.Value, out string why))
                    {
                        FlowTrace.Fail(Sys, "payload REJECTED WHOLESALE at '" + path + "': " + why +
                                            " Nothing changed - every catalog still resolves its COMPILED " +
                                            "copy or its previously accepted override. A half-applied " +
                                            "catalog is strictly worse than no override at all.");
                        return false;
                    }

                    next[path] = pair.Value;
                }
            }

            // ATOMIC: one assignment, whole table.
            s_table = next;
            s_provenance = prov;
            Generation++;

            LogConfiguration("payload accepted, rows=" + next.Count + " ignored=" + ignored);
            return true;
        }

        /// <summary>
        /// Validate ONE candidate catalog against the COMPILED copy. Returns false with a
        /// reason. Never throws - every parse is Guard-wrapped.
        /// <para>
        /// The compiled copy is read through a FRESH <see cref="LocalJsonCatalogSource"/>,
        /// never through <see cref="CanonicalJson.Read"/>, so validation can never recurse
        /// into the very seam it is validating for.
        /// </para>
        /// </summary>
        public static bool Validate(string relativePath, string candidate, out string why)
        {
            string path = Normalize(relativePath);

            if (string.IsNullOrWhiteSpace(candidate))
            {
                why = "the payload's text for this catalog is EMPTY. An empty catalog is the exact " +
                      "outcome this seam exists to make impossible.";
                return false;
            }

            if (candidate.Length > MaxCatalogBytes)
            {
                why = "the text is " + candidate.Length + " chars, over MaxCatalogBytes " + MaxCatalogBytes +
                      ". Nothing authored is that large; this reads as corruption.";
                return false;
            }

            JToken remote = Guard.Try<JToken>(Sys, "parse remote catalog '" + path + "'",
                () => JToken.Parse(candidate), null);
            if (remote == null)
            {
                why = "the text is not valid JSON (malformed or TRUNCATED - a body cut mid-transfer " +
                      "fails exactly here, which is why this check exists before the swap).";
                return false;
            }

            string baseline = Guard.Try<string>(Sys, "read compiled baseline '" + path + "'",
                () => new LocalJsonCatalogSource().Read(path), null);
            if (string.IsNullOrWhiteSpace(baseline))
            {
                why = "this build has NO compiled copy of that catalog, so there is nothing to compare " +
                      "the payload against. A remote source may only OVERRIDE data this build already " +
                      "ships; it may never introduce a catalog out of nowhere.";
                return false;
            }

            JToken compiled = Guard.Try<JToken>(Sys, "parse compiled baseline '" + path + "'",
                () => JToken.Parse(baseline), null);
            if (compiled == null)
            {
                why = "the COMPILED copy of that catalog does not parse, so no shape comparison is " +
                      "possible. Refusing the override rather than trusting it blind.";
                return false;
            }

            if (remote.Type != compiled.Type)
            {
                why = "root kind mismatch: the payload is a " + remote.Type + " but the compiled catalog " +
                      "is a " + compiled.Type + ". Every reader in the game is written against the " +
                      "compiled shape.";
                return false;
            }

            if (compiled.Type == JTokenType.Object)
            {
                var co = (JObject)compiled;
                var ro = (JObject)remote;
                foreach (var prop in co.Properties())
                {
                    if (ro[prop.Name] == null)
                    {
                        why = "the payload is missing the top-level key '" + prop.Name + "' that the " +
                              "compiled catalog has. That is the 'half a catalog' failure - a reader " +
                              "would find nothing where authored data used to be.";
                        return false;
                    }
                }
            }
            else if (compiled.Type == JTokenType.Array)
            {
                if (((JArray)remote).Count == 0)
                {
                    why = "the payload's array root is EMPTY while the compiled catalog has " +
                          ((JArray)compiled).Count + " entries. An empty catalog is never an override, " +
                          "it is a failure.";
                    return false;
                }
            }

            why = null;
            return true;
        }

        /// <summary>
        /// Serialise the standing table back to the wire shape, for the on-device cache.
        /// Returns null when there is nothing to cache. Never throws.
        /// </summary>
        public static string SerializeStandingTable()
        {
            var table = s_table;
            if (table == null) return null;
            return Guard.Try<string>(Sys, "serialize standing catalog overrides", () =>
            {
                var dto = new CatalogPayload
                {
                    Version = PayloadVersion,
                    ReadOk = true,
                    Reason = "cache",
                    Catalogs = new Dictionary<string, string>(table, StringComparer.Ordinal),
                };
                return JsonConvert.SerializeObject(dto);
            }, null);
        }

        /// <summary>
        /// One line stating the WHOLE configuration, so a felt-test capture can be
        /// reconstructed afterwards. Step when nothing is overridden (that IS today's
        /// behaviour); Warn when anything is, because an overridden build is not the
        /// shipping build and must not read as ordinary narration.
        /// </summary>
        public static void LogConfiguration(string why)
        {
            var table = s_table;
            int rows = table == null ? 0 : table.Count;
            string head = "CATALOG CONFIG (" + (why ?? "?") + "): generation=" + Generation +
                          " tableProvenance=" + s_provenance + " rows=" + rows +
                          " allowlist=" + Allowlist.Length + " deny=" + Denylist.Length;

            if (rows == 0)
            {
                FlowTrace.Step(Sys, head + " || EVERY canonical catalog resolves its COMPILED copy - " +
                                    "this session is TODAY'S BEHAVIOUR, unchanged. Nothing was " +
                                    "overridden from the database.");
                return;
            }

            var names = new List<string>(rows);
            foreach (var pair in table) names.Add(pair.Key + "(" + pair.Value.Length + " chars)");
            FlowTrace.Warn(Sys, head + " | " + string.Join("  ", names.ToArray()) +
                                " || " + rows + " catalog(s) are OVERRIDDEN from the database. This " +
                                "session is NOT the shipping catalog set - quote this line in any " +
                                "felt-test report, because it is the only record of what produced the run.");
        }

        // ---------------------------------------------------------------------
        //  Wire DTO - Newtonsoft. JsonUtility cannot express the 'catalogs' map.
        // ---------------------------------------------------------------------

        [Serializable]
        internal sealed class CatalogPayload
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("readOk")] public bool ReadOk { get; set; }
            [JsonProperty("reason")] public string Reason { get; set; }

            /// <summary>Logical catalog path -> the RAW JSON TEXT of that catalog.
            /// Text, not a nested object, so each candidate is validated independently
            /// and a bad one cannot corrupt the parse of a good one.</summary>
            [JsonProperty("catalogs")] public Dictionary<string, string> Catalogs { get; set; }
        }
    }
}
