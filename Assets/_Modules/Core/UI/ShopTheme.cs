// =============================================================================
// ShopTheme — shared Elarion shop visual identity (WO-175).
// -----------------------------------------------------------------------------
// One palette + a set of UI Toolkit styling helpers so every shop surface
// (the Cosmetic Shop in DeNelle.HUD and the PackStore in DeNelle.Wallet) reads
// as ONE authored merchant boutique in Elarion — wood-and-aether frame, warm
// gold accents, violet/aether highlights — instead of a generic dark dialog.
//
// Lives in DeNelle.Core (both DeNelle.HUD and DeNelle.Wallet already reference
// Core, neither references the other) so the identity is shared without a new
// cross-module dependency. Pure styling — no gameplay, no data, no UXML. All
// helpers apply INLINE styles so they survive the project's "UXML/USS does not
// render in player builds" trap (PIPELINE_STATE §8): a re-skin that works in a
// build, not just the editor.
//
// Mobile-first: tap targets are >= 40 px high, text stays legible at thumb size.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Shared Elarion shop palette + UI Toolkit styling helpers. Static, stateless,
    /// all inline-style based. Used by CosmeticShopPanel and PackStore so the two
    /// shop surfaces share one merchant-boutique identity.
    /// </summary>
    public static class ShopTheme
    {
        // ── Palette ──────────────────────────────────────────────────────────
        // Warm-wood + violet/aether merchant boutique. Names describe role.

        /// <summary>Full-screen dim behind the shop window.</summary>
        public static readonly Color Scrim = new Color(0.03f, 0.02f, 0.06f, 0.86f);

        /// <summary>Carved-wood frame outer border (deep walnut).</summary>
        public static readonly Color FrameWood = new Color(0.24f, 0.16f, 0.11f, 1f);

        /// <summary>Inner aether rim that lines the frame (soft violet glow).</summary>
        public static readonly Color FrameRim = new Color(0.62f, 0.45f, 0.86f, 0.85f);

        /// <summary>Gilt edge highlight on the frame + title rule.</summary>
        public static readonly Color Gilt = new Color(0.84f, 0.68f, 0.32f, 1f);

        /// <summary>Shop window background (dark stained wood / stone).</summary>
        public static readonly Color PanelBg = new Color(0.11f, 0.08f, 0.13f, 0.99f);

        /// <summary>Item / card slot background (a touch lighter than the panel).</summary>
        public static readonly Color SlotBg = new Color(0.16f, 0.12f, 0.19f, 1f);

        /// <summary>Recessed list well behind the scrolling cards.</summary>
        public static readonly Color WellBg = new Color(0.07f, 0.05f, 0.10f, 0.92f);

        /// <summary>Primary display text (warm parchment cream).</summary>
        public static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f, 1f);

        /// <summary>Secondary / flavour text (muted parchment).</summary>
        public static readonly Color ParchmentDim = new Color(0.78f, 0.74f, 0.66f, 1f);

        /// <summary>Glimmer / coin gold.</summary>
        public static readonly Color Glimmer = new Color(0.98f, 0.82f, 0.36f, 1f);

        /// <summary>Aether-violet — selected tabs, accents.</summary>
        public static readonly Color Aether = new Color(0.55f, 0.38f, 0.82f, 1f);

        /// <summary>Aether-violet, dim — unselected tab / chip rest state.</summary>
        public static readonly Color AetherDim = new Color(0.22f, 0.16f, 0.32f, 1f);

        /// <summary>Buy / confirm wood-green.</summary>
        public static readonly Color Confirm = new Color(0.30f, 0.46f, 0.28f, 1f);

        /// <summary>Owned / equipped success green.</summary>
        public static readonly Color Owned = new Color(0.40f, 0.66f, 0.42f, 1f);

        /// <summary>Disabled / locked stone grey.</summary>
        public static readonly Color Disabled = new Color(0.26f, 0.22f, 0.26f, 1f);

        // Small decorative glyphs (no font dependency — plain unicode the default
        // UI font renders). A crest before the title, a coin before Glimmer.
        public const string CrestGlyph = "✦";
        public const string CoinGlyph  = "◈";

        // ── Frame / panel ────────────────────────────────────────────────────

        /// <summary>
        /// Turns a full-screen element into the dim scrim that sits behind a shop
        /// window and visibly covers the world beneath it.
        /// </summary>
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
        /// Applies the carved-wood-and-aether shop-window frame to a panel: stained
        /// wood fill, a gilt outer edge, an inner aether rim, generous rounding and
        /// padding. Reads as a merchant's stall window, not a dialog box.
        /// </summary>
        public static void StylePanelFrame(VisualElement panel)
        {
            if (panel == null) return;
            panel.style.backgroundColor = PanelBg;

            SetBorderRadius(panel, 18);
            SetBorderWidth(panel, 3);
            SetBorderColor(panel, FrameWood);

            panel.style.paddingTop = 18; panel.style.paddingBottom = 18;
            panel.style.paddingLeft = 22; panel.style.paddingRight = 22;
        }

        /// <summary>
        /// A thin gilt accent line — used under a header to evoke an inlaid rule.
        /// </summary>
        public static VisualElement MakeRule()
        {
            var rule = new VisualElement();
            rule.style.height = 2;
            rule.style.marginTop = 6;
            rule.style.marginBottom = 10;
            rule.style.backgroundColor = new Color(Gilt.r, Gilt.g, Gilt.b, 0.55f);
            SetBorderRadius(rule, 1);
            return rule;
        }

        /// <summary>
        /// Builds the shop title with a small crest glyph in the game's display
        /// styling (bold, parchment, gilt crest). Returns the row so the caller
        /// can drop it straight into a header.
        /// </summary>
        public static VisualElement MakeTitle(string text)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var crest = new Label(CrestGlyph);
            crest.style.fontSize = 22;
            crest.style.color = Gilt;
            crest.style.marginRight = 8;
            row.Add(crest);

            var title = new Label(text);
            title.style.fontSize = 24;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Parchment;
            title.style.letterSpacing = 1f;
            row.Add(title);

            return row;
        }

        // ── Currency display ─────────────────────────────────────────────────

        /// <summary>
        /// A styled Glimmer chip: coin glyph + amount in gold on a recessed slot.
        /// Pass the Label back so the caller can update the amount later.
        /// </summary>
        public static VisualElement MakeGlimmerChip(out Label amountLabel, int amount)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.paddingTop = 4; chip.style.paddingBottom = 4;
            chip.style.paddingLeft = 10; chip.style.paddingRight = 12;
            chip.style.backgroundColor = new Color(0.18f, 0.14f, 0.08f, 1f);
            SetBorderRadius(chip, 12);
            SetBorderWidth(chip, 1);
            SetBorderColor(chip, new Color(Gilt.r, Gilt.g, Gilt.b, 0.55f));

            var coin = new Label(CoinGlyph);
            coin.style.fontSize = 15;
            coin.style.color = Glimmer;
            coin.style.marginRight = 6;
            chip.Add(coin);

            amountLabel = new Label(amount.ToString("N0"));
            amountLabel.style.fontSize = 15;
            amountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            amountLabel.style.color = Glimmer;
            chip.Add(amountLabel);

            return chip;
        }

        // ── Item icon slot ───────────────────────────────────────────────────

        /// <summary>
        /// A framed item-card icon slot. Prefers a real preview sprite/render; when
        /// none exists yet it falls back to a framed colour tile (still beats a bare
        /// swatch — it reads as an item portrait in a slot, not a debug colour box).
        /// </summary>
        public static VisualElement MakeIconSlot(Texture2D preview, Color tint, float size = 60f)
        {
            var slot = new VisualElement();
            slot.style.width = size; slot.style.height = size;
            slot.style.marginRight = 12;
            SetBorderRadius(slot, 10);
            SetBorderWidth(slot, 2);
            SetBorderColor(slot, FrameWood);
            slot.style.overflow = Overflow.Hidden;
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;

            if (preview != null)
            {
                slot.style.backgroundImage = new StyleBackground(preview);
                slot.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                slot.style.backgroundColor = SlotBg;
            }
            else
            {
                // Framed colour tile with an inner gem highlight so it reads as a
                // portrait slot, not a flat swatch.
                slot.style.backgroundColor = SlotBg;
                var gem = new VisualElement();
                gem.style.width = size * 0.5f; gem.style.height = size * 0.5f;
                gem.style.backgroundColor = tint;
                SetBorderRadius(gem, 7);
                SetBorderWidth(gem, 1);
                SetBorderColor(gem, new Color(1f, 1f, 1f, 0.30f));
                slot.Add(gem);
            }
            return slot;
        }

        // ── Buttons ──────────────────────────────────────────────────────────

        /// <summary>
        /// Styles a themed button with hover/press feedback. <paramref name="kind"/>
        /// picks the palette: Confirm (Buy/green), Aether (neutral/violet),
        /// Disabled (locked/grey). Rounds, pads, and bolds for thumb-size taps.
        /// </summary>
        public static void StyleButton(Button button, ButtonKind kind)
        {
            if (button == null) return;
            Color rest = ButtonRest(kind);
            Color text = kind == ButtonKind.Disabled ? ParchmentDim : Parchment;

            button.style.backgroundColor = rest;
            button.style.color = text;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 14;
            button.style.height = 42; // mobile tap target
            button.style.paddingLeft = 16; button.style.paddingRight = 16;
            button.style.marginTop = 2; button.style.marginBottom = 2;
            SetBorderRadius(button, 10);
            SetBorderWidth(button, 1);
            SetBorderColor(button, new Color(0f, 0f, 0f, 0.30f));

            // Hover / press feedback via inline restyle (USS :hover won't load in a
            // build, so we wire pointer callbacks instead).
            if (kind == ButtonKind.Disabled) return;
            Color hover = Lighten(rest, 0.10f);
            Color press = Darken(rest, 0.12f);
            button.RegisterCallback<PointerEnterEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = hover; });
            button.RegisterCallback<PointerLeaveEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = rest; });
            button.RegisterCallback<PointerDownEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = press; });
            button.RegisterCallback<PointerUpEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = hover; });
        }

        /// <summary>
        /// A small round corner close button (the "X"). Themed plum with a gilt rim.
        /// </summary>
        public static void StyleCloseButton(Button button)
        {
            if (button == null) return;
            button.text = "✕";
            button.style.width = 34; button.style.height = 34;
            button.style.fontSize = 15;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = AetherDim;
            button.style.color = Parchment;
            button.style.paddingLeft = 0; button.style.paddingRight = 0;
            button.style.paddingTop = 0; button.style.paddingBottom = 0;
            SetBorderRadius(button, 17);
            SetBorderWidth(button, 1);
            SetBorderColor(button, new Color(Gilt.r, Gilt.g, Gilt.b, 0.5f));
            Color hover = Lighten(AetherDim, 0.12f);
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = hover);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = AetherDim);
        }

        /// <summary>
        /// Styles a category tab with a clear selected / unselected state. Selected
        /// glows aether-violet with a gilt left marker; unselected sits recessed.
        /// </summary>
        public static void StyleTab(Button tab, bool selected)
        {
            if (tab == null) return;
            tab.style.height = 40;
            tab.style.marginBottom = 6;
            tab.style.fontSize = 14;
            tab.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            tab.style.color = selected ? Parchment : ParchmentDim;
            tab.style.backgroundColor = selected ? Aether : AetherDim;
            SetBorderRadius(tab, 9);
            SetBorderWidth(tab, 1);
            // A gilt left edge marks the active tab.
            tab.style.borderLeftWidth = selected ? 4 : 1;
            tab.style.borderLeftColor = selected ? Gilt : new Color(0f, 0f, 0f, 0.25f);
            tab.style.borderTopColor = new Color(0f, 0f, 0f, 0.25f);
            tab.style.borderRightColor = new Color(0f, 0f, 0f, 0.25f);
            tab.style.borderBottomColor = new Color(0f, 0f, 0f, 0.25f);
        }

        /// <summary>
        /// Styles a small currency-rail / price chip (e.g. SOL / USDC / SKR) with a
        /// selected highlight.
        /// </summary>
        public static void StyleChip(Button chip, bool selected)
        {
            if (chip == null) return;
            chip.style.height = 32;
            chip.style.fontSize = 13;
            chip.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            chip.style.paddingLeft = 12; chip.style.paddingRight = 12;
            chip.style.marginRight = 6; chip.style.marginTop = 2; chip.style.marginBottom = 2;
            chip.style.color = Parchment;
            chip.style.backgroundColor = selected ? Aether : AetherDim;
            SetBorderRadius(chip, 8);
            SetBorderWidth(chip, 1);
            SetBorderColor(chip, selected ? new Color(Gilt.r, Gilt.g, Gilt.b, 0.6f) : new Color(0f, 0f, 0f, 0.25f));
        }

        // ── Scroll well ──────────────────────────────────────────────────────

        /// <summary>
        /// Recesses a ScrollView into a themed list well and hides the default OS
        /// scrollbars (the greyest "unfinished" tell). Content still scrolls by
        /// drag / wheel; the native bars just don't show.
        /// </summary>
        public static void StyleScrollWell(ScrollView scroll)
        {
            if (scroll == null) return;
            scroll.style.backgroundColor = WellBg;
            SetBorderRadius(scroll, 12);
            scroll.style.paddingTop = 8; scroll.style.paddingBottom = 8;
            scroll.style.paddingLeft = 10; scroll.style.paddingRight = 10;

            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            // Collapse the scroller gutters so hidden bars don't reserve space.
            if (scroll.verticalScroller != null) scroll.verticalScroller.style.width = 0;
            if (scroll.horizontalScroller != null) scroll.horizontalScroller.style.height = 0;
        }

        // ── Card slot ────────────────────────────────────────────────────────

        /// <summary>Applies the standard item / pack card-slot styling.</summary>
        public static void StyleCard(VisualElement card)
        {
            if (card == null) return;
            card.style.backgroundColor = SlotBg;
            card.style.marginBottom = 8;
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.paddingLeft = 12; card.style.paddingRight = 12;
            SetBorderRadius(card, 12);
            SetBorderWidth(card, 1);
            SetBorderColor(card, new Color(Gilt.r, Gilt.g, Gilt.b, 0.22f));
        }

        // ── Button kind ──────────────────────────────────────────────────────

        public enum ButtonKind { Confirm, Aether, Disabled }

        private static Color ButtonRest(ButtonKind kind)
        {
            switch (kind)
            {
                case ButtonKind.Confirm:  return Confirm;
                case ButtonKind.Disabled: return Disabled;
                default:                  return Aether;
            }
        }

        // ── Colour helpers ───────────────────────────────────────────────────

        public static Color Lighten(Color c, float amount) =>
            new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);

        public static Color Darken(Color c, float amount) =>
            new Color(
                Mathf.Clamp01(c.r - amount),
                Mathf.Clamp01(c.g - amount),
                Mathf.Clamp01(c.b - amount),
                c.a);

        // ── Border shorthand (no shorthand props on IStyle) ──────────────────

        public static void SetBorderRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        public static void SetBorderWidth(VisualElement e, float w)
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
        }

        public static void SetBorderColor(VisualElement e, Color c)
        {
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }
    }
}
