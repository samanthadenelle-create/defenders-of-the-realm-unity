// =============================================================================
// HudTheme — shared fantasy palette + procedural panel/disc sprites (WO-307).
// -----------------------------------------------------------------------------
// One place for the in-game HUD's look so every cluster matches the owner
// concepts (docs/design/hud-*-concept.jpg): earthy stone panels, gold trim,
// parchment text. Sprites are generated in code (no external assets / no
// Resources dependency) so this works headless and in builds, and renders the
// rounded-rect frames + circular discs the concepts use without UXML.
//
// DeNelle.HUD assembly only — references Core/UnityEngine, never Village.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;   // canonical Elarion palette — uGUI mirrors UI-Toolkit

namespace DeNelle.HUD
{
    /// <summary>
    /// Centralised uGUI HUD palette + lazily-built rounded/disc sprites. Colour
    /// values are SOURCED from <see cref="ElarionUi"/> (DeNelle.Core.UI) so the
    /// uGUI HUD and the UI-Toolkit build/orient/admin panels read as ONE theme.
    /// These aliases stay so the controller's existing references keep compiling.
    /// </summary>
    public static class HudTheme
    {
        // ── Palette — aliased to the canonical ElarionUi palette ──────────────
        public static readonly Color PanelStone     = ElarionUi.PanelStone;
        public static readonly Color PanelStoneDark = ElarionUi.PanelStoneDark;
        public static readonly Color StoneTrim      = ElarionUi.StoneTrim;
        public static readonly Color Gold           = ElarionUi.Gold;
        public static readonly Color GoldButton     = ElarionUi.GoldButton;
        public static readonly Color Parchment      = ElarionUi.Parchment;
        public static readonly Color ParchmentDim   = ElarionUi.ParchmentDim;
        public static readonly Color Ink            = ElarionUi.Ink;
        public static readonly Color Affordable     = ElarionUi.Affordable;
        public static readonly Color Danger         = ElarionUi.Danger;

        // Vitals
        public static readonly Color HpRed      = ElarionUi.HpRed;
        public static readonly Color HpTrack    = ElarionUi.HpTrack;
        public static readonly Color ManaBlue   = ElarionUi.ManaBlue;
        public static readonly Color ManaTrack  = ElarionUi.ManaTrack;
        public static readonly Color CastleGold = new Color(0.84f, 0.70f, 0.26f, 1f);
        public static readonly Color CdShade    = new Color(0.03f, 0.03f, 0.05f, 0.74f);

        // Resource tints (icon discs)
        public static readonly Color Wood    = new Color(0.40f, 0.27f, 0.14f, 1f);
        public static readonly Color Iron    = new Color(0.55f, 0.57f, 0.62f, 1f);
        public static readonly Color Crystal = ElarionUi.Aether;

        // Slots / portraits
        public static readonly Color SlotBack     = new Color(0.22f, 0.17f, 0.11f, 0.95f);
        public static readonly Color SlotDisc     = new Color(0.30f, 0.24f, 0.16f, 1f);
        public static readonly Color PortraitFill = new Color(0.16f, 0.13f, 0.10f, 1f);

        // ── Typography scale (mirrors ElarionUi so uGUI text matches) ─────────
        public const int FontTitle = ElarionUi.FontTitle;
        public const int FontHead  = ElarionUi.FontHead;
        public const int FontBody  = ElarionUi.FontBody;
        public const int FontLabel = ElarionUi.FontLabel;
        public const int FontMicro = ElarionUi.FontMicro;

        // ── Procedural sprites (lazily built once) ───────────────────────────
        private static Sprite _rounded;
        private static Sprite _disc;

        /// <summary>9-sliced rounded-rect frame with a subtle gold edge.</summary>
        public static Sprite RoundedFrame
        {
            get { if (_rounded == null) _rounded = BuildRoundedSprite(); return _rounded; }
        }

        /// <summary>Solid circle for icon/rune discs.</summary>
        public static Sprite Disc
        {
            get { if (_disc == null) _disc = BuildDiscSprite(); return _disc; }
        }

        /// <summary>
        /// Add an Image with the panel frame in the given fill colour. Prefers the
        /// SWAPPABLE Resources/UI/panel_bg texture (9-sliced, full-white tint so the
        /// art shows true) when the owner has dropped one in; otherwise the procedural
        /// rounded stone frame tinted by <paramref name="fill"/>. No code change needed
        /// to slot the texture in later.
        /// </summary>
        public static Image StylePanel(GameObject go, Color fill)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            var bg = ElarionUi.PanelBackgroundSprite;
            if (bg != null)
            {
                img.sprite = bg;
                img.color = new Color(1f, 1f, 1f, fill.a); // show the art; keep the panel's alpha
            }
            else
            {
                img.sprite = RoundedFrame;
                img.color = fill;
            }
            img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>
        /// Wire idle/hover/pressed/disabled tints onto a uGUI Button so every HUD
        /// button reacts to touch consistently. <paramref name="idle"/> is the rest
        /// fill; hover lightens, pressed darkens, disabled greys.
        /// </summary>
        public static void StyleButtonColors(Button button, Color idle)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.ColorTint;
            var cb = button.colors;
            cb.normalColor      = Color.white;             // multiplies the Image colour
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor     = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            button.colors = cb;
        }

        // ── Sprite generation ────────────────────────────────────────────────
        // White rounded-rect with a faint border ring → tint via Image.color.
        private static Sprite BuildRoundedSprite()
        {
            const int size = 48;
            const int radius = 12;
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

        // Soft anti-aliased coverage for a rounded rectangle (1 inside → 0 out).
        private static float RoundedRectDistance(int x, int y, int w, int h, int radius)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float dx = Mathf.Max(Mathf.Max(radius - fx, fx - (w - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - fy, fy - (h - radius)), 0f);
            float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(dist + 0.5f); // 0 inside, →1 just outside
        }

        private static Sprite BuildDiscSprite()
        {
            const int size = 64;
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
