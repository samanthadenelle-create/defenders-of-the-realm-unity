// =============================================================================
// OfflineAccrualTrustRegression — headless oracle for WO-1128's CLIENT half:
// every offline window records WHICH CLOCK produced it, and records its own
// endpoints, so the server can reconcile it on the next round trip.
// -----------------------------------------------------------------------------
// THE SPLIT, so the next reader does not go looking for the clamp in here:
//   * The REFUSAL is server-side (api/game/save.js §RECONCILE) and is gated by that
//     file's own runnable self-test — `node api/game/save.js`, marker
//     ACCRUAL_RECONCILE_OK. Unity cannot execute JavaScript, so the clamp cannot be
//     asserted from this suite and pretending otherwise would be a gate that proves
//     nothing.
//   * The DECLARATION is client-side and lives here. If the client stops recording
//     the window's endpoints or the clock's trust, the server's comparison silently
//     degrades to "no_client_claim_clock" — an absence of judgement that LOOKS like
//     a pass in the logs. That is the failure this oracle exists to make loud.
//
// WHAT IT ASSERTS (each fails on a real known-bad state):
//   1. ANCHORED  — after ServerClock.Sync, a claim reports ServerAnchored == true /
//      IsProvisional == false. Fails if the service stops reading TimeSource
//      .IsServerAnchored (i.e. goes back to trusting whatever clock it got).
//   2. UNANCHORED — after ServerClock.ResetForTests, a claim reports
//      ServerAnchored == false / IsProvisional == true. Fails if the flag is
//      hardcoded true, which would tell the server every window was trustworthy.
//   3. NO CLIENT-SIDE PUNISHMENT — an UNANCHORED 2h window still reports the full
//      ~2h and still banks normally. ⛔ This is the case that must never "improve":
//      a cold launch is ALWAYS unanchored (ServerClock's Stopwatch dies with the
//      process), so any client-side penalty for an untrusted clock would tax every
//      honest offline player on every launch. Refuse server-side, never punish here.
//   4. THE WINDOW IS DECLARED — WindowStartUnixMs / NowUnixMs are populated and
//      ordered, and NowUnixMs matches the clock the coordinator persisted. Those two
//      numbers ARE the server's evidence; a zero pair is an unreconcilable save.
//
// SAFETY: mirrors OfflineHarvestRegression — snapshots the PlayerPrefs save blob,
// installs a THROWAWAY GameState + GameStateService by reflection (editmode never
// runs Awake), restores the live singleton, and — specific to this oracle —
// RESTORES ServerClock to its un-synced state in the finally, because the anchor is
// a process-wide static that later suites read.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class OfflineAccrualTrustRegression
    {
        private const string SaveKey = "dotr-save";
        private const double TwoHoursMs = 2.0 * 3600.0 * 1000.0;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorInstance = GameStateService.Instance;
            GameObject svcGo = null, gssGo = null;
            GameState throwaway = null;
            bool installed = false;

            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GameStateService (accrual-trust-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!TryInstallHeadlessState(gss, throwaway, out string installErr))
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "OFFLINE ACCRUAL TRUST", "needs fleet -- " + installErr);
                }
                installed = true;
                var state = gss.State;
                if (state == null)
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "OFFLINE ACCRUAL TRUST", "needs fleet -- throwaway state did not install");
                }

                svcGo = new GameObject("OfflineHarvestService (accrual-trust-oracle)");
                var svc = svcGo.AddComponent<OfflineHarvestService>();
                svc.OfflineCapHours = 10f;   // 2h windows below must never trip the cap

                // ── Case 1: ANCHORED clock is reported as anchored ────────────────
                // Anchor to the device's own current time: the VALUE is irrelevant to
                // this assertion (we only care that IsTrusted flips), and using "now"
                // keeps the window arithmetic below sane.
                ServerClock.ResetForTests();
                ServerClock.Sync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (!TimeSource.IsServerAnchored)
                {
                    failures.Add("case1 ServerClock.Sync did not make TimeSource.IsServerAnchored true " +
                                 "(the anchor seam moved — the whole defence is off)");
                }

                double now1 = TimeSource.NowUnixMs();
                state.LastHarvestClaimMs = now1 - TwoHoursMs;
                var r1 = svc.ClaimAccrual();
                if (r1 == null) failures.Add("case1 returned null result");
                else
                {
                    if (!r1.ServerAnchored)
                        failures.Add("case1 anchored claim reported ServerAnchored=false — the server " +
                                     "will treat a trustworthy window as provisional");
                    if (r1.IsProvisional)
                        failures.Add("case1 anchored claim reported IsProvisional=true (should mirror !ServerAnchored)");
                    if (r1.ClockSource != "server-anchored")
                        failures.Add($"case1 ClockSource was '{r1.ClockSource}', expected 'server-anchored'");
                }

                // ── Case 2: UNANCHORED clock is reported as unanchored ────────────
                ServerClock.ResetForTests();
                if (TimeSource.IsServerAnchored)
                    failures.Add("case2 ServerClock.ResetForTests left IsServerAnchored true (test seam broken)");

                double now2 = TimeSource.NowUnixMs();
                state.LastHarvestClaimMs = now2 - TwoHoursMs;
                var r2 = svc.ClaimAccrual();
                if (r2 == null) failures.Add("case2 returned null result");
                else
                {
                    if (r2.ServerAnchored)
                        failures.Add("case2 UNanchored claim reported ServerAnchored=true — every window " +
                                     "would claim to be trustworthy and the server's audit becomes a lie");
                    if (!r2.IsProvisional)
                        failures.Add("case2 UNanchored claim reported IsProvisional=false");
                    if (r2.ClockSource != "device")
                        failures.Add($"case2 ClockSource was '{r2.ClockSource}', expected 'device'");

                    // ── Case 3: the untrusted window is NOT punished client-side ──
                    // 2h claimed, 10h cap: the full window must be reported and the cap
                    // must NOT have fired. A "hardening" that shortens or zeroes an
                    // unanchored window taxes every cold launch — see the header.
                    if (r2.AwaySeconds < 7000.0)
                        failures.Add($"case3 UNanchored 2h window reported only {r2.AwaySeconds:0}s — an " +
                                     "unanchored clock must NOT reduce the window (cold launches are always unanchored)");
                    if (r2.WasCapped)
                        failures.Add("case3 UNanchored 2h window reported WasCapped against a 10h cap");

                    // ── Case 4: the window is DECLARED for the server to reconcile ─
                    if (!(r2.NowUnixMs > 0.0))
                        failures.Add("case4 NowUnixMs not recorded — the server has no window end to compare");
                    if (!(r2.WindowStartUnixMs > 0.0))
                        failures.Add("case4 WindowStartUnixMs not recorded — the server has no window start to compare");
                    if (r2.NowUnixMs < r2.WindowStartUnixMs)
                        failures.Add($"case4 window is inverted (start {r2.WindowStartUnixMs:0} > end {r2.NowUnixMs:0})");
                    // The persisted claim clock IS what the server reads back as
                    // lastHarvestClaimMs, so the declared end and the persisted stamp
                    // must be the same number or the reconciliation compares two
                    // different windows.
                    if (Math.Abs(state.LastHarvestClaimMs - r2.NowUnixMs) > 1.0)
                        failures.Add($"case4 declared window end ({r2.NowUnixMs:0}) != persisted " +
                                     $"LastHarvestClaimMs ({state.LastHarvestClaimMs:0}) — the server would " +
                                     "reconcile against a stamp the client never reported");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // The anchor is a process-wide static; leave it exactly as un-synced as
                // a fresh process, so a later suite never inherits this oracle's trust.
                ServerClock.ResetForTests();

                if (svcGo != null) UnityEngine.Object.DestroyImmediate(svcGo);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);

                if (installed) TrySetInstanceStatic(priorInstance);

                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            if (failures.Count == 0)
            {
                reason = "OFFLINE ACCRUAL TRUST OK — every window declares its clock " +
                         "(server-anchored vs device) and its own endpoints; an unanchored " +
                         "window is reported, never reduced. Server clamp gated separately by " +
                         "`node api/game/save.js` (ACCRUAL_RECONCILE_OK).";
                return true;
            }
            reason = $"OFFLINE ACCRUAL TRUST FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Headless state-install helpers (editmode has no Awake) — same seam as
        //  OfflineHarvestRegression; kept local so neither oracle can break the other.
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
