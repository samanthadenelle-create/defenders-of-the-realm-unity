// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// CanonicalCatalogTests (EditMode) — WO-329 regression suite.
// -----------------------------------------------------------------------------
// Parse-guards the canonical gear/recipe catalogs the game hydrates at runtime
// (GearCatalog, CraftingRecipeCatalog, ConsumableCraftingCatalog). Each file
// under StreamingAssets/Data/Canonical must exist, parse as well-formed JSON,
// and carry its expected non-empty top-level array. A typo or a leaked agent
// markup token here ships broken data with no compile error — this is the cheap
// static catch. (The broader stray-markup re-scan lives in
// DeNelle.Data.Tests/CanonicalJsonIntegrityTest; this fixture is the gear/recipe
// slice the regression suite calls out explicitly.)
// =============================================================================

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class CanonicalCatalogTests
    {
        private static string CanonicalDir =>
            Path.Combine(Application.streamingAssetsPath, "Data", "Canonical");

        private static JObject Parse(string fileName)
        {
            string path = Path.Combine(CanonicalDir, fileName);
            Assert.That(File.Exists(path), Is.True, $"missing canonical file: {fileName}");
            string raw = File.ReadAllText(path);
            Assert.That(string.IsNullOrWhiteSpace(raw), Is.False, $"empty canonical file: {fileName}");
            return JObject.Parse(raw);  // throws on malformed JSON -> test fails with the parser error
        }

        private static void AssertNonEmptyArray(JObject root, string key, string fileName)
        {
            var token = root[key];
            Assert.That(token, Is.Not.Null, $"{fileName}: missing '{key}' array");
            Assert.That(token.Type, Is.EqualTo(JTokenType.Array), $"{fileName}: '{key}' must be an array");
            Assert.That(((JArray)token).Count, Is.GreaterThan(0), $"{fileName}: '{key}' must not be empty");
        }

        [Test]
        public void weapons_catalog_parses_and_has_entries()
        {
            AssertNonEmptyArray(Parse("weapons.json"), "weapons", "weapons.json");
        }

        [Test]
        public void armor_catalog_parses_and_has_entries()
        {
            AssertNonEmptyArray(Parse("armor.json"), "armor", "armor.json");
        }

        [Test]
        public void crafting_recipes_parse_and_have_entries()
        {
            AssertNonEmptyArray(Parse("crafting-recipes.json"), "recipes", "crafting-recipes.json");
        }

        [Test]
        public void consumable_recipes_parse_and_have_entries()
        {
            AssertNonEmptyArray(Parse("consumable-recipes.json"), "recipes", "consumable-recipes.json");
        }

        [Test]
        public void first_weapon_entry_has_required_fields()
        {
            var weapons = (JArray)Parse("weapons.json")["weapons"];
            var first = (JObject)weapons[0];
            Assert.That(first["id"], Is.Not.Null, "weapon entry needs an id");
            Assert.That(first["job"], Is.Not.Null, "weapon entry needs a job");
        }
    }
}
