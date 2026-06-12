// =============================================================================
// CastleWallKitSpawner — drops a FRESH south-side wall kit for the owner to
// hand-arrange, then mirror x4 via CastleSideMirror.
//
// WHY: the committed MainCastle_Hall has a half-finished hand-edited south wall
// (W/N/E perimeter was deleted). Rather than re-run the DESTRUCTIVE
// CastleHubBuilder (which wipes CastleHubRoot), the owner authors ONE clean
// south side and mirrors it. This tool spawns the four kit pieces — gate +
// 2 wall segments + 1 corner tower — as prefab instances under a single
// "CastleSide_South" parent, pre-placed SYMMETRIC about x=0 (the gate axis) so
// the mirror lands correctly on all four faces (the mirror rotates the group
// rigidly around world origin; an asymmetric south source is the only thing that
// makes a mirrored side look wrong).
//
// Pieces match CastleHubBuilder exactly (same polyperfect prefabs -> GUIDs
// resolve, no magenta): Wall_Medieval_Stone, Gate_Medieval_Medium,
// Tower_Castle_Round.
//
// SAFE: pure additive prefab instances under Undo; writes no scene by itself
// (owner saves when happy). Editor-only. Never runs CastleHubBuilder.
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CastleWallKitSpawner
    {
        private const string PolyRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/";
        private const string SouthName = "CastleSide_South";

        // South edge at z=-44 (matches CastleHubBuilder's perimeter); symmetric about x=0.
        private const float SouthZ = -44f;

        [MenuItem("Defenders/Castle/Spawn Fresh Wall Kit (South side)")]
        public static void SpawnKit()
        {
            if (GameObject.Find(SouthName) != null &&
                !EditorUtility.DisplayDialog("Spawn Wall Kit",
                    $"'{SouthName}' already exists.\n\nDelete the old one first for a clean redo, or spawn another anyway?",
                    "Spawn anyway", "Cancel"))
                return;

            var wall  = LoadPoly("Wall_Medieval_Stone.prefab");
            var gate  = LoadPoly("Gate_Medieval_Medium.prefab");
            var tower = LoadPoly("Tower_Castle_Round.prefab");
            if (wall == null || gate == null || tower == null)
            {
                EditorUtility.DisplayDialog("Spawn Wall Kit",
                    "Missing polyperfect prefab(s) — make sure the Low Poly Ultimate Pack is imported.\n" +
                    "(If pieces show magenta, run Defenders > Art > Fix Polyperfect URP Materials.)", "OK");
                return;
            }

            var parent = new GameObject(SouthName);
            Undo.RegisterCreatedObjectUndo(parent, "Spawn Castle Wall Kit");
            parent.transform.position = Vector3.zero;

            Place(gate,  new Vector3(   0f, 0f, SouthZ), parent.transform, "Gate_South");
            Place(wall,  new Vector3( -12f, 0f, SouthZ), parent.transform, "Wall_South_L");
            Place(wall,  new Vector3(  12f, 0f, SouthZ), parent.transform, "Wall_South_R");
            Place(tower, new Vector3( -42f, 0f, SouthZ), parent.transform, "CornerTower_South");

            Selection.activeGameObject = parent;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

            EditorUtility.DisplayDialog("Spawn Wall Kit",
                $"Spawned a fresh kit under '{SouthName}':\n" +
                "  - Gate at centre (x=0)\n  - 2 wall segments (x = -12 / +12)\n  - 1 corner tower (x=-42)\n\n" +
                "Arrange to taste, but KEEP IT SYMMETRIC ABOUT X=0 (the gate axis) — that's what makes the mirror " +
                "land clean on all four sides. Then run Defenders > Castle > Mirror South Side x4, save, and ping me to bake.",
                "OK");
        }

        private static void Place(GameObject prefab, Vector3 pos, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, "Spawn Castle Wall Kit");
        }

        private static GameObject LoadPoly(string fileName)
            => AssetDatabase.LoadAssetAtPath<GameObject>(PolyRoot + fileName);
    }
}
