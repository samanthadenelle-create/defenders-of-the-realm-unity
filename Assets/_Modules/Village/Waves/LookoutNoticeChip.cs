// =============================================================================
// LookoutNoticeChip — friendly on-screen lookout tell (WO-1184 bounce).
// -----------------------------------------------------------------------------
// Presentation only. AlertIntelSystem decides WHEN to show; this chip is the
// skin: a small code-built uGUI notice on ElarionUiKit (ToastTone.Info), never
// a UIDocument, never a red bang. UXML does not render in player builds
// (CLAUDE.md §8 / WO-1182). Words carry the meaning (owner is red/green
// colourblind). ASCII-only. Never claims combat is happening offline. Never
// pairs a notice with a shield offer.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Small top-centre lookout notice. Informs, does not alarm. Independent
    /// overlay canvas so it ships on the same substrate as the rest of the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LookoutNoticeChip : MonoBehaviour
    {
        /// <summary>Player-facing title. Words, not a coloured pip.</summary>
        public const string TitleLine = "Lookout notice";

        /// <summary>Factual live-approach line. Timing is filled by the caller.</summary>
        public const string ApproachingPrefix = "Horde approaching -- ";

        // Above HudAreasHost (4000) so the chip is not buried; well below modals.
        private const int SortingOrder = 4020;
        private const float CardWidth = 560f;
        private const float CardHeight = 88f;

        private CanvasGroup _group;
        private ElarionUiKit.ToastParts _parts;
        private bool _visible;

        /// <summary>Build a hidden chip. Caller owns its lifetime.</summary>
        public static LookoutNoticeChip Create()
        {
            var go = new GameObject("LookoutNoticeChip");
            var chip = go.AddComponent<LookoutNoticeChip>();
            chip.Build();
            chip.Hide();
            return chip;
        }

        /// <summary>
        /// Two-line live copy: title + "Horde approaching -- {where} in {secs}s."
        /// Optional force-size (level-3 lookout) is prepended to the body, never
        /// as a panic headline. Does not say the player is under attack.
        /// </summary>
        public static string FormatLiveCopy(string where, int seconds, string forceSizeOrEmpty)
        {
            string whereSafe = string.IsNullOrEmpty(where) ? "the gates" : where;
            int secs = Mathf.Max(1, seconds);
            string body = ApproachingPrefix + whereSafe + " in " + secs + "s.";
            if (!string.IsNullOrEmpty(forceSizeOrEmpty))
                body = forceSizeOrEmpty.Trim() + " " + body;
            return TitleLine + "\n" + body;
        }

        public void Show(string text)
        {
            if (_parts == null) return;
            if (_parts.label != null) _parts.label.text = text ?? TitleLine;
            else
                FlowTrace.Warn("Lookout", "LookoutNoticeChip: ToastCard returned a null label -- notice text will not render.");
            if (!_visible)
            {
                gameObject.SetActive(true);
                if (_group != null) _group.alpha = 1f;
                _visible = true;
                FlowTrace.Step("Lookout", "lookout notice shown (uGUI chip)");
            }
        }

        public void Hide()
        {
            if (!_visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                return;
            }
            _visible = false;
            gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (this == null) return;
            Destroy(gameObject);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // Info tone = parchment + soft gold accent. Not Danger, not a red bang.
            _parts = ElarionUiKit.ToastCard(transform, ElarionUiKit.ToastTone.Info,
                                            accentLeft: true, align: TextAnchor.MiddleLeft);
            var crt = (RectTransform)_parts.card.transform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -96f);
            crt.sizeDelta = new Vector2(CardWidth, CardHeight);
            if (_parts.label != null)
            {
                _parts.label.fontSize = 22;
                _parts.label.alignment = TextAnchor.MiddleLeft;
            }
        }
    }
}
