# WO-1530 RESULT - the formula is read at source and traced; the 15-vs-10 hypothesis is NOT proven, and no number was changed

**Status:** MEASURED AT SOURCE - uncommitted in the working tree as of 2026-09-06 21:45, awaiting the wave-two
gate. The captured-raid half is open.
**Commit:** none. Edit-only lane, no Unity.
**Files:** `Village/World/Camps/GarrisonStatBlocks.cs:166-181` (the trace),
`Village/World/Camps/RaidGarrisonSpawner.cs:295,355` and `Village/World/Camps/GarrisonController.cs:307` (the three
call sites), `Assets/Editor/Regression/CombatAtbRegression.cs` (Check H3, `CheckEnemyScaleTracePresent`).
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and the owner's direction arrived 20:33;
`Builds/cg-aab.log` (20:54) is RED (42x `CS0103`, the Manage lane's half-written suites).

## 1. The formula, quoted from source

Every raid/garrison defender is built by `GarrisonStatBlocks`. `Enemy.SetBaseStats` / `ApplyDifficulty` are called
ONLY by `WaveManager` (`WaveManager.cs:2239,2468`), so the garrison path has no hidden fifth multiplier.

1. **Built block** - `BuildTypedDef` (`:116`) returns a HARDCODED per-id block via `BuildGenericDef` (`:183`),
   folding `GlobalDifficultyMult = 1.2f` (`:38`) into Hp (`:194`) and ContactDamage (`:196`).
2. **Level fold** - `ApplyLevelScale` (`:94-111`): `hpScale = 1 + 0.08*over`;
   `dmgScale = 1 + 0.04*min(over,10) + 0.02*min(max(0,over-10),10)`. So **HP +8%/level uncapped; damage +4%/level
   for 2-11, +2% for 12-21, then FLAT** (dmgScale maxes at 1.60). Height `*(1 + 0.012*over)`, XP `+3*over`.
3. **Per-config difficulty** - `RaidGarrisonSpawner.FoldDifficulty` (`:421-426`, multiplies at `:424-425`).
4. **Boss only** - `RaidGarrisonSpawner.cs:291-293`, `bossHpMult`/`bossDamageMult`, each floored at 1.

The level itself is `int enemyLevel = Mathf.Max(g.baseEnemyLevel, playerLevel + g.levelOffset)`
(`RaidGarrisonSpawner.cs:166`) - **camp level is the FLOOR, not the level**; a high-level player raises every
defender.

## 2. THE FINDING - the audit and the spawner read different sources

`docs/RAID_BALANCE_AUDIT_2026-09-06.md:187` lists the Orc Berserker at 117 HP / 10 damage - the `enemies.json`
values verbatim. **The raid spawn path never reads that row.** `BuildTypedDef` hardcodes `260f / 13f` for
`orc-berserker` (`GarrisonStatBlocks.cs:122`); only `orc-raider` reads the shared roster (`:125-135`). So every
damage and time-to-kill figure in the audit's A.3 table is computed against numbers that are not in the game.
`CombatAtbRegression` Check H already names this two-table divergence for `orc-raider` and tags it
`[FAIL-BY-DESIGN]` (`:750-793`) - a known, unclosed split. Worked example, `raider_camp_small` at level 3,
difficulty 1.0: authored 260/13 -> x1.2 = 312/15.6 -> level 3 (`over`=2, hp 1.16, dmg 1.08) = **361.9 HP / 16.85
damage**. **Leading hypothesis for the observed 15, NOT PROVEN:** 13 x 1.2 = 15.6, flooring to 15 on a HUD - which
needs NO level scaling at all. A competing path is level scaling plus hero mitigation (`Enemy.cs:2187` applies
`mitigated`, not raw `ContactDamage`). The trace decides it. **No number was changed** (sec.3).

## 3. Comment-vs-code mismatches found while reading (report only)

`GarrisonStatBlocks.cs:93` says "~5% contact damage"; `:104` is 4% then 2%. `:36` documents `1.0 = current live
feel`; `:38` is `1.2f`. `BuildTypedDef(string id, int level)` (`:116`) ignores its `level` parameter entirely, and
its `case "troll"` (`:121`) hardcodes threat 2. **`ogre` has no case arm** (`:118-147`) yet the Broken Garrison
asks for 2 - they fall to `default`, a `LogWarning` plus a generic 220/11 brute (`:144-146`).

## 4. Acceptance

- [x] The formula quoted from source with file:line - sec.1.
- [ ] The spawn trace lands and a captured raid's lines are pasted - the trace EXISTS
      (`GarrisonStatBlocks.TraceSpawnScale`, `FlowTrace.Step("EnemyScale"` at `:170`, three call sites, pinned by
      `CombatAtbRegression` Check H3 so removal fails the suite). **No raid captured**; the line shape in the
      ticket's sec.5.5 is explicitly labelled illustrative.
- [ ] The 15-vs-10 discrepancy explained or recorded as unexplained - **recorded as UNPROVEN** (sec.2), which the
      ticket accepts as an outcome. Not closed.
- [ ] The formula written into the audit doc - **OPEN**; its A.3 table needs correcting to the spawn-path numbers
      at the same time.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 5. Owed

One captured raid read for `[Flow:EnemyScale]`, which confirms or refutes sec.2's hypothesis; then the audit doc
update. Per the owner's order of operations this precedes the Hard/Extreme balance pass - nothing tunes until the
lines are read.
