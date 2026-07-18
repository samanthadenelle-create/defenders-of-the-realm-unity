// =============================================================================
// ClanChatVMTests (EditMode) — §2c gate for the clan-chat MVVM slice.
// -----------------------------------------------------------------------------
// Locks the projection + command routing MOVED out of ClanChatPanel into the pure
// ClanChatVM. FAKE ISource — no scene, no ClanService, no ChatPhraseCatalog.
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Services;
using DeNelle.HUD;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ClanChatVMTests
    {
        private sealed class FakeSource : ClanChatVM.ISource
        {
            public bool InClanFlag;
            public string Name = "Ember Wardens";
            public string Tag = "EMBR";
            public string Account = "me";
            public readonly List<ChatMessage> Msgs = new List<ChatMessage>();
            public readonly List<ChatPhraseDef> PhraseList = new List<ChatPhraseDef>();
            public readonly Dictionary<string, string> CatLabels = new Dictionary<string, string>();

            public int Leaves, Creates, Templated, Customs;
            public string LastName, LastTag, LastPhrase, LastCustom;

            public event Action Changed;
            public bool InClan => InClanFlag;
            public string ClanName => Name;
            public string ClanTag => Tag;
            public string AccountId => Account;
            public IReadOnlyList<ChatMessage> Messages => Msgs;
            public IReadOnlyList<ChatPhraseDef> Phrases => PhraseList;
            public string CategoryLabel(string key) => key != null && CatLabels.TryGetValue(key, out var l) ? l : (key ?? "Phrases");
            public void LeaveClan() { Leaves++; }
            public void CreateClan(string n, string t) { Creates++; LastName = n; LastTag = t; }
            public void AddTemplatedMessage(string id) { Templated++; LastPhrase = id; }
            public void AddCustomMessage(string t) { Customs++; LastCustom = t; }
            public void RaiseChanged() => Changed?.Invoke();
        }

        [Test]
        public void not_in_clan_shows_create_state_and_no_rows()
        {
            var src = new FakeSource { InClanFlag = false };
            using var vm = new ClanChatVM(src, null);
            Assert.That(vm.InClan, Is.False);
            Assert.That(vm.StatusLine, Is.EqualTo("No clan yet"));
            Assert.That(vm.ActionLabel, Is.EqualTo("Create"));
            Assert.That(vm.Messages.Count, Is.EqualTo(0));
            Assert.That(vm.Chips.Count, Is.EqualTo(0));
        }

        [Test]
        public void in_clan_empty_messages_shows_a_single_hint_row()
        {
            var src = new FakeSource { InClanFlag = true };
            using var vm = new ClanChatVM(src, null);
            Assert.That(vm.StatusLine, Is.EqualTo("[EMBR] Ember Wardens"));
            Assert.That(vm.ActionLabel, Is.EqualTo("Leave"));
            Assert.That(vm.Messages.Count, Is.EqualTo(1));
            Assert.That(vm.Messages[0].IsHint, Is.True);
        }

        [Test]
        public void messages_project_meta_you_and_custom_suffix()
        {
            var src = new FakeSource { InClanFlag = true };
            src.Msgs.Add(new ChatMessage { SenderId = "me", SenderName = "You", Text = "hi", IsCustom = false });
            src.Msgs.Add(new ChatMessage { SenderId = "other", SenderName = "Bob", Text = "yo", IsCustom = true });
            using var vm = new ClanChatVM(src, null);

            Assert.That(vm.Messages.Count, Is.EqualTo(2));
            Assert.That(vm.Messages[0].Meta, Is.EqualTo("You"));
            Assert.That(vm.Messages[0].Body, Is.EqualTo("hi"));
            Assert.That(vm.Messages[1].Meta, Is.EqualTo("Bob - custom"));
            Assert.That(vm.Messages[1].Body, Is.EqualTo("yo"));
        }

        [Test]
        public void chips_group_by_category_with_dividers()
        {
            var src = new FakeSource { InClanFlag = true };
            src.PhraseList.Add(new ChatPhraseDef { Id = "p1", Category = "greet", Text = "Hi", Emoji = "" });
            src.PhraseList.Add(new ChatPhraseDef { Id = "p2", Category = "greet", Text = "Hey", Emoji = "" });
            src.PhraseList.Add(new ChatPhraseDef { Id = "p3", Category = "taunt", Text = "Boo", Emoji = "" });
            src.CatLabels["greet"] = "Greetings";
            src.CatLabels["taunt"] = "Taunts";
            using var vm = new ClanChatVM(src, null);

            Assert.That(vm.Chips.Count, Is.EqualTo(5));
            Assert.That(vm.Chips[0].IsDivider, Is.True);
            Assert.That(vm.Chips[0].Label, Is.EqualTo("Greetings"));
            Assert.That(vm.Chips[1].IsDivider, Is.False);
            Assert.That(vm.Chips[1].PhraseId, Is.EqualTo("p1"));
            Assert.That(vm.Chips[3].IsDivider, Is.True);
            Assert.That(vm.Chips[3].Label, Is.EqualTo("Taunts"));
        }

        [Test]
        public void empty_phrases_yields_a_single_fallback_chip()
        {
            var src = new FakeSource { InClanFlag = true };   // no phrases
            using var vm = new ClanChatVM(src, null);
            Assert.That(vm.Chips.Count, Is.EqualTo(1));
            Assert.That(vm.Chips[0].IsFallback, Is.True);
        }

        [Test]
        public void commands_route_to_source_and_guard_out_of_clan()
        {
            var src = new FakeSource { InClanFlag = true };
            using var vm = new ClanChatVM(src, null);
            vm.OnHeaderButton();          Assert.That(src.Leaves, Is.EqualTo(1));
            vm.CreateClan("N", "T");      Assert.That(src.Creates, Is.EqualTo(1)); Assert.That(src.LastName, Is.EqualTo("N"));
            vm.SendPhrase("p");           Assert.That(src.Templated, Is.EqualTo(1)); Assert.That(src.LastPhrase, Is.EqualTo("p"));
            vm.SendCustom("hi");          Assert.That(src.Customs, Is.EqualTo(1)); Assert.That(src.LastCustom, Is.EqualTo("hi"));

            // Out of clan: sends are no-ops (matches the service guard + View behavior).
            var src2 = new FakeSource { InClanFlag = false };
            using var vm2 = new ClanChatVM(src2, null);
            vm2.SendPhrase("p"); vm2.SendCustom("hi");
            Assert.That(src2.Templated, Is.EqualTo(0));
            Assert.That(src2.Customs, Is.EqualTo(0));
        }

        [Test]
        public void changed_fires_on_source_change_and_stops_after_dispose()
        {
            var src = new FakeSource { InClanFlag = true };
            var vm = new ClanChatVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;

            src.RaiseChanged();
            Assert.That(changed, Is.GreaterThan(0));

            int before = changed;
            vm.Dispose();
            src.RaiseChanged();
            Assert.That(changed, Is.EqualTo(before));
        }
    }
}
