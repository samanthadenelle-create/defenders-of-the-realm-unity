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

        private const string  TargetScene = "Village2";
        private const KeyCode SpawnKey    = KeyCode.J;

        private static readonly Color GruntTint  = new Color(0.58f, 0.58f, 0.64f); // grey
        private static readonly Color TankTint   = new Color(0.72f, 0.20f, 0.16f); // red
        // WO-956: was green (0.22, 0.72, 0.34) - even a dev-hotkey ENEMY never wears the
        // safe hue (owner is red/green colourblind). Sickly violet via the hostile palette.
        private static readonly Color HealerTint = HostilePalette.PlaceholderEffectTint;

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
            // PLAYER-BUILD SAFETY: the 'J' spawn hotkey is gated behind the global
            // DevHotkeys kill-switch (default OFF) so it can never spawn a test enemy
            // pack in the shipped .exe OR the editor unless a dev opts in
            // (PlayerPrefs ff.devhotkeys=1).
            if (!DeNelle.Core.FeatureFlags.DevHotkeys) return;
            if (Input.GetKeyDown(SpawnKey)) SpawnTestPack();
            // Owner 2026-07-06: 'K' scatters HIGH-LEVEL enemies far from town to felt-test
            // the ThreatSkullPlate warning + the hero-vs-high-level damage feel.
            if (Input.GetKeyDown(KeyCode.K)) SpawnHighLevelScatter();
            // WO-680: 'B' spawns a Blink orc next to its Tripo counterpart for the
            // side-by-side felt-compare (Blink Stylized Orcs activation — additive,
            // DevHotkeys-gated like the rest, so live balance is untouched).
            if (Input.GetKeyDown(KeyCode.B)) SpawnBlinkOrcCompare();
        }

        /// <summary>
        /// WO-680 felt-compare: spawn ONE Blink orc warrior (Resources/Enemies/Blink,
        /// staged by BlinkOrcImporter) beside ONE Tripo orc-warrior, ~8 m in front of
        /// the hero, both idling at their own anchor (march target = own anchor, the
        /// SpawnHighLevelScatter pattern) so the owner can walk around them and judge
        /// look + animation quality side by side. Same stat block on both — the compare
        /// is purely visual/anim. Requires the import to have run; a missing Blink
        /// prefab degrades to EnemyFactory's tinted capsule + a FlowTrace.Fail naming
        /// the unresolved Resources path (run BlinkOrcImporter.Run).
        /// </summary>
        private void SpawnBlinkOrcCompare()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null)
            {
                Debug.LogWarning("[EnemyFamilyTestSpawner] No 'Player' hero found — cannot spawn the Blink compare.");
                return;
            }
            if (_root == null) _root = new GameObject("[EnemyFamilyTestPack]").transform;

            Vector3 fwd = hero.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 center = hero.transform.position + fwd * 8f;

            SpawnCompareOrc(CompareDef("blink-orc-warrior", "Blink Orc (new)"), center + right * 1.5f, hero);
            SpawnCompareOrc(CompareDef("orc-warrior", "Tripo Orc (current)"), center - right * 1.5f, hero);

            Debug.Log("[EnemyFamilyTestSpawner] Blink-vs-Tripo orc compare spawned 8m ahead " +
                      "(Blink on the right, Tripo on the left — both hold ground; walk around them).");
        }

        private void SpawnCompareOrc(EnemyDef def, Vector3 pos, GameObject hero)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                pos = hit.position;

            // Idle-at-anchor (own anchor = march target) so the pair stands for inspection.
            var anchor = new GameObject($"CompareAnchor-{def.Id}").transform;
            anchor.SetParent(_root, false);
            anchor.position = pos;

            Vector3 toHero = hero.transform.position - pos; toHero.y = 0f;
            Quaternion rot = toHero.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toHero) : Quaternion.identity;

            var enemy = EnemyFactory.Build(def, pos, rot, _root);
            enemy.gameObject.name = $"CompareEnemy ({def.DisplayName})";
            enemy.Configure($"compare-{def.Id}-{_counter++}", def, anchor);
        }

        private static EnemyDef CompareDef(string id, string label) => new EnemyDef
        {
            Id = id, Name = label, DisplayName = label, Ai = "walker",
            Hp = 80f, MoveSpeed = 2.6f, ContactDamage = 6f, AttackInterval = 1.2f, Height = 1.9f,
            AggroRadius = 6f, XpReward = 15, GlimmerReward = 2,
        };

        /// <summary>
        /// Scatter 5 high-level enemies 120–200 m out from the hero at random bearings
        /// (owner test rig, 2026-07-06). Levels 15/18/21/24/27 — Enemy.Level reads the
        /// authored-HP band (HP = level × 25, WO-611 F3), so the target frame shows the
        /// real number and ThreatSkullPlate skulls anything above the hero's level. Each
        /// enemy IDLES at its spawn anchor (its march target = its own anchor, not the
        /// Heart) so the test never pulls a Lv-27 pack into town — the hero must walk out.
        /// </summary>
        private void SpawnHighLevelScatter()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null)
            {
                Debug.LogWarning("[EnemyFamilyTestSpawner] No 'Player' hero found — cannot scatter.");
                return;
            }
            if (_root == null) _root = new GameObject("[EnemyFamilyTestPack]").transform;

            int spawned = 0;
            for (int i = 0; i < 5; i++)
            {
                int level = 15 + i * 3;
                float bearing = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist = UnityEngine.Random.Range(120f, 200f);
                Vector3 pos = hero.transform.position
                            + new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing)) * dist;
                if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 25f, NavMesh.AllAreas))
                {
                    Debug.Log($"[EnemyFamilyTestSpawner] scatter #{i} (Lv{level}): no navmesh within 25m of {pos} — skipped.");
                    continue;
                }
                pos = hit.position;

                var def = HighLevelDef(i, level);
                // Anchor = its own spawn point, so the enemy holds ground instead of
                // marching the Heart; AggroRadius engages when the hero closes in.
                var anchor = new GameObject($"ScatterAnchor-Lv{level}").transform;
                anchor.SetParent(_root, false);
                anchor.position = pos;

                var enemy = EnemyFactory.Build(def, pos, Quaternion.identity, _root);
                enemy.gameObject.name = $"ScatterEnemy (Lv{level} {def.DisplayName})";
                enemy.Configure($"scatter-lv{level}-{_counter++}", def, anchor);
                var brain = enemy.gameObject.AddComponent<EnemyBrain>();
                brain.Role = EnemyRole.DPS;
                int threat = level;   // capture for the plate
                ThreatSkullPlate.Attach(enemy.gameObject, () => threat);
                spawned++;
                DeNelle.Core.Diagnostics.FlowTrace.Step("Scatter",
                    $"Lv{level} '{def.DisplayName}' at {pos} ({dist:0}m out, hp={def.Hp}, dmg={def.ContactDamage})");
            }
            Debug.Log($"[EnemyFamilyTestSpawner] High-level scatter: {spawned}/5 placed 120–200m out. " +
                      "Walk out to test the skull warning + damage feel (they hold ground; they won't march on town).");
        }

        /// <summary>Synthesized high-level def: HP = level × 25 (the Enemy.Level band, so the
        /// shown level is exactly what we authored) + damage on the RegionMobSpawner 10%/threat
        /// curve. Skeleton-warrior visual family (AccuRig, always imported).</summary>
        private static EnemyDef HighLevelDef(int i, int level) => new EnemyDef
        {
            Id = $"scatter-elite-{i}", Name = "Hollow Reaver", DisplayName = "Hollow Reaver", Ai = "walker",
            Hp = level * 25f, MoveSpeed = 2.4f,
            ContactDamage = 8f * (1f + 0.10f * level), AttackInterval = 1.2f, Height = 2.0f,
            AggroRadius = 18f, XpReward = level * 10, GlimmerReward = level,
        };

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

            // One skinned body via the shared EnemyFactory — no parallel spawn code (CLAUDE.md §9).
            var enemy = EnemyFactory.Build(def, pos, rot, _root);
            enemy.gameObject.name = $"TestEnemy ({def.DisplayName})";
            enemy.Configure($"test-{def.Id}-{_counter++}", def, heart);

            var brain = enemy.gameObject.AddComponent<EnemyBrain>();
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
