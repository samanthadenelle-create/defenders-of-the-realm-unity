// =============================================================================
// CampDefenseWave - the DEFEND stage of the Kill -> Claim -> Build -> Defend loop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// DEF-166 / WO-239: closes the core loop. The moment the player BUILDS an outpost
// on a freshly-claimed camp, the displaced enemy faction mounts a single
// counterattack to take it back. This spawns a small attacker pack OUTSIDE the
// camp ring that marches on the OUTPOST (the IDamageableStructure) and tries to
// raze it. Two outcomes:
//   * SURVIVE - every attacker dies while the outpost still stands -> the camp is
//     SECURED. A one-time defend reward banks into GameState + hero XP, and
//     OnDefended fires. Persisted so a secured camp never re-raids on reload.
//   * LOST    - the outpost is razed (its HP hits 0 and it is Destroyed) before the
//     attackers fall -> the wave is abandoned, the camp stays CLAIMED (rebuildable),
//     and the raid can be retriggered by building a fresh outpost.
//
// ISOLATION: created/owned entirely by ClaimableCamp at runtime. Mirrors the
// existing CampGuards spawn path EXACTLY (EnemyFactory.Build + Enemy.Configure +
// SetBrainTarget) - the ONE enemy path (CLAUDE.md S9). It only REFERENCES public,
// read-only APIs and edits NO existing file except the single BuildOutpost hook in
// ClaimableCamp. The attackers target the outpost transform (Enemy's contact-attack
// probe finds the outpost's IDamageableStructure and razes it the same way it razes
// any structure). A blocker collider is added on the OUTPOST so the probe can hit it.
// Code-built only. LogWarning, never error. ASCII-only. Canon: village is Elarion.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.World;
using DeNelle.Core.State;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Spawns + tracks the one-time counterattack that follows an outpost build.
    /// Raises <see cref="OnDefended"/> when the player survives it, or
    /// <see cref="OnLost"/> when the outpost is razed first.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampDefenseWave : MonoBehaviour
    {
        /// <summary>Base attackers in the counterattack (scaled up a touch by threat).</summary>
        public const int BaseAttackerCount = 4;

        /// <summary>Ring radius (outside the camp footprint) attackers spawn on.</summary>
        public const float SpawnRing = 14f;

        /// <summary>Aether Crystals banked when the camp is successfully defended.</summary>
        public const int BaseDefendCrystals = 25;

        /// <summary>Hero XP granted when the camp is successfully defended.</summary>
        public const int BaseDefendXp = 60;

        /// <summary>PlayerPrefs key (+CampId) marking a camp as permanently secured.</summary>
        private const string PrefSecuredKey = "dotr-camp-secured-";

        /// <summary>Raised once when the last attacker dies and the outpost still stands.</summary>
        public event Action<CampDefenseWave> OnDefended;

        /// <summary>Raised once when the outpost is razed before the attackers fall.</summary>
        public event Action<CampDefenseWave> OnLost;

        private RegionId _region;
        private int _threat;
        private Outpost _outpost;
        private string _campId;
        private Transform _root;

        private readonly List<Enemy> _attackers = new List<Enemy>();
        private int _aliveCount;
        private bool _spawned;
        private bool _resolved;

        /// <summary>Attackers still alive (0 once the wave resolves).</summary>
        public int AliveCount => _aliveCount;

        /// <summary>Total attackers this counterattack started with.</summary>
        public int TotalAttackers => _attackers.Count;

        /// <summary>True once the wave has resolved (defended or lost).</summary>
        public bool Resolved => _resolved;

        /// <summary>
        /// Begin the counterattack. Idempotent. If this camp was already SECURED in a
        /// prior session (PlayerPrefs), it resolves as defended immediately and spawns
        /// nothing - a secured camp stays peaceful on reload.
        /// </summary>
        public void Begin(RegionId region, int threat, Outpost outpost, string campId)
        {
            if (_spawned) return;
            _spawned = true;
            _region = region;
            _threat = Mathf.Max(0, threat);
            _outpost = outpost;
            _campId = campId;

            // Already secured this camp in a previous session -> no raid, stay calm.
            if (!string.IsNullOrEmpty(_campId) &&
                PlayerPrefs.GetString(PrefSecuredKey + _campId, null) == "1")
            {
                Debug.Log($"[CampDefenseWave] {_campId} already secured - no counterattack.");
                ResolveDefended(grantReward: false, persist: false);
                return;
            }

            if (_outpost == null || !_outpost.IsAlive)
            {
                Debug.LogWarning("[CampDefenseWave] no live outpost to defend - aborting raid.");
                _resolved = true;
                return;
            }

            // Ensure the outpost can actually be hit by the attackers' contact probe.
            // The Outpost strips colliders off its visual primitives, so add a single
            // blocker collider on the outpost object (which hosts IDamageableStructure).
            EnsureOutpostBlocker(_outpost.gameObject);

            _root = new GameObject("[CampDefenseWave]").transform;
            _root.SetParent(transform, false);
            _root.localPosition = Vector3.zero;

            int count = BaseAttackerCount + Mathf.Clamp(_threat / 2, 0, 3);  // 4..7 by tier
            for (int i = 0; i < count; i++)
                SpawnOneAttacker(i, count);

            if (_aliveCount == 0)
            {
                // Nothing could spawn (no NavMesh / no roster) - treat as defended so
                // the loop never deadlocks waiting on attackers that never existed.
                Debug.LogWarning($"[CampDefenseWave] no attackers spawned for {_region} - auto-securing.");
                ResolveDefended(grantReward: true, persist: true);
                return;
            }

            Debug.Log($"[CampDefenseWave] {_campId} under attack by {_aliveCount} raiders - defend the outpost!");
        }

        private void Update()
        {
            if (_resolved) return;

            // The outpost was razed (Destroyed) before the attackers fell -> defense lost.
            if (_outpost == null || !_outpost.IsAlive)
            {
                ResolveLost();
            }
        }

        private void SpawnOneAttacker(int index, int count)
        {
            Vector3 campCentre = transform.position;

            // A ring of spawn positions OUTSIDE the camp footprint, NavMesh-snapped, so
            // attackers visibly march IN toward the outpost rather than popping on top of it.
            float ang = (count > 0 ? (index / (float)count) : 0f) * Mathf.PI * 2f;
            Vector3 want = campCentre + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * SpawnRing;
            Vector3 pos = want;
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, SpawnRing + 6f, NavMesh.AllAreas))
                pos = hit.position;

            // Region-appropriate enemy id (same picker the guards/roamers use).
            float depth = ZoneManager.Depth(campCentre);
            string enemyId = RegionSpawnTable.HasRoster(_region)
                ? RegionSpawnTable.PickEnemyId(_region, depth, UnityEngine.Random.value)
                : "orc-raider";
            if (string.IsNullOrEmpty(enemyId)) enemyId = "orc-raider";

            var def = BuildAttackerDef(enemyId, _threat);

            var enemy = EnemyFactory.Build(def, pos, Quaternion.identity, _root);
            if (enemy == null) return;
            enemy.gameObject.name = $"CampRaider ({enemyId} - {_region})";

            // March the attacker on the OUTPOST: Configure's "heart" arg is the
            // nav/attack goal. Enemy's contact-attack probe then finds the outpost's
            // IDamageableStructure and razes it the same way it razes any structure.
            // Enemy's own DEF-224 hero-aggro still lets it peel to fight a near hero.
            Transform target = _outpost != null ? _outpost.transform : transform;
            enemy.Configure($"campraider-{_region}-{index}", def, target);
            enemy.SetBrainTarget(target);

            enemy.Died += HandleAttackerDied;
            _attackers.Add(enemy);
            _aliveCount++;
        }

        private void HandleAttackerDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleAttackerDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0 && !_resolved)
            {
                // Last attacker fell and the outpost still stands -> camp secured.
                if (_outpost != null && _outpost.IsAlive)
                    ResolveDefended(grantReward: true, persist: true);
                else
                    ResolveLost();
            }
        }

        // ---------------------------------------------------------------------
        // Resolution.
        // ---------------------------------------------------------------------

        private void ResolveDefended(bool grantReward, bool persist)
        {
            if (_resolved) return;
            _resolved = true;
            UnsubscribeAll();

            if (grantReward) GrantDefendReward();

            if (persist && !string.IsNullOrEmpty(_campId))
            {
                PlayerPrefs.SetString(PrefSecuredKey + _campId, "1");
                PlayerPrefs.Save();
            }

            Debug.Log($"[CampDefenseWave] {_campId} DEFENDED - the outpost holds. Camp secured.");
            OnDefended?.Invoke(this);
        }

        private void ResolveLost()
        {
            if (_resolved) return;
            _resolved = true;
            UnsubscribeAll();
            Debug.LogWarning($"[CampDefenseWave] {_campId} LOST - the outpost was razed. Rebuild to try again.");
            OnLost?.Invoke(this);
        }

        private void GrantDefendReward()
        {
            int crystals = BaseDefendCrystals + _threat * 8;
            int xp = BaseDefendXp + _threat * 15;

            var state = GameStateService.Instance?.State;
            if (state != null)
            {
                GameStateService.Instance?.AddCrystals(crystals);   // unified onto Resources.Crystals; persists + raises ResourcesChanged
            }
            else
            {
                Debug.LogWarning("[CampDefenseWave] GameState null - defend crystals not banked.");
            }

            HeroProgression.Instance?.AddXp(xp);
            Debug.Log($"[CampDefenseWave] {_campId} defend reward: +{crystals} crystals, +{xp} XP.");
        }

        private void UnsubscribeAll()
        {
            for (int i = 0; i < _attackers.Count; i++)
                if (_attackers[i] != null) _attackers[i].Died -= HandleAttackerDied;
        }

        private void OnDestroy() => UnsubscribeAll();

        // ---------------------------------------------------------------------
        // Outpost blocker collider - so the attackers' contact probe can hit it.
        // ---------------------------------------------------------------------
        private static void EnsureOutpostBlocker(GameObject outpostGo)
        {
            if (outpostGo == null) return;
            if (outpostGo.GetComponent<Collider>() != null) return;
            // A small upright capsule centred on the post - non-trigger so the
            // enemy SphereCast (QueryTriggerInteraction.Ignore) registers it.
            var cap = outpostGo.AddComponent<CapsuleCollider>();
            cap.direction = 1;             // Y-up
            cap.height = 3f;
            cap.radius = 0.9f;
            cap.center = new Vector3(0f, 1.5f, 0f);
            cap.isTrigger = false;
        }

        // Attacker stat block - mirrors CampGuards.BuildGuardDef so raiders read the
        // same as the guard/roamer roster, with a small +10% aggression bump (they
        // are on the offensive). Uses the same early-game ease ramp so a brand-new
        // player's first defend is winnable.
        private static EnemyDef BuildAttackerDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);

            string id = string.IsNullOrEmpty(enemyId) ? "orc-raider" : enemyId;
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    name = "Raider";        ai = "charger";    hp = 95f;  spd = 3.3f; dmg = 12f; interval = 1.3f; height = 2.0f; xp = 22; break;
                case "caveman":
                    name = "Brute";         ai = "walker";     hp = 70f;  spd = 2.9f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Hound";         ai = "skirmisher"; hp = 42f;  spd = 4.4f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Cultist";       ai = "skirmisher"; hp = 80f;  spd = 3.6f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Warden";        ai = "walker";     hp = 140f; spd = 2.4f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Raider";        ai = "walker";     hp = 60f;  spd = 3.2f; dmg = 8f;  interval = 1.3f; height = 1.8f; xp = 15; break;
            }

            var def = new EnemyDef
            {
                Id = id,
                Name = name,
                DisplayName = name,
                Ai = ai,
                Hp = hp * scale,
                MoveSpeed = spd,
                ContactDamage = dmg * scale,
                AttackInterval = interval,
                Height = height,
                AggroRadius = 16f,
                XpReward = xp + threat,
                GlimmerReward = 3,
            };

            float ease = Mathf.Lerp(0.35f, 1f,
                Mathf.Clamp01((GameStateService.Instance?.State?.BestWave ?? 0) / 6f));
            def.Hp = Mathf.Max(1f, def.Hp * ease);
            def.ContactDamage = Mathf.Max(0f, def.ContactDamage * ease);
            return def;
        }
    }
}
