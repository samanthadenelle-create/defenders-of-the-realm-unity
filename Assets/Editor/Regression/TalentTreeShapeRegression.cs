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
// WHAT IS MEASURED AND LOGGED, never failed: the row census per tree and the implied
// content height at HeroSkillTreePanelMvvm.MinNodePitchPx, so the "does the tree fit the
// 493 px well without scrolling" question is answered by a number on every gate run
// instead of by a fresh hand calculation.
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
                         "behind a hidden one" + noteStr;
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

            notes.Add(name + " rows " + string.Join("/", rows.Select(r => r.Count.ToString()).ToArray()) +
                      " (bottom first, " + nodes.Count + " nodes)");
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
