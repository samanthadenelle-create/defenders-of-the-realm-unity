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
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

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
        /// <summary>WO-707: the structure is singleton (one per town) and one already
        /// stands (a BaseLayout record exists). Containers are never singleton.</summary>
        Singleton,
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
        // WO-1252: a two-line next-step toast needs more than 2.2s to be read. Multiline
        // messages get the longer life in ShowRaw.
        private const float DefaultLifeSeconds = 2.2f;
        private const float MultilineLifeSeconds = 3.6f;
        private float _lifeSeconds = DefaultLifeSeconds;
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

            FlowTrace.Warn("BuildToast", $"placement rejected -> '{message}'");
            Debug.Log($"[BuildMode] Placement blocked: {message}");
            GameSfx.PlayBuildDenied();

            ShowRaw(message, DefaultLifeSeconds);
        }

        /// <summary>
        /// Owner ask 2026-07-13 ("after offset tool have a visual on screen"): a NEUTRAL
        /// info toast — no denied buzz, caller-chosen lifetime — for confirmations like
        /// the Orient tool's saved recipe. Same chrome, same one-at-a-time rule.
        /// </summary>
        public static void ShowInfo(string message, float lifeSeconds = 8f)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            FlowTrace.Step("BuildToast", $"info toast -> '{message}'");
            ShowRaw(message, lifeSeconds);
        }

        private static void ShowRaw(string message, float lifeSeconds)
        {
            if (s_active != null) { Object.Destroy(s_active.gameObject); s_active = null; }

            var go = new GameObject("BuildFeedbackToast");
            var ui = go.AddComponent<BuildFeedbackToast>();
            if (lifeSeconds <= DefaultLifeSeconds + 0.01f && message != null && message.IndexOf('\n') >= 0)
                lifeSeconds = MultilineLifeSeconds;
            ui._lifeSeconds = Mathf.Max(1f, lifeSeconds);
            ui.Build(message);
            s_active = ui;
        }

        /// <summary>The default player-facing message for each rejection reason.</summary>
        public static string MessageFor(BuildRejectReason reason)
        {
            // Owner vocabulary 2026-07-24 ("tell me why it's red"): concrete, plain-words
            // reasons. CannotAfford keeps the generic line here; callers with a cost in hand
            // prefer the specialized "Not enough <Resource> (N)" shortfall (ShortfallMessage).
            switch (reason)
            {
                case BuildRejectReason.BadSurface:   return "Ground is too uneven here";
                case BuildRejectReason.Occupied:     return "Too close to another building";
                case BuildRejectReason.BlocksGate:   return "Would block the gate";
                case BuildRejectReason.OutOfBounds:  return "Outside the build area";
                case BuildRejectReason.CannotAfford: return "Not enough resources";
                case BuildRejectReason.Locked:       return "Locked";
                case BuildRejectReason.Singleton:    return "Already built";
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
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;   // never swallow gameplay / build input

            // ── The card (lower-centre, sits clear above the build palette) ─────
            // WO-562: built from the ONE shared obsidian toast (black fill + red "denied" left accent +
            // WebGL-safe Text), replacing the old hand-rolled brown bg + bespoke red bar.
            var parts = ElarionUiKit.ToastCard(transform, ElarionUiKit.ToastTone.Danger,
                                               accentLeft: true, align: TextAnchor.MiddleLeft);
            var crt = (RectTransform)parts.card.transform;
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 200f);   // above the 128px palette tray
            // WO-1252: wrap is deliberate. Count explicit newlines and grow the card so a
            // next-step sentence is never half-cut (the truncation class of the last seven days).
            int lines = 1;
            if (!string.IsNullOrEmpty(message))
            {
                for (int i = 0; i < message.Length; i++)
                    if (message[i] == '\n') lines++;
            }
            float height = Mathf.Max(72f, 24f + lines * 28f);
            crt.sizeDelta = new Vector2(500f, height);
            if (parts.label != null)
            {
                parts.label.text = message;
                parts.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                parts.label.verticalOverflow = VerticalWrapMode.Overflow;
            }

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
