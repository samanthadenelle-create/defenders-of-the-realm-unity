// =============================================================================
// SupercyanResourceWire — makes the Supercyan "Character Pack: Fantasy | RPG"
// bodies loadable by the troop factory (WO-453). VisualFactory.Skin loads
// "Resources/Heroes/<model>", but the Supercyan prefabs live under
// Assets/Supercyan/... — so we create lightweight prefab VARIANTS inside
// Resources/Heroes that inherit the Supercyan mesh + Animator + materials.
//
//   SC_Footman  <- Knight  (melee troop body)
//   SC_Archer   <- Archer  (ranged troop body)
//
// troops.json already points footman/archer at SC_Footman/SC_Archer (yaw 0 —
// Supercyan humanoids face +Z, no -90 Tripo correction). Idempotent: re-running
// overwrites the variants. Animator/controller ride along on the variant, so the
// idle/walk/attack clips play once the NavMeshAgent drives position.
//
// Batchmode: DeNelle.Editor.SupercyanResourceWire.Run
// Menu:      Defenders/Troops/Wire Supercyan Bodies
// =============================================================================
using System.IO;
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class SupercyanResourceWire
    {
        private const string ScBase =
            "Assets/Supercyan/Prefabs/Fantasy/Base/High Quality/";
        private const string DestDir = "Assets/Resources/Heroes";

        // source Supercyan prefab -> destination Resources variant name
        private static readonly (string src, string dest)[] Map =
        {
            ("Knight", "SC_Footman"),
            ("Archer", "SC_Archer"),
        };

        [MenuItem("Defenders/Troops/Wire Supercyan Bodies")]
        public static void Run()
        {
            if (!Directory.Exists(DestDir))
                Directory.CreateDirectory(DestDir);

            int made = 0;
            foreach (var (srcName, destName) in Map)
            {
                string srcPath = ScBase + srcName + ".prefab";
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
                if (src == null)
                {
                    Debug.LogWarning($"[SupercyanResourceWire] source prefab missing: {srcPath} " +
                                     "(Supercyan pack not imported?) — skipped.");
                    continue;
                }

                string destPath = $"{DestDir}/{destName}.prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
                if (instance == null)
                {
                    Debug.LogWarning($"[SupercyanResourceWire] could not instantiate {srcName} — skipped.");
                    continue;
                }
                // Root is a connected instance of the Supercyan prefab → SaveAsPrefabAsset
                // produces a VARIANT (inherits mesh/animator/materials, tiny override file).
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, destPath);
                Object.DestroyImmediate(instance);
                if (variant != null) { made++; Debug.Log($"[SupercyanResourceWire] {srcName} -> {destPath}"); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SupercyanResourceWire] created {made}/{Map.Length} Supercyan troop variant(s).");
        }
    }
}
