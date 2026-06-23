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

        private bool _populating;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // OuterWorld may ALREADY be loaded additively (the active scene is MainCastle_Hall,
            // OuterWorld streams in over it via WorldSceneLoader) by the time this DDOL singleton
            // boots — the per-scene sceneLoaded callback won't re-fire for an already-loaded scene.
            // So evaluate the WHOLE loaded set now, not just the active scene (the old bug: this
            // checked only GetActiveScene() == "OuterWorld", which is FALSE in MainCastle_Hall, so
            // reps never spawned in the live additive setup).
            MaybePopulate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MaybePopulate();

        // True when OuterWorld is loaded (active OR additive), case-insensitive — mirrors
        // RaidOutpostSystem.InOuterWorld so the rep gate matches the other world systems.
        private static bool OuterWorldLoaded()
        {
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded &&
                    !string.IsNullOrEmpty(s.name) &&
                    s.name.IndexOf(OuterWorldScene, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void MaybePopulate()
        {
            if (!FeatureFlags.OverworldEncounter) { FlowTrace.Step("Encounter", "MaybePopulate: ff.overworldencounter OFF — dormant."); return; }
            if (BattleArena.Instance != null && BattleArena.Instance.BattleInProgress) return; // not while a battle is up
            if (!OuterWorldLoaded())                                  // v1: OuterWorld only
            {
                FlowTrace.Step("Encounter", "MaybePopulate: OuterWorld not loaded yet — waiting for its sceneLoaded.");
                return;
            }
            if (_populating) return;                                  // a populate is already scheduled

            // Stagger off the scene-load frame (mirrors RaidOutpostSystem) so the rep
            // realizes after the world + navmesh are up.
            _populating = true;
            StartCoroutine(PopulateAfterDelay());
        }

        private System.Collections.IEnumerator PopulateAfterDelay()
        {
            yield return new WaitForSeconds(3f);
            _populating = false;

            // The hero spawns in MainCastle_Hall and WARPS into OuterWorld later (SceneTransitionTrigger).
            // If reps were anchored to the hero's CASTLE position they'd strand 26m+ from where the hero
            // actually walks out — "too far, they do not engage". Wait until the hero is actually standing
            // IN the OuterWorld region before anchoring the reps to its current position.
            float waited = 0f;
            while (waited < 30f && !HeroInOuterWorld())
            {
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            _reps.RemoveAll(r => r == null);   // drop stale references (scene change destroyed them)
            if (!HeroInOuterWorld())
            {
                FlowTrace.Warn("Encounter", "PopulateAfterDelay: hero not in OuterWorld after 30s — anchoring reps to world origin (will re-anchor on next OuterWorld load).");
            }
            int spawned = 0;
            for (int i = _reps.Count; i < RepCount; i++) { SpawnRep(i); spawned++; }
            FlowTrace.Step("Encounter", $"PopulateAfterDelay: ensured {_reps.Count}/{RepCount} reps live (spawned {spawned} this pass).");
        }

        // The hero is "in" OuterWorld once it is physically inside an outer region (ZoneManager
        // classifies its position into a roster region) — i.e. it has crossed out of the castle/
        // village footprint. Until then, anchoring reps to the hero would place them in the castle.
        private static bool HeroInOuterWorld()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) return false;
            bool outside = false;
            Guard.Try("Encounter", "hero-in-world check",
                () => outside = DeNelle.Core.World.RegionSpawnTable.HasRoster(
                                    DeNelle.Core.World.ZoneManager.GetZone(hero.transform.position)));
            return outside;
        }

        private void SpawnRep(int index)
        {
            var hero = GameObject.FindWithTag("Player");
            Vector3 origin = hero != null ? hero.transform.position : Vector3.zero;
            if (hero == null)
                FlowTrace.Warn("Encounter", $"SpawnRep #{index}: no 'Player'-tagged hero found — anchoring rep to world origin (it may strand far from the player).");

            // Default placement: a comfortable distance from the hero, spread by index.
            float ang = (index * 137f) * Mathf.Deg2Rad;
            Vector3 anchor = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (26f + 6f * index);

            // FINDABILITY PLACEMENT (arena felt-verify, 2026-06-23): rep #0 must spawn where the hero
            // can WALK STRAIGHT TO IT without crossing the south castle seam. (The owner saw a
            // south-placed rep but it sat on the OTHER SIDE of the gate — a different navmesh island,
            // unreachable while the seam is narrow.) So pick the FIRST short courtyard offset (biased
            // AWAY from the south gate, avoiding -Z) that BOTH snaps to navmesh AND yields a
            // PathComplete path from the hero — proving it's on the hero's OWN island. REVERSIBLE:
            // delete this branch to restore the angular spread for ALL reps.
            if (index == 0 && hero != null)
            {
                Vector3[] candidates =
                {
                    new Vector3( 10f, 0f,   0f),   // east
                    new Vector3(-10f, 0f,   0f),   // west
                    new Vector3(  0f, 0f,  10f),   // north (away from the south gate)
                    new Vector3(  8f, 0f,   8f),   // NE
                    new Vector3( -8f, 0f,   8f),   // NW
                    new Vector3( 12f, 0f,   0f),   // east, a touch farther
                    new Vector3(-12f, 0f,   0f),   // west, a touch farther
                };
                bool placed = false;
                var path = new UnityEngine.AI.NavMeshPath();
                foreach (var off in candidates)
                {
                    Vector3 cand = origin + off;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(cand, out var ch, 6f, UnityEngine.AI.NavMesh.AllAreas))
                        continue;
                    if (UnityEngine.AI.NavMesh.CalculatePath(origin, ch.position, UnityEngine.AI.NavMesh.AllAreas, path)
                        && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    {
                        anchor = ch.position;
                        placed = true;
                        FlowTrace.Step("Encounter", $"SpawnRep #0: REACHABLE test-placement at {anchor} (offset {off} from hero {origin}, path PathComplete) — same-island courtyard, no seam crossing. Revert this branch for angular spread.");
                        break;
                    }
                }
                if (!placed)
                    FlowTrace.Warn("Encounter", $"SpawnRep #0: no PathComplete courtyard offset found near hero {origin} — falling back to angular anchor {anchor} (may be unreachable; check bake/seam).");
            }

            // Belt-and-suspenders (data 2026-06-23): snap the anchor onto the baked navmesh so the
            // rep spawns walkable + can path to the hero. The terrain re-center (WO-483) puts a floor
            // under the play area; this guards the edges so a rep never lands in a no-navmesh pocket
            // (the old failure: "Failed to create agent because there is no valid NavMesh" / "no
            // COMPLETE path to hero"). If nothing's within 12m, log it LOUD rather than spawn a dead rep.
            if (UnityEngine.AI.NavMesh.SamplePosition(anchor, out var navHit, 12f, UnityEngine.AI.NavMesh.AllAreas))
                anchor = navHit.position;
            else
                FlowTrace.Warn("Encounter", $"SpawnRep #{index}: no navmesh within 12m of {anchor} — rep may be unreachable (check OuterWorld floor/bake).");

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

        // -----------------------------------------------------------------------------
        // TEST SEAM (WO-482 fleet oracle) — runs the SAME real spawn path MaybePopulate()
        // drives, but WITHOUT the flag/scene/already-populating gates and WITHOUT the
        // 3s+30s stagger waits (the oracle has already warped the hero into an OuterWorld
        // roster region + asserted navmesh). It ensures up to RepCount reps exist via the
        // real SpawnRep -> EnemyFactory -> RepEngageWatcher chain, so the oracle proves the
        // ACTUAL rep->engage->battle path, never a BeginEncounter bypass. ASCII-only.
        // -----------------------------------------------------------------------------
        public void ForcePopulateForTest()
        {
            _reps.RemoveAll(r => r == null);
            int spawned = 0;
            for (int i = _reps.Count; i < RepCount; i++) { SpawnRep(i); spawned++; }
            FlowTrace.Step("Encounter", $"ForcePopulateForTest: ensured {_reps.Count}/{RepCount} reps live (spawned {spawned} via real SpawnRep).");
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

            FlowTrace.Step("Encounter", $"ENGAGE rep '{gameObject.name}' -> BattleArena (family [{string.Join(",", _family)}], threat {_threat}, theme '{p.BackdropContext}', hero={(hero != null ? "found" : "NULL")}).");

            bool started = false;
            var arena = BattleArena.Instance;   // lazy singleton — non-null, but guard anyway
            if (arena == null)
            {
                FlowTrace.Fail("Encounter", "Engage: BattleArena.Instance was NULL — cannot drop to battle.");
            }
            else
            {
                Guard.Try("Encounter", "begin encounter", () => started = arena.BeginEncounter(p));
            }

            // No drop to battle is the OWNER's reported symptom — make the failure LOUD so a
            // capture pinpoints WHY (ff off / battle already in progress / empty family) instead
            // of the rep silently despawning and the player wondering why nothing happened.
            if (started)
                FlowTrace.Step("Encounter", $"Engage: BattleArena.BeginEncounter SUCCEEDED for rep '{gameObject.name}' — dropped to battle.");
            else
                FlowTrace.Fail("Encounter", $"Engage: BattleArena.BeginEncounter returned FALSE for rep '{gameObject.name}' — NO drop to battle (check ff.overworldencounter / BattleInProgress / empty family).");

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
