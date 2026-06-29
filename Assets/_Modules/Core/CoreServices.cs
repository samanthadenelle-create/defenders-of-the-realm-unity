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
using DeNelle.Core.HudModel;
using DeNelle.Core.Population;
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

        /// <summary>Registers the village HUD. Called by VillageHudController.Awake.
        /// Main-thread only (no locking) — registrations happen in Awake/OnDestroy.</summary>
        public static void RegisterHud(IVillageHud hud)
        {
            if (Hud != null && !ReferenceEquals(Hud, hud))
                Debug.LogWarning("[CoreServices] Replacing existing IVillageHud registration.");
            Hud = hud;
        }

        /// <summary>Unregisters the village HUD. Called by VillageHudController.OnDestroy.</summary>
        public static void UnregisterHud(IVillageHud hud) { if (ReferenceEquals(Hud, hud)) Hud = null; }

        // ── HUD model layer (WO-541) ──────────────────────────────────────────
        /// <summary>
        /// The active HUD model facade (read-only data + Changed events), or null
        /// when no HudModelHost is present in the loaded scenes. Producers write the
        /// models; views read them. Always null-check before use.
        /// </summary>
        public static IHudModel HudModel { get; private set; }

        /// <summary>Registers the HUD model facade. Called by HudModelHost.Awake (WO-541 Stage 2).
        /// Main-thread only (no locking) — registrations happen in Awake/OnDestroy.</summary>
        public static void RegisterHudModel(IHudModel m)
        {
            if (HudModel != null && !ReferenceEquals(HudModel, m))
                Debug.LogWarning("[CoreServices] Replacing existing IHudModel registration.");
            HudModel = m;
            DeNelle.Core.Diagnostics.FlowTrace.Step("HUD", "HudModel registered");
        }

        /// <summary>Unregisters the HUD model facade. Called by HudModelHost.OnDestroy.</summary>
        public static void UnregisterHudModel(IHudModel m) { if (ReferenceEquals(HudModel, m)) HudModel = null; }

        // ── Population growth (WO-587) ────────────────────────────────────────
        /// <summary>
        /// The active Population growth service, or null when no PopulationService is
        /// present (it self-bootstraps via PopulationBootstrap). Drives Echo workforce
        /// slot unlocks from milestones. Always null-check before use.
        /// </summary>
        public static IPopulationService Population { get; private set; }

        /// <summary>Registers the Population service. Called by PopulationService.Awake.
        /// Main-thread only (no locking) — registrations happen in Awake/OnDestroy.</summary>
        public static void RegisterPopulation(IPopulationService svc)
        {
            if (Population != null && !ReferenceEquals(Population, svc))
                Debug.LogWarning("[CoreServices] Replacing existing IPopulationService registration.");
            Population = svc;
        }

        /// <summary>Unregisters the Population service. Called by PopulationService.OnDestroy.</summary>
        public static void UnregisterPopulation(IPopulationService svc) { if (ReferenceEquals(Population, svc)) Population = null; }

        // ── Audio (WO-41) ─────────────────────────────────────────────────────
        /// <summary>
        /// The active audio service, or null before AudioBootstrap has run.
        /// Always null-check before use.
        /// </summary>
        public static IAudioService Audio { get; private set; }

        /// <summary>Registers the audio service. Called by AudioService.Awake.
        /// Main-thread only (no locking) — registrations happen in Awake/OnDestroy.</summary>
        public static void RegisterAudio(IAudioService audio)
        {
            if (Audio != null && !ReferenceEquals(Audio, audio))
                Debug.LogWarning("[CoreServices] Replacing existing IAudioService registration.");
            Audio = audio;
        }

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

        // ── Wallet signer (WO-121 backend save-auth) ─────────────────────────
        /// <summary>
        /// The active wallet message-signer, or null when no wallet is
        /// connected. Registered by WalletService (DeNelle.Wallet) on Connect,
        /// unregistered on Disconnect. GameStateService resolves it to sign the
        /// backend save/load auth nonce. Always null-check; even when non-null,
        /// check <see cref="IWalletSigner.CanSign"/> before signing (the devnet
        /// stub registers but cannot sign).
        /// </summary>
        public static IWalletSigner WalletSigner { get; private set; }

        /// <summary>Registers the wallet signer. Called by WalletService when a wallet connects.</summary>
        public static void RegisterWalletSigner(IWalletSigner signer)
        {
            if (WalletSigner != null && WalletSigner != signer)
                Debug.Log("[CoreServices] Replacing existing IWalletSigner registration.");
            WalletSigner = signer;
        }

        /// <summary>Unregisters the wallet signer. Called by WalletService on disconnect.</summary>
        public static void UnregisterWalletSigner(IWalletSigner signer)
        {
            if (ReferenceEquals(WalletSigner, signer)) WalletSigner = null;
        }
    }
}
