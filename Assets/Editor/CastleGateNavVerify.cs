// =============================================================================
// CastleGateNavVerify — READ-ONLY behavioral diagnostic for the
// "can't exit the castle" bug. PROVES (not assumes) whether a NavMeshAgent hero
// can path from the spawn marker out to the south-gate SceneTransitionTrigger
// (the proximity seam that loads OuterWorld). It does NOT modify or save the scene.
//
// WHY this exists: the castle exit is PROXIMITY-based (SceneTransitionTrigger fires
// when the hero comes within ProximityRadius of the gate marker, then WarpTo's to
// OuterWorld). Two independent things must both hold for the hero to exit:
//   (a) a COMPLETE NavMesh path from the spawn to the gate opening (else the agent
//       stalls at a wall/keep edge and never approaches the gate), AND
//   (b) the hero's CLOSEST reachable point to the trigger must be <= ProximityRadius
//       (else it can stand at the opening but never trip the seam).
// A source-grep can't see either; only a real NavMesh path query can. This is the
// behavioral gate the castle work was missing (the session's lesson: "the test is
// the lie" when it asserts code exists instead of behavior working).
//
// Batchmode: -executeMethod DeNelle.Editor.CastleGateNavVerify.Diagnose
// Logs "GATE_NAV_OK :: <detail>" or "GATE_NAV_FAIL :: <detail>".
// =============================================================================
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class CastleGateNavVerify
    {
        private const string ScenePath = "Assets/Scenes/MainCastle_Hall.unity";

        [MenuItem("Defenders/Castle/Verify Gate Nav (path spawn -> exit trigger)")]
        public static void VerifyMenu()
        {
            bool ok = Verify(out string detail);
            Debug.Log("[CastleGateNavVerify] " + (ok ? "GATE_NAV_OK" : "GATE_NAV_FAIL") + " :: " + detail);
        }

        // Batchmode entry (opens the scene itself).
        public static void Diagnose()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool ok = VerifyOpenScene(out string detail);
            Debug.Log("[CastleGateNavVerify] " + (ok ? "GATE_NAV_OK" : "GATE_NAV_FAIL") + " :: " + detail);
        }

        // PER-GATE reachability (read-only): paths the spawn -> EVERY SceneTransitionTrigger in
        // the scene and reports, for each, PathComplete + closest approach vs ProximityRadius. This
        // answers "can the hero reach ALL 4 gates" (the single-trigger Verify only checks the first
        // seam). Does NOT modify or save the scene. Batchmode: DiagnoseAllGates.
        public static void DiagnoseAllGates()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int surfaces = ReloadCommittedNavMesh();
            Debug.Log("[AllGates] NavMeshSurfaces with persisted data = " + surfaces);

            var spawnGo = GameObject.Find("HeroStartPoint_PlayerSpawn")
                       ?? GameObject.Find("HeroStartPoint_InsidePersonalQuarters")
                       ?? GameObject.Find("Capsule");
            if (spawnGo == null) { Debug.Log("[AllGates] GATE_NAV_FAIL :: no hero spawn marker"); return; }
            Vector3 spawn = spawnGo.transform.position;
            bool sSpawn = NavMesh.SamplePosition(spawn, out NavMeshHit hSpawn, 5f, NavMesh.AllAreas);
            Debug.Log($"[AllGates] spawn={spawn} onMesh={sSpawn}");
            if (!sSpawn) { Debug.Log("[AllGates] GATE_NAV_FAIL :: spawn not on navmesh"); return; }

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType == null) { Debug.Log("[AllGates] GATE_NAV_FAIL :: SceneTransitionTrigger type not found"); return; }
            var comps = UnityEngine.Object.FindObjectsByType(transType);
            Debug.Log("[AllGates] trigger count = " + comps.Length);

            var fR = transType.GetField("ProximityRadius");
            var fTgt = transType.GetField("targetSceneName");
            var fConf = transType.GetField("requireConfirm");
            int reachable = 0;
            foreach (var c in comps)
            {
                var mb = c as MonoBehaviour;
                if (mb == null) continue;
                Vector3 tp = mb.transform.position;
                float radius = (fR != null && fR.GetValue(mb) is float f) ? f : 6f;
                string tgt = fTgt?.GetValue(mb) as string ?? "?";
                bool conf = fConf != null && fConf.GetValue(mb) is bool b && b;

                bool sTrig = NavMesh.SamplePosition(tp, out NavMeshHit hTrig, 1.0f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                Vector3 target = sTrig ? hTrig.position : tp;
                NavMesh.CalculatePath(hSpawn.position, target, NavMesh.AllAreas, path);
                int corners = path.corners != null ? path.corners.Length : 0;
                Vector3 last = corners > 0 ? path.corners[corners - 1] : hSpawn.position;
                float approach = Vector3.Distance(last, tp);
                bool complete = path.status == NavMeshPathStatus.PathComplete;
                bool within = approach <= radius + 0.5f;
                bool ok = complete && within;
                if (ok) reachable++;
                Debug.Log($"[AllGates] '{mb.name}' target={tgt} requireConfirm={conf} pos={tp} radius={radius} " +
                          $"pathStatus={path.status} approach={approach:F1}m within={within} -> {(ok ? "REACHABLE" : "UNREACHABLE")}");
            }
            Debug.Log($"[AllGates] {(reachable == comps.Length ? "GATE_NAV_OK" : "GATE_NAV_FAIL")} :: {reachable}/{comps.Length} gates reachable from spawn.");
        }

        // WO-468 4-LANE DIAGNOSTIC (read-only): the runtime SceneTransitionTriggers do NOT exist
        // in the saved scene (RuntimeRegionGate builds them at play time), so DiagnoseAllGates sees
        // 0 triggers. This instead path-tests the hero spawn -> EACH of the 4 recipe-derived gate
        // openings + their exit-strip OUTER ends on the COMMITTED castle navmesh, naming exactly
        // which side(s) the navmesh fails to reach and the closest approach. This is the §12 capture
        // for "only south traversable". Batchmode: DiagnoseFourLanes.
        public static void DiagnoseFourLanes()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int surfaces = ReloadCommittedNavMesh();
            Debug.Log("[FourLanes] NavMeshSurfaces with persisted data = " + surfaces);

            var spawnGo = GameObject.Find("HeroStartPoint_PlayerSpawn")
                       ?? GameObject.Find("HeroStartPoint_InsidePersonalQuarters")
                       ?? GameObject.Find("Capsule");
            if (spawnGo == null) { Debug.Log("[FourLanes] GATE_NAV_FAIL :: no hero spawn marker"); return; }
            Vector3 spawn = spawnGo.transform.position;
            bool sSpawn = NavMesh.SamplePosition(spawn, out NavMeshHit hSpawn, 5f, NavMesh.AllAreas);
            Debug.Log($"[FourLanes] spawn={spawn} onMesh={sSpawn}");
            if (!sSpawn) { Debug.Log("[FourLanes] GATE_NAV_FAIL :: spawn not on navmesh"); return; }

            Vector3 southGate = ReadRecipeSouthGate();   // (-4.37,0,-40.6)
            // The exit strip extends ~18m OUTWARD past the gate (BuildGateExitStrips outsideReach),
            // and the runtime threshold sits ~22m back from the gate on the deck. Probe a point ~12m
            // outward of the gate (representative of where the hero must reach to trip the seam).
            var sides = new[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };
            int reached = 0;
            foreach (var (label, yaw) in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 gateW = rot * southGate;                       // gate opening, this side
                Vector3 outward = rot * Vector3.back;                  // south(-Z) rotated
                Vector3 stripOuter = gateW + outward * 12f;            // ~12m out, on the exit strip
                stripOuter.y = 0f;
                gateW.y = 0f;

                bool gOn = NavMesh.SamplePosition(gateW, out NavMeshHit hG, 3f, NavMesh.AllAreas);
                bool oOn = NavMesh.SamplePosition(stripOuter, out NavMeshHit hO, 3f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                Vector3 tgt = oOn ? hO.position : (gOn ? hG.position : stripOuter);
                NavMesh.CalculatePath(hSpawn.position, tgt, NavMesh.AllAreas, path);
                int corners = path.corners != null ? path.corners.Length : 0;
                Vector3 last = corners > 0 ? path.corners[corners - 1] : hSpawn.position;
                float approach = Vector3.Distance(last, stripOuter);
                bool complete = path.status == NavMeshPathStatus.PathComplete;
                bool ok = complete && oOn && approach <= 5f;
                if (ok) reached++;
                Debug.Log($"[FourLanes] {label}(yaw={yaw}) gateWorld={gateW} gateOnMesh={gOn} " +
                          $"stripOuter={stripOuter} stripOnMesh={oOn} pathStatus={path.status} " +
                          $"approach={approach:F1}m -> {(ok ? "REACHABLE" : "UNREACHABLE")}");
            }
            Debug.Log($"[FourLanes] {(reached == 4 ? "GATE_NAV_OK" : "GATE_NAV_FAIL")} :: {reached}/4 gate lanes reachable from spawn.");
        }

        // WO-468 wrapped-seam: verify the OuterWorld navmesh is WALKABLE at the 4 gate LANDINGS
        // (±66 on each axis) and CARVED (hole) under the castle footprint (origin). Opens OuterWorld
        // solo, loads its committed navmesh, samples each point. Batchmode: DiagnoseOuterWorldLandings.
        public static void DiagnoseOuterWorldLandings()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/OuterWorld.unity", OpenSceneMode.Single);
            int surfaces = ReloadCommittedNavMesh();
            Debug.Log("[OWLandings] OuterWorld NavMeshSurfaces with data = " + surfaces);

            // 4 landings = south fallback (-4.37,0.5,-66) rotated 0/90/180/270 about origin.
            Vector3 southLanding = new Vector3(-4.37f, 0.5f, -66f);
            var sides = new[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };
            int walkable = 0;
            foreach (var (label, yaw) in sides)
            {
                Vector3 land = Quaternion.Euler(0f, yaw, 0f) * southLanding;
                bool on = NavMesh.SamplePosition(land, out NavMeshHit h, 4f, NavMesh.AllAreas);
                if (on) walkable++;
                Debug.Log($"[OWLandings] {label} landing={land} onMesh={on} hit={(on ? h.position.ToString() : "n/a")}");
            }
            // Hole check: origin should NOT be walkable (carved by the Not-Walkable volume).
            bool originOn = NavMesh.SamplePosition(Vector3.zero, out NavMeshHit ho, 3f, NavMesh.AllAreas);
            Debug.Log($"[OWLandings] origin(castle footprint) onMesh={originOn} (EXPECT False — carved hole) hit={(originOn ? ho.position.ToString() : "n/a")}");
            Debug.Log($"[OWLandings] {(walkable == 4 && !originOn ? "OW_LANDINGS_OK" : "OW_LANDINGS_CHECK")} :: {walkable}/4 landings walkable, originHole={!originOn}.");
        }

        // RUNTIME diagnostic for "reach gate, nothing happens": dumps the actual state of
        // the SceneTransitionTrigger seam (active? enabled? position? radius? target?),
        // whether OuterWorld is loadable, and which objects carry the Player/HeroTarget tag
        // the trigger's ResolveHero() looks for. Catches the factors a navmesh path-query
        // cannot (a disabled/inactive trigger, a missing hero tag, an unloadable target).
        public static void DiagnoseExitRuntime()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[ExitDiag] opened " + ScenePath);

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType == null) { Debug.LogError("[ExitDiag] SceneTransitionTrigger TYPE not found (not compiled?)"); return; }

            var comps = UnityEngine.Object.FindObjectsByType(transType, FindObjectsInactive.Include);
            Debug.Log("[ExitDiag] SceneTransitionTrigger count (incl inactive) = " + comps.Length);
            foreach (var c in comps)
            {
                var mb = c as MonoBehaviour;
                if (mb == null) continue;
                var go = mb.gameObject;
                string tgt = transType.GetField("targetSceneName")?.GetValue(mb) as string ?? "?";
                object rad = transType.GetField("ProximityRadius")?.GetValue(mb);
                object tpos = transType.GetField("targetPosition")?.GetValue(mb);
                object add = transType.GetField("loadAdditive")?.GetValue(mb);
                Debug.Log($"[ExitDiag] trigger '{go.name}' worldPos={mb.transform.position} " +
                          $"activeInHierarchy={go.activeInHierarchy} compEnabled={mb.enabled} " +
                          $"target={tgt} radius={rad} warpTo={tpos} additive={add} " +
                          $"parent={(go.transform.parent != null ? go.transform.parent.name : "<root>")} " +
                          $"parentActive={(go.transform.parent != null ? go.transform.parent.gameObject.activeInHierarchy.ToString() : "n/a")}");
            }

            Debug.Log("[ExitDiag] CanStreamedLevelBeLoaded(OuterWorld) = " + Application.CanStreamedLevelBeLoaded("OuterWorld"));

            foreach (var tag in new[] { "Player", "HeroTarget" })
            {
                try
                {
                    var gos = GameObject.FindGameObjectsWithTag(tag);
                    Debug.Log($"[ExitDiag] tag '{tag}': {gos.Length} object(s)" +
                              (gos.Length == 0 ? " (hero is likely runtime-spawned — tag applied at spawn)" : ": " +
                               string.Join(", ", System.Array.ConvertAll(gos, g => g.name + "@" + g.transform.position))));
                }
                catch (System.Exception e) { Debug.Log($"[ExitDiag] tag '{tag}': ERROR {e.Message}"); }
            }

            var spawn = GameObject.Find("HeroStartPoint_PlayerSpawn") ?? GameObject.Find("Capsule");
            Debug.Log("[ExitDiag] spawn marker = " + (spawn != null ? spawn.name + "@" + spawn.transform.position : "<none>"));
            Debug.Log("[ExitDiag] DONE");
        }

        // MEASURE the REAL placed gate geometry (no extrapolation): for each CastleSide_*,
        // read the gate child's actual world-space renderer bounds (center, size, floor Y)
        // so the exit plane can be fit to what is actually there. Read-only.
        public static void MeasureGates()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[GateMeasure] opened " + ScenePath);

            foreach (var side in new[] { "South", "West", "North", "East" })
            {
                var parent = GameObject.Find("CastleSide_" + side);
                if (parent == null) { Debug.LogWarning("[GateMeasure] CastleSide_" + side + " NOT FOUND"); continue; }

                Transform gate = null;
                foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                    if (t.name.IndexOf("Gate", System.StringComparison.OrdinalIgnoreCase) >= 0) { gate = t; break; }
                if (gate == null) { Debug.LogWarning("[GateMeasure] no Gate child under CastleSide_" + side); continue; }

                var rends = gate.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0) { Debug.LogWarning("[GateMeasure] '" + gate.name + "' has no renderers"); continue; }
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                Debug.Log($"[GateMeasure] {side}: gate='{gate.name}' worldPos={gate.position} " +
                          $"boundsCenter={b.center} boundsSize={b.size} minY={b.min.y:F3} maxY={b.max.y:F3}");
            }

            var floor = GameObject.Find("CourtyardFloor_Nav");
            if (floor != null)
            {
                var fr = floor.GetComponentInChildren<Renderer>();
                Debug.Log($"[GateMeasure] CourtyardFloor_Nav transform.y={floor.transform.position.y}" +
                          (fr != null ? $" rendererBoundsY={fr.bounds.center.y:F3}" : ""));
            }
            else Debug.Log("[GateMeasure] CourtyardFloor_Nav not found (built at bake)");
            Debug.Log("[GateMeasure] DONE");
        }

        // Opens the scene then verifies (menu/standalone use).
        public static bool Verify(out string detail)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return VerifyOpenScene(out detail);
        }

        // Verifies the ALREADY-OPEN castle scene (so a rebuild pipeline can call this
        // in-session right after baking, with no second scene open).
        public static bool VerifyOpenScene(out string detail)
        {
            // 1. Load the committed navmesh into the runtime query system (clean slate first
            //    so an ExecuteAlways auto-add can't double-count). After this, NavMesh.* sees
            //    exactly the persisted bake.
            int surfaces = ReloadCommittedNavMesh();
            Debug.Log("[CastleGateNavVerify] NavMeshSurfaces with persisted data = " + surfaces);
            if (surfaces == 0)
            {
                detail = "no baked NavMeshSurface data found — castle has no navmesh to walk on";
                return false;
            }

            // 2. Hero spawn marker (the agent spawns here at runtime).
            var spawnGo = GameObject.Find("HeroStartPoint_PlayerSpawn")
                       ?? GameObject.Find("HeroStartPoint_InsidePersonalQuarters")
                       ?? GameObject.Find("Capsule");
            if (spawnGo == null) { detail = "no hero spawn marker (HeroStartPoint_PlayerSpawn / Capsule) in scene"; return false; }
            Vector3 spawn = spawnGo.transform.position;

            // 3. The exit-lane verify target. Preferred: a baked SceneTransitionTrigger (legacy
            //    scenes). RETIREMENT FALLBACK (owner F8 2026-07-02 "travel to outer world should
            //    not show"): CastleHubBuilder.EnsureExitSeamAtRecipeGate no longer bakes a trigger
            //    (the interior seam's 40m EffRadius prompt blanketed the courtyard; crossing =
            //    RuntimeRegionGate warp gates, CANON_GROUND_TRUTH_2026-07-01). It keeps a PLAIN
            //    "WorldGate_ConnectToOuterWorld_Marker" on the gate lane, so the spawn->gate
            //    reachability assertion is preserved by path-testing the marker instead, with the
            //    retired trigger's 12m lane radius as the acceptance tolerance.
            const float RetiredSeamLaneRadius = 12f;   // the retired baked trigger's ProximityRadius
            Vector3 triggerPos = Vector3.zero;
            float radius = 6f;
            bool foundTrigger = false;
            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType != null)
            {
                var comps = UnityEngine.Object.FindObjectsByType(transType);
                if (comps != null && comps.Length > 0)
                {
                    var mb = comps[0] as MonoBehaviour;
                    if (mb != null)
                    {
                        triggerPos = mb.transform.position;
                        var fR = transType.GetField("ProximityRadius");
                        if (fR != null) { object v = fR.GetValue(mb); if (v is float f) radius = f; }
                        foundTrigger = true;
                    }
                }
            }
            Vector3 recipeGate = ReadRecipeSouthGate();
            if (!foundTrigger)
            {
                var laneMarker = GameObject.Find("WorldGate_ConnectToOuterWorld_Marker");
                if (laneMarker != null)
                {
                    triggerPos = laneMarker.transform.position;
                    radius = RetiredSeamLaneRadius;
                    foundTrigger = true;
                    Debug.Log("[CastleGateNavVerify] no baked SceneTransitionTrigger (RETIRED 2026-07-02) — " +
                              "verifying spawn -> gate-lane marker at " + triggerPos + " (tolerance " + radius + "m).");
                }
            }
            if (!foundTrigger)
            {
                detail = "no SceneTransitionTrigger AND no WorldGate_ConnectToOuterWorld_Marker in scene — " +
                         "there is NO exit-lane target to verify (recipe south gate is at " + recipeGate +
                         "; run EnsureExitSeamAtRecipeGate to restore the lane marker)";
                return false;
            }

            // 4. Sample spawn + trigger onto the navmesh; compute the path.
            bool sSpawn = NavMesh.SamplePosition(spawn, out NavMeshHit hSpawn, 5f, NavMesh.AllAreas);
            // T-001 honesty fix (2026-06-13): tolerance 14f -> 1.0f. A 14m snap let the sampler
            // jump the trigger onto ANY nearby walkable patch — even when the gate strip never
            // fused — and reported a false PathComplete/EXITABLE (memory batchmode-spatial-verify-traps:
            // "SamplePosition tolerance snaps a target back and fakes a complete path"). At 1.0m the
            // trigger must sit on genuinely-connected mesh for the path to resolve, so an unfused gate
            // now correctly reads GATE_NAV_FAIL.
            bool sTrig  = NavMesh.SamplePosition(triggerPos, out NavMeshHit hTrig, 1.0f, NavMesh.AllAreas);
            Debug.Log($"[CastleGateNavVerify] spawn={spawn} onMesh={sSpawn}({(sSpawn ? hSpawn.position.ToString() : "-")})");
            Debug.Log($"[CastleGateNavVerify] trigger={triggerPos} radius={radius} onMesh={sTrig}({(sTrig ? hTrig.position.ToString() : "-")})");
            Debug.Log($"[CastleGateNavVerify] recipeSouthGate={recipeGate}");

            if (!sSpawn) { detail = $"spawn {spawn} is not on the navmesh (no walkable ground within 5m) — hero spawns stuck"; return false; }

            var path = new NavMeshPath();
            Vector3 target = sTrig ? hTrig.position : triggerPos;
            NavMesh.CalculatePath(hSpawn.position, target, NavMesh.AllAreas, path);

            int corners = path.corners != null ? path.corners.Length : 0;
            Vector3 lastCorner = corners > 0 ? path.corners[corners - 1] : hSpawn.position;
            float approach = Vector3.Distance(lastCorner, triggerPos); // closest the hero can get to the seam
            float navEdgeGapToTrigger = sTrig ? Vector3.Distance(hTrig.position, triggerPos) : 999f;

            Debug.Log($"[CastleGateNavVerify] path.status={path.status} corners={corners} lastCorner={lastCorner} " +
                      $"closestApproachToTrigger={approach:F1}m navEdgeGapToTrigger={navEdgeGapToTrigger:F1}m");

            bool complete = path.status == NavMeshPathStatus.PathComplete;
            bool withinRadius = approach <= radius + 0.5f;

            if (complete && withinRadius)
            {
                detail = $"EXITABLE — PathComplete, hero reaches within {approach:F1}m of the seam (radius {radius}m) so it fires.";
                return true;
            }
            if (complete && !withinRadius)
            {
                detail = $"NOT exitable — path is complete but the hero's closest reachable point is {approach:F1}m " +
                         $"from the trigger (> ProximityRadius {radius}m): hero walks to the gate but never trips the seam. " +
                         $"FIX: move the trigger onto the recipe gate opening {recipeGate}, or raise ProximityRadius.";
                return false;
            }
            detail = $"NOT exitable — NavMesh path is {path.status} (no complete spawn->gate route): the navmesh is not " +
                     $"connected from spawn through the gate. FIX: ensure the gate-exit strip + keep entrance fuse on bake.";
            return false;
        }

        // Remove all runtime navmesh, then re-add only the committed per-surface data so the
        // query system reflects exactly the persisted bake. Returns surfaces with data.
        private static int ReloadCommittedNavMesh()
        {
            NavMesh.RemoveAllNavMeshData();
            var surfType = FindType("Unity.AI.Navigation.NavMeshSurface");
            if (surfType == null) return 0;
            var dataProp = surfType.GetProperty("navMeshData");
            var surfaces = UnityEngine.Object.FindObjectsByType(surfType);
            int n = 0;
            foreach (var s in surfaces)
            {
                var data = dataProp != null ? dataProp.GetValue(s) as NavMeshData : null;
                if (data != null) { NavMesh.AddNavMeshData(data); n++; }
            }
            return n;
        }

        [Serializable] private class Piece  { public string name; public float[] pos; }
        [Serializable] private class Recipe { public Piece[] pieces; }

        private static Vector3 ReadRecipeSouthGate()
        {
            Vector3 fallback = new Vector3(-4.37f, 0f, -40.6f);
            var ta = Resources.Load<TextAsset>("Data/castle-south-recipe");
            if (ta == null) return fallback;
            var recipe = JsonUtility.FromJson<Recipe>(ta.text);
            if (recipe != null && recipe.pieces != null)
                foreach (var p in recipe.pieces)
                    if (p != null && p.name == "Gate_South" && p.pos != null && p.pos.Length == 3)
                        return new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            return fallback;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
