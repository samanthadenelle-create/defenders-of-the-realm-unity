// =============================================================================
// MobileInteractButton — the shared on-screen "Interact" button that gives every
// structure interaction a TOUCH path alongside the desktop [F] key (DEF-203).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// On mobile WebGL the structures (buildings, mines, the store, dungeon portals,
// upgrade/crafting panels) could ONLY be engaged with a keyboard "F" — there is
// no keyboard on a phone, so the player could not interact with them at all.
//
// This is a single DDOL singleton that owns ONE code-built bottom-centre button.
// Each frame an in-range structure calls Request(owner, label, onTap). The button
// shows that frame's request and fires its callback when tapped. If no structure
// requests it in a given frame, the button hides. This mirrors CampPromptUI's
// proven tap/hit-test pattern exactly (DEF-137): no UXML, no EventSystem, the
// builtin LegacyRuntime font, manual touch/mouse hit-testing. ASCII-only strings.
//
// The desktop [F] key is left untouched in every caller — this only ADDS a touch
// affordance, it never removes the key. Callers also flip their prompt text to be
// input-aware (e.g. "[ Tap / F ]") so the on-screen label is meaningful on a phone.
//
// Canon: village is Elarion.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Shared bottom-centre "Interact" button. Structures call
    /// <see cref="Request(object,string,Action)"/> every frame they are in range;
    /// the latest request that frame is what the button shows + fires on tap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileInteractButton : MonoBehaviour
    {
        private static MobileInteractButton _instance;

        private Canvas _canvas;
        private RectTransform _btnRect;
        private Text _label;

        // The request claimed THIS frame. Cleared in LateUpdate after rendering.
        private object _owner;
        private Action _onTap;
        private string _text;
        private bool _requestedThisFrame;
        private int _priority;

        /// <summary>
        /// Ask the shared button to show this frame with <paramref name="label"/>; tapping
        /// it invokes <paramref name="onTap"/>. Call every frame the structure is in range.
        /// Null-guarded + lazily self-bootstrapping; safe to call from any structure.
        /// </summary>
        public static void Request(object owner, string label, Action onTap)
            => Request(owner, label, onTap, 0);

        /// <summary>
        /// Priority overload (DEF-217). When several watchers are in range of the SAME
        /// building in one frame (e.g. BuildingInteractable + BuildingUpgradePanelInput +
        /// VillageCraftingPanelInput all see the Farm/Workshop), a "first caller wins"
        /// rule picks a non-deterministic winner each frame, so the button label FLICKERS
        /// between their prompts and reads as several competing prompts. We instead keep
        /// the HIGHEST-priority request this frame (ties broken by first-come) so exactly
        /// one stable prompt shows. Higher number = wins. Direct interact = 0; the
        /// panel-opening watchers pass a higher value so their richer action is what the
        /// single shared button offers on a shared building.
        /// </summary>
        public static void Request(object owner, string label, Action onTap, int priority)
        {
            if (onTap == null) return;
            var inst = EnsureInstance();
            if (inst == null) return;
            // Keep the existing claim unless this one outranks it. First claim of the
            // frame always sticks until something with a strictly higher priority arrives.
            if (inst._requestedThisFrame && priority <= inst._priority) return;
            inst._owner = owner;
            inst._onTap = onTap;
            inst._text = string.IsNullOrEmpty(label) ? "Interact" : label;
            inst._priority = priority;
            inst._requestedThisFrame = true;
        }

        /// <summary>
        /// True while the shared button is showing a request for <paramref name="owner"/>
        /// (or, when <paramref name="owner"/> is null, for anyone). Interactors that ALSO
        /// render a world-space prompt bubble use this to suppress the bubble so the touch
        /// button is the single on-screen prompt (DEF-217). Null-safe.
        /// </summary>
        public static bool IsShowingFor(object owner)
        {
            if (_instance == null) return false;
            if (!_instance._requestedThisFrame || _instance._owner == null) return false;
            return owner == null || ReferenceEquals(_instance._owner, owner);
        }

        /// <summary>True while the shared button is showing ANY request this frame.</summary>
        public static bool IsActive => _instance != null && _instance._requestedThisFrame
                                       && _instance._owner != null;

        /// <summary>
        /// Immediately drop any pending request from <paramref name="owner"/> and hide the
        /// button. Callers invoke this when they leave range / open a modal so a stale
        /// button can't linger for one frame. Null-safe; no-op if not the current owner.
        /// </summary>
        public static void Release(object owner)
        {
            if (_instance == null) return;
            if (_instance._owner != null && !ReferenceEquals(_instance._owner, owner)) return;
            _instance._owner = null;
            _instance._onTap = null;
            _instance._requestedThisFrame = false;
            _instance._priority = 0;
            _instance.HideButton();
        }

        private static MobileInteractButton EnsureInstance()
        {
            if (_instance != null) return _instance;
            // Some callers (RuntimeInitialize bootstraps) may run before any scene
            // MonoBehaviour; create the host on demand.
            var go = new GameObject("MobileInteractButton");
            _instance = go.AddComponent<MobileInteractButton>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Render the frame's request (if any).
            if (_requestedThisFrame && _btnRect != null)
            {
                if (_label != null && _label.text != _text) _label.text = _text;
                if (!_btnRect.gameObject.activeSelf) _btnRect.gameObject.SetActive(true);

                if (TryGetTap(out Vector2 tap) &&
                    RectTransformUtility.RectangleContainsScreenPoint(_btnRect, tap, null))
                {
                    var cb = _onTap;
                    // Clear before invoking so a callback that opens a modal + calls
                    // Release() doesn't fight a re-show this same frame.
                    _owner = null;
                    _onTap = null;
                    _requestedThisFrame = false;
                    _priority = 0;
                    HideButton();
                    cb?.Invoke();
                    return;
                }
            }
            else
            {
                HideButton();
            }
        }

        private void LateUpdate()
        {
            // Reset the per-frame claim AFTER everyone's Update has run. The button's
            // visible state was already committed in Update this frame; clearing the
            // flag here lets the next frame's first requester win again.
            _requestedThisFrame = false;
            _priority = 0;
            if (_owner == null) HideButton();
        }

        private void HideButton()
        {
            if (_btnRect != null && _btnRect.gameObject.activeSelf)
                _btnRect.gameObject.SetActive(false);
        }

        // =====================================================================
        // Code-built uGUI (build-safe font, no EventSystem) — CampPromptUI pattern.
        // =====================================================================
        private void BuildCanvas()
        {
            var go = new GameObject("InteractCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30050; // above the camp prompt (30000)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var btnGo = new GameObject("InteractButton");
            btnGo.transform.SetParent(_canvas.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.16f, 0.10f, 0.30f, 0.94f);   // village-violet
            _btnRect = img.rectTransform;
            // Bottom-centre, thumb-reachable, clear of the virtual joystick (left)
            // and the action buttons (right).
            _btnRect.anchorMin = new Vector2(0.5f, 0f);
            _btnRect.anchorMax = new Vector2(0.5f, 0f);
            _btnRect.pivot = new Vector2(0.5f, 0f);
            _btnRect.anchoredPosition = new Vector2(0f, 130f);
            _btnRect.sizeDelta = new Vector2(320f, 76f);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(btnGo.transform, false);
            _label = lblGo.AddComponent<Text>();
            _label.font = BuiltinFont();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(0.97f, 0.93f, 1.00f);
            _label.fontSize = 26;
            _label.fontStyle = FontStyle.Bold;
            _label.text = "Interact";
            var lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            _btnRect.gameObject.SetActive(false);
        }

        private static Font BuiltinFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        /// <summary>First-touch / mouse-down screen position this frame (no EventSystem).</summary>
        private static bool TryGetTap(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { pos = t.position; return true; }
            }
            if (Input.GetMouseButtonDown(0)) { pos = (Vector2)Input.mousePosition; return true; }
            pos = default;
            return false;
        }
    }
}
