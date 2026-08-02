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
        public void mage_q_is_thrain_fireball_and_arcane_bolt_is_preserved()
        {
            // RETARGETED 2026-08-02 (WO-861, owner-approved appendix A1). This test used to pin
            // mage Q == "Arcane Bolt" from the React port. Thrain's authored kit makes Q the
            // easy ranged starter FIREBALL (strike, 0 mana, 0.6s, 30 dmg, range 14) - so the old
            // assertion was pinning canon the OWNER deliberately replaced, and it failed on
            // arrival with: Expected "Arcane Bolt" But was "Fireball".
            // The canon value is NOT lost: Arcane Bolt was ported VERBATIM into the mage
            // learnable-spell pool (id "mage.arcane-bolt", same tuning, same VFX keys) precisely
            // so no shipped balance vanished in the retune. That is why this test now pins the
            // NEW slot occupant AND the no-mana-primary property that made Arcane Bolt the Q in
            // the first place - the shape of the kit is preserved even though the name moved.
            var q = AbilityCatalog.Find("mage", AbilitySlot.Q);
            Assert.That(q, Is.Not.Null, "the mage must have a Q");
            Assert.That(q.Name, Is.EqualTo("Fireball"),
                "canon ability name — verbatim, never paraphrased (WO-861 A1).");
            Assert.That(q.EffectEnum, Is.EqualTo(AbilityEffect.Strike));
            Assert.That(q.ManaCost, Is.EqualTo(0f),
                "the mage primary stays the NO-MANA attack — out of mana must never mean unarmed " +
                "(WO-861: mana is the survival lifeline, so the basic can never be gated on it).");
            Assert.That(q.Range, Is.GreaterThan(0f),
                "Thrain is a RANGED glass cannon — his primary must not be melee-range.");
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

                // RANGE: > 0 for anything that reaches OUT at a target; 0 is CORRECT and
                // meaningful for a SELF-targeted ability.
                // Relaxed 2026-08-02 (WO-861): the blanket "> 0" was authored when every mage
                // ability was a projectile, and it failed on arrival with "Arcane Shell range
                // must be > 0" — but Arcane Shell is a self-cast damage shield, so a range of 0
                // is the honest encoding of "on me", not a missing value. Asserting > 0 there
                // would have forced a fake number into the data to satisfy a test.
                // The REAL invariant this test protects is preserved below: a targeted ability
                // with range 0 can never hit anything, which IS a bug worth failing on.
                // NOTE: matched on the raw effect STRING, not EffectEnum — AbilityEffect has no
                // shield/drainshot/manaweave members (it is {Strike,Snare,Aoe,Cleave,Heal,Meteor}),
                // so the new WO-861 effects do not classify through the enum. ResolveEffect
                // switches on the string, which is why the string is the honest source here.
                string eff = (def.Effect ?? string.Empty).Trim().ToLowerInvariant();
                bool selfTargeted = eff == "shield" || eff == "heal" || eff == "manaweave";
                if (selfTargeted)
                    Assert.That(def.Range, Is.GreaterThanOrEqualTo(0f),
                        $"{def.Name} is self-targeted ('{eff}') so range 0 is valid, but it must not be negative.");
                else
                    Assert.That(def.Range, Is.GreaterThan(0f),
                        $"{def.Name} targets something at a distance ('{eff}') so range must be > 0 — " +
                        "a targeted ability with range 0 can never reach an enemy.");
            }
        }
    }
}
