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
        /// Runs once, automatically, after the runtime loads — before the first
        /// scene's objects run their Awake. Builds the persistent dev-console
        /// GameObject. The <c>[RuntimeInitializeOnLoadMethod]</c> attribute makes
        /// this self-starting: nothing in any scene has to call it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
            if (_spawned) return;
            _spawned = true;

            var go = new GameObject(PanelObjectName);
            Object.DontDestroyOnLoad(go);

            // The UIDocument needs a PanelSettings asset to render. The dev panel
            // loads one from Resources too (DevPanelSettings); if absent it falls
            // back to any PanelSettings already in Resources so it still draws.
            var document = go.AddComponent<UIDocument>();

            var panelSettings = Resources.Load<PanelSettings>("DevPanelSettings");
            if (panelSettings != null)
                document.panelSettings = panelSettings;

            var visualTree = Resources.Load<VisualTreeAsset>(PanelUxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning(
                    "[DevBootstrap] DevPanel.uxml not found under a Resources folder " +
                    $"('{PanelUxmlResourcePath}'). The QA dev console will not auto-spawn — " +
                    "place DevPanel.uxml in Assets/_Modules/DevTools/Resources/, or add a " +
                    "DevPanelController to a scene by hand. See docs/port-notes/dev-panel.md.");
                return;
            }
            document.visualTreeAsset = visualTree;
            document.sortingOrder = PanelSortOrder;

            // The controller binds the UXML in OnEnable; adding it after the
            // UIDocument is configured means the root is ready.
            go.AddComponent<DevPanelController>();

            Debug.Log("[DevBootstrap] QA dev console spawned (DEV build only). Press F1 to toggle.");
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR

// =============================================================================
// INTEGRATOR NOTES — making the auto-spawn work (DEV builds only).
// -----------------------------------------------------------------------------
// This bootstrap is fully automatic IF the panel assets are reachable via
// Resources.Load. The integrator does ONE of:
//
//   A. AUTO-SPAWN (recommended — zero per-scene work):
//      - Create Assets/_Modules/DevTools/Resources/.
//      - Move (or copy) DevPanel.uxml + DevPanel.uss into it. Unity finds them
//        by name; the .uss is referenced by the .uxml's <Style> tag.
//      - Create a PanelSettings asset there named "DevPanelSettings"
//        (Assets > Create > UI Toolkit > Panel Settings Asset). Optional — the
//        panel still renders against an existing PanelSettings if omitted, but
//        a dedicated one keeps the dev console's scale independent of the HUD.
//      The bootstrap then spawns the console in every scene, in the Editor and
//      in any Development build.
//
//   B. MANUAL (no Resources folder): skip the above and drop a GameObject with
//      a UIDocument (Source Asset = DevPanel.uxml) + DevPanelController into
//      whatever scenes QA needs it in.
//
// Either way, NOTHING here ships in a release build — the #if and the asmdef
// define constraint both exclude it.
// =============================================================================
