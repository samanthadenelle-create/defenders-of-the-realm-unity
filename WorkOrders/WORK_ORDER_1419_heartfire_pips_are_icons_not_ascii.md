# WO-1419: the Heartfire plate paints flame ICONS, not `[*] [ ]` ASCII pips

**Status:** FIXED 2026-09-05 22:27 - Codex lane landed ember-medallion slots (candidate A, lead greyscale pick), oracles moved with the ruling, HeartfirePipsRegression green, COMPILE_GATE_OK + REGRESSION_OK 386/386, AdaptiveHudPeaceful frame opened (RESULT file); device build after the owner's reboot; felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 (CLI) from the owner's ruling; dispatched to the Codex dev lane, PART 8 item 6)*
**Silo:** HUD - Heart plate (DeNelle.HUD + DeNelle.Core.State)
**Owner ruling (2026-09-05, verbatim):** "yeah i hate those [*] items when we should use some icon, we have over 4000"

## 1. The defect, at source
- `Assets/_Modules/Core/State/HeartfireCharges.cs:334-347` `FlameRow(charges, max)` returns `"[*] [*] [ ]"` - ASCII
  brackets stand in for flames. Its own doc-comment (`:330-333`) says colour and icon treatment are the owner's call
  and that the row "reads correctly with no art at all". She has now made the call: icons.
- `Assets/_Modules/HUD/Kit/HudKitController.cs:4544-4568` builds `_heartfireLabel.text = flames + HeartfireMarksGap + label`
  (`HeartfireMarksGap = "   "` at `:2020`; the label is created at `:2115-2119` with auto-sizing).
- Any oracle that asserts the `[*]` / `[ ]` literal or the exact `FlameRow` string is re-pointed WITH this ruling
  (grep `FlameRow` and `\[\*\]` under `Assets/Editor/Regression/` at your base and list every hit in the hand-back).

## 2. The target
- The flame marks become a row of `Image` slots on the Heart plate, one per max charge, to the LEFT of the text label;
  the text label keeps `PlateLabel` + `PlateRekindle` (WO-1415, pinned by `HeartfireRegression` PIN G) and drops the
  pip prefix. `CountLabel` ("Heartfire 2/3") remains the word form for badges that must fit.
- **Lit vs spent must differ by SHAPE/FILL, not hue** (owner is red/green colourblind): a lit slot shows the flame
  sprite at full alpha; a spent slot shows the same sprite as a hollow/dim silhouette (alpha ~0.25 AND a visibly
  different fill - e.g. the hollow variant if the pack has one) so the two states survive a greyscale check.
- Pure presentation: `HeartfireCharges` stays the state model. Add `FlameStates(charges, max)` returning `bool[]`
  (or keep `FlameRow` for logs and traces - do not delete it; traces still print it) and let the View bind slots to it.
- Repaint on the same change-detection the label uses (`force || countMoved`); never per frame (the `[Flow:Offset]`
  ring-buffer lesson at `HudKitController.cs:4570-4575`).

## 3. Icon selection - survey, do not invent
The icon packs in the tree are large ("over 4000"). Survey them at your base (`find Assets -iname '*.png'` under the
icon / RpgUi / UI packs; `Assets/Resources/RpgUi/` has roles `abilities, badge, element, hud, emblem, ...` -
`icon_heart.png` exists but no flame under `icons/`). Hand back **three candidate flame/ember sprites** with their
Resources-loadable path (or Addressable address), size, and a greyscale-contrast note for lit-vs-spent. The CLI picks
one against the greyscale gate; the owner is NOT asked to choose a hue (memory `owner-colorblind-delegate-visual-creative`).
If none of the candidates is Resources-loadable at runtime on Android, say so - a sprite that only the Editor can see
is not a candidate.

## 4. Regression (author; the CLI runs RED then GREEN)
`HeartfirePipsRegression` (or a new case group inside `HeartfireRegression`):
1. `[no-ascii-pips-on-plate]` source: `HudKitController.cs` no longer concatenates `FlameRow(` into `_heartfireLabel.text`. RED recipe: restore the concatenation.
2. `[slot-count]` runtime: `FlameStates(2,3)` -> `[true,true,false]`; `(0,3)` all false; `(5,3)` clamps to 3 true. RED: drop the clamp.
3. `[sprite-loads]` runtime: the chosen sprite path resolves via `Resources.Load<Sprite>` (non-null) - a missing art drop fails here, not silently on the phone.
4. `[states-differ-in-greyscale]` runtime: the lit and spent slot treatments differ in alpha by >= 0.5 OR use different sprites (assert the constants), so the state is not hue-only.
5. `[plate-copy-unchanged]` `HeartfireRegression` PIN G stays green untouched - the words are not this ticket's.

## 5. Acceptance
- [ ] `COMPILE_GATE_OK`, `REGRESSION_OK n/n` incl. `HeartfireRegression` and `HudLabelFitRegression` green; the new cases green with their RED proofs on record.
- [ ] `UI_CAPTURE_OK`: the HUD capture with the Heart plate OPENED by the CLI at 2670x1200 and 1920x1080: flames visible, lit vs spent distinguishable in a greyscale copy, label text unclipped.
- [ ] Device: owner sees flames on the plate; screencap read.

## 6. Not in scope
Plate words (WO-1415), the rekindle timer, the raid door gate, Manage (WO-1418).
