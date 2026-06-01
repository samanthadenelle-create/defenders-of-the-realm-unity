// =============================================================================
// FamilyTestSpawner — dev hotkey to spawn a leader + follower FAMILY and watch
// the formation/slot system move and switch (WO-146).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Mirrors EnemyFamilyTestSpawner ('J' role pack) but builds a leader + N followers
// as a Monster Family with FamilyLeader/FamilyMember so the formation is visible
// in the live (baked) Village with ZERO scene/prefab/SO edit:
//   • 'K' — spawn a fresh family (1 leader + 5 followers) ~16 m south of the Heart.
//   • 'L' — cycle the formation (LooseRing → Wedge → Line → TightPack → Column),
//           so the owner can confirm the LERP + slot stability.
//
// Open-world is BLOCKED on the WO-142 exterior NavMesh bake — this is Village-only.
// Dev-only; harmless otherwise. Promote to RaidDirector/EnemyGroupSpawner assembly
// once production family content exists.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core;

namespace DeNelle.Village
{
    /// <summary>Dev hotkeys: 'K' spawns a Monster Family, 'L' cycles its formation.</summary>
    public sealed class FamilyTestSpawner : MonoBehaviour
    {
        public static FamilyTestSpawner Instance { get; private set; }

        private const string  TargetScene = "Village";
        private const KeyCode SpawnKey    = KeyCode.K;
        private const KeyCode CycleKey    = KeyCode.L;
        private const int     FollowerCount = 5;

        private static readonly Color LeaderTint   = new Color(0.85f, 0.55f, 0.15f); // amber
        private static readonly Color FollowerTint = new Color(0.40f, 0.45f, 0.70f); // slate

        private Transform     _root;
        private FamilyLeader  _activeLeader;
        private int           _shapeIndex;
        private int           _counter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("FamilyTestSpawner").AddComponent<FamilyTestSpawner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) AnnounceHotkeys();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) AnnounceHotkeys();
        }

        private static void AnnounceHotkeys()
        {
            Debug.Log("[FamilyTestSpawner] 'K' = spawn a Monster Family (1 leader + " +
                      $"{FollowerCount} followers). 'L' = cycle its formation " +
                      "(LooseRing/Wedge/Line/TightPack/Column).");
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != TargetScene) return;
            if (Input.GetKeyDown(SpawnKey)) SpawnFamily();
            if (Input.GetKeyDown(CycleKey)) CycleShape();
        }

        private void CycleShape()
        {
            if (_activeLeader == null)
            {
                Debug.LogWarning("[FamilyTestSpawner] No active family — press 'K' first.");
                return;
            }
            _shapeIndex = (_shapeIndex + 1) % 5;
            var shape = (FormationType)_shapeIndex;
            _activeLeader.DevForceShape(shape);
            Debug.Log($"[FamilyTestSpawner] Formation → {shape}");
        }

        private void SpawnFamily()
        {
            Transform heart = ResolveHeart();
            if (heart == null)
            {
                Debug.LogWarning("[FamilyTestSpawner] No Heart found — cannot spawn the family.");
                return;
            }

            if (_root == null) _root = new GameObject("[FamilyTestPack]").transform;

            Vector3 origin = heart.position + new Vector3(0f, 0f, -16f);

            Enemy leaderEnemy = SpawnUnit(LeaderDef(), LeaderTint, 1.3f, origin, heart);
            var leader = leaderEnemy.gameObject.AddComponent<FamilyLeader>();
            _activeLeader = leader;
            _shapeIndex = 0;

            for (int i = 0; i < FollowerCount; i++)
            {
                Vector3 pos = origin + new Vector3((i - FollowerCount / 2) * 1.4f, 0f, -2f);
                Enemy follower = SpawnUnit(FollowerDef(i), FollowerTint, 1.0f, pos, heart);
                var member = follower.gameObject.AddComponent<FamilyMember>();
                leader.RegisterMember(member);
            }

            Debug.Log($"[FamilyTestSpawner] Spawned a family at {origin}: 1 leader + " +
                      $"{FollowerCount} followers. Press 'L' to cycle formations.");
        }

        private Enemy SpawnUnit(EnemyDef def, Color tint, float scale, Vector3 pos, Transform heart)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                pos = hit.position;

            Vector3 toHeart = heart.position - pos; toHeart.y = 0f;
            Quaternion rot = toHeart.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toHeart) : Quaternion.identity;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"FamilyUnit ({def.DisplayName})";
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = new Vector3(scale, scale, scale);

            if (go.TryGetComponent(out Collider col)) col.isTrigger = true;

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

            go.AddComponent<NavMeshAgent>();
            var enemy = go.AddComponent<Enemy>();
            enemy.Configure($"family-{def.Id}-{_counter++}", def, heart);
            go.AddComponent<EnemyBrain>();   // leader keeps it; followers disable it while following.
            return enemy;
        }

        private static EnemyDef LeaderDef() => new EnemyDef
        {
            Id = "family-leader", Name = "Pack Alpha", DisplayName = "Pack Alpha", Ai = "walker",
            Hp = 160f, MoveSpeed = 2.4f, ContactDamage = 10f, AttackInterval = 1.4f, Height = 2.2f,
            XpReward = 35, GlimmerReward = 6,
        };

        private static EnemyDef FollowerDef(int i) => new EnemyDef
        {
            Id = $"family-follower-{i}", Name = "Pack Runner", DisplayName = "Pack Runner", Ai = "walker",
            Hp = 55f, MoveSpeed = 2.8f, ContactDamage = 6f, AttackInterval = 1.2f, Height = 1.7f,
            XpReward = 14, GlimmerReward = 2,
        };

        private static Transform ResolveHeart()
        {
            var hc = FindAnyObjectByType<HeartController>();
            if (hc != null) return hc.transform;
            var byName = GameObject.Find("HeartOfTown") ?? GameObject.Find("Tree of Life") ?? GameObject.Find("Heart");
            return byName != null ? byName.transform : null;
        }
    }
}
