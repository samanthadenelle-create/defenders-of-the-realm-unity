// =============================================================================
// OfflineHarvestRegression — headless oracle for the WO-115 offline-accrual clock:
// cap clamp, backwards-clock (anti-tamper) monotonic guard, and advance-even-on-zero
// (the anti-double-grant contract).
// -----------------------------------------------------------------------------
// Drives the REAL OfflineHarvestService.ClaimAccrual() against a live GameStateService
// with a CONTROLLED GameState.LastHarvestClaimMs, asserting from data (no sources are
// present in a headless editor, so every haul is 0 — exactly what isolates the CLOCK
// logic from the accrual sources):
//   1. FRESH SAVE (LastHarvestClaimMs<=0) → seeds the clock to now, banks nothing (None),
//      and advances the clock forward (no giant retroactive first-claim).
//   2. LONG ABSENCE (20h with a 10h cap) → result.WasCapped == true, AwaySeconds >= cap,
//      Total == 0 (no sources), and the clock advanced to ~now (window never re-claimable).
//   3. BACKWARDS CLOCK (last set to the FUTURE) → elapsed clamps to 0 (no throw, no
//      negative haul) and the clock is re-stamped to now (monotonic anti-tamper guard).
//
// SAFETY: snapshots the raw PlayerPrefs save blob + restores it (svc.Load()) in a finally,
// and DestroyImmediate's the throwaway service, so the real save/state is untouched.
// Mirrors MonetizationCovenantRegression: public static bool Run(out string reason).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class OfflineHarvestRegression
    {
        private const string SaveKey = "dotr-save";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // Snapshot the persisted save so nothing we mutate here survives the oracle.
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameObject svcGo = null;
            bool createdGss = false;
            try
            {
                var gss = GameStateService.Instance;
                if (gss == null)
                {
                    var go = new GameObject("GameStateService (harvest-oracle)");
                    go.AddComponent<GameStateService>();   // Awake sets Instance + loads save
                    gss = GameStateService.Instance;
                    createdGss = true;
                }
                if (gss == null || gss.State == null)
                { reason = "OFFLINE HARVEST FAIL: no GameStateService/State available"; return false; }
                var state = gss.State;

                svcGo = new GameObject("OfflineHarvestService (oracle)");
                var svc = svcGo.AddComponent<OfflineHarvestService>();   // Awake sets Instance; Start (coroutine) does NOT run in edit mode
                svc.OfflineCapHours = 10f;   // deterministic cap for case 2

                double capSeconds = 10.0 * 3600.0;

                // --- Case 1: fresh save (clock 0) seeds forward, banks nothing ---
                state.LastHarvestClaimMs = 0;
                double before1 = state.LastHarvestClaimMs;
                var r1 = svc.ClaimAccrual();
                if (r1 == null) failures.Add("case1 fresh save returned null result");
                else if (r1.Total != 0) failures.Add($"case1 fresh save banked {r1.Total} (should seed clock + bank nothing)");
                if (state.LastHarvestClaimMs <= before1)
                    failures.Add($"case1 fresh save did NOT advance the clock (still {state.LastHarvestClaimMs})");

                // --- Case 2: 20h absence, 10h cap → WasCapped, Total 0, clock advanced ---
                double now2 = DeNelle.Village.TimeSource.NowUnixMs();
                double away20hMs = 20.0 * 3600.0 * 1000.0;
                state.LastHarvestClaimMs = now2 - away20hMs;
                double before2 = state.LastHarvestClaimMs;
                var r2 = svc.ClaimAccrual();
                if (r2 == null) { failures.Add("case2 returned null result"); }
                else
                {
                    if (!r2.WasCapped) failures.Add($"case2 20h absence vs 10h cap: WasCapped should be true (AwaySeconds={r2.AwaySeconds:0})");
                    if (r2.AwaySeconds < capSeconds) failures.Add($"case2 AwaySeconds {r2.AwaySeconds:0} < cap {capSeconds:0} (elapsed mis-measured)");
                    if (r2.Total != 0) failures.Add($"case2 banked {r2.Total} with NO sources present (phantom haul)");
                }
                if (state.LastHarvestClaimMs <= before2)
                    failures.Add("case2 did not advance the claim clock (window would be re-claimable → double-grant)");

                // --- Case 3: backwards clock (future) → clamp to 0, no throw, re-stamp now ---
                double now3 = DeNelle.Village.TimeSource.NowUnixMs();
                double future = now3 + 3600.0 * 1000.0;   // 1h in the future
                state.LastHarvestClaimMs = future;
                OfflineHarvestResult r3 = null;
                try { r3 = svc.ClaimAccrual(); }
                catch (System.Exception ex) { failures.Add($"case3 backwards clock THREW: {ex.GetType().Name}: {ex.Message}"); }
                if (r3 != null)
                {
                    if (r3.Total != 0) failures.Add($"case3 backwards clock banked {r3.Total} (negative-delta not clamped)");
                    if (r3.WasCapped) failures.Add("case3 backwards clock reported WasCapped (elapsed should clamp to 0, not cap)");
                }
                if (state.LastHarvestClaimMs > future)
                    failures.Add($"case3 clock left in the future ({state.LastHarvestClaimMs}) — monotonic guard did not re-stamp to now");
            }
            catch (System.Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (svcGo != null) Object.DestroyImmediate(svcGo);

                // Restore the persisted save blob, then reload live state from it so the
                // in-memory GameState the batch's later oracles read is unchanged too.
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
                var gss = GameStateService.Instance;
                if (gss != null && !createdGss) gss.Load();
            }

            if (failures.Count == 0)
            {
                reason = "OFFLINE HARVEST OK — fresh-seed + 10h cap clamp + backwards-clock guard hold; " +
                         "clock always advances (no re-claimable window)";
                return true;
            }
            reason = $"OFFLINE HARVEST FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }
    }
}
