// =============================================================================
// ArenaHudBridge — suppresses the whole gameplay HUD while an Arena setup screen
// (ATTACK recruit / DEFEND placement) is open, so the live town/wave HUD does
// not bleed through and clutter the modal (the WO this file lands with).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// CROSS-ASMDEF: DeNelle.Village must NOT reference DeNelle.HUD (HUD is Core-only,
// CLAUDE.md §5). So — exactly like BuildModeHudBridge — the HUD object is resolved
// via CoreServices.Hud (IVillageHud lives in DeNelle.Core) and SetHudVisible(bool)
// is reflected BY NAME (it is a HUD extra, not on the IVillageHud interface).
//
// Pure helper: a single static SetVisible(bool) the Arena controllers call on
// Enter(false) / Exit(true). No-op + warns once if the method isn't found, so a
// missing HUD never throws into the setup flow.
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village.Arena
{
    /// <summary>Hides/shows the full gameplay HUD during Arena setup screens. By name.</summary>
    internal static class ArenaHudBridge
    {
        private static MethodInfo s_setHudVisible;
        private static object s_boundHud;

        /// <summary>
        /// Resolve the HUD via CoreServices.Hud and reflect SetHudVisible(bool) by name
        /// (re-resolving if the HUD instance changed across a scene reload), then invoke
        /// it. No-op + warns once if the method isn't found.
        /// </summary>
        internal static void SetVisible(bool visible)
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
                    Debug.LogWarning("[ArenaHudBridge] HUD.SetHudVisible(bool) not found — " +
                                     "the gameplay HUD won't hide during Arena setup.");
            }

            s_setHudVisible?.Invoke(hud, new object[] { visible });
        }
    }
}
