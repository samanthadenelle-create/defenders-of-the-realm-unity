// =============================================================================
// AdConsentService — the single source of truth for ad-privacy consent.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/Monetization). Provider-AGNOSTIC on purpose: this
// class knows nothing about LevelPlay, ironSource or any SDK. The mediation layer
// reads it and applies it (LevelPlayInitializer). That mirrors the existing
// IAdService / RewardedAdManager seam — swapping mediators must never mean
// re-deciding privacy.
//
// ⚠ THIS IS TECHNICAL IMPLEMENTATION, NOT LEGAL ADVICE. Which regimes apply to
// this game depends on who plays it, what data is collected and where it ships —
// that determination is the owner's (with counsel), not this file's. What this
// file guarantees is that whatever she decides is captured once, persisted, and
// applied BEFORE any SDK initialises.
//
// ⛔ THE ORDERING RULE, and why it is the whole point.
// Every mediation SDK requires privacy state to be set BEFORE init. Set it after
// and the first impressions have already gone out under the wrong basis — and you
// cannot un-send them. So consent is resolved FIRST and init is deliberately
// WITHHELD until it is (see LevelPlayInitializer). A game that shows no ads for
// one extra screen is fine; one that serves personalised ads to someone who
// refused them is not.
//
// WITHDRAWABLE BY DESIGN. Consent that cannot be withdrawn is not consent, so the
// same panel is reachable for ever from Settings -> Privacy. That is also why the
// state is a tri-state (Unknown / Granted / Denied) rather than a bool: "has not
// been asked" and "was asked and said no" are different facts and must never
// collapse into the same false.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Monetization
{
    /// <summary>Tri-state consent. Unknown is NOT the same as Denied — see the header.</summary>
    public enum ConsentState
    {
        /// <summary>Never asked. Nothing may initialise on this value.</summary>
        Unknown = 0,
        /// <summary>Asked and granted.</summary>
        Granted = 1,
        /// <summary>Asked and refused. Ads may still serve, but NON-personalised.</summary>
        Denied = 2,
    }

    /// <summary>Persisted ad-privacy choices, applied by the mediation layer before init.</summary>
    public static class AdConsentService
    {
        private const string Sys = "AdConsent";

        private const string PrefGdpr       = "consent.gdpr";        // ConsentState
        private const string PrefCcpaOptOut = "consent.ccpaoptout";  // 0/1

        /// <summary>
        /// COPPA / child-directed. FALSE — OWNER-RULED 2026-08-20 ("i agree with you"), not inferred.
        /// This is a PRODUCT FACT rather than a player choice: Echoes of Elarion ships on the Solana
        /// dApp Store and is built around a crypto wallet, which is not a product directed to
        /// children. It is a named constant instead of a literal so the claim is auditable in one
        /// place, and so flipping it is a deliberate one-line act with a new owner ruling behind it
        /// — never an inferred default buried in a call site.
        ///
        /// ⚠ WHICH PRIVACY REGIMES APPLY to this title is a LEGAL determination the owner makes with
        /// counsel; this file implements the mechanism, it does not decide the question.
        /// </summary>
        public const bool ChildDirected = false;

        /// <summary>Raised whenever a choice changes, so live UI can re-read it.</summary>
        public static event Action Changed;

        /// <summary>The player's GDPR-style consent to PERSONALISED ads.</summary>
        public static ConsentState Gdpr
        {
            get
            {
                int raw = PlayerPrefs.GetInt(PrefGdpr, (int)ConsentState.Unknown);
                return raw == (int)ConsentState.Granted ? ConsentState.Granted
                     : raw == (int)ConsentState.Denied  ? ConsentState.Denied
                     : ConsentState.Unknown;
            }
        }

        /// <summary>True once the player has actually answered. Init gates on THIS, never on Gdpr alone.</summary>
        public static bool IsDecided => Gdpr != ConsentState.Unknown;

        /// <summary>
        /// CCPA / US state "do not sell or share my personal information". TRUE means the player
        /// HAS opted out — the same polarity the SDK expects, so nothing has to invert it at the
        /// call site. Default false = not opted out.
        /// </summary>
        public static bool CcpaOptOut => PlayerPrefs.GetInt(PrefCcpaOptOut, 0) == 1;

        /// <summary>Record the personalised-ads answer.</summary>
        public static void SetGdpr(bool granted)
        {
            PlayerPrefs.SetInt(PrefGdpr, (int)(granted ? ConsentState.Granted : ConsentState.Denied));
            PlayerPrefs.Save();
            FlowTrace.Step(Sys, $"GDPR consent recorded: {(granted ? "GRANTED" : "DENIED")} " +
                                "(denied still serves ads, non-personalised).");
            Raise();
        }

        /// <summary>Record the CCPA opt-out. True = the player opted OUT of sale/sharing.</summary>
        public static void SetCcpaOptOut(bool optedOut)
        {
            PlayerPrefs.SetInt(PrefCcpaOptOut, optedOut ? 1 : 0);
            PlayerPrefs.Save();
            FlowTrace.Step(Sys, $"CCPA opt-out recorded: {optedOut}");
            Raise();
        }

        /// <summary>
        /// Clear the recorded answer so the prompt is asked again. For the Settings "review my
        /// choices" door and for QA. Deliberately does NOT clear the CCPA opt-out: an opt-out is a
        /// standing instruction, and quietly reinstating sale/sharing because someone reopened a
        /// dialog would be the opposite of what the player asked for.
        /// </summary>
        public static void ResetGdprForReprompt()
        {
            PlayerPrefs.DeleteKey(PrefGdpr);
            PlayerPrefs.Save();
            FlowTrace.Step(Sys, "GDPR consent CLEARED - the prompt will be asked again. " +
                                "CCPA opt-out deliberately left as-is (a standing instruction).");
            Raise();
        }

        private static void Raise() =>
            Guard.Try(Sys, "raise consent Changed", () => Changed?.Invoke());

        /// <summary>One line describing the whole state, for logs and the Settings label.</summary>
        public static string Describe() =>
            $"gdpr={Gdpr} ccpaOptOut={CcpaOptOut} childDirected={ChildDirected}";
    }
}
