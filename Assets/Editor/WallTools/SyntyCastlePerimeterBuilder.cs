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

        // The shipped merged world is flat at y=0 and has no raised-island plinth.
        private const float MergedWorldGroundY = 0f;

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
            // ⚠ MEASURE THE PIVOT, NEVER INFER IT. How far the mesh's -X edge sits from the
            // prefab's own origin, taken from a real instance placed at the world origin.
            // The raw FBX reports SM_Bld_Castle_Wall_01 at X -0.00..5.00 (pivot on the LEFT
            // edge), but Unity's FBX axis conversion MIRRORS X on import, so the instantiated
            // prefab actually occupies -5.00..0.00. Reading the origin convention off the FBX
            // instead of off an instance is what put the whole ring 5m out and opened a corner
            // (2026-09-01 symmetry oracle: centre -5.00, bounds [-42.50, 32.50]). This value
            // is 0 for a left-edge pivot, -width for a right-edge pivot, -width/2 if centred —
            // all three place correctly without another edit.
            float wallPivotMinX = MeasureMinX(wall);
            float gatePivotMinX = MeasureMinX(gate);
            // How far the tower's authored foundation hangs BELOW its own origin (negative
            // min.y). Measured, never assumed — a pack update must move the seat with it.
            float wallSeatY      = MergedWorldGroundY - MeasureMinY(wall);
            float arrowslitSeatY = MergedWorldGroundY - MeasureMinY(arrowslit != null ? arrowslit : wall);
            float gateSeatY      = MergedWorldGroundY - MeasureMinY(gate);
            // This pack's wall-tower module imports vertically inverted relative to the
            // wall/gate modules. Flip it upright, then seat the rotated lower bound (which is
            // the negative of the unrotated max Y) on ground.
            float towerSeatY     = MergedWorldGroundY + MeasureMaxY(tower);

            // Clear the four side groups for a clean, reproducible rebuild.
            foreach (var n in SideNames)
            {
                var ex = GameObject.Find(n);
                if (ex != null) { Object.DestroyImmediate(ex); Debug.Log("[SyntyPerimeter] removed " + n); }
            }

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
                var midpoint = rot * new Vector3(0f, 0f, -halfExtent);
                var along    = rot * Vector3.right;

                // Corner tower for this side, at the side's own -X end. Four sides x one
                // corner each = the four corners, with no double-placement.
                // ⚠ SEAT IT BY ITS OWN BOUNDS. SM_Bld_Castle_Wall_Tower_M_01 measures
                // Y -2.50..+5.02m — 2.5m of that is BURIED FOUNDATION, authored below the
                // origin. Placing it at y=liftY like a wall module leaves the footing
                // standing proud above the ground (seen in the 2026-09-01 proof capture).
                // Push it down by its own min.y so the foundation goes where it belongs and
                // the tower's visible height matches the wall it joins.
                var cornerPos = rot * new Vector3(-halfExtent, towerSeatY, -halfExtent);
                var towerRot = rot * Quaternion.Euler(180f, 0f, 0f);
                if (Place(side.transform, tower, cornerPos, towerRot, "CornerTower") != null) towers++;

                for (int i = 0; i < SlotsPerSide; i++)
                {
                    // ⚠ LEFT-EDGE ORIGIN, NOT CENTRED. Measured 2026-09-01 from the FBX:
                    //   SM_Bld_Castle_Wall_01        X  -0.00..5.00   <- origin at the LEFT end
                    //   SM_Bld_Castle_Wall_Gate_01   X  -0.34..5.34   <- same
                    //   SM_Bld_Castle_Battlements_01 X   0.00..5.00   <- same
                    //   SM_Bld_Castle_Wall_Tower_M_01 X -1.47..1.59   <- this one IS centred
                    // The first cut of this loop used (i + 0.5) * moduleWidth, i.e. it treated
                    // the wall origin as CENTRED. That shifted every module +2.5m, so a run
                    // covered [-35,+40] instead of [-37.5,+37.5]: a 2.5m HOLE at one corner and
                    // a 2.5m overshoot at the other. That — not a missing corner module — was
                    // the open corner in the first proof capture. Towers stay centre-placed.
                    // Left edge of slot i along the run, then back off the MEASURED pivot so the
                    // module's -X edge lands exactly there. Slot i therefore occupies
                    // [runLeft, runLeft + moduleWidth] whatever the prefab's pivot convention is.
                    float runLeft = -span * 0.5f + i * moduleWidth;
                    bool useArrowslit = arrowslit != null && i % 4 == 1;
                    float moduleSeatY = useArrowslit ? arrowslitSeatY : wallSeatY;
                    var pos = midpoint + along * (runLeft - wallPivotMinX) + Vector3.up * moduleSeatY;

                    if (i == gateIndex)
                    {
                        var gatePos = midpoint + along * (runLeft - gatePivotMinX) + Vector3.up * gateSeatY;
                        var gateInstance = Place(side.transform, gate, gatePos, rot, "Gate");
                        if (gateInstance != null)
                        {
                            ApplyOpenGatePose(gateInstance);
                            AddGateFlankColliders(side.transform, gateInstance, s);
                            gates++;
                        }
                        continue;   // no battlement cap over the gate arch
                    }

                    // Every 4th module gets an arrowslit for silhouette variety. Same
                    // footprint, same pitch — variety WITHOUT breaking the grid.
                    var art = useArrowslit ? arrowslit : wall;
                    if (Place(side.transform, art, pos, rot, $"Wall_{i}") != null) walls++;

                    if (battlements != null &&
                        Place(side.transform, battlements, pos + Vector3.up * wallHeight, rot, $"Battlement_{i}") != null)
                        caps++;
                }

                if (structureLayer >= 0) SetLayerRecursively(side, structureLayer);
            }

            // ── SYMMETRY ORACLE (measured, not assumed) ─────────────────────────────
            // The south side must straddle x=0 evenly. A module-origin mistake (left-edge
            // treated as centred, or vice versa) shows up here as a non-zero centre offset
            // long before it shows up as an open corner in a screenshot.
            // ⚠ MEASURE THE WALL RUN ONLY, NOT THE WHOLE SIDE GROUP. Each side carries exactly
            // ONE corner tower, at its -X end, so the GROUP is asymmetric by design — its centre
            // sits ~1.5m negative and always will. The first cut of this oracle measured the whole
            // group and duly fired PERIMETER_ASYMMETRIC on geometry that was already correct.
            // An oracle that fails a correct build is worse than no oracle: it invites someone to
            // "fix" working code. What must straddle x=0 is the RUN of wall/gate/battlement slots.
            var southSide = GameObject.Find(SideNames[0]);
            if (southSide != null)
            {
                var runRends = new List<Renderer>();
                foreach (Transform child in southSide.transform)
                {
                    if (child.name.StartsWith("CornerTower")) continue;   // excluded, see above
                    runRends.AddRange(child.GetComponentsInChildren<Renderer>(true));
                }
                if (runRends.Count > 0)
                {
                    Bounds b = runRends[0].bounds;
                    for (int i = 1; i < runRends.Count; i++) b.Encapsulate(runRends[i].bounds);
                    float centreOffset = b.center.x;
                    Debug.Log($"[SyntyPerimeter] south WALL RUN (towers excluded) X " +
                              $"[{b.min.x:F2},{b.max.x:F2}] centre {centreOffset:F2} (want ~0), " +
                              $"width {b.size.x:F2}m (want {span:F2}m).");
                    if (Mathf.Abs(centreOffset) > moduleWidth * 0.25f)
                        Debug.LogError($"[SyntyPerimeter] PERIMETER_ASYMMETRIC the south wall run centres " +
                                       $"on x={centreOffset:F2}, not 0 — a pivot mistake shifts every piece " +
                                       "and opens a corner. Pivots are MEASURED (wallPivotMinX); do not " +
                                       "infer them from the FBX, whose X is mirrored on import.");
                    if (Mathf.Abs(b.size.x - span) > moduleWidth * 0.25f)
                        Debug.LogError($"[SyntyPerimeter] PERIMETER_SPAN_WRONG the south wall run measures " +
                                       $"{b.size.x:F2}m but the slot maths says {span:F2}m.");
                }
            }

            if (!VerifyGateClearance(roots))
            {
                Debug.LogError("[SyntyPerimeter] PERIMETER_FAIL gate clearance contract failed; scene will not be saved.");
                return null;
            }

            Debug.Log($"[SyntyPerimeter] measured pivots: wall minX {wallPivotMinX:F2} " +
                      $"(0 = left-edge, -{moduleWidth:F2} = right-edge), gate minX {gatePivotMinX:F2}, " +
                      $"ground seats wall={wallSeatY:F2}, arrowslit={arrowslitSeatY:F2}, " +
                      $"gate={gateSeatY:F2}, tower={towerSeatY:F2} (floor y={MergedWorldGroundY:F2}).");
            Debug.Log($"[SyntyPerimeter] module {moduleWidth:F2}m (measured) x {wallHeight:F2}m tall, " +
                      $"tower half {towerHalf:F2}m -> {SlotsPerSide} slots/side, span {span:F1}m, " +
                      $"extent +-{halfExtent:F1}m (plinth 44), gate slot {gateIndex} centred. " +
                      $"Placed {walls} walls + {caps} battlements + {gates} gates + {towers} towers " +
                      $"= {walls + caps + gates + towers} objects, ALL at scale 1.");
            return roots[0];
        }

        /// <summary>Four-gate proof: gate art owns no enabled collider, while two wall-owned
        /// jamb boxes reach to a centred passage at least four metres wide.</summary>
        private static bool VerifyGateClearance(List<GameObject> roots)
        {
            int passed = 0;
            for (int s = 0; s < roots.Count; s++)
            {
                var root = roots[s];
                if (root == null) continue;
                Transform gate = root.transform.Find("Gate");
                Transform left = root.transform.Find("Wall_DoorJamb_L");
                Transform right = root.transform.Find("Wall_DoorJamb_R");
                var leftBox = left != null ? left.GetComponent<BoxCollider>() : null;
                var rightBox = right != null ? right.GetComponent<BoxCollider>() : null;
                if (gate == null || leftBox == null || rightBox == null ||
                    !leftBox.enabled || !rightBox.enabled)
                    continue;

                bool gateClear = true;
                var gateCols = gate.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < gateCols.Length; i++)
                    if (gateCols[i] != null && gateCols[i].enabled) gateClear = false;

                bool alongX = s == 0 || s == 2;
                float clearWidth = alongX
                    ? rightBox.bounds.min.x - leftBox.bounds.max.x
                    : rightBox.bounds.min.z - leftBox.bounds.max.z;
                if (gateClear && clearWidth >= 3.95f) passed++;
                else Debug.LogError($"[SyntyPerimeter] gate {SideNames[s]} clearance failed: " +
                                    $"gateClear={gateClear} width={clearWidth:F2}m.");
            }

            if (passed == 4)
                Debug.Log("GATE_CLEARANCE_OK 4/4 gates: gate colliders disabled; wall jambs meet a 4.00m opening.");
            return passed == 4;
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

        /// <summary>
        /// The Synty gate prefab ships closed, while the overworld's four entrances are
        /// permanently traversable (GateTraversalInjector provides the nav-safe crossing).
        /// Author the matching open pose into the scene: swing both hinged leaves outward
        /// and lift the portcullis into the arch. Child names are pack-authored; missing
        /// children degrade to a warning instead of breaking a perimeter rebuild.
        /// </summary>
        private static void ApplyOpenGatePose(GameObject gate)
        {
            Transform left = FindDescendant(gate.transform, "SM_Bld_Castle_Wall_Gate_Door_L_01");
            Transform right = FindDescendant(gate.transform, "SM_Bld_Castle_Wall_Gate_Door_R_01");
            Transform portcullis = FindDescendant(gate.transform, "SM_Bld_Castle_Wall_Gate_Portcullis_01");

            if (left != null) left.localRotation = Quaternion.Euler(0f, 105f, 0f);
            if (right != null) right.localRotation = Quaternion.Euler(0f, -105f, 0f);
            // This modular arch has no upper gatehouse volume to conceal a raised grille;
            // leaving it translated above the roof reads as floating bars. Hide it in the
            // permanently-open state while retaining the prefab child for future animation.
            if (portcullis != null) portcullis.gameObject.SetActive(false);

            // The gate is presentation only in this permanently-open perimeter. Disable
            // EVERY collider under it (including the arch/root MeshCollider), then let the
            // wall-owned DoorJamb flank boxes below extend masonry collision precisely to
            // the opening. This avoids catching the hero/enemy on a hidden gate surface.
            DisableColliders(gate.transform);

            if (left == null || right == null || portcullis == null)
                Debug.LogWarning("[SyntyPerimeter] gate child layout changed; open pose is incomplete on " + gate.name + ".");
        }

        /// <summary>
        /// Extend the wall collision through the gate module's side masonry while keeping
        /// the central four-metre passage completely clear. These are siblings owned by the
        /// wall side, never children of the gate, so "remove every gate collider" remains a
        /// literal invariant. The visible arch supplies the masonry art.
        /// </summary>
        private static void AddGateFlankColliders(Transform side, GameObject gate, int sideIndex)
        {
            if (side == null || gate == null) return;
            var renderers = gate.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[SyntyPerimeter] gate has no renderer bounds; wall flanks not authored.");
                return;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            const float doorwayHalf = 2f;
            bool splitAlongX = sideIndex == 0 || sideIndex == 2;
            if (splitAlongX)
            {
                AddGateFlankCollider(side, "Wall_DoorJamb_L", b,
                    b.min.x, b.center.x - doorwayHalf, true);
                AddGateFlankCollider(side, "Wall_DoorJamb_R", b,
                    b.center.x + doorwayHalf, b.max.x, true);
            }
            else
            {
                AddGateFlankCollider(side, "Wall_DoorJamb_L", b,
                    b.min.z, b.center.z - doorwayHalf, false);
                AddGateFlankCollider(side, "Wall_DoorJamb_R", b,
                    b.center.z + doorwayHalf, b.max.z, false);
            }
        }

        private static void AddGateFlankCollider(Transform side, string name, Bounds source,
                                                  float axisMin, float axisMax, bool axisIsX)
        {
            if (axisMax <= axisMin + 0.05f) return;
            Vector3 min = source.min;
            Vector3 max = source.max;
            // Sink the wall-owned collision slightly into the terrain. Exact y=0
            // bottoms exposed a precision lip at the threshold on mobile agents.
            min.y = Mathf.Min(min.y, MergedWorldGroundY - 0.25f);
            if (axisIsX) { min.x = axisMin; max.x = axisMax; }
            else { min.z = axisMin; max.z = axisMax; }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            var go = new GameObject(name);
            go.transform.SetParent(side, false);
            go.transform.position = bounds.center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = bounds.size;
            Undo.RegisterCreatedObjectUndo(go, "Build gate wall flank");
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == exactName) return all[i];
            return null;
        }

        private static void DisableColliders(Transform root)
        {
            if (root == null) return;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
        }

        private static float MeasureX(GameObject prefab) => MeasureAxis(prefab, 0);
        private static float MeasureY(GameObject prefab) => MeasureAxis(prefab, 1);

        /// <summary>Lowest world X of a prefab's combined renderer bounds when placed at the
        /// origin — i.e. where its mesh starts relative to its own pivot. See wallPivotMinX.</summary>
        private static float MeasureMinX(GameObject prefab) => MeasureMin(prefab, 0);

        /// <summary>Lowest world Y of a prefab's combined renderer bounds when placed at the
        /// origin — negative for a module with an authored below-origin foundation.</summary>
        private static float MeasureMinY(GameObject prefab) => MeasureMin(prefab, 1);

        private static float MeasureMaxY(GameObject prefab) => MeasureMax(prefab, 1);

        private static float MeasureMin(GameObject prefab, int axis)
        {
            if (prefab == null) return 0f;
            var tmp = Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = Quaternion.identity;
            tmp.transform.localScale = Vector3.one;
            float min = 0f;
            var rends = tmp.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                min = b.min[axis];
            }
            Object.DestroyImmediate(tmp);
            return min;
        }

        private static float MeasureMax(GameObject prefab, int axis)
        {
            if (prefab == null) return 0f;
            var tmp = Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = Quaternion.identity;
            tmp.transform.localScale = Vector3.one;
            float max = 0f;
            var rends = tmp.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                max = b.max[axis];
            }
            Object.DestroyImmediate(tmp);
            return max;
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
