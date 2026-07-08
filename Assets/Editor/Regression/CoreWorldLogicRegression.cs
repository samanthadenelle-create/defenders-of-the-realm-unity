// =============================================================================
// CoreWorldLogicRegression — the shared world classifier + spawn-roster contract.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core). Headless, pure logic
// (no scene / no PlayMode). ZoneManager + RegionSpawnTable are THE single source of
// truth for "which region am I in, how deep, and which enemy roams here" (harvest,
// raids, crystal grades, roaming spawns all call them). This oracle proves their
// invariants from the REAL static tables, not a re-derivation:
//   • ZoneManager: origin -> Village; the four cardinals classify to the right region;
//     Village danger tier 0, outer regions tier > 0; ThreatLevel(Village)==0 and the
//     Ashwood core out-scales the Goldfields edge; DefaultZoneGraph is symmetric
//     (if A borders B then B borders A) and every region record resolves.
//   • RegionSpawnTable: every non-Village region HasRoster (Village does NOT); at any
//     depth band + any roll, PickEnemyId returns a NON-NULL id that BELONGS to that
//     region's roster; and Village PickEnemyId returns null (spawns nothing).
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!CoreWorldLogicRegression.Run(out var coreWorldReason)) failures.Add(coreWorldReason); else log.AppendLine("[core-world] " + coreWorldReason);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Editor
{
    public static class CoreWorldLogicRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CORE WORLD LOGIC (ZoneManager + RegionSpawnTable) ---");

            // ── ZoneManager: classification of the cardinals ─────────────────────────
            AssertZone(failures, Vector3.zero, RegionId.Village, "origin");
            AssertZone(failures, new Vector3(500f, 0f, 0f), RegionId.Goldfields, "far +X (East)");
            AssertZone(failures, new Vector3(-500f, 0f, 0f), RegionId.Stoneback, "far -X (West)");
            AssertZone(failures, new Vector3(0f, 0f, 500f), RegionId.Ashwood, "far +Z (North)");
            AssertZone(failures, new Vector3(0f, 0f, -500f), RegionId.Mirewood, "far -Z (South)");

            // ── ZoneManager: danger tiers + region records resolve ───────────────────
            foreach (RegionId id in System.Enum.GetValues(typeof(RegionId)))
            {
                if (!ZoneManager.Regions.TryGetValue(id, out var rec) || rec == null)
                { failures.Add($"ZoneManager.Regions has no record for '{id}'"); continue; }
                if (id == RegionId.Village && rec.DangerTier != 0)
                    failures.Add($"Village danger tier should be 0 (is {rec.DangerTier})");
                if (id != RegionId.Village && rec.DangerTier <= 0)
                    failures.Add($"outer region '{id}' danger tier should be > 0 (is {rec.DangerTier})");
            }

            // ── ZoneManager: ThreatLevel two-axis read ───────────────────────────────
            if (ZoneManager.ThreatLevel(Vector3.zero) != 0)
                failures.Add("ThreatLevel(origin/Village) should be 0");
            int ashCore = ZoneManager.ThreatLevel(new Vector3(0f, 0f, 500f)); // Ashwood core (tier4, deep)
            int goldEdge = ZoneManager.ThreatLevel(new Vector3(60f, 0f, 0f)); // Goldfields just past wall
            if (ashCore <= goldEdge)
                failures.Add($"Ashwood core ThreatLevel ({ashCore}) should out-scale Goldfields edge ({goldEdge})");

            // ── ZoneManager: DefaultZoneGraph symmetry + count ───────────────────────
            var graph = ZoneManager.DefaultZoneGraph();
            if (graph == null || graph.Count != 5)
                failures.Add($"DefaultZoneGraph should have 5 zones (has {(graph?.Count ?? 0)})");
            else
            {
                var byKey = new Dictionary<string, ZoneState>();
                foreach (var z in graph) if (z != null && !byKey.ContainsKey(z.RegionKey)) byKey[z.RegionKey] = z;
                foreach (var z in graph)
                {
                    if (z?.Neighbors == null) continue;
                    foreach (var n in z.Neighbors)
                    {
                        if (!byKey.TryGetValue(n, out var other))
                        { failures.Add($"zone '{z.RegionKey}' lists neighbor '{n}' not in the graph"); continue; }
                        if (other.Neighbors == null || !other.Neighbors.Contains(z.RegionKey))
                            failures.Add($"asymmetric adjacency: '{z.RegionKey}' -> '{n}' but not back");
                    }
                }
            }

            // ── RegionSpawnTable: roster presence + PickEnemyId never-null/in-roster ──
            if (RegionSpawnTable.HasRoster(RegionId.Village))
                failures.Add("Village should NOT have a roaming roster");
            if (RegionSpawnTable.PickEnemyId(RegionId.Village, 0.5f, 0.5f) != null)
                failures.Add("RegionSpawnTable.PickEnemyId(Village) should return null (spawns nothing)");

            float[] depths = { 0f, 0.34f, 0.5f, 0.67f, 1f };
            float[] rolls = { 0f, 0.25f, 0.5f, 0.75f, 0.999f };
            foreach (RegionId id in System.Enum.GetValues(typeof(RegionId)))
            {
                if (id == RegionId.Village) continue;
                if (!RegionSpawnTable.HasRoster(id))
                { failures.Add($"outer region '{id}' has NO roster (RegionSpawnTable.HasRoster false)"); continue; }

                var roster = RegionSpawnTable.RosterFor(id);
                var validIds = new HashSet<string>();
                if (roster != null) foreach (var e in roster) validIds.Add(e.EnemyId);

                foreach (var d in depths)
                    foreach (var rr in rolls)
                    {
                        string picked = RegionSpawnTable.PickEnemyId(id, d, rr);
                        if (string.IsNullOrEmpty(picked))
                            failures.Add($"PickEnemyId('{id}',depth={d:F2},roll={rr:F2}) returned null/empty (region must never be empty)");
                        else if (!validIds.Contains(picked))
                            failures.Add($"PickEnemyId('{id}',depth={d:F2},roll={rr:F2}) returned '{picked}' NOT in the region roster");
                    }
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CORE_WORLD_OK");
                reason = "CORE WORLD LOGIC OK — ZoneManager classification/threat/graph + RegionSpawnTable roster invariants hold";
                return true;
            }
            reason = "core-world: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CORE_WORLD_FAIL: " + reason);
            return false;
        }

        private static void AssertZone(List<string> failures, Vector3 pos, RegionId expected, string label)
        {
            var got = ZoneManager.GetZone(pos);
            if (got != expected)
                failures.Add($"GetZone({label}) expected {expected} but got {got}");
        }
    }
}
