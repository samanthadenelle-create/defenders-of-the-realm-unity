// =============================================================================
// ArcaneTower — WO-113. A buildable MAGIC / AoE defence tower.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Sibling to DefenseTower (the single-target archer/wizard tower) but with a
// distinct ROLE: a slow-firing arcane spire that lobs an arcane pulse at the
// best target and detonates an AoE BLAST on impact — every Hostile inside the
// blast radius takes Aether damage and is SLOWED (the debuff). One shot hits a
// whole cluster, so it trades single-target DPS for crowd control + splash.
//
// SAME PATH as every other catalog structure: registered by a JSON row
// (behaviorId "ArcaneTower"), built by StructureFactory.AttachBehavior, charged
// + placed by BuildModeController, replayed by BaseLayoutLoader. The factory
// copies the RepoProps stat block straight onto the serialized fields below, so
// it stays fully data-tunable (range / damage / fireRate / element come from the
// catalog row; the AoE radius + slow live in the optional aoeRadius / slowSeconds
// repo fields, falling back to the sensible serialized defaults here when 0).
//
// Reuses: IDamageable (DeNelle.Core.Combat) for find + AoE damage + ApplyStatus,
// EnemyBrain.Role for the same backline-first priority as DefenseTower, and the
// shared VFXManager for the cast + blast feel. Targeting mirrors DefenseTower's
// FindObjectsByType<MonoBehaviour> IDamageable scan (works for ground roster AND
// the apex dragon, which implements IDamageable directly) so the arcane tower
// has the same reach the wizard tower does — no WaveManager coupling needed.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Slow-firing AoE magic tower: pulses an arcane blast that damages + SLOWS
    /// every Hostile in a radius around the impact. Stats are copied off the
    /// catalog RepoProps by StructureFactory; all fields are tunable in-Inspector.
    /// </summary>
    public sealed class ArcaneTower : MonoBehaviour, IDamageableStructure
    {
        [Header("Core combat (set from the catalog RepoProps by StructureFactory)")]
        public float Range     = 22f;
        public float Damage    = 16f;    // damage applied to EVERY enemy in the blast
        public float FireRate  = 0.6f;   // shots per second — intentionally slow (AoE trade-off)
        public bool  CanHitAir = true;   // arcane bolts arc — reach fliers like the wizard tower
        public float AirThreshold = 3.5f;
        public DamageElement Element = DamageElement.Aether;

        [Header("AoE blast (radius + the SLOW debuff — the arcane tower's identity)")]
        [Tooltip("Radius (metres) of the splash detonation around the impact point.")]
        public float AoeRadius = 6f;
        [Tooltip("Seconds of Slow applied to every enemy caught in the blast (0 = no slow).")]
        public float SlowSeconds = 2.5f;
        [Tooltip("Fraction of full Damage dealt to splash victims other than the primary target (0-1).")]
        [Range(0f, 1f)] public float SplashDamageFraction = 0.7f;

        [Header("Look")]
        public Color BlastColor = new Color(0.6f, 0.4f, 1f, 1f);   // arcane violet

        // VFX CHAIN (visual only — gameplay damage stays on <see cref="Element"/>).
        // Full Arcane-Fire tower roster (docs/MAGIC_VFX_LIBRARY.md), all mirrored under
        // Assets/Resources/VFX/Projectiles/ via SpellsPackVfxMirror:
        //   Casting_Fire_2 → Projectile_Fire_3 (travel) → Explosion_Fire + Spell_Fire_6 (detonation).
        // Spell_Fire_6 is a stationary swirl — never parent it to ProjectileMover; only on impact.
        [Header("VFX (visual only)")]
        [Tooltip("Element for travel + base impact burst (Explosion_*).")]
        public DamageElement BoltVisualElement = DamageElement.Flame;
        [Tooltip("Resources/VFX/Projectiles/<name> cast wind-up at the spire muzzle.")]
        public string BoltCastVfx = "Casting_Fire_2";
        [Tooltip("Resources/VFX/Projectiles/<name> extra AoE detonation layered on impact.")]
        public string BoltImpactExtraVfx = "Spell_Fire_6";

        // Elevation perk (wall-mounted): a spire seated on a wall-walk TOP gets the high-ground
        // range/LOS bonus. 1 = ground (no bonus); set by BaseLayoutLoader.Spawn (e.g. 1.25) when
        // wall-mounted. A MULTIPLIER on EffectiveRange so it survives tier upgrades. Bounded.
        public float ElevationRangeMult = 1f;

        // ── IDamageableStructure — the marching-enemy siege target (F8-41) ─────
        // ROOT of F8-41 (DefenseTargetableRegression): like DefenseTower, this component did NOT
        // implement IDamageableStructure, so Enemy.SweepForNearestStructure's
        // collider.GetComponentInParent<IDamageableStructure>() returned null for the spire —
        // enemies marched straight past it. Implementing the interface (mirrors WallSegment / Gate)
        // makes the arcane spire a real siege target. The arcane tower is always player-owned.
        //
        // HP: no per-entry hp is authored in structures-catalog.json / RepoProps, so this is a
        // serialized default. 160 is sturdier than a wall (WallSegment's 0-100 track) but a touch
        // squishier than the single-target DefenseTower (200) — the AoE spire reads as the softer,
        // higher-value backline target. Tunable.
        [Header("Durability (IDamageableStructure — enemy siege target)")]
        [Tooltip("Max HP. Enemies deal contact damage to the spire they path to. No catalog hp is " +
                 "authored; this default (160) is sturdier than a wall, slightly softer than DefenseTower (200).")]
        [SerializeField, Min(10f)] private float _maxHp = 160f;
        private float _hp = -1f;   // <0 = not yet initialised; set to _maxHp on first use / Awake

        // WO-672 Slice A (owner rulings F8-39 "either they exist or do not" + F8-42
        // broken = inoperable until repaired): at 0 HP the spire BREAKS instead of
        // Destroy(gameObject)ing — an inoperable in-world shell until Repair().
        // Mirrors the ResourceCollector Broken model.
        private bool _broken;

        /// <summary>True once enemies broke this spire (hp 0) — inoperable until <see cref="Repair"/>. (WO-672)</summary>
        public bool IsBroken => _broken;

        /// <summary>Health 0..1 — the wave damage-report fraction (WO-672; mirrors ResourceCollector.HpFraction).</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(Hp / _maxHp) : 0f;

        /// <summary>Fired once when enemies destroy this spire (HP reaches 0). Observers
        /// (respawn/persistence) can subscribe; the F8-39 respawn work owns re-placement.
        /// WO-672: fires at the BREAK moment (the spire now persists as an inoperable
        /// shell) — listeners release targets exactly as before.</summary>
        public event System.Action<ArcaneTower> Destroyed;

        /// <summary>Current HP (lazy-initialised to <see cref="_maxHp"/>).</summary>
        private float Hp
        {
            get { if (_hp < 0f) _hp = _maxHp; return _hp; }
        }

        /// <summary><see cref="IDamageableStructure"/> — true while the spire still stands
        /// (hp &gt; 0 and not broken, WO-672).</summary>
        public bool IsAlive => Hp > 0f && !_broken;

        /// <summary>
        /// WO-672 (F8-42): full restore — HP back to max, broken cleared; the Update fire
        /// loop resumes on its own (it early-outs only while <see cref="_broken"/>). Cost
        /// enforcement lives with the caller, mirroring ResourceCollector.Repair.
        /// </summary>
        public void Repair()
        {
            _broken = false;
            _hp = _maxHp;
            FlowTrace.Step("Structure", $"'{name}' REPAIRED (hp {_maxHp:0})");
        }

        /// <summary>
        /// <see cref="IDamageableStructure"/> contact-attack entry point — a Hollow One in melee
        /// contact routes its hit here (the SAME seam WallSegment / Gate / the Heart use). Reduces
        /// HP; at zero the spire is destroyed and <see cref="Destroyed"/> fires. Traces the hit +
        /// the kill (§12).
        /// </summary>
        public void ApplyContactDamage(float amount)
        {
            if (amount <= 0f || Hp <= 0f) return;

            _hp = Hp - amount;
            FlowTrace.Throttle("ArcaneTower", $"hurt:{GetInstanceID()}", 1f,
                $"'{name}' took {amount:0.#} contact dmg -> HP {_hp:0.#}/{_maxHp:0.#} (enemy siege).");

            if (_hp <= 0f)
            {
                _hp = 0f;
                _broken = true;
                // WO-672 Slice A: no Destroy(gameObject) — the spire persists as an
                // inoperable shell ("either they exist or do not", F8-39) until Repair().
                FlowTrace.Step("Structure", $"'{name}' BROKE (hp 0) — inoperable until repaired");
                Destroyed?.Invoke(this);
            }
        }

        private void Awake()
        {
            if (_hp < 0f) _hp = _maxHp;
            EnsureContactCollider();
        }

        /// <summary>
        /// Guarantees a NON-TRIGGER collider exists so the enemy sweep's
        /// Physics.OverlapSphere(..., QueryTriggerInteraction.Ignore) can actually RETURN this
        /// spire. Idempotent: skips if a solid collider already exists in the hierarchy (the
        /// skinned visual usually carries one). Sized from the visual's renderer bounds — mirrors
        /// Tower.EnsureBodyCollider (DEF-74). The IDamageableStructure lives on this root, so
        /// GetComponentInParent from any child collider resolves it.
        /// </summary>
        private void EnsureContactCollider()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger) return;   // already hittable by the sweep

            float height = 4.5f, radius = 0.9f;
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                height = Mathf.Max(1f, b.size.y);
                radius = Mathf.Max(0.4f, Mathf.Max(b.size.x, b.size.z) * 0.5f);
            }

            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = false;
            cap.height = height;
            cap.radius = radius;
            cap.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private float _cd;
        private float _scan;
        private readonly List<IDamageable> _hostiles = new List<IDamageable>();

        // WO-430 — the Arcane Tower upgrade buffs ITS OWN damage/range (towerDamageMult /
        // towerRangeMult). Always player-owned, so the perk always applies. LIVE-READ.
        //
        // WO-676 (BULWARK talents) — the hero's strategic tree is read at this SAME choke
        // point: Keen Ballistics (towerDamage, fractional), Farsight Emplacements (towerRange,
        // flat metres), Standing Orders (towerAttackSpeed, fractional). Sums refresh on the
        // existing 0.4s Rescan tick via HeroTalentModifiers.StatSum (the Σ-registry pattern
        // HeroHealth.TakeDamage consumes). Σ=0 → identity, byte-identical baseline. The spire
        // is ALWAYS player-owned (no EnemyOwned variant), so no allegiance gate is needed.
        private float EffectiveDamage => Damage * DeNelle.Core.State.ModifierService.Active.TowerDamageMult * _talentDamageMult;
        private float EffectiveRange  => (Range * DeNelle.Core.State.ModifierService.Active.TowerRangeMult + _talentRangeAdd) * ElevationRangeMult;
        private float EffectiveFireRate => FireRate * _talentFireRateMult;

        // WO-676 — cached talent sums (identity until the first Rescan; refreshed every 0.4s).
        private float _talentDamageMult   = 1f;
        private float _talentRangeAdd     = 0f;
        private float _talentFireRateMult = 1f;

        /// <summary>WO-676 — the active hero's class slug for the talent Σ-registry read
        /// (mirrors PlayerAttackController's `_abilities.HeroClass : "knight"` resolution).</summary>
        private static string ActiveHeroClass()
        {
            var hero = HeroHealth.Instance;
            var abilities = hero != null ? hero.GetComponent<HeroAbilities>() : null;
            return abilities != null ? abilities.HeroClass : "knight";
        }

        /// <summary>WO-676 — one Σ-registry read per BULWARK type at the spire's existing stat
        /// seam (called from the 0.4s Rescan tick). Zero unlocked nodes → identity (1/0/1).</summary>
        private void RefreshTalentSums()
        {
            string heroClass = ActiveHeroClass();
            float dmg  = Talents.HeroTalentModifiers.StatSum(heroClass, "towerDamage");
            float rng  = Talents.HeroTalentModifiers.StatSum(heroClass, "towerRange");
            float rate = Talents.HeroTalentModifiers.StatSum(heroClass, "towerAttackSpeed");
            _talentDamageMult   = 1f + Mathf.Max(0f, dmg);
            _talentRangeAdd     = Mathf.Max(0f, rng);
            _talentFireRateMult = 1f + Mathf.Max(0f, rate);

            if (dmg > 0f)  FlowTrace.Once("ArcaneTower", "talent-towerDamage",
                $"BULWARK towerDamage applied to the arcane spire: +{dmg:P0} (Keen Ballistics).");
            if (rng > 0f)  FlowTrace.Once("ArcaneTower", "talent-towerRange",
                $"BULWARK towerRange applied to the arcane spire: +{rng:0.#}m (Farsight Emplacements).");
            if (rate > 0f) FlowTrace.Once("ArcaneTower", "talent-towerAttackSpeed",
                $"BULWARK towerAttackSpeed applied to the arcane spire: +{rate:P0} fire rate (Standing Orders).");
        }

        private void Update()
        {
            // WO-672 Slice C: a broken spire is INOPERABLE until repaired — no scan,
            // no acquire, no blast. Repair() clears the flag; the loop resumes next frame.
            if (_broken) return;

            _scan -= Time.deltaTime;
            if (_scan <= 0f) { Rescan(); _scan = 0.4f; }

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;

            var target = Acquire();
            if (target == null) return;

            // WO-676: Standing Orders (towerAttackSpeed) folds into the fire cadence.
            _cd = 1f / Mathf.Max(0.1f, EffectiveFireRate);
            FireBlast(target);
        }

        private void Rescan()
        {
            // WO-676: refresh the BULWARK talent sums on the same 0.4s cadence as the
            // target scan (never per frame).
            RefreshTalentSums();

            // PERF (overworld 1fps fix): mirrors DefenseTower. The old scan was
            // FindObjectsByType<MonoBehaviour>, which enumerates EVERY MonoBehaviour in ALL
            // loaded scenes (the additive overworld = tens of thousands) every 0.4s tick,
            // per tower — hundreds of ms/frame independent of enemy count. Scan only the two
            // CONCRETE hostile IDamageable implementors instead (engine-filtered to the live
            // enemy bodies + the dragon). Identical target set; no full-scene enumeration.
            _hostiles.Clear();
            foreach (var d in FindObjectsByType<EnemyDamageable>())
            {
                if (d == null || d.Faction != CombatFaction.Hostile) continue;
                // TOWERS DEFEND THE TOWN AUTONOMOUSLY (owner 2026-06-28, mirrors DefenseTower):
                // roaming encounter reps (RepEngageWatcher) are no longer skipped — they are now
                // killable (Hp=150) and tower damage does NOT trigger the arena
                // (RangedHitsEngage=false; only near-CONTACT with the HERO pops the battle). So
                // the arcane spire SHOULD blast + kill reps in range as automated town defense;
                // the arena still fires only when a rep engages the hero. No skip — every hostile
                // faction in range is acquired.
                _hostiles.Add(d);
            }
            foreach (var d in FindObjectsByType<DragonBoss>())
                if (d != null && d.Faction == CombatFaction.Hostile)
                    _hostiles.Add(d);
        }

        // Same backline-first priority as DefenseTower (healers > ranged/dps > rest > tanks).
        private IDamageable Acquire()
        {
            IDamageable best = null;
            int   bestPri = int.MaxValue;
            float bestSqr = float.MaxValue;
            float range = EffectiveRange;   // WO-430 — Arcane Tower range perk
            foreach (var d in _hostiles)
            {
                if (d == null || !d.IsAlive) continue;
                Vector3 p = d.WorldPosition;
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr > range * range) continue;
                if (p.y > AirThreshold && !CanHitAir) continue;
                int pri = Priority(d);
                if (pri < bestPri || (pri == bestPri && sqr < bestSqr))
                {
                    bestPri = pri; bestSqr = sqr; best = d;
                }
            }
            return best;
        }

        private static int Priority(IDamageable d)
        {
            var mb = d as MonoBehaviour;
            var brain = mb != null ? mb.GetComponent<EnemyBrain>() : null;
            if (brain == null) return 2;
            switch (brain.Role)
            {
                case EnemyRole.Healer:   return 0;
                case EnemyRole.Ranged:   return 1;
                case EnemyRole.DPS:      return 1;
                case EnemyRole.MiniBoss: return 2;
                case EnemyRole.Tank:     return 3;
                default:                 return 2;
            }
        }

        /// <summary>
        /// Detonates an arcane blast centred on <paramref name="primary"/>: the
        /// primary takes full <see cref="Damage"/>; every OTHER live Hostile within
        /// <see cref="AoeRadius"/> of the impact takes a splash fraction and all
        /// affected enemies are SLOWED for <see cref="SlowSeconds"/>.
        /// </summary>
        private void FireBlast(IDamageable primary)
        {
            // Hot loop (slow-firing, but still per-shot): Throttle entry so a live tower is
            // pinpointed in a capture without flooding the break-log.
            FlowTrace.Throttle("ArcaneTower", $"blast:{GetInstanceID()}", 1f,
                $"FireBlast (radius={AoeRadius}, dmg={EffectiveDamage:0.#}, slow={SlowSeconds}s).");
            Vector3 muzzle  = transform.position + Vector3.up * 2.5f;
            Vector3 impact  = primary.WorldPosition;

            // ── SPELL CAST (Casting_Fire_2 ember gather at spire top) ──────────
            ProjectileVFXCatalog.SpawnNamedOneShot(muzzle, BoltCastVfx);

            // WO-VFX-TOWERS: Hovl arcane cast burst at the muzzle, layered on top of the
            // legacy Casting_Fire_2 (null-safe no-op if the key/prefab is missing). Reads by
            // MOTION (violet gather-and-flash) so it's colorblind-legible; BlastColor is a hint.
            VFXManager.PlayKey("Arcane_Cast", muzzle, default, null, BlastColor);

            // Flying body: Projectile_Fire_3 (hero fireball bolt) via SpawnFlying(Flame).
            // URP heal runs at spawn (FixUrpShaders).
            // Fall back to emissive orb if the mirrored Resources prefab is missing.
            GameObject bolt = null;
            Guard.Try("ArcaneTower", "spawn spell bolt", () =>
            {
                bolt = new GameObject("ArcaneSpellBolt");
                bolt.transform.position = muzzle;

                var fx = ProjectileVFXCatalog.SpawnFlying(bolt.transform, BoltVisualElement);
                if (fx == null)
                {
                    FlowTrace.Warn("ArcaneTower",
                        $"FireBlast: SpawnFlying({BoltVisualElement}) unresolved — using fallback emissive orb.");
                    var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    orb.name = "ArcaneSpellOrb";
                    orb.transform.SetParent(bolt.transform, false);
                    orb.transform.localScale = Vector3.one * 0.55f;
                    var col = orb.GetComponent<Collider>(); if (col != null) Destroy(col);
                    var sh = Shader.Find("Universal Render Pipeline/Lit");
                    if (sh != null)
                    {
                        var m = new Material(sh);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", BlastColor);
                        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", BlastColor * 4f); }
                        var r = orb.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
                    }
                }

                // WO-VFX-TOWERS: a Hovl arcane bolt (loop key) FOLLOWS the mover transform, so the
                // travelling shot reads as an arcane projectile in motion (colorblind-legible by its
                // TRAIL/MOTION, not tint). Loop keys return a VFXHandle — Stop() it on arrival so the
                // trail doesn't linger after detonation. Null-safe if the key/prefab is missing.
                var boltFx = VFXManager.PlayKey("Arcane_Projectile", muzzle, default, null,
                                                BlastColor, 0f, 0f, bolt.transform);

                // Arcing lob; blast applies ON ARRIVAL (the un-pooled mover self-destroys, taking
                // its visual child with it). The AoE blast VFX (pooled Impact_ExplosionAether) +
                // damage/slow land in ApplyBlast, so the shot reads as a cast spell. Stop the
                // following bolt trail as the shot arrives, then ApplyBlast plays the Hovl impact.
                bolt.AddComponent<ProjectileMover>().Launch(impact + Vector3.up * 0.5f, 26f, 0.35f,
                    () => { boltFx?.Stop(); ApplyBlast(primary, impact); });
            });

            if (bolt == null)
            {
                // Bolt spawn threw — never let the spire go silently dead:
                // fall back to the legacy instant blast (Warn per INSTRUMENTATION_STANDARD).
                FlowTrace.Warn("ArcaneTower",
                    "FireBlast: spell-bolt spawn failed — applying blast instantly (visual-less fallback).");
                ApplyBlast(primary, impact);
            }
        }

        /// <summary>
        /// Detonate the blast at <paramref name="impact"/>: explosion VFX + full damage to
        /// the primary, splash + Slow to every other live Hostile in <see cref="AoeRadius"/>.
        /// Called on spell-orb ARRIVAL (or instantly by the fallback path above).
        /// </summary>
        private void ApplyBlast(IDamageable primary, Vector3 impact)
        {
            if (this == null) return;   // tower destroyed while the orb was in flight

            ProjectileVFXCatalog.SpawnImpact(impact, BoltVisualElement);
            ProjectileVFXCatalog.SpawnNamedOneShot(impact, BoltImpactExtraVfx);

            // WO-VFX-TOWERS: Hovl arcane detonation burst at the impact point, layered on top of
            // the legacy explosion (null-safe no-op on a missing key). The following-bolt trail is
            // stopped by the arrival closure in FireBlast before this runs.
            VFXManager.PlayKey("Arcane_Impact", impact, default, null, BlastColor);

            float aoeSq = AoeRadius * AoeRadius;
            float splash = Mathf.Clamp01(SplashDamageFraction);

            // The scan list was refreshed in Update; reuse it for the splash sweep so we don't
            // pay a second FindObjectsByType this frame. GUARD EACH victim independently: one
            // bad enemy (a destroyed body mid-iteration, a thrown TakeDamage/ApplyStatus) is
            // logged + skipped, never aborting the splash for the rest of the cluster (a silent
            // half-applied blast would read as "the AoE didn't hit everyone").
            int affected = 0;
            Guard.TryEach("ArcaneTower", "apply blast to enemy", _hostiles, d =>
            {
                if (d == null || !d.IsAlive) return;

                bool isPrimary = ReferenceEquals(d, primary);
                if (!isPrimary && (d.WorldPosition - impact).sqrMagnitude > aoeSq) return;

                float ed  = EffectiveDamage;   // WO-430 — Arcane Tower damage perk
                float dmg = isPrimary ? ed : ed * splash;
                d.TakeDamage(dmg, Element);

                if (SlowSeconds > 0f)
                    d.ApplyStatus(StatusEffect.Slow, SlowSeconds);
                affected++;
            });

            if (affected == 0)
            {
                // The primary resolved but nothing took the blast — the cluster vanished between
                // Acquire and detonation, or every victim was already dead. Self-report (not a
                // hard fail): the shot fired but landed on no one.
                // Fleet NRE 4/4 (break-log run0 t=60.8, ApplyBlast:249): `?.` does not see Unity's
                // fake-null — a DESTROYED primary passes and .name throws on the dead native
                // object. Explicit Unity-null check (project rule: no ?. on UnityEngine.Object).
                var pmb = primary as MonoBehaviour;
                string pname = pmb != null ? pmb.name : "<primary destroyed>";
                FlowTrace.Warn("ArcaneTower",
                    $"FireBlast: 0 enemies affected (primary='{pname}') — cluster gone/all dead at detonation.");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, Range);
            Gizmos.color = new Color(0.8f, 0.5f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, AoeRadius);
        }
    }
}
