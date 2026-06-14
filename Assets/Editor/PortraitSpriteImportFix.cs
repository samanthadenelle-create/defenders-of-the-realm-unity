// =============================================================================
// PortraitSpriteImportFix — imports the dialogue portrait textures as SPRITES.
// ROOT CAUSE of the playtest log spam "Failed to find icon Portraits/<id>" + blank
// vendor portraits (owner 2026-06-14): the Resources/Portraits/*.jpg were imported
// as plain Textures (textureType Default). CompanionDialoguePresenter injects an
// `icon:Portraits/<id>` tag (PortraitCache.Has passes via its Texture2D fallback),
// but the Yarn BASE presenter loads the icon with Resources.Load<Sprite> — which
// returns null on a Default texture -> "Failed to find icon" + no portrait shown.
//
// Fix: set every Resources/Portraits texture to TextureImporterType.Sprite (Single)
// so Resources.Load<Sprite> resolves. Idempotent (skips ones already Sprite).
//
// Batchmode: DeNelle.Editor.PortraitSpriteImportFix.Run
// Menu:      Defenders/Art/Fix Portrait Sprite Import
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PortraitSpriteImportFix
    {
        // The dialogue-portrait folders the presenter Resources.Load<Sprite>'s from.
        private static readonly string[] Dirs =
        {
            "Assets/Resources/Portraits",
            "Assets/Resources/PetPortraits",
        };

        [MenuItem("Defenders/Art/Fix Portrait Sprite Import")]
        public static void Run()
        {
            int fixedCount = 0, already = 0;
            foreach (var dir in Dirs)
            {
                if (!System.IO.Directory.Exists(dir)) continue;
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    if (imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single)
                    { already++; continue; }

                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.mipmapEnabled = false;            // UI sprite — no mips needed
                    EditorUtility.SetDirty(imp);
                    imp.SaveAndReimport();
                    fixedCount++;
                    Debug.Log($"[PortraitSpriteImportFix] -> Sprite: {path}");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PortraitSpriteImportFix] DONE — {fixedCount} portrait(s) re-imported as Sprite ({already} already Sprite).");
        }
    }
}
