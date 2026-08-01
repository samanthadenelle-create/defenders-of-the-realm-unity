// =============================================================================
// RealmMapVMTests (EditMode) — WO-826 Realm Map catalog + ViewModel suite.
// -----------------------------------------------------------------------------
// Covers the spec's §6 test bar and the VM's state-derivation law:
//   * realm-map.json loads through the REAL RealmMapCatalog (CanonicalJson path):
//     five region ids present; home id maps to the canon title "Elarion".
//   * Fresh save (BestWave 0, empty ledger) -> home first + every region LOCKED.
//   * bestWave gate derives Discovered live (Thornwood at BestWave >= 3).
//   * regionCleared gate chains off the Cleared ledger (Mirewood after Thornwood).
//   * Selection projects the detail (state text + gate reason); Travel is a
//     DISABLED stub until WO-827.
// The VM is bound to the REAL catalog defs + a FAKE progress source (the
// ClanChatVM ISource seam pattern) — no scene, no GameStateService needed.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.World;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class RealmMapVMTests
    {
        private sealed class FakeProgress : RealmMapVM.ISource
        {
            public int BestWave { get; set; }
            public readonly HashSet<string> Discovered = new HashSet<string>();
            public readonly HashSet<string> Cleared = new HashSet<string>();
            bool RealmMapVM.ISource.IsDiscovered(string regionId) => Discovered.Contains(regionId);
            bool RealmMapVM.ISource.IsCleared(string regionId) => Cleared.Contains(regionId);
        }

        private static RealmMapVM MakeVm(FakeProgress progress)
            => new RealmMapVM(progress, RealmMapCatalog.Home, RealmMapCatalog.Regions, onClose: null);

        [SetUp]
        public void FreshCatalog() => RealmMapCatalog.Reload();

        // ── Catalog (spec §6: JSON loads; five region ids; home id -> Elarion) ──

        [Test]
        public void catalog_loads_five_regions_with_expected_ids()
        {
            var ids = new HashSet<string>();
            foreach (var r in RealmMapCatalog.Regions) ids.Add(r.Id);

            Assert.That(RealmMapCatalog.Regions.Count, Is.EqualTo(5), "authored region count");
            Assert.That(ids, Does.Contain("thornwood"));
            Assert.That(ids, Does.Contain("mirewood"));
            Assert.That(ids, Does.Contain("hollowfrost"));
            Assert.That(ids, Does.Contain("emberwastes"));
            Assert.That(ids, Does.Contain("starfall-reach"));
        }

        [Test]
        public void home_id_maps_to_elarion_title()
        {
            Assert.That(RealmMapCatalog.Home, Is.Not.Null, "homeBase parsed");
            Assert.That(RealmMapCatalog.Home.Id, Is.EqualTo("avalon"), "wire id (React save compat)");
            Assert.That(RealmMapCatalog.Home.Title, Is.EqualTo("Elarion"), "canon player-facing title");
            Assert.That(RealmMapCatalog.TitleFor("avalon"), Is.EqualTo("Elarion"));
        }

        [Test]
        public void every_region_has_mappoint_and_known_gate_kind()
        {
            foreach (var r in RealmMapCatalog.Regions)
            {
                Assert.That(r.MapPoint, Is.Not.Null, $"{r.Id} mapPoint");
                Assert.That(r.Gate, Is.Not.Null, $"{r.Id} gate");
                Assert.That(r.Gate.Kind,
                    Is.EqualTo(RealmRegionGate.KindBestWave).Or.EqualTo(RealmRegionGate.KindRegionCleared),
                    $"{r.Id} gate kind is a known union member");
            }
        }

        // ── VM state derivation ─────────────────────────────────────────────────

        [Test]
        public void fresh_save_home_first_and_all_regions_locked()
        {
            var vm = MakeVm(new FakeProgress { BestWave = 0 });

            Assert.That(vm.Nodes.Count, Is.EqualTo(6), "home + 5 regions");
            Assert.That(vm.Nodes[0].IsHome, Is.True, "home renders first");
            Assert.That(vm.Nodes[0].State, Is.EqualTo(RealmMapVM.NodeState.Home));
            for (int i = 1; i < vm.Nodes.Count; i++)
                Assert.That(vm.Nodes[i].State, Is.EqualTo(RealmMapVM.NodeState.Locked),
                    $"region '{vm.Nodes[i].Id}' locked on a fresh save");

            Assert.That(vm.SelectedId, Is.EqualTo("avalon"), "home auto-selected (detail never blank)");
            Assert.That(vm.DetailTitle, Is.EqualTo("Elarion"));
            vm.Dispose();
        }

        [Test]
        public void bestwave_gate_discovers_thornwood_at_wave_three()
        {
            var vm = MakeVm(new FakeProgress { BestWave = 3 });
            Assert.That(StateOf(vm, "thornwood"), Is.EqualTo(RealmMapVM.NodeState.Discovered),
                "bestWave>=3 derives Thornwood discovered");
            Assert.That(StateOf(vm, "mirewood"), Is.EqualTo(RealmMapVM.NodeState.Locked),
                "Mirewood still gated on clearing Thornwood");
            vm.Dispose();
        }

        [Test]
        public void cleared_ledger_chains_the_regioncleared_gate()
        {
            var progress = new FakeProgress { BestWave = 3 };
            progress.Cleared.Add("thornwood");
            var vm = MakeVm(progress);

            Assert.That(StateOf(vm, "thornwood"), Is.EqualTo(RealmMapVM.NodeState.Cleared));
            Assert.That(StateOf(vm, "mirewood"), Is.EqualTo(RealmMapVM.NodeState.Discovered),
                "regionCleared gate satisfied by the Cleared ledger");
            Assert.That(StateOf(vm, "hollowfrost"), Is.EqualTo(RealmMapVM.NodeState.Locked));
            vm.Dispose();
        }

        // ── Selection detail + travel stub ──────────────────────────────────────

        [Test]
        public void selecting_a_locked_region_projects_state_and_gate_text()
        {
            var vm = MakeVm(new FakeProgress { BestWave = 0 });
            bool changed = false;
            vm.Changed += () => changed = true;

            vm.Select("mirewood");

            Assert.That(changed, Is.True, "Select raises Changed");
            Assert.That(vm.SelectedId, Is.EqualTo("mirewood"));
            Assert.That(vm.DetailState, Does.Contain("Locked"), "state is TEXT (colorblind law)");
            Assert.That(vm.DetailGate, Does.Contain("The Thornwood"),
                "gate reason names the prerequisite region");
            Assert.That(vm.DetailBody, Is.Not.Empty, "authored description projected");
            Assert.That(vm.ShowTravel, Is.True, "CTA slot reserved for regions");
            Assert.That(vm.TravelEnabled, Is.False, "travel is a disabled stub until WO-827");
            Assert.That(vm.TravelLabel, Does.Contain("coming with discovery"));
            vm.Dispose();
        }

        [Test]
        public void bestwave_gate_text_names_the_wave_requirement()
        {
            var vm = MakeVm(new FakeProgress { BestWave = 1 });
            vm.Select("thornwood");
            Assert.That(vm.DetailGate, Does.Contain("wave 3"), "bestWave gate reason cites the threshold");
            vm.Dispose();
        }

        [Test]
        public void no_player_facing_string_says_avalon()
        {
            var vm = MakeVm(new FakeProgress());
            foreach (var n in vm.Nodes)
                Assert.That(n.Title, Does.Not.Contain("Avalon"), $"node '{n.Id}' title canon");
            foreach (var id in new[] { "avalon", "thornwood", "mirewood" })
            {
                vm.Select(id);
                Assert.That(vm.DetailTitle, Does.Not.Contain("Avalon"));
                Assert.That(vm.DetailBody, Does.Not.Contain("Avalon"));
            }
            vm.Dispose();
        }

        private static RealmMapVM.NodeState StateOf(RealmMapVM vm, string id)
        {
            foreach (var n in vm.Nodes)
                if (n.Id == id) return n.State;
            Assert.Fail($"node '{id}' not found in vm.Nodes");
            return RealmMapVM.NodeState.Locked;
        }
    }
}
