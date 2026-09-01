using UnityEditor;

/// <summary>
/// Import contract for the approved Night Market handoff. Runtime resolves these assets as
/// Resources sprites; importing them as Default textures silently removes the authored UI.
/// </summary>
public sealed class NightMarketUiImporter : AssetPostprocessor
{
    private const string Root = "Assets/Resources/UI/NightMarket/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
        Apply((TextureImporter)assetImporter);
    }

    [MenuItem("Elarion/UI/Reimport Night Market Kit")]
    public static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
            Apply(importer);
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }

    private static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
