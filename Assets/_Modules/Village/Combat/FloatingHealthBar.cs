// =============================================================================
// FloatingHealthBar (WO-178) — code-built world-space HP bar over a combat unit.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small floating bar above an Enemy or the Hero that reflects the unit's live
// HP fraction. Built ENTIRELY in C# (uGUI world-space Canvas + Image) — no UXML /
// UIDocument, which do not render in player builds (PIPELINE_STATE.md §8,
// CLAUDE.md memory). Mirrors the NodeFillIndicator world-space-bar pattern and is
// styled to match the themed HUD language (arcane-violet frame, gold rim, the
// HUD's green→amber→red HP states) so combat units read as part of the same set.
//
// SELF-CONTAINED + ZERO PREFAB WIRING:
//   • Enemy.cs auto-adds it (EnsureHealthBar) and feeds it HpFraction / IsDead.
//   • HeroHealthBootstrap auto-adds it to the hero and feeds it Fraction / !IsAlive.
//   • It reads the unit through two delegates only — it never knows the concrete
//     type and so DeNelle.Village → Core asmdef rules are untouched.
//
// STATES:
//   healthy (> warn)   → themed green fill
//   warning (≤ warn)   → amber fill
//   critical (≤ crit)  → red fill + gentle pulse
//   full HP            → hidden (declutters the field; pops in on first damage)
//   dead               → hidden
// =============================================================================
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Code-built world-space HP bar floated over a combat unit (Enemy / Hero).
    /// Configure once with <see cref="Init"/>; it polls the supplied delegates each
    /// <see cref="LateUpdate"/> and self-destroys when the unit reports dead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingHealthBar : MonoBehaviour
    {
        // ── Themed palette (echoes VillageHudController / the quest-panel cards) ──
        private static readonly Color FrameColor    = new Color(0.10f, 0.08f, 0.16f, 0.92f); // arcane-violet card
        private static readonly Color RimColor      = new Color(1f,    0.86f, 0.45f, 0.85f);  // themed gold rim
        private static readonly Color TrackColor    = new Color(0.18f, 0.16f, 0.24f, 0.95f);  // empty track
        private static readonly Color HealthyColor  = new Color(0.30f, 0.78f, 0.40f, 1f);     // themed green
        private static readonly Color WarningColor  = new Color(1f,    0.74f, 0.18f, 1f);      // amber
        private static readonly Color CriticalColor = new Color(0.86f, 0.12f, 0.10f, 1f);      // red

        // HP-fraction thresholds for the colour states.
        private const float WarnThreshold = 0.55f;
        private const float CritThreshold = 0.25f;

        // ── Config (set via Init) ─────────────────────────────────────────────
        private Func<float> _fraction;     // 0..1 HP fraction supplier
        private Func<bool>  _isDead;        // true once the unit is dead
        private float _heightOffset = 2.4f; // world-units above the unit pivot
        private Vector2 _barSize = new Vector2(1.5f, 0.20f);
        private bool _hideAtFull = true;    // declutter: only show once damaged
        private bool _destroyOnDead = true; // enemies tear down; the hero respawns → just hide

        // ── Runtime refs ──────────────────────────────────────────────────────
        private Canvas _canvas;
        private RectTransform _fillRect;
        private Image _fillImg;
        private Transform _cam;
        private bool _built;

        /// <summary>
        /// Attach (or reuse) a floating HP bar on <paramref name="host"/>. The bar
        /// reads the unit via the supplied delegates so it stays type-agnostic.
        /// </summary>
        /// <param name="host">GameObject the bar floats over.</param>
        /// <param name="fraction">Returns the unit's 0..1 HP fraction.</param>
        /// <param name="isDead">Returns true once the unit has died.</param>
        /// <param name="heightOffset">Bar height above the host pivot (world units).</param>
        /// <param name="hideAtFull">When true the bar stays hidden until first damaged.</param>
        /// <param name="destroyOnDead">
        /// When true (enemies) the bar destroys itself once the unit reports dead;
        /// when false (the respawning hero) it merely hides and recovers on revive.
        /// </param>
        public static FloatingHealthBar Attach(GameObject host, Func<float> fraction,
            Func<bool> isDead, float heightOffset = 2.4f, bool hideAtFull = true,
            bool destroyOnDead = true)
        {
            if (host == null || fraction == null) return null;
            var bar = host.GetComponent<FloatingHealthBar>();
            if (bar == null) bar = host.AddComponent<FloatingHealthBar>();
            bar.Init(fraction, isDead, heightOffset, hideAtFull, destroyOnDead);
            return bar;
        }

        // Sane clamp for the head offset. Callers derive this from world-space
        // renderer bounds, and a mis-scaled / displaced (e.g. Tripo) mesh can report
        // a bounds.max.y metres away from its pivot — which would fling the bar far
        // from its owner and leave a green bar "floating mid-field with no owner".
        // Clamp it so the bar always sits just over the unit it belongs to.
        private const float MaxHeightOffset = 4.0f;

        /// <summary>(Re)configures the bar's data source and layout.</summary>
        public void Init(Func<float> fraction, Func<bool> isDead,
            float heightOffset = 2.4f, bool hideAtFull = true, bool destroyOnDead = true)
        {
            _fraction      = fraction;
            _isDead        = isDead;
            _heightOffset  = Mathf.Clamp(heightOffset, 0.5f, MaxHeightOffset);
            _hideAtFull    = hideAtFull;
            _destroyOnDead = destroyOnDead;
            if (_built && _canvas != null)
                _canvas.transform.localPosition = new Vector3(0f, _heightOffset, 0f);
        }

        private void Start()
        {
            BuildUi();
            var c = Camera.main;
            _cam = c != null ? c.transform : null;
        }

        private void BuildUi()
        {
            if (_built) return;
            _built = true;

            // World-space canvas anchored above the unit.
            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, _heightOffset, 0f);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var crt = _canvas.GetComponent<RectTransform>();
            crt.sizeDelta = _barSize;
            // Keep the bar a constant ~1.5m wide regardless of the host's scale —
            // enemies are imported at wildly different scales, so divide it out.
            // Clamp the host-scale we divide by: a near-zero (or absurd) lossyScale
            // — common on mis-imported / displaced meshes — would otherwise blow the
            // bar up to fill the screen ("HUGE green bar floating mid-screen").
            float hostScale = Mathf.Clamp(transform.lossyScale.x, 0.05f, 50f);
            canvasGo.transform.localScale = Vector3.one / hostScale;

            // Gold rim (slightly larger backing plate behind the frame).
            var rimGo = new GameObject("Rim");
            rimGo.transform.SetParent(canvasGo.transform, false);
            var rimImg = rimGo.AddComponent<Image>();
            rimImg.color = RimColor;
            StretchToCanvas(rimGo.GetComponent<RectTransform>(), -0.035f);

            // Arcane-violet frame plate.
            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(canvasGo.transform, false);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.color = FrameColor;
            StretchToCanvas(frameGo.GetComponent<RectTransform>(), -0.018f);

            // Empty track.
            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(canvasGo.transform, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = TrackColor;
            StretchToCanvas(trackGo.GetComponent<RectTransform>(), 0f);

            // Fill (left-anchored, width scaled by fraction).
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            _fillImg = fillGo.AddComponent<Image>();
            _fillImg.color = HealthyColor;
            _fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot     = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
            _fillRect.sizeDelta = new Vector2(_barSize.x, 0f);
        }

        // Stretches a child rect to fill the canvas, optionally inset (negative
        // inset = grow outward, used for the rim plate).
        private static void StretchToCanvas(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private void LateUpdate()
        {
            if (_canvas == null) return;

            // Lost-owner guard: if the data source went away (delegate cleared) or
            // the host this bar belongs to is no longer active, hide the bar so it
            // can never linger as an ownerless green bar floating in the field.
            if (_fraction == null || !gameObject.activeInHierarchy)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            // Dead. Enemies destroy the bar so it doesn't linger over a corpse;
            // the respawning hero merely hides it (the unit comes back).
            if (_isDead != null && _isDead())
            {
                if (_destroyOnDead)
                {
                    Destroy(_canvas.gameObject);
                    _canvas = null;
                    enabled = false;
                }
                else if (_canvas.enabled)
                {
                    _canvas.enabled = false;
                }
                return;
            }

            float frac = _fraction != null ? Mathf.Clamp01(_fraction()) : 1f;

            // Declutter: hide a full-HP unit's bar until it has actually taken a hit.
            bool show = !_hideAtFull || frac < 0.999f;
            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show) return;

            if (_fillRect != null)
                _fillRect.sizeDelta = new Vector2(_barSize.x * frac, 0f);

            if (_fillImg != null)
            {
                Color c = frac <= CritThreshold ? CriticalColor
                        : frac <= WarnThreshold ? WarningColor
                        :                          HealthyColor;
                // Critical: gentle pulse so a near-dead unit reads at a glance.
                if (frac <= CritThreshold)
                {
                    float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
                    c.a = pulse;
                }
                _fillImg.color = c;
            }

            // Billboard toward the camera.
            if (_cam == null)
            {
                var cm = Camera.main;
                _cam = cm != null ? cm.transform : null;
            }
            if (_cam != null)
                _canvas.transform.rotation =
                    Quaternion.LookRotation(_canvas.transform.position - _cam.position);
        }
    }
}
