# WORK ORDER 240 — Heartwood: Replace Cathedral with Living Tree Asset

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 240
**Date:** 2026-06-02
**Triggered by:** Design decision 2026-06-02 — the Heartwood is the regrowing Heart-Tree, not a stone cathedral.

---

## What needs to change

The plaza centrepiece is currently `Cathedral.fbx` (`VillageSceneBuilder.cs`, `BuildElarion()`, line ~763).
It needs to become a living, regrowing tree — twisted ancient trunk, lanterns hanging from branches,
stone well-ring at the base, green leaves at the crown, warm amber light rising through the bark.

This unblocks: intro video (WO-236), death screens (WO-235), all narrative copy that describes the Heartwood.

---

## Visual reference (owner-confirmed 2026-06-02)

The reference image shows:
- Massive twisted trunk, pale bark, spiralling growth pattern
- Hanging lanterns throughout the canopy (existing prop — check KayKit `lamp_*` or village light assets)
- Stone well-ring / raised planter around the roots
- Circular mosaic plaza ground around it
- Green leaves actively growing at the crown
- Warm amber-gold light radiating from within the bark veins

---

## Asset options (in priority order)

**Option A — Polyperfect pack** (already imported):
Check `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Nature_M/` for a large ancient tree prefab.
A large low-poly tree scaled up reads well at mobile resolution and fits the existing art style.

**Option B — KayKit Nature pack** (check if available):
`Assets/KayKit/` — KayKit includes a tree set. A large twisted tree with the right silhouette works.

**Option C — Procedural / custom build** (last resort):
Combine a large trunk cylinder + branch children + leaf billboard quads + point light for the amber glow.
Can be done entirely in `VillageSceneBuilder.cs` as a code-built GameObject tree — no FBX needed.

---

## Changes to `VillageSceneBuilder.cs`

In `BuildElarion()` (~line 763), replace:
```csharp
// OLD — cathedral
var cathedralGo = PlacePrefab("Assets/Models/Cathedral/Cathedral.fbx", Vector3.zero, ...);
```

With:
```csharp
// NEW — Heartwood living tree
var heartwoodGo = PlacePrefab("<chosen tree prefab path>", Vector3.zero, Quaternion.identity);
heartwoodGo.name = "Heartwood";
heartwoodGo.transform.localScale = Vector3.one * <scale to reach ~8m height>;

// Stone well-ring at base
var wellRing = PlacePrefab("<well or ring prefab>",
    Vector3.zero + Vector3.down * 0.1f, Quaternion.identity);
wellRing.name = "Heartwood_WellRing";

// Ambient warm light from within
var glow = new GameObject("Heartwood_Glow");
glow.transform.SetParent(heartwoodGo.transform);
glow.transform.localPosition = new Vector3(0f, 2f, 0f);
var light = glow.AddComponent<Light>();
light.type      = LightType.Point;
light.color     = new Color(1f, 0.75f, 0.3f);   // warm amber
light.intensity = 1.8f;
light.range     = 12f;

// HeartController still attaches here — the tree is the heart
heartwoodGo.AddComponent<HeartController>();
```

**Lanterns:** after placing the tree, scatter 4–6 `lamp_hanging` (KayKit or polyperfect) as children
at random branch-height positions (Y: 4–7m, random X/Z within 3m of centre).

---

## Mosaic plaza ground

The existing stone circle / mosaic plaza tile around the Heartwood base can stay or be added via
a flat disc mesh at Y=0 with a mosaic material. Low priority — the tree itself is the key visual.

---

## HeartController

`HeartController.cs` class name and code are **unchanged**. Just ensure it's added to `heartwoodGo`
instead of the cathedral. The display name "Heartwood" already propagated via DESIGN-DECISIONS.md.

---

## Acceptance criteria

- [ ] `Cathedral.fbx` no longer placed in village scene
- [ ] Living tree asset placed at (0,0,0), ~8m tall, named `"Heartwood"`
- [ ] Warm amber point light glows from within the trunk
- [ ] 4–6 hanging lanterns in the canopy as child objects
- [ ] Stone well-ring at base
- [ ] `HeartController` attached to the Heartwood GameObject
- [ ] Village scene rebakes cleanly with no z-fighting at tree base
- [ ] Brace balance check passed

## What NOT to touch
- `HeartController.cs` — no code changes
- `VillageHudController` — display name already reads "Heartwood"
- Any ATB, dungeon, or audio scripts
