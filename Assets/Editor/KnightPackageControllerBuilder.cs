// =============================================================================
// KnightPackageControllerBuilder — builds the dedicated PALADIN hero controller
// from the Knight hero package's EXTRACTED clips (owner ruling 2026-07-03: the
// Mixamo Paladin in Assets/HeroPackages/Knight IS the new hero body).
// -----------------------------------------------------------------------------
// Extends the proven HeroAnimatorFactory pattern (flat state list + 1-D blend
// trees + AnyState action triggers + upper-body overlay layer), sourced from the
// standalone .anim assets HeroPackageImporter extracted (stable guids, Humanoid
// against the Knight_Hero avatar).
//
// SINGLE CADENCE AUTHORITY (dossier §5.1 mandate): every blend child plays at
// timeScale 1 and every state at speed 1 — the runtime pairs this controller
// with anim.speed = 1 (HeroBodySwapper package path), so there is exactly ONE
// knob (HeroLocomotionCadence's PlayerPrefs "anim.runCadence", normalized so
// the default 1.5 = x1.0 here). No 0.5-global x2/x3-bake layering.
//
// RUNTIME PARAMETER CONTRACT (matched EXACTLY — no runtime param renames):
//   Speed(F) InCombat(B) Attack(T) Combo(I) Cast(T) CastVariant(I) WindUp(T)
//   Block(B) Hit(T) HitDir(I) Dead(B) DeathDir(I) Victory(T) Injured(B)
//   + Combat(B)   — HeroPoseController's guarded stance bool (wave lifecycle)
//   + Knockdown(T)— NEW, declared for the Sweep Fall / Getting Up pair (no
//                   runtime driver yet — declared-but-unused is the project's
//                   safe convention; ActorAnimator can adopt it later).
//
// DIRECTIONAL DEATHS (owner design, dossier addendum 2026-07-03) on the
// EXTENDED DeathDirection enum (AnimParams.cs — old values unchanged):
//   0 Fall/default → Combat_Weapon_Combat_Movement_Locked_Death
//   1 Left        → Signature_Standing_Death_Left_01
//   2 Right       → Signature_Death_From_Right
//   3 Front       → Signature_Death_Forward           (owner-mapped 07-03)
//   4 Back        → Signature_Standing_Death_Backward_01 (name-mapped)
//   5 Assassinate → Signature_Two_Handed_Sword_Death_1 (TENTATIVE — owner confirm)
//
// PREBATTLE (hostile) substate: Locomotion → Unsheathe (Sheathing Sword played
// REVERSED, state speed -1 + cycleOffset 1) → CombatLocomotion (Standing Aim
// Idle 01 @0 / Standing Walk Forward @2 / Sword And Shield Run @6). Entered on
// InCombat OR Combat; exits when both are false.
//
// PACKAGE GAPS filled from the shared Humanoid Action set (retargets):
//   Victory = Shared_Victory_Pose, Block = Shared_Block, InjuredLocomotion =
//   Action/Enemies injured idle/walk/run. Noted as package gaps.
//
// Also publishes the runtime pickup: Assets/Resources/Heroes/KnightPackage.prefab
// (a variant of Knight_Hero.fbx with this controller + its Humanoid avatar bound)
// so HeroAssetLoader.LoadHeroPrefab("KnightPackage") resolves in player builds.
//
// Run (batchmode, orchestrator-gated):
//   powershell -File ./run-unity-method.ps1 -Method DeNelle.Editor.KnightPackageControllerBuilder.Build -LogName knight-package-controller.log
// Or in-editor: Defenders > Heroes > Build Knight Package Controller.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Core.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class KnightPackageControllerBuilder
    {
        private const string PackageRoot    = "Assets/HeroPackages/Knight";
        private const string ExtractRoot    = PackageRoot + "/Animations/Extracted";
        private const string HeroFbxPath    = PackageRoot + "/Knight_Hero.fbx";
        private const string ControllerPath = PackageRoot + "/Controller/KnightPackage.controller";
        private const string UpperMaskPath  = PackageRoot + "/Controller/KnightPackageUpperBody.mask";
        private const string RuntimePrefab  = "Assets/Resources/Heroes/KnightPackage.prefab";
        private const string WeaponSkillJson = "Assets/Resources/Data/Canonical/weaponskill-animations.json";
        private const string Log = "[KnightPackageControllerBuilder] ";

        // Action Keyword Registry target (motion-castings.json, WO-670 slice 1):
        // every hardcoded pick below is wrapped in MotionCastings.Resolve with the
        // current constant as the terminal default — empty registry = byte-identical.
        private const string CastingTarget = "knight";

        // ── Locomotion thresholds — MUST match the raw Speed feed (HeroLocomotion
        // Velocity.magnitude, _moveSpeed = 6): idle@0 / walk@2 / run@6.
        private const float WalkThreshold = 2f;
        private const float RunThreshold  = 6f;
        // WO-218 convention: full-body attack/cast only when standing; the
        // upper-body layer carries the swing while moving.
        private const float StandingSpeedMax = 2.0f;
        // Snappy return timing (WO-217 convention).
        private const float ActionExitTime = 0.75f;  // at speed 1.0 (not 1.3/0.5-layered) let more of the swing play
        private const float ActionExitDur  = 0.08f;

        // ── Extracted clip names (Animations/Extracted/<name>.anim) ─────────────
        private const string ClipIdle       = "Passive_Locomotion_Idle_2";
        private const string ClipIdleVar3   = "Passive_Locomotion_Idle_3";
        private const string ClipIdleVar4   = "Passive_Locomotion_Idle_4";
        private const string ClipWalk       = "Passive_Locomotion_Walk";
        private const string ClipRun        = "Passive_Locomotion_Run";
        private const string ClipCombatIdle = "Combat_Weapon_Combat_Movement_Locked_Standing_Aim_Idle_01";
        private const string ClipCombatWalk = "Passive_Locomotion_Motion_Standing_Walk_Forward";
        private const string ClipCombatRun  = "Passive_Locomotion_Motion_Sword_And_Shield_Run";
        private const string ClipUnsheathe  = "Combat_Weapon_Combat_Movement_Locked_Sheathing_Sword"; // played reversed
        private const string ClipHit        = "Passive_Reaction_Hit_Reaction";
        private const string ClipBasicSlash = "Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash";
        private const string ClipSweepFall  = "Signature_Sweep_Fall";
        private const string ClipGettingUp  = "Signature_Getting_Up";

        // Owner pick 2026-07-11 — Block/deflect = S&S ShieldSwipe01 chaining into
        // ShieldSwipe02 (extracted by SwordShieldMovesImporter). Swipe01 arrives via
        // the `block` registry row (knight.block, manual); swipe02 is the second
        // beat the Block2 state plays. Both NO-OP-safe: missing swipe01 leaves the
        // registry falling through to the Shared_Block default; missing swipe02
        // leaves Block single-beat (warned, never broken).
        private const string ClipBlockSwipe01 = "Combat_Weapon_WeaponSkill_SwordShield_ShieldSwipe01";
        private const string ClipBlockSwipe02 = "Combat_Weapon_WeaponSkill_SwordShield_ShieldSwipe02";

        // Directional deaths (owner table — see file header).
        private const string ClipDeathDefault = "Combat_Weapon_Combat_Movement_Locked_Death";
        private const string ClipDeathLeft    = "Signature_Standing_Death_Left_01";
        private const string ClipDeathRight   = "Signature_Death_From_Right";
        private const string ClipDeathFront   = "Signature_Death_Forward";
        private const string ClipDeathBack    = "Signature_Standing_Death_Backward_01";
        private const string ClipDeathAssassinate = "Signature_Two_Handed_Sword_Death_1"; // TENTATIVE

        // Per-slot cast/special swings (CastVariant 1..4 = q/w/e/r) from the
        // WeaponSkill special-ability pool (owner: skill-level binding; these are
        // the controller-side states the skill rows resolve onto).
        private static readonly string[] SpellCastClips =
        {
            null,                                                    // [0] generic (basic slash)
            "Combat_Weapon_WeaponSkill_Inward_Slash",                // [1] q  Shield Bash
            "Combat_Weapon_WeaponSkill_Standing_Melee_Attack_360_High", // [2] w  Bulwark Slam
            "Combat_Spell_Two_Hand_Spell_Casting",                   // [3] e  Oath Ward (buff pose)
            "Combat_Weapon_WeaponSkill_Downward_Slice",              // [4] r  Lantern Charge
        };

        // weaponskill-animations.json clip name -> package WeaponSkill clip. The
        // json still names the OLD Assets/Action clips; this table is the seam
        // that resolves each skill row onto its package equivalent (unmapped rows
        // fall back to the basic slash).
        private static readonly Dictionary<string, string> JsonClipToPackage =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "standing melee attack horizontal",    "Combat_Weapon_WeaponSkill_Outward_Slash" },
            { "standing melee combo attack ver. 1",  "Combat_Weapon_WeaponSkill_Stabbing" },
            { "standing melee combo attack ver. 2",  "Combat_Weapon_WeaponSkill_Combo" },
            { "standing melee run jump attack",      "Combat_Weapon_WeaponSkill_GreatSword_Swing" },
            { "standing melee attack 360 high",      "Combat_Weapon_WeaponSkill_Standing_Melee_Attack_360_High" },
            { "sword and shield power up",           "Combat_Spell_Two_Hand_Spell_Casting" },
            { "standing melee attack downward",      "Combat_Weapon_WeaponSkill_Downward_Slice" },
        };

        // Package gaps — shared Humanoid clips that retarget onto the Paladin avatar.
        private const string SharedVictoryFbx = "Assets/Action/Shared/Shared_Victory_Pose.fbx";
        private const string SharedBlockFbx   = "Assets/Action/Shared/Shared_Block.fbx";
        // Injured IDLE upgraded 2026-07-03: "injured hurting idle" reads as hurt-but-standing
        // (clutching the wound, rooted) — a cleaner wounded read than the flat "injured idle"
        // placeholder. Same Humanoid mixamo rig (animationType:3), retargets onto the Paladin
        // avatar identically. Walk/run stay on the matching injured set.
        private const string InjuredIdleFbx   = "Assets/Action/Enemies/injured hurting idle.fbx";
        private const string InjuredWalkFbx   = "Assets/Action/Enemies/injured walk.fbx";
        private const string InjuredRunFbx    = "Assets/Action/Enemies/injured run.fbx";

        [MenuItem("Defenders/Heroes/Build Knight Package Controller")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(ExtractRoot))
            {
                Debug.LogError(Log + "extracted clips folder missing: " + ExtractRoot +
                    " — run DeNelle.Editor.HeroPackageImporter.ImportKnight first.");
                return;
            }

            // ── Load clips (null-guarded — a missing clip skips its state) ──────
            var idle       = Clip(ClipIdle);
            var idleVar3   = Clip(ClipIdleVar3);
            var idleVar4   = Clip(ClipIdleVar4);
            var walk       = Clip(ClipWalk);
            var run        = Clip(ClipRun);
            var combatIdle = Clip(ClipCombatIdle);
            var combatWalk = Clip(ClipCombatWalk);
            var combatRun  = Clip(ClipCombatRun);
            // Registry-wrapped picks (WO-670): keyword resolve with the hardcoded
            // clip as the terminal default (empty registry ⇒ identical output).
            var unsheathe  = MotionCastings.Resolve(CastingTarget, ActionKeywords.Unsheathe, Clip(ClipUnsheathe));
            var hit        = MotionCastings.Resolve(CastingTarget, ActionKeywords.Hit,       Clip(ClipHit));
            var basicSlash = Clip(ClipBasicSlash);
            var sweepFall  = MotionCastings.Resolve(CastingTarget, ActionKeywords.Knockdown, Clip(ClipSweepFall));
            var gettingUp  = MotionCastings.Resolve(CastingTarget, ActionKeywords.GettingUp, Clip(ClipGettingUp));
            var victory    = MotionCastings.Resolve(CastingTarget, ActionKeywords.Victory,
                                 FbxClip(SharedVictoryFbx));   // package gap — shared pose
            var block      = MotionCastings.Resolve(CastingTarget, ActionKeywords.Block,
                                 FbxClip(SharedBlockFbx));     // package gap — shared block

            // ── Idempotent controller create ─────────────────────────────────────
            AssetDatabase.DeleteAsset(ControllerPath);
            EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Parameter surface — EXACT runtime names (see file header).
            ctrl.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            ctrl.AddParameter("InCombat",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Combat",      AnimatorControllerParameterType.Bool);   // HeroPoseController
            ctrl.AddParameter("Attack",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Combo",       AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Cast",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("CastVariant", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("WindUp",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Block",       AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Hit",         AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitDir",      AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Dead",        AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("DeathDir",    AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Victory",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Injured",     AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Knockdown",   AnimatorControllerParameterType.Trigger); // Sweep Fall pair (no driver yet)

            var sm = ctrl.layers[0].stateMachine;

            // ── Calm Locomotion (default): Idle_2 @0 / Walk @2 / Run @6, timeScale 1 ──
            var loco = sm.AddState("Locomotion");
            sm.defaultState = loco;
            loco.motion = MakeTree(ctrl, "Locomotion",
                (idle, 0f), (walk, WalkThreshold), (run, RunThreshold));

            // ── Hostile CombatLocomotion: Aim Idle @0 / Standing Walk @2 / S&S Run @6 ──
            var combatLoco = sm.AddState("CombatLocomotion");
            combatLoco.motion = MakeTree(ctrl, "CombatLocomotion",
                (combatIdle, 0f), (combatWalk ?? walk, WalkThreshold), (combatRun ?? run, RunThreshold));

            // ── Idle variety (cheap timed variety): after N idle loops in Locomotion
            // while standing, play Idle_3 / Idle_4 once, then return. Cosmetic — a
            // missing clip skips the state. Movement (Speed) interrupts immediately.
            BuildIdleVariety(sm, loco, "IdleVariety3", idleVar3, 4f);
            BuildIdleVariety(sm, loco, "IdleVariety4", idleVar4, 9f);

            // ── Prebattle: Unsheathe (reversed Sheathing Sword) → CombatLocomotion ──
            // Entered on the hostile flip (InCombat via ActorAnimator.SetCombatStance
            // OR Combat via HeroPoseController — either drives it). Standing engages
            // play the unsheathe; engaging while already moving skips straight to the
            // combat gait (the clip is stationary).
            if (unsheathe != null)
            {
                var unsheatheState = sm.AddState("Unsheathe");
                unsheatheState.motion = unsheathe;
                unsheatheState.speed = -1f;          // owner mapping: Sheathing Sword REVERSED = draw
                unsheatheState.cycleOffset = 1f;     // start from the clip end when playing backward

                foreach (string flag in new[] { "InCombat", "Combat" })
                {
                    var toUnsheathe = loco.AddTransition(unsheatheState);
                    toUnsheathe.hasExitTime = false; toUnsheathe.duration = 0.1f;
                    toUnsheathe.AddCondition(AnimatorConditionMode.If, 0f, flag);
                    toUnsheathe.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

                    var toCombatMoving = loco.AddTransition(combatLoco);
                    toCombatMoving.hasExitTime = false; toCombatMoving.duration = 0.2f;
                    toCombatMoving.AddCondition(AnimatorConditionMode.If, 0f, flag);
                    toCombatMoving.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
                }

                // Reverse-playback exit: normalizedTime runs 1→0 under speed -1, and
                // Unity's exit-time crossing semantics differ by direction — author BOTH
                // a near-end (0.95) and near-start (0.05) exit so whichever crossing the
                // engine honors fires. Both target CombatLocomotion; harmless duplicate.
                foreach (float exit in new[] { 0.95f, 0.05f })
                {
                    var done = unsheatheState.AddTransition(combatLoco);
                    done.hasExitTime = true; done.exitTime = exit; done.duration = 0.15f;
                }
                // Abort (combat ended mid-draw).
                var abort = unsheatheState.AddTransition(loco);
                abort.hasExitTime = false; abort.duration = 0.15f;
                abort.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
                abort.AddCondition(AnimatorConditionMode.IfNot, 0f, "Combat");
            }
            else
            {
                // No unsheathe clip — hostile flip goes straight to the combat gait.
                foreach (string flag in new[] { "InCombat", "Combat" })
                {
                    var t = loco.AddTransition(combatLoco);
                    t.hasExitTime = false; t.duration = 0.25f;
                    t.AddCondition(AnimatorConditionMode.If, 0f, flag);
                }
            }

            // Postbattle calm: leave the combat gait when BOTH stance bools clear.
            var toCalm = combatLoco.AddTransition(loco);
            toCalm.hasExitTime = false; toCalm.duration = 0.25f;
            toCalm.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            toCalm.AddCondition(AnimatorConditionMode.IfNot, 0f, "Combat");

            // ── Attack combo states (Attack trigger + Combo int, standing-gated) ──
            // Attack0 = the BASIC swing (Sword And Shield Slash, owner spec); Attack1/2
            // resolve from weaponskill-animations.json knight rows via JsonClipToPackage.
            var comboClips = ResolveComboClips(basicSlash);
            // Registry wrap (WO-670): attack0/1/2 keywords over the json-resolved picks.
            string[] attackKeywords = { ActionKeywords.Attack0, ActionKeywords.Attack1, ActionKeywords.Attack2 };
            for (int i = 0; i < comboClips.Length && i < attackKeywords.Length; i++)
                comboClips[i] = MotionCastings.Resolve(CastingTarget, attackKeywords[i], comboClips[i]);
            for (int i = 0; i < comboClips.Length; i++)
            {
                if (comboClips[i] == null) continue;
                var st = sm.AddState("Attack" + i);
                st.motion = comboClips[i];
                st.speed = 1f;   // single cadence authority — natural Mixamo playback
                var to = sm.AddAnyStateTransition(st);
                to.hasExitTime = false; to.duration = 0.05f; to.canTransitionToSelf = false;
                to.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                to.AddCondition(AnimatorConditionMode.Less, StandingSpeedMax, "Speed");
                to.AddCondition(AnimatorConditionMode.Equals, i, "Combo");
                AddCombatAwareReturn(st, loco, combatLoco, ActionExitTime, ActionExitDur);
            }

            // ── Generic Cast + per-variant Cast_q/w/e/r (CastVariant gate) ────────
            // Registry wrap (WO-670): the generic cast resolves the `cast` keyword;
            // the q/w slots resolve `skill1`/`skill2` (the two skill keywords in the
            // closed vocabulary — e/r stay on their hardcoded picks). Resolved ONCE
            // here and reused by the upper-body layer so the two stay in lock-step.
            AnimationClip genericCast = MotionCastings.Resolve(CastingTarget, ActionKeywords.Cast, basicSlash);
            var spellClips = new AnimationClip[SpellCastClips.Length];
            for (int v = 1; v < SpellCastClips.Length; v++)
            {
                var slotClip = Clip(SpellCastClips[v]);
                if (v == 1) slotClip = MotionCastings.Resolve(CastingTarget, ActionKeywords.Skill1, slotClip);
                else if (v == 2) slotClip = MotionCastings.Resolve(CastingTarget, ActionKeywords.Skill2, slotClip);
                // Cast_e is the heal/ward slot (E-slot actives: Oathmend / Second
                // Wind / Mending Salve / Defender's Call). Owner pick 2026-07-11:
                // heal casts fire the Magical Moves "Magic Spell Cast 02" via the
                // knight.castHeal registry row (manual) — melee/caster hard rule +
                // the F8-48 animation half. Registry miss = hardcoded pick stands.
                else if (v == 3) slotClip = MotionCastings.Resolve(CastingTarget, ActionKeywords.CastHeal, slotClip);
                spellClips[v] = slotClip;
            }
            if (genericCast != null)
            {
                var castState = sm.AddState("Cast");
                castState.motion = genericCast;
                castState.speed = 1f;
                var toCast = sm.AddAnyStateTransition(castState);
                toCast.hasExitTime = false; toCast.duration = 0.05f; toCast.canTransitionToSelf = false;
                toCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
                toCast.AddCondition(AnimatorConditionMode.Less, StandingSpeedMax, "Speed");
                AddCombatAwareReturn(castState, loco, combatLoco, ActionExitTime, ActionExitDur);
            }
            string[] slotName = { "0", "q", "w", "e", "r" };
            for (int v = 1; v < spellClips.Length; v++)
            {
                var clip = spellClips[v];
                if (clip == null) continue;
                var st = sm.AddState("Cast_" + slotName[v]);
                st.motion = clip;
                st.speed = 1f;
                var to = sm.AddAnyStateTransition(st);
                to.hasExitTime = false; to.duration = 0.05f; to.canTransitionToSelf = false;
                to.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
                to.AddCondition(AnimatorConditionMode.Equals, v, "CastVariant");
                to.AddCondition(AnimatorConditionMode.Less, StandingSpeedMax, "Speed");
                AddCombatAwareReturn(st, loco, combatLoco, ActionExitTime, ActionExitDur);
            }

            // ── Hit reaction ─────────────────────────────────────────────────────
            if (hit != null)
            {
                var hitState = sm.AddState("Hit");
                hitState.motion = hit;
                var toHit = sm.AddAnyStateTransition(hitState);
                toHit.hasExitTime = false; toHit.duration = 0.04f; toHit.canTransitionToSelf = false;
                toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                AddCombatAwareReturn(hitState, loco, combatLoco, 0.7f, 0.1f);
            }

            // ── Knockdown pair: Sweep Fall → Getting Up (Knockdown trigger) ──────
            if (sweepFall != null && gettingUp != null)
            {
                var fall = sm.AddState("SweepFall");
                fall.motion = sweepFall;
                var toFall = sm.AddAnyStateTransition(fall);
                toFall.hasExitTime = false; toFall.duration = 0.05f; toFall.canTransitionToSelf = false;
                toFall.AddCondition(AnimatorConditionMode.If, 0f, "Knockdown");

                var up = sm.AddState("GettingUp");
                up.motion = gettingUp;
                var fallToUp = fall.AddTransition(up);
                fallToUp.hasExitTime = true; fallToUp.exitTime = 0.95f; fallToUp.duration = 0.1f;
                AddCombatAwareReturn(up, loco, combatLoco, 0.9f, 0.15f);
            }

            // ── Directional deaths (Dead bool latch + DeathDir int selector) ─────
            // Directional AnyState transitions are added BEFORE the unconditioned
            // default so their extra DeathDir condition gets first match; the default
            // (no DeathDir gate) covers 0 and any unmapped value. Revive (Dead=false)
            // returns every death state to Locomotion.
            // Registry wrap (WO-670): death1..5 = the directional table, death0 = the
            // unconditioned default (DeathDirection Fall) — hardcoded picks stay terminal.
            BuildDeath(sm, loco, "DeathLeft",        MotionCastings.Resolve(CastingTarget, ActionKeywords.Death1, Clip(ClipDeathLeft)),        1);
            BuildDeath(sm, loco, "DeathRight",       MotionCastings.Resolve(CastingTarget, ActionKeywords.Death2, Clip(ClipDeathRight)),       2);
            BuildDeath(sm, loco, "DeathFront",       MotionCastings.Resolve(CastingTarget, ActionKeywords.Death3, Clip(ClipDeathFront)),       3);
            BuildDeath(sm, loco, "DeathBack",        MotionCastings.Resolve(CastingTarget, ActionKeywords.Death4, Clip(ClipDeathBack)),        4);
            BuildDeath(sm, loco, "DeathAssassinate", MotionCastings.Resolve(CastingTarget, ActionKeywords.Death5, Clip(ClipDeathAssassinate)), 5); // TENTATIVE mapping
            BuildDeath(sm, loco, "Death",            MotionCastings.Resolve(CastingTarget, ActionKeywords.Death0, Clip(ClipDeathDefault)),     -1); // -1 = unconditioned default

            // ── Victory (package gap — shared pose retargets) ────────────────────
            if (victory != null)
            {
                var vic = sm.AddState("Victory");
                vic.motion = victory;
                var toVic = sm.AddAnyStateTransition(vic);
                toVic.hasExitTime = false; toVic.duration = 0.1f;
                toVic.AddCondition(AnimatorConditionMode.If, 0f, "Victory");
                var back = vic.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.15f;
            }
            else Debug.LogWarning(Log + "no victory clip (package gap) — Victory trigger no-ops.");

            // ── Block (owner pick 2026-07-11: "Shield block, for incoming attacks
            // and spell deflection use Shield Swipe 01 into shield swipe 2" — the
            // knight.block registry row resolves ShieldSwipe01; when it did AND the
            // extracted ShieldSwipe02 exists, a Block2 second beat chains off it
            // with the attack-combo transition discipline. Registry miss keeps the
            // Shared_Block package-gap default, single-beat — never a broken state.)
            if (block != null)
            {
                var blockState = sm.AddState("Block");
                blockState.motion = block;
                foreach (var from in new[] { loco, combatLoco })
                {
                    var toBlock = from.AddTransition(blockState);
                    toBlock.hasExitTime = false; toBlock.duration = 0.1f;
                    toBlock.AddCondition(AnimatorConditionMode.If, 0f, "Block");
                }

                // Second beat — only when the owner-picked swipe01 actually resolved
                // (chaining swipe02 after a non-swipe block clip would misread).
                AnimatorState blockChain = null;
                if (block.name == ClipBlockSwipe01)
                {
                    var swipe02 = Clip(ClipBlockSwipe02);
                    if (swipe02 != null)
                    {
                        blockChain = sm.AddState("Block2");
                        blockChain.motion = swipe02;
                        var toChain = blockState.AddTransition(blockChain);
                        toChain.hasExitTime = true; toChain.exitTime = 0.9f; toChain.duration = 0.08f;
                        toChain.AddCondition(AnimatorConditionMode.If, 0f, "Block");
                    }
                    else Debug.LogWarning(Log + "block second beat missing (" + ClipBlockSwipe02 +
                        ".anim) — Block stays single-beat on ShieldSwipe01 (run " +
                        "SwordShieldMovesImporter.Import to extract it).");
                }

                var blockExitStates = blockChain != null
                    ? new[] { blockState, blockChain }
                    : new[] { blockState };
                foreach (var st in blockExitStates)
                {
                    var backCombat = st.AddTransition(combatLoco);
                    backCombat.hasExitTime = false; backCombat.duration = 0.12f;
                    backCombat.AddCondition(AnimatorConditionMode.IfNot, 0f, "Block");
                    backCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
                    var backCalm = st.AddTransition(loco);
                    backCalm.hasExitTime = false; backCalm.duration = 0.12f;
                    backCalm.AddCondition(AnimatorConditionMode.IfNot, 0f, "Block");
                }
            }

            // ── Injured locomotion (package gap — Action/Enemies injured set) ────
            // KEPT per owner 2026-07-03 (reversed the brief "86 it"): the structure stays
            // wired; the current Action/Enemies retargets are a PLACEHOLDER until the owner
            // sources better dedicated Paladin injured clips — swap InjuredIdle/Walk/RunFbx.
            var injIdle = MotionCastings.Resolve(CastingTarget, ActionKeywords.InjuredIdle, FbxClip(InjuredIdleFbx)) ?? idle;
            var injWalk = MotionCastings.Resolve(CastingTarget, ActionKeywords.InjuredWalk, FbxClip(InjuredWalkFbx)) ?? walk;
            var injRun  = MotionCastings.Resolve(CastingTarget, ActionKeywords.InjuredRun,  FbxClip(InjuredRunFbx))  ?? run;
            var injured = sm.AddState("InjuredLocomotion");
            injured.motion = MakeTree(ctrl, "InjuredLocomotion",
                (injIdle, 0f), (injWalk, WalkThreshold), (injRun, RunThreshold));
            var toInjured = loco.AddTransition(injured);
            toInjured.hasExitTime = false; toInjured.duration = 0.2f;
            toInjured.AddCondition(AnimatorConditionMode.If, 0f, "Injured");
            var fromInjured = injured.AddTransition(loco);
            fromInjured.hasExitTime = false; fromInjured.duration = 0.2f;
            fromInjured.AddCondition(AnimatorConditionMode.IfNot, 0f, "Injured");

            // ── Upper-body overlay (attack/cast while moving, WO-218 pattern) ────
            if (genericCast != null)
                AddUpperBodyLayer(ctrl, genericCast, spellClips);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ── Publish the runtime pickup prefab (Resources/Heroes/KnightPackage) ──
            PublishRuntimePrefab(ctrl);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(Log + "DONE — controller at " + ControllerPath + " + runtime prefab at " +
                RuntimePrefab + " (assassinate death mapping TENTATIVE pending owner confirm).");
        }

        // Attack0 = basic Sword And Shield Slash (owner spec); Attack1/2 from the
        // weaponskill-animations.json knight rows (combo index → json clip →
        // JsonClipToPackage). Unresolved indices fall back to the basic slash so
        // the runtime's Combo cycling never lands on a missing state.
        private static AnimationClip[] ResolveComboClips(AnimationClip basicSlash)
        {
            var result = new AnimationClip[3];
            result[0] = basicSlash;
            result[1] = basicSlash;
            result[2] = basicSlash;
            try
            {
                if (File.Exists(WeaponSkillJson))
                {
                    // "class" is a C# keyword — remap the key before JsonUtility parse.
                    string text = File.ReadAllText(WeaponSkillJson).Replace("\"class\":", "\"clazz\":");
                    var file = JsonUtility.FromJson<WsFile>(text);
                    if (file?.skills != null)
                    {
                        foreach (var s in file.skills)
                        {
                            if (s == null || s.clazz != "knight" || s.trigger != "Attack") continue;
                            if (s.combo < 1 || s.combo > 2) continue; // 0 stays the basic slash
                            if (s.clip != null && JsonClipToPackage.TryGetValue(s.clip, out string pkg))
                            {
                                var clip = Clip(pkg);
                                if (clip != null) result[s.combo] = clip;
                                else Debug.LogWarning(Log + "json skill '" + s.skill +
                                    "' mapped to missing package clip '" + pkg + "' — basic slash fallback.");
                            }
                            else Debug.LogWarning(Log + "json skill '" + s.skill + "' clip '" + s.clip +
                                "' has no package equivalent — basic slash fallback.");
                        }
                    }
                }
                else Debug.LogWarning(Log + "weaponskill-animations.json missing at " + WeaponSkillJson +
                    " — all combo states use the basic slash.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Log + "weaponskill-animations.json parse failed (" + ex.Message +
                    ") — all combo states use the basic slash.");
            }
            return result;
        }

        [Serializable] private class WsFile  { public WsSkill[] skills; }
        [Serializable] private class WsSkill
        {
            public string clazz;    // remapped from "class"
            public string skill;
            public string slot;
            public string trigger;
            public int combo = -1;
            public string clip;
        }

        // 1-D Speed blend tree with explicit thresholds, timeScale 1 everywhere
        // (single cadence authority). Null children are skipped.
        private static BlendTree MakeTree(AnimatorController ctrl, string name,
                                          params (AnimationClip clip, float threshold)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false, // CRITICAL — see HeroAnimatorFactory (auto overwrites thresholds)
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            int added = 0;
            foreach (var (clip, threshold) in children)
            {
                if (clip == null) continue;
                tree.AddChild(clip, threshold);
                added++;
            }
            if (added == 0)
                Debug.LogWarning(Log + "blend tree '" + name + "' has NO clips — state will be empty.");
            return tree;
        }

        // Timed idle variety: after `loops` normalized loops in Locomotion while
        // standing (Speed<0.05), play the variety clip once, then return. Movement
        // interrupts immediately so responsiveness is untouched.
        private static void BuildIdleVariety(AnimatorStateMachine sm, AnimatorState loco,
                                             string name, AnimationClip clip, float loops)
        {
            if (clip == null) return;
            var st = sm.AddState(name);
            st.motion = clip;
            var to = loco.AddTransition(st);
            to.hasExitTime = true; to.exitTime = loops; to.duration = 0.3f;
            to.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            var done = st.AddTransition(loco);
            done.hasExitTime = true; done.exitTime = 0.95f; done.duration = 0.3f;
            var moved = st.AddTransition(loco);
            moved.hasExitTime = false; moved.duration = 0.1f;
            moved.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        // Return-to-gait transitions that respect the posture: an action ending
        // while hostile returns to CombatLocomotion (never re-triggering the
        // unsheathe), otherwise to calm Locomotion. Order matters — conditioned
        // combat returns are added first so they win the AnyState-style pick.
        private static void AddCombatAwareReturn(AnimatorState state, AnimatorState loco,
                                                 AnimatorState combatLoco, float exitTime, float duration)
        {
            foreach (string flag in new[] { "InCombat", "Combat" })
            {
                var toCombat = state.AddTransition(combatLoco);
                toCombat.hasExitTime = true; toCombat.exitTime = exitTime; toCombat.duration = duration;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, flag);
            }
            var toCalm = state.AddTransition(loco);
            toCalm.hasExitTime = true; toCalm.exitTime = exitTime; toCalm.duration = duration;
        }

        // One death state. dirValue >= 0 adds the DeathDir == dirValue gate;
        // dirValue -1 builds the UNCONDITIONED default (call it LAST so the
        // directional transitions get first match on the AnyState list).
        private static void BuildDeath(AnimatorStateMachine sm, AnimatorState loco,
                                       string name, AnimationClip clip, int dirValue)
        {
            if (clip == null)
            {
                Debug.LogWarning(Log + "death clip missing for state '" + name + "' — skipped " +
                    "(default Death covers that DeathDir).");
                return;
            }
            var st = sm.AddState(name);
            st.motion = clip;
            var to = sm.AddAnyStateTransition(st);
            to.hasExitTime = false; to.duration = 0.06f; to.canTransitionToSelf = false;
            to.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            if (dirValue >= 0)
                to.AddCondition(AnimatorConditionMode.Equals, dirValue, "DeathDir");
            var back = st.AddTransition(loco);
            back.hasExitTime = false; back.duration = 0.12f;
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }

        // WO-218 pattern: an "Upper Body" Override layer (arms+torso mask) driven
        // by the same Cast/Attack triggers so the hero swings while moving. Empty
        // default state contributes nothing when idle. Mirrors HeroAnimatorFactory.
        private static void AddUpperBodyLayer(AnimatorController ctrl, AnimationClip genericCast,
                                              AnimationClip[] spellClips)
        {
            var mask = EnsureUpperBodyMask();

            var sm = new AnimatorStateMachine { name = "Upper Body" };
            AssetDatabase.AddObjectToAsset(sm, ctrl);

            var empty = sm.AddState("Empty");
            sm.defaultState = empty;

            var upper = sm.AddState("CastUpper");
            upper.motion = genericCast;
            upper.speed = 1f;

            var toCast = sm.AddAnyStateTransition(upper);
            toCast.hasExitTime = false; toCast.duration = 0.05f; toCast.canTransitionToSelf = false;
            toCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
            var toAttack = sm.AddAnyStateTransition(upper);
            toAttack.hasExitTime = false; toAttack.duration = 0.05f; toAttack.canTransitionToSelf = false;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var back = upper.AddTransition(empty);
            back.hasExitTime = true; back.exitTime = ActionExitTime; back.duration = ActionExitDur;

            string[] slotName = { "0", "q", "w", "e", "r" };
            for (int v = 1; v < spellClips.Length; v++)
            {
                var clip = spellClips[v];
                if (clip == null) continue;
                var st = sm.AddState("CastUpper_" + slotName[v]);
                st.motion = clip;
                st.speed = 1f;
                var toV = sm.AddAnyStateTransition(st);
                toV.hasExitTime = false; toV.duration = 0.05f; toV.canTransitionToSelf = false;
                toV.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
                toV.AddCondition(AnimatorConditionMode.Equals, v, "CastVariant");
                var vBack = st.AddTransition(empty);
                vBack.hasExitTime = true; vBack.exitTime = ActionExitTime; vBack.duration = ActionExitDur;
            }

            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Upper Body",
                stateMachine = sm,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
            });
        }

        // Arms+torso mask (legs + root off) — package-local so it never fights the
        // shared Assets/Generated mask. Idempotent overwrite.
        private static AvatarMask EnsureUpperBodyMask()
        {
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root,        false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg,     false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg,    false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK,  false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            AssetDatabase.DeleteAsset(UpperMaskPath);
            AssetDatabase.CreateAsset(mask, UpperMaskPath);
            return mask;
        }

        // Publish Assets/Resources/Heroes/KnightPackage.prefab — a variant of the
        // package FBX carrying this controller + its own Humanoid avatar, so the
        // runtime pickup is one HeroAssetLoader.LoadHeroPrefab("KnightPackage")
        // (Resources auto-includes the FBX + controller as prefab dependencies in
        // player builds — nothing under HeroPackages needs its own Resources copy).
        private static void PublishRuntimePrefab(AnimatorController ctrl)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFbxPath);
            if (fbx == null)
            {
                Debug.LogError(Log + "hero FBX missing at " + HeroFbxPath + " — runtime prefab NOT published.");
                return;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                var anim = inst.GetComponent<Animator>();
                if (anim == null) anim = inst.AddComponent<Animator>();
                if (anim.avatar == null || !anim.avatar.isValid || !anim.avatar.isHuman)
                {
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(HeroFbxPath))
                        if (sub is Avatar av && av.isValid && av.isHuman) { anim.avatar = av; break; }
                }
                if (anim.avatar == null || !anim.avatar.isValid || !anim.avatar.isHuman)
                    Debug.LogError(Log + "Knight_Hero.fbx has no valid HUMANOID avatar — run " +
                        "HeroPackageImporter.ImportKnight (the prefab is published anyway; runtime " +
                        "self-reports + falls back to the Tripo body).");
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false; // HeroLocomotion owns movement (project convention)

                EnsureFolder("Assets/Resources/Heroes");
                PrefabUtility.SaveAsPrefabAsset(inst, RuntimePrefab, out bool ok);
                if (ok) Debug.Log(Log + "runtime prefab published: " + RuntimePrefab);
                else Debug.LogError(Log + "SaveAsPrefabAsset FAILED for " + RuntimePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        // ── Clip loaders ─────────────────────────────────────────────────────────
        private static AnimationClip Clip(string extractedName)
        {
            if (string.IsNullOrEmpty(extractedName)) return null;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ExtractRoot + "/" + extractedName + ".anim");
            if (clip == null)
                Debug.LogWarning(Log + "extracted clip not found: " + extractedName + ".anim");
            return clip;
        }

        // Shared Humanoid FBX clip (Assets/Action) — the retarget path for package
        // gaps (victory/block/injured). Skips __preview__ and T-pose takes.
        private static AnimationClip FbxClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning(Log + "shared clip FBX not found: " + fbxPath);
                return null;
            }
            AnimationClip fallback = null;
            foreach (var a in assets)
            {
                if (!(a is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                string n = clip.name.ToLowerInvariant();
                if (n.Contains("t-pose") || n.Contains("tpose") || n.Contains("bind")) { fallback ??= clip; continue; }
                return clip;
            }
            return fallback;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
