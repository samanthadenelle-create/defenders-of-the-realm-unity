// Editor tool: the FIRST grid-CoC wall — a square ring of tiled 1.5x3x1.5 wall
// segments with a 2-segment-wide ENTRANCE centered on each of the 4 sides (owner
// spec 2026-06-13: "start with a square with 4 entrances 2 pieces wide at each
// midpoint"). It is a SCRIPT-to-restore (reproducible), so it builds directly into
// the regular MainCastle_Hall under its own GridWall_Square root — re-run to rebuild,
// delete the root to remove. The working castle walls/seam are untouched (separate root).
//
// Construction matches the owner's mental model: place the 4 CORNERS, fill each side
// with contiguous pieces (each touches the next up to the corner), then take side/2
// and remove the 2 midpoint pieces for the entrance — repeated on all four sides.
//
// Batchmode: DeNelle.Editor.GridWallBuilder.BuildInCastle
// Menu:      Defenders/Walls/Build Grid Square (4 entrances)  — into the OPEN scene
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeNelle.Village;          // WallSegment
using DeNelle.Village.Walls;    // WallTier, WallTierData

namespace DeNelle.Editor
{
    public static class GridWallBuilder
    {
        // One grid cell = one wall segment, normalized to the owner's box.
        private static readonly Vector3 SegSize = new Vector3(1.5f, 3.0f, 1.5f);
        // Owner: "if we know the exact length from tower to tower / width of wall gives the
        // total parts." So the part count is DERIVED, not hardcoded: parts = round(span / segW).
        // SideLengthMeters = the corner-to-corner (tower-to-tower) span per side.
        private const float SideLengthMeters = 18f;   // -> 18 / 1.5 = 12 parts per side
        private const int   EntranceWidth     = 2;     // segments removed at each side's midpoint
        private const string RootName      = "GridWall_Square";
        private const string CastleScene   = "Assets/Scenes/MainCastle_Hall.unity";

        [MenuItem("Defenders/Walls/Build Grid Square (4 entrances)")]
        public static void BuildInOpenScene()
        {
            var root = BuildSquare();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[GridWallBuilder] built '{RootName}' in the open scene (not saved — Ctrl+S to keep).");
        }

        // Batch: build the square into MainCastle_Hall (the regular scene) + save. Safe
        // because it lives under its own root and is fully reproducible from this script.
        public static void BuildInCastle()
        {
            var scene = EditorSceneManager.OpenScene(CastleScene, OpenSceneMode.Single);
            BuildSquare();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GridWallBuilder] built + saved '{RootName}' into {CastleScene} " +
                      $"(span {SideLengthMeters}m/side -> {Mathf.RoundToInt(SideLengthMeters / SegSize.x)} parts, " +
                      $"{EntranceWidth}-wide entrance per side, seg {SegSize.x}x{SegSize.y}x{SegSize.z}).");
        }

        // Core: a centered square ring of segments, skipping the 2 center cells per
        // side for the entrance. Corners are placed by the N/S rows only; the E/W
        // rows skip the shared corner cells so corners are single (no double-stack).
        private static GameObject BuildSquare()
        {
            var prior = GameObject.Find(RootName);
            if (prior != null) Object.DestroyImmediate(prior);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            // DERIVE the part count from the span: parts = round(tower-to-tower / segment width).
            int n = Mathf.Max(EntranceWidth + 2, Mathf.RoundToInt(SideLengthMeters / SegSize.x));
            float half = n * SegSize.x * 0.5f;          // half of the actual (whole-segment) side
            // Entrance = the EntranceWidth center cells, centered for any N (even or odd).
            int e0 = (n - EntranceWidth) / 2;
            int e1 = e0 + EntranceWidth - 1;

            var mat = BuildWoodMaterial();
            int placed = 0;

            // cell-center coordinate along an axis for index i
            float Coord(int i) => -half + (i + 0.5f) * SegSize.x;
            bool IsEntrance(int i) => i >= e0 && i <= e1;

            for (int i = 0; i < n; i++)
            {
                if (IsEntrance(i)) continue;
                // South (z = -half) and North (z = +half) — full rows incl. the 4 corners.
                placed += Place(root.transform, mat, new Vector3(Coord(i), 0f, -half), $"S_{i}");
                placed += Place(root.transform, mat, new Vector3(Coord(i), 0f, +half), $"N_{i}");
            }
            for (int i = 1; i < n - 1; i++)   // skip shared corner cells (0, n-1)
            {
                if (IsEntrance(i)) continue;
                // West (x = -half) and East (x = +half).
                placed += Place(root.transform, mat, new Vector3(-half, 0f, Coord(i)), $"W_{i}");
                placed += Place(root.transform, mat, new Vector3(+half, 0f, Coord(i)), $"E_{i}");
            }

            Debug.Log($"[GridWallBuilder] '{RootName}': {placed} segments placed " +
                      $"(entrance cells {e0}..{e1} skipped per side -> 4 gaps {EntranceWidth * SegSize.x}m wide; side {n * SegSize.x}m).");
            return root;
        }

        // Place one wall segment: parent wrapper (world-aligned) + the mesh child
        // rotated -90X to stand upright (the FBX imports lying flat), box-fit to
        // 1.5x3x1.5, ground-seated, tier material + a WallSegment (tier 1 = Wood).
        private static int Place(Transform parent, Material mat, Vector3 localPos, string name)
        {
            var prefab = Resources.Load<GameObject>(WallTierData.Get(WallTier.Wood).SegmentPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GridWallBuilder] missing Resources/{WallTierData.Get(WallTier.Wood).SegmentPrefabPath}");
                return 0;
            }

            var seg = new GameObject($"Wall_{name}");
            seg.transform.SetParent(parent, false);
            seg.transform.localPosition = localPos;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            go.transform.SetParent(seg.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // stand upright

            // Box-fit to 1.5x3x1.5 via the parent (world-aligned; -90X is axis-aligned).
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                var s = seg.transform.localScale;
                if (b.size.x > 0.0001f) s.x *= SegSize.x / b.size.x;
                if (b.size.y > 0.0001f) s.y *= SegSize.y / b.size.y;
                if (b.size.z > 0.0001f) s.z *= SegSize.z / b.size.z;
                seg.transform.localScale = s;
                // Ground-seat (re-measure after scale).
                var r2 = go.GetComponentsInChildren<Renderer>(true);
                var b2 = r2[0].bounds;
                for (int k = 1; k < r2.Length; k++) b2.Encapsulate(r2[k].bounds);
                seg.transform.position += new Vector3(0f, -b2.min.y, 0f);
            }

            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    for (int j = 0; j < ms.Length; j++) ms[j] = mat;
                    r.sharedMaterials = ms;
                }

            // Wire the existing upgrade/durability component (tier 1 = Wood).
            var ws = seg.AddComponent<WallSegment>();
            ws.SetTier((int)WallTier.Wood);
            return 1;
        }

        private static Material _woodMat;

        // URP/Lit wood material from the Tripo maps (same approach as WallPreview).
        private static Material BuildWoodMaterial()
        {
            if (_woodMat != null) return _woodMat;
            var baseTex = Resources.Load<Texture2D>("Walls/Textures/wood_basecolor");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GridWall_Wood_Mat" };
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            var normalTex = Resources.Load<Texture2D>("Walls/Textures/wood_normal");
            if (normalTex != null) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); }
            _woodMat = mat;
            return mat;
        }
    }
}
