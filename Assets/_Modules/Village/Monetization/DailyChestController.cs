using System;
using System.Collections;
using DeNelle.Core;
using DeNelle.Core.Ads;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeNelle.Village.Monetization
{
    /// <summary>Once-per-UTC-day retention reward shown only after onboarding.</summary>
    public sealed class DailyChestController : MonoBehaviour
    {
        public const string PlacementId = "place.daily.chest";
        private const int BaseGold = 500;

        private static DailyChestController s_instance;
        private static bool s_tutorialJustFinished;

        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _handle;
        private TMP_Text _status;
        private Button _doubleButton;
        private bool _offeredThisSession;
        private bool _claiming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("DailyChestController");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DailyChestController>();
        }

        public static void NotifyTutorialFinished()
        {
            s_tutorialJustFinished = true;
            if (s_instance != null) s_instance.StartCoroutine(s_instance.OfferAfterDelay());
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(OfferAfterDelay());
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _offeredThisSession = false;
            StartCoroutine(OfferAfterDelay());
        }

        private IEnumerator OfferAfterDelay()
        {
            yield return new WaitForSecondsRealtime(s_tutorialJustFinished ? 0.75f : 1.25f);
            s_tutorialJustFinished = false;
            TryOffer();
        }

        private void Update()
        {
            if (_doubleButton == null || _claiming) return;
            bool ready = FeatureFlags.RewardedAdSkip && AdServices.Current.IsRewardedReadyFor(PlacementId);
            _doubleButton.interactable = ready;
            if (!ready) AdServices.Current.PreloadRewarded(PlacementId);
        }

        private void TryOffer()
        {
            if (_offeredThisSession || _modal != null || !HubScenes.IsHub(SceneManager.GetActiveScene().name)) return;
            var state = GameStateService.Instance?.State;
            if (state == null || !state.Onboarded || string.Equals(state.DailyChestDayKey, TodayKey(), StringComparison.Ordinal)) return;

            _offeredThisSession = true;
            Build();
            AdServices.Current.PreloadRewarded(PlacementId);
        }

        private void Build()
        {
            _handle = PanelManager.Register("Daily Chest", Close, IsShowing);
            _modal = ElarionUiKit.BuildObsidianModal("DailyChestUI", "Daily Chest",
                new Vector2(0.10f, 0.18f), new Vector2(0.90f, 0.82f), Close, 31010,
                medallionIcon: "icon_chest");
            Transform content = _modal.chrome.content.transform;

            ElarionUiKit.Label(content,
                "Your realm has prepared today's supplies. Claim 500 Gold now, or optionally watch one ad to claim 1,000 Gold.",
                0.50f, 0.88f, ElarionUi.Parchment, 31, TextAlignmentOptions.TopLeft);

            _status = ElarionUiKit.Label(content,
                "The free chest is always available.", 0.36f, 0.48f,
                ElarionUi.ParchmentDim, 25, TextAlignmentOptions.Center);

            ElarionUiKit.BuildObsidianButton(content, "Claim 500 Gold",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.06f, 0.10f), new Vector2(0.48f, 0.28f), () => Claim(BaseGold, "free"));

            _doubleButton = ElarionUiKit.BuildObsidianButton(content, "Watch Ad: Claim 1,000",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.52f, 0.10f), new Vector2(0.94f, 0.28f), WatchForDouble);

            if (!PanelManager.NotifyOpened(_handle)) Close();
        }

        private void WatchForDouble()
        {
            if (_claiming) return;
            if (!FeatureFlags.RewardedAdSkip || !AdServices.Current.IsRewardedReadyFor(PlacementId))
            {
                SetStatus("Ad unavailable right now. You can still claim 500 Gold.");
                AdServices.Current.PreloadRewarded(PlacementId);
                return;
            }

            _claiming = true;
            SetStatus("Opening optional ad...");
            AdServices.Current.ShowRewarded(PlacementId, result =>
            {
                _claiming = false;
                if (result.Rewarded) Claim(BaseGold * 2, "rewarded_double");
                else SetStatus("No reward was consumed. Claim 500 Gold, or try the ad again later.");
            });
        }

        private void Claim(int gold, string path)
        {
            if (_claiming) return;
            var service = GameStateService.Instance;
            var state = service?.State;
            if (state == null || string.Equals(state.DailyChestDayKey, TodayKey(), StringComparison.Ordinal))
            {
                Close();
                return;
            }

            if (EconomyService.Instance == null)
            {
                SetStatus("The realm ledger is still loading. Please try again in a moment.");
                return;
            }

            _claiming = true;
            state.DailyChestDayKey = TodayKey();
            EconomyService.Instance.AddCoins(gold);
            service.Save();
            FlowTrace.Step("DailyChest", $"claimed +{gold} Gold path={path} day={state.DailyChestDayKey}");
            Close();
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        private bool IsShowing() => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy;

        private void Close()
        {
            PanelManager.NotifyClosed(_handle);
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
            _modal = null;
            _status = null;
            _doubleButton = null;
        }

        private static string TodayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");
    }
}
