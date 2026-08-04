// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// BuildMenuVMTests (EditMode) — MVVM Silo C §2c permission gate.
// -----------------------------------------------------------------------------
// Locks BuildMenu's crystal-balance read in BuildMenuVM: Crystals sources the
// IEconomy.Crystals store, falls back to the standalone value when no economy,
// and the shared tower list VM is wired. Over a fake IEconomy (the injectable
// ctor — CreateDefault's service/scene resolution is not exercised here).
//
// 2026-08-04 — plus the tower the build menu PLACES. The menu used to load one
// pinned asset (Towers/DevTower) for every row, so the pick decided the price and
// nothing else. These cases lock the selection -> asset mapping both purely (the
// name/id match, no Resources) and against the real shipped assets.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.UI;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class BuildMenuVMTests
    {
        private static PlacedTowerListVM EmptyTowers()
            => new PlacedTowerListVM(() => new Tower[0]);

        [Test]
        public void crystals_read_from_the_economy()
        {
            var vm = new BuildMenuVM(new FakeEconomy { Crystals = 42 }, EmptyTowers(), null, fallbackCrystals: 7, onClose: null);
            Assert.That(vm.Crystals, Is.EqualTo(42));
        }

        [Test]
        public void crystals_fall_back_when_no_economy()
        {
            var vm = new BuildMenuVM(null, EmptyTowers(), null, fallbackCrystals: 7, onClose: null);
            Assert.That(vm.Crystals, Is.EqualTo(7));
        }

        [Test]
        public void the_shared_tower_list_is_wired()
        {
            var towers = EmptyTowers();
            var vm = new BuildMenuVM(new FakeEconomy(), towers, null, 0, null);
            Assert.That(vm.Towers, Is.SameAs(towers));
        }

        [Test]
        public void dispose_disposes_the_tower_list_without_throwing()
        {
            var vm = new BuildMenuVM(new FakeEconomy(), EmptyTowers(), null, 0, null);
            Assert.DoesNotThrow(() => vm.Dispose());
        }

        // =====================================================================
        //  The picked row -> the tower that gets raised (owner ruling 2026-08-04)
        // =====================================================================

        /// <summary>The four TowerData assets shipped in Resources/Towers, as the resolver sees them.</summary>
        private static List<BuildMenuVM.TowerAssetCandidate> ShippedTowerAssets()
            => new List<BuildMenuVM.TowerAssetCandidate>
            {
                new BuildMenuVM.TowerAssetCandidate("ArcherTower", "Archer Tower"),
                new BuildMenuVM.TowerAssetCandidate("DevTower",    "DevTower"),
                new BuildMenuVM.TowerAssetCandidate("FrostTower",  "Frost Tower"),
                new BuildMenuVM.TowerAssetCandidate("MageTower",   "Mage Tower"),
            };

        // THE DEFECT: the catalog row the player taps ("Archer Tower", id tower_ground_archer)
        // must resolve to that row's OWN asset. Before the fix every row resolved to DevTower.
        [Test]
        public void the_picked_catalog_row_resolves_its_own_tower_asset()
        {
            Assert.That(
                BuildMenuVM.ResolveTowerAssetName("tower_ground_archer", "Archer Tower", ShippedTowerAssets()),
                Is.EqualTo("ArcherTower"));
        }

        // The match must survive the id/display-name spelling differences the catalog uses:
        // spaces, underscores, casing and run-together words all name the same tower.
        [Test]
        public void the_match_ignores_spacing_underscores_and_casing()
        {
            var assets = ShippedTowerAssets();
            Assert.That(BuildMenuVM.ResolveTowerAssetName("frost_tower", "frost tower", assets), Is.EqualTo("FrostTower"));
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_mage", "MAGE TOWER", assets), Is.EqualTo("MageTower"));
            Assert.That(BuildMenuVM.ResolveTowerAssetName("archertower", null, assets), Is.EqualTo("ArcherTower"));
        }

        // A row whose display name IS an asset's name must not be stolen by a token match on a
        // different asset — the exact name is the stronger evidence and is checked first.
        [Test]
        public void an_exact_name_beats_a_token_match_on_another_asset()
        {
            var assets = new List<BuildMenuVM.TowerAssetCandidate>
            {
                new BuildMenuVM.TowerAssetCandidate("ArcherTower", "Archer Tower"),
                new BuildMenuVM.TowerAssetCandidate("FrostTower",  "Frost Tower"),
            };
            // The id names archer; the display name IS the Frost Tower. The display name wins.
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_archer_frost", "Frost Tower", assets),
                Is.EqualTo("FrostTower"));
        }

        // A catalog row that no TowerData was authored for must resolve NOTHING, so the caller
        // takes the traced DevTower fallback instead of silently claiming a match.
        [Test]
        public void a_row_with_no_authored_asset_resolves_nothing()
        {
            var assets = ShippedTowerAssets();
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_wall_wizard", "Ballista", assets), Is.Null);
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_arcane_spire", "Arcane Spire", assets), Is.Null);
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_catapult", "Catapult", assets), Is.Null);
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_siege_tower", "Sky Ballista (Anti-Air)", assets), Is.Null);
        }

        // DevTower reduces to the 3-letter token "dev", which is under the token floor. If it were
        // allowed to token-match it could claim rows that named a different tower — which is the
        // exact failure being fixed, re-introduced through the back door.
        [Test]
        public void the_fallback_asset_never_wins_a_token_match()
        {
            var assets = ShippedTowerAssets();
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_ground_archer", "Archer Tower", assets),
                Is.Not.EqualTo("DevTower"));
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_dev_harness", "Dev Harness", assets), Is.Null);
            // Asked for by its own name, it still resolves — it is a real asset, just not a magnet.
            Assert.That(BuildMenuVM.ResolveTowerAssetName("devtower", "DevTower", assets), Is.EqualTo("DevTower"));
        }

        [Test]
        public void no_candidates_resolves_nothing_rather_than_throwing()
        {
            Assert.That(BuildMenuVM.ResolveTowerAssetName("tower_ground_archer", "Archer Tower", null), Is.Null);
            Assert.That(BuildMenuVM.ResolveTowerAssetName(null, null, ShippedTowerAssets()), Is.Null);
        }

        // END-TO-END over the REAL shipped assets, through the method the View calls: the id the
        // Build-Tower screen holds for the Archer Tower row resolves the Archer Tower ASSET, and
        // the raise time printed beside it is that asset's own buildTime — not the pinned
        // DevTower's. This is the case that fails on the pre-fix code.
        [Test]
        public void the_id_the_view_holds_places_that_tower_and_prints_its_raise_time()
        {
            var archer = Resources.Load<DeNelle.Core.Data.TowerData>("Towers/ArcherTower");
            Assert.That(archer, Is.Not.Null,
                "Assets/Resources/Towers/ArcherTower.asset is tracked in the repo and must load in EditMode");

            var vm = new BuildMenuVM(new FakeEconomy(), EmptyTowers(), null, 0, null);
            var placed = vm.PlacedTowerDataFor("tower_ground_archer");

            Assert.That(placed, Is.SameAs(archer),
                "the picked row must place its OWN asset, not the Towers/DevTower fallback");
            Assert.That(vm.BuildSecondsFor("tower_ground_archer"),
                Is.EqualTo(Mathf.Max(0, Mathf.RoundToInt(archer.buildTime))),
                "the printed raise time reads the SAME asset the placement is handed");
            vm.Dispose();
        }

        // Nothing picked => the fallback, never null: no path through the menu may end up with no
        // tower to place at all.
        [Test]
        public void no_selection_falls_back_to_the_dev_tower()
        {
            var dev = Resources.Load<DeNelle.Core.Data.TowerData>(BuildMenuVM.FallbackTowerResourcePath);
            Assert.That(dev, Is.Not.Null, "the fallback asset is tracked in the repo and must load");

            var vm = new BuildMenuVM(new FakeEconomy(), EmptyTowers(), null, 0, null);
            Assert.That(vm.PlacedTowerDataFor(null), Is.SameAs(dev));
            Assert.That(vm.PlacedTowerDataFor(""), Is.SameAs(dev));
            vm.Dispose();
        }

        // A catalog row with no authored TowerData still places SOMETHING (the fallback) rather
        // than dead-ending the build — the residual gap the FlowTrace warn names at runtime.
        [Test]
        public void an_unauthored_row_still_resolves_the_fallback_rather_than_nothing()
        {
            var dev = Resources.Load<DeNelle.Core.Data.TowerData>(BuildMenuVM.FallbackTowerResourcePath);
            var vm = new BuildMenuVM(new FakeEconomy(), EmptyTowers(), null, 0, null);
            Assert.That(vm.PlacedTowerDataFor("tower_arcane_spire"), Is.SameAs(dev));
            vm.Dispose();
        }
    }
}
