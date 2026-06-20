// =============================================================================
// DialogueRunnerTests (EditMode) — WO-455 permission gate for our own dialogue.
// -----------------------------------------------------------------------------
// Proves the runner behaves AND has none of YarnSpinner's lifecycle hazards:
//   • lines walk in order; command-only node fires + ends (the <<stop>>-killer);
//   • options present + Choose branches; conditions gate options/nodes;
//   • Stop() mid-line is synchronous, raises Ended exactly once, throws nothing
//     (the "No node" / mid-command teardown NRE that ate the owner's hours).
// Pure C# — no scene, no async, no Yarn.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Dialogue;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class DialogueRunnerTests
    {
        private sealed class RecordingSink : IDialogueCommandSink
        {
            public readonly List<string> Fired = new List<string>();
            public void Run(string verb, IReadOnlyList<string> args)
            {
                string a = (args != null && args.Count > 0) ? string.Join(",", args) : "";
                Fired.Add(string.IsNullOrEmpty(a) ? verb : verb + "(" + a + ")");
            }
        }

        private sealed class FlagCond : IDialogueConditionSource
        {
            public readonly HashSet<string> True = new HashSet<string>();
            public bool Check(string condition) => True.Contains(condition);
        }

        private static DialogueLine L(string speaker, string text) =>
            new DialogueLine { Speaker = speaker, Text = text };

        // --- helpers to assemble a def quickly ---
        private static DialogueDef Def(params DialogueNode[] nodes)
        {
            var d = new DialogueDef { Id = "test" };
            d.Nodes.AddRange(nodes);
            return d;
        }

        [Test]
        public void Lines_WalkInOrder_ThenEnd()
        {
            var def = Def(new DialogueNode
            {
                Id = "n1",
                Lines = { L("Brom", "one"), L("Brom", "two"), L("Brom", "three") },
            });
            var runner = new DialogueRunner();
            var seen = new List<string>();
            bool ended = false;
            runner.LineShown += l => seen.Add(l.Text);
            runner.Ended += () => ended = true;

            runner.Begin(def, new RecordingSink(), new FlagCond());
            Assert.AreEqual("one", runner.CurrentLine.Text);
            runner.Advance(); // -> two
            runner.Advance(); // -> three
            Assert.AreEqual("three", runner.CurrentLine.Text);
            runner.Advance(); // past last line -> end
            CollectionAssert.AreEqual(new[] { "one", "two", "three" }, seen);
            Assert.IsTrue(ended);
            Assert.IsFalse(runner.IsRunning);
        }

        [Test]
        public void CommandOnlyNode_Fires_ThenEnds() // the Yarn <<stop>> killer
        {
            var def = Def(new DialogueNode
            {
                Id = "open",
                Commands = { new DialogueCommand { Verb = "OpenShop", Args = { "armorer" } } },
            });
            var sink = new RecordingSink();
            var runner = new DialogueRunner();
            bool ended = false;
            runner.Ended += () => ended = true;

            runner.Begin(def, sink, new FlagCond());
            CollectionAssert.AreEqual(new[] { "OpenShop(armorer)" }, sink.Fired);
            Assert.IsTrue(ended, "a command-only node fires its command and ends — no <<stop>> needed");
            Assert.IsFalse(runner.IsRunning);
        }

        [Test]
        public void Options_Present_AndChooseBranches()
        {
            var def = Def(
                new DialogueNode { Id = "start", Lines = { L("Brom", "well?") },
                    Options = {
                        new DialogueOption { Text = "yes", Goto = "yes" },
                        new DialogueOption { Text = "no",  Goto = "end" },
                    }},
                new DialogueNode { Id = "yes", Lines = { L("Brom", "good") } });
            var runner = new DialogueRunner();
            IReadOnlyList<DialogueOption> opts = null;
            runner.OptionsShown += o => opts = o;

            runner.Begin(def, new RecordingSink(), new FlagCond());
            runner.Advance();                 // past the line -> options
            Assert.IsNotNull(opts);
            Assert.AreEqual(2, opts.Count);
            runner.Choose(0);                 // "yes" -> node "yes"
            Assert.AreEqual("good", runner.CurrentLine.Text);
        }

        [Test]
        public void Conditions_GateOptions()
        {
            var def = Def(new DialogueNode { Id = "start", Lines = { L("Brom", "hi") },
                Options = {
                    new DialogueOption { Text = "secret", Requires = "hasKey", Goto = "end" },
                    new DialogueOption { Text = "bye", Goto = "end" },
                }});
            var cond = new FlagCond(); // hasKey is FALSE
            var runner = new DialogueRunner();
            IReadOnlyList<DialogueOption> opts = null;
            runner.OptionsShown += o => opts = o;

            runner.Begin(def, new RecordingSink(), cond);
            runner.Advance();
            Assert.AreEqual(1, opts.Count, "the gated 'secret' option is hidden when hasKey is false");
            Assert.AreEqual("bye", opts[0].Text);
        }

        [Test]
        public void Stop_MidLine_IsCleanAndIdempotent() // the race-free proof
        {
            var def = Def(new DialogueNode { Id = "n", Lines = { L("Brom", "a"), L("Brom", "b") } });
            var runner = new DialogueRunner();
            int endedCount = 0;
            runner.Ended += () => endedCount++;

            runner.Begin(def, new RecordingSink(), new FlagCond());
            Assert.IsTrue(runner.IsRunning);
            Assert.DoesNotThrow(() => runner.Stop());   // walk-away mid-line — no NRE, no "No node"
            Assert.IsFalse(runner.IsRunning);
            Assert.DoesNotThrow(() => runner.Stop());   // idempotent
            Assert.DoesNotThrow(() => runner.Advance()); // post-stop calls are safe no-ops
            Assert.DoesNotThrow(() => runner.Choose(0));
            Assert.AreEqual(1, endedCount, "Ended fires exactly once");
        }

        [Test]
        public void Begin_WhileRunning_StopsPrior()
        {
            var def1 = Def(new DialogueNode { Id = "a", Lines = { L("x", "1") } });
            var def2 = Def(new DialogueNode { Id = "b", Lines = { L("y", "2") } });
            var runner = new DialogueRunner();
            int ended = 0;
            runner.Ended += () => ended++;

            runner.Begin(def1, new RecordingSink(), new FlagCond());
            runner.Begin(def2, new RecordingSink(), new FlagCond()); // stops def1 cleanly
            Assert.AreEqual("2", runner.CurrentLine.Text);
            Assert.AreEqual(1, ended, "starting a new dialogue ends the prior exactly once");
        }
    }
}
