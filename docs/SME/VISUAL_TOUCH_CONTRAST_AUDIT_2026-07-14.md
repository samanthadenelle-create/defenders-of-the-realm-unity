# Visual/UX Audit — Button touch-targets & "too dark" contrast (Seeker APK / Pi Browser)

**Date:** 2026-07-14 · **Type:** READ-ONLY SME audit (for Grok cross-review) · **Branch:** `wip/village2-and-f8-tickets`
**Trigger:** owner device test — first Android APK on the Solana Seeker showed close/X buttons too small + colors too dark.
**All findings verified from actual code, not comments.**

---

## 0. TL;DR
- The owner's "close/X too small" is **two distinct problems**: (A) a set of **UIToolkit panels that draw raw-pixel `"X"` closes with NO reference-resolution scaler** (24–34 device px ≈ 10–14 dp on the Seeker — the true culprits), and (B) **the uGUI kit has NO touch-target floor at all** — `ElarionUi.TapTarget = 88` exists but is *dead code for gameplay UI* (only the UIToolkit `StyleButton` reads it). The canonical Close/Continue *is* pinned to 360×120 ref px and is fine; everything else free-floats by fraction anchors.
- The "too dark" is partly **owner-mandated** (WO-562 "black panel + gold trim — kill the brown"), so text-on-panel contrast is actually *excellent* (light parchment on near-black ≈ 15–18:1). The **measurable WCAG failures are the colored button faces**: parchment label on the green Confirm face is **~1.9:1 (FAIL)**; on red Danger **~3.2:1 (fails body text)**.
- **Owner rulings 2026-07-14:** build screen orientation = **LANDSCAPE (CoC-style)**; panels **KEEP near-black** (no lift) — fix button faces + sizes only.

---

## 1. Central UI kit map (cite these)

| Layer | File:line | Role |
|---|---|---|
| **Kit factory (uGUI)** | `Assets/_Modules/Core/UI/ElarionUiKit.cs` | master builders: `BuildModalCanvas` (96), `Scrim` (120), `BuildObsidianPanel` (495), `ObsidianCloseButton` (820), `Button` procedural (1218), `StyleButtonColors` (1265) |
| Obsidian widgets partial | `ElarionUiKitObsidian.cs` | `BuildObsidianButton` (577), `FontFloor=30` (2173), `FitSingleLine/FitBlock` (2194/2217), `BuildControllerCluster` (1921) |
| Kit conformance primitives | `ElarionUiKitConformance.cs` | WO-714 P1–P10 shared builders |
| **Palette / theme constants** | `Assets/_Modules/Core/UI/ElarionUi.cs` | all colors (49–88), font ladder (102–106), `FontFloorMobile=30` (114), **`TapTarget=88` (170)** |
| Theme routing | `Assets/_Modules/Core/UI/UiStyle.cs` | `Theme.Glass` etc; `TapTarget` re-export (133, 314) |
| Shop UIToolkit theme | `ShopTheme.cs` | `StyleCloseButton` (283), `StyleTab` (310) |

**Canonical size constants** (`ElarionUiKit.cs`): `CanonCtaWidth=360f` (300), `CanonCtaHeight=120f` (302), `DefaultCloseZone` (288). Close pin path: `ObsidianCloseButton` (820) → `SeatSharedCloseInside` (877) → `sizeDelta=(360,120)`.

**Key architectural fact:** two UI systems coexist. Gameplay panels are uGUI via ElarionUiKit (correct per CLAUDE.md). But **Referral, Promo, TalentTree, TowerSwap, Marketplace store, and ShopTheme are UIToolkit** (VisualElements) — these hold the tiny closes and also violate the uGUI-only rule.

---

## 2. Touch-target audit

### 2a. There is NO touch-target floor wired into the uGUI kit
- `ElarionUi.TapTarget = 88f` (`ElarionUi.cs:170`) is the *only* touch constant. **Its sole consumer is `ElarionUi.StyleButton` → `minHeight` at `ElarionUi.cs:344` (UIToolkit).** Confirmed by grep: no uGUI builder and no gameplay screen references `TapTarget`. So the analogue to the font floor (`FontFloor`/`FontFloorMobile=30`) **does not exist for buttons**.
- `BuildObsidianButton` (`ElarionUiKitObsidian.cs:577`), procedural `Button` (`ElarionUiKit.cs:1218`), and `ObsidianCloseButton` (unless canonically pinned) all anchor **purely by fraction-of-parent with no pixel clamp**. A button in a small sub-rect computes to an arbitrarily small physical size.
- **Exception (good):** the ONE shared Close and primary Continue get `sizeDelta=(360,120)` ref px via `SeatSharedCloseInside`/`PinCanonicalCtaSize`. Movement D-pad uses `Btn=88` ref px (`BuildControllerCluster:1923`).

### 2b. The actually-tiny closes — raw-pixel UIToolkit, no scaler (these are what the owner tapped)
UIToolkit here has no PanelSettings reference scaler, so **these sizes are literal device pixels**. On the Seeker (1080×2400, ~394 ppi, 1 dp ≈ 2.46 px):

| File:line | Current | Device size | ≈ dp |
|---|---|---|---|
| `ShopTheme.cs:287` `StyleCloseButton` | `height=34; minWidth=72; fontSize=14` | 34 px tall | **~14 dp** |
| `Village/Buildings/MarketplaceInteractor.cs:163-177` `"X  Close"` | `fontSize=14; padTop/Bot=6` | ~26 px tall | **~11 dp** |
| `Core/Promo/PromoCodeUI.cs:130` + `ApplyIconBtn` (288) | `fontSize=16; pad=6`, transparent | ~28 px | **~11 dp** |
| `Core/Referral/InviteFriendsUI.cs:136` + `ApplyIconBtn` | same ~28 px | ~28 px | **~11 dp** |
| `Village/Talents/TalentTreePanel.cs:157-161` `"X"` | `StyleButton(Danger)` then **`minHeight=Auto` (159)** → **removes the 88 floor**, pad 4 | ~24 px | **~10 dp** |
| `Village/Buildings/TowerSwapMenu.cs:221` `"X"` + `ApplyIconButton` | icon button, no min size | small | **~10–12 dp** |

All are **3–5× below** Apple HIG 44 pt / Material 48 dp / WCAG 2.5.5 44 px.

### 2c. uGUI per-panel closes that bypass the canonical pin (fraction-anchored, no floor)
- `Village/Arena/ArenaPanel.cs:200` and `:400` — `AddButton(..., "Close", ...)` at fraction anchors, not the chrome close.
- `Village/Buildings/NPCUpgradeStation.cs:105` — `CreateButton("Close", (0.8,-0.9), ...)`.
- `HUD/AdminOverlay.cs:254` — dev overlay (low priority).

### Recommended floor (with reasoning)
Reference→device factor on the Seeker is ≈**1.118×** (1080×1920 ref, match 0.5, both orientations). So:
- **48 dp (Material min) ≈ 118 device px ≈ 106 ref px.** Current `TapTarget=88` ref px ≈ 98 device px ≈ **40 dp — already under Material.** The comment "88px = 44pt" conflates ref-px with points; it's optimistic.
- **Recommendation:** kit touch floor **`MinTouchPx = 112` ref px** (~50 dp) for *all* buttons; **primary/close ≥ 132 ref px** (~60 dp, one-handed thumb). Bump `CanonCtaHeight 120 → 132`. For the raw-px UIToolkit closes set explicit **≥ 120 px, prefer 148 px** (they don't scale, so pick device px directly).

### Coverage: one kit change vs sweep
- **One-place win:** wiring a min-size clamp into `BuildObsidianButton` + procedural `Button` covers *every* uGUI panel that routes through the kit (the vast majority). Raising `CanonCtaHeight` covers all canonical Close/Continue instantly.
- **Per-file sweep (cannot be fixed at the kit):** the 6 UIToolkit sites in 2b (they don't call the kit) + the 3 uGUI per-panel closes in 2c. Best long-term fix is migrating 2b to `ElarionUiKit.ObsidianCloseButton`, but the fast device fix is to set their sizes.

---

## 3. Contrast / "too dark" audit

Palette actuals (`ElarionUi.cs`) and `ElarionUiKit.ObsidianFill` (line 186). WCAG relative-luminance / ratios computed:

| Token | Value (RGB / hex) | Rel. lum |
|---|---|---|
| `ObsidianFill` / `PanelStoneDark` | (0.02,0.02,0.025) ~#050506 | ~0.0016 |
| `PanelStone` | (0.055,0.05,0.06) ~#0e0d0f | ~0.004 |
| Panel `Backdrop` (`ElarionUiKit.cs:505`) | (0.02,0.015,0.012,0.94) | ~0.001 |
| `Gold`/`StoneTrim` | (0.831,0.686,0.216) **#d4af37** | ~0.449 |
| `Parchment` (body text) | (0.953,0.918,0.827) **#f3ead3** | ~0.826 |
| `Ink` (text on gold) | (0.137,0.098,0.055) | ~0.011 |
| `Affordable` (green) | (0.46,0.74,0.42) **#75bc6b** | ~0.412 |
| `Danger` (red) | (0.86,0.34,0.32) **#db5751** | ~0.225 |

**Ratios:**
- Parchment on ObsidianFill: **~18:1** — excellent. Text is NOT the dark problem.
- Gold trim/title on ObsidianFill: **~7.4:1** — good.
- Ink on Gold (gold CTA label): **~8.2:1** — good.
- **Parchment label on GREEN Confirm face: ~1.9:1 → FAIL** (need 4.5:1 text / 3:1 UI). `ObsidianButtonLabelColor` (`ElarionUiKitObsidian.cs:541`) sends Parchment onto every non-yellow face.
- **Parchment on RED Danger face: ~3.2:1** — passes large/UI (3:1), fails body text (4.5:1).

### The "too dark" tension (owner ruled KEEP BLACK 2026-07-14)
`ObsidianFill`/`PanelStoneDark` at luminance ~0.0016 is **near-pure-black by owner mandate (WO-562, comment `ElarionUi.cs:43-47`).** Owner confirmed 2026-07-14: **keep it** — do NOT lighten panels. (Option considered and declined: lift `ObsidianFill (0.02,0.02,0.025) → (0.11,0.11,0.125)` #1c1c20 via `ElarionUiKit.cs:186` + `ElarionUi.cs:51`.) The fix is on the **button faces + sizes**, not the panels.

### Colorblind (red/green) — current state is mostly compliant, keep it
- The kit already avoids meaning-by-hue: `ObsidianButtonLabelColor` keys on **luminance not hue** (`:538`); currency chips **never red/green flash** (`:668`); rarity carries a **shape glyph** channel (HudUiRegression check 3); WO-693/craft rows use **check/X glyph + have/need counts**, color as reinforcement only.
- **Action:** no regressions to introduce — but when darkening green/red faces (below), keep the glyph/label backup. Green `Affordable` and red `Danger` are the classic confusable pair; never let a *new* state ride on those faces alone.

### Recommended higher-contrast face values
- Confirm green: `Affordable (0.46,0.74,0.42)` used as a *button face* → deepen to **(0.16,0.42,0.20) #286b33** (parchment then reads **~5.4:1**). Keep the bright `Affordable` for *text/glyph* accents on dark panels (fine there).
- Danger red face: deepen to **(0.62,0.16,0.14) #9e2924** for a clean ≥4.5:1 with parchment.
- Both are edits to `FillFor` (`ElarionUiKit.cs:1253`) / `ElarionUi.ButtonRest` (`ElarionUi.cs:362`) — one place each.

---

## 4. Prioritized fix list for CLI (ranked by felt impact per unit of work)

**P0 — Kit touch floor (ONE place, covers all uGUI buttons).**
`ElarionUiKitObsidian.cs:577 BuildObsidianButton` and `ElarionUiKit.cs:1218 Button`: after building the rect, clamp shortest side to a new `MinTouchPx`. Add `public const float MinTouchPx = 112f;` near `CanonCtaWidth` (`ElarionUiKit.cs:300`). Also bump `CanonCtaHeight 120f → 132f` (`:302`) for close/primary. Verify: EditMode assert of the `sizeDelta` floor.

**P0 — Fix the tiny raw-pixel UIToolkit closes (per-file sweep, 6 sites).** Highest *felt* impact (literally what failed on device), but not kit-coverable:
- `ShopTheme.cs:287` — `height=34 → 148; minWidth=72 → 148; fontSize=14 → 34`.
- `MarketplaceInteractor.cs:171-177` — raise pad/fontSize to ~148 px box; or route to kit close.
- `PromoCodeUI.cs:288 ApplyIconBtn` and `InviteFriendsUI` `ApplyIconBtn` — give the "X" a fixed `width/height = 120px` instead of transparent auto-size.
- `TalentTreePanel.cs:159` — **delete `minHeight = StyleKeyword.Auto`** (it removes the 88 floor); set `minHeight = 120`.
- `TowerSwapMenu.cs:222 ApplyIconButton` — fixed ≥120 px.
Best-practice follow-up: migrate all six to `ElarionUiKit.ObsidianCloseButton` (retires the "X" per owner canon 2026-07-03, `ElarionUiKit.cs:806-819`).

**P1 — Colored-face contrast (ONE place each).** `ElarionUiKit.cs:1258` (Confirm) → deep green #286b33; `:1259` (Danger) → deep red #9e2924; mirror `ElarionUi.cs:367-368`. Fixes the 1.9:1 / 3.2:1 label failures. Keep glyph/label backups.

**P1 — Per-panel uGUI closes bypassing the pin (3 sites).** `ArenaPanel.cs:200,400`, `NPCUpgradeStation.cs:105` — route through `ObsidianCloseButton` or apply `PinCanonicalCtaSize`.

**P2 — "Too dark" panel lift — DECLINED by owner 2026-07-14** (keep WO-562 near-black). Left here for record only.

**P3 — ASCII-only violations (adjacent, will tofu on device).** `TowerSwapMenu.cs:228` en-dash `"—"`; `TalentTreePanel.cs:176` `💎` emoji in the Respec label. These fail the binding ASCII rule and `HudUiRegression` tofu oracle. Fix while touching these files.

### Headless verification available
- **`Assets/Editor/Regression/HudUiRegression.cs`** — source-lint gate, marker `HUDUI_OK`/`HUDUI_FAIL`; checks tofu/ASCII, UIDocument fence, kit conformance, Resources paths. **No size check exists** — add a reflection assert that `CanonCtaHeight ≥ 120` and a new `MinTouchPx ≥ 112`, and a UIToolkit-close min-size lint mirroring the existing pattern.
- **`Assets/Tests/EditMode/UiStyleTests.cs:95`** currently only asserts `TapTarget > 0`. Strengthen to `TapTarget ≥ 112` and add a headless build-a-button test asserting `sizeDelta` shortest side ≥ floor.
- **`Assets/_Modules/DevTools/AutoPilotDriver.cs`** has `FindUGuiCloseButton` (4738) / `FindUiToolkitCloseButton` (4770) — a runtime driver that can locate the close and measure its rect (a size oracle for on-device/PlayMode captures).
- **Screenshot fleet** ("fleet 13/13 panels", WO-693 §gates) — daylight/contrast eyes-on after face-color changes. Owner is the device gate (WO-700 §6: fleet can't run on-device).

### Notes / cross-refs
- **WO-700 sets the Android target to LANDSCAPE** (`WORK_ORDER_700...md:25`); the kit canvas reference is **portrait 1080×1920** (`ElarionUiKit.cs:106`) — ref→device factor works out ≈1.118 either way, but any per-screen literal tuned to portrait may under-reserve in landscape (see the landscape close-band history in `BuildObsidianPanel` comments `:568-607`). **Owner ruled the build screen LANDSCAPE 2026-07-14** — the build-HUD lane unifies its canvases to landscape.
- WO-693 established the `FontFloorMobile=30` floor pattern — the button touch-floor should follow the same "name the constant, no per-screen literals" discipline.
