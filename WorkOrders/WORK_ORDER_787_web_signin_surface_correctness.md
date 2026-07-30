# WORK ORDER 787 — Web-build Sign In surface correctness

**Status:** READY TO IMPLEMENT
**Lane:** Lane 4 (UI/HUD) + Platform (Pi/SKR skin)
**Type:** EXISTING (all three surfaces were built; each is now presenting wrong on the web build)
**Minted:** 2026-07-30 (owner felt-report from the deployed web build, with screenshot)
**Author:** UI/RCA seat (read-only). CLI implements + gates + builds. PO felt-verifies + closes.

---

## Symptom (owner, playing the deployed web build — screenshot attached in-thread)

The boot **SIGN IN** modal is visually broken and the sign-in options are wrong for a web build:

1. **"Stacked."** The "SIGN IN" title renders doubled/overlapping, and the **Sign In** +
   **Create Account** + **Sign in with Google** + **Play as Guest** buttons pile on top of each
   other and clip; the intro line "…or play as a guest and bind an" is truncated. Everything is
   crammed instead of a clean vertical stack.
2. **"In a web build there should not be sign in with Google."** Google sign-in is **APK-only**
   (owner ruling). It must not appear on the web/WebGL build.
3. **"Should not say Sign in with Pi in the same build. If not Pi-facing should always be SKR."**
   The corner **"Sign in with Pi"** button (top-right in the screenshot) must not show in a
   non-Pi build; when the build/runtime is not Pi-facing, the active skin must be **SKR/Solana**
   ("Connect Wallet"), never Pi.

This is a **render/layout + platform-gating** issue — the web-trace db (analytics_events) cannot
see it because it throws no exception; the proving evidence is the screenshot + the code below.

---

## RCA — proven from the code (layout) + owner rulings (platform gates)

### Part A — the "stacked" overlap (root cause found, high confidence)

`LoginPanelController.Build()` targets the panel's **body drop-zone**, then lays a full-height
fraction layout inside it:

- `Assets/_Modules/Onboarding/LoginPanelController.cs:107` builds the panel with **no `frameName`**,
  so `BuildObsidianPanel` takes the **procedural path**
  (`Assets/_Modules/Core/UI/ElarionUiKit.cs:730`+).
- `LoginPanelController.cs:112-114` picks `body = chrome.layout.body.transform`.
- `ElarionUiKit.cs:768-793` (WO-714 P6) builds `layout.body` as the default **Zone_Body** and
  **raises its bottom edge** by the close-band reservation:
  `pReserved = min(pCloseTop + 0.020, 0.45)` and `if (pz.body.y < pReserved) pz.body.y = pReserved;`.
  So `layout.body` is a **compressed sub-band** of the panel (floor pushed up to ~0.45), not the
  full 0..1 rect.
- But `LoginPanelController.cs:116-148` lays **8 controls** with **full-panel fractions** —
  intro `0.87–0.97`, email `0.72–0.82`, password `0.59–0.69`, status `0.505–0.565`,
  Sign In `0.40–0.49`, Create Account `0.295–0.385`, Google `0.19–0.28`, Guest `0.04–0.13`.
  Mapped into the compressed body band, every fractional slot shrinks below the control height,
  so the rows **overlap** — exactly "stacked."

**Why it's safe to move off `layout.body`:** this panel **hides its Close**
(`LoginPanelController.cs:110` `chrome.close.gameObject.SetActive(false)`, `onClose:null`) — the
whole reason `layout.body` is compressed (to reserve the close band) does not apply here. Login
should lay on the **full-rect `chrome.content`** like the legacy anchor-layout panels
(`ElarionUiKit.cs:632-635` names screens that lay custom fractions on `chrome.content` as the
intended full-rect surface).

**The doubled "SIGN IN" title** is secondary — likely the procedural `Header()` shadow/main pair
reading as two under the compressed scaling; re-verify after Part A (the body swap) since the
squeeze is what makes it read doubled. CLI: confirm from the post-fix screenshot before adding any
title change.

### Part B — Google button is APK-only

`LoginPanelController.cs:142-144` unconditionally builds `_google = "Sign in with Google"`. It must
be built **only on the Android/APK target**; hidden on WebGL. There is no existing platform guard in
this file (grep: no `UNITY_ANDROID` / `RuntimePlatform` in `Assets/_Modules/Onboarding`).

### Part C — Pi vs SKR skin is not Pi-facing-aware

The corner "Sign in with Pi" button is built by `PiSignInController.BuildButton()`
(`Assets/_Modules/Core/Platform/PiSignInController.cs:245-293`). Its label/behavior is chosen by
`CurrencySkinResolver.Active` (`PiSignInController.cs:56`, `:270`): `AuthMode==PiSdk` → "Sign in
with Pi"; `AuthMode==SolanaWallet` → "Connect Wallet".

`CurrencySkinResolver.Active` resolves in order **URL `?skin=` → skin.json `active` → hardcoded
default = Pi** (`Assets/_Modules/Core/Platform/CurrencySkinResolver.cs:117-129`,
`CurrencySkin.cs:117-124` `DefaultPi`). It has **no Pi-Browser-environment auto-detect**, so a plain
web build (opened outside Pi Browser) defaults to the **Pi** skin and shows "Sign in with Pi" — the
owner's exact complaint. Note `WebGLPiPlatform.IsPiBrowserEnvironment` already exists (used at
`PiSignInController.cs:102` to gate auto-sign-in) — that is the correct "is this Pi-facing?" signal.

---

## The fix (bounded)

**Part A — lay the login controls on the full-panel rect.**
- In `LoginPanelController.Build()`, set `body = chrome.content.transform` (the full 0..1 rect)
  instead of `chrome.layout.body.transform`. Keep the existing fraction anchors — they were designed
  for the full rect and will space correctly again.
- *(Alternative if the owner wants the sanctioned spacing helper: use
  `ElarionUiKit.BuildButtonColumn(body, gapPx)` (`ElarionUiKit.cs:583`) for the four buttons so a
  VerticalLayoutGroup enforces MinTouchPx spacing. Part A's content-rect swap is the minimal fix;
  do that first, screenshot, then adopt the column only if rows still read tight.)*
- Re-check the doubled title on the post-fix screenshot; only touch the title if it persists.

**Part B — gate Google to Android.**
- Build `_google` only under `#if UNITY_ANDROID && !UNITY_EDITOR` (or `Application.platform ==
  RuntimePlatform.Android`), matching the owner's "APK only" ruling. On web/editor, don't create the
  button and reflow the remaining controls (Sign In / Create Account / Guest) to use the freed
  vertical space so the stack stays evenly spaced.

**Part C — not Pi-facing ⇒ SKR skin.**
- Make `CurrencySkinResolver.Active` resolve to the **SKR/Solana** skin whenever the runtime is **not
  Pi-facing** — i.e. when `WebGLPiPlatform.IsPiBrowserEnvironment` is false — UNLESS an explicit
  `?skin=pi` / skin.json override is present. Keep the existing override order on top (explicit
  request wins); only the **default** flips from Pi → SKR for non-Pi hosts.
- Net effects: outside Pi Browser the corner button reads **"Connect Wallet"** (SKR), and inside Pi
  Browser it stays **"Sign in with Pi"** (zero regression to the live Pi path).
- Confirm the store/economy currency presentation follows the same skin (owner: "if not Pi-facing
  should always be SKR"). `CurrencySkinResolver.Active` is the single seam presentation reads, so
  flipping the default carries the store too — verify no view hardcodes "π"/"Pi".

---

## Root candidates / proving steps the CLI must run before/after editing (§12)

- **A:** After the `body → chrome.content` swap, run `RunCaptureHeadless` and open the Sign In PNG —
  confirm intro + 2 fields + 3 buttons (web) are evenly spaced, no overlap, no clip
  (memory `headless-screenshot-verify-ui-before-build`).
- **B:** Build/inspect a WebGL preview — assert **no** "Sign in with Google" button; then confirm the
  Android/APK build **does** show it (or gate-verify via the platform define).
- **C:** Open the WebGL preview outside Pi Browser — corner reads **"Connect Wallet"**; add `?skin=pi`
  → reverts to "Sign in with Pi" (override still wins); a Pi-Browser UA still auto-shows Pi.

---

## Acceptance

- [ ] `LoginPanelController` lays out on the full-panel rect; headless Sign In screenshot shows a
      clean vertical stack (no overlap/clip/truncation), title not doubled.
- [ ] Web/WebGL build shows **no** "Sign in with Google"; APK build still shows it.
- [ ] Outside Pi Browser the corner button + store currency resolve to **SKR** ("Connect Wallet");
      inside Pi Browser the Pi path is unchanged; explicit `?skin=pi`/skin.json override still wins.
- [ ] Brace/NUL gate passes on every `.cs` edited; `COMPILE_GATE_OK` emitted.
- [ ] Preview WebGL build handed to owner for the mobile felt-pass; **PO closes** (feel).
- [ ] A proving line / screenshot quoted in the `.RESULT.md`.

## What NOT to touch

- Production Pi path stays byte-identical **inside** Pi Browser (the live skin); only the **default**
  for non-Pi hosts flips to SKR. Do not remove Pi sign-in — gate it.
- Keep the auth-service layer untouched (`FirebaseAuthService`, `LoginViewModel`) — this is
  presentation/gating only (`LoginPanelController` header: "PRESENTATION ONLY").
- Do not re-enable a visible Close on the login panel (guest remains the escape).
- The existing Pi SDK await-timeout guards (`PiSignInController` 20s/30s/20s) stay.

---

*Notion "Work Orders" DB row — pending (add on a tooled session; NOTION_SOURCE_OF_TRUTH.md).*
