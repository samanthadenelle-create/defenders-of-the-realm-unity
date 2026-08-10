// =============================================================================
// UiSpotlight — the tutorial FOCUS MASK (WO-T2, upgraded WO-1012 §2b piece 1).
// -----------------------------------------------------------------------------
// Dims the whole screen EXCEPT one soft-edged circular cutout over a registered
// UI target or a world position, with a gold glow on the rim. Code-built uGUI on
// its own overlay canvas (kit canon — no UIToolkit), UNSCALED time, eased in/out
// ~200ms.
//
// WO-1012 §2b (owner 2026-08-09, supersedes the 2026-07-16 "glow not circle"
// ruling FOR FOCUS BEATS): the mask carries THREE styles —
//   * Focus   — ~65% dim, ONE soft-edged cutout, raycast-BLOCK outside the
//               cutout (tap beats: the cutout is the only actionable thing).
//               Blocking applies ONLY when the resolved target is a UI rect —
//               a world-anchored cutout never blocks (the player must still
//               move/fight; blocking movement on a world beat is the wedge).
//   * Gesture — ~35% dim, same cutout, NEVER blocks (drag/movement beats: the
//               world stays readable and the gesture must land anywhere).
//   * Glow    — the 2026-07-16 glow-only presentation, no dim, never blocks
//               (contextual hints + combat beats keep the old language).
//
// Cutout construction (pure uGUI, no shader): four dim rects around a square
// hole + a generated "circle-hole" sprite filling the square whose alpha is the
// dim color outside a feathered circle and 0 inside — together the screen is
// dimmed everywhere except a soft-edged circular window.
//
// Targets resolve through TutorialHighlightRegistry every frame (follows moving
// targets, survives late registration). Presentation-only: no service calls, no
// game-state reads — callers (TutorialFlow) own all logic.
//
// SEPARATE from ElarionUiKit by design (do-not-touch this slice); kit-promotion
// candidate: fold Show/ShowWorld into ElarionUiKit.Spotlight(...) in WO-T5.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Full-screen dim with a soft-edged circular cutout + gold glow over a
    /// highlight target. <see cref="Show(string, MaskStyle)"/> follows a registry
    /// id; <see cref="ShowWorld(Transform, MaskStyle)"/> follows a world anchor.
    /// <see cref="Hide"/> eases out. Blocks input outside the cutout ONLY in
    /// <see cref="MaskStyle.Focus"/> on a resolved UI-rect target.
    /// </summary>
    public sealed class UiSpotlight : MonoBehaviour
    {
        /// <summary>WO-1012 §2b presentation styles — see the file header.</summary>
        public enum MaskStyle
        {
            /// <summary>~65% dim, raycast-block outside the cutout (tap beats).</summary>
            Focus,
            /// <summary>~35% dim, never blocks (gesture/movement beats — world stays readable).</summary>
            Gesture,
            /// <summary>No dim, glow halo only, never blocks (contextual hints, combat beats).</summary>
            Glow,
        }

        private const float FadeSeconds = 0.2f;          // eased in/out ~200ms (unscaled)
        // WO-1012 §2b dim levels. History: DimAlpha was 0.62 at birth, then 0 (owner 2026-07-16
        // "glow to the button, not a circle"); WO-1012 re-rules FOCUS beats back to a dimmed
        // cutout while the glow-only look survives as MaskStyle.Glow.
        private const float FocusDimAlpha = 0.65f;       // Focus: the screen dims, the cutout teaches
        private const float GestureDimAlpha = 0.35f;     // Gesture: lighter — the world stays readable
        private const float HolePadding = 46f;           // px beyond the target rect (glow reach)
        private const float MinHoleDiameter = 120f;      // px — never a pin-prick
        private const float RingPulsePeriod = 1.4f;      // gold rim breathing (s)
        private const int CanvasSortOrder = 4200;        // above HUD, below modal dialogs
        private const int HoleTexSize = 256;             // generated cutout/ring texture size

        private static UiSpotlight _instance;

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _rootRect;
        private readonly Image[] _dimRects = new Image[4];   // top / bottom / left / right
        private RectTransform _holeCell;                      // the square cutout cell
        private Image _holeCorners;                           // circle-hole sprite (dims the cell corners)
        private Image _ring;                                  // gold pulse ring on the rim

        private string _targetId;          // registry id mode
        private Transform _worldAnchor;    // world mode
        private bool _visible;
        private float _fadeT;              // 0 hidden .. 1 shown
        private MaskStyle _style = MaskStyle.Focus;   // WO-1012: dim level + blocking per style

        private static Sprite _holeSprite;   // generated once, cached for the process
        private static Sprite _ringSprite;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Spotlight the target registered under <paramref name="highlightId"/>
        /// (TutorialHighlightRegistry). Re-resolves every frame, so late registration
        /// and moving targets both work; while unresolved the overlay stays hidden.
        /// <paramref name="style"/> picks the WO-1012 presentation (dim + blocking).</summary>
        public static void Show(string highlightId, MaskStyle style = MaskStyle.Focus)
        {
            var s = Ensure();
            s._targetId = highlightId;
            s._worldAnchor = null;
            s._visible = true;
            s.ApplyStyle(style);
            s._tracedTarget = null;   // BM-3: re-log the resolved target/rect for the new id
            Diagnostics.FlowTrace.Step("Tutorial", $"FocusMask SHOW highlightId={highlightId} style={style}");
        }

        /// <summary>Spotlight a world-space anchor (projected via the main camera).</summary>
        public static void ShowWorld(Transform anchor, MaskStyle style = MaskStyle.Gesture)
        {
            var s = Ensure();
            s._targetId = null;
            s._worldAnchor = anchor;
            s._visible = anchor != null;
            s.ApplyStyle(style);
        }

        /// <summary>Ease the spotlight out. Safe when not shown.</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            if (_instance._visible)
                Diagnostics.FlowTrace.Step("Tutorial", "FocusMask HIDE");
            _instance._visible = false;
            _instance._targetId = null;
            _instance._worldAnchor = null;
            _instance._tracedTarget = null;   // BM-3: forget the last resolved target
            if (_instance._group != null)
            {
                _instance._group.blocksRaycasts = false;   // never leave a dead mask eating taps
                _instance._group.interactable = false;
            }
        }

        /// <summary>Recolour the dim layers for <paramref name="style"/>. The four dim
        /// rects + the cutout-corner fill share one obsidian hue at the style's alpha;
        /// blocking is applied per-frame in Update (needs the resolved-target kind).</summary>
        private void ApplyStyle(MaskStyle style)
        {
            _style = style;
            float a = style == MaskStyle.Focus ? FocusDimAlpha
                    : style == MaskStyle.Gesture ? GestureDimAlpha
                    : 0f;
            Color dim = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                  ElarionUiKit.ObsidianFill.b, a);
            for (int i = 0; i < 4; i++)
                if (_dimRects[i] != null) _dimRects[i].color = dim;
            if (_holeCorners != null) _holeCorners.color = dim;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private static UiSpotlight Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("UiSpotlight");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UiSpotlight>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortOrder;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // WO-1012 §2b: Focus beats raycast-BLOCK outside the cutout, so the canvas now
            // carries a raycaster. Blocking is gated per-frame via the CanvasGroup (only a
            // shown Focus mask on a UI-rect target ever blocks — Gesture/Glow never do, and
            // dialogue/modals sort far above 4200 so they are never occluded).
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _rootRect = (RectTransform)transform;

            // Obsidian-tinted dim (not pure black) so the scrim reads as the kit's
            // warm-obsidian language; the gold glow completes the black+gold pairing.
            // Alpha is set per style in ApplyStyle; built at the Focus default.
            Color dim = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                  ElarionUiKit.ObsidianFill.b, FocusDimAlpha);
            for (int i = 0; i < 4; i++)
            {
                var r = new GameObject("Dim" + i, typeof(RectTransform), typeof(Image));
                r.transform.SetParent(transform, false);
                var img = r.GetComponent<Image>();
                img.color = dim;
                // The dim rects ARE the raycast blocker (outside the cutout). Whether they
                // actually block is the CanvasGroup's per-frame call in Update.
                img.raycastTarget = true;
                _dimRects[i] = img;
            }

            var cell = new GameObject("HoleCell", typeof(RectTransform));
            cell.transform.SetParent(transform, false);
            _holeCell = (RectTransform)cell.transform;

            var corners = new GameObject("HoleCorners", typeof(RectTransform), typeof(Image));
            corners.transform.SetParent(_holeCell, false);
            _holeCorners = corners.GetComponent<Image>();
            _holeCorners.sprite = HoleSprite();
            _holeCorners.color = dim;
            // NEVER a raycast target: this image spans the whole cutout cell (its alpha is
            // 0 inside the circle) — blocking here would kill the very tap the cutout
            // invites. The cell's soft corners therefore pass taps too; deliberate leak.
            _holeCorners.raycastTarget = false;
            Fill((RectTransform)corners.transform);

            var ring = new GameObject("PulseRing", typeof(RectTransform), typeof(Image));
            ring.transform.SetParent(_holeCell, false);
            _ring = ring.GetComponent<Image>();
            _ring.sprite = RingSprite();
            _ring.color = ElarionUi.Gold;
            _ring.raycastTarget = false;
            Fill((RectTransform)ring.transform);
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            // Eased fade on UNSCALED time (works while gameplay is paused).
            float dir = _visible ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            _group.alpha = _fadeT * _fadeT * (3f - 2f * _fadeT);   // smoothstep ease
            if (_fadeT <= 0f) { _group.blocksRaycasts = false; return; }

            if (!TryTargetScreenRect(out Rect target, out bool isUiRect))
            {
                // Unresolved target — keep the overlay invisible rather than dimming
                // the whole screen with no window (degrade gracefully, never wedge),
                // and NEVER block on an invisible mask.
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                return;
            }

            // WO-1012 §2b: raycast-block outside the cutout — Focus style ONLY, and only
            // when the resolved target is a UI rect (a world-anchored cutout never blocks:
            // the player must still be able to move/fight through the HUD).
            _group.blocksRaycasts = _visible && _style == MaskStyle.Focus && isUiRect;

            LayoutHole(target);

            // Gold rim breath — subtle scale + alpha pulse.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / RingPulsePeriod));
            _ring.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                                    Mathf.Lerp(0.45f, 0.95f, pulse));
            _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.06f, pulse);
        }

        // ── Target resolution ─────────────────────────────────────────────────

        private bool TryTargetScreenRect(out Rect rect, out bool isUiRect)
        {
            rect = default;
            isUiRect = false;

            if (!string.IsNullOrEmpty(_targetId))
            {
                var t = TutorialHighlightRegistry.Resolve(_targetId);
                if (t.Rect != null)
                {
                    // BM-3 (WO-746): FOLLOW the target's liveness. A registered card whose tray
                    // is collapsed (palette minimized while placing) or that was rebuilt this
                    // frame is INACTIVE — its RectTransform still returns stale world corners, so
                    // without this guard the glow floats at the last on-screen position (owner
                    // sighting: halo stranded over "Rotate Right"). Inactive => treat as
                    // unresolved => the overlay hides (Update sets alpha 0); it re-acquires when
                    // the card re-registers active.
                    if (!t.Rect.gameObject.activeInHierarchy) return false;
                    rect = ScreenRectOf(t.Rect);
                    isUiRect = true;
                    TraceResolved(t.Rect.name, rect);
                    return true;
                }
                if (t.World != null && TryWorldToScreen(t.World.position, out rect))
                {
                    TraceResolved(t.World.name, rect);
                    return true;
                }
                return false;
            }
            if (_worldAnchor != null)
                return TryWorldToScreen(_worldAnchor.position, out rect);
            return false;
        }

        // BM-3 (WO-746) instrumentation: log the resolved target + screen rect ONCE per
        // (id, target) so the §12 capture proves which element the spotlight actually sits on
        // (wrong-target vs stale-rect split), without a per-frame line. Re-armed by Show/Hide.
        private string _tracedTarget;
        private void TraceResolved(string targetName, Rect rect)
        {
            string key = (_targetId ?? "") + "|" + (targetName ?? "");
            if (_tracedTarget == key) return;
            _tracedTarget = key;
            Diagnostics.FlowTrace.Step("Tutorial",
                $"FocusMask resolved highlightId={_targetId} target={targetName} style={_style} " +
                $"rect=({rect.x:F0},{rect.y:F0},{rect.width:F0},{rect.height:F0})");
        }

        /// <summary>Screen-space pixel rect of a uGUI RectTransform. Internal so the
        /// sibling kit piece GuidePointer (WO-1012 §2b) shares ONE projection rule.</summary>
        internal static Rect ScreenRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = cam != null
                    ? (Vector2)cam.WorldToScreenPoint(corners[i])
                    : (Vector2)corners[i];   // overlay canvas corners ARE screen px
                min = Vector2.Min(min, sp);
                max = Vector2.Max(max, sp);
            }
            return new Rect(min, max - min);
        }

        /// <summary>Project a world position to a nominal screen rect (chest-high aim).
        /// Internal so GuidePointer shares the same projection (WO-1012 §2b).</summary>
        internal static bool TryWorldToScreen(Vector3 world, out Rect rect)
        {
            rect = default;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * 1.2f);   // aim chest-high
            if (sp.z <= 0f) return false;   // behind the camera — no spotlight
            const float worldTargetSize = 120f;
            rect = new Rect(sp.x - worldTargetSize * 0.5f, sp.y - worldTargetSize * 0.5f,
                            worldTargetSize, worldTargetSize);
            return true;
        }

        // ── Layout: 4 dim rects around a square cell + circular-corner fill ───

        private void LayoutHole(Rect target)
        {
            float d = Mathf.Max(MinHoleDiameter,
                                Mathf.Max(target.width, target.height) + HolePadding * 2f);
            Vector2 c = target.center;
            float half = d * 0.5f;
            float sw = Screen.width, sh = Screen.height;

            float left = c.x - half, right = c.x + half;
            float bottom = c.y - half, top = c.y + half;

            SetPx(_dimRects[0].rectTransform, 0f, top, sw, Mathf.Max(0f, sh - top));        // above
            SetPx(_dimRects[1].rectTransform, 0f, 0f, sw, Mathf.Max(0f, bottom));           // below
            SetPx(_dimRects[2].rectTransform, 0f, bottom, Mathf.Max(0f, left), d);          // left
            SetPx(_dimRects[3].rectTransform, right, bottom, Mathf.Max(0f, sw - right), d); // right
            SetPx(_holeCell, left, bottom, d, d);                                           // the window
        }

        private static void SetPx(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ── Generated sprites (once per process) ──────────────────────────────

        /// <summary>Square sprite: dim-opaque outside a feathered circle, clear inside —
        /// laid over the cutout cell it turns the square window into a circle.</summary>
        private static Sprite HoleSprite()
        {
            if (_holeSprite != null) return _holeSprite;
            // WO-1012 §2b: SOFT-edged cutout — the dim eases in across the outer ~22% of
            // the window instead of the old near-hard 0.92→0.99 rim.
            _holeSprite = MakeRadialSprite((dist01) =>
                Mathf.Clamp01(Mathf.InverseLerp(0.78f, 1.0f, dist01)));   // 0 inside → 1 at rim, feathered
            return _holeSprite;
        }

        /// <summary>Soft GLOW halo (owner 2026-07-16 "glow to the button, not a circle"): a wide
        /// feathered gold fill, brightest around the target edge and fading smoothly outward, so it
        /// reads as light emanating from the button rather than a hard ring drawn around it.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            _ringSprite = MakeRadialSprite((dist01) =>
            {
                // Peak near the target edge (~0.72), soft falloff both directions = a glowing halo.
                float g = 1f - Mathf.Clamp01(Mathf.Abs(dist01 - 0.72f) / 0.55f);
                return g * g;   // smooth, no hard rim
            });
            return _ringSprite;
        }

        private static Sprite MakeRadialSprite(System.Func<float, float> alphaByDist)
        {
            var tex = new Texture2D(HoleTexSize, HoleTexSize, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = HoleTexSize * 0.5f;
            var px = new Color[HoleTexSize * HoleTexSize];
            for (int y = 0; y < HoleTexSize; y++)
                for (int x = 0; x < HoleTexSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);   // 0 centre .. 1 rim (√2 corner)
                    px[y * HoleTexSize + x] = new Color(1f, 1f, 1f,
                        alphaByDist(Mathf.Min(dist, 1.42f)));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, HoleTexSize, HoleTexSize),
                                 new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
