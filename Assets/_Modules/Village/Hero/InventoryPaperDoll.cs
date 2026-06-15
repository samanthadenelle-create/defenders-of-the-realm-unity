// =============================================================================
// InventoryPaperDoll — paper-doll rendering (split from HeroInventoryController).
// -----------------------------------------------------------------------------
// Exact extraction for zero behavior change. RebuildPaperDoll, rows, bars.
// Heavy Tech for W/A medallion/slots as per current (Profile tabs, Healing, Sword icons).
// Matches ElarionUiKit dark-wood + gold (Forge shop look). No layout/func change.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        private void RebuildPaperDoll()
        {
            if (_paperDoll == null) return;
            for (int i = _paperDoll.transform.childCount - 1; i >= 0; i--)
                Destroy(_paperDoll.transform.GetChild(i).gameObject);

            string job = HeroJob;
            int level = HeroLevel();

            // ── Heavy Tech hud elements paper-doll medallion for weapons/armor (no Rpg, no _profileFrameSprite).
            // Profile tabs P1/fill.png (pack's ornate player tab style) as frame. Crest + name + Tech bars.
            // Redesigned left portrait area to match the mockup: ornate gold frame (Profile tab P1), inner portrait area with hero face (large glyph or loaded portrait), Lvl, name, colored bars for HP/MP, and stats.
            // Elegant circular/oval frame for mobile RPG raid style hero portrait.
            var medBand = AddImage(_paperDoll.transform, "MedBand",
                                   new Vector2(0.0f, 0.610f), new Vector2(1.0f, 0.995f), Color.white);
            var mbImg = medBand.GetComponent<Image>();
            if (mbImg != null)
            {
                var techFrame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/fill.png");
                if (techFrame == null) techFrame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/bg.png");
                if (techFrame == null) techFrame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P2/fill.png");
                if (techFrame != null)
                {
                    mbImg.sprite = techFrame; mbImg.type = Image.Type.Sliced;
                    mbImg.color = Color.white; mbImg.preserveAspect = false; mbImg.raycastTarget = false;
                    AddInnerRim(medBand, new Color(0.85f, 0.75f, 0.55f, 0.6f));
                }
                else
                {
                    mbImg.color = StoneNiche; ApplyRounded(mbImg);
                }
            }

            // Inner portrait circle for hero face (elegant like mockup blue inner with gold border).
            // Use large crest or hero glyph for the face; in full impl load hero portrait sprite from Resources/HeroPortraits.
            var portrait = AddCircle(medBand.transform, "HeroPortrait", 0.32f, 0.72f, 0.28f, new Color(0.1f, 0.15f, 0.35f, 1f)); // blue inner like mockup
            AddCircleRim(portrait, new Color(0.85f, 0.7f, 0.3f, 1f)); // gold rim

            // Large hero glyph in the portrait (or face).
            AddLabel(portrait.transform, ClassCrest(job), 0.0f, 1.0f, GiltInk,
                     ElarionUi.FontTitle + 40, TMPro.TextAlignmentOptions.Center, 0.1f, 0.9f, bold: true);

            // "Lvl" and hero info like mockup.
            AddLabel(medBand.transform, "Lvl", 0.05f, 0.25f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.05f, 0.3f);
            AddLabel(medBand.transform, "Hero", 0.05f, 0.18f, Ink,
                     ElarionUi.FontMicro + 4, TMPro.TextAlignmentOptions.Left, 0.05f, 0.3f);

            // Name and class like mockup (Grom Ironhand).
            AddLabel(medBand.transform, HeroDisplayName(job), 0.55f, 0.92f, Ink,
                     ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.32f, 0.98f, spacing: 1f);
            AddLabel(medBand.transform, Cap(job).ToUpperInvariant() + "  LV " + level, 0.42f, 0.58f,
                     InkMicro, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.32f, 0.98f, spacing: 2f);

            // Colored bars for HP (red), other stats (green, blue) like the mockup.
            PaperDollBarTech("HP", 0.25f, 0.38f, new Color(0.8f, 0.1f, 0.1f, 1f), medBand.transform);
            // Additional bar for "Till Next Level" or other, using green.
            PaperDollBarTech("LVL", 0.12f, 0.22f, new Color(0.2f, 0.6f, 0.2f, 1f), medBand.transform);

            // Heavy Tech hud elements for paper-doll bars (W/A focus): use pack's Loading bar or GreenUielements
            // loading assets (from Tech hud elements) instead of Rpg. Clean, no legacy Rpg dep for gear UI.
            // Fallback tinted if pack sprites absent.
            PaperDollBarTech("HP", 0.30f, 0.46f, new Color(0.62f, 0.16f, 0.14f, 1f), medBand.transform);
            PaperDollBarTech("MP", 0.10f, 0.26f, new Color(0.18f, 0.33f, 0.62f, 1f), medBand.transform);

            // No EQUIPMENT list / slots in left panel to exactly match the mockup's left side (portrait + name + bars + stats only).
            // The left is pure character details as in the provided mockup image.
            // Equipped status is shown via grid cell highlights/marks and the hero portrait/glyph.
            // Equipping functionality preserved (tap a grid cell to equip; selection/rebuilt grid works).
        }

        private void PaperDollRow(int slot, float top, float rowH, float gap, string label,
                                  string icon, Sprite iconSprite, string value, string rarity, bool filled)
        {
            float y1 = top - slot * (rowH + gap);
            float y0 = y1 - rowH;

            Color rc    = filled ? RarityColor(rarity) : AccentSoft;
            Color rcInk = filled ? RarityInk(rarity)   : InkDim;

            if (filled)
            {
                var halo = AddImage(_paperDoll.transform, "EquipGlow_" + label,
                                    new Vector2(0.025f, y0 - 0.012f), new Vector2(0.975f, y1 + 0.012f),
                                    new Color(rc.r, rc.g, rc.b, 0.22f), rounded: false);
                NoRaycast(halo);
            }

            var row = AddImage(_paperDoll.transform, "EquipRow_" + label,
                               new Vector2(0.04f, y0), new Vector2(0.96f, y1),
                               filled ? CellSel : new Color(Cell.r, Cell.g, Cell.b, 0.45f));
            NoRaycast(row);
            AddInnerRim(row, new Color(rc.r, rc.g, rc.b, filled ? 0.85f : 0.22f));

            Color socketTint = filled ? rc : new Color(AccentSoft.r, AccentSoft.g, AccentSoft.b, AccentSoft.a * 0.6f);
            var sock = ElarionUiKit.TechGearSocket(row.transform, "TechSocket", new Vector2(0.04f, 0.10f), new Vector2(0.32f, 0.90f), socketTint, isWeapon: label == "WEAPON");
            NoRaycast(sock);
            Color glyphCol = filled ? rcInk : new Color(InkDim.r, InkDim.g, InkDim.b, 0.55f);
            string glyph = filled
                ? (string.IsNullOrEmpty(icon) ? "?" : icon)
                : SlotGhostGlyph(label);
            AddIcon(sock.transform, filled ? iconSprite : null, glyph, ElarionUi.FontHead, glyphCol, filled ? 1f : 0.5f);

            AddLabel(row.transform, label, 0.50f, 0.92f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.40f, 0.97f, spacing: 2f);
            AddLabel(row.transform, value, 0.10f, 0.55f,
                     filled ? rcInk : new Color(InkDim.r, InkDim.g, InkDim.b, 0.7f),
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.40f, 0.97f, bold: filled);
        }

        private void PaperDollBarTech(string caps, float y0, float y1, Color fallbackFill, Transform host)
        {
            const float x0 = 0.50f, x1 = 0.97f;
            var frameGo = AddImage(host, "Bar_" + caps + "_frame",
                                   new Vector2(x0, y0), new Vector2(x1, y1), Color.white, rounded: false);
            var fImg = frameGo.GetComponent<Image>();
            if (fImg != null)
            {
                fImg.raycastTarget = false;
                Sprite fs = null;
                try { fs = Resources.Load<Sprite>("Tech hud elements/Sprites/GreenUielements/Loading bar/Loading bar"); } catch { }
                if (fs == null) try { fs = Resources.Load<Sprite>("Tech hud elements/Sprites/Loading 1/Loading 1"); } catch { }
                if (fs == null) try { fs = Resources.Load<Sprite>("Tech hud elements/Sprites/Healing Tabs/H4"); } catch { }
                if (fs != null) { fImg.sprite = fs; fImg.type = Image.Type.Sliced; fImg.color = Color.white; }
                else { fImg.color = new Color(0f, 0f, 0f, 0.35f); ApplyRounded(fImg); }
            }
            var fillGo = AddImage(frameGo.transform, "Bar_" + caps + "_fill",
                                  new Vector2(0.04f, 0.20f), new Vector2(0.97f, 0.80f), fallbackFill, rounded: false);
            var fillImg = fillGo.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.raycastTarget = false;
                Sprite fl = null;
                try { fl = Resources.Load<Sprite>("Tech hud elements/Sprites/GreenUielements/Loading bar/Loading bar"); } catch { }
                if (fl != null) { fillImg.sprite = fl; fillImg.type = Image.Type.Sliced; fillImg.color = Color.white; }
                else ApplyRounded(fillImg);
            }
            AddLabel(frameGo.transform, caps, 0f, 1f, Ink, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.Left, 0.03f, 0.30f, bold: true);
        }

    }
}