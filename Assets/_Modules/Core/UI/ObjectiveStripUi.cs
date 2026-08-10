// =============================================================================
// ObjectiveStripUi — WO-1012 §2b kit piece 3: the thin bottom-center objective
// strip. REPLACES ObjectiveBannerUi (the fat top banner + its "(0/1)" counter)
// as the tutorial's objective surface.
// -----------------------------------------------------------------------------
// One objective sentence + progress BEADS (filled disc = done, hollow ring =
// remaining — SHAPE difference, never colour alone; the beads always pair with
// the sentence, per the colourblind law). Fixed-PIXEL band (the fraction-band
// lesson) anchored just ABOVE the actionBar area (HudAreasHost tops it at
// y-fraction 0.150), which makes its band DISJOINT from the F8/dev overlays —
// those live along the TOP edge (BreakCaptureHarness draws its FLAGGED box at
// OnGUI top-left; the old top-center banner is what collided, WO-1010 D2).
//
// Code-built uGUI in the kit language (obsidian plate + gold rule + parchment
// text), UNSCALED-time fades, NO raycaster at all — nothing here is
// interactive (the ONE Skip is its own corner control, TutorialSkipUi).
// Suppresses under any arbiter-tracked modal exactly like the banner did
// (WO-795) and restores when it closes. [Flow:Tutorial] on show/hide.
// =============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static thin bottom-center objective strip: ONE sentence + progress beads.
    /// <see cref="Show"/> sets/replaces the objective, <see cref="SetProgress"/>
    /// advances the beads, <see cref="Hide"/> eases out.
    /// </summary>
    public sealed class ObjectiveStripUi : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;
        private const int CanvasSortOrder = 4300;    // the banner's old slot: above mask + pointer
        // Fixed-PIXEL strip band (the fraction-band lesson): the plate is a constant
        // 620x40 px; only its ANCHOR rides a screen fraction, pinned just above the
        // actionBar area's top edge (HudAreasHost: actionBar y 0.015-0.150).
        private const float StripWidth = 620f;
        private const float StripHeight = 40f;
        private const float AnchorYFraction = 0.158f;  // just above actionBar top (0.150)
        private const float BeadPx = 12f;
        private const float BeadGapPx = 7f;
        private const int MaxBeads = 12;             // sanity bound — the FTUE chain is ~6-8 steps

        private static ObjectiveStripUi _instance;

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private RectTransform _beadRow;
        private Image[] _beads = new Image[0];
        private int _total;
        private int _done;
        private bool _visible;
        private float _fadeT;
        private bool _modalSuppressed;

        private static Sprite _discSprite;   // filled bead (done)
        private static Sprite _ringSprite;   // hollow bead (remaining)

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show (or update) the strip: ONE objective sentence + beads showing
        /// <paramref name="done"/> of <paramref name="total"/> beats complete. A
        /// non-positive <paramref name="total"/> hides the bead row (sentence only).</summary>
        public static void Show(string text, int done = 0, int total = 0)
        {
            var s = Ensure();
            s._visible = true;
            if (s._label != null) s._label.text = text ?? "";
            s._total = Mathf.Clamp(total, 0, MaxBeads);
            s._done = Mathf.Clamp(done, 0, s._total);
            s.RebuildBeads();
            Diagnostics.FlowTrace.Step("Tutorial",
                $"ObjectiveStrip SHOW '{text}' progress={done}/{total}");
        }

        /// <summary>Advance the progress beads (objective sentence unchanged).</summary>
        public static void SetProgress(int done)
        {
            if (_instance == null) return;
            _instance._done = Mathf.Clamp(done, 0, _instance._total);
            _instance.PaintBeads();
        }

        /// <summary>Ease the strip out. Safe when not shown.</summary>
        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;
            Diagnostics.FlowTrace.Step("Tutorial", "ObjectiveStrip HIDE");
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static ObjectiveStripUi Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ObjectiveStrip");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ObjectiveStripUi>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Deliberately NO GraphicRaycaster — the strip is pure display; the ONE
            // Skip affordance lives on its own corner control (TutorialSkipUi).
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Plate — thin obsidian glass strip, bottom-center, fixed px.
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(transform, false);
            var prt = (RectTransform)plate.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, AnchorYFraction);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(StripWidth, StripHeight);
            var pimg = plate.GetComponent<Image>();
            pimg.color = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                   ElarionUiKit.ObsidianFill.b, 0.86f);
            pimg.raycastTarget = false;

            // Gold accent rule along the TOP edge (the strip hangs under the play area,
            // so the rule reads as its upper seam — kit chrome vocabulary).
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(prt, false);
            var rrt = (RectTransform)rule.transform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(0f, 2f);
            var rimg = rule.GetComponent<Image>();
            rimg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
            rimg.raycastTarget = false;

            // The one objective sentence — left-aligned, ellipsized, never wrapped.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(prt, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(14f, 0f);
            trt.offsetMax = new Vector2(-(MaxBeads * (BeadPx + BeadGapPx) * 0.5f + 20f), 0f);
            _label = textGo.AddComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_label);
            _label.fontSize = 17f;
            _label.color = ElarionUi.Parchment;
            _label.alignment = TextAlignmentOptions.MidlineLeft;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            _label.raycastTarget = false;

            // Bead row host — right end of the strip; beads laid out right-to-left.
            var row = new GameObject("Beads", typeof(RectTransform));
            row.transform.SetParent(prt, false);
            _beadRow = (RectTransform)row.transform;
            _beadRow.anchorMin = new Vector2(1f, 0.5f);
            _beadRow.anchorMax = new Vector2(1f, 0.5f);
            _beadRow.pivot = new Vector2(1f, 0.5f);
            _beadRow.anchoredPosition = new Vector2(-14f, 0f);
            _beadRow.sizeDelta = new Vector2(0f, StripHeight);
        }

        /// <summary>(Re)build the bead images to match <see cref="_total"/> and paint them.</summary>
        private void RebuildBeads()
        {
            if (_beadRow == null) return;
            if (_beads.Length != _total)
            {
                for (int i = _beadRow.childCount - 1; i >= 0; i--)
                    Destroy(_beadRow.GetChild(i).gameObject);
                _beads = new Image[_total];
                for (int i = 0; i < _total; i++)
                {
                    var b = new GameObject("Bead" + i, typeof(RectTransform), typeof(Image));
                    b.transform.SetParent(_beadRow, false);
                    var brt = (RectTransform)b.transform;
                    brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
                    brt.pivot = new Vector2(1f, 0.5f);
                    // Right-to-left: bead 0 (the first beat) ends up leftmost.
                    brt.anchoredPosition = new Vector2(-(_total - 1 - i) * (BeadPx + BeadGapPx), 0f);
                    brt.sizeDelta = new Vector2(BeadPx, BeadPx);
                    _beads[i] = b.GetComponent<Image>();
                    _beads[i].raycastTarget = false;
                }
            }
            PaintBeads();
        }

        /// <summary>Done = FILLED gold disc; remaining = HOLLOW parchment ring — the
        /// state is carried by SHAPE (filled vs hollow), colour is reinforcement only.</summary>
        private void PaintBeads()
        {
            for (int i = 0; i < _beads.Length; i++)
            {
                if (_beads[i] == null) continue;
                bool doneBead = i < _done;
                _beads[i].sprite = doneBead ? DiscSprite() : RingSprite();
                _beads[i].color = doneBead
                    ? ElarionUi.Gold
                    : new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.55f);
            }
        }

        // WO-795 rule inherited from the banner: while ANY arbiter-tracked modal owns
        // the screen the strip suppresses (fades out) and restores on close. Caller
        // state is untouched — it picks back up exactly where it was.
        private void Update()
        {
            bool modal = PanelManager.AnyOpen;
            if (modal != _modalSuppressed)
            {
                _modalSuppressed = modal;
                if (_visible)
                    Diagnostics.FlowTrace.Step("Tutorial", modal
                        ? "ObjectiveStrip suppressed - modal open ('" + (PanelManager.OpenPanelName ?? "?") + "')"
                        : "ObjectiveStrip restored - modal closed");
            }
            bool shown = _visible && !_modalSuppressed;
            float dir = shown ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            _group.alpha = _fadeT * _fadeT * (3f - 2f * _fadeT);
        }

        // ── Generated bead sprites (once per process) ─────────────────────────

        private static Sprite DiscSprite()
        {
            if (_discSprite != null) return _discSprite;
            _discSprite = MakeRadial((d) => 1f - Mathf.Clamp01(Mathf.InverseLerp(0.80f, 1.0f, d)));
            return _discSprite;
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            _ringSprite = MakeRadial((d) =>
            {
                // Hollow ring: a band near the rim, clear centre.
                float band = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.80f) / 0.16f);
                return band * band;
            });
            return _ringSprite;
        }

        private static Sprite MakeRadial(System.Func<float, float> alphaByDist)
        {
            const int N = 32;
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
                    px[y * N + x] = new Color(1f, 1f, 1f, alphaByDist(dist));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
