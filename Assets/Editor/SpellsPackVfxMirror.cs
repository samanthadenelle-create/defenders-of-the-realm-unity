// =============================================================================
// SpellsPackVfxMirror — copies gitignored Spells Pack VFX into
// Assets/Resources/VFX/Projectiles/ so Resources.Load + WebGL builds work on a
// fresh clone (mirrors CatalogPrefabImporter for the Polyperfect kit).
// -----------------------------------------------------------------------------
//   Defenders > VFX > Mirror Spells Pack To Resources
//   (batchmode: DeNelle.Editor.SpellsPackVfxMirror.CopyToResources)
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SpellsPackVfxMirror
    {
        private const string SrcRoot = "Assets/Spells Pack/Particles/Prefabs/";
        private const string DstDir  = "Assets/Resources/VFX/Projectiles/";

        private static readonly (string relSrc, string dstName)[] Mirrors =
        {
            ("Projectiles/Projectiles/Projectile_Fire_3.prefab", "Projectile_Fire_3.prefab"),
            ("Projectiles/Casting/Casting_Fire.prefab",        "Casting_Fire.prefab"),
            ("Projectiles/Casting/Casting_Fire_2.prefab",      "Casting_Fire_2.prefab"),
            ("Spells/Spell_Fire_6.prefab",                     "Spell_Fire_6.prefab"),
        };

        [MenuItem("Defenders/VFX/Mirror Spells Pack To Resources")]
        public static void CopyToResources()
        {
            if (!Directory.Exists(DstDir))
                Directory.CreateDirectory(DstDir);

            int copied = 0, skipped = 0, missing = 0;
            foreach (var (relSrc, dstName) in Mirrors)
            {
                string src = SrcRoot + relSrc;
                string dst = DstDir + dstName;

                if (File.Exists(dst))
                {
                    skipped++;
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null)
                {
                    Debug.LogWarning($"[SpellsPackVfxMirror] source missing (pack not imported?): {src}");
                    missing++;
                    continue;
                }

                if (AssetDatabase.CopyAsset(src, dst))
                    copied++;
                else
                {
                    Debug.LogWarning($"[SpellsPackVfxMirror] CopyAsset FAILED: {src} -> {dst}");
                    missing++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SpellsPackVfxMirror] DONE — copied {copied}, skipped {skipped}, missing {missing}. " +
                      $"SPELLS_VFX_MIRROR_OK");
        }
    }
}