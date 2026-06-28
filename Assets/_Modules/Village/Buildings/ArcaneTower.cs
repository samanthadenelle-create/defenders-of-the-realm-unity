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
    public sealed class ArcaneTower : MonoBehaviour
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

        private float _cd;
        private float _scan;
        private readonly List<IDamageable> _hostiles = new List<IDamageable>();

        // WO-430 — the Arcane Tower upgrade buffs ITS OWN damage/range (towerDamageMult /
        // towerRangeMult). Always player-owned, so the perk always applies. LIVE-READ.
        private float EffectiveDamage => Damage * DeNelle.Core.State.ModifierService.Active.TowerDamageMult;
        private float EffectiveRange  => Range  * DeNelle.Core.State.ModifierService.Active.TowerRangeMult;

        private void Update()
        {
            _scan -= Time.deltaTime;
            if (_scan <= 0f) { Rescan(); _scan = 0.4f; }

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;

            var target = Acquire();
            if (target == null) return;

            _cd = 1f / Mathf.Max(0.1f, FireRate);
            FireBlast(target);
        }

        private void Rescan()
        {
            // PERF (OuterWorld 1fps fix): mirrors DefenseTower. The old scan was
            // FindObjectsByType<MonoBehaviour>, which enumerates EVERY MonoBehaviour in ALL
            // loaded scenes (the additive OuterWorld = tens of thousands) every 0.4s tick,
            // per tower — hundreds of ms/frame independent of enemy count. Scan only the two
            // CONCRETE hostile IDamageable implementors instead (engine-filtered to the live
            // enemy bodies + the dragon). Identical target set; no full-scene enumeration.
            _hostiles.Clear();
            foreach (var d in FindObjectsByType<EnemyDamageable>(FindObjectsSortMode.None))
            {
                if (d == null || d.Faction != CombatFaction.Hostile) continue;
                // SKIP the un-killable OVERWORLD ENCOUNTER HOOKS (mirrors DefenseTower): these
                // reps carry a RepEngageWatcher marker and have Hp=9999 (roaming encounter
                // triggers, not town attackers). Without this guard the tower wastes every
                // blast on a 9999-HP hook that never dies -> reads as "0 damage", and the hit
                // re-triggers the encounter battle loop. Real enemies (normal HP, no marker)
                // are still acquired + damaged across ALL hostile factions in range.
                if (d.GetComponentInParent<RepEngageWatcher>() != null) continue;
                _hostiles.Add(d);
            }
            foreach (var d in FindObjectsByType<DragonBoss>(FindObjectsSortMode.None))
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

            // ── Cast + blast VFX (null-safe: no-op if VFXManager isn't booted) ──
            VFXManager.Play(VFXType.Cast_MageCharge, muzzle);
            VFXManager.Play(VFXType.Impact_ExplosionAether, impact);

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
                FlowTrace.Warn("ArcaneTower",
                    $"FireBlast: 0 enemies affected (primary='{(primary as MonoBehaviour)?.name ?? "<primary>"}') — cluster gone/all dead at detonation.");
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
