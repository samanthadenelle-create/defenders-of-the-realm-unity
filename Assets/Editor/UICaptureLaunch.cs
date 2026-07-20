// =============================================================================
// UICaptureLaunch -- editor launch hooks for the UICaptureMode UI screenshot harness.
// -----------------------------------------------------------------------------
// Two entry points live here:
//
//   RunCapture()          -- LEGACY Play-mode drive. Sets the UICaptureMode
//                            SessionState flag and enters Play mode; the runtime
//                            MonoBehaviour then boots a scene, opens each router
//                            panel, and ScreenCaptures. This ONLY works with a live
//                            interactive editor / a graphics player -- in
//                            `-batchmode -quit -executeMethod` the method returns
//                            immediately and Unity quits BEFORE Play ever ticks, so
//                            it produces ZERO pngs. Kept for the menu item only.
//
//   RunCaptureHeadless()  -- NEW, RELIABLE headless path (owner directive:
//                            catch visual bugs BEFORE builds ship). A fully
//                            SYNCHRONOUS edit-mode render: it builds the real
//                            code-built uGUI panel (starting with the founding /
//                            Echo unlock card that carries the just-fixed
//                            text/button overlap), switches its canvas to a
//                            camera, renders to a RenderTexture, and writes a PNG --
//                            all inside the one executeMethod call, so `-quit`
//                            takes effect only AFTER the pngs are on disk. No Play
//                            mode, no domain-reload race, no owner playtest.
//
// INVOKE (headless -- the wrapper passes -batchmode -quit and NO -nographics, so a
// real GPU renders real pixels):
//   powershell -File .\run-unity-method.ps1 `
//     -Method DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless -LogName ui-capture.log
//
// OUTPUT: Builds/ui-capture/<PanelName>.png  +  a final `UI_CAPTURE_OK <count>`
// marker line the caller greps to confirm.
// =============================================================================

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using DeNelle.Village;   // EchoUnlockDialogue, EchoRosterCatalog, EchoRosterEntry

namespace DeNelle.Editor
{
    /// <summary>Editor entries for the UI-capture harness (legacy Play-mode drive +
    /// the reliable synchronous edit-mode render).</summary>
    public static class UICaptureLaunch
    {
        private const string OutDir = "Builds/ui-capture/";

        // ---------------------------------------------------------------------
        //  LEGACY -- Play-mode drive (menu item only; not headless-reliable).
        // ---------------------------------------------------------------------
        [MenuItem("Defenders/UI/Capture UI Panels")]
        public static void RunCapture()
        {
            // Same key UICaptureMode reads at boot (kept as a public const there).
            SessionState.SetBool(DeNelle.Diagnostics.UICaptureMode.EditorRequestKey, true);
            Debug.Log("[UICap] capture requested -> entering Play mode (graphics run = real pixels; " +
                      "-nographics = blank frames + drive log). NOTE: does NOT work in -batchmode -quit; " +
                      "use RunCaptureHeadless for headless.");
            if (!EditorApplication.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        // ---------------------------------------------------------------------
        //  NEW -- reliable synchronous edit-mode render (headless).
        // ---------------------------------------------------------------------
        /// <summary>
        /// Headless, batchmode-safe UI capture. Renders each supported code-built panel
        /// to a PNG under <c>Builds/ui-capture/</c> WITHOUT entering Play mode. Prints
        /// every saved path and a final <c>UI_CAPTURE_OK &lt;count&gt;</c> marker.
        /// Callable via <c>-executeMethod DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless</c>.
        /// </summary>
        public static void RunCaptureHeadless()
        {
            int count = 0;
            try
            {
                Directory.CreateDirectory(OutDir);
                Debug.Log("[UICap-HL] headless UI capture start (batchmode=" + Application.isBatchMode +
                          ", graphicsDevice=" + SystemInfo.graphicsDeviceType +
                          ", out=" + Path.GetFullPath(OutDir) + ")");

                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Debug.LogWarning("[UICap-HL] NO graphics device (looks like -nographics) -- pngs will be " +
                                     "BLANK. Re-run WITHOUT -nographics for real pixels.");
                }

                count += CaptureFoundingEchoCard();
                count += CapturePauseMenu();

                Debug.Log("[UICap-HL] done -> " + Path.GetFullPath(OutDir));
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] capture run threw: " + e);
            }

            // The marker a headless caller greps to confirm the run produced pixels.
            Debug.Log("UI_CAPTURE_OK " + count);
        }

        // ---------------------------------------------------------------------
        //  Panel: the founding / Echo unlock card (EchoUnlockDialogue) at its
        //  LONGEST copy -- the founding echo Aldwin. This is the panel with the
        //  just-fixed text/button overlap the owner cares about. We render both
        //  the flavor state and the "Tell me more" LORE state (the worst-case
        //  copy), at two mobile-landscape resolutions, so any overlap shows as it
        //  does on device.
        // ---------------------------------------------------------------------
        private static int CaptureFoundingEchoCard()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject canvasGo = null;
            EchoUnlockDialogue dlg = null;

            try
            {
                // A pre-seeded EventSystem so EchoUnlockDialogue.EnsureEventSystem no-ops
                // (its DontDestroyOnLoad path warns harmlessly in edit mode; avoid the noise).
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                // Founding spirit == count 1 == Aldwin (long founding + lore copy).
                EchoRosterEntry entry = EchoRosterCatalog.ByCount(1);
                if (entry == null)
                {
                    Debug.LogWarning("[UICap-HL] EchoRosterCatalog.ByCount(1) returned null -- founding card skipped.");
                    return 0;
                }

                // Build the REAL card via its own public entry (data-driven, guarded).
                bool shown = EchoUnlockDialogue.Show(entry, 1);
                dlg = UnityEngine.Object.FindAnyObjectByType<EchoUnlockDialogue>();
                if (dlg == null)
                {
                    Debug.LogWarning("[UICap-HL] EchoUnlockDialogue instance not found after Show(shown=" +
                                     shown + ") -- founding card skipped.");
                    return 0;
                }

                canvasGo = GetPrivateGameObject(dlg, "_canvas");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] EchoUnlockDialogue._canvas was null -- card did not build; skipped.");
                    return 0;
                }

                // -- FLAVOR state (the default awaken copy) --
                if (RenderCanvasToPng(canvasGo, OutDir + "EchoUnlockDialogue_Aldwin_flavor_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "EchoUnlockDialogue_Aldwin_flavor_2340x1080.png", 2340, 1080)) saved++;

                // -- LORE state (the LONGEST copy, swapped in by "Tell me more") --
                // OnTellMore is private; invoking it mirrors the real button so we capture
                // exactly what the owner sees after tapping "Tell me more".
                InvokePrivate(dlg, "OnTellMore");
                if (RenderCanvasToPng(canvasGo, OutDir + "EchoUnlockDialogue_Aldwin_lore_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "EchoUnlockDialogue_Aldwin_lore_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] founding echo card capture threw: " + e);
            }
            finally
            {
                // Edit-mode teardown MUST be DestroyImmediate (the card's own Close uses
                // runtime Destroy, which errors in edit mode).
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (dlg != null && dlg.gameObject != null) UnityEngine.Object.DestroyImmediate(dlg.gameObject);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the Pause overlay (PauseController) -- the code-built kit modal
        //  (FrameOptions): Resume / Settings / Quit to Title over a scrim. The owner
        //  reported the option buttons "stacked". We build the REAL modal headless
        //  (attaching a bare SettingsController so the WORST case -- all three option
        //  buttons -- renders) and shoot it at two mobile-landscape resolutions so any
        //  overlap shows exactly as on device.
        //
        //  PauseController lives in the DeNelle.Settings assembly, which DeNelle.Editor
        //  does NOT reference, so every touch is via reflection (type-load + private
        //  field/method access) -- no compile-time dependency, no asmdef change.
        // ---------------------------------------------------------------------
        private static int CapturePauseMenu()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject pauseGo = null;
            GameObject settingsGo = null;
            GameObject canvasGo = null;

            try
            {
                Type pauseType = ResolveType("DeNelle.Settings.PauseController");
                if (pauseType == null)
                {
                    Debug.LogWarning("[UICap-HL] PauseController type not found -- pause menu capture skipped.");
                    return 0;
                }

                // A pre-seeded EventSystem so kit UI construction never warns in edit mode.
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                pauseGo = new GameObject("~UICapPause");
                var pause = pauseGo.AddComponent(pauseType);

                // Attach a bare SettingsController so the Settings button (the 3-button worst case)
                // builds. Guarded: if it cannot attach, the menu still builds with Resume + Quit.
                Type settingsType = ResolveType("DeNelle.Settings.SettingsController");
                if (settingsType != null)
                {
                    try
                    {
                        settingsGo = new GameObject("~UICapSettings");
                        var settings = settingsGo.AddComponent(settingsType);
                        SetPrivateField(pause, "_settings", settings);
                    }
                    catch (Exception se)
                    {
                        Debug.LogWarning("[UICap-HL] could not attach SettingsController (2-button capture): " + se.Message);
                    }
                }

                // Build the REAL modal. EnsureBuilt is private; it constructs _modal and leaves the
                // canvas INACTIVE. Pause() is deliberately NOT called, so there is no Time.timeScale
                // freeze and no PanelManager.NotifyOpened side effect -- a pure, static build.
                InvokePrivate(pause, "EnsureBuilt");

                object modal = GetPrivateFieldValue(pause, "_modal");
                if (modal == null)
                {
                    Debug.LogWarning("[UICap-HL] PauseController._modal null after EnsureBuilt -- pause menu did not build; skipped.");
                    return 0;
                }
                canvasGo = GetFieldValue(modal, "canvas") as GameObject;
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] Pause modal canvas null -- pause menu build produced no canvas; skipped.");
                    return 0;
                }

                canvasGo.SetActive(true);   // EnsureBuilt builds it hidden; show it for the shot

                if (RenderCanvasToPng(canvasGo, OutDir + "PauseMenu_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "PauseMenu_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] pause menu capture threw: " + e);
            }
            finally
            {
                // Destroy the canvas FIRST so PauseController.OnDestroy sees _modal.canvas == null and
                // never calls runtime Destroy() (illegal in edit mode -- same contract as the card).
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (settingsGo != null) UnityEngine.Object.DestroyImmediate(settingsGo);
                if (pauseGo != null) UnityEngine.Object.DestroyImmediate(pauseGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Render a uGUI canvas subtree to a PNG in EDIT mode (no Play).
        //  ScreenSpaceOverlay canvases render straight to the backbuffer and cannot
        //  be captured off a camera, so we flip the canvas to ScreenSpaceCamera,
        //  point a throwaway camera at a RenderTexture, force a synchronous layout +
        //  TMP mesh rebuild, render, and read the pixels back.
        // ---------------------------------------------------------------------
        private static bool RenderCanvasToPng(GameObject canvasGo, string path, int w, int h)
        {
            if (canvasGo == null) return false;

            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[UICap-HL] " + path + " -- no Canvas on target; skipped.");
                return false;
            }

            RenderMode prevMode = canvas.renderMode;
            Camera prevCam = canvas.worldCamera;
            float prevPlane = canvas.planeDistance;

            GameObject camGo = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                // Throwaway camera -> RenderTexture at the requested resolution.
                camGo = new GameObject("~UICapCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 1000f;
                cam.cullingMask = ~0;   // see every layer (the UI may be on any layer)

                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create();
                cam.targetTexture = rt;

                // Flip the overlay canvas to camera-space so the camera can capture it.
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;

                // CanvasScaler.Update does NOT run in a synchronous edit-mode call, so set
                // the ScaleWithScreenSize scale factor by hand (mirrors the scaler math) --
                // the card's fraction anchors reflow regardless, but font point size needs it
                // so overlap reproduces exactly as on device.
                ApplyScreenSpaceScale(canvas, w, h);

                // Force a full synchronous layout + graphic + TMP rebuild (twice: TMP
                // auto-size for the flavor block can need a second pass to settle).
                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    var rootRt = canvasGo.GetComponent<RectTransform>();
                    if (rootRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
                    foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (t != null) t.ForceMeshUpdate();
                    }
                    Canvas.ForceUpdateCanvases();
                }

                // Render (SRP-correct path first; fall back to legacy Camera.Render).
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) cam.SubmitRenderRequest(req);
                else cam.Render();

                // Read the pixels back.
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                tex.Apply(false);

                byte[] png = tex.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    Debug.LogWarning("[UICap-HL] " + path + " -- EncodeToPNG produced no bytes; skipped.");
                    return false;
                }

                File.WriteAllBytes(path, png);
                Debug.Log("[UICap-HL] saved " + w + "x" + h + " -> " + Path.GetFullPath(path) +
                          " (" + png.Length + " bytes)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] render " + path + " threw: " + e);
                return false;
            }
            finally
            {
                // Restore the canvas so a reused card renders identically on the next call.
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

        /// <summary>Set <c>canvas.scaleFactor</c> to what a ScaleWithScreenSize +
        /// MatchWidthOrHeight CanvasScaler would compute for a w x h target -- the scaler's
        /// own Update does not run in a synchronous edit-mode call.</summary>
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
            float logWeighted = Mathf.Lerp(logW, logH, match);
            float sf = Mathf.Pow(2f, logWeighted);
            if (sf <= 0f || float.IsNaN(sf) || float.IsInfinity(sf)) sf = 1f;

            canvas.scaleFactor = sf;
            canvas.referencePixelsPerUnit = scaler.referencePixelsPerUnit > 0f
                ? scaler.referencePixelsPerUnit : 100f;
        }

        // ---------------------------------------------------------------------
        //  Small reflection helpers (private access into the runtime card).
        // ---------------------------------------------------------------------
        private static GameObject GetPrivateGameObject(object target, string fieldName)
        {
            if (target == null) return null;
            var f = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(target) as GameObject : null;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            if (target == null) return;
            var m = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null) m.Invoke(target, null);
            else Debug.LogWarning("[UICap-HL] private method '" + methodName + "' not found -- lore state skipped.");
        }

        /// <summary>Find a type by full name across the loaded assemblies (the pause menu lives in
        /// DeNelle.Settings, which this editor assembly does not reference at compile time).</summary>
        private static Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Read a private instance field's value as a boxed object (nulls are safe).</summary>
        private static object GetPrivateFieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            var f = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(target) : null;
        }

        /// <summary>Read a public-or-private instance field's value as a boxed object (nulls are safe).</summary>
        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(target) : null;
        }

        /// <summary>Set a private instance field (used to inject the SettingsController fixture).</summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
            else Debug.LogWarning("[UICap-HL] private field '" + fieldName + "' not found.");
        }
    }
}
