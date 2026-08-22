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
// OUTPUT: Builds/ui-capture/<PanelName>_<w>x<h>.png  +  three DISTINCT marker lines
// the caller greps:
//     UI_CAPTURE_OK <count>                 -- non-blank frames written
//     UI_CAPTURE_FIDELITY_OK <n> builds     -- every panel was BUILT at the size it
//                                              was shot at (or _DEGRADED, an error)
//     UI_GEOMETRY_OK <n> canvases           -- numeric layout assertions passed
//                                              (or UI_GEOMETRY_FAIL x<n>, an error)
//     UI_ENDSTATE_FIT_OK <n> banners        -- WO-952: no `body rows COMPRESSED to fit`
//                                              fired AND the RESOLVED band stack measures
//                                              >= 0.995 of the content's own demand
//                                              (or UI_ENDSTATE_FIT_FAIL x<n>, an error).
//                                              Carries the numbers on BOTH paths.
//
// =============================================================================
//  THE FIDELITY HOLE THIS FILE USED TO HAVE (fixed 2026-08-05) -- READ THIS
// -----------------------------------------------------------------------------
//  Until today every panel was BUILT ONCE and then RE-SCALED per shot: the
//  capture flipped the canvas to ScreenSpaceCamera and called ApplyScreenSpaceScale,
//  which rewrites ONLY `canvas.scaleFactor`. It never resized the root canvas rect
//  and never moved `Screen.width`/`Screen.height`.
//
//  But panels compute their ZONE GEOMETRY AT BUILD TIME from
//  `ElarionUiKit.PostScaleCanvasHeight`, which reads `Screen.*` (and falls back to a
//  hard-coded 1920 when Screen is unusable). So EVERY png a run wrote shared ONE
//  geometry -- the editor process's -- and the "1920x1080" / "2340x1080" in the
//  filenames were LABELS, NOT LAYOUTS. Font point size reproduced; zone geometry did
//  not. That is precisely how the founding Echo card passed green at two "sizes" all
//  night while, on the device, its caption rendered entirely off the black plate.
//
//  THE FIX, two halves:
//    (1) BUILD PER TARGET SIZE. Every capture is now wrapped in ForEachTarget, which
//        runs the WHOLE build->shoot->teardown cycle once per target. Nothing is
//        built once and re-labelled.
//    (2) MOVE THE SURFACE BEFORE THE BUILD. CaptureSurfaceScope sets the resolution
//        the kit resolves geometry against, so PostScaleCanvasHeight returns the TARGET
//        value while the panel is being constructed. The scope VERIFIES the move by
//        reading the surface back; if it did not take, it does NOT pretend -- it logs
//        loudly and the run reports UI_CAPTURE_FIDELITY_DEGRADED.
//
//  WHY IT IS NOT THE GAME VIEW ANY MORE (2026-08-05 evening, from CAPTURED DATA).
//  The first cut of (2) drove the editor's main game view (the GameViewSizes /
//  SizeSelectionCallback reflection recipe) and hoped Screen.* would follow. It does
//  not, and the run proved it: Builds/ui-capture-rail.log:475 records
//  "batchmode=True, graphicsDevice=Direct3D11 ... screen=640x480" -- a REAL D3D11
//  device, so this is not the -nographics case -- and :489 records the reflection
//  SUCCEEDING ("game view accepted the size but Screen still reads 640x480").
//  UI_CAPTURE_FIDELITY_DEGRADED 38/38 was the whole run. Root cause: `-batchmode`
//  builds no editor window layout, so there is no GameView GUIView for Screen.* to
//  mirror and it stays on the 640x480 offscreen default no matter what the size list
//  says. Dropping -nographics cannot help -- that run already had a graphics device.
//  So the harness stopped depending on editor internals for the load-bearing move and
//  drives ElarionUiKit's injectable surface instead (default = Screen.*, override =
//  editor-only). The game-view move is still ATTEMPTED, because in an interactive
//  editor it really does move Screen.* and that also covers any panel still reading
//  Screen directly -- but nothing depends on it.
//
//  And because eyes-only review is not a defence, AuditGeometry now asserts the
//  layout NUMERICALLY on every captured canvas (see its banner).
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
using DeNelle.Core.Diagnostics;   // FlowTrace / ITraceSink (WO-952 EndState fit tap)
using DeNelle.Core.Quests;   // QuestDef/QuestStage/QuestReward (rumor-board worst-case fixture)
using DeNelle.Dungeons;   // LoreReadingModal, LoreReadRequest, LoreFragmentSet (WO-795)
using DeNelle.Village;    // EchoUnlockDialogue, EchoRosterCatalog, EchoRosterEntry, Tower, BuildMenu
using DeNelle.Village.Hero; // RumorBoardPanel, RumorBoardVM, IRumorBoardBackend (WO-810 board capture)
using DeNelle.Village.UI; // TowerManagerPanel, PlacedTowerListVM (WO-795)
using DeNelle.Village.Talents; // HeroSkillTreePanelMvvm (tree + hot-swap rail capture)

namespace DeNelle.Editor
{
    /// <summary>Editor entries for the UI-capture harness (legacy Play-mode drive +
    /// the reliable synchronous edit-mode render).</summary>
    public static class UICaptureLaunch
    {
        private const string OutDir = "Builds/ui-capture/";

        // ---------------------------------------------------------------------
        //  BLANK GUARD (2026-08-04). UI_CAPTURE_OK <n> is a PRE-SHIP GATE, so the
        //  number has to mean real pixels. It did not: every render was written and
        //  counted regardless of what was in it, so a run with no graphics device
        //  produced a full green count over flat black frames.
        //
        //  That is not hypothetical -- it is exactly how the owner's UI_REVIEW went
        //  empty. The AutoPilot fleet's own capture writer carries the same
        //  soft-fail ("a -nographics fleet writes a blank frame, never an error"),
        //  and a default-mode fleet run at 21:21 on 2026-08-04 overwrote 35 real
        //  review shots with 33150-byte black PNGs. A blank that looks like a
        //  capture is worse than an absent one: MISSING gets chased, blank gets
        //  reviewed. So every frame is measured BEFORE it is written; a flat one is
        //  refused, logged as an error, and counted as a FAILURE.
        // ---------------------------------------------------------------------
        private const int BlankSampleStride = 13;       // ~190k samples at 2340x1080
        private const int BlankMinDistinctBuckets = 3;  // 4-bit-per-channel quantisation
        private const float BlankMinInkFraction = 0.002f;

        // ---------------------------------------------------------------------
        //  CAPTURE TARGETS (2026-08-05).
        //
        //  2670x1200 is the Solana SEEKER'S REAL SURFACE. It had NEVER been shot by
        //  anything in this repo. The 2340x1080 entry below was only ever a harness
        //  size -- tools' run-autopilot-fleet.ps1 still (wrongly) describes it as the
        //  Seeker's exact screen; fix it there too. 1920x1080 is kept as the
        //  desktop/reference landscape.
        //
        //  Each target drives a FULL build->shoot->teardown cycle (ForEachTarget), so
        //  the geometry in the png is the geometry that resolution really produces.
        // ---------------------------------------------------------------------
        private struct CaptureTarget
        {
            public readonly int W;
            public readonly int H;
            public readonly string Tag;
            public CaptureTarget(int w, int h) { W = w; H = h; Tag = w + "x" + h; }
        }

        private static readonly CaptureTarget[] LandscapeTargets =
        {
            new CaptureTarget(1920, 1080),   // desktop / reference landscape
            new CaptureTarget(2340, 1080),   // common tall-phone landscape (NOT the Seeker)
            new CaptureTarget(2670, 1200),   // THE SEEKER'S REAL SURFACE
        };

        // Fidelity bookkeeping (reported as UI_CAPTURE_FIDELITY_OK / _DEGRADED).
        private static int _fidelityOk;
        private static int _fidelityDegraded;
        private static readonly HashSet<string> _fidelityReasons = new HashSet<string>(StringComparer.Ordinal);

        // Screen.* itself NEVER moves in batchmode (proven -- see the file banner). Tracked
        // separately from fidelity so the report can name that residual honestly instead of
        // letting a reader assume every Screen.* reader in the tree followed the target.
        private static int _screenStuckBuilds;
        private static string _screenStuckAt;

        // The divergence proof: two different ASPECTS must resolve DIFFERENT zone rects, or the
        // run is back to one geometry wearing several filenames. Non-null == proven.
        private static string _geoMoveProof;
        private static string _geoMoveFailure;

        /// <summary>
        /// Run one capture body ONCE PER TARGET SIZE, with the editor's game view driven to
        /// that size FIRST so the panel is BUILT against it (not built once and re-labelled).
        /// The body owns its own build + teardown, exactly as it did before.
        /// </summary>
        private static int ForEachTarget(string panelName, Func<CaptureTarget, int> buildAndShoot)
        {
            return ForEachTarget(panelName, LandscapeTargets, buildAndShoot);
        }

        private static int ForEachTarget(string panelName, CaptureTarget[] targets,
                                         Func<CaptureTarget, int> buildAndShoot)
        {
            int total = 0;
            if (targets == null || buildAndShoot == null) return 0;
            foreach (var target in targets)
            {
                using (new CaptureSurfaceScope(target, panelName))
                {
                    try
                    {
                        total += buildAndShoot(target);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[UICap-HL] " + panelName + " @" + target.Tag + " threw: " + e);
                    }
                }
            }
            return total;
        }

        // ---------------------------------------------------------------------
        //  CaptureSurfaceScope -- move the geometry the panel BUILDS against.
        //
        //  Panels resolve their zones through ElarionUiKit.PostScaleCanvasHeight on the
        //  frame they are constructed. Nothing this harness does to the canvas AFTER the
        //  build can change those zones, so the only honest fix is to move the value the
        //  build READS.
        //
        //  (1) THE LOAD-BEARING MOVE: ElarionUiKit.SetSurfaceOverride. The kit's surface
        //      defaults to Screen.* (shipped behaviour, unchanged) and is overridable
        //      ONLY in the editor. No UnityEditor internals, so a Unity upgrade cannot
        //      silently take this away -- which is the whole reason it is not the game
        //      view any more (see the file banner for the captured proof that the game
        //      view CANNOT move Screen.* in batchmode).
        //
        //  (2) BEST EFFORT: still drive the editor's main game view too. In an
        //      INTERACTIVE editor that genuinely moves Screen.*, which additionally
        //      covers any panel that still reads Screen directly. In batchmode it is a
        //      no-op and nothing here depends on it.
        //
        //  VERIFIED, not assumed: the ctor reads the effective surface back. A scope
        //  that could not move it does NOT pretend -- it records a reason and the run
        //  ends on UI_CAPTURE_FIDELITY_DEGRADED, i.e. the harness declares itself
        //  scale-only rather than shipping a green run over one geometry wearing three
        //  filenames. Screen.* is read back too and reported separately, so nobody
        //  mistakes "the kit surface moved" for "the process's screen moved".
        // ---------------------------------------------------------------------
        private sealed class CaptureSurfaceScope : IDisposable
        {
            private static bool _probed;
            private static Type _sizesType, _groupType, _sizeType, _sizeTypeEnum, _gameViewType;
            private static object _sizes;              // GameViewSizes.instance
            private static object _group;              // its current GameViewSizeGroup
            private static EditorWindow _gameView;
            private static string _probeFailure;

            private int _prevIndex;
            private bool _restore;

            public CaptureSurfaceScope(CaptureTarget target, string panelName)
            {
                _prevIndex = -1;
                _restore = false;

                // (1) The load-bearing move: what PostScaleCanvasHeight reads at BUILD time.
                ElarionUiKit.SetSurfaceOverride(target.W, target.H);

                // (2) Best effort: the editor's own game view (moves Screen.* interactively only).
                try
                {
                    Probe();
                    if (_probeFailure == null)
                    {
                        _prevIndex = GetSelectedIndex();
                        if (Select(target.W, target.H)) _restore = _prevIndex >= 0;
                    }
                }
                catch (Exception e)
                {
                    _probeFailure = _probeFailure ?? (e.GetType().Name + ": " + e.Message);
                }

                // VERIFY -- never trust a set, read it back. Screen first, for the record:
                // in batchmode it never moves, and the run must say so rather than let a
                // reader assume every Screen.* reader in the tree followed the target.
                int sw = Screen.width, sh = Screen.height;
                bool screenMoved = sw == target.W && sh == target.H;
                if (!screenMoved)
                {
                    _screenStuckAt = sw + "x" + sh;
                    _screenStuckBuilds++;
                }

                // The EFFECTIVE surface -- the value the kit will actually resolve zones from.
                int ew = ElarionUiKit.SurfaceWidth, eh = ElarionUiKit.SurfaceHeight;
                if (ew == target.W && eh == target.H)
                {
                    _fidelityOk++;
                    return;
                }

                _fidelityDegraded++;
                string why = "ElarionUiKit surface would not move: asked for " + target.Tag +
                             ", it reports " + ew + "x" + eh +
                             (_probeFailure != null ? " (game view path also failed: " + _probeFailure + ")" : "");
                if (_fidelityReasons.Add(why))
                {
                    Debug.LogError("[UICap-HL] GEOMETRY FIDELITY LOST for " + target.Tag +
                                   " (panel " + panelName + "): " + why +
                                   ". ElarionUiKit.PostScaleCanvasHeight will resolve this panel's zones " +
                                   "against THAT geometry, not " + target.Tag + ". The png is then " +
                                   "SCALE-ACCURATE ONLY -- font size reproduces, zone geometry does NOT. " +
                                   "Treat its filename as a label, not a layout.");
                }
            }

            public void Dispose()
            {
                // FIRST and unconditional: a leaked override would mis-size every panel built
                // later in this editor session (including the owner's own play-mode UI).
                ElarionUiKit.ClearSurfaceOverride();
                try
                {
                    if (_restore && _prevIndex >= 0) SelectIndex(_prevIndex);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[UICap-HL] game view size restore failed (harmless): " + e.Message);
                }
            }

            private static void Probe()
            {
                if (_probed) return;
                _probed = true;
                try
                {
                    var edAsm = typeof(EditorWindow).Assembly;
                    _sizesType = edAsm.GetType("UnityEditor.GameViewSizes");
                    _sizeType = edAsm.GetType("UnityEditor.GameViewSize");
                    _sizeTypeEnum = edAsm.GetType("UnityEditor.GameViewSizeType");
                    _gameViewType = edAsm.GetType("UnityEditor.GameView");
                    if (_sizesType == null || _sizeType == null || _sizeTypeEnum == null || _gameViewType == null)
                    {
                        _probeFailure = "UnityEditor internals not found (GameViewSizes/GameViewSize/" +
                                        "GameViewSizeType/GameView) -- this Unity version moved them";
                        return;
                    }

                    var singleton = typeof(ScriptableSingleton<>).MakeGenericType(_sizesType);
                    var instProp = singleton.GetProperty("instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _sizes = instProp != null ? instProp.GetValue(null, null) : null;
                    if (_sizes == null) { _probeFailure = "GameViewSizes.instance was null"; return; }

                    var groupProp = _sizesType.GetProperty("currentGroup",
                        BindingFlags.Public | BindingFlags.Instance);
                    _group = groupProp != null ? groupProp.GetValue(_sizes, null) : null;
                    if (_group == null) { _probeFailure = "GameViewSizes.currentGroup was null"; return; }
                    _groupType = _group.GetType();

                    // An EXISTING game view is preferred; GetWindow can be hostile in batchmode.
                    var open = Resources.FindObjectsOfTypeAll(_gameViewType);
                    if (open != null && open.Length > 0) _gameView = open[0] as EditorWindow;
                    if (_gameView == null)
                    {
                        try { _gameView = EditorWindow.GetWindow(_gameViewType, false, "Game", false); }
                        catch (Exception e) { _probeFailure = "no GameView and GetWindow threw (" + e.GetType().Name + ": " + e.Message + ")"; return; }
                    }
                    if (_gameView == null) { _probeFailure = "no GameView window exists and one could not be created"; return; }
                }
                catch (Exception e)
                {
                    _probeFailure = e.GetType().Name + ": " + e.Message;
                }
            }

            /// <summary>Index of the group's FixedResolution entry for w x h, adding it if absent.</summary>
            private static int IndexOf(int w, int h)
            {
                var totalM = _groupType.GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance);
                var getM = _groupType.GetMethod("GetGameViewSize",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (totalM == null || getM == null) return -1;

                var wProp = _sizeType.GetProperty("width", BindingFlags.Public | BindingFlags.Instance);
                var hProp = _sizeType.GetProperty("height", BindingFlags.Public | BindingFlags.Instance);
                if (wProp == null || hProp == null) return -1;

                int total = (int)totalM.Invoke(_group, null);
                for (int i = 0; i < total; i++)
                {
                    object s = getM.Invoke(_group, new object[] { i });
                    if (s == null) continue;
                    if ((int)wProp.GetValue(s, null) == w && (int)hProp.GetValue(s, null) == h) return i;
                }

                // Not present -> author it (FixedResolution, so the view is exactly w x h).
                var ctor = _sizeType.GetConstructor(new[] { _sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                var addM = _groupType.GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);
                if (ctor == null || addM == null) return -1;
                object created = ctor.Invoke(new object[]
                {
                    Enum.Parse(_sizeTypeEnum, "FixedResolution"), w, h, "UICap " + w + "x" + h,
                });
                addM.Invoke(_group, new[] { created });

                total = (int)totalM.Invoke(_group, null);
                for (int i = 0; i < total; i++)
                {
                    object s = getM.Invoke(_group, new object[] { i });
                    if (s == null) continue;
                    if ((int)wProp.GetValue(s, null) == w && (int)hProp.GetValue(s, null) == h) return i;
                }
                return -1;
            }

            private static int GetSelectedIndex()
            {
                var p = _gameViewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p == null || _gameView == null) return -1;
                try { return (int)p.GetValue(_gameView, null); }
                catch { return -1; }
            }

            private static bool Select(int w, int h)
            {
                int idx = IndexOf(w, h);
                if (idx < 0)
                {
                    _probeFailure = _probeFailure ?? ("could not find or author a " + w + "x" + h +
                                                      " FixedResolution game view size");
                    return false;
                }
                return SelectIndex(idx);
            }

            private static bool SelectIndex(int idx)
            {
                if (_gameView == null) return false;
                var m = _gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null)
                {
                    _probeFailure = _probeFailure ?? "GameView.SizeSelectionCallback not found";
                    return false;
                }
                m.Invoke(_gameView, new object[] { idx, null });
                _gameView.Repaint();
                // The size is applied on the view's next layout; force it now so the
                // Screen.* read in the ctor is the POST-change value, not the stale one.
                try { InternalEditorUtility_RepaintAllViews(); } catch { }
                return true;
            }

            private static void InternalEditorUtility_RepaintAllViews()
            {
                var t = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.InternalEditorUtility");
                var m = t != null ? t.GetMethod("RepaintAllViews", BindingFlags.Public | BindingFlags.Static) : null;
                if (m != null) m.Invoke(null, null);
            }
        }

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
            _fidelityOk = 0;
            _fidelityDegraded = 0;
            _fidelityReasons.Clear();
            _screenStuckBuilds = 0;
            _screenStuckAt = null;
            _geoMoveProof = null;
            _geoMoveFailure = null;
            _geoFailures.Clear();
            _geoCanvasesChecked = 0;
            _endStateFits.Clear();
            _endStateFitFailures.Clear();
            try
            {
                Directory.CreateDirectory(OutDir);
                Debug.Log("[UICap-HL] headless UI capture start (batchmode=" + Application.isBatchMode +
                          ", graphicsDevice=" + SystemInfo.graphicsDeviceType +
                          ", screen=" + Screen.width + "x" + Screen.height +
                          ", out=" + Path.GetFullPath(OutDir) + ")");

                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Debug.LogWarning("[UICap-HL] NO graphics device (looks like -nographics) -- pngs will be " +
                                     "BLANK. Re-run WITHOUT -nographics for real pixels.");
                }

                // BEFORE any panel is shot: prove the surface move actually changes resolved
                // geometry. If it does not, every png below shares one layout and the run is
                // worthless -- so this decides the fidelity marker (see ReportFidelity).
                ProveGeometryMoves();

                count += CaptureFoundingEchoCard();
                count += CapturePauseMenu();
                count += CaptureEchoRoster();
                count += CaptureEchoCard();          // WO-852: the resource-picker layout
                count += CaptureHelpMenu();
                count += CaptureDailyQuestHud();
                count += CaptureLoreReadingModal();
                count += CaptureTowerManagerPanel();
                count += CaptureBuildMenuUpgradeTower();
                count += CaptureRumorBoard();
                count += CaptureRealmMap();
                count += CaptureHeroSkillTree();
                count += CaptureQueueRail();         // WO-864: the CoC queue card rail
                count += CaptureBuildGhostChips();   // WO-1010 P1: chips on the ghost
                count += CapturePaletteCollapsed();  // WO-1010 P2: dock open + collapsed w/ restore tab
                count += CaptureEndStateWaveClear(); // WO-952: the wave-clear banner's fit, MEASURED
                count += CaptureDialogueOptions(); // WO-1030 DialogueView options state (2-opt + 4-opt worst case); WO-1031 re-pointed off the deleted pet_engage builder

                // RAID PILLAR (2026-08-16). Sixteen cases covered everything EXCEPT the one
                // pillar the owner asked to verify from screenshots. All three are defensive:
                // a raid screen that refuses to open logs and returns 0, it never throws the
                // run away.
                count += CaptureRaidSelection();     // the grid that was hard-refusing to open
                count += CaptureRaidDeploy();        // the pre-raid deploy screen (never shot before)
                count += CaptureRaidsFaceStates();   // WO-1008: the bar face live / 0-of-cap / partial

                Debug.Log("[UICap-HL] done -> " + Path.GetFullPath(OutDir));
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] capture run threw: " + e);
            }

            // Three DISTINCT markers (CLAUDE.md §8: one marker per entry point, never a
            // shared string -- that is how a 22-case pass once read as a full-suite pass).
            ReportFidelity();
            ReportGeometry();
            ReportEndStateFit();

            // The marker a headless caller greps to confirm the run produced pixels.
            Debug.Log("UI_CAPTURE_OK " + count);
        }

        /// <summary>Did every panel get BUILT at the size it was SHOT at?</summary>
        private static void ReportFidelity()
        {
            int total = _fidelityOk + _fidelityDegraded;

            // The residual, stated plainly. The kit surface moved; the PROCESS's screen did not
            // (batchmode has no game view). Everything resolving through ElarionUiKit is
            // therefore target-accurate; a panel that reads Screen.* DIRECTLY at build time is
            // not. Naming it is the difference between a known limit and a fresh silent lie.
            string residual = _screenStuckBuilds > 0
                ? " RESIDUAL: Screen.width/height itself never moved (stuck at " + _screenStuckAt +
                  " across " + _screenStuckBuilds + " builds) -- `-batchmode` builds no game view for " +
                  "it to mirror, with or without a graphics device. Zones resolved through " +
                  "ElarionUiKit (PostScaleCanvasHeight / SurfaceWidth / SurfaceHeight) ARE the " +
                  "target's; any panel still reading Screen.* directly at build time is not, and " +
                  "must be routed through the kit surface before its shot can be trusted."
                : "";

            if (total == 0)
            {
                Debug.LogError("UI_CAPTURE_FIDELITY_DEGRADED 0/0 builds -- no capture ran at all" + residual);
                return;
            }

            // The divergence proof gates the whole run: if two different aspects resolve the SAME
            // zone rects then every png shares one geometry, whatever the per-build checks said.
            if (_geoMoveProof == null)
            {
                Debug.LogError("UI_CAPTURE_FIDELITY_DEGRADED " + total + "/" + total +
                               " builds -- the harness could NOT prove that changing the target " +
                               "changes the resolved layout, so every png in this run must be treated " +
                               "as SCALE-ACCURATE ONLY (font size reproduces, zone geometry does NOT; " +
                               "the resolution in the filename is a LABEL, not a layout). Reason: " +
                               (_geoMoveFailure ?? "the aspect-divergence proof did not run") + residual);
                return;
            }

            if (_fidelityDegraded == 0)
            {
                Debug.Log("UI_CAPTURE_FIDELITY_OK " + _fidelityOk + " builds -- every panel was " +
                          "constructed with the kit surface at its target resolution, so the zone " +
                          "geometry in each png is that resolution's real geometry. PROOF: " +
                          _geoMoveProof + residual);
                return;
            }

            Debug.LogError("UI_CAPTURE_FIDELITY_DEGRADED " + _fidelityDegraded + "/" + total +
                           " builds -- the surface would not move to the target, so those " +
                           "pngs are SCALE-ACCURATE ONLY (font size reproduces, zone geometry does " +
                           "NOT; the resolution in the filename is a LABEL, not a layout). Reasons: " +
                           string.Join(" | ", new List<string>(_fidelityReasons).ToArray()) + residual);
        }

        // =====================================================================
        //  ProveGeometryMoves -- the run's own proof that the fix is working.
        // ---------------------------------------------------------------------
        //  A capture harness can claim any resolution it likes in a filename. The only
        //  thing that makes the claim MEAN something is that two targets with different
        //  ASPECTS resolve to DIFFERENT zone rects. Before today they did not -- one
        //  geometry wore three filenames all night and a caption rendering off its plate
        //  was invisible.
        //
        //  So the run measures it, on the real kit path: build the real Obsidian modal
        //  under each of the two most-different aspects in the matrix (today 1920x1080,
        //  aspect 1.778, and the Seeker's 2670x1200, aspect 2.225) and compare the
        //  resolved layout.body zone. Identical => the whole run is scale-only and
        //  ReportFidelity says so at ERROR severity. This is a FAILURE, never a warning:
        //  a warning is indistinguishable from the silence that shipped the defect.
        //
        //  Computed from the zone's RESOLVED anchors and PostScaleCanvasHeight rather than
        //  a live rect -- the overlay canvas's own rect is still the editor's 640x480
        //  (nothing can move that in batchmode), and it is not what the panel's fraction
        //  anchors resolve against anyway. Both axes are expressed in canvas-HEIGHT units
        //  so that every number in the proof is one the KIT produced (see MeasureZones).
        // =====================================================================
        private static readonly Vector2 ProbePanelMin = new Vector2(0.22f, 0.10f);
        private static readonly Vector2 ProbePanelMax = new Vector2(0.78f, 0.90f);

        private struct ZoneProbe
        {
            public string Tag;
            public float CanvasH;
            public Rect Body;
        }

        private static void ProveGeometryMoves()
        {
            _geoMoveProof = null;
            _geoMoveFailure = null;

            // The two most-different aspects in the matrix (never a hard-coded pair -- if the
            // matrix changes, the proof follows it).
            CaptureTarget lo = default(CaptureTarget), hi = default(CaptureTarget);
            float loAr = float.MaxValue, hiAr = float.MinValue;
            foreach (var t in LandscapeTargets)
            {
                float ar = (float)t.W / Mathf.Max(1, t.H);
                if (ar < loAr) { loAr = ar; lo = t; }
                if (ar > hiAr) { hiAr = ar; hi = t; }
            }
            if (Mathf.Abs(hiAr - loAr) < 0.01f)
            {
                _geoMoveFailure = "the capture matrix carries only ONE aspect ratio, so nothing in " +
                                  "this run can demonstrate that geometry follows the target. Keep at " +
                                  "least one 16:9-class target and the Seeker's 2670x1200 (2.225).";
                Debug.LogError("[UICap-HL] " + _geoMoveFailure);
                return;
            }

            ZoneProbe a, b;
            if (!MeasureZones(lo, out a) || !MeasureZones(hi, out b))
            {
                _geoMoveFailure = "the zone probe could not be measured at both aspects (see the " +
                                  "[UICap-HL] error above), so the run cannot claim geometry moved";
                return;
            }

            bool moved = Mathf.Abs(a.CanvasH - b.CanvasH) > 0.5f || RectDiffers(a.Body, b.Body, 0.5f);
            if (!moved)
            {
                _geoMoveFailure = "aspect " + loAr.ToString("0.###") + " (" + a.Tag + ") and aspect " +
                                  hiAr.ToString("0.###") + " (" + b.Tag + ") resolved the IDENTICAL " +
                                  "layout.body zone " + RectStr(a.Body) + " at canvas height " +
                                  a.CanvasH.ToString("0.#") + " -- i.e. the surface move is not " +
                                  "reaching ElarionUiKit.PostScaleCanvasHeight and every png in this " +
                                  "run shares one geometry, exactly as before the 2026-08-05 fix";
                Debug.LogError("[UICap-HL] GEOMETRY DID NOT MOVE: " + _geoMoveFailure);
                return;
            }

            _geoMoveProof = a.Tag + " (aspect " + loAr.ToString("0.###") + ") resolves layout.body " +
                            RectStr(a.Body) + " at canvas height " + a.CanvasH.ToString("0.#") +
                            " ref px, while " + b.Tag + " (aspect " + hiAr.ToString("0.###") +
                            ") resolves " + RectStr(b.Body) + " at " + b.CanvasH.ToString("0.#") +
                            " (rects in canvas-height units, all kit-derived) -- different targets, " +
                            "genuinely different layouts";
            Debug.Log("[UICap-HL] geometry-moves proof: " + _geoMoveProof);
        }

        /// <summary>Build the real kit modal under <paramref name="target"/> and measure its
        /// resolved body zone in kit reference px. Returns false (loudly) if it cannot.</summary>
        private static bool MeasureZones(CaptureTarget target, out ZoneProbe probe)
        {
            probe = default(ZoneProbe);
            ElarionUiKit.ObsidianModal modal = null;
            try
            {
                ElarionUiKit.SetSurfaceOverride(target.W, target.H);
                if (ElarionUiKit.SurfaceWidth != target.W || ElarionUiKit.SurfaceHeight != target.H)
                {
                    Debug.LogError("[UICap-HL] zone probe: ElarionUiKit surface refused " + target.Tag +
                                   " (reports " + ElarionUiKit.SurfaceWidth + "x" +
                                   ElarionUiKit.SurfaceHeight + "). The injectable surface is the ONE " +
                                   "lever this harness has left -- without it every shot is scale-only.");
                    return false;
                }

                modal = ElarionUiKit.BuildObsidianModal("~UICapZoneProbe", "Zone Probe",
                    ProbePanelMin, ProbePanelMax, null, 100);
                if (modal == null || modal.chrome == null || modal.chrome.content == null
                    || modal.chrome.layout == null || modal.chrome.layout.body == null)
                {
                    Debug.LogError("[UICap-HL] zone probe: BuildObsidianModal produced no layout.body " +
                                   "at " + target.Tag + " -- the probe measures the kit's real chrome, " +
                                   "so a null here means the kit path itself did not build.");
                    return false;
                }

                float canvasH = ElarionUiKit.PostScaleCanvasHeight(modal.chrome.content.transform);

                // EVERY number below comes from the KIT (this height + the zone's resolved
                // anchors). The x axis is deliberately expressed in canvas-HEIGHT units rather
                // than a width derived from the target's own aspect: a self-derived width would
                // make the two rects differ even if the kit had ignored the surface completely,
                // i.e. a proof that proves itself. This way the rects can only diverge because
                // the kit's own build-time geometry diverged.
                float canvasW = canvasH;

                // The body zone's resolved rect: its anchors within the panel, the panel's
                // anchors within the canvas, the canvas in reference units.
                var body = modal.chrome.layout.body;
                float px0 = ProbePanelMin.x * canvasW, px1 = ProbePanelMax.x * canvasW;
                float py0 = ProbePanelMin.y * canvasH, py1 = ProbePanelMax.y * canvasH;
                float pw = px1 - px0, ph = py1 - py0;
                probe = new ZoneProbe
                {
                    Tag = target.Tag,
                    CanvasH = canvasH,
                    Body = new Rect(px0 + body.anchorMin.x * pw,
                                    py0 + body.anchorMin.y * ph,
                                    (body.anchorMax.x - body.anchorMin.x) * pw,
                                    (body.anchorMax.y - body.anchorMin.y) * ph),
                };
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] zone probe threw at " + target.Tag + ": " + e);
                return false;
            }
            finally
            {
                if (modal != null && modal.canvas != null)
                    UnityEngine.Object.DestroyImmediate(modal.canvas);
                ElarionUiKit.ClearSurfaceOverride();
            }
        }

        private static bool RectDiffers(Rect a, Rect b, float eps)
        {
            return Mathf.Abs(a.xMin - b.xMin) > eps || Mathf.Abs(a.yMin - b.yMin) > eps
                || Mathf.Abs(a.width - b.width) > eps || Mathf.Abs(a.height - b.height) > eps;
        }

        // ---------------------------------------------------------------------
        //  Panel: the founding / Echo unlock card (EchoUnlockDialogue) at its
        //  LONGEST copy -- the founding echo Aldwin. This is the panel with the
        //  just-fixed text/button overlap the owner cares about. We render both
        //  the flavor state and the "Tell me more" LORE state (the worst-case
        //  copy), at EVERY capture target (including the Seeker's real 2670x1200),
        //  so any overlap shows as it does on device.
        //
        //  BUILT ONCE PER TARGET (2026-08-05). This card is the exact panel the old
        //  build-once/re-scale harness lied about: it was "captured at two sizes" and
        //  green all night while its caption rendered off the black plate on device,
        //  because both pngs carried the SAME zone geometry.
        // ---------------------------------------------------------------------
        private static int CaptureFoundingEchoCard()
        {
            return ForEachTarget("EchoUnlockDialogue", CaptureFoundingEchoCardOnce);
        }

        private static int CaptureFoundingEchoCardOnce(CaptureTarget target)
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

                // Build the REAL beat via its own public entry (data-driven, guarded).
                // OWNER RULING 2026-08-05 ("it should just simply be one screen"): the WO-831
                // EMERGENCE state is GONE -- its headline, arrival line, artwork and fade are
                // folded into the single awakening card. So there is no _emergenceCanvas to
                // capture and no OnEmergenceContinue advance to drive; Show() lands directly on
                // the one screen. Two states remain to capture: flavor and lore.
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
                    return saved;
                }

                // -- FLAVOR state (the default awaken copy) --
                if (RenderCanvasToPng(canvasGo,
                    OutDir + "EchoUnlockDialogue_Aldwin_flavor_" + target.Tag + ".png",
                    target.W, target.H)) saved++;

                // -- LORE state (the LONGEST copy, swapped in by "Tell me more") --
                // OnTellMore is private; invoking it mirrors the real button so we capture
                // exactly what the owner sees after tapping "Tell me more".
                InvokePrivate(dlg, "OnTellMore");
                if (RenderCanvasToPng(canvasGo,
                    OutDir + "EchoUnlockDialogue_Aldwin_lore_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
        //  Panel: DialogueView in its OPTIONS state (WO-1030: 'Repair structures'
        //  was sliced by the panel edge in landscape; the option band is now
        //  reserved FIRST and the text well scrolls). Shot at every landscape
        //  target (including the Seeker's real 2670x1200) in two flavors:
        //    * a 2-option node -- the common case, which must fit scroll-free;
        //    * a 4-option node -- the many-option worst case that must scroll with
        //      a visible affordance, never silently clip.
        //
        //  WO-1031: these used to shoot PetTaskController.BuildEngageDef("ice-wolf")
        //  (speaker "Frost"). That prompt is DELETED -- the guide wolf is Echo #1,
        //  Aldwin, and tasking lives in the Echo tab. The capture is now driven by
        //  SYNTHETIC probe defs, because what it proves is the SHARED DialogueView
        //  layout (canon reference implementation, UI_BLINK_TEMPLATE_CANON.md sec. 8)
        //  which every conversation in the game depends on -- so the WO-1030 fix
        //  keeps its screenshot coverage after the pet prompt's removal.
        //
        //  DialogueView lives in DeNelle.HUD, which DeNelle.Editor does NOT
        //  reference, so the view is resolved by reflection (like PauseController).
        //  Its OnEnable/OnDisable are invoked explicitly: edit mode never calls
        //  MonoBehaviour lifecycle on AddComponent, and the Opened subscription is
        //  how the view builds its panel when DialogueService.PlayDef fires.
        // ---------------------------------------------------------------------
        private static int CaptureDialogueOptions()
        {
            return ForEachTarget("DialogueOptions", CaptureDialogueOptionsOnce);
        }

        private static int CaptureDialogueOptionsOnce(CaptureTarget target)
        {
            int saved = 0;
            saved += CaptureDialogueDefOnce(target, BuildOptionProbeDef(2),
                "DialogueOptions_2opt");
            saved += CaptureDialogueDefOnce(target, BuildOptionProbeDef(4),
                "DialogueOptions_4opt");
            return saved;
        }

        /// <summary>An n-option probe node (WO-1030 acceptance: 2 options fit scroll-free;
        /// a 4-option node either fits or scrolls with a visible affordance). One builder so
        /// the two shots differ ONLY by option count. Speaker is a catalog speaker, never an
        /// invented name (WO-1031: no species -> name table exists any more).</summary>
        private static DeNelle.Core.Dialogue.DialogueDef BuildOptionProbeDef(int optionCount)
        {
            string[] labels =
            {
                "Gather resources",
                "Repair structures",
                "Stand watch at the gate",
                "Rest by the Heart of Elarion",
            };
            var def = new DeNelle.Core.Dialogue.DialogueDef
            {
                Id = "uicap_dialogue_" + optionCount + "opt",
                StartNode = "root",
            };
            var options = new List<DeNelle.Core.Dialogue.DialogueOption>();
            for (int i = 0; i < optionCount && i < labels.Length; i++)
                options.Add(new DeNelle.Core.Dialogue.DialogueOption { Text = labels[i], Goto = "" });

            def.Nodes.Add(new DeNelle.Core.Dialogue.DialogueNode
            {
                Id = "root",
                Lines = new List<DeNelle.Core.Dialogue.DialogueLine>
                {
                    new DeNelle.Core.Dialogue.DialogueLine
                    {
                        Speaker = "Your Echo",
                        Text = "Keeper, the option-layout probe: every choice below must stay reachable.",
                    },
                },
                Options = options,
            });
            return def;
        }

        private static int CaptureDialogueDefOnce(CaptureTarget target,
            DeNelle.Core.Dialogue.DialogueDef def, string shotName)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject viewGo = null;
            GameObject uiGo = null;
            Component view = null;
            try
            {
                Type viewType = ResolveType("DeNelle.HUD.DialogueView");
                if (viewType == null)
                {
                    Debug.LogWarning("[UICap-HL] DeNelle.HUD.DialogueView type not found -- " +
                                     shotName + " skipped.");
                    return 0;
                }
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                viewGo = new GameObject("~UICapDialogueView");
                view = viewGo.AddComponent(viewType);
                // Edit mode calls NO lifecycle on AddComponent -- wire the Opened subscription
                // the way play mode would, so PlayDef below reaches this view.
                InvokePrivate(view, "OnEnable");

                if (!DeNelle.Core.Dialogue.DialogueService.PlayDef(def))
                {
                    Debug.LogWarning("[UICap-HL] DialogueService.PlayDef refused the def -- " +
                                     shotName + " skipped.");
                    return 0;
                }

                // The prompt is line -> OPTIONS; advance once so the CHOICE LIST (the WO-1030
                // defect surface) is the state in the png.
                var vm = DeNelle.Core.Dialogue.DialogueService.ActiveVm;
                if (vm != null && !vm.ShowingOptions) vm.Advance();
                if (vm == null || !vm.ShowingOptions)
                {
                    Debug.LogWarning("[UICap-HL] dialogue VM never reached ShowingOptions -- " +
                                     shotName + " captures the non-options state instead.");
                }

                uiGo = GetPrivateGameObject(view, "_ui");
                if (uiGo == null)
                {
                    Debug.LogWarning("[UICap-HL] DialogueView._ui was null -- panel did not build; " +
                                     shotName + " skipped.");
                    return saved;
                }
                if (RenderCanvasToPng(uiGo, OutDir + shotName + "_" + target.Tag + ".png",
                                      target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] " + shotName + " capture threw: " + e);
            }
            finally
            {
                // Teardown WITHOUT the view's runtime Destroy path (Destroy errors in edit
                // mode): null the private _ui first so the view's close handler skips it, close
                // the VM (unbinds handlers + releases the panel arbiter via NotifyClosed), then
                // detach the static Opened subscription and DestroyImmediate the orphans.
                try
                {
                    if (view != null) SetPrivateField(view, "_ui", null);
                    DeNelle.Core.Dialogue.DialogueService.Stop();
                    if (view != null) InvokePrivate(view, "OnDisable");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[UICap-HL] dialogue capture teardown: " + e.Message);
                }
                if (uiGo != null) UnityEngine.Object.DestroyImmediate(uiGo);
                if (viewGo != null) UnityEngine.Object.DestroyImmediate(viewGo);
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
            return ForEachTarget("PauseMenu", CapturePauseMenuOnce);
        }

        private static int CapturePauseMenuOnce(CaptureTarget target)
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

                if (RenderCanvasToPng(canvasGo, OutDir + "PauseMenu_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
            return ForEachTarget("EchoPetButton", CaptureEchoRosterOnce);
        }

        private static int CaptureEchoRosterOnce(CaptureTarget target)
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

                if (RenderCanvasToPng(pipCanvas, OutDir + "EchoPetButton_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
            return ForEachTarget("HelpMenu", CaptureHelpMenuOnce);
        }

        private static int CaptureHelpMenuOnce(CaptureTarget target)
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

                if (RenderCanvasToPng(canvasGo, OutDir + "HelpMenu_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
            return ForEachTarget("DailyQuestHud", CaptureDailyQuestHudOnce);
        }

        private static int CaptureDailyQuestHudOnce(CaptureTarget target)
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

                if (RenderCanvasToPng(canvasGo, OutDir + "DailyQuestHud_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
            return ForEachTarget("LoreReadingModal", CaptureLoreReadingModalOnce);
        }

        private static int CaptureLoreReadingModalOnce(CaptureTarget target)
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
                if (RenderCanvasToPng(canvasGo, OutDir + "LoreReadingModal_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
            return ForEachTarget("TowerManagerPanel", CaptureTowerManagerPanelOnce);
        }

        private static int CaptureTowerManagerPanelOnce(CaptureTarget target)
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

                if (RenderCanvasToPng(canvasGo, OutDir + "TowerManagerPanel_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
            return ForEachTarget("BuildMenuUpgradeTower", CaptureBuildMenuUpgradeTowerOnce);
        }

        private static int CaptureBuildMenuUpgradeTowerOnce(CaptureTarget target)
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

                // 2026-08-04: the stubs used to be bare GameObject + AddComponent<Tower>, i.e.
                // towers with NO TowerData. That is the state a tower is in while the crew is
                // still raising it -- it has no level, no stats and no upgrade price -- so the
                // shot showed an un-authored tower rather than the upgrade screen. Seed the REAL
                // shipped ArcherTower asset (authored 100/220 upgrade costs) so the capture
                // exercises the priced path with real numbers. _data is set directly instead of
                // through Tower.Initialize because Initialize instantiates the level visual and
                // wires combat -- scene side effects this throwaway host must not create.
                string[] seedIds = { "Towers/ArcherTower", "Towers/FrostTower", "Towers/MageTower" };
                int seeded = 0;
                for (int i = 0; i < stubTowers.Length; i++)
                {
                    var seedData = Resources.Load<DeNelle.Core.Data.TowerData>(seedIds[i % seedIds.Length]);
                    if (seedData == null) continue;
                    SetPrivateField(stubTowers[i].GetComponent<Tower>(), "_data", seedData);
                    seeded++;
                }
                if (seeded == 0)
                    Debug.LogWarning("[UICap-HL] no TowerData under Resources/Towers -- the upgrade-tower " +
                                     "shot will render the still-under-construction state.");

                hostGo = new GameObject("~UICapBuildMenu");
                var menu = hostGo.AddComponent<BuildMenu>();

                InvokePrivate(menu, "EnsureBuilt");

                // Public injectable ctor: (economy, towers, wallRepair, fallbackCrystals,
                // onClose). A FUNDED ledger is injected so the shot shows the affordable CTA;
                // with a null economy the VM correctly reports 0 wood/iron on hand and the
                // button would read its (equally real) "Not enough Wood" state instead.
                vm = new BuildMenuVM(new CaptureLedger(400), PlacedTowerListVM.CreateDefault(null), null, 500, null);
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

                if (RenderCanvasToPng(canvasGo, OutDir + "BuildMenuUpgradeTower_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
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
        //  Panel: Brom's Rumor Board (RumorBoardPanel, DeNelle.Village.Hero --
        //  referenced, so direct types + private-field reflection only). WO-810
        //  rebuilt it as a master-detail board (chip strip / card list / detail
        //  pane + pinned CTA); this shot covers the review's stacked-layout risk.
        //
        //  Open() is the panel's ONLY build entry: it registers the arbiter
        //  handle, creates the LIVE VM (RumorBoardVM.CreateDefault) and paints
        //  once. For a DETERMINISTIC worst case we then swap in a VM built over
        //  the panel's own injectable backend seam (IRumorBoardBackend): 15
        //  rumors (3 in progress, one tracked; 12 available) and one rumor whose
        //  hook -- the detail body's variable text -- is the longest prose the
        //  pane must carry, plus a full multi-part rewards row. That rumor is
        //  pre-selected so the detail pane renders the worst body + Accept CTA.
        //
        //  EDIT-SAFE REPAINT: Repaint()'s ClearContent and RenderDetail call
        //  runtime Destroy on the FIRST paint's children (edit-illegal), so we
        //  DestroyImmediate the list children + the CTA ourselves (and null the
        //  CTA field) before invoking Repaint -- the repaint then runs
        //  Destroy-free (tower-manager parking recipe, applied as a pre-clear).
        //
        //  PORTRAIT: Open() picks the stacked-vs-split geometry at BUILD time from
        //  the KIT SURFACE (ElarionUiKit.SurfaceHeight > SurfaceWidth -- it used to
        //  read Screen.* directly, which batchmode can never move). The portrait
        //  targets below are BUILT under a portrait CaptureSurfaceScope, so Open()
        //  takes that branch ITSELF. The authored portrait anchors are still
        //  re-applied after the paint: they are the same values Open writes (so a
        //  no-op when the scope worked), and they keep the shot honest if the
        //  surface ever refused to move (UI_CAPTURE_FIDELITY_DEGRADED).
        // ---------------------------------------------------------------------
        private static readonly CaptureTarget[] RumorBoardTargets =
        {
            new CaptureTarget(1920, 1080),
            new CaptureTarget(2340, 1080),
            new CaptureTarget(2670, 1200),   // Seeker landscape
            new CaptureTarget(1080, 2340),
            new CaptureTarget(1200, 2670),   // Seeker portrait
        };

        private static int CaptureRumorBoard()
        {
            return ForEachTarget("RumorBoard", RumorBoardTargets, CaptureRumorBoardOnce);
        }

        private static int CaptureRumorBoardOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;
            RumorBoardPanel panel = null;
            RumorBoardVM worstVm = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                hostGo = new GameObject("~UICapRumorBoard");
                panel = hostGo.AddComponent<RumorBoardPanel>();

                // The real build path (chrome + chips + list + detail + status).
                panel.Open();

                canvasGo = GetPrivateGameObject(panel, "_ui");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] RumorBoardPanel._ui null after Open -- rumor board skipped.");
                    return 0;
                }

                // Swap the live VM for the worst-case VM (injectable backend seam).
                var liveVm = GetPrivateFieldValue(panel, "_vm") as RumorBoardVM;
                if (liveVm != null) liveVm.Dispose();   // also detaches the panel's Changed -> Repaint hook
                worstVm = new RumorBoardVM(new WorstCaseRumorBackend(), null);
                SetPrivateField(panel, "_vm", worstVm);
                SetPrivateField(panel, "_selectedId", WorstCaseRumorBackend.LongestBodyId);

                // Pre-clear the first paint with DestroyImmediate so the repaint below
                // makes zero runtime-Destroy calls (edit-mode contract).
                var contentRoot = GetPrivateGameObject(panel, "_contentRoot");
                if (contentRoot != null)
                {
                    for (int i = contentRoot.transform.childCount - 1; i >= 0; i--)
                    {
                        var ch = contentRoot.transform.GetChild(i);
                        if (ch != null) UnityEngine.Object.DestroyImmediate(ch.gameObject);
                    }
                }
                var ctaGo = GetPrivateGameObject(panel, "_detailCtaGo");
                if (ctaGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(ctaGo);
                    SetPrivateField(panel, "_detailCtaGo", null);
                }

                InvokePrivate(panel, "Repaint");   // worst-case list + longest detail body + Accept CTA

                if (target.H > target.W)
                {
                    // PORTRAIT: re-assert the authored portrait anchors (the ONLY delta of the
                    // portrait branch in Open). A no-op when the portrait scope worked and Open
                    // already took that branch; the honest correction when it did not.
                    RectTransform listViewport = null;
                    foreach (var srScroll in canvasGo.GetComponentsInChildren<ScrollRect>(true))
                    {
                        if (srScroll != null && srScroll.vertical && !srScroll.horizontal
                            && srScroll.gameObject.name == "Viewport")
                        {
                            listViewport = (RectTransform)srScroll.transform;
                            break;
                        }
                    }
                    var detailPane = GetPrivateFieldValue(panel, "_detailPane") as RectTransform;
                    if (listViewport != null && detailPane != null)
                    {
                        listViewport.anchorMin = new Vector2(0.03f, 0.48f);
                        listViewport.anchorMax = new Vector2(0.97f, 0.855f);
                        detailPane.anchorMin = new Vector2(0.05f, 0.05f);
                        detailPane.anchorMax = new Vector2(0.95f, 0.46f);
                    }
                    else
                    {
                        Debug.LogWarning("[UICap-HL] rumor board portrait anchors not re-applied -- list "
                                         + "viewport or detail pane not found (listViewport="
                                         + (listViewport != null) + ", detailPane="
                                         + (detailPane != null) + ").");
                    }
                    if (RenderCanvasToPng(canvasGo, OutDir + "RumorBoard_" + target.Tag + ".png",
                        target.W, target.H)) saved++;
                    return saved;
                }

                if (RenderCanvasToPng(canvasGo, OutDir + "RumorBoard_" + target.Tag + ".png",
                    target.W, target.H)) saved++;

                // WO-810 follow-up: the DAILY tab — its rows carry raw "{target}" authored
                // labels resolved via DailyQuestCatalog.ResolveLabel, so this shot pixel-
                // proves the substitution the F8 flagged. Repaint is edit-safe now
                // (RumorBoardPanel.SafeDestroy picks DestroyImmediate outside Play), so no
                // extra pre-clear pass is needed for these repaints.
                worstVm.SetTab("daily");
                InvokePrivate(panel, "Repaint");
                if (RenderCanvasToPng(canvasGo, OutDir + "RumorBoard_daily_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] rumor board capture threw: " + e);
            }
            finally
            {
                // Open() registered + NotifyOpened the arbiter handle; clear it by hand
                // (the panel's own Close() uses runtime Destroy -- edit-illegal).
                try
                {
                    if (panel != null)
                    {
                        var handle = GetPrivateFieldValue(panel, "_handle") as PanelHandle;
                        if (handle != null) PanelManager.NotifyClosed(handle);
                    }
                }
                catch (Exception pe)
                {
                    Debug.LogWarning("[UICap-HL] rumor board arbiter release failed (harmless): " + pe.Message);
                }
                if (worstVm != null) worstVm.Dispose();
                // Canvas FIRST so any later OnDestroy sees a dead _ui (edit-mode teardown contract).
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: the Realm Map parchment overworld (WO-826). Open() builds the
        //  real code-built panel from the dual-copy realm-map.json; with no
        //  GameStateService alive in edit mode the VM's live source reads a
        //  fresh save (BestWave 0, empty ledger), so the shot shows exactly the
        //  acceptance state: home Elarion selected + every region as LOCKED fog.
        //  Open() makes zero runtime-Destroy calls on a first build (empty node
        //  host, no CTA yet), so no DestroyImmediate pre-clear is needed.
        // ---------------------------------------------------------------------
        private static int CaptureRealmMap()
        {
            return ForEachTarget("RealmMap", CaptureRealmMapOnce);
        }

        private static int CaptureRealmMapOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;
            RealmMapPanel panel = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                hostGo = new GameObject("~UICapRealmMap");
                panel = hostGo.AddComponent<RealmMapPanel>();

                // The real build path (parchment plate + nodes + detail + disabled Travel CTA).
                panel.Open();

                canvasGo = GetPrivateGameObject(panel, "_ui");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] RealmMapPanel._ui null after Open -- realm map skipped.");
                    return 0;
                }

                // Landscape at every capture target -- and now genuinely BUILT at each one
                // (the panel used to be authored once under the editor's own Screen).
                if (RenderCanvasToPng(canvasGo, OutDir + "RealmMap_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] realm map capture threw: " + e);
            }
            finally
            {
                // Open() registered + NotifyOpened the arbiter handle; clear it by hand
                // (the panel's own Close() uses runtime Destroy -- edit-illegal).
                try
                {
                    if (panel != null)
                    {
                        var handle = GetPrivateFieldValue(panel, "_handle") as PanelHandle;
                        if (handle != null) PanelManager.NotifyClosed(handle);
                    }
                }
                catch (Exception pe)
                {
                    Debug.LogWarning("[UICap-HL] realm map arbiter release failed (harmless): " + pe.Message);
                }
                // Canvas FIRST so any later OnDestroy sees a dead _ui (edit-mode teardown contract).
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Hero talent graph + persistent four-slot hot-swap rail. This is built
        //  from the real live-class VM; the edit-mode fallback is Knight, matching
        //  first-run behavior when no gameplay GameState exists.
        // ---------------------------------------------------------------------
        private static int CaptureHeroSkillTree()
        {
            return ForEachTarget("HeroSkillTree", CaptureHeroSkillTreeOnce);
        }

        private static int CaptureHeroSkillTreeOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject canvasGo = null;
            HeroSkillTreePanelMvvm panel = null;
            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }
                hostGo = new GameObject("~UICapHeroSkillTree");
                panel = hostGo.AddComponent<HeroSkillTreePanelMvvm>();
                panel.Open();
                canvasGo = GetPrivateGameObject(panel, "_ui");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] HeroSkillTreePanelMvvm._ui null after Open -- skipped.");
                    return 0;
                }
                if (RenderCanvasToPng(canvasGo,
                    OutDir + "HeroSkillTree_" + target.Tag + ".png", target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] hero skill tree capture threw: " + e);
            }
            finally
            {
                try
                {
                    if (panel != null)
                    {
                        var handle = GetPrivateFieldValue(panel, "_panelHandle") as PanelHandle;
                        if (handle != null) PanelManager.NotifyClosed(handle);
                    }
                }
                catch (Exception pe)
                {
                    Debug.LogWarning("[UICap-HL] hero skill tree arbiter release failed: " + pe.Message);
                }
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }
            return saved;
        }

        // ---------------------------------------------------------------------
        //  Worst-case rumor-board backend: the deterministic fixture behind the
        //  RumorBoard shots. 15 rumors overflow the WO-795 list well; the
        //  pre-selected rumor carries the longest hook (= the detail body's
        //  variable prose) plus a full rewards row (crystals/food/magic + items).
        // ---------------------------------------------------------------------
        private sealed class WorstCaseRumorBackend : IRumorBoardBackend
        {
            public const string LongestBodyId = "uicap_rumor_longest";

            private readonly List<QuestDef> _defs = new List<QuestDef>();
            private readonly HashSet<string> _activeIds = new HashSet<string>();

            public WorstCaseRumorBackend()
            {
                // Three in progress (the first tracked) -- the In Progress section renders populated.
                for (int i = 1; i <= 3; i++)
                {
                    string id = "uicap_rumor_active" + i;
                    _defs.Add(MakeRumor(id, "Standing Watch Over the Western Fields " + i, "story",
                        "Hold the western fields until the lantern wardens return from the ridge.",
                        40, 20, 0, null));
                    _activeIds.Add(id);
                }

                // The longest-body rumor: the detail pane's worst case. Its hook is the
                // variable prose RenderDetail folds into the body, so make it long, and give
                // it every reward part so the rewards row is at its widest.
                _defs.Add(MakeRumor(LongestBodyId,
                    "The Long Letter from the Drowned Archive of Old Elarion", "endgame",
                    "Brom unfolds a letter soaked through and dried twice over. The archivist of the "
                    + "drowned wing writes that the shelves beneath the reservoir have begun to sing at "
                    + "dusk, a low note that loosens the mortar and wakes the lantern eels. She asks for "
                    + "a steady hand to carry the sealed ledger past the flooded stair, to count the "
                    + "black candles left burning in the reading room nobody has entered since the "
                    + "founding, and to bring back whichever page the water refuses to touch. The road "
                    + "there crosses both gates, the old orchard, and the culvert the masons swear was "
                    + "never on any drawing of the village.",
                    220, 90, 45, "relic_drowned_ledger"));

                // Eleven more available rumors -> 15 total on the All tab (list-well overflow).
                for (int i = 1; i <= 11; i++)
                {
                    _defs.Add(MakeRumor("uicap_rumor_avail" + i,
                        "Rumor of the " + Ordinal(i) + " Bell That Rings Itself", (i % 3 == 0) ? "gear" : "story",
                        "Track down why the " + Ordinal(i).ToLowerInvariant()
                        + " bell rings with nobody on the rope, and quiet it before nightfall.",
                        10 + i, i, 0, null));
                }
            }

            private static string Ordinal(int i)
            {
                string[] names = { "First", "Second", "Third", "Fourth", "Fifth", "Sixth",
                                   "Seventh", "Eighth", "Ninth", "Tenth", "Eleventh" };
                return i >= 1 && i <= names.Length ? names[i - 1] : i.ToString();
            }

            private static QuestDef MakeRumor(string id, string title, string type, string hook,
                                              int crystals, int food, int magic, string itemId)
            {
                var def = new QuestDef { Id = id, Title = title, Type = type };
                def.Stages.Add(new QuestStage
                {
                    StageId = id + "_s1",
                    ObjectiveText = hook,
                    Reward = new QuestReward { Crystals = crystals, Food = food, Magic = magic, GrantItemId = itemId },
                });
                return def;
            }

            // -- IRumorBoardBackend ------------------------------------------------
            public IReadOnlyList<QuestDef> Catalog => _defs;
            public bool Ready => true;
            public bool IsActive(string id) => id != null && _activeIds.Contains(id);
            public bool IsCompleted(string id) => false;
            public string ObjectiveFor(string id) =>
                "Hold the western fields until the lantern wardens return from the ridge.";
            public string TrackedId => "uicap_rumor_active1";
            public void StartQuest(string id) { }
            public void SetTracked(string id) { }

            // WO-810 follow-up (2026-08-02): three daily rows whose AUTHORED labels carry the
            // raw "{target}" token, resolved through the SAME DailyQuestCatalog.ResolveLabel
            // path the live backend now uses — the daily shot pixel-proves the substitution
            // (the F8 defect was raw "Clear {target} waves" titles on the Daily tab).
            private static readonly IReadOnlyList<RumorBoardVM.DailyRow> DailyRows = BuildDailyRows();

            private static IReadOnlyList<RumorBoardVM.DailyRow> BuildDailyRows()
            {
                var instances = new[]
                {
                    new DeNelle.Core.Quests.DailyQuestInstance
                    { Id = "uicap_daily1", TemplateId = "combat.clear-waves", Slot = "combat",
                      Target = 5, Progress = 2, Completed = false, Label = "Clear {target} waves" },
                    new DeNelle.Core.Quests.DailyQuestInstance
                    { Id = "uicap_daily2", TemplateId = "exploration.visit-regions", Slot = "exploration",
                      Target = 3, Progress = 3, Completed = true, Label = "Visit {target} regions beyond the walls" },
                    new DeNelle.Core.Quests.DailyQuestInstance
                    { Id = "uicap_daily3", TemplateId = "wildcard.raise-towers", Slot = "wildcard",
                      Target = 4, Progress = 0, Completed = false, Label = null },   // fallback: TemplateId
                };
                var rows = new List<RumorBoardVM.DailyRow>(instances.Length);
                foreach (var q in instances)
                    rows.Add(new RumorBoardVM.DailyRow(q.Id,
                        DeNelle.Core.Quests.DailyQuestCatalog.ResolveLabel(q),
                        q.Progress, q.Target, q.Completed));
                return rows;
            }

            public IReadOnlyList<RumorBoardVM.DailyRow> DailyToday => DailyRows;
            public event Action Changed { add { } remove { } }
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
        /// <summary>
        /// A read-only, evenly-funded ledger for capture runs. It exists only so a shot can show
        /// the affordable branch of a priced screen; it never pretends to have spent anything
        /// (TrySpend debits its own fields and nothing else), and no shipped code constructs it.
        /// </summary>
        private sealed class CaptureLedger : DeNelle.Village.IEconomy
        {
            public CaptureLedger(int each)
            {
                Coins = each; Wood = each; Iron = each; Food = each; Crystals = each;
            }

            public int Coins { get; private set; }
            public int Wood { get; private set; }
            public int Iron { get; private set; }
            public int Food { get; private set; }
            public int Crystals { get; private set; }

            public event Action<DeNelle.Village.ResourceSnapshot> OnChanged;

            public bool CanAfford(DeNelle.Village.ResourceCost cost)
                => Wood >= cost.Wood && Food >= cost.Food && Iron >= cost.Iron
                   && Crystals >= cost.Crystals && Coins >= cost.Coins;

            public bool TrySpend(DeNelle.Village.ResourceCost cost)
            {
                if (!CanAfford(cost)) return false;
                Wood -= cost.Wood; Food -= cost.Food; Iron -= cost.Iron;
                Crystals -= cost.Crystals; Coins -= cost.Coins;
                OnChanged?.Invoke(new DeNelle.Village.ResourceSnapshot(Wood, Food, Iron, Crystals));
                return true;
            }

            public DeNelle.Village.ResourceCost Grant(DeNelle.Village.ResourceCost amount)
            {
                Wood += amount.Wood; Food += amount.Food; Iron += amount.Iron;
                Crystals += amount.Crystals; Coins += amount.Coins;
                OnChanged?.Invoke(new DeNelle.Village.ResourceSnapshot(Wood, Food, Iron, Crystals));
                // Uncapped fake ledger: every requested unit lands, so applied == requested.
                return amount;
            }
        }

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
        //  Panel: the ECHO SELECT CARD (EchoCardView) -- the WO-830 resource picker.
        //  WO-852: this card had NO capture coverage, which is exactly why its
        //  fraction-band / sub-touch-floor overlap ("only the bottom chip is
        //  tappable") reached the owner. The card must show its info block AND
        //  touch-floor chips with no overlap at BOTH mobile-landscape aspects.
        //
        //  Driven WITHOUT OpenFor on purpose: OpenFor registers a PanelHandle with
        //  the static PanelManager whose Close callback would outlive this
        //  DestroyImmediate'd host and poison later captures. We seed the VM +
        //  open flag and call the view's own private Build/Refresh instead, so the
        //  REAL layout code runs against no global state.
        // ---------------------------------------------------------------------
        private static int CaptureEchoCard()
        {
            return ForEachTarget("EchoCard", CaptureEchoCardOnce);
        }

        private static int CaptureEchoCardOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject hostGo = null;
            GameObject modal = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                hostGo = new GameObject("~UICapEchoCard");
                var view = hostGo.AddComponent<EchoCardView>();

                // Echo index 0 = the founding spirit (longest name + the affinity note row,
                // i.e. the WORST-case row budget the picker has to seat).
                SetPrivateField(view, "_vm", new EchoCardVM(0));
                SetPrivateField(view, "_open", true);
                InvokePrivate(view, "Build");

                modal = GetPrivateGameObject(view, "_modal");
                if (modal == null)
                {
                    Debug.LogWarning("[UICap-HL] EchoCardView._modal null after Build -- echo card capture skipped.");
                    return 0;
                }
                modal.SetActive(true);
                InvokePrivate(view, "Refresh");

                if (RenderCanvasToPng(modal, OutDir + "EchoCard_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] echo card capture threw: " + e);
            }
            finally
            {
                // Edit-mode teardown MUST be DestroyImmediate (same contract as the other shots).
                if (modal != null) UnityEngine.Object.DestroyImmediate(modal);
                if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // NOTE: SetPrivateField already exists further down this file (the SettingsController
        // fixture helper) - the WO-852 capture reuses it rather than declaring a second copy
        // (CS0111 duplicate-member, caught by the compile gate 2026-08-02).

        // ---------------------------------------------------------------------
        //  Render a uGUI canvas subtree to a PNG in EDIT mode (no Play).
        //  ScreenSpaceOverlay canvases render straight to the backbuffer and cannot
        //  be captured off a camera, so we flip the canvas to ScreenSpaceCamera,
        //  point a throwaway camera at a RenderTexture, force a synchronous layout +
        //  TMP mesh rebuild, render, and read the pixels back.
        // ---------------------------------------------------------------------
        // ---------------------------------------------------------------------
        //  Panel: the WO-864 queue CARD RAIL, in BOTH of its hosts, driven by a
        //  synthetic ObsidianQueueGate snapshot that reproduces the owner's live
        //  2026-08-03 Seeker screen exactly: 1 of 2 builders busy on
        //  tower_arcane_spire with 193s left (so the idle slot MUST draw a visible
        //  "FREE" card), 3 identical footman trains queued (so they MUST collapse
        //  to one card with an x3 badge), and an idle Research channel.
        //
        //  Landscape-only, at every capture target. CORRECTION (2026-08-05): the old
        //  banner here described the second harness size as the device resolution. It
        //  is not -- the Seeker's real surface is 2670x1200, which nothing in this
        //  repo had ever shot. (The same wrong claim is still in tools'
        //  run-autopilot-fleet.ps1 -- fix it there too.)
        // ---------------------------------------------------------------------
        private static int CaptureQueueRail()
        {
            return ForEachTarget("QueueCardRail", CaptureQueueRailOnce);
        }

        private static int CaptureQueueRailOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject canvasGo = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                PublishQueueFixture();

                canvasGo = new GameObject("~UICapQueueRail", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 4000;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);   // HudAreasHost's scaler
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // A dark backdrop so the rail's own plate/contrast reads honestly in the PNG.
                var bg = new GameObject("Backdrop", typeof(Image));
                bg.transform.SetParent(canvasGo.transform, false);
                var bgrt = (RectTransform)bg.transform;
                bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
                bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
                bg.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.12f, 1f);

                // (1) THE ALWAYS-ON HUD SURFACE — the exact HudArea.QueueStatus band
                //     geometry (0.780-0.995 x, 0.530-0.865 y) the owner is looking at.
                var band = MakeAreaMount(canvasGo.transform, "Area_QueueStatus",
                    new Vector2(0.780f, 0.530f), new Vector2(0.995f, 0.865f));
                var chipBand = MakePixelBand(band, "ChipBand", 0f, ElarionUiKit.MinTouchPx, 4f);
                ElarionUiKit.BuildObsidianButton(chipBand, "Builders 1/2 | Training 1",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    Vector2.zero, Vector2.one, null);
                var railMount = MakePixelBand(band, "QueueRailMount",
                    ElarionUiKit.MinTouchPx + 6f, 240f, 2f);
                QueueRailView.Build(railMount, DeNelle.Core.Jobs.ChannelId.Builder,
                    QueueRailView.Options.Default);

                // (2) THE MODAL SURFACE — three stacked rails, one per channel, in the
                //     Work Queue modal's body footprint. Same component, so any visual
                //     disagreement between the two surfaces would show up side by side.
                var modal = MakeAreaMount(canvasGo.transform, "Area_WorkQueueBody",
                    new Vector2(0.29f, 0.21f), new Vector2(0.71f, 0.79f));
                var modalBg = new GameObject("ModalPlate", typeof(Image));
                modalBg.transform.SetParent(modal, false);
                var mrt = (RectTransform)modalBg.transform;
                mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
                mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
                modalBg.GetComponent<Image>().color = new Color(0.035f, 0.033f, 0.038f, 0.98f);

                float railH = QueueRailView.HeightOf(QueueRailView.Options.Default);
                float y = 8f;
                foreach (DeNelle.Core.Jobs.ChannelId ch in new[]
                {
                    DeNelle.Core.Jobs.ChannelId.Builder,
                    DeNelle.Core.Jobs.ChannelId.Train,
                    DeNelle.Core.Jobs.ChannelId.Research,
                })
                {
                    var head = MakePixelBand(modal, "Head_" + ch, y, 60f, 8f);
                    var lbl = head.gameObject.AddComponent<TextMeshProUGUI>();
                    ElarionUiKit.EnsureFont(lbl);
                    var st = ObsidianQueueGate.Status;
                    lbl.text = ObsidianQueueGate.WorkQueueStatus.LabelOf(ch) + "   " +
                               st.BusyOf(ch) + "/" + st.SlotsOf(ch) + " busy";
                    lbl.fontSize = ElarionUi.FontLabel;
                    lbl.color = ElarionUi.Gilt;
                    lbl.fontStyle = FontStyles.Bold;
                    lbl.alignment = TextAlignmentOptions.MidlineLeft;
                    y += 60f;

                    var mount = MakePixelBand(modal, "Rail_" + ch, y, railH, 8f);
                    QueueRailView.Build(mount, ch, QueueRailView.Options.Default);
                    y += railH + 10f;
                }

                Canvas.ForceUpdateCanvases();
                if (RenderCanvasToPng(canvasGo, OutDir + "QueueCardRail_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] queue rail capture threw: " + e);
            }
            finally
            {
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }
            return saved;
        }

        // The owner's live screen, as data. Publishing through the REAL Core seam means
        // the capture exercises the same path the game does -- no view-only fixture.
        private static void PublishQueueFixture()
        {
            var s = new ObsidianQueueGate.WorkQueueStatus
            {
                Available = true,
                BuilderBusy = 1, BuilderSlots = 2, BuilderQueued = 1,
                TrainBusy = 1, TrainSlots = 2, TrainQueued = 3,
                ResearchBusy = 0, ResearchSlots = 1, ResearchQueued = 0,
                SoonestRemainingSec = 193,
                Entries = new[]
                {
                    new ObsidianQueueGate.QueueEntry
                    {
                        Label = "Arcane Spire", Verb = "BUILD", JobId = "tower_arcane_spire@15_7",
                        RemainingSec = 193, Queued = false, StackCount = 1,
                    },
                    new ObsidianQueueGate.QueueEntry
                    {
                        Label = "Stone Wall -> L2", Verb = "UPGRADE", JobId = "wall_stone@3_4",
                        TargetTier = 2, RemainingSec = -1, Queued = true, StackCount = 1,
                    },
                },
                TrainEntries = new[]
                {
                    new ObsidianQueueGate.QueueEntry
                    {
                        Label = "Footman", Verb = "TRAIN", JobId = "barracks-train:troop-footman:a1",
                        IconRole = RpgUiCatalog.RoleIcons, IconKey = "icon_sword",
                        RemainingSec = 42, Queued = false, StackCount = 1,
                    },
                    new ObsidianQueueGate.QueueEntry
                    {
                        Label = "Footman", Verb = "TRAIN", JobId = "barracks-train:troop-footman:b1",
                        IconRole = RpgUiCatalog.RoleIcons, IconKey = "icon_sword",
                        RemainingSec = -1, Queued = true, StackCount = 3,
                    },
                },
                ResearchEntries = Array.Empty<ObsidianQueueGate.QueueEntry>(),
            };
            ObsidianQueueGate.PublishStatus(s);
        }

        private static RectTransform MakeAreaMount(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform MakePixelBand(RectTransform parent, string name,
            float yFromTopPx, float heightPx, float insetX)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-insetX * 2f, heightPx);
            rt.anchoredPosition = new Vector2(0f, -yFromTopPx);
            return rt;
        }

        /// <summary>
        /// True when the frame carries no picture: fewer than a handful of distinct
        /// (4-bit quantised) colours, or almost every pixel identical to the dominant one.
        /// A real Obsidian panel -- plate, gold trim, antialiased text -- clears this by an
        /// order of magnitude; an all-black no-graphics frame scores exactly 1 bucket and
        /// 0 ink. Sampled on a stride, so the check costs microseconds.
        /// </summary>
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

            measure = "distinct=" + buckets.Count + " ink=" + ink.ToString("F4") +
                      " (floors " + BlankMinDistinctBuckets + " / " + BlankMinInkFraction.ToString("F4") + ")";
            return buckets.Count < BlankMinDistinctBuckets || ink < BlankMinInkFraction;
        }

        // =====================================================================
        //  WO-1010 P1 — the chips that replaced the build intent bar
        // =====================================================================
        /// <summary>
        /// Shoot the Build HUD's ghost controls in the four states the WO's acceptance
        /// criteria name: valid, blocked, clamped against a screen corner, and with the nudge
        /// pad toggled on. The clamp shot is the one that matters most -- chips that walk
        /// off-screen when the ghost nears an edge make a building unplaceable, and that is a
        /// failure the OLD bottom-edge bar could not have had, so it is new risk this redesign
        /// introduced and must be photographed rather than reasoned about.
        ///
        /// Positioning is invoked EXPLICITLY via LayoutGhostControlsNow() because
        /// MonoBehaviour ticks do not run in edit mode -- without that call this would
        /// photograph the chips parked at the canvas centre in all four shots and prove
        /// nothing at all.
        /// </summary>
        private static int CaptureBuildGhostChips()
        {
            return ForEachTarget("BuildGhostChips", target =>
            {
                int saved = 0;
                GameObject hostGo = null;
                try
                {
                    var hud = DeNelle.Village.BuildHudController.Create(null,
                        null, null, null, null, null);
                    if (hud == null)
                    {
                        Debug.LogWarning("[UICap-HL] BuildHudController.Create returned null -- build chips skipped.");
                        return 0;
                    }
                    hostGo = hud.gameObject;

                    var canvasTf = hostGo.transform.Find("BuildHudCanvas");
                    if (canvasTf == null)
                    {
                        Debug.LogWarning("[UICap-HL] BuildHudCanvas not found under the HUD host -- build chips skipped.");
                        return 0;
                    }
                    GameObject canvasGo = canvasTf.gameObject;

                    hud.Show();
                    hud.SetState(DeNelle.Village.BuildHudState.Placing);
                    hud.SetPlacingLabel("Arcane Spire", "88 wood, 88 iron, 187 crystals");

                    // (1) VALID — chips near the middle of the field, OK chip affirmative.
                    hud.TrackGhost(new Vector2(target.W * 0.5f, target.H * 0.55f), true, null);
                    hud.LayoutGhostControlsNow();
                    if (RenderCanvasToPng(canvasGo, OutDir + "BuildGhostChips_valid_" + target.Tag + ".png",
                        target.W, target.H)) saved++;

                    // (2) BLOCKED — the refusal must be READABLE, not just red.
                    hud.TrackGhost(new Vector2(target.W * 0.5f, target.H * 0.55f), false, "Not enough Wood");
                    hud.LayoutGhostControlsNow();
                    // WO-942 gap 2: the D17 SPRITE path's invalid verdict had no assertion
                    // anywhere. The worded refusal on the PILL is photographed by this shot, but
                    // the confirm chip's own invalid state is a colour/alpha + interactable flip
                    // that a PNG cannot be trusted to prove (the owner is red/green colourblind;
                    // "did the check-mark dim" is exactly the judgement a screenshot should not
                    // be asked for). Measure it instead — see AssertConfirmChipInvalid.
                    AssertConfirmChipInvalid(canvasGo, target.Tag);
                    if (RenderCanvasToPng(canvasGo, OutDir + "BuildGhostChips_blocked_" + target.Tag + ".png",
                        target.W, target.H)) saved++;

                    // (3) EDGE — ghost jammed into the bottom-right corner. Every chip must
                    //     still be fully on-screen and still be tappable.
                    hud.TrackGhost(new Vector2(target.W - 2f, 2f), true, null);
                    hud.LayoutGhostControlsNow();
                    string edgePath = OutDir + "BuildGhostChips_edgeclamp_" + target.Tag + ".png";
                    if (RenderCanvasToPng(canvasGo, edgePath, target.W, target.H)) saved++;

                    // (4) NUDGE PAD — it follows STATE, with no toggle for the player to find
                    //     (owner ruling 2026-08-09: "it should be smart... user should not need
                    //     to do anything"). The assertion is inverted from the old one: a
                    //     surviving toggle button would now be the defect, so its ABSENCE is
                    //     what gets checked.
                    //
                    // ── WO-942 gap 1: THIS CASE USED TO PROVE NOTHING. ───────────────────
                    // It photographed BYTE-IDENTICAL to (3) at all three sizes (88555 / 108491 /
                    // 118402 — the identical-file-size tell, UI_PLAYBOOK §13.1), for TWO reasons
                    // that both had to be fixed:
                    //   a. it never moved the ghost, so it re-shot (3)'s corner-clamped frame; and
                    //   b. the pad's visibility is the BRAIN's per-frame verdict, delivered via
                    //      SetNudgePadAllowed from an Update that DOES NOT RUN IN EDIT MODE — so
                    //      the stick built by SetState(Placing) was still SetActive(false) and
                    //      the shot contained no stick at all.
                    // Both are now driven EXPLICITLY, exactly as LayoutGhostControlsNow() already
                    // is and for the identical reason. A capture that cannot draw the thing it
                    // captures launders an unverified state as verified (§13).
                    hud.TrackGhost(new Vector2(target.W * 0.28f, target.H * 0.5f), true, null);
                    hud.LayoutGhostControlsNow();
                    hud.SetNudgePadAllowed(true);

                    if (canvasGo.transform.Find("BuildNudgePadToggle") != null)
                        Debug.LogWarning("[UICap-HL] a BuildNudgePadToggle still exists -- the '+' toggle was " +
                                         "RETIRED; the pad follows placement state. REAL gap, not noise.");
                    AssertNudgePadOnScreen(canvasGo, target.Tag, target.W, target.H);

                    string padPath = OutDir + "BuildGhostChips_padon_" + target.Tag + ".png";
                    if (RenderCanvasToPng(canvasGo, padPath, target.W, target.H)) saved++;
                    AssertShotsDiffer(edgePath, padPath, "BuildGhostChips edgeclamp vs padon", target.Tag);
                }
                catch (Exception e)
                {
                    Debug.LogError("[UICap-HL] build ghost chips capture threw: " + e);
                }
                finally
                {
                    if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                }
                return saved;
            });
        }

        // =====================================================================
        //  WO-942 — the assertions that close the two capture-case gaps
        // =====================================================================

        /// <summary>
        /// WO-942 gap 2 — the D17 SPRITE path's INVALID verdict, MEASURED.
        ///
        /// D17 gave the confirm verb a check-mark sprite, and a check-mark has no word to flip.
        /// So <c>BuildHudController</c> renders the invalid state as DIM + DISABLED: the icon
        /// Image goes to alpha 0.35 and the Button goes non-interactable, with the WORDED reason
        /// staying on the pill. Both halves of that are unphotographable in practice — an alpha
        /// change on a small glyph is precisely the judgement a PNG (and a red/green colourblind
        /// reader) should never be asked to make — and until now NOTHING measured them. This is
        /// the measurement. On the ASCII fallback path the chip flips its WORD instead, so that
        /// is what gets asserted there.
        ///
        /// Warn-only, matching every other assertion in this harness: it prints a REAL-gap line
        /// the capture judge reads, and never aborts a capture run mid-way.
        /// </summary>
        private static void AssertConfirmChipInvalid(GameObject canvasGo, string tag)
        {
            if (canvasGo == null) return;

            Transform chip = FindDescendant(canvasGo.transform, "OkChip");
            if (chip == null)
            {
                Debug.LogWarning("[UICap-HL] " + tag + ": no 'OkChip' under the build HUD canvas -- the confirm " +
                                 "verb was renamed or removed, so the D17 invalid verdict is unmeasured. " +
                                 "REAL gap, not noise.");
                return;
            }

            var btn = chip.GetComponent<Button>();
            if (btn == null)
                Debug.LogWarning("[UICap-HL] " + tag + ": 'OkChip' carries no Button -- confirm cannot be " +
                                 "disabled at all while the placement is invalid. REAL gap, not noise.");
            else if (btn.interactable)
                Debug.LogWarning("[UICap-HL] " + tag + ": 'OkChip' is still INTERACTABLE while the ghost is " +
                                 "invalid -- the player can commit a refused placement. REAL gap, not noise.");

            // Sprite path: the glyph Image dims. ASCII fallback: the label flips OK -> No.
            Transform icon = FindDescendant(chip, "ChipIcon");
            var iconImg = icon != null ? icon.GetComponent<Image>() : null;
            if (iconImg != null)
            {
                const float WantAlpha = 0.35f;   // BuildHudController: new Color(1,1,1,0.35f)
                if (Mathf.Abs(iconImg.color.a - WantAlpha) > 0.02f)
                    Debug.LogWarning("[UICap-HL] " + tag + ": D17 sprite path -- 'OkChip/ChipIcon' alpha is " +
                                     iconImg.color.a.ToString("F2") + ", expected " + WantAlpha.ToString("F2") +
                                     " for the invalid verdict. The check-mark reads as ENABLED while the " +
                                     "placement is refused. REAL gap, not noise.");
                else
                    Debug.Log("[UICap-HL] " + tag + ": D17 invalid verdict OK (sprite path: icon alpha " +
                              iconImg.color.a.ToString("F2") + ", confirm non-interactable).");
                return;
            }

            var label = chip.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                Debug.LogWarning("[UICap-HL] " + tag + ": 'OkChip' has neither a ChipIcon nor a label -- the " +
                                 "confirm verb renders nothing at all. REAL gap, not noise.");
            else if (!string.Equals(label.text, "No", StringComparison.Ordinal))
                Debug.LogWarning("[UICap-HL] " + tag + ": ASCII fallback path -- 'OkChip' label reads '" +
                                 label.text + "', expected 'No' for the invalid verdict. The refusal would " +
                                 "rest on COLOUR ALONE, which the owner cannot see. REAL gap, not noise.");
            else
                Debug.Log("[UICap-HL] " + tag + ": D17 invalid verdict OK (ASCII path: label 'No', " +
                          "confirm non-interactable).");
        }

        /// <summary>
        /// WO-942 gap 1 — the nudge stick is ACTUALLY UP and ACTUALLY ON-SCREEN before the padon
        /// shot is taken. Edit-mode safe: the pad's visibility has just been driven explicitly by
        /// <c>SetNudgePadAllowed(true)</c>, because the brain's per-frame verdict never ticks here.
        /// An off-screen or hidden pad means the shot photographs a stick the player does not have.
        /// </summary>
        private static void AssertNudgePadOnScreen(GameObject canvasGo, string tag, int w, int h)
        {
            if (canvasGo == null) return;

            var pad = canvasGo.transform.Find("BuildNudgePad");
            if (pad == null || !pad.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[UICap-HL] " + tag + ": BuildNudgePad is absent/hidden while PLACING -- the " +
                                 "player has no way to nudge a piece and no toggle to summon one, and the " +
                                 "padon shot photographs nothing. REAL gap, not noise.");
                return;
            }

            // The host stretches the whole canvas; the STICK is the widget that has to be reachable.
            Transform stick = FindDescendant(pad, "AnalogStick") ?? FindDescendant(pad, "VirtualDPad");
            var root = canvasGo.GetComponent<RectTransform>();
            var srt = stick as RectTransform;
            if (stick == null || srt == null)
            {
                Debug.LogWarning("[UICap-HL] " + tag + ": BuildNudgePad is active but carries NO stick/d-pad " +
                                 "widget -- ElarionUiKit.BuildAnalogStick and the WO-611 d-pad fallback BOTH " +
                                 "failed to construct. REAL gap, not noise.");
                return;
            }

            if (!TryRectInRoot(srt, root, out Rect r))
            {
                Debug.LogWarning("[UICap-HL] " + tag + ": nudge stick rect could not be measured.");
                return;
            }

            Rect canvasRect = root.rect;
            bool inside = r.xMin >= canvasRect.xMin - GeoContainSlackPx && r.xMax <= canvasRect.xMax + GeoContainSlackPx
                       && r.yMin >= canvasRect.yMin - GeoContainSlackPx && r.yMax <= canvasRect.yMax + GeoContainSlackPx;
            if (!inside)
                Debug.LogWarning("[UICap-HL] " + tag + ": nudge stick rect (" + r + ") is NOT fully inside the " +
                                 canvasRect + " canvas at " + w + "x" + h + " -- part of the only pixel-nudge " +
                                 "control is unreachable. REAL gap, not noise.");
            else
                Debug.Log("[UICap-HL] " + tag + ": nudge stick UP and fully on-screen (" + stick.name +
                          ", " + r.width.ToString("F0") + "x" + r.height.ToString("F0") + " ref px).");
        }

        /// <summary>
        /// WO-942 gap 1 — the identical-file-size tell (UI_PLAYBOOK §13.1) turned into an
        /// assertion. Two capture cases that are meant to photograph DIFFERENT states and come out
        /// byte-identical did not photograph two states; one of them proved nothing, and a green
        /// run then launders that unverified state as verified. Byte length is the cheap, exact
        /// signal — PNGs of two genuinely different frames are never the same length.
        /// </summary>
        private static void AssertShotsDiffer(string pathA, string pathB, string what, string tag)
        {
            try
            {
                if (!File.Exists(pathA) || !File.Exists(pathB)) return;   // a missing shot is reported elsewhere
                long a = new FileInfo(pathA).Length;
                long b = new FileInfo(pathB).Length;
                if (a == b)
                    Debug.LogWarning("[UICap-HL] " + tag + ": " + what + " produced BYTE-IDENTICAL shots (" + a +
                                     " bytes each) -- the two cases photographed the SAME frame, so one of them " +
                                     "proves nothing (UI_PLAYBOOK 13.1). REAL gap, not noise.");
                else
                    Debug.Log("[UICap-HL] " + tag + ": " + what + " differ (" + a + " vs " + b + " bytes).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UICap-HL] " + tag + ": shot-difference check threw: " + e.Message);
            }
        }

        /// <summary>First descendant named <paramref name="name"/> (inactive included), or null.</summary>
        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        // =====================================================================
        //  WO-1010 P2 — the collapsed dock and its "^ Buildings (n)" way back
        // =====================================================================
        /// <summary>
        /// Shoot the palette dock in BOTH states: the open carousel, and collapsed-for-placing
        /// where the ONLY chrome left standing must be the restore tab.
        ///
        /// The collapsed shot is the point of this case. Collapse() hides every dock
        /// background so the field is clear, and until P2 that left NO route back to the
        /// carousel — so what this photograph has to establish is a negative plus a positive:
        /// the dock really is gone, AND exactly one labelled door remains. Neither half is
        /// visible to a compile gate or a data oracle.
        /// </summary>
        /// <summary>Shape of Data/Canonical/structures-catalog.json (mirrors the economy oracle's).</summary>
        private sealed class CaptureStructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<DeNelle.Core.Catalog.CatalogEntry> Entries
                = new List<DeNelle.Core.Catalog.CatalogEntry>();
        }

        /// <summary>
        /// Populate CatalogRegistry from the canonical structures file so the palette has real
        /// cards to draw. Idempotent, and it registers ONLY ids that are absent, so it can never
        /// stomp a row a different capture case set up. Reads through CanonicalJson — the same
        /// source of truth the game and the economy oracle use — rather than a hand-rolled path,
        /// so a capture can never show cards the shipped catalog does not contain.
        /// </summary>
        private static void HydrateCatalogForCapture()
        {
            try
            {
                if (DeNelle.Core.Catalog.CatalogRegistry.Count > 0) return;

                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning("[UICap-HL] structures-catalog.json unreadable -- palette cards cannot be " +
                                     "captured; the dock will render empty and prove nothing about cards.");
                    return;
                }

                var settings = new JsonSerializerSettings
                {
                    Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                var file = JsonConvert.DeserializeObject<CaptureStructuresFile>(json, settings);
                if (file == null || file.Entries == null || file.Entries.Count == 0)
                {
                    Debug.LogWarning("[UICap-HL] structures-catalog.json deserialized to 0 entries -- no cards to capture.");
                    return;
                }

                int n = 0;
                foreach (var e in file.Entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    if (DeNelle.Core.Catalog.CatalogRegistry.Get(e.id) == null)
                    { DeNelle.Core.Catalog.CatalogRegistry.Register(e); n++; }
                }
                Debug.Log("[UICap-HL] hydrated CatalogRegistry with " + n + " entry(ies) for the palette capture");
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] catalog hydration threw: " + e);
            }
        }

        private static int CapturePaletteCollapsed()
        {
            return ForEachTarget("BuildPaletteDock", target =>
            {
                int saved = 0;
                GameObject hostGo = null;
                GameObject canvasGo = null;
                try
                {
                    // The registry is populated at RUNTIME by the game's bootstrap, which never
                    // runs in an edit-mode capture — the first version of this case shot a dock
                    // reading "No buildables registered" and still reported green, which would
                    // have made the cards look verified when nothing about them was. Hydrate
                    // from the same canonical file the economy oracle uses.
                    HydrateCatalogForCapture();

                    hostGo = new GameObject("BuildPaletteUI_Capture");
                    var palette = hostGo.AddComponent<DeNelle.Village.BuildPaletteUI>();
                    palette.Configure(DeNelle.Core.Catalog.BuildType.Town);
                    palette.Show();

                    var canvasTf = hostGo.transform.Find("BuildPaletteCanvas");
                    canvasGo = canvasTf != null ? canvasTf.gameObject : null;
                    if (canvasGo == null)
                    {
                        // ElarionUiKit.BuildModalCanvas parents the canvas at the SCENE ROOT,
                        // not under the palette host — so this scan is the normal path, not a
                        // fallback. That distinction caused a real bug: destroying only the
                        // host leaked the canvas, canvases ACCUMULATED across the three target
                        // sizes, and this scan then returned target 1's stale, already-COLLAPSED
                        // canvas. The 2340 and 2670 "open" shots were byte-identical to their
                        // own collapsed shots and showed nothing but the restore tab. The
                        // capture reported green the whole time. Take the NEWEST match, and
                        // destroy it explicitly below.
                        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                            if (c != null && c.gameObject.name == "BuildPaletteCanvas") canvasGo = c.gameObject;
                    }
                    if (canvasGo == null)
                    {
                        Debug.LogWarning("[UICap-HL] BuildPaletteCanvas not found -- palette dock capture skipped.");
                        return 0;
                    }

                    // (1) OPEN — tabs + card carousel, the state the player picks from.
                    if (RenderCanvasToPng(canvasGo, OutDir + "BuildPaletteDock_open_" + target.Tag + ".png",
                        target.W, target.H)) saved++;

                    // (2) COLLAPSED — dock chrome gone, restore tab standing.
                    palette.Collapse("Arcane Spire");
                    if (RenderCanvasToPng(canvasGo, OutDir + "BuildPaletteDock_collapsed_" + target.Tag + ".png",
                        target.W, target.H)) saved++;

                    // The tab is the WHOLE point of the collapsed state; if it is absent the
                    // png would look like a correct empty field and hide a dead end.
                    var tab = canvasGo.transform.Find("BuildPaletteRestoreTab");
                    if (tab == null || !tab.gameObject.activeInHierarchy)
                        Debug.LogWarning("[UICap-HL] collapsed palette has NO active BuildPaletteRestoreTab -- " +
                                         "the player would have no way back to the carousel. REAL gap, not noise.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[UICap-HL] palette dock capture threw: " + e);
                }
                finally
                {
                    // Destroy the ROOT canvas too, not just the host — see the scan comment
                    // above. Sweep by name so a canvas from an earlier target that somehow
                    // survived cannot poison the next one either.
                    if (hostGo != null) UnityEngine.Object.DestroyImmediate(hostGo);
                    if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                    foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                        if (c != null && c.gameObject.name == "BuildPaletteCanvas")
                            UnityEngine.Object.DestroyImmediate(c.gameObject);
                }
                return saved;
            });
        }

        // =====================================================================
        //  WO-952 -- the EndState (wave-clear) banner, WITH EYES AND WITH A NUMBER.
        // ---------------------------------------------------------------------
        //  THE DEFECT THIS EXISTS FOR (F8 daemon, 2026-08-10, captured TWICE in one
        //  session on the desktop exe -- capture-20260810-102345.md and seq 2268):
        //
        //    [Flow:EndState] body rows COMPRESSED to fit: need=276px well=249px scale=0.9
        //    - the panel hit its screen-height clamp; every band is now below its own
        //      content size
        //
        //  The geometry half of WO-952 landed on 2026-08-10 (EndStateView.Bind's owned
        //  compact solve). It shipped SOURCE-REASONED: no capture case covered the
        //  EndState at all, so nothing in the harness could have told us if it worked --
        //  and "no capture case" is indistinguishable from "the case passes".
        //
        //  WHAT THIS ASSERTS, and why it is not a hollow assertion
        //  (INSTRUMENTATION_STANDARD 1.4b -- a trace that cannot report failure is a bug):
        //    (1) The banner's OWN instrumentation is TAPPED for the duration of the build
        //        (FlowTrace.Sink swap, restored in a finally). If the `COMPRESSED to fit`
        //        Fail line fires, this run FAILS -- the absence of that line is the
        //        acceptance signal the WO asked for, and it is now checked, not hoped for.
        //    (2) It is NOT trusted on its own. The SETTLED layout is MEASURED: the resolved
        //        body-well rect and the resolved band stack, both read back off the real
        //        RectTransforms in kit reference px, and the compression factor is
        //        RECOMPUTED from them (extent / need) instead of read off the view's own
        //        arithmetic. A pass prints all four numbers; a run where the numbers cannot
        //        be obtained is reported as a FAILURE, never as silence.
        //  So this case fails loudly in three distinct ways: the trace fired, the measured
        //  fit was short, or nothing could be measured at all.
        //
        //  FIXTURES, not the live factory. EndStateVM.FromWaveClear(n) reads the live wall
        //  damage ledger / wallet / repair controller, none of which stand up in a
        //  synchronous edit-mode render -- and a fixture is what pins the WORST case anyway.
        //  Two are built: the Repair-All banner (a CTA + a full 4-row damage report -- the
        //  exact shape that broke, because a CTA-carrying banner kept the stale close-band
        //  reservation) and the plain no-CTA banner (the path that never broke, so a
        //  regression in it would otherwise be silent).
        // =====================================================================

        /// <summary>Per-case measured fit record (all lengths in kit reference px).</summary>
        private struct EndStateFit
        {
            public string Label;
            public float NeedPx;          // the view's own traced content demand
            public float WellPx;          // MEASURED resolved body-well height
            public float ExtentPx;        // MEASURED first-band top -> last-band bottom
            public int Bands;
            public float MeasuredScale;   // ExtentPx / NeedPx -- recomputed, not read
            public float UnitFactor;      // measured root px per kit reference px (~1.0)
            public bool TracedCompression;
        }

        private static readonly List<EndStateFit> _endStateFits = new List<EndStateFit>();
        private static readonly List<string> _endStateFitFailures = new List<string>();

        /// <summary>Compression is real below this (the view's own threshold, same rationale:
        /// a self-fitting solve lands on target within float residue, a real clamp lands far
        /// below -- the captured defect measured 0.9).</summary>
        private const float EndStateFitFloor = 0.995f;

        /// <summary>Optional probe run on the settled, camera-space layout inside
        /// <see cref="RenderCanvasToPng"/> (canvas, label, w, h).</summary>
        private static Action<GameObject, string, int, int> _settledProbe;

        // Per-case hand-off between the trace tap and the settled-layout probe.
        private static float _endStateNeedPx;
        private static bool _endStateSawCompression;

        /// <summary>Forwards every FlowTrace line to the real sink AND keeps the
        /// [Flow:EndState] ones, so the panel's own instrumentation becomes this
        /// harness's evidence instead of scrolling past in a log nobody greps.</summary>
        private sealed class EndStateTraceTap : ITraceSink
        {
            private readonly ITraceSink _inner;
            public readonly List<string> Lines = new List<string>();

            public EndStateTraceTap(ITraceSink inner) { _inner = inner; }

            private void Keep(string line)
            {
                if (!string.IsNullOrEmpty(line) && line.IndexOf("[Flow:EndState]", StringComparison.Ordinal) >= 0)
                    Lines.Add(line);
            }

            public void Info(string line)  { Keep(line); if (_inner != null) _inner.Info(line); }
            public void Warn(string line)  { Keep(line); if (_inner != null) _inner.Warn(line); }
            public void Error(string line) { Keep(line); if (_inner != null) _inner.Error(line); }
        }

        private static int CaptureEndStateWaveClear()
        {
            return ForEachTarget("EndStateWaveClear", CaptureEndStateWaveClearOnce);
        }

        private static int CaptureEndStateWaveClearOnce(CaptureTarget target)
        {
            int saved = 0;
            saved += CaptureOneEndStateBanner(target, "EndStateWaveClear_repairAll", true);
            saved += CaptureOneEndStateBanner(target, "EndStateWaveClear_plain", false);
            return saved;
        }

        /// <summary>Build ONE real compact EndState banner, tap its trace, shoot it, and
        /// measure its settled fit. <paramref name="withCta"/> selects the Repair-All
        /// (CTA-carrying) shape -- the one the 2026-08-10 capture caught.</summary>
        private static int CaptureOneEndStateBanner(CaptureTarget target, string caseName, bool withCta)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            EndStateView view = null;
            GameObject canvasGo = null;
            ITraceSink prevSink = FlowTrace.Sink;
            var tap = new EndStateTraceTap(prevSink);
            string label = caseName + "_" + target.Tag;

            _endStateNeedPx = 0f;
            _endStateSawCompression = false;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                FlowTrace.Sink = tap;
                view = EndStateView.Show(BuildWaveClearFixture(withCta));
                FlowTrace.Sink = prevSink;

                if (view == null)
                {
                    RecordEndStateFitFailure(label + ": EndStateView.Show returned null -- the banner did " +
                        "not build, so this run measured NOTHING about its fit (an unbuilt panel is a " +
                        "failure of the case, not a pass by absence).");
                    return 0;
                }
                canvasGo = view.gameObject;

                // The view's own numbers, harvested from its instrumentation. `need=` is printed
                // by BOTH the owned compact solve's Step and the fit Fail/Step lines, so it is
                // present on every path a compact banner can take.
                _endStateNeedPx = ParseNumberAfter(tap.Lines, "need=");
                _endStateSawCompression = ContainsLine(tap.Lines, "body rows COMPRESSED to fit");

                // The reveal is a coroutine, and coroutines never tick in an edit-mode render --
                // so every CanvasGroup is still parked at its start-of-tween alpha 0 and the frame
                // would be blank. Stamp the tween's FINISHED state (what the player sees a beat
                // later); geometry is untouched by this, only opacity.
                foreach (var cg in canvasGo.GetComponentsInChildren<CanvasGroup>(true))
                    if (cg != null) cg.alpha = 1f;

                _settledProbe = MeasureEndStateFit;
                if (RenderCanvasToPng(canvasGo, OutDir + label + ".png", target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] end-state banner capture threw: " + e);
                RecordEndStateFitFailure(label + ": the capture threw before it could measure the fit (" +
                    e.GetType().Name + ": " + e.Message + ")");
            }
            finally
            {
                _settledProbe = null;
                FlowTrace.Sink = prevSink;
                // Edit-mode teardown MUST be DestroyImmediate (the view's own Close uses runtime
                // Destroy). EndStateView.OnDestroy unsubscribes sceneLoaded and clears the
                // end-state posture signal, so nothing leaks into the editor session.
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        /// <summary>The worst-case wave-clear banner as DATA. Row/label shapes mirror
        /// EndStateVM.FromWaveClear's own output (rewards + a damage report, capped at its
        /// CompactMaxSpoilRows = 4); the live factory is not callable headlessly because it
        /// reads the wall-damage ledger and the wallet.</summary>
        private static EndStateVM BuildWaveClearFixture(bool withCta)
        {
            var vm = new EndStateVM
            {
                Kind = EndStateKind.WaveResults,
                Title = "Wave 7 Cleared!",
                Subtitle = "The wave broke against your walls.\nThe north gate took the worst of it.",
                Compact = true,
                PrimaryLabel = null,      // compact banners auto-dismiss; no primary CTA
                PrimaryRoute = "dismiss",
                AutoDismissSeconds = 0f,  // no coroutine to leave pending in edit mode
                Stars = -1,
                TimeSeconds = -1f,
            };

            vm.Spoils.Add(new SpoilRowVM { Label = "Wood", Amount = "+240" });
            vm.Spoils.Add(new SpoilRowVM { Label = "Iron", Amount = "+85" });
            if (withCta)
            {
                // The CTA path: a full damage report (4 rows = the banner's hard cap) under a
                // Repair-All button. This is the shape that produced need=276px / well=249px.
                vm.Spoils.Add(new SpoilRowVM { Label = "North Gate", Amount = "DESTROYED, looted 120" });
                vm.Spoils.Add(new SpoilRowVM { Label = "Wall x3", Amount = "damaged" });
                vm.CtaLabel = "Repair All - 120 wood, 40 iron";
                vm.CtaEnabled = true;
                vm.CtaRoute = "repair-all";
            }
            return vm;
        }

        /// <summary>MEASURE the banner's settled fit off its real RectTransforms and RECOMPUTE
        /// the compression factor. Runs inside RenderCanvasToPng (camera-space, post-rebuild),
        /// which is the only place a rect read is in kit reference px.
        ///
        /// The band stack's measured extent is, by construction, need x scale: BuildBody lays
        /// bands top-down at (px * scale) with (BandGapPx * scale) between them, so
        /// first-band-top -> last-band-bottom == (sum px + gaps) * scale == need * scale. So
        /// extent / need IS the compression factor -- derived from resolved geometry, with no
        /// dependence on the view's own arithmetic. Anything that cannot be measured is
        /// recorded as a FAILURE: silence is not a pass.</summary>
        private static void MeasureEndStateFit(GameObject canvasGo, string label, int w, int h)
        {
            RectTransform root = canvasGo != null ? canvasGo.GetComponent<RectTransform>() : null;
            if (root == null)
            {
                RecordEndStateFitFailure(label + ": no root RectTransform on the captured canvas -- nothing measurable.");
                return;
            }

            RectTransform well = null;
            foreach (var rt in canvasGo.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt != null && rt.gameObject.name == "Zone_RewardWell") { well = rt; break; }
            }
            if (well == null)
            {
                RecordEndStateFitFailure(label + ": the banner built no 'Zone_RewardWell' -- EndStateView's " +
                    "body well is gone or renamed, so this gate is measuring nothing and must not read as green.");
                return;
            }

            var bandRects = new List<Rect>();
            for (int i = 0; i < well.childCount; i++)
            {
                var child = well.GetChild(i) as RectTransform;
                if (child == null || child.gameObject.name != "Band") continue;
                if (TryRectInRoot(child, root, out Rect br)) bandRects.Add(br);
            }
            if (bandRects.Count == 0)
            {
                RecordEndStateFitFailure(label + ": zero 'Band' rows resolved inside the body well -- the " +
                    "banner rendered no content bands, so its fit is unproven (and the png is empty of rows).");
                return;
            }
            if (!TryRectInRoot(well, root, out Rect wellRect))
            {
                RecordEndStateFitFailure(label + ": the body well resolved to a degenerate rect -- no measurement possible.");
                return;
            }

            float top = float.MinValue, bottom = float.MaxValue;
            float outsideBy = float.MinValue;
            foreach (var br in bandRects)
            {
                if (br.yMax > top) top = br.yMax;
                if (br.yMin < bottom) bottom = br.yMin;
                outsideBy = Mathf.Max(outsideBy, OutsideBy(br, wellRect));
            }
            float extentPx = top - bottom;

            // Root-space px per kit reference px. Expected ~1.0 (the kit's own reference space);
            // measured rather than assumed, because `need` is a KIT number and the extent is a
            // MEASURED one -- comparing them across a silently different unit is exactly the kind
            // of hidden mismatch that makes a green gate meaningless.
            float kitH = ElarionUiKit.PostScaleCanvasHeight(canvasGo.transform);
            float unit = kitH > 1f ? root.rect.height / kitH : 0f;
            if (unit <= 0.01f) unit = 1f;

            float needRootPx = _endStateNeedPx * unit;
            float measuredScale = needRootPx > 1f ? extentPx / needRootPx : 0f;

            var fit = new EndStateFit
            {
                Label = label,
                NeedPx = _endStateNeedPx,
                WellPx = wellRect.height / Mathf.Max(0.0001f, unit),
                ExtentPx = extentPx / Mathf.Max(0.0001f, unit),
                Bands = bandRects.Count,
                MeasuredScale = measuredScale,
                UnitFactor = unit,
                TracedCompression = _endStateSawCompression,
            };
            _endStateFits.Add(fit);

            string numbers = "need=" + fit.NeedPx.ToString("0") + "px (traced) well=" +
                             fit.WellPx.ToString("0") + "px (MEASURED) bands=" + fit.Bands +
                             " stack=" + fit.ExtentPx.ToString("0") + "px (MEASURED) -> measured scale=" +
                             fit.MeasuredScale.ToString("0.###") + " [root px per ref px = " +
                             unit.ToString("0.###") + "]";

            if (_endStateNeedPx <= 1f)
            {
                RecordEndStateFitFailure(label + ": the banner printed NO `need=` line, so there is no content " +
                    "demand to measure the well against. " + numbers + ". Treat this case as unproven, not passing.");
                return;
            }
            if (fit.TracedCompression)
            {
                RecordEndStateFitFailure(label + ": the panel's own net fired `body rows COMPRESSED to fit` -- " +
                    "every band is below its own content size. " + numbers);
                return;
            }
            if (measuredScale < EndStateFitFloor)
            {
                RecordEndStateFitFailure(label + ": MEASURED compression -- the resolved band stack is only " +
                    measuredScale.ToString("0.###") + " of the content's demand. " + numbers);
                return;
            }
            if (outsideBy > GeoContainSlackPx)
            {
                RecordEndStateFitFailure(label + ": a body band resolves " + outsideBy.ToString("0.#") +
                    "px OUTSIDE the body well it is laid into. " + numbers);
                return;
            }

            Debug.Log("[UICap-ENDSTATE] " + label + " fits: " + numbers);
        }

        private static void RecordEndStateFitFailure(string message)
        {
            _endStateFitFailures.Add(message);
            Debug.LogError("[UICap-ENDSTATE] " + message);
        }

        /// <summary>First number following <paramref name="token"/> in any of the captured lines
        /// (e.g. "need=276px" -> 276). Returns 0 when the token never appeared.</summary>
        private static float ParseNumberAfter(List<string> lines, string token)
        {
            if (lines == null || string.IsNullOrEmpty(token)) return 0f;
            foreach (var line in lines)
            {
                if (line == null) continue;
                int i = line.IndexOf(token, StringComparison.Ordinal);
                if (i < 0) continue;
                i += token.Length;
                int start = i;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == '-')) i++;
                if (i > start && float.TryParse(line.Substring(start, i - start),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v))
                    return v;
            }
            return 0f;
        }

        private static bool ContainsLine(List<string> lines, string needle)
        {
            if (lines == null) return false;
            foreach (var line in lines)
                if (line != null && line.IndexOf(needle, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>The WO-952 acceptance marker. DISTINCT from UI_CAPTURE_OK / UI_GEOMETRY_OK
        /// (CLAUDE.md 8: one marker per entry point -- a shared string is how a partial pass once
        /// read as a full one), and it always carries the MEASUREMENTS, never a bare "ok".</summary>
        private static void ReportEndStateFit()
        {
            if (_endStateFits.Count == 0 && _endStateFitFailures.Count == 0)
            {
                Debug.LogError("UI_ENDSTATE_FIT_FAIL x0 -- ZERO end-state banners were measured this run, so " +
                               "nothing here proves the WO-952 wave-clear fit. An absent case is not a passing case.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var f in _endStateFits)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(f.Label).Append(" need=").Append(f.NeedPx.ToString("0"))
                  .Append("px well=").Append(f.WellPx.ToString("0"))
                  .Append("px stack=").Append(f.ExtentPx.ToString("0"))
                  .Append("px bands=").Append(f.Bands)
                  .Append(" scale=").Append(f.MeasuredScale.ToString("0.###"));
            }

            if (_endStateFitFailures.Count > 0)
            {
                Debug.LogError("UI_ENDSTATE_FIT_FAIL x" + _endStateFitFailures.Count + " failure(s) over " +
                               _endStateFits.Count + " measured case(s) -- see the " +
                               "[UICap-ENDSTATE] lines above. Measured: " +
                               (sb.Length > 0 ? sb.ToString() : "(nothing measurable)"));
                return;
            }

            Debug.Log("UI_ENDSTATE_FIT_OK " + _endStateFits.Count + " banners -- no `body rows COMPRESSED to " +
                      "fit` fired and the RESOLVED band stack measures at least " +
                      EndStateFitFloor.ToString("0.###") + " of the content's own demand in every case. " +
                      "Measured: " + sb);
        }

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

                // THE NUMERIC GEOMETRY GATE. Runs on the SETTLED layout, before the pixels
                // exist -- eyes-on review is not a defence and never was (CLAUDE.md §12).
                AuditGeometry(canvasGo, Path.GetFileNameWithoutExtension(path), w, h);

                // A PANEL-SPECIFIC measurement on the SAME settled, camera-space layout the
                // audit just ran on. This is the only point in the run where a rect read is
                // in kit reference px (an overlay canvas's own rect is still the editor's
                // 640x480 in batchmode -- see the file banner), so any probe that has to
                // compare a RESOLVED rect against an authored px budget must run HERE.
                // Set by the capture that needs it, cleared in its finally; guarded, because a
                // throwing probe must never cost the run its screenshot.
                if (_settledProbe != null)
                {
                    try { _settledProbe(canvasGo, Path.GetFileNameWithoutExtension(path), w, h); }
                    catch (Exception pe) { Debug.LogError("[UICap-HL] settled-layout probe threw: " + pe); }
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

                // THE BLANK GUARD: measure the pixels before shipping them. A flat frame is
                // the no-graphics failure mode, not a screenshot -- refuse to write it, so a
                // reviewer sees an honest MISSING instead of a convincing empty rectangle,
                // and UI_CAPTURE_OK cannot be inflated by frames with nothing in them.
                if (IsBlank(tex, out string measure))
                {
                    Debug.LogError("[UICap-HL] BLANK RENDER (not written): " + path +
                                   " -- " + measure + ". Counted as a FAILURE, not a shot.");
                    return false;
                }

                byte[] png = tex.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    Debug.LogWarning("[UICap-HL] " + path + " -- EncodeToPNG produced no bytes; skipped.");
                    return false;
                }

                File.WriteAllBytes(path, png);
                Debug.Log("[UICap-HL] saved " + w + "x" + h + " -> " + Path.GetFullPath(path) +
                          " (" + png.Length + " bytes, " + measure + ")");
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

        // =====================================================================
        //  AuditGeometry -- the numeric layout gate (2026-08-05).
        // ---------------------------------------------------------------------
        //  A screenshot only catches a layout defect if a HUMAN opens it and sees the
        //  defect. That is not a gate; it is a hope. Two RCAs on 2026-08-05 landed on
        //  the same finding -- the harness was structurally blind, and green runs
        //  shipped broken panels to the owner.
        //
        //  So every captured canvas is now MEASURED on its settled layout. All rects
        //  are converted into ROOT-CANVAS LOCAL SPACE, which is the kit's reference-px
        //  space -- the same units MinTouchPx and the zone fractions are authored in --
        //  so every number in a failure message is directly comparable to the source.
        //
        //  RULE 1 [text-off-plate]  A TMP_Text under a kit Zone_Body must be fully
        //     inside that body's ZoneBacking rect. The ZoneBacking IS the black plate
        //     (ElarionUiKit ~line 690: ZoneBacking(layout.body, ObsidianFill)), and
        //     "the caption fell off the black" is the exact founding-Echo-card defect.
        //     Text under a RectMask2D/Mask is SKIPPED: masked content is clipped by
        //     construction and cannot visibly spill.
        //
        //  RULE 2 [button-overlap]  Two SIBLING Buttons must not overlap. Sibling
        //     buttons are bands laid in one host, and an overlap there is the
        //     "options stacked" / "only the bottom chip is tappable" defect.
        //
        //  RULE 3 [button-over-text]  A VISIBLE Button must not overlap a TMP_Text it
        //     does not own. Its own label is a descendant, so it is excluded; INVISIBLE
        //     buttons (no targetGraphic, or alpha < 0.05) are excluded too -- those are
        //     hit-area/scrim overlays and cannot collide visually with anything.
        //
        //  RULE 4 [sub-touch-floor]  A kit button (one carrying the ClampMinTouch guard)
        //     whose AUTHORED band resolves under ElarionUiKit.MinTouchPx. The guard grows
        //     it in LateUpdate, which never runs in an edit-mode capture -- so what this
        //     harness measures IS the pre-grow authored size. That is the point: the
        //     sub-floor band is the DEFECT SIGNATURE; the symmetric growth is only its
        //     consequence, and by the time you can see the growth the neighbour is
        //     already overlapped.
        //
        //  Elements that are clipped and NOT fully inside their clipper are skipped by
        //  rules 2 and 3: a scrolled-out row's pixel adjacency is unknowable.
        // =====================================================================
        private static readonly List<string> _geoFailures = new List<string>();
        private static int _geoCanvasesChecked;

        /// <summary>Containment slack, reference px. Sub-pixel seams are not defects.</summary>
        private const float GeoContainSlackPx = 1.5f;
        /// <summary>Overlap must exceed this on BOTH axes to count, reference px.</summary>
        private const float GeoOverlapPadPx = 2f;
        /// <summary>A Button covering this fraction of the canvas is a scrim, not a control.</summary>
        private const float GeoScrimAreaFraction = 0.80f;
        /// <summary>Cap on printed failure lines (all are still counted in the marker).</summary>
        private const int GeoMaxPrintedLines = 60;

        private static void AuditGeometry(GameObject canvasGo, string label, int w, int h)
        {
            RectTransform root = canvasGo != null ? canvasGo.GetComponent<RectTransform>() : null;
            if (root == null) return;

            _geoCanvasesChecked++;
            var fails = new List<string>();
            string at = " [" + label + " @" + w + "x" + h + "]";

            try
            {
                TMP_Text[] texts = canvasGo.GetComponentsInChildren<TMP_Text>(false);
                Button[] buttons = canvasGo.GetComponentsInChildren<Button>(false);

                if (!TryRectInRoot(root, root, out Rect canvasRect)) canvasRect = new Rect(0f, 0f, w, h);
                float canvasArea = Mathf.Max(1f, canvasRect.width * canvasRect.height);

                // ---- RULE 1: text must stay on its panel's black plate -------------
                foreach (var t in texts)
                {
                    if (t == null || !t.enabled || !t.gameObject.activeInHierarchy) continue;
                    if (string.IsNullOrEmpty(t.text) || t.color.a < 0.05f) continue;
                    var trt = t.transform as RectTransform;
                    if (!TryRectInRoot(trt, root, out Rect tr)) continue;

                    RectTransform body = ZoneBodyAbove(t.transform);
                    if (body == null) continue;                                  // header/footer/close copy
                    if (NearestClipper(t.transform, body.transform) != null) continue;   // masked well

                    RectTransform plate = PlateOf(body);
                    if (!TryRectInRoot(plate, root, out Rect pr)) continue;

                    float over = OutsideBy(tr, pr);
                    if (over > GeoContainSlackPx)
                        fails.Add("TEXT OFF PLATE" + at + " '" + PathOf(t.transform, canvasGo.transform) +
                                  "' (\"" + Snippet(t.text) + "\") overflows its layout.body ZoneBacking by " +
                                  over.ToString("0.#") + " ref px -- text " + RectStr(tr) +
                                  " vs plate " + RectStr(pr) +
                                  ". This is the founding-Echo-card defect: copy rendered off the black.");
                }

                // ---- RULE 2: sibling buttons must not overlap ----------------------
                for (int i = 0; i < buttons.Length; i++)
                {
                    var a = buttons[i];
                    if (!ButtonUsable(a, root, canvasGo.transform, canvasArea, out Rect ar)) continue;
                    for (int j = i + 1; j < buttons.Length; j++)
                    {
                        var b = buttons[j];
                        if (b == null || a.transform.parent != b.transform.parent) continue;
                        if (!ButtonUsable(b, root, canvasGo.transform, canvasArea, out Rect br)) continue;
                        if (!Overlaps(ar, br, GeoOverlapPadPx, out float ow, out float oh)) continue;
                        fails.Add("BUTTONS OVERLAP" + at + " siblings '" +
                                  PathOf(a.transform, canvasGo.transform) + "' " + RectStr(ar) + " and '" +
                                  PathOf(b.transform, canvasGo.transform) + "' " + RectStr(br) +
                                  " share " + ow.ToString("0.#") + "x" + oh.ToString("0.#") +
                                  " ref px -- two tap targets in one place; only one can win the raycast.");
                    }
                }

                // ---- RULE 3: a visible button must not sit on foreign text ---------
                foreach (var b in buttons)
                {
                    if (!ButtonUsable(b, root, canvasGo.transform, canvasArea, out Rect br)) continue;
                    if (!HasVisibleGraphic(b)) continue;     // hit areas / scrims cannot collide visually
                    foreach (var t in texts)
                    {
                        if (t == null || !t.enabled || !t.gameObject.activeInHierarchy) continue;
                        if (string.IsNullOrEmpty(t.text) || t.color.a < 0.05f) continue;
                        if (IsDescendantOf(t.transform, b.transform)) continue;   // its own label
                        if (IsDescendantOf(b.transform, t.transform)) continue;
                        if (ClippedOut(t.transform, canvasGo.transform, root)) continue;
                        var trt = t.transform as RectTransform;
                        if (!TryRectInRoot(trt, root, out Rect tr)) continue;
                        if (!Overlaps(br, tr, GeoOverlapPadPx, out float ow, out float oh)) continue;
                        fails.Add("BUTTON OVER TEXT" + at + " '" + PathOf(b.transform, canvasGo.transform) +
                                  "' " + RectStr(br) + " covers '" + PathOf(t.transform, canvasGo.transform) +
                                  "' (\"" + Snippet(t.text) + "\") " + RectStr(tr) + " by " +
                                  ow.ToString("0.#") + "x" + oh.ToString("0.#") + " ref px.");
                    }
                }

                // ---- RULE 4: authored band under the kit touch floor ---------------
                foreach (var b in buttons)
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    if (!HasMinTouchGuard(b)) continue;      // not a kit button; not this rule's contract
                    var brt = b.transform as RectTransform;
                    if (!TryRectInRoot(brt, root, out Rect br)) continue;
                    float shortest = Mathf.Min(br.width, br.height);
                    if (shortest >= ElarionUiKit.MinTouchPx - 0.5f) continue;
                    fails.Add("SUB-TOUCH-FLOOR BAND" + at + " '" + PathOf(b.transform, canvasGo.transform) +
                              "' resolves " + br.width.ToString("0.#") + "x" + br.height.ToString("0.#") +
                              " ref px -- shortest side " + shortest.ToString("0.#") + " is " +
                              (ElarionUiKit.MinTouchPx - shortest).ToString("0.#") + " px UNDER " +
                              "ElarionUiKit.MinTouchPx (" + ElarionUiKit.MinTouchPx.ToString("0.#") +
                              "). ClampMinTouch will grow it SYMMETRICALLY about its centre at runtime and " +
                              "spill it into both neighbours. Author the band AT the floor.");
                }
            }
            catch (Exception e)
            {
                fails.Add("GEOMETRY AUDIT THREW" + at + " " + e.GetType().Name + ": " + e.Message);
            }

            _geoFailures.AddRange(fails);
        }

        private static void ReportGeometry()
        {
            if (_geoCanvasesChecked == 0)
            {
                Debug.LogError("UI_GEOMETRY_FAIL x0 -- ZERO canvases were measured, so the geometry gate " +
                               "proved nothing this run (a green UI_CAPTURE_OK without it is the old blindness).");
                return;
            }
            if (_geoFailures.Count == 0)
            {
                Debug.Log("UI_GEOMETRY_OK " + _geoCanvasesChecked + " canvases -- no text off its plate, " +
                          "no overlapping sibling buttons, no button on foreign text, no authored band " +
                          "under the " + ElarionUiKit.MinTouchPx.ToString("0.#") + " px touch floor");
                return;
            }

            int shown = Mathf.Min(_geoFailures.Count, GeoMaxPrintedLines);
            for (int i = 0; i < shown; i++) Debug.LogError("[UICap-GEO] " + _geoFailures[i]);
            if (_geoFailures.Count > shown)
                Debug.LogError("[UICap-GEO] ... and " + (_geoFailures.Count - shown) + " more");

            Debug.LogError("UI_GEOMETRY_FAIL x" + _geoFailures.Count + " over " + _geoCanvasesChecked +
                           " canvases -- see the [UICap-GEO] lines above; each names the panel, the " +
                           "element and the numbers.");
        }

        // ---------------------------------------------------------------------
        //  Geometry helpers (all measurements in ROOT-CANVAS local = reference px).
        // ---------------------------------------------------------------------
        private static bool TryRectInRoot(RectTransform rt, RectTransform root, out Rect r)
        {
            r = default(Rect);
            if (rt == null || root == null) return false;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = root.InverseTransformPoint(c[i]);
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            if (float.IsNaN(minX) || float.IsNaN(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
                return false;
            r = new Rect(minX, minY, maxX - minX, maxY - minY);
            return r.width > 0.5f && r.height > 0.5f;
        }

        /// <summary>How far <paramref name="inner"/> pokes outside <paramref name="outer"/> (px; &lt;=0 = contained).</summary>
        private static float OutsideBy(Rect inner, Rect outer)
        {
            float left = outer.xMin - inner.xMin;
            float right = inner.xMax - outer.xMax;
            float bottom = outer.yMin - inner.yMin;
            float top = inner.yMax - outer.yMax;
            return Mathf.Max(Mathf.Max(left, right), Mathf.Max(bottom, top));
        }

        private static bool Overlaps(Rect a, Rect b, float pad, out float ow, out float oh)
        {
            ow = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            oh = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return ow > pad && oh > pad;
        }

        /// <summary>The kit body zone above <paramref name="t"/>, if any (ElarionUiKit names it Zone_Body).</summary>
        private static RectTransform ZoneBodyAbove(Transform t)
        {
            for (Transform p = t != null ? t.parent : null; p != null; p = p.parent)
                if (string.Equals(p.name, "Zone_Body", StringComparison.Ordinal)) return p as RectTransform;
            return null;
        }

        /// <summary>The black plate of a body zone: its ZoneBacking child, else the zone itself.</summary>
        private static RectTransform PlateOf(RectTransform body)
        {
            if (body == null) return null;
            for (int i = 0; i < body.childCount; i++)
            {
                Transform ch = body.GetChild(i);
                if (ch != null && string.Equals(ch.name, "ZoneBacking", StringComparison.Ordinal))
                    return ch as RectTransform;
            }
            return body;
        }

        /// <summary>Nearest masking ancestor between <paramref name="t"/> and <paramref name="stopAt"/> (exclusive).</summary>
        private static RectTransform NearestClipper(Transform t, Transform stopAt)
        {
            for (Transform p = t != null ? t.parent : null; p != null; p = p.parent)
            {
                if (p == stopAt) break;
                if (p.GetComponent<RectMask2D>() != null || p.GetComponent<Mask>() != null)
                    return p as RectTransform;
            }
            return null;
        }

        /// <summary>True when the element is clipped and not FULLY inside its clipper (scrolled out).</summary>
        private static bool ClippedOut(Transform t, Transform canvasRoot, RectTransform root)
        {
            RectTransform clip = NearestClipper(t, canvasRoot);
            if (clip == null) return false;
            var rt = t as RectTransform;
            if (!TryRectInRoot(rt, root, out Rect er)) return true;
            if (!TryRectInRoot(clip, root, out Rect cr)) return true;
            return OutsideBy(er, cr) > GeoContainSlackPx;
        }

        private static bool ButtonUsable(Button b, RectTransform root, Transform canvasRoot,
                                         float canvasArea, out Rect r)
        {
            r = default(Rect);
            if (b == null || !b.gameObject.activeInHierarchy) return false;
            var brt = b.transform as RectTransform;
            if (!TryRectInRoot(brt, root, out r)) return false;
            if (r.width * r.height >= canvasArea * GeoScrimAreaFraction) return false;   // scrim
            if (ClippedOut(b.transform, canvasRoot, root)) return false;
            return true;
        }

        private static bool HasVisibleGraphic(Button b)
        {
            var g = b != null ? b.targetGraphic : null;
            return g != null && g.enabled && g.color.a >= 0.05f;
        }

        /// <summary>The ClampMinTouch guard is a PRIVATE nested type in the kit -- match by name.</summary>
        private static bool HasMinTouchGuard(Button b)
        {
            if (b == null) return false;
            foreach (var mb in b.GetComponents<MonoBehaviour>())
                if (mb != null && string.Equals(mb.GetType().Name, "UiKitMinTouchGuard", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsDescendantOf(Transform child, Transform ancestor)
        {
            if (child == null || ancestor == null) return false;
            for (Transform p = child; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }

        private static string PathOf(Transform t, Transform stopAt)
        {
            if (t == null) return "<null>";
            string s = t.name;
            for (Transform p = t.parent; p != null && p != stopAt; p = p.parent)
                s = p.name + "/" + s;
            return s;
        }

        private static string RectStr(Rect r)
        {
            return "(x " + r.xMin.ToString("0.#") + ".." + r.xMax.ToString("0.#") +
                   ", y " + r.yMin.ToString("0.#") + ".." + r.yMax.ToString("0.#") + ")";
        }

        private static string Snippet(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length <= 48 ? s : s.Substring(0, 45) + "...";
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

        // =====================================================================
        //  RAID PILLAR CAPTURES (2026-08-16).
        // ---------------------------------------------------------------------
        //  Sixteen cases shipped before today and not one of them touched raids --
        //  the pillar the owner explicitly asked to verify from screenshots. Three
        //  cases close that hole, each following the established idiom exactly:
        //  ForEachTarget owns the per-resolution build, the body owns its own
        //  DestroyImmediate teardown, and every risky step is inside the try so a
        //  refusing raid screen costs ONE frame, never the remaining sixteen cases.
        //
        //  THE ARMY GATE, and how this harness gets past it honestly.
        //  RaidSelectionScreen.Open() refuses unless ArmyReadiness.Compute says the
        //  army is full. In an edit-mode capture there is no GameStateService, and
        //  Compute's documented stateless rule ("null st/Army publishes READY with
        //  zeros so headless/AutoPilot never false-blocks") returns Ready -- so the
        //  grid opens on the REAL static entry, with the REAL gate evaluated, not
        //  driven around it. RaidTestFlagScope additionally raises ff.raidtest for the
        //  duration as a belt-and-braces: if a future state seeds a partial army into
        //  this process, the capture still gets its frame instead of silently shooting
        //  the drillmaster redirect. The scope RESTORES the previous PlayerPrefs value
        //  (deleting the key when it was unset) so a capture run can never leave the
        //  owner's flags mutated.
        //
        //  TEARDOWN: neither screen's own Close() may be called here -- it routes
        //  through ElarionUiKit.ClosePanelWithFx, which outside play mode calls the
        //  runtime Destroy (edit-illegal). Both bodies release the arbiter handle by
        //  hand and DestroyImmediate the canvas FIRST, so the host's OnDestroy sees a
        //  dead _ui and skips its own Destroy -- the same contract the rumor-board and
        //  realm-map cases already keep.
        // =====================================================================

        /// <summary>
        /// Raise ff.raidtest for the life of a capture and put PlayerPrefs back exactly as it
        /// was found. A harness that leaves a bypass flag ON would silently change how the
        /// owner's next play session gates raids -- the restore is the load-bearing half.
        /// </summary>
        private sealed class RaidTestFlagScope : IDisposable
        {
            private const string Key = "ff.raidtest";
            private readonly int _prev;      // -1 == the key was not set at all

            public RaidTestFlagScope()
            {
                _prev = PlayerPrefs.GetInt(Key, -1);
                PlayerPrefs.SetInt(Key, 1);
                Debug.Log("[UICap-HL] ff.raidtest raised for the raid captures (previous value " +
                          (_prev < 0 ? "UNSET" : _prev.ToString()) + "); it is restored on scope exit. " +
                          "NOTE: headless has no GameState, so ArmyReadiness.Compute already returns " +
                          "READY by its stateless never-false-block rule -- this flag is the safety " +
                          "net, not the thing that opens the grid.");
            }

            public void Dispose()
            {
                try
                {
                    if (_prev < 0) PlayerPrefs.DeleteKey(Key);
                    else PlayerPrefs.SetInt(Key, _prev);
                    PlayerPrefs.Save();
                    Debug.Log("[UICap-HL] ff.raidtest restored to " +
                              (_prev < 0 ? "UNSET (key deleted)" : _prev.ToString()) + ".");
                }
                catch (Exception e)
                {
                    Debug.LogError("[UICap-HL] ff.raidtest RESTORE FAILED: " + e +
                                   " -- the owner's PlayerPrefs may still carry the bypass flag. " +
                                   "Clear it by hand (PlayerPrefs key 'ff.raidtest').");
                }
            }
        }

        // ---------------------------------------------------------------------
        //  Panel: RaidSelectionScreen -- the grid of raid cards. This is the screen
        //  that was hard-refusing to open, so a frame of it OPEN is the single most
        //  valuable raid picture. Shot through the real static Open(), which builds
        //  the real chrome + the real RaidSelectionVM projection off
        //  SceneConfigCatalog (the three flagship enemy raids, falling back to every
        //  enemy raid). An empty catalog renders the panel's own "No raids available."
        //  empty state -- which is still a true picture and is logged as such.
        // ---------------------------------------------------------------------
        private static int CaptureRaidSelection()
        {
            using (new RaidTestFlagScope())
                return ForEachTarget("RaidSelection", CaptureRaidSelectionOnce);
        }

        private static int CaptureRaidSelectionOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject canvasGo = null;
            RaidSelectionScreen screen = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                int raidCount = 0;
                var probe = RaidSelectionVM.CreateDefault(null);
                if (probe != null)
                {
                    raidCount = probe.Raids != null ? probe.Raids.Count : 0;
                    probe.Dispose();
                }
                if (raidCount == 0)
                {
                    Debug.LogWarning("[UICap-HL] SceneConfigCatalog projected ZERO enemy raids -- the raid " +
                                     "grid will render its 'No raids available.' empty state. The shot is " +
                                     "still honest, but it proves the CHROME opens, not that cards build.");
                }

                // The REAL static entry -- the same call the HUD Raids face reaches through
                // RaidEntryGate, army gate included (see the banner for why it passes headless).
                RaidSelectionScreen.Open();

                // Awake never runs on an edit-mode AddComponent, so the screen's own _instance
                // cache is always null here and Open() authors a fresh host each time; find it.
                screen = UnityEngine.Object.FindAnyObjectByType<RaidSelectionScreen>();
                if (screen == null)
                {
                    Debug.LogWarning("[UICap-HL] RaidSelectionScreen.Open() produced no screen instance -- " +
                                     "the army gate refused (or the host was not created). Raid grid skipped " +
                                     "for " + target.Tag + "; the remaining captures continue.");
                    return 0;
                }

                canvasGo = GetPrivateGameObject(screen, "_ui");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] RaidSelectionScreen._ui null after Open -- the grid did not " +
                                     "build; raid grid skipped for " + target.Tag + ".");
                    return 0;
                }

                if (RenderCanvasToPng(canvasGo, OutDir + "RaidSelection_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] raid selection capture threw: " + e);
            }
            finally
            {
                ReleaseScreenHandle(screen, "raid selection");
                // Canvas FIRST so the host's OnDestroy sees a dead _ui and never calls the
                // runtime Destroy (edit-illegal) -- the shared teardown contract.
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (screen != null && screen.gameObject != null)
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        // ---------------------------------------------------------------------
        //  Panel: RaidDeployScreen -- the pre-raid deploy screen (party row, troop
        //  list, scout report, BEGIN ASSAULT). No test and no bot had ever exercised
        //  this path, so the first frame of it is genuinely new information.
        //
        //  HONEST LIMIT, stated rather than dressed up: with no GameStateService in
        //  edit mode RaidDeployVM.CreateDefault resolves a NULL army, so the troop
        //  list renders its "No troops trained yet." empty state and the counts read
        //  zero. That is the real zero-army layout (exactly what a player with an
        //  empty barracks sees) -- it is NOT proof that a populated roster lays out.
        //  Seeding an army would need a live GameStateService, which this harness
        //  deliberately does not stand up.
        // ---------------------------------------------------------------------
        private static int CaptureRaidDeploy()
        {
            using (new RaidTestFlagScope())
                return ForEachTarget("RaidDeploy", CaptureRaidDeployOnce);
        }

        private static int CaptureRaidDeployOnce(CaptureTarget target)
        {
            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject canvasGo = null;
            RaidDeployScreen screen = null;
            RaidSelectionVM vm = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                // The def the player would have tapped: the FIRST card the real grid projects,
                // resolved through the same VM the grid uses (never a hand-built fixture def).
                vm = RaidSelectionVM.CreateDefault(null);
                SceneConfigDef def = null;
                if (vm != null && vm.Raids != null)
                {
                    foreach (var item in vm.Raids)
                    {
                        def = vm.DefFor(item.Id);
                        if (def != null) break;
                    }
                }
                if (def == null)
                {
                    Debug.LogWarning("[UICap-HL] no SceneConfigDef projected for any raid card -- the deploy " +
                                     "screen has nothing to open for; skipped at " + target.Tag + ".");
                    return 0;
                }

                RaidDeployScreen.Open(def);

                screen = UnityEngine.Object.FindAnyObjectByType<RaidDeployScreen>();
                if (screen == null)
                {
                    Debug.LogWarning("[UICap-HL] RaidDeployScreen.Open(def) produced no screen instance -- " +
                                     "deploy screen skipped at " + target.Tag + ".");
                    return 0;
                }

                canvasGo = GetPrivateGameObject(screen, "_ui");
                if (canvasGo == null)
                {
                    Debug.LogWarning("[UICap-HL] RaidDeployScreen._ui null after Open -- the deploy screen " +
                                     "did not build; skipped at " + target.Tag + ".");
                    return 0;
                }

                Debug.Log("[UICap-HL] raid deploy shot is the ZERO-ARMY state (no GameStateService in edit " +
                          "mode -> RaidDeployVM resolves a null army). Read it as the empty-barracks layout, " +
                          "not as proof that a populated troop list fits.");

                if (RenderCanvasToPng(canvasGo, OutDir + "RaidDeploy_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] raid deploy capture threw: " + e);
            }
            finally
            {
                if (vm != null) vm.Dispose();
                ReleaseScreenHandle(screen, "raid deploy");
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (screen != null && screen.gameObject != null)
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
        }

        /// <summary>Release a raid screen's single-modal arbiter slot by hand. Its own Close()
        /// cannot be used (ClosePanelWithFx -> runtime Destroy is edit-illegal), and a leaked
        /// handle would make the NEXT panel in the run open against a busy arbiter.</summary>
        private static void ReleaseScreenHandle(object screen, string what)
        {
            if (screen == null) return;
            try
            {
                var handle = GetPrivateFieldValue(screen, "_panelHandle") as PanelHandle;
                if (handle != null) PanelManager.NotifyClosed(handle);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UICap-HL] " + what + " arbiter release failed (harmless): " + e.Message);
            }
        }

        // ---------------------------------------------------------------------
        //  Panel: the bottom action bar's RAIDS FACE in its THREE states (WO-1008).
        //  The face used to VANISH with an empty army; it is now visible-and-greyed
        //  and carries the reason in WORDS ("Raids 0/5" / "Raids 3/5") because the
        //  owner is red/green colourblind and a hue tells her nothing. Whether that
        //  reads at a glance is a question ONLY a screenshot can answer -- so all
        //  three states are shot side by side in ONE frame per target, which is what
        //  makes them comparable.
        //
        //  Not a view-only mock: each row is painted from a REAL HudActionBarModel
        //  (the Core applicability model) driven over its ISource seam, so the face
        //  set, the dim decision AND the label text all come from the shipped compute
        //  -- exactly what HudKitController.ApplyRaidsDim renders. The kit builds the
        //  faces (BuildObsidianButton, same style/colour arguments as HudKitController),
        //  so the plate, the font and the touch floor are the shipped ones too.
        // ---------------------------------------------------------------------
        private sealed class FakeBarSource : DeNelle.Core.HudModel.HudActionBarModel.ISource
        {
            public bool TalkAvailable { get; set; }
            public bool RaidCapable { get; set; }
            public bool RaidArmyReady { get; set; }
            public int RaidDeployableSlots { get; set; }
            public int RaidQueuedSlots { get; set; }
            public int RaidCapSlots { get; set; }
            public bool MapUnlocked { get; set; }
            public bool BuildingFocused { get; set; }
        }

        private struct RaidFaceState
        {
            public string Caption;
            public FakeBarSource Source;
        }

        private static RaidFaceState[] BuildRaidFaceStates()
        {
            return new[]
            {
                new RaidFaceState
                {
                    Caption = "LIVE - army full, the face is undimmed and reads 'Raids'",
                    Source = new FakeBarSource
                    {
                        RaidCapable = true, RaidArmyReady = true,
                        RaidDeployableSlots = 5, RaidQueuedSlots = 0, RaidCapSlots = 5,
                    },
                },
                new RaidFaceState
                {
                    Caption = "GREYED / NO TROOPS - barracks built, nothing trained (WO-1008: visible, not hidden)",
                    Source = new FakeBarSource
                    {
                        RaidCapable = true, RaidArmyReady = false,
                        RaidDeployableSlots = 0, RaidQueuedSlots = 0, RaidCapSlots = 5,
                    },
                },
                new RaidFaceState
                {
                    Caption = "GREYED / PARTIAL ARMY - 2 ready + 1 training of 5",
                    Source = new FakeBarSource
                    {
                        RaidCapable = true, RaidArmyReady = false,
                        RaidDeployableSlots = 2, RaidQueuedSlots = 1, RaidCapSlots = 5,
                    },
                },
            };
        }

        // The kit face arguments per id, verbatim from HudKitController's bar build.
        private static string BarFaceWord(DeNelle.Core.HudModel.ActionBarButtonId id)
        {
            switch (id)
            {
                case DeNelle.Core.HudModel.ActionBarButtonId.Build: return "Build";
                case DeNelle.Core.HudModel.ActionBarButtonId.Talk: return "Talk";
                case DeNelle.Core.HudModel.ActionBarButtonId.Bag: return "Bag";
                case DeNelle.Core.HudModel.ActionBarButtonId.Raids:
                    return DeNelle.Core.HudModel.HudActionBarModel.RaidsBaseLabel;
                case DeNelle.Core.HudModel.ActionBarButtonId.Quests: return "Quests";
                case DeNelle.Core.HudModel.ActionBarButtonId.Upgrade: return "Manage";
                default: return id.ToString();
            }
        }

        private static ElarionUiKit.ObsidianButtonColor BarFaceColor(DeNelle.Core.HudModel.ActionBarButtonId id)
        {
            if (id == DeNelle.Core.HudModel.ActionBarButtonId.Build)
                return ElarionUiKit.ObsidianButtonColor.Yellow;
            if (id == DeNelle.Core.HudModel.ActionBarButtonId.Talk)
                return ElarionUiKit.ObsidianButtonColor.Green;
            return ElarionUiKit.ObsidianButtonColor.Gray;
        }

        private static int CaptureRaidsFaceStates()
        {
            return ForEachTarget("RaidsFaceStates", CaptureRaidsFaceStatesOnce);
        }

        private static int CaptureRaidsFaceStatesOnce(CaptureTarget target)
        {
            // HudKitController's own bar geometry, so the slot widths are the shipped ones.
            const float barGap = 0.01f;
            float barSlotW = (1f - barGap * (DeNelle.Core.HudModel.HudActionBarModel.MaxVisibleFaces - 1))
                             / DeNelle.Core.HudModel.HudActionBarModel.MaxVisibleFaces;

            int saved = 0;
            GameObject tempEventSystem = null;
            GameObject canvasGo = null;

            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    tempEventSystem = new GameObject("~UICapEventSystem");
                    tempEventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }

                canvasGo = new GameObject("~UICapRaidsFace",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 4000;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);   // HudAreasHost's scaler
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // Dark backdrop so the greyed plate's contrast reads honestly in the PNG.
                var bg = new GameObject("Backdrop", typeof(Image));
                bg.transform.SetParent(canvasGo.transform, false);
                var bgrt = (RectTransform)bg.transform;
                bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
                bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
                bg.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.12f, 1f);

                var root = (RectTransform)canvasGo.transform;
                var title = MakePixelBand(root, "Title", 8f, 52f, 24f);
                var titleLbl = title.gameObject.AddComponent<TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(titleLbl);
                titleLbl.text = "ACTION BAR - RAIDS FACE, THREE STATES (WO-1008)";
                titleLbl.fontSize = ElarionUi.FontLabel;
                titleLbl.color = ElarionUi.Gilt;
                titleLbl.fontStyle = FontStyles.Bold;
                titleLbl.alignment = TextAlignmentOptions.MidlineLeft;
                titleLbl.raycastTarget = false;

                var states = BuildRaidFaceStates();
                float y = 84f;
                foreach (var state in states)
                {
                    // The REAL Core compute decides the set, the dim and the words.
                    var model = new DeNelle.Core.HudModel.HudActionBarModel(state.Source);
                    model.SetPosture(DeNelle.Core.HudModel.HudActionBarModel.PostureTown);

                    var cap = MakePixelBand(root, "Caption", y, 44f, 24f);
                    var capLbl = cap.gameObject.AddComponent<TextMeshProUGUI>();
                    ElarionUiKit.EnsureFont(capLbl);
                    capLbl.text = state.Caption + "   [model: dim=" + model.RaidsDimmed +
                                  " reason=" + model.RaidsDimReason + " label='" + model.RaidsFaceLabel + "']";
                    capLbl.fontSize = ElarionUi.FontMicro;
                    capLbl.color = ElarionUi.Parchment;
                    capLbl.alignment = TextAlignmentOptions.MidlineLeft;
                    capLbl.raycastTarget = false;
                    y += 50f;

                    var barBand = MakePixelBand(root, "Bar", y, ElarionUiKit.MinTouchPx, 24f);
                    var actives = model.Active;
                    int n = actives != null ? actives.Count : 0;
                    float groupW = n > 0 ? n * barSlotW + (n - 1) * barGap : 0f;
                    float x = (1f - groupW) * 0.5f;
                    for (int i = 0; i < n; i++)
                    {
                        var id = actives[i];
                        bool isRaids = id == DeNelle.Core.HudModel.ActionBarButtonId.Raids;
                        // WO-1008: the Raids face carries the model's STATE label, every other
                        // face its plain word -- exactly what ApplyRaidsDim paints.
                        string word = isRaids ? model.RaidsFaceLabel : BarFaceWord(id);
                        var face = ElarionUiKit.BuildObsidianButton(barBand, word,
                            ElarionUiKit.ObsidianButtonStyle.Style1, BarFaceColor(id),
                            new Vector2(x, 0f), new Vector2(x + barSlotW, 1f), null);
                        if (isRaids && model.RaidsDimmed && face != null)
                        {
                            // ApplyRaidsDim, verbatim: tint face + label toward Disabled and
                            // NEVER touch interactable (a dimmed tap still reaches the redirect).
                            var faceImg = face.targetGraphic as Image;
                            if (faceImg != null) faceImg.color = ElarionUi.Disabled;
                            var faceLbl = face.GetComponentInChildren<TMP_Text>(true);
                            if (faceLbl != null) faceLbl.color = ElarionUi.Disabled;
                        }
                        x += barSlotW + barGap;
                    }
                    if (n == 0)
                    {
                        Debug.LogWarning("[UICap-HL] raids-face state '" + state.Caption + "' produced an " +
                                         "EMPTY action-bar set -- the row will be blank in the shot.");
                    }
                    y += ElarionUiKit.MinTouchPx + 42f;
                }

                Canvas.ForceUpdateCanvases();
                if (RenderCanvasToPng(canvasGo, OutDir + "RaidsFaceStates_" + target.Tag + ".png",
                    target.W, target.H)) saved++;
            }
            catch (Exception e)
            {
                Debug.LogError("[UICap-HL] raids face states capture threw: " + e);
            }
            finally
            {
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (tempEventSystem != null) UnityEngine.Object.DestroyImmediate(tempEventSystem);
            }

            return saved;
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
