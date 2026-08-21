**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 153 — World Crystal Mine (the relocated, buildable/placed crystal extractor)

**Status: READY TO IMPLEMENT**
**Date:** 2026-05-30
**Priority:** High — the crystal economy's home now that the village CrystalMine is removed (WO-150). The steady crystal faucet.
**Lane:** gameplay / economy code (CLI) + world placement. **NOT the frozen `VillageSceneBuilder`; no `Village.unity` hand-edit; no bake fired by UI.**
**Owner ask:** crystals relocate from the village onto world nodes — this is the *mine* (the persistent, repeatable crystal extractor in the world), distinct from the rare timed spawns (WO-154).

---

## RECONCILE — do NOT reinvent (verify before writing)

| Need | State | Where |
|---|---|---|
| Node model: `ResourceNode` + `HarvestNodeData` SO + `Extract()` → `GameState` bank | **BUILT/SPEC'd (WO-141)** | `WORK_ORDER_141_harvestable_resource_nodes.md` — **the Crystal Mine is a Crystal-type node, reuse this** |
| Regional crystal **grades** (rarer crystals in more dangerous regions) | **SPEC'd (WO-144)** | `WORK_ORDER_144_regional_crystal_subtypes.md` — the mine yields the region's grade |
| Old village passive CrystalMine (the F-to-upgrade mine) | **REMOVED from village (WO-150)** | `CrystalMine.cs` exists; reconcile — the *behavior* (passive yield + upgrade tiers) is the pattern; relocate it to the world, don't rebuild from scratch |
| Crystal wallet (`GameState.AetherCrystals`) + EconomyService grant/spend | **BUILT** | `EconomyService.cs`, `GameState.cs` |
| Worker / pet auto-harvest + offline accrual seams | **SPEC'd (WO-117/119/115)** | the mine exposes the same rate/store seam so they plug in |

**So the work:** define the Crystal Mine as a **Crystal-type harvest node** (WO-141 model) that is **persistent and repeatable** (a renewable mine, not a one-time pickup), region-gated to yield the local crystal **grade** (WO-144), with the **passive-yield + tier-upgrade** behavior salvaged from the old `CrystalMine.cs`. Reconcile with all three; add only the "mine" specifics.

---

## What makes the Mine distinct from a generic node (WO-141) and from rare spawns (WO-154)

- **Persistent + renewable:** unlike a one-shot node that depletes and despawns, the mine regenerates its crystal yield over time (a refilling vein) — it's the *reliable* faucet.
- **Upgradeable:** tiers raise yield rate / capacity (salvage the old `CrystalMine` tier pattern — passive trickle + upgrade cost via `EconomyService`).
- **Region-graded:** the crystal grade it yields is set by the region it sits in (WO-144) — a mine in a high-danger region yields a rarer grade.
- **Placed/built, not random:** the mine has a fixed home (placed by the world builder, or — later — player-built via build-mode WO-108), unlike WO-154's random timed spawns.

## Behavior

1. **Yield:** accumulates crystals of the region's grade at `yieldРerSec` (tier-scaled); player walks up + holds-to-extract (WO-141 prompt) to bank the accrued amount into `GameState.AetherCrystals` (grade-aware per WO-144). Optionally auto-banks a trickle for worker/pet/offline (WO-117/119/115 seams).
2. **Regen:** after extraction the vein refills over a cooldown up to a capacity cap — so it's repeatable but rate-limited (can't infinite-farm one mine).
3. **Upgrade:** tier raises rate + capacity (+ maybe unlocks higher grade), paid via `EconomyService.TrySpend` — mirror the old CrystalMine upgrade UX (code-built prompt, no UXML).
4. **Placement:** placed by the world builder in regions (CLI's world lane); seam left so build-mode (WO-108) can let the player place one later.

## Assembly / constraints (CLAUDE.md §5/§6)

- Data (`CrystalMineData` SO or reuse `HarvestNodeData` with a `renewable`+`tiers` extension) → `DeNelle.Core.Data`; node runtime → `DeNelle.Village` (or the world module). Village → Core only; bank writes `GameState` directly. HUD/Audio via `CoreServices.?`. No UXML, no `System.Reflection`, no new currency.

## Acceptance criteria

1. Crystal Mine is a **Crystal-type harvest node** built on the WO-141 model (not a parallel system).
2. **Renewable + rate-limited:** extracts crystals, then regenerates over a cooldown to a capacity cap — repeatable, not one-shot, not infinite.
3. **Region-graded:** yields the local region's crystal grade per WO-144.
4. **Upgradeable:** tiers raise yield/capacity via `EconomyService.TrySpend`; salvages the old `CrystalMine` tier/UX pattern, code-built prompt.
5. Banks to `GameState.AetherCrystals` (grade-aware); worker/pet/offline seams exposed (WO-117/119/115), not implemented here.
6. No `VillageSceneBuilder` edit, no bake, no UXML, no new currency, no parallel node system.
7. Brace balance on every `.cs`; Village→Core only; `?.` on cross-module calls.

## What NOT to touch
- Don't rebuild the harvest-node model (WO-141) or the grade system (WO-144) — extend them.
- Don't edit `VillageSceneBuilder.cs` / hand-edit `Village.unity` / fire a bake.
- Don't add a new currency or a second crystal wallet.

## Done checklist (CLAUDE.md §10)
- [ ] Built on WO-141 node model + WO-144 grades; old CrystalMine behavior salvaged not duplicated
- [ ] Renewable/rate-limited + upgradeable verified; banks grade-aware to GameState
- [ ] Brace balance; Village→Core only; no UXML/Reflection/new currency; no bake
- [ ] `WORK_ORDER_153_world_crystal_mine.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
