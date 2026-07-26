// =============================================================================
// DungeonToastView — WO-770.7 (fixes D13/D14): surface previously-SILENT dungeon
// feedback as a brief, non-blocking toast — a reached checkpoint, a crafted item,
// Bryn's greeting. Before this the events (`Checkpoint.ToastRequested`,
// `CraftingPedestal.ToastRequested`, Bryn's dialogue) fired into the void (0 subs).
// -----------------------------------------------------------------------------
// CODE-BUILT uGUI on its OWN Screen-Space-Overlay Canvas — NOT UXML — because UXML
// UIDocuments come up EMPTY in player builds (CLAUDE.md §8 / PIPELINE_STATE landmine).
// Built from the ONE shared obsidian toast chrome (ElarionUiKit.ToastCard, neutral
// Info tone) so it matches every other toast. Mirrors BuildFeedbackToast: one on
// screen at a time (rapid toasts replace, never stack), auto-fades, never
// raycast-blocks gameplay underneath. `Show(string)` is UnityEvent<string>-shaped so
// a checkpoint/pedestal `ToastRequested` can `AddListener(DungeonToastView.Show)`.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A self-contained, code-built uGUI toast for dungeon feedback. Pop it via
    /// <see cref="Show(string)"/>; it fades + auto-dismisses and only one shows at a
    /// time. Never raycast-blocks gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonToastView : MonoBehaviour
    {
        private const float DefaultLifeSeconds = 3.0f;
        private const float FadeSeconds = 0.5f;

        // One on-screen at a time (a rapid checkpoint+craft+greeting run replaces, never stacks).
        private static DungeonToastView s_active;

        private CanvasGroup _group;
        private float _lifeSeconds = DefaultLifeSeconds;
        private float _shownAt;

        /// <summary>
        /// Pop a neutral dungeon toast with <paramref name="message"/> (no-op on blank).
        /// Shaped for <c>UnityEvent&lt;string&gt;.AddListener</c> so checkpoint / crafting
        /// <c>ToastRequested</c> can subscribe it directly.
        /// </summary>
        public static void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            FlowTrace.Step("DungeonToast", $"toast -> '{message}'");

            if (s_active != null) { Object.Destroy(s_active.gameObject); s_active = null; }

            var go = new GameObject("DungeonToast");
            var ui = go.AddComponent<DungeonToastView>();
            ui.Build(message);
            s_active = ui;
        }

        private void Build(string message)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 720;   // above the dungeon HUD

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;   // never swallow gameplay / interact input

            // The ONE shared obsidian toast chrome (neutral Info tone), seated TOP-centre so it
            // reads as a passive notification clear of the hero + the bottom Interact button.
            var parts = ElarionUiKit.ToastCard(transform, ElarionUiKit.ToastTone.Info,
                                               accentLeft: true, align: TextAnchor.MiddleLeft);
            var crt = (RectTransform)parts.card.transform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -180f);   // just below the top edge
            crt.sizeDelta = new Vector2(560f, 84f);
            if (parts.label != null) parts.label.text = message;

            _shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _shownAt;
            if (elapsed >= _lifeSeconds) { Destroy(gameObject); return; }

            // Fade out over the final FadeSeconds.
            float fadeStart = _lifeSeconds - FadeSeconds;
            if (_group != null && elapsed > fadeStart)
                _group.alpha = Mathf.Clamp01((_lifeSeconds - elapsed) / FadeSeconds);
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }
    }
}
