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
        private const string TypeCameraFollow = "DeNelle.Village.Defend.HeroOverShoulderCamera";
        private const string TypeEventSystem = "UnityEngine.EventSystems.EventSystem";
        private const string TypeInputSystemUIInputModule = "UnityEngine.InputSystem.UI.InputSystemUIInputModule";

        private const string PanelSettingsPath =
            "Assets/_Modules/Village/PatriciaLight/Generated/PatriciaLightPanelSettings.asset";

        private const string Tower2Path = "Assets/Resources/PatriciaLight/tower2.fbx";
        private const string TypeTripoFixer = "DeNelle.Core.TripoMaterialFixer";

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

            // ── Bake the arena as REAL, editable scene objects ───────────────
            // (floor + tower2 + wooden stand + hero-spawn marker). Previously the
            // controller built these at runtime, so nothing showed in the editor
            // Hierarchy. Baking them lets the owner place/rotate/scale by eye.
            BakeArena();

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
        //  Arena bake — real, editable scene geometry
        // =====================================================================

        /// <summary>
        /// Bakes the Defend-the-Tower arena into the scene as real GameObjects under
        /// a "DefendTowerArena" root: a solid floor slab, the tower2 structure, a
        /// raised wooden stand set back facing the tower, and a HeroSpawn marker.
        /// The owner refines placement/rotation/scale by eye; PatriciaLightController
        /// detects these and reuses them instead of building at runtime.
        /// </summary>
        private static void BakeArena()
        {
            var arena = new GameObject("DefendTowerArena");

            // ── Solid floor slab (top just above y=0 so it overdraws any terrain) ─
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena Ground";
            floor.transform.SetParent(arena.transform, false);
            floor.transform.position   = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(240f, 0.2f, 240f);
            TintEditor(floor, new Color(0.22f, 0.24f, 0.20f));

            // ── Tower2 (the defended structure) at origin ────────────────────
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Tower2Path);
            if (fbx != null)
            {
                var tower = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                tower.name = "Tower";
                tower.transform.SetParent(arena.transform, false);
                tower.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // starting guess — refine in editor
                FitLargestDimension(tower, 17f);
                SeatOnGround(tower, Vector3.zero);
                AddByReflection(tower, TypeTripoFixer); // Tripo->URP materials at Play
            }
            else
            {
                Debug.LogWarning("[PatriciaLightSceneBuilder] " + Tower2Path + " not found — tower not baked.");
            }

            // ── Raised wooden stand set BACK (+Z), facing the tower ──────────
            const float standTop = 7f;
            const float standZ   = 18f;
            Color wood = new Color(0.45f, 0.30f, 0.16f), woodDark = new Color(0.30f, 0.20f, 0.10f);

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "StandPlatform";
            platform.transform.SetParent(arena.transform, false);
            platform.transform.position   = new Vector3(0f, standTop, standZ);
            platform.transform.localScale = new Vector3(7f, 0.5f, 5f);
            TintEditor(platform, wood);

            foreach (var c in new[] { new Vector2(-3f, -2f), new Vector2(3f, -2f),
                                      new Vector2(-3f,  2f), new Vector2(3f,  2f) })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "StandLeg";
                leg.transform.SetParent(arena.transform, false);
                leg.transform.position   = new Vector3(c.x, standTop * 0.5f, standZ + c.y);
                leg.transform.localScale = new Vector3(0.4f, standTop, 0.4f);
                TintEditor(leg, woodDark);
            }

            // ── Side boundary walls (LEFT/RIGHT edges) ───────────────────────
            // No front rail: it was purely cosmetic and its invisible collider was
            // the likely cause of "half the shots fire backwards" — gone now.
            // These low side walls sit on the ±X edges so the hero reads as standing
            // on a framed platform (not floating in air) without occluding the
            // forward view of the tower. Colliders stripped — nothing invisible in
            // front of the hero to deflect a shot.
            foreach (float sx in new[] { -3.5f, 3.5f })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "StandSideWall";
                wall.transform.SetParent(arena.transform, false);
                wall.transform.position   = new Vector3(sx, standTop + 0.55f, standZ);
                wall.transform.localScale = new Vector3(0.3f, 1.1f, 5f);
                TintEditor(wall, woodDark);
                var wc = wall.GetComponent<Collider>();
                if (wc != null) UnityEngine.Object.DestroyImmediate(wc);
                // Invisible: the strafe clamp (StrafeHalfWidth) is what actually keeps
                // the hero on the platform — these stay as edge markers but don't render.
                var wr = wall.GetComponent<MeshRenderer>();
                if (wr != null) wr.enabled = false;
            }

            // ── Hero spawn marker on the stand, facing the tower ─────────────
            var spawn = new GameObject("HeroSpawn");
            spawn.transform.SetParent(arena.transform, false);
            spawn.transform.position = new Vector3(0f, standTop + 0.35f, standZ - 1.3f);
            Vector3 toTower = Vector3.zero - spawn.transform.position; toTower.y = 0f;
            spawn.transform.rotation = toTower.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toTower.normalized, Vector3.up)
                : Quaternion.identity;
        }

        /// <summary>Scales an object uniformly so its largest world-bounds dimension
        /// equals <paramref name="target"/> — robust to import scale / orientation.</summary>
        private static void FitLargestDimension(GameObject go, float target)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (max < 0.0001f) return;
            go.transform.localScale *= target / max;
        }

        /// <summary>Shifts an object so its bounds base sits at <paramref name="basePos"/>.y
        /// and it is centred on basePos.x/z.</summary>
        private static void SeatOnGround(GameObject go, Vector3 basePos)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(basePos.x - b.center.x, basePos.y - b.min.y, basePos.z - b.center.z);
        }

        private static void TintEditor(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
            r.sharedMaterial = m;
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
