// =============================================================================
// BuildOrcHumanoidController — WO-481 slice 2b: a HUMANOID animator controller
// for the new Tripo Orcs (they're Humanoid; the KayKit enemy controllers are
// Generic and won't drive them). Idle/Attack/Hit/Dead from Assets/Action Humanoid
// clips, driven by the params ActorAnimator sets (InCombat/Attack/Hit/Dead). Saved
// to Resources/Enemies/OrcHumanoid.controller so AtbCombatantSwapper.ApplyEnemy-
// Animator can Resources.Load it (slice 2c maps the orcs to it).
//
//   run-unity-method.ps1 -Method DeNelle.Editor.BuildOrcHumanoidController.Run -LogName orc-controller.log
// =============================================================================

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BuildOrcHumanoidController
    {
        private const string Path = "Assets/Resources/Enemies/OrcHumanoid.controller";
        private const string IdleFbx   = "Assets/Action/Orc Idle.fbx";
        private const string AttackFbx = "Assets/Action/Knight/standing melee attack horizontal.fbx"; // humanoid, placeholder swing
        private const string HitFbx    = "Assets/Action/Shared/Shared_Hit_Reaction.fbx";
        private const string DeathFbx  = "Assets/Action/Shared/Shared_Death.fbx";

        [MenuItem("Defenders/Tripo/Build Orc Humanoid Controller (WO-481 2b)")]
        public static void Run()
        {
            var idle  = Clip(IdleFbx);
            var atk   = Clip(AttackFbx);
            var hit   = Clip(HitFbx);
            var death = Clip(DeathFbx);
            if (idle == null) { Debug.LogError("[OrcCtrl] no idle clip — aborting."); return; }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Path);
            ctrl.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead",     AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle");   sIdle.motion = idle;  sm.defaultState = sIdle;
            var sAtk  = sm.AddState("Attack");  sAtk.motion  = atk   != null ? atk   : idle;
            var sHit  = sm.AddState("Hit");     sHit.motion  = hit   != null ? hit   : idle;
            var sDead = sm.AddState("Dead");    sDead.motion = death != null ? death : idle;

            // Idle -> Attack (on trigger), Attack -> Idle (on exit)
            var a1 = sIdle.AddTransition(sAtk); a1.hasExitTime = false; a1.duration = 0.08f; a1.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            var a2 = sAtk.AddTransition(sIdle); a2.hasExitTime = true; a2.exitTime = 0.85f; a2.duration = 0.12f;

            // AnyState -> Hit (on trigger), Hit -> Idle (on exit)
            var h1 = sm.AddAnyStateTransition(sHit); h1.hasExitTime = false; h1.duration = 0.05f; h1.canTransitionToSelf = false; h1.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            var h2 = sHit.AddTransition(sIdle); h2.hasExitTime = true; h2.exitTime = 0.8f; h2.duration = 0.12f;

            // AnyState -> Dead (on bool)
            var d1 = sm.AddAnyStateTransition(sDead); d1.hasExitTime = false; d1.duration = 0.1f; d1.canTransitionToSelf = false; d1.AddCondition(AnimatorConditionMode.If, 0, "Dead");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OrcCtrl] ORC_CONTROLLER_OK -> {Path}  (idle={idle.name}, attack={(atk!=null?atk.name:"<idle>")}, hit={(hit!=null?hit.name:"<idle>")}, death={(death!=null?death.name:"<idle>")})");
        }

        private static AnimationClip Clip(string fbx)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            Debug.LogWarning($"[OrcCtrl] no clip in {fbx}");
            return null;
        }
    }
}
