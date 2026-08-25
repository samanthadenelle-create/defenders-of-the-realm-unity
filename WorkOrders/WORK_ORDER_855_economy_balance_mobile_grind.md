> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: a8bbd368; BuildEconomyRegression.cs (455 lines) plus 5 canonical JSONs retuned.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 855 — Economy balance pass: costs, times, gather, spam softcap, difficulty (data-first)

**Status:** DONE  
**Minted:** 2026-08-03 (CLI / Grok — owner: mobile-game grind + challenge; **tweak not rewrite**)  
**Silo:** Economy / Data / light BuildMode cost hook  
**Roles:** CLI (or Claude CLI) implements **numbers + thin cost multiplier only**; no system rewrites  
**Program adjacency:** WO-817 (queue look) · WO-821 (perk timers) · WO-808 gear curves · difficulty-profile.json  

---

## 0. North star (binding)

| Goal | Meaning |
|------|---------|
| **Mobile grind** | Player always has a next sink (build / train / research / reforge / upgrade) that costs **resources + wall-clock time** |
| **Challenging depth** | Late game costs/times and enemy pressure outpace “spam free towers + free army” |
| **Generic** | Prefer **JSON / SO / constant tables** already in tree; one **reusable softcap multiplier** for placeables |
| **No rewrites** | Do **not** replace queue engine, BarracksService flow, ResourceCollector behavior, GearProgression, or wave manager — only **feeds** and **one cost hook** |

### Explicit non-goals
- New currencies  
- New troop/tower types  
- Rebalancing combat AI / ability kits  
- Full terrain economy redesign  
- Making timers zero for “fun” (instant only via existing skip APIs)  
- Hard-coding Avalon / wrong wallet  

---

## 1. Implementation law (for Claude — read first)

```
1. Prefer EDITING existing dual-copy JSON and existing code defaults.
2. If a number is hard-coded in C# with no data file, extract ONLY that constant
   to a small economy-balance.json OR leave a clearly named const block —
   do not invent parallel systems.
3. Dual-copy rule: Resources/Data/Canonical AND StreamingAssets/Data/Canonical
   stay byte-identical for every JSON you touch.
4. After number edits: COMPILE_GATE_OK + REGRESSION_OK + new [economy-balance] oracle.
5. Brace-check every .cs. No .unity hand-edits. No mount/bash .cs writes.
6. If logic already multiplies by a curve, CHANGE THE CURVE INPUTS only.
```

### Allowed code surface (maximum)

| Touch | Why |
|-------|-----|
| JSON dual-copies listed in §3 | Primary work |
| `BuildTimerConfig` SO defaults and/or Resources asset | Time curve |
| `BarracksProgression` cost/seconds **multipliers** only (named constants or data) | Troop L costs already derived |
| `ResourceBuildingProgression` yield/interval/cost **table numbers only** | Already the balance table |
| `BuildModeController.CostFor` / `EffectiveCostFor` — **one optional softcap multiply** | Tower spam (generic) |
| `DataRegression` oracle | Guard rails |
| Optional new `economy-balance.json` | Softcap + global multipliers only — **if** you refuse more C# knobs |

### Forbidden code surface
- New JobKind / channel  
- New SaveSchema fields unless softcap needs a count (prefer **live world count**, no save)  
- Rewriting `ObsidianQueueEngine`, `TroopDeployer`, `Tower` combat loop  
- Changing `ff.barracks` / raid gate semantics  

---

## 2. Current state snapshot (audit baseline — 2026-08)

Use as **before** numbers; retune toward §4 targets. Re-measure after if data drifts.

### 2.1 Towers (`structures-catalog.json` · `behaviorId: DefenseTower`)

| id | L1 cost (W/I/C) | L1 dmg · fireRate · range | ≈DPS | upgradeCost table |
|----|-----------------|---------------------------|------|-------------------|
| tower_ground_archer | 70/40/0 | 6 · 2.5 · 14 | 15 | **missing** → fallback scale |
| tower_wall_wizard (Ballista) | 40/20/60 | 20 · 0.5 · 22 | 10 | missing |
| tower_siege_tower (AA) | 80/50/0 | 45 · 2.0 · 50 | **90** | missing |
| tower_catapult | 100/80/0 | 24 · 0.8 · 28 | 19 | missing |
| tower_arcane_spire | 30/30/80 | (repo) | — | missing |

**Issues:** Sky Ballista **cost/DPS ~1.7** vs Archer ~8.7 (spam-AA is free power). Upgrade uses `UpgradeCostFor` fallback (`buildCost × fromLevel`) when `repo.upgradeCost` null — **OK to keep formula; author explicit upgradeCost rows for control**.

`towers.json` zone/level stats (12/22/40 dmg curve) may **diverge** from catalog `repo.damage` — audit which path live DefenseTower reads; **do not merge systems**, only align **numbers** on the live path.

### 2.2 Troops (`troops.json` train cost + time)

| Troop | slots | train W/I/F | buildSeconds | ≈DPS | note |
|-------|-------|-------------|--------------|------|------|
| Footman | 1 | 40/10/5 | 30 | 12 | day-one |
| Archer | 1 | 30/20/5 | 45 | 24 | cheap DPS |
| Spearman | 1 | 50/25/10 | 50 | 15 | |
| Shieldguard | 2 | 60/40/15 | 70 | 8 | tank |
| Outrider | 2 | 80/50/20 | 90 | 20 | |
| Battlemage | 2 | 40/80/25 | 100 | 23 | |
| Echo Legionnaire | 3 | 100/100/40 | 150 | 28 | elite |

**Troop L upgrade** (`BarracksProgression`): cost = train × targetLevel; seconds = max(15, trainSec × level × 2). **Keep formula; retune multipliers only.**

### 2.3 Barracks levels (`barracks.json`)

| L | unlock | cost W/F/I/C | buildTimeSeconds |
|---|--------|--------------|------------------|
| 1 | Footman+Archer | 0 | 0 |
| 2 | Spearman | 150/40/60/0 | **120** (2m) |
| 3 | Shieldguard | 320/90/160/0 | **300** (5m) |
| 4 | Outrider | 600/180/320/20 | **720** (12m) |
| 5 | Battlemage | 1000/320/560/60 | **1500** (25m) |
| 6 | Echo Leg. | 1800/560/1000/140 | **3600** (1h) |

Shape is already mobile-ish; scale if sinks feel short vs gather.

### 2.4 Structure build/upgrade **time** (`BuildTimerConfig`)

Code defaults (if no SO asset):

- `baseBuildSeconds = 15`  
- `tierGrowth = 3.0` → tier0=15s, tier1=45s, tier2=135s, tier3=405s…  
- `upgradeMultiplier = 1.25`  
- `freeBuildSlots = 2`  
- max 48h  

**Gap:** duration is **tier-index only**, not structure-id weight — generic fix = optional `repo.buildSeconds` / `repo.upgradeSeconds[]` **if already supported**; else **only retune BuildTimerConfig** (no per-building code unless a field already exists on RepoProps — **check before adding fields**).

### 2.5 Gear (`weapons.json` + `gear-levels.json`)

- Rarity buy ladder exists (~common floor → legendary sink).  
- Improve: common L1→L5 wood 60…600 / iron 30…300; legendary higher. Soft placeholders — retune to §4.5.

### 2.6 Gathering

| Source | Knobs today |
|--------|-------------|
| Farm / Lumbermill / Forge progression | `ResourceBuildingProgression`: baseYield 20/15/8, yieldStep 12/10/6, intervals `{8,6.8,5.6,4.4,3.2}`s, costStep ~1.9–2.0 |
| Echo passive | `echoes-balance.json` + `EchoService` BaseRatePerHour |
| Offline | `OfflineHarvestService.OfflineCapHours = 10` |
| Mine nodes | `MineNode` YieldPerExtract=5, ExtractCooldown=8s, TotalExtracts=6 (inspector defaults) |
| Collectors place cost | catalog cheap (~40–60 wood) |

**Risk:** yields high + tower/troop costs low ⇒ **no grind**. Soften income **or** raise sinks (prefer **both slightly**).

### 2.7 Difficulty

- `difficulty-profile.json`: adaptive mult 0.75–1.45 (+spike 1.60), death targets, etc.  
- `waves.json`: 20 waves → dragon apex.  
- **Do not rewrite** adaptive system; optional **raise maxMultiplier / enemy HP scales in data only** if late sinks feel free.

### 2.8 Tower spam

**No** global “Nth tower costs more” today. Softcap is the main **new thin hook** (§5).

---

## 3. File checklist (edit only these unless oracle needs more)

| File (dual-copy when JSON) | What to tweak |
|----------------------------|---------------|
| `structures-catalog.json` | Tower `repo.cost`, `repo.damage`/`fireRate`/`range` if live, **author `repo.upgradeCost[]`**, optional seconds if field exists |
| `troops.json` | costWood/Iron/Food, buildSeconds, maxHp, attackDamage, attackCooldown (stats stay role-identity) |
| `barracks.json` | level costs + buildTimeSeconds |
| `troop-upgrades.json` | **Only if** strength/reach curves are OP; prefer leave |
| `gear-levels.json` | improve costWood/costIron ladders |
| `weapons.json` / `armor.json` | buy* prices if shop trivializes sinks |
| `echoes-balance.json` | rates / match bonuses if harvest too fat |
| `difficulty-profile.json` | maxMultiplier / countScale soft raise late |
| `building-tiers.json` | perk goldCost / production mults if instant gold too strong |
| `Resources/Economy/BuildTimerConfig` (or SO defaults in `BuildTimerConfig.cs`) | baseBuildSeconds, tierGrowth, upgradeMultiplier, freeBuildSlots |
| `ResourceBuildingProgression.cs` | **numeric tables only** (yield, interval, baseCost, costStep) |
| `BarracksProgression.cs` | **only** TroopUpgradeCost / Seconds scale factors |
| `BuildModeController.cs` | softcap multiply in CostFor path (§5) |
| Optional `economy-balance.json` | softcap curve + global gather mult + troop upgrade mults |

Always both:  
`Assets/Resources/Data/Canonical/...`  
`Assets/StreamingAssets/Data/Canonical/...`

---

## 4. Target design (generic mobile curves — **retune to feel**, formulas fixed)

Use **relative** targets so Claude can scale without inventing absolute “fun.”  
Define resource basket:

```
basket = wood + 1.5*iron + 1.0*food + 2.0*crystals
```

### 4.1 Tower place cost (L1)

| Role | Target basket vs Archer Tower L1 | Notes |
|------|----------------------------------|-------|
| Basic ground (Archer) | **1.0× baseline** (set baseline after retune) | Cheapest ground |
| Splash / catapult | **1.4–1.8×** | |
| Crystal mage / ballista | **1.5–2.0×** + crystal weight | |
| Anti-air | **2.0–2.5×** OR keep damage **much** lower | Fix cost/DPS outlier |
| Super / unique | **≥3×** + singleton if any |

**DPS economy:** after retune, rough `basket / DPS` should sit in a **band** (e.g. 8–20 for ground; AA not below ground). Oracle can assert min basket/DPS for AA ≥ basic ground.

### 4.2 Tower upgrade cost (L1→L2, L2→L3)

Prefer **authored** `repo.upgradeCost`:

```
L1→L2 basket ≈ 1.0–1.2 × place basket
L2→L3 basket ≈ 2.0–2.5 × place basket
```

If using fallback only: leave formula; raise place cost so scale tracks.

**Stats per level:** if live path multiplies damage/range by level, do not double-dip — either catalog L1 stats + level mult **or** explicit per-tier stats (whichever already exists). **One source.**

### 4.3 Troop train cost & time

| Tier (unlockBarracksTier) | Train time band | Basket vs Footman |
|---------------------------|-----------------|-------------------|
| 1 | 30–90s | 1.0–1.3× |
| 2–3 | 1–4 min | 1.5–2.5× |
| 4–5 | 3–8 min | 2.5–4× |
| 6 | 8–20 min | 4–6× |

Slots: cost should rise with slots (Shieldguard 2-slot not cheaper per slot than Footman).

**Stat identity:** keep roles (tank HP high, mage glass). Change costs/times first; stats only if cost/DPS oracle fails badly.

### 4.4 Troop upgrade (Research) — keep derived formula

Current:

```
cost(L) = trainCost * targetLevel
seconds(L) = max(15, trainSeconds * targetLevel * 2)
```

Retune via named factors (constants or economy-balance.json):

```
cost(L) = trainCost * (a + b * (L-1))     // default a=1,b=1 → same as now if a=0,b=L...
// SIMPLER: costMult = k * L  with k default 1.0; raise k to 1.25–1.5 for grind
seconds(L) = max(minSec, trainSeconds * L * timeK)  // timeK default 2; try 2.5–3 late
```

**Do not** per-troop special case unless data row already exists.

### 4.5 Barracks building upgrade times (already good shape)

Mobile reference bands (CoC-ish compression for single-player):

| Step | Time band |
|------|-----------|
| L1→L2 | 2–10 min |
| L2→L3 | 10–30 min |
| L3→L4 | 30–90 min |
| L4→L5 | 1–4 h |
| L5→L6 | 4–12 h |

Current values are inside/near bands — **nudge** if gather is buffed/nerfed.

### 4.6 Structure build/upgrade wall-clock (`BuildTimerConfig`)

Suggested default retune (feel; owner can push longer):

| Knob | Current default | Suggested start |
|------|-----------------|-----------------|
| baseBuildSeconds | 15 | **30–60** (still snappy first place) |
| tierGrowth | 3.0 | **2.2–2.8** if times explode too fast, **or 3.0–3.5** if endgame too short |
| upgradeMultiplier | 1.25 | **1.3–1.5** |
| freeBuildSlots | 2 | **keep 2** (scarcity) |

First freebie placements stay free (existing FreeBuildAvailable) — **do not break freebie**.

### 4.7 Gear improve

- Common full path L1→L5 total basket should be **≥ several early towers** or **≥ 1 barracks step**.  
- Legendary improve **much** higher; never cheaper than rare at same level.  
- Keep `statMult[0]=1.0`; do not raise top mult into broken combat without difficulty pass.

### 4.8 Gathering / income (grind side)

**Target feel:** at mid-game, **one** mid tower place **or** one mid troop train should cost on the order of **several minutes of active collectors+echoes**, not seconds.

Generic knobs (pick combination; document in RESULT):

| Lever | Direction for more grind |
|-------|---------------------------|
| YieldPerTick / baseYield | ↓ 10–30% |
| HarvestIntervalByLevel | ↑ (slower ticks) |
| costStep / baseCost for farm upgrades | ↑ (harder to scale income) |
| echoes-balance rates / match | slight ↓ if passive dominates |
| OfflineCapHours | keep 10 or drop to 6–8 if offline prints free builds |
| MineNode yield/cooldown | slight ↓ yield or ↑ cooldown |

**Production mult perks** in `building-tiers.json`: cap stacked mults so total wood production does not exceed ~**+50–80%** over base from perks alone without heavy gold sinks (raise goldCost if needed). Perk **timers** stay WO-821.

Echo crystals remain **slowest** faucet (existing monetization guard in echoes-balance comments) — preserve that ordering.

### 4.9 Difficulty (challenge side)

If after sink/income retune waves feel free:

- `difficulty-profile.json`: nudge `maxMultiplier` toward **1.5–1.7**, `countScale` slight ↑  
- Do **not** rewrite scoring  

If waves feel brutal after nerfing towers: reverse slightly — **one** pass only.

### 4.10 Buffs / bonuses (apply via existing modifiers only)

| Bonus type | Rule |
|------------|------|
| Building production mult | Already in building-tiers modifiers — retune numbers |
| Hero talent buildTime haste | Already clamps — do not remove; ensure economy still works if haste 0 |
| Troop L strength/reach | Existing curves — only shrink if raids trivial |
| Gear level | Existing ApplyStats choke — only retune gear-levels.json |
| Echo harvest mult | echoes-balance.json only |

**No new buff system.**

---

## 5. Tower (and optional defense) spam softcap — **generic thin hook**

### Intent
After **N** live towers, each **additional** tower place costs more. Stops pure wood→wall of towers without deleting the build verb.

### Generic design (structure-type agnostic)

```
effectiveCost = baseCost * SoftcapMultiplier(countOfSameClass)
```

Suggested class = **DefenseTower** (count all live DefenseTower behaviors / catalog type Tower).  
Optional later: same helper for walls — **out of scope unless free**.

### Suggested curve (data)

```json
{
  "towerSoftcap": {
    "freeCount": 4,
    "startAtCount": 5,
    "multPerExtra": 0.15,
    "maxMult": 3.0,
    "mode": "linear"
  }
}
```

Meaning:

```
if count < startAtCount: mult = 1
else mult = min(maxMult, 1 + (count - startAtCount + 1) * multPerExtra)
// place when count already 4 → 5th uses startAtCount=5 → mult = 1+0.15 = 1.15
```

Tune freeCount **4–6** for mobile base size.

### Code law
- Implement **one** helper e.g. `EconomySoftcap.Multiplier(classId, liveCount)`  
- Call from **single** place: `BuildModeController.CostFor` / `EffectiveCostFor` (or only Effective) so UI + commit agree  
- Count **live** towers (PlacedStructure / Building with DefenseTower) — no save field  
- **Does not** apply to upgrades of existing towers (only **new place**)  
- Freebie first-build still free (freebie runs before or zeros cost — keep existing order)  
- FlowTrace.Once when mult > 1  

### UI (minimal)
- If mult > 1, palette or toast can show “+X% (many towers)” — **optional**; honest cost on arm is enough  

---

## 6. Phased work plan (Claude execute in order)

### Phase 0 — Measure (no balance commit yet)
1. Table live tower path (catalog repo stats vs towers.json).  
2. Confirm CostFor / UpgradeCostFor / StartBuild duration inputs.  
3. Write `ECONOMY_BASELINE.md` snippet in RESULT with before baskets.

### Phase 1 — Softcap hook (code, small)
1. Add softcap data + `EconomySoftcap` (or inline static).  
2. Wire place cost only.  
3. Unit/oracle: 0–3 towers mult=1; past freeCount mult rises; cap at maxMult.

### Phase 2 — Tower costs/stats JSON
1. Fix AA / Ballista outliers (cost up and/or DPS down).  
2. Author `upgradeCost` for each tower maxLevel 3.  
3. Dual-copy + regression.

### Phase 3 — Troop + barracks JSON + upgrade mults
1. Nudge train costs/times to §4.3 bands.  
2. Barracks times/costs §4.5.  
3. Troop upgrade k / timeK if needed.

### Phase 4 — Timers SO
1. Retune `BuildTimerConfig` defaults/asset.  
2. Verify freebie + first places still feel OK.

### Phase 5 — Gather grind
1. Nudge ResourceBuildingProgression numbers and/or echoes-balance.  
2. Optional mine defaults if data-driven; else leave inspector.  
3. Ensure mid tower > ~few minutes income (document estimate in RESULT).

### Phase 6 — Gear + difficulty light pass
1. gear-levels / weapon buy if shop invalidates sinks.  
2. difficulty-profile soft raise only if needed.

### Phase 7 — Oracle + RESULT
1. DataRegression `[economy-balance]`:  
   - dual-copy parity for touched JSON  
   - AA basket/DPS ≥ ground archer basket/DPS  
   - softcap mult monotonic  
   - troop train buildSeconds > 0  
   - barracks L6 time ≥ L2 time  
2. `WORK_ORDER_855_economy_balance_mobile_grind.RESULT.md` with before/after tables.

---

## 7. Acceptance criteria

- [ ] COMPILE_GATE_OK + REGRESSION_OK including new economy-balance checks  
- [ ] No new queue/raid/train **logic** beyond softcap multiply + constant mults  
- [ ] Dual-copy parity on all edited JSON  
- [ ] Tower spam: 8th+ tower clearly more expensive than 1st (same type family)  
- [ ] Sky Ballista no longer cheapest damage-per-basket  
- [ ] Train + build + upgrade still use existing channels/timers (visible remaining time)  
- [ ] Gather rates: documented mid-game “minutes of income per mid sink” in RESULT  
- [ ] Freebies / founding free places still work  
- [ ] Elarion copy only; ResourceLedger remains the wallet  
- [ ] Owner felt: “I need to wait/gather between towers and army” without softlock  

---

## 8. Owner tuning dials (document defaults in RESULT)

| Dial | Softer grind | Harder grind |
|------|--------------|--------------|
| Softcap freeCount | 6–8 | 3–4 |
| Softcap multPerExtra | 0.08 | 0.20+ |
| baseBuildSeconds | 15–30 | 60–120 |
| Gather yield | +10% | −20–30% |
| Troop timeK | 2.0 | 3.0 |
| difficulty maxMultiplier | 1.45 | 1.65 |

---

## 9. Paste for Claude CLI

```text
Implement WORK_ORDER_855_economy_balance_mobile_grind.md as a DATA-FIRST pass.
Law: tweak numbers and at most one softcap cost multiplier — do NOT rewrite
queue, barracks, collectors, gear engine, or waves systems.

Order: Phase 0 measure → 1 softcap hook → 2 tower JSON → 3 troop/barracks →
4 BuildTimerConfig → 5 gather/echo numbers → 6 gear/difficulty light → 7 oracle+RESULT.

Dual-copy every JSON. Keep freebies. Fix tower cost/DPS outliers (esp. AA).
Mobile-style times: longer endgame, snappy early. Gather should force wait between sinks.
COMPILE_GATE_OK + REGRESSION_OK. Brace-check every .cs.
```

---

## 10. One-line truth

**Make the existing CoC-shaped economy actually bite:** raise/align **costs**, **timers**, and **gather**, add a **generic tower softcap**, and leave architecture alone.
