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
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BuildOrcHumanoidController
    {
        private const string BasePath = "Assets/Resources/Enemies/OrcHumanoid.controller";

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

        // Override asset paths (each IS a RuntimeAnimatorController — Resources.Load works).
        private const string MageOverridePath    = "Assets/Resources/Enemies/OrcHumanoid_Mage.controller";
        private const string WarriorOverridePath = "Assets/Resources/Enemies/OrcHumanoid_Warrior.controller";
        private const string TankOverridePath    = "Assets/Resources/Enemies/OrcHumanoid_Tank.controller";

        // State / clip names captured on the BASE so overrides can key into them.
        private static AnimationClip s_idle, s_walk, s_run, s_hit, s_death, s_windup;
        private static AnimationClip s_baseAttack, s_baseCast;

        [MenuItem("Defenders/Tripo/Build Orc Humanoid Family Controllers (WO-491)")]
        public static void Run()
        {
            s_idle   = Clip(IdleFbx);
            s_walk   = Clip(WalkFbx);
            s_run    = Clip(RunFbx);
            s_hit    = Clip(HitFbx);
            s_death  = Clip(DeathFbx);
            s_windup = Clip(WindUpFbx);
            s_baseAttack = Clip(BaseAttackFbx);
            s_baseCast   = Clip(BaseCastFbx);

            var injIdle = Clip(InjuredIdleFbx);
            var injWalk = Clip(InjuredWalkFbx);
            var injRun  = Clip(InjuredRunFbx);

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

            // ── Locomotion: 1-D blend tree on Speed (idle@0 / walk@6 / run@9) ──
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
            if (s_idle != null) { loco.AddChild(s_idle, 0f); locoChildren++; }
            if (s_walk != null) { loco.AddChild(s_walk, 6f); locoChildren++; }
            if (s_run  != null) { loco.AddChild(s_run,  9f); locoChildren++; }
            if (locoChildren == 0)
                Debug.LogWarning("[OrcCtrl] no locomotion clips - Locomotion state is empty.");

            // ── Injured locomotion: a SECOND 1-D blend tree on Speed, entered
            //    when Injured==true and returned when Injured==false. The wounded
            //    stance idle/limp/stagger when the orc is below the low-HP cutoff. ─
            var injuredState = sm.AddState("InjuredLocomotion");
            var injTree = new BlendTree { name = "InjuredLocomotion", blendType = BlendTreeType.Simple1D,
                                          blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(injTree, ctrl);
            injuredState.motion = injTree;
            int injChildren = 0;
            if (injIdle != null) { injTree.AddChild(injIdle, 0f); injChildren++; }
            if (injWalk != null) { injTree.AddChild(injWalk, 6f); injChildren++; }
            if (injRun  != null) { injTree.AddChild(injRun,  9f); injChildren++; }
            if (injChildren == 0)
            {
                // No injured clips — fall back to the healthy locomotion clips so the
                // state is never empty (Injured then reads identical to Locomotion).
                if (s_idle != null) injTree.AddChild(s_idle, 0f);
                if (s_walk != null) injTree.AddChild(s_walk, 6f);
                if (s_run  != null) injTree.AddChild(s_run,  9f);
                Debug.LogWarning("[OrcCtrl] no injured clips - InjuredLocomotion falls back to healthy loco.");
            }
            // Loco <-> InjuredLocomotion on the Injured bool.
            var toInjured = locoState.AddTransition(injuredState);
            toInjured.hasExitTime = false; toInjured.duration = 0.2f;
            toInjured.AddCondition(AnimatorConditionMode.If, 0f, "Injured");
            var fromInjured = injuredState.AddTransition(locoState);
            fromInjured.hasExitTime = false; fromInjured.duration = 0.2f;
            fromInjured.AddCondition(AnimatorConditionMode.IfNot, 0f, "Injured");

            // ── Attack — Any -> Attack on the trigger, returns to Locomotion ──
            var sAtk = sm.AddState("Attack");
            sAtk.motion = s_baseAttack != null ? s_baseAttack : s_idle;
            var toAtk = sm.AddAnyStateTransition(sAtk);
            toAtk.hasExitTime = false; toAtk.duration = 0.08f; toAtk.canTransitionToSelf = false;
            toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var atkBack = sAtk.AddTransition(locoState);
            atkBack.hasExitTime = true; atkBack.exitTime = 0.8f; atkBack.duration = 0.12f;

            // ── WindUp telegraph — Any -> WindUp on the trigger, returns to Loco ─
            // The readable wind-up pose before a cast / heavy attack lands.
            var sWindUp = sm.AddState("WindUp");
            sWindUp.motion = s_windup != null ? s_windup : s_idle;
            var toWind = sm.AddAnyStateTransition(sWindUp);
            toWind.hasExitTime = false; toWind.duration = 0.05f; toWind.canTransitionToSelf = false;
            toWind.AddCondition(AnimatorConditionMode.If, 0f, "WindUp");
            var windBack = sWindUp.AddTransition(locoState);
            windBack.hasExitTime = true; windBack.exitTime = 0.85f; windBack.duration = 0.1f;

            // ── Cast — Any -> Cast on the trigger, returns to Locomotion ──────
            // The mage spell-cast state (the WindUp telegraph fires just before it).
            var sCast = sm.AddState("Cast");
            sCast.motion = s_baseCast != null ? s_baseCast : (s_baseAttack != null ? s_baseAttack : s_idle);
            var toCast = sm.AddAnyStateTransition(sCast);
            toCast.hasExitTime = false; toCast.duration = 0.06f; toCast.canTransitionToSelf = false;
            toCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
            var castBack = sCast.AddTransition(locoState);
            castBack.hasExitTime = true; castBack.exitTime = 0.85f; castBack.duration = 0.12f;

            // ── Hit — Any -> Hit on the trigger, returns to Locomotion ───────
            var sHit = sm.AddState("Hit");
            sHit.motion = s_hit != null ? s_hit : s_idle;
            var toHit = sm.AddAnyStateTransition(sHit);
            toHit.hasExitTime = false; toHit.duration = 0.05f; toHit.canTransitionToSelf = false;
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            var hitBack = sHit.AddTransition(locoState);
            hitBack.hasExitTime = true; hitBack.exitTime = 0.8f; hitBack.duration = 0.12f;

            // ── Dead — Any -> Dead on the bool (latched, no return) ──────────
            var sDead = sm.AddState("Dead");
            sDead.motion = s_death != null ? s_death : s_idle;
            var toDead = sm.AddAnyStateTransition(sDead);
            toDead.hasExitTime = false; toDead.duration = 0.1f; toDead.canTransitionToSelf = false;
            toDead.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ── Per-role AnimatorOverrideControllers (clip swap on shared states) ─
            int mage = BuildOverride(ctrl, MageOverridePath, MageIdleFbx, MageAttackFbx, MageCastFbx);
            int warr = BuildOverride(ctrl, WarriorOverridePath, WarriorIdleFbx, WarriorAttackFbx, WarriorCastFbx);
            int tank = BuildOverride(ctrl, TankOverridePath, TankIdleFbx, TankAttackFbx, TankCastFbx);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OrcCtrl] ORC_CTRL_OK base='{BasePath}' loco={locoChildren} (idle={Name(s_idle)},walk={Name(s_walk)},run={Name(s_run)}) " +
                      $"injured={injChildren} cast={Name(s_baseCast)} windup={Name(s_windup)} | overrides mage={mage} warrior={warr} tank={tank}");
        }

        /// <summary>
        /// Builds one AnimatorOverrideController over the base, swapping the idle /
        /// attack / cast clips with the role's clips. Locomotion / hit / death stay
        /// shared (only the action clips differ per role). Returns the count of clips
        /// actually overridden (0 = all role clips missing -> base clips kept).
        /// </summary>
        private static int BuildOverride(AnimatorController baseCtrl, string path,
                                         string idleFbx, string attackFbx, string castFbx)
        {
            var roleIdle   = Clip(idleFbx);
            var roleAttack = Clip(attackFbx);
            var roleCast   = Clip(castFbx);

            AssetDatabase.DeleteAsset(path);
            var ovr = new AnimatorOverrideController(baseCtrl) { name = System.IO.Path.GetFileNameWithoutExtension(path) };

            int swapped = 0;
            // Remap the BASE clip instances to the role clips wherever they appear in
            // the override list (locomotion uses s_idle/walk/run; we only swap idle so
            // the standing-idle reads per role; walk/run stay the shared loco clips).
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            if (roleIdle   != null && s_idle       != null && roleIdle   != s_idle)       { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_idle, roleIdle)); swapped++; }
            if (roleAttack != null && s_baseAttack != null && roleAttack != s_baseAttack) { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_baseAttack, roleAttack)); swapped++; }
            if (roleCast   != null && s_baseCast   != null && roleCast   != s_baseCast)   { pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(s_baseCast, roleCast)); swapped++; }
            if (pairs.Count > 0) ovr.ApplyOverrides(pairs);

            AssetDatabase.CreateAsset(ovr, path);
            EditorUtility.SetDirty(ovr);
            return swapped;
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
