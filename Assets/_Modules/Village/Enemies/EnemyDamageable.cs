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
    public sealed class EnemyDamageable : MonoBehaviour, IDamageable, IDamageTintable
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
                    _enemy = GetComponent<Enemy>() ?? GetComponentInParent<Enemy>();
                return _enemy != null ? _enemy : null;         // fake-null -> real null
            }
        }

        // --- pending status timers (consumed when Enemy models them) ---
        private float _slowUntil;
        private float _freezeUntil;
        private float _burnUntil;

        /// <summary>Enemies are always hostile to the village's defenders.</summary>
        public CombatFaction Faction => CombatFaction.Hostile;

        /// <summary>World position of the enemy — used by range / nearest queries.
        /// Guarded for a destroyed adapter so a late land-burst query can't NRE.</summary>
        public Vector3 WorldPosition => this != null ? transform.position : Vector3.zero;

        /// <summary>Current enemy HP.</summary>
        public float Hp => E != null ? E.Hp : 0f;

        /// <summary>True while the enemy is alive and a valid attack target.</summary>
        public bool IsAlive => E != null && !E.IsDead && E.Hp > 0f;

        /// <summary>True while a freeze status is still active (Frost Nova / Glacial Bond).</summary>
        public bool IsFrozen => Time.time < _freezeUntil;

        /// <summary>True while a slow status is still active (Frostbite / Ranger snare).</summary>
        public bool IsSlowed => Time.time < _slowUntil;

        /// <summary>True while a burn DoT is still active (Emberbite).</summary>
        public bool IsBurning => Time.time < _burnUntil;

        private void Awake()
        {
            // Best-effort early cache; the E accessor self-heals if Enemy isn't on the
            // object yet at Awake (RequireComponent add-order) or sits on a parent.
            _enemy = GetComponent<Enemy>() ?? GetComponentInParent<Enemy>();
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
            E.TakeDamage(amount);
        }

        /// <summary>Forwards the source tint to the Enemy, which colours the next
        /// damage number it spawns (hero hits vs pet hits read differently).</summary>
        public void SetNextDamageTint(Color color)
        {
            if (E != null) E.SetNextDamageTint(color);
        }

        /// <summary>
        /// Records a status effect. Enemy.cs does not yet model status timers,
        /// so this stores the expiry locally and exposes <see cref="IsFrozen"/>
        /// / <see cref="IsSlowed"/> / <see cref="IsBurning"/> for the integrator
        /// to read into the enemy's nav speed once the fields land.
        /// </summary>
        public void ApplyStatus(StatusEffect effect, float seconds)
        {
            if (seconds <= 0f) return;
            float until = Time.time + seconds;
            switch (effect)
            {
                case StatusEffect.Slow:   _slowUntil   = Mathf.Max(_slowUntil, until);   break;
                case StatusEffect.Freeze: _freezeUntil = Mathf.Max(_freezeUntil, until); break;
                case StatusEffect.Burn:   _burnUntil   = Mathf.Max(_burnUntil, until);   break;
            }
        }
    }
}
