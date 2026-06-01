// =============================================================================
// RegionMobSpawner (WO-155) — roaming region mobs, region-appropriate + threat-scaled.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The THREAT half of the open-world explore loop (the reward half is MineNode /
// CrystalMineNode). As the player roams the outer world, this maintains a small
// live population of roaming mobs AROUND them, each:
//   • REGION-APPROPRIATE  — picked from RegionSpawnTable by ZoneManager.GetZone +
//     depth band (Wildlands living in Goldfields/Stoneback; Wound-tied in Mirewood/
//     Ashwood) — the owner's REGION_ENEMY_ROSTER.md assignment, as data.
//   • THREAT-SCALED       — stats + level set from ZoneManager.ThreatLevel (danger
//     tier × depth). Deeper in-region = tougher; Ashwood core is the deadliest.
//   • RED-SKULL TELLED     — a ThreatSkullPlate nameplate shows a Fallout-style skull
//     when the mob's ThreatLevel out-paces the player's level (risky / lethal).
//
// RECONCILIATION (no parallel spawn/enemy system — CLAUDE.md §9):
//   • Reuses Enemy + Enemy.Configure (the SAME path as TribeManager / WaveManager /
//     EnemyFamilyTestSpawner) — code-built capsules, NavMesh-seated, no prefab/SO.
//   • Reuses ZoneManager (region classifier) + RegionSpawnTable (roster) + ThreatLevel
//     (the single difficulty read) — does NOT re-implement any of them.
//   • Self-bootstrapping DDOL (mirrors TribeManager / EnemyFamilyTestSpawner) so it
//     needs NO scene edit and fires NO bake. Works the moment OuterWorld is loaded
//     additively over Village (WorldSceneLoader) and a baked NavMesh exists.
//
// ROAMING vs RAIDING: TribeManager (WO-160) anchors raider BANDS to a camp that
// raze SETTLEMENTS. This spawner is the ambient wilderness population that wanders
// near the PLAYER and aggros them — a different, complementary layer. They never
// march the Heart (the roam anchor overrides it) so they don't trickle into town.
//
// HOLLOW ONES are NOT spawned here (RegionSpawnTable omits them) — they stay the
// village wave faction per the roster doc, pending owner confirm on the haunted seam.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>
    /// Maintains a small live population of region-appropriate, threat-scaled roaming
    /// mobs around the player in the outer world. One self-bootstrapping DDOL singleton;
    /// no prefab / SO / scene wiring, no bake.
    /// </summary>
    public sealed class RegionMobSpawner : MonoBehaviour
    {
        public static RegionMobSpawner Instance { get; private set; }

        [Header("Population")]
        [Tooltip("How many roaming mobs to keep alive around the player when in an outer region.")]
        [Min(0)] public int TargetPopulation = 6;

        [Tooltip("Seconds between population/aggro maintenance passes (throttled — never per-frame heavy).")]
        [Min(0.1f)] public float TickInterval = 1.0f;

        [Header("Ranges (world units)")]
        [Tooltip("Inner radius of the spawn ring around the player (don't pop in their face).")]
        [Min(2f)] public float SpawnRingInner = 22f;
        [Tooltip("Outer radius of the spawn ring around the player.")]
        [Min(4f)] public float SpawnRingOuter = 38f;
        [Tooltip("Beyond this distance from the player a live mob is culled (out of sight, save budget).")]
        [Min(10f)] public float CullRadius = 70f;
        [Tooltip("A roaming mob aggros (paths to) the player within this radius; beyond it, it wanders its anchor.")]
        [Min(2f)] public float AggroRadius = 18f;
        [Tooltip("Radius a wandering mob picks new roam points within, around its spawn anchor.")]
        [Min(1f)] public float WanderRadius = 8f;

        [Header("Wander")]
        [Tooltip("Seconds between a wandering (non-aggro) mob re-picking a roam point.")]
        [Min(0.5f)] public float WanderRepathInterval = 4f;

        // Region tints so mobs read at a glance in the placeholder-capsule build.
        private static readonly Color LivingTint = new Color(0.38f, 0.55f, 0.28f);  // mossy green — Wildlands
        private static readonly Color WoundTint  = new Color(0.52f, 0.18f, 0.42f);  // sickly violet — Wound-tied

        private float _tickTimer;
        private Transform _player;
        private Transform _root;
        private int _counter;

        // One live roaming mob + its wander bookkeeping.
        private sealed class Mob
        {
            public Enemy Enemy;
            public Transform RoamAnchor;   // a child transform the mob wanders around / paths to when not aggroed
            public float NextWanderAt;
            public bool Aggroed;
        }

        private readonly List<Mob> _live = new List<Mob>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("RegionMobSpawner").AddComponent<RegionMobSpawner>();
        }

        private void Awake()
        {
            // Destroy(this), not the host — this may share a GameObject (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = TickInterval;

            ResolvePlayer();
            if (_player == null) return;

            // Only run while the player is actually in an outer region. In the safe
            // Village home zone we spawn nothing (and let any stragglers cull out).
            Vector3 pp = _player.position;
            bool playerOutside = RegionSpawnTable.HasRoster(ZoneManager.GetZone(pp));

            PruneAndDrive(pp);

            if (playerOutside)
                TopUpPopulation(pp);
        }

        // ── Maintenance: cull dead/far mobs, drive wander vs aggro ────────────────

        private void PruneAndDrive(Vector3 playerPos)
        {
            float cullSqr = CullRadius * CullRadius;
            float aggroSqr = AggroRadius * AggroRadius;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var mob = _live[i];
                if (mob == null || mob.Enemy == null || mob.Enemy.IsDead)
                {
                    Despawn(mob);
                    _live.RemoveAt(i);
                    continue;
                }

                float dSqr = (mob.Enemy.transform.position - playerPos).sqrMagnitude;
                if (dSqr > cullSqr)
                {
                    // Too far — recycle the budget (the population tops up near the player).
                    Despawn(mob);
                    _live.RemoveAt(i);
                    continue;
                }

                bool wantAggro = dSqr <= aggroSqr;
                if (wantAggro)
                {
                    // Chase the player: redirect the brain target to the player transform.
                    if (!mob.Aggroed)
                    {
                        mob.Aggroed = true;
                        mob.Enemy.SetBrainTarget(_player);
                    }
                }
                else
                {
                    // Wander: path to the roam anchor; re-pick it periodically so the mob drifts.
                    if (mob.Aggroed)
                    {
                        mob.Aggroed = false;
                        mob.Enemy.SetBrainTarget(mob.RoamAnchor);
                    }
                    if (Time.time >= mob.NextWanderAt)
                    {
                        RepickRoamPoint(mob);
                        mob.NextWanderAt = Time.time + WanderRepathInterval;
                    }
                }
            }
        }

        private void RepickRoamPoint(Mob mob)
        {
            if (mob.RoamAnchor == null) return;
            Vector2 r = Random.insideUnitCircle * WanderRadius;
            Vector3 want = mob.Enemy.transform.position + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, WanderRadius, NavMesh.AllAreas))
                mob.RoamAnchor.position = hit.position;
        }

        // ── Population top-up around the player ───────────────────────────────────

        private void TopUpPopulation(Vector3 playerPos)
        {
            int deficit = TargetPopulation - _live.Count;
            if (deficit <= 0) return;

            if (_root == null) _root = new GameObject("[RegionMobs]").transform;

            // Spawn at most a couple per tick so a fresh region fills in over a few
            // seconds rather than popping a whole pack in one frame.
            int spawnThisTick = Mathf.Min(deficit, 2);
            for (int n = 0; n < spawnThisTick; n++)
            {
                if (!TryFindSpawnPoint(playerPos, out Vector3 pos)) continue;

                RegionId region = ZoneManager.GetZone(pos);
                if (!RegionSpawnTable.HasRoster(region)) continue;   // not an outer region

                float depth = ZoneManager.Depth(pos);
                int threat = ZoneManager.ThreatLevel(pos);
                string enemyId = RegionSpawnTable.PickEnemyId(region, depth, Random.value);
                if (string.IsNullOrEmpty(enemyId)) continue;

                var mob = SpawnMob(enemyId, region, pos, threat);
                if (mob != null) _live.Add(mob);
            }
        }

        // Find a NavMesh-valid point in the spawn ring around the player.
        private bool TryFindSpawnPoint(Vector3 playerPos, out Vector3 pos)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Random.Range(SpawnRingInner, SpawnRingOuter);
                Vector3 want = playerPos + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                if (NavMesh.SamplePosition(want, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                {
                    pos = hit.position;
                    return true;
                }
            }
            pos = playerPos;
            return false;
        }

        // ── Spawn one roaming mob (reuse Enemy, code-built capsule — TribeManager precedent) ──

        private Mob SpawnMob(string enemyId, RegionId region, Vector3 pos, int threat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"RegionMob ({enemyId} · {region})";
            go.transform.SetParent(_root, false);
            go.transform.position = pos;

            // Size by the synthesised stat block's height (scale the capsule's ~2m default).
            var def = BuildRoamerDef(enemyId, threat);
            float scale = Mathf.Clamp(def.Height / 2f, 0.5f, 1.6f);
            go.transform.localScale = new Vector3(scale, scale, scale);

            // Trigger collider so the enemy's own contact probe can't self-hit (same as the other spawners).
            if (go.TryGetComponent(out Collider col)) col.isTrigger = true;

            // Region tint: Wildlands-living vs Wound-tied.
            var mr = go.GetComponent<Renderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var m = new Material(sh);
                    Color tint = IsWoundTied(region) ? WoundTint : LivingTint;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    else m.color = tint;
                    mr.sharedMaterial = m;
                }
            }

            // NavMeshAgent before Enemy ([RequireComponent]).
            go.AddComponent<NavMeshAgent>();
            var enemy = go.AddComponent<Enemy>();

            // A per-mob roam anchor (child of the root) it wanders around when not aggroed.
            // Using a Transform target keeps it OFF the Heart-march — these mobs never
            // trickle into the village. Configure passes a Heart fallback only as a last
            // resort if the anchor is ever lost; SetBrainTarget(anchor) overrides it.
            var anchorGo = new GameObject($"RoamAnchor-{enemyId}-{_counter}");
            anchorGo.transform.SetParent(_root, false);
            anchorGo.transform.position = pos;

            enemy.Configure($"region-{region}-{enemyId}-{_counter++}", def, ResolveHeart());
            enemy.SetBrainTarget(anchorGo.transform);   // roam, not Heart-march

            // Red-skull readiness tell — code-built nameplate (no UXML).
            ThreatSkullPlate.Attach(go, () => threat);

            return new Mob
            {
                Enemy = enemy,
                RoamAnchor = anchorGo.transform,
                NextWanderAt = Time.time + Random.Range(0f, WanderRepathInterval),
                Aggroed = false,
            };
        }

        private static void Despawn(Mob mob)
        {
            if (mob == null) return;
            if (mob.RoamAnchor != null) Destroy(mob.RoamAnchor.gameObject);
            if (mob.Enemy != null) Destroy(mob.Enemy.gameObject);
        }

        // ── Synthesised, threat-scaled stat blocks (TribeManager precedent) ───────
        // The Wildlands roster ids (orc-raider/caveman/feral-wolf) + tiefling-cultist
        // are NOT in enemies.json yet (forward design in docs/enemy-codex.md). We
        // synthesise a code-built EnemyDef per id here — this does NOT re-stat any
        // JSON-owned enemy; it gives the not-yet-statted roamers a sensible body that
        // ThreatLevel then scales. When enemies.json gains these ids, swap this for a
        // catalog lookup with no other change. necromancer EXISTS in JSON — but as a
        // 1700-HP village wave-boss; a roaming "lieutenant" reads it down to a tough
        // caster body so the open world isn't gated behind a raid boss.
        private static EnemyDef BuildRoamerDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);   // ThreatLevel-driven stat scaling

            // Per-roster archetype base. Codex roles: Wolf = fast skirmisher, Caveman =
            // brute walker, Orc Raider = heavy charger, Tiefling = demonic skirmisher,
            // Necromancer = caster lieutenant.
            string id = enemyId ?? "feral-wolf";
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    name = "Orc Raider";     ai = "charger";    hp = 95f;  spd = 3.1f; dmg = 12f; interval = 1.3f; height = 2.0f; xp = 22; break;
                case "caveman":
                    name = "Wildlands Caveman"; ai = "walker";  hp = 70f;  spd = 2.7f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Feral Wolf";     ai = "skirmisher"; hp = 42f;  spd = 4.2f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Tiefling Cultist"; ai = "skirmisher"; hp = 80f; spd = 3.4f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Wound Necromancer"; ai = "walker";  hp = 140f; spd = 2.2f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Wildlands Beast"; ai = "walker";    hp = 55f;  spd = 3.0f; dmg = 8f;  interval = 1.3f; height = 1.7f; xp = 14; break;
            }

            return new EnemyDef
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
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static bool IsWoundTied(RegionId region) =>
            region == RegionId.Mirewood || region == RegionId.Ashwood;

        private void ResolvePlayer()
        {
            if (_player != null) return;
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
        }

        private static Transform ResolveHeart()
        {
            var hc = FindAnyObjectByType<HeartController>();
            if (hc != null) return hc.transform;
            var byName = GameObject.Find("HeartOfTown") ?? GameObject.Find("Tree of Life") ?? GameObject.Find("Heart");
            return byName != null ? byName.transform : null;
        }
    }
}
