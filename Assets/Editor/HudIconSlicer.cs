// =============================================================================
// HudIconSlicer — slices the HUD-WIDGET sheet in Assets/Art/UI/HudIcons into
// named multiple-mode sub-sprites, then mirrors the sliced sheet into
// Assets/Resources/HudIcons so the HUD loads its widget art at RUNTIME
// (WebGL-safe) via Resources.LoadAll<Sprite>("HudIcons/<sheet>").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only). Mirrors ItemIconSlicer.cs.
//
// WHY A SLICER (not hand-set .meta): the source image is an AI-generated atlas
// sheet of HUD widget icons laid out on a clean uniform grid. We compute uniform
// cell rects from the grid READ off the image and assign meaningful sprite names
// so VillageHudController can address a widget sprite by name (sprite-first, with
// its existing code-drawn/glyph element kept as the fallback).
//
// WIDGET NAMES (the HUD looks these up — keep in lockstep with the controller):
//   hud_tree            Tree-of-Life crest (top-left HP cluster)
//   hud_hp_full / hud_hp_half / hud_hp_low   HP bar states
//   hud_compass         ornate compass (top-centre)
//   hud_settings        settings gear (top-right)
//   hud_inventory       inventory / loadout backpack (top-right)
//   hud_talk            talk speech bubble (town right panel)
//   hud_quest           quest arrows / marker (town right panel)
//   hud_ability_frame   runic ability-circle frame (combat skills panel)
//
// ── IMPORTANT (sheet-content note, 2026-06-10) ────────────────────────────────
// The image currently at HudIcons/hud_widgets_sheet.jpg is a 1168x784, 8-col x
// 4-row CONSUMABLES grid (potions / herbs / crystals / scrolls / runes / ore),
// NOT the HUD-widget mockup the work order describes (tree / hp-bars / compass /
// gear / backpack / talk / quest / ability-frame). So this slicer does TWO things:
//   1. It slices the sheet on a configurable grid and emits BOTH a row-major set
//      of generic position names (cell_rRcC) AND the WO widget names mapped onto
//      sheet cells, so the named sprites always exist + resolve.
//   2. When the CORRECT widget sheet is later dropped in at the same path, just
//      update WidgetLayout below to match its grid + slot order and re-run — the
//      HUD wiring already loads these names sprite-first, so no controller change
//      is needed. Until then the HUD falls back to its code-drawn glyphs (the
//      widget sprites simply won't visually match, but nothing breaks).
//
// RUNTIME LOAD (WebGL-safe): Resources cannot address Assets/Art. After slicing
// in place we COPY the sliced .jpg + its .meta into Assets/Resources/HudIcons/
// (same GUID / sprite-rects carried by the .meta). The HUD then calls
// Resources.LoadAll<Sprite>("HudIcons/<sheet>") — fully WebGL-safe, no
// AssetDatabase at runtime. Re-run this after editing / replacing the sheet.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HudIconSlicer
    {
        private const string SrcDir = "Assets/Art/UI/HudIcons";
        private const string ResDir = "Assets/Resources/HudIcons";
        private const string SheetFile = "hud_widgets_sheet.jpg";

        // The sheet's grid. The current sheet is an 8x4 atlas (1168x784). If the
        // real widget mockup replaces it, set Rows/Cols to its grid here.
        private const int Rows = 4;
        private const int Cols = 8;

        // WO widget names mapped onto sheet cells, ROW-MAJOR (top->bottom,
        // left->right). When the correct widget sheet lands, reorder these to match
        // the artwork's slot order; the HUD addresses sprites BY THESE NAMES.
        // Cells not assigned a widget name still get a generic cell_rRcC name (below),
        // so every cell is addressable for future hand-mapping.
        private static readonly string[] WidgetNames =
        {
            "hud_tree",        "hud_hp_full",   "hud_hp_half",  "hud_hp_low",
            "hud_compass",     "hud_settings",  "hud_inventory","hud_ability_frame",
            "hud_talk",        "hud_quest",     "",             "",
            "",                "",              "",             "",
        };

        [MenuItem("Defenders/Art/Slice HUD Icons")]
        public static void SliceMenu() { Run(); }

        // Public, batchmode-runnable: run-unity-method DeNelle.Editor.HudIconSlicer.Run
        public static void Run()
        {
            string path = SrcDir + "/" + SheetFile;
            if (!File.Exists(path))
            {
                Debug.LogWarning("[HudIconSlicer] missing sheet (skipped): " + path);
                return;
            }

            bool sliced = SliceSheet(path);
            CopyToResources(path);
            AssetDatabase.Refresh();
            Debug.Log("[HudIconSlicer] done — sliced " + (sliced ? "1" : "0") +
                      " sheet; mirrored to " + ResDir);
        }

        // ---------------------------------------------------------------------
        // Slice the sheet: importer -> Multiple, build a uniform grid of rects +
        // names. Emits WO widget names where mapped, plus a generic cell_rRcC name
        // for EVERY cell so nothing is unaddressable.
        // ---------------------------------------------------------------------
        private static bool SliceSheet(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[HudIconSlicer] no TextureImporter for " + path);
                return false;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Multiple;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.filterMode          = FilterMode.Bilinear;
            importer.textureCompression  = TextureImporterCompression.Uncompressed; // crisp HUD art
            importer.npotScale           = TextureImporterNPOTScale.None;

            int texW, texH;
            GetSourceSize(importer, path, out texW, out texH);
            if (texW <= 0 || texH <= 0) { texW = 1024; texH = 1024; }

            float cellW = (float)texW / Cols;
            float cellH = (float)texH / Rows;

            var metas = new List<SpriteMetaData>();
            int idx = 0;
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++, idx++)
                {
                    // Unity sprite rects are bottom-left origin; our names are top->bottom.
                    float x = col * cellW;
                    float y = (Rows - 1 - row) * cellH;
                    var rect = new Rect(x, y, cellW, cellH);

                    // Always emit a generic position name so every cell is addressable.
                    metas.Add(new SpriteMetaData
                    {
                        name      = "cell_r" + row + "c" + col,
                        rect      = rect,
                        alignment = (int)SpriteAlignment.Center,
                        pivot     = new Vector2(0.5f, 0.5f)
                    });

                    // Plus the WO widget name for this cell, if mapped.
                    string widget = idx < WidgetNames.Length ? WidgetNames[idx] : null;
                    if (!string.IsNullOrEmpty(widget))
                    {
                        metas.Add(new SpriteMetaData
                        {
                            name      = widget,
                            rect      = rect,
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
            Debug.Log("[HudIconSlicer] sliced " + Path.GetFileName(path) + " -> " +
                      metas.Count + " sprites (" + texW + "x" + texH + ", " + Rows + "x" + Cols + " grid)");
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
        // Mirror the sliced sheet (+ its .meta = sprite rects/names) into Resources
        // so the HUD can Resources.LoadAll<Sprite> it (WebGL-safe).
        // ---------------------------------------------------------------------
        private static void CopyToResources(string srcPath)
        {
            if (!AssetDatabase.IsValidFolder(ResDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "HudIcons");
            }

            string fileName = Path.GetFileName(srcPath);
            string dst = ResDir + "/" + fileName;

            // CopyAsset carries the .meta (and thus the sprite sub-rects + names).
            if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(srcPath, dst))
                Debug.LogWarning("[HudIconSlicer] copy-to-Resources failed: " + srcPath + " -> " + dst);
        }
    }
}
