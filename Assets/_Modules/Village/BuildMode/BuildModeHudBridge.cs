// =============================================================================
// BuildModeHudBridge — hides the bottom-middle COMBAT HUD while Build Mode is open.
// -----------------------------------------------------------------------------
// OWNER ASK: in Build Mode the bottom-middle combat cluster (the hero HP red /
// mana-XP blue status bars AND the 4-slot Q/W/E/R ability bar) OVERLAPS and blocks
// the build PALETTE, so the player can't see or pick items. Hide that cluster while
// building, restore it on exit.
//
// TRIGGER: BuildModeController.BuildModeChanged — a static Action<bool> fired true
// on Enter() and false on Exit(). This bridge subscribes once and forwards the flag
// to the HUD as SetCombatHudVisible(!building).
//
// CROSS-ASMDEF: DeNelle.Village must NOT reference DeNelle.HUD (HUD is Core-only).
// So — exactly like StartWaveHudBridge — the HUD object is resolved via
// CoreServices.Hud (IVillageHud lives in DeNelle.Core) and SetCombatHudVisible(bool)
// is reflected by name (it's a HUD extra, not on the IVillageHud interface).
//
// SELF-BOOTSTRAP: a pure static event hook (no scene object, no WaveManager
// dependency) installed at RuntimeInitializeOnLoadMethod, so it's live in every
// scene with no Village.unity re-save. Idempotent across play sessions (domain
// reload disabled → reset the guard at subsystem registration).
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Toggles the HUD combat cluster off while Build Mode is active.</summary>
    public static class BuildModeHudBridge
    {
        private static bool s_hooked;
        private static MethodInfo s_setCombatHudVisible;
        private static object s_boundHud;

        // Domain-reload disabled → statics persist between Play sessions, so reset
        // the guard at subsystem registration (runs before AfterSceneLoad).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_hooked = false;
            s_setCombatHudVisible = null;
            s_boundHud = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            BuildModeController.BuildModeChanged -= OnBuildModeChanged;
            BuildModeController.BuildModeChanged += OnBuildModeChanged;
        }

        /// <summary>Hide the combat HUD while building (true), restore it on exit (false).</summary>
        private static void OnBuildModeChanged(bool building)
        {
            SetCombatHudVisible(!building);
        }

        /// <summary>
        /// Resolve the HUD via CoreServices.Hud and reflect SetCombatHudVisible(bool) by
        /// name (re-resolving if the HUD instance changed across a scene reload), then
        /// invoke it. No-op + warns once if the method isn't found.
        /// </summary>
        private static void SetCombatHudVisible(bool visible)
        {
            object hud = CoreServices.Hud as object;
            if (hud == null) return;

            if (!ReferenceEquals(hud, s_boundHud) || s_setCombatHudVisible == null)
            {
                s_boundHud = hud;
                s_setCombatHudVisible = hud.GetType().GetMethod(
                    "SetCombatHudVisible",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(bool) }, null);
                if (s_setCombatHudVisible == null)
                    Debug.LogWarning("[BuildModeHudBridge] HUD.SetCombatHudVisible(bool) not found — " +
                                     "the combat HUD won't hide in Build Mode.");
            }

            s_setCombatHudVisible?.Invoke(hud, new object[] { visible });
        }
    }
}
