// =============================================================================
// RaidSceneRegistrar — registers the first-playable RaidBase_* scenes into
// EditorBuildSettings so SceneRouter.IsSceneRegistered (Application.CanStreamed
// LevelBeLoaded) passes for SceneRouter.GoRaid (WO-453 Step 4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)
//
// The three first-playable raid scenes exist on disk (Assets/Scenes/RaidBase_*.unity)
// but were NOT in EditorBuildSettings, so a GoRaid load would be rejected. This adds
// them idempotently — on editor load ([InitializeOnLoad]) AND via a manual menu — so
// the raid entry works in-editor and in player builds without a hand-edit of the
// EditorBuildSettings.asset. Mirrors BattleSceneBuilder.RegisterBuildSettings.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;

namespace DeNelle.Editor
{
    /// <summary>
    /// Idempotently adds the first-playable <c>RaidBase_*</c> scenes to
    /// EditorBuildSettings (auto on editor load + a manual menu item).
    /// </summary>
    [InitializeOnLoad]
    public static class RaidSceneRegistrar
    {
        private static readonly string[] RaidScenePaths =
        {
            "Assets/Scenes/RaidBase_raider_camp_small.unity",
            "Assets/Scenes/RaidBase_fortified_garrison.unity",
            "Assets/Scenes/RaidBase_mage_enclave.unity",
        };

        static RaidSceneRegistrar()
        {
            // Defer to first idle tick so we don't mutate build settings mid-import.
            EditorApplication.delayCall += () => Register(verbose: false);
        }

        [MenuItem("Defenders/Build/Register Raid Scenes")]
        public static void RegisterMenu() => Register(verbose: true);

        private static void Register(bool verbose)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = false;

            foreach (var path in RaidScenePaths)
            {
                // Only register a scene that actually exists on disk.
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                {
                    if (verbose) UnityEngine.Debug.LogWarning($"[RaidSceneRegistrar] missing scene '{path}' — skipped.");
                    continue;
                }

                bool present = scenes.Exists(s =>
                    string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase));
                if (present) continue;

                scenes.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
                if (verbose) UnityEngine.Debug.Log($"[RaidSceneRegistrar] added '{path}' to Build Settings.");
            }

            if (changed)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
                if (verbose) UnityEngine.Debug.Log("[RaidSceneRegistrar] Build Settings updated with raid scenes.");
            }
            else if (verbose)
            {
                UnityEngine.Debug.Log("[RaidSceneRegistrar] all raid scenes already registered — no change.");
            }
        }
    }
}
