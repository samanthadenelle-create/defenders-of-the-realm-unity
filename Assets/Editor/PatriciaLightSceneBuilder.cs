// =============================================================================
// PatriciaLightSceneBuilder — generates the dedicated "Defend the Tower" scene.
// -----------------------------------------------------------------------------
// WO-47 Phase 2 ("Patricia Light"). One static entry point the main Unity
// session runs (manually via the Defenders menu, or via -executeMethod):
//
//     -executeMethod DeNelle.Editor.PatriciaLightSceneBuilder.BuildScene
//
// It creates a FRESH, simple scene at Assets/Scenes/PatriciaLightMode.unity:
//   • a root GameObject carrying DeNelle.Village.PatriciaLightController
//     (the runtime director — it builds the tower / hero / spawners / pets / HUD
//     at PLAY time, so the saved .unity stays trivially simple);
//   • a Main Camera carrying DeNelle.Village.ThirdPersonCameraFollow (the
//     controller hands it the hero transform at runtime);
//   • a UIDocument host carrying a PanelSettings so the code-built HUD renders
//     in player builds (a null-PanelSettings UIDocument draws nothing — the
//     intro black-screen regression);
//   • an EventSystem + InputSystemUIInputModule so the HUD pet-toggle buttons
//     route pointer clicks.
// Then it registers the scene in EditorBuildSettings so SceneRouter can load it.
//
// SCENE-CORRUPTION NOTE (memory: village-scene-resave-corruption): the corruption
// risk is from RE-SAVING the complex Village scene, NOT from a fresh, simple
// scene like this one — which is exactly why the WO authorises a code-built fresh
// scene here. All the heavy content is instantiated at runtime, never saved.
//
// ASSEMBLY NOTE: DeNelle.Editor.asmdef does NOT reference DeNelle.Village, so the
// controller + camera-follow are added by REFLECTION (full type name) — the same
// pattern VillageSceneBuilder uses. The UIDocument / PanelSettings / EventSystem
// types live in auto-referenced engine modules and ARE directly usable.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that assembles the dedicated Patricia Light (Defend the
    /// Tower) scene. Entry point: <see cref="BuildScene"/>. Idempotent.
    /// </summary>
    public static class PatriciaLightSceneBuilder
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string ScenePath = ScenesDir + "/PatriciaLightMode.unity";

        private const string TypeController = "DeNelle.Village.PatriciaLightController";
        private const string TypeCameraFollow = "DeNelle.Village.ThirdPersonCameraFollow";
        private const string TypeEventSystem = "UnityEngine.EventSystems.EventSystem";
        private const string TypeInputSystemUIInputModule = "UnityEngine.InputSystem.UI.InputSystemUIInputModule";

        private const string PanelSettingsPath =
            "Assets/_Modules/Village/PatriciaLight/Generated/PatriciaLightPanelSettings.asset";

        /// <summary>
        /// Builds the dedicated Patricia Light scene + registers it in Build
        /// Settings. Runnable via
        /// <c>-executeMethod DeNelle.Editor.PatriciaLightSceneBuilder.BuildScene</c>.
        /// </summary>
        [MenuItem("Defenders/Patricia Light/Build Defend-the-Tower Scene")]
        public static void BuildScene()
        {
            EnsureFolder(ScenesDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Main Camera + third-person follow ────────────────────────────
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.09f); // tense night sky
            cam.fieldOfView = 60f;
            cam.farClipPlane = 400f;
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 12f, -14f);
            camGo.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            AddByReflection(camGo, TypeCameraFollow);

            // ── Director root (PatriciaLightController builds everything at runtime) ─
            var dirGo = new GameObject("PatriciaLightController");
            AddByReflection(dirGo, TypeController);

            // ── UI host: UIDocument + PanelSettings (so the code HUD renders) ─
            PanelSettings ps = ResolvePanelSettings();
            var uiGo = new GameObject("PatriciaLightUI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.sortingOrder = 0;

            // ── EventSystem so the HUD buttons route clicks ───────────────────
            EnsureEventSystem();

            // ── Save the fresh scene ─────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // ── Register in Build Settings (so SceneRouter can load it) ──────
            EnsureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool controllerOk = FindType(TypeController) != null;
            bool cameraOk = FindType(TypeCameraFollow) != null;
            Debug.Log(
                "[PatriciaLightSceneBuilder] BuildScene complete -> " + ScenePath +
                $" (controller wired={controllerOk}, cameraFollow wired={cameraOk}, " +
                $"PanelSettings={(ps != null)}). The tower/hero/spawners/pets/HUD are " +
                "instantiated at runtime by PatriciaLightController.");
            if (!controllerOk)
                Debug.LogError("[PatriciaLightSceneBuilder] PatriciaLightController type not found — " +
                               "is DeNelle.Village compiled? The scene root has no director.");
        }

        // =====================================================================
        //  PanelSettings — create (or reuse) an equivalent of the intro asset
        // =====================================================================

        private static PanelSettings ResolvePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(PanelSettingsPath)?.Replace('\\', '/'));

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;

            var themeUss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.modules.uielements/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss");
            if (themeUss != null)
                settings.themeStyleSheet = themeUss;

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }

        // =====================================================================
        //  EventSystem (Input-System UI module)
        // =====================================================================

        private static void EnsureEventSystem()
        {
            var esType = FindType(TypeEventSystem);
            if (esType == null)
            {
                Debug.LogWarning("[PatriciaLightSceneBuilder] EventSystem type not resolvable — " +
                                 "HUD button clicks may not fire.");
                return;
            }
            if (UnityEngine.Object.FindObjectOfType(esType) != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent(esType);
            var moduleType = FindType(TypeInputSystemUIInputModule);
            if (moduleType != null) go.AddComponent(moduleType);
        }

        // =====================================================================
        //  Build Settings registration
        // =====================================================================

        private static void EnsureBuildSettings()
        {
            var current = EditorBuildSettings.scenes;
            foreach (var s in current)
                if (s.path == ScenePath) return; // already registered

            var scenes = new List<EditorBuildSettingsScene>(current)
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =====================================================================
        //  Reflection helpers (DeNelle.Editor cannot reference DeNelle.Village)
        // =====================================================================

        private static Component AddByReflection(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"[PatriciaLightSceneBuilder] Type '{fullTypeName}' not found — " +
                               "is DeNelle.Village compiled? Component skipped.");
                return null;
            }
            return go.AddComponent(type);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
