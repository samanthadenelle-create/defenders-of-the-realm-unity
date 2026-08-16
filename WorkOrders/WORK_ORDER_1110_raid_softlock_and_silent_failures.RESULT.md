# RESULT — WO-1110 the raid's softlock, its silent catches, and the death-exit loot inversion

**Date:** 2026-08-16  **Seat:** CLI (commit `64092d611`)
**Status:** DONE — pending PO felt-verify

From the same SME raid-readiness audit. Three defects, ranked by what costs the player most.

## What shipped

1. **THE SOFTLOCK.** `RaidDeployController.Start` called `BuildHud()` unguarded, and `BuildHud` sat
   **BEFORE** `StartCoroutine(BindScoringRoutine)` — the clock-expiry subscriber. A throw left no tray,
   no Retreat button AND no timeout rescue: `OnTimeExpired` fired into nothing. The only exits were dying
   or killing the app. Every other risky op in that file was already `Guard.Try`-wrapped; this one line
   was the exception. **The clock now binds FIRST and the HUD build is guarded** — the way out never
   depends on presentation succeeding. Ships with a self-clearing `DebugForceBuildHudThrow` injection hook
   so the fix is TESTABLE rather than argued: old order hangs, new order still evacuates at the clock.
2. **A SILENT 55% PAY CUT.** `RaidScoring.ResolveRewardMultiplier` swallowed its catch, so a catalog miss
   quietly paid ×1 instead of ×2.2 on `mage_enclave` with no trace line at all. The 1f fallback is
   unchanged but every path to it now logs, naming `configId` + scene and stating outright that the player
   is being underpaid. Three more bare catches given traces and, where the player is affected, toasts
   (a dead card tap now says so instead of nothing). §12: a catch that swallows without logging is
   forbidden.
3. **AN INVERTED INCENTIVE.** Retreat and timeout both paid partial loot; hero death reconciled the army
   but never called `Finalize` / `LootFor` — so razing two thirds of a base and DYING paid **less** than
   razing the same and tapping Retreat. That is the inverse of the perverse incentive the retreat-loot
   block was written to remove, and it punished the more committed play. Extracted `SettlePartialLoot` as
   the single non-victory settlement authority (idempotent via the `Finalized` latch); death now routes
   through it **BEFORE** `ReconcileRaidEnd`, same order as retreat — order matters, `Finalize` samples
   destruction off the live field.

## Deliberately NOT done — recorded, not fixed

- ⚠ **The army is only mutated at `ReconcileRaidEnd`, so quitting mid-raid writes nothing** — a player can
  quit to avoid wounding their troops. A separate decision, deliberately left.

## Owner decision left open

- ⚠ **Death pays what retreat pays** — stated as a default, not presumed. Overrule to add a death penalty.

## Oracle

`RaidExitParityRegression` → `RAID_EXIT_PARITY_OK`. Pins the two exits together so they cannot drift
apart again, pins bind-before-build, and fails on any bare catch in the five raid runtime files. It strips
comments before matching so its own prose cannot satisfy it.
