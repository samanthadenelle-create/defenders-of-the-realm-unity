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
//   full HP + idle     → hidden (declutters the field; pops in on first damage)
//   dead               → hidden
//
// DEF-206 — "engage to reveal" visibility model (kills the raw-green-bar-on-
// everything noise the owner flagged). A bar shows only while the unit is
// ENGAGED: it took damage (HP < max), OR it is the player's locked / current
// target, OR it was either of those within the last few seconds. When combat
// ends the bar fades out (canvas alpha) and the field goes quiet again. The bar
// styles as a slim rounded chip (dark backing + gold rim + green→amber→red
// fill), not a raw quad; the player's locked target reads distinctly because the
// reticle owns the lock highlight and this bar simply stays lit while targeted.
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
        private Vector2 _barSize = new Vector2(1.5f, 0.18f);
        // DEF-206: when true the bar stays hidden until the unit is ENGAGED
        // (damaged / targeted / recently in combat), then fades back out. When
        // false (the hero) the bar is always present so you can locate yourself.
        private bool _hideUntilEngaged = true;
        private bool _destroyOnDead = true; // enemies tear down; the hero respawns → just hide

        // ── DEF-206 engagement state ──────────────────────────────────────────
        // How long the bar lingers (visible) after the last engagement before it
        // fades away again, and how fast it fades in / out.
        private const float LingerSeconds = 3.5f;
        private const float FadeSpeed     = 6f;   // alpha units / second
        private float _lastEngaged = -999f;       // Time.time of last damage / target ping
        private bool  _isTargeted;                // currently the player's locked / current target
        private float _lastFraction = 1f;         // to detect HP drops (= took damage)
        private CanvasGroup _group;               // drives the fade
        private float _alpha;                     // current eased alpha (0..1)

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

        /// <summary>
        /// DEF-206: convenience — find the bar on (or under) <paramref name="host"/>
        /// and flag it as the player's current target so it stays lit while locked.
        /// Pass <paramref name="targeted"/> false to release it (it then lingers a
        /// few seconds and fades). Safe to call on a host with no bar (no-op).
        /// </summary>
        public static void SetTargetedOn(GameObject host, bool targeted)
        {
            if (host == null) return;
            var bar = host.GetComponent<FloatingHealthBar>();
            if (bar != null) bar.SetTargeted(targeted);
        }

        /// <summary>DEF-206: mark this unit as the player's current/locked target.
        /// While targeted the bar is always shown; on release it lingers then fades.</summary>
        public void SetTargeted(bool targeted)
        {
            _isTargeted = targeted;
            if (targeted) _lastEngaged = Time.time;
        }

        /// <summary>DEF-206: ping the engagement timer (the unit is in active combat).
        /// Damage is auto-detected, but callers can force a reveal too.</summary>
        public void MarkEngaged() => _lastEngaged = Time.time;

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
            _fraction         = fraction;
            _isDead           = isDead;
            _heightOffset     = Mathf.Clamp(heightOffset, 0.5f, MaxHeightOffset);
            _hideUntilEngaged = hideAtFull;
            _destroyOnDead    = destroyOnDead;
            _lastFraction     = fraction != null ? Mathf.Clamp01(fraction()) : 1f;
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
            // DEF-206: a CanvasGroup drives a smooth fade in/out so the bar slides
            // away after combat instead of popping off — start hidden.
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var crt = _canvas.GetComponent<RectTransform>();
            crt.sizeDelta = _barSize;
            // Keep the bar a constant on-screen size regardless of the host's scale.
            // WO-302: enemies (scaled Humanoid orc/troll family) are imported at
            // wildly different — and NON-UNIFORM — scales. The old fix divided only
            // by lossyScale.X and set the canvas localScale to that uniform value, so
            // the canvas's WORLD scale on Y/Z stayed equal to the host's Y/Z scale.
            // The RectTransform lives in the XY plane → the bar's HEIGHT inflated by
            // the host's Y scale, giving the "giant green pill/oval" (a fat oval, not
            // a slim chip). Cancel the host scale PER-AXIS so the canvas world scale
            // is ~1 on every axis. ApplyScaleCompensation also re-runs each frame so
            // a late-settling rig or a runtime rescale can never re-inflate the bar.
            ApplyScaleCompensation();

            // DEF-206: a soft rounded-rect sprite gives the chip rounded corners +
            // a subtle inner softness so it reads as a styled bar, not a raw quad.
            Sprite chip = ChipSprite();

            // Gold rim (slightly larger backing plate behind the frame).
            var rimGo = new GameObject("Rim");
            rimGo.transform.SetParent(canvasGo.transform, false);
            var rimImg = rimGo.AddComponent<Image>();
            rimImg.sprite = chip; rimImg.type = Image.Type.Sliced;
            rimImg.color = RimColor;
            StretchToCanvas(rimGo.GetComponent<RectTransform>(), -0.035f);

            // Arcane-violet frame plate.
            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(canvasGo.transform, false);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.sprite = chip; frameImg.type = Image.Type.Sliced;
            frameImg.color = FrameColor;
            StretchToCanvas(frameGo.GetComponent<RectTransform>(), -0.018f);

            // Empty track.
            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(canvasGo.transform, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = chip; trackImg.type = Image.Type.Sliced;
            trackImg.color = TrackColor;
            StretchToCanvas(trackGo.GetComponent<RectTransform>(), 0f);

            // Fill (left-anchored, width scaled by fraction).
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            _fillImg = fillGo.AddComponent<Image>();
            _fillImg.sprite = chip; _fillImg.type = Image.Type.Sliced;
            _fillImg.color = HealthyColor;
            _fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot     = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
            _fillRect.sizeDelta = new Vector2(_barSize.x, 0f);
        }

        // WO-302: cancel the host transform's (possibly non-uniform, possibly huge
        // or near-zero) world scale PER-AXIS so the world-space canvas — and hence
        // the bar — renders at a constant on-screen size. Each component is the
        // inverse of the host's lossyScale on that axis, clamped to a sane band and
        // guarded against zero / NaN / Inf (mis-imported & displaced meshes report
        // these). Cheap enough to run every LateUpdate, which also fixes rigs whose
        // final scale isn't settled until after Start.
        private void ApplyScaleCompensation()
        {
            if (_canvas == null) return;
            Vector3 host = transform.lossyScale;
            _canvas.transform.localScale = new Vector3(
                InvAxis(host.x), InvAxis(host.y), InvAxis(host.z));
        }

        // Inverse of one lossyScale axis, made safe: a non-finite or near-zero scale
        // collapses to 1 (no compensation) instead of dividing toward infinity and
        // blowing the bar up to fill the screen.
        private static float InvAxis(float s)
        {
            if (float.IsNaN(s) || float.IsInfinity(s)) return 1f;
            float a = Mathf.Abs(s);
            if (a < 0.01f) return 1f;                 // degenerate → don't amplify
            return 1f / Mathf.Clamp(a, 0.05f, 50f);
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

        // ── DEF-206: rounded-rect chip sprite (cached, drawn once) ────────────
        // A 9-sliced white rounded-rect so every chip layer gets rounded corners
        // at any width. Generated at runtime (no art asset / no UXML), WebGL-safe.
        private static Sprite _chipSprite;
        private static Sprite ChipSprite()
        {
            if (_chipSprite != null) return _chipSprite;

            const int S = 32;             // texture is square; sliced borders stretch the middle
            const float radius = 10f;     // corner radius in px
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    // Signed distance to the rounded-rect edge → soft 1px antialiased border.
                    float dx = Mathf.Max(0f, radius - Mathf.Min(x, S - 1 - x));
                    float dy = Mathf.Max(0f, radius - Mathf.Min(y, S - 1 - y));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - dist);   // 1 inside, fades over the last px
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            // border = radius so the corners are preserved and only the centre stretches.
            _chipSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _chipSprite;
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

            // DEF-206: auto-detect damage. Any drop in the HP fraction since last
            // frame is a hit → ping the engagement timer so the bar reveals + lingers
            // (no need to touch every damage path; the fraction delegate is the truth).
            if (frac < _lastFraction - 0.0005f) _lastEngaged = Time.time;
            _lastFraction = frac;

            // ── "Engage to reveal" rule ─────────────────────────────────────────
            // The bar wants to be visible when the unit is ENGAGED:
            //   • not in hide-until-engaged mode at all (the hero — always on), OR
            //   • currently the player's locked/current target, OR
            //   • damaged (HP below full), OR
            //   • within the linger window after the last damage / target ping.
            bool engaged =
                !_hideUntilEngaged
                || _isTargeted
                || frac < 0.999f
                || (Time.time - _lastEngaged) < LingerSeconds;

            // Ease the alpha toward the want-state for a smooth fade in/out.
            float wantAlpha = engaged ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, wantAlpha, FadeSpeed * Time.deltaTime);
            if (_group != null) _group.alpha = _alpha;

            // Fully faded out → disable the canvas (saves the billboard / layout cost)
            // until it is engaged again.
            bool active = _alpha > 0.001f;
            if (_canvas.enabled != active) _canvas.enabled = active;
            if (!active) return;

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

            // WO-302: keep cancelling the host scale every frame so a late-settling
            // rig or a runtime rescale can never re-inflate the bar into a giant pill.
            ApplyScaleCompensation();

            // Billboard toward the camera — but PINNED UPRIGHT (WO-302 follow-on).
            // The old LookRotation(dir) with no up-vector let the quad tip edge-on
            // when the camera looked DOWN at a bar floating above an enemy, so the
            // near-top-down / over-shoulder view saw the bar in profile and it read
            // as a thin green oval/disc instead of a horizontal HP bar. Supplying
            // WORLD-UP as the up vector keeps the bar's local-up locked to world +Y,
            // so the quad always stays vertical (a true upright billboard) and the
            // bar reads as a flat horizontal HP bar from any camera angle, including
            // near-top-down. (Camera-facing on yaw; never tips flat.)
            if (_cam == null)
            {
                var cm = Camera.main;
                _cam = cm != null ? cm.transform : null;
            }
            if (_cam != null)
            {
                Vector3 toCam = _canvas.transform.position - _cam.position;
                // Guard the degenerate near-vertical case (camera straight overhead):
                // a look-direction nearly parallel to world-up makes LookRotation's
                // up reference unstable. Fall back to the camera's own up then.
                if (toCam.sqrMagnitude > 1e-6f)
                {
                    Vector3 flat = new Vector3(toCam.x, 0f, toCam.z);
                    Vector3 fwd  = flat.sqrMagnitude > 1e-6f ? flat : toCam;
                    _canvas.transform.rotation =
                        Quaternion.LookRotation(fwd, Vector3.up);
                }
            }
        }
    }
}
