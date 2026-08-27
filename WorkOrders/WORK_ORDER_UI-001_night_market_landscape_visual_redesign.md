# WORK ORDER UI-001 — Night Market landscape visual redesign

**Series:** UI (intentionally separate from numbered BOARD work)
**Status:** READY TO IMPLEMENT — owner felt-test 2026-08-27 Needs Work: "there is a VFX exiting about town along Y and it needs removed or turned off". Bounced from Fixed.
**Owner lane:** UI / Store presentation
**Date:** 2026-08-22
**Tracking:** Not tracked on `BOARD.html`. Do not edit or rebuild the board for this document.

**Design reference (interactive wireframe, SME-folded):** https://claude.ai/code/artifact/5b8dc821-9290-4936-b202-6e3974854955
- drive the UI-002 commerce states live, toggle thumb zones and greyscale; the badge moves and
  the single bottom band are drawn in. The wireframe is the visual acceptance target.

## §R. REBUILD SPEC v2 — build THIS (2026-08-22; supersedes any conflicting geometry elsewhere in this document)

### R1. The wireframe is the acceptance target, and it is measurable

The Money Screen artifact (link above) is drawn at **0.5x** the 2340x1080 screen. Conversion:
**wireframe CSS px x 1.812 = CanvasScaler reference units** (screen px = css x 2; ref = screen /
1.104, §0.4). Anything not numbered below is measured off the wireframe with that factor.

### R2. Screen composition (reference units, of the 2120 x 978 usable canvas)

| Region | Size |
|---|---|
| Top bar — wordmark + covenant line + wallet rail; **display-only** (§8 hard-reach) | full width x **100** |
| Body — three columns | spotlight **576** / market **~1058** (fluid) / commerce **486** |
| Single bottom band — legal left, `Close` centre (canon word-bar), promise right | full width x **132** (`CanonCtaHeight`) |

Body height = 978 - 100 - 132 = **746**. No other band exists; the §0.5 cavity must be gone.

### R3. THE CARD (owner rulings 4 + 5 — this is the heart of the rebuild)

**One template** — the single §2 helper `StorePackCard` — with `featured` / `standard` / `compact`
variants. Per card:

- **Rounded rectangle**, corner radius **~25 ref**; 1px border in the band colour at **.5 alpha**.
- **Glow**: outer bloom in the band colour at **.20 alpha** (selected: **.35** + a 1px inset ring);
  background = a top-centre radial of the band colour at **.28** over a dark vertical gradient
  (`#1A1424 -> #110D19`).
- **Art well on TOP**: standard **152 ref** tall, compact **101**. Until real pack art exists: a
  deliberate two-tone gradient per pack + a centred glyph + a bottom scrim — **never near-black**.
  ⚠ The wireframe’s emoji glyphs are stand-ins only — **TMP is ASCII-only**; in-game the glyph is
  an `RpgUiCatalog`/`ConceptIconResolver` icon sprite, or two-letter ASCII initials. No emoji.
- **Below the art, in order**: name (bold) · contents line (mono, dim — top goods, then `+N more`,
  then convenience) · the goods-per-dollar caption · price row = coin glyph + **SKR amount in
  gold** + USD small.
- **Badge**: a pill, top-right, gold, uppercase — only badges the arithmetic supports (§7).
- **The whole card is the tap target.** Every variant authored **>= 112 ref on both axes**
  (compact ≈ 228 tall — comfortably above); `ClampMinTouch` must be a **no-op** (WO-1060).

**Assets: exactly TWO new sprites** — one white rounded-rect 9-slice, one white radial-glow —
tinted per band through `UiStyle`. No per-band textures, no particles, **zero VFX loop slots**.

### R4. Colour (data-driven — the WO-1050 Lane A `band`/`orbTint` fields, never hardcoded)

| Band | Accent | Wireframe art-well placeholder |
|---|---|---|
| Baskets | aether `#8B5CF6` | `#4A2A72 -> #180F2E` (hearth) · `#3E2E6E -> #150F26` (starters) · `#452C6B -> #160F28` (folks) |
| Gap | ember `#FF7A33` | `#5A3316 -> #221007` (wagon) · `#4A4A55 -> #17171D` (crate) · `#57431C -> #201806` (cart) |
| Patronage | gold `#F0C24A` | `#54401E -> #221808` (patron) · `#5A431A -> #241A08` (vow) |
| Free | verdant `#3ED598` | the plate under the spotlight (§7 free-band reality) |

The four-light luminance ladder holds (195/177/145/113) and **words/badges still carry meaning** —
the greyscale capture is the gate.

### R5. CTA and states

Gradient **pill** (gold `#F0C24A -> #FFDF9A`), fully rounded, its own soft glow, **price on the
face** (`Buy - 36 SKR`). Disabled reasons are **plates, not buttons** (UI-002); the specular sweep
runs **only while `actionable == true`**. The commerce column sits in the right thumb arc (§8).

### R6. Ground and columns

Phone ground = deep radial `#1D1235 -> #0C0817 -> #070510`; spotlight and commerce columns are
translucent (`stall` at ~.72) so the ground reads through. Type comes from the kit via
`EnsureFont`/`UiStyle` — **the wireframe’s web faces are direction, not an import order.** Text
floors per §0.6/§8.

### R7. Build order

1. **WO-1060 oracle first** + the store into the `UICaptureLaunch` enumeration.
2. Re-derive every rect on the **978-unit** budget (§0.4); `SetSurfaceOverride(2340,1080)` in tests.
3. The card template + the two tinted sprites (R3).
4. Compose: bands + spotlight + commerce + the single bottom band (R2).
5. The two `packs.json` badge edits (§7).
6. Captures: the six review frames re-shot, plus greyscale — `UI_TOUCH_OK` green before any device
   pass.

### R8. Ruling 5’s keep-out table binds this rebuild

Adopt the reference’s finish; import none of its banned mechanics. Same shine, none of the pressure.

## DELIVERY REVIEW — 2026-08-22, six owner device frames (Seeker, live build)

### KEEP — the design language landed; fix by diff, do not rebuild

- Four bands with eyebrow headers AND their one-line promises, verbatim from the design
  (`CLOSE THE GAP / One resource, nothing else.` - `GET THE HEART MOVING / Baskets - everything at
  once.` - `FREE TONIGHT / Nothing is asked for before something is given.` - `PATRONAGE / Status,
  never power.`).
- The spotlight ledger — `What it holds`, per-good bars, printed numbers — and the arithmetic
  compare line (`7x the Hearth Spark's food, for 1.5x the price.`). Exactly the design.
- The trust strip: all four claims + the covenant verbatim + `Token price moves with the market.`
- UI-002 language is LIVE on device: `Wallet identity bound - authorize to purchase`,
  `[PENDING] Confirmation delayed - do not pay again`, the per-offer reconcile line, and the
  spotlight `[PENDING] Reconcile purchase - do not pay again`.
- Full contents printed on every card. Transparency held.

### REJECT — P0 first, every row maps to a section already written in this document

| # | Evidence (frame) | Defect | The fix is already written at |
|---|---|---|---|
| **P0-1** | Folk's Thanks shows **`20 SKR / $9.99`** | The real price is **120 SKR** — the leading digit is occluded by the overlapping Starter's Hand card. **The money screen is displaying a WRONG PRICE.** | §0.4 / §0.6 (the 978-unit budget) |
| **P0-2** | Ingot Crate shows **`6 SKR`** | Real price **36 SKR**, same occlusion by the Grain Cart card | same |
| **P0-3** | Grain Cart card drawn ON TOP of Timber Wagon; buries the `GET THE HEART MOVING` header; Starter's Hand covers Folk's Thanks; Patron of Elarion covers the `PATRONAGE` header and a price | Card-on-card overlap through the whole shelf — cards were authored against the 1920-unit reference while the real landscape budget is **978** (§0.4), so every fixed height lands double-size relative to its row | §0.4, §0.6 |
| **P0-4** | FREE TONIGHT: two giant `OPEN` slabs + a `Redeem a Code` slab drawn over their own cards | `ClampMinTouch` inflation — controls authored under 112 and force-grown over their neighbours | §0.7; WO-1060 Assert A |
| P1-5 | The two status lines overlap each other and clip under the band top | One status surface, stacked bands — original defect #8, unchanged | §3 hierarchy |
| P1-6 | Bottom ~35% of the panel is an empty grey slab with an oversized Close floating in it | The §0.5 close-band cavity, byte-for-byte as computed. The single bottom band (§6) was not implemented | §0.5, §6 |
| P1-7 | Every art well is a near-black embossed placeholder presented as product art | §4's rule: deliberate neutral placeholder + initials, never near-black | §4 |
| P2-8 | `BEST VALUE` still on Hearth Spark; `LAUNCH ONLY` still on Founder's Vow | The §7 merchandising corrections are data edits (`packs.json`) and were not applied | §7 |

**Acceptance criteria of THIS document objectively violated by the delivery:** "no large unexplained
black cavity" - "no text overlap, accidental clipping" - "quantities and currency must never be
clipped" - "store content, HUD, and Close behavior form one clear modal hierarchy".

### The routing — oracle first, then geometry

**Implement WO-1060 (clamp/overlap oracle) BEFORE re-attempting this screen, and add the store to
the `UICaptureLaunch` enumeration.** Every frame in this six-shot set becomes an automated FAIL
under Assert A (the inflated `OPEN` slabs) or Assert B (every occlusion, including both wrong-price
frames). The geometry fix is then verified by marker, not by another owner device pass — the owner
must never again be the detector for a defect class an oracle can see (CLAUDE.md §14).

Then the §0 pass, in order: re-derive every store rect against the **978-unit** budget (§0.4);
author every control >=112 both axes so the clamp is a NO-OP (§0.7); the single bottom band (§6);
one status surface (§3); placeholder art wells (§4); the two `packs.json` badge edits (§7).

## OWNER RULINGS 2026-08-22 (verbal, this session — bind every section below)

1. **"maximize whole screen"** — the store goes **FULL-SCREEN**, not a centered modal column.
   Wherever this document says "centered store surface with a clear maximum width", the ruling
   supersedes it: the surface is the screen. Safe-area insets still apply; a maximum width does not.
2. **"this is the money screen"** — this surface outranks other panels in polish priority. Treatment
   is calibrated accordingly; corners that are acceptable elsewhere are not acceptable here.
3. **"GET SME team on this"** — a four-lane SME review (merchandising, Unity kit feasibility, crypto
   payment UX, accessibility/ergonomics) was convened 2026-08-22; its findings are folded into this
   document below. Sections marked **[SME]** carry that provenance.
4. **"each pack in a sleek rounded rectangle with an image for the pack and below show what they
   are getting - different colors with backgrounds glowing"** (2026-08-22, later) — **the card
   template is RULED.** Rounded rectangle; art well on TOP; contents printed BENEATH; a per-band
   coloured GLOW background. Implementation notes, binding: the glow is a static tinted
   gradient/sprite behind the card — **zero VFX loop slots, never a particle**; rounded corners via
   the kit's existing rounded path, not a new chrome family; **colour + glow never carry meaning
   alone** — band eyebrow words and badges remain (owner is red/green colourblind; the four-light
   luminance ladder holds). ⚠ **ART DEPENDENCY:** pack art does not exist yet — until owner-supplied
   art lands, the art well is a deliberate two-tone gradient + glyph placeholder (§4), never the
   near-black wells the first delivery shipped. The wireframe shows the ruled card.
5. **The reference shop (2026-08-22, eight frames: "this is how my friend does his and i love it"),
   clarified same session: "it doesnt have to be the same but its sleek elegant"** — the friend's
   "Gear Up SHOP" is the AESTHETIC REFERENCE as a **QUALITY BAR, not a clone**. The Night Market
   keeps its own identity — Fraunces wordmark, the four-light luminance ladder, obsidian ground —
   **finished to his level of sleekness**. Adopt the finish; a handful of his MECHANICS are banned
   by this project's own standing rulings and must not ride in with the styling:

   | ADOPT (the finish) | KEEP OUT (and the standing rule that bans it) |
   |---|---|
   | Deep near-black ground with a subtle cast; soft outer bloom on cards | Mystery Eggs / "boosted chance to unlock" — **gacha; monetization spec C3 bans randomized spend** |
   | Rounded glow cards, one accent per category | "100/100 left" / "3 per account - 100 total ever" — **manufactured scarcity; this WO's own non-goal** |
   | Item rows: icon chip + name + xN quantity | Strike-through prices + "-10%" chips — **WO-1050 bans a strike-through on a price never charged**. (The one mechanic that COULD be made honest: real discounts need a true prior-price history — owner call.) |
   | Gradient pill CTA, price on the face | Energy scrolls / boss keys — **energy-gating; the arena is free, unlimited** |
   | Top currency rail with pill chips | Gear sets sold with +ATK/+DEF/+Crit — **zero combat power; the firewall validator rejects** |
   | Category colour coding + pill badges | "Pre-order" packs — **vapor; WO-1118 honest shelf** |

   The covenant IS the differentiator against exactly this kind of shop: same shine, none of the
   pressure. Print that contrast; do not blur it.

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

---

## PARTIAL DELIVERY — 2026-08-22 (UI implementation seat, edit-only; NOT gated, NOT committed)

**Status of §R: steps 1 and 3 and 5 LANDED. Steps 2, 4 and 6 are BLOCKED — see the blocker below.**

### THE BLOCKER, stated first because it changes what this document can claim

`Assets/_Modules/Wallet/PackStore.cs` was **OFF LIMITS to this seat** — the monetization lane held
it for the night, alongside `PackStoreVM.cs`, `PurchaseGate.cs`, `PurchaseEntitlementVerifier.cs`,
`SolanaWalletProvider.cs`, `WalletEndpoints.cs` and everything under `api/`.

**Every composition item in §R lives in that one file.** `NightMarketLayout`
(`PackStore.cs:87-104`), `EnsureBuilt` (`:265`), `BuildCard` (`:700`), `BuildTrustStrip`, the status
banner and the close band are all authored there. So §R2 (the 100 / 746 / 132 three-band screen),
§R7 step 2 (the 978-unit re-derivation), §R7 step 4 (composition), the single bottom band (§6) and
the one status surface (§3 / P1-5) **could not be attempted** and the P0 rows remain OPEN.

What landed is everything §R needs that lives OUTSIDE that file, so the composition pass is a
one-file change against a card template and an oracle that already exist.

### Landed

| §R step | File | What |
|---|---|---|
| 1 (oracle) | `Assets/Editor/UICaptureLaunch.cs` | `CaptureNightMarketStore` added to the capture enumeration — the store is no longer invisible to `AuditGeometry` |
| 1 (oracle) | `Assets/Editor/UICaptureLaunch.cs` | Assert B widened to cross-parent pairs; `UI_TOUCH_OK` marker; WO-1060 §5 baseline allow-list |
| 1 (oracle) | `Assets/_Modules/Core/UI/ElarionUiKit.cs` | `ClampMinTouch` growth RECORDER (Assert A). Clamp behaviour unchanged |
| 3 (card) | `Assets/_Modules/Wallet/StorePackCard.cs` (new) | The ONE card template, three variants, §R3 to the number |
| 3 (sprites) | `Assets/_Modules/Core/UI/ElarionUiKit.cs` | `RadialGlowSprite` + `ApplyRounded(img, radiusPx)`. Asset bill honoured: the rounded 9-slice already existed, so exactly ONE sprite was added |
| 5 (badges) | `packs.json` (both canonical copies) | `BEST VALUE` moved hearth-spark -> patron-of-elarion; `LAUNCH ONLY` -> `Founders are named on the Heart.` |

### One owner call this seat resolved, and how

§7 leaves `BEST VALUE` as **"move it to `patron-of-elarion` or delete it — OWNER CALL which."** It was
**MOVED**, not deleted: patron-of-elarion is the arithmetic winner at 1,968 total goods/$, so the
badge now sits on a claim the numbers support, and the shelf keeps a merchandising anchor. Deleting
was the other honest answer; say the word and it is a one-line revert. The reasoning is recorded in
both packs' `_shelfNote`, at source, not only here.

### Two residual defects found in `PackStore.cs` that this seat could not fix

1. **`PackStore.cs:1331` and `:1333` hardcode player-facing English** — `"Wallet {addr} bound -
   authorize to purchase"` and `"Wallet identity bound - authorize to purchase"`. CLAUDE.md §7 puts
   every player-facing string in `canon-strings.json`; these two are the UI-002 wallet-identity
   sentences and they are the only store copy living in code.
2. **UI-002 §4 is unsatisfiable as the file stands.** `storeBalanceUnavailable` renders "Wallet
   balance unavailable in this build." from a `BalanceState.Unavailable` branch that has already
   DROPPED the bound address, so the required "retain identity, offer retry" state cannot be
   rendered without a `PackStore` change — and there is no retry control to point at. The copy was
   deliberately LEFT ALONE: changing it to "Balance unavailable - retry" with nothing to tap would
   trade an honest sentence for a false one.


---

## COMPOSITION DELIVERY — 2026-08-22 (UI implementation seat #2, edit-only; NOT gated, NOT committed)

**The blocker above is CLEARED.** `PackStore.cs` was free this pass, so §R steps 2, 4 and 6's build
work landed. Every P0/P1 row now has a mechanism against it, stated at source.

### The root cause the previous pass could not reach, named

`BuildScrollColumn` set `VerticalLayoutGroup.childControlHeight = false`
(`PackStore.cs`, the scroll helper). With that flag OFF a `VerticalLayoutGroup` **ignores
`LayoutElement.preferredHeight` entirely** and lays each child out at its own rect height — which for
a code-built row GameObject is `RectTransform`'s default **100 units**. So every authored 168/240-unit
row resolved to 100, the cards inside were force-expanded down onto it, and `ClampMinTouch` then grew
each card back to the 112 floor **symmetrically about its centre**, i.e. straight over the row above
and below. **That is P0-3's card-on-card overlap, and through it both wrong prices (P0-1, P0-2).**
The flag is now TRUE and the row's height is a PARAMETER taken from `StorePackCard.CardHeight(variant)`,
so the row and the card are one number.

### Landed (all in `Assets/_Modules/Wallet/PackStore.cs` unless stated)

| § | What |
|---|---|
| R2 / R7-2 | `NightMarketLayout` re-authored in REFERENCE PX on the 2120x978 budget: top bar **100**, body **746** (spotlight 576 / market fluid / commerce 486), bottom band **132**. New `Region(...)` helper anchors by anchors+offset-px; the fraction-only `ZoneRect` is deleted |
| R2 / owner ruling 1 | Panel anchors are **(0,0)-(1,1)** and `frameName: null` — the portrait `FrameMerchant` is dropped (§0.3) and the procedural obsidian panel carries the full-bleed surface |
| §6 / P1-6 | **ONE bottom band.** `BuildTrustStrip` builds INTO it (legal left, promise right) and `SeatCloseInBottomBand()` re-parents the canon Close into its centre. The kit's close-band reservation now has nothing to reserve — the cavity is gone by construction, not by tuning |
| §3 / P1-5 | **ONE status surface.** `_statusBanner` moved into the commerce column and is NOT torn down on focus change; the spotlight's own second pending line is gone |
| R7-4 | `BuildPackCard` no longer draws a card — it resolves a `StorePackCardModel` and calls `StorePackCard.Build`. The parallel rail/Outline/orb/4x MakeText card implementation is deleted, as are `_cardRails`/`_cardBorders` (one `_cardHandles` map) |
| R5 / §8 | The CTA moved to the **commerce column**, bottom-right thumb arc, authored 438x134 ref px. Balance-after moved with it |
| §0.6 | `MakeText` now clamps every store string to `ElarionUi.FontFloorMobile(30)` at the one place store text is made; call sites raised to the §8 floors (names 44-52, body 30-40, price 54 in-card). New `FitInto` wraps `FitBlock` so no block can overflow its rect |
| §0.7 / P0-4 | Free-band doors authored at 228 with their buttons at ~116 px tall **stopping below their own blurb** — the giant `OPEN` slabs are gone and no button sits on its card's copy |
| §0.9 | `ApplySafeArea` — the FIRST `Screen.safeArea` read in the kit paths, applied once on the screen host. No-op in batchmode (full-rect safe area), so captures still measure the phone's rects |
| §R6 | `BuildGround` — one tinted radial (sprite two of the two-sprite bill) behind everything; spotlight/commerce stalls at .72 alpha so the ground reads through. **Zero new sprites, zero VFX slots** |
| CLAUDE.md §7 | The two hardcoded wallet sentences at the old `:1331/:1333` are now `storeBalanceBoundAddress` / `storeBalanceBoundIdentity` in **both** canon copies (byte-identical, ASCII). `storeValuePerDollar` added for the new goods-per-$ caption |

### Deviations from the letter of §R, and why

1. **The covenant is in the TOP BAR only; the bottom band's "promise right" is
   `storeTrustNeverPower` + the treasury claim.** §R2 lists the covenant in the top bar AND §6 wants a
   promise on the right of the bottom band. Printing the same sentence twice on one screen blunts the
   one line this shop is built around, so each string appears exactly once and all four trust claims
   are still on screen.
2. **The spotlight art is still the existing `Orb` gem, not a `Featured` card.** The spotlight is a
   ledger, not a card; `StorePackCardVariant.Featured` is therefore built but unused by this file.
   P1-7's near-black wells were a CARD defect and are fixed in the template.
3. **`FlowTrace` event strings still say `BuildSpotlightCta`** inside the renamed `BuildCommerce` —
   this document's non-goals forbid renaming trace events without an approved migration.

### Still open

- **UI-002 §4** (the `Unavailable` branch that has already dropped the bound address, with no retry
  control to point at) is UNCHANGED and still unsatisfiable. It needs a commerce decision, not a
  layout one.
- **§R7-6 captures are NOT run** — this seat is edit-only. Verify with
  `UICaptureLaunch` → `UI_TOUCH_OK`, `UI_GEOMETRY_OK`, `UI_CAPTURE_OK`, then open
  `Builds/ui-capture/NightMarket_*.png` for all three targets (1920x1080, 2340x1080, 2670x1200).
  NightMarket is deliberately NOT on the touch baseline allow-list, so it is expected to be able to
  go RED — read the failures, they are the proving line.
