// =============================================================================
// DialogueViewModelTests — §2c permission-gate for the WO-744 MVVM landmine-2 work.
// -----------------------------------------------------------------------------
// Locks (a) the speaker/portrait projection that moved OFF DialogueView into the VM
// (DialoguePortrait.Forced override -> PortraitPath / PortraitForced), and (b) the
// RELOCATED WO-702 builder truce: the BuildModeState.DialogueHiddenForBuilder write
// now lives in the VM and MUST toggle exactly as before on the founding_town path
// (a live dialogue hidden while the builder is open, released on end) — naive removal
// re-freezes Build Mode, so this test is the guard against that regression.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using DeNelle.Core;
using DeNelle.Core.Dialogue;

namespace DeNelle.Core.Tests
{
    public class DialogueViewModelTests
    {
        private sealed class NullSink : IDialogueCommandSink
        {
            public void Run(string verb, IReadOnlyList<string> args) { }
        }

        private sealed class NullCond : IDialogueConditionSource
        {
            public bool Check(string condition) => false;
        }

        // A minimal one-line conversation standing in for the founding_town step intro.
        private static DialogueDef OneLine(string id, string speaker, string text)
        {
            return new DialogueDef
            {
                Id = id,
                Nodes = new List<DialogueNode>
                {
                    new DialogueNode
                    {
                        Id = "n0",
                        Lines = new List<DialogueLine> { new DialogueLine { Speaker = speaker, Text = text } },
                    },
                },
            };
        }

        [SetUp]
        public void Reset()
        {
            DialoguePortrait.Forced = null;
            BuildModeState.DialogueHiddenForBuilder = false;
        }

        [Test]
        public void Begin_clears_a_leaked_forced_portrait()
        {
            // A previous conversation's forced portrait must not leak into the next open.
            DialoguePortrait.Forced = "Portraits/stale";
            var vm = new DialogueViewModel();
            vm.Begin(OneLine("founding_town", "Sylas", "We raise the town here."), new NullSink(), new NullCond());
            Assert.That(DialoguePortrait.Forced, Is.Null, "Begin resets the sticky override (relocated from the View)");
            Assert.That(vm.PortraitForced, Is.False);
        }

        [Test]
        public void Forced_portrait_projects_through_the_vm()
        {
            var vm = new DialogueViewModel();
            vm.Begin(OneLine("founding_town", "Sylas", "We raise the town here."), new NullSink(), new NullCond());
            // Simulate a per-node `portrait` command firing after Begin.
            DialoguePortrait.Forced = "Portraits/test";

            Assert.That(vm.Speaker, Is.EqualTo("Sylas"));
            Assert.That(vm.Title, Is.EqualTo("Sylas"));            // IPanelViewModel header projection
            Assert.That(vm.PortraitPath, Is.EqualTo("Portraits/test"));
            Assert.That(vm.PortraitForced, Is.True);
        }

        [Test]
        public void No_override_and_unknown_speaker_projects_null_portrait()
        {
            // Catalog may be absent in a headless test run; a missing dialogues.json only logs —
            // ignore that so the null-fallback assertion is the subject under test.
            LogAssert.ignoreFailingMessages = true;
            var vm = new DialogueViewModel();
            vm.Begin(OneLine("founding_town", "Nobody", "..."), new NullSink(), new NullCond());
            Assert.That(vm.PortraitForced, Is.False);
            Assert.That(string.IsNullOrEmpty(vm.PortraitPath), Is.True);
        }

        [Test]
        public void Builder_truce_write_toggles_on_founding_town_path()
        {
            var vm = new DialogueViewModel();
            vm.Begin(OneLine("founding_town", "Sylas", "We raise the town here."), new NullSink(), new NullCond());
            Assert.That(vm.IsOpen, Is.True);
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.False);

            // Builder opens -> the live dialogue hides (NOT closes) + the truce publishes true.
            Assert.That(vm.SetBuilderActive(true), Is.True, "state changed");
            Assert.That(vm.HiddenForBuilder, Is.True);
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.True);
            Assert.That(vm.IsOpen, Is.True, "hidden, never closed — Ended must NOT have fired");

            // Idempotent while still open.
            Assert.That(vm.SetBuilderActive(true), Is.False, "no change");
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.True);

            // Builder closes -> reshow + truce clears.
            Assert.That(vm.SetBuilderActive(false), Is.True);
            Assert.That(vm.HiddenForBuilder, Is.False);
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.False);
        }

        [Test]
        public void Truce_self_heals_when_dialogue_ends_while_hidden()
        {
            var vm = new DialogueViewModel();
            vm.Begin(OneLine("founding_town", "Sylas", "We raise the town here."), new NullSink(), new NullCond());
            vm.SetBuilderActive(true);
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.True);

            // A dialogue superseded/closed WHILE hidden must release the truce (no stuck-true flag
            // = no re-frozen Build Mode). OnEnded clears it.
            vm.Close();
            Assert.That(vm.IsOpen, Is.False);
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.False);

            // And a stray publish after end stays false (IsOpen gates it).
            vm.SetBuilderActive(true);
            Assert.That(BuildModeState.DialogueHiddenForBuilder, Is.False);
        }
    }
}
