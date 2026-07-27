// =============================================================================
// TroopController — one deployed friendly troop (WO-453 Step 1, combat-only).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A lightweight friendly fighter built from a TroopDef (troops.json). It COPIES
// the proven Hostile-hunt loop from DeNelle.Pets.Pet — an OverlapSphere scan for
// the nearest CombatFaction.Hostile IDamageable, a NavMeshAgent driven by Move()
// (updateRotation off; facing manual), and a guarded Animator — and it is itself
// damageable through IDamageableStructure, exactly like StoryCompanion, so the
// enemy contact-attack lane (Enemy.ProbeForStructure / EnemyBrain.TryAttack) can
// chip it down via GetComponentInParent<IDamageableStructure>().
//
// WHY NOT EnemyBrain: that brain is hero/Heart-hardcoded with no faction param —
// it can only HUNT the hero. Troops need the opposite (hunt Hostile foes), so we
// reuse the Pet hunt loop (which already filters to Hostile) rather than flip the
// enemy AI. Troops are CombatFaction.Friendly conceptually; they only READ foes.
//
// Footman (melee) and Archer (ranged-by-reach) share this class — only the def
// stats differ (the Archer's attackRange 14 makes it a standoff fighter; the
// travelling-projectile visual is deferred). Troops are EXPENDABLE: at 0 HP the
// body plays its death anim and is destroyed (no pool / respawn).
//
// Step-1 scope is combat only — there is NO rally / deploy-point / retreat verb
// here (that is Step 4). With no foe in range the troop simply idles in place.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Combat;
using DeNelle.BattleATB.Engine;   // StatusKind (unlocked special-ability vocabulary)
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// A deployed friendly troop. Hunts the nearest hostile <see cref="IDamageable"/>
    /// within its scan radius and attacks on a cooldown; itself takes contact damage
    /// through <see cref="IDamageableStructure"/>. Configured from a <see cref="TroopDef"/>
    /// (troops.json) via <see cref="Configure"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TroopController : MonoBehaviour, IDamageableStructure
    {
        [Header("Identity (from troops.json)")]
        [Tooltip("Stable troop id — e.g. troop-footman. Set by Configure().")]
        [SerializeField] private string _troopId;

        [Tooltip("The persisted PlayerTroop.Id this body was deployed from (WO-453 Step 4). " +
                 "Used by the retreat reconcile to tell survivors from the wounded. Empty for a " +
                 "dev/loose spawn that wasn't drawn from the army.")]
        [SerializeField] private string _ownedTroopId;

        [Header("Combat")]
        [Tooltip("Layers swept for IDamageable hostile targets. Set to the village Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Header("Live state")]
        [Tooltip("Current HP. Set from the def's MaxHp by Configure().")]
        [SerializeField] private float _hp;

        // --- runtime, from the TroopDef (troops.json) ---
        private TroopDef _def;
        private float _maxHp = 100f;
        private float _attackDamage = 12f;
        private float _attackRange = 2.5f;
        private float _attackCooldown = 1.0f;
        private float _moveSpeed = 4.0f;
        private float _huntScanRadius = 14f;
        private DamageElement _element = DamageElement.None;

        // WO-771.9 spawn-wiring: the EFFECTIVE baseline the veterancy/perk multipliers re-base
        // from. Set to the def stats in Configure; overwritten by ApplyUpgradeStats when the
        // troop is spawned at an upgrade level, so an upgraded troop's reach/strength survive a
        // subsequent ApplyDamageMultiplier/ApplyHealthMultiplier (which re-base, never compound).
        private float _baseMaxHp = 100f;
        private float _baseAttackDamage = 12f;

        // WO-771.9: the upgrade level this troop was resolved at (1 = pure baseline) + the
        // special abilities unlocked at that level (their StatusKind is the real-unit effect
        // vocabulary; per-tick status application is the V2 sim's job — here they are attached
        // as data the combat layer reads).
        private int _upgradeLevel = 1;
        private readonly List<AbilityUnlock> _unlockedAbilities = new List<AbilityUnlock>();

        private float _attackCdRemaining;

        // ── Target-hunt throttle (mirrors Pet's _huntTimer idiom) ────────────
        // Re-run the OverlapSphere scan only on an interval; reuse the cached foe
        // between ticks. The cheap per-frame work (move + attack timing) still
        // runs every frame, so combat feel is unchanged — only the SCAN cadence
        // drops. Explicit null/alive checks (never ?? on a UnityObject/IDamageable).
        private const float HuntScanInterval = 0.2f;
        private float _huntTimer;
        private IDamageable _cachedFoe;

        // Reusable overlap buffer — avoids per-frame GC (OverlapSphereNonAlloc).
        private readonly Collider[] _overlap = new Collider[48];

        // ── Navigation (mirrors Pet: drive a NavMeshAgent via Move()) ─────────
        // The agent constrains the troop to the SAME baked NavMesh the hero +
        // enemies use (it can't enter walls/buildings) and grounds it on the
        // walkable surface; we feed it our own step and keep rotation manual.
        private NavMeshAgent _agent;

        // Eased locomotion (mirrors Pet.MoveToward) so the troop accelerates out
        // of rest, coasts, then damps as it arrives — no constant-velocity dash.
        private float _currentSpeed;
        private const float Acceleration = 9f;
        private const float ArrivalDamp  = 1.6f;

        // Rally arrival epsilon (WO-453 Step 4): a troop within this flat distance of the
        // global rally point is "arrived" and idles instead of jittering on the spot.
        private const float RallyArrivalEpsilon = 1.25f;

        // ── Animation (guarded — a troop with no rig still fights) ────────────
        private Animator _animator;
        private Vector3 _lastPosition;
        private static readonly int AnimSpeed  = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit    = Animator.StringToHash("Hit");
        private static readonly int AnimDead   = Animator.StringToHash("Dead");
        private bool _hasSpeed, _hasAttack, _hasHit, _hasDead;

        // How long the corpse lingers after death before it's destroyed (lets the
        // Dead anim play). EXPENDABLE — no pool / respawn.
        private const float DeathHoldSeconds = 3f;
        private bool _dead;

        /// <summary>Stable troop id — e.g. <c>troop-footman</c>.</summary>
        public string TroopId => _troopId;

        /// <summary>
        /// The persisted <c>PlayerTroop.Id</c> this body was deployed from (WO-453 Step 4),
        /// or empty for a loose/dev spawn. Stamped by the deployer so the retreat reconcile
        /// can map a living troop back to its army record (survivor vs wounded).
        /// </summary>
        public string OwnedTroopId
        {
            get => _ownedTroopId;
            set => _ownedTroopId = value;
        }

        /// <summary>Current HP.</summary>
        public float Hp => _hp;

        /// <summary>Max HP from the def.</summary>
        public float MaxHp => _maxHp;

        /// <summary>True while the troop is alive.</summary>
        public bool IsAlive => !_dead && _hp > 0f;

        /// <summary>The static def this troop was configured from (troops.json).</summary>
        public TroopDef Def => _def;

        /// <summary>WO-771.9 — the upgrade level this troop was spawned at (1 = pure baseline).</summary>
        public int UpgradeLevel => _upgradeLevel;

        /// <summary>WO-771.9 — the special abilities unlocked at this troop's upgrade level (may be empty).</summary>
        public IReadOnlyList<AbilityUnlock> UnlockedAbilities => _unlockedAbilities;

        /// <summary>WO-771.9 — the StatusKinds this troop's unlocked abilities apply (real-unit effect vocabulary).</summary>
        public IEnumerable<StatusKind> UnlockedStatuses
        {
            get { foreach (var a in _unlockedAbilities) if (a != null) yield return a.StatusKind; }
        }

        /// <summary>
        /// Sets the LayerMask the troop sweeps for hostile targets. The deployer /
        /// factory calls this so the mask need not be authored per-instance.
        /// </summary>
        public void SetEnemyMask(LayerMask enemyMask) => _enemyMask = enemyMask;

        /// <summary>
        /// Applies a veterancy DAMAGE multiplier to this troop's per-hit damage (WO-453
        /// Step 4). Multiplies the def's base AttackDamage (resolved in Configure) so a
        /// veteran troop (PlayerTroop.DamageMultiplier = 1 + 0.05*rank) hits harder. Call
        /// AFTER Configure (the deployer does). Values &lt; 1 are clamped to 1 (a multiplier
        /// only ever buffs — it never weakens a fresh troop). Idempotent re-base: re-reads
        /// the def's base each call so repeated calls don't compound.
        /// </summary>
        public void ApplyDamageMultiplier(float multiplier)
        {
            // Re-base from the EFFECTIVE baseline (def, or the upgraded value ApplyUpgradeStats set)
            // so veterancy/perk multipliers compound on top of a WO-771.9 upgrade instead of wiping
            // it. With no upgrade applied, _baseAttackDamage == def.AttackDamage → identical to before.
            _attackDamage = _baseAttackDamage * Mathf.Max(1f, multiplier);
        }

        /// <summary>
        /// Applies a HEALTH multiplier to this troop's max HP (WO-430 city upgrades — the
        /// Armorer's troopHealthMult). BAKED AT SPAWN: re-reads the def's base MaxHp (so
        /// repeated calls don't compound) and sets HP to the new (buffed) max — a troop is
        /// born at full strength. Call AFTER Configure (the deployer does). Values &lt; 1 are
        /// clamped to 1 (a tier perk only buffs). Max HP is set ONCE at spawn, never
        /// live-scaled mid-fight (that would create current-HP exploit/death — owner-approved).
        /// </summary>
        public void ApplyHealthMultiplier(float multiplier)
        {
            // Re-base from the EFFECTIVE baseline (see ApplyDamageMultiplier) so a WO-771.9 upgrade
            // survives the perk multiply. With no upgrade applied, _baseMaxHp == def.MaxHp.
            _maxHp = _baseMaxHp * Mathf.Max(1f, multiplier);
            _hp = _maxHp;
        }

        /// <summary>
        /// WO-771.9 SPAWN-WIRING — applies a resolved <see cref="TroopRuntimeStats"/> (baseline
        /// folded with the troop's upgrade curves at its level, from
        /// <see cref="TroopStatResolver.Effective"/>) to this live unit ONCE at spawn: sets the
        /// effective HP / DPS(attack damage) / reach(attackRange) / aggro(huntScanRadius) as the
        /// new re-base baseline and refills HP, and records the unlocked special abilities
        /// (their StatusKind is applied to the real unit as effect data). Call AFTER
        /// <see cref="Configure"/> and BEFORE the veterancy/perk multipliers so those compound on
        /// the upgraded base. Null stats → no-op (pure baseline stays).
        /// </summary>
        public void ApplyUpgradeStats(TroopRuntimeStats stats)
        {
            if (stats == null) return;

            _upgradeLevel   = stats.Level < 1 ? 1 : stats.Level;
            _maxHp          = stats.MaxHp;
            _attackDamage   = stats.AttackDamage;
            _attackRange    = stats.AttackRange;
            _huntScanRadius = stats.AggroRadius;

            // The upgraded values become the new baseline the perk multipliers re-base from.
            _baseMaxHp        = stats.MaxHp;
            _baseAttackDamage = stats.AttackDamage;

            _hp = _maxHp;

            _unlockedAbilities.Clear();
            if (stats.UnlockedAbilities != null)
                foreach (var a in stats.UnlockedAbilities)
                    if (a != null) _unlockedAbilities.Add(a);
        }

        // ── IDamageableStructure (lets the enemy contact-attack lane hurt us) ──
        // Enemy.ProbeForStructure / EnemyBrain.TryAttack resolve their target via
        // GetComponentInParent<IDamageableStructure>(); implementing it here is the
        // "damageable wrapper" (same as StoryCompanion). The non-trigger collider the
        // factory attaches on a probe-visible layer is what lets that probe find us.
        bool IDamageableStructure.IsAlive => IsAlive;

        void IDamageableStructure.ApplyContactDamage(float amount) => TakeDamage(amount);

        /// <summary>
        /// Wires this troop from a <see cref="TroopDef"/> + spawn position. Called by
        /// the factory right after instantiation. HP, damage, speed and reach are read
        /// off the def.
        /// </summary>
        /// <param name="def">The troop def from <see cref="TroopCatalog"/>.</param>
        /// <param name="spawnPos">World position to seat the troop at.</param>
        public void Configure(TroopDef def, Vector3 spawnPos)
        {
            _def = def;
            if (def != null)
            {
                _troopId        = def.Id;
                _maxHp          = def.MaxHp;
                _attackDamage   = def.AttackDamage;
                _attackRange    = def.AttackRange;
                _attackCooldown = def.AttackCooldown;
                _moveSpeed      = def.MoveSpeed;
                _huntScanRadius = def.HuntScanRadius;
                _element        = ParseElement(def.Element);
            }

            // WO-771.9: seed the re-base baseline from the def; ApplyUpgradeStats overwrites it
            // when the troop spawns at an upgrade level (so it is never null-reffed downstream).
            _baseMaxHp        = _maxHp;
            _baseAttackDamage = _attackDamage;
            _upgradeLevel     = 1;
            _unlockedAbilities.Clear();

            _hp = _maxHp;
            _dead = false;

            // Snap to the spawn slot. Use Warp() when the agent is live so its internal
            // position stays in sync (a raw transform set would desync it → it'd snap
            // back / refuse to Move). Warp also lands on the nearest walkable point.
            if (_agent != null && _agent.isOnNavMesh)
                _agent.Warp(spawnPos);
            else
                transform.position = spawnPos;
        }

        private void Awake()
        {
            // The Animator sits on the skinned mesh child the factory seats.
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == AnimSpeed)  _hasSpeed  = true;
                    if (p.nameHash == AnimAttack) _hasAttack = true;
                    if (p.nameHash == AnimHit)    _hasHit    = true;
                    if (p.nameHash == AnimDead)   _hasDead   = true;
                }
            }
            _lastPosition = transform.position;

            // Mirror Pet's NavMeshAgent setup: drive it via Move() from our own eased
            // kinematics, manual facing (FaceToward). hero-ish radius/height so it paths
            // the shared single-agent navmesh like every other body.
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.agentTypeID = 0;            // share the hero's agent type / NavMeshLinks
            _agent.radius = 0.4f;
            _agent.height = 1.8f;
            _agent.baseOffset = 0f;
            _agent.speed = 30f;                // we drive via Move(); keep high so it never caps us
            _agent.acceleration = 200f;
            _agent.angularSpeed = 0f;
            _agent.updateRotation = false;     // facing handled manually (FaceToward)
            _agent.updateUpAxis = false;
            _agent.autoBraking = false;
            _agent.stoppingDistance = 0f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _attackCdRemaining = Mathf.Max(0f, _attackCdRemaining - dt);

            // Feed the Animator's Speed float from the actual per-frame displacement
            // (the agent is Move()-driven, no velocity to read). Guarded; no-op rig-less.
            if (_animator != null && _hasSpeed && dt > 0f)
            {
                float moved = (transform.position - _lastPosition).magnitude / dt;
                _animator.SetFloat(AnimSpeed, moved);
            }
            _lastPosition = transform.position;

            if (!IsAlive) return;

            // THROTTLE the target-hunt scan (mirrors Pet). Drop a cached foe that died /
            // was destroyed so we never aim at a corpse. Move + attack still run per-frame.
            _huntTimer -= dt;
            bool foeValid = _cachedFoe != null && _cachedFoe.IsAlive;
            if (_huntTimer <= 0f || !foeValid)
            {
                _huntTimer = HuntScanInterval;
                _cachedFoe = NearestHostile();
                foeValid = _cachedFoe != null && _cachedFoe.IsAlive;
            }

            var foe = foeValid ? _cachedFoe : null;
            if (foe == null)
            {
                // No foe — RALLY (WO-453 Step 4): if a global rally point is set and we are
                // farther than the arrival epsilon, walk toward it; otherwise idle in place.
                // Foe-in-range always wins (this branch only runs when there's no foe), so
                // rally only fills the idle gap (owner-decided default).
                Vector3? rally = TroopRally.Point;
                if (rally.HasValue)
                {
                    Vector3 r = rally.Value;
                    float flatDist = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(r.x, r.z));
                    if (flatDist > RallyArrivalEpsilon)
                        MoveToward(r, dt);
                }
                return;
            }

            Vector3 foePos = foe.WorldPosition;
            float dist = Vector3.Distance(transform.position, foePos);

            if (dist > _attackRange)
            {
                // hunt — close on the foe
                MoveToward(foePos, dt);
            }
            else
            {
                // in range — face the foe and attack on cooldown
                FaceToward(foePos);
                if (_attackCdRemaining <= 0f)
                    Attack(foe);
            }
        }

        // =====================================================================
        //  Combat — talks only to IDamageable (Hostile foes), like Pet.
        // =====================================================================

        /// <summary>
        /// The nearest living hostile <see cref="IDamageable"/> within the hunt-scan
        /// radius, or null. Discovery is via an enemy-LayerMask overlap — the troop
        /// never references the concrete Village Enemy type (copied from Pet).
        /// </summary>
        private IDamageable NearestHostile()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _huntScanRadius, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;
                float sqr = (dmg.WorldPosition - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = dmg;
                }
            }
            return best;
        }

        /// <summary>Lands one attack on <paramref name="foe"/> and resets the attack cooldown.</summary>
        private void Attack(IDamageable foe)
        {
            _attackCdRemaining = _attackCooldown;
            foe.TakeDamage(_attackDamage, _element);

            // Fire the strike animation in sync with the damage tick (guarded).
            if (_animator != null && _hasAttack) _animator.SetTrigger(AnimAttack);
        }

        // =====================================================================
        //  Damageable — contact damage through IDamageableStructure.
        // =====================================================================

        /// <summary>
        /// Applies <paramref name="amount"/> damage to this troop. At 0 HP it falls —
        /// plays the Dead anim and is destroyed after a short hold (EXPENDABLE; no pool
        /// or respawn). Mirrors Pet/StoryCompanion TakeDamage simplicity.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            _hp = Mathf.Max(0f, _hp - Mathf.Max(0f, amount));

            if (_animator != null && _hasHit && _hp > 0f) _animator.SetTrigger(AnimHit);
            if (_hp <= 0f) Die();
        }

        /// <summary>Heals the troop, clamped to max HP (for a future support kit).</summary>
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _hp = Mathf.Min(_maxHp, _hp + Mathf.Max(0f, amount));
        }

        /// <summary>The troop fell at 0 HP — latch the down state and destroy it (expendable).</summary>
        private void Die()
        {
            if (_dead) return;
            _dead = true;
            if (_animator != null && _hasDead) _animator.SetBool(AnimDead, true);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            Destroy(gameObject, DeathHoldSeconds);
        }

        // =====================================================================
        //  Movement — eased NavMeshAgent.Move() drift (copied from Pet).
        // =====================================================================

        private void MoveToward(Vector3 target, float dt)
        {
            Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);

            Vector3 toTarget = flatTarget - transform.position;
            float remaining = toTarget.magnitude;

            // Cruise speed, damped down as it nears the target so it eases to a stop.
            float desired = _moveSpeed * Mathf.Clamp01(remaining / ArrivalDamp);

            // Accelerate/decelerate toward the desired speed — launch ramp + soft stop.
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desired, Acceleration * dt);

            // Step, capped at the distance left so we never overshoot.
            float step = Mathf.Min(_currentSpeed * dt, remaining);

            // Move on the shared NavMesh — the agent clamps the step to the walkable
            // surface (no crossing walls/buildings) and follows its height. Fall back
            // to a raw transform move when the troop isn't on a NavMesh yet.
            if (remaining > 0.0001f)
            {
                Vector3 displacement = (toTarget / remaining) * step;
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.Move(displacement);
                else
                    transform.position = Vector3.MoveTowards(transform.position, flatTarget, step);
            }

            FaceToward(target);
        }

        private void FaceToward(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
        }

        private static DamageElement ParseElement(string element)
        {
            switch ((element ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "aether": return DamageElement.Aether;
                case "flame":  return DamageElement.Flame;
                case "ice":    return DamageElement.Ice;
                default:       return DamageElement.None;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _huntScanRadius);
        }
#endif
    }
}
