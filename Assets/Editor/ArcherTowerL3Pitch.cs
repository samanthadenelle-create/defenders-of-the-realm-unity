// =============================================================================
// ArcherTowerL3Pitch - apply the owner's X+90 to the L3 archer tower prefab.
// -----------------------------------------------------------------------------
// Owner, 2026-08-22: "X+90 rotation on the L3 ranger tower."
//
// WHY THE PREFAB AND NOT THE CATALOG - both proven, not assumed:
//   * MEASURED (StructurePoseCapture, 2026-08-22):
//         Tower_Wooden_Watchtower_L3__model.png   0.59 x 1.00 x 0.58  UPRIGHT
//         Tower_Wooden_Watchtower_L3__prefab.png  0.59 x 0.58 x 1.00  LYING DOWN
//     Same asset, two layers, opposite results - so the WRAPPER PREFAB is the
//     authority and the FBX is already fine. Editing the FBX/meta cannot move it,
//     which is why three earlier asset-layer attempts changed nothing.
//   * The catalog cannot reach it either: structures-catalog's own note records that
//     ReskinForLevel "does not apply entry.orientation - tier models rely on their
//     prefab-native orientation", so a catalog euler only ever reaches the BASE
//     visual. L2/L3 would stay down.
//
// So the correction belongs on the prefab's renderer-bearing child, which is the
// same place WoodenWatchtowerBuilder puts it ("bakes the -90 onto the MODEL CHILD
// and leaves the root at identity, precisely because Skin stomps the root").
// That builder can no longer run - it fails on L1 with "no renderer-bearing child" -
// so this applies the one correction it would have made, and nothing else.
//
// Prints bounds BEFORE and AFTER with an explicit verdict. Idempotent: if the prefab
// already measures upright it changes nothing and says so.
//
// ASCII-only. Judge by the MARKER, never the exit code.
//   Menu:      Defenders > Art > Fix L3 Archer Tower Pitch (X+90)
//   Batchmode: -executeMethod DeNelle.Editor.ArcherTowerL3Pitch.Run
//              -ExpectMarker ARCHER_L3_PITCH_OK
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ArcherTowerL3Pitch
    {
        private const string PrefabPath =
            DeNelle.Core.AssetRoots.StructureContent + "/Tower_Wooden_Watchtower_L3.prefab";

        private const float PitchX = 90f;   // owner-specified

        [MenuItem("Defenders/Art/Fix L3 Archer Tower Pitch (X+90)")]
        public static void Run()
        {
            try
            {
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                if (root == null)
                {
                    Debug.LogError("ARCHER_L3_PITCH_FAIL - could not load " + PrefabPath);
                    return;
                }

                try
                {
                    Transform child = FindRendererChild(root.transform);
                    if (child == null)
                    {
                        // Say WHICH shape it is - "no child" and "no renderers" are different bugs.
                        Debug.LogError("ARCHER_L3_PITCH_FAIL - no renderer-bearing child under the prefab " +
                                       "root (childCount=" + root.transform.childCount + "). This is the same " +
                                       "shape that stops WoodenWatchtowerBuilder from running.");
                        return;
                    }

                    string before = Describe(root);
                    Vector3 e = child.localEulerAngles;

                    child.localRotation = Quaternion.Euler(
                        PitchX,
                        Mathf.DeltaAngle(0f, e.y),
                        Mathf.DeltaAngle(0f, e.z));

                    string after = Describe(root);

                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Debug.Log($"[ArcherL3] child '{child.name}' localEuler " +
                              $"({Mathf.DeltaAngle(0f, e.x):0.#},{Mathf.DeltaAngle(0f, e.y):0.#},{Mathf.DeltaAngle(0f, e.z):0.#})" +
                              $" -> ({PitchX:0.#},{Mathf.DeltaAngle(0f, e.y):0.#},{Mathf.DeltaAngle(0f, e.z):0.#})");
                    Debug.Log($"[ArcherL3] BEFORE {before}");
                    Debug.Log($"[ArcherL3] AFTER  {after}");
                    Debug.Log("ARCHER_L3_PITCH_OK " + PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("ARCHER_L3_PITCH_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static Transform FindRendererChild(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.GetComponentInChildren<Renderer>(true) != null) return c;
            }
            return null;
        }

        /// <summary>Bounds plus an explicit verdict - a number alone has repeatedly been misread here.</summary>
        private static string Describe(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return "no renderers";
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            bool tall = b.size.y >= Mathf.Max(b.size.x, b.size.z) * 0.95f;
            return $"bounds {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}  => " +
                   (tall ? "UPRIGHT (height dominant)" : "LYING DOWN (height is not the long axis)");
        }
    }
}
