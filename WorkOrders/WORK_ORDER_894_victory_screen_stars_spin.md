# WORK ORDER 894 — Victory screen: real spinning stars + exact wireframe layout

**Status:** READY TO IMPLEMENT · **Silo:** UI / EndState · **For:** CLAUDE CLI · **Date:** 2026-08-05
**PO:** Samantha (owner) · **Author:** UI seat
**Binding acceptance:** the built screen must match the **§2 wireframe** and **§3 spacing table** EXACTLY, and the stars must be **real 5-point stars that visibly SPIN in** (§4). No approximations.

## 0. Problem (grounded, verified in source)
`Assets/_Modules/Village/UI/EndState/EndStateView.cs` is the shared victory/defeat/wave-clear screen.
- **The "stars" are NOT stars.** `BuildStarRow` (L738-768) draws **three 45° rotated squares (diamonds)** — a workaround because the TMP star glyph tofu'd. Owner: "make sure we have stars, not just some damn animation."
- **There is NO spin.** Stars only ride the generic staggered fade+scale reveal (`Track` → `RevealRoutine`, L849-892). No rotation. Owner: "how we do the spinning of the stars … an animation that doesn't even work."
- Spacing is band-based (`StarsPx = 48`, L529) and was sized for the diamond, not a hero star.

**This WO replaces `BuildStarRow` with real spinning 5-point stars and locks the whole victory layout to the wireframe.** It does NOT touch the panel-geometry law (header/body/CTA solve) — only the star band size + the star build/animation.

---

## 2. WIREFRAME (full victory modal — landscape, centered on a scrim)

```
        ╔══════════════════════════════════════════════════════╗
        ║                    ◈  (crest medallion, top-center)   ║
        ║                                                      ║
        ║                   V I C T O R Y !                    ║   ① HEADER band — FontTitle 88, 1 line, centered
        ║ ──────────────────────────────────────────────────── ║
        ║                                                      ║
        ║                   ✦  (crest emblem)                  ║   ② EMBLEM band — 64px, centered
        ║                                                      ║
        ║               "The wave is broken."                  ║   ③ SUBTITLE band — 60px/line, centered
        ║                                                      ║
        ║                ★      ★      ★                       ║   ④ STAR ROW band — 72px, 3 real 5-pt stars, SPIN-IN
        ║                                                      ║
        ║                    Time  0:14                        ║   ⑤ TIME band — 48px, centered gilt
        ║                                                      ║
        ║        ┌────────────────────────────────────┐        ║
        ║        │ [◆] Wood                      +15  │        ║   ⑥ SPOILS row — 64px each
        ║        ├────────────────────────────────────┤        ║
        ║        │ [◆] Iron                      +8   │        ║   ⑥ SPOILS row — 64px each
        ║        └────────────────────────────────────┘        ║
        ║                                                      ║   ⑦ guaranteed gap (BandGap 8px + CTA gap)
        ║             ┌────────────────────────────┐           ║
        ║             │          CONTINUE          │           ║   ⑧ CTA — canonical 360×132, footer band
        ║             └────────────────────────────┘           ║
        ╚══════════════════════════════════════════════════════╝
```

**Vertical order top→bottom is FIXED:** ① Header → ② Emblem → ③ Subtitle → ④ Stars → ⑤ Time → ⑥ Spoils(n) → ⑦ gap → ⑧ CTA.
Everything is **horizontally centered** in the panel. Panel width unchanged (0.22–0.78 of screen, WO-433). Panel height auto-solves to content (`PanelHalfHeight`, unchanged).

---

## 3. SPACING TABLE (exact — post-scale reference px, the same space as FontBody=50 / CanonCtaHeight)

| Band | Height (px) | Change | Notes |
|------|-------------|--------|-------|
| Header | anchors 0.820–0.985 of panel | unchanged | FontTitle 88, one line |
| ② Emblem | **64** (`EmblemPx`) | unchanged | crest, preserveAspect, centered 0.38–0.62 x |
| ③ Subtitle | **60/line** (`SubLinePx`) | unchanged | 1–4 wrapped lines |
| **④ Star row** | **72** (`StarsPx` 48 → **72**) | **CHANGED** | seats 56px hero stars + spin headroom |
| ⑤ Time | **48** (`TimePx`) | unchanged | gilt, bold, centered |
| ⑥ Spoils row | **64** (`RowPx`) each | unchanged | plate + 40px icon + label/value |
| Band gap | **8** (`BandGapPx`) | unchanged | between every band |
| ⑧ CTA | **132** (`CanonCtaHeight`) | unchanged | 360×132, seated in reclaimed close band |

**Star row internal layout (exact):**
- 3 stars, **horizontally centered** as a group, **vertically centered** in the 72px band.
- **Star diameter = 56 px** (square bbox 56×56).
- **Center-to-center spacing = 80 px** → star centers at panel-center-x offsets **−80, 0, +80 px** (`anchoredPosition.x`), `anchorMin=anchorMax=(0.5,0.5)`, `pivot=(0.5,0.5)`.
- Group visual width = 56 + 80 + 80 = 216 px; symmetric about center.
- `StarsPx` const 48 → **72**. (This is the only spacing constant that changes.)

---

## 4. THE STARS — real 5-point stars + the SPIN (the point of this WO)

### 4.1 Shape (real stars, never a diamond or a glyph)
- Each star is a **real 5-pointed star**, rendered as a `UnityEngine.UI.Image` with a **committed 5-point-star sprite**.
- **Source the sprite:** first check `RpgUiCatalog` for an existing star sprite; if none, add an editor helper that generates a clean **gold 5-point star** sprite (with a soft 1px bevel/edge) + a **dim outline** variant, committed to `Assets/Resources/UI/` (do NOT rely on a TMP glyph — it tofu's on the build font; do NOT use a rotated square).
- **Earned star:** gold fill (`ElarionUiKit` gold / `ObsidianTrim` gold family), full alpha.
- **Unearned star:** the dim/outline variant at ~14% alpha. Unearned stars **do not spin** — they fade in at their slot over 0.2s.
- `raycastTarget = false` on all stars.

### 4.2 The SPIN-IN (each EARNED star) — exact, unscaled time
Runs on `Time.unscaledDeltaTime` (screen never pauses — same rule as `RevealRoutine`).

| Property | From → To | Duration | Easing |
|----------|-----------|----------|--------|
| **Rotation (Z)** | **+540° → 0°** (1.5 clockwise turns — an unmistakable spin) | 0.40 s | ease-out-cubic |
| **Scale** | **0.0 → 1.15 → 1.0** (overshoot pop) | 0.40 s | ease-out-back |
| **Alpha** | 0 → 1 | first 0.12 s | linear |

- **Stagger:** star *i* starts at `starsBaseDelay + i × 0.15 s` (left→right, one after another).
- **Land pulse:** on completion, a quick **scale 1.0 → 1.08 → 1.0 over 0.12 s** stamp (so the star "lands" with weight).
- **Optional (recommended) land sparkle:** a one-shot `Vfx`/kit flash at the star center on land — nice-to-have, not required for acceptance.
- **Idle after landing:** a **subtle twinkle** — ±3% scale sine at ~0.5 Hz (or a soft alpha shimmer). **NOT a continuous full spin** (a forever-spinning rating star reads as "loading"). Idle twinkle is required; keep it gentle.

### 4.3 Sequence placement
The star band's reveal is beat ④ in the panel's staggered reveal. Its `starsBaseDelay` sits between the subtitle (③) and time (⑤) beats so the spin reads as its own moment — subtitle settles, THEN the stars spin in one-by-one, THEN time/spoils continue.

### 4.4 Implementation notes
- Replace the body of `BuildStarRow` (L738-768): build 3 star Images at the exact §3 positions; give each earned star its own spin coroutine (do NOT route earned stars through the generic `Track`/`RevealRoutine` — they need the rotation curve). Unearned stars may use the plain fade.
- Bump `StarsPx` 48 → 72 (L529).
- Keep it self-contained in `EndStateView` (the tween pattern already lives here; §comment already flags it a kit-promotion candidate).

---

## 5. Files to touch
- `Assets/_Modules/Village/UI/EndState/EndStateView.cs` — `BuildStarRow` (rebuild), `StarsPx` const (48→72), a new star-spin coroutine.
- `RpgUiCatalog` / a new editor helper — the committed 5-point-star sprite (gold + dim), if the kit lacks one → `Assets/Resources/UI/`.
- (No change to the panel-geometry law, the CTA, or the reveal of other bands.)

## 6. ACCEPTANCE CRITERIA (must match the wireframe EXACTLY)
**Layout / spacing — matches §2 + §3:**
- [ ] Vertical order is exactly ① Header → ② Emblem → ③ Subtitle → ④ Stars → ⑤ Time → ⑥ Spoils → ⑧ CTA; all centered.
- [ ] Star row band = 72px; 3 stars, 56px each, centers at −80/0/+80px from panel center, vertically centered.
- [ ] No band overlaps another (stars never print through the Time line — the original bug); no compression `FlowTrace.Fail`.
- [ ] Panel still auto-sizes to content (no cavernous empty space, no clipped content).

**Stars — real + spinning:**
- [ ] The pips are **real 5-point stars** (sprite), NOT diamonds, NOT rotated squares, NOT TMP glyphs.
- [ ] Earned stars **visibly SPIN in** (540°→0°) while scaling up with an overshoot pop, staggered left→right 0.15s apart, on unscaled time.
- [ ] Earned = gold filled; unearned = dim, no spin.
- [ ] Land pulse fires; a gentle idle twinkle runs after landing (no perpetual full spin).
- [ ] Works in a built player (not just editor) — no tofu, no missing sprite, no dead animation.

**Engineering:**
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`; brace-balanced; MVVM preserved (view reads no game state).
- [ ] **Headless UI capture** of the victory screen at 1, 2, and 3 stars — **open the PNGs** and confirm they match the wireframe; attach to the RESULT. (memory: screenshot-verify UI before shipping.)

**Owner felt-close:** the owner plays a win, sees three real gold stars spin in one-by-one and land with weight, correct spacing, nothing overlapping.

## 7. Do NOT
- Revert to diamonds or a TMP star glyph.
- Touch the panel-height solve / CTA law / other band reveals.
- Ship a star that only fades (no rotation) — the spin is the deliverable.
- Hand-edit any `.unity`/`.prefab` (this screen is code-built — edit `EndStateView.cs`).

## 8. RESULT
`WorkOrders/WORK_ORDER_894_victory_screen_stars_spin.RESULT.md` — the star sprite source, the final spin params used, and the 1/2/3-star headless screenshots proving the match to §2.
