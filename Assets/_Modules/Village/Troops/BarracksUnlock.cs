// =============================================================================
// BarracksUnlock - the ONE source of truth for the WO-724 Barracks unlock rule.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// CHARTER OPTION A (WO-723 charter + WO-724): the EXISTING baked Barracks
// (CastleBarracks) surfaces and its drillmaster + train UI unlock on
// FOUNDING-COMPLETE - NOT on a buildable-barracks / ff.basebuilding path
// (basebuilding stays OFF per the charter; a "buildable barracks" is out of scope).
//
// The founding-complete signal = GameState.Onboarded (set true by
// GameStateService.FinishOnboarding at the FTUE hand-off; the SAME gate the FTUE
// peace window + SylasStewardInjector.ArcIncomplete key on - no new flag).
//
// The feature flag ff.barracks stays DEFAULT OFF in code (owner 2026-07-10 V1 hide);
// testers opt in via PlayerPrefs "ff.barracks" = 1. Production default-ON is WO-731.
//
// EVERY runtime surface that decides whether the Barracks exists this session reads
// THIS predicate - the building visual (HubStructureVisualInjector), the drillmaster
// NPC (BarracksNpcInjector), and the train-UI verb (TroopDialogueCommands) - so the
// unlock rule lives in ONE place and can never drift between them.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Single source of truth for the WO-724 Barracks unlock (charter OPTION A):
    /// the Barracks surfaces + the drillmaster/train UI unlock when the feature flag
    /// is ON AND founding is complete. Read by every barracks runtime surface.
    /// </summary>
    public static class BarracksUnlock
    {
        /// <summary>
        /// The founding-complete signal = <see cref="GameState.Onboarded"/> (set by
        /// <c>GameStateService.FinishOnboarding</c>). No new flag - the SAME gate the
        /// FTUE peace window keys on. False when no save state is live.
        /// </summary>
        public static bool FoundingComplete
        {
            get
            {
                var svc = GameStateService.Instance;
                var state = svc != null ? svc.State : null;
                return state != null && state.Onboarded;
            }
        }

        /// <summary>
        /// The Barracks is surfaced/interactable when the feature flag is ON
        /// (<see cref="FeatureFlags.Barracks"/> - default OFF; testers set PlayerPrefs
        /// "ff.barracks" = 1) AND founding is complete. ff.basebuilding is NOT required.
        /// </summary>
        public static bool IsUnlocked => FeatureFlags.Barracks && FoundingComplete;
    }
}
