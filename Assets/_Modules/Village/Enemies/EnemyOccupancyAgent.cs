using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Cheap pre-aggro world-presence driver. It yields completely to EnemyBrain when
    /// combat starts; no second combat AI or per-frame perception is introduced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyOccupancyAgent : MonoBehaviour
    {
        private const float TickSeconds = 0.25f;
        private Enemy _enemy;
        private EnemyBrain _brain;
        private EnemyOccupancySlot _slot;
        private float _tickLeft;
        private float _gestureLeft;
        private bool _patrolAlternate;
        private bool _yieldedToCombat;

        public void Bind(EnemyOccupancySlot slot)
        {
            _enemy = GetComponent<Enemy>();
            _brain = GetComponent<EnemyBrain>();
            _slot = slot;
            _tickLeft = 0f;
            _gestureLeft = NextGestureDelay();
            _yieldedToCombat = false;
        }

        private void Update()
        {
            _tickLeft -= Time.deltaTime;
            if (_tickLeft > 0f) return;
            _tickLeft = TickSeconds;
            if (_enemy == null || _slot == null || _enemy.IsDead) return;

            bool alert = _brain != null && _brain.WantsCombatPresentation;
            if (alert)
            {
                if (!_yieldedToCombat)
                {
                    _yieldedToCombat = true;
                    _enemy.SetBrainTargetPosition(null);
                }
                return;
            }
            _yieldedToCombat = false;

            Vector3 facing = _slot.FacingDirection;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(facing.normalized, Vector3.up), 0.35f);

            if (_slot.Role == EnemyOccupancyRole.Prowler)
            {
                // Deterministic two-seat patrol: authored slot plus a bounded lateral seat.
                // No raw random-circle placement and no continuous path churn.
                Vector3 offset = _slot.transform.right * (_patrolAlternate ? 2f : -2f);
                _enemy.SetBrainTargetPosition(_slot.transform.position + offset);
            }
            else
            {
                _enemy.SetBrainTargetPosition(_slot.transform.position);
            }

            _gestureLeft -= TickSeconds;
            if (_gestureLeft <= 0f)
            {
                _gestureLeft = NextGestureDelay();
                _patrolAlternate = !_patrolAlternate;
                if (_slot.Role == EnemyOccupancyRole.Sentry ||
                    _slot.Role == EnemyOccupancyRole.CampInteraction)
                    _enemy.PlayAmbientGesture();
            }
        }

        private float NextGestureDelay()
        {
            int seed = _slot != null ? _slot.StableId.GetHashCode() : GetInstanceID();
            return 8f + Mathf.Abs(seed % 600) / 100f;
        }

        private void OnDisable()
        {
            if (_enemy != null) _enemy.SetBrainTargetPosition(null);
        }
    }
}
