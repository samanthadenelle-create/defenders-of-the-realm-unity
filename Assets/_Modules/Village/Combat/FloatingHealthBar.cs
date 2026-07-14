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
using DeNelle.Core.UI;   // P23: RpgUiCatalog nameplate art (sprite-first, §1.10)

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
        // WO-302: compact slim chip. The world-space canvas renders sizeDelta as
        // world metres (scale-compensated to ~1), so these ARE the bar's world
        // dimensions. The old 1.5 × 0.18 m read as a giant green pill over a ~2 m
        // enemy; a ~0.9 m wide, 0.11 m tall bar reads as a proportional slim HP
        // indicator above the head.
        private Vector2 _barSize = new Vector2(0.9f, 0.11f);
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
        private bool _usesPackFill;   // P23: true when the Blink nameplate fill art bound
        private Transform _cam;
        private bool _built;

        // WO-512 slice 3: LOCK highlight. The "engage to reveal" model lit the bar but gave
        // the player's LOCKED target NO distinct world-space mark - it looked identical to any
        // damaged orc. These build a red outline ring + a bright "lock caret" chevron above the
        // bar, shown ONLY while _isTargeted, so the locked orc is unmistakable in 3D (this
        // survives the camera framing better than the tiny head reticle).
        private Image _lockOutline;   // red ring framing the chip while locked
        private Image _lockCaret;     // bright marker above the bar while locked
        private static readonly Color LockHi = new Color(1f, 0.22f, 0.18f, 1f);   // loud lock red

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

            // Difficulty tell (reuse, do not duplicate): give EVERY enemy nameplate the
            // over-head ThreatSkullPlate warning — previously only RegionMobSpawner roamers
            // got one. Self-resolves the Enemy; a no-op on the hero's bar (no Enemy). The
            // spawner's explicit Attach (ZoneManager threat) still overrides it for region
            // mobs — Attach reuses the same component, so nothing double-stacks.
            if (host.GetComponent<Enemy>() != null) ThreatSkullPlate.AttachAuto(host);
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

            // W7 (WO-714) — sprite-FIRST plate: when the mirrored Blink nameplate
            // plate art resolves (enemy bg for enemies, Nameplate_Bar for the hero —
            // the same enemy-vs-friendly split the fill below already uses), the
            // frame layer wears the REAL pack plate and the procedural gold rim is
            // skipped (the pack plate carries its own chrome). Null art keeps the
            // pre-existing rim + arcane-violet chip look — the bar can never blank.
            var packPlate = RpgUiCatalog.Get(RpgUiCatalog.RoleHud,
                _destroyOnDead ? RpgUiCatalog.HudNameplateEnemyBg : RpgUiCatalog.HudNameplateBar);

            if (packPlate == null)
            {
                // Gold rim (slightly larger backing plate behind the frame).
                var rimGo = new GameObject("Rim");
                rimGo.transform.SetParent(canvasGo.transform, false);
                var rimImg = rimGo.AddComponent<Image>();
                rimImg.sprite = chip; rimImg.type = Image.Type.Simple;
                rimImg.color = RimColor;
                StretchToCanvas(rimGo.GetComponent<RectTransform>(), -0.035f);
            }

            // Frame plate — pack nameplate plate when present, arcane-violet chip fallback.
            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(canvasGo.transform, false);
            var frameImg = frameGo.AddComponent<Image>();
            if (packPlate != null)
            {
                frameImg.sprite = packPlate; frameImg.type = Image.Type.Simple;
                frameImg.color = Color.white;   // pack art's own forged-steel look is the chrome
                StretchToCanvas(frameGo.GetComponent<RectTransform>(), -0.028f);
            }
            else
            {
                frameImg.sprite = chip; frameImg.type = Image.Type.Simple;
                frameImg.color = FrameColor;
                StretchToCanvas(frameGo.GetComponent<RectTransform>(), -0.018f);
            }
            frameImg.raycastTarget = false;

            // Empty track.
            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(canvasGo.transform, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = chip; trackImg.type = Image.Type.Simple;
            trackImg.color = TrackColor;
            StretchToCanvas(trackGo.GetComponent<RectTransform>(), 0f);

            // Fill — P23 CONVERSION to THE ONE FILL-BINDING CONTRACT (HUD_OBSIDIAN §1.1,
            // the same law that fixes the 9/145 bug): NON-NULL sprite + Filled/Horizontal/
            // Left + fillAmount as the ONLY width mutation. This kills the old sizeDelta-
            // width pattern (the :466 site the architecture doc names). Sprite-first: the
            // pack's nameplate fill for the unit kind (enemy vs friendly), chip fallback.
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            _fillImg = fillGo.AddComponent<Image>();
            var packFill = RpgUiCatalog.Get(RpgUiCatalog.RoleHud,
                _destroyOnDead ? RpgUiCatalog.HudNameplateHealthEnemy : RpgUiCatalog.HudNameplateHealth);
            _usesPackFill = packFill != null;
            _fillImg.sprite = _usesPackFill ? packFill : chip;   // never null (§1.1 rule 1)
            _fillImg.type = Image.Type.Filled;
            _fillImg.fillMethod = Image.FillMethod.Horizontal;
            _fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImg.color = _usesPackFill ? Color.white : HealthyColor;
            _fillImg.raycastTarget = false;
            _fillImg.fillAmount = 1f;
            _fillRect = fillGo.GetComponent<RectTransform>();
            StretchToCanvas(_fillRect, 0f);   // full-stretch; fillAmount does the width

            // WO-512 slice 3: LOCK highlight overlay - a red outline ring grown OUTWARD past
            // the chip + a bright caret marker above it. Both start hidden; LateUpdate enables
            // them only while this unit is the player's locked/current target.
            var outlineGo = new GameObject("LockOutline");
            outlineGo.transform.SetParent(canvasGo.transform, false);
            _lockOutline = outlineGo.AddComponent<Image>();
            _lockOutline.sprite = chip; _lockOutline.type = Image.Type.Simple;
            _lockOutline.color = LockHi;
            _lockOutline.raycastTarget = false;
            // Grow OUTWARD so it frames the bar as a red border (drawn first = behind the chip).
            StretchToCanvas(_lockOutline.GetComponent<RectTransform>(), -0.06f);
            outlineGo.transform.SetAsFirstSibling();   // behind rim/frame/track/fill
            outlineGo.SetActive(false);

            // Bright caret/marker centered above the bar (a small downward-pointing chip).
            var caretGo = new GameObject("LockCaret");
            caretGo.transform.SetParent(canvasGo.transform, false);
            _lockCaret = caretGo.AddComponent<Image>();
            _lockCaret.sprite = chip; _lockCaret.type = Image.Type.Simple;
            _lockCaret.color = LockHi;
            _lockCaret.raycastTarget = false;
            var caretRt = _lockCaret.GetComponent<RectTransform>();
            caretRt.anchorMin = new Vector2(0.5f, 1f);
            caretRt.anchorMax = new Vector2(0.5f, 1f);
            caretRt.pivot     = new Vector2(0.5f, 0f);
            caretRt.sizeDelta = new Vector2(_barSize.y * 1.2f, _barSize.y * 1.2f);
            caretRt.anchoredPosition = new Vector2(0f, _barSize.y * 0.6f);   // just above the bar
            caretGo.transform.rotation = Quaternion.Euler(0f, 0f, 45f);      // diamond marker
            caretGo.SetActive(false);
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

            // WO-302 follow-on: _heightOffset is a WORLD-space height (callers derive
            // it from world renderer bounds). The canvas is a child, so its
            // localPosition.y is multiplied by the host's lossyScale.y — on a large
            // (e.g. ~100×) People-family host that flung the bar hundreds of metres
            // up ("green bar floating mid-field with no owner"). Convert the world
            // offset into the host's local units so the bar sits the same world
            // distance above the head regardless of host scale.
            _canvas.transform.localPosition = new Vector3(0f, _heightOffset * InvAxis(host.y), 0f);
        }

        // Inverse of one lossyScale axis, made safe.
        //
        // WO-302 ROOT CAUSE (re-fix 2026-06-12): the previous version clamped the
        // DIVISOR to [0.05, 50] before inverting. The orc/troll People-family
        // meshes import at a LARGE root scale (> 50). When the host scale exceeded
        // 50 the divisor was capped at 50, so the canvas world scale ended up
        // host/50 (e.g. a host scaled ~100 → bar rendered ~2× = the persistent
        // "giant green pill"). Capping the divisor is exactly wrong: the bigger
        // the host, the MORE compensation it needs, not less.
        //
        // Fix: divide by the host's ACTUAL scale so the canvas world scale is
        // truly ~1 on every axis no matter how large the host. We keep ONLY the
        // genuine safety guards — a non-finite scale, and a near-zero floor so a
        // degenerate (collapsed) host can't divide toward infinity.
        private static float InvAxis(float s)
        {
            if (float.IsNaN(s) || float.IsInfinity(s)) return 1f;
            float a = Mathf.Abs(s);
            if (a < 0.0001f) return 1f;   // degenerate / collapsed host → don't amplify
            return 1f / a;                // full per-axis compensation (no upper cap)
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

        // ── DEF-206 / WO-302: rounded-rect chip sprite (cached, drawn once) ───
        // WO-302 ROOT CAUSE of the "giant green pill": the chip layers used
        // Image.Type.Sliced with a 9-slice border. A SLICED image cannot render
        // smaller than the SUM of its borders — here radius(10px)+radius(10px) =
        // 20px of border per axis at 100 px/unit = 0.20 world-units MINIMUM on
        // BOTH width and height. Our bar is only 0.11 m tall, so the sliced Image
        // was floored to ≥0.20 m tall and the full-height rounded corners merged
        // into a fat green oval/pill — independent of (and surviving) the host-
        // scale fix. The layers now draw with Image.Type.Simple (set in BuildUi),
        // which stretches the whole texture to the rect with NO minimum-size floor,
        // so a 0.9 × 0.11 m bar renders exactly that size. To keep nice corners at
        // any width under Simple (which scales the corners with the quad rather
        // than holding them fixed), the radius is a modest fraction of the texture
        // so a wide thin bar still reads as a rounded chip, not a stretched blob.
        // Generated at runtime (no art asset / no UXML), WebGL-safe.
        private static Sprite _chipSprite;
        private static Sprite ChipSprite()
        {
            if (_chipSprite != null) return _chipSprite;

            const int S = 32;             // square source texture
            const float radius = 6f;      // corner radius in px (modest under Simple scaling)
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
            // No 9-slice border (Vector4.zero): the chip is drawn with Image.Type.Simple
            // so it has no minimum-size floor and can render at the bar's true 0.11 m height.
            _chipSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, Vector4.zero);
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

            // WO-512 slice 3: show the LOCK highlight (red outline + caret) ONLY while this
            // unit is the player's locked/current target, so the locked orc is unmistakable.
            bool showLock = _isTargeted;
            if (_lockOutline != null && _lockOutline.gameObject.activeSelf != showLock)
                _lockOutline.gameObject.SetActive(showLock);
            if (_lockCaret != null && _lockCaret.gameObject.activeSelf != showLock)
                _lockCaret.gameObject.SetActive(showLock);
            if (showLock && _lockCaret != null)
            {
                // Gentle pulse so the lock marker reads as a live, deliberate highlight.
                float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                var lc = LockHi; lc.a = pulse;
                _lockCaret.color = lc;
            }

            if (_fillImg != null)
            {
                // §1.1 rule 3: the ONLY width mutation is fillAmount (never sizeDelta).
                _fillImg.fillAmount = frac;

                if (_usesPackFill)
                {
                    // Coloured pack fill stays white; only the critical pulse rides alpha.
                    var pc = Color.white;
                    if (frac <= CritThreshold)
                        pc.a = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
                    _fillImg.color = pc;
                }
                else
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
