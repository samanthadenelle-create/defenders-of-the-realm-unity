// =============================================================================
// EnemyFamilyTestSpawner — dev tool: spawn distinct enemy FAMILIES on a hotkey
// so the role AI (EnemyBrain Tank / Healer / DPS) can be SEEN and tested.
// -----------------------------------------------------------------------------
// The role AI (EnemyBrain) + the group spawner (EnemyGroupSpawner / WaveEnemyGroup)
// landed in the GROUP 1/2 batch, but the group path needs authored WaveEnemyGroup
// SO assets + enemy prefabs + WaveManager inspector wiring — none of which exist,
// and the Village scene is curated (no re-save). So the role behaviours had no live
// content driving them.
//
// This self-bootstrapping DDOL dev tool closes that gap WITHOUT any scene edit,
// prefab, or SO: on the "J" key (in the Village scene) it builds a small pack of
// code-built enemies — 3 Grunts (DPS), 1 Tank, 1 Healer — each a tinted/sized
// capsule with NavMeshAgent + Enemy + EnemyBrain (mirrors WaveManager.BuildPlaceholder
// Enemy), configured from a per-family EnemyDef and given its EnemyRole. They march
// the real NavMesh toward the Heart, so the owner can watch:
//   • Grunts  — straight Heart-march (DPS role → Enemy's own nav).
//   • Tank    — charges the hero if tagged "Player" & in range, else nearest
//               structure (EnemyRole.Tank).
//   • Healer  — trails the pack and pulses Enemy.Heal() on the most-wounded ally
//               (EnemyRole.Healer) — visible as ally HP bars topping back up.
//
// Dev-only: triggers on a key press, harmless otherwise. Mirrors the project's
// other dev hooks (TowerLoopDevHarness 'N'). Promote to authored WaveEnemyGroup
// content once prefabs/SOs + WaveManager wiring exist.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Dev hotkey ('J') that spawns a Grunt/Tank/Healer pack to test EnemyBrain roles.</summary>
    public sealed class EnemyFamilyTestSpawner : MonoBehaviour
    {
        public static EnemyFamilyTestSpawner Instance { get; private set; }

        private const string  TargetScene = "Village";
        private const KeyCode SpawnKey    = KeyCode.J;

        private static readonly Color GruntTint  = new Color(0.58f, 0.58f, 0.64f); // grey
        private static readonly Color TankTint   = new Color(0.72f, 0.20f, 0.16f); // red
        private static readonly Color HealerTint = new Color(0.22f, 0.72f, 0.34f); // green

        private Transform _root;
        private int _counter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("EnemyFamilyTestSpawner").AddComponent<EnemyFamilyTestSpawner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) AnnounceHotkey();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) AnnounceHotkey();
        }

        private static void AnnounceHotkey()
        {
            Debug.Log("[EnemyFamilyTestSpawner] Press 'J' in the village to spawn a test pack " +
                      "(3 Grunts + 1 Tank + 1 Healer) and watch the EnemyBrain roles.");
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != TargetScene) return;
            if (Input.GetKeyDown(SpawnKey)) SpawnTestPack();
        }

        private void SpawnTestPack()
        {
            Transform heart = ResolveHeart();
            if (heart == null)
            {
                Debug.LogWarning("[EnemyFamilyTestSpawner] No Heart found — cannot spawn the test pack.");
                return;
            }

            if (_root == null) _root = new GameObject("[EnemyFamilyTestPack]").transform;

            // Origin: ~16 m south of the Heart, marching in. Clustered tight (< heal
            // scan radius) so the Healer can find its wounded allies.
            Vector3 origin = heart.position + new Vector3(0f, 0f, -16f);

            for (int i = 0; i < 3; i++)
                SpawnFamily(GruntDef(i), EnemyRole.DPS, GruntTint, 0.9f,
                            origin + new Vector3((i - 1) * 1.6f, 0f, 0f), heart);

            SpawnFamily(TankDef(),   EnemyRole.Tank,   TankTint,   1.4f, origin + new Vector3(0f, 0f, 1.5f),  heart);
            SpawnFamily(HealerDef(), EnemyRole.Healer, HealerTint, 1.0f, origin + new Vector3(0f, 0f, -2.5f), heart);

            Debug.Log("[EnemyFamilyTestSpawner] Spawned test pack: 3 Grunts (DPS) + 1 Tank + 1 Healer near " +
                      $"{origin} — they march on the Heart; attack them to see the Healer top up the Tank.");
        }

        private Enemy SpawnFamily(EnemyDef def, EnemyRole role, Color tint, float scale, Vector3 pos, Transform heart)
        {
            // Snap onto the baked NavMesh so the agent can path (same guard as the spawner).
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                pos = hit.position;

            Vector3 toHeart = heart.position - pos; toHeart.y = 0f;
            Quaternion rot = toHeart.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toHeart)
                : Quaternion.identity;

            // Capsule body (mirrors WaveManager.BuildPlaceholderEnemy).
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"TestEnemy ({def.DisplayName})";
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = new Vector3(scale, scale, scale);

            // The capsule's own collider must be a trigger so the enemy's contact
            // probe (a SphereCast with QueryTriggerInteraction.Ignore) can't self-hit.
            if (go.TryGetComponent(out Collider col)) col.isTrigger = true;

            // Family tint.
            var mr = go.GetComponent<Renderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var m = new Material(sh);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    else m.color = tint;
                    mr.sharedMaterial = m;
                }
            }

            // NavMeshAgent must exist before Enemy ([RequireComponent]); EnemyBrain
            // after Enemy (its [RequireComponent(typeof(Enemy))]). EnemyBrain.Awake
            // resolves the Heart + "Player"-tagged hero itself.
            go.AddComponent<NavMeshAgent>();
            var enemy = go.AddComponent<Enemy>();
            enemy.Configure($"test-{def.Id}-{_counter++}", def, heart);

            var brain = go.AddComponent<EnemyBrain>();
            brain.Role = role;

            return enemy;
        }

        // ── Per-family stat blocks (code-built EnemyDef) ──────────────────────

        private static EnemyDef GruntDef(int i) => new EnemyDef
        {
            Id = $"grunt-{i}", Name = "Hollow Grunt", DisplayName = "Hollow Grunt", Ai = "walker",
            Hp = 40f, MoveSpeed = 3.0f, ContactDamage = 6f, AttackInterval = 1.2f, Height = 1.7f,
            XpReward = 12, GlimmerReward = 2,
        };

        private static EnemyDef TankDef() => new EnemyDef
        {
            Id = "tank", Name = "Hollow Bulwark", DisplayName = "Hollow Bulwark", Ai = "walker",
            // High HP + slow so it survives long enough to BE healed (shows the Healer working).
            Hp = 220f, MoveSpeed = 1.6f, ContactDamage = 14f, AttackInterval = 1.6f, Height = 2.4f,
            AggroRadius = 12f, XpReward = 40, GlimmerReward = 8,
        };

        private static EnemyDef HealerDef() => new EnemyDef
        {
            Id = "healer", Name = "Hollow Mender", DisplayName = "Hollow Mender", Ai = "walker",
            Hp = 70f, MoveSpeed = 2.6f, ContactDamage = 3f, AttackInterval = 1.5f, Height = 1.8f,
            XpReward = 30, GlimmerReward = 6,
        };

        // ── Heart lookup (name/component, no hard dependency) ─────────────────

        private static Transform ResolveHeart()
        {
            var hc = FindAnyObjectByType<HeartController>();
            if (hc != null) return hc.transform;
            // Fallback by name if the controller type isn't found.
            var byName = GameObject.Find("HeartOfTown") ?? GameObject.Find("Tree of Life") ?? GameObject.Find("Heart");
            return byName != null ? byName.transform : null;
        }
    }
}
