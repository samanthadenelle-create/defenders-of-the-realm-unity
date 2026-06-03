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

namespace DeNelle.Village
{
    /// <summary>Rig family of an enemy mesh — selects which shared controller to apply.</summary>
    public enum EnemyRig { HumanoidMedium, HumanoidLarge, Boss, Dragon, OrcWarband }

    public static class EnemyAnimatorFactory
    {
        /// <summary>Resolves the rig family for a Resources/Enemies model name.</summary>
        public static EnemyRig RigFor(string modelName)
        {
            switch (modelName)
            {
                case "Skeleton_Golem": return EnemyRig.HumanoidLarge;
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
                case EnemyRig.HumanoidLarge: return "LargeEnemy";
                case EnemyRig.Boss:          return "Boss";
                case EnemyRig.Dragon:        return "Dragon";
                case EnemyRig.OrcWarband:    return "OrcWarband";   // DEF-221 Humanoid orc controller
                default:                     return "HumanoidEnemy";
            }
        }

        /// <summary>Stamps the shared controller for <paramref name="modelName"/> onto
        /// <paramref name="visual"/>'s Animator (adds one if missing). Root motion off —
        /// the NavMesh agent / air glide drives position; the clip just animates limbs.
        /// No-op-safe if the controller is absent (run EnemyAnimatorSetup to build it).</summary>
        public static void Apply(GameObject visual, string modelName)
        {
            if (visual == null) return;
            var anim = visual.GetComponentInChildren<Animator>() ?? visual.AddComponent<Animator>();
            anim.applyRootMotion = false;

            var ctrl = Resources.Load<RuntimeAnimatorController>("Enemies/" + Controller(RigFor(modelName)));
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
        }
    }
}
