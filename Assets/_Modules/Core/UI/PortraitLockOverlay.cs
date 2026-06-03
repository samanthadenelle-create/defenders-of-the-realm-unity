// =============================================================================
// PortraitLockOverlay — "rotate your device to portrait" gate.
// -----------------------------------------------------------------------------
// The whole game UI is portrait-designed. On mobile web you cannot hard-lock
// orientation, so when the device is held in LANDSCAPE the HUD/menus break.
// The standard fix (and the one the owner asked for): keep designing for
// portrait, and drop a clean full-screen "↻ rotate to portrait" overlay over
// the top whenever landscape is detected. Rotate back → overlay vanishes.
//
// SELF-BOOTSTRAPPING — same idiom as Audio/AudioBootstrap.cs: a static
// [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] entry point spawns a single
// DontDestroyOnLoad host the first time any scene loads. No scene wiring, no
// prefab, no PanelSettings dependency. It is alive in every scene from the
// title screen onward.
//
// CODE-BUILT uGUI ONLY (CLAUDE.md §8 — UXML/USS does not render in this
// project's WebGL builds). The overlay is a screen-space-overlay Canvas at the
// maximum sortingOrder (32767) so it sits above every gameplay/HUD canvas and
// every UIElements PanelSettings panel. It is built ONCE and toggled with
// SetActive — no per-frame allocations.
//
// It does NOT touch Time.timeScale (deliberately — don't fight the existing
// pause/ATB scaling). It just covers the broken view until the user rotates.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Full-screen overlay shown whenever the device is in landscape, asking the
    /// player to rotate back to portrait. Self-spawns at startup as a
    /// DontDestroyOnLoad singleton; no scene wiring required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortraitLockOverlay : MonoBehaviour
    {
        /// <summary>Active instance, if any.</summary>
        public static PortraitLockOverlay Instance { get; private set; }

        // ── Tuning ──────────────────────────────────────────────────────────
        // Hysteresis margin (in pixels) so a near-square aspect doesn't flicker
        // the overlay on/off frame-to-frame. We only consider it landscape when
        // width exceeds height by more than this margin, and only consider it
        // portrait again when height catches back up past the same margin.
        private const int AspectMarginPx = 24;

        // How often we re-check orientation. A light timer rather than every
        // frame — orientation changes are a human-scale event, ~5×/sec is ample.
        private const float CheckInterval = 0.2f;

        // The overlay sits above EVERYTHING. 32767 is the max signed-16-bit
        // sortingOrder Unity accepts for a Canvas.
        private const int TopSortingOrder = 32767;

        // ── State ───────────────────────────────────────────────────────────
        private GameObject _overlayRoot;     // built once, toggled active
        private bool _landscape;             // last computed orientation
        private float _nextCheck;

        /// <summary>
        /// Auto-invoked by Unity after the first scene loads. Idempotent — a
        /// second call (or a hand-placed instance) is a no-op.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Bootstrap()
        {
            // Owner 2026-06-03 ("why is the game forcing portrait mode" / "camera angle
            // won't work" in play mode): the rotate-to-portrait gate blocked LANDSCAPE in
            // the Editor + Device Simulator + play mode, making camera-angle testing
            // impossible. Real users only ever hit it in the actual mobile build, so skip
            // it in the Editor entirely. (Player builds keep the lock — Application.isEditor
            // is false there.)
            if (Application.isEditor) return;
            if (Instance != null) return;
            var host = new GameObject("PortraitLockOverlay");
            host.AddComponent<PortraitLockOverlay>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Never Destroy(gameObject) here — this host is dedicated to us,
                // but follow the project's "destroy the component, not the host"
                // safety convention regardless (singleton-dedup landmine).
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildOverlay();

            // Evaluate immediately so a session that starts in landscape is
            // gated on frame one rather than after the first CheckInterval.
            _landscape = ComputeLandscape(_landscape);
            ApplyVisibility();
            _nextCheck = Time.unscaledTime + CheckInterval;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckInterval;

            bool next = ComputeLandscape(_landscape);
            if (next == _landscape) return;

            _landscape = next;
            ApplyVisibility();
        }

        // ── Orientation logic ───────────────────────────────────────────────
        /// <summary>
        /// Decide whether we're in landscape, with hysteresis so a near-square
        /// aspect doesn't oscillate. <paramref name="current"/> is the previously
        /// reported state; we require the screen to cross the margin to flip it.
        /// </summary>
        private static bool ComputeLandscape(bool current)
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) return current; // degenerate (minimized) — keep state

            if (current)
            {
                // Currently showing the "rotate" overlay: only clear it once the
                // device is meaningfully taller than wide again.
                return w > h - AspectMarginPx;
            }
            // Currently hidden: only show it once width clearly beats height.
            return w > h + AspectMarginPx;
        }

        private void ApplyVisibility()
        {
            if (_overlayRoot != null) _overlayRoot.SetActive(_landscape);
        }

        // ── Overlay construction (once) ─────────────────────────────────────
        private void BuildOverlay()
        {
            // Root canvas — screen-space overlay, max sort order, parented to
            // this DDOL host so it survives scene loads with us.
            _overlayRoot = new GameObject("PortraitLockCanvas");
            _overlayRoot.transform.SetParent(transform, false);

            var canvas = _overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = TopSortingOrder;

            var scaler = _overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait reference
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster so the opaque background swallows taps to the
            // game beneath while the gate is up. (No buttons; no EventSystem
            // dependency required for it to block rendering.)
            _overlayRoot.AddComponent<GraphicRaycaster>();

            // Opaque dark full-screen background.
            var bg = new GameObject("Background");
            bg.transform.SetParent(_overlayRoot.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.03f, 0.07f, 1f); // near-black, faint Elarion purple
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Build-safe font — bundled with every player build.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Rotating-phone pictograph drawn from PURE ASCII only. The bundled
            // LegacyRuntime.ttf is a Latin set, so any Unicode symbol glyph
            // (rotate arrow U+21BB, arrows U+2192, etc.) renders as a missing-
            // glyph box. ASCII brackets read as an upright phone reliably.
            CreateText(
                parent: _overlayRoot.transform,
                name: "RotateGlyph",
                content: "[==]", // upright "phone" silhouette
                font: font,
                fontSize: 180,
                color: new Color(0.85f, 0.78f, 0.45f, 1f), // soft gold
                anchorY: 0.62f,
                height: 320f);

            // Instruction line.
            CreateText(
                parent: _overlayRoot.transform,
                name: "Message",
                content: "Please rotate your device\nto portrait",
                font: font,
                fontSize: 56,
                color: new Color(0.92f, 0.90f, 0.84f, 1f),
                anchorY: 0.40f,
                height: 260f);

            // Start hidden; ApplyVisibility flips it on if we boot in landscape.
            _overlayRoot.SetActive(false);
        }

        /// <summary>
        /// Helper: a centered uGUI Text band anchored at a vertical fraction of
        /// the canvas. Wraps and center-aligns; stretches full width.
        /// </summary>
        private static void CreateText(
            Transform parent, string name, string content, Font font,
            int fontSize, Color color, float anchorY, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            // Full width, fixed-height band centered on anchorY.
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
