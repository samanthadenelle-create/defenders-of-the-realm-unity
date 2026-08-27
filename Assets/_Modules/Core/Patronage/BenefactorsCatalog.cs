// =============================================================================
// BenefactorsCatalog - WO-1073, the client-side state of the Benefactors of the
// Realm wall.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Patronage
//
// SPLIT OF DUTIES, copied deliberately from MaintenanceCatalog / MaintenanceService
// (WO-1243) because it is the shape that makes an oracle possible: THIS file is
// pure state plus parse and knows nothing about transport, so the regression can
// drive every payload shape headlessly with no network and no PlayMode.
// BenefactorsService owns transport and only transport.
//
// -----------------------------------------------------------------------------
// WHAT THIS IS AND WHAT IT MUST NEVER BECOME
// -----------------------------------------------------------------------------
// The wall is a SINGLE GLOBAL list, identical in every kingdom, of $500 Founders
// only (owner ruling 2026-08-27). The server is the source of truth and the
// client renders. There is no local authoring path here, no way to add a row, no
// way to promote a tier, and there must never be one: a client-grantable honour
// roll is not an honour roll.
//
// ⛔ THE PUBLIC SURFACE IS THREE FIELDS PER ROW PLUS THE MONUMENT KEY.
// The endpoint deliberately never sends a wallet, an email, a real name or a
// dollar figure (api/patronage/benefactors.js states this at length). This file
// therefore has NOWHERE to put one - the DTO below has no field that could hold
// an identity, so a future payload that started leaking one would be dropped on
// the floor here rather than reaching a label. That is on purpose. Do not add a
// convenience field "just in case".
//
// -----------------------------------------------------------------------------
// THE MONUMENT KEY IS PER PATRON, NEVER A GLOBAL PHASE
// -----------------------------------------------------------------------------
// Owner ruling 2026-08-27(c): each Founder's monument is a CUSTOM FBX the owner
// authors WITH that patron, one-on-one. So `monumentAssetId` rides on the ROW.
// A patron with no bespoke asset yet resolves - server side - to
// <see cref="StandInMonumentAssetKey"/>. Founder A can be standing beside their
// real monument in the same payload in which Founder B is still on the stand-in.
// ⛔ Do NOT collapse this to one bool on the catalog. It is per row.
//
// -----------------------------------------------------------------------------
// FAIL-QUIET, NOT FAIL-LOUD
// -----------------------------------------------------------------------------
// An unreachable server, a timeout or an unparseable body leaves the standing
// list EXACTLY as it was and the panel keeps showing whatever it last had (on a
// cold boot: the honest empty state). A founder's honour roll erroring into a
// player's face is worse than it being briefly absent - the endpoint itself
// takes the same position, answering 200 with an empty wall rather than a 500.
// Every one of those paths is TRACED. None of them is silent.
//
// ASCII only (CLAUDE.md: the tofu oracle fails a non-ASCII player-facing string).
// Instrumentation: FlowTrace tag "Benefactors". Never strip it.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;

namespace DeNelle.Core.Patronage
{
    /// <summary>One row of the global Benefactors of the Realm wall. Immutable.</summary>
    public struct BenefactorRow
    {
        /// <summary>Founding order, 1-based. A fact about WHEN, never about how much.</summary>
        public readonly int Ordinal;

        /// <summary>The PLAYER-CHOSEN patron name. Never a wallet, an email or a real name.</summary>
        public readonly string PatronName;

        /// <summary>Founding DATE (yyyy-MM-dd), never a timestamp.</summary>
        public readonly string FoundedOn;

        /// <summary>This patron's monument asset key - their own, or the shared stand-in.</summary>
        public readonly string MonumentAssetId;

        /// <summary>True when <see cref="MonumentAssetId"/> is that patron's bespoke FBX.</summary>
        public readonly bool MonumentIsBespoke;

        public BenefactorRow(int ordinal, string patronName, string foundedOn,
                             string monumentAssetId, bool monumentIsBespoke)
        {
            Ordinal = ordinal;
            PatronName = patronName;
            FoundedOn = foundedOn;
            MonumentAssetId = monumentAssetId;
            MonumentIsBespoke = monumentIsBespoke;
        }
    }

    /// <summary>
    /// The standing wall, as last read from <c>GET /api/patronage/benefactors</c>.
    /// Pure state plus parse: no transport, no Unity API, headlessly drivable.
    /// </summary>
    public static class BenefactorsCatalog
    {
        /// <summary>FlowTrace tag for every line this system emits.</summary>
        public const string Sys = "Benefactors";

        /// <summary>
        /// ⛔ THE SHARED STAND-IN MONUMENT ADDRESSABLE KEY, pinned as a literal.
        /// <para>
        /// It MUST stay byte-identical to <c>PLACEHOLDER_MONUMENT_ASSET_ID</c> in
        /// <c>api/_lib/benefactors.js</c> - the server writes NULL and resolves to this
        /// string, and a database CHECK forbids storing it, so there is exactly one
        /// spelling of "placeholder" on either side of the wire. FoundersMonumentWallRegression
        /// case [standin-key] reds if the two ever drift.
        /// </para>
        /// <para>
        /// ⭐ THIS IS ALSO THE DROP-IN POINT FOR THE REAL SHARED STAND-IN ART. The value is
        /// an Addressables ADDRESS, not a prefab reference and not a primitive buried in
        /// logic: author an asset under this exact address, register it with the structure
        /// grouper, push it per CLAUDE.md section 16, and the monument in the hub becomes
        /// that mesh with NO code change. See FoundersMonumentInjector's header.
        /// </para>
        /// </summary>
        public const string StandInMonumentAssetKey = "monument_founder_standin";

        /// <summary>
        /// The one tier that appears on the wall. Owner ruling 2026-08-27: $500 Founders
        /// ONLY - "Do NOT list $50 or $150. Scarcity is what makes a public list read as
        /// an honour rather than a subscriber roster." Matches FOUNDER_TIER_ID in
        /// api/_lib/benefactors.js.
        /// </summary>
        public const string FounderTierId = "founder_benefactor";

        /// <summary>Player-facing name of the wall. ASCII, no colour carries meaning.</summary>
        public const string WallTitle = "Benefactors of the Realm";

        /// <summary>Shown when the wall is genuinely empty. This is the TRUE state on day
        /// one and must read as a fact, not as an error.</summary>
        public const string EmptyWallLine =
            "No Benefactors yet. This wall records the Founders of the Realm.";

        /// <summary>Shown when nothing has ever been fetched in this session.</summary>
        public const string NeverFetchedLine = "Reading the wall...";

        /// <summary>Provenance values. Surfaced in the panel footer so we never pretend a
        /// stale or never-read wall is live (the LeaderboardPanel honesty-badge rule).</summary>
        public const string ProvenanceNever = "never-read";
        public const string ProvenanceLive = "live";
        public const string ProvenanceStale = "stale";

        /// <summary>Rows the server sends by default. Mirrors WALL_DEFAULT_ROWS.</summary>
        public const int DefaultRowLimit = 50;

        /// <summary>Hard ceiling the server clamps to. Mirrors WALL_MAX_ROWS. A payload
        /// carrying more than this is TRUNCATED here as well, so a server bug cannot
        /// hand the UI an unbounded list to lay out.</summary>
        public const int MaxRowLimit = 200;

        /// <summary>Longest patron name the server can have accepted. Mirrors
        /// PATRON_NAME_MAX_LEN in api/_lib/patron-name.js. A longer one is REFUSED here
        /// rather than trusted - the wall is public text on someone else's screen.</summary>
        public const int MaxPatronNameLength = 24;

        private static readonly BenefactorRow[] Empty = new BenefactorRow[0];

        private static BenefactorRow[] s_rows = Empty;
        private static string s_provenance = ProvenanceNever;

        /// <summary>The standing wall. Never null; empty until a payload is accepted.</summary>
        public static IReadOnlyList<BenefactorRow> Rows => s_rows;

        /// <summary>Row count of the standing wall.</summary>
        public static int Count => s_rows.Length;

        /// <summary>Where the standing wall came from. One of the Provenance* constants.</summary>
        public static string Provenance => s_provenance;

        /// <summary>True once any payload has been accepted this session.</summary>
        public static bool EverRead => !string.Equals(s_provenance, ProvenanceNever, StringComparison.Ordinal);

        /// <summary>
        /// Raised whenever the standing wall changes (accepted payload, or a clear).
        /// The panel re-renders off this; nothing else subscribes.
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// The one-line honesty badge for the panel footer. Says what the player is
        /// looking at IN WORDS - no colour, no icon, per the colourblind rule.
        /// </summary>
        public static string FooterText()
        {
            if (!EverRead) return NeverFetchedLine;
            if (string.Equals(s_provenance, ProvenanceStale, StringComparison.Ordinal))
                return "Could not reach the realm. Showing the last wall read this session.";
            return s_rows.Length == 1
                ? "1 Founder, read from the realm."
                : s_rows.Length + " Founders, read from the realm.";
        }

        /// <summary>
        /// Drop the standing wall back to never-read. Used by the regression oracle
        /// between cases. NOT a failure path - a failed fetch calls
        /// <see cref="MarkFetchFailed"/> instead, which KEEPS the rows.
        /// </summary>
        public static void Clear()
        {
            s_rows = Empty;
            s_provenance = ProvenanceNever;
            Changed?.Invoke();
        }

        /// <summary>
        /// Transport could not deliver a usable payload. The standing wall is KEPT and
        /// only its provenance moves, so a player who opened the wall once does not
        /// watch it blank itself on a dropped packet. Never clears rows.
        /// </summary>
        public static void MarkFetchFailed(string why)
        {
            if (EverRead)
            {
                s_provenance = ProvenanceStale;
                FlowTrace.Warn(Sys, "fetch failed (" + (why ?? "no reason given") +
                                    ") - KEEPING the " + s_rows.Length + " standing row(s) and marking " +
                                    "provenance=" + ProvenanceStale + ". The wall never blanks on a " +
                                    "dropped packet.");
            }
            else
            {
                FlowTrace.Warn(Sys, "fetch failed (" + (why ?? "no reason given") +
                                    ") and nothing has ever been read this session - the panel shows the " +
                                    "honest empty state. This is also the correct day-one appearance.");
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Parse a payload and ATOMICALLY swap it in.
        /// <para>
        /// Returns FALSE on a hard parse failure, and on that path the EXISTING wall is
        /// left exactly as it was - a malformed live payload must never half-blank a good
        /// standing list part-way through a row loop.
        /// </para>
        /// <para>
        /// A payload with <c>success:false</c> is the server telling us it could not read
        /// its own table. That is REJECTED (rows kept), not accepted as "nobody has ever
        /// paid $500" - which is the single most insulting thing this screen could say.
        /// </para>
        /// </summary>
        public static bool ApplyPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                FlowTrace.Warn(Sys, "payload rejected: empty body. The standing wall is unchanged " +
                                    "(rows=" + s_rows.Length + ", provenance=" + s_provenance + ").");
                return false;
            }

            WallPayload dto = Guard.Try<WallPayload>(
                Sys, "parse wall payload",
                () => JsonConvert.DeserializeObject<WallPayload>(json),
                null);

            if (dto == null)
            {
                FlowTrace.Warn(Sys, "payload rejected: unparseable. The standing wall is unchanged " +
                                    "(rows=" + s_rows.Length + "). Nothing is published on the strength " +
                                    "of a body we could not read.");
                return false;
            }

            if (!dto.Success)
            {
                FlowTrace.Warn(Sys, "payload rejected: success=false - the server answered 200 and told " +
                                    "us its own read failed. Rejected rather than rendered: an empty wall " +
                                    "drawn from a failed read would tell every player that nobody has ever " +
                                    "founded the realm.");
                return false;
            }

            if (dto.Tier != null && !string.Equals(dto.Tier, FounderTierId, StringComparison.Ordinal))
            {
                // ⚠ The threshold FIGURE is deliberately not restated here. It lives on the server
                // (WO-1073 section 2: thresholds are DATA, the client renders and never computes),
                // and a copy of it in a client log line is one more thing to go stale. The tier ID
                // is the whole contract.
                FlowTrace.Warn(Sys, "payload rejected: tier='" + dto.Tier + "' is not '" + FounderTierId +
                                    "'. The wall carries the founder tier ONLY (owner ruling 2026-08-27); " +
                                    "a payload carrying another tier is a server-side defect, not " +
                                    "something to render.");
                return false;
            }

            var accepted = new List<BenefactorRow>();
            int rejected = 0;
            int bespoke = 0;

            if (dto.Benefactors != null)
            {
                for (int i = 0; i < dto.Benefactors.Count; i++)
                {
                    if (accepted.Count >= MaxRowLimit)
                    {
                        FlowTrace.Warn(Sys, "payload carries more than " + MaxRowLimit + " rows - TRUNCATED. " +
                                            "The server clamps to WALL_MAX_ROWS, so this means the server " +
                                            "clamp moved or broke.");
                        break;
                    }

                    var w = dto.Benefactors[i];
                    if (w == null) { rejected++; continue; }

                    string name = w.PatronName == null ? null : w.PatronName.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        rejected++;
                        FlowTrace.Warn(Sys, "row " + i + " dropped: no patronName. A nameless row on a public " +
                                            "honour roll is a defect, never a blank line to render.");
                        continue;
                    }
                    if (name.Length > MaxPatronNameLength)
                    {
                        rejected++;
                        FlowTrace.Warn(Sys, "row " + i + " dropped: patronName is " + name.Length +
                                            " chars, over the server's own cap of " + MaxPatronNameLength +
                                            ". A name this long cannot have passed api/_lib/patron-name.js, so " +
                                            "it is not trusted onto a public screen.");
                        continue;
                    }

                    string monument = w.MonumentAssetId == null ? null : w.MonumentAssetId.Trim();
                    if (string.IsNullOrEmpty(monument)) monument = StandInMonumentAssetKey;

                    // PER PATRON, never a global phase. Recompute rather than trust the flag,
                    // so a server that sends the two fields inconsistently cannot make one
                    // patron's stand-in read as a finished collaboration.
                    bool isBespoke = !string.Equals(monument, StandInMonumentAssetKey, StringComparison.Ordinal);
                    if (isBespoke != w.MonumentIsBespoke)
                    {
                        FlowTrace.Warn(Sys, "row " + i + " ('" + name + "'): monumentIsBespoke=" +
                                            w.MonumentIsBespoke + " disagrees with monumentAssetId='" + monument +
                                            "'. Using the ASSET ID as the answer - it is the field the world " +
                                            "actually renders from.");
                    }
                    if (isBespoke) bespoke++;

                    int ordinal = w.Ordinal > 0 ? w.Ordinal : accepted.Count + 1;
                    accepted.Add(new BenefactorRow(ordinal, name, NormalizeDate(w.FoundedOn),
                                                   monument, isBespoke));
                }
            }

            // ATOMIC: one assignment, whole wall.
            s_rows = accepted.ToArray();
            s_provenance = ProvenanceLive;

            FlowTrace.Step(Sys, "wall accepted: rows=" + s_rows.Length + " bespoke=" + bespoke +
                                " standin=" + (s_rows.Length - bespoke) + " dropped=" + rejected +
                                " (declared count=" + dto.Count + "). Per-patron monument state is " +
                                "MIXED by design - it is never a global phase.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// The founding date, trimmed to a DATE. The endpoint already sends yyyy-MM-dd;
        /// this defends against a timestamp slipping through, because the hour a player
        /// paid is nobody else's business.
        /// </summary>
        private static string NormalizeDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            int t = s.IndexOf('T');
            if (t > 0) s = s.Substring(0, t);
            int sp = s.IndexOf(' ');
            if (sp > 0) s = s.Substring(0, sp);
            return s;
        }

        // ---------------------------------------------------------------------
        //  Wire DTOs - Newtonsoft, matching api/patronage/benefactors.js exactly.
        //  ⛔ THERE IS NO FIELD HERE THAT COULD HOLD AN IDENTITY. See the header.
        // ---------------------------------------------------------------------

        [Serializable]
        internal sealed class WallPayload
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("tier")] public string Tier { get; set; }
            [JsonProperty("count")] public int Count { get; set; }
            [JsonProperty("benefactors")] public List<WallRow> Benefactors { get; set; }
        }

        [Serializable]
        internal sealed class WallRow
        {
            [JsonProperty("ordinal")] public int Ordinal { get; set; }
            [JsonProperty("patronName")] public string PatronName { get; set; }
            [JsonProperty("foundedOn")] public string FoundedOn { get; set; }
            [JsonProperty("monumentAssetId")] public string MonumentAssetId { get; set; }
            [JsonProperty("monumentIsBespoke")] public bool MonumentIsBespoke { get; set; }
        }
    }
}
