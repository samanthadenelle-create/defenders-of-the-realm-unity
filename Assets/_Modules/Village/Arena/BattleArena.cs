// =============================================================================
// BattleArena — the GENERIC isolated real-time battle controller (WO-482).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Owner directive 2026-06-23: "redo arena as a more generic class to handle both"
// + "no mapping of structures in battle, just a large enough arena to fight and
// kite." This is the GENERIC battle spine the overworld ENCOUNTER (PvE, this file's
// BeginEncounter) drives; the verified async-PvP ArenaMode keeps working untouched
// and is generalized ONTO this spine as a SEPARATE, regression-guarded follow-up
// (generalize-by-extraction, never rewrite the verified path in the risky step).
//
// THE LOOP (PvE encounter): engage -> build an OPEN kite arena (a large bounded
// floor + runtime NavMesh, NO fort/structures) staged at a far offset so it is
// isolated from the open world (which stays in memory, the owner's additive/keep-
// in-memory intent) -> warp the hero in (south) + spawn the enemy family (north)
// via the SHARED EnemyFactory + EnemyBrain roles -> REAL-TIME fight via the EXISTING
// combat stack (PlayerAttackController / HeroAbilities / hero-aggro DEF-224 /
// HeroHealth -- ZERO new combat code) -> WIN (all enemies dead) / LOSE (hero down)
// / FLEE -> reward + warp the hero back to the engagement spot -> OnBattleEnded.
//
// LOGIC vs PRESENTATION (HP-B2B law): this controller is LOGIC (build/spawn/watch/
// resolve/return + which abilities the skill tree allows). Models/anim/VFX/HUD are
// the PRESENTATION layer it reuses (EnemyFactory skins, HeroAbilities VFX, the HUD
// bridge) -- it never bakes presentation in.
//
// REUSE (CLAUDE.md "use items we have"): ArenaNavMeshBaker (runtime NavMesh),
// EnemyFactory/EnemyBrain (the orc family), BattleLock (input gate), CoreServices.
// Audio (BGM), HeroHealth/HeroLocomotion (hero), ArenaHudBridge (HUD show/hide).
//
// Instrumented per CLAUDE.md S12 (FlowTrace "BattleArena") so a HEADLESS run --
// which this isolated design makes fully self-contained -- pinpoints any dead step.
// ASCII-only logs; LogWarning, never error. Flag-gated by FeatureFlags.OverworldEncounter.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core;
using DeNelle.Core.Audio;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Generic real-time battle controller. PvE entry: <see cref="BeginEncounter"/>.
    /// One battle at a time; a runtime singleton the engage trigger drives.
    /// </summary>
    public sealed class BattleArena : MonoBehaviour
    {
        // Stage the arena FAR from the world origin so it is spatially isolated from
        // the open world (which stays loaded in memory -> cheap return). The global
        // skybox/ambient persist, so the backdrop still "matches where you were".
        private static readonly Vector3 ArenaCentre = new Vector3(5000f, 0f, 5000f);

        // Open kite arena footprint (owner doc ~28-35 x 18-22) -- big enough to kite.
        private const float ArenaHalfWidth = 30f;   // X half-extent (~60 wide) — owner 2026-06-23: bigger to KITE + lure one away
        private const float ArenaHalfDepth = 24f;   // Z half-extent (~48 deep) — open kite space, not a square

        private const float BattleTimeoutSeconds = 240f; // generous; a stuck fight ends, never soft-locks

        private static BattleArena _instance;

        /// <summary>The live BattleArena (creates a persistent host on first access).</summary>
        public static BattleArena Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("BattleArena");
                    DontDestroyOnLoad(host);
                    _instance = host.AddComponent<BattleArena>();
                }
                return _instance;
            }
        }

        /// <summary>True while a battle is staged (blocks a second start + locks panels/hotkeys).</summary>
        public bool BattleInProgress { get; private set; }

        /// <summary>Raised when a battle resolves: (params, won).</summary>
        public event Action<EncounterParams, bool> OnBattleEnded;

        private Func<bool> _battleProbe;
        private GameObject _arenaRoot;
        private readonly List<Enemy> _liveEnemies = new List<Enemy>();
        private EncounterParams _current;
        private bool _resolved;
        private BattleArenaHud _hud;
        private FamilyLeader _familyLeader;   // WO-146 MonsterFamily — the orc pack's leader
        private bool _familyEngaged;          // disbanded-on-arrival latch (formation -> real 1vN)
        private string _activeBiome;          // WO-499 resolved biome (backdrop + particles)
        private ArenaDeathCam _deathCam;      // WO-493 #4 climactic death-camera hold

        // WO-493 #4: the dying actor to linger on for the climactic death-cam (the LAST enemy
        // killed, or the hero on a loss). Captured at the resolving moment so the camera frames
        // the right body even as _liveEnemies empties / the hero ragdolls.
        private Transform _climaxBody;

        // Cavern mood: saved RenderSettings to restore on Resolve so the open world is untouched.
        private bool _moodSaved;
        private bool _savedFog;
        private Color _savedFogColor;
        private float _savedFogDensity;
        private Color _savedAmbientLight;
        private float _savedAmbientIntensity;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _battleProbe = () => BattleInProgress;
            BattleLock.RegisterProbe(_battleProbe);
        }

        private void OnDestroy()
        {
            BattleLock.UnregisterProbe(_battleProbe);
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Start the PvE encounter described by <paramref name="p"/>. Returns false (no
        /// stage) if a battle is already running, the params/family are empty, or the
        /// feature flag is off. The hero is warped into the arena; on resolve it is warped
        /// back to <see cref="EncounterParams.ReturnPosition"/>.
        /// </summary>
        public bool BeginEncounter(EncounterParams p)
        {
            if (BattleInProgress) { Debug.LogWarning("[BattleArena] a battle is already in progress - ignored."); return false; }
            if (p == null || p.EnemyIds == null || p.EnemyIds.Length == 0)
            {
                Debug.LogWarning("[BattleArena] null/empty EncounterParams - ignored.");
                return false;
            }
            if (!FeatureFlags.OverworldEncounter)
            {
                Debug.LogWarning("[BattleArena] ff.overworldencounter OFF - encounter suppressed.");
                return false;
            }

            _current = p;
            _resolved = false;
            _climaxBody = null;
            BattleInProgress = true;
            FlowTrace.Step("BattleArena", $"BeginEncounter: family=[{string.Join(",", p.EnemyIds)}] threat={p.Threat} theme='{p.BackdropContext}' return='{p.ReturnScene}'.");
            StartCoroutine(StageRoutine(p));
            return true;
        }

        // ---------------------------------------------------------------------
        //  Stage: build arena -> bake navmesh -> warp hero -> spawn family -> watch
        // ---------------------------------------------------------------------
        private IEnumerator StageRoutine(EncounterParams p)
        {
            // 1) Build the open kite arena (floor + boundary) at the far offset.
            BuildArena(p.BackdropContext);

            // 2) Runtime-bake a local NavMesh over the arena floor (REUSE ArenaNavMeshBaker:
            //    it adds a walkable plane + a NavMeshSurface and BuildNavMesh()es over the
            //    children colliders). The far-offset arena has no pre-baked mesh, so this is
            //    the genuine need the baker was built for (the WO-388 castle path).
            var baker = _arenaRoot.AddComponent<ArenaNavMeshBaker>();
            Guard.Try("BattleArena", "bake arena navmesh", () => baker.BakeForCastle(_arenaRoot.transform));
            // Give the (synchronous) bake + the floor realize a couple frames to settle.
            yield return null;
            yield return null;

            // 3) Warp the hero to the SOUTH stance, facing north toward the enemies.
            Vector3 heroStance = ArenaCentre + new Vector3(0f, 0f, -ArenaHalfDepth + 2f);
            WarpHero(heroStance, Quaternion.LookRotation(Vector3.forward));

            // 4) Spawn the enemy FAMILY across the NORTH side (loose formation, 1..6).
            SpawnFamily(p);

            if (_liveEnemies.Count == 0)
            {
                // Nothing staged -> abort cleanly rather than a phantom win.
                FlowTrace.Fail("BattleArena", "StageRoutine: no enemies spawned - aborting encounter (no phantom win).");
                Resolve(false);
                yield break;
            }

            // 5) Present: battle HUD + combat BGM. (Presentation layer; logic already staged.)
            Guard.Try("BattleArena", "show combat HUD", () => ArenaHudBridge.SetVisible(true));
            Guard.Try("BattleArena", "build battle overlay", () =>
            {
                _hud = BattleArenaHud.Create();
                _hud.SetFleeHandler(Flee);
                _hud.SetPrimary("Orc Warband", 1f, _liveEnemies.Count);
            });
            CoreServices.Audio?.PlayMusic(MusicTrack.Arena);

            FlowTrace.Step("BattleArena", $"StageRoutine: staged {_liveEnemies.Count} enemies; fight live.");

            // 6) Watch to resolution.
            yield return StartCoroutine(WatchToResolution());
        }

        // Build a large bounded floor (+ invisible boundary walls) at the arena centre.
        // NO structures (owner: "no mapping of structures, just a large enough arena").
        private void BuildArena(string theme)
        {
            _arenaRoot = new GameObject("[BattleArena_Stage]");
            _arenaRoot.transform.position = ArenaCentre;

            // WO-499 #3 danger gradient: resolve the BIOME from the context + threat so the
            // backdrop SIGNALS the fight (forest=easy ... volcanic=hard family ... castle=tanky).
            // Ground/edge/cavern-mood still key off the raw 'theme' (their existing keys); the
            // biome only drives the painted backdrop + the per-biome particles.
            int threat = _current != null ? _current.Threat : 0;
            _activeBiome = ArenaBiomeDressing.ResolveBiome(theme, threat);

            // Floor: a scaled primitive plane (10x10 units at scale 1) -> cover the footprint.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(_arenaRoot.transform, false);
            floor.transform.localScale = new Vector3((ArenaHalfWidth * 2f) / 10f + 0.4f, 1f, (ArenaHalfDepth * 2f) / 10f + 0.4f);
            ApplyGroundTheme(floor, theme);

            // Invisible boundary walls so neither hero nor enemy can wander off the stage.
            // (The NavMesh already confines agents; the walls are belt-and-braces + block
            //  the off-mesh hero-translation fallback.)
            BuildWall(new Vector3(0f,  2f,  ArenaHalfDepth + 0.5f), new Vector3(ArenaHalfWidth * 2f + 2f, 6f, 1f));
            BuildWall(new Vector3(0f,  2f, -ArenaHalfDepth - 0.5f), new Vector3(ArenaHalfWidth * 2f + 2f, 6f, 1f));
            BuildWall(new Vector3( ArenaHalfWidth + 0.5f, 2f, 0f),  new Vector3(1f, 6f, ArenaHalfDepth * 2f + 2f));
            BuildWall(new Vector3(-ArenaHalfWidth - 0.5f, 2f, 0f),  new Vector3(1f, 6f, ArenaHalfDepth * 2f + 2f));

            // Natural see-through edge OUTSIDE the walls (silhouette only, colliders stripped).
            DressArenaEdge(theme);

            // THE WOW (WO-499): a painted biome backdrop ringing the arena behind the treeline. Skip-safe.
            BuildBackdrop(_activeBiome);

            // WO-499 #2: subtle per-biome particles (leaves/motes/embers/dust + mist) parented to the
            // stage so they tear down with it. Cheap, short-lived, capped -> "effects clear out fast".
            ArenaBiomeDressing.BuildParticles(_arenaRoot.transform, _activeBiome);

            // Cavern ONLY: dim the persisted sky/ambient/fog to a stone-cave mood (restored on Resolve).
            // Default (outerworld/castle) leaves the persisted dawn sky untouched -- it already matches.
            if ((theme ?? "outerworld").ToLowerInvariant() == "cavern")
                ApplyCavernMood();

            FlowTrace.Step("BattleArena", $"BuildArena: open kite floor {ArenaHalfWidth * 2f}x{ArenaHalfDepth * 2f} at {ArenaCentre} (theme '{theme}', no structures).");
        }

        // ---------------------------------------------------------------------
        //  Natural arena edge: a jittered ring of low-poly props OUTSIDE the walls.
        // ---------------------------------------------------------------------
        // Silhouette-only treeline / boulders that ring the kite space so the arena reads as
        // a clearing, not a plane in a void. Parented to _arenaRoot (auto torn down with the
        // stage), colliders STRIPPED (never catch the kiting hero), deterministic seed.
        private void DressArenaEdge(string theme)
        {
            string key = (theme ?? "outerworld").ToLowerInvariant();

            // Per-theme prop set (Resources/Arena/<fbx name>); empty/sparse for stone themes.
            string[] props;
            int count;
            switch (key)
            {
                case "cavern":
                    props = new[] { "Rock_1_A_Color1", "Rock_2_C_Color1", "Rock_3_E_Color1", "Rock_1_J_Color1" };
                    count = 16;
                    break;
                case "castle":
                    props = new[] { "Rock_1_A_Color1", "Rock_2_C_Color1" };
                    count = 8;   // sparse/bare around a keep
                    break;
                default: // outerworld -- a soft treeline + scattered boulders
                    props = new[] { "Tree_2_A_Color1", "Tree_5_C_Color1", "Tree_7_A_Color1",
                                    "Tree_Bare_1_A_Color1", "Rock_1_A_Color1", "Rock_3_E_Color1" };
                    count = 18;
                    break;
            }
            if (props == null || props.Length == 0) return;

            count = Mathf.Clamp(count, 0, 20); // mobile-light cap

            // Deterministic seed off the theme so a given region always rings the same (autopilot-chaos memory).
            var rng = new System.Random(key.GetHashCode());

            var edge = new GameObject("[ArenaEdge]");
            edge.transform.SetParent(_arenaRoot.transform, false);

            float ringHalfX = ArenaHalfWidth + 4.5f;  // OUTSIDE the invisible walls (ArenaHalf+3..6)
            float ringHalfZ = ArenaHalfDepth + 4.5f;

            for (int i = 0; i < count; i++)
            {
                // Even angular spacing + jitter -> a natural, non-gridded ring.
                float baseAng = (i / (float)count) * Mathf.PI * 2f;
                float ang = baseAng + (float)(rng.NextDouble() - 0.5) * 0.35f;
                float radJitter = (float)rng.NextDouble() * 1.5f; // 0..1.5m outward jitter
                float x = Mathf.Cos(ang) * (ringHalfX + radJitter);
                float z = Mathf.Sin(ang) * (ringHalfZ + radJitter);

                string name = props[rng.Next(props.Length)];
                GameObject prefab = null;
                Guard.Try("BattleArena", "load edge prop '" + name + "'", () =>
                {
                    prefab = Resources.Load<GameObject>("Arena/" + name);
                });
                if (prefab == null)
                {
                    Debug.LogWarning("[BattleArena] edge prop 'Arena/" + name + "' not found - skipped.");
                    continue;
                }

                Guard.Try("BattleArena", "place edge prop '" + name + "'", () =>
                {
                    var go = UnityEngine.Object.Instantiate(prefab, edge.transform);
                    go.name = "Edge_" + name + "_" + i;
                    go.transform.localPosition = new Vector3(x, 0f, z);
                    go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float s = 0.9f + (float)rng.NextDouble() * 0.6f; // 0.9..1.5 scale variety
                    go.transform.localScale = new Vector3(s, s, s);
                    StripColliders(go);
                });
            }

            FlowTrace.Step("BattleArena", "DressArenaEdge: ringed '" + key + "' with up to " + count + " silhouette props.");
        }

        // Strip every collider so an edge prop is a pure silhouette (never blocks the kite/navmesh).
        // THE WOW (WO-499): a painted biome backdrop ringing the arena behind the treeline. Loads
        // Resources/Arena/Backdrops/<theme>_backdrop onto 4 inward-facing UNLIT quads (cyclorama) so any
        // camera angle shows a painted horizon. Unlit = glows like a matte painting; fog fades the seams.
        // Skip-safe: no texture -> keep the persisted sky. Parented to _arenaRoot (auto teardown).
        private void BuildBackdrop(string theme)
        {
            Guard.Try("BattleArena", "build backdrop", () =>
            {
                string key = (theme ?? ArenaBiomeDressing.Forest).ToLowerInvariant();
                var tex = Resources.Load<Texture2D>("Arena/Backdrops/" + key + "_backdrop");
                if (tex == null) tex = Resources.Load<Texture2D>("Arena/Backdrops/forest_backdrop");
                if (tex == null) tex = Resources.Load<Texture2D>("Arena/Backdrops/outerworld_backdrop");
                if (tex == null)
                {
                    FlowTrace.Step("BattleArena", "BuildBackdrop: no backdrop texture for '" + key + "' -> skip (persisted sky kept).");
                    return;
                }
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Texture");
                if (sh == null)
                {
                    // Build-strip guard: the unlit shader was dropped from the player (no baked/
                    // Always-Included reference). Degrade to "no backdrop" — keep the persisted sky —
                    // never throw on `new Material(null)`. Durable fix: AlwaysIncludedShaders helper.
                    FlowTrace.Warn("BattleArena", "BuildBackdrop: unlit shader missing from build -> skipping backdrop (sky kept).");
                    return;
                }
                var mat = new Material(sh) { name = "ArenaBackdrop_" + key };
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

                float r = Mathf.Max(ArenaHalfWidth, ArenaHalfDepth) + 16f;   // behind the treeline ring
                float h = 60f;
                var root = new GameObject("ArenaBackdrop");
                root.transform.SetParent(_arenaRoot.transform, false);
                root.transform.localPosition = new Vector3(0f, h * 0.32f, 0f);

                Vector3[] poss = { new Vector3(0f, 0f, r), new Vector3(0f, 0f, -r), new Vector3(r, 0f, 0f), new Vector3(-r, 0f, 0f) };
                float[] yaws = { 180f, 0f, -90f, 90f };
                for (int i = 0; i < 4; i++)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Backdrop_" + i;
                    q.transform.SetParent(root.transform, false);
                    q.transform.localPosition = poss[i];
                    q.transform.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
                    q.transform.localScale = new Vector3(r * 2.4f, h, 1f);
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    StripColliders(q);
                }
                FlowTrace.Step("BattleArena", "BuildBackdrop: cyclorama backdrop '" + key + "' behind the treeline (r=" + r.ToString("0") + ").");
            });
        }

        private static void StripColliders(GameObject go)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) UnityEngine.Object.Destroy(cols[i]);
        }

        // Save the persisted RenderSettings then dim them to a stone-cave mood (cavern only).
        // Null-safe + one-shot (a second call without restore is ignored so we never clobber the save).
        private void ApplyCavernMood()
        {
            if (_moodSaved) return;
            Guard.Try("BattleArena", "save+apply cavern mood", () =>
            {
                _savedFog = RenderSettings.fog;
                _savedFogColor = RenderSettings.fogColor;
                _savedFogDensity = RenderSettings.fogDensity;
                _savedAmbientLight = RenderSettings.ambientLight;
                _savedAmbientIntensity = RenderSettings.ambientIntensity;
                _moodSaved = true;

                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.10f, 0.10f, 0.13f);
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.02f;
                RenderSettings.ambientLight = new Color(0.18f, 0.17f, 0.22f);
                RenderSettings.ambientIntensity = 0.55f;
                FlowTrace.Step("BattleArena", "ApplyCavernMood: dim stone-cave fog/ambient set (saved for restore).");
            });
        }

        // Restore the saved RenderSettings on Resolve so the open world is untouched on return.
        private void RestoreCavernMood()
        {
            if (!_moodSaved) return;
            Guard.Try("BattleArena", "restore cavern mood", () =>
            {
                RenderSettings.fog = _savedFog;
                RenderSettings.fogColor = _savedFogColor;
                RenderSettings.fogDensity = _savedFogDensity;
                RenderSettings.ambientLight = _savedAmbientLight;
                RenderSettings.ambientIntensity = _savedAmbientIntensity;
                FlowTrace.Step("BattleArena", "RestoreCavernMood: open-world RenderSettings restored.");
            });
            _moodSaved = false;
        }

        private void BuildWall(Vector3 localPos, Vector3 size)
        {
            var wall = new GameObject("ArenaBound");
            wall.transform.SetParent(_arenaRoot.transform, false);
            wall.transform.localPosition = localPos;
            var box = wall.AddComponent<BoxCollider>();
            box.size = size;
            // No renderer -> invisible boundary.
        }

        // Tracks whether the textured-ground null fallback has already warned (once per session).
        private static bool _groundFallbackWarned;

        // Ground theme: lay the SOURCE REGION's real textured material on the floor so the
        // arena reads as an extension of where you stood (grass / dwarven-stone / sharp-stone),
        // tiled across the big plane. Skip-safe: a null Resources.Load keeps today's per-theme
        // Color tint exactly (LogWarning once) so a missing asset NEVER breaks the fight.
        private static void ApplyGroundTheme(GameObject floor, string theme)
        {
            var r = floor.GetComponent<Renderer>();
            if (r == null) return;

            string key = (theme ?? "outerworld").ToLowerInvariant();

            // theme -> Resources/Arena/<name> ground material (copied into Resources for runtime load).
            string matName;
            switch (key)
            {
                case "castle": matName = "Arena/Dwarven_Ground"; break;
                case "cavern": matName = "Arena/Floor_Sharp_Stones"; break;
                default:       matName = "Arena/Grass_1"; break;
            }

            Material loaded = null;
            Guard.Try("BattleArena", "load ground material '" + matName + "'", () =>
            {
                loaded = Resources.Load<Material>(matName);
            });

            if (loaded != null)
            {
                // Instance the shared material so tiling on the big plane does not mutate the asset.
                var inst = new Material(loaded) { name = "ArenaGround_" + key };
                if (inst.HasProperty("_BaseMap")) inst.SetTextureScale("_BaseMap", new Vector2(12f, 10f));
                if (inst.HasProperty("_MainTex")) inst.SetTextureScale("_MainTex", new Vector2(12f, 10f));
                r.sharedMaterial = inst;
                FlowTrace.Step("BattleArena", "ApplyGroundTheme: textured ground '" + matName + "' (theme '" + key + "').");
                return;
            }

            // Fallback: keep the original per-theme flat tint exactly as before.
            if (!_groundFallbackWarned)
            {
                _groundFallbackWarned = true;
                Debug.LogWarning("[BattleArena] ground material '" + matName + "' not found in Resources - using flat tint fallback.");
            }
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            Color c;
            switch (key)
            {
                case "castle": c = new Color(0.55f, 0.55f, 0.60f); break;   // stone
                case "cavern": c = new Color(0.34f, 0.30f, 0.36f); break;   // cave
                default:       c = new Color(0.40f, 0.52f, 0.30f); break;   // grassy overworld
            }
            var m = new Material(sh) { name = "ArenaGround_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            r.sharedMaterial = m;
        }

        // Spawn the orc family across the NORTH side as a BUILT MonsterFamily (WO-146): index 0 is
        // the FamilyLeader, the rest are FamilyMembers in formation. The pack APPROACHES the hero in
        // formation (the pivot's animated "led pack" feel), then disbands on arrival (WatchToResolution)
        // so every member fights the real 1vN (kite + peel one at a time, per the design doc). Reuses
        // the canonical FamilyTestSpawner pattern + EnemyFactory (the single spawn path, CLAUDE.md §9).
        private void SpawnFamily(EncounterParams p)
        {
            _liveEnemies.Clear();
            _familyLeader = null;
            _familyEngaged = false;
            Transform heart = _arenaRoot.transform; // arena-centre tether; hero-aggro (DEF-224) pulls them to the hero
            int n = Mathf.Clamp(p.EnemyIds.Length, 1, 6);

            for (int i = 0; i < n; i++)
            {
                string id = p.EnemyIds[i];
                EnemyDef def = BuildEncounterDef(id, p.Threat);

                // North side, spread on X; leader (i==0) a touch forward toward the hero.
                float spread = (n <= 1) ? 0f : Mathf.Lerp(-ArenaHalfWidth + 3f, ArenaHalfWidth - 3f, i / (float)(n - 1));
                float z = ArenaHalfDepth - 2f - (i == 0 ? 1.5f : 0f);
                Vector3 pos = ArenaCentre + new Vector3(spread, 0f, z);
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas)) pos = hit.position;

                Vector3 toHero = (ArenaCentre + new Vector3(0f, 0f, -ArenaHalfDepth)) - pos; toHero.y = 0f;
                Quaternion rot = toHero.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toHero) : Quaternion.identity;

                int idx = i;
                Enemy enemy = null;
                Guard.Try("BattleArena", $"spawn '{id}'", () =>
                {
                    enemy = EnemyFactory.Build(def, pos, rot, _arenaRoot.transform);
                    if (enemy == null) return;
                    enemy.gameObject.name = $"ArenaEnemy_{id}_{idx}";
                    enemy.Configure($"encounter-{id}-{idx}", def, heart);
                    var brain = enemy.gameObject.AddComponent<EnemyBrain>();
                    brain.Role = RoleForId(id);
                    // MonsterFamily wiring: first unit leads; the rest follow in formation.
                    if (idx == 0)
                        _familyLeader = enemy.gameObject.AddComponent<FamilyLeader>();
                    else if (_familyLeader != null)
                        _familyLeader.RegisterMember(enemy.gameObject.AddComponent<FamilyMember>());
                });

                if (enemy != null)
                {
                    _liveEnemies.Add(enemy);
                    enemy.Died += HandleEnemyDied;
                    FlowTrace.Step("BattleArena", $"SpawnFamily: '{id}' (role {RoleForId(id)}) at {pos}{(idx == 0 ? " [LEADER]" : " [follower]")}.");
                }
            }
        }

        // The pack approaches in formation; once the LEADER reaches the hero, disband the family so
        // every member breaks to fight (FamilyLeader.OnDisable -> Disband -> members StopFollowing ->
        // their EnemyBrain re-enables -> all engage via hero-aggro). One-shot.
        private void MaybeDisbandOnArrival()
        {
            if (_familyEngaged || _familyLeader == null) return;
            var heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return;
            if (Vector3.Distance(_familyLeader.transform.position, heroGo.transform.position) <= 6f)
            {
                _familyEngaged = true;
                _familyLeader.enabled = false;   // triggers Disband(): the pack breaks to fight
                FlowTrace.Step("BattleArena", "family reached the hero -> DISBAND (formation -> 1vN melee).");
            }
        }

        // Map a family id -> an EnemyBrain role (logic). The orc family: leader=DPS,
        // tank=Tank, mage=Ranged. Unknown ids default to DPS.
        private static EnemyRole RoleForId(string id)
        {
            string s = (id ?? "").ToLowerInvariant();
            if (s.Contains("tank")) return EnemyRole.Tank;
            if (s.Contains("mage") || s.Contains("caster") || s.Contains("shaman")) return EnemyRole.Ranged;
            if (s.Contains("heal") || s.Contains("acolyte")) return EnemyRole.Healer;
            return EnemyRole.DPS;
        }

        // Synthesise a code EnemyDef for an encounter id (the orc family ids are not in
        // enemies.json -- same forward-design pattern as RegionMobSpawner.BuildRoamerDef).
        // Stats mirror the ATB engine orc defs (Defs.ENEMY_DEFS) so the two stay coherent;
        // threat lightly scales HP/damage.
        private static EnemyDef BuildEncounterDef(string id, int threat)
        {
            float t = 1f + Mathf.Clamp(threat - 1, 0, 20) * 0.08f;   // +8% per threat tier
            string s = (id ?? "").ToLowerInvariant();

            float hp, dmg, spd, atk, height; string display;
            if (s.Contains("tank"))       { display = "Orc Bulwark";    hp = 190; dmg = 18; spd = 2.2f; atk = 1.6f; height = 2.3f; }
            else if (s.Contains("mage"))  { display = "Orc Spiritcaller"; hp = 85; dmg = 21; spd = 3.0f; atk = 1.4f; height = 1.9f; }
            else if (s.Contains("warrior")) { display = "Orc Warleader"; hp = 120; dmg = 24; spd = 3.2f; atk = 1.2f; height = 2.0f; }
            else                          { display = "Orc Raider";     hp = 100; dmg = 16; spd = 3.0f; atk = 1.2f; height = 1.9f; }

            return new EnemyDef
            {
                Id = id, Name = display, DisplayName = display, Ai = "walker",
                Hp = hp * t, MoveSpeed = spd, ContactDamage = dmg * t, AttackInterval = atk,
                Height = height, AggroRadius = 18f,
                XpReward = Mathf.RoundToInt(14 * t), GlimmerReward = Mathf.RoundToInt(3 * t),
            };
        }

        // Warp the hero (by "Player" tag) to a stance. Reuses HeroLocomotion.WarpTo via
        // reflection (BattleArena is DeNelle.Village, but the hero may not be resolvable by
        // type here in all call orders, so a tag + reflection lookup is the safe path that
        // also raises OnTeleported so SmartMobileCamera snaps).
        private static void WarpHero(Vector3 pos, Quaternion rot)
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) { FlowTrace.Warn("BattleArena", "WarpHero: no 'Player' hero found - skipped."); return; }

            var loco = hero.GetComponent("HeroLocomotion") as MonoBehaviour;
            if (loco != null)
            {
                var warp = loco.GetType().GetMethod("WarpTo", new[] { typeof(Vector3), typeof(Quaternion?) });
                if (warp != null) { warp.Invoke(loco, new object[] { pos, (Quaternion?)rot }); FlowTrace.Step("BattleArena", $"WarpHero -> {pos}."); return; }
            }
            hero.transform.SetPositionAndRotation(pos, rot);
            FlowTrace.Warn("BattleArena", "WarpHero: WarpTo not found - used transform fallback.");
        }

        // ---------------------------------------------------------------------
        //  Watch -> resolve
        // ---------------------------------------------------------------------
        private void HandleEnemyDied(Enemy e)
        {
            _liveEnemies.Remove(e);
            // WO-493 #4: remember the BATTLE-WINNING body so the death-cam lingers on the
            // climactic kill (only when this death empties the family). Reserved for the
            // last death -- not every kill.
            if (_liveEnemies.Count == 0 && e != null) _climaxBody = e.transform;
            FlowTrace.Step("BattleArena", $"enemy down; {_liveEnemies.Count} remain.");
        }

        private IEnumerator WatchToResolution()
        {
            float deadline = Time.time + BattleTimeoutSeconds;
            while (!_resolved)
            {
                // Pack approaches in formation, then breaks to fight when it reaches the hero.
                MaybeDisbandOnArrival();

                // WIN: every staged enemy is dead.
                _liveEnemies.RemoveAll(e => e == null || e.IsDead);
                // Push primary-target state to the overlay (presentation; logic owns the values).
                if (_hud != null && _liveEnemies.Count > 0)
                    _hud.SetPrimary(null, _liveEnemies[0] != null ? _liveEnemies[0].HpFraction : 0f, _liveEnemies.Count);
                if (_liveEnemies.Count == 0)
                {
                    // WO-493 #4: linger on the climactic kill (slow-mo) BEFORE teardown/return.
                    yield return StartCoroutine(PlayDeathCam(_climaxBody, slowMo: true));
                    Resolve(true);
                    yield break;
                }

                // LOSE: hero down.
                var hh = HeroHealth.Instance;
                if (hh != null && !hh.IsAlive)
                {
                    FlowTrace.Step("BattleArena", "hero down - loss.");
                    // Linger on the hero's defeat (no slow-mo -- the defeat beat plays at speed).
                    var heroGo = GameObject.FindWithTag("Player");
                    yield return StartCoroutine(PlayDeathCam(heroGo != null ? heroGo.transform : null, slowMo: false));
                    Resolve(false);
                    yield break;
                }

                // Safety: a stuck/AFK fight ends (loss) rather than soft-locking.
                if (Time.time >= deadline) { FlowTrace.Warn("BattleArena", "battle timeout - loss."); Resolve(false); yield break; }

                yield return new WaitForSeconds(0.25f);
            }
        }

        // WO-493 #4: run the climactic death-camera hold and WAIT for it before teardown/return.
        // Skip-safe: a null body or any failure ends instantly (the fight resolves as before, the
        // existing return-to-engagement-spot flow is untouched -- this only inserts a brief linger).
        private IEnumerator PlayDeathCam(Transform body, bool slowMo)
        {
            if (body == null)
            {
                FlowTrace.Warn("BattleArena", "PlayDeathCam: no body to linger on - skipping.");
                yield break;
            }

            if (_deathCam == null)
                Guard.Try("BattleArena", "create death cam", () => { _deathCam = gameObject.AddComponent<ArenaDeathCam>(); });
            if (_deathCam == null) yield break;

            FlowTrace.Step("BattleArena", $"PlayDeathCam: linger on '{body.name}' (slowMo={slowMo}).");
            _deathCam.Hold(body, slowMo);
            // Wait out the linger (safety-capped so a stuck hold never soft-locks the return).
            float guardDeadline = Time.unscaledTime + 7f;
            while (_deathCam.IsHolding && Time.unscaledTime < guardDeadline)
                yield return null;
        }

        /// <summary>Retreat from the battle (Flee button): ends it as a loss + returns. No reward.</summary>
        public void Flee()
        {
            if (!BattleInProgress || _resolved) return;
            FlowTrace.Step("BattleArena", "Flee -> retreat (return to the open world, no reward).");
            Resolve(false);
        }

        // ---------------------------------------------------------------------
        //  Resolve + return (reward, tear down the stage, warp hero home)
        // ---------------------------------------------------------------------
        private void Resolve(bool won)
        {
            if (_resolved) return;
            _resolved = true;
            FlowTrace.Step("BattleArena", $"Resolve: {(won ? "WIN" : "LOSS")}.");

            // Banner (presentation; self-destructs after a beat). Live overlay hides inside ShowResult.
            Guard.Try("BattleArena", "battle result banner", () => _hud?.ShowResult(won));
            _hud = null;

            // REWARD (logic, v1 minimal): XP on a win. Fuller loot (gear/resources) is the
            // EnemyOutpost loot-table reuse follow-up; kept light here so the loop is closed.
            if (won) Guard.Try("BattleArena", "grant win XP", () => GrantWinReward(_current));

            // Restore any cavern mood RenderSettings BEFORE the open world is back in view.
            RestoreCavernMood();

            // Tear the stage down: kill any survivors + destroy the arena root.
            foreach (var e in _liveEnemies) if (e != null) Guard.Try("BattleArena", "despawn enemy", () => Destroy(e.gameObject));
            _liveEnemies.Clear();
            if (_arenaRoot != null) Destroy(_arenaRoot);
            _arenaRoot = null;

            // Warp the hero back to the engagement spot (the open world stayed in memory).
            if (_current != null)
                WarpHero(_current.ReturnPosition, Quaternion.Euler(0f, _current.ReturnYaw, 0f));

            // Restore explore BGM + leave the HUD up for the open world.
            CoreServices.Audio?.PlayMusic(MusicTrack.Overworld);

            var done = _current;
            _current = null;
            BattleInProgress = false;

            OnBattleEnded?.Invoke(done, won);
            FlowTrace.Step("BattleArena", "Resolve: stage torn down, hero returned, battle ended.");
        }

        // Win reward (C2 — close the FELT reward loop): a staged-family/threat-scaled
        // payout the player FEELS, every drop routed to an EXISTING system (no parallel
        // economy — mirrors EnemyOutpost.GrantClearReward):
        //   1) hero XP        -> HeroProgression (kept; reflection, no ref-order assumption)
        //   2) skill points   -> WisdomCurrencyService.Grant (the talent-tree currency)
        //   3) resources      -> EconomyService.Grant (a small wood/iron bundle)
        //   4) gear (chance)  -> GearLoadout.Equip*ById (a low-tier weapon/armor drop)
        // V1-simple + deterministic-ish (formulas, not data files). Cross-module lookups
        // are Unity-fake-null-guarded (explicit != null, not ?.) per the lint.
        private static void GrantWinReward(EncounterParams p)
        {
            if (p == null) return;
            int family = Mathf.Max(0, p.EnemyIds != null ? p.EnemyIds.Length : 0);
            int threat = Mathf.Max(0, p.Threat);

            // 1) XP — unchanged path (HeroProgression via reflection).
            int xp = 20 + 8 * family + 4 * threat;
            var prog = GameObject.FindObjectOfType(Type.GetType("DeNelle.Village.HeroProgression, DeNelle.Village")) as MonoBehaviour;
            if (prog != null)
            {
                var add = prog.GetType().GetMethod("AddXp", new[] { typeof(float) });
                add?.Invoke(prog, new object[] { (float)xp });
            }
            FlowTrace.Step("BattleArena", $"GrantWinReward: +{xp} XP (family={family} threat={threat}).");

            // 2) SKILL POINTS (Wisdom) — 1 base + 1 per 2 family members + 1 per 2 threat
            // tiers, so a bigger/deadlier family pays a felt skill-point bump.
            int wisdom = 1 + family / 2 + threat / 2;
            var wallet = DeNelle.Village.Talents.WisdomCurrencyService.Instance;
            if (wallet != null)
            {
                wallet.Grant(wisdom);
                FlowTrace.Step("BattleArena", $"GrantWinReward: +{wisdom} Wisdom (skill points).");
            }
            else
            {
                FlowTrace.Warn("BattleArena", "GrantWinReward: WisdomCurrencyService null - skill points not granted.");
            }

            // 3) RESOURCES — a small wood/iron bundle via the existing EconomyService
            // (same grant surface EnemyOutpost uses; no new resource path).
            int wood = 10 + 4 * threat;
            int iron = 4 + 2 * threat;
            var econ = EconomyService.Instance;
            if (econ != null)
            {
                econ.Grant(wood: wood, iron: iron);
                FlowTrace.Step("BattleArena", $"GrantWinReward: +{wood} wood, +{iron} iron.");
            }
            else
            {
                FlowTrace.Warn("BattleArena", "GrantWinReward: EconomyService null - resources not granted.");
            }

            // 4) GEAR (chance) — a low-tier drop equipped through the REAL armory API
            // (GearLoadout.Equip*ById), exactly like the outpost loot path but capped at
            // the low tiers so the arena stays a light, frequent reward.
            string gear = TryGrantArenaGear(threat);
            if (gear != null)
                FlowTrace.Step("BattleArena", $"GrantWinReward: gear drop [{gear}] equipped.");
        }

        // Low-tier gear drop for an arena win — reuses the outpost's armory-grant pattern
        // (find the Player-tagged hero's GearLoadout, pick a catalog item the hero qualifies
        // for, equip it) but biased to common/uncommon. Drop chance rises a little with
        // threat. Returns the equipped item's display name, or null on no drop. Fake-null-safe.
        private static string TryGrantArenaGear(int threat)
        {
            const float baseChance = 0.30f;
            const float perTier    = 0.05f;
            const float maxChance  = 0.65f;
            float chance = Mathf.Min(maxChance, baseChance + perTier * Mathf.Max(0, threat));
            if (UnityEngine.Random.value > chance) return null;

            GameObject heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return null;

            var loadout = heroGo.GetComponent<DeNelle.Village.GearLoadout>();
            if (loadout == null) loadout = heroGo.AddComponent<DeNelle.Village.GearLoadout>();
            if (loadout == null) return null;

            var abilities   = heroGo.GetComponent<DeNelle.Village.HeroAbilities>();
            var progression = heroGo.GetComponent<DeNelle.Village.HeroProgression>();
            string job   = abilities != null ? abilities.HeroClass : DeNelle.Village.AbilityCatalog.DefaultClass;
            int    level = progression != null ? progression.Level : 1;

            // Bias low: arena drops stay common/uncommon (the outpost owns the rare/epic curve).
            string targetRarity = UnityEngine.Random.value < 0.65f ? "common" : "uncommon";

            // 50/50 weapon vs armor; fall back to the other type if the first yields none.
            if (UnityEngine.Random.value < 0.5f)
            {
                var w = PickArenaWeapon(job, level, targetRarity);
                if (w != null) { loadout.EquipWeaponById(w.id); return w.name; }
                var a = PickArenaArmor(level, targetRarity);
                if (a != null) { loadout.EquipArmorById(a.id); return a.name; }
            }
            else
            {
                var a = PickArenaArmor(level, targetRarity);
                if (a != null) { loadout.EquipArmorById(a.id); return a.name; }
                var w = PickArenaWeapon(job, level, targetRarity);
                if (w != null) { loadout.EquipWeaponById(w.id); return w.name; }
            }
            return null;
        }

        // Pick the eligible weapon at the target rarity the hero qualifies for; else the
        // best weapon for the hero's job/level (GearCatalog fallback). Null if none.
        private static DeNelle.Village.WeaponDef PickArenaWeapon(string job, int level, string rarity)
        {
            DeNelle.Village.WeaponDef exact = null;
            foreach (var w in DeNelle.Village.GearCatalog.AllWeapons())
            {
                if (w == null) continue;
                if (!string.IsNullOrEmpty(w.job)
                    && !w.job.Equals("any", StringComparison.OrdinalIgnoreCase)
                    && !w.job.Equals(job ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
                if (w.req != null && level < w.req.level) continue;
                if (string.Equals(w.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || w.damageMult > exact.damageMult) exact = w;
                }
            }
            return exact ?? DeNelle.Village.GearCatalog.BestWeapon(job, level);
        }

        private static DeNelle.Village.ArmorDef PickArenaArmor(int level, string rarity)
        {
            DeNelle.Village.ArmorDef exact = null;
            foreach (var a in DeNelle.Village.GearCatalog.AllArmors())
            {
                if (a == null) continue;
                if (a.req != null && level < a.req.level) continue;
                if (string.Equals(a.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || a.defense > exact.defense) exact = a;
                }
            }
            return exact ?? DeNelle.Village.GearCatalog.BestArmor("any", level);
        }
    }
}
