# WO-1384: HUD left column - the Heartfire row is unreadable and half off its plate; the Night Market card must be the shining gem

**Status:** FIXED - in 65d5a7eae, on the Seeker in build 2026.09.05.355952 (Heartfire single readable line inside the plate; Night Market card 320x156 with gold frame + aura). Awaiting owner felt-test. The 23:59 refinement (rounded corners + chasing colour-shifting glow, "WO-1384b" below) is a follow-up lane for a later build, not yet on the device.

## Owner, verbatim (2026-09-04 23:03-23:05, felt-test on the Seeker, build 355905)
1. "in the top left there is something under the Heart of Elarion, but i cannot read it its too small on screen"
2. "night market should not be ... it needs to be the shining gem, it should draw attention to it so it above all stands out"

## Evidence (captured, not inferred)
`docs/qa/seeker-hud-left-2026-09-04.png` (adb screencap 23:03, top-left crop at 3x). The row under the
objective reads `[*] [*] [*]  Heartfire` and is drawn straddling the plate's BOTTOM edge - the glyphs sit
half outside the panel - at the plate's smallest text size. The Night Market card below it is a dark
thumbnail with "NIGHT MA..." truncated, visually indistinguishable from the FLAG chip under it.

## Source (read this session)
- Heartfire row: `Assets/_Modules/HUD/Kit/HudKitController.cs:1655-1672` - `_heartfireLabel` is a
  `Label` at y 0.04-0.32 of `_heartPlate.Root`, `FontLabel`, `FitBlock`; the objective label sits at
  0.34-0.58 (`:1644-1650`, fit 16-18 px). The plate is `BuildPartyNameplate` at (0.02,0.02)-(0.99,0.98)
  of the cluster root (`:1642`). Painted by `RepaintHeartfire` (`:4020`) from `PostureSignals`; the
  words "[*] [*] [ ]" are the deliberate greyscale-safe state model (`:1661-1665`); colour/icon is the
  owner's call (WO-1379 s4).
- Night Market card: `BuildNightMarketCard` `:979-995`, seated by `HudLayoutBands.ResolveNightMarketCard`
  (the column's one authority) at `HudLayoutBands.NightMarketCardWidthPx x NightMarketCardHeightPx`;
  WO-1335 made it the store's PERMANENT face.

## Rulings to honour
- Heartfire: readable at arm's length on 2670x1200 (glyph band >= the objective's 16-18 px, target the
  plate's name size 20-26); INSIDE the plate; the three charges read as three distinct marks in
  greyscale. The state model and words stay; only size/placement/marks change. If the plate cannot
  hold three rows at readable size, grow the plate (the cluster root), never shrink the text.
- Night Market: the one element on the HUD that draws the eye - "the shining gem". Larger than the
  FLAG/settings chips, its own frame, a lit treatment (a gold frame/glow the kit already has -
  `ElarionUi.Gold`, the aura/glow primitives used by the Night Store aura, WO-1343), the full label
  "NIGHT MARKET" never truncated. Standout by SIZE, FRAME and LIGHT, never by hue alone.
  ⛔ The owner is red/green colourblind: no state carried by colour.
- Touch targets >= MinTouchPx; ASCII-only; `hud-areas.json` occupancy rows unchanged (no new widget id).

## Acceptance
- [ ] Headless capture at 2670x1200 (`UICaptureLaunch`): Heartfire row fully inside the plate, three
      marks legible at 100% zoom; Night Market card the largest and brightest element in the column.
- [ ] `HudLabelFitRegression` / `HudDockLayoutRegression` / `UiTouchClampRegression` green; add a pin
      that `_heartfireLabel`'s rect lies inside `_heartPlate.Root`'s rect.
- [ ] Owner felt-test on the Seeker.

## Not in scope
The Heartfire mechanics (WO-1379), the Night Market store itself (WO-1050/1335), the raid door.

## Owner refinement 2026-09-04 23:59 (verbatim, after seeing build 355952)
> "instead of just dropping a yellow box around the store on the left of UI can we round the edges and have a
> chasing soft color changing vfx, subtle but inviting?"

Ruling applied to the Night Market card (follow-up lane, WO-1384b in this WO):
- The rectangular gold `AddImage` frame goes; the card gets ROUNDED corners (the kit's rounded sprite, or a
  9-slice with radius) and a soft CHASING glow that runs around the edge and drifts through a warm palette
  (gold -> amber -> rose -> gold), slow (a lap every ~4-6 s), low alpha - "subtle but inviting".
- Colourblind law still binds: the size/rounding/glow-MOTION carry the standout; hue is never the only cue.
- Built with what exists: the `RadialGlowSprite` aura already mounted + a UV-scrolling ring (a second Image
  with a soft ring sprite rotated per frame, or `Material.mainTextureOffset`), driven from the HUD's existing
  Update at <= 1 ms/frame - the VFX budget line `[Flow:Store] aurora cost` is the pin. No particle system on
  the HUD canvas.
- Knobs on the tunables rail (owner 09-02: a balance/feel value is a tunable): `hud.nightMarketGlowLapSec`,
  `hud.nightMarketGlowAlphaPct`, `hud.nightMarketGlowPaletteMask` - all four sources in one change, oracle pinned.
- Headless capture cannot see motion: acceptance is the device (a 3 s screen recording via `adb shell
  screenrecord`) plus the frame-cost trace.
