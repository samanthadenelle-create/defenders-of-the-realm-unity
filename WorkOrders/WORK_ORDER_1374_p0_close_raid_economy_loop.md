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
