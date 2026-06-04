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

        private Pet _pet;
        private Transform _heroT;
        private float _resolveTimer;
        private System.Random _rng;

        private float _headingDeg;       // current wander heading (0 = +Z)
        private float _turnIntentDeg;    // signed bend currently being applied
        private float _turnIntentTimer;  // time until a new turn intent is rolled
        private float _pauseTimer;       // >0 = a "stop and sniff" beat

        private static Type s_heroType;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            // Stable per-pet rng so each pet has its own personality but a
            // restart of the scene replays the same trail.
            _rng = new System.Random(gameObject.GetInstanceID());
            _headingDeg = (float)(_rng.NextDouble() * 360.0);
        }

        private void Update()
        {
            if (_heroT == null)
            {
                _resolveTimer -= Time.deltaTime;
                if (_resolveTimer <= 0f)
                {
                    _resolveTimer = ResolveRetrySeconds;
                    _heroT = ResolveHeroTransform();
                }
                if (_heroT == null) return;
            }

            float dt = Time.deltaTime;
            Vector3 petPos = transform.position;
            Vector3 toHero = _heroT.position - petPos; toHero.y = 0f;
            float distHero = toHero.magnitude;

            // ── meander: re-roll a gentle turn intent periodically, and now and
            //    then start a "stop and sniff" pause ───────────────────────────
            _turnIntentTimer -= dt;
            if (_turnIntentTimer <= 0f)
            {
                _turnIntentDeg = (float)(_rng.NextDouble() * 2.0 - 1.0) * WanderTurnDegPerSec;
                _turnIntentTimer = 0.6f + (float)_rng.NextDouble() * 1.6f;
                if (_pauseTimer <= 0f && _rng.NextDouble() < 0.22)
                    _pauseTimer = 0.8f + (float)_rng.NextDouble() * 1.4f;
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

            // Project the carrot ahead along the heading. Shorten it during a
            // pause so the pet eases down (Pet.cs arrival brake) and looks around.
            float lead = _pauseTimer > 0f ? 0.25f : LeadDistance;
            float rad = _headingDeg * Mathf.Deg2Rad;
            Vector3 carrot = petPos + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * lead;

            // Hard clamp inside the leash so the pet is never sent past the limit.
            Vector3 fromHero = carrot - _heroT.position; fromHero.y = 0f;
            if (fromHero.magnitude > ReturnRadius)
                carrot = _heroT.position + fromHero.normalized * ReturnRadius;
            carrot.y = Mathf.Max(0f, carrot.y);

            _pet.SetHomePost(carrot);
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
