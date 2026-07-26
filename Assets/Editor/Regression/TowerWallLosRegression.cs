// =============================================================================
// TowerWallLosRegression [tower-wall-los] — locks the "towers shoot through walls"
// fix (owner felt-test 2026-07). Two halves must both hold, or a tower fires
// straight through the perimeter again:
//
//   FIX 1 — every wall SPAWNER puts its wall (and gate) pieces on the "Structure"
//           physics layer, because the towers' line-of-sight linecast is masked to
//           that layer. Asserts WallSegment, Village2Generator and BaseLayoutLoader
//           each set the "Structure" layer.
//   FIX 2 — DefenseTower and ArcaneTower each gate their acquire path with a
//           Structure-mask Physics.Linecast LoS reject (mirroring TowerCombat.
//           BlockedByWall). Both towers previously had NO LoS check at all.
//
// Source-lint (edit-mode, no PlayMode) — mirrors the dungeon regressions. Wired into
// DeNelle.Editor.DataRegression.RunAll. NEVER throws (missing file => a listed fail).
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TowerWallLosRegression
    {
        public static bool Run(out string reason)
        {
            string assets = Application.dataPath;
            string wallSeg  = Path.Combine(assets, "_Modules/Village/Walls/WallSegment.cs");
            string village2 = Path.Combine(assets, "_Village2/Village2Generator.cs");
            string baseLoad = Path.Combine(assets, "_Modules/Village/BuildMode/BaseLayoutLoader.cs");
            string defTower = Path.Combine(assets, "_Modules/Village/Buildings/DefenseTower.cs");
            string arcTower = Path.Combine(assets, "_Modules/Village/Buildings/ArcaneTower.cs");

            var fails = new List<string>();

            // ── FIX 1 — each wall spawner sets the "Structure" layer on walls ──────
            RequireStructureLayer(wallSeg,  "WallSegment.RebuildCollider", fails);
            RequireStructureLayer(village2, "Village2Generator (wall/gate pieces)", fails);
            RequireStructureLayer(baseLoad, "BaseLayoutLoader.Spawn (build-mode wall/gate)", fails);

            // ── FIX 2 — DefenseTower + ArcaneTower gate acquire with a Structure-mask LoS reject ──
            RequireLosGate(defTower, "DefenseTower", fails);
            RequireLosGate(arcTower, "ArcaneTower",  fails);

            if (fails.Count == 0)
            {
                Debug.Log("TOWER_WALL_LOS_OK");
                reason = "TOWER WALL LOS OK — walls carry the \"Structure\" layer at spawn (WallSegment/" +
                         "Village2Generator/BaseLayoutLoader) and DefenseTower+ArcaneTower reject shots " +
                         "blocked by a Structure-mask linecast";
                return true;
            }
            reason = "tower-wall-los: " + string.Join("; ", fails);
            Debug.LogError("TOWER_WALL_LOS_FAIL: " + reason);
            return false;
        }

        /// <summary>Asserts the file assigns the "Structure" layer (NameToLayer/GetMask + a layer set).</summary>
        private static void RequireStructureLayer(string path, string label, List<string> fails)
        {
            if (!File.Exists(path)) { fails.Add($"{label}: source file missing ({path})"); return; }
            string txt = File.ReadAllText(path);
            bool resolvesLayer = txt.Contains("NameToLayer(\"Structure\")") || txt.Contains("GetMask(\"Structure\")");
            bool assignsLayer  = Regex.IsMatch(txt, @"\.layer\s*=") || Regex.IsMatch(txt, @"gameObject\.layer\s*=");
            if (!resolvesLayer)
                fails.Add($"{label}: does not resolve the \"Structure\" layer (NameToLayer/GetMask) — walls stay on Default and towers shoot through them");
            if (!assignsLayer)
                fails.Add($"{label}: resolves \"Structure\" but never assigns it to a wall's .layer");
        }

        /// <summary>Asserts the tower rejects a candidate via a Structure-mask Physics.Linecast in its acquire path.</summary>
        private static void RequireLosGate(string path, string label, List<string> fails)
        {
            if (!File.Exists(path)) { fails.Add($"{label}: source file missing ({path})"); return; }
            string txt = File.ReadAllText(path);
            if (!txt.Contains("GetMask(\"Structure\")"))
                fails.Add($"{label}: no Structure LayerMask cached (GetMask(\"Structure\")) for the LoS gate");
            if (!txt.Contains("Physics.Linecast"))
                fails.Add($"{label}: no Physics.Linecast LoS check — its acquire path still shoots through walls");
            if (!txt.Contains("BlockedByWall"))
                fails.Add($"{label}: no BlockedByWall gate wired into the acquire loop");
            // The flyer exemption must survive (a ground wall must not silence the tower vs. the apex dragon).
            if (!Regex.IsMatch(txt, @"CombatLayer\.Flying\)\s*return\s+false"))
                fails.Add($"{label}: LoS gate is missing the flyer exemption (CombatLayer.Flying -> return false)");
        }
    }
}
