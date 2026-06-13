// =============================================================================
// DialogueResetService — wipes ALL Yarn dialogue state for a fresh New Game.
// -----------------------------------------------------------------------------
// "Start New" must begin with clean dialogue state. Without this, stale Yarn
// state from a prior run carries into the new game:
//   • the Yarn VARIABLE STORAGE ($-toggles like $structureCanShop, $upgradeType,
//     visited-node flags) — a re-used in-process runner keeps them set;
//   • the DialogueEventBus FIRED LATCHES (tower_placed, wave_cleared) — process
//     statics that survive scene loads;
// causing the dialogue-restart / option-corruption and stale vendor/quest state.
//
// This is the ONE tidy reset the Start-New path calls BEFORE loading gameplay.
// It lives in DeNelle.Core because that is the only assembly the Title screen
// (DeNelle.Onboarding) and the dialogue stack (DeNelle.Village, which owns the
// Yarn runner) both reference. The runner clear is a DECOUPLING HOOK
// (<see cref="YarnVariableClear"/>) the Village DialogueService registers at
// startup — mirroring the IntroLauncher / CoreServices service-locator pattern —
// so Core never needs a Yarn reference and the reset is a no-op (not a crash)
// when no runner exists yet (e.g. at title time, before the village is loaded).
//
// NEW-GAME ONLY: never call this on Continue — it would wipe a live game's
// dialogue progress. The once-only SeenTutorials recruit/intro gates are NOT
// touched here; GameStateService.ResetToNewGame() already clears those.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Resets all Yarn dialogue runtime state for a fresh New Game: clears the
    /// runner's variable storage (via the <see cref="YarnVariableClear"/> hook the
    /// dialogue stack registers) and the <see cref="DialogueEventBus"/> latches.
    /// Safe to call when no runner exists yet (title-time) — the hook is null and
    /// only the latches are cleared.
    /// </summary>
    public static class DialogueResetService
    {
        /// <summary>
        /// Set by the dialogue stack (DeNelle.Village.DialogueService) at startup.
        /// Clears the live DialogueRunner's VariableStorage if one is hosted. Null
        /// when the dialogue stack isn't loaded (e.g. at the Title screen, before
        /// any village scene) — in which case there is no runner state to clear and
        /// the latch wipe below is sufficient for the fresh start.
        /// </summary>
        public static System.Action YarnVariableClear;

        /// <summary>
        /// Wipes every piece of Yarn dialogue state so a New Game starts clean.
        /// Call this on the Start-New path BEFORE loading the gameplay scene.
        /// Each step is independently null-guarded so a missing piece never throws.
        /// </summary>
        public static void ResetForNewGame()
        {
            // 1) Yarn $-toggles / visited-node flags — only when a runner is live.
            //    Null-conditional: the hook is unset until the dialogue stack runs.
            try
            {
                YarnVariableClear?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DialogueResetService] Yarn variable clear hook threw — continuing: {e.Message}");
            }

            // 2) Gameplay→dialogue event latches (tower_placed, wave_cleared, …) so
            //    the FTUE waits resolve only on a FRESH raise, never a stale latch.
            DialogueEventBus.ClearAll();

            Debug.Log("[DialogueResetService] Yarn dialogue state reset for New Game (variables + event latches cleared).");
        }
    }
}
