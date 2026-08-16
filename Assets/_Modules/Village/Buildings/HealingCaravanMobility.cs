// =============================================================================
// HealingCaravanMobility — WO-991: slow-roll follow hero; glass HP support unit.
// -----------------------------------------------------------------------------
// Attached only on healing_caravan. Keeps HealingFountain heal-out-of-battle;
// adds crawl follow + fragile damageable so it cannot escort a full siege.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Catalog;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HealingCaravanMobility : MonoBehaviour, IDamageableStructure
    {
        // Crawl: hero walk ~4–6; caravan must feel useless as permanent escort.
        private const float FollowSpeed = 1.05f;
        private const float CatchUpStart = 6f;
        private const float CatchUpStop = 2.5f;
        private const float MaxHpGlass = 48f;
        private const float DamageTakenMult = 1.75f; // very easily damagable

        private NavMeshAgent _agent;
        private Transform _hero;
        private float _hp = MaxHpGlass;
        private bool _dead;
        private bool _moving;
        private float _nextHeroResolve;
        private Vector3 _lastDestination = new Vector3(float.MinValue, 0f, float.MinValue);

        public bool IsAlive => !_dead && _hp > 0f;
        public float CurrentHp => _hp;

        public void Configure(CatalogEntry entry)
        {
            FlowTrace.Step("Caravan",
                $"HealingCaravanMobility Configure id='{entry?.id}' glassHp={MaxHpGlass} followSpeed={FollowSpeed}");
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = FollowSpeed;
            _agent.acceleration = 4f;
            _agent.angularSpeed = 120f;
            _agent.stoppingDistance = CatchUpStop * 0.6f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.height = 2f;
            _agent.radius = 0.6f;
        }

        private void Start()
        {
            // DEFENSE-IN-DEPTH (2026-08-15 review finding #1): the footprint pipeline adds a
            // carving NavMeshObstacle AFTER Awake (same frame, after StructureFactory.Create
            // returns). BaseLayoutLoader now skips it for mobile structures, but if any other
            // path ever re-adds one, an agent + carving obstacle on one root means the agent
            // carves its own mesh away and never moves — remove it loudly instead.
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                FlowTrace.Warn("Caravan",
                    "NavMeshObstacle found on the caravan root — a carving obstacle kills the agent's own navmesh. Removing it (mobile unit must not carve).");
                Destroy(obstacle);
            }
        }

        private void Update()
        {
            if (_dead) return;
            // Re-resolve ONLY while unresolved (hero destroyed/respawned resets via the null
            // check) — the fallback FindFirstObjectByType is a whole-scene scan and must not
            // run twice a second forever on untagged rigs (2026-08-15 review, efficiency).
            if (_hero == null && Time.time >= _nextHeroResolve)
            {
                _nextHeroResolve = Time.time + 0.5f;
                ResolveHero();
            }
            if (_hero == null || _agent == null || !_agent.isOnNavMesh) return;

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_hero.position.x, 0f, _hero.position.z));

            if (!_moving && dist > CatchUpStart)
            {
                _moving = true;
                FlowTrace.Throttle("Caravan", "start-follow", 2f,
                    $"start follow dist={dist:F1}m hero={_hero.name}");
            }
            else if (_moving && dist < CatchUpStop)
            {
                _moving = false;
                if (_agent.isOnNavMesh) _agent.ResetPath();
                FlowTrace.Throttle("Caravan", "stop-follow", 2f,
                    $"stop follow dist={dist:F1}m");
            }

            if (_moving && _agent.isOnNavMesh)
            {
                _agent.speed = FollowSpeed;
                // Re-path only when the hero has actually moved (>0.5m) from the last
                // requested destination — a 1.05 m/s crawler does not need 60 fresh
                // NavMesh path requests per second (2026-08-15 review, efficiency).
                if ((_hero.position - _lastDestination).sqrMagnitude > 0.25f)
                {
                    _lastDestination = _hero.position;
                    _agent.SetDestination(_lastDestination);
                }
            }
        }

        private void ResolveHero()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; return; }
            var loco = Object.FindFirstObjectByType<HeroLocomotion>();
            if (loco != null) _hero = loco.transform;
        }

        public void ApplyContactDamage(float amount)
        {
            if (_dead || amount <= 0f) return;
            float applied = amount * DamageTakenMult;
            _hp = Mathf.Max(0f, _hp - applied);
            FlowTrace.Throttle("Caravan", "hit", 0.5f,
                $"ApplyContactDamage raw={amount:F1} applied={applied:F1} hp={_hp:F0}/{MaxHpGlass}");
            if (_hp <= 0f) Die();
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            FlowTrace.Step("Caravan", "DESTROYED — glass support unit killed (WO-991)");
            Destroy(gameObject, 0.4f);
        }

        /// <summary>True while actively crawling after the hero.</summary>
        public bool IsRolling => _moving;
    }
}
