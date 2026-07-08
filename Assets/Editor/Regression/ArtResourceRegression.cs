// =============================================================================
// ArtResourceRegression — headless "real object in -> assert -> one marker" oracle
// for the resources-art LOAD PATHS: dialogue portraits, the RPG-UI sprite atlas,
// the projectile-art sheets, and the item-icon sheets. Proves — from the REAL
// Resources.Load path the runtime uses, not a re-derivation — whether each art
// family actually resolves on disk, so a silent-null (blank portrait / procedural
// fallback) becomes a build-visible failure instead of a "shows nothing" mystery.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// FAIL-BY-DESIGN (truthful): the HeroPortraits check WILL FAIL until the art folder
// is added. Resources/HeroPortraits does NOT exist on disk (verified: `ls` -> ABSENT,
// and docs/MASTER_CATALOG/resources-art.md flags "NO Resources/HeroPortraits folder").
// PortraitCache.Get("HeroPortraits/<name>") therefore loads neither a Sprite nor a
// Texture2D and caches a null SILENTLY — the dialogue speaker renders no portrait with
// no error. This oracle reproduces PortraitCache.Build's EXACT resolution (Resources.
// Load<Sprite> then Resources.Load<Texture2D>) so the absence is proven from data.
// (PortraitCache lives in DeNelle.DialogueUI, which this editor asmdef does not
// reference; replicating its two Resources.Load calls tests the same underlying path.)
//
// The RPG-UI, projectile-art and item-icon checks exercise the REAL catalogs
// (RpgUiCatalog / ProjectileArtCatalog / ItemIconCatalog) and are expected to PASS
// when their sheets are imported/sliced — if they are not, the oracle fails truthfully.
//
// No scene / no PlayMode. Loads only (no GameObject/Sprite instances retained beyond
// the load), so there is no throwaway state to clean up.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!ArtResourceRegression.Run(out var artResReason)) failures.Add(artResReason); else log.AppendLine("[art-res] " + artResReason);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class ArtResourceRegression
    {
        // Dialogue speaker portrait paths the runtime actually requests (grepped from
        // TitleController / HeroSelectController / dialogue speakers).
        private static readonly string[] PortraitPaths =
        {
            "HeroPortraits/Sylas",
            "HeroPortraits/Grom",
        };

        /// <summary>
        /// Proves each resources-art family resolves through its REAL load path. Returns
        /// true (PASS) only when every family resolves; false + a reason naming each defect.
        /// The HeroPortraits family FAILS BY DESIGN until Resources/HeroPortraits is added.
        /// Deterministic, self-contained, no scene / no PlayMode.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ART RESOURCES (portrait / rpg-ui atlas / projectile-art / item-icon load paths) ---");

            // ── (1) HeroPortraits — FAIL BY DESIGN (folder absent) ───────────────────
            // Mirror PortraitCache.Build EXACTLY: Sprite first, then Texture2D wrap.
            foreach (var path in PortraitPaths)
            {
                bool resolved = ResolvesLikePortraitCache(path);
                log.AppendLine($"portrait '{path}' resolves = {resolved}");
                if (!resolved)
                    failures.Add($"portrait '{path}' resolves to NULL — neither Sprite nor Texture2D at " +
                        $"Resources/{path} (Resources/HeroPortraits folder ABSENT); PortraitCache.Get caches a " +
                        "null silently and the dialogue speaker shows no portrait. FAIL-BY-DESIGN: add the art folder.");
            }

            // ── (2) RPG-UI sprite atlas — expected PASS (folder imported) ────────────
            // Exercise the REAL RpgUiCatalog through Resources/RpgUi/bars.
            var barsAll = RpgUiCatalog.All(RpgUiCatalog.RoleBars);
            int barsCount = barsAll != null ? barsAll.Count : 0;
            log.AppendLine($"RpgUiCatalog.All('{RpgUiCatalog.RoleBars}') = {barsCount} sprite(s)");
            if (barsCount == 0)
                failures.Add($"RpgUiCatalog role '{RpgUiCatalog.RoleBars}' resolved 0 sprites — " +
                    "Resources/RpgUi/bars empty/not imported (run Defenders/Art/Import RPG UI Pack).");

            var barFrameRed = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, RpgUiCatalog.BarFrameRed);
            log.AppendLine($"RpgUiCatalog.Get('bars','{RpgUiCatalog.BarFrameRed}') = {(barFrameRed != null ? "sprite" : "<null>")}");
            if (barFrameRed == null)
                failures.Add($"RpgUiCatalog named sprite 'bars/{RpgUiCatalog.BarFrameRed}' resolved NULL — " +
                    "the canonical HP bar-frame art is missing from the imported atlas.");

            // ── (3) Projectile-art sheets — expected PASS (2 sheets on disk) ─────────
            var arrow = ProjectileArtCatalog.ForArrow();
            log.AppendLine($"ProjectileArtCatalog.ForArrow() = {(arrow != null ? "sprite" : "<null>")}");
            if (arrow == null)
                failures.Add("ProjectileArtCatalog.ForArrow() resolved NULL — no arrow sprite under " +
                    "Resources/ProjectileIcons (sheet not sliced/imported); archer bolt falls to procedural visual.");

            var fireBolt = ProjectileArtCatalog.ForElement(DamageElement.Flame);
            log.AppendLine($"ProjectileArtCatalog.ForElement(Flame) = {(fireBolt != null ? "sprite" : "<null>")}");
            if (fireBolt == null)
                failures.Add("ProjectileArtCatalog.ForElement(Flame) resolved NULL — no fire-bolt sprite in " +
                    "Resources/ProjectileIcons; flame projectiles fall to procedural visual.");

            // ── (4) Item-icon sheets — expected PASS (icon sheets on disk) ───────────
            // A known consumable keyword (health potion) must map to a real sheet sprite.
            var healthPotion = ItemIconCatalog.ForConsumable("potion_health", "Health Potion");
            log.AppendLine($"ItemIconCatalog.ForConsumable('potion_health') = {(healthPotion != null ? "sprite" : "<null>")}");
            if (healthPotion == null)
                failures.Add("ItemIconCatalog.ForConsumable('potion_health') resolved NULL — no potion_health sprite " +
                    "under Resources/ItemIcons (sheets not sliced/imported); item grid falls to glyph fallback.");

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // Reproduce PortraitCache.Build's resolution WITHOUT referencing DeNelle.DialogueUI:
        // a portrait "resolves" iff Resources.Load<Sprite> OR Resources.Load<Texture2D> is non-null.
        private static bool ResolvesLikePortraitCache(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (Resources.Load<Sprite>(path) != null) return true;
            return Resources.Load<Texture2D>(path) != null;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "ART_RESOURCE_OK");
                return "ART RESOURCES OK — portraits, RPG-UI atlas, projectile-art + item-icon sheets all resolve";
            }
            string reason = "art-res: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "ART_RESOURCE_FAIL: " + reason);
            return reason;
        }
    }
}
