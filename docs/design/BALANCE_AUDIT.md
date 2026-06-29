# Balance Audit — Combat, Progression & Economy (V1)

> READ-ONLY design/balance audit. Sourced from the live data files and combat code,
> not from comments (CLAUDE.md §12 / MASTER_CATALOG discipline). Every number below is
> cited to its file. Recommendations are **proposals for the owner**, not changes made.
> Author: balance-audit agent, 2026-06-28.

## Scope & sources

| System | Files read |
|---|---|
| Hero offense | `Assets/_Modules/Village/Enemies/PlayerAttackController.cs` (`_baseDamage=30`, `_attackCooldown=0.6`), `abilities.json`, `weapons.json` (damageMult) |
| Hero defense | `Assets/_Modules/Village/Hero/HeroHealth.cs` (`_maxHp=100`, flat-pull `DamagePerEnemy=6`), `armor.json` (defense fractions), `hero-talents.json` (DR/block/maxHP) |
| Hero scaling | `HeroProgression.cs` (XP curve, +6%/level dmg cap 3x), `WisdomCurrencyService.cs` |
| Enemies | `enemies.json` (wave/orc stat blocks), `BattleArena.BuildEncounterDef` (arena family + threat +8%/tier), `EnemyBrain.cs` (`damage=8`, attack path) |
| Arena/stars | `BattleArena.cs` (reward grant), `BattleStarRating.cs` (90s/120s thresholds, 1.0/1.25/1.5x) |
| Economy | `offline-storage.json` (echo rate, silo caps, storage ladder), `packs.json` (pricing), `OfflineHarvestService.cs` (node/settlement/pet accrual) |

---

## 1. Combat tuning

### 1.1 Hero damage chain
Outgoing melee = `30 (base) × weaponMult × levelMult × talentMult × timingMult`.
- **weaponMult:** 1.0 (common) → 1.25 → 1.6 → 2.1 (epic) → **2.4 (legendary)** — `weapons.json`.
- **levelMult:** `1 + 0.06·(L−1)`, capped **3.0×** (`HeroProgression.DamageMultiplier`).
- A maxed Knight: `30 × 2.4 × 3.0 = 216` per swing **before** talents/timing, at a 0.6 s cadence ≈ **360 DPS auto-attack**, plus a 220-dmg ult on a 40 s cd.

### 1.2 Enemy HP/damage (arena family, `BuildEncounterDef`, threat 1)
| Enemy | HP | Authored contact dmg | Speed |
|---|---|---|---|
| Orc Raider (grunt) | 100 | 16 | 3.0 |
| Orc Warleader (warrior/DPS) | 120 | 24 | 3.2 |
| Orc Spiritcaller (mage) | 85 | 21 | 3.0 |
| Orc Bulwark (tank) | 190 | 18 | 2.2 |
| **Orc Warlord (rare boss, 5%)** | **520** | **34** | 2.6 |

Threat scales **both HP and contact damage by +8%/tier**, clamped to +160% at threat 20 (`t = 1 + clamp(threat−1,0,20)·0.08`).

### 1.3 TTK (time-to-kill)
- **Hero → raider, L1, starter weapon:** `100 / 50 DPS ≈ 2.0 s`. A 4-member family ≈ **8–20 s** of fighting.
- **Hero → raider, maxed:** `100 / 360 DPS ≈ 0.3 s` — non-boss enemies are deleted on contact.
- **Enemy → hero:** see §1.4 — the dominant path caps at **24 HP/s**, so a 100-HP hero standing still in a full mob dies in ~4 s; a kiting hero takes almost nothing.

### 1.4 🔴 HEADLINE FINDING — two damage-to-hero paths, only one scales
There are **two concurrent ways an enemy hurts the hero**, and they disagree:
1. **`HeroHealth.Update` flat pull** — scans a 1.5 m ring and applies a **flat `DamagePerEnemy = 6`** per adjacent enemy per 1 s, capped at 4 enemies → **max 24 HP/s**. This path **ignores the enemy's authored `contactDamage` entirely.**
2. **`Enemy.ExecuteContactAttack`** — routes the enemy's **authored, threat-scaled `_contactDamage`** (16–34) through `ApplyContactDamage` → `HeroHealth.TakeDamage`.

The comments note enemies park at a 2.5 m siege radius "a metre outside HeroHealth's 1.5 m ring", which means for much of the fight the **flat-6 pull is the live path and the carefully-authored enemy damage table (16/18/21/24/34 + threat scaling) barely reaches the hero.** Net effect: **enemy damage variety and threat scaling are largely cosmetic** — a Warlord (34) and a Raider (16) hit the hero for the same flat 6, and a threat-20 zone is no deadlier per-hit than threat-1.

**Recommendation:** pick ONE damage model. Either (a) delete the flat-6 pull and let `ExecuteContactAttack` carry authored damage (preferred — restores the stat table's meaning), or (b) make `DamagePerEnemy` read `enemy.ContactDamage` instead of a constant. Until then, role/threat tuning of enemy damage has no felt impact.

### 1.5 🟠 Power creep — hero scales multiplicatively, enemies additively
Hero damage compounds (weapon × level × talent × timing), reaching ~7× a starter Knight. Enemy HP grows only **+8%/threat tier**. The curves diverge hard: by mid-game the hero one-shots the family while enemy HP has barely doubled. **Recommendation:** either scale enemy HP with a steeper threat curve (e.g. +15–20%/tier or a multiplicative band per region), or add enemy count/elite-density scaling so encounters stay meaningful as the hero's multipliers stack.

### 1.6 🟠 Defensive stacking has no diminishing returns
Incoming damage is reduced by **independent multiplicative sources**: armor `(1−defense)` up to 0.35, talent DR (Iron Resolve 0.18 + Resilience 0.20 + Knight Eternal 0.45 + Legendary Vanguard 0.35…), 25% full-block chance, Last Stand −60%, plus Eternal Aegis invuln windows and a once-per-run revive. `talentDr` is clamped to 0.95 **but armor and block are applied separately**, so effective mitigation can exceed 95%. A fully-specced Knight against the flat-6 pull is **functionally unkillable**. **Recommendation:** consolidate DR into one clamped pool (single `1 − totalDR`, hard cap ~80–85%), keep block/armor inside that cap, and gate the strongest capstones behind exclusivity (already noted as "owner-open: capstone exclusivity").

### 1.7 🟡 Ability damage is balanced for a different enemy scale
Knight kit (`abilities.json`): Heroic Leap 30 (cd6), Shield Bash 26 (cd9), Radiant Strike **220 AoE** (cd40). The 220 ult one-shots every non-boss in the family (HP 85–190) and nearly halves the Warlord (520). Mage ult is **600** (Meteor) — instakills any non-boss group. These read as tuned to the React v1 wave HP (Necromancer 1700), not the lighter arena family. Fine for "feel powerful" but reinforces §1.5: the ult trivializes the staged fight. **Recommendation:** either lengthen ult cooldowns relative to fight length or raise arena family HP so the ult is a burst, not a wipe.

---

## 2. Progression curve (XP / level / Wisdom)

### 2.1 XP curve (`HeroProgression.XpToNextFor`)
`cost(L) = 150 + 350·(L−1) + 500·(L−1)²` (front-loaded quadratic).

| Reach level | XP for that level | Cumulative XP |
|---|---|---|
| L2 | 150 | 150 |
| L3 | 1,000 | 1,150 |
| L4 | 2,850 | 4,000 |
| L5 | 5,700 | 9,700 |
| L6 | 9,550 | 19,250 |
| L8 | 27,100 | 53,900 |
| L10 | 43,800 | **115,950** |

Arena win XP = `(20 + 8·family + 4·threat) × starMult` + per-kill `14·t` × members. A family-4 / threat-3 / 3-star win ≈ **96 win + ~56–100 kills ≈ 150–200 XP/fight**.

### 2.2 🔴 The XP faucet doesn't match the curve for the V1 (arena) loop
- L1→L2 in **one** fight (150 XP) — good early reward, matches the "first level cheap" intent.
- Reaching **L10 needs ~116k cumulative XP ≈ 600–750 arena fights.** That is a wall.

The steep tail was tuned against **wave kill-XP (~1,800/wave** per the code comment), but the combat north-star is the **overworld arena encounter** (memory: overworld-encounter-isolated-battle), which pays ~175 XP/fight. **The curve is calibrated to a faucet that isn't the primary loop.** Either the arena needs to pay far more XP, or the top of the curve needs flattening (e.g. drop the quadratic coefficient from 500 to ~150–200), or both. As-is, talent acquisition stalls because levels are a primary Wisdom source (§2.3).

**Recommendation:** decide the canonical XP faucet (arena vs waves) and tune the curve to it. If arena is V1, target ~L10 in ~80–120 fights (≈ 5–10× current arena XP, or ~3× shallower curve).

### 2.3 🟠 Wisdom budget is under-specified across multiple faucets
- **Levels:** +2/level ≤L8, +3/level after → **~50 Wisdom by L20** (~70% of one tree).
- **Arena wins:** `(1 + family/2 + threat/2) × starMult` → **4–6 Wisdom per win** at mid threat.
- **Also** (per code): waves, daily quests, tier milestones grant Wisdom.

A full hero tree = 55 + 8 shared×2 = **71 Wisdom**. The design intent (HeroProgression comment, talent memory) is "**must specialize**, can't buy everything." But arena wins alone at 4–6 Wisdom/fight over the hundreds of fights the XP curve implies generate **far more than 71** — undermining the specialize constraint and letting a grinder buy a whole tree **plus** start a second hero's. The level faucet was deliberately tightened (89% → 70%), but the **other faucets were never budgeted against the 71 cap.** **Recommendation:** set a total-Wisdom budget for a maxed V1 hero (e.g. ~90–110, enough for one tree + shared but not two), then back-solve per-faucet rates; consider making arena Wisdom rarer (e.g. only on first-clear or milestone wins) rather than every fight.

### 2.4 🟡 Star rating barely interacts with progression
See §3.1 — because nearly every win is 3-star, the 1.5× multiplier is effectively a **flat** bonus baked into all XP/Wisdom/resource rewards rather than a skill incentive.

---

## 3. Arena rewards & star rating

### 3.1 🔴 Star thresholds are far too lenient for the actual TTK
`BattleStarRating`: **≤90 s = 3★, ≤120 s = 2★, else 1★.** But §1.3 shows a typical family fight resolves in **8–40 s**, and a maxed hero in **<10 s**. **Essentially every win is 3-star.** The star system — and its 1.5× reward multiplier, the gear-drop star bonus (`+0.10/star`), and the victory-burst flourish — is **dead content**: it never discriminates. **Recommendation:** retune thresholds to the real distribution (e.g. 3★ ≤ 20–25 s, 2★ ≤ 45 s, else 1★), or change the axis from raw duration to something the player controls under pressure (HP remaining, no-deaths, combo) so stars reward skill rather than the inevitable.

### 3.2 Reward grant (`GrantWinReward`), family-4 / threat-3 / 3★ example
| Reward | Formula | Value (×1.5) |
|---|---|---|
| XP | `(20 + 8·fam + 4·threat)` | 96 |
| Wisdom | `(1 + fam/2 + threat/2)` | 6 |
| Wood | `(10 + 4·threat)` | 33 |
| Iron | `(4 + 2·threat)` | 15 |
| Gear | `0.30 + 0.05·threat + 0.10·(stars−1)`, cap 0.85 | ~65% drop |

🟡 **Resource payout is trivial vs. the economy** (§4): 33 wood/win against storage tiers costing 400–7,000 wood means arena resources are flavor, not a real faucet — the **echo/idle economy is the resource engine**, the arena is the XP/Wisdom/gear engine. That's a defensible split, but worth stating as intentional. 🟠 **Gear drop ~65% per win** is generous; combined with the dead star-bonus it means gear floods in quickly — fine for a light loop, but check that the inventory/loadout can absorb the volume without trivializing the gear economy (`weapons.json` buy-costs become irrelevant if gear rains from the arena).

---

## 4. Economy balance

### 4.1 Echo / idle accrual (`offline-storage.json`)
- Rate = `activeEchoes × 120/hr`. Max **4 echoes → 480/hr** (~160 each of wood/iron/food via the 0.34/0.33/0.33 split).
- Echoes unlock every **5 waves** (`wavesPerEcho`), to 4 — ties idle income to the wave pillar.
- Silo cap (hours) by tier: **4h → 6 → 8 → 12 → 18h.** Absolute capacity = `capHours × rate`. At full echoes: base **1,920** → tier-5 **8,640** before overflow.

### 4.2 Storage upgrade ladder vs. accrual
| Tier | Silo / window (h) | Cost (wood/iron/crystals/coins) | Hours of full accrual to afford the wood alone |
|---|---|---|---|
| 2 | 6 / 12 | 400 / 150 / 0 / 250 | ~2.5 h |
| 3 | 8 / 16 | 1,200 / 500 / 200 / 800 | ~7.5 h |
| 4 | 12 / 24 | 3,000 / 1,400 / 700 / 2,000 | ~19 h |
| 5 | 18 / 36 | 7,000 / 3,200 / 1,800 / 5,000 | ~44 h |

🟢 **The ladder is well-shaped** — costs grow ~2–2.5×/tier and the per-tier wood cost is a few full-silo cycles, so each upgrade is a "save up over a day or two" sink. The overnight (T4=24h) and weekend (T5=36h) framing is coherent.

🟠 **Crystals are the bottleneck the whole economy routes through.** Crystals gate T3+ storage (200/700/1,800), rare+ weapons (`weapons.json`: 10–200 crystals), **and** building upgrades — but crystals are **not** in the echo silo split (`_splitNote`: "Crystals stay a premium/reward currency"). The only crystal faucets are mines/settlements/pets (`OfflineHarvestService`) and rewards. **Crystal demand vastly outstrips the faucet.** This is the intended monetization pressure, but it risks reading as a *wall* rather than a *choice* if the faucet is too thin. **Recommendation:** instrument actual crystal earn-rate in a headless idle run and confirm a non-paying player can afford ~1 rare weapon + T3 storage within the first few sessions; if not, widen the crystal faucet slightly or move T3 storage off crystals.

### 4.3 Packs (`packs.json`) — value vs. earn rate
| Pack | USD | Crystals | Crystals/$ | Notes |
|---|---|---|---|---|
| Hearth Spark | 1.99 | 200 | ~100 | 200 crystals = a full T3 storage tier, or a rare weapon |
| Lanternlight | 4.99 | 700 | ~140 | grants ≥ T3 storage outright (`packLinkedTiers`) |
| Folk's Thanks | 9.99 | 1,800 | ~180 | |
| Patron | 19.99 | 5,000 | ~250 | grants ≥ T4 storage |
| Founder's Vow | 49.99 | 15,000 | ~300 | grants ≥ T5; one-time |

🟢 **Standard whale-discount curve** (~3× value/$ from smallest to largest) — healthy.

🟠 **The cheapest pack ($1.99 / 200 crystals) skips the entire mid-game crystal economy** (a full storage tier or a rare weapon). Given crystals are the bottleneck (§4.2), this is a strong P2W lever for a "convenience-only, never combat-power" covenant (`_schemaNotes.convenience`). **A rare/epic weapon is combat power**, and packs hand the crystals that buy it. The covenant is honored for the *convenience items* but **not for the crystal grants**, which convert directly into weapon damage. **Recommendation:** either (a) make crystals purely cosmetic/storage (move weapon costs off crystals onto soft currency) to keep the no-combat-power covenant literally true, or (b) acknowledge crystals-as-power explicitly and tune the soft faucet so the paid path is a time-skip, not a power gate.

### 4.4 🟡 Dual premium currency (crystals vs. SKR) overlaps confusingly
Storage tiers expose **two** premium paths: a crystal soft-cost **and** an `skrFastTrack` (T2=30 → T5=240 SKR), while packs *also* grant tiers outright (`packLinkedTiers`) and are priced in SKR (25–600). A player who buys Lanternlight (60 SKR) gets ≥T3 storage for free, making the separate T3 fast-track (60 SKR) redundant. **Recommendation:** document the canonical premium path (SKR fast-track vs. pack grant vs. crystal grind) and remove or clearly differentiate the redundant one so the store reads cleanly.

---

## 5. Priority ledger

| # | Severity | Finding | Cheapest fix |
|---|---|---|---|
| 1 | 🔴 | Enemy authored `contactDamage` (16–34, threat-scaled) doesn't reach the hero — flat-6 pull dominates (§1.4) | Make `DamagePerEnemy` read `enemy.ContactDamage`, or remove one path |
| 2 | 🔴 | Star thresholds (90/120 s) vs. real TTK (8–40 s) → every win is 3★, star system is dead (§3.1) | Retune to ≤20/45 s, or change axis to HP-remaining/no-death |
| 3 | 🔴 | XP curve tuned to wave kill-XP, not the arena V1 loop → ~600+ fights to L10 (§2.2) | Pick the canonical faucet; flatten quadratic or 5–10× arena XP |
| 4 | 🟠 | Hero damage scales multiplicatively, enemy HP only +8%/tier → late-game trivializes (§1.5) | Steeper threat HP curve or count/elite scaling |
| 5 | 🟠 | Defensive DR stacks multiplicatively past 95% → maxed Knight unkillable (§1.6) | One clamped DR pool, hard cap ~80–85% |
| 6 | 🟠 | Wisdom budget not bounded across faucets → "must specialize" intent defeated (§2.3) | Set total-Wisdom budget, back-solve per-faucet |
| 7 | 🟠 | Crystal faucet ≪ crystal demand (storage + weapons + upgrades) (§4.2) | Verify earn-rate headless; widen faucet or move costs off crystals |
| 8 | 🟠 | Packs grant crystals that buy combat-power weapons — bends the "convenience-only" covenant (§4.3) | Move weapon costs to soft currency, or restate the covenant |
| 9 | 🟡 | Ults (220/600) one-shot the arena family (§1.7); gear drops ~65%/win flood loadout (§3.2); dual premium currency overlap (§4.4) | Cooldown/HP retune; drop-rate review; document premium path |

**Net read:** the **economy ladder (echoes + storage) is the best-tuned system** — coherent costs, sensible idle-vs-active split. The **combat and progression layers are tuned to a different (wave) era than the current arena north-star**, so the most impactful work is re-anchoring TTK, star thresholds, XP, and Wisdom to the actual overworld-encounter loop, and resolving the flat-6-vs-authored-damage disconnect so enemy stat tuning has any felt effect at all.
