# WORK ORDER 1263 — Night Market Mobile Store Redesign

**Status:** IMPLEMENTED — LANDSCAPE REFERENCE CAPTURED; TESTER APK + DEVICE FELT-PASS OWED
**Minted:** 2026-08-28 (CLI; banner bumped 1263 → 1264 in the same edit)
**Provenance:** Grok-authored draft, hand-delivered by the owner 2026-08-28, refined by CLI (canon
addendum below). The owner APPROVED the reference phone mockup this same day — the §2 design gate
for this store's look is satisfied for the SKR/Seeker channel.
**Project:** Elarion / The Night Market
**Target:** Android-first mobile game UI
**Priority:** High
**Owner Intent:** Replace the current desktop-like store presentation with a clean, premium,
card-based mobile storefront matching the approved visual mockup.
**Reference Mockup:** the owner-approved phone mockup (dark charcoal + deep purple + gold; featured
Starter's Hand card, sectioned card rails, footer badges, sticky gold Buy CTA). A side-by-side
recreation (Seeker/SKR vs the future Google Play fiat variant) lives on the design canvas
"Night Market Play Mockups".

**Landscape addendum (owner, 2026-08-28):** `NIGHT_MARKET_LANDSCAPE_REFERENCE.png` and
`WORK_ORDER_NIGHT_MARKET_LANDSCAPE_REDESIGN (1).md` are now the binding landscape target. In
landscape the selected offer owns the left rail, Basket/Patronage offers own the centre shelf,
Actions plus curated Gap offers own the right rail, and the single purchase CTA shares the bottom
action band with Close. Prices remain live provider text; currency lettering is never baked into
reusable art. The existing PackDef, PurchaseGate and PaymentProvider seams remain authoritative.

---

## 0. CANON ADDENDUM (CLI, binding — read before the Grok body below)

- **Code-built UI only. UXML does NOT work in builds** (CLAUDE.md §8, learned the hard way). Every
  component in §35 is built in code like the rest of the HUD.
- **Real file locations** (the Grok draft's Phase-1 "locate" step, pre-answered): store view/VM =
  `Assets/_Modules/Wallet/PackStore.cs` (3299 lines), `PackStoreVM.cs`, `StorePackCard.cs`,
  `PackStoreBootstrap.cs`, `ShortfallPackOffer.cs`; catalog = `PackCatalog.cs` +
  `Assets/Resources/Data/Canonical/packs.json` (+ StreamingAssets copy); wallet balance =
  `WalletService` / `WalletEndpoints`; purchase execution = `PackStore.Purchase` →
  `WalletService.Pay` (quote via `PurchaseQuoteService`); grants =
  `PackStoreVM.ApplyPackContents` → `EconomyService.GrantSpendablePurchased`.
- **§23 data model:** do NOT create the suggested `StoreOffer` interface fresh — `PackDef` /
  `PackCatalog` already carry this shape. Extend, never duplicate (draft's own §23 caveat, made
  binding). SKU ids are live save keys — display names only may change (§34's Grain Cart rename is
  display-side; the `sku` stays).
- **Touch standard:** min touch target in this project is `MinTouchPx = 112` device px (memory:
  mobile-ui-touch-contrast standard) — that supersedes the draft's 48dp where they conflict.
- **WO-1255 interplay (payment seam):** this redesign builds the presentation ONE layer above the
  upcoming `IPaymentProvider` seam. Render every price through a single `PriceLabel`-style
  component fed by the VM, so the Play channel can later swap "560 SKR ~ $4.99" for "$4.99" without
  touching layout. Do not deepen any direct `WalletService` coupling in the new views.
- **Owner is colorblind:** never gate the §29 selected state on hue alone (the draft already
  requires two cues — treat that as HARD). Greyscale check the final capture.
- **Ship gates:** standard ladder — `COMPILE_GATE_OK` + regression + `UI_CAPTURE_OK` with PNGs
  opened at 360dp-class and 412dp-class widths (draft §41), owner felt-verifies on the Seeker.
- The §33 clipped-copy defects are confirmed real (device screenshots 2026-08-28: "Heart gr",
  "1,400 c", "600 coi", "MONTHLY LED…").

---

## 1. Objective

Redesign **The Night Market** store so it feels intentionally built for a modern Android phone instead of a desktop panel squeezed onto a mobile viewport.

The new store must:

- Preserve the existing dark fantasy / medieval identity.
- Use a **dark charcoal + deep purple + gold** visual system.
- Keep SKR as the purchase currency.
- Preserve all existing store products, prices, balances, purchase logic, wallet logic, and transaction behavior unless explicitly called out below.
- Present information through clean cards, sections, resource icons, and a single obvious purchase action.
- Eliminate the current three-column desktop framing, oversized empty right panel, cramped labels, clipped text, and persistent bottom overlay feel.
- Be easy to scan with one hand on a portrait Android device.

This is a **UI/UX restructuring work order**, not a store-economy rebalance.

---

## 2. Design Target

Use the approved mockup as the visual target.

### Overall look

- Premium fantasy marketplace.
- Dark background with subtle purple tint.
- Gold used for hierarchy, active state, borders, prices, and purchase CTA.
- Cream / warm-white body text.
- Soft gradients and restrained glow.
- Rounded cards with thin borders.
- Clean spacing and readable mobile typography.
- Avoid ornate decoration that interferes with readability.

### Important

The production UI should reproduce the **screen content**, not the rendered physical phone shell from the mockup.

The actual game should occupy the device viewport normally. Do **not** add fake Android bezels, a fake camera hole, or a decorative device frame inside the app.

---

## 3. Core UX Change

### Current pattern to remove

The current UI behaves like:

- left-side item detail panel,
- middle product grid,
- right-side status / buy panel,
- large bottom close/footer bars,
- desktop-width content compressed onto a small screen.

### New pattern

Convert the store to a **single vertical mobile flow**:

1. Store header
2. Wallet balance
3. Non-pay-to-win reassurance message
4. Featured / selected offer card
5. Product category sections
6. Horizontally scrollable product cards or compact 2-column mobile grid
7. Store transparency strip
8. Sticky bottom purchase action for the currently selected product

The selected product should remain visually obvious at all times.

---

## 4. Screen Structure

Recommended component hierarchy:

```text
NightMarketScreen
├── SafeAreaContainer
│   ├── NightMarketHeader
│   │   ├── StoreTitle
│   │   └── WalletBalanceChip
│   ├── StorePromiseBanner
│   ├── ScrollView
│   │   ├── FeaturedOfferSection
│   │   │   └── FeaturedOfferCard
│   │   ├── StoreSection: Best Start
│   │   │   └── ProductCardRail / ProductGrid
│   │   ├── StoreSection: Patronage
│   │   │   └── ProductCardRail / ProductGrid
│   │   ├── StoreSection: Resources / Close the Gap
│   │   │   └── ProductCardRail / ProductGrid
│   │   ├── UtilityActionsSection
│   │   │   ├── RedeemCode
│   │   │   ├── SeasonTrack
│   │   │   └── MonthlyLeaderboard
│   │   ├── StoreTransparencyStrip
│   │   └── BottomScrollPadding
│   └── StickyPurchaseBar
│       ├── SelectedOfferSummary
│       ├── PrimaryBuyButton
│       └── SecondaryCloseAction
```

---

## 5. Header

### Store title

Display:

**The Night Market**

Presentation:

- left aligned or visually centered within the main content column,
- gold fantasy serif or existing game display font,
- strong visual hierarchy,
- no giant header that consumes excessive vertical space.

Suggested mobile sizing:

- 28–34sp title
- 1.15–1.25 line-height

### Wallet chip

Place wallet balance in a compact card/chip near the header.

Display:

```text
Your wallet
1,502 SKR
```

Optional:

- small SKR coin/token icon,
- abbreviated wallet address available through a tap or info affordance if still needed,
- network label such as Mainnet should be secondary metadata, not a headline.

Do not force wallet hash, network, and currency balance into one crowded line.

---

## 6. Store Promise Banner

Keep the anti-pressure message visible but cleaner.

Preferred copy:

> You are never required to spend anything. Ever.

Style:

- dark rounded banner,
- small shield / crest icon,
- warm-white body text,
- emphasize **Ever.** in gold,
- compact enough to avoid consuming excessive vertical height.

This should feel reassuring, not like legal boilerplate.

---

## 7. Featured Offer Card

The selected offer gets a full-width featured card near the top of the scroll view.

Default initial selection may remain **Starter's Hand** if that is current behavior.

### Featured card content

Include:

- product artwork / emblem,
- product name,
- short flavor description,
- category or badge,
- resource contents,
- value statement if applicable,
- SKR price,
- approximate USD value.

Example:

```text
BEST START

Starter's Hand
A steady hand for a new tender — everything you need to get the Heart going.

WHAT IT HOLDS
Wood      4,000
Iron      2,000
Crystals    400
Stone     1,500
Coins       600

2.7x the Hearth Spark's wood, for 1x the price.

560 SKR   ~ $4.99
```

### Resource presentation

Replace progress-bar-looking gray lines with recognizable resource tiles or icon/value cells.

Preferred layout:

```text
[wood icon]      [iron icon]      [crystal icon]
4,000            2,000            400
wood             iron             crystals

[stone icon]     [coin icon]
1,500            600
stone            coins
```

For wider phones, allow all five resource cells in one row if legibility remains high.

### Artwork

The mockup uses a mystical glowing hand for Starter's Hand.

Implementation should support a product-specific artwork asset rather than hardcoding initials like `SS`, `FS`, or `PB` as the primary visual treatment.

Initials may be retained only as temporary fallback art.

---

## 8. Product Sections

Use distinct store sections with a simple heading and optional **View all** action.

Recommended sections:

### Best Start

Products may include:

- Starter's Hand
- Folk's Thanks
- Permanent Builder
- other onboarding / progression products

### Patronage

Use for permanent, cosmetic, supporter, or status-oriented products.

Important design principle:

**Status, never power.**

If that principle exists in the current store messaging, retain it as subtle section helper text.

### Resources / Close the Gap

Use for single-resource convenience purchases.

Products may include:

- Timber Wagon
- Ingot Crate
- Stone resource pack
- Crystal resource pack
- other one-resource packs

Optional helper text:

**One resource, nothing else.**

---

## 9. Product Cards

Cards must be reusable components driven by store data.

### Card anatomy

```text
[badge - optional]
[art / icon]
Product Name
Short descriptor - optional
Resource summary - optional
Price in SKR
Approx. USD
```

### Example cards

#### Folk's Thanks

- 9,000 wood
- 4,500 iron
- 900 crystals
- 3,400 stone
- 1,400 coins
- 1,120 SKR
- approximately $9.99

#### Starter's Hand

- 4,000 wood
- 2,000 iron
- 400 crystals
- 1,500 stone
- 600 coins
- 560 SKR
- approximately $4.99

#### Permanent Builder

- Permanent builder
- +1 crew
- 1,120 SKR
- approximately $9.99

#### Timber Wagon

- single wood resource product
- 336 SKR
- approximately $2.99

#### Ingot Crate

- single iron resource product
- 336 SKR
- approximately $2.99

### Card behavior

On tap:

1. mark the card selected,
2. update the Featured Offer card,
3. update the sticky purchase bar,
4. maintain scroll position unless product detail expansion requires otherwise.

Do not immediately purchase on product-card tap.

Purchase only occurs from a dedicated buy action.

---

## 10. Product Grid / Rail Behavior

Preferred behavior for Android portrait:

### Option A — Horizontal rails

Each category uses a horizontally scrollable rail of cards.

Advantages:

- familiar mobile store pattern,
- keeps screen vertically compact,
- allows larger card art.

Recommended card width:

- ~42–48% of viewport width,
- display about 2.1 cards so the user can visually detect horizontal scrolling.

### Option B — 2-column responsive grid

Use if the existing app architecture strongly favors grids.

Requirements:

- exactly 2 columns on standard portrait phone widths,
- no 3-column layout below tablet breakpoint,
- cards must never clip price labels or resource data.

### Preferred implementation

Use horizontal rails for curated / featured categories and a 2-column grid for commodity resource packs if needed.

---

## 11. Sticky Purchase Bar

Replace the large detached right-side buy panel with a bottom sticky mobile CTA.

### Layout

```text
Selected: Starter's Hand
560 SKR  ·  ~ $4.99

[ Buy — 560 SKR ]

Close
```

A more compact implementation may omit the duplicate selected-product title if context is already obvious.

### Buy button

Requirements:

- full-width or nearly full-width,
- minimum 48dp tap height,
- preferably 52–60dp,
- gold fill or strong gold accent,
- dark text,
- clearly enabled / disabled states,
- include loading/progress state after activation,
- prevent duplicate transaction submission while pending.

### Close action

- visually secondary,
- text button or subtle icon + label,
- not equal visual weight to Buy,
- should not occupy a giant footer bar.

---

## 12. Purchase States

The sticky purchase area must support these explicit states.

### Ready

```text
Buy — 560 SKR
```

### Insufficient balance

```text
Need 560 SKR
Wallet: 420 SKR
```

Button disabled unless an existing token-acquisition path is available.

### Wallet not connected

```text
Connect Wallet
```

Do not show a purchase CTA that implies payment can proceed.

### Transaction pending

```text
Confirming purchase…
```

- disable repeated taps,
- optional spinner,
- keep user on the store screen.

### Success

Show concise confirmation:

```text
Purchase complete
Starter's Hand added to your realm.
```

Then refresh:

- wallet balance,
- relevant resources,
- product eligibility / ownership state.

### Failed / rejected

Display actionable copy.

Examples:

```text
Purchase cancelled.
No SKR was spent.
```

or

```text
Transaction failed.
Try again.
```

Never leave the CTA stuck in a loading state.

---

## 13. Permanent Product Handling

Products such as **Permanent Builder** require ownership awareness.

If already owned:

- replace purchase CTA with **Owned**,
- disable repurchase unless game rules explicitly allow stacking,
- show ownership state on the product card,
- do not rely only on backend rejection.

If multiple permanent-builder purchases are intentionally allowed, surface the rule clearly.

Do not infer this. Use existing business logic.

---

## 14. Badges

Allowed badges:

- Best Start
- Best Value
- Owned
- Limited, only if actually limited
- New, only if actually new

Badge rules:

- max one promotional badge per product card,
- gold pill treatment,
- compact uppercase or title case,
- must not cover important artwork or text,
- no giant banners consuming the entire card header.

---

## 15. Utility Actions

Current utility actions such as:

- Redeem a Code
- Season Track
- Monthly Leaderboard

should not visually compete with products.

Move them into a small **More** or utility section.

Recommended layout:

- compact pill buttons,
- compact 3-item row if width permits,
- otherwise horizontal scroll or 2-column layout.

These are navigation tools, not primary purchases.

---

## 16. Footer / Transparency Messaging

Retain the useful transparency language from the current store, but compress it into a polished information strip.

### Message 1

**0% STORE FEE**  
Every payment reaches the realm.

### Message 2

**TOKEN PRICE MOVES**  
with the market.

Use small supporting icons, such as:

- shield / crown / realm icon,
- scales / token market icon.

Do not use this area as a giant fixed footer.

---

## 17. Wallet / Chain Metadata

The current top-right display contains information similar to:

- abbreviated wallet address,
- Mainnet,
- SKR,
- wallet balance.

Refactor as follows:

### Always visible

- `1,502 SKR`

### Secondary metadata

- abbreviated wallet,
- network,
- token symbol if not already obvious.

Place secondary metadata behind:

- info icon,
- wallet details sheet,
- expandable chip,
- or a small second line if space permits.

Do not let blockchain metadata dominate the store title.

---

## 18. Responsive Layout Requirements

### Primary target

Android portrait phones.

Support at minimum:

- 360 × 800 logical viewport class
- 393 × 873 logical viewport class
- 412 × 915 logical viewport class

Also remain usable on:

- shorter 360 × 720 phones,
- large phones,
- Android landscape,
- tablet widths.

### Breakpoint guidance

#### Under 600dp width

- one main content column,
- featured card full-width,
- product sections use horizontal rails or 2-column grid,
- no side panels.

#### 600–839dp

- allow wider cards,
- optional 2-column content regions,
- retain clear product hierarchy.

#### 840dp+

Tablet may use multi-column layout, but it must be a deliberate responsive design, not restoration of the current cramped 3-panel structure.

---

## 19. Spacing System

Use a consistent spacing scale.

Suggested values:

```text
4dp   micro
8dp   tight
12dp  compact
16dp  standard
20dp  section interior
24dp  section separation
32dp  major separation
```

Recommended screen horizontal padding:

- 16dp minimum
- 20dp preferred on standard phones

Avoid content touching screen edges.

---

## 20. Typography

Use the game's fantasy display font sparingly.

### Display font

Use for:

- The Night Market
- section headers
- major product names if still easy to read

### UI font

Use a highly legible sans-serif for:

- resource values,
- prices,
- buttons,
- descriptions,
- wallet information,
- helper text.

Suggested hierarchy:

```text
Store title:            30–34sp
Featured product:       24–28sp
Section heading:        18–20sp
Product card title:     16–18sp
Price:                  18–22sp
Body:                   14–16sp
Metadata:               12–14sp
```

Do not use italics for large blocks of copy.

---

## 21. Color / Surface Tokens

Do not hardcode arbitrary colors throughout components. Create or reuse theme tokens.

Suggested conceptual tokens:

```text
nightMarket.bg.primary
nightMarket.bg.surface
nightMarket.bg.surfaceRaised
nightMarket.purple.deep
nightMarket.purple.glow
nightMarket.gold.primary
nightMarket.gold.muted
nightMarket.text.primary
nightMarket.text.secondary
nightMarket.text.muted
nightMarket.border.default
nightMarket.border.selected
nightMarket.success
nightMarket.error
```

Visual target:

- background: near-black charcoal with slight cool/purple bias,
- selected cards: deep purple glow / border,
- gold: warm antique-gold, not neon yellow,
- primary text: warm ivory,
- secondary text: muted stone/gray.

---

## 22. Resource Icon Rules

Every resource should have a stable, recognizable icon.

Required:

- wood
- iron
- crystals
- stone
- coins

Rules:

- transparent-background assets,
- consistent visual scale,
- no emojis in final production UI,
- same icon used across store and game HUD when practical,
- do not substitute text initials unless icon is missing.

---

## 23. Data Model Expectations

The UI should be data-driven.

Suggested shape:

```ts
interface StoreOffer {
  id: string;
  name: string;
  shortName?: string;
  category: 'best-start' | 'patronage' | 'resources' | string;
  description?: string;
  artwork?: string;
  badge?: 'best-start' | 'best-value' | 'owned' | 'new' | 'limited';
  skrPrice: number;
  usdEstimate?: number;
  resources?: {
    wood?: number;
    iron?: number;
    crystals?: number;
    stone?: number;
    coins?: number;
  };
  permanentEffect?: {
    label: string;
    amount?: number;
  };
  owned?: boolean;
  purchasable: boolean;
}
```

Adapt to the existing project model rather than creating duplicate state if equivalent structures already exist.

---

## 24. USD Price Handling

USD price is approximate because SKR moves with the market.

Display format:

```text
560 SKR
~ $4.99
```

Rules:

- SKR is the authoritative transaction amount.
- USD is display-only reference value unless current business logic says otherwise.
- Approximation marker `~` must remain.
- Refresh USD estimate when the pricing source updates.
- Do not imply a guaranteed fiat price when token value changes.

---

## 25. Accessibility

Minimum requirements:

- 48dp minimum touch targets.
- Text contrast suitable for dark-mode UI.
- Do not communicate selected state through color alone.
- Product cards should expose accessible names.
- Resource icons should have semantic labels where applicable.
- Respect font scaling where architecture allows it.
- Avoid tiny 9–10px-equivalent labels.

---

## 26. Motion

Keep animation restrained.

Allowed:

- subtle selected-card glow,
- brief card elevation/scale feedback,
- smooth horizontal rail movement,
- wallet value count transition,
- purchase confirmation sparkle / shimmer,
- subtle featured artwork ambient effect.

Avoid:

- constant pulsing purchase buttons,
- aggressive flashing,
- excessive particle effects behind text,
- animations that block interaction.

---

## 27. Loading / Skeleton State

The screen must not jump chaotically while store data loads.

Provide skeleton placeholders for:

- featured card,
- product cards,
- wallet balance.

If product data fails:

```text
The Night Market could not be loaded.
[ Try Again ]
```

Do not render empty card frames with unexplained blank space.

---

## 28. Empty Sections

If a category has zero eligible products:

- hide the section entirely,
- do not render an empty category heading,
- do not leave large blank vertical gaps.

---

## 29. Selected Product State

A selected product must be obvious through at least two cues:

- gold or purple selected border,
- elevated surface / glow,
- check marker,
- `Selected` text.

The sticky CTA must always match the selected product.

Example:

If user taps `Permanent Builder`, the bottom CTA must immediately become:

```text
Buy — 1,120 SKR
```

Never allow the selected card and CTA price to become desynchronized.

---

## 30. Scroll Behavior

- Header may scroll normally or use a compact sticky title bar after collapse.
- Purchase CTA remains anchored at the bottom.
- Add bottom padding to ScrollView so the last product is not hidden behind the sticky CTA.
- Do not use nested vertical scrolling regions.
- Horizontal product rails may scroll horizontally inside the single main vertical ScrollView.

---

## 31. Safe Areas

Respect:

- status bar,
- punch-hole camera / display cutouts,
- gesture navigation area,
- bottom system inset.

The sticky CTA must sit above the Android gesture area.

---

## 32. Existing Store Logic That Must Not Change

Unless existing code is broken, preserve:

- wallet connection logic,
- SKR balance retrieval,
- product eligibility,
- price calculation,
- purchase / transfer instructions,
- transaction submission,
- transaction confirmation,
- reward distribution,
- inventory/resource crediting,
- permanent entitlement handling,
- code redemption logic,
- season track behavior,
- leaderboard navigation.

This work order is primarily a view / interaction refactor.

Do not rewrite blockchain or purchase plumbing merely to accommodate the redesign unless technically required.

---

## 33. Current Copy Cleanup

Fix current presentation defects such as clipped copy:

Current visible issue example:

```text
A steady hand for a new tender —
everything you need to get the Heart gr
```

Expected:

```text
A steady hand for a new tender —
everything you need to get the Heart going.
```

All body text must wrap naturally.

No product name, price, balance, section label, or description may clip at standard Android widths.

---

## 34. Naming Cleanup

The current resource section appears to show a stone-related item under the title **Grain Cart**.

Since the game resource model has moved away from food/grain, audit store labels and remove stale food terminology.

Use a stone-appropriate product name, for example:

- Stone Cart
- Quarry Cart
- Mason's Load
- Stone Wagon

Do not silently change product IDs or backend references. Only update display naming unless the existing architecture requires synchronized metadata.

---

## 35. Componentization

Do not build the new screen as one monolithic component.

At minimum extract reusable components for:

```text
NightMarketHeader
WalletBalanceChip
StorePromiseBanner
StoreSectionHeader
FeaturedOfferCard
StoreProductCard
ResourceAmount
OfferBadge
StoreTransparencyStrip
StickyPurchaseBar
PurchaseStatusMessage
```

Use existing design-system primitives where available.

---

## 36. Implementation Sequence

Recommended execution order:

### Phase 1 — Inspect

1. Locate current Night Market view.
2. Locate store product definitions/data source.
3. Locate wallet balance source.
4. Locate purchase handler.
5. Locate reward-distribution success handling.
6. Locate resource icon assets.
7. Identify mobile viewport / responsive framework already in use.

### Phase 2 — Preserve behavior

1. Record existing product IDs.
2. Record existing SKR prices.
3. Record product rewards.
4. Record permanent-item entitlement logic.
5. Record navigation actions.
6. Add or update UI tests before major refactor where practical.

### Phase 3 — Build new components

1. Header.
2. Wallet chip.
3. Promise banner.
4. Featured card.
5. Product card.
6. Category section.
7. Transparency strip.
8. Sticky purchase bar.

### Phase 4 — Wire selection state

1. default selection,
2. card tap,
3. featured card update,
4. CTA update,
5. ownership state,
6. balance validation.

### Phase 5 — Wire transactions

Reconnect existing transaction logic to the new CTA.

Do not duplicate transaction logic.

### Phase 6 — Responsive pass

Validate at:

- 360px width
- 393px width
- 412px width
- short-height phone
- landscape
- tablet

### Phase 7 — Polish

- typography,
- spacing,
- badges,
- subtle motion,
- loading states,
- errors,
- accessibility.

---

## 37. Acceptance Criteria

### Layout

- [ ] Store is a single-column mobile-first experience on Android portrait.
- [ ] No desktop-style left/middle/right panels remain below tablet breakpoint.
- [ ] No giant empty right-side region remains.
- [ ] No persistent oversized `Close` block remains.
- [ ] Main content never sits beneath the sticky CTA.
- [ ] All content fits 360dp width without horizontal page overflow.

### Header

- [ ] `The Night Market` is clearly visible.
- [ ] Wallet SKR balance is clearly visible.
- [ ] Wallet/network metadata does not crowd the title.
- [ ] Anti-pressure message remains visible.

### Featured product

- [ ] Selected product has a large featured card.
- [ ] Featured card includes product name.
- [ ] Featured card includes product description where available.
- [ ] Featured card includes product-specific artwork or fallback.
- [ ] Featured card shows resource amounts using icons.
- [ ] Featured card shows SKR and approximate USD value.

### Product browsing

- [ ] Products are grouped into meaningful sections.
- [ ] Product cards are reusable components.
- [ ] Product cards are readable at Android phone width.
- [ ] Selecting a product does not accidentally purchase it.
- [ ] Selection updates the featured card.
- [ ] Selection updates sticky buy CTA.

### Purchase CTA

- [ ] Buy button is visible and easy to reach.
- [ ] CTA uses current selected-product SKR price.
- [ ] CTA cannot submit twice while transaction is pending.
- [ ] Insufficient balance has a clear state.
- [ ] Wallet disconnected has a clear state.
- [ ] Purchase rejection has a clear state.
- [ ] Purchase success refreshes balance/resources.

### Content

- [ ] No clipped text.
- [ ] No stale food/grain terminology remains for stone products.
- [ ] Resource icons match wood/iron/crystal/stone/coin.
- [ ] USD values remain marked approximate.
- [ ] `0% STORE FEE` message remains.
- [ ] token-price volatility message remains.

### Regression

- [ ] Existing purchase amounts are unchanged unless separately approved.
- [ ] Existing product rewards are unchanged unless separately approved.
- [ ] Existing transaction flow still works.
- [ ] Existing wallet connection still works.
- [ ] Existing redeem-code behavior still works.
- [ ] Existing season-track navigation still works.
- [ ] Existing leaderboard navigation still works.

---

## 38. Visual QA Checklist

Compare the implemented screen to the approved reference mockup.

The implementation should feel:

- clean,
- premium,
- mobile-native,
- readable,
- intentional,
- consistent with the fantasy world.

Reject the implementation if it still feels like:

- a desktop modal shrunk onto a phone,
- a spreadsheet of products,
- three unrelated panels,
- giant unused negative space,
- text layered over gradients without hierarchy,
- a crypto dashboard with a game skin.

The blockchain should be **under the hood**. The player should experience a fantasy market first.

---

## 39. Suggested Visual Hierarchy

```text
THE NIGHT MARKET                      [1,502 SKR]

[ You are never required to spend anything. Ever. ]

BEST START
┌─────────────────────────────────────┐
│ ART   Starter's Hand                │
│       Description                   │
│                                     │
│       wood iron crystal stone coin  │
│       values...                     │
│                                     │
│       560 SKR        ~ $4.99        │
└─────────────────────────────────────┘

BEST START                            View all ›
[ Folk's Thanks ] [ Permanent Builder ] →

PATRONAGE                             View all ›
[ Timber Wagon ]  [ Ingot Crate ] →

RESOURCES                             View all ›
[ Stone Pack ]    [ Crystal Pack ] →

[ 0% STORE FEE ]   [ TOKEN PRICE MOVES ]

────────────────────────────────────────
[             Buy — 560 SKR             ]
                  Close
```

---

## 40. Definition of Done

This work order is complete when:

1. The current Night Market mobile view has been replaced by the new mobile-first structure.
2. The new UI visually matches the approved reference direction.
3. Store behavior and wallet transactions remain intact.
4. All listed Android viewport classes pass visual inspection.
5. All acceptance criteria are met.
6. No clipped text or overlapping panels remain.
7. The selected product, displayed price, and submitted purchase amount are guaranteed to stay synchronized.
8. Permanent-product ownership states are respected.
9. Stone terminology is used consistently where food/grain was retired.
10. The production UI contains no fake phone-device frame.

---

## 41. CLI Execution Instruction

Before changing code, inspect the existing Night Market implementation and identify the files/components responsible for:

- layout,
- offer data,
- wallet state,
- purchase execution,
- reward fulfillment,
- product ownership,
- store navigation.

Then implement this redesign using the existing architecture whenever practical.

**Do not replace working business logic with guessed logic.**

If implementation details conflict with this work order, preserve the functional behavior and adapt the presentation. Flag only genuine blockers where a design requirement cannot be achieved without changing game rules or transaction semantics.

When finished, provide:

1. list of files changed,
2. concise implementation summary,
3. any assumptions made,
4. any unresolved visual differences from the reference,
5. test results,
6. screenshots at approximately 360dp and 412dp widths.

---

## 42. CLI IMPLEMENTATION — 2026-08-28

### RCA

The existing store was responsive only inside a landscape assumption. `NightMarketComposition`
could choose a three-column landscape or a narrower two-column landscape, but a portrait phone was
sent through that same width breakpoint. It therefore retained a desktop left/right split and could
hit the layout's declared width-deficit clamp. The view already had one authoritative composition
owner, data-driven cards, selection-to-CTA synchronization, safe-area handling, and preserved
purchase plumbing; replacing those would duplicate working systems.

### Implemented

- Added a derived `PortraitSingleColumn` composition selected by aspect before landscape
  breakpoints: selected-offer spotlight at top, scrollable two-up product shelf in the middle, and
  the existing purchase/status rail full-width and sticky at the bottom.
- Kept every price, grant, SKU, wallet call, purchase refusal, fulfillment path, utility action,
  and PackStore selection callback unchanged.
- Added explicit shelf height to the shared layout plan so runtime and regression consume the same
  geometry rather than maintaining a test-only portrait layout.
- Extended the live-RectTransform oracle to 360x800, 393x873, 412x915, and a 412x915 cutout/gesture
  surface. It asserts portrait chooses the one-column mode, landscape never does, body regions are
  disjoint and safe-area contained, two real cards remain above their width floor, the Buy control
  remains above the touch floor, and required commerce glyphs are not clipped.

### Deliberate cut / still required

This implementation delivers the structural mobile breakpoint without inventing a parallel set of
ten view classes or changing commerce logic. Final status still requires the standard Unity compile
and full regression gates, `UI_CAPTURE_OK`, opened 360/412-class PNGs, and owner felt-validation on
Seeker. Product-specific paid-offer art remains an external content dependency; the existing honest
fallback art stays in use where art is absent.
