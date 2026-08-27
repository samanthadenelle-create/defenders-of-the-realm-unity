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
        // WO-1218: the import authority for the ground textures, and the file that decides
        // whether a per-texture aniso level is honoured at all on the phone.
        private const string ImporterSrc = "Assets/Editor/TerrainLayerTextureImporter.cs";
        private const string QualitySrc  = "ProjectSettings/QualitySettings.asset";

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
                Case7_GroundSamplingIsAliasSafe(failures, notes, log);
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
                reason = "terrain-layer: 7 cases green" + noteStr;
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

        // -- Case 7: the ground can be SAMPLED without aliasing (WO-1218). --
        //
        // ## THE CAPTURED DEFECT
        //
        // Owner's device, Seeker build 2026.08.26.341419, tmp/screen-103219.png at the
        // Seeker's real 2670x1200: the whole meadow renders as a dense near-white
        // sparkle that gets WORSE with distance. Measured off that capture as the std
        // of luminance minus a 3x3 box blur - contrast at the ONE-PIXEL scale, which a
        // working mip chain must drive toward zero at range:
        //
        //     near ground (rows 1020-1180):  hp1 =  9.1
        //     mid  ground (rows  460- 620):  hp1 = 29.4     <- 3.2x WORSE further away
        //
        // Mipmaps were on the whole time. The hole was the ANISOTROPY RATIO: aniso 4
        // on a ground plane at grazing pitch selects the sharper 4:1 mip and then
        // undersamples the rest.
        //
        // ## WHY THIS CASE IS TWO ASSERTIONS AND NOT ONE
        //
        // Pinning the per-texture aniso alone would be a fix that can ship as a NO-OP.
        // Per-texture aniso is only honoured when the running quality tier's
        // anisotropicTextures is not Disable(0) - and this project ships a tier
        // (Seeker_Low) where it IS 0. So the tier Android defaults to is asserted too.
        // The desktop tier is ForceEnable(2), which is exactly why every editor and
        // desktop run rendered this ground at 16x and hid the defect for months.
        //
        // ## PROVING IT RED (WO-1138)
        //   * Put `aniso: 4` back into any one of the 16 .meta files -> this case fails
        //     naming that file and the value, while the other 15 still report 8.
        //   * Set anisotropicTextures back to 0 on the Android default tier in
        //     ProjectSettings/QualitySettings.asset -> this case fails with the no-op
        //     warning, even with all 16 textures at aniso 8.
        //   * Set anisoLevel back to 4 in TerrainLayerTextureImporter.cs -> this case
        //     fails, because a reimport would silently undo every meta above.
        //   * Delete a .meta -> this case FAILS. It does not skip. A guard that returned
        //     here on a missing file would land green having asserted nothing.
        private static void Case7_GroundSamplingIsAliasSafe(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // Ground is sampled at grazing angles across most of the frame. 4 was measured
            // to alias; 8 is the affordable step on a fill-rate-bound phone that already
            // takes 8 layers x 2 maps of fetches per pixel. If a fresh device capture at
            // the same pitch still shows mid-field hp1 far above the near-field value,
            // raise BOTH this number and the importer's - never one alone.
            const int MinAniso = 8;

            int checkedMetas = 0;
            for (int i = 0; i < TerrainLayerSet.Count; i++)
            {
                CheckOneMeta(TerrainLayerSet.BaseColorPath(i) + ".meta", MinAniso, failures, ref checkedMetas);
                CheckOneMeta(TerrainLayerSet.NormalPath(i) + ".meta", MinAniso, failures, ref checkedMetas);
            }

            int expectedMetas = TerrainLayerSet.Count * 2;
            if (checkedMetas != expectedMetas)
                failures.Add("case7 read " + checkedMetas + " of " + expectedMetas +
                             " terrain layer .meta files - every layer's BaseColor AND Normal must be " +
                             "alias-safe, and a meta that could not be read is a FAILURE, not a skip");

            // The importer is the authority on a REIMPORT. Metas alone would be silently
            // reverted the next time these PNGs are touched.
            if (!File.Exists(ImporterSrc))
            {
                failures.Add("missing " + ImporterSrc + " - the import authority for the ground " +
                             "textures is gone, so nothing keeps a reimport from restoring the " +
                             "aliasing aniso level");
            }
            else
            {
                string imp = File.ReadAllText(ImporterSrc);
                bool ok = false;
                for (int lvl = MinAniso; lvl <= 16; lvl++)
                    if (imp.IndexOf("anisoLevel = " + lvl + ";", StringComparison.Ordinal) >= 0) ok = true;
                if (!ok)
                    failures.Add(ImporterSrc + " does not set anisoLevel to at least " + MinAniso +
                                 " - a reimport would restore the grazing-angle aliasing captured in " +
                                 "tmp/screen-103219.png, and the .meta fix would ship as a no-op");
            }

            // THE ASSERTION THAT MAKES THE FIX NON-VACUOUS: the tier Android actually runs
            // must honour per-texture aniso at all.
            if (!File.Exists(QualitySrc))
            {
                failures.Add("missing " + QualitySrc + " - cannot prove the Android quality tier " +
                             "honours per-texture anisotropy, so the whole ground fix is unproven");
            }
            else
            {
                string[] qs = File.ReadAllLines(QualitySrc);
                int androidLevel = -1;
                for (int i = 0; i < qs.Length; i++)
                {
                    string t = qs[i].Trim();
                    if (!t.StartsWith("Android:", StringComparison.Ordinal)) continue;
                    if (int.TryParse(t.Substring("Android:".Length).Trim(), out int lv)) androidLevel = lv;
                    break;
                }

                if (androidLevel < 0)
                {
                    failures.Add(QualitySrc + " has no m_PerPlatformDefaultQuality Android entry - the " +
                                 "tier the phone boots into is unknown, so the aniso fix cannot be " +
                                 "proved to reach it");
                }
                else
                {
                    // Walk the quality level blocks in order and read the Nth
                    // anisotropicTextures. A level index past the end is itself a defect.
                    var aniso = new List<int>();
                    for (int i = 0; i < qs.Length; i++)
                    {
                        string t = qs[i].Trim();
                        if (!t.StartsWith("anisotropicTextures:", StringComparison.Ordinal)) continue;
                        if (int.TryParse(t.Substring("anisotropicTextures:".Length).Trim(), out int v))
                            aniso.Add(v);
                    }

                    if (androidLevel >= aniso.Count)
                    {
                        failures.Add(QualitySrc + ": Android defaults to quality level " + androidLevel +
                                     " but only " + aniso.Count + " level(s) exist - the phone falls back " +
                                     "to an unknown tier and no texture setting can be relied on");
                    }
                    else if (aniso[androidLevel] == 0)
                    {
                        failures.Add(QualitySrc + ": the Android default quality level " + androidLevel +
                                     " has anisotropicTextures: 0 (Disable) - per-texture anisotropy is " +
                                     "IGNORED there, so aniso " + MinAniso + " on the ground textures is a " +
                                     "NO-OP and the WO-1218 shimmer returns in full");
                    }
                    else
                    {
                        log.AppendLine("  case7: Android default quality level " + androidLevel +
                                       " has anisotropicTextures: " + aniso[androidLevel] +
                                       " (non-zero) - per-texture aniso is honoured on device");
                    }

                    // Not a failure: a tier that disables aniso entirely is a latent version
                    // of the same defect for any device that lands on it. Surfaced, not hidden.
                    for (int i = 0; i < aniso.Count; i++)
                        if (aniso[i] == 0)
                            notes.Add("quality level " + i + " has anisotropicTextures: 0 - any device " +
                                      "that boots into it renders the ground with NO anisotropy and will " +
                                      "shimmer exactly as WO-1218 captured");
                }
            }

            log.AppendLine("  case7: " + checkedMetas + " ground textures sampled alias-safe (mipmaps on, " +
                           "aniso >= " + MinAniso + ", no negative mip bias)");
        }

        /// <summary>
        /// Assert one terrain-layer texture .meta is sampled alias-safely. A meta that
        /// cannot be read is a FAILURE - never a silent skip.
        /// </summary>
        private static void CheckOneMeta(string metaPath, int minAniso, List<string> failures, ref int checkedMetas)
        {
            if (!File.Exists(metaPath))
            {
                failures.Add("missing import settings " + metaPath +
                             " - the ground sampling for this texture is unknown and therefore unproven");
                return;
            }

            string[] lines;
            try { lines = File.ReadAllLines(metaPath); }
            catch (Exception ex)
            {
                failures.Add("could not read " + metaPath + ": " + ex.Message);
                return;
            }

            checkedMetas++;
            int aniso = -1, mip = -1;
            float mipBias = 0f;
            bool sawMipBias = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("aniso:", StringComparison.Ordinal))
                    int.TryParse(t.Substring("aniso:".Length).Trim(), out aniso);
                else if (t.StartsWith("enableMipMap:", StringComparison.Ordinal))
                    int.TryParse(t.Substring("enableMipMap:".Length).Trim(), out mip);
                else if (t.StartsWith("mipBias:", StringComparison.Ordinal))
                    sawMipBias = float.TryParse(t.Substring("mipBias:".Length).Trim(),
                                                System.Globalization.NumberStyles.Float,
                                                System.Globalization.CultureInfo.InvariantCulture, out mipBias);
            }

            if (mip != 1)
                failures.Add(metaPath + ": enableMipMap is " + (mip < 0 ? "ABSENT" : mip.ToString()) +
                             " - without a mip chain the ground aliases at every distance");
            if (aniso < minAniso)
                failures.Add(metaPath + ": aniso is " + (aniso < 0 ? "ABSENT" : aniso.ToString()) +
                             ", below the " + minAniso + " the grazing-angle ground needs. This IS the " +
                             "WO-1218 shimmer: measured mid-field one-pixel contrast 29.4 vs 9.1 near " +
                             "field on tmp/screen-103219.png at aniso 4");
            if (sawMipBias && mipBias < 0f)
                failures.Add(metaPath + ": mipBias is " + mipBias.ToString("F2") +
                             " - a negative bias forces a sharper mip than the footprint warrants and " +
                             "re-creates the aliasing the aniso level was raised to remove");
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
