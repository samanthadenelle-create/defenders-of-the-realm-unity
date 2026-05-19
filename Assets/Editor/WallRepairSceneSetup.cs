// =============================================================================
// WallRepairSceneSetup — adds the player wall-repair mechanic to the village
// scene (Workstream B, scene wiring).
// -----------------------------------------------------------------------------
// The workstream brief forbids editing VillageSceneBuilder.cs. This is the
// separate editor file it asks for: a menu command that drops the wall-repair
// systems into the ALREADY-BUILT Village scene, without touching the builder.
//
// WHAT IT ADDS (under VillageRoot/GameplaySystems):
//   1. A "VillageHud" UIDocument GameObject hosting VillageHud.uxml + the
//      DeNelle.HUD.VillageHudController — IF the scene does not already have
//      one. (VillageSceneBuilder builds the BuildMenu UIDocument but not the
//      HUD; the repair prompt lives in the HUD, so it is created here.)
//   2. A "WallRepair" GameObject carrying:
//        - DeNelle.Village.WallRepairController  (the repair interaction loop)
//        - DeNelle.Village.WallRepairHudBridge   (the Village<->HUD glue)
//      The bridge's _controller + _hud references are wired so PromptShown /
//      PromptHidden / FeedbackShown reach the HUD and the HUD's Repair / Cancel
//      buttons reach the controller.
//
// REFLECTION: the DeNelle.Editor asmdef references neither DeNelle.Village nor
// DeNelle.HUD (module isolation, port spec Part 2). Every gameplay / HUD type is
// therefore touched by full-name reflection — the same approach
// VillageSceneBuilder uses.
//
// IDEMPOTENT: re-running the command removes the previously-added "WallRepair"
// object (and a HUD object it itself created) before re-adding, so it is safe
// to run after a scene rebuild.
//
// RUN IT: Unity menu  Defenders > Workstream B > Add Wall-Repair To Village.
// The integrator should run this once after the village scene is (re)built.
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that wires the Workstream-B wall-repair mechanic into the
    /// pre-built Village scene. Adds the repair controller, the HUD bridge and
    /// (when missing) the village HUD UIDocument, all by reflection so the
    /// editor assembly keeps its module isolation.
    /// </summary>
    public static class WallRepairSceneSetup
    {
        private const string VillageScenePath = "Assets/Scenes/Village.unity";
        private const string VillageHudUxmlPath = "Assets/_Modules/HUD/VillageHud.uxml";

        private const string VillageRootName = "VillageRoot";
        private const string GameplaySystemsName = "GameplaySystems";
        private const string WallRepairObjectName = "WallRepair";
        private const string HudObjectName = "VillageHud";

        // Full type names — resolved by reflection (no asmdef reference).
        private const string TypeWallRepairController = "DeNelle.Village.WallRepairController";
        private const string TypeWallRepairHudBridge = "DeNelle.Village.WallRepairHudBridge";
        private const string TypeVillageHudController = "DeNelle.HUD.VillageHudController";

        // =====================================================================
        //  Menu command
        // =====================================================================

        [MenuItem("Defenders/Workstream B/Add Wall-Repair To Village")]
        public static void AddWallRepairToVillage()
        {
            // 1) Open the village scene (or use it if already open).
            Scene scene = EnsureVillageSceneOpen();
            if (!scene.IsValid())
            {
                Debug.LogError($"[WallRepairSceneSetup] Could not open '{VillageScenePath}'. " +
                               "Build the village scene first (Defenders > Week 3 > Build Village Scene).");
                return;
            }

            // 2) Locate the systems root the builder created.
            Transform systemsRoot = FindGameplaySystemsRoot();
            if (systemsRoot == null)
            {
                Debug.LogError("[WallRepairSceneSetup] No 'VillageRoot/GameplaySystems' found — " +
                               "is this a built village scene? Aborting.");
                return;
            }

            // 3) Resolve the runtime types up front.
            Type controllerType = FindType(TypeWallRepairController);
            Type bridgeType = FindType(TypeWallRepairHudBridge);
            Type hudControllerType = FindType(TypeVillageHudController);
            if (controllerType == null || bridgeType == null)
            {
                Debug.LogError("[WallRepairSceneSetup] DeNelle.Village types not found — is the " +
                               "DeNelle.Village assembly compiled? Aborting.");
                return;
            }
            if (hudControllerType == null)
            {
                Debug.LogError("[WallRepairSceneSetup] DeNelle.HUD.VillageHudController not found — is " +
                               "the DeNelle.HUD assembly compiled? Aborting.");
                return;
            }

            // 4) Idempotency — clear a previous run's objects.
            RemoveExisting(systemsRoot, WallRepairObjectName);

            // 5) Find an existing village HUD, or build one (the repair prompt
            //    lives in the HUD, so the scene needs a VillageHudController).
            Component hudController = FindHudController(hudControllerType);
            if (hudController == null)
            {
                hudController = BuildVillageHud(systemsRoot, hudControllerType);
                if (hudController == null)
                {
                    Debug.LogError("[WallRepairSceneSetup] Could not build the village HUD. Aborting.");
                    return;
                }
                Debug.Log("[WallRepairSceneSetup] No VillageHud found — built one under GameplaySystems.");
            }
            else
            {
                Debug.Log("[WallRepairSceneSetup] Re-using the existing VillageHudController in the scene.");
            }

            // 6) Build the WallRepair GameObject — controller + bridge.
            var go = new GameObject(WallRepairObjectName);
            go.transform.SetParent(systemsRoot, false);

            Component controller = go.AddComponent(controllerType);
            Component bridge = go.AddComponent(bridgeType);

            // 7) Wire the bridge: _controller -> the WallRepairController on this
            //    object; _hud -> the VillageHudController instance.
            var bridgeSo = new SerializedObject(bridge);
            SetObjectField(bridgeSo, "_controller", controller);
            SetObjectField(bridgeSo, "_hud", hudController);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            // The WallRepairController auto-finds Camera.main; nothing else to wire.

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);

            Debug.Log("[WallRepairSceneSetup] Wall-repair mechanic wired into the Village scene " +
                      "(WallRepairController + WallRepairHudBridge under GameplaySystems). " +
                      "Repair prompt -> HUD; HUD Repair/Cancel -> controller.");
        }

        // =====================================================================
        //  Scene / hierarchy helpers
        // =====================================================================

        private static Scene EnsureVillageSceneOpen()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == VillageScenePath)
                return active;

            if (!System.IO.File.Exists(VillageScenePath))
                return default;

            return EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);
        }

        private static Transform FindGameplaySystemsRoot()
        {
            var root = GameObject.Find(VillageRootName);
            if (root == null) return null;
            var systems = root.transform.Find(GameplaySystemsName);
            if (systems != null) return systems;

            // Older / partial scene — create the systems root so the command
            // still has somewhere tidy to park the repair objects.
            var go = new GameObject(GameplaySystemsName);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        private static void RemoveExisting(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == name)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        // =====================================================================
        //  Village HUD — found or freshly built
        // =====================================================================

        /// <summary>Finds the first VillageHudController already in the open scene, or null.</summary>
        private static Component FindHudController(Type hudControllerType)
        {
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType(hudControllerType,
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType(hudControllerType, true);
#endif
            return (found != null && found.Length > 0) ? found[0] as Component : null;
        }

        /// <summary>
        /// Builds a "VillageHud" GameObject: a UIDocument hosting VillageHud.uxml
        /// + the VillageHudController. Mirrors how VillageSceneBuilder builds the
        /// BuildMenu UIDocument (PanelSettings reused from the project).
        /// </summary>
        private static Component BuildVillageHud(Transform parent, Type hudControllerType)
        {
            var go = new GameObject(HudObjectName);
            go.transform.SetParent(parent, false);

            var uiDoc = go.AddComponent<UIDocument>();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VillageHudUxmlPath);
            if (uxml != null)
            {
                uiDoc.visualTreeAsset = uxml;
            }
            else
            {
                Debug.LogWarning($"[WallRepairSceneSetup] VillageHud.uxml not found at " +
                                 $"'{VillageHudUxmlPath}' — assign the UIDocument source asset by hand.");
            }
            WirePanelSettings(uiDoc);

            // VillageHudController has [RequireComponent(typeof(UIDocument))] —
            // already satisfied. Wire its _document field to this UIDocument.
            Component hud = go.AddComponent(hudControllerType);
            var so = new SerializedObject(hud);
            SetObjectField(so, "_document", uiDoc);
            so.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        /// <summary>Assigns the first PanelSettings asset in the project to a UIDocument.</summary>
        private static void WirePanelSettings(UIDocument uiDoc)
        {
            if (uiDoc == null || uiDoc.panelSettings != null) return;
            var guids = AssetDatabase.FindAssets("t:PanelSettings");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
                if (panel != null) { uiDoc.panelSettings = panel; return; }
            }
            Debug.LogWarning("[WallRepairSceneSetup] No PanelSettings asset found — assign one to the " +
                             "VillageHud UIDocument in the inspector or the HUD will not render.");
        }

        // =====================================================================
        //  Reflection helpers (mirrors VillageSceneBuilder)
        // =====================================================================

        private static void SetObjectField(SerializedObject so, string field, UnityEngine.Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[WallRepairSceneSetup] Serialized field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} — not wired.");
                return;
            }
            prop.objectReferenceValue = value;
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
    }
}
