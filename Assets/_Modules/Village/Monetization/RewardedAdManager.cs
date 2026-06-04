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
    /// Rewarded-ad gate. Call <see cref="TryShowAd"/>; it returns false while the
    /// cooldown is still active, otherwise it records the view time and runs the
    /// (stubbed) ad via <see cref="ShowAdInternal"/>, which invokes the reward
    /// callback. Cooldown is a fixed 8 minutes, tracked with realtime so it is
    /// unaffected by pausing (Time.timeScale = 0).
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
        /// Attempts to show a rewarded ad. Returns false (and does nothing) if the
        /// cooldown is still active. Otherwise records the view time and dispatches
        /// to <see cref="ShowAdInternal"/>, which is responsible for invoking
        /// <paramref name="onReward"/> on successful completion.
        /// </summary>
        public bool TryShowAd(Action onReward)
        {
            if (!IsAdReady) return false;
            _lastShownRealtime = Time.realtimeSinceStartup;
            ShowAdInternal(onReward);
            return true;
        }

        /// <summary>
        /// Stub ad presentation. Immediately grants the reward (no SDK). Override
        /// in a platform-specific subclass to present a real rewarded ad and only
        /// invoke <paramref name="onReward"/> on genuine completion.
        /// </summary>
        // TODO: integrate Unity Ads / AdMob SDK in a platform override of this method.
        protected virtual void ShowAdInternal(Action onReward)
        {
            onReward?.Invoke();
        }
    }
}
