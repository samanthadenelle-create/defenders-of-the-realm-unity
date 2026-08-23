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
//   5-8. THE SERVER'S ANSWER IS READ (WO-1128 s3.3). load.js's serverLastSeenMs
//      parses; save.js's `accrual` clamp block parses and LOWERS the local balance
//      to the figure the server stored; applying it does NOT move
//      GameState.LastHarvestClaimMs (three legal writers, all in
//      OfflineClaimCoordinator -- and a rolled-back stamp would make the refused
//      stretch RE-CLAIMABLE next launch); and the adjustment can only ever
//      SUBTRACT, so neither a spent-down balance nor a forged response is a grant
//      path. Both wire bodies are typed out verbatim in the cases, so a key rename
//      on either side fails here instead of quietly parsing to nulls.
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

                // ── Cases 5-8: the server's ANSWER is parsed and applied (WO-1128 §3.3) ──
                // Before these existed, load.js's serverLastSeenMs and save.js's `accrual`
                // clamp block were both emitted and read by NOTHING: the server refused a
                // fabricated gain, stored the reduced figure, and the device went on showing
                // (and re-posting) the number it had been refused. Each case below fails on a
                // real regression of that half.
                RunReconcileCases(gss, state, failures);
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
                         "window is reported, never reduced; and the server's answer is READ — " +
                         "serverLastSeenMs parses, a reported clamp lowers the balance to the " +
                         "server's figure, the claim clock is untouched, and the adjustment only " +
                         "ever subtracts. Server-side clamp itself gated separately by " +
                         "`node api/game/save.js` (ACCRUAL_RECONCILE_OK).";
                return true;
            }
            reason = $"OFFLINE ACCRUAL TRUST FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  WO-1128 §3.3 — the client half of the reconciliation
        // =====================================================================

        /// <summary>
        /// Feeds REAL wire bodies (the exact shapes api/game/load.js and api/game/save.js
        /// emit) through the private response readers and asserts what lands on the state.
        /// Reflection is used because both readers are private by design — the seam being
        /// pinned is the WIRE CONTRACT, not the method's visibility.
        /// </summary>
        private static void RunReconcileCases(GameStateService gss, GameState state, List<string> failures)
        {
            // ── Case 5: load.js's serverLastSeenMs actually PARSES ────────────────
            // The field has been on the wire since WO-1128 and BackendLoadResponse
            // ignored it. A rename on either side silently returns us to that.
            var loadType = typeof(GameStateService).GetNestedType("BackendLoadResponse",
                BindingFlags.NonPublic);
            if (loadType == null)
            {
                failures.Add("case5 GameStateService.BackendLoadResponse not found by reflection " +
                             "(load response type renamed/removed)");
            }
            else
            {
                const string loadJson = "{\"ok\":true,\"success\":true,\"serverNowMs\":1700000000000," +
                                        "\"serverLastSeenMs\":1699999000000,\"data\":null}";
                object parsed = Newtonsoft.Json.JsonConvert.DeserializeObject(
                    loadJson, loadType, SaveSchema.JsonSettings);
                var prop = loadType.GetProperty("ServerLastSeenMs");
                if (prop == null)
                    failures.Add("case5 BackendLoadResponse has no ServerLastSeenMs member — load.js sends " +
                                 "serverLastSeenMs and the client would discard the server's own anchor again");
                else
                {
                    var got = prop.GetValue(parsed) as double?;
                    if (got == null || Math.Abs(got.Value - 1699999000000.0) > 1.0)
                        failures.Add($"case5 serverLastSeenMs parsed as '{got}', expected 1699999000000 " +
                                     "(JsonProperty name drifted from the wire)");
                }
            }

            var reader = typeof(GameStateService).GetMethod("ReadSaveResponse",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (reader == null)
            {
                failures.Add("case6 GameStateService.ReadSaveResponse not found by reflection — the save " +
                             "response reader is gone, so every accrual clamp is discarded unread again");
                return;
            }

            // The body save.js returns when it clamped wood: claimed 1000, allowed 400,
            // prior 300. Written out verbatim rather than built from the C# type, so a
            // key rename on either side FAILS here instead of silently parsing to nulls.
            const string clampJson =
                "{\"ok\":true,\"success\":true,\"serverNowMs\":1700000000000,\"accrual\":{" +
                "\"reconciled\":true,\"reason\":\"clamped_to_server_window\"," +
                "\"clientWindowSec\":36000,\"serverElapsedSec\":600,\"honestFraction\":0.0333," +
                "\"clamps\":[{\"field\":\"wood\",\"claimed\":1000,\"allowed\":400,\"prior\":300," +
                "\"claimedGain\":700,\"allowedGain\":100}],\"observed\":{}}}";

            double clockBefore = state.LastHarvestClaimMs;

            // ── Case 6: an over-claim is LOWERED to the server's figure ───────────
            state.Wood = 1000;
            reader.Invoke(gss, new object[] { clampJson });
            if (state.Wood != 400)
                failures.Add($"case6 clamped wood landed at {state.Wood}, expected 400 — the server refused " +
                             "600 of the claimed gain and the device kept it (and would re-post it next save)");

            // ── Case 7: ⛔ THE CLOCK IS NOT A CLAMP TARGET ────────────────────────
            // LastHarvestClaimMs has three legal writers, all in OfflineClaimCoordinator.
            // A fourth here would ALSO make the difference re-claimable next launch — the
            // double-grant save.js refuses server-side for exactly the same reason.
            if (Math.Abs(state.LastHarvestClaimMs - clockBefore) > 0.5)
                failures.Add($"case7 applying an accrual clamp MOVED LastHarvestClaimMs " +
                             $"({clockBefore:0} -> {state.LastHarvestClaimMs:0}) — the sync path must adjust " +
                             "BALANCES ONLY; the coordinator is the single owner of that clock");

            // ── Case 8: it can only ever SUBTRACT ────────────────────────────────
            // A player who spent down to BELOW the server's prior must not be topped back
            // up by a "clamp". Same arm protects against a forged/stale accrual block
            // being turned into a grant path.
            state.Wood = 50;
            reader.Invoke(gss, new object[] { clampJson });
            if (state.Wood != 50)
                failures.Add($"case8 a clamp RAISED wood to {state.Wood} from 50 — reconciliation is a " +
                             "subtraction, never a grant");

            // And a gain earned AFTER the snapshot survives the subtraction intact.
            state.Wood = 1500;                      // 1000 posted + 500 earned since
            reader.Invoke(gss, new object[] { clampJson });
            if (state.Wood != 900)
                failures.Add($"case8 post-snapshot earnings were not preserved: wood landed at {state.Wood}, " +
                             "expected 900 (1500 minus the 600 the server refused)");
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
