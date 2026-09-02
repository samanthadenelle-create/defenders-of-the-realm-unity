// =============================================================================
// PiAdGrantDecision — WO-1320. THE ONE FUNCTION THAT DECIDES WHETHER AN AD PAYS.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village.PiAds   Namespace: DeNelle.Village.Monetization
//
// WHY THIS IS A PURE STATIC AND NOT A METHOD ON THE PROVIDER.
// The grant rule is the single thing in this whole feature that must never regress,
// and a rule buried inside a MonoBehaviour that needs a Pi Browser, a live SDK, a real
// ad impression and a network round-trip to exercise is a rule NOTHING will ever test.
// Pulled out here it takes two plain inputs and returns an AdShowResult, so
// PiAdRewardVerificationRegression can drive every outcome — including the two that
// matter most, "client claims rewarded but the server did not grant" and "AD_CLOSED" —
// with no Unity runtime at all. The provider's job is to gather the two inputs; this
// file's job is to be right about them.
//
// ⛔ THE INVARIANT, stated once: `AdShowResult.Earned()` is constructed in EXACTLY ONE
// PLACE in the Pi ad path, and it is inside this file, behind BOTH conditions:
//     the SDK said AD_REWARDED   AND   /api/pi/ads-verify said granted.
// Neither alone is sufficient and the second is the one that counts. The Pi docs are
// explicit that a player may run a hacked SDK build, which makes the client's
// AD_REWARDED a claim by an untrusted party about its own reward. AdGateService's Law 4
// ("GRANT ONLY ON THE GENUINE EARNED-REWARD CALLBACK ... granting on show is fraud
// against the network") is the same rule from the game's side; this is its Pi shape.
//
// ⚠ THE RESULT VOCABULARY IS NOT EXHAUSTIVELY DOCUMENTED. Four strings are confirmed
// (PiAdResults). Everything else is logged VERBATIM and mapped to a generic failure. No
// enum, no parse, no guess — an unknown string must never fall through to a grant, and
// it must never be quietly rewritten into a known one either, because the exact text is
// the only way the next seat learns what Pi actually sends.
// =============================================================================

using System;
using DeNelle.Core.Ads;
using DeNelle.Core.Platform;

namespace DeNelle.Village.Monetization
{
    /// <summary>
    /// Turns one Pi ad presentation plus the server's verdict into an <see cref="AdShowResult"/>.
    /// Pure and side-effect free: no logging, no I/O, no statics. The caller logs
    /// <paramref name="trace"/>, which explains the decision in one line.
    /// </summary>
    public static class PiAdGrantDecision
    {
        /// <summary>
        /// Decide whether a Pi rewarded presentation pays out.
        ///
        /// <paramref name="serverGranted"/> is the answer from /api/pi/ads-verify —
        /// <c>mediator_ack_status == "granted"</c> and nothing looser. Pass FALSE whenever the
        /// server was not asked, could not be reached, or answered anything else: every one of
        /// those is a refusal, because an unverified reward and a denied one are the same thing
        /// from the ad network's point of view.
        /// </summary>
        /// <param name="ad">What the SDK reported, verbatim.</param>
        /// <param name="serverGranted">True ONLY when the backend confirmed mediator_ack_status == granted.</param>
        /// <param name="trace">One line explaining the decision, for FlowTrace. Never null.</param>
        public static AdShowResult Decide(PiAdResult ad, bool serverGranted, out string trace)
        {
            // ---- 1. The bridge never got an answer: timeout, no Pi.Ads, SDK rejection. ------
            // Distinct from NoFill on purpose (IAdService's own header): "the network had
            // nothing for you" and "our call broke" need different copy and different alarm.
            if (!ad.Ok)
            {
                trace = "PI_AD_NO_GRANT reason=bridge-failed detail=" +
                        (string.IsNullOrEmpty(ad.Error) ? "(no message)" : ad.Error);
                return AdShowResult.Unavailable(AdUnavailableReason.LoadFailed);
            }

            string result = ad.Result ?? string.Empty;

            // ---- 2. This Pi client cannot serve ads at all. ---------------------------------
            // Disabled, not NoFill: an old Pi Browser or an unsupported platform is not a
            // transient market condition and will not fix itself by trying again in a minute.
            if (ad.IsNotSupported)
            {
                trace = "PI_AD_NO_GRANT reason=ads-not-supported result=" + result;
                return AdShowResult.Unavailable(AdUnavailableReason.Disabled);
            }

            // ---- 3. THE PLAYER DISMISSED IT. ------------------------------------------------
            // Checked BEFORE anything looks at the server verdict, and deliberately so: even if
            // a verification somehow came back granted for this adId, a closed ad pays nothing.
            // This is the case the old `_adTcs.TrySetResult(true)` got wrong — it resolved TRUE
            // for AD_CLOSED, which is a free reward for tapping X.
            if (ad.IsClosed)
            {
                trace = "PI_AD_NO_GRANT reason=player-dismissed result=" + result +
                        " (AD_CLOSED never grants, whatever the server says)";
                return AdShowResult.Dismissed();
            }

            // ---- 4. Anything that is not the confirmed rewarded string. ---------------------
            // Includes AD_LOADED arriving from a show call (a preload result where a
            // presentation result belongs) and every undocumented string. Generic failure, and
            // the exact text is carried into the trace so it can be read, not guessed at.
            if (!ad.ClaimsRewarded)
            {
                trace = "PI_AD_NO_GRANT reason=" +
                        (ad.IsUnrecognised ? "unrecognised-result" : "not-rewarded") +
                        " result='" + result + "'";
                return AdShowResult.Unavailable(AdUnavailableReason.LoadFailed);
            }

            // ---- 5. The client CLAIMS the reward. Now it has to be proved. ------------------
            // No adId means there is literally nothing the backend can check, so there is no
            // path to a grant. Refuse rather than fall back to trusting the claim: a missing
            // token is exactly what a tampered client would present.
            if (string.IsNullOrEmpty(ad.AdId))
            {
                trace = "PI_AD_NO_GRANT reason=missing-adid result=" + result +
                        " (AD_REWARDED with no adId is unverifiable, so it is refused)";
                return AdShowResult.Unavailable(AdUnavailableReason.LoadFailed);
            }

            if (!serverGranted)
            {
                trace = "PI_AD_NO_GRANT reason=server-did-not-grant result=" + result +
                        " adId=" + Mask(ad.AdId) +
                        " (the client's AD_REWARDED is never sufficient - /api/pi/ads-verify " +
                        "must answer mediator_ack_status=granted)";
                return AdShowResult.Unavailable(AdUnavailableReason.LoadFailed);
            }

            // ---- 6. Both conditions met. The ONLY Earned() in the Pi ad path. ---------------
            trace = "PI_AD_GRANTED result=" + result + " adId=" + Mask(ad.AdId) +
                    " serverGranted=true";
            return AdShowResult.Earned();
        }

        /// <summary>
        /// Shorten an adId for the trace sink. It is a shared database and the id is a live
        /// reward token, so enough to correlate two lines and not enough to replay one.
        /// Same reasoning and same shape as PiPaymentEndpoints.Mask.
        /// </summary>
        public static string Mask(string adId)
        {
            if (string.IsNullOrEmpty(adId)) return "<none>";
            return adId.Length <= 8 ? adId : adId.Substring(0, 8) + "...";
        }

        /// <summary>
        /// True when <paramref name="result"/> is one of the four CONFIRMED Pi Ads strings.
        /// Exposed so the provider and the regression agree on what "recognised" means rather
        /// than each keeping its own list, which is how the two drift apart.
        /// </summary>
        public static bool IsConfirmedResultString(string result) =>
            string.Equals(result, PiAdResults.AdLoaded, StringComparison.Ordinal) ||
            string.Equals(result, PiAdResults.AdRewarded, StringComparison.Ordinal) ||
            string.Equals(result, PiAdResults.AdClosed, StringComparison.Ordinal) ||
            string.Equals(result, PiAdResults.AdsNotSupported, StringComparison.Ordinal);
    }
}
