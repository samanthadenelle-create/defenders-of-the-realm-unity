// =============================================================================
// RoughStoneFanfareCapture (WO-1596) - headless PNG proof for the rough-stone
// fanfare, in BOTH of its versions (first-ever, and a later repeat drop).
// -----------------------------------------------------------------------------
// WHY A DEDICATED ENTRY AND NOT A UICaptureLaunch TARGET: this screen is built by
// RoughStoneFanfarePanel.Build - the arbiter-free half of the View, added for
// exactly this reason - so a frame can be shot without PanelManager deciding
// whether a screenshot exists. UICaptureLaunch's own RenderCanvasToPng is private,
// so the render body below mirrors it (throwaway ortho camera -> RenderTexture ->
// EncodeToPNG) rather than reaching into it.
//
// ⚠ MUST BE RUN IN A GRAPHICS UNITY SESSION (batchmode, but NOT -nographics) - UI
// needs a real graphics device or every frame reads back flat. The blank guard
// below REFUSES to write a flat frame, so a -nographics run produces an honest
// missing file plus an error, never a convincing empty rectangle.
//
// INVOKE (from the repo root - the root is MACHINE-DEPENDENT, CLAUDE.md sec.0):
//   powershell -File .\run-unity-method.ps1 `
//     -Method DeNelle.Editor.RoughStoneFanfareCapture.CaptureAll `
//     -LogName rough-stone-fanfare-capture.log
//
// OUTPUT -> <repoRoot>/Builds/ui-capture/
//   RoughStoneFanfare_first_1080x2400.png
//   RoughStoneFanfare_repeat_1080x2400.png
// MARKER (judge by the marker, never the exit code - CLAUDE.md sec.16):
//   ROUGH_STONE_FANFARE_CAPTURE_OK <n> frame(s)
//   ROUGH_STONE_FANFARE_CAPTURE_FAIL ...
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI;
using DeNelle.Dungeons;

namespace DeNelle.Editor
{
    /// <summary>Renders the WO-1596 rough-stone fanfare to review PNGs, headless.</summary>
    public static class RoughStoneFanfareCapture
    {
        private const int ShotW = 1080;
        private const int ShotH = 2400;   // the Seeker's portrait aspect

        // Repo root resolved at RUNTIME from Unity's own anchor - never hardcoded
        // (C:\EoA on one box, D:\eoa on another; owner ruling 2026-08-09).
        private static string RepoRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/'); }
        }

        private static string OutDir { get { return RepoRoot + "/Builds/ui-capture/"; } }

        [MenuItem("Defenders/UI/Capture Rough Stone Fanfare")]
        public static void CaptureAll()
        {
            int shot = 0;
            var problems = new List<string>();
            try
            {
                Directory.CreateDirectory(OutDir);
                ElarionUiKit.SetSurfaceOverride(ShotW, ShotH);

                if (Shoot(true, "first", problems)) shot++;
                if (Shoot(false, "repeat", problems)) shot++;
            }
            catch (Exception e)
            {
                problems.Add("capture threw: " + e);
            }
            finally
            {
                ElarionUiKit.ClearSurfaceOverride();
            }

            if (shot == 2 && problems.Count == 0)
            {
                Debug.Log("ROUGH_STONE_FANFARE_CAPTURE_OK " + shot + " frame(s) -> " + OutDir);
                return;
            }
            Debug.LogError("ROUGH_STONE_FANFARE_CAPTURE_FAIL " + shot + "/2 frame(s) written; " +
                           problems.Count + " problem(s): " + string.Join(" | ", problems.ToArray()));
        }

        private static bool Shoot(bool firstEver, string tag, List<string> problems)
        {
            GameObject canvas = null;
            try
            {
                // The REAL VM path (materials.json through the shared catalog), so the capture
                // proves the copy the player gets - not a fixture's idea of it. Score 2 of 3 is
                // the value the owner's own device log recorded on her first clear.
                var vm = RoughStoneFanfareVM.For(DungeonExclusiveItems.RoughStoneId, 2, firstEver);
                canvas = RoughStoneFanfarePanel.Build(vm, null);
                if (canvas == null)
                {
                    problems.Add(tag + ": Build returned no canvas");
                    return false;
                }

                string path = OutDir + "RoughStoneFanfare_" + tag + "_" + ShotW + "x" + ShotH + ".png";
                if (!RenderToPng(canvas, path, ShotW, ShotH, out string why))
                {
                    problems.Add(tag + ": " + why);
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                problems.Add(tag + ": threw " + e.GetType().Name + ": " + e.Message);
                return false;
            }
            finally
            {
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        /// <summary>
        /// Mirrors UICaptureLaunch.RenderCanvasToPng (private there): flip the overlay canvas to
        /// camera space, replay the CanvasScaler math by hand (Update does not run in a
        /// synchronous edit-mode call), force a full layout + TMP rebuild, render, read back, and
        /// REFUSE to write a flat frame.
        /// </summary>
        private static bool RenderToPng(GameObject canvasGo, string path, int w, int h, out string why)
        {
            why = "unmeasured";
            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) { why = "no Canvas on the built root"; return false; }

            RenderMode prevMode = canvas.renderMode;
            Camera prevCam = canvas.worldCamera;
            float prevPlane = canvas.planeDistance;
            RenderTexture prevActive = RenderTexture.active;

            GameObject camGo = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                camGo = new GameObject("~RoughStoneCapCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 1000f;
                cam.cullingMask = ~0;

                var urpDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " +
                                               "Unity.RenderPipelines.Universal.Runtime");
                if (urpDataType != null && camGo.GetComponent(urpDataType) == null)
                    camGo.AddComponent(urpDataType);

                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create();
                cam.targetTexture = rt;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                ApplyScreenSpaceScale(canvas, w, h);

                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    var rootRt = canvasGo.GetComponent<RectTransform>();
                    if (rootRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
                    foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true))
                        if (t != null) t.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();
                }

                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) cam.SubmitRenderRequest(req);
                else cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                tex.Apply(false);

                if (IsBlank(tex, out string measure))
                {
                    why = "BLANK RENDER (not written) - " + measure +
                          ". A -nographics session cannot shoot UI; re-run WITH graphics.";
                    return false;
                }

                byte[] png = tex.EncodeToPNG();
                if (png == null || png.Length == 0) { why = "EncodeToPNG produced no bytes"; return false; }

                File.WriteAllBytes(path, png);
                Debug.Log("[RoughStoneCap] saved " + w + "x" + h + " -> " + Path.GetFullPath(path) +
                          " (" + png.Length + " bytes, " + measure + ")");
                why = measure;
                return true;
            }
            catch (Exception e)
            {
                why = "render threw " + e.GetType().Name + ": " + e.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (canvas != null)
                {
                    canvas.renderMode = prevMode;
                    canvas.worldCamera = prevCam;
                    canvas.planeDistance = prevPlane;
                }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            }
        }

        private static void ApplyScreenSpaceScale(Canvas canvas, int w, int h)
        {
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler == null) return;

            Vector2 refRes = scaler.referenceResolution;
            float refW = refRes.x > 1f ? refRes.x : 1080f;
            float refH = refRes.y > 1f ? refRes.y : 1920f;
            float match = Mathf.Clamp01(scaler.matchWidthOrHeight);

            float logW = Mathf.Log(w / refW, 2f);
            float logH = Mathf.Log(h / refH, 2f);
            float sf = Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
            if (sf <= 0f || float.IsNaN(sf) || float.IsInfinity(sf)) sf = 1f;

            canvas.scaleFactor = sf;
            canvas.referencePixelsPerUnit = scaler.referencePixelsPerUnit > 0f
                ? scaler.referencePixelsPerUnit : 100f;
        }

        // THE BLANK GUARD (same shape as UICaptureLaunch.IsBlank): measure the pixels before
        // shipping them, so a flat frame is reported as a FAILURE and never counted as a shot.
        private const int BlankSampleStride = 97;
        private const int BlankMinDistinctBuckets = 6;
        private const float BlankMinInkFraction = 0.01f;

        private static bool IsBlank(Texture2D tex, out string measure)
        {
            measure = "unmeasured";
            if (tex == null) return true;

            Color32[] px;
            try { px = tex.GetPixels32(); }
            catch (Exception e) { measure = "GetPixels32 threw: " + e.Message; return true; }
            if (px == null || px.Length == 0) { measure = "no pixels"; return true; }

            var buckets = new Dictionary<int, int>();
            int sampled = 0;
            for (int i = 0; i < px.Length; i += BlankSampleStride)
            {
                var p = px[i];
                int key = ((p.r >> 4) << 8) | ((p.g >> 4) << 4) | (p.b >> 4);
                buckets.TryGetValue(key, out int n);
                buckets[key] = n + 1;
                sampled++;
            }
            if (sampled == 0) { measure = "no samples"; return true; }

            int dominant = 0;
            foreach (var kv in buckets) if (kv.Value > dominant) dominant = kv.Value;
            float ink = 1f - (dominant / (float)sampled);

            measure = "distinct=" + buckets.Count + " ink=" + ink.ToString("F4");
            return buckets.Count < BlankMinDistinctBuckets || ink < BlankMinInkFraction;
        }
    }
}
