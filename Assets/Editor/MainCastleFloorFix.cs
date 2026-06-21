// =============================================================================
// MainCastleFloorFix — the REAL pink-floor fix, found by FloorDeepDiag (owner:
// "we need deeper root testing on it").
//
// PROVEN ROOT (Builds/floor-deep2.log): the courtyard floor TILES are warm
// (0.42,0.34,0.24) and warm-lit — NOT pink. The big 130x130 'CourtyardFloor_Nav'
// floor is renderer-DISABLED ("Invisible_Walkable", nav-only), and no terrain sits
// near the castle. So beyond the small central plaza there is NO visible floor, and
// the camera's SolidColor background RGBA(0.74,0.66,0.72) = pink-mauve fills the
// view = the "pink floor". A material repaint could never fix a missing floor.
//
// FIX: (1) make the big warm floor VISIBLE (enable its renderer + warm URP/Lit),
// nudged 5cm below the plaza tiles to avoid coplanar z-fight; (2) neutralize the
// pink camera background to an overcast neutral. Result: a real warm floor every-
// where + a non-pink horizon. READ/WRITE: opens, edits, SAVES MainCastle_Hall.
//
// Run: DeNelle.Editor.MainCastleFloorFix.Run  (run-unity-method, EDITOR CLOSED)
// =============================================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class MainCastleFloorFix
    {
        private const string ScenePath = "Assets/Scenes/MainCastle_Hall.unity";

        [MenuItem("Defenders/Castle/Fix Pink Floor (visible floor + neutral bg)")]
        public static void Run()
        {
            Log("=== MainCastle pink-floor REAL fix START ===");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1) Make the big invisible nav floor VISIBLE + warm, dropped below the plaza tiles.
            int enabled = 0;
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (mr == null) continue;
                string n = mr.name;
                if (n != "CourtyardFloor_Nav") continue;
                mr.enabled = true;
                var mat = new Material(lit) { name = "CastleFloorBig" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.24f, 1f));
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                mr.sharedMaterial = mat;
                // drop 5cm below the plaza tiles (y=-0.5) so the detailed plaza renders on top, big floor fills the rest
                var t = mr.transform;
                var p = t.position; p.y = -0.55f; t.position = p;
                enabled++;
                Log($"Enabled big floor '{n}' -> visible warm, y={t.position.y}.");
            }
            if (enabled == 0) Warn("CourtyardFloor_Nav not found — big floor not enabled (name may differ).");

            // 2) Neutralize the pink-mauve camera background.
            int cams = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam == null) continue;
                if (cam.clearFlags == CameraClearFlags.SolidColor)
                {
                    cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f); // neutral overcast slate (NOT pink)
                    cams++;
                }
            }
            Log($"Neutralized {cams} SolidColor camera background(s) (was pink-mauve 0.74,0.66,0.72).");

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Log($"Saved '{ScenePath}' (ok={saved}). bigFloorEnabled={enabled}, camsFixed={cams}.");
            Log("=== MainCastle pink-floor REAL fix DONE — open MainCastle_Hall + Play; the ground should be warm floor, not pink void ===");
        }

        private static void Log(string m)  => Debug.Log("[MainCastleFloorFix] " + m);
        private static void Warn(string m) => Debug.LogWarning("[MainCastleFloorFix] " + m);
    }
}
