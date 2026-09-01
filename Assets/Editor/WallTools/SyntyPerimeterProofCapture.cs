// =============================================================================
// SyntyPerimeterProofCapture — WO-1290 visual evidence for the Synty castle ring.
// -----------------------------------------------------------------------------
// Opens the SHIPPED hub, drops a throwaway camera, and renders the rebuilt perimeter
// from several angles. Colour + a greyscale companion for each, because the owner is
// red/green colourblind and the greyscale read is the gate that actually matters
// (memory owner-colorblind-delegate-visual-creative).
//
// WHY THIS EXISTS: compile-green and a marker prove the builder RAN. They cannot prove
// the wall LOOKS right — for a visual/spatial defect the screenshot IS the data
// (memory screenshots-are-primary-evidence-for-visual-defects). Nothing about this
// re-theme ships on a marker alone.
//
// ⚠ NO Shader.Find — it returns NULL in batchmode (CastleHubBuilder.cs:2549). We render
// the scene exactly as authored: its own terrain, its own lighting, its own materials.
// Nothing here creates a material, so nothing here can go magenta.
//
// Batchmode: DeNelle.Editor.SyntyPerimeterProofCapture.Run
// Menu:      Defenders/Art/Capture Synty Perimeter Proof
// Marker:    PERIMETER_PROOF_OK / PERIMETER_PROOF_FAIL
// =============================================================================
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SyntyPerimeterProofCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const string OutputDir = "docs/ui-evidence/wo1290_synty_perimeter";
        private const string HubScene = "Assets/Scenes/Main_Castle_Overworld.unity";

        private struct Shot
        {
            public string Name; public Vector3 Pos; public Vector3 LookAt; public float Fov;
            public Shot(string n, Vector3 p, Vector3 l, float f) { Name = n; Pos = p; LookAt = l; Fov = f; }
        }

        [MenuItem("Defenders/Art/Capture Synty Perimeter Proof")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            Directory.CreateDirectory(OutputDir);

            float lift = CastleHubBuilder.CastleFootprintLiftY;

            // Framed off the MEASURED ring: extent +-39m, wall 5m + 1.38m battlement.
            var shots = new[]
            {
                // The money shot: the south gate on approach, from outside the moat.
                new Shot("01_gate_approach",  new Vector3(0f, lift + 6f, -78f),
                                              new Vector3(0f, lift + 4f, -39f), 42f),
                // A long run of wall, to read the module repeat and the battlement line.
                new Shot("02_wall_run",       new Vector3(-26f, lift + 7f, -62f),
                                              new Vector3(4f,  lift + 3f, -39f), 40f),
                // A corner tower against two wall runs — the join that used to be an ellipse.
                new Shot("03_corner_tower",   new Vector3(-62f, lift + 9f, -62f),
                                              new Vector3(-39f, lift + 4f, -39f), 45f),
                // High three-quarter: the whole silhouette, ring closed on all four sides.
                new Shot("04_overview",       new Vector3(-72f, lift + 52f, -82f),
                                              new Vector3(0f,   lift + 2f, 0f), 46f),
                // From inside the courtyard looking out at the gate + rampart line.
                new Shot("05_courtyard_out",  new Vector3(0f, lift + 4f, -14f),
                                              new Vector3(0f, lift + 4f, -39f), 50f),
            };

            var cameraGo = new GameObject("PerimeterProofCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 900f;

            int written = 0;
            foreach (var shot in shots)
            {
                camera.transform.position = shot.Pos;
                camera.transform.rotation = Quaternion.LookRotation(shot.LookAt - shot.Pos);
                camera.fieldOfView = shot.Fov;
                if (Capture(camera, shot.Name)) written += 2;
            }

            Object.DestroyImmediate(cameraGo);
            AssetDatabase.Refresh();

            if (written == 0) { Debug.LogError("PERIMETER_PROOF_FAIL no images written"); return; }
            Debug.Log($"PERIMETER_PROOF_OK {written} image(s) -> {OutputDir}");
        }

        private static bool Capture(Camera camera, string name)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Texture2D tex = null;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"{OutputDir}/{name}_color.png", tex.EncodeToPNG());

                // Greyscale companion — the colourblind-safety read.
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    float v = px[i].r * 0.2126f + px[i].g * 0.7152f + px[i].b * 0.0722f;
                    px[i] = new Color(v, v, v, px[i].a);
                }
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes($"{OutputDir}/{name}_grey.png", tex.EncodeToPNG());
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PerimeterProof] shot '{name}' failed: {ex.Message}");
                return false;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                if (tex != null) Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
            }
        }
    }
}
