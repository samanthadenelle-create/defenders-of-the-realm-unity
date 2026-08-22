// =============================================================================
// DefenseReportPanelBootstrap — spawns the WO-1026 Defence Report screen, scene-free.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Scene-independent on purpose (CLAUDE.md §3 forbids hand-editing .unity scenes, and a
// per-scene wiring step means a dead route in whichever scene someone forgot to bake).
// Mirrors ManageScreenBootstrap / RealmMapPanelBootstrap.
//
// Idempotent + DontDestroyOnLoad: the panel registers PanelId.DefenseReport ONCE for the
// life of the process, so a scene load never leaves a dead route behind.
//
// It installs regardless of FeatureFlags.Siege. The flag gates whether attacks HAPPEN;
// the report screen must still open on a save that already holds reports (and must open
// to an honest empty state on one that does not) — a route that appears and disappears
// with a flag is a route nobody can reason about.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village.UI
{
    /// <summary>Installs the single <see cref="DefenseReportPanel"/> instance.</summary>
    public static class DefenseReportPanelBootstrap
    {
        private static DefenseReportPanel _instance;

        /// <summary>Create the panel host after the first scene load (idempotent).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            Guard.Try("Siege", "install defense report panel", () =>
            {
                var go = new GameObject("DefenseReportPanel");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<DefenseReportPanel>();
                FlowTrace.Step("Siege", "DefenseReportPanel installed (PanelId.DefenseReport).");
            });
        }
    }
}
