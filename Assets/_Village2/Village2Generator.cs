using UnityEngine;

// Village2Generator — clean, compilable rebuild of Grok's design (gatekeeper, experiment/village2).
// 4 quadrants around a centred Tree of Life, modular Quaternius walls + balcony ramparts +
// corner towers, gate gaps at the 4 cardinals, roads, moat, torches. ±42/±33 footprint.
// Plain MonoBehaviour (Assembly-CSharp) so the editor builder can drive it by reflection.
public class Village2Generator : MonoBehaviour
{
    [Header("=== Village2 ===")]
    public Transform villageParent;

    [Header("Center")]
    public GameObject treeOfLife;

    [Header("Main Buildings")]
    public GameObject blacksmithForge;
    public GameObject armorerShop;
    public GameObject lumbermill;
    public GameObject tavern;
    public GameObject church;

    [Header("Houses")]
    public GameObject[] smallHouses;
    public GameObject[] mediumHouses;

    [Header("Market")]
    public GameObject[] marketStalls;

    [Header("Defense Kit")]
    public GameObject wallStraight;     // Wall_UnevenBrick_Straight
    public GameObject balconyStraight;  // Balcony_Simple_Straight (rampart walkway)
    public GameObject balconyCorner;    // Balcony_Simple_Corner
    public GameObject stairsPrefab;     // Stairs_Exterior_Straight
    public GameObject towerBase;        // Corner_ExteriorWide_Brick
    public GameObject gatePrefab;       // Wall_Arch
    public GameObject moatWaterPlane;

    [Header("Roads")]
    public GameObject roadStraight;     // Floor_Brick

    [Header("Lighting")]
    public GameObject torchPrefab;

    [Header("Settings (plus/minus 42/33 spec)")]
    public float townHalfWidth = 42f;
    public float townHalfDepth = 33f;
    public float wallHeight = 5f;       // gatekeeper measures Wall_UnevenBrick_Straight height + sets this
    public float wallStep = 4f;         // gatekeeper sets to measured wall width
    public float gateHalfNorth = 6f;    // 12 m north opening
    public float gateHalfOther = 3f;    // 6 m S/E/W openings
    public float targetTreeHeight = 14f; // Tree of Life scaled to this height = plaza centerpiece

    [ContextMenu("Generate Full Village2")]
    public void GenerateVillage()
    {
        if (villageParent == null) villageParent = new GameObject("Village2").transform;
        for (int i = villageParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(villageParent.GetChild(i).gameObject);

        var tree = Place(treeOfLife, Vector3.zero, 0f);
        if (tree != null) ScaleToHeight(tree, targetTreeHeight);

        // Quadrants pulled outward (±23/±18) so the Tree of Life keeps a clear ~13 m plaza.
        CreateQuadrant("NE_Crafting",    new Vector3( 23, 0,  18), Quad.Crafting);
        CreateQuadrant("NW_Lumber",      new Vector3(-23, 0,  18), Quad.Residential);
        CreateQuadrant("SE_Residential", new Vector3( 23, 0, -18), Quad.Residential);
        CreateQuadrant("SW_Market",      new Vector3(-23, 0, -18), Quad.Market);

        CreateRoadSystem();
        CreateWalls();
        CreateMoat();
        PlaceLighting();

        Debug.Log("[Village2] Generated. children=" + villageParent.childCount);
    }

    private enum Quad { Crafting, Residential, Market }

    private void CreateQuadrant(string name, Vector3 center, Quad kind)
    {
        var quad = new GameObject(name).transform;
        quad.SetParent(villageParent, false);

        if (kind == Quad.Crafting)
        {
            Place(blacksmithForge, center + new Vector3(  9, 0,   9), 35f, quad);
            Place(armorerShop,     center + new Vector3(  9, 0,  -9), -40f, quad);
            Place(lumbermill,      center + new Vector3(-11, 0,   0), 90f, quad);
        }
        else if (kind == Quad.Market)
        {
            Place(tavern, center + new Vector3( -9, 0,  9), 25f, quad);
            Place(church, center + new Vector3( 10, 0, -8), 160f, quad);
            if (marketStalls != null && marketStalls.Length > 0)
                Place(marketStalls[0], center + new Vector3(0, 0, 0), 0f, quad);
        }
        else // Residential — 4 houses on a wide ring, 90° apart so they never overlap
        {
            int count = 4;
            float ringDist = 11f;
            for (int i = 0; i < count; i++)
            {
                float ang = i * 90f + 30f;
                var pos = center + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * ringDist, 0f, Mathf.Sin(ang * Mathf.Deg2Rad) * ringDist);
                var pool = (i % 2 == 0) ? mediumHouses : smallHouses;
                var house = Pick(pool, i);
                Place(house, pos, ang + 180f, quad);   // face the house toward the quadrant centre
            }
        }
    }

    private void CreateRoadSystem()
    {
        if (roadStraight == null) return;
        float hw = townHalfWidth * 0.7f, hd = townHalfDepth * 0.7f;
        for (float x = -hw; x <= hw; x += 4f) Place(roadStraight, new Vector3(x, 0.04f, 0f), 0f);
        for (float z = -hd; z <= hd; z += 4f) Place(roadStraight, new Vector3(0f, 0.04f, z), 90f);
    }

    private void CreateWalls()
    {
        float hw = townHalfWidth, hd = townHalfDepth;
        // North wall (z=+hd), gate gap at x=0 (12 m); rampart on top.
        WallRun(new Vector3(0, 0, hd), hw * 2f, 0f, gateHalfNorth);
        GateTowers(new Vector3(0, 0, hd), 0f);
        // South (z=-hd)
        WallRun(new Vector3(0, 0, -hd), hw * 2f, 180f, gateHalfOther);
        GateTowers(new Vector3(0, 0, -hd), 180f);
        // East (x=+hw)
        WallRun(new Vector3(hw, 0, 0), hd * 2f, 90f, gateHalfOther);
        GateTowers(new Vector3(hw, 0, 0), 90f);
        // West (x=-hw)
        WallRun(new Vector3(-hw, 0, 0), hd * 2f, -90f, gateHalfOther);
        GateTowers(new Vector3(-hw, 0, 0), -90f);

        CreateCornerTowers();
    }

    // Lays wall pieces edge-to-edge along a segment centred on `center`, skipping the gate gap
    // at the centre, plus a balcony walkway on top at wallHeight.
    private void WallRun(Vector3 center, float length, float yRot, float gateHalf)
    {
        if (wallStraight == null) return;
        float step = Mathf.Max(0.5f, wallStep);
        int segs = Mathf.CeilToInt(length / step);
        bool alongX = (yRot == 0f || yRot == 180f);
        for (int i = 0; i <= segs; i++)
        {
            float off = -length / 2f + i * step;
            if (Mathf.Abs(off) < gateHalf) continue; // gate opening
            var pos = center;
            if (alongX) pos.x = center.x + off; else pos.z = center.z + off;
            Place(wallStraight, pos, yRot);
            if (balconyStraight != null)
                Place(balconyStraight, pos + Vector3.up * wallHeight, yRot);
        }
    }

    private void GateTowers(Vector3 pos, float yRot)
    {
        var side = Quaternion.Euler(0, yRot, 0) * new Vector3(gateHalfOther + 2f, 0, 0);
        Place(towerBase, pos + side, yRot);
        Place(towerBase, pos - side, yRot);
        Place(gatePrefab, pos, yRot);
        if (stairsPrefab != null)
            Place(stairsPrefab, pos + Quaternion.Euler(0, yRot, 0) * new Vector3(0, 0, 5f), yRot + 90f);
    }

    private void CreateCornerTowers()
    {
        if (towerBase == null) return;
        float hw = townHalfWidth + 1f, hd = townHalfDepth + 1f;
        Place(towerBase, new Vector3( hw, 0,  hd), 0f);
        Place(towerBase, new Vector3(-hw, 0,  hd), 0f);
        Place(towerBase, new Vector3( hw, 0, -hd), 0f);
        Place(towerBase, new Vector3(-hw, 0, -hd), 0f);
    }

    private void CreateMoat()
    {
        if (moatWaterPlane == null) return;
        float hw = townHalfWidth, hd = townHalfDepth, off = 7f, w = 9f;
        var n = Place(moatWaterPlane, new Vector3(0, -0.3f, hd + off), 0f); if (n) n.transform.localScale = new Vector3(hw * 2.2f, 1, w);
        var s = Place(moatWaterPlane, new Vector3(0, -0.3f, -hd - off), 0f); if (s) s.transform.localScale = new Vector3(hw * 2.2f, 1, w);
        var e = Place(moatWaterPlane, new Vector3(hw + off, -0.3f, 0), 90f); if (e) e.transform.localScale = new Vector3(hd * 2.2f, 1, w);
        var west = Place(moatWaterPlane, new Vector3(-hw - off, -0.3f, 0), 90f); if (west) west.transform.localScale = new Vector3(hd * 2.2f, 1, w);
    }

    private void PlaceLighting()
    {
        if (torchPrefab == null) return;
        float hw = townHalfWidth, hd = townHalfDepth;
        Place(torchPrefab, new Vector3(0, 3.5f, hd - 3), 0f);
        Place(torchPrefab, new Vector3(0, 3.5f, -hd + 3), 180f);
        Place(torchPrefab, new Vector3(hw - 3, 3.5f, 0), 90f);
        Place(torchPrefab, new Vector3(-hw + 3, 3.5f, 0), -90f);
    }

    // helpers
    private void ScaleToHeight(GameObject go, float targetH)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float h = b.size.y;
        if (h > 0.01f) go.transform.localScale *= (targetH / h);
    }

    private GameObject Pick(GameObject[] pool, int i)
    {
        if (pool == null || pool.Length == 0) return null;
        return pool[i % pool.Length];
    }

    private GameObject Place(GameObject prefab, Vector3 pos, float yRot, Transform parent = null)
    {
        if (prefab == null) return null;
        return Instantiate(prefab, pos, Quaternion.Euler(0, yRot, 0), parent != null ? parent : villageParent);
    }

    [ContextMenu("Clear Village2")]
    public void ClearVillage()
    {
        if (villageParent == null) return;
        for (int i = villageParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(villageParent.GetChild(i).gameObject);
    }
}
