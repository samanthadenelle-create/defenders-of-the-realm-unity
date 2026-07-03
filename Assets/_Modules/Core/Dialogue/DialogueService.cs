// =============================================================================
// DialogueService (DeNelle.Core.Dialogue) — the seam for OUR dialogue (WO-455).
// -----------------------------------------------------------------------------
// Gameplay calls Play(dialogueId). A View (subscribed to Opened) builds its panel
// and binds to the returned VM. Village registers the command sink + condition
// source at boot (like CoreServices), so Core never references gameplay.
//
// Distinct from the Yarn-hosted DeNelle.Village.DialogueService it replaces — both
// can coexist during the flag-gated migration (FeatureFlags.CustomDialogue).
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Dialogue
{
    public static class DialogueService
    {
        private static IDialogueCommandSink _sink;
        private static IDialogueConditionSource _cond;

        /// <summary>Village registers these at boot (the verbs + condition evaluation).</summary>
        public static void RegisterSink(IDialogueCommandSink sink) => _sink = sink;
        public static void RegisterConditions(IDialogueConditionSource cond) => _cond = cond;

        /// <summary>Raised when a dialogue opens — the View subscribes, builds its panel, binds the VM.</summary>
        public static event Action<DialogueViewModel> Opened;

        /// <summary>Raised when ANY dialogue begins. Parameterless engine-wide signal for
        /// systems that only need "a conversation is on / off" — e.g. HeroLocomotion /
        /// HeroBodySwapper input-suppression (replaces the old Yarn onDialogueStart hook).</summary>
        public static event Action Started;
        /// <summary>Raised when the active dialogue ends (naturally or via Stop()). Pair of Started.</summary>
        public static event Action Ended;

        /// <summary>WO-T1 (Tutorial V2) — like <see cref="Ended"/> but carries the dialogue id that
        /// ended, so a listener (TutorialSignals "dialogue.ended:&lt;id&gt;") can key on WHICH
        /// conversation finished without tracking Play() calls itself. Additive — the
        /// parameterless <see cref="Ended"/> is unchanged and still fires.</summary>
        public static event Action<string> EndedWithId;

        public static DialogueViewModel ActiveVm { get; private set; }
        public static bool IsRunning => ActiveVm != null && ActiveVm.IsOpen;

        /// <summary>Play a dialogue by id. False if unknown. The View renders it; a command-only
        /// dialogue (e.g. just OpenShop) fires + closes immediately with no empty panel.</summary>
        public static bool Play(string dialogueId)
        {
            var def = DialogueCatalog.Find(dialogueId);
            if (def == null)
            {
                FlowTrace.Warn("Dialogue", $"Play: unknown dialogue id '{dialogueId}'.");
                return false;
            }
            var vm = new DialogueViewModel();
            ActiveVm = vm;
            vm.Closed += () =>
            {
                if (ReferenceEquals(ActiveVm, vm)) ActiveVm = null;
                Ended?.Invoke();
                EndedWithId?.Invoke(dialogueId);   // WO-T1 — id-carrying twin of Ended
            };

            FlowTrace.Step("Dialogue", $"Play '{dialogueId}'.");
            Opened?.Invoke(vm);              // View builds + binds BEFORE the first line fires
            Started?.Invoke();               // engine-wide "dialogue on" signal (input suppression etc.)
            vm.Begin(def, _sink, _cond);     // enters the entry node -> first line -> vm.Changed -> render
            return true;
        }

        /// <summary>Stop the active dialogue (synchronous, race-free).</summary>
        public static void Stop() => ActiveVm?.Close();
    }
}
