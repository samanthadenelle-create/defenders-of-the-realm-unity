// =============================================================================
// HelpMenu — small in-game overlay reachable from a "?" button in the HUD.
// Surfaces three actions:
//   • Report Bug — captures a screenshot to disk + opens the default mail
//     client with a populated mailto: to samanthadenelle@gmail.com (owner-
//     authorised destination, 2026-05-19). Auto-attaching the screenshot
//     requires a backend upload step — until that lands, the user attaches
//     the file from the printed path.
//   • Controls — static text describing WASD + 1/2/3/4 + Build hotkeys.
//   • Credits — DeNelle Studios + KayKit + Tripo attribution.
// -----------------------------------------------------------------------------
// Builds its UI at runtime so it works in any scene without needing UXML
// authored per-scene. Spawned by HelpMenuBootstrap (RuntimeInitializeOnLoad).
// =============================================================================

using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class HelpMenu : MonoBehaviour
    {
        public const string BugReportEmail = "samanthadenelle@gmail.com";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _overlay;
        private Label _toast;
        private float _toastUntil;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            // Borrow whatever PanelSettings the scene already ships. We look
            // for an existing UIDocument in the active scene and reuse its
            // panel settings — works regardless of which PanelSettings asset
            // the scene happens to use.
            if (_document.panelSettings == null)
            {
                foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (existing == _document || existing.panelSettings == null) continue;
                    _document.panelSettings = existing.panelSettings;
                    break;
                }
            }
            if (_document.panelSettings == null)
            {
                Debug.LogWarning("[HelpMenu] No PanelSettings available in scene — Help button hidden.");
                enabled = false;
                return;
            }
            // Render above every other UI in the scene.
            _document.sortingOrder = 100;
            BuildUi();
        }

        private void Update()
        {
            if (_toast != null && _toastUntil > 0f && Time.unscaledTime > _toastUntil)
            {
                _toast.style.display = DisplayStyle.None;
                _toastUntil = 0f;
            }
        }

        // ── UI construction ────────────────────────────────────────────────────
        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            if (_root == null) return;
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0;  _root.style.bottom = 0;

            // The little "?" launcher button — top-right, below Wave indicator.
            var launcher = new Button(ToggleOverlay) { text = "?" };
            launcher.style.position = Position.Absolute;
            launcher.style.top = 80; launcher.style.right = 20;
            launcher.style.width = 36; launcher.style.height = 36;
            launcher.style.fontSize = 18;
            launcher.style.unityFontStyleAndWeight = FontStyle.Bold;
            launcher.style.backgroundColor = new Color(0.10f, 0.07f, 0.15f, 0.92f);
            launcher.style.color = Color.white;
            launcher.style.borderTopLeftRadius = 18;
            launcher.style.borderTopRightRadius = 18;
            launcher.style.borderBottomLeftRadius = 18;
            launcher.style.borderBottomRightRadius = 18;
            _root.Add(launcher);

            // The overlay panel — hidden until launcher tapped.
            _overlay = new VisualElement { name = "help-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0;  _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            _root.Add(_overlay);

            var card = new VisualElement();
            card.style.minWidth = 380;
            card.style.maxWidth = 480;
            card.style.paddingTop = 24;    card.style.paddingBottom = 24;
            card.style.paddingLeft = 28;   card.style.paddingRight = 28;
            card.style.backgroundColor = new Color(0.07f, 0.05f, 0.11f, 0.98f);
            card.style.borderTopLeftRadius = 14; card.style.borderTopRightRadius = 14;
            card.style.borderBottomLeftRadius = 14; card.style.borderBottomRightRadius = 14;
            card.style.borderTopWidth = 1;  card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1; card.style.borderRightWidth = 1;
            var rim = new Color(0.78f, 0.66f, 0.16f, 0.6f);
            card.style.borderTopColor = rim;   card.style.borderBottomColor = rim;
            card.style.borderLeftColor = rim;  card.style.borderRightColor = rim;
            _overlay.Add(card);

            var title = new Label("Help");
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.90f, 0.78f);
            title.style.marginBottom = 16;
            card.Add(title);

            card.Add(MakeButton("Report a bug",   OnReportBug));
            card.Add(MakeButton("Controls",       OnShowControls));
            card.Add(MakeButton("Credits",        OnShowCredits));
            card.Add(MakeButton("Close",          ToggleOverlay));

            // Toast (status messages) — appears low-center, fades after 3 s.
            _toast = new Label(string.Empty);
            _toast.style.position = Position.Absolute;
            _toast.style.bottom = 80; _toast.style.left = 0; _toast.style.right = 0;
            _toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            _toast.style.color = Color.white;
            _toast.style.fontSize = 14;
            _toast.style.display = DisplayStyle.None;
            _root.Add(_toast);
        }

        private static Button MakeButton(string label, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.style.height = 40;
            b.style.marginTop = 6; b.style.marginBottom = 6;
            b.style.fontSize = 14;
            b.style.backgroundColor = new Color(0.18f, 0.12f, 0.28f, 1f);
            b.style.color = new Color(0.95f, 0.92f, 0.85f);
            b.style.borderTopLeftRadius = 8; b.style.borderTopRightRadius = 8;
            b.style.borderBottomLeftRadius = 8; b.style.borderBottomRightRadius = 8;
            return b;
        }

        // ── Actions ────────────────────────────────────────────────────────────
        private void ToggleOverlay()
        {
            if (_overlay == null) return;
            bool open = _overlay.style.display == DisplayStyle.None;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _overlay.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
        }

        private void OnReportBug()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "BugReports");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string shot = Path.Combine(dir, $"screenshot_{stamp}.png");
                ScreenCapture.CaptureScreenshot(shot);

                string scene = SceneManager.GetActiveScene().name;
                string subject = Uri.EscapeDataString($"[DotR] Bug — {scene} @ {stamp}");
                string body = Uri.EscapeDataString(
                    $"What happened:\n\n\n" +
                    $"Steps to reproduce:\n\n\n" +
                    $"--- auto-captured ---\n" +
                    $"Scene: {scene}\n" +
                    $"Time:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Build: {Application.version} ({Application.unityVersion})\n" +
                    $"Device: {SystemInfo.deviceModel} / {SystemInfo.operatingSystem}\n" +
                    $"Screen: {Screen.width}x{Screen.height}\n" +
                    $"Screenshot: {shot}\n" +
                    $"(Please attach the screenshot file above.)");
                string mailto = $"mailto:{BugReportEmail}?subject={subject}&body={body}";
                Application.OpenURL(mailto);
                ShowToast($"Screenshot saved to {shot}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HelpMenu] Bug report failed: " + ex.Message);
                ShowToast("Bug report failed — see log.");
            }
        }

        private void OnShowControls()
        {
            ShowToast("Controls — WASD/Arrows/dpad: move • 1/2/3/4 + face buttons: cast Q/W/E/R • Build button: tower placement • Esc: pause");
        }

        private void OnShowCredits()
        {
            ShowToast("Defenders of the Realm v2 — DeNelle Studios. Models: KayKit + Tripo. Audio: original soundtrack.");
        }

        private void ShowToast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.style.display = DisplayStyle.Flex;
            _toastUntil = Time.unscaledTime + 5f;
        }
    }
}
