// =============================================================================
// DailyQuestVMTests (EditMode) — §2c permission gate for the daily-quest MVVM slice.
// -----------------------------------------------------------------------------
// Locks the behavior MOVED out of DailyQuestHud (the View) into the pure DailyQuestVM,
// so the View swap is safe only while these stay green. Uses a FAKE ISource so the VM
// runs with NO scene, NO DailyQuestService singleton, NO DailyQuestCatalog.
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Quests;
using DeNelle.HUD;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class DailyQuestVMTests
    {
        private sealed class FakeSource : DailyQuestVM.ISource
        {
            public readonly List<DailyQuestInstance> Quests = new List<DailyQuestInstance>();
            public readonly Dictionary<string, DailyQuestSlotReward> Rewards = new Dictionary<string, DailyQuestSlotReward>();
            public int RerollCalls;

            public event Action Changed;
            public IReadOnlyList<DailyQuestInstance> TodayQuests => Quests;
            public DailyQuestSlotReward RewardForSlot(string slot)
                => slot != null && Rewards.TryGetValue(slot, out var r) ? r : null;
            public DailyQuestInstance Reroll(string slot) { RerollCalls++; return null; }
            public void RaiseChanged() => Changed?.Invoke();
        }

        private static FakeSource TwoQuests()
        {
            var s = new FakeSource();
            s.Quests.Add(new DailyQuestInstance
            { Id = "q1", TemplateId = "combat.a", Slot = "combat", Target = 5, Progress = 2, Completed = false, Label = "Clear {target} waves" });
            s.Quests.Add(new DailyQuestInstance
            { Id = "q2", TemplateId = "explore.b", Slot = "exploration", Target = 1, Progress = 1, Completed = true, Label = "Find the shrine" });
            s.Rewards["combat"] = new DailyQuestSlotReward
            { Slot = "combat", RewardCrystals = 25, RewardRandomItem = true };
            return s;
        }

        [Test]
        public void projection_matches_service_and_catalog()
        {
            using var vm = new DailyQuestVM(TwoQuests(), null);

            Assert.That(vm.Quests.Count, Is.EqualTo(2));
            Assert.That(vm.Quests[0].Name, Is.EqualTo("Clear 5 waves"), "{target} must be substituted in the label");
            Assert.That(vm.Quests[0].Equipped, Is.False);
            Assert.That(vm.Quests[1].Equipped, Is.True, "Equipped carries Completed");
            Assert.That(vm.ProgressText("q1"), Is.EqualTo("2 / 5"));
            Assert.That(vm.FlavorFor("q1"), Does.Contain("Combat"));

            var r = vm.RewardFor("q1");
            Assert.That(r.Crystals, Is.EqualTo(25));
            Assert.That(r.RandomItem, Is.True);
        }

        [Test]
        public void selection_defaults_to_first_and_select_mutates_and_fires_changed()
        {
            using var vm = new DailyQuestVM(TwoQuests(), null);
            Assert.That(vm.SelectedId, Is.EqualTo("q1"), "selection defaults to the first quest");

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Select("q2");
            Assert.That(vm.SelectedId, Is.EqualTo("q2"));
            Assert.That(changed, Is.GreaterThan(0), "Select must raise Changed");

            int before = changed;
            vm.Select("nope");
            Assert.That(vm.SelectedId, Is.EqualTo("q2"), "unknown id must not change selection");
            Assert.That(changed, Is.EqualTo(before), "unknown id must not raise Changed");
        }

        [Test]
        public void source_change_rebuilds_and_preserves_valid_selection()
        {
            var src = TwoQuests();
            using var vm = new DailyQuestVM(src, null);
            vm.Select("q2");

            src.Quests[0].Progress = 4;
            src.RaiseChanged();

            Assert.That(vm.ProgressText("q1"), Is.EqualTo("4 / 5"), "rebuild reprojects progress");
            Assert.That(vm.SelectedId, Is.EqualTo("q2"), "a still-present selection survives rebuild");
        }

        [Test]
        public void empty_set_is_empty_with_null_selection()
        {
            using var vm = new DailyQuestVM(new FakeSource(), null);
            Assert.That(vm.IsEmpty, Is.True);
            Assert.That(vm.SelectedId, Is.Null);
            Assert.That(vm.Quests.Count, Is.EqualTo(0));
        }

        // ── WO-879: the VM owns the SINGLE empty-state; the View renders what it says ──

        [Test]
        public void empty_state_is_one_vm_fact_carrying_its_own_copy()
        {
            using var vm = new DailyQuestVM(new FakeSource(), null);

            var empty = vm.EmptyState;
            Assert.That(empty.Active, Is.True, "an empty set must raise the VM's empty-state");
            Assert.That(empty.Headline, Is.Not.Null.And.Not.Empty,
                "the empty-state COPY belongs to the VM - a View that has to type its own headline " +
                "is how WO-879 shipped the message twice in two chromes");
            Assert.That(empty.Detail, Is.Not.Null.And.Not.Empty, "the supporting line is the VM's too");
            Assert.That(vm.IsEmpty, Is.EqualTo(empty.Active),
                "IsEmpty must be a PROJECTION of the one empty-state fact, not a second computation");
        }

        [Test]
        public void empty_state_is_inactive_and_blank_while_quests_exist()
        {
            using var vm = new DailyQuestVM(TwoQuests(), null);

            var empty = vm.EmptyState;
            Assert.That(empty.Active, Is.False, "a populated set must not raise the empty-state");
            Assert.That(empty.Headline, Is.Empty,
                "with quests present the empty copy must be BLANK, so a View that renders the fact " +
                "unconditionally draws nothing (one render path, never a stale second message)");
            Assert.That(empty.Detail, Is.Empty);
            Assert.That(vm.IsEmpty, Is.False);
        }

        [Test]
        public void empty_state_flips_with_the_source_and_stays_a_single_fact()
        {
            var src = TwoQuests();
            using var vm = new DailyQuestVM(src, null);
            Assert.That(vm.EmptyState.Active, Is.False);

            src.Quests.Clear();
            src.RaiseChanged();

            Assert.That(vm.EmptyState.Active, Is.True, "clearing the source must raise the empty-state");
            Assert.That(vm.IsEmpty, Is.True, "IsEmpty and EmptyState.Active can never disagree");
            using (var bornEmpty = new DailyQuestVM(new FakeSource(), null))
                Assert.That(vm.EmptyState.Headline, Is.EqualTo(bornEmpty.EmptyState.Headline),
                    "the empty headline is one authored constant, identical on every path");
            Assert.That(vm.SelectedId, Is.Null);
            Assert.That(vm.TryGetSelected(out _), Is.False, "no selection exists while empty");
        }

        [Test]
        public void try_get_selected_always_resolves_while_quests_exist()
        {
            var src = TwoQuests();
            using var vm = new DailyQuestVM(src, null);

            // THE invariant that makes the View's old "Select a quest" empty-state unreachable:
            // a non-empty set ALWAYS has a resolvable selection, so the View needs exactly one
            // empty branch (the VM's), not two.
            Assert.That(vm.TryGetSelected(out var first), Is.True);
            Assert.That(first.Id, Is.EqualTo("q1"));

            vm.Select("q2");
            Assert.That(vm.TryGetSelected(out var second), Is.True);
            Assert.That(second.Id, Is.EqualTo("q2"));

            vm.Select("nope");
            Assert.That(vm.TryGetSelected(out var stillSecond), Is.True,
                "a rejected Select must never leave the VM without a selection");
            Assert.That(stillSecond.Id, Is.EqualTo("q2"));

            src.Quests.RemoveAt(1);          // the selected quest disappears
            src.RaiseChanged();
            Assert.That(vm.TryGetSelected(out var reseated), Is.True,
                "rebuild must re-seat the selection, never leave a non-empty set unselected");
            Assert.That(reseated.Id, Is.EqualTo("q1"));
        }

        [Test]
        public void reroll_delegates_to_source()
        {
            var src = TwoQuests();
            using var vm = new DailyQuestVM(src, null);
            vm.Reroll("combat");
            Assert.That(src.RerollCalls, Is.EqualTo(1));
        }

        [Test]
        public void dispose_unsubscribes_no_callback_after_dispose()
        {
            var src = TwoQuests();
            var vm = new DailyQuestVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;
            vm.Dispose();

            int before = changed;
            src.RaiseChanged();
            Assert.That(changed, Is.EqualTo(before), "after Dispose the VM must not raise Changed");
        }
    }
}
