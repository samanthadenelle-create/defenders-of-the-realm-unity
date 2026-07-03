// =============================================================================
// FolksGranaryBuilder — Folk's Old Granary dungeon (D2) scene generator (Editor).
// -----------------------------------------------------------------------------
// One static entry point the main Unity session runs (manually via the
// Defenders menu, or via the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.FolksGranaryBuilder.BuildFolksGranary
//
// What it does — promotes the old DungeonStubBuilder.BuildFolksGranary stub
// (floor + 4 walls + capsule hero) into a hand-authored ~25x25 m granary
// cellar built from KayKit Dungeon Remastered 1.1 modular pieces. The scene
// dresses to the granary fiction:
//   - silo bay walls of stacked barrels + grain sacks (large/small barrels)
//   - a central hatch (a labelled placeholder cube — the pack ships no
//     trapdoor mesh) flanked by two oil lanterns
//   - oil-soaked rope coils (small barrels tinted dark) around the hatch
//   - a workbench (long table) with a third lantern overhead
//   - one ambient NPC near the entrance speaking a canned grain-silo line
//   - one EncounterTrigger near the exit pad
//   - one DungeonStubReturn exit pad (cylinder) that returns to the village
//   - a UIDocument sign at the top with the granary's flavour line
//
// IDEMPOTENT — re-running BuildFolksGranary() destroys every object under the
// generated "DungeonRoot" (plus stray Main Camera / Directional Light) and
// rebuilds from scratch. Safe to repeat.
//
// ASSEMBLY NOTE. DeNelle.Editor.asmdef references only Core / Data /
// Localization / URP — NOT DeNelle.Dungeons or DeNelle.Village. So this builder
// takes NO compile-time dependency on the dungeon / village MonoBehaviours:
// EncounterTrigger, DungeonStubReturn, Bryn / WandererBubble, AmbientNPC are
// added by REFLECTION (resolved by full type name) and their [SerializeField]
// fields are set through SerializedObject — the same pattern
// DungeonSceneBuilder.cs uses for the Healer's Cottage.
//
// KAYKIT MODELS. Built from "KayKit Dungeon Remastered 1.1" under
// Assets/Models/KayKit/.../Assets/fbx(unity)/. Each model loads via
// AssetDatabase.LoadAssetAtPath<GameObject>; on a missed path the builder
// substitutes a clearly-labelled placeholder primitive and logs a warning. It
// NEVER blocks.
//
// This script does NOT delete the DungeonStubBuilder.BuildFolksGranary stub
// path — both routes coexist; this dedicated builder takes precedence when the
// owner invokes "Defenders/Dungeons/Build Folk's Old Granary".
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
    /// Editor utility that assembles the Folk's Old Granary dungeon scene (D2)
    /// from KayKit Dungeon Remastered models. Entry point:
    /// <see cref="BuildFolksGranary"/>.
    /// </summary>
    public static class FolksGranaryBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";
        private const string DungeonScenePath = ScenesDir + "/Dungeon_FolksGranary.unity";

        /// <summary>Root for everything this builder generates — cleared + rebuilt each run.</summary>
        private const string DungeonRootName = "DungeonRoot";

        // ── KayKit Dungeon Remastered 1.1 paths ──────────────────────────────
        private const string PackRoot =
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx(unity)/";

        // ── Dungeon / Village MonoBehaviour type names (resolved by reflection) ─
        private const string TypeEncounterTrigger = "DeNelle.Dungeons.EncounterTrigger";
        private const string TypeDungeonStubEncounter = "DeNelle.Dungeons.DungeonStubEncounter";
        private const string TypeDungeonStubReturn = "DeNelle.Dungeons.DungeonStubReturn";
        private const string TypeDungeonHero = "DeNelle.Dungeons.DungeonHero";
        private const string TypeDungeonCameraRig = "DeNelle.Dungeons.DungeonCameraRig";
        private const string TypeAmbientNpc = "DeNelle.Village.AmbientNPC";
        private const string TypeTownsfolkBubble = "DeNelle.Village.TownsfolkBubble";

        // ── Cinemachine 3.x type names (the package assembly is loaded in the
        // editor; the Editor asmdef does not reference Unity.Cinemachine directly).
        private const string TypeCinemachineCamera = "Unity.Cinemachine.CinemachineCamera";
        private const string TypeCinemachineFollow = "Unity.Cinemachine.CinemachineFollow";
        private const string TypeCinemachineBrain  = "Unity.Cinemachine.CinemachineBrain";

        // ── Cellar geometry — a ~25x25 m granary footprint ───────────────────
        private const float Y = 0f;
        private const float WallHeight = 4f;
        private const float WallSegLen = 4f;        // KayKit wall piece ~4u long
        private const float MinX = -12f;
        private const float MaxX = 12f;
        private const float MinZ = -12f;
        private const float MaxZ = 12f;

        // ── Granary palette tints (dusty wheat / oiled wood) ─────────────────
        private static readonly Color TintWall = HexColor("8a7456");      // weathered plank
        private static readonly Color TintFloor = HexColor("a08458");     // dusty wheat
        private static readonly Color TintGrain = HexColor("c9a86a");     // pale grain
        private static readonly Color TintOiled = HexColor("3a2a1c");     // oil-soaked dark
        private static readonly Color TintHatch = HexColor("4a3422");     // hatch wood

        // ── Hard-locked NPC line (the only canned dialogue for this scene) ───
        private const string GranaryNpcLine =
            "Mind your step, Keeper. The silo cellars run deeper than the village " +
            "remembers, and the grain still listens.";

        // ── Sign copy — owner brief, verbatim ────────────────────────────────
        private const string SignTitle = "Folk's Old Granary";
        private const string SignFlavor =
            "Grain dust hangs in the air. Old stories sleep here.";

        // ── Running tallies for the summary log ──────────────────────────────
        private static int _placeholderCount;
        private static readonly List<string> _placeholders = new List<string>();
        private static int _modelCount;
        private static int _wallCount;
        private static int _floorCount;
        private static int _propCount;
        private static int _lightCount;
        private static int _encounterCount;
        private static int _brynCount;     // ambient NPCs (Bryn-style) attached
        private static int _loreStoneCount; // for parity with the summary contract

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the Folk's Old Granary dungeon scene. Runnable via
        /// <c>-executeMethod DeNelle.Editor.FolksGranaryBuilder.BuildFolksGranary</c>.
        /// Idempotent.
        /// </summary>
        [MenuItem("Defenders/Dungeons/Build Folk's Old Granary")]
        public static void BuildFolksGranary()
        {
            _placeholderCount = 0;
            _placeholders.Clear();
            _modelCount = 0;
            _wallCount = 0;
            _floorCount = 0;
            _propCount = 0;
            _lightCount = 0;
            _encounterCount = 0;
            _brynCount = 0;
            _loreStoneCount = 0;

            EnsureFolder(ScenesDir);
            AssetDatabase.Refresh();

            // ── Open or create the dungeon scene ─────────────────────────────
            UnityEngine.SceneManagement.Scene scene;
            if (File.Exists(DungeonScenePath))
                scene = EditorSceneManager.OpenScene(DungeonScenePath, OpenSceneMode.Single);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Idempotency: nuke any prior generated root + scene fixtures ──
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == DungeonRootName ||
                    go.name == "Main Camera" ||
                    go.name == "Directional Light" ||
                    go.name == "Floor" ||
                    go.name == "Wall" ||
                    go.name == "Lantern" ||
                    go.name == "DungeonHeroPlaceholder" ||
                    go.name == "ExitPad" ||
                    go.name == "EventSystem" ||
                    go.name == "DungeonSign")
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            // ── Granary-mood ambient lighting (warm oil-lantern bias) ────────
            ConfigureAmbient();

            // ── Scene root + sub-roots ───────────────────────────────────────
            var root = new GameObject(DungeonRootName);
            var floorRoot = NewChild(root.transform, "Floor");
            var wallRoot = NewChild(root.transform, "Walls");
            var dressRoot = NewChild(root.transform, "Dressing");
            var lightRoot = NewChild(root.transform, "Lighting");
            var actorRoot = NewChild(root.transform, "Actors");
            var encounterRoot = NewChild(root.transform, "Encounters");

            // ── Camera + faint directional key + EventSystem ─────────────────
            CreateCamera();
            CreateDirectionalLight();
            EnsureEventSystem();

            // =================================================================
            //  Geometry
            // =================================================================
            BuildFloor(floorRoot);
            BuildPerimeter(wallRoot);

            // =================================================================
            //  Dressing — silo bay walls, grain piles, oil-soaked rope coils,
            //  workbench, central hatch with flanking lanterns
            // =================================================================
            BuildSiloBays(dressRoot);
            BuildGrainPiles(dressRoot);
            BuildOilRopeCoils(dressRoot);
            BuildHatchWithLanterns(dressRoot, lightRoot);
            BuildWorkbench(dressRoot, lightRoot);
            BuildPerimeterLanterns(lightRoot);

            // =================================================================
            //  Hero placeholder (a labelled capsule — the playable rig is
            //  swapped in by the dungeon module at run time, same as the
            //  Healer's Cottage builder's "Keeper" stand-in).
            // =================================================================
            var heroGo = BuildHeroPlaceholder(actorRoot);

            // =================================================================
            //  Ambient NPC near the entrance (Bryn-equivalent — DeNelle.Village
            //  AmbientNPC via reflection; the canned grain-silo line is baked
            //  into a child GameObject name so it survives the scene save).
            // =================================================================
            BuildAmbientNpc(actorRoot, heroGo.transform);

            // =================================================================
            //  EncounterTrigger near the exit + exit pad with DungeonStubReturn
            // =================================================================
            BuildEncounterTrigger(encounterRoot);
            BuildExitPad(actorRoot);

            // =================================================================
            //  UIDocument sign at the top of the screen
            // =================================================================
            BuildUIDocumentSign(root.transform);

            // ── Save + register in Build Settings ────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, DungeonScenePath);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogSummary();
        }

        // =====================================================================
        //  Floor + perimeter walls
        // =====================================================================

        private static void BuildFloor(Transform parent)
        {
            // Dusty plank floor — the granary lived above ground but the cellar
            // floors are dirt-pack with plank patches.
            var model = LoadModel(PackRoot + "floor_wood_large.fbx");
            const float tile = 4f;
            for (float x = MinX + tile * 0.5f; x < MaxX; x += tile)
            for (float z = MinZ + tile * 0.5f; z < MaxZ; z += tile)
            {
                var go = InstantiateModel(model, "floor_wood_large.fbx", "granary floor");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, Y, z);
                ApplyTint(go, TintFloor);
                EnsureCollider(go);
                _floorCount++;
                if (model != null) _modelCount++;
            }

            // A scatter of dirt-floor tiles around the centre — grain dust /
            // spillage — for a tactile contrast on the wood plank field.
            var dirt = LoadModel(PackRoot + "floor_dirt_small_A.fbx");
            var spots = new Vector3[]
            {
                new Vector3( 0f, Y + 0.02f,  0f),
                new Vector3( 4f, Y + 0.02f, -2f),
                new Vector3(-3f, Y + 0.02f,  3f),
                new Vector3( 2f, Y + 0.02f,  5f),
                new Vector3(-5f, Y + 0.02f, -4f),
            };
            foreach (var p in spots)
            {
                var go = InstantiateModel(dirt, "floor_dirt_small_A.fbx", "dust patch");
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                ApplyTint(go, TintGrain);
                StripColliders(go);
                _floorCount++;
                if (dirt != null) _modelCount++;
            }
        }

        private static void BuildPerimeter(Transform parent)
        {
            // S edge (z = MinZ, run +X) ; N edge (z = MaxZ, run +X)
            BuildWallRun(parent, "S", new Vector3(MinX, Y, MinZ),
                Vector3.right, MaxX - MinX, 0f, doorOffset: -1f);
            BuildWallRun(parent, "N", new Vector3(MinX, Y, MaxZ),
                Vector3.right, MaxX - MinX, 180f,
                doorOffset: (MaxX - MinX) * 0.5f); // exit doorway in the N wall
            // W edge (x = MinX, run +Z) ; E edge (x = MaxX, run +Z)
            BuildWallRun(parent, "W", new Vector3(MinX, Y, MinZ),
                Vector3.forward, MaxZ - MinZ, -90f,
                doorOffset: (MaxZ - MinZ) * 0.5f); // entrance doorway in the W wall
            BuildWallRun(parent, "E", new Vector3(MaxX, Y, MinZ),
                Vector3.forward, MaxZ - MinZ, 90f, doorOffset: -1f);
        }

        private static void BuildWallRun(Transform parent, string side,
            Vector3 origin, Vector3 dir, float length, float yaw, float doorOffset)
        {
            var wallModel = LoadModel(PackRoot + "wall.fbx");
            var doorModel = LoadModel(PackRoot + "wall_doorway.fbx");

            int segs = Mathf.Max(1, Mathf.RoundToInt(length / WallSegLen));
            float step = length / segs;

            int doorSeg = doorOffset >= 0f
                ? Mathf.Clamp(Mathf.FloorToInt(doorOffset / step), 0, segs - 1)
                : -1;

            for (int i = 0; i < segs; i++)
            {
                float along = (i + 0.5f) * step;
                Vector3 pos = origin + dir * along;
                bool isDoor = i == doorSeg;

                GameObject go;
                if (isDoor)
                {
                    go = InstantiateModel(doorModel, "wall_doorway.fbx",
                        $"granary doorway {side}");
                    EnsureCollider(go);
                }
                else
                {
                    go = InstantiateModel(wallModel, "wall.fbx",
                        $"granary wall {side}");
                    EnsureCollider(go);
                }

                go.transform.SetParent(parent, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                ApplyTint(go, TintWall);
                _wallCount++;
                if (wallModel != null || doorModel != null) _modelCount++;
            }

            // Internal columns at the corners — short pillar pieces reading as
            // the granary's structural posts.
            if (side == "S")
            {
                Prop(parent, "column.fbx", MinX + 1.5f, Y, MinZ + 1.5f, 0f, TintWall);
                Prop(parent, "column.fbx", MaxX - 1.5f, Y, MinZ + 1.5f, 0f, TintWall);
            }
            else if (side == "N")
            {
                Prop(parent, "column.fbx", MinX + 1.5f, Y, MaxZ - 1.5f, 0f, TintWall);
                Prop(parent, "column.fbx", MaxX - 1.5f, Y, MaxZ - 1.5f, 0f, TintWall);
            }
        }

        // =====================================================================
        //  Dressing — silo bays, grain piles, oil-soaked rope coils, workbench
        // =====================================================================

        /// <summary>
        /// Two silo bays — towers of stacked large barrels along the E + W walls.
        /// These read as the grain-silo cellars the granary stored at depth.
        /// </summary>
        private static void BuildSiloBays(Transform parent)
        {
            // East silo bay — three large barrels stacked tight to the wall.
            BuildSiloStack(parent, new Vector3(MaxX - 2.5f, Y, -4f));
            BuildSiloStack(parent, new Vector3(MaxX - 2.5f, Y,  4f));
            // West silo bay — two stacks flanking the entrance doorway.
            BuildSiloStack(parent, new Vector3(MinX + 2.5f, Y, -6f));
            BuildSiloStack(parent, new Vector3(MinX + 2.5f, Y,  6f));
        }

        private static void BuildSiloStack(Transform parent, Vector3 baseAt)
        {
            // Base — a large decorated barrel reads as a riveted grain silo.
            Prop(parent, "barrel_large_decorated.fbx", baseAt.x, baseAt.y, baseAt.z, 0f, TintWall);
            // Mid + cap — a stacked-small-barrel piece plus a single large barrel.
            Prop(parent, "barrel_large.fbx", baseAt.x, baseAt.y + 1.4f, baseAt.z, 30f, TintWall);
            Prop(parent, "barrel_small_stack.fbx", baseAt.x + 0.6f, baseAt.y, baseAt.z + 1.2f, 60f, TintWall);
        }

        /// <summary>
        /// Loose grain piles — small crates + a labelled dust-mound placeholder
        /// where the pack ships no grain-pile mesh.
        /// </summary>
        private static void BuildGrainPiles(Transform parent)
        {
            Prop(parent, "crate_small.fbx", -7f, Y, -7f, 15f, TintGrain);
            Prop(parent, "crates_stacked.fbx", -7f, Y, 7f, 0f, TintGrain);
            Prop(parent, "crate_large.fbx", 7f, Y, -7f, 25f, TintGrain);
            Prop(parent, "box_stacked.fbx", 7f, Y, 7f, 0f, TintGrain);

            // Spilled grain mounds — three labelled placeholder cubes flattened
            // to a low pile so the room reads "grain spilled here."
            Placeholder(parent, "grain pile (spillage, no KayKit mesh)",
                new Vector3(-3f, Y + 0.18f, -1.5f), TintGrain,
                new Vector3(1.6f, 0.35f, 1.1f));
            Placeholder(parent, "grain pile (spillage, no KayKit mesh)",
                new Vector3(4f, Y + 0.16f, 1f), TintGrain,
                new Vector3(1.2f, 0.32f, 1.6f));
            Placeholder(parent, "grain pile (spillage, no KayKit mesh)",
                new Vector3(-5f, Y + 0.20f, 5f), TintGrain,
                new Vector3(1.4f, 0.40f, 1.4f));
        }

        /// <summary>
        /// Oil-soaked rope coils ring the central hatch — small barrels tinted
        /// near-black so they read as wound, oil-saturated rope.
        /// </summary>
        private static void BuildOilRopeCoils(Transform parent)
        {
            Prop(parent, "barrel_small.fbx", -1.8f, Y, -1.8f, 0f, TintOiled);
            Prop(parent, "barrel_small.fbx",  1.8f, Y, -1.8f, 0f, TintOiled);
            Prop(parent, "barrel_small.fbx", -1.8f, Y,  1.8f, 0f, TintOiled);
            Prop(parent, "barrel_small.fbx",  1.8f, Y,  1.8f, 0f, TintOiled);
        }

        /// <summary>
        /// The central hatch (a labelled placeholder cube — the pack ships no
        /// trapdoor mesh) flanked by two oil lanterns; the lanterns are point
        /// lights with a small visible candle prop underneath.
        /// </summary>
        private static void BuildHatchWithLanterns(Transform dress, Transform lightRoot)
        {
            // The hatch itself — a thin wood-tinted disc at floor level.
            Placeholder(dress, "cellar hatch (no KayKit mesh)",
                new Vector3(0f, Y + 0.05f, 0f), TintHatch,
                new Vector3(2.6f, 0.10f, 2.6f));
            // A ring of candle melt around the hatch — the pack's tall candles.
            Prop(dress, "candle_thin_lit.fbx", -0.9f, Y, -0.9f, 0f);
            Prop(dress, "candle_thin_lit.fbx",  0.9f, Y,  0.9f, 0f);

            // Two oil lanterns — placeholder posts (no lantern-post mesh in the
            // pack) topped with a warm point light.
            BuildOilLantern(lightRoot, "HatchLanternW", new Vector3(-2.5f, Y, 0f));
            BuildOilLantern(lightRoot, "HatchLanternE", new Vector3( 2.5f, Y, 0f));
        }

        /// <summary>
        /// An oil lantern: a tall placeholder post + a warm point light at the
        /// post's tip. Logged as a placeholder because the KayKit pack ships no
        /// lantern-post mesh — the §7 Healer's Cottage builder makes the same
        /// trade-off.
        /// </summary>
        private static void BuildOilLantern(Transform parent, string name, Vector3 footAt)
        {
            var post = new GameObject($"OilLantern_{name}");
            post.transform.SetParent(parent, false);
            post.transform.position = footAt;

            // The post itself — a labelled thin pillar so the lantern reads as
            // a fixture and not a free-floating ember.
            var postMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            postMesh.name = "[PLACEHOLDER] oil lantern post (no KayKit mesh)";
            postMesh.transform.SetParent(post.transform, false);
            postMesh.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            postMesh.transform.localScale = new Vector3(0.25f, 2.2f, 0.25f);
            ApplyTint(postMesh, HexColor("3a2818"));
            NotePlaceholder($"oil lantern post @ {footAt}");
            // The post is a non-blocking visual — keep its collider so the
            // Keeper cannot walk THROUGH the post (consistent with the BUG-007
            // rule the Healer's Cottage builder enforces).

            // The lantern head — a warm emissive cube hung from the post tip.
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "LanternHead";
            head.transform.SetParent(post.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            head.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            StripColliders(head);
            ApplyEmissive(head, HexColor("f6c97a"), 1.8f);

            // The warm point light — the actual illumination source (§7-style).
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(post.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = HexColor("f6c97a");
            light.intensity = 3.2f;
            light.range = 8f;
            light.shadows = LightShadows.None;
            _lightCount++;
        }

        /// <summary>
        /// A long workbench at the north wall with a third oil lantern overhead,
        /// a book, and a few small props — the granary's record-keeper corner.
        /// </summary>
        private static void BuildWorkbench(Transform dress, Transform lightRoot)
        {
            float bx = 0f;
            float bz = MaxZ - 3.5f;
            Prop(dress, "table_long.fbx", bx, Y, bz, 0f, TintWall);
            Prop(dress, "chair.fbx", bx - 1.5f, Y, bz - 1.4f, 0f, TintWall);
            Prop(dress, "book_brown.fbx", bx - 0.6f, Y + 0.55f, bz, 12f);
            Prop(dress, "book_tan.fbx", bx + 0.4f, Y + 0.55f, bz, -8f);
            Prop(dress, "candle_lit.fbx", bx + 1.1f, Y + 0.55f, bz, 0f);

            // Workbench lantern — above the table.
            BuildOilLantern(lightRoot, "WorkbenchLantern",
                new Vector3(bx, Y, bz - 0.2f));
        }

        /// <summary>
        /// A faint perimeter glow — small wall-mounted candle pools so the dark
        /// corners of the cellar do not read as a void.
        /// </summary>
        private static void BuildPerimeterLanterns(Transform parent)
        {
            LitFixture(parent, "CornerNW",
                new Vector3(MinX + 1.8f, Y + 2.4f, MaxZ - 1.8f),
                HexColor("ffd98a"), 1.2f, 5f);
            LitFixture(parent, "CornerNE",
                new Vector3(MaxX - 1.8f, Y + 2.4f, MaxZ - 1.8f),
                HexColor("ffd98a"), 1.2f, 5f);
            LitFixture(parent, "CornerSW",
                new Vector3(MinX + 1.8f, Y + 2.4f, MinZ + 1.8f),
                HexColor("ffd98a"), 1.0f, 4.5f);
            LitFixture(parent, "CornerSE",
                new Vector3(MaxX - 1.8f, Y + 2.4f, MinZ + 1.8f),
                HexColor("ffd98a"), 1.0f, 4.5f);
        }

        // =====================================================================
        //  Actors — hero placeholder, ambient NPC, encounter trigger, exit pad
        // =====================================================================

        private static GameObject BuildHeroPlaceholder(Transform parent)
        {
            // Hero root — same name DungeonStubReturn.cs:59 looks up via
            // GameObject.Find("DungeonHeroPlaceholder") for the F-key proximity
            // check. The root owns the CharacterController + DungeonHero so the
            // player can actually walk; the visual capsule is a colliderless
            // child so it doesn't fight the CharacterController body.
            var heroGo = new GameObject("DungeonHeroPlaceholder");
            heroGo.transform.SetParent(parent, false);
            // Spawn near the W-wall doorway, facing east into the cellar.
            heroGo.transform.position = new Vector3(MinX + 2.5f, Y, 0f);
            heroGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            // "HeroBody" so HeroBodySwapper (added below) replaces this capsule with
            // the player's real animated class FBX at runtime (owner WO-35: no pill).
            body.name = "HeroBody";
            body.transform.SetParent(heroGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            StripColliders(body);
            // PILL FIX (owner): e8d8b8 is near-white and reads as a glowing white
            // "pill" under the granary's oil-lantern point lights whenever
            // HeroBodySwapper fails to load the class FBX at run time. Tint the
            // FALLBACK capsule an emissive warm amber so it always reads as a hero
            // stand-in. On success HeroBodySwapper still destroys + replaces it.
            ApplyEmissive(body, HexColor("c98a3a"), 0.6f);

            // CharacterController sized to a human ~1.9 m capsule — DungeonHero
            // [RequireComponent]s this and slides it along the wall colliders.
            var cc = heroGo.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.45f;
            cc.skinWidth = 0.03f;

            // The DungeonHero locomotion (WASD + tap-to-move). Added by
            // reflection — DeNelle.Editor doesn't reference DeNelle.Dungeons.
            AddComponentByName(heroGo, TypeDungeonHero);
            // Real hero body at runtime (Mage/Knight/Ranger) instead of the capsule.
            AddComponentByName(heroGo, "DeNelle.Village.HeroBodySwapper");
            return heroGo;
        }

        /// <summary>
        /// One ambient NPC — a DeNelle.Village AmbientNPC, added by reflection
        /// (the Editor asmdef can't reference DeNelle.Village). The canned
        /// grain-silo line is baked into a child GameObject name so it survives
        /// the scene save and reads in the hierarchy without needing the
        /// strongly-typed dialogue pool.
        ///
        /// The DungeonSceneBuilder Bryn pattern (full Bryn + WandererBubble) is
        /// not used here because Bryn's Configure() requires a DungeonBrynDef
        /// from the layout JSON — the granary has no authored layout JSON, so
        /// AmbientNPC is the cleaner contract.
        /// </summary>
        private static void BuildAmbientNpc(Transform parent, Transform hero)
        {
            // Position near the entrance doorway (W wall), set back so the
            // Keeper walks past her on the way to the hatch.
            float nx = MinX + 5f;
            float nz = 0f;

            var npcGo = new GameObject("GranaryKeeper");
            npcGo.transform.SetParent(parent, false);
            npcGo.transform.position = new Vector3(nx, Y, nz);

            // A simple visual stand-in — a tinted capsule reads as a townsfolk
            // body (the AmbientNPC component does not bring its own mesh).
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "GranaryKeeperBody";
            body.transform.SetParent(npcGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            StripColliders(body);
            // PILL FIX (owner): a flat slate tint washes to near-white under the
            // entrance corner lantern. Emissive teal-slate so the NPC always
            // reads as a tinted body, not a blank white pill.
            ApplyEmissive(body, HexColor("5a7a86"), 0.5f);

            // The HARD-LOCKED canned line — baked into a child GameObject name
            // so it survives any build (no Editor-only state required). The
            // dungeon scene reads the line from this child if it ever wants to
            // surface it; otherwise AmbientNPC speaks its archetype pool.
            var lineMarker = new GameObject($"GranaryNpcLine: {GranaryNpcLine}");
            lineMarker.transform.SetParent(npcGo.transform, false);

            // The AmbientNPC component (DeNelle.Village — by reflection).
            Component npc = AddComponentByName(npcGo, TypeAmbientNpc);
            if (npc != null)
            {
                _brynCount++;
                // Wire the hero transform via SerializedObject so the proximity
                // check works the moment the scene loads (the runtime fallback
                // ResolveHeroFallback() also catches it, but the explicit wire
                // avoids the first-frame "no hero" branch).
                // AmbientNPC carries no _hero serialized field (that's a runtime
                // ref set via SetHero); the home anchor IS serialized, so we
                // pin it to the NPC's current position.
                var so = new SerializedObject(npc);
                var anchor = so.FindProperty("_homeAnchor");
                if (anchor != null)
                {
                    anchor.vector3Value = npcGo.transform.position;
                }
                var wander = so.FindProperty("_wander");
                if (wander != null)
                {
                    // An idler in the cellar — no NavMesh is baked here.
                    wander.boolValue = false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // A small TownsfolkBubble for her speech, also by reflection. The
            // bubble self-builds in Awake(); we only wire the reference.
            var bubbleGo = new GameObject("SpeechBubble");
            bubbleGo.transform.SetParent(npcGo.transform, false);
            bubbleGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            Component bubble = AddComponentByName(bubbleGo, TypeTownsfolkBubble);
            if (bubble != null && npc != null)
            {
                SetSerializedObjectRef(npc, "_bubble", bubble);
            }
        }

        /// <summary>
        /// One encounter zone near the exit. The granary ships NO DungeonController,
        /// so the rich EncounterTrigger (which only ticks once a controller calls
        /// ConfigureScripted + StartRun on a DungeonRuntimeState) would be inert —
        /// stepping on it did nothing and the ATB battle "never loaded." Instead
        /// this uses DungeonStubEncounter, the controller-free stub analog of
        /// DungeonStubReturn: it launches SceneRouter.GoBattle on hero proximity
        /// (with an F-key fallback) and round-trips back to this granary scene.
        /// </summary>
        private static void BuildEncounterTrigger(Transform parent)
        {
            // Just inside the N-wall doorway — the Keeper crosses it on the
            // approach to the exit pad.
            float ex = 0f;
            float ez = MaxZ - 6f;

            var trigGo = new GameObject("Encounter_granary-hollow");
            trigGo.transform.SetParent(parent, false);
            trigGo.transform.position = new Vector3(ex, Y, ez);

            // Visible trigger zone — a thin emissive disc so the Encounters
            // root reads in the editor.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "TriggerZone";
            disc.transform.SetParent(trigGo.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            disc.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
            StripColliders(disc);
            ApplyEmissive(disc, HexColor("8a3a2a"), 0.6f);

            // A trigger volume so the hero's CharacterController also fires
            // OnTriggerEnter (belt-and-braces with the component's proximity poll).
            var trigger = trigGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(4.4f, 3f, 4.4f);

            Component comp = AddComponentByName(trigGo, TypeDungeonStubEncounter);
            if (comp != null)
            {
                _encounterCount++;
                // Wire the round-trip: the battle returns to THIS granary scene.
                // _triggerRadius matches the visible disc footprint (~2.2 radius).
                SetFloatField(comp, "_triggerRadius", 2.4f);
                SetStringField(comp, "_returnScene", DungeonReturnScene);
                SetStringArrayField(comp, "_enemyTypes",
                    new[] { "hollow_villager_a", "hollow_villager_b" });
            }
            else
            {
                Debug.LogWarning(
                    "[FolksGranaryBuilder] DungeonStubEncounter type not found — the " +
                    "granary encounter zone will not launch a battle. Is DeNelle.Dungeons compiled?");
            }
        }

        /// <summary>The scene the granary battle round-trips back to.</summary>
        private const string DungeonReturnScene = "Dungeon_FolksGranary";

        /// <summary>
        /// The exit pad — a violet cylinder at the N-wall doorway, carrying a
        /// DungeonStubReturn (DeNelle.Dungeons, by reflection) that routes the
        /// Keeper home to the village on trigger entry (or on F-key proximity).
        /// </summary>
        private static void BuildExitPad(Transform parent)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "ExitPad";
            pad.transform.SetParent(parent, false);
            pad.transform.position = new Vector3(0f, Y + 0.05f, MaxZ - 1.5f);
            pad.transform.localScale = new Vector3(3f, 0.1f, 3f);
            ApplyEmissive(pad, new Color(0.78f, 0.55f, 1f, 0.85f), 1.2f);
            UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());

            // Trigger volume — the DungeonStubReturn OnTriggerEnter contract.
            var trigger = pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(3f, 3f, 3f);

            // The return-to-village component, by reflection.
            AddComponentByName(pad, TypeDungeonStubReturn);
        }

        // =====================================================================
        //  UIDocument sign
        // =====================================================================

        private static void BuildUIDocumentSign(Transform parent)
        {
            var go = new GameObject("DungeonSign");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<UIDocument>();

            // Borrow any project PanelSettings — same fallback the stub builder
            // uses; the Healer's Cottage path resolves a dedicated dungeon
            // panel asset, but the granary has no dedicated one yet.
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
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.borderTopLeftRadius = 10; card.style.borderTopRightRadius = 10;
            card.style.borderBottomLeftRadius = 10; card.style.borderBottomRightRadius = 10;
            card.style.backgroundColor = new StyleColor(new Color(0.10f, 0.07f, 0.04f, 0.92f));
            card.style.borderTopWidth = 1; card.style.borderBottomWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(0.95f, 0.78f, 0.45f, 1f));
            card.style.borderBottomColor = new StyleColor(new Color(0.95f, 0.78f, 0.45f, 1f));
            card.pickingMode = PickingMode.Ignore;
            root.Add(card);

            var h = new Label(SignTitle + " — " + SignFlavor);
            h.style.color = new StyleColor(new Color(1f, 0.96f, 0.88f, 1f));
            h.style.fontSize = 18;
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(h);

            var hint = new Label("Stand on the violet exit pad at the north wall to return to the village.");
            hint.style.color = new StyleColor(new Color(0.78f, 0.74f, 0.62f, 1f));
            hint.style.fontSize = 11;
            hint.style.marginTop = 4;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(hint);
        }

        // =====================================================================
        //  Camera + ambient light + EventSystem
        // =====================================================================

        private static void CreateCamera()
        {
            // Real Main Camera — receives the CinemachineBrain blend. The
            // FollowCameraRig built below carries the virtual CinemachineCamera
            // that drives this real camera's transform every frame.
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexColor("0d0a06");
            cam.fieldOfView = 50f;
            go.AddComponent<AudioListener>();
            // Static seat — CinemachineBrain hands control to the follow rig
            // the moment play starts.
            go.transform.position = new Vector3(0f, 14f, -16f);
            go.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

            if (AddComponentByName(go, TypeCinemachineBrain) == null)
            {
                Debug.LogWarning(
                    "[FolksGranaryBuilder] Could not add a CinemachineBrain to the Main " +
                    "Camera — the dungeon follow rig will not drive the view. Is the " +
                    "Unity.Cinemachine package installed?");
            }

            // Virtual camera with the dungeon's isometric follow rig — auto-binds
            // to the DungeonHero in Start() (DungeonCameraRig fallback) since
            // the granary has no DungeonController to call Bind() explicitly.
            var rig = new GameObject("FollowCameraRig");
            rig.transform.position = new Vector3(0f, 14f, -16f);
            rig.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
            if (AddComponentByName(rig, TypeCinemachineCamera) == null)
            {
                Debug.LogError(
                    "[FolksGranaryBuilder] Could not add CinemachineCamera — the follow " +
                    "rig is incomplete; the player will see the static seat only.");
                return;
            }
            AddComponentByName(rig, TypeCinemachineFollow);
            AddComponentByName(rig, TypeDungeonCameraRig);
        }

        private static void CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            // A very faint warm key — the oil lanterns carry the real light.
            light.color = HexColor("4a3a26");
            light.intensity = 0.22f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(58f, 35f, 0f);
        }

        private static void ConfigureAmbient()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.06f, 0.05f);
            RenderSettings.ambientIntensity = 0.08f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = HexColor("130d08");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 38f;
        }

        private static void LitFixture(Transform parent, string name, Vector3 pos,
            Color color, float intensity, float range)
        {
            var go = new GameObject($"LitFixture_{name}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            _lightCount++;
        }

        private static void EnsureEventSystem()
        {
            var esType = System.Type.GetType(
                "UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            if (esType == null) return;
            var existing = UnityEngine.Object.FindAnyObjectByType(esType);
            if (existing != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent(esType);

            var moduleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) go.AddComponent(moduleType);
        }

        // =====================================================================
        //  Geometry / collider / colour helpers
        // =====================================================================

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Prop(Transform parent, string fbx, float x, float y, float z,
            float yaw)
        {
            Prop(parent, fbx, x, y, z, yaw, Color.white);
        }

        private static void Prop(Transform parent, string fbx, float x, float y, float z,
            float yaw, Color tint)
        {
            var model = LoadModel(PackRoot + fbx);
            var go = InstantiateModel(model, fbx, $"prop {fbx}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (tint != Color.white) ApplyTint(go, tint);
            EnsureCollider(go);
            _propCount++;
            if (model != null) _modelCount++;
        }

        private static void Placeholder(Transform parent, string label, Vector3 pos,
            Color color, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"[PLACEHOLDER] {label}";
            cube.transform.SetParent(parent, false);
            cube.transform.position = pos;
            cube.transform.localScale = scale;
            ApplyTint(cube, color);
            NotePlaceholder(label);
        }

        private static GameObject MakePlaceholderCube(string label)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"[PLACEHOLDER] {label}";
            NotePlaceholder(label);
            return cube;
        }

        private static void NotePlaceholder(string label)
        {
            _placeholderCount++;
            if (_placeholders.Count < 32) _placeholders.Add(label);
            Debug.LogWarning(
                $"[FolksGranaryBuilder] KayKit asset missing / no mesh — placeholder used for: {label}");
        }

        private static GameObject LoadModel(string assetPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model != null) return model;

            string asPrefab = Path.ChangeExtension(assetPath, ".prefab")?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(asPrefab))
            {
                model = AssetDatabase.LoadAssetAtPath<GameObject>(asPrefab);
                if (model != null) return model;
            }
            return null;
        }

        private static GameObject InstantiateModel(GameObject model, string assetLabel,
            string placeholderLabel)
        {
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (instance != null)
                {
                    instance.name = model.name;
                    return instance;
                }
            }
            return MakePlaceholderCube($"{assetLabel} -> {placeholderLabel}");
        }

        private static void EnsureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                go.AddComponent<BoxCollider>();
                return;
            }
            bool any = false;
            Bounds b = default;
            foreach (var rdr in renderers)
            {
                if (rdr == null) continue;
                if (!any) { b = rdr.bounds; any = true; }
                else b.Encapsulate(rdr.bounds);
            }
            var bc = go.AddComponent<BoxCollider>();
            if (any)
            {
                bc.center = go.transform.InverseTransformPoint(b.center);
                bc.size = b.size;
            }
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(c);
        }

        private static void ApplyTint(GameObject go, Color color)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
        }

        private static void ApplyEmissive(GameObject go, Color color, float intensity)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            renderer.sharedMaterial = mat;
        }

        private static Color HexColor(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out var c) ? c : Color.magenta;
        }

        // =====================================================================
        //  Reflection helpers
        // =====================================================================

        private static Component AddComponentByName(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogWarning($"[FolksGranaryBuilder] Type '{fullTypeName}' not found — " +
                                 "is the owning assembly compiled? Component skipped.");
                return null;
            }
            var existing = go.GetComponent(type);
            return existing != null ? existing : go.AddComponent(type);
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

        private static void SetSerializedObjectRef(Component comp, string field,
            UnityEngine.Object value)
        {
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[FolksGranaryBuilder] Serialized field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} — wiring skipped for that field.");
                return;
            }
            if (value != null && prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning($"[FolksGranaryBuilder] Field '{field}' is not an object reference.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloatField(Component comp, string field, float value)
        {
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null || prop.propertyType != SerializedPropertyType.Float)
            {
                Debug.LogWarning($"[FolksGranaryBuilder] Float field '{field}' not found / wrong type — skipped.");
                return;
            }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStringField(Component comp, string field, string value)
        {
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null || prop.propertyType != SerializedPropertyType.String)
            {
                Debug.LogWarning($"[FolksGranaryBuilder] String field '{field}' not found / wrong type — skipped.");
                return;
            }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStringArrayField(Component comp, string field, string[] values)
        {
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[FolksGranaryBuilder] Array field '{field}' not found / not an array — skipped.");
                return;
            }
            prop.arraySize = values?.Length ?? 0;
            for (int i = 0; i < prop.arraySize; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        //  Build Settings + folder helpers
        // =====================================================================

        private static void EnsureBuildSettings()
        {
            var current = EditorBuildSettings.scenes;
            foreach (var s in current)
                if (string.Equals(s.path, DungeonScenePath,
                                  StringComparison.OrdinalIgnoreCase)) return;

            var scenes = new List<EditorBuildSettingsScene>(current);
            scenes.Add(new EditorBuildSettingsScene(DungeonScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // =====================================================================
        //  Summary log
        // =====================================================================

        private static void LogSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[FolksGranaryBuilder] Folk's Old Granary (D2) built.");
            sb.AppendLine($"  Scene: {DungeonScenePath}");
            sb.AppendLine($"  KayKit model instances: {_modelCount} " +
                          $"(walls {_wallCount}, floor tiles {_floorCount}, props {_propCount}).");
            sb.AppendLine($"  Static lights: {_lightCount}.");
            sb.AppendLine($"  EncounterTrigger components: {_encounterCount}.");
            sb.AppendLine($"  AmbientNPC components (Bryn-style): {_brynCount}.");
            sb.AppendLine($"  LoreStone components: {_loreStoneCount} (granary ships none — design parity placeholder).");
            sb.AppendLine($"  Placeholder primitives: {_placeholderCount}.");
            foreach (var p in _placeholders) sb.AppendLine($"    - {p}");
            sb.AppendLine("  Entry point: DeNelle.Editor.FolksGranaryBuilder.BuildFolksGranary");
            Debug.Log(sb.ToString());
        }
    }
}
