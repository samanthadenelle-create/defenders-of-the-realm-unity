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
using DeNelle.Core.Diagnostics;   // FlowTrace - [Flow:TroopVisual]
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
        private static readonly List<TroopController> Active = new List<TroopController>();

        /// <summary>Allocation-free live roster used by raid towers and squad support AI.</summary>
        public static IReadOnlyList<TroopController> ActiveTroops => Active;
        [Header("Identity (from troops.json)")]
        [Tooltip("Stable troop id — e.g. troop-footman. Set by Configure().")]
        [SerializeField] private string _troopId;

        [Tooltip("The persisted PlayerTroop.Id this body was deployed from (WO-453 Step 4). " +
                 "Used by the retreat reconcile to tell survivors from the wounded. Empty for a " +
                 "dev/loose spawn that wasn't drawn from the army.")]
        [SerializeField] private string _ownedTroopId;

        [Header("Combat")]
        [Tooltip("Layers swept for IDamageable hostile targets. Set to the village Enemy layer; " +
                 "Awake and SetEnemyMask add the Structure layer on top (WO-853) so raid walls " +
                 "and gates are acquirable.")]
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
        // WO-933 siege: prefer Hostile structures; bias damage structure vs unit.
        private bool _preferStructures;
        private float _structureDamageMult = 1f;
        private float _unitDamageMult = 1f;
        private bool _isSupport;

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
        // WO-853 raised this from 48: the hunt mask now includes the Structure layer, so a
        // sweep inside a raid base returns every wall panel in the 14 m scan radius as well as
        // the enemy bodies. OverlapSphereNonAlloc truncates at the buffer length and its result
        // order is arbitrary, so a 48-slot buffer filled with wall panels would crowd the enemy
        // colliders out and stop the troop finding a foe at all.
        private readonly Collider[] _overlap = new Collider[128];

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
        private static readonly int AnimSpeed    = Animator.StringToHash(AnimParams.Speed);
        private static readonly int AnimAttack   = Animator.StringToHash(AnimParams.Attack);
        private static readonly int AnimCast     = Animator.StringToHash(AnimParams.Cast);
        private static readonly int AnimInCombat = Animator.StringToHash(AnimParams.InCombat);
        private static readonly int AnimHit      = Animator.StringToHash(AnimParams.Hit);
        private static readonly int AnimDead     = Animator.StringToHash(AnimParams.Dead);
        private bool _hasSpeed, _hasAttack, _hasCast, _hasInCombat, _hasHit, _hasDead;
        /// <summary>Mage/Cleric controllers: strike fires Cast; melee/ranged fire Attack.</summary>
        private bool _useCastStrike;
        /// <summary>
        /// WO-935 Phase 3 (archer row): Ranger controllers read as a BOW SHOT - a released
        /// arrow that flies to the target - instead of the melee connect arc. Mutually
        /// exclusive with <see cref="_useCastStrike"/> by construction (one resolver,
        /// TroopFactory.ResolveRoleController, returns exactly one of Mage / Ranger / Knight).
        /// </summary>
        private bool _useBowShot;
        /// <summary>
        /// Lazily attached bow-shot presentation (WO-935 Phase 3). RangedAttackVFX is the
        /// INCUMBENT projectile launcher and is reused verbatim - Enemy.EnsureCastVfx attaches
        /// the very same component the very same way for enemy ranged casts. It owns the
        /// pooled body, the release flash and the arrival impact, so this slice writes NO
        /// second projectile mover, which is exactly what the work order forbids.
        /// </summary>
        private RangedAttackVFX _bowVfx;

        // §12 instrumentation (owner defect 2026-08-02 "troops slide / T-pose"): the LAST step of the
        // chain — proof that a parameter was actually written to a live Animator. One line per troop,
        // then never again (no per-frame log spam). If [Flow:TroopVisual] shows the controller was
        // assigned but this line never appears, the dead step is HERE (the param cache), not the rig.
        private bool _tracedFirstParamWrite;

        // How long the corpse lingers after death before it's destroyed (lets the
        // Dead anim play). EXPENDABLE — no pool / respawn.
        private const float DeathHoldSeconds = 3f;
        private bool _dead;

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

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
        /// factory calls this so the mask need not be authored per-instance. The Structure
        /// layer is added on top of whatever the caller passes (see
        /// <see cref="WithStructureLayer"/>) — <c>TroopDeployer.VillageEnemyMask</c> hands over
        /// the Enemy layer alone, and a troop that cannot sweep Structure can never find a wall.
        /// </summary>
        public void SetEnemyMask(LayerMask enemyMask) => _enemyMask = WithStructureLayer(enemyMask);

        /// <summary>
        /// WO-853: returns <paramref name="mask"/> with the "Structure" layer added.
        /// Walls and gates STAY on Structure — that layer is the tower line-of-sight blocker
        /// mask, so relayering them onto Enemy would make towers shoot through walls — which
        /// means the only way a sweep can find one is to include Structure in the mask.
        /// <see cref="LayerMask.GetMask"/> returns 0 for an undeclared layer, so the OR is a
        /// no-op and any caller's ~0 fallback survives unchanged.
        /// Widening is safe because <see cref="NearestHostile"/> rejects every non-Hostile
        /// faction: the player's own perimeter reports Friendly and is skipped.
        /// </summary>
        private static LayerMask WithStructureLayer(LayerMask mask) =>
            mask.value | LayerMask.GetMask("Structure");

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
                // WO-933: role "siege" → structure-prefer hunt (WC Demolisher / CoC wall-breaker).
                _preferStructures = string.Equals(def.Role, "siege", System.StringComparison.OrdinalIgnoreCase);
                _isSupport = string.Equals(def.Role, "support", System.StringComparison.OrdinalIgnoreCase);
                _structureDamageMult = def.StructureDamageMult > 0f ? def.StructureDamageMult : 1f;
                _unitDamageMult = def.UnitDamageMult > 0f ? def.UnitDamageMult : 1f;
                // Melee -> Knight Attack; archer -> Ranger Attack; mage -> Mage Cast.
                _useCastStrike = TroopFactory.UsesCastStrike(def, def.Model);
                // WO-935 Phase 3 (archer row): the same resolver decides the STRIKE READ.
                _useBowShot = TroopFactory.UsesBowShot(def, def.Model);
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
            //
            // ORDER IS LOAD-BEARING (owner defect 2026-08-02): this Awake runs SYNCHRONOUSLY inside
            // TroopFactory's AddComponent<TroopController>(), so whatever controller is bound at this
            // instant decides — for the whole life of the troop — which params are ever written.
            // TroopFactory.ApplyTroopAnimator therefore binds BEFORE that AddComponent. If that ever
            // gets reordered, every flag below stays false and the troop silently slides again, which
            // is exactly what the trace lines here exist to catch.
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == AnimSpeed)    _hasSpeed    = true;
                    if (p.nameHash == AnimAttack)   _hasAttack   = true;
                    if (p.nameHash == AnimCast)     _hasCast     = true;
                    if (p.nameHash == AnimInCombat) _hasInCombat = true;
                    if (p.nameHash == AnimHit)      _hasHit      = true;
                    if (p.nameHash == AnimDead)     _hasDead     = true;
                }
            }

            // §12: split "no animator" vs "no controller" vs "controller speaks a different
            // vocabulary" — the three distinct ways this troop ends up frozen. One line per spawn.
            if (_animator == null)
            {
                FlowTrace.Fail("TroopVisual",
                    $"id={_troopId}: NO Animator anywhere under the troop root - the body cannot animate at all " +
                    "(model missing -> tinted-capsule fallback, or a rig-less prop was skinned).");
            }
            else if (_animator.runtimeAnimatorController == null)
            {
                FlowTrace.Fail("TroopVisual",
                    $"id={_troopId}: Animator on '{_animator.gameObject.name}' has NO runtimeAnimatorController at " +
                    "Awake - every parameter write is skipped for this troop's whole life; it will slide/T-pose. " +
                    "TroopFactory.ApplyTroopAnimator must bind BEFORE AddComponent<TroopController>().");
            }
            else if (!_hasSpeed)
            {
                FlowTrace.Fail("TroopVisual",
                    $"id={_troopId}: controller '{_animator.runtimeAnimatorController.name}' declares NO '" +
                    AnimParams.Speed + "' parameter (params=" + DescribeParams(_animator) + ") - this troop " +
                    "will slide/T-pose. A vendor-pack controller (e.g. Supercyan StrafeMovement) speaks a " +
                    "different vocabulary; bind a controller built to AnimParams instead.");
            }
            else
            {
                FlowTrace.Step("TroopVisual",
                    $"id={_troopId}: driver armed on controller '{_animator.runtimeAnimatorController.name}' " +
                    $"- Speed={_hasSpeed} Attack={_hasAttack} Cast={_hasCast} InCombat={_hasInCombat} " +
                    $"Hit={_hasHit} Dead={_hasDead} useCastStrike={_useCastStrike}.");
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

            // WO-853: add the Structure layer to whatever mask was authored on this instance,
            // so a scene-placed / dev-spawned troop that never receives SetEnemyMask still
            // sweeps walls and gates. A no-op when the mask is already ~0 or Structure is
            // undeclared. SetEnemyMask applies the same widening to the deployer's mask.
            _enemyMask = WithStructureLayer(_enemyMask);
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
                if (!_tracedFirstParamWrite)
                {
                    // FIRST PARAM WRITTEN — the last step of the chain, proven once per troop.
                    _tracedFirstParamWrite = true;
                    FlowTrace.Step("TroopVisual",
                        $"id={_troopId}: FIRST param write - SetFloat('{AnimParams.Speed}', {moved:F2}) on " +
                        $"'{_animator.runtimeAnimatorController.name}'. The animation chain is live end to end.");
                }
            }
            _lastPosition = transform.position;

            if (!IsAlive) return;

            // Support troops form the squad's sustain layer. They heal the most-injured
            // nearby ally and follow it into range; when nobody needs healing they fall
            // through to the normal hostile hunt so they never stand inert.
            if (_isSupport && TryHealSquadmate(dt)) return;

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
                SetInCombat(false);
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

            SetInCombat(true);
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

        private void SetInCombat(bool on)
        {
            if (_animator != null && _hasInCombat)
                _animator.SetBool(AnimInCombat, on);
        }

        private bool TryHealSquadmate(float dt)
        {
            TroopController target = null;
            float lowestRatio = 1f;
            float rangeSqr = _huntScanRadius * _huntScanRadius;
            for (int i = 0; i < Active.Count; i++)
            {
                var ally = Active[i];
                if (ally == null || ally == this || !ally.IsAlive || ally._hp >= ally._maxHp) continue;
                float sqr = (ally.transform.position - transform.position).sqrMagnitude;
                if (sqr > rangeSqr) continue;
                float ratio = ally._maxHp > 0f ? ally._hp / ally._maxHp : 1f;
                if (ratio < lowestRatio) { lowestRatio = ratio; target = ally; }
            }
            if (target == null) return false;

            SetInCombat(true);
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > _attackRange)
            {
                MoveToward(target.transform.position, dt);
                return true;
            }

            FaceToward(target.transform.position);
            if (_attackCdRemaining > 0f) return true;
            _attackCdRemaining = _attackCooldown;
            target.Heal(_attackDamage);
            // A heal must read instantly in the raid scrum: warm cast at the cleric,
            // green-gold impact on the ally. These route through the pooled VFX manager.
            VFXManager.Play(VFXType.Cast_Heal, transform.position, transform.rotation, playSound: false);
            VFXManager.Play(VFXType.Impact_Heal, target.transform.position, target.transform.rotation);
            if (_animator != null)
            {
                if (_hasCast) _animator.SetTrigger(AnimCast);
                else if (_hasAttack) _animator.SetTrigger(AnimAttack);
            }
            return true;
        }

        // =====================================================================
        //  Combat — talks only to IDamageable (Hostile foes), like Pet.
        // =====================================================================

        /// <summary>
        /// The nearest living hostile <see cref="IDamageable"/> within the hunt-scan
        /// radius, or null. Discovery is via an enemy-LayerMask overlap — the troop
        /// never references the concrete Village Enemy type (copied from Pet).
        /// WO-933 siege: when <see cref="_preferStructures"/> is set, any Hostile that
        /// also implements <see cref="IDamageableStructure"/> beats pure units (nearest
        /// among structures first; else nearest unit — never freezes idle).
        /// </summary>
        private IDamageable NearestHostile()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _huntScanRadius, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            IDamageable bestUnit = null;
            IDamageable bestStruct = null;
            float bestUnitSqr = float.MaxValue;
            float bestStructSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;
                float sqr = (dmg.WorldPosition - transform.position).sqrMagnitude;
                if (_preferStructures && IsHostileStructure(dmg))
                {
                    if (sqr < bestStructSqr)
                    {
                        bestStructSqr = sqr;
                        bestStruct = dmg;
                    }
                }
                else if (sqr < bestUnitSqr)
                {
                    bestUnitSqr = sqr;
                    bestUnit = dmg;
                }
            }
            if (_preferStructures && bestStruct != null)
            {
                if (_cachedFoe != bestStruct)
                    FlowTrace.Step("TroopSiege",
                        $"id={_troopId}: prefer structure '{DescribeTarget(bestStruct)}' " +
                        $"(unit-fallback={(bestUnit != null ? DescribeTarget(bestUnit) : "none")}).");
                return bestStruct;
            }
            return bestUnit;
        }

        /// <summary>
        /// Hostile structures dual-implement <see cref="IDamageable"/> +
        /// <see cref="IDamageableStructure"/> (walls, towers, gates, spire). Pure
        /// garrison units typically only implement <see cref="IDamageable"/>.
        /// </summary>
        private static bool IsHostileStructure(IDamageable dmg)
        {
            if (dmg == null || dmg.Faction != CombatFaction.Hostile) return false;
            return dmg is IDamageableStructure;
        }

        private static string DescribeTarget(IDamageable dmg)
        {
            if (dmg is Component c && c != null) return c.GetType().Name;
            return dmg != null ? dmg.GetType().Name : "?";
        }

        /// <summary>Lands one attack on <paramref name="foe"/> and resets the attack cooldown.</summary>
        private void Attack(IDamageable foe)
        {
            _attackCdRemaining = _attackCooldown;
            float dmg = _attackDamage;
            if (_preferStructures || _structureDamageMult != 1f || _unitDamageMult != 1f)
            {
                bool structure = IsHostileStructure(foe);
                float mult = structure ? _structureDamageMult : _unitDamageMult;
                if (mult > 0f) dmg *= mult;
            }
            // WO-935: mage strike uses unified CombatCast (anim + VFX) then damage.
            Transform foeTf = (foe as Component) != null ? (foe as Component).transform : null;
            // NOTE (2026-08-15 review): Hunter's Mark scaling now lives in ONE place —
            // Enemy.TakeDamageFrom (CombatMark GameObject-key fix). Scaling here too would
            // double-apply. The cast spell id also follows the troop's element instead of
            // hardcoding Fireball, so a Holy caster no longer plays Fire VFX.
            if (_useCastStrike)
            {
                CombatCast.Play(CastSpellIdFor(_element), transform, foeTf, () =>
                {
                    if (foe != null && foe.IsAlive)
                        foe.TakeDamage(dmg, _element);
                });
            }
            else
            {
                foe.TakeDamage(dmg, _element);
                if (_animator != null)
                {
                    if (_hasAttack)
                        _animator.SetTrigger(AnimAttack);
                    else if (_hasCast)
                        _animator.SetTrigger(AnimCast);
                }

                // WO-935 Phase 3 (melee row) — THE BLOW LANDING WAS THE ONLY SILENT BEAT IN A
                // TROOP MELEE EXCHANGE. The swing anim plays above and the damage lands, but
                // nothing marked the CONTACT, so on a structure target — which has no health bar
                // in view — the player could not tell a hit from a whiff. The mage row has had
                // its cast presentation since 2026-08-15 (CombatCast, the branch above); this is
                // the same beat for everyone who swings.
                //
                // Deliberately a VERBATIM MIRROR of the enemy-side melee connect
                // (Enemies/Enemy.cs, "melee connect vfx"): the two sides of the same exchange must
                // read the same way, and copying the shipped pattern is what keeps them paired.
                //   • Impact_Physical is ALREADY cataloged (Editor/VFXCatalogGenerator.cs -> Lana
                //     Slash_stone_once) and is a ONESHOT, so it cannot consume one of the 20
                //     leak-prone loop slots — troop melee ticks fast and a loop row here would
                //     saturate the cap in seconds.
                //   • It is a stone-slash ARC: the read is silhouette and direction, not hue
                //     (colourblind law, CLAUDE.md §7 / WO-935 §2 VFX rule 5).
                //   • Placed at the TARGET's chest, not the troop's — that is where the blow
                //     resolves and where the eye already is.
                //   • playSound:false — the attack cue belongs to the animator; VFXManager must
                //     not layer a second one.
                // Guard.Try on both, so a VFX fault can never cost the damage that already landed.
                var foeComp = foe as Component;
                if (foeComp != null)
                {
                    Vector3 hitPos = foeComp.transform.position + Vector3.up * 1.0f;

                    // WO-935 Phase 3 (ARCHER ROW) - the bow shot, and the reason it forks here
                    // rather than layering on top of the melee arc.
                    //
                    // The archer's damage was, and REMAINS, instant and hit-scan: TakeDamage has
                    // already run at the top of this branch and is deliberately untouched, so DPS,
                    // threat and every downstream damage hook are byte-identical to before this
                    // change. This is option (a) of the work order - PURE PRESENTATION over the
                    // existing instant damage. Option (b), a real travel time, moves combat maths
                    // and is a different ticket.
                    //
                    // RangedAttackVFX is the INCUMBENT launcher and is reused verbatim: Enemy
                    // attaches the identical component the identical way for enemy ranged casts
                    // (Enemies/Enemy.cs, EnsureCastVfx). It already owns the muzzle/release flash,
                    // the POOLED travelling body and the arrival impact, so this slice adds NO
                    // second projectile mover - which the work order forbids by name.
                    //
                    // NO onArrive callback is passed, on purpose. An arrival payload here would
                    // re-time the damage to the flight and quietly change DPS, and it would fire
                    // after this troop can have died. The arrow is decoration over a hit that has
                    // already landed.
                    //
                    // The melee arc is REPLACED, not layered: a stone-slash arc on a bow release
                    // would read as the wrong verb, and the two must stay distinguishable at a
                    // glance in greyscale (colourblind law).
                    if (_useBowShot)
                    {
                        Guard.Try("TroopVisual", "bow shot vfx", () =>
                        {
                            var bow = EnsureBowVfx();
                            if (bow != null) bow.FireArrow(hitPos);
                        });
                        return;
                    }

                    Guard.Try("TroopVisual", "melee connect vfx", () =>
                        VFXManager.Play(VFXType.Impact_Physical, hitPos,
                                        Quaternion.identity, playSound: false));

                    // WHAT the blow landed on, layered ON TOP of the generic arc rather than
                    // replacing it: the arc is the CONTACT read and always fires, the surface
                    // burst is the MATERIAL read. Resolve returns None rather than guessing and
                    // Play no-ops on None, so an unrecognised target degrades to exactly the
                    // behaviour above this comment.
                    Guard.Try("TroopVisual", "melee surface impact", () =>
                        HitSurfaceVfx.ResolveAndPlay(foeComp, hitPos));
                }
            }
        }

        /// <summary>Cast presentation id for the troop's damage element. None keeps the
        /// owner-observed Fireball look (the only shipped cast-strike troop, SC_Mage, is
        /// element None — do not change its felt visual); Aether/Ice read as Arcane until
        /// dedicated casts exist.</summary>
        private static string CastSpellIdFor(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Flame: return CombatCast.Fireball;
                case DamageElement.Aether:
                case DamageElement.Ice:   return CombatCast.Arcane;
                default:                  return CombatCast.Fireball;
            }
        }

        /// <summary>
        /// WO-935 Phase 3 (archer row): lazily attach the INCUMBENT ranged launcher.
        /// A VERBATIM mirror of Enemy.EnsureCastVfx - same component, same lazy
        /// TryGetComponent-then-AddComponent shape - so both sides of a ranged exchange are
        /// launched by one owner rather than by two near-copies that can drift.
        /// Never returns a component on a dead body.
        /// </summary>
        private RangedAttackVFX EnsureBowVfx()
        {
            if (this == null) return null;
            if (_bowVfx == null)
                _bowVfx = TryGetComponent<RangedAttackVFX>(out var rv) ? rv : gameObject.AddComponent<RangedAttackVFX>();
            return _bowVfx;
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

        /// <summary>
        /// The parameter names a bound controller actually declares, for the §12 trace. Naming them
        /// is what turns "the troop does not animate" into "this controller speaks MoveVertical/
        /// Grounded/MoveState, not Speed/Attack/Hit/Dead" in a single captured line. Capped so a
        /// 32-parameter vendor controller cannot flood the log.
        /// </summary>
        private static string DescribeParams(Animator anim)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return "<no controller>";
            var ps = anim.parameters;
            if (ps == null || ps.Length == 0) return "<none>";
            var sb = new System.Text.StringBuilder();
            int max = ps.Length < 12 ? ps.Length : 12;
            for (int i = 0; i < max; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(ps[i] != null ? ps[i].name : "<null>");
            }
            if (ps.Length > max) sb.Append("/... (+").Append(ps.Length - max).Append(" more)");
            return sb.ToString();
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
