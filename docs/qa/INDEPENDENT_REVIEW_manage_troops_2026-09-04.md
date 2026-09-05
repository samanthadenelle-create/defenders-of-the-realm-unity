# INDEPENDENT UX REVIEW - Manage > Troops (2026-09-04)

Reviewer: second, independent designer seat. Formed WITHOUT reading WO-1382.
Inputs: device screenshot `seeker-223408-troops.png` (Solana Seeker, 2670x1200 landscape),
owner's words ("something is wrong with manage troops screen i think its the box around train
... redesign this screen more intuitively"), and a read of the real seams:
`Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` (Troops tab: lines 1421-1720),
`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` (`TroopChoiceVM` line 214,
`BuildTroopsBrowse` line 948), `Assets/_Modules/Core/UI/ElarionUiKit.cs` +
`ElarionUiKitObsidian.cs` (primitive list), and the four `Manage*Regression.cs` suites.

Coordinates below are NORMALIZED to the 2670x1200 frame, x left->right, y TOP->bottom
(screenshot convention). Where I state what the code does I cite the line; where I only infer
from pixels I say so.

---

## A. What is on screen (element by element)

Panel: one obsidian modal, gold-rimmed, x 0.04-0.96, y 0.045-0.92. Scrim behind it is thin
enough that HUD elements bleed through on both sides: a "FL.." chip at (0.03, 0.33) and a
"6" glyph at (0.96, 0.39).

Title row (y 0.08-0.13):
- "MANAGE - TROOPS" gold, centred at (0.56, 0.08).
- BACK button, x 0.06-0.22 (code: `_workspaceBack`, panel line ~520).
- QUEUE button, x 0.78-0.92 (code: `ManageHeaderActions` toggle, line 1047).

Line strip (y 0.15-0.19): three chip-bars reading "Builders 0/2" (x 0.08-0.35),
"Training 0/2" (x 0.38-0.62), "Research 0/2" (x 0.65-0.89). Each has an emblem on its left.
None looks tappable (they are status bars, `BuildStrip`).

Pager stubs, CLIPPED: two dark plates at x 0.72-0.79 and x 0.80-0.87, y 0.19-0.22, with only
their bottom third visible under the strip. Code: `BuildTroopWorkspacePager` "<" / ">"
anchored at workspace y 0.72-0.99 (line 1572-1575). Their glyphs are not readable on device.

Troop rail (left column, x ~0.14-0.22): three circular medallions stacked vertically, centres
at y 0.21 (top one clipped by the strip), 0.32 (selected - a gold outline is just visible), and
0.42 (dimmed, with a padlock badge). Code: `TroopSelectorRail` at workspace x 0.01-0.18,
3 per page (line 1529, 1539). No names beside them (deliberately - comment at line 1656).

THE BOX (the owner's "box around train"): a gold-bordered dark plate x 0.115-0.88, y 0.22-0.39.
It encloses: "Available" (green text, x 0.29, y 0.23 - partly cut by the box's top border),
the description "Front-line melee bruiser. Day-one workhorse." (x 0.29-0.585, y 0.26), and two
wide buttons on one line: "TRAIN" (x 0.34-0.54, y 0.30-0.35) and "UPGRADE OPTIONS"
(x 0.58-0.825, y 0.30-0.35). Both buttons render as identical dark plates with cream serif
text; nothing marks which mode is active. Code: `BuildTroopWorkspaceModes` (line 1684) tints
the selected one `Yellow`, but `BuildObsidianButton` re-skins through
`MedievalUiSkin.ApplyButton(pfBtn, color == Yellow)` (ElarionUiKitObsidian.cs:648) and the
panel then calls `MedievalUiSkin.ApplyButton(train, _troopMode == 0)` again (line 1692) - on
device the "primary" and "non-primary" faces are indistinguishable. I have not proven WHICH
Image draws the box border; from the pixels it is consistent with the workspace's own
`ApplyRowSurface` content-panel frame (line 1537) whose visible rim sits inset from the
420px row, but that is inference, not a captured fact.

The title "FOOTMAN - LEVEL 1" is NOT visible anywhere. Code places it at workspace y 0.82-0.98
(line 1554-1558); the top of the workspace is above the strip on this capture, so it is
clipped. The player sees "Available" with no noun above it.

Below the box (y 0.39-0.46), OUTSIDE it: "Train Footman" (bold, x 0.29, y 0.39), "550 gold"
(x 0.29, y 0.45), "Ready" (x 0.59, y 0.42), and a THIRD button labelled "TRAIN"
(x 0.74-0.86, y 0.40-0.45). Code: `TroopSelectedAction` zone, workspace y 0.00-0.34, filled by
`BuildBrowseRowContent` (line 1608-1611) - the same three-column browse row used by every
other tab (name+cost | affordability sentence | CTA at PrimaryX0-X1 = 0.76-0.98).

Armies row (y 0.51-0.56): "Saved army compositions" (x 0.12) and an "OPEN ARMIES" button
(x 0.71-0.87). Code: `AddActionNoteRow` at line 1449.

Scrollbar: a thin gold track at x 0.90, y 0.25-0.50 - the list scrolls.

CLOSE button: x 0.42-0.58, y 0.60-0.64 (the shared `CloseButton`). Dead space below it to the
panel rim (y 0.64-0.92) and to its left/right.

---

## B. What is wrong, ranked by how much it hurts a first-time player

1. THREE things say "TRAIN" and only one of them trains. The boxed "TRAIN" (0.34-0.54, 0.30)
   is a MODE toggle that is already active; tapping it does nothing visible. The unboxed
   "TRAIN" (0.74-0.86, 0.40) is the real verb (VM `Train Footman` row -> `TrainTroop(id)`,
   VM line 1001). A first-time player taps the bigger, higher, centred one first, sees no
   change, and concludes the screen is broken. This is the owner's "box around train": the
   frame promotes the toggle above the action. A toggle and a verb sharing one word on one
   screen is the single worst defect here.

2. The active mode is invisible. Even a player who understands "TRAIN | UPGRADE OPTIONS" is
   a segmented control cannot tell which segment is selected: both faces are identical on
   device (see A). The only signal is the row that appears underneath, which reads
   "Train Footman" in one mode and "Upgrade Footman -> L2" in the other - the player has to
   read the small row to learn the state of the big control above it. Inverted hierarchy.

3. The troop's NAME is off-screen. "FOOTMAN - LEVEL 1" is clipped above the strip, so the
   detail card opens with "Available" and a description with no subject. The pager arrows
   are clipped with it. The workspace is a 420px fixed row inside a scroll list, so whether
   the title is visible depends on scroll position - a name that sometimes exists.

4. The affordability sentence and cost are split across two boxes. "550 gold" sits in the
   unboxed row; the description and "Available" sit in the box. The player has to read
   across a frame border to assemble "Footman, 550 gold, Ready". Grouping is by code seam,
   not by meaning.

5. Nothing tells the player what a tap on TRAIN will DO. There is no "1 unit", no time, no
   "goes to the Training line", and after the tap the only feedback is the "Training 0/2"
   chip ticking to 1/2 a screen-height away. CoC shows the unit slide into the queue bar
   above the cards; here the queue is behind the QUEUE button and closed by default
   (`_queueDrawer.SetActive(false)`, pinned).

6. "UPGRADE OPTIONS" (plural, "options") promises a list; what it yields is one row. The word
   is longer than "TRAIN" so it also reads as the more important of the two.

7. The rail medallions have no names and only 3 per page. With 4+ troops the player pages
   with clipped "<" ">" arrows to find a troop whose face they may not recognise. Locked
   troops are dimmed grey + a padlock (good - not colour alone), but their REQUIREMENT
   ("Requires Barracks Tier 2") is only shown after selecting them.

8. "Available" is drawn green, "Requires ..." red (line 1566). The words carry the meaning
   so it passes the colour-alone rule, but for a red/green colourblind owner these two are
   the same colour, so the tint is doing nothing and the word is doing everything - the
   label would be better styled as a badge with a shape difference (check glyph vs lock).

9. Two exits (BACK top-left, CLOSE bottom-centre) with no label saying where each goes.
   BACK returns to the four category cards; CLOSE leaves Manage. A first-time player does
   not know a category launcher exists behind BACK.

10. Vertical budget: ~30% of the panel below CLOSE is empty while the top of the content is
    clipped. The 420px workspace + 132px armies row + 132px notice do not fit the well at
    this aspect, so the list scrolls even with one troop selected.

Not wrong, worth keeping: the three-column browse row itself (name/cost | sentence | CTA at
0.76-0.98) is a good, consistent shape; the affordability-as-sentence rule; the lock badge on
the medallion; the line strip as a persistent capacity readout.

---

## C. Redesign - Troops tab at 2670x1200

Principle: ONE verb per button, ONE box per meaning, the troop's name always on screen, and the
consequence of a tap visible without opening a drawer. Built only from existing primitives:
`BuildObsidianPanel` / `BuildObsidianModal` (chrome, already in use), `BuildObsidianButton`,
`Label` + `FitSingleLine` / `FitBlock`, `MakeScrollZone`, `AddImage`, `Portrait`, `Bar`
(BarKind for the training progress), `BuildLockBadge` (panel-local), `ToastCard` (existing
notice path), `ClampMinTouch`. No new widget family.

Fixed geometry (no scrolling of the workspace itself; only the troop rail scrolls):

```
 y=0.045 +------------------------------------------------------------------------+
         |  [ BACK ]          MANAGE - TROOPS                        [ QUEUE ]     |  title row 0.08-0.13 (unchanged, pinned)
 y=0.15  |  (Builders 0/2)     (Training 1/2 . 0/5 queued)     (Research 0/2)      |  strip 0.15-0.19; Training chip gains
         |                                                                          |  the queue-depth sentence, TAPPABLE -> opens drawer
 y=0.21  |  +-- RAIL ------+  +-- SELECTED TROOP --------------------------------+  |
         |  | (o) Footman  |  |  [portrait]  FOOTMAN                 Level 1     |  |  name band 0.23-0.28, always visible
         |  |     L1 *sel  |  |   0.34-0.44   Front-line melee bruiser.          |  |  desc 0.29-0.33
         |  | (o) Archer   |  |               Day-one workhorse.                 |  |
         |  |     L1       |  |                                                  |  |
         |  | (o) Knight   |  |  Train one:  550 gold  .  40s  .  Ready          |  |  fact line 0.35-0.39 (sentence, never tint)
         |  |   [lock] T2  |  |                                                  |  |
         |  | (o) ...      |  |  [ TRAIN 1 FOOTMAN ]        [ UPGRADE TO L2 ]    |  |  CTAs 0.41-0.50 (>=112px tall on device)
         |  |  scrolls     |  |   x 0.30-0.56                 x 0.60-0.86        |  |
         |  |              |  |                                                  |  |
         |  |              |  |  Upgrade: 300 wood, 120 iron . Short 40 iron     |  |  upgrade fact line 0.52-0.56 (sentence)
         |  +--------------+  +--------------------------------------------------+  |
         |    x 0.06-0.24        x 0.27-0.92                                       |
 y=0.60  |  +-- TRAINING NOW ----------------------------------------------------+  |
         |  |  (o) Footman  [=========------]  32s left    (o) Archer  queued 2nd |  |  inline queue band 0.60-0.70,
         |  |  Nothing training. Tap TRAIN to start.            [ OPEN QUEUE ]    |  |  read-only + one door; verbs stay in the drawer
         |  +--------------------------------------------------------------------+  |
 y=0.72  |  Saved army compositions                              [ OPEN ARMIES ]    |  armies row 0.72-0.80 (existing AddActionNoteRow)
 y=0.83  |                          [ CLOSE ]                                       |  shared CloseButton 0.83-0.90
 y=0.92  +------------------------------------------------------------------------+
```

Sizes (normalized to 2670x1200; every button is ClampMinTouch'd, so >= 112 canvas px):
- Rail: x 0.06-0.24, y 0.21-0.58; one entry per troop, each 0.09 tall (108 device px, grows
  to the floor via ClampMinTouch). Entry = medallion (existing bezel + icon) LEFT, name +
  "L<n>" text RIGHT of it. Selected entry keeps the gold Outline AND a filled chevron glyph
  ">" at its right edge (shape, not colour). Locked entry keeps the dim + padlock badge and
  shows "T2" (the required barracks tier) under the name. The rail is a `MakeScrollZone`;
  the "<" ">" pager buttons are deleted.
- Selected-troop card: x 0.27-0.92, y 0.21-0.58, ONE `ApplyRowSurface` frame. Portrait
  (existing `Portrait` primitive or the rail icon at 3x) x 0.29-0.37, y 0.23-0.33. Name band
  x 0.39-0.80 at FontTitle; "Level n" right-aligned x 0.80-0.90 in the same band.
- Fact line "Train one: 550 gold . 40s . Ready" - one `Label` + `FitSingleLine`, the whole
  affordability state as words (StateText from the VM row, unchanged).
- Two CTAs, each 0.26 wide x 0.09 tall (~694x108 device px), on ONE line, DIFFERENT words:
  "TRAIN 1 FOOTMAN" (Yellow/primary) and "UPGRADE TO L2" (Gray/secondary). No mode toggle
  exists any more; both verbs are always present when both rows exist in the VM. When the
  upgrade row is absent (max level) the second button reads "MAX LEVEL" and is
  non-interactable with the sentence "This troop is at its current maximum level." under it.
- Upgrade fact line under the CTAs: cost + StateText of the Upgrade row.
- TRAINING NOW band: x 0.06-0.92, y 0.60-0.70, read-only mirror of `_vm.QueueRows` for
  ChannelId.Train (medallion + name + existing `Bar` + "32s left" / "queued 2nd"), plus
  ONE button "OPEN QUEUE" (x 0.74-0.90) that calls the existing `ToggleQueueDrawer`. Finish
  Now / Ad / Cancel stay in the drawer (pinned there, see E). Empty state: "Nothing training.
  Tap TRAIN to start."
- Armies row and CLOSE as today.

Tap flow:
- Train one: tap rail entry (or accept the default = first unlocked troop, as today) ->
  tap "TRAIN 1 FOOTMAN" -> VM `TrainTroop(id)` -> `BarracksService.EnqueueTraining` ->
  `BuildTimerService` Train line (one mechanism, unchanged) -> `QueueChanged` re-renders:
  the TRAINING NOW band shows the new medallion with its bar, the "Training" strip chip
  reads "1/2 . 1/5 queued", the fact line updates gold. If unaffordable the CTA is Gray and
  the fact line already says "Short 120 gold" before the tap; tapping still routes the VM's
  broke-case notice (existing FlushNotice -> crystal store), never a silent no-op.
- See info: nothing to tap. Name, level, description, cost, time and state are always on the
  card. (Tier/stat detail beyond ShortDescription is out of scope; the VM does not carry it.)
- Upgrade: tap "UPGRADE TO L2" -> VM `UpgradeTroop(id)` -> same queue -> TRAINING NOW band
  shows "Footman -> L2" with its bar. When queued, the button reads "UPGRADING..." and is
  non-interactable (sentence under it: "Queued - 2nd in line").
- Queue: tap the Training chip or "OPEN QUEUE" -> existing drawer with Finish Now / Ad /
  Cancel / Move up.

Empty and locked states:
- Troops tab locked (no barracks): unchanged - the launcher card is locked with the padlock
  and "Build a Barracks to unlock Troops." toast (pinned).
- No troop definitions: card reads "No troop definitions are available." (existing string).
- Locked troop selected: card shows portrait dim + padlock, name, "Requires Barracks Tier 2"
  as the fact line, and the CTA area shows ONE Gray non-interactable button "LOCKED - TIER 2"
  with no second button. Selecting a locked troop is allowed (so the requirement is
  readable), matching the launcher's "locked stays tappable, refusal is explicit" rule.
- Queue full (5/5): CTA Gray, fact line "Training line full - 5/5 queued", tap routes the
  VM's existing refusal notice.

---

## D. Where I diverge from Clash of Clans, and why

Copied from CoC: a persistent "training now" strip with unit portraits and a progress bar
visible on the same screen as the train buttons; one tap = one unit; locked units shown in
place, dimmed, with their unlock requirement; the selected unit's info always readable.

Deliberately NOT copied:
1. CoC's horizontal card grid with tap-to-train ON THE CARD and long-press for info. On a
   phone a long-press is undiscoverable and a first-time player taps a card expecting to
   SELECT it, not to spend. Here the rail entry selects; the spend is a labelled verb button.
   The cost of one extra tap is worth never spending gold by accident.
2. CoC's tap-spam (tap 20 times, 20 troops). Our queue is a 5-deep `BuildTimerService` line,
   not a camp with capacity - repeated taps would hit "line full" on the 6th and the
   mechanic is one-job-per-tap by design. So the button says "TRAIN 1 FOOTMAN" and the queue
   depth sentence is on the chip; no counter badges on cards.
3. CoC's red/green affordability tints and its "-" dequeue badge on the card. Affordability
   is a sentence (owner colourblind, canon rule), and cancel stays in the drawer where the
   refund sentence lives (pinned).
4. CoC splits Train (Barracks) from Upgrade (Laboratory) into different buildings. The
   regression pin and the closed barracks talk-door make Manage > Troops the ONE door for
   both, so both verbs sit on one card, with different words so they never read as one.

---

## E. Implementation lane and the pins it must keep

Files touched (View only - the VM already emits everything the wireframe reads):
- `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs`: replace `AddTroopSplitWorkspace`,
  `BuildTroopRailChoice`, `BuildTroopWorkspacePager`, `BuildTroopWorkspaceModes`,
  `AddTroopPagerRow` (lines ~1527-1720) with the rail + card + training band; delete
  `_troopMode` (line 156) and `_troopChoicePage`. Keep `RenderTroopsDestination` (1421) as
  the entry and keep the `AddActionNoteRow("Saved army compositions", "Open armies", ...)`
  call (1449). Keep the object-name exclusions `TroopChoice_` in `ApplyOperationalMedievalSkin`
  (1097); `TroopMode_` may go with the toggle.
- `ManageScreenVM.cs`: NO change required. Optional: add `TrainSeconds`/`UpgradeCostText` to
  `TroopChoiceVM` if the fact line wants the time; otherwise read it from the BrowseRow.
- No `BuildTimerService`, `BarracksService`, or scene changes.

Pins to keep (literal strings asserted, from `Assets/Editor/Regression`):
1. `ManageTroopsTrainDoorRegression.cs` (VM-level, runs the real service): a BrowseRow with
   `ActionText == "Train"` whose `Label.StartsWith("Train ")`; a row with `ActionText ==
   "Upgrade"` whose `Label.StartsWith("Upgrade ")`; no two rows with identical labels; a
   muster row with non-null `Activate`; invoking the Train row's `Activate` puts a job on
   `ChannelId.Train` with `Kind == JobKind.TrainTroop` and id prefixed
   `BarracksService.TrainPrefix`. The View redesign does not touch any of these.
2. `ManageQueueDrawerRegression.cs` (panel source strings): `"BuildQueueDrawer(well)"`,
   `"_queueDrawer.SetActive(false)"`, `"float fixedNoRail = stripCost + noticeCost"`,
   `"ManageHeaderActions"`, `"TabsBandPx = 0f"`, `"\"QUEUE\""`; `RenderList` body must NOT
   contain `AddQueueRow` or `AddSectionHeader("IN QUEUE - "`; `RenderQueueDrawer` MUST
   contain `AddQueueRow(_vm.QueueRows[i])`; pairs `"Finish Now"` / `FinishNow(channel,
   jobId)` and `"Ad"` / `WatchAd(channel, jobId)` must both exist. => The TRAINING NOW band
   must be built by a NEW method (not `AddQueueRow`) called from `RenderTroopsDestination`,
   not from `RenderList`, and it must carry no Finish/Ad/Cancel verbs.
3. `ManageApprovedLauncherRegression.cs` + `ManageProgressiveDisclosureRegression.cs`
   (panel source strings): `"Build a Barracks to unlock Troops."`, `"Build a Barracks to
   unlock"`, `"BarracksUnlock.IsUnlocked"`, `"BuildLockBadge"`,
   `"UI/ElarionMedieval/badges/lock-badge"`, `"cards/troops-locked"`,
   `"ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"`,
   `"ActivateLauncherCard"`, `"MedievalUiSkin.ApplyShell(chrome)"`,
   `"ApplyOperationalMedievalSkin()"`, `"MedievalUiSkin.ApplyButton(button, primary)"`,
   `string.Equals(objectName, "Scrim"` and `string.Equals(objectName, "CloseButton"`,
   `"card.transition = Selectable.Transition.ColorTint"`, `"Build defense"`, `"UPGRADABLE
   TOWERS"`, `"Showing " + (first + 1)`, `"Previous page"`, `"Next page"`, `"Need another
   town structure?"`, `"Open build", OpenTownBuilder`. None are inside the Troops workspace
   code, but `ApplyOperationalMedievalSkin` keys "primary" off copy containing "TRAIN" /
   "UPGRADE" (line 1104-1107) - the new CTA labels keep those tokens so the skin still
   promotes them, and the Gray secondary must be named so the bulk pass does not repaint it
   primary (exclude it by object name as `TroopMode_` is today).

Also required before it reaches the owner: a fresh `RunCaptureHeadless` PNG of the Troops
tab at 2670x1200 with two, four and one troops, and one with a locked troop selected.

---

## F. Open questions only the owner can answer (one word each)

1. Should tapping TRAIN queue ONE unit per tap (yes) or open a count picker (no)?  [yes/no]
2. Keep BACK (to the four category cards) as a separate button from CLOSE?  [keep/merge]
3. Should the rail show troop NAMES beside the medallions (names) or icons only (icons)?  [names/icons]
