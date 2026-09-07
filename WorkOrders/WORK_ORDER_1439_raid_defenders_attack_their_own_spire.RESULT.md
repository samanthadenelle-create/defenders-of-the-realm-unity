# WO-1439 RESULT - defenders never select their own faction; one faction rule, five call sites

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify and one captured raid run showing the spire at zero garrison damage (AC3).
**Commit:** `32659c0f6` (bundled under a `feat(manage,build)` title; the diff was swept into the Manage commit and
the message does not name it - the `git apply --3way` staging hazard). Gated: `Enemy.cs:2475-2477` carries the
CLI's own at-the-gate CS8967 note. Status never flipped; this RESULT closes the gap after a read-only
re-verification at source on 2026-09-06.
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (18:48), `REGRESSION_OK 414/414` (18:50).

## Acceptance, verified at source
1. **Proven before edit** - the reject tally and the 11,620 / 8,359 counts quoted in the WO are recorded
   permanently in `Core/Combat/IDamageableStructure.cs:35-45` and `Core/Combat/CombatFactionRules.cs:18`.
2. **A defender never selects its own faction** - `CombatFactionRules.MayAttack` (`CombatFactionRules.cs:53`) is
   called at every enemy selection site: `Enemy.cs:2468` (forward probe), `Enemy.cs:2545` (sweep),
   `EnemyBrain.cs:1607,1623,1630,1743,1774`, `DragonBoss.cs:1602`. The seam oracle is a real body:
   `Enemy.DealStructureDamage` (`Enemy.cs:2089-2097`) refuses with `FlowTrace.Fail`. Regression cases D-G at
   `DataRegression.cs:2146-2232`, RED proof stated in-file.
3. **All 18 `IDamageableStructure` implementers declare `Faction`** (13 public, 5 explicit:
   `HealingCaravanMobility.cs:99`, `HeartController.cs:329`, `HeroHealth.cs:1630`, `StoryCompanion.cs:102`,
   `TroopController.cs:349`). `using DeNelle.Core.Combat;` intact everywhere.
4. **The pre-resolve timing window is ruled out** - `RaidSpire.Faction => CombatFaction.Hostile`
   (`RaidSpire.cs:217`) is a constant; the SceneOwnership-derived factions (`WallSegment`, `Gate:170`,
   `Building:151`, `Tower:150`, `ArcaneTower:169`, `ResourceCollector:178`) are expression-bodied and evaluated live;
   `RaidGarrisonSpawner.cs:156` sets `SceneOwnership.SetEnemyOwned(true)` before the garrison spawns.
- [ ] AC3 captured raid run on the post-fix build.

## Findings carried forward (no edit made)
- `Perception/AwarenessSensor.cs:233-239` still filters `IDamageableStructure` on `null + IsAlive` only. It feeds
  awareness escalation, never a damage target, so no friendly fire - but a raid guard stands permanently
  "committed" next to its own spire. Adjacent to WO-1438; take it there.
- `TroopController.cs:754` and `:824` re-implement the faction comparison inline
  (`dmg.Faction != CombatFaction.Hostile`) instead of calling `CombatFactionRules` - the second copy the seam
  exists to prevent. WO-1438's silo.
