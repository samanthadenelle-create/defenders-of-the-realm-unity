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
using UnityEngine.Rendering;            // URP Volume / VolumeProfile (arena bloom, WO-504 #2)
using UnityEngine.Rendering.Universal;  // Bloom override + UniversalAdditionalCameraData
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

        // Radius around ArenaCentre that counts as "inside the staged arena". Generous (well past
        // the ~30x24 footprint) so any arena actor reads as arena; anything else is the home scene.
        private const float ArenaWorldRadius = 200f;

        /// <summary>
        /// True when <paramref name="worldPos"/> lies inside the far-offset staged arena (vs. the
        /// home/open-world scene that stays in memory). Lets shared systems (e.g. combat feel) tell
        /// a LIVE arena hit from a home-scene bleed-through hit by where it happened — the arena is
        /// staged ~7km away, so distance is an unambiguous discriminator.
        /// </summary>
        public static bool IsArenaPosition(Vector3 worldPos)
            => (worldPos - ArenaCentre).sqrMagnitude <= ArenaWorldRadius * ArenaWorldRadius;

        // Open kite arena footprint (owner doc ~28-35 x 18-22) -- big enough to kite.
        private const float ArenaHalfWidth = 30f;   // X half-extent (~60 wide) — owner 2026-06-23: bigger to KITE + lure one away
        private const float ArenaHalfDepth = 24f;   // Z half-extent (~48 deep) — open kite space, not a square

        private const float BattleTimeoutSeconds = 240f; // generous; a stuck fight ends, never soft-locks

        // WO-505 "battle closing": the wall-clock time the fight went live (set in
        // BeginEncounter). Resolve subtracts it to get the duration the star rating reads.
        private float _battleStartTime;

        // WO-505: how long the victory/defeat cue is allowed to breathe before the explore
        // BGM crossfades back in. Matched to the result banner's ~2.5s hold so the climax
        // music plays under the banner, then the open-world ambient returns. Owner-tunable.
        private const float RewardCueSeconds = 2.5f;

        // LOSE-FLOW (owner TOP priority): on a LOSS the hero must land SAFE, not back inside the
        // rep's aggro (which re-fought instantly). We pull the return point BACK along the hero's
        // approach heading by this much — far enough to clear a rep's ~14m aggro radius — so the
        // hero recovers with breathing room instead of re-engaging on the spot. Win returns to the
        // exact engagement spot (the rep is dead) unchanged.
        private const float LossSafeRetreatMeters = 18f;   // > rep AggroRange (14f) so the warp clears aggro

        // ---------------------------------------------------------------------
        //  WO-504 #2: arena BLOOM tunables (BONES - owner felt-tunes the numbers).
        // ---------------------------------------------------------------------
        // The global DefaultVolumeProfile ships Bloom intensity 0 (effectively OFF) and the
        // URP asset has HDR off, so the arena's HDR materials (Crystal 4.2 / Arcane Shield 4.5
        // / FireTrail 3.0 / additive particles) do NOT glow. We add a LOCAL global Volume +
        // a code-built Bloom profile under _arenaRoot, force the combat camera's post-process
        // + HDR on for the fight, and restore both on Resolve. Cheap (bloom only) for mobile.
        //
        // Defaults: intensity moderate so HDR pops without washing the scene; threshold ~1.0
        // so only HDR > 1 (the VFX) blooms, not lit geometry. Owner tunes these by eye.
        private const float ArenaBloomIntensity = 1.4f;   // moderate glow multiplier (0..~3 typical)
        private const float ArenaBloomThreshold = 1.2f;   // only true-HDR VFX (lum > 1.2) blooms; lit ground stays out of bloom
        private const float ArenaBloomScatter   = 0.7f;   // glow spread (URP default)
        private const int   ArenaBloomPriority  = 100;    // outrank the global DefaultVolumeProfile (priority 0)

        private static BattleArena _instance;

        /// <summary>The live BattleArena (creates a persistent host on first access).</summary>
        public static BattleArena Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("BattleArena");
                    // DontDestroyOnLoad is play-mode-only and THROWS in an editor script
                    // (the headless ArenaCombatOracle stands this host up in batchmode to
                    // drive the real Resolve path). Guard it so the host is editor-
                    // instantiable; in a build/playtest the persist behaviour is unchanged.
                    if (Application.isPlaying) DontDestroyOnLoad(host);
                    _instance = host.AddComponent<BattleArena>();
                }
                return _instance;
            }
        }

        /// <summary>The live BattleArena WITHOUT creating one (null until first staged). Use this in
        /// hot paths / probes that must not spawn the singleton just to ask if a battle is running.</summary>
        public static BattleArena Existing => _instance;

        /// <summary>True iff a battle is currently staged — non-creating (safe in hot combat paths).</summary>
        public static bool AnyBattleInProgress => _instance != null && _instance.BattleInProgress;

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

        // WO-504 #2: combat-camera post-process/HDR overrides, saved so the open-world camera
        // is untouched on return. Saved when staged, restored in Resolve.
        private bool _camStateSaved;
        private Camera _bloomCam;
        private bool _savedCamAllowHDR;
        private bool _savedCamPostFx;
        private bool _hadCamData;

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
            _battleStartTime = Time.time;   // WO-505: start the star-rating clock.
            BattleInProgress = true;

            // BATTLE ISOLATION: freeze EVERY home-scene rep for the duration of the fight. The
            // open world stays in memory (additive intent), so without this its reps keep roaming/
            // chasing/aggroing + running home combat — bleeding rumble/feedback into the battle and
            // double-simulating (choppy). One static gate removes all three sources at once; reps
            // resume on Resolve. (Static so no per-rep FindObjectsOfType scan is needed.)
            RepEngageWatcher.PauseAll();

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

            // WO-512 slice 1: AUTO-LOCK the nearest enemy on engaging the battle (flag-gated).
            // Behind FeatureFlags.LockOn so OFF == today's exact behavior (no lock acquired). The single
            // lock owner is HeroTargetIndicator on the hero; EngageLock(null) picks the nearest hostile.
            // No camera/facing change here (slices 2-3) — only the reticle reds + abilities aim follow.
            if (FeatureFlags.LockOn)
            {
                Guard.Try("BattleArena", "auto-lock nearest on engage", () =>
                {
                    var hero = GameObject.FindWithTag("Player");
                    var indicator = hero != null ? hero.GetComponent<HeroTargetIndicator>() : null;
                    if (indicator != null)
                    {
                        indicator.EngageLock();   // auto-nearest
                        var t = indicator.LockedEnemyTarget as MonoBehaviour;
                        string nm = t != null ? t.gameObject.name.Replace("(Clone)", "").Trim() : "none";
                        FlowTrace.Step("BattleArena", "LOCKON acquire target='" + nm + "' (auto-nearest on engage).");
                        // WO-512 slice 2: bind the camera to keep the LOCKED enemy framed (reuses the
                        // SMC auto-framing damp; no snap). No-op when the flag is off. WatchToResolution
                        // re-binds each tick so a HUD switch / auto-drop-on-death re-frames smoothly.
                        if (t != null) SmartMobileCamera.Instance?.SetLockTarget(t.transform);
                    }
                });
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

            // WO-504 #2: force the combat camera's post-processing + HDR ON so the arena Volume's
            // Bloom is applied and HDR > 1 VFX actually glow. Saved + restored on Resolve.
            EnableCombatBloomCamera();

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

            // WO-506: the REAL authored landscape. Load + instantiate the forest-clearing
            // PREFAB (Resources/Arena/ForestClearingArena, built by ArenaPrefabBuilder) onto
            // _arenaRoot -> a real ground mesh + dressed treeline + soft light, NOT a primitive
            // box. The prefab's Ground carries a Default-layer MeshCollider so the StageRoutine
            // ArenaNavMeshBaker bakes over it (no duplicate bake here). Idempotent loaders are
            // Guard-wrapped; a null prefab degrades to a plain LIT ground (never white/primitive-box).
            GameObject stage = null;
            Guard.Try("BattleArena", "load arena landscape prefab", () =>
            {
                stage = Resources.Load<GameObject>("Arena/ForestClearingArena");
            });
            if (stage != null)
            {
                Guard.Try("BattleArena", "instantiate arena landscape", () =>
                {
                    var go = UnityEngine.Object.Instantiate(stage, _arenaRoot.transform, false);
                    go.name = "ArenaLandscape";
                });
                FlowTrace.Step("BattleArena", "BuildArena: loaded landscape prefab 'Arena/ForestClearingArena'.");
            }
            else
            {
                // SAFE FALLBACK: a plain lit ground plane (real _BaseColor, NO white emission)
                // so the fight degrades to a real floor, never a white box / missing ground.
                BuildFallbackFloor(theme);
                FlowTrace.Warn("BattleArena", "BuildArena: landscape prefab missing -> plain lit ground fallback.");
            }

            // Invisible boundary walls so neither hero nor enemy can wander off the stage.
            // (The NavMesh already confines agents; the walls are belt-and-braces + block
            //  the off-mesh hero-translation fallback.)
            BuildWall(new Vector3(0f,  2f,  ArenaHalfDepth + 0.5f), new Vector3(ArenaHalfWidth * 2f + 2f, 6f, 1f));
            BuildWall(new Vector3(0f,  2f, -ArenaHalfDepth - 0.5f), new Vector3(ArenaHalfWidth * 2f + 2f, 6f, 1f));
            BuildWall(new Vector3( ArenaHalfWidth + 0.5f, 2f, 0f),  new Vector3(1f, 6f, ArenaHalfDepth * 2f + 2f));
            BuildWall(new Vector3(-ArenaHalfWidth - 0.5f, 2f, 0f),  new Vector3(1f, 6f, ArenaHalfDepth * 2f + 2f));

            // WO-506: the edge treeline now lives IN the loaded landscape prefab (EdgeProps),
            // authored with the same DressArenaEdge ring math. The old runtime DressArenaEdge
            // call is dropped from the build path (the prefab provides the silhouette ring).

            // THE WOW (WO-499): a painted biome backdrop ringing the arena behind the treeline. Skip-safe.
            BuildBackdrop(_activeBiome);

            // WO-504 #2: the BLOOM multiplier so the HDR VFX/materials actually GLOW. A local
            // global Volume (priority above the global profile) + a code-built Bloom override,
            // staged with the arena (torn down with _arenaRoot). Camera post-fx/HDR forced on
            // separately (EnableCombatBloomCamera). Skip-safe -> no glow, never breaks the fight.
            BuildArenaBloom();

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
        //  WO-506 SAFE FALLBACK: a plain lit ground plane (real _BaseColor, NO white
        //  emission) used only when the landscape prefab is missing, so the fight
        //  degrades to a real floor, NEVER a white/primitive-box void. The prefab is
        //  the normal path; this guarantees a bakeable Default-layer floor regardless.
        // ---------------------------------------------------------------------
        private void BuildFallbackFloor(string theme)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // MeshFilter + MeshRenderer + MeshCollider
            floor.name = "ArenaFloor_Fallback";
            floor.transform.SetParent(_arenaRoot.transform, false);
            floor.layer = 0; // Default layer so ArenaNavMeshBaker (PhysicsColliders) bakes it.
            floor.transform.localScale = new Vector3((ArenaHalfWidth * 2f) / 10f + 0.4f, 1f, (ArenaHalfDepth * 2f) / 10f + 0.4f);
            ApplyGroundTheme(floor, theme);
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
                // PERMANENT live instrumentation (owner steer 2026-06-23 "debug line background loaded"):
                // a headless encounter run / F8 felt-test self-PROVES the painted biome backdrop actually
                // rendered (4 quads built on the success path) — not inferred from code-reading.
                FlowTrace.Step("BattleArena", "BACKDROP loaded theme=" + key + " tex=" + tex.name + " quads=4");
            });
        }

        // ---------------------------------------------------------------------
        //  WO-504 #2: arena BLOOM - a local global URP Volume + code-built Bloom profile.
        // ---------------------------------------------------------------------
        // Why a LOCAL volume (not the shipped DefaultVolumeProfile): that profile's Bloom is
        // intensity 0 (off), and the far-offset arena should glow ONLY during the fight (no
        // global post-fx change leaking to the open world). The Volume is parented to
        // _arenaRoot so it tears down automatically on Resolve. Global mode (isGlobal=true)
        // so it covers the whole far-offset stage without a box collider. Priority above the
        // global profile so its Bloom override wins. Skip-safe (Guard) -> no glow, never throws.
        private void BuildArenaBloom()
        {
            Guard.Try("BattleArena", "build arena bloom volume", () =>
            {
                // Code-built profile (ScriptableObject, not an asset) so there is no Resources
                // dependency and nothing to drag-drop. A single Bloom override, HDR-thresholded.
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "ArenaBloomProfile";

                var bloom = profile.Add<Bloom>(overrides: true);
                bloom.intensity.Override(Mathf.Max(0f, ArenaBloomIntensity));
                bloom.threshold.Override(Mathf.Max(0f, ArenaBloomThreshold));
                bloom.scatter.Override(Mathf.Clamp01(ArenaBloomScatter));
                // Mobile-cheap: leave high-quality filtering OFF (default) -> bloom only, light cost.

                // Tonemapping safety net: compress HDR back to SDR so the lit ground can never blow
                // out to white even with HDR camera + bloom. Neutral mode = perceptually faithful.
                var tonemap = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
                tonemap.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);

                var go = new GameObject("ArenaBloomVolume");
                go.transform.SetParent(_arenaRoot.transform, false);
                var vol = go.AddComponent<Volume>();
                vol.isGlobal = true;                 // covers the whole far-offset stage, no collider needed
                vol.priority = ArenaBloomPriority;   // outrank the global DefaultVolumeProfile
                vol.sharedProfile = profile;

                FlowTrace.Step("BattleArena", $"BuildArenaBloom: local bloom volume (intensity={ArenaBloomIntensity}, threshold={ArenaBloomThreshold}).");
            });
        }

        // Force the combat camera's post-processing + HDR ON for the fight so the arena Bloom
        // volume is applied and HDR > 1 VFX glow (the URP asset ships m_SupportsHDR off, so the
        // camera-level allowHDR is what lets the HDR materials exceed 1.0 into bloom). The prior
        // state is saved and restored on Resolve so the open-world camera is untouched. Skip-safe.
        private void EnableCombatBloomCamera()
        {
            if (_camStateSaved) return;
            Guard.Try("BattleArena", "enable combat bloom camera", () =>
            {
                var cam = Camera.main;
                if (cam == null) { FlowTrace.Warn("BattleArena", "EnableCombatBloomCamera: no Camera.main - bloom volume still applies if another camera has post-fx."); return; }

                _bloomCam = cam;
                _savedCamAllowHDR = cam.allowHDR;
                cam.allowHDR = true;

                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null)
                {
                    _hadCamData = true;
                    _savedCamPostFx = data.renderPostProcessing;
                    data.renderPostProcessing = true;
                }
                else
                {
                    _hadCamData = false;
                }

                _camStateSaved = true;
                FlowTrace.Step("BattleArena", $"EnableCombatBloomCamera: post-fx+HDR forced on (hadData={_hadCamData}).");
            });
        }

        // Restore the combat camera's saved post-fx/HDR state on Resolve. One-shot + null-safe
        // (the camera may have been destroyed between stage and resolve; we only touch it if live).
        private void RestoreCombatBloomCamera()
        {
            if (!_camStateSaved) return;
            Guard.Try("BattleArena", "restore combat bloom camera", () =>
            {
                if (_bloomCam != null)
                {
                    _bloomCam.allowHDR = _savedCamAllowHDR;
                    if (_hadCamData)
                    {
                        var data = _bloomCam.GetComponent<UniversalAdditionalCameraData>();
                        if (data != null) data.renderPostProcessing = _savedCamPostFx;
                    }
                }
                FlowTrace.Step("BattleArena", "RestoreCombatBloomCamera: open-world camera post-fx/HDR restored.");
            });
            _camStateSaved = false;
            _bloomCam = null;
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

        // LOSE-FLOW: a SAFE return point for a loss — pull back from the engagement spot along the
        // hero's (reversed) approach heading by LossSafeRetreatMeters so the hero lands OUTSIDE a
        // rep's ~14m aggro radius. Snapped onto the navmesh so the hero doesn't strand off-mesh /
        // below the floor; falls back to the raw offset (then the engagement spot) if no mesh is hit.
        private static Vector3 SafeLossReturnPosition(Vector3 engagePos, float engageYaw)
        {
            // The hero faced engageYaw at engage; retreat is BEHIND that facing (back toward where
            // the hero came from — typically toward the castle/seam, away from the rep ahead).
            Vector3 fwd = Quaternion.Euler(0f, engageYaw, 0f) * Vector3.forward;
            Vector3 retreat = engagePos - fwd.normalized * LossSafeRetreatMeters;

            Vector3 result = retreat;
            Guard.Try("BattleArena", "snap loss-safe return to navmesh", () =>
            {
                if (NavMesh.SamplePosition(retreat, out var hit, 12f, NavMesh.AllAreas))
                    result = hit.position;
            });
            FlowTrace.Step("BattleArena", $"SafeLossReturnPosition: engage={engagePos} -> safe={result} (retreat {LossSafeRetreatMeters}m).");
            return result;
        }

        // CAMERA RE-LOCK: re-enable the follow camera and have it re-acquire the hero after the
        // death-cam + warp. SmartMobileCamera owns the open-world follow; the death-cam disabled
        // it and our warp moved the hero, so explicitly enable + snap it here. Reflection-soft so a
        // missing API never throws into Resolve.
        private static void ReacquireFollowCamera()
        {
            Guard.Try("BattleArena", "reacquire follow camera", () =>
            {
                var smc = SmartMobileCamera.Instance;
                if (smc == null) return;
                if (!smc.enabled) smc.enabled = true;   // death-cam disabled it; re-enable the follow
                // Snap the rig onto the hero immediately so it doesn't ease across the whole world
                // on return (the warp moved the hero while the camera was suspended).
                smc.ForceFollowImmediate();
                FlowTrace.Step("BattleArena", "ReacquireFollowCamera: follow camera re-enabled + snapped to hero.");
            });
        }

        // CAMERA RE-LOCK (cont.): clear any stale target lock on the hero's reticle so a loss return
        // doesn't keep the dead/old foe locked (which would re-aim abilities at nothing). Resolved
        // off the live hero; guarded.
        private static void ClearHeroTargetLock()
        {
            Guard.Try("BattleArena", "clear hero target lock", () =>
            {
                var hero = GameObject.FindWithTag("Player");
                if (hero == null) return;
                var indicator = hero.GetComponent<HeroTargetIndicator>();
                indicator?.ClearLock();
            });
            // WO-512 slice 2: also release the lock-on CAMERA framing so the open-world camera
            // returns to its normal auto-framing / free-look on resolve (loss + win). No-op if no
            // lock target was ever bound; eases back via the shared damp (never a snap).
            Guard.Try("BattleArena", "clear lock camera framing",
                () => SmartMobileCamera.Instance?.ClearLockTarget());
        }

        // WO-512 slice 2: re-bind the lock-on camera to the hero's CURRENT locked enemy each watch
        // tick. The HUD/indicator can switch or drop the lock without BattleArena knowing, so we
        // read the live LockedEnemyTarget and hand its transform to the camera (SetLockTarget eases
        // via the shared damp; null/no-lock clears the camera framing). Guard-wrapped + reflection-
        // soft so a missing actor never throws into the watch loop.
        private static void MaybeRebindLockCamera()
        {
            Guard.Try("BattleArena", "rebind lock camera", () =>
            {
                var smc = SmartMobileCamera.Instance;
                if (smc == null) return;
                var hero = GameObject.FindWithTag("Player");
                var indicator = hero != null ? hero.GetComponent<HeroTargetIndicator>() : null;
                var locked = indicator != null ? indicator.LockedEnemyTarget as MonoBehaviour : null;
                if (locked != null) smc.SetLockTarget(locked.transform);
                else                smc.ClearLockTarget();
            });
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

                // WO-512 slice 2: keep the lock-on camera framed on the CURRENT locked foe. The
                // lock can switch (HUD cycle/tap) or auto-drop (target died) without BattleArena
                // observing it, so we re-read the live locked transform each tick and re-bind the
                // camera (a Transform set eased by the shared _leadPoint damp -> smooth re-frame,
                // never a snap). Flag-gated + Guard-wrapped so flag-off is today's exact path.
                if (FeatureFlags.LockOn) MaybeRebindLockCamera();

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
        //  HEADLESS ORACLE SEAM (WO-505) — drive the REAL Resolve() path with a
        //  synthetic context, no full PlayMode fight. Lets DeNelle.Editor's
        //  ArenaCombatOracle EXECUTE the win/loss resolve (audio cue + star/reward
        //  computation + GrantWinReward) and assert the FlowTrace FIRED lines were
        //  emitted — closing the "rests on code-reading inference" gap. This calls
        //  the SAME private Resolve the live fight calls (zero behaviour fork); it
        //  only pre-seeds the fields BeginEncounter would have set. Editor/QA seam
        //  only — never called by gameplay. duration lets the oracle pin a star tier.
        // ---------------------------------------------------------------------
        public void ResolveForTest(EncounterParams p, bool won, float durationSeconds)
        {
            _current = p;
            _resolved = false;
            _climaxBody = null;
            _battleStartTime = Time.time - Mathf.Max(0f, durationSeconds);
            BattleInProgress = true;
            Resolve(won);
        }

        // ---------------------------------------------------------------------
        //  Resolve + return (reward, tear down the stage, warp hero home)
        // ---------------------------------------------------------------------
        private void Resolve(bool won)
        {
            if (_resolved) return;
            _resolved = true;

            // WO-505 "battle closing": how long the fight took -> a 1..3 star rating. The
            // duration is read at the resolving moment so it reflects the actual fight.
            float durationSeconds = Mathf.Max(0f, Time.time - _battleStartTime);
            int stars = won ? BattleStarRating.StarsForDuration(durationSeconds) : 0;
            float rewardMult = won ? BattleStarRating.MultiplierForStars(stars) : 1f;
            FlowTrace.Step("BattleArena", $"Resolve: {(won ? "WIN" : "LOSS")} in {durationSeconds:0.0}s -> {stars} star(s), reward x{rewardMult:0.00}.");
            // WO-505 ORACLE FIRE-POINT (permanent live instrumentation — leave in behind the
            // FlowTrace toggle): the star/reward computation the headless ArenaCombatOracle reads
            // to PROVE (not infer) stars + multiplier were computed on this resolve path.
            FlowTrace.Step("BattleArena", $"STARS={stars} rewardMult={rewardMult:0.00} applied (won={won}).");

            // WO-505: play the climax CUE so the death-cam beat is not silent. Victory fanfare
            // on a win / defeat sting on a loss (the clips exist: Audio/Resources/victory.mp3,
            // defeat.mp3, wired by AudioBootstrap). Overworld BGM is restored after the banner
            // beat (RestoreAmbientAfter) so we never cut the climax to silence.
            // WO-505 ORACLE FIRE-POINT: an explicit FIRED line AT the PlayMusic call so the
            // headless oracle (and the owner's F8 felt-test break-log) prove the victory/defeat
            // cue actually fired on the real resolve path — not from code-reading. Permanent.
            FlowTrace.Step("BattleArena", won
                ? "VICTORY AUDIO FIRED track=Victory"
                : "DEFEAT AUDIO FIRED track=Defeat");
            Guard.Try("BattleArena", "battle result music cue",
                () => CoreServices.Audio?.PlayMusic(won ? MusicTrack.Victory : MusicTrack.Defeat));

            // Banner (presentation; self-destructs after a beat). Live overlay hides inside ShowResult.
            Guard.Try("BattleArena", "battle result banner", () => _hud?.ShowResult(won, stars));
            _hud = null;

            // REWARD (logic, v1 minimal): XP on a win, scaled by the star multiplier. Fuller
            // loot (gear/resources) is the EnemyOutpost loot-table reuse follow-up; kept light
            // here so the loop is closed.
            if (won) Guard.Try("BattleArena", "grant win XP", () => GrantWinReward(_current, rewardMult));

            // Restore any cavern mood RenderSettings BEFORE the open world is back in view.
            RestoreCavernMood();

            // WO-504 #2: hand the combat camera's post-fx/HDR back to its open-world state
            // (the Volume itself tears down with _arenaRoot below).
            RestoreCombatBloomCamera();

            // Tear the stage down: kill any survivors + destroy the arena root.
            foreach (var e in _liveEnemies) if (e != null) Guard.Try("BattleArena", "despawn enemy", () => Destroy(e.gameObject));
            _liveEnemies.Clear();
            if (_arenaRoot != null) Destroy(_arenaRoot);
            _arenaRoot = null;

            // LOSE-FLOW (owner TOP priority): make a LOSS RECOVERABLE — no instant re-engage.
            //   1) Despawn the triggering rep NOW (DestroyImmediate) if it somehow still exists,
            //      beating the queued-Destroy race that could leave it live as the hero returns.
            //   2) Open a post-loss re-aggro GRACE so NO rep can engage the hero for a few seconds,
            //      regardless of any rep's exact state — this is the loop-breaker.
            //   3) Warp the hero to a SAFE spot (pulled back along its approach heading past a rep's
            //      aggro radius), not the exact engagement spot inside aggro.
            if (!won && _current != null)
            {
                RepEngageWatcher.DespawnRepImmediate(_current.RepId);
                RepEngageWatcher.BeginPostLossGrace();   // ~3.5s no-engage window
            }

            // Compute the return pose. WIN: exact engagement spot (rep is dead). LOSS: safe retreat.
            Vector3 returnPos = _current != null ? _current.ReturnPosition : Vector3.zero;
            float   returnYaw = _current != null ? _current.ReturnYaw : 0f;
            if (!won && _current != null)
                returnPos = SafeLossReturnPosition(_current.ReturnPosition, _current.ReturnYaw);

            // Warp the hero home (the open world stayed in memory).
            if (_current != null)
                WarpHero(returnPos, Quaternion.Euler(0f, returnYaw, 0f));

            // BATTLE ISOLATION: the fight is over — let home reps roam/chase/aggro again. (On a
            // loss the post-loss grace above still suppresses ENGAGE for a few seconds even though
            // the pause is lifted, so the hero recovers; a win lifts both gates cleanly.)
            RepEngageWatcher.ResumeAll();

            // CAMERA RE-LOCK (loss especially): the death-cam released SmartMobileCamera, but the
            // hero was warped AFTER that and the stale target lock was never cleared. Re-enable the
            // follow camera + snap it to the hero, and clear the reticle's manual lock so the next
            // engagement starts clean.
            ReacquireFollowCamera();
            ClearHeroTargetLock();

            // WO-505: restore explore BGM AFTER the victory/defeat cue has had its beat, so
            // the climax is not cut to silence (the banner shows for ~2.5s; we let the sting
            // breathe, then crossfade back to Overworld). Coroutine on this persistent
            // singleton; guarded so a teardown race never throws.
            Guard.Try("BattleArena", "schedule ambient restore",
                () => StartCoroutine(RestoreAmbientAfter(RewardCueSeconds)));

            var done = _current;
            _current = null;
            BattleInProgress = false;

            OnBattleEnded?.Invoke(done, won);
            FlowTrace.Step("BattleArena", $"Resolve: stage torn down, hero {(won ? "returned" : "retreated SAFE")}, battle ended.");
        }

        // WO-505: wait out the victory/defeat cue, then crossfade back to the open-world
        // ambient. Unscaled wait so a slow-mo death-cam time scale doesn't stretch it.
        private System.Collections.IEnumerator RestoreAmbientAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            CoreServices.Audio?.PlayMusic(MusicTrack.Overworld);
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
        private static void GrantWinReward(EncounterParams p, float rewardMult = 1f)
        {
            if (p == null) return;
            int family = Mathf.Max(0, p.EnemyIds != null ? p.EnemyIds.Length : 0);
            int threat = Mathf.Max(0, p.Threat);

            // WO-505: the star rating scales the FELT payout (1x / 1.25x / 1.5x). Applied to
            // every quantified grant below (XP, wisdom, resources) so a faster, cleaner win
            // pays more. Guarded to a sane floor so a bad value never zeroes the reward.
            float mult = Mathf.Max(1f, rewardMult);

            // 1) XP — unchanged path (HeroProgression via reflection).
            int xp = Mathf.RoundToInt((20 + 8 * family + 4 * threat) * mult);
            var prog = GameObject.FindObjectOfType(Type.GetType("DeNelle.Village.HeroProgression, DeNelle.Village")) as MonoBehaviour;
            if (prog != null)
            {
                var add = prog.GetType().GetMethod("AddXp", new[] { typeof(float) });
                add?.Invoke(prog, new object[] { (float)xp });
            }
            FlowTrace.Step("BattleArena", $"GrantWinReward: +{xp} XP (family={family} threat={threat}).");

            // 2) SKILL POINTS (Wisdom) — 1 base + 1 per 2 family members + 1 per 2 threat
            // tiers, so a bigger/deadlier family pays a felt skill-point bump.
            int wisdom = Mathf.RoundToInt((1 + family / 2 + threat / 2) * mult);
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
            int wood = Mathf.RoundToInt((10 + 4 * threat) * mult);
            int iron = Mathf.RoundToInt((4 + 2 * threat) * mult);
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
