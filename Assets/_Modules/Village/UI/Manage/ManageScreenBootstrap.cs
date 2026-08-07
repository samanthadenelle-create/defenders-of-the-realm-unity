// =============================================================================
// ManageScreenBootstrap — spawns the WO-911 Manage/Queues screen, scene-free.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Scene-independent on purpose. The bar face that opens this screen is present in
// every calm(town) posture, so the panel must exist without a per-scene wiring
// step or the door leads nowhere in whichever scene someone forgot to bake it
// into. Mirrors the RealmMapPanelBootstrap / BuildingUpgradePanelMvvmBootstrap
// pattern already used for every other PanelRouter-registered panel.
//
// Idempotent + DontDestroyOnLoad: the screen registers its PanelId ONCE for the
// life of the process, so a scene load never leaves a dead route behind.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village.UI
{
    /// <summary>Installs the single <see cref="ManageScreenPanel"/> instance.</summary>
    public static class ManageScreenBootstrap
    {
        private static ManageScreenPanel _instance;

        /// <summary>Create the panel host after the first scene load (idempotent).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            Guard.Try("Manage", "install manage screen", () =>
            {
                var go = new GameObject("ManageScreenPanel");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<ManageScreenPanel>();
                FlowTrace.Step("Manage",
                    "ManageScreenPanel installed (PanelId.Manage + ObsidianQueueGate.ToggleRequested).");
            });
        }
    }
}
