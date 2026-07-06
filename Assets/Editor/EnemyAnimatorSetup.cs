// =============================================================================
// EnemyAnimatorSetup — prepares the Defend-the-Tower RUNTIME enemies' animation.
// -----------------------------------------------------------------------------
// The canonical AnimatorSetup (docs/enemy-codex.md §5) builds the SHARED character
// controllers (HumanoidEnemy / LargeEnemy / Boss / …) into Assets/Generated/
// Animators/ — designed for editor-assigned prefabs. The DTT arena builds its
// enemies at RUNTIME, so this pass:
//   1. gives the KayKit skeleton meshes an Avatar (Generic rigs need one to be
//      driven by a clip — without it they're "sliding statues"),
//   2. runs the canonical AnimatorSetup.BuildAnimators(),
//   3. copies the controllers the runtime arena uses into Resources/Enemies so
//      EnemyAnimatorFactory can Resources.Load them.
//
// Enemy.cs already drives Speed/Attack/Hit/Dead on whatever controller is applied,
// so once a controller is on the mesh the enemy walks/attacks/dies for free.
//
// Run headless: -executeMethod DeNelle.Editor.EnemyAnimatorSetup.Setup
//
// ── WO-217 / WO-218 NOTE ─────────────────────────────────────────────────────
// WO-217 (snappier enemy attacks) is implemented in AnimatorSetup.BuildHumanoid-
// Controller (Attack/Cast state.speed = 1.15 + earlier return-to-idle), because
// the actual enemy attack STATES live in the canonical controllers this script
// merely copies — there is nothing attack-related to tune here.
// WO-218 (upper-body attack layer) is SKIPPED for enemies on purpose: the KayKit
// enemies are GENERIC rigs (EnsureAvatar sets animationType = Generic). Humanoid
// AvatarMasks (SetHumanoidBodyPartActive) only apply to Humanoid avatars, so a
// humanoid upper-body mask would not mask a Generic rig correctly — it would risk
// breaking enemy locomotion/attack for no gain. Enemy layering is therefore left
// out; only the hero (Humanoid) gets the WO-218 upper-body layer.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class EnemyAnimatorSetup
    {
        private const string Res = "Assets/Resources/Enemies/";
        private const string Gen = "Assets/Generated/Animators/";

        // KayKit skeleton meshes still on the Generic Rig_Medium clip library.
        // AccuRig family (Mage/Warrior/Rogue/Healer) is Humanoid — run
        // PeopleCharacterImporter.ImportSkeletonFamily instead.
        private static readonly string[] KayKitSkeletonMeshes =
            { "Skeleton_Minion", "Skeleton_Golem", "Necromancer" };

        // Shared controllers the runtime DTT arena loads (copied into Resources).
        private static readonly string[] RuntimeControllers =
            { "HumanoidEnemy", "LargeEnemy", "Boss", "Dragon" };

        [MenuItem("Defenders/Animation/Setup Enemy Animators (DTT)")]
        public static void Setup()
        {
            // 1) Avatars for Generic playback.
            foreach (var s in KayKitSkeletonMeshes) EnsureAvatar(Res + s + ".fbx");

            // 2) Build the canonical shared controllers (Generated/Animators).
            AnimatorSetup.BuildAnimators();

            // 3) Copy the runtime ones into Resources/Enemies for the factory.
            foreach (var c in RuntimeControllers)
            {
                string src = Gen + c + ".controller";
                string dst = Res + c + ".controller";
                if (AssetImporter.GetAtPath(src) == null)
                {
                    Debug.LogWarning("[EnemyAnimatorSetup] canonical controller missing (skipped): " + src);
                    continue;
                }
                AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(src, dst))
                    Debug.LogWarning("[EnemyAnimatorSetup] copy failed: " + src + " -> " + dst);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyAnimatorSetup] done — skeleton avatars set; canonical controllers " +
                      "copied to Resources/Enemies (" + string.Join(", ", RuntimeControllers) + ").");
        }

        /// <summary>Generic rig + an Avatar generated from the mesh so clips can drive it.</summary>
        private static void EnsureAvatar(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[EnemyAnimatorSetup] no importer: " + fbxPath); return; }
            imp.animationType   = ModelImporterAnimationType.Generic;
            imp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;   // mesh FBX carries no clips; clips come from the shared rig
            imp.SaveAndReimport();
        }
    }
}
