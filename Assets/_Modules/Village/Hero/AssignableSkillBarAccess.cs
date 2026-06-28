// =============================================================================
// AssignableSkillBarAccess — static resolver over the live hero's AssignableSkillBar.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Mirrors HeroLoadoutAccess: a thin scene-free accessor the Skill-Tree ViewModels
// and the battle HUD use to read/write the player's assignable EXTRA skill bar
// without re-finding the hero each call. Caches the live instance (re-resolves on a
// stale cache / hero respawn). The hero is found by the canonical "Player" tag
// (CLAUDE.md §7). If the rig has no AssignableSkillBar yet, this AUTO-ADDS one
// (self-bootstrap — Awake loads the saved bar from PlayerPrefs) so the bar persists
// without wiring HeroControlEnsurer. When no hero exists every call is a safe no-op.
// All Unity-object null checks are explicit (the project lints away ?./?? on UnityEngine.Object).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Static accessor for the live hero's <see cref="AssignableSkillBar"/> (resolved by Player tag).</summary>
    public static class AssignableSkillBarAccess
    {
        private static AssignableSkillBar _cached;

        /// <summary>The live hero's assignable bar, or null when no hero is present. Re-resolves a stale cache.</summary>
        public static AssignableSkillBar Current
        {
            get
            {
                if (_cached != null) return _cached;
                _cached = Resolve();
                return _cached;
            }
        }

        private static AssignableSkillBar Resolve()
        {
            GameObject hero = null;
            try { hero = GameObject.FindWithTag("Player"); }
            catch { hero = null; }
            if (hero == null) return null;

            var bar = hero.GetComponentInChildren<AssignableSkillBar>();
            if (bar == null) bar = hero.AddComponent<AssignableSkillBar>();  // self-bootstrap; Awake loads prefs
            return bar;
        }

        /// <summary>Add <paramref name="abilityId"/> to the first free slot on the live hero (battle-locked).</summary>
        public static bool TryAdd(string abilityId)
        {
            var bar = Current;
            if (bar == null) return false;
            return bar.TryAdd(abilityId);
        }

        /// <summary>Assign <paramref name="abilityId"/> to a specific slot on the live hero (battle-locked).</summary>
        public static bool Assign(int slot, string abilityId)
        {
            var bar = Current;
            if (bar == null) return false;
            return bar.Assign(slot, abilityId);
        }

        /// <summary>Clear a slot on the live hero's bar (battle-locked).</summary>
        public static bool Clear(int slot)
        {
            var bar = Current;
            if (bar == null) return false;
            return bar.Clear(slot);
        }

        /// <summary>The slot index <paramref name="abilityId"/> occupies on the live bar, or -1 when absent / no hero.</summary>
        public static int SlotOf(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return -1;
            var bar = Current;
            return bar == null ? -1 : bar.SlotOf(abilityId);
        }

        /// <summary>True when <paramref name="abilityId"/> is on the bar in any slot.</summary>
        public static bool IsAssigned(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            var bar = Current;
            if (bar == null) return false;
            for (int i = 0; i < AssignableSkillBar.SlotCount; i++)
            {
                string id = bar.AbilityIdForSlot(i);
                if (!string.IsNullOrEmpty(id) &&
                    string.Equals(id, abilityId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>True while a battle is live — bar edits are rejected (see HeroLoadout.EditsLocked).</summary>
        public static bool EditsLocked => HeroLoadout.EditsLocked;
    }
}
