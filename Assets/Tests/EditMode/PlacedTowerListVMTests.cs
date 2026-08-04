// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// PlacedTowerListVMTests (EditMode) — MVVM Silo C §2c permission gate.
// -----------------------------------------------------------------------------
// Locks TowerManagerPanel + BuildMenu upgrade-screen behaviour in PlacedTowerListVM:
// the list poll + selection persistence + stale-selection drop over an injectable
// resolver (bare Tower components, no scene wiring — EditMode never calls Awake),
// plus the pure row/detail formatters that the Views render.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.UI;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class PlacedTowerListVMTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private Tower NewTower(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<Tower>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        [Test]
        public void empty_scene_has_no_towers()
        {
            var vm = new PlacedTowerListVM(() => new Tower[0]);
            Assert.That(vm.HasTowers, Is.False);
            Assert.That(vm.Selected, Is.Null);
            Assert.That(vm.DetailLine, Is.EqualTo("Select a tower to manage."));
        }

        [Test]
        public void refresh_lists_the_resolved_towers()
        {
            var a = NewTower("Tower-A");
            var b = NewTower("Tower-B");
            var vm = new PlacedTowerListVM(() => new[] { a, b });
            Assert.That(vm.Towers.Count, Is.EqualTo(2));
        }

        [Test]
        public void selection_persists_and_drops_when_the_tower_disappears()
        {
            var a = NewTower("Tower-A");
            var b = NewTower("Tower-B");
            var live = new List<Tower> { a, b };
            var vm = new PlacedTowerListVM(() => live.ToArray());

            vm.Select(a);
            Assert.That(vm.Selected, Is.SameAs(a));

            live.Remove(a);        // a leaves the scene
            vm.Refresh();
            Assert.That(vm.Selected, Is.Null, "a stale selection is dropped on Refresh");
        }

        [Test]
        public void select_raises_changed()
        {
            var a = NewTower("Tower-A");
            var vm = new PlacedTowerListVM(() => new[] { a });
            int changed = 0;
            vm.Changed += () => changed++;
            vm.Select(a);
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void manager_row_formatter_matches_the_legacy_string()
        {
            Assert.That(PlacedTowerListVM.FormatManagerRow(1, 2, 12f, 20f, false),
                Is.EqualTo("Tower 1  -  Lv 2   (rng 12, dmg 20)"));
            Assert.That(PlacedTowerListVM.FormatManagerRow(3, 1, 8.4f, 15.6f, true),
                Is.EqualTo("> Tower 3  -  Lv 1   (rng 8, dmg 16)"));
        }

        [Test]
        public void detail_formatter_matches_the_legacy_string()
        {
            Assert.That(PlacedTowerListVM.FormatDetail(2, 3, 12f, 20f, canUpgrade: true, cost: 50),
                Is.EqualTo("Selected: Lv 2/3  T3   |   rng 12   dmg 20   |   Upgrade: 50 cost"));
            Assert.That(PlacedTowerListVM.FormatDetail(3, 3, 12f, 20f, canUpgrade: false, cost: 0),
                Is.EqualTo("Selected: Lv 3/3  T3   |   rng 12   dmg 20   |   Max Level"));
        }

        [Test]
        public void menu_row_formatter_strips_the_tower_prefix()
        {
            Assert.That(PlacedTowerListVM.FormatMenuRow("Tower-Archer", 2, false),
                Is.EqualTo("Archer  (Lvl 2/3)"));
            Assert.That(PlacedTowerListVM.FormatMenuRow("Tower_Ballista", 1, true),
                Is.EqualTo("> Ballista  (Lvl 1/3)"));
        }

        // 2026-08-04 — the store-capture defect: the upgrade row labelled itself with the
        // GAMEOBJECT name, so raw identifiers ("Stone4", "DevTower", a prefab's "(Clone)")
        // reached the player as display names. The formatter now renders identifier shapes
        // as English, and leaves an already-clean name untouched.
        [Test]
        public void menu_row_formatter_renders_identifier_names_as_english()
        {
            Assert.That(PlacedTowerListVM.PrettifyTowerName("Tower_Stone4"), Is.EqualTo("Stone 4"));
            Assert.That(PlacedTowerListVM.PrettifyTowerName("DevTower"), Is.EqualTo("Dev Tower"));
            Assert.That(PlacedTowerListVM.PrettifyTowerName("Tower_Archer(Clone)"), Is.EqualTo("Archer"));
            // Idempotent: an authored display name survives unchanged.
            Assert.That(PlacedTowerListVM.PrettifyTowerName("Archer Tower"), Is.EqualTo("Archer Tower"));
            Assert.That(PlacedTowerListVM.PrettifyTowerName("Frost Tower"), Is.EqualTo("Frost Tower"));
            // A nameless object still reads as something a player can parse.
            Assert.That(PlacedTowerListVM.PrettifyTowerName(""), Is.EqualTo("Tower"));
            Assert.That(PlacedTowerListVM.PrettifyTowerName(null), Is.EqualTo("Tower"));
        }

        // A tower still being raised has no TowerData, so it has no level and no stats.
        // Printing "Lvl 1/3" (and, downstream, "0 dmg / 0m") for it is a fabricated reading.
        [Test]
        public void an_unbuilt_tower_reports_no_level_and_reads_as_building()
        {
            var t = NewTower("Tower_Archer");
            var vm = new PlacedTowerListVM(() => new[] { t });

            Assert.That(vm.IsBuilt(t), Is.False, "a tower with no TowerData is not finished");
            Assert.That(vm.LevelOf(t), Is.EqualTo(0));
            Assert.That(vm.DisplayNameFor(t), Is.EqualTo("Archer"),
                "with no authored name the cleaned object name is the fallback");
            Assert.That(PlacedTowerListVM.FormatMenuRow(vm.DisplayNameFor(t), vm.LevelOf(t), false, vm.IsBuilt(t)),
                Is.EqualTo("Archer  (building)"));

            vm.Dispose();
        }
    }
}
