// =============================================================================
// AutoPilotProbes — DEV-ONLY passive assertion probes for the AutoPilot bot.
// -----------------------------------------------------------------------------
// These probes ride ALONGSIDE the AutoPilotDriver's scripted phases and watch
// for UX / structural defects the phase machine does NOT catch — bugs that only
// show up by *observing the world state over time* rather than by driving a
// specific seam. They report exclusively through FlowTrace.Fail/Warn (tag
// [Flow:AutoTest]) so every violation lands in break-log.jsonl via the always-on
// BreakCaptureHarness and surfaces as an AutoPilot ticket.
//
// FIVE PROBES:
//   1. UNEXPECTED-CROSS  — a raid-destination scene (Garrison*/Outpost*/Village2/
//                          Raid*) loaded while the bot is in normal town traversal
//                          (NOT an intentional raid/cross phase). Catches the
//                          requireConfirm=false proximity auto-cross.
//   2. COPLANAR-FLOOR    — two large opaque floor MeshRenderers overlap in XZ with
//                          centre-Y within 0.1m → a z-fight cause (works headless).
//   3. WALL-CLIP         — the hero is INSIDE a non-trigger collider whose name /
//                          parent name reads Wall/Palisade/Fortif/Rampart → the
//                          hero is standing in wall geometry (walk-through-walls).
//   4. DUAL-NAVMESH /    — >1 additively-loaded scene contributes a NavMesh over the
//      STRANDED /          same XZ region (overlap); a NavMeshLink whose endpoint is
//      NAVMESH-LINK        off-mesh or an overlapping additive seam with NO bridging
//                          link; AND the hero's NavMeshAgent making no path progress
//                          toward any objective for >~20s → a possible softlock.
//   5. SEAM-REACHABLE    — every SceneTransitionTrigger must sit ON the baked navmesh
//                          AND be reachable-on-foot from the hero to within its
//                          ProximityRadius. A floating / unreachable seam never fires —
//                          the player can't cross. Runtime generalisation of the
//                          editor-only CastleGateNavVerify (regression-guards the
//                          2026-06-19 castle→OuterWorld bridge fix). Cross-scene seams
//                          (across a warp boundary) are census-only, never failed.
//
// GATING: this component is spawned ONLY by AutoPilotDriver (an autopilot-only
// host). It also self-checks AutoPilotInstaller-style intent is irrelevant — the
// driver only exists on an autopilot run — but to be safe every probe is a no-op
// unless _armed is set by the driver. THROTTLING: each per-tick check runs off a
// local realtime timer (see _intervalSeconds fields), and the chatty per-frame
// reads use FlowTrace.Throttle keys, so nothing spams the log.
//
// RELEASE-SAFE: the whole file is #if DEVELOPMENT_BUILD || UNITY_EDITOR.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY passive assertion probes that ride alongside <see cref="AutoPilotDriver"/>.
    /// Spawned + armed by the driver (autopilot-only). Reports defects via
    /// <c>FlowTrace.Fail</c> ([Flow:AutoTest]) so they land in break-log.jsonl.
    /// Every per-tick check is throttled off a local realtime timer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoPilotProbes : MonoBehaviour
    {
        private const string Tag = "AutoTest";

        // ── arming (autopilot-gate) ──────────────────────────────────────────
        // The driver is the ONLY thing that spawns this component, and only on an
        // autopilot run. We still require an explicit Arm() so a stray AddComponent
        // (editor inspector, test) never silently starts asserting against a normal
        // play session.
        private bool _armed;

        // ── shared hero handle (resolved lazily, refreshed on scene change) ───
        private HeroLocomotion _hero;
        private NavMeshAgent _heroAgent;

        // ── per-check throttle timers (realtime seconds) ─────────────────────
        private const float WallClipInterval   = 0.25f;  // ~4/sec
        private const float CoplanarInterval   = 5f;     // scan settled floors every 5s
        private const float NavMeshInterval    = 5f;     // dual-navmesh overlap scan
        private const float NavLinkInterval    = 5f;     // navmesh-link connectivity scan
        private const float SeamReachInterval  = 5f;     // seam reachability scan
        private const float StrandedInterval   = 1f;     // poll path progress 1/sec
        private const float HeroRefreshInterval = 2f;    // re-resolve hero handle

        private float _nextWallClip;
        private float _nextCoplanar;
        private float _nextNavMesh;
        private float _nextNavLink;
        private float _nextSeamReach;
        private float _nextStranded;
        private float _nextHeroRefresh;

        // ── raid-destination name detection (UNEXPECTED-CROSS) ───────────────
        // A scene whose name marks it as a raid target. Town traversal should never
        // load one of these unless the bot is in an intentional raid/cross phase.
        private static readonly string[] RaidSceneTokens =
        {
            "Garrison", "Outpost", "Village2", "Raid",
        };

        // The driver sets this true ONLY while it is intentionally crossing (the
        // AttemptExitCastle phase, or a future raid phase). While false, any
        // raid-destination scene load is an unexpected auto-teleport.
        private bool _intentionalCrossPhase;

        // ── stranded tracking (DUAL-NAVMESH / STRANDED) ──────────────────────
        // We flag "no progress toward any objective" by watching the hero's path
        // status + remaining distance: if for >StrandedHoldSeconds the agent has a
        // partial/invalid path (or has not closed distance to its target), we treat
        // it as stranded. Reset whenever the hero makes progress or is idle-by-design.
        private const float StrandedHoldSeconds = 20f;
        private float _strandedSince = -1f;       // realtime when the no-progress window opened (-1 = not open)
        private float _lastRemainingDist = float.PositiveInfinity;
        private Vector3 _lastHeroPos = new Vector3(float.NaN, float.NaN, float.NaN);
        private bool _strandedReported;           // fire the Fail at most once per window

        /// <summary>
        /// Arm the probes. Called by <see cref="AutoPilotDriver"/> on an autopilot run.
        /// Until armed, every check is a no-op (so a stray AddComponent never asserts
        /// against a normal play session). Idempotent.
        /// </summary>
        public void Arm()
        {
            if (_armed) return;
            _armed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FlowTrace.Step(Tag, "AutoPilotProbes ARMED — UNEXPECTED-CROSS, COPLANAR-FLOOR, WALL-CLIP, DUAL-NAVMESH/STRANDED, NAVMESH-LINK, SEAM-REACHABLE active.");
        }

        /// <summary>
        /// The driver calls this to declare whether the bot is *intentionally* crossing
        /// into another scene right now (the AttemptExitCastle phase / a raid phase). While
        /// true, a raid-destination scene load is EXPECTED and the UNEXPECTED-CROSS probe
        /// stays silent. While false (normal town traversal), such a load is flagged.
        /// </summary>
        public void SetIntentionalCrossPhase(bool intentional)
        {
            _intentionalCrossPhase = intentional;
        }

        private void OnDestroy()
        {
            if (_armed) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // =====================================================================
        //  PROBE 1: UNEXPECTED-CROSS (event-driven, not throttled — one per load)
        // =====================================================================
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_armed) return;
            try
            {
                string name = scene.name ?? string.Empty;
                if (!IsRaidScene(name)) return;

                if (_intentionalCrossPhase)
                {
                    FlowTrace.Step(Tag, $"UNEXPECTED-CROSS: raid scene '{name}' loaded during an INTENTIONAL cross phase — expected, not flagged.");
                    return;
                }

                // A raid-destination scene came online while the bot was just walking
                // town. This is the requireConfirm=false proximity auto-cross defect.
                FlowTrace.Fail(Tag, $"unexpected auto-teleport into {name} while walking town (raid-destination scene loaded outside an intentional raid/cross phase).");
            }
            catch (Exception ex) { FlowTrace.Warn(Tag, "UNEXPECTED-CROSS check threw: " + ex.Message); }
        }

        private static bool IsRaidScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            foreach (var tok in RaidSceneTokens)
                if (sceneName.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        // =====================================================================
        //  Update — drives the throttled per-tick probes
        // =====================================================================
        private void Update()
        {
            if (!_armed) return;
            float now = Time.realtimeSinceStartup;

            if (now >= _nextHeroRefresh)
            {
                _nextHeroRefresh = now + HeroRefreshInterval;
                RefreshHero();
            }

            if (now >= _nextWallClip)
            {
                _nextWallClip = now + WallClipInterval;
                try { CheckWallClip(); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "WALL-CLIP check threw: " + ex.Message); }
            }

            if (now >= _nextCoplanar)
            {
                _nextCoplanar = now + CoplanarInterval;
                try { CheckCoplanarFloors(); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "COPLANAR-FLOOR check threw: " + ex.Message); }
            }

            if (now >= _nextNavMesh)
            {
                _nextNavMesh = now + NavMeshInterval;
                try { CheckDualNavMesh(); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "DUAL-NAVMESH check threw: " + ex.Message); }
            }

            if (now >= _nextNavLink)
            {
                _nextNavLink = now + NavLinkInterval;
                try { CheckNavMeshLinks(); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "NAVMESH-LINK check threw: " + ex.Message); }
            }

            if (now >= _nextSeamReach)
            {
                _nextSeamReach = now + SeamReachInterval;
                try { CheckSeamReachable(); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "SEAM-REACHABLE check threw: " + ex.Message); }
            }

            if (now >= _nextStranded)
            {
                _nextStranded = now + StrandedInterval;
                try { CheckStranded(now); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "STRANDED check threw: " + ex.Message); }
            }
        }

        private void RefreshHero()
        {
            if (_hero == null)
            {
                _hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
                _heroAgent = null;
            }
            if (_hero != null && _heroAgent == null)
                _heroAgent = _hero.GetComponent<NavMeshAgent>();
        }

        // =====================================================================
        //  PROBE 2: COPLANAR-FLOOR (z-fight cause, works headless)
        //  Scan large floor MeshRenderers (footprint > ~30m on X and Z) across ALL
        //  loaded scenes. If two OPAQUE floors overlap in XZ and their centre Y
        //  differ by < 0.1m, Fail. Throttled (CoplanarInterval) + a per-pair Once
        //  key so the same coplanar pair is reported a single time.
        // =====================================================================
        private const float FloorFootprintMin = 30f;  // X and Z bounds size to qualify as a "large floor"
        private const float CoplanarYEpsilon  = 0.1f;  // centre-Y delta below this = z-fight risk
        private readonly HashSet<string> _coplanarSeen = new HashSet<string>();

        private void CheckCoplanarFloors()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsSortMode.None);
            if (renderers == null || renderers.Length < 2) return;

            // Collect only the large, opaque, horizontal floor candidates.
            var floors = new List<MeshRenderer>(8);
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (b.size.x < FloorFootprintMin || b.size.z < FloorFootprintMin) continue;
                // A floor is flat: its Y extent is small relative to its footprint.
                if (b.size.y > 2f) continue;
                if (!IsOpaque(r)) continue;
                floors.Add(r);
            }

            for (int i = 0; i < floors.Count; i++)
            {
                for (int j = i + 1; j < floors.Count; j++)
                {
                    var a = floors[i];
                    var b = floors[j];
                    Bounds ba = a.bounds, bb = b.bounds;

                    // Overlap in XZ?
                    bool overlapX = Mathf.Abs(ba.center.x - bb.center.x) < (ba.extents.x + bb.extents.x);
                    bool overlapZ = Mathf.Abs(ba.center.z - bb.center.z) < (ba.extents.z + bb.extents.z);
                    if (!overlapX || !overlapZ) continue;

                    float dY = Mathf.Abs(ba.center.y - bb.center.y);
                    if (dY >= CoplanarYEpsilon) continue;

                    // Stable key so we report a given pair once (names + rounded Y).
                    string key = PairKey(a.name, b.name);
                    if (!_coplanarSeen.Add(key)) continue;

                    FlowTrace.Fail(Tag, $"potential z-fight: {a.name} Y={ba.center.y:0.000} vs {b.name} Y={bb.center.y:0.000} " +
                        $"(coplanar floors overlap in XZ, centre-Y delta {dY:0.000}m < {CoplanarYEpsilon}m).");
                }
            }
        }

        // Opaque = the renderer's material(s) render in the geometry queue (< Transparent=3000).
        // A floor in the transparent queue can't z-fight opaquely; skip it. Null-safe.
        private static bool IsOpaque(Renderer r)
        {
            try
            {
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) return true; // no material info — treat as opaque
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (m.renderQueue >= 3000) return false; // transparent
                }
                return true;
            }
            catch { return true; }
        }

        private static string PairKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        // =====================================================================
        //  PROBE 3: WALL-CLIP
        //  On a throttled tick (~4/sec), OverlapBox at the hero's capsule. If the
        //  hero is INSIDE a non-trigger collider whose name or parent name reads
        //  Wall/Palisade/Fortif/Rampart, Fail (the hero is in wall geometry).
        // =====================================================================
        private static readonly string[] WallTokens = { "Wall", "Palisade", "Fortif", "Rampart" };
        private static readonly Collider[] _overlapBuf = new Collider[16];

        private void CheckWallClip()
        {
            if (_hero == null) return;
            Vector3 pos = _hero.transform.position;
            // Approximate the hero capsule: ~0.4m radius, ~1.8m tall (matches the
            // HeroLocomotion NavMeshAgent dimensions). Centre the box at mid-height.
            Vector3 centre = pos + Vector3.up * 0.9f;
            Vector3 half = new Vector3(0.35f, 0.85f, 0.35f);

            int n = Physics.OverlapBoxNonAlloc(centre, half, _overlapBuf, Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _overlapBuf[i];
                if (col == null || col.isTrigger) continue;
                // Ignore the hero's own colliders.
                if (col.transform == _hero.transform || col.transform.IsChildOf(_hero.transform)) continue;

                if (!NameReadsWall(col)) continue;

                // Hard Fail (error-level → ticket). FlowTrace.Fail is not itself throttled,
                // so guard it with a local throttle so a sustained clip logs at most once
                // every 2s per collider instead of 4/sec. (FlowTrace prepends the [Flow:Tag].)
                if (ShouldLogWallClip(col.name))
                    FlowTrace.Fail(Tag, $"hero inside wall geometry: {col.name} (hero at {pos}, non-trigger collider).");
            }
        }

        // Local per-collider throttle for the WALL-CLIP Fail: FlowTrace.Fail (error level)
        // is NOT throttled by FlowTrace, so we gate it here to ~once / 2s per collider name
        // so a sustained clip doesn't flood the break-log with duplicate errors.
        private const float WallClipFailEvery = 2f;
        private readonly Dictionary<string, float> _wallClipNextAt = new Dictionary<string, float>();

        private bool ShouldLogWallClip(string name)
        {
            float now = Time.realtimeSinceStartup;
            if (_wallClipNextAt.TryGetValue(name, out float next) && now < next) return false;
            _wallClipNextAt[name] = now + WallClipFailEvery;
            return true;
        }

        // True when the collider's own name OR an ancestor name contains a wall token.
        private static bool NameReadsWall(Collider col)
        {
            Transform t = col.transform;
            // Walk up a few levels (collider may sit on a child of the named wall root).
            for (int depth = 0; depth < 4 && t != null; depth++)
            {
                string n = t.name;
                foreach (var tok in WallTokens)
                    if (n.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                t = t.parent;
            }
            return false;
        }

        // =====================================================================
        //  PROBE 4a: DUAL-NAVMESH
        //  Flag when >1 additively-loaded scene contributes a NavMesh over the same
        //  XZ region (overlap). We read the baked NavMesh triangulation once per
        //  scan and bucket its vertices by source scene via NavMesh.CalculateTriangulation
        //  areas; since the triangulation does not carry a scene id, we instead detect
        //  the structural smell: >1 loaded scene each owning a NavMeshSurface/-Data whose
        //  bounds overlap in XZ. Reported once per overlapping pair.
        // =====================================================================
        private readonly HashSet<string> _navOverlapSeen = new HashSet<string>();

        private void CheckDualNavMesh()
        {
            // Only meaningful when >1 scene is loaded additively.
            int loaded = SceneManager.sceneCount;
            if (loaded < 2) return;

            // Build per-scene XZ footprints of NavMesh coverage. We approximate a scene's
            // NavMesh footprint from the bounds of all NavMeshAgents/Obstacles + the global
            // triangulation clipped per scene is not available, so we use the cheap, robust
            // proxy: the global triangulation's overall bounds vs. how many scenes are loaded.
            // To stay correct + cheap, we instead detect overlap between scenes by their
            // root-object world bounds intersecting in XZ AND each scene actually contributing
            // navmesh (an agent or the global triangulation covering that region).
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0) return;

            // Per-scene XZ bounds from active root objects (cheap structural proxy).
            var sceneBounds = new List<KeyValuePair<string, Bounds>>(loaded);
            for (int s = 0; s < loaded; s++)
            {
                Scene sc = SceneManager.GetSceneAt(s);
                if (!sc.isLoaded) continue;
                Bounds? bb = SceneXZBounds(sc);
                if (bb.HasValue) sceneBounds.Add(new KeyValuePair<string, Bounds>(sc.name, bb.Value));
            }

            for (int i = 0; i < sceneBounds.Count; i++)
            {
                for (int j = i + 1; j < sceneBounds.Count; j++)
                {
                    Bounds a = sceneBounds[i].Value, b = sceneBounds[j].Value;
                    bool overlapX = Mathf.Abs(a.center.x - b.center.x) < (a.extents.x + b.extents.x);
                    bool overlapZ = Mathf.Abs(a.center.z - b.center.z) < (a.extents.z + b.extents.z);
                    if (!overlapX || !overlapZ) continue;

                    string key = PairKey(sceneBounds[i].Key, sceneBounds[j].Key);
                    if (!_navOverlapSeen.Add(key)) continue;

                    FlowTrace.Fail(Tag, $"DUAL-NAVMESH: additively-loaded scenes '{sceneBounds[i].Key}' and " +
                        $"'{sceneBounds[j].Key}' overlap in XZ while a baked NavMesh is present — two navmeshes over the same region (agent may path onto the wrong surface).");
                }
            }
        }

        // =====================================================================
        //  PROBE 4c: NAVMESH-LINK CONNECTIVITY
        //  The show-stopper class (WO-453): a seam between two additively-loaded
        //  scenes that OVERLAP in XZ and both carry navmesh, but has NO NavMeshLink
        //  bridging them -> the hero cannot WALK the seam; the crossing falls back to
        //  a warp (or strands). This stayed hidden for weeks because the builder's
        //  link placement only emitted a silent Debug.LogWarning on failure. Here it
        //  BUBBLES UP: every run enumerates the links, validates each endpoint sits on
        //  the baked navmesh, and Fails loud on (a) a dangling link (endpoint off-mesh)
        //  and (b) an overlapping additive seam with zero bridging links.
        // =====================================================================
        private readonly HashSet<string> _navLinkSeen = new HashSet<string>();
        private bool _navLinkCensusDone;

        private void CheckNavMeshLinks()
        {
            var links = UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshLink>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // One-shot census so a run's report names EXACTLY what links exist + where.
            if (!_navLinkCensusDone)
            {
                _navLinkCensusDone = true;
                FlowTrace.Step(Tag, $"NAVMESH-LINK census: {links.Length} active NavMeshLink(s) across {SceneManager.sceneCount} loaded scene(s).");
            }

            // (a) Per-link endpoint validity: both ends must sample onto the baked navmesh,
            //     else the link bridges nothing (a dangling/mis-placed seam).
            const float SampleTol = 2.0f;
            foreach (var link in links)
            {
                if (link == null || !link.isActiveAndEnabled) continue;
                Transform t = link.transform;
                Vector3 startW = t.TransformPoint(link.startPoint);
                Vector3 endW   = t.TransformPoint(link.endPoint);
                bool startOk = NavMesh.SamplePosition(startW, out _, SampleTol, NavMesh.AllAreas);
                bool endOk   = NavMesh.SamplePosition(endW,   out _, SampleTol, NavMesh.AllAreas);
                if (startOk && endOk) continue;

                string lk = $"danglinglink:{link.gameObject.scene.name}:{link.name}";
                if (!_navLinkSeen.Add(lk)) continue;
                FlowTrace.Fail(Tag,
                    $"NAVMESH-LINK DANGLING: '{link.name}' in '{link.gameObject.scene.name}' has an endpoint OFF the navmesh " +
                    $"(start on-mesh={startOk} @ {startW}, end on-mesh={endOk} @ {endW}) — the link bridges nothing; the hero can't cross here.");
            }

            // (b) Overlapping additive seam with NO bridging link = warp-only seam (the WO-453
            //     castle<->OuterWorld show-stopper). For each pair of loaded scenes whose XZ
            //     footprints overlap, require >=1 NavMeshLink whose endpoints straddle the pair.
            int loaded = SceneManager.sceneCount;
            if (loaded < 2) return;

            var sceneBounds = new List<KeyValuePair<string, Bounds>>(loaded);
            for (int s = 0; s < loaded; s++)
            {
                Scene sc = SceneManager.GetSceneAt(s);
                if (!sc.isLoaded) continue;
                Bounds? bb = SceneXZBounds(sc);
                if (bb.HasValue) sceneBounds.Add(new KeyValuePair<string, Bounds>(sc.name, bb.Value));
            }

            for (int i = 0; i < sceneBounds.Count; i++)
            {
                for (int j = i + 1; j < sceneBounds.Count; j++)
                {
                    Bounds a = sceneBounds[i].Value, b = sceneBounds[j].Value;
                    bool overlapX = Mathf.Abs(a.center.x - b.center.x) < (a.extents.x + b.extents.x);
                    bool overlapZ = Mathf.Abs(a.center.z - b.center.z) < (a.extents.z + b.extents.z);
                    if (!overlapX || !overlapZ) continue;

                    // A bridging link = one whose two endpoints land in the two different scene footprints.
                    bool bridged = false;
                    foreach (var link in links)
                    {
                        if (link == null || !link.isActiveAndEnabled) continue;
                        Transform t = link.transform;
                        Vector3 sW = t.TransformPoint(link.startPoint);
                        Vector3 eW = t.TransformPoint(link.endPoint);
                        bool sInA = InXZ(a, sW), eInB = InXZ(b, eW);
                        bool sInB = InXZ(b, sW), eInA = InXZ(a, eW);
                        if ((sInA && eInB) || (sInB && eInA)) { bridged = true; break; }
                    }
                    if (bridged) continue;

                    string key = "nolink:" + PairKey(sceneBounds[i].Key, sceneBounds[j].Key);
                    if (!_navLinkSeen.Add(key)) continue;
                    FlowTrace.Fail(Tag,
                        $"NAVMESH-LINK MISSING: additive scenes '{sceneBounds[i].Key}' and '{sceneBounds[j].Key}' overlap in XZ " +
                        "but NO NavMeshLink bridges them — the seam is not walkable (warp-only / strands). This is the WO-453 castle<->OuterWorld class.");
                }
            }
        }

        private static bool InXZ(Bounds b, Vector3 p)
        {
            return Mathf.Abs(p.x - b.center.x) <= b.extents.x
                && Mathf.Abs(p.z - b.center.z) <= b.extents.z;
        }

        // World-space XZ bounds of a scene's active renderers (cheap structural proxy for
        // its footprint). Returns null if the scene has no renderable footprint.
        private static Bounds? SceneXZBounds(Scene sc)
        {
            Bounds acc = default;
            bool any = false;
            var roots = sc.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == null || !root.activeInHierarchy) continue;
                var rends = root.GetComponentsInChildren<Renderer>(false);
                foreach (var r in rends)
                {
                    if (r == null || !r.enabled) continue;
                    if (!any) { acc = r.bounds; any = true; }
                    else acc.Encapsulate(r.bounds);
                }
            }
            return any ? acc : (Bounds?)null;
        }

        // =====================================================================
        //  PROBE 4b: STRANDED
        //  If the hero's NavMeshAgent has had pathStatus != complete (or made no
        //  progress toward any objective) for >~20s, Fail (possible softlock — no
        //  path to an exit). Throttled (StrandedInterval, 1/sec) with a hold window.
        // =====================================================================
        private void CheckStranded(float now)
        {
            if (_hero == null || _heroAgent == null) return;
            var agent = _heroAgent;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                // Off-mesh / disabled (warp, lift) — not a stranding condition; reset.
                ResetStranded();
                return;
            }

            // Idle by design: no path set + at rest = the bot simply isn't trying to go
            // anywhere this beat (between phases). Not stranded.
            bool hasGoal = agent.hasPath || agent.pathPending ||
                           (!agent.isStopped && agent.remainingDistance > agent.stoppingDistance + 0.05f);
            Vector3 pos = _hero.transform.position;

            if (!hasGoal)
            {
                ResetStranded();
                _lastHeroPos = pos;
                return;
            }

            // Progress = either the remaining distance shrank, OR the hero physically moved.
            float remaining = agent.pathPending ? float.PositiveInfinity : agent.remainingDistance;
            bool distClosed = remaining < _lastRemainingDist - 0.25f;
            bool moved = !float.IsNaN(_lastHeroPos.x) &&
                         (Vector3.Distance(new Vector3(pos.x, 0f, pos.z),
                                           new Vector3(_lastHeroPos.x, 0f, _lastHeroPos.z)) > 0.25f);

            bool badPath = agent.pathStatus != NavMeshPathStatus.PathComplete;

            if (distClosed || moved)
            {
                // Making progress — clear any open stranding window.
                ResetStranded();
            }
            else
            {
                // No progress this tick. Open the window on first stall; fire once it holds.
                if (_strandedSince < 0f) _strandedSince = now;
                float held = now - _strandedSince;
                if (held >= StrandedHoldSeconds && !_strandedReported)
                {
                    _strandedReported = true;
                    FlowTrace.Fail(Tag, $"hero stranded — no path to exit (possible softlock): pathStatus={agent.pathStatus}, " +
                        $"remaining={remaining:0.0}m, no progress for {held:0}s at {pos}" +
                        (badPath ? " (path is partial/invalid)." : "."));
                }
            }

            _lastRemainingDist = remaining;
            _lastHeroPos = pos;
        }

        private void ResetStranded()
        {
            _strandedSince = -1f;
            _strandedReported = false;
            _lastRemainingDist = float.PositiveInfinity;
        }

        // =====================================================================
        //  PROBE 5: SEAM-REACHABLE
        //  Runtime generalisation of the editor-only CastleGateNavVerify. Every
        //  SceneTransitionTrigger must (a) sit ON the baked navmesh and (b) be
        //  reachable-on-foot from the hero to within its ProximityRadius. A seam the
        //  hero can't walk up to never fires — the player is stuck at the crossing.
        //  This is the class behind the 2026-06-19 castle→OuterWorld bridge fix (a
        //  trigger floated 1.5m above the deck, read off-mesh, and the deck never
        //  fused to the courtyard); this oracle regression-guards it every run.
        //
        //  FALSE-POSITIVE DISCIPLINE: a seam in a DIFFERENT scene than the hero sits
        //  legitimately across a warp boundary, so an incomplete path there is NOT a
        //  defect (census-only). An off-mesh seam, or an unreachable seam WITHIN the
        //  hero's own scene, is a hard Fail — but only after TWO consecutive scans
        //  read it bad (SeamStrikesToFail), so a transient first-tick read (hero mid-
        //  warp, navmesh settling) never files a false ticket.
        // =====================================================================
        private const float SeamSampleTol  = 2.0f;   // seam must sit within this of baked navmesh
        private const float SeamReachMargin = 1.0f;  // path must close to within radius + this
        private const int   SeamStrikesToFail = 2;   // consecutive bad reads before a Fail fires
        private bool _seamCensusDone;
        private readonly Dictionary<string, int> _seamStrikes = new Dictionary<string, int>();
        private readonly HashSet<string> _seamReported = new HashSet<string>();

        private void CheckSeamReachable()
        {
            if (_hero == null) return;
            Vector3 heroPos = _hero.transform.position;
            // The hero must be on the navmesh for a path query to mean anything — skip during
            // warps / lifts (off-mesh) so a transient airborne frame can't read every seam bad.
            if (!NavMesh.SamplePosition(heroPos, out NavMeshHit hHero, SeamSampleTol + 1f, NavMesh.AllAreas))
                return;

            var seams = UnityEngine.Object.FindObjectsByType<SceneTransitionTrigger>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (!_seamCensusDone)
            {
                _seamCensusDone = true;
                FlowTrace.Step(Tag, $"SEAM-REACHABLE census: {(seams != null ? seams.Length : 0)} active " +
                    $"SceneTransitionTrigger(s) across {SceneManager.sceneCount} loaded scene(s).");
            }
            if (seams == null || seams.Length == 0) return;

            string heroScene = _hero.gameObject.scene.name;

            foreach (var seam in seams)
            {
                if (seam == null || !seam.isActiveAndEnabled) continue;
                Vector3 seamPos = seam.transform.position;
                string id = seam.gameObject.scene.name + ":" + seam.gameObject.name;

                // (a) The seam must sit ON the navmesh. A floating / stranded trigger (the y=1.5
                //     bridge trigger that read off-mesh on 2026-06-19) can never be walked to.
                bool seamOnMesh = NavMesh.SamplePosition(seamPos, out NavMeshHit hSeam, SeamSampleTol, NavMesh.AllAreas);

                // (b) Reachability from the hero — only meaningful when the seam is on-mesh.
                bool reachable = false;
                NavMeshPathStatus status = NavMeshPathStatus.PathInvalid;
                float approach = float.PositiveInfinity;
                if (seamOnMesh)
                {
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(hHero.position, hSeam.position, NavMesh.AllAreas, path);
                    status = path.status;
                    int corners = path.corners != null ? path.corners.Length : 0;
                    Vector3 last = corners > 0 ? path.corners[corners - 1] : hHero.position;
                    approach = Vector3.Distance(last, seamPos);
                    reachable = status == NavMeshPathStatus.PathComplete
                                && approach <= seam.ProximityRadius + SeamReachMargin;
                }

                bool sameScene = string.Equals(seam.gameObject.scene.name, heroScene, StringComparison.Ordinal);

                // Classify: what (if anything) is wrong with this seam this tick.
                //  - off-mesh seam            = defect anywhere
                //  - on-mesh but unreachable  = defect ONLY within the hero's own scene
                //                               (cross-scene = warp boundary, expected)
                string problem = null;
                if (!seamOnMesh) problem = "offmesh";
                else if (!reachable && sameScene) problem = "unreach";

                if (problem == null)
                {
                    // Good (or a cross-scene warp seam) — clear any accrued strikes.
                    _seamStrikes.Remove(id);
                    continue;
                }

                // Two-strike confirm so a transient first read never files a false ticket.
                if (_seamReported.Contains(id)) continue;
                _seamStrikes.TryGetValue(id, out int n);
                n++;
                _seamStrikes[id] = n;
                if (n < SeamStrikesToFail) continue;
                _seamReported.Add(id);

                if (problem == "offmesh")
                    FlowTrace.Fail(Tag, $"SEAM-OFF-MESH: '{seam.gameObject.name}' in '{seam.gameObject.scene.name}' at {seamPos} " +
                        $"is not within {SeamSampleTol}m of any baked navmesh — the hero can never walk up to it; the seam can't fire.");
                else
                    FlowTrace.Fail(Tag, $"SEAM-UNREACHABLE: '{seam.gameObject.name}' in '{seam.gameObject.scene.name}' — the hero cannot walk to it " +
                        $"(path={status}, closest {approach:0.0}m vs ProximityRadius {seam.ProximityRadius:0.0}m). Bake gap or blocker between the hero and the seam; the crossing never fires.");
            }
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
