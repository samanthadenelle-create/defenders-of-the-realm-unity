#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Village;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.DevTools
{
    /// <summary>
    /// Argument-gated headed proof for the four continuous castle openings.
    /// Asserts TWO independent things per gate, because they can diverge:
    ///   1. CONTINUOUS WALKING - a clamped agent.Move() displacement carries the hero
    ///      from the inner seat past the outside radius. This proves the navmesh SURFACE
    ///      is walkable through the opening.
    ///   2. COMPLETE PATHING - NavMesh.CalculatePath() returns PathComplete in BOTH
    ///      directions between the outer seat and the inner seat. This proves the navmesh
    ///      GRAPH has a real edge through the wall, which is what the retired gate
    ///      NavMeshLink used to supply. Two disjoint surfaces that merely abut will pass
    ///      (1) and fail (2), and every pathfinding agent (enemies, troops) would then
    ///      silently stop routing into town while the hero still walks through fine.
    /// </summary>
    public sealed class GateTraversalProof : MonoBehaviour
    {
        private const float StartRadius = 34f;
        private const float OutsideRadius = 46f;
        private const float PathOuterRadius = 52f;
        private const float MovePulse = 0.55f;
        private const int MaxMoves = 40;
        private const string Sys = "GateProof";
        private string _output;
        private readonly List<string> _rows = new();
        private readonly List<string> _pathFailures = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string output = Arg("-gateProofDir");
            if (string.IsNullOrWhiteSpace(output)) return;
            var go = new GameObject("[GateTraversalProof]");
            DontDestroyOnLoad(go);
            var proof = go.AddComponent<GateTraversalProof>();
            proof._output = output;
            proof.StartCoroutine(proof.Run());
        }

        private IEnumerator Run()
        {
            FlowTrace.Step(Sys, "proof armed; waiting for hero/agent on Main_Castle_Overworld");
            float deadline = Time.realtimeSinceStartup + 30f;
            HeroLocomotion hero = null;
            NavMeshAgent agent = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                hero = FindFirstObjectByType<HeroLocomotion>();
                if (hero != null) agent = hero.GetComponent<NavMeshAgent>();
                if (hero != null && agent != null && agent.enabled && agent.isOnNavMesh &&
                    SceneManager.GetActiveScene().name == "Main_Castle_Overworld") break;
                yield return null;
            }

            Directory.CreateDirectory(_output);
            // panels_open_forced_clear / time_scale_forced_1x are NOT findings - the proof
            // itself forces both every sample frame (CloseAll + timeScale=1). They are logged
            // as declared harness state so nobody reads them as an observation.
            _rows.Add("side,move,elapsed_seconds,x,y,z,outward_progress,gate_links,path_in,path_out,panels_open_forced_clear,time_scale_forced_1x,image");
            if (hero == null || agent == null || !agent.isOnNavMesh)
            {
                FlowTrace.Fail(Sys, "hero/agent never became ready on Main_Castle_Overworld");
                Finish(false, "hero/agent never became ready on Main_Castle_Overworld");
                yield break;
            }

            // Isolate locomotion geometry from the native first-run CDN barrier. The proof
            // build may run in a network-restricted shell with no warmed content; that
            // overlay is intentionally persistent in production and is not PanelManager-owned.
            // Remove it only under this explicit proof argument. NOTE: because the proof
            // forces this state, the resulting CSV columns are harness declarations, not
            // evidence - see the header comment above.
            var loading = FindObjectsByType<LoadingOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var overlay in loading) if (overlay != null) Destroy(overlay.gameObject);
            yield return null;

            int gateLinks = CountGateLinks();
            bool passed = gateLinks == 0;
            string failure = gateLinks == 0 ? null : $"found {gateLinks} gate NavMeshLink(s)";
            if (gateLinks != 0) FlowTrace.Fail(Sys, failure);
            var routes = new (string side, Vector3 outward)[]
            {
                ("north", Vector3.forward), ("south", Vector3.back),
                ("east", Vector3.right), ("west", Vector3.left),
            };

            foreach (var route in routes)
            {
                PanelManager.CloseAll();
                Time.timeScale = 1f;
                Vector3 requested = route.outward * StartRadius;
                if (!NavMesh.SamplePosition(requested, out NavMeshHit seat, 8f, NavMesh.AllAreas))
                {
                    passed = false; failure = $"{route.side}: no inner navmesh seat";
                    FlowTrace.Fail(Sys, failure);
                    break;
                }

                // --- PATHFINDING ASSERTION (graph edge, not just surface) -------------
                string pathIn = "unsampled-outer-seat";
                string pathOut = "unsampled-outer-seat";
                if (!NavMesh.SamplePosition(route.outward * PathOuterRadius, out NavMeshHit outerSeat, 12f, NavMesh.AllAreas))
                {
                    string why = $"{route.side}: no outer navmesh seat at r={PathOuterRadius:F0} - cannot path-test this gate";
                    _pathFailures.Add(why);
                    FlowTrace.Fail(Sys, why);
                }
                else
                {
                    pathIn = EvaluatePath(route.side, "in", outerSeat.position, seat.position);
                    pathOut = EvaluatePath(route.side, "out", seat.position, outerSeat.position);
                }
                // ---------------------------------------------------------------------

                hero.WarpTo(seat.position, Quaternion.LookRotation(route.outward));
                yield return new WaitForSecondsRealtime(0.5f);

                float began = Time.realtimeSinceStartup;
                PanelManager.CloseAll();
                yield return Capture(route.side, 0, began, hero.transform.position, route.outward, gateLinks, pathIn, pathOut);
                bool outside = false;
                for (int move = 1; move <= MaxMoves; move++)
                {
                    PanelManager.CloseAll();
                    agent.Move(route.outward * MovePulse);
                    yield return new WaitForSecondsRealtime(0.12f);
                    Vector3 pos = hero.transform.position;
                    yield return Capture(route.side, move, began, pos, route.outward, gateLinks, pathIn, pathOut);
                    if (Vector3.Dot(pos, route.outward) >= OutsideRadius)
                    {
                        outside = true;
                        Debug.Log($"GATE_TRAVERSAL_EXIT_OK side={route.side} moves={move} elapsed={Time.realtimeSinceStartup - began:F3} pos={pos}");
                        FlowTrace.Step(Sys, $"exit ok side={route.side} moves={move} pathIn={pathIn} pathOut={pathOut}");
                        break;
                    }
                }
                if (!outside)
                {
                    passed = false;
                    failure = $"{route.side}: failed to reach outside radius after {MaxMoves} physical moves";
                    FlowTrace.Fail(Sys, failure);
                    break;
                }
            }

            if (_pathFailures.Count > 0)
            {
                passed = false;
                string pathText = "incomplete gate pathing -> " + string.Join(" | ", _pathFailures);
                failure = string.IsNullOrEmpty(failure) ? pathText : failure + " ; " + pathText;
            }
            Finish(passed, failure);
        }

        /// <summary>
        /// Requires NavMesh.CalculatePath(from,to) to return PathComplete. Anything else
        /// (PathPartial, PathInvalid, or a call that returns false) is recorded as a named
        /// failure for this gate side + direction and fails the whole proof.
        /// </summary>
        private string EvaluatePath(string side, string direction, Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            bool called = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);
            string status = called ? path.status.ToString() : "NoPath(CalculatePath returned false)";
            bool complete = called && path.status == NavMeshPathStatus.PathComplete;
            if (complete)
            {
                FlowTrace.Step(Sys, $"path {side}/{direction} PathComplete corners={path.corners.Length}");
            }
            else
            {
                string why = $"{side} path {direction} ({from} -> {to}) status={status}";
                _pathFailures.Add(why);
                FlowTrace.Fail(Sys, "GATE_PATH_FAIL " + why);
                Debug.LogWarning($"GATE_TRAVERSAL_PATH_FAIL side={side} dir={direction} status={status} corners={path.corners.Length}");
            }
            Debug.Log($"GATE_TRAVERSAL_PATH side={side} dir={direction} status={status} corners={path.corners.Length}");
            return status;
        }

        private IEnumerator Capture(string side, int move, float began, Vector3 pos, Vector3 outward, int links,
            string pathIn, string pathOut)
        {
            string image = $"{side}_{move:00}.png";
            float elapsed = Time.realtimeSinceStartup - began;
            float progress = Vector3.Dot(pos, outward);
            // The last two columns are forced by this harness on every sample frame; they are
            // recorded as declarations ("forced-*"), never as observations.
            _rows.Add(string.Join(",", side, move.ToString(CultureInfo.InvariantCulture),
                elapsed.ToString("F3", CultureInfo.InvariantCulture), pos.x.ToString("F3", CultureInfo.InvariantCulture),
                pos.y.ToString("F3", CultureInfo.InvariantCulture), pos.z.ToString("F3", CultureInfo.InvariantCulture),
                progress.ToString("F3", CultureInfo.InvariantCulture), links.ToString(CultureInfo.InvariantCulture),
                pathIn, pathOut,
                "forced-clear", "forced-1x", image));
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, image));
            Debug.Log($"GATE_TRAVERSAL_SAMPLE side={side} move={move} elapsed={elapsed:F3} pos={pos} progress={progress:F3} links={links} pathIn={pathIn} pathOut={pathOut} panels=forced-clear timeScale=forced-1x");
            yield return new WaitForEndOfFrame();
        }

        private static int CountGateLinks()
        {
            int count = 0;
            var links = FindObjectsByType<NavMeshLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var link in links)
                if (link != null && (link.name.StartsWith("GatePassage_", StringComparison.OrdinalIgnoreCase) ||
                    link.name.IndexOf("castle", StringComparison.OrdinalIgnoreCase) >= 0)) count++;
            return count;
        }

        private void Finish(bool passed, string failure)
        {
            File.WriteAllLines(Path.Combine(_output, "gate-traversal.csv"), _rows, Encoding.UTF8);
            File.WriteAllText(Path.Combine(_output, passed ? "PASS.txt" : "FAIL.txt"),
                passed ? "GATE_TRAVERSAL_PROOF_OK 4/4 continuous exits; 8/8 PathComplete gate routes (in+out per gate); zero gate links/warps."
                       : "GATE_TRAVERSAL_PROOF_FAIL: " + failure);
            Debug.Log(passed
                ? "GATE_TRAVERSAL_PROOF_OK 4/4 continuous exits; 8/8 PathComplete gate routes"
                : "GATE_TRAVERSAL_PROOF_FAIL: " + failure);
            if (passed) FlowTrace.Step(Sys, "proof passed: walking continuous AND pathing complete on all four gates");
            else FlowTrace.Fail(Sys, "proof failed: " + failure);
            Application.Quit(passed ? 0 : 12);
        }

        private static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
#endif
