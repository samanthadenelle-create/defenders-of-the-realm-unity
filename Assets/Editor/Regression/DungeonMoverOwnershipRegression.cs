// =============================================================================
// DungeonMoverOwnershipRegression [dungeon-mover-ownership]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village
// + DeNelle.Dungeons).
//
// WO-1016 / WO-968 (owner F8 seq 2312 "Everything is wrong check locomotion" +
// seq 2313 "No camera movement", 2026-08-10, Dungeon_HealersCottage).
//
// THE DEFECT THIS PINS, in one sentence: the dungeon had NO single owner of the hero
// transform, so the hero translated through the world while the animator was fed a dead
// velocity and the stick was interpreted in a frame of reference that did not exist.
// Three seams, one shape - and this suite asserts one thing about each:
//
//   (1) ONE MOVER OWNS THE TRANSFORM. Ownership is a per-frame CAPABILITY check
//       (HeroLocomotion.SelfMayWriteTransform), the exact inverse of DungeonHero's own
//       "my CharacterController is disabled, the arena owns the hero" guard. It is NOT a
//       static side-channel: the previous mechanism (DungeonController's scripted-move
//       stomp) was three shared statics and the owner's capture proves it lapsed
//       mid-session - [Flow:HeroLoco] vel=0.00 while the root moved on some frames, and
//       [Flow:HeroDrift] vel=(0.000,5.000) with live input on others.
//
//   (2) THE ANIMATOR'S SPEED SOURCE IS THE MOVER THAT MOVES IT.
//       HeroLocomotion.ResolveAnimatorFeed publishes the MEASURED root speed whenever a
//       foreign mover owns the rig, and DungeonHero.ShouldWriteSpeed yields the parameter
//       to ActorAnimator so there is exactly ONE Speed writer.
//
//   (3) A BASIS EXISTS. HeroLocomotion.ResolveBasisKind falls back to the camera that
//       ACTUALLY exists in the scene and reports MovementBasis.None rather than silently
//       resolving "forward" to world +Z. The fallback keys on the SmartMobileCamera
//       COMPONENT BEING ABSENT, never on its VALUE being zero - in town top-down framing
//       CameraYaw legitimately returns 0.
//
// WHAT THIS SUITE CANNOT PROVE, stated plainly (do not let a green tick here read as more
// than it is):
//   * that the walk cycle plays in a real dungeon - that needs a play session; the proof is
//     the [Flow:HeroOwner] / [Flow:DungeonMover] / [Flow:DungeonCam] lines in a capture.
//   * that the camera follows - the frozen-camera heal is a null-poll re-bind, and only a
//     capture can say whether it fired and whether the view then tracked.
//   * "it looks right now" - explicitly NOT proof of (1): a correct animator feed is also
//     exactly what two movers still fighting over one transform would look like.
//
// The pure predicates below are the SHIPPED decision functions, called by the live code -
// so these cases cover the real rule, not a copy of it. The source lints cover the wiring
// that has no headless seam.
//
// Markers: DUNGEON_MOVER_OWNERSHIP_OK / DUNGEON_MOVER_OWNERSHIP_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DungeonMoverOwnershipRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Dungeons;

namespace DeNelle.Editor.Regression
{
    public static class DungeonMoverOwnershipRegression
    {
        private const string LocoSrc   = "Assets/_Modules/Village/Hero/HeroLocomotion.cs";
        private const string HeroSrc   = "Assets/_Modules/Dungeons/DungeonHero.cs";
        private const string RigSrc    = "Assets/_Modules/Dungeons/DungeonCameraRig.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_MOVER_OWNERSHIP_OK - " + reason);
            else Debug.LogError("DUNGEON_MOVER_OWNERSHIP_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "one-owner",    () => Case1_OneOwner(failures));
                Case(failures, "speed-source", () => Case2_SpeedSource(failures));
                Case(failures, "basis-exists", () => Case3_BasisExists(failures));
                Case(failures, "wiring",       () => Case4_WiringLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "DUNGEON MOVER OWNERSHIP OK - exactly one component may write the hero " +
                         "transform (capability-gated, both directions); the animator's Speed is fed " +
                         "by whichever mover actually moved the root and has exactly one writer; and " +
                         "the movement basis falls back to the camera that exists, reporting None " +
                         "instead of a silent world-absolute identity.";
                return true;
            }
            reason = "dungeon-mover-ownership FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - ONE mover owns the transform
        // =====================================================================

        private static void Case1_OneOwner(List<string> failures)
        {
            // A live foreign CharacterController owns the rig -> HeroLocomotion writes NOTHING.
            if (HeroLocomotion.SelfMayWriteTransform(foreignOwnsTransform: true))
                failures.Add("[one-owner] HeroLocomotion.SelfMayWriteTransform(true) returned true - " +
                             "with a live CharacterController on the rig this component would still " +
                             "integrate the transform, which is the WO-968 S1 two-mover duel: two " +
                             "components writing one transform, and nothing in the log saying which.");

            // No foreign mover -> HeroLocomotion is the sole mover, unchanged town behaviour.
            if (!HeroLocomotion.SelfMayWriteTransform(foreignOwnsTransform: false))
                failures.Add("[one-owner] HeroLocomotion.SelfMayWriteTransform(false) returned false - " +
                             "with no foreign mover this component MUST be the mover, or the town/" +
                             "overworld hero cannot walk at all.");

            // The two rules must be strict complements: never both movers, never neither.
            foreach (bool foreign in new[] { true, false })
            {
                bool locoWrites = HeroLocomotion.SelfMayWriteTransform(foreign);
                if (locoWrites == foreign)
                    failures.Add("[one-owner] ownership is not exclusive for foreign=" + foreign +
                                 ": HeroLocomotion writes=" + locoWrites + " while the foreign mover " +
                                 "writes=" + foreign + ". Exactly one of the two must own the frame.");
            }
        }

        // =====================================================================
        //  Case 2 - the animator's speed source IS the mover that moves it
        // =====================================================================

        private static void Case2_SpeedSource(List<string> failures)
        {
            const float selfSpeed = 0f;         // dead by design while standing down
            const float rootSpeed = 4.2f;       // what the CharacterController actually did
            const float seamSpeed = 6f;

            // THE 2026-08-10 DEFECT, as one assertion: a foreign mover moved the root at 4.2 m/s
            // and the feed must be 4.2, not HeroLocomotion's dead 0.00.
            float fed = HeroLocomotion.ResolveAnimatorFeed(
                crossingSeam: false, seamSpeed: seamSpeed,
                foreignOwnsTransform: true, selfSpeed: selfSpeed, measuredRootSpeed: rootSpeed);
            if (!Mathf.Approximately(fed, rootSpeed))
                failures.Add("[speed-source] with a foreign mover owning the transform the animator was " +
                             "fed " + fed.ToString("0.00") + " instead of the MEASURED root speed " +
                             rootSpeed.ToString("0.00") + " - that is exactly WO-1016: the hero " +
                             "translates through the dungeon while the animator holds a single idle clip.");

            // Town: no foreign mover, so the feed is this component's own Velocity (unchanged).
            fed = HeroLocomotion.ResolveAnimatorFeed(
                crossingSeam: false, seamSpeed: seamSpeed,
                foreignOwnsTransform: false, selfSpeed: 3.3f, measuredRootSpeed: 99f);
            if (!Mathf.Approximately(fed, 3.3f))
                failures.Add("[speed-source] town feed regressed: expected the component's own Velocity " +
                             "(3.30) with no foreign mover, got " + fed.ToString("0.00") + ".");

            // The seam slide still wins (the crossing drives transform.position directly).
            fed = HeroLocomotion.ResolveAnimatorFeed(
                crossingSeam: true, seamSpeed: seamSpeed,
                foreignOwnsTransform: false, selfSpeed: 0f, measuredRootSpeed: 0f);
            if (!Mathf.Approximately(fed, seamSpeed))
                failures.Add("[speed-source] the seam-crossing feed no longer wins - the walk cycle " +
                             "would freeze mid-crossing.");

            // The stall predicate itself: moving + idle = stall; the three near neighbours are not.
            if (!HeroLocomotion.IsAnimationStalled(rootSpeed: 4.2f, animSpeed: 0.0f))
                failures.Add("[speed-source] IsAnimationStalled(4.2, 0.0) = false - the exact 2026-08-10 " +
                             "capture state (root moving, animator idle) is no longer detected.");
            if (HeroLocomotion.IsAnimationStalled(rootSpeed: 0.0f, animSpeed: 0.0f))
                failures.Add("[speed-source] standing still with an idle animator was flagged as a stall.");
            if (HeroLocomotion.IsAnimationStalled(rootSpeed: 4.2f, animSpeed: 4.2f))
                failures.Add("[speed-source] moving WITH a driven animator was flagged as a stall.");
            if (HeroLocomotion.IsAnimationStalled(rootSpeed: 4.2f, animSpeed: float.NaN))
                failures.Add("[speed-source] NaN animSpeed (= the controller declares no Speed param) was " +
                             "flagged as a stall - that is a different fault and must not be conflated.");

            // ── WO-1298: the SUPPRESSED feed (the owner's F8 seq 4362 gate glide) ────────────
            // Shape: InputSuppressed is raised by a dialogue/tutorial beat, no CharacterController
            // is present, and the root moves anyway. The old branch wrote a hard 0 into Speed every
            // frame and returned - the hero glided through the west gate in an idle pose.
            const float suppressedRunCap = 6f;   // HeroLocomotion.OverworldRunSpeed

            float suppressed = HeroLocomotion.ResolveSuppressedAnimatorFeed(14.49f, suppressedRunCap);
            if (HeroLocomotion.IsAnimationStalled(rootSpeed: 14.49f, animSpeed: suppressed))
                failures.Add("[suppressed-feed] the owner's captured state (velRoot=14.49 while input is " +
                             "suppressed) still feeds the animator " + suppressed.ToString("0.00") +
                             " - IsAnimationStalled is STILL true, i.e. the hero slides through the gate " +
                             "in an idle pose exactly as in F8 seq 4362 (WO-1298).");
            if (suppressed > suppressedRunCap + 0.001f)
                failures.Add("[suppressed-feed] the suppressed feed (" + suppressed.ToString("0.00") +
                             ") exceeds the run tier - a single large displacement would drive the blend " +
                             "tree past its authored top child.");

            // A moving-but-suppressed hero anywhere above the stall threshold must animate.
            if (HeroLocomotion.ResolveSuppressedAnimatorFeed(1.01f, suppressedRunCap) <
                HeroLocomotion.AnimStallAnimSpeed)
                failures.Add("[suppressed-feed] velRoot=1.01 m/s under suppression still feeds an idle " +
                             "animator - the acceptance bar for WO-1298 is that velRoot > 1 m/s can never " +
                             "coexist with animSpeed == 0.");

            // ...and the WO-377 contract is untouched: a genuinely STATIONARY suppressed hero must
            // still settle to a hard 0, or every story beat gains a twitching walk cycle.
            if (!Mathf.Approximately(HeroLocomotion.ResolveSuppressedAnimatorFeed(0f, suppressedRunCap), 0f))
                failures.Add("[suppressed-feed] a stationary suppressed hero is no longer fed 0 - the " +
                             "WO-377 dialogue hold has regressed.");
            if (!Mathf.Approximately(
                    HeroLocomotion.ResolveSuppressedAnimatorFeed(HeroLocomotion.AnimStallRootSpeed,
                                                                suppressedRunCap), 0f))
                failures.Add("[suppressed-feed] sub-threshold drift under suppression now drives the " +
                             "animator - noise would read as walking during a story beat.");

            // ONE Speed writer: DungeonHero yields whenever an ActorAnimator owns the parameter.
            if (DungeonHero.ShouldWriteSpeed(actorAnimatorPresent: true, animatorResolved: true, hasSpeedParam: true))
                failures.Add("[speed-source] DungeonHero still writes Speed while an ActorAnimator owns it - " +
                             "two components writing one Animator parameter is the WO-968 S2 shape.");
            if (!DungeonHero.ShouldWriteSpeed(actorAnimatorPresent: false, animatorResolved: true, hasSpeedParam: true))
                failures.Add("[speed-source] DungeonHero refuses to write Speed even with NO ActorAnimator " +
                             "present - a dungeon Keeper with no injected village rig would never animate.");
            if (DungeonHero.ShouldWriteSpeed(actorAnimatorPresent: false, animatorResolved: false, hasSpeedParam: true))
                failures.Add("[speed-source] DungeonHero would write Speed through a NULL/destroyed Animator " +
                             "handle.");
            if (DungeonHero.ShouldWriteSpeed(actorAnimatorPresent: false, animatorResolved: true, hasSpeedParam: false))
                failures.Add("[speed-source] DungeonHero would write a Speed parameter the live controller " +
                             "does not declare (that logs an error EVERY frame - the WO-163 param-spam trap).");
        }

        // =====================================================================
        //  Case 3 - a movement basis exists, and its absence is reported
        // =====================================================================

        private static void Case3_BasisExists(List<string> failures)
        {
            // Town: the SmartMobileCamera wins whenever it is PRESENT.
            if (HeroLocomotion.ResolveBasisKind(hasSmartCamera: true, hasUsableMainCamera: true)
                != HeroLocomotion.MovementBasis.SmartMobileCamera)
                failures.Add("[basis-exists] a present SmartMobileCamera no longer supplies the basis - " +
                             "town/overworld input would silently change frame of reference.");

            // Dungeon: no SmartMobileCamera exists in Dungeon_HealersCottage (zero references to its
            // script GUID in the scene, and nothing runtime-adds one), so the basis MUST come from
            // the camera that is actually there.
            if (HeroLocomotion.ResolveBasisKind(hasSmartCamera: false, hasUsableMainCamera: true)
                != HeroLocomotion.MovementBasis.MainCamera)
                failures.Add("[basis-exists] with no SmartMobileCamera the basis did not fall back to the " +
                             "scene's real camera - that is WO-968 S3: the stick silently reverts to " +
                             "world-absolute and 'forward' means world +Z regardless of the view.");

            // Neither: reported as None so it can FAIL LOUDLY, never a silent identity.
            if (HeroLocomotion.ResolveBasisKind(hasSmartCamera: false, hasUsableMainCamera: false)
                != HeroLocomotion.MovementBasis.None)
                failures.Add("[basis-exists] a total absence of basis sources does not resolve to " +
                             "MovementBasis.None - a missing basis must be nameable in the trace.");
        }

        // =====================================================================
        //  Case 4 - the wiring that has no headless seam, pinned at source
        // =====================================================================

        private static void Case4_WiringLint(List<string> failures)
        {
            string loco = StripComments(File.ReadAllText(LocoSrc));
            string hero = StripComments(File.ReadAllText(HeroSrc));
            string rig  = StripComments(File.ReadAllText(RigSrc));

            // (1) The ownership gate is actually CONSULTED in Update, not merely declared.
            if (!loco.Contains("ForeignMoverOwnsTransform()"))
                failures.Add("[wiring] " + LocoSrc + " no longer calls ForeignMoverOwnsTransform() - " +
                             "the per-frame ownership check is gone and the dungeon is back on the " +
                             "static side-channel that lapsed.");
            if (!Regex.IsMatch(loco, @"if\s*\(\s*foreignOwnsTransform\s*\)"))
                failures.Add("[wiring] " + LocoSrc + " no longer stands down on foreignOwnsTransform - " +
                             "HeroLocomotion would write the transform while a CharacterController owns it.");

            // (2) The animator feed goes through the tested decision function, not a raw Velocity.
            if (!loco.Contains("ResolveAnimatorFeed("))
                failures.Add("[wiring] " + LocoSrc + " no longer feeds ActorAnimator through " +
                             "ResolveAnimatorFeed - the mover-agnostic feed has been reverted and " +
                             "this suite's Case 2 would pass over dead code.");

            // (2b) WO-1298: the suppression branch feeds the animator through the tested function
            // rather than re-hardcoding a zero. Without this the Case-2 assertions above would pass
            // over dead code while the shipped branch still froze the rig mid-glide.
            if (!loco.Contains("ResolveSuppressedAnimatorFeed("))
                failures.Add("[wiring] " + LocoSrc + " no longer feeds the InputSuppressed branch through " +
                             "ResolveSuppressedAnimatorFeed - a hero moved by anything else during a " +
                             "dialogue beat is back to sliding in an idle pose (WO-1298 / F8 seq 4362).");
            if (Regex.IsMatch(loco, @"_actor\?\.SetLocomotion\(\s*0f\s*\)"))
                failures.Add("[wiring] " + LocoSrc + " has re-introduced an unconditional " +
                             "SetLocomotion(0f) - that literal IS the WO-1298 defect: it does not merely " +
                             "leave the animator stale, it OVERWRITES a live walk cycle with a dead zero " +
                             "every frame of the suppression.");

            // (3) The basis degradation that WAS the bug must not come back.
            if (Regex.IsMatch(loco, @"_smartCamera\s*!=\s*null\s*\?\s*_smartCamera\.CameraYaw\s*:\s*0f"))
                failures.Add("[wiring] " + LocoSrc + " has re-introduced the silent " +
                             "`_smartCamera != null ? CameraYaw : 0f` basis degradation - in a dungeon " +
                             "that resolves to identity and the stick becomes world-absolute.");
            if (!loco.Contains("ResolveMovementBasisYaw("))
                failures.Add("[wiring] " + LocoSrc + " no longer resolves its basis through " +
                             "ResolveMovementBasisYaw.");

            // (4) DungeonHero's Animator is re-resolved, not cached once in Awake (WO-968 E13).
            if (!hero.Contains("ResolveAnimator()"))
                failures.Add("[wiring] " + HeroSrc + " no longer re-resolves its Animator - an Awake-only " +
                             "cache resolves BEFORE the async HeroBodySwapper rebuilds the rig, making " +
                             "every Speed write a permanent silent no-op.");
            // NB: the opening brace is written \x7B, not a literal - the project's C# quality gate
            // (CLAUDE.md section 1) counts raw { / } characters per FILE, so a literal brace inside
            // a regex string reads to it as an unbalanced file.
            if (!Regex.IsMatch(hero, @"private\s+void\s+Update\s*\(\s*\)\s*\x7B\s*ResolveAnimator\s*\(\s*\)\s*;"))
                failures.Add("[wiring] " + HeroSrc + " does not call ResolveAnimator() first thing in " +
                             "Update - a self-heal that is never polled heals nothing.");

            // (5) Every stick sampler shares ONE basis site (no re-cloned silent fallbacks).
            if (Regex.Matches(hero, @"CameraRelative\s*\(").Count < 4)
                failures.Add("[wiring] " + HeroSrc + " no longer routes all three stick samplers (WASD, " +
                             "joystick, kit D-pad) through the single CameraRelative() basis site - a " +
                             "re-cloned projection block reintroduces the silent world-axis fallback.");

            // (6) The camera self-heal is polled. THE SHIP-TOGETHER RULE (WO-968 section 8): the basis
            //     fix without the camera fix is a constant 180-degree inverted stick.
            if (!rig.Contains("HealFollowTarget()"))
                failures.Add("[wiring] " + RigSrc + " no longer polls HealFollowTarget - the dungeon " +
                             "camera can freeze at its bind seat again, and a camera-relative stick " +
                             "against a frozen camera is a permanently INVERTED stick.");
            if (!Regex.IsMatch(rig, @"private\s+void\s+LateUpdate\s*\(\s*\)\s*\x7B\s*HealFollowTarget\s*\(\s*\)\s*;"))
                failures.Add("[wiring] " + RigSrc + " does not call HealFollowTarget() first thing in " +
                             "LateUpdate.");
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
