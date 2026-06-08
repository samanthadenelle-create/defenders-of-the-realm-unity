// =============================================================================
// EnemyOutpost - a real, walk-to ENEMY OUTPOST in the open world (RAID bite of the
// outpost -> raid -> loot elephant). Clear it by killing the garrison.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE LOOP (this slice): walk to the outpost -> the hero + party AUTO-FIGHT the
// guards (real Enemy via TargetManager - ZERO new combat code) -> kill the whole
// garrison (1 boss + ~5-6 guards) -> the outpost is CLEARED, fires OnCleared, and
// pays a FLAT reward (XP + a little crystals). Loot DROPS are the NEXT bite.
//
// REUSE (no reinvented wheels - mirrors the proven CampGuards pattern):
//   * OutpostFoundationGenerator  - the WOOD catalog-piece fortification visual
//     (GenerateFootprintRecipe + Realize), the SAME StructureFactory.Create path
//     the village build mode uses. LOCAL cell math; no village-grid involvement.
//   * EnemyFactory.Build()        - the ONE enemy creation path (CLAUDE.md §9).
//   * Enemy / EnemyBrain          - the boss gets an EnemyBrain in the MiniBoss
//     role (tougher stat block); guards are plain charger/walker Enemy.
//   * Enemy.SetBrainTarget(anchor) - tethers each garrison member to the outpost so
//     they HOLD the outpost instead of marching the Heart of Elarion. The hero's
//     own aggro + TargetManager still pull them into the fight when she arrives.
//   * Enemy.Died                  - subscribe-only kill counting; AllDead -> Clear.
//   * ZoneManager                 - region/threat scaling (deeper = deadlier).
//
// PERSISTENCE: PlayerPrefs only (mirrors ClaimableCamp) - a cleared raid stays
// cleared on reload; the save SCHEMA is untouched (save-owner follow-up).
//
// ISOLATION: created/owned entirely by RaidOutpostSystem at runtime. Touches NO
// existing file. References only PUBLIC read-only APIs. Code-built; LogWarning,
// never error, if optional art/registry pieces are missing (pack-missing-safe).
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.World;
using DeNelle.Core.State;

namespace DeNelle.Village.World.Camps
{
    /// <summary>A walk-to enemy outpost: a WOOD fort held by a boss-led garrison.
    /// Clear the whole garrison (hero + party auto-fight) to CLEAR it and collect a
    /// flat reward. Raises <see cref="OnCleared"/> once the last defender dies.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyOutpost : MonoBehaviour
    {
        // -- Garrison sizing --------------------------------------------------
        /// <summary>Base guard count (excludes the boss). Scaled up a touch by threat.</summary>
        public const int BaseGuardCount = 5;

        /// <summary>Radius the garrison stand-ring is laid out across, around the centre.</summary>
        public const float GarrisonRing = 6f;

        // -- Fortification footprint (LOCAL grid cells; cellSize = 3 m) --------
        private const int FortGridWidth = 6;
        private const int FortGridDepth = 6;

        // -- Flat clear reward (this slice; loot DROPS are the next bite) ------
        /// <summary>Aether Crystals banked on clear (before the threat-tier bonus).</summary>
        public int BaseClearCrystals { get; private set; } = 40;
        /// <summary>Hero XP granted on clear (before the threat-tier bonus).</summary>
        public int BaseClearXp { get; private set; } = 120;

        // -- Persistence (PlayerPrefs only - schema untouched; mirror ClaimableCamp) --
        private const string PrefClearedKey = "dotr-raid-cleared-";   // +OutpostId -> "1"

        /// <summary>Raised once the entire garrison is dead (the outpost is cleared).</summary>
        public event Action<EnemyOutpost> OnCleared;

        // -- Config -----------------------------------------------------------
        public RegionId Region { get; private set; } = RegionId.Goldfields;
        public int ThreatLevel { get; private set; }
        /// <summary>Stable id (region-based) used as the PlayerPrefs persistence key.</summary>
        public string OutpostId { get; private set; }

        // -- Runtime state ----------------------------------------------------
        /// <summary>True once the whole garrison is dead (or it was restored cleared).</summary>
        public bool Cleared { get; private set; }
        /// <summary>Living garrison members remaining (0 once cleared).</summary>
        public int AliveCount => _aliveCount;
        /// <summary>Total garrison members this outpost started with (boss + guards).</summary>
        public int TotalGarrison => _garrison.Count;

        private Transform _garrisonRoot;
        private readonly List<Enemy> _garrison = new List<Enemy>();
        private int _aliveCount;
        private bool _spawned;
        private bool _rewardPaid;

        /// <summary>Called by RaidOutpostSystem immediately after AddComponent.</summary>
        public void Configure(RegionId region, int threat)
        {
            Region = region;
            ThreatLevel = Mathf.Max(0, threat);
            OutpostId = "raid_" + region;
        }

        private void Start()
        {
            // Restore a previously-cleared raid: stay peaceful, no garrison, no fort
            // re-raise (the fight is over). A fresh raid spawns the fort + garrison.
            if (PlayerPrefs.GetString(PrefClearedKey + OutpostId, null) == "1")
            {
                Cleared = true;
                Debug.Log($"[EnemyOutpost] {OutpostId} restored as already CLEARED - peaceful.");
                return;
            }

            BuildFortification();
            SpawnGarrison();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _garrison.Count; i++)
                if (_garrison[i] != null) _garrison[i].Died -= HandleGarrisonDied;
        }

        // =====================================================================
        // FORTIFICATION - the WOOD visual (full reuse of OutpostFoundationGenerator).
        // =====================================================================

        private void BuildFortification()
        {
            // A small ~6x6 wood ring (perimeter walls + corner towers + one gate),
            // generated + realized through the SAME StructureFactory.Create path the
            // village build mode uses. LOCAL cell math against this root only - never
            // the village-scoped PlacementGrid / GameState.BaseLayout singletons.
            var recipe = OutpostFoundationGenerator.GenerateFootprintRecipe(
                FortGridWidth, FortGridDepth, OutpostTier.Wood);
            OutpostFoundationGenerator.Realize(recipe, transform, ~0);
        }

        // =====================================================================
        // GARRISON - boss + guards via EnemyFactory, tethered to the outpost.
        // Combat itself is FULL REUSE: these are real Enemy; the hero + party
        // auto-fight them via TargetManager. We write NO combat/targeting code.
        // =====================================================================

        private void SpawnGarrison()
        {
            if (_spawned) return;
            _spawned = true;

            _garrisonRoot = new GameObject("[Garrison]").transform;
            _garrisonRoot.SetParent(transform, false);
            _garrisonRoot.localPosition = Vector3.zero;

            // The BOSS holds the centre of the outpost (MiniBoss role).
            SpawnBoss();

            // The guard ring (charger/walker Enemy), threat-scaled count 5..8.
            int guards = BaseGuardCount + Mathf.Clamp(ThreatLevel / 2, 0, 3);
            for (int i = 0; i < guards; i++)
                SpawnGuard(i, guards);

            if (_aliveCount == 0)
            {
                // Nothing could spawn (no NavMesh / no roster) - treat as cleared so
                // the raid loop never deadlocks waiting on defenders that never existed.
                Debug.LogWarning($"[EnemyOutpost] no garrison spawned for {Region} outpost - auto-clearing.");
                Clear();
            }
        }

        private void SpawnBoss()
        {
            Vector3 pos = SnapToNav(transform.position);
            var def = BuildBossDef(ThreatLevel);

            var boss = EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot);
            if (boss == null) return;
            boss.gameObject.name = $"OutpostBoss ({Region})";

            var anchor = MakeAnchor("BossAnchor", pos);
            boss.Configure($"raidboss-{Region}", def, anchor);
            boss.SetBrainTarget(anchor);

            // The boss gets a brain in the MiniBoss role (tougher, holds the outpost).
            var brain = boss.gameObject.GetComponent<EnemyBrain>();
            if (brain == null) brain = boss.gameObject.AddComponent<EnemyBrain>();
            brain.Role = EnemyRole.MiniBoss;

            Track(boss);
        }

        private void SpawnGuard(int index, int count)
        {
            // A ring of stand positions around the outpost centre, NavMesh-snapped.
            float ang = (count > 0 ? (index / (float)count) : 0f) * Mathf.PI * 2f;
            Vector3 want = transform.position +
                           new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (GarrisonRing * 0.7f);
            Vector3 pos = SnapToNav(want);

            float depth = ZoneManager.Depth(transform.position);
            string enemyId = RegionSpawnTable.HasRoster(Region)
                ? RegionSpawnTable.PickEnemyId(Region, depth, UnityEngine.Random.value)
                : "orc-raider";
            if (string.IsNullOrEmpty(enemyId)) enemyId = "orc-raider";

            var def = BuildGuardDef(enemyId, ThreatLevel);

            var guard = EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot);
            if (guard == null) return;
            guard.gameObject.name = $"OutpostGuard ({enemyId} - {Region})";

            var anchor = MakeAnchor($"GuardAnchor-{index}", pos);
            guard.Configure($"raidguard-{Region}-{index}", def, anchor);
            guard.SetBrainTarget(anchor);

            Track(guard);
        }

        private void Track(Enemy e)
        {
            e.Died += HandleGarrisonDied;
            _garrison.Add(e);
            _aliveCount++;
        }

        private Transform MakeAnchor(string name, Vector3 pos)
        {
            // A local tether anchor so the defender HOLDS the outpost rather than
            // marching the Heart. Enemy.SetBrainTarget(anchor) overrides the Heart-
            // march; Enemy's own hero-aggro still pulls it into the fight on approach.
            var go = new GameObject(name);
            go.transform.SetParent(_garrisonRoot, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static Vector3 SnapToNav(Vector3 want)
        {
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, GarrisonRing + 6f, NavMesh.AllAreas))
                return hit.position;
            return want;
        }

        // =====================================================================
        // CLEAR - the last defender dies -> mark CLEARED, pay reward, persist.
        // =====================================================================

        private void HandleGarrisonDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleGarrisonDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0)
                Clear();
        }

        /// <summary>Mark the outpost cleared, pay the flat reward, and persist. Idempotent.</summary>
        public void Clear()
        {
            if (Cleared) return;
            Cleared = true;

            GrantClearReward();

            PlayerPrefs.SetString(PrefClearedKey + OutpostId, "1");
            PlayerPrefs.Save();

            Debug.Log($"[EnemyOutpost] {OutpostId} CLEARED - garrison wiped.");
            OnCleared?.Invoke(this);
        }

        // Flat clear reward (this slice): Aether Crystals banked into GameState (the
        // existing economy wallet path) + hero XP via HeroProgression. Both scale with
        // the outpost's threat tier so deadlier outposts pay better. Loot DROPS are the
        // NEXT bite. Idempotent - paid at most once per outpost.
        private void GrantClearReward()
        {
            if (_rewardPaid) return;
            _rewardPaid = true;

            int crystals = BaseClearCrystals + ThreatLevel * 10;
            int xp = BaseClearXp + ThreatLevel * 25;

            var state = GameStateService.Instance?.State;
            if (state != null)
            {
                state.AetherCrystals += crystals;
                GameStateService.Instance.ResourcesChanged.Invoke();
            }
            else
            {
                Debug.LogWarning("[EnemyOutpost] GameState null - clear crystals not banked.");
            }

            HeroProgression.Instance?.AddXp(xp);

            Debug.Log($"[EnemyOutpost] {OutpostId} clear reward: +{crystals} crystals, +{xp} XP.");
        }

        // =====================================================================
        // Stat blocks (code-built EnemyDef, threat-scaled). The boss is a tougher
        // tank/miniboss; the guards mirror CampGuards' synthesised roster so they
        // read the same as the rest of the open-world enemies.
        // =====================================================================

        private static EnemyDef BuildBossDef(int threat)
        {
            float scale = 1f + 0.12f * Mathf.Max(0, threat);
            var def = new EnemyDef
            {
                Id = "orc-warlord",
                Name = "Outpost Warlord",
                DisplayName = "Outpost Warlord",
                Ai = "charger",
                Hp = 420f * scale,
                MoveSpeed = 2.6f,
                ContactDamage = 22f * scale,
                AttackInterval = 1.4f,
                Height = 2.6f,
                AggroRadius = 16f,
                XpReward = 80 + threat * 5,
                GlimmerReward = 12,
            };
            return ApplyEarlyEase(def);
        }

        private static EnemyDef BuildGuardDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);

            string id = string.IsNullOrEmpty(enemyId) ? "orc-raider" : enemyId;
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    name = "Outpost Raider";  ai = "charger";    hp = 95f;  spd = 3.1f; dmg = 12f; interval = 1.3f; height = 2.0f; xp = 22; break;
                case "caveman":
                    name = "Outpost Brute";   ai = "walker";     hp = 70f;  spd = 2.7f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Outpost Hound";   ai = "skirmisher"; hp = 42f;  spd = 4.2f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Outpost Cultist"; ai = "skirmisher"; hp = 80f;  spd = 3.4f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Outpost Warden";  ai = "walker";     hp = 140f; spd = 2.2f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Outpost Guard";   ai = "walker";     hp = 60f;  spd = 3.0f; dmg = 8f;  interval = 1.3f; height = 1.8f; xp = 15; break;
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
                AggroRadius = 14f,
                XpReward = xp + threat,
                GlimmerReward = 3,
            };
            return ApplyEarlyEase(def);
        }

        // Early-game ease (same ramp as CampGuards / RegionMobSpawner): a brand-new
        // player meets soft defenders (x0.35 HP/damage) that ramp to full by ~BestWave 6,
        // so the FIRST raid is a winnable power-fantasy and scales with progression.
        private static EnemyDef ApplyEarlyEase(EnemyDef def)
        {
            float ease = Mathf.Lerp(0.35f, 1f,
                Mathf.Clamp01((GameStateService.Instance?.State?.BestWave ?? 0) / 6f));
            def.Hp = Mathf.Max(1f, def.Hp * ease);
            def.ContactDamage = Mathf.Max(0f, def.ContactDamage * ease);
            return def;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Cleared
                ? new Color(0.2f, 0.9f, 0.4f, 0.35f)
                : new Color(0.9f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, GarrisonRing);
        }
#endif
    }
}
