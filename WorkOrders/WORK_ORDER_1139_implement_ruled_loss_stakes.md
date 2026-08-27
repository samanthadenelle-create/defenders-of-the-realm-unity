**Status:** SUPERSEDED 2026-08-27 - see the banner directly below. Prior status: FIXED - AWAITING OWNER FELT-TEST TO CLOSE.

> # SUPERSEDED 2026-08-27 - BANK THEFT REPLACES COLLECTOR LOOTING
>
> **The ruling this ticket implemented is no longer the ruling.** The owner was shown that her
> 2026-08-26 siege ruling reinstated the system this ticket's 2026-08-22 ruling had deleted, and she
> resolved the collision:
>
> > **BANK THEFT REPLACES COLLECTOR LOOTING. A siege bills ONCE per attack, not twice.
> > Collector looting is REMOVED.**
> >
> > A siege takes exactly three things: structural damage, a repair bill, and theft of a PERCENTAGE
> > of UNPROTECTED bank resources under a PROTECTED FLOOR and a PER-ATTACK CAP.
> >
> > ```
> > LOOTABLE      Wood, Iron, Stone, Coins
> > UNTOUCHABLE   Crystals, SKR, purchased goods, equipped gear
> > ```
>
> **THE BODY BELOW IS FROZEN AND IS NOT REWRITTEN** (CLAUDE.md section 15: a dated point-in-time
> record gets a banner, never a rewrite). Everything it says about "collector looting only, no bank
> theft" was true when written and describes a system that no longer exists.
>
> **What actually changed in the tree, 2026-08-27 (WO-1026):**
> - `ResourceCollector` no longer takes anything when it breaks. `RaidLootFraction`,
>   `LootTakenFrom`, `IsResourceLootable` and `IsLootable` are DELETED -- that removal is what makes
>   the double-charge this ticket feared *inexpressible* rather than merely forbidden.
> - `StakeRules` regained a protected floor, a per-attack cap and the take arithmetic, now covering
>   **coins** as well as wood/iron/stone. The numbers are authored in
>   `Data/Canonical/siege-stakes.json` behind `SiegeStakesBalance` and are **OWNER-PENDING**.
> - `DefenseReportBuilder.ApplyStakes` performs **the single debit** through the existing
>   `EconomyService.TrySpend` path, of exactly the buckets the report renders.
> - `SiegeLossStakesRegression` was **RE-POINTED, NOT DELETED**. Its headline case used to fail the
>   gate if the bank moved at all; it now fails if the bank does not move by *exactly* the ledger,
>   and it gained the structural + behavioural proof that collector looting is gone. A green oracle
>   going red on a ruling change is the oracle doing its job.
> - The crystal exemption is **unchanged and unweakened**, and `SiegeUntouchableRegression` still
>   guards crystals / SKR / purchased goods / equipped gear on both of its axes.


<!-- The original status line, preserved verbatim: -->
<!-- **Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: IMPLEMENTED 2026-08-22 - COLLECTOR LOOTING ONLY. The bank-theft rival was deleted and made unbuildable (reflection fails the gate if its methods return). Crystal collectors exempt in two independent places. FeatureFlags.Siege is now ON and PROVEN - all four siege suites green on a fresh log. -->

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

---

# ★★ RULING SUPERSEDED 2026-08-22 — THE THEFT ALREADY EXISTS. DO NOT BUILD A SECOND ONE.

## THE FINDING
While implementing the flat 15%-of-bank take, the mechanic the owner actually wanted was found
**already shipped** as WO-664:

- `ResourceCollector.OnSiegeDestroyed()` -> `stolen = floor(_pending * RaidLootFraction)`,
  `RaidLootFraction = 0.5f`. **Half the UNCOLLECTED pending is carried off when a collector breaks.**
- `ResourceCollector.LastLootStolen` - "Pending resources stolen when the collector last broke under
  siege (session-scoped; cleared on Repair). The wave damage report reads it to show the 'looted' line."
- `WaveDamageReport.cs:107` already surfaces it as `LootStolen`.
- `ISiegeLootTarget` + `EnemyBrain.cs:1597` - enemies **prioritise collectors**, checked BEFORE the
  generic structure fallback. `SiegeRoleValue => 0.85f * (1f + FillFraction * 0.75f)`, so a FULL
  collector scores 1.49 vs an empty 0.85: raiders go for the ones worth robbing.

So the flat bank take built under this WO is a **RIVAL SYSTEM** - a second theft, from a different
pool, on a different trigger, through a different ledger. Two authorities for one concept.

## THE RULING (owner 2026-08-22: *"go with your recommendation"*)

> ## COLLECTOR LOOTING ONLY. `RaidLootFraction` STAYS 0.5. NO BANK THEFT.

**The player-facing rule, and it must stay this teachable:**
> **What you have COLLECTED is safe. What is still sitting in the building is at risk.**

**Why, recorded so it is not re-litigated:**
- **CoC parity.** CoC loots collectors heavily AND storages lightly - but the storage half only
  survives because of SHIELDS, village guard, the LOOT CART, and matchmaking limits. **We have none
  of that scaffolding**, so adding bank theft would make us HARSHER than the game we are modelling.
- **Agency is the retention variable, not severity.** Collector loot is fully preventable by
  collecting: it converts into return visits, the player blames themselves, resentment is low. Bank
  loot has NO agency, especially offline - loss aversion spikes short-term logins and is a leading
  churn cause.
- **Do not raise the fraction.** Return-visit pressure does not scale linearly with pain; it climbs
  until it crosses into "why bother", and that cliff is invisible until players are gone. The lever
  with better returns is **LEGIBILITY** - a report saying "your silo broke, 400 wood carried off"
  teaches the collect habit better than a harsher number nobody understands.

## ⛔ CRYSTAL COLLECTORS ARE NOT LOOTABLE (CLI decision 2026-08-22, reversible)
`HarvestResource` includes **`Crystals = 0`**, so a crystal collector exists and would otherwise be
looted. A player cannot distinguish harvested crystals from PURCHASED ones - they are the same
wallet - so any crystal loss reads as losing bought currency. Same reasoning as the bank exemption.
Pin it with a regression, not with care.

## WHAT THIS WO NOW IS
**REPORT the loot that already happened. Compute nothing.** `StakesLedger` is populated by SUMMING
`LastLootStolen` across collectors broken this siege - the number the player is told is the number
the collector actually lost, because it is the same number. Delete the bank-take arithmetic.
