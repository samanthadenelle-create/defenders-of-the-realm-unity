// =============================================================================
// BuildFeedbackToast — WO-394: surface WHY a build placement was rejected.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Before WO-394 a blocked build click did NOTHING — no message, no sound, no log
// the player could see. The placement gate (BuildModeController.IsValidPlacement /
// TowerPlacementSystem.CanPlace) collapsed five distinct rejection reasons into a
// single bool and the place loops simply skipped the place when it was false, so
// the player had no way to tell why ("not enough wood? no space? locked?").
//
// This is the visible half of the fix: a small, NON-BLOCKING toast popped just
// above the build palette with the specific reason ("Not enough Wood", "No space
// here", "Blocks the gate", "Can't build there"). It auto-dismisses after a couple
// of seconds and replaces any toast already on screen, so rapid invalid clicks
// don't stack. A short denied "buzz" (GameSfx.PlayBuildDenied) and the ghost's
// existing red tint round out the feedback.
//
// CODE-BUILT uGUI on its own Screen-Space-Overlay Canvas — NOT UXML — because UXML
// UIDocuments come up EMPTY in WebGL player builds (PIPELINE_STATE landmine). Built
// the same way as GearGrantToast so it is fresh-clone-safe + WebGL-safe (legacy
// runtime font, no scene wiring, every step null-guarded, ASCII-only strings).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// The specific reason a build placement was rejected. <see cref="BuildFeedbackToast"/>
    /// maps each to a short player-facing message; the controllers pick the reason at the
    /// point of rejection so the message is never a generic "can't build".
    /// </summary>
    public enum BuildRejectReason
    {
        /// <summary>Default / unknown — generic "Can't build there".</summary>
        Generic = 0,
        /// <summary>Surface is not flat buildable ground (slope, rooftop, water).</summary>
        BadSurface,
        /// <summary>Footprint overlaps another structure or occupied cells.</summary>
        Occupied,
        /// <summary>Footprint would block the gate spawn-to-Heart lane.</summary>
        BlocksGate,
        /// <summary>Out of the buildable map bounds.</summary>
        OutOfBounds,
        /// <summary>Player can't afford the resource cost.</summary>
        CannotAfford,
        /// <summary>A prerequisite tier / skill / unlock is not met.</summary>
        Locked,
    }

    /// <summary>
    /// A self-contained, code-built uGUI toast that tells the player WHY a build
    /// placement was rejected (WO-394). Pop it via <see cref="Show(BuildRejectReason)"/>
    /// or <see cref="Show(string)"/>; it fades + auto-dismisses and only one shows at
    /// a time. Never raycast-blocks gameplay underneath.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildFeedbackToast : MonoBehaviour
    {
        private const float LifeSeconds = 2.2f;
        private const float FadeSeconds = 0.45f;

        // One on-screen at a time (rapid invalid clicks replace, never stack).
        private static BuildFeedbackToast s_active;

        private CanvasGroup _group;
        private float _shownAt;

        /// <summary>
        /// Map a <see cref="BuildRejectReason"/> to a short, specific player message and
        /// pop the toast (plus the denied buzz). Use the <see cref="Show(string)"/>
        /// overload when the caller already has a tailored string (e.g. the exact missing
        /// resource "Not enough Wood (25)").
        /// </summary>
        public static void Show(BuildRejectReason reason)
        {
            Show(MessageFor(reason));
        }

        /// <summary>
        /// Pop the reason toast with an explicit message and play the denied buzz. Logs the
        /// reason too so playtest/dev builds show it in the console (WO-394 acceptance).
        /// Idempotent: replaces any toast already on screen.
        /// </summary>
        public static void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) message = "Can't build there";

            Debug.Log($"[BuildMode] Placement blocked: {message}");
            GameSfx.PlayBuildDenied();

            if (s_active != null) { Object.Destroy(s_active.gameObject); s_active = null; }

            var go = new GameObject("BuildFeedbackToast");
            var ui = go.AddComponent<BuildFeedbackToast>();
            ui.Build(message);
            s_active = ui;
        }

        /// <summary>The default player-facing message for each rejection reason.</summary>
        public static string MessageFor(BuildRejectReason reason)
        {
            switch (reason)
            {
                case BuildRejectReason.BadSurface:   return "Can't build there";
                case BuildRejectReason.Occupied:     return "No space here";
                case BuildRejectReason.BlocksGate:   return "Would block the gate";
                case BuildRejectReason.OutOfBounds:  return "Outside the build area";
                case BuildRejectReason.CannotAfford: return "Not enough resources";
                case BuildRejectReason.Locked:       return "Locked";
                default:                             return "Can't build there";
            }
        }

        private void Build(string message)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 720;   // above the build palette + HUD

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;   // never swallow gameplay / build input

            // ── The card (lower-centre, sits clear above the build palette) ─────
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 200f);   // above the 128px palette tray
            crt.sizeDelta = new Vector2(440f, 72f);

            var bg = card.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.05f, 0.06f, 0.94f);
            bg.raycastTarget = false;

            // Red left accent bar — the "denied" cue.
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(card.transform, false);
            var art = (RectTransform)accent.transform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(7f, 0f);
            var ai = accent.GetComponent<Image>();
            ai.color = new Color(0.85f, 0.22f, 0.22f, 1f);
            ai.raycastTarget = false;

            // Text — uGUI Text (no TMP dependency; WebGL-safe).
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(card.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(22f, 8f);
            lrt.offsetMax = new Vector2(-16f, -8f);

            var text = label.GetComponent<Text>();
            text.text = message;
            text.color = new Color(0.96f, 0.90f, 0.88f, 1f);
            text.fontSize = 24;
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
            float elapsed = Time.unscaledTime - _shownAt;
            if (elapsed >= LifeSeconds) { Destroy(gameObject); return; }

            // Fade out over the final FadeSeconds.
            float fadeStart = LifeSeconds - FadeSeconds;
            if (_group != null && elapsed > fadeStart)
                _group.alpha = Mathf.Clamp01((LifeSeconds - elapsed) / FadeSeconds);
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }
    }
}
