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
            vm.Closed += () => { if (ReferenceEquals(ActiveVm, vm)) ActiveVm = null; };

            FlowTrace.Step("Dialogue", $"Play '{dialogueId}'.");
            Opened?.Invoke(vm);              // View builds + binds BEFORE the first line fires
            vm.Begin(def, _sink, _cond);     // enters the entry node -> first line -> vm.Changed -> render
            return true;
        }

        /// <summary>Stop the active dialogue (synchronous, race-free).</summary>
        public static void Stop() => ActiveVm?.Close();
    }
}
