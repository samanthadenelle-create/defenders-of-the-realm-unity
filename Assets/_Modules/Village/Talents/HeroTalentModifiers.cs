// =============================================================================
// HeroTalentModifiers - the talent -> ability-stat bridge (WO-36, stat half).
// -----------------------------------------------------------------------------
// Class ability BINDING (which Q/W/E/R a hero casts) already works via
// HeroAbilities.SetHeroClass + AbilityCatalog. This file closes the second half
// of WO-36: unlocked skill-tree talents now actually move ability numbers.
//
// It reads the live unlocked-node set from WisdomCurrencyService.Instance and
// the per-hero tree from HeroTalentCatalog, then folds the per-node DamageBonus
// / CdReduction values (added to HeroTalentNodeDef) into two multipliers:
//
//   DamageMultiplier(class)   = 1 + sum(DamageBonus over unlocked class nodes)
//   CooldownMultiplier(class) = 1 - sum(CdReduction over unlocked class nodes)
//
// HeroAbilities multiplies def.Damage by the first and def.Cooldown by the
// second. Class-wide (every Q/W/E/R sees the same scalar) for this pass;
// per-slot targeting is a later refinement.
//
// MODULE ISOLATION: everything here lives in DeNelle.Village.Talents, the same
// DeNelle.Village asmdef as HeroAbilities and WisdomCurrencyService, so these
// are plain static calls - no reflection bridge needed.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village.Talents
{
    /// <summary>
    /// Translates a hero's unlocked talent nodes into ability-stat multipliers.
    /// Stateless + defensive: with no service, no tree, or no unlocked nodes it
    /// returns the identity multiplier (1f), so abilities behave exactly as the
    /// pre-talent baseline until the player actually learns a stat node.
    /// </summary>
    public static class HeroTalentModifiers
    {
        // Sanity clamps so a future content edit can't make a hero do absurd
        // damage or fire with effectively zero cooldown.
        private const float MaxDamageMultiplier = 3f;     // +200% damage ceiling
        private const float MinCooldownMultiplier = 0.4f; // at most -60% cooldown

        /// <summary>
        /// 1 + the summed DamageBonus of every unlocked node belonging to
        /// <paramref name="heroClass"/>. 1f when nothing applies. Clamped to a
        /// sane ceiling.
        /// </summary>
        public static float DamageMultiplier(string heroClass)
        {
            float sum = SumOverUnlocked(heroClass, additive: true);
            return Mathf.Clamp(1f + sum, 1f, MaxDamageMultiplier);
        }

        /// <summary>
        /// 1 - the summed CdReduction of every unlocked node belonging to
        /// <paramref name="heroClass"/>. 1f when nothing applies. Clamped so a
        /// cooldown can never drop below 40% of its base.
        /// </summary>
        public static float CooldownMultiplier(string heroClass)
        {
            float sum = SumOverUnlocked(heroClass, additive: false);
            return Mathf.Clamp(1f - sum, MinCooldownMultiplier, 1f);
        }

        /// <summary>
        /// Walks the hero's tree once, summing DamageBonus (additive: true) or
        /// CdReduction (additive: false) across the nodes the player has unlocked.
        /// </summary>
        private static float SumOverUnlocked(string heroClass, bool additive)
        {
            if (string.IsNullOrEmpty(heroClass)) return 0f;

            var service = WisdomCurrencyService.Instance;
            if (service == null) return 0f;

            IReadOnlyCollection<string> unlocked = service.Unlocked;
            if (unlocked == null || unlocked.Count == 0) return 0f;

            var tree = HeroTalentCatalog.GetTree(heroClass.Trim().ToLowerInvariant());
            if (tree == null || tree.Nodes == null) return 0f;

            // Node ids are hero-prefixed (e.g. "mage.t1a-..."), and the unlocked
            // set is a single shared pool, so the prefix is what segments a class's
            // nodes from another hero's. GetTree already returns only this class's
            // nodes, but we still confirm the id prefix as a belt-and-braces guard
            // against a mis-keyed tree.
            string prefix = heroClass.Trim().ToLowerInvariant() + ".";

            float sum = 0f;
            foreach (var node in tree.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                if (!node.Id.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                if (!service.IsUnlocked(node.Id)) continue;
                sum += additive ? node.DamageBonus : node.CdReduction;
            }
            return sum;
        }
    }
}
