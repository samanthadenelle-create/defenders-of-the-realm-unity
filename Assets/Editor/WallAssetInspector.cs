// Editor tool: import + inspect the owner's wall-tier FBX (tri count, bounds, materials)
// so the CLI can judge poly budget (tiled on a grid) + whether a URP/material fix is needed.
// Batchmode: DeNelle.Editor.WallAssetInspector.Inspect
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WallAssetInspector
    {
        private static readonly string[] Paths =
        {
            "Assets/Resources/Walls/wood_wall.fbx",
            "Assets/Resources/Walls/iron_wall.fbx",
            "Assets/Resources/Walls/steel_wall.fbx",   // may not exist yet
        };

        [MenuItem("Defenders/Walls/Inspect Wall FBX")]
        public static void Inspect()
        {
            var sb = new StringBuilder("[WallInspect]\n");
            foreach (var path in Paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { sb.AppendLine($"  MISSING: {path}"); continue; }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                int tris = 0; var mats = new System.Collections.Generic.HashSet<string>();
                Bounds b = default; bool boundsInit = false;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (m != null) tris += (int)(m.triangles.Length / 3);
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!boundsInit) { b = r.bounds; boundsInit = true; } else b.Encapsulate(r.bounds);
                    foreach (var mat in r.sharedMaterials)
                        mats.Add(mat == null ? "<null>" : (mat.shader == null ? mat.name + "<noShader>" : mat.shader.name));
                }
                sb.AppendLine($"  {System.IO.Path.GetFileName(path)}: tris={tris}, size={b.size}, mats=[{string.Join(", ", mats)}]");
            }
            Debug.Log(sb.ToString());
        }
    }
}
