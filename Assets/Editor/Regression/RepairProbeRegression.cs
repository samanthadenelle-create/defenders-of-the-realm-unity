// =============================================================================
// RepairProbeRegression — REP-1 headless probe: "repair spend succeeds but
// Buildings stay visually damaged."
// -----------------------------------------------------------------------------
// REGISTERED in DataRegression.RunAll as of WO-1496 (2026-09-06). It had opted out
// of RULE 2 with the standalone header token since REP-1 (the token is deleted from
// this file, not reworded); that opt-out is WITHDRAWN — it asserts a shipped-path
// contract (RepairFull
// reaches pristine) that belongs in the batch, not in a command somebody remembers.
// Still invokable standalone via:
//   run-unity-method.ps1 -Method DeNelle.Editor.RepairProbeRegression.RunStandalone
//                        -LogName repair-probe.log
// Contract mirrors the other suites: public static bool Run(out string reason);
// markers REPAIR_PROBE_OK (Debug.Log) / REPAIR_PROBE_FAIL (Debug.LogError).
//
// One run yields BOTH step-in/step-out closure lines (CLAUDE.md §12):
//   PATH 1 — LEGACY-MAGNITUDE PROOF: RepairTarget.Repair(100f) on a real
//     Building at hp=0. Building.Repair is additive-clamped
//     (_hp = min(_maxHp, _hp + amount), Building.cs:221-225) and buildings.json
//     authors MaxHp 120..240, so a fixed 100 lands HpFraction 0.42..0.83 with
//     NeedsRepair still true — the CAPTURED PROOF of the root cause. Logged as
//     PROOF, never assert-failed (the incremental primitive is intentional and
//     still reachable).
//   PATH 2 — SHIPPED-PATH VERIFICATION: reset to hp=0, call
//     RepairTarget.RepairFull() (the exact method ConfirmRepair and the
//     RepairAll fix lambda now invoke) and assert HpFraction >= 0.999.
//
// FIDELITY TRADEOFF (stated per the work order): WallRepairController.RepairAll
// itself is not driven here — its spend path needs a live EconomyService
// singleton + GameState mirror (Awake does not run on edit-mode AddComponent,
// so EconomyService.Instance stays null headless and CanAfford refuses every
// item). The probe drives RepairTarget.RepairFull() directly — the single
// full-restore primitive both charged call sites (WallRepairController.cs
// ConfirmRepair + the RepairAll Fix lambda) delegate to — so the repair
// contract is proven headless; the wallet/spend chain stays covered by the
// existing FlowTrace "Repair" lines + owner felt-verification.
//
// Masking-kind coverage: Wall (0..100 damage track) and Gate (MaxHp clamped
// <= 100) also run through RepairFull to prove the kinds that previously
// masked the bug still reach pristine.
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class RepairProbeRegression
    {
        /// <summary>Batchmode entry point (run-unity-method.ps1).</summary>
        public static void RunStandalone()
        {
            Run(out _);   // Run() emits the REPAIR_PROBE_OK / REPAIR_PROBE_FAIL marker
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            var created = new List<GameObject>();
            log.AppendLine("=== RepairProbeRegression (REP-1): real Building/Wall/Gate in, repair contract out ===");

            try
            {
                ProbeBuildings(created, failures, log);
                ProbeWall(created, failures, log);
                ProbeGate(created, failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"RepairProbeRegression threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                foreach (var go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }

            return Verdict(failures, log, out reason);
        }

        // =====================================================================
        //  Buildings — every catalog row, both paths
        // =====================================================================
        private static void ProbeBuildings(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            var defs = BuildingCatalog.Buildings;   // the REAL loader (buildings.json via CanonicalJson)
            if (defs == null || defs.Count == 0)
            {
                failures.Add("buildings.json hydrated 0 BuildingDefs (BuildingCatalog break) — probe undecidable");
                return;
            }

            int highMaxHp = 0, proofLines = 0;
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                var go = new GameObject("RepairProbe_" + def.Id);
                created.Add(go);
                var b = go.AddComponent<Building>();
                b.Configure(def);   // the REAL data-driven path: sets _maxHp/_hp from the catalog row

                var target = RepairTarget.TryWrap(b);
                if (target == null || !target.IsValid)
                {
                    failures.Add($"'{def.Id}': RepairTarget.TryWrap returned null/invalid for a live Building");
                    continue;
                }

                // ── PATH 1 — LEGACY-MAGNITUDE PROOF (log, never assert-fail) ──
                b.ApplyDamage(b.MaxHp * 2f);   // hp -> 0 through the real damage entry point
                target.Repair(100f);           // the pre-fix hardcoded magnitude, still reachable
                float legacyFrac = b.HpFraction;
                log.AppendLine($"  PROOF legacy-magnitude: '{def.Id}' MaxHp={b.MaxHp} Repair(100f) from hp=0 " +
                               $"-> HpFraction {legacyFrac:0.00} needsRepair={target.NeedsRepair}");
                if (b.MaxHp > 100.001f)
                {
                    highMaxHp++;
                    if (legacyFrac < 0.999f)
                        proofLines++;   // the under-repair reproduced — root cause captured
                    else
                        failures.Add($"'{def.Id}' (MaxHp {b.MaxHp}) reached FULL from Repair(100f) — the REP-1 " +
                                     "under-repair proof did not reproduce (Building.Repair magnitude semantics changed?)");
                }

                // ── PATH 2 — SHIPPED-PATH VERIFICATION (assert full restore) ──
                b.ApplyDamage(b.MaxHp * 2f);   // reset hp -> 0
                target.RepairFull();            // the exact call ConfirmRepair / RepairAll Fix now make
                log.AppendLine($"  SHIPPED RepairFull: '{def.Id}' MaxHp={b.MaxHp} from hp=0 " +
                               $"-> HpFraction {b.HpFraction:0.00} needsRepair={target.NeedsRepair}");
                if (b.HpFraction < 0.999f || target.NeedsRepair)
                    failures.Add($"'{def.Id}' (MaxHp {b.MaxHp}) RepairFull left HpFraction {b.HpFraction:0.00} " +
                                 $"needsRepair={target.NeedsRepair} — the charged repair still under-delivers");
            }

            if (highMaxHp == 0)
                failures.Add("no building authors MaxHp > 100 — the legacy-magnitude proof is vacuous (buildings.json changed?)");
            else
                log.AppendLine($"  buildings: {defs.Count} row(s) probed, {highMaxHp} with MaxHp>100, {proofLines} under-repair proof line(s)");
        }

        // =====================================================================
        //  Wall — the 0..100-damage-track kind that MASKED the bug
        // =====================================================================
        private static void ProbeWall(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            var go = new GameObject("RepairProbe_wall");
            created.Add(go);
            var wall = go.AddComponent<WallSegment>();
            wall.ApplyContactDamage(10000f);   // clamps the damage track to 100 (collapsed)

            var target = RepairTarget.TryWrap(wall);
            if (target == null) { failures.Add("wall: TryWrap returned null"); return; }

            target.RepairFull();
            log.AppendLine($"  SHIPPED RepairFull: wall damage-track from collapsed -> damageFraction {target.DamageFraction:0.00} " +
                           $"needsRepair={target.NeedsRepair}");
            if (target.NeedsRepair)
                failures.Add($"wall: RepairFull left damageFraction {target.DamageFraction:0.00} — the masking kind regressed");
        }

        // =====================================================================
        //  Gate — the MaxHp<=100 kind that MASKED the bug
        // =====================================================================
        private static void ProbeGate(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            var go = new GameObject("RepairProbe_gate");
            created.Add(go);
            var gate = go.AddComponent<Gate>();
            gate.TakeDamage(10000f);   // hp -> 0 (serialized defaults: hp/maxHp 100)

            var target = RepairTarget.TryWrap(gate);
            if (target == null) { failures.Add("gate: TryWrap returned null"); return; }

            target.RepairFull();
            log.AppendLine($"  SHIPPED RepairFull: gate MaxHp={gate.MaxHp} from hp=0 -> HpFraction {gate.HpFraction:0.00} " +
                           $"needsRepair={target.NeedsRepair}");
            if (gate.HpFraction < 0.999f || target.NeedsRepair)
                failures.Add($"gate: RepairFull left HpFraction {gate.HpFraction:0.00} — the masking kind regressed");
        }

        // =====================================================================
        //  Verdict + markers
        // =====================================================================
        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "REPAIR PROBE OK — legacy Repair(100f) under-repair PROOF captured on every MaxHp>100 building, " +
                         "and RepairFull (the shipped ConfirmRepair/RepairAll contract) fully restores every building + wall + gate";
                Debug.Log("REPAIR_PROBE_OK\n" + log);
                return true;
            }
            reason = $"REPAIR PROBE: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"REPAIR_PROBE_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
