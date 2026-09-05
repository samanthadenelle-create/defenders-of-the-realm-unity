# WORK ORDER 1375 - P1: give raids progression

**Status:** FIXED - in build 2026.09.05.355872, installed on the Seeker 2026-09-04 22:22 (versionCode 355872); its regression suite(s) green on the same tree. Awaiting owner felt-test.
**Silo / Lane:** Economy / raid ladder + Season Pass hooks
**Type:** NEW BEHAVIOUR on existing systems, owner-ruled
**Minted:** 2026-09-04 (CLI)

> ⭐ **THE SPEC IS `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` — THE NORTH STAR MAP.**
> Owner ruled 2026-09-04: *"these findings take presedence"* / *"this is the north star map"*.
> ⛔ **This ticket deliberately does NOT restate the numbers.** Read them there. A reward table
> copied into a work order is the duplicated-state defect this repo has paid for four times
> (CLAUDE.md §2 WO numbers, §5 the assembly table, §16 the R2 verify, WO-1137's fallback catalog).

## THE DELIVERABLE — the map's §10 "P1" list

Enable **Iron Bastion** · the clear-count unlock ladder (3 / 10 / 20 victories) · increasing
difficulty · increasing loot · **raid charges stack to 3** · the first-win daily bonus · raid XP feeds
the **Season Pass**.

## ⭐ TWO THINGS ARE ALREADY BUILT AND MERELY UNREACHED

1. **`RaidBase_IronBastion.unity` is baked and tooled** — and is in NEITHER `scene-configs.json` NOR
   Build Settings. ⚠ Adding a scene to Build Settings is a `ProjectSettings` change, not a data edit.
2. **The Season Pass is 30 authored tiers with no door.** ⛔ Its absence is ENFORCED by
   `PublicNavigationRetirementRegression` — **re-point that oracle, never delete it** (the WO-1159
   precedent: a ruling moved, so the pin moved and got STRICTER).

## ⭐ RAID ORDERS ARE THE RETENTION CHANGE, NOT THE LADDER

Map §5: charges **stack to 3**, one per 4h. *"Now somebody sleeping or working isn't punished."*
A returning player meets **3 Raid Orders Ready** and instant activity, instead of a countdown.

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
- ⛔ Do not invent a raid progression currency — Season XP is the ladder (map §6).
- ⛔ Do not delete the navigation-retirement oracle.
