# WO-1122 — Season pass (Keeper's Almanac) + revenue KPI ops

**Status:** SPEC — implement pass only after R6 (WO-1117); KPI phase can start earlier  
**Minted:** 2026-08-17 (CLI seat) — program WO-1117  
**Lane:** Monetization / LiveOps / Analytics  
**Depends on:** WO-1117 R6; cosmetic render pipeline OR pure-currency free track ruling  
**Sources:** `docs/monetization-v2-spec.md` §6; `docs/biz/ANALYTICS_KPI_PLAN.md`; WO-1116 admin dashboard

---

## 0. One-line truth

**Impulse packs make day-1 money; the season pass makes month-2 LTV.**  
Do not ship a paid pass that grants cosmetics which do not render — that is Founder's Vow all over again. Ship **KPI measurement first**; ship the **pass when the track is honest**.

---

## 1. Phase A — Revenue KPI (READY independent of pass)

Use existing `EventTracker` (do not greenfield). Minimum events:

| Event | Properties | Why |
|---|---|---|
| `store_open` | source (coppin / realm_storefront / shortfall / hud) | Funnel top |
| `store_pack_view` | sku, usd, shelf | Interest |
| `store_pay_start` | sku, rail (skr/sol/usdc) | Intent |
| `store_pay_complete` | sku, rail, paymentId | Revenue |
| `store_pay_fail` | sku, reason | Friction |
| `store_grant_ok` / `store_grant_fail` | sku | Trust |
| `shortfall_offer_shown` | resource, need, sku | Impulse thesis |
| `ad_offer_shown` / `ad_complete` / `ad_no_fill` | placementId | Free path |
| `boost_start` / `boost_refuse` | source, duration | Sink health |
| `finish_now` | costCrystals, jobKind | Crystal sink |

**Weekly ritual (owner + CLI):** top SKUs by revenue, conversion store_open→pay_complete, ad fill rate, crystal spend vs grant.  
Surface in WO-1116 admin when Phase 2 write paths allow; until then Neon/query or event export.

### Acceptance A
1. Events fire on device / headless smoke for store + shortfall paths.  
2. One weekly report template in `docs/` or admin page.  
3. No PII beyond existing playerId rules.

---

## 2. Phase B — Keeper's Almanac (SPEC until R6)

Per monetization-v2-spec §6 (generous, no FOMO):

| Rule | Value |
|---|---|
| Price | ~$9.99 / 120 SKR (after early-access graduation — **not** under $5 cap era unless owner overrides) |
| Unlock | **Permanent** — no season expiry FOMO |
| Track | **Cosmetic-only** for paid tiers (covenant) |
| Free track | 10 tiers parallel |
| Cadence | 90-day themes; player completes at own pace |
| Premium currency on free track | No crystals as pass grind reward that undercuts packs |

### Preconditions before coding pass UI
1. **At least one cosmetic category renders** (pet skin / building palette / banner / flair — not hero full outfit until rig supports it), **OR**  
2. Owner R6 chooses a **currency-only free track** and **delays paid pass**.

### Not day-1
- Pass is **retention**, not the first Buy-ON product. Sequence: impulse → crystals/boost → ads free path → **then** pass.

### Acceptance B (when unblocked)
1. Pass purchase grants track unlock; free track playable without pay.  
2. Every paid tier item equippable/visible.  
3. No FOMO countdown UI.  
4. KPI events for pass_view / pass_buy / tier_claim.

---

## 3. Explicitly not in scope

- Rewriting packs ladder (1118), payment rails (1121), LevelPlay (1120).  
- Loot boxes, battle-pass FOMO timers, combat pass rewards.  
