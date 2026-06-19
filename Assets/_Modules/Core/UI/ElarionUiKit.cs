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
using DeNelle.Core.Diagnostics;

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
        // WOOD CANON (single-source unification): the kit's surfaces now derive their HUE from the
        // ONE ElarionUi source (warm stone/wood) at the kit's own translucency alphas — so the HUD
        // keeps its see-through depth but reads dark-WOOD, not cool-glass. Tune the wood in ONE
        // place (ElarionUi.PanelStone / PanelStoneDark) and it propagates through every kit screen.
        /// <summary>Primary panel fill — dark translucent WOOD (play area shows through).</summary>
        public static readonly Color Glass      = new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.66f);
        /// <summary>Deeper wood for heavier panels / modal backboards.</summary>
        public static readonly Color GlassDeep  = new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.86f);
        /// <summary>Recessed near-black well / track behind a value or bar.</summary>
        public static readonly Color Track      = new Color(0.0f,  0.0f,  0.0f,  0.45f);
        /// <summary>Cell rest fill — warm wood a touch lighter than the tray.</summary>
        public static readonly Color Cell       = new Color(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.84f);
        /// <summary>Selected cell fill — brighter warm wood than the rest cell.</summary>
        public static readonly Color CellSelected = new Color(0.26f, 0.20f, 0.13f, 0.95f);
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
            // BlinkChrome: keep the GameObject (callers parent content onto it) but make the stone
            // backing transparent + skip the rim, so the Blink panel shows through the alcove.
            bool chrome = !DeNelle.Core.FeatureFlags.BlinkChrome;
            var n = AddImage(parent, "Niche", anchorMin, anchorMax, chrome ? StoneNiche : new Color(0f, 0f, 0f, 0f));
            if (chrome) AddInnerRim(n, AccentSoft);
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
            // BlinkChrome: skip the drop-shadow + the gilt underline rule (chrome); keep the TITLE (content).
            bool chrome = !DeNelle.Core.FeatureFlags.BlinkChrome;
            if (chrome)
            {
                // Soft shadow under the title for legibility on busy scenes.
                var shadow = Label(parent, ElarionUi.CrestGlyph + "  " + text, y0, y1,
                                   new Color(0f, 0f, 0f, 0.55f), ElarionUi.FontTitle,
                                   TextAlignmentOptions.Center, x0, x1, spacing: 6f, bold: true);
                shadow.GetComponent<RectTransform>().anchoredPosition += new Vector2(1.5f, -1.5f);
            }

            var title = Label(parent, ElarionUi.CrestGlyph + "  " + text, y0, y1,
                              ElarionUi.Gilt, ElarionUi.FontTitle,
                              TextAlignmentOptions.Center, x0, x1, spacing: 6f, bold: true);

            // Gilt rule hugging the header's bottom edge.
            if (chrome) Rule(parent, y0 - 0.008f, x0, x1);
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
            img.color = DeNelle.Core.FeatureFlags.BlinkChrome ? new Color(0f, 0f, 0f, 0f) : Accent;   // chrome: invisible
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
        // TECH HUD ELEMENTS — direct use of the "Tech hud elements" sprite pack
        // for ornate frames, play-button CTAs, loading bars (cooldowns/stats),
        // profile tabs (sockets), RPG icons, etc. Used for the full combat/vendor/
        // inventory restyle (WO-437/415/405).
        // Sprites are 9-slice friendly in many cases; we treat as sliced + tint.
        // =====================================================================

        /// <summary>
        /// Big thumb-friendly CTA button skinned from Tech pack Play Buttons / Menu Bars.
        /// Primary = gilt ornate frame (Equip, Buy, etc.). Falls back to kit button.
        /// </summary>
        public static Button TechPrimaryButton(Transform parent, string label,
                                               Vector2 anchorMin, Vector2 anchorMax,
                                               Action onClick = null)
        {
            // IMPORTANT: the pack's "Play buttons" sprite has the word PLAY baked INTO the art,
            // so a button using it reads "PLAY" no matter what label is passed (BUY/SELL/EQUIP all
            // showed "PLAY"). It is a literal Play/Start button, not a generic CTA frame. Use the
            // clean procedural gold button so the real label renders. (Swap in a confirmed
            // text-FREE ornate frame later if one exists in the pack.)
            Sprite frame = null;

            var go = new GameObject("TechBtn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = ElarionUi.GoldButton;
                ApplyRounded(img);
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // Guarantee large mobile target.
            if (rt.rect.height < 56f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 56f);

            Color ink = ElarionUi.Ink;
            var lbl = Label(go.transform, label, 0.08f, 0.92f, ink, ElarionUi.FontBody,
                            TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Ornate gear/weapon/armor socket frame from Tech hud elements pack.
        /// Uses Profile tabs (clean modern frames) and Healing Tabs (more ornate RPG style) for variety.
        /// Weapons get a slightly more "active" frame (e.g. Healing Tabs H1-H3 or Profiletab 2), Armor gets solid profile tabs.
        /// Tints to rarity. Large touch target. Returns the host so caller can parent an icon/child on top.
        /// Falls back to procedural rim if pack sprites not present (fresh clone note: pack may need re-import).
        /// </summary>
        public static GameObject TechGearSocket(Transform parent, string name,
                                                Vector2 anchorMin, Vector2 anchorMax,
                                                Color tint, bool isWeapon = false)
        {
            Sprite frame = null;
            try
            {
                if (isWeapon)
                {
                    // Heavy Tech pack: Healing Tabs (H1-H15.png) for dynamic weapon frames (ornate RPG tabs).
                    // Fallback to Play buttons (button N.png) for action-oriented weapon sockets.
                    frame = Resources.Load<Sprite>("Tech hud elements/Sprites/Healing Tabs/H1");
                    if (frame == null) frame = Resources.Load<Sprite>("Tech hud elements/Sprites/Play buttons/button 3");
                    if (frame == null) frame = Resources.Load<Sprite>("Tech hud elements/Sprites/GreenUielements/Buttons/Button 1");
                }
                else
                {
                    // Heavy Tech pack for armor: Profile tabs P1-P6 (use fill.png or bg.png as sliced frame for solid protective sockets).
                    // P1/fill.png etc provide layered bg/fill for depth; pick fill as main frame.
                    frame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/fill.png");
                    if (frame == null) frame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/bg.png");
                    if (frame == null) frame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P3/fill.png");
                    if (frame == null) frame = Resources.Load<Sprite>("Tech hud elements/Sprites/GreenUielements/Shield/Shield 1");
                }
            }
            catch (Exception e) { FlowTrace.Warn("ElarionUiKit", $"TechGearSocket frame Resources.Load threw (pack may be partial on fresh clone — degrading to committed frame): {e.GetType().Name}: {e.Message}"); }

            // Clean-build fallback: the "Tech hud elements" pack is gitignored — only the committed
            // RpgUi slice ships. Degrade to the committed grid-plate frame so the socket keeps a
            // themed frame (instead of dropping to the bare procedural rim) on a fresh checkout.
            if (frame == null) frame = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelGrid);

            var host = AddImage(parent, name, anchorMin, anchorMax, new Color(tint.r, tint.g, tint.b, 0.18f));
            var img = host.GetComponent<Image>();
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Sliced;
                img.color = new Color(tint.r, tint.g, tint.b, 0.95f);
            }
            else
            {
                AddInnerRim(host, new Color(tint.r, tint.g, tint.b, 0.6f));
            }
            return host;
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
            EnsureFont(t);          // assign a font BEFORE .text so first generation can't NRE
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        // Font-safe construction. A code-built TextMeshProUGUI whose font/material is still
        // unresolved at its first GenerateTextMesh() throws an NRE deep inside TMP — the
        // 2026-06-19 chaos-fleet caught exactly this (TextMeshProUGUI.GenerateTextMesh, 5/8
        // runs) on force-built panels (Canvas.ForceUpdateCanvases makes TMP generate the same
        // frame the row is created, before TMP_Settings.defaultFontAsset has been leaned on).
        // We never assigned a font here, so EVERY label was one timing edge from this NRE.
        // Assign the default font explicitly before any .text set; fall back to the shipped
        // LiberationSans SDF under Resources; Warn (never NRE / never silently blank) if both miss.
        private static TMPro.TMP_FontAsset _fontCache;
        /// <summary>Assign a resolved TMP font to <paramref name="t"/> if it has none, so its first
        /// GenerateTextMesh() cannot NRE. Call this BEFORE setting .text on any code-built TMP that
        /// is constructed outside <see cref="Label"/> (e.g. direct <c>new GameObject(typeof(TextMeshProUGUI))</c>).</summary>
        public static void EnsureFont(TextMeshProUGUI t)
        {
            if (t == null || t.font != null) return;
            if (_fontCache == null)
            {
                _fontCache = TMPro.TMP_Settings.defaultFontAsset
                          ?? UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
            if (_fontCache != null) t.font = _fontCache;
            else DeNelle.Core.Diagnostics.FlowTrace.Warn("UI",
                "ElarionUiKit.Label: no TMP font (TMP_Settings.defaultFontAsset null AND LiberationSans SDF " +
                "absent from Resources) — assigning none to avoid an NRE; text may not render until a font ships.");
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
        // BAR — the town-HUD vitals/progress bar (Well track + filled rounded
        // fill + optional inline value), ported from VillageHudController so the
        // combat HUD / store / inventory render the IDENTICAL ornate bar.
        // =====================================================================

        /// <summary>Which vital/progress a bar shows — drives the fill colour + pack frame/fill ids.</summary>
        public enum BarKind { Hp, Mp, Xp, Atb, Castle }

        /// <summary>Handle returned by <see cref="Bar"/>: the track rect, the
        /// fillAmount-driven fill Image, and the optional inline value label.</summary>
        public sealed class BarHandle
        {
            /// <summary>The recessed Well track the fill rides in.</summary>
            public RectTransform track;
            /// <summary>The Image.Type.Filled fill — drive its fillAmount [0..1].</summary>
            public Image fill;
            /// <summary>Optional inline value label centred over the fill (null if not requested).</summary>
            public TMP_Text valueLabel;
        }

        /// <summary>
        /// A complete vitals/progress bar: a recessed <see cref="Well"/> track with a
        /// rounded Image.Type.Filled fill (horizontal, origin-left, fillAmount=1) in the
        /// kind's colour, dressed sprite-FIRST with the RPG pack's ornate frame + colored
        /// fill (procedural rounded fallback when the pack is absent), and — when
        /// <paramref name="withValue"/> — a centred cream value label with a dark halo.
        /// Anchored by fraction-of-parent. Returns a <see cref="BarHandle"/> so callers
        /// drive fill.fillAmount + valueLabel.text without re-implementing the recipe.
        /// </summary>
        public static BarHandle Bar(Transform parent, BarKind kind,
                                    Vector2 anchorMin, Vector2 anchorMax, bool withValue = false)
        {
            var trackGo = Well(parent, anchorMin, anchorMax);
            var track = trackGo.GetComponent<RectTransform>();

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fr = fillGo.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(1.5f, 1.5f); fr.offsetMax = new Vector2(-1.5f, -1.5f);
            var fill = fillGo.GetComponent<Image>();
            fill.color = BarFillColor(kind);
            ApplyRounded(fill);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            DressBar(track, fill, kind);

            TMP_Text valueLabel = null;
            if (withValue)
            {
                valueLabel = Label(trackGo.transform, "", 0f, 1f, ElarionUi.Parchment,
                                   ElarionUi.FontLabel, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                valueLabel.outlineColor = new Color32(10, 10, 14, 200);
                valueLabel.outlineWidth = 0.14f;
                valueLabel.raycastTarget = false;
            }

            return new BarHandle { track = track, fill = fill, valueLabel = valueLabel };
        }

        /// <summary>The canonical fill colour for a bar kind (sourced from ElarionUi).</summary>
        public static Color BarFillColor(BarKind kind)
        {
            switch (kind)
            {
                case BarKind.Hp:     return ElarionUi.HpRed;
                case BarKind.Mp:     return ElarionUi.ManaBlue;
                case BarKind.Xp:     return new Color(1f, 0.85f, 0.15f, 1f); // yellow XP strip
                case BarKind.Atb:    return ElarionUi.Aether;
                case BarKind.Castle: return ElarionUi.Gold;
                default:             return ElarionUi.Gold;
            }
        }

        /// <summary>
        /// Dress an existing bar (track + its Image.Type.Filled fill) with the RPG pack's
        /// ornate frame + colored fill, sprite-FIRST — the port of
        /// VillageHudController.TryDressBar. The fill keeps its fillAmount binding; we
        /// swap its sprite to the pack's colored fill and drop the gilded frame over the
        /// track as a NON-RAYCAST overlay rendered last (the frame art is hollow so the
        /// fill shows through). No-op (procedural look preserved) when the pack is absent.
        /// Returns true when the pack art dressed the bar.
        /// </summary>
        public static bool DressBar(RectTransform track, Image fill, BarKind kind)
        {
            if (track == null) return false;

            string frameName, fillName;
            bool tintFill;
            Color fillTint = BarFillColor(kind);
            BarPackIds(kind, out frameName, out fillName, out tintFill);

            var frameSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, frameName);
            var fillSprite  = string.IsNullOrEmpty(fillName)
                ? null : RpgUiCatalog.Get(RpgUiCatalog.RoleBars, fillName);
            if (frameSprite == null && fillSprite == null) return false;

            // Swap the fill sprite (keeps Image.Type.Filled + fillAmount binding).
            if (fill != null && fillSprite != null)
            {
                fill.sprite = fillSprite;
                fill.color = tintFill ? fillTint : Color.white; // pack art's own colours unless tinted
            }

            // Drop the gilded frame over the track as a decorative, non-raycast overlay
            // rendered LAST so it sits above the fill; it stretches to the bar rect.
            if (frameSprite != null)
            {
                var fr = AddImage(track, "PackFrame", Vector2.zero, Vector2.one, Color.white, rounded: false)
                            .GetComponent<RectTransform>();
                fr.offsetMin = new Vector2(-10f, -8f);
                fr.offsetMax = new Vector2(10f, 8f); // ornate ends slightly overhang the track
                var fimg = fr.GetComponent<Image>();
                fimg.sprite = frameSprite;
                fimg.type = Image.Type.Simple;
                fimg.color = Color.white;
                fimg.raycastTarget = false;
                fr.SetAsLastSibling();
            }
            return true;
        }

        /// <summary>Map a bar kind to its RpgUiCatalog "bars" frame/fill ids + whether to tint the fill.</summary>
        private static void BarPackIds(BarKind kind, out string frameName, out string fillName, out bool tintFill)
        {
            switch (kind)
            {
                case BarKind.Hp:
                    frameName = RpgUiCatalog.BarFrameRed; fillName = RpgUiCatalog.BarFillRed; tintFill = false; break;
                case BarKind.Mp:
                    // Pack MP fill is green-glow; tint it blue (mana has no dynamic colour change).
                    frameName = RpgUiCatalog.BarFrameBlue; fillName = RpgUiCatalog.BarFillBlue; tintFill = true; break;
                case BarKind.Atb:
                    frameName = RpgUiCatalog.BarFrameBlue; fillName = RpgUiCatalog.BarFillBlue; tintFill = true; break;
                case BarKind.Xp:
                case BarKind.Castle:
                default:
                    // Generic gold/green socket frame; tint the fill to the kind colour.
                    frameName = RpgUiCatalog.BarFrameGreen; fillName = RpgUiCatalog.BarFillGreen; tintFill = true; break;
            }
        }

        // =====================================================================
        // PORTRAIT — circular class disc + gilt ring (ported from BattleHudUgui
        // CircleSprite/RingSprite) so combat + town frame portraits IDENTICALLY.
        // =====================================================================

        /// <summary>Handle returned by <see cref="Portrait"/>: the disc Image + its ring frame.</summary>
        public sealed class PortraitHandle
        {
            /// <summary>The circular portrait disc (assign .sprite to show class art).</summary>
            public Image image;
            /// <summary>The hollow ring frame around the disc (gilt when active, gold when not).</summary>
            public Image ring;
        }

        /// <summary>
        /// A circular portrait: a disc Image (shows <paramref name="sprite"/> when present,
        /// else a warm tan placeholder disc) inside a hollow gilt ring frame. The ring is
        /// brighter gilt when <paramref name="active"/>, soft gold otherwise. Fills its
        /// parent rect (size the parent). Returns a <see cref="PortraitHandle"/>.
        /// </summary>
        public static PortraitHandle Portrait(Transform parent, Sprite sprite, bool active = false)
        {
            var discGo = new GameObject("Portrait", typeof(Image));
            discGo.transform.SetParent(parent, false);
            var dr = discGo.GetComponent<RectTransform>();
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
            var disc = discGo.GetComponent<Image>();
            if (sprite != null) { disc.sprite = sprite; disc.color = Color.white; disc.preserveAspect = true; }
            else { disc.sprite = CircleSprite; disc.color = PortraitPlaceholder; }
            disc.raycastTarget = false;

            var ringGo = new GameObject("Ring", typeof(Image));
            ringGo.transform.SetParent(parent, false);
            var rr = ringGo.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var ring = ringGo.GetComponent<Image>();
            ring.sprite = RingSprite;
            ring.color = active ? ElarionUi.Gilt : new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
            ring.raycastTarget = false;
            ringGo.transform.SetAsLastSibling();

            return new PortraitHandle { image = disc, ring = ring };
        }

        /// <summary>Warm tan placeholder fill for a portrait disc with no class art yet.</summary>
        public static readonly Color PortraitPlaceholder = new Color(0.74f, 0.66f, 0.50f, 1f);

        /// <summary>
        /// ONE shared class-portrait resolver (consolidates BattleHudUgui.PortraitFor +
        /// VillageHudController.PortraitNameForClass). Loads
        /// Resources/HudIcons/&lt;Class&gt;/&lt;class&gt;; FIXES the Wizard "wiard" typo by
        /// trying "wizard" then "wiard"; and degrades gracefully when the Healer art is
        /// absent (falls back to the RPG-pack heart icon, then null). Accepts the canonical
        /// class words: Knight / Ranger / Wizard|Mage / Healer|Cleric (case-insensitive).
        /// Returns null when nothing resolves (callers keep their placeholder disc).
        /// </summary>
        public static Sprite PortraitForClass(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return null;
            switch (cls.Trim().ToLowerInvariant())
            {
                case "knight": return Resources.Load<Sprite>("HudIcons/Knight/knight");
                case "ranger": return Resources.Load<Sprite>("HudIcons/Ranger/ranger");
                case "mage":
                case "wizard":
                {
                    var sp = Resources.Load<Sprite>("HudIcons/Wizard/wizard");
                    if (sp == null) sp = Resources.Load<Sprite>("HudIcons/Wizard/wiard"); // staged-art typo
                    return sp;
                }
                case "cleric":
                case "healer":
                {
                    var sp = Resources.Load<Sprite>("HudIcons/Healer/healer");
                    if (sp == null) sp = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconHeart); // graceful fallback
                    return sp; // may be null — caller keeps its placeholder disc
                }
                default: return null;
            }
        }

        // =====================================================================
        // PARTY FRAME ROW — the town-HUD party-row recipe (portrait + name banner
        // + HP/MP bars on player_frame_bg / player_hp_fill), extracted so combat
        // and town render the IDENTICAL frame (ported from BuildPartyFrames).
        // =====================================================================

        /// <summary>Handle returned by <see cref="PartyFrameRow"/>: the row's live pieces.</summary>
        public sealed class PartyRowHandle
        {
            /// <summary>The row's root rect (parent it / position it).</summary>
            public RectTransform root;
            /// <summary>The class portrait disc Image (assign .sprite via <see cref="PortraitForClass"/>).</summary>
            public Image portrait;
            /// <summary>The name banner label (gold ink).</summary>
            public TMP_Text nameLabel;
            /// <summary>The HP fill (Image.Type.Filled) — drive fillAmount.</summary>
            public Image hpFill;
            /// <summary>The MP fill (Image.Type.Filled) — drive fillAmount.</summary>
            public Image mpFill;
            /// <summary>Optional HP value label centred on the HP bar.</summary>
            public TMP_Text hpText;
        }

        /// <summary>
        /// The town-HUD party-row: a dark-stone frame (player_frame_bg art, sprite-FIRST
        /// with a dark-panel fallback) carrying a circular class portrait, a gold name
        /// banner, and stacked HP (red) + MP (blue) bars dressed with the player_hp_fill /
        /// player_mp_fill art. Fills its parent rect (size + position the parent). Returns
        /// a <see cref="PartyRowHandle"/> so callers drive the portrait/name/fills without
        /// re-implementing the frame. Identical recipe for combat + town.
        /// </summary>
        public static PartyRowHandle PartyFrameRow(Transform parent, string initialName = "Hero")
        {
            var frameSprite = Resources.Load<Sprite>("HudIcons/player_frame_bg");
            var hpSprite    = Resources.Load<Sprite>("HudIcons/player_hp_fill");
            var mpSprite    = Resources.Load<Sprite>("HudIcons/player_mp_fill");

            var rootGo = new GameObject("PartyRow", typeof(Image));
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            var frImg = rootGo.GetComponent<Image>();
            if (frameSprite != null) { frImg.sprite = frameSprite; frImg.color = Color.white; }
            else { frImg.color = new Color(0.10f, 0.09f, 0.11f, 0.96f); ApplyRounded(frImg); }
            frImg.raycastTarget = false;

            // Portrait (class disc) in the circle on the left.
            var portWrap = AddImage(rootGo.transform, "PortraitWrap",
                                    new Vector2(0.035f, 0.12f), new Vector2(0.26f, 0.94f),
                                    new Color(0f, 0f, 0f, 0f), rounded: false);
            portWrap.GetComponent<Image>().raycastTarget = false;
            var portrait = Portrait(portWrap.transform, null, active: true);

            // Name banner (upper-right) — gold ink, auto-sizing.
            var nameLabel = Label(rootGo.transform, initialName, 0.52f, 0.95f,
                                  new Color(0.95f, 0.88f, 0.62f), ElarionUi.FontHead,
                                  TextAlignmentOptions.Left, 0.31f, 0.97f, bold: true);
            nameLabel.enableAutoSizing = true; nameLabel.fontSizeMin = 9f; nameLabel.fontSizeMax = 15f;

            // HP bar (red, mid-right).
            var hp = Bar(rootGo.transform, BarKind.Hp,
                         new Vector2(0.31f, 0.30f), new Vector2(0.985f, 0.50f), withValue: true);
            if (hpSprite != null) { hp.fill.sprite = hpSprite; hp.fill.color = Color.white; }

            // MP bar (blue, lower-right).
            var mp = Bar(rootGo.transform, BarKind.Mp,
                         new Vector2(0.31f, 0.07f), new Vector2(0.985f, 0.27f), withValue: false);
            if (mpSprite != null) { mp.fill.sprite = mpSprite; mp.fill.color = Color.white; }

            return new PartyRowHandle
            {
                root = root,
                portrait = portrait.image,
                nameLabel = nameLabel,
                hpFill = hp.fill,
                mpFill = mp.fill,
                hpText = hp.valueLabel
            };
        }

        // =====================================================================
        // PANEL / BUTTON — pack-frame-over-glass variants (RpgUiCatalog
        // RolePanel / RoleButton), sprite-FIRST with the procedural fallback.
        // =====================================================================

        /// <summary>
        /// A <see cref="Panel"/> dressed sprite-FIRST with the RPG pack's ornate panel
        /// frame (RolePanel) over the dark glass: when the pack art is present the panel's
        /// Image shows the framed plate; when absent it is the procedural glass+rim panel.
        /// Anchored by fraction-of-parent. Returns the panel GameObject.
        /// </summary>
        public static GameObject PanelFramed(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                             bool deep = false, string packSpriteName = null)
        {
            var p = Panel(parent, anchorMin, anchorMax, deep: deep, innerRim: true);
            // Only dress with a pack panel sprite when the caller EXPLICITLY names one (WO-438 maps a
            // specific frame per screen). A null/empty name must stay "plain dark-glass" — some screens
            // (e.g. the store) deliberately keep dark glass so their rows read clearly. Never default via
            // RpgUiCatalog.Get(role, null): that returns the FIRST sprite in the role (a gold grid), which
            // is what made the store read as an empty gold grid. Sprite-first with the procedural fallback.
            if (!string.IsNullOrEmpty(packSpriteName))
            {
                var packSprite = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, packSpriteName);
                if (packSprite != null && p.GetComponent<Image>() is Image img)
                {
                    img.sprite = packSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
            }
            return p;
        }

        /// <summary>
        /// A <see cref="Button"/> dressed sprite-FIRST with the RPG pack's button frame
        /// (RoleButton) over the kind fill: pack art when present, procedural rounded glass
        /// otherwise. Same state feedback + label rules as <see cref="Button"/>. Returns
        /// the Button so callers wire interactable / extra listeners.
        /// </summary>
        public static Button ButtonPack(Transform parent, string label, ButtonKind kind,
                                        Vector2 anchorMin, Vector2 anchorMax, Action onClick = null,
                                        string packSpriteName = null)
        {
            // The procedural gold button already carries the label clearly.
            var btn = Button(parent, label, kind, anchorMin, anchorMax, onClick);
            // IMPORTANT: the default RoleButton sprite (ButtonGold = "Play buttons/button 3") and the
            // rest of the pack's button art live in the Play-buttons folder with the word PLAY baked
            // INTO the graphic — overlaying it on a LABELED button makes BUY/SELL/EQUIP all read
            // "PLAY". So we only overlay a pack sprite when the caller EXPLICITLY supplies one (and is
            // responsible for it being text-free); otherwise the clean procedural gold button + label
            // is used. This is what fixes "PLAY everywhere" on the store tabs / inventory / equip.
            if (!string.IsNullOrEmpty(packSpriteName) && btn != null)
            {
                var packSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, packSpriteName);
                if (packSprite != null && btn.targetGraphic is Image img)
                {
                    img.sprite = packSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
            }
            return btn;
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
            if (panel == null || DeNelle.Core.FeatureFlags.BlinkChrome) return;   // chrome: skip the bottom gold rule
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
            if (host == null || DeNelle.Core.FeatureFlags.BlinkChrome) return;   // chrome: skip the gilt inner rim
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

        // ── Circle + ring sprites (ported from BattleHudUgui; lazily built once) ──
        // The portrait disc + gilt ring frame, shared so combat/town portraits match.
        private static Sprite _circle;
        private static bool _circleTried;
        private static Sprite _ring;
        private static bool _ringTried;

        /// <summary>Solid white AA disc for a portrait fill (null if the build failed under WebGL).</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (!_circleTried)
                {
                    _circleTried = true;
                    try { _circle = BuildCircleSprite(); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[ElarionUiKit] circle sprite build failed: " + e.Message);
                        _circle = null;
                    }
                }
                return _circle;
            }
        }

        /// <summary>Hollow white AA ring for a portrait frame (null if the build failed under WebGL).</summary>
        public static Sprite RingSprite
        {
            get
            {
                if (!_ringTried)
                {
                    _ringTried = true;
                    try { _ring = BuildRingSprite(); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[ElarionUiKit] ring sprite build failed: " + e.Message);
                        _ring = null;
                    }
                }
                return _ring;
            }
        }

        private static Sprite BuildCircleSprite()
        {
            const int size = 64;
            float r = size * 0.5f - 1f;
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildRingSprite()
        {
            const int size = 64;
            float ro = size * 0.5f - 1f;
            float ri = ro - 5f; // ring thickness
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float aOut = Mathf.Clamp01(ro - d + 0.5f);
                    float aIn = Mathf.Clamp01(d - ri + 0.5f);
                    float a = Mathf.Min(aOut, aIn);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
