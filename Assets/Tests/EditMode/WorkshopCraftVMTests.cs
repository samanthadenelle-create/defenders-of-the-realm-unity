// =============================================================================
// WorkshopCraftVMTests (EditMode) -- §2c lock for the Workshop crafting VM.
// -----------------------------------------------------------------------------
// Pure-projection tests (ProjectRecipe + BuildLarder, hermetic) + instance tests over
// a fake IWorkshopLarder: rows, Select / Craft commands + Changed, craft have/need.
// =============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Crafting;

namespace DeNelle.Tests.EditMode
{
    /// <summary>Fake larder with settable have-counts + craftable set + a TryCraft recorder.</summary>
    internal sealed class FakeWorkshopLarder : IWorkshopLarder
    {
        public readonly Dictionary<string, int> Have = new Dictionary<string, int>();
        public readonly HashSet<string> Craftable = new HashSet<string>();
        public string LastTryCraft;
        public bool TryCraftResult = true;

        public int Get(string id) => Have.TryGetValue(id, out var v) ? v : 0;
        public bool CanCraft(string recipeId) => Craftable.Contains(recipeId);
        public bool TryCraft(string recipeId) { LastTryCraft = recipeId; return TryCraftResult; }
        public IReadOnlyDictionary<string, int> Counts => Have;
        public event Action Changed;
        public void Raise() => Changed?.Invoke();
    }

    [TestFixture]
    public class WorkshopCraftVMTests
    {
        // Hermetic VM: inject identity resolvers so the instance never touches the JSON catalog.
        private static WorkshopCraftVM Vm(List<RecipeDef> recipes, FakeWorkshopLarder larder)
            => new WorkshopCraftVM(recipes, larder, null, id => id, _ => null, new List<IngredientDef>());

        private static RecipeDef Recipe(string id, string name, params (string ing, int n)[] lines)
        {
            var r = new RecipeDef { Id = id, DisplayName = name, ResultGlyph = "*", Description = "d",
                Ingredients = new List<RecipeIngredientLine>() };
            foreach (var (ing, n) in lines)
                r.Ingredients.Add(new RecipeIngredientLine { IngredientId = ing, Count = n });
            return r;
        }

        // ── Pure ProjectRecipe ────────────────────────────────────────────────

        [Test]
        public void project_recipe_maps_have_need_met_and_output()
        {
            var recipe = Recipe("torch", "Torch", ("reed", 2), ("resin", 1));
            var have = new Dictionary<string, int> { { "reed", 2 }, { "resin", 0 }, { "torch", 3 } };

            var vm = WorkshopCraftVM.ProjectRecipe(recipe, id => have.TryGetValue(id, out var v) ? v : 0,
                canCraft: false, displayNameFor: id => id);

            Assert.That(vm.HasRecipe, Is.True);
            Assert.That(vm.Id, Is.EqualTo("torch"));
            Assert.That(vm.OutputHeld, Is.EqualTo(3), "OutputHeld = have(OutputId)");
            Assert.That(vm.Ingredients.Count, Is.EqualTo(2));
            Assert.That(vm.Ingredients[0].Have, Is.EqualTo(2));
            Assert.That(vm.Ingredients[0].Need, Is.EqualTo(2));
            Assert.That(vm.Ingredients[0].Met, Is.True, "2/2 met");
            Assert.That(vm.Ingredients[1].Met, Is.False, "0/1 not met");
        }

        [Test]
        public void build_larder_orders_ingredients_then_recipes_then_orphans()
        {
            var counts = new Dictionary<string, int> { { "reed", 3 }, { "torch", 1 }, { "junk", 2 } };
            var ingredients = new List<IngredientDef> { new IngredientDef { Id = "reed", DisplayName = "Reed" } };
            var recipes = new List<RecipeDef> { Recipe("torch", "Torch") };

            string larder = WorkshopCraftVM.BuildLarder(counts, ingredients, recipes,
                id => counts.TryGetValue(id, out var v) ? v : 0,
                displayNameFor: id => id == "reed" ? "Reed" : id, glyphFor: _ => null);

            Assert.That(larder, Is.EqualTo("Larder:  Reed x3  ·  torch x1  ·  junk x2"));
        }

        [Test]
        public void build_larder_reads_empty_when_no_counts()
        {
            Assert.That(WorkshopCraftVM.BuildLarder(new Dictionary<string, int>(), null, null,
                _ => 0, id => id, _ => null), Is.EqualTo("Larder:  (empty)"));
        }

        // ── Instance rows / commands ──────────────────────────────────────────

        [Test]
        public void rows_project_display_craftable_and_selection()
        {
            var recipes = new List<RecipeDef> { Recipe("r1", "One"), Recipe("r2", "Two") };
            var larder = new FakeWorkshopLarder();
            larder.Craftable.Add("r1");
            var vm = Vm(recipes, larder);

            Assert.That(vm.Rows.Count, Is.EqualTo(2));
            Assert.That(vm.Rows[0].Name, Is.EqualTo("One"));
            Assert.That(vm.Rows[0].Affordable, Is.True, "r1 craftable");
            Assert.That(vm.Rows[0].Equipped, Is.True, "first recipe selected by default");
            Assert.That(vm.Rows[1].Affordable, Is.False);
            Assert.That(vm.Rows[1].Equipped, Is.False);
        }

        [Test]
        public void select_command_moves_selection_and_raises_changed()
        {
            var recipes = new List<RecipeDef> { Recipe("r1", "One"), Recipe("r2", "Two") };
            var vm = Vm(recipes, new FakeWorkshopLarder());
            int fires = 0; vm.Changed += () => fires++;

            vm.Select("r2");

            Assert.That(vm.SelectedRecipeId, Is.EqualTo("r2"));
            Assert.That(vm.Rows[1].Equipped, Is.True);
            Assert.That(fires, Is.EqualTo(1));
        }

        [Test]
        public void craft_command_forwards_selected_id_and_raises_changed()
        {
            var recipes = new List<RecipeDef> { Recipe("r1", "One") };
            var larder = new FakeWorkshopLarder();
            var vm = Vm(recipes, larder);
            int fires = 0; vm.Changed += () => fires++;

            vm.Craft();

            Assert.That(larder.LastTryCraft, Is.EqualTo("r1"), "Craft forwards the selected recipe id");
            Assert.That(fires, Is.EqualTo(1));
        }

        [Test]
        public void larder_change_re_raises_changed()
        {
            var larder = new FakeWorkshopLarder();
            var vm = Vm(new List<RecipeDef>(), larder);
            int fires = 0; vm.Changed += () => fires++;
            larder.Raise();
            Assert.That(fires, Is.EqualTo(1));
        }

        [Test]
        public void selected_projection_reflects_craftable_flag()
        {
            var recipes = new List<RecipeDef> { Recipe("r1", "One", ("reed", 1)) };
            var larder = new FakeWorkshopLarder();
            larder.Have["reed"] = 1;
            larder.Craftable.Add("r1");
            var vm = Vm(recipes, larder);

            Assert.That(vm.HasSelection, Is.True);
            Assert.That(vm.Selected.CanCraft, Is.True);
            Assert.That(vm.Selected.Ingredients[0].Met, Is.True);
        }
    }
}
