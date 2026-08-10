// =============================================================================
// GuidePointer — WO-1012 §2b kit piece 2: the ONE moving cue.
// -----------------------------------------------------------------------------
// A single slim GOLD CHEVRON that eases onto the current tap target and settles
// into a slow 6px bob (one moving element on screen, never five), plus a
// GHOST-FINGER drag replay for gesture beats — a soft disc that replays the
// drag arc on a ~2s loop and fades PERMANENTLY after the player's first
// successful gesture (NotifyGestureSuccess). Replaces the boxed marker
// language ("childish buttons") for good.
//
// Code-built uGUI on its own overlay canvas (kit canon — no UIToolkit),
// UNSCALED time. Purely decorative: NO GraphicRaycaster, every graphic
// raycastTarget=false — it can never eat a tap. Targets resolve through
// TutorialHighlightRegistry every frame (same liveness rules as UiSpotlight:
// inactive/unresolved => hidden, re-acquires when the target returns).
// Presentation-only: no service calls, no game-state reads — callers
// (TutorialFlow) own all logic. [Flow:Tutorial] on every show/hide/success.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// The tutorial's single moving cue: a gold chevron easing onto a highlight
    /// target (<see cref="Show"/>), or a ghost-finger drag replay for gesture
    /// beats (<see cref="ShowDrag"/>). <see cref="NotifyGestureSuccess"/> fades a
    /// drag replay permanently; <see cref="Hide"/> clears everything.
    /// </summary>
    public sealed class GuidePointer : MonoBehaviour
    {
        private const int CanvasSortOrder = 4210;    // above the FocusMask dim (4200), below the strip (4300)
        private const float ChevronSizePx = 44f;     // slim chevron footprint
        private const float TipGapPx = 10f;          // chevron-tip clearance off the target edge
        private const float EaseSeconds = 0.35f;     // ease onto the target
        private const float EaseTravelPx = 46f;      // approach distance during the ease
        private const float SettleAmpPx = 3f;        // +/-3px = the 6px settle loop (spec 2b)
        private const float SettlePeriodSeconds = 1.6f;
        private const float FadeSeconds = 0.2f;
        private const float DragLoopSeconds = 2f;    // ghost-finger replay cadence (spec 2b)
        private const float FingerSizePx = 56f;
        private const float SuccessFadeSeconds = 0.45f;

        private enum Mode { None, Chevron, Drag }

        private static GuidePointer _instance;

        // Gesture beats already succeeded once this session — their replay never
        // reshows ("fading permanently after the player's first successful gesture").
        private static readonly HashSet<string> s_gestureDone =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        private CanvasGroup _group;
        private Image _chevron;
        private RectTransform _chevronRt;
        private Image _finger;
        private RectTransform _fingerRt;

        private Mode _mode = Mode.None;
        private string _targetId;          // chevron target / drag FROM target
        private Vector2 _dragToViewport;   // drag TO point (viewport 0..1)
        private float _showAt;             // ease clock start (unscaled)
        private float _fadeT;
        private bool _successFading;       // drag replay dismissed by a real gesture
        private float _successFadeAt;
        private string _tracedKey;         // one trace per (mode, id) — no per-rotation spam

        private static Sprite _chevronSprite;
        private static Sprite _fingerSprite;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Ease the chevron onto the target registered under
        /// <paramref name="highlightId"/>. Re-resolves every frame; while the target
        /// is unresolved/inactive the pointer hides and re-acquires later.</summary>
        public static void Show(string highlightId)
        {
            if (string.IsNullOrEmpty(highlightId)) { Hide(); return; }
            var p = Ensure();
            bool changed = p._mode != Mode.Chevron ||
                !string.Equals(p._targetId, highlightId, System.StringComparison.OrdinalIgnoreCase);
            p._mode = Mode.Chevron;
            p._targetId = highlightId;
            p._successFading = false;
            if (changed)
            {
                p._showAt = Time.unscaledTime;
                p.Trace($"GuidePointer SHOW chevron highlightId={highlightId}");
            }
        }

        /// <summary>Ghost-finger drag replay for a gesture beat: loops the drag arc
        /// from the <paramref name="fromHighlightId"/> target to
        /// <paramref name="toViewport"/> (viewport 0..1) every ~2s. A beat whose
        /// gesture already succeeded this session never reshows its replay.</summary>
        public static void ShowDrag(string fromHighlightId, Vector2 toViewport)
        {
            if (string.IsNullOrEmpty(fromHighlightId)) { Hide(); return; }
            if (s_gestureDone.Contains(fromHighlightId))
            {
                Diagnostics.FlowTrace.Step("Tutorial",
                    $"GuidePointer drag replay for '{fromHighlightId}' suppressed — gesture already succeeded (permanent fade).");
                return;
            }
            var p = Ensure();
            bool changed = p._mode != Mode.Drag ||
                !string.Equals(p._targetId, fromHighlightId, System.StringComparison.OrdinalIgnoreCase);
            p._mode = Mode.Drag;
            p._targetId = fromHighlightId;
            p._dragToViewport = toViewport;
            p._successFading = false;
            if (changed)
            {
                p._showAt = Time.unscaledTime;
                p.Trace($"GuidePointer SHOW ghost-finger drag from='{fromHighlightId}' to=({toViewport.x:0.00},{toViewport.y:0.00}) loop={DragLoopSeconds:0.0}s");
            }
        }

        /// <summary>The player performed the gesture: fade the drag replay out and
        /// never show it again for this beat (spec 2b "fading permanently after
        /// first success"). Safe when no replay is live.</summary>
        public static void NotifyGestureSuccess(string fromHighlightId)
        {
            if (string.IsNullOrEmpty(fromHighlightId)) return;
            bool added = s_gestureDone.Add(fromHighlightId);
            if (added)
                Diagnostics.FlowTrace.Step("Tutorial",
                    $"GuidePointer gesture SUCCESS for '{fromHighlightId}' — replay fades permanently.");
            var p = _instance;
            if (p == null || p._mode != Mode.Drag) return;
            if (!string.Equals(p._targetId, fromHighlightId, System.StringComparison.OrdinalIgnoreCase)) return;
            p._successFading = true;
            p._successFadeAt = Time.unscaledTime;
        }

        /// <summary>Ease the pointer out (chevron or drag). Safe when not shown.</summary>
        public static void Hide()
        {
            if (_instance == null || _instance._mode == Mode.None) return;
            // Direct trace (not Trace()) — the dedupe key would otherwise swallow the
            // HIDE that follows its own SHOW for the same id.
            Diagnostics.FlowTrace.Step("Tutorial", "GuidePointer HIDE");
            _instance._tracedKey = null;
            _instance._mode = Mode.None;
            _instance._targetId = null;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private static GuidePointer Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("GuidePointer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GuidePointer>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Deliberately NO GraphicRaycaster — the pointer can never eat a tap.
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Built through the kit (ElarionUiKit.AddImage) rather than a raw GameObject:
            // [ui-obsidian] hard-fails a NEW hand-rolled widget, and the kit is the one place
            // the Image primitive is created. rounded:false because both of these carry an
            // AUTHORED sprite - the kit's 9-sliced rounded sprite would overwrite it.
            var ch = ElarionUiKit.AddImage(transform, "Chevron", Vector2.zero, Vector2.zero,
                                           ElarionUi.Gold, rounded: false);
            _chevronRt = (RectTransform)ch.transform;
            _chevronRt.anchorMin = _chevronRt.anchorMax = Vector2.zero;   // positioned in screen px
            _chevronRt.pivot = new Vector2(0.5f, 0.5f);
            _chevronRt.sizeDelta = new Vector2(ChevronSizePx, ChevronSizePx);
            _chevron = ch.GetComponent<Image>();
            _chevron.sprite = ChevronSprite();
            _chevron.color = ElarionUi.Gold;
            _chevron.raycastTarget = false;
            ch.SetActive(false);

            var fg = ElarionUiKit.AddImage(transform, "GhostFinger", Vector2.zero, Vector2.zero,
                                           ElarionUi.Parchment, rounded: false);
            _fingerRt = (RectTransform)fg.transform;
            _fingerRt.anchorMin = _fingerRt.anchorMax = Vector2.zero;
            _fingerRt.pivot = new Vector2(0.5f, 0.5f);
            _fingerRt.sizeDelta = new Vector2(FingerSizePx, FingerSizePx);
            _finger = fg.GetComponent<Image>();
            _finger.sprite = FingerSprite();
            // Parchment-warm ghost, translucent — reads as a fingertip, not a cursor.
            _finger.color = new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.85f);
            _finger.raycastTarget = false;
            fg.SetActive(false);
        }

        private void Update()
        {
            bool wantVisible = _mode != Mode.None;
            float dir = wantVisible ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            _group.alpha = _fadeT * _fadeT * (3f - 2f * _fadeT);
            if (_fadeT <= 0f)
            {
                if (_chevron != null) _chevron.gameObject.SetActive(false);
                if (_finger != null) _finger.gameObject.SetActive(false);
                return;
            }

            switch (_mode)
            {
                case Mode.Chevron: TickChevron(); break;
                case Mode.Drag: TickDrag(); break;
                default:
                    if (_chevron != null) _chevron.gameObject.SetActive(false);
                    if (_finger != null) _finger.gameObject.SetActive(false);
                    break;
            }
        }

        // ── Chevron: ease on, 6px settle loop ─────────────────────────────────

        private void TickChevron()
        {
            if (_finger.gameObject.activeSelf) _finger.gameObject.SetActive(false);

            if (!TryResolveScreenRect(_targetId, out Rect target))
            {
                // Unresolved/inactive target — hide rather than float at a stale spot
                // (the UiSpotlight BM-3 liveness rule).
                _chevron.gameObject.SetActive(false);
                return;
            }
            if (!_chevron.gameObject.activeSelf) _chevron.gameObject.SetActive(true);

            // Above the target pointing down; flip below-pointing-up when the target
            // sits in the top band (the chevron must never leave the screen).
            bool below = target.center.y > Screen.height * 0.62f;
            float half = ChevronSizePx * 0.5f;
            float baseY = below
                ? target.yMin - TipGapPx - half
                : target.yMax + TipGapPx + half;
            _chevronRt.localScale = new Vector3(1f, below ? -1f : 1f, 1f);

            // Ease onto the target (approach from farther out), then the slow settle bob.
            float t01 = Mathf.Clamp01((Time.unscaledTime - _showAt) / EaseSeconds);
            t01 = t01 * t01 * (3f - 2f * t01);
            float approach = (1f - t01) * EaseTravelPx * (below ? -1f : 1f);
            float bob = t01 >= 1f
                ? Mathf.Sin((Time.unscaledTime - _showAt) * (2f * Mathf.PI / SettlePeriodSeconds)) * SettleAmpPx
                : 0f;

            _chevronRt.anchoredPosition = new Vector2(target.center.x, baseY + approach + bob);
            _chevron.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, t01);
        }

        // ── Ghost finger: press → drag → release on a 2s loop ─────────────────

        private void TickDrag()
        {
            if (_chevron.gameObject.activeSelf) _chevron.gameObject.SetActive(false);

            // Permanent fade after the first real gesture.
            if (_successFading)
            {
                float f = 1f - Mathf.Clamp01((Time.unscaledTime - _successFadeAt) / SuccessFadeSeconds);
                if (f <= 0f) { _mode = Mode.None; _targetId = null; _finger.gameObject.SetActive(false); return; }
                var c0 = _finger.color; _finger.color = new Color(c0.r, c0.g, c0.b, 0.85f * f);
                return;
            }

            if (!TryResolveScreenRect(_targetId, out Rect fromRect))
            {
                // The from-target (e.g. a palette card) is collapsed/rebuilt — hide the
                // replay until it comes back; never replay from a stale position.
                _finger.gameObject.SetActive(false);
                return;
            }

            Vector2 from = fromRect.center;
            Vector2 to = new Vector2(_dragToViewport.x * Screen.width, _dragToViewport.y * Screen.height);

            float cycle = ((Time.unscaledTime - _showAt) % DragLoopSeconds) / DragLoopSeconds;
            float alpha; float scale; Vector2 pos;
            if (cycle < 0.12f)                       // press-down at the source
            {
                float t = cycle / 0.12f;
                alpha = t; scale = Mathf.Lerp(1f, 0.86f, t); pos = from;
            }
            else if (cycle < 0.72f)                  // the drag arc
            {
                float t = (cycle - 0.12f) / 0.60f;
                t = t * t * (3f - 2f * t);
                alpha = 1f; scale = 0.86f; pos = Vector2.Lerp(from, to, t);
            }
            else if (cycle < 0.86f)                  // release at the destination
            {
                float t = (cycle - 0.72f) / 0.14f;
                alpha = 1f - t; scale = Mathf.Lerp(0.86f, 1f, t); pos = to;
            }
            else                                     // beat of rest before the next replay
            {
                alpha = 0f; scale = 1f; pos = to;
            }

            if (!_finger.gameObject.activeSelf) _finger.gameObject.SetActive(true);
            _fingerRt.anchoredPosition = pos;
            _fingerRt.localScale = Vector3.one * scale;
            _finger.color = new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.85f * alpha);
        }

        // ── Target resolution (shared projection rules with UiSpotlight) ──────

        private static bool TryResolveScreenRect(string id, out Rect rect)
        {
            rect = default;
            if (string.IsNullOrEmpty(id)) return false;
            var t = TutorialHighlightRegistry.Resolve(id);
            if (t.Rect != null)
            {
                if (!t.Rect.gameObject.activeInHierarchy) return false;   // BM-3 liveness rule
                rect = UiSpotlight.ScreenRectOf(t.Rect);
                return true;
            }
            if (t.World != null)
                return UiSpotlight.TryWorldToScreen(t.World.position, out rect);
            return false;
        }

        private void Trace(string msg)
        {
            string key = _mode + "|" + (_targetId ?? "");
            if (_tracedKey == key && _mode != Mode.None) return;
            _tracedKey = key;
            Diagnostics.FlowTrace.Step("Tutorial", msg);
        }

        // ── Generated sprites (once per process) ──────────────────────────────

        /// <summary>Slim gold chevron pointing DOWN (two feathered strokes meeting at a
        /// low tip). Painted once; flipped in Y when the pointer sits below a target.</summary>
        private static Sprite ChevronSprite()
        {
            if (_chevronSprite != null) return _chevronSprite;
            const int N = 96;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 tip = new Vector2(0.5f, 0.22f);
            Vector2 armL = new Vector2(0.14f, 0.66f);
            Vector2 armR = new Vector2(0.86f, 0.66f);
            const float halfStroke = 0.075f;   // slim strokes
            const float feather = 0.04f;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N, (y + 0.5f) / N);
                    float d = Mathf.Min(DistToSegment(p, armL, tip), DistToSegment(p, armR, tip));
                    float a = 1f - Mathf.Clamp01((d - halfStroke) / feather);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            _chevronSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _chevronSprite;
        }

        /// <summary>Soft round fingertip disc: solid centre, feathered rim.</summary>
        private static Sprite FingerSprite()
        {
            if (_fingerSprite != null) return _fingerSprite;
            const int N = 96;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = N * 0.5f;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.Clamp01(Mathf.InverseLerp(0.55f, 1.0f, dist));
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            _fingerSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _fingerSprite;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 0.000001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
