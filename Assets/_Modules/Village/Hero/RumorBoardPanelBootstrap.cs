// =============================================================================
//  RumorBoardPanelBootstrap — eager PanelRouter registration (owner 2026-06-20)
// -----------------------------------------------------------------------------
//  The HUD context button (VillageHudController) opens the real quest/rumor board
//  via PanelRouter.Open(PanelId.RumorBoard). The board's opener used to be registered
//  ONLY inside DialogueCommandBridge.Install — which runs lazily, the first time a
//  dialogue plays (DialogueService hosts the runner on demand). So tapping the HUD
//  Quest button COLD (before any NPC conversation) found no opener registered and
//  nothing opened.
//
//  This [RuntimeInitializeOnLoadMethod] registers the board opener at scene-load,
//  independent of the dialogue lifecycle, so the HUD button always has a target. The
//  opener find-or-spawns the panel (same idiom DialogueCommandBridge.OpenRumorBoard
//  uses). DialogueCommandBridge may also register its instance method later; Register
//  is replace-last-wins and both openers drive the same surface, so that's harmless.
// =============================================================================
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Village.Hero
{
    internal static class RumorBoardPanelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            PanelRouter.Register(PanelId.RumorBoard, OpenRumorBoard);
        }

        /// <summary>Find-or-spawn the rumor board and open it. Mirrors
        /// DialogueCommandBridge.OpenRumorBoard so the HUD and dialogue paths share one surface.</summary>
        private static void OpenRumorBoard()
        {
            var panel = Object.FindObjectOfType<RumorBoardPanel>();
            if (panel == null)
                panel = new GameObject("RumorBoardPanelHost").AddComponent<RumorBoardPanel>();
            panel.Open();
        }
    }
}
