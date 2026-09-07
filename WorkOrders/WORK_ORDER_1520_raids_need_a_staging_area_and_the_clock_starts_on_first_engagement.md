# WO-1520: raids spawn the hero INSIDE defender range with the clock already running - a staging area, and a clock that starts on first engagement

**Status:** READY TO IMPLEMENT - P0, owner ruling 2026-09-06 20:26
**Silo:** Village/Troops raid lifecycle - `RaidDeployController`, `RaidScoring`, `RaidGarrisonSpawner`, and the
raid base generator's marker authoring. WO-1437 landed the lifecycle; WO-1436 the posture.
**LANDS AFTER** tonight's WO-1462 / 1463 commit (same files).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1520 -> 1521 in the same edit).

## 1. EVIDENCE

Owner ruling, verbatim:

> "battle for raids should start in some staging area outside of the attack range of everything so time starts
> on first engage. as soon as you spawn in you start dying without having even a second to deploy some troops"

Tonight's captures show exactly that. `raid-no-abilities-2026-09-06.log`, 12:59:47:

```
[Flow:Raid] stars settled: 0 (cleared=False destruction=32 % elapsed=45s/180s ...)
[Flow:Raid] hero death settle: partial loot for 32% razed
```

The hero died **45 seconds into a 180-second raid**. Contact begins within seconds of load:

```
[Flow:Raid] RaidSpire took N (contact)
```

plus the WO-1439 defender fight, immediately on entry. The hero seat is a baked marker inside the base:

```
[Flow:Hero] recover: re-homed carried hero   (0.00, 0.08, -39.00) (seat=baked marker)
```

And the clock is already running before the player has done anything:

```
RaidScoring.cs:715   _elapsed += Time.deltaTime;   // starts on SCENE ENTRY
```

Meanwhile deploying troops takes several taps, because the deploy bar sits over the ability row (WO-1436 sec.2B).
So the player is billed time and taking damage during the seconds they spend trying to deploy.

## 2. FIX SHAPE

1. **A STAGING marker per raid scene**, authored by the raid base generator
   (`Assets/Editor/WallTools/RaidBaseGenerator.cs`), at a point PROVEN outside every defender's
   `AwarenessSensor` radius and every tower's range - computed from the catalog ranges and ASSERTED in the
   generator. Never a magic distance. The carried hero seats there; troops deploy there.
2. **`RaidScoring` starts `_elapsed` and the 180 s clock on FIRST ENGAGEMENT**, not on scene entry: the first
   hero strike, the first troop strike, or the first defender acquiring a target - ONE authority, with a
   permanent `FlowTrace.Step("Raid", "clock started reason=...")`. Until then the HUD reads
   `STAGING - deploy your troops`.
3. **Defenders idle while no attacker is inside their sensor** (no awareness commit) - which WO-1458 and
   WO-1439 already require.
4. **RETREAT from staging costs nothing and pays nothing.**

## 3. WHAT NOT TO DO
- **Do not add a countdown or a grace timer.** She asked for a PLACE, not a delay. A grace timer still spawns
  the player in the fire and just delays the damage.
- Do not move the spire.
- Do not pick the staging distance by eye. If the assertion in the generator cannot be satisfied, that is a
  finding about the base layout - report it, do not soften the assert.

## 4. ACCEPTANCE
- [ ] A regression MEASURING the staging marker's distance against every defender sensor radius and tower
      range in the generated raid base. RED today - the current seat is inside range; state that proof in-file.
- [ ] A source case that `_elapsed` cannot advance before the engagement flag is set.
- [ ] A captured device raid where `clock started reason=` appears AFTER the first deploy, and the hero is
      alive at t=0 with full HP.
- [ ] `REGRESSION_OK n/n` on a fresh log.
