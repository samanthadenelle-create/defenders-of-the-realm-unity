// =============================================================================
// HudPostureReset — drop combat engagement signals so the kit returns to calm UI.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hud
//
// Called when combat ends (Battle context falls) or a hub scene loads so the HUD
// does not stay in hostile(prebattle/activebattle) after a fight (owner 2026-07-05).
// =============================================================================

using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;

namespace DeNelle.Village.Hud
{
    /// <summary>Clears pursuit pulses + manual target lock for peaceful posture.</summary>
    internal static class HudPostureReset
    {
        /// <summary>Combat just ended — release locks so explore/town UI can return.</summary>
        public static void OnCombatEnded()
        {
            ClearEngagementSignals("combat-ended");
        }

        /// <summary>Hub scene loaded — force peaceful town chrome.</summary>
        public static void OnHubLoaded(string sceneName)
        {
            ClearEngagementSignals("hub-load:" + sceneName);
        }

        private static void ClearEngagementSignals(string reason)
        {
            PostureSignals.ClearPursuits();

            GameObject hero = null;
            try { hero = GameObject.FindWithTag("Player"); }
            catch { hero = null; }

            if (hero != null)
            {
                var indicator = hero.GetComponent<HeroTargetIndicator>();
                if (indicator != null && indicator.LockEngaged)
                    indicator.ReleaseLock();
            }

            var model = CoreServices.HudModel;
            model?.Target?.Clear();

            FlowTrace.Step("HudKit", "posture reset (" + reason + ") -> peaceful");
        }
    }
}