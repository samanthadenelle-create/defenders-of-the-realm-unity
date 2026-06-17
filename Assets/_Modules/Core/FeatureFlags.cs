using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Central demo/web feature gate. THE DEMO LAW: a reachable feature must either WORK or be
    /// HIDDEN — a broken-but-visible feature is worse than an absent one, especially on the WEB
    /// build (the grant-demo target). Features default to their *proven* state; anything not
    /// verified end-to-end ships OFF and is flipped ON only once it's confirmed ("unflag when proven").
    ///
    /// Each entry point checks the matching flag before spawning/binding/opening. To test a gated
    /// feature without a rebuild, set PlayerPrefs "ff.&lt;name&gt;" to 1 (on) or 0 (off); -1/absent
    /// uses the default below.
    ///
    /// Status set by the 2026-06-16 demo-readiness audit:
    ///   RAID  = OFF — entry + combat work, but victory/return is NOT built (RaidGarrisonSpawner
    ///           .OnCleared has no subscriber, no RaidScorer, hero spawns as a capsule) → a cleared
    ///           raid soft-locks. Unflag once the victory + return + real-hero flow lands.
    ///   ARENA = ON  — full loop verified (enter→fight→win/lose→reward→return); SKR wallet is an
    ///           intentional client-side MVP stub. Demo-ready.
    /// </summary>
    public static class FeatureFlags
    {
        public static bool Raid  => Get("raid",  defaultOn: false);
        public static bool Arena => Get("arena", defaultOn: true);

        /// <summary>When ON, our decorative CHROME (gilt inner-rim / bottom rule / header shadow+rule /
        /// niche backings + per-panel solid fills + glows) does NOT render, so the Blink "Obsidian" panel
        /// sprite + functional content (text/rows/grid/buttons) show clean. Content/structure and the
        /// world-occluding backdrops are never hidden. Default OFF (current look). PlayerPrefs
        /// "ff.blinkchrome". Gated in ElarionUiKit + per-panel (memory ui-chrome-composition-and-blink-flag).</summary>
        public static bool BlinkChrome => Get("blinkchrome", defaultOn: false);

        /// <summary>WO-443 — when ON, the WebGL build streams its diagnostic logs (FlowTrace +
        /// Unity errors/exceptions) to the backend remote-trace sink (<see cref="DeNelle.Core.Diagnostics.WebTrace"/>)
        /// so a real web player's issue can be triaged from the DB. Default OFF (don't spam the DB).
        /// PlayerPrefs "ff.webtrace". Can also be flipped ON for ONE session via the WebGL URL
        /// query-param <c>?trace=1</c> (see <see cref="ApplyUrlActivationOnce"/>) so support can turn it
        /// on without a rebuild. The sink itself is a clean no-op on standalone/editor and stays dormant
        /// until a backend endpoint is configured.</summary>
        public static bool WebTrace => Get("webtrace", defaultOn: false);

        /// <summary>Per-feature resolve: PlayerPrefs override ("ff.&lt;name&gt;" = 0/1) wins, else the default.</summary>
        private static bool Get(string name, bool defaultOn)
        {
            int pref = PlayerPrefs.GetInt("ff." + name, -1);
            if (pref == 0) return false;
            if (pref == 1) return true;
            return defaultOn;
        }

        // ── WO-443 — WebGL one-session URL activation (?trace=1) ──────────────────
        private static bool s_urlActivationChecked;

        /// <summary>
        /// WO-443 — reads <see cref="Application.absoluteURL"/> on WebGL and, if it carries
        /// <c>?trace=1</c> (or <c>&amp;trace=1</c>), turns the <see cref="WebTrace"/> flag ON for THIS
        /// session only (writes PlayerPrefs "ff.webtrace"=1) so support can activate web tracing for a
        /// single player without a rebuild. Idempotent (runs its parse once) and safe to call on every
        /// platform — on editor/standalone <c>absoluteURL</c> is empty so it is a no-op. Never throws.
        /// </summary>
        public static void ApplyUrlActivationOnce()
        {
            if (s_urlActivationChecked) return;
            s_urlActivationChecked = true;
            try
            {
                string url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url)) return;

                int q = url.IndexOf('?');
                if (q < 0) return;
                string query = url.Substring(q + 1);

                foreach (var pair in query.Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    string key = (eq < 0 ? pair : pair.Substring(0, eq)).Trim();
                    string val = (eq < 0 ? "" : pair.Substring(eq + 1)).Trim();
                    if (key.Equals("trace", System.StringComparison.OrdinalIgnoreCase)
                        && (val == "1" || val.Equals("true", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        PlayerPrefs.SetInt("ff.webtrace", 1);
                        PlayerPrefs.Save();
                        Debug.Log("[FeatureFlags] ?trace=1 detected — web tracing activated for this session (ff.webtrace=1).");
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FeatureFlags] URL trace-activation parse skipped: " + ex.Message);
            }
        }

#if UNITY_EDITOR
        // ── Editor flag toggles (Defenders > Debug) — no registry editing, no Play Mode needed.
        // Flip, then re-open the panel to see it. Checkmark shows the current resolved state.
        private const string BlinkChromeMenu = "Defenders/Debug/Blink Chrome (hide our UI dressing)";

        [UnityEditor.MenuItem(BlinkChromeMenu, priority = 200)]
        private static void ToggleBlinkChrome()
        {
            bool on = !BlinkChrome;                       // resolved value, then invert
            PlayerPrefs.SetInt("ff.blinkchrome", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.blinkchrome = " + (on ? "ON (Blink panels show clean)" : "OFF (our chrome)"));
        }

        [UnityEditor.MenuItem(BlinkChromeMenu, validate = true)]
        private static bool ToggleBlinkChromeValidate()
        {
            UnityEditor.Menu.SetChecked(BlinkChromeMenu, BlinkChrome);
            return true;
        }
#endif
    }
}
