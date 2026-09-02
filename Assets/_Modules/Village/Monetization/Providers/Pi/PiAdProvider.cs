// =============================================================================
// PiAdProvider — WO-1320. The Pi Ad Network behind the EXISTING IAdService seam.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village.PiAds (its OWN leaf assembly)
// Namespace: DeNelle.Village.Monetization
//
// ⛔ WHY ITS OWN ASSEMBLY, AND WHY NOT THE ONE NEXT DOOR.
// `DeNelle.Village.AdProviders` — where LevelPlayInitializer lives — carries
// `defineConstraints: ["LEVELPLAY_PRESENT"]`, satisfied by a versionDefine on
// com.unity.services.levelplay. An assembly whose defineConstraint is unsatisfied is
// SKIPPED ENTIRELY. Putting Pi in there would therefore delete the Pi ad provider from
// the build on any machine or configuration where the LevelPlay package is absent —
// binding a Pi Browser feature to the presence of an unrelated Android SDK. This
// assembly has NO defineConstraints and references only DeNelle.Core + UniTask, so it
// always compiles and depends on nothing that can go missing.
//
// The FOLDER is load-bearing too: AdServiceSeamRegression permits a concrete ad-vendor
// token only under /Ads/Providers/ or /Monetization/Providers/. This is the adapter, so
// this is where that knowledge belongs. Nothing outside it names Pi's ad API.
//
// ⛔ ONE SEAM, ONE `AdServices.Current`.
// `AdServices` holds exactly one provider. Two initialisers racing to register would make
// the winner a matter of RuntimeInitialize ordering — i.e. undefined. So the two are made
// MUTUALLY EXCLUSIVE at their gates: this one refuses OUTSIDE Pi Browser, and
// LevelPlayInitializer.Install refuses INSIDE it. Neither can win a race that cannot start.
//
// ⛔ THE FOUR REGISTRATION CONDITIONS, all required, checked in this order:
//   1. inside Pi Browser              WebGLPiPlatform.IsPiBrowserEnvironment
//   2. Pi.init succeeded              the SDK's host channel actually answered
//   3. "ad_network" in nativeFeatures the documented Pi Ad Network feature probe
//   4. the player is Pi-authenticated the docs require an authenticated user for rewarded
// Fail any one and we NEVER call AdServices.Register, so `NullAdService` keeps answering
// `Disabled`, `AdGateService.Offer` hides the button, and the player is never shown a
// promise the platform cannot keep. Registering only AFTER bring-up succeeds is the
// pattern LevelPlayInitializer.cs:255 already sets (register in OnInitSuccess, never before).
//
// ⛔ NOTHING HERE GRANTS ANYTHING. This provider reports an AdShowResult and
// AdGateService owns caps, cooldowns, the ledger and the actual reward. The single
// Earned() in this path is constructed inside PiAdGrantDecision, behind the server check.
//
// ---------------------------------------------------------------------------
// TWO OWNER QUESTIONS ARE OPEN (WO-1320) AND THIS FILE TAKES THE CONSERVATIVE PATH:
//
// (a) CONSENT. Pi publishes no SetGDPRConsent / SetCCPA equivalent, so on Pi the existing
//     prompt would ask a question with nowhere to apply the answer. ASSUMED HERE: do not
//     prompt on Pi (an unanswerable question is worse than none) and defer to Pi Browser's
//     own regime — BUT if a refusal is ALREADY on file from another platform, honour it by
//     not registering at all. Recording a "no" and then serving ads anyway is the one
//     outcome that is indefensible; honouring a "no" we cannot technically implement, by
//     simply not serving, always is. If the owner rules the other way this is the only
//     block that changes.
//
// (b) PLACEMENTS. `ad-placements.json` carries a LevelPlay adUnitId per placement and
//     AdPlacementCatalog warns when one is missing. Pi has NO ad-unit concept: there is one
//     rewarded inventory and `showAd("rewarded")` takes no placement. ASSUMED HERE: the
//     placementId is accepted, TRACED (so per-placement analytics still work) and otherwise
//     ignored — every placement shares one readiness and one presentation. It is NOT
//     mapped to an invented unit id, because a fabricated id would read as configuration.
// ---------------------------------------------------------------------------
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Ads;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Monetization;
using DeNelle.Core.Platform;

namespace DeNelle.Village.Monetization
{
    /// <summary>
    /// <see cref="IAdService"/> over the Pi Developer Ad Network. Registered ONLY inside Pi
    /// Browser, only after Pi.init succeeds, only when the SDK reports the "ad_network" feature,
    /// and only for an authenticated Pioneer.
    /// </summary>
    public sealed class PiAdProvider : MonoBehaviour, IAdService
    {
        private const string Sys = "PiAds";

        /// <summary>The only ad type this provider presents. Interstitials are deliberately absent.</summary>
        private const string RewardedType = "rewarded";

        /// <summary>The documented nativeFeaturesList() token for the Pi Developer Ad Network.</summary>
        private const string AdNetworkFeature = "ad_network";

        // Sign-in is asynchronous and auto-fires at boot, so bring-up waits for it rather than
        // sampling once and giving up. Bounded: if the player never signs in we simply never
        // register, which is the correct outcome and not an error.
        private const float AuthWaitSeconds = 30f;
        private const float AuthPollSeconds = 0.5f;

        // IsRewardedReady is SYNCHRONOUS and Pi's isAdReady is a promise, so readiness is polled
        // and cached. 20s is short enough that a stale "ready" rarely survives to a tap, and long
        // enough not to hammer the SDK all session.
        private const float ReadyPollSeconds = 20f;

        private static PiAdProvider s_instance;

        private IPiPlatform _pi;
        private bool _registered;
        private bool _readyCached;
        private bool _probeInFlight;
        private bool _showing;
        private float _nextProbeAt;
        private AdUnavailableReason _unavailableReason = AdUnavailableReason.NotInitialised;

        public string ProviderName => "PiAds";
        public bool IsInitialised => _registered;

        public bool IsRewardedReady => _registered && !_showing && _readyCached;

        public AdUnavailableReason RewardedUnavailableReason =>
            IsRewardedReady ? AdUnavailableReason.None : _unavailableReason;

        // Pi has no ad-unit concept (see the header, assumption (b)): one rewarded inventory
        // serves every placement, so the placement-aware members answer the same thing. They are
        // NOT collapsed away, because AdGateService calls the placement forms and a provider that
        // answered differently through the two doors would be the bug.
        public bool IsRewardedReadyFor(string placementId) => IsRewardedReady;

        public AdUnavailableReason RewardedUnavailableReasonFor(string placementId) =>
            RewardedUnavailableReason;

        // -----------------------------------------------------------------
        //  Install + bring-up
        // -----------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_instance != null) return;

            // Gate 0 - the ad feature flag owns whether ads exist at all, on every platform.
            if (!FeatureFlags.RewardedAdSkip)
            {
                FlowTrace.Step(Sys, "PI_ADS_INSTALL_SKIPPED reason=flag-off (ff.rewardedadskip). " +
                                    "No Pi ad provider, no SDK call, no consent question.");
                return;
            }

            // Gate 1 - INSIDE PI BROWSER, or nothing. This is also the half of the mutual
            // exclusion that keeps LevelPlay and Pi off each other: outside Pi Browser we are
            // not a candidate for AdServices.Current at all, so there is no race to arbitrate.
            if (!WebGLPiPlatform.IsPiBrowserEnvironment)
            {
                FlowTrace.Step(Sys, "PI_ADS_INSTALL_SKIPPED reason=not-pi-browser. NullAdService keeps " +
                                    "answering Disabled and AdGateService hides every ad offer.");
                return;
            }

            // Assumption (a), the conservative half: a recorded REFUSAL is honoured by not
            // serving. Pi exposes no consent API, so "non-personalised only" is not something we
            // can ask Pi for - and serving anyway against a recorded no is the one outcome with
            // no defence. An UNDECIDED player is not blocked: we do not prompt on Pi (see header).
            if (AdConsentService.IsDecided && AdConsentService.Gdpr == ConsentState.Denied)
            {
                FlowTrace.Warn(Sys, "PI_ADS_INSTALL_SKIPPED reason=consent-denied (" +
                                    AdConsentService.Describe() + "). Pi publishes no consent API, so a " +
                                    "recorded refusal is honoured by serving NO ads rather than by serving " +
                                    "non-personalised ones. OPEN OWNER QUESTION - WO-1320 q1.");
                return;
            }

            var go = new GameObject("PiAdProvider");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<PiAdProvider>();
        }

        private void Start()
        {
            _pi = PiPlatform.Current;
            BringUpAsync().Forget();
        }

        /// <summary>
        /// Gates 2-4, in order, then register. Every refusal is traced with the gate that refused,
        /// because "the ad button never appeared" is otherwise indistinguishable between four
        /// completely different causes on a phone with no debugger.
        /// </summary>
        private async UniTaskVoid BringUpAsync()
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            try
            {
                FlowTrace.Step(Sys, "PI_ADS_BRINGUP_BEGIN inside Pi Browser; checking init -> ad_network -> auth.");

                // Gate 2 - Pi.init. Idempotent per the SDK contract, so calling it here is safe
                // even though PiSignInController also inits: both awaits resolve off the same
                // host-channel answer, and neither can proceed without it.
                bool inited = await _pi.Init(PiEnvironment.Sandbox);
                if (!inited)
                {
                    Refuse("init-failed", "Pi.init did not succeed (env=" + PiEnvironment.Label + ").");
                    return;
                }

                // Gate 3 - the documented feature probe. An empty list (SDK missing, call failed,
                // local timeout) is a "no" exactly like a list without the token: we never
                // register on a probe we could not complete.
                string[] features = await _pi.NativeFeatures();
                if (!Contains(features, AdNetworkFeature))
                {
                    Refuse("no-ad-network-feature",
                        "nativeFeaturesList() does not contain '" + AdNetworkFeature + "' (got [" +
                        string.Join(",", features ?? Array.Empty<string>()) + "]). This is also what an " +
                        "app NOT APPROVED for the Pi Developer Ad Network looks like - WO-1320 q2.");
                    return;
                }

                // Gate 4 - rewarded ads require an AUTHENTICATED user (the Pi docs state this
                // explicitly). Sign-in auto-fires at boot and is async, so wait rather than
                // sample; a player who never signs in simply never gets an ad offer.
                if (!await WaitForSignIn(cancel))
                {
                    Refuse("not-authenticated",
                        "no Pi sign-in after " + AuthWaitSeconds + "s. Rewarded ads require an " +
                        "authenticated Pioneer, so the provider stays unregistered.");
                    return;
                }

                // ---- All four gates passed. Register, exactly as LevelPlayInitializer does on
                // its own init success - never before, so AdServices.Current is never a provider
                // that cannot actually serve.
                _registered = true;
                _unavailableReason = AdUnavailableReason.NoFill;   // until the first probe answers
                AdServices.Register(this);
                FlowTrace.Step(Sys, "PI_ADS_REGISTERED provider=PiAds - AdServices.Current is now the Pi " +
                                    "Ad Network. Placements are traced but not mapped (Pi has no ad units).");

                PreloadRewarded();
            }
            catch (OperationCanceledException)
            {
                // Ordinary teardown (scene change / quit) - not a fault.
            }
            catch (Exception ex)
            {
                Refuse("bringup-threw", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void Refuse(string why, string detail)
        {
            _registered = false;
            _unavailableReason = AdUnavailableReason.NotInitialised;
            FlowTrace.Warn(Sys, "PI_ADS_NOT_REGISTERED reason=" + why + " - " + detail +
                                " NullAdService continues to answer Disabled; every ad-gated offer " +
                                "degrades to its non-ad path.");
        }

        private async UniTask<bool> WaitForSignIn(System.Threading.CancellationToken cancel)
        {
            float waited = 0f;
            while (!PiSignInController.IsSignedIn && waited < AuthWaitSeconds)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(AuthPollSeconds), cancellationToken: cancel);
                waited += AuthPollSeconds;
            }
            return PiSignInController.IsSignedIn;
        }

        private static bool Contains(string[] list, string token)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++)
                if (string.Equals(list[i], token, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // -----------------------------------------------------------------
        //  Readiness (async SDK behind a synchronous property)
        // -----------------------------------------------------------------

        private void Update()
        {
            if (!_registered || _showing || _probeInFlight) return;
            if (Time.realtimeSinceStartup < _nextProbeAt) return;
            _nextProbeAt = Time.realtimeSinceStartup + ReadyPollSeconds;
            ProbeReadyAsync().Forget();
        }

        public void PreloadRewarded() => PreloadRewarded(null);

        public void PreloadRewarded(string placementId)
        {
            if (!_registered || _probeInFlight) return;
            PreloadAsync(placementId).Forget();
        }

        /// <summary>
        /// requestAd + isAdReady. Pi Browser preloads internally, so requestAd is an optimisation
        /// and its refusal is not fatal - the readiness probe that follows is the answer that counts.
        /// </summary>
        private async UniTaskVoid PreloadAsync(string placementId)
        {
            _probeInFlight = true;
            try
            {
                PiAdResult requested = await _pi.RequestAd(RewardedType);
                FlowTrace.Step(Sys, "PI_ADS_PRELOAD placement=" + (placementId ?? "<none>") +
                                    " requestAd=" + requested);
                if (requested.Ok && !PiAdGrantDecision.IsConfirmedResultString(requested.Result))
                    FlowTrace.Warn(Sys, "PI_ADS_PRELOAD unrecognised requestAd result '" +
                                        (requested.Result ?? string.Empty) + "' - logged verbatim; " +
                                        "readiness is decided by isAdReady, not by this string.");

                await RefreshReady();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FlowTrace.Warn(Sys, "PI_ADS_PRELOAD_FAILED " + ex.GetType().Name + ": " + ex.Message);
            }
            finally { _probeInFlight = false; }
        }

        private async UniTaskVoid ProbeReadyAsync()
        {
            _probeInFlight = true;
            try { await RefreshReady(); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FlowTrace.Warn(Sys, "PI_ADS_READY_PROBE_FAILED " + ex.GetType().Name + ": " + ex.Message);
            }
            finally { _probeInFlight = false; }
        }

        private async UniTask RefreshReady()
        {
            bool ready = await _pi.IsAdReady(RewardedType);
            if (ready != _readyCached)
                FlowTrace.Step(Sys, "PI_ADS_READY_CHANGED ready=" + ready);
            _readyCached = ready;
            // NoFill, not LoadFailed: "the network has nothing for you right now" is ORDINARY and
            // temporary, and IAdService's header is emphatic that the two must not be flattened.
            _unavailableReason = ready ? AdUnavailableReason.None : AdUnavailableReason.NoFill;
        }

        // -----------------------------------------------------------------
        //  Presentation
        // -----------------------------------------------------------------

        public void ShowRewarded(Action<AdShowResult> onComplete) => ShowRewarded(null, onComplete);

        public void ShowRewarded(string placementId, Action<AdShowResult> onComplete)
        {
            if (!_registered)
            {
                Settle(onComplete, AdShowResult.Unavailable(AdUnavailableReason.Disabled),
                       "PI_AD_NO_GRANT reason=provider-not-registered");
                return;
            }
            if (_showing)
            {
                // One presentation at a time. A second concurrent showAd would give two callers a
                // claim on one adId, and an adId is a single reward token.
                Settle(onComplete, AdShowResult.Unavailable(AdUnavailableReason.LoadFailed),
                       "PI_AD_NO_GRANT reason=already-presenting");
                return;
            }

            _showing = true;
            ShowAsync(placementId, onComplete).Forget();
        }

        /// <summary>
        /// show -> read the result verbatim -> VERIFY SERVER-SIDE -> decide.
        /// The decision itself is PiAdGrantDecision's; this method only gathers its two inputs and
        /// guarantees the callback fires exactly once.
        /// </summary>
        private async UniTaskVoid ShowAsync(string placementId, Action<AdShowResult> onComplete)
        {
            string place = placementId ?? "<none>";
            try
            {
                FlowTrace.Step(Sys, "PI_AD_SHOW_BEGIN placement=" + place + " type=" + RewardedType);

                PiAdResult ad = await _pi.ShowAd(RewardedType);

                // The verification is attempted ONLY when the client claims a reward AND carries a
                // token. Anything else is already a refusal and there is nothing to ask about -
                // asking would just spend a round-trip to be told no.
                bool serverGranted = false;
                if (ad.ClaimsRewarded && !string.IsNullOrEmpty(ad.AdId))
                {
                    PiAdVerifyResult verdict = await PiAdVerifyEndpoint.VerifyAsync(ad.AdId);
                    serverGranted = verdict.Granted;
                }
                else if (ad.ClaimsRewarded)
                {
                    FlowTrace.Warn(Sys, "PI_ADS_VERIFY_SKIPPED reason=no-adid placement=" + place +
                                        " - AD_REWARDED arrived without an adId, so it cannot be verified " +
                                        "and is refused.");
                }

                AdShowResult result = PiAdGrantDecision.Decide(ad, serverGranted, out string trace);
                Settle(onComplete, result, trace + " placement=" + place);
            }
            catch (OperationCanceledException)
            {
                Settle(onComplete, AdShowResult.Unavailable(AdUnavailableReason.LoadFailed),
                       "PI_AD_NO_GRANT reason=cancelled placement=" + place);
            }
            catch (Exception ex)
            {
                Settle(onComplete, AdShowResult.Unavailable(AdUnavailableReason.LoadFailed),
                       "PI_AD_NO_GRANT reason=threw placement=" + place + " detail=" +
                       ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                _showing = false;
                // The shown ad is spent; ask for the next one so the button's availability is
                // truthful again as soon as possible.
                _readyCached = false;
                _unavailableReason = AdUnavailableReason.NoFill;
                _nextProbeAt = 0f;
            }
        }

        /// <summary>
        /// Fire the caller's callback exactly once, with the decision traced first so the trace
        /// exists even if the callback throws. Guarded, because a throwing call site must never
        /// leave the ad path wedged (CLAUDE.md sec.12).
        /// </summary>
        private static void Settle(Action<AdShowResult> onComplete, AdShowResult result, string trace)
        {
            if (result.Rewarded) FlowTrace.Step(Sys, trace);
            else FlowTrace.Warn(Sys, trace);
            Guard.Try(Sys, "settle Pi rewarded callback", () => onComplete?.Invoke(result));
        }

        private void OnDestroy()
        {
            AdServices.Unregister(this);
            _registered = false;
            if (s_instance == this) s_instance = null;
        }
    }
}
