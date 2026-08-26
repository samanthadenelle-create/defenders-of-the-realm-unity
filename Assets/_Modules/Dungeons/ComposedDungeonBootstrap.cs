// =============================================================================
// ComposedDungeonBootstrap — runtime pillars for Pipeline A (composed) dungeons.
// -----------------------------------------------------------------------------
// WO-1001 slices 3–5 foundation. Cottage has DungeonController; composed scenes
// only had hero + spawners + exit. This arms:
//   • a live DungeonRuntimeState run (so other systems can key off RunActive)
//   • Lantern on the Player (oil drain + oil-stone refill)
// Mirrors DungeonExitSpawner: sceneLoaded hook, idempotent, no re-bake required
// for already-baked scenes that carry ComposedOilStone markers.
//
// WO-1112 — THE SPLIT: this file is now the INSTALLER only. Everything with a
// lifetime (the run state, the lantern, the oil HUD, the ambush director) moved to
// ComposedDungeonHost, the MonoBehaviour it attaches. Two reasons, both defects that
// were shipping: (1) the DungeonRuntimeState lived in a LOCAL VARIABLE here and fell
// out of scope, so the composed exit had no run to pay out and every composed run
// scored 0 on the rough-stone economy; (2) the hero pillars were armed on the load
// frame, which is now ambiguous — SceneRouter.GoDungeonScene carries the town hero
// in and the baked one is destroyed at end of frame, so both answer the Player tag.
// =============================================================================

using System;
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

            var hostGo = new GameObject("ComposedDungeonHost");
            hostGo.transform.SetParent(composeRoot, false);

            // Runtime-only SO — not an asset on disk (cottage uses a shared inspector asset).
            var state = ScriptableObject.CreateInstance<DungeonRuntimeState>();
            var heroGo = GameObject.FindGameObjectWithTag("Player");
            Vector3 heroPos = heroGo != null ? heroGo.transform.position : Vector3.zero;
            string dungeonId = scene.name.StartsWith("dg_", StringComparison.Ordinal)
                ? scene.name
                : scene.name.Replace("DungeonCompose_", "");
            state.StartRun(dungeonId, "entry", heroPos, Environment.TickCount);
            FlowTrace.Step(Sys, $"StartRun id='{dungeonId}' hero={heroPos} scene='{scene.name}'");

            // WO-1222 — THE LOAD-FRAME POSE, CAPTURED BEFORE ANYTHING ELSE RUNS. This is the
            // first of two readings (the second is DungeonHeroSeat.VerifyArrival one frame later
            // from ComposedDungeonHost); together they say whether a wrong arrival pose was
            // ALREADY wrong when the scene loaded, or was written in the frame after. Without the
            // pair, a hero standing 7km away is a single number with no history. NOTE the hero
            // read here can be the DOOMED duplicate (Destroy is deferred to end of frame), which
            // is precisely why the authoritative check waits a frame — see the host's header.
            FlowTrace.Step(Sys,
                $"LOAD-FRAME hero pose: {heroPos} tagged='{(heroGo != null ? heroGo.name : "<no Player>")}' " +
                $"heroScene='{(heroGo != null ? heroGo.scene.name : "n/a")}' " +
                $"inBattleArena={DeNelle.Village.Arena.BattleArena.IsArenaPosition(heroPos)} " +
                $"battleInProgress={DeNelle.Village.Arena.BattleArena.AnyBattleInProgress} " +
                $"seatBaked={(DungeonHeroSeat.FindBakedSeat(scene).HasValue ? "yes" : "NO")}");

            // WO-1112: the run state is HANDED TO A LIVE OWNER instead of dying with this local
            // scope. That is what makes a composed exit payable — DungeonExitInteractable reads
            // ComposedDungeonHost.Current.RunState to judge the run and grant the rough stone.
            // The host also arms the lantern/HUD/ambush ONE FRAME LATE on purpose; see its header
            // (the carried hero and the baked hero both answer the Player tag on the load frame).
            var host = hostGo.AddComponent<ComposedDungeonHost>();
            host.Install(composeRoot, state);
            FlowTrace.Step(Sys, "ComposedDungeonHost installed (owns the run state; hero pillars arm next frame)");
        }
    }
}
