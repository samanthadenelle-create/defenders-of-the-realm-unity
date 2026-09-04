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
        importer.maxTextureSize = MaxSizeFor(assetPath);
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
            importer.maxTextureSize = MaxSizeFor(path);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.spriteBorder = BorderFor(path);
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>WO-1359 - the action-bar emblem sheet is 1983x793 carrying five ~386px circles.
    /// 2048 is the first power of two that does NOT resample it, and resampling is what we are
    /// buying off: a face medallion is about one dock-fifth of a 46%-wide ActionBar zone (~240
    /// reference px, ~300 device px on the owner's 2670-wide screen), so a 386px source is already
    /// the right side of sharp and a 1024 cap would drop it to 193px and soften the most-tapped
    /// art in the game. The kit's 4096 would only pad. Everything outside /actionbar/ is unchanged.
    /// (The slice manifest stores NORMALIZED rects, so even if this cap ever does resample the
    /// sheet the five faces still cut correctly - see SpriteSheetSlices.)</summary>
    private static int MaxSizeFor(string path)
    {
        string p = (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        return p.Contains("/actionbar/") ? 2048 : 4096;
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
