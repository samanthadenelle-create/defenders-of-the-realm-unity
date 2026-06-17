// =============================================================================
// PetHeroLeash — gives each deployed pet a natural "exploring companion" feel:
// it meanders around the hero on smooth, curving paths instead of sprinting in
// straight lines between unrelated points (the old behaviour, which read as a
// triangular ping-pong / yo-yo on a string).
// -----------------------------------------------------------------------------
// How it works (owner 2026-05-25 "should feel like natural exploration"):
//   • A continuously-drifting wander HEADING (a slow random walk) — the pet
//     never reverses sharply, so its path curves like an animal nosing around.
//   • The pet's HomePost (which Pet.cs steers toward) is a "carrot" projected a
//     few metres AHEAD of the pet along that heading, refreshed every frame.
//     Because it stays > Pet.ArrivalDamp (1.6 m) ahead, Pet.cs never hits its
//     arrival brake, so the pet cruises smoothly and never stop-starts.
//   • When the pet drifts past the explore radius the heading is gently steered
//     back toward the hero (a curve home, not a snap), scaling with how far out
//     it is; beyond the hard leash it beelines home. A clamp keeps the carrot
//     inside the leash at all times.
//   • Occasional "stop and sniff" beats shorten the carrot so the pet eases to a
//     near-stop and looks around, then resumes — adds life. Each pet has its own
//     RNG seed, so the three explore independently instead of moving as a clump.
//
// Cross-module note: DeNelle.Pets cannot reference DeNelle.Village (asmdef
// isolation), so HeroLocomotion is resolved by reflection — name-matched once,
// cached, refreshed on scene reload.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;

namespace DeNelle.Pets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Pet))]
    public sealed class PetHeroLeash : MonoBehaviour
    {
        private const float ResolveRetrySeconds = 1.0f;

        // Carrot distance ahead of the pet along its heading. Kept > Pet.cs's
        // ArrivalDamp (1.6 m) so the pet cruises without ever braking → smooth,
        // continuous motion instead of stop-start.
        private const float LeadDistance = 3.5f;
        // Pets keep AT LEAST this far from the hero — they ring him, never cruise
        // through his centre-of-frame spot. Without this a smoothly-moving pet
        // gliding through the (stationary, centred) hero reads as "the camera is
        // following the pet" (owner 2026-05-25, persisted across camera fixes).
        private const float InnerRadius = 4.5f;
        // Pet roams freely out to this radius of the hero; past it the heading is
        // steered back toward the hero so it curves home.
        private const float ExploreRadius = 9f;
        // Hard leash — the carrot is never placed beyond this; past it the pet
        // beelines home.
        private const float ReturnRadius = 13f;
        // Max gentle bend of the wander heading while meandering (deg/sec).
        private const float WanderTurnDegPerSec = 70f;
        // How sharply it may curve home at the very edge of the leash (deg/sec).
        private const float HomeSteerMaxDegPerSec = 200f;
        // Perlin-noise drift rate for the wander heading — how fast the coherent
        // turn-intent evolves. Lower = lazier meander, higher = more restless. TUNABLE.
        private const float NoiseDriftRate = 0.35f;
        // Idle-behavior FSM tuning (TUNABLE — set at playtest). Behaviors: 0 wander,
        // 1 sniff, 2 sit, 3 look, 4 circle, 5 dash. The controller maps each to a clip.
        private const float DashLead       = 7f;     // longer carrot → a brief dart ahead
        private const float OrbitRadius    = 5.5f;   // ring radius when circling the hero
        private const float OrbitDegPerSec = 45f;
        // Context-weighted behavior selection (research: weighted-random idle states +
        // cooldown). Index = behavior id. Hero MOVING → keep up; hero STILL → potter.
        private static readonly int[] WeightsHeroMoving = { 45, 5, 3, 5, 17, 25 };
        private static readonly int[] WeightsHeroStill  = { 30, 22, 14, 16, 12, 6 };

        private Pet _pet;
        private Transform _heroT;
        private float _resolveTimer;
        private float _noHeroTime;       // seconds the hero has been unresolved (for the not-following warn)
        private bool  _warnedNoHero;     // one-shot guard so the warn fires once per outage
        private System.Random _rng;

        private float _headingDeg;       // current wander heading (0 = +Z)
        private float _turnIntentDeg;    // signed bend currently being applied (Perlin-driven)
        private float _pauseTimer;       // >0 = the pet is stopped (sniff/sit/look idle beat)
        private float _noiseSeed;        // per-pet offset into the Perlin field (own personality)
        private int   _behavior;         // current idle-FSM behavior (0 wander … 5 dash)
        private float _behaviorTimer;    // dwell remaining in the current behavior
        private float _orbitDeg;         // angle for the "circle the hero" behavior
        private Vector3 _lastHeroPos;    // to detect whether the hero is moving (context weighting)

        private static Type s_heroType;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            // Stable per-pet rng so each pet has its own personality but a
            // restart of the scene replays the same trail.
            _rng = new System.Random(gameObject.GetInstanceID());
            _headingDeg = (float)(_rng.NextDouble() * 360.0);
            _noiseSeed = (float)(_rng.NextDouble() * 1000.0);   // unique slice of the noise field per pet
        }

        private void Update()
        {
            if (_heroT == null)
            {
                _noHeroTime += Time.deltaTime;
                _resolveTimer -= Time.deltaTime;
                if (_resolveTimer <= 0f)
                {
                    _resolveTimer = ResolveRetrySeconds;
                    _heroT = ResolveHeroTransform();
                }
                if (_heroT == null)
                {
                    // NO SILENT FAILURE (§12): a pet that can't resolve the hero just sits there
                    // ("doesnt follow", owner F8 2026-06-17) with no clue why. Surface it ONCE after
                    // a grace period so the next capture says whether the pet even found the hero.
                    if (!_warnedNoHero && _noHeroTime > 5f)
                    {
                        _warnedNoHero = true;
                        Debug.LogWarning("[PetHeroLeash] hero (HeroLocomotion) not resolved after 5s — " +
                                         "pet cannot follow until it appears. (No HeroLocomotion in scene / wrong scene?)");
                    }
                    return;
                }
            }
            // Hero is present this frame — clear the not-following watch.
            _noHeroTime = 0f;
            _warnedNoHero = false;

            float dt = Time.deltaTime;
            Vector3 petPos = transform.position;
            Vector3 toHero = _heroT.position - petPos; toHero.y = 0f;
            float distHero = toHero.magnitude;

            // ── heading drift: a CONTINUOUS Perlin-noise turn intent (coherent noise →
            //    smooth, animal-like curving), signed so there's no directional bias. ──
            _turnIntentDeg = (Mathf.PerlinNoise(_noiseSeed, Time.time * NoiseDriftRate) - 0.5f) * 2f * WanderTurnDegPerSec;

            // ── idle-behavior FSM: on a cooldown, weighted-randomly pick what the pet
            //    DOES (wander/sniff/sit/look/circle/dash), shifted by whether the hero is
            //    moving; the controller maps the int to a clip. Sniff/sit/look stop the
            //    pet (short carrot) so the idle anim reads; circle/dash drive movement. ──
            float heroSpeed = dt > 0f ? (_heroT.position - _lastHeroPos).magnitude / dt : 0f;
            _lastHeroPos = _heroT.position;
            _behaviorTimer -= dt;
            if (_behaviorTimer <= 0f)
            {
                _behavior = PickBehavior(heroSpeed > 0.6f);
                _behaviorTimer = 1.2f + (float)_rng.NextDouble() * 2.8f;   // 1.2–4 s dwell
                _pet.SetBehavior(_behavior);
                _pauseTimer = (_behavior >= 1 && _behavior <= 3) ? _behaviorTimer : 0f; // sniff/sit/look = stop
            }
            if (_pauseTimer > 0f) _pauseTimer -= dt;

            // Gently bend the heading (smooth random walk → curving paths).
            _headingDeg += _turnIntentDeg * dt;

            // Steer back toward the hero past the explore radius — a curve home,
            // its strength rising the further out we are (never a hard snap).
            if (distHero > ExploreRadius && distHero > 0.01f)
            {
                float homeDeg = Mathf.Atan2(toHero.x, toHero.z) * Mathf.Rad2Deg;
                if (distHero > ReturnRadius)
                {
                    _headingDeg = homeDeg; // beyond the leash → head straight home
                }
                else
                {
                    float urgency = Mathf.Clamp01(
                        (distHero - ExploreRadius) / Mathf.Max(0.01f, ReturnRadius - ExploreRadius));
                    float steer = Mathf.Lerp(WanderTurnDegPerSec, HomeSteerMaxDegPerSec, urgency) * dt;
                    _headingDeg = Mathf.MoveTowardsAngle(_headingDeg, homeDeg, steer);
                }
            }

            // Project the carrot ahead along the heading. Shorten it during a pause
            // (sniff/sit/look) so the pet eases down and the idle anim reads; lengthen
            // it for a dash so the pet briefly darts ahead.
            float lead = _pauseTimer > 0f ? 0.25f : (_behavior == 5 ? DashLead : LeadDistance);
            float rad = _headingDeg * Mathf.Deg2Rad;
            Vector3 carrot = petPos + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * lead;

            // "Circle the hero": override the carrot to an orbit point so the pet rings
            // the hero (curious/loyal) — still clamped inside the leash just below.
            if (_behavior == 4)
            {
                _orbitDeg += OrbitDegPerSec * dt;
                float o = _orbitDeg * Mathf.Deg2Rad;
                carrot = _heroT.position + new Vector3(Mathf.Sin(o), 0f, Mathf.Cos(o)) * OrbitRadius;
            }

            // Hard clamp inside the leash so the pet is never sent past the limit.
            Vector3 fromHero = carrot - _heroT.position; fromHero.y = 0f;
            if (fromHero.magnitude > ReturnRadius)
                carrot = _heroT.position + fromHero.normalized * ReturnRadius;
            carrot.y = Mathf.Max(0f, carrot.y);

            _pet.SetHomePost(carrot);
        }

        // Weighted-random idle behavior (0 wander,1 sniff,2 sit,3 look,4 circle,5 dash).
        // Hero MOVING → keep up (wander/dash/circle); hero STILL → potter (sniff/sit/look).
        // No per-frame alloc — called only on the behavior cooldown, using static weights.
        private int PickBehavior(bool heroMoving)
        {
            int[] w = heroMoving ? WeightsHeroMoving : WeightsHeroStill;
            int total = 0;
            for (int i = 0; i < w.Length; i++) total += w[i];
            int r = _rng.Next(total);
            for (int i = 0; i < w.Length; i++) { if (r < w[i]) return i; r -= w[i]; }
            return 0;
        }

        private static Transform ResolveHeroTransform()
        {
            try
            {
                if (s_heroType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Village.HeroLocomotion", false);
                        if (t != null) { s_heroType = t; break; }
                    }
                }
                if (s_heroType == null) return null;
                var found = UnityEngine.Object.FindObjectOfType(s_heroType) as Component;
                return found != null ? found.transform : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Keeps a TextMesh facing the main camera (used for pet name tags).</summary>
    [DisallowMultipleComponent]
    internal sealed class PetNameTagBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
