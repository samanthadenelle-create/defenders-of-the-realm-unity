// =============================================================================
// DungeonStubBuilder — minimal builder for secondary dungeon scenes.
// -----------------------------------------------------------------------------
// The Healer's Cottage builder (DungeonSceneBuilder) is ~2100 lines of fully
// authored layout + crafting + Bryn + lanterns. Cloning that level of polish
// for the other six dungeons (Folk's Granary, Sunken Bell-Tower, Wolfwarden's
// Vigil, Frost-Stair, Glass Cathedral, Apothecary's Vault) is a multi-week
// job. This file ships PLAYABLE STUB scenes — a floor, four perimeter walls,
// a hero spawn, an exit pad that returns to the village, and a thematic light
// — so the routing works end-to-end and owner can validate the loop before we
// invest in per-dungeon dressing.
//
// Each public BuildXxx() method just packs DungeonStubParams and calls Build().
// Add a new dungeon by adding a new wrapper method.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    public sealed class DungeonStubParams
    {
        public string DungeonId;       // e.g. Dungeon_FolksGranary
        public string DisplayName;     // e.g. Folk's Old Granary
        public Color  FloorTint;
        public Color  AmbientLightColor;
        public string FlavorLine;      // a single sign above the exit pad
    }

    public static class DungeonStubBuilder
    {
        private const string ScenesDir = "Assets/Scenes";

        // ── Public entry points (one per dungeon) ─────────────────────────────

        public static void BuildFolksGranary() => Build(new DungeonStubParams
        {
            DungeonId  = "Dungeon_FolksGranary",
            DisplayName = "Folk's Old Granary",
            FloorTint  = new Color(0.55f, 0.42f, 0.28f),
            AmbientLightColor = new Color(1f, 0.85f, 0.55f),
            FlavorLine = "Grain dust hangs in the air. Old stories sleep here.",
        });

        public static void BuildSunkenBellTower() => Build(new DungeonStubParams
        {
            DungeonId  = "Dungeon_SunkenBellTower",
            DisplayName = "Sunken Bell-Tower",
            FloorTint  = new Color(0.30f, 0.40f, 0.50f),
            AmbientLightColor = new Color(0.65f, 0.80f, 0.95f),
            FlavorLine = "The bell hangs still. The water knows your name.",
        });

        public static void BuildWolfwardensVigil() => Build(new DungeonStubParams
        {
            DungeonId  = "Dungeon_WolfwardensVigil",
            DisplayName = "Wolfwarden's Vigil",
            FloorTint  = new Color(0.32f, 0.30f, 0.26f),
            AmbientLightColor = new Color(0.80f, 0.70f, 0.55f),
            FlavorLine = "A long watch. The pelt has not been cold for centuries.",
        });

        public static void BuildFrostStair() => Build(new DungeonStubParams
        {
            DungeonId  = "Dungeon_FrostStair",
            DisplayName = "The Frost-Stair",
            FloorTint  = new Color(0.70f, 0.82f, 0.92f),
            AmbientLightColor = new Color(0.85f, 0.92f, 1f),
            FlavorLine = "Every step you take, the stair takes one with you.",
        });

        public static void BuildGlassCathedral() => Build(new DungeonStubParams
        {
            DungeonId  = "Dungeon_GlassCathedral",
            DisplayName = "Glass Cathedral",
            FloorTint  = new Color(0.88f, 0.86f, 0.90f),
            AmbientLightColor = new Color(0.95f, 0.92f, 1f),
            FlavorLine = "Do not break it; the choir is still inside.",
        });

        public static void BuildApothecarysVault() => Build(new DungeonStubParams
        {
            DungeonId  = "Dungeon_ApothecarysVault",
            DisplayName = "Apothecary's Vault",
            FloorTint  = new Color(0.40f, 0.55f, 0.45f),
            AmbientLightColor = new Color(0.70f, 0.90f, 0.75f),
            FlavorLine = "Bring nothing back. Drink nothing here.",
        });

        // ── Build implementation ──────────────────────────────────────────────

        private static void Build(DungeonStubParams p)
        {
            if (p == null || string.IsNullOrEmpty(p.DungeonId))
                throw new System.ArgumentException("DungeonStubBuilder: DungeonId is required.");

            string scenePath = $"{ScenesDir}/{p.DungeonId}.unity";

            // 1. Create or open the scene.
            Scene scene;
            if (!File.Exists(scenePath))
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            ClearScene();

            BuildFloor(p);
            BuildPerimeterWalls();
            BuildLighting(p);
            BuildHeroSpawn();
            BuildExitPad(p);
            BuildEventSystem();
            BuildUIDocumentWithSign(p);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            RegisterInBuildSettings(scenePath);

            Debug.Log($"[DungeonStubBuilder] {p.DungeonId} built — {p.DisplayName}");
        }

        private static void ClearScene()
        {
            var rootGos = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in rootGos) Object.DestroyImmediate(go);
        }

        private static void BuildFloor(DungeonStubParams p)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(40f, 0.5f, 40f);
            PaintLit(floor.GetComponent<Renderer>(), p.FloorTint);
            floor.isStatic = true;
        }

        private static void BuildPerimeterWalls()
        {
            const float half = 20f, wallH = 4.5f, t = 0.6f;
            BuildWall(new Vector3( 0f, wallH * 0.5f,  half), new Vector3(half * 2f, wallH, t)); // N
            BuildWall(new Vector3( 0f, wallH * 0.5f, -half), new Vector3(half * 2f, wallH, t)); // S
            BuildWall(new Vector3( half, wallH * 0.5f, 0f), new Vector3(t, wallH, half * 2f)); // E
            BuildWall(new Vector3(-half, wallH * 0.5f, 0f), new Vector3(t, wallH, half * 2f)); // W
        }

        private static void BuildWall(Vector3 pos, Vector3 scale)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Wall";
            w.transform.position = pos;
            w.transform.localScale = scale;
            PaintLit(w.GetComponent<Renderer>(), new Color(0.28f, 0.26f, 0.24f));
            w.isStatic = true;
        }

        private static void BuildLighting(DungeonStubParams p)
        {
            var lightGo = new GameObject("Lantern");
            lightGo.transform.position = new Vector3(0f, 7f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = p.AmbientLightColor;
            light.intensity = 3f;
            light.range = 35f;

            RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.07f);
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.08f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;
        }

        private static void BuildHeroSpawn()
        {
            // Placeholder hero — the real hero locomotion is dungeon-specific
            // (DungeonHero). For the stub, a labelled capsule reads as "you
            // are here" so the routing test is self-explanatory.
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DungeonHeroPlaceholder";
            go.transform.position = new Vector3(0f, 1f, -15f);
            PaintLit(go.GetComponent<Renderer>(), new Color(0.62f, 0.55f, 0.92f));

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 6f, -22f);
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            c.fieldOfView = 60f;
            cam.AddComponent<AudioListener>();
        }

        private static void BuildExitPad(DungeonStubParams p)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "ExitPad";
            pad.transform.position = new Vector3(0f, 0.05f, 14f);
            pad.transform.localScale = new Vector3(3f, 0.1f, 3f);
            PaintUnlit(pad.GetComponent<Renderer>(), new Color(0.78f, 0.55f, 1f, 0.85f), true);
            Object.DestroyImmediate(pad.GetComponent<Collider>());

            var trigger = pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(3f, 3f, 3f);

            // Attach a tiny return-to-village monobehaviour by reflection so
            // the editor asmdef doesn't need a runtime ref. The component is
            // defined alongside the stub in DungeonStubReturn.cs.
            var t = System.Type.GetType("DeNelle.Dungeons.DungeonStubReturn, DeNelle.Dungeons");
            if (t != null) pad.AddComponent(t);
        }

        private static void BuildEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModule != null) es.AddComponent(inputModule);
            else es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static void BuildUIDocumentWithSign(DungeonStubParams p)
        {
            var go = new GameObject("DungeonSign");
            var ui = go.AddComponent<UIDocument>();

            // Borrow a PanelSettings from any other UIDocument in the project.
            string[] guids = AssetDatabase.FindAssets("t:PanelSettings");
            if (guids != null && guids.Length > 0)
            {
                ui.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            ui.sortingOrder = 90;

            var root = ui.rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;

            var card = new VisualElement();
            card.style.marginTop = 24;
            card.style.marginLeft = StyleKeyword.Auto;
            card.style.marginRight = StyleKeyword.Auto;
            card.style.alignSelf = Align.Center;
            card.style.paddingLeft = 16; card.style.paddingRight = 16;
            card.style.paddingTop = 10;  card.style.paddingBottom = 10;
            card.style.borderTopLeftRadius = 10; card.style.borderTopRightRadius = 10;
            card.style.borderBottomLeftRadius = 10; card.style.borderBottomRightRadius = 10;
            card.style.backgroundColor = new StyleColor(new Color(0.06f, 0.04f, 0.10f, 0.92f));
            card.style.borderTopWidth = 1; card.style.borderBottomWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(0.78f, 0.55f, 1f, 1f));
            card.style.borderBottomColor = new StyleColor(new Color(0.78f, 0.55f, 1f, 1f));
            card.pickingMode = PickingMode.Ignore;
            root.Add(card);

            var h = new Label(p.DisplayName);
            h.style.color = new StyleColor(new Color(1f, 0.96f, 0.88f, 1f));
            h.style.fontSize = 22;
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(h);

            var sub = new Label(p.FlavorLine);
            sub.style.color = new StyleColor(new Color(0.85f, 0.83f, 0.78f, 1f));
            sub.style.fontSize = 13;
            sub.style.marginTop = 4;
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(sub);

            var hint = new Label("Stand on the violet exit pad to return to the village.");
            hint.style.color = new StyleColor(new Color(0.65f, 0.70f, 0.78f, 1f));
            hint.style.fontSize = 11;
            hint.style.marginTop = 6;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(hint);
        }

        private static void RegisterInBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
                if (string.Equals(s.path, scenePath, System.StringComparison.OrdinalIgnoreCase))
                    return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ── Material helpers ──────────────────────────────────────────────────

        private static void PaintLit(Renderer r, Color c)
        {
            if (r == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", 0f);
            r.sharedMaterial = m;
        }

        private static void PaintUnlit(Renderer r, Color c, bool transparent)
        {
            if (r == null) return;
            var sh = transparent
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
                : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (transparent && m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            r.sharedMaterial = m;
        }
    }
}
