// =============================================================================
// Canonical data — BuildingCatalog loader tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-XC-09 / TC-VIL-08: buildings.json must load via the typed
// BuildingCatalog and hydrate the five canonical buildings with valid HP, cost,
// footprint and model fields.
//
// BuildingCatalog reads StreamingAssets/Data/Canonical/buildings.json
// synchronously — a real directory in the Editor, so the loader runs in
// EditMode with no stub. Reload() is called in SetUp to defeat the static cache
// (so a prior test's data can never leak in).
// =============================================================================

using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Data.Tests
{
    [TestFixture]
    public class BuildingCatalogTest
    {
        [SetUp]
        public void SetUp()
        {
            // Defeat the static cache so each test re-reads the canonical file.
            BuildingCatalog.Reload();
        }

        // =====================================================================
        //  Parse + entry count
        // =====================================================================

        [Test]
        public void buildings_json_loads_exactly_seven_buildings()
        {
            Assert.That(BuildingCatalog.Buildings, Is.Not.Null,
                "BuildingCatalog.Buildings must never be null.");
            // Canon grew to 7: original 5 + lumbermill + forge (food/economy stations).
            // NOTE (owner roundtable 2026-06-07): intent is mill/granary/armorer + farm->node;
            // data currently has lumbermill+forge and farm still present — granary not yet added,
            // farm not yet moved out. Update this count if the canonical set changes.
            Assert.That(BuildingCatalog.Buildings.Count, Is.EqualTo(7),
                "buildings.json must hydrate the seven canonical gameplay buildings.");
        }

        [Test]
        public void all_seven_canonical_building_ids_are_present()
        {
            foreach (var id in new[] { "crystal-mine", "farm", "pet-house", "workshop", "arcane-tower", "lumbermill", "forge" })
            {
                Assert.That(BuildingCatalog.Find(id), Is.Not.Null,
                    $"buildings.json must contain the '{id}' building.");
            }
        }

        // =====================================================================
        //  Lookups
        // =====================================================================

        [Test]
        public void find_by_type_resolves_each_building_type()
        {
            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                Assert.That(BuildingCatalog.Find(type), Is.Not.Null,
                    $"BuildingCatalog.Find({type}) must resolve a def.");
            }
        }

        [Test]
        public void find_with_an_unknown_id_returns_null()
        {
            Assert.That(BuildingCatalog.Find("not-a-building"), Is.Null);
            Assert.That(BuildingCatalog.Find((string)null), Is.Null);
            Assert.That(BuildingCatalog.Find(string.Empty), Is.Null);
        }

        [Test]
        public void crystal_mine_resolves_to_the_crystal_mine_type()
        {
            var def = BuildingCatalog.Find("crystal-mine");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.ResolvedType, Is.EqualTo(BuildingType.CrystalMine),
                "the 'type' string must parse to the BuildingType enum.");
        }

        // =====================================================================
        //  Field sanity — every building must carry usable gameplay values
        // =====================================================================

        [Test]
        public void every_building_has_valid_hp_cost_model_and_footprint()
        {
            foreach (var def in BuildingCatalog.Buildings)
            {
                Assert.That(def, Is.Not.Null, "no null entry in the catalog.");
                Assert.That(string.IsNullOrEmpty(def.Id), Is.False, "every building has an id.");
                Assert.That(def.Hp, Is.GreaterThan(0f), $"{def.Id} must have HP > 0.");
                Assert.That(def.MaxHp, Is.GreaterThanOrEqualTo(def.Hp),
                    $"{def.Id} maxHp must be >= hp.");
                Assert.That(def.CrystalCost, Is.GreaterThanOrEqualTo(0),
                    $"{def.Id} crystalCost must be >= 0.");
                Assert.That(string.IsNullOrEmpty(def.Model), Is.False,
                    $"{def.Id} must name a KayKit model mesh.");
                // ResolvedFootprint never throws — falls back to Small with a warning.
                Assert.That(System.Enum.IsDefined(typeof(BuildingFootprint), def.ResolvedFootprint),
                    Is.True, $"{def.Id} footprint must resolve to a known enum value.");
            }
        }

        [Test]
        public void buildings_are_returned_sorted_by_build_menu_order()
        {
            int prev = int.MinValue;
            foreach (var def in BuildingCatalog.Buildings)
            {
                Assert.That(def.BuildMenuOrder, Is.GreaterThanOrEqualTo(prev),
                    "BuildingCatalog.Buildings must be sorted ascending by buildMenuOrder.");
                prev = def.BuildMenuOrder;
            }
        }

        [Test]
        public void building_display_names_are_canon_string_keys_not_literals()
        {
            // Port spec Part 4: displayName is a KEY into canon-strings.json, never
            // a literal — so it must be a single bareword token (no spaces).
            foreach (var def in BuildingCatalog.Buildings)
            {
                Assert.That(string.IsNullOrEmpty(def.DisplayName), Is.False,
                    $"{def.Id} must carry a displayName key.");
                Assert.That(def.DisplayName.Contains(" "), Is.False,
                    $"{def.Id} displayName '{def.DisplayName}' looks like a literal, not a canon key.");
            }
        }
    }
}
