// =============================================================================
// DungeonStatusCatalog — WO-1114, the remotely-flippable dungeon door state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// THE ONE RULE THIS FILE KEEPS:
//   A closed dungeon must read as WORLD, never as BUILD STATUS. This file owns
//   the DEV-MEANINGFUL half of that split — the `status` enum — and nothing
//   else. It carries NO player-facing prose of its own: the optional authored
//   headline/body ride through from the payload, and when they are absent the
//   CALLER falls back to canon-strings.json (see DungeonSealedDoorPanel /
//   DungeonPortal, both in DeNelle.Village, via VillageStrings.Canon).
//   ⛔ Do NOT type a player sentence into this file. CLAUDE.md §7.
//
// WHY IT IS TRANSPORT-FREE:
//   Pure state + parse. No MonoBehaviour, no coroutine, no UnityWebRequest.
//   That is what lets DungeonStatusRegression drive the whole fallback matrix
//   headlessly, with no network and no PlayMode. The fetch/cache lifecycle is
//   a SEPARATE file — DungeonStatusService (same folder).
//
// ⛔ THE SAFETY DIRECTION IS FAIL-CLOSED — read this before changing any branch.
//   OWNER RULING, 2026-08-26 (WO-1223), verbatim:
//       "not acesable if not in table, if in table and works then yes"
//   That INVERTS WO-1114 §6, and the inversion is the point. Until today a
//   dungeon with no row read OPEN, which is precisely why dg_healers_cottage
//   was reachable, black-screened the owner, and could not be sealed: there
//   was nothing to flip and the default let the player through anyway.
//
//   THE THREE-LINE CONTRACT, and there is no fourth state:
//     * A GATED id (PortalDungeonIds) is enterable ONLY when the standing
//       table carries a well-formed row for it whose status parses to `open`.
//     * EVERYTHING else about a gated id is CLOSED: absent id, null/empty id,
//       null table (no cache, server unreachable, timed out), rejected/empty/
//       malformed payload, a row with an unparseable status string.
//     * Two NAMED escapes, both deliberate and both loud:
//       (a) the kill switch — provenance "flag-off" (FeatureFlags.DungeonStatus
//           = 0) forces every door OPEN with no rebuild. It is the one lever
//           that survives a bad table, and it is why fail-closed is safe to ship.
//       (b) UngatedIds — reachable ids that are NOT dungeons (the Rootways
//           crossroads, fixtures, probes). Each carries a stated reason and is
//           pinned by DungeonStatusRegression. This is an ALLOWLIST, which is
//           what fail-closed means; it is not a fallback.
//
//   ⚠ THE COST, STATED OUT LOUD: a first-run player who has never reached the
//   network has no cache, so every gated dungeon reads CLOSED (as authored
//   WORLD prose, never as build status). A player who HAS reached the network
//   once keeps the cached table and is unaffected offline. That trade is the
//   owner's ruling; do not soften it back toward open without a new one.
//
// ATOMIC SWAP: ApplyPayload builds a whole new dictionary and assigns the
//   field in one statement. A malformed live payload therefore leaves the
//   previously-good (cached) table STANDING — it never blanks the world
//   half-way through a row loop.
//
// Instrumentation: FlowTrace system tag "DungeonStatus" (CLAUDE.md §12).
//   ⛔ Never strip these calls (owner ruling 2026-08-09). Flag them off if the
//   system is ever proven boring; the calls stay.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;

// =============================================================================
// OWNER RULING 2026-08-26 -- THE OFFLINE FIRST-RUN CONSEQUENCE IS ACCEPTED.
//
// Fail-closed means a BRAND-NEW player with no cache and no network sees EVERY
// gatable dungeon SEALED (provenance 'no-table'). The owner was shown that
// consequence explicitly, with the alternatives (a starter allowlist; a
// connectivity-specific door message), and ruled: SEALED IS CORRECT.
//
// !! So the offline-sealed world is the FEATURE, not a bug report waiting to
// happen. Do NOT "fix" it by reintroducing an open default for the no-table,
// unreachable, timeout, malformed-payload or unparseable-status branches --
// ALL FIVE of those were wide open before today (ParseState literally ended
// `default: return Open`), which is why the gate was no gate at all in every
// degraded condition.
//
// The sanctioned levers, both already pinned by DungeonStatusRegression:
//   * FeatureFlags.DungeonStatus = 0  -- the kill switch, provenance 'flag-off'
//   * UngatedIds                      -- doors that can never have a row
// Cached players are unaffected offline; they keep the last known table.
// =============================================================================

namespace DeNelle.Core.World
{
    /// <summary>
    /// The dev-meaningful door state. Anything that is not <see cref="Open"/>
    /// closes the door. This enum is the ONLY thing code branches on.
    /// </summary>
    public enum DungeonDoorState
    {
        /// <summary>Enterable. ⚠ NO LONGER the ground state — since the owner's
        /// fail-closed ruling (2026-08-26) this is reached ONLY by a well-formed row
        /// that says "open". Every failure resolves to <see cref="Sealed"/> instead.</summary>
        Open = 0,
        /// <summary>Barred from the outside.</summary>
        Sealed = 1,
        /// <summary>The way in has fallen in.</summary>
        Collapsed = 2,
        /// <summary>Closed while a rescue is under way.</summary>
        Rescue = 3,
        /// <summary>Under water.</summary>
        Flooded = 4,
    }

    /// <summary>
    /// One dungeon's resolved door state plus its OPTIONAL authored dressing.
    /// A readonly struct so the hot proximity tick in DungeonPortal can re-read
    /// it every 0.15 s with no allocation.
    /// </summary>
    public readonly struct DungeonDoorInfo
    {
        /// <summary>The dev-meaningful state. See <see cref="DungeonDoorState"/>.</summary>
        public readonly DungeonDoorState State;

        /// <summary>Authored one-line prose for the interact prompt. MAY be null or
        /// empty — the caller then falls back to canon-strings.json.</summary>
        public readonly string Headline;

        /// <summary>Authored body prose for the dialogue frame. MAY be null or empty
        /// — the caller then falls back to canon-strings.json.</summary>
        public readonly string Body;

        /// <summary>Art key for the door treatment. MAY be null or empty — the caller
        /// then uses the default seal treatment.</summary>
        public readonly string Sigil;

        /// <summary>True when the door lets the player through.</summary>
        public bool IsOpen => State == DungeonDoorState.Open;

        public DungeonDoorInfo(DungeonDoorState state, string headline, string body, string sigil)
        {
            State = state;
            Headline = headline;
            Body = body;
            Sigil = sigil;
        }

        /// <summary>An explicitly-open door. ⚠ Only the kill switch and the
        /// <see cref="DungeonStatusCatalog.UngatedIds"/> allowlist reach this now —
        /// it is NO LONGER the failure default (owner ruling 2026-08-26, WO-1223).</summary>
        public static DungeonDoorInfo OpenDefault => new DungeonDoorInfo(DungeonDoorState.Open, null, null, null);

        /// <summary>
        /// THE GROUND STATE since the fail-closed ruling. Every unresolved gated id
        /// returns this: <see cref="DungeonDoorState.Sealed"/> with NO authored prose,
        /// so DungeonSealedDoorPanel falls back to canon-strings.json
        /// (dungeonSealedHeadline / dungeonSealedBody) and the player reads WORLD,
        /// never build status. ⛔ Do not type a player sentence here (CLAUDE.md §7).
        /// </summary>
        public static DungeonDoorInfo ClosedDefault => new DungeonDoorInfo(DungeonDoorState.Sealed, null, null, null);
    }

    /// <summary>
    /// Static, transport-free table of dungeon door states. Always answers;
    /// never throws; resolves every unknown toward CLOSED (owner ruling 2026-08-26).
    /// </summary>
    public static class DungeonStatusCatalog
    {
        /// <summary>FlowTrace system tag for the fetch/cache/parse/resolution lane.
        /// The APPEARANCE lane uses "Portal" instead — do not cross them.</summary>
        public const string Sys = "DungeonStatus";

        /// <summary>Payload schema version this build was written against. A payload
        /// carrying a DIFFERENT version is still parsed (forward-compatible); only a
        /// hard parse failure rejects. See <see cref="ApplyPayload"/>.</summary>
        public const int PayloadVersion = 1;

        // Provenance literals — also the values the oracle asserts.
        public const string ProvenanceLive = "live";
        public const string ProvenanceCache = "cache";
        public const string ProvenanceDefault = "default";
        public const string ProvenanceFlagOff = "flag-off";

        /// <summary>
        /// THE GATED DOMAIN — the AuthoredPortal dungeon ids this system closes over.
        /// Since the fail-closed ruling this list is also the FENCE: an id in here is
        /// enterable only while the table says so, so membership is a real safety
        /// property and not just a lint target.
        /// <para>
        /// ⛔ Never add an id here that has no AuthoredPortal row — DungeonStatusRegression
        /// case [door-ids] source-lints exactly that, and case [door-coverage] proves the
        /// list is TOTAL over every reachable dungeon.
        /// </para>
        /// </summary>
        public static readonly string[] PortalDungeonIds =
        {
            "dg_starter_loop",
            "dg_sunken_vault",
            "dg_bonecrypt",
            "dg_ember_deep",

            // ── OWNER RULING, 2026-08-26 (WO-1223), verbatim: ────────────────────
            //   "not acesable if not in table, if in table and works then yes"
            // Both ids were REACHABLE (authored portal + injected def + enabled build
            // scene) and in NEITHER list, so [door-coverage] red-flagged them by name
            // and refused to self-resolve. The owner ruled them GATABLE, not exempt:
            // they belong in the domain that fails closed, NOT in UngatedIds.
            // dg_healers_cottage is the one she black-screened in with no row to flip.
            "dg_folks_granary",
            "dg_healers_cottage",
        };

        /// <summary>
        /// THE ALLOWLIST — reachable ids that are NOT dungeons, and therefore sit
        /// outside the fail-closed fence. Every entry needs a stated reason; this is
        /// the ONLY way an id escapes the closed default other than the kill switch.
        /// <para>
        /// ⛔ Adding an id here is how fail-closed gets softened back to fail-open one
        /// entry at a time. It is a design ruling, never an implementer's convenience.
        /// DungeonStatusRegression consumes THIS array (it no longer keeps a second
        /// copy) so the two cannot drift — CLAUDE.md's duplicated-state failure.
        /// </para>
        /// <list type="bullet">
        /// <item>dg_hollow_roads — the Rootways is a CROSSROADS, not a dungeon: one
        /// mouth, four arms into the biomes. Its portal row is DERIVED
        /// (DungeonWorldPortalSpawner.TryDeriveHollowRoadsPortal) and it is switched by
        /// FeatureFlags.BiomeRoads, not by a door state. It will never have a row, so
        /// fail-closed would permanently bar a passage that has no dungeon behind it.</item>
        /// <item>dg_descent_probe / dg_stair_rig / dg_stairwell_probe — test fixtures and
        /// probes with no AuthoredPortal at all. Nothing can place a door in front of a
        /// player for them, so there is nothing to close.</item>
        /// </list>
        /// </summary>
        public static readonly string[] UngatedIds =
        {
            "dg_hollow_roads", "dg_descent_probe", "dg_stair_rig", "dg_stairwell_probe",
        };

        /// <summary>True when <paramref name="dungeonId"/> is on the <see cref="UngatedIds"/>
        /// allowlist — i.e. outside this system by design, not by accident.</summary>
        public static bool IsUngated(string dungeonId) =>
            !string.IsNullOrEmpty(dungeonId) && Array.IndexOf(UngatedIds, dungeonId) >= 0;

        // Swapped atomically by ApplyPayload. Never mutated in place.
        private static Dictionary<string, DungeonDoorInfo> s_table;
        private static string s_provenance = ProvenanceDefault;

        /// <summary>"live" | "cache" | "default" | "flag-off" — where the standing
        /// table came from. Surfaced in the blocked-entry trace so a headless log
        /// says WHY a door was closed.</summary>
        public static string Provenance => s_provenance;

        /// <summary>True once a payload has been accepted from any source.</summary>
        public static bool Loaded => s_table != null;

        /// <summary>Number of rows in the standing table. ⚠ 0 no longer means "all open" —
        /// since the fail-closed ruling a table with no rows closes every gated door.</summary>
        public static int RowCount => s_table?.Count ?? 0;

        // ─────────────────────────────────────────────────────────────────────
        //  READ SIDE — always answers, never throws. ABSENCE => CLOSED.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve a dungeon's door. NEVER throws.
        /// <para>
        /// ⛔ FAIL-CLOSED (owner ruling 2026-08-26, WO-1223: <i>"not acesable if not in
        /// table, if in table and works then yes"</i>). A null/empty id, an id absent from
        /// the standing table, and a null table (no cache / server unreachable / timed out
        /// / payload rejected) ALL resolve <see cref="DungeonDoorState.Sealed"/>. The only
        /// ways through are a row that parses to <c>open</c>, the kill switch
        /// (<see cref="ProvenanceFlagOff"/>), and the <see cref="UngatedIds"/> allowlist.
        /// </para>
        /// <para>
        /// Every refusal is TRACED (CLAUDE.md §12 — no silent failures), throttled to
        /// ~1/5 s per id because DungeonPortal re-reads this on a 0.15 s proximity tick.
        /// </para>
        /// </summary>
        public static DungeonDoorInfo For(string dungeonId)
        {
            // (a) KILL SWITCH. DungeonStatusService.Bootstrap stamps this provenance when
            //     FeatureFlags.DungeonStatus is off. It is the one lever that survives a
            //     bad table with no rebuild, so it wins over everything below.
            if (string.Equals(s_provenance, ProvenanceFlagOff, StringComparison.Ordinal))
                return DungeonDoorInfo.OpenDefault;

            // (b) A null/empty id is not a dungeon. Closed, and loud - nothing should be
            //     asking about one, so this is a caller bug, not a data state.
            if (string.IsNullOrEmpty(dungeonId))
            {
                FlowTrace.Throttle(Sys, "for-null-id", 5f,
                    "For(null/empty id) - CLOSED by the fail-closed default (owner ruling 2026-08-26). " +
                    "A caller is asking about a dungeon it cannot name.");
                return DungeonDoorInfo.ClosedDefault;
            }

            // (c) ALLOWLIST. Crossroads, fixtures, probes - outside this system by design.
            if (IsUngated(dungeonId)) return DungeonDoorInfo.OpenDefault;

            var table = s_table;

            // (d) NO TABLE AT ALL: no cache on disk, or the server was unreachable /
            //     timed out / answered garbage and the payload was rejected. Under the
            //     old fail-open default this returned OPEN, which is the hole the ruling
            //     closes: an unreachable server could not gate anything.
            if (table == null)
            {
                FlowTrace.Throttle(Sys, "for-no-table:" + dungeonId, 5f,
                    "'" + dungeonId + "' CLOSED: no standing status table (provenance=" + s_provenance +
                    "). No cache, or the fetch failed / timed out / was rejected. Fail-closed by " +
                    "owner ruling 2026-08-26 (WO-1223).");
                return DungeonDoorInfo.ClosedDefault;
            }

            // (e) TABLE PRESENT, NO ROW FOR THIS ID. The literal case the owner ruled on.
            if (!table.TryGetValue(dungeonId, out var info))
            {
                FlowTrace.Throttle(Sys, "for-no-row:" + dungeonId, 5f,
                    "'" + dungeonId + "' CLOSED: NOT IN THE TABLE (rows=" + table.Count +
                    ", provenance=" + s_provenance + "). \"not acesable if not in table\" - " +
                    "owner ruling 2026-08-26 (WO-1223).");
                return DungeonDoorInfo.ClosedDefault;
            }

            return info;
        }

        /// <summary>Convenience for the hot path. Same contract as <see cref="For"/>.</summary>
        public static bool IsOpen(string dungeonId) => For(dungeonId).IsOpen;

        /// <summary>
        /// WO-1223 — THE REVERSE DIRECTION. Which of <see cref="PortalDungeonIds"/> the
        /// STANDING table carries no row for.
        /// <para>
        /// ⚠ CORRECTED 2026-08-26: this doc used to say "an absent id resolves OPEN and
        /// always will". The owner ruled the opposite the same day - an absent id now
        /// resolves CLOSED (see <see cref="For"/>), so this detector names the doors that
        /// are SHUT for want of a row, not doors that are merely ungateable.
        /// The original defect stands: the absence was SILENT. <see cref="ApplyPayload"/>
        /// has detected the other direction since day one (a row naming a dungeon this
        /// build does not ship, :FlowTrace.Step "payload carries unshipped id"); the
        /// direction that actually cost the owner an ungateable black screen — a
        /// SHIPPED DUNGEON WITH NO ROW — logged nothing at all.
        /// </para>
        /// <para>
        /// Returned as data, not just logged, so DungeonStatusRegression can pin the
        /// detector itself rather than trusting a log line nobody reads.
        /// </para>
        /// </summary>
        /// <returns>Never null. Empty when every shipped portal id has a row.</returns>
        public static string[] MissingPortalRows()
        {
            var table = s_table;
            var missing = new List<string>(PortalDungeonIds.Length);
            for (int i = 0; i < PortalDungeonIds.Length; i++)
            {
                string id = PortalDungeonIds[i];
                if (table == null || !table.ContainsKey(id)) missing.Add(id);
            }
            return missing.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  WRITE SIDE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Drop the standing table. ⚠ RENAMED IN MEANING 2026-08-26: this used to be
        /// "reset to the all-open ground state" and it no longer is. With no table every
        /// GATED id resolves CLOSED (<see cref="For"/> branch d); only the kill-switch
        /// provenance <see cref="ProvenanceFlagOff"/> still forces open. Used by the
        /// kill switch and by the regression oracle between cases.
        /// </summary>
        public static void Clear(string provenance = ProvenanceDefault)
        {
            s_table = null;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;
        }

        /// <summary>
        /// Parse a §3 payload and ATOMICALLY swap it in.
        /// <para>
        /// Returns FALSE on a hard parse failure — and on that path the EXISTING
        /// table is left exactly as it was. A malformed live payload must never
        /// blank a good cached table, and (see DungeonStatusService) must never be
        /// written to the cache file.
        /// </para>
        /// </summary>
        /// <param name="json">Raw payload text.</param>
        /// <param name="provenance">"live" | "cache" | a test label.</param>
        public static bool ApplyPayload(string json, string provenance)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                FlowTrace.Fail(Sys, "payload rejected: empty/whitespace body from provenance='" +
                                    (provenance ?? "null") + "' - keeping provenance=" + s_provenance);
                return false;
            }

            DungeonStatusPayload dto = Guard.Try<DungeonStatusPayload>(
                Sys, "parse payload (" + (provenance ?? "null") + ")",
                () => JsonConvert.DeserializeObject<DungeonStatusPayload>(json),
                null);

            if (dto == null || dto.Dungeons == null)
            {
                // Guard.Try already reported the exception through FlowTrace.Fail.
                FlowTrace.Fail(Sys, "payload rejected: no 'dungeons' map (provenance='" +
                                    (provenance ?? "null") + "') - keeping provenance=" + s_provenance);
                return false;
            }

            if (dto.Version != PayloadVersion)
            {
                // Forward-compatible ON PURPOSE. A v2 payload with extra fields must
                // not blank the world; only a hard parse failure rejects.
                FlowTrace.Warn(Sys, "payload version " + dto.Version + " != expected " + PayloadVersion +
                                    " - parsing anyway (forward-compatible).");
            }

            var next = new Dictionary<string, DungeonDoorInfo>(StringComparer.Ordinal);
            int closed = 0;
            int unshipped = 0;

            foreach (var pair in dto.Dungeons)
            {
                string id = pair.Key;
                if (string.IsNullOrWhiteSpace(id)) continue;

                var row = pair.Value;
                DungeonDoorState state = ParseState(row?.Status, id);
                next[id] = new DungeonDoorInfo(state, row?.Headline, row?.Body, row?.Sigil);

                if (state != DungeonDoorState.Open) closed++;
                if (Array.IndexOf(PortalDungeonIds, id) < 0)
                {
                    unshipped++;
                    // Kept in the table (harmless - nothing queries it) but counted.
                    FlowTrace.Step(Sys, "payload carries unshipped id '" + id + "' - kept, nothing queries it.");
                }
            }

            // ATOMIC: one assignment, whole table.
            s_table = next;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;

            // WO-1223 — THE OTHER DIRECTION, and it is the expensive one. Above, a row
            // naming a dungeon we do not ship gets a Step. A dungeon we DO ship that has
            // no row got nothing, which is how a reachable dungeon stayed ungateable
            // without anyone being told.
            // ⚠ ESCALATED Warn -> Fail, 2026-08-26: under the owner's fail-closed ruling an
            // uncovered id is no longer a harmless silence - it is a SHIPPED DUNGEON NO PLAYER
            // CAN ENTER. This line is the operator's early warning that a row is missing from
            // dungeon_status, and it must be as loud as the outage it describes.
            string[] uncovered = MissingPortalRows();
            if (uncovered.Length > 0)
            {
                FlowTrace.Fail(Sys, "payload has NO row for " + uncovered.Length + " shipped portal id(s): " +
                                    string.Join(",", uncovered) + " - under the fail-closed default (owner " +
                                    "ruling 2026-08-26) those doors are CLOSED to every player until a row " +
                                    "exists in dungeon_status. Seed them.");
            }

            FlowTrace.Step(Sys, "payload accepted (provenance=" + s_provenance + ") rows=" + next.Count +
                                " closed=" + closed + " unshipped=" + unshipped +
                                " uncovered=" + uncovered.Length +
                                " portalCoverage=" + DescribePortalCoverage(next));
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Case-insensitive status parse.
        /// <para>
        /// ⛔ INVERTED 2026-08-26 (owner ruling, WO-1223). A blank or unparseable value
        /// now maps to <see cref="DungeonDoorState.Sealed"/> and WARNS. It used to map to
        /// Open so that "a backend typo cannot lock a player out"; the ruling is
        /// <i>"if in table and works then yes"</i> — a row whose status does not parse
        /// does not work, so it does not open. A typo now closes a door instead of
        /// silently opening one, which is the direction the owner chose.
        /// </para>
        /// </summary>
        public static DungeonDoorState ParseState(string raw, string idForLog)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                FlowTrace.Warn(Sys, "blank status for id='" + (idForLog ?? "?") +
                                    "' - a row that says nothing does not say open. CLOSED (Sealed) " +
                                    "by owner ruling 2026-08-26 (WO-1223).");
                return DungeonDoorState.Sealed;
            }
            switch (raw.Trim().ToLowerInvariant())
            {
                case "open": return DungeonDoorState.Open;
                case "sealed": return DungeonDoorState.Sealed;
                case "collapsed": return DungeonDoorState.Collapsed;
                case "rescue": return DungeonDoorState.Rescue;
                case "flooded": return DungeonDoorState.Flooded;
                default:
                    FlowTrace.Warn(Sys, "unknown status '" + raw + "' for id='" + (idForLog ?? "?") +
                                        "' - it does not parse, so it does not work. CLOSED (Sealed) " +
                                        "by owner ruling 2026-08-26 (WO-1223).");
                    return DungeonDoorState.Sealed;
            }
        }

        /// <summary>One summary line naming which of the four portal ids the payload covers.</summary>
        private static string DescribePortalCoverage(Dictionary<string, DungeonDoorInfo> table)
        {
            var parts = new List<string>(PortalDungeonIds.Length);
            for (int i = 0; i < PortalDungeonIds.Length; i++)
            {
                string id = PortalDungeonIds[i];
                parts.Add(table.TryGetValue(id, out var info) ? id + "=" + info.State : id + "=absent(CLOSED)");
            }
            return string.Join(",", parts);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wire DTOs — Newtonsoft. JsonUtility cannot express the `dungeons` map.
        // ─────────────────────────────────────────────────────────────────────

        [Serializable]
        internal sealed class DungeonStatusPayload
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("dungeons")] public Dictionary<string, DungeonStatusRow> Dungeons;
        }

        [Serializable]
        internal sealed class DungeonStatusRow
        {
            [JsonProperty("status")] public string Status;
            [JsonProperty("headline")] public string Headline;
            [JsonProperty("body")] public string Body;
            [JsonProperty("sigil")] public string Sigil;
        }
    }
}
