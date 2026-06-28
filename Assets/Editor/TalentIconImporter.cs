using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Auto-configures the node-indexed talent-tree icons under
    /// <c>Assets/Resources/Talents/&lt;hero&gt;/</c> as lightweight UI sprites.
    ///
    /// Sheets were sliced into per-node PNGs (knight_01..20, ranger_01..20,
    /// wizard_01..20, shared_01..08). This postprocessor guarantees they import
    /// as transparent UI sprites loadable at runtime via
    /// <c>Resources.Load&lt;Sprite&gt;("Talents/&lt;hero&gt;/&lt;hero&gt;_NN")</c>
    /// (e.g. iconPath "Talents/knight/knight_07").
    ///
    /// Kept light per WO-408: Single sprite, alpha-as-transparency, no mipmaps,
    /// max 512 (UI tier), compressed. Touches nothing outside Talents/.
    /// </summary>
    public class TalentIconImporter : AssetPostprocessor
    {
        private const string TalentFolder = "Assets/Resources/Talents/";

        private bool IsTalentIcon =>
            assetPath.Replace('\\', '/').StartsWith(TalentFolder, System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessTexture()
        {
            if (!IsTalentIcon) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
