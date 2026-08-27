# WORK ORDER 1248 - Hero select shows "Pr..." where it means "Previous", and the carousel needs a real rotate control

**Status:** READY TO IMPLEMENT
**Silo:** UI / HUD
**Severity:** P2. Reachable on the hero select screen, which every player passes through.
**Origin:** Owner, on device, 2026-08-27: *"the hero select screen has pr... instead of previous need
better button to rotate the carosel"*.

---

## The two halves

**1. The label truncates.** The button reads `Pr...` instead of `Previous`. The word is being cut by
the control's width, not by the text being wrong.

**2. The rotate control is not good enough.** The owner asked for a *better button to rotate the
carousel* — this is a usability ask on top of the truncation, not a restatement of it. Fixing only
the truncation leaves half the ticket undone.

## ⭐ THIS IS THE THIRD TRUNCATION DEFECT THIS WEEK. TREAT THE CLASS, NOT JUST THE INSTANCE.

- **WO-1245** — the maintenance banner cut the operator's message at ~40 characters (`NoWrap` +
  `Ellipsis` on a fixed-width plate).
- **PROD-014** — "NEED MORE TO REPAIR" toast truncated on both lines.
- **This ticket** — `Previous` becomes `Pr...`.

Before fixing this one, **check whether the fix belongs one level up.** If several controls share a
sizing helper or a button recipe that assumes short labels, patching this single button just moves
the queue along. Say plainly in the RESULT which it was: a local width bug, or a shared recipe.

⚠ Do NOT "fix" it by shortening the word to `Prev`. That hides a layout defect behind copy, and the
next longer label re-breaks it. If an abbreviation is genuinely the right design, say so and make it
a deliberate choice, not a workaround.

## Mobile constraints that bind this

- **Touch targets: `MinTouchPx = 112`.** A carousel rotate control the owner is asking to be
  "better" is very likely also too small to hit reliably one-handed.
- The owner is **red/green colourblind** — never carry state by hue alone.
- **ASCII-only in strings.** The tofu oracle fails the regression on characters the UI font cannot
  render. If you want an ellipsis, three ASCII dots. No arrow glyphs unless font coverage is proven.
- **UXML does not work in builds** — code-built UI only.

## Required

1. The full word renders at every supported resolution, or an abbreviation is a stated design choice.
2. A rotate affordance that is comfortable one-handed on a phone.
3. Say whether the cause was local or a shared recipe (see above).

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **A fresh screenshot of the hero select screen** with the label fully readable. Screenshots are
   primary evidence for visual defects: WO-1245 passed every marker and its own dedicated regression
   while visibly truncating text, and only the image caught it. The headless harness is
   `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless`; add a hero-select capture if none exists.
3. A geometry assertion, not just an eyeball — the label's measured width must fit its container.
   Prove RED first (WO-1138).
4. Owner felt-verifies on device.

## What NOT to touch

- ⛔ WO-1010's BUILD palette carousel. Different screen, different ticket, already FIXED.
- ⛔ Hero roster/class data. This is presentation only.
