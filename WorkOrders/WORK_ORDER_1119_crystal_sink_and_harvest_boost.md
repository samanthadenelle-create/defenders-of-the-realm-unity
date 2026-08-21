# WO-1119 — Crystal sink + 2× harvest boost (Version B only)

**Status:** READY TO IMPLEMENT after WO-1117 R7 confirm (stack rule)  
**Minted:** 2026-08-17 (CLI seat) — program WO-1117  
**Lane:** Economy / Echoes / Pack convenience  
**Depends on:** WO-1117 R7; pairs with WO-1118 (boost lines in packs only after this lands)  
**Sources:** `docs/MONETIZATION_SME_REVIEW_2026-08-06.md` §3; `WORK_ORDER_economy_store_packs.md` §2c

---

## 0. One-line truth

**Crystals only sell if something worth buying costs crystals every week.**  
Today: ~154 crystals sink the whole catalog; fresh save starts with **250**. Impulse crystal packs are pointless until a **recurring** sink exists. The highest-value sink is a **2× harvest rate boost for time** (not more total offline bank).

---

## 1. Covenant — Version B only

| Version | Hook | Effect | Verdict |
|---|---|---|---|
| **A** | Multiply inside `AggregateHarvestMultiplier` | Rate **and** silo cap ×2 → offline player gets **2× resources** | **FORBIDDEN** — sells power/amount |
| **B** | Multiply `RatePerSecond` only; **cap untouched** | Silo fills in half the time; total banked same | **SHIP** — pure time |

Rules (binding):
- Cap effective mult at **2.0×**; stack = **extend duration only** (never multiply boosts).
- **Never** boost crystal harvest (`appliesTo` = wood | iron | food | all-harvestables-except-crystals).
- Refuse to start if bank full (`TownBankCapacity.HasHeadroom`) — plain toast, not silent burn.
- Partial-window offline claim must integrate **overlap** of boost window with claim window (SME §3).

**Pair with auto-collect while boost active** (or document that 24h boost is worthless to AFK players). Prefer auto-collect for 24h product honesty.

---

## 2. Product table

| Duration | Mult | Source | Price |
|---|---:|---|---|
| 30–60 min | 2.0× | Rewarded ad (`place.harvest.doubler`) | free, capped (ads WO-1120) |
| **4 hours** | 2.0× | **Crystal purchase** (~120 crystals, tunable) | **recurring sink** |
| 24 hours | 2.0× | Pack content (ladder after 1118) | $1.99 / $4.99 tiers |

4h matches `SiloCapHours = 4f` — longer than silo without auto-collect oversells.

---

## 3. Implementation scope

### 3a. Persistence (save — additive, inert defaults)
- `HarvestBoostEndsAtMs` (double unix-ms, 0 = none)
- `HarvestBoostMult` (float, default 1)
- Same clock family as `LastHarvestClaimMs`
- No schema version bump if read-migrate defaults are inert (match project habit; bump only if required)

### 3b. Engine
- Trailing factor on `EchoService.RatePerSecond` — **not** inside `AggregateHarvestMultiplier`
- Mirror on resource building yield path if separate (`CurrentEffectiveYield`) — multiply yield, not interval
- Offline claim overlap math
- FlowTrace: start / refuse-full / expire / claim-overlap

### 3c. Crystal shop surface
- One UI entry: "2× Harvest (4h) — N crystals" from Manage or Realm Store consumables row
- Spend crystals via existing spend path; grant boost on success only

### 3d. Pack convenience
- Extend `ConvenienceItemDef` with optional `BoostSpec` (economy_store_packs §2c)
- Kind `harvest_boost` already covenant-allowlisted in `PackCatalog`
- `ApplyPackContents` must **activate or stack-extend** the buff, not log-and-drop

### 3e. Timer / Finish-Now retune (same WO or immediate follow-up)
- Raise real wait bands so two ad skips (10 min each) cannot finish every job
- Retune `instantFinishCrystalsPerMinute` / floors so starter 250 cannot clear the whole catalog
- Goal: crystals feel scarce by day 2 of active play

---

## 4. Acceptance

1. With boost active, harvest **rate** doubles; silo **cap** unchanged (headless or instrumented proof).  
2. Offline 8h with 4h boost ≠ 2× full silo of resources (cap proof).  
3. Crystal purchase starts 4h boost; second purchase extends, does not 4×.  
4. Bank full → refuse + message; no `BANK FULL ... LOST` from boost income.  
5. Pack line with harvest_boost grants real buff.  
6. Regression suite no worse than baseline; FlowTrace lines present.  
7. `COMPILE_GATE_OK`.

## 5. Not in scope

- Ads SDK (1120), payment rails (1121), cosmetic pass (1122), combat buffs.  

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no HarvestBoost/RateMultiplier` — boost engine unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
