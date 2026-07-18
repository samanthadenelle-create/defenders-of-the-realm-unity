// =============================================================================
// SiblingPanelSettings — presentation helper for code-built UIDocument panels.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// MVVM Silo C: a freshly created UIDocument has no PanelSettings and renders
// invisible, so BuildStructureInfoPanel adopts a sibling's. That sibling scan is
// a PURE PRESENTATION concern (no game state) — but it uses FindObjectsByType,
// which the [ui-mvvm] oracle bans INSIDE a View. Extracting it here (a plain
// helper, not a *Panel / IPanelView candidate) keeps the View free of the banned
// symbol while preserving the exact adoption behaviour.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>Adopts a sibling UIDocument's PanelSettings onto a code-built document so it renders.</summary>
    internal static class SiblingPanelSettings
    {
        /// <summary>Copy the best sibling's PanelSettings onto <paramref name="document"/> and sort it
        /// <paramref name="sortAboveDelta"/> above that sibling (prefers a "Hud"-named source). No-op +
        /// warning when no sibling PanelSettings exists.</summary>
        public static void AdoptInto(UIDocument document, int sortAboveDelta)
        {
            if (document == null) return;
            UIDocument hud = null, any = null;
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (doc == null || doc == document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                document.panelSettings = src.panelSettings;
                document.sortingOrder = src.sortingOrder + sortAboveDelta;   // above HUD + palette
            }
            else
            {
                Debug.LogWarning("[BuildStructureInfoPanel] No sibling PanelSettings found — preview will not render.");
            }
        }
    }
}
