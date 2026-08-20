// =============================================================================
// DungeonMovementOwnerRegression — who owns the dungeon hero's transform, and what
// the movement basis is allowed to be read against.
// Markers: DUNGEON_MOVEMENT_OWNER_OK / DUNGEON_MOVEMENT_OWNER_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Dungeons).
//
// ── THE DEFECT THIS PINS (owner F8, 2026-08-20: "healers cottage movement broken") ──
// Captured in logs/device/enemy-color.log, pid ( 6783), 08-20 14:10:03..14:10:12:
//
//   [Flow:HeroOwner] scene='Dungeon_HealersCottage' owner=FOREIGN-CC
//     ownerCC=LIVE(foreign mover owns transform) ownerAgent=disabled
//     scriptedMove=ZEROED(dungeon neutralize ON) velSelf=0.00 velRoot=0.00
//     rootYaw=174.1 basis=Camera.main(flattened) basisYaw=174.1 mainCamYaw=174.1
//
//   [Flow:DungeonMover] planarVel=4.20 ... yaw=<X> ... camYaw=<X>   (X identical, every sample)
//
// TWO facts live in those lines, and only one of them is a bug:
//
//   1. owner=FOREIGN-CC is CORRECT and must stay correct. DungeonHero (a
//      CharacterController) is the INTENDED sole mover in a dungeon; the injected village
//      HeroLocomotion is deliberately neutralised but kept ENABLED for type-resolution and
//      enemy targeting (DungeonController.EnsureSingleDungeonMover). A "fix" that hands the
//      transform back to HeroLocomotion re-creates the two-mover fight this project already
//      solved. So this suite asserts FOREIGN-CC is the expected state, not the failure.
//
//   2. basisYaw == rootYaw == camYaw, every single sample, IS the bug. The over-the-shoulder
//      rig parents its follow pivot TO THE HERO at identity local yaw (DungeonCameraRig.
//      EnsurePivot), so the camera's yaw IS the hero's yaw. DungeonHero read its stick
//      against that camera and then turned the hero to the result — a closed positive
//      feedback loop. The capture shows what that feels like: nine 1 Hz samples at top speed
//      (planarVel=4.20) with the yaw sweeping 90 -> 206.7 -> 120.1 -> 291.9 -> 79.9 -> 211.1
//      -> 142.3 -> 129.7 -> 323.6 and a NET travel of 2.24 m out of ~30 m of integrated path.
//      The Keeper ran in circles. That is "movement broken".
//
// A third defect from the same capture is pinned here too: an armed tap-to-move target used
// to be abandoned ONLY on arrival, so an unreachable one ran the Keeper into a wall forever —
// 570 consecutive 1 Hz lines (08-19 19:51:29 -> 20:01:06, pid 6863) reading
// `planarVel=4.20 ... tapTarget=True pos=(-22.37, 0.08, -7.15)`: nine and a half minutes of
// full throttle with the position frozen to the centimetre.
//
// ── BOTH DIRECTIONS ──
// Every case here asserts the KNOWN-BAD state fails AND the correct state passes. Case 2 in
// particular runs the real closed loop numerically: the live-basis model must be caught by
// the oracle, and the latched-basis model must sail through it. An oracle that only ever
// reports OK proves nothing, so it is exercised against the bug it was written for.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Dungeons;

namespace DeNelle.Editor
{
    /// <summary>
    /// Pins the dungeon transform-ownership contract and the movement-basis rule.
    /// Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class DungeonMovementOwnerRegression
    {
        // Simulation knobs mirror DungeonHero's shipped tuning so the oracle is exercised
        // against the real numbers, not a toy.
        private const float SimMoveSpeed = 4.2f;    // DungeonHero._moveSpeed
        private const float SimTurnSpeed = 720f;    // DungeonHero._turnSpeed (deg/sec)
        private const float SimDt = 1f / 60f;
        private const float SimSeconds = 5f;
        private const float SimStickAngle = 45f;    // a perfectly ordinary diagonal push

        /// <summary>
        /// The travel ratio (net displacement / integrated path) below which a run counts as
        /// "commanded speed but went nowhere". The captured failure sat at ~0.075
        /// (2.24 m of travel against ~30 m of path); a straight walk sits at 1.0.
        /// </summary>
        private const float StallRatio = 0.25f;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DUNGEON MOVEMENT OWNER (2026-08-20 healers-cottage capture) ---");

            Case(failures, "ownership-rule", () => Case1_OwnershipRule(failures, log));
            Case(failures, "basis-loop", () => Case2_BasisFeedbackLoop(failures, log));
            Case(failures, "basis-seam", () => Case3_BasisSeamExists(failures, log));
            Case(failures, "tap-watchdog", () => Case4_TapWatchdog(failures, log));

            if (failures.Count > 0)
            {
                reason = "DUNGEON_MOVEMENT_OWNER_FAIL: " + string.Join(" | ", failures)
                         + "\n" + log;
                return false;
            }

            reason = "DUNGEON_MOVEMENT_OWNER_OK - 4/4 cases\n" + log;
            return true;
        }

        // ── Case 1: the ownership rule itself ────────────────────────────────────────────
        // DungeonHero.ShouldLatchBasis is the pure statement of "when may the basis be
        // re-read every frame". Assert the truth table in FULL, so neither a hard-coded
        // `true` nor a hard-coded `false` can satisfy it.
        private static void Case1_OwnershipRule(List<string> failures, StringBuilder log)
        {
            // The bug state: camera yaw slaved to the hero AND the player is steering.
            // The basis MUST be latched here — this is the exact condition the capture was in.
            if (!DungeonHero.ShouldLatchBasis(basisFollowsHeroYaw: true, steeringInputHeld: true))
                failures.Add("ShouldLatchBasis(true,true) returned FALSE — the over-the-shoulder "
                           + "rig's camera yaw IS the hero yaw, so a live basis steers against "
                           + "itself and the Keeper circles (captured 08-20 14:10:03..:12).");

            // FPV / iso: the camera is NOT slaved to the hero, so there is no loop to break and
            // the view must be tracked live. Latching here would feel like tank controls.
            if (DungeonHero.ShouldLatchBasis(basisFollowsHeroYaw: false, steeringInputHeld: true))
                failures.Add("ShouldLatchBasis(false,true) returned TRUE — with an independent "
                           + "look layer (FPV) or a fixed world yaw (iso) the basis must be read "
                           + "live; latching it would freeze the view out of the controls.");

            // Nothing held: nothing to latch, and the next hold must read the CURRENT view.
            if (DungeonHero.ShouldLatchBasis(basisFollowsHeroYaw: true, steeringInputHeld: false))
                failures.Add("ShouldLatchBasis(true,false) returned TRUE — a latch that survives "
                           + "the release makes the next push steer against a stale view.");

            // ⛔ THE TWO-SPACE INDENT ON EVERY SUB-CASE LINE IS LOAD-BEARING, not cosmetic.
            // DataRegression.CountOracleTagLines derives the suite DENOMINATOR by counting log
            // lines that START with '['. A bracketed sub-case at column 0 therefore reads as
            // another whole registered suite and inflates the total - which is exactly how this
            // file tripped [suite-count] ("SUITE VANISHED FROM THE DENOMINATOR") on 2026-08-20
            // by adding 4 phantom suites. Indent, or emit one line.
            log.AppendLine("  [ownership-rule] latch iff (camera yaw follows hero) AND (steering held) - "
                         + "all three off-states rejected.");
        }

        // ── Case 2: the feedback loop, run for real ──────────────────────────────────────
        // The point of this case is that the oracle is proven to CATCH the bug before it is
        // used to bless the fix. Same simulator, same stick, one difference: whether the basis
        // is re-read from the (hero-slaved) camera every frame, or latched at the push.
        private static void Case2_BasisFeedbackLoop(List<string> failures, StringBuilder log)
        {
            float liveRatio = SimulateTravelRatio(latchBasis: false);
            float latchedRatio = SimulateTravelRatio(latchBasis: true);

            // KNOWN-BAD must fail: if this ever passes, the oracle has gone blind and every
            // other assertion in this suite is worthless.
            if (liveRatio >= StallRatio)
                failures.Add($"the KNOWN-BAD live-basis model was NOT caught (travel ratio "
                           + $"{liveRatio:F3} >= {StallRatio:F2}). The oracle is blind: it would "
                           + "have blessed the very state the 08-20 capture recorded "
                           + "(2.24 m travelled against ~30 m of path).");

            // CORRECT must pass: a latched basis walks a straight line.
            if (latchedRatio < StallRatio)
                failures.Add($"the latched-basis model did not travel (ratio {latchedRatio:F3} < "
                           + $"{StallRatio:F2}) — with a fixed world heading the Keeper must walk "
                           + "a straight line; something re-introduced the loop.");

            log.AppendLine($"  [basis-loop] live basis ratio={liveRatio:F3} (must stall, < {StallRatio:F2}) "
                         + $"vs latched ratio={latchedRatio:F3} (must travel). "
                         + $"stick={SimStickAngle:0}deg speed={SimMoveSpeed:0.0} turn={SimTurnSpeed:0}deg/s.");
        }

        /// <summary>
        /// Closed-loop model of the dungeon control stack: basis -> heading -> FaceHeading turns
        /// the hero -> (over-the-shoulder) the camera copies that yaw -> basis. Returns net
        /// displacement / integrated path, which is 1.0 for a straight walk and near 0 for a spin.
        /// </summary>
        private static float SimulateTravelRatio(bool latchBasis)
        {
            int steps = Mathf.RoundToInt(SimSeconds / SimDt);
            float heroYaw = 0f;
            float latchedYaw = heroYaw;     // the basis captured at the moment the stick leaves rest
            Vector2 pos = Vector2.zero;
            float path = 0f;

            for (int i = 0; i < steps; i++)
            {
                // The camera yaw IS the hero yaw in over-the-shoulder (EnsurePivot parents the
                // follow pivot to the hero at identity local rotation).
                float cameraYaw = heroYaw;
                float basisYaw = latchBasis ? latchedYaw : cameraYaw;

                float headingYaw = basisYaw + SimStickAngle;
                float rad = headingYaw * Mathf.Deg2Rad;
                Vector2 step = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (SimMoveSpeed * SimDt);
                pos += step;
                path += step.magnitude;

                // FaceHeading: rotate toward the heading, capped by the turn rate.
                float delta = Mathf.DeltaAngle(heroYaw, headingYaw);
                float turn = Mathf.Clamp(delta, -SimTurnSpeed * SimDt, SimTurnSpeed * SimDt);
                heroYaw += turn;
            }

            return path <= 0.0001f ? 0f : pos.magnitude / path;
        }

        // ── Case 3: the seam that carries the fact ───────────────────────────────────────
        private static void Case3_BasisSeamExists(List<string> failures, StringBuilder log)
        {
            // The camera rig must PUBLISH whether its yaw is slaved to the hero. Without this
            // the mover cannot tell the loop-forming framing from the safe ones, and the only
            // choices left are "always latch" (breaks FPV) or "never latch" (the bug).
            var prop = typeof(DungeonCameraRig).GetProperty(
                "BasisFollowsHeroYaw", BindingFlags.Public | BindingFlags.Static);
            if (prop == null || prop.PropertyType != typeof(bool))
                failures.Add("DungeonCameraRig.BasisFollowsHeroYaw (public static bool) is missing — "
                           + "nothing tells DungeonHero that the over-the-shoulder pivot makes the "
                           + "camera yaw the hero's own yaw.");
            else if (prop.GetValue(null) is bool live && live)
                failures.Add("DungeonCameraRig.BasisFollowsHeroYaw is TRUE with no dungeon loaded — "
                           + "the flag leaked past teardown and would latch the TOWN hero's basis.");

            // And the mover must actually route its basis through the latch.
            var resolve = typeof(DungeonHero).GetMethod(
                "ResolveBasisYaw", BindingFlags.NonPublic | BindingFlags.Instance);
            if (resolve == null)
                failures.Add("DungeonHero.ResolveBasisYaw is missing — CameraRelative is reading the "
                           + "camera transform directly again, which is the 08-20 spin.");

            log.AppendLine("  [basis-seam] rig publishes BasisFollowsHeroYaw (false at rest) and the "
                         + "mover resolves its basis through ResolveBasisYaw.");
        }

        // ── Case 4: tap-to-move can no longer jam ────────────────────────────────────────
        private static void Case4_TapWatchdog(List<string> failures, StringBuilder log)
        {
            var t = typeof(DungeonHero);
            const BindingFlags Inst = BindingFlags.NonPublic | BindingFlags.Instance;
            const BindingFlags Stat = BindingFlags.NonPublic | BindingFlags.Static;

            if (t.GetMethod("TickTapWatchdog", Inst) == null)
                failures.Add("DungeonHero.TickTapWatchdog is missing — an armed tap target is "
                           + "abandoned only on ARRIVAL again, which is the captured 570-second "
                           + "full-throttle jam at pos=(-22.37, 0.08, -7.15) (08-19 19:51->20:01).");

            if (t.GetMethod("AbandonTapTarget", Inst) == null)
                failures.Add("DungeonHero.AbandonTapTarget is missing — there is no traced give-up "
                           + "path, so a blocked walk would end silently or not at all.");

            float stall = ReadConst(t, "TapStallSeconds", Stat);
            if (float.IsNaN(stall))
                failures.Add("TapStallSeconds is missing from DungeonHero — the watchdog has no "
                           + "stall threshold, so a blocked tap-walk never gives up.");
            else if (stall <= 0f || stall > 5f)
                failures.Add($"TapStallSeconds={stall} is outside (0, 5]. Zero/negative never fires; "
                           + "too long and the player spends the jam wondering what is wrong.");

            float cap = ReadConst(t, "TapMaxSeconds", Stat);
            if (float.IsNaN(cap))
                failures.Add("TapMaxSeconds is missing from DungeonHero — a tap-walk that creeps "
                           + "just fast enough to dodge the stall check would still run forever.");
            else if (float.IsNaN(stall) || cap <= stall || cap > 120f)
                failures.Add($"TapMaxSeconds={cap} must exceed TapStallSeconds={stall} and stay "
                           + "well under the captured 570-second jam.");

            float normalY = ReadConst(t, "TapFloorNormalY", Stat);
            if (float.IsNaN(normalY) || normalY <= 0f || normalY >= 1f)
                failures.Add($"TapFloorNormalY={normalY} must be a strict 0..1 upness threshold — it "
                           + "is what stops a tap on a WALL arming an unreachable destination "
                           + "(captured 08-20 14:10:09: 'armed walk to (-26.59, 1.44, -2.00)' with "
                           + "the floor at y=0.08).");

            log.AppendLine($"  [tap-watchdog] stall={stall:0.00}s cap={cap:0}s floorNormalY={normalY:0.00}; "
                         + "watchdog + traced give-up present.");
        }

        private static float ReadConst(Type t, string name, BindingFlags flags)
        {
            var f = t.GetField(name, flags);
            if (f == null || f.FieldType != typeof(float)) return float.NaN;
            try { return (float)f.GetRawConstantValue(); }
            catch { return float.NaN; }
        }

        /// <summary>Runs one case, converting a throw into a failure rather than a dead suite.</summary>
        private static void Case(List<string> failures, string id, Action body)
        {
            try { body(); }
            catch (Exception e) { failures.Add($"{id} THREW: {e.GetType().Name}: {e.Message}"); }
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
