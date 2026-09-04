# WORK ORDER 1374 - P0: close the raid economy loop

> # ✅ RESOLVED 2026-09-04 - THE MAP WINS, AND BOTH RULINGS SURVIVE INTACT.
>
> Owner, three times and in this order: ***"these findings take presedence"***, ***"this is the north
> star map"***, and ***"Make the goal when everything matches what i gave you"***. That settles the
> precedence question this banner was raised to ask. **`docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` is
> the specification.** Troops COST GOLD.
>
> **The two rulings were never actually exclusive - the earlier one is a SPEED-UP, not a price.**
> Read together they compose:
>
> | Axis | Ruling | Source |
> |---|---|---|
> | Troops have a **gold price** | 1,650 for three starters at Camp I | the map §1, and it is what the raid reward is sized against |
> | Troops also take **TIME** | a training clock, one of the map's three clocks (§5) | WO-1372 |
> | **Gold BUYS the remaining time** | *"paying gold is like saying we hired mercenaries"* | WO-1372, owner verbatim |
> | **Surplus resources SELL for gold** | *"players should be able to sell extra resources to get gold, for troop building"* | WO-1372, owner verbatim |
>
> So the sink is the gold price, the clock is the pacing, and mercenary-gold is the impatience tax on
> top - three distinct knobs, not one contested one. Nothing in WO-1372 is discarded except the single
> line *"FREE. Time only."*, which the map supersedes.
>
> ⛔ **The one thing that must NOT happen:** shipping the map's gold table on top of free troops. That
> is a faucet with no sink, and it was the real risk this banner caught.


**Status:** READY TO IMPLEMENT
**Silo / Lane:** Economy / raid rewards + FTUE + army grant
**Type:** NEW BEHAVIOUR on existing systems, owner-ruled
**Minted:** 2026-09-04 (CLI)
**Severity:** ⛔ **P0 — the north-star loop does not close without it.**

> ⭐ **THE SPEC IS `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` — THE NORTH STAR MAP.**
> Owner ruled 2026-09-04: *"these findings take presedence"* / *"this is the north star map"*.
> ⛔ **This ticket deliberately does NOT restate the numbers.** Read them there. A reward table
> copied into a work order is the duplicated-state defect this repo has paid for four times
> (CLAUDE.md §2 WO numbers, §5 the assembly table, §16 the R2 verify, WO-1137's fallback catalog).

## THE DELIVERABLE — the map's §10 "P0" list

Raid rewards **wood** · raid rewards **iron** · raid rewards **gold** · **rebalance crystals DOWN** ·
**free starter army** on Barracks completion · raid dailies require a Barracks · the Arena Herald
respects the raid gate · the Guide says "Journey -> Raids" · FTUE introduces Barracks -> army -> raid ·
the refusal message states explicitly what is missing.

> *"This alone could dramatically change behavior."*

## ⭐ THE SMALLEST PART IS THE BIGGEST WIN

`RaidScoring.cs:92-98` is **four serialized ints** (`_lootCrystalsBase 25`, `_lootFoodBase 60`,
`_lootCrystalsPerStar 10`, `_lootFoodPerStar 20`) inside one pure static function, and `ResourceCost`
**already carries wood, iron and coins**. Paying the right currencies is a data-shaped change.
⛔ Do not greenfield a loot system.

## ⛔ AND THE FUNNEL SHIPS IN THIS TICKET — see the map §11

Six events on the EXISTING analytics rail (`EventTracker` -> `/api/events/track` -> Neon
`analytics_events`): barracks unlocked · army trained · first raid attempted · first raid won ·
raid reward spent · **second raid attempted within 24h**.
⛔ Do not build a second telemetry path. ⭐ Without this the whole programme is unmeasurable and the
next redesign is guesswork — *"that last one is the gold nugget."*

## ⚠ DEPENDENCIES — read the map §12 before starting
WO-1372 (troops cost TIME, gold buys time) **changes what the 1,650-gold wall means** and must be
reconciled with the gold-sized-to-army-replacement maths. WO-1373's rough-stone chain is the other
reward axis and **must not double-pay**.

## EVERY NUMBER IS A TUNABLE. BINDING.

Standing rule (owner, 2026-09-02): **a balance value is a TUNABLE, default answer YES.** Register
the map's values as DEFAULTS on the existing rail — `Core/Ops/RemoteTunables.cs` Registry ·
`RemoteTunablesService.cs` · `TUNABLE_KEYS` in `api/_lib/tunables.js` · the Command Center Balance
tab — **all four in the SAME commit**; `[tunable-defaults]` names any two that disagree.
⛔ Do not build a second rail. No row / no network / no parse ⇒ today's behaviour exactly.
⭐ She is setting the reward curve for the main loop BY FEEL. Every value must reach her device in
~40 seconds, not a 10-minute APK round trip.

## ⛔ REGRESSION COVERAGE IS NOT OPTIONAL

Owner, 2026-09-04: *"shouldnt these items all have regression cases? so that stuff cant break
working features"*. **Every behaviour this ticket adds ships with an oracle, PROVEN RED FIRST**
against the real failing input, then green. A test that has never failed proves nothing (WO-1138).
Register each suite in `DataRegression` — an unregistered oracle never runs (the WO-973 failure).

## WHAT NOT TO TOUCH
- ⛔ No new currency. The map §3 names this explicitly, and Voidshards are already a currency with no job.
- ⛔ Do not raise crystals. They are timer compression and the curve is already too short.
- ⛔ Do not build PvP (map §9).

---

## LANDED 2026-09-04 - THE UNBLOCKED HALF (edit-only lane; lead gates + commits)

Everything below is correct under **BOTH** sides of the troop-cost fork, per this ticket's own
"WHAT IS SAFE TO BUILD MEANWHILE" banner. Nothing here reads, writes or sizes a gold value.

**1. Raids pay WOOD and IRON.** `RaidScoring.ComputeLoot` gained two trailing optional parameters
(`woodBase`, `ironBase`, both defaulting to 0, so every pre-existing caller compiles AND pays exactly
what it paid). They are scaled by the map's five-rung PERFORMANCE ladder in the new
`RaidLootTunables` (fail 18% / 1* 50% / 2* 75% / 3* 100% / 3*+100% razed 110%), then by the camp's
`rewardMultiplier`. Crystals and food keep their original arithmetic byte for byte.
⛔ `ResourceCost.Coins` stays 0, and a regression case fails the build if it ever does not.

> ⚠ **ONE AMBIGUITY IN THE MAP, RESOLVED IN THE OPEN AND NEEDING HER WORD.** §1's table is headed
> *"perfect 3 stars / 100%"* and gives **1,800 wood**, while the ladder in the same section lists
> **3 stars = 100%** AND **3 stars + 100% destruction = 110%**. Read strictly those cannot both be true
> of 1,800. **Taken here as: 1,800 is the BASE, 3 stars pays 100% of it, a total razing pays 1,980.**
> If she meant the other reading, it is two rows on the Command Center: `raid.lootWoodBase` 1636,
> `raid.lootIronBase` 1000. No rebuild.

**2. The free starter army.** `StarterArmyGrant` - a self-installing 0.5 s edge poll on the SAME
`StructureSingleton.IsBuilt("barracks")` predicate every other raid surface reads. Grants 3 free
Footmen through the one roster owner, latched on the monotonic `EverAcquiredItemIds` ledger under
`grant.starter-army`, so **no save-schema bump and no migrator**. Once per save: a rebuilt Barracks
never re-issues the squad.

**3. The six-event funnel (map §11).** `DeNelle.Core.Analytics.RaidFunnel`, on the EXISTING
`EventTracker` -> `/api/events/track` -> Neon rail. No second telemetry path. Each step latches per
install (a funnel counts PLAYERS, not events). The 24h "gold nugget" is computed client-side, emitted
as its own event, and **refuses to fire** on a missing stamp or a backwards device clock rather than
fabricating a conversion. Wired at: `SceneRouter.GoRaid` (attempted + second-within-24h),
`RaidVictoryController.HandleVictory` (won + arms spent), `BarracksProgression.GrantTrainedTroop`
(trained), `StarterArmyGrant` (barracks unlocked), and **both** spend surfaces
(`EconomyService.TrySpend`, `ResourceLedger.TrySpend`).

**4. The four discoverability holes.**
- Game Guide `raids`: "Open Raids from the HUD" -> **"Open Journey, then Raids"** (both canonical twins).
- `combat.raid.single` / `.double` now carry `requiresFeature: "raids"`, and `DailyQuests.FeatureShipped`
  resolves it from `PostureSignals.RaidCapable` - previously that switch returned a flat `true`, so
  authoring the field alone would have looked fixed and changed nothing.
- **The Arena Herald bypass is closed at the DOOR, not at the caller.** The capability gate now sits at
  the top of `RaidSelectionScreen.Open()`, ahead of the army gate. Fixing `ArenaHeraldSpawner:238`
  itself would have closed one door and left the next one to rediscover the bug - and a second
  predicate on a second surface is what WO-1357's header forbids by name.
- The refusal now names the ACTUAL blocker via `PostureSignals.RaidLockCopy` (no Barracks / destroyed
  Barracks / raids off in this build) and does **not** open the training panel for a non-troop blocker.

**8 TUNABLES** registered on the existing rail with the map's values as defaults, all sources in step
(`RemoteTunables.Registry` + `TUNABLE_KEYS` + the generated manifest + the Command Center presentation
+ `docs/PROD022_TUNABLE_FLAGS.md` + `[tunable-defaults]`'s literals): `raid.lootWoodBase`,
`raid.lootIronBase`, `raid.lootFailPct`, `raid.lootOneStarPct`, `raid.lootTwoStarPct`,
`raid.lootThreeStarPct`, `raid.lootPerfectPct`, `raid.starterArmySize`.
⛔ **No `raid.lootGoldBase`, deliberately** - registering one would silently pick the winner of the fork.

**4 NEW SUITES**, registered in `DataRegression.RunAll`, distinct markers:
`RAID_LOOT_CURRENCY_OK` / `RAID_FUNNEL_OK` / `STARTER_ARMY_OK` / `RAID_DISCOVERY_OK`.

### STILL BLOCKED, UNCHANGED
Gold rewards, `costGold`, troop pricing and everything under `Assets/_Modules/Village/Arena/**` were
NOT touched. The gold fence is now an ASSERTION rather than a comment: `[raid-loot-currency]` case (D)
sweeps all 44 star/destruction combinations and fails the build on a single coin.
