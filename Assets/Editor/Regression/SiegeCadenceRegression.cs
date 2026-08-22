// =============================================================================
// SiegeCadenceRegression — [siege-cadence] (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Registered ONCE in DataRegression.RunAll.
//
// Drives SiegeScheduler against a CONTROLLED clock with no scene. What it pins:
//
//  1. A FRESH cadence clock (<= 0) SEEDS FORWARD and banks NOTHING. Without this a new
//     save's clock of 0 reads as 1970 — an effectively infinite away window — and the
//     player's first act in the game is a retroactive assault.
//
//  2. A long absence CLAMPS to _maxPendingSieges. Coming home to five queued assaults is
//     a punishment for playing, not a consequence.
//
//  3. A BACKWARDS clock (device clock moved, or a save restored from the future) banks
//     nothing, does not throw, and RE-STAMPS — so the cadence is monotonic and can never
//     be stalled forever. (The OfflineHarvestRegression case-3 precedent.)
//
//  4. ⛔ THE WO-1147 INVARIANT: ApplyOfflineWindow NEVER touches GameState
//     .LastHarvestClaimMs. The OfflineClaimCoordinator owns that clock, and its own
//     interface doc says consumers must not write it. When three systems shared it, the
//     frame-order coin-flip meant offline Echo repair never accrued ONCE. Snapshot before,
//     assert after.
//
//  5. WITH ff.siege OFF the scheduler arms NOTHING — no session is opened. The feature is
//     default-OFF because the loss stakes are UNRULED, and "default off" has to be a
//     PROVEN property, not a hopeful one.
// =============================================================================

using System.Collections.Generic;
using System.Reflection;
using DeNelle.Core.State;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Cadence + offline-pressure oracle for the WO-1026 siege scheduler.</summary>
    public static class SiegeCadenceRegression
    {
        private const string SiegeFlagPref = "ff.siege";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            var prior = GameStateService.Instance;
            string rawSave = HeadlessState.SnapshotSave(out bool hadSave);
            bool hadFlag = PlayerPrefs.HasKey(SiegeFlagPref);
            int rawFlag = hadFlag ? PlayerPrefs.GetInt(SiegeFlagPref, -1) : -1;

            GameObject gssGo = null, schedGo = null;
            GameState throwaway = null;
            bool installed = false;

            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GameStateService (siege-cadence-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!HeadlessState.TryInstall(gss, throwaway, out string installErr))
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "SIEGE CADENCE", "needs fleet -- " + installErr);
                installed = true;
                var state = gss.State;

                schedGo = new GameObject("SiegeScheduler (oracle)");
                var sched = schedGo.AddComponent<SiegeScheduler>();

                // The cadence knobs are [SerializeField] private (config, not save state).
                // Pin them by reflection so the cases are deterministic; NAMED-SKIP if a knob
                // was renamed rather than false-failing the gate.
                if (!SetPrivate(sched, "_siegeIntervalHours", 6f, out string knobErr)
                    || !SetPrivate(sched, "_maxPendingSieges", 1, out knobErr)
                    || !SetPrivate(sched, "_offlineCapHours", 24f, out knobErr))
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "SIEGE CADENCE", "needs fleet -- " + knobErr);

                double intervalSec = 6.0 * 3600.0;
                double now = TimeSource.NowUnixMs();

                // ── Case 1: fresh clock seeds forward, banks nothing ──────────────
                state.LastSiegeUnixMs = 0;
                sched.ApplyOfflineWindow(Window(1, "oracle-fresh", now, now, 0, true));
                if (sched.PendingSieges != 0)
                    failures.Add($"case1 fresh clock banked {sched.PendingSieges} sieges (a new save must not be raided retroactively)");
                if (state.LastSiegeUnixMs <= 0)
                    failures.Add("case1 fresh clock was NOT seeded forward (it would re-read as 1970 every load)");

                // ── Case 2: 3x interval away, maxPending 1 -> clamps to 1 ─────────
                ResetPending(sched);
                state.LastSiegeUnixMs = now - (intervalSec * 3.0 * 1000.0);
                sched.ApplyOfflineWindow(Window(2, "oracle-3x", now,
                    state.LastSiegeUnixMs, intervalSec * 3.0, false));
                if (sched.PendingSieges != 1)
                    failures.Add($"case2 3x interval banked {sched.PendingSieges}, expected the _maxPendingSieges clamp of 1");

                // ── Case 3: backwards clock -> 0 pressure, no throw, re-stamped ───
                ResetPending(sched);
                double future = now + 3600.0 * 1000.0;
                state.LastSiegeUnixMs = future;
                try
                {
                    sched.ApplyOfflineWindow(Window(3, "oracle-backwards", now, future, 0, false));
                }
                catch (System.Exception ex)
                {
                    failures.Add($"case3 backwards clock THREW: {ex.GetType().Name}: {ex.Message}");
                }
                if (sched.PendingSieges != 0)
                    failures.Add($"case3 backwards clock banked {sched.PendingSieges} (a moved device clock must not mint sieges)");
                if (state.LastSiegeUnixMs > future)
                    failures.Add("case3 cadence clock left beyond the future stamp -- the monotonic guard did not re-stamp");

                // ── Case 4: ⛔ the WO-1147 invariant ──────────────────────────────
                ResetPending(sched);
                state.LastHarvestClaimMs = 1234567890.0;
                double harvestBefore = state.LastHarvestClaimMs;
                state.LastSiegeUnixMs = now - (intervalSec * 2.0 * 1000.0);
                sched.ApplyOfflineWindow(Window(4, "oracle-invariant", now,
                    state.LastSiegeUnixMs, intervalSec * 2.0, false));
                if (state.LastHarvestClaimMs != harvestBefore)
                    failures.Add($"case4 THE SIEGE SCHEDULER WROTE GameState.LastHarvestClaimMs " +
                                 $"({harvestBefore} -> {state.LastHarvestClaimMs}). The OfflineClaimCoordinator " +
                                 "owns that clock (IOfflineClaimConsumer says so). A second writer is the " +
                                 "WO-1147 bug -- offline Echo repair never accrued once.");

                // ── Case 5: ff.siege OFF arms nothing ────────────────────────────
                PlayerPrefs.SetInt(SiegeFlagPref, 0);
                ResetPending(sched);
                state.Onboarded = true;
                state.LastSiegeUnixMs = now - (intervalSec * 5.0 * 1000.0);   // long overdue
                SiegeSession.Abandon("oracle reset");
                try { sched.Evaluate(); }
                catch (System.Exception ex)
                { failures.Add($"case5 Evaluate() THREW with the flag off: {ex.GetType().Name}: {ex.Message}"); }
                if (SiegeSession.Current != null)
                {
                    failures.Add("case5 a siege session was OPENED with ff.siege OFF -- the loop must be " +
                                 "byte-identical to pre-WO-1026 until the owner rules the loss stakes");
                    SiegeSession.Abandon("oracle cleanup");
                }

                // ── Case 6: ForceSiegeNow is REFUSED while the flag is off ───────
                if (sched.ForceSiegeNow())
                {
                    failures.Add("case6 ForceSiegeNow succeeded with ff.siege OFF (the dev entry point " +
                                 "skips the CADENCE, never the safety gates)");
                    SiegeSession.Abandon("oracle cleanup");
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (schedGo != null) Object.DestroyImmediate(schedGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                if (installed) HeadlessState.TrySetInstance(prior);
                HeadlessState.RestoreSave(hadSave, rawSave);
                if (hadFlag) PlayerPrefs.SetInt(SiegeFlagPref, rawFlag);
                else PlayerPrefs.DeleteKey(SiegeFlagPref);
                PlayerPrefs.Save();
                SiegeSession.Abandon("oracle teardown");
            }

            if (failures.Count == 0)
            {
                reason = "SIEGE CADENCE OK -- fresh clock seeds (no retroactive siege); away time clamps to " +
                         "_maxPendingSieges; backwards clock re-stamps without minting; LastHarvestClaimMs is " +
                         "NEVER written (WO-1147 invariant); ff.siege OFF arms nothing";
                return true;
            }
            reason = $"SIEGE CADENCE FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        private static OfflineClaimWindow Window(int seq, string why, double now,
            double start, double elapsedSec, bool fresh)
            => new OfflineClaimWindow(seq, why, now, start, elapsedSec, fresh);

        /// <summary>Zeroes PendingSieges between cases. It is an auto-property with a PRIVATE
        /// setter, so the compiler-generated backing field is the only reachable seam — a public
        /// setter would be a production API existing purely for a test, which is worse.
        /// If the property is ever renamed this quietly no-ops and case 2 fails loudly, which is
        /// the correct failure direction (a rename should surface, not silently pass).</summary>
        private static void ResetPending(SiegeScheduler s)
        {
            var backing = typeof(SiegeScheduler).GetField("<PendingSieges>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (backing != null) backing.SetValue(s, 0);
        }

        private static bool SetPrivate(object target, string field, object value, out string err)
        {
            err = null;
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) { err = $"SiegeScheduler.{field} not found by reflection (cadence knob renamed)"; return false; }
            f.SetValue(target, value);
            return true;
        }
    }
}
