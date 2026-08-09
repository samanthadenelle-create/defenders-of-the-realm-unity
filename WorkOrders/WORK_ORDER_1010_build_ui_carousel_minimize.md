# WORK ORDER 1010 — Build-mode UI redesign: "Carousel + minimize" (CoC grammar, chips on the ghost)

**Status:** READY TO IMPLEMENT — **§7 defect pass** (P1/P2 core DELIVERED; owner screenshot review
2026-08-08 logged D1–D9, D1 first)
*(Prior: READY TO IMPLEMENT — owner ruling 2026-08-08: Direction B; first pick was C, reversed to B on
re-read: "in reading B is cleaner easier". B is final.)*
**Minted:** 2026-08-08 (UI seat) — number from `CLI_LANES_WO_NUMBERS.md` banner (bumped 1010 → 1011 in the same edit)
**Lane:** HUD/UI + BuildMode presentation. **No placement/persist/cost/catalog logic changes.**
**Provenance:** REAL TESTER FEEDBACK (2026-08-08, external testers, not the owner): game is fun, but the
build screen was the hardest part — *"couldn't understand it... too hard to use... buttons everywhere."*
Owner directive: emulate Clash of Clans (the WWCD tie-breaker rule), free maximum field real estate for
small pieces. Mockups: `UI_REVIEW/build_ui_redesign_mockups.html` — Direction B, which also matches the
owner's earlier build-HUD ruling verbatim (memory `build-hud-mobile-design`: large carousel,
minimize-on-select, Lean Touch pinch/rotate, bottom-left backup D-pad).
**Depends on / anchors:** `BuildHudController.cs`, `BuildModeController.cs`, `LeanTouchBuildDriver.cs`
(drag/pinch already wired), `BuildPlaceButton.cs`, `BuildCategoryRegistry` / `build-categories.json`
(WO-673 taxonomy: Town / Defenses / Walls), the existing card palette, `ElarionUiKit`.
**Adjacent:** WO-794 (buildmode upgrade verb), WO-1006 (Manage launcher — separate screen, do not couple).

---

## 1. The design (what the player experiences)

### Phase 1 — PICK: the card carousel
Entering build mode shows a **bottom card carousel** over the field (field stays visible behind it):
- **Category tabs above the cards:** Town / Defenses / Walls (reuse `BuildCategoryRegistry` — the WO-673
  taxonomy; no new categories).
- **Large cards** (~118×150 reference px): art, name, cost. Unaffordable/locked cards stay VISIBLE,
  grayed, with the reason as TEXT ("need 400 iron", "unlock: Foundry T2") — never hidden, never
  color-alone.
- Horizontal swipe scrolls the row; the selected card is highlighted by border + label (not tint alone).

### Phase 2 — PLACE: minimize-on-select, chips on the ghost
Tapping a card **minimizes the carousel to a corner edge-tab** — `^ Buildings (12)` — and the field is
clear. The ghost appears and the controls live ON it, CoC-style:
- **Ghost + footprint:** valid/invalid read by BOTH tint AND shape (solid vs dashed/broken outline —
  colorblind law).
- **⚠ SUPERSEDED BY D14 (§7, owner 2026-08-08): the three verbs live in a LEAN RIGHT-EDGE RAIL, not
  beside the ghost.** Original spec kept for history — the verbs and their behavior are unchanged, only
  the home moved:
  - `OK` chip (gold) — confirm placement; disabled-with-reason as text when invalid ("Blocked").
  - `Rot` chip — 90° step per tap.
  - `X` chip (red-bordered) — cancel this placement, restore the carousel.
- **Name + cost pill** floats above the ghost ("Arcane Spire - 88 wood, 88 iron, 187 crystals").
- **Gestures (existing `LeanTouchBuildDriver`):** one-finger drag moves the ghost; pinch zooms. (Twist
  rotate MAY be enabled as a bonus; the `Rot` chip is the canonical control.)
- **D-pad becomes an off-by-default TOGGLE:** a small translucent `+` button in the corner summons the
  4-way nudge pad for pixel-precise moves on small pieces (walls); tapping it again hides it. Never
  on-screen otherwise.
- Tapping the minimized `^ Buildings` tab reopens the carousel (cancels the current ghost if one is
  un-placed, with the standard no-charge cancel).
- After a successful PLACE: keep the piece selected-for-another? NO — mirror current behavior (repeat
  placement of the same card is a nice-to-have; do not add new flow rules in this WO).

### RETIRED from the screen
- Rotate Left / Rotate Right / PLACE / Cancel **word-buttons** (the intent bar) — replaced by the chips.
- The **always-on D-pad** — becomes the corner toggle.
- The separate "Placing: <name>" hint pill — folded into the ghost's name+cost pill.
- KEPT unchanged: the slim resource strip (top) and the single **X Done** exit (top-right).

### First-run hint (light)
One line above the carousel on a player's first 2 build sessions: `tap a card, then drag the ghost -
chips confirm / rotate / cancel` (ASCII). Dismisses forever after 3 successful placements. The chips are
self-evident enough that a full coach-mark system is NOT required (that was Direction C's burden).

---

## 2. What this changes in `BuildHudController.cs` (fate table)

| Element today | Fate |
|---|---|
| X Done exit | **KEEP** (unchanged — still the one exit) |
| Resource strip | **KEEP** (slim, top) |
| Palette strip | **REPLACE** with the card carousel + minimize-to-tab (same catalog/category data) |
| "Placing: <name>" hint pill | **FOLD** into the ghost's name+cost pill |
| Rotate Left / Rotate Right buttons | **RETIRE** → `Rot` chip (+ optional twist gesture) |
| PLACE button | **RETIRE** → `OK` chip on the ghost |
| Cancel button | **RETIRE** → `X` chip on the ghost |
| 4-way D-pad | **DEMOTE** to the off-by-default corner toggle |

Moving an EXISTING structure (entered via the current select/move flow) uses the SAME grammar: ghost +
chips + drag. One grammar everywhere.

---

## 3. Constraints (binding)

- **MinTouchPx 112** on every chip (visual ~52px circle sits inside a >=112px hit area — use
  `ClampMinTouch`'s invisible-padding path, not visual growth), card, tab, and toggle.
- **Colorblind law:** validity is shape + text, never tint alone; chip states carry text/shape; locked
  cards say why in words.
- **ASCII only** in TMP strings. Mockup emoji are stand-ins — use the real resource icon sprites with
  numeric text.
- **Code-built uGUI via `ElarionUiKit`** — no UXML (does not work in builds).
- **Fixed-pixel bands** for carousel, cards, chips (the WO-841/852/905 fraction-band lesson). Chips are
  screen-space UI anchored to the ghost's projected position, clamped to the safe area — never world-space
  billboards that can shrink with zoom.
- **MVVM ratchet:** presentation reads a VM; no new reflection bridges.
- **No logic changes:** placement validity, persist, costs, refunds, catalog, category mapping stay
  exactly as they are. This WO moves CONTROLS, not rules.
- **Instrument (§12):** `[Flow:BuildHud]` step lines for carousel-open, card-pick, minimize/restore,
  chip-confirm vs chip-cancel, rotate (chip vs gesture), D-pad-toggle use — headless runs prove which
  paths real players use.

---

## 4. Acceptance criteria

- [ ] Build mode opens with the category-tabbed card carousel (art + cost + affordability + lock reasons
      as text); swipeable; selection readable without color.
- [ ] Picking a card minimizes the carousel to the `^ Buildings (n)` tab and spawns the ghost with the
      three chips + name/cost pill. On-screen element count during placement: **6** (strip, X Done, tab,
      ghost pill, chip cluster, D-pad toggle).
- [ ] Drag moves the ghost; `Rot` chip steps 90°; `OK` places (disabled-with-reason on invalid spots);
      `X` cancels back to the carousel; pinch zooms.
- [ ] The D-pad appears ONLY via the corner toggle and hides again on tap.
- [ ] Rotate Left/Right/PLACE/Cancel word-buttons and the always-on D-pad are GONE from the player screen.
- [ ] Chips stay fully on-screen (and >=MinTouch) when the ghost is at a screen edge or the camera is
      zoomed far out.
- [ ] Small-piece test: place a 5-segment WALL run using drag + (toggle) nudge in under ~30s without
      mis-taps.
- [ ] Footprint validity readable with color vision deficiency (shape/dash + text, verified in capture).
- [ ] First-run hint shows on the first 2 sessions, dismisses forever after 3 successful placements.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (open the PNGs: carousel open,
      minimized+ghost+chips, invalid-spot state, D-pad toggled on, edge-clamped chips).
- [ ] **Re-test with the external testers** — the loop that triggered this WO closes with the same people:
      target verdict "I understood it without help."

---

## 5. What NOT to touch

- Placement/persist/validity logic, costs, refunds, catalog data, category taxonomy.
- The Manage screen (WO-1006) and the bottom action bar — separate surfaces.
- Camera rig beyond what `LeanTouchBuildDriver` already does for pinch.
- Scene files (no hand-edits — everything is code-built at runtime).
- Do NOT add repeat-placement / multi-place flows — grammar only, no new rules.

---

## 7. FELT-TEST DEFECTS — owner screenshot review, 2026-08-08 (fix before tester re-test)

Screenshot: build mode, tutorial "Place the Echo Hollow" step, Development Build. The core grammar
LANDED (chips + name pill on the ghost, `^ Buildings (11)` minimize tab, clean field, strip + X Done).
Five defects against this spec, in priority order:

- **D1 — an "Orient" WORD-BUTTON exists and CLIPS the Echoes counter.** A large obsidian "Orient"
  button sits mid-right — it is NOT in this spec (§2 retires rotation word-buttons; the `Rot` chip is
  the one rotate control) and it half-covers the Echoes chip so the screen reads "hoes 1/6".
  **Remove the Orient button entirely** (if it is the old Rotate bar's survivor or a new alias, it goes
  either way — one rotate control, on the ghost). The Echoes chip must be fully visible again.
- **D2 — tutorial banner overprints the F8 capture box.** "Place the Echo Hollow anywhere you like
  (0/1)" and the dev "What looks wrong? (Enter = save...)" input render on top of each other — garbled
  double-text at top-center. The F8 harness box is dev-only, but the collision is a real z/anchor bug:
  give the tutorial banner and the F8 overlay disjoint bands (tutorial banner lower, or F8 box moved),
  ASCII rule check while in there.
- **D3 — chip cluster sits ON the ghost, not BESIDE it.** OK/Rot/X overlap the green ghost art
  (spec §1 phase 2: cluster beside the ghost, repositioned to stay clear). Offset the cluster to the
  ghost's flank (screen-space, edge-clamped) so the piece being placed is never hidden by its own
  controls — the whole point of the redesign is seeing the piece.
- **D4 — chips look under-sized; verify MinTouch.** Visual chips are fine small, but confirm the
  ClampMinTouch invisible hit-pad path is active on OK/Rot/X (>=112px hit area). If the pad path was
  skipped they are mis-tap magnets — the exact tester complaint.
- **D5 — D-pad visible by default (verify).** Both the D-pad AND its `+` toggle are on screen during a
  fresh tutorial placement. Spec §2: D-pad appears ONLY after the toggle is tapped, hidden otherwise.
  If the capture shows the untoggled default, the default is wrong. (If the owner had tapped `+` first,
  ignore — but then the toggle should read as active/pressed, which it does not.)
- *(Nit, decide-later)* "Skip Tutorial" floats detached under X Done AND the banner has its own
  "Skip >" — two skip affordances. Tutorial chrome is not this WO's surface, but flag it to the
  tutorial owner: one skip.

**Second screenshot (same session — the carousel PICK phase).** The carousel LANDED: category tabs with
selection underline, art cards with names + price labels, crystals readout. Additional findings:

- **D6 — the D-pad + `+` toggle OVERPRINT the carousel and the first card.** The pad renders on top of
  the open panel, half-covering the Echo Hollow card. Confirms D5 (pad is on when it should not exist in
  the PICK phase at all — there is no ghost to nudge yet): the pad must be hidden whenever the carousel
  is open AND hidden until toggled during placement.
- **D7 — the Echoes 1/6 chip overlaps the carousel panel's top-right corner.** Same class as D1: HUD
  chips and the build chrome need disjoint reserved zones (the panel should claim its rect and the
  overlay chips yield, or vice versa — never both drawing in the same band).
- **D8 — "Walls" category tab is MISSING.** Spec §1 phase 1 and the WO-673 taxonomy: Town / Defenses /
  **Walls**. Only Town + Defenses render. If Walls was deliberately folded into Defenses, that
  contradicts the ruled taxonomy (walls split out — claimed-outpost wall canon) — restore the third tab.
- **D9 — every card reads "FREE" — VERIFY, not assumed-bug.** All six visible cards (Echo Hollow,
  Weaponsmith, Store, Armorer, Cathedral of Magic, Lumberyard) show FREE. If this is the
  first-build-free rule (`FreeBuildsUsed` / `firstBuildSeconds`) rendering correctly for never-built
  structures, fine — but then the label should say "First build FREE" so it reads as a rule, not a
  pricing bug; and a card the player CANNOT afford must never read FREE. Confirm against
  `BuildTimerConfig`/economy at source.
- *(Cosmetic)* The panel's top band (Crystals row) is ~1/3 empty dark space before the tab row, and the
  Town/Defenses tabs sit far apart with a large centered gap — tighten the bands when in there (fixed-px
  bands, §3).

**Third screenshot (owner-annotated markup, same session).** Three OWNER RULINGS drawn on the capture
plus one new defect class:

- **D10 (owner ruling, verbatim "Move to Corner Remove the X, Size smaller and more minized"):** the
  big `X Done` button becomes a COMPACT corner control — smaller, tucked into the true corner, label
  `Done` (drop the "X" glyph). Keep MinTouch on the hit area (visual can shrink; the invisible pad
  carries the 112px).
- **D11 (owner ruling, verbatim "Remove Fully" → the dev `Flag` button):** remove the on-screen F8
  Flag BUTTON from build mode entirely. ⚠ Scope: the BUTTON only — the F8 capture harness, hotkey and
  break-log stay (§12/§14: instrumentation is permanent; this is UI chrome, not instrumentation).
- **D12 (owner ruling, clarified 2026-08-08: "remove this and replace with virtual Dpad"):** the fixed
  4-arrow button pad is REMOVED and replaced with a **virtual D-pad/joystick** — the same analog-stick
  style control the gameplay HUD already uses (reuse that component; do not write a second stick).
  It drives the fine-nudge of the ghost. D5/D6 still apply on top: it exists ONLY in the PLACE phase,
  hidden until summoned by the toggle, never during PICK, never overlapping the carousel.
- **D13 — Defenses-tab card previews render as RAW 3D MODELS spilled over the world.** Ballista /
  Arcane Spire / a giant tree render unclipped at world scale over the field and panel (no obsidian
  card frames), with their cost text ("NEED 60W 30I 70C") floating loose. The Archer Tower card alone
  renders as a proper dark card. Likely the card preview path (RenderTexture or model-in-UI parenting)
  failing for all but the first card — §12: instrument the card-build path and read the trace before
  fixing. Also stray prop models scatter across the bottom band — probably the same spill.

**THE VISUAL BENCHMARK (owner re-affirmed 2026-08-08 by holding the mockup up against the build —
verdict on the delivered screen: "does not match"):** the PLACE-phase frame in
`UI_REVIEW/build_ui_redesign_mockups.html` is the cleanliness bar — *"this needs to be clean"* — with
ONE owner amendment on top:

- **D14 (owner ruling 2026-08-08, verbatim "i want a lean section on right"): the placement controls
  live in a LEAN RIGHT-EDGE RAIL, not floating beside the ghost.** A slim fixed vertical column hugging
  the right edge (right-thumb territory in landscape): confirm `OK`, `Rot`, cancel `X`, stacked with
  gutters, MinTouch hit areas, compact visuals. The ghost carries ONLY its name+cost pill; no chrome
  ever sits on or beside the piece. This SUPERSEDES D3's "flank the ghost" wording — same three verbs,
  new fixed home. The compact corner Done (D10) sits above the rail's top; the rail must clear the
  Echoes chip's reserved zone (D7).

Concretely, the delivered screen passes when: the right edge holds the lean rail (OK/Rot/X), the close
control is a SMALL ROUND corner button (D10), ONE minimized tab sits at the bottom, ONE thin hint line,
and NOTHING else floats on the field — no D-pad (until summoned, D12 virtual stick), no Flag (D11), no
Orient (D1), no second skip. Compare the capture side by side before calling the defect pass done.

Re-gate after fixes: `UI_CAPTURE_OK` on BOTH phases — PICK (carousel open: no D-pad, no Flag button,
compact corner Done, Echoes chip clear of the panel, three category tabs, every card framed with sane
preview scale, price labels verified) and PLACE (banner clear of F8 box, no Orient button, chips
flanking the ghost, Echoes chip whole, virtual D-pad absent until toggled) — each judged against the
mockup benchmark above.

---

## 8. Phasing (suggested — each phase ships behind the standard gates)

1. **P1 — chips replace the intent bar:** ghost chips (`OK`/`Rot`/`X`) + name/cost pill; retire the four
   word-buttons; D-pad becomes the toggle. Carousel untouched. (Biggest tester win, smallest slice.)
2. **P2 — carousel + minimize:** replace the palette strip with the card carousel, category tabs, and the
   minimize-to-tab behavior.
3. **P3 — polish:** first-run hint, edge-clamping refinements, optional twist-rotate gesture, capture-driven
   tuning from the `[Flow:BuildHud]` data.
