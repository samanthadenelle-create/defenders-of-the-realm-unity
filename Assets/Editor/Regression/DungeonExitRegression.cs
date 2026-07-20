// =============================================================================
// DungeonExitRegression [dungeon-exit] -- proves a composed dungeon can be LEFT.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Dungeons + Unity.AI.Navigation).
// A composed dungeon (DungeonCompose_*) gets its exit at RUNTIME from
// DungeonExitSpawner ([RuntimeInitializeOnLoadMethod] -> sceneLoaded -> TryInject),
// so a pure editmode scene open has no exit yet. This oracle therefore proves the
// exit-affordance PATH rather than a baked object:
//   * DungeonExitInteractable.Spawn(pos) builds a real, interactable exit (a trigger
//     collider that routes home) -- the affordance itself works, AND
//   * the runtime injector DungeonExitSpawner (Install + TryInject) exists so a loaded
//     composed scene actually receives one, AND
//   * the three exit-affordance types resolve (DungeonStubReturn / SceneTransitionTrigger
//     / DungeonExitInteractable).
// NavMesh reachability is a NOTE (headless -nographics bakes no walkable area -- the
// RoomForge navmesh case documents the same).
//
// Marker: DUNGEON_EXIT_OK / DUNGEON_EXIT_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!DungeonExitRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-exit] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Editor
{
    public static class DungeonExitRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DUNGEON EXIT (affordance builds + runtime injector present) ---");

            // (a) The three exit-affordance types resolve.
            foreach (var full in new[] { "DeNelle.Dungeons.DungeonStubReturn",
                                         "DeNelle.Village.SceneTransitionTrigger",
                                         "DeNelle.Dungeons.DungeonExitInteractable" })
            {
                if (FindType(full) == null)
                    failures.Add($"[dungeon-exit] exit-affordance type '{full}' not found (removed/renamed)");
                else log.AppendLine($"  type OK: {full}");
            }

            // (b) DungeonExitInteractable.Spawn builds a real, interactable exit.
            GameObject spawned = null;
            try
            {
                var exit = DeNelle.Dungeons.DungeonExitInteractable.Spawn(Vector3.zero);
                if (exit == null)
                {
                    failures.Add("[dungeon-exit] DungeonExitInteractable.Spawn returned null -- no exit affordance can be seated");
                }
                else
                {
                    spawned = exit.gameObject;
                    var trigger = exit.GetComponent<SphereCollider>();
                    bool hasTrigger = trigger != null && trigger.isTrigger;
                    log.AppendLine($"  Spawn built '{exit.name}', trigger collider={hasTrigger}");
                    if (!hasTrigger)
                        failures.Add("[dungeon-exit] the spawned exit has no trigger SphereCollider -- the player cannot activate it");

                    // NavMesh reachability -- best-effort NOTE (no bake in headless editmode).
                    if (NavMesh.SamplePosition(exit.transform.position, out _, 4f, NavMesh.AllAreas))
                        log.AppendLine("  exit position is on a NavMesh");
                    else
                        notes.Add("exit not on a baked NavMesh in editmode (runtime bakes it; RoomForge navmesh case documents the same)");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[dungeon-exit] DungeonExitInteractable.Spawn threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (spawned != null) UnityEngine.Object.DestroyImmediate(spawned);
            }

            // (c) The runtime injector exists (so a loaded composed scene receives an exit).
            var spawnerT = FindType("DeNelle.Dungeons.DungeonExitSpawner");
            if (spawnerT == null)
            {
                failures.Add("[dungeon-exit] DungeonExitSpawner not found -- composed scenes would never auto-receive an exit");
            }
            else
            {
                var install = spawnerT.GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var inject = spawnerT.GetMethod("TryInject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                bool hasRuntimeHook = install != null &&
                    install.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0;
                log.AppendLine($"  injector: Install(runtime-hook={hasRuntimeHook}) TryInject={(inject != null)}");
                if (inject == null)
                    failures.Add("[dungeon-exit] DungeonExitSpawner.TryInject not found -- the composed-scene exit injection path is gone");
                if (!hasRuntimeHook)
                    notes.Add("DungeonExitSpawner.Install lacks a resolvable [RuntimeInitializeOnLoadMethod] (or is named differently)");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "DUNGEON_EXIT_OK");
                reason = "DUNGEON EXIT OK -- the exit affordance builds with a trigger, the runtime injector is present, and all affordance types resolve" + noteStr;
                return true;
            }
            reason = "dungeon-exit: " + string.Join("; ", failures) + noteStr;
            Debug.LogError(log.ToString() + "DUNGEON_EXIT_FAIL: " + reason);
            return false;
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
