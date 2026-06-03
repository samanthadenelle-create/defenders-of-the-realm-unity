// =============================================================================
// PlayerProgressPanel — gear-screen overlay showing full progression detail.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// Opens/closes when the gear icon (settings-button) is tapped. Reads
// HeroProgression data via reflection on each open so the HUD assembly
// never gains a compile dependency on DeNelle.Village.
//
// Exact property names from HeroProgression.cs:
//   Level (int), Xp (float), XpToNext (float), LifetimeXp (float)
//
// UXML fallback: when UIToolkit elements are absent, renders an IMGUI overlay.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    /// <summary>
    /// Gear-screen panel showing level, XP progress bar, and lifetime XP.
    /// Opens via the settings button; reads HeroProgression data by reflection.
    /// Falls back to IMGUI when UIToolkit elements are unavailable.
    /// </summary>
    public class PlayerProgressPanel : MonoBehaviour
    {
        // ── UIToolkit elements ────────────────────────────────────────────────
        private VisualElement _panel;
        private Label         _levelBig;
        private Label         _xpDetail;
        private Label         _lifetimeXpLabel;
        private VisualElement _detailFill;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _open;
        private bool _uiReady;

        // ── IMGUI fallback data ───────────────────────────────────────────────
        private int   _imguiLevel = 1;
        private float _imguiXp;
        private float _imguiXpNeeded  = 200f;
        private float _imguiLifetime;

        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            var doc = GetComponent<UIDocument>() ?? FindObjectOfType<UIDocument>();
            if (doc?.rootVisualElement != null)
            {
                _panel           = doc.rootVisualElement.Q<VisualElement>("progress-panel");
                _levelBig        = doc.rootVisualElement.Q<Label>("progress-level");
                _xpDetail        = doc.rootVisualElement.Q<Label>("progress-xp-detail");
                _lifetimeXpLabel = doc.rootVisualElement.Q<Label>("progress-lifetime");
                _detailFill      = doc.rootVisualElement.Q<VisualElement>("progress-fill");

                // Wire gear / settings button.
                var gear = doc.rootVisualElement.Q<Button>("settings-button");
                gear?.RegisterCallback<ClickEvent>(_ => Toggle());

                // Wire close button inside panel.
                var close = doc.rootVisualElement.Q<Button>("progress-close");
                close?.RegisterCallback<ClickEvent>(_ => Close());

                _uiReady = (_panel != null);
            }

            // Start hidden.
            _panel?.AddToClassList("progress-panel--hidden");
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            RefreshData();
            _panel?.RemoveFromClassList("progress-panel--hidden");
            _open = true;
        }

        public void Close()
        {
            _panel?.AddToClassList("progress-panel--hidden");
            _open = false;
        }

        // ── Data refresh via reflection ───────────────────────────────────────

        private void RefreshData()
        {
            var prog = FindHeroProgression();
            if (prog == null) return;

            var type = prog.GetType();

            int   level      = GetInt(type, prog, "Level",      1);
            float currentXp  = GetFloat(type, prog, "Xp",       0f);
            float xpNeeded   = GetFloat(type, prog, "XpToNext", 200f);
            float lifetimeXp = GetFloat(type, prog, "LifetimeXp", 0f);

            // Cache for IMGUI fallback.
            _imguiLevel    = level;
            _imguiXp       = currentXp;
            _imguiXpNeeded = xpNeeded;
            _imguiLifetime = lifetimeXp;

            // Push to UIToolkit labels.
            if (_levelBig        != null) _levelBig.text        = $"Level {level}";
            if (_xpDetail        != null) _xpDetail.text        = $"{currentXp:N0} / {xpNeeded:N0} XP to next level";
            if (_lifetimeXpLabel != null) _lifetimeXpLabel.text = $"Total XP earned: {lifetimeXp:N0}";
            if (_detailFill      != null)
                _detailFill.style.width = Length.Percent(
                    xpNeeded > 0f ? Mathf.Clamp01(currentXp / xpNeeded) * 100f : 0f);
        }

        // ── Reflection helpers ────────────────────────────────────────────────

        private static Component FindHeroProgression()
        {
            // "Player" is a built-in tag; "HeroTarget" may be undefined (FindWithTag
            // throws on an undefined tag) — guard the fallback.
            var heroGo = GameObject.FindWithTag("Player")
                      ?? SafeFindWithTag("HeroTarget");

            Component prog = heroGo != null
                ? heroGo.GetComponent("HeroProgression")
                : null;

            if (prog == null)
            {
                var singletonGo = GameObject.Find("HeroProgression");
                if (singletonGo != null)
                    prog = singletonGo.GetComponent("HeroProgression");
            }

            return prog;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private static int GetInt(System.Type type, object obj, string propName, int fallback)
        {
            try
            {
                var p = type.GetProperty(propName);
                return p != null ? (int)p.GetValue(obj) : fallback;
            }
            catch { return fallback; }
        }

        private static float GetFloat(System.Type type, object obj, string propName, float fallback)
        {
            try
            {
                var p = type.GetProperty(propName);
                return p != null ? (float)p.GetValue(obj) : fallback;
            }
            catch { return fallback; }
        }

        // ── IMGUI fallback ────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_open || _uiReady) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // Centred modal: 360 × 240
            float pw = 360f, ph = 240f;
            float px = (sw - pw) * 0.5f;
            float py = (sh - ph) * 0.5f;

            // Panel background
            GUI.color = new Color(0.08f, 0.06f, 0.14f, 0.94f);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float margin = 20f;
            float cx = px + margin;
            float cy = py + margin;
            float cw = pw - margin * 2f;

            // Level heading
            var headStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = new Color(0.78f, 0.68f, 1f) }
            };
            GUI.Label(new Rect(cx, cy, cw, 30f), $"Level {_imguiLevel}", headStyle);
            cy += 34f;

            // Progress bar background
            float barH = 14f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(cx, cy, cw, barH), Texture2D.whiteTexture);

            // Progress bar fill
            float fill = _imguiXpNeeded > 0f
                ? Mathf.Clamp01(_imguiXp / _imguiXpNeeded)
                : 0f;
            GUI.color = new Color(0.48f, 0.37f, 0.66f, 1f);
            GUI.DrawTexture(new Rect(cx, cy, cw * fill, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;
            cy += barH + 6f;

            // XP detail
            GUI.Label(new Rect(cx, cy, cw, 20f),
                $"{_imguiXp:N0} / {_imguiXpNeeded:N0} XP to next level");
            cy += 24f;

            // Lifetime XP
            GUI.Label(new Rect(cx, cy, cw, 20f),
                $"Total XP earned: {_imguiLifetime:N0}");
            cy += 34f;

            // Close button
            if (GUI.Button(new Rect(cx, cy, 90f, 28f), "Close"))
                Close();
        }
    }
}
