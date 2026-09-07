// =============================================================================
// CastleNavTopologyDiag — runtime navmesh-TOPOLOGY RCA for the castle→overworld seam.
//
// THE BUG IT HUNTS: the AutoPilot bot reports the castle→overworld exit as
// SEAM-UNREACHABLE in MainCastle_Hall, yet edit-time CastleGateNavVerify reports
// GATE_NAV_OK / PathComplete and the owner felt-tested "all gates work". The
// contradiction smells like a DISCONNECTED NAVMESH ISLAND: the hero's courtyard
// navmesh may not be stitched to the gate-seam navmesh at RUNTIME (after the
// RuntimeRegionGate rebake + WorldSceneLoader additive overworld merge), even though
// edit-time SamplePosition false-greens because the bake looks fine statically.
//
// §12 INSTRUMENT-DON'T-GUESS: this captures the DATA that proves connected vs
// island — it samples the hero + each seam target onto the live navmesh and runs
// NavMesh.CalculatePath hero→seam, logging PathComplete vs PathPartial/Invalid for
// EACH gate, plus a courtyard reachability ring to map the reachable region extent.
//
// RUNTIME-ONLY by design: the navmesh is only live in play mode / the built player.
// Edit-time SamplePosition false-greens (that IS the bug), so there is deliberately
// NO [MenuItem] edit-time entry point — it self-bootstraps after scene load and
// runs ONCE, gated to MainCastle_Hall. Output goes through FlowTrace so it lands in
// break-log.jsonl / Player.log for harvest.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    public sealed class CastleNavTopologyDiag : MonoBehaviour
    {
        private const string Sys = "CastleNavTopo";
        private const string TargetScene = "MainCastle_Hall";
        private const float SettleSeconds = 1.5f;  // let RuntimeRegionGate rebake + WorldSceneLoader additive finish
        private const float SampleRadius = 3f;

        private bool _captured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // OPT-IN (2026-06-29): this diagnostic did its job — it proved the castle exit is reachable
            // by PROXIMITY (hero path closes to 0.10m of the seam, < the 12m warp radius) and that the
            // PathPartial status is a benign sub-decimetre weld seam, NOT a disconnect. Auto-running it
            // every session now just spams PathPartial "Fail" lines (its verdict predates the proximity
            // RCA). Keep it for future manual RCA but DON'T auto-fire: enable with PlayerPrefs
            // "diag.castlenav" = 1. Default OFF.
            if (PlayerPrefs.GetInt("diag.castlenav", 0) != 1) return;

            var go = new GameObject("__CastleNavTopologyDiag");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            var diag = go.AddComponent<CastleNavTopologyDiag>();
            SceneManager.sceneLoaded += diag.OnSceneLoaded;

            // If MainCastle_Hall is already the active scene at boot (e.g. it was the
            // start scene loaded before this hook), kick off directly too.
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == TargetScene)
                diag.TryStart();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene)
                TryStart();
        }

        private void TryStart()
        {
            if (_captured) return;
            // guard double-kick (OnSceneLoaded + boot active-scene path)
            StopAllCoroutines();
            StartCoroutine(CaptureAfterSettle());
        }

        private IEnumerator CaptureAfterSettle()
        {
            yield return new WaitForSeconds(SettleSeconds);
            if (_captured) yield break;
            _captured = true;

            FlowTrace.Step(Sys, $"=== CASTLE NAV TOPOLOGY CAPTURE START (scene={TargetScene}, settled={SettleSeconds}s) ===");

            Vector3 heroSampledPos = Vector3.zero;
            bool heroOnMesh = false;

            // -------- SECTION 1: hero on-mesh? -----------------------------------
            Guard.Try(Sys, "section1-hero", () =>
            {
                var hero = GameObject.FindWithTag("Player");
                if (hero == null)
                {
                    // WO-1513: the old fallback read the "HeroTarget" tag, which
                    // TagManager.asset has never declared — a permanently dead branch.
                    // The hero definitively carries HeroLocomotion (CLAUDE.md §7).
                    Guard.Try(Sys, "hero-fallback-component", () =>
                    {
                        var loco = FindFirstObjectByType<HeroLocomotion>();
                        if (loco != null) hero = loco.gameObject;
                    });
                }

                if (hero == null)
                {
                    FlowTrace.Fail(Sys, "HERO_NOT_FOUND: no GameObject tagged 'Player' and no HeroLocomotion in the scene");
                    return;
                }

                Vector3 hp = hero.transform.position;
                FlowTrace.Step(Sys, $"HERO '{hero.name}' worldPos={Fmt(hp)}");

                if (NavMesh.SamplePosition(hp, out NavMeshHit heroHit, SampleRadius, NavMesh.AllAreas))
                {
                    heroOnMesh = true;
                    heroSampledPos = heroHit.position;
                    float snap = Vector3.Distance(hp, heroHit.position);
                    int areaIdx = MaskToAreaIndex(heroHit.mask);
                    FlowTrace.Step(Sys, $"HERO_ON_MESH: sampled={Fmt(heroHit.position)} snapDist={snap:F2}m mask={heroHit.mask} area~{areaIdx}");
                }
                else
                {
                    FlowTrace.Fail(Sys, $"HERO_OFF_MESH: NavMesh.SamplePosition found NO navmesh within {SampleRadius}m of {Fmt(hp)}");
                }
            });

            // -------- SECTION 2: seam targets on-mesh? ---------------------------
            // collected (name, sampledPos, onMesh) for the connectivity pass
            var seamTargets = new List<SeamTarget>();

            Guard.Try(Sys, "section2-seams", () =>
            {
                // named singletons
                AddNamedTarget(seamTargets, "WorldGate_ConnectToOuterWorld_Marker");
                AddNamedTarget(seamTargets, "Floor_Bridge_Nav");

                // runtime seam objects (prefix __RuntimeSeam_ or contains RuntimeSeam_Trigger)
                Guard.Try(Sys, "section2-runtimeseams", () =>
                {
                    var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                    foreach (var t in all)
                    {
                        if (t == null) continue;
                        string n = t.name;
                        if (n.StartsWith("__RuntimeSeam_") || n.Contains("RuntimeSeam_Trigger"))
                            AddTarget(seamTargets, n, t.position);
                    }
                });

                // SceneTransitionTrigger components — WO-1511: SceneTransitionTrigger lives in
                // THIS assembly (DeNelle.Village), so the type is nameable directly and the
                // "type not resolved via reflection" Warn branch is now unreachable BY
                // CONSTRUCTION — a missing type would be a compile error, not a silent skip.
                Guard.Try(Sys, "section2-transitiontriggers", () =>
                {
                    var comps = Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsInactive.Include);
                    FlowTrace.Step(Sys, $"SceneTransitionTrigger count={comps.Length}");
                    foreach (var comp in comps)
                    {
                        if (comp == null) continue;
                        AddTarget(seamTargets, $"STT:{comp.gameObject.name}", comp.transform.position);
                    }
                });

                FlowTrace.Step(Sys, $"SEAM_TARGETS collected={seamTargets.Count}");
            });

            // -------- SECTION 3: CONNECTIVITY hero → each seam -------------------
            int reaches = 0;
            var completeList = new List<string>();
            var brokenList = new List<string>();

            Guard.Try(Sys, "section3-connectivity", () =>
            {
                if (!heroOnMesh)
                {
                    FlowTrace.Fail(Sys, "CONNECTIVITY_SKIPPED: hero is off-mesh, cannot compute paths");
                    return;
                }

                foreach (var st in seamTargets)
                {
                    if (!st.OnMesh)
                    {
                        brokenList.Add($"{st.Name}(target-off-mesh)");
                        FlowTrace.Fail(Sys, $"PATH hero→{st.Name}: TARGET_OFF_MESH, cannot path");
                        continue;
                    }

                    var path = new NavMeshPath();
                    bool ok = NavMesh.CalculatePath(heroSampledPos, st.SampledPos, NavMesh.AllAreas, path);
                    int corners = path.corners != null ? path.corners.Length : 0;
                    float lastToTarget = corners > 0
                        ? Vector3.Distance(path.corners[corners - 1], st.SampledPos)
                        : -1f;

                    string verdict = path.status.ToString(); // PathComplete / PathPartial / PathInvalid
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        reaches++;
                        completeList.Add(st.Name);
                        FlowTrace.Step(Sys, $"PATH hero→{st.Name}: {verdict} (calc={ok}) corners={corners} lastCornerToTarget={lastToTarget:F2}m");
                    }
                    else
                    {
                        brokenList.Add($"{st.Name}({verdict})");
                        FlowTrace.Fail(Sys, $"PATH hero→{st.Name}: {verdict} (calc={ok}) corners={corners} lastCornerToTarget={lastToTarget:F2}m — DISCONNECTED/ISLAND for this gate");
                    }
                }
            });

            // -------- SECTION 4: ISLAND CHECK — courtyard reachability ring ------
            float reachableRadius = -1f;

            Guard.Try(Sys, "section4-island", () =>
            {
                if (!heroOnMesh)
                {
                    FlowTrace.Fail(Sys, "ISLAND_CHECK_SKIPPED: hero off-mesh");
                    return;
                }

                Vector3 center = Vector3.zero; // courtyard center per spec
                float[] radii = { 10f, 20f, 30f };
                int[] bearings = { 0, 45, 90, 135, 180, 225, 270, 315 };

                foreach (float r in radii)
                {
                    int completeAtR = 0;
                    int sampledAtR = 0;
                    foreach (int deg in bearings)
                    {
                        float rad = deg * Mathf.Deg2Rad;
                        Vector3 p = center + new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
                        if (!NavMesh.SamplePosition(p, out NavMeshHit rh, SampleRadius, NavMesh.AllAreas))
                            continue; // no navmesh at all here
                        sampledAtR++;
                        var rp = new NavMeshPath();
                        NavMesh.CalculatePath(heroSampledPos, rh.position, NavMesh.AllAreas, rp);
                        if (rp.status == NavMeshPathStatus.PathComplete)
                        {
                            completeAtR++;
                            reachableRadius = Mathf.Max(reachableRadius, r);
                        }
                    }
                    FlowTrace.Step(Sys, $"ISLAND_RING r={r:F0}m: onMesh={sampledAtR}/{bearings.Length} reachable(Complete)={completeAtR}/{bearings.Length}");
                }
            });

            // -------- SECTION 5: one-line verdict --------------------------------
            Guard.Try(Sys, "section5-verdict", () =>
            {
                string completeStr = completeList.Count > 0 ? string.Join(",", completeList) : "none";
                string brokenStr = brokenList.Count > 0 ? string.Join(",", brokenList) : "none";
                FlowTrace.Step(Sys,
                    $"CASTLE_NAV_TOPOLOGY: hero reaches {reaches}/{seamTargets.Count} seams | " +
                    $"PathComplete=[{completeStr}] | broken=[{brokenStr}] | " +
                    $"courtyard reachable radius ~{(reachableRadius < 0 ? 0 : reachableRadius):F0}m | " +
                    $"heroOnMesh={heroOnMesh}");

                if (reaches == 0 && seamTargets.Count > 0)
                    FlowTrace.Fail(Sys, "VERDICT: hero reaches ZERO seams — courtyard navmesh is a DISCONNECTED ISLAND from the gate seams (edit-time false-green confirmed).");
                else if (reaches < seamTargets.Count)
                    FlowTrace.Warn(Sys, "VERDICT: partial — some seams reachable, some islanded. Connectivity is gate-specific.");
                else
                    FlowTrace.Step(Sys, "VERDICT: all seam targets reachable from hero — navmesh is CONNECTED (look elsewhere for the SEAM-UNREACHABLE report).");
            });

            FlowTrace.Step(Sys, "=== CASTLE NAV TOPOLOGY CAPTURE END (self-disabling) ===");

            // self-disable: unsubscribe + destroy so it never spams again this session
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Destroy(gameObject);
        }

        // ---- helpers -------------------------------------------------------------

        private struct SeamTarget
        {
            public string Name;
            public Vector3 SampledPos;
            public bool OnMesh;
        }

        private static void AddNamedTarget(List<SeamTarget> list, string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                FlowTrace.Warn(Sys, $"SEAM_TARGET '{name}' NOT FOUND in scene");
                return;
            }
            AddTarget(list, name, go.transform.position);
        }

        private static void AddTarget(List<SeamTarget> list, string name, Vector3 pos)
        {
            bool onMesh = NavMesh.SamplePosition(pos, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas);
            Vector3 sampled = onMesh ? hit.position : pos;
            if (onMesh)
            {
                float snap = Vector3.Distance(pos, hit.position);
                FlowTrace.Step(Sys, $"SEAM_TARGET '{name}' worldPos={Fmt(pos)} ON_MESH sampled={Fmt(sampled)} snapDist={snap:F2}m");
            }
            else
            {
                FlowTrace.Fail(Sys, $"SEAM_TARGET '{name}' worldPos={Fmt(pos)} OFF_MESH (no navmesh within {SampleRadius}m)");
            }
            list.Add(new SeamTarget { Name = name, SampledPos = sampled, OnMesh = onMesh });
        }

        private static int MaskToAreaIndex(int mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) return i;
            return -1;
        }

        private static string Fmt(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
    }
}
