# WO-1411: Build never says what you can afford, the ghost stage is three icon-only buttons, and confirm shows no cost or time

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:00:42, build 2026.09.07.359076). PRIOR STATUS: FIXED 2026-09-06 in eb161dc98 - suite-green after the MVVM fix, awaiting owner felt-verify. PRIOR STATUS: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review.

> **CORRECTION 2026-09-06 (WO-1478):** every cost basket quoted below as an example was a HARNESS STUB, not
> game data. `Assets/Editor/UICaptureLaunch.cs` hardcoded a wood+iron+crystals string into the ghost pill, so
> the `BuildGhostChips_blocked` frame this ticket cites shows a price the game cannot charge - and that shape
> (all three resources in one basket) is precisely what WO-947 forbids. The authored Arcane Spire row is
> **iron 360**. The example strings are replaced with placeholders below; the FIX SHAPE is unaffected, because
> it always said the line comes from the structure def, never from a literal.

## Evidence
- AGREED by both reviewers quoting the same words (`REVIEW_MERGED.md` row 10; `REVIEW_A_independent.md` C-1..C-4,
  `REVIEW_B_independent.md` C1 / C3 / C4 / C5). Frames: `Builds/ui-capture/BuildCollections_2670x1200.png` (07:02) -
  eight cards, no card says what is affordable; `Defenses` and `Protection` side by side; the eighth card
  `Upgrade Defenses` in a smaller title; banner `First build: select a category.`.
  `BuildGhostChips_blocked_2670x1200.png` (07:02) - `Arcane Spire - <fabricated basket, see the correction above>`
  + `Not enough Wood` (the worded refusal is good; the basket and the resource it names were both stub fiction),
  bottom-right three unlabelled glyphs (check / rotate / X). `BuildPreview_2670x1200.png` (09-01) -
  orientation only, no cost, no time. The palette dock frame (08-27) is STALE - re-capture before touching it.
- CODE: `Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs:191` `Label(..., "Upgrade Defenses", 30, ...)`,
  `:176` its tap `PanelRouter.Open(PanelId.Manage, "Defense")`; `BuildFirstUseGuide.cs:25` `First build: select a
  category.` (Step.Category); `BuildPreviewModal.cs` (confirm); `GhostPreview.cs` / `BuildPlaceButton.cs` (ghost stage).

## What the player experiences
Eight doors and no hint which one has something buildable behind it; a Manage door dressed as a category; a
ghost with three symbols to guess at, against the owner's names-not-icons ruling; and the last tap before
spending shows neither the price nor the wait.

## Fix shape (one mechanism)
- Collections card subtitle carries a count from the palette's existing NEED check: `Gathering - 2 you can build
  now` / `Defenses - nothing affordable yet` (VM computes, card renders).
- Ghost stage buttons get words: `PLACE / ROTATE / CANCEL` (kit `ButtonPack`, same seats).
- Confirm modal adds one line above CONFIRM: `<live cost words> . <build time> . Builder free` from the
  structure def + `BuildTimerConfig` + free-slot count. Never a literal - the words come from the same
  `CostFormat` seam the card and the ghost pill use.
- The first-use banner takes the PHASE (`Place it - drag, then PLACE`) once a ghost is armed; `Step.Category`
  copy never persists past its step.
- Rename per ruling #13: `Protection` -> `Walls & Gates`, `Defenses` -> `Towers`; `Upgrade Defenses` leaves the
  card grid and becomes a footer text link `Already built? Manage defenses >` (same route `:176`).

```
[ Gathering - 2 you can build now ] [ Towers - 1 you can build now ] [ Walls & Gates - nothing affordable yet ]
ghost:  Arcane Spire - <live cost words>                     [ PLACE ] [ ROTATE ] [ CANCEL ]
confirm: <live cost words> . <build time> . Builder free        [ CONFIRM ]
```
Trace: `FlowTrace.Step("Build", "collection=<id> affordable=<n>")`, `FlowTrace.Step("Build", "confirm cost='<text>' time=<s>")`.

## Acceptance
- [ ] RED first: `BuildAffordabilityWordsRegression` - every collection card subtitle contains `build now` or
      `affordable`; ghost stage exposes buttons labelled PLACE / ROTATE / CANCEL; confirm modal text contains the
      cost words and a duration; no card is titled `Upgrade Defenses`; banner text while a ghost is armed is not the
      Category step copy. Fails on the current tree.
- [ ] Headless: `BuildCollections`, `BuildGhostChips_blocked`, `BuildPreview` `_2670x1200.png` regenerated
      (`UI_CAPTURE_OK`), opened; `HudLabelFitRegression` green; the stale palette dock re-captured.
- [ ] Device: BUILD > pick a category > place; the three words and the confirm line read; screencap read.

## Not in scope
Structure costs/times (tunables); the palette dock's `NEE / D` wrap until a fresh frame proves it; build mode
camera/input; the Manage Defense tab (WO-1405).

## Owner ruling
- Section 2 #13 Rename? - written to the default: rename YES, keep the 8th card NO (footer link).
