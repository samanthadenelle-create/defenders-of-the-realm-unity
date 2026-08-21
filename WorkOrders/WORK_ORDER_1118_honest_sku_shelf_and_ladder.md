# WO-1118 — Honest SKU shelf: hide vapor, keep impulse, rewrite the $2/$5 ladder

**Status:** READY TO IMPLEMENT after owner R1–R3 on WO-1117 §4  
**Minted:** 2026-08-17 (CLI seat) — program WO-1117  
**Lane:** Monetization / data (`packs.json` dual-copy) + PackStore filter  
**Depends on:** WO-1117 rulings R1–R3  
**Blocks:** honest Buy-ON (WO-1121), shortfall trust (WO-1037)

---

## 0. One-line truth

**Stop selling what we cannot deliver.**  
`packs.json` v5 has 25 SKUs. Only the **12 impulse** packs are fully honest today (single resource → `GrantSpendable`). Ladder + bundles advertise cosmetics that do not render and convenience kinds with **zero redeemer**. Selling those after Buy-ON is a refund and reputation problem on a live store.

---

## 1. What to KEEP on the live shelf (v1 early-access)

### 1.1 Impulse family (12) — primary

| Resource | Small $1.99 / 25 SKR | Medium $2.99 / 36 SKR | Large $4.99 / 60 SKR |
|---|---|---|---|
| wood | 1000 | 3500 | 8000 |
| iron | 400 | 1200 | 3000 |
| food | 1000 | 3500 | 8000 | ⚠ optional hide from shortfall (R3) |
| crystals | 250 | 700 | 1600 | only after WO-1119 sink, or cut amounts |

**Already in data.** Work is **shelf filter + store UI honesty**, not inventing SKUs.

### 1.2 Value ladder (3) — REWRITE contents (SME §2)

Replace the visible ladder with **three** packs at **two** prices. Differ on **axis**, not quantity.

| SKU (reuse ids or retag) | USD / SKR | Contents (all deliverable) | Role |
|---|---|---|---|
| **hearth-spark** | $1.99 / 25 | 150 crystals; 1500 wood; 800 iron; 500 food; **1× harvest_boost 2.0× 4h** (after 1119) | Impulse basket |
| **starters-hand** | $4.99 / 60 | 400 crystals; 4000 wood; 2000 iron; 1500 food; **3× harvest_boost 4h**; **OR** 5× instant-build **only if redeemer ships same PR** | Resource-forward |
| **keepers-satchel** (new or retag lanternlight) | $4.99 / 60 | 900 crystals; **1× harvest_boost 24h** (must ship with auto-collect or cap honesty); optional builder-slot rental if that system exists | Time-forward |

**Until harvest boost lands (1119):** ship wood/iron/crystal lines only; **omit** boost lines rather than list dead kinds.

### 1.3 HIDE from default shelf (do not delete JSON yet)

| Group | SKUs | Why |
|---|---|---|
| Over-cap | folks-thanks, patron-of-elarion, founders-vow, echo-patron-pack, builders-cache | >$5 early access |
| Vapor-dominant | frostfall, embergrove, bloomtide, hero-wardrobe, realm-defender | cosmetics + dominated pricing |
| Old ladder duplicates | lanternlight (if keepers-satchel replaces it) | collapse |

Implementation: prefer `"shelf": "live" | "hidden" | "legacy"` on `PackDef` (additive JSON) + store filters `shelf != hidden`.  
Fallback: hard allowlist in PackStore if schema change is deferred — **prefer data flag**.

---

## 2. Code / data scope

1. **Dual-copy** `packs.json` (Resources + StreamingAssets) — always identical.  
2. `PackCatalog.cs` — parse `shelf` (default `"live"` for impulse; mark others `"hidden"`).  
3. `PackStore` / VM — only list live; detail page must not show convenience that cannot redeem.  
4. **Regression:**  
   - every `shelf:live` pack: every economy key has a grant path; every convenience kind is either allowlisted-as-redeemable OR count=0;  
   - no live pack with `usd > 5.00` while early-access flag is on;  
   - impulse packs remain exactly one economy key (existing oracle).  
5. **Do NOT** enable `skr_store.json` acquisition packs (arbitrage). Delete or quarantine acquisitionPacks in a comment + regression refuse-load.

---

## 3. Pricing rules (binding)

- Early-access **max $4.99** (owner memory; SME $5).  
- SKR amounts: **derive from one peg** (e.g. `skrPerUsd ≈ 12.5` → $1.99→25, $2.99→36, $4.99→60). Document peg in packs `_schemaNotes`.  
- No second SKR table.  
- 0% dApp fee: keep impulse prices low; do not "feel premium" at $49.99 until R1 graduation.

---

## 4. Acceptance

1. Default Realm Store shows ≤ **15** SKUs (12 impulse + ≤3 ladder), all ≤$4.99.  
2. Opening any live pack detail: every listed line is grantable today (screenshot + regression).  
3. Hidden SKUs still load for owned entitlement / admin but not Buy.  
4. Dual-copy match.  
5. `COMPILE_GATE_OK` + pack catalog regression green.  

## 5. Not in scope

- Payment rails (1121), harvest engine (1119), ads (1120), season pass (1122), cosmetic art.  

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `PackCatalog.cs:103; packs.json v5 line 36` — $2/$5 ladder rewrite not done. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
