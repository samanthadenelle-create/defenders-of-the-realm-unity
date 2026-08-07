// =============================================================================
// RewardedAdManager — DEF-69 (Linear: Monetization Framework). Singleton gate
// for rewarded ads, with NO ad SDK wired in (stubbed for a later platform pass).
// -----------------------------------------------------------------------------
// Lives in DeNelle.Village (the existing DeNelle.Village asmdef, which references
// DeNelle.Core — so it can see DeNelle.Core.Data types if needed later). Follows
// the project's singleton shape (DailyQuestService / EconomyService): static
// Instance, Awake guard, OnDestroy clear, plus a RuntimeInitialize self-bootstrap
// so Instance is never null at runtime without scene authoring.
//
// Reconciliation note vs the broad DEF-69 description: the Linear spec sketched a
// coroutine `IEnumerator ShowAdInternal` driven by a config SO with a 120s
// cooldown, plus a RewardResolver that calls HeartOfTown.Repair / EconomyService.
// This branch's work order narrows the deliverable to a self-contained,
// dependency-free gate: a synchronous TryShowAd(Action) with a fixed 480s
// cooldown and a protected virtual ShowAdInternal hook for the real SDK. We keep
// the spec's INTENT — no SDK, virtual override seam, and cooldown measured with
// Time.realtimeSinceStartup so it survives Time.timeScale changes.
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Rewarded-ad gate. Call <see cref="TryShowAd"/>; it returns TRUE ONLY when a reward was
    /// genuinely earned. It returns false when the feature flag
    /// <see cref="DeNelle.Core.FeatureFlags.RewardedAdSkip"/> is OFF (the shipping state — no ad
    /// SDK exists), while the cooldown is still active, or when <see cref="ShowAdInternal"/>
    /// refuses/fails to present an ad. Cooldown is a fixed 8 minutes, tracked with realtime so it
    /// is unaffected by pausing (Time.timeScale = 0), and is spent only by an ad actually presented.
    /// NOTE for the future SDK pass: real rewarded ads complete ASYNCHRONOUSLY, so an override will
    /// need this bool contract revisited (present -> await callback -> grant), not just filled in.
    /// </summary>
    public class RewardedAdManager : MonoBehaviour
    {
        /// <summary>Minimum seconds between rewarded-ad views (8 minutes).</summary>
        public const float CooldownSeconds = 480f;

        public static RewardedAdManager Instance;

        // Negative sentinel so the first call is always allowed (last view is
        // "long ago"); compared against Time.realtimeSinceStartup.
        private float _lastShownRealtime = -CooldownSeconds;

        /// <summary>True when the cooldown has elapsed and an ad may be shown.</summary>
        public bool IsAdReady =>
            Time.realtimeSinceStartup - _lastShownRealtime >= CooldownSeconds;

        /// <summary>Seconds remaining until the next ad is allowed (0 when ready).</summary>
        public float CooldownRemaining
        {
            get
            {
                float remaining = CooldownSeconds - (Time.realtimeSinceStartup - _lastShownRealtime);
                return remaining > 0f ? remaining : 0f;
            }
        }

        // Self-install so Instance is never null at runtime (no scene authoring
        // required). No-op if a scene-placed manager already exists.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("RewardedAdManager");
            DontDestroyOnLoad(go);
            go.AddComponent<RewardedAdManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// Attempts to show a rewarded ad. Returns TRUE ONLY when <paramref name="onReward"/> was
        /// actually invoked by <see cref="ShowAdInternal"/> — i.e. a reward was genuinely earned.
        /// Returns false (granting nothing) when the RewardedAdSkip flag is OFF, while the cooldown
        /// is still active, or when the presentation refuses/throws. It no longer returns true just
        /// because it dispatched: callers toast "skipped" off this bool, so a true here with no ad
        /// shown is the exact free-reward bug this replaces.
        /// </summary>
        public bool TryShowAd(Action onReward)
        {
            // RELEASE BLOCKER GATE (2026-08-07): the whole rewarded-ad path is flag-gated OFF
            // until a real SDK + WO-912 server-side window validation land. Refusing HERE as well
            // as at every UI build site means a stale caller can never reach the reward.
            if (!DeNelle.Core.FeatureFlags.RewardedAdSkip)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Once("Ads", "flagoff",
                    "TryShowAd refused: ff.rewardedadskip is OFF (no ad SDK is wired). " +
                    "No ad is shown and NO reward is granted. See FeatureFlags.RewardedAdSkip.");
                return false;
            }

            if (!IsAdReady) return false;

            // The reward may ONLY be granted by a genuine completion callback, so the caller's
            // action is wrapped: we record whether it actually fired rather than assuming it did.
            bool granted = false;
            Action onRewardEarned = () =>
            {
                granted = true;
                onReward?.Invoke();
            };

            // A throwing SDK presentation must never silently blank the queue screen (CLAUDE.md 12).
            bool presented = false;
            DeNelle.Core.Diagnostics.Guard.Try("Ads", "RewardedAdManager.ShowAdInternal",
                () => { presented = ShowAdInternal(onRewardEarned); });

            // Cooldown is spent only by an ad that was really presented — a refusal must not
            // lock the player out of a retry once the SDK is wired.
            if (presented) _lastShownRealtime = Time.realtimeSinceStartup;

            return granted;
        }

        /// <summary>
        /// Ad presentation seam. Returns true only when a real rewarded ad was actually presented
        /// by an SDK; the reward itself must come from that SDK's genuine completion callback
        /// (AdMob OnUserEarnedReward / Unity Ads OnUnityAdsShowComplete) by invoking
        /// <paramref name="onReward"/> — NEVER from "we showed it".
        ///
        /// THIS BASE IMPLEMENTATION IS A REFUSAL, NOT A REWARD. No ad SDK exists anywhere in this
        /// project (no AdMob / Unity Ads / ironSource / AppLovin package in Packages/manifest.json,
        /// no ad unit id, no mediation), so there is nothing to show and nothing has been earned.
        /// It used to call onReward unconditionally, which made "Watch an ad to skip 10 minutes" a
        /// button that granted the reward instantly, for free, with no ad and no revenue — and once a
        /// real network sits behind it, granting-on-show is fraud against that network.
        /// </summary>
        // TODO (blocked on FeatureFlags.RewardedAdSkip prerequisites): integrate Unity Ads / AdMob in
        // a platform override of this method. The override presents the ad, returns true, and calls
        // onReward ONLY from the SDK's earned-reward callback.
        protected virtual bool ShowAdInternal(Action onReward)
        {
            DeNelle.Core.Diagnostics.FlowTrace.Fail("Ads",
                "ShowAdInternal: NO ad SDK is wired in this project (no AdMob/Unity Ads/ironSource/" +
                "AppLovin package, no ad unit id, no mediation), so no ad can be presented. " +
                "The reward is WITHHELD on purpose - it may only ever be granted from a real " +
                "OnUserEarnedReward callback, never from having shown something. " +
                "See FeatureFlags.RewardedAdSkip for the two prerequisites (real SDK + WO-912 " +
                "server-side ad-window validation).");
            return false;
        }
    }
}
