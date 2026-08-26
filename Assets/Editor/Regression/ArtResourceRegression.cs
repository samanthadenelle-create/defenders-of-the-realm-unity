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
// ── WO-1234 (2026-08-26) — the hero-portrait family, CORRECTED ───────────────
// ⚠ THE OLD HEADER HERE WAS TRUE ON THE DAY IT WAS WRITTEN AND IS NOW FALSE. It
// said the art folder "does NOT exist on disk (verified: ls -> ABSENT)" and that
// the portrait check "WILL FAIL until the art folder is added". The owner's card
// art is installed (Sylas/Elara/Thrain/Grom), so the check now PASSES and the
// FAIL-BY-DESIGN wording would send the next reader hunting a defect that was
// closed. Left standing as a correction rather than silently deleted, because a
// comment that quietly becomes false is the exact failure this suite now gates.
//
// The check itself is unchanged in KIND: it reproduces PortraitCache.Build's EXACT
// resolution (Resources.Load<Sprite> then Resources.Load<Texture2D>) so a silent
// null — a speaker or a hero card that renders nothing, with no error — is proven
// from data. (PortraitCache lives in DeNelle.DialogueUI, which this editor asmdef
// does not reference; replicating its two loads tests the same underlying path.)
//
// ── AND THE NEW CASE: [portrait-path-literals] ──────────────────────────
// The folder segment used to be typed out ELEVEN times across six files. It is now
// declared ONCE, in DeNelle.Core.HeroPortraitPaths, and this suite sweeps every .cs
// under Assets/ to prove the quoted literal never comes back. The token it searches
// for is DERIVED FROM THAT CONSTANT, never re-typed — which is what stops this file
// becoming copy number twelve, the failure CLAUDE.md records in §0/§2/§5.
//
// The RPG-UI, projectile-art and item-icon checks exercise the REAL catalogs
// (RpgUiCatalog / ProjectileArtCatalog / ItemIconCatalog) and are expected to PASS
// when their sheets are imported/sliced — if they are not, the oracle fails truthfully.
//
// No scene / no PlayMode. Loads only (no GameObject/Sprite instances retained beyond
// the load), so there is no throwaway state to clean up.
//
// ALREADY REGISTERED — DataRegression.RunAll calls this suite. The WO-1234 case
// lives INSIDE Run(), so it needs no new registration line:
//   if (!ArtResourceRegression.Run(out var artResReason)) failures.Add(artResReason); else log.AppendLine("[art-res] " + artResReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class ArtResourceRegression
    {
        // The canon-roster slugs whose art must resolve. ⛔ SLUGS ONLY — the FOLDER is
        // composed by HeroPortraitPaths.ResourceKey, so this ledger cannot drift from the
        // path the runtime actually loads (WO-1234). The slugs are the filenames on disk
        // and are owner-frozen (SlugFor: Mage->Thrain, Knight->Grom, Ranger->Sylas,
        // Cleric->Elara). Elara is LOCKED as a class but her card art still has to
        // resolve — the locked state renders that card under a scrim, it does not omit it.
        private static readonly string[] PortraitSlugs = { "Sylas", "Grom", "Thrain", "Elara" };

        // The one file allowed to spell the folder literal.
        private const string PathsDeclarationFile = "HeroPortraitPaths.cs";

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

            // ── (1) Hero card art — resolves through the REAL load path ──────────
            // Mirror PortraitCache.Build EXACTLY: Sprite first, then Texture2D wrap.
            foreach (var slug in PortraitSlugs)
            {
                string path = DeNelle.Core.HeroPortraitPaths.ResourceKey(slug);
                bool resolved = ResolvesLikePortraitCache(path);
                log.AppendLine($"portrait '{path}' resolves = {resolved}");
                if (!resolved)
                    failures.Add($"portrait '{path}' resolves to NULL — neither Sprite nor Texture2D at " +
                        $"Resources/{path}. PortraitCache.Get caches that null SILENTLY, so the hero card / " +
                        "dialogue speaker renders a placeholder with no error on screen.");
            }

            // ── (1b) WO-1234 [portrait-path-literals] — the folder is declared ONCE ───
            CheckPortraitPathLiterals(failures, log);

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

        /// <summary>
        /// WO-1234 — proves the hero-art folder segment is spelled in exactly ONE place.
        /// Sweeps every .cs under Assets/ for the QUOTED form of the folder name (a double
        /// quote immediately followed by the segment). That shape only ever occurs inside a
        /// string literal, so prose mentioning the folder in a comment stays legal and only
        /// re-typed PATHS fail. The needle is built from HeroPortraitPaths.ResourcesFolder,
        /// so this suite can never become another copy of the thing it guards.
        /// <para>PROVEN RED BEFORE THE FIX: 11 hits across 6 files — HeroSelectController x2,
        /// InventoryPaperDoll x2, InventoryUIBuilder x1, PortraitCache x1, WebGLTextureShrink
        /// x1, and THIS FILE x3 (the oracle was itself one of the copies).</para>
        /// </summary>
        private static void CheckPortraitPathLiterals(List<string> failures, StringBuilder log)
        {
            string assetsDir = Application.dataPath;   // <repo>/Assets
            if (string.IsNullOrEmpty(assetsDir) || !Directory.Exists(assetsDir))
            {
                failures.Add("portrait-path-literals: cannot resolve the project's Assets/ directory — " +
                             "the sweep could not run (NAMED SKIP, not a pass).");
                return;
            }

            string[] sources;
            try { sources = Directory.GetFiles(assetsDir, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("portrait-path-literals: could not enumerate .cs under Assets/ — " + ex.Message);
                return;
            }
            if (sources.Length == 0)
            {
                failures.Add("portrait-path-literals: found ZERO .cs under Assets/ — the sweep cannot " +
                             "have been meaningful.");
                return;
            }

            // ⛔ NEVER re-typed: the needle IS the constant.
            string needle = "\"" + DeNelle.Core.HeroPortraitPaths.ResourcesFolder;

            var hits = new List<string>();
            int scanned = 0;
            foreach (string file in sources)
            {
                if (string.Equals(Path.GetFileName(file), PathsDeclarationFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }
                scanned++;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf(needle, StringComparison.Ordinal) < 0) continue;
                    hits.Add(Rel(assetsDir, file) + ":" + (i + 1));
                }
            }

            log.AppendLine($"portrait-path-literals: swept {scanned} .cs for {needle} -> {hits.Count} hit(s)");
            if (hits.Count > 0)
                failures.Add($"portrait-path-literals: the hero-art folder literal is re-typed at {hits.Count} " +
                    $"site(s) outside {PathsDeclarationFile} — [{string.Join(", ", hits.ToArray())}]. Use " +
                    "DeNelle.Core.HeroPortraitPaths.ResourceKey(slug) (or .ResourcesFolder). A second copy rots " +
                    "independently and its failure mode is a SILENT null portrait, not an error.");
        }

        /// <summary>Assets-relative path for a hit, so the failure names a file a seat can open.</summary>
        private static string Rel(string assetsDir, string full)
        {
            try { return "Assets/" + full.Substring(assetsDir.Length).Replace('\\', '/').TrimStart('/'); }
            catch { return full; }
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
