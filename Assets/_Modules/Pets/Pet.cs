// =============================================================================
// Pet — one in-village guardian pet (Week-4).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/pets/ + src/modules/village/pets/PetSprite
// -> _Modules/Pets/Pet.cs. One MonoBehaviour per deployed starter pet (Aether
// Sprite / Flame Pup / Ice Wolf). It reads its def from PetCatalog (pets.json),
// hunts the nearest enemy in range, and attacks on a cooldown.
//
// MODULE ISOLATION (port spec Part 2) — THE KEY DESIGN CALL:
//   DeNelle.Pets must NOT reference DeNelle.Village, so Pet.cs cannot see the
//   concrete Village `Enemy` type. Instead it attacks through
//   DeNelle.Core.Combat.IDamageable — a behaviourless contract in DeNelle.Core,
//   which BOTH modules already reference. The Village Enemy implements
//   IDamageable; Pet talks only to the interface. This mirrors the React
//   project's village combat registry, where the pet AI reads abstract enemy
//   runtime rows, never a concrete component (see PetSprite.tsx).
//
//   Enemy DISCOVERY is via Physics.OverlapSphere on an enemy LayerMask -> the
//   IDamageable on the hit collider. Pet never names DeNelle.Village.
//
// React parity: hunt speed / attack range / attack cooldown / per-rank damage
// all come from petData.ts + aggression.ts via pets.json. The defending pet
// "actively hunts — effectively unleashed" (petData.ts PET_DEFEND_LEASH = 999):
// it pursues the nearest enemy anywhere, returning to its home post when the
// field is clear.
// =============================================================================

using DeNelle.Core.Combat;
using DeNelle.Data;       // PetData SO — WO-86
using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>A deployed pet's behaviour mode — verbatim port of React's <c>PetMode</c>.</summary>
    public enum PetMode
    {
        /// <summary>Follows the hero around the village; does not fight.</summary>
        Idle = 0,
        /// <summary>Patrols as a defender — hunts the nearest enemy, auto-joins ATB.</summary>
        Defend = 1,
        /// <summary>Stationed at a wall span; held in ATB reserve for Rally.</summary>
        Fortify = 2,
    }

    /// <summary>
    /// One in-village guardian pet. Deployed at a slot ringing the Heart; in
    /// <see cref="PetMode.Defend"/> it hunts the nearest hostile
    /// <see cref="IDamageable"/> and attacks on a cooldown. Configured from a
    /// <see cref="PetDef"/> (pets.json) via <see cref="Configure"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Pet : MonoBehaviour
    {
        [Header("Identity (from pets.json)")]
        [Tooltip("Stable pet id — e.g. pet-aether-sprite. Set by Configure().")]
        [SerializeField] private string _petId;

        [Tooltip("Species id — aether-sprite / flame-pup / ice-wolf.")]
        [SerializeField] private string _species;

        [Tooltip("Bond rank 0–4 — drives max HP and per-hit damage.")]
        [SerializeField, Range(0, 4)] private int _bondRank;

        [Header("Data (WO-86)")]
        [Tooltip("Optional PetData ScriptableObject. When assigned, overrides attack stats in Configure(). Falls back to PetDef JSON values when null.")]
        [SerializeField] private PetData _petData;

        [Header("Behaviour")]
        [Tooltip("Deploy mode — Idle follows the hero, Defend hunts, Fortify holds a wall.")]
        [SerializeField] private PetMode _mode = PetMode.Defend;

        [Tooltip("Home post — the deploy slot ringing the Heart. Pet returns here when the field is clear.")]
        [SerializeField] private Vector3 _homePost;

        [Header("Combat")]
        [Tooltip("Layers swept for IDamageable enemy targets. Set to the village Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Tooltip("How far the pet will scan for an enemy to hunt (units). React PET_DEFEND_LEASH = 999.")]
        [SerializeField] private float _huntScanRadius = 60f;

        [Header("Live state")]
        [Tooltip("Current HP. Set from the bond-rank max HP by Configure().")]
        [SerializeField] private float _hp;

        // --- runtime, from the PetDef (pets.json) ---
        private PetDef _def;
        private float _maxHp = 70f;
        private float _attackDamage = 9f;
        private float _attackRange = 2.7f;
        private float _attackCooldown = 0.75f;
        private float _huntSpeed = 4.4f;
        private DamageElement _element = DamageElement.None;

        private float _attackCdRemaining;

        // ── Level progression (PetProgression drives these) ──────────────────
        // Stat multipliers from the pet's level. Default 1 (level 1 = base def
        // stats). _baseMaxHp captures the configured max so HP scaling is applied
        // off the base, not compounded each level.
        private float _progressionDmgMult = 1f;
        private float _baseMaxHp;

        // ── Natural locomotion (additive) ────────────────────────────────────
        // MoveToward eases speed up/down through _currentSpeed instead of
        // snapping to the full step, so pets accelerate out of rest, coast,
        // then damp as they arrive — no dead-straight constant-velocity dash.
        private float _currentSpeed;            // eased toward the target speed
        private const float Acceleration = 9f;  // units/s^2 ramp toward cruise
        private const float ArrivalDamp  = 1.6f; // start braking within this radius
        // Small per-pet multiplier so the three pets don't move in lockstep.
        // Stable per instance (seeded by id) — set in Awake.
        private float _speedJitter = 1f;

        // Reusable overlap buffer — avoids per-frame GC (OverlapSphereNonAlloc).
        private readonly Collider[] _overlap = new Collider[48];

        // ── Animation ─────────────────────────────────────────────────────────
        // The KayKit pet rig carries an Animator (the AnimatorSetup editor script
        // builds Pet.controller; the integrator assigns it to the pet prefab —
        // see docs/port-notes/animation-setup.md). Pet DRIVES it: the Speed float
        // blends idle <-> hunt-move, Attack fires on each strike, Hit on damage,
        // Dead latches the down state. Every Animator call is null-guarded so a
        // pet with no rig still fights. NOTE: the Ice Wolf is a QUADRUPED — codex
        // GAP-PRIMARY — and needs its own rig + controller, not Pet.controller.
        private Animator _animator;
        private Vector3 _lastPosition;

        // Animator parameter hashes — must match AnimatorSetup.cs's names.
        private static readonly int AnimSpeed  = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit    = Animator.StringToHash("Hit");
        private static readonly int AnimDead   = Animator.StringToHash("Dead");

        /// <summary>Stable pet id — e.g. <c>pet-aether-sprite</c>.</summary>
        public string PetId => _petId;

        /// <summary>Species id — aether-sprite / flame-pup / ice-wolf.</summary>
        public string Species => _species;

        /// <summary>Bond rank 0–4.</summary>
        public int BondRank => _bondRank;

        /// <summary>Deploy behaviour mode.</summary>
        public PetMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>Current HP.</summary>
        public float Hp => _hp;

        /// <summary>Max HP at the current bond rank.</summary>
        public float MaxHp => _maxHp;

        /// <summary>True while the pet is alive.</summary>
        public bool IsAlive => _hp > 0f;

        /// <summary>The pet's home post — its deploy slot ringing the Heart.</summary>
        public Vector3 HomePost => _homePost;

        /// <summary>
        /// Re-anchors the pet's home post. Used by the leash integrator so an
        /// Idle / Defend pet trails the hero around the village instead of
        /// drifting back to the Heart slot when the field is clear.
        /// </summary>
        public void SetHomePost(Vector3 worldPosition) => _homePost = worldPosition;

        /// <summary>The static def this pet was configured from (pets.json).</summary>
        public PetDef Def => _def;

        /// <summary>
        /// Sets the LayerMask the pet sweeps for enemy targets. The deployer /
        /// integrator calls this so the mask need not be authored per-prefab.
        /// </summary>
        public void SetEnemyMask(LayerMask enemyMask) => _enemyMask = enemyMask;

        /// <summary>
        /// Wires this pet from a <see cref="PetDef"/> + bond rank + home post.
        /// Called by the pet deployer right after instantiation. HP, damage,
        /// speed and reach are read off the def's bond-rank row.
        /// </summary>
        /// <param name="def">The pet def from <see cref="PetCatalog"/>.</param>
        /// <param name="bondRank">Bond rank 0–4.</param>
        /// <param name="homePost">World position of the deploy slot.</param>
        /// <param name="mode">Deploy mode — defaults to <see cref="PetMode.Defend"/>.</param>
        public void Configure(PetDef def, int bondRank, Vector3 homePost, PetMode mode = PetMode.Defend)
        {
            _def = def;
            _bondRank = Mathf.Clamp(bondRank, 0, 4);
            _homePost = homePost;
            _mode = mode;

            if (def != null)
            {
                _petId = def.Id;
                _species = def.Species;
                _huntSpeed = def.HuntSpeed;
                _attackRange = def.AttackRange;
                _attackCooldown = def.AttackCooldown;
                _element = ParseElement(def.Element);

                var rank = def.RankAt(_bondRank);
                if (rank != null)
                {
                    _maxHp = rank.MaxHp;
                    _attackDamage = rank.AttackDamage;
                }
            }

            // WO-86: overlay with PetData SO stats if assigned — SO wins over JSON.
            if (_petData != null)
            {
                _attackRange    = _petData.attackRange;
                _attackCooldown = _petData.attackCooldown;
                if (_petData.damage > 0f)
                    _attackDamage = _petData.damage;
            }

            _hp = _maxHp;
            transform.position = _homePost;
        }

        private void Awake()
        {
            // The Animator sits on the KayKit pet mesh child of the pet rig.
            _animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;

            // +/-12% stable per-pet cruise-speed variation so the pack doesn't
            // travel in perfect lockstep. Seeded by instance id (matches the
            // per-pet rng PetHeroLeash uses) so a scene restart is deterministic.
            var rng = new System.Random(gameObject.GetInstanceID());
            _speedJitter = 0.88f + (float)rng.NextDouble() * 0.24f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _attackCdRemaining = Mathf.Max(0f, _attackCdRemaining - dt);

            // Feed the Animator's Speed float from the actual per-frame
            // displacement — Pet moves kinematically (no agent / rigidbody to
            // read a velocity from). Null-guarded; no-op without a rig.
            if (_animator != null && dt > 0f)
            {
                float moved = (transform.position - _lastPosition).magnitude / dt;
                _animator.SetFloat(AnimSpeed, moved);
            }
            _lastPosition = transform.position;

            if (!IsAlive) return;
            // Idle / Fortify pets do not hunt — Idle trails the hero (the
            // integrator drives that), Fortify holds its wall span.
            if (_mode != PetMode.Defend) return;

            var foe = NearestHostile();
            if (foe == null)
            {
                // field clear — drift back to the home post (petData.ts: a
                // defender "returns to post only when none remain").
                MoveToward(_homePost, dt);
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

        /// <summary>
        /// Applies damage to this pet. Mirrors the React pet HP-registry write
        /// in PetSprite.tsx — a downed pet (HP &lt;= 0) stops fighting.
        /// </summary>
        /// <param name="amount">Damage on the 0–100 HP scale.</param>
        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            _hp = Mathf.Max(0f, _hp - Mathf.Max(0f, amount));

            if (_animator != null)
            {
                // Latch the down state at zero HP, else play the flinch.
                if (_hp <= 0f) _animator.SetBool(AnimDead, true);
                else _animator.SetTrigger(AnimHit);
            }
        }

        /// <summary>Heals the pet, clamped to max HP (Aether Sprite's Healing Spark, repairs).</summary>
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _hp = Mathf.Min(_maxHp, _hp + Mathf.Max(0f, amount));
        }

        // =====================================================================
        //  Combat — talks only to IDamageable (port spec Part 2 isolation).
        // =====================================================================

        /// <summary>
        /// The nearest living hostile <see cref="IDamageable"/> within the
        /// hunt-scan radius, or null. Discovery is via an enemy-LayerMask
        /// overlap — Pet never references the concrete Village Enemy type.
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

        /// <summary>
        /// Applies a level-progression stat bonus (called by PetProgression on
        /// level-up). Damage scales the per-hit damage; HP scales max HP off the
        /// configured base, preserving the current HP fraction so a level-up does
        /// not heal or hurt the pet.
        /// </summary>
        public void SetProgressionMultipliers(float damageMult, float hpMult)
        {
            _progressionDmgMult = Mathf.Max(1f, damageMult);

            if (_baseMaxHp <= 0f) _baseMaxHp = _maxHp;   // capture the configured max once
            float newMax = _baseMaxHp * Mathf.Max(1f, hpMult);
            float frac = _maxHp > 0f ? _hp / _maxHp : 1f;
            _maxHp = newMax;
            _hp = newMax * frac;
        }

        // Pet hits tint their floating damage number green (the hero's read cyan).
        private static readonly Color PetDamageColor = new Color(0.55f, 1.00f, 0.55f);

        /// <summary>Lands one attack on <paramref name="foe"/> and resets the attack cooldown.</summary>
        private void Attack(IDamageable foe)
        {
            _attackCdRemaining = _attackCooldown;
            float dealt = _attackDamage * _progressionDmgMult;
            // Source-tint this hit green so its floating number reads as PET damage,
            // distinct from the hero's cyan hits (IDamageTintable lives in Core).
            (foe as IDamageTintable)?.SetNextDamageTint(PetDamageColor);
            foe.TakeDamage(dealt, _element);

            // Kill-XP attribution: credit this pet's damage so it earns its share
            // of the enemy's XP when it dies (DamageAttribution lives in Core, so
            // Pets writes to it without referencing Village).
            DeNelle.Core.Combat.DamageAttribution.Record(foe, _petId, dealt);

            // Small element-coloured hit spark, reusing the hero ability VFX kit
            // via reflection (owner WO-35: pet hits had no VFX).
            PetAttackVfxBridge.Strike(ElementColor(_element), foe.WorldPosition);

            // Fire the strike animation in sync with the damage tick.
            if (_animator != null) _animator.SetTrigger(AnimAttack);

            // Ice Wolf's Frostbite (bond rank 1+) — attacks briefly slow the foe.
            // The other species' rank perks (burn, novas) are deeper Week-4+
            // wiring; the slow is the one with a clean IDamageable hook today.
            if (_element == DamageElement.Ice && _bondRank >= 1)
                foe.ApplyStatus(StatusEffect.Slow, 1.0f);
        }

        /// <summary>Hit-spark colour for a pet's element (WO-35).</summary>
        private static Color ElementColor(DamageElement e)
        {
            switch (e)
            {
                case DamageElement.Flame:  return new Color(1.00f, 0.44f, 0.26f); // #ff7043
                case DamageElement.Ice:    return new Color(0.49f, 0.83f, 0.99f); // #7dd3fc
                case DamageElement.Aether: return new Color(0.70f, 0.53f, 1.00f); // #b388ff
                default:                   return Color.white;
            }
        }

        // =====================================================================
        //  Movement — kinematic drift; NavMeshAgent wiring is the integrator's.
        // =====================================================================

        private void MoveToward(Vector3 target, float dt)
        {
            Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);

            // Distance left to cover on the ground plane.
            Vector3 toTarget = flatTarget - transform.position;
            float remaining = toTarget.magnitude;

            // Cruise speed for this pet, damped down as it nears the target so
            // it eases to a stop instead of snapping (arrival behaviour).
            float cruise = _huntSpeed * _speedJitter;
            float desired = cruise * Mathf.Clamp01(remaining / ArrivalDamp);

            // Accelerate/decelerate toward the desired speed rather than jumping
            // to it — gives a launch ramp and a soft stop.
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desired, Acceleration * dt);

            // Step, capped at the distance left so we never overshoot.
            float step = Mathf.Min(_currentSpeed * dt, remaining);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, step);

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
                case "flame": return DamageElement.Flame;
                case "ice": return DamageElement.Ice;
                default: return DamageElement.None;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.62f, 0.44f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);
            Gizmos.color = new Color(0.49f, 0.84f, 0.99f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _huntScanRadius);
        }
#endif
    }
}
