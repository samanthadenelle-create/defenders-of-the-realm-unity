// =============================================================================
// DungeonHudVMTests (EditMode) — §2c lock for the dungeon HUD lantern VM.
// -----------------------------------------------------------------------------
// Over a fake ILanternReadout (no scene / no Lantern MonoBehaviour): asserts the
// idle (no-lantern) readout, the bar fraction, the low / critical / warning band
// split, the low-oil pill flag, the burn-time label copy, and that SetLantern
// raises Changed. Locks the band logic that moved OUT of DungeonHudController.
// =============================================================================
using NUnit.Framework;
using DeNelle.Dungeons;

namespace DeNelle.Tests.EditMode
{
    /// <summary>Fake lantern seam with directly-settable readout values.</summary>
    internal sealed class FakeLanternReadout : ILanternReadout
    {
        public float OilFraction { get; set; } = 1f;
        public bool IsLowOil { get; set; }
        public float EstimatedSecondsRemaining { get; set; } = float.PositiveInfinity;
    }

    [TestFixture]
    public class DungeonHudVMTests
    {
        [Test]
        public void idle_readout_when_no_lantern_bound()
        {
            var vm = new DungeonHudVM();
            Assert.That(vm.HasLantern, Is.False);
            Assert.That(vm.BarFraction, Is.EqualTo(1f));
            Assert.That(vm.IsLow, Is.False);
            Assert.That(vm.IsCritical, Is.False);
            Assert.That(vm.IsWarning, Is.False);
            Assert.That(vm.ShowLowWarning, Is.False);
            Assert.That(vm.TimeLabel, Is.EqualTo("Light: --"));
        }

        [Test]
        public void bar_fraction_projects_and_clamps_oil()
        {
            var vm = new DungeonHudVM();
            vm.SetLantern(new FakeLanternReadout { OilFraction = 0.6f });
            Assert.That(vm.BarFraction, Is.EqualTo(0.6f).Within(1e-5f));

            vm.SetLantern(new FakeLanternReadout { OilFraction = 1.5f });
            Assert.That(vm.BarFraction, Is.EqualTo(1f), "over-full oil clamps to 1");
            vm.SetLantern(new FakeLanternReadout { OilFraction = -0.2f });
            Assert.That(vm.BarFraction, Is.EqualTo(0f), "negative oil clamps to 0");
        }

        [Test]
        public void warning_band_is_low_but_not_yet_critical()
        {
            // Low oil, fraction above the critical threshold (default 0.1) -> amber warning.
            var vm = new DungeonHudVM();
            vm.SetLantern(new FakeLanternReadout { OilFraction = 0.2f, IsLowOil = true });
            Assert.That(vm.IsLow, Is.True);
            Assert.That(vm.IsCritical, Is.False);
            Assert.That(vm.IsWarning, Is.True);
            Assert.That(vm.ShowLowWarning, Is.True);
        }

        [Test]
        public void critical_band_at_or_below_threshold_suppresses_warning()
        {
            var vm = new DungeonHudVM(0.1f);
            vm.SetLantern(new FakeLanternReadout { OilFraction = 0.1f, IsLowOil = true });
            Assert.That(vm.IsCritical, Is.True, "fraction == threshold is critical");
            Assert.That(vm.IsWarning, Is.False, "critical is not the amber warning band");
            Assert.That(vm.ShowLowWarning, Is.True, "the low-oil pill still shows when critical");
        }

        [Test]
        public void not_low_means_no_bands_even_if_fraction_low()
        {
            // Guard: critical/warning gate on IsLowOil, not the raw fraction alone.
            var vm = new DungeonHudVM();
            vm.SetLantern(new FakeLanternReadout { OilFraction = 0.05f, IsLowOil = false });
            Assert.That(vm.IsLow, Is.False);
            Assert.That(vm.IsWarning, Is.False);
            Assert.That(vm.ShowLowWarning, Is.False);
            Assert.That(vm.IsCritical, Is.True, "critical is a pure fraction band (paints red regardless)");
        }

        [Test]
        public void burn_time_label_formats_minutes_seconds_and_steady()
        {
            Assert.That(DungeonHudVM.FormatBurnTime(float.PositiveInfinity), Is.EqualTo("Light: steady"));
            Assert.That(DungeonHudVM.FormatBurnTime(24f), Is.EqualTo("Light: 24s"));
            Assert.That(DungeonHudVM.FormatBurnTime(72f), Is.EqualTo("Light: 1m 12s"));

            var vm = new DungeonHudVM();
            vm.SetLantern(new FakeLanternReadout { EstimatedSecondsRemaining = 90f });
            Assert.That(vm.TimeLabel, Is.EqualTo("Light: 1m 30s"));
        }

        [Test]
        public void final_warning_progress_is_continuous_over_last_thirty_seconds()
        {
            var fake = new FakeLanternReadout { EstimatedSecondsRemaining = 45f };
            var vm = new DungeonHudVM();
            vm.SetLantern(fake);
            Assert.That(vm.FinalWarningProgress, Is.EqualTo(0f), "warning is dormant above 30s");

            fake.EstimatedSecondsRemaining = 15f;
            Assert.That(vm.FinalWarningProgress, Is.EqualTo(0.5f).Within(0.001f));

            fake.EstimatedSecondsRemaining = 0f;
            Assert.That(vm.FinalWarningProgress, Is.EqualTo(1f));
        }

        [Test]
        public void set_lantern_raises_changed()
        {
            var vm = new DungeonHudVM();
            int fires = 0; vm.Changed += () => fires++;
            vm.SetLantern(new FakeLanternReadout());
            Assert.That(fires, Is.EqualTo(1));
            vm.SetLantern(null);
            Assert.That(fires, Is.EqualTo(2), "clearing the lantern also raises Changed");
        }
    }
}
