# WORK_ORDER_320 — Defend the Tower: losing has no impact (no defeat consequence)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06
**Reconcile with:** `PatriciaLightController` / WaveManager lose condition, WO-235 (death/spire-destroyed screens), WO-132 (hero health lose condition)

## Problem
Losing Defend the Tower (tower integrity → 0, or hero death) has **no consequence** — the player just lands
back in town as if nothing happened. There's no defeat screen, no penalty, no stakes.

## Goal
Losing DTT has a real, readable consequence: a defeat flow + a meaningful (but fair) penalty, then a clear
path to retry/return — so the mode has stakes.

## Scope
- Detect the loss properly (tower integrity 0 / hero down) in `PatriciaLightController`/WaveManager.
- Show the **defeat screen** (reuse WO-235 "spire destroyed / death" screen) — not a silent return.
- Apply a consequence (owner to tune): e.g. forfeit the run's rewards, a resource/no-reward penalty, or a
  cooldown before retry — routed through `EconomyService` if it costs resources. **No permanent loss** (stay
  fair / no rage-quit). Owner sets the exact penalty.
- Offer Retry / Return-to-town from the defeat screen (deliberate choice, not an auto-bounce).

## Acceptance criteria
- [ ] Losing DTT triggers a defeat screen (not a silent return to town).
- [ ] A consequence applies (rewards forfeited / penalty / retry cost) — configurable, not permanent.
- [ ] Player chooses Retry or Return-to-town from the screen.
- [ ] Winning still pays out normally (no regression to the win path).
- [ ] Costs/penalties via EconomyService; brace check; CompileGate OK; build SUCCESS; verify in play.

## Do NOT touch
- No `.unity` edits. Reuse WO-235 screens + EconomyService (don't fork). Coordinate with WO-317/318/319 (same mode).
