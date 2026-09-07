# WO-1518: research rows say SHORT without naming what, and LOCKED without naming the blocker or linking to it

**Status:** READY TO IMPLEMENT - owner rulings, 2026-09-06 20:12
**Silo:** Manage 2000-block research - `ManageScreenVM` research choice VM + `ManageWorkspacePanel` research
rows (WO-2010 area).
**LANDS AFTER** tonight's `ManageScreenVM.cs` commits (the WO-1405 / 1516 / 1517 lane).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1518 -> 1519 in the same edit).

## 1. EVIDENCE

Owner rulings, verbatim:

> "see screen, short doesnt help, i need to know waht im short"
> "if locked what is blocking and link to it"

Device frame `Logs/device/screens/owner-screen-20260906-201242.png` (build 358574, 20:12), Armorer research:

```
Reinforced Plating   Troop health +5%    [green arrow]  SHORT
Sharpened Edges      Troop damage +8%    [padlock]      LOCKED
Sturdy Shields       Troop health +10%   [padlock]      LOCKED
footer               "Army is full."     -- on a RESEARCH screen
```

`SHORT` names no resource and no amount. `LOCKED` names no blocker and offers no door.

**THE DOOR ALREADY EXISTS. THE DEFECT IS THE WORDS.** Owner note at 20:19, verbatim:

> "the logic is there on some if i click takes me there but should tell them that"

Confirmed at source - the VM composes BOTH the reason and the door already
(`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs`):

```
:422   StateWord   "Researched" | "Researching" | "Available" | "Locked"
:425   Locked      bool
:428   LockReason  BuildingPerkService.CanResearch's out reason, VERBATIM
                   ("" when not locked; a suite asserts exact equality)
:439   CtaLabel    "RESEARCH" | "RESEARCHING" | "UPGRADE THE HEART" | "UPGRADE <NAME>" | null
:442   DoorLabel   NULL (ruling 3.5)
:444   Activate    () => Research(bId,pId) when Available, OpenUpgradePanel(bId) when Locked
```

So WO-1390 / WO-2013 did ship the routing: `Activate` IS the door on a locked row, and `LockReason` IS the
blocker sentence. `DoorLabel` is deliberately NULL by ruling 3.5, and `LockReason` is reaching no face - which
is exactly why the row renders as a bare `LOCKED` that silently teleports the player somewhere.

This is the composed-but-unpainted family again (WO-1444, WO-1491, WO-1517). **Do not rebuild the routing.**

## 2. FIX SHAPE

- The VM composes the REASON; the View paints it and computes nothing.
- **SHORT becomes `SHORT 120 IRON`** - each missing resource with its shortfall, from the SAME cost authority
  the Build palette uses. WO-1411's affordability words are the precedent, never a second predicate.
- **LOCKED paints the EXISTING `LockReason`** plus a TAP AFFORDANCE, because `Activate` is already wired:
  `LOCKED - NEEDS BARRACKS L3 - TAP TO VIEW` (or the kit's tap chevron beside the blocker name). The face must
  say what the tap will DO - a row that navigates without announcing it is the whole of the 20:19 note.
- **First, name at source which rows carry a door and which do not.** `Activate` is non-null for Locked and
  Available and NULL for Researched/Researching (`ManageScreenVM.cs:444`); audit whether every locked row
  actually reaches that branch. Rows WITHOUT a door get one through the SAME seam (`Activate` ->
  `OpenUpgradePanel`), never a second routing path. WO-2013's `VIEW BARRACKS` is the precedent.
- The `Army is full.` footer paints ONLY where the army cap is the refusing reason. It is a WO-1517 word on
  the train door, not a global footer.
- The green arrow badge on a SHORT row states nothing; remove it (same call as WO-1516 and WO-1517).

## 3. WHAT NOT TO DO
- Do not hide SHORT or LOCKED rows. The owner asked to be TOLD what is missing, which is the opposite of
  filtering - and it is the deliberate contrast with WO-1516, where the BUILD grid does filter locked items.
- Do not write a second affordability or prerequisite predicate. One authority per fact.

## 4. ACCEPTANCE
- [ ] Measured case: every SHORT row names at least one resource AND an amount.
- [ ] Measured case: every LOCKED row's face names its blocker AND carries the tap affordance whenever a door
      is wired.
- [ ] A SOURCE-SHAPE case that FAILS if a locked row has a door (`Activate != null`) but no affordance text.
- [ ] Measured case: every LOCKED row's door opens a REGISTERED `PanelId` (`PanelDoorRegression`'s class of
      check), with the return door set.
- [ ] Measured case: the `Army is full.` footer is ABSENT on research screens.
- [ ] The RESULT states which rows carried a door BEFORE the change, with file:line.
- [ ] Headless research PNGs opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.
