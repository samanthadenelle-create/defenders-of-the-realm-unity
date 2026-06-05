// =============================================================================
// VirtualJoystick — on-screen movement stick for the WebGL / mobile build.
// -----------------------------------------------------------------------------
// 2026-06-02 (owner, live web build): "the village loads but there are no controls
// to navigate." HeroLocomotion reads keyboard/gamepad only — so the web build is
// unplayable on mobile (no keyboard) and needs a canvas-click for WASD on desktop.
// This adds a code-built thumbstick the hero reads as movement input.
//
// DESIGN (deliberately dependency-light so it can't fail in a player build):
//   • CODE-BUILT uGUI (Canvas + two Image discs with a runtime-drawn circle) — NO
//     UXML / UIDocument (which don't render in builds — project rule).
//   • POLLS UnityEngine.Input (touch first, then mouse) — NO EventSystem /
//     GraphicRaycaster needed, so nothing to mis-wire in WebGL.
//   • Engages ONLY when the press starts inside the bottom-left stick zone, so the
//     rest of the screen (ability buttons, left-click attack, camera) is untouched.
//   • HeroLocomotion.ReadMoveInput() adds VirtualJoystick.Move to its input vector.
// Self-bootstrapping DDOL; visible only in the gameplay (Village) scene.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Code-built on-screen movement joystick. Exposes <see cref="Move"/>
    /// (−1..1 per axis) read by <see cref="HeroLocomotion"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class VirtualJoystick : MonoBehaviour
    {
        /// <summary>Current stick deflection, −1..1 per axis (zero when not held).</summary>
        public static Vector2 Move { get; private set; }
        public static VirtualJoystick Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _baseRect, _knobRect;
        private Vector2 _baseCenter;   // screen-space px
        private float _radius;
        private bool _tracking;
        private float _visTimer;       // throttles the hero-presence visibility check
        private float _lateTouchTimer; // one-shot late re-check (Touchscreen.current can init late on mobile)
        private static Texture2D _disc;
        private static Sprite _discSprite;

        /// <summary>
        /// True only on a touch/mobile target. Desktop (WASD/mouse) never shows the stick.
        /// Robust gate: <see cref="Application.isMobilePlatform"/> OR a live touchscreen
        /// device (covers mobile WebGL, where isMobilePlatform alone is unreliable).
        /// </summary>
        private static bool IsTouchTarget()
        {
            return Application.isMobilePlatform
                || UnityEngine.InputSystem.Touchscreen.current != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("VirtualJoystick").AddComponent<VirtualJoystick>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUi();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m) => ApplyVisibility();

        private void ApplyVisibility()
        {
            // Show only on a touch/mobile target AND when there's a hero to drive (covers
            // Village + OuterWorld + any gameplay scene, and survives the hero spawning a
            // frame after scene-load). Desktop uses WASD/mouse — never show the stick there.
            bool show = IsTouchTarget() && FindFirstObjectByType<HeroLocomotion>() != null;
            if (_canvas != null && _canvas.gameObject.activeSelf != show)
                _canvas.gameObject.SetActive(show);
            if (!show) { Move = Vector2.zero; _tracking = false; }
        }

        private void Update()
        {
            // Re-check hero presence periodically (the hero spawns after scene-load, and
            // a scene swap may not have fired our handler yet).
            _visTimer -= Time.unscaledDeltaTime;
            if (_visTimer <= 0f) { _visTimer = 0.5f; ApplyVisibility(); }

            // One-shot late re-check: Touchscreen.current can initialize a frame or two
            // after startup on mobile, so re-evaluate visibility once shortly after launch.
            if (_lateTouchTimer < 1.5f)
            {
                _lateTouchTimer += Time.unscaledDeltaTime;
                if (_lateTouchTimer >= 1.5f) ApplyVisibility();
            }

            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            Layout();   // keep placement correct across resize / orientation

            GetPointer(out bool down, out Vector2 pos);

            // Engage only when the press starts within the bottom-left stick zone, so
            // taps elsewhere (ability buttons / attack / camera) are never stolen.
            bool inZone = (pos - _baseCenter).magnitude <= _radius * 1.7f;
            if (down && (_tracking || inZone))
            {
                _tracking = true;
                Vector2 delta = pos - _baseCenter;
                if (delta.magnitude > _radius) delta = delta.normalized * _radius;
                if (_knobRect != null) _knobRect.position = _baseCenter + delta;
                Move = delta / _radius;
            }
            else
            {
                _tracking = false;
                Move = Vector2.zero;
                if (_knobRect != null) _knobRect.position = _baseCenter;
            }
        }

        private static void GetPointer(out bool down, out Vector2 pos)
        {
            // Touch first (mobile), then mouse (desktop). Legacy Input (activeInputHandler=Both).
            if (UnityEngine.Input.touchCount > 0)
            {
                var t = UnityEngine.Input.GetTouch(0);
                down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                pos = t.position;
                return;
            }
            down = UnityEngine.Input.GetMouseButton(0);
            pos = (Vector2)UnityEngine.Input.mousePosition;
        }

        // DEF-202/204 (CAMERA_INPUT_OVERHAUL.md §4.2): the single source of truth for the
        // joystick's screen geometry, so CameraPanInput's start-zone exclusion can't drift
        // from the live layout. _radius = max(60, min(w,h)*0.16); centre at (_radius*1.35,
        // _radius*1.35) from bottom-left. Returns (radius, centre) — used by both Layout()
        // and the static IsInZone() test.
        private static void ComputeZone(out float radius, out Vector2 center)
        {
            float s = Mathf.Min(Screen.width, Screen.height);
            radius = Mathf.Max(60f, s * 0.16f);
            float margin = radius * 1.35f;
            center = new Vector2(margin, margin);   // bottom-left
        }

        /// <summary>
        /// True if <paramref name="screenPos"/> (px, origin bottom-left) is inside the
        /// joystick engage zone (the same radius*1.7 circle the joystick claims at Update).
        /// CameraPanInput uses this so it never claims a finger that STARTED in the stick zone.
        /// </summary>
        public static bool IsInZone(Vector2 screenPos)
        {
            // If the joystick isn't actually shown (desktop), its zone must NOT claim the
            // bottom-left corner — otherwise it would silently eat desktop build-placement
            // clicks there. Gate on the same touch/mobile + live-instance condition.
            if (!IsTouchTarget() || Instance == null
                || Instance._canvas == null || !Instance._canvas.gameObject.activeSelf)
                return false;
            ComputeZone(out float radius, out Vector2 center);
            return (screenPos - center).magnitude <= radius * 1.7f;
        }

        private void Layout()
        {
            ComputeZone(out _radius, out _baseCenter);
            if (_baseRect != null)
            {
                _baseRect.position = _baseCenter;
                _baseRect.sizeDelta = new Vector2(_radius * 2f, _radius * 2f);
            }
            if (_knobRect != null)
            {
                if (!_tracking) _knobRect.position = _baseCenter;
                _knobRect.sizeDelta = new Vector2(_radius, _radius);
            }
        }

        private void BuildUi()
        {
            var go = new GameObject("JoystickCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000;   // above the HUD, below the game-over overlay (32760)
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _baseRect = MakeDisc("JoyBase", go.transform, new Color(1f, 1f, 1f, 0.16f));
            _knobRect = MakeDisc("JoyKnob", go.transform, new Color(1f, 0.95f, 0.75f, 0.42f));
            Layout();
        }

        private static RectTransform MakeDisc(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = DiscSprite();
            img.color = color;
            img.raycastTarget = false;   // never blocks gameplay/HUD input
            return go.GetComponent<RectTransform>();
        }

        private static Sprite DiscSprite()
        {
            if (_discSprite != null) return _discSprite;
            const int S = 128;
            _disc = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = 1f - Mathf.SmoothStep(0.86f, 1f, d);   // soft filled circle
                _disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            _disc.Apply();
            _discSprite = Sprite.Create(_disc, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _discSprite;
        }
    }
}
