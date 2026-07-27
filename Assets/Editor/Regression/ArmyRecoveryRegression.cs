// =============================================================================
// ArmyRecoveryRegression — headless oracle for wounded-troop recovery (WO-781).
// -----------------------------------------------------------------------------
// Marker: TROOP_RECOVERY_OK / TROOP_RECOVERY_FAIL.
//
// ArmyStorage.TickRecovery used to have ZERO callers — wounded troops (set via
// ReconcileAfterRaid) never healed and the army silently degraded. This oracle
// proves the pure recovery math AND that a live caller exists:
//   1. TickRecovery(dt) pure step — RecoveryRemaining=10, dt=5 still wounded,
//      dt=6 more → healthy (clears Wounded, zero remaining).
//   2. AdvanceRecovery wall-clock path — past recoverAt heals; future stays
//      wounded; advancing past recovers; fresh-anchor seeds without retro heal.
//   3. Roster / veterancy / id preserved (no double-resurrect, no permadeath).
//   4. Reachability — TroopRecoveryService type exists (MonoBehaviour live caller)
//      AND source-lint: it calls AdvanceRecovery; ArmyStorage.AdvanceRecovery
//      calls TickRecovery (the zero-caller gap is closed).
//
// Pure EditMode-style logic + source-lint. No PlayMode. Never throws.
// Wire (DataRegression.RunAll):
//   if (!ArmyRecoveryRegression.Run(out var r)) failures.Add(r);
//   else log.AppendLine("[troop-recovery] " + r);
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless oracle for WO-781 wounded-troop recovery: pure TickRecovery /
    /// AdvanceRecovery math + live-caller reachability. Returns true + summary /
    /// false + detail; never throws.
    /// </summary>
    public static class ArmyRecoveryRegression
    {
        private const double T0 = 1_000_000.0;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== ArmyRecoveryRegression: wounded-troop recovery (WO-781) ===");

            try
            {
                CheckTickRecoveryPureStep(failures, log);
                CheckAdvanceRecoveryClock(failures, log);
                CheckNoDoubleResurrect(failures, log);
                CheckFreshAnchorGuard(failures, log);
                CheckLiveCallerReachability(failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"ArmyRecoveryRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "TROOP RECOVERY OK — TickRecovery pure step + AdvanceRecovery clock + " +
                         "no double-resurrect + fresh-anchor guard + TroopRecoveryService live caller";
                Debug.Log("TROOP_RECOVERY_OK\n" + log);
                return true;
            }

            reason = $"TROOP RECOVERY: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError("TROOP_RECOVERY_FAIL: " + reason + "\n" + log);
            return false;
        }

        // ── 1. pure TickRecovery(dt) step ─────────────────────────────────────
        private static void CheckTickRecoveryPureStep(List<string> failures, StringBuilder log)
        {
            var army = new ArmyStorage();
            var troop = new PlayerTroop("troop-1", "troop-footman");
            army.Owned.Add(troop);
            army.MarkWounded(troop, 10f);

            int half = army.TickRecovery(5f);
            if (half != 0)
                failures.Add($"TickRecovery(5) on 10s recovery returned {half} recovered (want 0)");
            if (!troop.Wounded)
                failures.Add("TickRecovery(5) on 10s recovery cleared Wounded early");
            if (troop.RecoveryRemaining < 4.5f || troop.RecoveryRemaining > 5.5f)
                failures.Add($"TickRecovery(5) left RecoveryRemaining={troop.RecoveryRemaining} (want ~5)");

            int full = army.TickRecovery(6f);
            if (full != 1)
                failures.Add($"TickRecovery(6) after partial returned {full} recovered (want 1)");
            if (troop.Wounded)
                failures.Add("TickRecovery past remaining did not clear Wounded");
            if (troop.RecoveryRemaining != 0f)
                failures.Add($"healed troop RecoveryRemaining={troop.RecoveryRemaining} (want 0)");
            if (!troop.IsDeployable)
                failures.Add("healed troop is not IsDeployable");

            // dt <= 0 no-op
            army.MarkWounded(troop, 30f);
            if (army.TickRecovery(0f) != 0 || army.TickRecovery(-1f) != 0)
                failures.Add("TickRecovery(dt<=0) must be a no-op");
            if (!troop.Wounded || troop.RecoveryRemaining != 30f)
                failures.Add("TickRecovery(dt<=0) mutated a wounded troop");

            log.AppendLine("  TickRecovery pure step (10s: dt=5 still wounded, dt=6 heals) OK");
        }

        // ── 2. AdvanceRecovery wall-clock path (offline + live) ───────────────
        private static void CheckAdvanceRecoveryClock(List<string> failures, StringBuilder log)
        {
            // Seed anchor first, THEN wound — matches live: wound while clock is running.
            var army = new ArmyStorage();
            var troop = new PlayerTroop("troop-2", "troop-archer");
            army.Owned.Add(troop);
            army.AdvanceRecovery(T0);                 // seed; credit nothing
            army.MarkWounded(troop, 300f);

            // Future recoverAt → still wounded
            int mid = army.AdvanceRecovery(T0 + 60_000.0);   // 60s < 300s
            if (mid != 0)
                failures.Add($"AdvanceRecovery mid-window returned {mid} recovered (want 0)");
            if (!troop.Wounded)
                failures.Add("AdvanceRecovery mid-window cleared Wounded early");
            if (troop.RecoveryRemaining < 239f || troop.RecoveryRemaining > 241f)
                failures.Add($"mid-window RecoveryRemaining={troop.RecoveryRemaining} (want ~240)");

            // Advance past remaining → heals
            int done = army.AdvanceRecovery(T0 + 60_000.0 + 300_000.0);
            if (done != 1)
                failures.Add($"AdvanceRecovery past recoverAt returned {done} recovered (want 1)");
            if (troop.Wounded)
                failures.Add("AdvanceRecovery past recoverAt left troop Wounded");

            // Past-in-one-step path: wound at T0, jump 120s with 60s recovery
            var army2 = new ArmyStorage();
            var t2 = new PlayerTroop("troop-3", "troop-footman");
            army2.Owned.Add(t2);
            army2.AdvanceRecovery(T0);
            army2.MarkWounded(t2, 60f);
            int past = army2.AdvanceRecovery(T0 + 120_000.0);
            if (past != 1 || t2.Wounded)
                failures.Add("AdvanceRecovery with recoverAt in the past did not heal");

            log.AppendLine("  AdvanceRecovery clock (future stays wounded, past heals) OK");
        }

        // ── 3. no double-resurrect / roster stable ────────────────────────────
        private static void CheckNoDoubleResurrect(List<string> failures, StringBuilder log)
        {
            var army = new ArmyStorage();
            var troop = new PlayerTroop("troop-7", "troop-footman") { VeterancyRank = 3 };
            army.Owned.Add(troop);
            army.AdvanceRecovery(T0);
            army.MarkWounded(troop, 60f);
            army.AdvanceRecovery(T0 + 120_000.0);

            if (army.Owned == null || army.Owned.Count != 1)
                failures.Add("recovery added/removed a troop (must never delete — wounded-recovery model)");
            if (troop.Id != "troop-7")
                failures.Add("recovery mutated PlayerTroop.Id (OwnedTroopId must stay stable)");
            if (troop.VeterancyRank != 3)
                failures.Add("recovery mutated VeterancyRank");

            int again = army.AdvanceRecovery(T0 + 999_000.0);
            if (again != 0)
                failures.Add($"second AdvanceRecovery re-healed {again} (not idempotent)");

            log.AppendLine("  roster/id/veterancy preserved + idempotent heal OK");
        }

        // ── 4. fresh-anchor guard (pre-WO save can't bank giant first-load heal) ─
        private static void CheckFreshAnchorGuard(List<string> failures, StringBuilder log)
        {
            var army = new ArmyStorage();
            var troop = new PlayerTroop("troop-fresh", "troop-footman");
            army.Owned.Add(troop);
            army.MarkWounded(troop, 300f);   // LastRecoveryTickMs still 0

            int recovered = army.AdvanceRecovery(T0 + 10_000_000.0);
            if (recovered != 0)
                failures.Add($"fresh-anchor AdvanceRecovery recovered {recovered} (must seed only, credit nothing)");
            if (!troop.Wounded)
                failures.Add("fresh-anchor cleared Wounded (retroactive over-heal)");
            if (army.LastRecoveryTickMs != T0 + 10_000_000.0)
                failures.Add($"fresh-anchor LastRecoveryTickMs={army.LastRecoveryTickMs} (want seeded to now)");

            log.AppendLine("  fresh-anchor seeds without retroactive heal OK");
        }

        // ── 5. live caller reachability (type + source-lint) ──────────────────
        private static void CheckLiveCallerReachability(List<string> failures, StringBuilder log)
        {
            // Type exists and is a MonoBehaviour (the runtime tick host).
            var t = typeof(TroopRecoveryService);
            if (t == null)
                failures.Add("TroopRecoveryService type missing (zero-caller gap not closed)");
            else if (!typeof(MonoBehaviour).IsAssignableFrom(t))
                failures.Add("TroopRecoveryService is not a MonoBehaviour");

            // Source-lint: the service must call AdvanceRecovery (the live path).
            string svcPath = Path.Combine(Application.dataPath,
                "_Modules/Village/Troops/TroopRecoveryService.cs");
            if (!File.Exists(svcPath))
                failures.Add("TroopRecoveryService.cs missing on disk");
            else
            {
                string svcTxt = File.ReadAllText(svcPath);
                if (!svcTxt.Contains("AdvanceRecovery"))
                    failures.Add("TroopRecoveryService.cs does not call AdvanceRecovery (live tick dead)");
                if (!svcTxt.Contains("TimeSource.NowUnixMs"))
                    failures.Add("TroopRecoveryService.cs does not use TimeSource.NowUnixMs (queue clock)");
            }

            // Source-lint: AdvanceRecovery must call TickRecovery (the pure step is wired).
            string armyPath = Path.Combine(Application.dataPath,
                "_Modules/Core/State/ArmyStorage.cs");
            if (!File.Exists(armyPath))
                failures.Add("ArmyStorage.cs missing on disk");
            else
            {
                string armyTxt = File.ReadAllText(armyPath);
                // AdvanceRecovery body must invoke TickRecovery (not just mention it in docs).
                int advIdx = armyTxt.IndexOf("public int AdvanceRecovery");
                int tickCall = armyTxt.IndexOf("TickRecovery((float)", advIdx >= 0 ? advIdx : 0);
                if (advIdx < 0)
                    failures.Add("ArmyStorage.AdvanceRecovery method missing");
                else if (tickCall < 0)
                    failures.Add("ArmyStorage.AdvanceRecovery does not call TickRecovery");
            }

            log.AppendLine("  live caller (TroopRecoveryService → AdvanceRecovery → TickRecovery) OK");
        }
    }
}
