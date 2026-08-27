# WORK ORDER 1064 — Measure daily Gold and rebuild the gear price ladder

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Parent:** WO-1063 · **Silo:** economy/pricing/regression

## Problem

The Daily Chest grants 500 Gold free or **1,000 Gold total after one rewarded ad**, once per UTC day.
Current gear resolves around 15–868 Gold, making most progression cheaper than one daily ad. Prices
derived from `GearAppraisal` also allow level-6 and level-10 overlap. Legacy `buyWood/buyIron/...`
fields do not drive vendor prices.

## A — Measure before tuning

Produce a dated, checked-in report of every live net-new Gold source:

- free and rewarded Daily Chest;
- enemy kills, wave clears and realistic full runs;
- dungeon rooms/chests/boss/settlement;
- raid/outpost first and repeat clears;
- Echo/passive Gold and caps;
- kill-combo Gold;
- actually redeemed quests, daily/weekly/season rewards.

Separate gear-sale refunds as recycling. Exclude dev tools, samples, dead config and authored rewards
with no grant consumer. Record cap, reset cadence and eligibility.

Model three profiles: chest-only, typical active, high-but-human activity.

## B — Acquisition-time targets

Initial owner-tunable bands after measurement:

| Progression | Chest-only target |
|---|---:|
| Starter | Granted |
| First alternative / uncommon | 1–3 days |
| Rare | 5–8 days |
| Epic | 10–18 days |
| Legendary | 25–40 days |

Active play must shorten these materially; watching one ad and leaving cannot be optimal progression.

## C — One price authority

`tier floor + effective stats + functioning effect + flexibility + set/prestige`

- Level/tier contributes a monotonic floor.
- Equal power needs a functioning effect/flexibility reason to cost more.
- Element may add a small flexibility premium; VFX/lore add none.
- Sell refund, list price, preview price and debit use the same resolved number.
- Emit a table: id, family, tier, power, effects, price, chest-only days, active-player days.

## Gates

- Pin Daily Chest 500/1,000 and once-per-UTC-day behavior.
- Every live gear row resolves price > 0.
- Prices are monotonic within each progression family.
- Displayed price equals debit; refund derives from the same authority.
- No epic/legendary costs less than one rewarded chest unless explicitly classified as a sidegrade.

## Do not

- Do not change the Daily Chest reward here.
- Do not count promised-but-unwired rewards.
- Do not silently nerf faucets to fit a chosen table.
- Do not hand-author a second UI price.
