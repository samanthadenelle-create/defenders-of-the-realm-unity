# RESULT — WO-1103 kill rewards: base + variance + kill scaling; field-kill toast

**Date:** 2026-08-16  **Seat:** CLI (commit `8b1d1a649`)
**Status:** IMPLEMENTED - pending PO felt-verify

## What changed

1. **Base + bounded variance (spec 1):** `rewardVariance` field added to BOTH `enemies.json`
   twins (tunables: 0.15 / 0.10 per row class) + `EnemyDef`; rolled at grant time in a SINGLE
   roll authority (one shared helper — no per-call-site duplicate formulas). Absent field = 0
   (no behavior change for un-migrated rows).
2. **Arena reads the catalog (spec 2):** the synthesized `XpReward = round(14*t)` defs in
   `BattleArena` are replaced by catalog lookups (threat scale kept as multiplier), closing
   the "follow-up" the code comment promised.
3. **Kills, not roster (spec 3):** payout counts ACTUAL kills — fixes B-1 (capped-spawn arena
   overpaying for enemies never spawned) and B-2 (bonus boss paying zero). The victory
   SUMMARY now shows the TRUE total banked (battle slice + per-enemy stream), so 4 kills
   visibly outpay 1 kill.
4. **Field-kill notification (spec 4):** ranged/outside-arena kills show a corpse label plus
   a pack bounty toast — one aggregate notification, no per-follower spam, no new
   notification system.
5. **Owner default CONFIRMED (spec 5):** leader-carries-the-pack payout kept, documented in
   the toast wording ("pack bounty").
6. **Falsified regressions (spec 6):** `EnemyRewardRegression` extended to drive `Enemy.Die`
   for real (no longer re-implements the grants); asserts kill-sum within the variance band,
   capped-spawn pays kills not roster, bonus boss counts, field kill emits the notification.
   `ArenaCombatOracle` extended to assert the SUMMARY arithmetic tracks the kill count.

## Files

`Enemy.cs`, `BattleArena.cs`, `OverworldEncounterSpawner.cs`, both `enemies.json` twins,
`EnemyDef`, `EnemyRewardRegression.cs`, `ArenaCombatOracle.cs`.

## Verification

- Gate green + committed (`8b1d1a649`); extended regressions are red-on-revert (falsified).

## PO felt-verify

- A 4-kill arena fight banks and DISPLAYS more than a 1-kill fight; the same fight twice
  yields different-but-range-bound totals.
- Kill an overworld enemy from range before arena engage: exactly one earned-rewards label /
  pack-bounty toast naming the amounts.
