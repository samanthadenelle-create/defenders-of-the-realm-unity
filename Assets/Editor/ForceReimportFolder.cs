// =============================================================================
// ForceReimportFolder — re-import a folder whose AssetDatabase index has drifted
// from what is actually on disk.
// -----------------------------------------------------------------------------
// WHY (2026-08-17): the S1 Addressables migration was reverted using FILESYSTEM
// moves (PowerShell), because AssetDatabase.MoveAsset refuses to move .fbm
// folders. Unity was not running for some of that, so the database never saw the
// changes. The symptom is nasty precisely because it is not visible on disk:
// AssetDatabase.FindAssets("t:Prefab", ...) returned NOTHING for
// Assets/Resources/NPCs/CraftPixPeople while the folder plainly held 14 .prefab
// files WITH their .meta files — which failed TownsfolkBodyPoolRegression with
// "14 pool entries have no prefab", a message that reads like the bodies were
// never built.
//
// ⛔ THE LESSON, WHICH IS BIGGER THAN THIS FILE: moving assets OUTSIDE Unity
// leaves the database stale in ways that surface far from the files you touched.
// The NPC folder was never touched by the revert — it broke anyway. Prefer
// AssetDatabase.MoveAsset; when it refuses (as .fbm forces), force a reimport of
// the WHOLE affected tree afterwards rather than assuming Refresh() caught it.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Forces a full re-import of a folder tree so the AssetDatabase re-indexes it.</summary>
    public static class ForceReimportFolder
    {
        /// <summary>
        /// Batchmode entry: -executeMethod DeNelle.Editor.ForceReimportFolder.Run -folder &lt;path&gt;
        /// Defaults to Assets/Resources when no folder is supplied — broad, but this only ever
        /// costs time, and an under-scoped reimport leaves exactly the stale state it is meant to fix.
        /// </summary>
        public static void Run()
        {
            string folder = "Assets/Resources";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-folder") { folder = args[i + 1]; break; }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError($"[Reimport] '{folder}' is not a valid folder — nothing done.");
                return;
            }

            Debug.Log($"[Reimport] force re-importing '{folder}' ...");
            AssetDatabase.ImportAsset(folder,
                ImportAssetOptions.ImportRecursive |
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // Report what the database can SEE afterwards — the count is the evidence the index
            // recovered, which is the whole question. A silent "done" would prove nothing.
            int prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Length;
            int models  = AssetDatabase.FindAssets("t:Model",  new[] { folder }).Length;
            int all     = AssetDatabase.FindAssets("", new[] { folder }).Length;
            Debug.Log($"[Reimport] '{folder}' now indexes {all} asset(s): {prefabs} prefab(s), {models} model(s).");
            Debug.Log($"FORCE_REIMPORT_OK {all} assets");
        }
    }
}
