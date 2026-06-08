// =============================================================================
// HudTheme — SLEEK / MINIMAL Elarion HUD skin (WO-334 redo).
// -----------------------------------------------------------------------------
// Owner direction (2026-06-08): the previous ornate parchment/stone-frame reskin
// was HATED and didn't display. This is the replacement — SLEEK · MINIMAL ·
// modern · mobile-first, but still skinned to FEEL like Elarion:
//   • dark, semi-transparent panels (not opaque stone, not generic flat)
//   • a SUBTLE gold/rune accent line — a HINT of fantasy, minimal execution
//   • clean sans-serif text, thin chrome, maximised play area
//
// All colours still SOURCE from the canonical ElarionUi palette (DeNelle.Core.UI)
// so the uGUI HUD and the UI-Toolkit panels read as one game — we just consume
// them with restraint (low-alpha dark glass + a thin gold rule) instead of the
// heavy stone slabs + gilt rims of the old pass.
//
// DeNelle.HUD assembly only — references Core/UnityEngine, never Village.
// Procedural sprites are built lazily in code so it works headless + in builds
// with no Resources/UXML dependency. Sprite builders are wrapped so a texture
// failure can never blank the HUD (WO-334).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;   // canonical Elarion palette — uGUI mirrors UI-Toolkit

namespace DeNelle.HUD
{
    /// <summary>
    /// Centralised SLEEK uGUI HUD palette + lazily-built rounded sprite. Colour
    /// values are SOURCED from <see cref="ElarionUi"/> so the uGUI HUD and the
    /// UI-Toolkit panels read as ONE theme — consumed minimally (dark glass +
    /// thin gold accent) for the new clean look. Every alias the controller used
    /// stays so nothing else has to change.
    /// </summary>
    public static class HudTheme
    {
        // ── Sleek surfaces — dark translucent "glass", not opaque stone ───────
        /// <summary>Primary HUD panel — dark glass, low alpha so play area shows through.</summary>
        public static readonly Color Glass      = new Color(0.06f, 0.07f, 0.09f, 0.66f);
        /// <summary>Slightly deeper glass for the bottom tray / heavier clusters.</summary>
        public static readonly Color GlassDeep  = new Color(0.04f, 0.05f, 0.07f, 0.78f);
        /// <summary>Recessed track behind a bar fill — near-black, subtle.</summary>
        public static readonly Color Track      = new Color(0.0f, 0.0f, 0.0f, 0.45f);
        /// <summary>Ability/skill cell rest fill — dark glass, a touch lighter than the tray.</summary>
        public static readonly Color Cell        = new Color(0.10f, 0.11f, 0.14f, 0.80f);

        // ── Accent — a SINGLE thin gold rune line, used sparingly ─────────────
        /// <summary>Thin gold accent line / hairline rim (low alpha — a hint, not a frame).</summary>
        public static readonly Color Accent     = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
        /// <summary>Even fainter accent for inner dividers.</summary>
        public static readonly Color AccentSoft = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f);

        // ── Text ──────────────────────────────────────────────────────────────
        public static readonly Color Text       = ElarionUi.Parchment;     // clean cream-white
        public static readonly Color TextDim     = ElarionUi.ParchmentDim;
        public static readonly Color Ink         = ElarionUi.Ink;          // dark text on gold
        public static readonly Color Gold        = ElarionUi.Gold;
        public static readonly Color Gilt        = ElarionUi.Gilt;

        // ── Vitals (kept role-named; mirror ElarionUi) ────────────────────────
        public static readonly Color HpRed      = ElarionUi.HpRed;
        public static readonly Color ManaBlue   = ElarionUi.ManaBlue;
        public static readonly Color CastleGold = ElarionUi.Gold;
        public static readonly Color CdShade    = new Color(0.0f, 0.0f, 0.0f, 0.62f);

        // ── WO-339 · TOWN-HUD lookout status pips (safe→combat) + mini-map hero ─
        /// <summary>Lookout GREEN — village safe, no threat.</summary>
        public static readonly Color LookoutSafe     = new Color(0.36f, 0.74f, 0.42f, 1f);
        /// <summary>Lookout YELLOW — scouts alert / mid-countdown.</summary>
        public static readonly Color LookoutAlert    = new Color(0.92f, 0.78f, 0.28f, 1f);
        /// <summary>Lookout RED — wave incoming in &lt;30s.</summary>
        public static readonly Color LookoutIncoming = new Color(0.86f, 0.30f, 0.28f, 1f);
        /// <summary>Lookout PURPLE — active combat.</summary>
        public static readonly Color LookoutCombat   = ElarionUi.Aether;
        /// <summary>Mini-map hero dot.</summary>
        public static readonly Color HeroDot         = ElarionUi.Gilt;

        // ── Resource tints (small icon dot per currency) ──────────────────────
        public static readonly Color Wood    = new Color(0.62f, 0.45f, 0.27f, 1f);
        public static readonly Color Iron    = new Color(0.70f, 0.72f, 0.78f, 1f);
        public static readonly Color Crystal = ElarionUi.Aether;
        public static readonly Color GoldRes = ElarionUi.Gilt;

        // ── Compatibility aliases (old controller names → new sleek values) ───
        // Kept so any not-yet-updated reference still compiles & reads sleek.
        public static readonly Color PanelStone     = Glass;
        public static readonly Color PanelStoneDark = GlassDeep;
        public static readonly Color StoneTrim      = AccentSoft;
        public static readonly Color GoldButton     = ElarionUi.GoldButton;
        public static readonly Color Parchment      = Text;
        public static readonly Color ParchmentDim   = TextDim;
        public static readonly Color GoldRim        = Accent;
        public static readonly Color TrimSoft       = AccentSoft;
        public static readonly Color Well           = Track;
        public static readonly Color SlotBack       = Cell;
        public static readonly Color SlotDisc       = new Color(0.16f, 0.17f, 0.22f, 0.9f);
        public static readonly Color PortraitFill   = new Color(0.10f, 0.11f, 0.14f, 0.95f);

        // ── Typography scale (mirrors ElarionUi) ──────────────────────────────
        public const int FontTitle = ElarionUi.FontTitle;
        public const int FontHead  = ElarionUi.FontHead;
        public const int FontBody  = ElarionUi.FontBody;
        public const int FontLabel = ElarionUi.FontLabel;
        public const int FontMicro = ElarionUi.FontMicro;

        // ── Procedural sprites (lazily built once; failure-safe for WO-334) ───
        private static Sprite _rounded;
        private static Sprite _disc;
        private static bool _roundedTried, _discTried;

        /// <summary>9-sliced rounded-rect (sharp small radius — sleek, not bubbly).
        /// Returns null if texture creation fails so the HUD never halts.</summary>
        public static Sprite RoundedFrame
        {
            get
            {
                if (!_roundedTried) { _roundedTried = true; _rounded = TryBuild(BuildRoundedSprite); }
                return _rounded;
            }
        }

        /// <summary>Solid circle for the small currency dots. Null-safe.</summary>
        public static Sprite Disc
        {
            get
            {
                if (!_discTried) { _discTried = true; _disc = TryBuild(BuildDiscSprite); }
                return _disc;
            }
        }

        private static Sprite TryBuild(System.Func<Sprite> builder)
        {
            // WO-334: a runtime Texture2D build that throws under WebGL must not
            // bubble up and blank the whole HUD — fall back to a null sprite
            // (Image still renders as a tinted quad).
            try { return builder(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HudTheme] procedural sprite build failed (using flat quad): " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Add a sleek panel Image — dark translucent glass with a soft rounded
        /// rect. NO heavy frame; the accent line is added separately + sparingly.
        /// </summary>
        public static Image StylePanel(GameObject go, Color fill)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = RoundedFrame;
            img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = fill;
            return img;
        }

        /// <summary>
        /// SLEEK accent — a single thin gold rule along the BOTTOM edge of the
        /// panel (a hint of Elarion runic gold), NOT a full ornate rim. Ignores
        /// raycasts. Returns the line Image so callers can retint/hide it.
        /// </summary>
        public static Image AddRim(GameObject parent, Color rimColor)
        {
            var go = new GameObject("Accent");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            // Thin underline hugging the bottom edge — minimal chrome.
            rt.anchorMin = new Vector2(0.06f, 0f);
            rt.anchorMax = new Vector2(0.94f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 1.5f);
            rt.anchoredPosition = new Vector2(0f, 1.5f);
            var img = go.AddComponent<Image>();
            img.color = rimColor;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
            return img;
        }

        /// <summary>Recessed near-black track behind a bar fill — flat + subtle.</summary>
        public static Image StyleWell(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = RoundedFrame;
            img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Track;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Wire idle/hover/pressed/disabled tints onto a uGUI Button — clean,
        /// subtle multiply feedback (no colour shift, just brightness).
        /// </summary>
        public static void StyleButtonColors(Button button, Color idle)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.ColorTint;
            var cb = button.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            cb.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.07f;
            button.colors = cb;
        }

        // ── Sprite generation ────────────────────────────────────────────────
        // White rounded-rect, small radius for a crisp modern corner.
        private static Sprite BuildRoundedSprite()
        {
            const int size = 32;
            const int radius = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x, y, size, size, radius);
                    byte a = (byte)Mathf.Clamp((int)((1f - d) * 255f), 0, 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static float RoundedRectDistance(int x, int y, int w, int h, int radius)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float dx = Mathf.Max(Mathf.Max(radius - fx, fx - (w - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - fy, fy - (h - radius)), 0f);
            float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(dist + 0.5f);
        }

        private static Sprite BuildDiscSprite()
        {
            const int size = 48;
            float r = size * 0.5f - 1f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    byte a = (byte)Mathf.Clamp((int)((r - d) * 255f), 0, 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
