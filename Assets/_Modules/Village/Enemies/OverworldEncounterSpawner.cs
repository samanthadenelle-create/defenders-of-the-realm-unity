// =============================================================================
// OverworldEncounterSpawner — the OPEN-WORLD HOOK for the WO-482 encounter loop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner design 2026-06-23: the open world holds cheap wandering "rep" mobs (a
// single orc that REPRESENTS a family). On ENGAGE -- the mob lands on the hero OR
// the hero attacks the mob -- we POP into the isolated real-time BattleArena where
// the FULL family is staged. The rep itself does NOT fight in-world (hook only):
// it wanders, and on AGGRO it CHASES with a wide leash at ~+5% the hero's speed
// (so a too-tough mob can't be outrun -- the danger-gradient stake) under a
// "they see us" chase-music sting.
//
// REUSE (CLAUDE.md "use items we have"): EnemyFactory builds the rep body (orc
// model + OrcHumanoid rig, WO-482 Slice 1) with ZERO contact damage; EnemyBrain +
// the Enemy hero-aggro (DEF-224) give it the wander/chase for free. The transition
// is the generic BattleArena.BeginEncounter (the isolated open kite arena).
//
// Self-bootstrapping DDOL singleton, FLAG-GATED by FeatureFlags.OverworldEncounter
// (default OFF -- dormant until the vertical is felt-verified). Instrumented per
// CLAUDE.md S12. ASCII logs; LogWarning, never error.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Arena;

namespace DeNelle.Village
{
    /// <summary>Spawns wandering orc "rep" mobs in the open world; engaging one pops into the BattleArena.</summary>
    public sealed class OverworldEncounterSpawner : MonoBehaviour
    {
        public static OverworldEncounterSpawner Instance { get; private set; }

        private const string OuterWorldScene = "OuterWorld";
        // The full family staged in the BATTLE when a rep is engaged (the rep is just the leader's face).
        private static readonly string[] OrcFamily = { "orc-warrior", "orc-tank", "orc-mage" };

        // Rep tuning. Wide aggro + a chase a touch faster than the hero (~6 base) so it
        // "means something" if you wandered into one too strong. Contact damage ZERO
        // (hook, not a combatant) -- engagement, not death, is what the rep delivers.
        private const float RepChaseSpeed = 6.3f;   // ~+5% over the hero's 6.0
        private const int   RepCount      = 2;

        private readonly List<GameObject> _reps = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("OverworldEncounterSpawner").AddComponent<OverworldEncounterSpawner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            MaybePopulate(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MaybePopulate(scene);

        private void MaybePopulate(Scene scene)
        {
            if (!FeatureFlags.OverworldEncounter) return;          // dormant until proven
            if (BattleArena.Instance != null && BattleArena.Instance.BattleInProgress) return; // not while a battle is up
            if (scene.name != OuterWorldScene) return;            // v1: OuterWorld only

            // Stagger off the scene-load frame (mirrors RaidOutpostSystem) so the rep
            // realizes after the world + navmesh are up.
            StartCoroutine(PopulateAfterDelay());
        }

        private System.Collections.IEnumerator PopulateAfterDelay()
        {
            yield return new WaitForSeconds(3f);
            // Drop stale references (scene change destroyed them).
            _reps.RemoveAll(r => r == null);
            for (int i = _reps.Count; i < RepCount; i++) SpawnRep(i);
        }

        private void SpawnRep(int index)
        {
            var hero = GameObject.FindWithTag("Player");
            Vector3 origin = hero != null ? hero.transform.position : Vector3.zero;

            // Place the rep a comfortable distance from the hero, spread by index.
            float ang = (index * 137f) * Mathf.Deg2Rad;
            Vector3 anchor = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (26f + 6f * index);

            var def = new EnemyDef
            {
                Id = "orc-warrior", Name = "Orc Warleader", DisplayName = "Orc Warband", Ai = "walker",
                Hp = 9999f,                 // the rep is a HOOK, not a kill -- it transitions on touch/hit
                MoveSpeed = RepChaseSpeed,  // ~+5% over the hero so it can run you down
                ContactDamage = 0f,         // never hurts the hero in-world (hook only)
                AttackInterval = 1.5f, Height = 2.0f, AggroRadius = 22f, // wide aggro / wide leash
                XpReward = 0, GlimmerReward = 0,
            };

            Enemy enemy = null;
            Guard.Try("Encounter", $"spawn rep #{index}", () =>
            {
                enemy = EnemyFactory.Build(def, anchor, Quaternion.identity, transform);
                if (enemy == null) return;
                enemy.gameObject.name = $"OrcRep_{index}";
                enemy.Configure($"orc-rep-{index}", def, null);   // no Heart -> it wanders its tether + aggros the hero
                enemy.SetBrainTargetPosition(anchor);             // tether: wander around its spawn until it sees you
                enemy.gameObject.AddComponent<EnemyBrain>().Role = EnemyRole.DPS;
                enemy.gameObject.AddComponent<RepEngageWatcher>().Init(OrcFamily, ZoneThreatAt(anchor));
            });

            if (enemy != null)
            {
                _reps.Add(enemy.gameObject);
                FlowTrace.Step("Encounter", $"spawned orc rep #{index} at {anchor} (wide aggro, +5% chase, 0 dmg).");
            }
        }

        // Light threat read from the world zone (reuses the shared classifier).
        private static int ZoneThreatAt(Vector3 pos)
        {
            int t = 1;
            Guard.Try("Encounter", "zone threat", () => t = Mathf.Max(1, DeNelle.Core.World.ZoneManager.ThreatLevel(pos)));
            return t;
        }
    }

    /// <summary>
    /// Rides on a rep mob: watches for ENGAGE (the rep reaches the hero, OR the hero
    /// attacks the rep) and on the first such event POPS into the BattleArena with the
    /// rep's family, consuming the rep. Also fires the "they see us" chase sting once on
    /// aggro. Pure hook logic (no combat). WO-482.
    /// </summary>
    public sealed class RepEngageWatcher : MonoBehaviour
    {
        private string[] _family;
        private int _threat;
        private bool _engaged;
        private bool _stung;
        private Enemy _enemy;

        private const float AggroRange  = 22f;  // wide -- "they see us" + chase begins
        private const float EngageRange = 2.6f; // contact -> transition

        public void Init(string[] family, int threat)
        {
            _family = (family != null && family.Length > 0) ? family : new[] { "orc-warrior" };
            _threat = Mathf.Max(1, threat);
            _enemy = GetComponent<Enemy>();
            if (_enemy != null) _enemy.Damaged += OnRepDamaged;   // hero attacked the rep -> engage
        }

        private void OnDestroy()
        {
            if (_enemy != null) _enemy.Damaged -= OnRepDamaged;
        }

        private void OnRepDamaged(Vector3 _) => Engage();

        private void Update()
        {
            if (_engaged || !FeatureFlags.OverworldEncounter) return;

            var hero = GameObject.FindWithTag("Player");
            if (hero == null) return;
            float d = Vector3.Distance(hero.transform.position, transform.position);

            if (!_stung && d <= AggroRange)
            {
                _stung = true;
                Guard.Try("Encounter", "chase sting", () => AbilityAudioBridge.PlayDangerSting());
                FlowTrace.Step("Encounter", "rep aggro -> chase sting ('they see us').");
            }

            if (d <= EngageRange) Engage();
        }

        private void Engage()
        {
            if (_engaged) return;
            if (BattleArena.Instance != null && BattleArena.Instance.BattleInProgress) return;
            _engaged = true;

            var hero = GameObject.FindWithTag("Player");
            string scene = SceneManager.GetActiveScene().name;

            var p = new EncounterParams
            {
                EnemyIds = _family,
                Threat = _threat,
                BackdropContext = ThemeForScene(scene),
                ReturnScene = scene,
                ReturnPosition = hero != null ? hero.transform.position : transform.position,
                ReturnYaw = hero != null ? hero.transform.eulerAngles.y : 0f,
                RepId = gameObject.name,
            };

            FlowTrace.Step("Encounter", $"ENGAGE rep '{gameObject.name}' -> BattleArena (family [{string.Join(",", _family)}], threat {_threat}, theme '{p.BackdropContext}').");

            bool started = false;
            Guard.Try("Encounter", "begin encounter", () => started = BattleArena.Instance.BeginEncounter(p));

            // Consume the rep regardless (the full family lives in the battle now); if the
            // battle failed to start (flag off / busy) the rep simply despawns -- never a stuck hook.
            Destroy(gameObject);
        }

        private static string ThemeForScene(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return "outerworld";
            string s = scene.ToLowerInvariant();
            if (s.Contains("castle")) return "castle";
            if (s.Contains("dungeon") || s.Contains("cavern")) return "cavern";
            return "outerworld";
        }
    }
}
