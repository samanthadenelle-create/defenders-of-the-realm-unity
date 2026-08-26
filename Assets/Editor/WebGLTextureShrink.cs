using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Shrinks the WebGL build payload by applying a WebGL-only platform texture
    /// override (per-folder maxTextureSize + Compressed) to every texture that
    /// SHIPS in the build. The DEFAULT importer settings are left untouched, so
    /// the Windows / desktop build keeps full-resolution art — only the WebGL
    /// platform variant is shrunk.
    ///
    /// Scope (anything that bundles into the WebGL player):
    ///   - Assets/Resources/**                         (Resources.Load — always ships)
    ///   - Assets/_Modules/**/Resources/**             (module Resources — always ships)
    ///   - Assets/polyperfect/**                        (world atlases pulled in by the
    ///                                                    village _M prefabs — ship via refs)
    ///   - Assets/Models/**                             (People/Peasant/Merchant/etc. char
    ///                                                    textures — ~2.4-5.5 MB each, ship
    ///                                                    via prefab/FBX refs; MISSED before)
    ///   - Assets/Spells Pack/**                        (spell VFX — referenced by VFXManager
    ///                                                    / VFXCatalog; some 6-12 MB normals)
    ///   - Assets/Lana Studio/**                        (Casual RPG VFX prefabs — ship via refs)
    ///   ( /Demo and /Examples subfolders are skipped — that art never ships. )
    ///
    /// Per-folder maxSize map (FolderRules below, tunable):
    ///   - UI / portraits / title / intro / dialogue → 512
    ///   - models / world / structures / atlases     → 1024
    ///   - default (no match)                         → 1024
    ///
    /// All overrides: format Automatic, Compressed, compressionQuality 50,
    /// overridden = true, then SaveAndReimport.
    ///
    /// Why this matters: the build report shows TEXTURES = ~191 MB (61.6%) across
    /// ~280 textures project-wide. The prior 3-folder version only saved ~16 MB.
    /// A 4096 RGBA texture re-imported at 1024 Compressed is ~30-60x smaller; a UI
    /// portrait at 512 is smaller still.
    ///
    /// Idempotent: re-running just re-asserts the same WebGL override.
    /// Batchmode entry point: DeNelle.Editor.WebGLTextureShrink.Apply
    /// </summary>
    public static class WebGLTextureShrink
    {
        // Root folders to scan recursively. Project-relative. Any that don't exist
        // on a given clone are skipped with a warning.
        private static readonly string[] RootFolders =
        {
            "Assets/Resources",
            "Assets/polyperfect",
            "Assets/Models",        // People/Peasant/Merchant/Blacksmith/Orc char textures (2.4-5.5 MB each)
            "Assets/Spells Pack",   // spell VFX (VFXManager/VFXCatalog refs) — large normals
            "Assets/Lana Studio",   // Casual RPG VFX prefabs ship via refs
            // _Modules has many Resources/ subfolders — discovered dynamically below.
        };

        // Path fragments that mark NON-shipping demo / example art. Any texture whose
        // path contains one of these (case-insensitive) is skipped — those scenes
        // never bundle into the player, so shrinking them is wasted reimport churn.
        private static readonly string[] SkipFragments =
        {
            "/Demo/",
            "/Demos/",
            "/Example/",
            "/Examples/",
        };

        // The parent under which every module's Resources/ folder lives.
        private const string ModulesRoot = "Assets/_Modules";

        // Per-folder maxSize map: if a texture's asset path CONTAINS the fragment
        // (case-insensitive), it uses that maxSize. First match wins, so order
        // matters — put the more specific UI fragments first. Default if no match.
        private static readonly (string fragment, int maxSize)[] FolderRules =
        {
            // ── UI / portraits / dialogue → 256 (web; small on-screen) ──────────
            // WO-1234: folder name from the ONE constant. ⚠ the 2026-08-26 art is a CARD with a
            // baked name plate — 256 px is legible on a phone portrait but marginal for the plate.
            // Owner call if WebGL ships this screen; unchanged here.
            (DeNelle.Core.HeroPortraitPaths.ResourcesFolder, 256),
            ("PetPortraits",  256),
            ("Portraits",     256),
            ("Dialogue",      256),
            ("/Title",        512),   // title splash is full-screen — keep a bit higher
            ("/Intro",        512),

            // ── heroes → 512 (.data still too big at 1024; 512 is fine for web) ─
            ("Heroes",        512),
            ("Enemies",       512),
            ("Structures",    512),
            ("polyperfect",   512), // world atlases — 512 is plenty for stylized low-poly
        };

        private const int DefaultMaxSize = 512;   // web default — aggressive; desktop unaffected (WebGL-only override)

        private const string WebGLPlatform     = "WebGL";
        private const int    CompressionQuality = 50;

        [MenuItem("Defenders/Build/Shrink Textures for WebGL")]
        public static void Apply()
        {
            // Build the full set of scan folders: static roots + every Resources/
            // folder discovered under Assets/_Modules.
            var scanFolders = new List<string>();
            foreach (var root in RootFolders)
            {
                if (AssetDatabase.IsValidFolder(root))
                    scanFolders.Add(root);
                else
                    Debug.LogWarning($"[WebGLTextureShrink] Skipping missing root: {root}");
            }

            foreach (var modRes in FindModuleResourcesFolders())
                scanFolders.Add(modRes);

            if (scanFolders.Count == 0)
            {
                Debug.LogWarning("[WebGLTextureShrink] No scan folders found — nothing to do.");
                return;
            }

            // De-dup texture GUIDs across overlapping roots (a folder could be a
            // child of another root in theory). Accumulate per-maxSize counts.
            var seen = new HashSet<string>();

            int  totalFound      = 0;
            int  totalOverridden = 0;
            long totalSourceBytes = 0;

            var perSizeCount = new SortedDictionary<int, int>();
            var perSizeBytes = new SortedDictionary<int, long>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var folder in scanFolders)
                {
                    string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });

                    foreach (string guid in guids)
                    {
                        if (!seen.Add(guid))
                            continue; // already processed via another root

                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path))
                            continue;

                        if (IsSkippedPath(path))
                            continue; // demo / example art — never ships

                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null)
                            continue; // not a TextureImporter (render texture, etc.) — skip

                        totalFound++;

                        int maxSize = ResolveMaxSize(path);

                        if (!perSizeCount.ContainsKey(maxSize))
                        {
                            perSizeCount[maxSize] = 0;
                            perSizeBytes[maxSize] = 0L;
                        }

                        long len = 0L;
                        try
                        {
                            string fullPath = Path.GetFullPath(path);
                            if (File.Exists(fullPath))
                                len = new FileInfo(fullPath).Length;
                        }
                        catch { /* size accounting only — non-fatal */ }

                        totalSourceBytes      += len;
                        perSizeBytes[maxSize] += len;

                        // WebGL PLATFORM OVERRIDE — leaves Default (desktop) intact.
                        var s = importer.GetPlatformTextureSettings(WebGLPlatform);
                        s.overridden         = true;
                        s.maxTextureSize     = maxSize;
                        s.format             = TextureImporterFormat.Automatic;
                        s.textureCompression = TextureImporterCompression.Compressed;
                        s.compressionQuality = CompressionQuality;
                        importer.SetPlatformTextureSettings(s);
                        importer.SaveAndReimport();

                        totalOverridden++;
                        perSizeCount[maxSize]++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            // ── Summary ───────────────────────────────────────────────────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[WebGLTextureShrink] Done — WebGL platform override " +
                          $"(Compressed, q{CompressionQuality}, format Automatic).");
            sb.AppendLine($"  Scan folders:              {scanFolders.Count}");
            sb.AppendLine($"  Total textures found:      {totalFound}");
            sb.AppendLine($"  Total textures overridden: {totalOverridden}");
            sb.AppendLine($"  Total SOURCE size (before reimport): {Mb(totalSourceBytes)} MB");
            sb.AppendLine("  Per maxSize bucket:");
            foreach (var kv in perSizeCount)
            {
                sb.AppendLine($"    maxSize {kv.Key}: {kv.Value} textures, " +
                              $"{Mb(perSizeBytes[kv.Key])} MB source");
            }
            sb.Append("  NOTE: WebGL.data shrink is realised at BUILD time (the override applies " +
                      "to the WebGL player variant). Desktop/Windows build is unaffected.");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Finds every "Resources" folder under Assets/_Modules so module-bundled
        /// textures (HeroPortraits, PetPortraits, Intro, etc.) are covered.
        /// </summary>
        private static List<string> FindModuleResourcesFolders()
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(ModulesRoot))
            {
                Debug.LogWarning($"[WebGLTextureShrink] Skipping missing root: {ModulesRoot}");
                return result;
            }

            // GetSubFolders is recursive only one level, so walk the tree.
            var stack = new Stack<string>();
            stack.Push(ModulesRoot);
            while (stack.Count > 0)
            {
                string cur = stack.Pop();
                foreach (var sub in AssetDatabase.GetSubFolders(cur))
                {
                    if (Path.GetFileName(sub) == "Resources")
                        result.Add(sub);
                    stack.Push(sub);
                }
            }
            return result;
        }

        /// <summary>True if the asset path is non-shipping demo/example art (skip it).</summary>
        private static bool IsSkippedPath(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var frag in SkipFragments)
            {
                if (p.IndexOf(frag, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Resolves the WebGL maxTextureSize for an asset path via FolderRules.</summary>
        private static int ResolveMaxSize(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var rule in FolderRules)
            {
                if (p.IndexOf(rule.fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return rule.maxSize;
            }
            return DefaultMaxSize;
        }

        private static string Mb(long bytes)
        {
            return (bytes / 1048576.0).ToString("F1");
        }
    }
}
