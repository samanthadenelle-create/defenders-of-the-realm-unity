using UnityEditor;
using UnityEngine;

/// <summary>Import contract for the approved Elarion layered PNG kit.</summary>
public sealed class ElarionMedievalUiImporter : AssetPostprocessor
{
    private const string Root = "Assets/Resources/UI/ElarionMedieval/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.spriteBorder = BorderFor(assetPath);
    }

    [MenuItem("Elarion/UI/Reimport Medieval UI Kit")]
    public static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.spriteBorder = BorderFor(path);
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }

    private static Vector4 BorderFor(string path)
    {
        string p = (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        if (p.Contains("/buttons/") || p.Contains("/tabs/") ||
            p.Contains("/progress/") || p.Contains("/dividers/"))
            return new Vector4(112f, 112f, 112f, 112f);
        if (p.Contains("/frames/modal-frame") || p.Contains("/frames/content-panel") ||
            p.Contains("/frames/card-frame"))
            return new Vector4(96f, 96f, 96f, 96f);
        if (p.Contains("status-panel") || p.Contains("queue-badge"))
            return new Vector4(96f, 96f, 96f, 96f);
        return Vector4.zero;
    }
}
