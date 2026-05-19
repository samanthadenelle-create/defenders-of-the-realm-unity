// =============================================================================
// OnboardingSceneBuilder — Week-1 scene generator (Editor-only)
// -----------------------------------------------------------------------------
// One static entry point — OnboardingSceneBuilder.BuildAll() — that the main
// Unity session runs (manually or via the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.OnboardingSceneBuilder.BuildAll
//
// What it does (v2 port-spec Part 5, Week 1):
//   1. Creates the four canonical scenes under Assets/Scenes/:
//        Title.unity                  — fully built (see below)
//        Village.unity                — near-empty (Camera + Light + named root)
//        Dungeon_HealersCottage.unity — near-empty
//        ATBBattle.unity              — near-empty
//   2. Builds the Title scene: Camera, EventSystem, a UIDocument carrying the
//      Title UXML, a VideoPlayer (+ RenderTexture) for the studio bumper, and
//      the three onboarding controllers (TitleController / SplashLoading /
//      StoryIntroController) with their serialized fields wired.
//   3. Registers all four scenes in EditorBuildSettings.scenes, Title first.
//
// IDEMPOTENT — re-running BuildAll() cleanly overwrites every generated scene
// and the generated RenderTexture/PanelSettings assets, then re-registers the
// build-settings list from scratch. Safe to run repeatedly.
//
// ASSEMBLY NOTE. The DeNelle.Editor.asmdef is fixed (the task forbids editing
// asmdefs) and references only DeNelle.Core + DeNelle.Data — NOT
// DeNelle.Onboarding, and not the Input System package. So this builder must
// NOT take a compile-time dependency on either:
//   * The onboarding MonoBehaviours (TitleController / SplashLoading /
//     StoryIntroController) are added and wired by REFLECTION — resolved by
//     full type name from the loaded DeNelle.Onboarding assembly, attached via
//     GameObject.AddComponent(Type), and their private [SerializeField] fields
//     set through SerializedObject (which needs only string field names).
//   * The EventSystem input module is resolved by type name too — the new
//     Input System module if the package is present, else the legacy module.
// Everything else (UIDocument, PanelSettings, VideoPlayer, VisualTreeAsset,
// RenderTexture) is in auto-referenced engine modules, so it is used directly.
//
// This script does NOT run itself; the main session triggers it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that generates the four Week-1 scenes and fully builds the
    /// Title scene. Entry point: <see cref="BuildAll"/>.
    /// </summary>
    public static class OnboardingSceneBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";
        private const string GeneratedDir = "Assets/_Modules/Onboarding/Generated";

        private const string TitleScenePath = ScenesDir + "/Title.unity";
        private const string VillageScenePath = ScenesDir + "/Village.unity";
        private const string DungeonScenePath = ScenesDir + "/Dungeon_HealersCottage.unity";
        private const string BattleScenePath = ScenesDir + "/ATBBattle.unity";

        private const string TitleUxmlPath = "Assets/_Modules/Onboarding/UI/TitleScreen.uxml";
        private const string HeartWingPath = "Assets/_Modules/Onboarding/Art/heart-wing.jpg";
        private const string BumperVideoPath = "Assets/_Modules/Onboarding/Video/studio-bumper.mp4";

        private const string PanelSettingsPath = GeneratedDir + "/OnboardingPanelSettings.asset";
        private const string BumperTexturePath = GeneratedDir + "/StudioBumperRT.renderTexture";

        // ── Onboarding MonoBehaviour type names (resolved by reflection) ─────
        private const string NsOnboarding = "DeNelle.Onboarding";
        private const string TypeTitleController = NsOnboarding + ".TitleController";
        private const string TypeSplashLoading = NsOnboarding + ".SplashLoading";
        private const string TypeStoryIntro = NsOnboarding + ".StoryIntroController";

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds all four Week-1 scenes and registers them in Build Settings.
        /// Runnable via <c>-executeMethod DeNelle.Editor.OnboardingSceneBuilder.BuildAll</c>.
        /// Idempotent — re-running overwrites cleanly.
        /// </summary>
        [MenuItem("Defenders/Week 1/Build Onboarding Scenes")]
        public static void BuildAll()
        {
            EnsureFolder(ScenesDir);
            EnsureFolder(GeneratedDir);

            // Shared generated assets (created once, reused / overwritten).
            var panelSettings = CreatePanelSettings();
            var bumperTexture = CreateBumperRenderTexture();

            BuildTitleScene(panelSettings, bumperTexture);
            BuildEmptyScene(VillageScenePath, "VillageRoot");
            BuildEmptyScene(DungeonScenePath, "DungeonRoot");
            BuildEmptyScene(BattleScenePath, "ATBBattleRoot");

            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OnboardingSceneBuilder] BuildAll complete — 4 scenes generated, Title fully built, Build Settings registered.");
        }

        // =====================================================================
        //  Title scene — fully built
        // =====================================================================

        private static void BuildTitleScene(PanelSettings panelSettings, RenderTexture bumperTexture)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Camera ───────────────────────────────────────────────────────
            CreateCamera();

            // ── EventSystem (UI input) ───────────────────────────────────────
            CreateEventSystem();

            // ── Title screen UIDocument ──────────────────────────────────────
            var titleUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TitleUxmlPath);
            if (titleUxml == null)
                Debug.LogError($"[OnboardingSceneBuilder] Title UXML not found at {TitleUxmlPath}.");

            var titleDocGo = new GameObject("TitleScreen UIDocument");
            var titleDocument = titleDocGo.AddComponent<UIDocument>();
            titleDocument.panelSettings = panelSettings;
            titleDocument.visualTreeAsset = titleUxml;

            // ── Studio bumper VideoPlayer ────────────────────────────────────
            var bumperClip = AssetDatabase.LoadAssetAtPath<VideoClip>(BumperVideoPath);
            if (bumperClip == null)
                Debug.LogWarning($"[OnboardingSceneBuilder] Studio bumper clip not found at {BumperVideoPath} — SplashLoading will use the static fallback card.");

            var videoGo = new GameObject("StudioBumper VideoPlayer");
            var videoPlayer = videoGo.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = bumperClip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = bumperTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.waitForFirstFrame = true;

            // ── SplashLoading (studio bumper) — own UIDocument ───────────────
            var splashGo = new GameObject("SplashLoading");
            var splashDoc = splashGo.AddComponent<UIDocument>();
            splashDoc.panelSettings = panelSettings;
            splashDoc.sortingOrder = 20; // above the title screen
            var splash = AddOnboardingComponent(splashGo, TypeSplashLoading);
            if (splash != null)
                WireFields(splash, new Dictionary<string, UnityEngine.Object>
                {
                    { "_videoPlayer", videoPlayer },
                    { "_videoTexture", bumperTexture },
                });

            // ── StoryIntroController (cold open) — own UIDocument ────────────
            var storyGo = new GameObject("StoryIntro");
            var storyDoc = storyGo.AddComponent<UIDocument>();
            storyDoc.panelSettings = panelSettings;
            storyDoc.sortingOrder = 15; // above the title, below the bumper
            var storyIntro = AddOnboardingComponent(storyGo, TypeStoryIntro);

            // ── TitleController (orchestrator) ───────────────────────────────
            var heartWing = AssetDatabase.LoadAssetAtPath<Sprite>(HeartWingPath);
            if (heartWing == null)
                Debug.LogWarning($"[OnboardingSceneBuilder] Heart-Wing sprite not found at {HeartWingPath} — the banner will be blank. Ensure heart-wing.jpg is imported with Texture Type = Sprite.");

            var titleControllerGo = new GameObject("TitleController");
            var titleController = AddOnboardingComponent(titleControllerGo, TypeTitleController);
            if (titleController != null)
                WireFields(titleController, new Dictionary<string, UnityEngine.Object>
                {
                    { "_splash", splash },
                    { "_storyIntro", storyIntro },
                    { "_titleDocument", titleDocument },
                    { "_heartWingBanner", heartWing },
                });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        // =====================================================================
        //  Near-empty scenes (Village / Dungeon / ATBBattle)
        // =====================================================================

        private static void BuildEmptyScene(string scenePath, string rootName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // A named root for the scene's future content (Week 3+).
            // ReSharper disable once ObjectCreationAsStatement
            new GameObject(rootName);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // =====================================================================
        //  Scene-object factories
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
        /// Creates the EventSystem. The input module is resolved by type name so
        /// the Editor assembly needs no compile-time dependency on the Input
        /// System package: the new InputSystemUIInputModule is used if present,
        /// otherwise the legacy StandaloneInputModule.
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
                Debug.LogWarning("[OnboardingSceneBuilder] No EventSystem input module type found — UI input may be inert until one is added.");
        }

        // =====================================================================
        //  Reflection helpers — onboarding components & field wiring
        // =====================================================================

        /// <summary>
        /// Adds an onboarding MonoBehaviour (resolved by full type name from the
        /// DeNelle.Onboarding assembly) to <paramref name="go"/>.
        /// </summary>
        private static Component AddOnboardingComponent(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"[OnboardingSceneBuilder] Type '{fullTypeName}' not found — is the DeNelle.Onboarding assembly compiled? Component skipped.");
                return null;
            }
            return go.AddComponent(type);
        }

        /// <summary>
        /// Sets a set of private <c>[SerializeField]</c> object-reference fields
        /// on <paramref name="target"/> via <see cref="SerializedObject"/> — no
        /// compile-time type dependency required.
        /// </summary>
        private static void WireFields(Component target, Dictionary<string, UnityEngine.Object> fields)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            foreach (var kv in fields)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop == null)
                {
                    Debug.LogError($"[OnboardingSceneBuilder] Serialized field '{kv.Key}' not found on {target.GetType().Name} — wiring skipped for that field.");
                    continue;
                }
                prop.objectReferenceValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
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
        //  Build Settings registration — Title first
        // =====================================================================

        private static void RegisterBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(VillageScenePath, true),
                new EditorBuildSettingsScene(DungeonScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }

        // =====================================================================
        //  Generated shared assets
        // =====================================================================

        private static PanelSettings CreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;

            // Apply the UI Toolkit default runtime theme if available so the
            // title screen has a base stylesheet (the project Theme.uss tokens
            // are applied per-document by Unity once imported).
            var themeUss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.modules.uielements/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss");
            if (themeUss != null)
                settings.themeStyleSheet = themeUss;

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }

        private static RenderTexture CreateBumperRenderTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(BumperTexturePath);
            if (existing != null) return existing;

            var rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
            {
                name = "StudioBumperRT",
            };
            AssetDatabase.CreateAsset(rt, BumperTexturePath);
            return rt;
        }

        // ── Folder helper ────────────────────────────────────────────────────
        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
