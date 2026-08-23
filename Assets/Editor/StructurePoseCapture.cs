// =============================================================================
// StructurePoseCapture - render structure models to PNG so a human can SEE the pose.
// -----------------------------------------------------------------------------
// WHY (2026-08-22): "for visual/spatial defects the SCREENSHOT is the data" - the
// FlowTrace shows what the code believes, the image shows what the player sees. The
// lying-down tower thread burned three static theories, and the one thing nobody had
// was a picture of the failing asset. There was no capture of the L3 archer tower at
// all; the only tower shot on disk is a build-mode GHOST from 2026-08-05, which is
// the BASE level and cannot show an L3 defect.
//
// ⭐ IT SHOOTS THE PREFAB AND THE MODEL SEPARATELY, ON PURPOSE. Measurement proved
// the prefab WRAPPER is the orientation authority, not the FBX: the wrapper holds a
// nested PrefabInstance whose m_Modifications carry transform overrides, so a model
// can be upright while the prefab that ships is on its side. Capturing only one of
// them is how that distinction stays invisible. Two images, side by side, make it
// obvious which layer is wrong.
//
// Neutral framing: camera distance is derived from each subject's own bounds, so a
// big model and a small one are directly comparable and nothing is cropped. The
// image is deliberately plain - no HUD, no ground - because the question is only
// "which way up is it".
//
// ASCII-only. Judge by the MARKER, never the exit code (CLAUDE.md section 8).
//
//   .\run-unity-method.ps1 -Method DeNelle.Editor.StructurePoseCapture.Run `
//       -LogName posecap.log -ExpectMarker STRUCTURE_POSE_CAPTURE_OK
// =============================================================================

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class StructurePoseCapture
    {
        private const string Root = DeNelle.Core.AssetRoots.StructureContent;
        private const string OutDir = "docs/ui-evidence/structure-pose-2026-08-22";
        private const int Size = 900;

        private static readonly string[] Names =
        {
            "Tower_Wooden_Watchtower",       // control - oracle PASSES
            "Tower_Wooden_Watchtower_L2",    // control - oracle PASSES
            "Tower_Wooden_Watchtower_L3",    // FAILS - aspect 0.58
            "Ballista_L1",                   // FAILS - native 90, lying down
            "Ballista_L2",                   // FAILS
            // Owner-reported 2026-08-22, reproduced on a fresh HEAD build: the jeweler
            // renders UPSIDE DOWN on default-village load (stone base up, roof into the
            // ground) while the runtime seats it at euler=(90,0,0) with uniform scale.
            // armorer/barracks are the CONTROLS: same Tripo family, same
            // bakeAxisConversion:1, and both look correct - so whatever is wrong is
            // specific to this mesh, not to the flag.
            "jeweler",
            "armorer",
            "barracks",
            // WO-1153: gate_stone claims its MEASURED mesh (the carve-out is Wall-only).
            // Its native XZ:Y aspect is what decides whether it over-claims the 3.00 m cell.
            "Gate_Medieval_Medium",
        };

        [MenuItem("Defenders/Art/Capture Structure Poses")]
        public static void Run()
        {
            int shot = 0;
            try
            {
                Directory.CreateDirectory(OutDir);
                foreach (string name in Names)
                {
                    shot += Capture(name, ".prefab", "prefab") ? 1 : 0;
                    shot += Capture(name, ".fbx", "model") ? 1 : 0;
                }

                if (shot == 0)
                {
                    // A capture run that produced NO images must not read as success.
                    Debug.LogError("STRUCTURE_POSE_CAPTURE_FAIL - zero images written. That is a failure, not a pass.");
                    return;
                }
                AssetDatabase.Refresh();
                Debug.Log("STRUCTURE_POSE_CAPTURE_OK " + shot + " image(s) -> " + OutDir);
            }
            catch (Exception ex)
            {
                Debug.LogError("STRUCTURE_POSE_CAPTURE_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool Capture(string name, string ext, string tag)
        {
            string path = Root + "/" + name + ext;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return false;   // not every model has a wrapper prefab

            GameObject inst = null;
            Camera cam = null;
            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                if (inst == null) return false;
                inst.transform.position = Vector3.zero;

                var rs = inst.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0)
                {
                    Debug.LogWarning("[PoseCap] " + name + ext + " has NO renderers - nothing to show. " +
                                     "That is itself the finding for a wrapper prefab.");
                    return false;
                }

                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

                // Frame from the subject's own size so every image is comparable.
                float radius = Mathf.Max(b.size.magnitude * 0.5f, 0.001f);
                var camGo = new GameObject("PoseCapCam");
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cam.orthographic = false;
                cam.fieldOfView = 35f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 40f + 100f;

                Vector3 dir = new Vector3(0.75f, 0.42f, -1f).normalized;   // 3/4 view, slightly above
                cam.transform.position = b.center + dir * (radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f);
                cam.transform.LookAt(b.center);

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                string outPath = OutDir + "/" + name + "__" + tag + ".png";
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                bool upright = b.size.y >= Mathf.Max(b.size.x, b.size.z);
                Debug.Log("[PoseCap] " + outPath + "  size=(" + b.size.x.ToString("0.00") + " x " +
                          b.size.y.ToString("0.00") + " x " + b.size.z.ToString("0.00") + ")  " +
                          (upright ? "UPRIGHT" : "LYING DOWN"));
                return true;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cam != null) { cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(cam.gameObject); }
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
            }
        }
    }
}
