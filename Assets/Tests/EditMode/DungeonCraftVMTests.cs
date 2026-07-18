// =============================================================================
// DungeonCraftVMTests (EditMode) -- §2c lock for the dungeon crafting VM.
// -----------------------------------------------------------------------------
// Over a REAL DungeonInventory (ScriptableObject) + fabricated CraftingRecipe/data:
// the pure Project() have/need/met/canCraft/already-crafted projection, plus the
// instance re-projecting + raising Changed on an inventory change. No DeNelle.Village
// reference anywhere (module isolation).
// =============================================================================
using NUnit.Framework;
using UnityEngine;
using DeNelle.Dungeons;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class DungeonCraftVMTests
    {
        private DungeonInventory _inv;

        [SetUp]
        public void SetUp() { _inv = ScriptableObject.CreateInstance<DungeonInventory>(); }

        [TearDown]
        public void TearDown() { if (_inv != null) Object.DestroyImmediate(_inv); _inv = null; }

        private static CraftingRecipe TorchRecipe() => new CraftingRecipe
        {
            Id = "torch", DisplayName = "Torch", Description = "A light.", ResultGlyph = "T",
            Ingredients = new[] { new RecipeIngredient { IngredientId = "reed", Count = 2 } },
        };

        private static CraftingDataSet Data() => new CraftingDataSet
        {
            Ingredients = { new CraftingIngredient { Id = "reed", DisplayName = "Dry Reed", Glyph = "r", Tint = "aabbcc" } },
            Recipes = { TorchRecipe() },
        };

        [Test]
        public void project_empty_inventory_reports_unmet_and_uncraftable()
        {
            var vm = DungeonCraftVM.Project(TorchRecipe(), Data(), _inv);
            Assert.That(vm.HasRecipe, Is.True);
            Assert.That(vm.AlreadyCrafted, Is.False);
            Assert.That(vm.CanCraft, Is.False);
            Assert.That(vm.Ingredients.Count, Is.EqualTo(1));
            Assert.That(vm.Ingredients[0].DisplayName, Is.EqualTo("Dry Reed"), "name from crafting data");
            Assert.That(vm.Ingredients[0].Glyph, Is.EqualTo("r"));
            Assert.That(vm.Ingredients[0].Tint, Is.EqualTo("aabbcc"));
            Assert.That(vm.Ingredients[0].Have, Is.EqualTo(0));
            Assert.That(vm.Ingredients[0].Need, Is.EqualTo(2));
            Assert.That(vm.Ingredients[0].Met, Is.False);
        }

        [Test]
        public void project_with_ingredients_is_craftable()
        {
            _inv.AddIngredient("reed", 2);
            var vm = DungeonCraftVM.Project(TorchRecipe(), Data(), _inv);
            Assert.That(vm.Ingredients[0].Met, Is.True);
            Assert.That(vm.CanCraft, Is.True);
        }

        [Test]
        public void project_after_craft_reports_finished_state()
        {
            var recipe = TorchRecipe();
            _inv.AddIngredient("reed", 2);
            Assert.That(_inv.Craft(recipe), Is.True);

            var vm = DungeonCraftVM.Project(recipe, Data(), _inv);
            Assert.That(vm.AlreadyCrafted, Is.True);
            Assert.That(vm.CanCraft, Is.False, "a crafted one-shot recipe is not re-craftable");
            Assert.That(vm.Ingredients[0].Met, Is.True, "consumed ingredients still read as satisfied");
            Assert.That(vm.Ingredients[0].Shown, Is.EqualTo(2), "shows the need, not the (now 0) have");
        }

        [Test]
        public void result_text_tracks_projection_state()
        {
            var recipe = TorchRecipe();
            var data = Data();
            var request = new CraftingPanelRequest { Pedestal = null, Recipe = recipe, CraftingData = data, Inventory = _inv };

            var vm = new DungeonCraftVM(request);
            Assert.That(vm.ResultText, Is.EqualTo("Gather the ingredients"));

            _inv.AddIngredient("reed", 2);
            vm.Rebind(request);   // re-project against the fuller inventory
            Assert.That(vm.ResultText, Is.EqualTo("Ready to craft"));

            _inv.Craft(recipe);
            vm.Rebind(request);
            Assert.That(vm.ResultText, Is.EqualTo("Torch crafted"));
            vm.Dispose();
        }

        [Test]
        public void inventory_change_re_projects_and_raises_changed()
        {
            var request = new CraftingPanelRequest { Pedestal = null, Recipe = TorchRecipe(), CraftingData = Data(), Inventory = _inv };
            var vm = new DungeonCraftVM(request);
            int fires = 0; vm.Changed += () => fires++;

            // A pickup mutates the inventory + fires InventoryChanged -> the VM re-projects.
            _inv.CollectPickup("pk-1", "reed", 2);

            Assert.That(fires, Is.GreaterThanOrEqualTo(1), "inventory change re-raises VM Changed");
            Assert.That(vm.Recipe.Ingredients[0].Met, Is.True, "re-projected against the new count");
            vm.Dispose();
        }

        [Test]
        public void craft_command_is_safe_without_a_pedestal()
        {
            var request = new CraftingPanelRequest { Pedestal = null, Recipe = TorchRecipe(), CraftingData = Data(), Inventory = _inv };
            var vm = new DungeonCraftVM(request);
            Assert.That(vm.Craft(), Is.False, "no pedestal -> Craft is a safe no-op");
            vm.Dispose();
        }
    }
}
