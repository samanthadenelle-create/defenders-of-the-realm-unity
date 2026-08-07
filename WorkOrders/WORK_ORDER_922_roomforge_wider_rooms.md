# WORK ORDER 922 — RoomForge: all rooms much wider

**Status: READY TO IMPLEMENT**  
**Minted:** 2026-08-07 (CLI / Grok — owner: “all rooms can be much wider”)  
**Silo:** World / Dungeon bake (RoomForge prefabs + recompose + rebake)  
**Roles:** CLI implement; batch rebuild prefabs + graphs + scenes  
**Depends on:** none hard; **combine bake with WO-919** if both ship (one rebuild wave)  
**Related:** WO-919 taller walls + ceilings; WO-921 torch/spawn (wider rooms ease “encased in fire”); WO-1001 compose path  
**Owner:** rooms feel cramped (6 m cells); want **much** more floor space.

---

## 0. One-line truth

Every composed room’s world size is `footprintCells × Cell`, and **`Cell = 6`** is the single master knob (`DefaultDungeonRoomsBuilder`). Almost all rooms are **1×1 = 6×6 m**; combat/boss are **2×2 = 12×12 m**. Raising `Cell` and rebuilding prefabs + recomposing layouts widens **all** rooms without hand-editing each graph.

---

## 1. Grounded sizing today

| Piece | Value | File |
|-------|--------|------|
| Cell size | **`6` m** | `DefaultDungeonRoomsBuilder.Cell` |
| 1×1 room | **6×6 m** floor | most corridors, hubs, lore, reward |
| 2×2 room | **12×12 m** | `CombatChamber`, `BossKeep` |
| Door gap | **2.2 m** clear | `BuildPerimeterWalls` |
| Socket halfWidth | ~1.1–1.5 m | door/arch sockets |
| Composer | mates **real** socket world poses | `GraphDungeonComposer` — rebuilds positions from prefabs |
| Emitted layout | `cellSize=1`, cells = solved world ints | recompose after prefab change |

Healer’s Cottage / KayKit outpost are **other builders** — out of scope unless owner asks.

---

## 2. Product target

**Default recommendation (ship unless owner overrides):**

| | Today | Target |
|--|--------|--------|
| Cell | 6 m | **10 m** (~1.67×) |
| 1×1 room | 6×6 | **10×10** |
| 2×2 room | 12×12 | **20×20** |

**Alternate if still tight after feel:** Cell **12 m** (2× linear, 4× area).

Do **not** only widen combat rooms — owner said **all rooms**.

Optional later (not V1 unless asked): bump selected footprints (e.g. BossKeep 2×2 → 3×3) **after** cell scale.

---

## 3. Scope

### Phase A — Master cell (required)

1. In `DefaultDungeonRoomsBuilder`, set:
   ```csharp
   private const float Cell = 10f; // was 6f — WO-922 owner: much wider rooms
   ```
2. Update comments / tooltips that say “6u cells” / “canon 6”:
   - `RoomPrefabMeta` tooltip  
   - `RoomForge/README.md`  
   - Any regression that hardcodes `6` as cell size (search `cellSize == 6`, `Cell = 6`, `6u cell`)
3. **Door clear:** keep gap **2.2–2.8 m** (human-scale door in a wider room is fine). Optionally raise to **2.6** if doorways feel like mouseholes.  
4. **Socket halfWidth:** leave unless mate checks fail; composer uses real transforms.

### Phase B — Rebuild room prefabs (required)

```
Defenders/Dungeon/Build Default Room Prefabs
// batch: DeNelle.Editor.RoomForge.DefaultDungeonRoomsBuilder.BuildAll
```

- All `Assets/Dungeon/Rooms/*.prefab` re-saved with new footprint metres.  
- `rooms-catalog.json` dual-copy updates `cellSize` + footprint metadata.

### Phase C — Recompose graphs + rebake scenes (required)

1. Re-run graph compose for every shipped graph (`dg_starter_loop`, `dg_sunken_vault`, `dg_bonecrypt`, `dg_ember_deep`, `dg_descent_probe`, …).  
2. Re-bake via existing Phase2 / DungeonBaker batch (`populateForPlay` as today).  
3. **Never hand-edit** `.unity`.  
4. Verify: socket mate distances still ~0 (composer re-solves); navmesh path entry→exit still completes on each floor; stair ports still seat on floor.

### Phase D — Dressing / traps / spawn (required check)

Wider rooms change density:

| System | Action |
|--------|--------|
| `DungeonDresser` | Torch corners use `halfW/halfD` — scales automatically. Confirm torches not sparse-weird; optional +1–2 floor props later. |
| Trap offsets | JSON offsets are room-local metres — **revisit** traps at `offset [0,0,0]` (room centre) so they don’t sit on spawn paths after resize. |
| Oil / chests / extracts | Same — seats are room-relative; smoke-test one dungeon. |
| Hero entry | Still entry room centre + nav sample — should feel better, not worse. |

### Phase E — Combine with WO-919 if both open

One rebuild wave:

1. Cell widen (this WO)  
2. Wall height + ceiling (919)  
3. Torch dial (921 A) if ready  
4. BuildAll prefabs → compose → bake once  

Document order in RESULT.

### Phase F — Out of scope

- Walkable stair meshes (still triggered ports).  
- Camera (WO-920).  
- Changing graph topology / room count.  
- Hand-authored Healer’s Cottage bounds.

---

## 4. Files (likely)

| File | Action |
|------|--------|
| `Assets/Editor/RoomForge/DefaultDungeonRoomsBuilder.cs` | `Cell = 10f` (or 12) |
| `Assets/_Modules/Dungeons/RoomForge/RoomPrefabMeta.cs` | tooltip canon cell |
| `Assets/Dungeon/Rooms/*.prefab` | rebuild only |
| `**/dungeon-layouts/rooms-catalog.json` | via BuildAll |
| Graph JSON layouts + composed scenes | recompose + rebake |
| Regressions hardcoding 6u | update expected cell |

---

## 5. Acceptance

- [ ] `Cell` ≥ **10** (or owner-picked 12); documented in RESULT.  
- [ ] All default room prefabs rebuilt; 1×1 floor spans new cell on X and Z.  
- [ ] All player-reachable composed dungeons recomposed + rebaked.  
- [ ] Doorways still walkable; stair Descend/Climb still work.  
- [ ] Capture: corridor + combat room look **noticeably** wider vs 2026-08-07 screenshots.  
- [ ] Nav path + trap/oil smoke-test on one multi-level dungeon.  
- [ ] `COMPILE_GATE_OK` + dungeon compose/multi-level regressions green.  
- [ ] RESULT: Cell before/after, list of rebaked dungeon ids, PNG compare.

---

## 6. Suggested default (no further ruling needed)

Ship **Cell = 10**. If PO still says cramped after one felt, bump to **12** in a same-WO follow-up bake (one-line + rebuild).

---

## 7. RESULT

`WorkOrders/WORK_ORDER_922_roomforge_wider_rooms.RESULT.md`
