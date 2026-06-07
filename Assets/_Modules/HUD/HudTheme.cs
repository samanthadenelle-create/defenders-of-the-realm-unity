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

namespace DeNelle.HUD
{
    /// <summary>Centralised HUD palette + lazily-built rounded/disc sprites.</summary>
    public static class HudTheme
    {
        // ── Palette (concept: earthy #2c2115 / stone #8b5e3c / gold #d4af37) ──
        public static readonly Color PanelStone     = new Color(0.172f, 0.129f, 0.082f, 0.92f); // #2c2115-ish
        public static readonly Color PanelStoneDark = new Color(0.110f, 0.086f, 0.058f, 0.96f);
        public static readonly Color StoneTrim      = new Color(0.545f, 0.369f, 0.235f, 1f);    // #8b5e3c
        public static readonly Color Gold           = new Color(0.831f, 0.686f, 0.216f, 1f);    // #d4af37
        public static readonly Color GoldButton     = new Color(0.831f, 0.686f, 0.216f, 0.96f);
        public static readonly Color Parchment       = new Color(0.945f, 0.910f, 0.820f, 1f);
        public static readonly Color Ink            = new Color(0.137f, 0.098f, 0.055f, 1f);    // dark text on gold

        // Vitals
        public static readonly Color HpRed      = new Color(0.78f, 0.13f, 0.13f, 1f);
        public static readonly Color HpTrack    = new Color(0.10f, 0.04f, 0.04f, 0.85f);
        public static readonly Color ManaBlue   = new Color(0.22f, 0.46f, 0.90f, 1f);
        public static readonly Color ManaTrack  = new Color(0.05f, 0.08f, 0.20f, 0.85f);
        public static readonly Color CastleGold = new Color(0.84f, 0.70f, 0.26f, 1f);
        public static readonly Color CdShade    = new Color(0.03f, 0.03f, 0.05f, 0.74f);

        // Resource tints (icon discs)
        public static readonly Color Wood    = new Color(0.40f, 0.27f, 0.14f, 1f);
        public static readonly Color Iron    = new Color(0.55f, 0.57f, 0.62f, 1f);
        public static readonly Color Crystal = new Color(0.55f, 0.35f, 0.85f, 1f);

        // Slots / portraits
        public static readonly Color SlotBack     = new Color(0.22f, 0.17f, 0.11f, 0.95f);
        public static readonly Color SlotDisc     = new Color(0.30f, 0.24f, 0.16f, 1f);
        public static readonly Color PortraitFill = new Color(0.16f, 0.13f, 0.10f, 1f);

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

        /// <summary>Add an Image with the rounded stone frame in the given fill colour.</summary>
        public static Image StylePanel(GameObject go, Color fill)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = fill;
            img.sprite = RoundedFrame;
            img.type = Image.Type.Sliced;
            return img;
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
