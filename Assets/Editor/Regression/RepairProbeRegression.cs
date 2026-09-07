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
// WO-1537 (2026-09-07) - THE FIXTURE WAS WRONG, NOT THE REPAIR MATH. This header
// used to cite "Building.cs:221-225" for a bare additive clamp and drove every
// building to hp=0 before repairing. Two things had moved underneath it:
//   * Building.Repair now sits at Building.cs:256-269 and carries the WO-753
//     owner ruling guard at :260 - "if (IsDestroyed) return;" (destroyed = LOST,
//     it returns ONLY via a full-cost build-mode placement, never an in-place
//     repair). WallSegment.Repair carries the mirrored guard at
//     WallSegment.cs:504.
//   * So driving to hp=0 / damage=100 puts the fixture in the DESTROYED state
//     the ruling excludes from repair at all - RepairFull correctly no-ops and
//     the assert read as an under-delivering repair (10 buildings + the wall,
//     Builds/reg-wave2.log 2026-09-06). Gate passed only because Gate.Repair
//     carries no such guard.
// The fixture now drives structures to DAMAGED (hp=1 / damage=99), which is the
// state the charged repair flow actually addresses; the assertion is UNCHANGED
// (HpFraction >= 0.999 and !NeedsRepair on every catalog row). PROBE D was ADDED
// to pin the ruling itself from the RepairTarget seam. No production HP math was
// changed by WO-1537.
//
// One run yields BOTH step-in/step-out closure lines (CLAUDE.md §12):
//   PATH 1 - LEGACY-MAGNITUDE PROOF: RepairTarget.Repair(100f) on a real
//     Building at hp=1 (DAMAGED, not destroyed). Building.Repair is
//     additive-clamped (_hp = min(_maxHp, _hp + amount), Building.cs:267) and
//     buildings.json authors MaxHp 120..240, so a fixed 100 lands HpFraction
//     0.42..0.84 with NeedsRepair still true - the CAPTURED PROOF of the root
//     cause. Logged as PROOF, never assert-failed (the incremental primitive is
//     intentional and still reachable).
//   PATH 2 - SHIPPED-PATH VERIFICATION: reset to hp=1, call
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
// masked the bug still reach pristine. The Gate is driven to hp=0 DELIBERATELY:
// Gate carries no destroyed guard, so a hp-0 gate is the one structure the
// ConfirmRepair "rebuild" branch (DamageFraction >= DestroyedFraction -> full
// build cost) can still reach and restore. That is live behaviour, so it is
// asserted as-is.
//
//   PROBE D - WO-753 RULING PIN (added WO-1537): drive a Building and a
//     WallSegment to DESTROYED, call RepairTarget.RepairFull(), and assert the
//     structure STAYS destroyed. DestroyedStructureRegression pins the same
//     ruling on the raw Repair(amount) primitives; this pins it on the
//     RepairFull seam the charged paths actually call, which nothing covered.
//     Gate is deliberately NOT pinned here - it has no guard and no ruling.
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
                ProbeDestroyedStaysDestroyed(created, failures, log);
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

                // WO-1537: drive to DAMAGED (hp=1), never DESTROYED (hp=0). Building.Repair
                // refuses a destroyed building by the WO-753 ruling (Building.cs:260), so a
                // hp=0 fixture proves the ruling, not the repair math. hp=1 is the deepest
                // still-repairable state, so it remains the strictest honest fixture.
                if (b.Hp <= 1f)
                {
                    failures.Add($"'{def.Id}': Configure left Hp {b.Hp} <= 1 - the damaged fixture is undecidable " +
                                 "(buildings.json hp row changed?)");
                    continue;
                }

                // -- PATH 1 - LEGACY-MAGNITUDE PROOF (log, never assert-fail) --
                b.ApplyDamage(b.Hp - 1f);      // hp -> 1 through the real damage entry point
                target.Repair(100f);           // the pre-fix hardcoded magnitude, still reachable
                float legacyFrac = b.HpFraction;
                log.AppendLine($"  PROOF legacy-magnitude: '{def.Id}' MaxHp={b.MaxHp} Repair(100f) from hp=1 " +
                               $"-> HpFraction {legacyFrac:0.00} needsRepair={target.NeedsRepair}");
                // WO-1537: from hp=1 the legacy magnitude tops out at 101, so the under-repair
                // only reproduces on rows authoring MaxHp > 101 (all ten do today: 120..240).
                if (b.MaxHp > 101.001f)
                {
                    highMaxHp++;
                    if (legacyFrac < 0.999f)
                        proofLines++;   // the under-repair reproduced - root cause captured
                    else
                        failures.Add($"'{def.Id}' (MaxHp {b.MaxHp}) reached FULL from Repair(100f) - the REP-1 " +
                                     "under-repair proof did not reproduce (Building.Repair magnitude semantics changed?)");
                }

                // -- PATH 2 - SHIPPED-PATH VERIFICATION (assert full restore) --
                b.ApplyDamage(b.Hp - 1f);      // reset hp -> 1 (DAMAGED, still repairable)
                target.RepairFull();            // the exact call ConfirmRepair / RepairAll Fix now make
                log.AppendLine($"  SHIPPED RepairFull: '{def.Id}' MaxHp={b.MaxHp} from hp=1 " +
                               $"-> HpFraction {b.HpFraction:0.00} needsRepair={target.NeedsRepair}");
                if (b.HpFraction < 0.999f || target.NeedsRepair)
                    failures.Add($"'{def.Id}' (MaxHp {b.MaxHp}) RepairFull left HpFraction {b.HpFraction:0.00} " +
                                 $"needsRepair={target.NeedsRepair} — the charged repair still under-delivers");
            }

            if (highMaxHp == 0)
                failures.Add("no building authors MaxHp > 101 - the legacy-magnitude proof is vacuous (buildings.json changed?)");
            else
                log.AppendLine($"  buildings: {defs.Count} row(s) probed, {highMaxHp} with MaxHp>101, {proofLines} under-repair proof line(s)");
        }

        // =====================================================================
        //  Wall — the 0..100-damage-track kind that MASKED the bug
        // =====================================================================
        private static void ProbeWall(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            var go = new GameObject("RepairProbe_wall");
            created.Add(go);
            var wall = go.AddComponent<WallSegment>();

            // WO-1537: drive to HEAVILY DAMAGED (damage ~99), NOT collapsed. WallSegment.Repair
            // refuses a collapsed section by the WO-753 ruling (WallSegment.cs:504), so a
            // damage=100 fixture proved the ruling, not the repair contract. ApplyContactDamage
            // divides by tier toughness (>= 1) and by the BULWARK reduction (>= 0), so an
            // increment can only ever land at or BELOW its raw amount - 0.5f steps cannot
            // overshoot 100 from below 99.
            int guard = 0;
            while (wall.Damage < 99f && guard++ < 4000)
                wall.ApplyContactDamage(0.5f);
            if (wall.Damage < 99f || wall.IsDestroyed)
            {
                failures.Add($"wall: could not reach the DAMAGED fixture (damage {wall.Damage:0.0}, " +
                             $"destroyed={wall.IsDestroyed}, {guard} step(s)) - ApplyContactDamage scaling changed?");
                return;
            }

            var target = RepairTarget.TryWrap(wall);
            if (target == null) { failures.Add("wall: TryWrap returned null"); return; }

            float preDamage = wall.Damage;
            target.RepairFull();
            log.AppendLine($"  SHIPPED RepairFull: wall damage-track from {preDamage:0.0}/100 (damaged, not collapsed) " +
                           $"-> damageFraction {target.DamageFraction:0.00} needsRepair={target.NeedsRepair}");
            if (target.NeedsRepair)
                failures.Add($"wall: RepairFull left damageFraction {target.DamageFraction:0.00} - the masking kind regressed");
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
        //  PROBE D - WO-753 ruling pinned at the RepairFull seam (added WO-1537)
        //  Destroyed = LOST. RepairFull is the method both CHARGED call sites
        //  invoke (WallRepairController.ConfirmRepair + the RepairAll Fix
        //  lambda), so the ruling has to hold THERE, not only on the raw
        //  Repair(amount) primitives DestroyedStructureRegression covers.
        // =====================================================================
        private static void ProbeDestroyedStaysDestroyed(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            // Building - destroyed at hp 0 (Building.cs:260 guard).
            var defs = BuildingCatalog.Buildings;
            if (defs != null && defs.Count > 0)
            {
                BuildingDef pick = null;
                foreach (var d in defs)
                    if (d != null && !string.IsNullOrEmpty(d.Id)) { pick = d; break; }

                if (pick != null)
                {
                    var go = new GameObject("RepairProbe_destroyed_" + pick.Id);
                    created.Add(go);
                    var b = go.AddComponent<Building>();
                    b.Configure(pick);
                    b.ApplyDamage(b.MaxHp * 2f);   // hp -> 0 = DESTROYED

                    var t = RepairTarget.TryWrap(b);
                    if (t == null || !t.IsValid)
                    {
                        failures.Add("WO-753 pin: TryWrap returned null/invalid for a destroyed Building");
                    }
                    else
                    {
                        bool destroyedFirst = b.IsDestroyed;
                        t.RepairFull();
                        log.AppendLine($"  WO-753 PIN: destroyed building '{pick.Id}' RepairFull -> HpFraction " +
                                       $"{b.HpFraction:0.00} isDestroyed={b.IsDestroyed} (must stay destroyed)");
                        if (!destroyedFirst)
                            failures.Add($"WO-753 pin: '{pick.Id}' did not report IsDestroyed at hp 0 - the fixture is undecidable");
                        else if (!b.IsDestroyed || b.HpFraction > 0.0001f)
                            failures.Add($"WO-753 pin: RepairFull REVIVED destroyed building '{pick.Id}' to HpFraction " +
                                         $"{b.HpFraction:0.00} - the owner ruling (destroyed = lost, rebuild at full cost) regressed");
                    }
                }
            }

            // WallSegment - destroyed at damage 100 (WallSegment.cs:504 guard).
            {
                var go = new GameObject("RepairProbe_destroyed_wall");
                created.Add(go);
                var w = go.AddComponent<WallSegment>();
                w.ApplyContactDamage(100000f);   // damage -> 100 = COLLAPSED

                var t = RepairTarget.TryWrap(w);
                if (t == null || !t.IsValid)
                {
                    failures.Add("WO-753 pin: TryWrap returned null/invalid for a collapsed WallSegment");
                }
                else
                {
                    bool collapsedFirst = w.IsDestroyed;
                    t.RepairFull();
                    log.AppendLine($"  WO-753 PIN: collapsed wall RepairFull -> damage {w.Damage:0.0}/100 " +
                                   $"isDestroyed={w.IsDestroyed} (must stay collapsed)");
                    if (!collapsedFirst)
                        failures.Add("WO-753 pin: WallSegment did not collapse on lethal contact damage - the fixture is undecidable");
                    else if (!w.IsDestroyed || w.Damage < 99.999f)
                        failures.Add($"WO-753 pin: RepairFull REVIVED a collapsed WallSegment to damage {w.Damage:0.0} " +
                                     "- the owner ruling (destroyed = lost, rebuild at full cost) regressed");
                }
            }

            // Gate is deliberately NOT pinned: Gate.Repair carries no destroyed guard, and a
            // hp-0 gate is exactly what the ConfirmRepair "rebuild" branch (DamageFraction >=
            // DestroyedFraction -> full build cost) restores. Pinning it either way would
            // invent a ruling that does not exist. See the WO-1537 RESULT.
        }

        // =====================================================================
        //  Verdict + markers
        // =====================================================================
        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "REPAIR PROBE OK - legacy Repair(100f) under-repair PROOF captured on every MaxHp>101 building, " +
                         "RepairFull (the shipped ConfirmRepair/RepairAll contract) fully restores every DAMAGED building + wall + gate, " +
                         "and the WO-753 ruling holds at the RepairFull seam (a DESTROYED building/wall stays destroyed)";
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
