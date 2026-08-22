# WORK ORDER UI-001 — Night Market landscape visual redesign

**Series:** UI (intentionally separate from numbered BOARD work)
**Status:** GUIDANCE + FOUR-LANE SME REVIEW FOLDED 2026-08-22 — READY FOR UI IMPLEMENTATION
**Owner lane:** UI / Store presentation
**Date:** 2026-08-22
**Tracking:** Not tracked on `BOARD.html`. Do not edit or rebuild the board for this document.

**Design reference (interactive wireframe, SME-folded):** https://claude.ai/code/artifact/5b8dc821-9290-4936-b202-6e3974854955
- drive the UI-002 commerce states live, toggle thumb zones and greyscale; the badge moves and
  the single bottom band are drawn in. The wireframe is the visual acceptance target.

## OWNER RULINGS 2026-08-22 (verbal, this session — bind every section below)

1. **"maximize whole screen"** — the store goes **FULL-SCREEN**, not a centered modal column.
   Wherever this document says "centered store surface with a clear maximum width", the ruling
   supersedes it: the surface is the screen. Safe-area insets still apply; a maximum width does not.
2. **"this is the money screen"** — this surface outranks other panels in polish priority. Treatment
   is calibrated accordingly; corners that are acceptable elsewhere are not acceptable here.
3. **"GET SME team on this"** — a four-lane SME review (merchandising, Unity kit feasibility, crypto
   payment UX, accessibility/ergonomics) was convened 2026-08-22; its findings are folded into this
   document below. Sections marked **[SME]** carry that provenance.

## Outcome

Redesign the existing Night Market so it reads as a polished, cohesive mobile-game storefront at the Seeker's 2340×1080 landscape resolution. Preserve the catalog, purchase authority, fulfillment rules, and monetization covenant. This is a presentation and interaction-layout pass, not a commerce rewrite.

## Device evidence

- Primary screenshot: `dev/tmp/seeker_store_skr_offer.png` (captured 2026-08-22, 2340×1080 landscape).
- Companion screenshot: `dev/tmp/seeker_store_pre_purchase.png`.
- Trace: `Logs/device/mon-skr-store-open-2026-08-22.log`.
- Trace proves eight pack cards were built across four bands and that StoreAurora stayed within its frame budget. Do not diagnose the sparse presentation as missing catalog data or solve it by removing content.

Observed presentation defects in the primary screenshot:

1. The modal occupies only a narrow central column while large black/sparse regions dominate its lower half and the surrounding HUD remains visually competitive.
2. The title is repeated (`The Night Market` in the frame and content header), weakening hierarchy.
3. The product spotlight, offer bands, legal/value copy, and Close action compete at several unrelated scales.
4. The first free card truncates `Redeem` to `Redee...`; card names and quantities are too small for phone viewing.
5. Pack art is extremely dark, tiny, and inconsistently framed. Several cards read as empty placeholders.
6. `Coming soon` occupies the expected purchase-action location without enough contextual hierarchy.
7. The large Close button consumes prime selling space and feels like a second modal nested beneath the store.
8. Disclaimer lines collide visually near the bottom of the content region and are too small to scan comfortably.
9. World HUD/status elements remain visible around the modal, producing layered noise rather than a deliberate modal state.

## Design direction

### 0. The load-bearing geometry — read before §1 [SME: Unity kit lane, cited at source]

**Full-screen is ONE constant plus ONE seam extension. Do not bypass the kit to get it.**

1. **The footprint seam already exists and is landscape-aware.** `ElarionUiKit.ModalArchetype` /
   `ModalAnchors` (`ElarionUiKit.cs:872-923`) — the landscape `Browse` archetype is
   `(0.06,0.06)-(0.94,0.94)` and its own doc names "shops". The narrow column on screen is
   `StorePanelAnchorMin/Max = 0.325-0.675` (`ElarionUiKit.cs:553`, consumed at
   `PackStore.cs:249-251`). **That constant is the change** — route PackStore through the archetype
   (or `(0,0)-(1,1)` + safe-area padding, per the owner's full-screen ruling).
2. **The interior seam is EXTENDED, never worked around.** `ZonesFor(frameName)`
   (`ElarionUiKit.cs:348-507`) is private, hardcoded fractions, no landscape variant — add a
   landscape branch THERE. The kit names the alternative as the failure class: *"screens laying
   custom fractions directly on chrome.content remain the unprotected legacy class"* (`:788-789`).
3. **Drop `FrameMerchant` on this screen.** The frame art is portrait 1005x1507 drawn
   `Image.Type.Simple` — no 9-slice (`:596`, `:401-406`). Full-bleed at 2340x1080 is a **3.25x
   horizontal stretch** of the ornate border, and its pixel-measured medallion/header fractions stop
   landing on art features. Correct path: `frameName: null` -> the procedural obsidian panel
   (`:784-853`) — aspect-agnostic, still builds zones and the close reservation.
4. **The real budget, computed:** CanvasScaler ref 1080x1920 at match 0.5 -> scale 1.104 ->
   **usable 2120 x 978 reference units**. Vertical is 51% of the 1920 everything was authored
   against; horizontal is nearly 2x. Every fraction below resolves against 978, not 1080.
5. **The black cavity is arithmetic, not styling.** The close-band reservation (`:626-668`,
   mirrored `:824-850`) divides fixed `CanonCtaHeight = 132` by that shrunken height — the body
   floor lands at ~0.28 of panel height **whether the panel is the current column or full-screen**.
   Widening alone removes nothing. The cavity is reclaimed by §6's single bottom band.
6. **Card and font consequences.** `CardHeightPx = 168` (`PackStore.cs:107`) fits ~5.8 rows in 978
   units. `PackStore.MakeText` (`:1737-1755`) sets raw `fontSize` 9-17 with no fit-guard against the
   project floor `ElarionUi.FontFloorMobile = 30f` (`ElarionUi.cs:123`) — those render ~10 physical
   px. Defects #4/#8 are fixed by raising every store string to >=30 + `FitSingleLine`, NOT by
   widening the panel.
7. **`Redee...` is clamp behaviour, not ellipsis config.** `UiKitMinTouchGuard`
   (`ElarionUiKit.cs:1064-1090`) grows sub-112 rects by writing offsets and is a documented NO-OP
   under layout groups (`:1039-1040`; `PackStore.cs:100-106` already warns of exactly this). Author
   every card >=112 reference px on both axes — WO-1060: the clamp must never fire.
8. **Batchmode trap:** `ModalAnchors` picks portrait/landscape off `SurfaceWidth/Height`, which fall
   back to `Screen.*` = **640x480 in batchmode** (`:884`, `:1096-1106`). Every automated layout
   assertion MUST call `SetSurfaceOverride(2340,1080)` first, or it measures the portrait branch and
   proves nothing.
9. **Safe-area insets have NO existing implementation** — no `Screen.safeArea` usage in the kit
   paths read. The §1 insets bullet is new work, not an inherited guarantee.

### 1. One landscape storefront grid

- **The store surface IS the screen** (owner ruling above). No centered column, no maximum width —
  full-bleed to the safe-area insets. *(The earlier text here proposing a "centered surface with a
  clear maximum width" is superseded by the ruling.)*
- Establish three regions: compact store header, scrollable merchandise body, persistent contextual footer/action area.
- At 2340×1080, show the featured pack and the current merchandise band side by side without compressing either below its readable minimum.
- Eliminate the unused black lower cavity. The merchandise body should expand to the available height; scrolling belongs inside that body.
- Respect cutouts, gesture areas, and device safe-area insets. **New work — see §0.9;** nothing in the kit reads `Screen.safeArea` today.

### 2. One design system — and the kit already owns it [SME: Unity kit lane]

**Do NOT build the nine `Store*` component classes this section previously listed.** The kit already
owns every one of them; nine new classes would be a parallel design system:

| Previously proposed | Already exists |
|---|---|
| `StoreSurface` | `ElarionUiKit.Panel` / `Well` / `BuildObsidianPanel` |
| `StoreHeader` / `StoreSectionHeader` | `ElarionUiKit.Header` |
| `StorePrimaryAction` / `StoreSecondaryAction` | `BuildObsidianButton` + `ButtonKind` roles |
| card body | `ElarionUiKitDetailCard` |
| type / spacing / corner tokens | `UiStyle.Theme` / `ElarionUi` |
| sprites | `RpgUiCatalog` roles |

**The rule instead:** route ALL store visuals through `ElarionUiKit` builders + `UiStyle.Theme`
tokens + `RpgUiCatalog` roles. Add at most **one** store-local helper — `StorePackCard` with
`featured` / `standard` / `compact` variants — inside `Assets/_Modules/Wallet/`. No second palette
(`NightMarketPalette.cs` folds into `UiStyle`; it does not grow), no second button family, no new
chrome. Variants change measurements and optional fields only; typography, spacing, corner and state
treatment come from the one authority. No one-off styling branches for named SKUs.

**WO-1060 compliance:** a tappable `compact` card is the exact clamp-rescue pattern the oracle bans.
Every variant is authored >=112 reference px on both axes, or it does not exist.

### 3. Visual hierarchy

- Keep one visible `The Night Market` title.
- Header order: store title → wallet/rail summary → Close icon/button.
- Featured product order: artwork → name/value label → short flavor line → transparent contents → price/state → primary CTA.
- Merchandise section order: section label + one-line promise → cards.
- Treat `Best Value`, `Free Tonight`, and other merchandising labels as consistent badges, not isolated text colors.
- Legal/covenant text must remain present but move into a legible, quiet footer; it must not cross card content or actions.

### 4. Pack art and content density

- Give every pack a deliberate art well with a readable silhouette and consistent aspect ratio.
- Never show an unresolved/near-black icon as if it were valid product art. Use an intentional neutral placeholder plus accessible product initials only when an asset is genuinely absent.
- Show the most decision-relevant contents at card level; reveal the full transparent contents on focus/details.
- Keep exact rewards visible before purchase. Do not introduce mystery boxes, randomized reveals, or urgency mechanics.

### 5. Touch and text

- No actionable control below the project's mobile touch-floor requirement.
- Never truncate action verbs such as `Redeem`, `Buy`, `Connect wallet`, `Retry`, or `Close`.
- Product names may wrap to two lines; quantities and currency must never be clipped.
- Use dynamic measurement/wrapping rather than fixed-width ellipsis for critical commerce copy.
- Validate contrast in full color and grayscale. State may not be communicated by hue alone.

### 6. Modal and HUD separation

- The store must read as the active layer. Dim/suppress nonessential village HUD and interaction prompts while it is open.
- Preserve any safety-critical status that product rules require, but do not allow HUD labels to visually merge with store content.
- Use the common modal/pause ownership established by the UI safety lane. Do not implement a second store-only simulation authority.
- **Close stays the bottom-centre word-bar.** Owner canon (2026-07-03, `ElarionUiKit.cs:858`) bans
  an X-close anywhere, and this document does not override canon. The fix for the oversized slab is
  the **single bottom band**: Close and the legal/covenant footer share ONE 132-reference-unit row
  (legal left, `Close` centre, wallet hint right) instead of stacking two bands — which is also what
  reclaims the §0.5 cavity. The ergonomics lane's top-right-corner recommendation is recorded as an
  **owner call only**: adopting it means superseding the 2026-07-03 canon, which no seat does on its
  own. Back/navigation behavior must remain predictable either way.

### 7. Merchandising corrections [SME: merchandising lane — arithmetic re-verified against packs.json this session]

**Badges the numbers contradict** (WO-1050 bans claims the arithmetic contradicts):

| Finding | Verified numbers | Correction |
|---|---|---|
| `BEST VALUE` sits on the WORST-value pack | hearth-spark = 754 wood/$ and 1,533 total-goods/$ — LAST of the five baskets (folks 1,922 / patron 1,968 / vow 1,962 / starters 1,703) | Move it to `patron-of-elarion`, the arithmetic winner — or delete it. **OWNER CALL which.** |
| `LAUNCH ONLY` (`packs.json:188`, founders-vow) is live FOMO copy | violates this document's own non-goal | Replace with the permanence claim: `Founders are named on the Heart.` |
| founders-vow loses its own compare line | 920 wood/$ vs patron's 925, at $30 more | Its one winning axis is 12 vs 5 lantern blessings — compare on that, or show no line. |
| Gap packs out-earn every basket per dollar | impulse-wood-medium = 1,171 wood/$ | Baskets compare on TOTAL goods across all five keys, never a single resource, or the ladder inverts. |

**What earns the full-screen space** (a column cannot do these):
- The **whole-ladder ledger**: all five basket rungs with their content bars side by side on the
  shared per-good scale. The value curve IS the pitch.
- A **persistent wallet rail** top-right: shortened address, network, SKR balance,
  **balance-after-this-purchase**, and a get-SKR affordance. Missing entirely today.
- A **receipts column**: owned packs + shortened signature + settle date — the highest-trust surface
  a crypto buyer can be shown, at zero content cost.
- **Art wells 3-4x current** — defect #5 is the top conversion loss on the screen.

**The three-second read:**
- 0-1s: wordmark + YOUR SKR balance + the covenant line; the free plate first in the reading path
  (given before asked).
- 1-2s: spotlight — art, name, exact contents with printed numbers, `36 SKR / $2.99`, the arithmetic
  compare, CTA.
- 2-3s: the ladder scan, price ascending, band eyebrows; trust strip last.

**Free-band reality (verified):** the daily chest CANNOT appear in this store today —
`DailyChestController` lives in `DeNelle.Village` and the asmdef runs Village -> Wallet **one way**
(WO-1050 deviation row 4). Until a Core-level status seam exists, the free presence is a
`Tonight, free` **plate under the spotlight**. Do NOT give Free a full-width rail for one promo-door
card; one card in a rail reads broken.

**Motion honesty:** the Lane G3 CTA specular sweep and G4 patronage sheen currently animate a
**dead button** while `RealmStorePurchase` is off. Gate both on `actionable == true` — a shine that
invites a tap nothing answers is the WO-931 defect class in motion form.

**Recorded for the owner, not directed:** SKR is pegged flat ~12.5/$ at every rung, so the 0% dApp
Store fee buys the player nothing visible. Pass some of it through as a stated SKR-rail advantage,
or stop implying a price benefit — an owner pricing call, out of scope here.

### 8. Ergonomic zoning and text floors [SME: accessibility lane — screen px at 2340x1080, ~405ppi]

| Zone | Region (screen px) | Use |
|---|---|---|
| Natural thumb reach | x 0-720 and x 1620-2340, y 480-1080 | ALL commerce actions |
| Stretch | bottom-centre x 720-1620, y 760-1080; side columns y 240-480 | secondary actions, band tabs |
| Hard reach | y 0-240, full width | display-only (the wallet rail lives here — read, not tapped) |
| Dead-centre display | x 720-1620, y 240-760 | featured art, contents ledger, price readout |

- **Primary purchase CTA: bottom-right**, inside x 1750-2290 / y 830-1050 — the right thumb arc.
- **Text floors** (screen px, arm's length): legal/disclaimer **>=30** (34 target) / body and
  contents **>=40** / card names **>=44** / CTA price **>=54**. These are the numbers "too small to
  scan" must meet — consistent with §0.6's reference-unit floor of 30 at scale 1.104.
- **Contrast is numeric:** 4.5:1 body, 3:1 large text — in the regression, not "validate contrast".

## Strict implementation scope

- Night Market/PackStore presentation, shared store component styling, responsive measurements, art wells, scrolling, modal backdrop, and Store-specific presentation regression.
- Catalog-driven rendering must remain intact.
- Existing StoreAurora may be restyled or reduced only if its measured budget and shared-texture behavior remain gated.

## Non-goals

- No edits to `PurchaseGate`, wallet signing, price/catalog authority, transaction construction, verification, reconciliation, fulfillment, feature flags, or backend APIs.
- No SKU, reward, price, or pack-content changes.
- No new scarcity/FOMO language, loot boxes, or combat-power offers.
- No manual `BOARD.html` edit.
- Do not rename existing analytics/FlowTrace events without a separately approved migration.

## Regression and device matrix

Automated/static coverage must prove:

1. All catalog rows still render through shared card classes; no SKU-name layout branch.
2. Critical commerce actions are not ellipsized.
3. The merchandise body owns remaining vertical space and the Close control does not overlap it.
4. Touch floors, safe-area insets, contrast/state labeling, and text wrapping remain within ruled thresholds.
5. Opening/closing the modal does not leave HUD or input layers in the wrong state.
6. StoreAurora remains inside the existing performance/draw-call budget if retained.
7. `SetSurfaceOverride(2340,1080)` is called before every automated layout assertion (§0.8) — a
   batchmode run without it measures the 640x480 portrait branch and proves nothing.
8. The store panel is in the `UICaptureLaunch` enumeration, so WO-1060's clamp/overlap oracle covers
   this screen (`UI_TOUCH_OK` includes it).
9. A **greyscale capture** of Disabled-CTA vs Actionable-CTA side by side is in the evidence set —
   the colourblind gate is a capture, not a claim.
10. Lane G3/G4 motion is verified OFF while `actionable == false` (§7 motion honesty).

Device evidence required at minimum:

| View/state | 2340×1080 Seeker | Additional width check |
|---|---:|---:|
| Store landing, no card focused | required | required |
| Featured pack focused | required | required |
| Lowest merchandise band reached | required | required |
| Long product name + maximum contents | required | required |
| Disabled CTA | required | required |
| Actionable SKR CTA | required | required |

For every required view, capture **before and after** images from equivalent game state. Include full-screen shots (to prove modal/HUD separation) and cropped store shots (to assess typography).

## Acceptance criteria

- At 2340×1080 the store uses the available landscape surface without a large unexplained black cavity.
- `Redeem` and every other commerce verb display in full.
- A player can identify the featured product, exact contents, price/state, and primary action in one scan.
- Pack cards look like members of one design system and product art is legible.
- No text overlap, accidental clipping, or unreadably tiny disclaimer copy appears in the device matrix.
- Store content, HUD, and Close behavior form one clear modal hierarchy.
- Before/after evidence is attached to the implementation result.
- All existing purchase-security and catalog regressions remain green.

## Stop conditions

Stop and return to the owner/commerce lane rather than guessing if:

- The redesign requires changing a price, grant, SKU, availability rule, payment rail, or security authority.
- Required product art does not exist and authoring new paid-offer art is necessary.
- A common modal/pause rule conflicts with active-wave safety semantics.
- The UI can only fit by hiding exact pre-purchase contents or required legal disclosure.
- A proposed shared-class refactor changes transaction or fulfillment behavior.

## Handoff evidence

Implementation result must list exact changed files, regression commands/results, device/build identity, and before/after screenshot paths. The implementing seat must not commit unrelated dirty-tree files.
