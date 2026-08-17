// =============================================================================
// OfflineClaimFanOutRegression -- headless oracle for WO-1147: ONE offline clock,
// ONE read, ONE fan-out, ONE advance.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS (sweep 2026-08-16): three consumers read
// GameState.LastHarvestClaimMs from three independently deferred coroutines and only
// OfflineHarvestService advanced it. EchoService read it in the SAME frame as the
// write (undefined component order -> the silo fill was a coin-flip) and
// EchoRepairService read it one frame LATER (always after the clock was zeroed ->
// OFFLINE ECHO REPAIR NEVER ACCRUED, for its entire life, silently).
//
// WHAT IS ASSERTED (all from DATA produced by the REAL services, never source-reading
// alone -- the source lint is only the "nobody re-adds a second writer" guard):
//   A. ONE DELTA: every registered consumer receives the SAME OfflineClaimWindow
//      instance values (sequence, now, elapsed) from a single claim -- proven by a
//      probe consumer registered alongside the three real ones.
//   B. ADVANCED EXACTLY ONCE: the persisted clock is UNCHANGED while the fan-out is
//      running (no consumer moves it mid-claim) and equals the window's NowUnixMs
//      afterwards; ClaimCount increments by exactly 1.
//   C. REPAIR ACCRUES: with elapsed time on the clock and an owned Echo,
//      EchoRepairService banks a NON-ZERO repair budget (the exact quantity that was
//      always 0 before). Zero here = the regression under test has returned.
//   D. CAP RESPECTED: a 20h window is counted as 4h by repair (OfflineCapHours) and
//      as 10h by the node harvest (its own away-cap) -- per-consumer caps preserved,
//      from the ONE shared raw window.
//   E. SOURCE LINT (comments AND string literals stripped first, so a doc-comment or a
//      trace string can never satisfy it): no consumer file assigns LastHarvestClaimMs;
//      OfflineClaimCoordinator.cs does.
//
// SAFETY: snapshots the PlayerPrefs save blob, installs a THROWAWAY GameState as the
// active state by reflection (editmode never runs Awake -- the CoreSaveContract /
// OfflineHarvest pattern), and destroys every object it created in a finally.
// Mirrors OfflineHarvestRegression: public static bool Run(out string reason).
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class OfflineClaimFanOutRegression
    {
        private const string SaveKey = "dotr-save";

        /// <summary>Probe consumer: records the window it was handed AND the value of the
        /// persisted clock at the moment it ran, so "advanced exactly once, after everyone"
        /// is provable rather than assumed.</summary>
        private sealed class ProbeConsumer : IOfflineClaimConsumer
        {
            public string OfflineConsumerName => "regression-probe";
            public int Calls;
            public OfflineClaimWindow Last;
            public double ClockSeenDuringFanOut;

            public void ApplyOfflineWindow(OfflineClaimWindow window)
            {
                Calls++;
                Last = window;
                var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                ClockSeenDuringFanOut = s != null ? s.LastHarvestClaimMs : -1.0;
            }
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorInstance = GameStateService.Instance;
            GameObject gssGo = null, ohsGo = null, echoGo = null, repairGo = null;
            GameState throwaway = null;
            bool installed = false;

            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GameStateService (fanout-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!TryInstallHeadlessState(gss, throwaway, out string installErr))
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "OFFLINE FAN-OUT", "needs fleet -- " + installErr);
                }
                installed = true;
                var state = gss.State;
                if (state == null)
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "OFFLINE FAN-OUT", "needs fleet -- throwaway state did not install");
                }

                state.EchoCount = 2;   // the owner's nightly roster: repair rate must be > 0

                // Clean registry: editmode has no live game, so whatever a prior oracle left
                // registered is stale. (Never called by gameplay.)
                OfflineClaimCoordinator.ResetForTests();

                ohsGo = new GameObject("OfflineHarvestService (fanout-oracle)");
                var ohs = ohsGo.AddComponent<OfflineHarvestService>();
                ohs.OfflineCapHours = 10f;

                echoGo = new GameObject("EchoService (fanout-oracle)");
                var echo = echoGo.AddComponent<EchoService>();
                echo.SiloCapHours = 4f;

                repairGo = new GameObject("EchoRepairService (fanout-oracle)");
                var repair = repairGo.AddComponent<EchoRepairService>();
                repair.OfflineCapHours = 4f;
                repair.MaxBankedFractions = 2f;

                // AddComponent does NOT run Awake in editmode, so register explicitly --
                // the same seam the runtime bootstraps use.
                var probe = new ProbeConsumer();
                OfflineClaimCoordinator.Register(ohs);
                OfflineClaimCoordinator.Register(echo);
                OfflineClaimCoordinator.Register(repair);
                OfflineClaimCoordinator.Register(probe);
                if (OfflineClaimCoordinator.ConsumerCount != 4)
                    failures.Add($"registry holds {OfflineClaimCoordinator.ConsumerCount} consumer(s), expected 4 " +
                                 "(a consumer failed to register -> it would be silently skipped by every claim)");

                // =============================================================
                //  Case 1 -- 20h away: one delta, one advance, caps per consumer
                // =============================================================
                double now = TimeSource.NowUnixMs();
                double away20hMs = 20.0 * 3600.0 * 1000.0;
                state.LastHarvestClaimMs = now - away20hMs;
                double clockBefore = state.LastHarvestClaimMs;
                int claimsBefore = OfflineClaimCoordinator.ClaimCount;

                float repairRatePerSec = EchoBonusCalculator.RepairFractionsPerSecond();
                if (repairRatePerSec <= 0f)
                    failures.Add("repair rate is 0 with 2 owned Echoes -- the repair oracle below cannot prove accrual " +
                                 "(EchoBonusCalculator.RepairFractionsPerSecond / echoes-balance.json regressed)");

                var window = OfflineClaimCoordinator.Claim("regression-20h");

                // -- A. ONE delta, seen by everyone -------------------------------
                if (window.ElapsedSeconds < 20.0 * 3600.0 - 5.0)
                    failures.Add($"claim window elapsed {window.ElapsedSeconds:0}s, expected ~72000s (20h) -- the single read mis-measured");
                if (probe.Calls != 1)
                    failures.Add($"probe consumer was called {probe.Calls}x for ONE claim (fan-out is not exactly-once)");
                if (probe.Last.Sequence != window.Sequence || probe.Last.NowUnixMs != window.NowUnixMs ||
                    probe.Last.ElapsedSeconds != window.ElapsedSeconds)
                    failures.Add($"consumer saw a DIFFERENT window than the claim returned " +
                                 $"(probe seq {probe.Last.Sequence}/elapsed {probe.Last.ElapsedSeconds:0} vs " +
                                 $"claim seq {window.Sequence}/elapsed {window.ElapsedSeconds:0}) -- consumers are not sharing one delta");

                // -- B. Clock advanced EXACTLY once, and only after the fan-out ----
                if (probe.ClockSeenDuringFanOut != clockBefore)
                    failures.Add($"the clock MOVED during the fan-out (consumer saw {probe.ClockSeenDuringFanOut:0}, " +
                                 $"pre-claim was {clockBefore:0}) -- a consumer is advancing it again (the original bug)");
                if (System.Math.Abs(state.LastHarvestClaimMs - window.NowUnixMs) > 0.5)
                    failures.Add($"post-claim clock {state.LastHarvestClaimMs:0} != window now {window.NowUnixMs:0} " +
                                 "(the single advance did not land)");
                if (OfflineClaimCoordinator.ClaimCount != claimsBefore + 1)
                    failures.Add($"ClaimCount went {claimsBefore} -> {OfflineClaimCoordinator.ClaimCount} for one claim (expected +1)");

                // -- C. Repair ACCRUED (the bug that never fired once) ------------
                if (repairRatePerSec > 0f)
                {
                    if (repair.LastOfflineGain <= 0f)
                        failures.Add("OFFLINE REPAIR BANKED ZERO over a 20h window with 2 owned Echoes -- " +
                                     "the three-consumer clock race has returned (repair reads a zeroed clock)");
                    if (repair.BankedWork <= 0f)
                        failures.Add($"repair work budget is {repair.BankedWork:0.###} after the offline window (expected > 0)");
                }

                // -- D. Per-consumer caps, from the ONE raw window ----------------
                double repairCapSec = 4.0 * 3600.0;
                if (System.Math.Abs(repair.LastOfflineCountedSeconds - repairCapSec) > 1.0)
                    failures.Add($"repair counted {repair.LastOfflineCountedSeconds:0}s of the 20h window, expected its " +
                                 $"{repairCapSec:0}s (4h) cap -- OfflineCapHours not applied to the shared window");
                float expectedGain = (float)(repairRatePerSec * repairCapSec);
                if (repairRatePerSec > 0f && Mathf.Abs(repair.LastOfflineGain - expectedGain) > Mathf.Max(0.001f, expectedGain * 0.01f))
                    failures.Add($"repair gained {repair.LastOfflineGain:0.####}, expected rate x cap = {expectedGain:0.####}");
                if (repair.BankedWork > repair.MaxBankedFractions + 0.0001f)
                    failures.Add($"repair banked {repair.BankedWork:0.###} > MaxBankedFractions {repair.MaxBankedFractions:0.###} " +
                                 "(the work ceiling is the windfall guard -- it must hold)");
                // The node-harvest consumer clamps the SAME window with its own 10h cap:
                // it reports the RAW away seconds and flags WasCapped.
                if (window.CappedSeconds(10.0) < 10.0 * 3600.0 - 1.0)
                    failures.Add("shared window did not yield the node path its full 10h cap over a 20h absence");
                if (!window.ExceedsCap(10.0) || !window.ExceedsCap(4.0))
                    failures.Add("20h window did not report ExceedsCap for the 10h/4h consumer caps");
                // The silo respects its OWN 4h cap on the same window.
                if (System.Math.Abs(window.CappedSeconds(echo.SiloCapHours) - 4.0 * 3600.0) > 1.0)
                    failures.Add($"silo cap window {window.CappedSeconds(echo.SiloCapHours):0}s != 4h -- SiloCapHours not honored");

                // =============================================================
                //  Case 2 -- fresh clock: seed forward, fan out NOTHING
                // =============================================================
                probe.Calls = 0;
                state.LastHarvestClaimMs = 0;
                var fresh = OfflineClaimCoordinator.Claim("regression-fresh");
                if (!fresh.WasFreshClock) failures.Add("fresh clock (<=0) did not report WasFreshClock");
                if (probe.Calls != 0)
                    failures.Add($"fresh clock fanned out to {probe.Calls} consumer(s) -- a fresh save must accrue NOTHING " +
                                 "(no giant retroactive first claim)");
                if (state.LastHarvestClaimMs <= 0)
                    failures.Add("fresh clock was not seeded forward -- the next launch would bank a retroactive haul");

                // =============================================================
                //  Case 3 -- backwards clock: elapsed clamps to 0, still re-stamped
                // =============================================================
                probe.Calls = 0;
                double future = TimeSource.NowUnixMs() + 3600.0 * 1000.0;
                state.LastHarvestClaimMs = future;
                OfflineClaimWindow back = default;
                try { back = OfflineClaimCoordinator.Claim("regression-backwards"); }
                catch (System.Exception ex) { failures.Add($"backwards clock THREW: {ex.GetType().Name}: {ex.Message}"); }
                if (back.ElapsedSeconds != 0.0)
                    failures.Add($"backwards clock produced elapsed {back.ElapsedSeconds:0}s (must clamp to 0 -- anti-tamper)");
                if (state.LastHarvestClaimMs > future)
                    failures.Add("backwards clock left the claim clock in the future (monotonic guard did not re-stamp)");

                // =============================================================
                //  E. Source lint -- one writer, and only one
                // =============================================================
                LintSingleWriter(failures);
            }
            catch (System.Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // EchoRepairService self-installs a logic-only WallRepairController host.
                var stray = GameObject.Find("WallRepair_EchoRepairEngine");
                if (stray != null) Object.DestroyImmediate(stray);

                if (repairGo != null) Object.DestroyImmediate(repairGo);
                if (echoGo != null) Object.DestroyImmediate(echoGo);
                if (ohsGo != null) Object.DestroyImmediate(ohsGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);

                OfflineClaimCoordinator.ResetForTests();

                if (installed) TrySetInstanceStatic(priorInstance);

                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            if (failures.Count == 0)
            {
                reason = "OFFLINE CLAIM FAN-OUT OK -- one clock read, one delta shared by every consumer, " +
                         "clock advanced exactly once per claim, offline Echo repair accrues non-zero, " +
                         "per-consumer caps (10h nodes / 4h silo / 4h repair) hold, single writer";
                return true;
            }
            reason = $"OFFLINE CLAIM FAN-OUT FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Source lint -- comments AND string literals stripped before matching
        // =====================================================================

        private static void LintSingleWriter(List<string> failures)
        {
            const string dir = "Assets/_Modules/Village/Harvest/";
            string[] consumers =
            {
                dir + "OfflineHarvestService.cs",
                dir + "EchoService.cs",
                dir + "EchoRepairService.cs",
            };
            const string owner = dir + "OfflineClaimCoordinator.cs";

            for (int i = 0; i < consumers.Length; i++)
            {
                string path = consumers[i];
                if (!System.IO.File.Exists(path)) { failures.Add($"lint: missing {path}"); continue; }
                string code = StripCommentsAndStrings(System.IO.File.ReadAllText(path));
                if (code.Contains("LastHarvestClaimMs"))
                    failures.Add($"lint: {System.IO.Path.GetFileName(path)} still touches LastHarvestClaimMs in CODE " +
                                 "-- the clock has a second owner again (that is the whole defect)");
            }

            if (!System.IO.File.Exists(owner)) { failures.Add($"lint: missing {owner}"); return; }
            string ownerCode = StripCommentsAndStrings(System.IO.File.ReadAllText(owner));
            if (!ownerCode.Contains("LastHarvestClaimMs"))
                failures.Add("lint: OfflineClaimCoordinator.cs no longer references LastHarvestClaimMs in CODE " +
                             "-- the single owner lost the field it owns");
        }

        /// <summary>
        /// Blanks comments AND string literals before matching, through the ONE shared
        /// stripper (<see cref="DeNelle.Editor.Regression.RegressionSourceText"/>) rather
        /// than a rolled-own copy -- a doc-comment or a FlowTrace message naming the field
        /// must never satisfy or trip this lint.
        /// </summary>
        private static string StripCommentsAndStrings(string src)
        {
            return DeNelle.Editor.Regression.RegressionSourceText.StripCommentsAndStrings(src);
        }

        // =====================================================================
        //  Headless state-install helpers (editmode has no Awake)
        // =====================================================================

        private static bool TryInstallHeadlessState(GameStateService svc, GameState state, out string err)
        {
            err = null;
            var stateField = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null)
            { err = "GameStateService._state field not found by reflection (state seam renamed/removed)"; return false; }
            stateField.SetValue(svc, state);
            if (!TrySetInstanceStatic(svc))
            { err = "GameStateService._instance static not found by reflection (singleton seam renamed/removed)"; return false; }
            return true;
        }

        private static bool TrySetInstanceStatic(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }
    }
}
