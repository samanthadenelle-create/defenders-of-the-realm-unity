// =============================================================================
// QuestTrackerVMTests (EditMode) — §2c gate for the quest-tracker MVVM slice.
// -----------------------------------------------------------------------------
// Locks the WO-454 tracked-quest resolution MOVED out of QuestTrackerHud into the
// pure QuestTrackerVM. FAKE ISource — no scene, no QuestService, no QuestCatalog.
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.HUD;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class QuestTrackerVMTests
    {
        private sealed class FakeSource : QuestTrackerVM.ISource
        {
            public readonly List<string> Ids = new List<string>();
            public string Tracked;
            public readonly HashSet<string> ActiveIds = new HashSet<string>();
            public readonly Dictionary<string, string> Types = new Dictionary<string, string>();
            public readonly Dictionary<string, string> Objectives = new Dictionary<string, string>();
            public string SetTrackedArg; public int SetTrackedCalls;

            public event Action Changed;
            public IReadOnlyList<string> ActiveQuestIds() => Ids;
            public string TrackedId => Tracked;
            public bool IsActive(string id) => ActiveIds.Contains(id);
            public void SetTracked(string id) { SetTrackedArg = id; SetTrackedCalls++; }
            public string ObjectiveTextFor(string id) => id != null && Objectives.TryGetValue(id, out var o) ? o : "";
            public string QuestTypeOf(string id) => id != null && Types.TryGetValue(id, out var t) ? t : null;
            public void RaiseChanged() => Changed?.Invoke();
        }

        [Test]
        public void fallback_prefers_main_story_over_first_active()
        {
            var src = new FakeSource();
            src.Ids.AddRange(new[] { "q_side", "q_main" });
            src.ActiveIds.Add("q_side"); src.ActiveIds.Add("q_main");
            src.Types["q_side"] = "side";
            src.Types["q_main"] = "main";
            src.Objectives["q_main"] = "Do the thing";

            using var vm = new QuestTrackerVM(src, null);
            Assert.That(vm.HasTrackedQuest, Is.True);
            Assert.That(vm.ResolvedTrackedId, Is.EqualTo("q_main"), "a main/story quest wins over the first active side quest");
            Assert.That(vm.ObjectiveText, Is.EqualTo("Do the thing"));
            Assert.That(vm.UpdateSnapshot, Is.EqualTo("q_main|Do the thing"));
        }

        [Test]
        public void empty_type_normalizes_to_story_so_first_active_wins()
        {
            var src = new FakeSource();
            src.Ids.AddRange(new[] { "q1", "q2" });
            src.ActiveIds.Add("q1"); src.ActiveIds.Add("q2");
            // No Type data — empty normalizes to "story", so the FIRST active is tracked.
            using var vm = new QuestTrackerVM(src, null);
            Assert.That(vm.ResolvedTrackedId, Is.EqualTo("q1"));
        }

        [Test]
        public void player_pin_is_honored_when_active()
        {
            var src = new FakeSource();
            src.Ids.AddRange(new[] { "q1", "q2" });
            src.ActiveIds.Add("q1"); src.ActiveIds.Add("q2");
            src.Tracked = "q2";
            using var vm = new QuestTrackerVM(src, null);
            Assert.That(vm.ResolvedTrackedId, Is.EqualTo("q2"), "an active player pin is honored (no fallback)");
        }

        [Test]
        public void inactive_pin_falls_back()
        {
            var src = new FakeSource();
            src.Ids.Add("q1");
            src.ActiveIds.Add("q1");
            src.Tracked = "ghost";   // not active
            using var vm = new QuestTrackerVM(src, null);
            Assert.That(vm.ResolvedTrackedId, Is.EqualTo("q1"));
        }

        [Test]
        public void no_active_quests_hides_the_icon()
        {
            using var vm = new QuestTrackerVM(new FakeSource(), null);
            Assert.That(vm.HasTrackedQuest, Is.False);
            Assert.That(vm.ResolvedTrackedId, Is.Null);
        }

        [Test]
        public void set_tracked_command_delegates_to_source()
        {
            var src = new FakeSource();
            src.Ids.Add("q1"); src.ActiveIds.Add("q1");
            using var vm = new QuestTrackerVM(src, null);
            vm.SetTracked("q1");
            Assert.That(src.SetTrackedCalls, Is.EqualTo(1));
            Assert.That(src.SetTrackedArg, Is.EqualTo("q1"));
        }

        [Test]
        public void changed_fires_on_source_change_and_stops_after_dispose()
        {
            var src = new FakeSource();
            src.Ids.Add("q1"); src.ActiveIds.Add("q1");
            var vm = new QuestTrackerVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;

            src.RaiseChanged();
            Assert.That(changed, Is.GreaterThan(0));

            int before = changed;
            vm.Dispose();
            src.RaiseChanged();
            Assert.That(changed, Is.EqualTo(before), "after Dispose the VM must not raise Changed");
        }
    }
}
