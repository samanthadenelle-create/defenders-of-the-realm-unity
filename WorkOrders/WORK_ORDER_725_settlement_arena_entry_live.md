# WORK ORDER 725 — Settlement Arena Entry Live

**Status:** READY TO IMPLEMENT (after WO-723)  
**Priority:** P0  
**Silo:** World / UI  
**Depends on:** WO-723 (**DONE** — RESULT pins Path A + Herald entry)  
**Blocks:** WO-726  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** M  
**Parallel-safe with:** WO-724  

> **723 RESULT amendment (binding):** Herald must **not** open Path B (`ArenaAttackRecruitController`).  
> Retarget to **AI camp select → Path A** (`GoRaid` / `RaidBase_*` + `RaidDeployController`).  
> Visible landmark: **colosseum** (`ff.colosseum` ON at close). See `WORK_ORDER_723_….RESULT.md` §2.  

---

## Goal

Player can **find and open** the settlement-attack flow from the hub. Entry + panel open + cancel clean. No full deploy polish (that is WO-726).

---

## Built already

- `ArenaHeraldSpawner` → `ArenaPanel.Open()`
- `ArenaPanel`, `ArenaCatalog` (3 seeded opponents)
- `ArenaMode.TryStartRaid`
- Gates: `ff.arena` (default OFF), `ff.colosseum` (default OFF)

---

## Gaps to close

1. **One discoverable entry** in hub (`Main_Castle_Overworld`) per WO-723 pin:
   - Herald marker **or** Colosseum (`ff.colosseum`) **or** HUD “Raid” — **one primary**.
2. Entry opens the **product** attack UI (Path A: AI camp / raid list — not crypto-blocked SKR-only gate).
3. **SKR wager** stub-safe: free PvE AI camps must not hard-block on empty wallet.
4. Clean close: no stuck `BattleLock`, no stranded camera/input.

---

## Tasks

1. Implement primary entry only (hide or do not enable secondary).
2. FlowTrace open/close (`Arena` system).
3. Ensure `ff.arena` OFF removes all entry surfaces.
4. Manual smoke: open ≤30s from hub; cancel → free roam.
5. Do **not** flip production default ON (WO-731).

---

## Acceptance

- [ ] From hub, player opens attack UI in ≤30s without dev panel.
- [ ] Cancel returns to free roam; input unlocked.
- [ ] `ff.arena` OFF = no entry surface.
- [ ] FlowTrace on open/close.
- [ ] CompileGate green.

---

## Not in scope

- Deploy tray / army wiring (WO-726).
- Real crypto wallet / on-chain stake.
- AI recipe authoring (WO-727).

---

## Key files

- `Assets/_Modules/Village/Arena/ArenaHeraldSpawner.cs`
- `Assets/_Modules/Village/Arena/ArenaPanel.cs`
- `Assets/_Modules/Village/Arena/ArenaMode.cs`
- `Assets/_Modules/Core/FeatureFlags.cs` (`Arena`, `Colosseum`)
- Hub colosseum visual injector (if used)

---

## RESULT

`WorkOrders/WORK_ORDER_725_settlement_arena_entry_live.RESULT.md`
