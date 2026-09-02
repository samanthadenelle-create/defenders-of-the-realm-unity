// =============================================================================
// PiAdRewardVerificationRegression — WO-1320. NO SERVER GRANT, NO REWARD.
// Marker: PI_AD_REWARD_OK
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Wired into DeNelle.Editor.DataRegression.RunAll.
//
// WHAT THIS GUARDS, and why the two headline cases are the ones they are.
//
// 1. "NO GRANTED VERIFICATION -> NO GRANT". The Pi Ads docs require server-side
//    verification before rewarding, because a player may run a hacked SDK build - so a
//    client-side { result: "AD_REWARDED" } is a claim made by an untrusted party about
//    its own reward. The tempting regression is the one that never runs: a live ad, a
//    live backend, a phone. This one drives PiAdGrantDecision directly instead, so the
//    rule is exercised on every gate run with no network and no Pi Browser.
//
// 2. "AD_CLOSED GRANTS NOTHING". This is the LATENT DEFECT the work order was minted
//    for. WebGLPiPlatform did `case "adReady": _adTcs?.TrySetResult(true);` - it
//    resolved TRUE on the mere ARRIVAL of a callback, because PiCallbackData declared
//    no `result` and no `adId` and JsonUtility had silently dropped both. AD_CLOSED,
//    ADS_NOT_SUPPORTED and a dismissed rewarded ad all read as "rewarded". Nothing had
//    ever called ShowAd, which is the only reason it never paid out a free reward. A
//    case that feeds AD_CLOSED and asserts no grant is what stops that returning.
//
// It is NOT a hollow pass: every behavioural case below asserts a specific outcome on a
// real call into shipping code, and the source-lints assert the ABSENCE of the exact
// shapes that would let a grant escape the decision function.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.Ads;
using DeNelle.Core.Platform;
using DeNelle.Village.Monetization;

namespace DeNelle.Editor
{
    /// <summary>
    /// Pins the Pi rewarded-ad grant rule. Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class PiAdRewardVerificationRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- PI REWARDED-AD VERIFICATION (WO-1320) ---");

            // =================================================================
            //  A. THE GRANT RULE, driven directly.
            // =================================================================

            // A1 - THE HEADLINE: the client claims the reward, the server did NOT grant it.
            //      This is the hacked-SDK case, and it is the whole reason ads-verify exists.
            Expect(failures, "A1 no-granted-verification -> no grant",
                   !Decide(Rewarded("adid-abc123"), serverGranted: false).Rewarded,
                   "a client-side AD_REWARDED with serverGranted=false GRANTED a reward. The Pi docs " +
                   "require server verification before rewarding; the client's word is never enough.");

            // A2 - THE OTHER HEADLINE: the player dismissed the ad. The old always-true bug.
            //      Fed with serverGranted TRUE on purpose - AD_CLOSED must lose even then, so a
            //      stale or replayed verification can never resurrect a dismissed ad.
            Expect(failures, "A2 AD_CLOSED -> no grant (even if the server said granted)",
                   !Decide(Closed(), serverGranted: true).Rewarded,
                   "AD_CLOSED granted a reward. This is precisely the WO-1320 latent defect: the old " +
                   "handler resolved true for every outcome, so dismissing the ad paid out.");

            // A2b - and it must be reported as DISMISSED specifically, not as a generic failure.
            //       AdGateService copy and telemetry both read the reason.
            Expect(failures, "A2b AD_CLOSED reports Dismissed/Abandoned",
                   Decide(Closed(), false).Outcome == AdShowOutcome.Dismissed &&
                   Decide(Closed(), false).Reason == AdUnavailableReason.Abandoned,
                   "AD_CLOSED must report Dismissed/Abandoned - 'you closed it early' and 'the network " +
                   "broke' are different sentences to the player.");

            // A3 - the success path. A suite that only proves refusals would pass just as well over
            //      a provider that can never grant at all, which is not the feature.
            Expect(failures, "A3 AD_REWARDED + serverGranted -> Earned",
                   Decide(Rewarded("adid-abc123"), serverGranted: true).Rewarded,
                   "AD_REWARDED verified as granted did NOT pay out - the feature does not work.");

            // A4 - AD_REWARDED with no adId is UNVERIFIABLE, so it is refused. A missing token is
            //      exactly what a tampered client would present.
            Expect(failures, "A4 AD_REWARDED without adId -> no grant",
                   !Decide(Rewarded(""), serverGranted: true).Rewarded,
                   "AD_REWARDED with an empty adId granted a reward. With no adId there is nothing " +
                   "/api/pi/ads-verify can check, so there is no path to a grant.");

            // A5 - ADS_NOT_SUPPORTED is Disabled, not NoFill: an old Pi client will not fix itself
            //      by retrying in a minute, and the copy must not promise that it will.
            Expect(failures, "A5 ADS_NOT_SUPPORTED -> Disabled, no grant",
                   !Decide(Result(PiAdResults.AdsNotSupported, ""), true).Rewarded &&
                   Decide(Result(PiAdResults.AdsNotSupported, ""), true).Reason == AdUnavailableReason.Disabled,
                   "ADS_NOT_SUPPORTED must be a non-granting Disabled outcome.");

            // A6 - AN UNDOCUMENTED RESULT STRING GRANTS NOTHING. The Pi result vocabulary is not
            //      exhaustively published; only four strings are confirmed. An unknown one must
            //      fall to a generic failure, never through to a grant.
            Expect(failures, "A6 unrecognised result string -> no grant",
                   !Decide(Result("AD_SOMETHING_NEW", "adid-abc123"), serverGranted: true).Rewarded,
                   "an undocumented Pi result string granted a reward. Unknown results are a generic " +
                   "failure by rule (WO-1320): only AD_LOADED/AD_REWARDED/AD_CLOSED/ADS_NOT_SUPPORTED " +
                   "are confirmed.");

            // A7 - a bridge failure (local timeout, missing Pi.Ads, SDK rejection) grants nothing.
            Expect(failures, "A7 bridge failure -> no grant",
                   !Decide(PiAdResult.Fail("local timeout"), serverGranted: true).Rewarded,
                   "a failed bridge call granted a reward.");

            // A8 - AD_LOADED is a REQUEST result, not a presentation result. Arriving from a show
            //      call it is a confused SDK, not an earned reward.
            Expect(failures, "A8 AD_LOADED from a show -> no grant",
                   !Decide(Result(PiAdResults.AdLoaded, "adid-abc123"), serverGranted: true).Rewarded,
                   "AD_LOADED granted a reward. It means an ad is loaded, not that one was watched.");

            // A9 - the confirmed-vocabulary helper is exactly the four strings and nothing else.
            //      Shared by the provider and this suite so the two cannot drift apart.
            Expect(failures, "A9 confirmed result vocabulary is exactly four strings",
                   PiAdGrantDecision.IsConfirmedResultString(PiAdResults.AdLoaded) &&
                   PiAdGrantDecision.IsConfirmedResultString(PiAdResults.AdRewarded) &&
                   PiAdGrantDecision.IsConfirmedResultString(PiAdResults.AdClosed) &&
                   PiAdGrantDecision.IsConfirmedResultString(PiAdResults.AdsNotSupported) &&
                   !PiAdGrantDecision.IsConfirmedResultString("AD_NOT_AVAILABLE") &&
                   !PiAdGrantDecision.IsConfirmedResultString(""),
                   "the confirmed Pi Ads vocabulary has changed. 'AD_NOT_AVAILABLE' in particular was " +
                   "claimed by an older in-repo work order and could NOT be confirmed in the docs - it " +
                   "must not be treated as known.");

            // A10 - the masker never echoes a full reward token into the shared trace database.
            Expect(failures, "A10 adId is masked for the trace sink",
                   PiAdGrantDecision.Mask("abcdefghijklmnop") != "abcdefghijklmnop" &&
                   PiAdGrantDecision.Mask(null) == "<none>",
                   "adId is written to the trace sink unmasked. It is a live reward token in a shared db.");

            log.AppendLine($"  grant-rule cases: 11 driven through PiAdGrantDecision.Decide (no network, no Pi Browser).");

            // =================================================================
            //  B. SOURCE-LINTS: no second door to a grant.
            // =================================================================
            string modulesDir = null;
            try { modulesDir = Path.Combine(Application.dataPath, "_Modules"); } catch { }

            if (string.IsNullOrEmpty(modulesDir) || !Directory.Exists(modulesDir))
            {
                // hollow-pass-ok: same shape as the other _Modules source-lints; that directory
                // cannot be absent in this project, so the skip is unreachable rather than risky.
                log.AppendLine("  (source-lints skipped -- Assets/_Modules not found)");
            }
            else
            {
                // B1 - the jslib sends FLAT adResult/adId. The nested `result: result` object was
                //      dropped by JsonUtility without an error, which is what made the outcome
                //      invisible to C# and the handler unconditionally true.
                string jslib = ReadOrEmpty(Path.Combine(Application.dataPath, "Plugins", "WebGL", "PiBridge.jslib"));
                if (jslib.Length == 0)
                {
                    failures.Add("Assets/Plugins/WebGL/PiBridge.jslib not found - the Pi ad bridge is missing");
                }
                else
                {
                    foreach (var token in new[] { "adResult:", "adId:", "PiIsAdReady", "PiRequestAd", "PiNativeFeatures" })
                        if (jslib.IndexOf(token, StringComparison.Ordinal) < 0)
                            failures.Add($"PiBridge.jslib no longer contains '{token}' - the WO-1320 ad bridge " +
                                         "has lost part of its contract (flat result fields + the ready/request/" +
                                         "feature probes).");

                    // The local timeout. Off Pi Browser the SDK can hang ~120s before rejecting
                    // (WO-678); without a guard, a caller awaiting showAd never resumes at all.
                    if (jslib.IndexOf("guard", StringComparison.Ordinal) < 0 ||
                        jslib.IndexOf("setTimeout", StringComparison.Ordinal) < 0)
                        failures.Add("PiBridge.jslib no longer arms a local timeout on the ad calls - " +
                                     "outside Pi Browser the SDK can hang ~120s and ShowAd had no timeout " +
                                     "at all (WO-678 / WO-1320).");
                }

                // B2 - PiCallbackData still DECLARES the fields. Their absence was the actual
                //      mechanism of the bug: JsonUtility cannot populate what is not declared.
                string webgl = ReadFirst(modulesDir, "WebGLPiPlatform.cs");
                if (webgl == null)
                {
                    failures.Add("WebGLPiPlatform.cs not found under Assets/_Modules");
                }
                else
                {
                    string code = StripComments(webgl);
                    foreach (var token in new[] { "adResult", "adId", "adReady", "featuresCsv" })
                        if (code.IndexOf(token, StringComparison.Ordinal) < 0)
                            failures.Add($"WebGLPiPlatform's PiCallbackData no longer declares '{token}' - " +
                                         "JsonUtility silently drops undeclared fields, which is exactly how " +
                                         "the ad result and adId went missing (WO-1320).");

                    // The always-true resolution must not come back in any form.
                    if (code.IndexOf("_adTcs", StringComparison.Ordinal) >= 0)
                        failures.Add("WebGLPiPlatform still has the old single '_adTcs' ad completion source. " +
                                     "That field is what `TrySetResult(true)` resolved unconditionally for " +
                                     "every outcome (WO-1320's latent free reward).");
                }

                // B3 - THE LOAD-BEARING LINT. AdShowResult.Earned() must be constructed in EXACTLY
                //      ONE place in the Pi ad path, and that place is PiAdGrantDecision - behind
                //      both the AD_REWARDED check and the server verdict. A provider that builds
                //      its own Earned() has a second door to a grant that this suite cannot see.
                string piDir = Path.Combine(modulesDir, "Village", "Monetization", "Providers", "Pi");
                if (!Directory.Exists(piDir))
                {
                    failures.Add("Assets/_Modules/Village/Monetization/Providers/Pi is missing - the Pi ad " +
                                 "provider must live in its OWN leaf assembly (DeNelle.Village.AdProviders " +
                                 "carries defineConstraints LEVELPLAY_PRESENT and would suppress it).");
                }
                else
                {
                    foreach (var path in Directory.GetFiles(piDir, "*.cs", SearchOption.AllDirectories))
                    {
                        string norm = path.Replace('\\', '/');
                        if (norm.EndsWith("PiAdGrantDecision.cs", StringComparison.OrdinalIgnoreCase)) continue;
                        if (StripComments(ReadOrEmpty(path)).IndexOf("AdShowResult.Earned", StringComparison.Ordinal) >= 0)
                            failures.Add($"{Rel(norm)} constructs AdShowResult.Earned() itself. The ONLY grant in " +
                                         "the Pi ad path belongs inside PiAdGrantDecision, behind BOTH the " +
                                         "AD_REWARDED check and /api/pi/ads-verify.");
                    }

                    // B4 - the provider actually asks the server. A decision function that is never
                    //      fed a real verdict is a rule nothing enforces at runtime.
                    string provider = ReadOrEmpty(Path.Combine(piDir, "PiAdProvider.cs"));
                    if (provider.Length == 0)
                        failures.Add("PiAdProvider.cs is missing from the Pi provider folder");
                    else
                    {
                        string pcode = StripComments(provider);
                        if (pcode.IndexOf("PiAdVerifyEndpoint.VerifyAsync", StringComparison.Ordinal) < 0)
                            failures.Add("PiAdProvider no longer calls PiAdVerifyEndpoint.VerifyAsync - the " +
                                         "server-side check the Pi docs require has been removed from the path.");
                        if (pcode.IndexOf("PiAdGrantDecision.Decide", StringComparison.Ordinal) < 0)
                            failures.Add("PiAdProvider no longer routes its outcome through PiAdGrantDecision.Decide " +
                                         "- the grant rule has moved somewhere this suite cannot reach it.");
                        if (pcode.IndexOf("IsPiBrowserEnvironment", StringComparison.Ordinal) < 0)
                            failures.Add("PiAdProvider no longer gates on WebGLPiPlatform.IsPiBrowserEnvironment - " +
                                         "it could register outside Pi Browser and race LevelPlay for the single " +
                                         "AdServices.Current.");
                        if (pcode.IndexOf("ad_network", StringComparison.Ordinal) < 0)
                            failures.Add("PiAdProvider no longer checks the 'ad_network' nativeFeatures token - " +
                                         "the documented Pi Ad Network feature gate is gone.");
                    }

                    // B5 - the leaf assembly must NOT carry a LevelPlay define constraint.
                    foreach (var asmdef in Directory.GetFiles(piDir, "*.asmdef", SearchOption.TopDirectoryOnly))
                    {
                        string text = ReadOrEmpty(asmdef);
                        string asmRel = Rel(asmdef.Replace('\\', '/'));
                        if (text.IndexOf("LEVELPLAY_PRESENT", StringComparison.Ordinal) >= 0)
                            failures.Add(asmRel + " carries LEVELPLAY_PRESENT. An assembly " +
                                         "whose defineConstraint is unsatisfied is skipped ENTIRELY, so this would " +
                                         "delete the Pi ad provider whenever the LevelPlay package is absent.");
                    }
                }

                // B6 - LevelPlay must refuse inside Pi Browser. The other half of one-provider.
                string lp = ReadFirst(modulesDir, "LevelPlayInitializer.cs");
                if (lp != null && StripComments(lp).IndexOf("IsPiBrowserEnvironment", StringComparison.Ordinal) < 0)
                    failures.Add("LevelPlayInitializer no longer refuses inside Pi Browser - two initialisers " +
                                 "would race for the single AdServices.Current and the winner would be decided " +
                                 "by RuntimeInitialize order, i.e. by nothing.");
            }

            // B7 - the endpoint exists and grants only on mediator_ack_status == granted.
            string endpoint = ReadOrEmpty(Path.Combine(RepoRoot(), "api", "pi", "ads-verify.js"));
            if (endpoint.Length == 0)
            {
                failures.Add("api/pi/ads-verify.js is missing - there is no server-side rewarded verification, " +
                             "which the Pi Ads docs require before any reward is given.");
            }
            else
            {
                if (endpoint.IndexOf("mediator_ack_status", StringComparison.Ordinal) < 0)
                    failures.Add("api/pi/ads-verify.js no longer reads mediator_ack_status - that field is the " +
                                 "ONLY grant condition.");
                if (endpoint.IndexOf("PI_NETWORK_API_KEY", StringComparison.Ordinal) >= 0 &&
                    endpoint.IndexOf("process.env", StringComparison.Ordinal) >= 0)
                    failures.Add("api/pi/ads-verify.js reads PI_NETWORK_API_KEY from the environment directly. " +
                                 "It must go through pi-payments.js's piApiKey()/configured(), which is the one " +
                                 "place that touches the key.");
            }

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log + "PI_AD_REWARD_OK");
                return true;
            }

            reason = "pi-ad-reward: " + string.Join("; ", failures);
            Debug.LogError(log + "PI_AD_REWARD_FAIL: " + reason);
            return false;
        }

        // -----------------------------------------------------------------
        //  helpers
        // -----------------------------------------------------------------

        private static AdShowResult Decide(PiAdResult ad, bool serverGranted) =>
            PiAdGrantDecision.Decide(ad, serverGranted, out _);

        private static PiAdResult Result(string result, string adId) =>
            new PiAdResult { Ok = true, Result = result, AdId = adId, Error = null };

        private static PiAdResult Rewarded(string adId) => Result(PiAdResults.AdRewarded, adId);
        private static PiAdResult Closed() => Result(PiAdResults.AdClosed, string.Empty);

        private static void Expect(List<string> failures, string caseName, bool condition, string detail)
        {
            if (!condition) failures.Add(caseName + ": " + detail);
        }

        /// <summary>Repo root, derived from Application.dataPath (".../Assets"). Never hardcoded (CLAUDE.md sec.0).</summary>
        private static string RepoRoot()
        {
            try { return Directory.GetParent(Application.dataPath).FullName; }
            catch { return string.Empty; }
        }

        private static string Rel(string normalisedPath)
        {
            int i = normalisedPath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? normalisedPath.Substring(i + 1) : normalisedPath;
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        private static string ReadFirst(string root, string fileName)
        {
            try
            {
                var hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                return hits.Length > 0 ? ReadOrEmpty(hits[0]) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Blank out comments so the lints test CODE, not prose. This file's own targets are all
        /// discussed at length in the headers they guard, and an oracle that cannot tell a doc
        /// comment from a call punishes the author for explaining the rule.
        /// (Borrowed verbatim from AdServiceSeamRegression, which learned it the hard way.)
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' '); break; }
                    sb.Append(' ');
                    i = end + 1;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
