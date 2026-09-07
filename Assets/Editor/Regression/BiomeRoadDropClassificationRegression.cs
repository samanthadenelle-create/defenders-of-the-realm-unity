// =============================================================================
// BiomeRoadDropClassificationRegression [biome-drop-classification]
//   Marker: BIOME_DROP_CLASS_OK / BIOME_DROP_CLASS_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Registered in DataRegression.RunAll.
// WO-1604, from F8 seq 4703 (device, 2026-09-07, Main_Castle_Overworld):
//
//   "[Flow:BiomeRoads] drop promised Ashwood but the hero landed at
//    (0.00, 0.08, 50.00), which ZoneManager classifies as Elarion."
//
// -----------------------------------------------------------------------------
// THE INVARIANT THIS SUITE OWNS, IN ONE SENTENCE:
//
//   ⛔ A BIOME DROP IS EITHER CLASSIFIED AS THE REGION ITS PROMPT NAMES, OR IT DOES
//      NOT EXIST. There is no third state, and in particular there is no "seated and
//      then complained about after the player had already been teleported".
//
// TWO AUTHORITIES USED TO ANSWER "WHERE DOES ASHWOOD START", AND NOTHING MADE THEM AGREE:
//   (a) BiomeRoads.EdgeFraction x the MEASURED origin-to-edge reach -- which actually
//       answers a DIFFERENT question ("how far out does the walkable world go").
//   (b) ZoneManager.GetZone -- an origin-relative home box plus a normalised
//       dominant-axis split. This is THE classifier: harvest, raids, threat scaling and
//       the arrival check all call it, so it is the one that decides what the player is
//       told they are standing in.
// WO-1604 makes (b) the owner and (a) a consumer: ResolveDrops now PROBES the classifier
// for the region's boundary along its cardinal (BiomeRoads.TryFindRegionBoundary, a
// bisection against GetZone itself -- not a copy of its constants) and refuses any point
// the classifier disagrees with. This suite is what keeps that true.
//
// -----------------------------------------------------------------------------
// WHY CASE 1 IS SHAPED THE WAY IT IS (the RED-first case the ticket asked for).
//
// A world whose north reach is 62.5m makes the OLD derivation produce exactly the
// coordinate in the capture: 62.5 x 0.8 = 50.0, and ZoneManager's home box reaches 52,
// so (0, y, 50) is Elarion while the prompt says Ashwood. Case 1 hands the resolver that
// world. On the pre-WO-1604 code it FAILS with the capture's own sentence; on the fixed
// code the boundary floor lifts the seat clear and it passes. That is the ticket's
// "pin: RED first with the current (0,0,50)" expressed as arithmetic rather than as a
// promise in a RESULT file.
//
// ⚠ AND A FINDING THAT MUST NOT BE LOST: THAT WORLD IS NOT THE ONE THAT SHIPPED. The hub
// world was MEASURED by this very system and the measurement is on disk --
// Builds/starter-settlement-proof-r4.log:19075, "[Flow:BiomeRoads] world bounds MEASURED
// from 1 terrain(s): centre (0.00, 17.00, 0.00) size (1000.00, 42.00, 1000.00)
// (half-extents (500.00, 21.00, 500.00))". North reach is therefore 500m, matching the
// scene (Main_Castle_Overworld's ExteriorTerrain transform sits at (-500, -4, -500)) and
// ExteriorTerrainBuilder's TerrainSizeXZ=1000 / TerrainCenterZ=0.
// So the live Ashwood drop derives at z=400 and CANNOT be (0, y, 50). The capture's
// hero had never been moved: the crossing's warp did not land, the 3s settle loop timed
// out, and VerifyArrival judged wherever the hero happened to be standing. That is a
// SEPARATE defect in SceneTransitionTrigger / HeroLocomotion's lane. Case 5 pins the
// instrumentation that now tells the two apart, because the missing drift number is the
// entire reason this ticket was minted against the wrong system.
//
// NO HOLLOW PASSES (CLAUDE.md sec.12): a world that produces zero drops, or a source file
// that cannot be read, is a FAIL here -- a suite that found nothing to look at has not
// passed, it has not run.
//
// Standalone batch entry:
//   -Method DeNelle.Editor.Regression.BiomeRoadDropClassificationRegression.RunStandalone
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Editor.Regression
{
    public static class BiomeRoadDropClassificationRegression
    {
        private const string CoreSrc     = "Assets/_Modules/Core/World/BiomeRoads.cs";
        private const string InjectorSrc = "Assets/_Modules/Village/World/HollowRoadsDropInjector.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- BIOME DROP CLASSIFICATION (every drop is its own region, or it does not exist) ---");

            try
            {
                Case1_TheCaptureGeometryNoLongerMislabelsAshwood(failures, notes, log);
                Case2_NoRoomForARegionMeansRefusedNotMislabelled(failures, notes, log);
                Case3_EveryDropClassifiesAsItsRegionAcrossManyWorlds(failures, notes, log);
                Case4_TheBoundaryIsAskedOfZoneManagerNotCopied(failures, notes, log);
                Case5_ArrivalFailuresNameThePromisedPointAndTheDrift(failures, notes, log);
            }
            catch (Exception ex)
            {
                // The stack is the point of a throwing suite (CLAUDE.md sec.12).
                failures.Add($"[biome-drop-classification] suite THREW: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            string noteStr = notes.Count > 0 ? " | " + string.Join("; ", notes) : "";
            if (failures.Count == 0)
            {
                reason = "biome-drop-classification: 5 cases green" + noteStr;
                Debug.Log(log.ToString() + "BIOME_DROP_CLASS_OK");
                return true;
            }

            reason = "biome-drop-classification: " + string.Join("; ", failures) + noteStr;
            Debug.LogError(log.ToString() + "BIOME_DROP_CLASS_FAIL: " + reason);
            return false;
        }

        public static void RunStandalone()
        {
            bool ok = Run(out string reason);
            Debug.Log(ok ? "BIOME_DROP_CLASS_OK " + reason : "BIOME_DROP_CLASS_FAIL " + reason);
        }

        // ── Case 1 — the exact geometry from the capture. ───────────────────────
        // North reach 62.5m: the old `reach = edgeReach * EdgeFraction` puts Ashwood's
        // drop at (0, y, 50.0), which ZoneManager calls Elarion. RED before WO-1604.
        private static void Case1_TheCaptureGeometryNoLongerMislabelsAshwood(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // Asymmetric on purpose: only the NORTH side is squeezed, so the other three
            // regions stay ordinary and a failure here can only be about Ashwood.
            // max.z = 62.5 => 62.5 * EdgeFraction(0.8) = 50.0, the capture's coordinate.
            var bounds = BoundsFromReach(600f, 600f, 62.5f, 600f);

            var drops = BiomeRoads.ResolveDrops(bounds);
            if (drops.Count == 0)
            {
                failures.Add("[biome-drop-capture] the capture-geometry world produced ZERO drops. It squeezes " +
                             "only the north side; the other three regions have 600m of room each and must still " +
                             "get their roads. Zero drops means the resolver is refusing everything, which is a " +
                             "different bug wearing this fix's clothes.");
                return;
            }

            // The old-code point, computed here so the failure line can name it explicitly.
            float legacyZ = 62.5f * BiomeRoads.EdgeFraction;
            RegionId legacyClass = ZoneManager.GetZone(new Vector3(0f, 0f, legacyZ));
            if (legacyClass == RegionId.Ashwood)
            {
                failures.Add($"[biome-drop-capture] this case cannot prove anything: ZoneManager now classifies " +
                             $"(0, 0, {legacyZ:0.##}) as Ashwood, so the pre-fix derivation would have been " +
                             "correct and the RED half of this pin has evaporated. The home-zone geometry moved - " +
                             "re-derive the squeeze so the legacy seat lands inside the home zone again, do not " +
                             "delete the case.");
            }

            bool sawAshwood = false;
            foreach (var d in drops)
            {
                RegionId cls = ZoneManager.GetZone(d.Point);
                if (cls != d.Region)
                {
                    failures.Add($"[biome-drop-capture] drop for '{d.Region}' derived to {d.Point}, which " +
                                 $"ZoneManager classifies as '{cls}'. This is F8 seq 4703 exactly: the prompt " +
                                 "names one region and the classifier names another. A drop whose point does not " +
                                 "classify as its own region must be REFUSED, never seated. Derivation: " +
                                 d.Derivation);
                }
                if (d.Region == RegionId.Ashwood)
                {
                    sawAshwood = true;
                    if (Mathf.Abs(d.Point.z - legacyZ) < 0.01f)
                        failures.Add($"[biome-drop-capture] the Ashwood drop sits at the pre-fix seat " +
                                     $"z={d.Point.z:0.##} - the edge-fraction reach is still the only thing " +
                                     "deciding the point, so ZoneManager's boundary is not being consulted at all.");
                }
            }

            if (!sawAshwood)
            {
                // Refusal is an acceptable outcome ONLY if the region genuinely has no room; here it
                // has 10.5m past the boundary, so a refusal means the clearance floor overshot.
                failures.Add("[biome-drop-capture] Ashwood got NO drop in the capture-geometry world. There IS " +
                             "room north of the home-zone boundary out to 62.5m, so this world must yield a " +
                             "correctly-classified Ashwood drop, not a refusal. Suspect the clearance being " +
                             "scaled off the boundary instead of off the remaining room.");
            }

            notes.Add($"[biome-drop-capture] north reach 62.5m: legacy seat z={legacyZ:0.##} classifies " +
                      $"'{legacyClass}', {drops.Count} drop(s) all classify as their own region");
            log.AppendLine($"  capture geometry: legacy z={legacyZ:0.##} -> {legacyClass}; {drops.Count} drops verified");
        }

        // ── Case 2 — a region with NO room is refused, not mislabelled. ─────────
        private static void Case2_NoRoomForARegionMeansRefusedNotMislabelled(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // North reach 30m: entirely inside ZoneManager's home box, so NOTHING on the +Z
            // axis in this world can honestly be called Ashwood.
            var bounds = BoundsFromReach(600f, 600f, 30f, 600f);

            if (ZoneManager.GetZone(new Vector3(0f, 0f, 30f)) == RegionId.Ashwood)
            {
                failures.Add("[biome-drop-noroom] (0, 0, 30) already classifies as Ashwood, so this world does " +
                             "not actually starve the region and the case proves nothing. Re-derive the squeeze " +
                             "against the current home-zone geometry.");
                return;
            }

            var drops = BiomeRoads.ResolveDrops(bounds);

            foreach (var d in drops)
            {
                if (d.Region == RegionId.Ashwood)
                    failures.Add($"[biome-drop-noroom] a world with NO Ashwood ground still produced an Ashwood " +
                                 $"drop at {d.Point} (classified " +
                                 $"'{ZoneManager.GetZone(d.Point)}'). A door labelled with a biome the world does " +
                                 "not contain is the exact lie WO-1604 closes - it must be refused.");

                RegionId cls = ZoneManager.GetZone(d.Point);
                if (cls != d.Region)
                    failures.Add($"[biome-drop-noroom] surviving drop '{d.Region}' at {d.Point} classifies as " +
                                 $"'{cls}'.");
            }

            // The refusal must be SURGICAL: the three regions that DO have room keep their roads.
            // A resolver that bails out entirely would pass the assertion above while silently
            // closing the whole tunnel, which is a worse outcome than the bug.
            int expected = BiomeRoads.DropRegions.Length - 1;
            if (drops.Count != expected)
                failures.Add($"[biome-drop-noroom] expected exactly {expected} drops (all but Ashwood) but got " +
                             $"{drops.Count}. The refusal must remove ONE road, not collapse the tunnel: the " +
                             "other three regions have 600m of room each.");

            notes.Add($"[biome-drop-noroom] starved north side -> {drops.Count}/{BiomeRoads.DropRegions.Length} " +
                      "roads, Ashwood refused");
            log.AppendLine($"  no-room world: {drops.Count} drops, Ashwood correctly absent");
        }

        // ── Case 3 — the invariant across a spread of worlds. ───────────────────
        private static void Case3_EveryDropClassifiesAsItsRegionAcrossManyWorlds(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            var worlds = new List<KeyValuePair<string, Bounds>>
            {
                // The LIVE hub, read at source: Assets/Scenes/Main_Castle_Overworld.unity seats
                // ExteriorTerrain at (-500, -4, -500) with a 1000x1000 TerrainData
                // (ExteriorTerrainBuilder TerrainSizeXZ=1000, TerrainCenterZ=0). This row is the
                // shipped geometry, not a synthetic one.
                new KeyValuePair<string, Bounds>("live 1000m hub", BoundsFromReach(500f, 500f, 500f, 500f)),
                new KeyValuePair<string, Bounds>("doubled world",  BoundsFromReach(1000f, 1000f, 1000f, 1000f)),
                new KeyValuePair<string, Bounds>("off-centre",     new Bounds(new Vector3(120f, 0f, -80f),
                                                                             new Vector3(1000f, 42f, 1000f))),
                new KeyValuePair<string, Bounds>("non-square",     BoundsFromReach(900f, 300f, 240f, 700f)),
                new KeyValuePair<string, Bounds>("barely-clear",   BoundsFromReach(70f, 70f, 70f, 70f)),
            };

            int totalDrops = 0;
            foreach (var w in worlds)
            {
                var drops = BiomeRoads.ResolveDrops(w.Value);
                totalDrops += drops.Count;

                foreach (var d in drops)
                {
                    RegionId cls = ZoneManager.GetZone(d.Point);
                    if (cls != d.Region)
                        failures.Add($"[biome-drop-worlds] in the '{w.Key}' world, the drop for '{d.Region}' " +
                                     $"derived to {d.Point}, which ZoneManager classifies as '{cls}'. " +
                                     $"Derivation: {d.Derivation}");

                    // Cardinal seating is what makes the classification unambiguous in the first
                    // place: on an exact diagonal the split is a coin-flip between two regions.
                    bool onAxis = (Mathf.Abs(d.Point.x) < 0.01f) != (Mathf.Abs(d.Point.z) < 0.01f);
                    if (!onAxis)
                        failures.Add($"[biome-drop-worlds] in the '{w.Key}' world, drop '{d.Region}' at " +
                                     $"{d.Point} is not seated on a single cardinal axis through the origin.");
                }

                // The two roomy worlds must produce a FULL set - a fix that made everything safe by
                // refusing everything would otherwise sail through the classification assertion.
                if ((w.Key == "live 1000m hub" || w.Key == "doubled world")
                    && drops.Count != BiomeRoads.DropRegions.Length)
                {
                    failures.Add($"[biome-drop-worlds] the '{w.Key}' world produced {drops.Count} of " +
                                 $"{BiomeRoads.DropRegions.Length} drops. A world with room on every side must " +
                                 "yield every road; a refusal here means the clearance floor is eating valid " +
                                 "ground.");
                }
            }

            if (totalDrops == 0)
            {
                failures.Add("[biome-drop-worlds] every probed world produced ZERO drops - this case asserted " +
                             "nothing and has not passed.");
            }

            notes.Add($"[biome-drop-worlds] {worlds.Count} worlds, {totalDrops} drops, all classified and " +
                      "cardinal-seated");
            log.AppendLine($"  worlds: {worlds.Count} probed, {totalDrops} drops all self-classifying");
        }

        // ── Case 4 — the boundary is ASKED of ZoneManager, never copied. ────────
        private static void Case4_TheBoundaryIsAskedOfZoneManagerNotCopied(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // (a) BEHAVIOUR: the probe answers with the classifier's own edge. Proven without
            //     naming a number here -- the point just inside is NOT the region and the point
            //     just outside IS, which is the definition of a boundary and stays true however
            //     ZoneManager is later re-sized.
            var dir = BiomeRoads.OutwardDirection(RegionId.Ashwood);
            if (dir == Vector3.zero)
            {
                failures.Add("[biome-drop-owner] Ashwood has no outward direction - the authored cardinal is " +
                             "gone, so nothing below can be probed.");
                return;
            }

            if (!BiomeRoads.TryFindRegionBoundary(RegionId.Ashwood, dir, 500f, out float boundary))
            {
                failures.Add("[biome-drop-owner] TryFindRegionBoundary found no Ashwood boundary within 500m " +
                             "along its own cardinal. On any world with room, the classifier must name an edge; " +
                             "a probe that cannot find one turns every drop into a refusal.");
                return;
            }

            const float Epsilon = 0.5f;
            RegionId justInside  = ZoneManager.GetZone(dir * (boundary - Epsilon));
            RegionId justOutside = ZoneManager.GetZone(dir * (boundary + Epsilon));

            if (justInside == RegionId.Ashwood)
                failures.Add($"[biome-drop-owner] the probe reported the Ashwood boundary at {boundary:0.##}m, " +
                             $"but {Epsilon}m INSIDE it already classifies as Ashwood - the probe is returning a " +
                             "point past the real edge, so the clearance is measured from the wrong place.");
            if (justOutside != RegionId.Ashwood)
                failures.Add($"[biome-drop-owner] the probe reported the Ashwood boundary at {boundary:0.##}m, " +
                             $"but {Epsilon}m OUTSIDE it classifies as '{justOutside}' - that is not a boundary.");

            // (b) A region the world has no room for must return FALSE, never a guessed distance.
            if (BiomeRoads.TryFindRegionBoundary(RegionId.Ashwood, dir, 5f, out float bogus))
                failures.Add($"[biome-drop-owner] TryFindRegionBoundary returned TRUE ({bogus:0.##}m) for a ray " +
                             "that never reaches Ashwood at all. A probe that guesses is worse than one that " +
                             "declines: the caller would seat a drop on the guess.");

            // (c) SOURCE: the derivation must consult the classifier rather than carry its own copy
            //     of the home-zone geometry. This is the CLAUDE.md sec.2/sec.5/sec.8 duplicated-state
            //     rule applied to the one number that decides what the player is told.
            string src = ReadSource(CoreSrc);
            if (src == null)
            {
                failures.Add($"[biome-drop-owner] cannot read {CoreSrc} - the lint has not run, so it has not " +
                             "passed.");
            }
            else
            {
                if (src.IndexOf("TryFindRegionBoundary", StringComparison.Ordinal) < 0)
                    failures.Add($"[biome-drop-owner] {CoreSrc} no longer exposes TryFindRegionBoundary - the " +
                                 "single-owner probe is gone, so the drop derivation has stopped asking " +
                                 "ZoneManager where a region begins.");
                if (src.IndexOf("ZoneManager.GetZone", StringComparison.Ordinal) < 0)
                    failures.Add($"[biome-drop-owner] {CoreSrc} never calls ZoneManager.GetZone. The classifier " +
                                 "is the authority on which region a point is in; a derivation that does not " +
                                 "consult it can only agree with it by luck, which is how F8 seq 4703 happened.");
            }

            notes.Add($"[biome-drop-owner] Ashwood boundary probed at {boundary:0.##}m and bracketed to " +
                      $"+/-{Epsilon}m against the classifier");
            log.AppendLine($"  owner: boundary {boundary:0.##}m, bracketed against ZoneManager.GetZone");
        }

        // ── Case 5 — the arrival failure lines carry their own evidence. ────────
        // The missing number IS the defect this case exists for: drift was computed and
        // printed only on success, so the failure line could not distinguish "the warp
        // never happened" from "the warp went to the wrong biome" - and WO-1604 was
        // consequently minted against the derivation instead of the crossing.
        private static void Case5_ArrivalFailuresNameThePromisedPointAndTheDrift(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            string src = ReadSource(InjectorSrc);
            if (src == null)
            {
                failures.Add($"[biome-drop-arrival] cannot read {InjectorSrc} - the lint has not run, so it has " +
                             "not passed.");
                return;
            }

            int verifyAt = src.IndexOf("void VerifyArrival(", StringComparison.Ordinal);
            if (verifyAt < 0)
            {
                failures.Add($"[biome-drop-arrival] VerifyArrival is gone from {InjectorSrc} - the far-side " +
                             "check that proves a drop did what its label said no longer exists.");
                return;
            }

            string body = src.Substring(verifyAt);

            // (a) The two failure modes must be SEPARATED by an explicit drift test, not left for
            //     the reader to infer from a coordinate.
            if (body.IndexOf("drift > ArrivalSettleRadius", StringComparison.Ordinal) < 0)
                failures.Add("[biome-drop-arrival] VerifyArrival no longer tests the drift against " +
                             "ArrivalSettleRadius before judging the region. Without that branch, a hero the warp " +
                             "never moved is reported as a region-split disagreement - which is exactly the " +
                             "mis-diagnosis F8 seq 4703 produced.");

            // (b) EVERY Fail raised after drift is known must carry the promised point AND the
            //     drift. A failure line the reader has to supplement with a code-read is the thing
            //     CLAUDE.md sec.12 forbids.
            int driftAt = body.IndexOf("float drift", StringComparison.Ordinal);
            if (driftAt < 0)
            {
                failures.Add("[biome-drop-arrival] VerifyArrival no longer computes a drift from the promised " +
                             "point at all.");
            }
            else
            {
                string afterDrift = body.Substring(driftAt);
                string[] chunks = afterDrift.Split(new[] { "FlowTrace.Fail(" }, StringSplitOptions.None);
                int checkedCalls = 0;
                for (int i = 1; i < chunks.Length; i++)
                {
                    int end = chunks[i].IndexOf(");", StringComparison.Ordinal);
                    string call = end > 0 ? chunks[i].Substring(0, end) : chunks[i];
                    checkedCalls++;

                    if (call.IndexOf("drift", StringComparison.Ordinal) < 0)
                        failures.Add("[biome-drop-arrival] an arrival FlowTrace.Fail raised after the drift is " +
                                     "known does not report it. The reader is then left inferring whether the " +
                                     "warp landed, which is how this ticket got pointed at the wrong system. " +
                                     "Offending call begins: " + Head(call));
                    if (call.IndexOf("s_promisedPoint", StringComparison.Ordinal) < 0)
                        failures.Add("[biome-drop-arrival] an arrival FlowTrace.Fail does not name the PROMISED " +
                                     "point, so the capture shows where the hero is but not where the drop said " +
                                     "they would be. Offending call begins: " + Head(call));
                }

                if (checkedCalls == 0)
                    failures.Add("[biome-drop-arrival] no FlowTrace.Fail calls remain after the drift is " +
                                 "computed - the arrival check has stopped reporting failures, which reads as a " +
                                 "permanently successful trip.");
            }

            // (c) A REFUSED road must reach the player, not only the log. The injector is the layer
            //     that knows a tunnel arm is about to dead-end.
            int injectAt = src.IndexOf("private void InjectDrops(", StringComparison.Ordinal);
            if (injectAt < 0)
            {
                failures.Add($"[biome-drop-arrival] InjectDrops is gone from {InjectorSrc}.");
            }
            else
            {
                string inject = src.Substring(injectAt, Math.Max(0, (verifyAt > injectAt ? verifyAt : src.Length) - injectAt));
                if (inject.IndexOf("DropRegions.Length", StringComparison.Ordinal) < 0)
                    failures.Add("[biome-drop-arrival] InjectDrops no longer compares the resolved drop count " +
                                 "against BiomeRoads.DropRegions.Length, so a refused road is silently absent - " +
                                 "the resolver deliberately refuses at Warn and hands the escalation here.");
                if (inject.IndexOf("Notify(", StringComparison.Ordinal) < 0)
                    failures.Add("[biome-drop-arrival] InjectDrops raises no Notify, so a closed road is a log " +
                                 "line only. The player walks an arm that dead-ends with no explanation.");
            }

            notes.Add("[biome-drop-arrival] arrival failures separated by drift and carrying promised point; " +
                      "refused roads escalate with a Notify");
            log.AppendLine("  arrival: drift-first branch present, every post-drift Fail self-describing");
        }

        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Build bounds from the four ORIGIN-TO-EDGE reaches the resolver actually consumes. Written
        /// this way because reach, not centre or size, is what ResolveDrops reads: expressing a test
        /// world as a centre+size means re-deriving the reaches by hand in every case, and that
        /// arithmetic is where a hostile-world test quietly stops being hostile.
        /// </summary>
        private static Bounds BoundsFromReach(float xPos, float xNeg, float zPos, float zNeg)
        {
            var min = new Vector3(-xNeg, -21f, -zNeg);
            var max = new Vector3(xPos, 21f, zPos);
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        /// <summary>Read a source file verbatim (comments and literals INCLUDED, deliberately: the
        /// message text these cases assert on lives inside string literals).</summary>
        private static string ReadSource(string relPath)
        {
            try
            {
                string full = Path.Combine(Directory.GetCurrentDirectory(), relPath);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Head(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= 90 ? s : s.Substring(0, 90) + "...";
        }
    }
}
