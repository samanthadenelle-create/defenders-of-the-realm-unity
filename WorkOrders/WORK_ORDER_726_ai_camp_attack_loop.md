<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-16
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-16) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 726 — AI Camp Attack Loop (Deploy → Clear → Return)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Priority:** P0 (the CoC bite)  
**Silo:** Combat / Raid  
**Depends on:** WO-724, WO-725  
**Blocks:** WO-727, WO-731  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** L  

---

## Goal

Trained army + hero attacks an **AI camp**, deploys on the field (CoC-style), clears garrison, returns home with loot. **No soft-lock.**

---

## Built already

| Piece | Path |
|-------|------|
| Deploy tray | `RaidDeployController.cs` (installs on `RaidBase_*`) |
| Spawn from army | `TroopDeployer.SpawnFromArmy` |
| Troops | `TroopController`, `TroopRally` |
| Target / garrison | `EnemyOutpost`, `GarrisonController`, `RaidGarrisonSpawner` |
| Victory | `RaidVictoryController`, `RaidClaimService` |
| Optional entry funnel | `ArenaMode`, `RaidEntryBridge`, legacy `RaidSelectionScreen` |

---

## Gaps to close

1. **Path A end-to-end** from WO-725 entry:
   - Select AI target → enter raid space → **tray shows trained `ArmyStorage` troops** (not empty; not ArenaDefense budget ids as primary).
2. **Win path:** clear → victory UI → loot applied → home (`GoCastle` / warp).
3. **Retreat path:** wounded recovery via `ArmyStorage.MarkWounded` (no permadeath).
4. **Hero** present and combat-capable; troops autonomous (COMBAT_PIVOT).
5. **Soft-lock proof:** win / lose / retreat / timeout all release `BattleLock` + restore control.
6. FlowTrace: Enter → Deploy → Clear → Claim → Return (`Raid` or `Arena` system).

---

## Tasks

1. Retarget entry→raid to consume `ArmyStorage` (per WO-723 Path A).
2. Ensure `RaidDeployController` (or equivalent) mounts on the chosen raid venue.
3. If `ArenaMode` remains the funnel: prefer army tray over `AttackSquad` budget list.
4. Instrument all exits; fix any stuck lock found from data.
5. Prefer fleet/headless marker e.g. `RAID_LOOP_OK`.

---

## Acceptance

- [ ] Train 3+ troops (724) → open raid → deploy all → clear AI camp → return with resources.
- [ ] Retreat mid-raid: survivors OK; downed wounded, not deleted.
- [ ] Second raid same session: no double-subscribe / soft-lock.
- [ ] Headless/fleet probe preferred (`RAID_LOOP_OK` or equivalent).
- [ ] Owner felt-pass: “this is CoC-shaped.”
- [ ] CompileGate green.

---

## Not in scope

- Beautiful multi-tier AI base art (WO-727).
- Defend-your-base (WO-729).
- Live multiplayer / matchmaking.
- Cooldown economy depth (WO-728).

---

## Key files

- `Assets/_Modules/Village/Troops/RaidDeployController.cs`
- `Assets/_Modules/Village/Troops/TroopDeployer.cs`
- `Assets/_Modules/Village/World/Camps/RaidVictoryController.cs`
- `Assets/_Modules/Village/World/Camps/EnemyOutpost.cs`
- `Assets/_Modules/Village/Arena/ArenaMode.cs`
- `Assets/_Modules/Village/Hero/RaidEntryBridge.cs`
- `Assets/_Modules/Core/Combat/BattleLock.cs`

---

## RESULT

`WorkOrders/WORK_ORDER_726_ai_camp_attack_loop.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `WO-932 RESULT 1-5; RaidScoring.cs` — deploy->claim loop landed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
