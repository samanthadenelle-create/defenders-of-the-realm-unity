using System.Collections.Generic;
using DeNelle.Core.Combat;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    public sealed class ElementalDamageResolverTests
    {
        [Test]
        public void Neutral_damage_is_unchanged()
        {
            var r = ElementalDamageResolver.Resolve(40f, DamageElement.Flame, DamageElement.None);
            Assert.That(r.Outcome, Is.EqualTo(AffinityOutcome.Neutral));
            Assert.That(r.FinalAmount, Is.EqualTo(40f));
        }

        [Test]
        public void Authored_vulnerability_is_exactly_twenty_five_percent()
        {
            var r = ElementalDamageResolver.Resolve(40f, DamageElement.Flame, DamageElement.None,
                new List<DamageElement> { DamageElement.Flame });
            Assert.That(r.Outcome, Is.EqualTo(AffinityOutcome.Vulnerable));
            Assert.That(r.Multiplier, Is.EqualTo(1.25f));
            Assert.That(r.FinalAmount, Is.EqualTo(50f));
        }

        [Test]
        public void Matching_affinity_resists_without_immunity()
        {
            var r = ElementalDamageResolver.Resolve(40f, DamageElement.Ice, DamageElement.Ice,
                new List<DamageElement> { DamageElement.Ice });
            Assert.That(r.Outcome, Is.EqualTo(AffinityOutcome.Resisted));
            Assert.That(r.Multiplier, Is.EqualTo(0.75f));
            Assert.That(r.FinalAmount, Is.EqualTo(30f));
            Assert.That(r.FinalAmount, Is.GreaterThan(0f));
        }

        [Test]
        public void Physical_never_uses_elemental_affinity()
        {
            var r = ElementalDamageResolver.Resolve(40f, DamageElement.None, DamageElement.None,
                new List<DamageElement> { DamageElement.None });
            Assert.That(r.Outcome, Is.EqualTo(AffinityOutcome.Neutral));
            Assert.That(r.FinalAmount, Is.EqualTo(40f));
        }
    }
}
