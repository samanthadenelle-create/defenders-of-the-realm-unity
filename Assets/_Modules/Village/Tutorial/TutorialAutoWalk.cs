// =============================================================================
// TutorialAutoWalk — drives the hero along a scripted waypoint path (WO-277).
// -----------------------------------------------------------------------------
// Scene 2/3 of the FTUE: "companion leads, hero follows, player input disabled".
// This component owns a transient waypoint marker and steers the hero toward it
// via HeroLocomotion.SetAutoWalk (which suppresses input + drives the existing
// NavMeshAgent). The TutorialDirector hands it a destination, then polls
// IsWalking / await-s arrival before continuing the sequence.
//
// We auto-walk to a single destination at a time (the director sequences the
// waypoints — gate to gate — between dialogue beats). A waypoint marker
// GameObject is created/reused so HeroLocomotion has a stable Transform to chase;
// destinations are snapped to the NavMesh so the agent can always reach them.
//
// Isolation/safety: lives in DeNelle.Village (drives HeroLocomotion directly).
// Null-safe — with no hero / no locomotion it reports "arrived" immediately so
// the director never stalls.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// Steers the hero to a scripted destination by driving
    /// <see cref="HeroLocomotion.SetAutoWalk"/>. The
    /// <see cref="TutorialDirector"/> sets a destination then waits on
    /// <see cref="HasArrived"/> before advancing the tour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialAutoWalk : MonoBehaviour
    {
        // Give up steering toward an unreachable point after this long so a bad
        // waypoint (off-mesh / blocked) can never wedge the tour — the director
        // then proceeds as if arrived.
        private const float WalkTimeout = 14f;

        private HeroLocomotion _hero;
        private Transform _waypoint;     // transient marker the hero chases
        private bool _walking;
        private float _deadline;

        /// <summary>True while the hero is being auto-walked toward a destination.</summary>
        public bool IsWalking => _walking;

        /// <summary>
        /// True once the hero has reached the current destination (or the walk
        /// timed out / there was nothing to drive). The director awaits this.
        /// </summary>
        public bool HasArrived
        {
            get
            {
                if (!_walking) return true;
                if (_hero == null) return true;
                if (Time.time >= _deadline) return true;
                return _hero.AutoWalkArrived;
            }
        }

        /// <summary>Assigns the hero locomotion to drive (the director wires this).</summary>
        public void SetHero(HeroLocomotion hero) => _hero = hero;

        /// <summary>
        /// Begin auto-walking the hero to <paramref name="worldPos"/> (snapped to
        /// the nearest NavMesh point). Player input is suppressed until
        /// <see cref="Stop"/> (or a new destination) is issued.
        /// </summary>
        public void WalkTo(Vector3 worldPos)
        {
            if (_hero == null)
            {
                _walking = false;
                return;
            }

            Vector3 dest = worldPos;
            if (NavMesh.SamplePosition(dest, out var hit, 8f, NavMesh.AllAreas))
                dest = hit.position;

            EnsureWaypoint();
            _waypoint.position = dest;

            _hero.SetAutoWalk(_waypoint);
            _walking = true;
            _deadline = Time.time + WalkTimeout;
        }

        /// <summary>
        /// Stop auto-walking and hand control back to the player. Safe to call when
        /// not walking. The director calls this when the scripted tour ends.
        /// </summary>
        public void Stop()
        {
            _walking = false;
            _hero?.ClearAutoWalk();
        }

        private void Update()
        {
            // Auto-stop once the hero arrives so the hero settles (input stays
            // suppressed via the director's flow until it issues the next WalkTo or
            // Stop — but we drop our own SetAutoWalk so the hero isn't pinned to a
            // stale waypoint while the director plays a dialogue beat).
            if (_walking && HasArrived)
            {
                _walking = false;
                _hero?.ClearAutoWalk();
            }
        }

        private void EnsureWaypoint()
        {
            if (_waypoint != null) return;
            var go = new GameObject("TutorialWaypoint");
            go.transform.SetParent(transform, false);
            _waypoint = go.transform;
        }

        private void OnDestroy()
        {
            // Never leave the hero pinned to a destroyed waypoint.
            _hero?.ClearAutoWalk();
        }
    }
}
