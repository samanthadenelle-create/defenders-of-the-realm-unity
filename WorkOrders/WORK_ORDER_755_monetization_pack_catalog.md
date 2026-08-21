<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-19
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-19) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — 755: Monetization Pack Catalog + Opening-Day Sales

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Author:** Creative Monetization Designer (design only — no `.cs` written, per CLAUDE.md Section 2/Section 13)
**Silo:** Monetization/Backend (Section 9 — isolated, parallel-safe; no scene files, no VillageSceneBuilder)
**Date:** 2026-07-19
**Companion:** WO-754 (rewarded-ad seam). Ads = the FREE income path; packs = the PAID accelerator. Section 7 composes them.

**North star (KEY_FACTS.md, BINDING):** *"Monetization = rewarded-ad income paths, NEVER a wall."*
V1 ships **ZERO crypto** (the wallet stack is devnet stub-only, `WalletService.DefaultNetwork = Devnet`).
Soft currency (Glimmer + the four resources Wood/Iron/Food/Crystal + Gold/Coins) is client-owned.
So every pack is an OPTIONAL accelerator, cosmetic, or convenience — never a progression gate, never
pay-to-win-required. The covenant line renders on every store surface: *"You are never required to
spend anything. Ever."* (`PackStore.cs:196`).

---

## 0. TL;DR — this is a CATALOG EXTENSION + 2 grant-bug fixes, NOT a greenfield

The store is ~70% built (CLAUDE.md Section 8). `PackStore` renders cards from `packs.json`, runs the
devnet-stub purchase flow, and applies contents through `PackStoreVM`. The **catalog already ships 13
packs** (5 canon pricing-ladder packs + 8 starter/seasonal bundles). What is missing is:

1. **Pure RESOURCE packs** — the owner's explicit ask ("buy resource packs"). Every current pack bundles
   cosmetics; there is no clean "just give me crystals/gold/glimmer" ladder. This WO adds two ladders:
   a **Glimmer ladder** and a **Resource-Crate ladder**.
2. **Extended-production / content packs** — a battle-pass season SKU, an Echo/pet patron pack, building-
   theme bundles; what ships now vs later.
3. **Opening-day sales** — a first-time-buyer bonus, a launch-window discount, the one-time Founder's Vow
   (exists), and an honest daily-deal rotation. Requires a small **additive schema** for sale windows.
4. **Two P1 GRANT BUGS that MUST be fixed first** (Section 2) or half of every pack silently grants nothing.

**Do NOT** rewrite `PackStore`/`PackStoreVM`/`PackCatalog`, re-model the JSON schema wholesale, or touch
the wallet rail. This WO adds JSON rows + a narrow grant-path fix + optional additive fields.

---

## 1. SME AUDIT — the money/grant path as it exists today (cite before you build)

| Piece | File:line | State |
|---|---|---|
| Store UI + purchase flow | `Assets/_Modules/Wallet/PackStore.cs:274` (`foreach PackCatalog.Packs`), `:448` (`Purchase`), `:488` (`WalletService.Pay`), `:494` (`_vm.ApplyPackContents`) | LIVE — renders all packs, devnet-stub pay, applies on confirm. |
| Entitlement grant (the VM) | `Assets/_Modules/Wallet/PackStoreVM.cs:68` (`ApplyPackContents`), `:83-91` (economy top-up), `:94-97` (cosmetic + SKU record), `:120` (`RecordOwned`) | LIVE — **but grants only Crystals/Food/Coins + records SKUs. See Section 2 bugs.** |
| Typed catalog + loader | `Assets/_Modules/Wallet/PackCatalog.cs:40` (`PackEconomy` = glimmer/crystals/food/coins), `:84` (`PackDef`), `:210` (covenant `ConvenienceAllowList`) | LIVE — WebGL-safe load; covenant firewall drops non-time-saving convenience kinds at load. |
| Pack catalog data | `Assets/Resources/Data/Canonical/packs.json` (+ `Assets/StreamingAssets/Data/Canonical/packs.json` twin) | LIVE — 13 packs, version 2. **DUAL-COPY: both files must be edited identically; Resources copy wins at load.** |
| Cosmetic catalog data | `Assets/Resources/Data/Canonical/cosmetics.json` (+ StreamingAssets twin) | LIVE — every pack cosmetic SKU has a matching `unlockMethod:achievement, glimmerCost:0` row so grants are not dangling in the CATALOG (but see Section 2, bug #2 — they dangle at RUNTIME). |
| Resource wallet (Crystals/Food/Coins) | `Assets/_Modules/Core/State/NestedTypes.cs:41` (`struct ResourceBalance` = crystals/food/coins) on `GameState.Resources` (`GameState.cs:50`) | LIVE — the single wallet the town HUD reads. |
| Wood / Iron | `GameState.Wood` / `GameState.Iron` (`GameState.cs:62-63`) — SEPARATE fields, NOT in `ResourceBalance` | LIVE. |
| Resource grant seam (HUD-refreshing, persisted) | `Assets/_Modules/Village/EconomyService.cs:294` (`Grant(ResourceCost)`), `:361` (`GrantSpendable` for Wood/Iron), `:463` (`AddCoins`) | LIVE — routes through GameStateService, raises `ResourcesChanged`. **THE correct grant hook.** |
| Glimmer (cosmetic-shop soft currency) | `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs:193` (`TryAddGlimmer`), `:233` (`GrantAchievement`), `:59` (PlayerPrefs `dotr-cosmetics-v1`) | LIVE — a SEPARATE persistence store from GameState. |
| Battle pass runtime | `Assets/_Modules/Cosmetics/BattlePassManager.cs:130` (`PurchasePremiumPass` spends 2400 Glimmer), `:195` (`ApplyReward`) | LIVE — needs a `BattlePassData` SO assigned or it no-ops. Premium track is bought with **Glimmer today, not a USD SKU.** |
| Area catalog (authority) | `docs/MASTER_CATALOG/economy-meta.md:167` (packs.json), `:208` (FLAG #16 split-brain ownership) | The split-brain is a KNOWN, DOCUMENTED flag. |

**Conclusion:** the render path, the pay path, and the resource grant seam all exist and are correct. The
grant path *inside `ApplyPackContents`* is incomplete in two ways that make packs lie to the player.

---

## 2. ★ P1 PREREQUISITE — TWO GRANT BUGS THAT MUST BE FIXED BEFORE SELLING ANYTHING ★

These are the owner-flagged P1s. A pack that advertises a reward and grants nothing is the exact opposite
of the honesty law. **No new pack ships until these are fixed** — otherwise the new resource/Glimmer packs
inherit the same silent failure.

### Bug #1 — Packs advertise Glimmer but grant 0 (and Wood/Iron too)

`PackStoreVM.ApplyPackContents` (`PackStoreVM.cs:83-91`) applies ONLY `Crystals`, `Food`, `Coins`:

```
var econ = pack.Contents != null ? pack.Contents.Economy : null;
if (econ != null) {
    var r = state.Resources;
    r.Crystals += econ.Crystals;
    r.Food += econ.Food;
    r.Coins += econ.Coins;
    state.Resources = r;
}
```

- `econ.Glimmer` is **never granted.** Every one of the 13 current packs lists a `glimmer` amount
  (hearth-spark 25 ... founders-vow 1000) and the player receives **none of it.** `packs.json` even
  admits it in `_schemaNotes.economy`: *"glimmer is carried for catalogue completeness (not yet applied
  at runtime)."* That is a shipped lie on the pack card (`PackStore.DescribeContents` prints "N glimmer").
- It also writes `state.Resources` **directly** instead of routing through `EconomyService.Grant`, so the
  town HUD does not necessarily refresh and no `ResourcesChanged` fires on the grant.

**Required fix (the correct grant hooks):**
- **Glimmer:** call `GlimmerCurrencyService.Instance?.TryAddGlimmer(econ.Glimmer)` when `econ.Glimmer > 0`.
  `PackStoreVM` is in `DeNelle.Wallet`, which cannot reference `DeNelle.Cosmetics` directly — resolve it by
  the SAME `AppDomain` type-name reflection this VM already uses for `MarketplaceInteractor` (`PackStoreVM.cs:143`).
  (Mirror the pattern `CryptoPaymentManager` already uses to reach `TryAddGlimmer` cross-asmdef.)
- **Crystals/Food/Coins:** route through `EconomyService.Instance?.Grant(new ResourceCost(crystals: econ.Crystals,
  food: econ.Food, coins: econ.Coins))` (also reflection, or via a Core seam) so the grant is persisted +
  HUD-refreshing. If the reflection cost is undesirable, at minimum call `GameStateService.AddCrystals/
  AddFood` + `AddCoins` (which raise `ResourcesChanged`) instead of the silent direct write.
- **Wood/Iron (new, for the resource packs in Section 3):** `EconomyService.Instance?.GrantSpendable(wood:
  econ.Wood, iron: econ.Iron)` when the new fields (Section 4.3) are non-zero.
- **Prove it (Section 12 discipline):** the existing `FlowTrace.Step("Store", "ApplyPackContents ... owned + economy applied")`
  must extend to log the Glimmer/Wood/Iron deltas, so a headless purchase run shows every advertised
  resource actually landed.

### Bug #2 — Pack cosmetics dangle / split-brain ownership vs the Glimmer shop

`ApplyPackContents` records cosmetic SKUs into `state.OwnedItemIds` (`PackStoreVM.cs:94-97` via `RecordOwned`),
but the **Cosmetic Shop equip/ownership system reads a DIFFERENT store**: `GlimmerCurrencyService._ownedSet`
(PlayerPrefs `dotr-cosmetics-v1`). So a pack-granted cosmetic is "owned" in `GameState.OwnedItemIds` yet
`GlimmerCurrencyService.Owns(sku) == false` and `Equip(sku)` is a no-op (`GlimmerCurrencyService.cs:170`).
The player pays, the emblem/skin appears "owned" to the pack system, and is **unequippable** in the wardrobe.
This is `economy-meta.md` FLAG #16 (two cosmetic-ownership sources of truth, unreconciled).

**Required fix:** in `ApplyPackContents`, for every cosmetic SKU the pack grants, ALSO call
`GlimmerCurrencyService.Instance?.GrantAchievement(sku)` (by the same reflection seam). `GrantAchievement`
is exactly the right entry point — it grants ownership OUTSIDE the Glimmer-spend path (`GlimmerCurrencyService.cs:233`),
which is what a paid cosmetic is. The cosmetics.json rows are already authored as `unlockMethod:achievement,
glimmerCost:0`, so `GrantAchievement` will accept them. Keep the `OwnedItemIds` record too (it is the pack
`IsOwned` entitlement key) — write BOTH so the pack-owned check and the wardrobe agree.

### Bug #3 (lesser — call out, do not block) — Convenience tokens are not applied

`ApplyPackContents` deliberately skips convenience tokens (`PackStoreVM.cs:99-102` comment; economy-meta
FLAG #11). Every pack lists instant-build / harvest-auto-collect etc. and none are granted — there is no
token tray/inventory yet. **This is a real "advertise but don't grant" gap** but it needs an inventory
system (out of scope here). **Interim honesty options (owner picks one):**
(a) build a minimal consumable-token tray (own WO) and grant on purchase; or
(b) until (a) ships, **remove the convenience arrays from the sale copy** so packs only advertise what
they actually grant (resources + cosmetics). Recommendation: (b) for launch, (a) as a fast-follow — never
sell a token the game cannot spend.

**Acceptance for Section 2:** a headless AutoPilot purchase of one pack shows, in `[Flow:Store]`, that
Glimmer + every resource + every cosmetic SKU landed in BOTH stores, and `GlimmerCurrencyService.Owns(sku)`
returns true for each pack cosmetic. No advertised line grants zero.

---

## 3. THE CATALOG DESIGN (get creative, grounded in the schema)

Price ladder reuses the canon points (from `packs.json _schemaNotes.pricing`) plus two low anchors:

| USD | usdc | sol | skr |
|---|---|---|---|
| $0.99 | 0.99 | 0.009 | 12 |
| $1.99 | 1.99 | 0.018 | 25 |
| $2.99 | 2.99 | 0.027 | 36 |
| $4.99 | 4.99 | 0.045 | 60 |
| $9.99 | 9.99 | 0.090 | 120 |
| $19.99 | 19.99 | 0.180 | 240 |
| $49.99 | 49.99 | 0.450 | 600 |

### 3.1 RESOURCE PACKS (the owner's explicit ask) — "they ACCELERATE, they don't unlock"

Two ladders. Both are PURE economy (no cosmetics, no convenience) so the messaging is clean: *"a head-start
on what you already earn for free every wave."* Value/dollar escalates with tier (the standard mobile
ladder — bigger tiers are better value, so there is a reason to buy up, never a reason you MUST).

**Glimmer ladder** (fuels the Cosmetic Shop; Glimmer is flex-only, the most covenant-safe currency to sell —
it buys nothing but appearance). Grant seam: `GlimmerCurrencyService.TryAddGlimmer` (fixed in Section 2).

| SKU | Name | USD | Glimmer | Value note |
|---|---|---|---|---|
| `glimmer-pouch` | Glimmer Pouch | $0.99 | 120 | base rate ~121/$ |
| `glimmer-satchel` | Glimmer Satchel | $4.99 | 700 | +15% value — the "value" pick |
| `glimmer-hoard` | Glimmer Hoard | $9.99 | 1600 | +32% value |
| `glimmer-vault` | Glimmer Vault | $19.99 | 3600 | +49% value — mega |

**Resource-Crate ladder** (Wood/Iron/Food/Crystal/Gold — the four build resources + Gold). Grant seam:
`EconomyService.Grant` (Crystals/Food/Coins) + `GrantSpendable` (Wood/Iron), fixed in Section 2. Sizes are
anchored to the existing bundle scale (hearth-spark 200 crystals ... founders-vow 15000 crystals).

| SKU | Name | USD | Wood | Iron | Food | Crystals | Gold(coins) | Tier feel |
|---|---|---|---|---|---|---|---|---|
| `provisioners-crate` | Provisioner's Crate | $1.99 | 400 | 150 | 150 | 250 | 200 | small |
| `quartermasters-crate` | Quartermaster's Crate | $4.99 | 1200 | 450 | 450 | 800 | 700 | medium (~+18% value) |
| `stronghold-cache` | Stronghold Cache | $9.99 | 2800 | 1000 | 1000 | 1900 | 1600 | large (~+35%) |
| `realm-treasury` | Realm Treasury | $19.99 | 6500 | 2400 | 2400 | 4200 | 3600 | mega (~+55%) |

**"Never a wall" framing (renders on the resource-pack cards):** *"Everything in this crate you also earn
by playing — this is a head start, not a gate."* No resource pack unlocks content; every building/upgrade
is reachable by free harvest + wave income. Ads (WO-754 `place.store.crystals`, `place.daily.chest`) are the
FREE version of the same faucet (Section 7).

### 3.2 EXTENDED-PRODUCTION / CONTENT PACKS (ongoing value)

These add durable value (cosmetics that persist, a season of content). Split into SHIP-NOW vs LATER.

**Ship now** (all grant through the fixed cosmetic + economy seams; cosmetics.json rows must exist):

- **Founder's Vow** — ALREADY IN CATALOG (`founders-vow`, tier 5, `founderOnly:true`, $49.99). Keep verbatim;
  it is the one-time launch-window flagship (Section 5). No change beyond the Section 2 grant fix making its
  1000 glimmer + 5 cosmetics actually land.
- **Starter's Hand** — ALREADY IN CATALOG (`starters-hand`, tier 9, $4.99). Position it as the merchandised
  "best first buy" (Section 5 first-time bonus attaches here).
- **Echo Patron Pack** — ALREADY IN CATALOG (`echo-patron-pack`, tier 10, $19.99, harvest-loop economy). Keep;
  it is the "pet/echo workforce" pack the owner named. (Note: its harvest-auto-collect tokens fall under
  Section 2 bug #3 — apply option (b) until the token tray ships.)
- **Building-theme bundles** — ALREADY IN CATALOG (Frostfall/Embergrove/Bloomtide seasonal palettes, tiers
  6-8). These ARE the "building themes" extended-content line; keep and rotate seasonally (Section 5 daily/
  seasonal deal).
- **NEW — Aether Companion Pack** (`aether-companion-pack`, $9.99): a pet-forward content pack —
  pet skin + pet-glow VFX cosmetic + a building palette + light economy. Fills the explicit "Echo/pet pack"
  cosmetic slot (the Echo Patron Pack is economy-forward; this is cosmetic-forward). Requires 3-4 new
  cosmetics.json rows (`cosmetic.aether-companion-pack.pet-skin` / `.pet-glow` / `.building-palette` /
  `.exclusive`) so no grant dangles.

**Ship later** (needs a system that is not launch-ready — spec now, build in a follow-on WO):

- **Season Pass SKU** (`season-pass-s1`, $9.99): a USD purchase of the `BattlePassManager` premium track.
  TODAY the premium track is bought with **2400 Glimmer** (`BattlePassManager.PurchasePremiumPass`), not USD,
  and `BattlePassManager` needs a `BattlePassData` SO assigned or it no-ops. **Later WO** wires a USD SKU that
  calls `PurchasePremiumPass()` (or a USD-equivalent grant path) + authors the season SO. Until then, the
  battle pass stays a Glimmer purchase, and the Glimmer ladder (3.1) is the top-up path to afford it — a clean
  compose. Do NOT ship a Season Pass USD SKU until the SO exists and the premium grant is proven.
- **Expansion content packs** (dungeons/regions) — post-launch; not a v1 pack. Named here only so the schema's
  `type` field (Section 4.3) reserves a `content-unlock` category for them.

### 3.3 Cosmetic bundles (skins/emblems/palettes)

The existing 8 bundles already cover hero wardrobes, weapon/shield flair, and seasonal palettes. No new
cosmetic-bundle ladder is needed for launch beyond the Aether Companion Pack (3.2). Keep the seasonal three
(Frostfall/Embergrove/Bloomtide) on a **rotation** rather than all-visible-always (Section 5 daily deal), so
the shelf feels fresh and the seasonal framing is honest (winter pack featured in winter, etc.).

---

## 4. OPENING-DAY / LAUNCH SALES

All honest: real countdowns, real one-time caps, no fake scarcity, no "was $X" anchor unless it was truly
ever that price. Every timed offer shows a REAL server-authoritative end time.

### 4.1 First-time-buyer bonus (the single strongest, honesty-safe lever)

- **What:** the player's FIRST pack purchase (any SKU) grants a one-time bonus rider — recommend **+100%
  Glimmer** on that first pack (doubles the Glimmer line only; does not touch resources/cosmetics). Clean,
  generous, and Glimmer is flex-only so it can never be pay-to-win.
- **Honesty:** shown as "First purchase bonus: double Glimmer" on every card until it is used, then it
  disappears (no permanent "SALE!" that never ends). One-time, tracked in save.
- **Seam:** a new persisted bool `GameState.FirstPurchaseBonusUsed` (additive save field). `ApplyPackContents`
  checks it: if false, grant `econ.Glimmer * 2` instead of `econ.Glimmer`, then set it true + Save. (Depends
  on the Section 2 Glimmer-grant fix existing first.)

### 4.2 Founder's Vow — one-time launch window (EXISTS, keep)

`founders-vow` (`founderOnly:true`) already renders a "Launch window only" tag (`PackStore.cs:326`). It is the
one-time, launch-only flagship. **Make the window REAL:** add a `saleWindow` with a concrete `endsUtc` (owner
sets the date in Section 8) so the "launch window only" claim is backed by an actual expiry, not vibes. After
the window it is removed from the catalog (never re-sold — that is the whole promise; re-selling it breaks the
honesty law).

### 4.3 Launch-week discount (optional, owner-gated)

- A **time-boxed launch discount** on ONE merchandised pack (recommend Starter's Hand or the Quartermaster's
  Crate) for the first 7 days: e.g. Starter's Hand at $2.99 during launch week, real countdown, reverts to
  $4.99 after. Only honest if the "regular" price is the price it actually holds after — so the pack must be
  genuinely $4.99 the rest of its life.
- **Seam:** the additive `saleWindow` + `salePricing` fields (below). Do NOT show a struck-through anchor
  unless the higher price is the real standing price.

### 4.4 Daily-deal / seasonal rotation (honest rotation, not fake scarcity)

- One **"Featured today"** slot that rotates a subset (the seasonal palettes, a resource crate) on a real
  daily/weekly cadence, with a real "changes in HH:MM" timer. The item is NOT gone forever — it rotates back,
  and the copy says so ("rotates back later"). Seasonal packs are FEATURED in-season, still buyable out of a
  featured slot via the full list. No countdown-to-never.
- **Seam:** a `rotation` group tag + a client or server-config schedule. For v1 this can be a simple
  client-side date-seeded pick (no backend needed) that highlights one SKU; the honest countdown is to the
  next local-midnight rotation.

### 4.5 Additive schema for sales (Section requires "sale window" — the schema has none today)

`packs.json` version 2 has NO sale/discount/window fields. Add these **optional, additive** fields (older
rows without them behave exactly as today — no migration, mirrors the append-only discipline in `GameState`):

- On `PackDef`: `"type"` (string: `resource` | `glimmer` | `cosmetic` | `starter` | `founder` | `season` |
  `content-unlock`) — a display/grouping tag so the store can tab/section the shelf. Default (absent) = treat
  as `cosmetic`/legacy.
- On `PackDef`: `"saleWindow": { "startsUtc": "...", "endsUtc": "..." }` — optional; when present the pack is
  only offered inside the window (Founder's Vow, launch discount). Absent = always available.
- On `PackDef`: `"salePricing": { usd, usdc, sol, skr }` — optional discounted rail; when present AND inside
  `saleWindow`, the store charges this instead of `pricing`. Absent = no discount.
- On `PackEconomy`: `"wood"` and `"iron"` (ints) — so resource crates can grant Wood/Iron (Section 3.1). These
  parse into new `PackEconomy.Wood/Iron` fields on `PackCatalog.PackEconomy` and are granted via
  `GrantSpendable` (Section 2).
- `PackCatalog`/`PackDef` code changes are small and additive: add the four fields + a
  `PackDef.IsAvailableNow(DateTime utcNow)` helper the render loop calls to filter windowed packs, and a
  `PackDef.EffectivePricing(utcNow)` that returns `salePricing` when in-window else `pricing`. **Keep the
  covenant firewall (`PackCatalog.EnforceCovenant`) intact** — new fields do not touch convenience validation.

---

## 5. PROPOSED packs.json ADDITION (drop-in, matches existing schema)

ASCII only. Append these objects to the `packs` array in BOTH `Assets/Resources/Data/Canonical/packs.json`
AND `Assets/StreamingAssets/Data/Canonical/packs.json` (dual-copy; Resources wins at load). Bump the file's
top-level `"version"` to `3` and extend `_schemaNotes` to document `type` / `saleWindow` / `salePricing` /
`economy.wood` / `economy.iron`. New tiers continue the unique-tier key sequence (14+). Convenience arrays are
intentionally OMITTED on the resource/glimmer packs (Section 2 bug #3 option (b) — do not advertise a token
the game cannot yet spend). Any cosmetic SKU referenced below MUST get a matching `cosmetics.json` row
(`unlockMethod:achievement, glimmerCost:0`) in the SAME change or the grant dangles.

```json
{
  "sku": "glimmer-pouch",
  "tier": 14,
  "type": "glimmer",
  "name": "Glimmer Pouch",
  "tagline": "A pouch of glimmer to spend on flair at the Cosmetic Shop.",
  "theme": "Pure soft-currency, smallest Glimmer tier.",
  "founderOnly": false,
  "pricing": { "usd": 0.99, "usdc": 0.99, "sol": 0.009, "skr": 12 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 120, "crystals": 0, "food": 0, "coins": 0 } }
},
{
  "sku": "glimmer-satchel",
  "tier": 15,
  "type": "glimmer",
  "name": "Glimmer Satchel",
  "tagline": "The value satchel - the most glimmer most tenders will want.",
  "theme": "Pure soft-currency, value tier (+15%).",
  "founderOnly": false,
  "pricing": { "usd": 4.99, "usdc": 4.99, "sol": 0.045, "skr": 60 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 700, "crystals": 0, "food": 0, "coins": 0 } }
},
{
  "sku": "glimmer-hoard",
  "tier": 16,
  "type": "glimmer",
  "name": "Glimmer Hoard",
  "tagline": "A hoard of glimmer for the tender who loves to dress the realm.",
  "theme": "Pure soft-currency, large tier (+32%).",
  "founderOnly": false,
  "pricing": { "usd": 9.99, "usdc": 9.99, "sol": 0.09, "skr": 120 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 1600, "crystals": 0, "food": 0, "coins": 0 } }
},
{
  "sku": "glimmer-vault",
  "tier": 17,
  "type": "glimmer",
  "name": "Glimmer Vault",
  "tagline": "A vault of glimmer - best value, a season of cosmetics covered.",
  "theme": "Pure soft-currency, mega tier (+49%).",
  "founderOnly": false,
  "pricing": { "usd": 19.99, "usdc": 19.99, "sol": 0.18, "skr": 240 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 3600, "crystals": 0, "food": 0, "coins": 0 } }
},
{
  "sku": "provisioners-crate",
  "tier": 18,
  "type": "resource",
  "name": "Provisioner's Crate",
  "tagline": "A head start, not a gate - resources you also earn every wave.",
  "theme": "Pure resources, small tier. Wood/Iron/Food/Crystal/Gold.",
  "founderOnly": false,
  "pricing": { "usd": 1.99, "usdc": 1.99, "sol": 0.018, "skr": 25 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 0, "wood": 400, "iron": 150, "food": 150, "crystals": 250, "coins": 200 } }
},
{
  "sku": "quartermasters-crate",
  "tier": 19,
  "type": "resource",
  "name": "Quartermaster's Crate",
  "tagline": "The quartermaster's value crate - stock the stores in one stroke.",
  "theme": "Pure resources, medium tier (+18% value).",
  "founderOnly": false,
  "pricing": { "usd": 4.99, "usdc": 4.99, "sol": 0.045, "skr": 60 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 0, "wood": 1200, "iron": 450, "food": 450, "crystals": 800, "coins": 700 } }
},
{
  "sku": "stronghold-cache",
  "tier": 20,
  "type": "resource",
  "name": "Stronghold Cache",
  "tagline": "A stronghold's worth of stores - raise the realm without pause.",
  "theme": "Pure resources, large tier (+35% value).",
  "founderOnly": false,
  "pricing": { "usd": 9.99, "usdc": 9.99, "sol": 0.09, "skr": 120 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 0, "wood": 2800, "iron": 1000, "food": 1000, "crystals": 1900, "coins": 1600 } }
},
{
  "sku": "realm-treasury",
  "tier": 21,
  "type": "resource",
  "name": "Realm Treasury",
  "tagline": "The realm's treasury opened - the biggest head start there is.",
  "theme": "Pure resources, mega tier (+55% value).",
  "founderOnly": false,
  "pricing": { "usd": 19.99, "usdc": 19.99, "sol": 0.18, "skr": 240 },
  "contents": { "cosmetics": [], "economy": { "glimmer": 0, "wood": 6500, "iron": 2400, "food": 2400, "crystals": 4200, "coins": 3600 } }
},
{
  "sku": "aether-companion-pack",
  "tier": 22,
  "type": "cosmetic",
  "name": "Aether Companion Pack",
  "tagline": "Dress your echo in twilight - a companion's finery for the harvest.",
  "theme": "Pet-forward cosmetic pack: pet skin + pet-glow VFX + palette + crest, light economy.",
  "founderOnly": false,
  "pricing": { "usd": 9.99, "usdc": 9.99, "sol": 0.09, "skr": 120 },
  "contents": {
    "cosmetics": ["cosmetic.aether-companion-pack.pet-skin", "cosmetic.aether-companion-pack.pet-glow", "cosmetic.aether-companion-pack.building-palette", "cosmetic.aether-companion-pack.exclusive"],
    "economy": { "glimmer": 150, "crystals": 800, "food": 300, "coins": 500 }
  },
  "packExclusiveCosmetic": "cosmetic.aether-companion-pack.exclusive"
}
```

**Launch-sale examples (illustrative — owner sets real UTC dates in Section 8):**

- Founder's Vow gains a real window (edit the EXISTING `founders-vow` object): add
  `"type": "founder"` and `"saleWindow": { "startsUtc": "2026-08-01T00:00:00Z", "endsUtc": "2026-08-15T00:00:00Z" }`.
- Launch-week discount on Starter's Hand (edit the EXISTING `starters-hand` object): add
  `"saleWindow": { "startsUtc": "2026-08-01T00:00:00Z", "endsUtc": "2026-08-08T00:00:00Z" }` and
  `"salePricing": { "usd": 2.99, "usdc": 2.99, "sol": 0.027, "skr": 36 }` (reverts to its standing $4.99 after).

---

## 6. GRANT-SEAM MAP (which hook each pack type uses — post Section-2 fix)

| Pack line | Contents | Grant seam (correct hook) | Ownership/entitlement |
|---|---|---|---|
| Glimmer ladder | glimmer only | `GlimmerCurrencyService.TryAddGlimmer` (reflection from Wallet) | pack SKU -> `OwnedItemIds` |
| Resource crates | wood/iron/food/crystal/gold | `EconomyService.Grant(ResourceCost{crystals,food,coins})` + `GrantSpendable(wood,iron)` | pack SKU -> `OwnedItemIds` |
| Cosmetic/content packs | cosmetics + light economy | economy as above; each cosmetic -> `GlimmerCurrencyService.GrantAchievement(sku)` AND `OwnedItemIds` | equippable in wardrobe (bug #2 fix) |
| Founder / Starter | mixed | all of the above | one-time via `founderOnly` + `saleWindow` |
| Season Pass (LATER) | premium BP track | `BattlePassManager.PurchasePremiumPass()` (needs SO + USD path, later WO) | `BP_HasPremium` PlayerPrefs |
| Convenience tokens (all packs) | instant-build etc. | NO seam yet (bug #3) - token tray WO, or omit from copy | n/a v1 |

---

## 7. HOW IT COMPOSES WITH REWARDED ADS (WO-754) — complementary, not competing

Ads = the FREE income path; packs = the PAID accelerator. They target the same faucets, so a player is never
forced toward the wallet — the ad is always the free alternative, which is the whole "never a wall" promise.

- **Crystals/resources:** WO-754 `place.store.crystals` ("Free crystals - watch a clip", `EconomyService.Grant(crystals:150)`)
  sits IN the shop next to the resource crates. The crate is the "I want a lot now" option; the ad is the
  "I'll earn it free" option. Same grant seam (`EconomyService.Grant`) — packs just grant more, faster.
- **Glimmer:** WO-754's later "Watch for +15 glimmer" trickle uses `GlimmerCurrencyService.TryAddGlimmer` — the
  SAME seam the Glimmer ladder uses (post Section-2 fix). The ad earns Glimmer slowly for free; the Glimmer
  ladder buys it in bulk. Both feed the Cosmetic Shop and the (later) Glimmer-bought battle pass.
- **Build skip:** WO-754 `place.build.skip` is the FREE timer-skip; the pack convenience tokens (when the tray
  ships) are the paid bulk version. Until the tray ships, only the ad path exists — honest.
- **Daily chest:** WO-754 `place.daily.chest` is a free daily retention nudge; the daily-deal rotation (Section 4.4)
  is the paid featured slot. They live side by side on the same daily surface.
- **Shared honesty surface:** both the ad offers and the pack cards render the covenant line. The store reads,
  in one breath: *"Watch a clip for free, or buy a crate to skip ahead - you are never required to spend
  anything. Ever."* The ad and the pack are two doors to the same room; neither is a wall.

---

## 8. ★ OWNER SETUP — decisions/pricing only you can make ★

1. **Approve or adjust the price ladder + pack sizes** (Section 3.1 tables). The USD anchors reuse the canon
   ladder; the resource/Glimmer amounts are my proposal — tune to your economy's earn rates.
2. **Set real launch-sale UTC dates** (Section 4.2/4.3/5): Founder's Vow window start+end, launch-discount
   window, and which SKU carries the launch discount.
3. **First-purchase bonus** (Section 4.1): approve "double Glimmer on first purchase" (or name a different
   one-time bonus). Confirm it is glimmer-only (keeps it non-pay-to-win).
4. **Convenience tokens (bug #3):** choose (a) build a token tray now (fast-follow WO) or (b) strip convenience
   copy from packs until the tray ships. Recommendation: (b) for launch.
5. **Season Pass:** confirm it stays a Glimmer purchase for v1 (no USD SKU until the `BattlePassData` SO +
   USD grant path ship in a later WO).
6. **Aether Companion Pack cosmetics:** approve the 4 new cosmetic rows (or cut to what art can deliver).
7. **Crypto stays OFF for v1** (confirm): all rails run the devnet stub; USD reference prices display, wallet
   rails are stub. No mainnet, no real SKR mint (`WalletEndpoints.SkrMint* = ""`).

---

## 9. ACCEPTANCE CRITERIA

- [ ] **Section 2 P1 fixes land FIRST:** Glimmer, Wood/Iron, Crystals/Food/Coins all grant on purchase (routed
      through `EconomyService`/`TryAddGlimmer`, HUD-refreshing); every pack cosmetic calls
      `GrantAchievement` so it is equippable; no advertised line grants zero. Headless-proven in `[Flow:Store]`.
- [ ] Convenience-token handling resolved per owner choice (tray WO filed, or copy stripped) — no unspendable
      token advertised.
- [ ] New Glimmer + Resource-Crate ladders added to BOTH `packs.json` copies (dual-copy identical), `version`
      bumped to 3, `_schemaNotes` extended; brace/JSON valid.
- [ ] Additive schema fields (`type`, `saleWindow`, `salePricing`, `economy.wood/iron`) parse on
      `PackDef`/`PackEconomy`; absent fields behave as today (no migration break); covenant firewall intact.
- [ ] Every new cosmetic SKU has a matching `cosmetics.json` row (both copies) — zero dangling grants.
- [ ] Launch sales honest: Founder's Vow + launch discount are real windows with real `endsUtc`; first-purchase
      bonus is one-time and disappears when used; daily rotation shows a real "changes in" timer, no fake scarcity.
- [ ] Resource/Glimmer pack cards render the "head start, not a gate" framing; covenant line present.
- [ ] Ad/pack compose: shop shows the free ad faucet beside the paid crate on the same surfaces (Section 7).
- [ ] Canon: one-line entry in `PIPELINE_STATE.md` Section 8 pointing at this WO + the Section 2 grant fixes.

## 10. WHAT NOT TO TOUCH
- Do NOT rewrite `PackStore`/`PackStoreVM`/`PackCatalog` — Section 2 is a NARROW grant-path fix + additive fields.
- Do NOT re-model the pack JSON schema — only APPEND rows + the four additive optional fields.
- Do NOT touch the wallet/crypto rail, mainnet, or the SKR mint — v1 is devnet stub, USD-reference display.
- Do NOT ship convenience tokens as buyable until a token tray exists (bug #3).
- Do NOT ship the Season Pass as a USD SKU until the `BattlePassData` SO + premium USD grant path exist.
- Do NOT re-sell Founder's Vow after its window — the one-time promise is the product.
- No `.unity` hand-edits; both `packs.json` copies edited identically; JSON validates; covenant line on every surface.

## 11. LANE / COORDINATION
Monetization/Backend lane (Section 9) — isolated, parallel-safe (JSON data + a narrow same-file grant fix in
`PackStoreVM` + additive `PackCatalog` fields; no scene files, no VillageSceneBuilder). `PackStoreVM.cs` is
the only `.cs` touched — single-agent, single-committer per Section 11. Composes with WO-754 (same
`EconomyService`/`GlimmerCurrencyService` seams; no conflict).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
