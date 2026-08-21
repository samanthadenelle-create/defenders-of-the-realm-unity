# WO-1050 — The Night Market: Realm Pack Store presentation redesign

**Status:** READY TO IMPLEMENT (presentation lanes A–E). Lane F is data-only and ships with A.
**Minted:** 2026-08-21 (**UI seat** - Claude UI authored The Night Market)
> **RENUMBERED 1132 -> 1050 on 2026-08-21 (owner instruction).** It was first minted from the CLI
> MAIN LINE; CLAUDE.md s2 gives the UI seat its own reserved block so the two seats can mint in
> parallel WITHOUT reading each other's state, and minting across blocks removes that guarantee.
> No collision occurred (1131 CLI / 1132 UI were distinct) - this restores the invariant rather
> than repairing damage. Banner rows corrected in the same edit: UI seat next free = 1051, CLI
> main line next free = 1132 (released, since nothing occupies it now).
**Lane:** Monetization / UI presentation (CLAUDE.md §9 — Monetization is an isolated lane)
**Class:** PRODUCT. Not a defect. The store is functionally correct and commercially inert.
**Owner ruling this session (2026-08-21):** the Patronage band carries **BOTH** anchors, **stacked** —
Founder's Vow (locked, anchors) above Keeper's Almanac (converts). Verbatim: *"both stacked"*.
**Design source:** the Night Market brief + interactive landscape wireframe —
https://claude.ai/code/artifact/1af66c5d-e41f-480e-a842-ece9772c9c78
**Reads before implementing:** `WORK_ORDER_1117_monetization_profitability_program.md` (program,
BLOCKED — this WO is **not** one of its phases and is not blocked by it),
`WORK_ORDER_1118_honest_sku_shelf_and_ladder.md` (the honest-shelf rule this must not walk back),
`WORK_ORDER_1037_shortfall_pack_offer.md`, `WORK_ORDER_947` §12c (cost-basket guardrails).

---

## 0. One-line truth

**The store works the way a receipt works.**
Five browsable SKUs render as 132 px text rows in a single scroll column: no art, no price
relationship between rows, a `Coming soon` right rail, the Rewards Distributor address as 12 px dim
text, and no SKR balance anywhere on screen. The WO-1118 thinning was correct — you cannot price
vapor — but it left a shelf with nothing to look at.

**This WO changes presentation only.** The money path, the purchase refusals, the catalogue
guardrails and the covenant are all untouched, and every lane below ships green with
`FeatureFlags.RealmStorePurchase` still **OFF**.

---

## 1. What exists today (verified at source, so the next seat does not re-derive it)

| Fact | Source |
|---|---|
| Shelf = one `ScrollRect` column, 132 px cards, `slot_item` plate | `PackStore.BuildScrollColumn` / `BuildPackCard` |
| Four sections, mood-named | `PackStore.SectionTitle` — "For Your Next Adventure" / "Useful Supplies" / "Make the Realm Yours" / "Support Elarion" |
| **5** visible SKUs of 25 | `packs.json` v5 — `hearth-spark`, `starters-hand`, `impulse-{wood,iron,food}-medium` |
| Nine impulse SKUs are shortfall-only | `shelfCurated` absent → `PackStore.Render` skips them |
| Buy CTA replaced by "Coming soon" | `FeatureFlags.RealmStorePurchase` declares `defaultOn: false` |
| SKR is the only rail; SOL/USDC dropped | `_defaultCurrency`, store buy-column fix 2026-07-16 |
| Shortfall resolver exists, never touches money | `ShortfallPackOffer.Resolve` → returns a `PackDef` |
| Daily chest = 500 gold, 1000 with a rewarded lantern | `DailyChestController.BaseGold`, `Claim(BaseGold * 2, "rewarded_double")` |
| Promo door is deliberately ungated | `PackStore.EnsureBuilt` — outside the purchase-flag test |
| Build target is landscape-only | `ProjectSettings.asset` — both portrait autorotates 0 |
| Touch floor 112 px, force-grown by `ClampMinTouch` | `ElarionUiKit.MinTouchPx = 112f` |

---

## 2. The screen

Two columns inside the existing Obsidian modal. **The modal, its frame (`RpgUiCatalog.FrameMerchant`),
its medallion, its shared Close and `ElarionUiKit.StorePanelAnchorMin/Max` are unchanged** — the
owner's 2026-07-15 "all stores same size / matching Y" felt-test ruling still binds.

```
┌ top bar ────────────────────────────────────────────────────────────┐
│ ◉ The Night Market            [1,204 SKR ≈ $96.32] [7xK…4mB]  [✕]   │
├──────────────┬──────────────────────────────────────────────────────┤
│ SPOTLIGHT    │ SHELF  (vertical scroll, four bands, fixed order)    │
│              │                                                      │
│  lit art     │ ▌FREE TONIGHT        chest · lantern · redeem        │
│  badge       │ ▌CLOSE THE GAP       wagon · crate · cart            │
│  name        │ ▌GET THE HEART       spark · starter's hand          │
│  tagline     │   MOVING                                             │
│  ─────────   │ ▌PATRONAGE           Founder's Vow    (locked)       │
│  bar ledger  │                      Keeper's Almanac (locked)       │
│  compare     │                                       ↑ BOTH STACKED │
│  price/CTA   │                                                      │
├──────────────┴──────────────────────────────────────────────────────┤
│ 0% STORE FEE · TREASURY 9Wz…Q4kP · TIME AND BEAUTY, NEVER POWER     │
│                        You are never required to spend anything. Ever│
└─────────────────────────────────────────────────────────────────────┘
```

**Band order is fixed and is not a preference.** Free is first (nothing is asked for before something
is given); Patronage is last so it anchors on the way out.

---

## 3. Lanes

### Lane A — `packs.json` (data, dual copy)

Four **additive** fields on `PackDef`. Parse them in `PackCatalog` beside the existing
`storeVisible` / `shelfCurated` / `storeSection` / `storeBadge` / `legacySkus`.

| Field | Type | Meaning |
|---|---|---|
| `band` | string | `free` \| `gap` \| `basket` \| `patronage`. Replaces `storeSection`'s mood names as the grouping key. |
| `orbTint` | string (hex) | The card gem. Data-authored so merchandising is a data edit, never a code edit. |
| `compareTo` | string (sku) | The SKU the comparison line is drawn against. Absent → no line. |
| `anchorOnly` | bool | Renders fully priced with **no Buy control ever built**. |

Rows to author:

- `hearth-spark`, `starters-hand` → `band: "basket"`, `compareTo` on `starters-hand` = `hearth-spark`.
- `impulse-{wood,iron,food}-medium` → `band: "gap"`, `compareTo` = `hearth-spark`.
- `founders-vow` → `band: "patronage"`, **`anchorOnly: true`**, `storeVisible: true`.
- **NEW ROW** `keepers-almanac` → `band: "patronage"`, `anchorOnly: true`, `storeVisible: true`,
  $9.99 / 120 SKR. See §4 — it is authored as an anchor, not as a product.

⛔ **`storeSection` is NOT deleted and NO `sku` is renamed.** `sku` is the live entitlement key in
`OwnedItemIds`; `keepers-satchel` already carries `lanternlight` in `legacySkus` for exactly this
reason. `band` is read with a fallback to the `storeSection` mapping so a row missing `band` still
lands somewhere sane.

⛔ **Dual copy stays md5-identical** — `Assets/Resources/Data/Canonical/packs.json` and
`Assets/StreamingAssets/Data/Canonical/packs.json`. `ImpulsePackRegression.CaseDualCopy` already
pins this; do not hand-edit one copy.

⛔ **The WO-1118 honest-shelf rule binds every new row.** A visible card may only advertise lines
that are grantable today. `keepers-almanac` advertises a *track*, not contents — see §4.

### Lane B — the shelf (`PackStore.cs`)

Replace the single scroll column with the two-column split. `BuildScrollColumn` becomes the shelf's
inner scroller; `BuildSectionHeader` / `SectionTitle` are replaced by the band head (3 px mark +
mono eyebrow + right-aligned sub-label).

- Cards are `flex 1 1 0` across a band strip, three to a row at 2400 px wide.
- **Size the card strips so `ClampMinTouch` is a no-op**, exactly as the current Buy rail is
  authored to be (`0.70–0.985 × 0.06–0.94` on a 132 px row). A sub-112 px card inflates and stacks —
  that is the precise defect that produced the grey-plate shelf the owner saw clip the frame on
  2026-07-16. Author above the floor; do not rely on the clamp being kind.
- Card content: orb, name, one goods line, price block (SKR large / USD small), optional corner flag.
- `aria`-equivalent selected state = border + 2 px left rail in the band colour **and** the card
  moving to the spotlight. Selection is never colour-only.

### Lane C — the spotlight

- Default target on open: `ShortfallPackOffer.Resolve` against the player's live shortfall. No
  shortfall → `starters-hand`.
- **`ShortfallPackOffer` gains exactly one new caller and no new capability.** It still returns a
  `PackDef` and still never grants, charges or routes. Do not add a purchase path to it — WO-931 is
  that mistake.
- Contents render as a **bar ledger**, one row per good the grant seam actually pays out. Bar scale
  is **shared across all packs** so a bigger pack is visibly bigger. Source the key list from the
  same place `DescribeContents` does, so a good that is granted is never un-drawn.
- The comparison line is **arithmetic over two real SKUs or it is absent**. No adjectives, no
  invented value index. If `compareTo` is missing or the ratio is not computable, draw nothing.
- Balance-after preview sits above the CTA.
- CTA label states what happens in the currency that leaves the wallet: `Buy for 36 SKR`.
  With the purchase flag OFF it reads `Coming soon` in the same slot — the whole screen still works.

### Lane D — the free band

`DailyChestController`'s chest and lantern become band 1 of the shelf; the promo door
(`RedeemCodePanel`) becomes its third card.

⛔ **Reward values and gates are unchanged.** 500 gold, 1,000 with the rewarded lantern, and the
reward may still only ever be granted from a real earned callback — `RewardedAdManager` withholds
on purpose and that is not this WO's to soften.
⛔ **The promo door stays outside the purchase-flag test.** It is built ungated today by design;
moving it into a band must not move it inside a branch it can fall out of.

### Lane E — the trust strip

Promote from footer legalese to a permanent full-width floor. Four claims, each verifiable:

1. `0% STORE FEE — EVERY SKR REACHES THE REALM` (Solana dApp Store takes no platform fee)
2. `TREASURY <WalletService.RewardsDistributorAddress shortened>`
3. `SKR BUYS TIME AND BEAUTY, NEVER POWER` (from `skr_store.json` `disclaimer`)
4. `You are never required to spend anything. Ever.` — **verbatim, italic, right-anchored, last read.**

`PackCatalog.CurrencyDisclaimer` keeps its place in the footer text.

### Lane G — rolling colour (owner request 2026-08-21: *"add rolling colors or whatever is popular"*)

**Yes — and it goes in exactly four places.** The 2026 storefront look is drifting mesh gradients and
iridescent sheen. It reads as expensive when it is *one held note* and as a slot machine when it is
sprinkled everywhere, so the budget is spent on the spotlight and nowhere near the shelf's
information.

| # | Moment | What it does | Timing |
|---|---|---|---|
| G1 | **Aurora drift** behind the spotlight art | Two offset radial gradients in the focused pack's light, drifting on opposed slow paths so the ground never repeats. **This is the rolling-colour moment.** | ~22 s loop, ease-in-out |
| G2 | **Light change on selection** | The aurora *crossfades* from the old band's light to the new one over 400 ms instead of cutting. Selecting a card feels like turning a lamp toward it. | 400 ms |
| G3 | **Specular sweep on the CTA** | A narrow bright band travels across the buy button. The one element that must never look asleep. | every ~6 s, 700 ms |
| G4 | **Patronage sheen** | The two anchor rows carry a slow iridescent gold roll along their top edge. The classic premium-tier tell — and it is the *only* shelf card with motion, which is what marks the tier. | ~14 s loop |

**⛔ The rules that keep it from becoming noise:**

- **Rolling colour never carries meaning.** Band identity stays with the mark, the eyebrow and the
  step in greyscale (§5). Strip every animation and the screen must still be fully readable — that is
  the acceptance test, not a preference.
- **No motion on prices, quantities, ledger bars, badges or the trust strip.** Anything a player
  reads to make a decision holds still.
- **Nothing flashes, strobes or pulses faster than 3 Hz.** No motion sits under body text.
- **The free band's claimable card gets one slow breath** (verdant, ~2.5 s) and **stops the moment it
  is claimed** — it marks availability, not urgency. It is the only pulse on the screen.
- **A settings toggle kills G1–G4** and the store falls back to the flat lights. Wire it to the
  existing reduced-motion preference if one exists; add one if it does not.

**How to build it (mobile, uGUI, no UXML):** one shared animated material with scrolling UVs over a
small gradient texture — **not** per-frame `Color` lerps across many `Graphic`s, which is how a store
modal quietly costs 4 ms a frame on a Seeker. Budget: **≤ 2 extra draw calls and ≤ 1 ms/frame**, and
the whole thing is a `Measure` scope (`FlowTrace.Measure("Store","aurora", warnAboveMs:2f)`) so a
regression self-reports instead of being felt.

### Lane F — regression

Extend `ImpulsePackRegression` (it already carries ten cases including `CaseDualCopy`,
`CaseSingleEconomyKey`, `CaseNotOnTheShelf`):

- `CaseAnchorBuildsNoBuy` — a row with `anchorOnly: true` must produce **no Buy control** in
  `BuildPackCard`, on **either** side of the `RealmStorePurchase` flag.
- `CaseBandIsKnown` — every `storeVisible` row resolves to one of the four bands.
- `CaseCompareIsArithmetic` — `compareTo` names an existing SKU and the two rows share at least one
  economy key, else the line must be absent.
- `CaseVisibleIsGrantable` — the WO-1118 honest-shelf rule, now enforced per band.
- `CaseNotOnTheShelf` **keeps passing unchanged** — the nine uncurated impulse SKUs stay
  shortfall-only. This redesign does not re-tag any row `shelfCurated`.

---

## 4. The Patronage band — owner ruling, both stacked

Owner call 2026-08-21: **both**, stacked, Vow above Almanac.

| Row | Price | Role | Why it is safe to show |
|---|---|---|---|
| **Founder's Vow** | $49.99 / 600 SKR | The anchor. Makes $2.99 read as pocket change. | `anchorOnly: true` — **it cannot be bought, so it cannot disappoint.** The early-access $5 cap is respected because nothing is sellable here. Copy: *"Returns at launch. Founders are named on the Heart."* |
| **Keeper's Almanac** | $9.99 / 120 SKR | The converter. A real product on a near horizon. | Also `anchorOnly: true` **for now** — copy: *"Opens with the season."* It graduates to buyable in WO-1122 when a cosmetic track that actually renders exists. |

⛔ **Both rows ship `anchorOnly` in this WO.** Neither is purchasable. The Vow is over the
early-access cap and its cosmetics do not render; the Almanac's track does not exist yet. Showing a
price without a Buy is the whole mechanism — it anchors while shipping **zero vapor**, which is the
one way to get anchoring past the WO-1118 rule.

The Almanac's card advertises the *shape* of the season, never contents: *"The free track runs
whether you buy or not — twelve of the twenty-four pages cost nothing."* That claim is true today
because it describes a design commitment, not a grant.

---

## 5. Palette — colour is information, and the owner is colourblind

Four lights, one per band. **Chosen so their rec.709 greyscale values step apart**, because the
owner reads this build without reliable hue discrimination and the shelf must survive that read.

| Band | Hex | Greyscale (of 255) | Used for |
|---|---|---:|---|
| Patronage | `#F0C24A` gold | 195 | The realm's own colour, kept rare |
| Free | `#3ED598` verdant | 177 | **Never on anything that costs** |
| Gap | `#FF7A33` ember | 145 | Timber, iron, grain — the stall-fire |
| Basket | `#8B5CF6` aether | 113 | Crystal, premium, the wallet |
| Ground | `#0A0810` / `#16111F` | 16 / 24 | Violet-biased black, not neutral grey |

**Rules that make the palette safe:**
- Every band also carries a **text eyebrow** and a 3 px mark. Colour never carries a message alone.
- Every card state also carries a **word**: `Owned`, `Claim`, `Locked`, `Your gap`.
- Affordability is a **filled bar**, never a green-vs-red swap.
- ⛔ **Do not ask the owner to pick or approve hues.** Ask about behaviour. The gate is the
  greyscale check (memory `owner-colorblind-delegate-visual-creative`).

Type: display **Fraunces 600** (pack names + wordmark), UI **Archivo 500/700** (every control and
label, legible at 9.5 px on a 1080-tall panel), data **IBM Plex Mono 500** (prices, balances,
quantities, signatures). If a face is unavailable in the kit, fall back within the existing
`ElarionUiKit.EnsureFont` path — **do not import a font pack in this WO.**

---

## 6. Copy deck

Shippable as authored. Stall-keeper's voice: plain, warm, specific, never breathless.

| Surface | String |
|---|---|
| Wordmark | The Night Market |
| Band 1 | **Free tonight** · resets `<UTC rollover>` |
| Band 2 | **Close the gap** — one resource, nothing else |
| Band 3 | **Get the Heart moving** — baskets, everything at once |
| Band 4 | **Patronage** — status, never power |
| Gap chip | **Your gap: 1,240 timber** *(the player's real number)* |
| Comparison | 2.3× the Spark's timber, one dollar more. |
| CTA | **Buy for 36 SKR** |
| Micro-line | on-chain receipt · 0% store fee · settles in ~2s |
| Owned | settled 12 Aug · `4f2a…9cD1` |
| Vow | **Returns at launch.** Founders are named on the Heart. |
| Almanac | **Opens with the season.** The free track runs whether you buy or not. |
| Connect | Connect a wallet to finish. Browsing needs nothing. |
| Failure | Nothing was charged. The realm still owes you nothing. |
| Covenant | *You are never required to spend anything. Ever.* |

The timer must be **the real UTC rollover the chest already uses**. A fixed string claiming a reset
that has not happened is the `[[missing:market]]` defect class (PROD-010).

---

## 7. ⛔ What NOT to touch

- **The money path.** `Purchase()`, `WalletService.Pay`, `PackStoreVM.ApplyPackContents`,
  `PackPurchased`, the analytics calls. Unchanged shape.
- **The three payment refusals.** `FeatureFlags.RealmStorePurchase` stays `defaultOn: false`;
  `WalletService.Pay` / `PayFlat` keep refusing; `SolanaWalletProvider` keeps blocking Mainnet.
  **Turning the rail on is WO-1121's call after a device wallet test, not this WO's.**
- **No tappable Buy over a refusing rail.** WO-931 is exactly that defect. `anchorOnly` rows build
  no button at all, and gated rows keep the `Coming soon` placeholder.
- **The nine uncurated impulse SKUs.** They stay shortfall-only. Re-tagging one `shelfCurated` is an
  OWNER call (WO-947 §12c.4).
- **`CloseStore()` and the `MarketplaceInteractor` reflection path.** It is the soft-lock guard.
- **`PanelManager` registration / the modal arbiter.**
- **FlowTrace.** Every `Enter`/`Step`/`Warn`/`Fail` in `PackStore` stays and the new surfaces get
  their own. Instrumentation is permanent (CLAUDE.md §12) — a blank store must keep self-reporting.
- **No `.unity` scene edits. No new `System.Reflection`.**

---

## 8. Acceptance criteria

1. Store opens pre-focused on the player's live shortfall; with no shortfall, on `starters-hand`.
2. Four bands render in the fixed order Free → Gap → Basket → Patronage, each with mark + eyebrow.
3. Patronage carries **both** anchors stacked, Vow above Almanac, **neither with a Buy control**.
4. Tapping any card moves the spotlight; the ledger, comparison line, price block and CTA all update.
5. Bar ledger draws every good the grant seam pays out, on a scale shared across packs.
6. Comparison line is arithmetic over two real SKUs, or absent.
7. Trust strip carries all four claims; the covenant line is verbatim and last.
8. **With `RealmStorePurchase` OFF the entire screen still renders** — bands, spotlight, ledger,
   trust strip — with `Coming soon` in the CTA slot and no tappable Buy anywhere.
9. Free band claims 500 gold / 1,000 with the lantern; the promo door remains reachable with
   purchases off.
10. No card or control is smaller than 112 px on its short side, so `ClampMinTouch` is a no-op and
    nothing inflates or overlaps.
11. Greyscale check: with hue removed, the four bands remain distinguishable and every state is
    still readable from its word.
11b. **Motion-off check:** with rolling colour disabled, the screen is still complete and readable.
    No animation carries information (Lane G).
11c. Aurora + sweep + sheen cost **≤ 2 extra draw calls and ≤ 1 ms/frame** on device, proven by the
    `FlowTrace.Measure` scope, not by eye.
12. Gates green: `COMPILE_GATE_OK`, `REGRESSION_OK <n>/<n> suites` including the four new
    `ImpulsePackRegression` cases, and **`UI_CAPTURE_OK` with the PNGs actually opened** —
    compile-green never proves a panel looks right (memory
    `headless-screenshot-verify-ui-before-build`).

---

## 9. Sequencing

Ships in one lane; the Monetization lane is file-disjoint from World, VFX and Combat (§9), so it can
run alongside them. **Zero payment risk** — every lane lands with the purchase rail still closed.

The store becomes worth looking at before it becomes able to charge. That is the correct order, and
it is what makes WO-1121's eventual Buy-ON a flag flip rather than a redesign.

---

## 10. Files

**Edit:** `Assets/_Modules/Wallet/PackStore.cs` · `Assets/_Modules/Wallet/PackCatalog.cs` ·
`Assets/Resources/Data/Canonical/packs.json` + `Assets/StreamingAssets/Data/Canonical/packs.json`
(dual copy, md5-identical) · `Assets/Editor/Regression/ImpulsePackRegression.cs` · the shared aurora material +
its gradient texture (new, Lane G)

**Read, do not edit:** `ShortfallPackOffer.cs` (one new caller only) · `PackStoreVM.cs` ·
`DailyChestController.cs` · `RedeemCodePanel.cs` · `WalletService.cs` · `FeatureFlags.cs`

**Canon to update in the same commit (§15):** `docs/MASTER_CATALOG/` store section, and the
`_comment` header in both `packs.json` copies naming this WO and the both-stacked ruling.
