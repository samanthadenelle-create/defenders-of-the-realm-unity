using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    public enum EnemyOccupancyRole
    {
        Sentry,
        Prowler,
        PackLeft,
        PackRight,
        Ambush,
        CampInteraction
    }

    /// <summary>Authored, reservable world seat for readable pre-aggro enemy jobs.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyOccupancySlot : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private EnemyOccupancyRole role = EnemyOccupancyRole.Sentry;
        [SerializeField] private Transform facingTarget;
        [SerializeField, Min(0.25f)] private float clearanceRadius = 0.75f;
        [SerializeField] private string[] allowedActions = { "idle_breathe", "idle_fidget", "alert_turn" };

        private Enemy _occupant;

        public string StableId => string.IsNullOrWhiteSpace(stableId) ? gameObject.name : stableId;
        public EnemyOccupancyRole Role => role;
        public Enemy Occupant => _occupant;
        public bool IsReserved => _occupant != null && !_occupant.IsDead;
        public Vector3 FacingDirection => facingTarget != null
            ? (facingTarget.position - transform.position).normalized
            : transform.forward;

        public void ConfigureRuntime(string id, EnemyOccupancyRole assignedRole, Transform lookAt = null)
        {
            if (!string.IsNullOrWhiteSpace(id)) stableId = id;
            role = assignedRole;
            if (lookAt != null) facingTarget = lookAt;
        }

        public bool TryReserve(Enemy enemy)
        {
            if (enemy == null || IsReserved) return false;
            _occupant = enemy;
            return true;
        }

        public void Release(Enemy enemy)
        {
            if (_occupant == enemy || _occupant == null) _occupant = null;
        }

        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(StableId)) { reason = "missing stable id"; return false; }
            if (clearanceRadius < 0.25f) { reason = "clearance radius below 0.25m"; return false; }
            if (!NavMesh.SamplePosition(transform.position, out _, clearanceRadius * 2f, NavMesh.AllAreas))
            { reason = "no NavMesh seat within clearance envelope"; return false; }
            if (allowedActions == null || allowedActions.Length == 0)
            { reason = "no allowed idle/action routine"; return false; }
            reason = null;
            return true;
        }
    }
}
