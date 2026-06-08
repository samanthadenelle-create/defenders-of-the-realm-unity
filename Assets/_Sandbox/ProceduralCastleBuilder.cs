// ProceduralCastleBuilder.cs — v3 reproduction of the owner's external castle script
// (Desktop/"castle script.txt"), wrapped in DeNelle.Sandbox. Builds a thick inner/outer
// wall ring with an L-shaped walkable rampart (rampartWidth), corner towers, a reinforced
// gate with flanking walls + TWO staircases (left/right), and scattered Apocalypse_M
// debris, then auto-bakes a NavMesh.
//
// v3 changes vs the previous reproduction:
//   • new public `rampartWidth` (L-shaped rampart ring that grows inward from the outer wall)
//   • AddRampartFloor now only tiles the rampart band, not the whole interior
//   • AddReinforcedGateWithStairs replaces AddMainGate (gate + reinforced flanking walls +
//     two staircases)
//   • default dimensions length=28, width=18
//
// Differences from the source: every Instantiate is null-guarded so a missing prefab logs
// a warning rather than throwing an NRE (the source assumed all prefabs assigned).
//
// NOTE: this script builds NO floor — the base needs a ground plane added separately
// (the CastleBuilderTester does this). Walls are scaled in Y by wallHeight, which
// stretches the wall mesh vertically rather than tiling it.

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace DeNelle.Sandbox
{
    [ExecuteInEditMode]
    public class ProceduralCastleBuilder : MonoBehaviour
    {
        [Header("=== GRID SYSTEM ===")]
        [Tooltip("Overall length of the castle (X axis) in grid units")]
        public int length = 28;

        [Tooltip("Overall width of the castle (Z axis) in grid units")]
        public int width = 18;

        [Tooltip("Size of each grid cell. Most Low Poly barriers work best at 2.")]
        public float unitSize = 2f;

        [Header("=== WALL SETTINGS ===")]
        [Tooltip("Height of the main walls")]
        public float wallHeight = 5f;

        [Tooltip("Distance between outer and inner wall (creates thickness)")]
        public float wallThickness = 2.2f;

        [Header("=== RAMPART SETTINGS ===")]
        [Tooltip("How wide the walkable rampart platform is. Grows inward from the outer wall only.")]
        public int rampartWidth = 4;               // This is what you control

        [Header("=== PREFABS ===")]
        public GameObject wallPrefab;              // Outer + Inner walls use this
        public GameObject damagedWallPrefab;
        public GameObject barricadeWindow;         // Crenellations
        public GameObject platformPrefab;          // Flat rampart floor

        [Header("=== GATE & ACCESS ===")]
        public GameObject gatePrefab;
        public GameObject stairsPrefab;

        [Header("=== DEBRIS ===")]
        public GameObject[] debrisPrefabs;

        [Header("=== NAVMESH ===")]
        public bool autoBakeNavMesh = true;

        // ================================================================
        // MAIN BUILD METHOD
        // ================================================================
        [ContextMenu("Build Full Castle")]
        public void BuildCastle()
        {
            ClearExisting();

            BuildWallRing(true);     // ← Outer walls (fixed edge)
            BuildWallRing(false);    // ← Inner walls (inset by wallThickness)

            AddRampartFloor();       // Uses rampartWidth, grows inward from outer wall
            AddCornerTowers();
            AddReinforcedGateWithStairs();
            SpawnRandomDebris();

            if (autoBakeNavMesh)
                BakeNavMesh();

            Debug.Log($"✅ Castle built! Rampart Width = {rampartWidth} | Grid: {length}x{width}");
        }

        // ================================================================
        // Builds one ring of walls (outer or inner)
        // ================================================================
        void BuildWallRing(bool isOuter)
        {
            // Offset determines whether this is the outer or inner wall
            float offset = isOuter ? 0f : wallThickness * unitSize * 0.5f;

            // Bottom and Top walls
            for (int x = 0; x < length; x++)
            {
                PlaceWallSegment(x, 0, offset, isOuter);           // Bottom
                PlaceWallSegment(x, width - 1, -offset, isOuter);  // Top
            }

            // Left and Right walls
            for (int z = 0; z < width; z++)
            {
                PlaceWallSegment(0, z, offset, isOuter);           // Left
                PlaceWallSegment(length - 1, z, -offset, isOuter); // Right
            }
        }

        // ================================================================
        // Places a single wall piece + optional crenellations
        // ================================================================
        void PlaceWallSegment(int x, int z, float thicknessOffset, bool isOuter)
        {
            Vector3 pos = new Vector3(x * unitSize + thicknessOffset, wallHeight / 2f, z * unitSize);

            GameObject prefab = (Random.value < 0.3f && damagedWallPrefab != null)
                ? damagedWallPrefab
                : wallPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: wallPrefab is unassigned — skipping wall segment.");
                return;
            }

            GameObject wall = Instantiate(prefab, pos, Quaternion.identity, transform);

            // Rotate side walls (left/right)
            if (x == 0 || x == length - 1)
                wall.transform.rotation = Quaternion.Euler(0, 90, 0);

            wall.transform.localScale = new Vector3(1f, wallHeight, 1f);

            // Only outer walls get battlements
            if (isOuter && barricadeWindow != null && Random.value < 0.7f)
            {
                Vector3 crenPos = pos + new Vector3(0, wallHeight * 0.6f, 0);
                Instantiate(barricadeWindow, crenPos, wall.transform.rotation, transform);
            }
        }

        // ================================================================
        // Rampart platform - grows INWARD from outer walls only (L-shaped ring)
        // ================================================================
        void AddRampartFloor()
        {
            if (platformPrefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: platformPrefab is unassigned — skipping AddRampartFloor.");
                return;
            }

            for (int x = 1; x < length - 1; x++)
            {
                for (int z = 1; z < width - 1; z++)
                {
                    // This condition creates the "L-shaped" rampart along the outer edges
                    bool isOnRampart =
                        x < rampartWidth ||
                        x > length - rampartWidth ||
                        z < rampartWidth ||
                        z > width - rampartWidth;

                    if (isOnRampart)
                    {
                        Vector3 pos = new Vector3(x * unitSize, wallHeight + 0.1f, z * unitSize);
                        Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                    }
                }
            }
        }

        // ================================================================
        // Rest of the methods (Corner Towers, Gate, Debris, etc.)
        // ================================================================
        void AddCornerTowers()
        {
            if (wallPrefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: wallPrefab is unassigned — skipping AddCornerTowers.");
                return;
            }

            float[] xs = { 0, length - 1 };
            float[] zs = { 0, width - 1 };

            foreach (float x in xs)
            foreach (float z in zs)
            {
                Vector3 pos = new Vector3(x * unitSize, wallHeight * 0.6f, z * unitSize);
                GameObject tower = Instantiate(wallPrefab, pos, Quaternion.identity, transform);
                tower.transform.localScale = new Vector3(2.8f, wallHeight * 1.1f, 2.8f);
            }
        }

        void AddReinforcedGateWithStairs()
        {
            if (gatePrefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: gatePrefab is unassigned — skipping AddReinforcedGateWithStairs.");
                return;
            }

            Vector3 gatePos = new Vector3(length * unitSize * 0.5f, 0, 0);
            Instantiate(gatePrefab, gatePos, Quaternion.identity, transform);

            // Reinforced walls beside gate (only if a wall prefab is available)
            if (wallPrefab != null)
            {
                Vector3 leftPos = gatePos + new Vector3(-4f, wallHeight * 0.5f, 4f);
                Vector3 rightPos = gatePos + new Vector3(4f, wallHeight * 0.5f, 4f);

                var left = Instantiate(wallPrefab, leftPos, Quaternion.Euler(0, 90, 0), transform);
                var right = Instantiate(wallPrefab, rightPos, Quaternion.Euler(0, 90, 0), transform);
                left.transform.localScale = new Vector3(1f, wallHeight * 1.1f, 4f);
                right.transform.localScale = new Vector3(1f, wallHeight * 1.1f, 4f);
            }
            else
            {
                Debug.LogWarning("ProceduralCastleBuilder: wallPrefab unassigned — skipping reinforced gate flanking walls.");
            }

            // Two staircases (Left and Right)
            if (stairsPrefab != null)
            {
                Instantiate(stairsPrefab, gatePos + new Vector3(-6f, 0.1f, 6f), Quaternion.Euler(0, 90, 0), transform);
                Instantiate(stairsPrefab, gatePos + new Vector3(6f, 0.1f, 6f), Quaternion.Euler(0, -90, 0), transform);
            }
            else
            {
                Debug.LogWarning("ProceduralCastleBuilder: stairsPrefab unassigned — skipping gate staircases.");
            }
        }

        void SpawnRandomDebris()
        {
            if (debrisPrefabs == null || debrisPrefabs.Length == 0) return;

            // Rampart debris
            for (int i = 0; i < 40; i++)
            {
                int x = Random.Range(2, length - 2);
                int z = Random.Range(2, width - 2);
                Vector3 pos = new Vector3(x * unitSize + Random.Range(-0.8f, 0.8f), wallHeight + 0.3f,
                                         z * unitSize + Random.Range(-0.8f, 0.8f));

                GameObject src = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
                if (src == null) continue;
                GameObject d = Instantiate(src, pos, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
                d.transform.localScale *= Random.Range(0.8f, 1.4f);
            }
        }

        void BakeNavMesh()
        {
            // NOTE: the owner's source called UnityEngine.AI.NavMeshBuilder.ClearAllNavMeshes()
            // / BuildNavMesh() — those parameterless statics live on UnityEditor.AI.NavMeshBuilder,
            // not the runtime UnityEngine.AI.NavMeshBuilder, so the original would not compile in a
            // runtime assembly. Guard the editor bake under UNITY_EDITOR (DeNelle.Sandbox is a
            // runtime asmdef and cannot reference UnityEditor unconditionally).
#if UNITY_EDITOR
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("NavMesh baked automatically!");
#else
            Debug.LogWarning("ProceduralCastleBuilder: NavMesh bake is editor-only — skipped at runtime.");
#endif
        }

        void ClearExisting()
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}
