// =============================================================================
// OutpostDefender — recruitable AI defensive troop (Tank / DPS / Healer).
// -----------------------------------------------------------------------------
// Spawned by OutpostHub after successful Economy.TrySpend.
// - Guards a post position.
// - Attacks nearby hostile targets (reuses IDamageable + basic contact or range).
// - Roles have different stats (Tank = high HP/low dmg, DPS = glass cannon,
//   Healer = supports by occasionally "healing" the hub or other defenders via
//   a small positive Grant or HP restore on friendlies — simplified here).
// - Light upkeep is handled by the owning hub (periodic Economy drain).
// - Contributes to defending harvest sites and the outpost itself.
// - Scaling stats applied at spawn time by the hub (distance / threat).
// =============================================================================
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village.World
{
    public sealed class OutpostDefender : MonoBehaviour, IDamageableStructure
    {
        public DefenderRole Role = DefenderRole.DPS;
        public Vector3 GuardPost;
        public float GuardRadius = 9f;
        public OutpostHub Hub;

        public float MaxHp = 40f;
        public float Damage = 10f;
        public float AttackRange = 4.5f;
        public float AttackCooldown = 1.1f;

        private float _hp;
        private float _nextAttack;
        private Transform _currentTarget;

        public bool IsAlive => _hp > 0f;

        public void ApplyContactDamage(float amount)
        {
            if (_hp <= 0f) return;
            _hp = Mathf.Max(0f, _hp - Mathf.Abs(amount));
            if (_hp <= 0f)
            {
                Debug.Log($"[OutpostDefender] {Role} fell.");
                Destroy(gameObject);
            }
        }

        private void Awake()
        {
            _hp = MaxHp;
            if (GuardPost == Vector3.zero) GuardPost = transform.position;
        }

        private void Update()
        {
            if (_hp <= 0f) return;

            // Stay near guard post.
            float distToPost = Vector3.Distance(transform.position, GuardPost);
            if (distToPost > GuardRadius * 0.6f)
            {
                Vector3 toPost = (GuardPost - transform.position).normalized;
                transform.position += toPost * 3.5f * Time.deltaTime;
            }

            // Find hostile (simple broad search — real version would use a team mask / Threat system).
            if (_currentTarget == null || !IsValidTarget(_currentTarget))
            {
                _currentTarget = FindNearestHostile(AttackRange * 1.6f);
            }

            if (_currentTarget != null)
            {
                // Face and move a bit closer.
                Vector3 dir = (_currentTarget.position - transform.position).normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.3f);

                float d = Vector3.Distance(transform.position, _currentTarget.position);
                if (d > AttackRange * 0.7f)
                {
                    transform.position += dir * 4f * Time.deltaTime;
                }

                if (Time.time >= _nextAttack && d <= AttackRange)
                {
                    Attack();
                    _nextAttack = Time.time + AttackCooldown;
                }
            }
        }

        private void Attack()
        {
            if (_currentTarget == null) return;

            // Apply damage to target if it is damageable.
            var dmgTarget = _currentTarget.GetComponent<IDamageableStructure>();
            if (dmgTarget != null && dmgTarget.IsAlive)
            {
                dmgTarget.ApplyContactDamage(Damage);
            }
            else
            {
                // Fallback: if it's an Enemy or other, try a generic message or just log.
                Debug.Log($"[OutpostDefender] {Role} hit {_currentTarget.name} for {Damage}.");
            }

            // Healer role occasionally "supports" by giving a tiny resource bump or healing the hub.
            if (Role == DefenderRole.Healer && Hub != null && Random.value < 0.35f)
            {
                // Symbolic support: small positive grant to the economy (represents "inspired gathering"
                // or minor supply line). Real healer would restore HP on friendlies.
                EconomyService.Instance?.Grant(food: 1);
            }
        }

        private Transform FindNearestHostile(float radius)
        {
            // Very broad — looks for anything tagged Enemy or with an Enemy component, or our own raider proxies.
            var all = Physics.OverlapSphere(transform.position, radius);
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var hit in all)
            {
                if (hit == null || hit.transform == transform) continue;
                if (!IsValidTarget(hit.transform)) continue;

                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = hit.transform;
                }
            }
            return best;
        }

        private bool IsValidTarget(Transform t)
        {
            if (t == null) return false;
            // Hostile if tagged Enemy, has Enemy component, or is one of our HarvestSiteRaider proxies.
            if (t.CompareTag("Enemy")) return true;
            if (t.GetComponent("Enemy") != null) return true;
            if (t.name.Contains("Raider") || t.name.Contains("HarvestRaider")) return true;
            return false;
        }

        private void OnDestroy()
        {
            if (Hub != null && Hub.Defenders != null)
            {
                // The hub will clean the list in its Update.
            }
        }
    }
}
