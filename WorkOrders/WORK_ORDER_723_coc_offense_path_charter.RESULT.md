# WORK ORDER 723 — RESULT — CoC Offense Path Charter + Flag Map

**Status:** DONE (charter locked)
**Closed:** 2026-07-16
**Owner pins captured live (felt-test session).**
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`

This is the ONE written law for the 723-731 program. CLI does not re-fork Barracks vs
ArenaAttack vs RaidOutpost after this. Grounded in an SME code survey (file:line cited);
no production flags flipped here (that is WO-731 after PO felt-pass).

---

## 1. OWNER PIN — Path

**PATH A (Barracks army / CoC tap-deploy) is the product spine.** Path B is PARKED (secondary, kept flag-gated, not deleted).

- **A =** `Barracks` train -> `ArmyStorage` roster -> `RaidDeployController` tap-deploy autonomous troops onto a `RaidBase_*` plate + hero -> clear -> loot -> `GoCastle`. Player commands ONE hero; troops are autonomous (setup -> deploy -> watch). Also the async-PvP-ready path (WO-730 reuses the same realize/deploy on a player `BaseLayout` snapshot).
- **B (parked) =** `ArenaAttackRecruitController` 50-pt budget squad, hero-LEASHED followers via `ArenaMode.SpawnAttackSquad`. Different economy of force (SKR wager vs trained army) and different feel (retinue vs commander). Do NOT ship as first-class.

## 2. OWNER PIN — Entry story (one sentence)

> **From Elarion, talk to the Arena Herald -> pick an AI camp -> tap-deploy your trained army + hero -> clear the camp -> loot -> return home.**

Entry surface = **Arena Herald NPC** (`ArenaHeraldSpawner` + `MobileInteractButton` proximity prompt). Landmark = the **colosseum model** (the Herald's visual is already suppressed in favor of it), so `ff.colosseum` becomes the visible entry building.

**AMENDMENT (critical):** today the Herald -> `ArenaPanel` ATTACK -> `ArenaAttackRecruitController` = **Path B**. The Herald NPC + its proximity prompt are REUSED, but its panel must be RETARGETED to a **camp-select** that launches **Path A** (route into a `RaidBase_*` plate via `SceneRouter.GoRaid`, deploy through `RaidDeployController`). The Herald is the entry; the 50-pt squad panel is NOT.

## 3. FLAG MAP (current default -> program end-state)

Current defaults quoted from `Assets/_Modules/Core/FeatureFlags.cs`:

| Flag | Today | End-state | Notes |
|------|-------|-----------|-------|
| `ff.barracks` | **OFF** (L556) | **ON** (WO-724) | OFF hard-returns `BarracksNpcInjector.Inject` (L96-97) -> no drillmaster -> train UI unreachable -> Path A has NO army source. Must go ON. |
| `ff.arena` | **OFF** (L33) | **ON** (WO-725) | OFF early-returns `ArenaHeraldSpawner.Bootstrap` (L82) -> the pinned Herald entry never spawns. Must go ON (but its panel retargeted to Path A per §2). |
| `ff.colosseum` | **OFF** (L565) | **ON** | Chosen Herald landmark (see §2). |
| `ff.raid` | **ON** (L27) | **ON** | Stays; soft-lock proven by WO-726. |
| `ff.raidwalk` | **ON** (L85) | **ON** | Path A enters via the Herald camp-select -> `GoRaid`, bypassing `RaidEntryBridge`/the HUD raid icon, so raidwalk's routing is not in Path A's path. No flip required. |
| `ff.basebuilding` | **OFF** (L67) | **OFF** | Not required for AI-camp PvE. |
| `ff.overworldencounter` | **ON** (L151, tagged "REVERT to false") | **ON (hold)** | See §5 conflict. Entry is the Herald, not walk-to outposts, so leaving it ON is fine for the program; the walk-to raid loop stays parked. |

**No flags are flipped in WO-723.** The ON flips land per-WO and default-ON only after the PO felt-pass (WO-731).

## 4. DEPRECATION LIST (relative to Path A)

| System | Class | Disposition |
|--------|-------|-------------|
| `ArenaAttackRecruitController` (Path B 50-pt squad) | **SECONDARY (parked)** | Keep, flag-gated behind Arena; do NOT delete. Its Herald->ArenaPanel entry chain is reused but retargeted to Path A. |
| Legacy `RaidSelectionScreen` -> `RaidDeployScreen` -> `GoRaid` teleport | **SECONDARY / transitional** | `GoRaid` (SceneRouter L421) is the KEEPER — it is the only route into a `RaidBase_*` plate and Path A reuses it. The `RaidSelectionScreen`/`RaidDeployScreen` UI is superseded by the Herald camp-select (WO-725) but stays as a dev/fallback entry. |
| Walk-to `RaidOutpostSystem` / `EnemyOutpost` | **SECONDARY, dead-by-flag** | Currently suppressed by `overworldencounter` ON (RaidOutpostSystem L141/L166). Not the pinned entry; retire or repurpose later as a map-approach INTO a `RaidBase_*` plate. Do NOT delete. |

## 5. CONFLICTS the downstream WOs must resolve (from SME survey)

1. **`overworldencounter` ON silently kills `RaidOutpostSystem`** (L141/L166) even though the flag docs assume it OFF for V1. Since Path A enters via the Herald, this is acceptable for the program — but WO-726/731 must NOT rely on walk-to outposts, and the stale flag comment should be corrected.
2. **`RaidBase_*` plate is reachable only via the Dev Panel** today (`DevPanelController` L1417 is the only non-flag-gated `GoRaid`). **This is the single biggest wiring gap for Path A** — WO-726 must wire Herald camp-select -> `GoRaid(RaidBase_*)` so the plate is reachable in normal play.
3. **Barracks-off starves Path A** — `RaidDeployController` consumes `GameState.Army` (L592-596); with `ff.barracks` OFF there is no in-play army source. WO-724 (Barracks ON) is a hard prerequisite for a playable WO-726.
4. **Two "attack squad" recruit surfaces** (Path A army vs Path B 50-pt squad) — resolved by this charter: Path A economy of force wins; Path B parked.

## 6. AMENDMENTS to downstream WOs (path = recommendation, so minimal)

- **WO-724 (Barracks Live):** unchanged in intent; confirm it flips `ff.barracks` ON and makes the drillmaster/train UI reachable so `ArmyStorage` has an in-play source. Prereq for 726.
- **WO-725 (Settlement Arena Entry Live):** RETARGET. Deliver the **Arena Herald NPC** (spawn via `ff.arena` ON, colosseum landmark) whose panel opens a **camp-select** (not the Path B 50-pt recruit). It must route into Path A (`GoRaid` -> `RaidBase_*`). Parallel-safe with 724.
- **WO-726 (AI Camp Attack Loop):** the tap-deploy plate (`RaidDeployController` + `TroopDeployer` + `RaidVictoryController`) already exists and is wired inside `RaidBase_*`; the WO's core work is **wiring the Herald camp-select -> `GoRaid` -> deploy trained army -> clear -> `GoCastle`** and proving the raid soft-lock. Do not rely on `RaidOutpostSystem`.
- **WO-727 (Recipe AI Settlements):** AI camps = tiered `BaseLayout` snapshots realized on the plate; same realize path async PvP (730) will reuse. No path change.
- **WO-728/729/730/731:** no charter-level amendment; 730 treats player `BaseLayout` snapshots as the async-PvP payload through the same realize/deploy path; 731 flips the flags ON only after PO felt-pass.
- **OPEN (confirm in 727):** `Village2RaidController` / `GarrisonController` may be a THIRD settlement-attack surface (Village2 path) — confirm its flag gating and whether it overlaps Path A; classify primary/secondary/dead then.

## 7. Acceptance (all met)

- [x] Owner-pinned Path A/B (A).
- [x] Flag table + entry story.
- [x] Deprecation list complete.
- [x] No code feature work (charter/RESULT only; no production flag flips).
- [x] Downstream amendments noted (725 retarget to Path A; Herald entry; 726 wiring gap named).
