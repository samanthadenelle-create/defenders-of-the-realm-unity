// UpgradableOutpostBuilder.cs — Grok-generated upgradable outpost generator, reproduced
// into the sandbox for a walkable test. Builds a small fortified outpost (perimeter
// walls + optional ramparts/stairs + corner towers) from grid + prefab fields.
//
// ONE INTENTIONAL CHANGE from the original Grok class: BuildPerimeterWalls leaves a
// ONE-DOOR GAP at the front-center of the bottom row (x == gridWidth/2, z == 0) so the
// outpost has an entrance. The original walled the full perimeter with no door.
//
// Also hardened: every Instantiate path null-guards its prefab so a missing assignment
// logs a warning instead of throwing (matches project convention for absent prefabs).

using UnityEngine;

namespace DeNelle.Sandbox
{
    [ExecuteInEditMode]
    public class UpgradableOutpostBuilder : MonoBehaviour
    {
        public int gridWidth = 8;
        public int gridDepth = 6;
        public float unitSize = 2f;
        [Range(0, 1)] public int upgradeLevel = 0;

        public GameObject wallPrefab;
        public GameObject platformPrefab;
        public GameObject towerPrefab;
        public GameObject stairsPrefab;

        [ContextMenu("Build / Upgrade Outpost")]
        public void BuildOutpost()
        {
            ClearExisting();
            BuildPerimeterWalls();
            if (upgradeLevel >= 1)
            {
                BuildRamparts();
                AddStairsToRamparts();
            }
            AddCornerTowers();
        }

        void BuildPerimeterWalls()
        {
            // Bottom row (z == 0) — front. Skip the center tile to leave a one-door gap.
            for (int x = 0; x < gridWidth; x++)
            {
                if (x == gridWidth / 2) continue; // ONE-DOOR GAP (entry)
                PlaceWall(x, 0);
            }
            // Top row (z == gridDepth-1) — back, full.
            for (int x = 0; x < gridWidth; x++) PlaceWall(x, gridDepth - 1);
            // Left + right columns (exclude corners already placed by the rows).
            for (int z = 1; z < gridDepth - 1; z++) PlaceWall(0, z);
            for (int z = 1; z < gridDepth - 1; z++) PlaceWall(gridWidth - 1, z);
        }

        void PlaceWall(int x, int z)
        {
            if (wallPrefab == null)
            {
                Debug.LogWarning("[UpgradableOutpostBuilder] wallPrefab is null — skipping wall.");
                return;
            }
            Vector3 pos = new Vector3(x * unitSize, 2f, z * unitSize);
            GameObject wall = Instantiate(wallPrefab, pos, Quaternion.identity, transform);
            if (wall != null && (x == 0 || x == gridWidth - 1))
                wall.transform.rotation = Quaternion.Euler(0, 90, 0);
        }

        void BuildRamparts()
        {
            if (platformPrefab == null)
            {
                Debug.LogWarning("[UpgradableOutpostBuilder] platformPrefab is null — skipping ramparts.");
                return;
            }
            for (int x = 0; x < gridWidth; x++)
                for (int z = 0; z < gridDepth; z++)
                {
                    bool isRampartArea = x <= 1 || x >= gridWidth - 2 || z <= 1 || z >= gridDepth - 2;
                    if (isRampartArea)
                    {
                        Vector3 pos = new Vector3(x * unitSize, 4.1f, z * unitSize);
                        Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                    }
                }
        }

        void AddStairsToRamparts()
        {
            if (stairsPrefab == null)
            {
                Debug.LogWarning("[UpgradableOutpostBuilder] stairsPrefab is null — skipping stairs.");
                return;
            }
            float midW = gridWidth * unitSize * 0.5f;
            float midD = gridDepth * unitSize * 0.5f;
            Instantiate(stairsPrefab, new Vector3(midW, 0.1f, 1f), Quaternion.Euler(0, 180, 0), transform);
            Instantiate(stairsPrefab, new Vector3(midW, 0.1f, (gridDepth - 1) * unitSize - 1f), Quaternion.identity, transform);
            Instantiate(stairsPrefab, new Vector3(1f, 0.1f, midD), Quaternion.Euler(0, 90, 0), transform);
            Instantiate(stairsPrefab, new Vector3((gridWidth - 1) * unitSize - 1f, 0.1f, midD), Quaternion.Euler(0, -90, 0), transform);
        }

        void AddCornerTowers()
        {
            if (towerPrefab == null)
            {
                Debug.LogWarning("[UpgradableOutpostBuilder] towerPrefab is null — skipping corner towers.");
                return;
            }
            Vector3[] corners =
            {
                new Vector3(0, 3f, 0),
                new Vector3((gridWidth - 1) * unitSize, 3f, 0),
                new Vector3(0, 3f, (gridDepth - 1) * unitSize),
                new Vector3((gridWidth - 1) * unitSize, 3f, (gridDepth - 1) * unitSize)
            };
            foreach (Vector3 pos in corners) Instantiate(towerPrefab, pos, Quaternion.identity, transform);
        }

        void ClearExisting()
        {
            while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}
