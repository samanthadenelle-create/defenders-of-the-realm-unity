// =============================================================================
// StructureContentReimport - force a reimport of Assets/StructureContent.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (2026-08-22): an IMPORTER SETTING change lands in the .fbx.meta,
// but a batchmode -executeMethod run does NOT reliably reimport off the back of it.
// The symptom is the worst kind: the fix is correct on disk, the run is green-ish,
// and the defect is unchanged - so the fix reads as DISPROVEN when it was merely
// never applied. That happened here while correcting
// Tower_Wooden_Watchtower_L3.fbx.meta's bakeAxisConversion (1 -> 0, matching its
// two siblings); the meta was right and the model still measured lying down,
// because the FBX had not been re-imported.
//
// There was no generic force-reimport entry point in the project - only
// HeroReimport ("Reimport Knight FBX") and ActionClipImporter, both hardcoded to
// their own asset. Any future importer-setting fix hits the same wall, so this is
// a folder-level tool rather than a fourth per-asset one.
//
// ASCII-only (PS 5.1 reads BOM-less files as ANSI).
// Judge by the MARKER, never the exit code (CLAUDE.md section 8).
//
// Batchmode:
//   .\run-unity-method.ps1 -Method DeNelle.Editor.StructureContentReimport.Run `
//       -LogName reimport.log -ExpectMarker STRUCTURE_REIMPORT_OK
// =============================================================================

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class StructureContentReimport
    {
        // Sourced from the single relocatable-root authority, never re-typed here -
        // the [asset-roots] gate rejects a second literal, and it caught this file on
        // its first run. A path spelled twice is a path that drifts.
        private const string Root = DeNelle.Core.AssetRoots.StructureContent;

        [MenuItem("Defenders/Art/Reimport StructureContent (force)")]
        public static void Run()
        {
            try
            {
                // Models only. Reimporting the whole folder would also churn every
                // material and prefab in it for no reason, and a bigger blast radius
                // makes the result harder to attribute.
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { Root });
                if (guids == null || guids.Length == 0)
                {
                    // An empty sweep must NOT look like a successful one.
                    Debug.LogError("STRUCTURE_REIMPORT_FAIL - no models found under " + Root +
                                   ". A reimport that touched NOTHING is a failure, not a pass.");
                    return;
                }

                string[] paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                                      .Where(p => !string.IsNullOrEmpty(p))
                                      .Distinct()
                                      .OrderBy(p => p, StringComparer.Ordinal)
                                      .ToArray();

                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string p in paths)
                        AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    // In a finally so a single bad model cannot leave the database
                    // in a batched-editing state for the rest of the run.
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("STRUCTURE_REIMPORT_OK " + paths.Length + " model(s) force-reimported under " + Root);
            }
            catch (Exception ex)
            {
                Debug.LogError("STRUCTURE_REIMPORT_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
