# WO-279: Village2Generator — Fix Y-positioning, moat, stairs, lighting, props
**Linear:** [DEF-254](https://linear.app/defenders-of-the-realm/issue/DEF-254/village2-gateportal-arch-submerged-in-water-wall-pieces-floating-moat)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
**Priority:** Urgent — village is visually broken

## File to edit
`Assets/Editor/Village2Generator.cs`

## Issues to fix (in order)

### 1. Wall base Y position (line 151-160)
**Problem:** Walls placed at `wallHeight * 0.5f` = 3.75. The wall mesh pivot is at center, so the base floats above ground.
**Fix:** If wall mesh pivot is center, `wallHeight * 0.5f` is correct for centering the mesh. BUT verify the actual mesh pivot point first. If pivot is at the bottom, use Y=0. Test both and use whichever puts the wall base flush with Y=0 ground.

```csharp
// Check if wall pivot is center or bottom
// Center pivot: Y = wallHeight * 0.5f (current — correct IF pivot is centered)
// Bottom pivot: Y = 0
```

### 2. Gate Y position (line 189-191)
**Problem:** Gate placed at same Y as wall center. Gate arch is submerging into moat water.
**Fix:** Gate should sit at Y=0 (ground level), not at wallHeight * 0.5f. The gate arch mesh is typically bottom-pivoted.

```csharp
private void CreateGateWithTowers(Vector3 position, float yRotation)
{
    // Gate sits at ground level, not wall center height
    Vector3 gatePos = new Vector3(position.x, 0f, position.z);
    if (gatePrefab != null)
        Instantiate(gatePrefab, gatePos, Quaternion.Euler(0, yRotation, 0), villageParent);
    // Towers also at ground level
    if (towerBase != null)
    {
        Vector3 dir = Quaternion.Euler(0, yRotation, 0) * Vector3.right * 9f;
        Instantiate(towerBase, gatePos + dir, Quaternion.Euler(0, yRotation, 0), villageParent);
        Instantiate(towerBase, gatePos - dir, Quaternion.Euler(0, yRotation, 0), villageParent);
    }
}
```

### 3. Moat — ring shape, not full plane (line 213-219)
**Problem:** Single scaled plane covers the entire area including village interior. Water is everywhere.
**Fix:** Create 4 separate water planes — one per wall side — positioned in the moat channel outside the walls. Each plane is a narrow rectangle, not a village-sized quad.

```csharp
private void CreateMoat()
{
    if (moatWaterPlane == null) return;
    float w = townHalfWidth;
    float d = townHalfDepth;
    float moatWidth = 6f;  // 6m wide moat channel
    float moatY = -1.5f;   // Below ground level

    // North moat
    var north = Instantiate(moatWaterPlane, villageParent);
    north.transform.localPosition = new Vector3(0, moatY, d + moatWidth * 0.5f);
    north.transform.localScale = new Vector3(w * 2f / 10f, 1f, moatWidth / 10f);

    // South moat
    var south = Instantiate(moatWaterPlane, villageParent);
    south.transform.localPosition = new Vector3(0, moatY, -d - moatWidth * 0.5f);
    south.transform.localScale = new Vector3(w * 2f / 10f, 1f, moatWidth / 10f);

    // East moat
    var east = Instantiate(moatWaterPlane, villageParent);
    east.transform.localPosition = new Vector3(w + moatWidth * 0.5f, moatY, 0);
    east.transform.localScale = new Vector3(moatWidth / 10f, 1f, d * 2f / 10f);

    // West moat
    var west = Instantiate(moatWaterPlane, villageParent);
    west.transform.localPosition = new Vector3(-w - moatWidth * 0.5f, moatY, 0);
    west.transform.localScale = new Vector3(moatWidth / 10f, 1f, d * 2f / 10f);
}
```
**Note:** Scale divisor (10f) assumes the water plane mesh is 10x10 units. Adjust if different. The key point: 4 separate strips OUTSIDE the walls, not one giant plane.

### 4. Add stairs at each gate (stairsPrefab is unused)
**Problem:** `stairsPrefab` is exposed but never instantiated. No way to walk up to ramparts.
**Fix:** Place stairs inside the wall near each gate, oriented to face the village interior.

```csharp
// Add to CreateGateWithTowers() after tower placement:
if (stairsPrefab != null)
{
    // Stairs inside the wall, facing village interior
    Vector3 inward = Quaternion.Euler(0, yRotation, 0) * Vector3.back * 4f;
    Vector3 stairPos = new Vector3(position.x, 0f, position.z) + inward;
    // Rotate stairs to face away from wall (toward village center)
    float stairRot = yRotation + 180f;
    Instantiate(stairsPrefab, stairPos + Quaternion.Euler(0, yRotation, 0) * Vector3.right * 6f, 
        Quaternion.Euler(0, stairRot, 0), villageParent);
}
```

### 5. Implement PlaceLighting() (line 221-225)
**Fix:** Place torches at gate entrances and every 3rd wall segment.

```csharp
private void PlaceLighting()
{
    if (torchPrefab == null) return;
    float w = townHalfWidth;
    float d = townHalfDepth;
    
    // Torches at each gate (inside)
    Vector3[] gatePositions = {
        new Vector3(0, 2f, d - 2f),
        new Vector3(0, 2f, -d + 2f),
        new Vector3(w - 2f, 2f, 0),
        new Vector3(-w + 2f, 2f, 0)
    };
    foreach (var pos in gatePositions)
    {
        Instantiate(torchPrefab, pos + Vector3.left * 3f, Quaternion.identity, villageParent);
        Instantiate(torchPrefab, pos + Vector3.right * 3f, Quaternion.identity, villageParent);
    }
    
    // Torch at village center (near Heartwood)
    Instantiate(torchPrefab, new Vector3(4f, 2f, 4f), Quaternion.identity, villageParent);
    Instantiate(torchPrefab, new Vector3(-4f, 2f, 4f), Quaternion.identity, villageParent);
    Instantiate(torchPrefab, new Vector3(4f, 2f, -4f), Quaternion.identity, villageParent);
    Instantiate(torchPrefab, new Vector3(-4f, 2f, -4f), Quaternion.identity, villageParent);
}
```

### 6. Implement PlaceDistrictProps() (line 228-230)
**Fix:** Scatter props in each district zone.

```csharp
private void PlaceDistrictProps()
{
    // South crafting — crates and barrels near forge
    ScatterProps(crates, new Vector3(0, 0, -26), 12f, 8f, 4);
    ScatterProps(barrels, new Vector3(0, 0, -26), 10f, 6f, 3);
    
    // East commerce — crates near market
    ScatterProps(crates, new Vector3(28, 0, 0), 14f, 10f, 6);
    
    // North lumber — barrels and crates
    ScatterProps(barrels, new Vector3(0, 0, 26), 12f, 8f, 4);
    ScatterProps(crates, new Vector3(0, 0, 26), 10f, 6f, 3);
    
    // Vines on walls (scatter near wall positions)
    if (vines != null && vines.Length > 0)
    {
        float w = townHalfWidth;
        float d = townHalfDepth;
        for (int i = 0; i < 12; i++)
        {
            int side = i % 4;
            Vector3 pos = side switch {
                0 => new Vector3(Random.Range(-w, w), Random.Range(1f, 4f), d),
                1 => new Vector3(Random.Range(-w, w), Random.Range(1f, 4f), -d),
                2 => new Vector3(w, Random.Range(1f, 4f), Random.Range(-d, d)),
                _ => new Vector3(-w, Random.Range(1f, 4f), Random.Range(-d, d))
            };
            Instantiate(vines[Random.Range(0, vines.Length)], pos, Quaternion.identity, villageParent);
        }
    }
}

private void ScatterProps(GameObject[] props, Vector3 center, float width, float depth, int count)
{
    if (props == null || props.Length == 0) return;
    for (int i = 0; i < count; i++)
    {
        Vector3 pos = center + new Vector3(
            Random.Range(-width * 0.5f, width * 0.5f), 0,
            Random.Range(-depth * 0.5f, depth * 0.5f));
        Instantiate(props[Random.Range(0, props.Length)], pos, 
            Quaternion.Euler(0, Random.Range(0, 360), 0), villageParent);
    }
}
```

### 7. Remove debug artifacts
- Remove any debug ray/line rendering (white diagonal line visible in playtests)
- Ensure no debug cubes or placeholder objects are instantiated
- Remove the green disc object if it's created by this generator

### 8. Bridge placement at gates
Add bridge mesh spanning the moat at each gate position.

```csharp
// Add bridge field to header:
// public GameObject bridgePrefab;

// In CreateGateWithTowers(), after gate placement:
if (bridgePrefab != null)
{
    Vector3 outward = Quaternion.Euler(0, yRotation, 0) * Vector3.forward * 6f;
    Vector3 bridgePos = new Vector3(position.x, 0f, position.z) + outward;
    Instantiate(bridgePrefab, bridgePos, Quaternion.Euler(0, yRotation, 0), villageParent);
}
```

## Verification (MANDATORY per DEF-236)

After ALL fixes:
- [ ] Generate village in editor
- [ ] Enter Play mode
- [ ] Walk to ALL 4 gates — verify walls touch gates, gates sit on ground
- [ ] Verify moat water is BELOW ground in a channel (not covering village)
- [ ] Verify bridges span the moat at each gate
- [ ] Verify stairs exist at each gate and connect to ramparts
- [ ] Verify torches are placed at gates and village center
- [ ] Verify props are scattered in districts
- [ ] Verify NO floating wall segments, NO submerged structures
- [ ] Verify NO debug lines, cubes, or green disc
- [ ] Screenshot each gate from inside AND outside — attach to ticket
- [ ] Brace balance check on Village2Generator.cs

## Do NOT Touch
- Village.unity (old scene)
- VillageSceneBuilder.cs (old generator)
- Any runtime gameplay code
