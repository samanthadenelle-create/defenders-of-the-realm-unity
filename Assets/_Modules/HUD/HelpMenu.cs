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
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class HelpMenu : MonoBehaviour
    {
        public const string BugReportEmail = "samanthadenelle@gmail.com";
        public const string BugReportEndpoint = "https://defenders-of-the-realm.vercel.app/api/bug-report";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _overlay;
        private Label _toast;
        private float _toastUntil;
        public static HelpMenu Instance { get; private set; }

        // DEF-212 modal arbiter handle. The gear/Help menu is a full-screen modal
        // (its backdrop is pickingMode.Position and eats input), so it MUST route
        // through PanelManager like every other panel — otherwise it stacks over
        // open content (e.g. the dialogue portrait) and an invisible-but-pickable
        // backdrop can trap the player when another panel opens behind it.
        private PanelHandle _panelHandle;

        private void Awake()
        {
            Instance = this;
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
            // Render above every other UI in the scene — including the uGUI
            // TOWN-HUD canvas (sortingOrder 140 in VillageHudController), whose
            // top-right mini-map otherwise drew over + ate the gear's raycasts.
            // UIToolkit panels and uGUI Screen-Space-Overlay canvases sort by
            // their own sortingOrder, so this must exceed the town canvas.
            _document.sortingOrder = 2700;   // above the town HUD (140) + inventory modal (2600) — settings menu is top-most
            BuildUi();
            // DEF-212: register with the single-modal arbiter AFTER BuildUi so _overlay exists.
            // Opening the Help menu now closes any other open panel; closing clears our slot.
            _panelHandle = PanelManager.Register("Help", Close, () => IsOpen);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>True while the Help overlay is visible (its backdrop is pickable).</summary>
        public bool IsOpen =>
            _overlay != null && _overlay.style.display != DisplayStyle.None;

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

            // Launcher RETIRED (WO-411): the TOWN-HUD's themed gear (VillageHudController top-right,
            // hud_settings art) now opens this menu via HelpMenu.Instance.ToggleOverlay() — one gear,
            // no floating duplicate.

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
            { var tf = HelpFont(); if (tf != null) title.style.unityFont = tf; }
            title.style.color = Color.white;
            title.style.marginBottom = 16;
            card.Add(title);

            card.Add(MakeButton("Report a bug",        OnReportBug));
            card.Add(MakeButton("Controls",            OnShowControls));
            card.Add(MakeButton("Reset Hero & Pet",    OnResetProgress));
            // WO-2026-06-12: "Dev tools" routes to AdminOverlay, which itself SHIPS in release
            // builds (it is NOT #if-gated — see AdminOverlay.cs). Gating only the entry point to
            // dev builds left the owner with no Settings-bar route to the live AdminOverlay in a
            // player build (chord-only). Always surface the entry so the Settings bar reaches the
            // owner-only overlay everywhere AdminOverlay exists. AdminOverlay's own owner-wallet /
            // chord gate is the access control; this is just the launcher.
            card.Add(MakeButton("Dev tools",           OnOpenDevTools));
            card.Add(MakeButton("Credits",             OnShowCredits));
            card.Add(MakeButton("Close",               Close));

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

        // WO-417: explicit font so the settings text renders even if the borrowed PanelSettings'
        // theme has no default font (blank purple rows = backgrounds draw, glyphs don't).
        private static Font _helpFont;
        private static Font HelpFont()
        {
            if (_helpFont == null) _helpFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _helpFont;
        }

        private static Button MakeButton(string label, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.style.height = 40;
            b.style.marginTop = 6; b.style.marginBottom = 6;
            b.style.fontSize = 14;
            var f = HelpFont(); if (f != null) b.style.unityFont = f;
            b.style.backgroundColor = new Color(0.18f, 0.12f, 0.28f, 1f);
            b.style.color = Color.white;
            b.style.borderTopLeftRadius = 8; b.style.borderTopRightRadius = 8;
            b.style.borderBottomLeftRadius = 8; b.style.borderBottomRightRadius = 8;
            return b;
        }

        // ── Actions ────────────────────────────────────────────────────────────
        public void ToggleOverlay() => SetOpen(!IsOpen);

        /// <summary>Explicitly hide the Help overlay (Close button + modal-arbiter close).</summary>
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _overlay.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            // Route through the modal arbiter (DEF-212): opening closes any other open
            // panel; closing clears our slot so an invisible backdrop can never linger
            // and trap input. NotifyOpened/Closed are no-ops if state is unchanged, so
            // the Close callback the handle invokes won't recurse.
            if (open) PanelManager.NotifyOpened(_panelHandle);
            else PanelManager.NotifyClosed(_panelHandle);
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
                string textBody =
                    $"What happened:\n\n\n" +
                    $"Steps to reproduce:\n\n\n" +
                    $"--- auto-captured ---\n" +
                    $"Scene: {scene}\n" +
                    $"Time:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Build: {Application.version} ({Application.unityVersion})\n" +
                    $"Device: {SystemInfo.deviceModel} / {SystemInfo.operatingSystem}\n" +
                    $"Screen: {Screen.width}x{Screen.height}\n" +
                    $"Screenshot: {shot}";

                // 1) POST the report to the live Vercel endpoint (lands in
                //    Postgres alongside the React app's existing reports).
                StartCoroutine(PostBugReport(textBody, scene));

                // 2) Also open the user's default mail client so the
                //    screenshot can be attached (the API endpoint accepts text
                //    only and caps the description at 4 000 chars).
                string subject = Uri.EscapeDataString($"[DotR] Bug — {scene} @ {stamp}");
                string body = Uri.EscapeDataString(textBody +
                    $"\n(Please attach the screenshot file above.)");
                Application.OpenURL($"mailto:{BugReportEmail}?subject={subject}&body={body}");

                ShowToast($"Screenshot saved to {shot}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HelpMenu] Bug report failed: " + ex.Message);
                ShowToast("Bug report failed — see log.");
            }
        }

        /// <summary>
        /// POSTs the bug-report text to the live Vercel endpoint. Failure is
        /// logged but does not break the mailto fallback that runs in parallel.
        /// </summary>
        private System.Collections.IEnumerator PostBugReport(string description, string scene)
        {
            string payload = "{\"description\":" + JsonEncode(description) +
                             ",\"context\":{\"route\":" + JsonEncode(scene) +
                             ",\"appVersion\":" + JsonEncode(Application.version) + "}}";
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            using (var req = new UnityWebRequest(BugReportEndpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                    Debug.Log("[HelpMenu] Bug report posted: " + req.downloadHandler.text);
                else
                    Debug.LogWarning("[HelpMenu] Bug report POST failed: " + req.error + " — mailto fallback still ran.");
            }
        }

        /// <summary>Minimal JSON-string encoder for the bug-report payload.</summary>
        private static string JsonEncode(string s)
        {
            var sb = new StringBuilder(s.Length + 16);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private void OnShowControls()
        {
            ShowToast("Controls — WASD/Arrows/dpad: move • 1/2/3/4 + face buttons: cast Q/W/E/R • Build button: tower placement • F: interact • Esc: pause");
        }

        private void OnShowCredits()
        {
            ShowToast("Defenders of the Realm v2 — DeNelle Studios. Models: KayKit + Tripo. Audio: original soundtrack.");
        }

        /// <summary>
        /// Resets save state via reflection so the player can redo hero + pet
        /// selection. GameStateService.Reset() is the carve-out helper that
        /// wipes progression but keeps wallet binding (per its existing
        /// contract). After reset, route back to HeroSelect.
        /// </summary>
        private void OnResetProgress()
        {
            try
            {
                var t = System.Type.GetType("DeNelle.Core.State.GameStateService, DeNelle.Core");
                if (t == null) { ShowToast("Reset failed — GameStateService missing."); return; }
                var instance = t.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (instance == null) { ShowToast("Reset failed — service not alive."); return; }
                var reset = t.GetMethod("ResetToNewGame",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                reset?.Invoke(instance, null);

                var router = System.Type.GetType("DeNelle.Core.SceneRouter, DeNelle.Core");
                var goHero = router?.GetMethod("GoHeroSelect",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (goHero != null)
                {
                    ShowToast("Reset — heading back to Hero Select…");
                    goHero.Invoke(null, null);
                }
                else
                {
                    ShowToast("Reset done — restart the game to redo selection.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[HelpMenu] Reset failed: " + ex.Message);
                ShowToast("Reset failed — see log.");
            }
        }

        /// <summary>
        /// Opens the AdminOverlay so the owner can trigger waves / give
        /// crystals / mute / etc. without hunting for the Ctrl+Shift+A chord.
        /// </summary>
        private void OnOpenDevTools()
        {
            var admin = UnityEngine.Object.FindFirstObjectByType<AdminOverlay>();
            if (admin == null) { ShowToast("Admin overlay not in scene yet."); return; }
            // Close Help FIRST, then open Admin. Both route through PanelManager, so the
            // arbiter also enforces single-modal; doing it explicitly keeps order clear.
            Close();
            admin.Open();
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
