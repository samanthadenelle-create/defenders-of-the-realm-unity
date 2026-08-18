// =============================================================================
// EnemyMaterialRemap — persist enemy FBX material/texture bindings so they stop
// depending on where the file happens to sit.
// -----------------------------------------------------------------------------
// WHY (2026-08-18): moving Resources/Enemies -> Assets/EnemyContent broke the
// Troll_Mage material binding. The rig/colour audit caught it immediately:
//   'troll-mage' -> 'Troll_Mage' renderer 'tripo_mesh_f0d2cf3e': material
//   'tripo_mat_f0d2cf3e_Pbr' is UNCOLORED — no _BaseMap/_MainTex texture and no
//   non-default base colour (would render white/magenta)
//
// ⛔ ROOT CAUSE — AND IT IS A LATENT TRAP, NOT A ONE-OFF. These FBXs import with
// `externalObjects: {}` and `materialSearch: RecursiveUp`, i.e. the embedded
// material finds its texture by SEARCHING UPWARD FROM THE FBX'S FOLDER. That is a
// binding defined by POSITION, so it silently re-resolves — or fails to — every
// time the asset moves. Nothing records the intended texture; the model just
// looks white afterwards.
//
// THE FIX IS NOT "MOVE IT BACK". SearchAndRemapMaterials writes the resolved
// bindings into the .meta's externalObjects map, which is GUID-based and
// therefore position-INDEPENDENT. After this runs, the same FBX can be relocated
// again without the material caring. That is the difference between repairing an
// instance of the bug and removing the class of it.
//
// Same mechanism the structures pipeline already needed: see
// TripoAssetPostprocessor.ExtractArcaneSpire1, whose own comment records that
// Unity 6 dropped External material location, so an un-persisted remap leaves
// externalObjects empty and the model keeps a null-albedo material on every
// reimport.
// =============================================================================

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Persists material bindings for every model under the enemy content root.</summary>
    public static class EnemyMaterialRemap
    {
        private const string Root     = DeNelle.Core.AssetRoots.EnemyContent;
        private const string OkMarker = "ENEMY_MATERIAL_REMAP_OK";

        [MenuItem("Defenders/Art/Persist enemy material bindings")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogError($"[EnemyRemap] '{Root}' not found — nothing done.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Model", new[] { Root });
            int remapped = 0, alreadyBound = 0, noMaterials = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;

                int boundBefore = mi.GetExternalObjectMap().Count;

                // RecursiveUp searches upward from the asset; the textures for these models sit in
                // sibling folders (e.g. EnemyContent/TripoTex), so a plain Local search would miss
                // them. This is the same search the importer already declares — the difference is
                // that the RESULT gets written down instead of being recomputed from position.
                mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                           ModelImporterMaterialSearch.RecursiveUp);
                mi.SaveAndReimport();

                int boundAfter = mi.GetExternalObjectMap().Count;
                if (boundAfter == 0) { noMaterials++; continue; }
                if (boundAfter > boundBefore) { remapped++; Debug.Log($"[EnemyRemap] persisted {boundAfter} binding(s): {System.IO.Path.GetFileName(path)}"); }
                else alreadyBound++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyRemap] {guids.Length} model(s): {remapped} newly persisted, " +
                      $"{alreadyBound} already bound, {noMaterials} with no external material map.");
            Debug.Log($"{OkMarker} {remapped} remapped");
        }
    }
}
