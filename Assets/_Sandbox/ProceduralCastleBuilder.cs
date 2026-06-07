// ProceduralCastleBuilder.cs — faithful reproduction of the owner's external castle
// script (Desktop/"castle script.txt"), wrapped in DeNelle.Sandbox. Builds a thick
// inner/outer wall ring with ramparts, corner towers, a main gate + stairs, and
// scattered Apocalypse_M debris, then auto-bakes a NavMesh.
//
// Differences from the source: every Instantiate is null-guarded so a missing prefab
// logs a warning rather than throwing an NRE (the source assumed all prefabs assigned).
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
        [Header("=== Castle Settings ===")]
        public int length = 24;
        public int width = 16;
        public float unitSize = 2f;
        public float wallHeight = 5f;
        public float wallThickness = 2f;

        [Header("Main Prefabs")]
        public GameObject wallPrefab;
        public GameObject damagedWallPrefab;
        public GameObject barricadeWindow;
        public GameObject platformPrefab;       // Rampart floor

        [Header("Gate & Stairs")]
        public GameObject gatePrefab;
        public GameObject stairsPrefab;

        [Header("Debris (Apocalypse_M)")]
        public GameObject[] debrisPrefabs;      // Drag Barrel, Barrel_Empty, Blood_A/B/C, Blood_Bag_Spilled, Bed_Large_Frame_Broken, Bath_Broken, etc.

        [Header("NavMesh")]
        public bool autoBakeNavMesh = true;
        public LayerMask navMeshLayers = ~0;    // All layers by default

        [ContextMenu("Build Full Castle")]
        public void BuildCastle()
        {
            ClearExisting();

            BuildWallRing(true);   // Outer
            BuildWallRing(false);  // Inner

            AddRampartFloor();
            AddCornerTowers();
            AddMainGate();
            SpawnRandomDebris();

            if (autoBakeNavMesh)
                BakeNavMesh();

            Debug.Log("✅ Thick castle with inner/outer walls, ramparts, debris & NavMesh built!");
        }

        void BuildWallRing(bool isOuter)
        {
            float offset = isOuter ? 0 : wallThickness * unitSize * 0.5f;

            for (int x = 0; x < length; x++)
            {
                PlaceWallSegment(x, 0, offset, isOuter);
                PlaceWallSegment(x, width - 1, -offset, isOuter);
            }
            for (int z = 0; z < width; z++)
            {
                PlaceWallSegment(0, z, offset, isOuter);
                PlaceWallSegment(length - 1, z, -offset, isOuter);
            }
        }

        void PlaceWallSegment(int x, int z, float thicknessOffset, bool isOuter)
        {
            Vector3 pos = new Vector3(x * unitSize + thicknessOffset, wallHeight / 2f, z * unitSize);
            GameObject prefab = (Random.value < 0.3f && damagedWallPrefab != null) ? damagedWallPrefab : wallPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: wallPrefab is unassigned — skipping wall segment.");
                return;
            }

            GameObject wall = Instantiate(prefab, pos, Quaternion.identity, transform);

            if (x == 0 || x == length - 1)
                wall.transform.rotation = Quaternion.Euler(0, 90, 0);

            wall.transform.localScale = new Vector3(1f, wallHeight, 1f);

            if (isOuter && barricadeWindow != null && Random.value < 0.7f)
            {
                Vector3 crenPos = pos + new Vector3(0, wallHeight * 0.6f, 0);
                Instantiate(barricadeWindow, crenPos, wall.transform.rotation, transform);
            }
        }

        void AddRampartFloor()
        {
            if (platformPrefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: platformPrefab is unassigned — skipping AddRampartFloor.");
                return;
            }
            for (int x = 1; x < length - 1; x++)
                for (int z = 1; z < width - 1; z++)
                {
                    Vector3 pos = new Vector3(x * unitSize, wallHeight + 0.1f, z * unitSize);
                    Instantiate(platformPrefab, pos, Quaternion.identity, transform);
                }
        }

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

        void AddMainGate()
        {
            if (gatePrefab == null)
            {
                Debug.LogWarning("ProceduralCastleBuilder: gatePrefab is unassigned — skipping AddMainGate.");
                return;
            }
            Vector3 gatePos = new Vector3(length * unitSize * 0.5f, 0, 0);
            Instantiate(gatePrefab, gatePos, Quaternion.identity, transform);

            if (stairsPrefab != null)
            {
                Vector3 stairsPos = gatePos + new Vector3(3f, 0, 5f);
                Instantiate(stairsPrefab, stairsPos, Quaternion.Euler(0, 90, 0), transform);
            }
        }

        void SpawnRandomDebris()
        {
            if (debrisPrefabs == null || debrisPrefabs.Length == 0) return;

            // Debris on ramparts
            for (int i = 0; i < 35; i++)
            {
                int x = Random.Range(2, length - 2);
                int z = Random.Range(2, width - 2);
                Vector3 pos = new Vector3(x * unitSize + Random.Range(-0.8f, 0.8f), wallHeight + 0.3f, z * unitSize + Random.Range(-0.8f, 0.8f));
                GameObject src = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
                if (src == null) continue;
                GameObject debris = Instantiate(src, pos, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
                debris.transform.localScale *= Random.Range(0.8f, 1.3f);
            }

            // Base debris
            for (int i = 0; i < 20; i++)
            {
                int x = Random.Range(0, length);
                int z = Random.Range(0, width);
                Vector3 pos = new Vector3(x * unitSize, 0.2f, z * unitSize);
                GameObject src = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
                if (src == null) continue;
                Instantiate(src, pos, Quaternion.Euler(0, Random.Range(0, 360), Random.Range(-15f, 15f)), transform);
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
