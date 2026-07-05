// =============================================================================
// ObsidianComponentGalleryBuilder — renders EACH vendor-assembled Obsidian UI
// prefab (as the vendor composed it) to its OWN clean PNG, so the owner can
// study how the pieces fit together WITHOUT opening the Unity editor.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (Editor-only)
//
// The source of truth is the vendor's assembled prefabs in
//   Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/  (+ Buttons_Obsidian/)
// NOT the mirrored Resources copies and NOT a grid of loose sprites. Every prefab
// is instantiated EXACTLY as authored (PrefabUtility.InstantiatePrefab — no
// restyle, no re-parenting of its internals) and rendered at its DESIGNED size /
// composition, one image per prefab.
//
// WHY THE CAMERA DANCE: these prefabs are uGUI with no Canvas of their own, so we
// parent each under a fresh ScreenSpaceCamera Canvas pointed at a dedicated ortho
// camera + RenderTexture (the exact technique proven in ObsidianDemoCapture.cs —
// a ScreenSpaceOverlay canvas renders through NO camera, so a plain camera grabs
// black). A CanvasScaler in ConstantPixelSize mode (scaleFactor = supersample)
// makes the prefab render at its native authored pixels, supersampled for crisp
// edges. Each prefab is isolated in its own empty scene (never saved).
//
// SIZING (per prefab, from the AUTHORED root RectTransform):
//   * FIXED root (anchorMin == anchorMax): render at its native sizeDelta — no
//     aspect distortion (Bar1 800x30, TargetNameplate 259x26, Inventory 585x700,
//     tall stat panels, etc). Root is re-centred in the frame.
//   * STRETCH root (fills its parent screen — HUDCore / MerchantPanel / GameMenu /
//     LoadingScreen / LoginScreen): no intrinsic size, so render full-screen 1920x1080.
//
// Outputs -> C:/EoA/UI_REVIEW/OBSIDIAN_PREFABS/<PrefabName>.png  (+ _INDEX.txt)
//
// MUST RUN IN A GRAPHICS UNITY SESSION (windowed, NOT -nographics) — UI needs a
// real graphics device. Run (editor CLOSED so the project isn't locked):
//   "<Unity>\Unity.exe" -projectPath C:\EoA -batchmode -quit ^
//     -executeMethod DeNelle.Editor.ObsidianComponentGalleryBuilder.Build -logFile -
// Or in-editor: menu  Defenders/UI/Build Obsidian Component Gallery
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Editor
{
    /// <summary>Editor utility: renders each vendor Obsidian prefab to its own review PNG.</summary>
    public static class ObsidianComponentGalleryBuilder
    {
        private const string PrefabDir = "Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian";
        private const string OutDir    = "C:/EoA/UI_REVIEW/OBSIDIAN_PREFABS/";

        // Supersample over the prefab's native pixels (dropped to 1 for very large prefabs
        // so the RenderTexture never gets absurd).
        private const int SupersampleDefault = 2;
        private const int MaxRtDim = 4096;

        // Neutral dark backdrop so the black+gold chrome reads without washing out.
        private static readonly Color Backdrop = new Color(0.10f, 0.10f, 0.12f, 1f);

        // The focus set the owner named — rendered FIRST so those PNGs are guaranteed even if
        // a later prefab throws. Everything else in the folder is still rendered after.
        private static readonly string[] FocusOrder =
        {
            "HUDCore", "HUDCore_Diablo", "TargetNameplate", "PartyNameplate",
            "CastBar1", "CastBar2", "CastBar3",
            "MerchantPanel", "Inventory", "Crafting", "QuestLog", "TalentTree",
            "Characters", "GameMenu", "Loot", "PetPanel",
        };

        [MenuItem("Defenders/UI/Build Obsidian Component Gallery")]
        public static void Build()
        {
            Directory.CreateDirectory(OutDir);

            // 1. Enumerate every prefab under the vendor folder (recursive — includes
            //    Buttons_Obsidian/). Ordered focus-set-first, then the rest alphabetically.
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir });
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path)) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (!byName.ContainsKey(name)) byName[name] = path;
            }

            var ordered = new List<string>();     // asset paths, focus-first
            foreach (var f in FocusOrder)
                if (byName.TryGetValue(f, out var p)) { ordered.Add(p); byName.Remove(f); }
            var rest = new List<string>(byName.Values);
            rest.Sort(StringComparer.OrdinalIgnoreCase);
            ordered.AddRange(rest);

            Debug.Log($"[ObsidianGallery] {ordered.Count} vendor prefabs under {PrefabDir} -> {OutDir}");

            var index = new StringBuilder();
            index.AppendLine("OBSIDIAN VENDOR PREFAB GALLERY  (rendered as assembled)");
            index.AppendLine("source: " + PrefabDir);
            index.AppendLine("generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            index.AppendLine(new string('-', 60));

            int ok = 0, fail = 0;
            foreach (var path in ordered)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                string result;
                try
                {
                    result = RenderPrefab(path, name);
                    ok++;
                }
                catch (Exception e)
                {
                    result = "FAIL " + e.Message;
                    fail++;
                    Debug.LogWarning($"OBSIDIAN_GALLERY_WARN {name}: {e.Message}");
                }
                index.AppendLine(name.PadRight(24) + " : " + result);
            }

            index.AppendLine(new string('-', 60));
            index.AppendLine($"rendered {ok}, failed {fail}, of {ordered.Count}");
            File.WriteAllText(OutDir + "_INDEX.txt", index.ToString());
            Debug.Log($"[ObsidianGallery] DONE rendered={ok} failed={fail} -> {OutDir}");
        }

        /// <summary>Instantiate ONE prefab as authored under an isolated capture canvas, render it to
        /// a PNG at its designed size, return a one-line result string for the index.</summary>
        private static string RenderPrefab(string prefabPath, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "FAIL could not load asset";

            // Fresh empty scene per prefab so nothing leaks between renders (never saved).
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Dedicated ortho capture camera (ScreenSpaceCamera canvas renders into it).
            var camGo = new GameObject("__GalleryCaptureCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0f, 0f, -100f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;          // arbitrary — ScreenSpaceCamera refits the canvas
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            cam.cullingMask = ~0;
            var urpDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpDataType != null && camGo.GetComponent(urpDataType) == null)
                camGo.AddComponent(urpDataType);

            // Read the AUTHORED root anchors/size to decide native-vs-fullscreen sizing.
            var prefabRt = prefab.transform as RectTransform;
            bool fixedSize = false;
            int designW = 1920, designH = 1080;
            if (prefabRt != null)
            {
                bool stretch = !Mathf.Approximately(prefabRt.anchorMin.x, prefabRt.anchorMax.x)
                            || !Mathf.Approximately(prefabRt.anchorMin.y, prefabRt.anchorMax.y);
                var sd = prefabRt.sizeDelta;
                if (!stretch && sd.x >= 8f && sd.y >= 8f)
                {
                    fixedSize = true;
                    designW = Mathf.Clamp(Mathf.RoundToInt(sd.x), 32, 3600);
                    designH = Mathf.Clamp(Mathf.RoundToInt(sd.y), 32, 3600);
                }
            }

            // Supersample, capped so the RT never exceeds MaxRtDim on its long edge.
            int ss = SupersampleDefault;
            while (ss > 1 && (designW * ss > MaxRtDim || designH * ss > MaxRtDim)) ss--;
            int rtW = Mathf.Min(designW * ss, MaxRtDim);
            int rtH = Mathf.Min(designH * ss, MaxRtDim);

            // Capture canvas — ScreenSpaceCamera at our cam, ConstantPixelSize so the prefab
            // renders at its native pixels (scaleFactor = ss => canvas logical size = designW/H).
            var canvasGo = new GameObject("__GalleryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = ss;

            // Instantiate the prefab AS AUTHORED, then place it inside the frame:
            //   fixed  -> centre it (anchor 0.5, keep sizeDelta) so it sits in the middle.
            //   stretch-> fill the frame (anchors 0..1, offsets 0) as it was designed to.
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
            var rt = inst.transform as RectTransform;
            if (rt == null) rt = inst.AddComponent<RectTransform>();
            rt.localScale = Vector3.one;
            if (fixedSize)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }

            Canvas.ForceUpdateCanvases();

            // Render to a RenderTexture and read back.
            var rtex = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rtex.Create();
            var prevActive = RenderTexture.active;
            byte[] png = null;
            string dims = rtW + "x" + rtH;
            try
            {
                cam.targetTexture = rtex;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                cam.Render();   // URP can need a warm-up pass in batchmode
                RenderTexture.active = rtex;
                var tex = new Texture2D(rtW, rtH, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rtW, rtH), 0, 0);
                tex.Apply();
                png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
                rtex.Release();
                UnityEngine.Object.DestroyImmediate(rtex);
            }

            if (png == null || png.Length == 0)
                return "FAIL empty render (ran with -nographics? need a GRAPHICS session)";

            var outPath = OutDir + name + ".png";
            File.WriteAllBytes(outPath, png);
            Debug.Log($"OBSIDIAN_GALLERY_OK {outPath} ({png.Length} bytes) {dims} {(fixedSize ? "native" : "fullscreen")}");
            return (fixedSize ? "native " : "fullscreen ") + dims + "  (" + png.Length + " bytes)";
        }
    }
}
