// =============================================================================
// HeroLoadoutAccess — static resolver over the live hero's HeroLoadout component.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// HeroLoadout is a per-hero MonoBehaviour (resolved by GetComponent on the rig),
// not a singleton. The skill-tree + loadout ViewModels need a stable, scene-free
// way to read/write it without re-finding the hero each call site. This thin
// static caches the live instance (re-resolves if the hero respawns / the cache
// goes stale) and exposes the two operations the VMs use: IsEquipped(abilityId)
// and Equip(slot, abilityId). All Unity-object null checks are explicit (the
// project lints away ?./?? on UnityEngine.Object).
//
// The hero is found by the canonical "Player" tag (CLAUDE.md §7); the loadout
// lives in its children (HeroControlEnsurer adds it to the rig). When no hero /
// no loadout is present every call is a safe no-op (false / no change) so the
// panels still render — they just show an empty loadout until a hero exists.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Static accessor for the live hero's <see cref="HeroLoadout"/> (resolved by Player tag).</summary>
    public static class HeroLoadoutAccess
    {
        private static HeroLoadout _cached;

        /// <summary>The live hero's loadout, or null when no hero/loadout is present. Re-resolves a stale cache.</summary>
        public static HeroLoadout Current
        {
            get
            {
                if (_cached != null) return _cached;
                _cached = Resolve();
                return _cached;
            }
        }

        private static HeroLoadout Resolve()
        {
            GameObject hero = null;
            try { hero = GameObject.FindWithTag("Player"); }
            catch { hero = null; }
            if (hero == null) return null;
            return hero.GetComponentInChildren<HeroLoadout>();
        }

        /// <summary>True when <paramref name="abilityId"/> is equipped in any W/E/R slot.</summary>
        public static bool IsEquipped(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            var lo = Current;
            if (lo == null) return false;
            return SlotOf(lo, abilityId).HasValue;
        }

        /// <summary>The slot <paramref name="abilityId"/> is equipped in, or null when not equipped.</summary>
        public static AbilitySlot? SlotOf(HeroLoadout lo, string abilityId)
        {
            if (lo == null || string.IsNullOrEmpty(abilityId)) return null;
            foreach (var slot in new[] { AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
            {
                string id = lo.AbilityIdForSlot(slot);
                if (!string.IsNullOrEmpty(id) &&
                    string.Equals(id, abilityId, System.StringComparison.OrdinalIgnoreCase))
                    return slot;
            }
            return null;
        }

        /// <summary>
        /// Equip <paramref name="abilityId"/> into <paramref name="slot"/> on the live hero.
        /// Returns HeroLoadout.Equip's result (false when no hero / Q / duplicate / unchanged
        /// / battle-locked).
        /// </summary>
        public static bool Equip(AbilitySlot slot, string abilityId)
        {
            var lo = Current;
            if (lo == null) return false;
            return lo.Equip(slot, abilityId);
        }

        /// <summary>Assign to a specific slot on the live hero (battle-locked alias of Equip).</summary>
        public static bool Assign(AbilitySlot slot, string abilityId)
        {
            var lo = Current;
            if (lo == null) return false;
            return lo.Assign(slot, abilityId);
        }

        /// <summary>Add to the first free W/E/R slot on the live hero (battle-locked).</summary>
        public static bool TryAdd(string abilityId)
        {
            var lo = Current;
            if (lo == null) return false;
            return lo.TryAdd(abilityId);
        }

        /// <summary>True while a battle is live — loadout edits are rejected (see HeroLoadout.EditsLocked).</summary>
        public static bool EditsLocked => HeroLoadout.EditsLocked;
    }
}
