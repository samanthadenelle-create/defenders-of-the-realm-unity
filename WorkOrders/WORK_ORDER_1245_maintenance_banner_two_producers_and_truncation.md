# WORK ORDER 1245 - The maintenance banner has TWO producers, and it truncates the operator's message

**Status:** READY TO IMPLEMENT
**Silo:** Core/Ops + Core/UI (client only - no server, no schema)
**Severity:** P2. The banner's headline works, so the owner's core ask is met. What is broken is the
*why*, and the proof that the *why* works.
**Origin:** CLI, 2026-08-27. Found by the WO-1243 proof screenshot - not by reading the code, which
had looked correct twice.

---

## How this was found, because the method is the point

WO-1243 landed green: `COMPILE_GATE_OK`, `REGRESSION_OK 308/308 suites`, a dedicated
`MaintenanceTogglesRegression` proving all four fail-open paths. Every marker said done.

Then the banner was photographed (`Builds/ui-capture/MaintenanceBanner_*.png`) and both defects
below were visible in a single glance at one image. This is the standing lesson in
`docs/` and in memory `screenshots-are-primary-evidence-for-visual-defects`: **FlowTrace shows what
the code believes, the screenshot shows what the player gets.** A gate cannot tell you that a string
it never renders is being cut off by a font metric.

## Defect 1 - TWO PRODUCERS OF THE SAME LINE, and the tested one is the dead one

⛔ **`MaintenanceCatalog.BannerText()` HAS NO RUNTIME CALLER.** Verified 2026-08-27:

```
grep -rn "BannerText()" --include=*.cs Assets
  -> MaintenanceTogglesRegression.cs  (5 hits - the test)
  -> UICaptureLaunch.cs               (the capture, since corrected)
  -> MaintenanceCatalog.cs            (the declaration)
```

What the player actually sees comes from `MaintenanceBannerDriver.Line(area, state)`
(`Assets/_Modules/Core/Ops/MaintenanceBannerDriver.cs:171`), a **private** method with its own
formatting, reached via `RebuildLines()` -> `ObjectiveBannerUi.Show(line)`.

**The two formats genuinely differ.** Same sealed state, same run:

| Producer | Output | Who reads it |
|---|---|---|
| `MaintenanceCatalog.BannerText()` | `MAINTENANCE ON RAIDS AND THE STORE - Raids are closed while we fix the reward payout. Back shortly.` | the regression, and nothing else |
| `MaintenanceBannerDriver.Line()` | `MAINTENANCE ON RAIDS - Raids are closed while we fix the reward payout. Back shortly.` | **the player** |

⚠ So `MaintenanceTogglesRegression`'s banner assertions - the ones that make the toggles look
proven - **assert a string no player will ever see.** The seal/fail-open logic they cover is real and
still good; the BANNER coverage specifically is aimed at a dead API. That is this session's recurring
disease exactly: a second copy of a fact, drifting, with the copy nobody looks at being the one under
test.

**Required:** collapse to ONE producer. The catalog is the shared authority, so lift the driver's
per-area formatting into a public `MaintenanceCatalog.LineFor(...)`, have the driver call it, and
re-point the regression at it. Do NOT leave `BannerText()` behind as a second entry point "for the
tests" - that recreates the split on day one. If a combined multi-area summary is genuinely wanted
somewhere, it must be built FROM `LineFor` and have a real caller.

## Defect 2 - the operator's message is truncated, so the player is told nothing useful

`ObjectiveBannerUi` (`Assets/_Modules/Core/UI/ObjectiveBannerUi.cs:186-187`) sets
`textWrappingMode = NoWrap` and `overflowMode = Ellipsis` at `fontSize = 20`. Correct for the
tutorial objectives it was built for ("Build a tower"). Wrong for this.

Photographed result at 2340x1080:

```
MAINTENANCE ON RAIDS - Raids are closed whil...
```

The headline survives - so WO-1243's acceptance 3 ("reads as maintenance from its WORDS") is still
met, and the owner's ruling is still honoured. But the operator authors a message precisely so a
player who taps a closed area **already knows why**, and roughly the first 40 characters is all that
survives. Every message longer than that is decoration.

⚠ **`ObjectiveBannerUi` IS SHARED WITH THE TUTORIAL.** Do NOT flip wrapping on globally and re-flow
every tutorial objective as a side effect. Make it opt-in at the call site, and leave the tutorial's
current behaviour byte-identical.

⚠ The plate is a fixed-height single-line surface. If wrapping needs the plate to grow, that is a
layout change - measure it, do not eyeball it, and re-shoot the capture.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. Exactly ONE producer of a maintenance banner line, with a grep in the RESULT proving the retired
   one has no callers left.
3. The regression asserts the string the PLAYER receives. Prove RED first (WO-1138) by changing the
   driver's output and watching the test fail - if it still passes, it is still aimed at the wrong API.
4. ⭐ **A fresh `Builds/ui-capture/MaintenanceBanner_*.png` in which the whole operator message is
   readable.** This ticket was found by a screenshot and it closes on a screenshot. The capture entry
   point already exists: `DeNelle.Editor.UICaptureLaunch.CaptureMaintenanceBanner`.
5. The tutorial banner is unchanged - shoot it too and compare.
6. ASCII-only strings; no meaning by colour alone (the owner is red/green colourblind).

## What NOT to touch

- ⛔ The WO-1243 fail-OPEN ruling and the six toggles' semantics. This ticket is about the TEXT, not
  the gate.
- ⛔ The server half (`api/_lib/maintenance.js`, `api/maintenance.js`). Client-only.
- ⛔ The tutorial's existing wrap/ellipsis behaviour.
