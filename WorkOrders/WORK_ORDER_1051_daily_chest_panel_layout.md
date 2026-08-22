**Status:** READY TO IMPLEMENT

# WORK ORDER 1051 — Daily Chest panel: the claim buttons are drawn on top of the shared Close

**Minted:** 2026-08-21 (UI seat — Claude UI; UI-block banner bumped 1051 -> 1052 in the SAME edit)
**Assigned:** CLI implements. UI authored the layout; UI writes no `.cs` (CLAUDE.md §2).
**Lane:** UI presentation / Monetization surface (CLAUDE.md §9 — isolated lane)
**Class:** DEFECT (layout collision) + a formatting pass on the same screen.
**Owner report 2026-08-21:** the Daily Chest screen needs *"proper viewing not overlapping"*.
**Screen:** `Assets/_Modules/Village/Monetization/DailyChestController.cs` — `Build()`, lines 88-113.
**Sibling design language:** WO-1050 (The Night Market) and WO-1133 (The Armory Rail).

---

## 0. One-line truth

**Both claim buttons are authored on top of the shared Close bar.** This is not a hunch and it is
not a rendering quirk — it is arithmetic between two constants that can be read off the source
today, and the two rectangles provably intersect.

---

## 1. THE COLLISION, proven (no capture needed to establish it — only to confirm which pixels win)

Two facts, both read at source this session:

```
DailyChestController.Build()          ElarionUiKit.cs:297
  left  CTA  (0.06, 0.10)-(0.48, 0.28)    DefaultCloseZone = (0.360, 0.050, 0.640, 0.125)
  right CTA  (0.52, 0.10)-(0.94, 0.28)    i.e. x 0.360-0.640, y 0.050-0.125
```

Intersect them:

| Rect pair | x overlap | y overlap | Result |
|---|---|---|---|
| Left CTA x Close | **0.360 - 0.480** (12% of panel width) | **0.100 - 0.125** (2.5% of panel height) | **OVERLAP** |
| Right CTA x Close | **0.520 - 0.640** (12% of panel width) | **0.100 - 0.125** | **OVERLAP** |

**Both** claim buttons clip the Close bar, one from each side, leaving only its middle ~4% of panel
width unobstructed. And the CTAs are built **after** the chrome, so they are later siblings and
**draw on top** — the Close bar is occluded at both ends, and a tap inside either overlap band is
taken by the claim button, not by Close.

This matters more than a cosmetic misalignment because of owner canon: **no panel anywhere may use
an X for close** (`ElarionUiKit.cs:858`) — the close *is* that bottom-centre bar. Burying it under
two CTAs damages the one exit control the screen has.

### Why it happened — the structural cause, not the typo

`DailyChestController.cs:92` parents every child to **`_modal.chrome.content.transform`**.

Every other panel in the codebase prefers the frame's **body zone** and treats `content` as the
fallback — `CosmeticShopPanel.cs:263`, `PauseController.cs:161`, `SettingsController.cs:183`,
`DialogueView.cs:247`, `DailyQuestHud.cs:173`, and `PackStore.cs`:

```
var body = layout != null && layout.body != null ? (Transform)layout.body
                                                 : _modal.chrome.content.transform;
```

The chest takes the fallback unconditionally, so it authors raw fractions across the **whole panel
rect** with no knowledge of where the frame's header / footer / close zones are. **The overlap is
not a wrong number; it is a bypassed layout system.** Fixing only the `0.10` would leave the next
edit free to collide again.

### ⚠ Note for whoever fixes it — `layout.body` alone does NOT clear the Close

Default zones (`ElarionUiKit.ZonesFor`, `:326-329`): `body = (0.06, 0.10, 0.94, 0.875)`,
`footer = (0.08, 0.030, 0.92, 0.095)`, `close = (0.360, 0.050, 0.640, 0.125)`.
The default **body's own floor (0.10) sits inside the close box (0.050-0.125)**, and the default
footer overlaps it too. So simply reparenting is necessary but **not sufficient** — the CTA band
must also be raised. See §3.

---

## 2. The other defects on this screen (all read at source, all fixed by the same pass)

| # | Defect | Evidence |
|---|---|---|
| 2 | **Both labels run past the body well onto the ornate border.** `ElarionUiKit.Label` defaults to `x0 = 0.03f, x1 = 0.97f` (`:1710`); the default body well is `x 0.06-0.94`. Both labels overhang 3% of panel width on **each** side. | `DailyChestController.cs:94,99` pass no x arguments |
| 3 | **The description label's top edge clears the body well.** Authored `y 0.50-0.88`; the well tops out at **0.875**. | `:94` vs `ZonesFor :327` |
| 4 | **The medallion icon does not exist.** `medallionIcon: "icon_chest"` — there is **no `icon_chest`** in `Assets/Resources/RpgUi/icons/`, no file matching `*chest*` anywhere under `Assets/Resources`, and no `chest` row in `concept-icons.json`. Doubly dead: the modal is built with `frameName: null`, and the default frame declares `hasMedallion = false`. | `:90`, verified by glob this session |
| 5 | **A ready ad button still looks disabled.** `_doubleButton` is built `ObsidianButtonColor.Gray` and **never repainted**; `Update()` only flips `.interactable`. So when an ad IS ready the face is unchanged and the player has no visual reason to believe the button works. | built `:110`, toggled `:70-71` |
| 6 | **The panel is oversized for two sentences and two buttons.** `0.10-0.90 x 0.18-0.82` = 80% x 64% of the canvas, mostly empty. | `:89` |

⛔ **Defect 5 is also a colourblind failure.** Interactability is currently carried by a colour face
that never changes plus a `.interactable` flag with no visual. State must be readable without hue —
see §4.

---

## 3. The fix — geometry

### 3.1 Reparent first

Take the body zone the way every other panel does, with `content` only as the fallback. This is the
load-bearing change; the numbers below assume it.

### 3.2 Raise the CTA band clear of the Close box — use the sanctioned precedent

**This exact collision has been fixed once already in this kit.** `ElarionUiKit.cs:418-423`, for
`FrameRaid`, in its own words:

> *"MinTouchPx=112 button floor, so ClampMinTouch grew footer CTAs past the band into the shared
> Close underneath (the Raid Deploy bottom-row overlap). Explicit RAISED, button-height band
> instead: 0.13 panel height holds a MinTouch CTA on the post-scale landscape canvas."*

That fix authored `footer = (0.055, 0.155, 0.945, 0.285)`. **Adopt the same band here** — it clears
the close box's 0.125 ceiling by 0.03 of panel height:

| Element | Anchor min | Anchor max | Clears |
|---|---|---|---|
| **Claim (free) — PRIMARY** | `(0.055, 0.155)` | `(0.495, 0.285)` | close top 0.125 by 0.030 |
| **Watch ad (optional) — SECONDARY** | `(0.505, 0.155)` | `(0.945, 0.285)` | same |
| Shared Close | *(untouched)* | `(0.360, 0.050, 0.640, 0.125)` | now fully visible and fully tappable |

### 3.3 Bring the labels inside the well

| Element | Anchor min | Anchor max | Note |
|---|---|---|---|
| Headline | `(0.075, 0.700)` | `(0.925, 0.830)` | inside the well's `x 0.06-0.94` and under its `y 0.875` ceiling |
| Body sentence | `(0.075, 0.480)` | `(0.925, 0.680)` | pass `x0`/`x1` **explicitly** — do not take the 0.03/0.97 defaults |
| Status line | `(0.075, 0.330)` | `(0.925, 0.440)` | a clear 0.045 gutter above the CTA band |

Gutters: 0.020 headline-to-body, 0.040 body-to-status, 0.045 status-to-CTA. Nothing touches.

### 3.4 Shrink the panel to its content

`(0.155, 0.200)` - `(0.845, 0.800)` — 69% x 60%. Landscape-appropriate for two sentences and two
buttons, and it stops the content floating in an oversized well.

⚠ **Judgement call, flagged as one:** §3.4 is a proportion I chose, not a derived value. If it
fights the frame art, keep the existing `0.10-0.90 x 0.18-0.82` — **§3.2 and §3.3 are the fix and
they hold at either panel size**, because every number in them is panel-relative.

### 3.5 ⛔ Verify the CTA band actually reaches 112 px — do not assume it

`BuildModalCanvas` uses `referenceResolution = (1080, 1920)` with `MatchWidthOrHeight`
(`ElarionUiKit.cs:107`) — **a portrait reference on a landscape-locked build**, so the post-scale
canvas height in reference units is far smaller than the width, and a band that looks generous as a
fraction may land under the floor.

**Measure the resolved rect on the post-scale canvas** and confirm both CTAs are >= `MinTouchPx`
(112) on their short side. If the band cannot reach it, **raise the panel height** rather than let
`ClampMinTouch` inflate the buttons — inflation is what pushed the Raid CTAs into the Close bar in
the first place, and it would reproduce this very defect by a different route.

⚠ And read `ElarionUiKit.cs:1057` before measuring: `rect.height` returns **raw screen pixels**
until the CanvasScaler has applied. That was F8-5's root cause. Measure after layout, not during.

---

## 4. Formatting and state (the "proper viewing" half of the ask)

- **Hierarchy.** Headline / one sentence / status / two CTAs. The free claim is the **primary**
  (gold face); the ad is **secondary** (framed, not filled). The free path must never look like the
  lesser option — that is the covenant, not a style preference.
- **Defect 5 fix — the ad button's state must be visible.** Three states, each with a **word**, not
  only a colour: `Watch ad: claim 1,000` when ready, `Ad not ready` when not, `Opening ad...` while
  loading. Repaint the face with the state; do not leave a ready button wearing the Gray face.
- **Colour never carries meaning** (owner is red/green colourblind). Ready vs not-ready is the
  label plus the button style; never a green/grey swap alone.
- **Palette:** the WO-1050 / WO-1133 four lights — gold 195 / verdant 177 / ember 145 / aether 113
  (rec.709 luminance of 255). Free-path surfaces take **verdant**, consistent with the Night
  Market's free band, so "this costs nothing" reads the same in both screens.
- **Medallion:** either mirror a real `icon_chest` sprite and build the modal with a frame whose
  zones declare `hasMedallion = true`, **or drop the argument**. Passing an id that resolves to
  nothing, to a frame that has nowhere to put it, is two dead things pretending to be a feature.

### Strings — exact keys and text

Flat camelCase per the file's existing 133 keys. **ASCII-only** (non-ASCII renders as tofu).
**Both copies, byte-identical:** `Assets/Resources/Data/Canonical/canon-strings.json` and
`Assets/StreamingAssets/Data/Canonical/canon-strings.json`.

| Key | Text |
|---|---|
| `chestTitle` | `Daily Chest` |
| `chestHeadline` | `Today's supplies are ready.` |
| `chestBody` | `Your realm set aside 500 Gold. Claim it now, or watch one optional ad to take 1,000 instead.` |
| `chestStatusFree` | `The free chest is always available.` |
| `chestClaimFree` | `Claim 500 Gold` |
| `chestClaimDouble` | `Watch ad: claim 1,000` |
| `chestAdNotReady` | `Ad not ready` |
| `chestAdOpening` | `Opening ad...` |
| `chestAdUnavailable` | `Ad unavailable right now. You can still claim 500 Gold.` |
| `chestAdNoReward` | `No reward was consumed. Claim 500 Gold, or try the ad again later.` |
| `chestLedgerLoading` | `The realm ledger is still loading. Please try again in a moment.` |

The last three are the existing inline strings at `:118`, `:130` and `:150` — they are correct as
written and only need moving into the file.

---

## 5. ⛔ What NOT to touch

- **The reward values and the ad gate.** 500 Gold, 1,000 with the rewarded lantern.
  `RewardedAdManager` withholds the reward on purpose; it may only ever be granted from a real
  earned callback. **This is a layout ticket.**
- **`FeatureFlags.RewardedAdSkip`.** Leave its default alone.
- **The once-per-UTC-day gate**, `DailyChestDayKey`, `TodayKey()`, and the save write.
- **`PanelManager` registration** and the `NotifyOpened` -> `Close()` guard at `:113`.
- **The shared Close.** Move the CTAs off it; do not move, resize or re-skin it, and **never
  substitute an X** (`ElarionUiKit.cs:858`).
- **`ElarionUiKit.ZonesFor` default zones.** The default `body`/`footer`/`close` overlap noted in §1
  is a real kit-level wart, but changing a shared default touches every panel in the game. **Log it
  as a separate ticket; do not fix it inside this one.**

---

## 6. Acceptance

1. Neither claim button intersects `DefaultCloseZone`. Assert it as arithmetic on the resolved
   rects, not by eye.
2. The Close bar is fully visible and fully tappable across its whole width.
3. No label extends past the body well on any edge.
4. Both CTAs measure >= 112 px on their short side on the post-scale canvas, with
   `ClampMinTouch` a **no-op** (it does not grow either button).
5. The ad button's three states are each distinguishable **with hue removed**.
6. The medallion either renders real art or is not requested.
7. All eleven strings resolve from `canon-strings.json`, both copies, ASCII-only.
8. Verified in **both `ff.blinkchrome` states**.
9. Captures opened, not just taken: ad-ready, ad-not-ready, mid-claim, **and a greyscale pass**.
   Compile-green never proved a panel looked right.

---

## 7. Files

**Edit:** `Assets/_Modules/Village/Monetization/DailyChestController.cs` ·
`Assets/Resources/Data/Canonical/canon-strings.json` +
`Assets/StreamingAssets/Data/Canonical/canon-strings.json` (dual copy, byte-identical)

**Read, do not edit:** `Assets/_Modules/Core/UI/ElarionUiKit.cs` (zones, `DefaultCloseZone`,
`ClampMinTouch`, the `FrameRaid` precedent at `:418-423`) · `RewardedAdManager.cs` · `AdServices`

**Separate tickets, named not folded:** (a) the default-zone `body`/`footer`/`close` overlap in
`ElarionUiKit.ZonesFor`; (b) mirroring an `icon_chest` sprite if the medallion is wanted.
