// =============================================================================
// AssetImportPostprocessor — automatic KayKit import settings (Editor-only)
// -----------------------------------------------------------------------------
// An AssetPostprocessor that applies the v2 port-spec Part 7 ("Per-category
// import settings") automatically to every model and texture imported under
//
//     Assets/Models/KayKit/
//
// This means the KayKit Medieval / characters / weapons / anim packs (and later
// the Dungeon pack) get spec-correct import settings the moment Unity imports
// them — no hand-tweaking of dozens of GLTF/GLB files, no risk of a stray
// mesh shipping with Read/Write on or a 2048 texture atlas.
//
// WHY AN AssetPostprocessor (spec Part 7, last line):
//   "The agent runs Unity's Editor scripts (Assets/Editor/AssetImportPostprocessor.cs)
//    to apply these settings automatically on import."
// AssetPostprocessor callbacks fire during the import pipeline itself, before
// the asset is finalised, so the settings stick on first import and on every
// re-import (including a fresh checkout / Library rebuild).
//
// SCOPE GUARD: every callback first checks the asset path is under
// Assets/Models/KayKit/. Assets anywhere else in the project are left untouched.
//
// ── Model settings (spec Part 7) ─────────────────────────────────────────────
//   • Optimize Mesh ............ ON
//   • Read/Write Enabled ....... OFF (saves memory)
//   • Generate Lightmap UVs .... ON for static (non-skinned) meshes
//   • Mesh Compression ......... Medium
//   • Animation Type ........... Generic (KayKit art style; Humanoid is overkill)
//
// ── Texture settings (spec Part 2 + Part 7) ──────────────────────────────────
//   • Max Size ................. 1024 for character / building atlases,
//                                256 for small props
//   • Compression format ....... ASTC (Android + iOS); 6x6 block on both
//   • sRGB ..................... ON for albedo / base-colour textures,
//                                OFF for normal / mask / metallic / roughness
//   • Mipmaps .................. ON for 3D textures
//
// ── Material assignment (URP fix) ────────────────────────────────────────────
// The KayKit packs ship NO .mat files. Left to Unity, an FBX auto-imports its
// material from whatever shader the file names — in this URP project that comes
// out white (no _BaseMap) or magenta (a Built-in-pipeline shader URP cannot
// render). OnAssignMaterialModel intercepts that: for any model under
// Assets/Models/KayKit/ it returns a shared `Universal Render Pipeline/Lit`
// material whose _BaseMap is the palette atlas sitting beside the FBX
// (hexagons_medieval.png for the Medieval Hexagon Pack). Flat low-poly look —
// smoothness 0, metallic 0. The material is created once per folder atlas and
// reused, and the FBX is remapped to it. Models imported before this hook
// existed are repaired by the KayKitMaterials.FixAllMaterials menu/-executeMethod
// utility.
//
// ASSEMBLY NOTE. Lives in the fixed DeNelle.Editor.asmdef (Editor-only,
// editing asmdefs is forbidden). Uses only UnityEditor + UnityEngine APIs that
// the asmdef already exposes.
//
// This script does NOT run itself — Unity's import pipeline invokes it.
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.Build;   // WO-408: NamedBuildTarget — version-safe WebGL platform token
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Applies the v2 port-spec Part 7 import settings automatically to every
    /// model and texture imported under <c>Assets/Models/KayKit/</c>. Assets
    /// outside that folder are not touched.
    /// </summary>
    public sealed class AssetImportPostprocessor : AssetPostprocessor
    {
        // ── Scope ────────────────────────────────────────────────────────────
        // Only assets under this folder get the KayKit treatment. Forward
        // slashes — Unity asset paths are always '/' regardless of platform.
        private const string KayKitRoot = "Assets/Models/KayKit/";

        // WO-408: a SECOND, narrower texture-only scope. KayKit gets the full
        // mobile (ASTC) treatment above; everything else that SHIPS still needs a
        // WebGL maxTextureSize cap on IMPORT so a freshly-added 4096²/8192² texture
        // never re-bloats the build between optimizer runs. TextureBatchOptimizer
        // is the batch sweep; this keeps NEW textures held down automatically.
        // These are the shipping art roots from WO-408 (Resources + the heavy
        // gitignored packs). NOTE: forward slashes, case-insensitive match.
        private static readonly string[] ShipTextureRoots =
        {
            "Assets/Resources/",
            "Assets/Mirza Beig/",
            "Assets/Quaternius/",
            "Assets/Art/",
            "Assets/Spells Pack/",
        };

        // Per-role WebGL cap for the ShipTextureRoots (mirrors WO-408 / the
        // TextureBatchOptimizer SizeTable intent). First match wins.
        private const int HudIconCap     = 512;   // Resources/HudIcons (1254² source)
        private const int VfxSheetCap    = 2048;  // Mirza Beig 8192² spritesheets
        private const int Generic3DCap   = 1024;  // base-color / normal / world atlas

        // ── Model settings (spec Part 7) ─────────────────────────────────────
        private const ModelImporterMeshCompression MeshCompression =
            ModelImporterMeshCompression.Medium;

        // ── Texture settings (spec Part 2 + Part 7) ──────────────────────────
        // Character + building texture atlases.
        private const int AtlasMaxSize = 1024;
        // Small standalone props.
        private const int SmallPropMaxSize = 256;
        // ASTC 6x6 — same format on Android and iOS (spec Part 2: "ASTC 6x6 ...
        // ASTC 6x6 also for iOS, same format, smaller file size than ETC2").
        private const TextureImporterFormat AstcFormat = TextureImporterFormat.ASTC_6x6;
        // Quality 50 = Normal — a sensible mobile default for ASTC.
        private const int AstcCompressionQuality = 50;

        // Build targets the import override is applied for.
        private static readonly string[] PlatformTargets = { "Android", "iOS" };

        // =====================================================================
        //  Model import
        // =====================================================================

        /// <summary>
        /// Fires before a model (GLTF / GLB / FBX / OBJ) under
        /// <c>Assets/Models/KayKit/</c> is imported. Applies the Part 7 model
        /// settings: Optimize Mesh on, Read/Write off, Medium compression,
        /// Generic animation type, and Generate Lightmap UVs for static meshes.
        /// </summary>
        private void OnPreprocessModel()
        {
            if (!IsKayKitAsset(assetPath)) return;
            if (assetImporter is not ModelImporter model) return;

            // Geometry — spec Part 7.
            model.optimizeMeshPolygons = true;   // "Optimize Mesh" ON
            model.optimizeMeshVertices = true;
            model.isReadable = false;            // "Read/Write Enabled" OFF — saves memory
            model.meshCompression = MeshCompression; // "Medium"

            // Lightmap UVs — ON for static meshes only. A KayKit character /
            // skinned mesh is dynamic (it animates), so it never contributes to
            // baked lightmaps; generating its second UV set would just waste
            // import time and memory. Static props / walls / buildings DO get
            // baked, so they need the UVs. We detect "static" as "ships no
            // animation and no skinned/avatar rig".
            bool isStatic = !HasAnimationOrRig(model);
            model.generateSecondaryUV = isStatic; // "Generate Lightmap UVs" ON for static meshes

            // Animation — Generic for the KayKit art style (spec Part 7:
            // "Humanoid retargeting is overkill for this art style").
            // Leave NoAnimation models (static props) as-is; only models that
            // actually carry clips/rigs are forced to Generic.
            if (!isStatic && model.animationType != ModelImporterAnimationType.Generic)
                model.animationType = ModelImporterAnimationType.Generic;
        }

        // =====================================================================
        //  Material assignment — URP fix
        // =====================================================================

        /// <summary>
        /// Fires once per material a model under <c>Assets/Models/KayKit/</c>
        /// defines, letting us substitute the material Unity would otherwise
        /// auto-create. Returns a shared <c>Universal Render Pipeline/Lit</c>
        /// material whose <c>_BaseMap</c> is the palette atlas beside the FBX —
        /// so KayKit models render correctly in URP instead of white / magenta.
        /// Returning <c>null</c> falls back to Unity's default behaviour (used
        /// when no co-located atlas can be found, e.g. animation-only rigs).
        /// </summary>
        /// <param name="material">The material Unity is about to assign.</param>
        /// <param name="renderer">The renderer the material is assigned to.</param>
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!IsKayKitAsset(assetPath)) return null;

            // Find the shared palette atlas sitting beside this FBX.
            string texturePath = KayKitMaterials.FindPaletteTexture(assetPath);
            if (string.IsNullOrEmpty(texturePath)) return null; // -> Unity default

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) return null;

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return null; // URP missing — let Unity handle it

            // One shared .mat per atlas, written beside the texture. If it does
            // not exist yet (first import), let Unity create the default
            // material this pass; KayKitMaterials.FixAllMaterials creates the
            // .mat assets and remaps. Once the .mat exists, re-imports pick it
            // up here automatically.
            string dir     = System.IO.Path.GetDirectoryName(texturePath).Replace('\\', '/');
            string texName = System.IO.Path.GetFileNameWithoutExtension(texturePath);
            string matPath = dir + "/" + texName + "_URP.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                // Heal the cached material in case it drifted, then use it.
                if (existing.shader != urpLit) existing.shader = urpLit;
                KayKitMaterials.ConfigureUrpLitMaterial(existing, texture);
                return existing;
            }

            // No cached .mat yet: hand back a correctly-configured URP material
            // for this import so the model is at least not white/magenta. The
            // KayKitMaterials utility promotes these into shared .mat assets.
            var mat = new Material(urpLit) { name = texName + "_URP" };
            KayKitMaterials.ConfigureUrpLitMaterial(mat, texture);
            return mat;
        }

        // =====================================================================
        //  Texture import
        // =====================================================================

        /// <summary>
        /// Fires before a texture under <c>Assets/Models/KayKit/</c> is
        /// imported. Applies the Part 2 + Part 7 texture settings: per-role
        /// Max Size, ASTC 6x6 compression for Android + iOS, and sRGB on for
        /// albedo / off for normal &amp; mask maps.
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (assetImporter is not TextureImporter tex0) return;

            // WO-408: non-KayKit shipping textures get only a WebGL maxTextureSize
            // override on import (no ASTC/sRGB rewrite — those packs author their
            // own settings). This holds NEW textures down between optimizer sweeps.
            if (!IsKayKitAsset(assetPath))
            {
                ApplyShipWebGLCap(tex0);
                return;
            }

            var texture = tex0;

            string lowerPath = assetPath.ToLowerInvariant();
            bool isNormalOrMask = IsNormalOrMaskMap(lowerPath);

            // sRGB — ON for albedo / base-colour, OFF for linear data
            // (normal / mask / metallic / roughness / AO).
            texture.sRGBTexture = !isNormalOrMask;

            // A normal map should be flagged as such so Unity packs it correctly.
            texture.textureType = IsNormalMap(lowerPath)
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            // Mipmaps ON for 3D textures (spec Part 2: "Generate mipmaps ...
            // on for 3D textures").
            texture.mipmapEnabled = true;

            // Max Size — 1024 for character / building atlases, 256 for small
            // props. The KayKit shared atlases (hexagons_medieval.png,
            // *_texture.png, dungeon_texture.png) are atlases; anything else
            // under the pack is treated as a small prop texture.
            int maxSize = IsAtlasTexture(lowerPath) ? AtlasMaxSize : SmallPropMaxSize;

            // Default (desktop / editor) platform settings.
            texture.maxTextureSize = maxSize;

            // Per-platform ASTC 6x6 override for Android + iOS.
            foreach (var platform in PlatformTargets)
            {
                var settings = texture.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = maxSize;
                settings.format = AstcFormat;                 // ASTC 6x6
                settings.compressionQuality = AstcCompressionQuality;
                settings.textureCompression = TextureImporterCompression.Compressed;
                texture.SetPlatformTextureSettings(settings);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// True when the asset path is under <c>Assets/Models/KayKit/</c>.
        /// </summary>
        private static bool IsKayKitAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.StartsWith(KayKitRoot, StringComparison.OrdinalIgnoreCase);
        }

        // ── WO-408: WebGL-only cap for non-KayKit shipping textures ────────────

        /// <summary>
        /// True when the asset lives under one of the WO-408 shipping art roots
        /// (Resources + the heavy packs) — these get a WebGL maxTextureSize cap on
        /// import so a fresh 4096²/8192² texture cannot re-bloat the build.
        /// </summary>
        private static bool IsShipTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var root in ShipTextureRoots)
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// WO-408 per-role WebGL cap for a shipping texture path. Mirrors the
        /// TextureBatchOptimizer SizeTable intent: HUD icons 512, Mirza Beig VFX
        /// sheets 2048, everything else (base-color / normal / world atlas) 1024.
        /// </summary>
        private static int ResolveShipCap(string lowerPath)
        {
            if (lowerPath.Contains("/hudicons/")) return HudIconCap;
            if (lowerPath.Contains("/mirza beig/")) return VfxSheetCap;
            return Generic3DCap;
        }

        /// <summary>
        /// Applies ONLY a WebGL platform maxTextureSize override (Default/source
        /// settings untouched) to a non-KayKit shipping texture, capping it per
        /// <see cref="ResolveShipCap"/>. Idempotent: if the WebGL override already
        /// caps at or below the role cap, nothing changes.
        /// </summary>
        private static void ApplyShipWebGLCap(TextureImporter texture)
        {
            string path = texture.assetPath;
            if (!IsShipTexture(path)) return;

            int cap = ResolveShipCap(path.ToLowerInvariant());
            string webGL = NamedBuildTarget.WebGL.TargetName;

            var settings = texture.GetPlatformTextureSettings(webGL);
            // Already capped tightly enough — don't churn the import.
            if (settings.overridden && settings.maxTextureSize <= cap) return;

            settings.name = webGL;
            settings.overridden = true;
            settings.maxTextureSize = cap;
            texture.SetPlatformTextureSettings(settings);
        }

        /// <summary>
        /// True when the model ships animation clips or a skinned / avatar rig
        /// — i.e. it is a dynamic (animated) mesh, not a static prop. Static
        /// meshes get lightmap UVs; dynamic ones do not.
        /// </summary>
        private static bool HasAnimationOrRig(ModelImporter model)
        {
            // importAnimation is true by default; the reliable signal is whether
            // the source file actually contains clips, or a rig that produces an
            // avatar. KayKit character GLBs + the anim-pack rigs carry clips;
            // the medieval buildings / walls / props do not.
            bool hasClips = model.importedTakeInfos != null
                            && model.importedTakeInfos.Length > 0;
            bool hasRig = model.animationType == ModelImporterAnimationType.Human
                          || model.animationType == ModelImporterAnimationType.Generic;
            return hasClips || hasRig;
        }

        /// <summary>
        /// True for textures that should be treated as linear data — normal
        /// maps and mask / packed maps (metallic, roughness, AO, ORM, etc.).
        /// </summary>
        private static bool IsNormalOrMaskMap(string lowerPath)
        {
            return IsNormalMap(lowerPath)
                || lowerPath.Contains("_mask")
                || lowerPath.Contains("_metallic")
                || lowerPath.Contains("_roughness")
                || lowerPath.Contains("_orm")
                || lowerPath.Contains("_ao")
                || lowerPath.Contains("_occlusion")
                || lowerPath.Contains("_specular")
                || lowerPath.Contains("_emission")  // emission maps are linear too
                || lowerPath.Contains("_emissive");
        }

        /// <summary>
        /// True for normal-map textures (named with the usual conventions).
        /// </summary>
        private static bool IsNormalMap(string lowerPath)
        {
            return lowerPath.Contains("_normal")
                || lowerPath.Contains("_nrm")
                || lowerPath.EndsWith("_n.png")
                || lowerPath.EndsWith("_n.jpg")
                || lowerPath.Contains("normalmap")
                || lowerPath.Contains("_norm");
        }

        /// <summary>
        /// True for the shared KayKit texture atlases (character / building
        /// atlases) — these get the 1024 Max Size. Everything else under the
        /// pack is treated as a small-prop texture and capped at 256.
        /// The KayKit packs ship a handful of named shared atlases:
        /// <c>hexagons_medieval.png</c> (medieval buildings + walls),
        /// <c>*_texture.png</c> (mage_texture, knight_texture, ranger_texture —
        /// the character / weapon atlases) and <c>dungeon_texture.png</c>.
        /// </summary>
        private static bool IsAtlasTexture(string lowerPath)
        {
            return lowerPath.Contains("hexagons_medieval")
                || lowerPath.Contains("dungeon_texture")
                || lowerPath.EndsWith("_texture.png")
                || lowerPath.EndsWith("_texture.jpg")
                || lowerPath.Contains("atlas")
                || lowerPath.Contains("_albedo")
                || lowerPath.Contains("colormap")
                || lowerPath.Contains("_basecolor");
        }
    }
}
