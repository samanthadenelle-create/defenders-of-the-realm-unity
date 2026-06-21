// =============================================================================
// Village2LayoutDump — batchmode-runnable, READ-ONLY capture of the owner's
// hand-authored Village2 layout to JSON, so it becomes code-reproducible.
//
// WHY: Village2Playable's "Capture Selected -> Recipe" needs in-editor SELECTION,
// which a headless/batchmode run can't do. The owner hand-authored a big Village2
// redo and saved the scene; this dumps EVERY transform under the scene roots
// (name, hierarchy path, prefab source, local + world TRS, has-renderer) to a JSON
// the builder/recipe can be generated from. OPENS the scene, reads, writes JSON —
// NEVER saves/modifies the scene (no resave-corruption risk, §3).
//
// Run: DeNelle.Editor.Village2LayoutDump.Dump  (via run-unity-method.ps1, editor closed)
// =============================================================================
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class Village2LayoutDump
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";
        private const string OutPath   = "Assets/_Village2/village2-layout-dump.json";

        [MenuItem("Defenders/Village2/Dump Layout -> JSON (read-only)")]
        public static void Dump()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[Village2LayoutDump] opened '{ScenePath}', root objects: {scene.rootCount}");

            var sb = new StringBuilder();
            sb.Append("{\"scene\":\"Village2\",\"objects\":[");
            int count = 0;

            foreach (var root in scene.GetRootGameObjects())
                count = DumpRecursive(root.transform, "", sb, count);

            sb.Append("]}");

            System.IO.Directory.CreateDirectory("Assets/_Village2");
            System.IO.File.WriteAllText(OutPath, sb.ToString());
            AssetDatabase.ImportAsset(OutPath);
            Debug.Log($"[Village2LayoutDump] DONE — dumped {count} transform(s) -> {OutPath}");
            // Explicitly DO NOT save the scene (read-only contract).
        }

        private static int DumpRecursive(Transform t, string parentPath, StringBuilder sb, int count)
        {
            if (t == null) return count;
            string path = string.IsNullOrEmpty(parentPath) ? t.name : parentPath + "/" + t.name;

            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) as GameObject;
            string prefabPath = src != null ? AssetDatabase.GetAssetPath(src) : "";
            bool hasRenderer = t.GetComponent<Renderer>() != null;

            Vector3 lp = t.localPosition, le = t.localEulerAngles, ls = t.localScale;
            Vector3 wp = t.position, we = t.eulerAngles;

            if (count > 0) sb.Append(",");
            sb.Append("{")
              .AppendFormat("\"path\":\"{0}\",", Escape(path))
              .AppendFormat("\"name\":\"{0}\",", Escape(t.name))
              .AppendFormat("\"prefab\":\"{0}\",", Escape(prefabPath))
              .AppendFormat("\"hasRenderer\":{0},", hasRenderer ? "true" : "false")
              .AppendFormat("\"localPos\":[{0:0.###},{1:0.###},{2:0.###}],", lp.x, lp.y, lp.z)
              .AppendFormat("\"localRot\":[{0:0.###},{1:0.###},{2:0.###}],", le.x, le.y, le.z)
              .AppendFormat("\"localScale\":[{0:0.###},{1:0.###},{2:0.###}],", ls.x, ls.y, ls.z)
              .AppendFormat("\"worldPos\":[{0:0.###},{1:0.###},{2:0.###}],", wp.x, wp.y, wp.z)
              .AppendFormat("\"worldRot\":[{0:0.###},{1:0.###},{2:0.###}]", we.x, we.y, we.z)
              .Append("}");
            count++;

            for (int i = 0; i < t.childCount; i++)
                count = DumpRecursive(t.GetChild(i), path, sb, count);
            return count;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
