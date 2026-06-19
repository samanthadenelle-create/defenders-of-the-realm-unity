// =============================================================================
// BuildModeHudBridge — hides the WHOLE HUD while Build Mode is open.
// -----------------------------------------------------------------------------
// OWNER ASK (build-mode soft-blocker, 2026-06-19): in Build Mode the town HUD —
// especially the bottom-right town action diamond (Build/Talk/Shop/Quest) plus the
// combat clusters — OVERLAPS the build palette and STEALS the tap from the palette's
// Done button, so the player can't see/pick items OR exit build mode. Hide the whole
// HUD while building, restore it on exit. (Earlier this only hid the combat cluster,
// which left the town diamond up as the occluder.)
//
// TRIGGER: BuildModeController.BuildModeChanged — a static Action<bool> fired true
// on Enter() and false on Exit(). This bridge subscribes once and forwards the flag
// to the HUD as SetHudVisible(!building) (fades the root CanvasGroup + drops
// blocksRaycasts so nothing under the palette intercepts the Done tap). NO hotkey.
//
// CROSS-ASMDEF: DeNelle.Village must NOT reference DeNelle.HUD (HUD is Core-only).
// So — exactly like StartWaveHudBridge — the HUD object is resolved via
// CoreServices.Hud (IVillageHud lives in DeNelle.Core) and SetHudVisible(bool)
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
        private static MethodInfo s_setHudVisible;
        private static object s_boundHud;

        // Domain-reload disabled → statics persist between Play sessions, so reset
        // the guard at subsystem registration (runs before AfterSceneLoad).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_hooked = false;
            s_setHudVisible = null;
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

        /// <summary>Hide the WHOLE HUD while building (true), restore it on exit (false).</summary>
        private static void OnBuildModeChanged(bool building)
        {
            // WO (build-mode soft-blocker): hiding only the combat cluster left the town
            // action diamond (_townActionPanel, bottom-right) up — it OVERLAPS and steals the
            // tap from the build palette's Done button, so the player couldn't exit build mode.
            // SetHudVisible(false) fades the root CanvasGroup (every town + combat cluster) AND
            // drops blocksRaycasts, so nothing under the palette can swallow the Done tap.
            // Event-driven off BuildModeChanged — NO hotkey.
            SetHudVisible(!building);
        }

        /// <summary>
        /// Resolve the HUD via CoreServices.Hud and reflect SetHudVisible(bool) by name
        /// (re-resolving if the HUD instance changed across a scene reload), then invoke it.
        /// No-op + warns once if the method isn't found.
        /// </summary>
        private static void SetHudVisible(bool visible)
        {
            object hud = CoreServices.Hud as object;
            if (hud == null) return;

            if (!ReferenceEquals(hud, s_boundHud) || s_setHudVisible == null)
            {
                s_boundHud = hud;
                s_setHudVisible = hud.GetType().GetMethod(
                    "SetHudVisible",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(bool) }, null);
                if (s_setHudVisible == null)
                    Debug.LogWarning("[BuildModeHudBridge] HUD.SetHudVisible(bool) not found — " +
                                     "the HUD won't hide in Build Mode (Done button may stay occluded).");
            }

            s_setHudVisible?.Invoke(hud, new object[] { visible });
        }
    }
}
