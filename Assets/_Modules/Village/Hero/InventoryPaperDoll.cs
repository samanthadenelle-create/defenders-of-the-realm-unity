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

            // WO-713 A.3 — HP and MP are KIT bars WITH values (BuildObsidianBar, the §1.1
            // fill-binding contract: bar + "cur/max" label written atomically); the old
            // decorative LVL bar becomes the badge_level chip + a THIN XP strip. Values are
            // presentation reads of the live hero's components (same resolve family the
            // portrait/preview already uses); a missing hero leaves full quiet bars with the
            // labels blank — never a fake number (ResetLabel), never a blank column.
            var vitalsHero = GameObject.FindWithTag("Player");
            if (vitalsHero == null) vitalsHero = SafeFindByTag("HeroTarget");
            var hh = vitalsHero != null ? vitalsHero.GetComponentInChildren<HeroHealth>() : null;
            var ha = vitalsHero != null ? vitalsHero.GetComponentInChildren<HeroAbilities>() : null;
            var prog = _loadout != null ? _loadout.GetComponent<HeroProgression>()
                     : (vitalsHero != null ? vitalsHero.GetComponentInChildren<HeroProgression>() : null);

            var hpBar = ElarionUiKit.BuildObsidianBar(medBand.transform, ElarionUiKit.ObsidianBarKind.Health,
                new Vector2(0.08f, 0.27f), new Vector2(0.92f, 0.36f), withValue: true);
            if (hh != null) hpBar.SetImmediate(hh.Hp, hh.MaxHp);
            else { hpBar.SetImmediate(1f, 1f); hpBar.ResetLabel(); }

            var mpBar = ElarionUiKit.BuildObsidianBar(medBand.transform, ElarionUiKit.ObsidianBarKind.Mana,
                new Vector2(0.08f, 0.165f), new Vector2(0.92f, 0.255f), withValue: true);
            if (ha != null) mpBar.SetImmediate(ha.Mana, ha.MaxMana);
            else { mpBar.SetImmediate(1f, 1f); mpBar.ResetLabel(); }

            // Level badge (badge_level art; text "LV n" always carries the value) + thin XP strip.
            var badgeSp = RpgUiCatalog.Get(RpgUiCatalog.RoleBadge, RpgUiCatalog.BadgeLevel);
            var badgeGo = AddImage(medBand.transform, "LevelBadge",
                                   new Vector2(0.08f, 0.055f), new Vector2(0.24f, 0.155f),
                                   badgeSp != null ? Color.white : new Color(0f, 0f, 0f, 0.35f),
                                   rounded: badgeSp == null);
            NoRaycast(badgeGo);
            if (badgeSp != null)
            {
                var bImg = badgeGo.GetComponent<Image>();
                bImg.sprite = badgeSp; bImg.type = Image.Type.Simple; bImg.preserveAspect = true;
            }
            var lvLbl = AddLabel(badgeGo.transform, "LV " + level, 0f, 1f, GiltInk,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            ElarionUiKit.FitSingleLine(lvLbl, 0f, ElarionUi.FontMicro);

            var xpBar = ElarionUiKit.BuildObsidianBar(medBand.transform, ElarionUiKit.ObsidianBarKind.Xp,
                new Vector2(0.27f, 0.075f), new Vector2(0.92f, 0.135f), withValue: false);
            if (prog != null) xpBar.SetImmediate(prog.Xp, prog.XpToNext);
            else xpBar.SetImmediate(0f, 1f);

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

        // (PaperDollRow + PaperDollBarTech retired in WO-713: the equipment-row column was
        //  already dead code [zero callers], and the decorative Tech bars are replaced by the
        //  kit BuildObsidianBar HP/MP-with-values + badge_level + XP strip above. Verified
        //  zero remaining references before deletion.)
    }
}