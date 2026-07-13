// =============================================================================
// EchoAssignments -- the WO-658 gather-lane assignment SEAM (hosted by WO-681).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-658 specs per-echo assignment slots (drag/pick -> resource lane). Its picker
// UI was NOT yet landed, so the WO-681 Echo card hosts the picker (ONE surface,
// never two) and THIS static seam owns the storage half: a per-echo lane keyed by
// echo index, persisted in GameState.EchoLanes as a CSV ("wood,iron,idle").
//
// SCOPE (deliberate, per WO-681 "what NOT to touch"): this seam only STORES and
// REPORTS assignments. It does NOT split EchoService's accrual rate or the Dump
// mix -- those are WO-658's rate-split half and land there, consuming this same
// field. Until then the pooled silo behaves exactly as before; the card's STATE
// line reads this seam for the player-facing "what is this Echo doing" answer.
//
// Lane ids: "wood" / "iron" / "food" / "idle". Index 0 (the starter Echo)
// defaults to "wood" (ECHO_WORKFORCE_SPEC: start 1 Echo auto-assigned); an index
// beyond the stored CSV reads "idle" -- a newly unlocked Echo waits for the
// player's word (the WO-681 ask verb).
// =============================================================================
using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Static storage seam for per-Echo gather-lane assignments (WO-658 storage half,
    /// hosted by the WO-681 Echo card). Reads/writes <see cref="GameState.EchoLanes"/>;
    /// raises <see cref="Changed"/> after an assignment so the card + HUD refresh.
    /// </summary>
    public static class EchoAssignments
    {
        public const string LaneWood = "wood";
        public const string LaneIron = "iron";
        public const string LaneFood = "food";
        public const string LaneIdle = "idle";

        /// <summary>The assignable gather lanes, in display order (idle is a state, not a pick).</summary>
        public static readonly string[] Lanes = { LaneWood, LaneIron, LaneFood };

        /// <summary>Raised after any lane assignment changes (the card + HUD listen).</summary>
        public static event Action Changed;

        /// <summary>
        /// The lane assigned to the Echo at <paramref name="echoIndex"/>. Absent state /
        /// out-of-range index reads as the safe defaults: index 0 = "wood" (the starter
        /// Echo auto-assignment), any later index = "idle" (waiting for the player's word).
        /// </summary>
        public static string LaneOf(int echoIndex)
        {
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            string csv = s != null ? s.EchoLanes : null;
            if (string.IsNullOrEmpty(csv))
                return echoIndex == 0 ? LaneWood : LaneIdle;
            var parts = csv.Split(',');
            if (echoIndex < 0 || echoIndex >= parts.Length)
                return echoIndex == 0 ? LaneWood : LaneIdle;
            string lane = Normalize(parts[echoIndex]);
            return lane;
        }

        /// <summary>
        /// Assign the Echo at <paramref name="echoIndex"/> to <paramref name="lane"/>
        /// ("wood"/"iron"/"food"/"idle"), persist via GameStateService.Save(), and raise
        /// <see cref="Changed"/>. Returns false (logged, never silent) when state is
        /// absent or the inputs are out of range. [Flow:Echo] step-in/step-out.
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

            string norm = Normalize(lane);
            int count = EchoService.Instance != null ? EchoService.Instance.EchoCount : Math.Max(1, s.EchoCount);
            if (echoIndex < 0 || echoIndex >= count)
            {
                FlowTrace.Warn("Echo", $"AssignLane: echo index {echoIndex} out of range (owned {count}) -- ignored.");
                return false;
            }

            // Rebuild the CSV with the slot updated; missing trailing slots fill with
            // their read-side defaults so the stored shape is always index-aligned.
            var lanes = new string[count];
            for (int i = 0; i < count; i++) lanes[i] = LaneOf(i);
            string before = lanes[echoIndex];
            lanes[echoIndex] = norm;
            s.EchoLanes = string.Join(",", lanes);
            gs.Save();

            FlowTrace.Step("Echo",
                $"AssignLane: echo {echoIndex} '{before}' -> '{norm}' (lanes now [{s.EchoLanes}]). " +
                "Storage seam only -- WO-658 rate-split consumes this on land.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>ASCII display label for a lane id ("wood" -> "Wood").</summary>
        public static string LabelFor(string lane)
        {
            switch (Normalize(lane))
            {
                case LaneWood: return "Wood";
                case LaneIron: return "Iron";
                case LaneFood: return "Food";
                default:       return "Idle";
            }
        }

        private static string Normalize(string lane)
        {
            if (string.IsNullOrEmpty(lane)) return LaneIdle;
            switch (lane.Trim().ToLowerInvariant())
            {
                case LaneWood: return LaneWood;
                case LaneIron: return LaneIron;
                case LaneFood: return LaneFood;
                default:       return LaneIdle;
            }
        }
    }
}
