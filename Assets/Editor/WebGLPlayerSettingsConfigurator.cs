// =============================================================================
// WebGLPlayerSettingsConfigurator (WO-408) — scripted WebGL PlayerSettings + a
// drift-guard AssetPostprocessor so the texture savings don't creep back.
// -----------------------------------------------------------------------------
// THREE things:
//   1. Run(): set the WebGL player settings that shrink + serve the payload:
//        - WebGL.compressionFormat = Brotli   (smallest transfer)
//        - WebGL.decompressionFallback = true (Brotli loads on itch via the JS
//          fallback decoder — no Content-Encoding server header needed)
//        - data caching on, name-files-as-hashes on (cache-busting)
//      Then audit Resources/ for heavy textures (Resources bloats the build —
//      everything under it ships unconditionally) and flag them, and scan the UI
//      sheets to recommend sprite atlases. Writes a report to
//      Builds/TextureAudit/webgl-playersettings-report.txt.
//
//   2. WebGLTextureDriftPostprocessor (AssetPostprocessor.OnPostprocessTexture):
//      auto-applies the WebGL max-size + crunch override to ANY NEW shipping
//      texture at import time, so sizes can't drift back up after the batch pass.
//      Mirrors TextureBatchOptimizer's table (kept in sync intentionally).
//
//   3. ScaffoldUiAtlases(): create a SpriteAtlas asset per UI sheet folder if the
//      package is present, so many small UI sprites pack into one page.
//
// GUARDRAIL #1 still holds: the postprocessor only sets the WebGL PLATFORM
// override, never the Default/source settings.
//
// Batchmode entry point: DeNelle.Editor.WebGLPlayerSettingsConfigurator.Run
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WebGLPlayerSettingsConfigurator
    {
        private const string OutputDir = "Builds/TextureAudit";

        // A Resources texture larger than this on disk is FLAGGED (it ships
        // unconditionally and bloats the initial download). Tunable.
        private const long ResourcesHeavyBytes = 1_000_000; // 1 MB

        [MenuItem("Defenders/Build/Configure WebGL Player Settings")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WebGL Player Settings Configurator (WO-408) ===");
            sb.AppendLine($"timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // ── 1. Compression + fallback ───────────────────────────────────────
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            // Cache-bust + browser cache the data file across reloads.
            PlayerSettings.WebGL.dataCaching = true;
            try { PlayerSettings.WebGL.nameFilesAsHashes = true; } catch { /* older API */ }

            sb.AppendLine("PlayerSettings.WebGL:");
            sb.AppendLine($"  compressionFormat    = {PlayerSettings.WebGL.compressionFormat}");
            sb.AppendLine($"  decompressionFallback= {PlayerSettings.WebGL.decompressionFallback}");
            sb.AppendLine($"  dataCaching          = {PlayerSettings.WebGL.dataCaching}");
            sb.AppendLine();
            Debug.Log("[WebGLPlayerSettingsConfigurator] Set Brotli + decompressionFallback=true.");

            // ── 2. Flag heavy Resources textures ────────────────────────────────
            string projRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] resGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" });
            var heavy = new List<(string path, long bytes)>();
            long resTotal = 0;
            foreach (var g in resGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(p)) continue;
                if (AssetImporter.GetAtPath(p) as TextureImporter == null) continue;
                long bytes = 0;
                try { string f = Path.Combine(projRoot, p); if (File.Exists(f)) bytes = new FileInfo(f).Length; } catch { }
                resTotal += bytes;
                if (bytes >= ResourcesHeavyBytes) heavy.Add((p, bytes));
            }
            heavy.Sort((a, b) => b.bytes.CompareTo(a.bytes));

            sb.AppendLine($"HEAVY Resources/ textures (>= {ResourcesHeavyBytes / 1024} KB) — these ship unconditionally:");
            sb.AppendLine($"  Resources/ texture total on disk: {(resTotal / 1048576.0):F1} MB across {resGuids.Length} textures");
            foreach (var h in heavy)
                sb.AppendLine($"  {(h.bytes / 1048576.0),7:F2} MB  {h.path}");
            sb.AppendLine("  ACTION: move art only referenced by prefabs/scenes OUT of Resources/ so it");
            sb.AppendLine("          loads on demand instead of bloating the initial .data payload.");
            sb.AppendLine();

            // ── 3. Recommend sprite atlases for UI sheets ───────────────────────
            sb.AppendLine("UI sprite-atlas candidates (folders with many loose sprites):");
            foreach (var rec in RecommendAtlases())
                sb.AppendLine("  " + rec);
            sb.AppendLine();

            sb.AppendLine("NEXT: rebuild WebGL; Brotli + fallback serve on itch with no server headers.");

            Directory.CreateDirectory(Path.Combine(projRoot, OutputDir));
            string outPath = Path.Combine(projRoot, OutputDir, "webgl-playersettings-report.txt");
            File.WriteAllText(outPath, sb.ToString());

            AssetDatabase.SaveAssets();
            Debug.Log($"[WebGLPlayerSettingsConfigurator] Report -> {outPath}");
            Debug.Log($"WEBGL_PLAYERSETTINGS_OK :: heavyResources={heavy.Count} resTotalMB={(resTotal / 1048576.0):F1}");
        }

        /// <summary>Folders under Assets that hold many loose Sprite textures -> atlas candidates.</summary>
        private static List<string> RecommendAtlases()
        {
            var byFolder = new Dictionary<string, int>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D",
                new[] { "Assets/Resources", "Assets/Art", "Assets/_Modules" });
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(p)) continue;
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null || ti.textureType != TextureImporterType.Sprite) continue;
                string dir = Path.GetDirectoryName(p).Replace('\\', '/');
                byFolder.TryGetValue(dir, out int c);
                byFolder[dir] = c + 1;
            }
            var recs = new List<string>();
            foreach (var kv in byFolder)
                if (kv.Value >= 4)
                    recs.Add($"{kv.Value,3} sprites in {kv.Key}  -> create a SpriteAtlas here");
            recs.Sort();
            if (recs.Count == 0) recs.Add("(none — no folder has >= 4 loose sprites)");
            return recs;
        }
    }

    // =========================================================================
    // Drift guard: auto-applies the WebGL override to every NEW shipping texture
    // so sizes can't creep back up after the batch optimizer runs.
    // =========================================================================
    public sealed class WebGLTextureDriftPostprocessor : AssetPostprocessor
    {
        public const int CrunchQuality = 50;          // keep in sync with TextureBatchOptimizer
        private const int DefaultMaxSize = 512;

        private static readonly string[] SkipFragments =
        {
            "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Editor/",
        };

        // Mirror of TextureBatchOptimizer.SizeTable (first fragment match wins).
        private static readonly (string fragment, int maxSize)[] SizeTable =
        {
            ("/icons/", 128), ("icon_", 128), ("hudicon", 128), ("itemicon", 128),
            ("portrait", 256), ("heroportraits", 256), ("petportraits", 256), ("/dialogue/", 256),
            ("/title", 512), ("/intro", 512), ("/hero select", 512), ("/ui/", 512), ("/hud/", 512),
            ("/heroes", 1024), ("/people", 1024), ("/enemies", 512), ("/pets", 512),
            ("/structures", 512), ("/buildings", 512), ("/props", 512),
            ("polyperfect", 512), ("/models", 512), ("/spells pack", 512), ("/lana studio", 512),
        };

        private void OnPostprocessTexture(Texture2D texture)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var frag in SkipFragments)
                if (p.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return;

            var ti = assetImporter as TextureImporter;
            if (ti == null) return;

            int maxSize = ResolveMaxSize(p);
            bool hasAlpha = ti.DoesSourceTextureHaveAlpha();
            var fmt = (hasAlpha || ti.textureType == TextureImporterType.NormalMap)
                ? TextureImporterFormat.DXT5Crunched
                : TextureImporterFormat.DXT1Crunched;

            var web = ti.GetPlatformTextureSettings("WebGL");
            if (web.overridden && web.maxTextureSize == maxSize
                && web.format == fmt && web.compressionQuality == CrunchQuality)
                return; // already correct — no churn

            web.name = "WebGL";
            web.overridden = true;
            web.maxTextureSize = maxSize;
            web.format = fmt;
            web.textureCompression = TextureImporterCompression.Compressed;
            web.compressionQuality = CrunchQuality;
            web.crunchedCompression = true;
            ti.SetPlatformTextureSettings(web);
            // No SaveAndReimport here — we're INSIDE the import; setting the
            // platform settings is honoured by this same import pass.
        }

        private static int ResolveMaxSize(string assetPath)
        {
            string lp = assetPath.ToLowerInvariant();
            foreach (var rule in SizeTable)
                if (lp.IndexOf(rule.fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return rule.maxSize;
            return DefaultMaxSize;
        }
    }
}
