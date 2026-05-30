// =============================================================================
// CoreServices — cross-assembly service registry (WO-41 + WO-43).
// -----------------------------------------------------------------------------
// A static registry living in DeNelle.Core (referenced by every module) that
// lets implementing modules register concrete services behind Core-defined
// interfaces, so consumers never need an assembly reference to the implementor.
//
// Slots:
//   Jupiter — IJupiterService (WO-43, DeNelle.Web3)
//   Hud     — IVillageHud    (WO-41, DeNelle.HUD)
//   Audio   — IAudioService  (WO-41, DeNelle.Audio)
//
// Each slot follows the same Register/Unregister pattern: the concrete
// MonoBehaviour calls Register in Awake and Unregister in OnDestroy.
// Callers MUST null-check (e.g. CoreServices.Hud?.SetWave(n)).
// =============================================================================

using DeNelle.Core.Audio;
using DeNelle.Core.HUD;
using DeNelle.Core.Web3;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// A static service registry for cross-assembly access to game-wide
    /// services. Slots are populated at runtime by the implementing module
    /// and consumed through Core-defined interfaces.
    /// </summary>
    public static class CoreServices
    {
        // ── HUD (WO-41) ───────────────────────────────────────────────────────
        /// <summary>
        /// The active village HUD, or null when no VillageHudController is
        /// present in the loaded scenes. Always null-check before use.
        /// </summary>
        public static IVillageHud Hud { get; private set; }

        /// <summary>Registers the village HUD. Called by VillageHudController.Awake.</summary>
        public static void RegisterHud(IVillageHud hud) { Hud = hud; }

        /// <summary>Unregisters the village HUD. Called by VillageHudController.OnDestroy.</summary>
        public static void UnregisterHud(IVillageHud hud) { if (ReferenceEquals(Hud, hud)) Hud = null; }

        // ── Audio (WO-41) ─────────────────────────────────────────────────────
        /// <summary>
        /// The active audio service, or null before AudioBootstrap has run.
        /// Always null-check before use.
        /// </summary>
        public static IAudioService Audio { get; private set; }

        /// <summary>Registers the audio service. Called by AudioService.Awake.</summary>
        public static void RegisterAudio(IAudioService audio) { Audio = audio; }

        /// <summary>Unregisters the audio service. Called by AudioService.OnDestroy.</summary>
        public static void UnregisterAudio(IAudioService audio) { if (ReferenceEquals(Audio, audio)) Audio = null; }

        // ── Jupiter swap service (WO-43) ─────────────────────────────────────
        /// <summary>
        /// The active Jupiter swap service, or null when no swap host is present
        /// in the loaded scenes. Always null-check before use.
        /// </summary>
        public static IJupiterService Jupiter { get; private set; }

        /// <summary>Registers the Jupiter swap service. Called by JupiterSwapService.Awake.</summary>
        public static void RegisterJupiter(IJupiterService svc)
        {
            if (Jupiter != null && Jupiter != svc)
                Debug.LogWarning("[CoreServices] Replacing existing IJupiterService registration.");
            Jupiter = svc;
        }

        /// <summary>Unregisters the Jupiter swap service. Called by JupiterSwapService.OnDestroy.</summary>
        public static void UnregisterJupiter(IJupiterService svc)
        {
            if (Jupiter == svc) Jupiter = null;
        }
    }
}
