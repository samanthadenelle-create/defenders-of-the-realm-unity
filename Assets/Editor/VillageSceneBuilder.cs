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
    public static class VillageSceneBuilder
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
        private const string TypeVillageCamera = NsVillage + ".VillageCamera";
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
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == VillageRootName || go.name == "Main Camera" ||
                    go.name == "Directional Light")
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

            // ── Plaza + road network (§7) ────────────────────────────────────
            BuildPlaza(roadRoot);
            BuildRoads(roadRoot, tWallLayout);

            // ── Centerpieces — Elarion + the Keeper's Keep (§3) ──────────────
            var heart = BuildElarion(centerpieceRoot);
            BuildKeep(centerpieceRoot);

            // ── Five gameplay buildings (§5) ─────────────────────────────────
            int buildingCount = BuildBuildings(buildingRoot, controller);

            // ── City dressing (§6) ───────────────────────────────────────────
            BuildCityDressing(dressingRoot);

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
            // Drop a dungeon portal so the player can actually enter the
            // Healer's Cottage from the village (2026-05-20 PO observation:
            // dungeon scene exists but is unreachable in-game).
            SpawnDungeonPortal();
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
        private static void BuildPlaza(Transform parent)
        {
            var plazaRoot = NewChild(parent, "Plaza");
            var stone = LoadModel(HexTilesRoads + "hex_road_B.fbx");

            // ~6 hex wide × 5 hex deep block of paving centred on the origin.
            for (int row = -4; row <= 4; row++)
            {
                bool oddRow = (row & 1) != 0;
                float z = row * HexDepth;
                float xShift = oddRow ? HexWidth * 0.5f : 0f;
                for (int col = -5; col <= 5; col++)
                {
                    float x = col * HexWidth + xShift;
                    var tile = InstantiateModel(stone, "hex_road_B.fbx",
                        $"PlazaTile ({col},{row})");
                    tile.transform.SetParent(plazaRoot, false);
                    tile.transform.localPosition = new Vector3(x, 0.02f, z);
                    if (stone == null)
                    {
                        tile.transform.localScale = new Vector3(HexWidth, 0.16f, HexWidth);
                        ApplyColor(tile, new Color(0.62f, 0.60f, 0.56f));
                    }
                    _roadCount++;
                }
            }
            if (stone == null)
                Debug.LogWarning("[VillageSceneBuilder] hex_road_B.fbx missing -- plaza used placeholder paving.");
        }

        /// <summary>
        /// Lays the N-S spine and E-W cross — two 2-hex-wide paved roads forming
        /// a '+' from the plaza out to the four gates (§7.1). KayKit road tiles
        /// overlay the grass floor.
        /// </summary>
        private static void BuildRoads(Transform parent, Type tWallLayout)
        {
            var road = LoadModel(HexTilesRoads + "hex_road_A.fbx");
            var roadsRoot = NewChild(parent, "MainRoads");
            if (road == null)
                Debug.LogWarning("[VillageSceneBuilder] hex_road_A.fbx missing -- main roads used placeholders.");

            // N-S spine: two columns of tiles either side of X=0, the plaza
            // edge out to the N and S wall line.
            for (float z = HexDepth * 4f; z <= WallHalfZ - 1f; z += HexDepth)
            {
                LayRoadPair(roadsRoot, road, true, z);    // north arm
            }
            for (float z = -HexDepth * 4f; z >= -(WallHalfZ - 1f); z -= HexDepth)
            {
                LayRoadPair(roadsRoot, road, true, z);    // south arm
            }
            // E-W cross: two rows either side of Z=0.
            for (float x = HexWidth * 5f; x <= WallHalfX - 1f; x += HexWidth)
            {
                LayRoadPair(roadsRoot, road, false, x);   // east arm
            }
            for (float x = -HexWidth * 5f; x >= -(WallHalfX - 1f); x -= HexWidth)
            {
                LayRoadPair(roadsRoot, road, false, x);   // west arm
            }
        }

        /// <summary>Lays a 2-tile-wide road cross-section at one step along an arm.</summary>
        private static void LayRoadPair(Transform parent, GameObject road, bool northSouth, float along)
        {
            float[] lateral = { -HexWidth, 0f, HexWidth };
            foreach (var off in lateral)
            {
                var tile = InstantiateModel(road, "hex_road_A.fbx",
                    northSouth ? $"Road-NS ({along:0.0})" : $"Road-EW ({along:0.0})");
                tile.transform.SetParent(parent, false);
                Vector3 p = northSouth
                    ? new Vector3(off, 0.015f, along)
                    : new Vector3(along, 0.015f, off);
                tile.transform.localPosition = p;
                if (road == null)
                {
                    tile.transform.localScale = new Vector3(HexWidth, 0.14f, HexWidth);
                    ApplyColor(tile, new Color(0.55f, 0.46f, 0.34f));
                }
                _roadCount++;
            }
        }

        // =====================================================================
        //  Centerpiece 1 — Elarion the world-tree (§3.1)
        // =====================================================================

        /// <summary>
        /// Elarion — the sentient world-tree. Sits on a raised mound centre-west
        /// of the plaza, scaled ~3× normal, with violet emissive crystalline
        /// veins and a 6-stone ring (§14 Q3 default). NO building on its hex.
        /// </summary>
        private static Component BuildElarion(Transform parent)
        {
            // Owner direction 2026-05-20: replace the rock-cluster + tree
            // centerpiece with the Tripo fantasy cathedral at village centre,
            // scaled up to read as the village spire. The cathedral arrived
            // as Assets/Models/Cathedral/Cathedral.fbx (Tripo embedded-mat
            // FBX — TripoMaterialFixer rebuilds the URP material on Awake).
            Vector3 site = new Vector3(0f, 0f, 1f);

            var go = new GameObject("Heart (Elarion Tree of Life)");
            go.transform.SetParent(parent, false);
            go.transform.position = site;

            // ── Cathedral spire (Tripo FBX) ─────────────────────────────────
            // Owner 2026-05-20: skip the ElarionMound cylinder when the
            // cathedral loads — the green disk peeked out from under the
            // spire as an ugly leftover.
            GameObject cathedralInstance = null;
            // Owner direction 2026-05-25: REVERSE the 2026-05-20 cathedral-spire
            // call. The Cathedral "spire" renders as a rainbow-patchwork Tripo
            // mesh and is an 84MB memory hog — remove it and restore the Elarion
            // world-tree ("Tree of Life") centerpiece (raised mound + large tree
            // + violet crystal veins + 6 standing stones) built in the blocks
            // below, which all run when cathedralModel is null.
            GameObject cathedralModel = null;
            if (cathedralModel == null)
            {
                var mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mound.name = "ElarionMound";
                mound.transform.SetParent(go.transform, false);
                mound.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                mound.transform.localScale = new Vector3(5.0f, 0.35f, 5.0f);
                ApplyColor(mound, new Color(0.34f, 0.42f, 0.24f));
            }
            if (cathedralModel != null)
            {
                var cathedral = (GameObject)PrefabUtility.InstantiatePrefab(cathedralModel);
                cathedral.name = "CathedralSpire";
                cathedral.transform.SetParent(go.transform, false);
                cathedral.transform.localPosition = Vector3.zero;
                // Owner 2026-05-21: bumped from 8.05m → 16m so the new
                // dragon-tower Cathedral FBX (88MB, Tripo) reads at city-edge
                // distance — the dragon detail and stained-glass rose window
                // both want a taller silhouette to resolve as architecture
                // instead of as stone-coloured noise.
                NormalizeProp(cathedral, 16f);
                StripColliders(cathedral);
                // Owner 2026-05-20 "spire sits off the ground a little":
                // after NormalizeProp, the post-scale mesh bottom isn't
                // necessarily at y=0 (Tripo FBX pivots vary). Measure the
                // world-space bounds and lower the spire so feet land flush
                // on the village floor.
                SnapFeetToParent(cathedral);
                cathedralInstance = cathedral;
                // Attach the runtime URP-material fixer with a stone-grey tint
                // fallback so the cathedral reads as stone even if Tripo's
                // embedded basecolor extract didn't survive the build (owner
                // 2026-05-20 "still not colored"). The texture is preferred
                // when present; tint is the safety net.
                var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fixerType != null)
                {
                    var fixer = cathedral.AddComponent(fixerType);
                    var setTex = fixerType.GetMethod("SetFallbackTexture");
                    // Real basecolor from Tripo Send-To-Unity extract ships at
                    // Resources/Textures/Cathedral.png (~26 MB).
                    setTex?.Invoke(fixer, new object[] { "Textures/Cathedral" });
                    var setTint = fixerType.GetMethod("SetFallbackTint");
                    // Stone-grey with a faint warm bias — reads as cathedral
                    // limestone if the texture doesn't load. NOT white anymore
                    // (white = invisible against the sky / hard to read).
                    setTint?.Invoke(fixer, new object[] { new Color(0.74f, 0.72f, 0.68f) });
                }
                Debug.Log("[VillageSceneBuilder] Cathedral spire mounted at heart, scaled to ~8m, feet snapped to ground.");
            }
            else
            {
                Debug.LogWarning("[VillageSceneBuilder] Cathedral.fbx missing — keeping placeholder " +
                                 "tree mesh as the centerpiece.");
            }

            // ── The tree / spire ─────────────────────────────────────────────
            // Owner 2026-05-20: cathedral mesh replaces the tree. Skip the
            // tree block entirely when cathedralModel loaded successfully.
            if (cathedralModel == null)
            {
                var treeModel = LoadModel(HexDecoNature + "trees_A_large.fbx");
                GameObject tree;
                if (treeModel != null)
                {
                    tree = InstantiateModel(treeModel, "trees_A_large.fbx", "ElarionTree");
                    tree.name = "ElarionTree";
                    tree.transform.SetParent(go.transform, false);
                    tree.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                    tree.transform.localScale = Vector3.one * 3.0f;
                    ApplyColorAll(tree, new Color(0.24f, 0.42f, 0.22f));
                }
                else
                {
                    tree = new GameObject("[PLACEHOLDER] Elarion world-tree");
                    NotePlaceholder("Elarion world-tree");
                    tree.transform.SetParent(go.transform, false);
                    tree.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                    PrimitiveChild(tree.transform, "Trunk", PrimitiveType.Cylinder,
                        new Vector3(0f, 4f, 0f), new Vector3(1.4f, 4f, 1.4f),
                        new Color(0.30f, 0.21f, 0.13f));
                    PrimitiveChild(tree.transform, "Canopy", PrimitiveType.Sphere,
                        new Vector3(0f, 9f, 0f), new Vector3(7f, 6f, 7f),
                        new Color(0.24f, 0.42f, 0.22f));
                }
            }

            // Skip the violet veins + standing-stone ring when the cathedral
            // is the centerpiece (owner 2026-05-20 — the rock cluster + glow
            // shards read as clutter at the new village centre).
            if (cathedralModel != null) goto SkipLegacyDressing;

            // ── Violet emissive crystalline veins up the trunk (§3.1) ────────
            // #9d6fff, soft glow. A few thin emissive shards climbing the trunk.
            Color veinColor = HexColor("9d6fff");
            var veinsRoot = NewChild(go.transform, "CrystalVeins");
            for (int i = 0; i < 5; i++)
            {
                float t = i / 5f;
                float ang = t * 360f + 28f;
                var vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vein.name = $"Vein-{i}";
                vein.transform.SetParent(veinsRoot, false);
                UnityEngine.Object.DestroyImmediate(vein.GetComponent<Collider>());
                float r = 0.55f;
                vein.transform.localPosition = new Vector3(
                    Mathf.Cos(ang * Mathf.Deg2Rad) * r,
                    1.6f + t * 2.4f,
                    Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                vein.transform.localScale = new Vector3(0.16f, 2.2f, 0.16f);
                vein.transform.localRotation = Quaternion.Euler(8f, ang, 6f);
                ApplyEmissive(vein, veinColor, 0.6f); // intensity 0.6 per §3.1
            }

            // ── 6-stone ring of standing stones (§3.1 / §14 Q3 default 6) ────
            // No prop_stone_pillar in this pack -- rock_single_C is the upright
            // boulder that best reads as a standing stone.
            var stoneModel = LoadModel(HexDecoNature + "rock_single_C.fbx");
            var ringRoot = NewChild(go.transform, "StandingStones");
            const int stoneCount = 6;
            const float ringRadius = 4.4f;
            for (int i = 0; i < stoneCount; i++)
            {
                float ang = (360f / stoneCount) * i;
                Vector3 sp = new Vector3(
                    Mathf.Cos(ang * Mathf.Deg2Rad) * ringRadius,
                    0.2f,
                    Mathf.Sin(ang * Mathf.Deg2Rad) * ringRadius);
                var stone = InstantiateModel(stoneModel, "rock_single_C.fbx",
                    $"StandingStone-{i}");
                stone.name = $"StandingStone-{i}";
                stone.transform.SetParent(ringRoot, false);
                stone.transform.localPosition = sp;
                stone.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
                if (stoneModel != null)
                {
                    stone.transform.localScale = new Vector3(1.1f, 2.0f, 1.1f);
                    // The KayKit hex rock atlas does not resolve onto these
                    // meshes (they render white even when the shared material
                    // is force-assigned). Tint the ring a pale violet-grey so it
                    // reads as standing stone and ties to Elarion's crystalline
                    // veins (spec §3.1) — see unity-decisions.md 2026-05-19.
                    ApplyColorAll(stone, new Color(0.62f, 0.60f, 0.66f));
                }
                else
                {
                    stone.transform.localScale = new Vector3(0.7f, 2.2f, 0.7f);
                    stone.transform.localPosition += Vector3.up * 1.1f;
                    ApplyColor(stone, new Color(0.5f, 0.49f, 0.46f));
                }
                _propCount++;
            }

            SkipLegacyDressing:
            // Capture the authored transform before adding HeartController.
            // Awake() will snap to origin + scale unless _useAuthoredTransform=true.
            // We persist BOTH the toggle AND a fallback: directly re-apply the
            // authored values right after the component is added so even if the
            // SerializedObject write doesn't survive scene save, runtime gets
            // the right transform written into the scene asset. Fixes the
            // standing-stones "floating in air" symptom (2026-05-19).
            Vector3 authoredPos   = go.transform.position;
            Vector3 authoredScale = go.transform.localScale;

            var heartComp = AddVillageComponent(go, TypeHeartController);
            if (heartComp != null)
            {
                var so = new SerializedObject(heartComp);
                var prop = so.FindProperty("_useAuthoredTransform");
                if (prop != null) { prop.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
                else Debug.LogWarning("[VillageSceneBuilder] HeartController._useAuthoredTransform not found -- Elarion may snap to origin at Play.");
            }

            // Re-stamp the authored transform AFTER the controller is wired.
            go.transform.position   = authoredPos;
            go.transform.localScale = authoredScale;

            // Owner 2026-05-20: random cinematic dragon fly-bys across Elarion.
            // Foreshadows the apex wave-boss without interrupting gameplay.
            // Lives in DeNelle.Village (Cinematics) so the editor asmdef stays
            // free of a runtime dependency — resolved by reflection here, same
            // pattern as DragonBoss is wired through.
            var flybyType = FindType("DeNelle.Village.Cinematics.DragonCinematicFlyby");
            if (flybyType != null)
            {
                var flybyGo = new GameObject("DragonCinematicFlyby");
                flybyGo.transform.SetParent(go.transform, false);
                flybyGo.transform.localPosition = Vector3.zero; // village centre
                flybyGo.AddComponent(flybyType);
                Debug.Log("[VillageSceneBuilder] DragonCinematicFlyby attached at village centre " +
                          "— random cinematic dragon fly-bys scheduled.");
            }
            else
            {
                Debug.LogWarning("[VillageSceneBuilder] DeNelle.Village.Cinematics.DragonCinematicFlyby " +
                                 "not found — is the DeNelle.Village assembly compiled? Cameo fly-bys will not run.");
            }

            return heartComp;
        }

        // =====================================================================
        //  Centerpiece 2 — the Keeper's Keep (§3.2)
        // =====================================================================

        /// <summary>
        /// The Keeper's Keep — building_castle, placed adjacent to Elarion and
        /// slightly south-east so the two anchors frame the plaza (§3.2). Modest
        /// 2×2-hex footprint (§14 Q1 default). A violet banner flanks it (§3.2).
        /// </summary>
        private static void BuildKeep(Transform parent)
        {
            // Owner direction 2026-05-20 ("THESE TWO THINGS NEED REMOVED"):
            // the Keep building_castle + violet Avalon Banner both read as
            // a flat-untextured block + a tall violet pole next to the
            // cathedral spire — clutter, not centerpiece. Both removed.
            // The plaza centre is now the cathedral alone.
        }

        // =====================================================================
        //  Five gameplay buildings (§5)
        // =====================================================================

        private struct BuildingPlacement
        {
            public int Type;            // BuildingType enum ordinal
            public string Id;
            public string Label;
            public float X;
            public float Z;
            public float YawDeg;        // face the road / plaza
            public string Fbx;          // base name in buildings/<color>/
            public Color PlaceholderColor;
            public string FenceKind;    // "wood" or "stone"
            public string CustomFbx;    // optional full asset path to a custom (Tripo) FBX; overrides Fbx/Building() when set
            public string BaseColorTex; // optional: force this basecolor texture onto a clean URP/Lit material (single-texture Tripo buildings)
        }

        // Quadrant placements per spec §5. N = +Z. Curtain wall is [-28..+28] X,
        // [-21..+21] Z (south bows to -25). Buildings sit on 2×2-hex plots.
        private static readonly BuildingPlacement[] Buildings =
        {
            // Crystal Mine — moved outside the NW wall per owner direction
            // 2026-05-20 ("move those mines outside the village for
            // foraging"). Hero walks out the west or north gate to mine.
            new BuildingPlacement { Type = 0, Id = "crystal-mine", Label = "Crystal Mine",
                X = -38f, Z = 14f, YawDeg = 135f, Fbx = "building_mine",
                CustomFbx = "Assets/Art/TripoStructures/LumberMill.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/LumberMill.fbm/LumberMill_basecolor.JPEG",
                PlaceholderColor = new Color(0.38f, 0.65f, 0.98f), FenceKind = "stone" },
            // Pet House — Southwest creek-side (§5).
            new BuildingPlacement { Type = 1, Id = "pet-house", Label = "Pet House",
                X = -17f, Z = -10.5f, YawDeg = 55f, Fbx = "building_stables",
                CustomFbx = "Assets/Art/TripoStructures/PetHome.fbx",
                PlaceholderColor = new Color(0.98f, 0.82f, 0.48f), FenceKind = "wood" },
            // Arcane Tower — South-central, near the Keep (§5).
            new BuildingPlacement { Type = 2, Id = "arcane-tower", Label = "Arcane Tower",
                X = 6f, Z = -12.5f, YawDeg = 0f, Fbx = "building_tower_A",
                CustomFbx = "Assets/Art/TripoStructures/BuildTower.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/BuildTower.fbm/build_tower_basecolor.JPEG",
                PlaceholderColor = new Color(0.65f, 0.55f, 0.98f), FenceKind = "stone" },
            // Workshop — Northeast artisan district (§5).
            new BuildingPlacement { Type = 3, Id = "workshop", Label = "Workshop",
                X = 16f, Z = 12.5f, YawDeg = 215f, Fbx = "building_workshop",
                CustomFbx = "Assets/Art/TripoStructures/Forge.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/Forge.fbm/Forge_basecolor.JPEG",
                PlaceholderColor = new Color(1f, 0.60f, 0.32f), FenceKind = "wood" },
            // Farm — East open ground (§5). Windmill mesh.
            new BuildingPlacement { Type = 4, Id = "farm", Label = "Farm",
                X = 19f, Z = -1f, YawDeg = 270f, Fbx = "building_windmill",
                CustomFbx = "Assets/Art/TripoStructures/Farm.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/Farm.fbm/farm_basecolor.JPEG",
                PlaceholderColor = new Color(1f, 0.85f, 0.54f), FenceKind = "wood" },
        };

        private static int BuildBuildings(Transform parent, Component controller)
        {
            int count = 0;
            foreach (var b in Buildings)
            {
                var go = new GameObject($"Building-{b.Id} ({b.Label})");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(b.X, 0f, b.Z);
                go.transform.rotation = Quaternion.Euler(0f, b.YawDeg, 0f);

                // Owner Tripo models override the KayKit mesh when CustomFbx is set.
                bool custom = !string.IsNullOrEmpty(b.CustomFbx);
                var model = LoadModel(custom ? b.CustomFbx : Building(b.Fbx));
                GameObject visual = InstantiateModel(model,
                    custom ? System.IO.Path.GetFileName(b.CustomFbx)
                           : b.Fbx + "_" + BuildingColor + ".fbx",
                    $"{(custom ? b.CustomFbx : b.Fbx)} -> {b.Label}");
                visual.transform.SetParent(go.transform, false);
                if (model == null)
                {
                    visual.transform.localScale = new Vector3(3f, 3f, 3f);
                    visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    ApplyColor(visual, b.PlaceholderColor);
                }
                else if (custom)
                {
                    // Owner Tripo building. The FBX imports tipped ~90deg (lying
                    // flat), so stand it upright; then normalize the longest
                    // dimension to ~7 m and strip native colliders/rigidbody (the
                    // plot footprint BoxCollider below is the single gameplay collider).
                    visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    NormalizeProp(visual, 7f);
                    StripColliders(visual);
                    StripRigidbodies(visual);

                    if (!string.IsNullOrEmpty(b.BaseColorTex))
                    {
                        // Single-texture building: the auto-extracted Tripo materials
                        // render as a rainbow patchwork, so force a clean URP/Lit
                        // material built straight from the model's basecolor.
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(b.BaseColorTex);
                        var lit = Shader.Find("Universal Render Pipeline/Lit");
                        if (tex != null && lit != null)
                        {
                            var mat = new Material(lit) { name = b.Id + "_basecolor (URP)" };
                            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                            foreach (var rr in visual.GetComponentsInChildren<Renderer>(true))
                            {
                                var arr = rr.sharedMaterials;
                                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                                rr.sharedMaterials = arr;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[VillageSceneBuilder] basecolor '{b.BaseColorTex}' not found for {b.Id}; leaving Tripo materials.");
                        }
                    }
                    else
                    {
                        // Multi-part building (PetHome): rebuild each part's material
                        // as plain URP/Lit carrying its own basecolor (force-rebuild).
                        var bFixer = FindType("DeNelle.Core.TripoMaterialFixer");
                        if (bFixer != null)
                        {
                            var fx = visual.AddComponent(bFixer);
                            bFixer.GetMethod("ForceRebuildAll")?.Invoke(fx, null);
                        }
                    }
                }
                else
                {
                    // Native KayKit houses are ~1.2m tall; hero capsule is 2m.
                    // Owner direction (2026-05-19): houses should read as 3x
                    // hero height (≈6m). 4.5x lifts a ~1.2m house to ~5.4m.
                    visual.transform.localScale = new Vector3(BuildingScale, BuildingScale, BuildingScale);
                }
                // Owner 2026-05-20: hero used to walk THROUGH buildings —
                // adding a footprint BoxCollider so HeroLocomotion's CapsuleCast
                // blocks the move. Sized from mesh bounds; sits on the plot
                // GameObject (not the visual mesh) so it isn't scaled twice.
                AddBuildingFootprintCollider(go, visual);

                // Low fence marking the 2×2-hex property line (§5 -- dressing,
                // no collider). Build it as a child of the plot, not the
                // building visual, so it stays put.
                BuildPlotFence(go.transform, 3.4f, 3.4f, b.FenceKind);

                var comp = AddVillageComponent(go, TypeBuilding);
                if (comp != null)
                {
                    InvokeConfigure(comp, "Configure", b.Type, b.Id, b.Label);
                    RegisterWith(controller, "RegisterBuilding", comp);
                }
                count++;
            }
            return count;
        }

        /// <summary>
        /// Rings a building plot with a low KayKit fence (dressing only — the
        /// fence pieces have their colliders stripped so they never block
        /// pathing, per spec §5).
        /// </summary>
        private static void BuildPlotFence(Transform parent, float halfX, float halfZ, string kind)
        {
            // Owner direction 2026-05-20: "those wooden things in ground need
            // to disappear" — the per-plot fences read as clutter around the
            // bare yards and add nothing visually. Disabled entirely until a
            // real reason to wrap a plot lands.
            return;
            #pragma warning disable CS0162 // unreachable code retained for re-enable
            string fbx = kind == "stone"
                ? HexNeutral + "fence_stone_straight.fbx"
                : HexNeutral + "fence_wood_straight.fbx";
            var fenceModel = LoadModel(fbx);
            var fenceRoot = NewChild(parent, "PlotFence");

            // Four sides; each side is one fence piece scaled to span.
            (Vector3 pos, float yaw, float span)[] sides =
            {
                (new Vector3(0f, 0f, halfZ), 0f, halfX * 2f),
                (new Vector3(0f, 0f, -halfZ), 0f, halfX * 2f),
                (new Vector3(halfX, 0f, 0f), 90f, halfZ * 2f),
                (new Vector3(-halfX, 0f, 0f), 90f, halfZ * 2f),
            };
            foreach (var (pos, yaw, span) in sides)
            {
                var f = InstantiateModel(fenceModel, Path.GetFileName(fbx), "plot fence");
                f.transform.SetParent(fenceRoot, false);
                f.transform.localPosition = pos;
                f.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                StripColliders(f);
                if (fenceModel != null)
                {
                    // Same fixed-length-module fit as the curtain wall: stretch
                    // the fence piece's long horizontal axis to span the plot
                    // side. Local-space measure -> immune to the side's yaw.
                    FitWallVisualToRun(f, span);
                }
                else
                {
                    f.transform.localScale = new Vector3(span, 0.7f, 0.12f);
                    f.transform.localPosition = pos + Vector3.up * 0.35f;
                    ApplyColor(f, kind == "stone"
                        ? new Color(0.55f, 0.53f, 0.49f)
                        : new Color(0.46f, 0.34f, 0.22f));
                }
                _propCount++;
            }
            #pragma warning restore CS0162
        }

        // =====================================================================
        //  City dressing (§6) — residential / market / workshop / orchard
        // =====================================================================

        private struct DressDef
        {
            public string Name;
            public string Fbx;       // base name (colour appended for coloured buildings)
            public bool Neutral;     // true => no colour suffix (path is neutral/)
            public float X, Z, Yaw;
            public Color PlaceholderColor;
        }

        private static void BuildCityDressing(Transform parent)
        {
            // Owner direction 2026-05-20 ("spread out the town structures
            // wider — the clustered ones prevent navigation"). Spaced each
            // dressing building's footprint by ~1.5× from its prior
            // position so the hero (+ pet pack) can walk between them
            // freely. Quarter labels unchanged.

            // ── §6.1 Residential cluster (SW) — homes around a well ──────────
            var residential = NewChild(parent, "Residential-SW");
            var residentialDefs = new[]
            {
                new DressDef { Name = "Home-A1", Fbx = "building_home_A", X = -30f, Z = -8f, Yaw = 70f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-A2", Fbx = "building_home_A", X = -30f, Z = -18f, Yaw = 95f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-A3", Fbx = "building_home_A", X = -14f, Z = -22f, Yaw = 160f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-B1", Fbx = "building_home_B", X = -22f, Z = -23f, Yaw = 200f, PlaceholderColor = C("c9b48a") },
                new DressDef { Name = "Home-B2", Fbx = "building_home_B", X = -32f, Z = -23f, Yaw = 25f, PlaceholderColor = C("c9b48a") },
                new DressDef { Name = "Home-B3", Fbx = "building_home_B", X = -14f, Z = -14f, Yaw = 120f, PlaceholderColor = C("c9b48a") },
            };
            foreach (var d in residentialDefs) PlaceDressing(residential, d, false);
            PlaceDressing(residential,
                new DressDef { Name = "Well", Fbx = "building_well", X = -23f, Z = -16f, Yaw = 0f, PlaceholderColor = C("8aa0b0") },
                false);

            // ── §6.2 Market quarter (around the plaza, south) ────────────────
            var market = NewChild(parent, "Market-S");
            PlaceDressing(market,
                new DressDef { Name = "Market", Fbx = "building_market", X = -4f, Z = -13f, Yaw = 10f, PlaceholderColor = C("c98f4a") },
                false);
            PlaceDressing(market,
                new DressDef { Name = "Tavern", Fbx = "building_tavern", X = 16f, Z = -12f, Yaw = 250f, PlaceholderColor = C("b5793c") },
                false);
            PlaceDressing(market,
                new DressDef { Name = "Church", Fbx = "building_church", X = -3f, Z = 14f, Yaw = 185f, PlaceholderColor = C("d7d2c4") },
                false);

            // ── §6.3 Workshop quarter (NE) — blacksmith + townhall ───────────
            var workshopQ = NewChild(parent, "Workshop-NE");
            PlaceDressing(workshopQ,
                new DressDef { Name = "Blacksmith", Fbx = "building_blacksmith", X = 30f, Z = 13f, Yaw = 230f, PlaceholderColor = C("8a7d6a") },
                false);
            PlaceDressing(workshopQ,
                new DressDef { Name = "Townhall", Fbx = "building_townhall", X = 16f, Z = 12f, Yaw = 200f, PlaceholderColor = C("c2b79a") },
                false);
            BuildWorkshopYard(workshopQ, new Vector3(27f, 0f, 13f));

            // ── §6.4 Farm / orchard (E) — orchard tiles + farmer's hut ───────
            var orchard = NewChild(parent, "Orchard-E");
            BuildOrchard(orchard, new Vector3(26f, 0f, -1f));
            PlaceDressing(orchard,
                new DressDef { Name = "FarmersHut", Fbx = "building_home_A", X = 31f, Z = -14f, Yaw = 290f, PlaceholderColor = C("d8c69a") },
                false);

            // ── §6.5 Northern open ground ────────────────────────────────────
            // Owner direction 2026-05-20 ("rock still persists north gate"):
            // the building_shrine + scatter trees (cut-stumps that read as
            // rocks) all removed. Northern open ground is now genuinely
            // open — clean approach to the north gate.
            var northern = NewChild(parent, "Northern-OpenGround");
            // ScatterTrees(northern, new[] { ... }) — removed.
            // The return below short-circuits the legacy trailing
            // ScatterTrees call lower in this method.
            return;
            // A few scattered trees on the northern open ground (§6.5).
            ScatterTrees(northern, new[]
            {
                new Vector3(-9f, 0f, 16f), new Vector3(9f, 0f, 15f),
                new Vector3(-20f, 0f, 6f), new Vector3(21f, 0f, 17f),
            });
        }

        /// <summary>Instantiates one city-dressing building from a <see cref="DressDef"/>.</summary>
        private static void PlaceDressing(Transform parent, DressDef d, bool neutral)
        {
            var go = new GameObject("Dress-" + d.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d.X, 0f, d.Z);
            go.transform.rotation = Quaternion.Euler(0f, d.Yaw, 0f);

            string path = neutral || d.Neutral
                ? HexNeutral + d.Fbx + ".fbx"
                : Building(d.Fbx);
            var model = LoadModel(path);
            var visual = InstantiateModel(model, Path.GetFileName(path), d.Name);
            visual.transform.SetParent(go.transform, false);
            if (model == null)
            {
                visual.transform.localScale = new Vector3(2.6f, 2.6f, 2.6f);
                visual.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                ApplyColor(visual, d.PlaceholderColor);
            }
            else
            {
                visual.transform.localScale = new Vector3(BuildingScale, BuildingScale, BuildingScale);
                // Owner 2026-05-20 (black blobs in screenshot): several
                // KayKit dressing FBXes render as untextured dark shapes
                // because the hex atlas doesn't resolve in URP. Attach the
                // TripoMaterialFixer with the building's placeholder colour
                // as a tint so the building reads as itself, not a black
                // blob. Idempotent — a no-op when the atlas DID bind.
                var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fixerType != null)
                {
                    var fixer = visual.AddComponent(fixerType);
                    var setTint = fixerType.GetMethod("SetFallbackTint");
                    setTint?.Invoke(fixer, new object[] { d.PlaceholderColor });
                }
            }
            // Owner 2026-05-20: hero could walk through dressing buildings —
            // add a footprint BoxCollider so HeroLocomotion's sweep cast blocks
            // the move. Same approach as gameplay buildings.
            AddBuildingFootprintCollider(go, visual);
            _dressingCount++;
        }

        /// <summary>
        /// A small fenced yard between Workshop + Blacksmith with anvil / lumber
        /// / tool props (§6.3).
        /// </summary>
        private static void BuildWorkshopYard(Transform parent, Vector3 centre)
        {
            var yard = new GameObject("WorkshopYard");
            yard.transform.SetParent(parent, false);
            yard.transform.position = centre;
            BuildPlotFence(yard.transform, 2.6f, 2.4f, "wood");

            // Props — KayKit decoration/props. Each is normalised to a believable
            // largest-dimension target (metres) so the yard dressing reads at a
            // consistent scale despite the meshes' differing native sizes.
            // Owner direction 2026-05-20: the yard's wood weaponrack + lumber
            // + stone + barrel props read as "items that cause issues and
            // offer no value" — they clutter the plaza and block hero
            // pathing. Yard stripped down to just the plot fence; per-
            // building dressing can be reauthored later when each prop has
            // a real interaction hook.
        }

        /// <summary>
        /// The Farm's orchard — a patch of grass tiles dressed with apple/fruit
        /// trees + haybales around the windmill plot (§6.4).
        /// </summary>
        private static void BuildOrchard(Transform parent, Vector3 centre)
        {
            var orchardRoot = new GameObject("OrchardPlot");
            orchardRoot.transform.SetParent(parent, false);
            orchardRoot.transform.position = centre;

            // A 4×3 grid of fruit trees flanking the windmill (kept clear of
            // the building's own 2×2 plot).
            var treeModel = LoadModel(HexDecoNature + "trees_B_medium.fbx");
            for (int r = -1; r <= 1; r++)
            {
                for (int c = -2; c <= 2; c++)
                {
                    if (Mathf.Abs(c) <= 1 && r == 0) continue; // leave the mill clear
                    var t = InstantiateModel(treeModel, "trees_B_medium.fbx", "orchard tree");
                    t.name = "OrchardTree";
                    t.transform.SetParent(orchardRoot.transform, false);
                    t.transform.localPosition = new Vector3(c * 2.4f, 0f, r * 3.0f - 6f);
                    t.transform.localRotation = Quaternion.Euler(0f, (c + r) * 47f, 0f);
                    if (treeModel != null)
                        // Normalise to a consistent ~3.5m fruit tree -- the raw
                        // mesh size varies, so a flat *1.2 multiplier left the
                        // orchard reading unevenly.
                        NormalizeProp(t, 3.5f);
                    else
                    {
                        t.transform.localScale = new Vector3(1.4f, 2.6f, 1.4f);
                        t.transform.localPosition += Vector3.up * 1.3f;
                        ApplyColor(t, C("4f7a3a"));
                    }
                    _propCount++;
                }
            }
            // Haybales at the orchard edge — normalised to a ~1.4m bale.
            PlaceProp(orchardRoot.transform, HexDecoProps + "haybale.fbx",
                new Vector3(4.5f, 0f, -7f), 25f, "haybale", 1.4f);
            PlaceProp(orchardRoot.transform, HexDecoProps + "haybale.fbx",
                new Vector3(-5f, 0f, -8.5f), -40f, "haybale", 1.4f);
        }

        /// <summary>Scatters single trees at the given world positions (foliage dressing).</summary>
        private static void ScatterTrees(Transform parent, Vector3[] positions)
        {
            var treeModel = LoadModel(HexDecoNature + "tree_single_A.fbx");
            var treesRoot = NewChild(parent, "ScatteredTrees");
            int i = 0;
            foreach (var p in positions)
            {
                var t = InstantiateModel(treeModel, "tree_single_A.fbx", "scattered tree");
                t.name = $"Tree-{i++}";
                t.transform.SetParent(treesRoot, false);
                t.transform.localPosition = p;
                t.transform.localRotation = Quaternion.Euler(0f, i * 63f, 0f);
                if (treeModel != null)
                    // Normalise to a consistent ~5m tree, then apply a small
                    // per-tree size variation on top so the scatter still reads
                    // natural (the variation is now relative to a known base,
                    // not a raw mesh size that differs per import).
                    NormalizeProp(t, 5f * Mathf.Lerp(0.9f, 1.3f, (i % 3) / 3f));
                else
                {
                    t.transform.localScale = new Vector3(1.3f, 2.6f, 1.3f);
                    t.transform.localPosition += Vector3.up * 1.3f;
                    ApplyColor(t, C("3f6e34"));
                }
                _propCount++;
            }
        }

        /// <summary>
        /// Instantiates one KayKit prop at a local position; placeholder on miss.
        /// <paramref name="targetSize"/> is the prop's largest world dimension in
        /// metres — every prop is normalised to it via <see cref="NormalizeProp"/>
        /// so props from different KayKit folders read at a consistent scale.
        /// </summary>
        private static void PlaceProp(Transform parent, string assetPath, Vector3 localPos,
            float yaw, string label, float targetSize = 1.0f)
        {
            var model = LoadModel(assetPath);
            var prop = InstantiateModel(model, Path.GetFileName(assetPath), label);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPos;
            prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (model == null)
            {
                prop.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                prop.transform.localPosition = localPos + Vector3.up * 0.35f;
                ApplyColor(prop, C("9a8a6a"));
            }
            else
            {
                // KayKit props have inconsistent native mesh sizes -- normalise
                // each to a common yardstick so the dressing reads coherently.
                NormalizeProp(prop, targetSize);
            }
            _propCount++;
        }

        // =====================================================================
        //  Approach lanes + wave spawn points (§8)
        // =====================================================================

        // WO-27 (playable loop): enemies materialize this far OUTSIDE each gate and
        // march in down a paved corridor. World units (gate -> spawn ring), so the
        // distance is identical for N/S and E/W gates regardless of hex step. The
        // corridor + apron are built under the nav-static "Approaches" root and so
        // are included in BakeVillageNavMesh -> a continuous march lane to the gate.
        private const float ApproachLength = 40f;

        private static int BuildApproaches(Transform parent, Type tWallLayout, Component controller)
        {
            if (tWallLayout == null) return 0;
            var gates = ReadEnumerable(tWallLayout, "Gates");
            if (gates == null) return 0;

            var road = LoadModel(HexTilesRoads + "hex_road_A.fbx");
            var grass = LoadModel(HexTilesBase + "hex_grass.fbx");
            int spawnCount = 0;

            foreach (var gap in gates)
            {
                int index = (int)GetMember(gap, "Index");
                string direction = (string)GetMember(gap, "Direction");
                Vector3 gatePos = (Vector3)GetMember(gap, "Position");
                Vector3 outward = (Vector3)GetMember(gap, "OutwardNormal");

                var laneRoot = new GameObject($"Approach-{direction}");
                laneRoot.transform.SetParent(parent, false);

                float step = (Mathf.Abs(outward.z) > 0.5f) ? HexDepth : HexWidth;

                // WO-27: paved march corridor the full ApproachLength (40 m) out
                // each gate, 5 tiles wide (~8 m), so the NavMesh bakes a continuous
                // lane for the enemies to march down. Loops outward in hex steps
                // until the corridor reaches the spawn ring.
                Vector3 lateral = new Vector3(-outward.z, 0f, outward.x); // perpendicular
                int steps = Mathf.CeilToInt(ApproachLength / step);
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 along = gatePos + outward * (i * step);
                    foreach (var lat in new[] { -2f * HexWidth, -HexWidth, 0f, HexWidth, 2f * HexWidth })
                    {
                        var tile = InstantiateModel(road, "hex_road_A.fbx",
                            $"ApproachRoad-{direction}-{i}");
                        tile.transform.SetParent(laneRoot.transform, false);
                        tile.transform.position = along + lateral * lat + Vector3.up * 0.015f;
                        if (road == null)
                        {
                            tile.transform.localScale = new Vector3(HexWidth, 0.14f, HexWidth);
                            ApplyColor(tile, new Color(0.55f, 0.46f, 0.34f));
                        }
                        _roadCount++;
                    }
                }

                // Lane foliage / boulders removed per owner direction 2026-05-20
                // ("rocks in front of entrance"). Bare paving + grass apron only.

                // WO-27: the wave-spawn apron — a ~16 m x 16 m flat grass pad at the
                // corridor end (ApproachLength out), room for a full 12-enemy batch
                // to materialize on baked navmesh without overlap. Overlaps the
                // corridor end so apron + corridor navmesh are one continuous surface.
                Vector3 spawnCentre = gatePos + outward * ApproachLength;
                var zoneRoot = new GameObject($"SpawnZone-{direction}");
                zoneRoot.transform.SetParent(laneRoot.transform, false);
                zoneRoot.transform.position = spawnCentre;
                for (int gx = -5; gx <= 5; gx++)
                {
                    for (int gz = -5; gz <= 5; gz++)
                    {
                        var tile = InstantiateModel(grass, "hex_grass.fbx", "spawn tile");
                        tile.transform.SetParent(zoneRoot.transform, false);
                        tile.transform.localPosition = new Vector3(gx * HexWidth, 0.01f, gz * HexDepth);
                        if (grass == null)
                        {
                            tile.transform.localScale = new Vector3(HexWidth, 0.18f, HexWidth);
                            ApplyColor(tile, C("4a5a32"));
                        }
                        _groundCount++;
                    }
                }

                // The WaveSpawnPoint marker (§8.3) — invisible empty GO.
                var spawnGo = new GameObject($"WaveSpawnPoint-{direction}");
                spawnGo.transform.SetParent(zoneRoot.transform, false);
                spawnGo.transform.localPosition = Vector3.zero;
                var spawnComp = AddVillageComponent(spawnGo, TypeWaveSpawnPoint);
                if (spawnComp != null)
                {
                    InvokeConfigure(spawnComp, "Configure",
                        "spawn-" + index, index, direction, gatePos);
                }
                spawnCount++;
            }
            _ = controller;
            return spawnCount;
        }

        // =====================================================================
        //  Camera + light
        // =====================================================================

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Soft dawn pink-violet horizon tint (spec §14 Q4 default).
            camera.backgroundColor = new Color(0.74f, 0.66f, 0.72f);
            camera.farClipPlane = 600f;
            cameraGo.tag = "MainCamera";

            // ── Camera-angle: over-the-right-shoulder of Hero (Blaise) ───────
            // Hero spawns at world (2.5, 0, 2.5) facing +Z (toward open plaza).
            // Hero is a ~2m capsule (HeroBody at localPosition (0,1,0) on a
            // root at ground). Owner direction (2026-05-19): start over right
            // shoulder, ~2 feet up + 2 feet back. 2 ft ≈ 0.6 m (Unity units).
            //   • shoulder X offset: +0.3 right of hero center;
            //   • Y: hero head ~Y=2, +0.6 above = Y ≈ 2.6;
            //   • Z: 0.6 behind hero (hero faces +Z, so −0.6 in world Z) = 1.9.
            // FOV 60deg is the Unity default, comfortable for 3rd-person view.
            // Slight downward pitch (12deg) so the hero's back/shoulders frame
            // the lower-third of the screen.
            camera.fieldOfView = 60f;
            cameraGo.transform.position = new Vector3(2.8f, 2.6f, 1.9f);
            cameraGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            cameraGo.AddComponent<AudioListener>();

            // Attach the over-shoulder follow component. The hero transform is
            // wired later, after BuildHero returns (BuildVillage flow).
            AddVillageComponent(cameraGo, TypeVillageCamera);
        }

        /// <summary>
        /// Creates an EventSystem GameObject with the new-Input-System UI module
        /// in the active scene. UI Toolkit needs this to route pointer events to
        /// button.clicked handlers (HUD buttons silent without it). No-op when
        /// one already exists.
        /// </summary>
        private static void EnsureEventSystem()
        {
            var esType = FindType(TypeEventSystem);
            if (esType == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] UnityEngine.EventSystems.EventSystem " +
                                 "type not resolvable — HUD button clicks will not fire.");
                return;
            }
            var existing = UnityEngine.Object.FindObjectOfType(esType);
            if (existing != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent(esType);

            var moduleType = FindType(TypeInputSystemUIInputModule);
            if (moduleType != null) go.AddComponent(moduleType);
            else Debug.LogWarning("[VillageSceneBuilder] InputSystemUIInputModule type not " +
                                  "resolvable — falling back to EventSystem-only routing.");
        }

        /// <summary>
        /// Wires VillageHudController.BuildRequested → BuildMenu.Open so the
        /// HUD's Build button actually opens the placement menu.
        /// </summary>
        private static void WireBuildMenuHudBridge()
        {
            var buildMenuType = FindType(TypeBuildMenu);
            var hudType = FindType(TypeVillageHudController);
            var bridgeType = FindType(TypeBuildMenuHudBridge);
            if (buildMenuType == null || hudType == null || bridgeType == null) return;

            var buildMenu = UnityEngine.Object.FindObjectOfType(buildMenuType);
            var hud = UnityEngine.Object.FindObjectOfType(hudType);
            if (buildMenu == null || hud == null) return;

            var menuGo = ((Component)buildMenu).gameObject;
            var bridge = menuGo.GetComponent(bridgeType) ?? menuGo.AddComponent(bridgeType);

            var so = new SerializedObject(bridge);
            SetObjectField(so, "_hud", (UnityEngine.Object)hud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires VillageHudController.AbilityRequested → HeroAbilities.TryCast
        /// via a bridge component on the hero, so the HUD's Q/W/E/R buttons
        /// actually cast (clicks were dead before this — 2026-05-20).
        /// </summary>
        private static void WireHeroAbilitiesHudBridge()
        {
            var heroAbilitiesType = FindType(TypeHeroAbilities);
            var hudType = FindType(TypeVillageHudController);
            var bridgeType = FindType(TypeHeroAbilitiesHudBridge);
            if (heroAbilitiesType == null || hudType == null || bridgeType == null) return;

            var abilities = UnityEngine.Object.FindObjectOfType(heroAbilitiesType);
            var hud = UnityEngine.Object.FindObjectOfType(hudType);
            if (abilities == null || hud == null) return;

            var heroGo = ((Component)abilities).gameObject;
            var bridge = heroGo.GetComponent(bridgeType) ?? heroGo.AddComponent(bridgeType);

            var so = new SerializedObject(bridge);
            SetObjectField(so, "_hud", (UnityEngine.Object)hud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds a <c>BuildingInteractable</c> to every <c>Building</c> in the
        /// scene so walking near one shows the "Press F" prompt + dispatches.
        /// </summary>
        private static void WireBuildingInteractables()
        {
            var buildingType = FindType(TypeBuilding);
            var interactableType = FindType(TypeBuildingInteractable);
            if (buildingType == null || interactableType == null) return;
            foreach (var b in UnityEngine.Object.FindObjectsByType(
                         buildingType, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b is Component c && c.GetComponent(interactableType) == null)
                    c.gameObject.AddComponent(interactableType);
            }
        }

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

        private static void SpawnDungeonPortal()
        {
            var portalType = FindType(TypeDungeonPortal);
            if (portalType == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] DungeonPortal type not found — skipping.");
                return;
            }

            // Clean any pre-existing portals from prior builds.
            foreach (var name in new[] { "DungeonPortal", "DungeonPortal_HealersCottage",
                                         "DungeonPortal_FolksGranary" })
            {
                var existing = GameObject.Find(name);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }

            // Owner feedback 2026-05-20 ("still doorway items that say
            // healers cottage in front of gate"): the stone-arch portals
            // were blocking the south-gate sightline. Relocated to the
            // EAST and WEST sides of the village interior, well off the
            // N-S gate spine, so they read as side attractions instead of
            // gate clutter.
            // dungeonId is the SHORT id — SceneRouter.GoDungeon prepends
            // "Dungeon_". Passing the full scene name double-prefixed and
            // routed to a missing scene (owner 2026-05-20: prior connection
            // error). Strict short ids only.
            // Folk's Granary authored by FolksGranaryBuilder — east portal
            // routes back to its own dungeon now.
            BuildOneDungeonPortal(portalType, "DungeonPortal_HealersCottage",
                new Vector3(-18f, 0f, 6f), "HealersCottage", "Healer's Cottage");
            BuildOneDungeonPortal(portalType, "DungeonPortal_FolksGranary",
                new Vector3( 18f, 0f, 6f), "FolksGranary",   "Folk's Old Granary");
        }

        private static void BuildOneDungeonPortal(System.Type portalType, string objectName,
                                                  Vector3 position, string dungeonId,
                                                  string displayName)
        {
            var root = new GameObject(objectName);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            const float archWidth = 4.5f;
            const float archHeight = 5.2f;

            // Owner 2026-05-20 ("cannot find entrance to healers cottage"):
            // the portal needs a visible marker so the player can find it.
            // Use a flat ground disc + always-visible floating sign — neither
            // depends on the painted-material shader-find that previously
            // turned the arch into a violet ghost.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "PortalDisc";
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.transform.localScale = new Vector3(3.5f, 0.05f, 3.5f);
            // Bright violet disc that reads at distance — URP/Lit with a
            // strong base colour (no texture, no shader-find risk).
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = "PortalDisc" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.55f, 0.30f, 0.95f, 1f));
                if (mat.HasProperty("_Color"))     mat.SetColor("_Color", new Color(0.55f, 0.30f, 0.95f, 1f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(0.55f, 0.30f, 0.95f) * 1.5f);
                    mat.EnableKeyword("_EMISSION");
                }
                disc.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // Floating "Healer's Cottage" sign — TextMesh world-space label,
            // always visible (not gated on proximity).
            var sign = new GameObject("PortalSign");
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            sign.transform.localScale = Vector3.one * 0.08f;
            var tm = sign.AddComponent<TextMesh>();
            tm.text = "▼ " + displayName + " ▼";
            tm.fontSize = 64;
            tm.characterSize = 0.35f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.86f, 0.55f);
            // Mount a billboard component so the sign faces the camera.
            var bbType = FindType(NsVillage + ".PromptBillboard");
            if (bbType != null) sign.AddComponent(bbType);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, archHeight * 0.5f, 0f);
            trigger.size = new Vector3(archWidth, archHeight, 0.6f);
            trigger.isTrigger = true;

            // Mount the portal logic.
            var portal = root.AddComponent(portalType);
            var cfgMethod = portalType.GetMethod("Configure");
            cfgMethod?.Invoke(portal, new object[] { dungeonId, displayName });
        }

        private static void BuildPortalPillar(Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pillar";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            PaintMaterial(go.GetComponent<Renderer>(), new Color(0.32f, 0.28f, 0.25f), false);
        }

        private static void PaintMaterial(Renderer renderer, Color colour, bool transparent)
        {
            if (renderer == null) return;
            Shader shader = transparent
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
                : (Shader.Find("Universal Render Pipeline/Lit")   ?? Shader.Find("Standard"));
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", colour);
            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.renderQueue = 3000;
            }
            renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Wires WaveManager → VillageHudController so the HUD's wave timer
        /// actually updates. Adds a <c>WaveHudBridge</c> onto the WaveManager
        /// GameObject (the bridge talks to the HUD by reflection so DeNelle.Village
        /// does not have to reference DeNelle.HUD).
        /// </summary>
        private static void WireWaveHudBridge()
        {
            var waveType = FindType(TypeWaveManager);
            var hudType = FindType(TypeVillageHudController);
            var bridgeType = FindType(TypeWaveHudBridge);
            if (waveType == null || hudType == null || bridgeType == null) return;

            var wave = UnityEngine.Object.FindObjectOfType(waveType);
            var hud = UnityEngine.Object.FindObjectOfType(hudType);
            if (wave == null || hud == null) return;

            var waveGo = ((Component)wave).gameObject;
            var bridge = waveGo.GetComponent(bridgeType) ?? waveGo.AddComponent(bridgeType);

            var so = new SerializedObject(bridge);
            SetObjectField(so, "_wave", (UnityEngine.Object)wave);
            SetObjectField(so, "_hud", (UnityEngine.Object)hud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds the DailyQuestCombatBridge to the WaveManager GameObject so
        /// OnWaveCleared ticks the daily-quest service. Idempotent.
        /// </summary>
        private static void WireDailyQuestCombatBridge()
        {
            var waveType = FindType(TypeWaveManager);
            var bridgeType = FindType(TypeDailyQuestCombatBridge);
            if (waveType == null || bridgeType == null) return;
            var wave = UnityEngine.Object.FindObjectOfType(waveType);
            if (wave == null) return;
            var waveGo = ((Component)wave).gameObject;
            if (waveGo.GetComponent(bridgeType) == null)
                waveGo.AddComponent(bridgeType);
        }

        /// <summary>
        /// Finds the Main Camera built by <see cref="CreateCamera"/>, locates its
        /// VillageCamera follow component (added there), and sets the target
        /// transform to <paramref name="hero"/>. No-op if either is missing.
        /// </summary>
        private static void WireVillageCameraTarget(GameObject hero)
        {
            if (hero == null) return;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] No Camera.main found — " +
                                 "skipping VillageCamera target wiring.");
                return;
            }
            var camType = FindType(TypeVillageCamera);
            if (camType == null) return;
            var follow = cam.GetComponent(camType);
            if (follow == null) return;
            var so = new SerializedObject(follow);
            SetObjectField(so, "_target", hero.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateDirectionalLight()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            // Soft dawn — warm low sun (spec §14 Q4 default, §9.5 register).
            light.color = new Color(1f, 0.93f, 0.82f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            // ~15° above the horizon (spec §9.5).
            lightGo.transform.rotation = Quaternion.Euler(16f, -35f, 0f);
        }

        // =====================================================================
        //  Controller wiring (SerializedObject -- no compile-time dependency)
        // =====================================================================

        private static void WireController(Component controller, Transform wallRoot,
            Transform gateRoot, Transform buildingRoot, Component heart)
        {
            if (controller == null) return;
            var so = new SerializedObject(controller);
            SetObjectField(so, "_wallRoot", wallRoot);
            SetObjectField(so, "_gateRoot", gateRoot);
            SetObjectField(so, "_buildingRoot", buildingRoot);
            SetObjectField(so, "_heart", heart);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectField(SerializedObject so, string field, UnityEngine.Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[VillageSceneBuilder] Serialized field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} -- wiring skipped for that field.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        // =====================================================================
        //  KayKit model loading
        // =====================================================================

        /// <summary>
        /// Loads a model GameObject at an asset path. Returns null (caller falls
        /// back to a placeholder) when the asset is missing. Tries the given
        /// path, then the same path with a ".prefab" extension.
        /// </summary>
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

        /// <summary>
        /// Instantiates a loaded KayKit model. When <paramref name="model"/> is
        /// null a clearly-labelled placeholder cube is returned and the miss is
        /// logged + tallied.
        /// </summary>
        private static GameObject InstantiateModel(GameObject model, string assetLabel,
            string placeholderLabel)
        {
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (instance != null)
                {
                    instance.name = model.name;
                    // The whole Hexagon pack shares one atlas — force the shared
                    // URP material on so instances render textured even when the
                    // FBX importer's material remap fails to resolve (the
                    // decoration/nature meshes; see unity-decisions.md 2026-05-19).
                    ForceHexMaterial(instance);
                    return instance;
                }
            }
            return MakePlaceholderCube($"{assetLabel} -> {placeholderLabel}");
        }

        /// <summary>
        /// Axis-aligned bounds of every mesh under <paramref name="go"/>, expressed
        /// in <paramref name="go"/>'s OWN local space — independent of any rotation
        /// on <paramref name="go"/> itself OR on its parents.
        ///
        /// <para>Why this exists. The naive measure used <c>Renderer.bounds.size</c>,
        /// which is a WORLD-space AABB: once the piece (or a parent) is rotated, that
        /// AABB no longer maps to the mesh's own X/Y/Z extents, so a "length along
        /// local X" reading was actually returning the piece's depth/thickness. We
        /// instead take each child MeshFilter's <c>sharedMesh.bounds</c> (true mesh
        /// space) and push its 8 corners through
        /// <c>go.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix</c>.
        /// That round-trip cancels every rotation between the mesh and
        /// <paramref name="go"/>, leaving extents measured along
        /// <paramref name="go"/>'s local axes — exactly the axes <c>localScale</c>
        /// stretches.</para>
        /// </summary>
        private static bool TryMeasureLocalBounds(GameObject go, out Bounds local)
        {
            local = default;
            if (go == null) return false;
            bool any = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = go.transform.worldToLocalMatrix *
                              mf.transform.localToWorldMatrix;
                Vector3 c = mb.center, e = mb.extents;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 corner = m.MultiplyPoint3x4(
                        c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                    if (!any) { local = new Bounds(corner, Vector3.zero); any = true; }
                    else local.Encapsulate(corner);
                }
            }
            // Skinned meshes (rare for static dressing) — fall back to renderer
            // localBounds, which is already mesh-space for a SkinnedMeshRenderer.
            if (!any)
            {
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr == null) continue;
                    Bounds lb = smr.localBounds;
                    if (!any) { local = lb; any = true; }
                    else local.Encapsulate(lb);
                }
            }
            return any;
        }

        /// <summary>
        /// Scales a wall/fence visual so it spans exactly <paramref name="runLength"/>
        /// along the run direction. The KayKit straight modules are a fixed length;
        /// this stretches whichever of the visual's HORIZONTAL local axes is the long
        /// one (the run axis) up to <paramref name="runLength"/>, leaving height and
        /// thickness untouched. Auto-detecting the long axis makes the fit correct
        /// regardless of the piece's native orientation or its yaw-fix rotation —
        /// so straights tile flush against the native-scale corner pieces.
        /// </summary>
        private static void FitWallVisualToRun(GameObject visual, float runLength)
        {
            if (visual == null || runLength <= 0.01f) return;
            if (!TryMeasureLocalBounds(visual, out var lb)) return;

            var s = visual.transform.localScale;
            // The run axis is the longer of the two horizontal mesh extents.
            if (lb.size.x >= lb.size.z)
            {
                if (lb.size.x > 0.01f) s.x *= runLength / lb.size.x;
            }
            else
            {
                if (lb.size.z > 0.01f) s.z *= runLength / lb.size.z;
            }
            visual.transform.localScale = s;
        }

        /// <summary>
        /// Normalises a KayKit prop / dressing instance to a consistent, believable
        /// size. KayKit props are authored across several folders/packs at wildly
        /// different native mesh scales — a barrel, a haybale and a weapon rack do
        /// NOT share a unit, so dropping them all in at <c>localScale = 1</c> makes
        /// some read far too big and others too small next to each other and the
        /// buildings.
        ///
        /// <para>The fix: measure the instance's true mesh bounds (rotation-immune,
        /// via <see cref="TryMeasureLocalBounds"/>) and apply a UNIFORM scale that
        /// brings its largest extent — horizontal footprint or height, whichever
        /// dominates — to <paramref name="targetSize"/> world units. Every prop type
        /// is then sized to the same yardstick, so the village dressing reads
        /// coherently. The scale is clamped to a sane band so a freak mesh (or a
        /// placeholder cube) can't explode or vanish.</para>
        /// </summary>
        /// <param name="go">The prop instance to rescale (multiplies its current localScale).</param>
        /// <param name="targetSize">Desired largest world-space dimension, in metres.</param>
        private static void NormalizeProp(GameObject go, float targetSize)
        {
            if (go == null || targetSize <= 0.001f) return;
            if (!TryMeasureLocalBounds(go, out var lb)) return;

            // Largest of the three native extents under the prop's current scale.
            Vector3 cur = go.transform.localScale;
            float nativeMax = Mathf.Max(
                lb.size.x * Mathf.Abs(cur.x),
                Mathf.Max(lb.size.y * Mathf.Abs(cur.y), lb.size.z * Mathf.Abs(cur.z)));
            if (nativeMax < 0.0001f) return;

            float factor = targetSize / nativeMax;
            factor = Mathf.Clamp(factor, 0.05f, 40f); // guard freak meshes / placeholders
            go.transform.localScale = cur * factor;
        }

        /// <summary>
        /// Lifts/lowers <paramref name="go"/> on its local Y so the bottom of
        /// its combined renderer bounds lands at the parent's y. Use after
        /// <see cref="NormalizeProp"/> on any FBX whose pivot is off-floor
        /// (most Tripo exports). World-space bounds are read, then the offset
        /// is applied in local space so further parent transforms are respected.
        /// </summary>
        private static void SnapFeetToParent(GameObject go)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float parentY = go.transform.parent != null ? go.transform.parent.position.y : 0f;
            float footOffset = b.min.y - parentY;
            if (Mathf.Abs(footOffset) < 0.001f) return;
            go.transform.localPosition -= new Vector3(0f, footOffset, 0f);
        }

        // =====================================================================
        //  Reflection helpers
        // =====================================================================

        private static Component AddVillageComponent(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"[VillageSceneBuilder] Type '{fullTypeName}' not found -- is the " +
                               "DeNelle.Village assembly compiled? Component skipped.");
                return null;
            }
            return go.AddComponent(type);
        }

        private static System.Collections.IEnumerable ReadEnumerable(Type type, string propName)
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Debug.LogError($"[VillageSceneBuilder] Static property '{propName}' not found on {type.Name}.");
                return null;
            }
            return prop.GetValue(null) as System.Collections.IEnumerable;
        }

        private static object GetMember(object instance, string name)
        {
            var t = instance.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(instance);
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) return p.GetValue(instance);
            Debug.LogWarning($"[VillageSceneBuilder] Member '{name}' not found on {t.Name}.");
            return null;
        }

        private static void InvokeConfigure(Component target, string method, params object[] args)
        {
            if (target == null) return;
            var t = target.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != method) continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                try
                {
                    var coerced = new object[args.Length];
                    for (int i = 0; i < args.Length; i++)
                        coerced[i] = CoerceArg(args[i], ps[i].ParameterType);
                    m.Invoke(target, coerced);
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VillageSceneBuilder] {t.Name}.{method}() invoke failed: {e.Message}");
                    return;
                }
            }
            Debug.LogWarning($"[VillageSceneBuilder] No '{method}' overload with {args.Length} arg(s) on {t.Name}.");
        }

        private static object CoerceArg(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;
            if (targetType.IsEnum) return Enum.ToObject(targetType, value);
            if (typeof(IConvertible).IsAssignableFrom(targetType) && value is IConvertible)
                return Convert.ChangeType(value, targetType);
            return value;
        }

        private static void RegisterWith(Component controller, string method, params object[] args)
        {
            if (controller == null) return;
            InvokeConfigure(controller, method, args);
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
        //  Primitive / colour helpers
        // =====================================================================

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject PrimitiveChild(Transform parent, string name,
            PrimitiveType prim, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            ApplyColor(go, color);
            return go;
        }

        private static GameObject MakePlaceholderCube(string label)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"[PLACEHOLDER] {label}";
            // Force a neutral-gray URP material so the placeholder doesn't render
            // as URP's magenta "missing material" sphere/cube. Also drop the
            // collider — placeholder dressing must not block pathing / picks.
            ApplyColor(cube, new Color(0.65f, 0.65f, 0.65f));
            var col = cube.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            NotePlaceholder(label);
            return cube;
        }

        private static void NotePlaceholder(string label)
        {
            _placeholderCount++;
            if (_placeholders.Count < 24) _placeholders.Add(label);
            Debug.LogWarning($"[VillageSceneBuilder] KayKit asset missing -- placeholder primitive used for: {label}");
        }

        /// <summary>Strips every collider from a model instance (fence / prop dressing).</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(c);
        }

        /// <summary>
        /// Strips every Rigidbody from a model instance. Imported meshes from
        /// third-party packs occasionally include a default Rigidbody on the
        /// root — combined with our hero collider that meant the hero fell
        /// through the village floor (gravity applied + no ground collision).
        /// </summary>
        private static void StripRigidbodies(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Rigidbody>())
                UnityEngine.Object.DestroyImmediate(r);
        }

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

        private static Bounds ComputeMeshBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Builds a flat-colour URP material. A bare
        /// <c>new Material(Shader.Find("Universal Render Pipeline/Lit"))</c>
        /// renders WHITE in batchmode regardless of <c>_BaseColor</c> — it never
        /// goes through URP's material import/validation, so its shader keywords
        /// are not set up. The fix: COPY a known-good URP/Lit asset material (the
        /// KayKit atlas <c>.mat</c>, proven to render — the buildings use it) so
        /// the copy inherits the full keyword/property setup, then drop its
        /// texture and recolour it.
        /// </summary>
        private static Material MakeFlatMaterial(Color color)
        {
            Material mat;
            var baseMat = HexMaterial();
            if (baseMat != null)
            {
                mat = new Material(baseMat);          // inherits proper URP setup
                mat.SetTexture("_BaseMap", null);     // drop the atlas → flat colour
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;
            return mat;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = MakeFlatMaterial(color);
        }

        /// <summary>
        /// Flat-colour URP material on EVERY renderer of an instance (root +
        /// children, all submesh slots). Used where a KayKit model's atlas
        /// material does not resolve and a clean solid tint is wanted instead.
        /// </summary>
        private static void ApplyColorAll(GameObject go, Color color)
        {
            if (go == null) return;
            var mat = MakeFlatMaterial(color);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[slots];
                for (int i = 0; i < slots; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void ApplyEmissive(GameObject go, Color color, float intensity)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = MakeFlatMaterial(color);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            renderer.sharedMaterial = mat;
        }

        // ── Shared hex-atlas material (importer-remap fallback) ──────────────
        // The whole Medieval Hexagon Pack UV-maps onto ONE shared atlas
        // (hexagons_medieval.png), so a single URP material renders every model
        // in it correctly. Most instances resolve their material through the
        // FBX importer's external-material remap — but the decoration/nature
        // meshes (the Elarion tree + standing stones) render white because that
        // remap does not resolve at runtime (see unity-decisions.md 2026-05-19).
        // ForceHexMaterial side-steps the importer entirely by assigning the
        // shared .mat straight onto the scene renderers.
        private static Material _hexMaterial;

        private static Material HexMaterial()
        {
            if (_hexMaterial != null) return _hexMaterial;
            _hexMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                HexDecoNature + "hexagons_medieval_URP.mat");
            if (_hexMaterial == null)
                Debug.LogWarning("[VillageSceneBuilder] hexagons_medieval_URP.mat not found -- " +
                                 "run KayKitMaterials.FixAllMaterials first; nature meshes may " +
                                 "render untextured.");
            return _hexMaterial;
        }

        /// <summary>
        /// Force-assigns the shared hex-atlas URP material to every renderer of a
        /// model instance. No-op when the material can't be loaded (the instance
        /// then keeps whatever the importer gave it).
        /// </summary>
        private static void ForceHexMaterial(GameObject instance)
        {
            var mat = HexMaterial();
            if (mat == null || instance == null) return;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[slots];
                for (int i = 0; i < slots; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Parses a 6-digit hex string into a Color.</summary>
        private static Color HexColor(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out var c) ? c : Color.magenta;
        }

        /// <summary>Short alias for <see cref="HexColor"/> (placeholder palette).</summary>
        private static Color C(string rrggbb) => HexColor(rrggbb);

        // =====================================================================
        //  Build Settings + folder helpers
        // =====================================================================

        private static void EnsureBuildSettings()
        {
            var current = EditorBuildSettings.scenes;
            bool hasVillage = false;
            foreach (var s in current)
            {
                if (s.path == VillageScenePath) { hasVillage = true; break; }
            }
            if (hasVillage) return;

            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(TitleScenePath)) scenes.Add(new EditorBuildSettingsScene(TitleScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(VillageScenePath, true));
            if (File.Exists(DungeonScenePath)) scenes.Add(new EditorBuildSettingsScene(DungeonScenePath, true));
            if (File.Exists(BattleScenePath)) scenes.Add(new EditorBuildSettingsScene(BattleScenePath, true));
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
        private static GameObject EnsureEnemyPrefab()
        {
            string prefabPath = GeneratedPrefabDir + "/Enemy_HollowWalker.prefab";

            var enemyType = FindType(TypeEnemy);
            var enemyDamageableType = FindType(TypeEnemyDamageable);
            if (enemyType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.Enemy not found -- " +
                               "enemy prefab skipped; WaveManager will spawn placeholders.");
                return null;
            }

            // Build the prefab content in a temp scene object.
            var go = new GameObject("Enemy_HollowWalker");
            try
            {
                go.layer = EnemyLayer;

                // KayKit skeleton mesh as the visual child (placeholder capsule
                // on a miss — same fallback discipline as the rest of the builder).
                var skeleton = LoadModel(SkeletonMinionPath);
                GameObject visual = InstantiateModel(skeleton, "Skeleton_Blade.fbx",
                    "Hollow Walker enemy");
                visual.transform.SetParent(go.transform, false);
                if (skeleton == null)
                {
                    visual.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
                    visual.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    ApplyColor(visual, new Color(0.78f, 0.80f, 0.74f)); // bone
                }
                // The skeleton mesh + children should not collide / block — the
                // Enemy's own capsule collider is the single physics body.
                StripColliders(visual);
                SetLayerRecursive(go, EnemyLayer);

                // Capsule collider — the body hero abilities + pets sweep for
                // (Physics.OverlapSphere with QueryTriggerInteraction.Collide
                // still finds a trigger). It is a TRIGGER for the same reason
                // WaveManager's placeholder capsule is: Enemy.ProbeForStructure
                // forward-SphereCasts with QueryTriggerInteraction.Ignore, so a
                // trigger body is skipped and never shadows the real structure
                // ahead. A trigger body also keeps enemies from physically
                // jostling each other / the navmesh agents.
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.height = 2.0f;
                capsule.radius = 0.45f;
                capsule.center = new Vector3(0f, 1.0f, 0f);
                capsule.isTrigger = true;

                // Enemy — [RequireComponent(typeof(NavMeshAgent))] adds the agent.
                go.AddComponent(enemyType);
                // EnemyDamageable adapter — hero abilities + pets find IDamageable
                // through it (week4-hero-pets-gate.md item 1).
                if (enemyDamageableType != null)
                {
                    if (go.GetComponent(enemyDamageableType) == null)
                        go.AddComponent(enemyDamageableType);
                }
                else
                {
                    Debug.LogWarning("[VillageSceneBuilder] EnemyDamageable type not found -- " +
                                     "hero abilities + pets will not be able to hit enemies.");
                }

                // Tune the NavMeshAgent so it sits cleanly on the baked mesh.
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.radius = 0.4f;
                    agent.height = 2.0f;
                    agent.baseOffset = 0f;
                    agent.speed = 2.5f;            // EnemyDef overrides at Configure
                    agent.angularSpeed = 360f;
                    agent.acceleration = 24f;
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                if (prefab == null)
                    Debug.LogError($"[VillageSceneBuilder] Failed to save enemy prefab at '{prefabPath}'.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // =====================================================================
        //  Building prefabs — one per BuildingType, each with a Building
        // =====================================================================

        /// <summary>
        /// One built building prefab paired with its BuildingType ordinal — fed
        /// to BuildMenu's <c>_buildingPrefabs</c> list.
        /// </summary>
        private struct BuiltBuildingPrefab
        {
            public int TypeOrdinal;     // BuildingType enum ordinal
            public GameObject Prefab;
        }

        /// <summary>
        /// Builds (or refreshes) one placeable prefab per BuildingType, each
        /// carrying a <c>Building</c> component + the KayKit building mesh, and
        /// returns them. Fed into <c>BuildMenu._buildingPrefabs</c> so the build
        /// menu can place player-built buildings (week4-buildings.md item 5).
        /// Reuses the <see cref="Buildings"/> placement table for the mesh names.
        /// </summary>
        private static List<BuiltBuildingPrefab> EnsureBuildingPrefabs()
        {
            var result = new List<BuiltBuildingPrefab>();
            var buildingType = FindType(TypeBuilding);
            if (buildingType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.Building not found -- " +
                               "building prefabs skipped; the build menu will have no prefabs.");
                return result;
            }

            foreach (var b in Buildings)
            {
                string prefabPath = $"{GeneratedPrefabDir}/Building_{b.Id}.prefab";
                var go = new GameObject($"Building_{b.Id}");
                try
                {
                    var model = LoadModel(Building(b.Fbx));
                    GameObject visual = InstantiateModel(model,
                        b.Fbx + "_" + BuildingColor + ".fbx", $"{b.Fbx} -> {b.Label}");
                    visual.transform.SetParent(go.transform, false);
                    if (model == null)
                    {
                        visual.transform.localScale = new Vector3(3f, 3f, 3f);
                        visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                        ApplyColor(visual, b.PlaceholderColor);
                    }

                    // Building.EnsureBlocker() adds a BoxCollider at runtime; add
                    // one now so the saved prefab carries the footprint blocker.
                    var blocker = go.AddComponent<BoxCollider>();
                    blocker.size = new Vector3(3.2f, 3f, 3.2f);
                    blocker.center = new Vector3(0f, 1.5f, 0f);

                    // Building component — Configure(BuildingDef) is called at
                    // runtime by BuildMenu after the prefab is instantiated.
                    var comp = go.AddComponent(buildingType);
                    InvokeConfigure(comp, "Configure", b.Type, b.Id, b.Label);
                    // Pull HP / cost / display-name key from buildings.json so the
                    // saved prefab is data-correct even before BuildMenu re-Configures.
                    InvokeConfigure(comp, "ConfigureFromCatalog", b.Id);

                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    if (prefab != null)
                        result.Add(new BuiltBuildingPrefab { TypeOrdinal = b.Type, Prefab = prefab });
                    else
                        Debug.LogError($"[VillageSceneBuilder] Failed to save building prefab '{prefabPath}'.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            Debug.Log($"[VillageSceneBuilder] Built {result.Count}/5 building prefabs for the build menu.");
            return result;
        }

        // =====================================================================
        //  WaveManager
        // =====================================================================

        /// <summary>
        /// Adds the <c>WaveManager</c> sub-system GameObject and wires it to the
        /// Heart, the scene's WaveSpawnPoints and the enemy prefab. WaveManager
        /// also auto-finds the Heart + spawn points at Start, but wiring them
        /// here makes the scene self-describing (week4-waves.md item 2).
        /// </summary>
        private static void BuildWaveManager(Transform parent, Component heart, GameObject enemyPrefab)
        {
            var go = new GameObject("WaveManager");
            go.transform.SetParent(parent, false);

            var comp = AddVillageComponent(go, TypeWaveManager);
            if (comp == null) return;

            var so = new SerializedObject(comp);

            // _heart — the HeartController the enemies march toward.
            if (heart != null) SetObjectField(so, "_heart", heart);

            // _enemyRoot — a tidy parent for spawned enemies.
            var enemyRoot = NewChild(parent, "WaveEnemies");
            SetObjectField(so, "_enemyRoot", enemyRoot);

            // _enemyPrefab — typed `Enemy`; assign the prefab's Enemy component.
            if (enemyPrefab != null)
            {
                var enemyType = FindType(TypeEnemy);
                var enemyComp = enemyType != null ? enemyPrefab.GetComponent(enemyType) : null;
                if (enemyComp != null) SetObjectField(so, "_enemyPrefab", enemyComp);
            }

            // _apexBossPrefab — typed `DragonBoss`; assign the Boss_Dragon
            // prefab's DragonBoss component so the apex wave (waves.json wave 4,
            // "The Last Wing") can release the flying boss. The prefab is built
            // by DragonAnimatorSetup; a miss is non-fatal — the apex wave then
            // logs an error at runtime and clears (the loop never stalls).
            WireApexBossPrefab(so);

            // _spawnPoints — the list of WaveSpawnPoints already placed by the
            // approach-lane builder. Populate the serialized List<WaveSpawnPoint>.
            WireSpawnPointList(so, "_spawnPoints");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Loads the Boss_Dragon prefab and wires its <c>DragonBoss</c> component
        /// into <c>WaveManager._apexBossPrefab</c> (the field is typed
        /// <c>DragonBoss</c>). The prefab is produced by
        /// <c>DragonAnimatorSetup.BuildDragonBossPrefab</c> — if it has not been
        /// built yet the wiring is skipped with a warning; the apex wave then
        /// logs its own error at runtime rather than the loop stalling.
        /// </summary>
        private static void WireApexBossPrefab(SerializedObject so)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossDragonPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[VillageSceneBuilder] Boss_Dragon prefab not found at " +
                    $"'{BossDragonPrefabPath}' -- apex-boss wave will have no dragon. " +
                    "Run Defenders > Animation > Build Dragon Boss first.");
                return;
            }

            var dragonType = FindType(TypeDragonBoss);
            if (dragonType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.DragonBoss not found -- " +
                               "is the DeNelle.Village assembly compiled? Apex-boss prefab not wired.");
                return;
            }

            var dragonComp = prefab.GetComponent(dragonType);
            if (dragonComp == null)
            {
                Debug.LogError(
                    $"[VillageSceneBuilder] Boss_Dragon prefab at '{BossDragonPrefabPath}' " +
                    "carries no DragonBoss component -- apex-boss prefab not wired.");
                return;
            }

            SetObjectField(so, "_apexBossPrefab", dragonComp);
            Debug.Log("[VillageSceneBuilder] WaveManager._apexBossPrefab wired to Boss_Dragon " +
                      "(apex wave 'The Last Wing' will release Syndrath the Devourer).");
        }

        /// <summary>
        /// Fills a serialized <c>List&lt;WaveSpawnPoint&gt;</c> field with every
        /// WaveSpawnPoint component in the open scene.
        /// </summary>
        private static void WireSpawnPointList(SerializedObject so, string field)
        {
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[VillageSceneBuilder] Serialized list '{field}' not found / not an array " +
                                 $"on {so.targetObject.GetType().Name} -- spawn points left for auto-find.");
                return;
            }

            var spawnType = FindType(TypeWaveSpawnPoint);
            if (spawnType == null) return;

            var spawns = UnityEngine.Object.FindObjectsByType(
                spawnType, FindObjectsSortMode.None);
            prop.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
        }

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
        private static GameObject BuildHero(Transform parent, Component heart, Vector3 heartPos)
        {
            var go = new GameObject("Hero (Blaise)");
            go.transform.SetParent(parent, false);
            // Open plaza spot — far enough from every 4.5x-scaled building that
            // the OTS camera doesn't spawn looking into a wall. World coords
            // chosen against the building manifest at lines 928 / 1042-1095:
            // nearest neighbour (Tavern at 11,-8.5) is ~10 m away, comfortable.
            go.transform.position = new Vector3(6f, 0f, 4f);

            // Hero body — KayKit Protagonist_A.fbx (Mystery Series 5). The
            // primitive Capsule stays on the root as an INVISIBLE collider so
            // wall collision still works (the Protagonist mesh has its own
            // colliders, but those are stripped to keep nav clean).
            var collider = go.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.4f;
            collider.center = new Vector3(0f, 1f, 0f);

            // Tripo-generated Wizard FBX — comes with Walk + Cast animations
            // (owner paid for them, 2026-05-19). Wired with Wizard.controller
            // built by WizardAnimatorSetup.cs (Idle/Walk/Cast states + Speed
            // float + Cast trigger; matches HeroAbilities' Cast hash).
            const string HeroMeshPath = "Assets/Models/Wizard/Wizard.fbx";
            const string HeroAnimatorPath = "Assets/Models/Wizard/Wizard.controller";
            var heroModel = LoadModel(HeroMeshPath);
            GameObject body = null;
            if (heroModel != null)
            {
                body = (GameObject)PrefabUtility.InstantiatePrefab(heroModel);
            }
            if (body == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] Hero mesh '" + HeroMeshPath +
                                 "' not found — falling back to violet capsule placeholder.");
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                ApplyColor(body, HexColor("9d6fff"));
            }
            body.name = "HeroBody";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.zero;
            // KayKit Protagonist FBX imports at a non-uniform native size that
            // dwarfs the hero rig when dropped in raw. Normalise to ~2 m so the
            // OTS camera framing (tuned against the original capsule placeholder)
            // still reads correctly.
            if (heroModel != null) NormalizeProp(body, 2.0f);
            // Strip native KayKit colliders on the mesh; the hero-root capsule
            // collider above is the single source of truth for wall collision.
            StripColliders(body);
            // Strip any default Rigidbody the Tripo / KayKit import may have
            // dropped on the root — gravity on the hero pulled the wizard
            // through the village floor (2026-05-20 PO ticket).
            StripRigidbodies(body);

            // Wire the AnimatorController (built by WizardAnimatorSetup) so the
            // hero plays Idle / Walk / Cast. HeroLocomotion drives SetFloat
            // "Speed"; HeroAbilities already drives SetTrigger "Cast" (line 88).
            if (heroModel != null)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroAnimatorPath);
                if (ctrl != null)
                {
                    var anim = body.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.runtimeAnimatorController = ctrl;
                        anim.applyRootMotion = false;
                    }
                }
                else
                {
                    Debug.LogWarning("[VillageSceneBuilder] Wizard.controller not found at " +
                                     $"'{HeroAnimatorPath}' — run Defenders > Animation > " +
                                     "Setup Wizard Animator first.");
                }
            }

            // Hero faces +Z by default (toward the open plaza). Explicit reset
            // so HeroLocomotion's LookRotation chain starts from a known yaw.
            go.transform.rotation = Quaternion.identity;

            // Runtime body swap — replaces the Wizard placeholder with the
            // FBX matching the player's chosen HeroClass (Knight / Ranger).
            // No-op for Mage. Loads from Resources/Heroes/<slug>.fbx.
            AddVillageComponent(go, TypeHeroBodySwapper);

            // Walking input — WASD / arrows / dpad / left stick (new Input System).
            AddVillageComponent(go, TypeHeroLocomotion);

            var comp = AddVillageComponent(go, TypeHeroAbilities);
            if (comp == null) return go;

            // Ability input — 1/2/3/4 + gamepad face buttons → TryCast (Q/W/E/R
            // slots). 1-4 chosen over Q-W-E-R to avoid the W movement conflict.
            AddVillageComponent(go, TypeHeroAbilityInput);

            // Cinemachine rig DISABLED 2026-05-20: was putting the camera in
            // unexpected positions (hero appeared to fall off the world when
            // viewed from camera). Falling back to the hand-rolled
            // VillageCamera which we tuned earlier.
            // AddVillageComponent(go, TypeHeroCinemachineRig);

            var so = new SerializedObject(comp);
            // _heart — Healing Beacon (E) restores Heart HP.
            if (heart != null) SetObjectField(so, "_heart", heart);
            // _enemyMask — the ability hit-tests sweep only the Enemy layer.
            SetLayerMaskField(so, "_enemyMask", 1 << EnemyLayer);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // =====================================================================
        //  Ambient townsfolk (Workstream D) — wandering / idle KayKit villagers
        // =====================================================================

        /// <summary>
        /// One townsperson placement: a world spot, an archetype, whether it
        /// wanders, and a facing yaw for idlers.
        /// </summary>
        private struct TownsfolkSpot
        {
            public Vector3 Pos;
            public int Archetype;   // TownsfolkDialogue.Archetype ordinal
            public bool Wander;
            public float FacingY;
        }

        /// <summary>
        /// Populates the village with ambient townsfolk — KayKit civilian models
        /// carrying an <see cref="TypeAmbientNpc"/> component and a self-building
        /// <see cref="TypeTownsfolkBubble"/> word bubble. Some wander the baked
        /// NavMesh, some stand idle at authored spots. A <see cref="TypeTownsfolkController"/>
        /// hands every villager the Keeper transform for the proximity dialogue.
        ///
        /// <para>All townsfolk types live in DeNelle.Village and are wired by
        /// full-name reflection — the Editor asmdef cannot reference that module.
        /// Returns the count placed.</para>
        /// </summary>
        /// <param name="root">The VillageRoot transform.</param>
        /// <param name="heartPos">The Heart's world position — the plaza centre.</param>
        /// <param name="hero">The hero rig the townsfolk watch (may be null).</param>
        private static int BuildTownsfolk(Transform root, Vector3 heartPos, GameObject hero)
        {
            var npcType = FindType(TypeAmbientNpc);
            var bubbleType = FindType(TypeTownsfolkBubble);
            var controllerType = FindType(TypeTownsfolkController);
            if (npcType == null || bubbleType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.AmbientNPC / TownsfolkBubble " +
                               "not found -- is the DeNelle.Village assembly compiled? " +
                               "Ambient townsfolk skipped.");
                return 0;
            }

            var townsfolkRoot = NewChild(root, "Townsfolk");

            // The TownsfolkController coordinator — distributes the hero ref.
            Component controller = null;
            if (controllerType != null)
                controller = townsfolkRoot.gameObject.AddComponent(controllerType);
            else
                Debug.LogWarning("[VillageSceneBuilder] TownsfolkController type not found -- " +
                                 "townsfolk fall back to self-resolving the hero.");

            // ── Authored placements ──────────────────────────────────────────
            // Spots sit on the plaza / road network (on the baked NavMesh) and
            // around the named building quarters, so the village reads alive
            // from the camera. Archetype ordinals follow TownsfolkDialogue:
            //   0 Trader · 1 Villager · 2 Guard · 3 Child · 4 Elder
            // X grows east, Z grows north; the plaza is centred on the Heart.
            // Owner direction 2026-05-20: village feels crowded — cut the
            // ambient roster from 10 to 4. Keep one wanderer + one idler on
            // the plaza (the lively core), one off-duty guard near the gate
            // spine, and one trader at the market so each archetype still
            // appears once.
            var spots = new[]
            {
                // Plaza — the lively heart of the town.
                new TownsfolkSpot { Pos = heartPos + new Vector3( 4f, 0f,  5f), Archetype = 1, Wander = true,  FacingY = 200f },
                new TownsfolkSpot { Pos = heartPos + new Vector3( 2f, 0f, -6f), Archetype = 4, Wander = false, FacingY =   0f },
                // Market quarter (the church / market / tavern cluster).
                new TownsfolkSpot { Pos = new Vector3(-10f, 0f, -7f), Archetype = 0, Wander = false, FacingY =  90f },
                // Off-duty guard near the N gate spine.
                new TownsfolkSpot { Pos = new Vector3(  1f, 0f, 14f), Archetype = 2, Wander = true,  FacingY = 180f },
            };

            int placed = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                if (BuildOneTownsperson(townsfolkRoot, spots[i], i, npcType, bubbleType))
                    placed++;
            }

            // Hand the hero transform to the controller (it broadcasts to every
            // NPC on Start); the NPCs also self-resolve as a fallback.
            if (controller != null && hero != null)
                InvokeConfigure(controller, "SetHero", hero.transform);

            Debug.Log($"[VillageSceneBuilder] Ambient townsfolk placed -- {placed}/{spots.Length} " +
                      "villagers (wanderers + idlers) with engage-on-approach word bubbles.");
            return placed;
        }

        /// <summary>
        /// Builds a single ambient townsperson at <paramref name="spot"/>: a
        /// KayKit civilian model (round-robin over the four catalog civilians),
        /// an <c>AmbientNPC</c>, a <c>TownsfolkBubble</c> and — for a wanderer —
        /// a NavMeshAgent. Returns true on success.
        /// </summary>
        private static bool BuildOneTownsperson(Transform parent, TownsfolkSpot spot,
            int index, Type npcType, Type bubbleType)
        {
            var go = new GameObject($"Townsperson_{index:00}");
            go.transform.SetParent(parent, false);
            go.transform.position = spot.Pos;
            go.transform.rotation = Quaternion.Euler(0f, spot.FacingY, 0f);

            // KayKit civilian model — round-robin over Protagonist A/B + Helper
            // A/B (the catalog's named townsfolk stand-ins). Placeholder capsule
            // on a miss, matching the rest of the builder's fallback discipline.
            //
            // NOTE — InstantiateModel() force-assigns the shared MEDIEVAL HEX
            // ATLAS material (correct for the Hexagon-pack buildings, wrong for
            // a character). Townsfolk are instantiated directly here so the FBX
            // importer's own character materials/textures are kept intact.
            string modelPath = TownsfolkModelPaths[index % TownsfolkModelPaths.Length];
            var model = LoadModel(modelPath);
            GameObject visual;
            if (model != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (visual == null)
                {
                    visual = MakePlaceholderCube(
                        $"{Path.GetFileName(modelPath)} -> ambient townsperson");
                    model = null;   // fall through to the placeholder styling
                }
                else
                {
                    visual.name = model.name;
                }
            }
            else
            {
                visual = MakePlaceholderCube(
                    $"{Path.GetFileName(modelPath)} -> ambient townsperson");
            }
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;
            if (model == null)
            {
                // Placeholder body — a warm capsule so a missing model still
                // reads as a person standing in the town.
                visual.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
                visual.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                ApplyColor(visual, new Color(0.72f, 0.60f, 0.46f));
            }
            else
            {
                NormalizeProp(visual, 1.8f);   // size every civilian to ~human height
            }
            // The civilian mesh is decoration — its colliders must not block the
            // hero's tap-to-move raycast or shadow a structure ahead.
            StripColliders(visual);

            // AmbientNPC — the wander / idle + proximity-dialogue behaviour.
            var npc = go.AddComponent(npcType);

            // A NavMeshAgent for wanderers so they roam the baked village mesh.
            if (spot.Wander)
            {
                var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.radius = 0.34f;
                agent.height = 1.8f;
                agent.baseOffset = 0f;
                agent.speed = 1.6f;
                agent.angularSpeed = 240f;
                agent.acceleration = 12f;
                agent.stoppingDistance = 0.2f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType =
                    UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }

            // TownsfolkBubble — the self-building, billboarded word bubble.
            var bubble = go.AddComponent(bubbleType);

            // Wire the AmbientNPC's serialized fields + configure it.
            var so = new SerializedObject(npc);
            SetObjectField(so, "_bubble", bubble);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Configure(archetype, wander, homeAnchor) — homeAnchor is the spot.
            InvokeConfigure(npc, "Configure", spot.Archetype, spot.Wander, spot.Pos);
            // SetBubble is belt-and-braces in case the serialized wire is skipped.
            InvokeConfigure(npc, "SetBubble", bubble);

            return true;
        }

        // =====================================================================
        //  PetDeployer
        // =====================================================================

        /// <summary>
        /// Adds the <c>PetDeployer</c> GameObject, sets its Heart position +
        /// enemy LayerMask, and flips on its <c>_autoDeployOnStart</c> flag so it
        /// deploys the three starter pets itself on Start() — no separate runtime
        /// caller needed (week4-hero-pets-gate.md item 4).
        /// </summary>
        private static void BuildPetDeployer(Transform parent, Vector3 heartPos)
        {
            var go = new GameObject("PetDeployer");
            go.transform.SetParent(parent, false);

            var type = FindType(TypePetDeployer);
            if (type == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Pets.PetDeployer not found -- " +
                               "is the DeNelle.Pets assembly compiled? Pet deployer skipped.");
                return;
            }
            var comp = go.AddComponent(type);

            var so = new SerializedObject(comp);
            // _heartPosition — the centre of the pet deploy ring (a plain Vector3
            // so DeNelle.Pets never references DeNelle.Village).
            var heartProp = so.FindProperty("_heartPosition");
            if (heartProp != null) heartProp.vector3Value = heartPos;
            // _enemyMask — pets hunt only the Enemy layer.
            SetLayerMaskField(so, "_enemyMask", 1 << EnemyLayer);
            // _autoDeployOnStart — the deployer runs DeployStarterPets() itself.
            var autoProp = so.FindProperty("_autoDeployOnStart");
            if (autoProp != null) autoProp.boolValue = true;
            else Debug.LogWarning("[VillageSceneBuilder] PetDeployer._autoDeployOnStart not found -- " +
                                  "pets will not auto-deploy; call DeployStarterPets() at runtime.");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        //  BuildMenu UIDocument
        // =====================================================================

        /// <summary>
        /// Builds the build-menu <c>UIDocument</c> GameObject: a UIDocument whose
        /// source asset is <c>BuildMenu.uxml</c> plus the <c>BuildMenu</c>
        /// component, with the build camera, ground LayerMask and the five
        /// building prefabs wired (week4-buildings.md items 1, 3, 4, 5). The
        /// panel hides itself in OnEnable until the HUD's Build button calls
        /// Open() (item 2 — the HUD button wire — is left to the HUD pass).
        /// </summary>
        private static void BuildBuildMenu(Transform parent, List<BuiltBuildingPrefab> buildingPrefabs)
        {
            var go = new GameObject("BuildMenu");
            go.transform.SetParent(parent, false);

            // UIDocument needs a PanelSettings asset; reuse the project's if one
            // exists. Without it the document still serializes — the integrator
            // can assign PanelSettings in the inspector.
            var uiDoc = go.AddComponent<UIDocument>();
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(BuildMenuUxmlPath);
            if (uxml != null)
            {
                uiDoc.visualTreeAsset = uxml;
            }
            else
            {
                Debug.LogWarning($"[VillageSceneBuilder] BuildMenu.uxml not found at '{BuildMenuUxmlPath}' -- " +
                                 "assign the UIDocument source asset in the inspector.");
            }
            WirePanelSettings(uiDoc);

            // BuildMenu component — [RequireComponent(typeof(UIDocument))] is
            // already satisfied.
            var comp = AddVillageComponent(go, TypeBuildMenu);
            if (comp == null) return;

            var so = new SerializedObject(comp);
            // _document — the UIDocument on this GameObject.
            SetObjectField(so, "_document", uiDoc);
            // _buildCamera — leave blank: BuildMenu.Awake() defaults to Camera.main
            //   (the village Main Camera). Explicitly wiring it would need a
            //   FindObjectOfType<Camera>; the default is the documented behaviour.
            // _groundMask — the placement raycast must hit only the ground. The
            //   village ground tiles are on the Default layer (layer 0); restrict
            //   the mask to Default so the raycast does not snag on buildings.
            SetLayerMaskField(so, "_groundMask", 1 << 0);

            // _buildingPrefabs — the serialized List<BuildingPrefabEntry>. Each
            // entry is a struct { BuildingType Type; GameObject Prefab; }.
            WireBuildingPrefabList(so, "_buildingPrefabs", buildingPrefabs);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Assigns a PanelSettings asset to a UIDocument — finds the first one in
        /// the project. UI Toolkit needs PanelSettings to render; without it the
        /// menu is invisible. No-op (with a warning) when none exists.
        /// </summary>
        private static void WirePanelSettings(UIDocument uiDoc)
        {
            if (uiDoc == null || uiDoc.panelSettings != null) return;
            var guids = AssetDatabase.FindAssets("t:PanelSettings");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(path);
                if (panel != null) { uiDoc.panelSettings = panel; return; }
            }
            Debug.LogWarning("[VillageSceneBuilder] No PanelSettings asset found -- assign one to the " +
                             "BuildMenu UIDocument in the inspector or the build menu will not render.");
        }

        /// <summary>
        /// Fills the serialized <c>List&lt;BuildingPrefabEntry&gt;</c> on BuildMenu
        /// — one entry per built building prefab, each with its BuildingType
        /// ordinal + the prefab reference.
        /// </summary>
        private static void WireBuildingPrefabList(SerializedObject so, string field,
            List<BuiltBuildingPrefab> prefabs)
        {
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[VillageSceneBuilder] Serialized list '{field}' not found / not an array " +
                                 $"on {so.targetObject.GetType().Name} -- building prefabs not wired.");
                return;
            }

            prop.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                // BuildingPrefabEntry struct fields: `Type` (enum) + `Prefab` (GO).
                var typeProp = element.FindPropertyRelative("Type");
                var prefabProp = element.FindPropertyRelative("Prefab");
                if (typeProp != null) typeProp.enumValueIndex = prefabs[i].TypeOrdinal;
                if (prefabProp != null) prefabProp.objectReferenceValue = prefabs[i].Prefab;
            }
        }

        // =====================================================================
        //  NavMesh bake — legacy UnityEditor.AI API (com.unity.modules.ai)
        // =====================================================================

        /// <summary>
        /// Marks the village ground + wall + building geometry navigation-static
        /// and bakes a NavMesh for the open Village scene. Uses the legacy
        /// <c>UnityEditor.AI.NavMeshBuilder</c> API — the manifest carries
        /// <c>com.unity.modules.ai</c>, NOT the high-level
        /// <c>com.unity.ai.navigation</c> package (week4-waves.md item 1).
        ///
        /// REQUIRED for the wave loop: <c>Enemy</c> uses a NavMeshAgent and
        /// cannot move without a baked NavMesh.
        /// </summary>
        private static void BakeVillageNavMesh(GameObject root)
        {
            int marked = 0;

            // Mark the walkable / obstacle geometry navigation-static. Renderers
            // under Ground / Roads / Approaches are the walkable floor; Walls /
            // Buildings are obstacles the agents path around. Marking by
            // GameObjectUtility static flags is what NavMeshBuilder reads.
            //
            // WO-27 fix: "Gates" is DELIBERATELY excluded. The KayKit gate arch
            // (wall_straight_gate, scaled 4.5x) would voxelize into a navmesh wall
            // across the opening, leaving the route blocked at every gate -- so an
            // enemy could batter a gate down and STILL have no navmesh path through
            // to the Heart. Enemies are held at a CLOSED gate by gameplay instead
            // (Enemy.ProbeForStructure hits the Gate's blocker BoxCollider, which
            // has no renderer and so never affects the bake); when the gate is
            // destroyed they resume onto the continuous navmesh and pour through.
            // The opening therefore must stay WALKABLE in the bake.
            string[] navStaticRoots =
            {
                "Ground", "Roads", "Approaches", "Walls", "Buildings",
            };
            foreach (var rootName in navStaticRoots)
            {
                var sub = root.transform.Find(rootName);
                if (sub == null) continue;
                foreach (var r in sub.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                        flags | StaticEditorFlags.NavigationStatic);
                    marked++;
                }
            }

            // Bake the open Village scene. The legacy UnityEditor.AI.NavMeshBuilder
            // bakes the ACTIVE scene synchronously using the project's default
            // agent settings (Window > AI > Navigation > Agents). ClearAllNavMeshes
            // first keeps a re-run idempotent. The skeleton enemy's NavMeshAgent
            // (radius 0.4, height 2.0) sits inside the Unity default Humanoid
            // agent (radius 0.5, height 2.0) — the bake is valid for it.
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            // ── Static-batching pass (mobile-perf audit P0-4 / §2.1) ─────────
            // The NavMesh bake above flags only NavigationStatic — which leaves
            // static batching OFF for all ~2,930 village instances. The audit's
            // headline mobile risk is draw-call submission: 2,600+ ground tiles
            // issuing 2,600+ draws per frame. Flagging BatchingStatic lets Unity
            // merge identical static meshes at scene load, collapsing the draw
            // count. Done AFTER the bake so NavigationStatic is already set; we
            // OR the flag in so the existing NavigationStatic bit is preserved.
            MarkStaticBatchingAndInstancing();

            Debug.Log($"[VillageSceneBuilder] NavMesh baked -- {marked} renderer object(s) " +
                      "marked Navigation Static (Ground/Roads/Approaches walkable, " +
                      "Walls/Buildings obstacles; Gates left WALKABLE so enemies path " +
                      "through once destroyed). Legacy UnityEditor.AI synchronous bake.");
        }

        // =====================================================================
        //  Static batching + GPU instancing (mobile-perf audit P0-4 / §2.1)
        // =====================================================================

        /// <summary>
        /// Flags the static village geometry <c>BatchingStatic</c> and enables
        /// GPU instancing on the repeated-prop materials — the audit's P0-4
        /// draw-call fix (recommendations 2 and 3 in §2.1).
        ///
        /// <para><b>BatchingStatic.</b> ORed onto every renderer under the
        /// static-geometry roots (Ground / Walls / Gates / Roads / Buildings /
        /// Centerpieces / CityDressing / Approaches). The OR preserves the
        /// <c>NavigationStatic</c> bit <see cref="BakeVillageNavMesh"/> already
        /// set. Static batching merges identical static meshes at scene load,
        /// trading a little memory (duplicated vertex data) for a large drop in
        /// draw-call count — acceptable for the flat tile grid.</para>
        ///
        /// <para><b>GPU instancing.</b> Enabled on the shared materials of the
        /// CityDressing props/fences/trees. Instancing covers repeated
        /// mesh+material draws that static batching does NOT merge (e.g. props
        /// the batcher leaves separate), and is the §2.1-recommendation-3 path.
        /// Instancing only kicks in when the per-instance data is uniform — the
        /// builder's per-tile <c>ApplyColor</c> recolouring can break a batch,
        /// so this is best-effort: it flips the material flag, and the audit
        /// note in port-notes/mobile-settings.md flags the per-instance-colour
        /// follow-up.</para>
        /// </summary>
        private static void MarkStaticBatchingAndInstancing()
        {
            var root = GameObject.Find(VillageRootName);
            if (root == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] BatchingStatic pass skipped -- " +
                                 "VillageRoot not found.");
                return;
            }

            // Every root that holds STATIC village geometry. Superset of the
            // nav-static roots: also includes Centerpieces + CityDressing, which
            // are static set-dressing the NavMesh bake did not touch.
            string[] staticGeometryRoots =
            {
                "Ground", "Walls", "Gates", "Roads", "Buildings",
                "Centerpieces", "CityDressing", "Approaches",
            };

            int batched = 0;
            foreach (var rootName in staticGeometryRoots)
            {
                var sub = root.transform.Find(rootName);
                if (sub == null) continue;
                foreach (var r in sub.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    // OR the bit in — do NOT clobber NavigationStatic (set by
                    // BakeVillageNavMesh) or any other flag already present.
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                        flags | StaticEditorFlags.BatchingStatic);
                    batched++;
                }
            }

            // GPU instancing on the repeated CityDressing prop/fence/tree
            // materials (audit §2.1 recommendation 3). The dressing root holds
            // the props/fences/trees that share mesh+material; flipping
            // enableInstancing lets URP/Lit draw them instanced.
            int instanced = EnableInstancingOnDressingMaterials(root);

            Debug.Log($"[VillageSceneBuilder] Static-batching pass -- {batched} renderer object(s) " +
                      "flagged BatchingStatic (OR-ed onto the existing NavigationStatic bit); " +
                      $"GPU instancing enabled on {instanced} dressing material(s). " +
                      "Mobile-perf audit P0-4 / §2.1.");
        }

        /// <summary>
        /// Sets <c>Material.enableInstancing</c> on every distinct shared
        /// material under the CityDressing root. Returns the count flipped.
        /// </summary>
        private static int EnableInstancingOnDressingMaterials(GameObject root)
        {
            var dressing = root.transform.Find("CityDressing");
            if (dressing == null) return 0;

            var seen = new HashSet<Material>();
            int flipped = 0;
            foreach (var r in dressing.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !seen.Add(mat)) continue;
                    if (!mat.enableInstancing)
                    {
                        mat.enableInstancing = true;
                        EditorUtility.SetDirty(mat);
                        flipped++;
                    }
                }
            }
            return flipped;
        }

        // =====================================================================
        //  Week-4 reflection / wiring helpers
        // =====================================================================

        /// <summary>
        /// Sets a serialized <c>LayerMask</c> field. A LayerMask SerializedProperty
        /// is backed by an int — <c>intValue</c> carries the mask bits.
        /// </summary>
        private static void SetLayerMaskField(SerializedObject so, string field, int mask)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[VillageSceneBuilder] LayerMask field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} -- mask not set.");
                return;
            }
            prop.intValue = mask;
        }

        /// <summary>Recursively sets the layer on a GameObject and all its descendants.</summary>
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
