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
    // The Tree-of-Life FBX imports lying down; AutoUpright's auto-detect skips it (roundish).
    // DIAL THIS to the exact upright rotation (owner reads it off the Transform after standing
    // the tree by hand). -90 X is the common Z-up->Y-up fix; confirm/correct by eye.
    public Vector3 treeUprightEuler = new Vector3(-90f, 0f, 0f);

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
    public bool placeGateStairs = false; // OFF: auto-stairs landed disconnected ("spare steps in field"); rampart access is hand-designed
    public GameObject towerBase;        // Corner_ExteriorWide_Brick
    public GameObject gatePrefab;       // Wall_Arch
    public GameObject moatWaterPlane;

    [Header("Roads")]
    public GameObject roadStraight;     // Floor_Brick

    [Header("Ground")]
    // A continuous walkable ground plane painted with a proper URP material. Without this the
    // bare shell shows the default magenta/blue (no ground mesh under the scattered road tiles).
    // When assigned, GenerateVillage lays a flat plane at Y=0 and also re-skins the road tiles
    // so nothing renders on the missing/default shader. groundSize = the plane's half-extent in
    // metres (default comfortably covers the ±42/±33 town footprint).
    public Material groundMaterial;
    public float groundHalfSize = 70f;

    [Header("Perimeter toggle (terrain-only authoring canvas)")]
    // TERRAIN-ONLY mode: skip the entire generated perimeter — the 4 walls + gates + corner/gate
    // towers, the moat ring, and the perimeter torches — so the player authors the whole perimeter
    // from scratch on an empty canvas. Decorative houses/specialty/nature are already suppressed by
    // leaving their slots null (same mechanism as bones-only). Terrain/roads/ground + the
    // centrepiece Tree-of-Life still generate. Set via reflection by Village2Build; default OFF so
    // Village2 / Village3 / bones-only are unchanged.
    public bool skipPerimeter = false;

    [Header("Lighting")]
    public GameObject torchPrefab;

    [Header("Depth — Nature ring (encircling wood)")]
    public GameObject[] trees;
    public GameObject[] bushes;
    public GameObject[] rocks;
    public int natureCount = 110;     // total scatter density (tunable)
    public int seed = 1337;           // deterministic scatter — same seed = same wood

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

        UnityEngine.Random.InitState(seed);   // deterministic scatter — same seed = same wood

        var tree = Place(treeOfLife, Vector3.zero, 0f);
        if (tree != null)
        {
            // DEF: the centrepiece Tree-of-Life renders ON ITS SIDE when the lying-down
            // Tree_Of_Life.fbx prefab is used (its longest axis is authored horizontal),
            // because the upright correction was being trusted from treeUprightEuler —
            // which Village2Build zeroes out, so nothing stood it up.
            // FIX: DERIVE the upright correction from the tree's own renderer bounds
            // (the same self-correcting longest-axis->Y logic AutoUpright uses for the
            // nature ring) and BAKE it as the localRotation. This stands the tree up
            // regardless of which prefab is assigned (lying FBX or already-upright
            // polyperfect), in Village2, Village3, and everywhere this generator runs.
            // treeUprightEuler is used only as a fallback when bounds are too round to
            // tell (e.g. a bushy canopy), preserving the old authoring knob.
            tree.transform.localRotation = Quaternion.Euler(treeUprightEuler);
            UprightFromBounds(tree, treeUprightEuler);
            ScaleToHeight(tree, targetTreeHeight);
            SeatOnGround(tree, 0f);
            // Decorative centrepiece — the gameplay blocker is HeartController's clean capsule
            // (B1). A lying/scaled tree's mesh colliders walled off the whole plaza; strip them
            // so the centre is walkable regardless of the tree's final pose.
            StripColliders(tree);
            // DEF-267: the Tree_Of_Life FBX ships with ZERO usable materials → it renders flat
            // grey. Attach the runtime fixer so a freshly generated town's tree paints itself with
            // the ready Resources/Structures/Materials/TreeofLife_basecolor URP material on Start.
            // (The already-baked live Village2 scene is covered separately by the fixer's
            // [RuntimeInitializeOnLoadMethod] scan, which needs no scene edit.)
            if (tree.GetComponent<DeNelle.Core.TreeOfLifeMaterialFixer>() == null)
                tree.AddComponent<DeNelle.Core.TreeOfLifeMaterialFixer>();
            var trr = tree.GetComponentsInChildren<Renderer>();
            if (trr.Length > 0)
            {
                Bounds tb = trr[0].bounds;
                for (int i = 1; i < trr.Length; i++) tb.Encapsulate(trr[i].bounds);
                Debug.Log($"[Village2] Tree-of-Life upright check: bounds size={tb.size} (UPRIGHT = Y is the largest).");
            }
        }

        // Quadrants pulled outward (±23/±18) so the Tree of Life keeps a clear ~13 m plaza.
        CreateQuadrant("NE_Crafting",    new Vector3( 23, 0,  18), Quad.Crafting);
        CreateQuadrant("NW_Lumber",      new Vector3(-23, 0,  18), Quad.Residential);
        CreateQuadrant("SE_Residential", new Vector3( 23, 0, -18), Quad.Residential);
        CreateQuadrant("SW_Market",      new Vector3(-23, 0, -18), Quad.Market);

        CreateGroundPlane();   // continuous walkable ground (proper material — fixes the magenta/blue void)
        CreateRoadSystem();
        if (!skipPerimeter)
        {
            CreateWalls();
            CreateMoat();
            PlaceLighting();
        }
        else
        {
            Debug.Log("[Village2] TERRAIN-ONLY: skipped walls + gates + towers + moat + torches (player authors the perimeter).");
        }
        ScatterNatureRing();   // depth: a wood encircling the town (procedural scatter — exact pos irrelevant)

        Debug.Log("[Village2] Generated. children=" + villageParent.childCount);
    }

    private enum Quad { Crafting, Residential, Market }

    private void CreateQuadrant(string name, Vector3 center, Quad kind)
    {
        var quad = new GameObject(name).transform;
        quad.SetParent(villageParent, false);

        if (kind == Quad.Crafting)
        {
            Place(blacksmithForge, center + new Vector3(  9, 0,   9), 35f, quad, seat:true);
            Place(armorerShop,     center + new Vector3(  9, 0,  -9), -40f, quad, seat:true);
            Place(lumbermill,      center + new Vector3(-11, 0,   0), 90f, quad, seat:true);
        }
        else if (kind == Quad.Market)
        {
            Place(tavern, center + new Vector3( -9, 0,  9), 25f, quad, seat:true);
            Place(church, center + new Vector3( 10, 0, -8), 160f, quad, seat:true);
            if (marketStalls != null && marketStalls.Length > 0)
                Place(marketStalls[0], center + new Vector3(0, 0, 0), 0f, quad, seat:true);
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
                Place(house, pos, ang + 180f, quad, seat:true);   // face the house toward the quadrant centre
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

    // Lays a single flat ground plane at Y=0 under the whole town and paints it with the proper
    // URP groundMaterial. This is what fixes the "flat blue/magenta" bare-shell ground: without a
    // ground mesh the camera renders the void/skybox or the road tiles fall back to the default
    // (magenta) shader. The plane is decorative (collider stripped) — walkability comes from the
    // NavMesh bake; it just gives the authoring canvas a real surface to look at and build on.
    private void CreateGroundPlane()
    {
        if (groundMaterial == null)
        {
            Debug.Log("[Village2] Ground plane skipped — no groundMaterial assigned (would show magenta/blue void).");
            return;
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(villageParent, false);
        ground.transform.position = new Vector3(0f, 0f, 0f);
        // Unity's primitive Plane is 10x10 m at scale 1; scale so it spans 2*groundHalfSize metres.
        float s = (groundHalfSize * 2f) / 10f;
        ground.transform.localScale = new Vector3(s, 1f, s);

        var mr = ground.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = groundMaterial;

        // KEEP the collider — Build Mode positions the placement ghost (and tap-to-select) by raycasting
        // a ground collider; with none the ray misses and NO ghost ever shows. A flat plane collider at
        // Y=0 is just the floor and does not conflict with the NavMesh bake (NavMesh still owns walkability).

        Debug.Log($"[Village2] Ground plane laid at Y=0 (half-size {groundHalfSize}m) with material '{groundMaterial.name}'.");
    }

    private void CreateWalls()
    {
        float hw = townHalfWidth, hd = townHalfDepth;
        // North wall (z=+hd), gate gap at x=0 (12 m); rampart on top.
        WallRun(new Vector3(0, 0, hd), hw * 2f, 0f, gateHalfNorth);
        GateTowers(new Vector3(0, 0, hd), 0f, gateHalfNorth);
        // South (z=-hd)
        WallRun(new Vector3(0, 0, -hd), hw * 2f, 180f, gateHalfOther);
        GateTowers(new Vector3(0, 0, -hd), 180f, gateHalfOther);
        // East (x=+hw)
        WallRun(new Vector3(hw, 0, 0), hd * 2f, 90f, gateHalfOther);
        GateTowers(new Vector3(hw, 0, 0), 90f, gateHalfOther);
        // West (x=-hw)
        WallRun(new Vector3(-hw, 0, 0), hd * 2f, -90f, gateHalfOther);
        GateTowers(new Vector3(-hw, 0, 0), -90f, gateHalfOther);

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
            var w = Place(wallStraight, pos, yRot, seat:true);   // DEF-254: seat wall base at Y=0
            // DEF-254: stack the rampart walkway on the wall's ACTUAL measured top, not a
            // guessed wallHeight (which floated/sank the balcony when the mesh differed).
            if (balconyStraight != null && w != null)
            {
                float top = WorldTopY(w);
                var bal = Place(balconyStraight, new Vector3(pos.x, top, pos.z), yRot);
                if (bal != null) SeatOnGround(bal, top);
            }
        }
    }

    private void GateTowers(Vector3 pos, float yRot, float gateHalf)
    {
        // DEF-254: flank the ACTUAL opening (gateHalf per side) so towers meet the wall ends
        // instead of standing inside the gap. +2 m clears the gate arch.
        var side = Quaternion.Euler(0, yRot, 0) * new Vector3(gateHalf + 2f, 0, 0);
        Place(towerBase, pos + side, yRot, seat:true);
        Place(towerBase, pos - side, yRot, seat:true);
        Place(gatePrefab, pos, yRot, seat:true);   // gate arch base at ground, not submerged
        if (placeGateStairs && stairsPrefab != null)
            // Opt-in only (default OFF — rampart access is hand-designed; auto-stairs scattered
            // disconnected "spare steps" in the field).
            // DEF-254: stairs climb from INSIDE the wall (local -z) up to the rampart — they were
            // placed outward (+z) and landed in the moat ring.
            Place(stairsPrefab, pos + Quaternion.Euler(0, yRot, 0) * new Vector3(0, 0, -5f), yRot + 90f, seat:true);
    }

    private void CreateCornerTowers()
    {
        if (towerBase == null) return;
        float hw = townHalfWidth + 1f, hd = townHalfDepth + 1f;
        Place(towerBase, new Vector3( hw, 0,  hd), 0f, seat:true);
        Place(towerBase, new Vector3(-hw, 0,  hd), 0f, seat:true);
        Place(towerBase, new Vector3( hw, 0, -hd), 0f, seat:true);
        Place(towerBase, new Vector3(-hw, 0, -hd), 0f, seat:true);
    }

    private void CreateMoat()
    {
        if (moatWaterPlane == null) return;
        // DEF-254: water sits BELOW ground (Y=-0.6) and the ring is pushed OUT to off=10 so its
        // inner edge clears the corner towers (hd+1) — no longer floods the wall/stair base.
        // NOTE: a true sunken moat needs a carved terrain channel (separate pass); this keeps the
        // water as a clean ring outside the walls so nothing reads as "flooded".
        float hw = townHalfWidth, hd = townHalfDepth, off = 10f, w = 9f, y = -0.6f;
        var n = Place(moatWaterPlane, new Vector3(0, y, hd + off), 0f); if (n) n.transform.localScale = new Vector3(hw * 2.2f, 1, w);
        var s = Place(moatWaterPlane, new Vector3(0, y, -hd - off), 0f); if (s) s.transform.localScale = new Vector3(hw * 2.2f, 1, w);
        var e = Place(moatWaterPlane, new Vector3(hw + off, y, 0), 90f); if (e) e.transform.localScale = new Vector3(hd * 2.2f, 1, w);
        var west = Place(moatWaterPlane, new Vector3(-hw - off, y, 0), 90f); if (west) west.transform.localScale = new Vector3(hd * 2.2f, 1, w);
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

    private GameObject Place(GameObject prefab, Vector3 pos, float yRot, Transform parent = null, bool seat = false)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, pos, Quaternion.Euler(0, yRot, 0), parent != null ? parent : villageParent);
        // DEF-254: seat the piece so its renderer-bounds BOTTOM lands at pos.y, regardless of
        // the prefab's pivot (Quaternius kit pivots vary — raw placement floats/sinks pieces).
        if (seat) SeatOnGround(go, pos.y);
        return go;
    }

    // DEF-254 height hierarchy: shift a placed object vertically so the bottom of its combined
    // renderer bounds sits exactly at groundY. The single source of truth for "on the ground".
    private void SeatOnGround(GameObject go, float groundY)
    {
        if (go == null) return;
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        go.transform.position += new Vector3(0f, groundY - b.min.y, 0f);
    }

    // World-space TOP Y of a placed object's combined renderer bounds (for stacking the
    // rampart walkway on the wall's ACTUAL top instead of a guessed wallHeight).
    private float WorldTopY(GameObject go)
    {
        if (go == null) return 0f;
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return go.transform.position.y;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.max.y;
    }

    // DEF-96: stand an object up regardless of which way its FBX imported. Measures the
    // combined bounds; if the longest axis is horizontal (lying down), rotates that axis
    // to vertical. Self-correcting — no need to know the model's authored orientation.
    private void AutoUpright(GameObject go)
    {
        if (go == null) return;
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 s = b.size;
        float maxHoriz = Mathf.Max(s.x, s.z);
        // Only correct a CLEARLY lying model — guard so an upright-but-bushy tree
        // (canopy wider than trunk is tall) isn't mistakenly tipped on its side.
        if (maxHoriz <= s.y * 1.3f) return;
        if (s.x >= s.z) go.transform.Rotate(0f, 0f, -90f, Space.World);  // X longest -> bring to Y
        else            go.transform.Rotate(-90f, 0f, 0f, Space.World);  // Z longest -> bring to Y
    }

    // DEF (Tree-of-Life sideways): stand the CENTREPIECE up by measuring its combined
    // renderer bounds and rotating the longest (currently horizontal) axis to vertical Y.
    // Same self-correcting principle as AutoUpright, but: (a) it's called explicitly for
    // the centrepiece (which AutoUpright skips because its 1.3x guard treats it as "roundish"
    // once the canopy is wide), and (b) if the bounds are genuinely ambiguous (no axis clearly
    // longest), it leaves the already-applied `fallbackEuler` in place rather than guessing.
    // The rotation is baked into the tree's localRotation so it persists across rebuilds.
    private void UprightFromBounds(GameObject go, Vector3 fallbackEuler)
    {
        if (go == null) return;
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 s = b.size;
        float maxHoriz = Mathf.Max(s.x, s.z);
        // If a horizontal axis is clearly the longest, the tree is lying down -> stand it up
        // by bringing that axis to vertical (world-space rotate, composing with current rot).
        if (maxHoriz > s.y * 1.05f)
        {
            if (s.x >= s.z) go.transform.Rotate(0f, 0f, -90f, Space.World);  // X longest -> Y
            else            go.transform.Rotate(-90f, 0f, 0f, Space.World);  // Z longest -> Y
            Debug.Log($"[Village2] Tree-of-Life UprightFromBounds: was lying (bounds {s}); " +
                      $"rotated longest axis to vertical. final localEuler={go.transform.localEulerAngles}");
        }
        else
        {
            // Roundish/ambiguous -> trust the authoring knob (already applied by the caller).
            Debug.Log($"[Village2] Tree-of-Life UprightFromBounds: bounds {s} ambiguous (roundish); " +
                      $"kept fallback treeUprightEuler={fallbackEuler}.");
        }
    }

    // Strips colliders from a placed object (decorative props/centrepiece shouldn't block
    // the hero or wall off walkable space — gameplay blockers are added deliberately).
    private void StripColliders(GameObject go)
    {
        if (go == null) return;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
    }

    // DEPTH: scatters a wood ring OUTSIDE the walls — the single biggest "alive" lever for a
    // bare town. Procedural is correct here (organic scatter; exact position is irrelevant, so
    // it sidesteps the spatial-awareness problem that captured-authoring solves for structures).
    // Skips the 4 cardinal gate LANES so trees never block a gate exit. Height-normalized so
    // size reads right regardless of each pack's native scale. Deterministic via the seed.
    private void ScatterNatureRing()
    {
        bool any = (trees != null && trees.Length > 0)
                || (bushes != null && bushes.Length > 0)
                || (rocks != null && rocks.Length > 0);
        if (!any) { Debug.Log("[Village2] Nature ring skipped — no tree/bush/rock prefabs assigned."); return; }

        var nature = new GameObject("Nature").transform;
        nature.SetParent(villageParent, false);

        float innerW = townHalfWidth + 8f, innerD = townHalfDepth + 8f;  // clear of walls/towers
        float band = 22f;                                                // ring thickness
        const float laneHalf = 9f;                                       // keep gate corridors clear
        int placed = 0;

        for (int i = 0; i < natureCount; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float rW = innerW + UnityEngine.Random.Range(0f, band);
            float rD = innerD + UnityEngine.Random.Range(0f, band);
            var pos = new Vector3(Mathf.Cos(ang) * rW, 0f, Mathf.Sin(ang) * rD);

            // Don't drop anything into a cardinal gate lane (N/S corridor at x~0, E/W at z~0).
            if (Mathf.Abs(pos.x) < laneHalf || Mathf.Abs(pos.z) < laneHalf) continue;

            GameObject prefab; float targetH;
            PickNature(out prefab, out targetH);
            if (prefab == null) continue;

            var go = Place(prefab, pos, UnityEngine.Random.Range(0f, 360f), nature);
            if (go == null) continue;
            AutoUpright(go);              // stand tipped tree/bush FBXes up ("trees off 90 Z" — pack imports lying down)
            ScaleToHeight(go, targetH);   // sane size regardless of native scale
            SeatOnGround(go, 0f);
            StripColliders(go);           // decorative — never blocks the hero
            placed++;
        }
        Debug.Log($"[Village2] Nature ring: placed {placed} trees/bushes/rocks (of {natureCount} tries, gate lanes kept clear).");
    }

    // Weighted pick: mostly trees, some bushes, a few rocks — with a sensible target height each.
    private void PickNature(out GameObject prefab, out float targetH)
    {
        int r = UnityEngine.Random.Range(0, 10);
        if (r < 6 && trees != null && trees.Length > 0)
        { prefab = trees[UnityEngine.Random.Range(0, trees.Length)]; targetH = UnityEngine.Random.Range(5f, 8.5f); return; }
        if (r < 8 && bushes != null && bushes.Length > 0)
        { prefab = bushes[UnityEngine.Random.Range(0, bushes.Length)]; targetH = UnityEngine.Random.Range(1f, 2f); return; }
        if (rocks != null && rocks.Length > 0)
        { prefab = rocks[UnityEngine.Random.Range(0, rocks.Length)]; targetH = UnityEngine.Random.Range(0.8f, 1.8f); return; }
        if (trees != null && trees.Length > 0)
        { prefab = trees[UnityEngine.Random.Range(0, trees.Length)]; targetH = UnityEngine.Random.Range(5f, 8.5f); return; }
        prefab = null; targetH = 1f;
    }

    [ContextMenu("Clear Village2")]
    public void ClearVillage()
    {
        if (villageParent == null) return;
        for (int i = villageParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(villageParent.GetChild(i).gameObject);
    }
}
