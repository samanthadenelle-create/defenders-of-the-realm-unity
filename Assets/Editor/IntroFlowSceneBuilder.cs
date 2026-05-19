// =============================================================================
// IntroFlowSceneBuilder — generates the hero-select + pet-select scenes (Editor).
// -----------------------------------------------------------------------------
// The intro flow (owner-acceptance-checklist "Intro & first-run flow") runs:
//
//     Title (studio bumper) -> HeroSelect -> PetSelect -> Village
//
// HeroSelect and PetSelect are two new scenes. This Editor-only builder creates
// them and slots them into Build Settings between Title and Village.
//
// One static entry point — IntroFlowSceneBuilder.BuildAll() — runnable from the
// menu (Defenders/Intro Flow/Build Hero + Pet Select Scenes) or via:
//
//     -executeMethod DeNelle.Editor.IntroFlowSceneBuilder.BuildAll
//
// What it does:
//   1. Creates Assets/Scenes/HeroSelect.unity — a Camera, an EventSystem, and
//      a UIDocument carrying HeroSelectScreen.uxml with a HeroSelectController.
//   2. Creates Assets/Scenes/PetSelect.unity — the same shape with
//      PetSelectScreen.uxml + a PetSelectController.
//   3. Registers both scenes in EditorBuildSettings.scenes, inserted directly
//      AFTER Title and BEFORE Village. The insert is order-aware and idempotent
//      — it never duplicates an entry and never disturbs the other scenes.
//
// IDEMPOTENT — re-running BuildAll() overwrites both scenes cleanly and
// re-inserts the build-settings entries in the right place.
//
// ASSEMBLY NOTE — same constraint as OnboardingSceneBuilder: DeNelle.Editor
// references DeNelle.Core + DeNelle.Data only, NOT DeNelle.Onboarding. So the
// two select-screen controllers are added by REFLECTION (resolved by full type
// name from the loaded DeNelle.Onboarding assembly). The UIDocument /
// PanelSettings / VisualTreeAsset types are in auto-referenced engine modules
// and are used directly. The EventSystem input module is resolved by type name
// so the Editor assembly needs no Input-System dependency either.
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
    /// Editor utility that generates the hero-select and pet-select scenes and
    /// registers them in Build Settings between Title and Village. Entry point:
    /// <see cref="BuildAll"/>.
    /// </summary>
    public static class IntroFlowSceneBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";

        private const string TitleScenePath = ScenesDir + "/Title.unity";
        private const string VillageScenePath = ScenesDir + "/Village.unity";
        private const string HeroSelectScenePath = ScenesDir + "/HeroSelect.unity";
        private const string PetSelectScenePath = ScenesDir + "/PetSelect.unity";

        private const string HeroSelectUxmlPath = "Assets/_Modules/Onboarding/UI/HeroSelectScreen.uxml";
        private const string PetSelectUxmlPath = "Assets/_Modules/Onboarding/UI/PetSelectScreen.uxml";

        // The intro flow reuses the onboarding PanelSettings created by
        // OnboardingSceneBuilder. If it is absent (OnboardingSceneBuilder not
        // yet run) the builder creates an equivalent one at the same path.
        private const string PanelSettingsPath =
            "Assets/_Modules/Onboarding/Generated/OnboardingPanelSettings.asset";

        // ── Onboarding MonoBehaviour type names (resolved by reflection) ─────
        private const string NsOnboarding = "DeNelle.Onboarding";
        private const string TypeHeroSelect = NsOnboarding + ".HeroSelectController";
        private const string TypePetSelect = NsOnboarding + ".PetSelectController";

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the hero-select + pet-select scenes and registers them in
        /// Build Settings between Title and Village. Idempotent.
        /// </summary>
        [MenuItem("Defenders/Intro Flow/Build Hero + Pet Select Scenes")]
        public static void BuildAll()
        {
            EnsureFolder(ScenesDir);

            var panelSettings = ResolvePanelSettings();

            BuildSelectScene(HeroSelectScenePath, "HeroSelectRoot",
                HeroSelectUxmlPath, TypeHeroSelect, panelSettings);
            BuildSelectScene(PetSelectScenePath, "PetSelectRoot",
                PetSelectUxmlPath, TypePetSelect, panelSettings);

            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IntroFlowSceneBuilder] BuildAll complete — HeroSelect + PetSelect scenes " +
                      "generated and registered in Build Settings between Title and Village.");
        }

        // =====================================================================
        //  Select-screen scene builder
        // =====================================================================

        /// <summary>
        /// Builds one select-screen scene: a Camera, an EventSystem, and a
        /// UIDocument carrying <paramref name="uxmlPath"/> with the controller
        /// type <paramref name="controllerTypeName"/> attached.
        /// </summary>
        private static void BuildSelectScene(string scenePath, string rootName,
            string uxmlPath, string controllerTypeName, PanelSettings panelSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();

            // A named root for tidiness / future content.
            // ReSharper disable once ObjectCreationAsStatement
            new GameObject(rootName);

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml == null)
                Debug.LogError($"[IntroFlowSceneBuilder] UXML not found at {uxmlPath} — " +
                               "the select screen will be blank.");

            var docGo = new GameObject("SelectScreen UIDocument");
            var document = docGo.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;

            // The controller lives in DeNelle.Onboarding — added by reflection
            // (DeNelle.Editor cannot reference that assembly). It has a
            // [RequireComponent(typeof(UIDocument))] and falls back to the
            // UIDocument on its own GameObject, so attaching it here is enough.
            var controllerType = FindType(controllerTypeName);
            if (controllerType != null)
                docGo.AddComponent(controllerType);
            else
                Debug.LogError($"[IntroFlowSceneBuilder] Type '{controllerTypeName}' not found — " +
                               "is the DeNelle.Onboarding assembly compiled? Controller skipped.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // =====================================================================
        //  Scene-object factories (mirror OnboardingSceneBuilder)
        // =====================================================================

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 1f, -10f);
        }

        /// <summary>
        /// Creates the EventSystem with an input module resolved by type name —
        /// the new InputSystemUIInputModule if present, else the legacy module.
        /// </summary>
        private static void CreateEventSystem()
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();

            var moduleType =
                FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule") ??
                FindType("UnityEngine.EventSystems.StandaloneInputModule");
            if (moduleType != null)
                eventSystemGo.AddComponent(moduleType);
            else
                Debug.LogWarning("[IntroFlowSceneBuilder] No EventSystem input module type found — " +
                                 "UI input may be inert until one is added.");
        }

        /// <summary>Resolves a Type by full name across every loaded assembly.</summary>
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        // =====================================================================
        //  Build Settings — insert HeroSelect + PetSelect between Title/Village
        // =====================================================================

        /// <summary>
        /// Inserts the two select scenes into <see cref="EditorBuildSettings.scenes"/>
        /// directly after Title and before Village, without duplicating an entry
        /// or disturbing any other scene. Order-aware and idempotent.
        /// </summary>
        private static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Drop any existing entries for the two select scenes — we re-insert
            // them at the correct position below.
            scenes.RemoveAll(s => PathEquals(s.path, HeroSelectScenePath)
                                  || PathEquals(s.path, PetSelectScenePath));

            // Find the slot right after Title (or the front if Title is absent).
            int titleIndex = scenes.FindIndex(s => PathEquals(s.path, TitleScenePath));
            int insertAt = titleIndex >= 0 ? titleIndex + 1 : 0;

            scenes.Insert(insertAt, new EditorBuildSettingsScene(HeroSelectScenePath, true));
            scenes.Insert(insertAt + 1, new EditorBuildSettingsScene(PetSelectScenePath, true));

            // If Village somehow sits before the insert point, the load order
            // would be wrong — log it so the integrator can re-order. (With the
            // canonical OnboardingSceneBuilder list — Title, Village, Dungeon,
            // ATBBattle — this never happens; the inserts land before Village.)
            int villageIndex = scenes.FindIndex(s => PathEquals(s.path, VillageScenePath));
            if (villageIndex >= 0 && villageIndex < insertAt + 2)
                Debug.LogWarning("[IntroFlowSceneBuilder] Village appears before the select " +
                                 "scenes in Build Settings — verify the scene order.");

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool PathEquals(string a, string b)
        {
            return string.Equals(
                a?.Replace('\\', '/'), b?.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        //  PanelSettings — reuse the onboarding asset, or create an equivalent
        // =====================================================================

        private static PanelSettings ResolvePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            // OnboardingSceneBuilder normally creates this. If it has not run,
            // create an equivalent so the select scenes still render.
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

        // ── Folder helper ────────────────────────────────────────────────────
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
