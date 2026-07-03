// =============================================================================
// HudMoveInput — the kit's movement-input static (replaces VirtualDPadLean.Move).
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 §1.11 — P23 HUDKIT.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// The four round controller buttons (ElarionUiKit.BuildControllerCluster) write
// the held direction here; HeroLocomotion reads it by the SAME loose-reflection
// pattern it used for VirtualDPadLean (Type.GetType + static Move property —
// no Village->HUD assembly edge). HeroLocomotion's type string is repointed to
// "DeNelle.HUD.Kit.HudMoveInput, DeNelle.HUD" in the same change (P23).
// =============================================================================

using UnityEngine;

namespace DeNelle.HUD.Kit
{
    /// <summary>Current HUD movement deflection (-1..1 per axis; zero when released).</summary>
    public static class HudMoveInput
    {
        /// <summary>Read by HeroLocomotion via loose reflection (see header).</summary>
        public static Vector2 Move { get; private set; }

        /// <summary>Written by the kit's controller cluster only.</summary>
        public static void Set(Vector2 v)
        {
            Move = Vector2.ClampMagnitude(v, 1f);
        }
    }
}
