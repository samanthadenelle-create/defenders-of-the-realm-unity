using UnityEngine;

namespace DeNelle.Sandbox
{
    [ExecuteInEditMode]
    public class CastleBuilder : MonoBehaviour
    {
        [Header("=== Castle Settings ===")]
        public int courtyardSize = 8;      // Number of tiles - adjust for size
        public float tileSize = 3f;

        [Header("=== Drag Prefabs Here (Exact recommendations) ===")]
        [Tooltip("Search: Floor_Stone_3x3m")]
        public GameObject floorStonePrefab;

        [Tooltip("Search: Wall_Stone_3x3_A or B")]
        public GameObject wallStonePrefab;

        [Tooltip("Search: Wall_Stone_Corner_A")]
        public GameObject wallCornerPrefab;

        [Tooltip("Search: Stairs_House_Spiral_2m (or any spiral stairs)")]
        public GameObject spiralStairsPrefab;

        [Tooltip("Search: Tower or tall wall sections")]
        public GameObject towerBasePrefab;

        [Tooltip("For ramparts on top of walls")]
        public GameObject rampartFloorPrefab;

        [Tooltip("Main gate / door")]
        public GameObject gatePrefab;

        private Transform castleRoot;

        [ContextMenu("Build Full Castle Courtyard")]
        public void BuildCastle()
        {
            // Clear old build
            if (transform.childCount > 0)
                foreach (Transform child in transform.GetComponentsInChildren<Transform>())
                    if (child != transform) DestroyImmediate(child.gameObject);

            castleRoot = new GameObject("🏰 CastleCourtyard").transform;
            castleRoot.parent = transform;

            float half = (courtyardSize * tileSize) / 2f;
            float wallHeight = 4.5f; // Adjust to match your wall height

            BuildGround(half);
            BuildOuterWalls(half);
            BuildTowers(half, wallHeight);
            BuildRamparts(half, wallHeight);
            BuildSpiralStairs(half, wallHeight);
            BuildMainGate(half);

            Debug.Log("✅ Castle with Spiral Stairs Built! Drop your 8 key buildings inside the courtyard.");
        }

        void BuildGround(float half)
        {
            if (floorStonePrefab == null)
            {
                Debug.LogWarning("CastleBuilder: floorStonePrefab is unassigned — skipping BuildGround.");
                return;
            }
            var parent = CreateParent("Ground");
            for (int x = -courtyardSize; x <= courtyardSize; x++)
                for (int z = -courtyardSize; z <= courtyardSize; z++)
                    Instantiate(floorStonePrefab, new Vector3(x * tileSize, 0, z * tileSize), Quaternion.identity, parent);
        }

        void BuildOuterWalls(float half)
        {
            if (wallStonePrefab == null)
            {
                Debug.LogWarning("CastleBuilder: wallStonePrefab is unassigned — skipping BuildOuterWalls.");
                return;
            }
            var parent = CreateParent("OuterWalls");

            // North & South
            for (int x = -courtyardSize; x <= courtyardSize; x++)
            {
                Instantiate(wallStonePrefab, new Vector3(x * tileSize, 0, half), Quaternion.identity, parent);
                Instantiate(wallStonePrefab, new Vector3(x * tileSize, 0, -half), Quaternion.Euler(0, 180, 0), parent);
            }

            // East & West
            for (int z = -courtyardSize + 1; z < courtyardSize; z++)
            {
                Instantiate(wallStonePrefab, new Vector3(half, 0, z * tileSize), Quaternion.Euler(0, 90, 0), parent);
                Instantiate(wallStonePrefab, new Vector3(-half, 0, z * tileSize), Quaternion.Euler(0, -90, 0), parent);
            }

            // Corners
            if (wallCornerPrefab == null)
            {
                Debug.LogWarning("CastleBuilder: wallCornerPrefab is unassigned — skipping corners.");
                return;
            }
            Instantiate(wallCornerPrefab, new Vector3(half, 0, half), Quaternion.identity, parent);
            Instantiate(wallCornerPrefab, new Vector3(half, 0, -half), Quaternion.Euler(0, 90, 0), parent);
            Instantiate(wallCornerPrefab, new Vector3(-half, 0, half), Quaternion.Euler(0, -90, 0), parent);
            Instantiate(wallCornerPrefab, new Vector3(-half, 0, -half), Quaternion.Euler(0, 180, 0), parent);
        }

        void BuildTowers(float half, float wallHeight)
        {
            var parent = CreateParent("Towers");
            Vector3[] positions = { new Vector3(half, 0, half), new Vector3(half, 0, -half),
                                   new Vector3(-half, 0, half), new Vector3(-half, 0, -half) };

            foreach (var pos in positions)
            {
                if (towerBasePrefab) Instantiate(towerBasePrefab, pos, Quaternion.identity, parent);
            }
        }

        void BuildRamparts(float half, float wallHeight)
        {
            if (rampartFloorPrefab == null)
            {
                Debug.LogWarning("CastleBuilder: rampartFloorPrefab is unassigned — skipping BuildRamparts.");
                return;
            }
            var parent = CreateParent("Ramparts");

            // North & South ramparts
            for (int x = -courtyardSize + 1; x < courtyardSize; x++)
            {
                Instantiate(rampartFloorPrefab, new Vector3(x * tileSize, wallHeight, half - tileSize * 0.4f), Quaternion.identity, parent);
                Instantiate(rampartFloorPrefab, new Vector3(x * tileSize, wallHeight, -half + tileSize * 0.4f), Quaternion.identity, parent);
            }

            // East & West ramparts
            for (int z = -courtyardSize + 1; z < courtyardSize; z++)
            {
                Instantiate(rampartFloorPrefab, new Vector3(half - tileSize * 0.4f, wallHeight, z * tileSize), Quaternion.Euler(0, 90, 0), parent);
                Instantiate(rampartFloorPrefab, new Vector3(-half + tileSize * 0.4f, wallHeight, z * tileSize), Quaternion.Euler(0, 90, 0), parent);
            }
        }

        void BuildSpiralStairs(float half, float wallHeight)
        {
            if (spiralStairsPrefab == null) return;

            var parent = CreateParent("SpiralStairs");

            // Place spiral stairs inside/near towers to reach ramparts
            Instantiate(spiralStairsPrefab, new Vector3(half - 1.5f, 0, half - 1.5f), Quaternion.identity, parent);
            Instantiate(spiralStairsPrefab, new Vector3(half - 1.5f, 0, -half + 1.5f), Quaternion.identity, parent);
            Instantiate(spiralStairsPrefab, new Vector3(-half + 1.5f, 0, half - 1.5f), Quaternion.identity, parent);
            Instantiate(spiralStairsPrefab, new Vector3(-half + 1.5f, 0, -half + 1.5f), Quaternion.identity, parent);
        }

        void BuildMainGate(float half)
        {
            if (gatePrefab == null) return;
            var parent = CreateParent("Gate");
            Instantiate(gatePrefab, new Vector3(0, 0, -half), Quaternion.Euler(0, 180, 0), parent);
        }

        Transform CreateParent(string name)
        {
            var go = new GameObject(name);
            go.transform.parent = castleRoot;
            return go.transform;
        }
    }
}
