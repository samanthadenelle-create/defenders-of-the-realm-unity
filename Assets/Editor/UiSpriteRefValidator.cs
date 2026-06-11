// =============================================================================
// UiSpriteRefValidator — READ-ONLY scan for uGUI Image components with a missing
// (null) sprite reference, across all prefabs + all build scenes. WO-409
// acceptance gate so no UI ships with a blank/missing sprite.
// -----------------------------------------------------------------------------
// A uGUI Image with sprite == null draws as a flat box (or nothing) where art
// should be. This reports every such Image (asset path, GameObject path, whether
// it has a Button, current color) so the RPG-UI work can satisfy each one. It
// also flags Images whose sprite asset is missing/broken (overrideSprite logic
// is left to runtime — we only validate the serialized sprite ref).
//
// NOTE: an Image deliberately used as a solid-color panel may legitimately have a
// null sprite. Those still get reported (with HasButton/Color columns) so a human
// can triage; this validator does not auto-suppress them.
//
// Writes CSV to Builds/MagentaScan/. Does NOT modify any asset.
//
// Run (batchmode):  -executeMethod DeNelle.Editor.UiSpriteRefValidator.Run
// Menu:             Defenders/Art/Validate UI Sprite Refs
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeNelle.Editor
{
    public static class UiSpriteRefValidator
    {
        private const string OutDir = "Builds/MagentaScan";

        [MenuItem("Defenders/Art/Validate UI Sprite Refs")]
        public static void Run()
        {
            var rows = new List<string>();
            rows.Add("Source,AssetPath,GameObjectPath,ImageName,HasButton,Color,Reason");

            int prefab = ScanPrefabs(rows);
            int scenes = ScanScenes(rows);

            Directory.CreateDirectory(OutDir);
            string outPath = Path.Combine(OutDir, "ui_sprite_refs.csv");
            File.WriteAllText(outPath, string.Join("\n", rows), new UTF8Encoding(false));

            int total = prefab + scenes;
            Debug.Log($"[UiSpriteRefValidator] DONE — {total} Image(s) with a missing sprite ref " +
                      $"({prefab} in prefabs, {scenes} in scenes). Report: {outPath}");
        }

        private static int ScanPrefabs(List<string> rows)
        {
            int hits = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                    hits += Inspect("Prefab", path, img, rows);
            }
            return hits;
        }

        private static int ScanScenes(List<string> rows)
        {
            int hits = 0;
            string activeBefore = SceneManager.GetActiveScene().path;

            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (entry == null || !entry.enabled) continue;
                string scenePath = entry.path;
                if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) continue;

                Scene scene;
                try { scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[UiSpriteRefValidator] could not open scene {scenePath}: {e.Message}");
                    continue;
                }

                foreach (var go in scene.GetRootGameObjects())
                    foreach (var img in go.GetComponentsInChildren<Image>(true))
                        hits += Inspect("Scene:" + scenePath, scenePath, img, rows);
            }

            if (!string.IsNullOrEmpty(activeBefore) && File.Exists(activeBefore))
            {
                try { EditorSceneManager.OpenScene(activeBefore, OpenSceneMode.Single); }
                catch { /* best-effort restore */ }
            }
            return hits;
        }

        private static int Inspect(string source, string assetPath, Image img, List<string> rows)
        {
            if (img == null) return 0;
            if (img.sprite != null) return 0; // has a sprite → fine

            bool hasButton = img.GetComponent<Button>() != null;
            string color = $"RGBA({img.color.r:0.##},{img.color.g:0.##},{img.color.b:0.##},{img.color.a:0.##})";
            rows.Add($"{Csv(source)},{Csv(assetPath)},{Csv(GoPath(img.transform))},{Csv(img.name)}," +
                     $"{hasButton},{Csv(color)},MISSING_SPRITE");
            return 1;
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
