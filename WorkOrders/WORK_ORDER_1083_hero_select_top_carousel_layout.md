# WORK ORDER 1083 — Hero Select: top rotating carousel, details below (layout rebuild)

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Minted:** 2026-08-26, UI-seat banner block (bumped 1083 -> 1084 in the same edit)
**Silo:** Onboarding / UI layout
**Owner rulings (2026-08-26, this session):** *"redo it and make it clear, it should be a rotaing
carosel"* -> *"no make the top part the carosel and below it the details and specs"* -> mockup v2
approved (**"yes"**).
**Provenance:** `tmp/HANDOFF_TO_UI_hero_select_layout.md` (CLI courier handoff, 2026-08-26).
**Approved design:** `WorkOrders/WORK_ORDER_1083_mockup_2670x1200.png` (also at
`tmp/heroselect_mockup_2670x1200.png`; generator script preserved this session).

---

## 1. Evidence

- **Defect capture:** `tmp/heroselect2-104958.png` — Seeker device, 2670x1200, build
  `2026.08.26.341419` (⚠ built 08-25 21:24; fixes landed 21:52 are NOT in it — re-verify any
  defect against the tree before assuming unfixed).
- **Correct-Cleric capture:** `tmp/screen-105319.png` (same session, Elara selected).

Observed defects, all layout, none logic:

1. `Choose Sylas` CTA overlaps `NEXT`.
2. CTA clips the skill-badge stack at ~x1180, y440-500.
3. `PREV` overlaps the Knight card.
4. `NEXT` overlaps the Cleric card.
5. Bottom ~35% of the panel is empty while everything crams into the top half.
6. Minor: the `Wallet CHKK...sfkC` chip draws over the panel's top-right frame border.

## 2. The approved design (normative)

**Top band = the rotating carousel. Below it = details + specs. CTA alone at the bottom.**
Open the mockup PNG — it is the diff target for the acceptance screenshot.

Structure, top to bottom:

- **Header:** title centered; wallet chip INSIDE the header band, clear of the frame border.
- **Carousel band** (upper ~55% of the body): focal card large, gold-framed, centered on the
  screen axis; previous/next heroes flank it as SMALLER, DIMMED cards (depth = the rotation cue);
  PREV/NEXT arrow buttons outboard of the side cards, vertically centered on them; the locked
  hero's side/focal card carries a **SOON** word-ribbon (word, never colour alone). Wrap-around
  rotation (4 heroes, 4 dots); swipe, arrow tap, and side-card tap each rotate one step (all
  three inputs already exist in `HeroSelectController` — preserve them).
- **Under the focal card:** role label ("Wood Warden"), then the page-dot rail. Active dot is
  LARGER and gilt (size + colour, greyscale-safe).
- **Divider rule**, then the **details strip** in four columns across the full width:
  **LORE** | **STATS** (HP/ATTACK/SPEED pip rows) | **SIGNATURE** | **PRIMARY SKILLS**
  (slot badge + name rows). Content identical to today's specs panel — only the geometry moves.
- **CTA band:** `Choose <Hero>` / `Coming Soon`, centered, in an **exclusive** bottom band no
  other element may enter.

### Normative anchor rects — fractions of the 2670x1200 SCREEN (x left->right, y BOTTOM->top)

Derived from the approved mockup. The implementer maps these into body-well fractions of the
FrameCore chrome (`_chrome.layout.body`) so the same proportions hold; at 2670x1200 the result
must diff clean against the mockup.

| Element                    | xMin  | yMin  | xMax  | yMax  | px @2670x1200 (top-down y) |
|----------------------------|-------|-------|-------|-------|-----------------------------|
| Wallet chip                | 0.828 | 0.910 | 0.959 | 0.972 | x 2210-2560, y 34-108 |
| Focal card                 | 0.444 | 0.467 | 0.556 | 0.838 | x 1186-1484, y 195-640 |
| Side card LEFT             | 0.292 | 0.519 | 0.371 | 0.785 | x 780-990, y 258-577 |
| Side card RIGHT            | 0.629 | 0.519 | 0.708 | 0.785 | x 1680-1890, y 258-577 |
| PREV button                | 0.210 | 0.593 | 0.253 | 0.710 | x 560-676, y 348-488 |
| NEXT button                | 0.747 | 0.593 | 0.790 | 0.710 | x 1994-2110, y 348-488 |
| Role label band            | 0.400 | 0.415 | 0.600 | 0.455 | y 655-702, centered |
| Page-dot rail              | 0.440 | 0.381 | 0.560 | 0.409 | y 709-743, centered |
| Divider rule               | 0.049 | 0.368 | 0.951 | 0.368 | y 758 |
| Details col 1 LORE         | 0.049 | 0.175 | 0.330 | 0.348 | x 130-880, y 782-990 |
| Details col 2 STATS        | 0.348 | 0.175 | 0.622 | 0.348 | x 930-1660 (pips from 0.442) |
| Details col 3 SIGNATURE    | 0.640 | 0.175 | 0.764 | 0.348 | x 1710-2040 |
| Details col 4 SKILLS       | 0.783 | 0.155 | 0.951 | 0.348 | x 2090-2540 |
| CTA (exclusive band)       | 0.369 | 0.072 | 0.631 | 0.167 | x 985-1685, y 1000-1114 |

Aspect rule: fractions bind at the Seeker's 2670x1200 (⭐ never rendered here before `7e05e6d3`
— defects at this aspect are the norm). At other aspects the bands scale, but the invariants in
§4 (no-overlap, exclusive CTA band, MinTouchPx without collision) always bind.

## 3. ⛔ Cleric / Elara — DO NOT TOUCH

Verified correct as shipped (`tmp/screen-105319.png`): CTA reads "Coming Soon", detail reads
"Abilities revealed at launch", stats/lore still render. Owner (*"we dont use cleric" / "its a
one day thing"*) was DESCRIBING this behaviour, not ruling a change; she approved the 4-card
mockup. **Four cards and four dots is intended.** Spec nothing against: the roster
(`PlayableHeroes.cs:55-58`), the coercion (`GameStateService.cs:844`), Elara's art or enum
member. This section exists so nobody "fixes" a working screen. (History: an earlier handoff
draft invented a 3-card ruling from prose; the owner stopped it with *"look at screen / dont
guess"*. Closed — do not re-open.)

## 4. Binding constraints

- **`MinTouchPx = 112`** on every interactive element, on both axes — AND satisfying it may not
  create an overlap: bands are sized so no clamp-growth can collide (that is what broke the
  shipped screen). ⛔ Do NOT name `ClampMinTouch` as a cause — ruled out at three sites
  (measured 117 / 116.7-130.6 / 112.0 px). Check band arithmetic first.
- **ASCII-only TMP strings** (tofu on device otherwise). The arrow glyphs `<` `>` are ASCII.
- **Never meaning by colour alone** (owner is red/green colourblind): locked = the SOON word;
  active dot = larger AND gilt; focal card = gold frame AND scale. Must survive greyscale.
- **Code-built uGUI via `ElarionUiKit` only. NO UXML/UIDocument** — project law.
- **Presentation is a separate layer** — geometry only; never reach into hero/roster objects.
- **Preserve the controller contract exactly:** `OnDiveVillageClicked` routing
  (FoundingChoiceController -> GoCastle / BypassPetSelect hatch), `ChooseHero` pre-persist,
  returning-player skip, playable-set from `PlayableHeroes`, the BuildScreen VERIFY FlowTrace.
  §12: instrumentation stays; add `FlowTrace.Step` lines for the new band build, never remove.

## 5. Files

- **Edit:** `Assets/_Modules/Onboarding/HeroSelectController.cs` — `BuildScreen` /
  `BuildCarousel` / `BuildPreviewCard` / `BuildCenterStage` / `BuildSpecsPanel` geometry only
  (the specs panel becomes the four-column details strip; content and data sources unchanged).
- **Locate + edit (defect 6):** the wallet-chip builder — not identified in this pass; find the
  component that renders `Wallet <addr>` on this screen and re-anchor it inside the header band
  per §2. Do not assert its file from this WO; read it first.
- **Do NOT touch:** `PlayableHeroes.cs`, `GameStateService.cs`, `HeroCatalog.cs` data,
  routing/persistence code paths, any `.unity` scene by hand.

## 6. Acceptance criteria

1. `COMPILE_GATE_OK` + regression markers on fresh logs (marker-judged, never exit codes).
2. A **DEVICE screenshot at 2670x1200, opened and looked at**, diffed against
   `WORK_ORDER_1083_mockup_2670x1200.png`. ⛔ `UI_CAPTURE_OK` alone is insufficient — it proves
   a panel rendered, not that it looks right (two broken panels shipped behind green markers).
3. In that screenshot, explicitly verify each: CTA touches nothing; PREV/NEXT touch no card;
   skill rows unclipped; wallet chip clear of the frame border; the lower third is occupied;
   all five §1 overlaps gone.
4. Measured touch targets ≥112px both axes (log the numbers, don't eyeball).
5. Greyscale copy of the screenshot still distinguishes locked/active/focal states.
6. Rotation still works by all three inputs (swipe / arrows / side-card tap), wraps across the
   4 heroes, and the Cleric behaviour of §3 is byte-for-byte unchanged.
7. PO (owner) felt-verifies on device and closes — CLI never closes this.

Route: CLI implements, gates, commits; result file per protocol; board regenerated with the flip.
## LANDED-WORK AUDIT (2026-08-26)

Implementation landed in `b303c4fbf` (`HeroSelectController.cs`, `HeroPortraitPaths.cs`, portrait
loaders, and `ArtResourceRegression.cs`). Fresh evidence: `Builds/batch0-compile-2.log:1966`
`COMPILE_GATE_OK`; `Builds/batch0-regression-2.log:83492` `ART RESOURCES OK`; and
`:83814` `REGRESSION_OK 291/291`. **Post-FIXED APK checklist:** the 2670x1200 device screenshot,
visual comparison against the approved mockup, explicit collision checks, greyscale check, and owner close.
