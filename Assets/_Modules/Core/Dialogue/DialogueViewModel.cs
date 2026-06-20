// =============================================================================
// DialogueViewModel — MVVM VM for our dialogue (WO-455). Holds ALL state/logic;
// the DialogueView is a dumb uGUI skin that reads these and calls Advance/Choose.
// (Presentation-separation canon: the View never touches the runner or game state.)
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.Dialogue
{
    public sealed class DialogueViewModel
    {
        private readonly DialogueRunner _runner = new DialogueRunner();
        private readonly List<string> _optionLabels = new List<string>();

        public bool IsOpen { get; private set; }
        public string Speaker { get; private set; } = "";
        public string Text { get; private set; } = "";
        public bool ShowingOptions { get; private set; }
        public IReadOnlyList<string> OptionLabels => _optionLabels;

        /// <summary>Raised whenever the VM's visible state changes — the View repaints on this.</summary>
        public event Action Changed;
        /// <summary>Raised once when the dialogue ends — the View tears its panel down.</summary>
        public event Action Closed;

        public DialogueViewModel()
        {
            _runner.LineShown += OnLine;
            _runner.OptionsShown += OnOptions;
            _runner.Ended += OnEnded;
        }

        /// <summary>Wire the View FIRST (subscribe to Changed/Closed), THEN call Begin — the
        /// runner enters the entry node synchronously and fires the first line during this call.</summary>
        public void Begin(DialogueDef def, IDialogueCommandSink sink, IDialogueConditionSource cond)
        {
            IsOpen = true;
            _runner.Begin(def, sink, cond);
        }

        public void Advance() => _runner.Advance();
        public void Choose(int i) => _runner.Choose(i);
        public void Close() => _runner.Stop();   // synchronous, race-free

        private void OnLine(DialogueLine l)
        {
            ShowingOptions = false;
            Speaker = l != null ? (l.Speaker ?? "") : "";
            Text = l != null ? (l.Text ?? "") : "";
            _optionLabels.Clear();
            Changed?.Invoke();
        }

        private void OnOptions(IReadOnlyList<DialogueOption> opts)
        {
            ShowingOptions = true;
            _optionLabels.Clear();
            if (opts != null) foreach (var o in opts) _optionLabels.Add(o != null ? (o.Text ?? "") : "");
            Changed?.Invoke();
        }

        private void OnEnded()
        {
            IsOpen = false;
            ShowingOptions = false;
            Speaker = ""; Text = "";
            _optionLabels.Clear();
            Changed?.Invoke();
            Closed?.Invoke();
        }
    }
}
