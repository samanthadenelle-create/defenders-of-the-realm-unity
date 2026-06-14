// =============================================================================
// RaidBaseGenerator — the "Iron Bastion" CONCENTRIC raid-base generator: the
// raid-tier evolution of the single-ring PerimeterWallGenerator. Where Perimeter
// builds ONE square ring (4 corner towers + per-side fill, gate = centre slot),
// this builds a DOUBLE-RING fortress that funnels the player through a killing
// courtyard into a boss keep:
//
//   OUTER ring  (Iron, large)  — gate on SOUTH only → the single approach.
//   INNER keep  (Steel, small) — gate on NORTH only → OPPOSITE the outer gate,
//                                 so the player must cross the courtyard under
//                                 fire to reach the keep (the funnel).
//   BossSpawn marker at keep centre (0,0,0) — the garrison/boss spawner places
//                                 the boss here later (we only drop the marker).
//   Court watchtowers between the rings — crossfire over the courtyard.
//
// REUSES PerimeterWallGenerator's proven place pattern verbatim — box-fit to
// (1.5,3,1.5) / upright (-90X FBX-flat correction) / ground-seat / WallSegment+
// tier — but PARAMETERIZED BY TIER: the tier is passed into the segment placer so
// each ring is built from its own wall art (Iron outer, Steel keep). BuildRing is
// the reusable primitive — adding more layouts later (gauntlet, enclave) is just
// another Build* method composing rings.
//
// TURRET ARMING (important): corner/court towers are NAMED with "Watchtower" in
// them on purpose — GarrisonController.ArmGarrisonTurrets arms any prop whose name
// CONTAINS "Watchtower" as a live EnemyOwned turret at runtime. So the crossfire
// comes alive with NO DefenseTower wiring here — the name IS the contract.
//
// Tier-driven art via WallTierData.Get(tier).SegmentPrefabPath. Builds into the
// OPEN scene under its own idempotent root (re-run rebuilds; delete the root to
// remove). Null-guards missing prefabs with Debug.LogWarning (never crashes).
//
// -----------------------------------------------------------------------------
// CONFIG-DRIVEN (WO raid generator): BuildFromConfig(configId, parentRoot) reads a
// SceneConfigDef from SceneConfigCatalog (scene-configs.json — the ONE contract)
// and builds ANY raid level from data: 1+interiorWallLayers concentric rings
// (outer tier=wallTier, gates per entranceCount, inner keep = ReinforcedSteel with
// a single OPPOSITE-side gate = the Iron-Bastion funnel), archerTowerCount +
// mageTowerCount Watchtower props per towerPlacementStyle (Cardinal = mid-side,
// OverlappingFire = corners + mid-side crossfire), and a BossSpawn at centre.
// BuildIronBastion is preserved for the existing menu items. BuildSceneFor(id) /
// BuildAllRaidScenes() bake each level into Assets/Scenes/RaidBase_<id>.unity.
//
// Batchmode: DeNelle.Editor.RaidBaseGenerator.BuildInOpenScene
//            DeNelle.Editor.RaidBaseGenerator.BuildIntoScene  (pass a scene path)
//            DeNelle.Editor.RaidBaseGenerator.BuildAllRaidScenes
// Menu:      Defenders/Walls/Build Raid Base — Iron Bastion
//            Defenders/Walls/Build All Raid Scenes (config-driven)
// =============================================================================
using System;                   // Enum.TryParse, StringComparison
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeNelle.Village;          // WallSegment, SceneConfigCatalog, SceneConfigDef
using DeNelle.Village.Walls;    // WallTier, WallTierData
using Object = UnityEngine.Object;   // disambiguate Object (System.Object vs UnityEngine.Object) — System is in scope

namespace DeNelle.Editor
{
    public static class RaidBaseGenerator
    {
        // ── Tunables (owner-editable; later sourced from scene-configs.json) ──
        // Corner/court tower art (Resources path). Falls back gracefully if missing.
        private const string CornerTowerPath = "Structures/Tower_Medieval_Wood";

        // Outer ring: bigger, Iron, single SOUTH gate (the approach).
        private const int   OuterSlotsPerSide = 15;   // 15 * 1.5m = 22.5m wall run/side
        private const WallTier OuterTier      = WallTier.Iron;
        // Inner keep: smaller, ReinforcedSteel, single NORTH gate (opposite → funnel).
        private const int   InnerSlotsPerSide = 7;    // 7 * 1.5m = 10.5m wall run/side
        private const WallTier InnerTier      = WallTier.ReinforcedSteel;

        // One wall slot = the owner's box; X = the tiling unit (segment width).
        private static readonly Vector3 SegSize = new Vector3(1.5f, 3.0f, 1.5f);
        private const string RootName = "RaidBase_IronBastion";

        // Side index → cardinal name, by the 90°-around-origin rot index (0=S,1=E,2=N,3=W).
        private static readonly string[] SideName = { "S", "E", "N", "W" };

        private const string DefaultScene = "Assets/Scenes/MainCastle_Hall.unity";

        // ── Entry points ─────────────────────────────────────────────────────

        [MenuItem("Defenders/Walls/Build Raid Base — Iron Bastion")]
        public static void BuildInOpenScene()
        {
            var root = Build();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[RaidBaseGenerator] built '{RootName}' in the open scene " +
                      $"(outer {OuterTier} / keep {InnerTier} — Ctrl+S to keep).");
        }

        // Batch: open <scenePath>, build the bastion under its own root, save.
        // Reproducible (re-run rebuilds; delete the root to remove). Existing scene
        // content untouched (separate root). Parameterized so any test scene works.
        public static void BuildIntoScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) scenePath = DefaultScene;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RaidBaseGenerator] built + saved '{RootName}' into {scenePath} " +
                      $"(outer {OuterTier} / keep {InnerTier}).");
        }

        // Batch: build into a FRESH empty scene (camera + light) so the bastion stands
        // ALONE for a clean visual eyeball — no castle/hub overlap. Saves a dedicated file.
        public static void BuildToNewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Build();
            const string path = "Assets/Scenes/RaidBase_IronBastion.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[RaidBaseGenerator] built + saved '{RootName}' into NEW scene {path} " +
                      $"(outer {OuterTier} / keep {InnerTier}).");
        }

        // ── Config-driven entry points (WO raid generator) ───────────────────

        // The three flagship raid levels, in difficulty order.
        private static readonly string[] RaidConfigIds =
            { "raider_camp_small", "fortified_garrison", "mage_enclave" };

        [MenuItem("Defenders/Walls/Build All Raid Scenes (config-driven)")]
        public static void BuildAllRaidScenes()
        {
            SceneConfigCatalog.Invalidate();   // pick up any fresh JSON edit
            foreach (var id in RaidConfigIds) BuildSceneFor(id);
            Debug.Log($"[RaidBaseGenerator] baked {RaidConfigIds.Length} raid scene(s) from scene-configs.json.");
        }

        // Batch: build ONE config into its own fresh scene (camera + light) so the
        // raid base stands ALONE for a clean eyeball, saved to RaidBase_<id>.unity.
        // Mirrors BuildToNewScene but driven by the config id.
        public static void BuildSceneFor(string configId)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject($"RaidBase_{configId}");
            root.transform.position = Vector3.zero;
            BuildFromConfig(configId, root.transform);

            string path = $"Assets/Scenes/RaidBase_{configId}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[RaidBaseGenerator] built + saved raid '{configId}' into NEW scene {path}.");
        }

        // Build a complete raid level from its scene-config under parentRoot. Idempotent
        // per-config: an existing 'RaidBase_<id>' child of parentRoot is rebuilt. Reads
        // ALL geometry from the SceneConfigDef; garrison/scoring fields are ignored here
        // (spawner/scoring layers). Null-guards a missing config (LogWarning, no crash).
        public static void BuildFromConfig(string configId, Transform parentRoot)
        {
            var def = SceneConfigCatalog.Find(configId);
            if (def == null)
            {
                Debug.LogWarning($"[RaidBaseGenerator] no scene-config '{configId}' — nothing built.");
                return;
            }

            string rootName = $"RaidBase_{configId}";
            // Idempotent: remove a prior build under the same parent (or globally if parentRoot is null).
            var prior = parentRoot != null ? parentRoot.Find(rootName) : null;
            if (prior != null) Object.DestroyImmediate(prior.gameObject);
            var priorGlobal = GameObject.Find(rootName);
            if (priorGlobal != null && (parentRoot == null || priorGlobal.transform.parent != parentRoot))
                Object.DestroyImmediate(priorGlobal);

            var root = new GameObject(rootName);
            if (parentRoot != null) root.transform.SetParent(parentRoot, false);
            root.transform.localPosition = Vector3.zero;

            BuildConfigLayout(def, root.transform);

            // ── GARRISON WIRING ──────────────────────────────────────────────────
            // Attach the runtime spawner that fills this baked base with its config's
            // garrison (boss + composition, player-level-scaled). It carries the STORED
            // config id (NOT the scene name — the baked scene is RaidBase_<id>, which
            // won't match the config's sceneName, so SceneOwnership can't resolve it by
            // name; the spawner sets ownership itself off this id). At runtime it reads
            // SceneConfigCatalog.Find(configId).garrison, spawns on a ring at
            // baseRadius*0.5, and arms the Watchtower_* props.
            var spawner = root.AddComponent<DeNelle.Village.World.Camps.RaidGarrisonSpawner>();
            spawner.SetConfigId(configId);
        }

        // The data-driven layout: outer perimeter (+ optional inner keep) + towers +
        // boss marker, all from the config. The Iron-Bastion funnel falls out naturally
        // when interiorWallLayers>0 (inner keep gate OPPOSITE the outer gate).
        private static void BuildConfigLayout(SceneConfigDef def, Transform root)
        {
            WallTier outerTier = ParseTier(def.wallTier, WallTier.Wood);
            int slots = Mathf.Max(3, def.wallSegmentsPerSide);

            // ── OUTER perimeter gates from entranceCount ─────────────────────────
            //   1 = south only (the single approach); 2 = south + north (main + breach).
            bool twoGates = def.entranceCount >= 2;
            var outerGates = new bool[4] { true, false, twoGates, false }; // S [+N]
            float outerExtent = BuildRing(root, slots, outerTier,
                                          outerGates, CornerTowerPath, "Outer");

            // ── INNER keep ring(s) — the kill-zone (interiorWallLayers) ──────────
            //   ReinforcedSteel, smaller, single gate OPPOSITE the outer south gate
            //   (→ NORTH) so the player crosses the courtyard under fire (the funnel).
            float innermostExtent = outerExtent;
            int innerLayers = Mathf.Max(0, def.interiorWallLayers);
            for (int layer = 0; layer < innerLayers; layer++)
            {
                int innerSlots = Mathf.Max(3, slots - (layer + 1) * 4);   // each ring tighter
                var innerGates = new bool[4] { false, false, true, false }; // N only (opposite S)
                innermostExtent = BuildRing(root, innerSlots, WallTier.ReinforcedSteel,
                                            innerGates, CornerTowerPath, $"Keep{layer + 1}");
            }

            // ── TOWERS — archer + mage Watchtower props per placement style ──────
            // Named "Watchtower_*" → GarrisonController.ArmGarrisonTurrets arms them.
            // Placed in the courtyard band between the innermost ring and the outer ring.
            float towerBand = innerLayers > 0
                ? (innermostExtent + outerExtent) * 0.5f   // crossfire over the courtyard
                : outerExtent + SegSize.x * 0.6f;          // single-ring: just inside the wall line
            PlaceTowers(root, def, towerBand);

            // ── BOSS marker at centre ────────────────────────────────────────────
            var boss = new GameObject("BossSpawn");
            boss.transform.SetParent(root, false);
            boss.transform.localPosition = Vector3.zero;

            Debug.Log($"[RaidBaseGenerator] '{root.name}' ({def.displayName}, {def.difficulty}): " +
                      $"outer {outerTier} {slots}/side gates={(twoGates ? "S,N" : "S")} ±{outerExtent:F1}m, " +
                      $"{innerLayers} inner keep layer(s), style={def.towerPlacementStyle} " +
                      $"{def.archerTowerCount} archer + {def.mageTowerCount} mage towers, BossSpawn@centre.");
        }

        // Place archerTowerCount + mageTowerCount Watchtower props around the band radius.
        // Cardinal       → the 4 side-midpoints (N/E/S/W), round-robin filled.
        // OverlappingFire→ corners FIRST (diagonals) then side-midpoints → crossfire.
        // Archer towers carry "Watchtower_Archer_*", mage towers "Watchtower_Mage_*"
        // (both contain "Watchtower" → armed by GarrisonController).
        private static void PlaceTowers(Transform root, SceneConfigDef def, float band)
        {
            int archers = Mathf.Max(0, def.archerTowerCount);
            int mages = Mathf.Max(0, def.mageTowerCount);
            int total = archers + mages;
            if (total <= 0) return;

            bool overlapping = !string.Equals(def.towerPlacementStyle, "Cardinal",
                                               System.StringComparison.OrdinalIgnoreCase);
            var dirs = BuildTowerDirections(total, overlapping);
            var prefab = Resources.Load<GameObject>(CornerTowerPath);

            int placed = 0;
            for (int i = 0; i < total; i++)
            {
                bool isMage = i >= archers;          // archers first, then mages
                string kind = isMage ? "Mage" : "Archer";
                int idx = isMage ? (i - archers) : i;
                var pos = dirs[i] * band;
                if (PlaceCornerTower(root, prefab, pos, $"Watchtower_{kind}_{idx}")) placed++;
            }
            Debug.Log($"[RaidBaseGenerator] placed {placed}/{total} watchtowers " +
                      $"({archers} archer + {mages} mage, style={(overlapping ? "OverlappingFire" : "Cardinal")}).");
        }

        // Evenly spread `count` outward XZ directions. Cardinal uses the 4 side mids
        // (S/E/N/W) round-robin; OverlappingFire interleaves the 4 diagonals (corners)
        // FIRST then the 4 side mids, so the first towers cover the corners (crossfire),
        // with any remainder spread evenly on a circle to avoid stacking.
        private static Vector3[] BuildTowerDirections(int count, bool overlapping)
        {
            // Cardinal side-midpoint directions (match BuildRing's 90° index 0=S..3=W).
            Vector3[] cardinals =
            {
                new Vector3(0f, 0f, -1f),  // S
                new Vector3(1f, 0f,  0f),  // E
                new Vector3(0f, 0f,  1f),  // N
                new Vector3(-1f, 0f, 0f),  // W
            };
            Vector3[] diagonals =
            {
                new Vector3( 1f, 0f, -1f).normalized,  // SE
                new Vector3( 1f, 0f,  1f).normalized,  // NE
                new Vector3(-1f, 0f,  1f).normalized,  // NW
                new Vector3(-1f, 0f, -1f).normalized,  // SW
            };

            var anchors = overlapping
                ? new[] { diagonals[0], cardinals[1], diagonals[1], cardinals[2],
                          diagonals[2], cardinals[3], diagonals[3], cardinals[0] } // corner+mid weave
                : cardinals;

            var dirs = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (i < anchors.Length)
                {
                    dirs[i] = anchors[i];
                }
                else
                {
                    // Remainder: spread evenly on the circle, offset to dodge the anchors.
                    float t = (i - anchors.Length + 0.5f) / Mathf.Max(1, count - anchors.Length);
                    float ang = t * Mathf.PI * 2f + Mathf.PI * 0.25f;
                    dirs[i] = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                }
            }
            return dirs;
        }

        // Parse a wallTier string ("Wood"/"Iron"/"ReinforcedSteel") → WallTier enum.
        private static WallTier ParseTier(string s, WallTier fallback)
        {
            if (!string.IsNullOrEmpty(s) && Enum.TryParse(s, true, out WallTier t)) return t;
            return fallback;
        }

        // ── Composition: the Iron Bastion flagship layout ────────────────────

        private static GameObject Build()
        {
            var prior = GameObject.Find(RootName);
            if (prior != null) Object.DestroyImmediate(prior);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            BuildIronBastion(root.transform);
            return root;
        }

        // The flagship double-ring complex. Composes two BuildRings + boss marker +
        // court crossfire towers. Outer SOUTH gate, inner NORTH gate (opposite) →
        // the player crosses the killing courtyard under fire to reach the keep.
        private static void BuildIronBastion(Transform root)
        {
            // OUTER ring — Iron, large, gate on SOUTH only (the single approach).
            var outerGates = new bool[4] { true, false, false, false }; // S only
            float outerExtent = BuildRing(root, OuterSlotsPerSide, OuterTier,
                                          outerGates, CornerTowerPath, "Outer");

            // INNER keep ring — Steel, small, gate on NORTH only (OPPOSITE the outer
            // gate → the funnel: cross the courtyard under fire to breach the keep).
            var innerGates = new bool[4] { false, false, true, false }; // N only
            float innerExtent = BuildRing(root, InnerSlotsPerSide, InnerTier,
                                          innerGates, CornerTowerPath, "Keep");

            // BOSS spawn marker at the keep centre. Just the marker — the garrison/
            // boss spawner reads this transform and places the boss at runtime.
            var boss = new GameObject("BossSpawn");
            boss.transform.SetParent(root, false);
            boss.transform.localPosition = Vector3.zero;   // keep centre = world origin

            // COURT crossfire towers between the rings, off the inner keep corners.
            // Named "Watchtower_Court_<n>" so GarrisonController arms them too. Placed
            // partway out from each inner corner toward the outer ring, on the diagonal.
            float courtRadius = (innerExtent + outerExtent) * 0.5f;
            var courtPrefab = Resources.Load<GameObject>(CornerTowerPath);
            int courtTowers = 0;
            for (int c = 0; c < 4; c++)   // one off each diagonal (NE/NW/SE/SW)
            {
                // Diagonal directions: (±,±) on the XZ plane, normalized.
                var diag = new Vector3((c == 0 || c == 3) ? 1f : -1f, 0f,
                                       (c == 0 || c == 1) ? 1f : -1f).normalized;
                var pos = diag * courtRadius;
                if (PlaceCornerTower(root, courtPrefab, pos, $"Watchtower_Court_{c}")) courtTowers++;
            }

            Debug.Log($"[RaidBaseGenerator] '{RootName}': OUTER {OuterTier} {OuterSlotsPerSide}/side " +
                      $"(gate S, ±{outerExtent:F1}m) + KEEP {InnerTier} {InnerSlotsPerSide}/side " +
                      $"(gate N, ±{innerExtent:F1}m) + BossSpawn@centre + {courtTowers} court towers. " +
                      $"Funnel: cross the courtyard S→N under crossfire.");
        }

        // ── BuildRing: the reusable concentric primitive ─────────────────────
        // Builds ONE square ring centred on world origin: 4 corner towers via the
        // 90°-around-origin mirror, then per-side wall fill. A side's centre slot is
        // left OPEN as a gate ONLY if gateSides[s] is true; otherwise the whole side
        // is filled SOLID (no gate) → controlled, offset entry between rings.
        //
        // slotsPerSide is forced ODD so the gate is the exact CENTRE slot.
        // Returns the ring's halfExtent (corner offset) so callers can place props
        // relative to it. towerPrefabPath/ringName drive art + naming.
        private static float BuildRing(Transform root, int slotsPerSide, WallTier tier,
                                       bool[] gateSides, string towerPrefabPath, string ringName)
        {
            // ── Slot count → odd (gate is the centre, so centring is guaranteed) ─
            int n = Mathf.Max(3, slotsPerSide);
            if ((n & 1) == 0) n++;                  // safety: force ODD so the gate centres
            int gateIndex = (n - 1) / 2;
            float span = n * SegSize.x;             // wall-run length per side (ends-to-ends)

            // ── Tower footprint → derive the corner offset (extent) ──────────────
            var towerPrefab = Resources.Load<GameObject>(towerPrefabPath);
            float towerHalf = MeasureTowerHalf(towerPrefab);
            // Corner sits beyond the wall run by the tower's half-footprint, so the run
            // fills exactly the gap between the two corner towers' inner ends.
            float halfExtent = span * 0.5f + towerHalf;

            // ── Four corner WATCHTOWERS via the 90°-around-origin mirror ─────────
            // Author one corner at (H,0,-H); swing 90/180/270 around the centre (0,0,0).
            // Name MUST contain "Watchtower" → GarrisonController arms it as a turret.
            int towers = 0;
            var baseCorner = new Vector3(halfExtent, 0f, -halfExtent);
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0f, 90f * s, 0f);
                if (PlaceCornerTower(root, towerPrefab, rot * baseCorner,
                                     $"Watchtower_{ringName}_{SideName[s]}")) towers++;
            }

            // ── Fill each side; open the centre slot only where gateSides[s] ─────
            int segs = 0;
            for (int s = 0; s < 4; s++)
            {
                bool sideHasGate = gateSides != null && s < gateSides.Length && gateSides[s];
                var rot = Quaternion.Euler(0f, 90f * s, 0f);
                var midpoint = rot * new Vector3(0f, 0f, -halfExtent);
                var alongDir = rot * Vector3.right;
                for (int i = 0; i < n; i++)
                {
                    if (sideHasGate && i == gateIndex) continue;   // the gate opening (this side)
                    float along = -span * 0.5f + (i + 0.5f) * SegSize.x;
                    segs += PlaceSegment(root, midpoint + alongDir * along, rot, tier,
                                         $"{ringName}_S{SideName[s]}_{i}");
                }
            }

            Debug.Log($"[RaidBaseGenerator] ring '{ringName}': {n} slots/side, tier {tier} " +
                      $"({towers} watchtowers, {segs} wall segments); span {span:F1}m, " +
                      $"extent ±{halfExtent:F1}m, gates=[{GatesToStr(gateSides)}].");
            return halfExtent;
        }

        // Pretty-print the per-side gate flags for the log (e.g. "S").
        private static string GatesToStr(bool[] gateSides)
        {
            if (gateSides == null) return "none";
            string acc = "";
            for (int s = 0; s < 4 && s < gateSides.Length; s++)
                if (gateSides[s]) acc += (acc.Length > 0 ? "," : "") + SideName[s];
            return acc.Length > 0 ? acc : "none";
        }

        // ── Place helpers (mirrored from PerimeterWallGenerator) ─────────────

        // Half the larger horizontal extent of the corner tower prefab (its reach toward
        // the wall run). Measured from a throwaway instance; SegSize.x fallback if unknown.
        private static float MeasureTowerHalf(GameObject prefab)
        {
            if (prefab == null) return SegSize.x;
            var tmp = Object.Instantiate(prefab);
            float half = SegSize.x;
            var rends = tmp.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                half = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            }
            Object.DestroyImmediate(tmp);
            return half;
        }

        // Place a corner/court tower (seated, facing outward from the centre). The
        // caller's `name` carries the "Watchtower" token → runtime turret-arming. False
        // on missing prefab (logged, never crashes).
        private static bool PlaceCornerTower(Transform parent, GameObject prefab, Vector3 pos, string name)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[RaidBaseGenerator] tower prefab missing at Resources/{CornerTowerPath} — skipping '{name}'.");
                return false;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            go.name = name;                         // MUST contain "Watchtower" → GarrisonController arms it
            go.transform.SetParent(parent, false);
            var outward = pos.sqrMagnitude > 0.001f ? pos.normalized : Vector3.forward;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z), Vector3.up);
            go.transform.position = pos;

            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                go.transform.position += new Vector3(0f, -b.min.y, 0f);   // ground-seat
            }
            return true;
        }

        // Place one tier wall segment along a (rotated) side. PARAMETERIZED BY TIER:
        // the segment art comes from WallTierData.Get(tier).SegmentPrefabPath, so each
        // ring is built from its own wall art. Box-fit + seat are done while
        // axis-aligned (reliable world-AABB fit), THEN the wrapper is rotated to the
        // side and positioned — Y-rotation preserves the fit/seat.
        private static int PlaceSegment(Transform parent, Vector3 pos, Quaternion sideRot, WallTier tier, string name)
        {
            var prefab = Resources.Load<GameObject>(WallTierData.Get(tier).SegmentPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RaidBaseGenerator] missing Resources/{WallTierData.Get(tier).SegmentPrefabPath} (tier {tier}).");
                return 0;
            }

            var seg = new GameObject($"Wall_{name}");
            seg.transform.SetParent(parent, false);
            seg.transform.localPosition = Vector3.zero;
            seg.transform.localRotation = Quaternion.identity;     // fit while axis-aligned

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            go.transform.SetParent(seg.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // FBX imports flat → stand upright

            float seatY = 0f;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                var sc = seg.transform.localScale;
                if (b.size.x > 0.0001f) sc.x *= SegSize.x / b.size.x;
                if (b.size.y > 0.0001f) sc.y *= SegSize.y / b.size.y;
                if (b.size.z > 0.0001f) sc.z *= SegSize.z / b.size.z;
                seg.transform.localScale = sc;
                var r2 = go.GetComponentsInChildren<Renderer>(true);
                var b2 = r2[0].bounds;
                for (int k = 1; k < r2.Length; k++) b2.Encapsulate(r2[k].bounds);
                seatY = -b2.min.y;                                  // ground-seat offset
            }

            seg.transform.rotation = sideRot;
            seg.transform.position = new Vector3(pos.x, seatY, pos.z);

            var ws = seg.AddComponent<WallSegment>();
            ws.SetTier((int)tier);
            return 1;
        }
    }
}
