// =============================================================================
// DefenseTower — the auto-firing defensive structure (defensive-catalog v0 test).
// -----------------------------------------------------------------------------
// Proves "placement = role": an Archer tower on the GROUND (CanHitAir = false,
// short range) can't touch a flying dragon; a Wizard tower on the WALL-WALK
// (CanHitAir = true, long range, elevated) can. Targeting is by ROLE PRIORITY —
// the owner's "scamper to the DPS and healers" — squishy backline first.
//
// Reuses: IDamageable (DeNelle.Core.Combat) for find+damage, EnemyBrain.Role for
// priority, ProjectileMover for the visual bolt. All data-tunable.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Who a tower fights. <see cref="PlayerOwned"/> is the default and the legacy
    /// behaviour — every player-built / arena defender tower shoots Hostile enemies
    /// exactly as before. <see cref="EnemyOwned"/> flips the allegiance: a garrison
    /// turret targets the PLAYER PARTY (hero + companions) instead, damaging them
    /// through the same <see cref="IDamageableStructure"/> seam the enemy
    /// contact/ranged attacks already use on the hero.
    /// </summary>
    public enum TowerAllegiance
    {
        /// <summary>Default — shoots <see cref="CombatFaction.Hostile"/> enemies (legacy).</summary>
        PlayerOwned = 0,
        /// <summary>Garrison turret — shoots the player party (hero + companions).</summary>
        EnemyOwned = 1,
    }

    public sealed class DefenseTower : MonoBehaviour
    {
        public float Range       = 14f;
        public float Damage      = 8f;
        public float FireRate    = 1.2f;   // shots per second
        public bool  CanHitAir   = false;  // ground archers: false · wall wizards: true
        public float AirThreshold = 3.5f;  // target above this Y counts as "flying"
        public Color BoltColor   = Color.white;
        public DamageElement Element = DamageElement.None;

        // Allegiance — PlayerOwned (default) preserves every existing tower's
        // behaviour byte-for-byte; EnemyOwned garrison turrets target the player
        // party. Set by the spawner (GarrisonController) for garrison towers.
        public TowerAllegiance Allegiance = TowerAllegiance.PlayerOwned;

        private float _cd;
        private float _scan;
        private readonly List<IDamageable> _hostiles = new List<IDamageable>();

        // EnemyOwned target list — the player party (hero HeroHealth + companions
        // StoryCompanion), both IDamageableStructure (the seam enemies already hit
        // the hero through). Only populated/used in EnemyOwned mode.
        private readonly List<IDamageableStructure> _partyTargets = new List<IDamageableStructure>();

        // ── Targeting indicator (cheap persistent LineRenderer aim-beam) ──────
        // A single, reused LineRenderer drawn from the muzzle to the locked
        // target while one is acquired. Created lazily, never per-shot — towers
        // fire often, so this stays allocation-free in the hot loop. Hidden
        // (disabled) whenever there is no valid target.
        private LineRenderer _aimBeam;
        private Material      _aimBeamMat;

        private void Update()
        {
            // EnemyOwned garrison turret — target the player party instead of
            // Hostile enemies. Fully separate path so PlayerOwned stays identical.
            if (Allegiance == TowerAllegiance.EnemyOwned)
            {
                UpdateEnemyOwned();
                return;
            }

            _scan -= Time.deltaTime;
            if (_scan <= 0f) { Rescan(); _scan = 0.4f; }   // refresh target list a few times/sec

            // Acquire once per frame so the aim-beam tracks the current target
            // even between shots (the indicator reads "this tower is locked on").
            var target = Acquire();
            UpdateAimBeam(target);

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;
            if (target == null) return;
            _cd = 1f / Mathf.Max(0.1f, FireRate);
            Fire(target);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EnemyOwned (garrison turret) — mirror of the PlayerOwned tick but the
        // target set is the PLAYER PARTY (hero + companions) and damage routes
        // through IDamageableStructure.ApplyContactDamage — the SAME seam the
        // enemy melee/ranged attacks use on the hero (Enemy.RangedAttack →
        // structure.ApplyContactDamage). No new damage API.
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateEnemyOwned()
        {
            _scan -= Time.deltaTime;
            if (_scan <= 0f) { RescanParty(); _scan = 0.4f; }

            IDamageableStructure target = AcquireParty(out Vector3 targetPos);
            UpdateAimBeamAt(targetPos, target != null);

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;
            if (target == null) return;
            _cd = 1f / Mathf.Max(0.1f, FireRate);
            FireAtParty(target, targetPos);
        }

        /// <summary>Rebuilds the player-party target list (hero + companions).</summary>
        private void RescanParty()
        {
            _partyTargets.Clear();
            var hero = HeroHealth.Instance;
            if (hero != null) _partyTargets.Add(hero);
            foreach (var c in FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None))
                if (c != null) _partyTargets.Add(c);
        }

        /// <summary>
        /// Nearest in-range, alive party member. Reuses the same range + air gate
        /// as the player path; party members are ground units so the air gate is
        /// effectively a no-op for them (a downed hero is skipped via IsAlive).
        /// </summary>
        private IDamageableStructure AcquireParty(out Vector3 bestPos)
        {
            IDamageableStructure best = null;
            bestPos = default;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _partyTargets.Count; i++)
            {
                var d = _partyTargets[i];
                var mb = d as MonoBehaviour;
                if (mb == null || d == null || !d.IsAlive) continue;
                Vector3 p = mb.transform.position;
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr > Range * Range) continue;
                if (p.y > AirThreshold && !CanHitAir) continue;
                if (sqr < bestSqr) { bestSqr = sqr; best = d; bestPos = p; }
            }
            return best;
        }

        /// <summary>
        /// Fire on a party member: identical bolt visual + muzzle flash as the
        /// player path, but damage lands via ApplyContactDamage (the hero/companion
        /// IDamageableStructure seam).
        /// </summary>
        private void FireAtParty(IDamageableStructure target, Vector3 targetPos)
        {
            Vector3 muzzle = transform.position + Vector3.up * 2f;

            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "Bolt";
            bolt.transform.localScale = Vector3.one * 0.4f;
            var col = bolt.GetComponent<Collider>(); if (col != null) Destroy(col);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null)
            {
                var m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", BoltColor);
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", BoltColor * 2f); }
                var r = bolt.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
            }
            bolt.transform.position = muzzle;
            bolt.AddComponent<ProjectileMover>().Launch(targetPos + Vector3.up * 1f, 40f, CanHitAir ? 0.1f : 0.35f);

            VFXManager.Play(MuzzleVfxFor(Element), muzzle);

            target?.ApplyContactDamage(Damage);   // same seam the enemy attacks use on the hero
        }

        private void OnDisable()
        {
            // Don't leave a stale beam pointing at a dead target when the tower
            // is disabled (pooled, swapped, destroyed-soon).
            if (_aimBeam != null) _aimBeam.enabled = false;
        }

        private void Rescan()
        {
            // PERF (OuterWorld 1fps fix): the old scan was FindObjectsByType<MonoBehaviour>,
            // which enumerates EVERY MonoBehaviour in ALL loaded scenes (the additive
            // OuterWorld terrain/props/NPCs = tens of thousands) on every 0.4s tick, per
            // tower — hundreds of ms/frame regardless of enemy count. Scan only the two
            // CONCRETE hostile IDamageable implementors instead (engine-filtered, returns
            // just the live enemy bodies + the dragon). Identical target set, no full-scene
            // enumeration. Same Faction==Hostile gate preserved.
            _hostiles.Clear();
            foreach (var d in FindObjectsByType<EnemyDamageable>(FindObjectsSortMode.None))
                if (d != null && d.Faction == CombatFaction.Hostile)
                    _hostiles.Add(d);
            foreach (var d in FindObjectsByType<DragonBoss>(FindObjectsSortMode.None))
                if (d != null && d.Faction == CombatFaction.Hostile)
                    _hostiles.Add(d);
        }

        private IDamageable Acquire()
        {
            IDamageable best = null;
            int   bestPri = int.MaxValue;
            float bestSqr = float.MaxValue;
            foreach (var d in _hostiles)
            {
                if (d == null || !d.IsAlive) continue;
                Vector3 p = d.WorldPosition;
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr > Range * Range) continue;
                if (p.y > AirThreshold && !CanHitAir) continue;   // ground tower can't reach a flier
                int pri = Priority(d);
                if (pri < bestPri || (pri == bestPri && sqr < bestSqr))
                {
                    bestPri = pri; bestSqr = sqr; best = d;
                }
            }
            return best;
        }

        // "Scamper to the DPS and healers" — squishy backline first, tanks last.
        private static int Priority(IDamageable d)
        {
            var mb = d as MonoBehaviour;
            var brain = mb != null ? mb.GetComponent<EnemyBrain>() : null;
            if (brain == null) return 2;   // bosses / unknown — middling
            switch (brain.Role)
            {
                case EnemyRole.Healer:   return 0;   // kill the healer first
                case EnemyRole.Ranged:   return 1;
                case EnemyRole.DPS:      return 1;
                case EnemyRole.MiniBoss: return 2;
                case EnemyRole.Tank:     return 3;   // tanks last
                default:                 return 2;
            }
        }

        private void Fire(IDamageable target)
        {
            Vector3 muzzle = transform.position + Vector3.up * 2f;

            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "Bolt";
            bolt.transform.localScale = Vector3.one * 0.4f;
            var col = bolt.GetComponent<Collider>(); if (col != null) Destroy(col);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null)
            {
                var m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", BoltColor);
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", BoltColor * 2f); }
                var r = bolt.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
            }
            bolt.transform.position = muzzle;
            bolt.AddComponent<ProjectileMover>().Launch(target.WorldPosition + Vector3.up * 1f, 40f, CanHitAir ? 0.1f : 0.35f);

            // ── Muzzle flash ──────────────────────────────────────────────────
            // Brief pooled burst at the fire point each shot. Reuses the shared
            // VFXManager (object-pooled, quality-gated) — element-tinted so a
            // Flame tower reads orange, Ice pale-blue, Aether/None violet.
            // Null-safe via the static API: a no-op if VFXManager isn't booted.
            VFXManager.Play(MuzzleVfxFor(Element), muzzle);

            target.TakeDamage(Damage, Element);   // hitscan damage on fire (bolt is the feel)
        }

        /// <summary>Maps the tower's damage element to its tower-bolt muzzle VFXType.</summary>
        private static VFXType MuzzleVfxFor(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Flame: return VFXType.Projectile_TowerFire;
                case DamageElement.Ice:   return VFXType.Projectile_TowerIce;
                default:                  return VFXType.Projectile_TowerArcane;   // None / Aether
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Targeting indicator — a thin aim-beam from the muzzle to the locked
        // target. Cheap (one reused LineRenderer + one emissive material), and
        // readable (faint, element-tinted, slightly transparent so it doesn't
        // clutter the screen when many towers are firing).
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateAimBeam(IDamageable target)
        {
            if (target == null || !target.IsAlive)
            {
                if (_aimBeam != null) _aimBeam.enabled = false;
                return;
            }

            EnsureAimBeam();
            if (_aimBeam == null) return;   // (defensive — EnsureAimBeam always builds it)

            Vector3 muzzle = transform.position + Vector3.up * 2f;
            Vector3 hit    = target.WorldPosition + Vector3.up * 1f;
            _aimBeam.enabled = true;
            _aimBeam.SetPosition(0, muzzle);
            _aimBeam.SetPosition(1, hit);
        }

        /// <summary>
        /// Position-based aim-beam update for the EnemyOwned party-target path
        /// (the party uses IDamageableStructure, which has no WorldPosition).
        /// Mirrors <see cref="UpdateAimBeam"/> exactly but takes an explicit point.
        /// </summary>
        private void UpdateAimBeamAt(Vector3 targetWorldPos, bool hasTarget)
        {
            if (!hasTarget)
            {
                if (_aimBeam != null) _aimBeam.enabled = false;
                return;
            }

            EnsureAimBeam();
            if (_aimBeam == null) return;

            Vector3 muzzle = transform.position + Vector3.up * 2f;
            Vector3 hit    = targetWorldPos + Vector3.up * 1f;
            _aimBeam.enabled = true;
            _aimBeam.SetPosition(0, muzzle);
            _aimBeam.SetPosition(1, hit);
        }

        private void EnsureAimBeam()
        {
            if (_aimBeam != null) return;

            var beamGo = new GameObject("AimBeam");
            beamGo.transform.SetParent(transform, false);
            _aimBeam = beamGo.AddComponent<LineRenderer>();
            _aimBeam.useWorldSpace   = true;
            _aimBeam.positionCount   = 2;
            _aimBeam.numCapVertices  = 0;
            _aimBeam.alignment       = LineAlignment.View;
            _aimBeam.textureMode     = LineTextureMode.Stretch;
            _aimBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimBeam.receiveShadows  = false;

            // Thin, tapered, subtle — a hint of a lock-on laser, not a fat beam.
            _aimBeam.startWidth = 0.05f;
            _aimBeam.endWidth   = 0.02f;

            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _aimBeamMat = new Material(sh);
                Color tint = BoltColor; tint.a = 0.35f;   // faint, see-through
                if (_aimBeamMat.HasProperty("_BaseColor")) _aimBeamMat.SetColor("_BaseColor", tint);
                if (_aimBeamMat.HasProperty("_Color"))     _aimBeamMat.SetColor("_Color", tint);
                _aimBeam.sharedMaterial = _aimBeamMat;
            }

            Color c = BoltColor; c.a = 0.45f;
            _aimBeam.startColor = c;
            Color e = BoltColor; e.a = 0.15f;   // fades toward the target end
            _aimBeam.endColor = e;

            _aimBeam.enabled = false;   // off until a target is locked
        }

        private void OnDestroy()
        {
            if (_aimBeamMat != null) Destroy(_aimBeamMat);
        }
    }
}
