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

                // Fleet support: each parallel instance gets its own --seed (varies the
                // explore path) and --run=<id> (namespaces its output folder). Defaults
                // keep a lone run deterministic + writing to the root path.
                int seed = ParseInt("--seed=", AutoPilotDriver.DefaultSeed);
                string runId = ParseString("--run=");
                // Optional boot-scene override (owner request 2026-06-21): "--scene=Village2" (or the
                // AUTOPILOT_SCENE env var) boots the bot DIRECTLY into that scene instead of MainCastle_Hall,
                // so a headless/dev run lands in the real system under test (Village2 garrison, a Garrison_*
                // outpost) with no traversal. The target must be in Build Settings to load by name.
                string startScene = ParseString("--scene=");
                if (string.IsNullOrEmpty(startScene))
                {
                    try { startScene = Environment.GetEnvironmentVariable("AUTOPILOT_SCENE"); } catch { }
                }

                // In a browser (WebGL localhost dev instance) there is nothing to quit TO — Application.Quit
                // just freezes the tab. Keep the bot alive so the tab can be reloaded to re-run; the Windows
                // headless fleet still quits on done so run-autopilot-fleet.ps1 can cycle instances.
                bool quitOnDone = Application.platform != RuntimePlatform.WebGLPlayer;

                FlowTrace.Step("Auto", $"AutoPilotInstaller: autopilot requested — starting bot (quitOnDone={quitOnDone}, seed={seed}, run='{runId ?? "<none>"}', scene='{startScene ?? "<default>"}').");
                driver.Begin(quitOnDone: quitOnDone, seed: seed, runId: runId, startScene: startScene);
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

            // WebGL localhost dev instance: the CLI-arg + env-var paths don't exist in a browser,
            // so a DEV web build opts in via the page URL query — "?autopilot=1" (any "autopilot"
            // token in the query). This whole file is #if DEVELOPMENT_BUILD || UNITY_EDITOR, so a
            // RELEASE web build can NEVER auto-bot (the trigger isn't compiled in). Each tab can
            // vary its explore path with "&seed=1001" and namespace output with "&run=web1".
            try
            {
                if (UrlQuery().Contains("autopilot")) return true;
            }
            catch { }

            return false;
        }

        /// <summary>The lower-cased URL query string (WebGL page URL after '?'), or "" elsewhere / on failure.</summary>
        private static string UrlQuery()
        {
            try
            {
                string url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url)) return "";
                int q = url.IndexOf('?');
                return q >= 0 ? url.Substring(q + 1).ToLowerInvariant() : "";
            }
            catch { return ""; }
        }

        /// <summary>Read a "key=value" token from the WebGL URL query ("?autopilot=1&amp;seed=1001&amp;run=web1"); null if absent/empty.</summary>
        private static string UrlQueryValue(string key)
        {
            try
            {
                string query = UrlQuery();
                if (string.IsNullOrEmpty(query)) return null;
                string wantKey = (key ?? string.Empty).ToLowerInvariant();
                foreach (var pair in query.Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0) continue;
                    if (pair.Substring(0, eq) == wantKey)
                    {
                        string v = pair.Substring(eq + 1).Trim();
                        return string.IsNullOrEmpty(v) ? null : v;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Parse an int CLI arg of the form "<prefix><n>" (e.g. "--seed=7"); fallback if absent/bad.</summary>
        private static int ParseInt(string prefix, int fallback)
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null)
                    foreach (var a in args)
                        if (!string.IsNullOrEmpty(a) && a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string v = a.Substring(prefix.Length).Trim();
                            if (int.TryParse(v, out int n)) return n;
                        }
            }
            catch { }
            // WebGL fallback: "--seed=" -> URL "?...&seed=1001" query param.
            try
            {
                string v = UrlQueryValue(prefix.TrimStart('-').TrimEnd('='));
                if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int n)) return n;
            }
            catch { }
            return fallback;
        }

        /// <summary>Parse a string CLI arg of the form "<prefix><value>" (e.g. "--run=3"); null if absent/empty.</summary>
        private static string ParseString(string prefix)
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null)
                    foreach (var a in args)
                        if (!string.IsNullOrEmpty(a) && a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string v = a.Substring(prefix.Length).Trim();
                            return string.IsNullOrEmpty(v) ? null : v;
                        }
            }
            catch { }
            // WebGL fallback: "--run=" -> URL "?...&run=web1", "--scene=" -> "?...&scene=Village2".
            try
            {
                string v = UrlQueryValue(prefix.TrimStart('-').TrimEnd('='));
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch { }
            return null;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
