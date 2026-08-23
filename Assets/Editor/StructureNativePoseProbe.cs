// =============================================================================
// StructureNativePoseProbe - print the NATIVE import pose of structure models.
// -----------------------------------------------------------------------------
// WHY (2026-08-22): the L3 archer tower renders lying down and TWO static theories
// were wrong about why - first "re-run the baker" (the baker itself no longer runs),
// then "bakeAxisConversion is the odd one out" (proven false once a forced reimport
// made the test real). CLAUDE.md section 12 is explicit that static reading LOCATES
// candidates and never CONCLUDES a cause, so this measures instead of arguing.
//
// THE ONE QUANTITY NOBODY HAS MEASURED: the FBX's own native root rotation.
// catalog row tower_ground_archer carries "preservePrefabRotation": true - the ONLY
// row in the catalog that does - and it works by keeping the model's native pose
// (a 270 X) so the tower stands BEFORE VisualFactory.Fit measures it. If that native
// pose is now identity there is nothing to preserve, and the row's whole mechanism
// is inert. That is a measurement, not an opinion.
//
// Prints, per model: native root euler, child count, and the WORLD bounds of an
// instantiated copy, so a lying-down model is visible as depth > height.
//
// ASCII-only. Judge by the MARKER, never the exit code (CLAUDE.md section 8).
//
//   .\run-unity-method.ps1 -Method DeNelle.Editor.StructureNativePoseProbe.Run `
//       -LogName pose.log -ExpectMarker NATIVE_POSE_PROBE_OK
// =============================================================================

using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class StructureNativePoseProbe
    {
        // Sourced from the single relocatable-root authority - the [asset-roots] gate
        // rejects a second literal, and it is right to.
        private const string Root = DeNelle.Core.AssetRoots.StructureContent;

        // The rows the orientation oracle flagged, plus their PASSING siblings. The
        // siblings are the control: without them a reading is a number with nothing
        // to compare it to, which is how both earlier theories went wrong.
        private static readonly string[] Names =
        {
            "Tower_Wooden_Watchtower",       // passes  - control
            "Tower_Wooden_Watchtower_L2",    // passes  - control
            "Tower_Wooden_Watchtower_L3",    // FAILS   - aspect 0.58
            "Ballista_L1",                   // FAILS   - root tilt 90
            "Ballista_L2",                   // FAILS   - root tilt 90
            "Gate_Medieval_Medium",          // WO-1153 - does it over-claim vs the 3.00 m cell?
        };

        [MenuItem("Defenders/Art/Probe Structure Native Pose")]
        public static void Run()
        {
            var sb = new StringBuilder();
            int measured = 0;
            try
            {
                foreach (string name in Names)
                {
                    string path = AssetDatabase.FindAssets(name + " t:Model", new[] { Root })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p) &&
                                             System.IO.Path.GetFileNameWithoutExtension(p) == name);

                    if (string.IsNullOrEmpty(path))
                    {
                        sb.AppendLine("  " + name.PadRight(30) + " NO MODEL ASSET FOUND under " + Root);
                        continue;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null)
                    {
                        sb.AppendLine("  " + name.PadRight(30) + " asset did not load: " + path);
                        continue;
                    }

                    Vector3 nativeEuler = asset.transform.localRotation.eulerAngles;

                    // Instantiate to measure real world bounds - the asset's own bounds
                    // are authoring-space and would not show a lying-down silhouette.
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    string boundsText = "no renderers";
                    int rends = 0;
                    try
                    {
                        var rs = inst.GetComponentsInChildren<Renderer>(true);
                        rends = rs.Length;
                        if (rends > 0)
                        {
                            Bounds b = rs[0].bounds;
                            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                            boundsText = string.Format(
                                "size=({0:0.00} w x {1:0.00} h x {2:0.00} d)  {3}",
                                b.size.x, b.size.y, b.size.z,
                                (b.size.y >= Mathf.Max(b.size.x, b.size.z) ? "UPRIGHT" : "LYING DOWN (h is not the long axis)"));
                        }
                    }
                    finally
                    {
                        if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
                    }

                    sb.AppendLine("  " + name.PadRight(30) +
                                  " nativeEuler=(" + nativeEuler.x.ToString("0.0") + ", " +
                                  nativeEuler.y.ToString("0.0") + ", " + nativeEuler.z.ToString("0.0") + ")" +
                                  "  children=" + asset.transform.childCount +
                                  "  renderers=" + rends + "  " + boundsText);
                    measured++;
                }

                Debug.Log("[NativePose] native import pose of structure models:\n" + sb);

                if (measured == 0)
                {
                    // An empty probe must never read as a successful one.
                    Debug.LogError("NATIVE_POSE_PROBE_FAIL - measured ZERO models. That is a failure, not a pass.");
                    return;
                }
                Debug.Log("NATIVE_POSE_PROBE_OK " + measured + "/" + Names.Length + " model(s) measured");
            }
            catch (Exception ex)
            {
                Debug.LogError("NATIVE_POSE_PROBE_FAIL - " + ex.GetType().Name + ": " + ex.Message + "\n" + sb);
            }
        }
    }
}
