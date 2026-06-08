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
using DeNelle.Core.State;     // WO-36: GameState backstop for hero class self-resolve
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

        // WO-163: cached "Cast" param presence for the currently-resolved Animator.
        // HeroBodySwapper assigns a fresh runtimeAnimatorController at runtime, so
        // re-scan whenever the resolved Animator changes (same pattern as
        // HeroLocomotion.RefreshParamCache). Driving an absent param logs an error.
        private bool _hasCastParam;
        private Animator _paramCheckedAnimator;

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

            // WO-36 (Bug 1 backstop): self-resolve hero class from GameState so the
            // correct Q/W/E/R loadout is active even in test scenes where
            // HeroBodySwapper is absent. HeroBodySwapper.Start() calls SetHeroClass()
            // directly in the normal village flow (runs after Awake), so this only
            // matters for stand-alone test scenes and editor play without a full rig.
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                var opt = svc.State?.HeroClass.ToNullable();
                if (opt.HasValue)
                {
                    _heroClass = opt.Value switch
                    {
                        // Fully-qualified: unqualified 'HeroClass' shadows to a string in this scope.
                        DeNelle.Core.State.HeroClass.Knight => "knight",
                        DeNelle.Core.State.HeroClass.Ranger => "ranger",
                        DeNelle.Core.State.HeroClass.Mage   => "mage",
                        // WO-226: the Cleric is a caster — reuse the Mage loadout.
                        DeNelle.Core.State.HeroClass.Cleric => "mage",
                        _                                   => AbilityCatalog.DefaultClass,
                    };
                    Debug.Log($"[HeroAbilities] Awake backstop: resolved class '{ _heroClass}' from GameState.");
                }
            }
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
            float scaledCooldown = def.Cooldown * HeroTalentModifiers.CooldownMultiplier(_heroClass);
            _cooldownRemaining[(int)slot] = scaledCooldown;
            _mana -= def.ManaCost;

            // WO-97 Bug 3: drive the per-slot cooldown fill overlay on the ability
            // button. AbilityCooldownUI lives on the button GameObject; the ability
            // HeroAbilities lives on the hero rig, so we broadcast to any registered
            // listener via the HudBridge rather than a direct GetComponent. As a
            // belt-and-braces fallback also check this GO (in case someone collocates).
            GetComponent<AbilityCooldownUI>()?.StartCooldown(scaledCooldown);

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
            // WO-163: (re)scan the resolved controller for the "Cast" param so we
            // never drive an absent param (a controller swap rebinds the animator).
            if (_animator != null && _animator != _paramCheckedAnimator)
            {
                _paramCheckedAnimator = _animator;
                _hasCastParam = false;
                if (_animator.runtimeAnimatorController != null)
                {
                    foreach (var p in _animator.parameters)
                        if (p.nameHash == AnimCast) { _hasCastParam = true; break; }
                }
            }
            if (_animator != null && _hasCastParam) _animator.SetTrigger(AnimCast);

            // Core fix: also drive the guarded ActorAnimator (IActorAnimator) so the
            // DTT Patricia hero (and village) get the Cast/Attack states from the
            // HeroAnimatorFactory controllers (upper-body layer + base). The legacy
            // direct _animator is kept for any direct listeners; ActorAnimator
            // re-resolves on body swap and guards missing params.
            var actor = GetComponent<ActorAnimator>();
            if (actor == null) actor = GetComponentInChildren<ActorAnimator>(true);
            // Class-specific attack/cast per factory controllers: Knight uses melee Attack
            // clip/stance, Ranger (aim), Mage/Cleric (cast). Call both; per-class controller
            // maps the right one (or no-op if param missing).
            actor?.PlayAttack(0);
            actor?.PlayCast();

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

        // DEF (combat feel): lazily-attached ranged projectile launcher (arrow / spell orb).
        private RangedAttackVFX _rangedVfx;

        // Gear v1: lazily-attached equipped-gear loadout — its WeaponMult joins the damage chain.
        private GearLoadout _gear;

        // DEF-47: timing bonus captured by TryCast() from AttackTimingBonus,
        // consumed by the next ResolveEffect() damage call, then reset to 1.
        private float _pendingTimingBonus = 1f;

        // ── Defend-the-Tower aim overrides (null in village → behaviour unchanged) ──
        /// <summary>When set, offensive abilities resolve from this world point (the
        /// turret player's crosshair / aim target) instead of the hero's feet — so a
        /// stationary hero's spells reach the distant enemies. PatriciaLightController
        /// sets it per cast; village mode leaves it null.</summary>
        public Vector3? AimPointOverride;
        /// <summary>When set by HeroTargetIndicator, single-target offensive abilities hit
        /// THIS exact reticle-locked foe instead of re-searching via an OverlapSphere — so
        /// the hero damages precisely what the ring shows (the registry target, the same one
        /// companions hit), even if that enemy's collider isn't found by a physics sweep.
        /// Null → fall back to NearestHostile.</summary>
        public IDamageable LockedTarget;
        /// <summary>When set, Heal effects route here (repair the tower) instead of
        /// healing the caster. Returns true when it handled the heal. Null = heal hero.</summary>
        public System.Func<float, bool> HealHandler;

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
            float dmg = def.Damage * HeroTalentModifiers.DamageMultiplier(_heroClass) * levelMult * _pendingTimingBonus * WeaponMult();
            _pendingTimingBonus = 1f;

            // DTT: offensive abilities resolve from the player's aim point (crosshair
            // target) so a stationary turret hero's spells reach the distant enemies.
            // Null override (village) keeps the original hero-centred behaviour.
            Vector3 atk = AimPointOverride ?? origin;

            switch (def.EffectEnum)
            {
                case AbilityEffect.Heal:
                {
                    // WO-220: the Heal branch never routes through SpawnVfx (where every
                    // other ability fires its cast SFX), so the heal cast was silent.
                    // Play the class-flavoured cast sting here directly so every ability
                    // beat — offensive AND heal — has a sound. The bridge null-guards
                    // CoreServices.Audio internally.
                    AbilityAudioBridge.PlayForClassAndKind(_heroClass, def.EffectEnum);

                    // DTT routes the heal to repair the TOWER (HealHandler). Otherwise
                    // it heals the CASTER — executive call 2026-05-28: "heal hero is
                    // correct — cannot heal a tree." def.Damage carries the amount.
                    if (HealHandler == null || !HealHandler(def.Damage))
                    {
                        var heroHp = GetComponent<HeroHealth>() ?? HeroHealth.Instance;
                        if (heroHp != null) heroHp.Heal(def.Damage);
                        VFXManager.Play(VFXType.Cast_Heal, origin + Vector3.up * 1.2f);
                    }
                    break;
                }

                case AbilityEffect.Strike:
                case AbilityEffect.Snare:
                {
                    // Prefer the reticle's locked target (registry — exactly what the ring
                    // shows + companions hit); fall back to the OverlapSphere search. This
                    // fixes "ring locks but my hits do 0" — the physics sweep was finding a
                    // different/no enemy than the registry-locked one.
                    var foe = (LockedTarget != null && LockedTarget.IsAlive)
                        ? LockedTarget
                        : NearestHostile(atk, def.Range + _enemyHitRadius);
                    // WO-125 Bug 1: the apex dragon orbits at altitude ~22-34u — far
                    // outside a short-slot sweep (Q ~13u) — so an OverlapSphere from the
                    // hero's feet can never reach it. In village mode (no aim override),
                    // when no ground enemy is in reach, let the single-target offensive
                    // slots punch up at the live boss so the airborne apex is hittable
                    // (not only during a low swoop). Ground targeting is untouched: a
                    // reachable ground enemy is still preferred. Resolved through the
                    // Core IDamageable seam via WaveManager — no concrete/HUD ref added.
                    if (foe == null && AimPointOverride == null)
                        foe = LiveBoss();
                    if (foe != null)
                    {
                        // DEF (combat feel): LAUNCH a visible projectile (Ranger arrow / Mage
                        // orb) and land the damage WHEN it arrives — "seeing the arrow/spell go
                        // is fun; click-button-instant-FX is sad." Capture the payload for the
                        // arrival closure; the enemy's TakeDamage fires all the impact juice
                        // (red flash + damage number + hit-stop) at the moment of connection.
                        var hitFoe = foe;
                        float hitDmg = dmg;
                        var hitEl = element;
                        bool snare = def.EffectEnum == AbilityEffect.Snare;
                        LaunchProjectile(foe.WorldPosition, () =>
                        {
                            if (hitFoe == null || !hitFoe.IsAlive) return;
                            hitFoe.TakeDamage(hitDmg, hitEl);
                            DeNelle.Core.Combat.DamageAttribution.Record(hitFoe, HeroProgression.Id, hitDmg);
                            if (snare) hitFoe.ApplyStatus(StatusEffect.Slow, 2.5f); // castAbility.ts snare
                        });
                    }
                    // Cast beat only (origin SFX/VFX). Impact juice now comes from the projectile
                    // ARRIVAL (enemy TakeDamage), so no target hint -> no premature impact flash.
                    SpawnVfx(atk, def, 1.6f, null);
                    break;
                }

                case AbilityEffect.Aoe:
                case AbilityEffect.Cleave:
                    // blast centred on the aim point (the crosshair cluster)
                    Blast(atk, def.Range, dmg, element, def.Freeze);
                    SpawnVfx(atk, def, def.Range);
                    break;

                case AbilityEffect.Meteor:
                {
                    // blast centred on the nearest enemy cluster to the aim point
                    var foe = NearestHostile(atk, float.MaxValue);
                    // WO-125 Bug 1: Meteor's 1000u sweep already encloses the orbiting
                    // dragon (it's a layer-8 IDamageable with a collider), so this is a
                    // belt-and-braces fallback for the rare case the sweep misses it.
                    if (foe == null && AimPointOverride == null)
                        foe = LiveBoss();
                    Vector3 target = foe != null ? foe.WorldPosition : atk;
                    // DEF (combat feel): hurl a visible orb to the target, then EXPLODE on arrival
                    // (blast + impact VFX), so the ultimate reads as a meteor streaking in and
                    // landing rather than an instant area-pop. Same proven projectile pattern as
                    // Strike/Snare; the RangedAttackVFX cast-burst covers the cast beat.
                    LaunchProjectile(target, () =>
                    {
                        Blast(target, def.Range, dmg, element, 0f);
                        SpawnVfx(target, def, def.Range);
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// DEF (combat feel): launches a VISIBLE projectile (Ranger arrow / Mage-or-other
        /// spell orb) toward <paramref name="target"/> and invokes <paramref name="onArrive"/>
        /// when it lands — so ranged hits read as the shot travelling + connecting rather than
        /// an instant hit-scan ("seeing the arrow/spell go is fun"). Lazily attaches
        /// <see cref="RangedAttackVFX"/> so it works on every hero without a builder change.
        /// </summary>
        private void LaunchProjectile(Vector3 target, System.Action onArrive)
        {
            if (_rangedVfx == null)
                _rangedVfx = GetComponent<RangedAttackVFX>() ?? gameObject.AddComponent<RangedAttackVFX>();
            if (_heroClass == "ranger") _rangedVfx.FireArrow(target, onArrive);
            else                        _rangedVfx.FireSpellOrb(target, onArrive);
        }

        /// <summary>
        /// Gear v1: the equipped weapon's damage multiplier (1.0 when none / no catalog).
        /// Lazily attaches <see cref="GearLoadout"/> so every hero gets gear with no builder
        /// change; graceful — a missing catalog leaves the multiplier at 1.0.
        /// </summary>
        private float WeaponMult()
        {
            if (_gear == null) _gear = GetComponent<GearLoadout>() ?? gameObject.AddComponent<GearLoadout>();
            return _gear.WeaponMult;
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

        // WO-125 Bug 1: cached WaveManager so the hero can reach the apex boss (which
        // lives in WaveManager._liveApexBoss, NOT in the OverlapSphere-reachable enemy
        // roster). Resolved lazily and re-resolved only while null — survives a body swap.
        private WaveManager _wave;

        /// <summary>
        /// The live apex boss as a hostile <see cref="IDamageable"/>, or null when no
        /// boss is up / it is dead. Lets the short-range offensive slots punch up at an
        /// airborne boss they could never sweep. Talks only to the Core seam.
        /// </summary>
        private IDamageable LiveBoss()
        {
            if (_wave == null)
            {
                var found = FindObjectsByType<WaveManager>(FindObjectsSortMode.None);
                _wave = found.Length > 0 ? found[0] : null;
            }
            var boss = _wave?.LiveApexBoss;
            if (boss != null && boss.IsAlive && ((IDamageable)boss).Faction == CombatFaction.Hostile)
                return boss;
            return null;
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
                // bug-triage P2: startLifetimeMultiplier is the curve multiplier, not seconds —
                // use the actual max lifetime (matches VFXManager.DetectDuration) so longer
                // prefab effects aren't destroyed before their particles finish.
                float life = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(ps.gameObject, life + 0.5f);
                return;
            }

            // WO-195: add a distinct element-coded CAST flash so each spell reads
            // as its own element (fire / frost / arcane / holy) at the cast beat.
            // SpellVfxFactory resolves (effect, class, accent) -> a Cast_* VFXType
            // and plays it through the canonical VFXManager (pooled prefab when
            // wired, procedural fallback otherwise). Additive — the main per-class
            // effect below is unchanged.
            SpellVfxFactory.PlayCast(def.EffectEnum, _heroClass, def.UnityColor, at);

            // DEF-VFX-01: route through VFXManager so prefab-based art swaps require
            // no code changes. Falls back to procedural if no prefab is wired.
            AbilityVfxKit.PlayHeroAbility(def.EffectEnum, def.UnityColor, at,
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
    }
}
