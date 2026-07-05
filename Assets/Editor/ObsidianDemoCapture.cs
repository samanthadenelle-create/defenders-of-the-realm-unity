// =============================================================================
// ObsidianDemoCapture — renders the vendor's assembled Obsidian UI demo scene
// (OBSIDIAN_DEMO.unity) to PNGs so the owner can VIEW the HUD/UI as images
// without opening the Unity editor.
//
// The demo is a uGUI scene whose Canvases are ScreenSpaceOverlay (m_RenderMode:0)
// — overlay canvases DO NOT render through any camera, so a camera+RenderTexture
// captures nothing. This capture switches each overlay canvas to
// ScreenSpaceCamera IN MEMORY ONLY (never saved), points it at a dedicated
// orthographic camera + RenderTexture sized to the canvas's native reference
// resolution (1920x1080), renders, and reads back to PNG. It then computes each
// key component's on-screen rect and writes tight crops so each piece can be
// viewed close-up.
//
// Outputs -> C:/EoA/UI_REVIEW/OBSIDIAN_DEMO/
//   obsidian_demo_FULL.png         — the whole assembled HUD
//   obsidian_demo_TargetNameplate.png
//   obsidian_demo_PlayerStatBars.png
//   obsidian_demo_BuffsDebuffs.png
//   obsidian_demo_CastBar.png
//   obsidian_demo_ActionBar.png
//
// MUST BE RUN IN A GRAPHICS UNITY SESSION (windowed, NOT -nographics) — UI needs
// a real graphics device to render. Run:
//   "<Unity>\Unity.exe" -projectPath C:\EoA -batchmode -quit ^
//     -executeMethod DeNelle.Editor.ObsidianDemoCapture.Capture -logFile -
//   (omit -nographics; keep the editor CLOSED so the project isn't locked)
// Or in-editor via the menu: Defenders/UI/Capture Obsidian Demo
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Editor
{
    /// <summary>Renders the assembled Obsidian UI demo scene to review PNGs.</summary>
    public static class ObsidianDemoCapture
    {
        private const string ScenePath =
            "Assets/Blink/Art/UI/_DEMO_UIPacks/OBSIDIAN_DEMO.unity";
        private const string OutDir = "C:/EoA/UI_REVIEW/OBSIDIAN_DEMO/";

        // Supersample factor over the canvas's native reference resolution, for
        // crisper full + crop images.
        private const int Supersample = 2;

        // Key components the owner named. Each label is matched against the demo's
        // object names (first ACTIVE RectTransform whose name equals / contains a
        // candidate wins). Order candidates most-specific first.
        private static readonly (string label, string[] candidates)[] CropTargets =
        {
            ("TargetNameplate", new[] { "TargetNameplate", "TargetName", "RareTarget", "BossTarget", "TargetIcon" }),
            ("PlayerStatBars",  new[] { "StatBars", "Bars" }),
            ("PartyNameplate",  new[] { "PartyNameplates", "PartyNameplate" }),
            ("BuffsDebuffs",    new[] { "Buffs", "Debuffs", "BuffBar", "Bonus_Icons" }),
            ("CastBar",         new[] { "CastBars", "CastBar1", "CastBar" }),
            ("ActionBar",       new[] { "MainActionBar", "HUDCore_Diablo", "HUD_DIABLO", "HUDCore", "Buttons1", "Buttons" }),
        };

        [MenuItem("Defenders/UI/Capture Obsidian Demo")]
        public static void Capture()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"OBSIDIAN_CAPTURE_FAIL missing scene: {ScenePath}");
                return;
            }

            // 1. Open the demo (in-memory tweaks only — we NEVER save this scene).
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(OutDir);

            // 2. Gather canvases. If none, UI can't render — bail with a clear note.
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (canvases == null || canvases.Length == 0)
            {
                Debug.LogError(
                    "OBSIDIAN_CAPTURE_FAIL no active Canvas found — nothing to render. " +
                    "If you ran with -nographics, re-run in a GRAPHICS session.");
                return;
            }

            // 3. Native size = the primary canvas's CanvasScaler reference resolution.
            int nativeW = 1920, nativeH = 1080;
            foreach (var c in canvases)
            {
                var scaler = c.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.referenceResolution.x >= 100f &&
                    scaler.referenceResolution.y >= 100f)
                {
                    nativeW = Mathf.RoundToInt(scaler.referenceResolution.x);
                    nativeH = Mathf.RoundToInt(scaler.referenceResolution.y);
                    break;
                }
            }
            int w = nativeW * Supersample;
            int h = nativeH * Supersample;

            // 4. Dedicated ortho camera the canvases render into.
            var camGo = new GameObject("__ObsidianCaptureCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0f, 0f, -100f);
            camGo.transform.rotation = Quaternion.identity;
            cam.orthographic = true;
            cam.orthographicSize = 5f;      // arbitrary — ScreenSpaceCamera refits the canvas
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f); // neutral dark backdrop
            cam.cullingMask = ~0;

            // URP needs the additional-camera-data component.
            var urpDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpDataType != null && camGo.GetComponent(urpDataType) == null)
                camGo.AddComponent(urpDataType);

            // 5. Convert overlay canvases -> ScreenSpaceCamera pointed at our cam
            //    (in-memory only). Remember originals to restore after.
            var restore = new List<(Canvas c, RenderMode mode, Camera worldCam, float plane)>();
            int converted = 0;
            foreach (var c in canvases)
            {
                restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 10f;
                    converted++;
                }
                else if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)
                {
                    c.worldCamera = cam;
                    c.planeDistance = 10f;
                }
            }
            Debug.Log($"[ObsidianDemoCapture] canvases={canvases.Length} converted_to_camera={converted} native={nativeW}x{nativeH} render={w}x{h}");

            Canvas.ForceUpdateCanvases();

            // 6. Render to a RenderTexture and read back the full frame.
            var rtex = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rtex.Create();
            var prevActive = RenderTexture.active;
            Texture2D full = null;
            try
            {
                cam.targetTexture = rtex;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                cam.Render(); // second pass — URP can need a warm-up render in batchmode
                RenderTexture.active = rtex;
                full = new Texture2D(w, h, TextureFormat.RGB24, false);
                full.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                full.Apply();
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
            }

            // 7. Write the full-scene PNG.
            byte[] fullPng = full != null ? full.EncodeToPNG() : null;
            if (fullPng != null && fullPng.Length > 0)
                WritePng(OutDir + "obsidian_demo_FULL.png", fullPng);
            else
                Debug.LogError("OBSIDIAN_CAPTURE_FAIL full-scene EncodeToPNG produced no data (black/empty render?)");

            // Heuristic black-render warning: sample the centre pixel row.
            if (full != null && IsLikelyBlank(full))
                Debug.LogWarning(
                    "OBSIDIAN_CAPTURE_WARN full render looks near-empty — if it's black, the UI did " +
                    "NOT render. Re-run in a WINDOWED graphics session (see file header), NOT -nographics.");

            // 8. Per-component tight crops.
            foreach (var (label, candidates) in CropTargets)
            {
                var target = FindByName(candidates);
                if (target == null)
                {
                    Debug.LogWarning($"OBSIDIAN_CAPTURE_WARN crop '{label}' not found (tried: {string.Join(", ", candidates)}) — skipped");
                    continue;
                }
                CropAndWrite(full, cam, target, w, h, OutDir + $"obsidian_demo_{label}.png", label);
            }

            // 9. Restore canvases (belt-and-braces — scene is never saved anyway).
            foreach (var r in restore)
            {
                if (r.c == null) continue;
                r.c.renderMode = r.mode;
                r.c.worldCamera = r.worldCam;
                r.c.planeDistance = r.plane;
            }

            // 10. Cleanup.
            if (full != null) UnityEngine.Object.DestroyImmediate(full);
            rtex.Release();
            UnityEngine.Object.DestroyImmediate(rtex);
            UnityEngine.Object.DestroyImmediate(camGo);

            Debug.Log($"[ObsidianDemoCapture] DONE -> {OutDir}");
        }

        /// <summary>First ACTIVE RectTransform whose name equals or contains a candidate.</summary>
        private static RectTransform FindByName(string[] candidates)
        {
            var all = UnityEngine.Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            // Exact match first (across all candidates), then contains.
            foreach (var cand in candidates)
                foreach (var rt in all)
                    if (rt != null && rt.gameObject.activeInHierarchy &&
                        string.Equals(rt.name, cand, StringComparison.OrdinalIgnoreCase))
                        return rt;
            foreach (var cand in candidates)
                foreach (var rt in all)
                    if (rt != null && rt.gameObject.activeInHierarchy &&
                        rt.name.IndexOf(cand, StringComparison.OrdinalIgnoreCase) >= 0)
                        return rt;
            return null;
        }

        /// <summary>Crops the target's on-screen rect out of the full frame and writes it.</summary>
        private static void CropAndWrite(Texture2D full, Camera cam, RectTransform target,
                                         int w, int h, string path, string label)
        {
            if (full == null) return;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // world space: BL, TL, TR, BR

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var world in corners)
            {
                // WorldToScreenPoint with a targetTexture cam is in RT pixel space,
                // bottom-left origin — matching ReadPixels / GetPixels.
                Vector3 sp = cam.WorldToScreenPoint(world);
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }

            // Padding around the component so its chrome/frame isn't clipped.
            int pad = Mathf.RoundToInt(12f * Supersample);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX) - pad, 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY) - pad, 0, h - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX) + pad, 1, w);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY) + pad, 1, h);
            int cw = x1 - x0, ch = y1 - y0;

            if (cw <= 1 || ch <= 1)
            {
                Debug.LogWarning($"OBSIDIAN_CAPTURE_WARN crop '{label}' has zero on-screen size (offscreen/collapsed) — skipped");
                return;
            }

            var crop = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, y0, cw, ch));
            crop.Apply();
            byte[] png = crop.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(crop);

            if (png != null && png.Length > 0)
                WritePng(path, png, $"[{target.name}] rect={cw}x{ch}");
            else
                Debug.LogWarning($"OBSIDIAN_CAPTURE_WARN crop '{label}' EncodeToPNG produced no data");
        }

        /// <summary>Rough blank check — samples a grid; true if nearly all pixels equal the clear colour.</summary>
        private static bool IsLikelyBlank(Texture2D tex)
        {
            int hits = 0, samples = 0;
            for (int x = 0; x < tex.width; x += Mathf.Max(1, tex.width / 32))
                for (int y = 0; y < tex.height; y += Mathf.Max(1, tex.height / 32))
                {
                    samples++;
                    Color c = tex.GetPixel(x, y);
                    // "blank" = very dark (near the 0.10 backdrop) everywhere.
                    if (c.r < 0.16f && c.g < 0.16f && c.b < 0.20f) hits++;
                }
            return samples > 0 && hits >= samples - 2;
        }

        private static void WritePng(string path, byte[] data, string extra = null)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, data);
                Debug.Log($"OBSIDIAN_CAPTURE_OK {path} ({data.Length} bytes){(extra != null ? " " + extra : "")}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"OBSIDIAN_CAPTURE_WARN could not write {path}: {e.Message}");
            }
        }
    }
}
