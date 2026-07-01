// =============================================================================
// HeroCombatEngagement — "the hero is in a live real-time fight RIGHT HERE"
// battle-lock source (dungeon / outpost in-scene combat fix, 2026-06-30).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// THE BUG THIS CLOSES (owner F8 "[Dungeon] why am i doing 0 damage"):
//   Every hero OUTGOING attack path is gated on BattleLock.IsInBattle():
//     - PlayerAttackController.Update / TriggerBasicAttack  (melee swing)
//     - HeroAbilityInput.Update                             (primary Q + slots)
//   BattleLock is only raised by the STAGED battle owners that register a probe:
//     ATBCombatManager, ArenaMode, BattleArena.
//   The overnight dungeon/outpost chain (DungeonChainBuilder) instead places
//   OutpostEnemyGroupSpawner markers that self-spawn HOLLOW skeletons DIRECTLY
//   into the WALKABLE scene (heart==null, EnemyBrain.SetHeroOnlyTarget(true)) and
//   are fought in place — NO BattleArena is ever staged for them, so
//   BattleLock.IsInBattle() stays FALSE and the hero's swings/casts are all
//   suppressed. The ONLY damage that reached those enemies was the passive
//   Retaliation-reflect talent (HeroHealth.ApplyReflect -> Enemy.TakeDamageFrom,
//   dealtByHero=false), which is why the break-log showed dealtByHero=True=0 and
//   dealtByHero=False=48 tiny reflect hits. It is NOT a faction bug —
//   EnemyDamageable.Faction is always Hostile; the attacks never fired.
//
// THE FIX (correct layer, general — not a dungeon-specific hack):
//   A live in-scene combatant that is fighting the hero IS a battle. This static
//   is a reference-counted set of currently-engaged combatants; it registers a
//   single BattleLock probe that reports IN-BATTLE while the set is non-empty. An
//   Enemy that is a hero-only, heart-less duelist raises/clears its own membership
//   as the hero enters/leaves its aggro range (see Enemy.UpdateHeroCombatEngagement).
//   Staged battles (BattleArena/ATB/Arena) keep their own probes — this only adds
//   the missing "in-place real-time fight" source, so the hero can actually attack
//   the dungeon/outpost hollows. Scoped to hero-only combatants, so it never trips
//   on overworld roamers (which pop the arena) or village-wave siege enemies.
//
// Reset-safe: on a domain reload every static below resets and BattleLock's probe
// list is cleared too; the first SetEngaged() call re-registers the probe.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Reference-counted "the hero is in a live in-scene fight" signal that feeds
    /// <see cref="BattleLock"/>. Combatants call <see cref="SetEngaged"/> as they
    /// begin/end engaging the hero; <see cref="BattleLock.IsInBattle"/> then reports
    /// true while ANY combatant is engaged. See the file header for the full RCA.
    /// </summary>
    public static class HeroCombatEngagement
    {
        // The set of currently-engaged combatants (token = the Enemy component). A set
        // (not a counter) makes SetEngaged idempotent and self-correcting: a token can
        // never be double-counted, and a missed clear is bounded to one stale token.
        private static readonly HashSet<object> _engaged = new HashSet<object>();
        private static bool _probeRegistered;

        // Register the single BattleLock probe exactly once, lazily (so nothing is wired
        // until the first real in-scene combatant engages). BattleLock dedups by delegate
        // reference, and both statics reset together on a domain reload, so this is safe.
        private static void EnsureProbe()
        {
            if (_probeRegistered) return;
            _probeRegistered = true;
            BattleLock.RegisterProbe(() => _engaged.Count > 0);
        }

        /// <summary>
        /// Mark <paramref name="token"/> (an in-scene combatant) as engaging the hero
        /// (<paramref name="engaged"/> true) or disengaged (false). While at least one
        /// combatant is engaged, <see cref="BattleLock.IsInBattle"/> reports true so the
        /// hero's attack input is live. Idempotent; null-safe.
        /// </summary>
        public static void SetEngaged(object token, bool engaged)
        {
            if (token == null) return;
            EnsureProbe();
            if (engaged) _engaged.Add(token);
            else _engaged.Remove(token);
        }

        /// <summary>True while at least one in-scene combatant is engaging the hero.</summary>
        public static bool AnyEngaged => _engaged.Count > 0;

        /// <summary>Number of combatants currently engaging the hero (diagnostics).</summary>
        public static int EngagedCount => _engaged.Count;
    }
}
