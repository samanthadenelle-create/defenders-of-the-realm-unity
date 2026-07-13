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
using DeNelle.Core.Diagnostics;
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

            // ── WO-573 FIX (owner felt-bug: "giant gold OVAL blob").
            // The card used to load a Tech/Rpg medallion sprite as its FULL-CARD background;
            // on the live build the chain fell through to RpgUiCatalog.PanelProfile ("profile_frame"
            // = a gold sunburst portrait medallion) which, stretched across this narrow/tall card,
            // read as a giant gold oval — and NO real portrait art was ever loaded (just a tinted
            // disc + gold AddCircleRim + a class glyph). Now: a flat OBSIDIAN card (black + thin gold
            // inner rim — the WO-554 chrome), with the REAL hero portrait art framed cleanly inside
            // (fixed region, preserveAspect → never an ellipse). No portrait on disk → a clean dark
            // placeholder + class crest, never a raw gold blob.
            var medBand = AddImage(_paperDoll.transform, "MedBand",
                                   new Vector2(0.0f, 0.04f), new Vector2(1.0f, 0.99f),
                                   new Color(0.03f, 0.03f, 0.045f, 0.96f));
            var mbImg = medBand.GetComponent<Image>();
            if (mbImg != null)
            {
                ApplyRounded(mbImg);
                mbImg.raycastTarget = false;
                AddInnerRim(medBand, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
            }

            // Real hero portrait — TOP of the card, full width, in a thin gold-rimmed obsidian frame.
            // preserveAspect keeps the bust un-stretched; the frame clips letterbox to the rounded card.
            var portraitFrame = AddImage(medBand.transform, "PortraitFrame",
                                         new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.97f),
                                         new Color(0.015f, 0.015f, 0.02f, 1f));
            NoRaycast(portraitFrame);
            AddInnerRim(portraitFrame, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.70f));

            // LIVE 3D dressed hero FIRST (REUSE the Gear screen's proven HeroPreviewViewer): renders
            // the active hero with the equipped weapon/shield/armor into this frame. Falls back to the
            // static 2D portrait / class crest when there is no hero body or the viewer can't build —
            // so the niche is never the empty transparent container it used to be.
            if (!TryMountHeroPreview(portraitFrame.transform))
            {
                var artSprite = LoadHeroPortrait(job);
                if (artSprite != null)
                {
                    var art = AddImage(portraitFrame.transform, "PortraitArt",
                                       new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Color.white);
                    var aImg = art.GetComponent<Image>();
                    if (aImg != null)
                    {
                        aImg.sprite = artSprite;
                        aImg.type = Image.Type.Simple;
                        aImg.preserveAspect = true;     // <- the fix: never stretch into an ellipse/blob
                        aImg.raycastTarget = false;
                    }
                }
                else
                {
                    // Clean framed placeholder (NOT a raw gold ellipse): a class crest on the dark frame.
                    AddLabel(portraitFrame.transform, ClassCrest(job), 0f, 1f, GiltInk,
                             ElarionUi.FontTitle + 30, TMPro.TextAlignmentOptions.Center, 0.1f, 0.9f, bold: true);
                }
            }

            // Tappable hero portrait → open the full Character / Gear Preview paper-doll (EquipmentPanel).
            // A transparent overlay button covers the WHOLE portrait region (the preview RawImage + frame
            // are NoRaycast, so the tap lands here). Owner ask: the old micro "View Gear" tag read as a tiny
            // dead link, so the large tap target was undiscoverable — make it OBVIOUS: a visible gold ribbon
            // button across the portrait's bottom edge with a large label. Added LAST so it sits on top of
            // the live preview. Null-safe.
            var gearTapGo = AddImage(medBand.transform, "ViewGearTap",
                                     new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.97f), new Color(0, 0, 0, 0));
            var gearTapImg = gearTapGo.GetComponent<Image>();
            var gearTapBtn = gearTapGo.AddComponent<Button>();
            gearTapBtn.targetGraphic = gearTapImg;
            StyleButtonColors(gearTapBtn);
            gearTapBtn.onClick.AddListener(OpenGearPreview);

            // Visible gold "VIEW GEAR" ribbon along the portrait's bottom edge — large, unmistakable.
            var gearRibbon = AddImage(gearTapGo.transform, "ViewGearRibbon",
                                      new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.16f),
                                      new Color(ElarionUi.Gold.r * 0.55f, ElarionUi.Gold.g * 0.42f, 0.06f, 0.92f));
            var ribImg = gearRibbon.GetComponent<Image>();
            if (ribImg != null) { ApplyRounded(ribImg); ribImg.raycastTarget = false; }
            AddInnerRim(gearRibbon, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f));
            var gearTag = AddLabel(gearRibbon.transform, "VIEW GEAR", 0.0f, 1.0f, Color.white,
                                   ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f,
                                   spacing: 2f, bold: true);
            gearTag.raycastTarget = false;
            // Eyes-sweep 2026-07-06: "VIEW GEAR" painted over "Grom Ironhand / KNIGHT LV1" —
            // FontHead text overflowed the thin ribbon down into the name band. Fit inside (§1.14).
            ElarionUiKit.FitSingleLine(gearTag, 0f, ElarionUi.FontHead);

            // Name + class • level — centered band just under the portrait (no overlap with the art).
            // Both labels fit-or-ellipsize inside their own band so they never bleed into the
            // ribbon above or the bars below.
            var heroNameLbl = AddLabel(medBand.transform, HeroDisplayName(job), 0.44f, 0.515f, Ink,
                     ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 1f, bold: true);
            ElarionUiKit.FitSingleLine(heroNameLbl, 0f, ElarionUi.FontHead);
            var classLbl = AddLabel(medBand.transform, Cap(job).ToUpperInvariant() + "   LV " + level, 0.385f, 0.44f,
                     InkMicro, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 2f);
            ElarionUiKit.FitSingleLine(classLbl, 0f, ElarionUi.FontMicro);

            // Colored bars in NON-overlapping vertical bands (HP red / MP blue / LVL green),
            // full card width below the name. Single HP bar (dup removed).
            PaperDollBarTech("HP", 0.27f, 0.36f, new Color(0.62f, 0.16f, 0.14f, 1f), medBand.transform);
            PaperDollBarTech("MP", 0.165f, 0.255f, new Color(0.18f, 0.33f, 0.62f, 1f), medBand.transform);
            // Additional bar for "Till Next Level" or other, using green.
            PaperDollBarTech("LVL", 0.06f, 0.15f, new Color(0.2f, 0.6f, 0.2f, 1f), medBand.transform);

            // No EQUIPMENT list / slots in left panel — portrait + name + bars only. Equipped status
            // is shown via grid cell highlights/marks. Equipping is preserved (tap a grid cell).
        }

        // WO-573 — load the active hero's portrait art for the inventory card. The portraits in
        // Resources/HeroPortraits are imported as DEFAULT textures (spriteMode:0), so
        // Resources.Load<Sprite> returns NULL — we try Sprite first (future-proof if the import flips)
        // then load the Texture2D and wrap it in a Sprite (mirrors TitleController.FramePortrait's
        // Texture2D fallback). Returns null when no art exists for the class (caller shows a crest).
        private static Sprite LoadHeroPortrait(string job)
        {
            string slug = PortraitSlug(job);
            if (string.IsNullOrEmpty(slug)) return null;

            var sp = Resources.Load<Sprite>("HeroPortraits/" + slug);
            if (sp != null) return sp;

            var tex = Resources.Load<Texture2D>("HeroPortraits/" + slug);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);

            FlowTrace.Warn("Inventory",
                $"LoadHeroPortrait: no Resources/HeroPortraits/{slug} sprite or texture for job '{job}' — using class-crest placeholder.");
            return null;
        }

        // Map a hero class to its portrait file slug (the first token of the canon display name:
        // Grom / Thrain / Sylas / Elara), so the inventory card stays in sync with HeroDisplayName.
        private static string PortraitSlug(string job)
        {
            switch ((job ?? "").ToLowerInvariant())
            {
                case "knight": return "Grom";
                case "mage":   return "Thrain";
                case "ranger": return "Sylas";
                case "healer":
                case "cleric": return "Elara";
                default:        return null;
            }
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
            // WO-573: full card width (was 0.50–0.97 = right half, which crowded the old portrait).
            const float x0 = 0.08f, x1 = 0.92f;
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
                // Clean-build fallback (Tech pack gitignored): committed RpgUi bar frame.
                if (fs == null) fs = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, RpgUiCatalog.BarFrameGreen);
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
                // Clean-build fallback (Tech pack gitignored): committed RpgUi tinted bar fill.
                if (fl == null) { fl = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, RpgUiCatalog.BarFillGreen); }
                if (fl != null) { fillImg.sprite = fl; fillImg.type = Image.Type.Sliced; fillImg.color = fl.name != null && fl.name.StartsWith("bar_fill") ? fallbackFill : Color.white; }
                else ApplyRounded(fillImg);
            }
            AddLabel(frameGo.transform, caps, 0f, 1f, Ink, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.Left, 0.03f, 0.30f, bold: true);
        }

    }
}