// =============================================================================
// HeroTalentModifiers — the talent → ability/defense stat bridge (v2 effects).
// -----------------------------------------------------------------------------
// Reads the live unlocked-node set from WisdomCurrencyService + the per-hero tree
// (+ the Shared Universal pool) from HeroTalentCatalog, then aggregates the v2
// per-node `effect` payloads into the multipliers/fractions the combat code reads:
//
//   DamageMultiplier(class)        = 1 + Σ(damageBonus + allStatsPct)      [HeroAbilities]
//   CooldownMultiplier(class)      = 1 - Σ(cdReduction)                    [HeroAbilities]
//   MaxHpMultiplier(class)         = 1 + Σ(maxHpPct + allStatsPct)         [HeroHealth.MaxHp]
//   IncomingDamageReduction(class) = Σ(damageReduction + defense + allStatsPct)  [HeroHealth.TakeDamage]
//   BlockChance(class) / RollBlock = Σ(blockChance)                        [HeroHealth.TakeDamage]
//   HealAmountMultiplier(class)    = 1 + Σ(modifyAbility where stat=="heal")  [HeroAbilities heal]
//
// V1 WIRING SCOPE (solo Knight north star): only KNIGHT pure-stat + heal-modify +
// the unlockAbility skill nodes are wired (unlockAbility reuses the existing v1
// loadout-equip flow via the node's kind="skill"+abilityId — no extra code here).
// Knight ALLY-dependent effects (auras / Oathweld / Knight-Eternal ally portion /
// Champion) are NO-OP in solo V1 — there are no combat allies — and their SELF
// portion (e.g. Knight Eternal's +45% defense) still applies because it is the
// node's `effect.value`. Behavioural handlers tagged "(… — V-later)" in the data
// (reflect / dot / invuln / laststand / taunt-on-enemies / summon / stun) are not
// yet built; their nodes are takeable + scored but inert until those land.
// Mage/Ranger are STORED-not-wired: their nodes load as data+icons; their effects
// would only apply if that class were ever the active hero (it is not in V1).
//
// Stateless + defensive: with no service / no tree / no unlocked nodes every method
// returns the identity (1f / 0f), so combat behaves exactly as the pre-talent
// baseline until the player learns a node.
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Village.Talents
{
    /// <summary>Translates a hero's unlocked talent nodes into ability/defense stat modifiers.</summary>
    public static class HeroTalentModifiers
    {
        // Sanity clamps.
        private const float MaxDamageMultiplier  = 3f;     // +200% damage ceiling
        private const float MinCooldownMultiplier = 0.4f;  // at most -60% cooldown
        private const float MaxHpMult            = 3f;     // +200% HP ceiling
        private const float MaxIncomingReduction = 0.85f;  // never reduce a hit by more than 85%
        private const float MaxBlock             = 0.85f;  // block chance ceiling

        // ── Offensive (existing consumers in HeroAbilities) ───────────────────────

        public static float DamageMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "damageBonus") + StatSum(heroClass, "allStatsPct") + LegacyDamage(heroClass);
            return Mathf.Clamp(1f + sum, 1f, MaxDamageMultiplier);
        }

        public static float CooldownMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "cdReduction") + LegacyCooldown(heroClass);
            return Mathf.Clamp(1f - sum, MinCooldownMultiplier, 1f);
        }

        // ── Defensive (consumed in HeroHealth) ────────────────────────────────────

        /// <summary>1 + Σ(maxHpPct, allStatsPct). Applied to the hero's effective max HP.</summary>
        public static float MaxHpMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "maxHpPct") + StatSum(heroClass, "allStatsPct");
            return Mathf.Clamp(1f + sum, 1f, MaxHpMult);
        }

        /// <summary>
        /// Fractional reduction (0..0.85) applied to incoming damage: damageReduction
        /// (Iron Resolve, Resilience) + defense (Legendary Vanguard, Knight Eternal self
        /// portion) + allStatsPct. NOTE: Legendary Vanguard's "while stationary" gate is
        /// applied flat in V1 (see node note); Last Stand's conditional low-HP DR is a
        /// V-later behavioural handler and is NOT summed here.
        /// </summary>
        public static float IncomingDamageReduction(string heroClass)
        {
            float sum = StatSum(heroClass, "damageReduction")
                      + StatSum(heroClass, "defense")
                      + StatSum(heroClass, "allStatsPct");
            return Mathf.Clamp(sum, 0f, MaxIncomingReduction);
        }

        /// <summary>Σ(blockChance) clamped to 0..0.85 (Guardian Stance; Battle Instinct is crit, not block).</summary>
        public static float BlockChance(string heroClass)
        {
            return Mathf.Clamp(StatSum(heroClass, "blockChance"), 0f, MaxBlock);
        }

        /// <summary>Rolls a block this hit (full negate). Safe with no nodes (chance 0 → false).</summary>
        public static bool RollBlock(string heroClass)
        {
            float c = BlockChance(heroClass);
            return c > 0f && UnityEngine.Random.value < c;
        }

        // ── Heal modify (Mending Oath) ────────────────────────────────────────────

        /// <summary>1 + Σ(modifyAbility nodes flagged stat=="heal"). Mending Oath = +0.30.</summary>
        public static float HealAmountMultiplier(string heroClass)
        {
            return 1f + SumModifyHeal(heroClass);
        }

        // ── Generic stat accessor (also exposes not-yet-wired stats: critChance,
        //    attackSpeed, manaRegen, manaCostReduction, healthRegen, moveSpeed,
        //    range, dodge, shieldStrength, wisdomPerLevel) for future consumers. ─────

        /// <summary>Σ(effect.value) over unlocked nodes (hero tree + shared) whose effect.type matches.</summary>
        public static float StatSum(string heroClass, string effectType)
        {
            if (string.IsNullOrEmpty(effectType)) return 0f;
            float acc = 0f;
            ForEachUnlocked(heroClass, n =>
            {
                if (n.Effect != null && string.Equals(n.Effect.Type, effectType, StringComparison.OrdinalIgnoreCase))
                    acc += n.Effect.Value;
            });
            return acc;
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        private static float SumModifyHeal(string heroClass)
        {
            float acc = 0f;
            ForEachUnlocked(heroClass, n =>
            {
                if (n.Effect != null
                    && string.Equals(n.Effect.Type, "modifyAbility", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(n.Effect.Stat, "heal", StringComparison.OrdinalIgnoreCase))
                    acc += n.Effect.Value;
            });
            return acc;
        }

        // Legacy v1 fields (top-level damageBonus / cdReduction on a node). v2 data
        // drives effects through the `effect` object, but reading these keeps any
        // remaining v1-shaped node additive (0 when absent → no behaviour change).
        private static float LegacyDamage(string heroClass)
        {
            float acc = 0f;
            ForEachUnlocked(heroClass, n => acc += n.DamageBonus);
            return acc;
        }

        private static float LegacyCooldown(string heroClass)
        {
            float acc = 0f;
            ForEachUnlocked(heroClass, n => acc += n.CdReduction);
            return acc;
        }

        /// <summary>
        /// Invokes <paramref name="visit"/> for every UNLOCKED node belonging to the hero's
        /// tree (id prefix "&lt;class&gt;.") OR the Shared Universal pool (id prefix "shared.").
        /// </summary>
        private static void ForEachUnlocked(string heroClass, Action<HeroTalentNodeDef> visit)
        {
            if (string.IsNullOrEmpty(heroClass) || visit == null) return;

            var service = WisdomCurrencyService.Instance;
            if (service == null) return;
            var unlocked = service.Unlocked;
            if (unlocked == null || unlocked.Count == 0) return;

            string slug = heroClass.Trim().ToLowerInvariant();

            // Hero tree.
            var tree = HeroTalentCatalog.GetTree(slug);
            if (tree != null && tree.Nodes != null)
            {
                foreach (var node in tree.Nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                    if (service.IsUnlocked(node.Id)) visit(node);
                }
            }

            // Shared Universal pool (applies to whichever hero learned them).
            var shared = HeroTalentCatalog.SharedNodes;
            if (shared != null)
            {
                foreach (var node in shared)
                {
                    if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                    if (service.IsUnlocked(node.Id)) visit(node);
                }
            }
        }
    }
}
