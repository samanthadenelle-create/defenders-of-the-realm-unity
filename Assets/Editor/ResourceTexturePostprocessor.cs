// =============================================================================
// ResourceTexturePostprocessor — forces every PNG/JPG under
// Assets/Resources/Textures/ to import as a Default (3D) texture with sRGB
// on, so basecolor maps actually bind to URP/Lit materials at runtime.
// -----------------------------------------------------------------------------
// Owner question 2026-05-20: the cathedral spire kept rendering as a dark grey
// blob even though Cathedral.png shipped in Resources/Textures/. Root cause:
// Unity sometimes auto-imports large basecolor PNGs as Sprite/UI textures or
// strips sRGB, which makes _BaseMap binds render solid grey.
//
// This postprocessor catches the import and forces:
//   • textureType = Default (3D texture)
//   • sRGBTexture = true (basecolors are colour textures, not data maps)
//   • mipmapEnabled = true (distant mesh sampling)
//
// No-op when those settings are already correct.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class ResourceTexturePostprocessor : AssetPostprocessor
    {
        private const string Prefix = "Assets/Resources/Textures/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase))
                return;
            var ti = assetImporter as TextureImporter;
            if (ti == null) return;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (!ti.sRGBTexture) { ti.sRGBTexture = true; dirty = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
            if (dirty) Debug.Log("[ResourceTexturePostprocessor] Normalised import for " + assetPath);
        }
    }
}
