# Gear Gold ledger — 2026-08-22

Scope: source-proven, net-new Gold (`EconomyService.Coins`) only. Sale refunds are recycling and
are excluded. Dev grants, authored rewards without a live grant call, crystals, and paid pack
contents are not used to justify the earn-time ladder.

| Faucet | Live amount/rate | Cadence/cap | Source proof |
|---|---:|---|---|
| Daily Chest, free | 500 | once per UTC date | `DailyChestController.BaseGold`, `DailyChestDayKey`, `TodayKey()` |
| Daily Chest, rewarded | 1,000 total | alternative claim; one rewarded ad and once per UTC date | `DailyChestController.WatchForDouble/Claim` |
| Enemy kill | 3–140 base per current canonical row, ±10–15% | per kill | `enemies.json coinReward/rewardVariance` → `Enemy.Die` → `EconomyService.AddCoins` |
| Kill combo | 25 at 5 kills; another 60 at 8 kills | once per threshold per streak | `KillComboTracker` |
| Challenge outpost | 120 | per successful live clear while `ff.raidwalk` is enabled | `ChallengeOutpostVictoryController` |
| Echo Gold assignment, level 1 | 900/hour before aggregate multipliers | online/offline production; must assign an owned Harvest Echo to Gold and bank the silo | `EchoBonusCalculator.HarvestRatePerHour` → `EchoService.DumpSilos` |

## Exclusions and reachability notes

- The challenge-outpost payout is excluded from the default profile while `RaidContinuousWalk` is
  off. A constant behind a disabled route is not income.
- Pack coins are purchased goods, not play income.
- Gear-sale refunds are 50% of the same buy authority and are recycling, not a faucet.
- Battle-pass, quest, seasonal, raid and dungeon data rows are not counted here unless their live
  grant consumer and practical cadence are demonstrated. This prevents promised configuration from
  being treated as spendable income.
- Echo Gold is repeatable but opportunity-costed: assigning an Echo to Gold displaces another
  harvest target. It is therefore included only in active profiles, never chest-only.

## Acquisition profiles used by the opening ladder

These are explicit calibration profiles, not claims about telemetry:

| Profile | Daily Gold model |
|---|---:|
| Chest-only | 1,000 |
| Typical active | 1,900 + combat drops (rewarded chest + one hour of Lv1 Echo Gold) |
| High-but-human | 4,600 + combat drops (rewarded chest + four hours of Lv1 Echo Gold) |

Combat drops remain additive rather than folded into a false single average until run telemetry
records roster composition and completion. The price authority therefore uses conservative tier
floors of 1,000 / 2,000 / 6,000 / 12,000 / 25,000 Gold before stat/effect/set premiums. Active play
shortens each target materially, while the 1,000-Gold daily chest remains the stable denominator.

## Price authority

`GearCatalog.GetBuyCost` is the shared list/debit authority and delegates to `GearAppraisal`.
PartyShop display and `EconomyService.TrySpend` consume that same `ResourceCost`; refunds scale that
same result by 50%. VFX and narrative names add zero. A functioning authored effect adds a bounded
20% tier-floor premium; a missing consumer must not be authored or advertised.
