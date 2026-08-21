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
// THE SAFETY DIRECTION IS ONE-WAY — read this before changing any branch:
//   EVERY failure resolves toward OPEN. Unknown id, unknown status string,
//   absent id, null id, empty table, version mismatch, bad parse — all OPEN.
//   A backend typo, a stale cache or a dropped network must NEVER lock a
//   player out of working content. The only thing that closes a door is a
//   well-formed row that explicitly says so. (WO-1114 §6, every line.)
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

namespace DeNelle.Core.World
{
    /// <summary>
    /// The dev-meaningful door state. Anything that is not <see cref="Open"/>
    /// closes the door. This enum is the ONLY thing code branches on.
    /// </summary>
    public enum DungeonDoorState
    {
        /// <summary>Enterable. The ground state, and the state every failure resolves to.</summary>
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

        /// <summary>The ground state. Every failure path returns this.</summary>
        public static DungeonDoorInfo OpenDefault => new DungeonDoorInfo(DungeonDoorState.Open, null, null, null);
    }

    /// <summary>
    /// Static, transport-free table of dungeon door states. Always answers;
    /// never throws; always resolves unknowns toward OPEN.
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

        /// <summary>The four AuthoredPortal dungeon ids this system's domain covers.
        /// Fixtures and probes (dg_descent_probe, dg_stair_rig, dg_stairwell_probe)
        /// have no portal and can never be gated; dg_hollow_roads is a CROSSROADS,
        /// not a dungeon, and is gated by FeatureFlags.BiomeRoads instead.
        /// ⛔ Never add an id here that has no AuthoredPortal row.</summary>
        public static readonly string[] PortalDungeonIds =
        {
            "dg_starter_loop",
            "dg_sunken_vault",
            "dg_bonecrypt",
            "dg_ember_deep",
        };

        // Swapped atomically by ApplyPayload. Never mutated in place.
        private static Dictionary<string, DungeonDoorInfo> s_table;
        private static string s_provenance = ProvenanceDefault;

        /// <summary>"live" | "cache" | "default" | "flag-off" — where the standing
        /// table came from. Surfaced in the blocked-entry trace so a headless log
        /// says WHY a door was closed.</summary>
        public static string Provenance => s_provenance;

        /// <summary>True once a payload has been accepted from any source.</summary>
        public static bool Loaded => s_table != null;

        /// <summary>Number of rows in the standing table (0 when all-open).</summary>
        public static int RowCount => s_table?.Count ?? 0;

        // ─────────────────────────────────────────────────────────────────────
        //  READ SIDE — always answers, never throws, unknown => Open
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve a dungeon's door. NEVER throws. A null/empty/unknown id, or an
        /// id absent from the payload, resolves <see cref="DungeonDoorState.Open"/>
        /// — absence is not a closure (WO-1114 §6 row 6).
        /// </summary>
        public static DungeonDoorInfo For(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId)) return DungeonDoorInfo.OpenDefault;
            var table = s_table;
            if (table == null) return DungeonDoorInfo.OpenDefault;
            return table.TryGetValue(dungeonId, out var info) ? info : DungeonDoorInfo.OpenDefault;
        }

        /// <summary>Convenience for the hot path. Same contract as <see cref="For"/>.</summary>
        public static bool IsOpen(string dungeonId) => For(dungeonId).IsOpen;

        // ─────────────────────────────────────────────────────────────────────
        //  WRITE SIDE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reset to the all-open ground state. Used by the kill switch and by the
        /// regression oracle between cases.
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

            FlowTrace.Step(Sys, "payload accepted (provenance=" + s_provenance + ") rows=" + next.Count +
                                " closed=" + closed + " unshipped=" + unshipped +
                                " portalCoverage=" + DescribePortalCoverage(next));
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Case-insensitive status parse. An unparseable value maps to
        /// <see cref="DungeonDoorState.Open"/> and warns. NEVER fails closed —
        /// a future backend typo must not lock a player out (WO-1114 §3).
        /// </summary>
        public static DungeonDoorState ParseState(string raw, string idForLog)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DungeonDoorState.Open;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "open": return DungeonDoorState.Open;
                case "sealed": return DungeonDoorState.Sealed;
                case "collapsed": return DungeonDoorState.Collapsed;
                case "rescue": return DungeonDoorState.Rescue;
                case "flooded": return DungeonDoorState.Flooded;
                default:
                    FlowTrace.Warn(Sys, "unknown status '" + raw + "' for id='" + (idForLog ?? "?") +
                                        "' - treating as OPEN.");
                    return DungeonDoorState.Open;
            }
        }

        /// <summary>One summary line naming which of the four portal ids the payload covers.</summary>
        private static string DescribePortalCoverage(Dictionary<string, DungeonDoorInfo> table)
        {
            var parts = new List<string>(PortalDungeonIds.Length);
            for (int i = 0; i < PortalDungeonIds.Length; i++)
            {
                string id = PortalDungeonIds[i];
                parts.Add(table.TryGetValue(id, out var info) ? id + "=" + info.State : id + "=absent(open)");
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
