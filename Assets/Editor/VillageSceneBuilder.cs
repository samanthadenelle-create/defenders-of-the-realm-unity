// =============================================================================
// VillageSceneBuilder — Avalon village INTERIOR scene generator (Editor-only).
// -----------------------------------------------------------------------------
// One static entry point that the main Unity session runs (manually via the
// Defenders menu, or via the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage
//
// What it does — implements docs/avalon-village-layout-spec.md §1–§8, §10–§14
// (the WALLED INTERIOR). §9, the exterior wilderness, is a SEPARATE agent's
// job: this builder leaves flat Y=0 ground at the wall line for the seam.
//
//   §4  Curtain wall — a SHAPED RECTANGLE (~30×24 hex, south bow-out), NOT a
//       square. Driven by the reworked WallLayout (Segments / Gates). Four
//       cardinal gates are the only breaks.
//   §3  Two centerpieces side-by-side — Elarion the world-tree (violet emissive
//       veins, raised mound, 6-stone ring) + the Keeper's Keep (building_castle).
//   §7  Central plaza + N-S spine + E-W cross roads to the four gates.
//   §5  The five named gameplay buildings with the spec's KayKit assignments
//       and quadrant placements.
//   §6  City dressing — residential cluster (SW) + well, market quarter
//       (market / tavern / church), workshop quarter (NE) with blacksmith /
//       townhall, farm / orchard (E), northern shrine.
//   §8  Approach lanes + a WaveSpawnPoint per gate, beyond each gate.
//
// IDEMPOTENT — re-running BuildVillage() destroys every object under the
// generated "VillageRoot" (plus the Main Camera / Directional Light) and
// rebuilds from scratch. Safe to repeat.
//
// ASSEMBLY NOTE. DeNelle.Editor.asmdef references only Core / Data / Localization
// — NOT DeNelle.Village. So this builder takes NO compile-time dependency on
// the village MonoBehaviours: VillageController / WallSegment / Gate /
// HeartController / Building / WaveSpawnPoint are added by REFLECTION (resolved
// by full type name) and their [SerializeField] fields are set through
// SerializedObject. WallLayout's static Segments / Gates tables are likewise
// read reflectively.
//
// KAYKIT MODELS. Built from the "KayKit Medieval Hexagon Pack 1.0.1" under
// Assets/Models/KayKit/. NOTE — no separate KayKit Forest / Nature pack is
// imported, so Elarion (spec §3.1) uses the Hexagon pack's own largest tree
// mesh (decoration/nature/trees_A_large.fbx) scaled up; logged as a decision.
// Each model loads via AssetDatabase.LoadAssetAtPath<GameObject>; on a missed
// path the builder substitutes a clearly-labelled placeholder primitive and
// logs a warning. It NEVER blocks.
//
// This script does NOT run itself; the main session triggers it. It does NOT
// touch any .asmdef file.
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
    /// Editor utility that assembles the Avalon village interior scene from the
    /// KayKit Medieval Hexagon Pack models, per
    /// docs/avalon-village-layout-spec.md §1–§8 / §10–§14. Entry point:
    /// <see cref="BuildVillage"/>.
    /// </summary>
    // NOTE: split across partial files (WO-181) to keep each under ~800 lines.
    //   VillageSceneBuilder.cs          — entry (BuildVillage), constants, orchestration
    //   VillageSceneBuilder.Helpers.cs  — reflection bridge + model/material/strip utilities
    public static partial class VillageSceneBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";
        private const string VillageScenePath = ScenesDir + "/Village.unity";
        private const string TitleScenePath = ScenesDir + "/Title.unity";
        private const string DungeonScenePath = ScenesDir + "/Dungeon_HealersCottage.unity";
        private const string BattleScenePath = ScenesDir + "/ATBBattle.unity";

        /// <summary>Root for everything this builder generates -- cleared + rebuilt each run.</summary>
        private const string VillageRootName = "VillageRoot";

        // ── KayKit Medieval Hexagon Pack paths ───────────────────────────────
        // The folder name literally contains "(unity)" -- parentheses are valid
        // in Unity asset paths, so LoadAssetAtPath handles them directly.
        private const string HexPackRoot =
            "Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/";

        private const string HexTilesBase = HexPackRoot + "tiles/base/";
        private const string HexTilesRoads = HexPackRoot + "tiles/roads/";
        private const string HexNeutral = HexPackRoot + "buildings/neutral/";
        private const string HexDecoNature = HexPackRoot + "decoration/nature/";
        private const string HexDecoProps = HexPackRoot + "decoration/props/";

        // Hero capsule = 2m tall; owner directive (2026-05-20): drop from 4.5x
        // → 3.0x so housetops no longer dominate the OTS camera when the hero
        // walks past. Houses end at ~3.6 m (≈ 2× hero) — still imposing, walls
        // (which inherit the same multiplier) stay readable from camera height.
        private const float BuildingScale = 3.0f;

        /// <summary>
        /// Building colour variant used consistently across the town (spec §10
        /// implies one variant for symmetry). "green" reads as a warm,
        /// lived-in Avalon palette.
        /// </summary>
        private const string BuildingColor = "green";

        private static string Building(string baseName)
        {
            return HexPackRoot + "buildings/" + BuildingColor + "/" +
                   baseName + "_" + BuildingColor + ".fbx";
        }

        // ── Hex grid geometry ────────────────────────────────────────────────
        // KayKit medieval hexagons are flat-top. Flat-to-flat ≈ sqrt(3) ≈ 1.732u;
        // each offset row steps 1.5u. Tiles are laid on an offset (axial) grid so
        // they tessellate. World units are used everywhere else for placement.
        private const float HexWidth = 1.732f;   // flat-to-flat (X step within a row)
        private const float HexDepth = 1.5f;     // row step (Z)

        // ── Wall geometry mirror (Editor asmdef can't reference DeNelle.Village) ─
        private const float WallThicknessConst = 0.62f;
        private const float GateHalfWidthConst = 1.4f;
        // GateGapHalf mirrors WallLayout.GateGapHalf (= GateHalfWidth + 0.6f = 2.0f).
        // The wall ring has a 4 m opening per gate (GateGapHalf * 2).
        private const float GateGapHalfConst = GateHalfWidthConst + 0.6f; // = 2.0 m
        // Curtain-wall half-extents — MUST mirror WallLayout.WallHalfX / WallHalfZ.
        private const float WallHalfX = 28f;
        private const float WallHalfZ = 21f;
        private const float SouthBowDepth = 4f;
        // KayKit wall-piece yaw correction — owner-observed 2026-05-19 that the
        // straights sit ~90deg off and corners ~180deg off vs WallLayout's
        // local-X run assumption. Tunable; adjust against the screenshot.
        private const float WallStraightYawFix = 90f;
        private const float WallCornerYawFix = 180f;

        // ── Village MonoBehaviour type names (resolved by reflection) ────────
        private const string NsVillage = "DeNelle.Village";
        private const string TypeVillageController = NsVillage + ".VillageController";
        private const string TypeWallSegment = NsVillage + ".WallSegment";
        private const string TypeGate = NsVillage + ".Gate";
        private const string TypeHeartController = NsVillage + ".HeartController";
        private const string TypeBuilding = NsVillage + ".Building";
        private const string TypeWaveSpawnPoint = NsVillage + ".WaveSpawnPoint";
        private const string TypeWallLayout = NsVillage + ".WallLayout";

        // ── Week-4 gameplay-system type names (resolved by reflection) ───────
        // DeNelle.Editor.asmdef does NOT reference DeNelle.Village / DeNelle.Pets,
        // so every Week-4 gameplay type is touched by full-name reflection only —
        // never a direct type reference (that would break the Editor build).
        private const string TypeWaveManager = NsVillage + ".WaveManager";
        private const string TypeEnemy = NsVillage + ".Enemy";
        private const string TypeEnemyDamageable = NsVillage + ".EnemyDamageable";
        private const string TypeDragonBoss = NsVillage + ".DragonBoss";
        private const string TypeHeroAbilities = NsVillage + ".HeroAbilities";
        private const string TypeHeroLocomotion = NsVillage + ".HeroLocomotion";
        private const string TypeHeroBodySwapper = NsVillage + ".HeroBodySwapper";
        private const string TypeHeroAbilityInput = NsVillage + ".HeroAbilityInput";
        private const string TypeHeroAbilitiesHudBridge = NsVillage + ".HeroAbilitiesHudBridge";
        private const string TypeHeroCinemachineRig = NsVillage + ".HeroCinemachineRig";
        private const string TypeVillageCamera     = NsVillage + ".VillageCamera";
        // DEF-53: adaptive follow camera — added alongside VillageCamera; at runtime
        // SmartMobileCamera.EnforceSoleCamera() disables VillageCamera and takes over.
        private const string TypeSmartMobileCamera = NsVillage + ".SmartMobileCamera";
        private const string TypeWaveHudBridge = NsVillage + ".WaveHudBridge";
        private const string TypeDailyQuestCombatBridge = NsVillage + ".DailyQuestCombatBridge";
        private const string TypeBuildMenuHudBridge = NsVillage + ".BuildMenuHudBridge";
        private const string TypeBuildingInteractable = NsVillage + ".BuildingInteractable";
        private const string TypeDungeonPortal = NsVillage + ".DungeonPortal";
        private const string TypeVillageHudController = "DeNelle.HUD.VillageHudController";
        private const string TypeEventSystem = "UnityEngine.EventSystems.EventSystem";
        private const string TypeInputSystemUIInputModule = "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
        private const string TypeBuildMenu = NsVillage + ".BuildMenu";
        private const string NsPets = "DeNelle.Pets";
        private const string TypePetDeployer = NsPets + ".PetDeployer";

        // ── Ambient-townsfolk type names (Workstream D, resolved by reflection) ─
        private const string TypeAmbientNpc = NsVillage + ".AmbientNPC";
        private const string TypeTownsfolkBubble = NsVillage + ".TownsfolkBubble";
        private const string TypeTownsfolkController = NsVillage + ".TownsfolkController";

        // ── KayKit civilian-character model paths (ambient townsfolk) ────────
        // Mystery Monthly Series 5 — the catalog's named civilian stand-ins.
        // These packs keep their FBX directly under characters/ (no fbx(unity)
        // subfolder); LoadModel resolves the .fbx path directly.
        private const string MysterySeries5Root =
            "Assets/Models/KayKit/KayKit Mystery Monthly Series 5/";
        private static readonly string[] TownsfolkModelPaths =
        {
            MysterySeries5Root + "10 - April 2025 - Protagonists/characters/Protagonist_A.fbx",
            MysterySeries5Root + "10 - April 2025 - Protagonists/characters/Protagonist_B.fbx",
            MysterySeries5Root + "6 - December 2024 - Helpers/characters/Helper_A.fbx",
            MysterySeries5Root + "6 - December 2024 - Helpers/characters/Helper_B.fbx",
        };

        // ── Week-4 asset paths ───────────────────────────────────────────────
        /// <summary>KayKit Hollow-Walker skeleton mesh (the Week-4 wave enemy).</summary>
        private const string SkeletonMinionPath =
            "Assets/Models/KayKit/KayKit Skeletons 1.1/assets/fbx(unity)/Skeleton_Blade.fbx";
        /// <summary>BuildMenu UI Toolkit document the build menu UIDocument hosts.</summary>
        private const string BuildMenuUxmlPath =
            "Assets/_Modules/Village/Buildings/UI/BuildMenu.uxml";

        // Store wiring: the walk-up Marketplace trigger (DeNelle.Village) + the PackStore
        // UIDocument (DeNelle.Wallet) it opens. The store is ~built already; the builder
        // just places it in the village scene (MarketplaceInteractor auto-finds PackStore).
        private const string TypeMarketplaceInteractor = NsVillage + ".MarketplaceInteractor";
        private const string TypePackStore = "DeNelle.Wallet.PackStore";
        private const string PackStoreUxmlPath = "Assets/_Modules/Wallet/UI/PackStore.uxml";
        /// <summary>ForceFieldGate shader the gate force-field material runs.</summary>
        private const string ForceFieldShaderPath =
            "Assets/Shaders/ForceFieldGate.shader";
        /// <summary>Generated force-field material asset (created at build time).</summary>
        private const string ForceFieldMaterialPath =
            "Assets/Shaders/ForceFieldGate.mat";
        /// <summary>Folder generated Week-4 prefabs are written to.</summary>
        private const string GeneratedPrefabDir = "Assets/Prefabs/Village/Generated";

        /// <summary>
        /// The apex flying-boss prefab (the Black Dragon). Built by
        /// DragonAnimatorSetup.BuildDragonBossPrefab — carries a DragonBoss.
        /// Wired into <c>WaveManager._apexBossPrefab</c> for the apex wave.
        /// </summary>
        private const string BossDragonPrefabPath =
            GeneratedPrefabDir + "/Boss_Dragon.prefab";

        /// <summary>
        /// User-layer index for wave enemies — see ProjectSettings/TagManager.asset
        /// slot 8 ("Enemy"). HeroAbilities / Pet / PetDeployer enemy-mask fields
        /// are set to <c>1 &lt;&lt; EnemyLayer</c>; enemy GameObjects are put on it.
        /// </summary>
        private const int EnemyLayer = 8;

        // Running tallies for the summary log.
        private static int _placeholderCount;
        private static readonly List<string> _placeholders = new List<string>();
        private static int _groundCount;
        private static int _roadCount;
        private static int _dressingCount;
        private static int _propCount;

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the Avalon village interior scene per
        /// docs/avalon-village-layout-spec.md §1–§8 / §10–§14. Runnable via
        /// <c>-executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage</c>.
        /// Idempotent.
        /// </summary>
        [MenuItem("Defenders/Week 3/Build Village Scene")]
        public static void BuildVillage()
        {
            _placeholderCount = 0;
            _placeholders.Clear();
            _groundCount = 0;
            _roadCount = 0;
            _dressingCount = 0;
            _propCount = 0;

            EnsureFolder(ScenesDir);
            AssetDatabase.Refresh();

            // ── Open or create the Village scene ─────────────────────────────
            UnityEngine.SceneManagement.Scene scene;
            if (File.Exists(VillageScenePath))
                scene = EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Idempotency: nuke any prior generated root ───────────────────
            // WO-150: also destroy stray standalone roots from earlier bakes that
            // are no longer generated — the two dungeon portals (relocating to
            // world nodes) and any leftover Crystals prop — so a clean re-bake
            // leaves only the intended village (they were authored as their own
            // scene roots, outside VillageRoot, so the VillageRoot nuke missed them).
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == VillageRootName || go.name == "Main Camera" ||
                    go.name == "Directional Light" ||
                    go.name == "DungeonPortal_HealersCottage" ||
                    go.name == "DungeonPortal_FolksGranary" ||
                    go.name.StartsWith("DungeonPortal") ||
                    go.name == "Crystals" ||
                    // OuterWorld is its own scene now (OuterWorld.unity, loaded
                    // additively by WorldSceneLoader). Strip any stale copy left in
                    // Village.unity from before the scene split so it isn't duplicated.
                    go.name == "OuterWorldRoot")
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            var tWallLayout = FindType(TypeWallLayout);
            if (tWallLayout == null)
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.WallLayout not found -- is the DeNelle.Village assembly compiled? Wall ring will be empty.");

            // ── Scene root ───────────────────────────────────────────────────
            var root = new GameObject(VillageRootName);

            // ── Lighting / camera ────────────────────────────────────────────
            CreateDirectionalLight();   // soft dawn (§14 Q4 default)
            CreateCamera();
            // UI Toolkit button clicks need an EventSystem + InputSystemUIInputModule
            // (HUD buttons stayed silent without it — 2026-05-19).
            EnsureEventSystem();

            // ── VillageController orchestrator ───────────────────────────────
            var controllerGo = new GameObject("VillageController");
            controllerGo.transform.SetParent(root.transform, false);
            Component controller = AddVillageComponent(controllerGo, TypeVillageController);

            // ── Sub-roots ────────────────────────────────────────────────────
            var groundRoot = NewChild(root.transform, "Ground");
            var wallRoot = NewChild(root.transform, "Walls");
            var gateRoot = NewChild(root.transform, "Gates");
            var roadRoot = NewChild(root.transform, "Roads");
            var buildingRoot = NewChild(root.transform, "Buildings");
            var centerpieceRoot = NewChild(root.transform, "Centerpieces");
            var dressingRoot = NewChild(root.transform, "CityDressing");
            var approachRoot = NewChild(root.transform, "Approaches");

            // ── Ground floor — flat Y=0 hex grass, interior + 1-hex seam ─────
            BuildGroundFloor(groundRoot);

            // ── Curtain wall + gates (WallLayout-driven, shaped rectangle) ───
            int wallCount = BuildWallRing(wallRoot, tWallLayout, controller);
            int gateCount = BuildGates(gateRoot, tWallLayout, controller);
            // Owner 2026-05-20: at 3.0× scale the WallLayout-baked gate gap
            // (4 m wide) is too narrow versus the now-9 m-tall walls — segments
            // adjacent to gates visually close the arch. Sweep + cull any wall
            // section whose centre is within BuildingScale × 2 m of a gate.
            ClearWallsNearGates(wallRoot, gateRoot);

            // ── WO-101: polyperfect stone wall perimeter ─────────────────────
            // Replaces / supplements the KayKit wall ring with polyperfect stone
            // wall segments, corner towers, mid-wall towers and cardinal gates.
            // Runs after ClearWallsNearGates so the two systems don't fight.
            BuildWallPerimeter(wallRoot);

            // ── WO-104 Stage 2: moat ring + drawbridges around the curtain wall ──
            BuildMoat(wallRoot);

            // ── WO-104 §7: rampart stairs (inner-wall access, flanking each gate) ──
            BuildRamparts(wallRoot);

            // ── Plaza + road network (§7) ────────────────────────────────────
            BuildPlaza(roadRoot);
            BuildRoads(roadRoot, tWallLayout);

            // ── Centerpieces — Elarion (the Heart) only (§3) ─────────────────
            var heart = BuildElarion(centerpieceRoot);
            // WO-150 + DESIGN-DECISIONS #3 (CLAUDE.md §7 "No Keep building"): the
            // Keeper's Keep (building_castle + banner) is NOT one of the five mapped
            // buildings and contradicts canon; disabled so regen stops placing it.
            // BuildKeep(centerpieceRoot);   // WO-150: not in the 5-building roster — disabled

            // ── Five gameplay buildings (§5) ─────────────────────────────────
            int buildingCount = BuildBuildings(buildingRoot, controller);

            // ── City dressing (§6) ───────────────────────────────────────────
            // WO-150: SKIPPED — the KayKit dressing buildings (homes/tavern/church/
            // blacksmith/townhall/well) were deleted by the owner and re-spawned as
            // magenta missing-material meshes on regen. Skip-guarded like the orchard
            // so a full BuildVillage no longer resurrects them. (Method kept; the
            // dressingRoot stays empty.) Re-enable only if the dressing is re-arted.
            // BuildCityDressing(dressingRoot);   // WO-150: magenta-ghost regen — disabled

            // ── Approach lanes + wave spawn points (§8) ──────────────────────
            int spawnCount = BuildApproaches(approachRoot, tWallLayout, controller);

            // ── Wire the controller's serialized fields ──────────────────────
            WireController(controller, wallRoot, gateRoot, buildingRoot, heart);

            // ── Week-4 gameplay systems (waves / hero / pets / build menu) ───
            // WaveManager + HeroAbilities + PetDeployer + BuildMenu, all the
            // enemy / building prefabs, the gate force-field material — every
            // item from the three week4-*.md integration checklists.
            BuildGameplaySystems(root, gateRoot, heart);

            // ── Build Settings ───────────────────────────────────────────────
            EnsureBuildSettings();

            // ── Save the scene BEFORE the NavMesh bake ───────────────────────
            // The legacy UnityEditor.AI bake associates the generated NavMesh
            // asset with the scene FILE on disk, so the scene must already be
            // saved (a freshly-created Village.unity otherwise has no path).
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);

            // ── NavMesh — mark ground/walls navigation-static + bake ─────────
            // REQUIRED for the wave loop: enemies use NavMeshAgents and cannot
            // move without a baked NavMesh (week4-waves.md item 1). Done after
            // all geometry is placed + the scene is on disk, via the legacy
            // UnityEditor.AI API.
            // DOTR_SKIP_NAVMESH=1 skips the bake for crash-bisect builds only
            // (the NavMeshSettings + baked NavMesh asset are an early-load suspect
            // for the "level3 corrupted / Position out of bounds" player crash).
            if (System.Environment.GetEnvironmentVariable("DOTR_SKIP_NAVMESH") == "1")
                Debug.LogWarning("[VillageSceneBuilder] DOTR_SKIP_NAVMESH=1 — skipping NavMesh bake (crash-bisect test build).");
            else
                BakeVillageNavMesh(root);

            // ── Re-save so the baked-NavMesh scene reference is persisted ────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── HUD restore (2026-05-19) ─────────────────────────────────────
            // BuildVillage tears down VillageRoot, which removes the HUD GameObject
            // that WallRepairSceneSetup.AddWallRepairToVillage authored. Chain it
            // back in so the HUD survives every BuildVillage run instead of
            // requiring the menu to be hit manually.
            try { WallRepairSceneSetup.AddWallRepairToVillage(); }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VillageSceneBuilder] HUD auto-restore via " +
                                 "WallRepairSceneSetup.AddWallRepairToVillage failed: " + ex.Message);
            }

            // Wire WaveManager → HUD so the wave-countdown timer actually
            // updates (the HUD shipped without a subscriber — 2026-05-19).
            WireWaveHudBridge();
            // Pipe wave clears into the daily-quest service so the combat
            // slot's "Clear N waves" template ticks during real play.
            WireDailyQuestCombatBridge();
            // Wire HUD Build button → BuildMenu.Open (the click did nothing
            // before this bridge — 2026-05-20 PO observation).
            WireBuildMenuHudBridge();
            // Wire HUD Q/W/E/R buttons → HeroAbilities.TryCast (clicks were
            // also dead — 2026-05-20 PO observation).
            WireHeroAbilitiesHudBridge();
            // Add proximity prompts to every Building so the player can press F
            // to interact (2026-05-20 PO observation: no interaction).
            WireBuildingInteractables();
            // WO-150: SKIPPED — dungeon entrances are RELOCATING to world nodes in
            // the outer regions (WO-111 / outer-world), not the village. The in-town
            // portal generator (which also imported as magenta/missing) is disabled
            // so regen no longer re-spawns it. (Method kept for the world-node WO.)
            // SpawnDungeonPortal();   // WO-150: relocating to world nodes — disabled
            // WO-126: repair any renderer with a missing/error material (the Crystals.fbx
            // drop-in imported with no material -> magenta) BEFORE the scene is saved, so the
            // fix lands in Village.unity and the player build.
            RepairMissingMaterials();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);

            // Build the exterior wilderness terrain so the world reads as a
            // walled village inside a landscape, not a void (2026-05-19).
            try { ExteriorTerrainBuilder.BuildExterior(); }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VillageSceneBuilder] Exterior terrain build failed: " + ex.Message);
            }

            Debug.Log($"[VillageSceneBuilder] BuildVillage complete -- " +
                      $"{_groundCount} ground tiles, {wallCount} wall sections/corners, " +
                      $"{gateCount} cardinal gates, {_roadCount} plaza/road tiles, " +
                      $"{buildingCount} gameplay buildings, {_dressingCount} dressing buildings, " +
                      $"{_propCount} props/fences, {spawnCount} wave spawn points, " +
                      $"1 Elarion + 1 Keep. Week-4 gameplay systems (WaveManager / " +
                      $"HeroAbilities / PetDeployer / BuildMenu) + NavMesh wired. " +
                      $"{_placeholderCount} placeholder primitive(s)" +
                      (_placeholderCount > 0 ? ": " + string.Join(", ", _placeholders) : "."));
        }

        /// <summary>
        /// NavMesh-only rebake. Opens the EXISTING saved Village scene and rebakes
        /// the NavMesh against its CURRENT contents — it does NOT regenerate any
        /// geometry, so manual scene cleanup (removed signs/trees, edited walls)
        /// is preserved. Use this, NOT BuildVillage (which tears down VillageRoot
        /// and rebuilds from the placement tables), to remap nav after hand edits.
        /// Headless: -executeMethod DeNelle.Editor.VillageSceneBuilder.RebakeNavMeshOnly
        /// </summary>
        [MenuItem("Defenders/Week 3/Rebake NavMesh Only")]
        public static void RebakeNavMeshOnly()
        {
            var scene = EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);

            var root = GameObject.Find(VillageRootName);
            if (root == null)
            {
                Debug.LogError("[VillageSceneBuilder] RebakeNavMeshOnly: VillageRoot not found in " +
                               VillageScenePath + " — nothing baked.");
                return;
            }

            BakeVillageNavMesh(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VillageSceneBuilder] RebakeNavMeshOnly complete — NavMesh remapped against " +
                      "current scene contents and saved to " + VillageScenePath + ".");
        }

        /// <summary>
        /// WO-126: repairs any renderer in the active scene whose material is null or on
        /// Unity's error shader (renders magenta). Crystal-named objects (the Crystals.fbx
        /// drop-in, which imported with no material) get a glowing aether-cyan gem material
        /// matching the Aether Crystals theme; anything else gets a neutral stone fallback.
        /// Valid materials are untouched. Runs before SaveScene so the fix lands in the build;
        /// the owner can refine the crystal look live in the editor afterwards.
        /// </summary>
        private static void RepairMissingMaterials()
        {
            Material crystalMat = null;
            int fixedSlots = 0;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        bool err = m == null || m.shader == null
                                   || m.shader.name.Contains("InternalError")
                                   || m.shader.name == "Hidden/InternalErrorShader";
                        if (!err) continue;

                        bool isCrystal = root.name.IndexOf("crystal", System.StringComparison.OrdinalIgnoreCase) >= 0
                                       || r.name.IndexOf("crystal", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        Material fix;
                        if (isCrystal)
                        {
                            if (crystalMat == null)
                            {
                                var sh = Shader.Find("Universal Render Pipeline/Lit");
                                crystalMat = new Material(sh) { name = "AetherCrystal (repair)" };
                                if (crystalMat.HasProperty("_BaseColor"))  crystalMat.SetColor("_BaseColor", new Color(0.32f, 0.80f, 0.95f));
                                if (crystalMat.HasProperty("_Smoothness")) crystalMat.SetFloat("_Smoothness", 0.85f);
                                if (crystalMat.HasProperty("_EmissionColor"))
                                {
                                    crystalMat.EnableKeyword("_EMISSION");
                                    crystalMat.SetColor("_EmissionColor", new Color(0.12f, 0.42f, 0.60f));
                                }
                            }
                            fix = crystalMat;
                        }
                        else
                        {
                            if (_perimeterStoneFallback == null)
                            {
                                var sh = Shader.Find("Universal Render Pipeline/Lit");
                                _perimeterStoneFallback = new Material(sh) { name = "PerimeterStoneFallback" };
                                if (_perimeterStoneFallback.HasProperty("_BaseColor"))
                                    _perimeterStoneFallback.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.49f));
                            }
                            fix = _perimeterStoneFallback;
                        }
                        mats[i] = fix;
                        changed = true;
                        fixedSlots++;
                        Debug.Log($"[VillageSceneBuilder] WO-126 RepairMissingMaterials: '{r.name}' (root '{root.name}') " +
                                  $"was {(m == null ? "NULL" : m.shader?.name)} -> {(isCrystal ? "aether-crystal" : "stone")} fallback.");
                    }
                    if (changed) r.sharedMaterials = mats;
                }
            }
            Debug.Log($"[VillageSceneBuilder] WO-126 RepairMissingMaterials: fixed {fixedSlots} magenta slot(s).");
        }

        // =====================================================================
        //  Ground floor — flat Y=0 hex grass disc (interior + seam ring)
        // =====================================================================

        /// <summary>
        /// Lays a flat Y=0 floor of KayKit hex grass tiles covering the walled
        /// interior plus a 1-hex seam ring just outside the wall line, so the
        /// exterior agent (§9) has flat ground to blend its terrain into.
        /// </summary>
        private static void BuildGroundFloor(Transform parent)
        {
            var grass = LoadModel(HexTilesBase + "hex_grass.fbx");

            // Cover a rectangle wide enough to clear the curtain wall + south
            // bow + approach lanes (the exterior agent replaces ground beyond
            // the approach zone with Terrain).
            float halfX = WallHalfX + 14f;
            float halfZ = WallHalfZ + SouthBowDepth + 14f;
            int cols = Mathf.CeilToInt(halfX / HexWidth);
            int rows = Mathf.CeilToInt(halfZ / HexDepth);

            for (int row = -rows; row <= rows; row++)
            {
                bool oddRow = (row & 1) != 0;
                float z = row * HexDepth;
                float xShift = oddRow ? HexWidth * 0.5f : 0f;
                for (int col = -cols; col <= cols; col++)
                {
                    float x = col * HexWidth + xShift;
                    if (Mathf.Abs(x) > halfX || Mathf.Abs(z) > halfZ) continue;

                    var tile = InstantiateModel(grass, "hex_grass.fbx",
                        $"GroundTile ({col},{row})");
                    tile.transform.SetParent(parent, false);
                    tile.transform.localPosition = new Vector3(x, 0f, z);
                    if (grass == null)
                    {
                        tile.transform.localScale = new Vector3(HexWidth, 0.2f, HexWidth);
                        ApplyColor(tile, new Color(0.38f, 0.50f, 0.27f));
                    }
                    _groundCount++;
                }
            }

            if (grass == null)
                Debug.LogWarning("[VillageSceneBuilder] hex_grass.fbx missing -- ground floor used placeholder discs.");
        }

        // =====================================================================
        //  Curtain wall — driven by WallLayout.Segments (shaped rectangle)
        // =====================================================================

        private static int BuildWallRing(Transform parent, Type tWallLayout, Component controller)
        {
            if (tWallLayout == null) return 0;

            var segments = ReadEnumerable(tWallLayout, "Segments");
            if (segments == null)
            {
                Debug.LogError("[VillageSceneBuilder] WallLayout.Segments returned null -- wall ring skipped.");
                return 0;
            }

            const float wallHeight = 3.0f; // tier 0 (Wooden) -- WALL_TARGET_H[0]
            int count = 0;

            var straightModel = LoadModel(HexNeutral + "wall_straight.fbx");
            var cornerModel = LoadModel(HexNeutral + "wall_corner_A_outside.fbx");

            foreach (var seg in segments)
            {
                int index = (int)GetMember(seg, "Index");
                float length = (float)GetMember(seg, "Length");
                bool corner = (bool)GetMember(seg, "Corner");
                Vector3 pos = (Vector3)GetMember(seg, "Position");
                Quaternion rot = (Quaternion)GetMember(seg, "Rotation");

                var go = new GameObject((corner ? "WallCorner-" : "WallSection-") + index);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);

                var model = corner ? cornerModel : straightModel;
                string meshName = corner ? "wall_corner_A_outside.fbx" : "wall_straight.fbx";
                GameObject visual = InstantiateModel(model, meshName,
                    corner ? $"wall_corner ({index})" : $"wall_straight ({index})");
                visual.transform.SetParent(go.transform, false);

                // WO-104: the poly castle curtain walls (BuildWallPerimeter) are now the
                // perimeter VISUAL — hide this KayKit wall mesh so they don't double-stack.
                // The WallSegment + its collider on `go` stay for gameplay (barrier/repair).
                foreach (var rr in visual.GetComponentsInChildren<Renderer>()) rr.enabled = false;

                // KayKit wall pieces' native orientation differs from WallLayout's
                // local-X run assumption — owner observed 2026-05-19 that straights
                // sit ~90deg off and corners ~180deg off. Tunable yaw correction
                // applied to the visual mesh (the WallSegment collider on `go`
                // keeps WallLayout's transform).
                visual.transform.localRotation = Quaternion.Euler(
                    0f, corner ? WallCornerYawFix : WallStraightYawFix, 0f);

                // Match the 4.5x building scale so walls don't look dwarfed by
                // the (now 6 m tall) houses. FitWallVisualToRun below stretches
                // the run axis independently, so this is safe — height +
                // thickness get the lift, length stays at runLength.
                if (model != null)
                    visual.transform.localScale *= BuildingScale;

                if (model != null && !corner)
                {
                    // KayKit wall_straight is a fixed-length module; stretch its
                    // long horizontal axis so the section fills its computed run
                    // length EXACTLY (WallLayout already insets the run ends off
                    // the corner blocks, so a flush fit leaves no gap/overlap).
                    // Corner pieces stay at native scale. FitWallVisualToRun
                    // measures in the visual's own local space, so the result is
                    // immune to both WallStraightYawFix and the segment rotation
                    // on `go` -- the old world-AABB measure read thickness, not
                    // length, which is what produced the gaps/overlaps.
                    FitWallVisualToRun(visual, length);
                }
                else if (model == null)
                {
                    visual.transform.localScale = corner
                        ? new Vector3(length, wallHeight, length)
                        : new Vector3(length, wallHeight, WallThicknessConst);
                    visual.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, 0f);
                    ApplyColor(visual, new Color(0.49f, 0.45f, 0.40f));
                }

                var segComp = AddVillageComponent(go, TypeWallSegment);
                if (segComp != null)
                {
                    InvokeConfigure(segComp, "Configure", seg, wallHeight);
                    RegisterWith(controller, "RegisterWallSegment", segComp, seg);
                }
                count++;
            }
            return count;
        }

        // =====================================================================
        //  Cardinal gates — driven by WallLayout.Gates
        // =====================================================================

        private static int BuildGates(Transform parent, Type tWallLayout, Component controller)
        {
            if (tWallLayout == null) return 0;

            var gates = ReadEnumerable(tWallLayout, "Gates");
            if (gates == null)
            {
                Debug.LogError("[VillageSceneBuilder] WallLayout.Gates returned null -- gates skipped.");
                return 0;
            }

            var gateModel = LoadModel(HexNeutral + "wall_straight_gate.fbx");

            int count = 0;
            foreach (var gap in gates)
            {
                string id = (string)GetMember(gap, "Id");
                string direction = (string)GetMember(gap, "Direction");
                Vector3 pos = (Vector3)GetMember(gap, "Position");
                Quaternion rot = (Quaternion)GetMember(gap, "Rotation");

                var go = new GameObject($"Gate-{id} ({direction})");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);

                GameObject visual;
                if (gateModel != null)
                {
                    visual = InstantiateModel(gateModel, "wall_straight_gate.fbx",
                        $"gate ({direction})");
                }
                else
                {
                    visual = new GameObject($"[PLACEHOLDER] gate ({direction})");
                    NotePlaceholder($"gate ({direction})");
                    float half = GateHalfWidthConst;
                    var left = PrimitiveChild(visual.transform, "PillarL", PrimitiveType.Cube,
                        new Vector3(-half, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f),
                        new Color(0.49f, 0.45f, 0.40f));
                    var right = PrimitiveChild(visual.transform, "PillarR", PrimitiveType.Cube,
                        new Vector3(half, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f),
                        new Color(0.49f, 0.45f, 0.40f));
                    var field = PrimitiveChild(visual.transform, "ForceField", PrimitiveType.Cube,
                        new Vector3(0f, 1.6f, 0f), new Vector3(half * 2f, 3.0f, 0.1f),
                        new Color(0.61f, 0.44f, 1f, 1f));
                    _ = left; _ = right; _ = field;
                }
                visual.transform.SetParent(go.transform, false);

                // WO-136: hide the KayKit gate visual mesh — the polyperfect
                // perimeter (BuildWallPerimeter) is now the visible wall+gate art,
                // so the old KayKit gate arch double-stacked as "old outer gates."
                // Keep the GameObject + its gate gameplay/collider/force-field; only
                // the visual renderers are disabled (same approach as the wall ring).
                foreach (var rr in visual.GetComponentsInChildren<Renderer>()) rr.enabled = false;

                // KayKit gate piece (wall_straight_gate) needs the same yaw
                // correction as wall_straight — owner-observed 2026-05-19 the
                // gates sit ~90deg off.
                visual.transform.localRotation = Quaternion.Euler(0f, WallStraightYawFix, 0f);

                // Match the 4.5x wall scaling so the gate doesn't look like a
                // pinhole between two giant wall sections (2026-05-19 PO P0).
                if (gateModel != null)
                    visual.transform.localScale *= BuildingScale;

                // Owner 2026-05-20 ("hole where walls don't touch door"):
                // WallLayout leaves a GAP of GateGapHalf*2 = 4 m, but the
                // gate mesh's natural width matches only GateHalfWidth*2 =
                // 2.8 m — so a 1.43× stretch on the gate's run-axis fills
                // the gap so the wall sections meet the gate edge cleanly.
                // After WallStraightYawFix (90°) the gate's local X is the
                // run direction, so scale.x is what to stretch.
                if (gateModel != null)
                {
                    Vector3 s = visual.transform.localScale;
                    s.x *= (1.4f + 0.6f) / 1.4f;
                    visual.transform.localScale = s;
                }

                // Owner 2026-05-20 ("purple frame on gate"): the KayKit gate
                // mesh has a stone-arch submesh whose material falls through
                // to URP's magenta fallback in the player build. Attach the
                // TripoMaterialFixer with a stone-grey tint so the arch
                // reads as proper stone instead of the broken-material pink.
                var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fixerType != null && gateModel != null)
                {
                    var fixer = visual.AddComponent(fixerType);
                    var setTint = fixerType.GetMethod("SetFallbackTint");
                    setTint?.Invoke(fixer, new object[] { new Color(0.52f, 0.50f, 0.46f) });
                }

                // Owner 2026-05-20 ("still rocks in front of gate cannot
                // access gate"): the KayKit gate FBX ships with a MeshCollider
                // that follows the stone arch + wall geometry, which blocks
                // the hero's CapsuleCast right in the gate threshold. Strip
                // all colliders on the gate visual so the doorway is genuinely
                // walk-through. The wall sections on either side keep their
                // colliders so the perimeter remains solid.
                StripColliders(visual);

                // DEF-19 (2026-05-27): ClearWallsNearGates uses
                // radius = BuildingScale*2 = 6 m to widen the visual gate
                // opening, but that destroys wall-section BoxColliders in a
                // [GateGapHalf=2 m … 6 m] wing on EACH SIDE of the gate
                // centre.  The gate visual fills the centre 4 m, leaving the
                // outer 4 m wings on each side as invisible collision gaps the
                // hero can walk through.  Fix: add two invisible wing-blocker
                // BoxColliders directly on the gate root (`go`) to close those
                // gaps.  Local X of `go` is the wall run direction (WallLayout
                // convention), so ±wingCenterLocal positions the blockers
                // correctly for all four cardinal gates.
                {
                    const float wingInner       = GateGapHalfConst;          // 2.0 m — gate visual edge
                    const float wingOuter       = BuildingScale * 2f;         // 6.0 m — ClearWallsNearGates radius
                    const float wingLength      = wingOuter - wingInner;      // 4.0 m per wing
                    const float wingCenterLocal = (wingInner + wingOuter) * 0.5f; // 4.0 m from gate centre
                    const float gateWallH       = 3.0f;                       // mirrors wallHeight in BuildWallRing
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size   = new Vector3(wingLength, gateWallH, WallThicknessConst);
                        bc.center = new Vector3(side * wingCenterLocal, gateWallH * 0.5f, 0f);
                    }
                }

                // Castle arch removed per owner direction 2026-05-20: the
                // Tripo castle ballast Tower FBX rendered as a pink ghost in
                // the player build even after the URP material fix, so the
                // gate falls back to the bare wall_straight_gate piece. No
                // force-field shimmer either — the gate reads as a simple
                // open passage now.

                var gateComp = AddVillageComponent(go, TypeGate);
                if (gateComp != null)
                {
                    InvokeConfigure(gateComp, "Configure", gap);
                    RegisterWith(controller, "RegisterGate", gateComp, gap);
                }
                count++;
            }
            return count;
        }

        // =====================================================================
        //  Plaza + road network (§7)
        // =====================================================================

        /// <summary>
        /// The central paved plaza between Elarion and the Keep (§3.3 / §7.3) —
        /// a slightly-raised patch of stone road tiles. Simple stone paving
        /// (§14 Q2 default).
        /// </summary>

        /// <summary>
        /// Rings a building plot with a low KayKit fence (dressing only — the
        /// fence pieces have their colliders stripped so they never block
        /// pathing, per spec §5).
        /// </summary>

        // =====================================================================
        //  Camera + light
        // =====================================================================


        /// <summary>
        /// Spawns a glowing stone arch near the south gate that routes the
        /// player into Dungeon_HealersCottage via DungeonPortal (DeNelle.Village).
        /// Idempotent — a prior "DungeonPortal" GO is destroyed first.
        /// </summary>
        /// <summary>
        /// Loads Assets/Models/CastleGate/castle+ballast+Tower.fbx and parents
        /// a stripped-collider instance to <paramref name="gate"/>. Owner ask
        /// 2026-05-20: replaces the violet force-field shimmer with a real
        /// castle arch so the gate reads as a fortified entrance.
        /// </summary>
        private static void AttachCastleArch(Transform gate)
        {
            const string CastlePath = "Assets/Models/CastleGate/castle+ballast+Tower.fbx";
            var model = LoadModel(CastlePath);
            if (model == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] castle+ballast+Tower.fbx not found at " +
                                 $"'{CastlePath}' — skipping castle arch.");
                return;
            }
            var arch = InstantiateModel(model, "castle+ballast+Tower.fbx", "CastleArch");
            arch.transform.SetParent(gate, false);

            // Align with the gate opening: the existing wall_straight_gate
            // already takes WallStraightYawFix; the arch needs the same yaw so
            // it sits flush rather than 90deg across the opening.
            arch.transform.localPosition = Vector3.zero;
            arch.transform.localRotation = Quaternion.Euler(0f, WallStraightYawFix, 0f);

            // Normalise arch to the gate width so a single FBX serves all
            // four cardinal gates regardless of native KayKit / Tripo scale.
            // Target: ~2 × gate half-width on the wall axis, BuildingScale up.
            float targetWidth = GateHalfWidthConst * 2f * BuildingScale;
            var renderers = arch.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float maxDim = Mathf.Max(b.size.x, b.size.z);
                if (maxDim > 0.01f)
                    arch.transform.localScale *= (targetWidth / maxDim);
            }
            else
            {
                arch.transform.localScale = Vector3.one * BuildingScale;
            }

            // Strip every collider so hero + pets walk through unobstructed
            // (the wall_straight_gate piece already supplies the side-pillar
            // colliders that read as the gate's solid bits).
            foreach (var c in arch.GetComponentsInChildren<Collider>(true))
                if (c != null) UnityEngine.Object.DestroyImmediate(c);

            // The Tripo FBX imports with Phong materials that URP can't render
            // (owner 2026-05-20: arch read as transparent pink ghost). Mount
            // the TripoMaterialFixer runtime component with a Resources-loaded
            // fallback texture so the arch is properly rebuilt in URP/Lit when
            // the player loads the scene.
            var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
            if (fixerType != null)
            {
                var fixer = arch.AddComponent(fixerType);
                var setMethod = fixerType.GetMethod("SetFallbackTexture");
                setMethod?.Invoke(fixer, new object[] { "Textures/CastleArch" });
            }
        }


        /// <summary>
        /// Wires WaveManager → VillageHudController so the HUD's wave timer
        /// actually updates. Adds a <c>WaveHudBridge</c> onto the WaveManager
        /// GameObject (the bridge talks to the HUD by reflection so DeNelle.Village
        /// does not have to reference DeNelle.HUD).
        /// </summary>


        /// <summary>
        /// Destroys any wall section / corner whose world centre lies within
        /// <c>BuildingScale × 2 m</c> of any gate's world position. The
        /// WallLayout already carves a 4 m gap for each gate, but at 3.0×
        /// scale that gap is visually narrow vs the now-9 m-tall walls; the
        /// flanking segments end up obscuring the gate's arch from the hero's
        /// eye line. Owner direction 2026-05-20.
        /// </summary>
        private static void ClearWallsNearGates(Transform wallRoot, Transform gateRoot)
        {
            if (wallRoot == null || gateRoot == null) return;
            float radius = BuildingScale * 2f;
            float r2 = radius * radius;

            // Cache gate positions so we don't enumerate twice in the inner loop.
            var gates = new System.Collections.Generic.List<Vector3>(gateRoot.childCount);
            for (int i = 0; i < gateRoot.childCount; i++)
                gates.Add(gateRoot.GetChild(i).position);
            if (gates.Count == 0) return;

            // Destroy any wall plot whose centre is inside any gate's radius.
            // Iterate snapshot so destroying children doesn't skew the loop.
            var walls = new System.Collections.Generic.List<Transform>(wallRoot.childCount);
            for (int i = 0; i < wallRoot.childCount; i++) walls.Add(wallRoot.GetChild(i));
            int removed = 0;
            foreach (var wall in walls)
            {
                if (wall == null) continue;
                Vector3 wp = wall.position;
                foreach (var gp in gates)
                {
                    float dx = wp.x - gp.x;
                    float dz = wp.z - gp.z;
                    if (dx * dx + dz * dz <= r2)
                    {
                        UnityEngine.Object.DestroyImmediate(wall.gameObject);
                        removed++;
                        break;
                    }
                }
            }
            if (removed > 0)
                Debug.Log($"[VillageSceneBuilder] ClearWallsNearGates removed {removed} wall section(s) " +
                          $"within {radius:F1} m of a gate.");
        }

        // =====================================================================
        //  WO-101: polyperfect stone wall perimeter
        // =====================================================================

        /// <summary>
        /// Builds the polyperfect stone wall perimeter around the village using
        /// Low Poly Ultimate Pack _M prefabs. Layout:
        ///   North wall  z = +33, segments every 3 m, x = -42 to +42.
        ///   South wall  z = -33, same + Gate_Medieval_Medium at x = 0.
        ///   East wall   x = +42, segments every 3 m, z = -33 to +33 + Gate_Medieval_Small at z = 0.
        ///   West wall   x = -42, same + Gate_Medieval_Small at z = 0.
        ///   Corner towers (SM_Tower_Castle_Round) at (±42, 0, ±33).
        ///   Mid-wall towers (SM_Tower_Medieval_Wood) at each wall midpoint.
        /// All placed under <paramref name="wallRoot"/> so ClearWallsNearGates /
        /// BakeVillageNavMesh automatically include them.
        /// </summary>
        private static void BuildWallPerimeter(Transform wallRoot)
        {
            // ── Polyperfect prefab paths ──────────────────────────────────────
            // WO-104: the real curtain wall is Wall_Stone_3x3_A in Buildings_M/parts/
            // Building Walls_M/ (Medieval_M never had "Wall_Medieval_Stone" → walls
            // were silently missing). Towers/gates stay in Medieval_M.
            const string WallSeg      = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Buildings_M/parts/Building Walls_M/Wall_Stone_3x3_A.prefab";
            const string GateMedium   = PolyMedievalDir + "Gate_Medieval_Medium.prefab";
            const string GateSmall    = PolyMedievalDir + "Gate_Medieval_Small.prefab";
            const string TowerCorner  = PolyMedievalDir + "Tower_Castle_Round.prefab";
            const string TowerMidWall = PolyMedievalDir + "Tower_Castle_Square.prefab"; // WO-104 square watchtower

            // Load prefabs once (null → graceful placeholder skip).
            var wallSegModel  = AssetDatabase.LoadAssetAtPath<GameObject>(WallSeg);
            var gateMedModel  = AssetDatabase.LoadAssetAtPath<GameObject>(GateMedium);
            var gateSmModel   = AssetDatabase.LoadAssetAtPath<GameObject>(GateSmall);
            var towerCorModel = AssetDatabase.LoadAssetAtPath<GameObject>(TowerCorner);
            var towerMidModel = AssetDatabase.LoadAssetAtPath<GameObject>(TowerMidWall);

            if (wallSegModel == null)
                Debug.LogWarning("[VillageSceneBuilder] WO-101 Wall_Medieval_Stone prefab not found " +
                                 $"at '{WallSeg}' — wall perimeter will have no stone segments " +
                                 "(polyperfect pack re-import needed on this machine).");

            // ── Layout constants ──────────────────────────────────────────────
            const float wallZ    = 33f;  // north/south wall Z
            const float wallX    = 42f;  // east/west wall X
            const float segStep  =  3f;  // segment pitch (pack snaps at 3 × 3 m)

            var perimeterRoot = new GameObject("WallPerimeter");
            perimeterRoot.transform.SetParent(wallRoot, false);
            var pr = perimeterRoot.transform;

            // ── Helpers: instantiate, then size per type ──────────────────────
            // Poly _M prefabs are tiny AND narrow natively -> uniform scaling made tall
            // thin bars with gaps (the picket-fence the owner saw). Walls must be STRETCHED
            // horizontally to fill the 3 m run and pinned to a fixed height so neighbours
            // abut into a continuous curtain. Towers/gates get a uniform NormalizeProp.
            // Wall sizing is measured from WORLD renderer bounds at yaw 0 (world x/z == local
            // x/z before rotating), so the maths needs no rotation bookkeeping.
            const float wallHeight  = 5f;  // curtain wall world height
            const float towerTarget = 9f;  // corner/mid towers stand above the curtain
            // Gate FIX (2026-05-30): NormalizeProp fits the LARGEST dimension to this
            // value. At 6 the gatehouse came out shorter than the 5 m wall + a tiny
            // opening ("way too small", all gates). The gatehouse prefab is taller
            // than wide, so fitting its height to ~10 makes it read as a proper
            // gatehouse rising to/above the curtain with a hero-passable arch.
            const float gateTarget  = 10f; // gatehouse — taller than the 5 m curtain, full-size arch
            bool loggedTile = false;

            System.Func<GameObject, string, Vector3, GameObject> Make =
                (prefab, label, pos) =>
            {
                if (prefab == null) return null;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (inst == null) return null;
                inst.name = label;
                inst.transform.SetParent(pr, false);
                inst.transform.position = pos;
                RepairPerimeterMaterials(inst);   // WO-126: stone fallback for null/error material slots (magenta gate arch)
                return inst;
            };

            System.Action<GameObject, string, Vector3, float> Wall =
                (prefab, label, pos, yaw) =>
            {
                var g = Make(prefab, label, pos);
                if (g == null) return;
                var rs = g.GetComponentsInChildren<Renderer>();
                if (rs != null && rs.Length > 0)
                {
                    Bounds wb = rs[0].bounds;                                   // world AABB, yaw 0
                    for (int i = 1; i < rs.Length; i++) wb.Encapsulate(rs[i].bounds);
                    if (!loggedTile)
                    {
                        Debug.Log($"[VillageSceneBuilder] WO-104 wall tile native world bounds = {wb.size} " +
                                  $"(localScale {g.transform.localScale}); fitting run->{segStep}m height->{wallHeight}m.");
                        loggedTile = true;
                    }
                    var s = g.transform.localScale;
                    // Scale run axis to the 3 m pitch, thickness axis up to a SUBSTANTIAL
                    // 1.2 m (native 0.35 m read as thin bars/posts with the void showing
                    // through), height to wallHeight. Run = the longer horizontal extent.
                    const float wallThick   = 1.2f;
                    const float wallOverlap = 1.5f;   // run spans 1.5x the 3 m pitch so the tile's
                                                      // solid stone (narrower than its 3 m bbox)
                                                      // overlaps its neighbour -> no gaps between segments
                    if (wb.size.x >= wb.size.z)
                    {
                        if (wb.size.x > 0.001f) s.x *= (segStep * wallOverlap) / wb.size.x;   // run, overlapped
                        if (wb.size.z > 0.001f) s.z *= wallThick / wb.size.z;                  // thickness -> 1.2 m
                    }
                    else
                    {
                        if (wb.size.z > 0.001f) s.z *= (segStep * wallOverlap) / wb.size.z;
                        if (wb.size.x > 0.001f) s.x *= wallThick / wb.size.x;
                    }
                    if (wb.size.y > 0.001f) s.y *= wallHeight / wb.size.y;
                    g.transform.localScale = s;
                }
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                // DIAGNOSTIC (WO-158 castle debris): log each segment's FINAL world
                // bounds + position so we can see which pieces come out flat/wrong
                // (the slabs embedded in the wall). Remove once the cause is found.
                {
                    var dr = g.GetComponentsInChildren<Renderer>();
                    if (dr != null && dr.Length > 0)
                    {
                        Bounds fb = dr[0].bounds;
                        for (int i = 1; i < dr.Length; i++) fb.Encapsulate(dr[i].bounds);
                        Debug.Log($"[WALLDIAG] {g.name} pos={g.transform.position} " +
                                  $"worldSize={fb.size} center={fb.center} scale={g.transform.localScale}");
                    }
                }
                StripColliders(g);                                            // WallSegment colliders (BuildWallRing) own gameplay
                StripRigidbodies(g);
            };

            System.Action<GameObject, string, Vector3, float, float> Big =
                (prefab, label, pos, yaw, target) =>
            {
                var g = Make(prefab, label, pos);
                if (g == null) return;
                NormalizeProp(g, target);                                     // uniform measure + scale
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                StripColliders(g);
                StripRigidbodies(g);
            };

            // ── North wall (z = +33) — segments + gate at x = 0 (WO-158: 4th gate) ──
            // The "too small / all gates broken" issue was a SCALE bug (gateTarget,
            // fixed above to 10), not a north-specific fault — so north keeps its gate.
            for (float x = -wallX + segStep * 0.5f; x < wallX; x += segStep)
            {
                if (Mathf.Abs(x) < 3f) continue;   // 6 m opening for the north gate
                Wall(wallSegModel, "WallPerimeter-North", new Vector3(x, 0f, wallZ), 0f);
            }
            Big(gateMedModel, "Gate-North-Main", new Vector3(0f, 0f, wallZ), 0f, gateTarget);

            // ── South wall (z = -33) — segments + main gate at x = 0 ─────────
            for (float x = -wallX + segStep * 0.5f; x < wallX; x += segStep)
            {
                if (Mathf.Abs(x) < 3f) continue;   // 6 m opening for the main gate
                Wall(wallSegModel, "WallPerimeter-South", new Vector3(x, 0f, -wallZ), 0f);
            }
            Big(gateMedModel, "Gate-South-Main", new Vector3(0f, 0f, -wallZ), 0f, gateTarget);

            // ── East wall (x = +42) — segments from z = -33 to +33 ───────────
            for (float z = -wallZ + segStep * 0.5f; z < wallZ; z += segStep)
            {
                if (Mathf.Abs(z) < 3f) continue;   // opening for east gate
                Wall(wallSegModel, "WallPerimeter-East", new Vector3(wallX, 0f, z), 90f);
            }
            Big(gateSmModel, "Gate-East-Side", new Vector3(wallX, 0f, 0f), 90f, gateTarget);

            // ── West wall (x = -42) — segments from z = -33 to +33 ───────────
            for (float z = -wallZ + segStep * 0.5f; z < wallZ; z += segStep)
            {
                if (Mathf.Abs(z) < 3f) continue;   // opening for west gate
                Wall(wallSegModel, "WallPerimeter-West", new Vector3(-wallX, 0f, z), 270f);
            }
            Big(gateSmModel, "Gate-West-Side", new Vector3(-wallX, 0f, 0f), 270f, gateTarget);

            // ── Corner towers at (±42, 0, ±33) ───────────────────────────────
            Big(towerCorModel, "Tower-NE-Corner", new Vector3( wallX, 0f,  wallZ), 0f, towerTarget);
            Big(towerCorModel, "Tower-NW-Corner", new Vector3(-wallX, 0f,  wallZ), 0f, towerTarget);
            Big(towerCorModel, "Tower-SE-Corner", new Vector3( wallX, 0f, -wallZ), 0f, towerTarget);
            Big(towerCorModel, "Tower-SW-Corner", new Vector3(-wallX, 0f, -wallZ), 0f, towerTarget);

            // ── Mid-wall towers at each cardinal wall midpoint ────────────────
            Big(towerMidModel, "Tower-North-Mid", new Vector3(  -10f, 0f,  wallZ), 0f,   towerTarget);  // off-centre: flank the north gate
            Big(towerMidModel, "Tower-South-Mid", new Vector3(    0f, 0f, -wallZ), 180f, towerTarget);
            Big(towerMidModel, "Tower-East-Mid",  new Vector3( wallX, 0f,     0f), 90f,  towerTarget);
            Big(towerMidModel, "Tower-West-Mid",  new Vector3(-wallX, 0f,     0f), 270f, towerTarget);

            // Count placed pieces for the build log (reuse _propCount as a proxy).
            int segCount = perimeterRoot.transform.childCount;
            Debug.Log($"[VillageSceneBuilder] WO-101 BuildWallPerimeter: {segCount} polyperfect perimeter " +
                      "pieces placed (wall segments + corner/mid towers + cardinal gates). " +
                      "Polyperfect atlas materials URP-correct; colliders stripped.");
        }

        /// <summary>
        /// Scales a flat ground/water plane so its horizontal footprint is exactly
        /// <paramref name="size"/> m on both axes (tiles abut), leaving Y untouched.
        /// Measured from world renderer bounds (planes carry no rotation here).
        /// </summary>
        private static void FitGroundTile(GameObject go, float size)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0) return;
            Bounds wb = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) wb.Encapsulate(rs[i].bounds);
            var s = go.transform.localScale;
            if (wb.size.x > 0.001f) s.x *= size / wb.size.x;
            if (wb.size.z > 0.001f) s.z *= size / wb.size.z;
            go.transform.localScale = s;
        }

        /// <summary>
        /// WO-104 Stage 2: a 6 m-wide ring of <c>Terrain_Plane_Lake</c> water tiles around
        /// the curtain wall exterior — inner edge flush with the wall (±42/±33), outer edge
        /// 6 m out (±48/±39), set slightly below grade so it reads as a channel. The 6 m gate
        /// spans (south/east/west) are left clear for the drawbridges, which are placed flat
        /// across the moat at each gate. Fills the dark exterior void at the wall base.
        /// </summary>
        private static void BuildMoat(Transform wallRoot)
        {
            // WO-104: Terrain_Plane_Water is the clean water surface; Terrain_Plane_Lake is a
            // GRASS tile with a pond cut into it (tiled 204x it read as a green holey mass).
            const string LakePath       = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Terrains_M/Planes_M/Terrain_Plane_Water.prefab";
            const string DrawbridgePath = PolyMedievalDir + "Drawbridge_Medieval.prefab";

            var lake       = AssetDatabase.LoadAssetAtPath<GameObject>(LakePath);
            var drawbridge = AssetDatabase.LoadAssetAtPath<GameObject>(DrawbridgePath);
            if (lake == null)
            {
                Debug.LogWarning($"[VillageSceneBuilder] WO-104 moat: Terrain_Plane_Lake not found at " +
                                 $"'{LakePath}' — moat skipped (polyperfect re-import needed).");
                return;
            }

            var moatRoot = new GameObject("Moat");
            moatRoot.transform.SetParent(wallRoot, false);
            var mr = moatRoot.transform;

            const float innerX = 42f, innerZ = 33f;   // flush with wall base
            const float outerX = 48f, outerZ = 39f;   // 6 m outside the wall
            const float step    = 3f;
            const float waterY  = -0.4f;               // sit in a channel, below grade
            const float gateHalf = 3f;                 // 6 m drawbridge spans to leave clear

            int placed = 0;

            for (float x = -outerX + step * 0.5f; x < outerX; x += step)
            for (float z = -outerZ + step * 0.5f; z < outerZ; z += step)
            {
                float ax = Mathf.Abs(x), az = Mathf.Abs(z);
                bool insideWall = ax < innerX && az < innerZ;   // strictly inside the curtain
                bool insideOuter = ax <= outerX && az <= outerZ;
                if (insideWall || !insideOuter) continue;        // only the 6 m ring band

                // Leave the gate spans clear for the drawbridges (all 4 gates).
                bool northGate = ax < gateHalf && z >  innerZ - 0.5f;   // north band, x≈0
                bool southGate = ax < gateHalf && z < -innerZ + 0.5f;   // south band, x≈0
                bool eastGate  = az < gateHalf && x >  innerX - 0.5f;   // east band, z≈0
                bool westGate  = az < gateHalf && x < -innerX + 0.5f;   // west band, z≈0
                if (northGate || southGate || eastGate || westGate) continue;

                var t = (GameObject)PrefabUtility.InstantiatePrefab(lake);
                if (t == null) continue;
                t.name = "MoatTile";
                t.transform.SetParent(mr, false);
                t.transform.position = new Vector3(x, waterY, z);
                FitGroundTile(t, step);
                StripColliders(t);
                StripRigidbodies(t);
                placed++;
            }

            // ── Drawbridges: flat across the moat at each gate (WO-158: all 4) ──
            if (drawbridge != null)
            {
                // (label, position just outside the gate, yaw facing outward)
                var spans = new (string name, Vector3 pos, float yaw)[]
                {
                    ("Drawbridge-North", new Vector3(0f, 0f, (innerZ + 3f)), 180f),
                    ("Drawbridge-South", new Vector3(0f, 0f, -(innerZ + 3f)), 0f),
                    ("Drawbridge-East",  new Vector3(innerX + 3f, 0f, 0f),    90f),
                    ("Drawbridge-West",  new Vector3(-(innerX + 3f), 0f, 0f), 270f),
                };
                foreach (var (name, pos, yaw) in spans)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(drawbridge);
                    if (d == null) continue;
                    d.name = name;
                    d.transform.SetParent(mr, false);
                    d.transform.position = pos;
                    d.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    NormalizeProp(d, 7f);          // span the 6 m moat with margin
                    SnapFeetToParent(d);           // sit flush on the ground (was a raised surface)
                    StripColliders(d);             // decorative: hero crosses on the ground through the gate
                    StripRigidbodies(d);           //   (the raised collider was forcing a walk-around)
                }
            }
            else
            {
                Debug.LogWarning($"[VillageSceneBuilder] WO-104 moat: Drawbridge_Medieval not found at " +
                                 $"'{DrawbridgePath}' — gates left open across the moat.");
            }

            Debug.Log($"[VillageSceneBuilder] WO-104 BuildMoat: {placed} water tiles in the " +
                      $"6 m ring (inner +-{innerX}/+-{innerZ}, outer +-{outerX}/+-{outerZ}, y={waterY}); " +
                      $"{(drawbridge != null ? 3 : 0)} drawbridges at the gate spans.");
        }

        /// <summary>
        /// WO-104 §7 + unified-NavMesh rampart (owner 2026-05-30): a WALKABLE wall-walk the hero
        /// AND enemies navigate via the shared NavMesh. A flat stone WALKWAY runs along each wall's
        /// inner top edge (y = wall height), and a gentle stone RAMP (~29°, under the 45° NavMesh
        /// slope limit) climbs from the interior ground up to it, flanking each gate. All pieces are
        /// flagged NavigationStatic so BakeVillageNavMesh connects ground -> ramp -> walkway — making
        /// a hero defending up top reachable: enemies path up the same ramp to attack.
        /// </summary>
        private static void BuildRamparts(Transform wallRoot)
        {
            var root = new GameObject("Ramparts");
            root.transform.SetParent(wallRoot, false);
            var rr = root.transform;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var stone = sh != null ? new Material(sh) { name = "RampartStone" } : null;
            if (stone != null && stone.HasProperty("_BaseColor"))
                stone.SetColor("_BaseColor", new Color(0.52f, 0.50f, 0.46f));

            // Owner 2026-05-30: show the DESIGNED staircase as the visual and hide the nav ramp
            // beneath it, so it reads as climbing real stairs while the NavMesh stays a clean ramp.
            var stairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PolyMedievalDir + "Stairs_Medieval_Stone.prefab");

            const float wallX = 42f, wallZ = 33f;   // poly curtain inner edges
            const float topY  = 5f;                 // wall height -> walkway level
            const float walkW = 3f;                 // walkway depth (inner side)
            const float rampRun = 9f;               // horizontal run (5 m rise -> ~29°, < 45° limit)
            const float rampW = 3f;                 // ramp width
            int pieces = 0;

            // Local: nav-static stone box (CreatePrimitive carries a BoxCollider; harmless — the
            // agents move on the NavMesh, not via physics).
            System.Func<string, Vector3, Vector3, Quaternion, GameObject> Box =
                (name, pos, size, rot) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(rr, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = size;
                if (stone != null) { var rd = go.GetComponent<Renderer>(); if (rd != null) rd.sharedMaterial = stone; }
                GameObjectUtility.SetStaticEditorFlags(go,
                    GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.NavigationStatic);
                pieces++;
                return go;
            };

            // ── Walkways: flat slabs along each wall's INNER top edge (y = topY) ──
            float wkN = wallZ - walkW * 0.5f;
            Box("Walkway-North", new Vector3(0f, topY, wkN),  new Vector3(2f * wallX, 0.4f, walkW), Quaternion.identity);
            Box("Walkway-South", new Vector3(0f, topY, -wkN), new Vector3(2f * wallX, 0.4f, walkW), Quaternion.identity);
            float wkE = wallX - walkW * 0.5f;
            Box("Walkway-East",  new Vector3(wkE,  topY, 0f), new Vector3(walkW, 0.4f, 2f * wallZ), Quaternion.identity);
            Box("Walkway-West",  new Vector3(-wkE, topY, 0f), new Vector3(walkW, 0.4f, 2f * wallZ), Quaternion.identity);

            // ── Parapet (WO-136): a low battlement along each walkway's OUTER edge ──
            // The walkway top sits at topY + 0.2 (0.4-thick slab). The parapet is a
            // nav-static stone box rising ~1.2 m above it on the side facing OUT of
            // the village, so the hero reads as protected and the Box collider stops
            // him walking off the rampart edge (WO-136 acceptance: parapet fall-off).
            const float parH    = 1.4f;                 // parapet height above the walk
            const float parThk  = 0.4f;                 // parapet thickness
            float parTopY = topY + 0.2f + parH * 0.5f;  // centre y (walk-top + half height)
            float parZ    = wallZ - parThk * 0.5f;       // outer edge, inset by half-thickness
            float parX    = wallX - parThk * 0.5f;
            Box("Parapet-North", new Vector3(0f,  parTopY,  parZ), new Vector3(2f * wallX, parH, parThk), Quaternion.identity);
            Box("Parapet-South", new Vector3(0f,  parTopY, -parZ), new Vector3(2f * wallX, parH, parThk), Quaternion.identity);
            Box("Parapet-East",  new Vector3(parX,  parTopY, 0f),  new Vector3(parThk, parH, 2f * wallZ), Quaternion.identity);
            Box("Parapet-West",  new Vector3(-parX, parTopY, 0f),  new Vector3(parThk, parH, 2f * wallZ), Quaternion.identity);

            // ── Wall barrier collision (WO-136): collide on the REAL visible wall ──
            // BUG (owner): the perimeter wall mesh (BuildWallPerimeter, ±42/±33) has its
            // colliders STRIPPED (line ~2925) — gameplay collision lived on the hidden
            // inner KayKit ring (BuildWallRing, ±28/±21), so hero/enemies collided with an
            // invisible wall offset from the one they see. FIX: a full-height barrier box on
            // the visible wall line, Y=0 → wall-top (topY=5), broken at the SAME gate gaps the
            // wall mesh leaves so enemy lanes stay open. Nav-static so the bake routes around it.
            //   Gate gaps (from BuildWallPerimeter): South/East/West each skip |coord|<3 (6 m
            //   opening); North has NO gate (unbroken). Mirror that exactly here.
            const float barThk = 1.2f;                 // match the visible wall thickness (wallThick)
            const float barH   = topY;                 // Y=0 → wall-top (5 m)
            float barY  = barH * 0.5f;                 // box centre
            float barZ  = wallZ - barThk * 0.5f;       // sit on the visible wall line, inset half-thickness
            float barX  = wallX - barThk * 0.5f;
            const float gateHalf = 3f;                 // 6 m gate opening half-width
            float runHalf = wallX - gateHalf;          // half-length of one side of a gated (S) wall span
            float sideHalf = (wallX - gateHalf) * 0.5f;// centre offset of each half-span (gated walls)
            // North wall — gate at x=0: two spans flanking the 6 m opening.
            Box("WallBarrier-North-W", new Vector3(-(gateHalf + sideHalf), barY, barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            Box("WallBarrier-North-E", new Vector3( (gateHalf + sideHalf), barY, barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            // South wall — main gate at x=0: two spans flanking the 6 m gap.
            Box("WallBarrier-South-W", new Vector3(-(gateHalf + sideHalf), barY, -barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            Box("WallBarrier-South-E", new Vector3( (gateHalf + sideHalf), barY, -barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            // East wall — side gate at z=0: two spans flanking the gap.
            float sideHalfZ = (wallZ - gateHalf) * 0.5f;
            Box("WallBarrier-East-S", new Vector3(barX, barY, -(gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            Box("WallBarrier-East-N", new Vector3(barX, barY,  (gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            // West wall — side gate at z=0: two spans flanking the gap.
            Box("WallBarrier-West-S", new Vector3(-barX, barY, -(gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            Box("WallBarrier-West-N", new Vector3(-barX, barY,  (gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            _ = runHalf;  // (kept for clarity; spans computed via sideHalf/sideHalfZ)

            // ── Ramps: gentle stone inclines from interior ground up to the walkway edge ──
            // Defined by bottom (interior, y=0) + top (walkway edge, y=topY); LookRotation aligns
            // the slab's length to the slope so its top face is the walkable surface.
            System.Action<string, Vector3, Vector3> Ramp = (name, bottom, top) =>
            {
                Vector3 mid = (bottom + top) * 0.5f;
                Vector3 fwd = (top - bottom).normalized;
                float len = Vector3.Distance(bottom, top);
                // Two objects in parallel: an INVISIBLE nav plank (the walkable surface the agents
                // climb) + the DESIGNED staircase as the visual on top. Decouples look from navigate.
                var rampGo = Box(name, mid, new Vector3(rampW, 0.4f, len), Quaternion.LookRotation(fwd, Vector3.up));
                var rd = rampGo.GetComponent<Renderer>();
                if (rd != null) rd.enabled = false;   // hidden — the staircase below is the visual
                if (stairPrefab != null)
                {
                    var st = (GameObject)PrefabUtility.InstantiatePrefab(stairPrefab);
                    if (st != null)
                    {
                        st.name = name + "-Visual";
                        st.transform.SetParent(rr, false);
                        st.transform.position = bottom;
                        Vector3 horiz = new Vector3(fwd.x, 0f, fwd.z).normalized;
                        if (horiz.sqrMagnitude > 0.0001f)
                            st.transform.rotation = Quaternion.LookRotation(horiz, Vector3.up);
                        NormalizeProp(st, 4f);   // WO-136: 7f read as oversized "big steps" — 4f sits proportional to the 5m wall
                        StripColliders(st);
                        StripRigidbodies(st);
                    }
                }
            };
            // WO-166 #4: ramps run PARALLEL to (hugging) their wall, not perpendicular
            // into the courtyard. Both ends sit on the walkway-edge line (z=±zEdge for
            // N/S, x=±xEdge for E/W); the 9 m climb run goes ALONG the wall axis, so the
            // stairs read as climbing the wall face instead of jutting 9 m mid-courtyard
            // (owner: "the stps in middle"). Run spans x∈[-15,-6] (N/S) / z∈[-15,-6] (E/W),
            // clearing the centred gate gap (|coord|<3); the slab's 3 m width sits just
            // inside the wall. NavMesh link ground→walkway is preserved (ends unchanged in Y).
            float zEdge = wallZ - walkW;   // walkway inner edge (=30): ramp top meets it
            Ramp("Ramp-South", new Vector3(-6f - rampRun, 0f, -zEdge), new Vector3(-6f, topY, -zEdge));
            Ramp("Ramp-North", new Vector3(-6f - rampRun, 0f,  zEdge), new Vector3(-6f, topY,  zEdge));
            float xEdge = wallX - walkW;   // =39
            Ramp("Ramp-East",  new Vector3( xEdge, 0f, -6f - rampRun), new Vector3( xEdge, topY, -6f));
            Ramp("Ramp-West",  new Vector3(-xEdge, 0f, -6f - rampRun), new Vector3(-xEdge, topY, -6f));

            Debug.Log($"[VillageSceneBuilder] WO-104 BuildRamparts: {pieces} nav-static stone pieces " +
                      "(4 wall-walks + 4 climb ramps); hero + enemies share the NavMesh up to the rampart.");
        }

        /// <summary>
        /// Adds a BoxCollider sized to the building's mesh bounds to the plot
        /// root GameObject. HeroLocomotion's CapsuleCast sweeps against these
        /// each frame so the hero can no longer walk through structures
        /// (owner 2026-05-20). The visual mesh still has its own colliders
        /// stripped via InstantiateModel so there's a single source of truth
        /// for collision.
        /// </summary>
        private static void AddBuildingFootprintCollider(GameObject root, GameObject visual)
        {
            if (root == null || visual == null) return;
            // Don't duplicate.
            if (root.GetComponent<BoxCollider>() != null) return;

            Bounds bounds = ComputeMeshBounds(visual);
            if (bounds.size == Vector3.zero) return;
            // Convert world-space mesh bounds back to root-local — the visual
            // sits inside root with its own scale/rotation, so we ask Unity to
            // express the bounds in root's local frame.
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(bounds.center);
            // size of the visual in world units, then divide by root.lossyScale
            // so the collider tracks the world bounds even if root is scaled.
            Vector3 sz = bounds.size;
            Vector3 ls = root.transform.lossyScale;
            col.size = new Vector3(
                Mathf.Max(1.2f, (ls.x != 0f ? sz.x / ls.x : sz.x) * 0.8f),
                ls.y != 0f ? sz.y / ls.y : sz.y,
                Mathf.Max(1.2f, (ls.z != 0f ? sz.z / ls.z : sz.z) * 0.8f));
        }


        // #####################################################################
        // ##  WEEK 4 — village gameplay-system integration                  ##
        // ##  ------------------------------------------------------------   ##
        // ##  Wires every item from the three week4-*.md integration         ##
        // ##  checklists into the Village scene:                             ##
        // ##    - WaveManager wired to the Heart + spawn points              ##
        // ##    - HeroAbilities on a hero rig near the Heart                 ##
        // ##    - PetDeployer (auto-deploys the three starter pets)          ##
        // ##    - BuildMenu UIDocument with the 5 building prefabs           ##
        // ##    - the KayKit Skeleton enemy prefab (Enemy + EnemyDamageable) ##
        // ##    - the ForceFieldGate material wired onto every Gate          ##
        // ##  Every gameplay TYPE is touched by full-name reflection — the   ##
        // ##  DeNelle.Editor asmdef cannot reference DeNelle.Village/.Pets.  ##
        // #####################################################################

        /// <summary>
        /// Builds + wires the Week-4 village gameplay systems. Idempotent — the
        /// generated GameObjects all live under <c>VillageRoot</c> (cleared at
        /// the top of <see cref="BuildVillage"/>); the prefab + material assets
        /// are overwritten in place on a re-run.
        /// </summary>
        /// <param name="root">The VillageRoot transform.</param>
        /// <param name="gateRoot">The Gates sub-root — every Gate gets the force-field material.</param>
        /// <param name="heart">The HeartController component (may be null if the type was missing).</param>
        private static void BuildGameplaySystems(GameObject root, Transform gateRoot, Component heart)
        {
            EnsureFolder(GeneratedPrefabDir);

            var systemsRoot = NewChild(root.transform, "GameplaySystems");

            // 1) Assets — the force-field material, the enemy prefab, the five
            //    building prefabs. Built first so the scene components can wire
            //    against them.
            Material forceFieldMat = EnsureForceFieldMaterial();
            GameObject enemyPrefab = EnsureEnemyPrefab();
            var buildingPrefabs = EnsureBuildingPrefabs();

            // 2) Wire the force-field material onto every Gate's renderer.
            WireGateForceFields(gateRoot, forceFieldMat);

            // 3) The Heart's world position — the centre of the pet ring + the
            //    hero's spawn anchor. Falls back to origin when the Heart type
            //    was not found.
            Vector3 heartPos = heart != null ? heart.transform.position : Vector3.zero;

            // 4) WaveManager — its own sub-system GameObject.
            BuildWaveManager(systemsRoot, heart, enemyPrefab);

            // 5) HeroAbilities — on a hero rig stood near the Heart.
            GameObject hero = BuildHero(systemsRoot, heart, heartPos);

            // 5b) Wire the over-shoulder camera onto the hero now that it exists.
            //     CreateCamera() attached the follow component without a target
            //     because the hero hadn't been built yet.
            WireVillageCameraTarget(hero);

            // 6) PetDeployer — auto-deploys the three starter pets on Start().
            BuildPetDeployer(systemsRoot, heartPos);

            // 7) BuildMenu — a UIDocument GameObject with the build-menu UI.
            BuildBuildMenu(systemsRoot, buildingPrefabs);

            // 7b) Marketplace + PackStore — DISABLED for now. Placing it worked, but
            //     opening it rendered the WRONG panel (the hero talent tree) because
            //     PackStore's UIDocument grabbed the SHARED PanelSettings, and the UXML
            //     came up blank in the build. Re-enable after PackStore gets its OWN
            //     PanelSettings + a code-built UI (not UXML-template-driven).
            // BuildMarketplace(systemsRoot);

            // 8) Ambient townsfolk — wandering / idle KayKit villagers with
            //    engage-on-approach word bubbles. They watch the hero rig built
            //    in step 5 for the proximity check (Workstream D).
            int townsfolk = BuildTownsfolk(root.transform, heartPos, hero);

            Debug.Log("[VillageSceneBuilder] Week-4 gameplay systems wired -- " +
                      "WaveManager + HeroAbilities + PetDeployer + BuildMenu, " +
                      $"{townsfolk} ambient townsfolk, " +
                      $"enemy prefab {(enemyPrefab != null ? "OK" : "MISSING")}, " +
                      $"force-field material {(forceFieldMat != null ? "OK" : "MISSING")}.");
        }

        // =====================================================================
        //  Force-field gate material
        // =====================================================================

        /// <summary>
        /// Creates (or refreshes) the <c>ForceFieldGate.mat</c> material asset
        /// from <c>Assets/Shaders/ForceFieldGate.shader</c> and returns it. The
        /// material carries no per-instance overrides — <c>Gate.cs</c> drives the
        /// <c>_Collapse</c> property at runtime via a MaterialPropertyBlock.
        /// </summary>
        private static Material EnsureForceFieldMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ForceFieldShaderPath);
            if (shader == null)
            {
                Debug.LogError("[VillageSceneBuilder] ForceFieldGate.shader not found at " +
                               $"'{ForceFieldShaderPath}' -- gate force-field material skipped.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(ForceFieldMaterialPath);
            if (existing != null)
            {
                // Keep the asset; just make sure it still runs the right shader.
                if (existing.shader != shader) existing.shader = shader;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var mat = new Material(shader) { name = "ForceFieldGate" };
            AssetDatabase.CreateAsset(mat, ForceFieldMaterialPath);
            return mat;
        }

        /// <summary>
        /// Assigns the force-field material to each Gate's force-field renderer
        /// and wires that renderer into <c>Gate._forceFieldRenderer</c>. The
        /// Week-3 builder placed a <c>ForceFieldShimmer</c> cube child per gate —
        /// that cube's MeshRenderer becomes the shader-driven sheet.
        /// </summary>
        private static void WireGateForceFields(Transform gateRoot, Material forceFieldMat)
        {
            if (gateRoot == null) return;
            int wired = 0;
            var gateType = FindType(TypeGate);

            foreach (Transform gateGo in gateRoot)
            {
                // The Week-3 builder names the violet sheet "ForceFieldShimmer".
                Transform shimmer = gateGo.Find("ForceFieldShimmer");
                Renderer fieldRenderer = shimmer != null
                    ? shimmer.GetComponent<Renderer>()
                    : gateGo.GetComponentInChildren<Renderer>();

                if (fieldRenderer != null && forceFieldMat != null)
                    fieldRenderer.sharedMaterial = forceFieldMat;

                // Wire Gate._forceFieldRenderer so Gate.cs can drive _Collapse.
                if (gateType != null)
                {
                    var gateComp = gateGo.GetComponent(gateType);
                    if (gateComp != null && fieldRenderer != null)
                    {
                        var so = new SerializedObject(gateComp);
                        SetObjectField(so, "_forceFieldRenderer", fieldRenderer);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                wired++;
            }
            Debug.Log($"[VillageSceneBuilder] Force-field material wired onto {wired} gate(s).");
        }

        // =====================================================================
        //  Enemy prefab — KayKit Skeleton_Minion + Enemy + EnemyDamageable
        // =====================================================================

        /// <summary>
        /// Builds (or refreshes) the wave-enemy prefab — the KayKit Hollow Walker
        /// skeleton with an <c>Enemy</c> (RequireComponent pulls in a
        /// <c>NavMeshAgent</c>) + an <c>EnemyDamageable</c> adapter + a capsule
        /// collider, all on the <see cref="EnemyLayer"/>. Saved as a prefab asset
        /// so <c>WaveManager._enemyPrefab</c> can reference it. Returns the prefab
        /// asset GameObject, or null when a required type / mesh is missing.
        /// </summary>

        // =====================================================================
        //  Hero rig + HeroAbilities
        // =====================================================================

        /// <summary>
        /// Builds a simple hero rig (Blaise) near the Heart, gives it a
        /// <c>HeroAbilities</c> component, wires the Heart ref (so Healing
        /// Beacon can heal) and sets the enemy LayerMask (week4-hero-pets-gate.md
        /// item 3 + item 2). A capsule visual stands in until the KayKit hero
        /// mesh is imported. Returns the hero GameObject so the townsfolk
        /// system can watch its transform for proximity dialogue.
        /// </summary>

    }
}
