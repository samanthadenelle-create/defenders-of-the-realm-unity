// =============================================================================
// SceneScreenshot — batchmode build + review screenshots, for owner review
// outside the Unity Editor. Builds the Elarion village (interior + exterior)
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

        /// <summary>
        /// Opens CastleTest.unity, frames the union of ALL active Renderer bounds
        /// with an angled overview camera, ensures lighting so the render isn't
        /// black, and writes castle_render.png to the Desktop + docs/issues/ —
        /// so the owner can SEE the built castle without opening Unity.
        ///
        /// Batchmode: -executeMethod DeNelle.Editor.SceneScreenshot.CaptureCastleTest
        /// Headless caveat: a standard Camera + camera.Render() renders fine in
        /// batchmode under URP (RenderTexture readback, no display needed); run
        /// WITHOUT -nographics so a graphics device exists.
        /// </summary>
        [MenuItem("Defenders/Sandbox/Screenshot CastleTest")]
        public static void CaptureCastleTest()
        {
            const string scenePath = "Assets/Scenes/CastleTest.unity";
            const string desktopPath = "C:/Users/Kayden-Laptop/Desktop/castle_render.png";
            const string repoPath = "docs/issues/castle_render.png";
            const int w = 1600, h = 900;

            // 1. Open the scene.
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"SCREENSHOT_FAIL missing scene: {scenePath}");
                return;
            }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 2. Union of all active Renderer bounds (the castle).
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude);
            Bounds bounds = default;
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any)
            {
                Debug.LogError($"SCREENSHOT_FAIL no active renderers in {scenePath}");
                return;
            }

            // 3. Ensure lighting so the render isn't black.
            bool hasDir = false;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Exclude))
            {
                if (l != null && l.enabled && l.type == LightType.Directional) { hasDir = true; break; }
            }
            GameObject tempLight = null;
            if (!hasDir)
            {
                tempLight = new GameObject("__CastleScreenshotSun");
                var l = tempLight.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                l.color = Color.white;
                tempLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            if (RenderSettings.ambientLight.maxColorComponent < 0.05f)
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f, 1f);

            // 4. Angled overview camera framing the bounds.
            Vector3 center = bounds.center;
            float dist = Mathf.Max(bounds.extents.magnitude * 2.2f, 5f);
            float pitch = 35f * Mathf.Deg2Rad;
            float yaw = 30f * Mathf.Deg2Rad;
            // Direction FROM center TO camera: up by sin(pitch), back/side by cos(pitch).
            Vector3 dir = new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                Mathf.Sin(pitch),
                -Mathf.Cos(yaw) * Mathf.Cos(pitch)).normalized;
            Vector3 camPos = center + dir * dist;

            var camGo = new GameObject("__CastleScreenshotCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = camPos;
            camGo.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.62f, 0.85f, 1f); // sky-ish
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = dist + bounds.size.magnitude + 100f;
            cam.fieldOfView = 50f;

            // URP: attach UniversalAdditionalCameraData if the type is present.
            var urpDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpDataType != null && camGo.GetComponent(urpDataType) == null)
                camGo.AddComponent(urpDataType);

            // 5. Render to a RenderTexture and read back.
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            byte[] png = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.Render(); // second pass — URP can need a warm-up render in batchmode.
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                png = tex.EncodeToPNG();
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
            }

            // 6. Write to both destinations.
            if (png != null && png.Length > 0)
            {
                TryWriteBytes(desktopPath, png);
                TryWriteBytes(Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", repoPath)), png);
            }
            else
            {
                Debug.LogError("SCREENSHOT_FAIL EncodeToPNG produced no data");
            }

            // 7. Cleanup.
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            if (tempLight != null) UnityEngine.Object.DestroyImmediate(tempLight);

            Debug.Log($"SCREENSHOT_OK {desktopPath} bounds=center{center}/size{bounds.size}");
        }

        /// <summary>
        /// WO-593: renders the LIVE MainCastle_Hall (the raised-castle-on-plinth result) to the
        /// Desktop so the owner can see the overnight bake without opening Unity. Run WITHOUT
        /// -nographics: -executeMethod DeNelle.Editor.SceneScreenshot.CaptureMainCastleRaised
        /// </summary>
        [MenuItem("Defenders/Sandbox/Screenshot MainCastle Raised")]
        public static void CaptureMainCastleRaised()
        {
            const string scenePath = "Assets/Scenes/MainCastle_Hall.unity";
            const string desktopPath = "C:/Users/Kayden-Laptop/Desktop/castle_raised_render.png";
            const string repoPath = "docs/issues/castle_raised_render.png";
            const int w = 1600, h = 900;

            if (!File.Exists(scenePath)) { Debug.LogError($"SCREENSHOT_FAIL missing scene: {scenePath}"); return; }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            Bounds bounds = default; bool any = false;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (!any) { Debug.LogError($"SCREENSHOT_FAIL no active renderers in {scenePath}"); return; }

            bool hasDir = false;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l != null && l.enabled && l.type == LightType.Directional) { hasDir = true; break; }
            GameObject tempLight = null;
            if (!hasDir)
            {
                tempLight = new GameObject("__CastleScreenshotSun");
                var l = tempLight.AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 1.1f; l.color = Color.white;
                tempLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            if (RenderSettings.ambientLight.maxColorComponent < 0.05f)
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f, 1f);

            Vector3 center = bounds.center;
            float dist = Mathf.Max(bounds.extents.magnitude * 2.2f, 5f);
            float pitch = 35f * Mathf.Deg2Rad, yaw = 30f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch)).normalized;
            var camGo = new GameObject("__CastleScreenshotCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = center + dir * dist;
            camGo.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.62f, 0.85f, 1f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = dist + bounds.size.magnitude + 100f; cam.fieldOfView = 50f;

            var urpDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpDataType != null && camGo.GetComponent(urpDataType) == null) camGo.AddComponent(urpDataType);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var prevActive = RenderTexture.active;
            Texture2D tex = null; byte[] png = null;
            try
            {
                cam.targetTexture = rt; cam.Render(); cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                png = tex.EncodeToPNG();
            }
            finally { cam.targetTexture = null; RenderTexture.active = prevActive; }

            if (png != null && png.Length > 0)
            {
                TryWriteBytes(desktopPath, png);
                TryWriteBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", repoPath)), png);
            }
            else Debug.LogError("SCREENSHOT_FAIL EncodeToPNG produced no data");

            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            if (tempLight != null) UnityEngine.Object.DestroyImmediate(tempLight);
            Debug.Log($"SCREENSHOT_OK {desktopPath} bounds=center{center}/size{bounds.size}");
        }

        /// <summary>
        /// Opens Village2.unity and renders a straight TOP-DOWN orthographic plan PNG
        /// (bird's-eye), bounds-fit, for the owner to hand a layout reference to an
        /// external tool. Read-only on the scene (OpenScene + render, never saves).
        /// Run WITHOUT -nographics: -executeMethod DeNelle.Editor.SceneScreenshot.CaptureVillage2TopDown
        /// </summary>
        [MenuItem("Defenders/Sandbox/Screenshot Village2 TopDown")]
        public static void CaptureVillage2TopDown()
        {
            const string scenePath = "Assets/Scenes/Village2.unity";
            const string desktopPath = "C:/Users/Kayden-Laptop/Desktop/village2_topdown.png";
            const string repoPath = "docs/issues/village2_topdown.png";
            const int w = 2048, h = 2048; // square — true plan view

            if (!File.Exists(scenePath)) { Debug.LogError($"SCREENSHOT_FAIL missing scene: {scenePath}"); return; }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Union of active renderer bounds.
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude);
            Bounds bounds = default; bool any = false;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (!any) { Debug.LogError($"SCREENSHOT_FAIL no active renderers in {scenePath}"); return; }

            // Ensure lighting so the plan isn't black.
            bool hasDir = false;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Exclude))
                if (l != null && l.enabled && l.type == LightType.Directional) { hasDir = true; break; }
            GameObject tempLight = null;
            if (!hasDir)
            {
                tempLight = new GameObject("__V2ScreenshotSun");
                var l = tempLight.AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 1.1f; l.color = Color.white;
                tempLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            if (RenderSettings.ambientLight.maxColorComponent < 0.05f)
                RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f, 1f);

            // Top-down orthographic camera, bounds-fit (square RT -> fit the larger of X/Z extent).
            Vector3 center = bounds.center;
            float orthoSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.08f;
            float camHeight = center.y + bounds.extents.y + 50f;
            var camGo = new GameObject("__V2ScreenshotCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(center.x, camHeight, center.z);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
            cam.orthographic = true; cam.orthographicSize = orthoSize;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = camHeight + bounds.size.y + 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            var urpDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpDataType != null && camGo.GetComponent(urpDataType) == null) camGo.AddComponent(urpDataType);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var prevActive = RenderTexture.active; Texture2D tex = null; byte[] png = null;
            try
            {
                cam.targetTexture = rt; cam.Render(); cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                png = tex.EncodeToPNG();
            }
            finally { cam.targetTexture = null; RenderTexture.active = prevActive; }

            if (png != null && png.Length > 0)
            {
                TryWriteBytes(desktopPath, png);
                TryWriteBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", repoPath)), png);
            }
            else Debug.LogError("SCREENSHOT_FAIL EncodeToPNG produced no data");

            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            if (tempLight != null) UnityEngine.Object.DestroyImmediate(tempLight);
            Debug.Log($"SCREENSHOT_OK village2 topdown bounds=center{center}/size{bounds.size} ortho={orthoSize}");
        }

        private static void TryWriteBytes(string path, byte[] data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, data);
                Debug.Log($"[SceneScreenshot] Saved {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SCREENSHOT_WARN could not write {path}: {e.Message}");
            }
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

            // 5. Elarion close-up — the Heart centerpiece (tree + standing-stone
            //    ring + mound), for verifying the centerpiece renders textured.
            Capture(new Vector3(-6f, 10f, -16f), Quaternion.Euler(20f, 0f, 0f), 52f,
                    "screenshot-village-elarion.png");
        }

        /// <summary>
        /// Verification-only: opens the ALREADY-baked Village.unity and renders
        /// review PNGs WITHOUT re-baking — so a re-bake + screenshot cycle is two
        /// fast passes, not a double build. Framed for the WO-26 enlarged ring
        /// (interior ~84 m x 66 m, WallHalfX=42 / WallHalfZ=33): a straight
        /// top-down orthographic plan view (best for judging spacing/navigability)
        /// plus a pulled-back perspective hero view.
        ///   docs/verify-village-topdown.png    — orthographic plan (spacing check)
        ///   docs/verify-village-perspective.png — pulled-back 3/4 view
        /// Run: -executeMethod DeNelle.Editor.SceneScreenshot.CaptureVillageOnly
        /// </summary>
        [MenuItem("Defenders/Week 3/Capture Village (no re-bake)")]
        public static void CaptureVillageOnly()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Village.unity", OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneScreenshot] OpenScene(Village) threw: {e}");
                return;
            }

            // Top-down orthographic plan view, centred on origin. Ortho size 95
            // (half-height) covers Z -95..+95 and (16:9) X -169..+169 -> takes in
            // the whole WO-27 spawn-world "plus shape": the walled ring PLUS the
            // four ~40 m march corridors + 16 m spawn aprons radiating out the
            // cardinal gates (tips at ~+/-90). Best view for judging corridor
            // continuity, building spacing, and clear streets.
            CaptureOrtho(new Vector3(0f, 220f, 0.01f), Quaternion.Euler(90f, 0f, 0f), 95f,
                         "verify-village-topdown.png");

            // Pulled-back 3/4 perspective from the south, high + wide enough to take
            // in the enlarged town and the south march corridor running out the gate.
            Capture(new Vector3(0f, 92f, -120f), Quaternion.Euler(38f, 0f, 0f), 60f,
                    "verify-village-perspective.png");
        }

        /// <summary>
        /// Diagnostic: opens the baked Village and renders close-ups of the Tripo
        /// gameplay buildings (no re-bake) so their orientation + materials can be
        /// inspected. Run: -executeMethod DeNelle.Editor.SceneScreenshot.CaptureBuildingsClose
        /// </summary>
        [MenuItem("Defenders/Week 3/Capture Buildings (no re-bake)")]
        public static void CaptureBuildingsClose()
        {
            try { EditorSceneManager.OpenScene("Assets/Scenes/Village.unity", OpenSceneMode.Single); }
            catch (Exception e) { Debug.LogError($"[SceneScreenshot] OpenScene threw: {e}"); return; }

            // SE cluster — arcane-tower (6,-12.5) + farm (19,-1), seen from the south.
            Capture(new Vector3(12f, 9f, -32f), Quaternion.Euler(12f, 0f, 0f), 58f,
                    "verify-buildings-se.png");
            // Pet House (-17,-10.5) from the south-west.
            Capture(new Vector3(-17f, 7f, -26f), Quaternion.Euler(14f, 0f, 0f), 55f,
                    "verify-buildings-pethouse.png");
            // Workshop (16,12.5) from the south.
            Capture(new Vector3(16f, 8f, 0f), Quaternion.Euler(14f, 0f, 0f), 55f,
                    "verify-buildings-workshop.png");
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

        /// <summary>
        /// Renders one 1920×1080 PNG from a temporary ORTHOGRAPHIC camera —
        /// a true plan view with no perspective distortion, the right tool for
        /// judging building spacing and street widths. <paramref name="orthoSize"/>
        /// is the camera half-height in world units.
        /// </summary>
        private static void CaptureOrtho(Vector3 pos, Quaternion rot, float orthoSize, string fileName)
            => Capture(pos, rot, 0f, fileName, orthoSize);

        /// <summary>Renders one 1920×1080 PNG from a temporary camera.</summary>
        private static void Capture(Vector3 pos, Quaternion rot, float fov, string fileName, float orthoSize = 0f)
        {
            const int w = 1920, h = 1080;

            var camGo = new GameObject("__ScreenshotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = rot;
            if (orthoSize > 0f)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
            }
            else
            {
                cam.fieldOfView = fov;
            }
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
