// Editor tool: drop the 3 wall-tier meshes (wood/iron/steel) as a visible demo row in
// MainCastle_Hall so the owner can judge the Wood->Iron->Reinforced-Steel look in-game.
// PREVIEW ONLY — a castle rebake will wipe this row (it's not in BuildCastleHub).
// Batchmode: DeNelle.Editor.WallPreview.PlaceInCastle
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WallPreview
    {
        private const string ScenePath = "Assets/Scenes/MainCastle_Hall.unity";
        private const string RowName = "WallPreview_Row";
        private const float TargetHeight = 1.5f;   // normalize each segment to ~1.5m tall

        // (resourcePath, label, world x in the row)
        private static readonly (string res, string name, float x)[] Tiers =
        {
            ("Walls/wood_wall",  "WallPreview_Wood",  -2.5f),
            ("Walls/iron_wall",  "WallPreview_Iron",   0f),
            ("Walls/steel_wall", "WallPreview_Steel",  2.5f),
        };

        [MenuItem("Defenders/Walls/Place Wall Preview Row In Castle")]
        public static void PlaceInCastle()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var prior = GameObject.Find(RowName);
            if (prior != null) Object.DestroyImmediate(prior);
            var row = new GameObject(RowName);
            row.transform.position = new Vector3(0f, 0f, 3f);   // courtyard, in front of spawn/Heart

            foreach (var (res, name, x) in Tiers)
            {
                var prefab = Resources.Load<GameObject>(res);
                if (prefab == null) { Debug.LogWarning($"[WallPreview] missing Resources/{res}"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (go == null) go = Object.Instantiate(prefab);
                go.name = name;
                go.transform.SetParent(row.transform, false);
                go.transform.localPosition = new Vector3(x, 0f, 0f);
                go.transform.localRotation = Quaternion.identity;

                // Normalize to ~1.5m tall from renderer bounds (export scales vary).
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    if (b.size.y > 0.0001f) go.transform.localScale *= (TargetHeight / b.size.y);
                    // Ground-seat: drop so the bottom rests on y=0 (re-measure after scaling).
                    var rends2 = go.GetComponentsInChildren<Renderer>(true);
                    var b2 = rends2[0].bounds;
                    for (int i = 1; i < rends2.Length; i++) b2.Encapsulate(rends2[i].bounds);
                    float worldBottom = b2.min.y;
                    go.transform.position += new Vector3(0f, -worldBottom, 0f);
                }

                // Steel runes: enable emission so the blue glow reads.
                if (name.Contains("Steel"))
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && m.HasProperty("_EmissionColor"))
                            {
                                m.EnableKeyword("_EMISSION");
                                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                            }
                }
                Debug.Log($"[WallPreview] placed {name} (scaled to ~{TargetHeight}m, ground-seated).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WallPreview] saved — demo row in MainCastle_Hall courtyard (~0,0,3).");
        }
    }
}
