using DeNelle.Core.Combat;
using DeNelle.Village;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    public sealed class GearProgramContractTests
    {
        [Test]
        public void Price_floors_are_monotonic_on_daily_chest_denominator()
        {
            Assert.That(GearAppraisal.TierBaseValue(DeNelle.Village.Crafting.GearTier.Common), Is.EqualTo(1000));
            Assert.That(GearAppraisal.TierBaseValue(DeNelle.Village.Crafting.GearTier.Fine), Is.EqualTo(2000));
            Assert.That(GearAppraisal.TierBaseValue(DeNelle.Village.Crafting.GearTier.Master), Is.EqualTo(6000));
            Assert.That(GearAppraisal.TierBaseValue(DeNelle.Village.Crafting.GearTier.Legendary), Is.EqualTo(12000));
        }

        [Test]
        public void Functioning_effect_has_price_value_but_vfx_and_lore_do_not()
        {
            var plain = new WeaponDef { id = "plain", rarity = "uncommon", damageMult = 1.2f };
            var effect = new WeaponDef { id = "effect", rarity = "uncommon", damageMult = 1.2f,
                effectKind = "burn", effectChance = 0.2f, effectDurationSeconds = 4f };
            var cosmetic = new WeaponDef { id = "cosmetic", rarity = "uncommon", damageMult = 1.2f,
                name = "Poetic Name", flavor = "Lore", vfxHit = "weapon.hit.flame" };
            Assert.That(GearAppraisal.Appraise(effect).estimatedValue,
                Is.GreaterThan(GearAppraisal.Appraise(plain).estimatedValue));
            Assert.That(GearAppraisal.Appraise(cosmetic).estimatedValue,
                Is.EqualTo(GearAppraisal.Appraise(plain).estimatedValue));
        }

        [Test]
        public void Semantic_vfx_registry_holds_unknown_verbs()
        {
            Assert.That(WeaponVfxMap.ResolveSemanticVerb("weapon.hit.flame"), Is.EqualTo("Fireball_Impact"));
            Assert.That(WeaponVfxMap.ResolveSemanticVerb("weapon.hit.owner-never-tagged"), Is.Null);
        }
    }
}
