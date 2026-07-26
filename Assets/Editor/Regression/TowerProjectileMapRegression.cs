// =============================================================================
// TowerProjectileMapRegression — owner VfxManualPicks per-tier tower projectiles.
// -----------------------------------------------------------------------------
// SOURCE-LINT (reads the tower .cs under Assets/_Modules/** + the generated
// HovlVfxCatalog.asset, no PlayMode) so it slots into the DataRegression batch and
// runs in seconds. Owner-tags-VFX / CLI-maps-verbatim: the owner tagged NEW per-tier
// archer projectile keys (ArcherTowerLevel1/2_Projectile) in the VfxCaster; the names
// ARE the mapping. This gate proves the mapping is actually WIRED and every projectile
// key a tower fires is CATALOGUED (a dangling key = a bare pellet at runtime, no error).
//
// Proves:
//   (a) DefenseTower.ProjectileKeyFor references BOTH per-tier archer keys
//       (ArcherTowerLevel1_Projectile + ArcherTowerLevel2_Projectile) AND the base/top
//       ArcherTower_Projectile — i.e. the tier 1/2/3 archer arrow ladder is wired.
//   (b) the arcane base/upgraded keys are wired (ArcaneTower.cs references
//       ARcaneTower_Projectile [upgraded] + ArcaneTower-Baselevel_Projectile [base]).
//   (c) EVERY "*_Projectile" string literal referenced in DefenseTower.cs + ArcaneTower.cs
//       is a catalogued key in Resources/VFX/HovlVfxCatalog.asset ("  - Key: <key>").
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!TowerProjectileMapRegression.Run(out var r)) failures.Add(r); else log...("[tower-proj-map] " + r);
//
// Marker: TOWER_PROJECTILE_MAP_OK / TOWER_PROJECTILE_MAP_FAIL: <reason>
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TowerProjectileMapRegression
    {
        private const string DefenseTowerRel = "_Modules/Village/Buildings/DefenseTower.cs";
        private const string ArcaneTowerRel  = "_Modules/Village/Buildings/ArcaneTower.cs";
        private const string CatalogRel      = "Resources/VFX/HovlVfxCatalog.asset";

        // Matches a "…_Projectile" string literal referenced in source (owner key convention).
        private static readonly Regex ProjKeyLiteral =
            new Regex("\"([A-Za-z0-9_\\-]+_Projectile)\"", RegexOptions.Compiled);

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TOWER PROJECTILE MAP (owner VfxManualPicks per-tier keys) ---");

            string dtPath  = Path.Combine(Application.dataPath, DefenseTowerRel);
            string atPath  = Path.Combine(Application.dataPath, ArcaneTowerRel);
            string catPath = Path.Combine(Application.dataPath, CatalogRel);

            string dtSrc = ReadOrFail(dtPath, "DefenseTower.cs", failures);
            string atSrc = ReadOrFail(atPath, "ArcaneTower.cs", failures);
            string catTxt = ReadOrFail(catPath, "HovlVfxCatalog.asset", failures);

            if (failures.Count > 0)
            {
                reason = "tower-proj-map: " + string.Join("; ", failures);
                Debug.LogError(log.ToString() + "TOWER_PROJECTILE_MAP_FAIL: " + reason);
                return false;
            }

            // (a) Per-tier archer ladder wired in DefenseTower.ProjectileKeyFor.
            foreach (var k in new[] { "ArcherTowerLevel1_Projectile", "ArcherTowerLevel2_Projectile", "ArcherTower_Projectile" })
                if (!dtSrc.Contains("\"" + k + "\""))
                    failures.Add($"DefenseTower.cs does NOT reference the archer key '{k}' — per-tier archer projectile mapping not wired");
            log.AppendLine("  (a) DefenseTower archer tier ladder: L1=ArcherTowerLevel1_Projectile, L2=ArcherTowerLevel2_Projectile, L3=ArcherTower_Projectile");

            // (b) Arcane base/upgraded keys wired in ArcaneTower.
            foreach (var k in new[] { "ARcaneTower_Projectile", "ArcaneTower-Baselevel_Projectile" })
                if (!atSrc.Contains("\"" + k + "\""))
                    failures.Add($"ArcaneTower.cs does NOT reference the arcane key '{k}' — base/upgraded arcane projectile mapping not wired");
            log.AppendLine("  (b) ArcaneTower base/upgraded: upgraded=ARcaneTower_Projectile, base=ArcaneTower-Baselevel_Projectile");

            // (c) Every "*_Projectile" key referenced by either tower is catalogued.
            var referenced = new SortedSet<string>();
            foreach (Match m in ProjKeyLiteral.Matches(dtSrc)) referenced.Add(m.Groups[1].Value);
            foreach (Match m in ProjKeyLiteral.Matches(atSrc)) referenced.Add(m.Groups[1].Value);

            if (referenced.Count == 0)
                failures.Add("no '*_Projectile' key literals found in DefenseTower.cs / ArcaneTower.cs (regex/convention drift?)");

            foreach (var key in referenced)
            {
                bool catalogued = catTxt.Contains("Key: " + key);
                if (!catalogued)
                    failures.Add($"projectile key '{key}' is referenced by a tower but is NOT catalogued in HovlVfxCatalog.asset (would fire a bare pellet at runtime)");
                log.AppendLine($"    key '{key}' -> catalogued={catalogued}");
            }
            log.AppendLine($"  (c) {referenced.Count} referenced projectile key(s) checked against the catalog");

            if (failures.Count == 0)
            {
                reason = "TOWER_PROJECTILE_MAP_OK";
                Debug.Log(log.ToString() + "TOWER_PROJECTILE_MAP_OK");
                return true;
            }

            reason = "tower-proj-map: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TOWER_PROJECTILE_MAP_FAIL: " + reason);
            return false;
        }

        private static string ReadOrFail(string path, string label, List<string> failures)
        {
            if (!File.Exists(path)) { failures.Add($"{label} not found at '{path}'"); return string.Empty; }
            try { return File.ReadAllText(path); }
            catch (System.Exception ex) { failures.Add($"{label} read threw: {ex.Message}"); return string.Empty; }
        }
    }
}
