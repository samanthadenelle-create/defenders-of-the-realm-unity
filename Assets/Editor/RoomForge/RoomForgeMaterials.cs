// =============================================================================
// RoomForgeMaterials — ONE shared wall mat + ONE shared floor mat for all rooms.
// -----------------------------------------------------------------------------
// Uses KayKit Dungeon atlas (dungeon_texture.png) already in the repo. Flat URP/Lit,
// tiled simply so procedural cube shells look like dungeon stone without per-piece
// UV authoring. Props from KayKit keep their own pack materials (Fix KayKit Materials).
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.RoomForge
{
    public static class RoomForgeMaterials
    {
        private const string OutFolder = "Assets/Dungeon/Materials";
        private const string WallMatPath = OutFolder + "/RoomWall_KayKit.mat";
        private const string FloorMatPath = OutFolder + "/RoomFloor_KayKit.mat";
        private const string AccentMatPath = OutFolder + "/RoomAccent_KayKit.mat";
        private const string UrpLit = "Universal Render Pipeline/Lit";

        // Preferred atlas paths (first hit wins).
        private static readonly string[] AtlasCandidates =
        {
            "Assets/Models/KayKit/dungeon/dungeon_texture.png",
            "Assets/Models/KayKit/dungeon/fbx(unity)/dungeon_texture.png",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx/dungeon_texture.png",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/textures/dungeon_texture.png",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx(unity)/dungeon_texture.png",
        };

        private static readonly string[] ExistingMatCandidates =
        {
            "Assets/Models/KayKit/dungeon/dungeon_texture_URP.mat",
            "Assets/Models/KayKit/dungeon/fbx(unity)/dungeon_texture_URP.mat",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx/dungeon_texture_URP.mat",
        };

        /// <summary>Shared wall material (tiling ~1–2 on large faces).</summary>
        public static Material Wall => GetOrCreate(WallMatPath, tiling: 1.5f, darken: 0.92f);

        /// <summary>Shared floor material (higher tiling so 6u cells read as tiles).</summary>
        public static Material Floor => GetOrCreate(FloorMatPath, tiling: 2.5f, darken: 1f);

        /// <summary>Optional accent (boss/reward) — same atlas, slight warm tint.</summary>
        public static Material Accent => GetOrCreate(AccentMatPath, tiling: 1.5f, darken: 1f, warmTint: true);

        [MenuItem("Defenders/Dungeon/Ensure Room Forge Materials (KayKit atlas)")]
        public static void EnsureMenu()
        {
            var w = Wall;
            var f = Floor;
            var a = Accent;
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomForgeMaterials] wall={(w != null ? WallMatPath : "NULL")} " +
                      $"floor={(f != null ? FloorMatPath : "NULL")} accent={(a != null ? AccentMatPath : "NULL")} " +
                      $"atlas={FindAtlasPath() ?? "MISSING"}");
        }

        /// <summary>Apply wall/floor materials to all MeshRenderers under root by name heuristic.</summary>
        public static void ApplyToRoomRoot(GameObject root, bool useAccentFloor = false)
        {
            if (root == null) return;
            var wall = Wall;
            var floor = useAccentFloor ? Accent : Floor;
            if (wall == null && floor == null) return;

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                bool isFloor = n.StartsWith("Floor") || n.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool isWall = n.StartsWith("Wall") || n.StartsWith("Choke") || n.StartsWith("Seal")
                              || n.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isFloor && floor != null) r.sharedMaterial = floor;
                else if (isWall && wall != null) r.sharedMaterial = wall;
                else if (wall != null) r.sharedMaterial = wall; // default shell → wall look
            }
        }

        private static Material GetOrCreate(string matPath, float tiling, float darken, bool warmTint = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                // Re-point base map if atlas moved / was fixed.
                Texture2D tex = LoadAtlas();
                if (tex != null && existing.GetTexture("_BaseMap") != tex)
                {
                    existing.SetTexture("_BaseMap", tex);
                    existing.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            // Prefer cloning an already-good KayKit URP mat if present.
            Material sourcePack = null;
            foreach (var p in ExistingMatCandidates)
            {
                sourcePack = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (sourcePack != null) break;
            }

            Shader shader = Shader.Find(UrpLit);
            if (shader == null)
            {
                Debug.LogError("[RoomForgeMaterials] URP Lit shader missing.");
                return sourcePack;
            }

            EnsureFolder(OutFolder);
            Material mat = sourcePack != null ? new Material(sourcePack) : new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(matPath);

            Texture2D atlas = LoadAtlas();
            if (atlas != null)
            {
                mat.SetTexture("_BaseMap", atlas);
                mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
                // URP also uses mainTexture on some paths
                mat.mainTexture = atlas;
                mat.mainTextureScale = new Vector2(tiling, tiling);
            }
            else
            {
                Debug.LogWarning("[RoomForgeMaterials] dungeon_texture.png not found under KayKit — " +
                                 "rooms will use flat URP Lit until pack is present. Run Fix KayKit Materials after import.");
            }

            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);

            Color baseCol = warmTint
                ? new Color(1f, 0.92f, 0.78f, 1f)
                : new Color(darken, darken, darken, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
            else mat.color = baseCol;

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static Texture2D LoadAtlas()
        {
            string path = FindAtlasPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static string FindAtlasPath()
        {
            foreach (var p in AtlasCandidates)
            {
                if (File.Exists(p.Replace("Assets/", Application.dataPath + "/"))
                    || AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null)
                {
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null)
                        return p;
                }
            }
            // Fallback: GUID search
            string[] guids = AssetDatabase.FindAssets("dungeon_texture t:Texture2D", new[] { "Assets/Models/KayKit" });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.EndsWith("dungeon_texture.png")) return path;
            }
            return null;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
