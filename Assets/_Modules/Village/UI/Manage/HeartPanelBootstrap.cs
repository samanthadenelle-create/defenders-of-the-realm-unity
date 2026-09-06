// =============================================================================
// HeartPanelBootstrap — spawns the WO-2017 Heart of Elarion surface, scene-free.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Scene-independent on purpose, and for the same reason ManageScreenBootstrap is:
// the doors onto this screen (the Manage header face, every village-gated
// building card, every village-gated research row) exist in every calm(town)
// posture, so the panel must exist without a per-scene wiring step or the door
// leads nowhere in whichever scene someone forgot to bake it into.
//
// ⚠ A panel with no spawner is a panel with no door - that is exactly how
// BarracksPanel sat unreachable in the tree (OWNER_RULINGS_LOCKED §21). This file
// is the door's other half; deleting it silently retires PanelId.Heart.
//
// Idempotent + DontDestroyOnLoad: the screen registers its PanelId ONCE for the
// life of the process, so a scene load never leaves a dead route behind.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village.UI
{
    /// <summary>Installs the single <see cref="HeartPanel"/> instance.</summary>
    public static class HeartPanelBootstrap
    {
        private static HeartPanel _instance;

        /// <summary>Create the panel host after the first scene load (idempotent).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            Guard.Try("Heart", "install heart panel", () =>
            {
                var go = new GameObject("HeartPanel");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<HeartPanel>();
                FlowTrace.Step("Heart", "HeartPanel installed (PanelId.Heart registered).");
            });
        }
    }
}
