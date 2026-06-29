# Monetization / PackStore — State Audit (2026-06-28)

READ-ONLY audit. No files were edited. Scope: the store/monetization system under
`Assets/_Modules/Wallet` + `Assets/_Modules/Cosmetics` + the canonical pack data.

---

## 1. TL;DR

The pack-store is **~70% built and runs end-to-end on a devnet stub** — catalog →
typed model → store UI → wallet pay → entitlement grant → save round-trip all exist
and are instrumented. What is missing to actually **ship sellable packs** is: (a) a
real wallet provider wired (Solana SDK not installed; everything runs through
`StubWalletProvider`), (b) the store is **not entry-pointed in the live scene**
(`MarketplaceInteractor` had its F-key/proximity open path removed; nothing calls
`OpenStore()`), (c) the **pack cosmetic SKUs are dangling** — `packs.json` grants
SKUs like `cosmetic.hearth-spark.exclusive` that **do not exist in `cosmetics.json`**,
so a purchased cosmetic resolves to nothing visible, and (d) convenience tokens
(`instant-build` etc.) are granted in name only — there is no token tray / redemption
system. Mainnet is owner-gated and intentionally off.

---

## 2. What is BUILT

### Data + model (solid)
- `Assets/_Modules/Wallet/PackCatalog.cs` — typed `PackDef` model + static loader.
  Reads `Data/Canonical/packs.json` via `DeNelle.Core.CanonicalJson.Read` (Resources
  first for WebGL-safety, StreamingAssets fallback). Lookups by SKU + tier. Caches.
- `Assets/StreamingAssets/Data/Canonical/packs.json` (source) + mirror at
  `Assets/Resources/Data/Canonical/packs.json` (the Resources copy the runtime loads).
  **Both must stay in sync** (same dual-copy rule as cosmetics.json).
- 5 canonical packs already authored (tiers 1–5: Hearth Spark, Lanternlight, Folk's
  Thanks, Patron of Elarion, Founder's Vow).

### Store UI (built, code-built not UXML)
- `Assets/_Modules/Wallet/PackStore.cs` — `MonoBehaviour` (`[RequireComponent(UIDocument)]`).
  **Ignores its own `PackStore.uxml`** (UXML renders empty in player builds — known trap)
  and constructs the whole scaffold in code via `ShopTheme`. Renders one card per pack:
  name, tagline, USD reference, SOL/USDC/SKR currency chips, contents summary, Buy/Owned.
  Fully `FlowTrace`-instrumented (no silent blanks).
- `Assets/_Modules/Wallet/UI/PackStore.uxml` + `.uss` — present but effectively dead
  (the build-trap workaround bypasses them).

### Wallet + payment seam (built, stub-backed)
- `Assets/_Modules/Wallet/WalletService.cs` — app-facing surface. `Connect` / `GetBalance`
  / `Pay(pack,currency)` / `PayFlat(txId,currency,amount)`. Depends on `IWalletProvider`,
  never the SDK. Auto-selects provider: `SolanaWalletProvider` if `SOLANA_SDK` define set,
  else `StubWalletProvider`. Network pinned to **Devnet** (`DefaultNetwork`); Mainnet flip
  is owner-gated. Also implements `IWalletSigner` for backend save-auth (WO-121).
- Providers: `StubWalletProvider.cs` (devnet mock, ships now), `SolanaWalletProvider.cs`
  (`#if SOLANA_SDK`-guarded, inert until the package is installed).
- `Assets/_Modules/Wallet/WalletRegistry.cs` — treasury / Rewards Distributor address
  (transparency display only; public address, never a key).
- `Assets/_Modules/Wallet/CryptoPaymentManager.cs` — singleton bridge for **non-pack**
  Glimmer top-ups (PayWithSOL/SKR/USDC → grants Glimmer via reflection into
  `GlimmerCurrencyService`). SKR +25% bonus. Separate path from pack purchases.
- Tests: `Assets/_Modules/Wallet/Tests/` (WalletService / WalletRegistry / StubProvider).

### Entitlement / grant (built for economy + ownership)
- `PackStore.ApplyPackContents(pack)` — on confirmed pay: adds `economy.crystals/food/coins`
  to `GameState.Resources`; records the pack SKU + each cosmetic SKU into
  `GameState.OwnedItemIds` (`Assets/_Modules/Core/State/GameState.cs:63`); persists via
  `GameStateService.Save()`. Self-verifies the SKU landed (FlowTrace.Fail if not).
  Persisted in save schema at `SaveSchema.cs:110` (`ownedItemIds`).

### Cosmetic catalog (separate, soft-currency system)
- `Assets/_Modules/Cosmetics/CosmeticCatalog.cs` + `cosmetics.json` — 12 Glimmer-priced
  cosmetics (hero/pet/village skins). This is the **Glimmer shop**, distinct from the
  pack store. `GlimmerCurrencyService`, `CosmeticApplier`, `BattlePassManager` exist.

---

## 3. What is MISSING / blocking sellable packs

1. **No live entry point.** `MarketplaceInteractor.cs` had its proximity-/F-open path
   **removed** (Update() now just releases the button; `OpenStore()` is never called).
   The store can only be opened if something external enables the `PackStoreUI`
   GameObject. README confirms: "Store scene-wiring currently DISABLED pending own
   PanelSettings." → Needs a real, current entry point + its own PanelSettings.
2. **Dangling pack cosmetic SKUs.** `packs.json` grants `cosmetic.<pack>.exclusive`,
   `cosmetic.<pack>.hero-outfit`, `.pet-skin`, `.building-palette`, `.permanent-banner`
   — **none of these IDs exist in `cosmetics.json`**. They get recorded as owned but
   resolve to no `CosmeticDef`, so there is nothing to equip/show. Either add matching
   pack-exclusive entries to `cosmetics.json` or build a resolver. (This is the biggest
   "looks built but does nothing" gap.)
3. **Convenience tokens are nominal.** `instant-build / instant-repair /
   harvest-auto-collect / xp-weekend` are described in the data and counted in the UI,
   but `ApplyPackContents` explicitly does NOT grant them ("no token tray yet … flagged
   for the Week-8 inventory pass"). No redemption system exists.
4. **`glimmer` economy field is dropped.** Packs list `economy.glimmer`, but
   `ApplyPackContents` only applies crystals/food/coins (Glimmer isn't in
   `GameState.Resources`). Paid Glimmer in a pack is currently lost.
5. **Real money rail not wired.** Solana Unity SDK not installed; `SOLANA_SDK` define
   unset → only the stub runs. Stripe/USD is web-only and out of scope for Unity. Mainnet
   is intentionally owner-gated (do not flip).
6. **`founderOnly` window not enforced in Unity.** The flag is read and a "Launch window
   only" tag shows, but there is no date gate; the owner is expected to gate the window.

---

## 4. EXACT store-pack data schema

**File the runtime loads:** `Assets/Resources/Data/Canonical/packs.json`
(Resources copy; WebGL-safe). **Source/edit copy:** `Assets/StreamingAssets/Data/Canonical/packs.json`.
**Keep BOTH copies identical.** Loaded by `PackCatalog.cs` → deserialized with
Newtonsoft into `PackCatalogData`/`PackDef`. JSON property names are the contract
(camelCase, mapped via `[JsonProperty]`).

### Root object (`PackCatalogData`)
| field | type | notes |
|---|---|---|
| `version` | int | schema version (currently `1`) |
| `currencyDisclaimer` | string | permanent UI disclaimer for wallet-rail purchases |
| `packs` | array of PackDef | the purchasable packs |

(`_comment`, `_sources`, `_schemaNotes` are ignored doc-only keys — fine to keep.)

### `PackDef` (one entry in `packs[]`)
| field | type | required | notes |
|---|---|---|---|
| `sku` | string | yes | stable entitlement key, kebab-case (e.g. `"hearth-spark"`). Recorded into `OwnedItemIds`. |
| `tier` | int | yes | pricing tier 1–5 (also the lookup key for `FindByTier`) |
| `name` | string | yes | canon display name, verbatim |
| `tagline` | string | yes | narrative one-liner on the card |
| `theme` | string | no | pricing-ladder theme description (catalog/doc field; not shown on card) |
| `founderOnly` | bool | no | default false; true shows "Launch window only" tag |
| `pricing` | object | yes | see below |
| `contents` | object | yes | see below |
| `packExclusiveCosmetic` | string | no | the single cosmetic SKU exclusive to this pack |

### `pricing` (object)
| field | type | notes |
|---|---|---|
| `usd` | number | Stripe/USD **reference** price, display only (web-only rail) |
| `usdc` | number | USDC wallet-rail amount |
| `sol` | number | native SOL wallet-rail amount |
| `skr` | number | SKR (Solana Seeker token) wallet-rail amount |

The Unity store reads **sol/usdc/skr** for actual payment; `usd` is shown as "$X.XX reference".

### `contents` (object)
- `cosmetics`: `string[]` — cosmetic SKUs granted (each recorded into `OwnedItemIds`).
- `economy`: object — `{ "glimmer": int, "crystals": int, "food": int, "coins": int }`.
  Runtime currently applies **crystals/food/coins** only (glimmer + food note: food IS
  applied; glimmer is NOT — not in `GameState.Resources`).
- `convenience`: array of `{ "kind": string, "count": int, "description": string }`.
  `kind` ∈ `instant-build | instant-repair | harvest-auto-collect | xp-weekend`.
  Currently **counted/displayed but not granted** (no token system).

### Canonical example entry (copy this shape verbatim for generation)
```json
{
  "sku": "hearth-spark",
  "tier": 1,
  "name": "Hearth Spark",
  "tagline": "A tiny welcome — the Heart kindles a spark for a new tender.",
  "theme": "Tiny welcome — beginner cosmetic + small economy bump.",
  "pricing": { "usd": 1.99, "usdc": 1.99, "sol": 0.018, "skr": 25 },
  "contents": {
    "cosmetics": ["cosmetic.hearth-spark.exclusive"],
    "economy": { "glimmer": 25, "crystals": 200, "food": 50, "coins": 100 },
    "convenience": [
      { "kind": "instant-build", "count": 1, "description": "Skip the build animation for one building." }
    ]
  },
  "packExclusiveCosmetic": "cosmetic.hearth-spark.exclusive"
}
```

**Generation cautions:**
- `sku` must be unique + stable; it is the persisted entitlement key.
- `tier` should be unique (1..N) — `FindByTier` returns first match.
- Cosmetic SKUs referenced here **should also exist in `cosmetics.json`** (currently
  they don't — fix that in the same pass, or the cosmetic is unredeemable).
- Use the `cosmetic.<sku>.<role>` naming already in use (`.exclusive`, `.hero-outfit`,
  `.pet-skin`, `.building-palette`, `.permanent-banner`).
- Keep StreamingAssets + Resources copies identical.

---

## 5. Suggested pack 'slots' worth creating (~6–8)

The current 5 are a **price-ladder** (tiny→founder). The richer opportunity is
**themed bundles** at the value/supporter tiers (tier 2–4 pricing, `founderOnly:false`),
each anchored by a pack-exclusive cosmetic set. Rough slots:

1. **Frostfall Bundle** — winter theme. Frostfall Knight outfit + Glacierborn pet skin
   + Frozen Hearth lantern; mid economy. (Builds on existing frost cosmetics.)
   ~tier 3 ($9.99).
2. **Embergrove Bundle** — autumn theme. Embergrove Mage robes + Emberkin pup +
   Embergrove building palette; mid economy. ~tier 3.
3. **Bloomtide / Spring Awakening** — spring theme. Bloomtide Ranger + Bloomtide
   banners + a fresh green building palette; small-mid economy. ~tier 2 ($4.99).
4. **Starter's Hand** — pure onboarding value pack: heavy crystals/food/coins +
   a few instant-build tokens + one common cosmetic. New-player conversion. ~tier 1–2.
5. **Echo Patron Pack** — economy headstart themed around the echo workforce /
   harvest loop: large coins/food + harvest-auto-collect tokens + a workshop palette.
   ~tier 4 ($19.99).
6. **Hero Wardrobe Pack** — cosmetic-forward: a multi-piece Knight wardrobe set (ties
   into the Wardrobe/Dressable system) + exclusive; light economy. ~tier 3.
7. **Realm Defender Bundle** — combat-cosmetic flair: weapon/shield cosmetic flair for
   the Knight (visual only — covenant: no combat power) + banner; mid economy. ~tier 3.
8. **Builder's Cache** — convenience-forward (once the token tray exists): big
   instant-build + instant-repair counts + an xp-weekend + a building palette. ~tier 4.

Each slot = a `PackDef` per the §4 schema; each needs matching cosmetic entries added
to `cosmetics.json` so the granted SKUs resolve.

---

## 6. Key files (absolute paths)

- `C:\eoa\Assets\_Modules\Wallet\PackCatalog.cs` — model + loader
- `C:\eoa\Assets\_Modules\Wallet\PackStore.cs` — store UI + purchase flow + grant
- `C:\eoa\Assets\_Modules\Wallet\WalletService.cs` — wallet surface (Connect/Pay/PayFlat)
- `C:\eoa\Assets\_Modules\Wallet\StubWalletProvider.cs` / `SolanaWalletProvider.cs` — providers
- `C:\eoa\Assets\_Modules\Wallet\CryptoPaymentManager.cs` — Glimmer top-up bridge
- `C:\eoa\Assets\_Modules\Wallet\WalletRegistry.cs` — treasury address
- `C:\eoa\Assets\_Modules\Village\Buildings\MarketplaceInteractor.cs` — (disabled) entry point
- `C:\eoa\Assets\StreamingAssets\Data\Canonical\packs.json` — pack data (source)
- `C:\eoa\Assets\Resources\Data\Canonical\packs.json` — pack data (runtime-loaded mirror)
- `C:\eoa\Assets\_Modules\Cosmetics\CosmeticCatalog.cs` + `...\Canonical\cosmetics.json` — Glimmer cosmetics
- `C:\eoa\Assets\_Modules\Core\State\GameState.cs` (OwnedItemIds) / `SaveSchema.cs` (persist)
- `C:\eoa\docs\monetization-v2-spec.md` — the canonical spec the data was extracted from
