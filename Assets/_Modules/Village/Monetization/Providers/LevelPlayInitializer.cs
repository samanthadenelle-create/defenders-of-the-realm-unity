// =============================================================================
// LevelPlayInitializer — apply privacy, THEN initialise LevelPlay. In that order,
// always.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village.AdProviders — its OWN assembly, guarded by
// defineConstraints ["LEVELPLAY_PRESENT"] with a versionDefine on
// com.unity.services.levelplay.
//
// ⛔ WHY ITS OWN ASSEMBLY AND NOT A #if. An asmdef reference to a missing assembly is a
// hard error, so a `#if` inside DeNelle.Village would NOT survive the package being
// absent — the dangling `Unity.LevelPlay` reference errors before any preprocessor
// runs. An assembly whose defineConstraint is unsatisfied is skipped ENTIRELY, its
// references included, which is the only construct that makes this adapter genuinely
// optional. Add the package and the versionDefine fires, the constraint is satisfied,
// and this code compiles itself back in with nothing to remember.
//
// Folder is also load-bearing: AdServiceSeamRegression allows a concrete ad-vendor
// token only under /Ads/Providers/ or /Monetization/Providers/ (WO-912 §10.5). The folder is not cosmetic:
// Game code reaches ads through DeNelle.Core.Ads.IAdService; a vendor name loose in game
// code turns a forced provider swap into a rewrite. This file is the ADAPTER, so it is the
// one place that name belongs.
// This is the ONLY file in the project that knows LevelPlay exists as a concrete
// SDK; AdConsentService (Core) stays provider-agnostic so swapping mediators never
// means re-deciding privacy.
//
// SDK: com.unity.services.levelplay@9.5.1 ("Ads Mediation"), verified installed.
// API verified against the package source, not documentation:
//   Unity.Services.LevelPlay.LevelPlay.Init(string appKey, string userId = null)
//   LevelPlayPrivacySettings.SetGDPRConsent(bool)   <- current; the Dictionary
//   overload SetGDPRConsents is [Obsolete] in this version
//   LevelPlayPrivacySettings.SetCCPA(bool) / SetCOPPA(bool)
//
// ⛔ THE ORDERING RULE. Privacy state MUST be set before Init. Set it after and the
// first impressions have already gone out on the wrong basis, and they cannot be
// un-sent. So this withholds Init entirely until the player has answered, rather
// than initialising on a default and correcting afterwards. Showing no ads for one
// extra screen is fine; serving personalised ads to someone who refused them is not.
//
// GATED OFF BY DEFAULT. FeatureFlags.RewardedAdSkip is the existing ad gate and it
// defaults OFF, so nothing here runs for players until the owner flips it. That is
// deliberate: the app key is live, and a live key plus an accidental init is a real
// impression on a real account.
//
// APP KEY 27850b635 is not a secret. It ships inside the APK and identifies the
// app; it authorises nothing. Hard-coding it is correct - reading it from a config
// the store build cannot ship would just be a way to fail on device.
// =============================================================================

using System;
using UnityEngine;
using Unity.Services.LevelPlay;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Monetization;
using DeNelle.Core.UI;

namespace DeNelle.Village.Monetization
{
    /// <summary>Initialises LevelPlay exactly once, after consent is resolved.</summary>
    public sealed class LevelPlayInitializer : MonoBehaviour
    {
        private const string Sys = "LevelPlay";

        /// <summary>LevelPlay dashboard app key for com.denellestudios.echoesofelarion (Android).</summary>
        private const string AppKey = "27850b635";

        private static LevelPlayInitializer s_instance;
        private static bool s_initStarted;

        /// <summary>True once the SDK has reported a successful init. Ad call sites may gate on this.</summary>
        public static bool Ready { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_instance != null) return;

            // The ad gate owns whether ads exist at all. With it OFF we do not init, do not ask for
            // consent, and do not touch the SDK - an un-asked question is the honest state when the
            // feature is not live.
            if (!FeatureFlags.RewardedAdSkip)
            {
                FlowTrace.Step(Sys, "ads are flagged OFF (ff.rewardedadskip) - LevelPlay not initialised " +
                                    "and no consent prompt shown. Flip the flag to bring the path up.");
                return;
            }

            var go = new GameObject("LevelPlayInitializer");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<LevelPlayInitializer>();
        }

        private void Start()
        {
            Guard.Try(Sys, "begin LevelPlay bring-up", BeginBringUp);
        }

        private void BeginBringUp()
        {
            if (s_initStarted) return;

            if (AdConsentService.IsDecided)
            {
                FlowTrace.Step(Sys, $"consent already on file ({AdConsentService.Describe()}) - " +
                                    "applying privacy then initialising.");
                ApplyPrivacyThenInit();
                return;
            }

            // NOT DECIDED: ask first, init second. Never the other way round.
            FlowTrace.Step(Sys, "no consent on file - showing the prompt BEFORE init. The SDK stays " +
                                "uninitialised until the player answers.");
            AdConsentPanel.Show(onDecided: () =>
            {
                if (!AdConsentService.IsDecided)
                {
                    // The prompt closed without an answer (arbiter swap). Do NOT init on a guess:
                    // an assumed consent is the one failure mode this whole file exists to prevent.
                    FlowTrace.Warn(Sys, "consent prompt closed with no answer - LevelPlay NOT initialised. " +
                                        "It will ask again next launch.");
                    return;
                }
                Guard.Try(Sys, "apply privacy + init after consent", ApplyPrivacyThenInit);
            });
        }

        private void ApplyPrivacyThenInit()
        {
            if (s_initStarted) return;
            s_initStarted = true;

            // ---- PRIVACY FIRST, every branch, no exceptions --------------------
            Guard.Try(Sys, "apply privacy settings", () =>
            {
                bool personalised = AdConsentService.Gdpr == ConsentState.Granted;
                LevelPlayPrivacySettings.SetGDPRConsent(personalised);
                LevelPlayPrivacySettings.SetCCPA(AdConsentService.CcpaOptOut);
                LevelPlayPrivacySettings.SetCOPPA(AdConsentService.ChildDirected);

                FlowTrace.Step(Sys, $"privacy applied BEFORE init: gdprConsent={personalised} " +
                                    $"ccpaOptOut={AdConsentService.CcpaOptOut} " +
                                    $"coppa={AdConsentService.ChildDirected}");
            });

            // ---- then, and only then, init -------------------------------------
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed  += OnInitFailed;

            FlowTrace.Step(Sys, $"LevelPlay.Init('{AppKey}') - app is set to Temporary in the dashboard " +
                                "and live inventory is enabled manually by LevelPlay support (the Solana " +
                                "dApp Store has no https listing URL for auto-verification).");
            LevelPlay.Init(AppKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration config)
        {
            Ready = true;
            FlowTrace.Step(Sys, "LEVELPLAY_INIT_OK - SDK initialised; ad units may load.");
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Ready = false;
            // FAIL, not Warn: with ads flagged ON, an SDK that never initialised means every ad
            // offer in the game is a button that cannot pay out. That is player-facing.
            FlowTrace.Fail(Sys, $"LEVELPLAY_INIT_FAIL - {error?.ErrorMessage ?? "(no message)"}. " +
                                "No ad will serve this session; every ad-gated offer must degrade " +
                                "to its non-ad path rather than dangle a dead button.");
        }

        private void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed  -= OnInitFailed;
            if (s_instance == this) s_instance = null;
        }
    }
}
