// =============================================================================
// DragonPreview — renders a review screenshot of the imported BGE Dragon FBX
// (Assets/Models/Dragon/BGE_Dragon.fbx, converted from the owner's .blend).
//   -executeMethod DeNelle.Editor.DragonPreview.CaptureDragon
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Spawns the imported dragon in a temp scene and captures a PNG.</summary>
    public static class DragonPreview
    {
        private const string DragonFbx = "Assets/Black Dragon/Dragon_Baked_Actions_fbx_7.4_binary.fbx";

        [MenuItem("Defenders/Preview/Capture Dragon")]
        public static void CaptureDragon()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DragonFbx);
            if (model == null)
            {
                Debug.LogError("[DragonPreview] Dragon FBX not found at " + DragonFbx);
                return;
            }
            var dragon = (GameObject)PrefabUtility.InstantiatePrefab(model);
            dragon.transform.position = Vector3.zero;
            dragon.transform.rotation = Quaternion.Euler(0f, 150f, 0f);

            var renderers = dragon.GetComponentsInChildren<Renderer>();
            bool any = false;
            Bounds b = default;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any)
            {
                Debug.LogError("[DragonPreview] Dragon instance has no renderers.");
                return;
            }
            Debug.Log($"[DragonPreview] Dragon renderers: {renderers.Length}, bounds size {b.size}.");

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.transform.rotation = Quaternion.Euler(42f, 35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.58f);

            float size = Mathf.Max(0.5f, b.size.magnitude);
            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.19f);
            cam.transform.position = b.center + new Vector3(size * 0.55f, size * 0.4f, -size * 1.05f);
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = size * 10f + 100f;

            const int w = 1280, h = 960;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "screenshot-dragon.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[DragonPreview] Saved " + path);
        }
    }
}
