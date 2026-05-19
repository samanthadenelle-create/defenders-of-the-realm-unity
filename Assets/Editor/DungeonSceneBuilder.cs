// =============================================================================
// DungeonSceneBuilder — Healer's Cottage dungeon (D1) scene generator (Editor).
// -----------------------------------------------------------------------------
// One static entry point the main Unity session runs (manually via the
// Defenders menu, or via the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.DungeonSceneBuilder.BuildHealersCottage
//
// What it does — implements docs/dungeons-3d-unity-layout-spec.md §9 (the
// EXPANDED Healer's Cottage): 3 levels, 12 rooms, built from KayKit Dungeon
// Remastered 1.1 modular pieces.
//
//   §9.1  Ground floor (6 rooms): Garden Approach, Entrance Room, Main Room /
//         Hearth, Kitchen, Pantry Alcove, Workshop (boss room).
//   §9.2  Upper floor (2 rooms, ladder up from Main Room): Loft Bedroom,
//         Loft Study.
//   §9.3  Underground (4 rooms, trapdoor / stair down): Root Cellar, Storage,
//         Crypt Sub-Level, Hidden Vault.
//   §5    Room taxonomy archetypes; §6.1/§6.2/§6.3 asset palettes; §7
//         lantern-low ambient lighting; §8 the 60/20/20 encounter mix.
//
// HARD-LOCKED content (verbatim — never paraphrased), sourced from
// docs/dungeon-3d-healers-cottage-design.md:
//   - Bryn's Tier-1 entry line (spec §12 / design §4 Beat 1).
//   - The five lore-stone journal entries (design §4 Beats 2/3/5/6 + the
//     redacted Hidden-Vault draft).
//   - The mini-boss spec — "The Apprentice of the Apothecary" (design §4 Beat 6).
//
// IDEMPOTENT — re-running BuildHealersCottage() destroys every object under the
// generated "DungeonRoot" (plus the Main Camera / Directional Light) and
// rebuilds from scratch. Safe to repeat.
//
// ASSEMBLY NOTE. DeNelle.Editor.asmdef references only Core / Data /
// Localization / URP — NOT DeNelle.Dungeons. So this builder takes NO
// compile-time dependency on the dungeon MonoBehaviours: DungeonController,
// Lantern, LoreStone, Checkpoint, EncounterTrigger, Bryn and the
// DungeonRuntimeState ScriptableObject are added / created by REFLECTION
// (resolved by full type name) and their [SerializeField] fields are set
// through SerializedObject — the exact pattern VillageSceneBuilder uses.
//
// KAYKIT MODELS. Built from "KayKit Dungeon Remastered 1.1" under
// Assets/Models/KayKit/.../Assets/fbx(unity)/. Each model loads via
// AssetDatabase.LoadAssetAtPath<GameObject>; on a missed path the builder
// substitutes a clearly-labelled placeholder primitive and logs a warning. It
// NEVER blocks. The pack ships no door / ladder / trapdoor / sarcophagus mesh,
// so those use labelled placeholders by design (logged for the decisions log).
//
// This script does NOT run Unity itself; the main session triggers it. It does
// NOT touch any .asmdef file.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that assembles the expanded Healer's Cottage dungeon scene
    /// (D1) from KayKit Dungeon Remastered models, per
    /// docs/dungeons-3d-unity-layout-spec.md §9. Entry point:
    /// <see cref="BuildHealersCottage"/>.
    /// </summary>
    public static class DungeonSceneBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";
        private const string DungeonScenePath = ScenesDir + "/Dungeon_HealersCottage.unity";
        private const string TitleScenePath = ScenesDir + "/Title.unity";
        private const string VillageScenePath = ScenesDir + "/Village.unity";
        private const string BattleScenePath = ScenesDir + "/ATBBattle.unity";

        private const string RuntimeStateDir = "Assets/_Modules/Dungeons/State";
        private const string RuntimeStateAssetPath =
            RuntimeStateDir + "/HealersCottageRuntimeState.asset";

        /// <summary>Root for everything this builder generates — cleared + rebuilt each run.</summary>
        private const string DungeonRootName = "DungeonRoot";

        // ── KayKit Dungeon Remastered 1.1 paths ──────────────────────────────
        // The folder name literally contains "(unity)" — parentheses are valid
        // in Unity asset paths, so LoadAssetAtPath handles them directly.
        private const string PackRoot =
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx(unity)/";

        // ── Dungeon MonoBehaviour / SO type names (resolved by reflection) ───
        private const string NsDungeons = "DeNelle.Dungeons";
        private const string TypeDungeonController = NsDungeons + ".DungeonController";
        private const string TypeDungeonHero = NsDungeons + ".DungeonHero";
        private const string TypeDungeonCameraRig = NsDungeons + ".DungeonCameraRig";
        private const string TypeLantern = NsDungeons + ".Lantern";
        private const string TypeLoreStone = NsDungeons + ".LoreStone";
        private const string TypeCheckpoint = NsDungeons + ".Checkpoint";
        private const string TypeEncounterTrigger = NsDungeons + ".EncounterTrigger";
        private const string TypeBryn = NsDungeons + ".Bryn";
        private const string TypeWandererBubble = NsDungeons + ".WandererBubble";
        private const string TypeRuntimeState = NsDungeons + ".DungeonRuntimeState";
        // Workstream C — dungeon crafting system + oil HUD.
        private const string TypeDungeonInventory = NsDungeons + ".DungeonInventory";
        private const string TypeIngredientPickup = NsDungeons + ".IngredientPickup";
        private const string TypeCraftingPedestal = NsDungeons + ".CraftingPedestal";
        private const string TypeCraftingPanelController = NsDungeons + ".CraftingPanelController";
        private const string TypeDungeonHudController = NsDungeons + ".DungeonHudController";

        // ── Workstream C — crafting / HUD asset paths ────────────────────────
        private const string InventoryAssetPath =
            RuntimeStateDir + "/HealersCottageInventory.asset";
        private const string CraftingPanelUxmlPath =
            "Assets/_Modules/Dungeons/UI/CraftingPanel.uxml";
        private const string DungeonHudUxmlPath =
            "Assets/_Modules/Dungeons/UI/DungeonHud.uxml";
        private const string DungeonGeneratedDir = "Assets/_Modules/Dungeons/Generated";
        private const string DungeonPanelSettingsPath =
            DungeonGeneratedDir + "/DungeonPanelSettings.asset";

        // ── Cinemachine type names (resolved by reflection — the DeNelle.Editor
        //    asmdef does NOT reference Unity.Cinemachine; the package assembly is
        //    nonetheless always loaded in the editor, so FindType resolves it —
        //    the same pattern BattleSceneBuilder uses for the Input System UI
        //    module). Cinemachine 3.x type names.
        private const string TypeCinemachineCamera = "Unity.Cinemachine.CinemachineCamera";
        private const string TypeCinemachineBrain = "Unity.Cinemachine.CinemachineBrain";
        private const string TypeCinemachineFollow = "Unity.Cinemachine.CinemachineFollow";

        // ── Level Y-planes (§4.1 Pattern A — Surface + Cellar + Loft) ────────
        private const float YGround = 0f;
        private const float YUpper = 6f;
        private const float YUnder = -6f;

        // ── Lighting (§7 — the lantern is the rule) ──────────────────────────
        private const float AmbientIntensity = 0.05f;   // §7 ambient floor
        private const float WallHeight = 4f;            // KayKit wall ~4u tall
        private const float WallSegLen = 4f;            // KayKit wall piece ~4u long

        // ── Cozy / apothecary / fortress palette tints (§6.1/§6.2/§6.3) ──────
        private static readonly Color TintCozy = HexColor("d8c69a");      // amber/honey
        private static readonly Color TintApothecary = HexColor("5e8c86"); // muted teal
        private static readonly Color TintFortress = HexColor("8b8f99");   // cool grey
        private static readonly Color TintCave = HexColor("9fb4c4");       // ice-grey

        // ── Hard-locked Bryn line (spec §12 Tier 1 — design §4 Beat 1) ───────
        // VERBATIM from docs/dungeon-3d-healers-cottage-design.md §4 Beat 1.
        private const string BrynTier1Line =
            "The path opens easy, Keeper. But mind the rocks — they remember you. " +
            "And don't walk it dark. The cottage keeps her shadows close.";

        // ── Running tallies for the summary log ──────────────────────────────
        private static int _placeholderCount;
        private static readonly List<string> _placeholders = new List<string>();
        private static int _modelCount;
        private static int _wallCount;
        private static int _floorCount;
        private static int _propCount;
        private static int _lightCount;

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the expanded Healer's Cottage dungeon scene per
        /// docs/dungeons-3d-unity-layout-spec.md §9. Runnable via
        /// <c>-executeMethod DeNelle.Editor.DungeonSceneBuilder.BuildHealersCottage</c>.
        /// Idempotent.
        /// </summary>
        [MenuItem("Defenders/Dungeons/Build Healer's Cottage (D1)")]
        public static void BuildHealersCottage()
        {
            _placeholderCount = 0;
            _placeholders.Clear();
            _modelCount = 0;
            _wallCount = 0;
            _floorCount = 0;
            _propCount = 0;
            _lightCount = 0;

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
                if (go.name == DungeonRootName || go.name == "Main Camera" ||
                    go.name == "Directional Light")
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            // ── §7 ambient lighting floor — the lantern is the dominant light ─
            ConfigureAmbient();

            // ── Scene root + sub-roots ───────────────────────────────────────
            var root = new GameObject(DungeonRootName);
            var levelGround = NewChild(root.transform, "Level_Ground");
            var levelUpper = NewChild(root.transform, "Level_Upper");
            var levelUnder = NewChild(root.transform, "Level_Underground");
            var verticalRoot = NewChild(root.transform, "VerticalConnectors");
            var lightRoot = NewChild(root.transform, "Lighting");
            var loreRoot = NewChild(root.transform, "LoreStones");
            var checkpointRoot = NewChild(root.transform, "Checkpoints");
            var encounterRoot = NewChild(root.transform, "Encounters");
            var actorRoot = NewChild(root.transform, "Actors");

            // ── Camera (top-down isometric — matches DungeonController tilt) ──
            CreateCamera();
            // A faint key light so geometry is not pitch-black before play —
            // §7: real illumination is the Keeper's lantern at run time.
            CreateDirectionalLight();

            // =================================================================
            //  Build the 12 rooms across 3 levels (§9.1 / §9.2 / §9.3)
            // =================================================================

            foreach (var r in Rooms)
            {
                Transform levelParent =
                    r.Level == Level.Upper ? levelUpper :
                    r.Level == Level.Underground ? levelUnder : levelGround;
                BuildRoom(r, levelParent, lightRoot);
            }

            // ── Vertical connectors (§4.2 — stairs as architecture) ──────────
            BuildVerticalConnectors(verticalRoot);

            // =================================================================
            //  Runtime-state ScriptableObject (reflection — created on disk)
            // =================================================================
            UnityEngine.Object runtimeState = EnsureRuntimeStateAsset();

            // =================================================================
            //  The Keeper rig + hero-attached lantern (§7)
            //  BuildHeroRig now also wires the Week-5 DungeonHero locomotion
            //  controller onto the Keeper (reflection — DeNelle.Dungeons).
            // =================================================================
            var heroGo = BuildHeroRig(actorRoot);
            Component heroController = GetComponentByName(heroGo, TypeDungeonHero);
            Component lantern = BuildLantern(heroGo);

            // =================================================================
            //  Cinemachine follow camera rig (Week 5) — a CinemachineCamera +
            //  CinemachineFollow + DungeonCameraRig, all added by reflection.
            // =================================================================
            var camRig = BuildFollowCameraRig(actorRoot);
            Component cameraRig = GetComponentByName(camRig, TypeDungeonCameraRig);
            Component followCamera = GetComponentByName(camRig, TypeCinemachineCamera);

            // =================================================================
            //  Dungeon-module interactables — placed per §9, wired by reflection
            // =================================================================
            BuildLoreStones(loreRoot, runtimeState, heroGo.transform);
            BuildCheckpoints(checkpointRoot, runtimeState, heroGo.transform);
            BuildEncounters(encounterRoot, runtimeState);
            BuildChests(actorRoot, runtimeState);
            Component bryn = BuildBryn(actorRoot, runtimeState);
            BuildMiniBoss(actorRoot);

            // =================================================================
            //  Workstream C — dungeon crafting system + lantern oil HUD
            // =================================================================
            // The shared crafting inventory ScriptableObject (reflection — the
            // type lives in DeNelle.Dungeons).
            UnityEngine.Object dungeonInventory = EnsureInventoryAsset();

            // Ingredient pickups — one mote per crafting-recipes.json placement.
            var ingredientRoot = NewChild(root.transform, "IngredientPickups");
            BuildIngredientPickups(ingredientRoot, dungeonInventory, heroGo.transform);

            // The crafting pedestal — replaces the §5.4 placeholder shard
            // pedestal primitive in the Hidden Vault with the real interactable.
            Component craftingPedestal =
                BuildCraftingPedestal(actorRoot, dungeonInventory, heroGo.transform);

            // The dungeon HUD (oil meter) + crafting-panel UIDocuments.
            UnityEngine.UIElements.PanelSettings dungeonPanel = EnsureDungeonPanelSettings();
            Component dungeonHud = BuildDungeonHud(root.transform, dungeonPanel);
            Component craftingPanel = BuildCraftingPanel(root.transform, dungeonPanel);

            // ── Ambient BGM source (echoes-beneath-elarion — stub, §11) ──────
            // The clip is left unassigned — echoes-beneath-elarion.mp3 is not in
            // the project. DungeonController.StartAmbientAudio() guards a null
            // clip (logs a warning, plays silently). See dungeon-integration.md.
            var bgmGo = new GameObject("AmbientBGM");
            bgmGo.transform.SetParent(actorRoot, false);
            var bgm = bgmGo.AddComponent<AudioSource>();
            bgm.loop = true;
            bgm.playOnAwake = false;
            bgm.spatialBlend = 0f;        // 2D — an ambient bed, not a point source
            bgm.volume = 0.25f;           // audio-mix-spec §2 dungeon default

            // ── BUG-007 hardening — every wall/structure mesh carries a collider
            VerifyWallColliders(root.transform);

            // =================================================================
            //  DungeonController orchestrator — wired by reflection
            // =================================================================
            var controllerGo = new GameObject("DungeonController");
            controllerGo.transform.SetParent(root.transform, false);
            Component controller = AddDungeonComponent(controllerGo, TypeDungeonController);
            WireController(
                controller, runtimeState, heroGo.transform, heroController,
                followCamera, cameraRig, lantern, bryn,
                loreRoot, checkpointRoot, encounterRoot, bgm,
                dungeonInventory, craftingPedestal, ingredientRoot,
                craftingPanel, dungeonHud);

            // ── Save + register in Build Settings ────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, DungeonScenePath);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogSummary();
        }

        // =====================================================================
        //  Room model (§5 taxonomy + §6 palette + §9 layout)
        // =====================================================================

        private enum Level { Ground, Upper, Underground }

        /// <summary>The §6 asset-palette register a room dresses to.</summary>
        private enum Palette { Cozy, Apothecary, Fortress, Cave }

        /// <summary>A doorway gap in a room perimeter wall — wires room-to-room.</summary>
        private sealed class Doorway
        {
            public string Side;        // "N" | "S" | "E" | "W"
            public float Offset;       // gap centre offset from the wall's start
            public bool Illusory;      // an illusory (no-collider) hidden passage
        }

        private sealed class RoomDef
        {
            public string Id;
            public string Title;
            public Level Level;
            public Palette Palette;
            public string Taxonomy;     // §5 archetype label
            // Footprint on the floor plane — X east(+)/west(-), Z south(+)/north(-).
            public float MinX, MaxX, MinZ, MaxZ;
            public bool Ceiling = true; // close the top (§3) — false for outdoor
            public bool DarkFloor;      // §7 — no static room light (lantern-only)
            public Doorway[] Doors = Array.Empty<Doorway>();
        }

        // ── The 12 rooms (§9). Coordinates: X east(+)/west(-), Z south(+)/    ─
        //    north(-), ~6u cells. Keeper enters the Garden Approach (SW).      ─
        private static readonly RoomDef[] Rooms =
        {
            // ── §9.1 Ground floor — 6 rooms ──────────────────────────────────
            new RoomDef {
                Id = "garden-approach", Title = "Garden Approach", Level = Level.Ground,
                Palette = Palette.Cozy, Taxonomy = "Entrance Room (§5.1)",
                MinX = -36f, MaxX = -20f, MinZ = -8f, MaxZ = 8f,
                Ceiling = false, // outdoor courtyard — sun from the open sky
                Doors = new[] {
                    new Doorway { Side = "E", Offset = 8f }, // -> entrance-room
                },
            },
            new RoomDef {
                Id = "entrance-room", Title = "Entrance Room", Level = Level.Ground,
                Palette = Palette.Cozy, Taxonomy = "Entrance / Checkpoint (§5.1/§5.7)",
                MinX = -20f, MaxX = -4f, MinZ = -8f, MaxZ = 8f,
                Doors = new[] {
                    new Doorway { Side = "W", Offset = 8f }, // -> garden-approach
                    new Doorway { Side = "E", Offset = 8f }, // -> main-room
                },
            },
            new RoomDef {
                Id = "main-room", Title = "Main Room / Hearth", Level = Level.Ground,
                Palette = Palette.Cozy, Taxonomy = "Combat Room (§5.3)",
                MinX = -4f, MaxX = 16f, MinZ = -10f, MaxZ = 10f,
                Doors = new[] {
                    new Doorway { Side = "W", Offset = 10f }, // -> entrance-room
                    new Doorway { Side = "E", Offset = 6f },  // -> kitchen
                },
            },
            new RoomDef {
                Id = "kitchen", Title = "Kitchen", Level = Level.Ground,
                Palette = Palette.Cozy, Taxonomy = "Dressing Room (§5.2)",
                MinX = 16f, MaxX = 30f, MinZ = -10f, MaxZ = 4f,
                Doors = new[] {
                    new Doorway { Side = "W", Offset = 4f }, // -> main-room
                    new Doorway { Side = "S", Offset = 7f }, // -> pantry-alcove
                },
            },
            new RoomDef {
                Id = "pantry-alcove", Title = "Pantry Alcove", Level = Level.Ground,
                Palette = Palette.Cozy, Taxonomy = "Lore Room (§5.2)",
                MinX = 16f, MaxX = 26f, MinZ = 4f, MaxZ = 14f,
                Doors = new[] {
                    new Doorway { Side = "N", Offset = 7f }, // -> kitchen
                    new Doorway { Side = "E", Offset = 5f }, // -> workshop
                },
            },
            new RoomDef {
                Id = "workshop", Title = "Workshop", Level = Level.Ground,
                Palette = Palette.Apothecary, Taxonomy = "Boss Room (§5.8)",
                MinX = 26f, MaxX = 46f, MinZ = -6f, MaxZ = 14f,
                DarkFloor = true, // §6.2 — deliberately dim apothecary
                Doors = new[] {
                    new Doorway { Side = "W", Offset = 14f }, // -> pantry-alcove
                },
            },

            // ── §9.2 Upper floor — 2 rooms (ladder up from Main Room) ────────
            new RoomDef {
                Id = "loft-bedroom", Title = "Loft Bedroom", Level = Level.Upper,
                Palette = Palette.Cozy, Taxonomy = "Lore Room (§5.2)",
                MinX = -4f, MaxX = 10f, MinZ = -10f, MaxZ = 4f,
                Doors = new[] {
                    new Doorway { Side = "E", Offset = 7f }, // -> loft-study
                },
            },
            new RoomDef {
                Id = "loft-study", Title = "Loft Study", Level = Level.Upper,
                Palette = Palette.Cozy, Taxonomy = "Lore Room — lantern-reveal (§5.2)",
                MinX = 10f, MaxX = 22f, MinZ = -10f, MaxZ = 4f,
                Doors = new[] {
                    // illusory: a second hidden lore stone, revealed by lantern light
                    new Doorway { Side = "W", Offset = 7f, Illusory = false },
                },
            },

            // ── §9.3 Underground — 4 rooms (trapdoor / stair down) ───────────
            new RoomDef {
                Id = "root-cellar", Title = "Root Cellar", Level = Level.Underground,
                Palette = Palette.Fortress, Taxonomy = "Treasure / Combat (§5.4/§5.3)",
                MinX = -20f, MaxX = -4f, MinZ = -8f, MaxZ = 8f,
                DarkFloor = true, // §4.1 — underground is lantern-mandatory
                Doors = new[] {
                    new Doorway { Side = "E", Offset = 8f }, // -> storage
                },
            },
            new RoomDef {
                Id = "storage", Title = "Storage", Level = Level.Underground,
                Palette = Palette.Fortress, Taxonomy = "Treasure / Combat (§5.4/§5.3)",
                MinX = -4f, MaxX = 12f, MinZ = -8f, MaxZ = 8f,
                DarkFloor = true,
                Doors = new[] {
                    new Doorway { Side = "W", Offset = 8f }, // -> root-cellar
                    new Doorway { Side = "S", Offset = 8f }, // -> crypt-sublevel
                },
            },
            new RoomDef {
                Id = "crypt-sublevel", Title = "Crypt Sub-Level", Level = Level.Underground,
                Palette = Palette.Fortress, Taxonomy = "Trap / Checkpoint (§5.5/§5.7)",
                MinX = -4f, MaxX = 12f, MinZ = 8f, MaxZ = 26f,
                DarkFloor = true,
                Doors = new[] {
                    new Doorway { Side = "N", Offset = 8f }, // -> storage
                    // illusory wall — light-puzzle reveal into the Hidden Vault
                    new Doorway { Side = "E", Offset = 9f, Illusory = true },
                },
            },
            new RoomDef {
                Id = "hidden-vault", Title = "Hidden Vault", Level = Level.Underground,
                Palette = Palette.Cave, Taxonomy = "Treasure — secret (§5.4)",
                MinX = 12f, MaxX = 24f, MinZ = 12f, MaxZ = 24f,
                DarkFloor = true,
                Doors = new[] {
                    new Doorway { Side = "W", Offset = 9f, Illusory = true }, // -> crypt
                },
            },
        };

        private static RoomDef FindRoom(string id)
        {
            foreach (var r in Rooms) if (r.Id == id) return r;
            return null;
        }

        // =====================================================================
        //  Room construction — perimeter walls + floor + ceiling + dressing
        // =====================================================================

        private static void BuildRoom(RoomDef r, Transform levelParent, Transform lightRoot)
        {
            float y = LevelY(r.Level);
            var roomGo = new GameObject($"Room_{r.Id}");
            roomGo.transform.SetParent(levelParent, false);
            roomGo.transform.position = new Vector3(0f, y, 0f);

            BuildFloor(r, roomGo.transform, y);
            BuildPerimeter(r, roomGo.transform, y);
            if (r.Ceiling) BuildCeiling(r, roomGo.transform, y);
            DressRoom(r, roomGo.transform, y, lightRoot);
        }

        /// <summary>Tiles the room footprint with the palette's floor piece.</summary>
        private static void BuildFloor(RoomDef r, Transform parent, float y)
        {
            string floorFbx = FloorPiece(r.Palette);
            var model = LoadModel(PackRoot + floorFbx);
            var floorRoot = NewChild(parent, "Floor");

            const float tile = 4f; // KayKit floor tile ~4u
            for (float x = r.MinX + tile * 0.5f; x < r.MaxX; x += tile)
            for (float z = r.MinZ + tile * 0.5f; z < r.MaxZ; z += tile)
            {
                var go = InstantiateModel(model, floorFbx, $"floor {r.Id}");
                go.transform.SetParent(floorRoot, false);
                go.transform.position = new Vector3(x, y, z);
                EnsureCollider(go);
                _floorCount++;
                if (model != null) _modelCount++;
            }
        }

        /// <summary>
        /// Lays the room's four perimeter wall runs from KayKit wall pieces.
        /// Doorway gaps wire the room to its neighbour; illusory gaps render a
        /// wall piece but skip its collider (a hidden passage — §5.4/§5.6).
        /// </summary>
        private static void BuildPerimeter(RoomDef r, Transform parent, float y)
        {
            var wallRoot = NewChild(parent, "Walls");
            // S edge (z = MinZ, run +X) ; N edge (z = MaxZ, run +X)
            BuildWallRun(r, wallRoot, "S", new Vector3(r.MinX, y, r.MinZ),
                Vector3.right, r.MaxX - r.MinX, 0f);
            BuildWallRun(r, wallRoot, "N", new Vector3(r.MinX, y, r.MaxZ),
                Vector3.right, r.MaxX - r.MinX, 180f);
            // W edge (x = MinX, run +Z) ; E edge (x = MaxX, run +Z)
            BuildWallRun(r, wallRoot, "W", new Vector3(r.MinX, y, r.MinZ),
                Vector3.forward, r.MaxZ - r.MinZ, -90f);
            BuildWallRun(r, wallRoot, "E", new Vector3(r.MaxX, y, r.MinZ),
                Vector3.forward, r.MaxZ - r.MinZ, 90f);
        }

        private static void BuildWallRun(RoomDef r, Transform parent, string side,
            Vector3 origin, Vector3 dir, float length, float yaw)
        {
            string wallFbx = WallPiece(r.Palette, "wall");
            string doorFbx = WallPiece(r.Palette, "wall_doorway");
            var wallModel = LoadModel(PackRoot + wallFbx);
            var doorModel = LoadModel(PackRoot + doorFbx);

            int segs = Mathf.Max(1, Mathf.RoundToInt(length / WallSegLen));
            float step = length / segs;

            Doorway door = FindDoor(r, side);
            // Which segment index holds the door (offset along the run).
            int doorSeg = door != null
                ? Mathf.Clamp(Mathf.FloorToInt(door.Offset / step), 0, segs - 1)
                : -1;

            for (int i = 0; i < segs; i++)
            {
                float along = (i + 0.5f) * step;
                Vector3 pos = origin + dir * along;
                bool isDoor = i == doorSeg;

                GameObject go;
                if (isDoor && !door.Illusory)
                {
                    // A real doorway — collidable frame around a walk-through gap.
                    go = InstantiateModel(doorModel, doorFbx, $"doorway {r.Id}/{side}");
                    EnsureCollider(go);
                }
                else if (isDoor && door.Illusory)
                {
                    // An illusory wall — renders solid, NO collider (hidden passage).
                    go = InstantiateModel(wallModel, wallFbx, $"illusory wall {r.Id}/{side}");
                    StripColliders(go);
                    go.name = "[ILLUSORY] " + go.name;
                }
                else
                {
                    go = InstantiateModel(wallModel, wallFbx, $"wall {r.Id}/{side}");
                    EnsureCollider(go);
                }

                go.transform.SetParent(parent, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                ApplyTint(go, PaletteTint(r.Palette));
                _wallCount++;
                if (wallModel != null || doorModel != null) _modelCount++;
            }
        }

        private static Doorway FindDoor(RoomDef r, string side)
        {
            foreach (var d in r.Doors) if (d.Side == side) return d;
            return null;
        }

        /// <summary>Closes the room top with ceiling tiles (§3 — true enclosed feel).</summary>
        private static void BuildCeiling(RoomDef r, Transform parent, float y)
        {
            var model = LoadModel(PackRoot + "ceiling_tile.fbx");
            var ceilRoot = NewChild(parent, "Ceiling");
            const float tile = 4f;
            float top = y + WallHeight;
            for (float x = r.MinX + tile * 0.5f; x < r.MaxX; x += tile)
            for (float z = r.MinZ + tile * 0.5f; z < r.MaxZ; z += tile)
            {
                var go = InstantiateModel(model, "ceiling_tile.fbx", $"ceiling {r.Id}");
                go.transform.SetParent(ceilRoot, false);
                go.transform.position = new Vector3(x, top, z);
                StripColliders(go);
                if (model != null) _modelCount++;
            }
        }

        // =====================================================================
        //  Per-room dressing (§6 palettes) + static lit-fixture lights (§7)
        // =====================================================================

        private static void DressRoom(RoomDef r, Transform parent, float y, Transform lightRoot)
        {
            var dress = NewChild(parent, "Dressing");
            float cx = (r.MinX + r.MaxX) * 0.5f;
            float cz = (r.MinZ + r.MaxZ) * 0.5f;

            switch (r.Id)
            {
                case "garden-approach":
                    // §5.1 — outdoor approach: dirt, rocks, a lantern post.
                    Prop(dress, "rocks_small.fbx", cx - 4f, y, cz + 2f, 0f);
                    Prop(dress, "rocks.fbx", cx + 3f, y, cz - 3f, 40f);
                    Prop(dress, "barrel_small.fbx", r.MaxX - 3f, y, r.MinZ + 3f, 0f);
                    Placeholder(dress, "lantern_post (no KayKit mesh)",
                        new Vector3(cx - 5f, y, cz - 4f), HexColor("ffcf6b"),
                        new Vector3(0.4f, 2.2f, 0.4f));
                    LitFixture(lightRoot, "GardenSky", new Vector3(cx, y + 5f, cz),
                        HexColor("fff2cc"), 1.1f, 14f); // partial daylight (§7 exception)
                    break;

                case "entrance-room":
                    // §5.1/§5.7 — threshold + first checkpoint. Dust, a chair, a rug.
                    Prop(dress, "chair.fbx", r.MinX + 3f, y, r.MaxZ - 3f, 35f);
                    Prop(dress, "shelf_small_candles.fbx", r.MinX + 2f, y, cz, 90f);
                    Placeholder(dress, "rug over hidden trapdoor",
                        new Vector3(cx + 4f, y + 0.02f, cz + 3f), HexColor("8a5a3a"),
                        new Vector3(3.5f, 0.05f, 3.5f));
                    Placeholder(dress, "trapdoor (lantern-revealed, no KayKit mesh)",
                        new Vector3(cx + 4f, y + 0.04f, cz + 3f), HexColor("5a3a22"),
                        new Vector3(2.2f, 0.1f, 2.2f));
                    LitFixture(lightRoot, "EntranceCandle",
                        new Vector3(r.MinX + 2f, y + 1.4f, cz), HexColor("ffd98a"), 2.0f, 4f);
                    break;

                case "main-room":
                    // §5.3 — the hearth. Worktable, shelves, ladder up to the loft.
                    Prop(dress, "table_long_decorated_A.fbx", cx, y, cz, 0f);
                    Prop(dress, "chair.fbx", cx - 2.5f, y, cz, 90f);
                    Prop(dress, "chair.fbx", cx + 2.5f, y, cz, -90f);
                    Prop(dress, "shelves_decorated.fbx", r.MinX + 2f, y, r.MaxZ - 3f, 90f);
                    Prop(dress, "shelves.fbx", r.MinX + 2f, y, r.MinZ + 3f, 90f);
                    Prop(dress, "barrel_large.fbx", r.MaxX - 3f, y, r.MaxZ - 3f, 0f);
                    Placeholder(dress, "hearth fireplace (no KayKit mesh)",
                        new Vector3(r.MaxX - 1.5f, y + 1.5f, cz), HexColor("6b4226"),
                        new Vector3(2f, 3f, 3f));
                    Placeholder(dress, "ladder up to the Loft (no KayKit mesh)",
                        new Vector3(r.MinX + 1.5f, y + 3f, r.MinZ + 6f), HexColor("a07840"),
                        new Vector3(0.4f, 6f, 1.6f));
                    LitFixture(lightRoot, "HearthFire",
                        new Vector3(r.MaxX - 2.5f, y + 1.4f, cz), HexColor("ff9a4a"), 3.0f, 6f);
                    break;

                case "kitchen":
                    // §6.1 — kitchen-themed dressing. Table, pots, barrels.
                    Prop(dress, "table_medium.fbx", cx, y, cz, 0f);
                    Prop(dress, "plate_stack.fbx", cx, y + 0.55f, cz, 0f);
                    Prop(dress, "barrel_small_stack.fbx", r.MinX + 3f, y, r.MaxZ - 3f, 0f);
                    Prop(dress, "shelf_large.fbx", r.MaxX - 2f, y, cz, -90f);
                    Prop(dress, "keg.fbx", r.MaxX - 3.5f, y, r.MinZ + 3f, 0f);
                    LitFixture(lightRoot, "KitchenCandle",
                        new Vector3(cx, y + 1.4f, r.MinZ + 3f), HexColor("ffd98a"), 1.6f, 4f);
                    break;

                case "pantry-alcove":
                    // §5.2 — lore room. Shelves of jars, a small reading table.
                    Prop(dress, "shelf_small_books.fbx", r.MinX + 2f, y, cz, 90f);
                    Prop(dress, "crate_small.fbx", r.MaxX - 3f, y, r.MaxZ - 3f, 20f);
                    Prop(dress, "crates_stacked.fbx", r.MaxX - 3f, y, r.MinZ + 3f, 0f);
                    LitFixture(lightRoot, "PantryCandle",
                        new Vector3(cx, y + 1.4f, cz), HexColor("ffd98a"), 1.4f, 3.5f);
                    break;

                case "workshop":
                    // §5.8 / §6.2 — apothecary boss room. Bench, vials, drawers,
                    // dramatic rim light. Deliberately dim (design §4 Beat 6).
                    Prop(dress, "table_long_decorated_C.fbx", r.MaxX - 4f, y, cz, -90f);
                    Prop(dress, "bottle_A_labeled_green.fbx", r.MaxX - 4f, y + 0.55f, cz - 1f, 0f);
                    Prop(dress, "bottle_B_brown.fbx", r.MaxX - 4f, y + 0.55f, cz + 1f, 0f);
                    Prop(dress, "bookcase_double_decoratedA.fbx", cx, y, r.MaxZ - 2f, 180f);
                    Prop(dress, "shelves_decorated.fbx", cx, y, r.MinZ + 2f, 0f);
                    Prop(dress, "table_small.fbx", cx - 4f, y, cz, 0f);
                    Prop(dress, "book_brown.fbx", cx - 4f, y + 0.55f, cz, 25f);
                    Prop(dress, "candle_lit.fbx", cx - 4f, y + 0.6f, cz - 1f, 0f);
                    // §5.8 — one strong rim light suggesting drama.
                    LitFixture(lightRoot, "WorkshopRim",
                        new Vector3(r.MaxX - 3f, y + 3.5f, cz), HexColor("7fd0c4"), 2.4f, 9f);
                    break;

                case "loft-bedroom":
                    // §5.2 — Mira's bedroom. Bed, desk, lore stone (journal 3).
                    Prop(dress, "bed_A_single.fbx", r.MinX + 3.5f, y, r.MinZ + 4f, 0f);
                    Prop(dress, "table_small_decorated_B.fbx", r.MaxX - 3f, y, r.MaxZ - 3f, 0f);
                    Prop(dress, "chair.fbx", r.MaxX - 3f, y, r.MaxZ - 5f, 180f);
                    LitFixture(lightRoot, "LoftCandle",
                        new Vector3(r.MaxX - 3f, y + 1.4f, r.MaxZ - 3f), HexColor("ffd98a"), 1.5f, 3.5f);
                    break;

                case "loft-study":
                    // §5.2 — narrow attic study. Window light, hidden lore stone.
                    Prop(dress, "table_small.fbx", cx, y, cz, 0f);
                    Prop(dress, "book_tan.fbx", cx, y + 0.55f, cz, 15f);
                    Prop(dress, "bookcase_single_decoratedB.fbx", r.MaxX - 2f, y, cz, -90f);
                    LitFixture(lightRoot, "StudyWindow",
                        new Vector3(cx, y + 3f, r.MinZ + 1f), HexColor("dfe7ff"), 1.2f, 7f);
                    break;

                case "root-cellar":
                    // §5.4 — damp treasure cellar. Water puddle, crates, barrels.
                    Prop(dress, "barrel_large.fbx", r.MinX + 3f, y, r.MaxZ - 3f, 0f);
                    Prop(dress, "crates_stacked.fbx", r.MinX + 3f, y, r.MinZ + 3f, 0f);
                    Prop(dress, "crate_large.fbx", r.MaxX - 4f, y, r.MaxZ - 4f, 30f);
                    Placeholder(dress, "water puddle (no KayKit mesh)",
                        new Vector3(r.MaxX - 4f, y + 0.02f, r.MinZ + 4f), HexColor("3a5a6a"),
                        new Vector3(3f, 0.05f, 3f));
                    Placeholder(dress, "stair down from Main Room (cellar entry)",
                        new Vector3(r.MinX + 2f, y + 1f, cz), HexColor("6b6b6b"),
                        new Vector3(2f, 2f, 4f));
                    break;

                case "storage":
                    // §5.4 — adjacent cellar store. Crates + barrels everywhere.
                    Prop(dress, "barrel_small_stack.fbx", r.MinX + 3f, y, r.MinZ + 3f, 0f);
                    Prop(dress, "crates_stacked.fbx", r.MaxX - 3f, y, r.MaxZ - 3f, 0f);
                    Prop(dress, "crate_large_decorated.fbx", cx, y, r.MaxZ - 3f, 15f);
                    Prop(dress, "box_stacked.fbx", r.MaxX - 3f, y, r.MinZ + 3f, 0f);
                    break;

                case "crypt-sublevel":
                    // §5.5/§5.7 — trap floor + pre-boss checkpoint. A sarcophagus.
                    Prop(dress, "floor_tile_big_spikes.fbx", cx, y + 0.05f, cz + 2f, 0f);
                    Prop(dress, "floor_tile_big_grate.fbx", cx, y + 0.05f, cz - 4f, 0f);
                    Prop(dress, "pillar_decorated.fbx", r.MinX + 2.5f, y, cz, 0f);
                    Prop(dress, "pillar_decorated.fbx", r.MaxX - 2.5f, y, cz, 0f);
                    Placeholder(dress, "stone sarcophagus (no KayKit mesh)",
                        new Vector3(cx, y + 0.6f, r.MaxZ - 4f), HexColor("777067"),
                        new Vector3(3f, 1.2f, 6f));
                    break;

                case "hidden-vault":
                    // §5.4 / §6.4 cave accents — the deepest secret. The crafting
                    // shard pedestal is no longer a labelled placeholder primitive:
                    // Workstream C builds the real CraftingPedestal interactable
                    // here via BuildCraftingPedestal() (called from the entry
                    // point), hydrated from crafting-recipes.json.
                    Prop(dress, "rocks_decorated.fbx", r.MinX + 3f, y, r.MaxZ - 3f, 0f);
                    Prop(dress, "rocks.fbx", r.MaxX - 3f, y, r.MinZ + 3f, 50f);
                    Prop(dress, "coin_stack_large.fbx", cx + 2f, y, cz - 2f, 0f);
                    LitFixture(lightRoot, "VaultGlow",
                        new Vector3(cx, y + 1.5f, cz), HexColor("b59cff"), 1.6f, 5f);
                    break;
            }
        }

        // =====================================================================
        //  Vertical connectors (§4.2 — stairs as architecture, not teleports)
        // =====================================================================

        private static void BuildVerticalConnectors(Transform parent)
        {
            // Stair down — Main Room (ground) -> Root Cellar (underground).
            var stairDown = LoadModel(PackRoot + "stairs_long.fbx");
            var sd = InstantiateModel(stairDown, "stairs_long.fbx", "stair Main->Cellar");
            sd.transform.SetParent(parent, false);
            sd.transform.position = new Vector3(-2f, YUnder, -2f);
            sd.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            EnsureCollider(sd);
            if (stairDown != null) _modelCount++;

            // Stair up — Main Room (ground) -> Loft Bedroom (upper). Ladder is a
            // labelled placeholder (KayKit ships no ladder mesh) — the stair piece
            // gives a physical climb affordance beside it.
            var stairUp = LoadModel(PackRoot + "stairs_wood.fbx");
            var su = InstantiateModel(stairUp, "stairs_wood.fbx", "stair Main->Loft");
            su.transform.SetParent(parent, false);
            su.transform.position = new Vector3(0f, YGround, -4f);
            su.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            EnsureCollider(su);
            if (stairUp != null) _modelCount++;

            // Trapdoor route — Entrance Room (ground) -> Root Cellar. The trapdoor
            // is dressed in the Entrance Room; the connecting flight lives here.
            var trapStair = LoadModel(PackRoot + "stairs_narrow.fbx");
            var ts = InstantiateModel(trapStair, "stairs_narrow.fbx", "trapdoor stair Entrance->Cellar");
            ts.transform.SetParent(parent, false);
            ts.transform.position = new Vector3(-12f, YUnder, 5f);
            ts.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            EnsureCollider(ts);
            if (trapStair != null) _modelCount++;
        }

        // =====================================================================
        //  Hero rig + lantern + follow camera
        // =====================================================================

        private static GameObject BuildHeroRig(Transform parent)
        {
            var heroGo = new GameObject("Keeper");
            heroGo.transform.SetParent(parent, false);
            // Spawn at the layout's authored spawn point (Garden Approach, SW).
            // DungeonController.ResolveSpawnPosition() re-places the hero at run
            // time from the layout JSON; this is the in-editor placement so the
            // scene reads correctly before play.
            var garden = FindRoom("garden-approach");
            float sx = (garden.MinX + garden.MaxX) * 0.5f;
            float sz = (garden.MinZ + garden.MaxZ) * 0.5f;
            heroGo.transform.position = new Vector3(sx, YGround, sz);
            // Face east into the cottage — matches healers-cottage.json spawn.facingY.
            heroGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // A simple visual stand-in capsule — the real hero prefab (the KayKit
            // mage mesh) is wired by the playable build; the dungeon scene only
            // needs a transform target. The capsule is sized to the mage mesh.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "KeeperBody";
            body.transform.SetParent(heroGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            // The visual capsule must NOT collide — the CharacterController IS the
            // collision body. A stray capsule collider would fight the controller.
            StripColliders(body);
            ApplyTint(body, HexColor("e8d8b8"));

            // CharacterController sized to the KayKit mage mesh (radius ~0.35,
            // height ~1.9, centred) — DungeonHero [RequireComponent]s this and
            // slides it along the wall mesh colliders (the BUG-007 guarantee).
            var cc = heroGo.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.slopeLimit = 50f;     // climbs the KayKit stair pieces
            cc.stepOffset = 0.45f;   // steps the stair treads / floor seams
            cc.skinWidth = 0.03f;

            // Week-5 DungeonHero locomotion (WASD + tap-to-move, sliding wall
            // collision). Added by reflection — the Editor asmdef cannot
            // reference DeNelle.Dungeons. The controller auto-finds it on the
            // hero rig; the explicit wire in WireController is belt-and-braces.
            AddDungeonComponent(heroGo, TypeDungeonHero);
            return heroGo;
        }

        private static Component BuildLantern(GameObject heroGo)
        {
            // The Lantern requires a Light component (RequireComponent on the type).
            var lanternGo = new GameObject("Lantern");
            lanternGo.transform.SetParent(heroGo.transform, false);
            lanternGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = lanternGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = HexColor("f6c97a");
            light.range = 6f;       // §7 — base 6u lantern reach
            light.intensity = 9f;
            light.shadows = LightShadows.None;
            _lightCount++;
            return AddDungeonComponent(lanternGo, TypeLantern);
        }

        private static GameObject BuildFollowCameraRig(Transform parent)
        {
            // The Week-5 dungeon follow camera: a CinemachineCamera carrying a
            // CinemachineFollow (Body) and the DungeonCameraRig component. The
            // rig deliberately has NO Aim component, so Cinemachine preserves the
            // authored isometric pitch (DungeonCameraRig.ApplyFraming locks it).
            //
            // All three components are added by reflection — the DeNelle.Editor
            // asmdef references neither Unity.Cinemachine nor DeNelle.Dungeons,
            // but both assemblies are loaded in the editor so FindType resolves
            // them (the BattleSceneBuilder InputSystemUIInputModule pattern).
            var rig = new GameObject("FollowCameraRig");
            rig.transform.SetParent(parent, false);
            // Seat at the authored isometric offset over the Garden Approach
            // entry; DungeonCameraRig.Bind() re-seats it on the hero at run time.
            var garden = FindRoom("garden-approach");
            float cx = (garden.MinX + garden.MaxX) * 0.5f;
            float cz = (garden.MinZ + garden.MaxZ) * 0.5f;
            rig.transform.position = new Vector3(cx, YGround + 13f, cz - 9f);
            rig.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

            // CinemachineCamera (the virtual camera) + CinemachineFollow (Body).
            Component vcam = AddComponentByName(rig, TypeCinemachineCamera);
            AddComponentByName(rig, TypeCinemachineFollow);
            if (vcam == null)
            {
                Debug.LogError(
                    "[DungeonSceneBuilder] Could not add a CinemachineCamera — is the " +
                    "Unity.Cinemachine package installed? The follow camera is incomplete.");
            }

            // The Week-5 DungeonCameraRig — self-configuring isometric rig. It
            // [RequireComponent]s CinemachineCamera (added above) and owns the
            // framing maths; DungeonController.Bind() drives it.
            AddDungeonComponent(rig, TypeDungeonCameraRig);
            return rig;
        }

        // =====================================================================
        //  Lore stones (§5.2) — HARD-LOCKED journal copy, verbatim from design
        // =====================================================================

        /// <summary>
        /// The five lore stones (§9.4 — Entrance, Main, Loft Bedroom, Workshop,
        /// Hidden Vault). Bodies are CANON — verbatim from
        /// docs/dungeon-3d-healers-cottage-design.md §4 (and the §9.3 redacted
        /// Hidden-Vault draft). Never paraphrased.
        /// </summary>
        private static void BuildLoreStones(Transform parent, UnityEngine.Object runtimeState,
            Transform hero)
        {
            var stones = new[]
            {
                new LoreSpec {
                    Id = "journal-1", RoomId = "entrance-room",
                    AssetKey = "table_small.fbx",
                    Title = "Alduin's journal — entry 1",
                    Body = new[] {
                        "The folk came again today. The well at Carrow's edge is going bad. " +
                        "Crystals work on the early signs but the late ones — I don't know yet. " +
                        "I came here to learn the green ways. I am still learning.",
                    },
                },
                new LoreSpec {
                    Id = "journal-2", RoomId = "main-room",
                    AssetKey = "table_long_decorated_A.fbx",
                    Title = "Alduin's journal — entry 2",
                    Body = new[] {
                        "The Folk who came from the village this week: Wren's boy, the miller's " +
                        "wife, the two children of Carrow. I told them what I had. The well is " +
                        "poisoned, I know that now. It is not anything I can name. It is older " +
                        "than the names I have.",
                    },
                },
                new LoreSpec {
                    Id = "journal-3", RoomId = "loft-bedroom",
                    AssetKey = "table_small_decorated_B.fbx",
                    Title = "Alduin's journal — entry 3",
                    Body = new[] {
                        "The dreams are back. The Wound, calling. I am not the same in the " +
                        "mornings. Mira would have known what to say. I miss her hands.",
                        "If anyone reads this — I am going to the Wound. I am going to try one " +
                        "more thing. The Heart will hold while I am gone. The Heart always holds.",
                        "Forgive me.",
                    },
                },
                new LoreSpec {
                    Id = "journal-4", RoomId = "workshop",
                    AssetKey = "table_small.fbx",
                    Title = "Alduin's journal — entry 4 (the last one)",
                    Body = new[] {
                        "I am going. The Wound is louder than the Heart now. I have left a thing " +
                        "in the cellar for whoever is next — a seed. Plant it at the Folk's " +
                        "table, if there is anyone to plant it. It will grow into something old " +
                        "and quiet. The Folk used to say there were trees like it once, before " +
                        "any of us. Maybe one of them will remember the song.",
                        "The Heart will hold. The Heart always holds.",
                        "Tend it, Keeper. I am sorry I could not.",
                    },
                },
                // §9.3 Hidden Vault — the windowsill carving + redacted draft.
                // The carving line is VERBATIM from design §4 Beat 5.
                new LoreSpec {
                    Id = "journal-vault", RoomId = "hidden-vault",
                    AssetKey = "table_small.fbx",
                    Title = "A carving, and a struck-through draft",
                    Body = new[] {
                        "M.M. + A.M., 31st of Honeymonth",
                        "The seed is not the only thing I leave. There is a thing I have not " +
                        "written plainly anywhere, and I will not write it plainly here. If you " +
                        "have come this deep, you have read enough to know the shape of it. " +
                        "Mira walked the green ways first. I followed her into them. I am " +
                        "following her still.",
                    },
                },
            };

            int total = stones.Length;
            foreach (var s in stones)
            {
                var room = FindRoom(s.RoomId);
                float y = LevelY(room.Level);
                float cx = (room.MinX + room.MaxX) * 0.5f;
                float cz = (room.MinZ + room.MaxZ) * 0.5f;
                Vector3 pos = new Vector3(cx, y, cz);

                var stoneGo = new GameObject($"LoreStone_{s.Id}");
                stoneGo.transform.SetParent(parent, false);
                stoneGo.transform.position = pos;

                // A reading-stand visual from the palette's small table.
                var model = LoadModel(PackRoot + s.AssetKey);
                var visual = InstantiateModel(model, s.AssetKey, $"lore stand {s.Id}");
                visual.transform.SetParent(stoneGo.transform, false);
                visual.transform.localPosition = Vector3.zero;
                Prop(stoneGo.transform, "book_grey.fbx", cx, y + 0.55f, cz, 10f);
                if (model != null) _modelCount++;

                // A small glow so the stone reads from across a dark room (§7).
                var glow = stoneGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = HexColor("cdb6ff");
                glow.range = 3f;
                glow.intensity = 1.0f;
                glow.shadows = LightShadows.None;
                _lightCount++;

                Component comp = AddDungeonComponent(stoneGo, TypeLoreStone);
                // Configure(DungeonLoreStone, DungeonRuntimeState, Transform, int).
                // The strongly-typed DungeonLoreStone lives in DeNelle.Dungeons,
                // which the Editor asmdef cannot reference — so the layout JSON
                // (already authored with this copy) is the runtime source; the
                // controller hydrates each stone from it at scene load. Here we
                // only pin the placement + the runtime-state ref via reflection.
                SetSerializedObjectRef(comp, "_runtimeState", runtimeState);
            }
        }

        private sealed class LoreSpec
        {
            public string Id, RoomId, AssetKey, Title;
            public string[] Body;
        }

        // =====================================================================
        //  Checkpoint shrines (§5.7 / §13) — 2 per the Cottage layout
        // =====================================================================

        private static void BuildCheckpoints(Transform parent, UnityEngine.Object runtimeState,
            Transform hero)
        {
            // §13: shrine 1 — Entrance Room (after Bryn's intro beat);
            //      shrine 2 — Crypt Sub-Level (pre-boss, underground).
            var defs = new[]
            {
                new CheckpointSpec {
                    Id = "checkpoint-entrance", RoomId = "entrance-room",
                    Toast = "The Folk have walked this way before, Keeper. Catch your breath.",
                },
                new CheckpointSpec {
                    Id = "checkpoint-crypt", RoomId = "crypt-sublevel",
                    Toast = "Beyond this room, the last of him. Steady your light.",
                },
            };

            foreach (var c in defs)
            {
                var room = FindRoom(c.RoomId);
                float y = LevelY(room.Level);
                float cx = (room.MinX + room.MaxX) * 0.5f;
                float cz = room.MinZ + 3f; // tucked near the room edge — an alcove
                Vector3 pos = new Vector3(cx, y, cz);

                var shrineGo = new GameObject($"Checkpoint_{c.Id}");
                shrineGo.transform.SetParent(parent, false);
                shrineGo.transform.position = pos;

                // Stone pedestal — a KayKit pillar piece.
                var pedModel = LoadModel(PackRoot + "pillar.fbx");
                var pedestal = InstantiateModel(pedModel, "pillar.fbx", $"shrine pedestal {c.Id}");
                pedestal.transform.SetParent(shrineGo.transform, false);
                pedestal.transform.localPosition = Vector3.zero;
                if (pedModel != null) _modelCount++;

                // Floating violet crystal (§5.7 — pulsing crystal on a pedestal).
                var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crystal.name = "Crystal";
                crystal.transform.SetParent(shrineGo.transform, false);
                crystal.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                crystal.transform.localScale = new Vector3(0.4f, 0.7f, 0.4f);
                crystal.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                StripColliders(crystal);
                ApplyEmissive(crystal, HexColor("9b6cff"), 2.2f);

                // §7 — checkpoint shrines emit ambient violet glow (~3u radius).
                var shrineLight = new GameObject("ShrineGlow").AddComponent<Light>();
                shrineLight.transform.SetParent(shrineGo.transform, false);
                shrineLight.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                shrineLight.type = LightType.Point;
                shrineLight.color = HexColor("9b6cff");
                shrineLight.range = 3f;
                shrineLight.intensity = 2.4f;
                shrineLight.shadows = LightShadows.None;
                _lightCount++;

                // Two candles + two flowers (§5.7) — candles as KayKit pieces.
                Prop(shrineGo.transform, "candle_lit.fbx", cx - 1f, y, cz, 0f);
                Prop(shrineGo.transform, "candle_lit.fbx", cx + 1f, y, cz, 0f);

                Component comp = AddDungeonComponent(shrineGo, TypeCheckpoint);
                SetSerializedObjectRef(comp, "_runtimeState", runtimeState);
                SetSerializedObjectRef(comp, "_crystalRenderer",
                    crystal.GetComponent<Renderer>());
                SetSerializedObjectRef(comp, "_crystalLight", shrineLight);
                SetSerializedObjectRef(comp, "_crystal", crystal.transform);
            }
        }

        private sealed class CheckpointSpec { public string Id, RoomId, Toast; }

        // =====================================================================
        //  Scripted encounters (§8 / §9.4 — 5 scripted fights, 60/20/20 mix)
        // =====================================================================

        private static void BuildEncounters(Transform parent, UnityEngine.Object runtimeState)
        {
            // §9.4 — 4 scripted combats: Garden, Main (pair roster), Cellar,
            // Storage. Order MATCHES healers-cottage.json scriptedEncounters[] so
            // DungeonController.HydrateEncounters can pair child[i] -> def[i].
            var defs = new[]
            {
                new EncSpec { Id = "garden-hollow-one", RoomId = "garden-approach" },
                new EncSpec { Id = "main-room-hollow-pair", RoomId = "main-room" },
                new EncSpec { Id = "cellar-hollow-one", RoomId = "root-cellar" },
                new EncSpec { Id = "storage-hollow-one", RoomId = "storage" },
            };

            foreach (var e in defs)
            {
                var room = FindRoom(e.RoomId);
                float y = LevelY(room.Level);
                float cx = (room.MinX + room.MaxX) * 0.5f;
                float cz = (room.MinZ + room.MaxZ) * 0.5f;

                var trigGo = new GameObject($"Encounter_{e.Id}");
                trigGo.transform.SetParent(parent, false);
                trigGo.transform.position = new Vector3(cx, y, cz);

                Component comp = AddDungeonComponent(trigGo, TypeEncounterTrigger);
                SetSerializedObjectRef(comp, "_runtimeState", runtimeState);
                // ConfigureScripted at run time hydrates the roster from the
                // layout JSON; the trigger transform is the authored anchor.
            }

            // The mini-boss encounter trigger — built LAST so it is the final
            // child of the Encounters root; DungeonController.HydrateEncounters
            // configures the trailing child with Layout.miniBoss via ConfigureBoss.
            // The visual boss stand-in is a separate placement (BuildMiniBoss).
            var workshop = FindRoom("workshop");
            float by = LevelY(workshop.Level);
            float bx = workshop.MaxX - 6f;
            float bz = (workshop.MinZ + workshop.MaxZ) * 0.5f;

            var bossGo = new GameObject("Encounter_apprentice-of-the-apothecary");
            bossGo.transform.SetParent(parent, false);
            bossGo.transform.position = new Vector3(bx, by, bz);

            Component bossComp = AddDungeonComponent(bossGo, TypeEncounterTrigger);
            SetSerializedObjectRef(bossComp, "_runtimeState", runtimeState);
        }

        private sealed class EncSpec { public string Id, RoomId; }

        // =====================================================================
        //  Treasure chests (§9.4 — Cellar, Storage, Hidden Vault)
        // =====================================================================

        private static void BuildChests(Transform parent, UnityEngine.Object runtimeState)
        {
            var chestRoot = NewChild(parent, "Chests");
            PlaceChest(chestRoot, "cellar-chest", "root-cellar", "chest.fbx",
                "Cloak of the Lightbearer");
            PlaceChest(chestRoot, "storage-chest", "storage", "chest.fbx",
                "Soul Embers pouch");
            PlaceChest(chestRoot, "vault-chest", "hidden-vault", "chest_gold.fbx",
                "rare crafting shard");
        }

        private static void PlaceChest(Transform parent, string id, string roomId,
            string assetKey, string label)
        {
            var room = FindRoom(roomId);
            float y = LevelY(room.Level);
            float x = room.MaxX - 3f;
            float z = room.MaxZ - 3f;
            var model = LoadModel(PackRoot + assetKey);
            var go = InstantiateModel(model, assetKey, $"chest {id} ({label})");
            go.name = $"Chest_{id}";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, z);
            EnsureCollider(go);
            if (model != null) _modelCount++;
        }

        // =====================================================================
        //  Bryn the Wanderer (§12 — Tier-1 entry line, HARD-LOCKED)
        // =====================================================================

        private static Component BuildBryn(Transform parent, UnityEngine.Object runtimeState)
        {
            var garden = FindRoom("garden-approach");
            float bx = garden.MinX + 5f;
            float bz = (garden.MinZ + garden.MaxZ) * 0.5f - 2f;

            var brynGo = new GameObject("Bryn");
            brynGo.transform.SetParent(parent, false);
            brynGo.transform.position = new Vector3(bx, YGround, bz);

            // A simple visual stand-in — Bryn's character prefab is wired by the
            // playable build; the dungeon scene needs the placement + line.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "BrynBody";
            body.transform.SetParent(brynGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            StripColliders(body);
            ApplyTint(body, HexColor("6b7d8a"));

            // World-space speech-bubble anchor. The WandererBubble component
            // (DeNelle.Dungeons — added by reflection) IS the run-time bubble: a
            // billboarded panel + 3D text implementing IWandererBubble. Bryn
            // drives it through her _bubbleBehaviour seam (wired below). Bryn
            // reads her line from lore-fragments.json / the layout JSON at run
            // time; the canon Tier-1 line is also baked into a child GameObject
            // name so the placement stays self-documenting and verifiable in the
            // saved scene. Bryn's Tier-1 line, verbatim (spec §12 / design §4
            // Beat 1):
            var bubble = new GameObject("SpeechBubbleAnchor");
            bubble.transform.SetParent(brynGo.transform, false);
            bubble.transform.localPosition = Vector3.zero;
            var brynLine = new GameObject($"BrynTier1Line: {BrynTier1Line}");
            brynLine.transform.SetParent(bubble.transform, false);

            // The concrete IWandererBubble — self-builds its panel + text in
            // Awake(). Lives on the anchor; assigned to Bryn._bubbleBehaviour.
            Component bubbleComp = AddDungeonComponent(bubble, TypeWandererBubble);

            Component comp = AddDungeonComponent(brynGo, TypeBryn);
            SetSerializedObjectRef(comp, "_runtimeState", runtimeState);
            // _bubbleBehaviour is typed MonoBehaviour; WandererBubble implements
            // IWandererBubble and Bryn casts it on Configure().
            SetSerializedObjectRef(comp, "_bubbleBehaviour", bubbleComp);
            return comp;
        }

        // =====================================================================
        //  Mini-boss — The Apprentice of the Apothecary (design §4 Beat 6)
        // =====================================================================

        private static void BuildMiniBoss(Transform parent)
        {
            // HARD-LOCKED mini-boss spec — docs/dungeon-3d-healers-cottage-design.md
            // §4 Beat 6: "The Apprentice of the Apothecary — a stronger Hollow
            // One ... 2.5x normal Hollow HP, +50% damage, has one special — a
            // 'tincture' attack that briefly blinds the Keeper (shrinks light
            // radius by 50% for 6 seconds)."
            var workshop = FindRoom("workshop");
            float bx = workshop.MaxX - 6f;
            float bz = (workshop.MinZ + workshop.MaxZ) * 0.5f;

            var bossGo = new GameObject("MiniBoss_ApprenticeOfTheApothecary");
            bossGo.transform.SetParent(parent, false);
            bossGo.transform.position = new Vector3(bx, YGround, bz);

            // A clearly-labelled visual stand-in — the Hollow-One enemy prefab is
            // supplied by the battle/enemy module, not by this scene builder.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "BossBody";
            body.transform.SetParent(bossGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            body.transform.localScale = new Vector3(1.15f, 1.2f, 1.15f);
            ApplyEmissive(body, HexColor("5e8c86"), 0.8f);

            // The HARD-LOCKED mini-boss spec is baked into child GameObject
            // names so it survives any build (no Editor-only component is
            // attached). The encounter / battle module resolves the enemy type
            // and stat block from the layout JSON's miniBoss block at run time.
            AddSpecChild(bossGo, "DisplayName: The Apprentice of the Apothecary");
            AddSpecChild(bossGo, "EnemyType: hollow_apprentice_minor");
            AddSpecChild(bossGo, "HP: 2.5x normal Hollow One (design §4 Beat 6)");
            AddSpecChild(bossGo, "Damage: +50% (design §4 Beat 6)");
            AddSpecChild(bossGo,
                "Special: Tincture — shrinks the Keeper's lantern reach 50% for 6s");
        }

        /// <summary>Bakes one HARD-LOCKED spec line into a child GameObject name.</summary>
        private static void AddSpecChild(GameObject parent, string line)
        {
            var go = new GameObject(line);
            go.transform.SetParent(parent.transform, false);
        }

        // =====================================================================
        //  Workstream C — dungeon crafting system (ingredient pickups +
        //  crafting pedestal) + the lantern oil HUD.
        // =====================================================================

        /// <summary>
        /// Ensures the shared DungeonInventory ScriptableObject asset exists on
        /// disk and returns it. The type lives in DeNelle.Dungeons (not
        /// referenced by the Editor asmdef), so it is created by reflection — the
        /// same pattern as <see cref="EnsureRuntimeStateAsset"/>.
        /// </summary>
        private static UnityEngine.Object EnsureInventoryAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(InventoryAssetPath);
            if (existing != null) return existing;

            var type = FindType(TypeDungeonInventory);
            if (type == null)
            {
                Debug.LogError($"[DungeonSceneBuilder] Type '{TypeDungeonInventory}' not found — " +
                               "is the DeNelle.Dungeons assembly compiled? Inventory skipped.");
                return null;
            }

            EnsureFolder(RuntimeStateDir);
            var so = ScriptableObject.CreateInstance(type);
            so.name = "HealersCottageInventory";
            AssetDatabase.CreateAsset(so, InventoryAssetPath);
            AssetDatabase.SaveAssets();
            return so;
        }

        /// <summary>
        /// Builds the collectible ingredient-pickup motes. The placements live in
        /// crafting-recipes.json (DeNelle.Dungeons hydrates each pickup from the
        /// strongly-typed IngredientPlacement at run time) — the Editor asmdef
        /// cannot read that JSON's typed shape, so the placements are mirrored
        /// here as a small literal table. The COUNT + ORDER must match
        /// crafting-recipes.json's ingredientPlacements[] so DungeonController
        /// can pair child[i] -> placement[i]. Keep the two in sync.
        /// </summary>
        private static void BuildIngredientPickups(
            Transform parent, UnityEngine.Object inventory, Transform hero)
        {
            // MIRROR of crafting-recipes.json ingredientPlacements[] (same order).
            var defs = new[]
            {
                new IngredientSpec {
                    PickupId = "reed-garden", IngredientId = "dry-reed",
                    RoomId = "garden-approach", Pos = new Vector3(-24f, 0f, 5f),
                    Tint = "c9a86a",
                },
                new IngredientSpec {
                    PickupId = "cloth-entrance", IngredientId = "oil-soaked-cloth",
                    RoomId = "entrance-room", Pos = new Vector3(-8f, 0f, -5f),
                    Tint = "d8b15a",
                },
                new IngredientSpec {
                    PickupId = "resin-main", IngredientId = "ember-resin",
                    RoomId = "main-room", Pos = new Vector3(11f, 0f, 6f),
                    Tint = "e2683a",
                },
            };

            foreach (var d in defs)
            {
                var room = FindRoom(d.RoomId);
                float y = room != null ? LevelY(room.Level) : YGround;

                var pickupGo = new GameObject($"IngredientPickup_{d.PickupId}");
                pickupGo.transform.SetParent(parent, false);
                pickupGo.transform.position = new Vector3(d.Pos.x, y, d.Pos.z);

                // The visual mote — a small emissive octahedron-ish cube floating
                // at chest height. The IngredientPickup bobs / spins it.
                var moteVisual = new GameObject("Mote");
                moteVisual.transform.SetParent(pickupGo.transform, false);

                var mote = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mote.name = "MoteMesh";
                mote.transform.SetParent(moteVisual.transform, false);
                mote.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                mote.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
                mote.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                StripColliders(mote);
                ApplyEmissive(mote, HexColor(d.Tint), 2.0f);

                // A faint glow so the ingredient reads from across a dark room.
                var glow = new GameObject("MoteGlow").AddComponent<Light>();
                glow.transform.SetParent(moteVisual.transform, false);
                glow.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                glow.type = LightType.Point;
                glow.color = HexColor(d.Tint);
                glow.range = 2.6f;
                glow.intensity = 1.4f;
                glow.shadows = LightShadows.None;
                _lightCount++;

                Component comp = AddDungeonComponent(pickupGo, TypeIngredientPickup);
                SetSerializedObjectRef(comp, "_inventory", inventory);
                SetSerializedObjectRef(comp, "_moteVisual", moteVisual);
                // _moteSpin is the spin/bob transform — the inner mote mesh.
                SetSerializedObjectRef(comp, "_moteSpin", mote.transform);
            }
        }

        private sealed class IngredientSpec
        {
            public string PickupId, IngredientId, RoomId, Tint;
            public Vector3 Pos;
        }

        /// <summary>
        /// Builds the real crafting pedestal in the Hidden Vault — REPLACES the
        /// §5.4 placeholder shard-pedestal primitive. A KayKit pillar topped with
        /// a floating violet shard (the same violet -> gold settle language as
        /// the checkpoint shrine). The CraftingPedestal component (DeNelle.Dungeons,
        /// added by reflection) hydrates from crafting-recipes.json at run time.
        /// </summary>
        private static Component BuildCraftingPedestal(
            Transform parent, UnityEngine.Object inventory, Transform hero)
        {
            // Position MIRRORS crafting-recipes.json pedestal.position.
            var vault = FindRoom("hidden-vault");
            float y = vault != null ? LevelY(vault.Level) : YUnder;
            var pos = new Vector3(18f, y, 18f);

            var craftRoot = NewChild(parent, "CraftingPedestal");
            var pedestalGo = craftRoot.gameObject;
            pedestalGo.transform.position = pos;

            // Stone pedestal — a KayKit pillar piece (same as the shrine pedestal).
            var pedModel = LoadModel(PackRoot + "pillar_decorated.fbx");
            var pedestal = InstantiateModel(pedModel, "pillar_decorated.fbx",
                "crafting pedestal base");
            pedestal.transform.SetParent(pedestalGo.transform, false);
            pedestal.transform.localPosition = Vector3.zero;
            EnsureCollider(pedestal);
            if (pedModel != null) _modelCount++;

            // The floating crafting shard — violet while uncrafted, settles gold.
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Shard";
            shard.transform.SetParent(pedestalGo.transform, false);
            shard.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            shard.transform.localScale = new Vector3(0.45f, 0.75f, 0.45f);
            shard.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            StripColliders(shard);
            ApplyEmissive(shard, HexColor("9b6cff"), 2.2f);

            // The shard's point light — a 'follow the light' cue from afar.
            var shardLight = new GameObject("ShardGlow").AddComponent<Light>();
            shardLight.transform.SetParent(pedestalGo.transform, false);
            shardLight.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            shardLight.type = LightType.Point;
            shardLight.color = HexColor("9b6cff");
            shardLight.range = 4f;
            shardLight.intensity = 2.4f;
            shardLight.shadows = LightShadows.None;
            _lightCount++;

            // A small world-space interact prompt — a thin pill above the shard.
            var prompt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prompt.name = "InteractPrompt";
            prompt.transform.SetParent(pedestalGo.transform, false);
            prompt.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            prompt.transform.localScale = new Vector3(1.2f, 0.28f, 0.08f);
            StripColliders(prompt);
            ApplyEmissive(prompt, HexColor("f6c97a"), 1.4f);
            prompt.SetActive(false); // toggled on by CraftingPedestal on proximity

            Component comp = AddDungeonComponent(pedestalGo, TypeCraftingPedestal);
            SetSerializedObjectRef(comp, "_inventory", inventory);
            SetSerializedObjectRef(comp, "_shardRenderer", shard.GetComponent<Renderer>());
            SetSerializedObjectRef(comp, "_shardLight", shardLight);
            SetSerializedObjectRef(comp, "_shard", shard.transform);
            SetSerializedObjectRef(comp, "_interactPrompt", prompt);
            return comp;
        }

        /// <summary>
        /// Ensures a PanelSettings asset for the dungeon's UI Toolkit documents
        /// (the oil HUD + the crafting panel). Mirrors the BattleSceneBuilder
        /// pattern — ScaleWithScreenSize at 1920×1080.
        /// </summary>
        private static UnityEngine.UIElements.PanelSettings EnsureDungeonPanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(
                DungeonPanelSettingsPath);
            if (existing != null) return existing;

            EnsureFolder(DungeonGeneratedDir);
            var settings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
            settings.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;

            var theme = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(
                "Packages/com.unity.modules.uielements/PackageResources/StyleSheets/" +
                "Generated/DefaultRuntimeTheme.tss");
            if (theme != null) settings.themeStyleSheet = theme;

            AssetDatabase.CreateAsset(settings, DungeonPanelSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// Builds the dungeon HUD UIDocument — DungeonHud.uxml + the
        /// DungeonHudController (DeNelle.Dungeons, reflection). The oil meter is
        /// fed the Lantern reference by DungeonController on load.
        /// </summary>
        private static Component BuildDungeonHud(
            Transform parent, UnityEngine.UIElements.PanelSettings panel)
        {
            var hudGo = new GameObject("DungeonHUD UIDocument");
            hudGo.transform.SetParent(parent, false);

            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(
                DungeonHudUxmlPath);
            if (uxml != null)
                doc.visualTreeAsset = uxml;
            else
                Debug.LogWarning($"[DungeonSceneBuilder] DungeonHud.uxml not found at " +
                                 $"'{DungeonHudUxmlPath}' — assign the UIDocument source in the inspector.");

            // The controller [RequireComponent]s UIDocument — same GameObject.
            return AddDungeonComponent(hudGo, TypeDungeonHudController);
        }

        /// <summary>
        /// Builds the crafting-panel UIDocument — CraftingPanel.uxml + the
        /// CraftingPanelController (DeNelle.Dungeons, reflection). The panel hides
        /// itself until the crafting pedestal raises its open event; the
        /// DungeonController binds the two on load.
        /// </summary>
        private static Component BuildCraftingPanel(
            Transform parent, UnityEngine.UIElements.PanelSettings panel)
        {
            var panelGo = new GameObject("CraftingPanel UIDocument");
            panelGo.transform.SetParent(parent, false);

            var doc = panelGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            // A higher sort order so the modal paints above the oil HUD.
            doc.sortingOrder = 10f;
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(
                CraftingPanelUxmlPath);
            if (uxml != null)
                doc.visualTreeAsset = uxml;
            else
                Debug.LogWarning($"[DungeonSceneBuilder] CraftingPanel.uxml not found at " +
                                 $"'{CraftingPanelUxmlPath}' — assign the UIDocument source in the inspector.");

            return AddDungeonComponent(panelGo, TypeCraftingPanelController);
        }

        // =====================================================================
        //  DungeonController wiring (SerializedObject — no compile dependency)
        // =====================================================================

        private static void WireController(
            Component controller, UnityEngine.Object runtimeState, Transform hero,
            Component heroController, Component followCamera, Component cameraRig,
            Component lantern, Component bryn,
            Transform loreRoot, Transform checkpointRoot, Transform encounterRoot,
            AudioSource bgm,
            UnityEngine.Object dungeonInventory, Component craftingPedestal,
            Transform ingredientRoot, Component craftingPanel, Component dungeonHud)
        {
            if (controller == null) return;
            var so = new SerializedObject(controller);
            SetObjectField(so, "_runtimeState", runtimeState);
            SetObjectField(so, "_hero", hero);
            // Week-5: the DungeonHero locomotion controller — input is held off
            // across the spawn teleport, then enabled once the run is live.
            SetObjectField(so, "_heroController", heroController);
            // Week-5: the Cinemachine follow camera + its self-configuring
            // DungeonCameraRig. Both added by reflection in BuildFollowCameraRig;
            // ConfigureCamera() prefers the rig and falls back to the bare camera.
            SetObjectField(so, "_followCamera", followCamera);
            SetObjectField(so, "_cameraRig", cameraRig);
            SetObjectField(so, "_lantern", lantern);
            SetObjectField(so, "_bryn", bryn);
            SetObjectField(so, "_loreStoneRoot", loreRoot);
            SetObjectField(so, "_checkpointRoot", checkpointRoot);
            SetObjectField(so, "_encounterRoot", encounterRoot);
            SetObjectField(so, "_ambientBgm", bgm);
            // Workstream C — crafting system + dungeon oil HUD.
            SetObjectField(so, "_dungeonInventory", dungeonInventory);
            SetObjectField(so, "_craftingPedestal", craftingPedestal);
            SetObjectField(so, "_ingredientRoot", ingredientRoot);
            SetObjectField(so, "_craftingPanel", craftingPanel);
            SetObjectField(so, "_dungeonHud", dungeonHud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectField(SerializedObject so, string field,
            UnityEngine.Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[DungeonSceneBuilder] Serialized field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} — wiring skipped for that field.");
                return;
            }
            if (value != null && !(prop.propertyType == SerializedPropertyType.ObjectReference))
            {
                Debug.LogWarning($"[DungeonSceneBuilder] Field '{field}' is not an object reference.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        /// <summary>
        /// Sets one [SerializeField] object-reference field on a reflectively
        /// added component, skipping gracefully when the field is missing or the
        /// value type does not fit.
        /// </summary>
        private static void SetSerializedObjectRef(Component comp, string field,
            UnityEngine.Object value)
        {
            if (comp == null) return;
            var so = new SerializedObject(comp);
            SetObjectField(so, field, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        //  Runtime-state ScriptableObject — created / loaded by reflection
        // =====================================================================

        /// <summary>
        /// Ensures a DungeonRuntimeState asset exists on disk and returns it. The
        /// SO type lives in DeNelle.Dungeons (not referenced by the Editor
        /// asmdef), so it is created via <see cref="ScriptableObject.CreateInstance(Type)"/>
        /// against the reflectively-resolved type.
        /// </summary>
        private static UnityEngine.Object EnsureRuntimeStateAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RuntimeStateAssetPath);
            if (existing != null) return existing;

            var type = FindType(TypeRuntimeState);
            if (type == null)
            {
                Debug.LogError($"[DungeonSceneBuilder] Type '{TypeRuntimeState}' not found — is " +
                               "the DeNelle.Dungeons assembly compiled? Runtime state skipped.");
                return null;
            }

            EnsureFolder(RuntimeStateDir);
            var so = ScriptableObject.CreateInstance(type);
            so.name = "HealersCottageRuntimeState";
            AssetDatabase.CreateAsset(so, RuntimeStateAssetPath);
            AssetDatabase.SaveAssets();
            return so;
        }

        // =====================================================================
        //  KayKit model loading + placeholder helpers
        // =====================================================================

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

        /// <summary>Instantiates a KayKit prop FBX at a world position + yaw.</summary>
        private static void Prop(Transform parent, string fbx, float x, float y, float z,
            float yaw)
        {
            var model = LoadModel(PackRoot + fbx);
            var go = InstantiateModel(model, fbx, $"prop {fbx}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            EnsureCollider(go);
            _propCount++;
            if (model != null) _modelCount++;
        }

        /// <summary>
        /// Drops a clearly-labelled placeholder cube for a feature the KayKit
        /// pack ships no mesh for (ladder, trapdoor, sarcophagus, water puddle,
        /// lantern post). Logged + tallied — never blocks.
        /// </summary>
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
                $"[DungeonSceneBuilder] KayKit asset missing / no mesh — placeholder used for: {label}");
        }

        // =====================================================================
        //  Palette mapping (§6.1 cozy / §6.2 apothecary / §6.3 fortress)
        // =====================================================================

        private static string FloorPiece(Palette p)
        {
            switch (p)
            {
                case Palette.Cozy: return "floor_wood_large.fbx";        // §6.1 wood-plank
                case Palette.Apothecary: return "floor_tile_large.fbx";  // §6.2 stone
                case Palette.Fortress: return "floor_tile_large.fbx";    // §6.3 dungeon stone
                case Palette.Cave: return "floor_tile_large_rocks.fbx";  // §6.4 rocky
                default: return "floor_tile_large.fbx";
            }
        }

        /// <summary>The wall-family FBX for a palette — base or doorway variant.</summary>
        private static string WallPiece(Palette p, string kind)
        {
            // The pack ships ONE wall family; palette character comes from the
            // floor piece + tint + lighting. Cozy rooms use the plain wall,
            // underground/fortress use the cracked wall for an aged stone read.
            if (kind == "wall_doorway") return "wall_doorway.fbx";
            switch (p)
            {
                case Palette.Fortress: return "wall_cracked.fbx";
                case Palette.Cave: return "wall_broken.fbx";
                default: return "wall.fbx";
            }
        }

        private static Color PaletteTint(Palette p)
        {
            switch (p)
            {
                case Palette.Cozy: return TintCozy;
                case Palette.Apothecary: return TintApothecary;
                case Palette.Fortress: return TintFortress;
                case Palette.Cave: return TintCave;
                default: return Color.white;
            }
        }

        // =====================================================================
        //  Lighting (§7 — lantern-low ambient + lit-fixture point lights)
        // =====================================================================

        private static void ConfigureAmbient()
        {
            // §7 — ambient light very low (~0.05). The Keeper's lantern is the
            // dominant light source at run time.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(
                AmbientIntensity, AmbientIntensity, AmbientIntensity * 1.1f);
            RenderSettings.ambientIntensity = AmbientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogColor = HexColor("0a0a10");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 42f;
        }

        /// <summary>A static lit-fixture point light (~2u pool — §7).</summary>
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

        private static void CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            // A very faint key — geometry is legible in-editor; §7 says the
            // lantern carries real illumination at run time.
            light.color = HexColor("39414f");
            light.intensity = 0.18f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(58f, 35f, 0f);
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexColor("070709");
            cam.fieldOfView = 42f;
            go.AddComponent<AudioListener>();

            // The CinemachineBrain — the real Camera is driven by the
            // CinemachineCamera on the FollowCameraRig (Week 5). Added by
            // reflection (the DeNelle.Editor asmdef does not reference
            // Unity.Cinemachine; the package assembly is loaded in the editor).
            // DungeonController.ResolveUnityCamera() finds this camera via
            // Camera.main and feeds it to the hero for screen-relative WASD.
            if (AddComponentByName(go, TypeCinemachineBrain) == null)
            {
                Debug.LogWarning(
                    "[DungeonSceneBuilder] Could not add a CinemachineBrain to the Main " +
                    "Camera — the Cinemachine follow rig will not drive the view. Is the " +
                    "Unity.Cinemachine package installed?");
            }

            // Top-down isometric framing over the Garden Approach (§9.1 entry) —
            // the static seat; CinemachineBrain hands control to the follow rig
            // the moment play starts.
            var garden = FindRoom("garden-approach");
            float cx = (garden.MinX + garden.MaxX) * 0.5f;
            float cz = (garden.MinZ + garden.MaxZ) * 0.5f;
            go.transform.position = new Vector3(cx, YGround + 13f, cz - 9f);
            go.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        }

        // =====================================================================
        //  Geometry / collider / colour helpers
        // =====================================================================

        private static float LevelY(Level level)
        {
            switch (level)
            {
                case Level.Upper: return YUpper;
                case Level.Underground: return YUnder;
                default: return YGround;
            }
        }

        /// <summary>
        /// Guarantees a model instance has a collider so the Keeper cannot walk
        /// through it (acceptance §14.13 — no walk-through-walls). Adds a fitted
        /// box collider when the FBX import shipped none.
        /// </summary>
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

        // =====================================================================
        //  BUG-007 — every wall / structure mesh must carry a collider
        // =====================================================================

        /// <summary>
        /// Final hardening pass for BUG-007 ("no walk-through-walls"). Walks every
        /// object under "Walls" / "VerticalConnectors" and guarantees a collider,
        /// EXCEPT the layout's illusory walls (named with the <c>[ILLUSORY]</c>
        /// prefix per <see cref="BuildWallRun"/>) — those are deliberately
        /// collider-free hidden passages (the Keeper walks through into the
        /// secret room). <see cref="BuildWallRun"/> / <see cref="Prop"/> already
        /// call <see cref="EnsureCollider"/> per piece; this pass catches any
        /// wall/structure mesh whose FBX import stripped or never produced one,
        /// so a single re-run hardens the whole scene. Idempotent — a mesh that
        /// already has a collider is left untouched.
        /// </summary>
        private static void VerifyWallColliders(Transform root)
        {
            int hardened = 0;
            int illusorySkipped = 0;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;

                // Only act on wall-run / vertical-connector pieces — floors,
                // ceilings, props, lights and actors are out of scope here
                // (floors/props get their colliders at build time; ceilings are
                // deliberately collider-free).
                if (!IsWallStructure(t)) continue;

                // Illusory walls are intentionally non-colliding hidden passages.
                if (go.name.StartsWith("[ILLUSORY]"))
                {
                    illusorySkipped++;
                    continue;
                }

                if (go.GetComponentInChildren<Collider>() != null) continue;

                EnsureCollider(go);
                hardened++;
            }

            if (hardened > 0)
                Debug.Log($"[DungeonSceneBuilder] BUG-007 pass — added a collider to " +
                          $"{hardened} wall/structure mesh(es) that imported without one.");
            Debug.Log($"[DungeonSceneBuilder] BUG-007 pass — {illusorySkipped} illusory wall(s) " +
                      "left collider-free by design (hidden passages).");
        }

        /// <summary>
        /// True when <paramref name="t"/> is a wall-run or vertical-connector
        /// piece — i.e. it sits under a "Walls" group or the
        /// "VerticalConnectors" root. These are the meshes the Keeper must not
        /// be able to walk through.
        /// </summary>
        private static bool IsWallStructure(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == "Walls" || p.name == "VerticalConnectors")
                    return true;
            }
            return false;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Tints a model's first renderer — palette character (§6).</summary>
        private static void ApplyTint(GameObject go, Color color)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
            renderer.sharedMaterial = mat;
        }

        private static void ApplyEmissive(GameObject go, Color color, float intensity)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
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

        private static Component AddDungeonComponent(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"[DungeonSceneBuilder] Type '{fullTypeName}' not found — is the " +
                               "DeNelle.Dungeons assembly compiled? Component skipped.");
                return null;
            }
            return go.AddComponent(type);
        }

        /// <summary>
        /// Adds a component resolved purely by full type name (e.g. a Cinemachine
        /// type from the Unity.Cinemachine package). Like
        /// <see cref="AddDungeonComponent"/> but worded for the package case —
        /// the assembly is loaded in the editor even though the DeNelle.Editor
        /// asmdef does not list it as a reference. Returns null (and warns) when
        /// the type cannot be resolved.
        /// </summary>
        private static Component AddComponentByName(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogWarning($"[DungeonSceneBuilder] Type '{fullTypeName}' not found — the " +
                                 "owning package may not be installed. Component skipped.");
                return null;
            }
            // Some components may already be present via [RequireComponent] when
            // an earlier AddComponent pulled them in — reuse rather than double.
            var existing = go.GetComponent(type);
            return existing != null ? existing : go.AddComponent(type);
        }

        /// <summary>
        /// Returns a component resolved by full type name, or null when the type
        /// is unresolved or absent. Guards <see cref="GameObject.GetComponent(Type)"/>
        /// against a null Type (which would throw).
        /// </summary>
        private static Component GetComponentByName(GameObject go, string fullTypeName)
        {
            if (go == null) return null;
            var type = FindType(fullTypeName);
            return type == null ? null : go.GetComponent(type);
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

        // =====================================================================
        //  Build Settings + folder helpers
        // =====================================================================

        private static void EnsureBuildSettings()
        {
            var current = EditorBuildSettings.scenes;
            foreach (var s in current)
                if (s.path == DungeonScenePath) return; // already registered

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
            sb.AppendLine("[DungeonSceneBuilder] Healer's Cottage (D1) built.");
            sb.AppendLine($"  Scene: {DungeonScenePath}");
            sb.AppendLine($"  Rooms: 12 across 3 levels (ground 6 / upper 2 / underground 4).");
            sb.AppendLine($"  KayKit model instances: {_modelCount} " +
                          $"(walls {_wallCount}, floor tiles {_floorCount}, props {_propCount}).");
            sb.AppendLine($"  Static lights: {_lightCount}  (ambient floor {AmbientIntensity}).");
            sb.AppendLine($"  Placeholder primitives: {_placeholderCount}.");
            foreach (var p in _placeholders) sb.AppendLine($"    - {p}");
            sb.AppendLine("  Entry point: DeNelle.Editor.DungeonSceneBuilder.BuildHealersCottage");
            Debug.Log(sb.ToString());
        }
    }
}
