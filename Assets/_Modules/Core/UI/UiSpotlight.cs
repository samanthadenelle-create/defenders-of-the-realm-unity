// =============================================================================
// UiSpotlight — reusable dim-screen + circular-cutout attention affordance (WO-T2).
// -----------------------------------------------------------------------------
// Dims the whole screen EXCEPT a circular cutout over a registered UI target or
// a world position, with a gold pulse ring on the rim. Code-built uGUI on its
// own overlay canvas (kit canon — no UIToolkit), UNSCALED time, eased in/out
// ~200ms. It GUIDES, it never gates: every graphic is raycastTarget=false so
// input passes straight through (spec §2.2 "closable/ignorable — the completion
// signal advances, not the spotlight").
//
// Cutout construction (pure uGUI, no shader): four dim rects around a square
// hole + a generated "circle-hole" sprite filling the square whose alpha is the
// dim color outside a feathered circle and 0 inside — together the screen is
// dimmed everywhere except an exact circular window.
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
    /// Full-screen dim with a circular cutout + gold pulse ring over a highlight
    /// target. <see cref="Show(string)"/> follows a registry id;
    /// <see cref="ShowWorld(Transform)"/> follows a world anchor. <see cref="Hide"/>
    /// eases out. Never blocks input.
    /// </summary>
    public sealed class UiSpotlight : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;          // eased in/out ~200ms (unscaled)
        // Owner 2026-07-16 "instead of the circle have a GLOW to the button": the affordance was a
        // full-screen DIM with a hard circular ring cutout (read as "a circle around the button").
        // Now it is a soft GLOW halo ON the target, no screen dim. DimAlpha=0 removes the scrim; the
        // ring sprite is a wide feathered glow (see RingSprite). Nothing else dims the screen.
        private const float DimAlpha = 0f;               // was 0.62 — no screen dim; glow-on-target only
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

        private static Sprite _holeSprite;   // generated once, cached for the process
        private static Sprite _ringSprite;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Spotlight the target registered under <paramref name="highlightId"/>
        /// (TutorialHighlightRegistry). Re-resolves every frame, so late registration
        /// and moving targets both work; while unresolved the overlay stays hidden.</summary>
        public static void Show(string highlightId)
        {
            var s = Ensure();
            s._targetId = highlightId;
            s._worldAnchor = null;
            s._visible = true;
            s._tracedTarget = null;   // BM-3: re-log the resolved target/rect for the new id
            Diagnostics.FlowTrace.Step("Spotlight", $"show highlightId={highlightId}");
        }

        /// <summary>Spotlight a world-space anchor (projected via the main camera).</summary>
        public static void ShowWorld(Transform anchor)
        {
            var s = Ensure();
            s._targetId = null;
            s._worldAnchor = anchor;
            s._visible = anchor != null;
        }

        /// <summary>Ease the spotlight out. Safe when not shown.</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            _instance._targetId = null;
            _instance._worldAnchor = null;
            _instance._tracedTarget = null;   // BM-3: forget the last resolved target
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
            // Deliberately NO GraphicRaycaster — the overlay can never eat a tap.
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _rootRect = (RectTransform)transform;

            // Obsidian-tinted dim (not pure black) so the scrim reads as the kit's
            // warm-obsidian language; the gold pulse ring completes the black+gold pairing.
            Color dim = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                  ElarionUiKit.ObsidianFill.b, DimAlpha);
            for (int i = 0; i < 4; i++)
            {
                var r = new GameObject("Dim" + i, typeof(RectTransform), typeof(Image));
                r.transform.SetParent(transform, false);
                var img = r.GetComponent<Image>();
                img.color = dim;
                img.raycastTarget = false;
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
            if (_fadeT <= 0f) return;

            if (!TryTargetScreenRect(out Rect target))
            {
                // Unresolved target — keep the overlay invisible rather than dimming
                // the whole screen with no window (degrade gracefully, never wedge).
                _group.alpha = 0f;
                return;
            }

            LayoutHole(target);

            // Gold rim breath — subtle scale + alpha pulse.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / RingPulsePeriod));
            _ring.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                                    Mathf.Lerp(0.45f, 0.95f, pulse));
            _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.06f, pulse);
        }

        // ── Target resolution ─────────────────────────────────────────────────

        private bool TryTargetScreenRect(out Rect rect)
        {
            rect = default;

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
            Diagnostics.FlowTrace.Step("Spotlight",
                $"show highlightId={_targetId} target={targetName} " +
                $"rect=({rect.x:F0},{rect.y:F0},{rect.width:F0},{rect.height:F0})");
        }

        private static Rect ScreenRectOf(RectTransform rt)
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

        private static bool TryWorldToScreen(Vector3 world, out Rect rect)
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
            _holeSprite = MakeRadialSprite((dist01) =>
                Mathf.Clamp01(Mathf.InverseLerp(0.92f, 0.99f, dist01)));   // 0 inside → 1 at rim
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
