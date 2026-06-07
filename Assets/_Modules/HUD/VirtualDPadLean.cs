// =============================================================================
// VirtualDPadLean — Lean Touch based virtual D-Pad for mobile hero movement.
// -----------------------------------------------------------------------------
// Uses Lean.Touch for all input (LeanFingerDown, LeanFingerSet, LeanFingerUp).
// Matches the "fantasy D-pad" in the dark fantasy mobile UI reference.
//
// Design:
// - Code-built visuals (base circle metal frame, knob).
// - Constrained to radius (thumb friendly).
// - Outputs normalized Vector2 (-1..1) via static Move (like previous VirtualJoystick for easy integration with HeroLocomotion).
// - Only active on touch targets.
// - Self bootstraps or attached by HUDManager.
// - No direct dep on Village/Hero systems (loose via static or event).
//
// To integrate with existing HeroLocomotion (which currently uses InputSystem + VirtualJoystick):
//   - HeroLocomotion can poll VirtualDPadLean.Move in addition to its current input (or replace VirtualJoystick.Move with this when Lean is active).
//   - Or add a small bridge in Village that reads this static and feeds locomotion.
//   - Since HUD cannot ref Village, the static + optional UnityEvent<Vector2> OnMove allows Village code to subscribe without asm cycle.
//
// Requires LeanTouch / LeanCommon / CW.Common in the HUD asmdef references.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using Lean.Touch;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class VirtualDPadLean : MonoBehaviour
    {
        /// <summary>Current D-Pad deflection (-1..1 per axis). Zero when not tracking. Static for easy cross-assembly polling.</summary>
        public static Vector2 Move { get; private set; }

        public static VirtualDPadLean Instance { get; private set; }

        [Header("Visuals (built at runtime if not assigned)")]
        [Tooltip("Optional pre-existing base Image (circle frame). If null, one is created.")]
        [SerializeField] private Image _baseImage;

        [Tooltip("Optional pre-existing knob Image. If null, one is created.")]
        [SerializeField] private Image _knobImage;

        [Header("Tuning")]
        [Tooltip("Radius in screen pixels for the knob travel (thumb friendly ~80-120).")]
        [SerializeField] private float _radius = 90f;

        [Tooltip("Show only on touch/mobile targets (like VirtualJoystick).")]
        [SerializeField] private bool _mobileOnly = true;

        private RectTransform _baseRect;
        private RectTransform _knobRect;
        private Vector2 _center;
        private bool _tracking;
        private LeanFinger _activeFinger;

        private Canvas _canvas; // parent canvas for screen pos calcs

        private static Texture2D _discTex;
        private static Sprite _discSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // HUDManager will usually own and attach this; bootstrap only if missing for standalone test.
            if (Instance != null) return;
            // Don't auto-create here to avoid polluting scenes; HUDManager creates when building UI.
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Don't DDOL by default; HUDManager parents to its canvas root.
            EnsureVisuals();
            if (_mobileOnly && !IsTouchTarget())
            {
                if (_baseRect != null) _baseRect.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Move = Vector2.zero;
        }

        private bool IsTouchTarget()
        {
            // Lean Touch + mobile platform is sufficient for D-Pad visibility. Avoid hard InputSystem asm ref
            // (HUD asmdef does not include Unity.InputSystem). Fall back to platform flag.
            return Application.isMobilePlatform;
        }

        private void EnsureVisuals()
        {
            if (_baseImage == null)
            {
                var baseGO = new GameObject("DPadBase", typeof(RectTransform), typeof(Image));
                baseGO.transform.SetParent(transform, false);
                _baseRect = baseGO.GetComponent<RectTransform>();
                _baseRect.sizeDelta = new Vector2(_radius * 2, _radius * 2);
                _baseImage = baseGO.GetComponent<Image>();
                _baseImage.sprite = GetDiscSprite();
                _baseImage.color = new Color(0.6f, 0.6f, 0.65f, 0.85f); // metal frame look
                _baseImage.type = Image.Type.Sliced;
            }
            else
            {
                _baseRect = _baseImage.GetComponent<RectTransform>();
            }

            if (_knobImage == null)
            {
                var knobGO = new GameObject("DPadKnob", typeof(RectTransform), typeof(Image));
                knobGO.transform.SetParent(_baseRect, false);
                _knobRect = knobGO.GetComponent<RectTransform>();
                _knobRect.sizeDelta = new Vector2(_radius * 0.6f, _radius * 0.6f);
                _knobImage = knobGO.GetComponent<Image>();
                _knobImage.sprite = GetDiscSprite();
                _knobImage.color = new Color(0.8f, 0.8f, 0.85f, 1f);
                _knobImage.type = Image.Type.Sliced;
            }
            else
            {
                _knobRect = _knobImage.GetComponent<RectTransform>();
            }

            _center = _baseRect.anchoredPosition; // or calculate in Start if dynamic
            // Center the knob initially
            if (_knobRect != null) _knobRect.anchoredPosition = Vector2.zero;
        }

        private static Sprite GetDiscSprite()
        {
            if (_discSprite != null) return _discSprite;
            if (_discTex == null)
            {
                _discTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                Color[] cols = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        float dx = x - 32f;
                        float dy = y - 32f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        cols[y * 64 + x] = (dist <= 31f) ? Color.white : Color.clear;
                    }
                }
                _discTex.SetPixels(cols);
                _discTex.Apply();
            }
            _discSprite = Sprite.Create(_discTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            return _discSprite;
        }

        private void Update()
        {
            if (_mobileOnly && !IsTouchTarget())
            {
                Move = Vector2.zero;
                if (_knobRect != null) _knobRect.anchoredPosition = Vector2.zero;
                return;
            }

            // LeanTouch handles the gestures; we poll the active finger state in the Lean callbacks.
            // This Update is for safety reset and visibility.
            if (!_tracking)
            {
                Move = Vector2.zero;
                if (_knobRect != null) _knobRect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Called by LeanFingerDown event (wire via LeanTouchEvents or code in HUDManager or on the dpad GO).</summary>
        public void OnFingerDown(LeanFinger finger)
        {
            if (_tracking || finger == null) return;

            Vector2 screenPos = finger.ScreenPosition;
            // Check if press is within the base rect (simple bounds check for thumb zone)
            if (_baseRect == null) return;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_baseRect, screenPos, null, out local);

            if (local.magnitude > _radius * 1.2f) return; // outside the dpad zone

            _tracking = true;
            _activeFinger = finger;
            _center = _baseRect.anchoredPosition; // update if the base moves

            UpdateKnob(screenPos);
        }

        /// <summary>Called by LeanFingerSet / LeanFingerUpdate (continuous while down).</summary>
        public void OnFingerSet(LeanFinger finger)
        {
            if (!_tracking || finger == null || finger != _activeFinger) return;

            UpdateKnob(finger.ScreenPosition);
        }

        /// <summary>Called by LeanFingerUp.</summary>
        public void OnFingerUp(LeanFinger finger)
        {
            if (finger == null || finger != _activeFinger) return;

            _tracking = false;
            _activeFinger = null;
            Move = Vector2.zero;

            if (_knobRect != null) _knobRect.anchoredPosition = Vector2.zero;
        }

        private void UpdateKnob(Vector2 screenPos)
        {
            if (_baseRect == null || _knobRect == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_baseRect, screenPos, null, out localPoint);

            // Constrain to radius
            if (localPoint.magnitude > _radius)
            {
                localPoint = localPoint.normalized * _radius;
            }

            _knobRect.anchoredPosition = localPoint;

            // Normalized output (-1..1)
            Move = localPoint / _radius;
        }

        /// <summary>Optional: call from HUDManager to position the dpad visuals at runtime (e.g. bottom left safe area).</summary>
        public void PositionAt(RectTransform targetParent, Vector2 anchoredPos, float size)
        {
            if (_baseRect == null) EnsureVisuals();
            if (_baseRect != null)
            {
                _baseRect.SetParent(targetParent, false);
                _baseRect.anchoredPosition = anchoredPos;
                _baseRect.sizeDelta = new Vector2(size, size);
            }
            if (_knobRect != null)
            {
                _knobRect.anchoredPosition = Vector2.zero;
            }
            _radius = size * 0.5f * 0.8f; // inner travel radius
        }
    }
}