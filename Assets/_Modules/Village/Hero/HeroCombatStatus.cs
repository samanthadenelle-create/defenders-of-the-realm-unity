// =============================================================================
// HeroCombatStatus — player-side combat status timers for the HUD buff/debuff row.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Mirrors EnemyDamageable's status storage (WO-609). Auto-added on the hero rig;
// producers read via CollectActive — presentation never touches this component.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Combat;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Tracks timed buffs/debuffs on the player for the battle HUD row.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroCombatStatus : MonoBehaviour
    {
        private readonly CombatStatusTracker _tracker = new CombatStatusTracker();

        /// <summary>Apply a CC debuff to the hero.</summary>
        public void ApplyStatus(StatusEffect effect, float seconds) => _tracker.Apply(effect, seconds);

        /// <summary>Apply or refresh a named timed buff/debuff.</summary>
        public void ApplyNamed(string id, string label, float seconds, bool isBuff)
            => _tracker.ApplyNamed(id, label, seconds, isBuff);

        /// <summary>Clear a named timed status (e.g. mana draught ended early).</summary>
        public void ClearNamed(string id) => _tracker.ClearNamed(id);

        /// <summary>Collect active statuses for the HUD producer.</summary>
        public void CollectActive(List<ActiveStatusSnapshot> dst, int max = 6)
            => _tracker.CollectActive(dst, max);

        /// <summary>Resolve the live hero's status tracker, auto-adding if absent.</summary>
        public static HeroCombatStatus GetOrAdd(GameObject hero)
        {
            if (hero == null) return null;
            var s = hero.GetComponent<HeroCombatStatus>();
            if (s == null) s = hero.AddComponent<HeroCombatStatus>();
            return s;
        }

        /// <summary>Find Player-tagged hero status component (may be null).</summary>
        public static HeroCombatStatus Current
        {
            get
            {
                GameObject hero = null;
                try { hero = GameObject.FindWithTag("Player"); }
                catch { hero = null; }
                return hero != null ? GetOrAdd(hero) : null;
            }
        }
    }
}