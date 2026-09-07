// =============================================================================
// AwarenessSensor — consolidated situational-awareness sensor (WO-147).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   One throttled OverlapSphere per enemy per cadence builds a PerceptionSnapshot
//   (hero / pet / nearest tower / nearest structure / Heart / living allies + HP)
//   and derives facts (AllyCount, MostDamagedAlly, IsOutnumbered, IsAllyDying,
//   IsHeroFocusingMe, ThreatCount). From those facts it computes an escalating
//   AwarenessState (Unaware → Alerted → Engaged) with decay.
//
//   It is the single source of perception data for the enemy AI:
//     • EnemyBrain drives the scan cadence (Scan()) via its dormant _targetEvalTimer
//       and pushes AwarenessState → the Animator "IsAlert" param.
//     • WO-145's scorer can read the candidate facts (same scan, no second scan).
//     • WO-146's family leader aggregates per-member sensors and pushes a shared
//       alert via RaiseSharedAlert (one sees → all react). There is exactly ONE
//       perception system — no separate GroupPerception component.
//
// PERF: a single 32-collider buffer, no per-frame allocation; the allies list is
//   cleared + refilled, never re-newed. Between scans Update() only ticks the
//   decay timer. Distant Unaware enemies scan less often (state/distance LOD).
//
// BACKWARD-SAFE: auto-added by EnemyBrain in Awake if absent; an enemy with no
//   tags/pet/family behaves exactly as before. RequireComponent(Enemy).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Consolidated per-enemy perception sensor. Attach alongside <see cref="Enemy"/>
    /// (auto-added by <see cref="EnemyBrain"/>). Performs one throttled overlap scan
    /// per cadence and exposes a cached <see cref="PerceptionSnapshot"/> plus an
    /// escalating <see cref="AwarenessState"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class AwarenessSensor : MonoBehaviour
    {
        /// <summary>The cached result of one scan — pure data, reused between scans.</summary>
        public sealed class PerceptionSnapshot
        {
            public Transform Hero;
            public bool      HeroInRange;
            public Transform Pet;
            public bool      PetInRange;
            public Transform Heart;
            public Transform NearestTower;
            public Transform NearestStructure;

            public readonly List<Enemy> Allies = new List<Enemy>(16);
            public Enemy MostDamagedAlly;
            public int   AllyCount;

            // Derived facts.
            public bool  IsOutnumbered;
            public bool  IsAllyDying;
            public bool  IsHeroFocusingMe;
            public int   ThreatCount;

            public void Clear()
            {
                Hero = Pet = Heart = NearestTower = NearestStructure = null;
                HeroInRange = PetInRange = false;
                Allies.Clear();
                MostDamagedAlly = null;
                AllyCount = 0;
                IsOutnumbered = IsAllyDying = IsHeroFocusingMe = false;
                ThreatCount = 0;
            }
        }

        [Header("Perception")]
        [Tooltip("Radius of the single consolidated overlap scan. Defaults to the " +
                 "widest radius any consumer needs (threat / tower / heal scans).")]
        [SerializeField, Min(1f)] private float _perceptionRadius = 20f;

        [Tooltip("Ally HP fraction at/below which IsAllyDying is true (escalates to Alerted).")]
        [SerializeField, Range(0.05f, 0.6f)] private float _allyDyingThreshold = 0.25f;

        [Tooltip("Seconds with no escalation trigger before awareness steps down one level.")]
        [SerializeField, Min(0.5f)] private float _alertDecaySeconds = 4f;

        [Tooltip("dot(hero.forward, toThisEnemy) above this = the hero is roughly facing/focusing this enemy.")]
        [SerializeField, Range(0f, 1f)] private float _heroFocusDot = 0.6f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private Enemy _enemy;
        private readonly Collider[] _scanBuffer = new Collider[32];
        private readonly PerceptionSnapshot _snapshot = new PerceptionSnapshot();

        private Transform _heartTransform;
        private Transform _heroTransform;
        private Transform _petTransform;

        private AwarenessState _state = AwarenessState.Unaware;
        private float _decayTimer;
        private float _lastHpFraction = 1f;   // HP-delta poll (WO-147 §3.5 option a — zero Enemy edit).
        private AwarenessState _sharedFloor = AwarenessState.Unaware;

        // ── Public read surface ───────────────────────────────────────────────

        /// <summary>The most recent perception snapshot (last <see cref="Scan"/>).</summary>
        public PerceptionSnapshot Snapshot => _snapshot;

        /// <summary>This enemy's own escalating awareness level.</summary>
        public AwarenessState State => _state;

        /// <summary>
        /// Awareness including any shared family floor (max of own state and the
        /// floor raised by <see cref="RaiseSharedAlert"/>). For a solo enemy this
        /// equals <see cref="State"/> (no-op overlay).
        /// </summary>
        public AwarenessState SharedState =>
            (AwarenessState)Mathf.Max((int)_state, (int)_sharedFloor);

        /// <summary>True when the sensor currently perceives any hostile (hero / pet / live tower).</summary>
        public bool HasThreat => _snapshot.ThreatCount > 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            ResolveSceneRefs();
        }

        private void OnEnable()
        {
            // Reset on (re)enable so a recycled/reloaded enemy starts clean.
            _state = AwarenessState.Unaware;
            _sharedFloor = AwarenessState.Unaware;
            _decayTimer = _alertDecaySeconds;
            _lastHpFraction = _enemy != null ? _enemy.HpFraction : 1f;
        }

        private void ResolveSceneRefs()
        {
            var hc = FindAnyObjectByType<HeartController>();
            _heartTransform = hc != null ? hc.transform : null;

            // WO-450: resolve the hero by component (HeroLocomotion — every hero variant
            // carries it), not the (undeclared) "HeroTarget" tag; the hero now carries the
            // built-in "Player" tag as a fallback.
            var loco = FindAnyObjectByType<HeroLocomotion>();
            _heroTransform = loco != null ? loco.transform : TryFindByTag("Player");

            _petTransform = TryFindByTag("PetTarget");
        }

        private static Transform TryFindByTag(string tag)
        {
            try
            {
                var go = GameObject.FindWithTag(tag);
                return go != null ? go.transform : null;
            }
            catch (UnityEngine.UnityException)
            {
                return null;   // tag undefined in this project — no candidate.
            }
        }

        // ── Scan (driven by EnemyBrain's throttle) ────────────────────────────

        /// <summary>
        /// WO-147: performs ONE consolidated overlap scan, rebuilds the snapshot,
        /// derives the facts, and steps the <see cref="AwarenessState"/>. Called by
        /// <see cref="EnemyBrain"/> on its (LOD-scaled) eval cadence — NOT per frame.
        /// </summary>
        public void Scan()
        {
            if (_enemy == null || _enemy.IsDead) return;

            // Re-resolve scene refs lazily if they were lost (scene reload safety).
            if (_heartTransform == null && _heroTransform == null) ResolveSceneRefs();

            _snapshot.Clear();
            _snapshot.Heart = _heartTransform;

            Vector3 pos = transform.position;
            float r2 = _perceptionRadius * _perceptionRadius;

            // Cached known transforms: hero, pet.
            if (_heroTransform != null)
            {
                _snapshot.Hero = _heroTransform;
                _snapshot.HeroInRange =
                    (_heroTransform.position - pos).sqrMagnitude <= r2;
            }
            if (_petTransform != null)
            {
                _snapshot.Pet = _petTransform;
                _snapshot.PetInRange =
                    (_petTransform.position - pos).sqrMagnitude <= r2;
            }

            // One overlap scan for allies / towers / structures.
            int count = Physics.OverlapSphereNonAlloc(pos, _perceptionRadius, _scanBuffer);
            float nearestTowerSqr = float.MaxValue;
            float nearestStructSqr = float.MaxValue;
            float worstFrac = float.MaxValue;
            int liveTowers = 0;

            for (int i = 0; i < count; i++)
            {
                Collider col = _scanBuffer[i];
                if (col == null) continue;

                var ally = col.GetComponentInParent<Enemy>();
                if (ally != null && ally != _enemy && !ally.IsDead)
                {
                    _snapshot.Allies.Add(ally);
                    float frac = ally.HpFraction;
                    if (frac < worstFrac) { worstFrac = frac; _snapshot.MostDamagedAlly = ally; }
                    continue;
                }

                var tower = col.GetComponentInParent<Tower>();
                if (tower != null && tower.IsAlive)
                {
                    liveTowers++;
                    float sqr = (tower.transform.position - pos).sqrMagnitude;
                    if (sqr < nearestTowerSqr) { nearestTowerSqr = sqr; _snapshot.NearestTower = tower.transform; }
                    continue;
                }

                // WO-1438 (adjacent to WO-1439): this filtered on null + IsAlive ONLY, with no
                // faction test — the same missing CONCEPT that had the raid garrison destroying
                // the RaidSpire it exists to guard (10,687 captured
                // `ProbeForStructure hit 'RaidSpire'` lines). Here it meant a raid guard's
                // NearestStructure could be its OWN spire or wall, so the guard read as
                // permanently "committed" beside friendly masonry and never went to meet the
                // player's warband. One predicate, called — never re-implemented at the site.
                var structure = col.GetComponentInParent<IDamageableStructure>();
                if (_enemy != null && CombatFactionRules.MayAttack(_enemy.SelfFaction, structure))
                {
                    float sqr = (col.transform.position - pos).sqrMagnitude;
                    if (sqr < nearestStructSqr) { nearestStructSqr = sqr; _snapshot.NearestStructure = col.transform; }
                }
            }

            // Derived facts.
            _snapshot.AllyCount = _snapshot.Allies.Count;
            _snapshot.IsAllyDying =
                _snapshot.MostDamagedAlly != null && worstFrac <= _allyDyingThreshold;

            int threats = (_snapshot.HeroInRange ? 1 : 0)
                        + (_snapshot.PetInRange ? 1 : 0)
                        + liveTowers;
            _snapshot.ThreatCount = threats;
            _snapshot.IsOutnumbered = threats > 0 && _snapshot.AllyCount < threats;
            _snapshot.IsHeroFocusingMe = ComputeHeroFocusingMe(pos);

            UpdateAwareness();
        }

        private bool ComputeHeroFocusingMe(Vector3 pos)
        {
            if (!_snapshot.HeroInRange || _heroTransform == null) return false;
            Vector3 toMe = pos - _heroTransform.position; toMe.y = 0f;
            if (toMe.sqrMagnitude < 0.01f) return true;
            Vector3 fwd = _heroTransform.forward; fwd.y = 0f;
            return Vector3.Dot(fwd.normalized, toMe.normalized) >= _heroFocusDot;
        }

        // ── Awareness escalation + decay ──────────────────────────────────────

        private void UpdateAwareness()
        {
            bool tookDamage = false;
            if (_enemy != null)
            {
                float frac = _enemy.HpFraction;
                tookDamage = frac < _lastHpFraction - 0.0001f;
                _lastHpFraction = frac;
            }

            // Committed: a perceivable offensive target is in range (hero / tower / structure).
            bool committed = _snapshot.HeroInRange || _snapshot.NearestTower != null
                          || _snapshot.NearestStructure != null;

            // Alerted: any perceived threat / dying ally / damage / shared alert.
            bool alertTrigger = _snapshot.HeroInRange || _snapshot.PetInRange
                             || _snapshot.IsAllyDying || tookDamage
                             || _sharedFloor >= AwarenessState.Alerted;

            AwarenessState target;
            if ((committed && (_snapshot.HeroInRange || _snapshot.IsHeroFocusingMe)) || tookDamage)
                target = AwarenessState.Engaged;
            else if (alertTrigger)
                target = AwarenessState.Alerted;
            else
                target = AwarenessState.Unaware;

            if ((int)target >= (int)_state)
            {
                // Escalate immediately and refresh the decay window.
                _state = target;
                _decayTimer = _alertDecaySeconds;
            }
            else
            {
                // No fresh trigger at the current level — decay one step at a time.
                _decayTimer -= TimeSinceLastScan();
                if (_decayTimer <= 0f && _state > AwarenessState.Unaware)
                {
                    _state = (AwarenessState)((int)_state - 1);
                    _decayTimer = _alertDecaySeconds;
                }
            }

            // The shared floor is transient — it must be re-raised each leader tick.
            _sharedFloor = AwarenessState.Unaware;
        }

        // The brain scans on its eval interval; approximate elapsed for the decay.
        private float _lastScanTime;
        private float TimeSinceLastScan()
        {
            float now = Time.time;
            float dt = _lastScanTime > 0f ? Mathf.Max(0f, now - _lastScanTime) : 0f;
            _lastScanTime = now;
            return dt;
        }

        // ── Shared group alert (WO-146/147 §5) ────────────────────────────────

        /// <summary>
        /// WO-147: a family leader pushes its aggregate awareness down to members so
        /// a member that hasn't personally perceived the threat still reacts (one
        /// sees → all react). Raises this sensor's floor for the next scan; null-safe.
        /// </summary>
        public void RaiseSharedAlert(AwarenessState level)
        {
            if ((int)level > (int)_sharedFloor) _sharedFloor = level;
            if ((int)level > (int)_state)
            {
                _state = level;
                _decayTimer = _alertDecaySeconds;
            }
        }
    }
}
