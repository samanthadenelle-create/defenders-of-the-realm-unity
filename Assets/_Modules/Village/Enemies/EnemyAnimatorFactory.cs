// =============================================================================
// EnemyAnimatorFactory — one entry point that puts the right SHARED animator
// controller on any enemy mesh (human OR animal), for the runtime-built arena.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The canonical AnimatorSetup (docs/enemy-codex.md §5) builds a tiny set of
// SHARED controllers from KayKit's shared rigs — HumanoidEnemy (Rig_Medium),
// LargeEnemy (Rig_Large), Boss, Dragon — each wired with idle/move/attack/hit/
// death and the exact params Enemy.cs already drives (Speed/Attack/Hit/Dead).
// EnemyAnimatorSetup copies those controllers into Resources/Enemies so this
// runtime factory can load them (the editor pipeline outputs to Generated/).
//
// So animating an enemy is one call: pick the controller by rig family and stamp
// it on the mesh. Enemy.cs's DriveAnimator then makes it walk/attack/die for free.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Rig family of an enemy mesh — selects which shared controller to apply.</summary>
    public enum EnemyRig { HumanoidMedium, HumanoidLarge, Boss, Dragon, OrcWarband, LargeHumanoid }

    public static class EnemyAnimatorFactory
    {
        /// <summary>Resolves the rig family for a Resources/Enemies model name.</summary>
        public static EnemyRig RigFor(string modelName)
        {
            switch (modelName)
            {
                case "Skeleton_Golem": return EnemyRig.HumanoidLarge;
                // WO-445: the big brutes (Cave Troll, Demon, OgreMage) are AccuRIG /
                // Character-Creator HUMANOID rigs (FBX animationType: Humanoid, CC_Base_*
                // bones). They were previously routed to the LargeEnemy controller, but
                // LargeEnemy's clips come from KayKit's GENERIC Rig_Large skeleton — a
                // Generic clip cannot retarget onto a Humanoid avatar, so the brutes
                // T-posed / slid (the WO-445 symptom). They now share the LargeHumanoid
                // controller, built from the Mixamo Assets/Action Humanoid clip library
                // (same retargetable source the OrcWarband family uses) so the brute's
                // Humanoid avatar drives idle/walk/attack/death correctly.
                case "Troll":
                case "Demon":
                case "OgreMage":       return EnemyRig.LargeHumanoid;
                case "Necromancer":    return EnemyRig.Boss;
                case "Dragon":         return EnemyRig.Dragon;
                // DEF-221: the orc family is HUMANOID (Tripo), so it CANNOT use the
                // KayKit Generic HumanoidEnemy controller — it gets its own.
                case "Orc_Berserker":
                case "Orc_Shaman":
                case "Orc_Necromancer": return EnemyRig.OrcWarband;
                default:               return EnemyRig.HumanoidMedium; // Warrior/Minion/Rogue/Mage
            }
        }

        private static string Controller(EnemyRig rig)
        {
            switch (rig)
            {
                case EnemyRig.HumanoidLarge:  return "LargeEnemy";
                case EnemyRig.Boss:           return "Boss";
                case EnemyRig.Dragon:         return "Dragon";
                case EnemyRig.OrcWarband:     return "OrcWarband";    // DEF-221 Humanoid orc controller
                case EnemyRig.LargeHumanoid:  return "LargeHumanoid"; // WO-445 Humanoid brute controller (Troll/Demon/OgreMage)
                default:                      return "HumanoidEnemy";
            }
        }

        /// <summary>Stamps the shared controller for <paramref name="modelName"/> onto
        /// <paramref name="visual"/>'s Animator (adds one if missing). Root motion off —
        /// the NavMesh agent / air glide drives position; the clip just animates limbs.
        /// No-op-safe if the controller is absent (run EnemyAnimatorSetup to build it).</summary>
        public static void Apply(GameObject visual, string modelName)
        {
            if (visual == null) return;
            var anim = visual.GetComponentInChildren<Animator>();
            if (anim == null) anim = visual.AddComponent<Animator>();
            anim.applyRootMotion = false;

            // WO-53 (perf): off-screen animator culling. EVERY enemy in the project is
            // built through EnemyFactory.Build -> EnemyAnimatorFactory.Apply (the single
            // enemy-creation path), so setting it ONCE here culls every wave/roaming/
            // tribe/outpost/garrison enemy at spawn — no per-call-site edits needed.
            // CullUpdateTransforms keeps the state machine (and any anim events Enemy.cs
            // drives) running while skipping transform/mesh writes when off-camera; we do
            // NOT use CullCompletely (it can desync gameplay-driven anim events).
            if (anim != null) anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            EnemyRig rig = RigFor(modelName);
            string ctrlName = Controller(rig);
            var ctrl = Resources.Load<RuntimeAnimatorController>("Enemies/" + ctrlName);
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                FlowTrace.Once("Enemy", $"anim-{modelName}",
                    $"animator: model '{modelName}' -> rig {rig} -> controller '{ctrlName}' OK");
            }
            else
            {
                FlowTrace.Warn("Enemy",
                    $"animator: model '{modelName}' -> rig {rig} -> controller 'Enemies/{ctrlName}' " +
                    "MISSING — enemy has no walk/attack/die anim (run EnemyAnimatorSetup)");
            }
        }
    }
}
