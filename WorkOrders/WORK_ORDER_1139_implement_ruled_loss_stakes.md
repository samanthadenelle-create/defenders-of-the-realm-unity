**Status:** READY TO IMPLEMENT

# WORK ORDER 1139 — Implement the ruled loss stakes: theft, the repair bill, and turning the siege on

**Minted:** 2026-08-21 (CLI, banner bumped 1139 -> 1140 in the SAME edit)
**Lane:** Raid / village defense. **Class:** the CONSEQUENCE half of the loop.
**Split out of:** WO-1026, whose own deliverable (siege cadence + persisted Defense Report) SHIPPED
2026-08-21. This is the piece that was ruled but not built.

## ⛔ THIS TICKET IS THE ONLY THING KEEPING `FeatureFlags.Siege` OFF

The cadence and the report are live in the tree. Without stakes a siege resolves, reports, and
takes NOTHING - the "safe interim" WO-1026 named. That is correct to sit in the tree and WRONG to
ship as the finished loop. **Turning the flag on is the last step of this ticket, not the first.**

## THE RULING (owner 2026-08-21 — recorded in full in WO-1026, summarised here)

### What the player LOSES
| Rule | Value |
|---|---|
| Steal fraction | **15% of CURRENTLY BANKED** wood / food / iron |
| Protected floor | below **~20% of that resource's capacity is UNTOUCHABLE** |
| Crystals | ⛔ **NEVER STEALABLE** |
| Offline sieges | **YES — they steal too**, not only sieges the player fought |
| Repair bill | only structures that ACTUALLY took damage; `ceil(buildCost x damageFraction)`, **crystals never charged** |
| Troops | normal recovery path (5 / 20 / 45 min by difficulty). Never permadeath. |

### What the player NEVER loses
**No building downgrade. No destroyed permanent progress. No lost stars or cleared-camp progress.**

⚠ The ruling reversed twice inside one exchange (a clicked "resources stolen" option, then *"No
resource theft"*, then **"Allow theft, i think it causes real risk"**). The THIRD is live. WO-1026
records all three with the superseded block struck through — read it there before implementing.

## ⛔ THE CRYSTAL EXEMPTION IS NOT A BALANCE KNOB
Crystals are purchasable with real money. Taking a currency a player paid for converts a gameplay
loss into a refund request. Wood/food/iron are EARNED; crystals are BOUGHT.
*(Context that lowers the risk but does NOT license changing this: the pay path has never been
activated, so nobody currently holds a purchased balance — see memory
`published-but-payments-never-activated`. The exemption still stands, because it must hold on the
day payments DO go live, and that day must not require anyone to remember this rule.)*

## THE TWO IMPLEMENTATION CONSTRAINTS THAT MATTER MOST

1. **Theft MUST be computed from the SAME persisted record the report reads.** If what the player
   is TOLD they lost and what the wallet ACTUALLY lost are computed twice, they WILL drift — and a
   report that lies about a loss is worse than no report. One computation, one number.
2. **An offline theft MUST be legible on next launch.** The player learns it from the report, never
   by noticing a number got smaller. An unexplained loss is the resented version of this mechanic;
   an explained one is the loop working.

## SEAMS THAT ALREADY EXIST — COMPOSE, DO NOT GREENFIELD
- `DefenseReportBuilder.BuildStakes` — the SINGLE plug point. Replace rule id
  `none.interim.wo1026`; `StakesLedger` stops being all-zeros.
- `DefenseOutcomeRecord` already carries flattened `RepairWood/Iron/Food`, `HoldTimeSeconds`,
  `BreachOrdinal`, ring-buffered at 10.
- `WallRepairController` is the one repair-pricing authority (`RepairAllCost()`, crystals never
  charged) with a persistent `HubRepairAffordance` button.
- `ArmyStorage.MarkWounded` / `AdvanceRecovery` — offline-safe, monotonic, seeds forward.
- Storage caps (`stockpiles-cap-capacity`, WO-947 baskets) — theft READS the cap to compute the
  floor; it must not invent a second capacity notion.

## ACCEPTANCE
- [ ] A lost siege takes exactly the ruled slice; a player under the floor loses NOTHING
- [ ] Crystals are never touched — pinned by a regression, not by care
- [ ] Report figure == wallet delta, from one computation (regression, not inspection)
- [ ] An offline siege is legible on next launch before the player notices the number
- [ ] Nothing downgrades, nothing permanent is destroyed, no cleared-camp progress is lost
- [ ] `FeatureFlags.Siege` ON — **last**
- [ ] Owner felt-verify: *does losing feel like it was my fault, and do I know what to change?*
