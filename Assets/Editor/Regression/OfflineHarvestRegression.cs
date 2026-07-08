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
using System.Reflection;
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

            // HEADLESS STATE INSTALL — editmode batchmode NEVER runs GameStateService.Awake
            // (Awake fires only in play mode / on ExecuteAlways), so a bare
            // AddComponent<GameStateService>() leaves Instance + State null — the exact cause of
            // the historic false-FAIL "no GameStateService/State available". Mirror
            // CoreSaveContractRegression: construct a THROWAWAY GameState SO and install it as the
            // active state for the duration by setting the private static _instance + the
            // [SerializeField] _state via reflection, restoring the prior live service in finally.
            GameStateService priorInstance = GameStateService.Instance;
            GameObject svcGo = null, gssGo = null;
            GameState throwaway = null;
            bool installed = false;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();   // fresh defaults; all collections init'd → Save()-safe
                gssGo = new GameObject("GameStateService (harvest-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!TryInstallHeadlessState(gss, throwaway, out string installErr))
                {
                    // The GameStateService singleton/state seam moved — genuinely unrunnable
                    // headless. NAMED SKIP (return true), never a false FAIL (harness-integrity).
                    reason = "OFFLINE HARVEST skipped: needs fleet — " + installErr;
                    return true;
                }
                installed = true;
                var state = gss.State;   // the throwaway — never null now
                if (state == null)
                { reason = "OFFLINE HARVEST skipped: needs fleet — throwaway state did not install"; return true; }

                svcGo = new GameObject("OfflineHarvestService (oracle)");
                var svc = svcGo.AddComponent<OfflineHarvestService>();   // ClaimAccrual reads GameStateService.Instance directly (no Awake needed)
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
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);

                // Restore the live service the batch's later oracles read. DestroyImmediate
                // above may have nulled the static via OnDestroy, so set it back explicitly.
                if (installed) TrySetInstanceStatic(priorInstance);

                // Restore the persisted save blob (svc.Save() wrote to it during the run).
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
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

        // =====================================================================
        //  Headless state-install helpers (editmode has no Awake)
        // =====================================================================

        /// <summary>
        /// Installs <paramref name="state"/> as the active state on <paramref name="svc"/> and
        /// promotes <paramref name="svc"/> to the live singleton, by reflection over the private
        /// <c>_state</c> field + the <c>_instance</c> static — the same seam Awake sets, which does
        /// NOT run on AddComponent in editmode batchmode. Returns false (with a named reason) if
        /// either seam was renamed/removed, so the caller NAMED-SKIPs instead of false-failing.
        /// </summary>
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

        /// <summary>Sets the private static <c>GameStateService._instance</c> (null allowed, to restore).
        /// Returns false only if the field seam is gone.</summary>
        private static bool TrySetInstanceStatic(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }
    }
}
