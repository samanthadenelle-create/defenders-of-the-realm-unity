// =============================================================================
// EchoAssignments -- the per-echo assignment SEAM (WO-658/681 storage, WO-738
// functional lanes + level, WO-830 per-echo harvest RESOURCE).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owns the STORAGE half of per-echo agency: a per-echo assignment + level keyed by
// echo index, persisted in GameState.EchoLanes as a CSV of tokens.
//
// TOKEN GRAMMAR (v33 lane:level, EXTENDED additively by WO-830 and again by WO-811
// -- NO schema bump either time, read-migrated per the SaveMigrator additive law;
// documented in SaveSchema.cs too):
//   idle                       -> Idle (carries no level, no resource)
//   <lane>:<level>             -> functional lane token harvest/crafting/defense/
//                                 exploration. For "harvest" the RESOURCE defaults
//                                 on read to the echo's AFFINITY (EchoRosterCatalog).
//   <resource>:<level>         -> the WO-830 primary form: a HARVEST assignment with
//                                 an explicit resource -- wood/iron/food/gold/crystals.
//                                 Lane reads as Harvest; the resource is preserved.
//   any bare token (no :level) -> level 1 (default-on-read).
// WO-1108 REPAIR READ-MIGRATION: "repair" was an assignable task (WO-811) and is not any
// more -- repair is now PASSIVE across every owned Echo. A stored "repair:N" is therefore
// READ-MIGRATED to the HARVEST lane (level preserved), where the resource resolves to that
// Echo's AFFINITY. It must never fall to the unknown-token Idle default: idle would silently
// zero that Echo's yield. Still NO schema bump -- grammar-only, the v33/WO-830 precedent.
// (An OLDER build meeting "repair:N" still reads Idle via its own unknown-token default --
// no crash, no corruption; the newer build migrates it on the next load.)
// BACKWARD-COMPATIBLE READ (additive, default-on-read, NO migrator):
//   - pre-v33 legacy "wood"/"iron"/"food" -> Harvest at that resource, level 1
//     (the pre-733 resource vocabulary is now FIRST-CLASS again -- WO-830 note:
//     the grammar started as resource tokens before v33 and returns to them);
//   - v33 "harvest:N" -> Harvest at the echo's affinity resource, level N;
//   - unknown tokens -> Idle.
// WRITE PATH: the resource picker writes the explicit <resource>:<level> form;
// generic Assign(LaneHarvest) keeps writing "harvest:<level>" (reads as affinity).
//
// SCOPE: this seam STORES + REPORTS. The consumers are live (WO-738 shipped them):
// EchoBonusCalculator (rate math + dump weights + readouts) and the card/roster VMs
// read LaneOf/LevelOf/ResourceTokenOf; EchoService recomputes off Changed.
// =============================================================================
using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Static storage seam for per-Echo assignment (lane + harvest resource + level).
    /// Reads/writes <see cref="GameState.EchoLanes"/> (a CSV of tokens -- see the file
    /// header grammar); raises <see cref="Changed"/> after any write so the card + HUD refresh.
    /// </summary>
    public static class EchoAssignments
    {
        // ── Functional lane tokens (WO-738) ──────────────────────────────────
        public const string LaneIdle        = "idle";
        public const string LaneHarvest     = "harvest";
        public const string LaneCrafting    = "crafting";
        public const string LaneDefense     = "defense";
        public const string LaneExploration = "exploration";
        /// <summary>WO-811 legacy: the REPAIR task token ("repair:&lt;level&gt;"). RETIRED as an
        /// ASSIGNMENT by WO-1108 (repair is passive across the whole roster) -- the constant
        /// survives ONLY so stored saves keep read-migrating (NormalizeToken maps it to the
        /// Harvest lane, where the resource resolves to the Echo's affinity). Never write it.</summary>
        public const string LaneRepair      = "repair";

        // ── Harvest RESOURCE tokens (WO-830 -- first-class again). wood/iron/food
        //    date from the pre-v33 grammar (kept read-compatible ever since); gold +
        //    crystals are the WO-830 additions. All five read as the Harvest lane
        //    with the resource preserved. ──
        public const string ResWood     = "wood";
        public const string ResIron     = "iron";
        public const string ResFood     = "food";
        public const string ResGold     = "gold";
        public const string ResCrystals = "crystals";

        // Back-compat aliases (pre-830 code referenced these names).
        public const string LaneWood = ResWood;
        public const string LaneIron = ResIron;
        public const string LaneFood = ResFood;

        /// <summary>The assignable functional lanes, in display order (Idle is a state, not a pick).
        /// WO-1108 REMOVED Repair (added by WO-811): it is no longer a task an Echo can be put
        /// on -- every owned Echo mends passively.</summary>
        public static readonly string[] Lanes = { LaneHarvest, LaneCrafting, LaneDefense, LaneExploration };

        /// <summary>The lanes the picker actually OFFERS: HARVEST ONLY (WO-830 ruling --
        /// the card is a per-Echo RESOURCE picker; the dead Crafting chip is removed
        /// entirely per the Sec.3e owner-confirmed default). Defense + Exploration remain
        /// in <see cref="Lanes"/> (constants + LabelFor + normalization intact) so any
        /// already-stored token still reads back, but none of the three is offered
        /// (unlock undesigned -- owner ruling 2026-07-24; no stub/teaser rows).
        /// WO-1108 NOTE: the WO-811 "Repair structures" chip that used to ride beside the five
        /// resource chips is GONE -- repair is passive across the whole roster, so there is
        /// nothing to pick. This list stays the WO-830 resource-picker contract unchanged.</summary>
        public static readonly string[] PickableLanes = { LaneHarvest };

        /// <summary>The five harvest resources the card's RESOURCE PICKER offers (WO-830),
        /// in display order. Identity is carried by icon + TEXT, never hue (colorblind law).</summary>
        public static readonly string[] PickableResources = { ResWood, ResIron, ResFood, ResGold, ResCrystals };

        /// <summary>Raised after any assignment/level change (the card + HUD listen).</summary>
        public static event Action Changed;

        // ── Read API ──────────────────────────────────────────────────────────

        /// <summary>
        /// The functional lane assigned to the Echo at <paramref name="echoIndex"/> (bare lane
        /// token, no ":level"). Resource tokens (wood/iron/food/gold/crystals) read as Harvest.
        /// Absent state / out-of-range index reads the safe defaults: index 0 = Harvest (the
        /// starter Echo auto-assignment), any later index = Idle.
        /// </summary>
        public static string LaneOf(int echoIndex)
        {
            return LaneFromToken(CanonicalPart(RawToken(echoIndex), echoIndex));
        }

        /// <summary>
        /// WO-830: the harvest RESOURCE token assigned to the Echo at <paramref name="echoIndex"/>
        /// ("wood".."crystals"), or "" when the echo is not on the Harvest lane. A generic
        /// "harvest" token (v33 saves) defaults on read to the echo's AFFINITY resource
        /// (EchoRosterCatalog) -- the read-migration that keeps old saves matched.
        /// </summary>
        public static string ResourceTokenOf(int echoIndex)
        {
            string part = CanonicalPart(RawToken(echoIndex), echoIndex);
            if (IsResourceToken(part)) return part;
            if (part == LaneHarvest)
            {
                var entry = EchoRosterCatalog.ByIndex(echoIndex);
                return entry != null ? EchoRosterCatalog.TargetToken(entry.Affinity)
                                     : EchoRosterCatalog.TargetToken(HarvestTarget.Wood);
            }
            return "";   // idle / crafting / defense / exploration -- no harvest resource
        }

        /// <summary>WO-830: the typed harvest target of an echo. False when not harvesting.</summary>
        public static bool TryTargetOf(int echoIndex, out HarvestTarget target)
        {
            return EchoRosterCatalog.TryTargetFromToken(ResourceTokenOf(echoIndex), out target);
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

        // ── Write API (rebuilds the CSV of tokens; resources preserved) ───────

        /// <summary>
        /// Assign the Echo at <paramref name="echoIndex"/> to <paramref name="lane"/> -- a
        /// functional lane id, a harvest RESOURCE token (WO-830 picker), or idle. PRESERVES
        /// the echo's current level. Persists via GameStateService.Save() and raises
        /// <see cref="Changed"/>. Returns false (logged, never silent) when state is absent
        /// or the index is out of range. [Flow:Echo].
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
            tokens[echoIndex] = BuildToken(NormalizeToken(lane), level);
            s.EchoLanes = string.Join(",", tokens);
            gs.Save();

            FlowTrace.Step("Echo",
                $"AssignLane: echo {echoIndex} '{before}' -> '{tokens[echoIndex]}' (lanes now [{s.EchoLanes}]).");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// WO-830: assign the Echo at <paramref name="echoIndex"/> to HARVEST a specific
        /// resource (the card's resource-picker verb). <paramref name="resourceToken"/> must be
        /// one of <see cref="PickableResources"/>; anything else logs + no-ops (returns false).
        /// Writes the explicit <c>&lt;resource&gt;:&lt;level&gt;</c> token form.
        /// </summary>
        public static bool AssignHarvest(int echoIndex, string resourceToken)
        {
            string norm = (resourceToken ?? "").Trim().ToLowerInvariant();
            if (!IsResourceToken(norm))
            {
                FlowTrace.Warn("Echo", $"AssignHarvest(echo={echoIndex}, resource='{resourceToken}') -- not a harvest resource token; ignored.");
                return false;
            }
            return Assign(echoIndex, norm);
        }

        /// <summary>
        /// RETIRED by WO-1108 -- repair is no longer an assignable task. Every owned Echo
        /// mends PASSIVELY (EchoBonusCalculator.RepairFractionsPerSecond sums the whole
        /// roster), so there is nothing left to assign. Kept as a LOUD refusal rather than
        /// deleted so any surviving caller (or a re-added picker chip) fails visibly instead
        /// of silently writing a token the read path now migrates away: ALWAYS returns false
        /// and NEVER mutates state. Stored <c>repair:N</c> tokens still read (see
        /// <c>NormalizeToken</c> -- they migrate to the Echo's affinity harvest resource).
        /// </summary>
        public static bool AssignRepair(int echoIndex)
        {
            FlowTrace.Warn("Echo",
                $"AssignRepair(echo={echoIndex}) -- RETIRED (WO-1108): repair is passive across every " +
                "owned Echo and is not assignable. Ignored; assignment unchanged.");
            return false;
        }

        /// <summary>
        /// Set the LEVEL (clamped to [1, <see cref="EchoBalanceCatalog.MaxLevel"/>]) of the Echo at
        /// <paramref name="echoIndex"/>, keeping its current assignment (lane AND resource).
        /// Persists + raises <see cref="Changed"/>. Returns false (logged) when state is absent
        /// or the index is out of range. [Flow:Echo].
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
            var tokens = BuildTokens(count);
            string before = tokens[echoIndex];
            string part = CanonicalPart(before, echoIndex);
            tokens[echoIndex] = BuildToken(part, clamped);
            s.EchoLanes = string.Join(",", tokens);
            gs.Save();

            FlowTrace.Step("Echo",
                $"SetLevel: echo {echoIndex} '{part}' level -> {clamped} (was token '{before}'; lanes now [{s.EchoLanes}]).");
            Changed?.Invoke();
            return true;
        }

        /// <summary>ASCII display label for a lane id ("harvest" -> "Harvest"; resource tokens
        /// label as "Harvest" -- use <see cref="ResourceLabelFor"/> for the resource word).</summary>
        public static string LabelFor(string lane)
        {
            switch (LaneFromToken(NormalizeToken(lane)))
            {
                case LaneHarvest:     return "Harvest";
                case LaneCrafting:    return "Crafting";
                case LaneDefense:     return "Defense";
                case LaneExploration: return "Exploration";
                // WO-1108: "Repair" is unreachable here -- LabelFor normalizes first, and
                // NormalizeToken migrates "repair" to the Harvest lane. Repair is passive now.
                default:              return "Idle";
            }
        }

        /// <summary>ASCII display label for a harvest resource token ("wood" -> "Wood").
        /// Empty string for a non-resource token.</summary>
        public static string ResourceLabelFor(string resourceToken)
        {
            return EchoRosterCatalog.TryTargetFromToken(resourceToken, out var t)
                ? EchoRosterCatalog.TargetLabel(t)
                : "";
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

        /// <summary>Rebuild the full index-aligned token array from the current stored values,
        /// PRESERVING each echo's canonical part (resource tokens stay resource tokens).</summary>
        private static string[] BuildTokens(int count)
        {
            var tokens = new string[count];
            for (int i = 0; i < count; i++)
                tokens[i] = BuildToken(CanonicalPart(RawToken(i), i), LevelOf(i));
            return tokens;
        }

        /// <summary>Compose a token from a canonical part + level (idle carries no level suffix).</summary>
        private static string BuildToken(string part, int level)
        {
            if (string.IsNullOrEmpty(part) || part == LaneIdle) return LaneIdle;
            return part + ":" + ClampLevel(level);
        }

        /// <summary>The canonical stored PART of a raw token: splits off ":level", normalizes,
        /// and applies the index-0 starter default when empty.</summary>
        private static string CanonicalPart(string token, int echoIndex)
        {
            if (string.IsNullOrEmpty(token))
                return echoIndex == 0 ? LaneHarvest : LaneIdle;
            int colon = token.IndexOf(':');
            string part = colon < 0 ? token : token.Substring(0, colon);
            return NormalizeToken(part);
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

        /// <summary>True when <paramref name="part"/> is one of the five harvest resource tokens.</summary>
        private static bool IsResourceToken(string part)
        {
            switch (part)
            {
                case ResWood:
                case ResIron:
                case ResFood:
                case ResGold:
                case ResCrystals:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Normalize any incoming part to a canonical STORAGE token. Resource tokens are
        /// PRESERVED (WO-830 -- they carry the assignment); lane tokens pass through; unknown -> idle.</summary>
        private static string NormalizeToken(string part)
        {
            if (string.IsNullOrEmpty(part)) return LaneIdle;
            switch (part.Trim().ToLowerInvariant())
            {
                case LaneHarvest:     return LaneHarvest;
                case LaneCrafting:    return LaneCrafting;
                case LaneDefense:     return LaneDefense;
                case LaneExploration: return LaneExploration;
                // WO-1108 READ-MIGRATION: repair stopped being an assignable task (it is now
                // PASSIVE across the whole roster -- EchoBonusCalculator.RepairFractionsPerSecond).
                // A stored "repair:N" therefore migrates to the HARVEST lane, where
                // ResourceTokenOf() resolves it to that Echo's AFFINITY resource. It must NEVER
                // fall through to the unknown-token default: idle would silently ZERO that
                // Echo's yield, which is the exact silent-loss WO-830's read-migration law
                // forbids. No schema bump -- grammar-only, the v33 precedent.
                case LaneRepair:      return LaneHarvest;
                case ResWood:         return ResWood;
                case ResIron:         return ResIron;
                case ResFood:         return ResFood;
                case ResGold:         return ResGold;
                case ResCrystals:     return ResCrystals;
                case LaneIdle:        return LaneIdle;
                default:              return LaneIdle;
            }
        }

        /// <summary>Map a canonical part to its FUNCTIONAL lane (resource tokens -> Harvest).</summary>
        private static string LaneFromToken(string part)
        {
            if (IsResourceToken(part))
            {
                FlowTrace.Once("Echo", "resource-token-read",
                    "EchoAssignments: harvest resource token read as the Harvest lane with the resource preserved (WO-830 grammar; pre-v33 wood/iron/food remain compatible).");
                return LaneHarvest;
            }
            switch (part)
            {
                case LaneHarvest:
                case LaneCrafting:
                case LaneDefense:
                case LaneExploration:
                    return part;   // WO-1108: no LaneRepair case -- NormalizeToken migrated it to Harvest
                default:
                    return LaneIdle;
            }
        }
    }
}
