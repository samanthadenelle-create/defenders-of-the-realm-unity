// =============================================================================
// RuntimeRegionGate — the RUNTIME variant of the WO-467 RegionGate primitive.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner ask (2026-06-23, "world seam still broken" x3): build the castle<->OuterWorld
// crossing FROM A RECIPE AT RUNTIME — no editor bake, no .unity hand-edit (CLAUDE.md §3).
// The editor RegionGateBuilder (queued) writes the deck+trigger into MainCastle_Hall.unity
// and bakes offline; that re-touches a hand-dialed scene every time geometry moves (the
// −572 → origin re-center, WO-483, invalidated every baked coord). This component instead
// reads region-gates.json and ASSEMBLES the crossing on scene load from primitives + a
// runtime NavMeshSurface re-bake, so a coord change is a DATA edit, never a re-bake.
//
// Self-bootstrapping like WorldSceneLoader (AfterSceneLoad + sceneLoaded subscription,
// with the same domain-reload-off guard reset) so NO scene wiring is needed. For each
// recipe row whose `from` == the active hub scene it builds, under __RuntimeSeam_<id>:
//   1. Walkable approach DECK welded to the source navmesh (lift CreateInvisibleFloor +
//      overrideArea=Walkable) → runtime NavMeshSurface.BuildNavMesh() (no editor bake).
//   2. SceneTransitionTrigger seated at the threshold (deck level) — reused verbatim,
//      only transform + the 4 public fields set.
//   3. HeroLinkCrossing entry/destination PAIR sharing the gate id (GUID backbone, WO-479).
//   4. Gate-funnel choke panels (BoxCollider + NavMeshObstacle carve) at the arch edges.
//   5. A narrow cross-scene NavMeshLink for AI — built ONLY once OuterWorld is additive
//      (the far endpoint is live at runtime; impossible at bake time). HERO path stays the
//      masked warp; the link is the AI path.
//
// Coords are SOURCED, never hardcoded: south-gate from the castle-south recipe at runtime;
// the OuterWorld LANDING from DeNelle.Core.World.WorldGeometry (WO-483) by reflection if it
// exists, else a loud-warned pre-WO-483 fallback (the same value the committed donor seam
// used). When WorldGeometry lands the literal is dropped automatically — no edit here.
//
// Instrumented per §12: [Flow:RuntimeSeam] Step/Warn/Fail + Guard.Try on every risky op.
// Idempotent (destroys a prior __RuntimeSeam_<id> first); safe no-op off a hub scene.
// Flag-gated reversible: FeatureFlags.RuntimeWorldSeam (PlayerPrefs ff.runtimeworldseam).
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime builder/host for ONE recipe-driven region crossing. One instance per built
    /// gate; the static bootstrap spins one up per matching recipe row on a hub scene.
    /// </summary>
    public sealed class RuntimeRegionGate : MonoBehaviour
    {
        private const string OuterWorldSceneName = "OuterWorld";
        private const string RecipeResourcePath  = "Data/region-gates";   // Resources/Data/region-gates.json

        // Pre-WO-483 fallback only — the donor AddCastleBridgeSeam warped to (gateX, 0.5, -66)
        // and BuildSeamlessOuterWorldSeam linked the OuterWorld north edge near z=-76. We read the
        // real value from WorldGeometry when it exists; this is the loud-warned stopgap until then.
        private static readonly Vector3 FallbackLandingNoGeom = new Vector3(-4.37f, 0.5f, -66f);

        // --------------------------------------------------------------------
        //  SELF-BOOTSTRAP (mirrors WorldSceneLoader: AfterSceneLoad + sceneLoaded).
        // --------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGuard() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;   // de-dupe across domain reloads
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Handle the case where a hub is already the active scene right now.
            TryBuildForScene(SceneManager.GetActiveScene(), "Init-active");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) =>
            TryBuildForScene(scene, "sceneLoaded");

        private static void TryBuildForScene(Scene scene, string via)
        {
            if (!FeatureFlags.RuntimeWorldSeam)
            {
                FlowTrace.Once("RuntimeSeam", "flag-off",
                    "RuntimeWorldSeam flag OFF (ff.runtimeworldseam=0) — runtime seam not built (editor-baked seam in effect).");
                return;
            }
            if (!HubScenes.IsHub(scene.name))
                return;   // only build crossings whose `from` is a hub; silent no-op elsewhere

            var rows = LoadRecipe();
            if (rows == null || rows.Count == 0)
            {
                FlowTrace.Warn("RuntimeSeam",
                    $"({via}) no region-gates.json rows loaded — no runtime crossing built on '{scene.name}'.");
                return;
            }

            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrEmpty(row.id)) continue;
                if (!string.Equals(row.from, scene.name, System.StringComparison.Ordinal)) continue;

                string hostName = "__RuntimeSeam_" + row.id;
                // Idempotent: destroy a prior subtree of this id first (re-entry / editor play-twice).
                var prior = GameObject.Find(hostName);
                if (prior != null) Object.Destroy(prior);

                var host = new GameObject(hostName);
                host.transform.position = Vector3.zero;   // host at origin → local == world for endpoints
                var gate = host.AddComponent<RuntimeRegionGate>();
                gate._recipe = row;
                FlowTrace.Step("RuntimeSeam",
                    $"({via}) building runtime crossing '{row.id}' on '{scene.name}' -> '{row.to}' (type={row.type}, loadMode={row.loadMode}).");
            }
        }

        // --------------------------------------------------------------------
        //  INSTANCE BUILD
        // --------------------------------------------------------------------
        private GateRecipeRow _recipe;
        private Vector3 _gatePos;          // recipe south gate (castle-local, unchanged by re-center)
        private float   _thresholdZ;       // deck far end (trigger + entry marker)
        private Vector3 _landing;          // OuterWorld landing (from WorldGeometry, else fallback)
        private bool    _aiLinkBuilt;

        private void Start()
        {
            using var _ = FlowTrace.Enter("RuntimeSeam", $"Build crossing '{_recipe?.id}'");
            if (_recipe == null) { FlowTrace.Fail("RuntimeSeam", "no recipe on host — abort."); return; }

            // 1) Source coords at runtime — NEVER hardcode the source gate or the landing.
            _gatePos = ReadSouthGatePos();
            float backFromGate = _recipe.thresholdBackFromGate > 0.01f ? _recipe.thresholdBackFromGate : 22f;
            _thresholdZ = _gatePos.z - backFromGate;       // out past the gate, on the castle deck
            _landing = ResolveOuterWorldLanding();
            FlowTrace.Step("RuntimeSeam",
                $"coords: gate={_gatePos} thresholdZ={_thresholdZ:F1} landing={_landing} (gate=recipe, landing=WorldGeometry-or-fallback).");

            float width = _recipe.approachWidth > 0.01f ? _recipe.approachWidth : 7f;

            // 2) Walkable approach deck welded to the source navmesh + runtime re-bake.
            GameObject deck = BuildApproachDeck(width);

            // 3) Threshold SceneTransitionTrigger (hero masked-warp) at the deck far end.
            BuildThresholdTrigger();

            // 4) HeroLinkCrossing entry/destination pair (GUID-keyed).
            BuildHeroCrossingPair();

            // 5) Gate-funnel choke panels at the arch edges.
            BuildFunnelPanels(width);

            // Runtime navmesh re-bake of the source surface so the deck welds + is on-mesh.
            RebakeSourceSurface();

            // Build-time reachability assert (tight tolerance — no stacked false-green).
            AssertApproachWelded();

            // 6) AI cross-scene link — only once OuterWorld is additive-loaded.
            if (SceneManager.GetSceneByName(OuterWorldSceneName).isLoaded)
                BuildAiLink(width);
            else
            {
                FlowTrace.Step("RuntimeSeam",
                    "OuterWorld not additive yet — deferring AI NavMeshLink until it loads (subscribing sceneLoaded).");
                SceneManager.sceneLoaded += OnOuterWorldLoaded;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnOuterWorldLoaded;
        }

        private void OnOuterWorldLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != OuterWorldSceneName || _aiLinkBuilt) return;
            float width = _recipe != null && _recipe.approachWidth > 0.01f ? _recipe.approachWidth : 7f;
            BuildAiLink(width);
            SceneManager.sceneLoaded -= OnOuterWorldLoaded;
        }

        // --------------------------------------------------------------------
        //  PART 1 — walkable approach deck (lift CreateInvisibleFloor body).
        //  Spans gate-Z → threshold-Z with the proven +overlap into the courtyard so the
        //  runtime bake FUSES it with the source navmesh (the 2026-06-19 weld lesson).
        // --------------------------------------------------------------------
        private GameObject BuildApproachDeck(float width)
        {
            GameObject deck = null;
            Guard.Try("RuntimeSeam", "build approach deck", () =>
            {
                float overlap   = _recipe.approachOverlap > 0.01f ? _recipe.approachOverlap : 6f;
                float centreZ   = (_gatePos.z + _thresholdZ) * 0.5f;
                float lenZ      = Mathf.Abs(_gatePos.z - _thresholdZ) + overlap;   // +overlap welds to courtyard
                // Unity plane = 10m/unit: scale.z = len/10, scale.x = width/10.
                deck = CreateInvisibleWalkableFloor(transform, "RuntimeSeam_Deck_Nav",
                    new Vector3(_gatePos.x, _gatePos.y, centreZ),
                    new Vector3(width / 10f, 1f, lenZ / 10f));
                FlowTrace.Step("RuntimeSeam",
                    $"deck: centre=({_gatePos.x:F2},{_gatePos.y:F2},{centreZ:F1}) len≈{lenZ:F1}m width={width}m (gate {_gatePos.z:F1} → threshold {_thresholdZ:F1}, +{overlap}m courtyard overlap).");
            });
            if (deck == null)
                FlowTrace.Fail("RuntimeSeam", "approach deck FAILED to build — the crossing cannot weld; hero will hit the navmesh edge.");
            return deck;
        }

        // Lift of CastleHubBuilder.CreateInvisibleFloor + AddWalkableNavMeshModifier, runtime form:
        // a Plane (MeshCollider), renderer disabled, NavMeshModifier overrideArea=Walkable so the gate
        // arch can't carve it. Runtime can reference Unity.AI.Navigation directly (asmdef ref).
        private static GameObject CreateInvisibleWalkableFloor(Transform parent, string name,
            Vector3 worldPos, Vector3 localScale)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); // MeshFilter + MeshRenderer + MeshCollider
            plane.name = name;
            plane.transform.SetParent(parent, false);
            plane.transform.position = worldPos;
            plane.transform.localScale = localScale;

            var r = plane.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;                       // invisible; bake reads the MeshCollider
            if (plane.GetComponent<MeshCollider>() == null) plane.AddComponent<MeshCollider>();

            // FORCE walkable so an overlapping gate arch can't carve/seal the deck (WO-168 lesson).
            var mod = plane.GetComponent<NavMeshModifier>();
            if (mod == null) mod = plane.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = 0;                                            // 0 = Walkable
            return plane;
        }

        // --------------------------------------------------------------------
        //  PART 2 — threshold SceneTransitionTrigger (hero masked-warp).
        //  REUSED verbatim — only transform + the 4 public fields set (per the WO "do not
        //  change its field wiring" rule). Seated AT the deck (deck level, not floating).
        // --------------------------------------------------------------------
        private void BuildThresholdTrigger()
        {
            Guard.Try("RuntimeSeam", "seat SceneTransitionTrigger", () =>
            {
                var go = new GameObject("RuntimeSeam_Trigger");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(_gatePos.x, _gatePos.y, _thresholdZ);

                var trig = go.AddComponent<SceneTransitionTrigger>();
                trig.targetSceneName = _recipe.to;
                trig.targetPosition  = _landing;
                trig.loadAdditive    = !string.Equals(_recipe.loadMode, "single", System.StringComparison.OrdinalIgnoreCase);
                trig.ProximityRadius = _recipe.triggerRadius > 0.01f ? _recipe.triggerRadius : 6f;
                FlowTrace.Step("RuntimeSeam",
                    $"trigger seated @({_gatePos.x:F2},{_gatePos.y:F2},{_thresholdZ:F1}) -> '{_recipe.to}'@{_landing} additive={trig.loadAdditive} r={trig.ProximityRadius}.");
            });
        }

        // --------------------------------------------------------------------
        //  PART 3 — HeroLinkCrossing entry/destination pair, GUID-keyed by gate id.
        //  REUSED verbatim (id-paired distance-independent warp via Partner()).
        // --------------------------------------------------------------------
        private void BuildHeroCrossingPair()
        {
            Guard.Try("RuntimeSeam", "place HeroLinkCrossing pair", () =>
            {
                string id = "rgate_" + _recipe.id;

                var entry = new GameObject("RuntimeSeam_HeroLink_Entry");
                entry.transform.SetParent(transform, false);
                entry.transform.position = new Vector3(_gatePos.x, _gatePos.y, _thresholdZ);
                var ec = entry.AddComponent<HeroLinkCrossing>();
                ec.crossingId = id;
                ec.bidirectional = true;

                var dest = new GameObject("RuntimeSeam_HeroLink_Dest");
                dest.transform.SetParent(transform, false);
                dest.transform.position = _landing;
                var dc = dest.AddComponent<HeroLinkCrossing>();
                dc.crossingId = id;
                dc.bidirectional = true;

                FlowTrace.Step("RuntimeSeam",
                    $"HeroLinkCrossing pair '{id}' — entry@{entry.transform.position} dest@{dest.transform.position}.");
            });
        }

        // --------------------------------------------------------------------
        //  PART 4 — gate-funnel choke panels (WO-479): two thin invisible vertical
        //  panels (BoxCollider + carving NavMeshObstacle) at the inner arch edges so
        //  navmesh + physics route ONLY through the opening. Auto-fit from arch width.
        // --------------------------------------------------------------------
        private void BuildFunnelPanels(float width)
        {
            Guard.Try("RuntimeSeam", "build funnel panels", () =>
            {
                float half = width * 0.5f;
                // Place panels just OUTSIDE the opening edges (±half) on the gate line; thin in X,
                // tall in Y, deep enough in Z to seal the side gaps along the approach.
                float panelZ = _gatePos.z;
                BuildPanel($"RuntimeSeam_Funnel_L", new Vector3(_gatePos.x - half - 0.5f, _gatePos.y + 1.5f, panelZ));
                BuildPanel($"RuntimeSeam_Funnel_R", new Vector3(_gatePos.x + half + 0.5f, _gatePos.y + 1.5f, panelZ));
                FlowTrace.Step("RuntimeSeam",
                    $"funnel panels at ±{half + 0.5f:F1}m of gate x={_gatePos.x:F2} — navmesh+physics route through the opening only.");
            });
        }

        private void BuildPanel(string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 3f, 6f);                 // thin X, tall Y, deep Z (seals the side gap)

            var obs = go.AddComponent<NavMeshObstacle>();
            obs.shape   = NavMeshObstacleShape.Box;
            obs.size    = box.size;
            obs.carving = true;                                 // carve so navmesh routes through the opening
        }

        // --------------------------------------------------------------------
        //  Runtime navmesh re-bake of JUST the source surface (no editor bake).
        //  A local NavMeshSurface over the seam host + the source courtyard so the deck welds.
        //  Proven runtime path (ArenaNavMeshBaker.BuildNavMesh sync).
        // --------------------------------------------------------------------
        private void RebakeSourceSurface()
        {
            Guard.Try("RuntimeSeam", "runtime NavMeshSurface re-bake", () =>
            {
                // Collect from the WHOLE scene (deck + courtyard floor) so the deck FUSES with the
                // existing castle navmesh rather than baking an isolated island. PhysicsColliders so
                // the renderer-off deck is picked up; agent type 0 (the shared hero/enemy agent).
                var surf = gameObject.GetComponent<NavMeshSurface>();
                if (surf == null) surf = gameObject.AddComponent<NavMeshSurface>();
                surf.collectObjects = CollectObjects.All;

                // BAKE FROM PHYSICS COLLIDERS, NOT RENDER MESHES — matches the WORKING
                // ArenaNavMeshBaker (useGeometry=PhysicsColliders, agentTypeID=0). DATA-PROVEN
                // (fleet build run): the prior bake dragged in the polyperfect castle RENDER MESHES
                // (wall-medieval_stone / tower-castle_round / UCX_Floor_WoodDark_1 …), which require
                // "Read/Write Enabled" on import they don't have -> RuntimeNavMeshBuilder SKIPPED
                // 150+ sources ("is skipped because it does not allow read access"), leaving the seam
                // navmesh incomplete so the crossing wouldn't path.
                surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surf.agentTypeID = 0;

                // EXCLUDE the "Structure" layer (the castle walls/gates/towers live there per
                // CastleHubBuilder WO-449) so the bake never even visits those non-readable imported
                // meshes — the read-access skip spam disappears and the source set stays clean. The
                // weld geometry (the renderer-off invisible nav planes: this deck + the courtyard
                // NavMeshFloor_Invisible_Walkable) sit on the Default layer and ARE collected, with
                // their primitive MeshColliders (readable) — so deck + courtyard still FUSE. Their
                // carving NavMeshObstacles cut the walls back out, exactly as the editor bake did.
                int structureLayer = LayerMask.NameToLayer("Structure");
                surf.layerMask = structureLayer >= 0 ? ~(1 << structureLayer) : ~0;

                // Voxel/region tuning copied from ArenaNavMeshBaker (the proven runtime bake): a
                // finer voxel for a tight local weld + prune stray islands smaller than MinRegionArea.
                surf.overrideVoxelSize = true;
                surf.voxelSize         = 0.18f;   // ArenaNavMeshBaker.VoxelSize
                surf.minRegionArea     = 0.5f;    // ArenaNavMeshBaker.MinRegionArea

                surf.BuildNavMesh();
                FlowTrace.Step("RuntimeSeam",
                    $"runtime NavMeshSurface.BuildNavMesh() — useGeometry=PhysicsColliders, layerMask excludes 'Structure'({structureLayer}) " +
                    "so non-readable polyperfect render meshes are NOT collected; deck welded into source navmesh from colliders (no editor bake, no read-access skips).");
            });
        }

        // Build-time reachability oracle (tight tolerance so a snap onto a stacked far-scene
        // navmesh can't false-green — the 2026-06-19 lesson). Emit RUNTIME_SEAM_NAV_OK/_FAIL.
        private void AssertApproachWelded()
        {
            Guard.Try("RuntimeSeam", "assert deck welded to source navmesh", () =>
            {
                // Source courtyard reference point (origin = castle centre) → the threshold trigger.
                var courtyard = new Vector3(0f, _gatePos.y, _gatePos.z + 4f);     // just inside the gate
                var threshold = new Vector3(_gatePos.x, _gatePos.y, _thresholdZ);

                bool sStart = NavMesh.SamplePosition(courtyard, out NavMeshHit hStart, 5f, NavMesh.AllAreas);
                bool sEnd   = NavMesh.SamplePosition(threshold, out NavMeshHit hEnd, 1.0f, NavMesh.AllAreas);
                if (!sStart || !sEnd)
                {
                    FlowTrace.Fail("RuntimeSeam",
                        $"RUNTIME_SEAM_NAV_FAIL — could not sample courtyard(onMesh={sStart}) or threshold(onMesh={sEnd}) within tolerance. " +
                        "The deck did NOT bake walkable / weld (SEAM-OFF-MESH). FIX: widen approachOverlap so the runtime bake fuses deck+courtyard.");
                    return;
                }
                var path = new NavMeshPath();
                NavMesh.CalculatePath(hStart.position, hEnd.position, NavMesh.AllAreas, path);
                if (path.status == NavMeshPathStatus.PathComplete)
                    FlowTrace.Step("RuntimeSeam",
                        $"RUNTIME_SEAM_NAV_OK — PATH-COMPLETE courtyard→threshold({_gatePos.x:F2},{_gatePos.y:F2},{_thresholdZ:F1}); deck welds to source navmesh, hero walks to the trigger.");
                else
                    FlowTrace.Fail("RuntimeSeam",
                        $"RUNTIME_SEAM_NAV_FAIL — courtyard→threshold path is {path.status}; deck did NOT weld (gap between gate and deck). " +
                        "FIX: widen approachOverlap so the runtime bake fuses them into one surface (SEAM-OFF-MESH).");
            });
        }

        // --------------------------------------------------------------------
        //  PART 5 — AI cross-scene NavMeshLink (lift BuildBridgeNavLink body).
        //  Built ONLY once OuterWorld is additive-loaded so the far endpoint lands on a LIVE
        //  navmesh (the runtime-only capability the editor bake never had). Hero path stays the
        //  masked warp; this is the AI path (reps/troops).
        // --------------------------------------------------------------------
        private void BuildAiLink(float width)
        {
            if (_aiLinkBuilt) return;
            bool ok = Guard.Try("RuntimeSeam", "build AI NavMeshLink (OuterWorld additive)", () =>
            {
                var hostGo = new GameObject("RuntimeSeam_AiNavLink");
                hostGo.transform.SetParent(transform, false);
                hostGo.transform.position = Vector3.zero;     // endpoints absolute (link space == host @ origin)

                var link = hostGo.AddComponent<NavMeshLink>();
                if (link == null) throw new System.Exception("AddComponent<NavMeshLink> returned null.");
                // Start on the source deck just past the threshold; end at the OuterWorld landing.
                link.startPoint    = new Vector3(_gatePos.x, _gatePos.y, _thresholdZ);
                link.endPoint      = _landing;
                link.width         = width;
                link.bidirectional = true;
                link.area          = 0;                       // Walkable
                link.UpdateLink();
                _aiLinkBuilt = true;
                FlowTrace.Step("RuntimeSeam",
                    $"AI NavMeshLink start={link.startPoint} end={link.endPoint} width={width} — castle↔OuterWorld AI path live (both scenes additive).");
            });
            if (!ok)
                FlowTrace.Fail("RuntimeSeam",
                    "AI NavMeshLink FAILED — reps/troops cannot PATH the crossing. (Hero masked-warp still works; this is the AI-only path.)");
        }

        // --------------------------------------------------------------------
        //  COORD SOURCING — never a stale literal.
        // --------------------------------------------------------------------

        // Read the recipe south gate at RUNTIME from Resources/Data/castle-south-recipe (the same
        // shape CastleHubBuilder.ReadSouthGatePos parses). Castle-local — unchanged by the re-center.
        private static Vector3 ReadSouthGatePos()
        {
            Vector3 fallback = new Vector3(-4.37f, 0f, -40.6f);
            var ta = Resources.Load<TextAsset>("Data/castle-south-recipe");
            if (ta == null)
            {
                FlowTrace.Warn("RuntimeSeam", "castle-south-recipe not found — using fallback south gate " + fallback + ".");
                return fallback;
            }
            var recipe = JsonUtility.FromJson<SouthRecipe>(ta.text);
            if (recipe != null && recipe.pieces != null)
                foreach (var p in recipe.pieces)
                    if (p != null && p.name == "Gate_South" && p.pos != null && p.pos.Length == 3)
                        return new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            FlowTrace.Warn("RuntimeSeam", "Gate_South not in recipe — using fallback " + fallback + ".");
            return fallback;
        }

        // The OuterWorld landing is the WO-483 single-source-of-truth coord. WorldGeometry doesn't
        // exist yet, so resolve by REFLECTION (so no edit is needed when it lands) and loud-warn a
        // pre-WO-483 fallback if it's absent. Recognized member names (any one): a static Vector3
        // field/property SouthGateSeamLanding, or a method SouthGateSeamLanding().
        private static Vector3 ResolveOuterWorldLanding()
        {
            Vector3 found = Vector3.zero;
            bool got = false;
            Guard.Try("RuntimeSeam", "resolve WorldGeometry.SouthGateSeamLanding", () =>
            {
                var t = System.Type.GetType("DeNelle.Core.World.WorldGeometry, DeNelle.Core");
                if (t == null) return;
                const System.Reflection.BindingFlags F =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                var prop = t.GetProperty("SouthGateSeamLanding", F);
                if (prop != null && prop.PropertyType == typeof(Vector3))
                { found = (Vector3)prop.GetValue(null); got = true; return; }
                var field = t.GetField("SouthGateSeamLanding", F);
                if (field != null && field.FieldType == typeof(Vector3))
                { found = (Vector3)field.GetValue(null); got = true; return; }
                var m = t.GetMethod("SouthGateSeamLanding", F, null, System.Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(Vector3))
                { found = (Vector3)m.Invoke(null, null); got = true; }
            });

            if (got)
            {
                FlowTrace.Step("RuntimeSeam", $"landing from WorldGeometry.SouthGateSeamLanding = {found} (origin-centered, WO-483).");
                return found;
            }
            FlowTrace.Warn("RuntimeSeam",
                $"WorldGeometry.SouthGateSeamLanding NOT present (WO-483 not landed) — using PRE-WO-483 FALLBACK landing {FallbackLandingNoGeom}. " +
                "This is a stopgap: when WorldGeometry lands, the landing auto-sources from it (no edit). Verify the hero emerges inside the OuterWorld terrain.");
            return FallbackLandingNoGeom;
        }

        // --------------------------------------------------------------------
        //  RECIPE MODEL (JsonUtility-friendly; flat scalars).
        // --------------------------------------------------------------------
        private static List<GateRecipeRow> LoadRecipe()
        {
            List<GateRecipeRow> rows = null;
            Guard.Try("RuntimeSeam", "load region-gates.json", () =>
            {
                var ta = Resources.Load<TextAsset>(RecipeResourcePath);
                if (ta == null)
                {
                    FlowTrace.Warn("RuntimeSeam", "region-gates.json not found in Resources/Data — no runtime crossings.");
                    return;
                }
                var wrap = JsonUtility.FromJson<GateRecipeFile>(ta.text);
                if (wrap != null && wrap.gates != null)
                    rows = new List<GateRecipeRow>(wrap.gates);
            });
            return rows;
        }

        [System.Serializable]
        private class GateRecipeFile { public GateRecipeRow[] gates; }

        [System.Serializable]
        private class GateRecipeRow
        {
            public string id;
            public string type;                 // tunnel | pass | bridge
            public string loadMode;             // warp | stream | single
            public string from;
            public string to;
            public float  approachWidth;
            public float  approachOverlap;
            public float  thresholdBackFromGate;
            public float  triggerRadius;
            public bool   occluder;
        }

        [System.Serializable] private class SouthPiece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [System.Serializable] private class SouthRecipe { public SouthPiece[] pieces; public float[] parentPos; public float[] parentRot; }
    }
}
