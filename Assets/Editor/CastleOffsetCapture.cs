// =============================================================================
// CastleOffsetCapture — captures the owner's hand-authored CastleSide_South
// layout to a JSON recipe so the castle becomes CODE-REPRODUCIBLE.
//
// THE REAL SOLUTION (owner, 2026-06-12): instead of a fragile hand-dialed scene
// that a regen would revert, capture the owner's actual offsets into data, so the
// builder can DELETE + COMPLETELY RECREATE the castle reproducing the owner's
// layout. This tool reads each child under CastleSide_South (prefab + local TRS)
// + the parent's world transform, and writes castle-south-recipe.json. The
// recreate builder (CastleSideFromRecipe) then rebuilds south from the recipe and
// mirrors x4.
//
// READ-ONLY on the scene: opens it, reads transforms, writes a JSON asset. Does
// NOT modify or save the scene.
// =============================================================================
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CastleOffsetCapture
    {
        private const string ScenePath  = "Assets/Scenes/MainCastle_Hall.unity";
        private const string RecipePath = "Assets/Resources/Data/castle-south-recipe.json";

        [MenuItem("Defenders/Castle/Capture South Side -> Recipe")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var south = GameObject.Find("CastleSide_South");
            if (south == null)
            {
                Debug.LogError("[CastleOffsetCapture] 'CastleSide_South' not found in " + ScenePath);
                return;
            }

            var sb = new StringBuilder();
            sb.Append("{\"pieces\":[");
            int count = 0;
            foreach (Transform child in south.transform)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) as GameObject;
                string prefab = src != null ? src.name : child.name;
                Vector3 p = child.localPosition, e = child.localEulerAngles, s = child.localScale;
                if (count > 0) sb.Append(",");
                sb.Append("{")
                  .AppendFormat("\"name\":\"{0}\",", child.name)
                  .AppendFormat("\"prefab\":\"{0}\",", prefab)
                  .AppendFormat("\"pos\":[{0:0.###},{1:0.###},{2:0.###}],", p.x, p.y, p.z)
                  .AppendFormat("\"rot\":[{0:0.###},{1:0.###},{2:0.###}],", e.x, e.y, e.z)
                  .AppendFormat("\"scale\":[{0:0.###},{1:0.###},{2:0.###}]", s.x, s.y, s.z)
                  .Append("}");
                count++;
            }
            Vector3 pp = south.transform.position, pe = south.transform.eulerAngles;
            sb.Append("],")
              .AppendFormat("\"parentPos\":[{0:0.###},{1:0.###},{2:0.###}],", pp.x, pp.y, pp.z)
              .AppendFormat("\"parentRot\":[{0:0.###},{1:0.###},{2:0.###}]", pe.x, pe.y, pe.z)
              .Append("}");

            System.IO.Directory.CreateDirectory("Assets/Resources/Data");
            System.IO.File.WriteAllText(RecipePath, sb.ToString());
            AssetDatabase.ImportAsset(RecipePath);
            Debug.Log("[CastleOffsetCapture] captured " + count + " south-side pieces -> " + RecipePath + "\n" + sb);
        }
    }
}
