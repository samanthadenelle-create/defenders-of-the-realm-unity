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

        // OBSIDIAN CANON (WO-562, owner 2026-06-28): the panel language is BLACK + GOLD TRIM, never
        // brown. These tokens were warm stone/wood (#2c2115); they are now near-black obsidian so EVERY
        // surface routing through the shared layer (ElarionUi UIToolkit helpers + ShopTheme + the
        // UiStyle.UiTheme kit tokens seeded from these .r/.g/.b) reskins to black at once. Match
        // ElarionUiKit.ObsidianFill (0.02,0.02,0.025,0.98) so uGUI + UIToolkit panels read identically.
        /// <summary>Panel surface fill — near-black obsidian, lifted a hair for cells/slots over the panel.</summary>
        public static readonly Color PanelStone     = new Color(0.055f, 0.050f, 0.060f, 0.96f);
        /// <summary>Panel backboard / recessed tray fill — the canonical obsidian black.</summary>
        public static readonly Color PanelStoneDark = new Color(0.020f, 0.020f, 0.025f, 0.98f);
        /// <summary>Full-screen dim behind a modal — deep near-black scrim.</summary>
        public static readonly Color Scrim          = new Color(0.015f, 0.012f, 0.020f, 0.82f);

        /// <summary>Trim / inner rim — runic GOLD (was hewn-stone brown; gold-trim canon WO-562).</summary>
        public static readonly Color StoneTrim      = new Color(0.831f, 0.686f, 0.216f, 1f);
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
        /// <summary>Affordable / confirm / success green (TEXT/GLYPH accent on dark panels).</summary>
        public static readonly Color Affordable     = new Color(0.46f, 0.74f, 0.42f, 1f);
        /// <summary>Unaffordable / danger / locked red (TEXT/GLYPH accent on dark panels).</summary>
        public static readonly Color Danger         = new Color(0.86f, 0.34f, 0.32f, 1f);

        // BUTTON-FACE contrast (VISUAL_TOUCH_CONTRAST_AUDIT 2026-07-14, P1): parchment on the
        // OLD bright faces failed WCAG (green ~1.9:1, red ~3.2:1). The FACE deepens; the bright
        // Affordable/Danger above stay for text/glyph accents. These are opaque so the rendered
        // face equals the audited colour (no near-black composite muddying the hue).
        /// <summary>Confirm/green BUTTON FACE — deep green #286b33 (parchment ~5.4:1).</summary>
        public static readonly Color ConfirmFace    = new Color(0.157f, 0.420f, 0.200f, 1f);
        /// <summary>Danger/red BUTTON FACE — deep red #9e2924 (parchment ~6.3:1).</summary>
        public static readonly Color DangerFace     = new Color(0.620f, 0.160f, 0.140f, 1f);
        /// <summary>Disabled / inert stone grey.</summary>
        public static readonly Color Disabled       = new Color(0.30f, 0.26f, 0.22f, 1f);

        // Vitals (mirrored by HudTheme for the uGUI bars).
        public static readonly Color HpRed      = new Color(0.78f, 0.13f, 0.13f, 1f);
        public static readonly Color HpTrack    = new Color(0.10f, 0.04f, 0.04f, 0.85f);
        public static readonly Color ManaBlue   = new Color(0.22f, 0.46f, 0.90f, 1f);
        public static readonly Color ManaTrack  = new Color(0.05f, 0.08f, 0.20f, 0.85f);

        // ── Typography scale ──────────────────────────────────────────────────
        // One ladder used by both toolkits so sizes feel deliberate, not ad-hoc.
        // MOBILE LEGIBILITY (universal-tester feedback 2026-07-04, BINDING): these are
        // REFERENCE-px against the 1080x1920 portrait canvas. The old ladder (24/18/15/13/11)
        // was desktop-px on a phone canvas — 15px body ≈ 0.8% of height, ~3x too small,
        // illegible on a ~6" screen. Re-sized to established mobile standards (iOS HIG 17pt
        // body / Android Material 14–16sp, expressed as % of the 1920 reference height):
        //   Title 88px = 4.6% (header band 4–6%),  Head 64px = 3.3% (sub-header),
        //   Body 50px = 2.6% (body band 2.5–3.5%),  Label 40px = 2.1%,  Micro 32px = 1.7%.
        // Every screen inherits this via ElarionUiKit.Label/Header/Button + ShopTheme. Size
        // only, no restyle. If a zone now overflows with the larger text, that's a per-screen
        // layout follow-up (note it) — do NOT shrink the ladder back to hide overflow.
        public const int FontTitle = 88; // modal / banner title       4.6% of H  (was 24)
        public const int FontHead  = 64; // section header, hero name   3.3% of H  (was 18)
        public const int FontBody  = 50; // standard label / card name  2.6% of H  (was 15)
        public const int FontLabel = 40; // small label / cost / hint   2.1% of H  (was 13)
        public const int FontMicro = 32; // hotkey badge, rune strip    1.7% of H  (was 11)

        // WO-693: the ONE named mobile readability floor for detail surfaces (reference-px on
        // the 1080x1920 canvas). Detail panes pass THIS to ElarionUiKit.FitBlock/FitSingleLine —
        // bands grow or content ellipsis-truncates; text NEVER auto-shrinks below it. Matches
        // ElarionUiKit.FontFloor (30 = the proven nameplate mobile-legible minimum); the kit's
        // post-layout guard may still relax to its 20px FontHardFloor as the last resort. No
        // per-screen font literals — screens name the floor, never a number.
        public const float FontFloorMobile = 30f;

        // ── WO-697: the ONE compact currency/number formatter ─────────────────
        // Kit law (permanent): a currency VALUE never ellipsizes and never
        // auto-shrinks below the font floor — it COMPACTS here instead. The
        // CurrencyChip builder calls this itself, so no caller can reintroduce
        // the six-digit-clips bug (ticket RES-1).

        /// <summary>
        /// Compact, ASCII-only number formatting for currency readouts (WO-697).
        /// Thresholds: &lt; 10,000 renders verbatim as plain digits (NO group
        /// separators — locale separators can be non-ASCII and surprise TMP);
        /// &gt;= 10,000 renders one decimal below 100 of the tier unit ("98.6k"),
        /// none at/above ("100k", "999k", "1.2m", "12m", "1.2b"). Decimals
        /// TRUNCATE (a wallet is never overstated) and a trailing ".0" is
        /// dropped ("10k", never "10.0k"). Negative values keep their sign.
        /// </summary>
        public static string CompactNumber(long v)
        {
            // |long.MinValue| overflows long negation — widen through ulong.
            bool neg = v < 0;
            ulong a = neg ? (ulong)(-(v + 1)) + 1UL : (ulong)v;
            string s;
            if (a < 10000UL)
                s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
            else if (a < 1000000UL)
                s = CompactTier(a, 1000UL, "k");
            else if (a < 1000000000UL)
                s = CompactTier(a, 1000000UL, "m");
            else
                s = CompactTier(a, 1000000000UL, "b");
            return neg ? "-" + s : s;
        }

        /// <summary>Int convenience overload of <see cref="CompactNumber(long)"/>.</summary>
        public static string CompactNumber(int v) => CompactNumber((long)v);

        /// <summary>
        /// WO-1407: THE ONE player-facing duration formatter. ASCII h/m/s: "45s", "14m 15s",
        /// "1h 5m". Every countdown a player reads (the Heart plate wave line, the UIElements
        /// wave overlay, the queue rail timers) prints through here, so no surface can ever
        /// show a bare "855s" again (the merged UI review row 6: raw seconds on the town HUD).
        /// Above 60 seconds the result ALWAYS carries a minutes term - HudLabelFitRegression
        /// [countdown-minutes] sweeps 61..7200 and fails on any bare seconds count.
        /// Negative input clamps to "0s"; hours drop the seconds ("1h 5m"), because a 12-hour
        /// build timer does not need its seconds and the rail column cannot seat them.
        /// </summary>
        public static string Duration(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int h = seconds / 3600, m = (seconds % 3600) / 60, s = seconds % 60;
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m " + s + "s";
            return s + "s";
        }

        // One tier of the compact grammar: one truncated decimal while the scaled
        // value is below 100 ("98.6k"), whole units at/above ("100k").
        private static string CompactTier(ulong a, ulong unit, string suffix)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            ulong whole = a / unit;
            if (whole >= 100UL) return whole.ToString(inv) + suffix;
            ulong tenth = (a / (unit / 10UL)) % 10UL;   // truncated first decimal (no overflow)
            return tenth == 0UL
                ? whole.ToString(inv) + suffix
                : whole.ToString(inv) + "." + tenth.ToString(inv) + suffix;
        }

        // ── Spacing / shape scale ─────────────────────────────────────────────
        public const float PadCard   = 12f;
        public const float PadPanel  = 18f;
        public const float RadiusSm  = 6f;
        public const float RadiusMd  = 10f;
        public const float RadiusLg  = 16f;
        public const float TapTarget = 88f;  // mobile minimum: 44pt (iOS) / 48dp (Material) ≈ 88px in the 1080-wide reference (was 44 = ~half a touch target)

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
            // Gold-trim canon (WO-562): a full, crisp 3px gold border (was a faint 2px @0.55a).
            SetBorderWidth(panel, 3);
            SetBorderColor(panel, new Color(Gold.r, Gold.g, Gold.b, 1f));
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
                case ButtonKind.Confirm:  return ConfirmFace;
                case ButtonKind.Danger:   return DangerFace;
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
