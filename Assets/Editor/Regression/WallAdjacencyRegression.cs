// =============================================================================
// WallAdjacencyRegression [wall-adjacency]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-972 (owner F8 seq 2327, verbatim: "cannot build walls beside each other").
//
// THE BUG THIS PINS, proven from her capture and not inferred:
//
//   [Flow:Build] REJECT Occupied cell=(17,16) fp=(2x2) gate=CellGrid
//                occupantCell=(17,17) occupant='wall_wood'
//   [Flow:Structure] 'wall_wood' carries Collider 'MeshCollider' bounds size=(3.03, 3.73, 1.42)
//   [Flow:Grid] Occupy cell=(12,17)/(14,17)/(16,17) footprint=(2x2) id='wall_wood'
//
// A palisade that is 3.03 m across and 1.42 m THICK, on a 3.00 m cell, was claiming a
// 2x2 BLOCK. Two collapses stacked: StructureFactory.MeasureUprightFootprintMetres
// reduces the mesh to Max(size.x, size.z) (the 1.42 m depth is discarded), and
// PlacementGrid.FootprintCells then CEILS AND SQUARES it - so a 1% overshoot
// (3.03 on 3.00) doubled the claim and re-applied that doubling to the thin axis that
// was never over a cell. Result: a wall owned its neighbours' squares (her reject) and
// wall runs sat on a 6 m pitch with a ~3 m hole between every 3.03 m segment.
//
// THE FIX IS CLAIM-SIDE ONLY - the mesh is never resized, so the walls-excluded-from-
// height-cadence carve-out holds and the NavMeshObstacle is byte-identical
// (Clamp(rendered*0.85, cellSize, claim) is 3x3 m at BOTH the old 2x2 and the new 1x1).
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (a) LIVE GRID PROBE - a REAL PlacementGrid replays her exact captured cells. A wall
//       claimed at (16,17) must leave (17,17) - beside it - and (17,16) - her failing
//       corner - PLACEABLE, while (16,17) itself stays refused. This is the headline
//       case: it goes red the moment the claim grows past one cell again.
//
//   (b) THE DECOUPLING IS LIVE - the mesh-driven claim for a 3.03 m body is still 2x2
//       (the old math is not "fixed" and must not be relied on), while the wall's actual
//       claim is 1x1. If someone re-points walls at the measured mesh, these converge
//       and the case fails.
//
//   (c) DATA PINS - the wall rows still author a footprint that fits ONE cell, and still
//       carry NO heightMul (the deliberate cadence carve-out: narrowing a wall opens
//       pathable gaps in already-saved runs). Either drift silently re-opens the bug.
//
//   (d) SOURCE INVARIANT (comment-stripped lint) - the claim seam needs a play session
//       and a skinned prefab to exercise end-to-end, so the wiring is pinned at source:
//       both claim sites call MeasureClaimFootprintMetres, the Wall branch exists, walls
//       may abut walls, and the refusal still speaks in WORDS (the owner is red/green
//       colourblind, so a red ghost tint is not a refusal message).
//
//   NOT provable here: that the run LOOKS continuous and that enemies still cannot walk
//   through it - that is the owner's felt-verify (PO closes, per docs/TICKET_PIPELINE.md).
//
// Markers: WALL_ADJACENCY_OK / WALL_ADJACENCY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.WallAdjacencyRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class WallAdjacencyRegression
    {
        private const string CatalogRelPath  = "Data/Canonical/structures-catalog.json";
        private const string FactorySrc      = "Assets/_Modules/Village/Catalog/StructureFactory.cs";
        private const string ControllerSrc   = "Assets/_Modules/Village/BuildMode/BuildModeController.cs";
        private const string LoaderSrc       = "Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs";
        private const string CardVmSrc       = "Assets/_Modules/Village/BuildMode/StructureCardVM.cs";

        /// <summary>The wall rows the palette + saves can produce (WO-948: wood builds, stone upgrades).</summary>
        private static readonly string[] WallIds = { "wall_wood", "wall_stone" };

        /// <summary>Her captured body width. Kept as the oracle for the mesh-driven claim.</summary>
        private const float CapturedWallWidthMetres = 3.03f;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("WALL_ADJACENCY_OK - " + reason);
            else Debug.LogError("WALL_ADJACENCY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var created  = new List<GameObject>();
            try
            {
                Case(failures, "adjacency",  () => Case1_AdjacencyOnRealGrid(failures, created));
                Case(failures, "decoupling", () => Case2_ClaimIsDecoupledFromMesh(failures, created));
                Case(failures, "data-pins",  () => Case3_CatalogPins(failures));
                Case(failures, "wiring",     () => Case4_SourceWiringLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                foreach (var go in created)
                {
                    if (go == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                    else                       UnityEngine.Object.DestroyImmediate(go);
                }
            }

            if (failures.Count == 0)
            {
                reason = "WALL ADJACENCY OK - a wall claims ONE cell, so her captured corner (17,16) " +
                         "and the cell beside a wall (17,17) are both placeable while the wall's own " +
                         "cell stays refused; the claim is decoupled from the 3.03 m mesh (which still " +
                         "ceils to 2x2); wall rows keep a one-cell authored footprint and no heightMul; " +
                         "both claim sites, the abut allowance and the words-based refusal are wired.";
                return true;
            }

            reason = failures.Count + " failure(s): " + string.Join(" | ", failures);
            return false;
        }

        /// <summary>Run one case, converting a throw into a failure rather than killing the suite.</summary>
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - her exact captured cells, on a REAL PlacementGrid
        // =====================================================================
        private static void Case1_AdjacencyOnRealGrid(List<string> failures, List<GameObject> created)
        {
            PlacementGrid grid = NewGrid(created);
            float authored = AuthoredFootprintMetres("wall_wood", failures);
            if (authored <= 0f) return;   // Case 3 reports the data problem

            Vector2Int claim = grid.FootprintCells(authored, 0f);
            if (claim.x != 1 || claim.y != 1)
            {
                failures.Add("[adjacency] a wall claims " + claim.x + "x" + claim.y + " cells from its " +
                             "authored footprint " + authored.ToString("0.###") + "m on a " +
                             grid.cellSize.ToString("0.##") + "m cell - a wall is a ONE-CELL tile. This is " +
                             "the seq-2327 bug: a claim wider than the tile makes a wall own its " +
                             "neighbours' squares and forces wall runs onto a pitch with holes in it.");
                return;   // the cell assertions below would be noise on top of this
            }

            // Her captured layout: a completed wall at 16_17 (the last of the 12/14/16 run).
            var wallCell = new Vector2Int(16, 17);
            grid.Occupy(wallCell, claim, "wall_wood");

            // THE HEADLINE: her exact rejected cell. Captured:
            //   REJECT Occupied cell=(17,16) ... occupantCell=(17,17) occupant='wall_wood'
            if (!grid.CanPlace(new Vector2Int(17, 16), claim))
                failures.Add("[adjacency] cell (17,16) is still refused next to a wall at (16,17) - this is " +
                             "the owner's captured seq-2327 reject ('cannot build walls beside each other'), " +
                             "back again. The neighbour is claiming squares its body does not occupy.");

            // Directly BESIDE, along the run - the continuous-wall case.
            if (!grid.CanPlace(new Vector2Int(17, 17), claim))
                failures.Add("[adjacency] the cell immediately beside a wall (17,17) is refused - wall runs " +
                             "would go back to a 6 m pitch with a ~3 m hole between every segment.");

            // And the row behind / in front, so corners and double walls work.
            if (!grid.CanPlace(new Vector2Int(16, 18), claim))
                failures.Add("[adjacency] the cell behind a wall (16,18) is refused - the SQUARED claim is " +
                             "back: a 1.42 m-thick wall must never own the row behind it.");
            if (!grid.CanPlace(new Vector2Int(15, 17), claim))
                failures.Add("[adjacency] the cell before a wall (15,17) is refused - a wall run cannot be " +
                             "extended backwards.");

            // The rule that MUST survive: never two walls on the SAME square.
            if (grid.CanPlace(wallCell, claim))
                failures.Add("[adjacency] the wall's OWN cell (16,17) is placeable - the one-cell claim has " +
                             "stopped registering occupancy entirely, which would stack walls on one square.");

            // Free must release exactly what it claimed (sell / move leak guard).
            grid.Free(wallCell, claim);
            if (!grid.CanPlace(wallCell, claim))
                failures.Add("[adjacency] Free did not release the wall's cell - selling or moving a wall " +
                             "would leave a permanent dead square.");
        }

        // =====================================================================
        //  CASE 2 - the claim is genuinely decoupled from the fitted mesh
        // =====================================================================
        private static void Case2_ClaimIsDecoupledFromMesh(List<string> failures, List<GameObject> created)
        {
            PlacementGrid grid = NewGrid(created);

            // The OLD path, on her captured body width. This must STILL be 2x2 - the mesh
            // math is not what was fixed, and a suite that assumed otherwise would go green
            // for the wrong reason.
            Vector2Int meshClaim = grid.FootprintCells(CapturedWallWidthMetres, 0f);
            if (meshClaim.x < 2 || meshClaim.y < 2)
            {
                failures.Add("[decoupling] FootprintCells(" + CapturedWallWidthMetres.ToString("0.##") +
                             "m) is now " + meshClaim.x + "x" + meshClaim.y + ", not the 2x2 the capture " +
                             "recorded. The cell math changed under this fix - re-verify WO-972's premise " +
                             "before trusting the rest of this suite.");
                return;
            }

            float authored = AuthoredFootprintMetres("wall_wood", failures);
            if (authored <= 0f) return;

            Vector2Int authoredClaim = grid.FootprintCells(authored, 0f);
            if (authoredClaim == meshClaim)
                failures.Add("[decoupling] the wall's claim (" + authoredClaim.x + "x" + authoredClaim.y +
                             ") equals the MESH-driven claim (" + meshClaim.x + "x" + meshClaim.y + ") - " +
                             "walls have been re-pointed at the measured mesh, which is exactly the " +
                             "coupling WO-972 removed. The 3 cm the body overhangs its cell is what makes " +
                             "a run continuous; it must not be allowed to double the claim again.");

            // Cardinal yaws must not inflate a wall's claim either (a rotated wall is still a tile).
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                Vector2Int c = grid.FootprintCells(authored, yaw);
                if (c != authoredClaim)
                    failures.Add("[decoupling] a wall at yaw " + yaw.ToString("0") + " claims " + c.x + "x" + c.y +
                                 " but " + authoredClaim.x + "x" + authoredClaim.y + " at yaw 0 - rotating a " +
                                 "wall to turn a corner would refuse cells the un-rotated one allows.");
            }
        }

        // =====================================================================
        //  CASE 3 - the catalog data the fix stands on
        // =====================================================================
        private static void Case3_CatalogPins(List<string> failures)
        {
            JObject root = ReadCatalog(failures);
            if (root == null) return;

            var entries = root["entries"] as JArray;
            if (entries == null) { failures.Add("[data-pins] structures-catalog.json has no 'entries' array."); return; }

            const float CellSize = 3f;   // PlacementGrid.cellSize default
            foreach (string id in WallIds)
            {
                JObject row = FindRow(entries, id);
                if (row == null)
                {
                    failures.Add("[data-pins] catalog row '" + id + "' is missing - saved towns replay walls " +
                                 "through CatalogRegistry, so a removed row loses them.");
                    continue;
                }

                var type = (string)row["type"];
                if (!string.Equals(type, "Wall", StringComparison.Ordinal))
                    failures.Add("[data-pins] '" + id + "' has type '" + (type ?? "<null>") + "', not 'Wall' - the " +
                                 "one-cell claim and the abut allowance are BOTH keyed on CatalogType.Wall, so " +
                                 "this row would silently fall back to the mesh-driven 2x2 claim.");

                var repo = row["repo"] as JObject;
                var placement = repo != null ? repo["placement"] as JObject : null;
                var fp = placement != null ? placement["footprint"] : null;
                if (fp == null)
                {
                    failures.Add("[data-pins] '" + id + "' has no repo.placement.footprint - that value now DRIVES " +
                                 "the wall's grid claim, so without it the claim falls back to the measured mesh.");
                    continue;
                }

                float metres = (float)fp;
                if (metres <= 0f || metres > CellSize)
                    failures.Add("[data-pins] '" + id + "' authors placement.footprint=" + metres.ToString("0.###") +
                                 "m, which does not fit ONE " + CellSize.ToString("0.##") + "m cell - this re-opens " +
                                 "seq 2327 directly (a >1 cell claim makes a wall own its neighbours' squares).");

                // The cadence carve-out (structures-catalog.json _heightCadence + the per-row
                // _heightNote): heightMul is UNIFORM, so authoring one narrows the wall and opens
                // pathable GAPS in already-saved runs while shrinking its NavMeshObstacle.
                if (repo != null && repo["heightMul"] != null)
                    failures.Add("[data-pins] '" + id + "' now authors repo.heightMul=" + repo["heightMul"] +
                                 " - walls are DELIBERATELY left on the 1.0 base. heightMul is a uniform scale, " +
                                 "so it narrows the wall as well as lowering it, which opens pathable gaps in " +
                                 "already-saved wall runs and shrinks the NavMeshObstacle with them. That is a " +
                                 "save-compat break, not a visual tweak (see the row's _heightNote).");
            }
        }

        // =====================================================================
        //  CASE 4 - source wiring lint (comment-stripped)
        // =====================================================================
        private static void Case4_SourceWiringLint(List<string> failures)
        {
            string factory    = ReadStripped(FactorySrc, failures);
            string controller = ReadStripped(ControllerSrc, failures);
            string loader     = ReadStripped(LoaderSrc, failures);
            if (factory == null || controller == null || loader == null) return;

            // (1) The claim seam exists and branches on Wall.
            // WO-986: MeasureClaimFootprintXZ is the CoC non-square authority; Metres wraps it.
            bool hasClaimApi = factory.Contains("MeasureClaimFootprintMetres")
                            || factory.Contains("MeasureClaimFootprintXZ");
            if (!hasClaimApi)
                failures.Add("[wiring] " + FactorySrc + " no longer defines MeasureClaimFootprintMetres/XZ - the " +
                             "wall claim has been folded back into the measured mesh.");
            else if (!Regex.IsMatch(factory, @"CatalogType\s*\.\s*Wall"))
                failures.Add("[wiring] " + FactorySrc + " defines a claim API but no longer " +
                             "branches on CatalogType.Wall - every row would claim off the authored footprint, " +
                             "which under-claims real buildings.");

            // (2) BOTH claim sites use it. If placement and replay disagree, a reload Occupies a
            //     different cell set than placement promised and the run re-breaks on load.
            if (!controller.Contains("MeasureClaimFootprintMetres") && !controller.Contains("MeasureClaimFootprintXZ"))
                failures.Add("[wiring] " + ControllerSrc + " no longer claims via MeasureClaimFootprintMetres/XZ - " +
                             "live placement is back on the mesh-driven 2x2 claim (the seq-2327 reject).");
            if (!loader.Contains("MeasureClaimFootprintMetres") && !loader.Contains("MeasureClaimFootprintXZ"))
                failures.Add("[wiring] " + LoaderSrc + " no longer claims via MeasureClaimFootprintMetres/XZ - " +
                             "placement and REPLAY would claim different cells, so a saved wall run would " +
                             "re-break the next time the town loads.");

            // (3) Wall may abut wall. Without this the reject simply moves from the cell grid to
            //     the world-overlap gate: two 3.03 m bodies on 3 m centres overlap by 3 cm.
            if (!controller.Contains("armedIsWall"))
                failures.Add("[wiring] " + ControllerSrc + " has lost the wall-abuts-wall allowance in " +
                             "OverlapsExistingStructure - neighbouring walls overlap by ~3 cm BY DESIGN " +
                             "(that overlap is what makes a run continuous), so the strict AABB test would " +
                             "reject them again as gate=WorldOverlap.");

            // (4) WORDS, never colour alone - the owner is red/green colourblind.
            if (!controller.Contains("_lastRejectDetail"))
                failures.Add("[wiring] " + ControllerSrc + " no longer names the occupant on an Occupied reject - " +
                             "the refusal falls back to a red ghost tint plus a generic line, which tells a " +
                             "red/green colourblind player nothing about WHY the placement failed.");

            // (5) The proving line stays. Instrumentation is permanent (CLAUDE.md section 12): the
            //     measured footprint was logged NOWHERE, which is why this RCA had to bound it from
            //     a collider dump instead of reading it.
            if (!Regex.IsMatch(factory, @"FlowTrace\s*\.\s*Once"))
                failures.Add("[wiring] " + FactorySrc + " has lost the WALL CLAIM proving line - the authored-vs-" +
                             "measured footprint would again be recorded nowhere, and the next wall-claim " +
                             "regression would start from zero evidence.");

            // (6) The PLAYER-FACING footprint label reports the CLAIM, not a second measure of its
            //     own. BuildStructureInfoPanel renders StructureCardVM.FootprintLabel as the
            //     "Footprint" row; while it read MeasureUprightFootprintMetres it told the player
            //     "2x2 cells" for a wall that placement claims as 1x1 - the panel contradicting the
            //     grid is the same class of defect as seq 2327, just on the reporting side.
            string cardVm = ReadStripped(CardVmSrc, failures);
            if (cardVm != null)
            {
                if (!cardVm.Contains("MeasureClaimFootprintMetres") && !cardVm.Contains("MeasureClaimFootprintXZ"))
                    failures.Add("[wiring] " + CardVmSrc + " no longer derives its Footprint label from " +
                                 "MeasureClaimFootprintMetres/XZ - the info panel is measuring the mesh itself " +
                                 "again, so a wall's card would read '2x2 cells' while placement claims 1x1.");
                if (Regex.IsMatch(cardVm, @"MeasureUprightFootprintMetres"))
                    failures.Add("[wiring] " + CardVmSrc + " still calls MeasureUprightFootprintMetres - the " +
                                 "label must read the ONE claim authority, never a second divergent measure.");
                // The label's fallback returns "1x1 cells", which is INDISTINGUISHABLE on screen
                // from a genuine 1x1 structure. INSTRUMENTATION_STANDARD 1.4b: a trace that cannot
                // report failure is a bug, so the fallback must say it is a default, not a measure.
                if (!Regex.IsMatch(cardVm, @"FlowTrace\s*\.\s*Once"))
                    failures.Add("[wiring] " + CardVmSrc + " has lost the footprint-label fallback trace - a " +
                                 "failed measure or a null PlacementGrid would silently render '1x1 cells', " +
                                 "reading identically to a correct 1x1 in both the UI and the log.");
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// A real PlacementGrid with the SAME origin centring Awake applies (editmode
        /// AddComponent does not run Awake, so cell-to-world would not match the runtime grid).
        /// </summary>
        private static PlacementGrid NewGrid(List<GameObject> created)
        {
            var go = new GameObject("WallAdjacencyRegressionGrid");
            go.hideFlags = HideFlags.HideAndDontSave;   // a batch run can never dirty an open scene
            created.Add(go);
            var grid = go.AddComponent<PlacementGrid>();
            grid.origin = new Vector3(-grid.gridWidth * grid.cellSize * 0.5f, 0f,
                                      -grid.gridHeight * grid.cellSize * 0.5f);
            return grid;
        }

        /// <summary>The authored placement footprint for a row, or -1 when unreadable.</summary>
        private static float AuthoredFootprintMetres(string id, List<string> failures)
        {
            JObject root = ReadCatalog(failures);
            var entries = root != null ? root["entries"] as JArray : null;
            JObject row = entries != null ? FindRow(entries, id) : null;
            var repo = row != null ? row["repo"] as JObject : null;
            var placement = repo != null ? repo["placement"] as JObject : null;
            var fp = placement != null ? placement["footprint"] : null;
            return fp != null ? (float)fp : -1f;
        }

        private static JObject FindRow(JArray entries, string id)
        {
            foreach (var e in entries)
            {
                var o = e as JObject;
                if (o != null && string.Equals((string)o["id"], id, StringComparison.Ordinal)) return o;
            }
            return null;
        }

        private static JObject ReadCatalog(List<string> failures)
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
                if (string.IsNullOrEmpty(json))
                {
                    failures.Add("[data-pins] " + CatalogRelPath + " unreadable (CanonicalJson.Read returned empty).");
                    return null;
                }
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                failures.Add("[data-pins] " + CatalogRelPath + " failed to parse: " + ex.Message);
                return null;
            }
        }

        private static string ReadStripped(string path, List<string> failures)
        {
            if (!File.Exists(path)) { failures.Add("[wiring] source file missing: " + path); return null; }
            return StripComments(File.ReadAllText(path));
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
