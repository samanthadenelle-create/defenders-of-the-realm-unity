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
        // Source pack root (gitignored — owner re-imports). Trailing slash matters.
        // Prefabs live under per-category folders, e.g. .../Prefabs_M/Medieval_M/<Name>.prefab.
        private const string SrcRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";

        // Destination Resources folder (so visualPrefabPath = "Structures/<Name>" loads).
        private const string DstDir = "Assets/Resources/Structures/";

        // One kit prefab: file stem (no .prefab) + the _M category folder it lives in.
        private readonly struct KitPrefab
        {
            public readonly string Name;
            public readonly string Category; // folder under Prefabs_M (no trailing slash)
            public KitPrefab(string name, string category) { Name = name; Category = category; }
        }

        // The defensive-kit prefabs the catalog references (owner's prefab map, WO).
        // Most live in Medieval_M; a few primitives live in other category folders.
        private static readonly KitPrefab[] KitPrefabs =
        {
            // --- already copied / verified (keep) ---
            new KitPrefab("Tower_Medieval_Wood",       "Medieval_M"),  // tower_ground_archer L1
            new KitPrefab("Tower_Castle_Round",        "Medieval_M"),  // tower_ground_archer L2
            new KitPrefab("Tower_Medieval_Big",        "Medieval_M"),  // tower_wall_wizard
            new KitPrefab("Windmill_Medieval",         "Medieval_M"),  // mill
            new KitPrefab("Wall_Medieval_Wood",        "Medieval_M"),  // wall_wood
            new KitPrefab("Wall_Medieval_Stone",       "Medieval_M"),  // wall_stone
            new KitPrefab("Gate_Medieval_Medium",      "Medieval_M"),  // gate_stone

            // --- new (WO prefab-match table) ---
            new KitPrefab("Catapult",                  "Medieval_M"),  // tower_catapult
            new KitPrefab("Ballista",                  "Medieval_M"),  // tower_siege_tower
            new KitPrefab("Stables_Medieval",          "Medieval_M"),  // pet-house
            new KitPrefab("House_Medieval_Medium",     "Medieval_M"),  // workshop / forge
            new KitPrefab("House_Medieval_Large",      "Medieval_M"),  // market
            new KitPrefab("House_Medieval_Small",      "Medieval_M"),  // composite mine_crystal / Healer's Cottage
            new KitPrefab("Watermill_Medieval",        "Medieval_M"),  // lumbermill
            new KitPrefab("Well",                      "Medieval_M"),  // mine_crystal
            new KitPrefab("Marketplace_Stand_Simple",  "Medieval_M"),  // composite market
            new KitPrefab("Torche_Wall",               "Fantasy_M"),   // deco_torch
            new KitPrefab("Anvil",                     "Tools_M"),     // composite forge
            new KitPrefab("Altar",                     "Fantasy_M"),   // composite Heart of Elarion
            new KitPrefab("Pillar_Ionic",              "Roman_M"),     // composite Heart of Elarion
        };

        [MenuItem("Defenders/Catalog/Copy Kit Prefabs To Resources")]
        public static void CopyKitToResources()
        {
            if (!Directory.Exists(DstDir))
                Directory.CreateDirectory(DstDir);

            int copied = 0, skipped = 0, missing = 0;
            foreach (var kit in KitPrefabs)
            {
                string name = kit.Name;
                string src = SrcRoot + kit.Category + "/" + name + ".prefab";
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
