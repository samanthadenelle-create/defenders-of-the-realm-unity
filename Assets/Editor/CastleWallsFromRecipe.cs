// =============================================================================
// CastleWallsFromRecipe — DELETE + COMPLETELY RECREATE the castle wall sides
// from the captured recipe, reproducibly.
//
// THE REAL SOLUTION (owner, 2026-06-12): the castle walls are no longer a fragile
// hand-dialed scene — they're data (castle-south-recipe.json, captured by
// CastleOffsetCapture from the owner's authored CastleSide_South). This builder
// reads that recipe, deletes the four existing CastleSide_* groups, rebuilds the
// SOUTH side from the recipe, and mirrors it to West/North/East (90/180/270
// around world origin — same as CastleSideMirror). So a regen reproduces the
// OWNER's layout instead of reverting it. Re-runnable (idempotent).
//
// SAFE: only touches the four CastleSide_* wall groups (NOT the keep/interior/
// hero/camera). Additive prefab instances under Undo; writes no scene itself
// (owner saves + re-bakes navmesh when happy).
// =============================================================================
using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CastleWallsFromRecipe
    {
        private const string PolyRoot  = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/";
        private const string SouthName = "CastleSide_South";
        private static readonly (float angle, string label)[] Sides =
            { (90f, "West"), (180f, "North"), (270f, "East") };

        [Serializable] private class Piece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [Serializable] private class Recipe { public Piece[] pieces; public float[] parentPos; public float[] parentRot; }

        [MenuItem("Defenders/Castle/Recreate Walls from Recipe (delete + rebuild + mirror)")]
        public static void Recreate()
        {
            var ta = Resources.Load<TextAsset>("Data/castle-south-recipe");
            if (ta == null) { Debug.LogError("[CastleWallsFromRecipe] recipe not found (Resources/Data/castle-south-recipe). Run Capture South Side first."); return; }
            var recipe = JsonUtility.FromJson<Recipe>(ta.text);
            if (recipe == null || recipe.pieces == null || recipe.pieces.Length == 0) { Debug.LogError("[CastleWallsFromRecipe] recipe empty / failed to parse."); return; }

            // Delete the four existing sides for a clean recreate.
            foreach (var n in new[] { SouthName, "CastleSide_West", "CastleSide_North", "CastleSide_East" })
            {
                var ex = GameObject.Find(n);
                if (ex != null) { Undo.DestroyObjectImmediate(ex); Debug.Log("[CastleWallsFromRecipe] removed " + n); }
            }

            // Rebuild the SOUTH side from the recipe.
            var parent = new GameObject(SouthName);
            Undo.RegisterCreatedObjectUndo(parent, "Recreate Castle Walls");
            parent.transform.position    = V(recipe.parentPos);
            parent.transform.eulerAngles = V(recipe.parentRot);

            int placed = 0;
            foreach (var piece in recipe.pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolyRoot + piece.prefab + ".prefab");
                if (prefab == null) { Debug.LogWarning("[CastleWallsFromRecipe] missing prefab: " + piece.prefab); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = piece.name;
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition    = V(piece.pos);
                go.transform.localEulerAngles = V(piece.rot);
                go.transform.localScale       = V(piece.scale, Vector3.one);
                Undo.RegisterCreatedObjectUndo(go, "Recreate Castle Walls");
                placed++;
            }

            // Mirror south -> West/North/East around world origin (the Heart at 0,0,0).
            foreach (var (angle, label) in Sides)
            {
                var clone = UnityEngine.Object.Instantiate(parent, parent.transform.parent);
                clone.name = "CastleSide_" + label;
                var rot = Quaternion.Euler(0f, angle, 0f);
                clone.transform.position = rot * parent.transform.position;
                clone.transform.rotation = rot * parent.transform.rotation;
                Undo.RegisterCreatedObjectUndo(clone, "Recreate Castle Walls");
            }

            Debug.Log("[CastleWallsFromRecipe] recreated south from recipe (" + placed + " pieces) + mirrored x4. " +
                      "Save the scene, then re-bake the castle NavMesh (select the NavMeshSurface > Bake).");
        }

        private static Vector3 V(float[] a, Vector3 fallback = default)
            => (a != null && a.Length == 3) ? new Vector3(a[0], a[1], a[2]) : fallback;
    }
}
