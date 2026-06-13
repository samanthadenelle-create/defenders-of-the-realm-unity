// Editor tool: drop the 3 wall-tier meshes (wood/iron/steel) as a visible demo row in
// MainCastle_Hall so the owner can judge the Wood->Iron->Reinforced-Steel look in-game.
// PREVIEW ONLY — a castle rebake will wipe this row (it's not in BuildCastleHub).
// Batchmode: DeNelle.Editor.WallPreview.PlaceInCastle
using System.Collections.Generic;
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
                    // Parent wrapper (no rotation) so the per-axis box-fit maps cleanly to WORLD
                    // axes even though the mesh child is rotated -90X to stand upright (owner: the
                    // FBX imports lying on its back; -90X stands it up — same as the Tree of Life).
                    var seg = new GameObject($"Wall_{name}_{i}");
                    seg.transform.SetParent(row.transform, false);
                    float x = (i - (RunLength - 1) / 2f) * TargetSize.x;   // contiguous, centred run
                    seg.transform.localPosition = new Vector3(x, 0f, z);

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (go == null) go = Object.Instantiate(prefab);
                    go.transform.SetParent(seg.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // stand it upright

                    // Box-fit to 1.5 x 3.0 x 1.5 via the PARENT (world-aligned) from the rotated
                    // child's world bounds (-90X is axis-aligned, so no shear).
                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds;
                        for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                        var s = seg.transform.localScale;
                        if (b.size.x > 0.0001f) s.x *= TargetSize.x / b.size.x;
                        if (b.size.y > 0.0001f) s.y *= TargetSize.y / b.size.y;
                        if (b.size.z > 0.0001f) s.z *= TargetSize.z / b.size.z;
                        seg.transform.localScale = s;
                        // Ground-seat the parent (re-measure after scale + rotation).
                        var r2 = go.GetComponentsInChildren<Renderer>(true);
                        var b2 = r2[0].bounds;
                        for (int k = 1; k < r2.Length; k++) b2.Encapsulate(r2[k].bounds);
                        seg.transform.position += new Vector3(0f, -b2.min.y, 0f);
                    }

                    // Assign the tier's textured URP material (Tripo basecolor/normal/metallic;
                    // steel also emissive so the runes glow). Falls back to the Tripo fixer (grey)
                    // if a tier's textures aren't imported yet.
                    var mat = BuildTierMaterial(name.ToLower());
                    if (mat != null)
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        {
                            var ms = r.sharedMaterials;
                            for (int j = 0; j < ms.Length; j++) ms[j] = mat;
                            r.sharedMaterials = ms;
                        }
                    else
                    {
                        var tripoFix = FindType("DeNelle.Core.TripoMaterialFixer");
                        if (tripoFix != null && go.GetComponent(tripoFix) == null) go.AddComponent(tripoFix);
                    }
                }
                Debug.Log($"[WallPreview] placed {RunLength}x {name} run (1.5x3x1.5 each) at z={z}.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WallPreview] saved — demo row in MainCastle_Hall courtyard (~0,0,3).");
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();

        // URP/Lit material for a tier from its Tripo maps in Resources/Walls/Textures/{tier}_*.
        private static Material BuildTierMaterial(string tier)
        {
            if (_matCache.TryGetValue(tier, out var cached) && cached != null) return cached;
            var baseTex = Resources.Load<Texture2D>($"Walls/Textures/{tier}_basecolor");
            if (baseTex == null) return null;   // this tier's textures not imported yet

            // Import-type fixups: normal map as NormalMap; metallic/roughness linear.
            SetImporter($"Walls/Textures/{tier}_normal", TextureImporterType.NormalMap, false);
            SetImporter($"Walls/Textures/{tier}_metallic", TextureImporterType.Default, true);
            SetImporter($"Walls/Textures/{tier}_roughness", TextureImporterType.Default, true);
            var normalTex = Resources.Load<Texture2D>($"Walls/Textures/{tier}_normal");
            var metalTex = Resources.Load<Texture2D>($"Walls/Textures/{tier}_metallic");

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"Wall_{tier}_Mat" };
            mat.SetTexture("_BaseMap", baseTex);
            if (normalTex != null) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); }
            if (metalTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", metalTex);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_GlossMapScale", tier == "wood" ? 0.2f : 0.45f); // smoothness control
            }
            if (tier == "steel")
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", baseTex);      // bright blue runes in basecolor emit
                mat.SetColor("_EmissionColor", new Color(0.45f, 0.65f, 1f) * 1.6f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            _matCache[tier] = mat;
            return mat;
        }

        private static void SetImporter(string resPath, TextureImporterType type, bool linear)
        {
            var p = $"Assets/Resources/{resPath}.JPEG";
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) return;
            bool changed = false;
            if (imp.textureType != type) { imp.textureType = type; changed = true; }
            if (linear && imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
            if (changed) imp.SaveAndReimport();
        }
    }
}
