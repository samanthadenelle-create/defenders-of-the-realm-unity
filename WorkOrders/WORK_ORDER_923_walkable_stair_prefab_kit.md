> ## RECONCILED 2026-08-08 - true status is PARTIAL - BLOCKED ON RESEARCH
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the prefab-kit half EXISTS (`StairConnector_*_{Up,Down}.prefab`, landed `15d1081d`), but acceptance sec.6 (`dg_descent_probe` = `PathComplete`) is UNMET after FOUR failed hypothesis rounds - see WO-927. Marking this "READY TO IMPLEMENT" understates a hard research blocker and would send a session in expecting a straightforward build.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 923 — Walkable multi-level stairs (prefab kit: visual steps + invisible ramp)

**Status: PARTIAL — BLOCKED ON RESEARCH (see WO-927)** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (Grok — from `HANDOFF_GROK_DUNGEON_MULTILEVEL_NAV.md` + owner video/context)  
**Silo:** Dungeons / RoomForge bake  
**Roles:** CLI implement (Claude thrashing on plane maths — this WO freezes the recipe)  
**Source handoff:** `WorkOrders/HANDOFF_GROK_DUNGEON_MULTILEVEL_NAV.md` (read first; do not re-derive numbers)  
**Canon:** `RoomForgeCanon.cs` + `DungeonBakerChecks.FloorSeparationY` — **read, never copy-paste literals into new files without the authority**  
**Depends on:** enclose + cell widen already in tree (Cell=10, WallHeight=4, etc.)  
**Owner context:** Screen Recording 2026-08-07 184814 + still: green unlit gate, **Extract** prompt, no walkable descent — multi-level `PathPartial`

---

## 0. ★ THE ACTUAL BUG — Claude ships PORTALS, not stairs

**What is in the tree today (verified):**

```csharp
// DungeonBaker.PopulateForPlay — WO-1001 slice 1b
// "triggered floor transition — refine to walk-through later"
// "no staircase mesh, no NavMeshLink yet"
int stairPorts = DressVerticalStairPorts(...);  // → DungeonPortLink "Descend" / "Climb"
```

| What Claude built | What the player needs |
|-------------------|------------------------|
| `DungeonPortLink` fade + teleport between floor islands | **Walkable stair geometry** + continuous NavMesh |
| Prompt **Descend** / **Climb** | See steps, walk down |
| Separate navmesh islands (`PathPartial`) | **PathComplete** entry → deep |
| Extract as real escape | Stairs as diegetic progress |

**⛔ STOP RULE FOR IMPLEMENTERS (Claude / any seat):**

1. **Do NOT** treat `DressVerticalStairPorts` as the multi-level solution.  
2. **Do NOT** “finish” multi-level by adding more ports, better prompts, or Extract.  
3. **Do NOT** leave “ports for now, stairs later” in a RESULT that claims DONE.  
4. **DONE means:** player walks the stair ramp; bake log `PathComplete`; ports **removed or disabled** on pairs that have a stair prefab.

Ports may remain **only** as a temporary fallback if the stair prefab fails to load — with `FlowTrace.Warn`, never as the designed path.

---

## 0b. Why Claude is struggling (and the fix)

| Struggle | Truth |
|----------|--------|
| Portals are easier than geometry | **True and forbidden.** WO-1001 1b was an interim; owner now requires real stairs. |
| “Steps + plane” as freeform bake maths | **Wrong architecture.** Build a **prefab once**, instantiate at socket. |
| `PrimitiveType.Plane` for the walk surface | **Wrong collider.** Use a **thin Cube + BoxCollider**; **destroy MeshRenderer + MeshFilter** (`HideMesh` / KayKit cladding pattern). Plane → MeshCollider, 10×10 unit scale traps, single-sided confusion. |
| Stepped colliders for nav | **Wrong.** Stepped colliders bake a bad navmesh. **Steps = visual only (no collider). Ramp collider = only walk surface.** |
| Leaving ports as “good enough” | Ports keep Extract as the real escape. Owner: **stairs must be real** so the run is a place, not a UI dismiss. |
| Moving stair sockets | **Forbidden.** X/Z stay **0**; Y stays **±FloorSeparationY/2**. |

**Owner method (binding):** *visual stairs below; invisible ramp on the nose line; seam both landings; cut ceiling/floor holes.*

---

## 1. Proof the problem is real (do not re-measure)

| Dungeon | Multi-level path entry → deep |
|---------|--------------------------------|
| `dg_starter_loop` | PathComplete (flat — no stairs) |
| `dg_descent_probe` / vault / bonecrypt / ember | **PathPartial** — floors stacked, **nothing walkable between** |

Sockets mate; composer places rooms; **no walkable geometry / no NavMesh link**.  
`DressVerticalStairPorts` is a **teleport workaround**, not architecture.

Video still: locked **green unlit slab** + **Extract** — diegetic loop broken.

---

## 2. Frozen numbers (from authority files)

```
RoomForgeCanon.Cell               = 10.0 m
RoomForgeCanon.WallHeight         =  4.0 m
RoomForgeCanon.FloorOccupiedHeight≈ 4.4 m
DungeonBakerChecks.FloorSeparationY = 6.0 m   // rise of ONE flight
Dead air between ceiling and next slab = 6.0 − 4.4 = 1.6 m  // shaft/cut must open this
```

**Slope for V1 straight flight:** rise 6 m over run **10 m** (full 1×1 cell) = **31°** (under 45° agent max).  
**Do not** use 6 m run (45° — no margin).

---

## 3. Architecture — STAIR PREFAB KIT (not bake-time geometry)

### 3.1 Prefabs to create (editor builder, one menu)

| Prefab path | Rise | Footprint | Notes |
|-------------|------|-----------|--------|
| `Assets/Dungeon/Stairs/Stair_Straight.prefab` | exactly **6.0** m | full 10 m run × ~2.4 m width | V1 **required** |
| `Assets/Dungeon/Stairs/Stair_Left.prefab` | 6.0 via two 3.0 half-flights + landing | ~5 m + 5 m | V1.1 if straight blocks graphs |
| `Assets/Dungeon/Stairs/Stair_Right.prefab` | mirror of Left | same | genuine **mirror**, not 180° rotate |

**Builder:** `Assets/Editor/RoomForge/DefaultStairPrefabBuilder.cs`  
Menu: `Defenders/Dungeon/Build Stair Prefab Kit`  
Batch: `DefaultStairPrefabBuilder.BuildAll`

### 3.2 What each prefab CONTAINS (assemble once)

```
Stair_Straight (root local: bottom landing at y=0, run +Z, width along X)
├── VisualSteps/          // N cubes or KayKit steps — MeshRenderer ON, Collider OFF
├── RampCollider/         // thin Cube, BoxCollider ON, MeshRenderer+MeshFilter DESTROYED
├── LandingLower/         // optional thin pad collider overlap lower floor (if not part of ramp)
├── LandingUpper/         // same for upper
├── ShaftCutters/         // trigger volumes OR documented cut sizes for baker (see §4)
└── Anchors/
    ├── BottomNose        // empty at lower walk surface centre-front
    └── TopNose           // empty at upper walk surface centre-front
```

**Ramp geometry (straight V1 — no guessing):**

```
// Lower walk surface y = 0 (local). Upper walk surface y = FloorSeparationY = 6.
// Nose line: from (0, 0, 0) to (0, 6, 10) in local space if run = Cell = 10 along +Z.
// Ramp cube:
//   length along run  = 10.0 + 2*landingOverlap   // landingOverlap = 0.35 m each end
//   width along X     = 2.4
//   thickness         = 0.15  (thin)
//   centre: midpoint of nose segment, rotated so long axis = nose line
//   pitch: atan2(6, 10) ≈ 31°
```

**Landing overlap (non-negotiable):** ramp extends **≥0.35 m** onto lower floor and **≥0.35 m** onto upper floor so NavMesh regions **merge**. Hairline butt joints = PathPartial forever.

**Steps:** pure visuals under the ramp; **no colliders**. If feet clip visually, raise ramp 1–2 cm on the nose line — do not dual-collide.

**Never use `PrimitiveType.Plane`.**

### 3.3 Seating algorithm when measuring an external mesh (optional path)

Owner rule for imported stair art — V1 **procedural cubes are fine** and skip this; keep for later KayKit stairs:

1. Width axis = horizontal axis with **constant** top height.  
2. Run axis = horizontal axis with **monotonic climb**.  
3. Bottom = min Y end of run; Top = max Y end.  
4. `seatY = lowerFloorY - bounds.min.y`; yaw align run to travel.  
5. If `rise != FloorSeparationY` → **tile** or **Fail loud** — never scale treads.

Log every measure: `STAIR MEASURED widthAxis=… runAxis=… rise=… flights=…`

---

## 4. Bake integration (DungeonBaker / composer)

### 4.1 When a StairDown↔StairUp pair is mated

1. **Instantiate** `Stair_Straight` (or L/R from graph tag later) at the **mated socket world position**.  
2. Yaw so **run axis** points along the intended horizontal travel (for V1: use room’s open door facing, or +Z of the stair room).  
3. Align **BottomNose** to lower floor walk surface at the shaft; **TopNose** to upper.  
4. **Do not move** room sockets.  
5. **Cut holes** in:
   - lower room **ceiling** (if present) under the shaft  
   - upper room **floor slab** over the shaft  
   Hole size ≈ ramp width × (run segment in that slab) + margin 0.2 m.  
   Implementation options (pick one, document in RESULT):  
   - **A (preferred V1):** stair room prefabs already built **without** floor/ceiling in a centre shaft rectangle (Boolean authoring in `DefaultDungeonRoomsBuilder` for StairDown/StairUp only).  
   - **B:** baker disables/destroys colliders+renderers of floor/ceiling in a bounds check against stair shaft.  

### 4.2 NavMesh

Keep `NavMeshCollectGeometry.PhysicsColliders`. Ramp **must** keep BoxCollider. Re-bake surface after stairs placed.

### 4.3 Ports

| Phase | Behavior |
|-------|----------|
| **V1 done** | When stair prefab present and PathComplete, **do not place** Descend/Climb ports on that pair (or leave ports disabled). |
| **Fallback** | If stair prefab missing, keep existing `DressVerticalStairPorts` + FlowTrace.Warn — never silent void. |

### 4.4 Grid invariant

Stair socket local: `(0, ±FloorSeparationY/2, 0)` only. Prefab children may extend in X/Z; **socket empty stays at origin**.

---

## 5. Regression — `[stair-shell]` (required)

Add to `DungeonMultiLevelRegression` or `RoomForgeRegression`:

1. Prefab exists at path.  
2. Root-to-TopNose ΔY ≈ `FloorSeparationY` (±0.05).  
3. Exactly one (or more) **enabled** BoxCollider(s) on ramp path with **no MeshRenderer** on that GO.  
4. Visual step GOs have **no** Collider.  
5. Slope from ramp transform ≤ 44°.  
6. After bake of `dg_descent_probe`: path entry→deep_vault (or deepest) = **PathComplete**.

**Do not loosen PathComplete.**

---

## 6. Acceptance

- [ ] `Stair_Straight.prefab` built; openable in Project.  
- [ ] `dg_descent_probe` bake SUMMARY shows `PathComplete` entry→deep.  
- [ ] All multi-level dungeons PathComplete after recompose+rebake.  
- [ ] Player can **walk** down without Extract / Descend port.  
- [ ] Headless capture of stairwell opened (no floating feet, no ceiling block mid-ramp).  
- [ ] `COMPILE_GATE_OK` + multilevel + room-shell regressions green.  
- [ ] RESULT lists: prefab paths, hole strategy A/B, ports disabled yes/no, PathComplete lines.

---

## 7. Explicitly out of scope (do not smuggle)

- Legendary gate re-shade (handoff §3.1) — **after** stairs exist  
- EXIT label depth (§3.2)  
- Env_Candle play seam (§3.5)  
- Camera blue bg (§3.4)  
- NavMeshLink as primary (allowed only as emergency fallback with owner OK)

---

## 8. Implement order for CLI (no thrash)

1. Read handoff §1–§2b only.  
2. Implement `DefaultStairPrefabBuilder` → build `Stair_Straight` with **cubes**, not Plane.  
3. Hole strategy **A** on StairDown/StairUp room shells if needed.  
4. Baker: instantiate stair on mated pairs; skip ports when stair present.  
5. Recompose + rebake `dg_descent_probe` first; prove PathComplete.  
6. Roll to all multi-level graphs.  
7. Oracle + RESULT.

---

## 9. RESULT

`WorkOrders/WORK_ORDER_923_walkable_stair_prefab_kit.RESULT.md`
