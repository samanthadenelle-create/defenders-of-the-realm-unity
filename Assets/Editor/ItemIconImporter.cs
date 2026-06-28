using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Auto-configures every texture under <c>Assets/Resources/ItemIcons/</c> as a
    /// lightweight UI sprite so it is loadable at runtime via
    /// <c>Resources.Load&lt;Sprite&gt;("ItemIcons/&lt;id&gt;")</c>.
    ///
    /// WO-542 root cause: these PNGs imported as <c>textureType: 0 (Default),
    /// spriteMode: 0</c>, so <c>Resources.Load&lt;Sprite&gt;</c> returned NULL and the
    /// hero shop / inventory fell back to glyphs. The shop load path
    /// (<c>PartyShopPanelMvvm.ResolveItemSprite</c> →
    /// <c>Resources.Load&lt;Sprite&gt;(iconPath)</c>, iconPath = "ItemIcons/&lt;id&gt;")
    /// needs a real Sprite asset. This postprocessor forces every ItemIcons texture
    /// (new accessories + existing armor) to import as a Single sprite in one pass.
    ///
    /// Mirrors <see cref="TalentIconImporter"/>: Single sprite, alpha-as-transparency,
    /// no mipmaps, max 512 (UI tier), compressed. Touches nothing outside ItemIcons/.
    ///
    /// NOTE: the sliced sheets (Ud37F/inEJH/WRdWM/VxBVb/...) consumed by
    /// <c>ItemIconCatalog</c> via <c>Resources.LoadAll&lt;Sprite&gt;</c> carry their own
    /// Multiple-mode .meta (sprite rects) — those are left untouched because their .meta
    /// already declares the correct mode; this forces Single only on per-id PNGs whose
    /// meta would otherwise default to Default/0.
    /// </summary>
    public class ItemIconImporter : AssetPostprocessor
    {
        private const string ItemIconFolder = "Assets/Resources/ItemIcons/";

        private bool IsItemIcon =>
            assetPath.Replace('\\', '/').StartsWith(ItemIconFolder, System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessTexture()
        {
            if (!IsItemIcon) return;

            var importer = (TextureImporter)assetImporter;

            // Preserve already-sliced sheets (Multiple sprite mode) — only normalise
            // textures that are not explicitly authored as multi-sprite sheets.
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                return;
            }

            importer.textureType       = TextureImporterType.Sprite;
            importer.spriteImportMode  = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.alphaSource       = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled     = false;
            importer.wrapMode          = TextureWrapMode.Clamp;
            importer.filterMode        = FilterMode.Bilinear;
            importer.maxTextureSize    = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
