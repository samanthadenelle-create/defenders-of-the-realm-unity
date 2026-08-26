# WORK ORDER 1208 - RESULT: the collector fallback reconciles instead of racing scene load

**Status:** IMPLEMENTED 2026-08-25 - awaiting the owner's device verification (a dungeon round trip:
leave town, return, confirm the Quarry still pays).
**Commit:** `a79bd0644`
**Gates:** `COMPILE_GATE_OK` + `REGRESSION_OK 284/284 suites`, both marker-asserted on fresh logs.

---

## What was actually wrong

Not a missing component. The fallback wiring was evaluated **synchronously at `sceneLoaded`**, where
it could run either before `GameState` was ready - a null state correctly reads as "nothing built" and
withholds a fallback - or while the OUTGOING placed collector was still registered, moments before its
own scene-teardown `OnDisable`. Either way no collector was registered afterwards, and **there was no
state-ready or post-teardown retry to notice.**

The owner's device showed the registry flapping inside one session, which is what a race looks like
from outside:

```
19:12:31  existence gate OPEN for 'farm' (liveCollector=yes)
19:22:31  'farm' ... NO ResourceCollector is registered - 13 WITHHELD this tick
19:29:25  existence gate OPEN for 'farm' (liveCollector=yes)
```

⭐ And the building is the **Quarry** - `collector_farm` already reads displayName "Quarry", role
`stone_producer`. So this was the town's primary Stone faucet paying nothing, not a legacy food
building awaiting retirement.

## What landed

- The DDOL host owns a retry driver bound to `GameStateService.StateReplaced`, with an immediate
  catch-up when the state is already loaded, plus a **next-frame reconciliation on every scene
  transition** so teardown ordering cannot hide a collector.
- **Single owner per id.** A real placed collector outranks the DDOL fallback and takes the id; the
  fallback is **PARKED**, not merely deactivated, and parking suppresses its disable-time save so a
  stale snapshot cannot overwrite the placed collector's PlayerPrefs. Two writers on one key is the
  failure this repo keeps paying for.
- The formerly **SILENT** `if (host == null) return;` now warns. A silent failure is forbidden
  (CLAUDE.md sec.12) and that return was one.

## ⭐ Two self-reviews rejected earlier versions, and that is why this is safe

1. An unconditional `OnDisable` retry would have **RESURRECTED a sold Quarry** from the monotonic
   ever-built ledger, which cannot see a sale.
2. State replacement could retain a live fallback across `ResetToNewGame`, **paying a brand-new town
   from a dead save**. Reset now parks fallbacks absent from the new ledger without persisting them,
   and real placed collectors are never disabled by that ledger check.

Both are the kind of defect that ships green and surfaces weeks later as "my resources are wrong".

## Relationship to the lead's earlier mitigation - both are needed

`ResourceBuildingHarvester` was changed earlier the same evening so a tick with no live collector is
**HELD, not burned** (the rollover consumed the interval *before* the null check, so the payout was
computed and destroyed). That mitigation is untouched here and still clamped to exactly one owed tick,
because the monotonic ledger cannot see a sold collector.

**This ticket fixes the CAUSE; that mitigation keeps the VALUE safe while the cause is absent.** Do not
remove one on the grounds that the other exists.

## Harvest note

Written in an isolated dev worktree and harvested by the committer by explicit path, with line endings
normalised to the repo's CRLF - the source carried MIXED endings, which would otherwise have rendered
a 125-line change as a whole-file rewrite.

## What is NOT proven here

Device behaviour. The gates prove the tree compiles and 284 suites are green; they cannot prove the
Quarry pays after a dungeon round trip on a phone. That is the owner's verification and it is the
reason this ticket is not closed.
