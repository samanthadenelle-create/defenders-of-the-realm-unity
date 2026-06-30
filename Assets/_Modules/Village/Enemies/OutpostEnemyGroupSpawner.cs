// =============================================================================
// OutpostEnemyGroupSpawner — spawns a small, seeded SKELETON group at a choke in
// the Phase-2 outpost/dungeon chain. A runtime hook (or a placed marker carrying
// this component) calls SpawnGroup(center, seed) to populate the room with a
// weighted mix of Hollow skeletons that aggro the hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// REUSES (does NOT reinvent, CLAUDE.md §9):
//   - EnemyFactory.Build (the ONE skinned-enemy creation path) + Enemy.Configure
//   - EnemyBrain (Role + SetHeroOnlyTarget) for hero-aggro behaviour
//   - The hollow-* EnemyDef stat-block pattern from EnemyFamilyTestSpawner
//   - The hollow-* ids auto-resolve to KayKit Skeleton_* models in EnemyFactory.
//
// Each spawned skeleton is configured with heart=null (hero-aggro, not a siege
// wave) and SetHeroOnlyTarget(true). Seeded System.Random => repeatable layouts.
// ASCII strings only. Canon: the village is Elarion.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;   // FlowTrace (TGVRU, CLAUDE.md §12)

namespace DeNelle.Village
{
    /// <summary>Spawns a seeded weighted skeleton group around a choke point (hero-aggro).</summary>
    public sealed class OutpostEnemyGroupSpawner : MonoBehaviour
    {
        private const string Sys = "OutpostEnemies";

        [Tooltip("Ring radius (world units) the group is spread across around the center.")]
        [SerializeField] private float formationRadius = 3.5f;

        [Tooltip("When true, a placed marker spawns its group automatically on Start (seeded from scene + position).")]
        [SerializeField] private bool autoSpawnOnStart = true;
        [SerializeField] private int minCount = 3;
        [SerializeField] private int maxCount = 7;

        private Transform _root;
        private int _counter;
        private bool _autoSpawned;

        // Tiny runtime bootstrapper: a marker baked into the chain spawns its group once
        // on Start, seeded deterministically from the scene name + its world position so the
        // layout is repeatable. Disable autoSpawnOnStart to drive SpawnGroup from an external hook.
        private void Start()
        {
            if (!autoSpawnOnStart || _autoSpawned) return;
            _autoSpawned = true;
            SpawnGroup(transform.position, ComputeSeed(), minCount, maxCount);
        }

        private int ComputeSeed()
        {
            var scene = gameObject.scene;
            string key = (scene.IsValid() ? scene.name : "scene") + ":" +
                         Mathf.RoundToInt(transform.position.x) + "," + Mathf.RoundToInt(transform.position.z);
            return key.GetHashCode();
        }

        /// <summary>
        /// Spawn a formation ring of [<paramref name="min"/>..<paramref name="max"/>]
        /// weighted skeletons around <paramref name="center"/>, seeded by
        /// <paramref name="seed"/> (repeatable). Each enemy aggros the hero.
        /// </summary>
        public void SpawnGroup(Vector3 center, int seed, int min = 3, int max = 7)
        {
            if (min < 1) min = 1;
            if (max < min) max = min;

            var rng = new System.Random(seed);
            int count = rng.Next(min, max + 1);

            if (_root == null)
                _root = new GameObject("[OutpostSkeletonGroup]").transform;

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                // Spread evenly around a ring, jittered slightly so it does not read as a clock face.
                float ang = (i / (float)count) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.6 - 0.3);
                float rad = formationRadius * (0.7f + (float)rng.NextDouble() * 0.6f);
                Vector3 slot = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);

                // Snap each slot onto the baked NavMesh so the agent can path.
                if (NavMesh.SamplePosition(slot, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    slot = hit.position;

                string id = WeightedSkeletonId(rng);
                EnemyDef def = DefFor(id, _counter++);
                EnemyRole role = RoleFor(id);

                Vector3 toCenter = center - slot; toCenter.y = 0f;
                Quaternion rot = toCenter.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(toCenter)
                    : Quaternion.identity;

                var enemy = EnemyFactory.Build(def, slot, rot, _root);
                if (enemy == null) continue;
                enemy.gameObject.name = $"OutpostSkeleton ({def.Id})";
                enemy.Configure($"outpost-{def.Id}-{_counter}", def, null);   // heart=null -> hero-aggro

                var brain = enemy.gameObject.AddComponent<EnemyBrain>();
                brain.Role = role;
                brain.SetHeroOnlyTarget(true);

                spawned++;
            }

            FlowTrace.Step(Sys, $"spawned {spawned} skeletons @ {center} seed {seed} (rolled count {count})");
        }

        // ── Weighted family pick — mostly walkers, some rogues/warriors, rare acolyte ──
        private static string WeightedSkeletonId(System.Random rng)
        {
            // Weights: walker 5, rogue 2, warrior 2, acolyte 1  (total 10).
            int roll = rng.Next(0, 10);
            if (roll < 5) return "hollow-walker";
            if (roll < 7) return "hollow-rogue";
            if (roll < 9) return "hollow-warrior";
            return "hollow-acolyte";
        }

        private static EnemyRole RoleFor(string id)
        {
            switch (id)
            {
                case "hollow-warrior": return EnemyRole.Tank;
                case "hollow-acolyte": return EnemyRole.Healer;
                default:               return EnemyRole.DPS;   // walker / rogue
            }
        }

        // ── Per-family stat blocks (mirrors EnemyFamilyTestSpawner's code-built EnemyDef) ──
        private static EnemyDef DefFor(string id, int n)
        {
            switch (id)
            {
                case "hollow-rogue":
                    return new EnemyDef
                    {
                        Id = "hollow-rogue", Name = "Hollow Rogue", DisplayName = "Hollow Rogue", Ai = "skirmisher",
                        Hp = 34f, MoveSpeed = 3.6f, ContactDamage = 7f, AttackInterval = 1.0f, Height = 1.7f,
                        XpReward = 14, GlimmerReward = 3,
                    };
                case "hollow-warrior":
                    return new EnemyDef
                    {
                        Id = "hollow-warrior", Name = "Hollow Warrior", DisplayName = "Hollow Warrior", Ai = "walker",
                        Hp = 180f, MoveSpeed = 1.8f, ContactDamage = 13f, AttackInterval = 1.5f, Height = 2.4f,
                        AggroRadius = 12f, XpReward = 36, GlimmerReward = 7,
                    };
                case "hollow-acolyte":
                    return new EnemyDef
                    {
                        Id = "hollow-acolyte", Name = "Hollow Acolyte", DisplayName = "Hollow Acolyte", Ai = "walker",
                        Hp = 60f, MoveSpeed = 2.6f, ContactDamage = 4f, AttackInterval = 1.5f, Height = 1.8f,
                        XpReward = 28, GlimmerReward = 6,
                    };
                default: // hollow-walker
                    return new EnemyDef
                    {
                        Id = "hollow-walker", Name = "Hollow Walker", DisplayName = "Hollow Walker", Ai = "walker",
                        Hp = 40f, MoveSpeed = 3.0f, ContactDamage = 6f, AttackInterval = 1.2f, Height = 1.7f,
                        XpReward = 12, GlimmerReward = 2,
                    };
            }
        }
    }
}
