// =============================================================================
// ItemIconSlicer — slices the item-art sheets in Assets/Art/UI/ItemIcons into
// named multiple-mode sub-sprites, then mirrors the sliced sheets into
// Assets/Resources/ItemIcons so they load at RUNTIME (WebGL-safe) via
// Resources.LoadAll<Sprite>("ItemIcons/<sheet>").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// WHY A SLICER (not hand-set .meta): the source images are AI-generated atlas
// sheets (swords / bows / shields in a row, an armour grid, potion/crafting
// grids). Each is a clean grid or single row. We compute uniform cell rects from
// the layout READ off each image and assign meaningful sprite names so the
// runtime catalog (ItemIconCatalog) can address a sprite by name.
//
// LAYOUTS READ (left->right, top->bottom; rarity ascends left->right):
//   Ud37F.jpg  swords     1 row  x 5 cols  -> sword_t1..sword_t5
//   inEJH.jpg  bows       1 row  x 5 cols  -> bow_t1..bow_t5     (labels scrambled; by shape)
//   WRdWM.jpg  shields    1 row  x 5 cols  -> shield_wooden/steel/rune/dragon/magical
//   VxBVb.jpg  armour     3 rows x 6 cols  -> helm_* / chest_* / gauntlet_* / belt_* / pauldron
//   0D5St.jpg  mixed mega 2 rows x 6 cols  -> (overview sheet) generic art_r{r}_c{c}
//   ConsumablesCrafting/
//     bRUz5.jpg potions   2 rows x 4 cols  -> potion_health/mana/.../fire
//     CtQcX.jpg crafting  4 rows x 6 cols  -> mat_* / potion_* / rune_* (content-named row by row)
//     jdRCa.jpg crafting  4 rows x 8 cols  -> potion_* / herb_* / crystal_* / scroll_* / rune_* / ore_*
//   (P94Fw.jpg is a HUD-icon MOCKUP sheet, NOT item icons -> intentionally skipped.)
//
// RUNTIME LOAD (WebGL-safe): Resources cannot address Assets/Art. So after
// slicing in place we COPY each sliced .jpg + its .meta into Assets/Resources/
// ItemIcons/ (same GUID/sprite-rects carried by the .meta). The catalog then
// calls Resources.LoadAll<Sprite>("ItemIcons/<sheet>") — fully WebGL-safe,
// no AssetDatabase at runtime. Re-run this after editing any sheet.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ItemIconSlicer
    {
        private const string SrcDir   = "Assets/Art/UI/ItemIcons";
        private const string ConsDir  = "Assets/Art/UI/ItemIcons/ConsumablesCrafting";
        private const string ResDir   = "Assets/Resources/ItemIcons";

        // A sheet to slice: source path, grid dimensions, and an explicit name list
        // (row-major, top->bottom / left->right). Blank "" names skip that cell
        // (e.g. empty grid slots) so we don't emit junk sprites.
        private struct Sheet
        {
            public string Path;
            public int Rows;
            public int Cols;
            public string[] Names; // length == Rows*Cols, row-major; "" = skip cell
        }

        [MenuItem("Defenders/Art/Slice Item Icons")]
        public static void SliceMenu() { Run(); }

        // Public, batchmode-runnable: run-unity-method DeNelle.Editor.ItemIconSlicer.Run
        public static void Run()
        {
            var sheets = BuildSheetTable();
            int sliced = 0;
            foreach (var s in sheets)
            {
                if (!File.Exists(s.Path))
                {
                    Debug.LogWarning("[ItemIconSlicer] missing sheet (skipped): " + s.Path);
                    continue;
                }
                if (SliceSheet(s)) sliced++;
            }

            CopyToResources(sheets);
            AssetDatabase.Refresh();
            Debug.Log("[ItemIconSlicer] done — sliced " + sliced + " sheet(s); mirrored to " + ResDir);
        }

        // ---------------------------------------------------------------------
        // The layout table (the names map 1:1 to ItemIconCatalog's lookups).
        // ---------------------------------------------------------------------
        private static List<Sheet> BuildSheetTable()
        {
            var list = new List<Sheet>();

            // Swords — single row, 5 tiers ascending.
            list.Add(new Sheet
            {
                Path = SrcDir + "/Ud37F.jpg", Rows = 1, Cols = 5,
                Names = new[] { "sword_t1", "sword_t2", "sword_t3", "sword_t4", "sword_t5" }
            });

            // Bows — single row, 5 tiers (labels on-sheet are scrambled; ordered by shape:
            // plain wood -> ornate -> recurve -> ethereal/glowing -> bone/spiked).
            list.Add(new Sheet
            {
                Path = SrcDir + "/inEJH.jpg", Rows = 1, Cols = 5,
                Names = new[] { "bow_t1", "bow_t2", "bow_t3", "bow_t4", "bow_t5" }
            });

            // Shields — single row, 5 named tiers.
            list.Add(new Sheet
            {
                Path = SrcDir + "/WRdWM.jpg", Rows = 1, Cols = 5,
                Names = new[] { "shield_wooden", "shield_steel", "shield_rune", "shield_dragon", "shield_magical" }
            });

            // Armour grid — 3 rows x 6 cols. Read content:
            //  Row1: pauldron, then 5 helmets (a..e).
            //  Row2: 4 chest/cuirass pieces, a chest variant, a gauntlet.
            //  Row3: belts / faulds, a gauntlet.
            list.Add(new Sheet
            {
                Path = SrcDir + "/VxBVb.jpg", Rows = 3, Cols = 6,
                Names = new[]
                {
                    "pauldron_a", "helm_a", "helm_b", "helm_c", "helm_d", "helm_e",
                    "chest_a",    "chest_b", "chest_c", "chest_d", "chest_e", "gauntlet_a",
                    "belt_a",     "belt_b",  "belt_c",  "belt_d",  "chest_f", "gauntlet_b"
                }
            });

            // 0D5St — a MIXED overview/mega sheet (bows + staff + shields on top, helms +
            // rune-shields + chest + gauntlet below). Items are NOT on a uniform grid, so
            // we slice a coarse 2x6 reference grid with generic names (the catalog draws
            // its weapon/armour art from the dedicated sheets above; this is kept sliceable
            // for reference / future hand-naming, not wired into the catalog).
            list.Add(new Sheet
            {
                Path = SrcDir + "/0D5St.jpg", Rows = 2, Cols = 6,
                Names = new[]
                {
                    "misc_r1c1", "misc_r1c2", "misc_r1c3", "misc_r1c4", "misc_r1c5", "misc_r1c6",
                    "misc_r2c1", "misc_r2c2", "misc_r2c3", "misc_r2c4", "misc_r2c5", "misc_r2c6"
                }
            });

            // Potions — 2 rows x 4 cols (named by fluid colour / on-sheet label).
            //  Row1: Health(red), Mana(blue), Mana(cyan), Poison(violet)
            //  Row2: Strength(green), Strength(yellow), Poison(magenta), Fire(orange)
            list.Add(new Sheet
            {
                Path = ConsDir + "/bRUz5.jpg", Rows = 2, Cols = 4,
                Names = new[]
                {
                    "potion_health", "potion_mana", "potion_mana_b", "potion_poison",
                    "potion_strength", "potion_strength_b", "potion_poison_b", "potion_fire"
                }
            });

            // Crafting grid CtQcX — 4 rows x 6 cols. Content read row by row:
            //  R1: herb, crystal, crystal, scroll, crystal, crystal
            //  R2: herb, crystal, runestone, crystal, potion, potion
            //  R3: runestone, runestone, runestone, pouch, potion, potion
            //  R4: dagger, hammer, pouch, pouch, dagger, hammer
            list.Add(new Sheet
            {
                Path = ConsDir + "/CtQcX.jpg", Rows = 4, Cols = 6,
                Names = new[]
                {
                    "mat_herb",      "mat_crystal_a", "mat_crystal_b", "mat_scroll_a", "mat_crystal_c", "mat_crystal_d",
                    "mat_herb_b",    "mat_crystal_e", "mat_rune_a",    "mat_crystal_f","potion_a",      "potion_b",
                    "mat_rune_b",    "mat_rune_c",    "mat_rune_d",    "mat_pouch_a",  "potion_c",      "potion_d",
                    "mat_dagger",    "mat_hammer_a",  "mat_pouch_b",   "mat_pouch_c",  "mat_dirk",      "mat_hammer_b"
                }
            });

            // Crafting grid jdRCa — 4 rows x 8 cols (transparent-bg item set). Read:
            //  R1: 6 potions, herb, crystal
            //  R2: 4 potions, 3 herbs, scroll
            //  R3: 2 potions, runestone, 3 crystals, 2 scrolls
            //  R4: 2 potions, 3 runestones, ore, 2 ingots
            list.Add(new Sheet
            {
                Path = ConsDir + "/jdRCa.jpg", Rows = 4, Cols = 8,
                Names = new[]
                {
                    "potion_e", "potion_f", "potion_g", "potion_h", "potion_i", "potion_j", "mat_herb_c", "mat_crystal_g",
                    "potion_k", "potion_l", "potion_m", "potion_n", "mat_herb_d","mat_herb_e","mat_herb_f","mat_scroll_b",
                    "potion_o", "potion_p", "mat_rune_e","mat_crystal_h","mat_crystal_i","mat_crystal_j","mat_scroll_c","mat_scroll_d",
                    "potion_q", "potion_r", "mat_rune_f","mat_rune_g","mat_rune_h","mat_ore","mat_ingot_a","mat_ingot_b"
                }
            });

            return list;
        }

        // ---------------------------------------------------------------------
        // Slice one sheet: importer -> Multiple, build uniform grid rects + names.
        // ---------------------------------------------------------------------
        private static bool SliceSheet(Sheet s)
        {
            var importer = AssetImporter.GetAtPath(s.Path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ItemIconSlicer] no TextureImporter for " + s.Path);
                return false;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode     = SpriteImportMode.Multiple;
            importer.mipmapEnabled        = false;
            importer.alphaIsTransparency  = true;
            importer.wrapMode             = TextureWrapMode.Clamp;
            importer.filterMode           = FilterMode.Bilinear;
            importer.textureCompression   = TextureImporterCompression.Uncompressed; // no JPEG-on-DXT artefacts
            importer.npotScale            = TextureImporterNPOTScale.None;

            // Read the real pixel dimensions so the grid lands on the source resolution.
            int texW, texH;
            GetSourceSize(importer, s.Path, out texW, out texH);
            if (texW <= 0 || texH <= 0) { texW = 1024; texH = 1024; }

            float cellW = (float)texW / s.Cols;
            float cellH = (float)texH / s.Rows;

            var metas = new List<SpriteMetaData>();
            int idx = 0;
            for (int row = 0; row < s.Rows; row++)
            {
                for (int col = 0; col < s.Cols; col++, idx++)
                {
                    string name = idx < s.Names.Length ? s.Names[idx] : null;
                    if (string.IsNullOrEmpty(name)) continue; // explicit skip

                    // Unity sprite rects are bottom-left origin; our names are top->bottom.
                    float x = col * cellW;
                    float y = (s.Rows - 1 - row) * cellH;

                    var md = new SpriteMetaData
                    {
                        name      = name,
                        rect      = new Rect(x, y, cellW, cellH),
                        alignment = (int)SpriteAlignment.Center,
                        pivot     = new Vector2(0.5f, 0.5f)
                    };
                    metas.Add(md);
                }
            }

#pragma warning disable 0618 // spritesheet is the stable batch API for grid slicing
            importer.spritesheet = metas.ToArray();
#pragma warning restore 0618

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[ItemIconSlicer] sliced " + Path.GetFileName(s.Path) + " -> " + metas.Count + " sprites (" + texW + "x" + texH + ")");
            return true;
        }

        private static void GetSourceSize(TextureImporter importer, string path, out int w, out int h)
        {
            w = 0; h = 0;
            // Prefer the importer's reported source size (reflection-free public API).
            try { importer.GetSourceTextureWidthAndHeight(out w, out h); }
            catch { /* older API — fall through */ }
            if (w > 0 && h > 0) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) { w = tex.width; h = tex.height; }
        }

        // ---------------------------------------------------------------------
        // Mirror sliced sheets (+ their .meta = sprite rects/names) into Resources
        // so the runtime catalog can Resources.LoadAll<Sprite> them (WebGL-safe).
        // ---------------------------------------------------------------------
        private static void CopyToResources(List<Sheet> sheets)
        {
            if (!AssetDatabase.IsValidFolder(ResDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "ItemIcons");
            }

            foreach (var s in sheets)
            {
                if (!File.Exists(s.Path)) continue;
                string fileName = Path.GetFileName(s.Path);
                string dst = ResDir + "/" + fileName;

                // CopyAsset carries the .meta (and thus the sprite sub-rects + names).
                if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(s.Path, dst))
                    Debug.LogWarning("[ItemIconSlicer] copy-to-Resources failed: " + s.Path + " -> " + dst);
            }
        }
    }
}
