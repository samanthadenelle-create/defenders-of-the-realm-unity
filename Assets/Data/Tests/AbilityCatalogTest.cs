// =============================================================================
// Canonical data — AbilityCatalog loader tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-XC-09: abilities.json must load via the typed
// AbilityCatalog and hydrate the per-class Q/W/E/R hero loadouts. The v2
// foundation reads the Mage (Blaise); Knight + Ranger are authored placeholders.
//
// AbilityCatalog reads StreamingAssets/Data/Canonical/abilities.json
// synchronously — a real directory in the Editor — so it runs in EditMode.
// =============================================================================

using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Data.Tests
{
    [TestFixture]
    public class AbilityCatalogTest
    {
        [SetUp]
        public void SetUp()
        {
            AbilityCatalog.Reload();
        }

        // =====================================================================
        //  Parse + loadout shape
        // =====================================================================

        [Test]
        public void mage_loadout_hydrates_all_four_qwer_abilities()
        {
            var loadout = AbilityCatalog.GetLoadout("mage");
            Assert.That(loadout, Is.Not.Null);
            Assert.That(loadout.Count, Is.EqualTo(4),
                "the mage must have a full Q/W/E/R loadout.");
        }

        [Test]
        public void mage_loadout_is_ordered_q_w_e_r()
        {
            var loadout = AbilityCatalog.GetLoadout("mage");
            Assert.That(loadout[0].SlotEnum, Is.EqualTo(AbilitySlot.Q));
            Assert.That(loadout[1].SlotEnum, Is.EqualTo(AbilitySlot.W));
            Assert.That(loadout[2].SlotEnum, Is.EqualTo(AbilitySlot.E));
            Assert.That(loadout[3].SlotEnum, Is.EqualTo(AbilitySlot.R));
        }

        [Test]
        public void default_class_constant_is_the_mage()
        {
            Assert.That(AbilityCatalog.DefaultClass, Is.EqualTo("mage"),
                "the v2-foundation default hero class is Blaise the Mage.");
            var byDefault = AbilityCatalog.GetLoadout(null);
            Assert.That(byDefault.Count, Is.EqualTo(4),
                "a null class id must fall back to the default (mage) loadout.");
        }

        [Test]
        public void class_lookup_is_case_insensitive()
        {
            Assert.That(AbilityCatalog.GetLoadout("MAGE").Count, Is.EqualTo(4));
            Assert.That(AbilityCatalog.GetLoadout("  Mage  ").Count, Is.EqualTo(4));
        }

        [Test]
        public void unknown_class_returns_an_empty_loadout()
        {
            Assert.That(AbilityCatalog.GetLoadout("necromancer"), Is.Empty,
                "an unknown class id must yield an empty list, not throw.");
        }

        // =====================================================================
        //  Per-slot lookup
        // =====================================================================

        [Test]
        public void find_resolves_each_mage_slot()
        {
            foreach (AbilitySlot slot in System.Enum.GetValues(typeof(AbilitySlot)))
            {
                var def = AbilityCatalog.Find("mage", slot);
                Assert.That(def, Is.Not.Null, $"mage must have a {slot}-slot ability.");
                Assert.That(def.SlotEnum, Is.EqualTo(slot), "the def's slot must match the lookup.");
            }
        }

        [Test]
        public void find_with_an_unknown_class_returns_null()
        {
            Assert.That(AbilityCatalog.Find("paladin", AbilitySlot.Q), Is.Null);
        }

        // =====================================================================
        //  Canon values — the Mage's Arcane Bolt (verbatim from React mage.ts)
        // =====================================================================

        [Test]
        public void mage_q_is_the_canon_arcane_bolt()
        {
            var q = AbilityCatalog.Find("mage", AbilitySlot.Q);
            Assert.That(q.Name, Is.EqualTo("Arcane Bolt"),
                "canon ability name — verbatim, never paraphrased.");
            Assert.That(q.EffectEnum, Is.EqualTo(AbilityEffect.Strike));
            Assert.That(q.ManaCost, Is.EqualTo(0f),
                "Arcane Bolt is the no-mana primary (React mage.ts).");
        }

        [Test]
        public void mage_r_is_the_canon_meteor_strike_ultimate()
        {
            var r = AbilityCatalog.Find("mage", AbilitySlot.R);
            Assert.That(r.Name, Is.EqualTo("Meteor Strike"));
            Assert.That(r.EffectEnum, Is.EqualTo(AbilityEffect.Meteor));
            Assert.That(r.Cooldown, Is.GreaterThan(0f), "the ultimate has a cooldown.");
        }

        [Test]
        public void every_mage_ability_has_a_parseable_accent_colour()
        {
            foreach (var def in AbilityCatalog.GetLoadout("mage"))
            {
                // UnityColor falls back to white if the hex is unparseable —
                // assert the source string is a valid #rrggbb form.
                Assert.That(string.IsNullOrEmpty(def.Color), Is.False,
                    $"{def.Name} must carry an accent colour.");
                Assert.That(UnityEngine.ColorUtility.TryParseHtmlString(def.Color, out _),
                    Is.True, $"{def.Name} colour '{def.Color}' must be a valid hex string.");
            }
        }

        [Test]
        public void every_mage_ability_has_non_negative_tuning_values()
        {
            foreach (var def in AbilityCatalog.GetLoadout("mage"))
            {
                Assert.That(def.Cooldown, Is.GreaterThanOrEqualTo(0f), $"{def.Name} cooldown >= 0.");
                Assert.That(def.ManaCost, Is.GreaterThanOrEqualTo(0f), $"{def.Name} manaCost >= 0.");
                Assert.That(def.Damage, Is.GreaterThanOrEqualTo(0f), $"{def.Name} damage >= 0.");
                Assert.That(def.Range, Is.GreaterThan(0f), $"{def.Name} range must be > 0.");
            }
        }
    }
}
