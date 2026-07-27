// =============================================================================
// WeaponElementalOnHitTests (EditMode) — elemental melee on-hit VFX resolver.
// -----------------------------------------------------------------------------
// Locks WeaponVfxMap.ElementalOnHitKey: the ONE data-driven reader that maps a
// WeaponDef.element string to an OWNER-TAGGED HovlVfxCatalog impact key (every key
// tagged manual:true in Assets/Editor/VfxManualPicks.json — mapped VERBATIM, never
// substituted). Asserts:
//   • each recognised element resolves its owner-tagged key (fire->Fireball_Impact,
//     ice/frost->Frost_Impact, freezing->Freezing_Impact, lightning/electric->
//     Thunderbolt_Impact, arcane->Arcane_Impact, poison->PosionCloud_Cast);
//   • the lookup is case-/whitespace-insensitive (normalized);
//   • null weapon / null / empty / unknown element -> null (no elemental layer);
//   • HELD elements with no owner-tagged on-hit key (e.g. holy) -> null.
//
// Pure logic (no MonoBehaviour, no scene) mirroring the project EditMode pattern.
// The "key is genuinely owner-tagged" half is gated by DataRegression's weapon-vfx
// oracle (see WeaponVfxMap header + the marker line reported with this change).
// =============================================================================

using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class WeaponElementalOnHitTests
    {
        private static WeaponDef Weapon(string element) =>
            new WeaponDef { id = "t_" + (element ?? "none"), name = "test", element = element };

        [TestCase("fire",        "Fireball_Impact")]
        [TestCase("ice",         "Frost_Impact")]
        [TestCase("frost",       "Frost_Impact")]
        [TestCase("freeze",      "Freezing_Impact")]
        [TestCase("freezing",    "Freezing_Impact")]
        [TestCase("lightning",   "Thunderbolt_Impact")]
        [TestCase("electric",    "Thunderbolt_Impact")]
        [TestCase("electricity", "Thunderbolt_Impact")]
        [TestCase("thunder",     "Thunderbolt_Impact")]
        [TestCase("arcane",      "Arcane_Impact")]
        [TestCase("poison",      "PosionCloud_Cast")]
        public void element_maps_to_owner_tagged_impact_key(string element, string expectedKey)
        {
            Assert.That(WeaponVfxMap.ElementalOnHitKey(Weapon(element)), Is.EqualTo(expectedKey),
                $"element '{element}' must resolve its owner-tagged on-hit key '{expectedKey}'");
        }

        [TestCase("FIRE")]
        [TestCase("  fire  ")]
        [TestCase("Fire")]
        public void element_lookup_is_case_and_whitespace_insensitive(string element)
        {
            Assert.That(WeaponVfxMap.ElementalOnHitKey(Weapon(element)), Is.EqualTo("Fireball_Impact"),
                "the element string is normalized (Trim + ToLowerInvariant) before lookup");
        }

        [Test]
        public void null_weapon_resolves_null()
        {
            Assert.That(WeaponVfxMap.ElementalOnHitKey(null), Is.Null,
                "null weapon must be null-safe -> no elemental layer");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void null_or_empty_element_resolves_null(string element)
        {
            Assert.That(WeaponVfxMap.ElementalOnHitKey(Weapon(element)), Is.Null,
                "a weapon with no element brand must add no elemental on-hit layer");
        }

        [TestCase("unknownelement")]
        [TestCase("shadow")]
        public void unknown_element_resolves_null(string element)
        {
            Assert.That(WeaponVfxMap.ElementalOnHitKey(Weapon(element)), Is.Null,
                "an unrecognised element must fall through to null (no elemental layer)");
        }

        [TestCase("holy")]
        [TestCase("water")]
        [TestCase("earth")]
        [TestCase("nature")]
        public void held_element_with_no_owner_tag_resolves_null(string element)
        {
            // HELD: the owner has NOT tagged an on-hit key for these elements yet. The resolver
            // must stay null (never substitute an untagged key) until the owner tags one.
            Assert.That(WeaponVfxMap.ElementalOnHitKey(Weapon(element)), Is.Null,
                $"HELD element '{element}' has no owner-tagged on-hit key -> must stay null");
        }
    }
}
