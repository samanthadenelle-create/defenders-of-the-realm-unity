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
    ///   RAID  = ON  — the core loop is closed: RaidVictoryController now subscribes to
    ///           RaidGarrisonSpawner.OnCleared and runs victory -> CLAIM (RaidClaimService +
    ///           SceneOwnership flip player-owned) -> NEXT COMPANION (AddToParty) -> RETURN
    ///           (victory banner + GoCastle, with an auto-return safety timer), so a cleared
    ///           raid no longer soft-locks. The full WO-431 star-scoring/reward SCREEN and the
    ///           WO-441 Phase-C auto-harvest outpost are follow-ups layered on this spine.
    ///   ARENA = ON  — full loop verified (enter→fight→win/lose→reward→return); SKR wallet is an
    ///           intentional client-side MVP stub. Demo-ready.
    /// </summary>
    public static class FeatureFlags
    {
        public static bool Raid  => Get("raid",  defaultOn: true);
        public static bool Arena => Get("arena", defaultOn: true);

        /// <summary>WO-449 — when ON, the raid loop IS the continuous distance-gated WALK: the raid
        /// target is a live EnemyOutpost spawned in the OuterWorld (~70m out a gate), the hero walks
        /// to it on one continuous NavMesh, combat triggers on approach (Enemy hero-aggro), and clearing
        /// it claims the base + grants the next companion IN PLACE — there is NO DEPLOY screen and NO
        /// teleport (the hero never leaves the open world). When OFF, the legacy
        /// RaidSelectionScreen -> RaidDeployScreen -> SceneRouter.GoRaid teleport path is restored
        /// verbatim (the raid icon opens the selection screen; RaidOutpostSystem does not spawn the
        /// walk-to outpost). Default ON. PlayerPrefs "ff.raidwalk".</summary>
        public static bool RaidContinuousWalk => Get("raidwalk", defaultOn: true);

        /// <summary>When OFF, the "Travel to &lt;outpost&gt;" confirm-to-cross prompt on garrison /
        /// raid-outpost seams (<see cref="DeNelle.Village.World.SceneTransitionTrigger"/> whose target is a
        /// <c>Garrison_*</c> / <c>Outpost_*</c> / <c>RaidBase_*</c> scene) is SUPPRESSED — the player can NOT
        /// fast-travel to an outpost area; reaching it must be earned by walking (the WO-453 distance-gated
        /// region vision). The castle&lt;-&gt;OuterWorld crossing is NOT an outpost destination and is never
        /// gated by this flag. Default OFF (owner 2026-06-19: "i dont want that as a fast travel option, at
        /// least not yet"). Flip ON via PlayerPrefs "ff.outposttravel" = 1 to restore the travel prompt.</summary>
        public static bool OutpostTravel => Get("outposttravel", defaultOn: false);

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

        /// <summary>When ON, tapping an upgradable building opens the code-built MVVM
        /// <c>BuildingUpgradePanelMvvm</c> (a big "Upgrade Building" CTA + a tier-ladder grid)
        /// instead of the legacy Yarn upgrade menu / UIDocument BuildingUpgradePanel. Presentation
        /// only — the upgrade math (BuildingUpgradeService / ResourceBuildingState) is unchanged.
        /// Default OFF. PlayerPrefs "ff.buildingupgradepanel". The MVVM bootstrap only spawns when
        /// ON, and the legacy UIDocument bootstrap suppresses itself when ON, so the two never
        /// double-register PanelId.BuildingUpgrade.</summary>
        public static bool BuildingUpgradePanel => Get("buildingupgradepanel", defaultOn: true);

        /// <summary>When ON, opening a weapon/armor shop opens the native code-built MVVM
        /// <c>PartyShopPanelMvvm</c> (party-member selector + tap-to-filter + unified single-tap
        /// buy/equip/sell + real item images + stat/buff deltas) instead of the legacy
        /// <c>ShopPanel</c> (two sell bars, no party selection, blank icons). Presentation +
        /// transaction routing through the proven IEconomy / IInventoryStore / IEquipTarget seams;
        /// the catalog + equip math is unchanged. Default OFF. PlayerPrefs "ff.partyshop". The MVVM
        /// bootstrap only spawns when ON, and CmdOpenShop routes to PanelRouter→PartyShop only when
        /// ON (legacy ShopPanel path when OFF), so the two never double-open.</summary>
        public static bool PartyShop => Get("partyshop", defaultOn: true);

        /// <summary>Global runtime kill-switch for ALL dev keyboard hotkeys (DevPanel F1, DebugCanvas
        /// F12, AdminOverlay Ctrl+Shift+A, the test spawners J/K/L, the tower dev harness B/J/K/N/U,
        /// the jukebox J open, etc.). Default OFF — so every dev hotkey is DEAD everywhere (editor AND
        /// build) unless a developer explicitly opts in by setting PlayerPrefs "ff.devhotkeys" = 1.
        /// This is the single gate the dev hotkeys check at the top of their key-read; it replaces the
        /// old <c>#if UNITY_EDITOR</c> wraps that left the keys live in the editor (where the owner
        /// tests). Movement (WASD/arrows), weapon skills/spells, F8 capture and F9 are NOT dev hotkeys
        /// and are unaffected by this flag.</summary>
        public static bool DevHotkeys => Get("devhotkeys", defaultOn: false);

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
