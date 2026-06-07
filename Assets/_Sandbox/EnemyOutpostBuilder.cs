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

        [ContextMenu("Build Enemy Outpost")]
        public void BuildOutpost()
        {
            ClearExisting();
            switch (outpostType)
            {
                case 0: BuildBarricadeStronghold(); break;
                case 1: BuildRuinedCompound(); break;
                case 2: BuildWatchtowerNest(); break;
                case 3: BuildLayeredDefenseCitadel(); break;
                default: BuildBarricadeStronghold(); break;
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
