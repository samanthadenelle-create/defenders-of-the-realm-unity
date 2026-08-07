# WORK ORDER 919 — RoomForge dungeons: taller walls + ceilings + kill blue sky

**Status: READY TO IMPLEMENT**  
**Minted:** 2026-08-07 (CLI / Grok — owner screenshots 12:29 / 12:30 + review)  
**Silo:** World / Dungeon bake (RoomForge — no hand-edit of `.unity`)  
**Roles:** CLI implement + batch re-bake; PO felt-closes “reads as interior”  
**Depends on:** Room library + composed bake path already shipping (WO-1001 Phase 2 / starter loop)  
**Related:** WO-1000 (KayKit **outpost** only — different builder); Healer’s Cottage = gold bar (`DungeonSceneBuilder`)  
**Owner proof:** screenshots show open-top ~chest-height walls under bright blue sky; maze reads outdoor, not crypt.

---

## 0. One-line truth

Composed dungeons (RoomForge room prefabs + `DungeonBaker`) ship as **2.8 m open-top box mazes** with **no ceiling** and **no sky kill**. The camera sits at or above the wall line, so half the frame is procedural blue sky — the set feels broken even when props/torches are fine.

---

## 1. Grounded cause (do not re-guess)

| Fact | Where |
|------|--------|
| Perimeter walls **2.8 m** | `DefaultDungeonRoomsBuilder.BuildPerimeterWalls` — `float wallH = 2.8f` |
| Choke walls **2.4 m** | `BuildChokeInterior` — `float wallH = 2.4f` |
| **No ceiling** in room prefabs | `BuildOne` = floor + walls + sockets only |
| Ambient only half-done | `DungeonBaker`: flat ambient `(0.08,0.09,0.12)` + dir light 0.35 — **no fog, no skybox clear, no solid camera background** |
| Gold bar already exists | `DungeonSceneBuilder`: `WallHeight = 4f`, `BuildCeiling` (KayKit `ceiling_tile`), `ConfigureAmbient` (fog `#0a0a10` 14→42 m, ambient ~0.05) |
| Owner shots | `Screenshot 2026-08-07 122912.png`, `123008.png` — blue sky over short walls, third-person over the maze |

**Out of scope for this WO:** KayKitChallengeOutpost (that is **WO-1000**). This WO is the composed path named in WO-1000 §4 follow-up.

---

## 2. Product intent

- Every composed room reads as an **enclosed interior** — no blue sky, no “pit in a field.”
- Walls tall enough that a hero + modest third-person seat **cannot see over** the perimeter into sky.
- Lighting mood: dark shell, warm fixtures (existing dresser torches keep working against low ambient).
- Match **Healer’s Cottage bar** for enclose + ambient, not necessarily full KayKit modular retile in V1 (textures can stay RoomForge materials if already applied).

---

## 3. Scope

### Phase A — Room shell geometry (`DefaultDungeonRoomsBuilder`)

1. **Raise walls**
   - Perimeter `wallH`: **2.8 → 4.0** (preferred) or **4.5** if still short after capture; document chosen value.
   - Choke interior masses: match perimeter height (or ≥ perimeter − 0.2 m).
2. **Add ceiling pass** per room in `BuildOne` after walls:
   - Prefer KayKit `ceiling_tile.fbx` tiled over footprint (mirror `DungeonSceneBuilder.BuildCeiling`), **or**
   - Solid ceiling slab at wall top (primitive cube) if KayKit load is blocked — must fully occlude sky from any in-room camera under the plate.
   - Ceiling colliders: optional; if present, must not block NavMesh (ceiling above agent height). Do **not** mark ceiling as NavigationStatic walkable.
3. Door gaps stay at current clear width (~2.2 m); only **height** of flanking wall pieces rises.
4. Re-run **Build Default Room Prefabs** (`DefaultDungeonRoomsBuilder.BuildAll` / batch). All `Assets/Dungeon/Rooms/*.prefab` update.

### Phase B — Scene ambient + sky kill (`DungeonBaker`)

Port Healer’s `ConfigureAmbient` values (reuse numbers, do not invent a third palette):

- `ambientMode = Flat`, ambient ~`(0.05, 0.05, 0.055)` (or cottage constants).
- `RenderSettings.fog = true`, linear, dark color ≈ `#0a0a10`, start ~14, end ~42.
- **Kill procedural skybox** for composed dungeon scenes: solid dark / none (same intent as WO-1000).
- Camera clear flags / background near-black if the bake seats a camera (or document that runtime `DungeonController` / main camera must apply on load — either bake or runtime bootstrap, **one place**, not both fighting).
- Soften directional fill (~0.15–0.20), not outdoor sun.

### Phase C — Re-bake all player-reachable composed layouts

After prefab rebuild:

1. Re-bake every composed layout that uses the default room library (starter loop, Sunken Vault, Bonecrypt, Ember Deep, descent probe — whatever `Phase2DungeonBatch` / composer currently ships).
2. **Never hand-edit** `.unity` files.
3. NavMesh still bake from PhysicsColliders; verify doorway walkability after taller walls (gaps unchanged — should stay green).

### Phase D — Proof

- Headless or Editor capture of the same room type as owner shots (combat chamber / maze with barrels).
- Open PNGs: **no blue sky** above walls; ceiling continuous; walls clearly above hero head.
- Optional source-lint: room prefab builder contains ceiling construction + wallH ≥ 4.0 constant (do not flake on mesh counts).

### Phase E — Explicitly out of scope

- Camera feel / FPV default → **WO-920** (depends on enclose; implement after or in parallel carefully).
- Full KayKit modular wall retile (nice-to-have; V1 enclose is geometry + ambient).
- Healer’s Cottage re-bake (already enclosed).
- WO-1000 outpost builder (separate file).

---

## 4. Files (likely)

| File | Action |
|------|--------|
| `Assets/Editor/RoomForge/DefaultDungeonRoomsBuilder.cs` | wallH + ceiling pass |
| `Assets/Editor/RoomForge/DungeonBaker.cs` | ConfigureAmbient-equivalent + sky kill |
| `Assets/Dungeon/Rooms/*.prefab` | Rebuild via menu/batch only |
| Composed scene outputs under bake folder | Re-bake via existing batch menus |
| Optional: small regression under `Assets/Editor/Regression/` | wall height / ceiling present |

**Do not touch:** `DungeonCameraRig` (WO-920), VillageSceneBuilder, hub terrain.

---

## 5. Acceptance

- [ ] Room prefabs: wall height ≥ **4.0 m**; choke matches.  
- [ ] Every default room prefab has a **ceiling** that occludes sky from interior.  
- [ ] Composed bake applies dark ambient + fog + no bright blue skybox.  
- [ ] Player-reachable composed dungeons re-baked; nav paths still complete doorway-to-doorway.  
- [ ] Capture PNG(s) opened — no blue sky over walls (compare to owner 2026-08-07 shots).  
- [ ] `COMPILE_GATE_OK` + relevant dungeon regression green.  
- [ ] RESULT lists wallH chosen, which layouts re-baked, before/after notes.

---

## 6. RESULT

`WorkOrders/WORK_ORDER_919_roomforge_enclose_taller_walls_ceilings.RESULT.md`
