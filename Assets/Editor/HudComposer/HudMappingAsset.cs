// =============================================================================
// DeNelle.Editor.HudComposer.HudMappingAsset — the saved HUD Composer document.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only — includePlatforms:[Editor]). This is a
// DESIGN-TIME mapping document, never referenced at runtime, so it deliberately
// lives in the editor assembly: it must never ship a runtime type into a build.
//
// It records, per HUD screen, the owner's authoring choices:
//   • which CONTEXT the screen renders in (Town / Battle / Overworld / Modal —
//     maps 1:1 to DeNelle.Core.HudModel.HudContext from WO-541);
//   • which DATA SOURCE (a WO-541 HudModel record, or a *VM / *Def / *Loadout /
//     *Catalog / HUD ScriptableObject) the dumb View binds to;
//   • which 9-slice background texture skins it;
//   • where the generated MVVM stubs were written (View / VM / bootstrap paths).
//
// The HUD Composer window reads + writes this asset; "Generate MVVM Stubs" turns
// each row into a dumb View + thin VM + bootstrap that bind CoreServices.HudModel
// and render per HudContextModel.Context (the One-Model architecture, WO-541).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Editor.HudComposer
{
    /// <summary>The four HUD contexts (mirror of DeNelle.Core.HudModel.HudContext, WO-541).
    /// Held here as a local enum so the editor tool compiles BEFORE WO-541 Stage 1 lands;
    /// the generated stubs reference the real Core enum by name.</summary>
    public enum HudComposerContext
    {
        Town,
        Battle,
        Overworld,
        Modal
    }

    /// <summary>One authored screen→model→skin mapping. Plain serializable data.</summary>
    [Serializable]
    public sealed class HudScreenMapping
    {
        [Tooltip("Logical screen name; the generated View is <ScreenName>PanelMvvm.")]
        public string screenName = "NewScreen";

        [Tooltip("Which HUD context this screen renders in (HudContextModel.Context).")]
        public HudComposerContext context = HudComposerContext.Town;

        [Tooltip("Fully-qualified type name of the bound data source (HudModel record or *VM/*Def/*Loadout/*Catalog).")]
        public string modelTypeName = "";

        [Tooltip("GUID of the 9-slice background texture that skins this screen.")]
        public string nineSliceGuid = "";

        [Tooltip("GUID of the OPTIONAL source screen/panel prefab this mapping was seeded from.")]
        public string sourcePrefabGuid = "";

        // -- Generation bookkeeping (filled by 'Generate MVVM Stubs'). ----------
        public string generatedViewPath = "";
        public string generatedVmPath = "";
        public string generatedBootstrapPath = "";

        public HudScreenMapping() { }

        public HudScreenMapping(string name, HudComposerContext ctx)
        {
            screenName = string.IsNullOrEmpty(name) ? "NewScreen" : name;
            context = ctx;
        }
    }

    /// <summary>
    /// The HUD Composer save file: a list of screen mappings. Create / pick / save it
    /// in the HUD Composer window (Tools ▸ DeNelle ▸ HUD Composer).
    /// </summary>
    [CreateAssetMenu(fileName = "HudMapping", menuName = "DeNelle/HUD Composer/Mapping Asset", order = 400)]
    public sealed class HudMappingAsset : ScriptableObject
    {
        [Tooltip("Folder the generated MVVM stubs are written to (default DeNelle.HUD module).")]
        public string outputFolder = "Assets/_Modules/HUD/Generated";

        [Tooltip("One row per HUD screen.")]
        public List<HudScreenMapping> mappings = new List<HudScreenMapping>();

        /// <summary>A clean, code-safe identifier derived from a screen name (PascalCase, no spaces).</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Screen";
            var sb = new System.Text.StringBuilder(raw.Length);
            bool upNext = true;
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(upNext ? char.ToUpperInvariant(c) : c);
                    upNext = false;
                }
                else
                {
                    upNext = true;   // a separator capitalises the next letter
                }
            }
            if (sb.Length == 0) return "Screen";
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');   // identifiers can't start with a digit
            return sb.ToString();
        }
    }
}
