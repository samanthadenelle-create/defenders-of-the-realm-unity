// =============================================================================
// LevelUpVMTests (EditMode) — §2c lock for the level-up skill-point VM.
// -----------------------------------------------------------------------------
// Over a fake ILevelUpModel (no scene / no SkillSystem / no HeroProgression):
// asserts the points/pill/button-label projection, the level>=2 auto-show gate,
// the hero-level>=2 collapse gate, the Spend command mutation + should-hide
// return + Changed, and that model relays (SkillsChanged / LeveledUp) re-raise.
// =============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Data;
using DeNelle.Village.UI;

namespace DeNelle.Tests.EditMode
{
    /// <summary>Fake skill-point/hero-level model with settable state + manual raises.</summary>
    internal sealed class FakeLevelUpModel : ILevelUpModel
    {
        public int Points = 2;
        public int Level = 1;
        public readonly Dictionary<SkillType, int> Levels = new Dictionary<SkillType, int>();
        public int SpendCalls;
        public SkillType LastSpent;

        public int AvailablePoints => Points;
        public int HeroLevel => Level;
        public int SkillLevel(SkillType type) => Levels.TryGetValue(type, out var v) ? v : 0;

        public bool Spend(SkillType type)
        {
            SpendCalls++;
            LastSpent = type;
            if (Points <= 0) return false;
            Points--;
            Levels[type] = SkillLevel(type) + 1;
            SkillsChanged?.Invoke();   // mirror the real SkillSystem event
            return true;
        }

        public event Action SkillsChanged;
        public event Action<int> LeveledUp;
        public void RaiseSkillsChanged() => SkillsChanged?.Invoke();
        public void RaiseLeveledUp(int n) => LeveledUp?.Invoke(n);
    }

    [TestFixture]
    public class LevelUpVMTests
    {
        [Test]
        public void projects_points_and_pill_copy()
        {
            var vm = new LevelUpVM(new FakeLevelUpModel { Points = 3 });
            Assert.That(vm.AvailablePoints, Is.EqualTo(3));
            Assert.That(vm.PointsLine, Is.EqualTo("Available points: 3"));
            Assert.That(vm.PillText, Is.EqualTo("3 skill points — Spend"));
            Assert.That(vm.CanSpend, Is.True);
        }

        [Test]
        public void pill_copy_singular_at_one_point()
        {
            var vm = new LevelUpVM(new FakeLevelUpModel { Points = 1 });
            Assert.That(vm.PillText, Is.EqualTo("1 skill point — Spend"));
        }

        [Test]
        public void cannot_spend_at_zero_points()
        {
            var vm = new LevelUpVM(new FakeLevelUpModel { Points = 0 });
            Assert.That(vm.CanSpend, Is.False);
        }

        [Test]
        public void skill_button_label_reads_model_level()
        {
            var f = new FakeLevelUpModel();
            f.Levels[SkillType.Blacksmith] = 4;
            var vm = new LevelUpVM(f);
            Assert.That(vm.SkillButtonLabel("Blacksmith", SkillType.Blacksmith), Is.EqualTo("Blacksmith  (Lv 4)   +"));
            Assert.That(vm.SkillButtonLabel("Arcane", SkillType.Arcane), Is.EqualTo("Arcane  (Lv 0)   +"));
        }

        [Test]
        public void auto_show_gate_suppresses_level_1_opens_level_2_plus()
        {
            var vm = new LevelUpVM(new FakeLevelUpModel());
            Assert.That(vm.ShouldAutoShow(1), Is.False, "level 1 is the baseline, no auto-popup");
            Assert.That(vm.ShouldAutoShow(2), Is.True);
            Assert.That(vm.ShouldAutoShow(7), Is.True);
        }

        [Test]
        public void hero_at_level_2_plus_tracks_hero_level()
        {
            Assert.That(new LevelUpVM(new FakeLevelUpModel { Level = 1 }).HeroAtLevel2Plus, Is.False);
            Assert.That(new LevelUpVM(new FakeLevelUpModel { Level = 2 }).HeroAtLevel2Plus, Is.True);
        }

        [Test]
        public void spend_mutates_model_and_raises_changed()
        {
            var f = new FakeLevelUpModel { Points = 2 };
            var vm = new LevelUpVM(f);
            int fires = 0; vm.Changed += () => fires++;

            bool shouldHide = vm.Spend(SkillType.Woodworking);

            Assert.That(f.SpendCalls, Is.EqualTo(1));
            Assert.That(f.LastSpent, Is.EqualTo(SkillType.Woodworking));
            Assert.That(vm.AvailablePoints, Is.EqualTo(1));
            Assert.That(shouldHide, Is.False, "a point remains -> stay open");
            Assert.That(fires, Is.GreaterThanOrEqualTo(1), "Spend fires Changed (via the model relay)");
        }

        [Test]
        public void spend_last_point_signals_hide()
        {
            var f = new FakeLevelUpModel { Points = 1 };
            var vm = new LevelUpVM(f);
            bool shouldHide = vm.Spend(SkillType.Arcane);
            Assert.That(vm.AvailablePoints, Is.EqualTo(0));
            Assert.That(shouldHide, Is.True, "no points left -> the View should hide");
        }

        [Test]
        public void model_skills_changed_re_raises_changed()
        {
            var f = new FakeLevelUpModel();
            var vm = new LevelUpVM(f);
            int fires = 0; vm.Changed += () => fires++;
            f.RaiseSkillsChanged();
            Assert.That(fires, Is.EqualTo(1));
        }

        [Test]
        public void leveled_up_is_re_raised_and_raises_changed()
        {
            var f = new FakeLevelUpModel();
            var vm = new LevelUpVM(f);
            int got = -1, fires = 0;
            vm.LeveledUp += n => got = n;
            vm.Changed += () => fires++;
            f.RaiseLeveledUp(5);
            Assert.That(got, Is.EqualTo(5));
            Assert.That(fires, Is.EqualTo(1), "a level-up also repaints");
        }

        [Test]
        public void dispose_detaches_from_model_events()
        {
            var f = new FakeLevelUpModel();
            var vm = new LevelUpVM(f);
            int fires = 0; vm.Changed += () => fires++;
            vm.Dispose();
            f.RaiseSkillsChanged();
            f.RaiseLeveledUp(3);
            Assert.That(fires, Is.EqualTo(0), "no fires after Dispose");
        }
    }
}
