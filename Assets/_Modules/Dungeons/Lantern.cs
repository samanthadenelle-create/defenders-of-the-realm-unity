// =============================================================================
// Lantern — the Keeper's hero-attached dungeon lantern (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/lantern/ -> _Modules/Dungeons/Lantern.cs + LanternLight.prefab
// Port spec Part 5 Week 6: "PointLight attached to hero, intensity falls over
// time (oil mechanic). Refill at oil stones. Audio: lantern-flicker.mp3 at low oil."
//
// C# port of src/modules/dungeons/3d/lantern/DungeonLantern3D.tsx. The React
// component is a warm THREE.PointLight that follows the Keeper; this is its
// Unity incarnation — a `UnityEngine.Light` of type Point, parented to (or
// following) the hero rig.
//
// ── The oil mechanic (design §4 — the lantern intro/payoff beats) ──
// The lantern burns OIL. Oil drains slowly while the run is active; as oil
// drops, the light's range AND intensity fall with it, so the dark closes in.
// Walking into an oil stone's radius refills the flask. At low oil the
// lantern-flicker SFX plays and the flame breathing speeds up — a felt warning
// to find an oil stone.
//
// ── Tincture light-shrink (design §4 Beat 6) ──
// The Apothecary mini-boss's "Tincture" special shrinks the lantern reach to
// 50% for 6 seconds. C# port of lanternDebuff.ts — see TriggerTincture().
//
// The flame "breathes" — a slow intensity oscillation — UNLESS reduced motion
// is set, in which case the intensity is pinned to its mean (a calm light).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// The Keeper's lantern — a hero-following point light whose reach and
    /// brightness fall as oil drains, refilled at oil stones. Port of the React
    /// <c>DungeonLantern3D</c> + <c>lanternDebuff</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class Lantern : MonoBehaviour
    {
        // ── Tuning — radius model (DungeonLantern3D BASE_DISTANCE etc.) ───────

        [Header("Light reach (world units)")]
        [Tooltip("Always-on lantern reach at full oil — the §5-amendment base ~6u.")]
        [SerializeField] private float _baseRange = 6f;

        [Tooltip("Extra reach when the equipped Lightbearer Cloak is owned (+1 tile).")]
        [SerializeField] private float _cloakBonusRange = 1.5f;

        [Tooltip("The lantern's mean point-light intensity at full oil.")]
        [SerializeField] private float _baseIntensity = 9f;

        [Tooltip("Warm lantern colour (matches the gate-Wardens' torch tone).")]
        [SerializeField] private Color _lanternColor = new Color(0.965f, 0.788f, 0.478f);

        [Tooltip("Height above the hero pivot the lantern light sits — chest height.")]
        [SerializeField] private float _lanternHeight = 1.4f;

        // ── Tuning — oil mechanic ────────────────────────────────────────────

        [Header("Oil")]
        [Tooltip("Maximum oil in the flask (arbitrary units — a full flask).")]
        [SerializeField] private float _maxOil = 100f;

        [Tooltip("Oil drained per second while the run is active.")]
        [SerializeField] private float _oilDrainPerSec = 1.6f;

        [Tooltip("Below this fraction of max oil, the lantern reads as 'low oil' " +
                 "— the flicker SFX plays and the flame breathes faster.")]
        [SerializeField, Range(0f, 1f)] private float _lowOilFraction = 0.25f;

        [Tooltip("Floor on the oil-driven dimming — the lantern never fully dies, " +
                 "so an out-of-oil Keeper can still find an oil stone.")]
        [SerializeField, Range(0f, 1f)] private float _minOilLightFraction = 0.35f;

        // ── Tuning — breathing + Tincture (lanternDebuff.ts) ─────────────────

        [Header("Flame breathing")]
        [Tooltip("Breathing depth — +/- this fraction of the base intensity.")]
        [SerializeField, Range(0f, 0.5f)] private float _breathAmplitude = 0.12f;

        [Tooltip("Breathing rate at full oil — a slow ~0.2 Hz flame breath.")]
        [SerializeField] private float _breathHz = 0.2f;

        [Tooltip("Breathing-rate multiplier applied when oil is low — a faster, " +
                 "anxious flicker that reads as a warning.")]
        [SerializeField] private float _lowOilBreathMult = 3f;

        [Header("Tincture debuff (Apothecary boss — design §4 Beat 6)")]
        [Tooltip("Lantern reach multiplier while a Tincture is active (50%).")]
        [SerializeField, Range(0f, 1f)] private float _tinctureRangeMul = 0.5f;

        [Tooltip("How long a single Tincture cast dims the lantern (6s).")]
        [SerializeField] private float _tinctureDurationSec = 6f;

        [Tooltip("How fast the reach eases toward its target each second.")]
        [SerializeField] private float _rangeLerpPerSec = 9f;

        [Header("Audio")]
        [Tooltip("lantern-flicker.mp3 — looped while oil is low.")]
        [SerializeField] private AudioSource _flickerAudio;

        [Header("Accessibility")]
        [Tooltip("When true, the flame breathing is pinned flat (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Light _light;
        private DungeonController _controller;
        private Transform _hero;
        private IReadOnlyList<DungeonOilStone> _oilStones;
        /// <summary>
        /// WO-1001 slice 5: composed dungeons have no full DungeonController — when true,
        /// oil drains without a cottage run state (ComposedDungeonBootstrap arms this).
        /// </summary>
        private bool _standaloneRun;

        /// <summary>Current oil in the flask, 0..maxOil.</summary>
        private float _oil;

        /// <summary>The live, eased point-light range (rides below the full reach).</summary>
        private float _liveRange;

        /// <summary>Seconds remaining on the active Tincture dimming, or 0.</summary>
        private float _tinctureRemaining;

        /// <summary>True while the equipped Lightbearer Cloak is owned (+1 tile reach).</summary>
        private bool _cloakOwned;

        // ── Read-only state ──────────────────────────────────────────────────

        /// <summary>Oil remaining as a 0..1 fraction of a full flask.</summary>
        public float OilFraction => _maxOil > 0f ? Mathf.Clamp01(_oil / _maxOil) : 0f;

        /// <summary>True when oil has dropped into the warning band.</summary>
        public bool IsLowOil => OilFraction <= _lowOilFraction;

        /// <summary>
        /// WO-1001 slice 6: true when the flask is critically low or empty — the dark
        /// has closed in. Drives higher ambush odds (RandomEncounterTable darkness mult)
        /// and the "push vs extract" tension for legendary loot gates.
        /// </summary>
        public bool IsInDarkness => OilFraction <= Mathf.Min(_lowOilFraction, 0.12f);

        /// <summary>
        /// The oil fraction at or below which the lantern reads as "low oil" —
        /// the warning band the HUD oil meter tints amber/red against.
        /// </summary>
        public float LowOilFraction => _lowOilFraction;

        /// <summary>
        /// Estimated seconds of light left before the flask runs dry, at the
        /// current drain rate (a Tincture does not change the drain rate, only
        /// reach). Returns <see cref="float.PositiveInfinity"/> when the lantern
        /// is not draining — e.g. before a run starts. The HUD reads this for the
        /// duration readout (owner acceptance checklist: make the duration legible).
        /// </summary>
        public float EstimatedSecondsRemaining =>
            _oilDrainPerSec > 0f ? _oil / _oilDrainPerSec : float.PositiveInfinity;

        /// <summary>True while the Apothecary boss's Tincture is shrinking the reach.</summary>
        public bool IsTinctureActive => _tinctureRemaining > 0f;

        /// <summary>The lantern's full (un-debuffed, full-oil) reach in world units.</summary>
        public float FullRange => _baseRange + (_cloakOwned ? _cloakBonusRange : 0f);

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _light = GetComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _lanternColor;
            _light.intensity = _baseIntensity;
            _light.shadows = LightShadows.None; // the lantern is a fill light, not a shadow caster
            _oil = _maxOil;
            _liveRange = FullRange;
            _light.range = _liveRange;
        }

        /// <summary>
        /// Stops the looped flicker SFX if the lantern is disabled or torn down
        /// mid-run while oil is low — otherwise the loop plays on after the run
        /// ends (the flicker is started/stopped only inside <see cref="Update"/>).
        /// </summary>
        private void OnDisable()
        {
            if (_flickerAudio != null && _flickerAudio.isPlaying)
                _flickerAudio.Stop();
        }

        /// <summary>
        /// Wires the lantern to the dungeon: the controller (for run state), the
        /// hero transform it follows, and the layout's oil-stone refill points.
        /// Called by <see cref="DungeonController"/> on dungeon load.
        /// </summary>
        public void Configure(
            DungeonController controller,
            IReadOnlyList<DungeonOilStone> oilStones,
            Transform hero)
        {
            _controller = controller;
            _standaloneRun = false;
            _oilStones = oilStones;
            _hero = hero;
            _oil = _maxOil;
            _liveRange = FullRange;
        }

        /// <summary>
        /// WO-1001 slice 5: arm the lantern on a composed dungeon (no DungeonController).
        /// Oil drains for the whole scene visit; oil stones still refill on proximity.
        /// </summary>
        public void ConfigureStandalone(IReadOnlyList<DungeonOilStone> oilStones, Transform hero)
        {
            _controller = null;
            _standaloneRun = true;
            _oilStones = oilStones;
            _hero = hero;
            _oil = _maxOil;
            _liveRange = FullRange;
            FlowTrace.Step("Dungeon",
                $"Lantern.ConfigureStandalone: oil={_oil:F0} stones={oilStones?.Count ?? 0} " +
                $"hero='{(hero != null ? hero.name : "<null>")}' (composed path)");
        }

        /// <summary>
        /// Sets whether the equipped Lightbearer Cloak is owned. The Root Cellar
        /// chest grants the Cloak; on equip the reach grows by one tile
        /// (design §4 Beat 4 / acceptance criterion 6).
        /// </summary>
        public void SetCloakOwned(bool owned)
        {
            _cloakOwned = owned;
        }

        /// <summary>Sets the reduced-motion preference — pins the flame breathing flat.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            FollowHero();

            bool runActive = _standaloneRun
                || (_controller != null
                    && _controller.RuntimeState != null
                    && _controller.RuntimeState.RunActive);

            if (runActive)
            {
                DrainOil(Time.deltaTime);
                CheckOilStones();
            }

            TickTincture(Time.deltaTime);
            ApplyRange(Time.deltaTime);
            ApplyIntensity();
            DriveFlickerAudio();
        }

        /// <summary>Keeps the lantern light at the Keeper's chest height.</summary>
        private void FollowHero()
        {
            if (_hero == null) return;
            Vector3 p = _hero.position;
            p.y += _lanternHeight;
            transform.position = p;
        }

        /// <summary>Drains oil over time; the run's lantern slowly burns down.</summary>
        private void DrainOil(float dt)
        {
            _oil = Mathf.Max(0f, _oil - _oilDrainPerSec * dt);
        }

        /// <summary>
        /// Refills the flask when the Keeper stands within an oil stone's radius
        /// (design §4 — oil stones are the lantern's refill points).
        /// </summary>
        private void CheckOilStones()
        {
            if (_hero == null || _oilStones == null) return;
            Vector3 heroPos = _hero.position;
            // Cottage oil stones are planar (Y=0 layout). Composed multi-level uses full 3D
            // so a stone on floor -6 does not refill the hero on floor 0 (WO-1001 slice 5).
            bool planar = !_standaloneRun;
            if (planar) heroPos.y = 0f;

            foreach (var stone in _oilStones)
            {
                if (stone == null) continue;
                Vector3 stonePos = stone.position.ToWorld();
                if (planar) stonePos.y = 0f;
                float r = stone.radius;
                if ((heroPos - stonePos).sqrMagnitude <= r * r)
                {
                    _oil = _maxOil; // a stone tops the flask right off
                    return;
                }
            }
        }

        /// <summary>Begins (or refreshes) a Tincture light-shrink — port of triggerTincture.</summary>
        public void TriggerTincture()
        {
            // A fresh cast always re-arms the full window rather than stacking.
            _tinctureRemaining = _tinctureDurationSec;
        }

        /// <summary>Clears the Tincture channel — called on run teardown.</summary>
        public void ClearTincture()
        {
            _tinctureRemaining = 0f;
        }

        private void TickTincture(float dt)
        {
            if (_tinctureRemaining > 0f)
                _tinctureRemaining = Mathf.Max(0f, _tinctureRemaining - dt);
        }

        /// <summary>
        /// Eases the live light range toward its target. The target is the full
        /// reach scaled by the oil level (low oil pulls the dark in) and, while
        /// a Tincture is active, halved on top of that.
        /// </summary>
        private void ApplyRange(float dt)
        {
            // Oil scales reach between minOilLightFraction (empty) and 1 (full).
            float oilScale = Mathf.Lerp(_minOilLightFraction, 1f, OilFraction);
            float target = FullRange * oilScale;
            if (IsTinctureActive) target *= _tinctureRangeMul;

            if (_reducedMotion)
            {
                _liveRange = target;
            }
            else
            {
                float k = 1f - Mathf.Pow(0.0001f,
                    Mathf.Min(dt, 0.05f) * (_rangeLerpPerSec / 9f));
                _liveRange += (target - _liveRange) * k;
            }
            _light.range = _liveRange;
        }

        /// <summary>
        /// Drives the point-light intensity — the slow flame breathing scaled by
        /// oil (low oil dims the mean and quickens the breath). Pinned flat when
        /// reduced motion is set.
        /// </summary>
        private void ApplyIntensity()
        {
            // Oil scales the mean intensity the same way it scales reach.
            float oilScale = Mathf.Lerp(_minOilLightFraction, 1f, OilFraction);
            float mean = _baseIntensity * oilScale;

            if (_reducedMotion)
            {
                _light.intensity = mean;
                return;
            }

            float hz = IsLowOil ? _breathHz * _lowOilBreathMult : _breathHz;
            float breath = Mathf.Sin(Time.time * hz * 2f * Mathf.PI) * _breathAmplitude;
            _light.intensity = mean * (1f + breath);
        }

        /// <summary>Loops the lantern-flicker SFX while oil is low; stops it otherwise.</summary>
        private void DriveFlickerAudio()
        {
            if (_flickerAudio == null) return;
            if (IsLowOil && !_flickerAudio.isPlaying)
            {
                _flickerAudio.loop = true;
                _flickerAudio.Play();
            }
            else if (!IsLowOil && _flickerAudio.isPlaying)
            {
                _flickerAudio.Stop();
            }
        }
    }
}
