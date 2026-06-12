// =============================================================================
// CastleSideMirror — owner hand-authors ONE castle side; this mirrors it ×4.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only menu tool)
//
// WORKFLOW (owner + agent split):
//   1. Owner hand-builds the SOUTH side — wall · gate · wall · corner tower, made
//      walkable / turret-wide, positioned by eye — as children of ONE parent
//      GameObject named "CastleSide_South". The parent can sit ANYWHERE (clone the
//      existing south wall in place); the mirror always pivots around the Heart at
//      world origin (0,0,0), so there's no need to move it or zero its transform.
//   2. Owner runs  Defenders ▸ Castle ▸ Mirror South Side ×4 .
//   3. This clones CastleSide_South into West / North / East copies rotated
//      90 / 180 / 270° around origin. Re-runnable: it deletes prior copies first,
//      so the owner can tweak the south side and re-mirror as many times as needed.
//   4. Owner hand-nudges any per-side offsets, saves, closes the editor; the agent
//      then bakes NavMesh through the 4 gates + confirms spawn-point alignment.
//
// SAFE: pure additive GameObject cloning under Undo; touches no other file, writes
// no scene by itself (owner saves when happy). Canon: the village is Elarion.
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Mirrors the hand-authored "CastleSide_South" template to all four
    /// castle faces by rotating copies around the origin (the Heart at 0,0,0).</summary>
    public static class CastleSideMirror
    {
        private const string SouthName = "CastleSide_South";

        // The three other faces, by Y rotation of the south template around origin.
        private static readonly (float angle, string label)[] Sides =
        {
            (90f,  "West"),
            (180f, "North"),
            (270f, "East"),
        };

        [MenuItem("Defenders/Castle/Mirror South Side x4 (90deg around origin)")]
        public static void MirrorSouthSide()
        {
            var south = GameObject.Find(SouthName);
            if (south == null)
            {
                EditorUtility.DisplayDialog("Castle Side Mirror",
                    $"No GameObject named '{SouthName}' found.\n\nBuild the south wall + gate + corner tower under a single parent " +
                    $"named '{SouthName}', placed at (0,0,0), then run this again.", "OK");
                return;
            }

            // NOTE: the south group can sit ANYWHERE — it need not be at (0,0,0). The mirror
            // always pivots around the Heart at WORLD ORIGIN (below), so cloning the existing
            // south wall in place and just naming its parent is enough.

            int made = 0;
            foreach (var (angle, label) in Sides)
            {
                string cloneName = "CastleSide_" + label;

                // Re-runnable: clear a prior copy of this face so tweaks re-mirror cleanly.
                var prior = GameObject.Find(cloneName);
                if (prior != null) Undo.DestroyObjectImmediate(prior);

                var clone = Object.Instantiate(south, south.transform.parent);
                clone.name = cloneName;
                // Rigidly swing the WHOLE side around the Heart at WORLD ORIGIN — rotate both
                // its position and its orientation by the angle. Works wherever the south group
                // sits (it need NOT be at 0,0,0); the pivot is always the Heart at (0,0,0).
                var rot = Quaternion.Euler(0f, angle, 0f);
                clone.transform.position = rot * south.transform.position;
                clone.transform.rotation = rot * south.transform.rotation;
                Undo.RegisterCreatedObjectUndo(clone, "Mirror Castle Side");
                made++;
            }

            Debug.Log($"[CastleSideMirror] Mirrored '{SouthName}' into {made} rotated copies (West 90, North 180, East 270 around origin). " +
                      "Hand-nudge per-side offsets, save, then bake NavMesh through the 4 gates.");
            EditorUtility.DisplayDialog("Castle Side Mirror",
                $"Done — created West / North / East copies of '{SouthName}', rotated 90 / 180 / 270 around origin.\n\n" +
                "Eyeball + nudge any per-side offsets, save the scene, then ping me to bake NavMesh + align the gates.", "OK");
        }
    }
}
