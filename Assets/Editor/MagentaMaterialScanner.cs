// =============================================================================
// MagentaMaterialScanner — READ-ONLY scan for "magenta" renderers across the
// whole project (all prefabs + all build scenes). WO-409 Bug 1 diagnosis.
// -----------------------------------------------------------------------------
// Unity renders a renderer MAGENTA when its material cannot draw. The three
// causes we detect here:
//   1. sharedMaterial == null                         (no material assigned)
//   2. material.shader == null                         (material lost its shader)
//   3. shader is Hidden/InternalErrorShader OR its
//      name contains "InternalError" / "Hidden/InternalError"
//      (built-in/Standard shader not valid under URP, stripped, or asset missing)
//
// Writes a CSV of every offender to Builds/MagentaScan/ so the fixer + a human
// can see exactly which asset / GameObject / renderer / shader is at fault.
// Does NOT modify any asset.
//
// Run (batchmode):  -executeMethod DeNelle.Editor.MagentaMaterialScanner.Run
// Menu:             Defenders/Art/Scan Magenta Materials
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class MagentaMaterialScanner
    {
        private const string OutDir = "Builds/MagentaScan";

        [MenuItem("Defenders/Art/Scan Magenta Materials")]
        public static void Run()
        {
            var rows = new List<string>();
            // header
            rows.Add("Source,AssetPath,GameObjectPath,RendererType,RendererName,MaterialSlot,Reason,ShaderName,MaterialName");

            int prefabOffenders = ScanPrefabs(rows);
            int sceneOffenders = ScanScenes(rows);

            Directory.CreateDirectory(OutDir);
            string outPath = Path.Combine(OutDir, "magenta_scan.csv");
            File.WriteAllText(outPath, string.Join("\n", rows), new UTF8Encoding(false));

            int total = prefabOffenders + sceneOffenders;
            Debug.Log($"[MagentaMaterialScanner] DONE — {total} offending material slot(s) " +
                      $"({prefabOffenders} in prefabs, {sceneOffenders} in scenes). Report: {outPath}");
        }

        // ---- prefab pass ----------------------------------------------------
        private static int ScanPrefabs(List<string> rows)
        {
            int offenders = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                var renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                    offenders += InspectRenderer("Prefab", path, r, rows);
            }
            return offenders;
        }

        // ---- scene pass -----------------------------------------------------
        private static int ScanScenes(List<string> rows)
        {
            int offenders = 0;
            string activeBefore = SceneManager.GetActiveScene().path;

            foreach (var sceneEntry in EditorBuildSettings.scenes)
            {
                if (sceneEntry == null || !sceneEntry.enabled) continue;
                string scenePath = sceneEntry.path;
                if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) continue;

                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MagentaMaterialScanner] could not open scene {scenePath}: {e.Message}");
                    continue;
                }

                foreach (var go in scene.GetRootGameObjects())
                {
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                        offenders += InspectRenderer("Scene:" + scenePath, scenePath, r, rows);
                }
            }

            // restore a benign empty scene so the editor isn't left on the last build scene
            if (!string.IsNullOrEmpty(activeBefore) && File.Exists(activeBefore))
            {
                try { EditorSceneManager.OpenScene(activeBefore, OpenSceneMode.Single); }
                catch { /* best-effort restore */ }
            }
            return offenders;
        }

        // ---- per-renderer classification ------------------------------------
        private static int InspectRenderer(string source, string assetPath, Renderer r, List<string> rows)
        {
            if (r == null) return 0;
            // skip particle/line/trail renderers without shared materials handled the same way;
            // they still go magenta on a bad shader, so include them.
            var mats = r.sharedMaterials;
            int offenders = 0;

            if (mats == null || mats.Length == 0)
            {
                rows.Add(Row(source, assetPath, GoPath(r.transform), r, 0, "NO_MATERIAL_SLOTS", "", ""));
                return 1;
            }

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                string reason = ClassifyMaterial(m, out string shaderName, out string matName);
                if (reason != null)
                {
                    rows.Add(Row(source, assetPath, GoPath(r.transform), r, i, reason, shaderName, matName));
                    offenders++;
                }
            }
            return offenders;
        }

        /// <summary>Returns a non-null reason string when the material slot will render magenta; null when fine.</summary>
        public static string ClassifyMaterial(Material m, out string shaderName, out string matName)
        {
            shaderName = "";
            matName = m != null ? m.name : "";
            if (m == null)
                return "NULL_MATERIAL";

            var sh = m.shader;
            if (sh == null)
                return "NULL_SHADER";

            shaderName = sh.name;
            if (shaderName.Contains("InternalError") ||
                shaderName.Contains("Hidden/InternalError") ||
                sh == Shader.Find("Hidden/InternalErrorShader"))
                return "INTERNAL_ERROR_SHADER";

            return null;
        }

        private static string Row(string source, string assetPath, string goPath, Renderer r,
                                  int slot, string reason, string shaderName, string matName)
        {
            string rType = r != null ? r.GetType().Name : "Renderer";
            string rName = r != null ? r.name : "";
            return $"{Csv(source)},{Csv(assetPath)},{Csv(goPath)},{Csv(rType)},{Csv(rName)}," +
                   $"{slot},{Csv(reason)},{Csv(shaderName)},{Csv(matName)}";
        }

        private static string GoPath(Transform t)
        {
            if (t == null) return "";
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
