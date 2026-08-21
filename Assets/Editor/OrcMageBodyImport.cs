// =============================================================================
// OrcMageBodyImport — lands the 2026-08-20 AccuRig "orcmage" delivery into the
// EXISTING, unused `Orc_Mage` slot and binds its art at TIER 1.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/Tripo/Import Orc_Mage AccuRig body
// Batch: -executeMethod DeNelle.Editor.OrcMageBodyImport.RunBatch
// Marker: ORC_MAGE_IMPORT_OK   |   ORC_MAGE_IMPORT_FAIL <reason>
//
// OWNER RULING 2026-08-20: "use the unused orcmage" — the new body REPLACES the
// mesh at Assets/EnemyContent/Orc_Mage.fbx rather than landing alongside under a
// new name. The .fbx BINARY is swapped; the .meta (and therefore the GUID, and
// therefore every Addressables entry that points at it) is PRESERVED and only
// re-configured here.
//
// WHY THIS FILE EXISTS AT ALL — the three-part trap (MASTER_CATALOG/resources-art §3b).
// TripoAssetPostprocessor.OnPreprocessModel claims EVERY FBX under
// Assets/EnemyContent/ and, absent a `<Body>.fbx.tripo-extracted` sentinel, force-sets
// materialLocation = External (legacy) + materialName = BasedOnTextureName +
// materialSearch = RecursiveUp on EVERY import. Legacy External IGNORES the .meta
// externalObjects remap table and resolves a model's material by SEARCHING the project
// for a .mat named after the TEXTURE. So the fix is THREE parts and any TWO of them
// still render another body's art:
//     1. the SENTINEL  — the postprocessor's own opt-out, so it stops rewriting us
//     2. materialLocation = InPrefab  — so the remap table is consulted at all
//     3. a PER-BODY .mat bound to this body's OWN .fbm diffuse
// Applying only 1+2 leaves the mesh with a null-albedo embedded material; applying
// only 2+3 lets the next reimport silently revert both.
//
// WHICH ATLAS, AND WHY IT IS NOT THE PRETTIER FILENAME. The delivery ships TWO
// diffuse images and they are DIFFERENT PICTURES, not one file renamed:
//     orcmage.fbm/tripo_mat_2256a6d3_Pbr_Diffuse.jpg   md5 b2bd4950…  (125 KB)
//     tripo_convert_….fbm/orcmage_basecolor.JPEG        md5 f90e74b7…  ( 76 KB)
// AccuRig re-baked its atlas for its OWN mesh, so the HASHED maps are the ones
// authored against the RIGGED mesh's UVs; the nicely-named ones belong to the
// UNRIGGED tripo_convert export. Binding a texture authored for one mesh onto a
// differently-UV'd mesh is precisely the scrambled-patches defect this project spent
// 2026-08-20 removing (seven skeletons, then Orc_Warrior/Orc_Tank). The choice is
// PROVEN BY RENDER in OrcMageProof, not argued from filenames.
//
// ⛔ WHAT THIS DELIBERATELY DOES NOT DO:
//   • never touches enemies.json — which enemy WEARS this body is an owner decision
//   • never edits any other body's fbx/meta/mat, the Addressables data, or a scene
//   • never deletes or overwrites TripoTex/Orc_Mage_* or OrcTex/Orc_Mage_* — those
//     atlases were authored for the SUPERSEDED sculpt and other assets assert they
//     exist (EnemyRigColorRegression:182, AtbCombatantSwapper:761-763). Tier-1
//     binding is what makes them unreachable for this model; removing them is a
//     separate ticket. ADD, never mutate.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class OrcMageBodyImport
    {
        private const string EC = "Assets/EnemyContent/";

        private const string FbxPath = EC + "Orc_Mage.fbx";
        private const string SentinelPath = FbxPath + ".tripo-extracted";
        private const string MatPath = EC + "Orc_Mage.mat";

        /// <summary>The AccuRig-baked maps — authored against the RIGGED mesh's UVs.
        /// The .fbm sidecar is where §3b requires a body's own base map to live, and where
        /// EnemyBodyTextureRegression asserts it lives.</summary>
        private const string DiffusePath = EC + "Orc_Mage.fbm/tripo_mat_2256a6d3_Pbr_Diffuse.jpg";
        private const string NormalPath = EC + "Orc_Mage.fbm/tripo_mat_2256a6d3_Pbr_Normal.jpg";

        /// <summary>
        /// Material node names to remap. The FBX declares exactly one material,
        /// `tripo_mat_2256a6d3_Pbr` (verified by binary scan of the delivered file). The
        /// texture-derived aliases are listed too because the importer's material NAMING
        /// mode decides which key Unity writes, and a remap keyed on a name Unity did not
        /// choose is a remap that silently does nothing — the failure mode that left
        /// Necromancer_NEW untextured. Necromancer_NEW.fbx.meta carries both spellings for
        /// the same reason; this follows that precedent rather than betting on one.
        /// </summary>
        private static readonly string[] MaterialKeys =
        {
            "tripo_mat_2256a6d3_Pbr",
            "tripo_mat_2256a6d3_Pbr_Diffuse",
            "tripo_mat_2256a6d3",
        };

        [MenuItem("Defenders/Tripo/Import Orc_Mage AccuRig body")]
        public static void RunMenu() => Run();

        public static void RunBatch() => Run();

        public static void Run()
        {
            var log = new StringBuilder();
            var failures = new List<string>();

            AssetDatabase.Refresh();

            // ── 1. THE SENTINEL ──────────────────────────────────────────────────
            // Written FIRST and re-asserted on every run. Without it the very next
            // reimport (this one included) hands the importer back to
            // TripoAssetPostprocessor, which flips materialLocation to External and
            // discards everything below. It is TRACKED as of 2026-08-20 (the
            // *.tripo-extracted gitignore line was lifted), so unlike the six older
            // sentinels this one survives a fresh clone.
            if (!File.Exists(SentinelPath))
            {
                File.WriteAllText(SentinelPath,
                    "Tripo textures extracted by TripoAssetPostprocessor. " +
                    "Delete this file (and the sibling Textures/ folder) to force re-extract.");
                log.AppendLine("SENTINEL  written " + SentinelPath);
            }
            else
            {
                log.AppendLine("SENTINEL  present " + SentinelPath);
            }
            AssetDatabase.Refresh();

            // ── 2. TEXTURE IMPORT TYPES ──────────────────────────────────────────
            // A normal map imported as a Default sRGB colour texture renders scrambled
            // lighting — seven pre-existing TripoTex normals are typed that way and this
            // is not becoming the eighth.
            if (!SetTextureType(DiffusePath, TextureImporterType.Default, true, log)) failures.Add("diffuse import");
            if (!SetTextureType(NormalPath, TextureImporterType.NormalMap, false, log)) failures.Add("normal import");

            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (diffuse == null) failures.Add("diffuse NOT FOUND at " + DiffusePath);
            if (normal == null) failures.Add("normal NOT FOUND at " + NormalPath);

            // ── 3. THE PER-BODY MATERIAL ─────────────────────────────────────────
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                failures.Add("URP/Lit shader not found — is this project still URP?");
            }
            else
            {
                if (mat == null)
                {
                    mat = new Material(shader) { name = "Orc_Mage" };
                    AssetDatabase.CreateAsset(mat, MatPath);
                    log.AppendLine("MATERIAL  created " + MatPath);
                }
                else
                {
                    mat.shader = shader;
                    log.AppendLine("MATERIAL  updated " + MatPath);
                }

                if (diffuse != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diffuse);
                }
                if (normal != null && mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
                // Skin/cloth, not chrome. The AccuRig atlas carries its own shading; a
                // high smoothness or any metallic turns an orc into a mannequin.
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }

            // ── 4. THE MODEL IMPORTER ────────────────────────────────────────────
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("ORC_MAGE_IMPORT_FAIL no ModelImporter at " + FbxPath);
                return;
            }

            // Drop the SUPERSEDED sculpt's remaps. They key on material names the new
            // FBX does not declare, so leaving them is not merely untidy — a stale key
            // is indistinguishable in the .meta from a live one, and the next person
            // reading the file cannot tell which binding is real.
            foreach (var kv in new List<KeyValuePair<AssetImporter.SourceAssetIdentifier, Object>>(
                         importer.GetExternalObjectMap()))
            {
                importer.RemoveRemap(kv.Key);
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;   // part 2 of 3
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;

            // HUMANOID, regenerated. The .meta we inherited carries the SUPERSEDED sculpt's
            // human bone table; clearing it and leaving autoGenerateAvatarMappingIfUnspecified
            // on makes Unity re-derive the mapping from THIS skeleton. A stale mapping is the
            // "sliding statue" path: the avatar is non-null, so a naive check passes, while the
            // clips retarget onto bones that are not there and the body holds its bind pose.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var hd = importer.humanDescription;
            hd.human = new HumanBone[0];
            hd.skeleton = new SkeletonBone[0];
            importer.humanDescription = hd;

            importer.SaveAndReimport();

            // Remap AFTER the reimport: the material sub-assets only exist once the model
            // has been parsed, and a remap written against names that do not exist yet is
            // silently dropped.
            if (mat != null)
            {
                foreach (var key in MaterialKeys)
                {
                    importer.AddRemap(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), key), mat);
                }
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh();

            // ── 5. READ THE RESULT BACK OFF THE IMPORTED ASSET ───────────────────
            // Everything above is intent. This is measurement.
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            log.AppendLine($"IMPORTER  materialLocation={importer.materialLocation} " +
                           $"materialName={importer.materialName} materialSearch={importer.materialSearch} " +
                           $"animationType={importer.animationType} avatarSetup={importer.avatarSetup} " +
                           $"bakeAxisConversion={importer.bakeAxisConversion} " +
                           $"externalObjects={importer.GetExternalObjectMap().Count}");

            if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                failures.Add("materialLocation reverted to " + importer.materialLocation +
                             " — the postprocessor won despite the sentinel");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                failures.Add("the FBX did not import to a GameObject at all");
            }
            else
            {
                var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var bones = new HashSet<Transform>();
                int verts = 0, boundMats = 0, slots = 0;
                foreach (var s in smrs)
                {
                    if (s == null) continue;
                    if (s.sharedMesh != null) verts += s.sharedMesh.vertexCount;
                    if (s.bones != null) foreach (var b in s.bones) if (b != null) bones.Add(b);
                    var ms = s.sharedMaterials;
                    if (ms == null) continue;
                    foreach (var m in ms)
                    {
                        slots++;
                        if (m == null) continue;
                        if (AssetDatabase.GetAssetPath(m) == MatPath) boundMats++;
                    }
                }

                var bounds = new Bounds(model.transform.position, Vector3.zero);
                bool anyB = false;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (!anyB) { bounds = r.bounds; anyB = true; }
                    else bounds.Encapsulate(r.bounds);
                }

                int ccBase = 0;
                foreach (var b in bones) if (b.name.StartsWith("CC_Base")) ccBase++;

                log.AppendLine($"MESH      skinned={smrs.Length} verts={verts} " +
                               $"distinctBones={bones.Count} (CC_Base={ccBase}) " +
                               $"bounds=({bounds.size.x:F2},{bounds.size.y:F2},{bounds.size.z:F2})");
                log.AppendLine($"MATERIAL  slots={slots} boundToOrc_Mage.mat={boundMats}");

                if (smrs.Length == 0) failures.Add("no SkinnedMeshRenderer — the mesh imported UNRIGGED");
                if (bones.Count == 0) failures.Add("zero bones — dead rig");
                if (slots > 0 && boundMats != slots)
                    failures.Add($"only {boundMats}/{slots} material slots resolved to {MatPath} — " +
                                 "the remap did not take, so this body is wearing something else");

                // UPRIGHT CHECK. Tripo exports Z-up; a humanoid whose bounding box is
                // deeper than it is tall imported lying down. Measured rather than assumed,
                // because "it looked fine in the render" is not a number.
                if (bounds.size.y < bounds.size.z)
                    failures.Add($"the body imported LYING DOWN (boundsY={bounds.size.y:F2} < " +
                                 $"boundsZ={bounds.size.z:F2}) — bakeAxisConversion needs flipping");

                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FbxPath);
                if (avatar == null)
                {
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                        if (o is Avatar a) { avatar = a; break; }
                }
                log.AppendLine($"AVATAR    {(avatar == null ? "NULL" : avatar.name)} " +
                               $"valid={(avatar != null && avatar.isValid)} " +
                               $"human={(avatar != null && avatar.isHuman)}");
                if (avatar == null || !avatar.isValid)
                    failures.Add("humanoid avatar is missing or INVALID — clips would retarget onto nothing");
                else if (!avatar.isHuman)
                    failures.Add("avatar imported GENERIC, not humanoid — the shared humanoid clips cannot retarget");

                int clips = 0;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips++;
                log.AppendLine($"CLIPS     embedded AnimationClips imported = {clips} " +
                               "(the body animates from the shared OrcHumanoid_Mage controller, " +
                               "so this number is informational, not a pass condition)");
            }

            Debug.Log("[OrcMageImport]\n" + log);

            if (failures.Count == 0)
            {
                Debug.Log("ORC_MAGE_IMPORT_OK Orc_Mage.fbx replaced with the AccuRig body, " +
                          "sentinel + InPrefab + per-body .mat all in place.");
            }
            else
            {
                foreach (var f in failures) Debug.LogError("[OrcMageImport] DEFECT: " + f);
                Debug.LogError($"ORC_MAGE_IMPORT_FAIL {failures.Count} defect(s): " +
                               string.Join(" | ", failures));
            }
        }

        private static bool SetTextureType(string path, TextureImporterType type, bool srgb, StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                log.AppendLine("TEXTURE   NO IMPORTER at " + path);
                return false;
            }
            if (imp.textureType != type || imp.sRGBTexture != srgb)
            {
                imp.textureType = type;
                imp.sRGBTexture = srgb;
                imp.SaveAndReimport();
            }
            log.AppendLine($"TEXTURE   {path} type={imp.textureType} sRGB={imp.sRGBTexture}");
            return true;
        }
    }
}
