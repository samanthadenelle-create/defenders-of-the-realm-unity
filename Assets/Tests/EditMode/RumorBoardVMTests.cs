// =============================================================================
// RumorBoardVMTests (EditMode) — §2c permission gate for the rumor-board MVVM slice.
// Locks the active/available bucketing + tab filtering + accept/track commands +
// the daily projection that MOVED out of RumorBoardPanel into the pure RumorBoardVM.
// Uses a FAKE IRumorBoardBackend (no scene, no QuestService/QuestCatalog/GameState).
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Quests;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class RumorBoardVMTests
    {
        private sealed class FakeBackend : IRumorBoardBackend
        {
            public List<QuestDef> Quests = new List<QuestDef>();
            public HashSet<string> Active = new HashSet<string>();
            public HashSet<string> Completed = new HashSet<string>();
            public string Tracked;
            public bool ReadyValue = true;
            public List<RumorBoardVM.DailyRow> Daily = new List<RumorBoardVM.DailyRow>();

            public int StartCalls, TrackCalls;

            public IReadOnlyList<QuestDef> Catalog => Quests;
            public bool Ready => ReadyValue;
            public bool IsActive(string id) => Active.Contains(id);
            public bool IsCompleted(string id) => Completed.Contains(id);
            public string ObjectiveFor(string id) => "objective-" + id;
            public string TrackedId => Tracked;
            public void StartQuest(string id) { StartCalls++; Active.Add(id); Changed?.Invoke(); }
            public void SetTracked(string id) { TrackCalls++; Tracked = id; }
            public IReadOnlyList<RumorBoardVM.DailyRow> DailyToday => Daily;
            public event Action Changed;
        }

        private static QuestDef Q(string id, string title, string type = null)
        {
            var def = new QuestDef { Id = id, Title = title, Type = type };
            def.Stages = new List<QuestStage>
            {
                new QuestStage { StageId = "s1", ObjectiveText = "hook-" + id }
            };
            return def;
        }

        private static QuestDef Gated(string id, string title, string requires)
        {
            var def = Q(id, title);
            def.RequiresQuestId = requires;
            return def;
        }

        private static FakeBackend Seed()
        {
            var b = new FakeBackend();
            b.Quests.Add(Q("q_story", "The Dimming"));            // available, story (no type)
            b.Quests.Add(Q("q_gear", "Forge Gear", "gear"));      // available, gear
            b.Quests.Add(Q("q_active", "Underway"));              // active
            b.Quests.Add(Q("q_done", "Finished"));               // completed -> off board
            b.Active.Add("q_active");
            b.Completed.Add("q_done");
            return b;
        }

        [Test]
        public void buckets_split_active_available_and_drop_completed()
        {
            var b = Seed();
            using var vm = new RumorBoardVM(b, null);   // default tab = "all"

            Assert.That(vm.ActiveQuests.Count, Is.EqualTo(1));
            Assert.That(vm.ActiveQuests[0].Id, Is.EqualTo("q_active"));

            var availIds = new List<string>();
            foreach (var i in vm.AvailableQuests) availIds.Add(i.Id);
            Assert.That(availIds, Does.Contain("q_story"));
            Assert.That(availIds, Does.Contain("q_gear"));
            Assert.That(availIds, Does.Not.Contain("q_done"), "completed quests leave the board");
            Assert.That(availIds, Does.Not.Contain("q_active"), "active quests are not in Available");
        }

        [Test]
        public void tab_filter_restricts_by_type_and_fires_changed()
        {
            var b = Seed();
            using var vm = new RumorBoardVM(b, null);
            int changed = 0; vm.Changed += () => changed++;

            vm.SetTab("gear");
            Assert.That(changed, Is.GreaterThan(0), "SetTab must raise Changed");

            var availIds = new List<string>();
            foreach (var i in vm.AvailableQuests) availIds.Add(i.Id);
            Assert.That(availIds, Does.Contain("q_gear"));
            Assert.That(availIds, Does.Not.Contain("q_story"), "story quest is filtered out on the Gear tab");

            vm.SetTab("story");
            availIds.Clear();
            foreach (var i in vm.AvailableQuests) availIds.Add(i.Id);
            Assert.That(availIds, Does.Contain("q_story"));
            Assert.That(availIds, Does.Not.Contain("q_gear"));
        }

        [Test]
        public void tracked_active_quest_reads_equipped()
        {
            var b = Seed();
            b.Tracked = "q_active";
            using var vm = new RumorBoardVM(b, null);
            Assert.That(vm.ActiveQuests[0].Equipped, Is.True, "the tracked/pinned quest reads as Equipped");
        }

        [Test]
        public void hook_and_objective_helpers_read_the_right_source()
        {
            var b = Seed();
            using var vm = new RumorBoardVM(b, null);
            Assert.That(vm.HookFor("q_story"), Is.EqualTo("hook-q_story"), "available hook = stage-1 objective");
            Assert.That(vm.ObjectiveFor("q_active"), Is.EqualTo("objective-q_active"), "active objective = current stage");
        }

        [Test]
        public void accept_starts_quest_and_sets_status_and_fires_changed()
        {
            var b = Seed();
            using var vm = new RumorBoardVM(b, null);
            int changed = 0; vm.Changed += () => changed++;

            vm.Accept("q_story");
            Assert.That(b.StartCalls, Is.EqualTo(1));
            Assert.That(vm.Status, Does.Contain("The Dimming"));
            Assert.That(changed, Is.GreaterThan(0));
            // It became active -> moved out of Available into Active.
            Assert.That(b.IsActive("q_story"), Is.True);
        }

        [Test]
        public void quest_gated_on_an_unfinished_prerequisite_stays_off_the_board()
        {
            // The defect this locks: with no prerequisite gate the terminal act of a chain is
            // startable (and finishable) on a fresh save, so whatever it unlocks is a freebie.
            var b = Seed();
            b.Quests.Add(Gated("q_act2", "The Old Fire", "q_story"));
            using var vm = new RumorBoardVM(b, null);

            var availIds = new List<string>();
            foreach (var i in vm.AvailableQuests) availIds.Add(i.Id);
            Assert.That(availIds, Does.Not.Contain("q_act2"),
                "a quest whose requiresQuestId is not completed must not be offered");

            vm.Accept("q_act2");
            Assert.That(b.StartCalls, Is.EqualTo(0), "Accept must refuse a gated quest, whoever calls it");
            Assert.That(vm.Status, Does.Contain("The Dimming"),
                "the refusal names the prerequisite's TITLE, not its raw id");
        }

        [Test]
        public void completing_the_prerequisite_opens_the_next_act()
        {
            var b = Seed();
            b.Quests.Add(Gated("q_act2", "The Old Fire", "q_story"));
            b.Completed.Add("q_story");
            using var vm = new RumorBoardVM(b, null);

            var availIds = new List<string>();
            foreach (var i in vm.AvailableQuests) availIds.Add(i.Id);
            Assert.That(availIds, Does.Contain("q_act2"), "the gate opens once the prerequisite is completed");

            vm.Accept("q_act2");
            Assert.That(b.StartCalls, Is.EqualTo(1));
            Assert.That(b.IsActive("q_act2"), Is.True);
        }

        [Test]
        public void accept_when_not_ready_sets_the_not_ready_status()
        {
            var b = Seed();
            b.ReadyValue = false;
            using var vm = new RumorBoardVM(b, null);
            vm.Accept("q_story");
            Assert.That(b.StartCalls, Is.EqualTo(0), "no StartQuest when the service isn't ready");
            Assert.That(vm.Status, Does.Contain("aren't ready"));
        }

        [Test]
        public void track_sets_tracked_then_closes()
        {
            var b = Seed();
            int closed = 0;
            using var vm = new RumorBoardVM(b, () => closed++);
            vm.Track("q_active");
            Assert.That(b.TrackCalls, Is.EqualTo(1));
            Assert.That(b.Tracked, Is.EqualTo("q_active"));
            Assert.That(closed, Is.EqualTo(1), "Track pins then closes the board");
        }

        [Test]
        public void daily_tab_projects_daily_rows()
        {
            var b = Seed();
            b.Daily.Add(new RumorBoardVM.DailyRow("d1", "Build Towers", 2, 3, false));
            using var vm = new RumorBoardVM(b, null);
            vm.SetTab("daily");
            Assert.That(vm.IsDailyTab, Is.True);
            Assert.That(vm.DailyQuests.Count, Is.EqualTo(1));
            Assert.That(vm.DailyQuests[0].Title, Is.EqualTo("Build Towers"));
            Assert.That(vm.DailyQuests[0].Progress, Is.EqualTo(2));
            // The catalog buckets are empty under the Daily tab (it renders from DailyQuests).
            Assert.That(vm.ActiveQuests.Count, Is.EqualTo(0));
            Assert.That(vm.AvailableQuests.Count, Is.EqualTo(0));
        }

        [Test]
        public void daily_label_resolver_substitutes_target_and_falls_back()
        {
            // WO-810 follow-up: the shared DailyQuestCatalog.ResolveLabel the live backend's
            // DailyToday now routes through (mirrors DailyQuestVMTests' substitution lock).
            var q = new DailyQuestInstance
            { Id = "q1", TemplateId = "combat.a", Slot = "combat", Target = 5, Label = "Clear {target} waves" };
            Assert.That(DailyQuestCatalog.ResolveLabel(q), Is.EqualTo("Clear 5 waves"),
                "{target} must be substituted in the daily title");

            q.Label = null;
            Assert.That(DailyQuestCatalog.ResolveLabel(q), Is.EqualTo("combat.a"),
                "empty label falls back to TemplateId");

            q.TemplateId = null;
            Assert.That(DailyQuestCatalog.ResolveLabel(q), Is.EqualTo("combat"),
                "then to Slot");

            Assert.That(DailyQuestCatalog.ResolveLabel(null), Is.EqualTo(""),
                "null instance resolves to empty, never throws");
        }

        [Test]
        public void dispose_unsubscribes_from_backend_changed()
        {
            var b = Seed();
            var vm = new RumorBoardVM(b, null);
            int changed = 0; vm.Changed += () => changed++;
            vm.Dispose();
            int before = changed;
            b.StartQuest("q_story");   // fires backend.Changed
            Assert.That(changed, Is.EqualTo(before), "after Dispose the VM must not react to backend Changed");
        }
    }
}
