// =============================================================================
// TalentTreeShapeRegression [talent-tree-shape] - the owner's SHAPE LAW for every
// talent tree, common and specialty alike (owner ruling 2026-08-16).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
// Markers: TALENT_TREE_SHAPE_OK / TALENT_TREE_SHAPE_FAIL.
//
// THE LAW, verbatim from the owner:
//   "we don't have to put seven on the bottom row. We can put three on the bottom row
//    and have those three branch into another layer of four because one can be a base
//    that goes two different directions ... Keep it no more than three wide at the
//    bottom and let it branch a little bit as it goes up."
//   "common or specialty should still start from a few simple then really refine to
//    the playstyle of the user."
//
// WHAT WENT WRONG BEFORE: the shared pool was reshaped to 3 -> 4 -> 4 while the three
// PER-CLASS trees still fanned five (knight: eight, counting the STEWARD/BULWARK bases)
// flat across their bottom rank with no authored position at all on ranger/mage - so the
// runtime auto-placer decided their shape, and the tree the player actually looked at
// was the unreshaped one. Nothing in the repo asserted the law, so "fixed" and "half
// fixed" read identically at the gate.
//
// WHAT IS PINNED (hard failures):
//   1 [authoring]  every node in every tree AND in the shared pool carries BOTH x and y,
//                  inside the 0..1 authoring contract. An unauthored node is shaped by
//                  ResolveGraphNorms' fallback instead of by the designer.
//   2 [base]       the BOTTOM row (largest y; y ascends downward) of every tree and of
//                  the shared pool holds AT MOST THREE nodes, every one of them a root
//                  (no prerequisites) and priced at the tree's cheapest cost. A base is
//                  the simple, entry-level pick - never a mid-tier node that happens to
//                  have been dragged down.
//   3 [widen]      the row directly above the base is STRICTLY wider than the base, and
//                  no row above the base is narrower than the base. The tree opens as it
//                  rises; it never funnels back to a bottleneck.
//   4 [graph]      every prerequisite id resolves; every non-base node has at least one
//                  prerequisite (no orphan floating above the tree); every prerequisite
//                  carries a STRICTLY LARGER y than its child (upward flow, which also
//                  makes a cycle impossible by construction); and every node is reachable
//                  from a base by walking prerequisites.
//   5 [hidden]     no VISIBLE node depends on a "hidden": true node - hiding does not
//                  rewrite the prereq graph (HeroTalentNodeDef.Hidden's own caution), so
//                  such a node would read "Requires <invisible thing>" forever.
//
//   6 [viewport]   (WO-1310) every tree, rotated and solved through the VIEW'S OWN
//                  SolveGraphLatticePx against the reference 1695x493 well, lands inside
//                  the board: no plate closer to the content origin than half of
//                  HeroSkillTreePanelMvvm.PlateClearPx (that inset is the plate PLUS its
//                  hung nameplate PLUS the focus glow, so anything tighter is sliced by
//                  the RectMask2D), and the resolved board is no more than MaxScrollWide
//                  viewports across / MaxScrollTall viewports down.
//
//   7 [first-point] (WO-1306) every CLASS tree's bottom row holds at least one node that
//                  GRANTS A CASTABLE, and the ability id it names resolves in
//                  AbilityCatalog. Owner ruling 2026-09-02: "we want them to unlocka few
//                  items that can go in the quick swap bar fast, why because our retention
//                  number is very low and people are not returning". Stated over EVERY tree
//                  rather than pinned per class - the knight (02f9b8a4f) and the mage
//                  (WO-1306) were each fixed on the same night, and two per-class pins
//                  would have left the next class free to regress silently. The shared pool
//                  is exempt: it is the universal strip, not a class identity.
//
// !! THE HEADER USED TO CLAIM, RIGHT HERE, that "the implied content height at
// HeroSkillTreePanelMvvm.MinNodePitchPx" was "measured and logged, never failed - so the
// 'does the tree fit the 493 px well' question is answered by a number on every gate run".
// THAT WAS FALSE DOCUMENTATION. This file contained no reference to MinNodePitchPx, to any
// pitch, or to the 493 px well anywhere; the only note was the row census. So the question
// was never answered by anything, and a tree that could not fit - or that opened sliced
// through its top rank, which is what WO-1310 reported - passed this gate silently. Rule 6
// is the measurement AND the assertion the old sentence only promised. A number that is
// measured and never failed is exactly how "fits" and "does not fit" read identically.
//
// WHAT IS STILL MEASURED AND LOGGED ONLY: the row census per tree, and the resolved board
// size in viewports, printed on every run so drift is visible before it is a failure.
//
// DERIVED, NEVER AUTHORED: this oracle reads x/y/prerequisites/cost/hidden only. Row
// membership, node state, track shape and pixel geometry are all DERIVED downstream
// (SolveGraphLatticePx owns position; HeroSkillTreeVM owns state) and are asserted by
// SkillsPanelLayoutRegression [grid] / TalentFocusSingletonRegression, not here.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.TalentTreeShapeRegression.RunAll
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DeNelle.Village;            // AbilityCatalog - rule 7 resolves the granted ability id
using DeNelle.Village.Talents;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class TalentTreeShapeRegression
    {
        /// <summary>The owner's cap: no more than three nodes on a tree's bottom row.</summary>
        public const int MaxBaseRowNodes = 3;

        /// <summary>Row-clustering tolerance in normalised y. Mirrors
        /// HeroSkillTreePanelMvvm.RowClusterNorm - two nodes closer than this in y are the
        /// same visual row, which is exactly how the shipped solver groups them.</summary>
        public const float RowClusterNorm = 0.055f;

        /// <summary>Reference graph well in ref px (2340x1080 device), the same rect the
        /// sibling oracles TalentFocusSingletonRegression / SkillsPanelLayoutRegression replay
        /// their band arithmetic against. Landscape and SHORT - which is the whole reason the
        /// board's many axis (the track lanes) has to be the horizontal one.</summary>
        public const float RefWellWidthPx = 1695f;
        public const float RefWellHeightPx = 493f;

        /// <summary>How far a resolved board may exceed the well before it stops being a board
        /// and starts being a corridor. Scrolling is legitimate (WO-1310 acceptance 1); an
        /// endless scroll is not. The full-tree worst case sits well inside both today - the
        /// budget exists to fail the NEXT axis mix-up, not to certify the current one.</summary>
        public const float MaxScrollWide = 3.0f;
        public const float MaxScrollTall = 6.0f;

        private static readonly string[] Slugs = { "knight", "ranger", "mage" };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TALENT_TREE_SHAPE_OK - " + reason);
            else Debug.LogError("TALENT_TREE_SHAPE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                HeroTalentCatalog.Reload();

                var all = new Dictionary<string, HeroTalentNodeDef>(StringComparer.Ordinal);
                var groups = new List<KeyValuePair<string, List<HeroTalentNodeDef>>>();

                foreach (var slug in Slugs)
                {
                    var tree = HeroTalentCatalog.GetTree(slug);
                    if (tree == null || tree.Nodes == null || tree.Nodes.Count == 0)
                    {
                        failures.Add("[data] hero tree '" + slug + "' is missing or empty - the shape law " +
                                     "cannot be checked and the class has no talents to spend on");
                        continue;
                    }
                    var list = tree.Nodes.Where(n => n != null && !string.IsNullOrEmpty(n.Id)).ToList();
                    groups.Add(new KeyValuePair<string, List<HeroTalentNodeDef>>(slug, list));
                }

                var shared = HeroTalentCatalog.SharedNodes;
                if (shared == null || shared.Count == 0)
                    failures.Add("[data] the shared (common) pool is empty - the law covers common AND " +
                                 "specialty trees (owner 2026-08-16)");
                else
                    groups.Add(new KeyValuePair<string, List<HeroTalentNodeDef>>("shared",
                        shared.Where(n => n != null && !string.IsNullOrEmpty(n.Id)).ToList()));

                foreach (var g in groups)
                    foreach (var n in g.Value)
                        all[n.Id] = n;

                foreach (var g in groups) CheckTree(g.Key, g.Value, all, failures, notes);

                CheckHiddenStranding(all, failures);

                // 6 [viewport] - WO-1310. A tree that renders sliced or scrolls forever is a
                // failed tree even when every authored row is legal, and nothing here used to
                // look. Each CLASS board is the class nodes PLUS the shared pool, because that
                // is what the panel actually draws once the shared shelf is engaged.
                var sharedPool = groups.FirstOrDefault(g => g.Key == "shared").Value;
                foreach (var g in groups)
                {
                    if (g.Key == "shared") continue;
                    var board = new List<HeroTalentNodeDef>(g.Value);
                    if (sharedPool != null) board.AddRange(sharedPool);
                    CheckViewport(g.Key, board, failures, notes);
                }
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "TALENT TREE SHAPE OK - every tree (common and specialty) starts from at most " +
                         MaxBaseRowNodes + " simple roots and branches wider as it rises; all x/y authored " +
                         "inside 0..1; no orphan, no cycle, nothing unreachable, no visible node stranded " +
                         "behind a hidden one; every CLASS tree's first point buys a CASTABLE that resolves " +
                         "in the catalog; and every board resolves INSIDE the 1695x493 well - no plate " +
                         "inside the clearance inset, no board past the scroll budget" + noteStr;
                return true;
            }
            reason = "talent-tree-shape FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        // =====================================================================
        //  One tree (or the shared pool) against the law
        // =====================================================================
        private static void CheckTree(string name, List<HeroTalentNodeDef> nodes,
                                      Dictionary<string, HeroTalentNodeDef> all,
                                      List<string> failures, List<string> notes)
        {
            if (nodes == null || nodes.Count == 0) return;

            // ── 1 [authoring] every node positioned, inside 0..1 ─────────────────
            bool positionsOk = true;
            foreach (var n in nodes)
            {
                bool xs = n.X >= 0f, ys = n.Y >= 0f;
                if (!xs || !ys)
                {
                    positionsOk = false;
                    failures.Add("[authoring] '" + n.Id + "' (" + name + ") carries no authored position " +
                                 "(x=" + F(n.X) + ", y=" + F(n.Y) + ") - the runtime auto-placer would decide " +
                                 "its row, so the designer's shape is not the shape the player sees");
                    continue;
                }
                if (n.X > 1.0001f || n.Y > 1.0001f)
                {
                    positionsOk = false;
                    failures.Add("[authoring] '" + n.Id + "' sits at (" + F(n.X) + "," + F(n.Y) + ") - outside " +
                                 "the 0..1 authoring contract the lattice solver normalises from");
                }
            }
            if (!positionsOk) return;   // row maths on unauthored data would only invent noise

            // ── rows, bottom (largest y) first ───────────────────────────────────
            var rows = ClusterRows(nodes);
            var baseRow = rows[0];

            // ── 2 [base] at most three, all roots, all cheapest ──────────────────
            if (baseRow.Count > MaxBaseRowNodes)
                failures.Add("[base] '" + name + "' puts " + baseRow.Count + " nodes on its bottom row (" +
                             Ids(baseRow) + ") - the law is AT MOST " + MaxBaseRowNodes +
                             ", branching wider as it rises (owner 2026-08-16). Move the extras up a row " +
                             "and hang them off a base; never widen the base");

            int minCost = nodes.Min(n => n.Cost);
            foreach (var n in baseRow)
            {
                if (n.Prerequisites != null && n.Prerequisites.Any(p => !string.IsNullOrEmpty(p)))
                    failures.Add("[base] bottom-row node '" + n.Id + "' has a prerequisite (" +
                                 string.Join(",", n.Prerequisites.ToArray()) + ") - a base is where the tree " +
                                 "STARTS; a gated node on the bottom row means the real base is somewhere else");
                if (n.Cost > minCost)
                    failures.Add("[base] bottom-row node '" + n.Id + "' costs " + n.Cost + " while '" + name +
                                 "' has a " + minCost + "-cost tier - the base row must be the SIMPLE, cheapest " +
                                 "entry picks, not a mid-tier node dragged down");
            }

            // ── 3 [widen] the tree opens as it rises ─────────────────────────────
            if (rows.Count < 2)
                failures.Add("[widen] '" + name + "' resolves to a single row - there is nothing to specialise " +
                             "into; a tree must branch upward from its bases");
            else
            {
                if (rows[1].Count <= baseRow.Count)
                    failures.Add("[widen] '" + name + "' goes " + baseRow.Count + " -> " + rows[1].Count +
                                 " from the base to the row above it - the row above the bases must be STRICTLY " +
                                 "wider (one base can feed two directions), or the player never gets a fork");
                for (int r = 1; r < rows.Count; r++)
                    if (rows[r].Count < baseRow.Count)
                        failures.Add("[widen] '" + name + "' row " + (r + 1) + " holds " + rows[r].Count +
                                     " node(s) (" + Ids(rows[r]) + "), narrower than its " + baseRow.Count +
                                     "-node base - the tree funnels back into a bottleneck instead of refining");
            }

            // ── 4 [graph] reachable, acyclic-by-construction, no orphans ─────────
            var baseIds = new HashSet<string>(baseRow.Select(n => n.Id), StringComparer.Ordinal);
            var reached = new HashSet<string>(baseIds, StringComparer.Ordinal);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var n in nodes)
                {
                    if (reached.Contains(n.Id)) continue;
                    if (n.Prerequisites == null) continue;
                    var live = n.Prerequisites.Where(p => !string.IsNullOrEmpty(p)).ToList();
                    if (live.Count > 0 && live.All(reached.Contains)) { reached.Add(n.Id); grew = true; }
                }
            }

            foreach (var n in nodes)
            {
                bool isBase = baseIds.Contains(n.Id);
                var live = n.Prerequisites == null
                    ? new List<string>()
                    : n.Prerequisites.Where(p => !string.IsNullOrEmpty(p)).ToList();

                if (!isBase && live.Count == 0)
                    failures.Add("[graph] '" + n.Id + "' is not on the base row yet has NO prerequisite - it " +
                                 "floats free above the tree, so the 'pick a path then specialise it' story " +
                                 "is broken for it");

                foreach (var p in live)
                {
                    HeroTalentNodeDef parent;
                    if (!all.TryGetValue(p, out parent))
                    {
                        failures.Add("[graph] '" + n.Id + "' requires '" + p + "', which is not a known node");
                        continue;
                    }
                    if (parent.Y <= n.Y + 0.0001f)
                        failures.Add("[graph] '" + n.Id + "' (y=" + F(n.Y) + ") requires '" + p + "' (y=" +
                                     F(parent.Y) + "), which does NOT sit below it - progression must flow " +
                                     "upward, and the strict ordering is what makes a cycle impossible");
                }

                if (!reached.Contains(n.Id))
                    failures.Add("[graph] '" + n.Id + "' is unreachable from any base of '" + name +
                                 "' - no sequence of purchases can ever get the player to it");
            }

            // ── 7 [first-point] the first Wisdom point must buy something to PRESS ──
            CheckFirstPointIsCastable(name, baseRow, failures, notes);

            notes.Add(name + " rows " + string.Join("/", rows.Select(r => r.Count.ToString()).ToArray()) +
                      " (bottom first, " + nodes.Count + " nodes)");
        }

        // =====================================================================
        //  7 [first-point] - EVERY CLASS TREE'S FIRST POINT BUYS A CASTABLE
        // -----------------------------------------------------------------------------
        //  Owner ruling 2026-09-02, verbatim, and it is a BUSINESS rule, not a taste one:
        //    "we want them to unlocka few items that can go in the quick swap bar fast,
        //     why because our retention number is very low and people are not returning"
        //
        //  A new player whose first talent point buys +2% of an invisible stat has nothing
        //  to press, and the measured consequence is that they do not come back.
        //
        //  ⛔ THIS IS DELIBERATELY GENERAL, NOT A PER-CLASS LIST. The knight was fixed on
        //  2026-09-02 (commit 02f9b8a4f, Thunderbolt promoted to the base row) and the mage
        //  on the same night (WO-1306, mage.t1n3 re-authored into the drainshot 'Siphon
        //  Ward'). Pinning either one BY ID would have made each fix its own special case and
        //  left the NEXT class - or a future re-shuffle of an existing one - free to regress
        //  silently, which is exactly how the mage came to be the only outlier in the first
        //  place. The rule is stated once, over every tree, so a fourth class inherits it.
        //
        //  The shared pool is EXEMPT and it is exempt on purpose: it is not a class identity,
        //  it is the universal strip, and it already satisfies the rule anyway (shared.n9
        //  Arcane Bolt / n10 Mend / n11 Dash). Exempting it keeps the rule about the thing it
        //  is actually about - the CLASS tree's first impression.
        //
        //  THREE THINGS ARE PROVEN, because two of them look identical from a distance and
        //  only the third is what the player experiences:
        //    (a) a base-row node GRANTS an ability at all (kind=skill / a non-empty
        //        abilityId / an unlockAbility effect);
        //    (b) the id it names RESOLVES in AbilityCatalog - a node that grants a spell the
        //        catalog has never heard of leaves the loadout with nothing to equip, which
        //        reads to the player exactly like a stat node;
        //    (c) that ability is not a dead token. HeroLoadoutVM builds the hot-swap choices
        //        from unlocked SKILL-kind nodes, so (a)+(b) IS reachability to the bar.
        // =====================================================================
        private static void CheckFirstPointIsCastable(string name, List<HeroTalentNodeDef> baseRow,
                                                      List<string> failures, List<string> notes)
        {
            if (string.Equals(name, "shared", StringComparison.Ordinal)) return;
            if (baseRow == null || baseRow.Count == 0) return;

            var granting = new List<string>();
            var dangling = new List<string>();

            foreach (var n in baseRow)
            {
                string abilityId = n.AbilityId;
                bool declaresSkill = !string.IsNullOrEmpty(abilityId)
                                     || string.Equals(n.Kind, "skill", StringComparison.OrdinalIgnoreCase)
                                     || (n.Effect != null && string.Equals(n.Effect.Type, "unlockAbility",
                                                                           StringComparison.OrdinalIgnoreCase));
                if (!declaresSkill) continue;

                // An unlockAbility effect may name the id instead of the node-level field.
                if (string.IsNullOrEmpty(abilityId) && n.Effect != null) abilityId = n.Effect.Ability;

                if (string.IsNullOrEmpty(abilityId))
                {
                    dangling.Add(n.Id + " (declares kind=skill but names no ability)");
                    continue;
                }
                if (AbilityCatalog.FindById(abilityId) == null)
                {
                    dangling.Add(n.Id + " -> '" + abilityId + "' (no such ability in AbilityCatalog)");
                    continue;
                }
                granting.Add(n.Id + " -> " + abilityId);
            }

            foreach (var d in dangling)
                failures.Add("[first-point] '" + name + "' base-row node " + d + ". A base node that " +
                             "advertises a spell the catalog cannot resolve gives the loadout nothing to " +
                             "equip, so the player's first point buys a name and no button - which is " +
                             "indistinguishable, to them, from buying a stat.");

            if (granting.Count == 0)
                failures.Add("[first-point] '" + name + "' has NO castable on its bottom row (" +
                             Ids(baseRow) + ") - a new player's FIRST Wisdom point in this tree buys a " +
                             "passive stat and gives them nothing to press. Owner ruling 2026-09-02: " +
                             "\"we want them to unlocka few items that can go in the quick swap bar fast, " +
                             "why because our retention number is very low and people are not returning\". " +
                             "Fix it the way the knight and the mage were fixed - by making a CHEAPEST-COST " +
                             "ROOT grant an ability - never by dragging a pricier node down, which rule 2 " +
                             "[base] will refuse.");
            else
                notes.Add(name + " first-point castable(s): " + string.Join(", ", granting.ToArray()));
        }

        // =====================================================================
        //  6 [viewport] - does the board the PLAYER gets fit the board it is drawn on
        //
        //  This runs the VIEW'S OWN axis rotation and its OWN public solver, so it measures
        //  what ships rather than a second copy of the arithmetic. Two things fail:
        //    (a) a plate inside the clearance inset - it is sliced by the RectMask2D (the
        //        WO-1310 "AETHER BOND cut in half by the panel's top edge" capture);
        //    (b) a board past the scroll budget - the WO-1310 defect shape, where the
        //        progression axis was fed to the COLUMNS and the lanes to the ROWS, giving a
        //        dozen rows against three columns inside a 1695x493 landscape well.
        //  Both are numbers, and both are printed pass or fail.
        // =====================================================================
        private static void CheckViewport(string name, List<HeroTalentNodeDef> board,
                                          List<string> failures, List<string> notes)
        {
            if (board == null || board.Count == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var n in board)
            {
                if (n.X < minX) minX = n.X;
                if (n.X > maxX) maxX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.Y > maxY) maxY = n.Y;
            }
            float spanX = Mathf.Max(0.001f, maxX - minX);
            float spanY = Mathf.Max(0.001f, maxY - minY);

            // The view's rotation, verbatim (HeroSkillTreePanelMvvm.RebuildTracks): lane on the
            // WIDE axis, progression inverted onto the short one so the base rank is row 0.
            var norms = new float[board.Count * 2];
            for (int i = 0; i < board.Count; i++)
            {
                norms[i * 2] = (board[i].X - minX) / spanX;
                norms[i * 2 + 1] = 1f - (board[i].Y - minY) / spanY;
            }

            float pad = HeroSkillTreePanelMvvm.GraphPadPx;
            float boxW = RefWellWidthPx - pad * 2f;
            float boxH = RefWellHeightPx - pad * 2f - HeroSkillTreePanelMvvm.RankBandPx;

            float[] px;
            try { px = HeroSkillTreePanelMvvm.SolveGraphLatticePx(norms, boxW, boxH); }
            catch (Exception ex)
            {
                failures.Add("[viewport] '" + name + "': SolveGraphLatticePx THREW " +
                             ex.GetType().Name + ": " + ex.Message);
                return;
            }
            if (px == null || px.Length != norms.Length)
            {
                failures.Add("[viewport] '" + name + "': the solver returned " +
                             (px == null ? "null" : (px.Length / 2).ToString()) + " centres for " +
                             board.Count + " nodes - the board cannot be measured");
                return;
            }

            float clearHalf = HeroSkillTreePanelMvvm.PlateClearPx * 0.5f;
            float pxMinX = float.MaxValue, pxMinY = float.MaxValue;
            float pxMaxX = float.MinValue, pxMaxY = float.MinValue;
            string worstId = "-";
            for (int i = 0; i < board.Count; i++)
            {
                float x = px[i * 2], y = px[i * 2 + 1];
                if (x < pxMinX || y < pxMinY) worstId = board[i].Id;
                if (x < pxMinX) pxMinX = x;
                if (y < pxMinY) pxMinY = y;
                if (x > pxMaxX) pxMaxX = x;
                if (y > pxMaxY) pxMaxY = y;
            }

            if (pxMinX < clearHalf - 0.5f || pxMinY < clearHalf - 0.5f)
                failures.Add("[viewport] '" + name + "': plate '" + worstId + "' resolves to (" +
                             F(pxMinX) + "," + F(pxMinY) + "), inside the " + F(clearHalf) +
                             " px clearance inset (half of HeroSkillTreePanelMvvm.PlateClearPx - the " +
                             "plate, its hung nameplate AND the focus ring). It is sliced by the " +
                             "RectMask2D at the panel edge: half a node, its icon cut");

            // Symmetric extents, exactly as RebuildTracks sizes the content rect.
            float contentW = Mathf.Max(pxMaxX + pxMinX, pxMaxX + clearHalf + pad);
            float contentH = Mathf.Max(pxMaxY + pxMinY,
                                       pxMaxY + clearHalf + pad + HeroSkillTreePanelMvvm.RankBandPx);
            float wide = contentW / RefWellWidthPx;
            float tall = contentH / RefWellHeightPx;

            if (wide > MaxScrollWide + 0.001f)
                failures.Add("[viewport] '" + name + "' resolves to " + F(wide) + " viewports WIDE (budget " +
                             F(MaxScrollWide) + ") - " + F(contentW) + " px of board inside a " +
                             F(RefWellWidthPx) + " px well");
            if (tall > MaxScrollTall + 0.001f)
                failures.Add("[viewport] '" + name + "' resolves to " + F(tall) + " viewports TALL (budget " +
                             F(MaxScrollTall) + ") - " + F(contentH) + " px of board inside a " +
                             F(RefWellHeightPx) + " px well. A landscape well is SHORT: the many axis " +
                             "(track lanes) belongs on the WIDE side and progression on the short one. " +
                             "Feeding them the other way round is the WO-1310 defect");

            notes.Add(name + " board " + F(contentW) + "x" + F(contentH) + " px = " + F(wide) + "x" +
                      F(tall) + " viewports (budget " + F(MaxScrollWide) + "x" + F(MaxScrollTall) +
                      "), tightest inset " + F(Mathf.Min(pxMinX, pxMinY)) + " px vs clearance " +
                      F(clearHalf) + " px");
        }

        /// <summary>Nodes grouped into visual rows, BOTTOM ROW FIRST (largest y). Same
        /// clustering the shipped solver uses, so a row here is a row on screen.</summary>
        private static List<List<HeroTalentNodeDef>> ClusterRows(List<HeroTalentNodeDef> nodes)
        {
            var sorted = nodes.OrderByDescending(n => n.Y).ThenBy(n => n.X).ToList();
            var rows = new List<List<HeroTalentNodeDef>>();
            var current = new List<HeroTalentNodeDef>();
            float anchor = sorted[0].Y;
            foreach (var n in sorted)
            {
                if (current.Count > 0 && anchor - n.Y > RowClusterNorm)
                {
                    rows.Add(current);
                    current = new List<HeroTalentNodeDef>();
                    anchor = n.Y;
                }
                current.Add(n);
            }
            if (current.Count > 0) rows.Add(current);
            return rows;
        }

        /// <summary>Hiding a node does not rewrite the prerequisite graph, so a VISIBLE node
        /// whose prerequisite is hidden reads "Requires &lt;something you cannot see&gt;" forever
        /// (the caution written on HeroTalentNodeDef.Hidden itself).</summary>
        private static void CheckHiddenStranding(Dictionary<string, HeroTalentNodeDef> all,
                                                 List<string> failures)
        {
            foreach (var n in all.Values)
            {
                if (n.Hidden || n.Prerequisites == null) continue;
                foreach (var p in n.Prerequisites)
                {
                    HeroTalentNodeDef parent;
                    if (string.IsNullOrEmpty(p) || !all.TryGetValue(p, out parent)) continue;
                    if (parent.Hidden)
                        failures.Add("[hidden] visible node '" + n.Id + "' requires HIDDEN node '" + p +
                                     "' - it can never be unlocked and reads 'Requires " +
                                     (parent.Name ?? p) + "' against a node the player cannot see");
                }
            }
        }

        private static string Ids(List<HeroTalentNodeDef> row)
        {
            return string.Join(", ", row.Select(n => n.Id).ToArray());
        }

        private static string F(float v)
        {
            return v.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
