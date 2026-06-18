// =============================================================================
// TextureBatchOptimizer (WO-408) — the core size-reduction pass. Applies a
// per-role MAX-SIZE TABLE + Crunch compression to every SHIPPING texture via a
// WebGL PLATFORM OVERRIDE, then reimports. Goal: Textures 203 MB -> < 60 MB.
// -----------------------------------------------------------------------------
// GUARDRAIL #1 (non-negotiable): WebGL platform overrides ONLY. The texture's
// Default/source import settings are NEVER touched — desktop/Windows + the source
// asset keep full resolution. We only set:
//     new TextureImporterPlatformSettings {
//         name = "WebGL", overridden = true,
//         maxTextureSize = <role>, format = <crunched>, compressionQuality = q }
//
// GUARDRAIL #2: a companion QA pass (TextureQAThumbnailCapture below, invoked at
// the end) renders a per-texture thumbnail PNG at the NEW WebGL settings into
// Builds/TextureAudit/QA/ so the owner can eyeball compression artifacts BEFORE
// shipping. Crunch artifacts hit per-texture; this catches them per-texture.
//
// MAX-SIZE TABLE (by role/path fragment, first match wins — order matters):
//   icon / tiny             -> 128
//   ui-sprite / portrait    -> 256
//   ui-background / title   -> 512
//   character / hero / enemy-> 1024  (down to 512 for the very heavy ones via path)
//   environment / props     -> 512
//   large background        -> 1024
//   default                 -> 512
//
// COMPRESSION: Crunch (DXT1 for opaque, DXT5 for alpha) via
// TextureImporterFormat.DXT1Crunched / DXT5Crunched, compressionQuality tunable
// (CrunchQuality const). Crunch gives the smallest .data payload on WebGL.
// Normal maps use DXT5Crunched (BC3) — fine for stylized low-poly.
//
// IDEMPOTENT: re-running re-asserts the same override; a texture already at the
// target size+format is logged as "unchanged" and skipped from reimport churn.
//
// Batchmode entry point: DeNelle.Editor.TextureBatchOptimizer.Run
// (capture QA only:        DeNelle.Editor.TextureBatchOptimizer.CaptureQAOnly)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;   // WO-408: NamedBuildTarget for the version-safe WebGL platform token
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TextureBatchOptimizer
    {
        // ── Tunables ────────────────────────────────────────────────────────────
        // Crunch quality 0..100. 50 is a good web default; raise toward 80 if the
        // QA thumbnails show artifacts on a hero/background, lower toward 30 to
        // squeeze further. Applies to all crunched formats below.
        public const int CrunchQuality = 50;

        // WO-408 DEFECT 1 (decisive fix): Unity 6 renamed the WebGL build target
        // platform string from "WebGL" to "Web". A hardcoded "WebGL" literal here
        // wrote per-texture overrides under a name the Unity 6 build pipeline IGNORES,
        // so every override was a silent no-op (textures shipped at full size).
        // NamedBuildTarget.WebGL.TargetName is the canonical, version-safe token
        // (resolves to the correct platform string for whatever editor version is
        // running) — used for GetPlatformTextureSettings / SetPlatformTextureSettings
        // / TextureImporterPlatformSettings.name at every call site below.
        private static readonly string WebGLPlatform = NamedBuildTarget.WebGL.TargetName;
        private const string QaSubDir = "Builds/TextureAudit/QA";

        // Root folders that hold SHIPPING art. Demo/Example/Editor subfolders are
        // skipped via SkipFragments. _Modules Resources/ folders discovered below.
        private static readonly string[] RootFolders =
        {
            "Assets/Resources",
            "Assets/polyperfect",
            "Assets/Models",
            "Assets/Art",
            "Assets/Spells Pack",
            "Assets/Lana Studio",
            "Assets/Quaternius",   // WO-408: gitignored pack — its texture metas drift / never got the WebGL override; scan it too (missing root is skipped with a warning)
            "Assets/Mirza Beig",   // WO-408: Ultimate VFX spritesheets are 8192² — the scariest single-texture payload. Was never in a scanned root, so the cap never reached them; add the root and cap the sheets to 2048 (see SizeTable).
        };

        private const string ModulesRoot = "Assets/_Modules";

        private static readonly string[] SkipFragments =
        {
            "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Editor/",
        };

        // MAX-SIZE TABLE. First fragment that matches the lowercased path wins, so
        // put the most specific / smallest buckets FIRST. Default applies on no match.
        private static readonly (string fragment, int maxSize)[] SizeTable =
        {
            // ── HUD icon FOLDER -> 512 (WO-408 §A) ──────────────────────────────
            // The shipped HUD icons (Resources/HudIcons/hud_quest.png, Elarion.png)
            // are authored at 1254² — overkill. WO-408 caps them to 512, NOT 128:
            // they render at HUD resolution where 128 would visibly soften the
            // medallion/clock chrome. This rule is listed BEFORE the generic
            // "hudicon" 128 bucket so the folder match wins (first match wins).
            ("/hudicons/",     512),

            // ── Mirza Beig Ultimate VFX spritesheets -> 2048 (WO-408 §B) ────────
            // 8192² sheets (explosion/smokeWisps/solarFlare/blob/liquid…) — one is
            // ~21 MB even ASTC. WO-408 caps them to 2048 (16× fewer pixels). The
            // whole pack lives under "Mirza Beig/" so a single path fragment
            // covers every sprite + spritesheet uniformly.
            ("/mirza beig/",   2048),

            // ── tiny icons -> 128 ───────────────────────────────────────────────
            ("/icons/",        128),
            ("icon_",          128),
            ("hudicon",        128),
            ("itemicon",       128),

            // ── UI sprites / portraits / dialogue -> 256 ────────────────────────
            ("portrait",       256),
            ("heroportraits",  256),
            ("petportraits",   256),
            ("/dialogue/",     256),

            // ── UI backgrounds / title / intro (full-screen) -> 512 ─────────────
            ("/title",         512),
            ("/intro",         512),
            ("/hero select",   512),
            ("/ui/",           512),
            ("/hud/",          512),

            // ── heroes / characters -> 1024 (key art, on-screen close) ──────────
            ("/heroes",        1024),
            ("/people",        1024),
            ("/enemies",       512),   // enemies seen at distance — 512 enough
            ("/pets",          512),

            // ── environment / props / world atlases -> 512 ──────────────────────
            ("/structures",    512),
            ("/buildings",     512),
            ("/props",         512),
            ("polyperfect",    512),
            ("/models",        512),
            ("/spells pack",   512),
            ("/lana studio",   512),
        };

        private const int DefaultMaxSize = 512;

        [MenuItem("Defenders/Build/Optimize Textures for WebGL (max-size + crunch)")]
        public static void Run()
        {
            var scanFolders = new List<string>();
            foreach (var root in RootFolders)
            {
                if (AssetDatabase.IsValidFolder(root)) scanFolders.Add(root);
                else Debug.LogWarning($"[TextureBatchOptimizer] Skipping missing root: {root}");
            }
            foreach (var modRes in FindModuleResourcesFolders())
                scanFolders.Add(modRes);

            if (scanFolders.Count == 0)
            {
                Debug.LogWarning("[TextureBatchOptimizer] No scan folders — nothing to do.");
                return;
            }

            var seen = new HashSet<string>();
            var changedPaths = new List<string>();
            int found = 0, changed = 0, unchanged = 0, skipped = 0;
            var perSize = new SortedDictionary<int, int>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var folder in scanFolders)
                {
                    string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                    foreach (string guid in guids)
                    {
                        if (!seen.Add(guid)) continue;

                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;
                        if (IsSkipped(path)) { skipped++; continue; }

                        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (ti == null) continue;

                        found++;
                        int maxSize = ResolveMaxSize(path);
                        if (!perSize.ContainsKey(maxSize)) perSize[maxSize] = 0;
                        perSize[maxSize]++;

                        // Choose a crunched format: DXT5Crunched if the source has
                        // alpha (or is a normal map), else DXT1Crunched.
                        bool hasAlpha = ti.DoesSourceTextureHaveAlpha();
                        var fmt = (hasAlpha || ti.textureType == TextureImporterType.NormalMap)
                            ? TextureImporterFormat.DXT5Crunched
                            : TextureImporterFormat.DXT1Crunched;

                        var web = ti.GetPlatformTextureSettings(WebGLPlatform);

                        bool alreadyApplied = web.overridden
                            && web.maxTextureSize == maxSize
                            && web.format == fmt
                            && web.compressionQuality == CrunchQuality;

                        if (alreadyApplied) { unchanged++; continue; }

                        // ── WebGL PLATFORM OVERRIDE — Default settings untouched ──
                        // Mutate the struct returned by GetPlatformTextureSettings
                        // (proven pattern in WebGLTextureShrink) and set name so the
                        // override binds to the WebGL platform explicitly.
                        web.name = WebGLPlatform;
                        web.overridden = true;
                        web.maxTextureSize = maxSize;
                        web.format = fmt;
                        web.textureCompression = TextureImporterCompression.Compressed;
                        web.compressionQuality = CrunchQuality;
                        web.crunchedCompression = true;
                        ti.SetPlatformTextureSettings(web);
                        ti.SaveAndReimport();

                        changed++;
                        if (changedPaths.Count < 60) changedPaths.Add($"  {maxSize,5} {fmt} {path}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[TextureBatchOptimizer] Done (crunch q{CrunchQuality}, WebGL override).");
            sb.AppendLine($"  Scan folders: {scanFolders.Count}");
            sb.AppendLine($"  Found: {found}  Changed: {changed}  Unchanged: {unchanged}  Skipped(demo): {skipped}");
            sb.AppendLine("  Per-maxSize bucket (count):");
            foreach (var kv in perSize) sb.AppendLine($"    {kv.Key}: {kv.Value}");
            sb.AppendLine("  First changes:");
            foreach (var c in changedPaths) sb.AppendLine(c);
            Debug.Log(sb.ToString());

            // GUARDRAIL #2 — visual QA thumbnails at the new settings.
            CaptureQA();

            Debug.Log($"TEXTURE_OPTIMIZE_OK :: changed={changed} unchanged={unchanged}");
        }

        /// <summary>Re-run only the QA thumbnail capture (no import changes).</summary>
        [MenuItem("Defenders/Build/Capture Texture QA Thumbnails")]
        public static void CaptureQAOnly() => CaptureQA();

        // ── Visual QA: write a thumbnail of each shipping texture AT its imported
        //    (post-override, in-editor preview) state so the owner can scan for
        //    crunch artifacts. We use AssetPreview / the imported Texture2D, scaled
        //    to a 256px thumbnail PNG, grouped by role folder for fast review. ──
        private static void CaptureQA()
        {
            string projRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string qaDir = Path.Combine(projRoot, QaSubDir);
            Directory.CreateDirectory(qaDir);

            var seen = new HashSet<string>();
            int written = 0;

            foreach (var root in RootFolders)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
                foreach (string guid in guids)
                {
                    if (!seen.Add(guid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || IsSkipped(path)) continue;

                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex == null) continue;

                    byte[] png = MakeThumbnailPng(tex, 256);
                    if (png == null) continue;

                    // Name: role__originalfilename.png so it sorts by role.
                    string role = ResolveMaxSize(path).ToString();
                    string fileSafe = path.Replace('\\', '/').Replace("Assets/", "")
                        .Replace('/', '_');
                    string outFile = Path.Combine(qaDir, $"{role}__{fileSafe}.png");
                    try { File.WriteAllBytes(outFile, png); written++; }
                    catch (Exception e) { Debug.LogWarning($"[TextureBatchOptimizer] QA write failed {outFile}: {e.Message}"); }
                }
            }

            Debug.Log($"[TextureBatchOptimizer] QA thumbnails written: {written} -> {qaDir}");
        }

        /// <summary>
        /// Renders <paramref name="tex"/> into a square thumbnail PNG of side
        /// <paramref name="size"/> via a Blit so we read back the IMPORTED (GPU)
        /// pixels — i.e. what the WebGL override produced — not the source file.
        /// </summary>
        private static byte[] MakeThumbnailPng(Texture2D tex, int size)
        {
            RenderTexture rt = null;
            RenderTexture prev = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(size, size, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                readable.Apply();
                return readable.EncodeToPNG();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TextureBatchOptimizer] thumbnail failed: {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
            }
        }

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

        private static bool IsSkipped(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var frag in SkipFragments)
                if (p.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static int ResolveMaxSize(string assetPath)
        {
            string p = assetPath.Replace('\\', '/').ToLowerInvariant();
            foreach (var rule in SizeTable)
                if (p.IndexOf(rule.fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return rule.maxSize;
            return DefaultMaxSize;
        }
    }
}
