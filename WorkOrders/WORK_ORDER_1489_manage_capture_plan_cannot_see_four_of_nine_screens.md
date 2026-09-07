# WO-1489: the Manage capture plan cannot SEE four of the nine mockup screens, and two frames are CAPTURE_LEDGER_MISSING

**Status:** READY TO IMPLEMENT
**Silo:** Manage 2000-block (WO-2016, the capture plan) + `ManageWorkspacePanel` + a sprite asset.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1489 -> 1490 in the same edit).

## 1. EVIDENCE

Two frames never captured at all:

```
ManageFlow_BUILD_locked      CAPTURE_LEDGER_MISSING
ManageFlow_ARMY_max          CAPTURE_LEDGER_MISSING
```

And `ManageFlow_BUILD_max` shows NO before/after stats, no cost row, no time and no UPGRADE button - although
every painter exists:

```
ManageWorkspacePanel.cs:1006-1016   before/after stats
ManageWorkspacePanel.cs:1110-1115   cost row
ManageWorkspacePanel.cs:1140-1142   time / upgrade action
```

So the plan is not reaching the states that would paint them. The catapult sprite also renders an ALPHA
CHECKERBOARD in the captured frame.

Net: four of the nine mockup screens (hub, upgradeable detail, trainable detail) are not observable by the
capture plan, so "all nine match" is unprovable - not wrong, unprovable.

## 2. FIX SHAPE

- Extend `BuildManageFlowPlan` to drive the four unreachable states: the hub, an upgradeable detail with a
  next level, a trainable detail, and the locked/max variants.
- Fix the catapult sprite's alpha (re-import or re-author) so the frame shows the art, not the checkerboard.
- The ledger must FAIL on a planned frame that goes missing, rather than logging it.

## 3. WHAT NOT TO DO
- Do not re-assert "all nine screens match" until the plan covers nine and every frame is present in the
  ledger. The capture-round count is not evidence.

## 4. ACCEPTANCE
- [ ] Nine frames present, zero `CAPTURE_LEDGER_MISSING`, on a fresh run.
- [ ] `ManageFlow_BUILD_max` shows stats, cost, time and the UPGRADE button; PNG opened.
- [ ] Catapult sprite renders opaque.
- [ ] `REGRESSION_OK n/n` on a fresh log.
