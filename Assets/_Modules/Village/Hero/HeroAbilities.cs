// =============================================================================
// HeroAbilities — Blaise's Q/W/E/R combat block (Week-4).
// -----------------------------------------------------------------------------
// Port spec Part 3 / Part 5 Week 4: src/modules/village/hero/castAbility.ts +
// heroAbilities.ts -> HeroAbilities.cs. Reads ability tuning from abilities.json
// via AbilityCatalog; resolves casts against the village's IDamageable enemies;
// uses Unity built-in ParticleSystems as placeholder VFX (the React shockwave
// ring + combatFx — final art is later).
//
//   Q  Arcane Bolt    — strike: single hit on the nearest enemy in range.
//   W  Frost Nova     — aoe:    blast around the hero, freezes what it catches.
//   E  Healing Beacon — heal:   restores Heart HP, no enemy damage.
//   R  Meteor Strike  — meteor: blast centred on the nearest enemy cluster.
//
// PORT NOTE — castAbility.ts mutated a CombatState struct directly. The Unity
// port has no CombatState yet (the village combat registry is a later week), so
// HeroAbilities owns its own mana pool + per-slot cooldown timers and discovers
// enemies via Physics.OverlapSphere -> IDamageable. The cast math (cooldown /
// mana gate, nearest-in-range, blast-radius hit test) is line-equivalent to
// castAbility.ts.
//
// MODULE ISOLATION (port spec Part 2): this file is in DeNelle.Village, so it
// CAN see DeNelle.Village.Enemy directly — but it deliberately talks to enemies
// only through DeNelle.Core.Combat.IDamageable, the same seam the Pets module
// uses. That keeps the cast code independent of the concrete Enemy component.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Combat;
using DeNelle.Village.Talents; // WO-36: talent -> ability-stat multipliers
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Blaise's hero ability block — the Mage's Q/W/E/R kit. Holds the mana
    /// pool + per-slot cooldown timers, resolves a queued cast each frame, and
    /// spawns placeholder particle VFX. Lives on the hero rig; wired by
    /// <see cref="VillageController"/> (the integrator hands it the Heart ref).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroAbilities : MonoBehaviour
    {
        [Header("Hero class")]
        [Tooltip("Hero class id read from abilities.json. v2 foundation = mage (Blaise).")]
        [SerializeField] private string _heroClass = AbilityCatalog.DefaultClass;

        [Header("Mana")]
        [Tooltip("Max mana pool. The React combat state used 0-10 (heroAbilities.ts).")]
        [SerializeField] private float _maxMana = 10f;

        [Tooltip("Mana regenerated per second. React: 0.9/s * manaRegenMul (Aether pet perk).")]
        [SerializeField] private float _manaRegenPerSecond = 0.9f;

        [Tooltip("Mana-regen multiplier — raised by the Aether Sprite's Mana Tide perk.")]
        [SerializeField, Min(0.1f)] private float _manaRegenMultiplier = 1f;

        [Header("Scene refs (wired by VillageController / integrator)")]
        [Tooltip("Elarion — the Heart. Healing Beacon (E) restores its HP.")]
        [SerializeField] private HeartController _heart;

        [Tooltip("Layers an ability hit-test sweeps for IDamageable targets. Set to the Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Header("Placeholder VFX")]
        [Tooltip("Optional — a ParticleSystem spawned at the cast point. " +
                 "When null, HeroAbilities builds a built-in particle burst at runtime.")]
        [SerializeField] private ParticleSystem _castVfxPrefab;

        [Tooltip("Enemy collision radius added to ability range — matches ENEMY_HIT_R in castAbility.ts.")]
        [SerializeField] private float _enemyHitRadius = 0.85f;

        // --- runtime state (the React CombatState fields HeroAbilities owns) ---
        private float _mana;
        private readonly float[] _cooldownRemaining = new float[4]; // indexed by AbilitySlot

        // Reusable overlap buffer — avoids per-cast GC (Physics.OverlapSphereNonAlloc).
        private readonly Collider[] _overlap = new Collider[64];

        // ── Animation ─────────────────────────────────────────────────────────
        // The hero rig's Animator (Hero.controller, built by the AnimatorSetup
        // editor script; assigned to the hero prefab by the integrator — see
        // docs/port-notes/animation-setup.md) plays the Q/W/E/R cast animation.
        // HeroAbilities fires the Cast trigger whenever an ability resolves.
        // Null-guarded so the cast logic runs without a rig.
        private Animator _animator;

        /// <summary>Animator <c>Cast</c> trigger hash — matches AnimatorSetup.cs.</summary>
        private static readonly int AnimCast = Animator.StringToHash("Cast");

        /// <summary>Current mana, 0..<see cref="MaxMana"/>.</summary>
        public float Mana => _mana;

        /// <summary>Max mana pool.</summary>
        public float MaxMana => _maxMana;

        /// <summary>Hero class id (drives the abilities.json lookup).</summary>
        public string HeroClass => _heroClass;

        /// <summary>Mana-regen multiplier — bumped by the Aether Sprite's Mana Tide perk.</summary>
        public float ManaRegenMultiplier
        {
            get => _manaRegenMultiplier;
            set => _manaRegenMultiplier = Mathf.Max(0.1f, value);
        }

        /// <summary>Seconds of cooldown remaining on a slot — 0 means ready.</summary>
        public float CooldownRemaining(AbilitySlot slot) => _cooldownRemaining[(int)slot];

        /// <summary>0..1 cooldown fill for the HUD — 1 = ready, 0 = just cast.</summary>
        public float CooldownFraction(AbilitySlot slot)
        {
            var def = AbilityCatalog.Find(_heroClass, slot);
            if (def == null || def.Cooldown <= 0f) return 1f;
            return 1f - Mathf.Clamp01(_cooldownRemaining[(int)slot] / def.Cooldown);
        }

        /// <summary>True when the slot is off cooldown AND there is enough mana to cast it.</summary>
        public bool CanCast(AbilitySlot slot)
        {
            var def = AbilityCatalog.Find(_heroClass, slot);
            if (def == null) return false;
            return _cooldownRemaining[(int)slot] <= 0f && _mana >= def.ManaCost;
        }

        /// <summary>Wires the Heart reference (the integrator calls this from VillageController).</summary>
        public void SetHeart(HeartController heart) => _heart = heart;

        /// <summary>
        /// Sets the hero class id that drives the abilities.json lookup (WO-36).
        /// The field defaults to "mage" and was never reassigned, so a Knight or
        /// Ranger cast the Mage loadout. HeroBodySwapper calls this after swapping
        /// in the real class body so each hero casts its own kit. AbilityCatalog
        /// normalises the id, but lower-case it here to be safe.
        /// </summary>
        public void SetHeroClass(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return;
            _heroClass = slug.Trim().ToLowerInvariant();
            Debug.Log($"[HeroAbilities] Hero class set to '{_heroClass}' (abilities will resolve from this loadout).");
        }

        private void Awake()
        {
            _mana = _maxMana;
            // The Animator sits on the KayKit hero mesh child of the hero rig.
            _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // ── mana regen + cooldown ticks (heroAbilities.ts lines 122-126) ──
            _mana = Mathf.Min(_maxMana, _mana + _manaRegenPerSecond * dt * _manaRegenMultiplier);
            for (int i = 0; i < _cooldownRemaining.Length; i++)
                _cooldownRemaining[i] = Mathf.Max(0f, _cooldownRemaining[i] - dt);
        }

        /// <summary>
        /// Attempts to cast the Q/W/E/R ability in <paramref name="slot"/>.
        /// Returns true when the cast fired (cooldown + mana satisfied), false
        /// when it was on cooldown / out of mana / unknown. Mirrors the
        /// cooldown+mana gate at the top of castAbility.ts.
        /// </summary>
        public bool TryCast(AbilitySlot slot)
        {
            var def = AbilityCatalog.Find(_heroClass, slot);
            if (def == null)
            {
                Debug.LogWarning($"[HeroAbilities] No ability for {_heroClass}/{slot} in abilities.json.");
                return false;
            }

            // castAbility.ts: `if (combat.cd[kind] > 0 || combat.mana < def.mana) return null;`
            if (_cooldownRemaining[(int)slot] > 0f || _mana < def.ManaCost)
                return false;

            // WO-36 (talent -> stat): unlocked skill-tree talents shave cooldowns
            // class-wide via CdReduction. CooldownMultiplier returns 1f when the
            // hero has no cooldown talents unlocked, preserving the JSON baseline.
            _cooldownRemaining[(int)slot] = def.Cooldown * HeroTalentModifiers.CooldownMultiplier(_heroClass);
            _mana -= def.ManaCost;

            // Play the hero's cast animation in sync with the ability resolving.
            // Self-heal the reference: Awake() caches it before HeroBodySwapper
            // swaps the real FBX body in, so the Awake cache is stale/null.
            // HeroBodySwapper re-caches this via reflection after the swap, but
            // re-resolve here too as a backstop (only while null).
            if (_animator == null)
            {
                var bodyT = transform.Find("HeroBody");
                if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
            }
            if (_animator != null) _animator.SetTrigger(AnimCast);

            Vector3 origin = transform.position;

            // DEF-47: register the cast with the timing-bonus tracker. The returned
            // multiplier (1.00–1.50× depending on chain depth) is stored and applied
            // to outgoing damage inside ResolveEffect. Heals are unaffected.
            _pendingTimingBonus = AttackTimingBonus.NotifyCast(origin);

            ResolveEffect(def, origin);
            return true;
        }

        // =====================================================================
        //  Effect resolution — line-equivalent to castAbility.ts.
        // =====================================================================

        // Cached level-progression on the hero (added at runtime by
        // ProgressionManager). Its DamageMultiplier scales ability damage on top
        // of the talent multiplier; resolved lazily so it survives the body swap.
        private HeroProgression _progression;

        // DEF-47: timing bonus captured by TryCast() from AttackTimingBonus,
        // consumed by the next ResolveEffect() damage call, then reset to 1.
        private float _pendingTimingBonus = 1f;

        private void ResolveEffect(AbilityDef def, Vector3 origin)
        {
            DamageElement element = ElementOf(def);

            // WO-36 (talent -> stat): scale outgoing enemy damage by the hero's
            // unlocked DamageBonus talents (class-wide). DamageMultiplier returns
            // 1f when nothing is unlocked, so the abilities.json baseline holds
            // until the player learns a damage node. NOTE: the Heal case below
            // deliberately uses raw def.Damage (heal amount), not this scalar.
            // The level-progression multiplier stacks on top (1f until level 2+).
            if (_progression == null) _progression = GetComponent<HeroProgression>();
            float levelMult = _progression != null ? _progression.DamageMultiplier : 1f;
            // DEF-47: apply the chain timing bonus captured in TryCast.
            // _pendingTimingBonus is 1.00× when no chain is active, up to 1.50×
            // at chain 4+. Reset to 1f so a missed follow-up can't carry over.
            float dmg = def.Damage * HeroTalentModifiers.DamageMultiplier(_heroClass) * levelMult * _pendingTimingBonus;
            _pendingTimingBonus = 1f;

            switch (def.EffectEnum)
            {
                case AbilityEffect.Heal:
                    // castAbility.ts: heartHp = min(heartMax, heartHp + power)
                    if (_heart != null)
                        _heart.SetHp(_heart.Hp + def.Damage);
                    SpawnVfx(_heart != null ? _heart.transform.position : origin, def, 5f);
                    break;

                case AbilityEffect.Strike:
                case AbilityEffect.Snare:
                {
                    // nearest enemy within reach + ENEMY_HIT_R
                    var foe = NearestHostile(origin, def.Range + _enemyHitRadius);
                    if (foe != null)
                    {
                        foe.TakeDamage(dmg, element);
                        DeNelle.Core.Combat.DamageAttribution.Record(foe, HeroProgression.Id, dmg);
                        if (def.EffectEnum == AbilityEffect.Snare)
                            foe.ApplyStatus(StatusEffect.Slow, 2.5f); // castAbility.ts snare
                    }
                    SpawnVfx(origin, def, 1.6f, foe != null ? foe.WorldPosition : (Vector3?)null);
                    break;
                }

                case AbilityEffect.Aoe:
                case AbilityEffect.Cleave:
                    // blast centred on the caster
                    Blast(origin, def.Range, dmg, element, def.Freeze);
                    SpawnVfx(origin, def, def.Range);
                    break;

                case AbilityEffect.Meteor:
                {
                    // blast centred on the nearest enemy cluster (castAbility.ts)
                    var foe = NearestHostile(origin, float.MaxValue);
                    Vector3 target = foe != null ? foe.WorldPosition : origin;
                    Blast(target, def.Range, dmg, element, 0f);
                    SpawnVfx(target, def, def.Range);
                    break;
                }
            }
        }

        /// <summary>
        /// Damages every hostile <see cref="IDamageable"/> within
        /// <paramref name="radius"/> + the enemy hit radius of
        /// <paramref name="centre"/>. Mirrors the local <c>blast()</c> closure
        /// in castAbility.ts.
        /// </summary>
        private void Blast(Vector3 centre, float radius, float damage, DamageElement element, float freezeSeconds)
        {
            float r = radius + _enemyHitRadius;
            int count = Physics.OverlapSphereNonAlloc(centre, r, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var target = AsHostile(_overlap[i]);
                if (target == null) continue;
                // OverlapSphere is centre-distance; castAbility.ts hypot()'s the
                // same way, so no extra precision pass is needed.
                target.TakeDamage(damage, element);
                DeNelle.Core.Combat.DamageAttribution.Record(target, HeroProgression.Id, damage);
                if (freezeSeconds > 0f)
                    target.ApplyStatus(StatusEffect.Freeze, freezeSeconds);
            }
        }

        /// <summary>
        /// The nearest living hostile <see cref="IDamageable"/> within
        /// <paramref name="maxRange"/> of <paramref name="origin"/>, or null.
        /// Mirrors the local <c>nearest()</c> closure in castAbility.ts.
        /// </summary>
        private IDamageable NearestHostile(Vector3 origin, float maxRange)
        {
            float sweep = maxRange == float.MaxValue ? 1000f : maxRange;
            int count = Physics.OverlapSphereNonAlloc(origin, sweep, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var target = AsHostile(_overlap[i]);
                if (target == null) continue;
                float sqr = (target.WorldPosition - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = target;
                }
            }
            return best;
        }

        /// <summary>Resolves a collider to a living hostile target, or null.</summary>
        private static IDamageable AsHostile(Collider col)
        {
            if (col == null) return null;
            var dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) return null;
            return dmg;
        }

        private static DamageElement ElementOf(AbilityDef def)
        {
            // Map the Mage kit's effect colours to elements for resist math.
            switch (def.EffectEnum)
            {
                case AbilityEffect.Aoe: return DamageElement.Ice;     // Frost Nova
                case AbilityEffect.Meteor: return DamageElement.Flame; // Meteor Strike
                default: return DamageElement.Aether;                  // Arcane Bolt / Beacon
            }
        }

        // =====================================================================
        //  Placeholder VFX — Unity built-in particles (port spec Week 4).
        // =====================================================================

        // WO-35: real per-ability VFX via AbilityVfxKit (was a single tinted dot
        // burst — the "random dots"). An authored _castVfxPrefab still overrides.
        // targetHint = the foe / impact point (drives the strike tracer + meteor fall).
        private void SpawnVfx(Vector3 at, AbilityDef def, float radius, Vector3? targetHint = null)
        {
            AbilityAudioBridge.PlayForClassAndKind(_heroClass, def.EffectEnum);   // class-flavoured SFX (WO-37)
            if (_castVfxPrefab != null)
            {
                ParticleSystem ps = Instantiate(_castVfxPrefab, at, Quaternion.identity);
                ps.Play();
                float life = ps.main.duration + ps.main.startLifetimeMultiplier;
                Destroy(ps.gameObject, life + 0.5f);
                return;
            }

            AbilityVfxKit.SpawnAbilityVfxForClass(def.EffectEnum, def.UnityColor, at,
                                          Mathf.Max(0.6f, radius), targetHint ?? at, _heroClass);
        }

        /// <summary>
        /// Builds a one-shot Unity built-in particle burst — the Week-4
        /// placeholder for the React ability shockwave ring (heroAbilities.ts).
        /// Final VFX art lands later.
        /// </summary>
        private static ParticleSystem BuildBuiltInBurst(Vector3 at)
        {
            var go = new GameObject("AbilityVFX_Placeholder");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.45f;                     // matches the React ring's 0.45s life
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 6f;
            main.startSize = 0.35f;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            // A runtime-added ParticleSystem ships with the legacy built-in
            // particle material, which URP renders as a magenta/invalid burst
            // (same missing-shader class as the pets in WO-05). Swap in a URP
            // unlit particle material so the placeholder is actually visible.
            // Only replace when a known shader resolves in THIS build (Shader.Find
            // returns null for a stripped shader) so we never trade the default
            // for a missing (magenta) one. Vertex colour is honoured by both
            // shaders, so TintAndSize's startColor still drives the ability hue.
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
                if (sh != null) psr.material = new Material(sh);
            }

            return ps;
        }

        /// <summary>Tints + scales a particle burst to an ability's colour / radius.</summary>
        private static void TintAndSize(ParticleSystem ps, Color color, float radius)
        {
            var main = ps.main;
            main.startColor = color;

            var shape = ps.shape;
            // Burst spread roughly matches the ability's blast radius so the
            // placeholder reads at the right scale in the scene.
            if (shape.enabled)
                shape.radius = Mathf.Clamp(radius * 0.5f, 0.2f, 8f);
        }
    }
}
