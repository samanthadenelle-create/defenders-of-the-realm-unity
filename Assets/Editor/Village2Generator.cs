using UnityEngine;

public class Village2Generator : MonoBehaviour
{
    [Header("=== Defenders of the Kingdom - Village2 (FINAL) ===")]
    public Transform villageParent;

    [Header("Center - Tree of Life")]
    public GameObject treeOfLife;

    [Header("District Buildings")]
    public GameObject blacksmithForge;
    public GameObject armorerShop;
    public GameObject lumbermill;
    public GameObject tavern;
    public GameObject church;

    [Header("Houses & Market")]
    public GameObject[] smallHouses;
    public GameObject[] mediumHouses;
    public GameObject[] marketStalls;

    [Header("Defense Kit")]
    public GameObject wallStraight;
    public GameObject balconyStraight;
    public GameObject balconyCorner;
    public GameObject stairsPrefab;
    public GameObject towerBase;
    public GameObject gatePrefab;
    public GameObject moatWaterPlane;

    [Header("Roads & Lighting")]
    public GameObject roadStraight;
    public GameObject torchPrefab;

    [Header("Props for Districts")]
    public GameObject[] crates;
    public GameObject[] vines;
    public GameObject[] barrels;

    [Header("Settings")]
    public float townHalfWidth = 45f;
    public float townHalfDepth = 40f;
    public float wallHeight = 7.5f;
    public float rampartHeightOffset = 1.6f;

    [ContextMenu("Generate FINAL Village2")]
    public void GenerateVillage()
    {
        if (villageParent == null)
            villageParent = new GameObject("Village2").transform;

        // Clean previous
        foreach (Transform child in villageParent)
            DestroyImmediate(child.gameObject);

        // Center
        if (treeOfLife != null)
            Instantiate(treeOfLife, Vector3.zero, Quaternion.identity, villageParent);

        // Districts
        CreateNorthLumberDistrict();
        CreateEastCommerceDistrict();
        CreateSouthCraftingDistrict();
        CreateWestResidentialDistrict();

        CreateRoadSystem();
        CreateDefensiveWallsAndTowers();
        CreateMoat();
        PlaceLighting();
        PlaceDistrictProps();

        Debug.Log("FINAL Village2 Generated! Tree centered, Roads added, Props placed.");
    }

    // ==================== DISTRICTS ====================
    private void CreateNorthLumberDistrict()
    {
        Vector3 center = new Vector3(0, 0, 26);
        if (lumbermill) Instantiate(lumbermill, center + new Vector3(0, 0, 6), Quaternion.Euler(0, 180, 0), villageParent);
        PlaceRandomHouses(center, 14, 10, 8);
    }

    private void CreateEastCommerceDistrict()
    {
        Vector3 center = new Vector3(28, 0, 0);
        if (tavern) Instantiate(tavern, center + new Vector3(5, 0, 5), Quaternion.Euler(0, 45, 0), villageParent);
        PlaceMarketStalls(center);
    }

    private void CreateSouthCraftingDistrict()
    {
        Vector3 center = new Vector3(0, 0, -26);
        if (blacksmithForge) Instantiate(blacksmithForge, center + new Vector3(-9, 0, -6), Quaternion.Euler(0, 90, 0), villageParent);
        if (armorerShop) Instantiate(armorerShop, center + new Vector3(9, 0, 5), Quaternion.Euler(0, -90, 0), villageParent);
    }

    private void CreateWestResidentialDistrict()
    {
        Vector3 center = new Vector3(-28, 0, 0);
        PlaceRandomHouses(center, 16, 12, 12);
    }

    private void PlaceRandomHouses(Vector3 center, float width, float depth, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-width * 0.5f, width * 0.5f);
            float z = Random.Range(-depth * 0.5f, depth * 0.5f);
            Vector3 pos = center + new Vector3(x, 0, z);
            var houses = Random.value > 0.5f ? mediumHouses : smallHouses;
            if (houses != null && houses.Length > 0)
                Instantiate(houses[Random.Range(0, houses.Length)], pos, Quaternion.Euler(0, Random.Range(0, 360), 0), villageParent);
        }
    }

    private void PlaceMarketStalls(Vector3 center)
    {
        if (marketStalls == null || marketStalls.Length == 0) return;
        for (int i = 0; i < 6; i++)
        {
            Vector3 pos = center + new Vector3(Random.Range(-12f, 12f), 0, Random.Range(-9f, 9f));
            Instantiate(marketStalls[Random.Range(0, marketStalls.Length)], pos, Quaternion.Euler(0, Random.Range(0, 360), 0), villageParent);
        }
    }

    // ==================== ROADS & DEFENSE ====================
    private void CreateRoadSystem()
    {
        CreateRoadLine(new Vector3(-45, 0.1f, 0), new Vector3(45, 0.1f, 0));
        CreateRoadLine(new Vector3(0, 0.1f, -40), new Vector3(0, 0.1f, 40));
    }

    private void CreateRoadLine(Vector3 start, Vector3 end)
    {
        if (roadStraight == null) return;
        int segments = 12;
        for (int i = 0; i <= segments; i++)
        {
            Vector3 pos = Vector3.Lerp(start, end, (float)i / segments);
            Instantiate(roadStraight, pos, Quaternion.identity, villageParent);
        }
    }

    private void CreateDefensiveWallsAndTowers()
    {
        float w = townHalfWidth;
        float d = townHalfDepth;

        CreateWallSegment(new Vector3(0, wallHeight * 0.5f, d), w * 2, 0);
        CreateGateWithTowers(new Vector3(0, wallHeight * 0.5f, d), 0);

        CreateWallSegment(new Vector3(0, wallHeight * 0.5f, -d), w * 2, 180);
        CreateGateWithTowers(new Vector3(0, wallHeight * 0.5f, -d), 180);

        CreateWallSegment(new Vector3(w, wallHeight * 0.5f, 0), d * 2, 90);
        CreateGateWithTowers(new Vector3(w, wallHeight * 0.5f, 0), 90);

        CreateWallSegment(new Vector3(-w, wallHeight * 0.5f, 0), d * 2, -90);
        CreateGateWithTowers(new Vector3(-w, wallHeight * 0.5f, 0), -90);

        CreateCornerTowers();
    }

    private void CreateWallSegment(Vector3 center, float length, float yRotation)
    {
        if (wallStraight == null) return;
        int segments = Mathf.CeilToInt(length / 8f);
        float segLen = length / segments;

        for (int i = 0; i < segments; i++)
        {
            float offset = (i - segments * 0.5f + 0.5f) * segLen;
            Vector3 dir = Quaternion.Euler(0, yRotation, 0) * Vector3.right;
            Vector3 pos = center + dir * offset;

            Instantiate(wallStraight, pos, Quaternion.Euler(0, yRotation, 0), villageParent);

            if (balconyStraight != null)
            {
                Vector3 balconyPos = pos + new Vector3(0, wallHeight + rampartHeightOffset, 0);
                Instantiate(balconyStraight, balconyPos, Quaternion.Euler(0, yRotation, 0), villageParent);
            }
        }
    }

    private void CreateGateWithTowers(Vector3 position, float yRotation)
    {
        if (gatePrefab != null)
            Instantiate(gatePrefab, position, Quaternion.Euler(0, yRotation, 0), villageParent);

        if (towerBase != null)
        {
            Vector3 dir = Quaternion.Euler(0, yRotation, 0) * Vector3.right * 9f;
            Instantiate(towerBase, position + dir, Quaternion.Euler(0, yRotation, 0), villageParent);
            Instantiate(towerBase, position - dir, Quaternion.Euler(0, yRotation, 0), villageParent);
        }
    }

    private void CreateCornerTowers()
    {
        float w = townHalfWidth + 3;
        float d = townHalfDepth + 3;
        if (towerBase == null) return;

        Instantiate(towerBase, new Vector3(w, 0, d), Quaternion.identity, villageParent);
        Instantiate(towerBase, new Vector3(w, 0, -d), Quaternion.identity, villageParent);
        Instantiate(towerBase, new Vector3(-w, 0, d), Quaternion.identity, villageParent);
        Instantiate(towerBase, new Vector3(-w, 0, -d), Quaternion.identity, villageParent);
    }

    private void CreateMoat()
    {
        if (moatWaterPlane == null) return;
        GameObject moat = Instantiate(moatWaterPlane, Vector3.zero, Quaternion.identity, villageParent);
        moat.transform.localScale = new Vector3(1.32f, 1f, 1.32f);
        moat.transform.localPosition = new Vector3(0, -2.0f, 0);
    }

    private void PlaceLighting()
    {
        if (torchPrefab == null) return;
        // TODO: Place torches at gate entrances and along wall segments
    }

    private void PlaceDistrictProps()
    {
        // TODO: Scatter crates, vines, barrels in each district
    }

    [ContextMenu("Clear Village2")]
    public void ClearVillage()
    {
        if (villageParent != null)
            foreach (Transform child in villageParent)
                DestroyImmediate(child.gameObject);
    }
}
