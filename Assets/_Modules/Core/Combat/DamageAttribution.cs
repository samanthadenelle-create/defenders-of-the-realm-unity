// =============================================================================
// DamageAttribution — per-target damage ledger for kill-XP attribution.
// -----------------------------------------------------------------------------
// Damage dealers (hero abilities, pets, towers) call Record(target, attackerId,
// amount) right after they land a hit. When the target dies its killer drains
// the ledger and shares XP by contribution + a kill-credit bonus to the top
// damager ("Smart Shared XP": damage share + kill credit).
//
// Keyed by the target OBJECT REFERENCE (the IDamageable / Enemy instance), so a
// dealer's `foe` and the dying enemy's `this` resolve to the same bucket — the
// same component instance whether typed as IDamageable or Enemy hashes equal.
//
// Lives in DeNelle.Core so BOTH DeNelle.Village (hero, enemies) and DeNelle.Pets
// can write to it without referencing each other (module isolation). Buckets are
// tiny and short-lived — one per living enemy, removed on Drain/Forget, and the
// whole ledger is Cleared on scene load to drop anything a non-death teardown left.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Combat
{
    /// <summary>Tracks who dealt how much damage to each combat target, for kill-XP sharing.</summary>
    public static class DamageAttribution
    {
        // target -> (attackerId -> cumulative damage dealt to that target).
        private static readonly Dictionary<object, Dictionary<string, float>> s_ledger =
            new Dictionary<object, Dictionary<string, float>>();

        /// <summary>Records <paramref name="amount"/> damage dealt to <paramref name="target"/> by <paramref name="attackerId"/>.</summary>
        public static void Record(object target, string attackerId, float amount)
        {
            if (target == null || string.IsNullOrEmpty(attackerId) || amount <= 0f) return;
            if (!s_ledger.TryGetValue(target, out var byAttacker))
            {
                byAttacker = new Dictionary<string, float>();
                s_ledger[target] = byAttacker;
            }
            byAttacker.TryGetValue(attackerId, out float cur);
            byAttacker[attackerId] = cur + amount;
        }

        /// <summary>
        /// Returns and removes the per-attacker damage for <paramref name="target"/>
        /// (call once, when it dies). Null if nothing was recorded against it.
        /// </summary>
        public static Dictionary<string, float> Drain(object target)
        {
            if (target == null) return null;
            if (s_ledger.TryGetValue(target, out var byAttacker))
            {
                s_ledger.Remove(target);
                return byAttacker;
            }
            return null;
        }

        /// <summary>Drops a target's ledger without reading it (e.g. it breached the Heart, no kill-XP).</summary>
        public static void Forget(object target)
        {
            if (target != null) s_ledger.Remove(target);
        }

        /// <summary>Clears the whole ledger — called on scene load to drop stale buckets.</summary>
        public static void Clear() => s_ledger.Clear();
    }
}
