// =============================================================================
// PanelSettingsRepair — repairs UIDocuments left with a null PanelSettings.
// -----------------------------------------------------------------------------
// The intro scenes (Title / HeroSelect / PetSelect) were generated with their
// UIDocuments' m_PanelSettings serialized as {fileID: 0} — a null reference.
// A UIDocument with no PanelSettings creates no render panel, so its UI never
// draws (the screen stays black). This pass opens each intro scene, assigns the
// OnboardingPanelSettings asset to every UIDocument that is missing one, and
// saves the scene.
//
// Run headless (repair + build in one batchmode session):
//   Unity.exe -batchmode -quit -buildTarget Win64 -projectPath <proj> \
//     -executeMethod DeNelle.Editor.PanelSettingsRepair.RepairAndBuild
// =============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility: assigns the OnboardingPanelSettings asset to any intro-scene
    /// UIDocument whose PanelSettings reference is null, so the UI can render.
    /// </summary>
    public static class PanelSettingsRepair
    {
        private const string OnboardingPanelSettingsPath =
            "Assets/_Modules/Onboarding/Generated/OnboardingPanelSettings.asset";

        private static readonly string[] IntroScenes =
        {
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/HeroSelect.unity",
            "Assets/Scenes/PetSelect.unity",
        };

        /// <summary>Assigns the OnboardingPanelSettings to every PanelSettings-less UIDocument in the intro scenes.</summary>
        [MenuItem("Defenders/Onboarding/Repair Intro PanelSettings")]
        public static void RepairIntroPanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(OnboardingPanelSettingsPath);
            if (ps == null)
            {
                Debug.LogError($"[PanelSettingsRepair] PanelSettings asset not found at " +
                               $"{OnboardingPanelSettingsPath} — cannot repair.");
                return;
            }

            foreach (string scenePath in IntroScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int total = 0, repaired = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var doc in root.GetComponentsInChildren<UIDocument>(true))
                    {
                        total++;
                        // Write the serialized m_PanelSettings field directly via
                        // SerializedObject — the panelSettings *property* setter
                        // does not reliably persist into a saved scene from an
                        // editor script (this is why the scene builders' own
                        // assignments were lost).
                        var so = new SerializedObject(doc);
                        var prop = so.FindProperty("m_PanelSettings");
                        if (prop != null && prop.objectReferenceValue == null)
                        {
                            prop.objectReferenceValue = ps;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            repaired++;
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[PanelSettingsRepair] {scenePath}: {repaired}/{total} UIDocument(s) repaired.");
            }
        }

        /// <summary>Repairs the intro-scene PanelSettings, then builds the Windows player.</summary>
        public static void RepairAndBuild()
        {
            RepairIntroPanelSettings();
            DesktopBuild.BuildWindows();
        }
    }
}
