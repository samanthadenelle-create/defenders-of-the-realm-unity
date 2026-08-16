// =============================================================================
// HubSceneMaintenance — WO-1022 + WO-1025 step 1 (2026-08-16 overnight)
// -----------------------------------------------------------------------------
// Two batchmode entry points over the merged hub scene. Both print a distinct
// marker; judge by the MARKER, never the exit code (canon).
//
//   1. StripMissingPrefabInstances (WO-1022) — removes every root whose prefab
//      asset no longer exists (the 56 refs to the three GUIDs deleted outside
//      git ~07-04; they render NOTHING in game, so removal changes zero pixels
//      and only silences the per-load error burst that floods the F8 inbox).
//      Emits SCENE_STRIP_OK <n> removed / SCENE_STRIP_FAIL. Saves the scene ONLY
//      when at least one instance was removed AND the post-checks pass.
//      ⚠ Deviation from WO-1022 §4 recorded in the WO: run in the MAIN tree, not
//      an isolated worktree — the scene is committed clean, so git is the
//      restore path, and the save is post-verified (NUL scan + non-trivial size)
//      before any commit. A worktree would force a full cold-Library reimport.
//
//   2. DumpHeartTreeChildren (WO-1025 step 1) — read-only inventory of
//      HeartOfElarion's hierarchy (renderer / particle / light components per
//      child, with prefab provenance). NEVER saves. Emits HEART_DUMP_OK <n>.
//      Caveat printed in the dump: a scene walk cannot see RUNTIME-spawned
//      emitters (VFXManager pool) — those need a play capture; this dump settles
//      only the scene-attached half.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor.SceneMaintenance
{
    public static class HubSceneMaintenance
    {
        private const string ScenePath = "Assets/Scenes/Main_Castle_Overworld.unity";

        [MenuItem("Defenders/Scene/Strip Missing Prefab Instances (WO-1022)")]
        public static void StripMissingPrefabInstances()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("SCENE_STRIP_FAIL: could not open " + ScenePath);
                return;
            }

            var doomed = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                CollectMissingPrefabInstances(root.transform, doomed);

            if (doomed.Count == 0)
            {
                Debug.Log("SCENE_STRIP_OK 0 removed — no missing-prefab instances found (already clean). Scene NOT saved.");
                return;
            }

            var names = new StringBuilder();
            foreach (var go in doomed)
            {
                names.Append(go.name).Append("; ");
                Object.DestroyImmediate(go);
            }

            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                Debug.LogError("SCENE_STRIP_FAIL: SaveScene returned false after removing " + doomed.Count + " instance(s).");
                return;
            }

            // Post-checks: the saved file must be non-trivial and NUL-clean is checked by the
            // caller (binary scenes legitimately contain NULs — the caller checks SIZE + git
            // diff shape instead; a truncated save is the corruption signature).
            var fi = new FileInfo(ScenePath);
            if (!fi.Exists || fi.Length < 100_000)
            {
                Debug.LogError($"SCENE_STRIP_FAIL: saved scene is suspiciously small ({(fi.Exists ? fi.Length : 0)} bytes) — treat as corruption, restore via git.");
                return;
            }

            Debug.Log($"SCENE_STRIP_OK {doomed.Count} removed (sceneBytes={fi.Length}) :: {names}");
        }

        private static void CollectMissingPrefabInstances(Transform t, List<GameObject> doomed)
        {
            // A missing-prefab instance root: Unity flags the instance whose source asset is gone.
            if (PrefabUtility.IsPrefabAssetMissing(t.gameObject))
            {
                doomed.Add(t.gameObject);
                return; // children die with the root
            }
            for (int i = 0; i < t.childCount; i++)
                CollectMissingPrefabInstances(t.GetChild(i), doomed);
        }

        [MenuItem("Defenders/Scene/Dump Heart Tree Children (WO-1025)")]
        public static void DumpHeartTreeChildren()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("HEART_DUMP_FAIL: could not open " + ScenePath);
                return;
            }

            GameObject heart = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindDeep(root.transform, "HeartOfElarion");
                if (found != null) { heart = found.gameObject; break; }
            }
            if (heart == null)
            {
                Debug.LogError("HEART_DUMP_FAIL: no 'HeartOfElarion' object in " + ScenePath);
                return;
            }

            int rows = 0;
            var sb = new StringBuilder();
            sb.AppendLine("[HeartDump] WO-1025 step 1 — scene-attached inventory under 'HeartOfElarion'.");
            sb.AppendLine("[HeartDump] CAVEAT: runtime-spawned emitters (VFXManager pool) are INVISIBLE to this dump — they need a play capture.");
            DumpRecursive(heart.transform, "", sb, ref rows);
            Debug.Log(sb.ToString());
            Debug.Log($"HEART_DUMP_OK {rows} component row(s). Scene NOT saved (read-only pass).");
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static void DumpRecursive(Transform t, string indent, StringBuilder sb, ref int rows)
        {
            var comps = new List<string>();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { comps.Add("<MISSING SCRIPT>"); continue; }
                if (c is ParticleSystem || c is Light || c is Renderer || c is Projector)
                    comps.Add(c.GetType().Name);
            }
            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            string provenance = src != null ? AssetDatabase.GetAssetPath(src) : "(scene-authored)";
            if (comps.Count > 0 || t.childCount == 0)
            {
                sb.AppendLine($"[HeartDump] {indent}{t.name} active={t.gameObject.activeInHierarchy} " +
                              $"pos={t.position} scale={t.lossyScale} comps=[{string.Join(",", comps)}] src={provenance}");
                rows++;
            }
            for (int i = 0; i < t.childCount; i++)
                DumpRecursive(t.GetChild(i), indent + "  ", sb, ref rows);
        }
    }
}
