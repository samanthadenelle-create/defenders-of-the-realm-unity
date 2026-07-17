# PROGRAM — WO-723 → WO-731 · CoC Arena + Barracks → AI Camps → Async PvP

**Status:** MINTED 2026-07-16 · **WO-723 DONE** (start implement at **724**)  
**Numbering:** next free after roster/layout = **738** (`CLI_LANES_WO_NUMBERS.md`)  
**Canon:** `docs/ARENA_SOLUTION.md` · `docs/COMBAT_PIVOT_NORTHSTAR.md` · `docs/NORTH_STAR.md`  
**Claude packet:** `WorkOrders/CLAUDE_HANDOFF_2026-07-16_barracks_coc_roster.md`  
**723 RESULT (binding):** `WorkOrders/WORK_ORDER_723_coc_offense_path_charter.RESULT.md` — Path A + Herald → Path A camp select

---

## One-line summary

Reuse Barracks + `ArmyStorage` + `RaidDeploy` + `EnemyOutpost`/`ArenaMode`; connect under **one CoC offense path**; ship **AI recipe camps** first; treat player `BaseLayout` snapshots as the future **async PvP** payload.

---

## Player goals

1. **Arena live** — discoverable settlement-attack entry.  
2. **Barracks unlocked** — train → army roster.  
3. **Attack AI camps CoC-style** — deploy troops + hero → clear → loot → home.  
4. **PvP-ready architecture** — same realize path; swap AI layout for player snapshot later.

---

## Do not conflate

| Name | System | Role |
|------|--------|------|
| **Hero Arena** | `BattleArena` | Single-hero real-time kite (`ff.overworldencounter`) — **not this spine** |
| **Settlement Arena / Raid** | `ArenaMode` + army deploy | CoC attack loop — **this program** |

**Unifying law:** player controls **one hero**; troops are **autonomous** (setup → deploy → watch).

---

## Dependency graph

```
723 charter ✅ DONE
       │
       ▼
724 Barracks ──┐
725 Arena entry ─┴► 726 Attack loop ──► 727 AI recipes
                                              │
                              ┌───────────────┼───────────────┐
                              ▼               ▼               ▼
                           728 economy     729 defend      730 PvP I/O
                              └───────────────┬───────────────┘
                                              ▼
                                           731 close + flags
```

| Parallel-safe | Serial spine |
|---------------|--------------|
| **724 ∥ 725** (start here) | 724 → 726 → 727 → 728 → 731 |
| 729 ∥ 730 (after 727) | |

---

## Work orders

| WO | Status | Title | File |
|----|--------|-------|------|
| **723** | **DONE** | CoC Offense Path Charter + Flag Map | RESULT: `WORK_ORDER_723_coc_offense_path_charter.RESULT.md` |
| **724** | **START** | Barracks Live: Train → ArmyStorage | `WORK_ORDER_724_barracks_live_train_army.md` |
| **725** | READY | Settlement Arena Entry Live (Path A retarget) | `WORK_ORDER_725_settlement_arena_entry_live.md` |
| **726** | READY | AI Camp Attack Loop (Deploy → Clear → Return) | `WORK_ORDER_726_ai_camp_attack_loop.md` |
| **727** | READY | Recipe AI Settlements (Tiered BaseLayout camps) | `WORK_ORDER_727_recipe_ai_settlements.md` |
| **728** | READY | Repeatable Raid Economy (Cooldown, Stars, Loot) | `WORK_ORDER_728_repeatable_raid_economy.md` |
| **729** | READY | Defend & Watch (AI attacks player base) | `WORK_ORDER_729_defend_and_watch.md` |
| **730** | READY | Async PvP Foundation (Snapshot I/O, no live netcode) | `WORK_ORDER_730_async_pvp_foundation.md` |
| **731** | READY | Felt-Complete Vertical + Flag Flip + Canon | `WORK_ORDER_731_coc_felt_complete_close.md` |

---

## Implement order (723 closed)

1. ~~**723**~~ **DONE** — Path A + Herald entry locked in RESULT. Do not re-charter.  
2. **724 + 725** in parallel (**start here**).  
3. **726** when both green.  
4. **727 → 728** serially.  
5. **729 ∥ 730** after 727.  
6. **731** last (flag defaults ON only after PO felt-pass).

---

## Path decision (LOCKED by WO-723 RESULT)

| Path A (**product spine**) | Path B (**parked**) |
|----------------------------|---------------------|
| Barracks → `ArmyStorage` → `RaidDeployController` | `ArenaAttackRecruitController` 50-pt budget squad |
| CoC tap-deploy on raid plate | Hero-leashed attack squad in `ArenaMode` |

**Do not ship both as first-class.** Herald retargets to Path A camp select (not Path B recruit panel).

---

## Flag end-state (after 731 PO pass)

| Flag | Start | End |
|------|-------|-----|
| `ff.barracks` | OFF | **ON** |
| `ff.arena` | OFF | **ON** |
| `ff.colosseum` | OFF | ON only if chosen landmark |
| `ff.raid` / `ff.raidwalk` | ON | ON (soft-lock proven) |
| `ff.basebuilding` | OFF | not required for AI-camp PvE |

---

## Critical code (reuse)

- `FeatureFlags.cs` · `ArmyStorage` · `TroopTrainingPanel` · `BarracksNpcInjector`  
- `RaidDeployController` · `TroopDeployer` · `RaidVictoryController`  
- `ArenaMode` · `ArenaPanel` · `ArenaHeraldSpawner` · `ArenaCatalog`  
- `EnemyOutpost` · `GarrisonController` · `RaidOutpostSystem`  
- `BaseLayout` / Realize · `ArenaNavMeshBaker` · `ProceduralSiegeArenaBuilder`

---

## Related program (owner 2026-07-16)

**Troop roster + upgrade unlocks** = **WO-732 → WO-736**  
Index: `WorkOrders/WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`

Prefer green (or in-flight) **before WO-724 felt-pass** so Barracks is not “two troops forever.”

| Default (T1) | Unlock ladder (Barracks T2→T6) |
|--------------|--------------------------------|
| Footman, Archer | Spearman → Shieldguard → Outrider → Battlemage → Echo Legionnaire |
