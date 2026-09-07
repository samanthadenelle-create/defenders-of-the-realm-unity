# WO-1525 RESULT - the harvest result is rows now, not prose

**Status:** IMPLEMENTED - 2026-09-06, UNCOMMITTED, awaiting gate. Edit-only lane: no Unity, no gate,
no git. Capture + felt-verify OWED (below).

## What changed
**NEW `Assets/_Modules/Core/UI/HarvestResultVM.cs` (+331).** The pure seam, on the WO-1408
`WelcomeBackDoorsVM` pattern: rows in, rows out - no service lookup, no clock, no scene. Per row:
`BankedText` (`+2,814`, headline), `WaitingText` (`23,353 waiting, safe`), `StorageText`
(`26,000 / 26,000  FULL`), `StateWord`, `Fill01`, one action (`BUILD STONEYARD`/`UPGRADE LUMBERYARD`/
`SPEND WOOD`) to `PanelId.Manage`+`"Buildings"`. `MaxRows = 3`, DERIVED from the modal's band
constants (a 4th plate falls under the touch floor) and free today: `UncappableResources`
(`TownBankCapacity.cs:265-269`) exempts Crystals/Coins, so only Wood/Iron/Food overflow; extras
collapse to `+N more`. **Colourblind law:** state is the WORD `FULL`/`OVER` in
the bar label. **WO-1434 law:** the waiting figure is kept; `nothing was lost` is ONE footer, and is
withheld when any row genuinely burned.

**`Assets/_Modules/Core/UI/HarvestOverflowModal.cs` (+231 / -10).** The prose Label is replaced by
`BuildRows`/`BuildRow`: plate per resource, name + banked figure on top, an `ElarionUiKit.Bar` whose
value label carries both figures AND the state word, the waiting figure beneath, the chip at FULL
plate height (`:281`) - the WelcomeBackPopup `DoorRowH` trick. `Route` (`:296`) goes through
`PanelRouter`, Warning on a refusal; `BuiltContainers` (`:316`) counts non-`IsBaseStore` slots off
`TownBankCapacity.Apportion`.

**Readable-floor fitting (reg-wave3b red, FIXED).** The rewrite dropped the call
`TownBankCapRegression.cs:461-463` scans for. Restored as a REAL fit on the one wrapping block:
`FitBlock(label, ElarionUi.FontFloorMobile, ElarionUi.FontMicro)` on the footer (`:219`) - FitBlock
wraps and TRUNCATES, so the reassurance cannot be shortened into a lie. Every other label passes the
mobile floor explicitly; no ellipsis mode is assigned in this file.

**NEW `Assets/Editor/Regression/HarvestResultShapeRegression.cs` (+340).** 12 cases on the owner's
20:29 figures (Stone 0/32,307 cap 3,000 no Stoneyard; Wood 2,814/26,167; Iron 792/13,083). Markers
`HARVEST_RESULT_SHAPE_OK`/`_FAIL`.

## Registration line (DataRegression.cs NOT edited)
```
DeNelle.Core.Diagnostics.Guard.Try("Regression", "harvest-result-shape suite", () => { if (!DeNelle.Editor.Regression.HarvestResultShapeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[harvest-result-shape] " + r); });
```
Seat it beside the existing `harvest-result-copy` line (`DataRegression.cs:691`).
## Contradictions raised, not resolved
1. **`BuildBody` no longer draws the screen** - kept because `HarvestResultCopyRegression` calls it
   and `[clamped-grant-warns]` greps its literals. Retire-or-rewrite is a LEAD RULING.
2. **A comment lied; corrected in the same breath (section 11B).** It called the `Collected:` /
   `Uncollected:` literals "PINNED by [clamped-grant-warns]" as authoritative truth. They ARE
   grepped - `TownBankCapRegression.cs:457-460`, a lint - not by the warn case at `:384-399`.
3. **That oracle pins a variable NAME, not behaviour** - it greps the exact text
   `FitBlock(label, ElarionUi.FontFloorMobile`, so the footer variable is `label`, said at source.
4. **`SPEND`'s destination is unproven** - only `Manage`+`"Buildings"` is provably registered
   scene-independently, so all three chips route there; a spend surface is the owner's call.
5. **Section 2B (stone cap, no Stoneyard) untouched** - economy lane, as the ticket instructs.
## OWED
- Headless `HarvestOverflow_*.png` at 2670x1200 AND 1920x1080, OPENED, greyscale still reads. No
  Unity ran here, so "three rows fit" and every pixel height are NOT claimed.
- `REGRESSION_OK n/n` + green `[clamped-grant-warns]` on a fresh log; `.meta` for the two new `.cs`
  (editor generates, committer stages); owner felt-verify closes the ticket.

## Gate evidence in-lane
Braces / NUL 0 on all three `.cs` (86/86, 19/19, 22/22); zero non-ASCII added; FlowTrace kept and
extended by one compact `vm.TraceLine` Step; modal file is LF in the working copy.
