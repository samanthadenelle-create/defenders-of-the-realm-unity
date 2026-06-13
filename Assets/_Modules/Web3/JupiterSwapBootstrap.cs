// =============================================================================
// JupiterSwapBootstrap — self-registering swap-panel host (WO-43).
// -----------------------------------------------------------------------------
// WO-43 requires the swap panel to attach to an existing screen WITHOUT editing
// that screen's controller. This [RuntimeInitializeOnLoadMethod] bootstrap does
// exactly that: it spawns a self-contained GameObject carrying a UIDocument +
// JupiterSwapService + JupiterSwapPanelController in the relevant scenes, so no
// Title / Hero-Select / Village controller is touched. The panel starts hidden
// and is shown on demand via CoreServices.Jupiter.OpenSwapPanel(...).
//
// Mirrors the project's existing bootstrap pattern (DailyQuestHudBootstrap,
// CompassHudBootstrap, DevBootstrap):
//   * borrows a PanelSettings from an existing scene UIDocument (the same trick
//     the recent BuildMenu commit used — see commit 30ff18b), so it does not
//     load a PanelSettings by name and never renders a black null-panel;
//   * loads the panel UXML via Resources.Load so it needs no scene reference.
//
// GRACEFUL DEGRADATION: if the UXML is not reachable via Resources, or no scene
// UIDocument exists to borrow PanelSettings from, the bootstrap logs once and
// simply does not spawn — never a crash. See the INTEGRATOR NOTES at the foot.
//
// SCOPE GUARD: this file lives entirely inside DeNelle.Web3 and edits no other
// module. It only READS a PanelSettings off whatever UIDocument the active
// screen already owns.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Web3
{
    /// <summary>
    /// Spawns the Jupiter swap-panel host once per relevant scene without
    /// touching any existing screen controller. Idempotent + scene-scoped.
    /// </summary>
    public static class JupiterSwapBootstrap
    {
        private const string HostObjectName = "JupiterSwapHost";

        /// <summary>Resources-relative path the panel UXML is loaded from.</summary>
        private const string PanelUxmlResourcePath = "JupiterSwapPanel";

        /// <summary>Swap panel sort order — above gameplay HUD, below the dev console.</summary>
        private const float PanelSortOrder = 500f;

        // Scenes the swap panel is allowed to host itself in. The swap CTA is an
        // onboarding / monetisation surface, so Title + the select screens +
        // Village are the natural homes. An unknown scene is skipped.
        private static readonly string[] AllowedScenes =
        {
            "Title", "HeroSelect", "PetSelect", "Village2"
        };

        private static bool _warnedNoUxml;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => SpawnInScene(scene);

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (!IsAllowedScene(scene.name)) return;

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            // One host total, not one per scene.
            foreach (var existing in Object.FindObjectsByType<JupiterSwapService>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate JupiterSwapHost suppressed (one already exists)");
                    return;
                }
            }

            // The panel UXML ships in Assets/_Modules/Web3/Resources/ so this
            // resolves out-of-the-box in both the editor and player builds.
            var visualTree = Resources.Load<VisualTreeAsset>(PanelUxmlResourcePath);
            if (visualTree == null)
            {
                if (!_warnedNoUxml)
                {
                    _warnedNoUxml = true;
                    Debug.LogWarning(
                        "[JupiterSwapBootstrap] JupiterSwapPanel.uxml not found under a " +
                        $"Resources folder ('{PanelUxmlResourcePath}'). Expected at " +
                        "Assets/_Modules/Web3/Resources/JupiterSwapPanel.uxml. The swap panel " +
                        "will not auto-spawn until the asset is restored there, or a " +
                        "JupiterSwapService host is added to a scene by hand.");
                }
                return;
            }

            var panel = FindPanelSettings();
            if (panel == null)
            {
                // No scene UIDocument to borrow from yet — silently skip; the next
                // scene load (with a HUD/title document present) will succeed.
                return;
            }

            var go = new GameObject(HostObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = visualTree;
            doc.sortingOrder = PanelSortOrder;

            // Service first (registers with CoreServices on Awake), then the
            // controller (binds the UXML in OnEnable, once the doc is configured).
            go.AddComponent<JupiterSwapService>();
            go.AddComponent<JupiterSwapPanelController>();
            FlowTrace.Step("UI", "JupiterSwapHost created (single instance)");
        }

        private static bool IsAllowedScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            // Dungeon scenes are "Dungeon_<id>" — the swap CTA can live there too.
            if (sceneName.StartsWith("Dungeon_", System.StringComparison.Ordinal)) return true;
            for (int i = 0; i < AllowedScenes.Length; i++)
                if (sceneName == AllowedScenes[i]) return true;
            return false;
        }

        private static PanelSettings FindPanelSettings()
        {
            // Borrow from an existing scene UIDocument so we never render a black
            // null-panel and never need a PanelSettings-by-name Resources load.
            var docs = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — the swap panel auto-spawns out-of-the-box.
// -----------------------------------------------------------------------------
// The panel UXML/USS ship at Assets/_Modules/Web3/Resources/ so the bootstrap's
// Resources.Load<VisualTreeAsset>("JupiterSwapPanel") resolves automatically in
// the editor and in player builds. The .uss is pulled in by the UXML's
// <Style src="JupiterSwapPanel.uss" /> tag (same folder, relative path).
// The bootstrap hosts the (hidden) panel in Title / HeroSelect / PetSelect /
// Village / Dungeon scenes automatically — no per-scene wiring required.
//
// OWNER-CONFIG before a real swap can occur:
//   * JupiterSwapService._skrMint — fill the live SKR SPL mint address.
//   * A SwapFeeConfig asset (Assets > Create > Defenders > Swap Fee Config),
//     assigned on the JupiterSwapService, with the fee token-account set.
//     Without it the service uses safe defaults (0.2 % fee, 0.5 % slippage,
//     no fee account) — quotes still work for UI testing.
//   * The swap-transaction SIGNING is a stub (WalletBridgeStub) — wire the real
//     Solana signer before shipping.
//
// To OPEN the panel from gameplay (e.g. an insufficient-SKR moment):
//      CoreServices.Jupiter?.OpenSwapPanel(minimumSkr);
// No assembly reference to DeNelle.Web3 is required — the call goes through the
// DeNelle.Core IJupiterService interface.
// =============================================================================
