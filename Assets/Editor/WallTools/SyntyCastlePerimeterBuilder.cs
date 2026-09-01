// =============================================================================
// SyntyCastlePerimeterBuilder — the castle perimeter as MODULES, at native scale.
// -----------------------------------------------------------------------------
// WO-1290 (owner ruling 2026-09-01: full Synty re-theme, walls at NATIVE height,
// zero scaling). Replaces the CastleWallsFromRecipe stretch path.
//
// ⛔ WHY THE OLD PATH IS RETIRED — measured 2026-09-01, not inferred (CLAUDE.md §12).
// castle-south-recipe.json is FOUR pieces mirrored x4: 20 objects for the whole castle.
// The source mesh SM_Wall_Medieval_Stone measures 15.75 x 8.49 x 2.39m, and the recipe
// places it at scale.x 1.62 and 1.95 — so one 15.75m mesh renders at 25.5m and 30.7m,
// i.e. every stone and merlon stretched 62% and 95% wider than authored, differently on
// the two halves of the same wall. The seam filler adds a THIRD arbitrary scaleX
// (gap / baseWallLen), and CornerTower_South scales a ROUND tower 1.28 on X only — an
// ellipse. Non-uniform scale also breaks the normal-map tangent basis, so the three
// pieces light differently. Every "seam fix" in that file adds another stretch factor:
// its unit of construction is a SCALED SLAB, so it cannot produce a good wall.
//
// THE REPLACEMENT: Synty POLYGON Fantasy Kingdom castle modules on their native 5m grid,
// every instance at scale (1,1,1). Measured from the FBX (Synty is cm, 500 units = 5.00m):
//   SM_Bld_Castle_Wall_01        5.00 x 5.00 x 0.50m    20 tris
//   SM_Bld_Castle_Battlements_01 5.00 x 1.38 x 0.50m   146 tris   (caps the wall)
//   SM_Bld_Castle_Wall_Gate_01   5.67 x 5.86 x 1.26m  2530 tris
//   SM_Bld_Castle_Wall_Tower_M_01 3.05 dia x 7.52m     608 tris
// A full ring is ~15k tris. The retired path is ~8k for 20 stretched slabs; the
// GridWallBuilder/Tripo path is 1.28M. This is better-looking AND cheaper than both.
//
// ⚠ THE PACK IS GITIGNORED, DELIBERATELY — same policy as polyperfect/Quaternius/Blink
// (.gitignore "Synty POLYGON" block). This builder references Assets/Synty/... directly,
// exactly as CastleWallsFromRecipe referenced the gitignored polyperfect pack. A fresh
// clone re-imports the pack from the Asset Store. A missing prefab is a LogWarning and a
// skipped piece, never an exception (CLAUDE.md §4).
//
// ⚠ NO Shader.Find HERE. It returns NULL in batchmode (CastleHubBuilder.cs:2549). The
// Synty prefabs already carry their URP material (Generic_Basic.shadergraph, which
// declares a UniversalTarget + UniversalLitSubTarget), so there is nothing to re-shader
// and no magenta pass to run.
//
// GEOMETRY — the owner's 2026-06-13 CoC algorithm, unchanged and already proven in
// PerimeterWallGenerator: place 4 corner towers via the 90/180/270 mirror about the Heart
// at world origin, span each side between the tower inner ends, force an ODD slot count so
// the single CENTRE slot is a natural gate. Only the art and the module pitch change.
//
// Batchmode: DeNelle.Editor.SyntyCastlePerimeterBuilder.BuildInHub
// Menu:      Defenders/Walls/Build Synty Castle Perimeter
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SyntyCastlePerimeterBuilder
    {
        private const string SyntyCastle =
            "Assets/Synty/PolygonFantasyKingdom/Prefabs/Castle/";

        private const string WallPrefab        = SyntyCastle + "SM_Bld_Castle_Wall_01.prefab";
        private const string ArrowslitPrefab   = SyntyCastle + "SM_Bld_Castle_Wall_Arrowslit_01.prefab";
        private const string BattlementsPrefab = SyntyCastle + "SM_Bld_Castle_Battlements_01.prefab";
        private const string GatePrefab        = SyntyCastle + "SM_Bld_Castle_Wall_Gate_01.prefab";
        private const string TowerPrefab       = SyntyCastle + "SM_Bld_Castle_Wall_Tower_M_01.prefab";

        // The SHIPPED hub. MainCastle_Hall.unity is LEGACY and is NOT the hub (CLAUDE.md §7).
        private const string HubScene = "Assets/Scenes/Main_Castle_Overworld.unity";

        // The four side roots. ⚠ THE NAMES ARE LOAD-BEARING: CastleWallNavObstacleInstaller
        // matches "CastleSide_" to fit its carving NavMeshObstacles, and the hero is a
        // NavMeshAgent that IGNORES physics colliders — only a carved NavMesh stops her
        // ("im in the wall", owner F8 2026-07-15). Rename these and the walls stop blocking.
        private static readonly string[] SideNames =
            { "CastleSide_South", "CastleSide_West", "CastleSide_North", "CastleSide_East" };

        // WO-449: wall masonry lives on "Structure" so the tower / HeroTargetIndicator /
        // PlayerAttackController line-of-sight linecasts occlude through it. Lose this and
        // towers shoot through walls again.
        private const string StructureLayerName = "Structure";

        // Slots per side. ODD so the centre slot is the gate (owner 2026-06-13). At the
        // measured 5.00m module this puts the wall line at |z| ~= 39m — inside PlinthHalf
        // (44) and clear of the moat band (44..62), with the gate exit strip still reaching
        // the plinth edge. 17 slots would land the wall AT the plinth rim (44.0); 15 keeps
        // the whole ring on the plinth with margin.
        private const int SlotsPerSide = 15;

        // Gap tolerance: modules are placed edge-to-edge at the MEASURED module width, so
        // this only guards against a pack update changing the mesh.
        private const float MinModuleWidth = 0.5f;

        [MenuItem("Defenders/Walls/Build Synty Castle Perimeter")]
        public static void BuildInOpenScene()
        {
            var root = Build();
            if (root == null) return;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[SyntyPerimeter] built in the open scene (not saved — Ctrl+S to keep).");
        }

        /// <summary>Batch: open the hub, rebuild the perimeter, save. Idempotent.</summary>
        public static void BuildInHub()
        {
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            var root = Build();
            if (root == null) { Debug.LogError("[SyntyPerimeter] PERIMETER_FAIL build returned null — scene NOT saved."); return; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SyntyPerimeter] PERIMETER_OK built + saved into " + HubScene);
        }

        private static GameObject Build()
        {
            var wall        = Load(WallPrefab);
            var arrowslit   = Load(ArrowslitPrefab);
            var battlements = Load(BattlementsPrefab);
            var gate        = Load(GatePrefab);
            var tower       = Load(TowerPrefab);

            if (wall == null || gate == null || tower == null)
            {
                Debug.LogWarning("[SyntyPerimeter] core modules missing (pack not imported?) — " +
                                 "perimeter NOT rebuilt, existing walls left untouched. " +
                                 "Re-import Synty POLYGON Fantasy Kingdom into Assets/Synty/.");
                return null;
            }

            // MEASURE the module pitch and the tower reach from the real prefabs. Never
            // assume 5.00m — a pack update must move the grid, not silently overlap it.
            float moduleWidth = MeasureX(wall);
            if (moduleWidth < MinModuleWidth)
            {
                Debug.LogWarning($"[SyntyPerimeter] wall module measured {moduleWidth:F2}m along X — " +
                                 "implausible; aborting rather than building an overlapped ring.");
                return null;
            }
            float wallHeight = MeasureY(wall);
            float towerHalf  = MeasureX(tower) * 0.5f;
            // How far the tower's authored foundation hangs BELOW its own origin (negative
            // min.y). Measured, never assumed — a pack update must move the seat with it.
            float towerBaseDrop = Mathf.Max(0f, -MeasureMinY(tower));

            // Clear the four side groups for a clean, reproducible rebuild.
            foreach (var n in SideNames)
            {
                var ex = GameObject.Find(n);
                if (ex != null) { Object.DestroyImmediate(ex); Debug.Log("[SyntyPerimeter] removed " + n); }
            }

            float liftY      = CastleHubBuilder.CastleFootprintLiftY;  // WO-593 island raise
            float span       = SlotsPerSide * moduleWidth;
            float halfExtent = span * 0.5f + towerHalf;
            int   gateIndex  = (SlotsPerSide - 1) / 2;

            int structureLayer = LayerMask.NameToLayer(StructureLayerName);
            if (structureLayer < 0)
                Debug.LogWarning("[SyntyPerimeter] WO-449: '" + StructureLayerName +
                                 "' layer missing — walls left on Default, the LoS gate degrades off.");

            var roots = new List<GameObject>();
            int walls = 0, caps = 0, gates = 0, towers = 0;

            for (int s = 0; s < 4; s++)
            {
                var side = new GameObject(SideNames[s]);
                Undo.RegisterCreatedObjectUndo(side, "Build Synty Castle Perimeter");
                side.transform.position = Vector3.zero;
                roots.Add(side);

                var rot      = Quaternion.Euler(0f, 90f * s, 0f);
                var midpoint = rot * new Vector3(0f, liftY, -halfExtent);
                var along    = rot * Vector3.right;

                // Corner tower for this side, at the side's own -X end. Four sides x one
                // corner each = the four corners, with no double-placement.
                // ⚠ SEAT IT BY ITS OWN BOUNDS. SM_Bld_Castle_Wall_Tower_M_01 measures
                // Y -2.50..+5.02m — 2.5m of that is BURIED FOUNDATION, authored below the
                // origin. Placing it at y=liftY like a wall module leaves the footing
                // standing proud above the ground (seen in the 2026-09-01 proof capture).
                // Push it down by its own min.y so the foundation goes where it belongs and
                // the tower's visible height matches the wall it joins.
                var cornerPos = rot * new Vector3(-halfExtent, liftY - towerBaseDrop, -halfExtent);
                if (Place(side.transform, tower, cornerPos, rot, "CornerTower") != null) towers++;

                for (int i = 0; i < SlotsPerSide; i++)
                {
                    float offset = -span * 0.5f + (i + 0.5f) * moduleWidth;
                    var pos = midpoint + along * offset;

                    if (i == gateIndex)
                    {
                        if (Place(side.transform, gate, pos, rot, "Gate") != null) gates++;
                        continue;   // no battlement cap over the gate arch
                    }

                    // Every 4th module gets an arrowslit for silhouette variety. Same
                    // footprint, same pitch — variety WITHOUT breaking the grid.
                    var art = (arrowslit != null && i % 4 == 1) ? arrowslit : wall;
                    if (Place(side.transform, art, pos, rot, $"Wall_{i}") != null) walls++;

                    if (battlements != null &&
                        Place(side.transform, battlements, pos + Vector3.up * wallHeight, rot, $"Battlement_{i}") != null)
                        caps++;
                }

                if (structureLayer >= 0) SetLayerRecursively(side, structureLayer);
            }

            Debug.Log($"[SyntyPerimeter] module {moduleWidth:F2}m (measured) x {wallHeight:F2}m tall, " +
                      $"tower half {towerHalf:F2}m -> {SlotsPerSide} slots/side, span {span:F1}m, " +
                      $"extent +-{halfExtent:F1}m (plinth 44), gate slot {gateIndex} centred. " +
                      $"Placed {walls} walls + {caps} battlements + {gates} gates + {towers} towers " +
                      $"= {walls + caps + gates + towers} objects, ALL at scale 1.");
            return roots[0];
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static GameObject Load(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // LogWarning, never error: the pack is gitignored and may not be imported (§4).
            if (p == null) Debug.LogWarning("[SyntyPerimeter] prefab missing: " + path);
            return p;
        }

        /// <summary>Place one module at scale 1. Returns null if instantiation failed.</summary>
        private static GameObject Place(Transform parent, GameObject prefab, Vector3 pos,
                                        Quaternion rot, string name)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            if (go == null) return null;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position   = pos;
            go.transform.rotation   = rot;
            go.transform.localScale = Vector3.one;   // NEVER scaled — that is the whole point
            Undo.RegisterCreatedObjectUndo(go, "Build Synty Castle Perimeter");
            return go;
        }

        private static float MeasureX(GameObject prefab) => MeasureAxis(prefab, 0);
        private static float MeasureY(GameObject prefab) => MeasureAxis(prefab, 1);

        /// <summary>Lowest world Y of a prefab's combined renderer bounds when placed at the
        /// origin — negative for a module with an authored below-origin foundation.</summary>
        private static float MeasureMinY(GameObject prefab)
        {
            if (prefab == null) return 0f;
            var tmp = Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = Quaternion.identity;
            tmp.transform.localScale = Vector3.one;
            float minY = 0f;
            var rends = tmp.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                minY = b.min.y;
            }
            Object.DestroyImmediate(tmp);
            return minY;
        }

        /// <summary>Combined renderer-bounds extent of a prefab along one axis, from a
        /// throwaway instance (a prefab asset's renderers report no world bounds).</summary>
        private static float MeasureAxis(GameObject prefab, int axis)
        {
            if (prefab == null) return 0f;
            var tmp = Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = Quaternion.identity;
            tmp.transform.localScale = Vector3.one;
            float size = 0f;
            var rends = tmp.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                size = b.size[axis];
            }
            Object.DestroyImmediate(tmp);
            return size;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
