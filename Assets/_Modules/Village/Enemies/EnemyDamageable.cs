// =============================================================================
// EnemyDamageable — bridges the village Enemy to the cross-module IDamageable.
// -----------------------------------------------------------------------------
// Port spec Part 2 (module isolation): hero abilities (DeNelle.Village) and pets
// (DeNelle.Pets) both attack enemies, but the Pets module's asmdef must NOT
// reference DeNelle.Village. The shared seam is DeNelle.Core.Combat.IDamageable
// — a behaviourless contract in DeNelle.Core, which both modules already
// reference (see week4-hero-pets-gate.md, decision row).
//
// WHY AN ADAPTER, NOT `Enemy : IDamageable` DIRECTLY:
//   The cleanest end state is for Enemy itself to implement IDamageable. But the
//   Week-4 task brief says "create NEW files" and leaves Enemy.cs wiring to the
//   integrator. So this adapter is a NEW file that sits ON the Enemy GameObject
//   and forwards the interface to the existing Enemy component. The hero/pet
//   target sweeps do GetComponentInParent<IDamageable>(), which finds this
//   adapter. The integrator may later fold this into Enemy directly and delete
//   this file — both forms satisfy the same contract.
//
// INTEGRATOR: add EnemyDamageable to the Enemy prefab (RequireComponent pulls
// it automatically if you add [RequireComponent(typeof(EnemyDamageable))] to
// Enemy). Without it, hero abilities + pets cannot find / hit enemies.
//
// STATUS EFFECTS: Enemy.cs does not yet model slow / freeze / burn timers, so
// ApplyStatus is a logged no-op for now — the hooks land when Enemy gains the
// status fields (mirrors the React EnemyRuntime freezeUntil / slowUntil flags).
// =============================================================================

using DeNelle.Core.Combat;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Adapter MonoBehaviour that exposes a village <see cref="Enemy"/> as a
    /// <see cref="IDamageable"/> so hero abilities and pets — including the
    /// isolated <c>DeNelle.Pets</c> module — can damage it without referencing
    /// the concrete <see cref="Enemy"/> type. Lives on the Enemy GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyDamageable : MonoBehaviour, IDamageable, IDamageTintable, IHeroDamageMarkable, ICombatLayered
    {
        private Enemy _enemy;

        // 2026-06-02 ROOT FIX: Enemy has [RequireComponent(typeof(EnemyDamageable))], so a
        // runtime AddComponent<Enemy>() adds THIS adapter FIRST — its Awake then caches
        // GetComponent<Enemy>() while Enemy doesn't exist yet, leaving _enemy null forever
        // and IsAlive permanently false (targeting + damage silently skip the enemy). Re-
        // resolve lazily so the cache self-heals regardless of component-add order.
        //
        // 2026-06-04 DEF-268 NRE FIX: a hero ranged projectile's land-burst can read this
        // adapter (IsAlive) AFTER the enemy GameObject was destroyed (the projectile killed
        // it on impact, then the burst re-checks the now-destroyed target). On a destroyed
        // MonoBehaviour `this == null` is true (Unity fake-null) and touching gameObject /
        // GetComponent throws a real NRE — so bail to null FIRST. Also collapse a fake-null
        // _enemy to a real null so callers' `E != null` guards behave correctly.
        private Enemy E
        {
            get
            {
                if (this == null) return null;                 // adapter itself destroyed
                if (_enemy == null)
                {
                    _enemy = GetComponent<Enemy>();
                    if (_enemy == null) _enemy = GetComponentInParent<Enemy>();
                }
                return _enemy != null ? _enemy : null;         // fake-null -> real null
            }
        }

        private readonly CombatStatusTracker _status = new CombatStatusTracker();

        /// <summary>Enemies are always hostile to the village's defenders.</summary>
        public CombatFaction Faction => CombatFaction.Hostile;

        /// <summary>
        /// ICombatLayered — the air/ground layer this enemy occupies, forwarded from
        /// the underlying <see cref="Enemy.IsFlying"/>. Lets a tower's acquisition loop
        /// gate on the targeting matrix through the Core seam (no Village reference).
        /// A destroyed/missing Enemy reads as Ground (safe default).
        /// </summary>
        public CombatLayer Layer => E != null ? E.CombatLayer : CombatLayer.Ground;

        /// <summary>World position of the enemy — used by range / nearest queries.
        /// Guarded for a destroyed adapter so a late land-burst query can't NRE.</summary>
        public Vector3 WorldPosition => this != null ? transform.position : Vector3.zero;

        /// <summary>Current enemy HP.</summary>
        public float Hp => E != null ? E.Hp : 0f;

        /// <summary>True while the enemy is alive and a valid attack target.</summary>
        public bool IsAlive => E != null && !E.IsDead && E.Hp > 0f;

        /// <summary>True while a freeze status is still active (Frost Nova / Glacial Bond).</summary>
        public bool IsFrozen => _status.IsFrozen;

        /// <summary>True while a slow status is still active (Frostbite / Ranger snare).</summary>
        public bool IsSlowed => _status.IsSlowed;

        /// <summary>True while a burn DoT is still active (Emberbite).</summary>
        public bool IsBurning => _status.IsBurning;

        private void Awake()
        {
            // Best-effort early cache; the E accessor self-heals if Enemy isn't on the
            // object yet at Awake (RequireComponent add-order) or sits on a parent.
            _enemy = GetComponent<Enemy>();
            if (_enemy == null) _enemy = GetComponentInParent<Enemy>();
        }

        /// <summary>Routes ability / pet damage into the existing <see cref="Enemy.TakeDamage"/>.</summary>
        /// <param name="amount">Damage on the 0–100 HP scale.</param>
        /// <param name="element">Element of the damage source (resist math is a later pass).</param>
        public void TakeDamage(float amount, DamageElement element)
        {
            if (E == null || !IsAlive) return;
            // WO-219: stamp the element so Enemy.TakeDamageFrom can tint the impact
            // burst (flame / ice / aether) for this one hit. None = melee/physical →
            // Enemy keeps its existing grey-spark VfxPool path. Consumed once there.
            E.SetNextImpactElement(element);
            // Element resist / bonus math is a later tuning pass — enemies.json
            // does not yet carry per-element resistances. Forward raw damage.
            var resolved = ElementalDamageResolver.Resolve(amount, element, E.Affinity, E.ElementalVulnerabilities);
            E.TakeDamage(resolved.FinalAmount);
        }

        /// <summary>Forwards the source tint to the Enemy, which colours the next
        /// damage number it spawns (hero hits vs pet hits read differently).</summary>
        public void SetNextDamageTint(Color color)
        {
            if (E != null) E.SetNextDamageTint(color);
        }

        /// <summary>Ticket #61: forwards the hero-source mark to the Enemy so the NEXT hit
        /// (and a kill it causes) feeds the combo / kill-streak / RAMPAGE feedback. Only the
        /// hero's attack/ability paths call this; pets, towers, DoT and environment do not.</summary>
        public void MarkNextHitFromHero()
        {
            if (E != null) E.SetNextDealtByHero(true);
        }

        /// <summary>Records a CC status and exposes timers for nav + HUD producers.</summary>
        public void ApplyStatus(StatusEffect effect, float seconds) => _status.Apply(effect, seconds);

        /// <summary>Collect active statuses for the battle HUD enemy buff/debuff row.</summary>
        public void CollectActive(System.Collections.Generic.List<ActiveStatusSnapshot> dst, int max = 6)
            => _status.CollectActive(dst, max);
    }
}
