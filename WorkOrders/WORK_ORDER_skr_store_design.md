<!-- status-reconcile-2026-08-22 -->
> # CONTRADICTS CANON - DO NOT IMPLEMENT. **WE NEVER HOLD SKR.**
> **Flagged 2026-08-22 by the status/evidence audit. Body preserved as history; do not act on it.**
>
> This WO specs an **in-game SKR balance we credit, debit and spend from** (a held-SKR premium store, an
> `ISkrLedger`). That is **forbidden by name, in the shipping code**:
>
> > `Assets/_Modules/Wallet/PackStore.cs:467-473` -
> > *"THE GAME NEVER HOLDS SKR AND MUST NEVER READ AS IF IT DOES. SKR is Solana Mobile's own governance
> > token - the owner did not mint it, does not own it, and is not releasing a token of her own; it is the
> > settlement rail a dApp Store title converts out through. There is NO in-game SKR ledger, earn loop or
> > spend loop and there must never be one. This label is a READ-ONLY MIRROR of the player's OWN wallet ...
> > Never written, never granted, never deducted in-game."*
>
> Same ruling in `Assets/_Modules/Core/Platform/StakeRewardsResolver.cs:5-7` ("we NEVER mint it, NEVER
> custody it, NEVER hold a withdrawable in-game balance") and in the user-level canon
> (*SKR is Solana Mobile's governance token - not ours, never minted, never held; the only real balance is
> the player's wallet, read-only*).
>
> **The only sanctioned SKR surface is a read-only mirror of the player's own wallet.** Any successor WO
> must start from that constraint, not from this document's ledger model.
> Also note the 2026-08-17 era-sweep banner below is superseded by this one: the subject **was** superseded,
> the sweep simply had no evidence of it.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — SKR Store (Premium Token Economy + Store)

**Type:** DESIGN SPEC (ideas + data schema). **No `.cs` in this WO** — implementation is a follow-up WO.
**Status:** DRAFT FOR OWNER REVIEW — not yet READY TO IMPLEMENT.
**Author lane:** Monetization/Backend (§9 parallel lane — fully isolated).
**Date:** 2026-06-28
**Supersedes nothing.** *Layers on top of* the existing `docs/monetization-v2-spec.md` + `Assets/_Modules/Wallet/PackStore.cs` + `packs.json`. Does **not** replace them.

---

## 0. The one-sentence shape

> **SKR is a player-held premium token balance.** The existing PackStore (real money / SOL / USDC / SKR-rail) **tops up that balance**; a new **SKR Store** then **spends the balance** on cosmetics, convenience, and exclusive crowns/skins — all data-driven, ethical (cosmetic/convenience-first, never combat power), and on a thin runtime interpreter so the catalog is content, not code.

### Why this is new (read before building)

The repo **already has SKR** — but only as a *payment rail*: `CurrencyKind.Skr` in `Assets/_Modules/Wallet/WalletService.cs`, and `PackPricing.skr` in `packs.json`. Today you *pay a pack in SKR* the way you'd pay in SOL. There is **no SKR you hold, see a balance of, or spend in a dedicated store.**

The owner's "SKR store or something" asks for the missing half: **SKR as a held premium currency** (a wallet balance like gems/crystals in a F2P game) **with its own store catalog.** This WO designs that held-balance layer and the store that spends it, reusing every existing piece (PackStore for acquisition, OwnedItemIds for entitlements, the wardrobe/cosmetic SKU system for grants).

---

## 1. WHAT SKR IS

**SKR = the premium token currency of Echoes of Elarion.** One unified premium balance the player owns, distinct from the soft economies.

### 1.1 The currency stack (where SKR sits)

| Layer | Currency | Earned by | Spent on | Authority |
|---|---|---|---|---|
| Soft — harvest | **Wood / Iron / Grain** (echo workforce) | Echoes harvesting (drag-drop, cap 5) | Building, crafting, upgrades | Local save |
| Soft — coin | **Gold / Coins** | Combat, quests, selling | Store stock, building-tier research (WC3 tree) | Local save |
| Soft — build | **Crystals** | Gameplay, pack top-ups | Tower builds, premium-ish convenience | Local save |
| Cosmetic-soft | **Glimmer** | Gameplay | Cosmetic shop (existing) | Local save |
| **PREMIUM** | **★ SKR ★** | **Real money, achievements, on-chain (staged)** | **SKR Store: cosmetics, convenience, exclusive crowns/skins** | **Local → cloud → Solana (staged)** |

SKR is the **only** currency with a real-money / on-chain acquisition path. Everything else is earned in-game. This separation is the ethical firewall: SKR buys **time and beauty, never power** (the covenant, `monetization-v2-spec.md` §2).

### 1.2 How SKR is acquired (three paths, all optional)

1. **Bought with real money / crypto (primary).** The existing **PackStore** becomes the SKR *on-ramp*. Today a pack grants `crystals/food/coins`; we add an **`skr` grant** to the pack contents bag (a "Token Pouch" line). Buying *Lanternlight* with Stripe/SOL/USDC/SKR-rail credits the player's **SKR balance** in addition to its cosmetics. We also add **pure SKR-pouch packs** (SKU `skr-pouch-*`) whose only content is an SKR amount — the classic "buy gems" tiers.
2. **Earned via play / achievements (the generous covenant).** `monetization-v2-spec.md` §12 already designs **achievement SKR drops** (first wave 0.5 SKR, first dungeon 2 SKR, …) funded by the owner's staked-SKR yield. Those drops **credit the same held balance.** A non-spending player still accrues a (modest) SKR balance and can shop the SKR Store. **"Never required to spend, ever"** holds — every SKR-Store item is *also* reachable through earned SKR or has a soft-currency equivalent.
3. **On-chain via the staged Solana path (last, optional).** Per `data-architecture-hybrid-db-direction` (T2 player state: local → cloud → Solana). At V1 the balance is a **local save integer** behind an `ISkrLedger` seam (see §6). Later the seam resolves the balance from cloud, then from the player's Solana wallet (real SPL SKR). **No wallet is required to play or to hold a local SKR balance.**

### 1.3 The design stance — ethical, not pay-to-win (BINDING)

Inherits the covenant verbatim from `monetization-v2-spec.md` §2 and the single-Knight V1 (`combat-pivot-single-hero-northstar`):

- **SKR buys cosmetics + convenience ONLY.** Never combat stats, never higher caps, never permanent passives. The `category` field on every catalog entry is constrained to `{cosmetic, convenience, premium_pack, exclusive}` — there is no `combat` category and the validator rejects one (§5.4).
- **Every SKR item is also earnable** (soft-currency twin or achievement path) — except the deliberately-scarce `exclusive` crowns/skins, which are *expression*, never advantage.
- **Full contents shown pre-purchase. No loot boxes, no gacha, no randomized SKR spends** (C3). An SKR "pack" is a transparent bundle, not a roll.
- **No FOMO timers** except the single ethical scarcity moment already sanctioned (Founder's-Vow-style launch window), and even that is `optional` per entry.
- **Single-Knight V1 fit:** cosmetics target the one Knight + his weapon/shield flair + pet skins + village ambiance — exactly the surfaces the pivot kept. No "buy a second hero."

---

## 2. THE SKR STORE — what SKR buys

A new **player-initiated** store screen (discovery mirrors `monetization-v2-spec.md` §7.1 — a `🪙` glyph; never a pop-up, C5). It reads `skr_store.json` and renders one card per entry, each priced **in SKR** and showing its grant. The balance is shown top-right; "Get more SKR" routes to PackStore (§3). Built in **code-built uGUI** per the Obsidian/Blink UI canon (`ui-blink-template-master-frame-formula`) — **no UXML in builds** (§8 of CLAUDE.md). Reuse `BuildObsidianPanel(frameName)` drop-zones; the store drops chrome-less cards in.

### 2.1 The catalog, tiered

| Tier | Catalog band | SKR cost band | Examples | `category` |
|---|---|---|---|---|
| **T1 — Trinkets** | Small expression | 5–25 SKR | Weapon glow, shield crest decal, single pet-skin variant, lantern-color swap | `cosmetic` |
| **T2 — Attire** | Knight outfits / wardrobe | 30–80 SKR | Knight armor reskin (static, per pivot), cape/tabard variants, banner palette | `cosmetic` |
| **T3 — Convenience** | Time-savers (consumable tokens) | 10–60 SKR | Instant-build token x5, instant-repair x5, harvest auto-collect 24h, 2×-XP weekend | `convenience` |
| **T4 — Premium packs** | Curated SKR bundles | 100–300 SKR | Themed set: outfit + pet skin + ambient theme + token bundle (an SKR-priced mirror of the PackStore packs) | `premium_pack` |
| **T5 — Exclusive crowns/skins** | Prestige expression, scarce | 250–1000 SKR | Founder crown, Keeper's-Tournament champion skin, animated weapon VFX, in-village inscribed banner | `exclusive` |

Notes:
- **T3 convenience tokens** are the bent-covenant items (`monetization-v2-spec.md` §5.3) — already-permitted: instant-build / instant-repair / harvest-auto-collect / XP-weekend. **Forbidden list still binds** (no fire-rate, no permanent passive, no cap raise). Tokens are finite consumables landing in the `consumables`/token tray.
- **T5 exclusives** carry an optional `availability` window (the single ethical scarcity moment). Owners keep them forever; the SKU stops selling after the window. Everything else is permanent stock.
- **`premium_pack` (T4)** = an *SKR-denominated* twin of a `packs.json` pack. It **reuses the same cosmetic SKUs + convenience defs** — DRY. The only new thing is "you can also buy this bundle by spending held SKR instead of a fresh real-money transaction."

### 2.2 Grants — what a purchase actually does

A purchase **debits SKR** and **applies a grant bag** (same fulfillment shape PackStore already uses — `ApplyPackContents` → `OwnedItemIds` + economy + token tray). Grant kinds (data-driven, §5):

- `cosmetic_sku` — adds a wardrobe/cosmetic SKU to `OwnedItemIds` (reuses `BlinkWardrobe`/`VisualFactory.Skin`, `wardrobe-dressable-capability`).
- `convenience_token` — adds N consumable tokens (kind ∈ existing `ConvenienceItemDef.Kind`).
- `economy` — soft-currency top-up (crystals/food/coins/glimmer) — for `premium_pack` parity.
- `bundle` — a list of the above (for T4/T5).

---

## 3. DUAL-CURRENCY MODEL — coexistence, not replacement

```
 REAL MONEY / CRYPTO                 HELD PREMIUM                    SOFT (in-game)
 ┌───────────────────┐   tops up    ┌──────────────┐   spends on    ┌──────────────────┐
 │  PackStore         │ ───────────▶ │  SKR balance │ ─────────────▶ │  SKR Store        │
 │  (Stripe/SOL/USDC/ │              │  (ISkrLedger)│                │  (cosmetics /     │
 │   SKR rail)        │              └──────────────┘                │   convenience /   │
 │  packs.json        │                   ▲                          │   exclusive)      │
 └───────────────────┘                   │ credits                  └──────────────────┘
        │ also grants                     │
        │ cosmetics/economy        ┌──────┴────────┐
        ▼                          │ Achievement   │  (yield-funded, §12 v2-spec)
   OwnedItemIds                    │ SKR drops     │
                                   └───────────────┘

 Wood/Iron/Grain (echoes) ─┐
 Gold/Coins ───────────────┼─▶ stay 100% in-game. NEVER buyable with SKR; SKR never buys power.
 Crystals / Glimmer ───────┘    (Glimmer cosmetic shop continues to exist alongside the SKR Store.)
```

**Rules of coexistence (BINDING):**
1. **SKR never buys soft currency directly** beyond the curated `premium_pack` parity bundles (which exist already in `packs.json`); and **soft currency never buys SKR.** The membranes are one-directional and explicit so the premium tier can't be farmed or inflated by grinding.
2. **PackStore is unchanged in spirit** — it keeps selling packs for real money / crypto. We only **add** (a) an `skr` grant field to a pack's contents bag, and (b) new `skr-pouch-*` SKUs whose content is purely SKR. No existing pack SKU, price, or flow is removed.
3. **Glimmer cosmetic shop stays.** SKR Store is the *premium* cosmetic/convenience tier; Glimmer is the *earned* cosmetic tier. Some SKUs appear in both (earn with Glimmer **or** buy instantly with SKR) — the SKU system already supports dual sourcing (`monetization-v2-spec.md` §5.1).
4. **One fulfillment path.** Both PackStore and SKR Store converge on the same entitlement writer (`OwnedItemIds` + economy + token tray + `Save()`), so there is exactly one place a grant lands — no parallel inventory logic.

---

## 4. DATA SCHEMA — thin interpreter, zero hardcoded branches

Owner thinks in **data structures** (`owner-thinks-in-data-structures`): the store is a **lookup table over a thin runtime interpreter**. Three tables: **balance**, **catalog entries**, **acquisition packs**. The runtime does exactly four verbs — `GetBalance`, `Credit`, `Debit`, `ApplyGrant` — and *never* switches on a SKU name. New items = new JSON rows, never code.

### 4.1 Files

- **Catalog (new):** `Assets/StreamingAssets/Data/Canonical/skr_store.json` (mirrored to `Assets/Resources/Data/Canonical/skr_store.json` per the WebGL `CanonicalJson.Read` pattern PackCatalog already uses). **A starter example is delivered alongside this WO** at that path.
- **Schema (this doc, §5).** The runtime types mirror `PackCatalog.cs` style (Newtonsoft `[JsonProperty]`, static loader, `EnsureLoaded`).
- **Balance:** lives in player save (`GameState`) behind `ISkrLedger` — *not* a new currency in `ResourceBalance` (that struct is the *soft* wallet; SKR is premium and staged toward on-chain authority, so it gets its own seam). See §6.

### 4.2 Record shapes (the tables)

**Table A — SKR balance** (player state, one row):
```
SkrLedger {
  balance        : long      // whole SKR is fine for V1 display; store milli-SKR if sub-unit drops needed
  pendingClaims  : [ SkrDrop ]   // wallet-less achievement drops awaiting claim (v2-spec §12)
  lastSyncUtc    : string    // ISO-8601; cloud/Solana reconcile marker (staged)
  source         : enum { local, cloud, onchain }   // which authority currently owns the balance
}
SkrDrop { id:string, amount:number, reason:string, grantedUtc:string }
```

**Table B — SKR catalog entry** (the store rows):
```
SkrCatalogEntry {
  id            : string                 // stable SKU, e.g. "skr_crown_founder"
  name          : string                 // display
  tagline       : string                 // narrative-bible voice
  tier          : int 1..5
  category      : enum { cosmetic, convenience, premium_pack, exclusive }   // NO 'combat' — validator rejects
  costSkr       : number                 // price in SKR
  grant         : Grant                  // what the purchase delivers (see Table D)
  iconId        : string                 // Addressable/Resources pointer (binary stays out of DB — pointer only)
  availability  : Availability?          // optional scarcity window (exclusives); absent = permanent stock
  earnableHint  : string?                // copy: how to also get this without spending (covenant transparency)
  repeatable    : bool                   // false for cosmetics/exclusives (own-once), true for convenience tokens
}
Availability { startUtc:string?, endUtc:string?, launchWindowOnly:bool }
```

**Table C — SKR acquisition pack** (real-money → SKR; the on-ramp rows):
```
SkrAcquisitionPack {
  sku           : string                 // "skr_pouch_small", reuses PackStore purchase rails
  name          : string
  skrAmount     : number                 // SKR credited on purchase
  bonusSkr      : number                 // "+10% bonus" marketing line; 0 if none
  pricing       : { usd, usdc, sol, skr }  // SAME shape as PackPricing in PackCatalog.cs (reuse type)
  founderOnly   : bool
}
```
> `SkrAcquisitionPack` is intentionally **the same shape as `PackDef` minus the cosmetic bag** — at implementation time it can simply be `packs.json` packs whose `contents.skr` is set, plus pure-SKR SKUs. Prefer **extending `packs.json`** over a third file if the team wants one fewer catalog; this WO keeps them separate for clarity but flags the merge as a valid simplification.

**Table D — Grant** (shared by Table B and pack contents):
```
Grant {
  kind        : enum { cosmetic_sku, convenience_token, economy, bundle }
  cosmeticSku : string?                  // kind=cosmetic_sku
  token       : { kind:string, count:int }?   // kind=convenience_token (kind ∈ ConvenienceItemDef.Kind)
  economy     : { glimmer, crystals, food, coins }?  // kind=economy
  items       : [ Grant ]?               // kind=bundle (recursive; the T4/T5 path)
}
```

### 4.3 The thin interpreter (design — no code here)

```
Purchase(entryId):
  entry  = Catalog.Find(entryId)            // table lookup, no switch
  Guard: entry != null, entry available now, !(owned && !entry.repeatable)
  Guard: Ledger.Balance >= entry.costSkr    // FlowTrace.Fail on insufficient — never silent
  Ledger.Debit(entry.costSkr)               // ISkrLedger
  ApplyGrant(entry.grant)                   // recursive, reuses PackStore.ApplyPackContents fulfillment
  Save()                                    // round-trip; verify entitlement landed (v2-spec pattern)
  FlowTrace.Step(...)                       // instrument every step (§12 CLAUDE.md)
```
`ApplyGrant` recurses on `bundle`, dispatches the other three kinds to the **existing** grant sinks (`OwnedItemIds`, token tray, `ResourceBalance`). **No SKU is special-cased.** Adding "skr_crown_dragon" is a JSON row; the interpreter already knows how to grant a `cosmetic_sku`.

---

## 5. JSON SCHEMA (authoritative) + delivered example

The machine-readable schema + a starter catalog ship with this WO at:
`Assets/StreamingAssets/Data/Canonical/skr_store.json` (the data) — its shape is the §4.2 tables. The store loader (`SkrStoreCatalog`, future WO) mirrors `PackCatalog.cs`: `CanonicalJson.Read("Data/Canonical/skr_store.json")` → Newtonsoft parse → cached typed records.

### 5.4 Validation invariants (regression-gated, the data-regression lane)
A `SkrStoreRegression` (future WO) asserts, headlessly:
1. Every `category` ∈ `{cosmetic, convenience, premium_pack, exclusive}` — **a `combat`/stat grant fails the build.** (The hard pay-to-win firewall.)
2. Every `costSkr > 0`; every `grant.kind` valid; every `cosmeticSku` resolves to a real SKU; every `token.kind` ∈ `ConvenienceItemDef.Kind`.
3. No entry grants `crystals/coins/food` **above** the equivalent SKR-pouch value (anti-inflation of soft economy).
4. Every non-`exclusive` entry has a non-empty `earnableHint` (covenant transparency: shows the free path).
5. `iconId` is a pointer string only — **no binary inlined** (`data-architecture` T1 rule).

---

## 6. SOLANA-READINESS — staged, never required for V1

Maps 1:1 onto the ratified data architecture (`data-architecture-hybrid-db-direction`, T2 player state: **local → cloud → Solana**):

- **`ISkrLedger` seam (the whole trick).** All SKR reads/writes go through one interface — `GetBalance / Credit / Debit / Reconcile`. V1 ships `LocalSkrLedger` (a save integer + `pendingClaims`). The store, the achievement drops, and PackStore top-ups all talk to the seam, **knowing nothing about where the balance lives.** This is the exact `ISaveProvider` / `ICatalogSource` pattern already ratified.
- **Stage 1 (V1, now):** `LocalSkrLedger` — balance is a number in the save. **No wallet, no network, no Solana SDK.** The game is fully playable and the SKR Store fully functional offline. `source = local`.
- **Stage 2 (cloud):** `CloudSkrLedger` resolves the balance from the cloud save DB (the deliberate first online dependency). Reconcile-on-boot like the entitlements slice. `source = cloud`. **Binary never in DB** — the ledger is a number; icons stream via Addressables (§5.4 inv. 5).
- **Stage 3 (on-chain, last):** `SolanaSkrLedger` reads/writes the player's real SPL **SKR** balance via the existing `WalletService` / `SolanaWalletProvider`. The `Debit` becomes an on-chain transfer (or a backend-verified spend, like the pack purchase verifier `monetization-v2-spec.md` §8.3). `source = onchain`. **Still optional** — a wallet-less player stays on `local`/`cloud` forever and the store still works with locally-held/earned SKR; only *on-chain-owned* SKR needs the wallet.
- **Integrity follows the staged rule** (`data-architecture` integrity note): local at-rest = HMAC via the save seam (detect tamper); valuable on-chain SKR = Solana authority (prevent). Don't build server anti-tamper before the cloud stage exists.

**Net:** V1 is a self-contained premium-currency store with a real-money on-ramp through the existing PackStore, and a clean seam so cloud and Solana light up later **without touching the store, the catalog, or the player.**

---

## 7. What NOT to touch / build (scope guard)

- **Do NOT** add SKR to `ResourceBalance` (that's the soft wallet; SKR is premium + staged on-chain → its own seam).
- **Do NOT** rewrite PackStore — only **add** an `skr` grant field + `skr-pouch-*` SKUs to `packs.json` (additive).
- **Do NOT** greenfield a wallet/payment system — `WalletService`/`CurrencyKind`/`SolanaWalletProvider` exist; reuse them.
- **Do NOT** introduce any `combat`/stat-affecting grant — the validator (§5.4 inv.1) is a build gate.
- **Do NOT** author cards in UXML — code-built uGUI Obsidian panel only (UXML renders empty in builds).
- **Do NOT** put binary (icons/skins) in the catalog/DB — pointer strings only.

## 8. Acceptance (for the follow-up IMPLEMENTATION WO, not this one)

- [ ] `skr_store.json` loads via `CanonicalJson.Read`; typed `SkrCatalogEntry` records hydrate (mirror `PackCatalog`).
- [ ] `ISkrLedger` seam exists; `LocalSkrLedger` persists balance + `pendingClaims` in save; no network dependency.
- [ ] SKR Store screen (code-built Obsidian uGUI) renders one card per entry, shows balance, "Get more SKR" → PackStore.
- [ ] Purchase debits SKR, applies grant via the **shared** PackStore fulfillment path, verifies entitlement landed, saves.
- [ ] PackStore additively credits SKR (pack `skr` grant + `skr-pouch-*` SKUs).
- [ ] Achievement SKR drops (`monetization-v2-spec.md` §12) credit the same ledger; wallet-less → `pendingClaims`.
- [ ] `SkrStoreRegression` enforces all §5.4 invariants headlessly (the pay-to-win firewall is a build gate).
- [ ] FlowTrace on every purchase step; no silent failure on insufficient balance / failed grant.
- [ ] Zero Solana SDK / wallet required for V1; `source=local` end-to-end.

---

## 9. Open questions for the owner (route before IMPLEMENT)

1. **Display unit** — whole SKR only, or sub-unit (achievement drops are fractional, e.g. 0.5 SKR)? Recommend store milli-SKR internally, display whole + one decimal.
2. **Merge catalogs?** Keep `skr_store.json` separate (clarity) vs fold acquisition packs into `packs.json` (one fewer file). WO assumes separate; merge is a sanctioned simplification.
3. **Exclusive scarcity** — do we want any `launchWindowOnly` crowns at V1 (the Founder's-Vow ethical-scarcity pattern), or all-permanent stock for the first release?
4. **Glimmer overlap** — which cosmetic SKUs are dual-sourced (earn w/ Glimmer **or** buy w/ SKR) vs SKR-exclusive? Needs an SKU map.
5. **Name** — "SKR Store" working title. Narrative-bible voice option: *"The Seeker's Coffer"* / *"Token Coffer"* to match the cozy framing.

---

## 10. Sources read (grounding)

- `docs/monetization-v2-spec.md` (the existing SKR-rail packs, covenant §2, yield rewards §12, treasury/Solana §8/§15)
- `Assets/_Modules/Wallet/PackStore.cs`, `PackCatalog.cs`, `WalletService.cs` (`CurrencyKind.Skr`, fulfillment, devnet stub)
- `Assets/_Modules/Core/State/NestedTypes.cs` (`ResourceBalance`), `GameState.cs` (`OwnedItemIds`)
- `packs.json` (`Assets/{StreamingAssets,Resources}/Data/Canonical/packs.json`)
- Memory: `data-architecture-hybrid-db-direction` (local→cloud→Solana staging, binaries-never-in-DB), `combat-pivot-single-hero-northstar`, `owner-thinks-in-data-structures`, `wardrobe-dressable-capability`, `ui-blink-template-master-frame-formula`
