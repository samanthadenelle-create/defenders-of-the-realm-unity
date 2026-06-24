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
        private const int   RepCount      = 8;   // owner 2026-06-23: scatter roaming reps (8 for the verify lap; proximity-realization next so this can scale to 20+ cheaply)

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

            // SCATTER (owner 2026-06-23 "20 random ones roaming everywhere"): each rep takes a RANDOM
            // reachable navmesh point in a ring around the hero, so they populate the world and you can
            // always bump into one. Validate PathComplete (up to 8 tries) so a rep never strands on an
            // island across the seam. Each rep then ROAMS its leash (RepEngageWatcher) until it sees you,
            // then chases. (Replaces the old single-rep courtyard placement; THIS is the spread.)
            // CASTLE = SAFE (owner 2026-06-23): a rep may ONLY spawn on an OuterWorld roster region,
            // never inside the castle/Village footprint (enemies can't reliably traverse the seam
            // navmesh). The anchor starts UNSET -- it is ONLY assigned from a candidate that PASSES the
            // HasRoster zone gate. If the 8-try loop finds none, we DO NOT SPAWN (no castle-side
            // fall-through). This keeps the castle a safe shop/gear haven; the chase begins only once
            // the hero has crossed into OuterWorld.
            // ===== V2 TODO (owner wants to RESOLVE this, not now) =====
            // The castle-safe rule is currently a WORKAROUND for a navmesh limitation: enemy
            // agents don't reliably path ACROSS the RegionGate seam (separate navmesh islands +
            // the hero warp-crossing, not an agent-walkable link). V2: stitch/link the navmesh
            // across the seam (NavMeshLink the agents actually traverse) so reps CAN pursue the
            // hero between regions -- then "castle = safe" becomes a deliberate DESIGN choice
            // (e.g. a warded threshold), not a tech limitation, and this OuterWorld-only spawn
            // gate + the chase-stalls-at-seam behaviour can be lifted/retuned.
            Vector3 anchor = Vector3.zero;
            bool anchorFound = false;
            if (hero != null)
            {
                var path = new UnityEngine.AI.NavMeshPath();
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float a = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float dist = UnityEngine.Random.Range(14f, 55f);
                    Vector3 cand = origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(cand, out var ch, 8f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    bool inOuter = false;
                    Guard.Try("Encounter", "rep zone gate", () => inOuter =
                        DeNelle.Core.World.RegionSpawnTable.HasRoster(DeNelle.Core.World.ZoneManager.GetZone(ch.position)));
                    if (!inOuter) continue;
                    if (UnityEngine.AI.NavMesh.CalculatePath(origin, ch.position, UnityEngine.AI.NavMesh.AllAreas, path)
                        && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    { anchor = ch.position; anchorFound = true; break; }
                }
            }

            // NO castle-side fall-through: if no OuterWorld-side candidate cleared the zone gate in 8
            // tries (e.g. the hero is still in/near the castle), SKIP this spawn so the castle stays safe.
            if (!anchorFound)
            {
                FlowTrace.Warn("Encounter", $"SpawnRep #{index}: no OuterWorld-side candidate in 8 tries -> skipping (castle stays safe).");
                return;
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

            // POST-SNAP CASTLE-SAFE RE-CHECK (owner 2026-06-23): the 12m navmesh snap above can drift
            // the anchor OFF its zone-gated candidate and back across the seam into the Village/castle
            // footprint. Re-confirm the FINAL position is still an OuterWorld roster region; if it
            // drifted into the castle, ABORT the spawn so a snapped point never leaks a rep castle-side.
            bool finalInOuter = false;
            Guard.Try("Encounter", "rep zone gate (post-snap)", () => finalInOuter =
                DeNelle.Core.World.RegionSpawnTable.HasRoster(DeNelle.Core.World.ZoneManager.GetZone(anchor)));
            if (!finalInOuter)
            {
                FlowTrace.Warn("Encounter", $"SpawnRep #{index}: final anchor {anchor} snapped into a non-OuterWorld (castle/Village) region -> aborting spawn (castle stays safe).");
                return;
            }

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
                enemy.SetBrainTargetPosition(anchor);             // tether: idle at its spawn until it sees you
                // NO EnemyBrain (fix 2026-06-23 "can't find the orc"): a DPS brain calls
                // SetBrainTargetPosition(null) EVERY frame (DPS returns no destination), which CLEARED
                // RepEngageWatcher's chase target each frame -> the rep never actually chased. The
                // RepEngageWatcher now fully owns the rep: tether (above) until aggro, then it drives
                // the brain-position override onto the hero uncontested so the orc runs you down.
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
        private const float LeashRadius = 14f;  // wander this far from spawn until aggro

        private Vector3 _leashCenter;           // spawn point -- centre of the wander leash
        private float   _roamRepathAt;          // next time to pick a new roam point

        public void Init(string[] family, int threat)
        {
            _family = (family != null && family.Length > 0) ? family : new[] { "orc-warrior" };
            _threat = Mathf.Max(1, threat);
            _enemy = GetComponent<Enemy>();
            _leashCenter = transform.position;                    // wander leash centred on the spawn
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

            // FALL-THROUGH GUARD (owner 2026-06-23 "they fall through ground when I change zones"):
            // a zone/navmesh swap can drop a NavMeshAgent below the floor. If a rep falls below y=-2,
            // re-snap it onto the navmesh AND log it -- self-heals, and PROVES whether the fall is real.
            if (transform.position.y < -2f)
            {
                Guard.Try("Encounter", "rep re-seat", () =>
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 20f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        transform.position = hit.position;
                        FlowTrace.Warn("Encounter", $"rep '{gameObject.name}' fell below y=-2 -> re-seated onto navmesh at {hit.position}.");
                    }
                });
            }

            var hero = GameObject.FindWithTag("Player");
            if (hero == null) return;
            float d = Vector3.Distance(hero.transform.position, transform.position);

            if (!_stung && d <= AggroRange)
            {
                _stung = true;
                Guard.Try("Encounter", "chase sting", () => AbilityAudioBridge.PlayDangerSting());
                FlowTrace.Step("Encounter", "rep aggro -> chase sting ('they see us').");
            }

            // ROAM until aggro, then CHASE -- "a wandering leash till it goes to battle" (owner 2026-06-23).
            // The rep drives Enemy's brain-position override (no EnemyBrain to clear it): a random leash
            // point while idle, the hero once it sees you. +5% MoveSpeed guarantees the chase closes to
            // EngageRange, so the orc runs you down instead of being left behind.
            if (_enemy != null)
            {
                if (_stung)
                    Guard.Try("Encounter", "rep chase", () => _enemy.SetBrainTargetPosition(hero.transform.position));
                else if (Time.time >= _roamRepathAt)
                {
                    Vector3 roam = PickRoamPoint();
                    Guard.Try("Encounter", "rep roam", () => _enemy.SetBrainTargetPosition(roam));
                    _roamRepathAt = Time.time + UnityEngine.Random.Range(2.5f, 5f);
                }
            }

            if (d <= EngageRange) Engage();
        }

        // Random navmesh point within the leash of the spawn -- the wander target while idle.
        private Vector3 PickRoamPoint()
        {
            Vector3 p = _leashCenter;
            Guard.Try("Encounter", "roam pick", () =>
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * LeashRadius;
                Vector3 cand = _leashCenter + new Vector3(r.x, 0f, r.y);
                if (UnityEngine.AI.NavMesh.SamplePosition(cand, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                    p = hit.position;
            });
            return p;
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
