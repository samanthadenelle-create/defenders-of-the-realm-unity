// =============================================================================
// ObjectiveBannerUi — one-line current-objective strip (WO-T2, spec §2.2).
// -----------------------------------------------------------------------------
// Top-centre, non-blocking, code-built uGUI in the kit language (obsidian glass
// plate + gold accent rule + parchment text — UiStyle/ElarionUi tokens). Replaces
// the UIToolkit TutorialHudOverlay banner as the tutorial's objective surface,
// but is REUSABLE: anything (quests, events) can Show a one-liner.
//
// Optional SKIP affordance for skippable tutorial steps: presentation raises the
// supplied onSkip intent and does nothing else (MVVM — the caller owns what skip
// means). Unscaled-time fade; never blocks gameplay input outside its own small
// Skip button.
//
// SEPARATE from ElarionUiKit by design (do-not-touch this slice); kit-promotion
// candidate: ElarionUiKit.ObjectiveStrip(...) in WO-T5.
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static one-line objective banner. <see cref="Show"/> sets/replaces the
    /// current objective; <see cref="Hide"/> eases it out.
    /// </summary>
    public sealed class ObjectiveBannerUi : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;
        private const int CanvasSortOrder = 4300;    // just above the spotlight dim
        private const float BannerWidth = 620f;
        private const float BannerHeight = 46f;

        private static ObjectiveBannerUi _instance;

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private Button _skipBtn;
        private GameObject _skipHost;
        private Action _onSkip;
        private bool _visible;
        private float _fadeT;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show (or update) the objective line. <paramref name="count"/> &gt; 0
        /// appends " (0/count)"-style progress via <see cref="SetProgress"/>.
        /// <paramref name="onSkip"/> non-null ⇒ the Skip affordance shows and raises it.</summary>
        public static void Show(string text, int count = 0, Action onSkip = null)
        {
            var b = Ensure();
            b._visible = true;
            b._onSkip = onSkip;
            b._baseText = text ?? "";
            b._count = Mathf.Max(0, count);
            b._done = 0;
            b.RefreshLabel();
            if (b._skipHost != null) b._skipHost.SetActive(onSkip != null);
        }

        /// <summary>Update progress on a counted objective (e.g. 1 of 1 towers).</summary>
        public static void SetProgress(int done)
        {
            if (_instance == null) return;
            _instance._done = Mathf.Max(0, done);
            _instance.RefreshLabel();
        }

        /// <summary>Ease the banner out. Safe when not shown.</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            _instance._onSkip = null;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private string _baseText = "";
        private int _count;
        private int _done;

        private void RefreshLabel()
        {
            if (_label == null) return;
            _label.text = _count > 0 ? $"{_baseText}  <color=#C9A54A>({_done}/{_count})</color>" : _baseText;
        }

        private static ObjectiveBannerUi Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ObjectiveBanner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ObjectiveBannerUi>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();   // only the Skip button raycasts
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Plate — obsidian glass strip, top-centre, non-blocking.
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(transform, false);
            var prt = (RectTransform)plate.transform;
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -14f);
            prt.sizeDelta = new Vector2(BannerWidth, BannerHeight);
            var pimg = plate.GetComponent<Image>();
            // Obsidian plate: the single ObsidianFill hue at a translucent alpha so the
            // strip reads as the kit's black-panel language (play area shows through).
            pimg.color = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                   ElarionUiKit.ObsidianFill.b, 0.86f);
            pimg.raycastTarget = false;

            // Gold accent rule along the bottom edge (kit chrome vocabulary).
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(prt, false);
            var rrt = (RectTransform)rule.transform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 2f);
            var rimg = rule.GetComponent<Image>();
            rimg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
            rimg.raycastTarget = false;

            // Objective text.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(prt, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = new Vector2(0.02f, 0f);
            trt.anchorMax = new Vector2(0.86f, 1f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _label = textGo.AddComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_label);
            _label.fontSize = 20f;
            _label.color = ElarionUi.Parchment;
            _label.alignment = TextAlignmentOptions.MidlineLeft;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            _label.raycastTarget = false;

            // Skip affordance (small, right edge) — shown only when the caller
            // supplied an onSkip intent (skippable steps).
            _skipHost = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
            _skipHost.transform.SetParent(prt, false);
            var srt = (RectTransform)_skipHost.transform;
            srt.anchorMin = new Vector2(0.87f, 0.16f);
            srt.anchorMax = new Vector2(0.99f, 0.84f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var simg = _skipHost.GetComponent<Image>();
            simg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.16f);
            _skipBtn = _skipHost.GetComponent<Button>();
            _skipBtn.targetGraphic = simg;
            _skipBtn.onClick.AddListener(() => _onSkip?.Invoke());

            var skipTextGo = new GameObject("Label", typeof(RectTransform));
            skipTextGo.transform.SetParent(srt, false);
            var strt = (RectTransform)skipTextGo.transform;
            strt.anchorMin = Vector2.zero;
            strt.anchorMax = Vector2.one;
            strt.offsetMin = Vector2.zero;
            strt.offsetMax = Vector2.zero;
            var st = skipTextGo.AddComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(st);
            st.fontSize = 15f;
            st.color = ElarionUi.ParchmentDim;
            st.alignment = TextAlignmentOptions.Center;
            st.text = "Skip >";   // ASCII only (no glyphs in TMP)
            st.raycastTarget = false;

            _skipHost.SetActive(false);
        }

        private void Update()
        {
            float dir = _visible ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            float eased = _fadeT * _fadeT * (3f - 2f * _fadeT);
            _group.alpha = eased;
            _group.blocksRaycasts = _visible && _onSkip != null;   // only for the Skip tap
            _group.interactable = _group.blocksRaycasts;
        }
    }
}
