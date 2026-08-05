# Reward Economy — measured baseline, 2026-08-04

**Status:** REFERENCE / measured facts. Not a plan, not a WO.
**Method:** read-only measurement, every figure verified at source. Nothing here is inferred from a
comment or a doc (CLAUDE.md §12 — comments lie).
**Measured against:** HEAD `35485f31`, i.e. **after** WO-855 (economy balance) and after today's
collector-income and crystal-mine fixes.

This exists so the reward WO is written from numbers, not from a summary of a summary. If you are
writing that WO, read this first. If a figure here disagrees with an older doc, this one was measured
later and at source.

---

## 0. Income denominators (what everything below is measured against)

basket = `wood + 1.5*iron + food + 2*crystals`

| State | basket/hr |
|---|---|
| EARLY (L1 collectors, 1 echo, no perks) | **2,304** |
| MID (L3, 3 echoes) | **16,046** |
| LATE (maxed, 6 echoes, full perks) | **141,480** |

⚠ The LATE figure supersedes the **127,440** quoted in the WO-855 Phase 5 report — that number omitted
the L5 gold collector. EARLY and MID reproduce exactly.

---

## 1. THE SCALING LAW — rewards do not scale with enemy level

**There is no level or tier field on an enemy.** `EnemyDef` (`Assets/_Modules/Village/Waves/WaveData.cs:116-124`)
carries exactly three reward fields — `xpReward`, `glimmerReward`, `coinReward` — all flat constants.
**No reward code anywhere reads a level.**

| Axis | Scales with wave? | Source |
|---|---|---|
| Enemy HP | Yes — x1.0 -> **x2.5**, hard-clamped | `WaveScalingCurve.cs:70-76`; injected `WaveManager.cs:1664-1668` |
| Enemy damage | Yes — x1.0 -> x2.0, clamped | same curve |
| Enemy count | Yes — 4 -> **22**, capped | `WaveCompositionBuilder.cs:147-149, 172-174` |
| **coinReward / kill** | **NO — flat forever** | `enemies.json`, read at `Enemy.cs:2625` |
| **xpReward / kill** | **NO — flat forever** | `Enemy.cs:2610` |
| Wave-clear resources | Yes — x1.0 -> x1.8 at w20, **uncapped past it** | `WaveManager.cs:2405-2407` |

**Consequence:** a wave-20 Hollow Walker has 130 HP (2.5x a wave-1 walker's 52) and pays the identical
**4 gold and 10 XP**. Reward-per-effort on any individual enemy DECAYS ~60% across the schedule. The
only reason a late wave pays more is that the composition swaps in more expensive *species* — a mix
change, not a scaling law.

Two corrections to previously-circulated numbers:
- The scale formula is `step = waveId / 5` (integer division), **not** `floor((wave-1)/5)`
  (`WaveManager.cs:2405`). Wave 5 already pays x1.2.
- The `[SerializeField]` reward defaults **are** the live values — confirmed in the baked scenes
  (`Main_Castle_Overworld.unity:2860-2876`, `MainCastle_Hall.unity:1626-1642`): wood 20/spread 10/
  interval 3, iron 15/10/4, food 30/20/2, scalePerStep 0.2, **stepCap 0**, woodPerKill 1, ironPerKill 0.
  No override anywhere. `_woodRewardPerWave` / `_ironRewardPerWave` are **0**, so the WO-330 linear ramp
  is wired but inert.

---

## 2. Coin per difficulty — this part is CORRECT, leave it alone

Coin tracks HP tightly. `coin ~= 0.084 * HP ~= 0.45 * XP` across the entire roster; median 84 coin/1k HP;
total spread only **1.8x**.

| enemy | hp | coin | coin/1k HP |
|---|---|---|---|
| hollow-warrior | 156 | 10 | **64.1** (lowest) |
| hollow-brute | 900 | 60 | 66.7 |
| necromancer | 1700 | 120 | 70.6 |
| troll | 320 | 24 | 75.0 |
| hollow-walker | 52 | 4 | 76.9 |
| ogre | 280 | 22 | 78.6 |
| orc-necromancer | 600 | 50 | 83.3 |
| orc-raider | 130 | 11 | 84.6 |
| orc-berserker | 117 | 10 | 85.5 |
| hollow-rogue | 70 | 6 | 85.7 |
| hollow-acolyte | 90 | 8 | 88.9 |
| orc-shaman | 78 | 7 | 89.7 |
| hollow-mage | 85 | 8 | 94.1 |
| hollow-reaper | 240 | 28 | **116.7** (highest) |

**The 3–120 coin spread is not a balance problem — it is HP tracking correctly.** Only in-table outlier
worth touching: `hollow-warrior` underpays 24% and is the most-spawned mid/strong unit.

**Gold is the one correctly-tuned currency.** Sinks: research tree **13,650** gold total
(`ResearchCatalog.cs:63-207`, 26 nodes at 150–1,500), ward stones 1,550, crystal mine 600, fountain
1,400 — ~**17,200** for the whole permanent tree. A 20-wave run pays **3,487** gold, so ~5 runs buys it.
Note `building-tiers.json` perks are **not** gold-priced (they cost `pointCost` from building XP —
`building-tiers.json:5`).

---

## 3. Total-run economics, post-WO-855

Designed 20-wave payout: **1,214 basket + 3,487 gold + ~3.6 crystals**, over 8,020 s wall-clock
(2.23 h — 5,745 s countdown + 2,275 s combat).

| | basket/hr | vs EARLY | vs MID | vs LATE |
|---|---|---|---|---|
| Wall-clock (2.23 h) | **545** | **0.24x** | 0.034x | 0.004x |
| Combat-only (0.63 h) | **1,921** | **0.83x** | 0.12x | 0.014x |

**The whole 20-wave campaign does not fund one tier-1 building upgrade.** Cheapest tier-1 is 500 wood +
700 food; the campaign yields 460 wood.

WO-855 raised the loop's relative weight from ~3% to ~24% of EARLY income — real progress — but it decays
to 3.4% at MID and 0.4% at LATE, because the reward ramp (+20% per 5 waves, x1.8 at w20) is far shallower
than the income curve (7.0x EARLY->MID, 61x EARLY->LATE).

---

## 4. ⚠ ACTIVE PLAY PAYS LESS THAN IDLING — the headline number

- **Per combat-hour: 1,921 vs 2,304 = 0.83x.** An hour of the player's attention buys 17% LESS than an
  hour of doing nothing, at EARLY. At MID 0.12x. At LATE 0.014x.
- **Per wall-clock wave cycle (401 s avg): 61 basket fought vs 256 idled = 0.24x.**
- Per-wave `xIdle` ranges **0.04x–0.50x** and **never once reaches 1.0** across all 20 waves.

---

## 5. ⚠ THE UNBUDGETED CRYSTAL FAUCET — live, 400x the designed rate

`Assets/_Modules/Village/Waves/KillComboTracker.cs:46-47` — **25 Aether Crystals at a 5-kill streak,
60 more at 8**, granted via `CrystalEconomy.AddCrystals` (`:190-198`). Streak window 8 s
(`CombatFeedbackManager.cs:125`).

**It is LIVE.** It appears in no scene (GUID `4b63d198…`, zero hits) but self-installs at `:228-246` via
`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` whenever a `WaveManager` exists — and
`Main_Castle_Overworld.unity:2842` has one. Ordering is safe (`CombatFeedbackManager` bootstraps at
`BeforeSceneLoad`, `:159`), so the subscription lands.

| | crystals per 20-wave run |
|---|---|
| **Designed** boss-wave drop (`WaveManager.AwardWaveCrystals:2519-2558`; `ServerConfig.cs:161-164` — 45% chance, 1–3, every 5th) | **3.6** |
| **Actual** combo faucet, one streak per wave | **1,435** |
| Combo faucet, farming the 8 s window | 2,155 |

**400x the designed rate**, uncapped and undocumented. It directly contradicts the WO-830 monetization
guard in `echoes-balance.json` `_authoringNotes` ("crystals remain the slowest faucet") — the guard
WO-855 was built around.

At basket weight 2.0 those crystals are **2,870 basket**, i.e. **2.4x the entire designed wave reward**.
Real 20-wave payout is therefore ~4,091 basket (0.80x EARLY idle), of which **~70% comes from a system
nobody budgeted.** This is why the loop does not *feel* completely dead.

---

## 6. ⚠ THE OVERWORLD REP LOOP — the game's actual optimal strategy

`OverworldEncounterSpawner` is the **only** roaming population in the live build. Every other world
spawner is code-complete but returns at the top of `Update()`/`Bootstrap()` because `ff.overworldencounter`
defaults ON — verified `RegionMobSpawner.cs:150`, `TribeManager.cs:123`, `CampSystem.cs:118`,
`RaidOutpostSystem.cs:142`.

Reward payload (`OverworldEncounterSpawner.cs:909-945`):
```
Hp            = 98f * levelScale                  // levelScale = 1 + 0.08*(threat-1)
ContactDamage = 0f                                // it cannot hurt you
XpReward      = round(14 * bodies * levelScale)   // bodies = FULL rolled pack size, 1..7
// no CoinReward -> Enemy.cs:2625 fallback: gold = max(4, round(xp * 0.4))
```

- With `ff.overworldleaderonlyroam` ON (default, `FeatureFlags.cs:95`) **only one body spawns but XP is
  paid for all 7.**
- `RepEngageWatcher.RangedHitsEngage = false` (`:1123`); the code comment at `:1091` says it outright —
  *"the rep can be WHITTLED DOWN and KILLED in the open world by ranged."*
- Killing it runs the full `Enemy.Die` path: gold, XP, Glimmer, item drop, **and the combo crystals above**.
- Respawn: 6 ring reps top up every **10 s** (`:76`), 8 scatter reps every 180 s. **No lifetime cap.**

| source | HP | gold | coin/1k HP | vs wave median |
|---|---|---|---|---|
| Wave roster (median) | — | — | 84 | 1.0x |
| **Apex dragon Syndrath** | **4,200** | **0** | **0** | **0x** |
| Overworld rep, threat 3, pack 4 | 114 | 26 | **229** | **2.7x** |
| Overworld rep, threat 5, pack 7 | 129 | 52 | **403** | **4.8x** |

At a conservative 5 kills/min against 114 HP zero-damage targets: **~7,800 gold/hr, ~19,500 XP/hr,
~3,187 crystals/hr (~6,375 basket/hr = 2.8x EARLY idle)** — renewable and risk-free.

---

## 7. ⚠ THE APEX DRAGON PAYS NOTHING

`DragonBoss.cs` (1,588 lines) grepped for `AddCoins|CoinReward|XpReward|Grant|AddXp|reward|Econom|gold|
Progression` returns **zero matches**. `Die()` (`:1336-1350`) only fires `Died?.Invoke(this)`, and
`WaveManager.HandleApexBossDied` (`:2716-2720`) just unsubscribes.

**Syndrath: 4,200 HP, 285 s authored combat — the longest fight in the game — pays 0 gold, 0 XP,
0 Glimmer, 0 crystals.** Its only reward is the `boss-hoard` loot table via `ItemDropWatcher.OnBossDied`
(`ItemDropWatcher.cs:106-111`): ~10 crafting materials. At the roster's own median rate it should pay
~353 gold and ~785 XP.

Ground bosses are fine by contrast — the wave-5 troll (1,050 HP, `waves.json` `bossHp`) and the
necromancers at 6/12/18 route through `Enemy.Die` and pay normally.

---

## 8. ⚠ ENDLESS MODE IS AN UNBOUNDED INFLATION EXPLOIT

Past wave 20: enemy HP clamps at x2.5 and roster count caps at 66 bodies by wave 60 — **difficulty is
flat from there.** But `_rewardScalingStepCap = 0` means the payout multiplier grows **+20% every 5
waves forever**: **x5.0 at wave 100, x9.0 at 200, x41 at 1000** — against fixed difficulty.

---

## 9. Content authored but unreachable

- **`hollow-mage`, `hollow-reaper`, `hollow-brute` never spawn in waves.** `WaveCompositionBuilder`'s
  pools (`:129-144, 305-319`) never draw them. Paid-for content with zero spawn path. (`hollow-reaper`
  is also the roster's highest coin/kHP at 116.7 — and unspawnable.)
- **`orc-raider` xpReward mismatch:** `enemies.json:288` says 24, the `WildlandsRoster.cs:155` code
  fallback says 22.

---

## 10. Verdict

**Not "dead" and not "overpaying" — miscalibrated in both directions at once.** The intended
defend->earn->build faucet is starved (0.24x idle, decaying to 0.4% of income at LATE), while two
unintended faucets run wide open: an undocumented crystal combo bonus that violates the crystal guard
and supplies ~70% of banked value, and a risk-free respawning overworld farm paying 4.8x the roster's
own gold rate.

**Any reward WO should treat §5 (crystal combo) and §8 (endless runaway) as live defects to close
BEFORE tuning §3/§4 upward** — otherwise the tuning is applied on top of two faucets that will be
removed, and the numbers will be wrong twice.

---

*Measured 2026-08-04 against HEAD `35485f31`. Every file:line opened at source.*
