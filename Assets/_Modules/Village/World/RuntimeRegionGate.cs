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
                    $"({via}) building runtime crossing '{row.id}' [{SideName(row.facingYaw)} facingYaw={row.facingYaw:F0}] on '{scene.name}' -> '{row.to}' (type={row.type}, loadMode={row.loadMode}).");
            }
        }

        // --------------------------------------------------------------------
        //  INSTANCE BUILD
        // --------------------------------------------------------------------
        private GateRecipeRow _recipe;
        private Vector3 _gatePos;          // recipe SOUTH-FRAME gate (castle-local, unchanged by re-center). NOT pre-rotated.
        private float   _thresholdZ;       // deck far end (trigger + entry marker), SOUTH-FRAME Z
        private float   _thresholdY;       // deck-surface Y AT the threshold (WO-593 sloped descent low end;
                                           // == _gatePos.y until BuildApproachDeck derives the slope)
        private Vector3 _landing;          // OuterWorld landing (SOUTH-FRAME; from WorldGeometry, else fallback)
        private bool    _aiLinkBuilt;
        private bool    _returnBuilt;      // WO-602: OUTER return entrance built once (Start-or-OnOuterWorldLoaded)

        // 4-SIDE SUPPORT (host-rotation about origin). All south math is authored in the SOUTH frame
        // and converted to world by ToWorld() = _yawRot * southPoint (host sits at origin, so a yaw
        // rotation about Y == rotation about the castle centre). facingYaw maps south onto each side:
        //   South yaw=0  | West yaw=90 | North yaw=180 | East yaw=270.  REGRESSION GUARD: at yaw=0,
        // _yawRot == identity so ToWorld(p) == p exactly (identity*vector is a bit-exact passthrough)
        // and every oriented child gets identity rotation — the build is mathematically identical to
        // the pre-4-side south-only build.
        private float      _facingYaw;
        private Quaternion _yawRot = Quaternion.identity;

        // Convert a SOUTH-FRAME point to world by rotating it about the castle origin by facingYaw.
        private Vector3 ToWorld(Vector3 southLocal) => _yawRot * southLocal;

        private static string SideName(float yaw)
        {
            int y = Mathf.RoundToInt(yaw) % 360; if (y < 0) y += 360;
            switch (y) { case 0: return "South"; case 90: return "West"; case 180: return "North"; case 270: return "East"; default: return "yaw" + yaw.ToString("F0"); }
        }

        private void Start()
        {
            using var _ = FlowTrace.Enter("RuntimeSeam", $"Build crossing '{_recipe?.id}'");
            if (_recipe == null) { FlowTrace.Fail("RuntimeSeam", "no recipe on host — abort."); return; }

            // 0) Side selection — host-rotation about origin. facingYaw rotates the proven SOUTH build
            //    onto this gate's side (S=0/W=90/N=180/E=270). yaw=0 => identity => south build unchanged.
            _facingYaw = _recipe.facingYaw;
            _yawRot    = Quaternion.Euler(0f, _facingYaw, 0f);
            string side = SideName(_facingYaw);

            // 1) Source coords at runtime — NEVER hardcode the source gate or the landing. These stay in
            //    the SOUTH FRAME; ToWorld() rotates each authored point onto this side at placement time.
            _gatePos = ReadSouthGatePos();

            // WO-593 island raise (§12 captured proof, break-log 2026-07-02): SPAWN_TO_GATE_FAIL on all
            // 4 sides — threshold(…, 0.00, ±62.60) vs lastCorner y=3.08/3.09: the recipe is authored in
            // the PRE-RAISE castle frame (Gate_South y=0), but CastleWallsFromRecipe.cs:57 lifts the wall
            // parent by CastleHubBuilder.CastleFootprintLiftY (PlayerPrefs "castle.liftY", default 3) and
            // the gate strips/courtyard nav ride at that height. Lift the whole gate chain (deck fallback,
            // threshold trigger, HeroLink entry, funnel panels, beacon, AI-link start, weld asserts) by the
            // SAME base so the courtyard→threshold lane is one welded surface at the real nav height.
            float baseLift = PlayerPrefs.GetFloat("castle.liftY", 3f);
            if (baseLift > 0.01f)
            {
                _gatePos.y += baseLift;
                FlowTrace.Step("RuntimeSeam", "gate chain lifted by castle.liftY=" + baseLift.ToString("0.0") +
                    " (recipe is pre-raise frame; matches CastleWallsFromRecipe wall-parent lift).");
            }
            float backFromGate = _recipe.thresholdBackFromGate > 0.01f ? _recipe.thresholdBackFromGate : 22f;
            _thresholdZ = _gatePos.z - backFromGate;       // out past the gate, on the castle deck
            _thresholdY = _gatePos.y;                      // refined to the sloped-deck low end in BuildApproachDeck
            _landing = ResolveOuterWorldLanding();
            FlowTrace.Step("RuntimeSeam",
                $"coords[{side} facingYaw={_facingYaw:F0}]: gateSouth={_gatePos} thresholdZ={_thresholdZ:F1} landingSouth={_landing} -> WORLD gate={ToWorld(_gatePos)} landing={ToWorld(_landing)} (gate=recipe, landing=WorldGeometry-or-fallback, rotated about castle origin).");

            float width = _recipe.approachWidth > 0.01f ? _recipe.approachWidth : 7f;

            // 2) Walkable approach deck welded to the source navmesh + runtime re-bake.
            GameObject deck = BuildApproachDeck(width);

            // 3) Threshold SceneTransitionTrigger (hero masked-warp) at the deck far end.
            BuildThresholdTrigger();

            // 4) HeroLinkCrossing entry/destination pair (GUID-keyed).
            BuildHeroCrossingPair();

            // 5) Gate-funnel choke panels at the arch edges.
            BuildFunnelPanels(width);

            // 5b) VISIBLE gate beacon so the player can SEE where to cross (findability).
            BuildGateBeacon();

            // Runtime navmesh re-bake of the source surface so the deck welds + is on-mesh.
            RebakeSourceSurface();

            // Build-time reachability assert (tight tolerance — no stacked false-green).
            AssertApproachWelded();

            // 6) AI cross-scene link — only once OuterWorld is additive-loaded.
            if (SceneManager.GetSceneByName(OuterWorldSceneName).isLoaded)
            {
                BuildAiLink(width);
                BuildReturnEntrance(width);   // WO-602: the OUTER→courtyard way back home (needs outer ground live)
                LogSpawnReachability();   // OuterWorld already additive at Start: log full-route reachability now
            }
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
            BuildReturnEntrance(width);   // WO-602: the OUTER→courtyard way back home (outer ground now live)
            // WO-468 4-lane capture: now that BOTH the castle navmesh AND the additive OuterWorld
            // navmesh are live (the real play condition), log whether the hero SPAWN can PATH to this
            // gate's threshold. The Start-time AssertApproachWelded tests only gate-local weld + ran
            // castle-only; this tests the full spawn->gate route with the dual navmesh present, which
            // is what AssertHeroCrossing actually exercises.
            LogSpawnReachability();
            SceneManager.sceneLoaded -= OnOuterWorldLoaded;
        }

        // Per-gate spawn->threshold reachability with all navmeshes live (castle + additive OuterWorld).
        private void LogSpawnReachability()
        {
            Guard.Try("RuntimeSeam", "log spawn->gate reachability", () =>
            {
                var spawnGo = GameObject.Find("HeroStartPoint_PlayerSpawn")
                           ?? GameObject.Find("HeroStartPoint_InsidePersonalQuarters");
                Vector3 spawn = spawnGo != null ? spawnGo.transform.position : Vector3.zero;
                Vector3 threshold = ToWorld(new Vector3(_gatePos.x, _thresholdY, _thresholdZ));   // sloped-deck low end (WO-593)
                bool sSpawn = NavMesh.SamplePosition(spawn, out NavMeshHit hS, 5f, NavMesh.AllAreas);
                bool sThr   = NavMesh.SamplePosition(threshold, out NavMeshHit hT, 2f, NavMesh.AllAreas);
                if (!sSpawn || !sThr)
                {
                    FlowTrace.Fail("RuntimeSeam",
                        $"SPAWN_TO_GATE_FAIL [{SideName(_facingYaw)}] — sample spawn(onMesh={sSpawn})@{spawn} or threshold(onMesh={sThr})@{threshold} failed.");
                    return;
                }
                var path = new NavMeshPath();
                NavMesh.CalculatePath(hS.position, hT.position, NavMesh.AllAreas, path);
                int corners = path.corners != null ? path.corners.Length : 0;
                Vector3 last = corners > 0 ? path.corners[corners - 1] : hS.position;
                float approach = Vector3.Distance(last, hT.position);
                if (path.status == NavMeshPathStatus.PathComplete)
                    FlowTrace.Step("RuntimeSeam",
                        $"SPAWN_TO_GATE_OK [{SideName(_facingYaw)}] — spawn{spawn} -> threshold{threshold} PATH-COMPLETE (approach {approach:F1}m). Hero can walk to this gate.");
                else
                    FlowTrace.Fail("RuntimeSeam",
                        $"SPAWN_TO_GATE_FAIL [{SideName(_facingYaw)}] — spawn{spawn} -> threshold{threshold} is {path.status}, closest {approach:F1}m (lastCorner {last}). " +
                        "The dual castle+OuterWorld navmesh severs this lane (likely a structure pinch or dual-sheet edge). This is the AssertHeroCrossing failure.");
            });
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
                // WO-435: make the VISIBLE bridge deck physically solid + capture the collider/
                // navmesh-Y diagnostics FIRST — this must run BEFORE the sloped-run raycast below
                // (line ~346) so that raycast lands on the deck TOP (not the terrain under it),
                // which is what pins the walk lane's low end to the deck surface (no clip-through).
                EnsureBridgeDeckCollider();

                // ROOT-CAUSE FIX (data-proven RUNTIME_SEAM_NAV_FAIL / PathPartial, 2026-06-23):
                // (a) the weld tongue was too SHORT *and* mis-centred. The old math centred the deck
                //     at the gate↔threshold MIDPOINT and added the overlap to the *total length*, so
                //     only overlap/2 (=3m of the recipe's 6m) actually reached NORTH past the gate into
                //     the courtyard nav-floor — the other 3m hung south past the threshold (wasted).
                //     After agent-radius erosion + the fine 0.18 voxel + minRegionArea pruning, a 3m
                //     tongue between two separately-voxelised planes failed to FUSE -> PathPartial.
                //     Fix: floor the overlap to a SUBSTANTIAL minimum and extend the deck so the WHOLE
                //     overlap reaches north of the gate (deep into CourtyardFloor_Nav / GateExit_South_Nav,
                //     which run y=0 from ±65m / 8m inside the wall) — none wasted south of the threshold.
                // (b) Y must match the courtyard nav surface (all CastleHubBuilder nav planes are y=0).
                //     We SAMPLE the existing courtyard navmesh just inside the gate and snap the deck to
                //     that exact Y so a vertical lip can't read as a gap; fall back to _gatePos.y (0).
                float recipeOverlap = _recipe.approachOverlap > 0.01f ? _recipe.approachOverlap : 6f;
                // Deep overlap so the deck DEEPLY OVERLAPS the existing castle navmesh — the agent paths
                // on the UNION of the overlapping surfaces, so with the Children-only bake (see
                // RebakeSourceSurface) this overlap is what connects the deck to the editor-baked courtyard.
                // 18m drives the deck ~18m past the gate, well inside CourtyardFloor_Nav (±65m) and the
                // 8m-inside GateExit_South_Nav strip — far past any erosion/voxel pinch.
                float overlap   = Mathf.Max(recipeOverlap, 18f);

                // Deck Y = the actual courtyard nav surface height (snap to it), else _gatePos.y. Deeper
                // probe per the external review: sample 10m inside the gate, lifted +2m, radius 15 — lands
                // squarely on the courtyard navmesh body (not the gate-edge fringe) so the deck Y matches.
                float deckY = _gatePos.y;
                var probe = ToWorld(new Vector3(_gatePos.x, _gatePos.y + 2f, _gatePos.z + 10f));   // deep inside the courtyard (WORLD, this side)
                if (NavMesh.SamplePosition(probe, out NavMeshHit pHit, 15f, NavMesh.AllAreas))
                    deckY = pHit.position.y;

                // The deck spans from the threshold (south end) NORTH past the gate by the full overlap,
                // so the courtyard-side end sits at gate.z + overlap (north = +Z here; gate.z=-40.6).
                // WO-593 raise + F8 2026-07-02 flag_14 ("walking in the air off the bridge"): the deck
                // used to be ONE FLAT plane at courtyard height (y=liftY) all the way to the threshold —
                // past the plinth edge (r=44) that is a walkable catwalk hovering liftY above the outer
                // ground, floating OVER the descending bridge/ramps. Split it:
                //   FLAT TONGUE  courtyard(+overlap) → the plinth edge, at courtyard nav height;
                //   SLOPED RUN   plinth edge (y=deckY) → threshold (y = MEASURED surface there: the
                //                bridge/ramp deck when present — now collider-real — else flush terrain),
                // so the walkable lane FOLLOWS the visible descent instead of flying beside it.
                const float PlinthEdgeR = 44f;   // = CastleHubBuilder.PlinthHalf (editor const, mirrored — same
                                                 // convention as CastleMoatBuilder.RampInnerRadius)
                float courtyardEndZ = _gatePos.z + overlap;        // north end, INSIDE the courtyard
                float plinthEdgeZ   = -PlinthEdgeR;                // south-frame plinth edge
                bool  sloped        = _thresholdZ < plinthEdgeZ - 0.5f && _gatePos.y > 0.5f;

                float flatEndZ = sloped ? plinthEdgeZ : _thresholdZ;
                float centreZ  = (courtyardEndZ + flatEndZ) * 0.5f;
                float lenZ     = Mathf.Abs(courtyardEndZ - flatEndZ);
                // Unity plane = 10m/unit: scale.z = len/10, scale.x = width/10. The plane is ROTATED by
                // _yawRot so its long (Z) axis points along this gate's true outward axis (yaw=0 => identity).
                deck = CreateInvisibleWalkableFloor(transform, "RuntimeSeam_Deck_Nav",
                    ToWorld(new Vector3(_gatePos.x, deckY, centreZ)),
                    new Vector3(width / 10f, 1f, lenZ / 10f),
                    _yawRot);

                if (sloped)
                {
                    // Low-end Y = the real surface at the threshold XZ: raycast (ignoring triggers) hits
                    // the stone bridge / ramp deck (both carry colliders now) or the terrain; fallback =
                    // the ExteriorTerrainBuilder flush level 0 (CastleDepressionDepth=0f within ±62).
                    Vector3 probeW = ToWorld(new Vector3(_gatePos.x, 0f, _thresholdZ)) + Vector3.up * 25f;
                    float lowY = 0f; bool measured = false;
                    if (Physics.Raycast(probeW, Vector3.down, out RaycastHit lh, 60f, ~0, QueryTriggerInteraction.Ignore))
                    { lowY = lh.point.y; measured = true; }

                    // ── ROOT-1 fix (WO-607, owner 2026-07-04): South-only RUNTIME_SEAM_NAV_FAIL ──
                    // §12 CAPTURED DATA (break-log 2026-07-04): South threshold probe measured low-end y=5.84 —
                    // ABOVE the plinth edge deckY≈3.08, i.e. an ASCENDING ramp, physically impossible for a
                    // descending seam; W/N/E measured 3.00 and welded (RUNTIME_SEAM_NAV_OK). Cause: on the
                    // owner-tuned South side the bridge is "extended over the lip to the ground" (owner note
                    // 07-04), so the all-layer downward raycast hits the RAISED bridge/lip, not the courtyard
                    // floor. The weld TARGET is the source navmesh, so when the probe lands above the deck we
                    // sample the real navmesh Y at the threshold and weld to THAT (what W/N/E effectively do).
                    // Only the broken case (probe > deck) changes → W/N/E keep their raw measure; the lip, the
                    // owner pose, and flag_14 (still a contained descending lane) are untouched. Instrumented so
                    // the fleet oracle PROVES the flip to RUNTIME_SEAM_NAV_OK[South] — not shipped on faith.
                    string probeHit = measured ? (lh.collider != null ? lh.collider.name : "<null-col>") : "<no-hit>";
                    int   probeLayer = (measured && lh.collider != null) ? lh.collider.gameObject.layer : -1;
                    float navThrY = float.NaN;
                    bool  haveNav = NavMesh.SamplePosition(ToWorld(new Vector3(_gatePos.x, deckY, _thresholdZ)),
                                                           out NavMeshHit thrNav, 8f, NavMesh.AllAreas);
                    if (haveNav) navThrY = thrNav.position.y;
                    bool badHit = measured && lowY > deckY + 0.25f;   // probe above the deck = ascending ramp = bad hit
                    if (badHit)
                    {
                        // Prefer the REAL navmesh Y at the threshold (what W/N/E weld to); if the source
                        // navmesh doesn't reach the threshold XZ, fall back to the deck Y so the ramp is at
                        // worst flat — never ascending. Either way the low-end can no longer float above the deck.
                        lowY = (haveNav && !float.IsNaN(navThrY)) ? navThrY : deckY;
                    }
                    string navStr  = haveNav ? navThrY.ToString("F2") : "NONE";
                    string outcome = badHit
                        ? " raw probe ABOVE deck (hit raised bridge/lip) -> welded to " + (haveNav ? "navmesh" : "deck") + " Y instead"
                        : " (probe kept)";
                    FlowTrace.Step("RuntimeSeam",
                        $"deck[{SideName(_facingYaw)}] THRESHOLD-PROBE: hit '{probeHit}' (layer {probeLayer}) rawY={lh.point.y:F2} " +
                        $"deckY(plinth)={deckY:F2} navmesh@threshold={navStr} -> lowY={lowY:F2}{outcome}.");
                    _thresholdY = lowY;

                    // Contain the lane to the VISIBLE crossing: on the south side clamp the sloped width
                    // to the measured stone-bridge deck (inside its parapets) so the hero cannot nav-walk
                    // off the bridge sides (flag_14). Other sides keep the recipe width (their ramps are
                    // 9m flaring 15.75m — wider than the 7m lane).
                    float slopeWidth = width;
                    var bridgeGo = GameObject.Find("RuntimeSeam_Bridge_South");
                    if (bridgeGo != null && Mathf.Abs(Mathf.DeltaAngle(_facingYaw, 0f)) < 1f)
                    {
                        Bounds bb = default; bool have = false;
                        foreach (var br in bridgeGo.GetComponentsInChildren<Renderer>(true))
                        { if (br == null) continue; if (!have) { bb = br.bounds; have = true; } else bb.Encapsulate(br.bounds); }
                        if (have) slopeWidth = Mathf.Min(width, Mathf.Max(3f, bb.size.x - 1.2f));
                    }

                    float run   = Mathf.Abs(plinthEdgeZ - _thresholdZ);
                    float drop  = deckY - lowY;
                    float slen  = Mathf.Sqrt(run * run + drop * drop);
                    float pitch = -Mathf.Atan2(drop, run) * Mathf.Rad2Deg;   // local +Z (courtyard end) tilts UP — ramp convention
                    Vector3 slopeCentre = new Vector3(_gatePos.x, (deckY + lowY) * 0.5f, (plinthEdgeZ + _thresholdZ) * 0.5f);
                    CreateInvisibleWalkableFloor(transform, "RuntimeSeam_Deck_Nav_Slope",
                        ToWorld(slopeCentre),
                        new Vector3(slopeWidth / 10f, 1f, slen / 10f),
                        _yawRot * Quaternion.AngleAxis(pitch, Vector3.right));
                    FlowTrace.Step("RuntimeSeam",
                        $"deck[{SideName(_facingYaw)}] SLOPE: plinth edge z={plinthEdgeZ:F1} y={deckY:F2} -> threshold z={_thresholdZ:F1} y={lowY:F2} " +
                        $"(low end {(measured ? "MEASURED by raycast" : "fallback flush 0")}, pitch {pitch:F1}deg, width {slopeWidth:F1}m" +
                        $"{(slopeWidth < width - 0.01f ? " clamped to the stone-bridge deck" : "")}) — walk lane follows the descent, no mid-air catwalk (flag_14).");
                }

                FlowTrace.Step("RuntimeSeam",
                    $"deck[{SideName(_facingYaw)}]: centreSouth=({_gatePos.x:F2},{deckY:F2},{centreZ:F1})->world{ToWorld(new Vector3(_gatePos.x, deckY, centreZ))} len≈{lenZ:F1}m width={width}m " +
                    $"(flat tongue {courtyardEndZ:F1}→{flatEndZ:F1}{(sloped ? " + sloped run to threshold " + _thresholdZ.ToString("F1") : "")}, +{overlap:F0}m FULL deck/courtyard overlap north of gate {_gatePos.z:F1}, deckY snapped to courtyard navmesh — overlap is the connect with the Children-only bake).");
            });
            if (deck == null)
                FlowTrace.Fail("RuntimeSeam", "approach deck FAILED to build — the crossing cannot weld; hero will hit the navmesh edge.");
            return deck;
        }

        // --------------------------------------------------------------------
        //  WO-435 — make the VISIBLE bridge deck physically SOLID (hero clips through/under
        //  the bridge near "Enter Elarion"). RCA: the walkable descent's low-end Y is fixed by
        //  a downward raycast (BuildApproachDeck sloped block, QueryTriggerInteraction.Ignore).
        //  If the visible deck (RuntimeSeam_Bridge_South when present, else the scene prefab
        //  Drawbridge_Approach) carries NO physics collider on its top face, that raycast MISSES
        //  the deck and lands on the terrain UNDER it → the nav slope sits at terrain Y while the
        //  deck floats above (flag_14 "walking in the air off the bridge") / the hero rides below
        //  the deck (clip-under). A dedicated deck-top BoxCollider makes the deck solid REGARDLESS
        //  of the navmesh bake, so the raycast + physics both read the true deck surface.
        //  §12 INSTRUMENT-FIRST: logs collider presence on both candidate bridges + the NavMesh Y
        //  directly above the deck centre so a headless run splits collider-missing (add box, done)
        //  from navmesh-Y-wrong (bake stale → REBAKE, orchestrator-run). Idempotent + Guard-wrapped.
        // --------------------------------------------------------------------
        private const string BridgeDeckColliderName = "RuntimeSeam_BridgeDeck_Collider";

        private void EnsureBridgeDeckCollider()
        {
            Guard.Try("RuntimeSeam", "ensure bridge deck collider (WO-435)", () =>
            {
                var seamBridge = GameObject.Find("RuntimeSeam_Bridge_South");
                var drawbridge = GameObject.Find("Drawbridge_Approach");
                var bridge = seamBridge != null ? seamBridge : drawbridge;

                // Deck centre = the AABB of the visible deck's renderers (world), else the
                // south-frame threshold XZ at the current deck Y (pre-slope estimate).
                Vector3 deckCentre = ToWorld(new Vector3(_gatePos.x, _thresholdY, _thresholdZ));
                Bounds deckBounds = default; bool haveBounds = false;
                if (bridge != null)
                {
                    foreach (var br in bridge.GetComponentsInChildren<Renderer>(true))
                    {
                        if (br == null) continue;
                        if (!haveBounds) { deckBounds = br.bounds; haveBounds = true; }
                        else deckBounds.Encapsulate(br.bounds);
                    }
                    if (haveBounds) deckCentre = deckBounds.center;
                }

                // §12 diagnostics: collider presence on each candidate + the NavMesh Y directly
                // above the deck centre (the editor bake at this point — RebakeSourceSurface runs
                // later — so navY << deckTopY here proves a STALE bake that a rebake would fix).
                bool drawHasCol = drawbridge != null && drawbridge.GetComponentInChildren<Collider>(true) != null;
                bool seamHasCol = seamBridge != null && seamBridge.GetComponentInChildren<Collider>(true) != null;
                bool navHit = NavMesh.SamplePosition(deckCentre + Vector3.up * 3f, out NavMeshHit nh, 5f, NavMesh.AllAreas);
                float deckTopY = haveBounds ? deckBounds.max.y : deckCentre.y;
                FlowTrace.Step("RuntimeSeam",
                    $"BRIDGE_DECK[{SideName(_facingYaw)}]: Drawbridge_Approach={(drawbridge == null ? "ABSENT" : (drawHasCol ? "HAS-collider" : "NO-collider"))}, " +
                    $"RuntimeSeam_Bridge_South={(seamBridge == null ? "ABSENT" : (seamHasCol ? "HAS-collider" : "NO-collider"))}; " +
                    $"deckCentre={deckCentre} deckTopY={deckTopY:F2}; navSample(+3m up) onMesh={navHit} navY={(navHit ? nh.position.y.ToString("F2") : "n/a")} " +
                    "(navY far BELOW deckTopY => STALE bake, REBAKE recommended; collider handled below).");

                if (bridge == null)
                {
                    FlowTrace.Warn("RuntimeSeam",
                        "BRIDGE_DECK: neither RuntimeSeam_Bridge_South nor Drawbridge_Approach found — cannot add a deck collider (no clip-through fix applied).");
                    return;
                }
                if (!haveBounds)
                {
                    FlowTrace.Warn("RuntimeSeam",
                        $"BRIDGE_DECK: '{bridge.name}' has no renderers to size a deck collider — skipping (cannot size safely).");
                    return;
                }

                // Idempotent: our deck-top collider lives on a dedicated child parented to the seam
                // host (origin, identity scale — no parent-scale distortion). The host subtree is
                // destroyed + rebuilt on every seam build, so this never accumulates; still check
                // first per the spec so a re-entry within one build can't double-add.
                if (transform.Find(BridgeDeckColliderName) != null)
                {
                    FlowTrace.Step("RuntimeSeam",
                        $"BRIDGE_DECK: '{BridgeDeckColliderName}' already present — solid deck already ensured (idempotent no-op).");
                    return;
                }

                var colGo = new GameObject(BridgeDeckColliderName);
                colGo.transform.SetParent(transform, true);              // host @ origin, scale 1
                colGo.transform.position = new Vector3(deckBounds.center.x, deckBounds.max.y, deckBounds.center.z);
                colGo.transform.rotation = Quaternion.identity;          // AABB-aligned slab over the deck top
                var box = colGo.AddComponent<BoxCollider>();
                const float thickness = 0.5f;                            // thin slab flush with the deck TOP
                box.size = new Vector3(Mathf.Max(0.5f, deckBounds.size.x), thickness, Mathf.Max(0.5f, deckBounds.size.z));
                box.center = new Vector3(0f, -thickness * 0.5f, 0f);     // top face sits at deckBounds.max.y

                FlowTrace.Step("RuntimeSeam",
                    $"BRIDGE_DECK: added '{BridgeDeckColliderName}' BoxCollider over '{bridge.name}' size={box.size} topY={deckBounds.max.y:F2} " +
                    "— deck is now physically SOLID so the slope raycast pins the walk lane to the deck surface (hero can't clip through/under, WO-435).");
            });
        }

        // Lift of CastleHubBuilder.CreateInvisibleFloor + AddWalkableNavMeshModifier, runtime form:
        // a Plane (MeshCollider), renderer disabled, NavMeshModifier overrideArea=Walkable so the gate
        // arch can't carve it. Runtime can reference Unity.AI.Navigation directly (asmdef ref).
        private static GameObject CreateInvisibleWalkableFloor(Transform parent, string name,
            Vector3 worldPos, Vector3 localScale, Quaternion worldRot)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); // MeshFilter + MeshRenderer + MeshCollider
            plane.name = name;
            plane.transform.SetParent(parent, false);
            plane.transform.position = worldPos;
            plane.transform.rotation = worldRot;   // orient long axis along this gate's outward axis (yaw=0 => identity, unchanged)
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
                // FORGIVING/FINDABLE FIX (data-proven, owner felt-test 2026-06-23):
                // [Flow:RuntimeSeam] showed `closestEver=61.1m radius=40m out` — the hero only
                // reached the prompt when walking DEAD-SOUTH to the exact threshold spot
                // (z≈-62.6), because the OLD trigger sat at the FAR threshold end of the deck and
                // its sphere (effective 40m) barely scraped the deck's north end. Reseat the
                // trigger at the DECK CENTRE so its generous sphere BLANKETS the whole approach
                // lane — the "Travel to the Outer World" prompt now fires the moment the hero is
                // heading down the south approach, from anywhere on the deck, not just one spot.
                float overlap   = _recipe.approachOverlap > 0.01f ? Mathf.Max(_recipe.approachOverlap, 18f) : 18f;
                float deckNorthZ = _gatePos.z + overlap;     // north end, inside the courtyard
                float deckCentreZ = (deckNorthZ + _thresholdZ) * 0.5f;   // centre of the approach deck
                var go = new GameObject("RuntimeSeam_Trigger");
                go.transform.SetParent(transform, false);
                go.transform.position = ToWorld(new Vector3(_gatePos.x, _gatePos.y, deckCentreZ));   // WORLD, this side

                var trig = go.AddComponent<SceneTransitionTrigger>();
                trig.suppressPrompt  = true;   // passive walk-across seam: HeroLinkCrossing crosses; no "Travel to..." button (owner 2026-06-23).
                trig.targetSceneName = _recipe.to;
                trig.targetPosition  = ToWorld(_landing);   // WORLD landing for this side (yaw=0 => south value unchanged)
                trig.loadAdditive    = !string.Equals(_recipe.loadMode, "single", System.StringComparison.OrdinalIgnoreCase);
                // WO-497 seam slim-down: the 44m radius was a FINDABILITY band-aid for the (now-
                // suppressed) "Travel to..." tap-prompt. With suppressPrompt=true the actual crossing
                // is the ~2m HeroLinkCrossing warp; this trigger is only the passive warp BACKSTOP, so
                // the recipe radius is shrunk to ~8m (region-gates.json). The deck weld is INDEPENDENT
                // of this radius (RUNTIME_SEAM_NAV_OK path unaffected). SceneTransitionTrigger may still
                // floor the effective radius internally; that's fine for a suppressed-prompt backstop.
                trig.ProximityRadius = _recipe.triggerRadius > 0.01f ? _recipe.triggerRadius : 6f;
                FlowTrace.Step("RuntimeSeam",
                    $"trigger[{SideName(_facingYaw)}] seated @DECK-CENTRE world{ToWorld(new Vector3(_gatePos.x, _gatePos.y, deckCentreZ))} (southZ={deckCentreZ:F1}) -> '{_recipe.to}'@{ToWorld(_landing)} additive={trig.loadAdditive} r={trig.ProximityRadius} (EffRadius=Max(r,40m)) — sphere blankets the {overlap + (_gatePos.z - _thresholdZ):F0}m approach deck so the prompt is FORGIVING (fires on the whole approach, not just the exact spot).");
                // WO-530: behavior-neutral. Log the trigger's FINAL world position (AFTER parenting/
                // SetParent, which is what AutoPilot's SEAM-UNREACHABLE measures against) plus a navmesh
                // sample at both the trigger and the threshold, so a fleet run shows where the trigger
                // ACTUALLY lands at runtime and whether it sits on the baked mesh (vs the 7045m red herring).
                Vector3 thresholdWorld = ToWorld(new Vector3(_gatePos.x, _thresholdY, _thresholdZ));   // sloped-deck low end (WO-593)
                bool trigOnMesh = NavMesh.SamplePosition(go.transform.position, out NavMeshHit hTrig, 4f, NavMesh.AllAreas);
                bool thrOnMesh  = NavMesh.SamplePosition(thresholdWorld, out NavMeshHit hThr, 4f, NavMesh.AllAreas);
                FlowTrace.Step("RuntimeSeam",
                    $"trigger FINAL world pos {go.transform.position} (parent '{(transform != null ? transform.name : "<none>")}') " +
                    $"navSample(onMesh={trigOnMesh} hit @ {(trigOnMesh ? hTrig.position.ToString() : "n/a")}); " +
                    $"thresholdWorld {thresholdWorld} navSample(onMesh={thrOnMesh} hit @ {(thrOnMesh ? hThr.position.ToString() : "n/a")}).");
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
                // _thresholdY = the sloped-deck low end (WO-593 descent): the marker must sit ON the
                // walk surface or its small enterRadius can never be entered (flag_14 follow-through).
                entry.transform.position = ToWorld(new Vector3(_gatePos.x, _thresholdY, _thresholdZ));   // WORLD, this side
                var ec = entry.AddComponent<HeroLinkCrossing>();
                ec.crossingId = id;
                ec.bidirectional = true;

                var dest = new GameObject("RuntimeSeam_HeroLink_Dest");
                dest.transform.SetParent(transform, false);
                dest.transform.position = ToWorld(_landing);   // WORLD landing, this side
                var dc = dest.AddComponent<HeroLinkCrossing>();
                dc.crossingId = id;
                dc.bidirectional = true;
                // WO-602 (way back home): the OUTER end's default 2m enterRadius was an invisible
                // pin-point in an open field — the ONLY passive return path, and physically missable
                // by any approach more than 2m off the exact lane centre (the moat ramps FLARE the
                // landing to ~1.75x gate width, so most natural approaches missed it). Widen the
                // OUTER radius to cover the flared ramp-landing lane, derived from the recipe
                // approach width (half-deck + the component's stock 2m). The courtyard ENTRY keeps
                // its tight 2m radius so the exit fires exactly at the threshold, unchanged.
                float destWidth = _recipe.approachWidth > 0.01f ? _recipe.approachWidth : 7f;
                dc.enterRadius = Mathf.Max(dc.enterRadius, destWidth * 0.5f + 2f);

                FlowTrace.Step("RuntimeSeam",
                    $"HeroLinkCrossing pair '{id}' — entry@{entry.transform.position} (r={ec.enterRadius:F1}) " +
                    $"dest@{dest.transform.position} (r={dc.enterRadius:F1}, WO-602 widened outer return lane).");
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
                // FORGIVING LANE (2026-06-23): widen the gap between the funnel panels so a NORMAL
                // (not dead-centre) south approach is never pinched to the lane centre. Panel X-half
                // = 0.5, so seat the panel centre at half + 2.5 → inner carve face at half + 2.0, a
                // 2.0m clearance OUTSIDE each deck edge (was 0.5m). That widens the walkable gap by
                // ~3m total — the hero can drift off-centre on the approach and still reach the
                // trigger sphere. The carve still seals the SIDE gaps beyond the deck; it cannot
                // pinch the welded weld-tongue lane.
                float panelOffset = half + 2.5f;
                float panelZ = _gatePos.z;
                // Panels are placed in WORLD via ToWorld and ROTATED by _yawRot so their thin (X) sealing
                // axis flanks this gate's opening along its true tangential axis (yaw=0 => identity, unchanged).
                BuildPanel($"RuntimeSeam_Funnel_L", ToWorld(new Vector3(_gatePos.x - panelOffset, _gatePos.y + 1.5f, panelZ)), _yawRot);
                BuildPanel($"RuntimeSeam_Funnel_R", ToWorld(new Vector3(_gatePos.x + panelOffset, _gatePos.y + 1.5f, panelZ)), _yawRot);
                FlowTrace.Step("RuntimeSeam",
                    $"funnel panels at ±{panelOffset:F1}m of gate x={_gatePos.x:F2} (inner carve face ±{half + 2.0f:F1}, 2.0m clear of deck edge ±{half:F1}) — route through opening, lane WIDENED/unpinched (forgiving off-centre approach).");
            });
        }

        private void BuildPanel(string name, Vector3 worldPos, Quaternion worldRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;   // orient thin sealing axis along this gate's tangent (yaw=0 => identity)

            var box = go.AddComponent<BoxCollider>();
            // thin X, tall Y, modest Z (seals the side gap AT the gate line without carving deep along
            // the welded approach tongue — a 6m Z-depth straddled the gate into the courtyard weld zone).
            box.size = new Vector3(1f, 3f, 3f);

            var obs = go.AddComponent<NavMeshObstacle>();
            obs.shape   = NavMeshObstacleShape.Box;
            obs.size    = box.size;
            obs.carving = true;                                 // carve so navmesh routes through the opening
        }

        // --------------------------------------------------------------------
        //  PART 5b — VISIBLE gate BEACON (findability fix, 2026-06-23).
        //  The seam WORKED but the player couldn't SEE where to cross (owner: "walking
        //  directly south and only south works"). Add a cheap, prefab-free, code-built
        //  emissive beacon at the gate: a tall glowing pillar + a colored point light, in
        //  the same style as WardStone's runtime code-built glow (cube + Light + emissive).
        //  Skip-safe (Guard.Try → LogWarning, never a hard error): if anything fails to
        //  build, the crossing still works, the player just loses the visual cue.
        // --------------------------------------------------------------------
        private void BuildGateBeacon()
        {
            Guard.Try("RuntimeSeam", "build visible gate beacon", () =>
            {
                // Seat the beacon AT the gate opening (NOT on the deck lane — offset to the
                // gate centre so it marks the threshold without obstructing the walk).
                Vector3 beaconBase = ToWorld(new Vector3(_gatePos.x, _gatePos.y, _gatePos.z));   // WORLD gate, this side

                var root = new GameObject("RuntimeSeam_GateBeacon");
                root.transform.SetParent(transform, false);
                root.transform.position = beaconBase;   // pillar/light are Y-axis symmetric — no rotation needed

                // Warm portal-blue, like WardStone's lit ward-glow — reads as a "go here" cue.
                Color glowColor = new Color(0.40f, 0.75f, 1.00f);

                // 1) Emissive pillar BUILT BUT HIDDEN (owner 2026-06-30): the four "Beacon_Pillar"
                //    cubes render as white "beams" standing in the castle gate archways. The owner
                //    wants them not drawn. Per the hide-don't-destroy convention we KEEP the
                //    GameObject (so nothing referencing it by name breaks, and it's trivially
                //    reversible) and just DISABLE its renderer — no white beam, no behavior change.
                //    The pillar is render-only (no collider, no NavMeshObstacle) and is built at
                //    runtime, so it has no role in the baked navmesh / seam crossing either way.
                //    Re-enable the renderer to restore the visual findability cue.
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Beacon_Pillar";
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.localPosition = new Vector3(0f, 3f, 0f);   // rise from the ground
                pillar.transform.localScale    = new Vector3(0.5f, 6f, 0.5f);
                var col = pillar.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);                       // never obstruct the lane
                var r = pillar.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    // Give the hidden pillar a VALID URP material before disabling. A CreatePrimitive
                    // cube keeps Unity's built-in default material = InternalErrorShader (magenta) under
                    // URP, so MagentaGuard's scene sweep flags the (hidden) pillar as a stray magenta
                    // cube and error-spams the break-log every load (owner F8 2026-06-30, dev console).
                    // A valid URP/Lit mat means it's never "magenta"; the renderer is then disabled so
                    // it still never draws — hidden AND silent.
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null) r.sharedMaterial = new Material(sh) { name = "BeaconPillar_Hidden" };
                    r.enabled = false;                                      // HIDE not destroy (owner 2026-06-30)
                }

                // 2) A colored point light at the gate so the area is lit + the eye is drawn to it.
                var lightGo = new GameObject("Beacon_Light");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 4f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type      = LightType.Point;
                light.color     = glowColor;
                light.intensity = 4f;
                light.range     = 18f;
                light.shadows   = LightShadows.None;                         // cheap

                FlowTrace.Step("RuntimeSeam",
                    $"gate BEACON built @{beaconBase} (emissive pillar + point light, no collider) — player can SEE the south crossing now (findability fix).");
            });
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
                // Collect ONLY the CHILDREN of the seam host (the clean deck + funnel/trigger/link
                // primitives) — NOT the whole scene. This eliminates the ~240 "does not allow read
                // access" skips the polyperfect castle render meshes caused (external review fix, matches
                // the proven ArenaNavMeshBaker which also bakes Children-only). The deck connects to the
                // EXISTING editor-baked courtyard navmesh by DEEPLY OVERLAPPING it (18m tongue + Y-snap,
                // see BuildApproachDeck) — the agent paths on the union of the overlapping walkable surfaces.
                var surf = gameObject.GetComponent<NavMeshSurface>();
                if (surf == null) surf = gameObject.AddComponent<NavMeshSurface>();
                surf.collectObjects = CollectObjects.Children;

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
                // CastleHubBuilder WO-449) — belt-and-braces with Children-only: even among the host's
                // children nothing on Structure is voxelised, so the deck lane stays clear.
                int structureLayer = LayerMask.NameToLayer("Structure");
                surf.layerMask = structureLayer >= 0 ? ~(1 << structureLayer) : ~0;

                // Voxel/region tuning copied from ArenaNavMeshBaker (the proven runtime bake): a
                // finer voxel for a tight local weld + prune stray islands smaller than MinRegionArea.
                surf.overrideVoxelSize = true;
                surf.voxelSize         = 0.18f;   // ArenaNavMeshBaker.VoxelSize
                surf.minRegionArea     = 0.5f;    // ArenaNavMeshBaker.MinRegionArea

                surf.BuildNavMesh();
                FlowTrace.Step("RuntimeSeam",
                    $"runtime NavMeshSurface.BuildNavMesh() — Children + PhysicsColliders only, layerMask excludes 'Structure'({structureLayer}) " +
                    "so the ~240 non-readable polyperfect render-mesh skips are gone; deck connects to the editor-baked courtyard navmesh by deep 18m overlap + Y-snap (no read-access skips).");
            });
        }

        // Build-time reachability oracle (tight tolerance so a snap onto a stacked far-scene
        // navmesh can't false-green — the 2026-06-19 lesson). Emit RUNTIME_SEAM_NAV_OK/_FAIL.
        private void AssertApproachWelded()
        {
            Guard.Try("RuntimeSeam", "assert deck welded to source navmesh", () =>
            {
                // Source courtyard reference point (origin = castle centre) → the threshold trigger.
                // Both rotated to WORLD for this side so the per-gate reachability check samples the
                // real navmesh (yaw=0 => south values unchanged).
                var courtyard = ToWorld(new Vector3(0f, _gatePos.y, _gatePos.z + 4f));     // just inside the gate
                var threshold = ToWorld(new Vector3(_gatePos.x, _thresholdY, _thresholdZ));   // sloped-deck low end (WO-593)

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
                        $"RUNTIME_SEAM_NAV_OK [{SideName(_facingYaw)} facingYaw={_facingYaw:F0}] — PATH-COMPLETE courtyard→threshold world{threshold}; deck welds to source navmesh, hero walks to the trigger.");
                else
                    FlowTrace.Fail("RuntimeSeam",
                        $"RUNTIME_SEAM_NAV_FAIL [{SideName(_facingYaw)} facingYaw={_facingYaw:F0}] — courtyard→threshold path is {path.status}; deck did NOT weld (gap between gate and deck). " +
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
                hostGo.transform.rotation = Quaternion.identity;   // keep link space == WORLD so the rotated endpoints below are used verbatim (Unity auto-orients the band perpendicular to the segment)

                var link = hostGo.AddComponent<NavMeshLink>();
                if (link == null) throw new System.Exception("AddComponent<NavMeshLink> returned null.");
                // Start on the source deck just past the threshold; end at the OuterWorld landing — both
                // rotated to WORLD for this side (yaw=0 => south values unchanged).
                link.startPoint    = ToWorld(new Vector3(_gatePos.x, _thresholdY, _thresholdZ));   // sloped-deck low end (WO-593)
                link.endPoint      = ToWorld(_landing);
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
        //  PART 6 — OUTER RETURN ENTRANCE (WO-602 "way back home").
        //  ROOT CAUSE (owner felt-test 2026-07-03 + SceneTransitionTrigger.cs's own
        //  ledger "The crossing is ONE-WAY: there is no OuterWorld->castle return seam
        //  wired"): the ONLY return path was the HeroLinkCrossing dest marker — an
        //  INVISIBLE 2m pin-point on open ground with no prompt (the castle-side
        //  trigger runs suppressPrompt=true and targets OuterWorld anyway) and no
        //  visual (Beacon_Pillar is built hidden, owner 2026-06-30). Fix = a SYMMETRIC
        //  outer-face crossing per gate:
        //    (a) a SceneTransitionTrigger AT the outer landing (real outer ground,
        //        generous radius) with the standard visible "Enter Elarion" prompt
        //        (suppressPrompt=false on the OUTER trigger ONLY — the retired
        //        interior-seam prompt stays suppressed) warping to the courtyard-side
        //        landing at y=liftY, NavMesh-snapped;
        //    (b) a subtle outside-facing affordance: two short glowing gate-posts
        //        flanking the landing + a point light (NOT the hidden archway pillar —
        //        the owner's complaint was white beams IN the archways; these are low,
        //        colored, and outside at the ramp mouth);
        //    (c) the widened passive HeroLinkCrossing dest radius (BuildHeroCrossingPair)
        //        remains the walk-in path; this trigger is the visible, tappable read.
        //  Built beside BuildAiLink (OuterWorld additive) so the outer ground exists
        //  for the Y-snap. All positions derived from recipe/landing geometry — no
        //  magic constants.
        // --------------------------------------------------------------------
        private void BuildReturnEntrance(float width)
        {
            if (_returnBuilt) return;
            bool ok = Guard.Try("RuntimeSeam", "build OUTER return entrance (WO-602)", () =>
            {
                string side = SideName(_facingYaw);

                // OUTER anchor = the SAME landing the exit warps the hero to (WorldGeometry-or-
                // fallback, rotated for this side). Snap Y to the real outer ground: OuterWorld is
                // additive-loaded when this runs, so a downward raycast finds the terrain surface.
                Vector3 outerWorld = ToWorld(_landing);
                if (Physics.Raycast(outerWorld + Vector3.up * 25f, Vector3.down, out RaycastHit ground, 60f, ~0, QueryTriggerInteraction.Ignore))
                    outerWorld.y = ground.point.y;

                // COURTYARD return landing: just inside the gate on the raised courtyard. _gatePos.y
                // already carries castle.liftY (WO-593 lift, applied in Start), and we snap to the
                // baked castle navmesh so the warp target derives from the mesh, not a constant.
                // Inset = the deck overlap tongue depth's gate-side third (~6m past the arch) so the
                // hero lands ON the welded courtyard surface, clear of the arch/funnel panels.
                float inset = (_recipe.thresholdBackFromGate > 0.01f ? _recipe.thresholdBackFromGate : 22f) * 0.27f;
                Vector3 courtyardWorld = ToWorld(new Vector3(_gatePos.x, _gatePos.y, _gatePos.z + inset));
                if (NavMesh.SamplePosition(courtyardWorld, out NavMeshHit cyHit, 4f, NavMesh.AllAreas))
                    courtyardWorld = cyHit.position;

                // (a) The visible, tappable return trigger — the standard shared prompt path.
                var go = new GameObject("RuntimeSeam_ReturnTrigger_" + side);
                go.transform.SetParent(transform, false);
                go.transform.position = outerWorld;

                var trig = go.AddComponent<SceneTransitionTrigger>();
                trig.targetSceneName = _recipe.from;      // back INTO the hub (already additive → reposition-only warp)
                trig.targetPosition  = courtyardWorld;    // y=liftY courtyard landing, navmesh-snapped
                trig.loadAdditive    = true;              // hub never unloaded; Cross() repositions only
                trig.suppressPrompt  = false;             // OUTER face ONLY — interior seam prompt stays retired
                trig.promptOverride  = "Enter Elarion";   // walk-up entry → honors the authored radius (no 40m floor)
                // Generous, geometry-derived: covers the moat ramp's flared landing (~1.75x gate width).
                trig.ProximityRadius = Mathf.Max(_recipe.triggerRadius > 0.01f ? _recipe.triggerRadius : 6f, width * 1.5f);

                FlowTrace.Step("RuntimeSeam",
                    $"OUTER trigger {side} armed at {outerWorld} r={trig.ProximityRadius:F1} -> warps hero to courtyard {courtyardWorld} " +
                    $"('Enter Elarion' prompt, target '{_recipe.from}'; return crossing now SYMMETRIC — WO-602). " +
                    "A fire logs as [Flow:Seam] Cross 'RuntimeSeam_ReturnTrigger_" + side + "'.");

                // (b) Subtle outside-facing affordance: two short glowing posts flanking the
                //     landing lane + a point light. Portal-blue = the established "go here" cue.
                Color glow = new Color(0.40f, 0.75f, 1.00f);
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material postMat = null;
                if (sh != null)
                {
                    postMat = new Material(sh) { name = "ReturnPost_Glow", color = glow };
                    postMat.EnableKeyword("_EMISSION");
                    postMat.SetColor("_EmissionColor", glow * 2f);
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    // Flank the lane in the SOUTH frame (lateral = X), rotated to this side; snap
                    // each post to its own ground so a sloped landing can't float/bury one.
                    Vector3 postWorld = ToWorld(new Vector3(_landing.x + s * (width * 0.5f + 0.6f), _landing.y, _landing.z));
                    if (Physics.Raycast(postWorld + Vector3.up * 25f, Vector3.down, out RaycastHit pg, 60f, ~0, QueryTriggerInteraction.Ignore))
                        postWorld.y = pg.point.y;

                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "ReturnPost_" + side + (s < 0 ? "_L" : "_R");
                    post.transform.SetParent(go.transform, true);
                    post.transform.position = postWorld + Vector3.up * 0.9f;   // half of the 1.8m height
                    post.transform.rotation = _yawRot;
                    post.transform.localScale = new Vector3(0.35f, 1.8f, 0.35f);
                    var pc = post.GetComponent<Collider>();
                    if (pc != null) Object.Destroy(pc);   // never block the lane
                    var pr = post.GetComponent<MeshRenderer>();
                    if (pr != null && postMat != null) pr.sharedMaterial = postMat;   // valid URP mat — never MagentaGuard bait
                    else if (pr != null) pr.enabled = false;                          // no valid shader → hide rather than magenta
                }
                var lightGo = new GameObject("ReturnLight_" + side);
                lightGo.transform.SetParent(go.transform, true);
                lightGo.transform.position = outerWorld + Vector3.up * 3f;
                var light = lightGo.AddComponent<Light>();
                light.type      = LightType.Point;
                light.color     = glow;
                light.intensity = 3f;
                light.range     = 14f;
                light.shadows   = LightShadows.None;   // cheap

                _returnBuilt = true;
                FlowTrace.Step("RuntimeSeam",
                    $"return AFFORDANCE {side}: 2 glow posts flanking the landing (±{width * 0.5f + 0.6f:F1}m) + point light @ {outerWorld} — " +
                    "the outside face now READS as the way home (owner canon: no invisible triggers).");
            });
            if (!ok)
                FlowTrace.Fail("RuntimeSeam",
                    $"OUTER return entrance FAILED to build [{SideName(_facingYaw)}] — the way back home is still the invisible 2m pin-point (WO-602 regression).");
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
            public float  facingYaw;            // Y-rotation that maps the proven SOUTH build onto this gate's side: S=0 W=90 N=180 E=270 (default 0 = south)
        }

        [System.Serializable] private class SouthPiece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [System.Serializable] private class SouthRecipe { public SouthPiece[] pieces; public float[] parentPos; public float[] parentRot; }
    }
}
