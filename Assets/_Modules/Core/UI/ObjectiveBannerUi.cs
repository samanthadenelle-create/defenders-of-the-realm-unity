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
        // Persistent "Skip Tutorial" affordance — completes the WHOLE FTUE (distinct
        // from the per-step _skipHost Skip>). Shown whenever a caller supplies onSkipAll
        // and confirmed through a lightweight kit confirm before firing (never on accident).
        private Button _skipAllBtn;
        private GameObject _skipAllHost;
        private Action _onSkipAll;
        private bool _visible;
        private float _fadeT;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show (or update) the objective line. <paramref name="count"/> &gt; 0
        /// appends " (0/count)"-style progress via <see cref="SetProgress"/>.
        /// <paramref name="onSkip"/> non-null ⇒ the Skip affordance shows and raises it.</summary>
        public static void Show(string text, int count = 0, Action onSkip = null, Action onSkipAll = null)
        {
            var b = Ensure();
            b._visible = true;
            b._onSkip = onSkip;
            b._onSkipAll = onSkipAll;
            b._baseText = text ?? "";
            b._count = Mathf.Max(0, count);
            b._done = 0;
            b.RefreshLabel();
            if (b._skipHost != null) b._skipHost.SetActive(onSkip != null);
            if (b._skipAllHost != null) b._skipAllHost.SetActive(onSkipAll != null);
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
            _instance._onSkipAll = null;
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
            // Owner 2026-07-16: the step-instruction strip sat ON TOP of the compass — both
            // parked top-centre. The HUD-kit compass lives in the Status "crown" area, which
            // HudAreasHost anchors at screen-fraction y 0.845..0.990 (top ~15%). Anchor this
            // banner to that crown's exact LOWER edge (0.845) so it hangs just BELOW the compass
            // in both orientations (a screen fraction is the same physical y on this
            // ConstantPixelSize canvas as on the kit's ScaleWithScreenSize canvas).
            prt.anchorMin = new Vector2(0.5f, 0.845f);
            prt.anchorMax = new Vector2(0.5f, 0.845f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -6f);
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

            // Persistent "Skip Tutorial" affordance — top-right SCREEN corner (child of the
            // canvas root, not the strip), so it stays put while the tutorial runs.
            // Presentation-separation law (MVVM): a DUMB, kit-styled view from the Obsidian
            // button factory — frame/face/label ink/font/hover feedback all live in the kit —
            // whose ONLY injected dependency is the onClick Action. That Action stays
            // RequestSkipAll: presentation raises the kit confirm and only then invokes the
            // caller's _onSkipAll (→ TutorialFlow.SkipAll). No hand-rolled Image/Button/trim/
            // label assembly. Style1/Gray = the standardized quiet obsidian HUD face.
            _skipAllBtn = ElarionUiKit.BuildObsidianButton(
                transform, "Skip Tutorial",                    // ASCII only (no glyphs in TMP)
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                onClick: RequestSkipAll);
            _skipAllHost = _skipAllBtn != null ? _skipAllBtn.gameObject : null;
            if (_skipAllHost != null)
            {
                var sart = (RectTransform)_skipAllHost.transform;
                // Owner 2026-07-16: "Skip Tutorial should not be over top the Menu button." The HUD
                // Menu/gear lives in the top-right corner; drop Skip Tutorial DOWN the right edge below
                // it (clear of Menu above and the ability rail lower) so the two never overlap. The kit
                // anchored at (1,1) with zero offsets; collapse that to the HUD-consistent fixed box.
                sart.pivot = new Vector2(1f, 1f);
                sart.anchoredPosition = new Vector2(-14f, -116f);   // clear the taller box under Menu
                sart.sizeDelta = new Vector2(248f, 72f);            // HUD-consistent (owner 2026-07-16)
                ElarionUiKit.ClampMinTouch(_skipAllBtn);            // kit touch floor guard (never shrinks)

                _skipAllHost.SetActive(false);
            }
        }

        /// <summary>The persistent Skip-Tutorial tap: presentation raises a lightweight
        /// confirm (kit ConfirmModal) and only invokes the caller's onSkipAll intent on
        /// confirm — MVVM: the banner owns the confirm chrome, the caller owns what skip
        /// means (TutorialFlow.SkipAll). Never fires on an accidental single tap.</summary>
        private void RequestSkipAll()
        {
            var skip = _onSkipAll;
            if (skip == null) return;

            ElarionUiKit.ConfirmModal modal = null;
            modal = ElarionUiKit.BuildConfirmModal(
                "SkipTutorialConfirm",
                "Skip Tutorial",
                "Skip the tutorial? You'll keep everything it grants.",
                "Skip",
                "Keep Playing",
                onConfirm: () => { if (modal != null && modal.canvas != null) Destroy(modal.canvas); skip(); },
                onCancel:  () => { if (modal != null && modal.canvas != null) Destroy(modal.canvas); });
        }

        // WO-795 (16-panel audit): while ANY arbiter-tracked modal owns the screen
        // (Store, Cosmetic, Jukebox, Rumor Board, Hot-Swap, Bug Report ...) the coach
        // banner SUPPRESSES (fades out + drops raycasts) so it never crosses a modal's
        // header, and RESTORES when the modal closes. Caller state (_visible, _onSkip,
        // progress) is untouched -- the banner picks back up exactly where it was.
        private bool _modalSuppressed;

        private void Update()
        {
            bool modal = PanelManager.AnyOpen;
            if (modal != _modalSuppressed)
            {
                _modalSuppressed = modal;
                // Trace only when the change is player-visible (a shown banner); a modal
                // toggling while no objective is up would be per-open log noise.
                if (_visible)
                    DeNelle.Core.Diagnostics.FlowTrace.Step("UI", modal
                        ? "ObjectiveBanner suppressed - modal open ('" + (PanelManager.OpenPanelName ?? "?") + "')"
                        : "ObjectiveBanner restored - modal closed");
            }
            bool shown = _visible && !_modalSuppressed;
            float dir = shown ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            float eased = _fadeT * _fadeT * (3f - 2f * _fadeT);
            _group.alpha = eased;
            // Raycast only for a live tap target: the per-step Skip> or the persistent Skip Tutorial.
            _group.blocksRaycasts = shown && (_onSkip != null || _onSkipAll != null);
            _group.interactable = _group.blocksRaycasts;
        }
    }
}
