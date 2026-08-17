// =============================================================================
// TerrainLayerRegression [terrain-layer]   Marker: TERRAIN_LAYER_OK / TERRAIN_LAYER_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Registered in DataRegression.RunAll.
//
// WHAT THIS PINS (WO-1101, under WO-1044 biome identity canon, owner-approved 2026-08-17):
//
//   1. THE GROUND HAS ART AT ALL. Every generated .terrainlayer resolves a real
//      diffuseTexture AND a real normalMapTexture. This case alone would have caught
//      the state this ticket found: five layers whose diffuse was a 64x64 procedural
//      solid colour embedded in ExteriorTerrainData.asset, and m_NormalMapTexture
//      {fileID: 0} on ALL FIVE. Nobody noticed for months because "the ground looks
//      flat" is an opinion and an opinion cannot fail a gate.
//
//   2. THE ART IS TRACKED. Every referenced texture lives under
//      Assets/Generated/Terrain/Layers/ — never under Assets/Blink/ or
//      Assets/polyperfect/, which are gitignored with ZERO tracked files. A layer
//      pointing into a gitignored pack renders on the authoring machine and is
//      colourless on every other clone: the "pink floor" failure class (CLAUDE.md §12),
//      which cost three guess-cycles the last time it shipped.
//
//   3. THE COLOURBLIND GATE IS A MEASUREMENT, NOT AN OPINION. The owner is red/green
//      colourblind, so biomes must separate by VALUE, not hue. This asserts the measured
//      Rec.709 luminance of each march's ground against its authored target, and a
//      minimum ΔL between ADJACENT marches on the compass cycle. Today's shipped tints
//      FAIL it and always did: grass L=0.447 vs stone L=0.521 is ΔL 0.074, so Goldfields
//      and Stoneback were near-indistinguishable in greyscale.
//
//   4. ASHWOOD IS NOT RE-INVERTED. WO-1044 §1 authors Ashwood as near-black trunks on a
//      PALE ground ("ink on ash"); the shipped Exterior_Dead layer was L=0.176 — dark
//      ground, the exact opposite. An explicit floor keeps a future "restore the dark
//      ash" edit from silently undoing ratified canon.
//
//   5. THERE IS EXACTLY ONE LAYER-INDEX AUTHORITY. The builder and the runtime repaint
//      must both size from TerrainLayerSet.Count, and neither may re-declare its own
//      index table. The runtime repaint is the only splat the player sees on device, so
//      a drift here is a device-only defect that no editor gate can see.
//
// LUMINANCE IS MEASURED FROM THE PNG BYTES, not from the imported Texture2D: the curated
// textures import with isReadable=false (runtime memory), so GetPixels would throw. Reading
// the file and decoding into a scratch Texture2D always works in the editor and measures
// exactly what a clone would get.
//
// NO HOLLOW PASSES (CLAUDE.md §12): zero layers found, or an unreadable PNG, is a FAIL.
// A suite that found nothing to look at has not passed — it has not run.
//
// Standalone batch entry:
//   -Method DeNelle.Editor.Regression.TerrainLayerRegression.RunStandalone
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core.World;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class TerrainLayerRegression
    {
        private const string BuilderSrc = "Assets/Editor/ExteriorTerrainBuilder.cs";
        private const string RuntimeSrc = "Assets/_Modules/Village/World/WorldSceneLoader.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TERRAIN LAYERS (curated ground art + the colourblind value gate) ---");
            log.AppendLine(TerrainLayerSet.Manifest());

            try
            {
                Case1_EveryLayerHasCuratedBaseColorAndNormal(failures, notes, log);
                Case2_AllArtIsTrackedNeverAGitignoredPack(failures, notes, log);
                Case3_MeasuredLuminanceMatchesTheAuthoredTargets(failures, notes, log);
                Case4_AdjacentMarchesSeparateInGreyscale(failures, notes, log);
                Case5_AshwoodGroundIsPaleNotDark(failures, notes, log);
                Case6_OneLayerIndexAuthority(failures, notes, log);
            }
            catch (Exception ex)
            {
                // The stack is the point of a throwing suite (CLAUDE.md §12): without it the
                // failure line names only this catch site and the next reader has to guess.
                failures.Add($"[terrain-layer] suite THREW: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            string noteStr = notes.Count > 0 ? " | " + string.Join("; ", notes) : "";
            if (failures.Count == 0)
            {
                reason = "terrain-layer: 6 cases green" + noteStr;
                Debug.Log(log.ToString() + "TERRAIN_LAYER_OK");
                return true;
            }

            reason = "terrain-layer: " + string.Join("; ", failures) + noteStr;
            Debug.LogError(log.ToString() + "TERRAIN_LAYER_FAIL: " + reason);
            return false;
        }

        public static void RunStandalone()
        {
            bool ok = Run(out string reason);
            Debug.Log(ok ? "TERRAIN_LAYER_OK " + reason : "TERRAIN_LAYER_FAIL " + reason);
        }

        // ── Case 1 — the ground has art, and it has relief. ─────────────────────
        private static void Case1_EveryLayerHasCuratedBaseColorAndNormal(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            if (TerrainLayerSet.Layers.Length != TerrainLayerSet.Count)
            {
                failures.Add("TerrainLayerSet.Layers has " + TerrainLayerSet.Layers.Length +
                             " entries but Count says " + TerrainLayerSet.Count);
                return;
            }

            int checkedCount = 0;
            for (int i = 0; i < TerrainLayerSet.Count; i++)
            {
                var def = TerrainLayerSet.Layers[i];
                string bc = TerrainLayerSet.BaseColorPath(i);
                string nm = TerrainLayerSet.NormalPath(i);

                if (!File.Exists(bc)) failures.Add("layer " + i + " '" + def.Name + "': BaseColor MISSING at " + bc);
                if (!File.Exists(nm)) failures.Add("layer " + i + " '" + def.Name + "': Normal MISSING at " + nm);

                // If a bake has already run, the generated .terrainlayer must carry both maps.
                string lp = TerrainLayerSet.TerrainLayerPath(i);
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
                if (layer == null)
                {
                    notes.Add("no baked .terrainlayer yet for '" + def.Name + "' (bake pending)");
                }
                else
                {
                    if (layer.diffuseTexture == null)
                        failures.Add("'" + def.Name + "'.terrainlayer has NULL diffuseTexture");
                    if (layer.normalMapTexture == null)
                        failures.Add("'" + def.Name + "'.terrainlayer has NULL normalMapTexture " +
                                     "(this is the 'ground reads flat' defect — all five shipped layers had it)");
                    if (layer.tileSize.x <= 0.01f)
                        failures.Add("'" + def.Name + "'.terrainlayer has zero tileSize");
                }
                checkedCount++;
            }

            if (checkedCount == 0) failures.Add("no layers to check — the suite did not run");
            log.AppendLine("  case1: inspected " + checkedCount + " layer(s)");
        }

        // ── Case 2 — no layer may point into a gitignored pack. ─────────────────
        private static void Case2_AllArtIsTrackedNeverAGitignoredPack(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            string[] banned = { "Assets/Blink/", "Assets/polyperfect/", "Assets/Quaternius/" };
            int inspected = 0;

            for (int i = 0; i < TerrainLayerSet.Count; i++)
            {
                string lp = TerrainLayerSet.TerrainLayerPath(i);
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
                if (layer == null) continue;
                inspected++;

                foreach (var tex in new Texture[] { layer.diffuseTexture, layer.normalMapTexture })
                {
                    if (tex == null) continue;
                    string p = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(p)) continue;
                    p = p.Replace('\\', '/');
                    foreach (var b in banned)
                        if (p.StartsWith(b, StringComparison.OrdinalIgnoreCase))
                            failures.Add("layer '" + TerrainLayerSet.Layers[i].Name + "' references '" + p +
                                         "' — that pack is GITIGNORED (zero tracked files). It renders here " +
                                         "and is colourless on every other clone.");
                    if (!p.StartsWith(TerrainLayerSet.TextureFolder + "/", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        notes.Add("layer '" + TerrainLayerSet.Layers[i].Name + "' texture is outside " +
                                  TerrainLayerSet.TextureFolder + ": " + p);
                }
            }
            log.AppendLine("  case2: inspected " + inspected + " baked layer(s) for gitignored art");
        }

        // ── Case 3 — measured value vs authored target. ─────────────────────────
        private static void Case3_MeasuredLuminanceMatchesTheAuthoredTargets(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            int measured = 0;
            for (int i = 0; i < TerrainLayerSet.Count; i++)
            {
                var def = TerrainLayerSet.Layers[i];
                float l;
                if (!TryMeasureLuminance(TerrainLayerSet.BaseColorPath(i), out l))
                {
                    failures.Add("could not measure luminance of '" + def.Name + "' at " +
                                 TerrainLayerSet.BaseColorPath(i));
                    continue;
                }
                measured++;
                log.AppendLine("  " + def.Name.PadRight(18) + " L=" + l.ToString("0.000") +
                               " (target " + def.TargetLuminance.ToString("0.00") + ")");
                if (Mathf.Abs(l - def.TargetLuminance) > TerrainLayerSet.LuminanceTolerance)
                    failures.Add("'" + def.Name + "' measures L=" + l.ToString("0.000") +
                                 " but is authored at " + def.TargetLuminance.ToString("0.00") +
                                 " (tolerance ±" + TerrainLayerSet.LuminanceTolerance.ToString("0.00") + ")");
            }
            if (measured == 0) failures.Add("measured zero textures — the suite did not run");
            log.AppendLine("  case3: measured " + measured + " basecolor(s)");
        }

        // ── Case 4 — THE COLOURBLIND GATE, as arithmetic. ───────────────────────
        // The four marches sit on a compass CYCLE (E→N→W→S→E), so every march is adjacent
        // to two others and the four values have to alternate. Hue is not allowed to carry
        // any of this.
        private static void Case4_AdjacentMarchesSeparateInGreyscale(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            var cycle = TerrainLayerSet.CompassCycle;
            var lum = new Dictionary<RegionId, float>();

            foreach (var r in cycle)
            {
                int idx = TerrainLayerSet.PrimaryLayerFor(r);
                float l;
                if (!TryMeasureLuminance(TerrainLayerSet.BaseColorPath(idx), out l))
                {
                    failures.Add("march " + r + ": cannot measure its primary ground layer '" +
                                 TerrainLayerSet.Layers[idx].Name + "'");
                    continue;
                }
                lum[r] = l;
            }

            if (lum.Count != cycle.Length)
            {
                failures.Add("only " + lum.Count + "/" + cycle.Length + " march grounds measurable — cannot judge ΔL");
                return;
            }

            int pairs = 0;
            for (int i = 0; i < cycle.Length; i++)
            {
                var a = cycle[i];
                var b = cycle[(i + 1) % cycle.Length];   // wraps — S is adjacent to E
                float d = Mathf.Abs(lum[a] - lum[b]);
                pairs++;
                log.AppendLine("  ΔL " + a + "(" + lum[a].ToString("0.000") + ") ↔ " +
                               b + "(" + lum[b].ToString("0.000") + ") = " + d.ToString("0.000"));
                if (d < TerrainLayerSet.MinAdjacentMarchDeltaL)
                    failures.Add("ADJACENT marches " + a + " and " + b + " are only ΔL " + d.ToString("0.000") +
                                 " apart (minimum " + TerrainLayerSet.MinAdjacentMarchDeltaL.ToString("0.00") +
                                 ") — they will not separate in greyscale, which is how the owner reads them.");
            }
            if (pairs != 4) failures.Add("expected 4 adjacent march pairs on the compass cycle, checked " + pairs);
        }

        // ── Case 5 — Ashwood stays PALE (WO-1044 §1 "ink on ash"). ──────────────
        private static void Case5_AshwoodGroundIsPaleNotDark(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            const float PaleFloor = 0.50f;
            int idx = TerrainLayerSet.PrimaryLayerFor(RegionId.Ashwood);
            float l;
            if (!TryMeasureLuminance(TerrainLayerSet.BaseColorPath(idx), out l))
            {
                failures.Add("cannot measure Ashwood's ground layer");
                return;
            }
            log.AppendLine("  case5: Ashwood ground L=" + l.ToString("0.000") + " (floor " + PaleFloor + ")");
            if (l < PaleFloor)
                failures.Add("Ashwood ground measures L=" + l.ToString("0.000") + ", below the pale floor " +
                             PaleFloor + ". WO-1044 §1 authors Ashwood as near-black trunks on a PALE powdery " +
                             "ground; the pre-WO-1101 code shipped L=0.176 — the inverse of canon. " +
                             "Put the darkness in the trunks, not the floor.");
        }

        // ── Case 6 — one authority for the layer indices. ───────────────────────
        private static void Case6_OneLayerIndexAuthority(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // The runtime repaint must not carry bare layer literals again. It is the only
            // splat the player sees on device, so this drift is device-only and silent.
            if (!File.Exists(RuntimeSrc)) { failures.Add("missing " + RuntimeSrc); return; }
            if (!File.Exists(BuilderSrc)) { failures.Add("missing " + BuilderSrc); return; }

            string runtime = File.ReadAllText(RuntimeSrc);
            string builder = File.ReadAllText(BuilderSrc);

            if (runtime.IndexOf("TerrainLayerSet.", StringComparison.Ordinal) < 0)
                failures.Add(RuntimeSrc + " does not reference TerrainLayerSet — the DEF-108 runtime repaint " +
                             "has gone back to hardcoded layer indices. That mispaints the ground ON DEVICE " +
                             "and nowhere an editor gate can see.");

            if (builder.IndexOf("TerrainLayerSet.", StringComparison.Ordinal) < 0)
                failures.Add(BuilderSrc + " does not reference TerrainLayerSet — the bake has grown its own " +
                             "layer table again.");

            // The builder must not re-declare its own count.
            if (builder.IndexOf("LayerCount = 5", StringComparison.Ordinal) >= 0 ||
                builder.IndexOf("LayerCount = 8", StringComparison.Ordinal) >= 0)
                failures.Add(BuilderSrc + " re-declares a literal LayerCount — the count belongs to " +
                             "TerrainLayerSet.Count alone.");

            log.AppendLine("  case6: both splat authorities read the shared TerrainLayerSet contract");
        }

        // ── Luminance measurement ───────────────────────────────────────────────
        /// <summary>
        /// Mean Rec.709 luminance of a PNG on disk, sampled on a grid. Decoded from the file
        /// bytes rather than from the imported asset because the curated textures import with
        /// isReadable=false — GetPixels on those throws, and a suite that throws on the thing
        /// it is meant to measure is worse than no suite.
        /// </summary>
        private static bool TryMeasureLuminance(string assetPath, out float luminance)
        {
            luminance = 0f;
            if (!File.Exists(assetPath)) return false;

            Texture2D tex = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(assetPath);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes, false)) return false;

                int w = tex.width, h = tex.height;
                if (w < 2 || h < 2) return false;
                var px = tex.GetPixels32();

                double sum = 0.0; int n = 0;
                int step = Mathf.Max(1, w / 128);
                for (int y = 0; y < h; y += step)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x += step)
                    {
                        var c = px[row + x];
                        sum += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0;
                        n++;
                    }
                }
                if (n == 0) return false;
                luminance = (float)(sum / n);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[terrain-layer] luminance measure failed for '" + assetPath + "': " + ex.Message);
                return false;
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
