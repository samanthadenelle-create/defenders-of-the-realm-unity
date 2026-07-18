// =============================================================================
// ArenaPaletteVMTests (EditMode) — §2c permission gate for the shared Arena palette
// MVVM slice. Locks the card projection + budget/affordability edges that MOVED out
// of ArenaAttackPaletteUI / ArenaDefensePaletteUI into the pure ArenaPaletteVM.
// Uses a FAKE def list (no scene, no ArenaDefenseCatalog singleton).
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DeNelle.Village.Arena;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ArenaPaletteVMTests
    {
        private const int Pool = 50;

        private static List<ArenaDefenseDef> Defs() => new List<ArenaDefenseDef>
        {
            new ArenaDefenseDef { Id = "def_ranger", DisplayName = "Ranger", PointCost = 5 },
            new ArenaDefenseDef { Id = "def_knight", DisplayName = "Knight", PointCost = 10 },
            new ArenaDefenseDef { Id = "def_ballista", DisplayName = "Ballista", PointCost = 20 },
        };

        [Test]
        public void cards_project_catalog_name_and_cost()
        {
            using var vm = new ArenaPaletteVM(ArenaPaletteMode.Defense, Defs(), Pool, null);
            Assert.That(vm.Cards.Count, Is.EqualTo(3));
            Assert.That(vm.Cards[0].Id, Is.EqualTo("def_ranger"));
            Assert.That(vm.Cards[0].Name, Is.EqualTo("Ranger"));
            Assert.That(vm.Cards[0].Price, Is.EqualTo(5));
            Assert.That(vm.Cards[2].Price, Is.EqualTo(20));
        }

        [Test]
        public void full_pool_makes_everything_affordable()
        {
            using var vm = new ArenaPaletteVM(ArenaPaletteMode.Attack, Defs(), Pool, null);
            // Fresh VM defaults Remaining to the full pool.
            foreach (var c in vm.Cards) Assert.That(c.Affordable, Is.True);
        }

        [Test]
        public void set_budget_flips_affordability_and_fires_changed()
        {
            using var vm = new ArenaPaletteVM(ArenaPaletteMode.Attack, Defs(), Pool, null);
            int changed = 0; vm.Changed += () => changed++;

            // Only 8 points remain: Ranger(5) fits, Knight(10)/Ballista(20) do not.
            vm.SetBudget(spent: 42, remaining: 8, squadCount: 3);

            Assert.That(changed, Is.GreaterThan(0), "SetBudget must raise Changed");
            Assert.That(vm.Cards[0].Affordable, Is.True,  "Ranger(5) fits 8 remaining");
            Assert.That(vm.Cards[1].Affordable, Is.False, "Knight(10) exceeds 8 remaining");
            Assert.That(vm.Cards[2].Affordable, Is.False, "Ballista(20) exceeds 8 remaining");
            Assert.That(vm.PointsLabel, Does.Contain("42 / 50"));
            Assert.That(vm.PointsLabel, Does.Contain("3 units"));
        }

        [Test]
        public void defense_armed_card_stays_affordable_even_when_pool_spent()
        {
            using var vm = new ArenaPaletteVM(ArenaPaletteMode.Defense, Defs(), Pool, null);
            vm.SetBudget(spent: 50, remaining: 0, squadCount: 0);
            // Nothing fits 0 remaining...
            foreach (var c in vm.Cards) Assert.That(c.Affordable, Is.False);

            // ...but arming a card keeps it re-tappable (affordable + highlighted).
            int changed = 0; vm.Changed += () => changed++;
            vm.Arm("def_knight");
            Assert.That(changed, Is.GreaterThan(0), "Arm must raise Changed");
            var knight = vm.Cards.First(c => c.Id == "def_knight");
            Assert.That(knight.Affordable, Is.True, "the armed card stays affordable at 0 remaining");
            Assert.That(knight.Equipped, Is.True, "the armed card reads as highlighted");
        }

        [Test]
        public void attack_mode_has_no_armed_highlight()
        {
            using var vm = new ArenaPaletteVM(ArenaPaletteMode.Attack, Defs(), Pool, null);
            vm.Arm("def_knight");   // no-op highlight in Attack mode
            foreach (var c in vm.Cards) Assert.That(c.Equipped, Is.False);
            Assert.That(vm.PointsLabel, Does.StartWith("Squad Points:"));
        }

        [Test]
        public void def_for_returns_the_backing_catalog_def()
        {
            using var vm = new ArenaPaletteVM(ArenaPaletteMode.Defense, Defs(), Pool, null);
            Assert.That(vm.DefFor("def_ballista"), Is.Not.Null);
            Assert.That(vm.DefFor("def_ballista").PointCost, Is.EqualTo(20));
            Assert.That(vm.DefFor("nope"), Is.Null);
        }

        [Test]
        public void dispose_unsubscribes_no_callback_after_dispose()
        {
            var vm = new ArenaPaletteVM(ArenaPaletteMode.Attack, Defs(), Pool, null);
            int changed = 0; vm.Changed += () => changed++;
            vm.Dispose();
            int before = changed;
            vm.SetBudget(0, 10);   // must not raise after dispose
            Assert.That(changed, Is.EqualTo(before));
        }
    }
}
