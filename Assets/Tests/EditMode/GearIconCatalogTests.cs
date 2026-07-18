// =============================================================================
// GearIconCatalogTests (EditMode) — §2c permission gate for the icon-leak seam.
// -----------------------------------------------------------------------------
// UI_MVVM_MIGRATION_PLAN §1 Phase 1: the shop / inventory / equipment / party-shop
// Views used to re-pull GearCatalog.Find* to feed ItemIconCatalog.For* when painting
// a row/slot icon (a gameplay-catalog read inside a dumb-skin View). GearIconCatalog
// absorbs that pair so the View resolves art from ROLE + ID keys instead.
//
// This locks the seam contract so the View swap is safe: Resolve(role,id) MUST return
// the SAME sprite the old `ItemIconCatalog.For*(GearCatalog.Find*(id))` pair returned,
// and Glyph(role,id) mirrors the old type-glyph fallback. Uses the REAL catalogs (as
// ShopVMTests does) — when a sheet isn't sliced in this env both sides resolve null,
// so the equivalence still holds.
// =============================================================================

using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class GearIconCatalogTests
    {
        [Test]
        public void resolve_unknown_role_returns_null()
        {
            Assert.That(GearIconCatalog.Resolve("not-a-role", "whatever"), Is.Null,
                "an unrecognised icon role must resolve to no sprite (caller uses its own fallback)");
        }

        [Test]
        public void glyph_unknown_role_returns_question_mark()
        {
            Assert.That(GearIconCatalog.Glyph("not-a-role", "whatever"), Is.EqualTo("?"),
                "an unrecognised role must fall through to the neutral '?' glyph");
        }

        [Test]
        public void resolve_weapon_matches_the_old_gearcatalog_itemiconcatalog_pair()
        {
            var weapons = GearCatalog.AllWeapons();
            if (weapons == null || weapons.Count == 0) Assert.Ignore("no catalog weapons in this env");
            string id = weapons[0].id;

            var viaSeam = GearIconCatalog.Resolve(InventoryVM.IconRoleWeapon, id);
            var manual  = ItemIconCatalog.ForWeapon(GearCatalog.FindWeapon(id));
            Assert.That(viaSeam, Is.EqualTo(manual),
                "the seam MUST return the SAME sprite the old GearCatalog+ItemIconCatalog pair returned");
        }

        [Test]
        public void resolve_armor_matches_the_old_gearcatalog_itemiconcatalog_pair()
        {
            var armors = GearCatalog.AllArmors();
            if (armors == null || armors.Count == 0) Assert.Ignore("no catalog armor in this env");
            string id = armors[0].id;

            var viaSeam = GearIconCatalog.Resolve(InventoryVM.IconRoleArmor, id);
            var manual  = ItemIconCatalog.ForArmor(GearCatalog.FindArmor(id));
            Assert.That(viaSeam, Is.EqualTo(manual),
                "the seam MUST return the SAME armor sprite the old pair returned");
        }

        [Test]
        public void resolve_potion_matches_itemiconcatalog_forconsumable()
        {
            var viaSeam = GearIconCatalog.Resolve(InventoryVM.IconRolePotion, "minor-heal-potion", "Minor Heal Potion");
            var manual  = ItemIconCatalog.ForConsumable("minor-heal-potion", "Minor Heal Potion");
            Assert.That(viaSeam, Is.EqualTo(manual),
                "the potion role must forward verbatim to ItemIconCatalog.ForConsumable(id,name)");
        }

        [Test]
        public void resolve_missing_id_is_null_never_throws()
        {
            // A def-less id resolves via ForWeapon(null)/ForArmor(null) -> null (the caller's glyph
            // fallback), never an exception. Locks the null-guard behaviour the Views rely on.
            Assert.That(GearIconCatalog.Resolve(InventoryVM.IconRoleWeapon, "definitely-not-a-real-weapon-id"), Is.Null);
            Assert.That(GearIconCatalog.Resolve(InventoryVM.IconRoleArmor, "definitely-not-a-real-armor-id"), Is.Null);
            Assert.That(GearIconCatalog.Glyph(InventoryVM.IconRoleWeapon, "definitely-not-a-real-weapon-id"),
                Is.EqualTo("?"), "a def-less weapon glyph falls through to '?'");
        }
    }
}
