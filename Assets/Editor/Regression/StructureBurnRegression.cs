// =============================================================================
// StructureBurnRegression [structure-burn] - proves WO-761 fire lingers till repaired.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
//
// Drives the real DeNelle.Village.StructureBurn component (composed on a throwaway
// GameObject) through its production seam, with a stub IDamageableStructure standing
// in for a tower/wall, and PROVES the three load-bearing behaviours:
//   (1) IGNITE + TICK  - a burning structure loses HP over time via ApplyContactDamage.
//   (2) REPAIR = EXTINGUISH - restoring HP (fraction jumps back above 50%) stops the
//       burn on the next tick (self-detected; no repair-path hook needed).
//   (3) DESTROY - burn damage can bring the structure to 0; the burn then ends.
// Also asserts STACK = REFRESH (a re-ignite never double-composes / double-burns).
//
// VFXManager.Instance is null in edit mode, so StartFireVfx is a proven no-op here -
// this suite validates the DAMAGE + STATE machine; the fire VFX is null-safe.
//
// Marker: STRUCTURE_BURN_OK / STRUCTURE_BURN_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   Guard.Try(... () => { if (!StructureBurnRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[structure-burn] " + r); });
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class StructureBurnRegression
    {
        // A minimal burnable stand-in: HP on a 0..max scale, the same two verbs the
        // real structures expose to StructureBurn (IsAlive + ApplyContactDamage).
        private sealed class StubStructure : IDamageableStructure
        {
            public float Hp;
            public float Max;
            public bool IsAlive => Hp > 0f;
            public void ApplyContactDamage(float amount) => Hp = Mathf.Max(0f, Hp - amount);
            public float Fraction => Max > 0f ? Mathf.Clamp01(Hp / Max) : 0f;
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- STRUCTURE BURN (WO-761: fire lingers on <=50% structures till repaired/destroyed) ---");

            GameObject host = null;
            try
            {
                host = new GameObject("StructureBurnTestHost");
                var burn = host.AddComponent<StructureBurn>();

                // (1) IGNITE + TICK: a structure sitting at exactly 50% catches fire and drains.
                var stub = new StubStructure { Hp = 50f, Max = 100f };
                burn.Configure(stub, () => stub.Fraction, stub.Max);
                burn.Ignite();
                if (!burn.IsBurning) failures.Add("Ignite did not set IsBurning at 50% HP");

                float before = stub.Hp;
                for (int i = 0; i < 5; i++) burn.TickForTest(0.5f);   // 5 ticks * 0.5s
                log.AppendLine($"  after 5 ticks: HP {before:0.0} -> {stub.Hp:0.0} (burning={burn.IsBurning})");
                if (stub.Hp >= before) failures.Add($"burn ticks did not lower HP ({before:0.0} -> {stub.Hp:0.0})");
                if (!burn.IsBurning) failures.Add("burn extinguished on its own while still damaged (must NOT self-expire)");

                // STACK = REFRESH: a second ignite must not add a second StructureBurn.
                burn.Ignite();
                int comps = host.GetComponents<StructureBurn>().Length;
                if (comps != 1) failures.Add($"re-ignite stacked components ({comps} StructureBurn on host, expected 1)");

                // (2) REPAIR = EXTINGUISH: HP fraction jumps back above 50% -> burn stops.
                stub.Hp = stub.Max;                 // a full repair
                burn.TickForTest(0.5f);
                if (burn.IsBurning) failures.Add("repair did not extinguish the burn (still burning after HP restored)");
                float afterRepair = stub.Hp;
                burn.TickForTest(0.5f);
                if (stub.Hp < afterRepair) failures.Add("burn kept ticking AFTER extinguish (repaired structure still taking burn damage)");

                // (3) DESTROY: re-ignite low, burn all the way to 0, burn ends (no infinite loop).
                stub.Hp = 6f;
                burn.Ignite();
                if (!burn.IsBurning) failures.Add("re-ignite at 6% HP did not start a fresh burn");
                for (int i = 0; i < 40 && stub.Hp > 0f; i++) burn.TickForTest(0.5f);
                log.AppendLine($"  burn-to-death: HP now {stub.Hp:0.0} (burning={burn.IsBurning}, alive={stub.IsAlive})");
                if (stub.Hp > 0f) failures.Add("burn never destroyed the structure (HP stuck above 0)");
                if (burn.IsBurning) failures.Add("burn still active after the structure was destroyed (leaked DoT)");
            }
            catch (System.Exception ex)
            {
                failures.Add($"StructureBurn drive threw: {ex.Message}");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                log.AppendLine("STRUCTURE_BURN_OK");
                return "ignite<=50% -> tick DoT -> repair extinguishes -> burn-to-death ends (no self-expire, no stack)";
            }
            log.AppendLine("STRUCTURE_BURN_FAIL");
            foreach (var f in failures) log.AppendLine("  FAIL: " + f);
            return "STRUCTURE_BURN_FAIL: " + string.Join("; ", failures);
        }
    }
}
