// =============================================================================
// CatalogPrefabImporter — copies the defensive-kit _M prefabs out of the
// (gitignored) Polyperfect pack into Assets/Resources/Structures/ so the catalog's
// Resources.Load(visualPrefabPath) can actually find them at runtime.
// -----------------------------------------------------------------------------
// The _M prefabs live at
//   Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/<Name>.prefab
// which is NOT under a Resources/ folder, so the runtime catalog loader can't reach
// them. AssetDatabase.CopyAsset duplicates the prefab into Resources/Structures/ and
// REWRITES its dependency GUIDs into the copy, so the mesh/material references stay
// intact (the source pack stays untouched and gitignored).
//
// Idempotent: a prefab already present at the destination is skipped, so re-running
// after a fresh pack re-import is safe and cheap.
//
//   Defenders > Catalog > Copy Kit Prefabs To Resources
//   (batchmode: DeNelle.Editor.CatalogPrefabImporter.CopyKitToResources)
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CatalogPrefabImporter
    {
        // Source pack folder (gitignored — owner re-imports). Trailing slash matters.
        private const string SrcDir =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/";

        // Destination Resources folder (so visualPrefabPath = "Structures/<Name>" loads).
        private const string DstDir = "Assets/Resources/Structures/";

        // The defensive-kit prefabs the catalog references (owner's prefab map, WO).
        // Tower L1/L2 + walls + gate + mill. Names are the prefab file stems (no .prefab).
        private static readonly string[] KitPrefabs =
        {
            "Tower_Medieval_Wood",   // tower_ground_archer L1
            "Tower_Castle_Round",    // tower_ground_archer L2
            "Tower_Medieval_Big",    // tower_wall_wizard   L2
            "Windmill_Medieval",     // mill
            "Wall_Medieval_Wood",    // wall_wood
            "Wall_Medieval_Stone",   // wall_stone
            "Gate_Medieval_Medium",  // gate_stone
        };

        [MenuItem("Defenders/Catalog/Copy Kit Prefabs To Resources")]
        public static void CopyKitToResources()
        {
            if (!Directory.Exists(DstDir))
                Directory.CreateDirectory(DstDir);

            int copied = 0, skipped = 0, missing = 0;
            foreach (var name in KitPrefabs)
            {
                string src = SrcDir + name + ".prefab";
                string dst = DstDir + name + ".prefab";

                // Idempotent — already in Resources, leave it.
                if (File.Exists(dst))
                {
                    skipped++;
                    continue;
                }

                // Source absent (pack not imported on this machine) — warn, don't error
                // (CLAUDE.md §4: pack may not be imported).
                if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null)
                {
                    Debug.LogWarning($"[CatalogPrefabImporter] source prefab missing (pack not imported?): {src}");
                    missing++;
                    continue;
                }

                // CopyAsset preserves mesh/material dependencies (rewrites GUIDs into the copy).
                if (AssetDatabase.CopyAsset(src, dst))
                {
                    copied++;
                }
                else
                {
                    Debug.LogWarning($"[CatalogPrefabImporter] CopyAsset FAILED: {src} -> {dst}");
                    missing++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CatalogPrefabImporter] DONE — copied {copied}, skipped {skipped} already-present, " +
                      $"missing {missing}. Destination: {DstDir}. CATALOG_KIT_COPY_OK");
        }
    }
}
