// =============================================================================
// StorefrontOrientationCapture — render each baked hub storefront from an
// IDENTICAL camera so their orientations can be compared side by side.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Batch:
//   -executeMethod DeNelle.Editor.StorefrontOrientationCapture.CaptureAll
// Output: Builds/StorefrontCaps/<HostName>.png  + a measured summary line each.
//
// WHY THIS EXISTS. +90 and -90 about X produce IDENTICAL bounding boxes, so no
// measurement, no gate and no regression can tell an upright storefront from an
// upside-down one. The project has learned this the hard way more than once. The
// ONLY instrument that can decide is a picture — and a picture is only evidence if
// every subject is shot the same way, which is what this does: same distance in
// units-of-model-height, same elevation, same light, same background.
//
// Owner 2026-08-19: "I want you to test it. with data as well as with an image and
// compare it to any of the other images. such as use the weaponsmith for... because
// they use the same structure, just different signs."
//
// It captures ALL FOUR baked storefronts rather than a chosen pair, because the
// useful comparison is "which of these is the odd one out" and that question needs
// the whole set. It also PRINTS the measured transform next to each shot, so the
// image and the number are in the same artifact and cannot drift apart.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class StorefrontOrientationCapture
    {
        private const string HubScenePath = "Assets/Scenes/Main_Castle_Overworld.unity";
        private const string OutDir = "Builds/StorefrontCaps";
        private const int Res = 900;
        /// <summary>Spare layer used to photograph one storefront with nothing in front of it.</summary>
        private const int IsolationLayer = 31;

        private static readonly string[] Hosts =
        {
            "Jeweler_Gems_Storefront",
            "Blacksmith_Weapons_Storefront",
            "Forge_Armor_Storefront",
            "CastleBarracks",
        };

        [MenuItem("Defenders/World/Capture Storefront Orientations (compare)")]
        public static void CaptureAll()
        {
            if (!File.Exists(HubScenePath))
            {
                Debug.LogError($"[StorefrontCap] hub scene missing: {HubScenePath}");
                return;
            }
            if (SceneManager.GetActiveScene().path != HubScenePath)
                EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(OutDir);

            // One camera + one light, reused for every subject. Reusing them is the
            // whole point: a per-subject rig would make the images incomparable.
            var camGo = new GameObject("~StorefrontCapCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);   // flat, so the silhouette reads
            cam.orthographic = false;
            cam.fieldOfView = 35f;

            var lightGo = new GameObject("~StorefrontCapLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightGo.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

            var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var lines = new List<string>();

            foreach (string hostName in Hosts)
            {
                var host = GameObject.Find(hostName);
                if (host == null)
                {
                    Debug.LogWarning($"[StorefrontCap] '{hostName}' not in scene — skipped.");
                    lines.Add($"{hostName,-32} NOT IN SCENE");
                    continue;
                }

                // Frame on the RENDERED bounds, not the transform: an off-pivot model
                // would otherwise sit half out of frame and the shots would not match.
                if (!TryWorldBounds(host, out Bounds b))
                {
                    Debug.LogWarning($"[StorefrontCap] '{hostName}' has no renderer bounds — skipped.");
                    lines.Add($"{hostName,-32} NO RENDERER BOUNDS");
                    continue;
                }

                float h = Mathf.Max(0.01f, b.size.y);
                // Distance in units of MODEL HEIGHT so a taller model is not simply bigger
                // in frame - every subject fills the same fraction of the image.
                Vector3 dir = new Vector3(0.75f, 0.34f, -1f).normalized;
                cam.transform.position = b.center + dir * (h * 3.1f);
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = h * 40f;

                // ISOLATE THE SUBJECT. The first run of this tool produced a flat olive
                // rectangle for the jeweler: it stands against the castle wall, and the
                // camera landed inside it. A shot that photographs the neighbour is not
                // evidence about the subject. Moving the camera per-subject would fix the
                // occlusion and destroy the comparability that is the whole point, so
                // instead the subject is moved to a spare layer and the camera is told to
                // see nothing else. Same rig, every subject, guaranteed unoccluded.
                var saved = new Dictionary<Transform, int>();
                MoveToLayer(host.transform, IsolationLayer, saved);
                cam.cullingMask = 1 << IsolationLayer;

                cam.Render();

                foreach (var kv in saved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                string path = Path.Combine(OutDir, hostName + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                // The MEASURED numbers, beside the image they belong to.
                Transform visual = FirstRenderedChild(host.transform) ?? host.transform;
                Vector3 e = visual.localEulerAngles;
                string euler = $"({Mathf.DeltaAngle(0f, e.x):F0},{Mathf.DeltaAngle(0f, e.y):F0},{Mathf.DeltaAngle(0f, e.z):F0})";
                string line = $"{hostName,-32} visual='{visual.name}' localEuler={euler,-16} " +
                              $"boundsSize=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) -> {path}";
                lines.Add(line);
                Debug.Log("[StorefrontCap] " + line);
            }

            // A/B THE JEWELER. +90 and -90 about X are AABB-identical, so the only way to
            // say which is upright is to photograph both from the same camera. This shoots
            // the alternate pitch into '<name>__ALT.png' and puts it back, changing nothing.
            var jew = GameObject.Find("Jeweler_Gems_Storefront");
            var jewVisual = jew != null ? FirstRenderedChild(jew.transform) : null;
            if (jewVisual != null)
            {
                Vector3 keep = jewVisual.localEulerAngles;
                float flipped = -Mathf.DeltaAngle(0f, keep.x);
                jewVisual.localEulerAngles = new Vector3(flipped, keep.y, keep.z);

                if (TryWorldBounds(jew, out Bounds ab))
                {
                    float ah = Mathf.Max(0.01f, ab.size.y);
                    Vector3 adir = new Vector3(0.75f, 0.34f, -1f).normalized;
                    cam.transform.position = ab.center + adir * (ah * 3.1f);
                    cam.transform.LookAt(ab.center);
                    cam.farClipPlane = ah * 40f;

                    var asaved = new Dictionary<Transform, int>();
                    MoveToLayer(jew.transform, IsolationLayer, asaved);
                    cam.cullingMask = 1 << IsolationLayer;
                    cam.Render();
                    foreach (var kv in asaved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

                    RenderTexture aprev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var atex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
                    atex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
                    atex.Apply();
                    RenderTexture.active = aprev;
                    string apath = Path.Combine(OutDir, "Jeweler_Gems_Storefront__ALT.png");
                    File.WriteAllBytes(apath, atex.EncodeToPNG());
                    Object.DestroyImmediate(atex);
                    lines.Add($"{"Jeweler ALT pitch",-32} localEuler=({flipped:F0},{keep.y:F0},{keep.z:F0}) -> {apath}");
                    Debug.Log($"[StorefrontCap] ALT jeweler pitch {flipped:F0} -> {apath}");
                }

                jewVisual.localEulerAngles = keep;   // scene left exactly as found
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);

            File.WriteAllLines(Path.Combine(OutDir, "_summary.txt"), lines);
            Debug.Log($"[StorefrontCap] STOREFRONT_CAPTURE_OK {lines.Count} subject(s) -> {OutDir}");
        }

        private static void MoveToLayer(Transform t, int layer, Dictionary<Transform, int> saved)
        {
            saved[t] = t.gameObject.layer;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) MoveToLayer(t.GetChild(i), layer, saved);
        }

        private static Transform FirstRenderedChild(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.GetComponentInChildren<MeshRenderer>(true) != null ||
                    c.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return c;
            }
            return null;
        }

        private static bool TryWorldBounds(GameObject go, out Bounds b)
        {
            b = default;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
