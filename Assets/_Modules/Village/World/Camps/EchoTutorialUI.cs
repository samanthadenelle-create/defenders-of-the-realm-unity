// =============================================================================
// EchoTutorialUI — WO-360 mini-tutorial overlay shown when Echo auto-deploys.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// A small, NON-BLOCKING bottom-left overlay that pops the moment Echo (the pet)
// is summoned at an enemy outpost: "Echo will aid you in battle. Tap to dismiss."
// It auto-dismisses after ~3s and can be dismissed early by a tap/click anywhere.
//
// ── Why code-built uGUI (NOT UXML) ───────────────────────────────────────────
//   PIPELINE_STATE §8 / project landmine: UXML UIDocuments come up EMPTY in
//   WebGL player builds. This overlay is built entirely in C# on its own world-
//   space-free Screen-Space-Overlay Canvas so it renders identically in editor +
//   WebGL with no PanelSettings dependency. Self-contained: it creates its Canvas,
//   shows itself, counts down, and Destroys its own GameObject on dismiss.
//
// ── Non-blocking ─────────────────────────────────────────────────────────────
//   The card sits in the bottom-left corner and does NOT raycast-block the rest
//   of the screen (combat / movement keep working underneath); a full-screen
//   transparent tap-catcher behind it accepts the dismiss tap WITHOUT swallowing
//   gameplay input on first frame (a short min-hold guards the entering tap).
//
// Isolation/safety: lives in DeNelle.Village. No external service required; every
// step is null-guarded and try/caught at the call site. ASCII-only strings.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// A self-contained, code-built uGUI bottom-left "Echo will aid you" toast.
    /// Create one via <see cref="Show"/>; it auto-dismisses after
    /// <see cref="AutoDismissSeconds"/> or on the first tap after a short grace.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoTutorialUI : MonoBehaviour
    {
        private const float AutoDismissSeconds = 3.0f;
        // Don't let the tap that triggered the deploy instantly dismiss the toast.
        private const float MinHold = 0.5f;

        // One on-screen at a time (re-summon shouldn't stack toasts).
        private static EchoTutorialUI s_active;

        private CanvasGroup _group;
        private float _shownAt;
        private bool _prevPressed;
        private bool _dismissing;

        /// <summary>
        /// Builds + shows the Echo mini-tutorial toast. Idempotent: if one is already
        /// on screen this is a no-op. <paramref name="petName"/> personalises the line
        /// when provided (the FTUE-named Echo); falls back to "Echo".
        /// </summary>
        public static void Show(string petName = null)
        {
            if (s_active != null) return;

            var go = new GameObject("EchoTutorialUI");
            var ui = go.AddComponent<EchoTutorialUI>();
            ui.Build(petName);
            s_active = ui;
        }

        private void Build(string petName)
        {
            string name = string.IsNullOrWhiteSpace(petName) ? "Echo" : petName.Trim();

            // ── Canvas (Screen-Space Overlay, on top of the HUD) ────────────────
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;   // above the HUD, below hard-modal prompts

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster is required for the card's own tap-catcher to work,
            // but we keep the card small + corner-anchored so it never blocks play.
            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;          // we read taps manually (works in WebGL)
            _group.blocksRaycasts = false;        // never swallow gameplay input

            // ── The card (bottom-left) ──────────────────────────────────────────
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0f, 0f);
            crt.pivot = new Vector2(0f, 0f);
            crt.anchoredPosition = new Vector2(28f, 28f);
            crt.sizeDelta = new Vector2(420f, 96f);

            var bg = card.GetComponent<Image>();
            bg.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;

            // Gold left accent bar (matches the project's prompt styling).
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(card.transform, false);
            var art = (RectTransform)accent.transform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(6f, 0f);
            var ai = accent.GetComponent<Image>();
            ai.color = new Color(0.85f, 0.72f, 0.36f, 1f);
            ai.raycastTarget = false;

            // Text — uGUI Text (no TMP dependency required; WebGL-safe).
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(card.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(20f, 10f);
            lrt.offsetMax = new Vector2(-14f, -10f);

            var text = label.GetComponent<Text>();
            text.text = name + " will aid you in battle. Tap to dismiss.";
            text.color = new Color(0.93f, 0.92f, 0.88f, 1f);
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;

            _shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (_dismissing) return;

            float elapsed = Time.unscaledTime - _shownAt;

            // Auto-dismiss failsafe.
            if (elapsed >= AutoDismissSeconds) { Dismiss(); return; }

            // Tap-to-dismiss after a short grace so the entering tap can't eat it.
            if (elapsed >= MinHold && ConsumeTap()) Dismiss();
        }

        private void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            if (s_active == this) s_active = null;
            Destroy(gameObject);
        }

        // Rising-edge tap/click detection across the new Input System AND legacy
        // Input (activeInputHandler = Both) so it works in every build config.
        private bool ConsumeTap()
        {
            bool pressed = false;

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed) pressed = true;

            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) pressed = true;

            if (!pressed)
            {
                if (UnityEngine.Input.GetMouseButton(0)) pressed = true;
                else if (UnityEngine.Input.touchCount > 0) pressed = true;
            }

            bool edge = pressed && !_prevPressed;
            _prevPressed = pressed;
            return edge;
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }
    }
}
