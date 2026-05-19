// =============================================================================
// BattleSceneBuilder — Week-2 ATB battle scene generator (Editor-only)
// -----------------------------------------------------------------------------
// One static entry point — BattleSceneBuilder.BuildBattleScene() — that the
// main Unity session runs (manually or via the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.BattleSceneBuilder.BuildBattleScene
//
// What it does (v2 port-spec Part 5, Week 2):
//   Assembles Assets/Scenes/ATBBattle.unity:
//     • a Main Camera framed on the two combatants,
//     • a Directional Light,
//     • a ground plane,
//     • a placeholder HERO capsule (left) and ENEMY capsule (right),
//     • a UIDocument carrying BattleHUD.uxml + a generated PanelSettings,
//     • an EventSystem (input module resolved by type name),
//     • the BattleController MonoBehaviour, with every [SerializeField] wired.
//   Then registers ATBBattle.unity in EditorBuildSettings (added if absent).
//
// IDEMPOTENT — re-running cleanly overwrites the scene and the generated
// PanelSettings + ATBRuntimeState assets, and re-registers Build Settings
// without duplicating the entry. Safe to run repeatedly.
//
// ASSEMBLY NOTE. The DeNelle.Editor.asmdef references only DeNelle.Core +
// DeNelle.Data — NOT DeNelle.BattleATB. So this builder must NOT take a
// compile-time dependency on the BattleATB assembly. Like
// OnboardingSceneBuilder, it works entirely by REFLECTION:
//   * The BattleController MonoBehaviour and the ATBRuntimeState ScriptableObject
//     are resolved by full type name from the loaded DeNelle.BattleATB assembly,
//     created via ScriptableObject.CreateInstance(Type) / AddComponent(Type),
//     and their [SerializeField] fields are set through SerializedObject (which
//     needs only string field names).
// Everything else (Camera, Light, UIDocument, PanelSettings, EventSystem,
// primitives) is in auto-referenced engine modules and is used directly.
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

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that builds the Week-2 ATB battle scene. Entry point:
    /// <see cref="BuildBattleScene"/>.
    /// </summary>
    public static class BattleSceneBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";
        private const string GeneratedDir = "Assets/_Modules/BattleATB/Generated";

        private const string BattleScenePath = ScenesDir + "/ATBBattle.unity";
        private const string BattleHudUxmlPath = "Assets/_Modules/BattleATB/UI/BattleHUD.uxml";

        private const string PanelSettingsPath = GeneratedDir + "/BattlePanelSettings.asset";
        private const string RuntimeStatePath = GeneratedDir + "/ATBRuntimeState.asset";

        // ── BattleATB type names (resolved by reflection) ───────────────────
        private const string NsBattleAtb = "DeNelle.BattleATB";
        private const string TypeBattleController = NsBattleAtb + ".BattleController";
        private const string TypeRuntimeState = NsBattleAtb + ".State.ATBRuntimeState";

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds <c>ATBBattle.unity</c> and registers it in Build Settings.
        /// Runnable via
        /// <c>-executeMethod DeNelle.Editor.BattleSceneBuilder.BuildBattleScene</c>.
        /// Idempotent — re-running overwrites the scene + generated assets cleanly.
        /// </summary>
        [MenuItem("Defenders/Week 2/Build ATB Battle Scene")]
        public static void BuildBattleScene()
        {
            EnsureFolder(ScenesDir);
            EnsureFolder(GeneratedDir);

            PanelSettings panelSettings = CreatePanelSettings();
            ScriptableObject runtimeState = CreateRuntimeState();

            BuildScene(panelSettings, runtimeState);
            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BattleSceneBuilder] BuildBattleScene complete — ATBBattle.unity assembled and registered.");
        }

        // =====================================================================
        //  Scene assembly
        // =====================================================================

        private static void BuildScene(PanelSettings panelSettings, ScriptableObject runtimeState)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── A named scene root for the battle content ───────────────────
            var root = new GameObject("ATBBattleRoot");

            // ── Camera — framed on the two combatants ───────────────────────
            CreateCamera();

            // ── Directional light ───────────────────────────────────────────
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            lightGo.transform.SetParent(root.transform);
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            // ── Ground plane ────────────────────────────────────────────────
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            TintRenderer(ground, new Color(0.14f, 0.12f, 0.18f));

            // ── Placeholder combatant capsules ──────────────────────────────
            // Hero on the left (party), enemy on the right — facing each other.
            var heroCapsule = CreateCombatantCapsule(
                root.transform, "HeroCapsule",
                new Vector3(-2.4f, 1f, 0f),
                new Color(0.49f, 0.23f, 0.93f), // the Heart's violet
                90f);
            var enemyCapsule = CreateCombatantCapsule(
                root.transform, "EnemyCapsule",
                new Vector3(2.4f, 1f, 0f),
                new Color(0.74f, 0.19f, 0.27f), // crimson
                -90f);

            // ── EventSystem (UI input) ──────────────────────────────────────
            CreateEventSystem();

            // ── BattleHUD UIDocument ────────────────────────────────────────
            var battleHud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BattleHudUxmlPath);
            if (battleHud == null)
                Debug.LogError($"[BattleSceneBuilder] BattleHUD UXML not found at {BattleHudUxmlPath}.");

            var hudGo = new GameObject("BattleHUD UIDocument");
            hudGo.transform.SetParent(root.transform);
            var hudDocument = hudGo.AddComponent<UIDocument>();
            hudDocument.panelSettings = panelSettings;
            hudDocument.visualTreeAsset = battleHud;

            // ── BattleController (orchestrator) ─────────────────────────────
            // The controller requires a UIDocument; placing it on the same
            // GameObject satisfies [RequireComponent(typeof(UIDocument))].
            Component controller = AddBattleComponent(hudGo, TypeBattleController);
            if (controller != null)
            {
                WireFields(controller, new Dictionary<string, UnityEngine.Object>
                {
                    { "_runtimeState", runtimeState },
                    { "_hudDocument", hudDocument },
                    { "_heroCapsule", heroCapsule.transform },
                    { "_enemyCapsule", enemyCapsule.transform },
                });
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        // =====================================================================
        //  Scene-object factories
        // =====================================================================

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.05f, 0.09f);
            camera.fieldOfView = 55f;
            cameraGo.tag = "MainCamera";
            // Slightly elevated, pulled back, looking down on the two capsules.
            cameraGo.transform.position = new Vector3(0f, 3.4f, -7.5f);
            cameraGo.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            cameraGo.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Creates a placeholder capsule combatant: a primitive capsule, tinted,
        /// rotated to face its opponent. The capsule's Collider is removed — the
        /// ATB battle is engine-driven, not physics-driven.
        /// </summary>
        private static GameObject CreateCombatantCapsule(
            Transform parent, string name, Vector3 position, Color tint, float facingY)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = name;
            capsule.transform.SetParent(parent);
            capsule.transform.position = position;
            capsule.transform.localRotation = Quaternion.Euler(0f, facingY, 0f);
            TintRenderer(capsule, tint);

            var collider = capsule.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            return capsule;
        }

        /// <summary>
        /// Creates the EventSystem. The input module is resolved by type name so
        /// the Editor assembly needs no compile-time dependency on the Input
        /// System package.
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
                Debug.LogWarning("[BattleSceneBuilder] No EventSystem input module type found — UI input may be inert until one is added.");
        }

        /// <summary>Apply a flat URP-lit tint to a primitive's renderer.</summary>
        private static void TintRenderer(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // Use the URP Lit shader when present; fall back to the default.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            var mat = new Material(shader) { color = color };
            renderer.sharedMaterial = mat;
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

            var themeUss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.modules.uielements/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss");
            if (themeUss != null)
                settings.themeStyleSheet = themeUss;

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }

        /// <summary>
        /// Creates (or reuses) the runtime-only ATBRuntimeState ScriptableObject
        /// asset. The type is resolved by reflection — the Editor assembly does
        /// not reference DeNelle.BattleATB.
        /// </summary>
        private static ScriptableObject CreateRuntimeState()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RuntimeStatePath);
            if (existing != null) return existing;

            var type = FindType(TypeRuntimeState);
            if (type == null)
            {
                Debug.LogError($"[BattleSceneBuilder] Type '{TypeRuntimeState}' not found — is the DeNelle.BattleATB assembly compiled? Runtime state not created.");
                return null;
            }

            var instance = ScriptableObject.CreateInstance(type);
            instance.name = "ATBRuntimeState";
            AssetDatabase.CreateAsset(instance, RuntimeStatePath);
            return instance;
        }

        // =====================================================================
        //  Reflection helpers — BattleATB components & field wiring
        // =====================================================================

        /// <summary>
        /// Adds a BattleATB MonoBehaviour (resolved by full type name from the
        /// DeNelle.BattleATB assembly) to <paramref name="go"/>.
        /// </summary>
        private static Component AddBattleComponent(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"[BattleSceneBuilder] Type '{fullTypeName}' not found — is the DeNelle.BattleATB assembly compiled? Component skipped.");
                return null;
            }
            return go.AddComponent(type);
        }

        /// <summary>
        /// Sets a set of private <c>[SerializeField]</c> object-reference fields on
        /// <paramref name="target"/> via <see cref="SerializedObject"/> — no
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
                    Debug.LogError($"[BattleSceneBuilder] Serialized field '{kv.Key}' not found on {target.GetType().Name} — wiring skipped for that field.");
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
        //  Build Settings registration
        // =====================================================================

        /// <summary>
        /// Ensures <c>ATBBattle.unity</c> is in EditorBuildSettings. If the scene
        /// list already contains it, the list is left untouched; otherwise the
        /// battle scene is appended. Idempotent.
        /// </summary>
        private static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool present = scenes.Exists(s =>
                string.Equals(s.path, BattleScenePath, StringComparison.OrdinalIgnoreCase));

            if (!present)
            {
                scenes.Add(new EditorBuildSettingsScene(BattleScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        // =====================================================================
        //  Folder helper
        // =====================================================================

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
