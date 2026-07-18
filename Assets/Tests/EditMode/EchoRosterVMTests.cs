// =============================================================================
// EchoRosterVMTests (EditMode) -- §2c lock for the Echo roster VM (extends the base).
// -----------------------------------------------------------------------------
// Over a fake IEchoWorkforce (owned count injected) + the REAL static Echo* math
// (GameState-less deterministic defaults): asserts the 6-card projection, the
// locked-card unlock-wave math, owned-card readout shape, and the OpenCard / Assign
// commands. Reuses FakeEchoWorkforce from EchoWorkforceVMTests (same assembly).
// =============================================================================
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EchoRosterVMTests
    {
        private static EchoRosterVM Vm(FakeEchoWorkforce f, System.Action<int> open = null)
            => new EchoRosterVM(f, open ?? (_ => { }), null);

        [Test]
        public void roster_projects_one_card_per_canonical_spirit()
        {
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 1 });
            Assert.That(vm.Cards.Count, Is.EqualTo(EchoRosterCatalog.Count));
        }

        [Test]
        public void owned_and_locked_cards_split_on_owned_count()
        {
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 1, MaxEchoes = 6 });
            Assert.That(vm.Cards[0].Owned, Is.True, "index 0 owned at count 1");
            Assert.That(vm.Cards[1].Owned, Is.False, "index 1 locked at count 1");
        }

        [Test]
        public void locked_card_status_uses_real_unlock_wave_cadence()
        {
            // index K (0-based) unlocks at K * wavesPerEcho.
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 1, WavesPerEcho = 5 });
            Assert.That(vm.Cards[1].StatusText, Is.EqualTo("Locked\nUnlocks at wave 5"));
            Assert.That(vm.Cards[2].StatusText, Is.EqualTo("Locked\nUnlocks at wave 10"));
        }

        [Test]
        public void locked_card_name_is_masked()
        {
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 1 });
            Assert.That(vm.Cards[1].DisplayName, Is.EqualTo("Locked Echo"));
        }

        [Test]
        public void owned_card_shows_real_identity_and_readout()
        {
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 6 });
            var card = vm.Cards[0];
            var entry = EchoRosterCatalog.ByIndex(0);
            Assert.That(card.DisplayName, Is.EqualTo(entry.DisplayName));
            // Status = element + "\n" + lane/level/bonus readout (colorblind-safe TEXT).
            Assert.That(card.StatusText, Does.StartWith(entry.Element + "\n"));
            Assert.That(card.StatusText, Does.Contain("Lv "));
        }

        [Test]
        public void open_card_command_routes_the_tapped_index()
        {
            int captured = -1;
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 6 }, i => captured = i);
            vm.OpenCard(3);
            Assert.That(captured, Is.EqualTo(3));
        }

        [Test]
        public void assign_command_is_safely_rejected_without_gamestate()
        {
            // No GameStateService in EditMode -> EchoAssignments.Assign logs + returns false (never throws).
            var vm = Vm(new FakeEchoWorkforce { EchoCount = 2 });
            Assert.That(vm.Assign(0, EchoAssignments.LaneCrafting), Is.False);
        }

        [Test]
        public void model_changed_rebuilds_cards_and_raises_changed()
        {
            var f = new FakeEchoWorkforce { EchoCount = 1, MaxEchoes = 6 };
            var vm = Vm(f);
            int fires = 0; vm.Changed += () => fires++;

            f.EchoCount = 3;
            f.RaiseChanged();

            Assert.That(fires, Is.EqualTo(1));
            Assert.That(vm.Cards[2].Owned, Is.True, "card 2 owned after count rose to 3");
        }
    }
}
