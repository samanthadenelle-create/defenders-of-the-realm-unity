# WO-1541 RESULT - one producer names the camp, and the army line is the door

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 1 (MANAGE hub) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. **All four acceptances landed.**)*
Edit-only lane: no Unity run, no gate, no commit. Read at source this session (CLAUDE.md 11B).

## 1. THE SEAT RULING THAT CLOSED ACCEPTANCE 3
Owner, 2026-09-06 (question tool): **"RAISE THE CARD, TAPPABLE ROW."** Recorded as WO-1541 s4.1. It
resolves the collision this lane measured and refused to decide alone (s11B.B): ruling 2 wanted a
door, **WO-1422 ruling 3.10** bans squeezing a third face beside TRAIN + UPGRADE, and `MinTouchPx`
(112) refused the 26px band. It takes the option 3.10 itself names - *"a third face needs a taller
card"* - so **3.10 is honoured, not overruled**: the door is its own row above the CTA band.
## 2. THE DOOR (`ManageScreenPanel.cs`)
- **`:255-307` - the ARMY card's own band ladder.** `TroopArmyDoorRowPx = 112` (== `MinTouchPx`),
  `TroopCardPx = 346` (260 + the army band's 26 -> 112 growth, nothing else). ⛔ **Every fraction is
  DERIVED from px constants, not typed** - the 2026-09-06 "bare plate" RCA on this card was a typed
  pair resolving to 18.2px, under TMP's cull floor.
- ⛔ **A SEPARATE LADDER, not an edit to `TroopCtaY0/Y1`.** Those and `TroopWorkspacePx` are shared by
  the **Buildings, Defense and Research** cards, which stay byte-for-byte untouched. **`:3838`** the
  Troops workspace is seated at `TroopCardPx`; **`:3814-3818`** the fold trace reports the real height.
- **`:4040-4067`** - one `BuildObsidianButton` named `TroopCta_RaidDoor`, authored to 112px
  (`ClampMinTouch` still called, nothing left to rescue), plus an **ASCII `>` chevron** at x
  0.90-0.97. ⛔ Chevron, not colour: the owner is red/green colourblind; non-ASCII renders tofu.
  **`:4068-4076`** with no camp published the row stays a LABEL. **`:4083`** the state badge moved to
  x 0.72-0.90 so it cannot overlap the chevron; it gained the full band and lost nothing.
- **`ManageScreenVM.cs:2090-2097`** - the door calls **`RaidEntryGate.RequestOpen`**, the exact call
  the Journey deck's Raids card makes (`PlayerDeckWorkspace.cs:746`). ⚠ Deliberately **not**
  `RaidSelectionScreen.Open()` (legal here) and **no new `PanelId`**: one Core seam, one raid door.
## 3. THE FACT, THE ONE PRODUCER, THE TYPOGRAPHY
- `PostureSignals.cs:355-395` - `RaidNextCampName` / `RaidNextCampGarrison` / `RaidNextCampChanged` /
  `SetRaidNextCamp` (`:380`), change-only + `FlowTrace.Step`, the `SetRaidOpenCampCount` shape.
- `BuildTimerService.cs:2292-2348` - the ONE producer, picked in the SAME loop that counts open
  camps. The three lines `JourneyDeckSubtitleRegression` pins verbatim are untouched.
- `ManageScreenVM.cs:2033-2079` - `BuildTroopArmySummary` READS the signals; `new
  Hero.RaidSelectionVM(` is gone. Door published `:517-528`. Acceptance 4: `FontMicro` ->
  `FontLabel`, `ParchmentDim` -> `Parchment`, bold - now in a 112px row, so the rank-up is height too.
## 4. ORACLE - `ManageTroopsTrainDoorRegression`
- **Case 11** (`:741`, called `:302`) - fixture: publishes a camp no catalog contains and asserts the
  VM says it back with a non-null door; no camp -> no clause, no door.
- **Case 12** (`:903`, called `:72`) - tripwire: forbids a second `RaidSelectionVM` walk, requires
  the read and the publish, fails on a return to `FontMicro`. ⚠ Source-shaped by nature, said so in-file.
- **Case 14** (`:970`, called `:73`) - the ruling's three halves, replayed off the constants the
  renderer reads (the `ManageQueueDrawerRegression` case-9 idiom): row >= `MinTouchPx`; the card SUM
  grew past the legacy 260; **no neighbouring band dropped below its original px**; the workspace is
  seated at `TroopCardPx`; `TroopCta_RaidDoor` exists, reads `_vm.TroopArmyDoor`, opens via
  `RaidEntryGate.RequestOpen`. **RED:** `TroopArmyDoorRowPx = 26f`, or re-seat at `TroopWorkspacePx`.
## 5. REGISTRATION - none. `[manage-train-door]` is already at `DataRegression.cs:438`.
## 6. OWED, AND ONE CROSS-LANE FINDING
- ⚠ **`DrawerModeListKeepPx` now describes the OLD card.** It is `10 + TroopWorkspacePx * (1 -
  TroopCtaY1)` = 154.3px, and `ManageQueueDrawerRegression:273` pins that string **verbatim**, so it
  stays green while measuring a 260px card the Troops tab no longer paints (346px). At the reference
  well the fold is now `10 + 346 + 8 + 120 = 484 > 401`. **This is WO-1488's silo, so it was not
  touched.** It affects only the legacy `!WorkspaceActive` path (`RenderList:3112` returns early when
  the workspace owns the well). **Route to WO-1488.**
- `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on **fresh** logs, judged by the marker.
- All three oracle cases **proven RED before green** (this lane cannot run Unity).
- **Fresh** Manage/ARMY captures, opened - no frame in the repo postdates this code. Capture-judged:
  the summary is centred in the row by `BuildObsidianButton`, and the badge takes x 0.72-0.90.
- `JourneyDeckSubtitleVM` deliberately unchanged (WO s4): the deck reads the COUNT, the Manage line
  the NAME - one producer, two facts. Then owner felt-verify + close (s13: the PO closes, not CLI).
