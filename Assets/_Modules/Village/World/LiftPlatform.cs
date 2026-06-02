// =============================================================================
// LiftPlatform — an Elden Ring-style pressure-plate lift that carries the hero
// between ground level and the rampart deck. Replaces the unclimbable stairs
// (task #8): no NavMesh stair-climb, no step-offset fuss — you stand on it, it
// lifts you. Code-built + runtime-spawned by RampartLiftInstaller (no scene edit,
// no village regen).
// -----------------------------------------------------------------------------
// transform.y == the platform's TOP SURFACE level (so the hero's feet ride at
// exactly transform.y). The visual slab is a child offset DOWN by half its
// thickness so its top face sits on the surface. Stepping onto an idle platform
// sends it to the opposite end; it auto-returns a moment after you step off.
//
// RIDE: while the hero stands within the footprint, their Y is locked to the
// surface each LateUpdate — works whether or not the custom HeroLocomotion
// ground-clamps. FIRST PASS — the carry feel wants an in-editor test.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>A vertical lift the hero rides between ground and the rampart deck.</summary>
    [DisallowMultipleComponent]
    public sealed class LiftPlatform : MonoBehaviour
    {
        private float _bottomSurfaceY;
        private float _topSurfaceY;
        private float _speed = 4.5f;        // m/s vertical travel
        private float _holdSeconds = 0.6f;  // pause before auto-returning once empty
        private float _halfFootprint = 1.6f;

        private enum State { AtBottom, Rising, AtTop, Lowering }
        private State _state = State.AtBottom;
        private float _holdTimer;
        private Transform _hero;
        private bool _heroOnLast;

        /// <summary>Set the travel range (TOP-SURFACE world Y at each end) + footprint.
        /// Call once right after AddComponent.</summary>
        public void Configure(float bottomSurfaceY, float topSurfaceY, float footprint)
        {
            _bottomSurfaceY = bottomSurfaceY;
            _topSurfaceY    = topSurfaceY;
            _halfFootprint  = footprint * 0.5f;

            var p = transform.position;
            p.y = _bottomSurfaceY;
            transform.position = p;
            _state = State.AtBottom;
        }

        private void LateUpdate()
        {
            if (_hero == null) _hero = FindHero();
            bool heroOn = HeroOnPlatform();

            switch (_state)
            {
                case State.AtBottom:
                    if (heroOn && !_heroOnLast) _state = State.Rising;       // stepped on → go up
                    break;

                case State.AtTop:
                    if (!heroOn)
                    {
                        _holdTimer += Time.deltaTime;
                        if (_holdTimer > _holdSeconds) { _state = State.Lowering; _holdTimer = 0f; }
                    }
                    else _holdTimer = 0f;
                    break;

                case State.Rising:
                    if (MoveSurfaceTo(_topSurfaceY)) { _state = State.AtTop; _holdTimer = 0f; }
                    break;

                case State.Lowering:
                    if (MoveSurfaceTo(_bottomSurfaceY)) _state = State.AtBottom;
                    break;
            }

            // Carry: lock the hero's feet to the platform surface while aboard.
            if (heroOn && _hero != null)
            {
                Vector3 hp = _hero.position;
                hp.y = transform.position.y;
                _hero.position = hp;
            }
            _heroOnLast = heroOn;
        }

        private bool MoveSurfaceTo(float targetY)
        {
            var p = transform.position;
            p.y = Mathf.MoveTowards(p.y, targetY, _speed * Time.deltaTime);
            transform.position = p;
            return Mathf.Abs(p.y - targetY) < 0.001f;
        }

        // Hero is "on" if inside the XZ footprint and their feet are near the surface.
        private bool HeroOnPlatform()
        {
            if (_hero == null) return false;
            Vector3 d = _hero.position - transform.position;
            if (Mathf.Abs(d.x) > _halfFootprint || Mathf.Abs(d.z) > _halfFootprint) return false;
            return Mathf.Abs(d.y) < 1.3f;   // feet within ~waist height of the surface
        }

        private static Transform FindHero()
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) go = GameObject.FindWithTag("HeroTarget");
            return go != null ? go.transform : null;
        }
    }
}
