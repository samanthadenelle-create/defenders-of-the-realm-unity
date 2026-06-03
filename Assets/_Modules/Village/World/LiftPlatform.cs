// =============================================================================
// LiftPlatform — an Elden Ring-style pressure-plate lift that carries the hero
// between ground level and the rampart deck. Replaces the unclimbable stairs
// (task #8): no NavMesh stair-climb, no step-offset fuss — you stand on it, it
// lifts you. Code-built + runtime-spawned by RampartLiftInstaller (no scene edit,
// no village regen).
// -----------------------------------------------------------------------------
// transform.y == the platform's TOP SURFACE level (so the hero's feet ride at
// exactly transform.y). The visual slab is a child offset DOWN by half its
// thickness so its top face sits on the surface.
//
// THE RIDE (owner 2026-06-02 "starts underground and drops me before the top"):
// the hero is driven by a NavMeshAgent (HeroLocomotion), which clamps them to the
// navmesh surface every frame. A passive Y-carry loses that fight the instant the
// platform leaves the navmesh — the agent drags the hero back down, `heroOn` goes
// false, and the carry quits mid-rise (the "drops me before the top" bug). So
// while travelling we SUSPEND the agent and LOCK the hero rigidly to the platform
// (XZ + Y), guaranteeing they ride the whole way. The agent is re-enabled only
// once we're back down on the ground navmesh; up on the deck the hero moves via
// HeroLocomotion's off-navmesh transform fallback.
//
// TOGGLE travel: stepping onto an idle platform sends it to the OPPOSITE end and
// it stays there (no auto-return) — so after riding up you can explore the deck,
// walk back on, and ride down again instead of being stranded up top.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>A vertical lift the hero rides between ground and the rampart deck.</summary>
    [DisallowMultipleComponent]
    public sealed class LiftPlatform : MonoBehaviour
    {
        private float _bottomSurfaceY;
        private float _topSurfaceY;
        private float _speed = 4.5f;        // m/s vertical travel
        private float _halfFootprint = 1.6f;

        private enum State { AtBottom, Rising, AtTop, Lowering }
        private State _state = State.AtBottom;
        private Transform _hero;
        private NavMeshAgent _heroAgent;
        private bool _heroOnLast;
        private bool _agentSuspended;

        // DEF-147: true ONLY while this lift is actively carrying the hero (agent
        // suspended + hero pinned to the slab). HeroLocomotion's off-mesh ground-snap
        // reads AnyCarrying() and skips the snap during a ride so it never yanks the
        // hero off the lift. Read-only; does NOT change any lift behavior.
        private bool _carrying;
        private static int s_carryingCount;

        /// <summary>True while ANY lift is mid-ride carrying the hero. Read-only signal
        /// for HeroLocomotion's ground-snap so it never fights the lift's hand-carry.</summary>
        public static bool AnyCarrying() => s_carryingCount > 0;

        private void SetCarrying(bool value)
        {
            if (value == _carrying) return;
            _carrying = value;
            s_carryingCount += value ? 1 : -1;
            if (s_carryingCount < 0) s_carryingCount = 0;   // defensive clamp
        }

        private void OnDisable()
        {
            // Releasing the carry flag if this lift is torn down mid-ride keeps the
            // global count honest (e.g. scene unload while travelling).
            SetCarrying(false);
        }

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
            if (_hero == null) ResolveHero();
            bool heroOn = HeroOnPlatform();

            switch (_state)
            {
                case State.AtBottom:
                    if (heroOn && !_heroOnLast) _state = State.Rising;    // stepped on at ground → go up
                    break;

                case State.AtTop:
                    if (heroOn && !_heroOnLast) _state = State.Lowering;  // stepped back on at deck → go down
                    break;

                case State.Rising:
                    if (MoveSurfaceTo(_topSurfaceY)) _state = State.AtTop;
                    break;

                case State.Lowering:
                    if (MoveSurfaceTo(_bottomSurfaceY)) _state = State.AtBottom;
                    break;
            }

            bool travelling = _state == State.Rising || _state == State.Lowering;

            if (heroOn && travelling)
            {
                // Take full authority: suspend the agent's ground-clamp and pin the
                // hero to the platform so they CAN'T be dragged off mid-ride.
                SuspendAgent();
                SetCarrying(true);                     // DEF-147: signal active carry
                _hero.position = transform.position;   // lock XZ + Y to the platform
            }
            else
            {
                SetCarrying(false);                    // DEF-147: ride over → clear signal
                // Not actively carrying — hand control straight back to the NavMeshAgent
                // so the hero is navmesh-bound at BOTH ends (ground AND the baked rampart
                // deck). BUG (owner 2026-06-02 "where i can fly"): leaving the agent
                // suspended up on the deck let the transform-fallback move the hero with no
                // ground clamp, so they floated off the rampart. The deck is NavigationStatic
                // + baked, so re-enabling binds them to it. Agent is suspended ONLY during
                // the brief ride (the if-branch above), never while resting.
                if (_agentSuspended) RestoreAgent();

                // Gentle assist: keep a hero standing on an idle platform at its
                // surface (no XZ lock, so they can freely walk off).
                if (heroOn && _hero != null)
                {
                    Vector3 hp = _hero.position;
                    hp.y = transform.position.y;
                    _hero.position = hp;
                }
            }

            _heroOnLast = heroOn;
        }

        private void SuspendAgent()
        {
            if (_agentSuspended) return;
            if (_heroAgent != null && _heroAgent.enabled)
            {
                _heroAgent.enabled = false;
                _agentSuspended = true;
            }
        }

        private void RestoreAgent()
        {
            _agentSuspended = false;
            if (_heroAgent == null) return;
            _heroAgent.enabled = true;
            // Sync the agent to where the lift left the hero so it doesn't snap back.
            if (_heroAgent.isOnNavMesh) _heroAgent.Warp(_hero.position);
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

        private void ResolveHero()
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) go = GameObject.FindWithTag("HeroTarget");
            if (go == null) return;
            _hero = go.transform;
            _heroAgent = go.GetComponent<NavMeshAgent>();
        }
    }
}
