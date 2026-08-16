# RESULT — WO-1042 rough stone → Jeweler polish → refined gem

**Date:** 2026-08-16  **Seat:** CLI (commit `eff761fcc`, shipped together with WO-1041)
**Status:** DONE — pending PO felt-verify

Owner design: a run drops a **ROUGH** stone; the Jeweler polishes it over time into a gem; the run grade
raises the odds; that gem feeds the **EXISTING** ring chain. Up to 5 rolls per stone, trade-down with NO
floor, shatter, paid attempts with disclosed odds, staking grants ATTEMPTS.

## What shipped

- **REUSED, NOT BUILT:** `jeweler-recipes.json`, the bench, the panel and the ring chain all shipped in
  WO-553. The only new pieces are **the rough stone, the polish job and the odds table** —
  `jewel-polish.json` (both twins), `materials.json` rows, `JewelPolishService.cs`,
  `JewelPolishConfirmPanel.cs`, `DungeonRunGrade.cs`, `DungeonRunPayout.cs`.
- **QUEUE:** `JobKind.JewelPolish` rides the **RESEARCH** channel — no bespoke timer, no `Update`, no
  timestamp (canon §8). It inherits persistence, offline accrual, the depth cap and the slot economy free,
  and now competes with troop/perk research for lab slots — the CoC ratchet applied to a new verb at zero
  cost.
- ⛔ **PAID INSTANT-FINISH EXCLUDED BY OWNER RULING, AT THE MECHANISM.** The rush verb is generic over
  `JobKind` (it matches by `StructureId` and never consults kind), so a new kind **INHERITS**
  purchasability — omission would not have held. `JobRushPolicy` gates three sites: price returns 0 (no UI
  can render the button), `TryInstantFinish` gates BEFORE price/wallet (so the refusal cannot read as
  "you're broke"), and `CompleteAnyJob` is gated to wall the charge-then-complete bypass. Refusals are
  LOUD, with the WHY at the refusal site: **a paid instant resolve of a RANDOM outcome is a loot box**,
  regulated in several shipping jurisdictions. Ad-skip stays allowed; deterministic kinds untouched.
- ⚠ **SHATTER IS THE ANTI-PAY-TO-WIN MECHANISM, NOT FLAVOUR** — reasoning recorded at the roll site.
  Re-rolling costs no material, so unlimited paid attempts would converge on the top tier with CERTAINTY
  (trade-down does not stop it — the player simply stops when satisfied). Shatter re-ties attempts to
  earned material.
- **NO FLOOR on trade-down** (owner): a floored re-roll is a chore, not a gamble — with no downside it is
  strictly dominant and the decision evaporates. Self-balancing: whoever holds a top-tier stone will not
  risk it.
- **Staking grants ATTEMPTS, never odds.** The owner agreed immediately when the "+5% odds" version was
  flagged as breaking her own fairness model. `IPolishBonusProvider`, zero default, behind
  `FeatureFlags.StakingPolishBonus` — **default OFF** for Play-store token-gating compliance.

## Deliberately NOT done

- **No chain query in this lane** — the staking bonus provider is a seam with a zero default, not a wallet
  integration.
- No new timer, no new panel framework, no second polish surface.

## Owner decisions left open

- The §5 rulings this WO was blocked on were **taken live during implementation** and are recorded above
  (rolls per stone, no floor, shatter, paid-attempt disclosure, staking = attempts). Nothing is
  outstanding, but the **odds table in `jewel-polish.json` is the tuning handle** and has not been felt-
  tested.
- `FeatureFlags.StakingPolishBonus` stays OFF until she rules on token-gating.
