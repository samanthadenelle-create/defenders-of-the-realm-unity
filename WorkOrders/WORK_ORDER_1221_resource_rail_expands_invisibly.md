# WORK ORDER 1221 - Tapping the resource chip logs "expanded" and renders NOTHING

**Status:** READY TO IMPLEMENT
**Silo:** HUD
**Severity:** P1 — the player cannot see Wood / Iron / Stone / Crystals anywhere in town.
**Origin:** Owner felt-test, Seeker build `2026.08.26.341419`, 2026-08-26.
Owner verbatim: *"cannot click 1015 to see the resources"*.

---

## PROOF — an injected tap, and a before/after capture inside the 6-second window

The owner's report was that the chip could not be clicked. **That is not what is happening**, and the
distinction is the whole ticket: the tap lands, the code declares success, and nothing appears.

**1. The tap registers.** `adb shell input tap 2510 731` (centre of the gold chip, device
coordinates), immediately after `logcat -c`:
```
10:57:47.984 [Flow:HudKit] resource chips tap-expanded (6s window)
10:57:47.988 [Flow:HudKit] resource panel expanded (opener live=True)
10:57:47.988 DeNelle.HUD.Kit.HudKitController:Update()
```

**2. Nothing renders.** Device capture taken INSIDE the 6-second window,
`tmp/resources-expanded-105803.png`, against `tmp/resources-tap-105648.png` from before the tap:
**both frames show only the gold chip reading `1034`.** No Wood, no Iron, no Stone, no Crystals.
Byte-different screenshots, identical HUD.

**3. §12 classification: BUILT-BUT-INVISIBLE.** Not *data-empty* (the log's own `opener live=True`
says the opener resolved) and not *threw-and-skipped* (no Guard/Fail line anywhere in the window).

## The seam

`Assets/_Modules/HUD/Kit/HudKitController.cs:~1679-1688`:
```csharp
tapBtn.transition = Selectable.Transition.None;
tapBtn.onClick.AddListener(() =>
{
    _chipsExpandUntil = Time.unscaledTime + 6f;
    FlowTrace.Step("HudKit", "resource chips tap-expanded (6s window)");
});
_resGoldOnly.plate.raycastTarget = true;   // the chip is the tap target here
...
Register("resourceChipsCollapsed", WrapAsWidget("resourceChipsCollapsed", tapGo));
```

The click handler only moves a **timestamp**. Whatever consumes `_chipsExpandUntil` in `Update()` is
the thing that is not painting. Start there, and instrument the consumer before editing it.

## ⭐ SECOND DEFECT, IN THE SAME LINE — a trace that cannot report failure

```
[Flow:HudKit] resource panel expanded (opener live=True)
```

**This line prints "expanded" whether or not a single chip appeared.** Ask the standard's question —
*"what broken state would make this line print something different?"* — and the answer is **none**.
It is decoration, and it is decoration sitting in the exact place that would otherwise have caught
this months ago.

⛔ **The fix is NEVER to delete it.** Make it falsifiable: assert an OUTCOME, not an intent — the
resolved rect of the expanded rail, its child/chip COUNT, and its resolved opacity, measured AFTER
layout settles. Reuse **`DeNelle.Core.Diagnostics.UiSurfaceProbe`**
(`Assets/_Modules/Core/Diagnostics/UiSurfaceProbe.cs`, WO-976) — do NOT re-derive the arithmetic. It
already separates `SURFACE_ZERO_SIZE` / `SURFACE_TRANSPARENT` / `SURFACE_OFFSCREEN` /
`SURFACE_BEHIND`, which is precisely the four-way split needed here.

Two travelling rules from that WO, both load-bearing here:
- **Measure AFTER layout settles.** A size read at expand time is pre-settle; the observed ceiling
  elsewhere is 8 frames — **poll, do not guess a frame count**.
- **When it cannot be measured, emit a NAMED SKIP, never a pass** (`IsUnmeasurableEnvironment`).
  Batchmode runs no layout pass. *"Not measured"* and *"measured and fine"* must never be the same
  value.

Add the ranked entry to `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md` in the same change.

## ⚠ Also decide (raise to the owner, do NOT decide alone)

**The 6-second auto-collapse.** Even once it paints, the rail closes itself 6 seconds after a tap.
A player checking whether they can afford something may well be slower than that. Is a timed peek
the intended interaction, or should the tap TOGGLE? That is a design call, not an implementer's.
Report the recommendation; do not change the duration silently.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **A DEVICE SCREENSHOT at 2670x1200 taken INSIDE the expand window, showing Wood, Iron, Stone
   and Crystals**, opened and looked at. ⛔ A green marker is NOT acceptance — the current build
   already emits a cheerful success line while rendering nothing.
3. ⭐ A regression that **FAILS on today's tree**: drive the expand and assert the rail's measured
   child count > 1 and non-zero resolved size via `UiSurfaceProbe`. Prove it RED first
   (WO-1138) — and note that a test asserting only "the handler ran" would pass today and is
   exactly the decoration being replaced.
4. The falsified trace line quoted in the RESULT, showing a real measurement rather than
   `opener live=True`.
5. Owner felt-verifies and CLOSES.

## What NOT to touch

- ⛔ `ClampMinTouch` as a diagnosis — ruled out at three sites already; the tap DEMONSTRABLY lands.
- ⛔ `MinTouchPx = 112`. Nothing here may shrink a touch target.
- ⛔ The collapsed gold-only chip itself. It renders correctly; the expansion is the defect.
- ⛔ Never convey the resource identity by colour alone (owner is red/green colourblind) — icon plus
  WORD, as `HudKitController.cs:1591-1596` already pairs `CurrencyKind.Food` with the label `"Stone"`.
