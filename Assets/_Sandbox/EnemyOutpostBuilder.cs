// EnemyOutpostBuilder.cs — Grok-generated ENEMY-outpost generator, reproduced into the
// sandbox for a walkable test. Builds a small enemy base from a grid + prefab fields, in
// one of three (plus a bonus fourth) layout variations chosen by outpostType.
//
//   outpostType 0 → Barricade Stronghold  (dense damaged perimeter + barricades + debris)
//   outpostType 1 → Ruined Compound       (sparse, broken, debris-heavy ruins)
//   outpostType 2 → Watchtower Nest       (sparse perimeter + elevated watch platforms
//                                           connected by an elevated walkway)
//   outpostType 3 → Layered Defense Citadel (bonus; three concentric rings + ramps)
//
// THE GROK SCRIPT WAS INCOMPLETE — these were completed here (see CASTLE/OUTPOST WO):
//   • PlaceDamagedWall(x,z)          was an empty stub        → implemented
//   • BuildPerimeterWallsSparse()    was called, never defined → implemented
//   • PlaceElevatedWalkway(...)      was called, never defined → implemented
// Every Instantiate path null-guards its prefab so a missing assignment logs a warning
// (Debug.LogWarning) instead of throwing — matches project convention for absent prefabs.

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeNelle.Sandbox
{
    [ExecuteInEditMode]
    public class EnemyOutpostBuilder : MonoBehaviour
    {
        // 0 = Barricade Stronghold, 1 = Ruined Compound, 2 = Watchtower Nest, 3 = Layered Citadel
        [Range(0, 3)] public int outpostType = 0;

        public int gridWidth = 10;
        public int gridDepth = 10;
        public float unitSize = 2f;

        public GameObject wallPrefab;        // intact wall
        public GameObject damagedWall;       // damaged/broken wall variant
        public GameObject barricadeWindow;   // window/firing-slit barricade piece
        public GameObject platformPrefab;    // flat platform / floor tile (watch platforms)
        public GameObject stairsPrefab;      // straight stairs (used by the citadel variant)
        public GameObject[] debrisPrefabs;   // scatter props (barrels, blood, broken furniture)

        // Guard patrol — spawn NavMeshAgent guards that walk a waypoint patrol around the
        // outpost interior/perimeter. Bodies resolve from an enemy prefab if one is found,
        // else fall back to a tinted primitive capsule. See SpawnGuards().
        public bool spawnGuards = true;
        public int guardCount = 4;

        // Surface seating — drop the WHOLE outpost root onto whatever surface it's placed on
        // (any terrain elevation), rather than assuming Y=0. See SeatRootOnSurface().
        public bool seatRootOnSurface = true;
        public LayerMask surfaceMask = ~0;   // default: hit everything

        [ContextMenu("Build Enemy Outpost")]
        public void BuildOutpost()
        {
            // Ride the outpost root on the surface BEFORE placing pieces, so every piece
            // (and the SeatAllPieces grounding pass) is relative to the surface height.
            SeatRootOnSurface();

            ClearExisting();
            switch (outpostType)
            {
                case 0: BuildBarricadeStronghold(); break;
                case 1: BuildRuinedCompound(); break;
                case 2: BuildWatchtowerNest(); break;
                case 3: BuildLayeredDefenseCitadel(); break;
                default: BuildBarricadeStronghold(); break;
            }

            // SEAT PASS — the hardcoded Y-positions above assume a center/base prefab pivot,
            // but our pack's prefab pivots differ, so pieces float. Re-seat every piece on
            // its renderer-bounds base so ground pieces sit ON the ground and elevated tiers
            // sit AT their tier height. Same bounds-bottom seat used in VisualFactory.
            SeatAllPieces();

            // STAIRS PASS — after every piece is grounded/tiered, bridge the floating stairs
            // dynamically (bounds-measured, NO hardcoded heights) from the ground up to the
            // nearest elevated deck so the citadel tiers are actually climbable.
            ConnectStairsToDecks();

            // GUARD PASS — last, after all geometry is seated/tiered and the navmesh-relevant
            // content exists. Spawns waypoints + patrol guards on the ground. Guards spawn
            // BEFORE the tester's navmesh re-bake; that's fine — GuardPatrol.Start runs at Play
            // (after the bake is saved) and is navmesh-guarded, so it can't NRE off-navmesh.
            SpawnGuards();
        }

        // ================================================================
        // GUARD PATROL PASS — waypoints + NavMeshAgent guards
        // ================================================================
        // Creates a "Waypoints" child holding empty waypoint transforms at grounded grid
        // positions around the interior/perimeter, then spawns guardCount guard GameObjects
        // (enemy prefab if resolvable, else a dark-tinted capsule fallback), each with a
        // NavMeshAgent + GuardPatrol fed the shared waypoint list. Patrol only starts once a
        // NavMesh is baked (GuardPatrol is navmesh-safe); no-ops cleanly if not.
        void SpawnGuards()
        {
            if (!spawnGuards || guardCount <= 0) return;

            // --- 1) Build the waypoint ring (grounded, parented under "Waypoints") ---
            var waypointHolder = new GameObject("Waypoints");
            waypointHolder.transform.SetParent(transform, false);

            var waypoints = new List<Transform>();

            // Patrol an inset rectangle one cell in from the perimeter, sampling its four edges.
            int minX = 1, maxX = Mathf.Max(1, gridWidth - 2);
            int minZ = 1, maxZ = Mathf.Max(1, gridDepth - 2);

            // Collect perimeter grid cells of the inset rectangle, every other cell, clockwise.
            var cells = new List<Vector2Int>();
            for (int x = minX; x <= maxX; x += 2) cells.Add(new Vector2Int(x, minZ));      // bottom edge →
            for (int z = minZ + 2; z <= maxZ; z += 2) cells.Add(new Vector2Int(maxX, z));  // right edge ↑
            for (int x = maxX - 2; x >= minX; x -= 2) cells.Add(new Vector2Int(x, maxZ));  // top edge ←
            for (int z = maxZ - 2; z > minZ; z -= 2) cells.Add(new Vector2Int(minX, z));   // left edge ↓

            if (cells.Count == 0) cells.Add(new Vector2Int(minX, minZ));

            foreach (var c in cells)
            {
                Vector3 wp = new Vector3(c.x * unitSize, 0f, c.y * unitSize);
                wp.y = GroundedY(wp);
                var t = new GameObject($"WP_{waypoints.Count}").transform;
                t.SetParent(waypointHolder.transform, true);
                t.position = wp;
                waypoints.Add(t);
            }

            // --- 2) Resolve a guard body prefab (enemy prefab → else capsule fallback) ---
            GameObject bodyPrefab = ResolveGuardBodyPrefab();
            bool usingFallback = bodyPrefab == null;

            // --- 3) Spawn the guards spread across the waypoints ---
            var guardHolder = new GameObject("Guards");
            guardHolder.transform.SetParent(transform, false);

            int spawned = 0;
            for (int i = 0; i < guardCount; i++)
            {
                Transform spawnWp = waypoints.Count > 0 ? waypoints[(i * waypoints.Count / Mathf.Max(1, guardCount)) % waypoints.Count] : null;
                Vector3 spawnPos = spawnWp != null ? spawnWp.position : transform.position;

                GameObject guard;
                if (bodyPrefab != null)
                {
                    guard = Instantiate(bodyPrefab, spawnPos, Quaternion.identity, guardHolder.transform);
                }
                else
                {
                    // Fallback: a dark-tinted primitive capsule, grounded by half its height.
                    guard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    guard.transform.SetParent(guardHolder.transform, true);
                    Vector3 p = spawnPos; p.y += 1f; // capsule pivot is centre; half-height ≈ 1
                    guard.transform.position = p;
                    var rend = guard.GetComponent<Renderer>();
                    if (rend != null && rend.sharedMaterial != null)
                    {
                        var mat = new Material(rend.sharedMaterial);
                        mat.color = new Color(0.18f, 0.18f, 0.22f); // dark slate
                        rend.sharedMaterial = mat;
                    }
                }

                if (guard == null) continue;
                guard.name = $"Guard_{i}";

                // Ensure a NavMeshAgent (enemy prefab may already have one).
                var navAgent = guard.GetComponent<NavMeshAgent>();
                if (navAgent == null) navAgent = guard.AddComponent<NavMeshAgent>();

                var patrol = guard.GetComponent<GuardPatrol>();
                if (patrol == null) patrol = guard.AddComponent<GuardPatrol>();
                patrol.SetPatrolPoints(waypoints);

                spawned++;
            }

            Debug.Log($"[EnemyOutpostBuilder] SpawnGuards: {waypoints.Count} waypoint(s), {spawned}/{guardCount} guard(s) — body={(usingFallback ? "primitive-capsule FALLBACK" : bodyPrefab.name + " (resolved prefab)")}. Patrol begins once NavMesh is baked.");
        }

        // Grounded Y for a world XZ via downward raycast against surfaceMask (same logic as
        // SeatRootOnSurface); falls back to the root's Y if nothing is hit.
        float GroundedY(Vector3 worldXZ)
        {
            Vector3 origin = new Vector3(worldXZ.x, transform.position.y + 500f, worldXZ.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f,
                                 surfaceMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return transform.position.y;
        }

        // Try to resolve an enemy body prefab for guards: Resources first (runtime-safe),
        // then AssetDatabase by name (editor-only). Returns null → caller uses the capsule
        // fallback. Names tried match the project's committed enemy art.
        GameObject ResolveGuardBodyPrefab()
        {
            string[] candidates =
            {
                "Enemy_HollowWalker",
                "Skeleton_Warrior",
                "Skeleton_Minion",
                "Skeleton_Mage",
                "Skeleton_Rogue",
            };

            // 1) "Enemies/<name>" via EnemyAssetLoader — Addressables-first, Resources/Enemies-fallback
            //    (enemies are committed + runtime-loaded per project memory).
            foreach (var name in candidates)
            {
                var fromRes = DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab(name);
                if (fromRes == null) fromRes = Resources.Load<GameObject>(name);
                if (fromRes != null) return fromRes;
            }

#if UNITY_EDITOR
            // 2) Editor: search the project for a prefab matching any candidate name.
            foreach (var name in candidates)
            {
                string[] guids = AssetDatabase.FindAssets($"{name} t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
            // 3) Editor: last resort — any prefab whose name starts with "Skeleton" or "Enemy".
            foreach (var prefix in new[] { "Skeleton", "Enemy_" })
            {
                string[] guids = AssetDatabase.FindAssets($"{prefix} t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
#endif
            return null;
        }

        // ================================================================
        // STAIRS → DECK CONNECTOR (dynamic, bounds-measured)
        // ================================================================
        // The hardcoded stair Y-positions (0.1 / 4.1) leave each stairs piece floating with
        // a gap between its top step and the deck it should reach. This pass MEASURES, from
        // the actually-seated platform pieces, the deck top heights present in the build, then
        // for every stairs piece:
        //   1. grounds its bounds.min.y on the ground (Y = root surface, i.e. 0 in local terms),
        //   2. picks the nearest deck top ABOVE the ground that the stairs should reach,
        //   3. SCALES the stairs vertically so its bounds span ground → that deck top
        //      (accepts mild step-stretch — simplest reliable visual bridge), and
        //   4. nudges it flush against the closest deck edge so it isn't floating mid-air.
        // Finally it tags the stairs "Stairs" so BakeWalkable marks the incline walkable, and
        // adds a NavMesh-friendly note. Null/empty-safe; no-op when there are no stairs or no
        // decks (e.g. non-citadel variants).
        void ConnectStairsToDecks()
        {
            // 1) Gather seated stairs + platform (deck) pieces by name.
            var stairs = new System.Collections.Generic.List<Transform>();
            var decks  = new System.Collections.Generic.List<Bounds>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform piece = transform.GetChild(i);
                if (piece == null) continue;
                string n = piece.name;
                bool isStairs   = n.IndexOf("Stair", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPlatform = !isStairs && (n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0
                                             || n.IndexOf("Platform", System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (!isStairs && !isPlatform) continue;

                if (!TryGetPieceBounds(piece, out Bounds b)) continue;
                if (isStairs) stairs.Add(piece);
                else          decks.Add(b);
            }

            if (stairs.Count == 0) return;            // nothing to connect (non-citadel)
            if (decks.Count == 0)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] ConnectStairsToDecks: no deck/platform pieces found — stairs left as-is.");
                return;
            }

            // 2) Ground reference = lowest deck base (the root surface the build sits on).
            float groundY = float.MaxValue;
            foreach (var d in decks) groundY = Mathf.Min(groundY, d.min.y);

            int connected = 0;
            foreach (var s in stairs)
            {
                if (!TryGetPieceBounds(s, out Bounds sb)) continue;

                // 2a) Ground the stair base first.
                float dropToGround = groundY - sb.min.y;
                if (Mathf.Abs(dropToGround) > 0.0001f)
                {
                    Vector3 p = s.position; p.y += dropToGround; s.position = p;
                    if (TryGetPieceBounds(s, out Bounds rb)) sb = rb;
                }

                // 2b) Find the nearest deck whose top is ABOVE the ground (the one this stair
                //     should reach), by horizontal proximity then height.
                Bounds? target = null;
                float bestDist = float.MaxValue;
                Vector3 sCenterXZ = new Vector3(sb.center.x, 0f, sb.center.z);
                foreach (var d in decks)
                {
                    float deckTop = d.max.y;
                    if (deckTop <= groundY + 0.25f) continue; // skip ground-level decks
                    float dist = Vector3.Distance(sCenterXZ, new Vector3(d.center.x, 0f, d.center.z));
                    if (dist < bestDist) { bestDist = dist; target = d; }
                }
                if (target == null) continue; // no elevated deck to reach

                Bounds deck = target.Value;
                float deckTopY = deck.max.y;
                float rise = deckTopY - groundY;
                if (rise <= 0.01f) continue;

                // 2c) SCALE the stairs vertically so its bounds span ground → deck top.
                //     (Bounds-driven, no hardcoded heights; mild step-stretch accepted.)
                float curHeight = Mathf.Max(0.001f, sb.size.y);
                float yScale = rise / curHeight;
                Vector3 ls = s.localScale;
                ls.y *= yScale;
                s.localScale = ls;

                // Re-measure + re-ground after scaling (scaling about pivot shifts bounds).
                if (TryGetPieceBounds(s, out Bounds sb2))
                {
                    float reDrop = groundY - sb2.min.y;
                    if (Mathf.Abs(reDrop) > 0.0001f)
                    {
                        Vector3 p = s.position; p.y += reDrop; s.position = p;
                        if (TryGetPieceBounds(s, out Bounds sb3)) sb2 = sb3;
                    }

                    // 2d) Slide the stairs flush against the nearest deck EDGE on the dominant
                    //     horizontal axis, so the top step meets the deck instead of floating
                    //     in open air. Move along whichever axis (X/Z) the stairs are further
                    //     from the deck centre on, until the stair's near face touches the deck.
                    float dx = deck.center.x - sb2.center.x;
                    float dz = deck.center.z - sb2.center.z;
                    Vector3 pos = s.position;
                    if (Mathf.Abs(dx) >= Mathf.Abs(dz))
                    {
                        // Approach along X — park the stair just outside the deck's X face.
                        float deckEdgeX = deck.center.x - Mathf.Sign(dx) * (deck.size.x * 0.5f);
                        float stairHalfX = sb2.size.x * 0.5f;
                        float desiredCenterX = deckEdgeX - Mathf.Sign(dx) * stairHalfX;
                        pos.x += desiredCenterX - sb2.center.x;
                    }
                    else
                    {
                        float deckEdgeZ = deck.center.z - Mathf.Sign(dz) * (deck.size.z * 0.5f);
                        float stairHalfZ = sb2.size.z * 0.5f;
                        float desiredCenterZ = deckEdgeZ - Mathf.Sign(dz) * stairHalfZ;
                        pos.z += desiredCenterZ - sb2.center.z;
                    }
                    s.position = pos;
                }

                // 2e) Ensure the bake marks this incline walkable. BakeWalkable matches the
                //     "Stairs"/"Stair" name fragment, so guarantee it's present on the piece.
                if (s.name.IndexOf("Stair", System.StringComparison.OrdinalIgnoreCase) < 0)
                    s.name = "Stairs_" + s.name;

                connected++;
            }

            // NavMesh note: BakeWalkable (CastleBuilderTester) marks "Stairs" NavigationStatic,
            // and a single vertically-scaled straight stair gives a continuous gentle-enough
            // incline that the baked NavMesh links ground→deck WITHOUT an explicit OffMeshLink.
            // If a deck top exceeds the agent's max climb after scaling, the bake simply won't
            // connect that span — add a manual OffMeshLink there (left to the scene wiring, as
            // the editor asmdef owns NavMesh, not this runtime builder).
            Debug.Log($"[EnemyOutpostBuilder] ConnectStairsToDecks: bridged {connected}/{stairs.Count} stair piece(s) ground(Y={groundY:F2})→deck.");
        }

        // Combined world-space renderer bounds for a piece; false if it has no renderers.
        bool TryGetPieceBounds(Transform piece, out Bounds bounds)
        {
            bounds = default;
            if (piece == null) return false;
            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++)
                bounds.Encapsulate(renderers[r].bounds);
            return true;
        }

        // ================================================================
        // SURFACE SEAT — ride the WHOLE outpost root on the placed surface
        // ================================================================
        // Raycast straight DOWN from high above the builder's transform position against
        // surfaceMask. If it hits ground/terrain, set the root's Y to the hit point so the
        // entire outpost rides on the surface at any elevation. If nothing is hit, leave Y
        // as-is. Null/empty-safe and editor-safe ([ExecuteInEditMode]); called at the START
        // of BuildOutpost so SeatAllPieces then grounds pieces relative to this root.
        void SeatRootOnSurface()
        {
            if (!seatRootOnSurface) return;

            Vector3 origin = transform.position + Vector3.up * 500f;
            // Ignore our own colliders / triggers so we don't seat the root on a child piece.
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f,
                                 surfaceMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = transform.position;
                p.y = hit.point.y;
                transform.position = p;
            }
            // No hit → leave Y as-is (e.g. flat-plane test scenes seated at Y=0).
        }

        // ================================================================
        // SEAT PASS — fix floating pieces (pivot-agnostic)
        // ================================================================
        // For each direct child piece, drop/raise it on Y so its combined renderer-bounds
        // BASE sits at its intended floor level. The intended floor is inferred from the
        // piece's CURRENT y (set by the placement helpers): y < 3 → ground (floor 0); else
        // → an elevated tier whose deck height IS that current y. This removes the float
        // caused by varying prefab pivots without changing any placement logic.
        void SeatAllPieces()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform piece = transform.GetChild(i);
                if (piece == null) continue;

                var renderers = piece.GetComponentsInChildren<Renderer>();
                if (renderers == null || renderers.Length == 0) continue;

                // Combined world-space renderer bounds for this piece.
                Bounds b = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                    b.Encapsulate(renderers[r].bounds);

                // Ground (floor 0) when this piece was placed near the floor; otherwise the
                // piece's current y IS its intended tier deck height.
                float targetFloorY = piece.position.y < 3f ? 0f : piece.position.y;

                // Shift on Y so bounds.min.y == targetFloorY.
                float delta = targetFloorY - b.min.y;
                if (Mathf.Abs(delta) > 0.0001f)
                {
                    Vector3 p = piece.position;
                    p.y += delta;
                    piece.position = p;
                }
            }
        }

        // ================================================================
        // VARIATION 0: Barricade Stronghold (dense, defended, debris cover)
        // ================================================================
        void BuildBarricadeStronghold()
        {
            // Solid perimeter of (mostly) intact walls with the odd damaged section.
            for (int x = 0; x < gridWidth; x++)
            {
                PlaceRandomWall(x, 0);
                PlaceRandomWall(x, gridDepth - 1);
            }
            for (int z = 1; z < gridDepth - 1; z++)
            {
                PlaceRandomWall(0, z);
                PlaceRandomWall(gridWidth - 1, z);
            }

            // Inner barricade line — windowed barricades the defenders fire from.
            int innerZ = gridDepth / 2;
            for (int x = 2; x < gridWidth - 2; x += 2)
                PlaceBarricade(x, innerZ);

            // Heavy debris for cover.
            AddDebris(40);

            Debug.Log("✅ Barricade Stronghold built.");
        }

        // ================================================================
        // VARIATION 1: Ruined Compound (sparse, broken, debris-strewn)
        // ================================================================
        void BuildRuinedCompound()
        {
            // Broken-down perimeter: many gaps, everything damaged.
            for (int x = 0; x < gridWidth; x++)
            {
                if (Random.value > 0.45f) PlaceDamagedWall(x, 0);
                if (Random.value > 0.45f) PlaceDamagedWall(x, gridDepth - 1);
            }
            for (int z = 1; z < gridDepth - 1; z++)
            {
                if (Random.value > 0.5f) PlaceDamagedWall(0, z);
                if (Random.value > 0.5f) PlaceDamagedWall(gridWidth - 1, z);
            }

            // A few collapsed interior wall stubs.
            for (int i = 0; i < 4; i++)
            {
                int rx = Random.Range(2, gridWidth - 2);
                int rz = Random.Range(2, gridDepth - 2);
                PlaceDamagedWall(rx, rz);
            }

            // Lots of debris — this is a ruin.
            AddDebris(70);

            Debug.Log("✅ Ruined Compound built.");
        }

        // ================================================================
        // VARIATION 2: Watchtower Nest (sparse perimeter + elevated platforms)
        // ================================================================
        void BuildWatchtowerNest()
        {
            // Sparse perimeter ring (every other cell).
            BuildPerimeterWallsSparse();

            // Four elevated watch platforms near the corners (a few tiles each).
            PlaceWatchPlatform(1, 1);
            PlaceWatchPlatform(gridWidth - 2, 1);
            PlaceWatchPlatform(1, gridDepth - 2);
            PlaceWatchPlatform(gridWidth - 2, gridDepth - 2);

            // Elevated walkways connecting the platforms (a perimeter catwalk).
            PlaceElevatedWalkway(1, 1, gridWidth - 2, 1);
            PlaceElevatedWalkway(gridWidth - 2, 1, gridWidth - 2, gridDepth - 2);
            PlaceElevatedWalkway(gridWidth - 2, gridDepth - 2, 1, gridDepth - 2);
            PlaceElevatedWalkway(1, gridDepth - 2, 1, 1);

            AddDebris(25);

            Debug.Log("✅ Watchtower Nest built.");
        }

        // ================================================================
        // VARIATION 3 (BONUS): Layered Defense Citadel — from the Grok appendix.
        // Enemies hold a strong vertical + sightline advantage across three rings.
        // ================================================================
        void BuildLayeredDefenseCitadel()
        {
            BuildIrregularOuterRing();
            BuildMiddleElevatedRing();
            BuildInnerCitadel();
            AddStrategicStairsAndRamps();
            AddDebris(75);
            Debug.Log("✅ Layered Defense Citadel built - Enemies have strong vertical & sightline advantage!");
        }

        void BuildIrregularOuterRing()
        {
            for (int x = 0; x < gridWidth; x += 1)
            {
                if (Random.value > 0.2f) PlaceDamagedWall(x, 0);
                if (Random.value > 0.2f) PlaceDamagedWall(x, gridDepth - 1);
            }
            for (int z = 0; z < gridDepth; z += 1)
            {
                if (Random.value > 0.25f) PlaceDamagedWall(0, z);
                if (Random.value > 0.25f) PlaceDamagedWall(gridWidth - 1, z);
            }
        }

        void BuildMiddleElevatedRing()
        {
            if (platformPrefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] platformPrefab is null — skipping middle ring.");
                return;
            }
            for (int x = 2; x < gridWidth - 2; x += 2)
                for (int z = 2; z < gridDepth - 2; z += 2)
                    if ((x + z) % 3 == 0) // staggered pattern
                    {
                        Vector3 pos = new Vector3(x * unitSize, 4.2f, z * unitSize);
                        Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                    }
        }

        void BuildInnerCitadel()
        {
            int centerX = gridWidth / 2;
            int centerZ = gridDepth / 2;

            if (platformPrefab != null)
            {
                for (int x = centerX - 2; x <= centerX + 2; x++)
                    for (int z = centerZ - 2; z <= centerZ + 2; z++)
                    {
                        Vector3 pos = new Vector3(x * unitSize, 7.5f, z * unitSize);
                        Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                    }
            }
            else
            {
                Debug.LogWarning("[EnemyOutpostBuilder] platformPrefab is null — skipping citadel platform.");
            }

            // Tall central tower (scaled wall).
            if (wallPrefab != null)
            {
                Vector3 towerPos = new Vector3(centerX * unitSize, 8f, centerZ * unitSize);
                GameObject centralTower = Instantiate(wallPrefab, towerPos, Quaternion.identity, transform);
                if (centralTower != null)
                    centralTower.transform.localScale = new Vector3(3f, 6f, 3f);
            }
            else
            {
                Debug.LogWarning("[EnemyOutpostBuilder] wallPrefab is null — skipping central tower.");
            }
        }

        void AddStrategicStairsAndRamps()
        {
            if (stairsPrefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] stairsPrefab is null — skipping ramps/stairs.");
                return;
            }
            // Outer → middle layer.
            Instantiate(stairsPrefab, new Vector3(4 * unitSize, 0.1f, 4 * unitSize), Quaternion.Euler(0, 45, 0), transform);
            Instantiate(stairsPrefab, new Vector3((gridWidth - 4) * unitSize, 0.1f, (gridDepth - 4) * unitSize), Quaternion.Euler(0, -135, 0), transform);
            // Middle → inner citadel.
            Instantiate(stairsPrefab, new Vector3((gridWidth / 2 - 2) * unitSize, 4.1f, (gridDepth / 2) * unitSize), Quaternion.Euler(0, 90, 0), transform);
        }

        // ================================================================
        // Wall / barricade helpers
        // ================================================================

        // Place a wall that is usually intact but occasionally damaged.
        void PlaceRandomWall(int x, int z)
        {
            if (Random.value < 0.25f)
            {
                PlaceDamagedWall(x, z);
                return;
            }
            if (wallPrefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] wallPrefab is null — skipping wall.");
                return;
            }
            Vector3 pos = new Vector3(x * unitSize, 2f, z * unitSize);
            GameObject wall = Instantiate(wallPrefab, pos, Quaternion.identity, transform);
            // Side walls (left/right columns) run along Z → rotate 90°.
            if (wall != null && (x == 0 || x == gridWidth - 1))
                wall.transform.rotation = Quaternion.Euler(0, 90, 0);
        }

        // COMPLETED STUB: place a damaged wall (fallback to wallPrefab), same placement +
        // side-wall rotation rule as PlaceRandomWall, but ALWAYS the damaged variant.
        void PlaceDamagedWall(int x, int z)
        {
            GameObject prefab = damagedWall != null ? damagedWall : wallPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] damagedWall and wallPrefab are both null — skipping damaged wall.");
                return;
            }
            Vector3 pos = new Vector3(x * unitSize, 2f, z * unitSize);
            GameObject wall = Instantiate(prefab, pos, Quaternion.identity, transform);
            if (wall != null && (x == 0 || x == gridWidth - 1))
                wall.transform.rotation = Quaternion.Euler(0, 90, 0);
        }

        // Place a windowed barricade (fallback to wall/damaged) along the inner firing line.
        void PlaceBarricade(int x, int z)
        {
            GameObject prefab = barricadeWindow != null ? barricadeWindow
                              : (wallPrefab != null ? wallPrefab : damagedWall);
            if (prefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] no barricade/wall prefab — skipping barricade.");
                return;
            }
            Vector3 pos = new Vector3(x * unitSize, 2f, z * unitSize);
            Instantiate(prefab, pos, Quaternion.identity, transform);
        }

        // COMPLETED UNDEFINED METHOD: sparse perimeter ring — a wall every OTHER cell around
        // the gridWidth×gridDepth edge (used by the Watchtower Nest variation).
        void BuildPerimeterWallsSparse()
        {
            // Bottom + top rows, every other cell.
            for (int x = 0; x < gridWidth; x += 2)
            {
                PlaceRandomWall(x, 0);
                PlaceRandomWall(x, gridDepth - 1);
            }
            // Left + right columns, every other cell (skip corners already covered).
            for (int z = 2; z < gridDepth - 1; z += 2)
            {
                PlaceRandomWall(0, z);
                PlaceRandomWall(gridWidth - 1, z);
            }
        }

        // ================================================================
        // Platform / walkway helpers
        // ================================================================

        // A small elevated watch platform: a 2×2 cluster of platform tiles at watch height.
        void PlaceWatchPlatform(int cx, int cz)
        {
            if (platformPrefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] platformPrefab is null — skipping watch platform.");
                return;
            }
            const float watchY = 4f;
            for (int dx = 0; dx <= 1; dx++)
                for (int dz = 0; dz <= 1; dz++)
                {
                    int x = Mathf.Clamp(cx + dx, 0, gridWidth - 1);
                    int z = Mathf.Clamp(cz + dz, 0, gridDepth - 1);
                    Vector3 pos = new Vector3(x * unitSize, watchY, z * unitSize);
                    Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                }
        }

        // COMPLETED UNDEFINED METHOD: place platform tiles in a straight line from (x1,z1)
        // to (x2,z2) at an elevated Y, forming a catwalk between watch platforms. Steps one
        // cell at a time along whichever axis differs (the callers use axis-aligned segments).
        void PlaceElevatedWalkway(int x1, int z1, int x2, int z2)
        {
            if (platformPrefab == null)
            {
                Debug.LogWarning("[EnemyOutpostBuilder] platformPrefab is null — skipping elevated walkway.");
                return;
            }
            const float walkY = 6f;
            int dx = x2 - x1;
            int dz = z2 - z1;
            int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
            if (steps == 0)
            {
                Vector3 only = new Vector3(x1 * unitSize, walkY, z1 * unitSize);
                Instantiate(platformPrefab, only, Quaternion.identity, transform);
                return;
            }
            int stepX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int stepZ = dz == 0 ? 0 : (dz > 0 ? 1 : -1);
            int x = x1, z = z1;
            for (int i = 0; i <= steps; i++)
            {
                int cx = Mathf.Clamp(x, 0, gridWidth - 1);
                int cz = Mathf.Clamp(z, 0, gridDepth - 1);
                Vector3 pos = new Vector3(cx * unitSize, walkY, cz * unitSize);
                Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                x += stepX;
                z += stepZ;
            }
        }

        // ================================================================
        // Debris + housekeeping
        // ================================================================

        void AddDebris(int count)
        {
            if (debrisPrefabs == null || debrisPrefabs.Length == 0)
                return; // debris is optional — silently skip when none assigned.

            float maxX = (gridWidth - 1) * unitSize;
            float maxZ = (gridDepth - 1) * unitSize;
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
                if (prefab == null) continue;
                Vector3 pos = new Vector3(Random.Range(0f, maxX), 0f, Random.Range(0f, maxZ));
                Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                Instantiate(prefab, pos, rot, transform);
            }
        }

        // Alias kept for parity with the Grok appendix call site naming.
        void AddDebrisEverywhere(int count) => AddDebris(count);

        void ClearExisting()
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}
