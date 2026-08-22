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
using DeNelle.Core.Ads;
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
        private bool IsCooldownReady =>
            Time.realtimeSinceStartup - _lastShownRealtime >= CooldownSeconds;

        public bool IsAdReady => IsCooldownReady && AdServices.Current.IsRewardedReady;

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
        [Obsolete("MON-1146: synchronous rewarded ads cannot represent an asynchronous SDK outcome. Use AdGateService.Present/RequestAd.")]
        public bool TryShowAd(Action onReward)
        {
            DeNelle.Core.Diagnostics.FlowTrace.Fail("Ads",
                "TryShowAd(sync) is permanently refused (WO-1146). A synchronous return cannot " +
                "report a real network's later earned-reward callback and bypasses AdGateService's " +
                "placement ledger. Move the caller to AdGateService.Present/RequestAd.");
            return false;
#if false
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

            if (!IsCooldownReady || !AdServices.Current.IsRewardedReady)
            {
                AdServices.Current.PreloadRewarded();
                return false;
            }

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
#endif
        }

        /// <summary>
        /// WO-1125 — THE ASYNC CONTRACT. Presents a rewarded ad and reports the outcome through
        /// <paramref name="onComplete"/> WHEN THE AD FINISHES, which is the only shape a real SDK
        /// can honour. Returns true when presentation STARTED (so the caller can show a spinner or
        /// disable the button); it does NOT mean a reward was earned.
        ///
        /// <para>WHY THE SYNCHRONOUS <see cref="TryShowAd(Action)"/> CANNOT BE USED WITH A REAL SDK.
        /// That method returns `granted`, which is only true if the reward callback fired BEFORE it
        /// returned. A real network presents a full-screen ad and calls back seconds later, so
        /// `granted` is ALWAYS false at return time. The player watches the whole ad, earns the
        /// reward, and the caller reports failure - `ManageScreenVM` would say "No ad available
        /// right now." to someone who just sat through thirty seconds of video. The file has warned
        /// about this since it was written ("an override will need this bool contract revisited
        /// (present -> await callback -> grant), not just filled in"); this is that revisit.</para>
        ///
        /// <para>THE GRANT STILL COMES FROM THE SDK, NEVER FROM US. <paramref name="onReward"/> is
        /// invoked only from a genuine earned-reward callback. <paramref name="onComplete"/> reports
        /// what happened either way, so a caller can tell "dismissed early" (no reward, say so
        /// honestly) from "never presented" (offer a retry) - a distinction the bool could not make.</para>
        ///
        /// <para>Cooldown is spent on PRESENTATION, matching the sync path: a refusal must never
        /// lock the player out of retrying.</para>
        /// </summary>
        public bool RequestAd(Action onReward, Action<AdShowResult> onComplete)
            => RequestAd("place.build.skip", onReward, onComplete);

        /// <summary>Placement-aware live contract. Each placement uses its own readiness and unit.</summary>
        public bool RequestAd(string placementId, Action onReward, Action<AdShowResult> onComplete)
        {
            if (!DeNelle.Core.FeatureFlags.RewardedAdSkip)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Once("Ads", "flagoff-async",
                    "RequestAd refused: ff.rewardedadskip is OFF (no ad SDK is wired). " +
                    "No ad is shown and NO reward is granted.");
                onComplete?.Invoke(AdShowResult.Unavailable(AdUnavailableReason.Disabled));
                return false;
            }

            if (!AdServices.Current.IsRewardedReadyFor(placementId))
            {
                AdUnavailableReason why = AdServices.Current.RewardedUnavailableReasonFor(placementId);
                AdServices.Current.PreloadRewarded(placementId);
                onComplete?.Invoke(AdShowResult.Unavailable(why));
                return false;
            }

            // ONE-SHOT GUARD. An SDK that fires its callbacks twice (or a completion racing a
            // dismissal) must never grant twice - the reward here is real player value.
            bool settled = false;
            bool granted = false;

            Action onRewardEarned = () =>
            {
                if (settled) return;
                granted = true;
                DeNelle.Core.Diagnostics.Guard.Try("Ads", "rewarded grant", () => onReward?.Invoke());
            };

            Action<AdShowResult> onSettled = result =>
            {
                if (settled)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Ads",
                        $"rewarded completion fired TWICE (second={result}) - ignored. " +
                        "The grant is one-shot; a double callback can never pay twice.");
                    return;
                }
                settled = true;

                // Trust the SDK's outcome, but never pay for an outcome that did not grant.
                if (result.Rewarded && !granted)
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Ads",
                        "SDK reported Rewarded but no earned-reward callback ever fired - " +
                        "NOT granting. The grant may only come from the reward callback itself.");

                DeNelle.Core.Diagnostics.FlowTrace.Step("Ads",
                    $"rewarded presentation settled: outcome={result} granted={granted}");
                DeNelle.Core.Diagnostics.Guard.Try("Ads", "rewarded onComplete",
                    () => onComplete?.Invoke(result));
            };

            bool presented = false;
            DeNelle.Core.Diagnostics.Guard.Try("Ads", "RewardedAdManager.ShowAdInternal(async)",
                () => { presented = ShowAdInternal(placementId, onRewardEarned, onSettled); });

            // MON-1146: the placement catalog/AdGateService is the sole cooldown authority for
            // the live async path. A second fixed 480s timer here used to override daily chest's
            // authored 0s and harvest's 3600s, coupling unrelated placements together.
            if (!presented && !settled)
                onSettled(AdShowResult.Unavailable(AdUnavailableReason.LoadFailed));

            return presented;
        }

        /// <summary>
        /// WO-1125 — the ASYNC presentation seam a real SDK overrides. Return true when the ad was
        /// actually put on screen. Call <paramref name="onReward"/> ONLY from the SDK's genuine
        /// earned-reward callback (the provider adapter's OnAdRewarded), and call
        /// <paramref name="onComplete"/> exactly once when the ad closes, fails, or is dismissed.
        ///
        /// <para>The base implementation delegates to the legacy synchronous seam so an existing
        /// override keeps working unchanged: it presents, and settles immediately with whatever the
        /// sync path decided. That is correct for a refusal (today's shipping state) and is exactly
        /// what a real SDK must NOT do - which is why the SDK override goes here, not there.</para>
        /// </summary>
        protected virtual bool ShowAdInternal(Action onReward, Action<AdShowResult> onComplete)
            => ShowAdInternal("place.build.skip", onReward, onComplete);

        /// <summary>Placement-aware SDK seam; never substitutes one placement's loaded unit.</summary>
        protected virtual bool ShowAdInternal(string placementId, Action onReward,
                                              Action<AdShowResult> onComplete)
        {
            IAdService ads = AdServices.Current;
            if (!ads.IsRewardedReadyFor(placementId))
            {
                onComplete?.Invoke(AdShowResult.Unavailable(
                    ads.RewardedUnavailableReasonFor(placementId)));
                ads.PreloadRewarded(placementId);
                return false;
            }

            ads.ShowRewarded(placementId, result =>
            {
                if (result.Rewarded) onReward?.Invoke();
                onComplete?.Invoke(result);
            });
            return true;
        }

        /// <summary>
        /// Ad presentation seam. Returns true only when a real rewarded ad was actually presented
        /// by an SDK; the reward itself must come from that SDK's genuine completion callback
        /// (AdMob OnUserEarnedReward / Unity Ads OnUnityAdsShowComplete) by invoking
        /// <paramref name="onReward"/> — NEVER from "we showed it".
        ///
        /// THIS BASE IMPLEMENTATION IS A REFUSAL, NOT A REWARD, AND IT IS THE LEGACY SYNC PATH.
        /// It used to call onReward unconditionally, which made "Watch an ad to skip 10 minutes" a
        /// button that granted the reward instantly, for free, with no ad and no revenue — and once a
        /// real network sits behind it, granting-on-show is fraud against that network.
        ///
        /// <para>WO-1120 — THE "NO SDK EXISTS" JUSTIFICATION IS RETIRED, THE REFUSAL IS NOT. A real
        /// network IS wired now (WO-1125; the vendor is named ONLY inside Providers/, never here). What is
        /// still true is that a SYNCHRONOUS bool cannot express "the player is watching": a real
        /// rewarded ad completes seconds after this returns, so anything granted from this path is
        /// granted BEFORE the ad finished. That is why it still refuses, and why it must keep
        /// refusing even after the flag goes on. The live path is
        /// <see cref="ShowAdInternal(Action, Action{AdShowResult})"/>, which reaches the SDK through
        /// <see cref="IAdService"/> and grants only from the genuine earned-reward callback.</para>
        /// </summary>
        protected virtual bool ShowAdInternal(Action onReward)
        {
            DeNelle.Core.Diagnostics.FlowTrace.Fail("Ads",
                "ShowAdInternal(sync): REFUSED. This is the legacy SYNCHRONOUS seam and it can never " +
                "grant honestly - a real rewarded ad completes AFTER this returns, so any reward paid " +
                "here would be paid before the ad finished, which is fraud against the network. " +
                "A network IS integrated (WO-1125); the async seam RequestAd/ShowAdInternal" +
                "(onReward, onComplete) is the live path and grants only from OnAdRewarded. " +
                "If you reached this line, the CALLER is on the old path - move it, do not fill this in.");
            return false;
        }
    }
}
