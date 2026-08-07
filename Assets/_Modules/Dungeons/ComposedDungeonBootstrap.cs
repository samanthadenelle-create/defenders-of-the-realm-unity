// =============================================================================
// ComposedDungeonBootstrap — runtime pillars for Pipeline A (composed) dungeons.
// -----------------------------------------------------------------------------
// WO-1001 slices 3–5 foundation. Cottage has DungeonController; composed scenes
// only had hero + spawners + exit. This arms:
//   • a live DungeonRuntimeState run (so other systems can key off RunActive)
//   • Lantern on the Player (oil drain + oil-stone refill)
// Mirrors DungeonExitSpawner: sceneLoaded hook, idempotent, no re-bake required
// for already-baked scenes that carry ComposedOilStone markers.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Auto-installs composed-dungeon runtime pillars on every DungeonCompose_* scene.
    /// </summary>
    internal static class ComposedDungeonBootstrap
    {
        private const string Sys = "ComposedDungeon";
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryArm(SceneManager.GetActiveScene());
            FlowTrace.Step(Sys, "installed sceneLoaded hook (lantern + run state for composed dungeons)");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryArm(scene);

        private static void TryArm(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots;
            try { roots = scene.GetRootGameObjects(); }
            catch (Exception ex)
            {
                FlowTrace.Warn(Sys, $"GetRootGameObjects failed for '{scene.name}': {ex.Message}");
                return;
            }

            Transform composeRoot = null;
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go != null && go.name.StartsWith("DungeonCompose_", StringComparison.Ordinal))
                {
                    composeRoot = go.transform;
                    break;
                }
            }
            if (composeRoot == null) return;

            // Idempotent — don't double-arm if we already attached a host.
            if (composeRoot.Find("ComposedDungeonHost") != null)
            {
                FlowTrace.Step(Sys, $"already armed on '{scene.name}' — skip");
                return;
            }

            Guard.Try(Sys, $"arm composed pillars on '{scene.name}'", () => Arm(scene, composeRoot));
        }

        private static void Arm(Scene scene, Transform composeRoot)
        {
            // Step IN / step OUT of the whole arm: an ENTER with no matching EXIT is the signature
            // of a throw or an early return partway through, which is otherwise indistinguishable
            // from "the dungeon just has no pillars".
            using var _scope = FlowTrace.Enter(Sys, $"arm '{scene.name}'");

            var host = new GameObject("ComposedDungeonHost");
            host.transform.SetParent(composeRoot, false);

            // Runtime-only SO — not an asset on disk (cottage uses a shared inspector asset).
            var state = ScriptableObject.CreateInstance<DungeonRuntimeState>();
            var heroGo = GameObject.FindGameObjectWithTag("Player");
            Vector3 heroPos = heroGo != null ? heroGo.transform.position : Vector3.zero;
            string dungeonId = scene.name.StartsWith("dg_", StringComparison.Ordinal)
                ? scene.name
                : scene.name.Replace("DungeonCompose_", "");
            state.StartRun(dungeonId, "entry", heroPos, Environment.TickCount);
            FlowTrace.Step(Sys, $"StartRun id='{dungeonId}' hero={heroPos} scene='{scene.name}'");

            if (heroGo == null)
            {
                FlowTrace.Warn(Sys, "no Player-tagged hero — lantern not armed");
                return;
            }

            // Collect baked oil stones (planar refill, same contract as cottage).
            var markers = composeRoot.GetComponentsInChildren<ComposedOilStone>(true);
            var stones = new List<DungeonOilStone>(markers.Length);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                Vector3 p = m.transform.position;
                stones.Add(new DungeonOilStone
                {
                    id = m.Id,
                    roomId = "",
                    position = new DungeonPoint { x = p.x, y = p.y, z = p.z },
                    radius = m.Radius,
                });
            }

            // Ensure a lantern light follows the Keeper.
            var lantern = heroGo.GetComponentInChildren<Lantern>(true);
            if (lantern == null)
            {
                var lightGo = new GameObject("Lantern");
                lightGo.transform.SetParent(heroGo.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                lightGo.AddComponent<Light>();
                lantern = lightGo.AddComponent<Lantern>();
                FlowTrace.Step(Sys, "created Lantern under Player (composed bake had none)");
            }

            lantern.ConfigureStandalone(stones, heroGo.transform);
            FlowTrace.Step(Sys,
                $"lantern armed standalone: stones={stones.Count} hero='{heroGo.name}' " +
                $"(WO-1001 slice 5 oil drain active)");

            // WO-1001 slice 6: darkness ambush director (higher odds when oil critical).
            ComposedKeyBag.Clear();
            var ambush = host.AddComponent<ComposedAmbushDirector>();
            ambush.Configure(lantern, heroGo.transform, state, tier: 1);
            FlowTrace.Step(Sys, "ComposedAmbushDirector armed (slice 6 darkness ambush)");

            // WO-1001 1b/7: count what the bake actually left in the scene. These are the pillars
            // whose bake-time Configure used to be discarded by SaveScene, so a zero here on a
            // dungeon that authored them is the exact signature of that class of defect returning.
            int ports = composeRoot.GetComponentsInChildren<DungeonPortLink>(true).Length;
            int locks = composeRoot.GetComponentsInChildren<ComposedLockedPort>(true).Length;
            int keys = composeRoot.GetComponentsInChildren<ComposedKeyPickup>(true).Length;
            int traps = composeRoot.GetComponentsInChildren<ComposedTrapHazard>(true).Length;
            FlowTrace.Step(Sys,
                $"pillars present in '{scene.name}': stairPorts={ports} lockedPorts={locks} " +
                $"keys={keys} traps={traps} oilStones={stones.Count}");
        }
    }
}
