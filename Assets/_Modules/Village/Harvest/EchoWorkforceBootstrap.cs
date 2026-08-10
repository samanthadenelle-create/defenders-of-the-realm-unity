// =============================================================================
// EchoWorkforceBootstrap -- self-installs the Echo Workforce V1 at runtime
// (ECHO_WORKFORCE_SPEC), no scene authoring / no VillageSceneBuilder re-save,
// mirroring OfflineHarvestBootstrap.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// One persistent EchoService across scenes (the farm faucet is global, not
// per-scene). Installed AfterSceneLoad so GameStateService (loads the save in its
// Awake) is up before EchoService reads EchoCount / the silo clock.
//
// WORKERMANAGER RECONCILE (do NOT run two competing harvest systems): the Echo
// model is the V1 workforce abstraction, so we RETIRE WorkerManager's harvest role
// here -- its self-contained offline catch-up is already off (UseOfflineCatchUp =
// false, superseded by OfflineHarvestService), and we DISABLE its click-to-dispatch
// + auto-collect tick so the capsule worker no longer banks nodes in parallel with
// the Echoes. (We leave the component present/visual; the Echo silo is the single
// V1 faucet.) This is idempotent + null-safe.
// =============================================================================
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Installs the single persistent <see cref="EchoService"/> + reconciles WorkerManager.</summary>
    public static class EchoWorkforceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (EchoService.Instance == null)
            {
                var go = new GameObject("EchoService");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<EchoService>();
                go.AddComponent<EchoWaveUnlockBridge>();       // routes WaveManager.OnWaveCleared -> Echo unlocks
                go.AddComponent<EchoWorkforceHud>();          // WO-555: hidden Echo panel, opened by the HUD harvest button (next to Settings) via HarvestPanelGate
                go.AddComponent<EchoUnlockFeedback>();        // F8 2026-07-15: unmissable in-view unlock feedback (persistent pip + center banner + reward SFX), independent of the hidden panel
                go.AddComponent<EchoRepairService>();         // WO-811: the REPAIR task consumer (real repair via WallRepairController; offline-fair on the shared clock)
            }

            // Reconcile WorkerManager: retire its competing harvest role for V1 so it
            // can't bank the same nodes the Echo silo abstracts. Runs every scene load
            // (a fresh WorkerManager may spawn per scene) -- idempotent + null-safe.
            RetireWorkerHarvest();
        }

        /// <summary>
        /// Disable WorkerManager's live harvest verbs (click-dispatch + the per-frame
        /// auto-collect drive) so the capsule worker stops banking nodes in parallel
        /// with the Echo workforce. The component stays (visual / future re-use); only
        /// its harvest ROLE is retired. Its offline catch-up is already off by default.
        /// </summary>
        private static void RetireWorkerHarvest()
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;
            wm.ClickToDispatch = false;       // no click -> no new node claims
            wm.UseOfflineCatchUp = false;     // belt-and-braces: OfflineHarvestService owns offline
            DeNelle.Core.Diagnostics.FlowTrace.Step("Echo",
                "WorkerManager harvest role retired for V1 (ClickToDispatch off) -- Echo silo is the single faucet.");
        }
    }
}
