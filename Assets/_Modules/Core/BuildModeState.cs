// =============================================================================
// BuildModeState — Core cross-module seam for "is the builder open?" (WO-702).
// -----------------------------------------------------------------------------
// OWNER ASK (F8 felt-test 2026-07-13): "pause the sylas dialogue till either
// action asked is completed or closed builder". The captured collision:
// [Flow:Tutorial] STEP-STUCK :: founding_town — the step's intro dialogue
// opened BEHIND the build palette and sat unread for 120s.
//
// The truce needs two cross-assembly reads that the asmdef law forbids directly
// (Village -> Core only, HUD -> Core only, never Village <-> HUD):
//   * TutorialFlow (Village) must defer a step-intro DialogueService.Play while
//     the builder is open;
//   * DialogueView (HUD) must hide (NOT close) an already-open dialogue while
//     the builder is open and re-show it on exit.
// So the flag lives HERE, in Core — mirroring the CoreServices / DialoguePortrait
// static-seam pattern. Village (BuildModeController.Enter/Exit) WRITES IsActive;
// HUD + Village READ it. HUD (DialogueView) WRITES DialogueHiddenForBuilder while
// it is actually holding a live dialogue off-screen; Village (the build placement
// loop's InputSuppressed gate) READS it so the builder stays usable during the
// truce instead of freezing against the invisible dialogue's input lock.
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>Cross-assembly build-mode flags (WO-702 dialogue/builder truce).
    /// Village writes <see cref="IsActive"/>; HUD writes
    /// <see cref="DialogueHiddenForBuilder"/>; both sides only ever read the other's.</summary>
    public static class BuildModeState
    {
        /// <summary>TRUE while Build Mode is open (BuildModeController.Enter..Exit).
        /// Written by Village only.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>TRUE while the dialogue view is holding a LIVE (open, un-ended)
        /// dialogue hidden because the builder is open. Written by HUD only. Village's
        /// build placement loop reads it to bypass the dialogue input freeze — the
        /// hidden dialogue can't be mis-clicked, so the builder stays usable.</summary>
        public static bool DialogueHiddenForBuilder { get; set; }

        /// <summary>Village-side publisher (BuildModeController Enter/Exit/OnDestroy).</summary>
        public static void SetActive(bool active) => IsActive = active;

        // Domain reload disabled -> statics persist between Play sessions; reset at
        // subsystem registration so a session can never inherit a stale truce.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsActive = false;
            DialogueHiddenForBuilder = false;
        }
    }
}
