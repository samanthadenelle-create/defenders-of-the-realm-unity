// =============================================================================
// ArenaGroundFix -- SURGICAL repair for the owner's HAND-DRESSED arena prefab.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)  Namespace: DeNelle.Editor
//
// PROVEN ROOT CAUSE (do NOT re-investigate): ArenaPrefabBuilder.BuildGround
// assigned a RUNTIME `new Material(grass)` instance to the Ground MeshRenderer.
// PrefabUtility.SaveAsPrefabAsset cannot serialize a runtime-only instance into a
// prefab, so the Ground material serialized as null ({fileID: 0}) -> URP renders
// the missing material as MAGENTA.
//
// This fixer does NOT regenerate the prefab. It LOADS the existing prefab contents
// (which include the owner's hand-added "Design" group: dungeon wall/pillar props +
// defensive spots, plus EdgeProps/Lighting), reassigns ONLY the Ground MeshRenderer's
// sharedMaterial to the SERIALIZABLE Grass_1.mat ASSET reference, then saves -- so
// every other child is preserved byte-for-byte.
//
//   Defenders > Arena > Fix Ground Material
//   (batchmode: DeNelle.Editor.ArenaGroundFix.FixGroundMaterial)
//   Prints marker: ARENA_GROUND_FIXED :: <path>
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ArenaGroundFix
    {
        private const string PrefabPath = "Assets/Resources/Arena/ForestClearingArena.prefab";
        private const string GroundMat  = "Assets/Resources/Arena/Grass_1.mat";
        // Stone material for the owner's hand-added "Design" group (dungeon walls + pillars).
        // Ships under Resources/ so it is GUARANTEED present in a build.
        private const string DesignMat  = "Assets/Resources/Arena/Dwarven_Ground.mat";

        [MenuItem("Defenders/Arena/Fix Ground Material")]
        public static void FixGroundMaterial()
        {
            var matAsset = AssetDatabase.LoadAssetAtPath<Material>(GroundMat);
            if (matAsset == null)
            {
                Debug.LogError("[ArenaGroundFix] Source material asset not found: " + GroundMat + " -- aborting (no fix applied).");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError("[ArenaGroundFix] Could not load prefab contents: " + PrefabPath);
                return;
            }

            try
            {
                // Find the Ground child by name (recursive; tolerant of nesting).
                Transform groundT = FindByName(contents.transform, "Ground");
                if (groundT == null)
                {
                    Debug.LogWarning("[ArenaGroundFix] 'Ground' child not found under " + PrefabPath + " -- nothing to fix.");
                    return;
                }

                var mr = groundT.GetComponent<MeshRenderer>();
                if (mr == null)
                {
                    Debug.LogWarning("[ArenaGroundFix] 'Ground' has no MeshRenderer -- nothing to fix.");
                    return;
                }

                // Assign the SERIALIZABLE ASSET reference directly (NOT a runtime instance).
                // Idempotent: re-running with the asset already assigned is a harmless no-op write.
                mr.sharedMaterial = matAsset;

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[ArenaGroundFix] SaveAsPrefabAsset reported failure for " + PrefabPath);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ArenaGroundFix] DONE. ARENA_GROUND_FIXED :: " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // =====================================================================
        // FixDesignMaterials -- the owner's hand-added "Design" group (4 dungeon
        // walls + 4 stone pillars, polyperfect SM_Dungeon_*) rendered FLAT WHITE
        // in the arena: the FBX import has importMaterials:0 with NO material remap,
        // so the MeshRenderers have an empty material slot -> URP default-Lit white
        // (RCA proven 2026-06-29, agent file:line). This LOADS the prefab, assigns
        // the SERIALIZABLE Dwarven_Ground.mat (Resources => in-build) to every
        // renderer under "Design", and saves -- every other child preserved.
        //
        //   Defenders > Arena > Fix Design (white slabs)
        //   (batchmode: DeNelle.Editor.ArenaGroundFix.FixDesignMaterials)
        //   Prints marker: ARENA_DESIGN_FIXED :: <count> renderer(s)
        // =====================================================================
        [MenuItem("Defenders/Arena/Fix Design (white slabs)")]
        public static void FixDesignMaterials()
        {
            var matAsset = AssetDatabase.LoadAssetAtPath<Material>(DesignMat);
            if (matAsset == null)
            {
                Debug.LogError("[ArenaGroundFix] Design stone material not found: " + DesignMat + " -- aborting (no fix applied).");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError("[ArenaGroundFix] Could not load prefab contents: " + PrefabPath);
                return;
            }

            try
            {
                Transform designT = FindByName(contents.transform, "Design");
                if (designT == null)
                {
                    Debug.LogWarning("[ArenaGroundFix] 'Design' group not found under " + PrefabPath + " -- nothing to fix.");
                    return;
                }

                int fixedCount = 0;
                foreach (var mr in designT.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    // Assign the SERIALIZABLE asset to EVERY material slot (walls/pillars are
                    // single-material, but be safe if a mesh has submeshes). Idempotent.
                    var slots = mr.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] != matAsset) { slots[i] = matAsset; changed = true; }
                    }
                    if (slots.Length == 0) { slots = new[] { matAsset }; changed = true; }
                    if (changed) { mr.sharedMaterials = slots; fixedCount++; }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[ArenaGroundFix] SaveAsPrefabAsset reported failure for " + PrefabPath);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ArenaGroundFix] DONE. ARENA_DESIGN_FIXED :: " + fixedCount + " renderer(s) -> " + DesignMat);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // Depth-first search for the first descendant (or self) named `name`.
        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
