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

            // ── Build Settings ───────────────────────────────────────────────
            EnsureBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VillageSceneBuilder] BuildVillage complete -- " +
                      $"{_groundCount} ground tiles, {wallCount} wall sections/corners, " +
                      $"{gateCount} cardinal gates, {_roadCount} plaza/road tiles, " +
                      $"{buildingCount} gameplay buildings, {_dressingCount} dressing buildings, " +
                      $"{_propCount} props/fences, {spawnCount} wave spawn points, " +
                      $"1 Elarion + 1 Keep. " +
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

                if (model != null && !corner)
                {
                    // KayKit wall_straight is a fixed-length module; scale its
                    // long (local X) axis so the section fills its computed run
                    // length. (Corner pieces stay at native scale.)
                    float baseLen = MeasureLocalLength(visual);
                    if (baseLen > 0.01f)
                    {
                        var s = visual.transform.localScale;
                        s.x *= length / baseLen;
                        visual.transform.localScale = s;
                    }
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

                // The violet shimmer plane — a thin emissive quad in the opening.
                // (Week-3 visual stand-in; the shimmer shader lands Week 4.)
                // Must take the SAME yaw correction as the gate model above, or
                // the shimmer sits 90deg across the opening instead of filling it.
                var shimmer = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shimmer.name = "ForceFieldShimmer";
                shimmer.transform.SetParent(go.transform, false);
                shimmer.transform.localPosition = new Vector3(0f, 1.7f, 0f);
                shimmer.transform.localRotation = Quaternion.Euler(0f, WallStraightYawFix, 0f);
                shimmer.transform.localScale = new Vector3(GateHalfWidthConst * 2f, 3.0f, 0.08f);
                UnityEngine.Object.DestroyImmediate(shimmer.GetComponent<Collider>());
                ApplyEmissive(shimmer, new Color(0.61f, 0.44f, 1f), 1.4f);

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
            for (int row = -3; row <= 3; row++)
            {
                bool oddRow = (row & 1) != 0;
                float z = row * HexDepth;
                float xShift = oddRow ? HexWidth * 0.5f : 0f;
                for (int col = -4; col <= 4; col++)
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
            float[] lateral = { -HexWidth * 0.5f, HexWidth * 0.5f };
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
            // Centre-west of the plaza (§7.3) so the two anchors frame it.
            Vector3 site = new Vector3(-6f, 0f, 1f);

            var go = new GameObject("Heart (Elarion)");
            go.transform.SetParent(parent, false);
            go.transform.position = site;

            // ── Raised mound (spec §3.1 -- a slight elevation) ───────────────
            var mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mound.name = "ElarionMound";
            mound.transform.SetParent(go.transform, false);
            mound.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            mound.transform.localScale = new Vector3(5.0f, 0.35f, 5.0f);
            ApplyColor(mound, new Color(0.34f, 0.42f, 0.24f)); // mossy grass mound

            // ── The tree itself ──────────────────────────────────────────────
            // No KayKit Forest/Nature pack is imported, so the Hexagon pack's
            // own largest tree mesh stands in for Elarion, scaled up (§3.1
            // fallback + §13 decisions log entry).
            var treeModel = LoadModel(HexDecoNature + "trees_A_large.fbx");
            GameObject tree;
            if (treeModel != null)
            {
                tree = InstantiateModel(treeModel, "trees_A_large.fbx", "ElarionTree");
                tree.name = "ElarionTree";
                tree.transform.SetParent(go.transform, false);
                tree.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                tree.transform.localScale = Vector3.one * 3.0f; // ~3× normal (§3.1)
                // The hex atlas will not resolve onto trees_A_large (renders
                // white even force-assigned — same mesh/UV quirk as the standing
                // stones). Flat-tint it a deep canopy green so the centerpiece
                // reads as a world-tree; the violet veins still glow over it.
                ApplyColorAll(tree, new Color(0.24f, 0.42f, 0.22f));
                Debug.Log("[VillageSceneBuilder] Elarion uses KayKit Hexagon-pack " +
                          "decoration/nature/trees_A_large.fbx scaled 3x, flat-tinted " +
                          "(atlas does not resolve onto this mesh) -- spec §3.1/§13.");
            }
            else
            {
                tree = new GameObject("[PLACEHOLDER] Elarion world-tree");
                NotePlaceholder("Elarion world-tree");
                tree.transform.SetParent(go.transform, false);
                tree.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                var trunk = PrimitiveChild(tree.transform, "Trunk", PrimitiveType.Cylinder,
                    new Vector3(0f, 4f, 0f), new Vector3(1.4f, 4f, 1.4f),
                    new Color(0.30f, 0.21f, 0.13f));
                var canopy = PrimitiveChild(tree.transform, "Canopy", PrimitiveType.Sphere,
                    new Vector3(0f, 9f, 0f), new Vector3(7f, 6f, 7f),
                    new Color(0.24f, 0.42f, 0.22f));
                _ = trunk; _ = canopy;
            }

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

            var heartComp = AddVillageComponent(go, TypeHeartController);
            // HeartController.Awake() would snap the transform to origin + apply
            // its own scale. We host Elarion centre-WEST of the plaza beside the
            // Keep (spec §3.3) and scale the tree mesh itself, so tell the
            // controller to keep the authored transform.
            if (heartComp != null)
            {
                var so = new SerializedObject(heartComp);
                var prop = so.FindProperty("_useAuthoredTransform");
                if (prop != null) { prop.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
                else Debug.LogWarning("[VillageSceneBuilder] HeartController._useAuthoredTransform not found -- Elarion may snap to origin at Play.");
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
            Vector3 site = new Vector3(6f, 0f, -3f); // ~SE of Elarion (§3.2)

            var go = new GameObject("KeepersKeep");
            go.transform.SetParent(parent, false);
            go.transform.position = site;
            go.transform.rotation = Quaternion.Euler(0f, 200f, 0f); // face the plaza

            var castle = LoadModel(Building("building_castle"));
            GameObject visual = InstantiateModel(castle, "building_castle_green.fbx",
                "Keeper's Keep");
            visual.transform.SetParent(go.transform, false);
            if (castle == null)
            {
                visual.transform.localScale = new Vector3(4f, 4f, 4f);
                visual.transform.localPosition = new Vector3(0f, 2f, 0f);
                ApplyColor(visual, new Color(0.55f, 0.52f, 0.48f));
            }
            else
            {
                // building_castle renders untextured (white) — the hex atlas
                // does not resolve onto this mesh. Flat-tint it a warm keep-stone
                // so the Keeper's Keep reads as built stone (see decisions log).
                ApplyColorAll(visual, new Color(0.60f, 0.57f, 0.50f));
            }

            // Violet banner beside the Keep (§3.2 -- recolour a KayKit flag).
            var flag = LoadModel(HexDecoProps + "flag_blue.fbx");
            var banner = InstantiateModel(flag, "flag_blue.fbx", "Avalon Banner");
            banner.name = "AvalonBanner";
            banner.transform.SetParent(go.transform, false);
            banner.transform.localPosition = new Vector3(-2.6f, 0f, 1.6f);
            if (flag != null)
                ApplyEmissive(banner, HexColor("9d6fff"), 0.25f); // tint toward violet
            else
            {
                banner.transform.localScale = new Vector3(0.3f, 3.2f, 0.3f);
                banner.transform.localPosition += Vector3.up * 1.6f;
                ApplyColor(banner, HexColor("9d6fff"));
            }
            _propCount++;
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
        }

        // Quadrant placements per spec §5. N = +Z. Curtain wall is [-28..+28] X,
        // [-21..+21] Z (south bows to -25). Buildings sit on 2×2-hex plots.
        private static readonly BuildingPlacement[] Buildings =
        {
            // Crystal Mine — Northwest rocky district (§5).
            new BuildingPlacement { Type = 0, Id = "crystal-mine", Label = "Crystal Mine",
                X = -17f, Z = 12.5f, YawDeg = 135f, Fbx = "building_mine",
                PlaceholderColor = new Color(0.38f, 0.65f, 0.98f), FenceKind = "stone" },
            // Pet House — Southwest creek-side (§5).
            new BuildingPlacement { Type = 1, Id = "pet-house", Label = "Pet House",
                X = -17f, Z = -10.5f, YawDeg = 55f, Fbx = "building_stables",
                PlaceholderColor = new Color(0.98f, 0.82f, 0.48f), FenceKind = "wood" },
            // Arcane Tower — South-central, near the Keep (§5).
            new BuildingPlacement { Type = 2, Id = "arcane-tower", Label = "Arcane Tower",
                X = 6f, Z = -12.5f, YawDeg = 0f, Fbx = "building_tower_A",
                PlaceholderColor = new Color(0.65f, 0.55f, 0.98f), FenceKind = "stone" },
            // Workshop — Northeast artisan district (§5).
            new BuildingPlacement { Type = 3, Id = "workshop", Label = "Workshop",
                X = 16f, Z = 12.5f, YawDeg = 215f, Fbx = "building_workshop",
                PlaceholderColor = new Color(1f, 0.60f, 0.32f), FenceKind = "wood" },
            // Farm — East open ground (§5). Windmill mesh.
            new BuildingPlacement { Type = 4, Id = "farm", Label = "Farm",
                X = 19f, Z = -1f, YawDeg = 270f, Fbx = "building_windmill",
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
                    float baseLen = MeasureLocalLength(f);
                    if (baseLen > 0.01f)
                    {
                        var s = f.transform.localScale;
                        s.x *= span / baseLen;
                        f.transform.localScale = s;
                    }
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
            // ── §6.1 Residential cluster (SW) — homes around a well ──────────
            var residential = NewChild(parent, "Residential-SW");
            var residentialDefs = new[]
            {
                new DressDef { Name = "Home-A1", Fbx = "building_home_A", X = -22f, Z = -6f, Yaw = 70f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-A2", Fbx = "building_home_A", X = -22f, Z = -12f, Yaw = 95f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-A3", Fbx = "building_home_A", X = -10f, Z = -16f, Yaw = 160f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-B1", Fbx = "building_home_B", X = -16f, Z = -16f, Yaw = 200f, PlaceholderColor = C("c9b48a") },
                new DressDef { Name = "Home-B2", Fbx = "building_home_B", X = -23f, Z = -16f, Yaw = 25f, PlaceholderColor = C("c9b48a") },
                new DressDef { Name = "Home-B3", Fbx = "building_home_B", X = -10.5f, Z = -10f, Yaw = 120f, PlaceholderColor = C("c9b48a") },
            };
            foreach (var d in residentialDefs) PlaceDressing(residential, d, false);
            // The well at the cluster's centre (§6.1).
            PlaceDressing(residential,
                new DressDef { Name = "Well", Fbx = "building_well", X = -16.5f, Z = -11.5f, Yaw = 0f, PlaceholderColor = C("8aa0b0") },
                false);

            // ── §6.2 Market quarter (around the plaza, south) ────────────────
            var market = NewChild(parent, "Market-S");
            // Market on the plaza's south side.
            PlaceDressing(market,
                new DressDef { Name = "Market", Fbx = "building_market", X = -3f, Z = -9f, Yaw = 10f, PlaceholderColor = C("c98f4a") },
                false);
            // Tavern on the plaza's SE corner.
            PlaceDressing(market,
                new DressDef { Name = "Tavern", Fbx = "building_tavern", X = 11f, Z = -8.5f, Yaw = 250f, PlaceholderColor = C("b5793c") },
                false);
            // Church on the plaza's north side — small, not competing (§6.2).
            PlaceDressing(market,
                new DressDef { Name = "Church", Fbx = "building_church", X = -2f, Z = 9.5f, Yaw = 185f, PlaceholderColor = C("d7d2c4") },
                false);

            // ── §6.3 Workshop quarter (NE) — blacksmith + townhall ───────────
            var workshopQ = NewChild(parent, "Workshop-NE");
            // Blacksmith adjacent to the Workshop building (Workshop is at +16,+12.5).
            PlaceDressing(workshopQ,
                new DressDef { Name = "Blacksmith", Fbx = "building_blacksmith", X = 22.5f, Z = 9f, Yaw = 230f, PlaceholderColor = C("8a7d6a") },
                false);
            // Townhall on the NE corner of the plaza (small civic building).
            PlaceDressing(workshopQ,
                new DressDef { Name = "Townhall", Fbx = "building_townhall", X = 11f, Z = 8.5f, Yaw = 200f, PlaceholderColor = C("c2b79a") },
                false);
            // A small fenced workshop yard between Workshop + Blacksmith, with
            // anvil / lumber / tool props (§6.3).
            BuildWorkshopYard(workshopQ, new Vector3(19.5f, 0f, 9.5f));

            // ── §6.4 Farm / orchard (E) — orchard tiles + farmer's hut ───────
            var orchard = NewChild(parent, "Orchard-E");
            BuildOrchard(orchard, new Vector3(19f, 0f, -1f));
            // Farmer's hut on the orchard's edge (§6.4 -- a building_home_A).
            PlaceDressing(orchard,
                new DressDef { Name = "FarmersHut", Fbx = "building_home_A", X = 23f, Z = -10f, Yaw = 290f, PlaceholderColor = C("d8c69a") },
                false);

            // ── §6.5 Northern open ground — a wayside shrine ─────────────────
            var northern = NewChild(parent, "Northern-OpenGround");
            PlaceDressing(northern,
                new DressDef { Name = "Shrine", Fbx = "building_shrine", X = 0.5f, Z = 16.5f, Yaw = 180f, PlaceholderColor = C("b9b0a0") },
                false);
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

            // Props — KayKit decoration/props.
            PlaceProp(yard.transform, HexDecoProps + "resource_lumber.fbx",
                new Vector3(-1.2f, 0f, 0.8f), 20f, "lumber");
            PlaceProp(yard.transform, HexDecoProps + "resource_stone.fbx",
                new Vector3(1.3f, 0f, -0.7f), -35f, "stone");
            PlaceProp(yard.transform, HexDecoProps + "barrel.fbx",
                new Vector3(0.9f, 0f, 1.1f), 0f, "barrel");
            PlaceProp(yard.transform, HexDecoProps + "weaponrack.fbx",
                new Vector3(-1.0f, 0f, -1.2f), 90f, "weaponrack");
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
                        t.transform.localScale = Vector3.one * 1.2f;
                    else
                    {
                        t.transform.localScale = new Vector3(1.4f, 2.6f, 1.4f);
                        t.transform.localPosition += Vector3.up * 1.3f;
                        ApplyColor(t, C("4f7a3a"));
                    }
                    _propCount++;
                }
            }
            // Haybales at the orchard edge.
            PlaceProp(orchardRoot.transform, HexDecoProps + "haybale.fbx",
                new Vector3(4.5f, 0f, -7f), 25f, "haybale");
            PlaceProp(orchardRoot.transform, HexDecoProps + "haybale.fbx",
                new Vector3(-5f, 0f, -8.5f), -40f, "haybale");
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
                    t.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.3f, (i % 3) / 3f);
                else
                {
                    t.transform.localScale = new Vector3(1.3f, 2.6f, 1.3f);
                    t.transform.localPosition += Vector3.up * 1.3f;
                    ApplyColor(t, C("3f6e34"));
                }
                _propCount++;
            }
        }

        /// <summary>Instantiates one KayKit prop at a local position; placeholder on miss.</summary>
        private static void PlaceProp(Transform parent, string assetPath, Vector3 localPos,
            float yaw, string label)
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
            _propCount++;
        }

        // =====================================================================
        //  Approach lanes + wave spawn points (§8)
        // =====================================================================

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

                // 5 hexes of paved road extending outward (§8.1), 2 tiles wide.
                Vector3 lateral = new Vector3(-outward.z, 0f, outward.x); // perpendicular
                for (int i = 1; i <= 5; i++)
                {
                    Vector3 along = gatePos + outward * (i * step);
                    foreach (var lat in new[] { -HexWidth * 0.5f, HexWidth * 0.5f })
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

                // Light foliage flanking the lane (§8.1) — a couple of trees /
                // rocks per side.
                var foliageModel = LoadModel(HexDecoNature + "tree_single_B.fbx");
                var rockModel = LoadModel(HexDecoNature + "rock_single_A.fbx");
                for (int i = 1; i <= 4; i += 2)
                {
                    Vector3 along = gatePos + outward * (i * step);
                    var tL = InstantiateModel(foliageModel, "tree_single_B.fbx", "approach tree");
                    tL.transform.SetParent(laneRoot.transform, false);
                    tL.transform.position = along + lateral * 3.4f;
                    var rR = InstantiateModel(rockModel, "rock_single_A.fbx", "approach rock");
                    rR.transform.SetParent(laneRoot.transform, false);
                    rR.transform.position = along - lateral * 3.4f;
                    if (foliageModel == null) { ApplyColor(tL, C("3f6e34")); tL.transform.position += Vector3.up; }
                    if (rockModel == null) { ApplyColor(rR, C("8a8780")); rR.transform.position += Vector3.up * 0.4f; }
                    _propCount += 2;
                }

                // The wave-spawn zone — a 3×3 hex grass plot, 5 hexes out (§8.1 / §8.3).
                Vector3 spawnCentre = gatePos + outward * (7f * step);
                var zoneRoot = new GameObject($"SpawnZone-{direction}");
                zoneRoot.transform.SetParent(laneRoot.transform, false);
                zoneRoot.transform.position = spawnCentre;
                for (int gx = -1; gx <= 1; gx++)
                {
                    for (int gz = -1; gz <= 1; gz++)
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
            // Lifted, pitched-down framing taking in the whole shaped town.
            cameraGo.transform.position = new Vector3(0f, 46f, -58f);
            cameraGo.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            cameraGo.AddComponent<AudioListener>();
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

        /// <summary>Renderer-bounds length along local X of a model instance (for run-fit scaling).</summary>
        private static float MeasureLocalLength(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return 0f;
            bool any = false;
            Bounds b = default;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b.size.x : 0f;
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
    }
}
