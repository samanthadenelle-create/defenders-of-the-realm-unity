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
using UnityEngine.UI;                 // world-space threat nameplate ("!" alert) on the engaging rep
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Arena;
using DeNelle.Village.UI;             // Billboard — keeps the rep's threat cue facing the camera

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
        // CONCURRENT roaming rep count. Owner 2026-06-24 FELT: "drop those 20 spawns down to like 6"
        // / "there are a lot" — the world holds ~6 reps at once (was 8), NOT a 20-up-front swarm.
        // Nudge this to retune crowding.
        private const int   RepCount      = 6;   // owner 2026-06-24: concurrent reps (was 8) — keep the world populated, not crowded

        // RESPAWN/MAINTAIN tuning (owner 2026-06-24 "just set a respawn"): reps are CONSUMED when
        // engaged (Engage() destroys the rep -> the family fights in the BattleArena), so without a
        // maintain loop the world depletes to empty. A repeating maintain loop re-tops the world back
        // to RepCount, but only after a delay so a fresh replacement doesn't pop in instantly on top of
        // the hero. RespawnCheckInterval = how often we re-evaluate the live count; tune both.
        private const float RespawnCheckInterval = 10f;  // owner 2026-06-24: re-top-up cadence (~8-15s feel)

        // BUFFER tuning (owner 2026-06-24 FELT values — dial these): give the hero room to cross
        // out of the castle and walk a bit into OuterWorld before any rep is on top of her.
        // SpawnMinDistance/SpawnMaxDistance = the ring (around the hero) reps spawn into. Raising
        // the MIN pushes reps DEEPER so a freshly-crossed hero isn't aggro'd right at the seam.
        private const float SpawnMinDistance = 28f;  // was 14f — push reps deeper into OuterWorld
        private const float SpawnMaxDistance = 55f;  // ring outer edge (unchanged)

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
        internal static bool OuterWorldLoaded()
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

            // RESPAWN/MAINTAIN: keep the world topped at RepCount. Reps are CONSUMED on engage
            // (Engage() Destroy()s them), so this loop replaces any that died/engaged after a delay
            // (RespawnCheckInterval) — the world stays populated at ~RepCount without a 20-up-front
            // swarm. Idempotent: only one maintain loop runs (guarded by _maintaining).
            if (!_maintaining)
            {
                _maintaining = true;
                StartCoroutine(MaintainLoop());
            }
        }

        private bool _maintaining;

        // Perpetual top-up: every RespawnCheckInterval, while OuterWorld is loaded + no battle is
        // staged + the hero is in OuterWorld, re-spawn replacements until the live count is back at
        // RepCount. The respawn DELAY is the interval itself (a consumed rep is replaced on the next
        // tick, not instantly), so a replacement never pops in on top of a freshly-returned hero.
        // Spawns stay spread out — SpawnRep scatters each onto a random reachable ring point.
        private System.Collections.IEnumerator MaintainLoop()
        {
            var wait = new WaitForSeconds(RespawnCheckInterval);
            while (true)
            {
                yield return wait;

                if (!FeatureFlags.OverworldEncounter) continue;                 // dormant
                if (BattleArena.Instance != null && BattleArena.Instance.BattleInProgress) continue; // not mid-battle
                if (!OuterWorldLoaded()) continue;                              // OuterWorld only
                if (!HeroInOuterWorld()) continue;                              // anchor to the hero only once she is out

                _reps.RemoveAll(r => r == null);   // drop consumed/destroyed reps
                if (_reps.Count >= RepCount) continue;

                int spawned = 0;
                for (int i = _reps.Count; i < RepCount; i++) { SpawnRep(i); spawned++; }
                if (spawned > 0)
                    FlowTrace.Step("Encounter", $"respawn rep -> {_reps.Count}/{RepCount} live (respawned {spawned} this tick).");
            }
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
                    float dist = UnityEngine.Random.Range(SpawnMinDistance, SpawnMaxDistance);
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
                // FIELD-KILLABLE REP (owner 2026-06-28): the rep is no longer an un-killable hook.
                // The player can KITE it with ranged attacks and KILL it in the open world BEFORE the
                // BattleArena triggers, earning the SAME payout an arena win of this orc family grants
                // (Enemy.Die(killed:true) -> HeroProgression.AddXp + Glimmer/gold + ItemDropWatcher loot).
                // Hp ~ one tanky orc's worth (arena tank=190, warrior=120 at threat 1): kitable in a few
                // ranged hits, never a one-shot. XP/Glimmer aggregate the 3-orc family (BattleArena
                // BuildEncounterDef: ~14 XP + ~3 Glimmer per orc, x3). All three are OWNER-TUNABLE.
                Hp = 150f,                  // owner-tunable: between arena warrior(120) and tank(190); kitable, not one-shot
                MoveSpeed = RepChaseSpeed,  // ~+5% over the hero so it can run you down
                ContactDamage = 0f,         // never hurts the hero in-world (hook only)
                AttackInterval = 1.5f, Height = 2.0f, AggroRadius = 8f, // notice radius (owner 2026-06-27: 22->8, reconciled to RepEngageWatcher.AggroRange)
                XpReward = 42, GlimmerReward = 9, // owner-tunable: ~14 XP + ~3 Glimmer per orc x3 (matches an arena win of this family)
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
        private GameObject _threatCue;   // world-space "!" nameplate raised on aggro (child of the rep)

        // AggroRange = how far a rep can NOTICE the hero and start the chase. Lowered from 22f
        // (owner 2026-06-24 FELT buffer) so a rep doesn't spot the hero from across the map / reach
        // back across the seam — the hero gets a buffer after crossing before being hunted. Once
        // aggro'd, the chase/leash/engage behaviour below is UNCHANGED (owner loves the chase).
        private const float AggroRange  = 8f;   // owner 2026-06-27: 14->8 (chase starts at 8m; fight still only at contact/TouchDistance)
        private const float EngageRange = 2.6f; // contact -> transition
        private const float LeashRadius = 14f;  // wander this far from spawn until aggro

        // -------------------------------------------------------------------------
        //  BATTLE ISOLATION + POST-LOSS GRACE (lose-flow fix, owner TOP priority).
        //  Two STATIC gates shared by EVERY home-scene rep:
        //    * _battlePaused  — while a BattleArena fight is staged, ALL home reps freeze
        //      (no roam/chase/aggro/engage). Removes the home-combat rumble bleed, the
        //      re-engage loop source, AND the double-sim choppiness in one move. Driven by
        //      BattleArena.BeginEncounter (PauseAll) / Resolve (ResumeAll).
        //    * _noEngageUntil — a brief re-aggro GRACE after a LOSS: no rep may aggro/engage
        //      the hero until this wall-clock time, so the hero recovers instead of being
        //      re-fought the instant it warps home. Set by BattleArena.Resolve on a loss.
        //  Both are honored at the TOP of Update() and Engage() so the loop breaks no matter
        //  the exact rep state. Tunables are named consts.
        // -------------------------------------------------------------------------
        private const float PostLossGraceSeconds = 3.5f;   // owner ~3-4s recovery window after a loss

        private static bool  _battlePaused;     // true while a BattleArena fight is staged
        private static float _noEngageUntil;    // Time.time before which no rep may aggro/engage

        /// <summary>Freeze every home-scene rep (roam/chase/aggro/engage) for the duration of a
        /// staged battle. Called by BattleArena.BeginEncounter. Idempotent.</summary>
        public static void PauseAll() => _battlePaused = true;

        /// <summary>Resume home-scene reps after a battle resolves. Called by BattleArena.Resolve.</summary>
        public static void ResumeAll() => _battlePaused = false;

        /// <summary>Open a post-loss re-aggro grace window: no rep may aggro/engage the hero until
        /// now + <paramref name="seconds"/> (defaults to the tuned PostLossGraceSeconds). Called by
        /// BattleArena.Resolve on a LOSS so the hero is not instantly re-engaged.</summary>
        public static void BeginPostLossGrace(float seconds = PostLossGraceSeconds)
            => _noEngageUntil = Time.time + Mathf.Max(0f, seconds);

        /// <summary>True while a battle is staged OR the post-loss grace window is open — no rep
        /// may aggro/engage the hero. Read by the aggro check.</summary>
        private static bool EngagementSuppressed => _battlePaused || Time.time < _noEngageUntil;

        /// <summary>
        /// LOSS cleanup: immediately remove any still-live home rep whose GameObject name matches
        /// <paramref name="repId"/> (the EncounterParams.RepId that triggered the fight). The
        /// triggering rep is normally Destroy()'d in Engage(), but a queued Destroy can race the
        /// loss-resolve warp and leave the hero inside its aggro on return — so we DestroyImmediate
        /// any survivor here to guarantee it is gone. Guarded; never throws into Resolve.
        /// </summary>
        public static void DespawnRepImmediate(string repId)
        {
            if (string.IsNullOrEmpty(repId)) return;
            Guard.Try("Encounter", "loss rep despawn", () =>
            {
                var watchers = FindObjectsOfType<RepEngageWatcher>();
                if (watchers == null) return;
                foreach (var w in watchers)
                {
                    if (w == null || w.gameObject == null) continue;
                    if (w.gameObject.name != repId) continue;
                    FlowTrace.Step("Encounter", $"DespawnRepImmediate: removing lingering rep '{repId}' on loss (kills the instant re-engage).");
                    DestroyImmediate(w.gameObject);
                }
            });
        }

        private Vector3 _leashCenter;           // spawn point -- centre of the wander leash
        private float   _roamRepathAt;          // next time to pick a new roam point

        // CONTACT ENGAGE (owner 2026-06-27): the battle triggers on near-CONTACT with the HERO,
        // not the old generous 2.6m EngageRange. touchDist = heroRadius + repRadius + 0.2f, resolved
        // once from the actual colliders (CharacterController / CapsuleCollider / NavMeshAgent radius).
        // Falls back to a small 0.7m constant (NOT 2.6f) if a radius can't be read. Cached after the
        // first successful resolve. Aggro/chase still use AggroRange — ONLY engage becomes contact.
        private const float TouchPadding      = 0.2f;   // owner: "+.2f difference radius"
        private const float TouchFallbackDist = 0.7f;   // used only if a collider radius can't be resolved
        private float _touchDist = -1f;                 // <0 until resolved from real colliders

        public void Init(string[] family, int threat)
        {
            _family = (family != null && family.Length > 0) ? family : new[] { "orc-warrior" };
            _threat = Mathf.Max(1, threat);
            _enemy = GetComponent<Enemy>();
            _leashCenter = transform.position;                    // wander leash centred on the spawn
            // FIELD-KILL DECOUPLE (owner 2026-06-28): damage no longer auto-engages the arena. With
            // RangedHitsEngage=false the rep can be WHITTLED DOWN and KILLED in the open world by ranged
            // attacks; only near-CONTACT with the hero (TouchDistance in Update) starts the BattleArena.
            // Flip the const true to restore the old "any hit pops the fight" hook behaviour.
            if (RangedHitsEngage && _enemy != null) _enemy.Damaged += OnRepDamaged;   // hero attacked the rep -> engage
        }

        // OWNER-TUNABLE hook: when true, ANY damage to the rep (incl. a ranged hit) instantly engages
        // the arena (the legacy un-killable-hook behaviour). When false (V1 default), ranged hits damage
        // the rep normally so it can be field-killed for full XP+loot; only contact starts the fight.
        private const bool RangedHitsEngage = false;

        private void OnDestroy()
        {
            if (RangedHitsEngage && _enemy != null) _enemy.Damaged -= OnRepDamaged;
        }

        private void OnRepDamaged(Vector3 _) => Engage("hero-attacked-rep");

        private void Update()
        {
            if (_engaged || !FeatureFlags.OverworldEncounter) return;

            // CHAIN ISOLATION (owner 2026-06-30): reps are DontDestroyOnLoad and survive a single-load
            // into the dungeon chain (Outpost1/Dungeon/Outpost2). They must NOT roam/aggro/engage there
            // — a rep staging a BattleArena in a single-loaded scene is what caused the Village2
            // SpawnFamily NRE / WarpHero-off-mesh errors. Only act while OuterWorld is actually loaded
            // (the reps' home region); in a chain scene OuterWorld is unloaded so they stay inert.
            if (!OverworldEncounterSpawner.OuterWorldLoaded()) return;

            // BATTLE ISOLATION + POST-LOSS GRACE: while a fight is staged, or during the brief
            // post-loss recovery window, EVERY home rep freezes — no roam/chase/aggro/engage.
            // This kills the home-combat rumble bleed, the instant re-engage loop, and the
            // double-sim choppiness. The rep simply holds until the gate clears.
            if (EngagementSuppressed) return;

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
                // THREAT CUE (encounter feedback): raise a visible "!" nameplate over the rep the
                // instant it aggros, so the player connects "that orc is hunting me -> contact starts
                // the fight" — the missing pre-engage telegraph. Pairs the audio sting with a visual.
                RaiseThreatCue();
                FlowTrace.Step("Encounter", "rep aggro -> chase sting + threat nameplate ('they see us').");
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

            // CONTACT ENGAGE (owner 2026-06-27): proximity battle fires only at near-TOUCH of the
            // HERO (heroR+repR+0.2f), not the old generous EngageRange. Aggro/chase above are
            // UNCHANGED — only this engage threshold became contact-based.
            float touchDist = TouchDistance(hero);
            if (d <= touchDist) Engage($"rep-touched-hero d={d:0.0}m touch={touchDist:0.0}m");
        }

        // THREAT CUE (encounter feedback): build a world-space "!" alert + foe name floating above
        // the rep when it aggros, so the rep reads as a THREAT pre-engage. A child of the rep, so it
        // moves with it and is auto-destroyed when Engage() Destroy()s the rep (no manual cleanup).
        // Billboard-faced to the camera. Presentation only; reuses the legacy uGUI + Billboard.
        private void RaiseThreatCue()
        {
            if (_threatCue != null) return;
            Guard.Try("Encounter", "rep threat nameplate", () =>
            {
                var root = new GameObject("RepThreatCue");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(0f, 3.0f, 0f);   // above a ~2m orc
                root.transform.localScale = Vector3.one * 0.01f;            // world-space UI scale

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var crt = canvas.GetComponent<RectTransform>();
                crt.sizeDelta = new Vector2(260f, 150f);                    // -> ~2.6 x 1.5 world units
                root.AddComponent<DeNelle.Village.UI.Billboard>();         // keep it facing the camera

                var panel = AddCuePanel(canvas.transform, new Vector2(260f, 150f), new Color(0.08f, 0.02f, 0.02f, 0.78f));

                var bang = AddCueText(panel.transform, "!", 96, new Color(0.95f, 0.25f, 0.20f), TextAnchor.UpperCenter);
                var br = bang.rectTransform;
                br.anchorMin = new Vector2(0f, 0.35f); br.anchorMax = new Vector2(1f, 1f);
                br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;

                var foeLabel = AddCueText(panel.transform, FoeName(), 34, new Color(0.95f, 0.85f, 0.40f), TextAnchor.LowerCenter);
                var nr = foeLabel.rectTransform;
                nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 0.35f);
                nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;

                _threatCue = root;
                FlowTrace.Step("Encounter", $"threat nameplate raised on rep '{gameObject.name}' ('! {FoeName()}').");
            });
        }

        // A player-facing label for the rep's family (ASCII-only, legacy runtime font). An all-orc
        // family reads "Orc Warband" (matching the rep DisplayName); else the leader id is humanised.
        private string FoeName()
        {
            if (_family == null || _family.Length == 0) return "Foes";
            foreach (var id in _family)
                if (id != null && id.IndexOf("orc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Orc Warband";
            var lead = _family[0] ?? "Foes";
            lead = lead.Replace('-', ' ').Replace('_', ' ').Trim();
            return lead.Length == 0 ? "Foes" : (char.ToUpperInvariant(lead[0]) + (lead.Length > 1 ? lead.Substring(1) : ""));
        }

        private static Image AddCuePanel(Transform parent, Vector2 size, Color col)
        {
            var go = new GameObject("CuePanel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
            return img;
        }

        private static Text AddCueText(Transform parent, string s, int size, Color col, TextAnchor anchor)
        {
            var go = new GameObject("CueText");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = s; t.fontSize = size; t.color = col; t.alignment = anchor;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // CONTACT-ENGAGE threshold (owner 2026-06-27 "has to TOUCH the hero or +.2f difference
        // radius"): touchDist = heroRadius + repRadius + 0.2f, read from the REAL colliders so the
        // battle only fires at near-contact with the HERO. Cached after the first successful resolve
        // (colliders don't change at runtime). If neither radius can be read, falls back to a small
        // 0.7m constant — never the old generous 2.6m. Pure read; no behavior beyond the threshold.
        private float TouchDistance(GameObject hero)
        {
            if (_touchDist > 0f) return _touchDist;   // cached
            float heroR = ColliderRadius(hero);
            float repR  = ColliderRadius(gameObject);
            if (heroR <= 0f && repR <= 0f)
                return TouchFallbackDist;             // neither resolved yet — don't cache, retry next frame
            float hr = heroR > 0f ? heroR : TouchFallbackDist * 0.5f;
            float rr = repR  > 0f ? repR  : TouchFallbackDist * 0.5f;
            _touchDist = hr + rr + TouchPadding;
            FlowTrace.Step("Encounter",
                $"touchDist resolved for rep '{gameObject.name}': heroR={hr:0.00} repR={rr:0.00} +pad {TouchPadding:0.00} => {_touchDist:0.00}m.");
            return _touchDist;
        }

        // Best-effort horizontal radius of a character: CharacterController, then CapsuleCollider,
        // then NavMeshAgent.radius, then any Collider's bounds extent. Returns 0 if nothing readable.
        private static float ColliderRadius(GameObject go)
        {
            if (go == null) return 0f;
            float r = 0f;
            Guard.Try("Encounter", "collider radius", () =>
            {
                var cc = go.GetComponent<CharacterController>();
                if (cc != null) { r = cc.radius * Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.z); return; }
                var cap = go.GetComponentInChildren<CapsuleCollider>();
                if (cap != null) { r = cap.radius * Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.z); return; }
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) { r = agent.radius; return; }
                var col = go.GetComponentInChildren<Collider>();
                if (col != null) { r = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z); }
            });
            return r;
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

        private void Engage(string cause)
        {
            if (_engaged) return;
            if (BattleArena.Instance != null && BattleArena.Instance.BattleInProgress) return;
            // Honor the battle-pause + post-loss grace here too: OnRepDamaged (the hero hit the
            // rep) routes straight to Engage and bypasses Update's gate, so the same suppression
            // must hold or a single stray swing re-starts the fight inside the grace window.
            if (EngagementSuppressed) return;
            _engaged = true;

            // TRIGGER PROOF (instrumentation only — no behavior change): capture exactly WHY this
            // battle began — which rep, the hero distance, whether the hero attacked vs proximity,
            // and the suppression/flag state — so the next F8 capture pinpoints the cause.
            var heroGo = GameObject.FindWithTag("Player");
            float heroDist = heroGo != null ? Vector3.Distance(heroGo.transform.position, transform.position) : -1f;
            FlowTrace.Step("Encounter",
                $"TRIGGER cause='{cause}' rep='{gameObject.name}' heroDist={heroDist:0.0}m " +
                $"aggroRange={AggroRange} engageRange={EngageRange} touchDist={(_touchDist > 0f ? _touchDist : -1f):0.00}m " +
                $"heroAttacked={cause.StartsWith("hero-attacked")} " +
                $"suppressed={EngagementSuppressed} ff={FeatureFlags.OverworldEncounter}");

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
