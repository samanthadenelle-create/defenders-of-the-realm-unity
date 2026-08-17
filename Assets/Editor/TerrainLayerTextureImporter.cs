// =============================================================================
// TerrainLayerTextureImporter — deterministic import settings for the CURATED
// overworld ground textures (WO-1101).
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
// -----------------------------------------------------------------------------
// WHY A POSTPROCESSOR AND NOT HAND-WRITTEN .meta FILES.
// The curated PNGs under Assets/Generated/Terrain/Layers/ arrive as plain files. Left
// alone Unity imports them at the project default (2048, Default type, sRGB) — which
// silently (a) blows the mobile texture budget and (b) imports the *_Normal maps as
// COLOUR textures, so the terrain gets a normal map that is not a normal map and the
// relief that separates Stoneback from Goldfields never appears. Hand-authoring sixteen
// .meta files would encode the same settings once, in files nobody re-reads; this runs
// on every import including a fresh clone's first one.
//
// The layer contract (which stems exist, what they are for) lives in
// DeNelle.Core.World.TerrainLayerSet — this file only decides HOW they import.
// =============================================================================
using DeNelle.Core.World;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class TerrainLayerTextureImporter : AssetPostprocessor
    {
        // Mobile budget: 1024 on desktop, 512 on Android. Ground is viewed at a
        // distance and tiles 6–16 m per repeat, so 512 on device is not a visible loss
        // and 8 layers x 2 maps at 1024 would be ~32 MB of VRAM we do not need.
        private const int DesktopMaxSize = 1024;
        private const int AndroidMaxSize = 512;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(TerrainLayerSet.TextureFolder + "/"))
                return;

            var importer = (TextureImporter)assetImporter;
            bool isNormal = assetPath.EndsWith(TerrainLayerSet.NormalSuffix);

            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;          // normal maps are DATA, never colour
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;                   // ground is viewed at grazing angles
            importer.isReadable = false;               // never needed at runtime; halves memory
            importer.maxTextureSize = DesktopMaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = AndroidMaxSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
