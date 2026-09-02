// =============================================================================
// PiLoginGateRegression [pi-login-gate] -- a player who is SIGNED IN WITH PI must
// never be shown the CHOOSE YOUR WALLET surface, and the SKR/Solana skin must be
// byte-for-byte unchanged by that fix.
// -----------------------------------------------------------------------------
// THE DEFECT (WO-1322, owner-captured in REAL Pi Browser, 2026-09-02,
// session wt-1454dc8bfaa4, reproduced in wt-ea6bc0d7b98f and wt-2129a836dcdd):
//   [Flow:Skin] Pi Browser host detected - resolving the Pi skin.
//   [Flow:Skin] Currency skin resolved: 'pi' (auth=PiSdk, symbol=pi, identity=PiUid).
//   [Flow:Pi]   Signed in as <redacted> (uid bound to session).
//   ... and the game presented CHOOSE YOUR WALLET / "Your wallet is your save" anyway.
// LoginPanelController.PresentOrContinue sampled exactly TWO inputs (the live wallet
// connection and the attested wallet-bound save) and hardcoded the third to false.
// PiSignInController was referenced NOWHERE in the file. The gate was SKIN-BLIND: under
// SkinAuthMode.PiSdk the WALLET IS NOT THE IDENTITY -- PiSignInController.SignedInUid is.
//
// What this oracle pins (the three rows WO-1322 acceptance names, all explicit):
//   (a) PI SKIN + PI SIGNED IN  -> CONTINUE. The captured case. Driven through the pure
//       four-input seam ShouldContinueWithoutLogin(bool,bool,bool,bool) by reflection.
//   (b) PI SKIN + NOT SIGNED IN -> unchanged: the two wallet inputs still decide, so a
//       fresh Pi player with nothing bound still PRESENTS and can still play as guest.
//   (c) SKR / SOLANA SKIN       -> BYTE-FOR-BYTE UNCHANGED. This is the regression risk,
//       so it is asserted directly and loudly: the whole three-input WO-1249 truth table
//       is re-driven with the Pi input pinned FALSE and must match the three-input
//       overload exactly, row for row. A first run with everything false still PRESENTS.
//   (d) The Pi input is a SEPARATE NAMED PARAMETER, not smuggled through legacySignedIn
//       (WO-1322 "Note for whoever implements": hiding a live identity source behind a
//       parameter documented as permanently dead is how the next reader deletes it).
//   (e) SOURCE PINS on LoginPanelController.cs: it samples PiSignInController.IsSignedIn,
//       AND-s it with SkinAuthMode.PiSdk (the clause that protects row (c)), reports the
//       Pi input in the decision trace, introduces NO await/network call into
//       PresentOrContinue (WO-837-B: "the worst softlock site on the whole surface"),
//       never logs a Pi uid or username, and still leaves Play as Guest un-disabled.
//
// LoginPanelController lives in DeNelle.Onboarding, which this asmdef does not
// reference; the seam is driven by reflection, exactly like LoginGateRegression.
// This suite is ADDITIVE to [login-gate] -- that one still owns the wallet truth table,
// the attestation input and the WO-1249 production-boot pins. It is not duplicated here.
//
// Marker: PI_LOGIN_GATE_OK / PI_LOGIN_GATE_FAIL. Expected: GREEN.
// Wire (DataRegression.RunAll): [pi-login-gate].
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class PiLoginGateRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- PI LOGIN GATE (WO-1322: a signed-in Pi player must not meet the wallet wall) ---");

            var lpcType = FindType("DeNelle.Onboarding.LoginPanelController");
            if (lpcType == null)
            {
                failures.Add("[pi-login-gate] LoginPanelController type not found (DeNelle.Onboarding not compiled?)");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            var decide4 = lpcType.GetMethod("ShouldContinueWithoutLogin",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) }, null);
            var decide3 = lpcType.GetMethod("ShouldContinueWithoutLogin",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(bool), typeof(bool), typeof(bool) }, null);

            if (decide4 == null)
            {
                failures.Add("[pi-login-gate] LoginPanelController.ShouldContinueWithoutLogin(bool,bool,bool,bool) " +
                             "is gone -- the Pi identity input no longer has a testable seam, and the WO-1322 " +
                             "defect (Pi signed in, wallet wall shown anyway) is unguarded");
            }
            if (decide3 == null)
            {
                failures.Add("[pi-login-gate] the three-input ShouldContinueWithoutLogin overload is gone -- " +
                             "the SKR/Solana truth table WO-1249 pinned can no longer be compared against, so " +
                             "row (c) 'byte-for-byte unchanged' is unprovable");
            }
            if (decide4 == null || decide3 == null)
            {
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            // -- (d) the Pi input has its OWN NAMED PARAMETER --------------------
            CheckParameterNaming(decide4, failures, log);

            // -- (a) + (b) the Pi rows ------------------------------------------
            // (walletConnected, walletIdentityBound, legacySignedIn, piIdentitySignedIn) -> want
            var piRows = new (bool wc, bool wb, bool legacy, bool pi, bool want, string what)[]
            {
                // (a) THE CAPTURED CASE: Pi Browser, signed in, no Solana wallet anywhere.
                (false, false, false, true,  true,
                    "(a) PI SKIN + PI SIGNED IN, no wallet at all -- the owner's 2026-09-02 capture"),
                (true,  false, false, true,  true,  "(a) Pi signed in AND a wallet connected"),
                (false, true,  false, true,  true,  "(a) Pi signed in AND a wallet-bound save"),
                // (b) Pi skin, NOT signed in: the wallet inputs still decide, unchanged.
                (false, false, false, false, false,
                    "(b) PI SKIN + NOT signed in, nothing bound -- must still PRESENT (guest stays the escape)"),
                (true,  false, false, false, true,
                    "(b) PI SKIN + NOT signed in but a wallet IS connected -- the wallet inputs still decide"),
                (false, true,  false, false, true,
                    "(b) PI SKIN + NOT signed in but the save is wallet-bound -- the wallet inputs still decide"),
            };
            foreach (var row in piRows)
            {
                bool got;
                try { got = (bool)decide4.Invoke(null, new object[] { row.wc, row.wb, row.legacy, row.pi }); }
                catch (Exception ex)
                {
                    failures.Add("[pi-login-gate] four-input seam threw on '" + row.what + "': " +
                                 ex.GetType().Name + ": " + ex.Message);
                    continue;
                }
                log.AppendLine("  (wc=" + row.wc + ",wb=" + row.wb + ",legacy=" + row.legacy + ",pi=" + row.pi +
                               ") -> " + Word(got) + " (want " + Word(row.want) + ") : " + row.what);
                if (got == row.want) continue;
                failures.Add(row.want
                    ? "[pi-login-gate] the gate would PRESENT the CHOOSE YOUR WALLET wall to a player who is " +
                      "already in -- " + row.what + " (this IS the WO-1322 defect)"
                    : "[pi-login-gate] the gate would SKIP the login surface for " + row.what +
                      " -- a player with no identity at all must still get the one-time connect or guest");
            }

            // -- (c) THE SKR / SOLANA SKIN IS BYTE-FOR-BYTE UNCHANGED ------------
            CheckSkrUnchanged(decide3, decide4, failures, log);

            // -- (e) source pins -------------------------------------------------
            CheckSource(failures, log);

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        /// <summary>
        /// (d) WO-1322 explicitly forbids passing the Pi flag through <c>legacySignedIn</c>.
        /// The fourth parameter must be named for what it is, and the third must still be
        /// the dead legacy one -- otherwise a reader deleting "the permanently false legacy
        /// input" would delete the live Pi identity source with it.
        /// </summary>
        private static void CheckParameterNaming(MethodInfo decide4, List<string> failures, StringBuilder log)
        {
            var ps = decide4.GetParameters();
            if (ps.Length != 4)
            {
                failures.Add("[pi-login-gate] the four-input seam does not have four parameters");
                return;
            }
            string third = ps[2].Name ?? string.Empty;
            string fourth = ps[3].Name ?? string.Empty;
            log.AppendLine("  seam parameters: " + ps[0].Name + ", " + ps[1].Name + ", " + third + ", " + fourth);

            if (third.IndexOf("legacy", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[pi-login-gate] the THIRD parameter is no longer the dead 'legacySignedIn' input -- " +
                             "if the Pi identity was folded into it, a live identity source is now hiding behind a " +
                             "parameter documented as permanently false (WO-1322 forbids exactly this)");

            if (fourth.IndexOf("pi", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[pi-login-gate] the FOURTH parameter is not named for the Pi identity ('" + fourth +
                             "') -- the next reader cannot see what the input is, which is the whole reason " +
                             "WO-1322 refused to reuse legacySignedIn");

            if (fourth.IndexOf("legacy", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[pi-login-gate] the FOURTH parameter is named 'legacy...' -- the live Pi identity " +
                             "source must not be labelled legacy");
        }

        /// <summary>
        /// (c) THE REGRESSION RISK, asserted directly. Every row of the three-input WO-1249
        /// truth table is re-driven through the FOUR-input seam with the Pi input pinned
        /// FALSE (which is exactly what the caller passes off the Pi skin, because
        /// PresentOrContinue AND-s the flag with AuthMode == SkinAuthMode.PiSdk). The two
        /// overloads must agree on every row, and the SKR first run must still PRESENT.
        /// </summary>
        private static void CheckSkrUnchanged(MethodInfo decide3, MethodInfo decide4,
                                              List<string> failures, StringBuilder log)
        {
            log.AppendLine("--- (c) SKR / SOLANA SKIN: byte-for-byte unchanged (pi input pinned FALSE) ---");
            bool[] bools = { false, true };
            int rows = 0;
            foreach (bool wc in bools)
                foreach (bool wb in bools)
                    foreach (bool legacy in bools)
                    {
                        bool old3, new4;
                        try
                        {
                            old3 = (bool)decide3.Invoke(null, new object[] { wc, wb, legacy });
                            new4 = (bool)decide4.Invoke(null, new object[] { wc, wb, legacy, false });
                        }
                        catch (Exception ex)
                        {
                            failures.Add("[pi-login-gate] SKR parity row (" + wc + "," + wb + "," + legacy +
                                         ") threw: " + ex.GetType().Name + ": " + ex.Message);
                            continue;
                        }
                        rows++;
                        log.AppendLine("  SKR (" + wc + "," + wb + "," + legacy + ") -> 3-input " + Word(old3) +
                                       " / 4-input(pi=false) " + Word(new4));
                        if (old3 != new4)
                            failures.Add("[pi-login-gate] SKR/Solana BEHAVIOUR CHANGED at (walletConnected=" + wc +
                                         ", walletIdentityBound=" + wb + ", legacySignedIn=" + legacy + "): the " +
                                         "three-input gate says " + Word(old3) + " and the four-input gate says " +
                                         Word(new4) + ". WO-1322 forbids weakening the gate for the wallet skin.");
                    }

            if (rows != 8)
            {
                failures.Add("[pi-login-gate] the SKR parity table drove " + rows + " rows, not 8 -- the " +
                             "byte-for-byte proof is incomplete and must not read as a pass");
                return;
            }

            // The one row WO-1249 calls out by name, asserted on its own so a future reader
            // sees it stated rather than buried in a loop: a genuine SKR first run PRESENTS.
            bool firstRun;
            try { firstRun = (bool)decide4.Invoke(null, new object[] { false, false, false, false }); }
            catch (Exception ex)
            {
                failures.Add("[pi-login-gate] SKR first-run row threw: " + ex.GetType().Name + ": " + ex.Message);
                return;
            }
            log.AppendLine("  SKR FIRST RUN (all four false) -> " + Word(firstRun) + " (want PRESENT)");
            if (firstRun)
                failures.Add("[pi-login-gate] an SKR/Solana first run with every input false now CONTINUES -- " +
                             "WO-1249: that PRESENT is the one-time connect, identical on a tester APK and the " +
                             "store build, and it must not be skipped");
        }

        private static void CheckSource(List<string> failures, StringBuilder log)
        {
            log.AppendLine("--- (e) LoginPanelController.cs source pins ---");
            string path = Path.Combine(Application.dataPath, "_Modules/Onboarding/LoginPanelController.cs");
            if (!File.Exists(path))
            {
                failures.Add("[pi-login-gate] LoginPanelController.cs not found");
                return;
            }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[pi-login-gate] LoginPanelController.cs unreadable (" + ex.Message + ")");
                return;
            }

            Require(text, "PiSignInController.IsSignedIn", failures, log,
                "the gate no longer samples the Pi session -- a signed-in Pi player meets the CHOOSE YOUR " +
                "WALLET wall again (WO-1322, owner capture 2026-09-02)");
            Require(text, "SkinAuthMode.PiSdk", failures, log,
                "the gate no longer AND-s the Pi input with the Pi auth mode -- that clause is the ONLY thing " +
                "keeping the SKR/Solana skin unchanged, so dropping it weakens the wallet gate everywhere");
            Require(text, "piIdentitySignedIn=", failures, log,
                "the decision trace no longer reports the Pi input -- the next wrong outcome is invisible in a " +
                "Pi Browser capture (INSTRUMENTATION_STANDARD 1.4b: report the decision AND its inputs)");
            Require(text, "login gate decision=", failures, log,
                "the gate's decision trace is gone entirely");

            // WO-1322 / WO-837-B: PresentOrContinue must stay synchronous. Slice the method
            // body and forbid an await inside it specifically -- the file legitimately awaits
            // elsewhere (OnConnectWallet, WatchLateConnect), so a whole-file scan cannot say this.
            CheckPresentOrContinueIsSynchronous(text, failures, log);

            // Never log the Pi identity itself. The booleans plus the auth mode are enough.
            Forbid(text, "SignedInUid", failures, log,
                "the login gate touches the Pi UID -- it must read only the IsSignedIn boolean, and a uid must " +
                "never reach Player.log (same rule as the wallet address, WO-1249)");
            Forbid(text, "SignedInUsername", failures, log,
                "the login gate touches the Pi username -- never log or render a player identity here");

            // Softlock law: guest is never disabled. SetBusy must still touch only _connectWallet.
            Require(text, "if (_connectWallet != null) _connectWallet.interactable = !busy;", failures, log,
                "SetBusy no longer reads as 'lock the connect button only' -- Play as Guest must remain " +
                "reachable and never disabled (softlock law, LoginPanelController header)");
            Forbid(text, "_guest.interactable", failures, log,
                "something now sets Play as Guest interactable -- the escape hatch must never be disabled");
        }

        private static void CheckPresentOrContinueIsSynchronous(string text, List<string> failures, StringBuilder log)
        {
            const string sig = "public static void PresentOrContinue(";
            int start = text.IndexOf(sig, StringComparison.Ordinal);
            if (start < 0)
            {
                failures.Add("[pi-login-gate] PresentOrContinue(Action) is gone or is no longer 'public static void' " +
                             "-- if it became async, WO-837-B's removal of the 12s blocking boot probe " +
                             "(the worst softlock site on the whole surface) has been undone");
                return;
            }
            // Body ends at the next method-level declaration; the private Build() follows it.
            int end = text.IndexOf("private void Build()", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(text.Length, start + 6000);
            // CODE ONLY. The body's own comments SAY the words "no await" and "no network call"
            // (that is the whole point of the WO-837-B warning living there), so a raw substring
            // scan would fail on the very comment documenting the rule -- a hollow red. Strip the
            // line comments and assert against what the compiler actually sees.
            string body = StripLineComments(text.Substring(start, end - start));

            if (body.IndexOf("await ", StringComparison.Ordinal) >= 0)
                failures.Add("[pi-login-gate] PresentOrContinue contains an 'await' -- WO-837-B removed a blocking " +
                             "network probe from exactly here and WO-1322 forbids reintroducing one. " +
                             "PiSignInController.IsSignedIn is a static bool; keep the gate synchronous.");
            else
                log.AppendLine("  PresentOrContinue body contains no await OK");

            foreach (string banned in new[] { "UnityWebRequest", "HttpClient", ".Result", "Task.Run" })
            {
                if (body.IndexOf(banned, StringComparison.Ordinal) >= 0)
                    failures.Add("[pi-login-gate] PresentOrContinue references '" + banned + "' -- no network call " +
                                 "and no blocking wait may live in the boot gate (WO-837-B / WO-1322)");
            }
        }

        /// <summary>
        /// Drop every <c>//</c> line comment (and <c>///</c> doc line) so the await/network pins
        /// read real code. Deliberately simple: this body contains no block comments and no string
        /// literal carrying a slash pair, and a simple stripper that over-strips would only make
        /// the pin blind -- so the pins above are paired with the positive Require checks in
        /// CheckSource, which read the file whole.
        /// </summary>
        private static string StripLineComments(string body)
        {
            var sb = new StringBuilder(body.Length);
            foreach (string line in body.Split('\n'))
            {
                int slash = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(slash >= 0 ? line.Substring(0, slash) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static string Word(bool continueIn) => continueIn ? "CONTINUE" : "PRESENT";

        private static void Require(string text, string needle, List<string> failures, StringBuilder log, string why)
        {
            if (text.IndexOf(needle, StringComparison.Ordinal) < 0)
                failures.Add("[pi-login-gate] source no longer contains '" + needle + "' -- " + why);
            else
                log.AppendLine("  source references '" + needle + "' OK");
        }

        private static void Forbid(string text, string needle, List<string> failures, StringBuilder log, string why)
        {
            if (text.IndexOf(needle, StringComparison.Ordinal) >= 0)
                failures.Add("[pi-login-gate] source contains '" + needle + "' -- " + why);
            else
                log.AppendLine("  source does not contain '" + needle + "' OK");
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "PI_LOGIN_GATE_OK");
                return "PI LOGIN GATE OK -- a signed-in Pi player continues, an unsigned one is unchanged, " +
                       "and the SKR/Solana truth table is bit-identical to the three-input gate";
            }
            string reason = "pi-login-gate: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "PI_LOGIN_GATE_FAIL: " + reason);
            return reason;
        }
    }
}
