// =============================================================================
// HubFoliageInjector -- runtime, NON-DESTRUCTIVE natural dressing (trees, rocks,
// bushes) for the home hub. FIRST-PASS BONES the owner dials by eye.
// -----------------------------------------------------------------------------
// OWNER REQUEST (2026-08-02): "can we add more trees and natural items in world?
// Feels very empty and boring."
//
// WHY A RUNTIME INJECTOR (not a scene edit / rebake) -- identical rationale to
//   HubAmbientVfxInjector + CavePortalRepointInjector: re-saving
//   Main_Castle_Overworld.unity carries the project's scene-resave corruption
//   history (CLAUDE.md SS3 "NEVER hand-edit"). So this self-bootstrapping DDOL
//   singleton SCATTERS props at runtime and NEVER touches the .unity file.
//   It also never adds a collider or a NavMeshObstacle, so it can NOT invalidate
//   the baked navmesh -- no rebake is ever required to ship or revert this.
//
// ART SOURCING (the constraint that shaped this file):
//   * A RUNTIME injector cannot use AssetDatabase, so every model must come from
//     Resources. The polyperfect Low Poly Ultimate Pack (the catalog in
//     docs/polyperfect-asset-catalog.md, Nature_M/Trees_M + Nature_M/Stones_M)
//     is GITIGNORED (.gitignore line 128 "/Assets/polyperfect/") AND is not under
//     any Resources folder -- so it is unreachable from runtime code. Referencing
//     it here would produce a hub that is empty on every clean clone and in CI.
//   * The GIT-TRACKED, Resources-resident nature props below are therefore the
//     primary source (verified with `git ls-files`):
//         Assets/Resources/Arena/Tree_2_A_Color1.fbx      (tracked)
//         Assets/Resources/Arena/Tree_5_C_Color1.fbx      (tracked)
//         Assets/Resources/Arena/Tree_7_A_Color1.fbx      (tracked)
//         Assets/Resources/Arena/Tree_Bare_1_A_Color1.fbx (tracked)
//         Assets/Resources/Arena/Rock_1_A_Color1.fbx      (tracked)
//         Assets/Resources/Arena/Rock_1_J_Color1.fbx      (tracked)
//         Assets/Resources/Arena/Rock_2_C_Color1.fbx      (tracked)
//         Assets/Resources/Arena/Rock_3_E_Color1.fbx      (tracked)
//         Assets/Resources/Hedges/Fence_Shrub.prefab      (tracked)
//     These are the same KayKit forest props ForestClearingArena.prefab already
//     ships, so the look is consistent with the arena backdrop.
//     CAVEAT (honest): their shared material lives in the GITIGNORED KayKit pack
//     (Assets/Models/KayKit/.../forest_texture_URP.mat), so on a clean clone the
//     meshes load but land on Unity's default material. That is exactly what
//     DeNelle.Core.EnvironmentTreeMaterialFixer exists to repair, and we call it
//     explicitly after the scatter (see FixSpawnedMaterials) because BOTH systems
//     hook sceneLoaded and the handler order is not guaranteed.
//   * MISSING PREFAB IS NEVER FATAL (CLAUDE.md SS4): a Resources miss logs ONE
//     Debug.LogWarning per path and the scatter continues with what resolved.
//
// GATED -- FeatureFlags.HubFoliage (PlayerPrefs "ff.hubfoliage" = 0 turns it off
//   with NO rebuild), plus per-tier const toggles below.
//
// DeNelle.Village -> DeNelle.Core only. No reflection. ASCII only.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>Runtime, non-destructive natural dressing (trees / rocks / bushes) scattered
    /// around the castle hub with a fixed seed and hard keep-out zones.</summary>
    public sealed class HubFoliageInjector : MonoBehaviour
    {
        public static HubFoliageInjector Instance { get; private set; }

        private const string TargetScene       = "MainCastle_Hall";
        private const string MergedTargetScene = "Main_Castle_Overworld";
        private static bool IsCastleHubScene(string n) => n == TargetScene || n == MergedTargetScene;
        private const string HolderName = "HubFoliage (runtime)";

        // =====================================================================
        //  TUNABLES -- the owner dials these by eye. Plain consts (never a
        //  drag-drop inspector field, per the never-dragdrop rule) so a density
        //  change is a one-line edit + rebuild. Distances are world metres.
        // =====================================================================

        // ---- Master per-tier toggles (cheap kill switches beside the flag) ----
        private const bool EnableTrees  = true;   // (1) the big silhouette lever
        private const bool EnableRocks  = true;   // (2) boulders / ground breakup
        private const bool EnableBushes = true;   // (3) low filler at tree feet

        // ---- INSTANCE BUDGET (mobile / Android Seeker) -----------------------
        // 150 props over ~9 unique low-poly meshes (KayKit forest set, ~300-900
        // tris each) and <= 2 unique materials. With GPU instancing on (below)
        // that is roughly 10-14 extra draw calls and <= ~120k tris worst case --
        // comfortably inside the Seeker budget next to the existing hub, and it
        // is the number to LOWER FIRST if the hub ever drops frames.
        // NOTE: static batching is deliberately NOT used -- the source FBXs are
        // imported with isReadable: 0 (verified in Arena/*.fbx.meta), and
        // StaticBatchingUtility.Combine needs read/write-enabled meshes, so a
        // runtime combine would fail and/or duplicate mesh memory.
        private const int MaxInstancesConst = 150;

        // Tier weights (relative). Mostly trees -- they are what reads as "not empty".
        private const int TreeWeight  = 55;
        private const int RockWeight  = 27;
        private const int BushWeight  = 18;

        // ---- SCATTER REGION --------------------------------------------------
        // An annulus centred on the CASTLE origin (0,0,0): the castle footprint,
        // its walls and the moat are all authored origin-relative
        // (CastleMoatBuilder MoatOuterRadius = 62; the four gate landings sit at
        // +/-66 on each axis -- CastleGateNavVerify). Starting at 74 therefore
        // clears the moat, the shore strip and the gate landings by ~8m.
        // The exterior terrain is 1000x1000 centred on origin
        // (ExteriorTerrainBuilder TerrainSizeXZ = 1000), so 185 stays well inside it.
        private const float InnerRadius = 74f;    // nothing closer than this to the castle
        private const float OuterRadius = 185f;   // outer edge of the dressed band

        // The four cardinal gate CORRIDORS run out along the world axes. Anything
        // whose |x| or |z| is under this half-width is skipped, so a gate exit and
        // its approach lane are never dressed. Mirrors Village2Generator.ScatterNatureRing.
        private const float GateLaneHalfWidth = 14f;

        // ---- KEEP-OUT ZONES --------------------------------------------------
        private const float HeartClearRadius     = 40f;  // clear ring around the Heart / Tree of Life
        private const float StructureClearance   = 4.0f; // added to a structure's own bounds radius
        private const float RouteCorridorRadius  = 7.0f; // clear width around a walked route
        private const float MinPropSpacing       = 3.5f; // props never clump / z-fight into each other
        private const float MaxStructureRadius   = 60f;  // ignore mega-colliders (terrain/floor/navmesh planes)

        // ---- GROUND SAMPLING -------------------------------------------------
        private const float GroundProbeStartY   = 150f;  // raycast down from here
        private const float GroundProbeLength   = 400f;
        private const float MinGroundY          = -0.75f; // below this = moat/water/void -> reject
        private const float MaxGroundSlopeDeg   = 32f;    // no trees glued to cliff faces
        private const float NavSampleRadius     = 6f;     // NavMesh.SamplePosition probe radius
        private const float PhysicsProbeHeight  = 2.5f;   // overlap probe centre above ground
        private const float PhysicsProbeRadius  = 1.8f;   // "is something solid already here"

        // ---- SIZE / LOOK -----------------------------------------------------
        private const float TreeHeightMin = 5.0f,  TreeHeightMax = 9.0f;
        private const float RockHeightMin = 0.9f,  RockHeightMax = 2.4f;
        private const float BushHeightMin = 0.9f,  BushHeightMax = 1.8f;
        // Shadows: trees keep them (a tree with no shadow floats); the small props
        // drop out of the shadow pass entirely -- the cheapest real mobile saving here.
        private const bool  TreeShadows = true;
        private const bool  RockShadows = false;
        private const bool  BushShadows = false;
        private const float ScaleJitter = 0.12f;   // +/- 12% uniform scale variety

        // ---- DETERMINISM -----------------------------------------------------
        // FIXED SEED + System.Random (NOT UnityEngine.Random, which shares global
        // state with spawners/VFX and would make the hub -- and any screenshot
        // diff -- different on every run). Same seed => byte-identical layout.
        private const int ScatterSeedConst = 20260802;
        // Candidates generated per wanted instance. Most candidates are rejected by
        // the keep-out pass, so we oversample; 12x has comfortable headroom.
        private const int CandidateOversample = 12;
        private const int MaxCandidateRequest = MaxInstancesConst * 20;  // hard ceiling on the sampler

        // Fallback Heart centre if the controller/anchor is not up yet -- matches
        // HubAmbientVfxInjector.HeartCenterFallback + the authored placement.
        private static readonly Vector3 HeartCenterFallback = new Vector3(0f, 0f, 12f);

        // ---- RESOURCES-TRACKED SOURCE PROPS ---------------------------------
        // Runtime-loadable (Resources) AND git-tracked -- see the header note on
        // why the polyperfect _M prefabs cannot be used from runtime code.
        // The polyperfect equivalents, for whoever mirrors them into Resources later:
        //   Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Nature_M/Trees_M/Tree_Oak.prefab
        //   .../Nature_M/Trees_M/Tree_Birch.prefab, .../Trees_M/Tree_Conifer.prefab
        //   .../Nature_M/Stones_M/Rock_Large.prefab, .../Stones_M/Rocks_Small.prefab
        private static readonly string[] TreePaths =
        {
            "Arena/Tree_2_A_Color1",
            "Arena/Tree_5_C_Color1",
            "Arena/Tree_7_A_Color1",
            "Arena/Tree_Bare_1_A_Color1",
        };
        private static readonly string[] RockPaths =
        {
            "Arena/Rock_1_A_Color1",
            "Arena/Rock_1_J_Color1",
            "Arena/Rock_2_C_Color1",
            "Arena/Rock_3_E_Color1",
        };
        private static readonly string[] BushPaths =
        {
            "Hedges/Fence_Shrub",
        };

        // Route anchors the hero actually walks to. Kept clear as corridors so a
        // tree never lands in the middle of a path the player needs. The four gate
        // landings are the authored +/-66 axis points (CastleGateNavVerify).
        private static readonly Vector3[] RouteAnchors =
        {
            new Vector3(0f,  0f,  66f),   // north gate landing
            new Vector3(0f,  0f, -66f),   // south gate landing
            new Vector3(66f, 0f,   0f),   // east gate landing
            new Vector3(-66f,0f,   0f),   // west gate landing
        };

        // =====================================================================
        //  Public, oracle-visible surface (a headless regression asserts these).
        // =====================================================================

        /// <summary>Hard cap on spawned decorative props (mobile budget).</summary>
        public static int MaxInstances => MaxInstancesConst;

        /// <summary>The fixed scatter seed -- same seed, same hub, every run.</summary>
        public static int ScatterSeed => ScatterSeedConst;

        /// <summary>
        /// PURE, DETERMINISTIC candidate sampler: <paramref name="count"/> XZ points uniformly
        /// distributed over the scatter annulus with the cardinal gate lanes removed. No scene
        /// access, no UnityEngine.Random -- callable headless, which is how the oracle proves
        /// determinism + the geometric keep-out. Never returns more than the internal ceiling.
        /// </summary>
        public static Vector3[] GenerateCandidates(int seed, int count)
        {
            if (count <= 0) return Array.Empty<Vector3>();
            if (count > MaxCandidateRequest) count = MaxCandidateRequest;

            var rng = new System.Random(seed);
            var pts = new List<Vector3>(count);
            int guard = count * 40;   // lanes reject ~40% of the disc; this cannot spin forever

            double rIn2  = (double)InnerRadius * InnerRadius;
            double rOut2 = (double)OuterRadius * OuterRadius;

            while (pts.Count < count && guard-- > 0)
            {
                double ang = rng.NextDouble() * Math.PI * 2.0;
                // sqrt-of-uniform keeps the density even across the annulus instead of
                // bunching everything against the inner rim.
                double r = Math.Sqrt(rng.NextDouble() * (rOut2 - rIn2) + rIn2);
                float x = (float)(Math.Cos(ang) * r);
                float z = (float)(Math.Sin(ang) * r);
                if (Mathf.Abs(x) < GateLaneHalfWidth || Mathf.Abs(z) < GateLaneHalfWidth) continue;
                pts.Add(new Vector3(x, 0f, z));
            }
            return pts.ToArray();
        }

        // =====================================================================
        //  Bootstrap / lifecycle -- mirrors HubAmbientVfxInjector exactly.
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject(nameof(HubFoliageInjector)).AddComponent<HubFoliageInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsCastleHubScene(SceneManager.GetActiveScene().name)) SafeInject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsCastleHubScene(scene.name)) SafeInject();
        }

        // Never let the scatter throw out of a sceneLoaded handler (an uncaught throw
        // there halts the WebGL player) -- same guard CavePortalRepointInjector uses.
        private void SafeInject()
        {
            try { Inject(); }
            catch (Exception e)
            {
                Debug.LogWarning("[HubFoliage] scatter threw (non-fatal, hub is simply undressed): " + e);
            }
        }

        // =====================================================================
        //  The scatter
        // =====================================================================

        private void Inject()
        {
            // Flag gate -- togglable with NO rebuild (PlayerPrefs "ff.hubfoliage" = 0).
            if (!FeatureFlags.HubFoliage)
            {
                FlowTrace.Step("HubFoliage", "HubFoliage flag OFF -- natural dressing skipped.");
                return;
            }

            // Idempotent: drop any prior runtime holder so a re-load never double-scatters.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            var holder = new GameObject(HolderName);

            // ---- 1. keep-out set, gathered from the LIVE scene ----------------
            var keepOuts = new List<KeepOut>(256);
            Guard.Try("HubFoliage", "collect keep-out zones", () => CollectKeepOuts(keepOuts));

            // ---- 2. deterministic candidates ---------------------------------
            Vector3[] candidates = GenerateCandidates(ScatterSeedConst, MaxInstancesConst * CandidateOversample);

            // ---- 3. tier plan (also deterministic -- its own stream) ----------
            var rng = new System.Random(ScatterSeedConst ^ 0x5f3a);

            int placedTrees = 0, placedRocks = 0, placedBushes = 0, rejected = 0;
            var placedPositions = new List<Vector3>(MaxInstancesConst);
            var instancedMaterials = new HashSet<Material>();

            for (int i = 0; i < candidates.Length; i++)
            {
                int total = placedTrees + placedRocks + placedBushes;
                if (total >= MaxInstancesConst) break;   // HARD instance cap

                Vector3 flat = candidates[i];

                // Ground + slope + water rejection.
                if (!TryResolveGround(flat, out Vector3 ground)) { rejected++; continue; }

                // Keep-out rejection (Heart, structures, route corridors).
                if (IsBlocked(ground, keepOuts)) { rejected++; continue; }

                // Spacing rejection (no clumping / z-fighting).
                if (TooClose(ground, placedPositions)) { rejected++; continue; }

                // Something solid already standing here? (buildings/props with colliders)
                if (Physics.CheckSphere(ground + Vector3.up * PhysicsProbeHeight,
                                        PhysicsProbeRadius, ~0, QueryTriggerInteraction.Ignore))
                { rejected++; continue; }

                Tier tier = PickTier(rng);
                if (!SpawnProp(tier, ground, rng, holder.transform, instancedMaterials)) { rejected++; continue; }

                placedPositions.Add(ground);
                if (tier == Tier.Tree) placedTrees++;
                else if (tier == Tier.Rock) placedRocks++;
                else placedBushes++;
            }

            int placed = placedTrees + placedRocks + placedBushes;
            if (placed == 0)
            {
                FlowTrace.Warn("HubFoliage",
                    "HubFoliageInjector placed 0 props -- either every Resources prop is missing " +
                    "(clean clone / pack not imported) or every candidate was rejected by the keep-out pass. " +
                    $"(candidates={candidates.Length}, rejected={rejected}, keepOuts={keepOuts.Count})");
            }
            else
            {
                FlowTrace.Step("HubFoliage",
                    $"HubFoliageInjector: scattered {placed}/{MaxInstancesConst} props " +
                    $"(trees={placedTrees}, rocks={placedRocks}, bushes={placedBushes}; " +
                    $"candidates={candidates.Length}, rejected={rejected}, keepOuts={keepOuts.Count}, " +
                    $"seed={ScatterSeedConst}, band={InnerRadius}-{OuterRadius}m).");
            }

            // ---- 4. material repair ------------------------------------------
            // The tracked FBXs reference a material inside the GITIGNORED KayKit pack, so on a
            // clean clone they land on Unity's default. EnvironmentTreeMaterialFixer repairs
            // "*tree*"-named renderers -- but it hooks sceneLoaded too and may already have run
            // BEFORE us, so we call it explicitly instead of hoping for handler order.
            if (placed > 0)
                Guard.Try("HubFoliage", "repair scattered foliage materials", FixSpawnedMaterials);
        }

        private static void FixSpawnedMaterials()
        {
            EnvironmentTreeMaterialFixer.FixAllTrees();
        }

        // =====================================================================
        //  Tier selection + spawning
        // =====================================================================

        private enum Tier { Tree, Rock, Bush }

        private static Tier PickTier(System.Random rng)
        {
            int tw = EnableTrees  ? TreeWeight : 0;
            int rw = EnableRocks  ? RockWeight : 0;
            int bw = EnableBushes ? BushWeight : 0;
            int sum = tw + rw + bw;
            if (sum <= 0) return Tier.Tree;   // caller's SpawnProp will miss + reject; never throws

            int roll = rng.Next(0, sum);
            if (roll < tw) return Tier.Tree;
            if (roll < tw + rw) return Tier.Rock;
            return Tier.Bush;
        }

        // Instantiate one decorative prop. Returns false on any miss so the caller
        // just moves to the next candidate. NEVER throws, NEVER hard-fails.
        private bool SpawnProp(Tier tier, Vector3 ground, System.Random rng, Transform holder,
                               HashSet<Material> instancedMaterials)
        {
            string[] paths;
            float minH, maxH;
            bool shadows;
            string label;

            // NOTE: the per-tier Enable* toggles are honoured in PickTier (a disabled tier
            // gets weight 0 and is never picked), so there is no redundant check here.
            switch (tier)
            {
                case Tier.Rock:
                    paths = RockPaths; minH = RockHeightMin; maxH = RockHeightMax; shadows = RockShadows; label = "Rock";
                    break;
                case Tier.Bush:
                    paths = BushPaths; minH = BushHeightMin; maxH = BushHeightMax; shadows = BushShadows; label = "Bush";
                    break;
                default:
                    paths = TreePaths; minH = TreeHeightMin; maxH = TreeHeightMax; shadows = TreeShadows; label = "Tree";
                    break;
            }

            if (paths == null || paths.Length == 0) return false;
            string path = paths[rng.Next(0, paths.Length)];

            GameObject src = LoadCached(path);
            if (src == null) return false;   // already warned ONCE for this path

            GameObject go = null;
            Guard.Try("HubFoliage", $"instantiate '{path}'", () => go = Instantiate(src, holder));
            if (go == null) return false;

            // Name carries the tier token so EnvironmentTreeMaterialFixer's "*tree*" matcher
            // finds the trees, and so an F8 capture reads clearly.
            go.name = "Foliage_" + label + "_" + path.Substring(path.LastIndexOf('/') + 1);
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f));
            go.transform.localScale = Vector3.one;

            // DECORATIVE ONLY -- strip every collider so a prop can never block the hero,
            // never blocks a NavMeshAgent, and can never invalidate the baked navmesh.
            // (Mirrors HubAmbientVfxInjector's AddDecor + Village2Generator.ScatterNatureRing.)
            // Disable first: Destroy is deferred to end-of-frame, and the very next candidate's
            // Physics.CheckSphere would otherwise see this prop's collider.
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                c.enabled = false;
                Destroy(c);
            }

            // Trees only: some packs export lying down (Village2Generator hit "trees off 90 Z").
            // Stand it up ONLY when the mesh is clearly wider than it is tall -- rocks and
            // bushes are naturally wide, so they are never rotated.
            if (tier == Tier.Tree) AutoUprightTree(go);

            float targetH = Mathf.Lerp(minH, maxH, (float)rng.NextDouble());
            float jitter  = 1f + ((float)rng.NextDouble() * 2f - 1f) * ScaleJitter;
            ScaleToHeight(go, targetH * jitter);
            SeatOnGround(go, ground.y);

            ApplyRendererSettings(go, shadows, instancedMaterials);
            return true;
        }

        // ---- prefab cache (one Resources.Load per path, one warning per miss) ----
        private static readonly Dictionary<string, GameObject> s_cache = new Dictionary<string, GameObject>();

        private static GameObject LoadCached(string path)
        {
            if (s_cache.TryGetValue(path, out var cached)) return cached;

            GameObject prefab = null;
            try { prefab = Resources.Load<GameObject>(path); }
            catch (Exception e)
            {
                Debug.LogWarning("[HubFoliage] Resources.Load('" + path + "') threw: " + e.Message);
            }

            if (prefab == null)
            {
                // CLAUDE.md SS4: a missing prop is a WARNING, never an error and never a throw --
                // the art pack may simply not be imported on this machine.
                Debug.LogWarning("[HubFoliage] prop not found at Resources/" + path +
                                 " -- skipping it (art pack may not be imported on this clone).");
            }
            s_cache[path] = prefab;   // cache the miss too, so we warn once
            return prefab;
        }

        // =====================================================================
        //  Renderer / material settings (mobile)
        // =====================================================================

        private static void ApplyRendererSettings(GameObject go, bool castShadows, HashSet<Material> instanced)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                r.shadowCastingMode = castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = castShadows;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;       // runtime-spawned: no baked probes anyway
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                // GPU INSTANCING -- the whole scatter is N clones of a handful of
                // mesh+material pairs, which is the exact case instancing collapses
                // into one draw. sharedMaterials (never .materials) so we do not leak
                // a per-instance material clone per prop.
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || instanced.Contains(m)) continue;
                    instanced.Add(m);
                    try { m.enableInstancing = true; }
                    catch (Exception e)
                    {
                        FlowTrace.Warn("HubFoliage",
                            "could not enable GPU instancing on material '" + m.name + "': " + e.Message);
                    }
                }
            }
        }

        // =====================================================================
        //  Keep-out zones
        // =====================================================================

        private struct KeepOut
        {
            public Vector3 Center;   // world, y ignored
            public float   Radius;
        }

        // Build the keep-out set from the LIVE scene, by COMPONENT (never by a hardcoded
        // building list):
        //   * the Heart of Elarion / Tree of Life -- a wide clear plaza ring
        //   * EVERY non-trigger Collider that is not the ground -- that is every building,
        //     wall, tower, gate, mine node, harvest site, NPC and baked prop, without this
        //     file needing to know any of their type names
        //   * the walked ROUTES from the hub centre out to each gate landing, expanded into
        //     corridors along the real NavMesh path when one can be computed
        private void CollectKeepOuts(List<KeepOut> keepOuts)
        {
            // --- Heart / plaza ---
            Vector3 heart = ResolveHeartCenter();
            keepOuts.Add(new KeepOut { Center = heart, Radius = HeartClearRadius });

            // --- every solid thing already in the scene ---
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var c in colliders)
            {
                if (c == null || c.isTrigger) continue;
                if (IsGroundLike(c)) continue;

                Bounds b = c.bounds;
                float radius = Mathf.Max(b.extents.x, b.extents.z) + StructureClearance;
                if (radius <= 0f || radius > MaxStructureRadius) continue;   // mega-collider = ground plane
                keepOuts.Add(new KeepOut { Center = b.center, Radius = radius });
            }

            // --- walked routes: hub centre -> each gate landing ---
            foreach (var anchor in RouteAnchors)
                AddRouteCorridor(keepOuts, heart, anchor);
        }

        // Expand the route from 'from' to 'to' into a chain of keep-out spheres. Uses the REAL
        // NavMesh path when the navmesh is available (so the corridor follows the walkable route,
        // not a straight line through a wall); falls back to the straight segment otherwise.
        private static void AddRouteCorridor(List<KeepOut> keepOuts, Vector3 from, Vector3 to)
        {
            Vector3[] corners = null;
            try
            {
                var path = new NavMeshPath();
                if (NavMesh.SamplePosition(from, out NavMeshHit a, NavSampleRadius, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(to,   out NavMeshHit b, NavSampleRadius, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) &&
                    path.corners != null && path.corners.Length >= 2)
                {
                    corners = path.corners;
                }
            }
            catch (Exception e)
            {
                FlowTrace.Warn("HubFoliage", "NavMesh route sample failed (using straight corridor): " + e.Message);
            }

            if (corners == null) corners = new[] { from, to };

            for (int i = 0; i < corners.Length - 1; i++)
                StampSegment(keepOuts, corners[i], corners[i + 1]);
        }

        // Lay overlapping spheres along a segment so the whole corridor is covered.
        private static void StampSegment(List<KeepOut> keepOuts, Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            if (len <= 0.01f)
            {
                keepOuts.Add(new KeepOut { Center = a, Radius = RouteCorridorRadius });
                return;
            }
            int steps = Mathf.Clamp(Mathf.CeilToInt(len / RouteCorridorRadius), 1, 128);
            for (int s = 0; s <= steps; s++)
                keepOuts.Add(new KeepOut { Center = Vector3.Lerp(a, b, s / (float)steps), Radius = RouteCorridorRadius });
        }

        private static bool IsBlocked(Vector3 pos, List<KeepOut> keepOuts)
        {
            for (int i = 0; i < keepOuts.Count; i++)
            {
                var k = keepOuts[i];
                float dx = pos.x - k.Center.x;
                float dz = pos.z - k.Center.z;
                if (dx * dx + dz * dz < k.Radius * k.Radius) return true;
            }
            return false;
        }

        private static bool TooClose(Vector3 pos, List<Vector3> placed)
        {
            float min2 = MinPropSpacing * MinPropSpacing;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = pos.x - placed[i].x;
                float dz = pos.z - placed[i].z;
                if (dx * dx + dz * dz < min2) return true;
            }
            return false;
        }

        // Terrain, the invisible walkable navmesh floor and any other world-sized plane are
        // the SURFACE we scatter ON, not a keep-out. Everything else is.
        private static bool IsGroundLike(Collider c)
        {
            if (c is TerrainCollider) return true;
            string n = c.gameObject.name;
            if (!string.IsNullOrEmpty(n))
            {
                string lower = n.ToLowerInvariant();
                if (lower.Contains("terrain") || lower.Contains("ground") ||
                    lower.Contains("navmesh") || lower.Contains("floor")) return true;
            }
            Bounds b = c.bounds;
            return b.size.x > MaxStructureRadius * 2f && b.size.z > MaxStructureRadius * 2f;
        }

        private static Vector3 ResolveHeartCenter()
        {
            var heart = FindAnyObjectByType<DeNelle.Village.HeartController>();
            if (heart != null) return heart.transform.position;

            var anchor = GameObject.Find("HeartOfElarion");
            if (anchor != null) return anchor.transform.position;

            var visual = GameObject.Find("TreeOfLife_Visual");
            if (visual != null) return visual.transform.position;

            return HeartCenterFallback;
        }

        // =====================================================================
        //  Ground resolution
        // =====================================================================

        // Drop a ray from high above the candidate. Rejects: no ground at all, water/moat
        // (below MinGroundY), and anything steeper than MaxGroundSlopeDeg. Falls back to a
        // NavMesh sample when there is no collider (a navmesh-only surface).
        private static bool TryResolveGround(Vector3 flat, out Vector3 ground)
        {
            ground = flat;
            Vector3 origin = new Vector3(flat.x, GroundProbeStartY, flat.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeLength,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y < MinGroundY) return false;                       // moat / water / void
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > MaxGroundSlopeDeg) return false;                      // cliff face
                ground = hit.point;
                return true;
            }

            // No collider under the candidate -- try the navmesh (some floors are nav-only).
            if (NavMesh.SamplePosition(new Vector3(flat.x, 0f, flat.z), out NavMeshHit nh,
                                       NavSampleRadius, NavMesh.AllAreas))
            {
                if (nh.position.y < MinGroundY) return false;
                ground = nh.position;
                return true;
            }
            return false;
        }

        // =====================================================================
        //  Placement helpers (bounds-derived, art-independent) -- mirrors the
        //  proven Village2Generator.ScatterNatureRing helpers.
        // =====================================================================

        // Stand a lying tree up: if its widest horizontal axis is much larger than its
        // height, rotate that axis to vertical. Guarded so a rock-shaped tree is left alone.
        private static void AutoUprightTree(GameObject go)
        {
            if (!TryMeasureBounds(go.transform, out Bounds b)) return;
            float h = b.size.y;
            float wx = b.size.x, wz = b.size.z;
            float widest = Mathf.Max(wx, wz);
            if (h >= widest * 0.6f) return;   // already tall enough -- leave it alone

            // Rotate the widest horizontal axis up.
            if (wx >= wz) go.transform.Rotate(0f, 0f, 90f, Space.Self);
            else          go.transform.Rotate(90f, 0f, 0f, Space.Self);
        }

        private static void ScaleToHeight(GameObject go, float targetHeight)
        {
            if (targetHeight <= 0f) return;
            if (!TryMeasureBounds(go.transform, out Bounds b)) return;
            if (b.size.y <= 0.0001f) return;
            float k = targetHeight / b.size.y;
            k = Mathf.Clamp(k, 0.01f, 100f);
            go.transform.localScale = go.transform.localScale * k;
        }

        private static void SeatOnGround(GameObject go, float groundY)
        {
            if (!TryMeasureBounds(go.transform, out Bounds b)) return;
            float delta = groundY - b.min.y;
            go.transform.position += new Vector3(0f, delta, 0f);
        }

        // Encapsulate all child renderer bounds. False if none (pack not imported).
        private static bool TryMeasureBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
