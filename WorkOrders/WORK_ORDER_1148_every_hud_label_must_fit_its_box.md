**Status:** FUTURE - not scheduled. Owner ruling 2026-08-22: *"put as future"*, *"as its not
anything but noise"*.

> ### ⚠ THIS IS COSMETIC NOISE, NOT A DEFECT THAT COSTS THE PLAYER ANYTHING
> A truncated label is UGLY. It does not lose a building, take a resource, strand a loop slot,
> or stop anyone reaching content. Every one of those happened this week and each outranked
> this. **Do NOT pull this ahead of functional work**, and do not let its "generalise the guard"
> framing make it feel structural - the PRINCIPLE is structural, the SYMPTOM is a cut word.
>
> Pick it up when the queue is quiet, or fold it into whatever HUD work comes next.

# WORK ORDER 1148 — Every HUD label must fit its box, not just the two we measured

**Minted:** 2026-08-22 (CLI, banner bumped 1148 -> 1149 in the SAME edit)
**Lane:** HUD / UI. **Class:** GENERALISE A POINT FIX.
**Evidence:** device screenshot from the Seeker, 2026-08-22 13:1x, `Main_Castle_Overworld`, 2670x1200.

## WHY THIS EXISTS

WO-1144 fixed two truncated HUD labels and shipped an oracle that MEASURES real glyph advances
against real boxes at two landscape aspects. It works — the device confirms both fixes:

| label | before | after |
|---|---|---|
| Collectors chip | `Tap to collec` | `Collectors 0/2 full / 3830 waiting` |
| Manage face | `Manag...` | `Manage` + `2/3 idle` on two lines |

**And in the SAME FRAME, two other labels are still cut:**

- **`"Raids ..."`** — bottom action bar, ellipsised while Build / Talk / Bag / Quests all fit.
- **`"SK... 209"`** — top-left currency chip, cut mid-word.

⛔ **THE POINT OF THIS TICKET IS NOT THOSE TWO LABELS.** It is that the oracle **asserts only the
strings it was pointed at**. `Raids` and `SK` passed by never being asked. Fixing them one at a time
produces a third round of this exact ticket.

> ### A guard that checks only what it was pointed at is the same defect class this repo spent
> ### 2026-08-21/22 removing everywhere else: a 4-line hollow-pass window, two VFX oracles with a
> ### gap between them, a fixture that judged a cube while reporting on a slab.

## SCOPE

1. **Enumerate EVERY player-visible HUD label and its box** — action-bar faces (all seven ordinals,
   including the context-gated Talk and the dormant Map), rail chips, currency chips, the wave
   banner, zone/region text, panel titles, toasts. Derive the list; ⛔ do not hand-maintain one, or
   the next label added is unchecked and nothing says so.
2. **Extend `HudLabelFitRegression` to sweep that derived set** using the existing
   `ElarionUiKit.MeasureLineWidthPx` (real per-glyph advances) at the two landscape aspects it
   already covers.
3. **Fix what the sweep surfaces.** Expect more than the two above.
4. **Where a string genuinely cannot fit at any legible size**, shorten it in `canon-strings.json`
   (BOTH copies, byte-identical, ASCII) — never inline at the call site, and never by dropping below
   `ElarionUiKit.FontFloor`.

## THE TRAP WO-1144 ALREADY MAPPED — read its RESULT before starting

Its four defects had **four different causes**, and the same will be true here:
- a string wider than ANY legible size can render in its box (no fit call fixes that — the string
  must change);
- a SENTENCE authored into a word-sized slot;
- a world-space `TextMesh` whose screen size is a function of camera distance;
- two widgets mounted into ONE rect by `hud-areas.json`.

**Diagnose per label. A blanket "add FitBlock everywhere" pass would paper over at least two of those
four** and leave the layout still wrong.

## ⛔ CONSTRAINTS
- `MinTouchPx = 112`. **Do NOT shrink a control to make its label fit**, and do NOT touch
  `CanonCtaWidth` (360) / `CanonCtaHeight` (132) — restored 2026-08-22 after a silent shrink, with
  ~25 files deriving layout from them. Apple's 44x44pt guidance is the source of record.
- Owner is **RED/GREEN COLOURBLIND** — never resolve a collision by recolouring.
- Code-built uGUI only; UXML does not work in player builds.
- ⚠ **MEASURE, do not RESTATE.** Assert measured width against the real box; never recompute the
  expected width from the same constants the layout uses.

## ALSO SEEN IN THE SAME FRAME (fix here or split, implementer's call)
- **`"Wave 2 / Next wave in 103s"` still collides with `Start Now`.** WO-1144 moved the wave block to
  its own fixed-px band and the countdown still overlaps the button — so that fix is INCOMPLETE, not
  merely un-generalised. Worth confirming against the shipped build before re-diagnosing.
- The zone label (`"Elarion - Safe"`) is overlapped by the minimap's lower-left corner.

## ACCEPTANCE
- [ ] No player-visible HUD label truncates or ellipsises at 2670x1200 or at one other landscape aspect
- [ ] The oracle sweeps a DERIVED label set, so a newly added label is covered without an edit
- [ ] Verified by DEVICE SCREENSHOT, not by reading layout code — this ticket exists because the
      numbers looked fine and the frame did not
