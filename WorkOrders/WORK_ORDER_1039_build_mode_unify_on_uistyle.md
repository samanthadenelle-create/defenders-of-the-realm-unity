# WORK ORDER 1039 — Build mode looks disjointed: 4 builders each decide their own chrome. `UiStyle` exists and they ignore it

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1039 → 1040 in the same edit
**Lane:** UI presentation, build-mode surfaces. No gameplay behaviour changes.
**Provenance:** owner 2026-08-16 — *"should we style all of these with one unified style? Looks good but
disjoined"*, with the build-mode screenshot (Archer Tower shortfall banner, Skip Tutorial, category
column, confirm/rotate/cancel rail, objective strip, wallet row, Done).

---

## 1. The answer: YES — and it is already your ruling, already specced, already half-built

`docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` **§6** records the owner directive verbatim:

> *"make a styling-type **SINGLETON for ONE UI style for EVERYTHING** — not piece this and piece that."*

§6 is a full design: `UiStyle` as the single authority (§6.2), before/after call-site examples (§6.3),
a phased non-breaking migration (§6.4), the swappable `UiTheme` record (§6.5), the **full offender
roster** (§6.6), and `UiStyle.Try(Style.Obsidian)` as the one-lever reskin (§6.7).

**Status measured at source, 2026-08-16:**

| §6 phase | state |
|---|---|
| (a) introduce `UiStyle` + `UiTheme` | ✅ **LANDED** — `Assets/_Modules/Core/UI/UiStyle.cs` exists |
| (b) route `ElarionUiKit` through it | ✅ partially — kit files reference it |
| **(c) migrate panels to semantic tokens** | ❌ **NEVER DONE for build mode** |
| (d) delete dead literals / fold `ShopTheme` | ❌ `ShopTheme.cs` still present — the duplicate palette §6.1 named |

**Only 10 files in all of `_Modules` reference `UiStyle.`** And `BuildMenu.cs` — one of the surfaces in
the screenshot — has **zero**.

**So the disjointedness the owner is seeing is not a new problem to solve. It is phase (c), unstarted
on this screen.** The authority was built and the callers never adopted it.

## 2. Why this screen reads disjointed — four independent chrome decisions

Every element in that screenshot is built by a different file, each choosing its own plate treatment:

| element | builder |
|---|---|
| objective strip (*"Work takes time…"* + dot pips) | `Core/UI/ObjectiveBannerUi.cs` |
| **Skip Tutorial** | `Core/UI/TutorialSkipUi.cs` |
| build HUD shell / rail / wallet | `Village/BuildMode/BuildHudController.cs` |
| category column (Town / Defense / Castle Structures) | `Village/Buildings/UI/BuildMenu.cs` — **0 `UiStyle` refs** |

The result is visible in one frame: the category buttons are flat grey slabs with no ornate frame,
while the objective strip, wallet row and rail wear black-and-gold plates; the shortfall banner has a
gold rim; Done is a small gold disc. **Three or four different plate languages on one screen.** Nothing
is individually wrong — which is exactly why the owner's read is *"looks good but disjoined."*

## 3. Scope — THIS SCREEN ONLY

⚠ **§6.6 lists every screen in the game as an offender. Do NOT migrate them all here.** Per
`docs/ARCHITECTURE_PRINCIPLES.md` (HP B2B): bounded context, scope deliberately limited, and **never
smuggle a structural refactor into player-facing work**. This ticket is **one increment of phase (c)**,
on the screen the owner flagged.

**In scope:** the four builders in §2 read their frame / plate / button / state colours from `UiStyle.*`
instead of local literals.

**Out of scope:** every other screen (file a follow-up increment per screen), phase (d)'s `ShopTheme`
fold, and any change to `UiStyle`'s API.

⚠ **If a token is missing from `UiStyle`, ADD IT there — do not add a literal back at the call site.**
A call site that "just this once" hardcodes a colour is how the authority erodes; §6.1 documented ~12
independent decision sites before `UiStyle` existed, and this is how it returns to that.

## 4. Constraints

- **The frame is the chrome** (`UI_BLINK_TEMPLATE_CANON.md` §0) — surfaces drop chrome-less content into
  kit-built plates; they do not paint their own
- **Gold is for accents and content, never default chrome** (Grok-02 §4.2). ⚠ Unifying must not mean
  gilding everything — the category column being quieter than the wallet row may be **correct
  hierarchy**. Unify the *language*, not the emphasis
- **Colourblind law** — every state legible in greyscale. ⚠ Build affordability is an **open colour-only
  defect** (anchor 2026-08-09: *"the build placement ghost (valid/invalid on the red/green axis)"*), and
  it lives on this screen. This migration is the natural moment to give it a shape/text channel
- **Coordinate with in-flight tickets on the same screen:** **WO-1033** (Skip → `BuildObsidianButton`,
  top-middle — ✅ appears to have landed, it is top-middle in this capture), **WO-1034** (place-then-rotate
  tooltip), **WO-1037** (shortfall pack offer, which attaches to the *"Not enough Wood (90)"* banner).
  ⚠ All four touch build mode — **sequence them, do not run them as parallel lanes on the same files**

## 5. Acceptance criteria

- [ ] All four §2 builders take frame / plate / button / state colour from `UiStyle.*`
- [ ] **Zero chrome literals remain** in those four files — grep proves it
- [ ] Any token added went into `UiStyle`, not a call site (§3)
- [ ] The screen reads as **one system** in a single capture — same plate language across the category
      column, strip, rail, wallet and banner
- [ ] Deliberate hierarchy preserved — unification is not flattening (§4)
- [ ] Greyscale pass: every state distinguishable
- [ ] No gameplay/behaviour change — presentation only
- [ ] Verified at **2670x1200**, the Seeker's real surface

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. `UI_CAPTURE_OK` — **open the PNGs**; a before/after pair of the whole screen is the actual evidence
   here, since "disjointed" is a whole-frame property no per-widget check can see
3. Owner felt-verifies: *"does this read as one game now?"* + closes (§13)

## 7. Finding for the board (do not action here)

`UiStyle` adoption is **10 files** across `_Modules`, and `ShopTheme.cs` still duplicates the palette
§6.1 flagged. Phase (c) is unstarted almost everywhere and phase (d) never began. ⚠ **Each remaining
screen should be its own small increment** — a single "migrate everything" ticket would be exactly the
structural-refactor smuggling §3 forbids, and would put every screen in the game in one blast radius.
