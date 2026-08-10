# RESULT — WO-1013: Castle Defense Plans — mechanics complete; beat + sheet held for WO-1012 integration

**Verified:** 2026-08-10 (CLI orchestrator; implementing agent + gates)

## What landed

- **Visible-locked card (built — none existed):** new `visibleLockedIds` data axis on
  build-categories rows (Defense: `tower_arcane_spire` → "Recover the plans"); locked card renders
  dimmed with art + REAL cost + the reason in words, un-tappable, never armed; unlock lifts live on
  the next palette Configure/Refresh. WO-948's lockedIds axis undisturbed.
- **Persistence:** `unlock.tower_arcane_spire` via the SeenTutorials keyed store — NO schema bump.
- **Trigger/drop:** `CastleDefensePlansService` (1 Hz, town-scenes only) spawns the plans prop at the
  gate once `GameState.WavesCompleted >= 2` (persisted lifetime counter — covers skip-tutorial),
  deterministically respawned until collected; `CastleDefensePlansPickup` mirrors ComposedKeyPickup;
  `TryCollect` = once-ever gate → live catalog cost read (crystals-inclusive arcane basket) →
  persisted unlock → `EconomyService.GrantPurchased` (cap-proof exact grant). Wave 3+ can never drop
  (persisted flag closes `ShouldSpawnDrop`; regression-pinned).
- **Funnel:** `[Flow:Progression]` drop-spawned / collected / unlocked / first-spire-built.
- **Regression:** `CastlePlansUnlockRegression` `[castle-plans]` — GREEN in the 2026-08-10 11:38 run
  (lock data, VM projection + live unlock, ShouldSpawnDrop truth table, exact-basket idempotent
  grant). Registration applied by the orchestrator (namespace corrected: `DeNelle.Editor`).

## Deliberately deferred (integration items, post-WO-1012-pipeline)

1. The contextual guide beat: proposed `ctx_plans_recovered` step JSON + `progression.plans_collected`
   signal wiring (in the agent report) — merges into tutorial-steps.json after the pipeline lands.
2. The owner's parchment sheet (`Assets/Resources/UI/CastlePlans_ArcaneTower.png`, staged): the only
   lore modal is Dungeons-assembly-scoped; a ~80-line Village-side `CastlePlansSheet` on the lore
   grammar is specced in the report — a deliberate follow-up, mechanics never depend on it.
3. ⚠ OWNER NAMING RULING open: the art says "ARCANE TOWER"; the buildable is the Arcane Spire
   (`tower_arcane_spire`); a distinct `arcane-tower` (magic-upgrades building) exists. QR-5.7.

## Owner felt-verify

Locked card visible from first build-mode open → survive wave 2 → glinting satchel at the gate →
walk over it → Spire affordable and buildable, no FREE label anywhere.
