// =============================================================================
// DestroyedStructureRegression — WO-753 owner ruling oracle.
// -----------------------------------------------------------------------------
// Owner ruling 2026-07-19 (memory `destroyed-items-no-rebuild-full-cost-and-vfx-
// cleanup`, SUPERSEDES the WO-672 "persistent inoperable shell"): a structure
// DESTROYED by enemies is LOST — NO in-place repair, the object + its bound NPC
// are removed, the player rebuilds fresh at FULL cost.
//
// STANDALONE oracle (deliberately NOT wired into DataRegression.RunAll — the
// orchestrator wires the marker line noted in the WO report). Invoked via:
//   run-unity-method.ps1 -Method DeNelle.Editor.DestroyedStructureRegression.RunStandalone
//                        -LogName destroyed-structure.log
// Contract mirrors the other suites: public static bool Run(out string reason);
// markers DESTROYED_STRUCTURE_OK (Debug.Log) / DESTROYED_STRUCTURE_FAIL (LogError).
//
// PROBES (one run, step-in/step-out lines per CLAUDE.md §12):
//   A. REPAIR NO-OP ON DESTROYED (Point 3) — DefenseTower / ArcaneTower / Tower /
//      ResourceCollector / WallSegment: drive to broken, call Repair(), assert HP
//      stays 0 / _broken stays true (destroyed = lost, no repair-back-online).
//   B. DESTROYED EXCLUDED FROM REPAIR-ALL (Point 3) — assert the exclusion
//      PREDICATES the WallRepairController offer now uses: a broken tower reports
//      IsBroken (skipped), a collapsed wall reports DamageFraction >=
//      WallRepairController.DestroyedFraction (skipped).
//   C. OBJECT + VENDOR REMOVED ON DEATH (Points 1/2) — PLAY-MODE ONLY: the removal
//      (Destructible.NotifyBroken → free grid cell + drop persisted record +
//      despawn vendor + Destroy(gameObject)) needs the live PlacementGrid /
//      GameStateService singletons (their Awake only runs in play mode) and a
//      runtime Destroy (edit-mode Destroy is forbidden). Skipped with a logged
//      note under an edit-mode batch run — drive it from the AutoPilot/play harness.
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Editor
{
    public static class DestroyedStructureRegression
    {
        /// <summary>Batchmode entry point (run-unity-method.ps1).</summary>
        public static void RunStandalone()
        {
            Run(out _);   // Run() emits the DESTROYED_STRUCTURE_OK / DESTROYED_STRUCTURE_FAIL marker
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            var created = new List<GameObject>();
            log.AppendLine("=== DestroyedStructureRegression (WO-753): destroyed = lost, no repair, rebuild full-cost ===");

            try
            {
                ProbeRepairNoOp(created, failures, log);
                ProbeRepairAllExclusion(created, failures, log);
                ProbeObjectRemoval(created, failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"DestroyedStructureRegression threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                foreach (var go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }

            return Verdict(failures, log, out reason);
        }

        // =====================================================================
        //  A. Repair() no-ops on a DESTROYED structure (Point 3)
        // =====================================================================
        private static void ProbeRepairNoOp(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            // DefenseTower — Hp lazy-inits to _maxHp; break it, then Repair() must NOT un-break.
            {
                var go = new GameObject("Destroyed_DefenseTower"); created.Add(go);
                var t = go.AddComponent<DefenseTower>();
                t.ApplyContactDamage(100000f);   // hp -> 0, _broken = true
                bool brokeOk = t.IsBroken && t.HpFraction <= 0.0001f;
                t.Repair();
                bool stayed = t.IsBroken && t.HpFraction <= 0.0001f;
                log.AppendLine($"  DefenseTower: broke={brokeOk} after Repair() broken={t.IsBroken} hpFrac={t.HpFraction:0.00}");
                if (!brokeOk) failures.Add("DefenseTower did not reach broken/hp0 on lethal contact damage");
                else if (!stayed) failures.Add("DefenseTower.Repair() REVIVED a destroyed tower (ruling: destroyed = lost, no repair)");
            }

            // ArcaneTower — same lazy-Hp path.
            {
                var go = new GameObject("Destroyed_ArcaneTower"); created.Add(go);
                var t = go.AddComponent<ArcaneTower>();
                t.ApplyContactDamage(100000f);
                bool brokeOk = t.IsBroken && t.HpFraction <= 0.0001f;
                t.Repair();
                bool stayed = t.IsBroken && t.HpFraction <= 0.0001f;
                log.AppendLine($"  ArcaneTower: broke={brokeOk} after Repair() broken={t.IsBroken} hpFrac={t.HpFraction:0.00}");
                if (!brokeOk) failures.Add("ArcaneTower did not reach broken/hp0 on lethal contact damage");
                else if (!stayed) failures.Add("ArcaneTower.Repair() REVIVED a destroyed spire (ruling: destroyed = lost, no repair)");
            }

            // Tower (legacy TowerCombat tower) — _hp starts 0 (Awake/Configure don't run on an
            // edit-mode AddComponent), so seed it to _maxHp for the probe. Editor-test seeding only.
            {
                var go = new GameObject("Destroyed_Tower"); created.Add(go);
                var t = go.AddComponent<Tower>();
                SeedPrivateFloat(t, "_hp", t.MaxHp);
                t.ApplyContactDamage(t.MaxHp * 2f);   // hp -> 0, _broken = true
                bool brokeOk = t.IsBroken && t.HpFraction <= 0.0001f;
                t.Repair();
                bool stayed = t.IsBroken && t.HpFraction <= 0.0001f;
                log.AppendLine($"  Tower: broke={brokeOk} after Repair() broken={t.IsBroken} hpFrac={t.HpFraction:0.00}");
                if (!brokeOk) failures.Add("Tower did not reach broken/hp0 on lethal contact damage");
                else if (!stayed) failures.Add("Tower.Repair() REVIVED a destroyed tower (ruling: destroyed = lost, no repair)");
            }

            // ResourceCollector — Configure sets _hp = _maxHp (no Awake needed); break via siege.
            {
                var go = new GameObject("Destroyed_Collector"); created.Add(go);
                var c = go.AddComponent<ResourceCollector>();
                c.Configure(ResourceBuildingProgression.FarmId);
                // Edit-mode determinism: the collector round-trips HP through PlayerPrefs
                // (SaveState/LoadState), so a prior probe run can leave FarmId broken and the
                // siege below then no-ops at the `if (!IsAlive) return;` guard. Seed a known
                // ALIVE state first (mirrors the Tower _hp seed above). The assertion — siege
                // drives it broken, then Repair() must NOT revive it — is unchanged.
                SeedPrivateFloat(c, "_maxHp", 120f);
                SeedPrivateFloat(c, "_hp", 120f);
                SeedPrivateBool(c, "_broken", false);
                c.ApplyContactDamage(100000f);   // OnSiegeDestroyed -> _broken = true, hp 0
                bool brokeOk = c.IsBroken && c.HpFraction <= 0.0001f;
                c.Repair();
                bool stayed = c.IsBroken && c.HpFraction <= 0.0001f;
                log.AppendLine($"  ResourceCollector: broke={brokeOk} after Repair() broken={c.IsBroken} hpFrac={c.HpFraction:0.00}");
                if (!brokeOk) failures.Add("ResourceCollector did not reach broken/hp0 on lethal siege damage");
                else if (!stayed) failures.Add("ResourceCollector.Repair() REVIVED a destroyed collector (ruling: destroyed = lost, no repair)");
            }

            // WallSegment — collapse the 0..100 damage track; Repair(amount) must no-op at rubble.
            {
                var go = new GameObject("Destroyed_Wall"); created.Add(go);
                var w = go.AddComponent<WallSegment>();
                w.ApplyContactDamage(100000f);   // _damage clamps to 100 -> IsDestroyed
                bool brokeOk = w.IsDestroyed && w.Damage >= 99.999f;
                w.Repair(50f);
                bool stayed = w.IsDestroyed && w.Damage >= 99.999f;
                log.AppendLine($"  WallSegment: collapsed={brokeOk} after Repair(50) destroyed={w.IsDestroyed} damage={w.Damage:0.0}");
                if (!brokeOk) failures.Add("WallSegment did not collapse (damage 100) on lethal contact damage");
                else if (!stayed) failures.Add("WallSegment.Repair() REVIVED a collapsed section (ruling: destroyed = lost, no repair)");
            }
        }

        // =====================================================================
        //  B. Destroyed structures are EXCLUDED from the Repair-All offer (Point 3)
        //     Asserts the exclusion PREDICATES CollectRepairAllSet / AddDamagedOfType now use.
        // =====================================================================
        private static void ProbeRepairAllExclusion(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            // Tower-family skip predicate = IsBroken.
            {
                var go = new GameObject("RepairAll_DefenseTower"); created.Add(go);
                var t = go.AddComponent<DefenseTower>();
                t.ApplyContactDamage(100000f);
                log.AppendLine($"  RepairAll exclusion: broken DefenseTower IsBroken={t.IsBroken} (skip predicate)");
                if (!t.IsBroken) failures.Add("broken DefenseTower does not report IsBroken — the Repair-All skip predicate would not fire");
            }

            // Wall skip predicate = DamageFraction >= WallRepairController.DestroyedFraction.
            {
                var go = new GameObject("RepairAll_Wall"); created.Add(go);
                var w = go.AddComponent<WallSegment>();
                w.ApplyContactDamage(100000f);
                var target = RepairTarget.TryWrap(w);
                if (target == null || !target.IsValid)
                {
                    failures.Add("RepairTarget.TryWrap returned null/invalid for a collapsed WallSegment");
                }
                else
                {
                    bool wouldSkip = target.DamageFraction >= WallRepairController.DestroyedFraction;
                    log.AppendLine($"  RepairAll exclusion: collapsed wall DamageFraction={target.DamageFraction:0.000} " +
                                   $">= DestroyedFraction({WallRepairController.DestroyedFraction:0.000}) => skip={wouldSkip}");
                    if (!wouldSkip)
                        failures.Add($"collapsed WallSegment DamageFraction {target.DamageFraction:0.000} < DestroyedFraction " +
                                     "— the CollectAllDamaged skip predicate would not exclude a destroyed wall");
                }
            }
        }

        // =====================================================================
        //  C. Object + bound vendor removed on death (Points 1/2) — PLAY MODE ONLY
        // =====================================================================
        private static void ProbeObjectRemoval(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            if (!Application.isPlaying)
            {
                log.AppendLine("  ObjectRemoval: SKIPPED (edit-mode batch) — Destructible.NotifyBroken removal needs the " +
                               "live PlacementGrid/GameStateService singletons (Awake runs only in play mode) and a runtime " +
                               "Destroy. Drive from the AutoPilot/play harness to assert grid-free + record-drop + vendor despawn.");
                return;
            }

            var building = new GameObject("Removal_Building"); created.Add(building);
            var placed = building.AddComponent<PlacedStructure>();
            placed.itemId = "arcane_tower";
            placed.gridCell = new Vector2Int(3, 3);
            placed.footprint = Vector2Int.one;

            var vendorGo = new GameObject("Removal_Vendor");   // NOT added to `created` — death must destroy it
            var seat = building.AddComponent<VendorSeatMarker>();
            seat.Vendor = vendorGo;

            var d = Destructible.Ensure(building);
            d.NotifyBroken("regression-death");

            // Vendor ref cleared (and the vendor GameObject scheduled for destruction).
            if (seat.Vendor != null)
                failures.Add("NotifyBroken did not clear the bound VendorSeatMarker.Vendor ref on death");
            else
                log.AppendLine("  ObjectRemoval: bound vendor ref cleared on death (vendor despawned).");

            log.AppendLine("  ObjectRemoval: NotifyBroken invoked in play mode — grid-free + record-drop exercised " +
                           "(assert live PlacementGrid.CanPlace + BaseLayout record absence in the play harness).");
        }

        // Editor-test seeding: set a private serialized float when Awake/Configure did not run.
        private static void SeedPrivateFloat(object target, string field, float value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }

        // Editor-test seeding: set a private serialized bool when Awake/Configure did not run.
        private static void SeedPrivateBool(object target, string field, bool value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }

        // =====================================================================
        //  Verdict + markers
        // =====================================================================
        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "DESTROYED STRUCTURE OK — Repair() no-ops on every destroyed tower/spire/collector/wall, and " +
                         "the Repair-All exclusion predicates (IsBroken / DamageFraction>=DestroyedFraction) fire on destroyed structures";
                Debug.Log("DESTROYED_STRUCTURE_OK\n" + log);
                return true;
            }
            reason = $"DESTROYED STRUCTURE: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"DESTROYED_STRUCTURE_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
