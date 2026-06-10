// =============================================================================
// ElarionUi — the ONE in-game UI theme for Echoes of Elarion (WO-307 polish).
// -----------------------------------------------------------------------------
// Single source of truth for the in-game HUD + build / orient / admin panels so
// every surface reads as ONE designed UI — warm parchment-and-stone with a runic
// gold accent, danger red, affordable green — matching the Brand Bible (warm
// dusk, hopeful-rebuild, runic/stone) and the existing ShopTheme merchant skin.
//
// Lives in DeNelle.Core.UI because Core is the ONLY assembly that DeNelle.HUD
// (uGUI HudTheme) AND DeNelle.Village (UIElements build/orient palettes) both
// reference — so the identity is shared WITHOUT a forbidden HUD<->Village edge
// (CLAUDE.md §5). Pure styling: no gameplay, no data, no UXML. Both a colour
// palette (consumed by the uGUI HudTheme) and a set of UI-Toolkit styling
// helpers (consumed by the Village panels), all applying INLINE styles so they
// survive the "UXML/USS does not render in player builds" trap (PIPELINE_STATE
// §8) — a re-skin that works in a build, not just the editor.
//
// SWAPPABLE BACKGROUND HOOK: PanelBackground / MenuBackground lazily load
//   Resources/UI/panel_bg   (in-HUD panels)
//   Resources/UI/menu_bg    (full-screen modals)
// if present, else null (callers fall back to the solid styled fill). The owner
// drops in a Grok-generated parchment/stone texture at those paths and it slots
// in with NO code change — uGUI gets a 9-slice Sprite, UI-Toolkit a tiled
// StyleBackground via the helpers below.
//
// Mobile-first: tap targets >= 44 px; text legible at thumb size.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Shared Elarion in-game UI palette + typography scale + UI-Toolkit styling
    /// helpers + the swappable panel-background hook. Static, stateless, inline.
    /// </summary>
    public static class ElarionUi
    {
        // ── Palette (warm parchment / stone + runic gold) ─────────────────────
        // Role-named. uGUI (HudTheme) mirrors these so both toolkits agree.

        /// <summary>Earthy stone panel fill (#2c2115).</summary>
        public static readonly Color PanelStone     = new Color(0.172f, 0.129f, 0.082f, 0.94f);
        /// <summary>Darker recessed panel / tray fill.</summary>
        public static readonly Color PanelStoneDark = new Color(0.110f, 0.086f, 0.058f, 0.97f);
        /// <summary>Full-screen dim behind a modal.</summary>
        public static readonly Color Scrim          = new Color(0.02f, 0.015f, 0.04f, 0.62f);

        /// <summary>Hewn-stone trim / inner rim (#8b5e3c).</summary>
        public static readonly Color StoneTrim      = new Color(0.545f, 0.369f, 0.235f, 1f);
        /// <summary>Runic gold — borders, titles, accents (#d4af37).</summary>
        public static readonly Color Gold           = new Color(0.831f, 0.686f, 0.216f, 1f);
        /// <summary>Brighter gilt highlight (#eec848).</summary>
        public static readonly Color Gilt           = new Color(0.933f, 0.784f, 0.282f, 1f);
        /// <summary>Gold call-to-action button fill.</summary>
        public static readonly Color GoldButton     = new Color(0.831f, 0.686f, 0.216f, 0.97f);

        /// <summary>Primary display text — warm parchment cream.</summary>
        public static readonly Color Parchment      = new Color(0.953f, 0.918f, 0.827f, 1f);
        /// <summary>Secondary / flavour text — muted parchment.</summary>
        public static readonly Color ParchmentDim   = new Color(0.78f, 0.74f, 0.66f, 1f);
        /// <summary>Dark ink — text ON gold (high contrast).</summary>
        public static readonly Color Ink            = new Color(0.137f, 0.098f, 0.055f, 1f);

        /// <summary>Aether-violet — runic / magic accent (selected, crystals).</summary>
        public static readonly Color Aether         = new Color(0.55f, 0.38f, 0.82f, 1f);
        /// <summary>Aether-violet dim — rest / unselected.</summary>
        public static readonly Color AetherDim      = new Color(0.24f, 0.18f, 0.34f, 1f);

        // ── State colours ─────────────────────────────────────────────────────
        /// <summary>Affordable / confirm / success green.</summary>
        public static readonly Color Affordable     = new Color(0.46f, 0.74f, 0.42f, 1f);
        /// <summary>Unaffordable / danger / locked red.</summary>
        public static readonly Color Danger         = new Color(0.86f, 0.34f, 0.32f, 1f);
        /// <summary>Disabled / inert stone grey.</summary>
        public static readonly Color Disabled       = new Color(0.30f, 0.26f, 0.22f, 1f);

        // Vitals (mirrored by HudTheme for the uGUI bars).
        public static readonly Color HpRed      = new Color(0.78f, 0.13f, 0.13f, 1f);
        public static readonly Color HpTrack    = new Color(0.10f, 0.04f, 0.04f, 0.85f);
        public static readonly Color ManaBlue   = new Color(0.22f, 0.46f, 0.90f, 1f);
        public static readonly Color ManaTrack  = new Color(0.05f, 0.08f, 0.20f, 0.85f);

        // ── Typography scale ──────────────────────────────────────────────────
        // One ladder used by both toolkits so sizes feel deliberate, not ad-hoc.
        public const int FontTitle = 24; // modal / banner title
        public const int FontHead  = 18; // section header, hero name
        public const int FontBody  = 15; // standard label / card name
        public const int FontLabel = 13; // small label / cost / hint
        public const int FontMicro = 11; // hotkey badge, rune strip

        // ── Spacing / shape scale ─────────────────────────────────────────────
        public const float PadCard   = 12f;
        public const float PadPanel  = 18f;
        public const float RadiusSm  = 6f;
        public const float RadiusMd  = 10f;
        public const float RadiusLg  = 16f;
        public const float TapTarget = 44f;  // mobile minimum

        // Decorative glyphs (default UI font renders these — no font dependency).
        public const string CrestGlyph = "*";
        public const string RuneGlyphs = "+ x * + = - . + : * = - x * = . ";

        // ── Swappable background hook ─────────────────────────────────────────
        // Loaded once; null until the owner drops a texture at the Resources path.
        private static bool _panelBgTried, _menuBgTried;
        private static Texture2D _panelBgTex, _menuBgTex;
        private static Sprite _panelBgSprite;

        /// <summary>In-HUD panel background texture (Resources/UI/panel_bg) or null.</summary>
        public static Texture2D PanelBackground
        {
            get
            {
                if (!_panelBgTried) { _panelBgTried = true; _panelBgTex = Resources.Load<Texture2D>("UI/panel_bg"); }
                return _panelBgTex;
            }
        }

        /// <summary>Full-screen modal background texture (Resources/UI/menu_bg) or null.</summary>
        public static Texture2D MenuBackground
        {
            get
            {
                if (!_menuBgTried) { _menuBgTried = true; _menuBgTex = Resources.Load<Texture2D>("UI/menu_bg"); }
                return _menuBgTex;
            }
        }

        /// <summary>
        /// The panel-bg as a 9-slice uGUI Sprite (built once from the loaded texture),
        /// or null when no texture is present. uGUI panels use this when present and
        /// keep their procedural rounded sprite otherwise.
        /// </summary>
        public static Sprite PanelBackgroundSprite
        {
            get
            {
                var tex = PanelBackground;
                if (tex == null) return null;
                if (_panelBgSprite == null)
                {
                    // Generous border so it 9-slices as a framed panel; if the dropped
                    // texture is smaller, clamp the border to a quarter of each side.
                    float bx = Mathf.Min(24f, tex.width  * 0.25f);
                    float by = Mathf.Min(24f, tex.height * 0.25f);
                    _panelBgSprite = Sprite.Create(
                        tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                        100f, 0, SpriteMeshType.FullRect, new Vector4(bx, by, bx, by));
                }
                return _panelBgSprite;
            }
        }

        /// <summary>
        /// Apply the swappable panel background to a UI-Toolkit element if a texture
        /// exists (tiled / scaled to fill); returns true when one was applied so the
        /// caller can skip its solid fallback fill. No-ops (returns false) otherwise.
        /// </summary>
        public static bool TryApplyPanelBackground(VisualElement e)
        {
            if (e == null) return false;
            var tex = PanelBackground;
            if (tex == null) return false;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            return true;
        }

        /// <summary>Same as <see cref="TryApplyPanelBackground"/> but for the full-screen menu bg.</summary>
        public static bool TryApplyMenuBackground(VisualElement e)
        {
            if (e == null) return false;
            var tex = MenuBackground;
            if (tex == null) return false;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            return true;
        }

        // ── UI-Toolkit styling helpers (Village panels route through these) ───

        /// <summary>Full-screen dim scrim behind a modal panel.</summary>
        public static void StyleScrim(VisualElement scrim)
        {
            if (scrim == null) return;
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0; scrim.style.right = 0;
            scrim.style.top = 0;  scrim.style.bottom = 0;
            scrim.style.backgroundColor = Scrim;
            scrim.style.alignItems = Align.Center;
            scrim.style.justifyContent = Justify.Center;
        }

        /// <summary>
        /// Stone panel with a runic-gold rim + rounding. Prefers the swappable
        /// Resources/UI/panel_bg texture; falls back to the solid stone fill.
        /// </summary>
        public static void StylePanel(VisualElement panel, bool dark = false)
        {
            if (panel == null) return;
            if (!TryApplyPanelBackground(panel))
                panel.style.backgroundColor = dark ? PanelStoneDark : PanelStone;
            SetRadius(panel, RadiusLg);
            SetBorderWidth(panel, 2);
            SetBorderColor(panel, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
        }

        /// <summary>A recessed well (scroll tray / viewport) — darker, lightly framed.</summary>
        public static void StyleWell(VisualElement well)
        {
            if (well == null) return;
            well.style.backgroundColor = PanelStoneDark;
            SetRadius(well, RadiusMd);
            SetBorderWidth(well, 1);
            SetBorderColor(well, new Color(StoneTrim.r, StoneTrim.g, StoneTrim.b, 0.5f));
        }

        /// <summary>Thin gilt rule under a header.</summary>
        public static VisualElement MakeRule()
        {
            var rule = new VisualElement();
            rule.style.height = 2;
            rule.style.marginTop = 6; rule.style.marginBottom = 10;
            rule.style.backgroundColor = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
            SetRadius(rule, 1);
            return rule;
        }

        /// <summary>Title row: gilt crest glyph + bold parchment title at FontTitle.</summary>
        public static VisualElement MakeTitle(string text)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var crest = new Label(CrestGlyph);
            crest.style.fontSize = FontTitle - 2;
            crest.style.color = Gilt;
            crest.style.marginRight = 8;
            row.Add(crest);

            var title = new Label(text);
            title.style.fontSize = FontTitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Gilt;
            title.style.letterSpacing = 1.5f;
            row.Add(title);
            return row;
        }

        public enum ButtonKind { Gold, Neutral, Confirm, Danger, Disabled }

        /// <summary>
        /// Themed button with hover/press feedback (USS :hover won't load in builds,
        /// so feedback is wired via pointer callbacks). Gold = primary CTA (dark ink
        /// text); Neutral = stone; Confirm = green; Danger = red; Disabled = grey.
        /// </summary>
        public static void StyleButton(Button button, ButtonKind kind)
        {
            if (button == null) return;
            Color rest = ButtonRest(kind);
            Color text = kind == ButtonKind.Gold ? Ink
                       : kind == ButtonKind.Disabled ? ParchmentDim
                       : Parchment;

            button.style.backgroundColor = rest;
            button.style.color = text;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = FontBody;
            button.style.minHeight = TapTarget;
            button.style.paddingLeft = 16; button.style.paddingRight = 16;
            button.style.marginTop = 2; button.style.marginBottom = 2;
            SetRadius(button, RadiusMd);
            SetBorderWidth(button, 1);
            SetBorderColor(button, kind == ButtonKind.Gold
                ? new Color(Gilt.r, Gilt.g, Gilt.b, 0.9f)
                : new Color(0f, 0f, 0f, 0.30f));

            if (kind == ButtonKind.Disabled) return;
            Color hover = Lighten(rest, 0.10f);
            Color press = Darken(rest, 0.12f);
            button.RegisterCallback<PointerEnterEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = hover; });
            button.RegisterCallback<PointerLeaveEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = rest; });
            button.RegisterCallback<PointerDownEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = press; });
            button.RegisterCallback<PointerUpEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = hover; });
        }

        private static Color ButtonRest(ButtonKind kind)
        {
            switch (kind)
            {
                case ButtonKind.Gold:     return GoldButton;
                case ButtonKind.Confirm:  return new Color(Affordable.r, Affordable.g, Affordable.b, 0.92f);
                case ButtonKind.Danger:   return new Color(Danger.r, Danger.g, Danger.b, 0.55f);
                case ButtonKind.Disabled: return Disabled;
                default:                  return PanelStone;
            }
        }

        // ── Colour helpers ────────────────────────────────────────────────────
        public static Color Lighten(Color c, float a) =>
            new Color(Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a), Mathf.Clamp01(c.b + a), c.a);

        public static Color Darken(Color c, float a) =>
            new Color(Mathf.Clamp01(c.r - a), Mathf.Clamp01(c.g - a), Mathf.Clamp01(c.b - a), c.a);

        // ── Border shorthand (IStyle has no shorthand props) ──────────────────
        public static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public static void SetBorderWidth(VisualElement e, float w)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        }

        public static void SetBorderColor(VisualElement e, Color c)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }
    }
}
