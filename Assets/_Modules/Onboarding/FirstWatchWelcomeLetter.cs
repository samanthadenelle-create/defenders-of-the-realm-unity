using System.Collections;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeNelle.Onboarding
{
    /// <summary>WO-1264 launch letter, shown once at the first safe gameplay moment.</summary>
    public sealed class FirstWatchWelcomeLetter : MonoBehaviour
    {
        private const string SeenKey = "eoa.first-watch-letter.2026-08";
        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _panelHandle;
        private bool _open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
            if (FindAnyObjectByType<FirstWatchWelcomeLetter>() != null) return;
            var go = new GameObject("[FirstWatchWelcomeLetter]");
            DontDestroyOnLoad(go);
            go.AddComponent<FirstWatchWelcomeLetter>();
        }

        private void Awake()
        {
            _panelHandle = PanelManager.Register("First Watch Letter", Close, () => _open);
            StartCoroutine(WaitForSafeMoment());
        }

        private IEnumerator WaitForSafeMoment()
        {
            while (PlayerPrefs.GetInt(SeenKey, 0) != 1)
            {
                var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                string scene = SceneManager.GetActiveScene().name;
                bool gameplayScene = scene != SceneRouter.Title && scene != SceneRouter.HeroSelect &&
                                     scene != SceneRouter.PetSelect;
                if (state != null && state.Onboarded && gameplayScene &&
                    !PanelManager.AnyOpen && !BattleLock.IsInBattle())
                {
                    Show();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
            Destroy(gameObject);
        }

        private void Show()
        {
            _modal = ElarionUiKit.BuildObsidianModal(
                "FirstWatchWelcomeLetterUI", "WELCOME TO THE WATCH",
                new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.84f),
                Close, sortingOrder: 31010);
            if (_modal == null || _modal.canvas == null || _modal.chrome == null)
            {
                FlowTrace.Fail("FirstWatch", "welcome letter could not build; it will retry next session");
                Destroy(gameObject);
                return;
            }

            _open = true;
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                _open = false;
                Destroy(_modal.canvas);
                _modal = null;
                StartCoroutine(WaitForSafeMoment());
                return;
            }

            var content = _modal.chrome.content.transform;
            var letterTexture = Resources.Load<Texture2D>("UI/Onboarding/welcome-letter-complete-v1");
            if (letterTexture == null)
            {
                FlowTrace.Fail("FirstWatch", "welcome letter art missing; refusing blank modal");
                Close();
                return;
            }

            var artGo = new GameObject("WelcomeLetterArt", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            artGo.transform.SetParent(content, false);
            var artRect = (RectTransform)artGo.transform;
            artRect.anchorMin = new Vector2(0.04f, 0.22f);
            artRect.anchorMax = new Vector2(0.96f, 0.98f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            var art = artGo.GetComponent<RawImage>();
            art.texture = letterTexture;
            art.color = Color.white;
            art.raycastTarget = false;
            var fitter = artGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)letterTexture.width / letterTexture.height;

            ElarionUiKit.BuildObsidianButton(content, "Hold the Line",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.19f), Close);

            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            FlowTrace.Step("FirstWatch", "welcome letter shown (campaign code withheld)");
        }

        private void Close()
        {
            _open = false;
            PanelManager.NotifyClosed(_panelHandle);
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
            _modal = null;
            Destroy(gameObject);
        }
    }
}
