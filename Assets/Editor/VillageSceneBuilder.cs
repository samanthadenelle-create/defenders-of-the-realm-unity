// =============================================================================
// VillageSceneBuilder — Elarion village INTERIOR scene generator (Editor-only).
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
    /// Editor utility that assembles the Elarion village interior scene from the
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
        /// lived-in Elarion palette.
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
        private const string TypeRampartNavLinkInstaller = NsVillage + ".RampartNavLinkInstaller";
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

        // ── WO-133: first-run tutorial (FTUE) ────────────────────────────────
        /// <summary>OnboardingFlow lives in DeNelle.Onboarding (resolved by reflection).</summary>
        private const string TypeOnboardingFlow = "DeNelle.Onboarding.OnboardingFlow";
        /// <summary>The coach-mark overlay UXML (editor reference; code-built fallback renders in builds).</summary>
        private const string TutorialOverlayUxmlPath =
            "Assets/_Modules/Onboarding/UI/TutorialOverlay.uxml";

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
        /// Builds the Elarion village interior scene per
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
                    go.name == "OuterWorldRoot" ||
                    // WO-173 Option A: the exterior terrain lives in OuterWorld now. Strip
                    // any stale terrain baked into Village (the old broken-material copy) so
                    // it can't render as an invisible void layer or z-fight OuterWorld's terrain.
                    go.name == "ExteriorRoot" || go.name == "ExteriorTerrain")
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

            // ── City floor — flat walkable slab a hair ABOVE the terrain ────
            // WO-192: the old ~3,000-hex-tile floor was dropped (it z-fought the
            // OuterWorld terrain and was a draw-call hog). But removing it left the
            // interior with NO continuous flat footing — the hero could only walk the
            // raised road tiles. RE-ADD a single flat slab spanning the whole city
            // footprint at y=+0.02 (a hair above the coplanar Y=0 terrain so it wins
            // the depth test with no flicker — no material-priority logic needed). It
            // gets a grass/dirt URP material (kills the bare purple-grey terrain look),
            // a collider, and is marked NavigationStatic so the Village bake covers the
            // entire interior → continuous flat navmesh, "walk anywhere off the road."
            BuildCityFloor(groundRoot);

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

            // ── WO-189b: data-driven city population from CityManifest.json ──
            // Repopulates the full Elarion roster (~29 buildings, ~100 props,
            // 6 wardens, 4 bridges) under root/CityManifestRoot. Runs after the
            // hand-placed buildings (it skips the 5 duplicates) and BEFORE the
            // NavMesh bake so the new structures voxelize into navigation.
            BuildCityFromManifest(root.transform);

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
        //  City floor — single flat walkable slab (WO-192)
        // =====================================================================

        /// <summary>
        /// WO-192: a single flat walkable slab spanning the whole city footprint,
        /// sitting at y = +0.02 (a hair ABOVE the coplanar Y=0 OuterWorld terrain so
        /// it wins the depth test cleanly — no z-fight, no render-queue tricks). Carries
        /// a grass/dirt URP material (so the interior reads as ground, not bare terrain),
        /// a flat BoxCollider, and the NavigationStatic flag so BakeVillageNavMesh covers
        /// the entire interior. This is the continuous flat footing that lets the hero +
        /// enemy NavMeshAgents walk anywhere inside the walls (off the roads) and out
        /// through every gate onto the terrain.
        /// </summary>
        private static void BuildCityFloor(Transform parent)
        {
            // Footprint: reach out to the moat band (±54/±47 from BuildMoat) plus a
            // small margin so the gate exits + bridges land on the slab, not off its
            // edge. A single quad-thin box keeps it to ONE draw call (vs ~3,000 tiles).
            const float halfX = 58f;
            const float halfZ = 52f;
            const float floorY = 0.02f;   // a hair above the Y=0 terrain — kills the z-fight
            const float thk    = 0.1f;    // thin slab; collider top lands at floorY

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CityFloor";
            floor.transform.SetParent(parent, false);
            // Slab top surface sits at floorY: centre = floorY - thk/2.
            floor.transform.localPosition = new Vector3(0f, floorY - thk * 0.5f, 0f);
            floor.transform.localScale = new Vector3(halfX * 2f, thk, halfZ * 2f);

            // Grass/dirt URP material — flat, lit, no z-fight (renderer present but
            // it's the topmost surface at y=+0.02 so it draws over the terrain).
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null)
            {
                var mat = new Material(sh) { name = "CityGroundGrass" };
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.34f, 0.44f, 0.26f)); // mossy grass-dirt
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);
                var rd = floor.GetComponent<Renderer>();
                if (rd != null) rd.sharedMaterial = mat;
            }

            // The CreatePrimitive box already carries a BoxCollider (kept — gives the
            // hero solid footing). Flag NavigationStatic so the bake voxelizes the whole
            // interior as walkable floor.
            GameObjectUtility.SetStaticEditorFlags(floor,
                GameObjectUtility.GetStaticEditorFlags(floor) | StaticEditorFlags.NavigationStatic);

            _groundCount++;
            Debug.Log($"[VillageSceneBuilder] WO-192 BuildCityFloor: one flat grass slab " +
                      $"{halfX * 2f:0}x{halfZ * 2f:0}u at y={floorY} (above the Y=0 terrain, no z-fight), " +
                      "collider + NavigationStatic -> continuous interior navmesh.");
        }

        // =====================================================================
        //  Curtain wall — driven by WallLayout.Segments (shaped rectangle)
        // =====================================================================


        /// <summary>
        /// WO-104 Stage 2: a 6 m-wide ring of <c>Terrain_Plane_Lake</c> water tiles around
        /// the curtain wall exterior — inner edge flush with the wall (±42/±33), outer edge
        /// 6 m out (±48/±39), set slightly below grade so it reads as a channel. The 6 m gate
        /// spans (south/east/west) are left clear for the drawbridges, which are placed flat
        /// across the moat at each gate. Fills the dark exterior void at the wall base.
        /// </summary>

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
