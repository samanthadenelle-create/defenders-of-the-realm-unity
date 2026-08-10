# WORK ORDER 1010 — RESULT (P1 + P2 delivered; P3 + tester re-test OPEN)

**Date:** 2026-08-09  **Seat:** CLI  **Branch:** `wip/village2-and-f8-tickets` (local, NOT pushed)
**Status:** **PARTIALLY COMPLETE — do NOT close.** P1 and P2 are implemented and gate-verified.
P3, the card-layout pass against the Direction B mockup, and the external-tester re-test are OPEN.
The WO closes when the testers who raised it say "I understood it without help" — not before.

---

## 1. Commits (in order)

| Commit | What |
|---|---|
| `2b6f30ad` | P1 — chips on the ghost replace the four word-buttons |
| `7999e3cb` | P1 — three defects the screenshots found + the capture case that found them |
| `dc23e4b5` | P2 — `^ Buildings (n)` restore tab |
| `c44a735a` | P2 — collapsed-dock capture; tab moved off the wallet chips |
| `25c2d2f8` | Unaffordable cards say `NEED`; capture hydrates the catalog |
| `080dec16` | Card-tray safe-area inset; capture stops photographing a stale canvas |
| `de8052b8` | `BuildCardArtRegression` — the card-art gate |
| `4adba9b1` | Owner's stockpile + wall portraits, aliased (15/29 → 21/29) |
| `7486f566` | `fountain_healing` → `healing_caravan` + Crystal Mine / Sky Ballista / Healing Caravan art (24/29) |
| `5388e6cf` | `workshop` displays **Weaponsmith** (+ art alias so the rename does not orphan its portrait) |
| `5fd60e1b` | `CoreCatalogRegression` no longer leaks its probe into the shared static registry |

**Gates:** `COMPILE_GATE_OK` · `REGRESSION_OK 132/132 suites` · `UI_CAPTURE_OK 62` /
`UI_CAPTURE_FIDELITY_OK` — and the PNGs were opened, not just counted.

---

## 2. Acceptance criteria — honest status

| Criterion | Status |
|---|---|
| Carousel with category tabs, swipeable, selection readable without colour | **PRE-EXISTING** (`BuildPaletteUI` already had tabs + cards; not rebuilt) |
| Pick a card → minimise to `^ Buildings (n)` tab + ghost with chips + name/cost pill | **DONE** |
| Drag moves ghost; `Rot` steps 90°; `OK` places (refuses-with-reason); `X` cancels; pinch zooms | **DONE** (drag/pinch were already `LeanTouchBuildDriver`) |
| D-pad only via corner toggle | **DONE** — build-owned pad on the Core kit seam, no new reflection bridge |
| Rotate L/R / PLACE / Cancel word-buttons + always-on D-pad GONE | **DONE** |
| Chips stay on-screen and ≥ MinTouch at screen edges / zoomed out | **DONE** — pill+chips clamp as a UNIT (see §3) |
| 5-segment wall run in ~30s without mis-taps | **NOT VERIFIED** — needs a human |
| Footprint validity readable with CVD | **PARTIAL** — the OK chip and pill now carry WORDS; the ghost's own tint/dash shape is untouched |
| First-run hint (2 sessions, dismiss after 3 placements) | **NOT DONE** (P3) |
| `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK` | **DONE** |
| Re-test with the external testers | **NOT DONE — this is what closes the WO** |

---

## 3. NINE defects found by LOOKING AT PIXELS — none by any gate

This is the load-bearing lesson of the ticket. Compile-green and 132 suites said nothing about
any of these; every one is only wrong once drawn.

1. **Chips invisible.** `ElarionUiKit.ObsidianFill` is `(0.02,0.02,0.025)` — black. Chip circles and
   the pill rendered black-on-black; only bare text floated over the field. *Worse* than the
   word-buttons they replaced. Chips follow the ghost over arbitrary terrain and cannot borrow
   contrast from what is behind them → each now carries an accent EDGE.
2. **Pill overflowed** — name+cost wrapped to two lines and spilled outside its own background.
3. **Blocked reason did not fit on a chip** (author error): "Not enough Wood" wrapped to FOUR lines
   and covered its neighbours. A sentence needs the wide surface; a chip has room for a verb.
   Chip says `OK`/`No`; the 620px pill carries the why.
4. **Pill and chips COLLIDED at a screen corner.** Each clamped independently, so each satisfied
   "fully on-screen" while the chips sat on top of the cost text. Two separately-correct clamps,
   one unreadable result → they now clamp as a unit, pill flipping below when there is no room above.
5. **Affordability was COLOUR-ONLY.** `BuildCard` honoured "never colour-alone" for the freebie case —
   the comment above the line even cites that the owner is red/green colourblind — but the
   unaffordable case changed only `Danger` vs `Affordable`. The cost string was byte-identical.
   **The owner could not tell an affordable card from an unaffordable one.** Now reads `NEED 80W 30I`.
6. **Card tray ran to the screen edge**, putting the cost line where a gesture-bar inset clips it first.
7. **The capture itself was LYING.** `BuildModalCanvas` parents at the SCENE ROOT, so destroying the
   host leaked the canvas; canvases accumulated across the three target sizes and the name-scan
   returned target 1's stale, already-COLLAPSED canvas. The 2340 and 2670 "open" shots were
   byte-identical to their own collapsed shots. **The tell — identical file sizes — was visible in the
   listing before the wrong picture was.**
8. **The capture drew nothing.** `CatalogRegistry` is filled by a RUNTIME bootstrap that never runs in
   edit mode, so the first palette capture photographed "No buildables registered" and reported green.
9. **Renaming a label nearly deleted art.** `workshop` owns no portrait; its art resolved purely through
   the display-name slug `"Forge"` → `forge.jpg`. The Weaponsmith rename would have silently turned a
   fully-illustrated card into a letter glyph. **A portrait must never hang off a label creative can change.**

---

## 4. New permanent guards

- **`BuildCardArtRegression`** — every shipped catalog row must resolve real art via
  `BuildPaletteUI.ResolveEntryArtPublic` (the resolver the game uses, NOT a filename guess).
  Ratcheted: today's artless ids are recorded debt, any NEW one fails. Pass line refuses to flatter:
  *"OK (WITH RECORDED DEBT) — 24 of 29 … This is NOT a clean shop."*
  It reads the **canonical file**, not the shared static registry, so its verdict cannot depend on
  suite order.
- **`CoreCatalogRegression`** now snapshots/restores the registry and **asserts** its probe is gone.

---

## 5. Corrections the author owes the record

- I told the owner `mine_crystal`, `mill`, `tower_siege_tower`, `gate_stone` were "visible in game
  today". **All four are filtered out by `BuildCategoryRegistry.LockedIds`** — WO-707 retired
  `mine_crystal` (mining = world nodes) and `mill` (Farm is the food producer); Defense locks
  `gate_stone` + `tower_siege_tower`. `Crystal_Mines.png` and `Sky_Ballista.png` were generated on
  that bad advice. They are wired and will work if those buildings return.
  **What misled me: display names ≠ ids.** The card reading "Armorer" is id `forge`; "Forge" is id
  `workshop`. The `KnownArtlessIds` comment now states in bold that nothing on it is player-visible
  and that it is a debt ledger, NOT a work queue.
- I claimed the dock chrome was pixel-verified before discovering defect #7 — only the 1920 pair
  actually was.

---

## 6. Still open

- **P3:** first-run hint, edge-clamp refinements, optional twist-rotate, `[Flow:BuildHud]`-driven tuning.
- **Card layout** vs the Direction B mockup (sizing / lock-reason text) — geometry and legibility were
  verified; aesthetics were not.
- **`Iron_Wall.png` has no `wall_iron` catalog row** and cannot appear until one is authored.
- **The "explicit pivot"** (owner, 2026-08-09): un-retiring `mine_crystal` / `tower_siege_tower`,
  and/or `FeatureFlags.WallsTab` — UNSPECIFIED, awaiting the owner. Reversing WO-707 more broadly is
  a design decision, not a CLI call.
- **The external-tester re-test.** Nothing in this file substitutes for it.

---

## 7. ADDENDUM — 2026-08-09 evening defect-pass wave (CLI; every claim capture- or source-verified)

Gates on the combined tree: `COMPILE_GATE_OK` (zero `error CS`) + `REGRESSION_OK 133/133 suites` +
`UI_CAPTURE_OK 62` / `UI_CAPTURE_FIDELITY_OK 44` — and the PNGs were OPENED and judged side-by-side
against `UI_REVIEW/build_ui_target_wireframe.html` (the owner re-pinned it as the expectation tonight).

- **D17 CLOSED.** `element/check` + `element/rotate` sprites authored in the pack's own style
  (gold 232,158,0, cross-matched stroke) beside `element/cross`; `RpgUiCatalog.ElementCheck/Rotate`
  constants added; the rail now renders check / rotate-arrow / cross SPRITES in discs (capture-proven).
  Sprite-path invalid state = dim (alpha 0.35) + disabled, worded reason stays on the pill.
- **D19 seating CLOSED.** The palette dock consumes `BuildHudController.ResourceStripReservedPx`;
  the strip no longer overprints the card costs (capture-proven).
- **D5/D6/D12 residual CLOSED.** The WO-683 always-on touch D-pad is retired from
  `LeanTouchBuildDriver` (its `HudMoveInput` reflection seam deleted with it — a §10-positive);
  the ONE nudge control is the HUD's auto-showing analog stick, state-gated.
- **D16 stopgap SHIPPED.** ONE banner-integrated skip (per-step wins; skip-all behind the kit
  confirm); the floating corner "Skip Tutorial" is gone, clearing the D10 corner. The full tutorial
  redesign is **WO-1012** (UI seat, minted tonight) — deliberately not started in this pass.
- **P3 hint line SHIPPED** with the spec's first-run gate (2 sessions / 3 placements, PlayerPrefs).
- **PICK band-tightening SHIPPED** (the owner's "This screen is not correct" F8): header band
  collapsed, dock 540 -> 410, tabs packed adjacent, balance beside the tabs (capture-proven).
- Follow-on tickets minted: **WO-941** (pre-existing RumorBoard/RealmMap `UI_GEOMETRY_FAIL x16` —
  attributed to runs BEFORE this wave) and **WO-942** (capture-case gaps: `padon` byte-identical
  to `edgeclamp`; no assertion on the sprite-path dim state).

**Still open on this WO:** D8 (owner ruling — Walls tab vs the 07-13 walls-ship-with-settlement
ruling), the external-tester re-test (the thing that closes it), owner felt-verify of tonight's
screen, and WO-942's runtime check of the dim state.
