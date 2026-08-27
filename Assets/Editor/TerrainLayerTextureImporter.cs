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
            // ANISO 8, NOT 4 (WO-1218). Read the measurement before changing this number.
            //
            // At 4 the owner's device rendered the entire meadow as a dense, near-white
            // sparkle that got WORSE with distance. Measured off the device capture
            // tmp/screen-103219.png (2670x1200, Seeker, build 2026.08.26.341419), as the
            // standard deviation of luminance minus a 3x3 box blur - i.e. contrast at the
            // ONE-PIXEL scale, which a working mip chain must drive toward zero at range:
            //
            //     near ground  (rows 1020-1180):  hp1 =  9.1   <- correct, mip resolved
            //     mid  ground  (rows  460- 620):  hp1 = 29.4   <- 3.2x WORSE further away
            //     far  ground  (rows  380- 460):  hp1 = 23.7
            //
            // Energy at mid-distance is FLAT across every scale from 1 px to 16 px
            // (hp1 29.4 / hp16 36.4), which is Nyquist-limit noise, and the excursion is
            // largest in the BLUE channel (53.2) on the LOWEST-blue part of the frame
            // (mean B 140 vs G 199) - white flecks punching out of green, not a hue
            // problem. Mipmaps are on and always were; the mip chain is not the hole.
            //
            // THE HOLE IS THE ANISOTROPY RATIO. A ground plane at these grazing pitches
            // needs far more than 4 taps along the major axis; the GPU still selects the
            // sharper 4:1 mip and then undersamples the rest, which aliases hardest at
            // grazing angles and grows with distance - exactly the measured profile.
            //
            // AND IT WAS DEVICE-ONLY FOR A READ-AT-SOURCE REASON, not by luck:
            //   ProjectSettings/QualitySettings.asset:130  Desktop     anisotropicTextures: 2  (Forced On -> 16x on everything)
            //   ProjectSettings/QualitySettings.asset:77   Seeker_High anisotropicTextures: 1  (Per-Texture -> honours THIS number)
            //   ProjectSettings/QualitySettings.asset:170  Android default quality = 1
            //   device log 08-25 19:46:09  "device='Solana Mobile Inc. Seeker' ... tier='Seeker_High'"
            // Every editor and desktop run forced 16x and hid it; the phone honoured 4.
            //
            // 8 and not 16: this device is FILL-RATE BOUND and the terrain already takes
            // 8 layers x 2 maps of texture fetches per pixel, so doubling the aniso taps
            // is the measured, affordable step. If a fresh capture at the same pitch still
            // shows mid-field hp1 well above the near-field value, 16 is the next move -
            // judge it by that number, not by eye.
            //
            // NOTE: this postprocessor only runs on IMPORT, so the same value is written
            // into the 16 .meta files under Assets/Generated/Terrain/Layers/ in the same
            // change. A code-only edit here would take effect for nobody until something
            // happened to reimport, which is how an import fix ships as a no-op.
            importer.anisoLevel = 8;                   // ground is viewed at grazing angles
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
