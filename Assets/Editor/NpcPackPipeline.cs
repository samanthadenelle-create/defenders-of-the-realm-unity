// =============================================================================
// NpcPackPipeline (WO-93 / Option 1) - runs the full People-pack source-shrink +
// build in one batchmode pass, so a single Unity launch does everything:
//   1. Trim     - drop the duplicate folder + unused animation FBX  (~171->~35 MB)
//   2. Compress - 4K TGA source -> 2K PNG, GUID-preserved            (~528->~50 MB)
//   3. Build    - Animator Controllers + prefabs (1D-1E) from the lean clip set
// Each stage is wrapped so one failure is logged but doesn't abort the rest
// (all destructive stages back up to <repo>/Backups/ first). Parse the log for
// TRIM_DONE / COMPRESS_DONE / [NpcPackBuild] / PIPELINE_DONE.
//
// Run: Defenders -> NPC Pack - Source Shrink + Build (full pipeline), or headless
//      run-unity-method.ps1 -Method DeNelle.Editor.NpcPackPipeline.RunAll
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class NpcPackPipeline
    {
        [MenuItem("Defenders/NPC Pack - Source Shrink + Build (full pipeline)")]
        public static void RunAll()
        {
            Stage("Trim",     NpcPackTrimmer.Trim);
            Stage("Compress", NpcPackSourceCompressor.CompressSourceTextures);
            Stage("Build",    NpcPackBuild.BuildControllersAndPrefabs);
            Debug.Log("PIPELINE_DONE");
        }

        private static void Stage(string name, Action run)
        {
            try { run(); }
            catch (Exception e) { Debug.LogError($"[NpcPackPipeline] stage '{name}' FAILED: {e}"); }
        }
    }
}
