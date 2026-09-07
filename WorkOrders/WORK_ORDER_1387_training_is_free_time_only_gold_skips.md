# WO-1387: training and troop upgrades cost TIME only - gold is spent only to skip the clock

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:49:47, build 2026.09.07.358574). PRIOR STATUS: FIXED - in 65d5a7eae, on the Seeker in build 2026.09.05.355952 (training charges nothing, "Train one: 45s . Ready"; HIRE REINFORCEMENTS is the gold skip; TrainingCostsTimeOnlyRegression green). Device proof on 355952: TRAIN 1 FOOTMAN -> enqueued -> TRAINING NOW rows=1. Awaiting owner felt-test.

## Owner, verbatim (2026-09-04 23:14-23:16, Seeker, build 355905)
> "can you check the training. seems brutal almsot 4000 stone to upgrade, and gold? Seems should be
> resources and the speed up is gold" -> "we agreed earlier training free" -> "just time" ->
> "and gold is to hire mercenaries if they dont want to wait" -> "the last CLI did bad changes" ->
> "the idea was lets start them with a free army to get them into raids"

## THE REVERSAL CHAIN - read before touching anything (CLAUDE.md s15: never silently re-derive)
1. WO-1372 (2026-09-04 morning): "troops cost TIME, gold buys time" - the line "FREE. Time only."
2. Commit `281902df0` (afternoon): the owner's three quotes ("this is the north star map", "gold buys
   hire mercenaries instead of waiting on time") were read as: gold is the PRICE of a troop (1,650 for
   three starters), time is the pacing, a second gold spend hires mercenaries. WO-1372's line was
   struck through. `troops.json` carries `costGold` per troop; `BarracksProgression.TroopUpgradeCost`
   returns `coins: CostGold * targetLevel` (`:154-160`, "placeholder curve; WO-771.14 owns balance").
3. Tonight, on the device: 550 gold to train, thousands to upgrade -> "we agreed earlier training
   free ... just time ... and gold is to hire mercenaries if they dont want to wait ... the last CLI did bad
   changes". **This is the live ruling.** It restores (1); (2) was the previous CLI mis-reading her quotes. Recorded in `KEY_FACTS.md`.


## THE INTENT (owner, 23:20): "start them with a free army to get them into raids"
Already in the build and green, and this WO must not disturb it:
- `StarterArmyGrant` - the first Barracks grants 3 free deployable Footmen through the one roster owner,
  latched once per save (`[starter-army-grant]`, in build 355872/355905).
- WO-823 first-raid soft gate (save v41 `everCompletedRaid`): the raid door opens on 3 deployable SLOTS
  until the first raid completes, then the full cap applies.
This WO makes everything AFTER the free three cost only time, so the path is: free army -> first raid ->
raid gold -> hire mercenaries when impatient. Acceptance adds: a fresh save reaches BEGIN ASSAULT with
zero gold spent (headless: StarterArmyGrantRegression + RaidFunnelRegression steps 1-3 green on the same tree).

## What changes (seams read at source)
- `BarracksService.EnqueueTraining` / the VM train row (`ManageScreenVM.cs` ~:1007 `AddGoldBrowseRow("Train "
  + name, ..., def.CostGold, ...)`, `FillTrainFacts`): the train cost is ZERO; the fact line reads
  `Train one: 45s . Ready` (no gold term). `troops.json` `costGold` STAYS on the row (it is the raid-reward
  anchor and the mercenary-hire basis) but is NOT charged at enqueue.
- `BarracksProgression.TroopUpgradeCost(troopId, level)` returns an EMPTY `ResourceCost`; `TroopUpgradeSeconds`
  is the only price. `CanUpgradeTroop` drops the affordability test; the upgrade fact line reads
  `Upgrade: 90s . Ready`.
- `BuildTimerService.FinishPaysGold(TrainTroop)` and `HireReinforcementsPrice` UNCHANGED - gold still buys
  the skip (WO-1372 Lane D, shipped tonight).
- `StarterArmyGrant` (3 free footmen) unchanged.
- Suites: `HireReinforcementsRegression` (case 3 fixture seeds coins - still valid), `RaidGoldArrowRegression` /
  `RaidLootCurrencyRegression` (reward sizing stays as ruled; do not touch), `ManageTroopsTrainDoorRegression`
  (rows `Train `/`Upgrade ` - keep), `StarterArmyGrantRegression`, `TroopRosterRegression`,
  `BuildTimerMercenaryRegression`: re-read each and re-pin only where a pin encodes a gold train cost.
  Add `[training-costs-time-only]`: enqueue a Train job with ZERO coins/resources -> succeeds; upgrade with
  zero -> succeeds; proven RED first.

## Acceptance
- [ ] On the Seeker: TRAIN 1 FOOTMAN with 0 gold works; the fact line shows time only; HIRE REINFORCEMENTS
      still quotes gold for the skip.
- [ ] Upgrade to L2 with 0 resources works; the upgrade line shows time only.
- [ ] `REGRESSION_OK` with the new pin; the reward suites untouched and green.
- [ ] `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` gets a dated banner at the troop-cost lines pointing here.
