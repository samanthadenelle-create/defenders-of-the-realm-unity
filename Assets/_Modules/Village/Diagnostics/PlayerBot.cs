// =============================================================================
// PlayerBot — autonomous synthetic playtester (DEV / editor only, OFF by default).
//
// Drives the hero through a scripted route of OBJECTIVES, each with a success
// predicate + timeout, and logs a Debug.LogError on any failure — which the
// always-on BreakCaptureHarness captures into break-log.jsonl exactly like an F8
// flag. So the bot finds + records bugs without a human at the keyboard.
//
// It is BLUEPRINT-AWARE: the route is built from the castle's key coordinates
// (spawn -> gate -> exit-to-overworld), the same spatial truth the rest of the
// systems should consume. The exit objective is the one that catches the current
// "reach gate, nothing happens" bug: it asserts the overworld transition actually
// fires within a timeout.
//
// ENABLE: set PlayerPrefs "dev.playerbot" = 1 (or PlayerBot.Enabled = true before
// scene load). OFF by default — it never runs in a normal player session.
//
// Drives the hero by taking over its NavMeshAgent (suspends HeroLocomotion for the
// run, restores it after) and SetDestination-ing through the route — so it tests
// real navmesh connectivity + the proximity seam end-to-end.
// =============================================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    public class PlayerBot : MonoBehaviour
    {
        public static bool Enabled = false;
        private const string EnableKey = "dev.playerbot";

        // Spawn the bot in gameplay scenes when enabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Enabled && PlayerPrefs.GetInt(EnableKey, 0) != 1) return;
            string scene = SceneManager.GetActiveScene().name;
            // Only the hub gameplay scenes (don't run in menus).
            // WO-608: include the merged single-scene home hub (ff.MergedWorld). Safe when
            // OFF — that scene never loads on the legacy path.
            if (scene != "MainCastle_Hall" && scene != "Village2" && scene != "Main_Castle_Overworld") return;
            if (FindAnyObjectByType<PlayerBot>() != null) return;
            var go = new GameObject("[PlayerBot]");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerBot>();
            Debug.Log("[PlayerBot] enabled — will drive a scripted route in " + scene);
        }

        private struct Objective
        {
            public string Label;
            public Vector3 Target;     // world point to drive toward
            public float ReachDist;    // success if within this of target
            public float Timeout;      // seconds before FAIL
            public System.Func<bool> Done; // optional override success check
        }

        private GameObject _hero;
        private NavMeshAgent _agent;
        private Behaviour _loco; // HeroLocomotion, suspended during bot control

        private IEnumerator Start()
        {
            // Let the scene + hero spawn settle.
            yield return new WaitForSeconds(2.0f);

            _hero = GameObject.FindWithTag("Player");
            if (_hero == null) { Fail("no Player-tagged hero found — cannot run"); yield break; }
            _agent = _hero.GetComponent<NavMeshAgent>();
            if (_agent == null) { Fail("hero has no NavMeshAgent — cannot drive"); yield break; }
            _loco = _hero.GetComponent<HeroLocomotion>();

            // Take control: suspend player locomotion, sane bot speed.
            if (_loco != null) _loco.enabled = false;
            float prevSpeed = _agent.speed;
            _agent.speed = 6f;
            _agent.autoBraking = true;

            Debug.Log("[PlayerBot] === route start (scene=" + SceneManager.GetActiveScene().name + ") ===");

            foreach (var obj in BuildRoute())
                yield return RunObjective(obj);

            // Restore.
            _agent.speed = prevSpeed;
            if (_loco != null) _loco.enabled = true;
            Debug.Log("[PlayerBot] === route complete ===");
        }

        // Castle route (blueprint coords). Generalise per-scene as the spatial contract lands.
        private IEnumerable<Objective> BuildRoute()
        {
            // 1. Settle on navmesh near spawn.
            yield return new Objective { Label = "spawn-on-mesh", Target = new Vector3(0, 0, -2f), ReachDist = 6f, Timeout = 8f };

            // 2. Walk to just inside the south gate opening.
            yield return new Objective { Label = "approach-south-gate", Target = new Vector3(-4.37f, 0f, -38f), ReachDist = 4f, Timeout = 20f };

            // 3. EXIT: push to the seam point; success = we actually leave to the overworld.
            //    This is the objective that catches the current exit bug.
            string startScene = SceneManager.GetActiveScene().name;
            yield return new Objective
            {
                Label = "exit-to-outerworld",
                Target = new Vector3(-4.37f, 0f, -46f),
                ReachDist = 2f,
                Timeout = 20f,
                Done = () =>
                    SceneManager.GetActiveScene().name != startScene ||           // active scene changed
                    DeNelle.Core.HubScenes.IsOverworld(SceneManager.GetSceneByName("Main_Castle_Overworld").name) && _hero.transform.position.z < -60f // warped south
            };
        }

        private IEnumerator RunObjective(Objective o)
        {
            Debug.Log($"[PlayerBot] -> {o.Label}  target={o.Target}");
            if (_agent.isOnNavMesh) _agent.SetDestination(o.Target);

            float t = 0f;
            while (t < o.Timeout)
            {
                if (o.Done != null && o.Done()) { Debug.Log($"[PlayerBot] OK  {o.Label} (event)"); yield break; }
                if (_hero != null && Vector3.Distance(_hero.transform.position, o.Target) <= o.ReachDist && o.Done == null)
                { Debug.Log($"[PlayerBot] OK  {o.Label} (reached, {t:F1}s)"); yield break; }

                // Re-issue destination if the agent lost its path (seam crossings disable it).
                if (_agent != null && _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance < 0.25f && o.Done == null)
                    _agent.SetDestination(o.Target);

                t += Time.deltaTime;
                yield return null;
            }

            float dist = _hero != null ? Vector3.Distance(_hero.transform.position, o.Target) : -1f;
            Fail($"objective '{o.Label}' TIMED OUT after {o.Timeout}s (hero at {_hero?.transform.position}, {dist:F1}m from target). " +
                 (o.Label == "exit-to-outerworld"
                     ? "Reached the gate but never transitioned to the overworld — the exit seam did not carry the hero across."
                     : "Could not reach the waypoint — likely a navmesh gap or blocker."));
        }

        // Debug.LogError so the always-on BreakCaptureHarness records it into break-log.jsonl.
        private void Fail(string why) => Debug.LogError("[PlayerBot] FAIL :: " + why);
    }
}
#endif
