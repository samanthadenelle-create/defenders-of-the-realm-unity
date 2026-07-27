// =============================================================================
// RoomForgeMaterials — ONE shared wall mat + ONE shared floor mat for all rooms.
// -----------------------------------------------------------------------------
// SOLID STONE URP/Lit — deliberately NO texture. The procedural room shells are
// Unity PRIMITIVE CUBES; a cube maps the full 0→1 UV across every face, so sampling
// the KayKit colormap atlas (dungeon_texture.png = grid of solid-color swatches)
// repeats the WHOLE palette across each face = a rainbow patchwork. Flat stone
// _BaseColor sidesteps that. Props from KayKit keep their own pack materials (they
// are real FBX with authored UVs — Fix KayKit Materials — do NOT touch those).
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

        // Solid stone tones (no texture — see header for why atlas-on-cube = rainbow).
        private static readonly Color WallStone = new Color(0.42f, 0.42f, 0.45f, 1f);
        private static readonly Color FloorStone = new Color(0.30f, 0.30f, 0.33f, 1f);
        private static readonly Color AccentStone = new Color(0.46f, 0.41f, 0.35f, 1f); // warm stone

        /// <summary>Shared wall material — solid mid-grey stone.</summary>
        public static Material Wall => GetOrCreate(WallMatPath, WallStone);

        /// <summary>Shared floor material — solid darker stone.</summary>
        public static Material Floor => GetOrCreate(FloorMatPath, FloorStone);

        /// <summary>Optional accent (boss/reward) — solid warm stone.</summary>
        public static Material Accent => GetOrCreate(AccentMatPath, AccentStone);

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

        private static Material GetOrCreate(string matPath, Color stoneColor)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                // Regeneration: force solid stone. STRIP any atlas map (older versions of this
                // asset assigned dungeon_texture.png → rainbow on cube shells) and set stone color.
                bool dirty = false;
                if (existing.HasProperty("_BaseMap") && existing.GetTexture("_BaseMap") != null)
                {
                    existing.SetTexture("_BaseMap", null);
                    dirty = true;
                }
                if (existing.HasProperty("_MainTex") && existing.GetTexture("_MainTex") != null)
                {
                    existing.SetTexture("_MainTex", null);
                    dirty = true;
                }
                if (existing.mainTexture != null)
                {
                    existing.mainTexture = null;
                    dirty = true;
                }
                ApplyStone(existing, stoneColor, ref dirty);
                if (dirty) EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find(UrpLit);
            if (shader == null)
            {
                Debug.LogError("[RoomForgeMaterials] URP Lit shader missing.");
                return null;
            }

            EnsureFolder(OutFolder);
            // Fresh URP/Lit with NO texture — solid stone. Cube shells must not sample the atlas.
            Material mat = new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(matPath);

            bool _ = false;
            ApplyStone(mat, stoneColor, ref _);

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static void ApplyStone(Material mat, Color stoneColor, ref bool dirty)
        {
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            if (mat.HasProperty("_BaseColor"))
            {
                if (mat.GetColor("_BaseColor") != stoneColor) { mat.SetColor("_BaseColor", stoneColor); dirty = true; }
            }
            else if (mat.color != stoneColor) { mat.color = stoneColor; dirty = true; }

            if (mat.HasProperty("_Color") && mat.GetColor("_Color") != stoneColor)
            {
                mat.SetColor("_Color", stoneColor);
                dirty = true;
            }
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
