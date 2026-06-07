# CityManifest.draft.json — README for CLI

**WO-189a (parallel village-redesign data lane). Author: UI/architect. DO NOT touch `VillageSceneBuilder*.cs` to land this — this is DATA only.**

This manifest is the durable source list `VillageSceneBuilder` should consume on every rebake
(per `DESIGN_ELARION_CITY.md` §0/§6 — the empty-city fix). CLI wires the builder to read it.

## Schema
```
{
  "meta":      { grounding constants + provenance },
  "buildings": [ { id, prefab, prefabKind, pos:[x,0,z], rotY, district, purpose, secondaryPrefab? } ],
  "props":     [ { prefab, prefabKind, pos:[x,0,z], rotY, group } ],
  "wardens":   [ { npc, prefab, atBuilding, heldProp, anim, pos:[x,0,z], rotY } ],
  "roads":     [ { id, from, to, tilePrefab, notes } ],
  "bridges":   [ { gate, prefab, pos:[x,0,z], rotY } ]
}
```
- `prefabKind`: `"polyperfect"` = path under `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/<Cat>_M/<Name>.prefab`;
  `"kaykit_hex"` = FBX under `Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/buildings/<color>/<name>.fbx`
  (team-color variant — pick one consistent color when instancing; `_blue` chosen here, swap freely).
- `pos` is world XZ at ground Y=0; the builder applies its own feet-snap/normalize as it does today (Content.cs `NormalizeProp`/`SnapFeetToParent`).

## Grounding constants (cite when verifying)
- **Inner curtain wall (buildable extent):** `WallHalfX = 28` (E-W / X), `WallHalfZ = 21` (N-S / Z),
  south bow-out to Z = -25 (`SouthBowDepth = 4`). — `Assets/_Modules/Village/Walls/WallLayout.cs:126-135`
- **Cardinal gate openings (the only wall gaps):** N (0,+21), E (+28,0), S (0,-25 on the bow face), W (-28,0).
  — `WallLayout.cs` gate table (`GateAngles` / run mid-points, lines 246-275).
- **Heart of Elarion** authored at (0,0,1), plaza ~±5 hex (≈±8m) around origin. — `VillageSceneBuilder.Content.cs:116`, `BuildPlaza` lines 15-43.
- **Roads:** 2-tile cross from plaza edge to each gate along X and Z axes. — `Content.cs BuildRoads` lines 50-76.
- **Existing 5 gameplay buildings (kept, do not duplicate):** Pet House (20,10,55°), Arcane Tower (-20,-10,0°),
  Forge (20,-10,215°), Farm (-15,14,270°), Market (15,-20,0°). — `Content.cs Buildings[]` lines 400-455.
  This manifest's `buildings` list REPLACES + EXPANDS that array (it includes those 5 at their current
  transforms plus the full roster). CLI: either point the builder at this list, or merge.
- **Gate clearance rule:** keep building centroids ≥6m off every gate opening (DESIGN §3; the builder's own
  `ValidateBuildingGateClearance`, Content.cs:462, asserts ≥8m vs the OUTER 33/42 ring — all placements here
  also clear the inner 28/21 gate openings by ≥6m). Roads/props may sit in the lane; buildings may not.

## Assumptions CLI should verify before wiring
1. **Two wall rings exist.** The polyperfect VISUAL perimeter is at 42/33 (`VillageSceneBuilder.Walls.cs:437-438`),
   but the GAMEPLAY wall + gate openings are the WallLayout 28/21 ring. All buildings here fit INSIDE 28/21.
   Confirm which ring is the collision/navmesh wall before trusting clearances. (Spawn lanes run through the 28/21 gates.)
2. **Towers = KayKit `building_watchtower`** per DESIGN §3 owner note (NOT the ornate `Tower_Medieval_Big`).
   4 corner towers placed just inside the inner corners. Defenses otherwise live on the rampart (WO-181) — NOT placed here.
3. **KayKit team color:** `_blue` used throughout for consistency; harmless to swap to neutral/green.
4. **Heart set** (Altar + Pillars + Statues + Candlesticks) is authored here as props ringing (0,0,1); the builder
   already builds the Tree-of-Life + standing-stone ring at the Heart (Content.cs `BuildElarion`) — these props
   are ADDITIVE dressing around it; verify no overlap with the existing 6-stone ring (radius 4.4) before placing the inner pillars.
5. **Wardens** reference polyperfect People_M FBX (`Man_Sir` etc.) + Tools_M held props (`Hammer`). The "anim" field is
   a hint string for AnimatorSetup/AmbientNPC; CLI maps it to a real clip. Blacksmith Warden sits at the Forge anvil.
6. **Counts:** ~30 buildings (incl. 4 towers + Heart), ~95 props, 6 wardens, 4 bridges, 4 road axes. Tune density to perf budget on Seeker.
