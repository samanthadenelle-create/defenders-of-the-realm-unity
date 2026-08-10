// =============================================================================
// TutorialSkipUi — WO-1012 §2b kit piece 5: THE ONE SKIP.
// -----------------------------------------------------------------------------
// A single SMALL CORNER control + ONE confirm sheet. Retires BOTH previous skip
// affordances: the big floating "Skip Tutorial" button AND the objective
// banner's inline "Skip >" (the banner itself is retired by ObjectiveStripUi).
// The confirm promises what the checkpoint system already guarantees: progress
// is saved (SeenTutorials persists per step, so a declined skip resumes from
// the same beat on the next launch).
//
// Placement: right screen edge in the FREE band between the ActionRail top
// (y 0.420) and the QueueStatus bottom (y 0.530) — the only unregistered
// right-edge band in HudAreasHost — so it collides with NOTHING: not the
// system/settings corner, not the build HUD's compact Done (the WO-1010 D10
// collision that killed the old (1,1) corner button), and not the F8/dev
// overlays (top edge).
//
// MinTouchPx law: the visible face stays small; an INVISIBLE hit pad carries
// the full touch floor (the padding-never-growth rule). The confirm sheet is
// the kit's shared ConfirmModal, with a post-layout touch-floor pass on its
// two buttons. MVVM: this control owns the chrome + confirm; the caller
// (TutorialFlow.SkipAll) owns what skip MEANS. [Flow:Tutorial] throughout.
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static single-skip corner control for the FTUE. <see cref="Show"/> arms it
    /// with the caller's confirmed-skip intent; <see cref="Hide"/> removes it
    /// (flow finished/torn down). One confirm sheet, never an instant skip.
    /// </summary>
    public sealed class TutorialSkipUi : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;
        private const int CanvasSortOrder = 4310;   // above mask/pointer, beside the strip band
        // The free right-edge band: ActionRail tops at 0.420, QueueStatus bottoms at
        // 0.530 (HudAreasHost) — anchor the pad's TOP at 0.530 and hang it downward.
        private static readonly Vector2 AnchorFraction = new Vector2(1f, 0.530f);
        private const float FaceWidth = 96f;
        private const float FaceHeight = 30f;

        private static TutorialSkipUi _instance;

        private CanvasGroup _group;
        private GameObject _padHost;
        private Action _onConfirmedSkipAll;
        private ElarionUiKit.ConfirmModal _confirm;   // live confirm sheet (null when closed)
        private bool _confirmTouchFloorApplied;
        private bool _visible;
        private float _fadeT;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show the corner Skip, armed with the caller's confirmed skip-all
        /// intent (invoked ONLY after the confirm sheet's Skip).</summary>
        public static void Show(Action onConfirmedSkipAll)
        {
            var s = Ensure();
            s._onConfirmedSkipAll = onConfirmedSkipAll;
            if (!s._visible)
            {
                s._visible = true;
                Diagnostics.FlowTrace.Step("Tutorial", "SkipControl SHOW (the ONE corner skip, WO-1012)");
            }
        }

        /// <summary>Remove the skip control (and any open confirm). Safe when hidden.</summary>
        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;
            _instance._onConfirmedSkipAll = null;
            _instance.CloseConfirm();
            Diagnostics.FlowTrace.Step("Tutorial", "SkipControl HIDE");
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static TutorialSkipUi Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("TutorialSkip");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TutorialSkipUi>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();   // only the pad raycasts
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Invisible MinTouchPx hit pad (padding-never-growth rule) with the small
            // visible face centred inside it.
            _padHost = new GameObject("SkipPad", typeof(RectTransform), typeof(Image), typeof(Button));
            _padHost.transform.SetParent(transform, false);
            var prt = (RectTransform)_padHost.transform;
            prt.anchorMin = prt.anchorMax = AnchorFraction;
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-6f, -2f);
            prt.sizeDelta = new Vector2(ElarionUiKit.MinTouchPx, ElarionUiKit.MinTouchPx);
            var padImg = _padHost.GetComponent<Image>();
            padImg.color = new Color(0f, 0f, 0f, 0f);   // invisible, still raycastable
            var btn = _padHost.GetComponent<Button>();
            btn.targetGraphic = padImg;
            btn.onClick.AddListener(OnSkipTapped);

            // The quiet visible face — obsidian plate, thin gold edge, "Skip" label.
            var face = new GameObject("Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(prt, false);
            var frt = (RectTransform)face.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(FaceWidth, FaceHeight);
            var faceImg = face.GetComponent<Image>();
            faceImg.color = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                      ElarionUiKit.ObsidianFill.b, 0.80f);
            faceImg.raycastTarget = false;

            var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(frt, false);
            var ert = (RectTransform)edge.transform;
            ert.anchorMin = new Vector2(0f, 0f);
            ert.anchorMax = new Vector2(1f, 0f);
            ert.pivot = new Vector2(0.5f, 0f);
            ert.sizeDelta = new Vector2(0f, 2f);
            var eimg = edge.GetComponent<Image>();
            eimg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);
            eimg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(frt, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(label);
            label.fontSize = 14f;
            label.color = ElarionUi.ParchmentDim;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Skip";   // ASCII only
            label.raycastTarget = false;
        }

        /// <summary>The corner control's tap: raise the ONE confirm sheet. Never skips
        /// on the bare tap; a second tap while the sheet is open is a no-op.</summary>
        private void OnSkipTapped()
        {
            if (_onConfirmedSkipAll == null) return;
            if (_confirm != null && _confirm.canvas != null) return;   // sheet already up
            Diagnostics.FlowTrace.Step("Tutorial", "SkipControl tapped — raising the confirm sheet");
            _confirmTouchFloorApplied = false;
            _confirm = ElarionUiKit.BuildConfirmModal(
                "SkipTutorialConfirm",
                "Skip Tutorial",
                "Skip the walkthrough? Your progress is saved.",
                "Skip",
                "Keep Playing",
                onConfirm: () =>
                {
                    Diagnostics.FlowTrace.Step("Tutorial", "SkipControl CONFIRMED — invoking skip-all");
                    var act = _onConfirmedSkipAll;
                    CloseConfirm();
                    act?.Invoke();
                },
                onCancel: () =>
                {
                    Diagnostics.FlowTrace.Step("Tutorial", "SkipControl declined — resuming the walkthrough");
                    CloseConfirm();
                });
        }

        private void CloseConfirm()
        {
            if (_confirm != null && _confirm.canvas != null) Destroy(_confirm.canvas);
            _confirm = null;
        }

        private void Update()
        {
            float dir = _visible ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            _group.alpha = _fadeT * _fadeT * (3f - 2f * _fadeT);
            _group.blocksRaycasts = _visible && _onConfirmedSkipAll != null;
            _group.interactable = _group.blocksRaycasts;

            // MinTouchPx on the CONFIRM sheet's buttons: the kit modal lays its buttons
            // out as panel fractions, so measure one frame after open (rects are valid
            // post-layout) and pad any short button up to the touch floor via sizeDelta
            // (padding, not growth — anchors untouched).
            if (_confirm != null && _confirm.canvas != null && !_confirmTouchFloorApplied)
            {
                bool measured = EnsureTouchFloor(_confirm.confirm) & EnsureTouchFloor(_confirm.cancel);
                if (measured) _confirmTouchFloorApplied = true;
            }
        }

        /// <summary>Pad a button's rect up to the MinTouchPx floor once its layout has
        /// resolved. Returns false while the rect still reads 0 (layout pending).</summary>
        private static bool EnsureTouchFloor(Button b)
        {
            if (b == null) return true;   // no button (single-button sheet) — nothing to do
            var rt = b.transform as RectTransform;
            if (rt == null) return true;
            float h = rt.rect.height;
            if (h <= 0.5f) return false;  // layout not resolved yet — retry next frame
            if (h < ElarionUiKit.MinTouchPx)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y + (ElarionUiKit.MinTouchPx - h));
            return true;
        }
    }
}
