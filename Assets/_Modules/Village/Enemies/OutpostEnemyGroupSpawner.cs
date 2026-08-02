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

        [Tooltip("WO-770.11 dungeon leash: each skeleton stays dormant at its spawn slot " +
                 "until the hero comes within this radius (world units). Prevents the whole " +
                 "room beelining the entry. ~10m = room-sized. <= 0 disables the leash. " +
                 "WO-797: superseded by the room wake gate when a room area is configured.")]
        [SerializeField] private float leashRadius = 10f;

        // ── WO-797 room ownership (F8 seq 461/622 "all enemies at the entrance") ──
        // When areaSize is non-zero this spawner OWNS a room: spawn slots are seated
        // strictly inside the room AABB, and every spawned brain is bound to it
        // (EnemyBrain.SetRoomArea) so mobs wake off the ROOM FOOTPRINT and are
        // confined to the room + slack even while provoked. Fields are serialized so
        // DungeonBaker can write them into the SCENE via SerializedObject at bake;
        // DungeonRoomBinder configures them at runtime for already-baked scenes.
        [Header("Room ownership (WO-797)")]
        [Tooltip("Owning room's instance id (e.g. 'junction'). Diagnostic + contract only.")]
        [SerializeField] private string roomId = "";
        [Tooltip("World-space center of the owning room's AABB.")]
        [SerializeField] private Vector3 areaCenter;
        [Tooltip("World-space size of the owning room's AABB. Zero = room ownership OFF.")]
        [SerializeField] private Vector3 areaSize;
        [Tooltip("Metres a mob may step outside the room AABB (through a doorway) while fighting.")]
        [SerializeField] private float areaSlack = 2f;
        [Tooltip("Wake distance measured from the ROOM FOOTPRINT (not a ring slot) to the hero.")]
        [SerializeField] private float wakeRadius = 6f;

        private Transform _root;
        private int _counter;
        private bool _autoSpawned;
        // WO-797: brains this spawner created — lets a late ConfigureRoomArea retro-bind
        // enemies that already spawned (binder-after-Start ordering safety net).
        private readonly System.Collections.Generic.List<EnemyBrain> _spawnedBrains =
            new System.Collections.Generic.List<EnemyBrain>();

        /// <summary>True when this spawner owns a room AABB (WO-797).</summary>
        public bool HasRoomArea => areaSize.sqrMagnitude > 0.01f;

        /// <summary>The owning room's instance id ("" when unbound).</summary>
        public string RoomId => roomId;

        /// <summary>
        /// WO-797: bind this spawner to its room. Called by DungeonRoomBinder at scene load
        /// (before Start's auto-spawn) for already-baked composed scenes; the re-bake path
        /// writes the same serialized fields via SerializedObject instead. If enemies were
        /// already spawned (late call), they are retro-bound so no mob is ever ownerless.
        /// min/max &lt; 0 = keep the serialized counts (no creative re-authoring).
        /// </summary>
        public void ConfigureRoomArea(string room, Bounds area, float wake, float slack,
                                      int min = -1, int max = -1, float formation = -1f)
        {
            roomId = room ?? string.Empty;
            areaCenter = area.center;
            areaSize = area.size;
            wakeRadius = Mathf.Max(0f, wake);
            areaSlack = Mathf.Max(0f, slack);
            if (min > 0) minCount = min;
            if (max > 0) maxCount = Mathf.Max(min > 0 ? min : minCount, max);
            if (formation > 0f) formationRadius = formation;
            FlowTrace.Step(Sys, $"room area configured: room '{roomId}' center {areaCenter} " +
                $"size {areaSize} wake {wakeRadius:F1} slack {areaSlack:F1} (spawned so far {_spawnedBrains.Count})");

            // Retro-bind anything already spawned (defensive: the binder normally runs
            // before Start, so this list is empty on the happy path).
            for (int i = 0; i < _spawnedBrains.Count; i++)
            {
                var brain = _spawnedBrains[i];
                if (brain == null) continue;
                brain.SetRoomArea(roomId, area, areaSlack, wakeRadius);
                FlowTrace.Step(Sys, $"retro-assigned '{brain.gameObject.name}' -> room '{roomId}'");
            }
        }

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

            // WO-797: when this spawner owns a room, seat every slot STRICTLY INSIDE the
            // room AABB (negative slack shrinks by 0.5m) — the old unclamped ring let
            // junction slots land in the neighbouring corridor, inside one leash radius
            // of the entry hero seat (data-proven cause 1 of the entrance camp).
            bool hasArea = HasRoomArea;
            Bounds area = new Bounds(areaCenter, areaSize);

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                // Spread evenly around a ring, jittered slightly so it does not read as a clock face.
                float ang = (i / (float)count) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.6 - 0.3);
                float rad = formationRadius * (0.7f + (float)rng.NextDouble() * 0.6f);
                Vector3 slot = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                if (hasArea)
                    slot = EnemyBrain.ConfineToArea(slot, area, -0.5f);

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
                // WO-770.11 hotfix: tether each skeleton to its own spawn slot so a
                // distant room's group stays dormant until the hero approaches, instead
                // of beelining the global hero across the whole dungeon.
                brain.SetLeash(slot, leashRadius);
                // WO-797: bind the mob to its OWNING ROOM — wake measured from the room
                // footprint, every nav destination (incl. provoked chases) confined to
                // the room AABB + slack. Room-assignment is a captured data line per
                // enemy (CLAUDE.md sec.12).
                if (hasArea)
                {
                    brain.SetRoomArea(roomId, area, areaSlack, wakeRadius);
                    FlowTrace.Step(Sys, $"assigned 'outpost-{def.Id}-{_counter}' -> room '{roomId}' " +
                        $"anchor {slot} (wake {wakeRadius:F1}m from footprint, slack {areaSlack:F1}m)");
                }
                else
                {
                    FlowTrace.Warn(Sys, $"'outpost-{def.Id}-{_counter}' spawned with NO room ownership " +
                        $"(anchor-leash only, {leashRadius:F1}m) - WO-797 binder/bake did not configure this spawner");
                }
                _spawnedBrains.Add(brain);

                spawned++;
            }

            FlowTrace.Step(Sys, $"spawned {spawned} skeletons @ {center} seed {seed} (rolled count {count}) " +
                (hasArea ? $"room '{roomId}'" : "NO room area"));
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
                case "hollow-warrior": return EnemyRole.DPS;
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
                        Hp = 156f, MoveSpeed = 2.2f, ContactDamage = 10f, AttackInterval = 1.3f, Height = 1.88f,
                        AggroRadius = 10f, XpReward = 28, GlimmerReward = 5,
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
