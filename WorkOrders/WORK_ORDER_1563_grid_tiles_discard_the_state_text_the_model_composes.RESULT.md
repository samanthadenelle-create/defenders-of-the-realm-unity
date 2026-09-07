# WO-1563 RESULT - the grid tile states its state, in words

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 2 (BUILDINGS grid) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane: no Unity run, no)*
gate, no commit. Every line was read at source this session (CLAUDE.md section 11B).

## WHAT LANDED - `Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs`, `BuildTile` only

- **`:829-884`, LAYER 7** - `BuildTile` paints `tile.StateText`, the string the model was already
  composing and the renderer discarded. Confirmed the model supplies it: `ManageVmProjection.cs:216`
  and `:312` both set `StateText = item.BadgeText`; the sibling `BuildListRow` paints it at `:717`.
- **`:193-196`** - `TileStateX0 = 0.02f, TileStateX1 = 0.61f`, on the medallion's OWN band
  `TileMedY0..TileMedY1` (`:192`). Word top-LEFT, glyph top-RIGHT, and the word's x ends before
  `TileMedX0` (0.63), so the two state channels cannot overprint.

## ACCEPTANCE
1. **Words from `tile.StateText`** - `:863-874`.
2. **Nothing inferred in the View** - no `switch` on `VisualState` in `BuildTile`; the string is
   painted verbatim. The plate is drawn only when the string is non-empty (`:863`), so an empty state
   paints nothing rather than the bare plate this file already pays for.
3. **No hue dependence** - a WORD on a dark plate: a string plus a shape, on the precedent LAYER 5
   records for selection (*"a gold BORDER ... reads in greyscale"*). No colour carries meaning.
4. **No band taken, none under the floor.** The word rides `TileMedY0..TileMedY1` = 0.35 of the cell
   = **42px** at the `MinTileHeightPx` (120) floor (`:155`), clear of `MinTextBandPx` (28, `:168`).
   The NAME band (`TileTitleY0..Y1`, 0.02-0.26) and the progress bar are byte-for-byte unchanged, so
   `ManageScreenPanel.cs:3948`'s *"never re-shrink a text band below ~24px"* is honoured by taking
   nothing at all. A `FlowTrace.Warn` at `:876-881` reports the band in px if a host ever starves it,
   in the shape of the existing WELL SHORTFALL warning at `:540-548`.
5-7. Oracle below; captures and markers owed.

## ORACLE - `ManageProgressiveDisclosureRegression`, both halves
- **Model half, fixture** (`:327-338`) - inside the existing WO-1516 `CheckBuildGridIsUnlockedOnly`
  GameState fixture: every composed BUILD tile must carry a non-empty `StateText`. It is the only
  non-colour state channel left, because WO-1516 correctly withholds the Available medallion.
  **RED:** blank `BadgeText` in the VM's tile composers.
- **Renderer half, source pin** (`:27-48`) - `BuildTile`'s body must reference `tile.StateText`.
  ⚠ **A source scan by necessity, said so in-file:** `ManageWorkspacePanel` builds real UGUI objects
  and no existing `Manage*Regression` stands one up headless. **RED:** delete the LAYER 7 block.

## REGISTRATION - none. `[manage-progressive-disclosure]` is already at `DataRegression.cs:439`.

## OWED
- `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on **fresh** logs, judged by the marker, never the exit code.
- Both halves **proven RED before green**, both runs recorded (this lane cannot run Unity).
- Fresh `ManageFlow_BUILD_gridtop` + `ManageFlow_ARMY_gridtop`, **opened**, plus the greyscale check
  (acceptance 3) and `[Flow:Manage]` confirmed silent of the well-shortfall warning (acceptance 4).
  The 18:39 frames predate this code and must not be judged against.
- ⚠ **ONE THING THE CAPTURE MUST DECIDE, named rather than assumed:** the state plate spans
  x 0.02-0.61 over the upper-left of the portrait zone (`TilePortX0..X1`, `:190`). On a wide BUILD
  cell the art is drawn `preserveAspect` and centred, so the plate should clear it - but that is
  geometry I have not seen rendered. If it covers art, the plate narrows; the WORD does not shrink
  (`FontHardFloor`, and a sub-floor band renders BLANK, not small).
- Owner felt-verify + close (section 13: the PO closes, not the CLI).

## NOT TOUCHED
The 10 tiles / 5 chips, the research picker (WO-1564), `ManageScreenVM.cs`'s tile composers, tile art
(WO-2015 / WO-1489), the activity strip (WO-2012), and `MaxTileHeightPx` (`:161`).
