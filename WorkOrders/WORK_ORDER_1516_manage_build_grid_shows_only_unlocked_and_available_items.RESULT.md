# WO-1516 RESULT - the Manage BUILD grid shows only unlocked, available items

**Status:** IMPLEMENTED - 2026-09-06, uncommitted, awaiting the Unity gate.
**Lane:** edit-only. No Unity run, no gate, no commit. Files touched: `ManageScreenVM.cs`,
`ManageProgressiveDisclosureRegression.cs`.

## WHAT LANDED (verified at source this session, not from the hand-back text)

1. **ONE unlock authority.** `ManageScreenVM.InventoryTiles()`
   (`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:3876`) now fills `_inventoryTiles` from
   `BuildInventoryModel.Tiles(_activeFilter)` at `:3905`, with the reasoning recorded in-code at
   `:3895` ("ONE PREDICATE, AND IT IS THE PALETTE'S OWN"). `Tiles` is the accessor whose own doc
   comment (cited at `:3868`) records that it matches the BUILD palette's `BuildAvailability.Offered`.
   So the Manage grid and the Build palette answer "is this unlocked" from the same predicate, and
   no second predicate was written.
2. **The green up-arrow stops lying** (acceptance line 2). New
   `ProjectAffordanceTile(...)` (`:3967`) wraps `ManageVmProjection.ProjectTile` and WITHHOLDS the
   status medallion when a tile's visual state is the `Available` CATCH-ALL while its primary action
   is not actually available. Called from the three grids that carry a `ManageAction`: BUILD
   (`:3930`), ARMY (`:4217`) and RESEARCH perks (`:4421`). The four meaningful glyphs
   (locked / in-progress / queue / max) are untouched.
3. **`ManageTiles(...)` is left standing** and unused by this path, so the ARMY grid's locked-troop
   treatment (mockup panel 4) is unaffected - this ruling was BUILD-only.
4. **WO-1516 fix-shape item 3 needs no action.** Grepped `Assets/` this session: the string
   `"UNLOCKS AT VILLAGE LEVEL"` appears in NO live composition path. The only hits are a comment in
   `ManageScreenPanel.cs:3535` describing the retired early-return, and comments in
   `ManageBuildingsCardRegression.cs:195,216` / `HeartSurfaceRegression.cs:18,162`. The copy is
   already gone from the Manage path.
5. **Section 3 respected.** Unaffordable-but-unlocked items are NOT filtered - they get WORDS
   (see WO-1518's `ShortBadgeText`, `ManageScreenVM.cs:4878`). Only the *locked* state leaves.

## MEASURED CASE

`ManageProgressiveDisclosureRegression.CheckBuildGridIsUnlockedOnly`
(`Assets/Editor/Regression/ManageProgressiveDisclosureRegression.cs:204`, called at `:25`, tagged
`[build-grid-is-unlocked-only]`). Stands up a `GameState` fixture, drives the real VM through
`EnterTab(ManageTabId.Build)`, composes the grid, and asserts:
- (a) the tile count equals `BuildInventoryModel.Tiles(chip).Count` - the authority, not a literal (`:273`);
- (b) no tile renders `ManageTileVisualState.Locked` (`:285`);
- (c) a tile reading `SHORT` / `HEART GATED` carries no `StateIconKey` - the badge that meant nothing (`:298`).
It FAILS rather than skips when the grid composes zero tiles (`:267`) or the state seam is absent (`:238`).

## GATE HYGIENE (this lane)
`ManageScreenVM.cs` braces 418/418, NUL 0. `ManageProgressiveDisclosureRegression.cs` braces 16/16,
NUL 0. No `.cs` written through a shell redirect.

## REGISTRATION
No `DataRegression.cs` edit is needed. `ManageProgressiveDisclosureRegression` is ALREADY registered
in HEAD at `DataRegression.cs:439` (`[manage-progressive-disclosure]`).

## OWED - not doable from an edit-only lane
- `COMPILE_GATE_OK` and `REGRESSION_OK n/n` on a fresh log.
- **CLOSED 2026-09-06** by the WO-1541 / 1563 / 1564 Manage lane, which held the `UICaptureLaunch.cs`
  silo: the `(Build, LockedDetail)` skip is now in the plan loop at
  `Assets/Editor/UICaptureLaunch.cs:7684-7697`, with the WO-1516 citation. ARMY and RESEARCH keep
  their `LockedDetail` frame; `expected` still derives from `plan.Length` (`:7753`), so no frame
  count is hand-kept. Uncommitted, awaiting the same gate. The original owed text follows.
- **The `BuildManageFlowPlan` spec line (WO section 5).** `Assets/Editor/UICaptureLaunch.cs` is STILL
  another lane's uncommitted work (`git status` shows ` M` on it this session), so it was deliberately
  not touched. Without the three-line skip of `(ManageTabId.Build, ManageFlowFrame.LockedDetail)`, the
  next `RunManageFlowMapCaptureHeadless` will `MANAGE_FLOW_MAP_FAIL` - the BUILD grid can no longer
  produce a locked tile to photograph.
- A fresh `ManageFlow_BUILD_gridtop` PNG, opened (acceptance line 4).
- Owner device felt-verify + close (CLAUDE.md section 13: the PO closes, not the CLI).
