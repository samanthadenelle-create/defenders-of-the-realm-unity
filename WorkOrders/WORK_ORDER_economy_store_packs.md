<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — Economy Store Packs (resource / farming-boost / offline-storage)

**Status:** DESIGN — ideas + legwork (NOT ready to implement until schema-extension sign-off)
**Type:** NEW CONTENT on an EXISTING system (PackStore) — do NOT greenfield
**Silo:** Monetization / Backend (CLAUDE.md §9 — isolated lane)
**Author:** design pass (no `.cs` written)
**Sample data:** `WorkOrders/economy_store_packs.sample.json` (PackDef-shaped, drop-in once schema is extended)

---

## 0. What the owner asked for

> "Packs for resources and speed boosts for farming, offline storage upgrades."

These are **store offerings layered on top of the existing PackStore** — they are CONTENT
(JSON), not new code, exactly like the existing five packs (Hearth Spark → Founder's Vow).
The intent is the **convenience / time-saving** monetization lane, never combat power
(the "bent covenant", monetization-v2-spec §5.3 — these packs honour it: resources, speed,
storage, never damage/armor).

This doc designs four product families + sample data so implementation is a JSON drop plus a
small, well-scoped schema extension.

---

## 1. Grounding — the REAL PackStore schema (verified from code)

Read before changing anything:
- `Assets/_Modules/Wallet/PackCatalog.cs` — typed model (`PackDef`, `PackPricing`, `PackEconomy`, `PackContents`, `ConvenienceItemDef`)
- `Assets/_Modules/Wallet/PackStore.cs` — store UI + `ApplyPackContents` (entitlement fulfilment)
- `Assets/StreamingAssets/Data/Canonical/packs.json` (+ mirror `Assets/Resources/Data/Canonical/packs.json`) — the live catalogue

### Current `PackDef` (verbatim shape)
```jsonc
{
  "sku": "lanternlight",
  "tier": 2,
  "name": "Lanternlight",
  "tagline": "...",
  "theme": "...",
  "founderOnly": false,
  "pricing": { "usd": 4.99, "usdc": 4.99, "sol": 0.045, "skr": 60 },
  "contents": {
    "cosmetics": ["cosmetic.lanternlight.exclusive"],
    "economy":   { "glimmer": 75, "crystals": 700, "food": 200, "coins": 400 },
    "convenience": [ { "kind": "instant-build", "count": 3, "description": "..." } ]
  },
  "packExclusiveCosmetic": "cosmetic.lanternlight.exclusive"
}
```

### Currency rails (`WalletService.CurrencyKind`)
`Sol = 0`, `Usdc = 1`, `Skr = 2`. USD is reference-only (display). **Wallet packs charge in
SOL / USDC / SKR.** `ApplyPackContents` currently lands `crystals / food / coins` into
`state.Resources` and records owned SKUs; convenience tokens are noted as "flagged for the
inventory pass" (not yet consumed).

### Existing convenience-kind vocabulary
`instant-build` · `instant-repair` · `harvest-auto-collect` · `xp-weekend`.

---

## 2. Schema extension required (the ONLY code/data-model legwork)

The current `PackEconomy` models `glimmer / crystals / food / coins`. The echo workforce
harvests **wood / iron / grain** — these are not yet economy fields. To ship the families
below, extend the model in three small, additive, backward-compatible ways. (All omitted
fields default to 0 / empty, so the existing five packs are untouched.)

### 2a. Add the three farm resources to `PackEconomy`
`PackCatalog.cs` → `PackEconomy`:
```csharp
[JsonProperty("wood")]  public int Wood;
[JsonProperty("iron")]  public int Iron;
[JsonProperty("grain")] public int Grain;
```
And `ApplyPackContents` adds `r.Wood += econ.Wood; r.Iron += econ.Iron; r.Grain += econ.Grain;`
(assumes the resource wallet / offline-storage system exposes these — coordinate with that
parallel design; clamp to current storage cap, see §5d).

### 2b. Add a soft-currency price track to `PackPricing`
Owner wants resource packs priced in **soft currency, gold, AND SKR**. The wallet rails cover
SKR (and SOL/USDC). Add an in-game-currency price so a pack can be bought **without a wallet**:
```csharp
[JsonProperty("coins")]    public int CoinsPrice;     // "gold"
[JsonProperty("crystals")] public int CrystalsPrice;  // "soft currency" / gems
```
Convention: a pack is **soft-purchasable** if `pricing.coins > 0` or `pricing.crystals > 0`;
it is **wallet-purchasable** if `usdc/sol/skr > 0`. A pack may offer both (buy with gold OR
fast-track with SKR). The store renders a soft-currency buy chip alongside the SOL/USDC/SKR
chips when a soft price is present. (Mixed-currency UI is a small `PackStore.BuildPackCard`
follow-up — out of scope for this design doc, noted for the impl WO.)

### 2c. Add new convenience kinds (timed boosts + storage)
No schema change to `ConvenienceItemDef` for the simple ones — they reuse `{ kind, count, description }`.
New kinds (handlers added in the inventory/consumable pass):
- `harvest-boost` — a timed harvest-rate multiplier (see the timed-boost model, §4a)
- `instant-fill-storage` — immediately fill every storage type to its current cap
- `workforce-slot` — permanent +1 echo workforce slot (hard cap 5, see §4c)
- `storage-tier-jump` — buy N storage-capacity tier(s) (see §5)
- `offline-window-extension` — extend the offline-accrual window (see §5)

A **timed boost** needs more than `count`, so add ONE optional nested object on the
convenience item (still backward compatible — null on every existing item):
```csharp
[JsonProperty("boost")] public BoostSpec Boost;   // null for non-timed kinds
```
```csharp
[Serializable] public sealed class BoostSpec {
    [JsonProperty("multiplier")]   public double Multiplier;    // 2.0 = 2x
    [JsonProperty("durationHours")]public double DurationHours; // 1 / 8 / 24
    [JsonProperty("appliesTo")]    public string AppliesTo;     // "all" | "wood" | "iron" | "grain"
    [JsonProperty("stack")]        public string Stack;         // "extend" | "refresh" | "reject" | "queue"
}
```

> **Implementation note:** this is the whole code surface — three int fields, two price ints,
> one nested `BoostSpec`, plus the matching `ApplyPackContents` lines and (later) the
> consumable handlers. Everything else in this WO is pure JSON content.

---

## 3. RESOURCE PACKS

Bundles of wood / iron / grain at four tiers — **single-resource** (for the player who is
bottlenecked on one thing) and **mixed** (balanced top-up). Priced in **gold (coins)**,
**soft currency (crystals)**, and **SKR** so a no-wallet player can still buy with earned
gold, while a wallet player can fast-track with SKR.

### Tier ladder (amounts are starting points — tune against the harvest-rate economy)
| Tier   | Mixed (each of W/I/G) | Single-resource | Gold (coins) | Soft (crystals) | SKR |
|--------|----------------------|-----------------|--------------|-----------------|-----|
| Small  | 250 each             | 750             | 500          | 40              | 10  |
| Medium | 750 each             | 2,250           | 1,400        | 110             | 25  |
| Large  | 2,000 each           | 6,000           | 3,600        | 280             | 60  |
| Mega   | 6,000 each           | 18,000          | 10,000       | 750             | 150 |

**Framing:**
- **Starter** — the Small mixed pack ("Hearthstock Crate") is flagged the *starter* offer:
  cheap, one-tap, deliberately under-priced as the first-purchase nudge.
- **Best value** — the Large mixed pack ("Granary Haul") carries the *best value* ribbon
  (most resource-per-currency before Mega; Mega is the whale tier, intentionally less
  efficient per unit so Large reads as the smart buy).
- Single-resource packs ("Timberload", "Ironload", "Grainload") exist at Medium + Large only —
  they target a specific bottleneck and price ~10% above the equivalent mixed slice per unit
  (you pay a small premium for exactly-what-you-need).

> ⚠ Resource grants must respect storage caps (§5d). A pack that would overflow the cap should
> either (a) clamp + warn, or (b) be gated behind owning the matching storage tier. Design
> choice for the offline-storage owner — flagged, not decided here.

---

## 4. FARMING SPEED BOOSTS

### 4a. Timed harvest-rate boosts (the timed-boost data model)
Time-limited multipliers on the echo workforce harvest rate. Sold as a convenience item with a
`boost` spec (§2c).

| SKU slug             | Multiplier | Duration | Applies to | Gold | Soft | SKR |
|----------------------|-----------|----------|------------|------|------|-----|
| `boost-2x-1h`        | 2x        | 1 h      | all        | 300  | 25   | 6   |
| `boost-2x-8h`        | 2x        | 8 h      | all        | 1,500| 120  | 30  |
| `boost-2x-24h`       | 2x        | 24 h     | all        | 3,500| 280  | 70  |
| `boost-3x-1h-rush`   | 3x        | 1 h      | all        | 600  | 50   | 12  |

**Stack rules (`boost.stack`):**
- `extend` (default for same-multiplier) — buying the same boost while active **adds its
  duration** to the remaining timer (no double-dipping the multiplier; honest value).
- `refresh` — resets duration to the new value if longer (used by daily-login grants).
- `reject` — a *higher* multiplier already active blocks a *lower* one from overwriting it
  (you never downgrade yourself by accident); surface "a stronger boost is already running".
- `queue` — a higher multiplier bought while a lower one runs **queues** to start when the
  current expires (premium 3x rush respects an active 2x by waiting, never wasted).
- A single active boost per `appliesTo` channel; `all` and a single-resource boost can run
  concurrently and **multiply** (2x all × 2x wood = 4x wood) — intentional, lets a whale
  stack a targeted rush. Cap the effective multiplier at **5x** to stop runaway stacking.

**Persistence:** boosts are wall-clock based (store `endsAtUtc`), so they keep ticking while
offline and interact with the offline-accrual window (§5) — an active boost multiplies offline
harvest too, up to the offline window length. This is a major value prop; call it out on the
card ("works while you're away").

### 4b. Instant-fill storage
`instant-fill-storage` (count = N uses). One tap fills **every** storage type (wood/iron/grain)
to its current cap. The pure-convenience "I don't want to wait" button.
| SKU slug              | Uses | Gold  | Soft | SKR |
|-----------------------|------|-------|------|-----|
| `instant-fill-1`      | 1    | 1,200 | 90   | 20  |
| `instant-fill-5`      | 5    | 5,000 | 380  | 80  |

### 4c. Extra echo / extra workforce slot
`workforce-slot` — a **permanent** +1 echo workforce slot. Recall the workforce cap is **5**
(3 organic born at life-force thresholds + up to 2 paid/flex slots, memory `echo-workforce-drag-drop`).
So this SKU sells the **+1 above organic growth, to a hard cap of 5** — buying it past 5 is
blocked (store greys it out / shows "workforce at maximum").
| SKU slug          | Grants            | Gold   | Soft | SKR | Notes |
|-------------------|-------------------|--------|------|-----|-------|
| `workforce-slot-1`| +1 echo slot (→4) | 8,000  | 600  | 120 | first paid slot |
| `workforce-slot-2`| +1 echo slot (→5) | 20,000 | 1,400| 280 | final slot, hard cap |

> The grant is a permanent capability, not a consumable — `ApplyPackContents` records it as an
> owned entitlement and bumps the workforce-cap stat. The slot still has to be *assigned* a
> harvest target by the player (drag-drop), preserving "passive-to-play, engaging-to-watch".

---

## 5. OFFLINE STORAGE UPGRADE PACKS

The convenience monetization for the parallel **offline-storage system** (storage caps +
upgrade tiers + an offline-accrual window). Two products:

### 5a. Storage-capacity tier jumps (`storage-tier-jump`)
Buy one or more storage-tier upgrades outright instead of grinding the soft-currency upgrade.
Each tier raises the per-resource cap (assume the storage system defines tiers T1…Tn with a
cap curve; these packs grant `count` tier(s)).
| SKU slug              | Grants      | Gold   | Soft | SKR | Framing |
|-----------------------|-------------|--------|------|-----|---------|
| `storage-tier-1`      | +1 tier     | 4,000  | 320  | 70  | soft-currency convenience |
| `storage-tier-3`      | +3 tiers    | 10,000 | 800  | 160 | "best value" jump |
| `storage-tier-max`    | to max tier | —      | —    | 400 | **SKR fast-track only** — whale skip |

### 5b. Offline-window extensions (`offline-window-extension`)
Extend how long the village keeps accruing while you're away (the classic idle-game offline cap).
Assume a base window (e.g. 8 h). These extend it — temporary (consumable) or permanent.
| SKU slug                 | Effect                         | Gold  | Soft | SKR | Stack |
|--------------------------|--------------------------------|-------|------|-----|-------|
| `offline-window-+8h`     | +8 h window, single use        | 1,000 | 80   | 18  | consumable, refresh |
| `offline-window-+24h`    | +24 h window, single use       | 2,600 | 200  | 45  | consumable, refresh |
| `offline-window-perm-24h`| permanent base window → 24 h   | —     | —    | 220 | **SKR fast-track**, permanent |

### 5c. SKR fast-track principle
Soft-currency / gold is the *grind* path (always available, no wallet). SKR (and SOL/USDC) is
the *fast-track* — the highest-end skips (`storage-tier-max`, `offline-window-perm-24h`) are
**SKR-only** to give the premium token a meaningful exclusive without selling combat power.

### 5d. Cap-interaction rule (cross-system flag)
Resource packs (§3) + instant-fill (§4b) + boosts that overflow must respect the **current
storage cap**. Recommended: grants clamp to cap and the store warns "storage full — upgrade
to hold more", which *itself* nudges the storage-tier packs. Final clamp-vs-gate behaviour is
the offline-storage owner's call — **flagged, not decided here.**

---

## 6. BUNDLES

Combo packs (resources + a boost + a storage tier) at a headline discount vs. buying the parts.
Discount math is illustrative — tune to ~20–30% off the à-la-carte sum.

- **Farmer's Bundle** (`bundle-farmers`) — the signature combo: Medium mixed resources +
  `boost-2x-8h` + `storage-tier-1`. Priced ~25% under the sum. The "everything a new farmer
  needs" hero offer; carries the **best value** ribbon in the bundles row.
- **Starter Stockpile** (`bundle-starter`) — Small mixed resources + `boost-2x-1h` +
  `instant-fill-1`, cheap, flagged *starter*, first-session nudge.
- **Granary Mogul** (`bundle-mogul`) — Mega mixed resources + `boost-2x-24h` +
  `storage-tier-3` + `workforce-slot-1`. The whale combo; SKR + soft both offered.
- **Weekly Deal** (`bundle-weekly-deal`) — a **recurring rotating slot**. One bundle is
  surfaced as "this week's deal" with a countdown and a steeper discount (~35%). Implement as
  a tag/flag on whichever pack is featured (see §7 `featured` + `rotationGroup`), NOT a new
  SKU each week — the rotation picks from a pool. Resets weekly (Monday, aligning with the
  existing "Monday sync" mentioned in PackCatalog).

---

## 7. Optional catalogue flags (legwork for the impl WO)

To support starter/best-value/weekly framing without bespoke code, add these optional
`PackDef` flags (all default false/empty — backward compatible):
```csharp
[JsonProperty("badge")]        public string Badge;        // "starter" | "best-value" | "" — drives the ribbon
[JsonProperty("featured")]     public bool   Featured;     // weekly-deal spotlight
[JsonProperty("rotationGroup")]public string RotationGroup;// e.g. "weekly-deal-pool"
[JsonProperty("category")]     public string Category;     // "resource" | "boost" | "storage" | "bundle" | "cosmetic"
[JsonProperty("discountPct")]  public int    DiscountPct;  // display "25% off" on bundles
```
`category` lets the store render these in a **separate "Economy" tab** from the cosmetic packs
(keeps the existing five untouched in their own row). This is the cleanest way to add the new
families without disturbing the canon five.

---

## 8. Acceptance criteria (for the future impl WO — NOT this design)

- [ ] `PackEconomy` gains `wood/iron/grain`; `ApplyPackContents` lands them, clamped to cap
- [ ] `PackPricing` gains `coins`/`crystals` soft-price; store renders a soft-buy chip
- [ ] `ConvenienceItemDef` gains optional `boost` (`BoostSpec`); new kinds handled
- [ ] New `PackDef` flags (`badge/featured/rotationGroup/category/discountPct`) parsed + ignored-when-absent
- [ ] The existing five packs still load + purchase identically (regression)
- [ ] Sample data (`economy_store_packs.sample.json`) loads with no parse errors
- [ ] Cap-interaction + workforce-cap-5 + SKR-only-fast-track rules enforced
- [ ] Timed-boost stack rules (`extend/refresh/reject/queue`, 5x cap) enforced + persist across offline

## 9. What NOT to touch
- Do **not** edit the canonical five packs in `packs.json`.
- Do **not** add combat-power to any pack (bent-covenant law, §5.3).
- Do **not** greenfield a second store — extend PackStore/PackCatalog.
- Do **not** hand-wire prices in `.cs` — all amounts flow from JSON (PackCatalog law).

## 10. Open questions for the owner / parallel-system owners
1. **Storage owner:** clamp-and-warn vs. gate-behind-tier when a resource grant overflows? (§5d)
2. **Storage owner:** exact tier cap curve + base offline window length (sample assumes 8 h base, T1…Tn).
3. **Economy owner:** are `coins`(=gold) and `crystals`(=soft) the right two soft tracks, or is
   there a distinct "gold" currency separate from coins?
4. **SKR owner:** confirm the SKR amounts (sample uses the existing ladder's ~12 SKR ≈ $1 feel).
5. Resource amounts in §3/§4 are placeholders — tune against the real harvest-rate curve.

## 11. ART STILL NEEDED
Every SKU below references an `artId` placeholder (`art.econpack.*`). Icons not yet produced —
needs: resource-crate icons (wood/iron/grain + mixed), a boost/hourglass icon, an
instant-fill icon, an extra-echo icon, storage-vault + offline-moon icons, and three bundle
hero cards. Flagged for the art pass; placeholders won't block wiring/testing.
