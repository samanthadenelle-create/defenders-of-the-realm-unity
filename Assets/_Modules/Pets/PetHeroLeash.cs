// =============================================================================
// PetHeroLeash — keeps the pet's HomePost anchored to a slot trailing the hero
// so every deployed pet drifts toward (and around) the player as they roam the
// village.
// -----------------------------------------------------------------------------
// Owner direction 2026-05-20: "focus on pets as i cant see them and should be
// on auto follow hero". The deploy slot ringing the Heart was 15+ metres from
// the spawn position, so the wider camera never framed the pets. Pet.cs itself
// only chases enemies — in Defend mode it returns to HomePost when the field
// is clear. So we just re-anchor HomePost to a leash point behind the hero
// every frame; the pet's existing kinematic drifter does the rest.
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
        // Pets wander inside this donut around the hero — outer = how far
        // they may stray, inner = how close they can crowd him.
        private const float WanderRadiusInner = 2.0f;
        private const float WanderRadiusOuter = 6.5f;
        // If the hero strays this far from the current wander point, snap
        // the point closer immediately so the pet doesn't get left behind.
        private const float MaxLeashDistance = 11.0f;
        // Range of "exploration time" between picking new wander points.
        private const float MinThinkSeconds = 1.6f;
        private const float MaxThinkSeconds = 4.5f;
        // Distance at which the pet considers itself "arrived" at the
        // current wander point and starts the next think timer.
        private const float ArrivalRadius = 0.8f;

        private Pet _pet;
        private Transform _heroT;
        private float _resolveTimer;
        private Vector3 _currentWanderPoint;
        private float _thinkTimer;
        private bool _hasWanderPoint;
        private System.Random _rng;

        private static Type s_heroType;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            // Stable per-pet rng so each pet has its own personality but a
            // restart of the scene plays the same trail.
            _rng = new System.Random(gameObject.GetInstanceID());
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

            // Owner ask 2026-05-20: pets should "be busy exploring around but
            // stay in proximity of hero" — not a tight orbit. Each pet picks
            // its own wander point in a donut around the hero, walks toward
            // it, then picks a new one after a randomised think delay so the
            // pack reads as three distinct creatures going about their day.
            _thinkTimer -= Time.deltaTime;
            bool needPoint =
                !_hasWanderPoint ||
                _thinkTimer <= 0f ||
                Vector3.Distance(transform.position, _currentWanderPoint) < ArrivalRadius ||
                Vector3.Distance(_heroT.position, _currentWanderPoint) > MaxLeashDistance;

            if (needPoint)
            {
                _currentWanderPoint = PickWanderPoint(_heroT.position);
                _thinkTimer = Mathf.Lerp(
                    MinThinkSeconds, MaxThinkSeconds,
                    (float)_rng.NextDouble());
                _hasWanderPoint = true;
            }

            _pet.SetHomePost(_currentWanderPoint);
        }

        private Vector3 PickWanderPoint(Vector3 heroPos)
        {
            // Random angle 0..2π; random radius in [inner, outer].
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            float radius = Mathf.Lerp(
                WanderRadiusInner, WanderRadiusOuter,
                (float)_rng.NextDouble());
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius);
            Vector3 p = heroPos + offset;
            p.y = Mathf.Max(0f, p.y);
            return p;
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
