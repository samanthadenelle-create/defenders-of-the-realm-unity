// =============================================================================
// MusicToggleBootstrap — a small, always-visible music ♪ on/off button on EVERY
// screen (owner: "missing the music on off toggle everywhere").
// -----------------------------------------------------------------------------
// Auto-installs at runtime (no scene wiring) the same way SettingsBootstrap does:
// a [RuntimeInitializeOnLoadMethod] + a SceneManager.sceneLoaded hook re-creates a
// tiny bottom-right toggle in each scene. It borrows a live UIDocument's
// PanelSettings (so it renders at the scene's UI scale) and drives music through
// SettingsModel — which persists to the save and is shared by the Settings screen,
// so the quick toggle and the full options menu stay in sync.
//
// Note the fresh-game default is Muted = true (a11y), so without a visible toggle
// a new player hears nothing AND has no obvious way to turn audio on; this button
// is that affordance. Turning music ON also clears the master mute so it's audible.
//
// Lives in DeNelle.Settings (references DeNelle.Core + UI Toolkit only).
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace DeNelle.Settings
{
    /// <summary>Installs the always-visible music toggle into every loaded scene.</summary>
    public static class MusicToggleBootstrap
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install();

        private static void Install()
        {
            if (Object.FindAnyObjectByType<MusicToggleHud>() != null) return;

            // Borrow a PanelSettings from any UIDocument in the scene so the toggle
            // renders at that scene's UI scale (a code-built UIDocument with no
            // PanelSettings renders nothing — commit 30ff18b).
            PanelSettings ps = null;
            float topSort = float.MinValue;   // UIDocument.sortingOrder is a float
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (doc == null || doc.panelSettings == null) continue;
                if (doc.sortingOrder >= topSort) { topSort = doc.sortingOrder; ps = doc.panelSettings; }
            }
            if (ps == null) return;   // no UI in this scene — nothing to host the toggle on

            // Inactive-then-activate so the UIDocument builds its root with the
            // PanelSettings already assigned (mirrors WaveFeedbackDirector).
            var go = new GameObject("MusicToggleHud");
            go.SetActive(false);
            var doc2 = go.AddComponent<UIDocument>();
            doc2.panelSettings = ps;
            doc2.sortingOrder = topSort + 50;   // above the scene's own UI
            go.AddComponent<MusicToggleHud>();
            go.SetActive(true);
        }
    }

    /// <summary>Builds + drives the ♪ toggle button on its own UIDocument root.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MusicToggleHud : MonoBehaviour
    {
        private Button _btn;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;   // only the button captures clicks

            _btn = new Button(Toggle) { name = "music-toggle" };
            var s = _btn.style;
            s.position = Position.Absolute;
            s.bottom = 14f; s.right = 14f;            // bottom-right — clears top bars + ability bar
            s.width = 44f; s.height = 44f;
            s.fontSize = 22f;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.color = Color.white;
            s.borderTopWidth = 0f; s.borderBottomWidth = 0f; s.borderLeftWidth = 0f; s.borderRightWidth = 0f;
            s.borderTopLeftRadius = 22f; s.borderTopRightRadius = 22f;
            s.borderBottomLeftRadius = 22f; s.borderBottomRightRadius = 22f;
            root.Add(_btn);
            Refresh();
        }

        /// <summary>Music is audible only when not master-muted AND its volume is up.</summary>
        private static bool MusicOn => !SettingsModel.Muted && SettingsModel.MusicVolume > 0.01f;

        private void Toggle()
        {
            if (MusicOn)
            {
                SettingsModel.MusicVolume = 0f;            // music off (SFX untouched)
            }
            else
            {
                SettingsModel.Muted = false;               // ensure it's actually audible
                if (SettingsModel.MusicVolume < 0.01f)
                    SettingsModel.MusicVolume = SettingsModel.DefaultMusicVolume;
            }
            SettingsModel.ApplyAll();                      // push to the mixer + persist
            Refresh();
        }

        private void Refresh()
        {
            if (_btn == null) return;
            bool on = MusicOn;
            _btn.text = on ? "♪" : "♪̷";                    // note + slashed note
            _btn.tooltip = on ? "Music: On" : "Music: Off";
            _btn.style.backgroundColor = on
                ? new Color(0.16f, 0.52f, 0.34f, 0.92f)    // green = playing
                : new Color(0.40f, 0.13f, 0.13f, 0.92f);   // red = muted
        }
    }
}
