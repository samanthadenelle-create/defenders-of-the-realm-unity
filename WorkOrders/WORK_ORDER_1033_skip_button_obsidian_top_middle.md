# WORK ORDER 1033 — Skip button: use the common Obsidian button, move it to top-middle

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1033 → 1034 in the same edit
**Lane:** HUD / tutorial chrome. Presentation only.
**Provenance:** owner 2026-08-16, verbatim: *"Move Skip Button to standard Obsidian button from common
and move to top middle of screen."*

---

## 1. What is wrong now (owner screenshots, two contexts)

**Build-mode capture:** the Skip control is a bare label with **no button chrome at all**, sitting in the
top-right and **overlapping the vertical confirm/rotate/cancel rail** — its text collides with the
circular confirm glyph beneath it.

**Echo-dialogue capture:** the same control renders as a small unstyled tab **overlapping the
`Echoes 1/6` chip**.

Two failures, both structural, not cosmetic:

1. **It is not built from the kit.** Every CTA in the game goes through
   `ElarionUiKit.BuildObsidianButton` (Grok-02 §4: *"use these, don't invent widgets"*). This one
   doesn't, so it inherits no frame, no 3-state art, no touch floor.
2. **It is anchored into occupied space** — top-right is the action rail's territory in build mode and
   the Echoes chip's in town, so it collides in **both**.

## 2. The change

**Rebuild it with the common factory, and re-anchor to top-middle.**

- Construct via **`ElarionUiKit.BuildObsidianButton`** — the shared 5×4 Style×Color family. It supplies
  the frame, pressed/disabled states and the `MinTouchPx` floor for free.
- **Neutral/quiet emphasis.** Skip is an escape hatch, not a primary action. ⚠ Do **not** give it the
  gold/primary face — canon reserves gold for *accents and content*, never as default chrome
  (Grok-02 §4.2), and a loud Skip invites accidental tutorial loss.
- **Anchor top-centre**, horizontally centred. Top-middle is genuinely free in both captures.
- **Clear the HUD-safe bands.** ⚠ Do not let it drift into the `TargetInfo` band — `DialogueView`
  already treats `y=0.660` as the ceiling of its safe area (`DialogueView.cs:664-671`). Sit **above**
  that, and confirm against the objective strip, which is also top-anchored in the FTUE.
- **One construction, both contexts.** It appears in build mode *and* in town dialogue; it must be the
  same widget, positioned by the same rule. Two copies is how the two different broken renders happened.

## 3. Do NOT

- Do not hand-roll a button — that is what produced this defect (Grok-02 §6: *"screens still
  hand-rolling tabs/wallets/slots = non-conformant"*)
- Do not restyle `ElarionUiKit` itself; this is a **caller** fix
- Do not change what Skip *does* — scope is chrome + placement only
- Do not place it where it can be hit while reaching for Confirm in build mode. ⚠ Skipping the tutorial
  by accident is unrecoverable-feeling for a new player; distance from Confirm is a **safety** property,
  not a layout preference

## 4. Acceptance criteria

- [ ] Skip is built by `ElarionUiKit.BuildObsidianButton` — no bespoke button construction remains
- [ ] Anchored **top-middle**, horizontally centred
- [ ] **Zero overlap** in build mode (confirm/rotate/cancel rail) and in town (`Echoes n/6` chip) — the
      two captures that prompted this
- [ ] Meets `ElarionUiKit.MinTouchPx` (112) — it is currently far below the floor
- [ ] Readable in **greyscale**; emphasis carried by frame + position, not hue (colourblind law)
- [ ] Not adjacent to Confirm in build mode (§3)
- [ ] Verified at **2670x1200**, the Seeker's real surface — ⚠ the capture harness was geometry-blind
      until `7e05e6d3`; a resolution in a PNG filename was a *label*, not a layout

## 5. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. `UI_CAPTURE_OK` — **open the PNGs** in both contexts (memory
   `headless-screenshot-verify-ui-before-build`)
3. Device screenshot, landscape
4. Owner felt-verifies + closes (§13)
