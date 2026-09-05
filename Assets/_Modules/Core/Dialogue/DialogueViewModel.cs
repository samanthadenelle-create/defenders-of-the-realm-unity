// =============================================================================
// DialogueViewModel — MVVM VM for our dialogue (WO-455). Holds ALL state/logic;
// the DialogueView is a dumb uGUI skin that reads these and calls Advance/Choose.
// (Presentation-separation canon: the View never touches the runner or game state.)
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Core.Dialogue
{
    public sealed class DialogueViewModel : IPanelViewModel
    {
        private readonly DialogueRunner _runner = new DialogueRunner();
        private readonly List<string> _optionLabels = new List<string>();

        public bool IsOpen { get; private set; }
        public string Speaker { get; private set; } = "";
        public string Text { get; private set; } = "";
        public bool ShowingOptions { get; private set; }
        public IReadOnlyList<string> OptionLabels => _optionLabels;

        /// <summary>IPanelViewModel header text. The dialogue View renders Speaker directly, but
        /// the binding contract needs a Title — the current speaker name is the natural header.</summary>
        public string Title => Speaker;

        // ── Catalog/portrait projections (WO-744 MVVM: moved OFF the View) ────────
        // The View used to read DialogueCatalog.FindSpeaker + DialoguePortrait.Forced itself.
        // Those catalog/state reads now live HERE as read-only projections; the View binds the
        // strings and resolves sprites (a presentation concern) from them.

        /// <summary>The current speaker's guild/shop affiliation sub-line (card standard), or null.
        /// The GUIDE (WO-1012 P2 — lines authored with the "{guide}" token, resolved in OnLine)
        /// carries its identity-seam affiliation; everyone else resolves via the speakers block.</summary>
        public string Affiliation
        {
            get
            {
                if (Tutorial.TutorialGuide.IsGuideSpeaker(Speaker))
                    return Tutorial.TutorialGuide.Affiliation;
                var rec = DialogueCatalog.FindSpeaker(Speaker);
                return rec != null ? rec.Affiliation : null;
            }
        }

        /// <summary>The AUTHORED portrait Resources path for the current speaker: an active
        /// per-node `portrait` command override wins, else the speakers-block record's portrait.
        /// Null when neither is set — the View then falls back to a class portrait / silhouette
        /// (presentation). The View never touches DialoguePortrait / DialogueCatalog directly.</summary>
        public string PortraitPath
        {
            get
            {
                string forced = DeNelle.Core.DialoguePortrait.Forced;
                if (!string.IsNullOrEmpty(forced)) return forced;
                var rec = DialogueCatalog.FindSpeaker(Speaker);
                if (rec != null && !string.IsNullOrEmpty(rec.Portrait)) return rec.Portrait;
                return null;
            }
        }

        /// <summary>True when <see cref="PortraitPath"/> came from a per-node `portrait` command
        /// override (vs the speakers-block default) — the View marks its trace source accordingly.</summary>
        public bool PortraitForced => !string.IsNullOrEmpty(DeNelle.Core.DialoguePortrait.Forced);

        // ── WO-702 dialogue/builder truce (RELOCATED from DialogueView, WO-744) ───
        // The truce write BuildModeState.DialogueHiddenForBuilder USED to live in the View's
        // per-frame TickBuilderTruce. Naive removal RE-FREEZES Build Mode (the founding_town
        // softlock: closing a dialogue-gated step's intro would falsely fire Ended). So it is
        // RELOCATED here — the VM owns the truce flag + the publish. The View only polls
        // BuildModeState.IsActive each frame and forwards it via SetBuilderActive, then reads
        // HiddenForBuilder to hide (NOT close) the live panel. The flag is CLEARED on OnEnded so
        // it can never stick true after teardown (the View's old self-healing live==false publish).

        /// <summary>TRUE while a LIVE dialogue is held hidden because the builder is open.</summary>
        public bool HiddenForBuilder { get; private set; }

        /// <summary>Forward the per-frame builder state (the View polls BuildModeState.IsActive).
        /// Owns the truce publish: BuildModeState.DialogueHiddenForBuilder = hidden AND still open,
        /// so the build placement loop knows the input lock belongs to an invisible dialogue and
        /// stays usable. Returns TRUE when the hidden state CHANGED (the View repaints + re-arms
        /// its advance min-hold on a change).</summary>
        public bool SetBuilderActive(bool builderActive)
        {
            bool changed = builderActive != HiddenForBuilder;
            HiddenForBuilder = builderActive;
            DeNelle.Core.BuildModeState.DialogueHiddenForBuilder = HiddenForBuilder && IsOpen;
            return changed;
        }

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
            // A per-node `portrait` command override is scoped to ITS dialogue: clear the sticky
            // static at every Begin (before the runner fires this dialogue's commands) so the
            // previous conversation's forced portrait can't leak onto this one. RELOCATED from the
            // View's OnOpened (WO-744) — the first visible paint runs after Begin, so this reset
            // still precedes any RefreshPortrait read.
            DeNelle.Core.DialoguePortrait.Forced = null;
            IsOpen = true;
            _runner.Begin(def, sink, cond);
        }

        public void Advance() => _runner.Advance();
        public void Choose(int i) => _runner.Choose(i);
        public void Close() => _runner.Stop();   // synchronous, race-free

        /// <summary>IPanelViewModel disposal — detach the runner handlers + null the events so no
        /// handler leaks (mirrors the panel VMs' unsubscribe discipline). Idempotent.</summary>
        public void Dispose()
        {
            _runner.LineShown -= OnLine;
            _runner.OptionsShown -= OnOptions;
            _runner.Ended -= OnEnded;
            Changed = null;
            Closed = null;
        }

        private void OnLine(DialogueLine l)
        {
            ShowingOptions = false;
            // WO-1012 P2: tutorial lines author the "{guide}" speaker token — resolve it
            // to the live guide identity (the pet-Echo) at surface time. Copy unchanged;
            // non-token speakers pass through untouched.
            Speaker = l != null ? Tutorial.TutorialGuide.ResolveToken(l.Speaker ?? "") : "";
            // WO-1389: line TEXT may carry live-data tokens ("{army.used}", "{camp.next.defenders}")
            // registered by gameplay (DialogueTextTokens). Resolved at surface time so an authored
            // sentence can never go stale against the catalog it describes; unknown tokens pass
            // through untouched, so every earlier line is byte-identical to before.
            Text = l != null ? DialogueTextTokens.Resolve(l.Text ?? "") : "";
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
            // WO-702 truce: a dialogue that ends (naturally, or superseded/closed while hidden)
            // MUST release the builder truce so BuildModeState.DialogueHiddenForBuilder can never
            // stick true after teardown (replaces the View's old live==false self-healing publish).
            HiddenForBuilder = false;
            DeNelle.Core.BuildModeState.DialogueHiddenForBuilder = false;
            Changed?.Invoke();
            Closed?.Invoke();
        }
    }
}
