# WO-1525 RESULT - the harvest result is rows now, not prose

**Status:** IMPLEMENTED - 2026-09-06, EXTENDED 2026-09-07 (see the dated section at the end),
UNCOMMITTED, awaiting gate. Edit-only lane: no Unity, no gate,
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


---

# ADDENDUM 2026-09-07 - the owner's two frames, and what the 09-06 lane could not have known

The 09-06 rewrite shipped to the device (build 358872) and the owner captured BOTH return screens
one minute apart. The redesign is on the screen and the rows read; four defects survived, and one of
them is the reason she said "looked like a fail still on offline harvesting".

## 1. THE ONE PRODUCER - fixed. `HarvestResultVM.Merge` (HarvestResultVM.cs)
`Logs/device/screens/owner-harvest-20260907-011321.png` (WELCOME BACK) vs
`Logs/device/screens/owner-screen-20260907-011426.png` (HARVEST RESULT), 60 s apart:

| | welcome back (banked / STILL WAITING) | harvest result (banked / waiting) | merged waiting |
|---|---|---|---|
| Wood | +2906 / 40,972 | +2,906 / 12,236 | 12,236 + 28,736 silo = **40,972** |
| Iron | +1535 / 21,843 | +1,535 / 6,035 | 6,035 + 15,808 = **21,843** |
| Stone | 0 / 45,257 | 0 / 30,932 | 30,932 + 14,325 = **45,257** |

!! `ReturnRowDestiny` prints `r.Waits = Pending - Banks`, so "40972 MORE WAITS" is what will still be
waiting AFTER the tap - NOT the pending pool. Reading it as pending is an off-by-Banks error that
fits the STONE row by accident (Banks = 0 there) and misses the other two by exactly their banked
figure. The corrected reconciliation above matches the welcome-back column to the unit and sums to
its own 108,072 footer.

The BANKED figures agreed to the unit. **The waiting figures diverged because the harvest result
never merged its producers.** The welcome-back screen has merged both since WO-1434
(`OfflineHarvestService.BuildReturnRows` sums `FromCollectors + FromSilo`); this screen received SIX
statuses for THREE resources, drew the first three and collapsed the Echo silo half into "+3 more".
`Build` now folds by `BankResource` first. Merged `Current` is the MINIMUM (the pre-tap reading), so
`Current + Granted` stays exactly one post-collect figure.

**This also kills "+3 more" at the root, and raising `MaxRows` would have entrenched the bug.**
Only Wood/Iron/Food can overflow (`TownBankCapacity.UncappableResources`), so after the merge
`TotalRowCount` cannot exceed `MaxRows` from any live caller. The tail line is KEPT, unreachable,
for the day a fourth cappable resource exists. `MaxRows = 3` is unchanged.

## 2. TRUNCATED CHIPS - fixed. `HarvestResultRow.ActionVerb` / `.ActionTarget`
"UPGRADE LUMBER..." and "UPGRADE STONEYA..." on her frame; "UPGRADE FOUNDRY" fit. The chip used
`FitSingleLine`, whose contract ellipsizes past the mobile floor - so the one control whose job is
to say WHERE TO GO named a building that does not exist. The break point is now AUTHORED by the VM
and the modal draws two lines with `FitBlock` (wrap-then-truncate, never an ellipsis). `ActionText`
is unchanged, so every existing grep and trace still works.

## 3. THE SCREEN NOW TRACES ITS OWN STRINGS - `vm.ScreenText`, drawn at `HarvestOverflowModal` Open
The 09-06 lane traced input numbers and a row COUNT. Neither proves what text reached the player,
which is why her frame could only be read by eye. `[Flow:Bank] harvest-result screen: ...` now
carries the exact strings, and a second line names the merge when it folded anything.

## 4. THE "*" BEFORE THE TITLE IS NOT A MISSING SPRITE - NO FIX MADE, deliberately
`ElarionUi.CrestGlyph = "*"` (`ElarionUi.cs:201`) is an authored ASCII literal, prepended by the
SHARED title helper (`ElarionUiKit.cs:1532,1538`). Every obsidian modal in the game wears it.
Changing it is a global chrome decision, not this screen's - **raised to the lead**.

## 5. Fixture (the ticket's "screen == banked" proof)
`HarvestResultShapeRegression` cases **13 `[one-row-per-resource]`** and
**14 `[stalled-collector-at-cap]`**. Case 13 drives the SIX statuses the device produced and asserts
three rows, no "+N more", waiting = 40,972 / 21,843 / 45,257 (the welcome-back figures, to the unit),
`After == pre-tap wallet + banked delta`, `MergedSources == 2` per row, and an authored chip split.
Case 14 drives the device's own farm states from the same session - headroom 0 (01:01:56, banked
"0", 32,307 waiting) and headroom 2100 (01:03:36, banked "+2,100", 30,207 waiting).
Case 11 `[row-cap]` was REPAIRED in the same edit: it built `MaxRows + 2` statuses all carrying
`BankResource.Wood`, which the merge now folds into ONE row - it would have been asserting the cap
against a one-row screen.

## 6. Welcome-back surface (in scope as of tonight - it IS the harvest screen on resume)
- **The RAID door and the army/Heartfire line are RETIRED** - owner reversal 01:13, verbatim "no
  idea why raid is listed here". `WelcomeBackDoorsVM` no longer produces them; the fields stay and
  are always empty, so `AddReadyBand` draws nothing. `WelcomeBackDoorsRegression` cases 3/4 are
  INVERTED to `[raid-door-retired]` so restoring WO-1408's spec fails in editmode instead of on her
  device. The ATTACKED row SURVIVES - it is a report door, not a raid invitation. Recorded for
  WO-1408's RESULT.
- **The truncated footer** ("...Spend, or upgrade stora") - `AddMendLine` seats every line in a
  0.09-tall band; the one full SENTENCE on the screen now gets `AddFooterSentence` (0.19, two lines,
  same wrap-then-truncate helper).
- **The copy no longer implies 30 minutes produced 108k**: `ReturnFooterLine` reads
  *"108,072 is already waiting in your collectors and Echo silo - nothing is lost. Spend, or upgrade
  storage."* Grouped with InvariantCulture (a device locale groups with U+00A0 = tofu).

## OWED - not claimed, no Unity ran here
- The `HarvestOverflow_*.png` capture at 2670x1200 and 1920x1080, OPENED. The fixture in
  `UICaptureLaunch.CaptureSystemModalsOnce` was REPLACED with the owner's own six-status state (long
  container names, three full plates), so the capture now exercises both defects - but **that three
  plates fit and no chip truncates is a CAPTURE claim and is unproven from this lane.**
- `REGRESSION_OK n/n` on a fresh log; owner felt-verify closes it.
- The large EMPTY BAND under the welcome-back table is the shared shell's fixed body Zone
  (`ElarionUiKit` `ZoneBacking(layout.body, ObsidianFill)`), not this file's geometry. Retiring the
  ready band and growing the footer reclaim some of it; shrinking the zone is a kit change and was
  NOT attempted without a measurement.
