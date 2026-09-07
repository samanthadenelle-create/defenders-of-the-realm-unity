# WO-1422: Manage - DEFENSE, RESEARCH and TROOPS all take the WO-1418 Buildings shape, and the paged list is retired

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 2 (BUILDINGS grid), 4 (TROOPS 3x3 grid), 7 (RESEARCH picker) not yet passed (2026-09-07); code landed build 2026.09.06.357599. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: FIXED 2026-09-06 - ON THE SEEKER in build 2026.09.06.357599 (chain 02:59-03:04: APK_OK 463MB, R2_PARITY_OK objects=271; installed 03:06, versionName read off dumpsys; Firebase App Distribution notified) - landed in `9ad5c7e3c`; COMPILE_GATE_OK (c17) + REGRESSION_OK 393/393 (r17) + MANAGE_OPERATIONAL_CAPTURE_OK 12/12 touch=clean (capman6), all twelve frames opened; six oracles proven RED then GREEN (rRED1/2/3); three CLI claims disproved by the lanes and recorded; two known gaps NOT hidden (Defense queue-band label/art, long-name ellipsis). Device build in flight; owner felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-06 (CLI) from the owner's ruling; dispatched to Opus lanes the same night)*)*
**Silo:** HUD / Manage (Village assembly, code-built uGUI) - lanes split by FILE, not by tab
**Owner ruling (2026-09-06, verbatim):** *"I like the way that the build screen is looking for the buildings. That's the
right idea. Can you repeat the same process for troops, defenses, and whatever the other one is"* and, later the same
night, *"for research for troops and for defense, I want them all to match the same structure of how buildings looks
under manage"*.
**Owner ruling on the second door (2026-09-06, AskUserQuestion):** **"Keep one door, but name what's behind it."**
VIEW DETAILS is retired as a label; the door survives, renamed to what it opens, and is HIDDEN when that ladder has
nothing behind it. Applies to Buildings too - see section 3.5.
**Base commit:** `0e274bf25`. WO-1421 (Journey) lands in a disjoint file and may land before or after.

---

## 1. The defect, measured

Two frames captured headlessly this session (`RunManageOperationalCaptureHeadless`, `Builds/capman4`,
`MANAGE_OPERATIONAL_CAPTURE_OK 12/12`, 2026-09-06 01:26), opened by the CLI at 2670x1200.

**`ManageDefense_2670x1200.png`** - the fixture places one archer tower, and the tab still shows nothing to do:
```
MANAGE - DEFENSE                       [BACK]                       [QUEUE]
[Builders 2/2 . 3 queued] [Training 2/2 . 3 queued] [Research 2/2 . 3 queued]
UPGRADABLE TOWERS - affordable first
  No defenses are ready to upgrade. Build your first tower or wall here.
  Need another tower?                                        [BUILD DEFENSE]
                                   [CLOSE]
```
Two thirds of the panel is empty black. No portrait, no selected item, no sense of a ladder.

**`ManageResearch_2670x1200.png`** - the paged text list, clipped mid-row by CLOSE:
```
MANAGE - RESEARCH                      [BACK]                       [QUEUE]
RESEARCH PROJECTS
  Showing 1-4 of 14 - page 1 of 4
  Lumber Mill - Improved Logging          Ready - takes 11m 0s      [RESEARCH]
  1000 gold
  Cathedral of Magic - Arcane Basics      Ready - takes 13m 0s      [RESEARCH]
  1200 gold                        <- CLIPPED by the CLOSE bar
                                   [CLOSE]
```
A paging sentence, developer-shaped `Building - Perk` labels, word-costs, a state sentence per row, and row two cut in
half. This is the exact shape WO-1418 removed from Buildings.

**`ManageTroops` on the device** (`logs/device/screens/seeker-357453-manage-troops.png`, build `2026.09.06.357453`)
already has the workspace, but three parity gaps against Buildings are visible in the frame:
- no state-word badge (Buildings paints `Upgradable` in a badge; Troops paints nothing),
- the benefit line is jammed into a CTA subtitle and **truncates**: `4m 30s . Ready . L3 unlocks Sweepi...`,
- the second door still reads the developer word `VIEW DETAILS` (Buildings) / is absent (Troops).

## 2. The target

All four Manage tabs render the SAME workspace: **portrait rail (left ~26%) + one selected card (right) + a NOW band +
one footer row**. Defense and Research gain it; Troops closes its three parity gaps; Buildings changes only its door
label. The paged list, its pager and its row painter are DELETED.

---

## 3. RULINGS - read every one before designing. These are the decisions the survey surfaced.

### 3.1 Defense rail = ONE ROW PER TYPE, not per placed instance. (This is the load-bearing ruling.)
`BuildDefenseBrowse` (`ManageScreenVM.cs:809-852`) keys rows on **`itemId + "#" + level`** (`:830-831`) and composes the
CTA against one grid cell via `PlacedUpgradeKey.Compose(placed.itemId, placed.cellX, placed.cellZ)` (`:845-846`). Two
Archer Towers at L1 and L2 are two rows today.

**Ruling: the rail lists one row per TYPE.** The card names the type, states how many are placed and at what levels, and
its CTA upgrades **the first placed instance at the lowest level** - which is **exactly what the code already targets**:
`ManageScreenVM.cs:840-844` says in its own words that the key names the FIRST placed instance at that level, and that
keying per instance would emit one row per wall segment. **So this is a presentation change, not a behaviour change.**
- Card sub-line: `"3 placed . lowest L1"` (compose from the tally; when one is placed, `"1 placed . L1"`).
- ⛔ Do NOT build a per-instance rail. `wall_wood` is upgradable and a town has many segments; the rail would be
  unbounded. This is the trap the existing comment warns about.
- `ManageDefenseUpgradeDoorRegression.cs:207-230` drives the **VM** and requires at least one `BrowseRowVM` with
  `ActionText == "Upgrade"` and a non-null `Activate`. **`BuildDefenseBrowse` STAYS** (section 3.4), so that case stays
  green untouched.

### 3.2 Defense is NOT towers-only, and the card must be honest about it.
`BuildDefenseBrowse` admits every `BaseLayout` id with `PlacedStructureUpgradeService.MaxLevelFor(entry) > 1`. Measured
from `structures-catalog.json`, that is: five towers (`tower_ground_archer`, `tower_ballista`, `tower_siege_tower`,
`tower_catapult`, `tower_arcane_spire`, max 3), `wall_wood` (2), `mine_crystal` (3), `healing_caravan` (3), and the three
storage containers `lumberyard` / `foundry` / `silo` (6 each). `wall_stone` and the Gate carry **no ladder at all**.
- **Ruling: change NOTHING about membership tonight.** Same set, new presentation. The heading
  `"UPGRADABLE TOWERS - affordable first"` dies with the paged path, so the lie dies with it.
- **Open for the owner (do not decide it in code):** storage containers and the healing caravan appear under DEFENSE.
  That is pre-existing and may be right or wrong; it is section 9 item 1.

### 3.3 Defense's NOW band IS the Buildings band, and is named `BUILDING NOW`.
`ChannelOf(Defense) == ChannelId.Builder` (`VM:379-387`, the `default:` arm shared with Buildings) and
`BuildQueueRows(ChannelOf(Tab))` (`VM:436`) is per CHANNEL. `VM:539` states the canon: *"Defence and Buildings share the
ONE Builder rail."*
- **Ruling: Defense reuses `AddBuildingNowBand()` verbatim, header word `BUILDING NOW`.** Do not invent
  `DEFENDING NOW`; a second name for one queue is the duplicated state this repo keeps getting burned by.

### 3.4 The paged path is RETIRED, and its three pins move WITH the ruling in the SAME commit.
`AddBrowseRow` has **exactly one call site**, `ManageScreenPanel.cs:1719`, inside the pager (verified by the CLI this
session). Once Defense and Research leave, that call site is unreachable.
- **Ruling: DELETE** the pager block (`Panel:1710-1724`), `AddBrowseRow` (`:2940`) and `BuildBrowseRowContent`
  (`:2946-2993`). Leaving them is dead code under a green pin - precisely the failure
  `ManageQueueDrawerRegression.cs:103-113` was written to catch for `AddQueueRow` (*"a private method with zero callers
  is dead code that LOOKS like a shipped feature"*), and the same duplicated-state rot CLAUDE.md section 5 was corrected for.
- **KEEP the VM builders `BuildDefenseBrowse` and `BuildResearchBrowse` and their `Rebuild()` call sites.** This is the
  proven Troops precedent: `BuildTroopsBrowse` (`VM:1271`) was retained when WO-1382 rebuilt that panel, and
  `ManageTroopsTrainDoorRegression.cs:147-192` still drives it. `BrowseRows` remains the VM's row truth and three suites
  drive it; only the PANEL stops painting it. The one surviving panel reader is the Troops "Saved army compositions" row
  (`Panel:2133-2139`), which reads `_vm.BrowseRows` directly and does not call `AddBrowseRow` - it is unaffected.
- **Re-point these three cases in the same commit** (CLAUDE.md section 15):
  | Suite:line | Today | After |
  |---|---|---|
  | `ManageProgressiveDisclosureRegression.cs:42,:48` | Panel contains `"UPGRADABLE TOWERS"` | assert the Defense DESTINATION exists: `RenderDefenseDestination` present, and `"UPGRADABLE TOWERS"` ABSENT |
  | `ManageProgressiveDisclosureRegression.cs:51-53` | Panel contains `"Showing \" + (first + 1)"`, `"Previous page"`, `"Next page"` | assert all three ABSENT - the pager is retired. Rename the case's failure text; it currently reads "overflow has no visible count and bidirectional paging affordance", which is no longer the design |
  | `ManageProgressiveDisclosureRegression.cs:90-94` | `BuildBrowseRowContent` body contains `r.Locked` + `BuildLockBadge(` | **the lock treatment MOVES to the Research card**: assert `BuildResearchCard`'s body contains `choice.Locked` and `BuildLockBadge(` |
  | `ManageBuildingsCardRegression.cs:143-145` | `RenderBuildingsDestination` lacks `"Showing "` AND the file still contains `"Showing \" + (first + 1)"` | drop the second half; keep the first. Its failure text explicitly names *"the still-live Defense/Research pager"* - that phrase must go with it |
  | `BuildCollectionPlayerRegression.cs:118-119` | Panel contains `"UPGRADABLE TOWERS - affordable first"` and `"Build defense", OpenDefenseBuilder` | the heading half retires; **the `"Build defense", OpenDefenseBuilder` half MUST STAY GREEN** - keep that exact call as the Defense destination's footer row (section 4.2) |
  | `ManageApprovedLauncherRegression.cs:52` | Panel contains `"Build defense"` | unchanged, stays green via the same footer row |
- ⛔ Every one of these suites stays REGISTERED in `DataRegression.cs`. `RegressionMarkerRegression` counts registration
  call-sites in source; removing one shifts the pinned denominator.

### 3.5 The second door is renamed, gated, and applies to Buildings too.
Owner ruling this session. Each choice VM carries **`DoorLabel` (string, null when there is no door)**:
- **Buildings:** `DoorLabel = "PERKS"` when `BuildingTierCatalog` authors at least one perk for that ladder, else
  **null**. Measured perk counts (`building-tiers.json`): `arcane-tower` 4, `lumbermill` 4, `armorer` 3, `barracks` 3,
  `forge` 3, **`farm` 0**. So the Farm card shows ONE full-width CTA and no second door - that is the feature.
  The door still calls the existing `OpenUpgradePanel(id)` (`VM:1541`).
- **Troops:** `DoorLabel = "SKILLS"` when that troop has an authored skill/perk surface, else null. If no such surface
  exists, `DoorLabel` is null for every troop and Troops ships with one CTA plus its upgrade CTA - say so in the
  hand-back rather than inventing a door.
- **Defense:** `DoorLabel = null` for now. There is no per-defense detail page; do not invent one.
- **Research:** `DoorLabel = null` on an available perk. On a LOCKED perk the card's single CTA is the existing
  `"UPGRADE " + BUILDING` / `"UPGRADE THE HEART"` face (section 3.7).
- ⛔ `ManageBuildingsCardRegression` pins the Buildings CTA object names `BuildingCta_Upgrade` and
  `BuildingCta_Details`. **Keep the OBJECT NAME `BuildingCta_Details`; change only the LABEL TEXT.** Renaming the
  GameObject breaks the pin for no player-visible gain.

### 3.6 Research rail = ONE ROW PER PERK (17 rows), not per building.
`building-tiers.json` authors **17 perks across 5 buildings**. A per-building rail (5 rows) would need 3-4 verbs in the
card's single CTA band (`TroopCtaY0..TroopCtaY1`), which no existing card grammar supports.
- **Ruling: one rail row per perk**, card = one perk, one CTA. This is the Buildings grammar unchanged. 17 rows scroll
  fine; the rail is already a scroll view (`Panel:2183`).
- Rail row sub-line = the owning building's name, so `Lumber Mill - Improved Logging` becomes a NAME (`Improved
  Logging`) over a SUB-LINE (`Lumber Mill`). The `" - "` developer label shape dies.
- Perk art is the **one fully covered axis**: `Assets/Resources/HudIcons/BuildingUpgrades/` holds 15 `.jpg` +
  `Upgrade.png` covering all 17 perks, loaded today by `BuildingUpgradePanelMvvm.cs:2025` as
  `Resources.Load<Sprite>("HudIcons/BuildingUpgrades/" + IconName)`. Use that path. ⚠ `BuildingPerkDef.IconId`'s doc
  comment (`BuildingTierCatalog.cs:41`) names `Resources/HudItems/BuildingUpgrades/` - **that folder does not exist**;
  the comment is wrong. Fix the comment in this commit (CLAUDE.md section 15) and cite the real loader.

### 3.7 Research shows its WHOLE tree, including states the list used to hide.
Today an OWNED perk emits no row (`VM:1546`) and an IN-PROGRESS perk emits no row (`VM:1570`).
- **Ruling: `ResearchChoiceVM` includes them.** `StateWord` is exactly one of
  **`"Researched" | "Researching" | "Available" | "Locked"`** (ASCII; the state WORD is the only carrier of state -
  the owner is red/green colourblind, hue is decoration).
  This is the same deliberate delta WO-1418 made when it stopped hiding maxed buildings.
- `Researched` -> no CTA. `Researching` -> one non-interactable face reading `RESEARCHING`. `Locked` -> one
  non-interactable face carrying the reason verbatim from `BuildingPerkService.CanResearch(bId, pId, out reason)`
  (`BuildingPerkService.cs:170-192`) plus the existing `"UPGRADE " + NAME` / `"UPGRADE THE HEART"` door
  (`VM:1580`), which is the behaviour `[research-locked-visible]` protects. `Available` -> `RESEARCH`.
- Research has **no LEVEL**. The card's `LEVEL n` slot carries the owning building's tier requirement instead:
  `"TIER " + PerkUnlockTier`. Do not paint `LEVEL 0`.

### 3.8 Defense art: the tier portraits exist on disk and NO code path can reach them.
`Assets/Resources/Portraits/` root holds `archer-tower.png` + `-2` + `-3`, and the same 3-file pattern for `ballista`,
`catapult`, `arcane-spire`, `wizard-tower`, plus `Sky_Ballista`, `Wooden_Wall`, `Stone_Wall`, `Iron_Wall`,
`Crystal_Mines`, `Healing_Caravan`, `storage_wood` / `storage_iron` / `storage_food`.
- `BuildPaletteUI.ResolveEntryArtPublic` **never appends a level suffix**; `LoadManageBuildingSprite`
  (`Panel:1937-1944`) is level-aware but probes **only** `Portraits/Buildings/`. So `archer-tower-2.png` is on disk and
  unreachable.
- **Ruling: add a `DefenseSprite(DefenseChoiceVM)` that probes, in order:** `Portraits/<portraitKey>-<level>` (level>1),
  then `Portraits/<portraitKey>`, then `BuildPaletteUI.ResolveEntryArtPublic(entry)` (which owns the alias table at
  `BuildPaletteUI.cs:1532-1562` covering `tower_siege_tower->Sky_Ballista`, `wall_wood->Wooden_Wall`,
  `mine_crystal->Crystal_Mines`, `healing_caravan->Healing_Caravan`, `lumberyard->storage_wood`, `foundry->storage_iron`,
  `silo->storage_food`), then `ConceptIconResolver`, then a `FlowTrace.Warn` + the hammer fallback.
  `ManageScreenVM.ResolveBuildingPortraitKey` (`VM:1143-1155`) already emits exactly `"Portraits/<key>[-level]"` - reuse
  it to produce `portraitKey`.
- ⛔ `ManageBuildingsCardRegression.cs:189` (`[building-art-palette-first]`) bans `choice.IconKey` inside
  **`BuildingSprite`'s body**. That ban is scoped to that one method. `DefenseSprite` is a different method and is NOT
  bound by it - but do not touch `BuildingSprite`.
- Result: a Defense card paints real tower art at every tier, tonight, with no new art needed.

### 3.9 The Research NOW band has no art id, and that is acceptable.
`QueueRowVM.BuildingId` is populated **only** when `channel == ChannelId.Builder` (`VM:614`), so a Research job carries
no id. `BuildTroopTrainingNowJob` falls back to `null` art.
- **Ruling: parse the perk from the job id and resolve its icon THROUGH THE CATALOG.** Research job ids are
  `"building-research:<building>:<perk>"` (`SeedManageCaptureQueue`, `UICaptureLaunch.cs:7159-7175`). Split on `':'`,
  take index 2, resolve `HudIcons/BuildingUpgrades/<IconName>` through the catalog. If the split does not yield a known
  perk, pass `null` and let the existing fallback run - **never** throw, and log a `FlowTrace.Warn` naming the id.
  ⚠ **CORRECTED 2026-09-06: this WO originally quoted the job id `building-research:arcane-tower:warding`, and that id
  is INVALID.** `warding` is not a perk; the authored id is **`arcane-warding-runes`** (`building-tiers.json`,
  arcane-tower tier 3). `BuildingPerkService.IsResearching` compares the WHOLE job id
  (`BuildingPerkService.cs:128-131`), so **the `Researching` state has been unreachable in every capture ever taken** -
  a real defect found by the fixture lane, now corrected there. **Never hardcode a perk literal**, and never trust the
  third segment without a catalog lookup. Also: `CatalogRegistry` is NOT populated under `-executeMethod` (see the
  correction in section 4 Lane D), so guard every `CatalogRegistry.Get` in a sprite path against null.
- Defense band: `NormalizeBuildingJobId` (`VM:1104-1116`) produces e.g. `tower-ground-archer`, which is not a
  `BuildingChoiceVM.Id`, so `FindBuildingChoice` returns null and the band falls back. **Accept the fallback tonight**
  and record it as a known gap in the RESULT; do not widen `NormalizeBuildingJobId`, it is shared with Buildings.

### 3.10 Troops parity - three named gaps, nothing else.
1. Add the state-word badge to `BuildTroopCard`, same zone Buildings uses (`(0.74,0.70)-(0.98,0.83)`), painting
   `TroopChoiceVM.StateWord`. If that field does not exist, add it to the VM in lane A with the same four-value
   discipline (`"Training" | "Locked" | "Max" | "Upgradable"`).
2. **Move the benefit line out of the CTA subtitle** to its own row under the CTAs at Buildings' zone
   (`y 0.445-0.535`), text `"After upgrade: " + <benefit>`. This is what fixes the measured truncation
   `4m 30s . Ready . L3 unlocks Sweepi...`; do not fix it by shrinking the font.
3. Apply the section 3.5 door.
⛔ **Verify the CTA band still clears the touch floor with the faces it now carries.** `TroopCtaY0=0.01f`,
`TroopCtaY1=0.445f`, `TroopWorkspacePx=260f` -> band height `113.1 px` vs `ElarionUiKit.MinTouchPx = 112`. That is a
**1.1 px margin**. Any change to those three constants breaks the floor AND breaks
`ManageQueueDrawerRegression.cs:205,230` which reads them by name and pins
`DrawerModeListKeepPx = 10f + TroopWorkspacePx * (1f - TroopCtaY1)`. **Do not touch those constants.** If three faces
cannot fit horizontally at 1920x1080, drop the SECOND door (it is nullable by section 3.5), never the touch height.

### 3.11 Reuse every existing constant and helper. Add no parallel set.
`TroopWorkspacePx 260`, `TroopRailRowPx 112`, `TrainingNowBandPx 120`, `TrainingNowRowPx 88`, `TroopCtaY0 0.01`,
`TroopCtaY1 0.445`, `BandCtrlY0/Y1 0.03/0.97`, `BandGapPx 12`, `SectionHeaderPx 64`, `RowHeightPx 132`, `ListTailPx 28`,
`PrimaryX0 0.76`, `PrimaryX1 0.98`, `ClusterX1`. Rail zone `(0,0)-(0.26,1)`, card zone `(0.275,0)-(1,1)`.
`ManageQueueDrawerRegression.cs:205` reads several by name via `Const(panel, "TroopWorkspacePx")`.

---

## 4. Lanes - split by FILE, because all three tabs live in the same two files

⚠ **Defense, Research and Troops all edit `ManageScreenPanel.cs` and `ManageScreenVM.cs`.** They cannot be three
parallel tab-lanes; they would collide. CLAUDE.md section 9: same-file work = one agent.

### Lane A - `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` ONLY
Declare these two classes **verbatim** (lane B codes against them in parallel, so the names are contract):

```csharp
public sealed class DefenseChoiceVM
{
    public string Id;                 // BaseLayout itemId, e.g. "tower_ground_archer"
    public string CatalogEntryId;     // for BuildPaletteUI.ResolveEntryArtPublic
    public string Name;               // NameOf(entry, itemId) - never "X - grid 3, 7 - L1 -> L2"
    public string PortraitKey;        // ResolveBuildingPortraitKey output, e.g. "Portraits/archer-tower-2"
    public int Level;                 // the LOWEST placed level of this type
    public int MaxLevel;              // PlacedStructureUpgradeService.MaxLevelFor(entry)
    public int PlacedCount;           // how many of this type are placed
    public string PlacedText;         // "3 placed . lowest L1"  (ruling 3.1)
    public string StateWord;          // "Building" | "Max" | "Upgradable"   (ASCII, ruling 3.7 discipline)
    public string Description;        // one sentence, StructureCardVM.DescriptionFor, else the tier Effect
    public IReadOnlyList<CostPart> UpgradeCostParts;  // BuildModeController.UpgradeCostFor(entry, level)
    public string UpgradeTimeText;    // QueueRailView.FormatTime(...); NULL if not reachable - never hardcode
    public bool UpgradeReady;         // affordable && !Building && !Max
    public string AfterUpgradeText;   // next level's effect; "" when Max
    public int NextLevel;             // Level + 1, or 0 when Max
    public string JobKey;             // PlacedUpgradeKey.Compose(itemId, cellX, cellZ) of the FIRST lowest instance
    public string DoorLabel;          // NULL for Defense (ruling 3.5)
    public Action Activate;           // () => UpgradePlaced(JobKey); NULL when Max
}
public readonly List<DefenseChoiceVM> DefenseChoices = new();

public sealed class ResearchChoiceVM
{
    public string BuildingId;         // "arcane-tower"
    public string PerkId;             // "warding"
    public string Name;               // perk display name, e.g. "Improved Logging"
    public string BuildingName;       // rail sub-line + card sub-line, e.g. "Lumber Mill"
    public string IconName;           // BuildingPerkDef.IconId -> "HudIcons/BuildingUpgrades/<IconName>"
    public int UnlockTier;            // BuildingTierCatalog.PerkUnlockTier(bId, pId)
    public string TierText;           // "TIER 2" - the card's LEVEL slot (ruling 3.7)
    public string StateWord;          // "Researched" | "Researching" | "Available" | "Locked"
    public bool Locked;               // StateWord == "Locked"  (the pin in 3.4 reads choice.Locked)
    public string LockReason;         // CanResearch's out reason, verbatim; "" when not locked
    public string Description;        // the perk's authored effect sentence
    public IReadOnlyList<CostPart> CostParts;  // gold-only; CostFormat.Parts with ("gold","Gold",price)
    public string TimeText;           // FormatTime(BuildingPerkService.ResearchSeconds(bId,pId))
    public bool Ready;                // Available && affordable
    public string CtaLabel;           // "RESEARCH" | "RESEARCHING" | "UPGRADE THE HEART" | "UPGRADE <NAME>" | null
    public string DoorLabel;          // NULL (ruling 3.5)
    public Action Activate;           // () => Research(bId,pId), or OpenUpgradePanel(bId) when locked; NULL if Researched
}
public readonly List<ResearchChoiceVM> ResearchChoices = new();
```
- Producers `BuildDefenseChoices()` and `BuildResearchChoices()`, called **unconditionally from `Rebuild()`** beside
  `BuildBuildingChoices()` (`VM:440`) - not tab-gated, exactly as Buildings is.
- Mirror `BuildBuildingChoices` (`VM:1013-1084`) for shape. Defense data: iterate `state.BaseLayout` and tally by
  `itemId`; `PlacedStructureUpgradeService.MaxLevelFor(entry)`; skip `ceiling <= 1`; cost from
  `BuildModeController.UpgradeCostFor(entry, level)`; `PlacedUpgradeKey.Compose(itemId, cellX, cellZ)` of the first
  instance at the lowest level. Research data: `BuildingTierCatalog.All` -> per building (owned only,
  `CountPlacedThisTown()` `VM:903`) -> per tier -> per perk; `BuildingPerkService.IsOwned` -> `Researched`;
  in-progress -> `Researching`; `CanResearch(out reason)` -> `Available` / `Locked`.
- **Add `TroopChoiceVM.StateWord` and `TroopChoiceVM.DoorLabel`** if absent (ruling 3.10, 3.5).
- **Add `BuildingChoiceVM.DoorLabel`** and populate it per ruling 3.5. Leave every other Buildings field alone.
- ⛔ **KEEP `BuildDefenseBrowse` and `BuildResearchBrowse` and their `Rebuild()` call sites exactly as they are**
  (ruling 3.4). Three suites drive them.
- ⛔ `ResourceCost` has **no gold field** (`RepoProps.cs:21-30`) - that is why `BuildResearchBrowse` formats gold by
  hand. Use `CostFormat.Parts` with an explicit gold `CostPart`, the way `BuildingUpgradeCostParts` (`VM:1126-1141`)
  already does. Do not add a field to `ResourceCost`.

### Lane B - `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` ONLY (compiles after A)
- **Placement is load-bearing.** Define `RenderDefenseDestination` and `RenderResearchDestination` **after
  `FindSummary` (`:1760`)**, next to `RenderBuildingsDestination` (`:1767`). Anything between `RenderList` (`:1665`) and
  `FindSummary` enters `ManageQueueDrawerRegression.cs:90`'s ban window and fails the build. Two further windows must
  stay unbroken: `RenderQueueDrawer`->`RenderList` (`:1555-1665`, pinned at `:116-120,:252-256`) and
  `ApplyDrawerPlacement`->`SyncQueueToggleFace` (`:1187-1274`, pinned at `:231` and `ManageBuildingsCardRegression:152`).
- Branch both tabs in `RenderList` exactly as Buildings is at `:1686-1690`, then **DELETE the pager block
  (`:1710-1724`), `AddBrowseRow` (`:2940`) and `BuildBrowseRowContent` (`:2946-2993`)** (ruling 3.4).
- Mirror `AddBuildingWorkspaceRow` (`:1799`), `BuildBuildingRailRow` (`:1850`), `BuildBuildingCard` (`:1965`),
  `BuildDisabledBuildingFace` (`:2050`), `AddBuildingNowBand` (`:2062`) as `AddDefenseWorkspaceRow` /
  `BuildDefenseRailRow` / `BuildDefenseCard` and `AddResearchWorkspaceRow` / `BuildResearchRailRow` /
  `BuildResearchCard`. **Defense reuses `AddBuildingNowBand()` unchanged** (ruling 3.3); Research needs
  `AddResearchNowBand()` with the header word `RESEARCHING NOW` and the perk-icon override from ruling 3.9.
- New fields `_selectedDefenseId`, `_selectedResearchKey` beside `_selectedTroopId` (`:219`) / `_selectedBuildingId`
  (`:220`). Selection default: first `!Locked`, else `[0]`, copied from `:1779-1789`.
- `DefenseSprite` per ruling 3.8; `ResearchSprite` loading `HudIcons/BuildingUpgrades/<IconName>`.
- **`DrawerInBandMode` (`:1179`): APPEND, never reorder.** `ManageBuildingsCardRegression.cs:154` pins the verbatim
  substring `ManageTab.Troops || _vm.Tab == ManageTab.Buildings`. It becomes
  `... ManageTab.Troops || _vm.Tab == ManageTab.Buildings || _vm.Tab == ManageTab.Defense || _vm.Tab == ManageTab.Research`.
  Update its doc comment, which currently says Defense and Research keep the full-body drawer.
- **`ApplyDrawerPlacement` (`:1196-1204`): ADD an OR-branch**, never a rename. Add
  `private const string ResearchNowPrefix = "ResearchNow";` and extend the `StartsWith` test.
  `ManageQueueDrawerRegression.cs:246-249` pins the literals `TrainingNowPrefix = "TroopTrainingNow"`,
  `MakeRowHost("TroopTrainingNowBand"`, `MakeRowHost("TroopTrainingNowRow_"` - leave all three untouched.
- Footer rows: Defense keeps **`AddActionNoteRow("Need another tower?", "Build defense", OpenDefenseBuilder)`**
  verbatim - `BuildCollectionPlayerRegression.cs:119` and `ManageApprovedLauncherRegression.cs:52` both pin
  `"Build defense"`. Research gets no footer door unless one already exists.
- Apply ruling 3.5's label change to `BuildBuildingCard`'s `BuildingCta_Details` (label only, object name unchanged)
  and ruling 3.10's three Troops fixes to `BuildTroopCard` (`:2301`).
- ⛔ **Do not touch `Panel:590-645`** (`HudLabelFitRegression` Case 6 reads it as the `PlayerDeckWorkspace` reference).
- ⛔ Do not touch the `Render()` two-line sequence `ApplyDrawerPlacement();` + `// WO-1368` at `:1374-1375`
  (`ManageQueueDrawerRegression.cs:263-266`).

### Lane C - suites (new files + re-points; NO other lane touches these)
- New `Assets/Editor/Regression/ManageDefenseCardRegression.cs` and
  `Assets/Editor/Regression/ManageResearchCardRegression.cs` (section 5). Fresh unique `.meta` guids, grepped.
- Re-point the six cases in ruling 3.4's table, plus `ManageBuildingsCardRegression`'s door-label case if it pins the
  literal `"VIEW DETAILS"` (check; if it does, re-point it to the new label WITH the ruling).
- Hand back the `DataRegression.cs` registration lines **as text**; that file is a CLI-owned merge point.

### Lane D - `Assets/Editor/UICaptureLaunch.cs` ONLY
The Defense frame is currently **empty** - proof that the fixture cannot exercise the new card.
⚠ **CORRECTED 2026-09-06 by the lane that owns this file: the CLI's stated cause here was WRONG.** This WO originally
said `BuildDefenseBrowse` skipped the tower for being at its ceiling. It is not: the tower is L1 of 3, so it would have
emitted a row. The real bail is one line earlier, `ManageScreenVM.cs:821` (`entry == null -> continue`) - under
`-executeMethod`, `CatalogBootstrap`'s `[RuntimeInitializeOnLoadMethod]` (`Village/Catalog/CatalogBootstrap.cs:96`)
**never fires**, so `CatalogRegistry.Get` returns null for every `BaseLayout` row. Seeding alone would have changed
nothing; the fixture must hydrate the catalog first (`HydrateCatalogForCapture`, which exists because the palette
capture hit the identical hole). Recorded per CLAUDE.md section 11B: an inference stated as a cause, corrected by a
measurement.
- Seed the `CaptureManageOperational` fixture (`:7071-7099`) so Defense shows a populated rail: at least three
  upgradable types at different levels (e.g. `tower_ground_archer` x2 at L1 and L2, `tower_ballista` L1,
  `lumberyard` L3) and one at max so a `Max` card paints.
- Seed Research so all four state words are reachable: one owned perk, one in progress (the existing
  `SeedManageCaptureQueue` Research jobs at `:7159-7175` already give `Researching`), one available, one tier-locked.
- Set `_selectedDefenseId` / `_selectedResearchKey` per target width the way Buildings does at `:7114-7122`.
- ⛔ **Frame count stays 12.** `MANAGE_OPERATIONAL_CAPTURE_OK` asserts `count == 12` (`:6951`). Do not add frames.
- ⛔ `JourneyDeckSubtitleRegression.cs:37` pins `PostureSignals.SetArmyFill(0, 10)` inside the Journey fixture block -
  do not disturb it.
- Also verify `RunManageDefenseCaptureHeadless` (`:6894`), which opens Defense with **no fixture at all**, still paints
  a legible empty state under the new destination. Its marker is `MANAGE_DEFENSE_CAPTURE_OK 3/3`.
- `RunManageLiveQueueCaptureHeadless` (`:6962`) captures Defense and Research **with the drawer OPEN**; those three
  tabs now use band mode, so that frame's geometry changes. Expect it, open it, do not "fix" it.

---

## 5. Regressions the lanes author (the CLI proves RED then GREEN)

Lanes cannot run Unity. Author each case with a **one-line REVERT RECIPE** in a comment; the CLI applies it, proves RED,
restores, proves GREEN, and records both markers in the RESULT. A missing fixture is a FAIL that names itself. No
hollow passes.

**`ManageDefenseCardRegression`** - marker `MANAGE_DEFENSE_CARD_OK` / `_FAIL <case>`
1. `[one-choice-per-type]` one `DefenseChoiceVM` per placed upgradable TYPE, never per instance: place two archer
   towers at different levels, assert exactly one choice with `PlacedCount == 2`.
2. `[lowest-level-targeted]` that choice's `Level` is the LOWEST placed level and `JobKey` composes to the first
   instance at it. RED: take the highest.
3. `[no-grid-labels]` no `Name` contains `"grid "` or `"->"`. RED: restore the old label composition.
4. `[every-choice-speaks]` non-empty `Description` and `StateWord` in {Upgradable, Max, Building} for every choice.
5. `[walls-do-not-explode]` place 8 `wall_wood` segments; assert the rail still yields exactly ONE wall choice.
   **This is the case that guards ruling 3.1.**
6. `[defense-band-is-builder]` source: `RenderDefenseDestination`'s body calls `AddBuildingNowBand()`. RED: give it its
   own band.
7. `[defense-art-tiers-reachable]` source: `DefenseSprite`'s body probes a level-suffixed key. RED: drop the suffix.
8. `[touch-floor]` replay from `Const()`: `(TroopCtaY1 - TroopCtaY0) * TroopWorkspacePx >= ElarionUiKit.MinTouchPx`.
9. `[build-defense-door-survives]` file-wide: `"Build defense", OpenDefenseBuilder` still present.

**`ManageResearchCardRegression`** - marker `MANAGE_RESEARCH_CARD_OK` / `_FAIL <case>`
1. `[one-choice-per-perk]` choice count equals the authored perk count for owned buildings; 17 total when all five are
   placed. RED: key on building.
2. `[all-four-states]` a fixture reaching each of `Researched` / `Researching` / `Available` / `Locked` yields that
   `StateWord`. RED: restore `if (IsOwned) continue;`.
3. `[locked-reason-verbatim]` a locked choice's `LockReason` equals `BuildingPerkService.CanResearch`'s out string
   exactly. RED: substitute a generic "Locked.".
4. `[research-locked-visible]` **the migrated case**: `BuildResearchCard`'s body contains `choice.Locked` and
   `BuildLockBadge(`. RED: paint it like an available card.
5. `[no-level-zero]` no Research card path emits `"LEVEL "`; it emits `TierText`. RED: reuse the Buildings line.
6. `[gold-cost-parts]` a choice's `CostParts` carries a gold part with the authored price. RED: format by hand.
7. `[no-dash-labels]` no `Name` contains `" - "`. RED: restore `buildingName + " - " + perk.Name`.
8. `[perk-icon-path]` source: the sprite loader uses `HudIcons/BuildingUpgrades/`, not `HudItems/`. RED: use the path
   from the stale doc comment. **This case exists because that comment names a folder that does not exist.**

**Re-pointed cases** (ruling 3.4) each need their RED recipe recorded too - the CLI proves the re-pointed direction.

---

## 6. Acceptance - the CLI ticks these
- [ ] Brace balance + NUL scan on every `.cs` touched (counts in each hand-back); every new `.meta` guid unique.
- [ ] `COMPILE_GATE_OK` on a fresh log.
- [ ] `REGRESSION_OK n/n` with **all four** Manage suites, `BuildCollectionPlayerRegression`,
      `ManageApprovedLauncherRegression`, `ManageDefenseUpgradeDoorRegression`, `ManageTroopsTrainDoorRegression`,
      `PlacedUpgradePageTruthRegression`, `ObsidianQueueRegression`, `HudLabelFitRegression`, `CostRowFitRegression`,
      `SessionShapeRegression` green, and the two new suites green **with every RED proof on record**.
- [ ] `RunManageOperationalCaptureHeadless` -> `MANAGE_OPERATIONAL_CAPTURE_OK 12/12`, zero `[UICap-GEO]`,
      `touch=clean`. The CLI OPENS `ManageDefense_2670x1200.png`, `ManageDefense_1920x1080.png`,
      `ManageResearch_2670x1200.png`, `ManageResearch_1920x1080.png`, `ManageTroops_*` and `ManageBuildings_*`:
      rail with real art, one selected card, a state word, cost chips, the NOW band, both CTAs >= 112 px, nothing
      clipped by CLOSE.
- [ ] `RunManageDefenseCaptureHeadless` -> `MANAGE_DEFENSE_CAPTURE_OK 3/3` (the empty-town Defense still reads).
- [ ] `RunManageLiveQueueCaptureHeadless` -> `MANAGE_LIVE_QUEUE_CAPTURE_OK 9/9`, frames opened (drawer geometry moved
      to band mode for Defense and Research - expected).
- [ ] Device: installed, and the owner's felt-test closes it.
- [ ] Deviations recorded in the RESULT, at minimum: the Defense NOW band shows Builder jobs shared with Buildings
      (3.3); the Defense band medallion falls back to the hammer (3.9).

## 7. Not in scope
Buildings' layout (only its door LABEL changes); `Troop*`/`Building*`/`Defense*`/`Research*` unification into one
parameterised builder (that is the Phase 2 WO, after her felt-test of all four); new art; membership of the Defense tab
(section 9); the Queue drawer's own contents; the Journey deck (WO-1421).

## 8. Absorbed
**WO-1405's remaining half** - the Defense `grid x, y -> display name + compass side` item - is superseded by ruling
3.1: the rail is per TYPE, so a grid coordinate never reaches the player. Flip WO-1405 to FIXED in the board commit that
lands this, citing 3.1.

## 9. Open for the owner (do not decide these in code)
1. Storage containers (`lumberyard` / `foundry` / `silo`), `mine_crystal` and `healing_caravan` are listed under
   **DEFENSE** because they carry an upgrade ladder. Should they move to Buildings?
2. The Research rail is 17 rows in one flat scroll. Group by building later, or leave flat?
3. Troops' second door: if no skill surface exists, Troops ships with no second door (ruling 3.5). Confirm.
4. Defense card sub-line wording: `"3 placed . lowest L1"`.
