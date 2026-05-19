// =============================================================================
// SceneScreenshot — batchmode build + review screenshots, for owner review
// outside the Unity Editor. Builds the Avalon village (interior + exterior)
// and the Healer's Cottage dungeon, and renders review PNGs into docs/.
//   docs/screenshot-village-week3.png            — the walled town
//   docs/screenshot-village-week3-exterior.png   — the wilderness around it
//   docs/screenshot-dungeon-healers-cottage.png  — the D1 dungeon layout
// Run (no -nographics — rendering needs a graphics device):
//   -executeMethod DeNelle.Editor.SceneScreenshot.CaptureAll
// =============================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Builds the game scenes and renders review PNGs.</summary>
    public static class SceneScreenshot
    {
        /// <summary>Builds + captures everything (village + dungeon).</summary>
        [MenuItem("Defenders/Build + Capture Everything")]
        public static void CaptureAll()
        {
            CaptureVillage();
            CaptureDungeon();
        }

        [MenuItem("Defenders/Week 3/Build Village + Capture Screenshots")]
        public static void CaptureVillage()
        {
            // 0. Fix KayKit FBX materials -> URP (white/magenta -> textured).
            try { KayKitMaterials.FixAllMaterials(); }
            catch (Exception e) { Debug.LogError($"[SceneScreenshot] FixAllMaterials threw: {e}"); }

            // 1. Build the walled interior.
            try { VillageSceneBuilder.BuildVillage(); }
            catch (Exception e) { Debug.LogError($"[SceneScreenshot] BuildVillage threw: {e}"); }

            // 2. Build the exterior wilderness (Terrain, biomes, paths, skybox/fog).
            try { ExteriorTerrainBuilder.BuildExterior(); }
            catch (Exception e) { Debug.LogError($"[SceneScreenshot] BuildExterior threw: {e}"); }

            try { EditorSceneManager.SaveOpenScenes(); } catch { /* best-effort */ }

            // 3. Interior view — framed on the walled castle-town.
            Capture(new Vector3(0f, 56f, -62f), Quaternion.Euler(40f, 0f, 0f), 55f,
                    "screenshot-village-week3.png");

            // 4. Exterior view — high + wide, showing the wilderness biomes.
            Capture(new Vector3(0f, 215f, -265f), Quaternion.Euler(38f, 0f, 0f), 60f,
                    "screenshot-village-week3-exterior.png");
        }

        /// <summary>Builds the Healer's Cottage dungeon and captures a review PNG.</summary>
        [MenuItem("Defenders/Dungeons/Build + Capture Healer's Cottage")]
        public static void CaptureDungeon()
        {
            try { DungeonSceneBuilder.BuildHealersCottage(); }
            catch (Exception e) { Debug.LogError($"[SceneScreenshot] BuildHealersCottage threw: {e}"); }

            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Dungeon_HealersCottage.unity", OpenSceneMode.Single);

            // Review aid (in-memory only — NOT saved): the dungeon runs
            // lantern-dark, so hide ceiling/roof pieces so the room layout reads.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var n = t.name.ToLowerInvariant();
                    if (n.Contains("ceiling") || n.Contains("roof"))
                        t.gameObject.SetActive(false);
                }

            var prevFog = RenderSettings.fog;
            var prevLight = RenderSettings.ambientLight;
            var prevMode = RenderSettings.ambientMode;
            var prevIntensity = RenderSettings.ambientIntensity;
            // The dungeon's own linear fog (14-42u) fogs the whole layout out at
            // a review camera distance — disable it just for the screenshot.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.64f, 0.60f);
            RenderSettings.ambientIntensity = 1f;

            // A bright temporary sun — the dungeon's own lighting is lantern-dark
            // by design, so a review shot needs real light to read the layout.
            var sunGo = new GameObject("__DungeonReviewSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(52f, 28f, 0f);

            Capture(new Vector3(0f, 70f, -60f), Quaternion.Euler(52f, 0f, 0f), 62f,
                    "screenshot-dungeon-healers-cottage.png");

            UnityEngine.Object.DestroyImmediate(sunGo);
            RenderSettings.ambientLight = prevLight;
            RenderSettings.ambientMode = prevMode;
            RenderSettings.ambientIntensity = prevIntensity;
            RenderSettings.fog = prevFog;
        }

        /// <summary>Renders one 1920×1080 PNG from a temporary camera.</summary>
        private static void Capture(Vector3 pos, Quaternion rot, float fov, string fileName)
        {
            const int w = 1920, h = 1080;

            var camGo = new GameObject("__ScreenshotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = rot;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // second pass — URP can need a warm-up render in batchmode.

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            var path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "docs", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            Debug.Log($"[SceneScreenshot] Saved {path}");
        }
    }
}
