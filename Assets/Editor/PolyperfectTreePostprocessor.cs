// =============================================================================
// PolyperfectTreePostprocessor — WO-323 (WHITE TREES), Unity-6000 supported path
// -----------------------------------------------------------------------------
// The polyperfect Low Poly Ultimate Pack tree FBX files (SM_Tree*) import with
// NO usable material, so their renderer slots come out white in URP — the exact
// WO-323 symptom. The ORIGINAL fix (PolyperfectUrpFix.RemapTreeFbxToAtlas) bound
// the shared atlas via ModelImporterMaterialLocation.External + AddRemap. That
// path is DEAD in Unity 6000.4 — Unity logs "MaterialLocation.External is
// obsolete. External Material Location is no longer supported" and the atlas does
// NOT bind (an auto-generated embedded 'other' material wins instead).
//
// SUPPORTED U6 PATH (this file):
//   1. OnPreprocessModel — for SM_Tree* models under .../polyperfect, set
//      materialImportMode = ImportViaMaterialDescription so OnAssignMaterialModel
//      is invoked for each material slot.
//   2. OnAssignMaterialModel — return the shared project atlas material
//      M_Atlas_LPUP.mat (already URP/Lit with the pack atlas on _BaseMap). Unity
//      binds that shared material IN-PLACE (embedded, supported) instead of
//      generating a fresh white one.
//
// Trees only. Every other model — including KayKit (AssetImportPostprocessor) —
// is left untouched: both callbacks early-return for non-tree paths, and Unity
// uses the FIRST non-null OnAssignMaterialModel return across all postprocessors,
// so the two postprocessors never collide (KayKit returns null here, trees return
// null there).
//
// polyperfect is gitignored → re-runs on import after the pack is re-imported on
// a fresh clone. The PolyperfectUrpFix menu force-reimports the tree FBXs so this
// runs on demand. Missing pack / missing atlas: warn + return null (never crash,
// §4 missing-pack rule).
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    /// <summary>
    /// Binds the shared <c>M_Atlas_LPUP.mat</c> atlas material to the polyperfect
    /// <c>SM_Tree*</c> FBX files on import, the Unity-6000-supported way (material
    /// description + <see cref="OnAssignMaterialModel"/>), replacing the obsolete
    /// External material-location remap. Only the polyperfect trees are affected.
    /// </summary>
    public sealed class PolyperfectTreePostprocessor : AssetPostprocessor
    {
        // Match: path contains "polyperfect" AND filename starts with "SM_Tree".
        private const string PolyperfectToken = "polyperfect";
        private const string TreeFilePrefix   = "SM_Tree";

        // Project atlas material — already URP/Lit with the pack atlas on _BaseMap.
        // Cached statically so the FindAssets/Load only runs once per domain.
        private const string AtlasMatName = "M_Atlas_LPUP";
        private static Material _cachedAtlas;
        private static bool _atlasLookupDone;

        // =====================================================================
        //  Model import — opt the tree FBXs into material-description import
        // =====================================================================

        /// <summary>
        /// Fires before a model is imported. For polyperfect <c>SM_Tree*</c>
        /// models, switches material import to
        /// <see cref="ModelImporterMaterialImportMode.ImportViaMaterialDescription"/>
        /// so <see cref="OnAssignMaterialModel"/> is called for each slot. Does
        /// NOT touch materialLocation (obsolete in U6). All other models ignored.
        /// </summary>
        private void OnPreprocessModel()
        {
            if (!IsPolyperfectTree(assetPath)) return;
            if (assetImporter is not ModelImporter model) return;

            if (model.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        // =====================================================================
        //  Material assignment — bind the shared atlas in-place
        // =====================================================================

        /// <summary>
        /// Fires once per material a polyperfect <c>SM_Tree*</c> model defines.
        /// Returns the shared <c>M_Atlas_LPUP.mat</c> so Unity binds the existing
        /// atlas material in-place (embedded, supported) instead of generating a
        /// new white one. Returns <c>null</c> for every non-tree model (so KayKit
        /// and all other imports are unaffected) and when the atlas is missing
        /// (lets normal import proceed — §4 missing-pack rule, never crash).
        /// </summary>
        /// <param name="material">The material Unity is about to assign.</param>
        /// <param name="renderer">The renderer the material is assigned to.</param>
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!IsPolyperfectTree(assetPath)) return null; // non-tree → untouched

            var atlas = ResolveAtlas();
            if (atlas == null) return null;                 // missing pack → default
            return atlas;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// True when the asset path contains "polyperfect" (case-insensitive) and
        /// the file name starts with "SM_Tree" — i.e. a polyperfect tree FBX.
        /// </summary>
        private static bool IsPolyperfectTree(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.IndexOf(PolyperfectToken, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            return !string.IsNullOrEmpty(file)
                && file.StartsWith(TreeFilePrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Locates and caches the project atlas material <c>M_Atlas_LPUP.mat</c>.
        /// Returns <c>null</c> (with a warning) if the pack is not imported, so the
        /// caller can fall back to normal import instead of crashing.
        /// </summary>
        private static Material ResolveAtlas()
        {
            if (_cachedAtlas != null) return _cachedAtlas;
            if (_atlasLookupDone) return null; // already searched, not found

            _atlasLookupDone = true;
            string[] guids = AssetDatabase.FindAssets(AtlasMatName + " t:Material");
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(p)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(p) != AtlasMatName) continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat != null)
                {
                    _cachedAtlas = mat;
                    return _cachedAtlas;
                }
            }

            FlowTrace.Warn("PolyperfectTrees",
                "M_Atlas_LPUP.mat not found (is the polyperfect pack imported?) — " +
                "leaving tree materials to default import (WO-323).");
            Debug.LogWarning("[PolyperfectTreePostprocessor] M_Atlas_LPUP.mat not found; " +
                             "tree FBX material binding skipped (pack may not be imported).");
            return null;
        }

        /// <summary>
        /// Drops the cached atlas reference so the next import re-resolves it.
        /// Called by the force-reimport menu after the pack changes.
        /// </summary>
        internal static void InvalidateAtlasCache()
        {
            _cachedAtlas = null;
            _atlasLookupDone = false;
        }
    }
}
