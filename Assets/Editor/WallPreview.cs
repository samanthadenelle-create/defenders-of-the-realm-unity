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
        // Owner spec: normalize each segment to an exact 1.5w x 3.0h x 1.5d box (one grid cell).
        private static readonly Vector3 TargetSize = new Vector3(1.5f, 3.0f, 1.5f);
        private const int RunLength = 3;   // tile N segments per tier so it reads as a WALL, not a block

        // (resourcePath, label, row z) — each tier is a contiguous run at its own z.
        private static readonly (string res, string name, float z)[] Tiers =
        {
            ("Walls/wood_wall",  "Wood",   2.5f),
            ("Walls/iron_wall",  "Iron",   5.5f),
            ("Walls/steel_wall", "Steel",  8.5f),
        };

        [MenuItem("Defenders/Walls/Place Wall Preview Row In Castle")]
        public static void PlaceInCastle()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var prior = GameObject.Find(RowName);
            if (prior != null) Object.DestroyImmediate(prior);
            var row = new GameObject(RowName);
            row.transform.position = Vector3.zero;   // courtyard, between spawn and the Heart

            foreach (var (res, name, z) in Tiers)
            {
                var prefab = Resources.Load<GameObject>(res);
                if (prefab == null) { Debug.LogWarning($"[WallPreview] missing Resources/{res}"); continue; }

                for (int i = 0; i < RunLength; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (go == null) go = Object.Instantiate(prefab);
                    go.name = $"Wall_{name}_{i}";
                    go.transform.SetParent(row.transform, false);
                    float x = (i - (RunLength - 1) / 2f) * TargetSize.x;   // contiguous, centred run
                    go.transform.localPosition = new Vector3(x, 0f, z);
                    go.transform.localRotation = Quaternion.identity;

                    // Normalize to the exact 1.5 x 3.0 x 1.5 box (per-axis from world bounds).
                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds;
                        for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                        var s = go.transform.localScale;
                        if (b.size.x > 0.0001f) s.x *= TargetSize.x / b.size.x;
                        if (b.size.y > 0.0001f) s.y *= TargetSize.y / b.size.y;
                        if (b.size.z > 0.0001f) s.z *= TargetSize.z / b.size.z;
                        go.transform.localScale = s;
                        // Ground-seat (re-measure after scaling).
                        var r2 = go.GetComponentsInChildren<Renderer>(true);
                        var b2 = r2[0].bounds;
                        for (int k = 1; k < r2.Length; k++) b2.Encapsulate(r2[k].bounds);
                        go.transform.position += new Vector3(0f, -b2.min.y, 0f);
                    }

                    if (name == "Steel")
                    {
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                            foreach (var m in r.sharedMaterials)
                                if (m != null && m.HasProperty("_EmissionColor"))
                                {
                                    m.EnableKeyword("_EMISSION");
                                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                                }
                    }
                }
                Debug.Log($"[WallPreview] placed {RunLength}x {name} run (1.5x3x1.5 each) at z={z}.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WallPreview] saved — demo row in MainCastle_Hall courtyard (~0,0,3).");
        }
    }
}
