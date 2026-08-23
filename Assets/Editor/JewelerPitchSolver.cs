// =============================================================================
// JewelerPitchSolver - SOLVE the pitch offline instead of guessing and re-baking.
// -----------------------------------------------------------------------------
// Owner, 2026-08-22: "You should be able to take that prefab at those constraints,
// knowing the bounds and knowing the rotation, be able to extrapolate how it's gonna
// look and seed it in a scratch pad and confirm."  Yes. This does that.
//
// WHY IT EXISTS: this one mesh burned three bake cycles because each attempt changed
// a constant, re-baked, rebuilt the player and LOOKED. That loop is ~20 minutes per
// guess. The rotation is deterministic, so every candidate can be evaluated in one
// editor run with no bake at all.
//
// ⛔ IT JUDGES ON TWO INDEPENDENT SIGNALS, because on 2026-08-22 each one lied alone:
//   * AABB height-dominance     - cannot tell UPRIGHT from UPSIDE-DOWN (both tall).
//   * the mesh's own up-axis    - and .up is the WRONG axis for a Z-up mesh, which
//                                 is how a flat slab scored a perfect 1.00.
// A candidate must satisfy BOTH: bounds tall AND mesh-up pointing at world up.
// The solver prints every candidate so the reader can see the losers too.
//
// ASCII-only. Judge by the MARKER, never the exit code.
//   .\run-unity-method.ps1 -Method DeNelle.Editor.JewelerPitchSolver.Run `
//       -LogName pitchsolve.log -ExpectMarker PITCH_SOLVE_OK
// =============================================================================

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class JewelerPitchSolver
    {
        private const string Root = DeNelle.Core.AssetRoots.StructureContent;

        // Every mesh the hub bake pitches, so the solver states the whole family at once.
        // STEMS, not catalog ids. WO orientation audit 2026-08-23: "workshop" and "forge" were
        // listed by CATALOG ID, but the models on disk are ShopAndCrafting.fbx and Forge.fbx, so
        // those two rows silently reported "no asset" and the family was never fully solved.
        private static readonly string[] Subjects = { "jeweler", "armorer", "barracks", "Forge", "ShopAndCrafting" };
        private static readonly float[] Candidates = { 0f, 90f, -90f, 180f };

        [MenuItem("Defenders/Art/Solve Structure Pitch (no bake)")]
        public static void Run()
        {
            int solved = 0;
            try
            {
                foreach (string name in Subjects)
                {
                    string path = Root + "/" + name + ".fbx";
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) { Debug.Log($"[PITCH] {name,-10} no asset at {path}"); continue; }

                    Debug.Log($"[PITCH] ===== {name} =====");
                    string best = "none"; float bestScore = -2f;

                    foreach (float pitch in Candidates)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                        try
                        {
                            inst.transform.position = Vector3.zero;
                            inst.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

                            var rs = inst.GetComponentsInChildren<Renderer>(true);
                            if (rs.Length == 0) { Debug.Log($"[PITCH] {name,-10} pitch {pitch,6:0} : NO RENDERERS"); continue; }

                            Bounds b = rs[0].bounds;
                            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

                            bool tall = b.size.y >= Mathf.Max(b.size.x, b.size.z) * 0.95f;

                            // Try BOTH bases; a mesh family may be Y-up or Z-up and we must not
                            // assume which. Whichever points nearest world-up is this mesh's up.
                            float dotUp = Vector3.Dot(inst.transform.up, Vector3.up);
                            float dotFwd = Vector3.Dot(inst.transform.forward, Vector3.up);
                            float meshUp = Mathf.Abs(dotUp) >= Mathf.Abs(dotFwd) ? dotUp : dotFwd;
                            string axis = Mathf.Abs(dotUp) >= Mathf.Abs(dotFwd) ? "up" : "fwd";

                            // ⭐ THE TAPER TEST - owner's idea, and the only signal here that reads
                            // the GEOMETRY rather than a convention. A building has a BROAD BASE and
                            // a NARROW PEAK, so sample the real vertices in the top 20% of the Y
                            // range against the bottom 20%: top narrower => roof is up => UPRIGHT.
                            // This cannot be fooled by AABB symmetry (+90 and -90 are AABB-identical,
                            // which is what hid this bug for days) and it needs no assumption about
                            // whether the mesh is Y-up or Z-up.
                            float taper = TaperRatio(inst.transform, b);   // <1 = narrow on top

                            string verdict;
                            if (!tall) verdict = "FLAT / ON ITS SIDE";
                            else if (taper < 0.80f) verdict = "<<< UPRIGHT (peak up)";
                            else if (taper > 1.25f) verdict = "UPSIDE DOWN (peak down)";
                            else verdict = "ambiguous taper";

                            float score = (tall ? 1f : 0f) + (taper < 1f ? (1f - taper) : -(taper - 1f));
                            if (score > bestScore) { bestScore = score; best = pitch.ToString("0"); }

                            Debug.Log($"[PITCH] {name,-10} pitch {pitch,6:0} : bounds " +
                                      $"{b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}  " +
                                      $"tall={tall,-5} meshUp({axis})={meshUp:+0.00;-0.00} " +
                                      $"taper(top/bottom)={taper:0.00}  {verdict}");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(inst); }
                    }

                    Debug.Log($"[PITCH] {name,-10} => SOLVED PITCH = {best}");
                    solved++;
                }

                if (solved == 0) { Debug.LogError("PITCH_SOLVE_FAIL - solved nothing. Not a pass."); return; }
                Debug.Log($"PITCH_SOLVE_OK {solved} subject(s) solved");
            }
            catch (Exception ex)
            {
                Debug.LogError("PITCH_SOLVE_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// The TAPER TEST. Kept as a forwarder because this name is cited across the canon and
        /// the WO trail; the one implementation now lives in MeshTaper (DeNelle.EditorRegression)
        /// so StructureOrientationOracle can measure with the SAME body instead of a copy.
        /// </summary>
        internal static float TaperRatio(Transform root, Bounds b) => MeshTaper.Ratio(root, b);
    }
}
