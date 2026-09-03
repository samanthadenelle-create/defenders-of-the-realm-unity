# WORK ORDER 1319 — Action-bar face labels overlap into an unreadable run at narrow aspect

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — implemented by de5bb13a5 `fix(pi): the wallet gate, the SKR storefront, and the overlapping action bar` (body §WO-1319), with the measured RCA in bc62afa16. Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02; body unchanged.)* *(Prior line:)* **Status:** READY TO IMPLEMENT
**Silo:** HUD / Layout
**Minted:** 2026-09-02 (CLI) from an owner screenshot during a desktop web felt-test.
**Severity:** P2 — legibility, not function. The bar still works.

## Owner evidence

Screenshot, `echoes-of-elarion.vercel.app`, build `2026.09.02.352005`, narrow desktop browser window.
The bottom action bar renders its labels as one unbroken run:

```
BUILDTALKHERO...QUEUE MANAGE
```

Five separate face labels printed with no gap, overlapping each other.

## Why this is a real defect and not just "the window is too narrow"

The clipping of OTHER surfaces at this aspect is expected — the game's UI is authored for landscape
and this was a tall, narrow window. **The labels are different.** Text that runs into its neighbour
is a layout failure at ANY width: the correct degradation is to ellipsize, shrink to a floor, or drop
the label and keep the icon. Overlapping is the one outcome that is never right, and it means the
label widths are not constrained by their slot.

⚠ Do NOT "fix" this by declaring the aspect unsupported. A Pi Browser phone in portrait, before the
WO-1312 rotation engages or if its fail-safe fires, lands in exactly this shape.

## Where to look

`HudActionBarModel` / its View own the slot geometry. Canon facts, so they are not re-derived:
- `MaxVisibleFaces = 6` is a MAXIMUM, never the count. `ButtonCount` stays **7** (enum identity /
  array bound). The bar is normally FIVE faces in open town — `Talk` is added only while a talkable
  NPC is in range and `Raids` only when `RaidCapable`. **A five-face bar is the feature working.**
- `ActionBarButtonId.Map` is dormant at ordinal 4 — never renumber, the face arrays are ordinal-indexed.
- Touch targets: `MinTouchPx = 112`.

So the slot count is VARIABLE at runtime, which is very likely the mechanism: a width divided for six
faces, or labels sized for a fixed slot, will collide once the real count and the real width disagree.
**Establish that from the layout code before changing a number.**

## Acceptance criteria

1. At the owner's aspect, every face label is readable and none overlaps its neighbour.
2. The degradation is explicit and authored — ellipsis, a size floor, or icon-only below a width
   threshold. Not "it happens to fit now".
3. Correct at 5 faces AND at 6 (NPC in range). Prove both; the variable count is the suspected cause.
4. `MinTouchPx = 112` still honoured — do not shrink the tap target to make text fit.
5. Verified from a CAPTURE, not from reasoning. A screenshot is the primary evidence for a visual
   defect; `UI_CAPTURE_OK` proves pixels were written, not that they are legible — that marker went
   green over a panel carrying four visible defects on 2026-09-01.

## What NOT to touch

- ⛔ Do not renumber `ActionBarButtonId` or change `ButtonCount` (7). Both are load-bearing.
- ⛔ Do not "restore" a sixth always-on face to make the maths even. Five is correct in open town.
- ⛔ Do not carry meaning by colour. The owner is red/green colourblind.
- ⛔ ASCII-only strings — non-ASCII renders as tofu in TMP.

---

## Implementation record (2026-09-02, edit-only agent; NOT yet gated or captured)

**Mechanism established from the layout code, not guessed.** The bar the owner captured is the
`AdaptivePeacefulDock` (BUILD / TALK / HERO / JOURNEY / MANAGE), not the retired
`HudActionBarModel` repacker - `HudKitController.BindActionBar` returns early whenever
`_peacefulDockRoot != null` and hides every legacy face. The chain, measured:

* `HudAreasHost.Build` - canvas is ScaleWithScreenSize 1080x1920, MatchWidthOrHeight 0.5, so the
  canvas-LOCAL width is `sqrt(W/H) * 1440` reference px.
* `HudAreasHost` ActionBar mount = `0.270 .. 0.730` = **46% of that width**.
  Landscape 16:9 -> 883 px. A tall/narrow window (W/H 0.60) -> **513 px**.
* `HudKitController.BuildPeacefulDockSlot` sliced the mount into five equal FRACTIONS
  (gap 0.018, width `(1 - 6*gap)/5`): 157.5 px per slot in landscape, **91.5 px** narrow.
* 91.5 < `ElarionUiKit.MinTouchPx` (112), so `UiKitMinTouchGuard.LateUpdate` grew every slot
  symmetrically about its centre by 10.2 px per side into a gap that was **9.2 px** wide.
  Five 112 px slots need 560 px; the mount offered 513. The slot RECTS overlapped by
  construction and the caption (anchored 0.06..0.94 of its slot root) rode the overlap.

The clamp is correct. The FRACTION AUTHORING was the defect - the same failure class as
WO-865 and WO-1060.

**Fix.** A four-rung authored ladder, solved in reference pixels, live (a browser window
resize is a shipping event, so build-time math would be stale):

1. COMFORTABLE - the authored fraction already clears the floor. Landscape is reproduced
   byte-for-byte (the oracle re-derives the retired literals at three shipping sizes).
2. EXPAND - the track grows RIGHT ONLY, up to `HudAreasHost.SafeRightX` (0.995). Never left:
   the mount's left edge IS the MoveCluster's right edge, and a mis-tap on the movement stick
   is strictly worse than a narrow bar.
3. TIGHTEN - gaps collapse toward zero before any slot is allowed under the touch floor.
4. OVERFLOW - only below aspect ~0.28 (narrower than 1:3.6, measured) is the floor unreachable
   in one row; there the solver declares it, drops captions to icon-only, splits evenly, and
   FlowTrace.Warns. Nothing silently overlaps.

Captions additionally go through the shared kit's existing `ElarionUiKit.FitSingleLine`
(NoWrap + bounded autosize + Ellipsis, floored at `FontHardFloor` 20) so a label can never be
wider than its slot whatever the solver hands it. No shared-kit method was modified.

**Files**
* NEW `Assets/_Modules/Core/UI/HudDockLayout.cs` - pure static solver (DeNelle.Core).
* NEW `Assets/_Modules/HUD/Kit/HudDockSlotLayout.cs` - live responder (measures, applies, traces).
* NEW `Assets/Editor/Regression/HudDockLayoutRegression.cs` - `[dock-layout]`, markers
  `HUD_DOCK_LAYOUT_OK` / `HUD_DOCK_LAYOUT_FAIL`; registered in `DataRegression`.
* `Assets/_Modules/HUD/Kit/HudAreasHost.cs` - ActionBar band edges named once + derived headroom.
* `Assets/_Modules/HUD/Kit/HudKitController.cs` - peaceful AND combat docks wired to the solver.

**Scope note (recorded loudly, owner unavailable):** the COMBAT dock (`BuildAdaptiveCombatDock`,
six medallions) sits in the SAME mount and sliced it into six fractions - it carried the identical
defect one posture away, worse. It was fixed with the same ladder rather than left to be
re-reported. That is the only scope expansion.

**Still open - only a fresh capture can settle it (acceptance 5):**
* Whether "JOURNEY" reading as "JOUR..." at a portrait slot width is acceptable copy, or whether
  the owner would rather shorten the word. That is a copy decision, deliberately not made here.
* Whether the caption band is tall enough in LANDSCAPE for the new 20 px floor: it resolves to
  ~27 px at 2340x1080 against a ~25-28 px line box, so `UiKitTextFitGuard` may grow the caption
  rect by ~1 px and log one Warn per face on first build. Harmless if so; visible in a capture.
* The dock is visibly OFF-CENTRE at narrow aspects (it grows right only). Deliberate.
