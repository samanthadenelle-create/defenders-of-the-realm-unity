// =============================================================================
// BuildOrcHumanoidController — WO-491: the FULL animation set for the Tripo orc
// family (Warrior / Tank / Mage). They share ONE humanoid rig (owner 2026-06-23:
// "they all pull same rig"), so this builds ONE base controller (OrcHumanoid) with
// the complete AnimParams state machine + per-role AnimatorOverrideControllers that
// only swap the clips on the shared states.
//
// FIXES THE SLIDE: the old controller had NO Speed param + NO walk state, so a
// moving NavMeshAgent orc stayed in Idle and slid. This adds a Speed Locomotion
// BlendTree (idle/walk/run) — the same proven pattern as HeroAnimatorFactory
// (useAutomaticThresholds=false is load-bearing; auto-thresholds skips walk).
//
// Also adds: Cast (Any->Cast on trigger, mage spell), WindUp telegraph, and an
// Injured locomotion sub-tree (entered when Injured==true, low-HP wounded stance).
//
//   run-unity-method.ps1 -Method DeNelle.Editor.BuildOrcHumanoidController.Run -LogName orc-controller.log
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BuildOrcHumanoidController
    {
        private const string BasePath = "Assets/Resources/Enemies/OrcHumanoid.controller";

        // Action Keyword Registry targets (motion-castings.json, WO-670 slice 1):
        // base states resolve `orc`; the role overrides resolve orc-mage/-warrior/
        // -tank (which inherit orc). Every hardcoded pick below stays the terminal
        // default — empty registry = byte-identical controllers.
        private const string CastingTargetBase    = "orc";
        private const string CastingTargetMage    = "orc-mage";
        private const string CastingTargetWarrior = "orc-warrior";
        private const string CastingTargetTank    = "orc-tank";

        // ── Shared locomotion / shared states ────────────────────────────────
        private const string IdleFbx       = "Assets/Action/Orc Idle.fbx";
        private const string WalkFbx       = "Assets/Action/standing walk forward.fbx";
        private const string RunFbx        = "Assets/Action/standing run forward.fbx";
        private const string HitFbx        = "Assets/Action/Shared/Shared_Hit_Reaction.fbx";
        private const string DeathFbx      = "Assets/Action/Shared/Shared_Death.fbx";
        private const string WindUpFbx     = "Assets/Action/Standing 2H Magic Attack 01.fbx"; // telegraph wind-up pose

        // ── Injured (wounded) locomotion sub-tree ────────────────────────────
        private const string InjuredIdleFbx = "Assets/Action/Enemies/injured idle.fbx";
        private const string InjuredWalkFbx = "Assets/Action/Enemies/injured walk.fbx";
        private const string InjuredRunFbx  = "Assets/Action/Enemies/injured run.fbx";

        // Combat-stance locomotion (InCombat bool — braced idle / weapon gait while alert).
        private const string CombatIdleFbx = "Assets/Action/Shared/Shared_Combat_Idle.fbx";
        private const string CombatWalkFbx = "Assets/Action/Knight/sword and shield walk.fbx";
        private const string CombatRunFbx  = "Assets/Action/Knight/sword and shield run.fbx";

        // ── Base (default) action clips — overridden per role below ──────────
        private const string BaseAttackFbx = "Assets/Action/Knight/standing melee combo attack ver. 1.fbx";
        private const string BaseCastFbx   = "Assets/Action/Spell Cast.fbx";

        // ── Per-role action clips (swapped via AnimatorOverrideController) ────
        // Mage — casts spells.
        private const string MageAttackFbx = "Assets/Action/Spell Cast.fbx";
        private const string MageCastFbx   = "Assets/Action/Spell Cast.fbx";
        private const string MageIdleFbx   = "Assets/Action/Orc Idle.fbx";
        // Warrior — sword swings.
        private const string WarriorAttackFbx = "Assets/Action/Knight/standing melee combo attack ver. 1.fbx";
        private const string WarriorCastFbx   = "Assets/Action/Sword And Shield Attack.fbx";
        private const string WarriorIdleFbx   = "Assets/Action/Orc Idle.fbx";
        // Tank — shield idle, taunt, heavy downward attack.
        private const string TankAttackFbx = "Assets/Action/Knight/standing melee attack downward.fbx";
        private const string TankCastFbx   = "Assets/Action/Knight/standing taunt battlecry.fbx";
        private const string TankIdleFbx   = "Assets/Action/Knight/sword and shield idle.fbx";

        // ── ANTI-CHOP crossfades (owner 2026-07-02 "enemy anims off/choppy") ──
        // The v1 transitions cut in at 0.05–0.08s — a visible snap-pop on every
        // attack/hit/cast entry, and a 0.10–0.12s snap back into locomotion. Polished
        // bands: actions blend IN over 0.10s (still reads snappy) and blend BACK into
        // locomotion over 0.20s (the 0.15–0.25 locomotion band). Named so the live
        // OrcHumanoid.controller yaml and this factory stay in lock-step on regen.
        private const float ActionBlendIn = 0.10f; // Any -> Attack/WindUp/Cast/Hit
        private const float LocoBlendBack = 0.20f; // action state -> Locomotion return
        private const float DeathBlendIn  = 0.15f; // Any -> Dead (soften the collapse)

        // Override asset paths (each IS a RuntimeAnimatorController — Resources.Load works).
        private const string MageOverridePath    = "Assets/Resources/Enemies/OrcHumanoid_Mage.controller";
        private const string WarriorOverridePath = "Assets/Resources/Enemies/OrcHumanoid_Warrior.controller";
        private const string TankOverridePath    = "Assets/Resources/Enemies/OrcHumanoid_Tank.controller";

        // State / clip names captured on the BASE so overrides can key into them.
        private static AnimationClip s_idle, s_walk, s_run, s_hit, s_death, s_windup;
        private static AnimationClip s_baseAttack, s_baseCast;
        private static AnimationClip s_combatWalk, s_combatRun;

        [MenuItem("Defenders/Tripo/Build Orc Humanoid Family Controllers (WO-491)")]
        public static void Run()
        {
            // Registry-wrapped picks (WO-670): keyword resolve against `orc` with the
            // hardcoded clip as the terminal default (empty registry ⇒ identical output).
            s_idle   = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Idle,    Clip(IdleFbx));
            s_walk   = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Walk,    Clip(WalkFbx));
            s_run    = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Run,     Clip(RunFbx));
            s_hit    = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Hit,     Clip(HitFbx));
            s_death  = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Death0,  Clip(DeathFbx));
            s_windup = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.WindUp,  Clip(WindUpFbx));
            s_baseAttack = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Attack0, Clip(BaseAttackFbx));
            s_baseCast   = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.Cast,    Clip(BaseCastFbx));

            var injIdle = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.InjuredIdle, Clip(InjuredIdleFbx));
            var injWalk = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.InjuredWalk, Clip(InjuredWalkFbx));
            var injRun  = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.InjuredRun,  Clip(InjuredRunFbx));

            if (s_idle == null) { Debug.LogError("[OrcCtrl] no idle clip - aborting."); return; }

            // ── Build the BASE controller from scratch (idempotent) ──────────
            AssetDatabase.DeleteAsset(BasePath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(BasePath);

            // Canonical AnimParams set (WO-284) so ActorAnimator's guarded verbs all resolve.
            ctrl.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            ctrl.AddParameter("InCombat",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Cast",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("CastVariant", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("WindUp",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",         AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitDir",      AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Dead",        AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("DeathDir",    AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Injured",     AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;

            // ── Locomotion: 1-D blend tree on Speed (idle@0 / walk@1.5 / run@3.5) ──
            // ORC-SPEED-TUNED thresholds (NOT hero-tuned): orcs move 2.09-3.04 m/s
            // post-multiplier, so the old hero values (walk@6 / run@9) were never
            // crossed and the orc stayed in Idle while moving -> slide. walk@1.5f
            // covers the whole orc range; run@3.5f catches the fast end / the 6.3 m/s
            // chase rep. Owner can felt-tune these named values.
            // CRITICAL: useAutomaticThresholds=false so the explicit thresholds stick
            // (Unity defaults true and overwrites them, which skips walk). Mirrors
            // HeroAnimatorFactory.cs:216-238 — the proven blend that fixes the slide.
            var locoState = sm.AddState("Locomotion");
            sm.defaultState = locoState;
            var loco = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D,
                                       blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(loco, ctrl);
            locoState.motion = loco;
            int locoChildren = 0;
            if (s_idle != null) { loco.AddChild(s_idle, 0f);   locoChildren++; }
            if (s_walk != null) { loco.AddChild(s_walk, 1.5f); locoChildren++; } // orc-speed-tuned walk threshold
            if (s_run  != null) { loco.AddChild(s_run,  3.5f); locoChildren++; } // orc-speed-tuned run threshold
            if (locoChildren == 0)
                Debug.LogWarning("[OrcCtrl] no locomotion clips - Locomotion state is empty.");
            ApplyOrcLocomotionCadence(loco);

            // ── Injured locomotion: a SECOND 1-D blend tree on Speed, entered
            //    when Injured==true and returned when Injured==false. The wounded
            //    stance idle/limp/stagger when the orc is below the low-HP cutoff. ─
            var injuredState = sm.AddState("InjuredLocomotion");
            var injTree = new BlendTree { name = "InjuredLocomotion", blendType = BlendTreeType.Simple1D,
                                          blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(injTree, ctrl);
            injuredState.motion = injTree;
            int injChildren = 0;
            // ORC-SPEED-TUNED thresholds (NOT hero-tuned) — same idle@0 / walk@1.5 /
            // run@3.5 as healthy locomotion so the wounded orc reaches walk too.
            if (injIdle != null) { injTree.AddChild(injIdle, 0f);   injChildren++; }
            if (injWalk != null) { injTree.AddChild(injWalk, 1.5f); injChildren++; }
            if (injRun  != null) { injTree.AddChild(injRun,  3.5f); injChildren++; }
            if (injChildren == 0)
            {
                // No injured clips — fall back to the healthy locomotion clips so the
                // state is never empty (Injured then reads identical to Locomotion).
                if (s_idle != null) injTree.AddChild(s_idle, 0f);
                if (s_walk != null) injTree.AddChild(s_walk, 1.5f); // orc-speed-tuned
                if (s_run  != null) injTree.AddChild(s_run,  3.5f); // orc-speed-tuned
                Debug.LogWarning("[OrcCtrl] no injured clips - InjuredLocomotion falls back to healthy loco.");
            }
            ApplyOrcLocomotionCadence(injTree);
            // Loco <-> InjuredLocomotion on the Injured bool.
            var toInjured = locoState.AddTransition(injuredState);
            toInjured.hasExitTime = false; toInjured.duration = 0.2f;
            toInjured.AddCondition(AnimatorConditionMode.If, 0f, "Injured");
            var fromInjured = injuredState.AddTransition(locoState);
            fromInjured.hasExitTime = false; fromInjured.duration = 0.2f;
            fromInjured.AddCondition(AnimatorConditionMode.IfNot, 0f, "Injured");

            // ── CombatLocomotion: braced idle + weapon walk/run (InCombat bool) ──
            var combatIdle = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.CombatIdle, Clip(CombatIdleFbx));
            var combatWalk = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.CombatWalk, Clip(CombatWalkFbx));
            var combatRun  = MotionCastings.Resolve(CastingTargetBase, ActionKeywords.CombatRun,  Clip(CombatRunFbx));
            s_combatWalk = combatWalk;
            s_combatRun  = combatRun;
            AnimatorState combatLocoState = null;
            if (combatIdle != null)
            {
                combatLocoState = sm.AddState("CombatLocomotion");
                var cblend = new BlendTree { name = "CombatLocomotion", blendType = BlendTreeType.Simple1D,
                                             blendParameter = "Speed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(cblend, ctrl);
                combatLocoState.motion = cblend;
                cblend.AddChild(combatIdle, 0f);
                if (combatWalk != null) cblend.AddChild(combatWalk, 1.5f);
                else if (s_walk != null) cblend.AddChild(s_walk, 1.5f);
                if (combatRun != null) cblend.AddChild(combatRun, 3.5f);
                else if (s_run != null) cblend.AddChild(s_run, 3.5f);
                ApplyOrcLocomotionCadence(cblend);

                var toCombat = locoState.AddTransition(combatLocoState);
                toCombat.hasExitTime = false; toCombat.duration = 0.25f;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
                var toCalm = combatLocoState.AddTransition(locoState);
                toCalm.hasExitTime = false; toCalm.duration = 0.25f;
                toCalm.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            }

            // ── Attack — Any -> Attack on the trigger, returns to Locomotion ──
            var sAtk = sm.AddState("Attack");
            sAtk.motion = s_baseAttack != null ? s_baseAttack : s_idle;
            var toAtk = sm.AddAnyStateTransition(sAtk);
            toAtk.hasExitTime = false; toAtk.duration = ActionBlendIn; toAtk.canTransitionToSelf = false;
            toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            AddActionReturn(sAtk, locoState, combatLocoState, 0.8f, LocoBlendBack);

            // ── WindUp telegraph — Any -> WindUp on the trigger, returns to Loco ─
            var sWindUp = sm.AddState("WindUp");
            sWindUp.motion = s_windup != null ? s_windup : s_idle;
            var toWind = sm.AddAnyStateTransition(sWindUp);
            toWind.hasExitTime = false; toWind.duration = ActionBlendIn; toWind.canTransitionToSelf = false;
            toWind.AddCondition(AnimatorConditionMode.If, 0f, "WindUp");
            AddActionReturn(sWindUp, locoState, combatLocoState, 0.85f, LocoBlendBack);

            // ── Cast — Any -> Cast on the trigger, returns to Locomotion ──────
            var sCast = sm.AddState("Cast");
            sCast.motion = s_baseCast != null ? s_baseCast : (s_baseAttack != null ? s_baseAttack : s_idle);
            var toCast = sm.AddAnyStateTransition(sCast);
            toCast.hasExitTime = false; toCast.duration = ActionBlendIn; toCast.canTransitionToSelf = false;
            toCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
            AddActionReturn(sCast, locoState, combatLocoState, 0.85f, LocoBlendBack);

            // ── Hit — Any -> Hit on the trigger, returns to Locomotion ───────
            var sHit = sm.AddState("Hit");
            sHit.motion = s_hit != null ? s_hit : s_idle;
            var toHit = sm.AddAnyStateTransition(sHit);
            toHit.hasExitTime = false; toHit.duration = ActionBlendIn; toHit.canTransitionToSelf = false;
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            AddActionReturn(sHit, locoState, combatLocoState, 0.8f, LocoBlendBack);

            // ── Dead — Any -> Dead on the bool (latched, no return) ──────────
            var sDead = sm.AddState("Dead");
            sDead.motion = s_death != null ? s_death : s_idle;
            var toDead = sm.AddAnyStateTransition(sDead);
            toDead.hasExitTime = false; toDead.duration = DeathBlendIn; toDead.canTransitionToSelf = false;
            toDead.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ── Per-role AnimatorOverrideControllers (clip swap on shared states) ─
            int mage = BuildOverride(ctrl, MageOverridePath, CastingTargetMage, MageIdleFbx, MageAttackFbx, MageCastFbx);
            int warr = BuildOverride(ctrl, WarriorOverridePath, CastingTargetWarrior, WarriorIdleFbx, WarriorAttackFbx, WarriorCastFbx);
            int tank = BuildOverride(ctrl, TankOverridePath, CastingTargetTank, TankIdleFbx, TankAttackFbx, TankCastFbx);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OrcCtrl] ORC_CTRL_OK base='{BasePath}' loco={locoChildren} (idle={Name(s_idle)},walk={Name(s_walk)},run={Name(s_run)}) " +
                      $"injured={injChildren} cast={Name(s_baseCast)} windup={Name(s_windup)} | overrides mage={mage} warrior={warr} tank={tank}");
        }

        /// <summary>
        /// Per-child timeScale on orc locomotion blend trees — matches agent travel speed
        /// (~1.5–3.5 m/s) so feet don't skate at run threshold. Mirrors hero cadence bake.
        /// </summary>
        private static void ApplyOrcLocomotionCadence(BlendTree tree)
        {
            if (tree == null) return;
            var kids = tree.children;
            for (int i = 0; i < kids.Length; i++)
            {
                float th = kids[i].threshold;
                kids[i].timeScale = th >= 3f ? 1.75f : th >= 1.2f ? 1.35f : 1f;
            }
            tree.children = kids;
        }

        /// <summary>
        /// Builds one AnimatorOverrideController over the base, swapping the idle /
        /// attack / cast clips with the role's clips. Locomotion / hit / death stay
        /// shared (only the action clips differ per role). Returns the count of clips
        /// actually overridden (0 = all role clips missing -> base clips kept).
        /// </summary>
        private static int BuildOverride(AnimatorController baseCtrl, string path,
                                         string castingTarget, string idleFbx, string attackFbx, string castFbx)
        {
            // Registry wrap (WO-670): the role target (orc-mage/-warrior/-tank,
            // inherits orc) resolves first; the role's hardcoded clip stays terminal.
            var roleIdle   = MotionCastings.Resolve(castingTarget, ActionKeywords.Idle,    Clip(idleFbx));
            var roleAttack = MotionCastings.Resolve(castingTarget, ActionKeywords.Attack0, Clip(attackFbx));
            var roleCast   = MotionCastings.Resolve(castingTarget, ActionKeywords.Cast,    Clip(castFbx));
            // Per-role locomotion (orc-tank: S&S mocap walk/run — less Mixamo hip sway on CC_Base bulk).
            var roleWalk       = MotionCastings.Resolve(castingTarget, ActionKeywords.Walk,       s_walk);
            var roleRun        = MotionCastings.Resolve(castingTarget, ActionKeywords.Run,        s_run);
            var roleCombatWalk = MotionCastings.Resolve(castingTarget, ActionKeywords.CombatWalk, s_combatWalk);
            var roleCombatRun  = MotionCastings.Resolve(castingTarget, ActionKeywords.CombatRun,  s_combatRun);

            AssetDatabase.DeleteAsset(path);
            var ovr = new AnimatorOverrideController(baseCtrl) { name = System.IO.Path.GetFileNameWithoutExtension(path) };

            int swapped = 0;
            // Remap BASE clip instances to role clips (idle/attack/cast + optional loco overrides).
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            if (roleIdle   != null && s_idle       != null && roleIdle   != s_idle)       { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_idle, roleIdle)); swapped++; }
            if (roleAttack != null && s_baseAttack != null && roleAttack != s_baseAttack) { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_baseAttack, roleAttack)); swapped++; }
            if (roleCast   != null && s_baseCast   != null && roleCast   != s_baseCast)   { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_baseCast, roleCast)); swapped++; }
            if (roleWalk       != null && s_walk       != null && roleWalk       != s_walk)       { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_walk, roleWalk)); swapped++; }
            if (roleRun        != null && s_run        != null && roleRun        != s_run)        { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_run, roleRun)); swapped++; }
            if (roleCombatWalk != null && s_combatWalk != null && roleCombatWalk != s_combatWalk) { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_combatWalk, roleCombatWalk)); swapped++; }
            if (roleCombatRun  != null && s_combatRun  != null && roleCombatRun  != s_combatRun)  { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_combatRun, roleCombatRun)); swapped++; }
            if (pairs.Count > 0) ovr.ApplyOverrides(pairs);

            AssetDatabase.CreateAsset(ovr, path);
            EditorUtility.SetDirty(ovr);
            return swapped;
        }

        private static void AddActionReturn(AnimatorState state, AnimatorState locoState,
                                            AnimatorState combatLocoState, float exitTime, float duration)
        {
            if (combatLocoState != null)
            {
                var toCombat = state.AddTransition(combatLocoState);
                toCombat.hasExitTime = true; toCombat.exitTime = exitTime; toCombat.duration = duration;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            }
            var toCalm = state.AddTransition(locoState);
            toCalm.hasExitTime = true; toCalm.exitTime = exitTime; toCalm.duration = duration;
        }

        private static string Name(AnimationClip c) => c != null ? c.name : "<idle>";

        private static AnimationClip Clip(string fbx)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            Debug.LogWarning($"[OrcCtrl] no clip in {fbx}");
            return null;
        }
    }
}
