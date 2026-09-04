# WORK ORDER 1376 - P2: build retention around the loop

**Status:** READY TO IMPLEMENT — ⛔ **sequenced AFTER WO-1375**
**Silo / Lane:** Retention / Journey navigation + weekly ladder + dungeons + troop defence
**Type:** NEW BEHAVIOUR on existing systems, owner-ruled
**Minted:** 2026-09-04 (CLI)

> ⭐ **THE SPEC IS `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` — THE NORTH STAR MAP.**
> Owner ruled 2026-09-04: *"these findings take presedence"* / *"this is the north star map"*.
> ⛔ **This ticket deliberately does NOT restate the numbers.** Read them there. A reward table
> copied into a work order is the duplicated-state defect this repo has paid for four times
> (CLAUDE.md §2 WO numbers, §5 the assembly table, §16 the R2 verify, WO-1137's fallback catalog).

## THE DELIVERABLE — the map's §10 "P2" list

Weekly **Realm Threat** ladder · Journey **Dungeons** card · first dungeon accessible · **Season Pass
navigation** · **Realm Map navigation** · dungeon rewards · troops participate in wave defence.

## ⭐ JOURNEY BECOMES FIVE CARDS

Quests · Raids · Dungeons · Realm Map · Season. *"Two cards makes Journey look unfinished."*
The deck was designed for four and shipped with two — `PlayerDeckWorkspace.cs:588-624`.

## ⛔ TWO GATES STAND IN THE WAY, BOTH REAL

1. **All six dungeons are FAIL-CLOSED** behind a live `/api/dungeon-status` row
   (`DungeonStatusCatalog.cs:20-48`) — no network, stale cache or bad payload all resolve to Sealed.
   ⚠ **Whether that endpoint currently serves `open` rows is NOT PROVEN.** Verify it or change the
   gate; do not assume.
2. **Season Pass and Realm Map navigation is ENFORCED ABSENT** by
   `PublicNavigationRetirementRegression`. Re-point it.

## ⚠ TROOPS IN WAVE DEFENCE IS THE LARGEST ITEM HERE
Map §9: **ASSIGN DEFENDERS** before a wave, so the army serves offence AND defence and stops being a
raid tax. ⚠ The owner marked it *"Not P0, though."* It is the tail of this ticket, not its head —
**consider splitting it out** rather than letting it block the navigation wins.

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
- ⛔ Do not build PvP (map §9). *"That's a whole dragon."*
