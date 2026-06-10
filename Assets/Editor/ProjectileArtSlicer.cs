// =============================================================================
// ProjectileArtSlicer — slices the two projectile art sheets in
// Assets/Art/VFX/Projectiles into named sub-sprites, then mirrors the sliced
// sheets into Assets/Resources/ProjectileIcons so they load at RUNTIME
// (WebGL-safe) via Resources.LoadAll<Sprite>("ProjectileIcons/<sheet>").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// WHY A SLICER (not hand-set .meta): the source images are AI-generated atlas
// sheets. Each sheet is split into REGIONS (a zone of the image), and each region
// is a uniform grid of cells. We compute the cell rects from the region bounds
// (normalized 0..1, top-left origin in the layout we read off the image) and
// assign meaningful sprite names so ProjectileArtCatalog can address a sprite by
// name. Mirrors the proven Assets/Editor/ItemIconSlicer.cs approach.
//
// LAYOUTS READ off each image (left->right, top->bottom):
//
//   projectiles_arrows_magic.jpg
//     Region "arrows"  (top ~0..0.52 of the image, transparent bg):
//        4 rows x 4 cols = 16 arrows. Named by element/look:
//          R1: arrow_plain, arrow_fire,  arrow_plain_b, arrow_gold
//          R2: arrow_red,   arrow_gold_b,arrow_steel,   arrow_dark
//          R3: arrow_gold_c,arrow_fire_b,arrow_dark_b,  arrow_ice
//          R4: arrow_steel_b,arrow_steel_c,arrow_fire_c,arrow_red_b
//     Region "bolts"   (bottom ~0.52..1.0 of the image, dark bg):
//        2 rows x ~ -> sliced as a 2 x 5 reference grid (magic bolts/impacts):
//          R1: bolt_fire, bolt_arcane, bolt_ice_blue, impact_ice, impact_ice_b
//          R2: bolt_lightning, bolt_lightning_b, (skip), bolt_holy, (skip)
//        (the bottom row is irregular; only the clean cells are named, rest skip.)
//
//   projectiles_spell_vfx_lifecycle.jpg  (per-element cast/travel/impact sets, dark bg)
//     Sliced as a coarse reference grid; the catalog only needs the clean
//     travel-body + impact cells, named by element:
//        bolt_fire_lc, bolt_arcane_lc, bolt_ice_lc, bolt_lightning_lc, bolt_holy_lc
//        impact_fire, impact_arcane, impact_lightning  (+ cast_* flashes)
//
// The catalog (ProjectileArtCatalog) is keyed off these names; the maps are kept
// in lockstep. Unmatched names simply fall back to a default arrow/bolt sprite.
//
// RUNTIME LOAD (WebGL-safe): Resources cannot address Assets/Art. After slicing
// in place we COPY each sliced sheet (+ its .meta = sprite rects/names) into
// Assets/Resources/ProjectileIcons/. Re-run after editing any sheet.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ProjectileArtSlicer
    {
        private const string SrcDir = "Assets/Art/VFX/Projectiles";
        private const string ResDir = "Assets/Resources/ProjectileIcons";

        // One uniform grid laid over a normalized region of the sheet.
        // Coordinates are 0..1 with TOP-LEFT origin (the way we read the image);
        // they are flipped to Unity's bottom-left pixel rects at slice time.
        private struct Region
        {
            public float X, Y, W, H;   // normalized region bounds (top-left origin)
            public int Rows, Cols;
            public string[] Names;     // length == Rows*Cols, row-major; "" = skip cell
        }

        private struct Sheet
        {
            public string Path;
            public List<Region> Regions;
        }

        [MenuItem("Defenders/Art/Slice Projectile Icons")]
        public static void SliceMenu() { Run(); }

        // Public, batchmode-runnable:
        //   run-unity-method DeNelle.Editor.ProjectileArtSlicer.Run
        public static void Run()
        {
            var sheets = BuildSheetTable();
            int sliced = 0;
            foreach (var s in sheets)
            {
                if (!File.Exists(s.Path))
                {
                    Debug.LogWarning("[ProjectileArtSlicer] missing sheet (skipped): " + s.Path);
                    continue;
                }
                if (SliceSheet(s)) sliced++;
            }

            CopyToResources(sheets);
            AssetDatabase.Refresh();
            Debug.Log("[ProjectileArtSlicer] done — sliced " + sliced + " sheet(s); mirrored to " + ResDir);
        }

        // ---------------------------------------------------------------------
        // The layout table (names map 1:1 to ProjectileArtCatalog's lookups).
        // ---------------------------------------------------------------------
        private static List<Sheet> BuildSheetTable()
        {
            var list = new List<Sheet>();

            // ── Sheet 1: arrows (top, transparent) + magic bolts (bottom, dark) ──
            list.Add(new Sheet
            {
                Path = SrcDir + "/projectiles_arrows_magic.jpg",
                Regions = new List<Region>
                {
                    // Arrows — top ~52% of the image, a 4x4 grid.
                    new Region
                    {
                        X = 0f, Y = 0f, W = 1f, H = 0.52f,
                        Rows = 4, Cols = 4,
                        Names = new[]
                        {
                            "arrow_plain",  "arrow_fire",   "arrow_plain_b", "arrow_gold",
                            "arrow_red",    "arrow_gold_b", "arrow_steel",   "arrow_dark",
                            "arrow_gold_c", "arrow_fire_b", "arrow_dark_b",  "arrow_ice",
                            "arrow_steel_b","arrow_steel_c","arrow_fire_c",  "arrow_red_b",
                        }
                    },
                    // Magic bolts / impacts — bottom ~48%, a 2x5 reference grid.
                    // Top row: fireball, arcane(purple), ice-blue bolt, ice shard, ice shard.
                    // Bottom row: lightning bolt (wide), lightning/holy, fire streak.
                    new Region
                    {
                        X = 0f, Y = 0.52f, W = 1f, H = 0.48f,
                        Rows = 2, Cols = 5,
                        Names = new[]
                        {
                            "bolt_fire",     "bolt_arcane",  "bolt_ice_blue", "impact_ice",  "impact_ice_b",
                            "bolt_lightning","bolt_holy",    "bolt_fire_b",   "",            "",
                        }
                    },
                }
            });

            // ── Sheet 2: per-element cast/travel/impact lifecycle sets (dark bg) ──
            // Irregular labelled panels; sliced as a coarse 4x4 reference grid so
            // each element's TRAVEL body + an IMPACT burst land on a clean cell.
            // The catalog falls back to sheet-1 bolts for any element not matched,
            // so naming here is best-effort additive coverage.
            list.Add(new Sheet
            {
                Path = SrcDir + "/projectiles_spell_vfx_lifecycle.jpg",
                Regions = new List<Region>
                {
                    new Region
                    {
                        X = 0f, Y = 0f, W = 1f, H = 1f,
                        Rows = 4, Cols = 4,
                        Names = new[]
                        {
                            // R1: fire travel (long flame), -, arcane missile, impact_arcane
                            "bolt_fire_lc",     "",              "bolt_arcane_lc", "impact_arcane",
                            // R2: cast_fire, mid arcane, -, scepter set
                            "cast_fire",        "bolt_arcane_mid","",              "bolt_ice_lc",
                            // R3: lightning travel, -, holy/light beam, impact_lightning
                            "bolt_lightning_lc","",              "bolt_holy_lc",   "impact_lightning",
                            // R4: lightning mid, -, cast_lightning, nature/holy staff
                            "bolt_lightning_mid","",             "cast_lightning", "bolt_nature_lc",
                        }
                    },
                }
            });

            return list;
        }

        // ---------------------------------------------------------------------
        // Slice one sheet: importer -> Multiple, build rects from each region.
        // ---------------------------------------------------------------------
        private static bool SliceSheet(Sheet s)
        {
            var importer = AssetImporter.GetAtPath(s.Path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ProjectileArtSlicer] no TextureImporter for " + s.Path);
                return false;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Multiple;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.filterMode          = FilterMode.Bilinear;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.npotScale           = TextureImporterNPOTScale.None;

            int texW, texH;
            GetSourceSize(importer, s.Path, out texW, out texH);
            if (texW <= 0 || texH <= 0) { texW = 1024; texH = 1024; }

            var metas = new List<SpriteMetaData>();
            foreach (var r in s.Regions)
            {
                float regX = r.X * texW;
                float regH = r.H * texH;
                float regW = r.W * texW;
                // Region Y is given top-left; convert the region's TOP edge to a
                // bottom-left pixel Y for the whole region block.
                float regBottomY = (1f - (r.Y + r.H)) * texH;

                float cellW = regW / r.Cols;
                float cellH = regH / r.Rows;

                int idx = 0;
                for (int row = 0; row < r.Rows; row++)
                {
                    for (int col = 0; col < r.Cols; col++, idx++)
                    {
                        string name = idx < r.Names.Length ? r.Names[idx] : null;
                        if (string.IsNullOrEmpty(name)) continue;

                        float x = regX + col * cellW;
                        // Within the region, row 0 is the TOP -> highest pixel Y.
                        float y = regBottomY + (r.Rows - 1 - row) * cellH;

                        metas.Add(new SpriteMetaData
                        {
                            name      = name,
                            rect      = new Rect(x, y, cellW, cellH),
                            alignment = (int)SpriteAlignment.Center,
                            pivot     = new Vector2(0.5f, 0.5f)
                        });
                    }
                }
            }

#pragma warning disable 0618 // spritesheet is the stable batch API for grid slicing
            importer.spritesheet = metas.ToArray();
#pragma warning restore 0618

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[ProjectileArtSlicer] sliced " + Path.GetFileName(s.Path) +
                      " -> " + metas.Count + " sprites (" + texW + "x" + texH + ")");
            return true;
        }

        private static void GetSourceSize(TextureImporter importer, string path, out int w, out int h)
        {
            w = 0; h = 0;
            try { importer.GetSourceTextureWidthAndHeight(out w, out h); }
            catch { /* older API — fall through */ }
            if (w > 0 && h > 0) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) { w = tex.width; h = tex.height; }
        }

        // ---------------------------------------------------------------------
        // Mirror sliced sheets (+ .meta = sprite rects/names) into Resources so
        // the runtime catalog can Resources.LoadAll<Sprite> them (WebGL-safe).
        // ---------------------------------------------------------------------
        private static void CopyToResources(List<Sheet> sheets)
        {
            if (!AssetDatabase.IsValidFolder(ResDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "ProjectileIcons");
            }

            foreach (var s in sheets)
            {
                if (!File.Exists(s.Path)) continue;
                string fileName = Path.GetFileName(s.Path);
                string dst = ResDir + "/" + fileName;

                if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(s.Path, dst))
                    Debug.LogWarning("[ProjectileArtSlicer] copy-to-Resources failed: " + s.Path + " -> " + dst);
            }
        }
    }
}
