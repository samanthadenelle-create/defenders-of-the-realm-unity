# WO-1518: research rows say SHORT without naming what, and LOCKED without naming the blocker or linking to it

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 8 (RESEARCH tree) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate)*
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

## 5. LANE HAND-BACK (edit-only lane, 2026-09-06)

### WHICH ROWS CARRIED A DOOR BEFORE THE CHANGE (acceptance line 6, answered at source)

`ManageScreenVM.ComposeResearchItem` composed a **`PrerequisiteBlocked` action with
`Route = ManageRoute.ToBuildCard(c.BuildingId, "VIEW BUILDING")` on EVERY locked perk** (the
`if (c.Locked)` arm). `ManageVmProjection.ProjectAction` turns any blocked action with a routable
route into a live, ENABLED door. So **100% of locked research rows already had a working door** -
the routing was never the defect, exactly as the ticket says. What no face carried was the WORDS.
(The `ResearchChoiceVM.Activate` seam the ticket cites is the OTHER, legacy path -
`ManageScreenVM.cs:444` in the pre-change numbering - still non-null for Locked and Available and
null for Researched/Researching. It is untouched.)

### WHAT LANDED (`ManageScreenVM.cs` only - the renderer needed no change)

- **SHORT names what.** New `ShortBadgeText(IReadOnlyList<CostPart>)` computes
  `Amount - BankOf(ConceptId)` over the item's OWN cost basket - the same parts the cost row paints
  and the same bank reader `CostVms` uses - and emits `SHORT 120 IRON`. Wired into
  `ComposeResearchItem` and `ApplyBuildBadge` (which now takes the cost parts). No second
  affordability predicate. It stays a WORD + NUMBERS, not a sentence: the research row's state
  column is ~a quarter of the row wide with an 18px `FitSingleLine` floor, and a sentence there is
  culled blank.
- **LOCKED names why, and says what the tap does.** `BadgeText` becomes `LOCKED - TAP` (the short
  form the state column can hold), and the blocker sentence is JOINED onto `NextRungLine`, which the
  renderer paints as the row's wide SECOND line. So the row reads
  `name / "Troop damage +8% . Requires Barracks Tier 3" / [padlock] LOCKED - TAP`. The `- TAP` half
  is derived from whether a door can be routed, never assumed.
- **The green arrow leaves a SHORT row.** `ProjectAffordanceTile` (added by WO-1516) withholds the
  status medallion whenever the tile's state is the `Available` catch-all and its primary action is
  refused. Applied to research perk tiles as well as BUILD and ARMY.
- **The `Army is full.` footer.** It is NOT a footer: it is `ManageScreenVM.Notice`, the single band
  `ManageScreenPanel.BuildNotice` seats beside CLOSE, still holding the sentence `BarracksService`
  handed back on a refused TRAIN tap. Nothing cleared it, so it rode the back stack onto the Armorer
  research screen. New `ClearStaleNotice(destination)` is called from `EnterTab` and `GoTo`: a
  refusal belongs to the screen whose verb was refused. Fixed in the NAVIGATOR, not the band -
  suppressing it in the View would need the View to decide which sentences belong on which screen.

### MEASURED CASES
- `ManageRowBenefitRegression.CheckResearchRowsSayWhatAndWhy` - **empties the fixture's purse**
  (the existing fixture is deliberately rich and could never produce a SHORT row), walks every
  school's composed perk tiles and asserts: a `SHORT` word carries a digit; a `SHORT` row carries no
  status medallion; a LOCKED row's state word carries `TAP`; a LOCKED row's second line contains
  `ResearchChoiceVM.LockReason` verbatim. It FAILS rather than skips when no SHORT and no LOCKED row
  was produced.
- `ManageTroopsTrainDoorRegression` case 9 (shared with WO-1517) proves the `Army is full.` sentence
  does not survive navigation to another Manage screen.

**Not verified by this lane (edit-only):** the `PanelDoorRegression`-class check that each locked
row's door opens a REGISTERED `PanelId` with a return door, and the headless research PNGs. Both
need the Unity gate.
