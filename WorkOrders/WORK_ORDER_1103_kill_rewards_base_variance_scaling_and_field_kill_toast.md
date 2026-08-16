# WORK ORDER 1103 - Kill rewards: base value + bounded variance + kill-count scaling; field-kill earned toast

**Status:** IMPLEMENTED 2026-08-16 - pending PO felt-verify (commit `8b1d1a649`); see RESULT
**Minted:** 2026-08-16 (orchestrator; banner bumped 1103 -> 1104 in the same edit)
**Silo:** Combat economy / rewards
**Source:** OWNER DIRECTIVE 2026-08-16 (verbatim): *"in a battle arena each enemy should have a
base value with some random on it to make range bound, so if 4 enemies killed the xp and rewards
are more. Separately on the outside world if you can kill enemies from a distance before they
start battle arena combat, then there should be some type of notification of what they got."*

---

## 1. Audited ground truth (read-only audit 2026-08-16, all cites verified at source)

How rewards are actually created today:

- **Arena battle-level payout** (`BattleArena.cs:2734-2792`):
  `XP = round((20 + 8*family + 4*threat) * mult)`, `wood = round((10+4*threat)*mult)`,
  `iron = round((4+2*threat)*mult)`, where `mult` = star rating 1.00/1.25/1.50
  (`BattleStarRating.cs:56-61`) and **`family = p.EnemyIds.Length` (`:2738`) - the encounter
  ROSTER, not kills**. `HandleEnemyDied` (`:1863-1871`) keeps NO kill counter.
- **Per-enemy stream stacks on top**: every arena body is a real `Enemy` with a SYNTHESIZED def
  (`BattleArena.cs:1597-1603`): `XpReward = round(14*t)`, `GlimmerReward = round(3*t)`,
  `CoinReward` unset -> gold falls back to `max(4, round(XpReward*0.4))` (`Enemy.cs:2752-2756`).
  Plus a THIRD stream: `ProgressionManager.Distribute` HP-derived XP (`ProgressionManager.cs:110-138`).
  The victory SUMMARY reports only the battle-level slice (`:2242-2250`) - on-screen under-reports.
- **Zero randomness** anywhere in XP/gold: all formulas deterministic. Only randomness = the ~4%
  gear roll (`BattleArena.cs:2806-2814`, star bonus clamped away by `maxChance=0.04`) and
  `LootTableCatalog.Roll`. `enemies.json` has `xpReward`/`coinReward` but NO variance field.
- **`enemies.json` rewards are DEAD for arena + overworld** - both synthesize defs in code
  (`BattleArena.cs:1590-1591` admits it; `OverworldEncounterSpawner.cs:910`). The catalog rows
  are only read for wave/camp/outpost spawns (`Enemy.cs:2739/2754`).
- **Overworld ranged field-kill DOES pay** (deliberate: `RangedHitsEngage=false`,
  `OverworldEncounterSpawner.cs:1124`, rationale `:1091-1095`) via the same `Enemy.Die` grants +
  `ItemDropWatcher` loot rolls (`ItemDropWatcher.cs:95-104`). BUT: leader-only payout
  (`XpReward = round(14*bodies*levelScale)` on the leader, followers = 0, `:929/:944`), and
  `ConsumePack` (`:1435-1444`) destroys followers so their loot never rolls.
- **NO notification of what was earned**: `Enemy.cs:2739` discards `AddXp`'s return;
  `EconomyService.AddCoins` (`EconomyService.cs:608-619`) is silent; only a LEVEL UP label exists
  (`ProgressionManager.cs:150-151`).

### Two proven payout BUGS (fix in this WO)
- **B-1**: low-level spawn cap (`BattleArena.cs:1399-1403`) caps spawned `n`, but payout reads the
  UNCAPPED `p.EnemyIds.Length` (`:2738`) - a capped fight pays for enemies never spawned.
- **B-2**: the 5% bonus boss (`:1519`) is outside `EnemyIds` - killing it adds nothing to `family`.

### Gap table vs owner expectations
| Expectation | Status | Missing piece |
|---|---|---|
| A1 per-enemy BASE value | PARTIAL | dead for arena/overworld (synthesized defs ignore catalog) |
| A2 bounded RANDOM variance | ABSENT | no variance anywhere; no data field |
| A3 total scales with KILLS | PARTIAL | per-enemy stream stacks, but battle payout scales on roster (wrong twice, B-1/B-2) and the SUMMARY hides the per-enemy stream |
| B1 field kill pays | MET | leader-only caveat; follower loot destroyed by ConsumePack |
| B2 field kill notifies | ABSENT | no call site at all |

## 2. Spec

1. **Base + bounded variance (data-driven).** Add `rewardVariance` (fraction, e.g. 0.15 =
   +/-15%) to the enemy rows in BOTH canonical `enemies.json` mirrors + `EnemyDef`. At grant time
   roll `value * (1 + Random.Range(-v, +v))`, rounded, in ONE shared helper (single authority;
   suggest a static on `EnemyDef` or a small `RewardRoll` util in Village). Default 0 when the
   field is absent (no behavior change for un-migrated rows).
2. **Arena reads the catalog.** Replace the synthesized `XpReward = round(14*t)` in
   `BattleArena.cs:1597-1603` with a catalog lookup by the encounter's enemy id (the follow-up
   the code comment at `:1590-1591` already promises), keeping the threat scale as a multiplier.
   Set `CoinReward` from the catalog row too.
3. **Kill-count scaling, honestly.** Count actual kills in `HandleEnemyDied` (`:1863`), pay
   `GrantWinReward` from KILLS not `EnemyIds.Length` (fixes B-1), include the bonus boss (fixes
   B-2), and make the victory SUMMARY report the TOTAL banked (battle slice + per-enemy stream)
   so 4 kills visibly > 1 kill.
4. **Field-kill toast (B2).** At the `Enemy.Die` grant choke point (`Enemy.cs:2731-2756`), when
   the kill happens OUTSIDE an active arena (no `BattleInProgress`), emit ONE aggregate
   notification: `DamageNumberSpawner.SpawnLabel` ("+N XP  +M gold" at the corpse,
   `DamageNumberSpawner.cs:140` - the LEVEL UP precedent) and/or `ElarionUiKit.ShowToast`
   (`ElarionUiKitConformance.cs:393`, precedent `BankOverflowToastPresenter.cs:78`). NO new
   notification system. Aggregate a leader-pack payout into one label, not per-follower spam.
5. **Overworld follower equity (owner-visible fairness):** either keep leader-carries-the-pack
   (document it in the toast: "pack bounty +N") or move to per-body payouts - OWNER CALL; default
   to keeping leader-carry + toast wording, smallest change.
6. **Regression (the bug becomes a test).** Extend `EnemyRewardRegression` to drive `Enemy.Die`
   (its own header `:11-14` admits it currently re-implements the grants) and assert: N kills sum
   to ~N x base within the variance band; capped-spawn arena pays kills not roster; bonus boss
   counts; a field kill emits the notification call (source-lint or seam probe).
   Extend `ArenaCombatOracle` (`:147-167`) to assert SUMMARY xp tracks the kill count.

## 3. What NOT to touch
- `ProgressionManager.Distribute`'s shared-XP stream (separate design surface; do not triple-dip
  changes in one WO).
- The gear-drop chance constants (owner balance surface).
- Wisdom stays zeroed (WO-763 ruling, `BattleArena.cs:2764`).

## 4. Files
`Enemy.cs:2731-2756`; `BattleArena.cs:1399-1403, 1519, 1597-1603, 1863-1871, 2242-2250,
2734-2792`; `OverworldEncounterSpawner.cs:910-946, 1435-1444`; both `enemies.json` mirrors +
`EnemyDef`; `EnemyRewardRegression.cs`; `ArenaCombatOracle.cs`.

## 5. Acceptance
- 4-kill arena banks and DISPLAYS measurably more than 1-kill (trace + SUMMARY line).
- Same fight twice yields different-but-range-bound totals (variance live).
- A ranged field kill shows exactly one earned-rewards label/toast naming the amounts.
- All existing reward regressions green; new cases red on revert (falsified).
