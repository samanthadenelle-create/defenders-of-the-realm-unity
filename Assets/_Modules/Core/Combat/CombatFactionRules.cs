// =============================================================================
// CombatFactionRules — the ONE place that answers "may this attacker hit that?".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// WHY THIS FILE EXISTS (WO-1439, 2026-09-06)
//   A raid garrison spent an entire raid destroying the RaidSpire it exists to guard.
//   Measured, not inferred:
//     [Flow:EnemyAggro] raidguard-raider_camp_small-0: ProbeForStructure hit 'RaidSpire'
//                       -> stopping agent to attack                        (x11,620)
//     [Flow:EnemyAggro] raidguard-raider_camp_small-0: sweep OverlapSphere r=3.0m
//                       colliders=2 -> accepted=1
//                       rejected[null=0,noStructComp=1,dead=0,hero=0] nearest=RaidSpire
//   That second line is the proving one: the reject tally ENUMERATES every filter the
//   sweep had — null, no-component, dead, is-it-the-hero — and faction is not among them.
//   The spire passed because nothing ever asked whose it was. 8,359 of those hits landed
//   AFTER `[Flow:World] SceneOwnership resolved 'RaidBase_raider_camp_small' -> Enemy-owned
//   (IsEnemyOwned=True)`, so the ownership machinery was live and correct and the defect is
//   squarely the missing test.
//
// THE RULE, stated once: NO ACTOR MAY DAMAGE AN ASSET OF ITS OWN FACTION.
//
// ⛔ DO NOT COPY THIS COMPARISON INTO A CALL SITE. There are four selection sites in the
// enemy stack alone (Enemy.ProbeForStructureForward, Enemy.SweepForNearestStructure,
// EnemyBrain.ChooseTarget's overlap scan, EnemyBrain.FindNearestStructure) plus the damage
// sink. Four hand-copies of one predicate is the duplicated-state failure this repo has
// already paid for four separate times (CLAUDE.md §2 stale WO block, §5 retired dependency
// table, §8 restated constants, §16 the inlined R2 push/verify pair that DRIFTED). One
// function, called everywhere.
//
// ⛔ AND DO NOT SPECIAL-CASE A TARGET BY NAME OR ID. "Is it the spire?" leaves every wall,
// tower and building in exactly the same state while looking fixed. The defect was a
// missing CONCEPT.
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Friend-or-foe arbitration for combat target selection. Pure, allocation-free and
    /// side-effect-free so a selection loop can call it per candidate per frame, and so a
    /// regression can assert it directly.
    /// </summary>
    public static class CombatFactionRules
    {
        /// <summary>
        /// True when <paramref name="attacker"/> is allowed to select / damage
        /// <paramref name="target"/>: the target must exist, still be alive, and belong to a
        /// DIFFERENT faction. A null or dead target is not attackable, so a caller can use
        /// this as the whole guard rather than chaining it after its own null/alive checks.
        /// </summary>
        /// <param name="attacker">The striking actor's own faction.</param>
        /// <param name="target">The candidate structure/body. Null is handled.</param>
        public static bool MayAttack(CombatFaction attacker, IDamageableStructure target)
        {
            if (target == null) return false;
            if (!target.IsAlive) return false;
            return target.Faction != attacker;
        }

        /// <summary>
        /// True when <paramref name="target"/> is on the attacker's OWN side — the
        /// friendly-fire condition. Separate from <see cref="MayAttack"/> because the
        /// instrumentation needs to name WHY a candidate was rejected (same-faction is a
        /// different story from dead or out of range), and because the damage-sink oracle
        /// asserts on exactly this without caring about liveness.
        /// </summary>
        public static bool IsFriendlyFire(CombatFaction attacker, IDamageableStructure target)
        {
            return target != null && target.Faction == attacker;
        }
    }
}
