// =============================================================================
// AutoPilotInstaller — DEV-ONLY auto-spawner for the AutoPilot playtest bot.
// -----------------------------------------------------------------------------
// Mirrors BreakCaptureHarness.Install(): a [RuntimeInitializeOnLoadMethod] that
// creates a single DontDestroyOnLoad host for the AutoPilotDriver — but ONLY
// when the run was explicitly asked for, via either:
//   * the "--autopilot" command-line arg (headless / CI playtest), OR
//   * the AUTOPILOT environment variable being set.
// A normal editor Play / a shipped dev build does NOT auto-launch the bot, so
// this is inert unless you opt in.
//
// Everything is try/caught so a diagnostic can never break startup. The driver
// it spawns quits the app on completion (the headless path wants that).
//
// RELEASE-SAFE: the whole file is #if DEVELOPMENT_BUILD || UNITY_EDITOR.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY startup hook that spawns <see cref="AutoPilotDriver"/> when the
    /// run opted in via the <c>--autopilot</c> CLI arg or the <c>AUTOPILOT</c>
    /// env var. Compiled out of release builds.
    /// </summary>
    public static class AutoPilotInstaller
    {
        private const string HostName = "~AutoPilotDriver";
        private static bool s_installed;

        /// <summary>
        /// Runs once after the first scene loads (the hero + UI exist by then,
        /// matching DevBootstrap's AfterSceneLoad timing). Spawns the driver only
        /// when AutoPilot was requested.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_installed) return;
            try
            {
                if (!Requested()) return;
                s_installed = true;

                var go = new GameObject(HostName);
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                var driver = go.AddComponent<AutoPilotDriver>();

                FlowTrace.Step("Auto", "AutoPilotInstaller: --autopilot requested — starting bot (quitOnDone=true).");
                driver.Begin(quitOnDone: true);
            }
            catch (Exception e)
            {
                // A diagnostic must never break startup.
                try { Debug.LogWarning("[AutoPilot] installer failed: " + e.Message); } catch { }
            }
        }

        /// <summary>True when "--autopilot" is on the command line OR AUTOPILOT env var is set.</summary>
        private static bool Requested()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null)
                    foreach (var a in args)
                        if (!string.IsNullOrEmpty(a) &&
                            a.Equals("--autopilot", StringComparison.OrdinalIgnoreCase))
                            return true;
            }
            catch { }

            try
            {
                string env = Environment.GetEnvironmentVariable("AUTOPILOT");
                if (!string.IsNullOrEmpty(env)) return true;
            }
            catch { }

            return false;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
