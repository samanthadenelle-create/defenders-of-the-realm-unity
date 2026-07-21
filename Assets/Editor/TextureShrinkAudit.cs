// =============================================================================
// TextureShrinkAudit (APK size pass) -- shrink the ANDROID texture payload that
// dominates the APK. Measured: assets/bin/Data (~1.3 GB uncompressed) is ~95% of
// the 462 MB APK, sourced almost entirely from the art packs -- KayKit (1.6 GB on
// disk), polyperfect (473 MB), Lana Studio VFX (125 MB), Leohpaz (25 MB), and
// Resources/Heroes (81 MB).
// -----------------------------------------------------------------------------
// WHY A NEW TOOL (not a reuse of the WebGL tools). The existing size tools --
// WebGLTextureShrink / TextureBatchOptimizer -- write overrides under the *WebGL*
// build target (NamedBuildTarget.WebGL) using DXT-crunched formats. Those are
// no-ops for an Android/APK build, which reads the *Android* platform override and
// wants ASTC. This tool is the Android sibling: same proven scan/idempotency/
// module-Resources-discovery patterns (borrowed from TextureBatchOptimizer +
// TextureAuditTool), but every override is written under NamedBuildTarget.Android
// with ASTC formats. It touches ONLY the Android platform override on git-tracked
// .meta files -- the Default (desktop/editor) import settings and the source art
// are never modified, so every change is fully revertable and lossless.
//
// TWO batchmode-callable entry points:
//   DeNelle.Editor.TextureShrinkAudit.Report   -- DRY RUN, changes NOTHING.
//       Scans every Texture2D under the pack roots + Resources, buckets by
//       category, prints the current vs proposed Android settings + a rough
//       estimated saving, writes Builds/texture-shrink-report.txt, ends with the
//       marker TEX_AUDIT_REPORT_OK.
//   DeNelle.Editor.TextureShrinkAudit.Apply    -- applies the per-category rules
//       (Android ASTC + maxSize cap + crunch-where-safe), idempotent, ends with
//       the marker  TEX_SHRINK_APPLY_OK changed=<n> skipped=<n>.
//
// SAFETY INVARIANTS:
//   * NEVER upscales. targetMax = Min(importer.maxTextureSize, category.maxSize),
//     so the importer cap only ever moves DOWN (or stays). A 512 source under a
//     1024 category rule stays 512 -- it is never raised.
//   * The HERO stays the star: Resources/Heroes uses maxSize 2048 (only capped if
//     currently larger, never reduced below 2048 -- guaranteed by the Min() above)
//     with high-quality ASTC_4x4 and NO crunch on the albedo. Preserves fidelity.
//   * Normal maps keep their NormalMap type and are never crunched (crunch wrecks
//     tangent-space normals) -- ASTC without crunch.
//   * Skips lightmaps and already-tiny textures (<= 256). UI/icons stay legible
//     (generous 1024 cap, no aggressive crunch).
//   * Every per-texture importer op is wrapped in try/catch: a bad asset is logged
//     and skipped, it never aborts the run.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;   // NamedBuildTarget.Android -- version-safe platform token
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TextureShrinkAudit
    {
        // =====================================================================
        //  TUNABLES
        // =====================================================================

        // The Android build target's platform-override token ("Android"), resolved
        // version-safely rather than hardcoded. All GetPlatformTextureSettings /
        // SetPlatformTextureSettings / TextureImporterPlatformSettings.name use it.
        private static readonly string AndroidPlatform = NamedBuildTarget.Android.TargetName;

        // Report output (relative to the project root).
        private const string ReportRelPath = "Builds/texture-shrink-report.txt";

        // Progress log cadence for Apply().
        private const int ProgressEvery = 100;

        // Textures at or below this (imported) size are left alone -- already tiny,
        // shrinking them saves nothing and risks legibility.
        private const int TinySkipMaxDim = 256;

        // The scan roots. Missing roots are skipped with a warning (packs like
        // polyperfect are gitignored and absent on some clones). Every module
        // Resources/ folder is added dynamically (see FindModuleResourcesFolders).
        private static readonly string[] RootFolders =
        {
            "Assets/Models/KayKit",   // the big target -- dungeon/env props
            "Assets/polyperfect",     // low-poly shared atlases
            "Assets/Lana Studio",     // Casual RPG VFX particle textures
            "Assets/Leohpaz",         // FX pack
            "Assets/Spells Pack",     // 292 MB shipped VFX textures -- MANY at 8192 (biggest hog)
            "Assets/Hovl Studio",     // 89 MB shipped VFX textures -- shipping RGBA32
            "Assets/Resources",       // Heroes (hero art) + UI icons + misc shipped art
        };

        private const string ModulesRoot = "Assets/_Modules";

        // Non-shipping / editor-only art -- never touched.
        private static readonly string[] SkipFragments =
        {
            "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Editor/",
        };

        // Categories, most conservative first in intent. Detection is by asset path
        // (case-insensitive) plus texture-type -- see ResolveCategory.
        private enum TexCat { Hero, KayKit, Polyperfect, LanaVfx, Leohpaz, Vfx, Ui, Default }

        // Per-category rule. maxSize is a CAP (only ever lowers the importer max --
        // see targetMax). format is the Android ASTC variant. crunch/crunchQuality
        // apply only where the flag is set (and only on formats that support crunch;
        // ASTC ignores it harmlessly -- setting it is safe either way).
        private struct CatRule
        {
            public int MaxSize;
            public TextureImporterFormat Format;
            public bool Crunch;
            public int CrunchQuality;

            public CatRule(int maxSize, TextureImporterFormat format, bool crunch, int crunchQuality)
            {
                MaxSize = maxSize;
                Format = format;
                Crunch = crunch;
                CrunchQuality = crunchQuality;
            }
        }

        // PER-CATEGORY RULES (conservative where it shows, aggressive where it does not):
        //   Hero        -> 2048, ASTC_4x4 (high quality), NO crunch -- the hero is the star.
        //   KayKit      -> 1024, ASTC_6x6, crunch q50 -- aggressive; dungeon/env props.
        //   Polyperfect -> 512,  ASTC_6x6, crunch q50 -- small shared low-poly atlases.
        //   LanaVfx     -> 512,  ASTC_6x6, crunch q50 -- VFX tolerates crunch.
        //   Leohpaz     -> 1024, ASTC_6x6, crunch q50.
        //   Vfx         -> 1024, ASTC_6x6, crunch q50 -- Spells Pack (many 8192!) + Hovl
        //                  Studio + any Resources /vfx/. The single biggest win: caps the
        //                  8192 Spells Pack textures down to 1024. VFX tolerates crunch.
        //   Ui          -> 1024, ASTC_6x6, NO crunch  -- legibility > size.
        //   Default     -> 1024, ASTC_6x6, NO crunch  -- unmatched shipped art, conservative.
        private static readonly Dictionary<TexCat, CatRule> Rules = new Dictionary<TexCat, CatRule>
        {
            { TexCat.Hero,        new CatRule(2048, TextureImporterFormat.ASTC_4x4, false, 100) },
            { TexCat.KayKit,      new CatRule(1024, TextureImporterFormat.ASTC_6x6, true,  50)  },
            { TexCat.Polyperfect, new CatRule(512,  TextureImporterFormat.ASTC_6x6, true,  50)  },
            { TexCat.LanaVfx,     new CatRule(512,  TextureImporterFormat.ASTC_6x6, true,  50)  },
            { TexCat.Leohpaz,     new CatRule(1024, TextureImporterFormat.ASTC_6x6, true,  50)  },
            { TexCat.Vfx,         new CatRule(1024, TextureImporterFormat.ASTC_6x6, true,  50)  },
            { TexCat.Ui,          new CatRule(1024, TextureImporterFormat.ASTC_6x6, false, 50)  },
            { TexCat.Default,     new CatRule(1024, TextureImporterFormat.ASTC_6x6, false, 50)  },
        };

        // Approximate bytes-per-pixel for the ASTC formats we emit (for the rough
        // in-build size estimate only). ASTC_4x4 = 8.0 bpp, ASTC_6x6 = 3.56 bpp.
        private static double AstcBytesPerPixel(TextureImporterFormat fmt)
        {
            switch (fmt)
            {
                case TextureImporterFormat.ASTC_4x4: return 8.0 / 8.0;    // 1.000 B/px
                case TextureImporterFormat.ASTC_6x6: return 3.56 / 8.0;   // 0.445 B/px
                case TextureImporterFormat.ASTC_8x8: return 2.0 / 8.0;    // 0.250 B/px
                default:                             return 4.0;          // RGBA32 fallback
            }
        }

        // =====================================================================
        //  (1) REPORT -- dry run, changes nothing
        // =====================================================================

        [MenuItem("Defenders/Build/Texture Shrink -- Report (Android, dry run)")]
        public static void Report()
        {
            var folders = ResolveScanFolders();
            if (folders.Count == 0)
            {
                Debug.LogWarning("[TextureShrinkAudit] No scan folders found -- nothing to report.");
                return;
            }

            string projRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var seen = new HashSet<string>();

            // Per-category accumulators.
            var count       = new Dictionary<TexCat, int>();
            var onDiskBytes = new Dictionary<TexCat, long>();
            var estBefore   = new Dictionary<TexCat, double>();
            var estAfter    = new Dictionary<TexCat, double>();
            foreach (TexCat c in Enum.GetValues(typeof(TexCat)))
            {
                count[c] = 0; onDiskBytes[c] = 0L; estBefore[c] = 0.0; estAfter[c] = 0.0;
            }

            int totalScanned = 0, totalSkipped = 0;

            foreach (var folder in folders)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    if (!seen.Add(guid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    if (IsSkippedPath(path)) { totalSkipped++; continue; }

                    try
                    {
                        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (ti == null) { totalSkipped++; continue; }

                        // Imported dimension = current effective size (already capped
                        // by the current maxTextureSize). Used for the estimate and to
                        // guarantee we never propose an upscale.
                        int curDim = ImportedMaxDim(path, ti);
                        if (IsTiny(ti, curDim)) { totalSkipped++; continue; }

                        TexCat cat = ResolveCategory(path, ti);
                        CatRule rule = Rules[cat];

                        int targetMax = Math.Min(ti.maxTextureSize, rule.MaxSize); // never upscales
                        int proposedDim = Math.Min(curDim, targetMax);

                        var fmt = FormatFor(cat, ti);
                        double bpp = AstcBytesPerPixel(fmt);
                        double before = (double)curDim * curDim * bpp * 1.3333;      // +mips
                        double after  = (double)proposedDim * proposedDim * bpp * 1.3333;

                        count[cat]++;
                        onDiskBytes[cat] += OnDiskBytes(path, projRoot);
                        estBefore[cat]  += before;
                        estAfter[cat]   += after;
                        totalScanned++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[TextureShrinkAudit] Report: skipped '{path}' -- {e.Message}");
                        totalSkipped++;
                    }
                }
            }

            // -- Build the report --------------------------------------------
            var sb = new StringBuilder();
            sb.AppendLine("=== Texture Shrink Audit -- Android APK size pass (DRY RUN) ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Android platform token: {AndroidPlatform}");
            sb.AppendLine($"Scan folders: {folders.Count}   Textures scanned: {totalScanned}   Skipped (demo/editor/tiny/lightmap): {totalSkipped}");
            sb.AppendLine();
            sb.AppendLine("Per-category plan (est. sizes are in-build ASTC bytes incl. mips -- rough):");
            sb.AppendLine(string.Format("{0,-12} {1,6} {2,10} {3,-11} {4,8} {5,6} {6,10} {7,10} {8,9}",
                "category", "count", "onDiskMB", "format", "maxCap", "crunch", "estBefMB", "estAftMB", "saveMB"));

            double totBefore = 0, totAfter = 0; long totDisk = 0; int totCount = 0;
            foreach (TexCat c in Enum.GetValues(typeof(TexCat)))
            {
                if (count[c] == 0) continue;
                var rule = Rules[c];
                double bMB = estBefore[c] / 1048576.0;
                double aMB = estAfter[c] / 1048576.0;
                sb.AppendLine(string.Format("{0,-12} {1,6} {2,10:F1} {3,-11} {4,8} {5,6} {6,10:F1} {7,10:F1} {8,9:F1}",
                    c, count[c], onDiskBytes[c] / 1048576.0, ShortFmt(rule.Format), rule.MaxSize,
                    (c == TexCat.Hero ? "no" : (rule.Crunch ? "yes" : "no")),
                    bMB, aMB, bMB - aMB));
                totBefore += estBefore[c]; totAfter += estAfter[c];
                totDisk += onDiskBytes[c]; totCount += count[c];
            }

            sb.AppendLine();
            sb.AppendLine($"TOTAL textures: {totCount}");
            sb.AppendLine($"TOTAL on-disk source: {totDisk / 1048576.0:F1} MB");
            sb.AppendLine($"TOTAL est in-build (ASTC) before: {totBefore / 1048576.0:F1} MB");
            sb.AppendLine($"TOTAL est in-build (ASTC) after:  {totAfter / 1048576.0:F1} MB");
            sb.AppendLine($"TOTAL est resolution-cap saving:  {(totBefore - totAfter) / 1048576.0:F1} MB");
            sb.AppendLine();
            sb.AppendLine("NOTES:");
            sb.AppendLine("  * 'saveMB' isolates the maxSize (resolution) cap at a fixed ASTC format,");
            sb.AppendLine("    so it is a floor. ASTC itself compresses ~4x-16x vs uncompressed RGBA,");
            sb.AppendLine("    an ADDITIONAL win realised at APK build time (Android override).");
            sb.AppendLine("  * Only the Android platform override changes; Default (desktop) + source");
            sb.AppendLine("    art are untouched. All changes are to git-tracked .meta -- fully revertable.");
            sb.AppendLine("  * NEVER upscales: targetMax = Min(importer.max, category.max). Hero stays >= 2048, ASTC_4x4, no crunch.");

            string outPath = WriteReport(projRoot, sb.ToString());
            Debug.Log(sb.ToString());
            Debug.Log($"[TextureShrinkAudit] Report written -> {outPath}");
            Debug.Log("TEX_AUDIT_REPORT_OK");
        }

        // =====================================================================
        //  (2) APPLY -- writes Android ASTC overrides, idempotent
        // =====================================================================

        [MenuItem("Defenders/Build/Texture Shrink -- Apply (Android ASTC)")]
        public static void Apply()
        {
            var folders = ResolveScanFolders();
            if (folders.Count == 0)
            {
                Debug.LogWarning("[TextureShrinkAudit] No scan folders found -- nothing to apply.");
                Debug.Log("TEX_SHRINK_APPLY_OK changed=0 skipped=0");
                return;
            }

            var seen = new HashSet<string>();
            int changed = 0, skipped = 0, processed = 0;
            var perCatChanged = new Dictionary<TexCat, int>();
            foreach (TexCat c in Enum.GetValues(typeof(TexCat))) perCatChanged[c] = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var folder in folders)
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                    {
                        if (!seen.Add(guid)) continue;
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;
                        if (IsSkippedPath(path)) { skipped++; continue; }

                        try
                        {
                            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                            if (ti == null) { skipped++; continue; }

                            int curDim = ImportedMaxDim(path, ti);
                            if (IsTiny(ti, curDim)) { skipped++; continue; }

                            TexCat cat = ResolveCategory(path, ti);
                            CatRule rule = Rules[cat];

                            int targetMax = Math.Min(ti.maxTextureSize, rule.MaxSize); // never upscales
                            var fmt = FormatFor(cat, ti);
                            // Normal maps + explicitly no-crunch categories never crunch.
                            bool crunch = rule.Crunch && ti.textureType != TextureImporterType.NormalMap;
                            int quality = rule.CrunchQuality;

                            var s = ti.GetPlatformTextureSettings(AndroidPlatform);

                            bool alreadyOk = s.overridden
                                && s.maxTextureSize == targetMax
                                && s.format == fmt
                                && s.crunchedCompression == crunch;
                            if (alreadyOk) { skipped++; processed++; continue; }

                            s.name = AndroidPlatform;
                            s.overridden = true;
                            s.maxTextureSize = targetMax;
                            s.format = fmt;
                            s.textureCompression = TextureImporterCompression.Compressed;
                            s.crunchedCompression = crunch;
                            s.compressionQuality = quality;
                            ti.SetPlatformTextureSettings(s);

                            // SaveAndReimport writes the modified importer to the
                            // .meta THEN reimports -- equivalent to writing settings
                            // + AssetDatabase.ImportAsset(path, ForceUpdate), and the
                            // persist-to-.meta step is REQUIRED for a
                            // SetPlatformTextureSettings change to actually stick.
                            ti.SaveAndReimport();

                            changed++;
                            perCatChanged[cat]++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[TextureShrinkAudit] Apply: skipped '{path}' -- {e.Message}");
                            skipped++;
                        }

                        processed++;
                        if (processed % ProgressEvery == 0)
                            Debug.Log($"[TextureShrinkAudit] ...processed {processed}  (changed {changed}, skipped {skipped})");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[TextureShrinkAudit] Apply done (Android override). changed={changed} skipped={skipped}");
            foreach (TexCat c in Enum.GetValues(typeof(TexCat)))
                if (perCatChanged[c] > 0) sb.AppendLine($"    {c}: {perCatChanged[c]} changed");
            Debug.Log(sb.ToString());
            Debug.Log($"TEX_SHRINK_APPLY_OK changed={changed} skipped={skipped}");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Static roots that exist + every module Resources/ folder.</summary>
        private static List<string> ResolveScanFolders()
        {
            var folders = new List<string>();
            foreach (var root in RootFolders)
            {
                if (AssetDatabase.IsValidFolder(root)) folders.Add(root);
                else Debug.LogWarning($"[TextureShrinkAudit] Skipping missing root: {root}");
            }
            foreach (var modRes in FindModuleResourcesFolders())
                if (!folders.Contains(modRes)) folders.Add(modRes);
            return folders;
        }

        /// <summary>Walks Assets/_Modules and returns every "Resources" folder.</summary>
        private static List<string> FindModuleResourcesFolders()
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(ModulesRoot)) return result;
            var stack = new Stack<string>();
            stack.Push(ModulesRoot);
            while (stack.Count > 0)
            {
                string cur = stack.Pop();
                foreach (var sub in AssetDatabase.GetSubFolders(cur))
                {
                    if (Path.GetFileName(sub) == "Resources") result.Add(sub);
                    stack.Push(sub);
                }
            }
            return result;
        }

        /// <summary>True if the path is non-shipping demo/example/editor art.</summary>
        private static bool IsSkippedPath(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var frag in SkipFragments)
                if (p.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// Category detection from the asset path (case-insensitive) + texture type.
        /// Pack roots map 1:1 by path fragment; within Resources we split Hero vs UI
        /// vs Default. First match wins.
        /// </summary>
        private static TexCat ResolveCategory(string assetPath, TextureImporter ti)
        {
            string p = assetPath.Replace('\\', '/').ToLowerInvariant();

            // Pack roots -- unambiguous by path.
            if (p.Contains("/models/kaykit")) return TexCat.KayKit;
            if (p.Contains("/polyperfect"))   return TexCat.Polyperfect;
            if (p.Contains("/lana studio"))   return TexCat.LanaVfx;
            if (p.Contains("/leohpaz"))       return TexCat.Leohpaz;

            // VFX roots -- the biggest shippable hogs. Spells Pack (many 8192!) + Hovl
            // Studio (RGBA32) are top-level packs; also any /vfx/ folder under Resources
            // not already caught above. maxSize cap 1024 + ASTC_6x6 + crunch = huge win.
            if (p.Contains("/spells pack") || p.Contains("/hovl studio") || p.Contains("/vfx/"))
                return TexCat.Vfx;

            // The hero is the star -- conservative rule (checked before the _Metallic /
            // UI heuristics so hero mask/metallic maps still stay 2048).
            if (p.Contains("/resources/heroes")) return TexCat.Hero;

            // Linear data maps (metallic/mask/roughness/AO) are NOT UI and must not be
            // treated as legibility-sensitive -- route to Default (ASTC, cap 1024). This
            // catches the huge Enemies/Blink *_Metallic.tga (5.33 MB RGBA32 today) even
            // though the "blink" UI heuristic below would otherwise grab them.
            if (p.Contains("_metallic") || p.Contains("_mask") || p.Contains("_roughness")
                || p.Contains("_ao") || p.Contains("_occlusion") || p.Contains("_specular"))
                return TexCat.Default;

            // UI / icons -- keep legible. Sprites + the known UI/icon folders. /rpgui
            // (the 362 MB NPOT RGBA32 pack) lands here -> ASTC handles NPOT, killing the
            // RGBA32 fallback. Enemies albedo (via "blink") also lands here -> ASTC 1024.
            if (ti != null && ti.textureType == TextureImporterType.Sprite) return TexCat.Ui;
            if (p.Contains("/hudicons") || p.Contains("/itemicons") || p.Contains("/projectileicons")
                || p.Contains("/rpgui") || p.Contains("icon") || p.Contains("/ui/") || p.Contains("/hud/")
                || p.Contains("blink") || p.Contains("obsidian") || p.Contains("portrait") || p.Contains("/dialogue/"))
                return TexCat.Ui;

            return TexCat.Default;
        }

        /// <summary>
        /// The Android format for a category, overriding to a non-crunch-friendly
        /// ASTC for normal maps (never crunch a normal map -- it wrecks the vectors).
        /// </summary>
        private static TextureImporterFormat FormatFor(TexCat cat, TextureImporter ti)
        {
            // All categories emit ASTC; the crunch flag (not the format) is what we
            // suppress for normal maps / no-crunch categories. Keep the per-category
            // ASTC block size as authored.
            return Rules[cat].Format;
        }

        /// <summary>Skip lightmaps + already-tiny textures.</summary>
        private static bool IsTiny(TextureImporter ti, int importedMaxDim)
        {
            if (ti.textureType == TextureImporterType.Lightmap) return true;
            if (importedMaxDim > 0 && importedMaxDim <= TinySkipMaxDim) return true;
            // Fallback when dimensions were unreadable: use the importer cap.
            if (importedMaxDim <= 0 && ti.maxTextureSize <= TinySkipMaxDim) return true;
            return false;
        }

        /// <summary>
        /// Largest imported dimension of the texture (width or height). Reflects the
        /// CURRENT maxTextureSize cap, so it is a safe upper bound for the proposed
        /// size (guarantees no upscale). Returns 0 if it cannot be loaded.
        /// </summary>
        private static int ImportedMaxDim(string path, TextureImporter ti)
        {
            try
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return Math.Max(tex.width, tex.height);
            }
            catch { /* fall through -- non-fatal */ }
            return 0;
        }

        private static long OnDiskBytes(string assetPath, string projRoot)
        {
            try
            {
                string full = Path.Combine(projRoot, assetPath);
                if (File.Exists(full)) return new FileInfo(full).Length;
            }
            catch { /* size accounting only -- non-fatal */ }
            return 0L;
        }

        private static string ShortFmt(TextureImporterFormat fmt)
        {
            string s = fmt.ToString();
            return s.StartsWith("ASTC_") ? s.Substring(5) + "(ASTC)" : s;
        }

        private static string WriteReport(string projRoot, string body)
        {
            string outPath = Path.Combine(projRoot, ReportRelPath);
            try
            {
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, body);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TextureShrinkAudit] Could not write report to {outPath}: {e.Message}");
            }
            return outPath;
        }
    }
}
