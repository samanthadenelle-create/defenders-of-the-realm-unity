// =============================================================================
// SceneCleanupTools — owner-facing Editor utilities for the village scene.
// -----------------------------------------------------------------------------
// One file, several menu items under Defenders/Scene Cleanup/. Designed for
// the recovery-machine workflow after the 2026-05-24 fresh clone: most of the
// village's prefabs landed via GUID-remap so the scene looks correct but the
// hierarchy is messy and some art still renders magenta because legacy
// Built-In materials haven't been converted yet.
//
// What's in here:
//   1. "Convert All Materials to URP"
//      — Calls the same menu item the renderer settings expose, programmatic
//        so it can be re-run anytime new art comes in. Fixes the bulk magenta.
//
//   2. "Report Scene Stats"
//      — Prints scene-wide counts: OrchardTree instances, hex_grass tiles,
//        wall pieces, missing-prefab anchors. Run this first before touching
//        anything; tells you what's there.
//
//   3. "Group Trees Under Environment/Trees"
//      — Creates an Environment GameObject (and a Trees child) if missing,
//        reparents every OrchardTree under it. Pure organization, no
//        deletion, doesn't move world-space positions (worldPositionStays).
//
//   4. "Deduplicate OrchardTrees (proximity < 1.5m)"
//      — Greedy nearest-neighbour cull: any OrchardTree within 1.5m of an
//        already-kept tree gets destroyed. Undo is supported (one Cmd+Z
//        rolls back the whole pass).
//
// All operations are Undo-recorded so a Cmd+Z (Ctrl+Z) on the Scene window
// rolls them back. Operations only touch the ACTIVE scene's contents — they
// do not modify project-level prefab assets.
// =============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class SceneCleanupTools
    {
        private const float DedupeRadius = 1.5f;
        private const string EnvRootName = "Environment";
        private const string TreesParentName = "Trees";

        // ── 1. Material conversion ────────────────────────────────────────────

        [MenuItem("Defenders/Scene Cleanup/Convert All Materials to URP")]
        public static void ConvertMaterialsToUrp()
        {
            // Unity 6 renamed this from "Convert All Project Materials to URP"
            // to "Convert All Built-In Materials to Current SRP". Try both —
            // ExecuteMenuItem returns silently if the menu doesn't exist.
            bool ran = EditorApplication.ExecuteMenuItem(
                "Edit/Rendering/Materials/Convert All Built-In Materials to Current SRP");
            if (!ran)
            {
                ran = EditorApplication.ExecuteMenuItem(
                    "Edit/Rendering/Materials/Convert All Project Materials to URP");
            }
            if (!ran)
            {
                Debug.LogWarning("[SceneCleanupTools] Couldn't find the URP material-conversion menu item. " +
                                 "Run it manually: Edit → Rendering → Materials → Convert All Built-In Materials to Current SRP.");
                return;
            }
            Debug.Log("[SceneCleanupTools] Triggered URP material conversion. Confirm any dialog Unity raises; conversion may take 30s+.");
        }

        // ── 2. Scene stats ────────────────────────────────────────────────────

        [MenuItem("Defenders/Scene Cleanup/Report Scene Stats")]
        public static void ReportSceneStats()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                Debug.LogWarning("[SceneCleanupTools] No active scene.");
                return;
            }

            var rootObjects = active.GetRootGameObjects();

            int total = 0;
            var nameCounts = new Dictionary<string, int>();
            foreach (var root in rootObjects)
                CountRecursive(root.transform, nameCounts, ref total);

            // High-signal names we want explicit numbers on.
            string[] watchedNames = {
                "OrchardTree", "hex_grass", "hex_road_A", "hex_road_B",
                "wall_straight", "wall_corner_A_outside", "wall_straight_gate",
                "DistantMountainPeak", "haybale", "HeroBody",
                "Protagonist_A", "Protagonist_B", "Helper_A", "Helper_B",
            };

            Debug.Log("=== Scene Stats — " + active.name + " ===");
            Debug.Log("Total GameObjects in scene: " + total);
            Debug.Log("Top-level root objects:    " + rootObjects.Length);
            Debug.Log("--- Watched names ---");
            foreach (var n in watchedNames)
            {
                int c = nameCounts.TryGetValue(n, out var v) ? v : 0;
                Debug.Log("  " + n.PadRight(30) + " " + c);
            }

            // Also report any name that appears more than 50 times — likely a
            // dense decorator we may want to dedupe or move to a terrain tool.
            Debug.Log("--- Names with >50 instances ---");
            var dense = new List<KeyValuePair<string, int>>();
            foreach (var kv in nameCounts)
                if (kv.Value > 50) dense.Add(kv);
            dense.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in dense) Debug.Log("  " + kv.Key.PadRight(40) + " " + kv.Value);
        }

        private static void CountRecursive(Transform t, Dictionary<string, int> bag, ref int total)
        {
            total++;
            if (bag.TryGetValue(t.name, out var v)) bag[t.name] = v + 1;
            else bag[t.name] = 1;
            for (int i = 0; i < t.childCount; i++)
                CountRecursive(t.GetChild(i), bag, ref total);
        }

        // ── 3. Group trees under Environment/Trees ────────────────────────────

        [MenuItem("Defenders/Scene Cleanup/Group Trees Under Environment-Trees")]
        public static void GroupTreesUnderEnvironment()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid()) { Debug.LogWarning("No active scene."); return; }

            Transform treesParent = EnsureEnvironmentChild(TreesParentName);

            int moved = 0;
            foreach (var go in FindAllByName("OrchardTree", active))
            {
                if (go == null) continue;
                if (go.transform.parent == treesParent) continue;
                // worldPositionStays=true so the tree visually stays put;
                // only its hierarchy parent changes.
                Undo.SetTransformParent(go.transform, treesParent, "Group OrchardTrees");
                moved++;
            }
            Debug.Log("[SceneCleanupTools] Moved " + moved + " OrchardTree(s) under " +
                      EnvRootName + "/" + TreesParentName + ".");
            EditorSceneManager.MarkSceneDirty(active);
        }

        private static Transform EnsureEnvironmentChild(string childName)
        {
            // Top-level Environment GameObject
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            GameObject env = null;
            foreach (var r in roots) if (r.name == EnvRootName) { env = r; break; }
            if (env == null)
            {
                env = new GameObject(EnvRootName);
                Undo.RegisterCreatedObjectUndo(env, "Create Environment root");
            }

            // Direct child
            Transform child = env.transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + childName + " group");
                Undo.SetTransformParent(go.transform, env.transform, "Parent " + childName);
                child = go.transform;
            }
            return child;
        }

        // ── 4. Deduplicate OrchardTrees by proximity ──────────────────────────

        [MenuItem("Defenders/Scene Cleanup/Deduplicate OrchardTrees (proximity)")]
        public static void DeduplicateOrchardTrees()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid()) { Debug.LogWarning("No active scene."); return; }

            var trees = FindAllByName("OrchardTree", active);
            if (trees.Count == 0)
            {
                Debug.Log("[SceneCleanupTools] No OrchardTree instances found.");
                return;
            }

            int removed = 0;
            var kept = new List<Vector3>(trees.Count);
            float r2 = DedupeRadius * DedupeRadius;

            // Sort by position so the cull is deterministic across runs.
            trees.Sort((a, b) =>
            {
                if (a == null || b == null) return 0;
                Vector3 pa = a.transform.position, pb = b.transform.position;
                int cx = pa.x.CompareTo(pb.x); if (cx != 0) return cx;
                int cz = pa.z.CompareTo(pb.z); if (cz != 0) return cz;
                return pa.y.CompareTo(pb.y);
            });

            foreach (var go in trees)
            {
                if (go == null) continue;
                Vector3 p = go.transform.position;
                bool tooClose = false;
                for (int i = 0; i < kept.Count; i++)
                {
                    if ((kept[i] - p).sqrMagnitude < r2) { tooClose = true; break; }
                }
                if (tooClose)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
                else
                {
                    kept.Add(p);
                }
            }

            Debug.Log("[SceneCleanupTools] OrchardTree dedupe: kept " + kept.Count +
                      ", removed " + removed + " duplicate(s) within " +
                      DedupeRadius + "m.");
            EditorSceneManager.MarkSceneDirty(active);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<GameObject> FindAllByName(string name, Scene scene)
        {
            var found = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                CollectByName(root.transform, name, found);
            return found;
        }

        private static void CollectByName(Transform t, string name, List<GameObject> output)
        {
            if (t.name == name) output.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++)
                CollectByName(t.GetChild(i), name, output);
        }
    }
}
#endif
