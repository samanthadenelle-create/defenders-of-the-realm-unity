// =============================================================================
// DevBootstrap — DEV-ONLY auto-spawner for the QA dev console.
// -----------------------------------------------------------------------------
// Spawns the DevPanel (DevPanelController + a UIDocument) once at startup, in
// every scene, with NOTHING to wire per scene — so QA never has to remember to
// add the panel. A [RuntimeInitializeOnLoadMethod] hook builds a single
// DontDestroyOnLoad GameObject the moment the runtime starts.
//
// ── RELEASE-SAFE — the whole point of this file ─────────────────────────────
// The entire body is `#if DEVELOPMENT_BUILD || UNITY_EDITOR`. In a release
// player build this file compiles to nothing, the [RuntimeInitializeOnLoadMethod]
// never exists, and no dev console is ever created. Belt-and-braces, the
// DeNelle.DevTools asmdef also carries the matching define constraint, so the
// whole assembly — this bootstrap included — is absent from a release build.
//
// THE PANEL'S UXML: this bootstrap loads DevPanel.uxml from a Resources folder
// so it needs no scene reference. The integrator places (or symlinks) the panel
// UXML/USS at Assets/_Modules/DevTools/Resources/DevPanel.uxml — see the
// integrator notes below and docs/port-notes/dev-panel.md. If the asset is not
// found the bootstrap logs once and the panel is simply absent (no crash); the
// integrator can instead drop a DevPanelController into a scene by hand.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY startup hook that spawns the QA dev console once, persistently,
    /// for every scene. Compiled out of release builds.
    /// </summary>
    public static class DevBootstrap
    {
        private const string PanelObjectName = "[DEV] QA Dev Console";

        /// <summary>The Resources-relative path the panel UXML is loaded from.</summary>
        private const string PanelUxmlResourcePath = "DevPanel";

        /// <summary>Panel UI sort order — well above any gameplay HUD UIDocument.</summary>
        private const float PanelSortOrder = 9000f;

        private static bool _spawned;

        /// <summary>
        /// Runs once, automatically, after the first scene has loaded. Builds the
        /// persistent dev-console GameObject. The <c>[RuntimeInitializeOnLoadMethod]</c>
        /// attribute makes this self-starting: nothing in any scene has to call it.
        /// <para>
        /// AfterSceneLoad (not BeforeSceneLoad) is deliberate: the console's
        /// UIDocument needs a PanelSettings WITH A THEME to render, and the most
        /// reliable source of a themed PanelSettings is a sibling HUD UIDocument
        /// already alive in the loaded scene (the same adoption pattern
        /// AdminOverlay / ArenaDefensePaletteUI use). By the time this runs those
        /// siblings exist; before the scene loads they do not.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (_spawned) return;
            _spawned = true;

            // ── Publisher polish (2026-07-02): kill the always-on red strip ──────
            // In a Development build UNITY'S OWN "Development Console" overlay (the
            // red error strip — NOT this DevPanel, which starts closed behind F1)
            // pops whenever any error/exception logs; it sat in 29 of 32 owner
            // felt-test screenshots. Errors still reach Player.log + the F8
            // BreakCaptureHarness, so the strip is pure noise. Gate its visibility
            // behind an explicit opt-in: PlayerPrefs "dev.console" = 1 restores it.
            // developerConsoleEnabled=false is the load-bearing half — visible=false
            // alone only hides the CURRENT strip; the engine re-shows it on the next
            // error unless the console is disabled outright. Dev tooling itself
            // (this panel, F8, logs) is untouched.
            if (PlayerPrefs.GetInt("dev.console", 0) != 1)
            {
                Debug.developerConsoleEnabled = false;
                Debug.developerConsoleVisible = false;
            }

            var go = new GameObject(PanelObjectName);
            Object.DontDestroyOnLoad(go);

            // The UIDocument needs a PanelSettings asset to render.
            var document = go.AddComponent<UIDocument>();
            // RCA 2026-06-21: ResolvePanelSettings may return a SHARED asset (Resources DevPanelSettings or a
            // borrowed sibling). Setting sortingOrder must NOT mutate a shared asset, and UIDocument.sortingOrder
            // doesn't reliably reach PanelSettings.sortingOrder (which input dispatch reads). So CLONE it to an
            // own instance, then set sortingOrder on the clone — independent + the layering becomes real.
            var ps = ResolvePanelSettings();
            if (ps != null) { ps = Object.Instantiate(ps); ps.name = "DevRuntimePanelSettings"; }
            document.panelSettings = ps;
            document.sortingOrder = PanelSortOrder;
            if (document.panelSettings != null) document.panelSettings.sortingOrder = PanelSortOrder;

            // The controller builds its ENTIRE UI in C# (inline styles) — it does
            // NOT read DevPanel.uxml (this project's UXML renders empty in player
            // builds; see DevPanelController.BindElements). So the UXML is OPTIONAL:
            // assign it only if it happens to be reachable via Resources, purely so
            // a future UXML-based variant could pick it up. A missing UXML is NOT a
            // reason to skip the spawn — that was the bug that made the console
            // unreachable (no toggle target despite F1 firing).
            var visualTree = Resources.Load<VisualTreeAsset>(PanelUxmlResourcePath);
            if (visualTree != null)
                document.visualTreeAsset = visualTree;

            // The controller binds (code-builds) its UI in OnEnable; adding it after
            // the UIDocument is configured means the root is ready.
            go.AddComponent<DevPanelController>();

            Debug.Log("[DevBootstrap] QA dev console spawned (DEV build only). " +
                      "Press F1 (or tap the 'DEV' corner chip) to toggle.");
        }

        /// <summary>
        /// Resolves a PanelSettings for the dev-console UIDocument. Order:
        ///   1. A dedicated Resources asset named "DevPanelSettings", if present.
        ///   2. Adopt one from a sibling UIDocument already in the scene (carries a
        ///      working themeStyleSheet — the bit a UI-Toolkit panel needs to draw).
        ///   3. Failing both, create a runtime PanelSettings and borrow a theme from
        ///      any live UIDocument (mirrors ArenaDefensePaletteUI). This guarantees
        ///      the console renders even in a scene with no other UI-Toolkit canvas.
        /// </summary>
        private static PanelSettings ResolvePanelSettings()
        {
            var dedicated = Resources.Load<PanelSettings>("DevPanelSettings");
            if (dedicated != null) return dedicated;

            // Look for a sibling UIDocument with a usable (themed) PanelSettings.
            foreach (var doc in Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Include))
            {
                if (doc != null && doc.panelSettings != null &&
                    doc.panelSettings.themeStyleSheet != null)
                    return doc.panelSettings;
            }

            // Last resort: build one at runtime, borrowing any available theme.
            var created = ScriptableObject.CreateInstance<PanelSettings>();
            created.name = "DevRuntimePanelSettings";
            created.scaleMode = PanelScaleMode.ConstantPixelSize;   // dev tool — fixed px
            foreach (var doc in Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Include))
            {
                if (doc != null && doc.panelSettings != null &&
                    doc.panelSettings.themeStyleSheet != null)
                {
                    created.themeStyleSheet = doc.panelSettings.themeStyleSheet;
                    break;
                }
            }
            return created;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR

// =============================================================================
// INTEGRATOR NOTES — the auto-spawn (DEV builds only).
// -----------------------------------------------------------------------------
// ZERO per-scene work, ZERO required Resources assets. The bootstrap spawns the
// console automatically in every scene, in the Editor and in any Development
// build, then:
//   - Press F1, OR tap the on-screen "DEV" corner chip (top-right), to toggle it.
//
// The console builds its WHOLE UI in C# (DevPanelController, inline styles) — it
// does NOT depend on DevPanel.uxml/.uss (this project's UXML renders empty in
// player builds). So the only thing the UIDocument needs to draw is a
// PanelSettings, which ResolvePanelSettings() supplies in this order:
//   1. A Resources asset named "DevPanelSettings" (optional override).
//   2. A themed PanelSettings adopted from a sibling HUD UIDocument in the scene.
//   3. A runtime-created PanelSettings (theme borrowed if any exists).
//
// OPTIONAL polish (not required for the console to work):
//   - Drop a "DevPanelSettings" PanelSettings asset under any Resources/ folder
//     to give the dev console its own independent scale/theme.
//
// NOTHING here ships in a release build — the #if and the asmdef define
// constraint both exclude the whole DeNelle.DevTools assembly.
// =============================================================================
