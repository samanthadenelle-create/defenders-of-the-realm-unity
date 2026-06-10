// =============================================================================
// ElarionUiKit — the SHARED code-built uGUI coherence kit for Echoes of Elarion.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// ONE visual language, built from reusable pieces. Every HUD / modal / screen in
// the game can be assembled by calling these static builders instead of each
// surface re-implementing its own AddImage/AddLabel/AddButton/RoundedSprite recipe
// (which is how ArenaPanel, HeroInventoryController, HeroEquipHud and the HUD each
// grew their own near-identical copy). This kit CONSOLIDATES the BEST of those —
// the rich depth/frame/niche + rarity-escalating frames from the inventory, the
// proven Canvas/Scaler/Raycaster modal boilerplate + sleek dark-glass panels +
// WebGL-safe rounded-sprite fallback from ArenaPanel / HudTheme — into one place.
//
// WHY HERE: DeNelle.Core.UI is the ONE assembly that DeNelle.HUD (uGUI) AND
// DeNelle.Village (panels) both reference, so a shared kit lives here WITHOUT a
// forbidden HUD<->Village edge (CLAUDE.md §5). Colours / fonts / radii all SOURCE
// from the canonical ElarionUi palette so every surface reads as ONE designed game.
//
// CODE-BUILT uGUI ONLY (Canvas/Image/Button/ScrollRect/TextMeshProUGUI) — the
// proven-reliable path; UXML/UI-Toolkit HUDs come up empty in player builds
// (PIPELINE_STATE §8). The procedural rounded sprite is built lazily once and is
// failure-safe: if the Texture2D build throws under WebGL it falls back to null and
// Images render as flat tinted quads — a surface can NEVER blank (WO-334 guard).
//
// ADDITIVE: this file only ADDS the kit. No existing UI is modified by it; the
// older surfaces keep their private helpers and compile unchanged. A later pilot
// converts the main HUD by calling these methods (`// TODO adopt kit`).
//
// ASCII-only structural strings; callers pass their own display text.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Shared, static, ElarionUi-sourced uGUI builders so every surface is built
    /// from the same coherent pieces (modal canvas, scrim, depth panel, button,
    /// header, rarity slot/card, label). Each method returns the created object so
    /// callers can parent children / restyle / wire events. Stateless + WebGL-safe.
    /// </summary>
    public static class ElarionUiKit
    {
        // ── Sleek surface tints (the dark-glass language, consolidated) ───────
        // These were duplicated verbatim across ArenaPanel / HeroInventory* / HUD.
        // Centralised here so the whole game's glass depth reads identically.
        /// <summary>Primary panel fill — dark translucent glass (play area shows through).</summary>
        public static readonly Color Glass      = new Color(0.06f, 0.07f, 0.09f, 0.66f);
        /// <summary>Deeper glass for heavier panels / modal backboards.</summary>
        public static readonly Color GlassDeep  = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        /// <summary>Recessed near-black well / track behind a value or bar.</summary>
        public static readonly Color Track      = new Color(0.0f,  0.0f,  0.0f,  0.45f);
        /// <summary>Cell rest fill — dark glass a touch lighter than the tray.</summary>
        public static readonly Color Cell       = new Color(0.10f, 0.11f, 0.14f, 0.84f);
        /// <summary>Selected cell fill — brighter than the rest cell.</summary>
        public static readonly Color CellSelected = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        /// <summary>Warm stone backboard behind a hero / display niche.</summary>
        public static readonly Color StoneNiche = new Color(0.075f, 0.060f, 0.048f, 0.96f);

        /// <summary>Thin gold accent line (a hint of runic gold, not a heavy frame).</summary>
        public static readonly Color Accent     = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
        /// <summary>Even fainter gold for inner rims / soft underlines.</summary>
        public static readonly Color AccentSoft = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f);

        // ── Canonical button kinds (consolidates StyleButtonColors variants) ──
        /// <summary>Button intent. Gold = primary CTA (dark-ink text); Confirm = green;
        /// Danger = red; Quiet = neutral glass.</summary>
        public enum ButtonKind { Gold, Confirm, Danger, Quiet }

        // =====================================================================
        // MODAL CANVAS — the boilerplate every full-screen surface repeats.
        // =====================================================================

        /// <summary>
        /// Create a standalone ScreenSpaceOverlay Canvas (with CanvasScaler 1080x1920
        /// ScaleWithScreenSize, match 0.5) + GraphicRaycaster. Returns the root
        /// GameObject; parent your scrim / panel under it. Mobile-first reference res.
        /// </summary>
        public static GameObject BuildModalCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "ModalCanvas" : name);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        /// <summary>
        /// Full-screen dark backdrop behind a modal (alpha ~0.85), raycast-blocking so
        /// clicks don't fall through to the scene. If <paramref name="onTapClose"/> is
        /// supplied the scrim becomes a transition-less Button that fires it (tap-outside
        /// to dismiss). Returns the scrim GameObject.
        /// </summary>
        public static GameObject Scrim(Transform parent, Action onTapClose = null)
        {
            var go = AddImage(parent, "Scrim", Vector2.zero, Vector2.one,
                              new Color(0.02f, 0.015f, 0.04f, 0.85f), rounded: false);
            if (onTapClose != null)
            {
                var btn = go.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onTapClose());
            }
            return go;
        }

        // =====================================================================
        // PANEL — the framed depth panel (glass fill + soft gold rim).
        // =====================================================================

        /// <summary>
        /// The canonical framed panel: dark glass rounded rect with a soft gold
        /// underline rim and (optionally) an inner hairline rim for crisp depth.
        /// Anchored by fraction-of-parent (anchorMin/anchorMax) so it reflows.
        /// </summary>
        public static GameObject Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                       bool deep = false, bool innerRim = true)
        {
            var p = AddImage(parent, "Panel", anchorMin, anchorMax, deep ? GlassDeep : Glass);
            AddRimUnderline(p);
            if (innerRim) AddInnerRim(p, AccentSoft);
            return p;
        }

        /// <summary>A recessed near-black well (scroll tray / value plate), lightly framed.</summary>
        public static GameObject Well(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var w = AddImage(parent, "Well", anchorMin, anchorMax, Track);
            AddInnerRim(w, new Color(0f, 0f, 0f, 0.4f));
            return w;
        }

        /// <summary>A warm stone display niche (hero portrait / showcase alcove) with a gold inner rim.</summary>
        public static GameObject Niche(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var n = AddImage(parent, "Niche", anchorMin, anchorMax, StoneNiche);
            AddInnerRim(n, AccentSoft);
            return n;
        }

        // =====================================================================
        // HEADER — section header (crest glyph + gilt underline rule).
        // =====================================================================

        /// <summary>
        /// A section header band: gilt crest glyph + spaced title at FontTitle, with a
        /// soft drop-shadow for depth and a thin gilt rule underneath. Spans the given
        /// x-band [x0,x1] across the top of <paramref name="parent"/>. Returns the
        /// header's title label so callers can retext / recolour it.
        /// </summary>
        public static TextMeshProUGUI Header(Transform parent, string text,
                                             float x0 = 0.06f, float x1 = 0.94f,
                                             float y0 = 0.92f, float y1 = 0.98f)
        {
            // Soft shadow under the title for legibility on busy scenes.
            var shadow = Label(parent, ElarionUi.CrestGlyph + "  " + text, y0, y1,
                               new Color(0f, 0f, 0f, 0.55f), ElarionUi.FontTitle,
                               TextAlignmentOptions.Center, x0, x1, spacing: 6f, bold: true);
            shadow.GetComponent<RectTransform>().anchoredPosition += new Vector2(1.5f, -1.5f);

            var title = Label(parent, ElarionUi.CrestGlyph + "  " + text, y0, y1,
                              ElarionUi.Gilt, ElarionUi.FontTitle,
                              TextAlignmentOptions.Center, x0, x1, spacing: 6f, bold: true);

            // Gilt rule hugging the header's bottom edge.
            Rule(parent, y0 - 0.008f, x0, x1);
            return title;
        }

        /// <summary>A thin gilt hairline rule at fractional height <paramref name="y"/> across [x0,x1].</summary>
        public static GameObject Rule(Transform parent, float y, float x0, float x1)
        {
            var go = new GameObject("Rule", typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y); r.anchorMax = new Vector2(x1, y);
            r.offsetMin = new Vector2(0f, -1f); r.offsetMax = new Vector2(0f, 1f);
            var img = go.GetComponent<Image>();
            img.color = Accent;
            img.raycastTarget = false;
            return go;
        }

        // =====================================================================
        // BUTTON — canonical button with consistent state feedback.
        // =====================================================================

        /// <summary>
        /// The canonical button: a rounded-glass rect with a centred bold label, the
        /// kind-appropriate fill (Gold CTA / green Confirm / red Danger / neutral
        /// Quiet) and the shared brightness press/hover/disabled feedback. Anchored by
        /// fraction-of-parent. Returns the Button so callers can wire interactable /
        /// extra listeners. Gold uses dark-ink text; the rest use cream parchment.
        /// </summary>
        public static Button Button(Transform parent, string label, ButtonKind kind,
                                    Vector2 anchorMin, Vector2 anchorMax, Action onClick = null)
        {
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = FillFor(kind);
            ApplyRounded(img);

            var btn = go.GetComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Color textColor = kind == ButtonKind.Gold ? ElarionUi.Ink : ElarionUi.Parchment;
            var tt = Label(go.transform, label, 0f, 1f, textColor, ElarionUi.FontBody,
                           TextAlignmentOptions.Center, 0f, 1f, spacing: 1f, bold: true);
            tt.raycastTarget = false;
            return btn;
        }

        /// <summary>Rest fill colour for a button kind (sourced from ElarionUi state colours).</summary>
        public static Color FillFor(ButtonKind kind)
        {
            switch (kind)
            {
                case ButtonKind.Gold:    return ElarionUi.GoldButton;
                case ButtonKind.Confirm: return new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.92f);
                case ButtonKind.Danger:  return new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.55f);
                default:                 return Glass;   // Quiet
            }
        }

        /// <summary>Shared subtle brightness feedback (no colour shift) for any uGUI Button.</summary>
        public static void StyleButtonColors(Button button)
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

        // =====================================================================
        // SLOT — universal rarity-framed item / gear slot.
        // =====================================================================

        /// <summary>
        /// A universal rarity-framed slot: the frame strength ESCALATES by rarity
        /// (common = quiet hairline ... legendary = strong gilt halo), a recessed cell
        /// sits inset inside it, with a rarity gem pip top-left and toggleable
        /// equipped-check + lock overlay children (start hidden; flip via
        /// <see cref="SetSlotEquipped"/> / <see cref="SetSlotLocked"/>). Returns the
        /// slot's CELL GameObject so callers add their own icon / count / button.
        /// </summary>
        public static GameObject Slot(Transform parent, int rarityIndex,
                                      Vector2 anchorMin, Vector2 anchorMax, bool dim = false)
        {
            Color rc = RarityColor(rarityIndex);
            float strength = RarityFrameStrength(rarityIndex);

            var frame = AddImage(parent, "SlotFrame", anchorMin, anchorMax,
                                 new Color(rc.r, rc.g, rc.b, dim ? strength * 0.4f : strength));
            frame.GetComponent<Image>().raycastTarget = false;

            var cell = AddImage(frame.transform, "Cell", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f),
                                dim ? new Color(Cell.r, Cell.g, Cell.b, 0.55f) : Cell);

            // Rarity gem pip — small tinted square top-left, reads the tier at a glance.
            var gem = AddImage(cell.transform, "Gem", new Vector2(0.06f, 0.78f), new Vector2(0.22f, 0.94f),
                               new Color(rc.r, rc.g, rc.b, 0.95f));
            gem.GetComponent<Image>().raycastTarget = false;

            // Equipped check chip (hidden by default).
            var check = AddImage(cell.transform, "EquippedCheck", new Vector2(0.62f, 0.78f), new Vector2(0.94f, 0.94f),
                                 new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.95f));
            check.GetComponent<Image>().raycastTarget = false;
            var checkLbl = Label(check.transform, "v", 0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel,
                                 TextAlignmentOptions.Center, 0f, 1f, bold: true);
            checkLbl.raycastTarget = false;
            check.SetActive(false);

            // Lock overlay (veil + lock chip), hidden by default.
            var lockGo = AddImage(cell.transform, "LockOverlay", Vector2.zero, Vector2.one,
                                  new Color(0f, 0f, 0f, 0.55f), rounded: false);
            lockGo.GetComponent<Image>().raycastTarget = false;
            var lockChip = Label(lockGo.transform, "\U0001F512", 0.30f, 0.70f, ElarionUi.Gilt, ElarionUi.FontHead,
                                 TextAlignmentOptions.Center, 0f, 1f, bold: true);
            lockChip.raycastTarget = false;
            lockGo.SetActive(false);

            return cell;
        }

        /// <summary>Toggle a Slot's equipped-check overlay (built by <see cref="Slot"/>).</summary>
        public static void SetSlotEquipped(GameObject slotCell, bool equipped)
        {
            if (slotCell == null) return;
            var t = slotCell.transform.Find("EquippedCheck");
            if (t != null) t.gameObject.SetActive(equipped);
        }

        /// <summary>Toggle a Slot's lock overlay (built by <see cref="Slot"/>).</summary>
        public static void SetSlotLocked(GameObject slotCell, bool locked)
        {
            if (slotCell == null) return;
            var t = slotCell.transform.Find("LockOverlay");
            if (t != null) t.gameObject.SetActive(locked);
        }

        // =====================================================================
        // CARD — polished item card (icon well + rarity frame + name).
        // =====================================================================

        /// <summary>
        /// A polished item card: a rarity-framed glass tile with a recessed round icon
        /// well (the glyph/icon sits in it), a rarity gem pip, and a rarity-coloured
        /// name along the bottom band. Returns the card's CELL GameObject (which has a
        /// Button) so callers wire the tap + drop in the icon glyph. The icon well is
        /// found at child "IconWell" for callers that want to swap a sprite in.
        /// </summary>
        public static GameObject Card(Transform parent, int rarityIndex, string name, string icon,
                                      Vector2 anchorMin, Vector2 anchorMax, Action onTap = null)
        {
            Color rc = RarityColor(rarityIndex);
            float strength = RarityFrameStrength(rarityIndex);

            var frame = AddImage(parent, "CardFrame", anchorMin, anchorMax,
                                 new Color(rc.r, rc.g, rc.b, strength));
            frame.GetComponent<Image>().raycastTarget = false;

            var cell = new GameObject("Card", typeof(Image), typeof(Button));
            cell.transform.SetParent(frame.transform, false);
            var crt = cell.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.04f, 0.04f); crt.anchorMax = new Vector2(0.96f, 0.96f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var img = cell.GetComponent<Image>();
            img.color = Cell;
            ApplyRounded(img);
            var btn = cell.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onTap != null) btn.onClick.AddListener(() => onTap());

            // Recessed icon well (decorative — non-raycast so the whole card is one tap target).
            var well = AddImage(cell.transform, "IconWell", new Vector2(0.26f, 0.40f), new Vector2(0.74f, 0.92f),
                                new Color(0f, 0f, 0f, 0.30f));
            well.GetComponent<Image>().raycastTarget = false;
            var ic = Label(well.transform, string.IsNullOrEmpty(icon) ? "?" : icon, 0f, 1f,
                           ElarionUi.Parchment, ElarionUi.FontTitle + 2, TextAlignmentOptions.Center, 0.05f, 0.95f);
            ic.raycastTarget = false;

            // Rarity gem pip.
            var gem = AddImage(cell.transform, "Gem", new Vector2(0.06f, 0.80f), new Vector2(0.20f, 0.94f),
                               new Color(rc.r, rc.g, rc.b, 0.95f));
            gem.GetComponent<Image>().raycastTarget = false;

            // Name in the rarity colour along the bottom.
            var nm = Label(cell.transform, name ?? "", 0.06f, 0.36f, rc, ElarionUi.FontMicro,
                           TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            nm.raycastTarget = false;
            return cell;
        }

        // =====================================================================
        // LABEL — shared text builder (fraction-anchored).
        // =====================================================================

        /// <summary>
        /// A TextMeshProUGUI label anchored by fraction-of-parent (x0,y0)-(x1,y1).
        /// The shared text primitive every surface used a private copy of. Returns the
        /// label so callers can mutate .text later. Raycast-off by default (decorative).
        /// </summary>
        public static TextMeshProUGUI Label(Transform parent, string text, float y0, float y1,
            Color color, int size, TextAlignmentOptions align,
            float x0 = 0.03f, float x1 = 0.97f, float spacing = 0f, bool bold = false)
        {
            var go = new GameObject("Label", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        // =====================================================================
        // RARITY — ONE canonical map for the whole game.
        // =====================================================================
        // Index ladder: 0 common, 1 uncommon, 2 rare, 3 epic, 4 legendary. The
        // string overloads accept the catalog's rarity words. Centralises what the
        // inventory / HUD each hardcoded so every surface tiers items identically.

        /// <summary>Named rarity tiers (index == the int the kit's overloads accept).</summary>
        public enum Rarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

        /// <summary>The canonical rarity colour for an index (0..4, clamped).</summary>
        public static Color RarityColor(int rarityIndex)
        {
            switch (Mathf.Clamp(rarityIndex, 0, 4))
            {
                case 1:  return new Color(0.46f, 0.74f, 0.42f, 1f);   // uncommon green
                case 2:  return new Color(0.32f, 0.58f, 0.92f, 1f);   // rare blue
                case 3:  return new Color(0.66f, 0.42f, 0.86f, 1f);   // epic purple
                case 4:  return new Color(0.92f, 0.62f, 0.24f, 1f);   // legendary orange
                default: return new Color(0.80f, 0.80f, 0.78f, 1f);   // common grey
            }
        }

        /// <summary>The canonical rarity colour for a catalog rarity word (case-insensitive).</summary>
        public static Color RarityColor(string rarity) => RarityColor(RarityIndex(rarity));

        /// <summary>A small font-safe ASCII glyph per rarity tier (. - = + *).</summary>
        public static string RarityGlyph(int rarityIndex)
        {
            switch (Mathf.Clamp(rarityIndex, 0, 4))
            {
                case 4:  return "*";   // legendary
                case 3:  return "+";   // epic
                case 2:  return "=";   // rare
                case 1:  return "-";   // uncommon
                default: return ".";   // common
            }
        }

        /// <summary>Glyph overload for a catalog rarity word.</summary>
        public static string RarityGlyph(string rarity) => RarityGlyph(RarityIndex(rarity));

        /// <summary>
        /// How loud the rarity frame glows (0..1): common a quiet hairline, legendary a
        /// strong gilt halo — so tiers feel visibly escalating. Used by Slot / Card.
        /// </summary>
        public static float RarityFrameStrength(int rarityIndex)
        {
            switch (Mathf.Clamp(rarityIndex, 0, 4))
            {
                case 4:  return 0.95f;
                case 3:  return 0.85f;
                case 2:  return 0.72f;
                case 1:  return 0.58f;
                default: return 0.40f;
            }
        }

        /// <summary>Frame-strength overload for a catalog rarity word.</summary>
        public static float RarityFrameStrength(string rarity) => RarityFrameStrength(RarityIndex(rarity));

        /// <summary>Map a catalog rarity word to the canonical index (0..4); unknown = common.</summary>
        public static int RarityIndex(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "legendary": return 4;
                case "epic":      return 3;
                case "rare":      return 2;
                case "uncommon":  return 1;
                default:          return 0;
            }
        }

        // =====================================================================
        // PRIMITIVES — shared image / rim builders + the rounded sprite.
        // =====================================================================

        /// <summary>
        /// A fraction-anchored Image. rounded=true applies the 9-sliced rounded sprite
        /// (flat tinted quad if the sprite build failed under WebGL). The base building
        /// block for panels, wells, chips, frames.
        /// </summary>
        public static GameObject AddImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Color color, bool rounded = true)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            if (rounded) ApplyRounded(img);
            return go;
        }

        /// <summary>Apply the shared rounded 9-slice sprite to an Image (no-op if build failed).</summary>
        public static void ApplyRounded(Image img)
        {
            if (img == null) return;
            var sprite = RoundedSprite;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        }

        /// <summary>A single faint gold rule hugging a panel's bottom edge (sleek accent).</summary>
        public static void AddRimUnderline(GameObject panel)
        {
            if (panel == null) return;
            var go = new GameObject("Accent", typeof(Image));
            go.transform.SetParent(panel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0f);
            rt.anchorMax = new Vector2(0.94f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 1.5f);
            rt.anchoredPosition = new Vector2(0f, 1.5f);
            var img = go.GetComponent<Image>();
            img.color = AccentSoft;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
        }

        /// <summary>A 1px inner rim hugging an element's edges — crisp framed depth.</summary>
        public static void AddInnerRim(GameObject host, Color color)
        {
            if (host == null) return;
            var go = new GameObject("Rim", typeof(Image));
            go.transform.SetParent(host.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(1f, 1f); rt.offsetMax = new Vector2(-1f, -1f);
            var img = go.GetComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
            ApplyRounded(img);
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();   // behind content added after it
        }

        // ── Procedural rounded sprite (lazily built once; WebGL failure-safe) ──
        // Mirrors HudTheme.RoundedFrame so the whole game's corners match exactly.
        private static Sprite _rounded;
        private static bool _roundedTried;
        private static Sprite RoundedSprite
        {
            get
            {
                if (!_roundedTried)
                {
                    _roundedTried = true;
                    try { _rounded = BuildRoundedSprite(); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[ElarionUiKit] rounded sprite build failed (flat quad): " + e.Message);
                        _rounded = null;
                    }
                }
                return _rounded;
            }
        }

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
    }
}
