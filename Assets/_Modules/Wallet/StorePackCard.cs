// =============================================================================
// StorePackCard — THE ONE Night Market card template (UI-001 §R3, owner ruling 4)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet
//
// Owner ruling 2026-08-22 (verbal, ruling 4), quoted so it cannot be paraphrased
// away: "each pack in a sleek rounded rectangle with an image for the pack and
// below show what they are getting - different colors with backgrounds glowing".
//
// THIS FILE IS THE WHOLE OF THAT RULING, AND IT IS ONE TEMPLATE WITH THREE
// VARIANTS — Featured / Standard / Compact. UI-001 §2 permits EXACTLY ONE
// store-local helper on top of the kit, and this is it. A second card builder,
// a per-SKU styling branch, or a second palette is the parallel-design-system
// failure that §2 exists to forbid.
//
// ⛔ THE ASSET BILL IS EXACTLY TWO WHITE SPRITES (§R3), and neither is new art:
//   1. ElarionUiKit's rounded 9-slice   — every corner in the game already uses it;
//      ApplyRounded(img, radiusPx) only rescales its baked 6 px border.
//   2. ElarionUiKit.RadialGlowSprite    — a white radial falloff, added beside it.
// Both are TINTED here through NightMarketPalette / the authored orbTint. There
// are NO per-band textures, NO particles, and ZERO VFX loop slots: the glow is a
// static Image, which is the load-bearing half of ruling 4's implementation note.
//
// ⛔ COLOUR NEVER CARRIES MEANING ALONE. The owner is red/green colourblind, so
// every card states its band in WORDS (the band eyebrow above it), its state in a
// WORD (the state pill), and its price in DIGITS. Strip every hue and the card
// still reads — that is the acceptance test, and it is a greyscale capture, not a
// claim. See NightMarketPalette's header for the four-carrier ordering.
//
// ⛔ TMP IS ASCII-ONLY. The wireframe's emoji glyphs are STAND-INS. In-game the
// art-well glyph is a ConceptIconResolver sprite, or two-letter ASCII initials
// derived from the pack name. Shipping an emoji is a tofu box on first device boot.
//
// ⛔ EVERY VARIANT IS AUTHORED >= MinTouchPx(112) ON BOTH AXES, IN REFERENCE PX,
// so ElarionUiKit.ClampMinTouch is a NO-OP (WO-1060 Assert A). The heights below
// are FIXED reference px and not fractions of a parent zone, precisely because the
// 2026-08-22 device frames showed what a fraction of a SHRUNKEN landscape budget
// does: the usable canvas is 2120 x 978 reference units (UI-001 §0.4), not 1080 x
// 1920, so a height authored as a fraction of "the panel" lands at roughly half
// what its author measured. Author the number; let the column scroll.
//
// ⛔ THIS FILE TOUCHES NO COMMERCE AUTHORITY. It renders decisions; it never makes
// one. `actionable`, the CTA face and the refusal sentence are all PASSED IN by the
// caller, which got them from PurchaseGate — UI-002's binding rule. There is no
// PurchaseGate reference in this file and there must never be one, or the button
// and the charge acquire two different opinions.
//
// Landscape only; code-built uGUI (UXML does not work in player builds — the
// PackStore.uxml/.uss pair still on disk is a TRAP, not a starting point).
// =============================================================================

using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;

namespace DeNelle.Wallet
{
    /// <summary>Which of the one template's three sizes a card is drawn at.</summary>
    public enum StorePackCardVariant
    {
        /// <summary>The spotlight subject: tallest art well, room for the ledger beneath.</summary>
        Featured = 0,
        /// <summary>The shelf default — two-up in the market column.</summary>
        Standard = 1,
        /// <summary>The dense rung (impulse / gap rows). Still a full tap target.</summary>
        Compact = 2,
    }

    /// <summary>
    /// Everything the card DISPLAYS, resolved by the caller. Deliberately a plain data carrier:
    /// the card must not be able to compute a price, a state or an entitlement, because a second
    /// place that computes commerce truth is a second authority (UI-002).
    /// </summary>
    public struct StorePackCardModel
    {
        /// <summary>Stable id — used for the GameObject name and the tap callback only.</summary>
        public string Sku;
        /// <summary>Pack display name. Wraps to two lines; never ellipsized (§5).</summary>
        public string Name;
        /// <summary>The goods line: top goods, then "+N more", then convenience. Caller-derived
        /// from the SAME describer the spotlight ledger uses, so the two can never disagree.</summary>
        public string Contents;
        /// <summary>The arithmetic caption ("1,922 goods per $"). Empty = drawn absent, never faked.</summary>
        public string ValueCaption;
        /// <summary>Rail amount with its unit, e.g. "36 SKR". NEVER clipped (§5).</summary>
        public string PriceMajor;
        /// <summary>Fiat reference, e.g. "$2.99". Empty when no quote backs it.</summary>
        public string PriceMinor;
        /// <summary>Uppercase merchandising pill. ⛔ Only badges the arithmetic supports (§7).</summary>
        public string Badge;
        /// <summary>State WORD ("Owned" / "Locked" / "Your gap"). Outranks the badge when present.</summary>
        public string StateWord;
        /// <summary>The band this card lives in — supplies the accent light.</summary>
        public StoreBand Band;
        /// <summary>Authored per-pack tint (packs.json <c>orbTint</c>). Empty falls back to the band light.</summary>
        public string OrbTint;
        /// <summary>Concept ids tried in order for the art-well glyph. Null/miss -> ASCII initials.</summary>
        public string[] GlyphConcepts;
        /// <summary>Draws the selected treatment (brighter bloom + a 1 px inset ring).</summary>
        public bool Selected;
    }

    /// <summary>Live handles a caller needs after the build (selection repaint, state repaint).</summary>
    public sealed class StorePackCardHandle
    {
        public GameObject Root;
        public Button Button;
        public Image Glow;
        public Image Ring;
        public TextMeshProUGUI StateLabel;
        public TextMeshProUGUI PriceLabel;
    }

    /// <summary>The single Night Market card template. Static builders; holds no state.</summary>
    public static class StorePackCard
    {
        // =====================================================================
        //  THE MEASUREMENTS — reference px on the 2120 x 978 landscape budget.
        // ---------------------------------------------------------------------
        //  Every number here is AUTHORED, not derived from a parent fraction, and
        //  every one of them clears MinTouchPx(112) on both axes by construction.
        //  The shortest card is Compact at 228 px tall: 116 px of headroom over
        //  the floor, so no re-layout of the column can quietly push it under.
        // =====================================================================

        /// <summary>Card corner radius, reference px (§R3: "~25 ref").</summary>
        public const float CornerRadiusPx = 25f;

        /// <summary>Card heights per variant, reference px.</summary>
        public const float FeaturedHeightPx = 470f;
        public const float StandardHeightPx = 344f;
        public const float CompactHeightPx  = 228f;

        /// <summary>Art-well heights per variant, reference px (§R3: standard 152, compact 101).</summary>
        public const float FeaturedArtPx = 228f;
        public const float StandardArtPx = 152f;
        public const float CompactArtPx  = 101f;

        /// <summary>Minimum authored card WIDTH. Two-up in the ~1058 px market column leaves
        /// ~500 each; this is the floor a third column would have to respect.</summary>
        public const float MinCardWidthPx = 300f;

        // Glow alphas (§R3). Named so the greyscale oracle can cite them.
        private const float GlowAlpha         = 0.20f;
        private const float GlowAlphaSelected = 0.35f;
        private const float ArtRadialAlpha    = 0.28f;
        private const float BorderAlpha       = 0.50f;

        /// <summary>How far the bloom bleeds OUTSIDE the card rect, reference px. The glow is a
        /// raycast-off decoration; it deliberately does not enlarge the tap target.</summary>
        private const float GlowBleedPx = 34f;

        // The dark vertical gradient behind every card (§R3): #1A1424 -> #110D19.
        private static readonly Color CardTop    = Hex(0x1A, 0x14, 0x24);
        private static readonly Color CardBottom = Hex(0x11, 0x0D, 0x19);

        // Text floors, reference px. UI-001 §8 states the screen-px floors at 2340x1080
        // (legal >=30 / body >=40 / names >=44 / CTA price >=54); at the kit's scale 1.104
        // those are within a rounding of the same numbers in reference px, so the reference
        // values are used directly and every one of them clears ElarionUi.FontFloorMobile(30).
        private const int FontName    = 44;
        private const int FontBody    = 40;
        private const int FontCaption = 32;
        private const int FontPrice   = 54;
        private const int FontMinor   = 32;
        private const int FontBadge   = 30;

        /// <summary>Art-well height for a variant, reference px.</summary>
        public static float ArtWellHeight(StorePackCardVariant v) =>
            v == StorePackCardVariant.Featured ? FeaturedArtPx :
            v == StorePackCardVariant.Compact  ? CompactArtPx  : StandardArtPx;

        /// <summary>Card height for a variant, reference px.</summary>
        public static float CardHeight(StorePackCardVariant v) =>
            v == StorePackCardVariant.Featured ? FeaturedHeightPx :
            v == StorePackCardVariant.Compact  ? CompactHeightPx  : StandardHeightPx;

        /// <summary>
        /// Build one card under <paramref name="parent"/>.
        /// <para>The WHOLE CARD is the tap target (§R3) — <paramref name="onTap"/> is wired to the
        /// root Button and nothing inside the card is separately tappable, so Assert B cannot find
        /// two interactive rects in one place here.</para>
        /// <para>Never throws on missing data: an absent sprite falls back to ASCII initials, an
        /// absent caption is DRAWN ABSENT rather than invented, and a failed sprite build skips the
        /// bloom instead of painting a white quad.</para>
        /// </summary>
        public static StorePackCardHandle Build(Transform parent, StorePackCardModel model,
                                                StorePackCardVariant variant, Action onTap)
        {
            var handle = new StorePackCardHandle();
            if (parent == null) return handle;

            Color light  = NightMarketPalette.For(model.Band);
            Color accent = NightMarketPalette.ParseTint(model.OrbTint, light);
            float cardH  = CardHeight(variant);
            float artH   = ArtWellHeight(variant);

            // ── ROOT ─────────────────────────────────────────────────────────
            // LayoutElement carries the AUTHORED height so a vertical layout group in the market
            // column cannot negotiate it below the floor. flexibleWidth shares the row.
            var rootGo = new GameObject("packcard-" + (model.Sku ?? "unknown"),
                typeof(RectTransform), typeof(LayoutElement), typeof(Button), typeof(Image));
            rootGo.transform.SetParent(parent, false);
            handle.Root = rootGo;

            var le = rootGo.GetComponent<LayoutElement>();
            le.minHeight = cardH;
            le.preferredHeight = cardH;
            le.minWidth = MinCardWidthPx;
            le.flexibleWidth = 1f;

            // The root Image is the raycast surface AND the card's body fill in one object: a
            // separate transparent hit-plate would be a second interactive rect stacked on the
            // visible one, which Assert B correctly reports as an overlap.
            var body = rootGo.GetComponent<Image>();
            body.color = CardBottom;
            ElarionUiKit.ApplyRounded(body, CornerRadiusPx);
            body.raycastTarget = true;

            var card = rootGo.transform;

            // ── GLOW — behind everything, bleeding outside the rect ──────────
            handle.Glow = AddGlow(card, accent, model.Selected ? GlowAlphaSelected : GlowAlpha);

            // ── The dark vertical gradient (top half lightened toward #1A1424) ─
            // Two flat rounded plates rather than a gradient texture: the asset bill is two
            // sprites and a third texture for a background ramp is exactly the drift §R3 bans.
            var topHalf = Plate(card, CardTop, new Vector2(0f, 0.45f), new Vector2(1f, 1f));
            if (topHalf != null) topHalf.color = new Color(CardTop.r, CardTop.g, CardTop.b, 0.85f);

            // ── ART WELL, ON TOP (ruling 4) ──────────────────────────────────
            BuildArtWell(card, model, accent, artH, cardH, variant);

            // ── 1 px border in the band colour at .5 alpha (§R3) ─────────────
            handle.Ring = AddRing(card, new Color(accent.r, accent.g, accent.b, BorderAlpha), 1f);

            // Selected also gets a 1 px INSET ring — a second, brighter carrier so selection is
            // never the bloom alone (the bloom is the first thing a greyscale read loses).
            if (model.Selected)
                AddRing(card, new Color(1f, 1f, 1f, 0.30f), 1f, inset: 5f);

            // ── TEXT STACK, BENEATH THE ART (ruling 4) ───────────────────────
            // Anchored from the TOP in reference px so each row's height is the authored number,
            // not a share of whatever the parent turned out to be.
            float y = artH + 14f;

            var name = TopAnchoredText(card, model.Name, FontName, ElarionUi.Parchment,
                FontStyles.Bold, TextAlignmentOptions.TopLeft, y, variant == StorePackCardVariant.Compact ? 52f : 96f);
            if (name != null)
            {
                name.textWrappingMode = TextWrappingModes.Normal;   // names WRAP (§5)
                // FitBlock is the kit's own overflow protection: auto-size DOWN inside
                // [FontFloorMobile .. FontName] and only then truncate. Never a raw Overflow, which
                // would draw the name outside its rect and onto the card above it -- the exact
                // class of spill AuditGeometry rule 1 exists to catch.
                ElarionUiKit.FitBlock(name, ElarionUi.FontFloorMobile, FontName);
            }
            y += (variant == StorePackCardVariant.Compact ? 52f : 96f) + 6f;

            if (variant != StorePackCardVariant.Compact)
            {
                var contents = TopAnchoredText(card, model.Contents, FontBody,
                    new Color(0.90f, 0.93f, 0.98f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.TopLeft, y, 92f);
                if (contents != null)
                {
                    contents.textWrappingMode = TextWrappingModes.Normal;
                    ElarionUiKit.FitBlock(contents, ElarionUi.FontFloorMobile, FontBody);
                }
                y += 98f;

                // The goods-per-dollar caption. ABSENT when the caller could not compute it —
                // an invented value line is the claim-the-arithmetic-does-not-support defect (§7).
                if (!string.IsNullOrEmpty(model.ValueCaption))
                {
                    TopAnchoredText(card, model.ValueCaption, FontCaption, ElarionUi.ParchmentDim,
                        FontStyles.Italic, TextAlignmentOptions.TopLeft, y, 40f);
                }
            }

            // ── PRICE ROW, pinned to the BOTTOM ──────────────────────────────
            // Bottom-pinned, so a two-line name above can never push the price out of the card —
            // "quantities and currency must never be clipped" is an acceptance criterion, and the
            // 2026-08-22 frames failed it by showing "20 SKR" for a 120 SKR pack.
            handle.PriceLabel = BottomAnchoredText(card, model.PriceMajor, FontPrice, ElarionUi.Gilt,
                FontStyles.Bold, TextAlignmentOptions.BottomLeft, 14f, 62f, 0.06f, 0.62f);
            // ⛔ THE PRICE IS THE ONE STRING ON THIS SCREEN THAT MUST NOT CLIP. On 2026-08-22 the
            // owner's device showed "20 SKR" for a 120 SKR pack and "6 SKR" for a 36 SKR pack --
            // the leading digit occluded. FitSingleLine shrinks toward the floor before it would
            // ever truncate, so the digits survive a narrower column.
            if (handle.PriceLabel != null)
                ElarionUiKit.FitSingleLine(handle.PriceLabel, ElarionUi.FontFloorMobile, FontPrice);
            if (!string.IsNullOrEmpty(model.PriceMinor))
                BottomAnchoredText(card, model.PriceMinor, FontMinor, ElarionUi.Parchment,
                    FontStyles.Normal, TextAlignmentOptions.BottomRight, 18f, 44f, 0.62f, 0.94f);

            // ── THE STATE / BADGE PILL — top-right of the art well ───────────
            // The state WORD outranks the merchandising badge: "Owned" must never be hidden behind
            // "BEST VALUE", or the player is invited to buy what they already have.
            string pill = !string.IsNullOrEmpty(model.StateWord) ? model.StateWord : model.Badge;
            // ⚠ NOT uppercased here. The authored badges ARE already uppercase where the merchandiser
            // meant them to be ("BEST START"), and founders-vow's ruled replacement for its retired
            // FOMO copy is a SENTENCE -- "Founders are named on the Heart." -- which a forced
            // ToUpperInvariant would shout. Presentation must not overrule authored copy.
            if (!string.IsNullOrEmpty(pill))
                handle.StateLabel = BuildPill(card, Ascii(pill), cardH, artH);

            // ── THE WHOLE CARD IS THE TAP TARGET ─────────────────────────────
            var btn = rootGo.GetComponent<Button>();
            btn.targetGraphic = body;
            handle.Button = btn;
            if (onTap != null) btn.onClick.AddListener(() => onTap());

            // ⛔ A PROOF, NOT A RESCUE. Every variant is authored >= 228 px tall and >= 300 px wide,
            // so this call CANNOT fire. It is here so that if a future re-layout ever drops the card
            // under the floor, WO-1060's recorder names this exact control instead of the owner
            // finding a card grown over its neighbour on a device days later.
            ElarionUiKit.ClampMinTouch(btn);

            return handle;
        }

        /// <summary>Repaint selection without rebuilding the card (bloom + inset ring only).</summary>
        public static void SetSelected(StorePackCardHandle handle, bool selected)
        {
            if (handle == null || handle.Glow == null) return;
            var c = handle.Glow.color;
            handle.Glow.color = new Color(c.r, c.g, c.b, selected ? GlowAlphaSelected : GlowAlpha);
        }

        // =====================================================================
        //  Pieces
        // =====================================================================

        private static void BuildArtWell(Transform card, StorePackCardModel model, Color accent,
                                         float artH, float cardH, StorePackCardVariant variant)
        {
            float top = 1f - (artH / Mathf.Max(1f, cardH));

            var wellGo = new GameObject("ArtWell", typeof(RectTransform), typeof(Image));
            wellGo.transform.SetParent(card, false);
            var wrt = (RectTransform)wellGo.transform;
            wrt.anchorMin = new Vector2(0f, top); wrt.anchorMax = new Vector2(1f, 1f);
            wrt.offsetMin = new Vector2(4f, 0f); wrt.offsetMax = new Vector2(-4f, -4f);

            // ⛔ NEVER NEAR-BLACK (§4 / P1-7). The first delivery shipped embossed near-black wells
            // presented as product art; the rule is a DELIBERATE two-tone placeholder that reads as
            // "art has not landed yet", not as "this pack is empty". The two tones are derived from
            // the pack's own authored tint, so no per-pack texture and no per-SKU branch is needed.
            var well = wellGo.GetComponent<Image>();
            well.color = Darken(accent, 0.22f);
            ElarionUiKit.ApplyRounded(well, CornerRadiusPx);
            well.raycastTarget = false;

            // The second tone: a top-centre radial of the band colour (§R3, alpha .28). This is
            // sprite two of the two-sprite bill, tinted — not a gradient texture.
            var glow = ElarionUiKit.RadialGlowSprite;
            if (glow != null)
            {
                var g = new GameObject("ArtRadial", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(wellGo.transform, false);
                var grt = (RectTransform)g.transform;
                grt.anchorMin = new Vector2(0.10f, 0.10f); grt.anchorMax = new Vector2(0.90f, 1.45f);
                grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                var gi = g.GetComponent<Image>();
                gi.sprite = glow;
                gi.color = new Color(accent.r, accent.g, accent.b, ArtRadialAlpha);
                gi.raycastTarget = false;
            }

            // ── THE GLYPH — icon sprite, else ASCII initials. NEVER an emoji. ──
            Sprite icon = null;
            if (model.GlyphConcepts != null && model.GlyphConcepts.Length > 0)
            {
                try { icon = ConceptIconResolver.ResolveAny(model.GlyphConcepts); }
                catch (Exception e)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Store",
                        "StorePackCard: icon resolve threw for '" + (model.Sku ?? "?") + "' (" +
                        e.GetType().Name + "); falling back to ASCII initials.");
                }
            }

            if (icon != null)
            {
                var ig = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
                ig.transform.SetParent(wellGo.transform, false);
                var irt = (RectTransform)ig.transform;
                irt.anchorMin = new Vector2(0.32f, 0.20f); irt.anchorMax = new Vector2(0.68f, 0.90f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var ii = ig.GetComponent<Image>();
                ii.sprite = icon;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }
            else
            {
                var initials = Initials(model.Name);
                var t = CentredText(wellGo.transform, initials,
                    variant == StorePackCardVariant.Compact ? 56 : 84,
                    new Color(1f, 1f, 1f, 0.82f));
                if (t != null) t.characterSpacing = 6f;
            }

            // Bottom scrim so the name below never fights the well's brightest edge.
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(wellGo.transform, false);
            var srt = (RectTransform)scrim.transform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0.34f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var si = scrim.GetComponent<Image>();
            si.color = new Color(CardBottom.r, CardBottom.g, CardBottom.b, 0.72f);
            si.raycastTarget = false;
        }

        private static TextMeshProUGUI BuildPill(Transform card, string text, float cardH, float artH)
        {
            float wellTop = 1f - (artH / Mathf.Max(1f, cardH));
            float pillH = 44f;

            var go = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.42f, 1f); rt.anchorMax = new Vector2(0.96f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta = new Vector2(0f, pillH);

            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.92f);
            ElarionUiKit.ApplyRounded(img, pillH * 0.5f);
            // ⛔ raycastTarget OFF. A badge is INFORMATION, not a control: leaving it tappable would
            // put a second interactive rect on top of the card's own, which is precisely what
            // WO-1060 Assert B reports as an overlap — and it would also swallow the card's tap.
            img.raycastTarget = false;

            var t = CentredText(go.transform, text, FontBadge, new Color(0.08f, 0.06f, 0.03f, 1f));
            if (t != null)
            {
                t.fontStyle = FontStyles.Bold;
                t.characterSpacing = 4f;
                ElarionUiKit.FitSingleLine(t, ElarionUi.FontFloorMobile, FontBadge);
            }
            // Unused wellTop kept out of the layout deliberately: the pill anchors to the CARD top,
            // not the well, so a variant change to the art height cannot move the badge off it.
            _ = wellTop;
            return t;
        }

        private static Image AddGlow(Transform card, Color accent, float alpha)
        {
            var sprite = ElarionUiKit.RadialGlowSprite;
            if (sprite == null) return null;      // no sprite -> no bloom; NEVER a white quad

            var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-GlowBleedPx, -GlowBleedPx);
            rt.offsetMax = new Vector2(GlowBleedPx, GlowBleedPx);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = new Color(accent.r, accent.g, accent.b, alpha);
            img.raycastTarget = false;            // decoration never eats a tap
            go.transform.SetAsFirstSibling();
            return img;
        }

        private static Image AddRing(Transform card, Color color, float thicknessPx, float inset = 0f)
        {
            // The ring is the ROUNDED sprite with fillCenter OFF — no third sprite (§R3 asset bill).
            var go = new GameObject(inset > 0f ? "RingInset" : "Ring", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            var img = go.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img, CornerRadiusPx);
            if (img.sprite == null) { img.enabled = false; return img; }   // no white slab fallback
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.pixelsPerUnitMultiplier = thicknessPx > 0f ? 6f / thicknessPx : 1f;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image Plate(Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            ElarionUiKit.ApplyRounded(img, CornerRadiusPx);
            img.raycastTarget = false;
            return img;
        }

        // ── Text helpers ─────────────────────────────────────────────────────
        // All three assign a font BEFORE .text (EnsureFont) — a code-built TMP whose font is
        // unresolved at its first GenerateTextMesh NREs deep inside TMP, and force-built capture
        // panels hit that timing edge every run.

        private static TextMeshProUGUI TopAnchoredText(Transform parent, string text, int size,
            Color color, FontStyles style, TextAlignmentOptions align, float topOffsetPx, float heightPx)
        {
            var t = NewText(parent, text, size, color, style, align);
            if (t == null) return null;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.06f, 1f); rt.anchorMax = new Vector2(0.94f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topOffsetPx);
            rt.sizeDelta = new Vector2(0f, heightPx);
            return t;
        }

        private static TextMeshProUGUI BottomAnchoredText(Transform parent, string text, int size,
            Color color, FontStyles style, TextAlignmentOptions align, float bottomOffsetPx,
            float heightPx, float x0, float x1)
        {
            var t = NewText(parent, text, size, color, style, align);
            if (t == null) return null;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(x1, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottomOffsetPx);
            rt.sizeDelta = new Vector2(0f, heightPx);
            return t;
        }

        private static TextMeshProUGUI CentredText(Transform parent, string text, int size, Color color)
        {
            var t = NewText(parent, text, size, color, FontStyles.Bold, TextAlignmentOptions.Center);
            if (t == null) return null;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 8f); rt.offsetMax = new Vector2(-8f, -8f);
            return t;
        }

        private static TextMeshProUGUI NewText(Transform parent, string text, int size, Color color,
                                               FontStyles style, TextAlignmentOptions align)
        {
            if (parent == null) return null;
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);                 // font BEFORE .text (TMP first-generation NRE)
            t.text = Ascii(text);
            t.fontSize = Mathf.Max(size, (int)ElarionUi.FontFloorMobile);   // never below the mobile floor
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;                    // text is never a tap target inside the card
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // ── ASCII + colour utilities ─────────────────────────────────────────

        /// <summary>
        /// Strips every non-ASCII code point. TMP's shipped font renders anything outside ASCII as
        /// a TOFU BOX, and the one screen that must never look broken is the one that takes money —
        /// so the guard is here, at the single place card text is set, rather than trusted to every
        /// caller and every future packs.json edit.
        /// </summary>
        private static string Ascii(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            bool clean = true;
            for (int i = 0; i < s.Length; i++) { if (s[i] > 126 || s[i] < 9) { clean = false; break; } }
            if (clean) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= 32 && c <= 126) sb.Append(c);
                else if (c == '\n' || c == '\t') sb.Append(c);
                // NUMERIC code points, never literal glyphs: this source file stays ASCII-clean
                // itself, so no editor / mount round-trip (CLAUDE.md 0) can re-encode the very
                // characters this method exists to replace.
                else if (c == 0x2019 || c == 0x2018) sb.Append('\'');        // curly quotes
                else if (c == 0x201C || c == 0x201D) sb.Append('"');
                else if (c == 0x2013 || c == 0x2014) sb.Append('-');         // en / em dash
                else if (c == 0x2026) sb.Append("...");                      // ellipsis
                else if (c == 0x00A0) sb.Append(' ');                        // nbsp
                // anything else is DROPPED: a missing character reads as a typo, a tofu box reads
                // as a broken build, and on this screen that difference is money.
            }
            return sb.ToString();
        }

        /// <summary>Two-letter ASCII initials for the placeholder art well ("Hearth Spark" -> "HS").</summary>
        private static string Initials(string name)
        {
            string a = Ascii(name);
            if (string.IsNullOrEmpty(a)) return "??";
            var sb = new StringBuilder(2);
            bool atWordStart = true;
            for (int i = 0; i < a.Length && sb.Length < 2; i++)
            {
                char c = a[i];
                if (c == ' ' || c == '\'' || c == '-') { atWordStart = true; continue; }
                if (atWordStart && char.IsLetterOrDigit(c)) { sb.Append(char.ToUpperInvariant(c)); atWordStart = false; }
            }
            if (sb.Length == 0) sb.Append('?');
            return sb.ToString();
        }

        private static Color Darken(Color c, float toward)
        {
            return new Color(Mathf.Lerp(c.r, 0.06f, 1f - toward),
                             Mathf.Lerp(c.g, 0.05f, 1f - toward),
                             Mathf.Lerp(c.b, 0.10f, 1f - toward), 1f);
        }

        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
