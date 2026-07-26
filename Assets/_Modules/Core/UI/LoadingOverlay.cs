// =============================================================================
// LoadingOverlay — a reusable "hang on, we're loading" screen (owner felt-test
// 2026-07-24: "when I click Load Default / Design My Own there's a long delay —
// add a loading screen").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE PROBLEM: the founding choice (FoundingChoiceController) tears its overlay
// DOWN and then fires SceneRouter.GoCastle — a fade-load of the big Castle/hub
// scene (which also streams OuterWorld additively). Because the founding overlay
// is destroyed FIRST, the screen goes blank/frozen for the whole scene load. This
// overlay fills that gap: a dark cover + a centred message + a small spinner that
// SURVIVES the scene load (DontDestroyOnLoad) and auto-dismisses once the first
// hub frame has rendered.
//
// CODE-BUILT uGUI on its OWN ScreenSpaceOverlay Canvas — NOT UXML (UIDocuments come
// up EMPTY in player builds, PIPELINE_STATE Section 8). Built the same way as
// BuildFeedbackToast (Canvas + CanvasScaler + CanvasGroup on its own root object)
// so it is fresh-clone-safe, WebGL-safe, and needs no scene wiring. One at a time.
// ASCII-only structural strings; the caller passes the display message.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// A self-contained, code-built uGUI loading cover on its own ScreenSpaceOverlay
    /// canvas. Call <see cref="Show(string)"/> immediately BEFORE kicking off a scene
    /// load; it <c>DontDestroyOnLoad</c>s itself so it stays up while the scene loads,
    /// then auto-dismisses a short settle after the first scene load (with a
    /// minimum-show floor to avoid a flash and a hard max-show failsafe so it can
    /// never stick). Only one shows at a time. <see cref="Hide"/> dismisses early.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingOverlay : MonoBehaviour
    {
        // ── Timing (all in UNSCALED seconds so a paused timescale never freezes us) ──
        /// <summary>Minimum time the overlay stays up, so a fast load doesn't just flash.</summary>
        private const float MinShowSeconds = 0.4f;
        /// <summary>Settle after the first scene load, to let the first hub frame render.</summary>
        private const float SettleSeconds = 0.5f;
        /// <summary>Hard failsafe: never stick longer than this even if no scene ever loads.</summary>
        private const float MaxShowSeconds = 30f;
        /// <summary>Fade-out duration when dismissing.</summary>
        private const float FadeSeconds = 0.3f;
        /// <summary>Spinner spin rate (degrees / second).</summary>
        private const float SpinDegPerSec = 220f;

        // One on-screen at a time.
        private static LoadingOverlay s_active;

        private CanvasGroup _group;
        private RectTransform _spinner;
        private float _spinAngle;

        private float _shownAt;          // unscaled time the overlay was shown
        private bool _sceneLoaded;       // has a scene loaded since Show?
        private float _sceneLoadedAt;    // unscaled time of that first scene load

        private bool _dismissing;
        private float _dismissStartedAt;

        /// <summary>
        /// Put the loading cover up (idempotent: a second call while one is live is a
        /// no-op). Survives the next scene load and auto-dismisses once the first hub
        /// frame has settled. Call this immediately BEFORE starting the scene load.
        /// </summary>
        public static void Show(string message = "Loading your realm...")
        {
            if (s_active != null) return;   // one at a time

            if (string.IsNullOrWhiteSpace(message)) message = "Loading your realm...";

            FlowTrace.Step("LoadingOverlay", $"Show '{message}'");

            var go = new GameObject("LoadingOverlay");
            var ui = go.AddComponent<LoadingOverlay>();
            ui.Build(message);
            Object.DontDestroyOnLoad(go);   // survive the scene load
            s_active = ui;

            SceneManager.sceneLoaded += ui.OnSceneLoaded;
        }

        /// <summary>Dismiss the loading cover early (fades out, then destroys). No-op if none is up.</summary>
        public static void Hide()
        {
            if (s_active != null) s_active.BeginDismiss();
        }

        private void Build(string message)
        {
            // Own ScreenSpaceOverlay canvas, sorted just BELOW the modal band (31000) so it
            // covers all normal game UI during the load but is deliberately NOT a top-band
            // "modal": a transient DontDestroyOnLoad loading cover is not a user-dismissable
            // panel and must NOT be owned by the scene-scoped back-button / battle-lock arbiter
            // (which would tear down across the very scene load this cover exists to span, and
            // whose [modal-registration] contract only applies to real >=31000 modals). During
            // the founding->hub transition no modal is open (the founding panel is destroyed
            // first), so 30990 fully covers the screen.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30990;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = true;   // cover the load (no stray taps to whatever is behind)

            // Dark, near-opaque backdrop (raycast-blocking full cover).
            ElarionUiKit.AddImage(transform, "LoadingBg", Vector2.zero, Vector2.one,
                new Color(0.02f, 0.02f, 0.03f, 0.96f), rounded: false);

            // Centred message (parchment on the dark cover, no meaning in colour).
            var label = ElarionUiKit.Label(transform, message,
                0.42f, 0.50f, ElarionUi.Parchment, ElarionUi.FontBody,
                TMPro.TextAlignmentOptions.Center, 0.08f, 0.92f);
            if (label != null) label.raycastTarget = false;

            // Simple spinner — a small gold square just below the message, rotated each
            // frame in Update. Centred, ~120 ref px at the 1080x1920 reference res.
            var spin = ElarionUiKit.AddImage(transform, "Spinner",
                new Vector2(0.444f, 0.53f), new Vector2(0.556f, 0.59f),
                ElarionUi.Gold, rounded: true);
            _spinner = spin != null ? spin.transform as RectTransform : null;
            var spinImg = spin != null ? spin.GetComponent<Image>() : null;
            if (spinImg != null) spinImg.raycastTarget = false;

            _shownAt = Time.unscaledTime;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_sceneLoaded) return;                 // only the FIRST load after Show
            _sceneLoaded = true;
            _sceneLoadedAt = Time.unscaledTime;
            FlowTrace.Step("LoadingOverlay", $"first scene loaded '{scene.name}' — settling {SettleSeconds:F2}s then dismissing.");
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            // Spin the spinner (unscaled so a paused game still animates it).
            if (_spinner != null)
            {
                _spinAngle -= SpinDegPerSec * Time.unscaledDeltaTime;
                if (_spinAngle <= -360f) _spinAngle += 360f;
                _spinner.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);
            }

            if (_dismissing)
            {
                float fadeElapsed = Time.unscaledTime - _dismissStartedAt;
                if (_group != null)
                    _group.alpha = Mathf.Clamp01(1f - fadeElapsed / FadeSeconds);
                if (fadeElapsed >= FadeSeconds) Destroy(gameObject);
                return;
            }

            float elapsed = Time.unscaledTime - _shownAt;

            // Hard failsafe — never stick, even if no scene ever loads.
            if (elapsed >= MaxShowSeconds)
            {
                FlowTrace.Warn("LoadingOverlay", $"max-show {MaxShowSeconds:F0}s hit (no settle) — force-dismissing.");
                BeginDismiss();
                return;
            }

            // Dismiss once the first scene has loaded AND settled AND the min-show floor is met.
            bool settled = _sceneLoaded && (Time.unscaledTime - _sceneLoadedAt) >= SettleSeconds;
            if (settled && elapsed >= MinShowSeconds)
                BeginDismiss();
        }

        private void BeginDismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            _dismissStartedAt = Time.unscaledTime;
            if (_group != null) _group.blocksRaycasts = false;   // let the hub take input during the fade
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;   // idempotent even if already removed
            if (s_active == this) s_active = null;
        }
    }
}
