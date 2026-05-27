// =============================================================================
// EnemyGroupCoordinator — atomic group-release coordination (DEF-72).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Holds all enemies of a spawned group in Suppressed state until every member
//   has been registered (i.e. the whole group has spawned). Then releases them
//   all simultaneously so the group charges together rather than trickling in.
//
//   Enemies with TacticalData.SuppressDelay == 0 are never suppressed; they
//   charge immediately regardless of coordinator state (backward compatible).
//
// ARCHITECTURE:
//   EnemyGroupSpawner creates one EnemyGroupCoordinator GameObject per group,
//   registers each spawned EnemyBrain, then calls FinaliseGroup() when all
//   entries are spawned. FinaliseGroup calls ReleaseAll() immediately if no
//   enemies are suppressed; otherwise the suppress timer fires ReleaseAll on
//   the slowest member's delay.
//
// INTEGRATION:
//   EnemyGroupSpawner is the only caller. No manual setup needed.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Holds an enemy group in <see cref="EnemyTacticalState.Suppressed"/> until
    /// every member has spawned, then releases them all at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyGroupCoordinator : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────

        private readonly List<EnemyBrain> _members = new List<EnemyBrain>();
        private int  _expectedCount;
        private bool _released;
        private float _suppressTimer;
        private float _maxSuppressDelay;

        // ── Setup ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Must be called before registering members so the coordinator knows
        /// when the group is complete.
        /// </summary>
        public void Initialise(int expectedCount)
        {
            _expectedCount = expectedCount;
            _released = false;
            _suppressTimer = 0f;
            _maxSuppressDelay = 0f;
        }

        /// <summary>
        /// Register a spawned <see cref="EnemyBrain"/>. If the brain has a
        /// <see cref="TacticalData"/> with a positive suppress delay, the brain
        /// is placed in <see cref="EnemyTacticalState.Suppressed"/> immediately.
        /// </summary>
        public void RegisterMember(EnemyBrain brain)
        {
            if (brain == null) return;
            _members.Add(brain);
            brain.Died += HandleMemberDied;

            // Suppress this member if its tactics ask for a hold delay.
            float delay = brain.SuppressDelay;
            if (delay > 0f)
            {
                brain.SetTacticalState(EnemyTacticalState.Suppressed);
                if (delay > _maxSuppressDelay)
                    _maxSuppressDelay = delay;
            }
        }

        /// <summary>
        /// Call after all members have been registered. Starts the release timer
        /// (or releases immediately when no suppression is needed).
        /// </summary>
        public void FinaliseGroup()
        {
            if (_maxSuppressDelay <= 0f)
            {
                ReleaseAll();
            }
            else
            {
                _suppressTimer = _maxSuppressDelay;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (_released || _suppressTimer <= 0f) return;

            _suppressTimer -= Time.deltaTime;
            if (_suppressTimer <= 0f)
                ReleaseAll();
        }

        // ── Release ───────────────────────────────────────────────────────────

        /// <summary>
        /// Releases every registered living member from Suppressed → Rush
        /// simultaneously, creating the "whole group charges at once" effect.
        /// </summary>
        public void ReleaseAll()
        {
            if (_released) return;
            _released = true;

            foreach (EnemyBrain brain in _members)
            {
                if (brain == null) continue;
                // Only override Suppressed — don't interrupt a member that already
                // switched to Rush or Retreat due to taking damage.
                if (brain.TacticalState == EnemyTacticalState.Suppressed)
                    brain.SetTacticalState(EnemyTacticalState.Rush);
            }

            Debug.Log(
                $"[EnemyGroupCoordinator] Released {_members.Count} enemies simultaneously.");

            // Self-destruct once all members are released — no ongoing cost.
            Destroy(gameObject, 0.1f);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void HandleMemberDied(Enemy _)
        {
            // Remove dead members so they don't hold up release if all the rest
            // are already released (shouldn't happen, but defensive).
            _members.RemoveAll(b => b == null || b.GetComponent<Enemy>()?.IsDead == true);
        }
    }
}
