// =============================================================================
// PolyperfectUrpFix — converts the Low Poly Ultimate Pack (polyperfect) materials
// from built-in shaders to URP/Lit so they stop rendering magenta in this URP
// project. Headless equivalent of importing URP_LowPolyUltimatePack.unitypackage.
// -----------------------------------------------------------------------------
// The pack ships with built-in (Standard) materials; in URP those render pink.
// This scans every material under Assets/polyperfect and, for any on a built-in /
// error shader, swaps it to URP/Lit, carrying over base colour + main texture +
// emission. Materials already on URP are left alone. In-place edit (same material
// GUIDs), so the baked Village buildings render correctly without a re-bake.
//
// WO-323 (WHITE TREES) — second pass, RemapTreeFbxToAtlas():
//   The polyperfect tree FBX files import with materialImportMode:0 (None) and an
//   EMPTY externalObjects map (verified in the .fbx.meta — e.g. SM_Tree_Round.fbx,
//   SM_Tree_Beech.fbx). With no material imported, every tree renderer slot is
//   NULL, which URP draws as flat WHITE — the exact symptom in WO-323. The shared
//   atlas material M_Atlas_LPUP.mat is ALREADY correct (URP/Lit + _BaseMap wired to
//   the pack atlas), so the fix is NOT a shader swap but a MATERIAL ASSIGNMENT:
//   remap each tree FBX's material slots to M_Atlas_LPUP via the importer's
//   external-object map and reimport (the same proven mechanism KayKitMaterials
//   uses for the hex trees). This repairs the source at IMPORT level, so baked
//   scenes AND fresh generations render the trees textured with no re-bake of the
//   material asset and no runtime band-aid. Idempotent: a slot already pointing at
//   the atlas is left alone.
//
// polyperfect is gitignored → this is a LOCAL editor op (re-run after re-importing
// the pack on a fresh clone, same as importing the pack's own URP unitypackage).
//
// Run: -executeMethod DeNelle.Editor.PolyperfectUrpFix.Fix
// =============================================================================

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PolyperfectUrpFix
    {
        private const string Root = "Assets/polyperfect";

        // The pack's shared tree/nature atlas material — already URP/Lit with the
        // atlas wired into _BaseMap (verified in M_Atlas_LPUP.mat). Trees, bushes
        // and most nature props share this one material in the original pack scenes.
        private const string AtlasMatPath =
            "Assets/polyperfect/Low Poly Ultimate Pack/Materials/M_Atlas_LPUP.mat";

        [MenuItem("Defenders/Art/Fix Polyperfect URP Materials")]
        public static void Fix()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[PolyperfectUrpFix] 'Universal Render Pipeline/Lit' shader not found — is URP installed?");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { Root });
            int scanned = 0, converted = 0;

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                scanned++;

                string sn = mat.shader != null ? mat.shader.name : "";
                bool builtIn = mat.shader == null
                            || sn == "Standard"
                            || sn.StartsWith("Legacy Shaders/")
                            || sn.Contains("InternalErrorShader")
                            || sn == "Standard (Specular setup)";
                if (!builtIn) continue;   // already URP / a custom shader → leave it

                // Capture the legacy properties before the shader swap drops them.
                Color baseCol = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color emis = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

                mat.shader = lit;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
                if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (emis.maxColorComponent > 0.01f)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emis);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(mat);
                converted++;
            }

            // WO-323: remap the white tree FBX files to the shared atlas material.
            int treesRemapped = RemapTreeFbxToAtlas();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PolyperfectUrpFix] scanned {scanned} polyperfect material(s); converted {converted} built-in → URP/Lit; " +
                      $"remapped {treesRemapped} white tree FBX(s) → M_Atlas_LPUP (WO-323).");
        }

        // ── WO-323: white-tree FBX → atlas-material remap ────────────────────────
        // The tree FBX files import with NO material (materialImportMode:0, empty
        // externalObjects), so their renderer slots are null → URP white. Point each
        // tree FBX's material slots at the already-correct M_Atlas_LPUP.mat via the
        // importer external-object map and reimport. Returns the number of FBX
        // importers actually changed (idempotent: already-correct ones are skipped).
        private static int RemapTreeFbxToAtlas()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasMatPath);
            if (atlas == null)
            {
                Debug.LogWarning("[PolyperfectUrpFix] WO-323 — shared atlas material not found at " +
                                 AtlasMatPath + "; skipping tree-FBX remap (is the pack imported?).");
                return 0;
            }

            // Every tree FBX in the pack. The trees live under .../Nature_M|T/Trees_M|T/
            // and a couple of themed folders; match by the SM_Tree* file-name prefix so
            // we only touch trees, not every model in the pack.
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { Root });
            int remapped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in modelGuids)
                {
                    string fbxPath = AssetDatabase.GUIDToAssetPath(g);
                    if (string.IsNullOrEmpty(fbxPath)) continue;

                    string file = System.IO.Path.GetFileNameWithoutExtension(fbxPath);
                    if (file == null || !file.StartsWith("SM_Tree", System.StringComparison.OrdinalIgnoreCase))
                        continue;   // trees only

                    if (RemapModelToAtlas(fbxPath, atlas))
                        remapped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return remapped;
        }

        // Point every material slot of the FBX at the atlas material via the importer's
        // external-object remap, switching the importer to use external materials so the
        // remap takes effect (mirrors KayKitMaterials.RemapModelMaterials). Reimports
        // only when something changed. Returns true if a reimport was issued.
        private static bool RemapModelToAtlas(string fbxPath, Material atlas)
        {
            if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
                return false;

            bool importerChanged = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importerChanged = true;
            }
            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importerChanged = true;
            }

            // Build the set of material sub-asset identifiers to remap: the FBX's own
            // embedded materials, plus any keys already present in the external map.
            var identifiers = new System.Collections.Generic.List<AssetImporter.SourceAssetIdentifier>();
            var sourceMaterials = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(fbxPath)
                .OfType<Material>();
            foreach (var srcMat in sourceMaterials)
                identifiers.Add(new AssetImporter.SourceAssetIdentifier(srcMat));

            foreach (var kvp in importer.GetExternalObjectMap())
            {
                if (kvp.Key.type == typeof(Material) &&
                    !identifiers.Any(id => id.name == kvp.Key.name && id.type == kvp.Key.type))
                    identifiers.Add(kvp.Key);
            }

            // No identifiers at all (a fresh import that reported no sub-materials yet):
            // toggling the importer to External + reimporting makes Unity surface the
            // material slots, which a follow-up run then remaps. Still progress.
            bool remapChanged = false;
            foreach (var id in identifiers)
            {
                if (id.type != typeof(Material)) continue;
                var current = importer.GetExternalObjectMap();
                if (current.TryGetValue(id, out UnityEngine.Object existing) && existing == atlas)
                    continue;   // already pointing at the atlas — idempotent
                importer.AddRemap(id, atlas);
                remapChanged = true;
            }

            if (!importerChanged && !remapChanged)
                return false;

            importer.SaveAndReimport();
            return true;
        }
    }
}
