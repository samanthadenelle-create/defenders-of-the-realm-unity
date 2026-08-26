# WORK ORDER 1232 - "Lv 68" on a wave-7 enemy: two call sites still run the RETIRED HP/25 heuristic

**Status:** READY TO IMPLEMENT
**Silo:** HUD / Combat presentation
**Severity:** P1. It also drives the DANGER SKULL, so the threat warning is wrong by the same factor.
**Origin:** Owner felt-test, Seeker build `2026.08.26.342290`, 2026-08-26. Owner verbatim:
***"it died but said it was lvl 68 enemy"***. Captured state: hero **Thrain Lv 5**, **Wave 7**,
Elarion (`tmp/screen-lvl68.png`).

---

## ROOT CAUSE - found at source, no theory required

`Enemy.Level` is the CORRECT value and has been since WO-611 F3. Its own doc comment
(`Enemy.cs:484-489`) states what it replaced:

> the truthful display level for the target frame ("Lv N"). Set in `Configure` from the authored def
> (a STABLE per-archetype band), **replacing the old HUD-side `EnemyLevelStub` HP/25 heuristic that
> read the runtime maxHp and crept upward as wave-scaling inflated it.**

**Two call sites were never migrated and still run the retired heuristic verbatim:**

- `Assets/_Modules/Village/HUD/HudModelHost.cs:223` - `EnemyLevelStub(Enemy e)`
- `Assets/_Modules/Village/Combat/ThreatSkullPlate.cs:70` - `EnemyThreatLevel(Enemy e)`

Both are byte-identical:

```csharp
float maxHp = e.MaxHp > 0.001f ? e.MaxHp : e.Hp;
return Mathf.Max(1, Mathf.RoundToInt(maxHp / 25f));
```

**The arithmetic confirms the report:** 68 x 25 = **1700 HP**, which is what wave-7 scaling inflates
an ordinary enemy to. The label is not reporting a level - it is reporting `maxHp / 25`.

Each site's comment says it "Mirrors" the other so the two "read exactly one value" - and they do
read one value. **It is the wrong one.** This is the duplicated-state drift CLAUDE.md documents
repeatedly (the stale WO number block, the retired dependency table, the hardcoded repo root): the
authority moved to `Enemy.Level`, the copies stayed.

## The second, worse consequence

`ThreatSkullPlate.EnemyThreatLevel` feeds `delta = enemyDifficulty - playerLevel` and the
`LethalDelta` / `RiskyDelta` bands (`ThreatSkullPlate.cs:59-61`). With the heuristic, a routine
wave-7 enemy reads as **63 levels above** a Lv 5 hero, so **every enemy shows a lethal skull** and the
warning carries no information. Fixing the label without fixing the skull leaves half the defect.

`HudModelProducers.cs:481` already carries a comment saying it uses the real level and "not the old
EnemyLevelStub HP/25 heuristic" - so **one consumer was migrated and two were missed.** Confirm there
is no fourth.

## OWNER RULING 2026-08-26 - this may not be a number at all

Owner verbatim, on being shown the root: ***"but lvl 68 versus a lvl 5 seems off"*** ->
***"then remove the level or just but boss or something to tell"***.

**So pointing the two sites at `Enemy.Level` is the FLOOR, not necessarily the answer.** The owner is
open to removing the numeric level entirely in favour of a QUALITATIVE tell - Boss / Elite / ordinary -
which communicates "what am I facing" without inviting the player to do level arithmetic against a
hero level the number was never comparable to.

Implement it in this order:

1. **First, kill the lie.** Both sites must stop deriving a level from `maxHp / 25f`. That is
   non-negotiable and stands regardless of what the presentation becomes.
2. **Then propose the presentation.** Bring back a recommendation with the authored data in hand:
   what per-archetype level bands actually exist, whether a boss/elite flag already exists on the def
   (waves.json authors `boss` and `apexBoss` - check whether the Enemy carries that through), and
   whether a number, a word, or both serves the player best. **The owner decides the final
   presentation** - do not ship a redesign as if it were the fix.
3. If a qualitative tell is the answer, it must be **a WORD the player can read**. The owner is
   red/green colourblind; a skull tinted differently is not a tell. This repo already holds that line
   in `canon-strings.json` (`_raidCooldownNote`: *"EVERY one of these is a WORD the player can read"*).

## Required

1. Both sites read **`Enemy.Level`**. Delete the two heuristic bodies - do not "improve" the divisor;
   there is an authored value and the heuristic must not survive in any form.
2. **Sweep for other consumers** of an HP-derived level. Search for `/ 25f`, `maxHp /`, `EnemyLevelStub`,
   `EnemyThreatLevel` across `Assets/`. Report every hit and its disposition. If a legitimate consumer
   needs a difficulty band that is NOT the level, say so - do not silently repurpose `Level` for it.
3. Re-check the `LethalDelta` / `RiskyDelta` thresholds against REAL levels. They were tuned (if at
   all) against inflated numbers, so they are very likely wrong now. **Report what they are and what
   they now mean; do not retune them without saying so** - threat banding is player-felt and may be an
   owner ruling.
4. Instrument per section 12: a `FlowTrace` line when a target frame resolves a level, naming the
   source, so a future regression of this is one read rather than a felt-test.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression that FAILS on today's tree: configure an enemy with an authored level, inflate its
   `MaxHp` to a wave-scaled value, and assert the displayed level and the threat level BOTH report the
   authored level and are INDEPENDENT of MaxHp. Prove it RED first (WO-1138) and state how.
3. A guard case asserting no `Assets/` source derives a level from `maxHp / 25f`, so this cannot
   silently return a third time.
4. **A DEVICE SCREENSHOT** at 2670x1200 of a wave enemy's target frame showing a sane level next to a
   Lv 5 hero.
5. Owner felt-verifies and CLOSES.

## What NOT to touch

- `Enemy.Level`, `Enemy.Configure`, or the authored per-archetype level bands. That side is correct.
- Wave HP scaling. The inflated HP is intended; reading a LEVEL off it is the defect.
- The danger-skull VISUALS (colour/'art'). The owner is red/green colourblind and the plate's
  non-colour encoding is deliberate - this ticket changes the NUMBER feeding it, nothing else.
