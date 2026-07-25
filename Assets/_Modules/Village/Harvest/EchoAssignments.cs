// =============================================================================
// EchoAssignments -- the per-echo lane assignment SEAM (WO-658/681 storage, WO-738
// functional lanes + level).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owns the STORAGE half of per-echo agency: a per-echo lane + level keyed by echo
// index, persisted in GameState.EchoLanes as a CSV of "lane:level" tokens
// (e.g. "harvest:3,idle,crafting:1"). WO-738 evolved the vocabulary from resource
// lanes (wood/iron/food) to FUNCTIONAL lanes (harvest/crafting/defense/exploration
// + idle) and enriched the token from a bare lane to lane:level.
//
// BACKWARD-COMPATIBLE READ (additive, default-on-read, NO migrator):
//   - a legacy resource token wood/iron/food  -> the functional Harvest lane
//   - a bare token with no ":level" suffix     -> level 1
//   - "idle"                                    -> Idle (carries no level)
// So the shipped "wood" starter value keeps working (reads Harvest / level 1).
//
// SCOPE (still deliberate): this seam STORES + REPORTS. The rate/dump split + the
// EchoLaneBonuses recompute that CONSUME this field are phase 2 (a later agent);
// this file is the contract they read. Public API preserved for the phase-2 callers
// EchoRosterView + EchoCardVM: LaneOf / Lanes / Assign / LabelFor / LaneIdle / Changed.
// =============================================================================
using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Static storage seam for per-Echo lane + level assignments. Reads/writes
    /// <see cref="GameState.EchoLanes"/> (a CSV of "lane:level" tokens); raises
    /// <see cref="Changed"/> after any write so the card + HUD refresh.
    /// </summary>
    public static class EchoAssignments
    {
        // ── Functional lane tokens (WO-738) ──────────────────────────────────
        public const string LaneIdle        = "idle";
        public const string LaneHarvest     = "harvest";
        public const string LaneCrafting    = "crafting";
        public const string LaneDefense     = "defense";
        public const string LaneExploration = "exploration";

        // ── Legacy resource-lane tokens (pre-738), kept as COMPATIBILITY ALIASES
        //    so any lingering reference compiles and any stored value normalizes
        //    forward to the functional Harvest lane on read. Not in Lanes (the
        //    picker offers the functional lanes only).
        public const string LaneWood = "wood";
        public const string LaneIron = "iron";
        public const string LaneFood = "food";

        /// <summary>The assignable functional lanes, in display order (Idle is a state, not a pick).</summary>
        public static readonly string[] Lanes = { LaneHarvest, LaneCrafting, LaneDefense, LaneExploration };

        /// <summary>The lanes the picker actually OFFERS right now -- only the LIVE lanes (Harvest,
        /// Crafting). Defense + Exploration stay in <see cref="Lanes"/> (constants + LabelFor +
        /// NormalizeLane intact) so any already-stored token still reads back, but they are NOT
        /// offered as picks: their unlock is not designed, so the card shows no stub/teaser rows
        /// (owner ruling 2026-07-24).</summary>
        public static readonly string[] PickableLanes = { LaneHarvest, LaneCrafting };

        /// <summary>Raised after any lane/level assignment changes (the card + HUD listen).</summary>
        public static event Action Changed;

        // ── Read API (bare lane token; level parsed separately) ───────────────

        /// <summary>
        /// The functional lane assigned to the Echo at <paramref name="echoIndex"/> (bare token,
        /// no ":level"). Absent state / out-of-range index reads the safe defaults: index 0 =
        /// Harvest (the starter Echo auto-assignment), any later index = Idle. Legacy resource
        /// tokens (wood/iron/food) normalize forward to Harvest.
        /// </summary>
        public static string LaneOf(int echoIndex)
        {
            return ParseLane(RawToken(echoIndex), echoIndex);
        }

        /// <summary>
        /// The level (1..maxLevel) of the Echo at <paramref name="echoIndex"/>. A bare legacy
        /// token (no ":level") reads level 1 (default-on-read); the value is clamped to
        /// [1, <see cref="EchoBalanceCatalog.MaxLevel"/>].
        /// </summary>
        public static int LevelOf(int echoIndex)
        {
            return ClampLevel(ParseLevel(RawToken(echoIndex)));
        }

        // ── Write API (rebuilds the CSV of richer lane:level tokens) ──────────

        /// <summary>
        /// Assign the Echo at <paramref name="echoIndex"/> to <paramref name="lane"/> (a functional
        /// lane id, or a legacy/idle token -- normalized). PRESERVES the echo's current level.
        /// Persists via GameStateService.Save() and raises <see cref="Changed"/>. Returns false
        /// (logged, never silent) when state is absent or the index is out of range. [Flow:Echo].
        /// </summary>
        public static bool Assign(int echoIndex, string lane)
        {
            using var _t = FlowTrace.Enter("Echo", "AssignLane");
            var gs = GameStateService.Instance;
            var s = gs != null ? gs.State : null;
            if (s == null)
            {
                FlowTrace.Warn("Echo", $"AssignLane(echo={echoIndex}, lane='{lane}') before GameState -- ignored.");
                return false;
            }

            int count = OwnedCount(s);
            if (echoIndex < 0 || echoIndex >= count)
            {
                FlowTrace.Warn("Echo", $"AssignLane: echo index {echoIndex} out of range (owned {count}) -- ignored.");
                return false;
            }

            int level = LevelOf(echoIndex);   // keep the echo's level across a lane change
            var tokens = BuildTokens(count);
            string before = tokens[echoIndex];
            tokens[echoIndex] = BuildToken(lane, level);
            s.EchoLanes = string.Join(",", tokens);
            gs.Save();

            FlowTrace.Step("Echo",
                $"AssignLane: echo {echoIndex} '{before}' -> '{tokens[echoIndex]}' (lanes now [{s.EchoLanes}]). " +
                "Storage seam only -- the rate-split / bonus recompute consume this in phase 2.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Set the LEVEL (clamped to [1, <see cref="EchoBalanceCatalog.MaxLevel"/>]) of the Echo at
        /// <paramref name="echoIndex"/>, keeping its current lane. Persists + raises <see cref="Changed"/>.
        /// Returns false (logged) when state is absent or the index is out of range. [Flow:Echo].
        /// </summary>
        public static bool SetLevel(int echoIndex, int level)
        {
            using var _t = FlowTrace.Enter("Echo", "SetLevel");
            var gs = GameStateService.Instance;
            var s = gs != null ? gs.State : null;
            if (s == null)
            {
                FlowTrace.Warn("Echo", $"SetLevel(echo={echoIndex}, level={level}) before GameState -- ignored.");
                return false;
            }

            int count = OwnedCount(s);
            if (echoIndex < 0 || echoIndex >= count)
            {
                FlowTrace.Warn("Echo", $"SetLevel: echo index {echoIndex} out of range (owned {count}) -- ignored.");
                return false;
            }

            int clamped = ClampLevel(level);
            string lane = LaneOf(echoIndex);
            var tokens = BuildTokens(count);
            string before = tokens[echoIndex];
            tokens[echoIndex] = BuildToken(lane, clamped);
            s.EchoLanes = string.Join(",", tokens);
            gs.Save();

            FlowTrace.Step("Echo",
                $"SetLevel: echo {echoIndex} lane '{lane}' level -> {clamped} (was token '{before}'; lanes now [{s.EchoLanes}]).");
            Changed?.Invoke();
            return true;
        }

        /// <summary>ASCII display label for a lane id ("harvest" -> "Harvest"; legacy/idle normalized).</summary>
        public static string LabelFor(string lane)
        {
            switch (NormalizeLane(lane))
            {
                case LaneHarvest:     return "Harvest";
                case LaneCrafting:    return "Crafting";
                case LaneDefense:     return "Defense";
                case LaneExploration: return "Exploration";
                default:              return "Idle";
            }
        }

        // ── Internals ─────────────────────────────────────────────────────────

        /// <summary>Owned echo count (EchoService if live, else the clamped GameState count).</summary>
        private static int OwnedCount(GameState s)
        {
            return EchoService.Instance != null ? EchoService.Instance.EchoCount : Math.Max(1, s.EchoCount);
        }

        /// <summary>The raw stored token for an index (or the read-side default token).</summary>
        private static string RawToken(int echoIndex)
        {
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            string csv = s != null ? s.EchoLanes : null;
            if (string.IsNullOrEmpty(csv))
                return echoIndex == 0 ? (LaneHarvest + ":1") : LaneIdle;
            var parts = csv.Split(',');
            if (echoIndex < 0 || echoIndex >= parts.Length)
                return echoIndex == 0 ? (LaneHarvest + ":1") : LaneIdle;
            return parts[echoIndex];
        }

        /// <summary>Rebuild the full index-aligned token array from the current read-side values.</summary>
        private static string[] BuildTokens(int count)
        {
            var tokens = new string[count];
            for (int i = 0; i < count; i++)
                tokens[i] = BuildToken(LaneOf(i), LevelOf(i));
            return tokens;
        }

        /// <summary>Compose a "lane:level" token (idle carries no level suffix).</summary>
        private static string BuildToken(string lane, int level)
        {
            string norm = NormalizeLane(lane);
            if (norm == LaneIdle) return LaneIdle;
            return norm + ":" + ClampLevel(level);
        }

        /// <summary>The bare functional lane of a raw token (splits off ":level", normalizes).</summary>
        private static string ParseLane(string token, int echoIndex)
        {
            if (string.IsNullOrEmpty(token))
                return echoIndex == 0 ? LaneHarvest : LaneIdle;
            int colon = token.IndexOf(':');
            string lanePart = colon < 0 ? token : token.Substring(0, colon);
            return NormalizeLane(lanePart);
        }

        /// <summary>The level suffix of a raw token; a bare token (no ":") reads level 1.</summary>
        private static int ParseLevel(string token)
        {
            if (string.IsNullOrEmpty(token)) return 1;
            int colon = token.IndexOf(':');
            if (colon < 0 || colon + 1 >= token.Length) return 1;
            string lvPart = token.Substring(colon + 1).Trim();
            if (int.TryParse(lvPart, out int lv) && lv >= 1) return lv;
            return 1;
        }

        /// <summary>Clamp a level to [1, EchoBalanceCatalog.MaxLevel].</summary>
        private static int ClampLevel(int level)
        {
            int max = EchoBalanceCatalog.MaxLevel;
            if (max < 1) max = 1;
            if (level < 1) return 1;
            if (level > max) return max;
            return level;
        }

        /// <summary>Normalize a lane id to a canonical functional token (legacy wood/iron/food -> Harvest).</summary>
        private static string NormalizeLane(string lane)
        {
            if (string.IsNullOrEmpty(lane)) return LaneIdle;
            switch (lane.Trim().ToLowerInvariant())
            {
                case LaneHarvest:     return LaneHarvest;
                case LaneCrafting:    return LaneCrafting;
                case LaneDefense:     return LaneDefense;
                case LaneExploration: return LaneExploration;
                // Legacy resource lanes map forward to Harvest (additive-safe, default-on-read).
                case LaneWood:
                case LaneIron:
                case LaneFood:
                    FlowTrace.Once("Echo", "legacy-lane-forward",
                        "EchoAssignments: legacy resource lane token (wood/iron/food) read forward to the functional Harvest lane (level defaults to 1). Backward-compatible, no migrator.");
                    return LaneHarvest;
                case LaneIdle:        return LaneIdle;
                default:              return LaneIdle;
            }
        }
    }
}
