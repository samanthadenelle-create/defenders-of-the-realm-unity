// =============================================================================
// RumorBoardVMTests (EditMode) - sec.2c permission gate for the rumor-board MVVM slice.
// WO-1192 v3: the board OFFERS work only, so what is locked here is the AVAILABLE
// bucketing, the prerequisite gate, Accept, the PAGING window (three at a time,
// wrapping), the NEW flag, the reward-chip projection, and the one-line hook that
// can no longer end mid-word.
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
            public HashSet<string> Seen = new HashSet<string>();
            public bool ReadyValue = true;

            public int StartCalls;

            public IReadOnlyList<QuestDef> Catalog => Quests;
            public bool Ready => ReadyValue;
            public bool IsActive(string id) => Active.Contains(id);
            public bool IsCompleted(string id) => Completed.Contains(id);
            public void StartQuest(string id) { StartCalls++; Active.Add(id); Changed?.Invoke(); }
            public bool HasSeen(string id) => id != null && Seen.Contains(id);
            public void MarkSeen(string id) { if (id != null) Seen.Add(id); }
            public event Action Changed;
        }

        private static QuestDef Q(string id, string title, string type = null, string objective = null)
        {
            var def = new QuestDef { Id = id, Title = title, Type = type };
            def.Stages = new List<QuestStage>
            {
                new QuestStage { StageId = "s1", ObjectiveText = objective ?? ("hook-" + id) }
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
            b.Quests.Add(Q("q_active", "Underway"));              // active -> off the board
            b.Quests.Add(Q("q_done", "Finished"));                // completed -> off the board
            b.Active.Add("q_active");
            b.Completed.Add("q_done");
            return b;
        }

        [Test]
        public void the_board_offers_only_available_work()
        {
            var b = Seed();
            using var vm = new RumorBoardVM(b, null);

            var availIds = new List<string>();
            foreach (var i in vm.AvailableQuests) availIds.Add(i.Id);
            Assert.That(availIds, Does.Contain("q_story"));
            Assert.That(availIds, Does.Contain("q_gear"));
            Assert.That(availIds, Does.Not.Contain("q_done"), "completed quests leave the board");
            Assert.That(availIds, Does.Not.Contain("q_active"),
                "an ACTIVE quest belongs to the HUD tracker - the board only OFFERS work (WO-1192)");
        }

        [Test]
        public void the_page_window_is_three_and_next_wraps()
        {
            // THE DEFECT THIS LOCKS: a fixed 3-up board that pages off the end shows a blank
            // board, or refuses to move. The owner chose the keep-going form: Next always
            // advances and WRAPS.
            var b = new FakeBackend();
            for (int i = 1; i <= 7; i++) b.Quests.Add(Q("q" + i, "Rumor " + i));
            using var vm = new RumorBoardVM(b, null);

            Assert.That(RumorBoardVM.PageSize, Is.EqualTo(3), "the v3 board is three posters");
            Assert.That(vm.PageCount, Is.EqualTo(3), "7 rumors = 3 pages of at most 3");
            Assert.That(vm.PageQuests.Count, Is.EqualTo(3));
            Assert.That(vm.PageQuests[0].Id, Is.EqualTo("q1"));

            vm.NextPage();
            Assert.That(vm.PageIndex, Is.EqualTo(1));
            Assert.That(vm.PageQuests[0].Id, Is.EqualTo("q4"));

            vm.NextPage();
            Assert.That(vm.PageIndex, Is.EqualTo(2));
            Assert.That(vm.PageQuests.Count, Is.EqualTo(1), "the last page is honestly SHORT, not padded");
            Assert.That(vm.PageQuests[0].Id, Is.EqualTo("q7"));

            vm.NextPage();
            Assert.That(vm.PageIndex, Is.EqualTo(0), "Next WRAPS at the end - it never dead-ends");
            Assert.That(vm.PageQuests[0].Id, Is.EqualTo("q1"));
        }

        [Test]
        public void an_empty_board_still_reports_one_page_and_next_is_safe()
        {
            var b = new FakeBackend();
            using var vm = new RumorBoardVM(b, null);
            Assert.That(vm.PageCount, Is.EqualTo(1), "'page 1 of 1' must always be a truthful sentence");
            Assert.That(vm.PageQuests.Count, Is.EqualTo(0));
            Assert.That(vm.HasMultiplePages, Is.False);
            vm.NextPage();
            Assert.That(vm.PageIndex, Is.EqualTo(0), "wrapping an empty board must not walk off the end");
        }

        [Test]
        public void accepting_the_last_rumor_on_a_page_walks_the_page_back()
        {
            // THE DEFECT THIS LOCKS: accept the only rumor on the last page and the board is
            // left showing an empty page while rumors are still posted behind it.
            var b = new FakeBackend();
            for (int i = 1; i <= 4; i++) b.Quests.Add(Q("q" + i, "Rumor " + i));
            using var vm = new RumorBoardVM(b, null);
            vm.NextPage();
            Assert.That(vm.PageIndex, Is.EqualTo(1));
            Assert.That(vm.PageQuests.Count, Is.EqualTo(1));

            vm.Accept("q4");
            Assert.That(vm.PageCount, Is.EqualTo(1));
            Assert.That(vm.PageIndex, Is.EqualTo(0));
            Assert.That(vm.PageQuests.Count, Is.EqualTo(3), "the board falls back to a page that exists");
        }

        [Test]
        public void new_is_true_until_the_page_is_marked_seen()
        {
            var b = Seed();
            using var vm = new RumorBoardVM(b, null);
            Assert.That(vm.IsNew("q_story"), Is.True, "a rumor never shown is NEW");
            vm.MarkPageSeen();
            Assert.That(vm.IsNew("q_story"), Is.False,
                "a NEW chip that never clears is chrome, not state");
        }

        [Test]
        public void the_hook_is_one_line_and_never_ends_mid_word()
        {
            // THE DEFECT THIS LOCKS, verbatim from the two failing captures: the objective
            // rendered as "...have begun to sin" (the word is "sing") and "...wakes the
            // lantern eels. Sh". A hook cut at a SENTENCE or a WORD boundary cannot do that.
            const string letter =
                "The shelves have begun to sing at dusk. " +
                "She asks for a steady hand to carry the sealed ledger past the flooded stair.";
            string hook = RumorBoardVM.OneLineHook(letter);
            Assert.That(hook, Is.EqualTo("The shelves have begun to sing at dusk."),
                "the hook is the first SENTENCE when one fits");
            Assert.That(hook, Does.Not.EndWith("sin"));

            // A first sentence longer than the budget falls back to a WORD boundary + "...".
            string cut = RumorBoardVM.OneLineHook(
                "Track down why the eleventh bell rings with nobody at all upon the rope, "
                + "and quiet it before nightfall arrives over the western fields.");
            Assert.That(cut.Length, Is.LessThanOrEqualTo(RumorBoardVM.HookMaxChars + 3));
            Assert.That(cut, Does.EndWith("..."));
            string body = cut.Substring(0, cut.Length - 3);
            Assert.That(body, Does.Not.EndWith(" "));
            // THE assertion that makes "mid-word" unreachable: the last word before the
            // ellipsis is a WHOLE word of the source, not a prefix of one.
            int lastSpace = body.LastIndexOf(' ');
            Assert.That(lastSpace, Is.GreaterThan(0));
            string lastWord = body.Substring(lastSpace + 1);
            Assert.That(
                "Track down why the eleventh bell rings with nobody at all upon the rope, "
                + "and quiet it before nightfall arrives over the western fields.",
                Does.Contain(lastWord + " ").Or.Contain(lastWord + ","),
                "the hook was cut at a word boundary, not through a word");
        }

        [Test]
        public void reward_chips_carry_kind_and_amount_and_are_not_a_fixed_count()
        {
            var b = new FakeBackend();
            var def = Q("q_rich", "Rich Rumor");
            def.Stages[0].Reward = new List<QuestRewardLine>
            {
                new QuestRewardLine { Kind = QuestRewardLine.KindXp, Amount = 400 },
                new QuestRewardLine { Kind = QuestRewardLine.KindCrystals, Amount = 220 },
                new QuestRewardLine { Kind = QuestRewardLine.KindFood, Amount = 90 },
            };
            b.Quests.Add(def);
            b.Quests.Add(Q("q_poor", "Unrewarded Rumor"));
            using var vm = new RumorBoardVM(b, null);

            var chips = vm.RewardChipsFor("q_rich");
            Assert.That(chips.Count, Is.EqualTo(3), "one chip per authored reward, never a fixed count");
            Assert.That(chips[0].Kind, Is.EqualTo(RumorBoardVM.RewardKind.Xp));
            Assert.That(chips[0].IsCurrency, Is.False, "XP is a WORD chip, not an icon chip");
            Assert.That(chips[1].Kind, Is.EqualTo(RumorBoardVM.RewardKind.Crystals));
            Assert.That(chips[1].Amount, Is.EqualTo(220), "the icon chip renders the AMOUNT, not a letter");
            Assert.That(chips[1].IsCurrency, Is.True);
            // Canon sec.7: the authored `food` slot IS Stone.
            Assert.That(chips[2].Kind, Is.EqualTo(RumorBoardVM.RewardKind.Stone));
            Assert.That(chips[2].Text, Is.EqualTo("Stone 90"));
            Assert.That(chips[2].Text, Does.Not.Contain("Food"));

            Assert.That(vm.RewardChipsFor("q_poor").Count, Is.EqualTo(0),
                "an unrewarded rumor draws NO row rather than an empty rule");
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
        public void type_tag_reads_off_the_quest_type_field()
        {
            var b = new FakeBackend();
            b.Quests.Add(Q("q_main", "Main One"));
            b.Quests.Add(Q("q_gear2", "Gear One", "gear"));
            b.Quests.Add(Q("q_daily", "Daily One", "daily"));
            b.Quests.Add(Q("q_end", "End One", "endgame"));
            using var vm = new RumorBoardVM(b, null);
            Assert.That(vm.TypeFor("q_main"), Is.EqualTo("Main"));
            Assert.That(vm.TypeFor("q_gear2"), Is.EqualTo("Gear"));
            Assert.That(vm.TypeFor("q_daily"), Is.EqualTo("Daily"));
            Assert.That(vm.TypeFor("q_end"), Is.EqualTo("Endgame"));
        }

        [Test]
        public void daily_label_resolver_substitutes_target_and_falls_back()
        {
            // The shared DailyQuestCatalog.ResolveLabel substitution lock. It no longer has a
            // consumer on THIS board (the daily tab is retired with the rest of the tabs), but
            // the resolver is still the one path every daily label goes through.
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
