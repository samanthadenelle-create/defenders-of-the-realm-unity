// =============================================================================
// BlinkIconImporter — mirrors the Blink "500 RPG Spell Icons — Fantasy" library
// (Assets/Blink/Art/Icons, GITIGNORED owner-purchased pack) into committed
// Assets/Resources/RpgUi/<role>/... so the runtime RpgUiCatalog / ConceptIconResolver
// can serve real spell icons on a fresh clone / CI / WebGL (WO-681).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// Sibling of RpgUiImporter (same CopyAsset-into-Resources pattern, §2.1 of
// docs/SME/BLINK_SME.md — "UI: mirror-to-Resources"): CopyAsset generates a FRESH
// GUID per copy, so there is no duplicate-GUID clash with the gitignored originals,
// and the committed mirrors keep working when the pack is absent (LogWarning + skip).
//
// WHAT IT MIRRORS (the pack's own class/category structure is preserved):
//   • Classes/<ArchetypeGroup>/<Class>/<Class><N>.png  (5 groups x 5 classes x 20 = 500)
//       -> Resources/RpgUi/spellicons/<ArchetypeGroup>/<Class>/<Class><N>.png
//     Resources.LoadAll is recursive, so RpgUiCatalog role "spellicons" indexes all
//     500 by their unique class-prefixed names (e.g. Get("spellicons","Guardian13")).
//   • Emblems/<Class>.png (25 class emblems)
//       -> Resources/RpgUi/emblem/<Class>.png            (role "emblem")
//   • Extra/Slots/Slot1-3.png + Slot_<Class>.png (28 action-bar slot frames)
//       -> Resources/RpgUi/classslot/<name>.png          (role "classslot", 9-sliced)
//   NOT mirrored (deliberate): Extra/ archetype backgrounds + illustrations +
//   Promo* marketing art, SourceFiles/, Demo/ — decorative/marketing, not icon
//   pipeline material; add explicitly if a screen ever needs them.
//
// IMPORT SETTINGS — matches RpgUiImporter.ForceSpriteImport (Sprite/Single, no mips,
// clamp, bilinear, alpha-transparency) EXCEPT compression: the existing handful of
// chrome sprites import Uncompressed for crisp gilt edges, but 553 mirrored PNGs
// uncompressed would add ~150 MB of texture data to every build (Resources always
// ship). Spell icons are 256x256 painterly art rendered at =64 px in the action bar,
// so Compressed (DXT/ASTC) at capped size is the UI-appropriate choice:
//   spellicons max 256, emblems/slots max 512 (emblems are 600x600 and may render
//   large on hero-select). Whole library lands around ~40 MB of compressed texture.
//
// RUN (batchmode-safe):  run-unity-method DeNelle.Editor.BlinkIconImporter.Run
//   (menu: Defenders/Art/Import Blink Spell Icons)
// Idempotent: re-run replaces existing mirrors (delete + re-copy, fresh settings).
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BlinkIconImporter
    {
        private const string PackRoot = "Assets/Blink/Art/Icons";
        private const string ResRoot  = "Assets/Resources/RpgUi";

        // Role folders under Resources/RpgUi (open role strings — RpgUiCatalog.EnsureRole
        // loads any "RpgUi/<role>" folder; no catalog code change needed).
        private const string RoleSpellIcons = "spellicons";
        private const string RoleEmblem     = "emblem";
        private const string RoleClassSlot  = "classslot";

        [MenuItem("Defenders/Art/Import Blink Spell Icons")]
        public static void ImportMenu() { Run(); }

        // Public, batchmode-runnable.
        public static void Run()
        {
            if (!Directory.Exists(PackRoot))
            {
                // Pack absent (fresh clone / CI) — the committed mirrors keep working; no-op.
                Debug.LogWarning("[BlinkIconImporter] pack not present (" + PackRoot +
                                 ") — nothing to mirror; committed mirrors remain as-is.");
                return;
            }

            int copied = 0, failed = 0;

            // ── 1) Classes/<Group>/<Class>/<Class><N>.png -> spellicons/<Group>/<Class>/ ──
            // DEMO-DAY TRIM (2026-07-12, WebGL/Vercel 100MB budget + LoadAll memory):
            // mirror ONLY the classes concept-icons.json actually maps (~160 icons ≈ 10MB)
            // instead of all 25 (~40MB). Widen this set when new mappings need it.
            var mappedClasses = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase)
            {
                "Guardian", "Paladin", "Hunter", "Barbarian",
                "Deathknight", "Electromancer", "Pyromancer", "Arcanist",
            };
            string classesRoot = PackRoot + "/Classes";
            if (Directory.Exists(classesRoot))
            {
                foreach (string groupDir in Directory.GetDirectories(classesRoot))
                {
                    string group = Path.GetFileName(groupDir);
                    foreach (string classDir in Directory.GetDirectories(groupDir))
                    {
                        string cls = Path.GetFileName(classDir);
                        if (!mappedClasses.Contains(cls)) continue;   // demo-day trim
                        string dstDir = ResRoot + "/" + RoleSpellIcons + "/" + group + "/" + cls;
                        foreach (string src in Directory.GetFiles(classDir, "*.png"))
                            Mirror(src, dstDir, maxSize: 256, border: 0, ref copied, ref failed);
                    }
                }
            }
            else Debug.LogWarning("[BlinkIconImporter] missing " + classesRoot + " — skipped spell icons.");

            // ── 2) Emblems/<Class>.png -> emblem/ (may render large on hero-select: 512) ──
            string emblemRoot = PackRoot + "/Emblems";
            if (Directory.Exists(emblemRoot))
            {
                foreach (string src in Directory.GetFiles(emblemRoot, "*.png"))
                    Mirror(src, ResRoot + "/" + RoleEmblem, maxSize: 512, border: 0, ref copied, ref failed);
            }
            else Debug.LogWarning("[BlinkIconImporter] missing " + emblemRoot + " — skipped emblems.");

            // ── 3) Extra/Slots -> classslot/ (ornate square frames; 9-sliced so they can
            //       stretch as action-bar slot chrome without distorting the corners). ──
            string slotsRoot = PackRoot + "/Extra/Slots";
            if (Directory.Exists(slotsRoot))
            {
                foreach (string src in Directory.GetFiles(slotsRoot, "*.png"))
                    Mirror(src, ResRoot + "/" + RoleClassSlot, maxSize: 512, border: 40, ref copied, ref failed);
            }
            else Debug.LogWarning("[BlinkIconImporter] missing " + slotsRoot + " — skipped slot frames.");

            AssetDatabase.Refresh();
            Debug.Log("[BlinkIconImporter] done — mirrored " + copied + " sprite(s) into " + ResRoot +
                      " (" + failed + " failed). Roles: " + RoleSpellIcons + "/" + RoleEmblem + "/" +
                      RoleClassSlot + ". Address via RpgUiCatalog.Get(role, name) / concept-icons.json.");
        }

        // Copy one pack PNG into Resources (keeping its own file name) + force sprite import.
        private static void Mirror(string srcPath, string dstDir, int maxSize, int border,
                                   ref int copied, ref int failed)
        {
            string src = srcPath.Replace('\\', '/');
            EnsureFolder(dstDir);
            string dst = dstDir + "/" + Path.GetFileName(src);

            if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(src, dst))
            {
                Debug.LogWarning("[BlinkIconImporter] copy failed: " + src + " -> " + dst);
                failed++;
                return;
            }
            ForceSpriteImport(dst, maxSize, border);
            copied++;
        }

        // Sprite/Single, UI-ready — RpgUiImporter.ForceSpriteImport settings, but Compressed
        // (see header: 553 sprites uncompressed would bloat every build) and size-capped.
        private static void ForceSpriteImport(string assetPath, int maxSize, int border)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[BlinkIconImporter] no TextureImporter for " + assetPath);
                return;
            }
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.filterMode          = FilterMode.Bilinear;
            importer.textureCompression  = TextureImporterCompression.Compressed;
            importer.npotScale           = TextureImporterNPOTScale.None;
            importer.maxTextureSize      = maxSize;
            if (border > 0)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder   = new Vector4(border, border, border, border);
                settings.spriteMeshType = SpriteMeshType.FullRect; // sliced frames need FullRect
                importer.SetTextureSettings(settings);
            }
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        // ── Folder helper (mirrors RpgUiImporter.EnsureFolder) ──
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
