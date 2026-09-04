// =============================================================================
// ActionBarEmblemSlicer (WO-1359) - derive the action-bar face slice manifest
// from the emblem sheet's OWN ALPHA, so a new sheet is a drop-in.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)
//
// The owner authors the five calm-dock faces as ONE sheet. This tool finds the
// emblems by segmenting the sheet's alpha into islands, gives every island the
// SAME box (the largest island plus a small pad, centred on its own bounds) so all
// five render at one scale, and writes them out as NORMALIZED rects into the
// sibling .json that SpriteSheetSlices reads at runtime.
//
// ⭐ THE NAME ORDER BELOW IS THE ONLY THING TO EDIT WHEN A SHEET CHANGES.
// Islands are found left to right; FaceOrder says which face each one IS. The bar
// then looks its art up BY NAME, never by index - which is what stops a re-ordered
// sheet from quietly handing MANAGE the JOURNEY emblem, a swap that looks
// plausible on both faces and therefore ships.
//
// Deliberately NOT an AssetPostprocessor: this writes a second asset beside the
// texture, and doing that during import is how you get a reimport loop. It is a
// menu item, run once when new art lands.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ActionBarEmblemSlicer
    {
        private const string SheetAsset = "Assets/Resources/UI/ElarionMedieval/actionbar/actionbar-emblems.png";
        private const string ManifestAsset = "Assets/Resources/UI/ElarionMedieval/actionbar/actionbar-emblems.json";
        private const string ResourcesPath = "UI/ElarionMedieval/actionbar/actionbar-emblems";

        /// <summary>The sheet's emblems, LEFT TO RIGHT. Re-order this - and nothing else - if a
        /// future sheet arranges them differently.</summary>
        private static readonly string[] FaceOrder = { "build", "talk", "hero", "journey", "manage" };

        /// <summary>Alpha at or above this counts as art. Above the export noise floor, below any
        /// real edge pixel.</summary>
        private const byte AlphaFloor = 8;

        /// <summary>Breathing room around the shared box, in source pixels.</summary>
        private const int PadPx = 4;

        [MenuItem("Elarion/UI/Re-slice Action Bar Emblems")]
        public static void ReSlice()
        {
            string report;
            bool ok = Run(out report);
            if (ok) Debug.Log("ACTION_BAR_SLICE_OK - " + report);
            else Debug.LogError("ACTION_BAR_SLICE_FAIL: " + report);
        }

        /// <summary>Derive and write the manifest. Never throws; returns false with a reason.</summary>
        public static bool Run(out string report)
        {
            report = "";
            if (!File.Exists(SheetAsset)) { report = "no sheet at " + SheetAsset; return false; }

            // Decode the PNG bytes directly rather than reading the imported texture: the shipped
            // import settings are compressed and non-readable, and this must work without
            // toggling them (toggling them is how the shipped art ends up different).
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(SheetAsset)))
                { report = "could not decode " + SheetAsset; return false; }

                int w = tex.width, h = tex.height;
                Color32[] pixels = tex.GetPixels32();

                // Column occupancy, then left-to-right islands.
                var occupied = new bool[w];
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                        if (pixels[row + x].a >= AlphaFloor) occupied[x] = true;
                }
                List<int[]> islands = Segments(occupied);
                if (islands.Count != FaceOrder.Length)
                {
                    report = "found " + islands.Count + " alpha islands across the sheet but FaceOrder " +
                             "names " + FaceOrder.Length + ". Either the sheet changed shape or two emblems " +
                             "are touching - name the faces to match before slicing, never by guessing";
                    return false;
                }

                // Vertical bounds per island, then ONE shared box for all five so no face renders
                // larger than its neighbours.
                var boxes = new int[islands.Count][];   // x0, y0, x1, y1 (top-origin, inclusive)
                int maxW = 0, maxH = 0;
                for (int i = 0; i < islands.Count; i++)
                {
                    int x0 = islands[i][0], x1 = islands[i][1];
                    int y0 = -1, y1 = -1;
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * w;
                        bool any = false;
                        for (int x = x0; x <= x1 && !any; x++) any = pixels[row + x].a >= AlphaFloor;
                        if (!any) continue;
                        if (y0 < 0) y0 = y;
                        y1 = y;
                    }
                    if (y0 < 0) { report = "island " + i + " has no rows above the alpha floor"; return false; }
                    boxes[i] = new[] { x0, y0, x1, y1 };
                    maxW = Mathf.Max(maxW, x1 - x0 + 1);
                    maxH = Mathf.Max(maxH, y1 - y0 + 1);
                }

                int boxW = Mathf.Min(w, maxW + 2 * PadPx);
                int boxH = Mathf.Min(h, maxH + 2 * PadPx);

                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append(" \"_comment\": \"WO-1359 action-bar face emblems. GENERATED by ")
                  .Append("Elarion/UI/Re-slice Action Bar Emblems from the sheet's own alpha bounding boxes - ")
                  .Append("do not hand-edit. Rects are NORMALIZED 0..1 in Unity texture space (origin ")
                  .Append("BOTTOM-LEFT) so they survive any maxTextureSize downscale. Faces are keyed BY NAME; ")
                  .Append("the sheet's left-to-right order lives in ActionBarEmblemSlicer.FaceOrder and is the ")
                  .Append("only thing to edit if a new sheet re-orders the emblems.\",\n");
                sb.Append(" \"sheet\": \"").Append(ResourcesPath).Append("\",\n");
                sb.Append(" \"sourceWidth\": ").Append(w).Append(",\n");
                sb.Append(" \"sourceHeight\": ").Append(h).Append(",\n");
                sb.Append(" \"faces\": {\n");
                for (int i = 0; i < boxes.Length; i++)
                {
                    float cx = (boxes[i][0] + boxes[i][2] + 1) * 0.5f;
                    float cy = (boxes[i][1] + boxes[i][3] + 1) * 0.5f;   // top-origin
                    float px = Mathf.Clamp(cx - boxW * 0.5f, 0f, w - boxW);
                    float pyTop = Mathf.Clamp(cy - boxH * 0.5f, 0f, h - boxH);
                    float py = h - (pyTop + boxH);                        // -> bottom-origin
                    sb.Append("  \"").Append(FaceOrder[i]).Append("\": { ")
                      .Append("\"x\": ").Append(Num(px / w)).Append(", ")
                      .Append("\"y\": ").Append(Num(py / h)).Append(", ")
                      .Append("\"width\": ").Append(Num((float)boxW / w)).Append(", ")
                      .Append("\"height\": ").Append(Num((float)boxH / h)).Append(" }")
                      .Append(i == boxes.Length - 1 ? "\n" : ",\n");
                }
                sb.Append(" }\n}\n");

                File.WriteAllText(ManifestAsset, sb.ToString(), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ManifestAsset);
                report = FaceOrder.Length + " faces sliced from " + w + "x" + h + " into a shared " +
                         boxW + "x" + boxH + " box -> " + ManifestAsset;
                return true;
            }
            catch (Exception e)
            {
                report = e.GetType().Name + ": " + e.Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string Num(float v)
        {
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>Inclusive [start,end] runs of true in <paramref name="flags"/>.</summary>
        private static List<int[]> Segments(bool[] flags)
        {
            var runs = new List<int[]>();
            int start = -1;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] && start < 0) start = i;
                else if (!flags[i] && start >= 0) { runs.Add(new[] { start, i - 1 }); start = -1; }
            }
            if (start >= 0) runs.Add(new[] { start, flags.Length - 1 });
            return runs;
        }
    }
}
