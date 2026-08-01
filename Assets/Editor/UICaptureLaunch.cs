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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;   // lore-fragments.json parse (WO-795 lore capture)
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;    // PanelManager / PanelHandle (lore-modal arbiter teardown)
using DeNelle.Dungeons;   // LoreReadingModal, LoreReadRequest, LoreFragmentSet (WO-795)
using DeNelle.Village;    // EchoUnlockDialogue, EchoRosterCatalog, EchoRosterEntry, Tower, BuildMenu
using DeNelle.Village.UI; // TowerManagerPanel, PlacedTowerListVM (WO-795)

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
                count += CaptureEchoRoster();
                count += CaptureHelpMenu();
                count += CaptureDailyQuestHud();
                count += CaptureLoreReadingModal();
                count += CaptureTowerManagerPanel();
                count += CaptureBuildMenuUpgradeTower();

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
        //  Panel: the persistent Echo pip + "Pets" HUD button overlay
        //  (EchoUnlockFeedback). WO 2026-07-24 moved this button from bottom-centre
        //  (ate centre real estate) to the RIGHT screen edge, vertically centred, at
        //  a >=112px square touch target -- this shot is the felt-verify that it now
        //  hugs the right edge and did not drift back to centre.
        //
        //  We capture the pip+button OVERLAY canvas, NOT the full EchoRosterView modal
        //  that the button opens: EchoRosterView.OpenPanel pulls heavy runtime deps
        //  (EchoRosterVM.CreateDefault over EchoService state, PanelManager.NotifyOpened
        //  side effects, a DontDestroyOnLoad host) that do not stand up cleanly in a
        //  synchronous edit-mode render. The pip+button canvas is precisely the chrome
        //  this WO repositioned, so it is the right thing to screenshot. (Per WO: do
        //  NOT force EchoRoster headlessly.)
        //
        //  EchoUnlockFeedback lives in DeNelle.Village (already referenced here). Its
        //  Start() does NOT run on AddComponent in edit mode, so we drive the same two
        //  private builders Start calls -- BuildPip (creates _pipCanvas) then
        //  BuildPetBoxButton (adds the right-edge Pets button onto it).
        // ---------------------------------------------------------------------
        private static int CaptureEchoRoster()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject pipCanvas = null;

            try
            {
                // Pre-seed an EventSystem so BuildPetBoxButton's EnsureEventSystem no-ops
                // (its DontDestroyOnLoad path warns harmlessly in edit mode; avoid the noise).
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                hostGo = new GameObject("~UICapEchoFeedback");
                var feedback = hostGo.AddComponent<EchoUnlockFeedback>();

                // Build the pip, then the Pets button onto its canvas (the two private methods
                // Start would call). Both are guarded/font-safe kit construction -- no play mode.
                InvokePrivate(feedback, "BuildPip");
                InvokePrivate(feedback, "BuildPetBoxButton");

                pipCanvas = GetPrivateGameObject(feedback, "_pipCanvas");
                if (pipCanvas == null)
                {
                    Debug.LogWarning("[UICap-HL] EchoUnlockFeedback._pipCanvas null after BuildPip -- pip/Pets capture skipped.");
                    return 0;
                }

                pipCanvas.SetActive(true);   // scene-gate never ran (no Start); ensure it is visible

                if (RenderCanvasToPng(pipCanvas, OutDir + "EchoPetButton_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(pipCanvas, OutDir + "EchoPetButton_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] echo pip/Pets button capture threw: " + e);
            }
            finally
            {
                // Edit-mode teardown MUST be DestroyImmediate (same contract as the card/pause shots).
                if (pipCanvas != null) UnityEngine.Object.DestroyImmediate(pipCanvas);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the Help/Settings modal (HelpMenu) -- WO-795 wave 2 wrapped its
        //  button column in a masked ScrollRect well so the rows can never spill
        //  into the kit's shared Close band. Editor compiles carry the
        //  DEVELOPMENT_BUILD/UNITY_EDITOR rows too (Dev Tools + the dev grant),
        //  so this shot renders the WORST-case row count the well must clip.
        //
        //  HelpMenu lives in the DeNelle.HUD assembly, which DeNelle.Editor does
        //  NOT reference, so every touch is via reflection (the CapturePauseMenu
        //  recipe). Awake never runs on an edit-mode AddComponent, so no
        //  PanelManager registration leaks from this capture.
        // ---------------------------------------------------------------------
        private static int CaptureHelpMenu()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;

            try
            {
                Type helpType = ResolveType("DeNelle.HUD.HelpMenu");
                if (helpType == null)
                {
                    Debug.LogWarning("[UICap-HL] HelpMenu skipped -- DeNelle.HUD.HelpMenu type not found.");
                    return 0;
                }

                // Pre-seed an EventSystem so kit UI construction never warns in edit mode.
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                hostGo = new GameObject("~UICapHelpMenu");
                var help = hostGo.AddComponent(helpType);

                // Build the REAL modal. EnsureBuilt is private; it constructs _modal
                // (frame + scroll well + rows + toast) and leaves the canvas INACTIVE.
                // SetOpen/ToggleOverlay are deliberately NOT called -- no PanelManager
                // NotifyOpened side effect, a pure static build (pause-menu contract).
                InvokePrivate(help, "EnsureBuilt");

                object modal = GetPrivateFieldValue(help, "_modal");
                if (modal == null)
                {
                    Debug.LogWarning("[UICap-HL] HelpMenu._modal null after EnsureBuilt -- help menu skipped.");
                    return 0;
                }
                canvasGo = GetFieldValue(modal, "canvas") as GameObject;
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] HelpMenu modal canvas null -- help menu build produced no canvas; skipped.");
                    return 0;
                }

                canvasGo.SetActive(true);   // EnsureBuilt builds it hidden; show it for the shot

                if (RenderCanvasToPng(canvasGo, OutDir + "HelpMenu_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "HelpMenu_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] help menu capture threw: " + e);
            }
            finally
            {
                // Destroy the canvas FIRST so HelpMenu.OnDestroy (if it ever ran) sees a
                // dead canvas and never calls runtime Destroy (edit-mode teardown contract).
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the Daily Quests master-detail card (DailyQuestHud) -- WO-795
        //  wave 2 rebuilt the quest list as a ScrollRect well (Viewport drag
        //  catcher + RectMask2D, fixed-height LayoutElement rows) so a deep quest
        //  log scrolls instead of truncating.
        //
        //  DeNelle.HUD is unreferenced -> reflection (CapturePauseMenu recipe).
        //  DailyQuestService.Instance only exists in Play mode (RuntimeInitialize
        //  bootstrap), so the VM resolves an EMPTY set headless and the shot
        //  carries the honest empty state (frame + wells + "No daily quests
        //  today."). We deliberately do NOT reflect-seed the service: standing it
        //  up would need a private static Instance injection AND writes today's
        //  rolled set into the owner's editor PlayerPrefs -- a real side effect,
        //  not a cheap fake. The WO-795 well itself is built by EnsureBuilt
        //  regardless of data, so its geometry IS in the shot.
        // ---------------------------------------------------------------------
        private static int CaptureDailyQuestHud()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;

            try
            {
                Type hudType = ResolveType("DeNelle.HUD.DailyQuestHud");
                if (hudType == null)
                {
                    Debug.LogWarning("[UICap-HL] DailyQuestHud skipped -- DeNelle.HUD.DailyQuestHud type not found.");
                    return 0;
                }

                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                hostGo = new GameObject("~UICapDailyQuestHud");
                var hud = hostGo.AddComponent(hudType);

                // EnsureBuilt creates the VM + canvas + FrameQuest chrome + the WO-795
                // scroll well, then hides the chrome root. Repaint (normally driven by
                // OnEnable, which never runs in edit mode) paints the list/detail wells.
                // ClearZone preserves the kit's ZoneBacking by name, so the first paint
                // makes zero runtime-Destroy calls -- edit-mode safe.
                InvokePrivate(hud, "EnsureBuilt");
                InvokePrivate(hud, "Repaint");

                canvasGo = GetPrivateGameObject(hud, "_canvas");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] DailyQuestHud._canvas null after EnsureBuilt -- daily quests skipped.");
                    return 0;
                }

                // EnsureBuilt hides the CHROME ROOT (not the canvas); show it for the shot.
                object chrome = GetPrivateFieldValue(hud, "_chrome");
                var chromeRoot = GetFieldValue(chrome, "root") as GameObject;
                if (chromeRoot != null) chromeRoot.SetActive(true);

                if (RenderCanvasToPng(canvasGo, OutDir + "DailyQuestHud_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "DailyQuestHud_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] daily quest hud capture threw: " + e);
            }
            finally
            {
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the lore-stone reading modal (LoreReadingModal, DeNelle.Dungeons
        //  -- a referenced assembly, so this capture is reflection-light). WO-795
        //  wave 2 gave the body a vertical ScrollRect well so long lore scrolls at
        //  a FIXED readable size (owner law: reflow, never shrink fonts).
        //
        //  WORST CASE by construction: we parse the canon lore-fragments.json from
        //  StreamingAssets and pick the fragment with the LONGEST total body --
        //  today that is journal-4, but the pick tracks the data as canon grows.
        //  Show() is the panel's own public entry and runs Build synchronously
        //  (no lifecycle dependency), so the REAL modal renders the canon prose
        //  verbatim. Teardown NotifyCloses the arbiter handle by hand because the
        //  panel's own Close() uses runtime Destroy (illegal in edit mode).
        // ---------------------------------------------------------------------
        private static int CaptureLoreReadingModal()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            LoreReadingModal modal = null;
            GameObject canvasGo = null;

            try
            {
                string jsonPath = Path.Combine(Application.streamingAssetsPath,
                    "Data/Canonical/lore-fragments.json");
                if (!File.Exists(jsonPath))
                {
                    Debug.LogWarning("[UICap-HL] LoreReadingModal skipped -- lore-fragments.json not found at "
                                     + jsonPath);
                    return 0;
                }

                LoreFragmentSet set = JsonConvert.DeserializeObject<LoreFragmentSet>(
                    File.ReadAllText(jsonPath));
                if (set == null || set.Fragments == null || set.Fragments.Count == 0)
                {
                    Debug.LogWarning("[UICap-HL] LoreReadingModal skipped -- lore-fragments.json parsed empty.");
                    return 0;
                }

                // Longest total body = the worst case the scroll well must carry.
                LoreFragment worst = null;
                int worstLen = -1;
                foreach (var f in set.Fragments)
                {
                    if (f == null || f.Body == null || f.Body.Length == 0) continue;
                    int len = 0;
                    foreach (var p in f.Body) len += p != null ? p.Length : 0;
                    if (len > worstLen) { worstLen = len; worst = f; }
                }
                if (worst == null)
                {
                    Debug.LogWarning("[UICap-HL] LoreReadingModal skipped -- no fragment carries a body.");
                    return 0;
                }

                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                LoreReadingModal.Show(new LoreReadRequest
                {
                    LoreStoneId = worst.Id,
                    Title = worst.Title,
                    Body = worst.Body,
                });
                modal = UnityEngine.Object.FindAnyObjectByType<LoreReadingModal>();
                if (modal == null)
                {
                    Debug.LogWarning("[UICap-HL] LoreReadingModal instance not found after Show -- lore capture skipped.");
                    return 0;
                }

                canvasGo = GetPrivateGameObject(modal, "_canvas");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] LoreReadingModal._canvas null -- modal did not build; skipped.");
                    return 0;
                }

                Debug.Log("[UICap-HL] lore worst-case fragment = '" + worst.Id + "' (" + worstLen + " chars).");
                if (RenderCanvasToPng(canvasGo, OutDir + "LoreReadingModal_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "LoreReadingModal_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] lore reading modal capture threw: " + e);
            }
            finally
            {
                // Build() registered + NotifyOpened the "LoreReading" arbiter handle; clear
                // it by hand (the panel's own Close() would runtime-Destroy -- edit-illegal).
                try
                {
                    if (modal != null)
                    {
                        var handle = GetPrivateFieldValue(modal, "_handle") as PanelHandle;
                        if (handle != null) PanelManager.NotifyClosed(handle);
                    }
                }
                catch (Exception pe)
                {
                    Debug.LogWarning("[UICap-HL] lore modal arbiter release failed (harmless): " + pe.Message);
                }
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (modal != null && modal.gameObject != null) UnityEngine.Object.DestroyImmediate(modal.gameObject);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the tower manager (TowerManagerPanel, DeNelle.Village.UI --
        //  referenced, so direct types + private-field reflection only). WO-795
        //  wave 2 moved the tower rows into a persistent ScrollRect well so an
        //  unbounded tower list scrolls instead of truncating at the old 0.24
        //  floor. We spawn EIGHT stub towers (below) so the rows OVERFLOW the
        //  well -- the clip is the thing this shot verifies.
        //
        //  Refresh() clears the body's non-well children with runtime Destroy()
        //  (edit-illegal), so those children (the kit ZoneBacking plate, plus the
        //  footer text on the no-frame fallback path) are PARKED outside the body
        //  for the call and restored at their original sibling index after --
        //  zero Destroy noise, true chrome in the shot. NO row is selected: row
        //  selection spawns the in-world marker via CreatePrimitive + runtime
        //  Destroy on its collider, which edit mode forbids.
        // ---------------------------------------------------------------------
        private static int CaptureTowerManagerPanel()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;
            GameObject[] stubTowers = null;
            PlacedTowerListVM vm = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                stubTowers = CreateStubTowers(8);

                hostGo = new GameObject("~UICapTowerManager");
                var panel = hostGo.AddComponent<TowerManagerPanel>();

                // Awake never ran (edit mode), so build the modal + inject the VM the
                // panel's Awake would have created. CreateDefault's FindObjectsByType
                // poll picks the stub towers up like real ones.
                InvokePrivate(panel, "EnsureBuilt");
                vm = PlacedTowerListVM.CreateDefault(null);
                SetPrivateField(panel, "_vm", vm);

                // Park non-well body children across Refresh (see banner).
                var bodyHost = GetPrivateFieldValue(panel, "_bodyHost") as Transform;
                var wellGo = GetPrivateFieldValue(panel, "_listViewport") as GameObject;
                var parked = new List<KeyValuePair<Transform, int>>();
                if (bodyHost != null)
                {
                    for (int i = 0; i < bodyHost.childCount; i++)
                    {
                        var ch = bodyHost.GetChild(i);
                        if (ch == null || (wellGo != null && ch.gameObject == wellGo)) continue;
                        parked.Add(new KeyValuePair<Transform, int>(ch, i));
                    }
                    foreach (var kv in parked) kv.Key.SetParent(null, false);
                }
                try
                {
                    InvokePrivate(panel, "Refresh");
                }
                finally
                {
                    foreach (var kv in parked)
                    {
                        if (kv.Key == null || bodyHost == null) continue;
                        kv.Key.SetParent(bodyHost, false);
                        kv.Key.SetSiblingIndex(kv.Value);
                    }
                }

                object modal = GetPrivateFieldValue(panel, "_modal");
                canvasGo = GetFieldValue(modal, "canvas") as GameObject;
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] TowerManagerPanel modal canvas null -- tower manager skipped.");
                    return 0;
                }

                canvasGo.SetActive(true);   // EnsureBuilt builds it hidden; show it for the shot

                if (RenderCanvasToPng(canvasGo, OutDir + "TowerManagerPanel_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "TowerManagerPanel_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] tower manager capture threw: " + e);
            }
            finally
            {
                if (vm != null) vm.Dispose();
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                DestroyStubTowers(stubTowers);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the BuildMenu UPGRADE TOWER screen (DeNelle.Village -- direct
        //  types). WO-795 wave 2 put the placed-tower list into a ScrollRect well
        //  above the fixed info band (UpgradeInfoTop); the info block + Upgrade
        //  CTA stay OUTSIDE the well. Worst case rendered here: eight stub tower
        //  rows overflowing the well PLUS the first tower SELECTED, so the cost
        //  rows / result line / Upgrade button all render below the list.
        //
        //  RenderUpgradeTower is invoked DIRECTLY instead of the public Render():
        //  Render()'s screen-switch clear uses runtime Destroy() on the body
        //  children (edit-illegal), and on a fresh build there is nothing to
        //  clear anyway. Known delta: the "Crystals: N" readout Render() tops
        //  every screen with is absent from this shot (it lives in Render, not
        //  RenderUpgradeTower). The VM is built through its public injectable
        //  ctor (null economy -> the standalone 500-crystal fallback), so no
        //  service/scene context is needed -- BuildModeController is NOT touched.
        // ---------------------------------------------------------------------
        private static int CaptureBuildMenuUpgradeTower()
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;
            GameObject[] stubTowers = null;
            BuildMenuVM vm = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                stubTowers = CreateStubTowers(8);

                hostGo = new GameObject("~UICapBuildMenu");
                var menu = hostGo.AddComponent<BuildMenu>();

                InvokePrivate(menu, "EnsureBuilt");

                // Public injectable ctor: (economy, towers, wallRepair, fallbackCrystals,
                // onClose) -- null economy/wallRepair are the VM's own supported fallbacks.
                vm = new BuildMenuVM(null, PlacedTowerListVM.CreateDefault(null), null, 500, null);
                SetPrivateField(menu, "_vm", vm);

                // Select the first stub tower BEFORE the single render pass so the
                // selected-row highlight + upgrade info block are in the shot.
                SetPrivateField(menu, "_selectedTowerForUpgrade",
                    stubTowers[0].GetComponent<Tower>());

                InvokePrivate(menu, "RenderUpgradeTower");

                object modal = GetPrivateFieldValue(menu, "_modal");
                canvasGo = GetFieldValue(modal, "canvas") as GameObject;
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] BuildMenu modal canvas null -- upgrade-tower screen skipped.");
                    return 0;
                }

                canvasGo.SetActive(true);   // EnsureBuilt builds it hidden; show it for the shot

                if (RenderCanvasToPng(canvasGo, OutDir + "BuildMenuUpgradeTower_1920x1080.png", 1920, 1080)) saved++;
                if (RenderCanvasToPng(canvasGo, OutDir + "BuildMenuUpgradeTower_2340x1080.png", 2340, 1080)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] build menu upgrade-tower capture threw: " + e);
            }
            finally
            {
                if (vm != null) vm.Dispose();
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                DestroyStubTowers(stubTowers);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Stub towers for the two tower-list captures: bare GameObjects carrying
        //  a REAL Tower component. Tower has no RequireComponent/Reset/OnValidate
        //  and its stat getters are null-guarded (no TowerData -> level 1, rng 0,
        //  dmg 0), so an un-awoken edit-mode instance is inert -- but it IS what
        //  FindObjectsByType<Tower> returns, so both panels list it through their
        //  real VM path. Eight of them overflow the WO-795 wells on purpose: the
        //  clip-not-spill behaviour is exactly what the screenshots verify.
        // ---------------------------------------------------------------------
        private static GameObject[] CreateStubTowers(int count)
        {
            string[] flavors = { "Flame", "Ice", "Aether", "Stone" };
            var towers = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                towers[i] = new GameObject("Tower_" + flavors[i % flavors.Length] + (i + 1));
                towers[i].AddComponent<Tower>();
            }
            return towers;
        }

        private static void DestroyStubTowers(GameObject[] towers)
        {
            if (towers == null) return;
            foreach (var t in towers)
                if (t != null) UnityEngine.Object.DestroyImmediate(t);
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
