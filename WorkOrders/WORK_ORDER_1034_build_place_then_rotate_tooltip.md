# WORK ORDER 1034 — Build mode: teach "tap once to place, then rotate" with a tooltip

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1034 → 1035 in the same edit
**Lane:** Build-mode HUD / onboarding. Presentation + copy.
**Provenance:** owner 2026-08-16: *"add tool til on build that click once to place then rotate (if
needed)"* — read as **tooltip**, with the build-mode screenshot showing the placement ghost and the
rotate rail.

---

## 1. The gap

The build screenshot shows the green Lumber Mill ghost, a right-hand rail of **confirm / rotate /
cancel** glyphs, and the objective strip reading *"Place your Lumbermill - it harvests timber for you."*

Nothing tells the player the **interaction grammar**: that a tap *places* and that rotation is a
*separate, optional* step afterwards. The rail's glyphs are icon-only, so the sequence has to be
inferred. In a first-session build step, inferring is exactly what a new player will not do.

⚠ **The rotate affordance is also colour-carried today** — canon lists *"the build placement ghost
(valid/invalid on the red/green axis)"* as an **open colour-only defect** (anchor 2026-08-09). This
tooltip is a chance to add the shape/text channel that defect needs anyway; it does not fix the ghost,
but it should not lean on hue either.

## 2. The change

A **short, dismissible tooltip** in build mode conveying, in the player's order of operations:

1. **Tap to place** — the primary action
2. **Then rotate, if you want** — explicitly optional (the owner's *"if needed"*)

Requirements:

- **Kit-built.** Use the shared tooltip/toast surface (`ElarionUiKit` — `BuildToast` / the shared notif
  plates per Grok-02 §4). ⚠ Do **not** hand-roll a label; that is the exact defect WO-1033 is fixing on
  the Skip button.
- **Anchored clear of everything.** ⚠ Build mode is the most crowded screen in the game — the category
  column (Town/Defense/Castle Structures), the confirm/rotate/cancel rail, the objective strip, the
  wallet row, the joystick, **and** the Skip button being re-anchored by WO-1033. Coordinate with 1033:
  both target free space and must not claim the same spot.
- **Show when it helps, not forever.** First placements, or until the player has rotated once. A
  permanently-parked tooltip becomes furniture and stops being read.
- **Point at the rotate control** when mentioning rotation, so the sentence and the glyph connect.
- **ASCII only** — non-ASCII renders as tofu (□) on device (Grok-02 §4.2).

## 3. ⛔ OWNER CALL — one question

**Is rotation available before commit, after commit, or both?** The copy must match the real grammar,
and I could not confirm the commit order from the screenshot alone. ⚠ If the tooltip teaches an order
the code does not implement, it is worse than no tooltip — a new player will follow it, fail, and
conclude the game is broken. **Confirm the actual sequence in the build-mode code before writing final
copy**, and record it in the RESULT.

## 4. Do NOT

- Do not add a modal or anything that blocks placement
- Do not restyle the rail glyphs — separate concern
- Do not hand-roll the tooltip widget (§2)
- Do not make it undismissible, and do not re-show it every session once learned
- Do not encode meaning in hue alone (colourblind law)

## 5. Acceptance criteria

- [ ] On a first build placement the player is told **tap to place**, then **rotate if wanted**
- [ ] Built from the shared kit tooltip/toast surface — no bespoke widget
- [ ] **Zero overlap** with the category column, confirm/rotate/cancel rail, objective strip, wallet
      row, joystick, or the WO-1033 Skip button
- [ ] Dismissible, and stops appearing once the player has demonstrably learned it
- [ ] Copy matches the **actual** interaction order per the §3 ruling
- [ ] ASCII-only; legible in greyscale
- [ ] Verified at **2670x1200** (the Seeker's real surface)

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. `UI_CAPTURE_OK` — **open the PNGs**; build mode is the densest screen, so overlap is the likely
   failure and only eyes catch it
3. Device screenshot, landscape, during an actual placement
4. Owner felt-verifies + closes (§13)
