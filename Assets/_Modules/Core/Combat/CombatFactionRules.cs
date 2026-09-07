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
            return Decide(attacker, target.Faction, target.IsAlive);
        }

        /// <summary>
        /// WO-1438 — the same rule for the BODY contract. <see cref="IDamageable"/> and
        /// <see cref="IDamageableStructure"/> are SEPARATE interfaces (neither extends the
        /// other), so the attacker-side selectors that talk in <see cref="IDamageable"/> could
        /// not reach the structure-shaped overload above and were re-implementing
        /// <c>Faction != CombatFaction.Hostile</c> inline. <c>TroopController.NearestHostile</c>
        /// stopped copying it today; <c>DeNelle.Pets.Pet</c>'s hunt loop is the REMAINING copy
        /// and was deliberately not touched by WO-1438's lane — it is the next call site to
        /// convert, not a claim that it already is.
        ///
        /// ⚠ OVERLOAD TRAP, stated so nobody has to rediscover it: passing a CONCRETE type that
        /// implements BOTH interfaces makes this call ambiguous and will not compile. That is
        /// deliberate and it is safe — a loud compile error, never a silent wrong answer. Call
        /// it through an <see cref="IDamageable"/>- or <see cref="IDamageableStructure"/>-typed
        /// reference, which is what every selection loop already holds.
        ///
        /// The dual implementers today are <c>WallSegment</c> and the other raid/village
        /// structures that also carry a body contract; <c>Tower</c> is NOT one (it implements
        /// only <see cref="IDamageableStructure"/>). Checked at the time of writing: every
        /// existing call site — Enemy.cs:2090/2468/2545, EnemyBrain.cs:1607/1623/1630/1743/1774,
        /// DragonBoss.cs:1602/1604, AwarenessSensor.cs:242 — passes an interface-typed variable
        /// or a Tower, so none of them became ambiguous when this overload was added.
        /// </summary>
        public static bool MayAttack(CombatFaction attacker, IDamageable target)
        {
            if (target == null) return false;
            return Decide(attacker, target.Faction, target.IsAlive);
        }

        /// <summary>
        /// The ONE comparison, in one place, so the two overloads above can never drift apart.
        /// </summary>
        private static bool Decide(CombatFaction attacker, CombatFaction targetFaction, bool alive)
        {
            if (!alive) return false;
            return targetFaction != attacker;
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

        /// <summary>
        /// WO-1438 — the body-contract twin of <see cref="IsFriendlyFire(CombatFaction,IDamageableStructure)"/>.
        /// Deliberately liveness-blind, exactly like it: a CLASSIFICATION question ("is this
        /// thing on my side?") is not the same question as an ATTACKABILITY question, and a
        /// selector that conflates them silently re-classifies a corpse. See the overload trap
        /// noted on <see cref="MayAttack(CombatFaction,IDamageable)"/>.
        /// </summary>
        public static bool IsFriendlyFire(CombatFaction attacker, IDamageable target)
        {
            return target != null && target.Faction == attacker;
        }
    }
}
