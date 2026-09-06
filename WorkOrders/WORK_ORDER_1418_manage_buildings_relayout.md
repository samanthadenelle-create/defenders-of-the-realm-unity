# WO-1418: Manage - Buildings re-layout: portrait list + one selected card + BUILDING NOW (Clash-of-Clans shape, less text)

**Status:** READY TO IMPLEMENT - minted 2026-09-05 (CLI, from the owner's evening ruling and her two mockups); dispatched to the Codex dev lane via `BATCH_STATE.md` PART 8
**Silo:** HUD / Manage (Village assembly, code-built uGUI)
**Owner ruling (2026-09-05, verbatim):** "i want tonight to be a focus on the UI layout that we have. I dont love it. Think of Clash of clans and warcraft, this is too much text on screen whereas they are simple and intuitive" / "manage is the big offender" / "we can reuse the building cards from build". Her two mockups (Manage - Buildings, Manage - Troops) are the APPROVED target shape (AskUserQuestion, this session).
**Absorbs:** WO-1405 (benefit line half only), WO-1406 (all), WO-1412 (all). See section 8.
**Base commit:** _filled at hand-off go - see BATCH_STATE PART 8. Do NOT start from today's dirty tree._

---

## 1. The defect, measured

Live Seeker screen (build 2026.09.05.356620, adb screencap 2026-09-05 evening, read by the CLI): Manage - Buildings is a
paged TEXT list.

```
MANAGE - BUILDINGS                                   [BACK]            [QUEUE]
[Builders 0/2] [Training 0/2 . 0/5 queued] [Research 0/2]
BUILDING UPGRADES - affordable first
Showing 1-4 of 6 - page 1 of 2
Armorer -> T1
Wood 1000, 670 gold            Short on resources            [UPGRADE]
Forge -> T1
Wood 1060, 680 gold            Short on resources            [UPGRADE]     <- row 2 clipped by CLOSE
                              [CLOSE]
```

No portrait, no picture of the building, no selected item, developer-shaped labels (`-> T1`), a paging sentence, a
"Short on resources" sentence on every row, and the list is cut at row two by the CLOSE bar.

## 2. The target (her mockup, Manage - Buildings), described

Landscape 2670x1200 (Seeker) and 1920x1080.

```
[BACK]                    MANAGE - BUILDINGS                              [QUEUE]
[ (hammer) Builders 1/2 . 1/4 queued ] [ (swords) Training 0/2 ] [ (book) Research 0/2 ]

+-- BUILDINGS --------+  +-- SELECTED BUILDING ----------------------------------------+
| (o) Town Hall     > |  |  (O)   LUMBER MILL                                            |
|     Level 3         |  |        LEVEL 2                                                |
| [(o) Lumber Mill  >]|  |        [Upgradable]                                           |
|     Level 2  <gold> |  |        Produces wood over time. Upgrade to improve output.    |
| (o) Quarry        > |  |        Upgrade: 1200 wood . 700 stone . 45m . Ready           |
|     Level 2         |  |        [ UPGRADE TO L3 ]        [ VIEW DETAILS ]              |
| (o) Arcane Spire  > |  |        After upgrade: +40 wood/hr . +250 capacity             |
|  (lock) Level 1 . T4|  +---------------------------------------------------------------+
+---------------------+  +-- BUILDING NOW ---------------------------------------------+
                         | (1) (o) Lumber Mill  [=========---]  18m left   [OPEN QUEUE] |
                         | (2) (o) Quarry       Queued 2nd                               |
                         +---------------------------------------------------------------+
| Recommended next upgrades                                            [OPEN BUILDINGS] |
                                   [CLOSE]
```

Left column ~27% width: scroll list of buildings, each row = round portrait medallion + name + "Level N" + chevron;
selected row carries a gold outline; a locked row is greyed with a lock badge and "Level 1 . T4" (the requirement).
Right top: the SELECTED BUILDING card. Right bottom: BUILDING NOW strip. Footer: one hint row + OPEN BUILDINGS; CLOSE.

## 3. Architecture ruling - read before you design anything

**The structure already exists in the same file. Copy it; do not invent a second one.** The Troops tab (WO-1382, in the
tree since `65d5a7eae`, on the owner's phone) IS this mockup, built:

| Piece | Troops implementation (copy this) | Buildings name (new) |
|---|---|---|
| rail + card row | `ManageScreenPanel.cs:1801 AddTroopWorkspaceRow` (rail x 0-0.26, card x 0.275-1.0, height `TroopWorkspacePx` = 260) | `AddBuildingWorkspaceRow` |
| rail row | `:1863 BuildTroopRailRow` (medallion + name + "Level n" + gold `Outline` on selected + `>` chevron + `BuildLockBadge`) | `BuildBuildingRailRow` |
| selected card | `:1934 BuildTroopCard` | `BuildBuildingCard` |
| "now" band | `:2098 AddTroopTrainingNowBand` (`MakeRowHost("TroopTrainingNowBand", TrainingNowBandPx)`) | `AddBuildingNowBand` (`MakeRowHost("BuildingNowBand", TrainingNowBandPx)`) |
| job row | `:2145 BuildTroopTrainingNowJob` - ordinal + `ElarionUiKit.Portrait` medallion + name + bar + `FormatTime(...) + " left"`; **channel-agnostic** (`r.Channel`, `r.JobId`) | **call it verbatim, zero copy** |
| tick | `_progressCells` (`:291-296`, `:2661-2700`) at 1 Hz via `ManageScreenVM.ProgressOfLive` (`VM:1784`) | reuse |

- **Reuse the Troop constants verbatim** - `TroopWorkspacePx`, `TroopCtaY0`, `TroopCtaY1`, `TrainingNowBandPx`,
  `TrainingNowRowPx`, `BandGapPx`, `SectionHeaderPx`, `RowHeightPx`, `StripBandPx`. Do not rename or add a parallel
  set: `ManageQueueDrawerRegression.cs:205` reads them by name via `Const(panel, "TroopWorkspacePx")` and `:230` pins the
  literal `DrawerModeListKeepPx = 10f + TroopWorkspacePx * (1f - TroopCtaY1)`.
- **Consequence, an intentional deviation from the mockup:** BUILDING NOW is a **full-width band under the workspace
  row** (that fold is proven at well = 533 px by the drawer suite), not tucked under the card only. Record it in the
  hand-back as a deviation, not a defect.
- **"Reuse the building cards from build"** = reuse the **material** of the WO-1417 palette card
  (`ElarionUiKit.ObsidianFill` plate + gold perimeter + ONE state WORD), promoted into the kit (lane A). The vertical
  PLACE-card composition is not the Manage shape.
- **Do not generalise `Troop*` and `Building*` into one parameterised builder in this WO.** That is Phase 2, its own
  WO, after the owner has felt-tested both tabs.

## 4. What the Buildings path DROPS (the "less text" half of the ruling)

Gone from the Buildings path: the heading `"BUILDING UPGRADES - affordable first"`, the `"Showing 1-4 of 6 - page 1 of 2"`
line, the `Previous page` / `Next page` pager, the `"Armorer -> T1"` label shape, the `"Short on resources"` sentence,
the `Wood 1000, 670 gold` word-costs. The card carries exactly: NAME, `LEVEL n`, one state WORD in a badge, one
description sentence, one cost line as icon chips, two CTAs, one "After upgrade:" line.

**The pins stay green with ZERO suite edits, by construction:**
- `ManageProgressiveDisclosureRegression.cs:51-55` checks the literals `"Showing " + (first + 1)`, `"Previous page"`,
  `"Next page"`, `"Need another town structure?"`, `"Open build", OpenTownBuilder` **file-wide**. Defense and Research
  keep the paged `RenderList` path, so every literal stays LIVE code, not dead.
- `ManageQueueDrawerRegression.cs:90` scopes the `AddQueueRow` ban to `Body(panel, "private void RenderList()",
  "private string FindSummary")`. Therefore **place `RenderBuildingsDestination` AFTER `FindSummary`** in the file, exactly
  as `RenderTroopsDestination` (`:1736`) sits outside that window.
- `RenderList:1695-1696` has `else if (_vm.Tab == ManageTab.Buildings) AddActionNoteRow("Need another town structure?",
  "Open build", OpenTownBuilder);` - that branch becomes unreachable. **Move that exact call** into
  `RenderBuildingsDestination` as its last row (it is the mockup's OPEN BUILDINGS footer) and delete the dead `else if`.
- `"BUILDING UPGRADES - affordable first"` (`:2204`, `BrowseHeading`) is pinned by nothing; leave the switch arm alone,
  it just stops being painted for Buildings. `"UPGRADABLE TOWERS - affordable first"` is Defense
  (`BuildCollectionPlayerRegression.cs:118`) - untouched.

## 5. Lanes (file-disjoint; A, B, D parallel; C authors in parallel and compiles after B)

### Lane A - kit promotion (Core assembly)
- `Assets/_Modules/Core/UI/CostFormat.cs`: `ElarionUiKit.CostRow(...)` (`:95`) and its private `AddCostText` hardcode
  `text.fontSize = 13`. Add an optional trailing parameter `float fontPx = 13f` threaded through, default unchanged so
  every existing caller renders identically. `CostRowFitRegression` is a runtime geometry suite (`BuildCostRow` at
  `:240`); it does not pin 13 - confirm by reading it and say so in the hand-back.
- New file `Assets/_Modules/Core/UI/ElarionUiKitGoldPerimeter.cs` (+ `.meta` with a fresh guid, grep it for
  uniqueness): `public static partial class ElarionUiKit { public static void GoldPerimeter(Transform host) { ... } }`
  copied from `BuildCollectionBrowser.cs:488 AddGoldPerimeter` (four `AddImage` edges at .008/.018/.982/.992,
  `ElarionUi.Gold` at .95 alpha). `ElarionUiKit` is already `public static partial` (`CostFormat.cs:93` proves it).
- **Do NOT migrate `BuildCollectionBrowser.cs`** to the kit method in this WO: `BuildCollectionPlayerRegression.cs:68`
  pins the literal `AddGoldPerimeter(card.transform)`. That migration rides its own later commit with the pin re-pointed.

### Lane B - the view-model (`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs`)
Add, mirroring `TroopChoiceVM` (`:221-263`):

```csharp
public sealed class BuildingChoiceVM
{
    public string Id;              // ladder id (the key BuildingTierCatalog / ModifierService use)
    public string Name;            // BuildingTierCatalog.Find(id).DisplayName  - NEVER "Armorer -> T1"
    public int Level;              // ModifierService.TierOf(id)
    public int MaxLevel;           // BuildingTierCatalog.MaxTier(id)
    public string IconKey;         // portrait key for the medallion (see below)
    public bool Locked;            // next tier exists and next.RequiresVillageTier > current VillageTier
    public string LockText;        // "Level 1 . T4"  (WO-1390 rule: the requirement, not a wall)
    public string StateWord;       // exactly one of: "Building" | "Locked" | "Max" | "Upgradable"
    public string Description;     // one sentence
    public IReadOnlyList<CostPart> UpgradeCostParts;  // CostFormat.Parts(...) of the NEXT tier basket; empty when Max
    public string UpgradeTimeText; // "45m" style via QueueRailView.FormatTime; NULL if the duration is not reachable
    public bool UpgradeReady;      // affordable && !Locked && !Building && !Max
    public string AfterUpgradeText;// next tier Effect  (this IS WO-1405's benefit line)
    public int NextTier;           // Level + 1, or 0 when Max
    public Action Activate;        // the existing UpgradeBuilding(id, next.Tier) closure (VM:913)
    public Action ViewDetails;     // wraps the private static OpenUpgradePanel(id) (VM:1541)
}
public readonly List<BuildingChoiceVM> BuildingChoices = new();
```

`BuildBuildingChoices()` is called beside `BuildBuildingsBrowse` (`:871`) from `Rebuild()`. Data, all existing:
- Iterate `CountPlacedThisTown()` (`:817`); `PlacedTally.SourceIds[0]` (`:777`) is the catalog id ->
  `CatalogRegistry.Get(...)` -> `StructureCardVM.DescriptionFor(entry)` (**public static**,
  `Assets/_Modules/Village/BuildMode/StructureCardVM.cs:238`; 18/28 catalog entries author a `description`, the rest get
  the typed fallback). If that is blank, fall back to the CURRENT tier's `BuildingTierDef.Effect`.
- `AfterUpgradeText` = `BuildingTierCatalog.TierOf(id, Level + 1).Effect` (`Assets/_Modules/Core/State/BuildingTierCatalog.cs:122`).
- Cost = `CostParts(BuildingTierBasket(next))` - the same `(wood, stone<-food, iron, crystals)` mapping the two build
  surfaces use (`BuildCollectionBrowser.cs:519`, `BuildPaletteUI.cs:1631`); gold if the tier basket carries it.
- Time: `BuildTimerService.cs:647-703` derives an upgrade's duration as
  `Config.DurationSecondsForTier(Mathf.Max(0, targetLevel - 2), BuildJobKind.Upgrade)`. Compose
  `UpgradeTimeText = QueueRailView.FormatTime(thatInt)`. **If `Config` is not reachable from the VM, leave
  `UpgradeTimeText` null and the View omits the term. Never hardcode "45m".**
- `StateWord`: `"Building"` if a live Builder job's id starts with `id + ":"` (the `QueueRowVM` set already carries
  channel + JobId), else `"Locked"`, else `"Max"` when `next == null && BuildingTierCatalog.IsUpgradable(id)`, else
  `"Upgradable"`. ASCII. This word is the ONLY carrier of state (owner is red/green colourblind; hue is decoration).
- **Behaviour delta, deliberate:** `BuildBuildingsBrowse` currently `continue`s maxed buildings (`:900`) so they vanish.
  The mockup lists every building; `BuildingChoices` INCLUDES maxed ones with `StateWord = "Max"` and no CTA.
- `IconKey`: the `QueueRowVM.IconKey`/`IconRole` route (`VM:600`) is what the queue rows use; for the rail and the card
  resolve the portrait the same way `BuildPaletteUI.ResolveEntryArtPublic(entry)` does (Village assembly, reachable)
  and prefer the tier portrait `Portraits/<slug>-<level>` when it exists (e.g. `arcane-spire-2.png`, `archer-tower-3.png`
  exist under `Assets/Resources/Portraits/`). Confirm `IconRole` is non-empty for Builder jobs; if null, resolve it here.
- Stop emitting the `"-> T"` label at the source (`:912`). No suite greps `"-> T"` (verified); the only readers of
  `vm.BrowseRows` are `ManageDefenseUpgradeDoorRegression:210` and `ManageTroopsTrainDoorRegression:147` (other tabs).
- **WO-1406 half:** `ChannelSummary.Describe()` (`:69-92`) - when `Busy == 0` the text is `"<Name> idle - <Slots> free"`;
  when busy it stays `"<Name> <Busy>/<Slots>"` (+ the Training depth suffix that `TrainingChipText` already adds).
  The Troops header army line (`Army 3 / 10`) and the locked Troops card door stay in WO-1406's text as lane C items.

### Lane C - the view + capture + suite
`Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs`:
- `private string _selectedBuildingId;` beside `_selectedTroopId`; default = first unlocked choice else `[0]` - copy
  `:1746-1756` verbatim. Rail tap sets it and calls `Render()`; no tutorial signal (Buildings has no authored beat).
- In `RenderList` (`:1638`), branch `ManageTab.Buildings` to `RenderBuildingsDestination(channel)` exactly as Troops is
  branched at `:1660-1666`. Define `RenderBuildingsDestination` **after `FindSummary`** (section 4). Body, in order:
  1. `AddBuildingWorkspaceRow(selected)` - rail + card, `TroopWorkspacePx` high.
  2. `AddBuildingNowBand()` - `MakeRowHost("BuildingNowBand", TrainingNowBandPx)`, header word `BUILDING NOW`, job 1 via
     `BuildTroopTrainingNowJob` inline, extra jobs as `MakeRowHost("BuildingNowRow_" + n, TrainingNowRowPx)`, an
     OPEN QUEUE `BuildObsidianButton` at `PrimaryX0..PrimaryX1` -> `ToggleQueueDrawer`. Empty queue: one line
     `"No builder at work"` (a word, not a blank).
  3. The moved `AddActionNoteRow("Need another town structure?", "Open build", OpenTownBuilder)` row (the footer).
  4. `MakeRowHost("ListTailSpacer", ListTailPx)` + `FlowTrace.Step("Manage", "buildings destination: rail=<n> selected=<id> jobs=<k>")`.
- `BuildBuildingCard(selected)`: `ElarionUiKit.Portrait` medallion; NAME at `ElarionUi.FontTitle`; `"LEVEL " + Level`;
  the badge = `AddImage` (green-ish plate is decoration) + `Label` with `StateWord`; one description line at
  `ElarionUi.FontMicro` with `FitSingleLine`; `ElarionUiKit.CostRow(card, UpgradeCostParts, ..., prefix: "Upgrade:",
  fontPx: (int)ElarionUi.FontMicro)` followed by the time term and the word `Ready` / `Short` (from `UpgradeReady`);
  primary `UPGRADE TO L{NextTier}` and secondary `VIEW DETAILS` via `BuildObsidianButton` on the `TroopCtaY0..TroopCtaY1`
  line, both through `ClampMinTouch`; `"After upgrade: " + AfterUpgradeText` under them. Locked -> ONE grey
  non-interactable face carrying `LockText`; Max -> no CTA, the badge says `Max`; Building -> the primary face reads
  `BUILDING` and is non-interactable.
- **`DrawerInBandMode` (`:1158`)** `=> _vm != null && (_vm.Tab == ManageTab.Troops || _vm.Tab == ManageTab.Buildings)`.
  Without this, opening QUEUE paints the full-body drawer over the card CTAs - the exact WO-1393 defect.
- **`ApplyDrawerPlacement` (`:1178`)** collapses rows by `child.name.StartsWith(TrainingNowPrefix, ...)`. Add
  `private const string BuildingNowPrefix = "BuildingNow";` and extend the test to `|| child.name.StartsWith(BuildingNowPrefix, ...)`.
  Keep the `TrainingNowPrefix` literal untouched (`ManageQueueDrawerRegression.cs:246` pins it).
- **WO-1406 view half:** in `BuildStrip` (`:994-1056`) every chip gets a transparent Button whose tap calls the existing
  tab-switch for its channel (Builders -> Buildings, Training -> Troops, Research -> Research); chip 1 keeps its
  drawer behaviour only if WO-1406's text says so - read it. The Troops header gains the army line `Army <n> / <cap>`
  from the existing WO-1389 army status; the locked Troops launcher card becomes a button labelled `BUILD A BARRACKS`
  whose tap enters build mode (trace), no toast.
- **WO-1412 view half:** the drawer's slot-offer row (`MakeRowHost("Drawer_SlotOfferRow"`) shows `BUY BUILDER` ONLY when
  every Builder slot is busy, and the label carries the real price (`... - 511 SKR (~$9.99)` shape per the WO; read the
  price from the pack, never a literal).
- **Do not touch `ManageScreenPanel.cs:590-645`** (the launcher card block that `HudLabelFitRegression` Case 6 reads as the
  reference for `PlayerDeckWorkspace`).
- `Assets/Editor/UICaptureLaunch.cs` `CaptureManageOperational` (`:7039`): the `:7057` `BaseLayout` fixture adds one
  building already at max tier and one whose next tier requires `VillageTier > 4` (fixture VillageTier = 4), so `Max`
  and a locked row both paint. `SeedManageCaptureQueue` (`:7121`) already enqueues `Upgrade barracks:2:0` -> a live bar
  row paints. **Frame count stays 12** (`MANAGE_OPERATIONAL_CAPTURE_OK count==12`); do not add frames.
- New `Assets/Editor/Regression/ManageBuildingsCardRegression.cs` (section 6) + its one registration line for
  `Assets/Editor/DataRegression.cs` **handed back as text** (that file is CLI-owned merge; do not edit it).

### Lane D - WO-1412 store half (`Assets/_Modules/Wallet/PackStore.cs` + `PanelRouter` only)
Manage -> drawer -> store -> CLOSE returns to Manage on the SENDING tab, not the HUD. Implement from the store side: the
store remembers the sending `(PanelId, tab)` it was opened with and re-opens it on CLOSE (`PanelRouter.Open(PanelId.Manage,
"<tab>")`). If a hunk in `ManageScreenPanel.cs` is unavoidable, hand it to lane C as text; do not edit that file in D.
RED-first suite `StoreReturnToManageRegression` per the WO's acceptance.

## 6. Regression - `ManageBuildingsCardRegression` (lane C authors; the CLI runs RED then GREEN at gate)

Codex cannot run Unity (BATCH_STATE PART 3.3). Author every case with its **one-line revert recipe** in a comment; the CLI
applies the revert, proves RED, restores, proves GREEN, and records both markers in the RESULT.

Runtime cases (build a `ManageScreenVM` headlessly the way `ManageTroopsTrainDoorRegression` does):
1. `[one-choice-per-building]` one `BuildingChoiceVM` per placed ladder id, **including maxed** ones. RED: restore the `continue` at `VM:900`.
2. `[every-choice-speaks]` every choice has non-empty `Description` AND `StateWord` in {Upgradable, Max, Locked, Building}. RED: null the Description fallback.
3. `[no-arrow-labels]` no `Name` contains `"->"`. RED: restore `Ascii(name) + " -> T" + targetTier`.
4. `[benefit-line]` every non-Max choice has non-empty `AfterUpgradeText` (WO-1405). RED: blank it.
5. `[idle-chip-word]` a 0-busy `ChannelSummary.Describe()` contains `idle` (WO-1406). RED: drop the branch.

Source-scoped cases (the drawer suite already states DataRegression cannot instantiate the panel):
6. `[card-paints-the-word]` `Body(panel, "private void BuildBuildingCard(", ...)` contains `.StateWord`. RED: paint a colour instead.
7. `[no-paging-when-it-fits]` `Body(panel, "private void RenderBuildingsDestination(", ...)` does NOT contain `"Showing "` AND file-wide `panel.Contains("Showing \" + (first + 1)")` is still TRUE. Both directions, so it cannot pass vacuously.
8. `[touch-floor]` replay from `Const()`: `(TroopCtaY1 - TroopCtaY0) * TroopWorkspacePx >= 112f` (`ElarionUiKit.MinTouchPx`).
9. `[drawer-band-covers-buildings]` `panel.Contains("ManageTab.Troops || _vm.Tab == ManageTab.Buildings")` and `Body(panel, "private void ApplyDrawerPlacement(", ...)` contains `BuildingNowPrefix`. RED: revert either.
10. `[footer-moved-not-lost]` `Body(panel, "private void RenderBuildingsDestination(", ...)` contains `"Need another town structure?"` and `Body(panel, "private void RenderList()", "private string FindSummary")` does NOT. RED: leave the dead `else if`.

Markers: `MANAGE_BUILDINGS_CARD_OK` / `MANAGE_BUILDINGS_CARD_FAIL <case>`. No hollow passes: a missing fixture is a FAIL naming it.

## 7. Acceptance (the CLI ticks these; Codex hands back the evidence it CAN produce)
- [ ] Brace balance + NUL scan on every `.cs` touched (counts in the hand-back); `.meta` guid unique.
- [ ] `COMPILE_GATE_OK`; `REGRESSION_OK n/n` with the four Manage suites, `BuildCollectionPlayerRegression`,
      `HudLabelFitRegression`, `CostRowFitRegression`, `ObsidianQueueRegression`, `SessionShapeRegression` green and
      `ManageBuildingsCardRegression` green **with its ten RED proofs on record**.
- [ ] `RunManageOperationalCaptureHeadless` -> `MANAGE_OPERATIONAL_CAPTURE_OK count==12`; `ManageBuildings_2670x1200.png`
      and `_1920x1080.png` OPENED by the CLI: selected card with the state word, cost chips (wood/stone render the WORD
      fallback until `currency_wood` / `currency_stone` art lands - that is expected, not a defect), one live bar row, a
      locked row, a Max row, both CTAs >= 112 px, nothing clipped by CLOSE. Zero `[UICap-GEO]` lines.
- [ ] Device: the owner opens Manage - Buildings on the tester build; screencap read; she felt-tests and closes.
- [ ] Deviation recorded: BUILDING NOW is a full-width band (section 3).

## 8. Absorbed tickets - what moves here and what stays
- **WO-1405**: the benefit line (`AfterUpgradeText`) is here. The `grid x, y -> display name + compass side` half is
  DEFENSE and stays on WO-1405 (`"grid " + placed.cellX` is pinned by `BuildCollectionPlayerRegression.cs:124`).
- **WO-1406**: all of it (lane B `Describe()`, lane C chips/army line/locked card door).
- **WO-1412**: all of it (lane D store return; lane C slot-offer row).
Their Status lines are flipped by the CLI in the board commit that lands this WO.

## 9. Not in scope
The Troops tab (WO-1382 shape is the template; do not modify it); Defense and Research tabs (keep the paged path);
`Troop*`/`Building*` unification (Phase 2 WO); migrating `BuildCollectionBrowser` to `ElarionUiKit.GoldPerimeter`;
new portrait art; the `currency_wood` / `currency_stone` icon drop (art, no code).

## 10. Owner rulings
- Target shape = her two mockups (2026-09-05 evening, AskUserQuestion "Yes, build to these").
- Order = close the tree first, then this (same answer).
- "reuse the building cards from build" - applied as material reuse (section 3).
- Open for her: the footer hint row text ("Recommended next upgrades") - the WO ships the moved "Need another town
  structure? / Open build" row as the footer; she may rename it.
