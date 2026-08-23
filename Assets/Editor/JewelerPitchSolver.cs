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
        private static readonly string[] Subjects = { "jeweler", "armorer", "barracks", "forge", "workshop" };
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

                            string verdict = (tall && meshUp > 0.90f) ? "<<< UPRIGHT" :
                                             (tall && meshUp < -0.90f) ? "UPSIDE DOWN" :
                                             (!tall ? "FLAT / ON ITS SIDE" : "ambiguous");

                            float score = (tall ? 1f : 0f) + meshUp;
                            if (score > bestScore) { bestScore = score; best = pitch.ToString("0"); }

                            Debug.Log($"[PITCH] {name,-10} pitch {pitch,6:0} : bounds " +
                                      $"{b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}  " +
                                      $"tall={tall,-5} meshUp({axis})={meshUp:+0.00;-0.00}  {verdict}");
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
    }
}
