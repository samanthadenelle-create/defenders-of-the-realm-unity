# WORK ORDER 1246 RESULT — Store SKU visibility inventory + engineering redeemers

**WO Status:** left READY TO IMPLEMENT (do not flip).
**Silo:** store catalog / grant / redeemer. Settlement rail, `api/admin/db.js`, `stats.js` untouched.
**Money:** mainnet is live. No SKU id deleted or renumbered. No wallet / email / real name logged.

Sources (not summaries): `WorkOrders/WORK_ORDER_1165_pack_lineup_monetization_review.md` §8–§9,
`Assets/Resources/Data/Canonical/packs.json` (StreamingAssets twin byte-identical after this
change), `Assets/Resources/Data/Canonical/battle_monthly.json` `monthlyCards[]`,
`PackCatalog.IsOnBrowsableShelf`, `PackStore.PacksInBand`, `PackCatalog.IsRedeemableConvenience`,
`Lantern.cs`, `HarvestBoostService.cs`, `CosmeticApplyRegression.cs`.

Visibility = `PackCatalog.IsOnBrowsableShelf` (the helper `PackStore.PacksInBand` now calls):
`StoreVisible && !(Impulse && !ShelfCurated)`. JSON-omitted `storeVisible` deserializes **true**.

Do **not** read this table as "9 hidden". Causes are not blended.

---

## Inventory — every SKU, one row, one cause

| SKU | USD | Shelf? | Why visible or hidden | Cause class |
|---|---:|:---:|---|---|
| `hearth-spark` | 4.99 | no (`storeVisible:false`) | At $4.99 it is strictly dominated by `starters-hand` (more of all five resources). Kept in catalog as `DEVNET_CANARY_SKU` / quotable. `_hiddenReason` cites WO-1069. | **Dominated pricing** (owner) |
| `keepers-satchel` | 4.99 | no (`storeVisible:false`) | 900 crystals only. WO-1165 §8: 180 crystals/$ vs 321 at the same price — do NOT unhide. `_hiddenReason` still says "blocked on WO-1119 (harvest_boost redeemer)"; that token is **no longer in contents**, so the leftover engineering reason is stale. Remaining live reason is the worse crystals/$. | **Dominated pricing** (owner) |
| `folks-thanks` | 9.99 | **yes** | Ladder basket. Economy + `lantern-oil-2x-expedition` (Lantern.cs redeemer). | Visible — working grant |
| `patron-of-elarion` | 19.99 | **yes** | Ladder basket. Economy + `lantern-oil-3x-expedition`. | Visible — working grant |
| `founders-vow` | 49.99 | **yes** | Ladder top. Economy + 12× lantern-oil-3x. Founder-name-on-Heart badge already stripped (WO-1165 §9.1). | Visible — working grant |
| `frostfall-bundle` | 9.99 | no (`storeVisible:false`) | Same $9.99 bag as `embergrove-bundle` (4 cosmetics + 1200 crystals / 400 stone / 800 coins + 3× instant-build). One product, two SKUs. Art is preview-tint only (`cosmetics.json` has `previewColor`, no mesh art — CosmeticApplyRegression). instant-build now has a redeemer (this WO) but the row stays hidden. | **Duplicate clones** (owner). Cosmetic tint is a **proven fifth**, not blended into clones. |
| `embergrove-bundle` | 9.99 | no | Identical contents and price to frostfall. | **Duplicate clones** (owner) |
| `bloomtide-bundle` | 4.99 | no | Third seasonal clone (half price, half bag, still the same shape: 4 cosmetics + economy + instant-build). | **Duplicate clones** (owner) |
| `starters-hand` | 4.99 | **yes** | $4.99 entry basket. Economy + 2× lantern-oil-2x. | Visible — working grant |
| `echo-patron-pack` | 19.99 | no (`storeVisible:false`) | Economy (works) + harvest-auto-collect + instant-build (were vapor; **redeemers shipped this WO**) + lantern-oil-3x (already live) + 2 cosmetics (tint-only). `_hiddenReason` "over-cap $19.99" is **stale** (WO-1121 put the $49.99 ladder back). Not unhidden. | Was **tokens with no redeemer** (engineering — now redeemed). Stays hidden for **cosmetic tint (fifth)** + not re-ruled onto the shelf. |
| `hero-wardrobe-pack` | 9.99 | no | 4 Knight cosmetics (tint-only) + light economy + 1× instant-build. | **Cosmetic tint (fifth)**; instant-build now redeemed. Not a clone of frostfall (different contents). |
| `realm-defender-bundle` | 9.99 | no | Weapon/shield flair + banner cosmetics (tint-only) + economy + 2× instant-build. | **Cosmetic tint (fifth)** |
| `builders-cache` | 19.99 | no (`storeVisible:false`) | 15× instant-build + 15× instant-repair + 2× xp-weekend (all three were inert buffs / no-redeemer; **redeemers shipped this WO**) + economy + 2 cosmetics. `_hiddenReason` over-cap is stale. | Was **inert buffs / no redeemer** (engineering — now redeemed). Stays hidden for **cosmetic tint (fifth)** + not re-ruled onto the shelf. |
| `impulse-wood-small` | 1.99 | no | 1000 wood. JSON omits `storeVisible` (defaults true) but `impulse && !shelfCurated` keeps it off the Night Market. Strictly dominated at $1.99 by any mixed basket that also grants ≥1000 wood. Reachable only via `ShortfallPackOffer`. | **Dominated pricing** (owner) **and** shortfall-only merchandising (see fifth/sixth below). Not blended: the *shelf hide* is `shelfCurated`; the *do-not-promote* reason is domination. |
| `impulse-wood-medium` | 2.99 | **yes** (`shelfCurated`) | 3500 wood. Owner ruling 2026-08-21 "Middle — one impulse tier per resource". | Visible — working grant |
| `impulse-wood-large` | 4.99 | no | 8000 wood. Shortfall-only. Collides with `starters-hand` at $4.99 on a single key (basket wins on the other four). | Shortfall-only (not a defect). Dominated-as-shelf-row vs `starters-hand` if promoted. |
| `impulse-iron-small` | 1.99 | no | 400 iron. Same shape as wood-small. | **Dominated pricing** (owner) + shortfall-only |
| `impulse-iron-medium` | 2.99 | **yes** (`shelfCurated`) | 1200 iron. | Visible — working grant |
| `impulse-iron-large` | 4.99 | no | 3000 iron. Shortfall-only. | Shortfall-only |
| `impulse-stone-small` | 1.99 | no | 1000 stone. `legacySkus: [impulse-food-small]`. Grain copy still authored (WO-1165 §7) — not a visibility cause. | Shortfall-only |
| `impulse-stone-medium` | 2.99 | **yes** (`shelfCurated`) | 3500 stone. `legacySkus: [impulse-food-medium]`. | Visible — working grant |
| `impulse-stone-large` | 4.99 | no | 8000 stone. `legacySkus: [impulse-food-large]`. | Shortfall-only |
| `impulse-crystals-small` | 1.99 | no | 250 crystals. None of the crystal rungs carry `shelfCurated` (the middle-three ruling named wood/iron/stone only). | Shortfall-only |
| `impulse-crystals-medium` | 2.99 | no | 700 crystals. | Shortfall-only |
| `impulse-crystals-large` | 4.99 | no | 1600 crystals. | Shortfall-only |
| `permanent-builder` | 9.99 | **yes** | Authored by **WO-1253**, not this ticket. This seat did **not** add, reprice, or merge it. Grant path is SKU ownership → `BuildTimerService.SlotCount` (`permanent-builder` kind is redeemable via ownership, not GearInventory). | Visible — WO-1253 concurrency entitlement |
| `monthly-wayfarer` | 4.99 | **yes** (Monthly Ledger panel, not Night Market cards) | 30-claim pool. `MonthlyCardService.ActivateCard` + `RewardGrantWriter` (purchased, uncapped). Exclusive cosmetic deliberately empty. | Visible — working grant (recurring lane) |
| `monthly-keeper` | 9.99 | **yes** (Monthly Ledger) | Same pool model, $9.99. | Visible — working grant (recurring lane) |

**Shelf count (Night Market):** 8 cards if `permanent-builder` is in the loaded catalog (4 baskets + 3 curated impulse + WO-1253). Without counting that row: 7. WO-1165's "4 baskets + 3 impulse" is the pre-1253 shelf.

**Proven extra causes (not blended into the four):**

5. **Cosmetic art is preview-tint only.** `CosmeticApplyRegression` states no cosmetic ART exists in the tree; apply reaches a renderer via `previewColor`. Pack cosmetic SKUs exist in `cosmetics.json` and `PackCosmeticIntegrityRegression` proves `Owns==true` after grant — ownership works, the look does not. Distinct from "no redeemer".
6. **Shortfall-only merchandising.** Nine impulse SKUs are omitted from the shelf by `shelfCurated` (WO-947 §12c.4 / owner 2026-08-21 "Middle"). They are purchasable against a real gap via `ShortfallPackOffer`, not hidden vapor.
7. **Stale `_hiddenReason` copy.** `echo-patron-pack` and `builders-cache` still say "over-cap $19.99 (early-access ceiling is $4.99)". WO-1121 retired that ceiling. The rows were not rewritten (this ticket may only mark `packs.json` unpurchasable when *withdrawing*; we withdrew nothing).

---

## Engineering (the two code causes)

**Decision: BUILD the redeemers, do not withdraw.** Money is live. Tokens already in `GearInventory` from any prior grant must start working. Withdrawing would strand paid charges. SKU ids kept. Rows kept. Prices untouched. frostfall/embergrove/bloomtide **not** merged.

New spender: `Assets/_Modules/Village/Monetization/ConvenienceRedeemer.cs`

| Kind | What it does | Consume moment |
|---|---|---|
| `instant-build` | Sets Builder job `durationMs = 0` (existing zero-duration complete path in `StartBuilderJob`) | Next `StartBuild` / `StartUpgrade` with time left |
| `instant-repair` | Sets Repair enqueue duration to 0 (existing `Enqueue` zero-duration complete) | Next `JobKind.Repair` |
| `harvest-auto-collect` | 24h window; `AutoHarvestService` ticks `CollectAll` (same as the Ancient Sawmill perk, timed) | First tick while no window; stacking extends |
| `xp-weekend` | 24h 2x on `HeroProgression.AddXp` only (time, not damage). Cap 2.0x; stack extends duration | First XP grant while no window |

`PackCatalog.RedeemableConvenienceKinds` gained those four in the **same change**. Lantern oil and WO-1253 `permanent_builder` left in place.

Still vapor as **pack tokens** (not implemented, not advertised on any `storeVisible` row): `harvest_boost` (HarvestBoostService is crystal/ad, not GearInventory), `instant_fill_storage`, `workforce_slot`, `storage_tier_jump`, `offline_window_extension`.

**Not withdrawn:** `echo-patron-pack`, `builders-cache`, seasonal clones. They stay `storeVisible:false`. Redeemers mean a future unhide is an owner merchandising call, not an engineering unblock.

`api/purchases/*` untouched. `USD_ANCHORS` untouched. No new SKU. No deleted row. No price edit.

---

## Owner questions (clones + dominated pricing)

### Q1 — Duplicate clones: `frostfall-bundle` / `embergrove-bundle` / `bloomtide-bundle`

They are one product sold three times (WO-1165 §8). This seat must not merge or reprice.

**Recommendation:** keep all three ids forever (they are live `purchase_entitlements` keys). Pick **one** $9.99 seasonal to unhide **after** unique art exists (fifth cause). Keep the other $9.99 as a legacy alias (`legacySkus` on the survivor) rather than a second card. Keep `bloomtide-bundle` as the $4.99 seasonal once art exists, or retire it from the shelf only.

**Revenue if left as-is:** $0 — they are hidden. **Revenue if all three unhide with tint-only art:** chargebacks on a live store for "I bought winter armour and got a blue tint." **Revenue if merged in data by deleting two ids:** silent orphan of any entitlement written against the deleted id.

### Q2 — Dominated pricing: `impulse-wood-small`, `impulse-iron-small`, `keepers-satchel`, `hearth-spark`

**Recommendation:** leave them off the Night Market. Keep the small impulse rungs on `ShortfallPackOffer` (a 1000-wood close against a 900-wood gap is still the honest smallest pack). Do not unhide `keepers-satchel` until its crystals/$ beats the $4.99 basket or it gains a real differentiator that is redeemable. Keep `hearth-spark` as the devnet canary, never as the entry card (`starters-hand` already holds that rung).

**Revenue if they sit on the shelf as-is:** players learn the store is a trap (the $1.99 wood pack vs any mixed $1.99/4.99 basket). That trains *not buying*. **Revenue if deleted:** forbidden — ids are live keys. **Revenue of a reprice:** owner-only; this seat did not touch prices.

---

## Regression

`Assets/Editor/Regression/StoreSkuGrantRegression.cs`, registered in `DataRegression.RunAll` as `[store-sku-grant]`.

- Dual-copy + parse of `packs.json` and `battle_monthly.json`: unreadable/empty = **FAIL**, never Skip-as-green (WO-1138).
- Every `IsOnBrowsableShelf` row must grant economy, a cosmetic, or a **redeemable** convenience.
- Advertised shelf convenience must pass `IsRedeemableConvenience`.
- Monthly cards must have a deliverable drip.
- Live `ApplyPackContents` for every shelf SKU (PartialSkip only if GameStateService will not install).
- Redeemer consume: `TrySkipBuildTimer` + `XpMultiplier` against a throwaway `GearInventory`.

COMPILE_GATE / DataRegression not run (Unity forbidden this seat). Owner felt-verifies the store on device (acceptance 4).

---

## Files touched

- `Assets/_Modules/Village/Monetization/ConvenienceRedeemer.cs` (new)
- `Assets/_Modules/Village/Buildings/BuildTimerService.cs`
- `Assets/_Modules/Village/Buildings/Progression/AutoHarvestService.cs`
- `Assets/_Modules/Village/Hero/HeroProgression.cs`
- `Assets/_Modules/Wallet/PackCatalog.cs` (redeemable set + `IsOnBrowsableShelf`)
- `Assets/_Modules/Wallet/PackStore.cs` (shelf filter uses that helper)
- `Assets/_Modules/Wallet/PackStoreVM.cs` (comment only)
- `Assets/Resources/Data/Canonical/packs.json` + StreamingAssets twin (`_schemaNotes.convenienceRedeemers` only)
- `Assets/Editor/Regression/StoreSkuGrantRegression.cs` (new)
- `Assets/Editor/Regression/DataRegression.cs` (register)

Brace-balanced. Dual-copy identical. No commit.
