// =============================================================================
// DialogueRunner — the runtime that walks a DialogueDef (WO-455, replaces Yarn).
// -----------------------------------------------------------------------------
// Plain C# state machine (NO MonoBehaviour, NO async, NO source generator) so it
// is unit-testable headless and has ZERO of Yarn's lifecycle hazards: we own
// Begin/Advance/Choose/Stop, so there is no "No node selected" race and no
// mid-command teardown NRE. Stop() is synchronous + idempotent.
//
// FLOW per node:  show LINES in order  ->  fire COMMANDS  ->  present OPTIONS
//                 (filtered by `requires`) OR follow `next` OR END.
// A command-only node (no lines, no options, no next) just fires its commands and
// ends — the exact pattern that needed the `<<stop>>` hack in Yarn, now trivial.
//
// Core stays clean: commands run through IDialogueCommandSink and conditions
// through IDialogueConditionSource (implemented Village-side, reusing the existing
// command vocabulary), so DeNelle.Core never references gameplay.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.Dialogue
{
    /// <summary>Runs a dialogue verb (OpenShop/StartQuest/PlaySfx/...) directly — no Yarn.</summary>
    public interface IDialogueCommandSink
    {
        void Run(string verb, IReadOnlyList<string> args);
    }

    /// <summary>Evaluates an option/node condition key (e.g. a quest flag). Unknown => false.</summary>
    public interface IDialogueConditionSource
    {
        bool Check(string condition);
    }

    public sealed class DialogueRunner
    {
        private enum Phase { Idle, Lines, Options }

        private DialogueDef _def;
        private IDialogueCommandSink _sink;
        private IDialogueConditionSource _cond;

        private DialogueNode _node;
        private int _lineIndex;
        private Phase _phase = Phase.Idle;
        private bool _active;   // master "a dialogue is in progress" flag (Ended fires once when it clears)
        private readonly List<DialogueOption> _visibleOptions = new List<DialogueOption>();

        /// <summary>Raised with each line as it is presented (speaker + text).</summary>
        public event Action<DialogueLine> LineShown;
        /// <summary>Raised with the visible options when a node reaches its choice point.</summary>
        public event Action<IReadOnlyList<DialogueOption>> OptionsShown;
        /// <summary>Raised once when the dialogue ends (naturally or via Stop()).</summary>
        public event Action Ended;

        public bool IsRunning => _active;
        public DialogueLine CurrentLine =>
            (_phase == Phase.Lines && _node != null && _node.Lines != null &&
             _lineIndex >= 0 && _lineIndex < _node.Lines.Count) ? _node.Lines[_lineIndex] : null;
        public IReadOnlyList<DialogueOption> CurrentOptions => _visibleOptions;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>Start a dialogue. Safe to call while another runs (it Stops first).</summary>
        public void Begin(DialogueDef def, IDialogueCommandSink sink, IDialogueConditionSource cond)
        {
            Stop();
            if (def == null) return;
            _def = def; _sink = sink; _cond = cond;
            _active = true;   // set BEFORE entering: a command-only node ends synchronously in EnterNode
            EnterNode(def.EntryNode());
        }

        /// <summary>Tap-to-continue: advance the current line, or move past the line block.
        /// No-op while options are showing (the player must Choose).</summary>
        public void Advance()
        {
            if (_phase != Phase.Lines || _node == null) return;
            var lines = _node.Lines;
            if (lines != null && _lineIndex + 1 < lines.Count)
            {
                _lineIndex++;
                LineShown?.Invoke(lines[_lineIndex]);
                return;
            }
            PostLines(_node);
        }

        /// <summary>Pick option index (into CurrentOptions). No-op unless options are showing.</summary>
        public void Choose(int optionIndex)
        {
            if (_phase != Phase.Options) return;
            if (optionIndex < 0 || optionIndex >= _visibleOptions.Count) return;
            var opt = _visibleOptions[optionIndex];
            string target = opt != null ? opt.Goto : null;
            if (string.IsNullOrEmpty(target) || string.Equals(target, "end", StringComparison.OrdinalIgnoreCase))
            { End(); return; }
            EnterNode(_def != null ? _def.FindNode(target) : null);
        }

        /// <summary>Stop immediately. Synchronous, idempotent, race-free — raises Ended once.</summary>
        public void Stop() => End();

        // ── Internals ─────────────────────────────────────────────────────────

        private void EnterNode(DialogueNode node)
        {
            if (node == null) { End(); return; }
            // A condition-gated node entered with a false condition just ends the line.
            if (!string.IsNullOrEmpty(node.Condition) && _cond != null && !_cond.Check(node.Condition))
            { End(); return; }

            _node = node;
            _lineIndex = 0;
            _visibleOptions.Clear();

            if (node.Lines != null && node.Lines.Count > 0)
            {
                _phase = Phase.Lines;
                LineShown?.Invoke(node.Lines[0]);
            }
            else
            {
                PostLines(node);
            }
        }

        // After the line block: fire commands, then branch to options / next / end.
        private void PostLines(DialogueNode node)
        {
            if (node.Commands != null)
            {
                foreach (var c in node.Commands)
                {
                    if (c == null || string.IsNullOrEmpty(c.Verb)) continue;
                    _sink?.Run(c.Verb, c.Args ?? new List<string>());
                }
            }

            _visibleOptions.Clear();
            if (node.Options != null)
            {
                foreach (var o in node.Options)
                {
                    if (o == null) continue;
                    if (!string.IsNullOrEmpty(o.Requires) && _cond != null && !_cond.Check(o.Requires)) continue;
                    _visibleOptions.Add(o);
                }
            }

            if (_visibleOptions.Count > 0)
            {
                _phase = Phase.Options;
                OptionsShown?.Invoke(_visibleOptions);
            }
            else if (!string.IsNullOrEmpty(node.Next))
            {
                EnterNode(_def != null ? _def.FindNode(node.Next) : null);
            }
            else
            {
                End();
            }
        }

        private void End()
        {
            if (!_active) return;   // idempotent — Ended fires exactly once
            _active = false;
            _phase = Phase.Idle;
            _node = null;
            _lineIndex = 0;
            _visibleOptions.Clear();
            Ended?.Invoke();
        }
    }
}
