# WORK ORDER 1258 - DB-driven promo packs (Neon packs table + one APK for inline contents)

**Status:** FIXED — CODE + NODE + UNITY REGRESSION PASS; NEON/VERCEL ROLLOUT OWED
**Minted:** 2026-08-28 (Grok/docs seat). Consumes banner **1258**; this mint bumps the main line to **1259**.
**Lane:** Monetization / live-ops. Neon `packs` + `api/promo/redeem.js` + **ONE APK** so the client applies server `contents` without `PackCatalog.Find`.
**Priority:** Follow-up to **WO-1256** (weekend crystals/coins two-tier). Do not block 1256. Do not ship welcome-500/welcome-100 until this APK is live.
**Silo:** `api/schema.sql` + `api/promo/redeem.js` + `Assets/_Modules/Core/Promo/PromoCodeService.cs` + `Assets/_Modules/Wallet/PackStoreVM.cs` (grant seam only). Night Market shelf-from-DB is NOT this ticket.
**Provenance:** Samantha architecture 2026-08-27/28 (Monetization spec + last-night ruling): the **DB row IS the pack**. Promo grants must not look up `packs.json`. One APK learns to apply inline `contents`; after that APK, INSERT freebie packs with no further builds.
**Depends on:** **WO-1256** (nullable `tier1_max` / `tier1_crystals` / `tier1_coins` on `promo_codes`; generic two-tier pick in redeem.js). If 1256 has not landed, land it first or include its ALTER; do not re-specify the weekend FIRSTWATCH crystal/coin campaign here.
**Cross-refs:** **WO-1256** (weekend MVP - crystals/coins two-tier, `reward_pack_sku` NULL). **WO-1115** (redeem rail). **WO-1244** (ops write). **WO-755** / `packs.json` (seed source once, then retired as promo authority). `api/schema.sql` reward_pack_sku comment (the "name a pack" ruling stays; the *authority* moves from `packs.json` to Neon `packs`).
**Numbering note:** CLI MAIN LINE (banner next-free **1258**). NOT UI block, NOT PROD (owner rule: new functionality -> WO).

---

## 1. Goal

Cut promo pack grants over to a Neon `packs` table so a code names a SKU, redeem.js **loads the pack from DB**, snapshots the contents, and returns `{ crystals, coins, packSku, contents }`. The client applies that `contents` through `PackStoreVM.ApplyPackContents` / `EconomyService.GrantSpendablePurchased` + `AddCoins` and **must not** call `PackCatalog.Find` on the promo path.

Weekend 1256 stays crystals/coins. This ticket is the resource pack path (wood/iron/stone + crystals/coins, plus cosmetics/convenience bags) without `reward_wood` columns and without shipping a new `packs.json` every campaign.

## 2. Acceptance

1. Neon table `packs` exists: `sku TEXT PRIMARY KEY`, `name TEXT NOT NULL`, `contents JSONB NOT NULL` (PackContents JSON), `active BOOLEAN NOT NULL DEFAULT TRUE`, `store_visible BOOLEAN NOT NULL DEFAULT FALSE`, `created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()`. Proven by `\d packs`.
2. `contents` jsonb **is** C# `PackContents` (`PackCatalog.cs`): `{ "cosmetics": [], "economy": { "wood", "iron", "stone", "crystals", "coins" }, "convenience": [{ "kind", "count", "description"? }] }`. Economy keys match `PackEconomy` (`stone` is the JSON key; C# field is still `Food`). Do **not** flatten wood/iron/stone into top-level columns. Do **not** add `reward_wood` / `reward_iron` / `reward_stone` on `promo_codes`.
3. One-time seed: existing `Assets/Resources/Data/Canonical/packs.json` rows INSERTed into `packs` (sku, name, contents from `contents`, `active=TRUE`, `store_visible` from `storeVisible`). After seed, **DB is the pack** for promo. `packs.json` is no longer source of truth for grants. Do not keep a dual-write.
4. `promo_codes.reward_pack_sku` references `packs.sku` (nullable FK, ON DELETE RESTRICT / no CASCADE). New nullable `reward_pack_sku_tier2 TEXT` (same FK). Reuse **WO-1256** `tier1_max` for first-N vs rest. When a pack SKU is set it **WINS** (existing precedence): crystal/coin columns (including `tier1_crystals` / `tier1_coins`) are ignored for that redeem. Those columns stay for 1256-style codes; do not DROP them this ticket.
5. `api/promo/redeem.js` loads the chosen pack row from `packs` (not from a JSON file, not from a hardcoded map). If `n < tier1_max` (when `tier1_max` set) use `reward_pack_sku`; else use `reward_pack_sku_tier2` if set, else `reward_pack_sku`. **No code-string branch.**
6. Snapshot the granted pack into `promo_redemptions`: add nullable `pack_sku TEXT` + `contents JSONB`. Existing `crystals` / `coins` columns still snapshot the economy crystals/coins (derived from `contents.economy`) so 1256 audit stays valid.
7. Success body: `{ success: true, reward: { crystals, coins, packSku, contents }, message }`. `contents` is the **snapshot** jsonb, not a live re-read.
8. **Refuse-before-burn** if the resolved pack is missing, `active=FALSE`, or contents is empty (no positive economy amount AND empty cosmetics AND empty convenience). `REWARD_UNAVAILABLE`, no INSERT. Same invariant as today: a code must never burn for nothing. DB row IS the pack — an unknown SKU is a refuse, not a client Find.
9. Client: **ONE APK**. Promo apply path deserializes server `contents` into `PackContents` and grants via `ApplyPackContents` / `GrantSpendablePurchased` + `AddCoins` (plus existing cosmetic / convenience seams). **Capability flag** in the redeem body (additive `supportsInlinePackRewards: true`, or a clearly named successor of `supportsPackRewards` that means *inline contents*, not *I can Find*). Pack-sku codes refuse-before-burn unless that flag is true. **Fail closed** if `contents` is missing/empty after a successful redeem — do **not** fall back to `PackCatalog.Find`. `Find` remains legal for Night Market browse/purchase only.
10. After that APK is proven live: INSERT `welcome-500` and `welcome-100` (section 6). `store_visible=FALSE`. No second APK. No `packs.json` edit required for those two SKUs.
11. No payment bypass. Promo is a gift grant on the promised path, not `WalletService.Pay`, not a skipped purchase. Existing three payment-refusal layers stay intact.
12. Code string never logged, never in traces, never in `promo_redeemed` analytics (existing rule).
13. Night Market shelf-from-DB is **out of scope** (later WO). `store_visible` is on the table so that WO has a column; this ticket does not switch `PackStore` off `packs.json`.

## 3. What EXISTS (do not rebuild)

| Piece | Where | Status |
|---|---|---|
| Weekend two-tier crystals/coins | WO-1256 / `tier1_*` columns / redeem.js pick | SEPARATE TICKET. Land 1256. This ticket consumes `tier1_max` for pack SKU selection. |
| Pack contents type | `PackCatalog.PackContents` + `PackEconomy` + `ConvenienceItemDef` | SHIPPED. Hydrate from JSON. |
| Grant seam | `PackStoreVM.ApplyPackContents(PackDef)` | SHIPPED. Uses `pack.Contents.Economy` -> `GrantSpendablePurchased` + `AddCoins`, cosmetics, convenience. Today requires a `PackDef` (and paid path uses `PackCatalog.Find`). **This ticket adds a contents-without-Find overload.** |
| Promo client | `PromoCodeService` | SHIPPED. Grants server crystals/coins. `supportsPackRewards` exists as a body flag; pack SKU still refused unless advertised. No inline `contents` apply. |
| redeem.js pack SKU | `api/promo/redeem.js` 1c | SHIPPED refuse-before-burn if `reward_pack_sku` set and client did not advertise pack support. Does **not** load a pack. Returns `packSku` only. |
| Catalog file | `packs.json` | SHIPPED Night Market + impulse SKUs. Seed **once**. Not promo authority after cutover. |
| schema comment | `api/schema.sql` reward_pack_sku | "Name a PACK; do not add reward_wood columns." Binding. This ticket keeps that law and moves the named pack into Neon. |

## 4. What to CREATE

### 4a. Neon `packs`

```sql
CREATE TABLE IF NOT EXISTS packs (
    sku            TEXT        PRIMARY KEY,
    name           TEXT        NOT NULL,
    contents       JSONB       NOT NULL,
    active         BOOLEAN     NOT NULL DEFAULT TRUE,
    store_visible  BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE promo_codes
    ADD COLUMN IF NOT EXISTS reward_pack_sku_tier2 TEXT;

-- FKs: add only after packs is seeded enough that existing reward_pack_sku values (likely all NULL) are valid.
-- ALTER TABLE promo_codes ADD CONSTRAINT promo_codes_reward_pack_sku_fk
--     FOREIGN KEY (reward_pack_sku) REFERENCES packs(sku);
-- ALTER TABLE promo_codes ADD CONSTRAINT promo_codes_reward_pack_sku_tier2_fk
--     FOREIGN KEY (reward_pack_sku_tier2) REFERENCES packs(sku);

ALTER TABLE promo_redemptions
    ADD COLUMN IF NOT EXISTS pack_sku TEXT;
ALTER TABLE promo_redemptions
    ADD COLUMN IF NOT EXISTS contents JSONB;
```

Document in `api/schema.sql` the same way 1256 documents `tier1_*`: comment + ALTER, do not put new columns in CREATE TABLE bodies until live (schema-parity.mjs).

`contents` example (welcome-100):

```json
{
  "cosmetics": [],
  "economy": { "wood": 100, "iron": 100, "stone": 100, "crystals": 100, "coins": 100 },
  "convenience": []
}
```

### 4b. Seed from packs.json once

One operator script or SQL generator. Map each pack: `sku`, `name`, `contents` = the JSON `contents` object (cosmetics/economy/convenience), `store_visible` = `storeVisible`, `active=TRUE`. Hidden rows stay hidden. Do not invent SKUs. Do not rewrite prices (prices are not on this table; Night Market still owns pricing in json until the later WO).

After seed: promo path never reads `packs.json`. Editing a promo pack = UPDATE `packs.contents` (or INSERT a new sku and point the code). Changing json must not change what an already-seeded sku grants unless you re-seed on purpose (do not build a sync daemon this ticket).

### 4c. redeem.js

Keep every 1256 / 1115 gate. After the two-tier **crystal/coin** pick (1256), if a pack SKU is selected:

1. SELECT `packs` WHERE `sku = chosen` AND `active = TRUE`.
2. If no row: refuse-before-burn `REWARD_UNAVAILABLE`.
3. Parse `contents`. Empty (all economy <= 0 AND no cosmetics AND no convenience): refuse-before-burn.
4. Derive `crystals` / `coins` from `contents.economy` (0 if absent).
5. Require `supportsInlinePackRewards === true` (name may be the existing flag **only if** it is redefined to mean inline contents, not Find). Else refuse-before-burn (retryable; do not burn).
6. INSERT snapshot: crystals, coins, pack_sku, contents jsonb.
7. Return `{ crystals, coins, packSku, contents }`.

Codes with `reward_pack_sku` NULL keep today's / 1256 crystal+coin behavior. No Find. No fs.readFile of packs.json.

### 4d. ONE APK (client)

- Redeem request advertises inline-pack capability.
- `RedeemResponse.reward` gains `contents` (PackContents JSON).
- Apply path: deserialize `contents`; call a new `PackStoreVM.ApplyPackContents(string sku, PackContents contents)` (or hydrate a throwaway PackDef from the wire **without** `PackCatalog.Find`). Grant economy through `GrantSpendablePurchased` + `AddCoins`; cosmetics / convenience through the existing seams in `ApplyPackContents`.
- If `contents` is null/empty on a packSku redeem: **fail closed**. Toast unknown / REWARD_UNAVAILABLE mapping. Do **not** `PackCatalog.Find(packSku)`.
- Double-grant guard: when `contents` is present, apply `contents` as the whole grant. Do **not** also apply top-level `reward.crystals` / `reward.coins` (those are the snapshot of the same economy). When `contents` is absent (1256 codes), keep today's crystals/coins apply.
- Recording the promo SKU as owned is OK (idempotent; `store_visible=false` keeps it off the shelf). Do not require the SKU to exist in `packs.json`.
- No RealmStorePurchase gate. No Pay. No code string in analytics (`hasPack` / packSku-without-code is fine if already the pattern).

### 4e. After APK is live (no further builds)

INSERT the two freebie packs. Then (optional, operator) point a code at them via `reward_pack_sku` / `reward_pack_sku_tier2` + `tier1_max`. FIRSTWATCH cutover from 1256 crystal columns to these SKUs is operator data, not a third binary.

## 5. Economy pins (welcome freebies)

**welcome-100** (BINDING):

- wood 100, iron 100, stone 100, crystals 100, coins 100
- cosmetics [], convenience []
- `store_visible=FALSE`

**welcome-500:**

- wood 500, iron 500, stone 500 — OK
- **500 crystals needs owner nod.** **SAFER (author this unless Sam nods 500 cr):** crystals **250** + coins **100**. Document 500 crystals + 500 coins as the nod-only override.
- cosmetics [], convenience []
- `store_visible=FALSE`

Do not silently copy 1256's 500 crystals + 500 coins into the pack. That was a crystals/coins-only weekend grant. The pack adds build resources; crystal size is a separate ruling.

Tier mapping when FIRSTWATCH (or a successor code) is pointed at packs: `reward_pack_sku = welcome-500`, `reward_pack_sku_tier2 = welcome-100`, `tier1_max = 500`. Crystal/coin columns may stay as a dead fallback (ignored while pack SKU is set) or zeroed; do not merge.

## 6. What NOT to touch / must not

- Hardcoded `reward_wood` / `reward_iron` / `reward_stone` / `reward_food` columns on `promo_codes`.
- `packs.json` as source of truth for promo grants after cutover. No "Find, then grant" promo path. No shipping a new APK to add welcome-500 to json.
- Payment bypass / `WalletService.Pay` skip / fake purchase receipt.
- Logging or tracing the promo **code** string.
- Night Market shelf rewrite (PackStore still reads json). Later WO.
- Re-opening WO-1115 or replacing WO-1256. 1256 stays the weekend MVP.
- Minting this as PROD.
- Starting a second APK after welcome INSERT.
- Special-casing `if (code === 'FIRSTWATCH')`.
- Hearth-spark / Founder's Vow as the welcome contents.

## 7. Sequencing

1. WO-1256 lands (ALTER + redeem.js two-tier crystals/coins + FIRSTWATCH row). Current Seeker APK works. **No pack SKU on FIRSTWATCH.**
2. This ticket: `packs` table + seed + redeem.js pack load + one APK (inline contents).
3. Prove NON-owner redeem of a **bound or inactive-until-ready** test pack code on the new APK (contents land: wood/iron/stone + crystals/coins). Then kill that test row (`active=FALSE`).
4. INSERT welcome-500 / welcome-100. Optionally point a live code at them. **No further APK.**

## 8. Files to edit

**Repo (implementer):**

- `api/schema.sql` (document `packs`, FKs, `reward_pack_sku_tier2`, `promo_redemptions.pack_sku` / `contents`)
- `api/promo/redeem.js` (load pack, snapshot, return contents, refuse-before-burn)
- seed script (one-shot from `packs.json`) if not pure SQL
- `Assets/_Modules/Core/Promo/PromoCodeService.cs` (flag + apply inline contents; fail closed)
- `Assets/_Modules/Wallet/PackStoreVM.cs` (contents-without-Find overload; still GrantSpendablePurchased)
- regression: promo apply must not call Find; empty contents must not burn (server) and must not Find (client)

**Repo (this mint / docs seat):**

- `WorkOrders/WORK_ORDER_1258_db_driven_promo_packs_neon_inline_contents.md` (this file)
- `CLI_LANES_WO_NUMBERS.md` (banner 1258 -> 1259, same edit)
- `BOARD.html` via `python tools/board_build.py`

**Production (implementer, after APK):**

- Neon CREATE/ALTER + seed
- INSERT welcome-500 / welcome-100
- Vercel api deploy **before** any live code points at pack SKUs

**Do not edit:** Night Market render path, `packs.json` as the welcome-pack authoring file, Pay rails, APK builders beyond this one client change.

## 9. Follow-ups (NOT this ticket)

- Night Market shelf-from-DB (`store_visible`, prices). Separate WO.
- Command Center authoring of packs / `reward_pack_sku_tier2` (WO-1244 leftover).
- DROP `tier1_crystals` / `tier1_coins` after every live two-tier code uses pack SKUs.
- Unique-per-creator codes.

## 10. Implementation evidence (2026-08-28)

- **RCA:** the prior client capability meant only “this APK can resolve a SKU from its baked
  catalog.” The server inserted the redemption before the client called `PackCatalog.Find`, so
  an absent/stale APK SKU burned a one-shot code without a grant and kept `packs.json` as the
  real authority.
- `api/promo/redeem.js` now requires `supportsInlinePackRewards`, selects only an active,
  non-empty Neon `packs` row, and inserts `pack_sku` plus the exact `contents` snapshot. Tiered
  selection, counter increment, pack validation, and redemption insert remain one SQL statement.
- `PromoCodeService` hydrates the returned JSON into the Wallet assembly's `PackContents` type
  and calls `PackStoreVM.ApplyPackContents(string, PackContents)`. It never calls
  `PackCatalog.Find`; missing inline contents fail closed; top-level crystal/coin audit mirrors
  are not double-granted.
- Added staged migrations `20260828_0005_db_promo_packs.sql` then one-shot seed generation via
  `tools/seed-promo-packs.mjs`, followed by FK migration `20260828_0006_db_promo_pack_fks.sql`.
  No production migration or promo-row mutation was performed in this implementation pass.
- Targeted regression: `node --test test/db-promo-packs.test.js
  test/first-watch-promo-surface.test.js` — **PASS 6/6**. `node --check api/promo/redeem.js` —
  **PASS**. Full Unity regression is deliberately serialized by the root builder and remains the
  final gate before this ticket can move to FIXED.
