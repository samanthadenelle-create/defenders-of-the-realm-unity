# WO-1517: the train/army screens never say the queue is full, the army is full, or whether a troop can be upgraded

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:15:00, build 2026.09.07.359076) - "owner in chat 2026-09-07 09:1x, verbatim: 'the 15 verify are the new screen UI work correct? THose I verified' - the board panel listed Fixed rows only, so t...". PRIOR STATUS: AWAITING OWNER MATCH - device frame vs mockup panel 5 (TROOP DETAIL), 6 (TROOP DETAIL locked) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate)*
**Silo:** Manage 2000-block - `ManageScreenVM` army/troop VMs + `ManageWorkspacePanel` troop detail
(WO-2008 / 2009).
**LANDS AFTER** tonight's `ManageScreenVM.cs` commit (the WO-1405 lane).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1517 -> 1518 in the same edit).

## 1. EVIDENCE

Owner ruling, verbatim:

> "on train army screens should show if queue is full and army is full also should show if a troop type can
> be upgraded"

What the screens show today:

```
ManageFlow_ARMY_gridtop            portraits and padlocks only - no state words at all
owner-screen-20260906-200741.png   the Build tab paints the SAME undifferentiated green up-arrow on
                                   every tile, stating nothing
```

Meanwhile the authorities all exist and are unread by the view:

```
BuildTimerConfig.queueDepthPerLine = 5        the Train line's depth cap
ArmyStorage  armyUsed / armyCap               the army cap, as WO-1408's doors VM already reads it
the troop tier catalog + Research state       upgrade availability
```

So a player at the cap taps TRAIN and gets nothing, with no sentence anywhere explaining why. This is the
same composed-but-unpainted family as WO-1444 and WO-1491.

## 1B. THE TROOP DETAIL SCREEN (second owner ruling, same minute)

> "see screen needs clear should show stats and what upgrade will promote to"

Device frame `Logs/device/screens/owner-screen-20260906-201037.png` (Archer troop detail, build 358574).
What is on it:

```
header    "Level 5 . TRAINABLE"
body      "Back-line ranged DPS. Fragile but hits hard. . L7 unlocks Thunderbolt"   (flavour ONLY)
row 1     label "Next"  ->  value "Train one: 1m 0s . Ready"
row 2     label "Time"  ->  value "Upgrade: 12m 0s . Ready"
button    "TRAIN . 1M 0S"
footnote  "Army is full."   small, bottom-left, while TRAIN still invites the tap
```

No stats of ANY kind. No UPGRADE button, although row 2 says the upgrade is Ready. Both row LABELS name
something other than their values. And the one sentence that would stop a wasted tap is a footnote under the
button that contradicts it.

A player cannot answer the two questions this screen exists for: what is this troop, and what does upgrading
it get me.

## 2. FIX SHAPE

- The VM composes THREE explicit strings; the View paints them and computes nothing:
  1. a **QUEUE FULL** band on the train door when the Train line is at `queueDepthPerLine`;
  2. an **ARMY FULL** band when `armyUsed >= armyCap` - and the TRAIN verb is REFUSED with that reason, never
     a silent no-op;
  3. per troop tile, an **UPGRADE AVAILABLE** / **MAX** / **NEEDS &lt;research&gt;** word from the authority.
- The green arrow badge either states one of those three, or it is removed (same call as WO-1516).

**On the troop DETAIL screen (section 1B):**

1. A **STATS BLOCK** from the troop tier catalog for the CURRENT level: health, damage, range, speed, cost,
   training time.
2. A **NEXT LEVEL column** carrying the same stats at level+1, so the promotion reads as a before/after -
   the SAME before -> after table the mockup already draws for buildings (WO-2007), not a second pattern.
   Keep the unlock line ("L7 unlocks Thunderbolt") alongside it.
3. An **UPGRADE button beside TRAIN** whenever upgrade is Ready, with its time and cost on its face.
4. **Row labels that match their values** - retire the `Next` / `Time` labels or make them true.
5. **ARMY FULL becomes a band on the TRAIN button**, replacing the footnote, and TRAIN is refused with that
   reason.

## 3. WHAT NOT TO DO
- Do not let the View recompute the caps. One authority per fact; the View paints.
- Do not disable the TRAIN button silently. A refused verb must say its reason.

## 4. ACCEPTANCE
- [ ] One MEASURED case per string (queue full, army full, each of the three upgrade words).
- [ ] A case that TRAIN is REFUSED with the ARMY FULL reason when `armyUsed >= armyCap`.
- [ ] A MEASURED case that the troop detail shows level AND level+1 stats from the troop tier catalog, for
      EVERY troop id - not just the one the capture happens to open.
- [ ] The UPGRADE button appears beside TRAIN whenever upgrade is Ready, carrying its time and cost.
- [ ] No row label contradicts its value on the detail screen.
- [ ] Headless `ManageFlow_ARMY_*` PNGs opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.

## 5. LANE HAND-BACK (edit-only lane, 2026-09-06)

All of it landed in `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs`; the renderer needed no
change, because every face used here already exists in `ManageWorkspacePanel`.

- **`FillTrainFacts`** now reads `ArmyReadiness.Compute(state)` + `TroopDialogueCommands.SlotOf` -
  the SAME formula and slot reader `BarracksService.EnqueueTraining` seeds its own refusal from -
  and tests the ARMY CAP **before** the line depth, matching the service's own order. New
  `TroopChoiceVM` fields: `ArmyFull`, `ArmyFullText`, `QueueFullText`, `ArmyUsedSlots`,
  `ArmyCapSlots`.
- **`ComposeTroopItem`** refuses TRAIN with the army-full reason as `QueueBlocked` + `Route.None`
  (reasoning recorded in-code: `Unaffordable` would claim a wallet that is never charged since
  WO-1387; `PrerequisiteBlocked` needs a routable door and raising the cap is not one destination;
  a routable blocked action has its LABEL replaced by the route CTA, which would delete the TRAIN
  verb the band is explaining). Tile badge precedence is now UPGRADING -> ARMY FULL -> QUEUE FULL ->
  the upgrade word -> TRAINABLE -> MAX -> IDLE.
- **`FillUpgradeFacts`** composes `TroopChoiceVM.UpgradeWord` -
  `UPGRADE AVAILABLE | MAX | UPGRADING | NEEDS <blocker>` - asked of `BarracksService
  .CanUpgradeTroop`, never re-derived. The Research LINE being full is deliberately excluded: that
  is the queue's state, not the troop's.
- **The detail card**: `TroopStatRows(c)` emits Health / Damage / Range / Speed (current value +
  `DeltaText` at level+1, from `TroopStatResolver.Effective` - the resolver `TroopDeployer` applies
  to the live unit) plus Train time. `TwoFacts` now takes its LABELS from the caller, retiring the
  hardcoded `Next` / `Time` on the Research and Build cards too. `ComposeDetail` seats the composed
  Upgrade action in the card's SECONDARY face slot - it had always been composed and never painted,
  because `ProjectSelection` fills that slot from `ActionOf(Cancel)` and a troop has no Cancel.
- **Measured cases** in `ManageTroopsTrainDoorRegression`: **case 8** walks EVERY unlocked troop's
  detail card (row count, labels, the retired `Next`/`Time` labels banned by name, a level+1 delta
  wherever a next level exists, the UPGRADE face with its time on it); **case 9** fills the fixture
  army through `BarracksProgression.GrantTrainedTroop`, then asserts `ArmyFull`, `TrainReady=false`,
  a `TrainStateText` naming the army, an `ARMY FULL` tile word, a TRAIN tap that enqueues nothing
  and leaves the reason as the notice - and (WO-1518) that the notice does not travel.

**Not done here:** `ManageScreenPanel.cs` was another lane's uncommitted work and was not touched.
Its legacy operational troop card reads `TroopChoiceVM.TrainStateText` / `UpgradeCostText`, both of
which now carry the new sentences, so it inherits the fix without an edit.
