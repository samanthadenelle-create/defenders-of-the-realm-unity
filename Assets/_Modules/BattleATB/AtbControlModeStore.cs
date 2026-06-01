// =============================================================================
// AtbControlModeStore — per-member ControlMode persistence (WO-169 P0)
// -----------------------------------------------------------------------------
// The WO requires each party member's PLAYER/AI control mode to be set when the
// member joins and changeable later in Settings, and to PERSIST across sessions.
//
// Rather than touch GameState's 41-field save round-trip (a schema bump owned by
// the save layer — out of scope for this code-only ATB pass), the preference is
// stored locally via PlayerPrefs keyed by the member's stable id. This keeps the
// change isolated to DeNelle.BattleATB and survives a session immediately, with
// a clean migration path: when the save layer is ready, swap the two Read/Write
// methods to read/write a GameState dictionary instead — call sites don't change.
//
// Default policy (when no preference is stored): the hero/anchor member defaults
// to Player; recruits and pets default to AI for casual play — but every member
// is flippable. The controller seeds the default on first join, then this store
// is the source of truth.
// =============================================================================

using DeNelle.BattleATB.Engine;
using UnityEngine;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Persists each ATB party member's <see cref="ControlMode"/> (Player/AI) by
    /// member id. Backed by PlayerPrefs today; a thin enough seam that the backing
    /// store can move to GameState later without touching callers.
    /// </summary>
    public static class AtbControlModeStore
    {
        private const string KeyPrefix = "atb.controlMode.";

        /// <summary>
        /// The stored mode for <paramref name="memberId"/>, or
        /// <paramref name="fallback"/> when nothing has been saved yet (first join).
        /// </summary>
        public static ControlMode Get(string memberId, ControlMode fallback)
        {
            if (string.IsNullOrEmpty(memberId)) return fallback;
            // 0 = Player, 1 = AI; -1 sentinel = "no preference stored".
            int stored = PlayerPrefs.GetInt(KeyPrefix + memberId, -1);
            if (stored < 0) return fallback;
            return stored == (int)ControlMode.AI ? ControlMode.AI : ControlMode.Player;
        }

        /// <summary>Persist <paramref name="mode"/> as the member's preference.</summary>
        public static void Set(string memberId, ControlMode mode)
        {
            if (string.IsNullOrEmpty(memberId)) return;
            PlayerPrefs.SetInt(KeyPrefix + memberId, (int)mode);
            PlayerPrefs.Save();
        }

        /// <summary>True when a preference has been explicitly stored for this member.</summary>
        public static bool HasPreference(string memberId)
        {
            return !string.IsNullOrEmpty(memberId)
                   && PlayerPrefs.GetInt(KeyPrefix + memberId, -1) >= 0;
        }
    }
}
