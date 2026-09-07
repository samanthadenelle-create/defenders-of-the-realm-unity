# WO-1382: Manage - Troops screen - the troop detail card floats over the roster; redesign the screen

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:49:53, build 2026.09.07.358574). PRIOR STATUS: FIXED - landed in 65d5a7eae (2026-09-04 23:5x; `_troopMode` deleted, TroopWorkspacePx 260, TRAINING NOW band; pinned by ManageTroopsTrainDoorRegression case 6 + ManageQueueDrawerRegression [drawer-clear-of-card]), on the Seeker as build 355952 and proven there on the headed walk (`docs/qa/UI_REVIEW_2026-09-05/INDEX.md` rows 07-09: Train CTA -> enqueued 45s -> TRAINING NOW rows=1); also in every Firebase build since. Awaiting owner felt-test. (The status flip was missed in the landing commit - corrected 2026-09-05 07:30 by the dispatcher pass.) Owner picked the independent review's wireframe 2026-09-04 22:45 (pasted back verbatim, section below); View-only lane on ManageScreenPanel.cs once the RCA section lands

**Owner (2026-09-04 22:34, felt-test on the Seeker, build 2026.09.05.355872), verbatim:**
> "something is wrong with manage troops screen i think its the box around train, can you have UI
> redesign this screen more intuitively?"

**Evidence (captured, not inferred):** `docs/qa/seeker-manage-troops-2026-09-04.png` (2670x1200,
adb screencap, 22:34). Surface: `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` (the only
file under `_Modules` that authors "OPEN ARMIES" / "UPGRADE OPTIONS" / "Saved army compositions").

## What the screenshot shows (each item is visible in the PNG)

1. A **troop detail card** ("Available / Front-line melee bruiser. Day-one workhorse. / TRAIN /
   UPGRADE OPTIONS") is drawn as a framed box **on top of** the roster list: its frame cuts through
   the troop portrait column on the left, and it covers the rows above and below the selected unit.
2. **Two TRAIN buttons for the same unit** - one inside the floating card, one on the "Train Footman
   / 550 gold / Ready" row beneath it. Which one is the door is not readable.
3. The **row's own label and price ("Train Footman", "550 gold") sit half under the card's bottom
   edge** - the card's y-extent is not reserved in the list layout; it is an overlay.
4. **Scroll arrows top-right are clipped** by the card's frame.
5. The lane chips (Builders 0/2 / Training 0/2 / Research 0/2) and the "Saved army compositions /
   OPEN ARMIES" row are fine and stay.

Read: the detail card was added as an overlay onto a list that never reserved room for it, so
every state where a unit is selected collides. This is a layout-ownership defect, not a styling nit.

## Design direction (tie-breaker: what would Clash of Clans do - owner's standing default)

CoC's Train Troops screen: a **grid of troop cards** (portrait, name, cost) with the **training
queue bar across the top**; tapping a card **trains one** immediately (the card IS the button);
tapping the **info "i"** opens a separate **modal** with the description / level / upgrade. There
is never an inline expandable box inside the grid. Proposed for us, for the UI seat to mock:

- Roster = one card per unit: portrait, name, gold cost, Ready/Locked state, ONE action face
  ("TRAIN"), consistent card height; the list reserves its own rows, nothing floats.
- Description + "Upgrade options" move to an **info modal** (tap the portrait or an "i" glyph) -
  the existing `ElarionUiKit.BuildObsidianModal` family, one modal on screen at a time.
- The Training 0/2 chip stays as the queue glance; queue rows remain in the Queue drawer.
- Locked units keep the lock glyph and say WHY (barracks level), in words, greyscale-safe.
- Touch targets >= MinTouchPx; ASCII-only strings; no meaning by colour alone.

## Acceptance (for the implementation lane, after the mock is approved)

- [ ] No element of the Troops tab overlaps another at any scroll position, any selected unit
      (headless UI capture at 2670x1200 + the owner's device).
- [ ] Exactly ONE "TRAIN" face per unit; tapping it enqueues through `BuildTimerService` (the one
      mechanism) and the Training chip increments.
- [ ] Description / upgrade live in a modal; `ModalArbiter` registered; dismiss returns to the same
      scroll position.
- [ ] `ManageTroopsTrainDoorRegression` + `UiObsidianConformanceRegression` + the UI capture pass
      stay green; add a capture case for "unit selected" if none exists.
- [ ] Owner felt-test on the Seeker.

## Not in scope
Troop balance, costs, the hire-mercenaries verb (WO-1372, shipped tonight), the Armies drawer.

## Hand-off
- UI seat: mock the grid + modal against the screenshot; mint nothing (this WO is the number).
- CLI: attach file:line RCA of the current layout (in flight, read-only agent), then implement on
  the owner's pick.

## RCA (read-only, 2026-09-04)

Read this session: the Seeker screencap, `Builds/ui-capture/ManageTroops_2670x1200.png` (mtime
2026-09-01 04:47), `ManageScreenPanel.cs` (HEAD `1ef5f6ad4`, 2026-09-04 17:34), `ManageScreenVM.cs`,
`ElarionUiKit.cs`, `ElarionUiKitObsidian.cs`, `MedievalUiSkin.cs`, `BarracksService.cs`, the sprite
`Assets/Resources/UI/ElarionMedieval/frames/content-panel.png` (+ .meta), `UICaptureLaunch.cs`, and
the five suites named below. `TroopTrainingPanel.cs` is NOT in the path - the Troops tab never
delegates to it (`RenderTroopsDestination` builds everything itself).

**The one-line finding:** there is no floating card and no overlay in the hierarchy. The whole
Troops tab is ONE 420 px list row (`TroopSplitWorkspace`) with every child anchored to its full
rect. The "card" the owner sees is that row's own background sprite, whose 9-slice draws its gold
border roughly 100 px INSIDE the row's top and bottom edges (the PNG has transparent margins), so
the title/requirement/rail/action-row children sit outside the visible frame and the frame appears
to float over the middle of them.

### 1. What builds the troop LIST rows ("Train Footman / 550 gold / Ready / TRAIN")

- Entry: `ManageScreenPanel.RenderList` -> `if (_vm.Tab == ManageTab.Troops) { RenderTroopsDestination(channel); MakeRowHost("ListTailSpacer", 28) }`
  at `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:1344-1349`.
- `RenderTroopsDestination` (`:1421-1452`): picks `selected` (persisted `_selectedTroopId`, else the
  first `Unlocked` choice, else `[0]` - `:1431-1441`), then calls `AddTroopSplitWorkspace(selected)`
  (`:1443`) and appends ONE `AddActionNoteRow("Saved army compositions", "Open armies", ...)`
  (`:1445-1451`). **There is no per-unit list.** The scroll content for this tab is exactly three
  rows: `TroopSplitWorkspace` (420 px), `ActionNote` (`RowHeightPx` = 132, `:72`), `ListTailSpacer`
  (28). Row factory = `MakeRowHost` (`:1798-1816`): `LayoutElement.preferredHeight/minHeight` +
  `sizeDelta.y` = heightPx, parented to `_listContent` (the kit scroll column,
  `ElarionUiKit.MakeScrollZone`, `ElarionUiKitObsidian.cs:3300-3340`: VerticalLayoutGroup
  `childControlHeight=false`, spacing 8, padding 10, RectMask2D viewport, ContentSizeFitter).
- The "Train Footman / 550 gold / Ready / TRAIN" row is NOT a list row. It is a zone INSIDE the
  workspace: `MakeZone(workspace, "TroopSelectedAction", (0.225, 0.00), (0.985, 0.34))` at
  `:1608-1609`, filled by `BuildBrowseRowContent(actionHost, action)` (`:1610`) - the same builder
  the Defense/Buildings tabs use for their 132 px browse rows (`:2150-2177`): name label
  y 0.52-0.98 x 0.02-0.50, cost y 0.04-0.48, state x 0.52-0.73, CTA at
  `PrimaryX0..PrimaryX1` = 0.76-0.98 x `RowCtrlY0..Y1` = 0.06-0.94 (`:80-84, :117-118`). The
  BrowseRowVM it renders is found by `SubjectId == selected.Id && ActionText == ("Train"|"Upgrade")`
  depending on `_troopMode` (`:1588-1597`).
- Unit portraits (the medallion column): `MakeZone(workspace, "TroopSelectorRail", (0.01,0.03), (0.18,0.97))`
  `:1539`, 3 per page (`pageSize = 3`, `:1529`), each `BuildTroopRailChoice` (`:1617-1667`) - a
  transparent Button + bezel + icon, tap = `_selectedTroopId = id; Render()` (`:1629`).
- **Scroll arrows** = the page pagers, built only when `pageCount > 1`
  (`:1570-1576`): `BuildTroopWorkspacePager` (`:1669-1682`) at workspace anchors
  `<` (0.78,0.72)-(0.875,0.99) and `>` (0.885,0.72)-(0.98,0.99). They page the 3-slot rail, not the
  list. They are children of the workspace row's TOP band.
- Dead code in the same file, kept for the record: `AddTroopSelectorRow` (`:1454`),
  `AddTroopPagerRow` (`:1702`), `AddTroopDetailRow` (`:1732`), `AddTroopModeRow` (`:1751`) are the
  earlier stacked-rows layout and are never called (grep: only their definitions).

### 2. What builds the "floating" DETAIL card ("Available / description / TRAIN / UPGRADE OPTIONS")

- Parent: the workspace row itself - `var workspace = MakeRowHost("TroopSplitWorkspace", 420f)`
  (`:1528-1536`, `workspaceHeight = 420f` at `:1529`), then **`ApplyRowSurface(workspace)` at
  `:1537`**. `ApplyRowSurface` (`:2179-2189`) adds an `Image` with sprite
  `UI/ElarionMedieval/frames/content-panel`, `type = Sliced`, `fillCenter = true`, colour
  (0.92, 0.88, 0.76, 0.96).
- The children and their literal workspace anchors (all `ElarionUiKit.Label` = anchors + zero
  offsets, `ElarionUiKit.cs:1888-1907`):
  - title `NAME - LEVEL n`: y 0.82-0.98, x 0.235-0.72 (`:1554-1558`)
  - requirement "Available": y 0.72-0.81, x 0.235-0.76 (`:1564-1568`)
  - description: y 0.61-0.71, x 0.235-0.76 (`:1559-1563`)
  - `TRAIN` mode tab: (0.29,0.33)-(0.57,0.61); `UPGRADE OPTIONS`: (0.59,0.33)-(0.93,0.61)
    (`BuildTroopWorkspaceModes`, `:1684-1700`)
  - locked copy (when `!selected.Unlocked`): y 0.18-0.62, x 0.28-0.96 (`:1580-1585`)
  - `TroopSelectedAction` zone: (0.225,0.00)-(0.985,0.34) (`:1608-1609`)
  - rail (0.01,0.03)-(0.18,0.97); pagers y 0.72-0.99 (see 1).
- **Reserved or overlay - proven: RESERVED.** The list DOES reserve 420 px for the workspace
  (`MakeRowHost` sets both `LayoutElement` and `sizeDelta`, and the column has
  `childControlHeight=false`, so the row keeps its authored height). Nothing is drawn outside a
  row's band. What the owner sees as a card is the workspace's own `content-panel` surface drawn
  SMALLER than its rect:
  - The sprite is 1672x941 px, `spriteBorder {96,96,96,96}`, PPU 100 (`content-panel.png.meta:51-52`).
    Opening the PNG: the gold frame art occupies roughly y 90-800 and x 20-1650 of the 941-high
    image - i.e. ~90 px of transparent margin above the frame and ~140 px below, ~20 px at the
    sides. With 9-slice borders of 96, the top corner/edge tiles carry that transparent margin
    at native scale and the bottom gold line lands in the STRETCHED centre slice, so the visible
    frame sits ~100 px inside the rect's top and bottom edges while hugging its left/right edges.
  - Measured on `Builds/ui-capture/ManageTroops_2670x1200.png` (2000-px-wide view): the workspace
    children span y 243 (title top) to 637 (cost bottom) = the 420 px row; the gold frame spans
    y 345-525 and x 230-1760 - top inset ~110, bottom inset ~118, side inset ~15-25. The
    requirement "Available" (0.72-0.81) straddles the frame's top line, the mode tabs and
    description are inside it, the `TroopSelectedAction` band (0.00-0.34) is entirely BELOW it,
    and the rail medallions (x 0.01-0.18) sit ON its left bracket. That is defects 1, 3 and the
    "portrait column cut through" exactly, with no overlay anywhere.
  - Same mechanism, checked: on the device shot (list scrolled ~60 px, title above the mask) the
    frame is at y 265-465 with the same insets. The other tabs' 132 px browse rows also get
    `ApplyRowSurface` (`:2153`) but at 132 px the 96+96 borders exceed the row, Unity scales the
    slices down and the frame is effectively invisible - which is why only the 420 px Troops row
    shows a box.
  - Defect 4 (arrows clipped) is NOT the frame: the pagers live at workspace y 0.72-0.99 and the
    device screenshot has the list scrolled so that band crosses the viewport's `RectMask2D`
    (`ElarionUiKitObsidian.cs:3311`). In the headless capture (scroll 0) both arrows are whole.
    Whether the device scroll is the owner's finger or the ScrollRect's rest position after
    `Render()` rebuilds the column is NOT proven here (no device `[Flow:Manage] bands(px)` line was
    read; the `:487` `bands(px)` trace would give LIST px for the Seeker well).

### 3. Why there are TWO "TRAIN" faces for one unit

- Face A (inside the "card"): `BuildTroopWorkspaceModes` `:1686-1689` -
  `ElarionUiKit.BuildObsidianButton(parent, "TRAIN", ..., () => { _troopMode = 0; Render(); })`,
  named `TroopMode_Train` (`:1694`). It is a MODE TAB (Train vs Upgrade view), yellow when
  `_troopMode == 0`. **It never enqueues anything.**
- Face B (on the "Train Footman" row): `BuildBrowseRowContent` `:2172-2176` -
  `BuildObsidianButton(row, r.ActionText /* "Train" */, ..., () => r.Activate?.Invoke())` where the
  row is the VM's `AddGoldBrowseRow("Train " + name, default, def.CostGold, "Train", () => TrainTroop(id))`
  (`ManageScreenVM.cs:1001`). `TrainTroop` (`ManageScreenVM.cs:1423-1451`) calls
  `BarracksService.EnqueueTraining(troopId, 1, out stopReason)` (`:1427`), which is the only
  path that reaches the queue: `BarracksService.cs:367` `queue.Enqueue(JobKind.TrainTroop, jobId, def.BuildSeconds, 0, ToJobCost(rawCost))`
  on `ChannelId.Train`, jobId `TrainPrefix + troopId + ":" + guid8` (`:362`). Completion lands the
  unit via `TrainTroopEffect` (`:495-502`). `MedievalUiSkin.ApplyButton` then upper-cases both
  labels (`MedievalUiSkin.cs:86`), so "Train" and "TRAIN" read identically.
- So: face A is a view toggle mis-labelled with the verb; face B is the door. ONE enqueue site
  (`ManageScreenVM.cs:1427` -> `BarracksService.cs:367`); ManageScreenVM never calls
  `BuildTimerService.Instance.Enqueue` directly (pinned, see 5).

### 4. Does a headless capture exist for this tab with a unit selected?

- YES, and it already shows the defect: `UICaptureLaunch.RunManageOperationalCaptureHeadless`
  (`Assets/Editor/UICaptureLaunch.cs:6896-6918`) calls `CaptureManageOperational(ManageTab.Troops)`
  (`:6908`, body `:7016-7080`): a fixture town (`barracks` tier 3, `BarracksLevel = 3`, 100k of
  everything), real `BuildTimerService` seeded with jobs, `panel.Open()` + `ShowOperational(tab)`,
  written to `Builds/ui-capture/ManageTroops_{1920x1080,2340x1080,2670x1200}.png`, marker
  `MANAGE_OPERATIONAL_CAPTURE_OK n/12`. `RunManageLiveQueueCaptureHeadless` (`:6926-6944`) also
  captures Troops. A unit IS selected in both - `RenderTroopsDestination` auto-selects the first
  unlocked troop (`ManageScreenPanel.cs:1436-1440`), so the frames show Footman selected with
  `_troopMode = 0`.
- Why it shipped unseen anyway:
  - The oracle attached to the capture (`ReportGeometry` / `ReportTouchOracle` = `LayoutOracle`
    rules: control >= `MinTouchPx` 112, control-vs-control and control-vs-text overlap) checks
    CONTROLS and TEXT, not whether a row's background `Image` encloses its children. A decorative
    sprite drawn short of its rect is invisible to it, so `touch=clean` was truthful and useless.
  - The three PNGs on disk are dated 2026-09-01 04:47; the last edit to the panel is
    `1ef5f6ad4` 2026-09-04 17:34 (and `ApplyRowSurface(workspace)` entered in `486cd7b17`,
    2026-09-01 15:43). No capture on disk postdates HEAD, and no chain script under `tools/` or
    `.claude/` invokes `RunManageOperationalCaptureHeadless` (grep: the only reference is
    `UICaptureLaunch.cs` itself) - it is a hand-run gate, so "open the PNGs" (memory
    `headless-screenshot-verify-ui-before-build`) was the only detector and it was not opened.
  - NOT covered by any case: a NON-default selection (rail tap), a LOCKED selection (the
    `:1580-1585` copy), `_troopMode = 1` (UPGRADE OPTIONS view), and page 2 of the rail
    (`pageCount > 1` only when > 3 troop defs exist in the fixture).

### 5. Regression suites that touch this tab, and the pins an implementation lane must keep

- `ManageTroopsTrainDoorRegression` (`Assets/Editor/Regression/ManageTroopsTrainDoorRegression.cs`,
  marker `MANAGE_TRAIN_DOOR_OK`). Drives the REAL `ManageScreenVM` (`vm.SelectTab(ManageTab.Troops); vm.Rebuild()`,
  `:136-138`) - VM-level, so a panel-only redesign cannot break it, but the VM row grammar is
  pinned:
  - case 1: at least one `BrowseRows` entry with `ActionText == "Train"` (`:142-143`).
  - case 2: at least one with `ActionText == "Upgrade"` (`:157-158`); every Train row's `Label`
    starts with `"Train "` and every Upgrade row's with `"Upgrade "` (`:163-169`); no two rows share
    a `Label` (`:174-178`).
  - case 3: a row whose `Label` contains `"Armies"` with non-null `Activate` (`:184-191`).
  - case 4: invoking the first Train row's `Activate` puts a job on `ChannelId.Train` whose
    `StructureId` starts with `BarracksService.TrainPrefix`, `Kind == JobKind.TrainTroop`,
    `Channel == ChannelId.Train` (`:196-228`).
  - case 5 (source): `ManageScreenVM.cs` must contain `BarracksService.EnqueueTraining` and
    `TroopDialogueCommands.ShowMusterUI`, and must NOT contain `BuildTimerService.Instance.Enqueue`
    (`CheckSingleDoorSource`, `:242-260`); `BuildingInteractable.cs` keeps `"barracks"` in `_noTalkDoor` (`:263-265`).
  - Consequence for the mock: "the card IS the button" is fine, but the VM must keep emitting
    verb-led `Train <name>` / `Upgrade <name>` rows; the panel may hide the label.
- `ManageProgressiveDisclosureRegression` (`.../ManageProgressiveDisclosureRegression.cs`), source
  pins on `ManageScreenPanel.cs` (`:19-56`): literal
  `"ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"`,
  `"BarracksUnlock.IsUnlocked"`, `"Build a Barracks to unlock"`, `"ActivateLauncherCard"`,
  `"_vm.Rebuild();"` appearing BEFORE `"RenderLauncherCards();"`, `"UPGRADABLE TOWERS"`,
  `"BuildQueueDrawer(well)"`, `"private void RenderList("` present and its body free of
  `AddSectionHeader("IN QUEUE - "`, `"Showing \" + (first + 1)"`, `"Previous page"`, `"Next page"`,
  `"Need another town structure?"`, `"\"Open build\", OpenTownBuilder"`,
  `"EnterBuildMode(DeNelle.Core.Catalog.BuildType.Town)"`; VM must contain `CountPlacedThisTown()`
  and `BuildVisibleTabs()`. None of these are in the Troops code, but a lane that moves
  `RenderList` or renames it breaks the scoped ban.
- `ManageApprovedLauncherRegression` (`.../ManageApprovedLauncherRegression.cs:16-46`, not in the
  task's list but it greps this file): `"Choose a path"`, `"Towers, walls & gates"`,
  `"Town structures & upgrades"`, `"Build a Barracks to unlock"`, `"Discover realm advancements"`,
  the four-tab literal above, NOT `"QueueBadgePlate_"` / `"0/5 queued\";"`,
  `"MedievalUiSkin.ApplyShell(chrome)"`, `"BarracksUnlock.IsUnlocked"`, **`"BuildLockBadge"`**,
  **`"UI/ElarionMedieval/badges/lock-badge"`**, `"cards/defense"`, `"cards/buildings"`,
  `"cards/troops-locked"`, `"cards/research"`, `"Build a Barracks to unlock Troops."`,
  `"_categoryNavigationCommitted"`, `"if (_categoryNavigationCommitted) return"`,
  `"card.transition = Selectable.Transition.ColorTint"`, `"ApplyOperationalMedievalSkin()"`,
  `"MedievalUiSkin.ApplyButton(button, primary)"`, `"string.Equals(objectName, \"Scrim\""`,
  `"string.Equals(objectName, \"CloseButton\""`, `"\"Build defense\""`. The lock-badge pair is the
  one the Troops rework can trip: keep `BuildLockBadge` in use for locked units.
- `UiObsidianConformanceRegression` (`.../UiObsidianConformanceRegression.cs`): source-lints
  `Assets/_Modules/**` for raw `new GameObject(..., typeof(Image|RawImage|Text|TextMeshProUGUI|TMP_Text))`
  / `AddComponent<Image|...>` (`:82-98`), `HardFailOnNew = true` (`:74`). A file is exempt when it
  contains the token `"ElarionUiKit"` (`:252` `routesThroughKit`) or is in `AllowList`/`KnownBaseline`.
  `ManageScreenPanel.cs` has 14 raw-Image constructions today and passes ONLY because it references
  `ElarionUiKit`. Pin to keep: any NEW panel/modal file the lane adds must build through
  `ElarionUiKit` (a fresh file with a raw `typeof(Image)` and no kit reference FAILS the gate).
- `UiTouchClampRegression` (`.../UiTouchClampRegression.cs`): does not touch this panel - it feeds
  `DeNelle.Core.UI.LayoutOracle` synthetic canvases at two aspects and asserts the oracle fires on
  RED-A (control < `MinTouchPx` = 112 ref px), RED-B / RED-B2 (two controls overlapping, same or
  different parents) with messages naming both widget paths and the overlap in px. The pins that
  bind the lane are the RULES, applied to the real panel by `UICaptureLaunch.AuditGeometry` during
  the capture: every Button >= 112 ref px on both axes at 1920x1080 / 2340x1080 / 2670x1200 with
  `ClampMinTouch` never having to grow it (`ElarionUiKit.cs:1052-1060`, growth ring = a WO-1060
  Assert A failure), and no two controls, and no control-over-text, sharing pixels.
- Also grepping the Troops names: `ManageDefenseUpgradeDoorRegression.cs` mentions Troop only in
  prose; `ManageQueueDrawerRegression` pins the drawer, not this tab. No suite references
  `TroopSplitWorkspace`, `TroopMode_*`, `TroopSelectedAction` or `TroopChoice_*` outside
  `ManageScreenPanel.cs` (the `:1097-1098` skip list in `ApplyOperationalMedievalSkin` is the only
  other consumer of those names - keep the prefixes or update that skip list).

### 6. Smallest change that removes the OVERLAP without the redesign (stopgap - NOT made)

- `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:1537` - delete (or comment out) the
  single call `ApplyRowSurface(workspace);`. That removes the only Image whose art is drawn short
  of its rect; every child keeps its authored band, the rail medallions stop sitting on a bracket,
  "Available" and "Train Footman" stop straddling a gold line. One line, no anchor changes, no VM
  change, no suite pin touched (`ApplyRowSurface` stays defined and used at `:1457/:1735/:1866/:1881/:2153`).
  Visual cost: the Troops row loses its (already misleading) frame and reads like the other tabs'
  rows, which also render frameless in practice at 132 px.
- Companion one-liner for defect 2 if the owner wants it in the same stopgap: `:1686` change the
  mode tab copy from `"TRAIN"` to a noun that is not the verb (e.g. `"TRAINING"`, ASCII, fits the
  0.28-wide band at `FitSingleLine` 30-44 px) so only the priced CTA on the row says TRAIN. Does
  not alter enqueue paths or any pinned string (no suite asserts `"TRAIN"` on the panel).
- NOT the stopgap: re-authoring the sprite's 9-slice borders in `content-panel.png.meta` - that
  changes every other `ApplyRowSurface` consumer and the launcher shell in the same commit.

### Files an implementation lane would touch

- `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` - `RenderTroopsDestination` (`:1421`),
  `AddTroopSplitWorkspace` (`:1527`), `BuildTroopRailChoice` (`:1617`), `BuildTroopWorkspacePager`
  (`:1669`), `BuildTroopWorkspaceModes` (`:1684`), the `TroopSelectedAction` zone (`:1608`); delete
  the dead `AddTroopSelectorRow` / `AddTroopPagerRow` / `AddTroopDetailRow` / `AddTroopModeRow`
  (`:1454-1770`); keep `BuildLockBadge` and the `:1097-1098` skin skip-list in step.
- `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` - only if the grid needs a per-unit VM
  richer than `TroopChoiceVM` (`:214`) + the `Train`/`Upgrade` `BrowseRowVM` pair (`:1001`, `:866`);
  `TrainTroop` (`:1423`) and `UpgradeTroop` (`:1455`) stay the doors.
- The info modal: `ElarionUiKit.BuildObsidianModal` family in
  `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs` + `PanelManager`/`ModalArbiter` registration
  (`Assets/_Modules/Core/UI/`), one modal at a time.
- `Assets/Editor/UICaptureLaunch.cs` - extend `CaptureManageOperational(ManageTab.Troops)`
  (`:7016`) or add a sibling case that sets `_selectedTroopId` (private, via `GetPrivateFieldValue`
  idiom) to a non-default and a locked unit, and one with the info modal open; the geometry
  oracle cannot see a background sprite, so the PNGs must be opened.
- Suites to keep green: `ManageTroopsTrainDoorRegression`, `ManageProgressiveDisclosureRegression`,
  `ManageApprovedLauncherRegression`, `UiObsidianConformanceRegression`; add a pin for "exactly one
  control labelled TRAIN per unit on the Troops canvas" if the owner keeps the mode tab.
- Not touched: `TroopTrainingPanel.cs` / `TroopTrainingVM.cs` (not on this path),
  `BarracksService.cs`, `BuildTimerService.cs`, `content-panel.png(.meta)`.

## OWNER RULING 2026-09-04 22:45 - the layout (pasted back by the owner, verbatim)

Source: `docs/qa/INDEPENDENT_REVIEW_manage_troops_2026-09-04.md` section C (the independent review,
formed without reading this WO). The owner pasted this wireframe back as the pick. It supersedes the
"Design direction" section above where they differ (notably: NO separate info modal - the description
lives on the selected-troop card; UPGRADE is a second button on the same card; a TRAINING NOW band
mirrors the Train line).

```
BACK               MANAGE - TROOPS                 QUEUE
Builders 0/2       Training 1/2 . 1/5       Research 0/2
[ TROOPS rail ]   [ SELECTED TROOP card ]
 Footman L1  >     [portrait] FOOTMAN                LEVEL 1
 Archer  L1        Front-line melee bruiser. Day-one workhorse.
 Shield  lock T2   Train one: 550 gold . 40 sec . Ready
 (scroll)          [ TRAIN 1 FOOTMAN ] [ UPGRADE TO L2 ]
                   Upgrade: 300 wood . 120 iron . Ready
[ TRAINING NOW band ]  Footman ########.. 32s   Archer . queued 2nd   [ OPEN QUEUE ]
Saved army compositions                          [ OPEN ARMIES ]
                         [ CLOSE ]
```

Binding details from the review the lane must honour:
- ONE verb per button, different words: `TRAIN 1 FOOTMAN` (primary) / `UPGRADE TO L2` (secondary);
  the boxed TRAIN/UPGRADE mode TOGGLE is deleted (it was a no-op that looked like the door).
- Name band always on screen (the current one is clipped above the strip); pager arrows deleted; the
  rail is a scroll list, selected = gold outline AND a `>` chevron; locked = dim + padlock + tier word.
- Fact line is a sentence: `Train one: <cost> . <time> . <state>`; the tap's consequence shows in the
  TRAINING NOW band without opening the drawer. Finish Now / Ad / Cancel STAY in the drawer;
  `OPEN QUEUE` calls the existing `ToggleQueueDrawer`.
- The band is a NEW method called from `RenderTroopsDestination`, never `AddQueueRow` inside
  `RenderList` (ManageQueueDrawerRegression pin). VM unchanged where possible; MVVM strict.
- Touch targets >= MinTouchPx; ASCII-only; state by words/shape never hue.
- Three owner questions from the review remain OPEN until she answers (count picker yes/no; BACK
  keep/merge; rail names/icons) - implement the review's defaults (one unit per tap; BACK kept;
  names beside medallions) and flag them in the RESULT.

## OWNER RULINGS 2026-09-04 22:50 - FINAL, after reading the independent review (verbatim excerpts)

The owner's three answers to the review's open questions:
- Train ONE troop per tap: **YES** - "No count picker. At least for now."
- Keep BACK separate from CLOSE: **KEEP** - "BACK stays upper-left. CLOSE remains centered at the
  bottom and slightly quieter."
- Rail shows troop names: **NAMES** - "You have nine troop types now. Icons-only gets increasingly
  annoying because you're asking the player to memorize faces instead of read a name."

Visual target: the owner's rendered mockup (received 22:50 - medieval framing, portrait medallions,
gold rail outline + chevron, SELECTED TROOP card with banner art, TRAINING NOW band with numbered
rows and a progress bar). "Use its interaction model, then use the mockup's stronger medieval
framing, portrait treatment, spacing, and visual polish on top of it." The interaction model is the
review's section C; the framing is the mockup's.

Binding points, in the owner's words:
1. Status strip: "Training becomes tappable and opens the existing queue drawer" and shows depth
   (`Training 1/2 . 1/5 queued`).
2. Rail: "scroll vertically / have no pager arrows / show portrait / show troop name / show level /
   show Barracks tier when locked / use a gold outline plus a chevron for selection".
3. Centre card: "Delete this entirely: TRAIN | UPGRADE OPTIONS. That should not be a mode switch."
   "The review recommends removing _troopMode entirely rather than trying to make the segmented
   control prettier. I strongly agree." Two verbs, two labels: `TRAIN 1 FOOTMAN` / `UPGRADE TO L2`.
4. "Put affordability in plain English": `Ready` / `Short 120 gold` / `Training line full . 5/5 queued`
   directly above or beneath the button; disabled button + the sentence. Never colour-only.
5. TRAINING NOW band under the card: informational mirror of the Train line (portrait, name, bar,
   `32s left` / `Queued 2nd`), `[ OPEN QUEUE ]`; empty state "Nothing training. Tap TRAIN to start."
   "The screen visibly reacts" the moment TRAIN is tapped (gold, chip, row, timer).
6. "Keep advanced queue actions OUT of this screen" - Cancel / Finish Now / Watch Ad / Move Up stay
   behind OPEN QUEUE.
7. Saved armies stays ONE row: "Saved army compositions [ OPEN ARMIES ]".
8. Locked troop: selectable, dim portrait, "Requires Barracks Tier 2", one Gray non-interactable
   `[ LOCKED . TIER 2 ]`, no Train/Upgrade buttons. "Don't hide future content."
9. "There are only four obvious verbs on the entire screen": BACK, TRAIN 1 <NAME>, UPGRADE TO L<n>,
   OPEN QUEUE / OPEN ARMIES. "Nothing masquerades as a button whose actual job is to switch another
   button."

## Owner felt-test 2026-09-04 23:19 (build 355905, the OLD layout): "clicking train on footman doesnt start a training bar o seem to do anything"
Device trace (adb logcat, same minute): `[Flow:Manage] tab -> Troops (line Train)` ... `troops browse: 9 troop
def(s) -> 2 Train row(s)` ... `queue drawer BUILT 0 row(s)` - and NO `[Flow:Manage] -> Train CTA 'troop-footman'`
line, which `ManageScreenVM.TrainTroop` emits on every real tap. Her tap never reached the VM. Screenshot:
`docs/qa/seeker-manage-troops-train-noop-2026-09-04.png` (FOOTMAN - LEVEL 3; the boxed TRAIN is the mode toggle;
the priced row's TRAIN sits at the frame's lower edge). This is the defect this WO's rebuild removes: one
`TRAIN 1 FOOTMAN` face wired to the VM row's Activate, no toggle, no frame over the row. Acceptance adds: on
the device, a TRAIN tap logs `Train CTA` + `train enqueued from Manage` and the Training chip moves 0/2 -> 1/2.
