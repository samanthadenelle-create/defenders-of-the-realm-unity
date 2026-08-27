// =============================================================================
// MaintenanceCatalog - WO-1243, the operator kill switches, client side.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ops
//
// Six areas the owner can seal from the command centre without shipping a build:
//     Farming | Raiding | Arena | Dungeons | Store | Server
// Server is the whole game: when it is sealed, every area answers sealed.
//
// -----------------------------------------------------------------------------
// WHAT THIS IS FOR, AND WHY IT IS NOT THE REAL LOCK
// -----------------------------------------------------------------------------
// Owner ruling 2026-08-27, verbatim: "mine allows if we see someone finds a
// hack, we seal that area and patch". This is EXPLOIT CONTAINMENT, not a
// maintenance-window nicety.
//
// !! AND THAT MEANS THIS FILE IS THE COURTESY LAYER, NOT THE CONTROL. Anyone
// exploiting the game is by definition running a client that does what they
// want; a check that lives only here is a closed sign on an unlocked door. It
// clears the area of honest players and leaves the attacker alone in it. The
// real seal is enforced server side, at api/_lib/maintenance.js, called from
// api/purchases/quote.js, api/game/save.js and api/leaderboard/submit.js.
//
// So what is this half FOR? Two things worth having, neither of them security:
//   1. A player who taps a sealed area is TOLD why, in the operator's own
//      words, instead of tapping a dead button.
//   2. Honest traffic stops immediately, which keeps the logs of the incident
//      readable while she patches.
// Never present this gate as the containment. It is the sign, not the lock.
//
// -----------------------------------------------------------------------------
// FAIL-OPEN. THIS IS THE OPPOSITE OF DungeonStatusCatalog, ON PURPOSE.
// -----------------------------------------------------------------------------
// No table, an unreachable server, a timeout, a malformed payload: EVERY area
// stays OPEN. Owner-confirmed 2026-08-27, verbatim:
//     "correct cause i cannot help if server is unreachable"
// The argument is CAPABILITY, not blast radius: with the server unreachable she
// cannot flip a toggle or author a message anyway, so closing the game buys
// nothing and costs every player their session.
//
// DungeonStatusCatalog (WO-1223) fails CLOSED because absence there must not
// GRANT access to content. Absence HERE must not DENY access to the whole game.
// Correctness versus availability - two different questions, two opposite
// answers, and a seat "correcting" one into the other breaks a live game.
// DO NOT UNIFY THE TWO SYSTEMS.
//
// -----------------------------------------------------------------------------
// NO DEVICE CACHE. Owner-ruled, and she was shown the consequence.
// -----------------------------------------------------------------------------
// Every check is live against the standing payload, which MaintenanceService
// re-fetches on a short interval. Nothing is written to disk. An offline player
// therefore falls back to the default, which under fail-open means everything
// is open. That was put to the owner explicitly and chosen. Do NOT add a cache
// "to be safe" - it was considered and rejected.
//
// -----------------------------------------------------------------------------
// TRANSPORT-FREE, like DungeonStatusCatalog and for the same reason: pure state
// plus parse, no MonoBehaviour, no UnityWebRequest. That is what lets
// MaintenanceTogglesRegression drive the whole matrix headlessly with no
// network and no PlayMode. The fetch lifecycle is MaintenanceService.cs.
//
// ASCII ONLY in every string in this file. The banner must read as maintenance
// from its WORDS - the owner is red/green colourblind and no meaning in this
// game may live in colour alone (CLAUDE.md section 7).
//
// Instrumentation: FlowTrace tag "Maintenance" (CLAUDE.md section 12). Every
// refusal names WHICH toggle closed it, so "raids do nothing" is one log line
// away from an answer instead of a theory. Never strip these calls.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;

namespace DeNelle.Core.Ops
{
    /// <summary>
    /// The six sealable areas. There is no seventh. Kept in step by hand with
    /// the CHECK constraint on maintenance_toggles in api/schema.sql and with
    /// the AREAS array in api/_lib/maintenance.js; MaintenanceTogglesRegression
    /// case [area-domain] source-lints all three against each other.
    /// </summary>
    public enum MaintenanceArea
    {
        /// <summary>Harvesting and collectors.</summary>
        Farming = 0,
        /// <summary>Raids.</summary>
        Raiding = 1,
        /// <summary>The arena.</summary>
        Arena = 2,
        /// <summary>Dungeon entry.</summary>
        Dungeons = 3,
        /// <summary>The store and the purchase surface.</summary>
        Store = 4,
        /// <summary>THE WHOLE GAME. Closes every area above.</summary>
        Server = 5,
    }

    /// <summary>One area's resolved seal state plus the operator's authored prose.</summary>
    public readonly struct MaintenanceState
    {
        /// <summary>True when the area refuses entry right now.</summary>
        public readonly bool Closed;

        /// <summary>Which toggle closed it: the area's own id, or "server". Null when open.</summary>
        public readonly string ClosedBy;

        /// <summary>The operator's message. MAY be null - the caller then uses
        /// <see cref="MaintenanceCatalog.DefaultBannerFor"/>.</summary>
        public readonly string Message;

        public MaintenanceState(bool closed, string closedBy, string message)
        {
            Closed = closed;
            ClosedBy = closedBy;
            Message = message;
        }

        /// <summary>The ground state, and under fail-open it is also every failure state.</summary>
        public static MaintenanceState Open => new MaintenanceState(false, null, null);
    }

    /// <summary>
    /// Static, transport-free table of area seals. Always answers; never throws;
    /// resolves every unknown toward OPEN (owner ruling 2026-08-27).
    /// </summary>
    public static class MaintenanceCatalog
    {
        /// <summary>FlowTrace system tag for the whole kill-switch lane.</summary>
        public const string Sys = "Maintenance";

        /// <summary>Payload schema version this build was written against. A payload
        /// carrying a different version is still parsed (forward-compatible); only a
        /// hard parse failure rejects, and a rejection leaves everything OPEN.</summary>
        public const int PayloadVersion = 1;

        // Provenance literals. Also the values the oracle asserts.
        public const string ProvenanceLive = "live";
        public const string ProvenanceDefault = "default";
        public const string ProvenanceFlagOff = "flag-off";

        /// <summary>The endpoint 404'd: the toggle system is not deployed here. See <see cref="MarkFeatureAbsent"/>.</summary>
        public const string ProvenanceAbsent = "absent";

        /// <summary>
        /// Shown when the toggle table cannot be read AND the area is the store, which
        /// is the single fail-CLOSED area (owner ruling 2026-08-27). It says plainly that
        /// nothing was charged, because a player who taps Buy and is refused will
        /// otherwise assume the money left and open a ticket about it.
        /// ASCII only - the UI font renders nothing else.
        /// </summary>
        public const string UnreadableStoreMessage =
            "The store is closed because we cannot reach the server. " +
            "Nothing has been charged. Please try again shortly.";

        /// <summary>The wire id of the whole-game toggle.</summary>
        public const string ServerAreaId = "server";

        /// <summary>
        /// The six wire ids, in enum order. Index by (int)MaintenanceArea.
        /// ASCII, lower case, and identical to the api/ AREAS array.
        /// </summary>
        public static readonly string[] AreaIds =
        {
            "farming", "raiding", "arena", "dungeons", "store", "server",
        };

        // Swapped atomically by ApplyPayload. Never mutated in place.
        private static Dictionary<string, MaintenanceState> s_table;
        private static string s_provenance = ProvenanceDefault;

        /// <summary>"live" | "default" | "flag-off" - where the standing table came
        /// from. Surfaced in every refusal trace so a headless log says WHY.</summary>
        public static string Provenance => s_provenance;

        /// <summary>True once a payload has been accepted. False means fail-open.</summary>
        public static bool Loaded => s_table != null;

        /// <summary>Rows in the standing table. 0 means nothing is sealed.</summary>
        public static int RowCount => s_table == null ? 0 : s_table.Count;

        /// <summary>Wire id for an area. Never throws on a bad cast.</summary>
        public static string IdOf(MaintenanceArea area)
        {
            int i = (int)area;
            return (i >= 0 && i < AreaIds.Length) ? AreaIds[i] : "unknown";
        }

        // ---------------------------------------------------------------------
        //  READ SIDE - always answers, never throws. ABSENCE => OPEN.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolve one area's seal. NEVER throws.
        /// <para>
        /// FAIL-OPEN (owner ruling 2026-08-27). A null table (no fetch yet, server
        /// unreachable, timed out, payload rejected) and an absent row BOTH resolve
        /// OPEN. The only way to a closed answer is a standing row that says closed,
        /// or the server row saying closed.
        /// </para>
        /// </summary>
        public static MaintenanceState For(MaintenanceArea area)
        {
            var table = s_table;

            // (a) NO TABLE. The fail-open branch, and the one a future seat will be
            //     tempted to "fix". Not traced per call: this is the resting state on
            //     every device that has not yet had a reply, and a throttled line per
            //     area per five seconds would drown the log for a non-event.
            //
            //     ⭐ THE STORE IS THE ONE EXCEPTION, AND IT FAILS *CLOSED* (owner ruling
            //     2026-08-27). WO-1243 raised this as an open question rather than
            //     assuming it, and money went real the same day.
            //
            //     The asymmetry is genuinely different for money than for gameplay. A
            //     wrongly-OPEN store can CHARGE real people during the exploit it was
            //     sealed for, and that is irreversible - refunds, chargebacks, trust. A
            //     wrongly-CLOSED store only defers a sale. For farming, raids, arena and
            //     dungeons the cost of a blip is a denied session, so the owner's original
            //     reasoning still governs them: "i cannot help if server is unreachable",
            //     and closing the whole game buys nothing when she cannot act anyway.
            //
            //     ⛔ DO NOT "unify" this back into one rule for all six. The inconsistency
            //     is the point, it was ruled deliberately, and MaintenanceTogglesRegression
            //     pins both halves.
            if (table == null)
            {
                return area == MaintenanceArea.Store
                    ? new MaintenanceState(true, IdOf(MaintenanceArea.Store), UnreadableStoreMessage)
                    : MaintenanceState.Open;
            }

            // (b) SERVER FIRST, unconditionally. A full maintenance window outranks
            //     every per-area row, including one that says the area is fine.
            if (table.TryGetValue(ServerAreaId, out var srv) && srv.Closed)
                return new MaintenanceState(true, ServerAreaId, srv.Message);

            // (c) The area's own row.
            string id = IdOf(area);
            if (table.TryGetValue(id, out var row) && row.Closed)
                return new MaintenanceState(true, id, row.Message);

            // (d) NO ROW FOR THIS AREA IS OPEN. Deliberate, and the inverse of
            //     WO-1223: api/schema.sql seeds with ON CONFLICT DO NOTHING, which
            //     does not back-fill an already-provisioned database. Under fail-open
            //     a missing row costs nothing.
            //
            //     ⭐ THE STORE'S FAIL-CLOSED RULE DELIBERATELY DOES *NOT* REACH HERE.
            //     Branch (a) is "we could not read the table at all"; this is "we read it
            //     fine and it has no row for this area". Those are different facts. Closing
            //     the store on a merely-absent row would hand the ON CONFLICT DO NOTHING
            //     seed trap - the one that shut two dungeons in production this week - the
            //     power to shut off revenue on a perfectly healthy server. The owner ruled
            //     on UNREACHABILITY, not on incomplete seeding.
            return MaintenanceState.Open;
        }

        /// <summary>Convenience. Same contract as <see cref="For"/>.</summary>
        public static bool IsClosed(MaintenanceArea area) => For(area).Closed;

        /// <summary>
        /// THE ONE CALL A REFUSAL SITE MAKES.
        /// <para>
        /// Returns true when the caller must REFUSE, and hands back the player-facing
        /// sentence to show. Every true is TRACED with the area, the toggle that closed
        /// it and the provenance, so a report of "raids do nothing" is triageable from a
        /// log line instead of a theory (CLAUDE.md section 12).
        /// </para>
        /// <para>
        /// A banner without an actual gate is decoration: the caller MUST return, not
        /// merely warn. Every call site in this repo does, and
        /// MaintenanceTogglesRegression case [gate-sites] source-lints that each one
        /// is followed by a return.
        /// </para>
        /// </summary>
        /// <param name="area">The area being entered.</param>
        /// <param name="what">What the player tried to do, for the log only. ASCII.</param>
        /// <param name="playerMessage">Never null when this returns true.</param>
        public static bool Refuses(MaintenanceArea area, string what, out string playerMessage)
        {
            var state = For(area);
            if (!state.Closed)
            {
                playerMessage = null;
                return false;
            }

            playerMessage = !string.IsNullOrWhiteSpace(state.Message)
                ? state.Message
                : DefaultBannerFor(area, state.ClosedBy);

            FlowTrace.Throttle(Sys, "refuse:" + IdOf(area), 5f,
                "REFUSED '" + (what ?? "?") + "': area=" + IdOf(area) +
                " closedBy=" + (state.ClosedBy ?? "?") +
                " provenance=" + s_provenance +
                " (operator kill switch, WO-1243). This is the COURTESY gate; the seal " +
                "itself is enforced server side in api/_lib/maintenance.js.");
            return true;
        }

        // ---------------------------------------------------------------------
        //  THE ONE PRODUCER OF A MAINTENANCE BANNER LINE (WO-1245)
        // ---------------------------------------------------------------------
        //  There used to be TWO. This file exposed BannerText(), and
        //  MaintenanceBannerDriver formatted its own line privately in a method
        //  called Line(). Same sealed state, two different sentences:
        //      BannerText()  -> "MAINTENANCE ON RAIDS AND THE STORE - <message>"
        //      driver.Line() -> "MAINTENANCE ON RAIDS - <message>"
        //  The driver's is what reached the screen; BannerText() was read only by
        //  MaintenanceTogglesRegression, so WO-1243's banner assertions proved a
        //  string no player would ever be shown. Found by photographing the banner
        //  after every marker had gone green.
        //
        //  BannerText() IS GONE. It is deliberately NOT kept "for the tests" -
        //  a second entry point recreates the split on day one. LineFor is the
        //  single formatter and BuildLines is the single roll; the driver, the
        //  payload trace below and the regression all go through them.
        // ---------------------------------------------------------------------

        /// <summary>
        /// THE banner line for ONE sealed area. The only place a maintenance line is
        /// formatted anywhere in the game.
        /// <para>
        /// Leads with the literal word MAINTENANCE and NAMES the area every time, then
        /// the operator's own sentence when she wrote one. Strip the colour, the plate
        /// and the font and the line still says exactly what is happening - the owner is
        /// red/green colourblind and no meaning here may live in hue (CLAUDE.md s7).
        /// </para>
        /// <para>
        /// Attribution comes from <see cref="MaintenanceState.ClosedBy"/>, so an area
        /// closed by a full `server` window reads MAINTENANCE ON THE REALM rather than
        /// naming the area - which is the truth of what is happening to the player.
        /// </para>
        /// <para>Returns null when the state is not closed: nothing sealed, nothing announced.</para>
        /// </summary>
        public static string LineFor(MaintenanceArea area, MaintenanceState state)
        {
            if (!state.Closed) return null;
            string head = "MAINTENANCE ON " + DisplayName(state.ClosedBy ?? IdOf(area));
            if (string.IsNullOrWhiteSpace(state.Message)) return head;
            return head + " - " + state.Message;
        }

        /// <summary>
        /// Build the whole roll: one <see cref="LineFor"/> line per sealed area.
        /// <para>
        /// A full `server` window OUTRANKS everything, so it contributes its single line
        /// and nothing else is listed - naming five areas when the whole realm is down is
        /// noise, not information.
        /// </para>
        /// <para>
        /// The caller supplies the list and it is CLEARED first, so the per-second driver
        /// rebuild allocates nothing. Never throws; a null table yields zero lines, which
        /// under fail-open is the correct resting answer.
        /// </para>
        /// </summary>
        public static void BuildLines(List<string> into)
        {
            if (into == null) return;
            into.Clear();

            var server = For(MaintenanceArea.Server);
            if (server.Closed)
            {
                into.Add(LineFor(MaintenanceArea.Server, server));
                return;
            }

            for (int i = 0; i < AreaIds.Length; i++)
            {
                var area = (MaintenanceArea)i;
                if (area == MaintenanceArea.Server) continue;
                var st = For(area);
                if (st.Closed) into.Add(LineFor(area, st));
            }
        }

        /// <summary>
        /// The whole roll on ONE line, for the log only. Built FROM <see cref="BuildLines"/>
        /// so it can never drift from what the player is shown - it is the same sentences,
        /// pipe-joined. Never call this to feed a UI surface.
        /// </summary>
        public static string JoinedLinesForLog()
        {
            var lines = new List<string>(AreaIds.Length);
            BuildLines(lines);
            return lines.Count == 0 ? "" : string.Join(" | ", lines.ToArray());
        }

        /// <summary>Upper-case display word for an area id. ASCII only.</summary>
        public static string DisplayName(string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return "THIS AREA";
            switch (areaId)
            {
                case "farming": return "FARMING";
                case "raiding": return "RAIDS";
                case "arena": return "THE ARENA";
                case "dungeons": return "DUNGEONS";
                case "store": return "THE STORE";
                case "server": return "THE REALM";
                default: return "THIS AREA";
            }
        }

        /// <summary>
        /// The fallback sentence when the operator sealed an area without authoring a
        /// message. It still names the area and still leads with the word MAINTENANCE.
        /// (tools/maintenance-toggle.mjs refuses a seal with no message, so this is a
        /// belt-and-braces path for a row written straight into Neon by hand.)
        /// </summary>
        public static string DefaultBannerFor(MaintenanceArea area, string closedBy)
        {
            string id = string.IsNullOrEmpty(closedBy) ? IdOf(area) : closedBy;
            if (string.Equals(id, ServerAreaId, StringComparison.Ordinal))
                return "MAINTENANCE ON THE REALM - the realm is closed for maintenance. Please try again shortly.";
            return "MAINTENANCE ON " + DisplayName(id) + " - this area is closed for maintenance. Please try again shortly.";
        }

        // ---------------------------------------------------------------------
        //  WRITE SIDE
        // ---------------------------------------------------------------------

        /// <summary>
        /// Drop the standing table. Under fail-open that means EVERYTHING IS OPEN,
        /// which is the correct resting state. Used by the kill switch and by the
        /// regression oracle between cases.
        /// </summary>
        public static void Clear(string provenance = ProvenanceDefault)
        {
            s_table = null;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;
        }

        /// <summary>
        /// The endpoint answered, and what it said is "this feature does not exist here"
        /// (HTTP 404). Everything resolves OPEN, the store INCLUDED.
        /// <para>
        /// ⭐ A 404 IS NOT AN OUTAGE, AND CONFLATING THE TWO SEALED A LIVE STORE. The
        /// owner's fail-closed ruling for the store is about UNREACHABILITY - "i cannot
        /// help if server is unreachable". A 404 is a SUCCESSFUL HTTP conversation whose
        /// content is "the toggle system is not deployed here". If it is not deployed,
        /// no toggle row exists, nothing was ever sealed, and sealing the store is
        /// inventing an outage rather than reporting one.
        /// </para>
        /// <para>
        /// Found on device 2026-08-27, minutes after the fail-closed rule shipped: the
        /// endpoint had not been deployed yet, every poll 404'd, and the owner's first
        /// screen said the store was closed. Under the old code that state was
        /// PERMANENT for that build - the store could never open, because a 404 never
        /// stops being a 404.
        /// </para>
        /// <para>
        /// ⛔ THIS IS NOT A WEAKENING OF THE SEAL, and the reason is that the client gate
        /// was never the enforcement. `store` has SERVER-SIDE teeth in
        /// api/purchases/quote.js, which refuses to issue a quote while the store is
        /// sealed. So a client that wrongly believes the store is open still cannot
        /// complete a purchase during a real seal. A TIMEOUT or a 5xx still means
        /// unreachable, and the store still fails CLOSED on those.
        /// </para>
        /// </summary>
        public static void MarkFeatureAbsent()
        {
            // An EMPTY but non-null table: readable, and holding no seals. That routes
            // For() past the unreachable branch and into "no row for this area is open",
            // which is exactly the truth here.
            s_table = new Dictionary<string, MaintenanceState>(StringComparer.Ordinal);
            s_provenance = ProvenanceAbsent;
        }

        /// <summary>
        /// Parse a payload and ATOMICALLY swap it in.
        /// <para>
        /// Returns FALSE on a hard parse failure, and on that path the EXISTING table is
        /// left exactly as it was - a malformed live payload must never blank a good
        /// standing table half way through a row loop.
        /// </para>
        /// <para>
        /// A payload whose readOk is false is the SERVER telling us it could not read
        /// its own table. That is the fail-open case arriving as a 200, and it clears
        /// the table rather than being treated as "nothing is sealed but we know it".
        /// </para>
        /// </summary>
        public static bool ApplyPayload(string json, string provenance)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                FlowTrace.Warn(Sys, "payload rejected: empty body from provenance='" +
                                    (provenance ?? "null") + "'. Fail-OPEN: the standing table is " +
                                    "unchanged (provenance=" + s_provenance + ").");
                return false;
            }

            MaintenancePayload dto = Guard.Try<MaintenancePayload>(
                Sys, "parse payload (" + (provenance ?? "null") + ")",
                () => JsonConvert.DeserializeObject<MaintenancePayload>(json),
                null);

            if (dto == null)
            {
                FlowTrace.Warn(Sys, "payload rejected: unparseable (provenance='" + (provenance ?? "null") +
                                    "'). Fail-OPEN by owner ruling 2026-08-27 - nothing is sealed on the " +
                                    "strength of a payload we could not read.");
                return false;
            }

            if (dto.Version != PayloadVersion)
            {
                FlowTrace.Warn(Sys, "payload version " + dto.Version + " != expected " + PayloadVersion +
                                    " - parsing anyway (forward-compatible).");
            }

            if (!dto.ReadOk)
            {
                // The endpoint answered 200 and told us its own read failed. Treat it as
                // the outage it is: clear to the open ground state and say so loudly.
                s_table = null;
                s_provenance = ProvenanceDefault;
                FlowTrace.Warn(Sys, "server reported readOk=false (reason='" + (dto.Reason ?? "?") +
                                    "') - the toggle table is unreadable ON THE SERVER. Every area is " +
                                    "OPEN (fail-open, owner ruling 2026-08-27). No seal can be applied " +
                                    "until the table reads again.");
                return true;
            }

            var next = new Dictionary<string, MaintenanceState>(StringComparer.Ordinal);
            int sealed_ = 0;
            int unknown = 0;

            if (dto.Areas != null)
            {
                foreach (var pair in dto.Areas)
                {
                    string id = pair.Key;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (Array.IndexOf(AreaIds, id) < 0)
                    {
                        unknown++;
                        FlowTrace.Step(Sys, "payload carries unknown area '" + id + "' - ignored.");
                        continue;
                    }
                    var row = pair.Value;
                    bool closed = row != null && row.Closed;
                    next[id] = new MaintenanceState(closed, closed ? (row.ClosedBy ?? id) : null, row?.Message);
                    if (closed) sealed_++;
                }
            }

            // ATOMIC: one assignment, whole table.
            s_table = next;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;

            if (sealed_ > 0)
            {
                FlowTrace.Warn(Sys, "payload accepted (provenance=" + s_provenance + ") rows=" + next.Count +
                                    " SEALED=" + sealed_ + " unknown=" + unknown +
                                    " banner=\"" + JoinedLinesForLog() + "\"");
            }
            else
            {
                FlowTrace.Step(Sys, "payload accepted (provenance=" + s_provenance + ") rows=" + next.Count +
                                    " sealed=0 unknown=" + unknown + " - nothing is closed.");
            }
            return true;
        }

        // ---------------------------------------------------------------------
        //  Wire DTOs - Newtonsoft. JsonUtility cannot express the 'areas' map.
        // ---------------------------------------------------------------------

        [Serializable]
        internal sealed class MaintenancePayload
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("readOk")] public bool ReadOk { get; set; }
            [JsonProperty("reason")] public string Reason { get; set; }
            [JsonProperty("areas")] public Dictionary<string, MaintenanceRow> Areas { get; set; }
        }

        [Serializable]
        internal sealed class MaintenanceRow
        {
            [JsonProperty("closed")] public bool Closed { get; set; }
            [JsonProperty("closedBy")] public string ClosedBy { get; set; }
            [JsonProperty("message")] public string Message { get; set; }
        }
    }
}
