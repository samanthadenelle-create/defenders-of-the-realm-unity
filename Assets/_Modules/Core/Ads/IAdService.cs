// =============================================================================
// IAdService — the PROVIDER-AGNOSTIC rewarded-ad seam (WO-912 sec.10.5).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ads
//
// WHY THIS EXISTS AT ALL, stated plainly because it is not a nicety:
// WO-912 sec.10.5 requires the ad provider to sit behind a thin seam "REGARDLESS"
// of which network is chosen, because the realistic failure mode here is not a bug
// — it is a PUBLISHER ACCOUNT TERMINATION that forces a provider swap after ship.
// If game code ever calls MaxSdk.* or Advertisement.* directly, that swap becomes a
// rewrite under time pressure, at the worst possible moment. Nothing outside the
// adapter may name a vendor type.
//
// The three finalists all expose the same shape, which is what makes one interface
// honest rather than a lowest-common-denominator fiction:
//     ready-check   MaxSdk.IsRewardedAdReady(id) | rewardedAd.CanShowAd() | LevelPlayRewardedAd.IsAdReady()
//     no-fill       NoFill 204 (vs AdLoadFailed -5001) | NO_FILL 3/9 | 509 Mediation No Fill
//     show + reward callback
//
// NO-FILL IS A FIRST-CLASS OUTCOME, NOT AN ERROR (sec.8.3). "No ads are eligible for
// your device right now" and "the SDK broke" demand different copy: one is honest and
// temporary, the other is a defect. AppLovin is the only finalist that separates them
// cleanly at the source (204 vs -5001), so the seam preserves that distinction even
// where a provider blurs it — an adapter may map UP into a richer answer, never down.
//
// THE UI LEADS WITH AVAILABILITY (sec.8.3): callers ask IsRewardedReady BEFORE they
// offer the button, so the player never taps a promise that fails. A provider without
// a ready-check cannot implement the required UX and is therefore disqualified.
//
// NOTHING HERE INSTALLS OR REFERENCES AN SDK. This file compiles with zero ad
// packages present, which is deliberate: WO-912 D3 blocks SDK installation until a
// written policy answer lands, and the seam must be buildable before that gate opens
// so the wiring is reviewed on its own rather than inside an integration commit.
// =============================================================================

using System;

namespace DeNelle.Core.Ads
{
    /// <summary>Why a rewarded ad could not be presented. Distinct causes, distinct copy.</summary>
    public enum AdUnavailableReason
    {
        /// <summary>An ad is available - not a failure.</summary>
        None = 0,

        /// <summary>
        /// The provider has no eligible ad for this device RIGHT NOW. Ordinary and temporary:
        /// per-user frequency caps retire bidders as the day goes on, so the eligible pool
        /// shrinks (WO-912 sec.10.6). Say "no ads available right now", never "something went
        /// wrong" - this is not an error and must not read as one.
        /// </summary>
        NoFill = 1,

        /// <summary>
        /// The SDK is not initialised yet, or initialisation failed. Distinct from NoFill: this
        /// one is ours to fix and should be visible in diagnostics rather than shrugged off.
        /// </summary>
        NotInitialised = 2,

        /// <summary>
        /// Our own rolling window / cooldown refused it - the player has taken their allowance.
        /// A GAME rule, never the network's. Kept separate so telemetry cannot confuse "we said
        /// no" with "the network said no", which is the difference between the cap binding and
        /// fill binding (WO-912 sec.10.7 - the single most important launch metric).
        /// </summary>
        CappedByGame = 3,

        /// <summary>
        /// The feature is switched off (feature flag, remote config, or an unsupported platform
        /// such as web where V1 hides ad offers entirely - sec.D8).
        /// </summary>
        Disabled = 4,

        /// <summary>
        /// A genuine load/present failure despite eligibility - the network tried and broke.
        /// AppLovin distinguishes this from NoFill as -5001 vs 204; an adapter over a provider
        /// that does not MUST NOT flatten everything to NoFill, or a real outage reads to us as
        /// ordinary market conditions forever.
        /// </summary>
        LoadFailed = 5,

        /// <summary>The player dismissed the ad before earning the reward. Grant nothing.</summary>
        Abandoned = 6,
    }

    /// <summary>How a rewarded presentation ended.</summary>
    public enum AdShowOutcome
    {
        /// <summary>Watched to the reward threshold. THE ONLY value that may grant anything.</summary>
        Rewarded = 0,
        /// <summary>Dismissed early. No reward.</summary>
        Dismissed = 1,
        /// <summary>Never presented - see the reason.</summary>
        NotShown = 2,
    }

    /// <summary>The result of one rewarded presentation attempt.</summary>
    public readonly struct AdShowResult
    {
        public readonly AdShowOutcome Outcome;
        public readonly AdUnavailableReason Reason;

        public AdShowResult(AdShowOutcome outcome, AdUnavailableReason reason)
        {
            Outcome = outcome;
            Reason = reason;
        }

        /// <summary>True ONLY when the player earned the reward. Callers grant on this and nothing else.</summary>
        public bool Rewarded => Outcome == AdShowOutcome.Rewarded;

        public static AdShowResult Earned() => new AdShowResult(AdShowOutcome.Rewarded, AdUnavailableReason.None);
        public static AdShowResult Dismissed() => new AdShowResult(AdShowOutcome.Dismissed, AdUnavailableReason.Abandoned);
        public static AdShowResult Unavailable(AdUnavailableReason why) => new AdShowResult(AdShowOutcome.NotShown, why);

        public override string ToString() =>
            Outcome == AdShowOutcome.Rewarded ? "Rewarded" : $"{Outcome} ({Reason})";
    }

    /// <summary>
    /// The rewarded-ad provider seam. Implemented ONCE per network, in a leaf assembly, behind a
    /// version define - mirroring the SolanaWalletProvider model (WO-754 sec.3.3) so an absent SDK
    /// is a compile-time no-op rather than a broken build.
    ///
    /// Game code depends on THIS and never on a vendor type.
    /// </summary>
    public interface IAdService
    {
        /// <summary>Human-readable provider name for diagnostics ("AppLovinMax", "UnityLevelPlay", "None").</summary>
        string ProviderName { get; }

        /// <summary>True once the SDK has initialised and can be asked for ads.</summary>
        bool IsInitialised { get; }

        /// <summary>
        /// True when a rewarded ad is loaded and presentable RIGHT NOW. The UI asks this BEFORE
        /// offering the button (sec.8.3) - leading with availability instead of failing after a tap.
        /// </summary>
        bool IsRewardedReady { get; }

        /// <summary>
        /// Why a rewarded ad is not currently presentable. <see cref="AdUnavailableReason.None"/>
        /// when <see cref="IsRewardedReady"/> is true. Drives the copy, so it must be specific.
        /// </summary>
        AdUnavailableReason RewardedUnavailableReason { get; }

        /// <summary>Begin loading a rewarded ad. Safe to call repeatedly; implementations coalesce.</summary>
        void PreloadRewarded();

        /// <summary>
        /// Present a rewarded ad. ASYNC BY CONTRACT - <paramref name="onComplete"/> fires once, later,
        /// on the main thread, and is the ONLY authority on whether a reward was earned.
        ///
        /// This shape is not incidental. RewardedAdManager.TryShowAd is currently a SYNCHRONOUS bool,
        /// and its own header (RewardedAdManager.cs:33-34) already flags that real rewarded ads
        /// complete asynchronously and that "an override will need this bool contract revisited
        /// (present -> await callback -> grant), not just filled in". A synchronous bool cannot
        /// express "the player is watching" - anything built on one either grants early (a free
        /// reward) or reports failure for an ad that is about to succeed.
        /// </summary>
        void ShowRewarded(Action<AdShowResult> onComplete);
    }

    /// <summary>Single provider-neutral registration point used by gameplay.</summary>
    public static class AdServices
    {
        private static IAdService s_current = NullAdService.Instance;
        public static IAdService Current => s_current ?? NullAdService.Instance;

        public static void Register(IAdService service) =>
            s_current = service ?? NullAdService.Instance;

        public static void Unregister(IAdService service)
        {
            if (ReferenceEquals(s_current, service)) s_current = NullAdService.Instance;
        }
    }

    /// <summary>
    /// The shipping default: no network, nothing presentable, nothing granted.
    ///
    /// It is a REAL OBJECT rather than a null so every caller takes the same code path whether or
    /// not an SDK exists - a null-checked seam is a seam whose no-provider branch is never exercised
    /// until the day it matters. It also keeps the game honest while WO-912 D3 holds the SDK: the
    /// answer is a clean, specific "Disabled", not an exception and not a silent true.
    /// </summary>
    public sealed class NullAdService : IAdService
    {
        public static readonly NullAdService Instance = new NullAdService();

        public string ProviderName => "None";
        public bool IsInitialised => false;
        public bool IsRewardedReady => false;
        public AdUnavailableReason RewardedUnavailableReason => AdUnavailableReason.Disabled;

        public void PreloadRewarded() { /* no provider - nothing to preload */ }

        public void ShowRewarded(Action<AdShowResult> onComplete)
        {
            // Invoke rather than drop: a caller awaiting a callback that never arrives is a
            // softlocked button. Silence is not a safe default for an async contract.
            onComplete?.Invoke(AdShowResult.Unavailable(AdUnavailableReason.Disabled));
        }
    }
}
