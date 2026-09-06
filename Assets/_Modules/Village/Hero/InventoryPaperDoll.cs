// =============================================================================
// InventoryPaperDoll — the Bag's HEADER strip (rewritten for WO-1133).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ⚠ THE FILE KEPT ITS NAME; ITS JOB CHANGED. There is no paper-doll card any more.
// What this file used to build was the gold hero CARD down the left of the bag —
// and that card was the ticket. Item by item, WO-1133 D8 removals, all gone:
//
//   * THE EMPTY PREVIEW BOX. A dark rectangle where a hero should be. Not a
//     mystery: the RT probe reports the preview render texture is a uniform clear
//     colour, and the camera's clear colour is byte-identical to the plate behind
//     it, so "drew a hero" and "drew nothing" were the same pixels. An empty box is
//     worse than no box - it reads as broken, and it was the single biggest reason
//     the gear view "had no benefit".
//   * THE GOLD "VIEW GEAR" RIBBON painted across that box. It called
//     PanelRouter.Open(PanelId.EquipmentPanel) - so the broken preview was sitting
//     directly on top of a button that opened the real gear screen. The route lives
//     on as rail entry one (D1: PROMOTE the gear view, cut the door not the room).
//   * THE CARD ITSELF, which overlapped the panel's own ornate frame (defect 7),
//     and its saturated green/blue/magenta bar stack (defect 9).
//
// ⛔ DO NOT RE-ADD ANY OF THE THREE. A second empty box would be worse than the
// first. If a hero render belongs anywhere on this screen it goes in the Gear
// section's niche, through TryMountHeroPreview's evidence gate, which refuses to
// mount a texture nothing has verified drew.
//
// WHAT IT BUILDS NOW (D3): the full-width header band - who you are, and your
// vitals, on ONE row, freeing the entire left band for the rail. The hero portrait
// helpers stay here because the frame's medallion socket still uses them.
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
        /// <summary>
        /// The header band: hero name, class + level, and the HP / MP / XP vitals, in one row.
        ///
        /// D5 (the owner is red/green colourblind): every vital carries its VALUE AS TEXT
        /// alongside the bar, so the reading survives a greyscale pass - the bar's LENGTH and
        /// the written number both mean the same thing and neither is a hue. The kit's
        /// BuildObsidianBar with withValue:true is the §1.1 fill-binding contract (bar and
        /// "cur/max" label written atomically), so the number can never drift from the fill.
        /// </summary>
        private void RebuildHeader()
        {
            if (_headerRoot == null) return;
            for (int i = _headerRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = _headerRoot.transform.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            string job = HeroJob;
            int level = HeroLevel();

            // LEFT — identity. Name on top, class + level beneath, both fit-or-ellipsize inside
            // their own band so neither can bleed into the vitals (§1.14).
            var nameLbl = AddLabel(_headerRoot.transform, HeroDisplayName(job), 0.50f, 1f, GiltInk,
                     ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft, 0.15f, 0.34f,
                     spacing: 1f, bold: true);
            ElarionUiKit.FitSingleLine(nameLbl, 0f, ElarionUi.FontHead);

            var classLbl = AddLabel(_headerRoot.transform,
                     Cap(job).ToUpperInvariant() + "   LV " + level, 0.02f, 0.46f,
                     InkMicro, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.MidlineLeft,
                     0.15f, 0.34f, spacing: 2f);
            ElarionUiKit.FitSingleLine(classLbl, 0f, ElarionUi.FontMicro);

            // Vitals are a presentation read of the live hero's components (the same resolve
            // family the medallion portrait uses). A missing hero leaves quiet full bars with
            // BLANK labels — never a fake number (ResetLabel), never a blank column.
            var vitalsHero = GameObject.FindWithTag("Player");
            if (vitalsHero == null) vitalsHero = SafeFindByTag("HeroTarget");
            var hh = vitalsHero != null ? vitalsHero.GetComponentInChildren<HeroHealth>() : null;
            var ha = vitalsHero != null ? vitalsHero.GetComponentInChildren<HeroAbilities>() : null;
            var prog = _loadout != null ? _loadout.GetComponent<HeroProgression>()
                     : (vitalsHero != null ? vitalsHero.GetComponentInChildren<HeroProgression>() : null);

            var hpBar = ElarionUiKit.BuildObsidianBar(_headerRoot.transform, ElarionUiKit.ObsidianBarKind.Health,
                new Vector2(0.36f, 0.56f), new Vector2(0.60f, 0.90f), withValue: true);
            RestyleHeaderBar(hpBar, new Color(0.42f, 0.055f, 0.075f, 1f));
            if (hh != null) hpBar.SetImmediate(hh.Hp, hh.MaxHp);
            else { hpBar.SetImmediate(1f, 1f); hpBar.ResetLabel(); }

            var mpBar = ElarionUiKit.BuildObsidianBar(_headerRoot.transform, ElarionUiKit.ObsidianBarKind.Mana,
                new Vector2(0.36f, 0.10f), new Vector2(0.60f, 0.44f), withValue: true);
            RestyleHeaderBar(mpBar, new Color(0.055f, 0.16f, 0.34f, 1f));
            if (ha != null) mpBar.SetImmediate(ha.Mana, ha.MaxMana);
            else { mpBar.SetImmediate(1f, 1f); mpBar.ResetLabel(); }

            // Level badge (badge_level art; the text "LV n" always carries the value, so the
            // badge is never the only place the number lives) + a thin XP strip beside it.
            var badgeSp = RpgUiCatalog.Get(RpgUiCatalog.RoleBadge, RpgUiCatalog.BadgeLevel);
            var badgeGo = AddImage(_headerRoot.transform, "LevelBadge",
                                   new Vector2(0.61f, 0.12f), new Vector2(0.68f, 0.88f),
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

            var xpBar = ElarionUiKit.BuildObsidianBar(_headerRoot.transform, ElarionUiKit.ObsidianBarKind.Xp,
                new Vector2(0.68f, 0.32f), new Vector2(0.75f, 0.68f), withValue: false);
            RestyleHeaderBar(xpBar, new Color(0.48f, 0.31f, 0.075f, 1f));
            if (prog != null) xpBar.SetImmediate(prog.Xp, prog.XpToNext);
            else xpBar.SetImmediate(0f, 1f);

            // WO-1254: Skills and Map are wayfinding chips in the header, never
            // inventory categories. Both are words, so dormancy survives greyscale.
            var talents = ElarionUiKit.ButtonPack(_headerRoot.transform,
                HudStrings.HeroFaceLabel(HudStrings.KeyHeroSkills, "button"),
                ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.755f, 0f), new Vector2(0.87f, 1f), OpenSkillTree);
            if (talents != null) ElarionUiKit.ClampMinTouch(talents);
            if (talents != null)
            {
                MedievalUiSkin.ApplyButton(talents, primary: true);
                var talentsImage = talents.targetGraphic as Image;
                if (talentsImage != null) talentsImage.type = Image.Type.Simple;
            }

            FlowTrace.Step("Inventory",
                $"Header built: job='{job}' lv={level} hero={(vitalsHero != null ? "found" : "MISSING")} " +
                $"hp={(hh != null ? "live" : "none")} mp={(ha != null ? "live" : "none")} xp={(prog != null ? "live" : "none")} " +
                "Talents=header RealmMap=retired");
        }

        // Bag identity bars use the same quiet native treatment as Equipment. The Obsidian
        // source art carries saturated gradients which read as a second, legacy UI family.
        private static void RestyleHeaderBar(ElarionUiKit.BarHandle bar, Color fillColor)
        {
            if (bar == null) return;
            if (bar.fill != null)
            {
                bar.fill.sprite = ElarionUiKit.SolidSprite;
                bar.fill.type = Image.Type.Simple;
                bar.fill.color = fillColor;
            }
            if (bar.frame != null)
            {
                bar.frame.sprite = ElarionUiKit.SolidSprite;
                bar.frame.type = Image.Type.Simple;
                bar.frame.color = new Color(0.50f, 0.37f, 0.14f, 0.72f);
            }
        }

        // WO-573 — load the active hero's portrait art for the frame's medallion socket. The
        // portraits under HeroPortraitPaths.ResourcesFolder are imported as DEFAULT textures (spriteMode:0),
        // so Resources.Load<Sprite> returns NULL — we try Sprite first (future-proof if the
        // import flips) then load the Texture2D and wrap it in a Sprite. Returns null when no
        // art exists for the class (caller shows a crest).
        private static Sprite LoadHeroPortrait(string job)
        {
            string slug = PortraitSlug(job);
            if (string.IsNullOrEmpty(slug)) return null;

            // WO-1234: folder from the ONE constant, never re-typed here.
            string key = DeNelle.Core.HeroPortraitPaths.ResourceKey(slug);
            var sp = Resources.Load<Sprite>(key);
            if (sp != null) return sp;

            var tex = Resources.Load<Texture2D>(key);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);

            FlowTrace.Warn("Inventory",
                $"LoadHeroPortrait: no Resources/{key} sprite or texture for job '{job}' — using class-crest placeholder.");
            return null;
        }

        // Map a hero class to its portrait file slug (the first token of the canon display name:
        // Grom / Thrain / Sylas / Elara), so the card stays in sync with HeroDisplayName.
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

        /// <summary>
        /// The portrait slug for the hero the player actually picked. The persisted-class half of
        /// the resolve lives in <see cref="InventoryVM.ActiveHeroJobKey"/> (class -> job key,
        /// sourced from GameState.HeroClass); this View only maps that job key onto the ART FILE
        /// name via <see cref="PortraitSlug"/>.
        ///
        /// WHY the indirection (strict-MVVM, UI-MVVM conformance oracle): this partial builds
        /// uGUI, so it is a VIEW — and a View must never read game state. The first cut of this
        /// method called GameStateService.Instance directly and tripped the oracle. Routing it
        /// through the bound VM is the SAME idiom the sibling partial uses for the wallet.
        ///
        /// Never returns null/empty: no bound VM falls back to the roster default's slug rather
        /// than blanking the medallion.
        /// </summary>
        private string ActiveHeroPortraitSlug()
        {
            const string DefaultSlug = "Grom";   // PlayableHeroes.Default (Knight) art slug

            if (_vm == null)
            {
                FlowTrace.Warn("Inventory",
                    "ActiveHeroPortraitSlug: no bound InventoryVM - showing the " + DefaultSlug +
                    " portrait. The VM is constructed before BuildRoot in Open(), so a null here " +
                    "means ConstructViewModel threw; that is the real defect, not the portrait.");
                return DefaultSlug;
            }
            return PortraitSlug(_vm.ActiveHeroJobKey) ?? DefaultSlug;
        }
    }
}
