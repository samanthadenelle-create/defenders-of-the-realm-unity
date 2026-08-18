// =============================================================================
// TripoTextureImportCap — auto-caps texture size for newly-imported character
// art (heroes / companions / enemies / Tripo bodies) so a freshly-imported
// Tripo character doesn't re-bloat the build.
// -----------------------------------------------------------------------------
// Owner question 2026-06-06: the shipped heroes carried 4096px, 17-24 MB
// basecolor PNGs that bloated the WebGL build. Tripo / People-pipeline FBXs
// export huge source textures (and extract huge .fbm sidecar PNGs), and nothing
// in the import pipeline caps them — so every new hero/companion/enemy re-bloats
// the player build until someone manually runs the WebGLTextureShrink tool.
//
// This AssetPostprocessor closes that gap. On the FIRST import of any texture
// under a character-art folder it sets a sane default:
//
//   • maxTextureSize ......... 1024 (down from Tripo's 4096)
//   • textureCompression ..... Compressed (DXT/BCn on desktop, not Uncompressed)
//   • a WebGL platform override at 512 (the build that bloated)
//
// SCOPE (path-gated — assets outside these folders are never touched):
//   Assets/Models/People/        — heroes + companions + orcs (shared Tripo rig)
//   Assets/Resources/Heroes/     — Mage / Knight / Ranger / Cleric bodies + props
//   Assets/EnemyContent/    — KayKit skeletons + Dragon
//   …and every .fbm / *_tex / Textures sidecar nested under those folders,
//   which are covered automatically because their paths start with the prefix.
//
// FIRST-IMPORT GUARD (does NOT clobber manual overrides):
// The cap is applied only when `assetImporter.importSettingsMissing` is true —
// Unity reports this when the asset has no persisted .meta import settings yet,
// i.e. it is being imported for the very first time (fresh drop / fresh clone /
// Library rebuild after the .meta was deleted). Once the .meta exists (which it
// always does after the first import — and any time someone hand-tweaks the
// importer in the inspector), `importSettingsMissing` is false and this hook is
// a no-op, so it never fights settings a human has dialed in.
//
// NOT retroactive: this only affects NEW imports / reimports-from-missing-meta.
// Existing committed textures keep their current settings — shrinking those is
// the WebGLTextureShrink tool's job, deliberately left separate.
//
// ASSEMBLY: DeNelle.Editor (Editor-only). Uses only UnityEditor + UnityEngine.
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Caps texture size + forces compression on the first import of any texture
    /// under the project's character-art folders, with a WebGL override at 512,
    /// so newly-imported Tripo heroes/companions/enemies don't bloat the build.
    /// Guarded to only act on first import — never clobbers manual overrides.
    /// </summary>
    public sealed class TripoTextureImportCap : AssetPostprocessor
    {
        // ── Scope ────────────────────────────────────────────────────────────
        // Unity asset paths are always '/'. Prefix-match also covers the nested
        // .fbm / *_tex / Textures sidecar folders Tripo + the People pipeline
        // emit (e.g. Assets/Resources/Heroes/Mage.fbm/...).
        private static readonly string[] CharacterArtFolders =
        {
            "Assets/Models/People/",
            "Assets/Resources/Heroes/",
            DeNelle.Core.AssetRoots.EnemyContent + "/",
        };

        // ── Cap values ───────────────────────────────────────────────────────
        // Default (desktop / editor) cap — 1024 is plenty for a stylized
        // low-poly mobile character; Tripo ships 4096.
        private const int DefaultMaxSize = 1024;
        // WebGL was the build that bloated (17-24 MB basecolors) — cap harder.
        private const int WebGLMaxSize = 512;

        private void OnPreprocessTexture()
        {
            if (!IsCharacterArtPath(assetPath)) return;

            var ti = assetImporter as TextureImporter;
            if (ti == null) return;

            // FIRST-IMPORT GUARD: only act when there are no persisted import
            // settings yet. Once a .meta exists (always after first import, and
            // whenever a human edits the importer) this is false → no-op, so we
            // never override manual settings. See file header.
            if (!ti.importSettingsMissing) return;

            // Default-platform cap + compression.
            if (ti.maxTextureSize > DefaultMaxSize)
                ti.maxTextureSize = DefaultMaxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;

            // WebGL platform override at 512 — the build that bloated.
            var webgl = ti.GetPlatformTextureSettings("WebGL");
            webgl.overridden = true;
            webgl.maxTextureSize = WebGLMaxSize;
            webgl.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(webgl);

            Debug.Log($"[TripoTextureImportCap] Capped first-import texture {assetPath} " +
                      $"→ default {DefaultMaxSize}px / WebGL {WebGLMaxSize}px, Compressed.");
        }

        /// <summary>True if the asset path is under any character-art folder.</summary>
        private static bool IsCharacterArtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.Replace('\\', '/');
            foreach (var folder in CharacterArtFolders)
            {
                if (p.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
