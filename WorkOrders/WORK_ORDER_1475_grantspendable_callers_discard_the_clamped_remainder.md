# WO-1475: every GrantSpendable caller discards the clamped remainder - quest rewards and mine nodes burn by omission

**Status:** SPEC - needs an owner ruling (loot into a full bank: lost, or retained?)
**Silo:** `EconomyService.GrantSpendable` + `QuestRewardBridge` + `MineNode`. Sibling of WO-1445, which
covered the offline-harvest path only.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1475 -> 1476 in the same edit).

## 1. EVIDENCE

```
EconomyService.cs:431-472   computes the lost amount and discards it via  out _
QuestRewardBridge.cs:128    ignores the returned ResourceCost
MineNode.cs:589             ignores the returned ResourceCost
MineNode.cs:594             ignores the returned ResourceCost
```

The service DOES compute what did not fit; every caller throws it away. So a quest reward or a mine node
claimed against a full bank silently pays less than it promised, with no popup, no pending, and no trace.

WO-1434's D3 law - capped yield is recoverable, not burned - is scoped to HARVEST. It does not say what
happens to LOOT.

## 2. THE RULING NEEDED

**Is loot into a full bank LOST, or RETAINED as pending?**

- **Lost** is the Clash of Clans behaviour and is a real pressure to upgrade storage
  (memory `design-tiebreaker-what-would-coc-do`, `stockpiles-cap-capacity`).
- **Retained** matches the law the owner already gave for harvest, and is the kinder read of a quest reward
  the player earned by playing.

Recommendation, stated so the ruling is one word: **lost for world loot (mine nodes), retained for promised
rewards (quests, raid spoils)** - the distinction being whether a number was PROMISED on a screen first.

## 3. FIX SHAPE (once ruled)

- One rule, applied at `GrantSpendable`, not at each caller. Callers stop ignoring the return.
- Either way, a permanent `FlowTrace.Warn` at the clamp naming the resource and the remainder, so a burn is
  never silent again.

## 4. ACCEPTANCE
- [ ] The ruling recorded in this file and in `DESIGN-DECISIONS.md`.
- [ ] No caller ignores the `GrantSpendable` return (grep pasted in the RESULT).
- [ ] Regression per path: full bank + quest reward, full bank + mine node.
- [ ] `REGRESSION_OK n/n` on a fresh log.
