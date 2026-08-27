# WO-1248 RESULT — Hero-select carousel rotate control

**WO:** `WorkOrders/WORK_ORDER_1248_hero_select_carousel_button_truncates.md`
**Status of WO file:** left as READY TO IMPLEMENT (task: do not flip Status).
**Seat:** CLI implementer. No commit, no `git add`, no Unity batchmode.

---

## Cause class: LOCAL width + a SHARED recipe (not a copy typo)

This is the third truncation this week (WO-1245 banner, PROD-014 repair toast, this ticket). Two halves:

1. **LOCAL width.** The rotate lanes were `PrevX 0.148-0.216` / `NextX 0.784-0.852` — **0.068 of the stage well**. At portrait 1080x1920 that plate is ~63 ref px. `BuildObsidianButton` insets its label to x `0.04-0.96`, leaving **~58 px** for the word.
2. **SHARED recipe.** `ElarionUiKit.BuildObsidianButton` always arms `FitSingleLine` (`NoWrap` + `Ellipsis`, autosize down to `FontFloor` 30). Any word that cannot fit that floor becomes `"Pr..."`. The live call was `BuildObsidianButton(..., "< PREV")` — already a shortened workaround, and `'<'` is a TMP rich-text landmine.

The kit recipe is correct for a CTA authored wide enough. It is the **wrong control for a narrow directional plate**. This ticket did **not** change `FitSingleLine` globally (that would smuggle a structural refactor into every Obsidian button). It stopped **feeding** that recipe a word the plate cannot hold.

Full `"Previous"` at `FontFloor` is wider than a `MinTouchPx`(112)-wide portrait plate. Stuffing the full word into the old recipe **is** the captured `"Pr..."`. Shortening to `"Prev"` as the only change would have hidden the layout bug behind copy, which the WO forbade.

---

## What the control looks like now

Designed **ICON+word** rotate plates, named `CarouselPrev` / `CarouselNext`:

| | Prev | Next |
|---|---|---|
| Plate | Obsidian Style1 Gray (brightness press feedback, not hue) | same |
| Chevron | `<<` at `FontHead` (64), richText OFF | `>>` |
| Word | `PREV` at `FontMicro` (32), richText OFF | `NEXT` |
| Layout | chevron on top (y 0.34-0.96 of the plate), word under it (y 0.04-0.32) | same |
| Overflow | `Overflow`, **not** Ellipsis — a miss is visible | same |
| Autosize | off — the oracle measures the size the player sees | same |

**Lanes (fractions of the stage well):**

- Prev x `0.012-0.142` (0.130 of well), Next x `0.858-0.988`
- Height inside the carousel band: y `0.42-0.90` (was `0.53-0.86`)
- Still disjoint from the side cards (`SideLXMin 0.2591`, `SideRXMax 0.7409`)

**Authored size, reference px** (CanvasScaler 1080x1920 match 0.5):

- Seeker landscape 2670x1200: ~241 x 169 (shortest 169 >= 112)
- Landscape 1920x1080: ~215 x 151
- Portrait 1080x1920: ~121 x 269 (shortest 121 >= 112)
- Seeker portrait 1200x2670: ~135 x 374

`PREV` / `NEXT` / `<<` / `>>` are ASCII. State is size + shape + the word, never hue (owner is red/green colourblind). `<<`/`>>` is the rotate affordance; the word is confirmation. This is a designed abbreviation **with** an icon, measured — not a hidden truncation of `"Previous"`.

Side cards, focal card, roster, and WO-1010's BUILD palette carousel were not touched.

---

## Geometry assertion / RED-first (WO-1138)

`Assets/Editor/Regression/HeroSelectCarouselRegression.cs` (`hero-select-carousel`), registered in `DataRegression.RunAll`.

- Measures `PREV`/`NEXT` at `FontMicro` and `<<`/`>>` at `FontHead` against the live plate at four surfaces. Width is `MeasureLineWidthPx` (real glyph advances), not a character count.
- Asserts shortest side >= `MinTouchPx` as **authored**, so `ClampMinTouch` is a no-op.
- Pins every fraction by source lint against `HeroSelectController.cs`.
- **CaseHistoricalIsRed:** the pre-fix 0.068 lane + kit 0.92 inset vs the word `"Previous"` at `FontFloor` on portrait 1080x1920. The suite **FAILS if that old box would pass**. That is the proof it would have gone red on today's truncated layout.

Markers: `HERO_SELECT_CAROUSEL_OK` / `HERO_SELECT_CAROUSEL_FAIL`. Also folded into `REGRESSION_OK <n>/<n> suites` once DataRegression runs.

---

## Capture

`UICaptureLaunch` had no hero-select shot. Added `CaptureHeroSelect` at:

- 1920x1080
- 2670x1200 (Seeker landscape)
- 1080x1920 (portrait)

Output: `Builds/ui-capture/HeroSelect_<w>x<h>.png`. Builds the real `HeroSelectController.BuildScreen` in edit mode (inactive host, so a save-with-hero cannot `GoCastle`). Not fired here — task forbids Unity batchmode. Next `RunCaptureHeadless` will write the PNGs; those are the primary visual evidence the WO asked for.

---

## Files edited

- `Assets/_Modules/Onboarding/HeroSelectController.cs` — rotate control + band table
- `Assets/Editor/Regression/HeroSelectCarouselRegression.cs` — new oracle
- `Assets/Editor/Regression/HeroSelectCarouselRegression.cs.meta`
- `Assets/Editor/Regression/DataRegression.cs` — register the suite
- `Assets/Editor/UICaptureLaunch.cs` — `CaptureHeroSelect`
- `WorkOrders/WORK_ORDER_1248_hero_select_carousel_button_truncates.RESULT.md` — this file

Not touched: WO Status line, HudKitController, WO-1010 BUILD palette, hero roster/class data, kit `FitSingleLine`.

---

## Gates not run (per task)

Unity batchmode was forbidden, so `COMPILE_GATE_OK` / `REGRESSION_OK` / `UI_CAPTURE_OK` are **not claimed**. Brace-balance was checked on every `.cs` touched. Owner felt-verify on device is still owed (WO acceptance #4).
