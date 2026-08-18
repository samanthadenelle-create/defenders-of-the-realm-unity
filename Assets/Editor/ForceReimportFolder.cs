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

using System.Collections.Generic;
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
            // ⛔ THE MIGRATED ROOTS MUST BE IN THIS LIST, NOT JUST Resources.
            // The runner cannot pass extra args, and the first version defaulted to
            // Assets/Resources alone — so after enemy art moved to Assets/EnemyContent, running
            // this reported a healthy 1996-asset reimport while never touching the tree that had
            // actually just moved. A repair tool that silently repairs the wrong folder is worse
            // than none: it produces a green line and leaves the fault in place.
            var folders = new List<string>
            {
                "Assets/Resources",
                DeNelle.Core.AssetRoots.EnemyContent,
                DeNelle.Core.AssetRoots.StructureContent,
            };

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-folder") { folders = new List<string> { args[i + 1] }; break; }

            int grandTotal = 0;
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.Log($"[Reimport] '{folder}' absent — skipped (not an error; it may not exist yet).");
                    continue;
                }

                Debug.Log($"[Reimport] force re-importing '{folder}' ...");
                AssetDatabase.ImportAsset(folder,
                    ImportAssetOptions.ImportRecursive |
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);

                // Report what the database can SEE afterwards — the count is the evidence the index
                // recovered, which is the whole question. A silent "done" would prove nothing.
                int prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Length;
                int models  = AssetDatabase.FindAssets("t:Model",  new[] { folder }).Length;
                int all     = AssetDatabase.FindAssets("", new[] { folder }).Length;
                grandTotal += all;
                Debug.Log($"[Reimport] '{folder}' now indexes {all} asset(s): {prefabs} prefab(s), {models} model(s).");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log($"FORCE_REIMPORT_OK {grandTotal} assets across {folders.Count} root(s)");
        }
    }
}
