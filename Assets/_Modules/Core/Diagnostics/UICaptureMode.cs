// =============================================================================
// UICaptureMode -- graphics-enabled UI screenshot harness (owner directive
// 2026-06-28, docs/qa/UI_CAPTURE_TEST_SCENARIOS.md).
// -----------------------------------------------------------------------------
// WHY: the headless AutoPilot fleet runs -nographics, so its break_*.png frames
// are BLANK. To get REAL pixels of every gameplay panel we need a GRAPHICS-
// ENABLED run (a display/GPU present). This harness boots a gameplay scene,
// opens each panel DETERMINISTICALLY (via PanelRouter.Open / a controller's own
// Open()/Show()/Toggle() -- NEVER click simulation), waits for layout, and calls
// ScreenCapture.CaptureScreenshot() for a LANDSCAPE (1920x1080) and a PORTRAIT
// (1080x2340) pass, then exits clean.
//
// EDITOR / GRAPHICS-BOX TOOLING ONLY:
//   * A real player build WITH a display produces real pixels.
//   * -nographics produces BLANK frames -- but the [Flow:UICap] lines still prove
//     the DRIVE ran (which panels opened, at what resolution), per the scenarios
//     doc. So a -nographics log is a useful smoke test even without pixels.
//   * This file itself does NOT force -nographics; the CALLER runs graphics-ON.
//
// HOW TO RUN:
//   * Player build (the reliable "real pixels" path): launch the graphics player
//     with the launch arg  -uiCapture  (a development build -- this file is gated
//     #if DEVELOPMENT_BUILD || UNITY_EDITOR).
//   * Editor batchmode: -executeMethod DeNelle.Editor.UICaptureLaunch.RunCapture
//     (the editor launch hook enters Play mode with the capture flag set), OR
//     -executeMethod DeNelle.Diagnostics.UICaptureMode.RunCapture. Keep the editor
//     CLOSED so the project isn't locked; do NOT pass -quit (the harness exits itself).
//   * Editor menu: Defenders/UI/Capture UI Panels.
//
// OUTPUT: Builds/UICaps/<name>.png (landscape) + Builds/UICaps/portrait/<name>.png.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Diagnostics
{
    /// <summary>
    /// Graphics-enabled UI capture harness. Spawns itself at boot when the run
    /// opted in via the <c>-uiCapture</c> command-line arg (player) or the editor
    /// SessionState flag set by <c>RunCapture()</c> / the editor launch hook.
    /// Compiled out of release builds.
    /// </summary>
    public sealed class UICaptureMode : MonoBehaviour
    {
        // Shared with the editor launch hook (DeNelle.Editor.UICaptureLaunch) so a
        // -executeMethod / menu invocation can request a capture across the domain
        // reload that entering Play mode triggers. SessionState survives the reload.
        public const string EditorRequestKey = "DeNelle.UICapture.Requested";

        private const string Tag = "UICap";
        private const string HostName = "~UICaptureMode";
        private const string BootScene = "MainCastle_Hall";
        private const string OutRoot = "Builds/UICaps/";

        private static bool s_booted;
        private bool _running;

        // ---------------------------------------------------------------------
        //  Boot hook: spawn the harness once, only when a capture was requested.
        // ---------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (s_booted) return;
            try
            {
                if (!ShouldRun()) return;
                s_booted = true;
                ConsumeEditorFlag();

                var go = new GameObject(HostName);
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<UICaptureMode>();
            }
            catch (Exception e)
            {
                // A diagnostic must never break startup.
                try { Debug.LogWarning("[UICap] AutoBoot failed: " + e.Message); } catch { }
            }
        }

        /// <summary>
        /// Editor entry: request a capture, then enter Play mode (a graphics-enabled
        /// batchmode play session renders real pixels; a -nographics one logs the
        /// drive). In a player this is a no-op -- launch with the <c>-uiCapture</c>
        /// arg instead. Callable via
        /// <c>-executeMethod DeNelle.Diagnostics.UICaptureMode.RunCapture</c>.
        /// </summary>
        public static void RunCapture()
        {
#if UNITY_EDITOR
            UnityEditor.SessionState.SetBool(EditorRequestKey, true);
            if (!UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorApplication.EnterPlaymode();
#else
            Debug.LogWarning("[UICap] RunCapture() is an editor entry. In a player build, " +
                             "launch the graphics executable with the -uiCapture arg.");
#endif
        }

        /// <summary>True when this run asked for a capture (CLI arg or editor flag).</summary>
        private static bool ShouldRun()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null)
                {
                    foreach (var a in args)
                    {
                        if (string.IsNullOrEmpty(a)) continue;
                        if (a.Equals("-uiCapture", StringComparison.OrdinalIgnoreCase) ||
                            a.Equals("--uiCapture", StringComparison.OrdinalIgnoreCase) ||
                            a.Equals("-captureUI", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { }

#if UNITY_EDITOR
            try { if (UnityEditor.SessionState.GetBool(EditorRequestKey, false)) return true; }
            catch { }
#endif
            return false;
        }

        /// <summary>Clear the one-shot editor flag so a later normal Play doesn't re-trigger.</summary>
        private static void ConsumeEditorFlag()
        {
#if UNITY_EDITOR
            try { UnityEditor.SessionState.SetBool(EditorRequestKey, false); } catch { }
#endif
        }

        private void Start()
        {
            if (_running) return;
            _running = true;
            StartCoroutine(Run());
        }

        // ---------------------------------------------------------------------
        //  Scenario table -- every panel named in the UI_REVIEW review set.
        // ---------------------------------------------------------------------
        private enum Kind { Router, ReflectInstance, ReflectStatic, Unsupported }

        private sealed class Shot
        {
            public string File;          // output basename (no extension)
            public Kind Kind;
            public PanelId Id;           // Router
            public string TypeName;      // Reflect (full name)
            public string OpenMethod;    // Reflect
            public string CloseMethod;   // Reflect (optional, best-effort)
            public string Reason;        // Unsupported (why it is skipped)

            public static Shot Route(string file, PanelId id) =>
                new Shot { File = file, Kind = Kind.Router, Id = id };

            public static Shot Instance(string file, string type, string open, string close = null) =>
                new Shot { File = file, Kind = Kind.ReflectInstance, TypeName = type, OpenMethod = open, CloseMethod = close };

            public static Shot Static(string file, string type, string open) =>
                new Shot { File = file, Kind = Kind.ReflectStatic, TypeName = type, OpenMethod = open };

            public static Shot Skip(string file, string reason) =>
                new Shot { File = file, Kind = Kind.Unsupported, Reason = reason };
        }

        private static readonly Shot[] Scenarios =
        {
            // --- PanelRouter-registered panels (boot-registered or find-or-spawn) ---
            Shot.Route("store_packs",         PanelId.RealmStore),        // SCENARIO A (Store)
            Shot.Route("inv_weapons",         PanelId.Inventory),         // SCENARIO B (Inventory)
            Shot.Route("hero_skilltree",      PanelId.HeroSkillTree),     // HeroTalents -> HeroSkillTree
            Shot.Route("workshop_crafting",   PanelId.Crafting),          // Workshop / gear crafting
            Shot.Route("building_upgrade",    PanelId.BuildingUpgrade),
            Shot.Route("cosmetic_shop",       PanelId.CosmeticShop),
            Shot.Route("party_shop",          PanelId.PartyShop),
            Shot.Route("rumor_board",         PanelId.RumorBoard),
            Shot.Route("hero_loadout",        PanelId.HeroLoadout),
            Shot.Route("consumable_crafting", PanelId.ConsumableCrafting),// Alchemy bench
            Shot.Route("jeweler_crafting",    PanelId.JewelerCrafting),
            Shot.Route("equipment",           PanelId.EquipmentPanel),
            Shot.Route("game_guide",          PanelId.GameGuide),

            // --- Reflection-resolved panels (no PanelId; Core cannot ref these asmdefs) ---
            Shot.Instance("settings",     "DeNelle.Settings.SettingsController", "Open"),
            Shot.Instance("music_jukebox","DeNelle.Audio.MusicSelectionPanel",   "Open", "Close"),
            Shot.Instance("help_menu",    "DeNelle.HUD.HelpMenu",                "ToggleOverlay", "Close"),
            Shot.Static  ("bug_report",   "DeNelle.HUD.BugReportView",           "Open"),

            // --- Named in the review set but not deterministically openable here ---
            Shot.Skip("pet_skilltree", "RETIRED (2026-07-08) -- pet skill-tree stack deleted; nothing registers it."),
            Shot.Skip("dialogue",      "needs an active conversation (DialogueRunner) -- no deterministic zero-arg open."),
            Shot.Skip("hero_select",   "onboarding-scene controller (HeroSelectController) -- not present in the hub."),
            Shot.Skip("build_menu",    "BuildMode being edited by WO-746 in parallel -- intentionally not touched."),
        };

        // ---------------------------------------------------------------------
        //  Drive
        // ---------------------------------------------------------------------
        private IEnumerator Run()
        {
            // Focus-loss immunity: a windowed graphics run must not pause when the
            // owner uses her machine (mirrors AutoPilotDriver).
            Application.runInBackground = true;
            FlowTrace.Enabled = true;   // make sure the drive lines emit for this run

            FlowTrace.Step(Tag, "harness start (batchmode=" + Application.isBatchMode +
                                 ", graphicsDevice=" + SystemInfo.graphicsDeviceType +
                                 ", scene='" + SceneManager.GetActiveScene().name + "')");

            EnsureDir(OutRoot);
            EnsureDir(OutRoot + "portrait/");

            yield return BootToScene();

            // Let the boot bootstraps (RealmStore, Inventory, RumorBoard, Help, Music)
            // and any scene panels register with PanelRouter before we open them.
            float settle = 0f;
            while (settle < 2.0f) { settle += Time.unscaledDeltaTime; yield return null; }

            // LANDSCAPE pass (1920x1080).
            yield return CapturePass(string.Empty, 1920, 1080);

            // PORTRAIT pass (1080x2340).
            yield return CapturePass("portrait/", 1080, 2340);

            FlowTrace.Step(Tag, "harness complete -> " + Path.GetFullPath(OutRoot));
            Exit();
        }

        private IEnumerator BootToScene()
        {
            var active = SceneManager.GetActiveScene().name;
            if (active == BootScene)
            {
                FlowTrace.Step(Tag, "boot -> already in '" + BootScene + "'.");
                yield break;
            }

            FlowTrace.Step(Tag, "boot -> loading '" + BootScene + "' (from '" + active + "').");
            bool loaded = Guard.Try(Tag, "LoadScene(" + BootScene + ")", () => SceneManager.LoadScene(BootScene));
            if (!loaded)
            {
                FlowTrace.Warn(Tag, "boot -> LoadScene('" + BootScene + "') threw (in Build Settings?). " +
                                    "Capturing whatever scene is active instead.");
                yield break;
            }

            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != BootScene &&
                   Time.realtimeSinceStartup - t0 < 30f)
                yield return null;

            if (SceneManager.GetActiveScene().name != BootScene)
                FlowTrace.Warn(Tag, "boot -> '" + BootScene + "' never became active within 30s; continuing.");
            else
                FlowTrace.Step(Tag, "boot -> arrived in '" + BootScene + "'.");

            for (int i = 0; i < 3; i++) yield return null;   // let Awake/Start run
        }

        private IEnumerator CapturePass(string subDir, int w, int h)
        {
            Guard.Try(Tag, "SetResolution " + w + "x" + h, () => Screen.SetResolution(w, h, false));
            yield return null;
            yield return null;   // let the resolution change apply

            string label = string.IsNullOrEmpty(subDir) ? "landscape" : "portrait";
            FlowTrace.Step(Tag, "pass " + label + " target=" + w + "x" + h +
                                " (screen now " + Screen.width + "x" + Screen.height + ")");

            foreach (var shot in Scenarios)
            {
                // Clear any panel/overlay left open so shots never overlap.
                Guard.Try(Tag, "CloseAll before " + shot.File, PanelManager.CloseAll);
                yield return null;

                if (shot.Kind == Kind.Unsupported)
                {
                    FlowTrace.Warn(Tag, shot.File + " SKIPPED -- " + shot.Reason);
                    continue;
                }

                bool opened = OpenShot(shot);
                if (!opened)
                {
                    FlowTrace.Warn(Tag, shot.File + " open path unresolved (not registered / no instance / threw) -- skipped.");
                    continue;
                }

                // Two frames for layout to settle before the capture.
                yield return null;
                yield return null;

                string path = OutRoot + subDir + shot.File + ".png";
                Guard.Try(Tag, "CaptureScreenshot " + path, () => ScreenCapture.CaptureScreenshot(path, 1));
                FlowTrace.Step(Tag, shot.File + " captured " + Screen.width + "x" + Screen.height +
                                    " -> " + Path.GetFullPath(path));

                // Let the end-of-frame async PNG write flush before we close/move on.
                // (Plain frame waits -- WaitForEndOfFrame can hang in -nographics batchmode.)
                yield return null;
                yield return null;

                CloseShot(shot);
                yield return null;
            }
        }

        // ---------------------------------------------------------------------
        //  Open / close a single scenario
        // ---------------------------------------------------------------------
        private bool OpenShot(Shot shot)
        {
            switch (shot.Kind)
            {
                case Kind.Router:
                    // PanelRouter.Open is itself Guarded + verifies visibility; false =
                    // not registered OR opened-but-not-visible. Either way, skip cleanly.
                    return Guard.Try(Tag, "Router.Open " + shot.Id, () => PanelRouter.Open(shot.Id), false);

                case Kind.ReflectStatic:
                    return Guard.Try(Tag, "Static " + shot.TypeName + "." + shot.OpenMethod, () =>
                    {
                        var t = FindType(shot.TypeName);
                        if (t == null) return false;
                        var m = t.GetMethod(shot.OpenMethod,
                            BindingFlags.Public | BindingFlags.Static);
                        if (m == null) return false;
                        m.Invoke(null, null);
                        return true;
                    }, false);

                case Kind.ReflectInstance:
                    return Guard.Try(Tag, "Instance " + shot.TypeName + "." + shot.OpenMethod, () =>
                    {
                        var t = FindType(shot.TypeName);
                        if (t == null) return false;
                        var inst = UnityEngine.Object.FindAnyObjectByType(t);
                        if (inst == null) return false;
                        var m = t.GetMethod(shot.OpenMethod,
                            BindingFlags.Public | BindingFlags.Instance);
                        if (m == null) return false;
                        m.Invoke(inst, null);
                        return true;
                    }, false);
            }
            return false;
        }

        private void CloseShot(Shot shot)
        {
            // The modal arbiter closes router + arbiter-routed panels.
            Guard.Try(Tag, "CloseAll after " + shot.File, PanelManager.CloseAll);

            // Best-effort explicit close for a reflected instance that owns one.
            if (shot.Kind == Kind.ReflectInstance && !string.IsNullOrEmpty(shot.CloseMethod))
            {
                Guard.Try(Tag, "Close " + shot.TypeName + "." + shot.CloseMethod, () =>
                {
                    var t = FindType(shot.TypeName);
                    if (t == null) return;
                    var inst = UnityEngine.Object.FindAnyObjectByType(t);
                    if (inst == null) return;
                    var m = t.GetMethod(shot.CloseMethod, BindingFlags.Public | BindingFlags.Instance);
                    m?.Invoke(inst, null);
                });
            }
        }

        /// <summary>Resolve a type by full name across every loaded assembly (Core cannot
        /// reference the HUD/Settings/Audio asmdefs directly without a cycle).</summary>
        private static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            var direct = Type.GetType(fullName);
            if (direct != null) return direct;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        // ---------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------
        private static void EnsureDir(string relDir)
        {
            Guard.Try(Tag, "mkdir " + relDir, () =>
            {
                if (!Directory.Exists(relDir)) Directory.CreateDirectory(relDir);
            });
        }

        private static void Exit()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
