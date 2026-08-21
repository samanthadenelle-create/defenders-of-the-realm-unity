// =============================================================================
// HollowWalkerImport — import + BIND the owner's AccuRig "Hollow Walker" body.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/Art/Hollow Walker/1 Import + Bind
//        Defenders/Art/Hollow Walker/2 Prove rig + animation + render
// Batch: -executeMethod DeNelle.Editor.HollowWalkerImport.Run
// Markers: HOLLOW_WALKER_IMPORT_OK <n> checks | HOLLOW_WALKER_IMPORT_FAIL
// The rig/animation/render proof is the SIBLING file HollowWalkerProof.cs
// (-executeMethod DeNelle.Editor.HollowWalkerProof.Run -> HOLLOW_WALKER_PROOF_OK).
//
// WHY THIS FILE EXISTS RATHER THAN A HAND-EDITED .meta.
// docs/MASTER_CATALOG/resources-art.md §3b documents a trap this project has already
// paid for: TripoAssetPostprocessor.OnPreprocessModel claims EVERY FBX under
// Assets/EnemyContent and, absent a "<Body>.fbx.tripo-extracted" sentinel, force-sets
// materialLocation = External (legacy) + materialName = BasedOnTextureName +
// materialSearch = RecursiveUp on EVERY import. Legacy External IGNORES the .meta
// externalObjects remap table and resolves a model's material by SEARCHING the project
// for a .mat named after the TEXTURE. All four AccuRig skeleton bodies name their
// diffuse "Material_Pbr_Diffuse", so seven enemy ids collapsed onto ONE material and
// wore the Mage's UV layout. A hand-written .meta is therefore NOT a fix — the next
// reimport rewrites it. The fix is three parts, applied together, and this script is
// the thing that applies all three in one deterministic pass:
//   (1) the sentinel (the postprocessor's own opt-out),
//   (2) materialLocation = InPrefab,
//   (3) an explicit per-body .mat remap bound to this body's OWN Hollow_Walker.fbm art.
// Any TWO of the three still render another body's texture.
//
// WHY THE ALBEDO COMES FROM THE FBM AND NOT FROM THE LOOSE hollowwalker_*.JPEG SET.
// Hollow_Walker.fbx EMBEDS its two authored maps as binary JPEG chunks:
//   tripo_mat_836f0627_Pbr_Diffuse.jpg  and  tripo_mat_836f0627_Pbr_Normal.jpg
// (verified by reading the FBX's Video/Content nodes, not inferred). Those are the exact
// maps the mesh's UVs were authored against, they carry Tripo's per-model hash so they
// cannot collide with the Material_Pbr_Diffuse family by NAME, and extracting them puts
// them in this body's own "<Body>.fbm/" folder — which is precisely what
// EnemyBodyTextureRegression rule (B) requires. Binding here is TIER 1 in
// EnemyArtCoverageRegression's precedence: real in edit mode, real in a build, real
// before Start() runs, and the only tier guaranteed to match the mesh's own UVs. The
// runtime TripoTex atlas fallback (tier 2) is NOT used and must not be relied on — it
// resolves through an address whose Resources half (Assets/Resources/Enemies) no longer
// exists, and it only applies once Start() has run.
//
// ⚠ NORMAL MAPS. A JPEG bound to _BumpMap while the importer still types it Default is
// decoded as if it were DXT5nm and renders as scrambled lighting — which reads as a
// broken MESH rather than a wrong import setting. Seven pre-existing TripoTex normals are
// wrong this way. This script sets textureType = NormalMap BEFORE binding, and REFUSES to
// bind a normal it could not type (it logs and leaves _BumpMap empty instead).
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>One-shot importer + binder for the owner's Hollow Walker AccuRig body.</summary>
    public static class HollowWalkerImport
    {
        public const string Model = "Hollow_Walker";

        private const string ContentRoot = "Assets/EnemyContent";
        private const string FbxPath     = ContentRoot + "/" + Model + ".fbx";
        private const string FbmDir      = ContentRoot + "/" + Model + ".fbm";
        private const string MatRoot     = ContentRoot + "/Materials";
        private const string MatPath     = MatRoot + "/" + Model + "_Body.mat";
        private const string Sentinel    = FbxPath + ".tripo-extracted";

        private const string OkMarker   = "HOLLOW_WALKER_IMPORT_OK";
        private const string FailMarker = "HOLLOW_WALKER_IMPORT_FAIL";

        // ─────────────────────────────────────────────────────────────────────────
        [MenuItem("Defenders/Art/Hollow Walker/1 Import + Bind")]
        public static void Run()
        {
            var notes = new List<string>();
            var fails = new List<string>();

            try
            {
                Import(notes, fails);
            }
            catch (System.Exception ex)
            {
                fails.Add("threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }

            foreach (string n in notes) Debug.Log("[HollowWalker] " + n);

            if (fails.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append(FailMarker).Append(' ').Append(fails.Count).Append(" defect(s):");
                foreach (string f in fails) sb.Append("\n  - ").Append(f);
                Debug.LogError(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log(OkMarker + " " + notes.Count + " checks");
        }

        // ─────────────────────────────────────────────────────────────────────────
        private static void Import(List<string> notes, List<string> fails)
        {
            if (!File.Exists(FbxPath))
            {
                fails.Add("no FBX at " + FbxPath + " — copy the owner's Hollow.fbx there first.");
                return;
            }
            notes.Add("source: " + FbxPath + " (" + new FileInfo(FbxPath).Length + " bytes)");

            // ── (1) the postprocessor opt-out, BEFORE anything triggers an import ──
            if (!File.Exists(Sentinel))
            {
                fails.Add("sentinel " + Sentinel + " is MISSING. Without it " +
                          "TripoAssetPostprocessor.OnPreprocessModel force-sets materialLocation=External " +
                          "+ materialName=BasedOnTextureName on this FBX and the remap below becomes inert " +
                          "(resources-art.md §3b). Write the sentinel first.");
                return;
            }
            notes.Add("sentinel present: " + Sentinel + " — the postprocessor will not rewrite this importer");

            AssetDatabase.ImportAsset(FbxPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) { fails.Add(FbxPath + " did not produce a ModelImporter."); return; }

            // ── rig + animation, mirroring the working AccuRig bodies ─────────────
            // Skeleton_Warrior.fbx.meta (verified at source): animationType 3 (Human),
            // avatarSetup 1 (CreateFromThisModel), autoGenerateAvatarMappingIfUnspecified 1,
            // bakeAxisConversion 1. Same CC_Base bone naming, so the same settings apply.
            importer.animationType  = ModelImporterAnimationType.Human;
            importer.avatarSetup    = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.bakeAxisConversion = true;

            // ── (2) materials stay IN the prefab so the remap table is honoured ───
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation   = ModelImporterMaterialLocation.InPrefab;
            importer.materialName       = ModelImporterMaterialName.BasedOnMaterialName;

            importer.SaveAndReimport();
            notes.Add("importer configured: animationType=Human, avatarSetup=CreateFromThisModel, " +
                      "materialLocation=InPrefab, bakeAxisConversion=true");

            // ── extract this body's OWN embedded art into <Body>.fbm ──────────────
            if (!HasImage(FbmDir))
            {
                importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
                bool extracted = importer != null && importer.ExtractTextures(FbmDir);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                notes.Add("ExtractTextures(" + FbmDir + ") -> " + extracted);
            }
            if (!HasImage(FbmDir))
            {
                fails.Add("no image landed in " + FbmDir + " — the FBX's embedded diffuse/normal did not " +
                          "extract, so there is no own-.fbm art to bind (tier 1 impossible).");
                return;
            }
            foreach (string p in ImagesIn(FbmDir)) notes.Add("extracted: " + p);

            // ── type the maps BEFORE binding them ─────────────────────────────────
            string albedoPath = PickImage(FbmDir, new[] { "diffuse", "basecolor", "albedo" });
            string normalPath = PickImage(FbmDir, new[] { "normal", "_nrm" });

            if (string.IsNullOrEmpty(albedoPath))
            {
                fails.Add("no diffuse/basecolor image found in " + FbmDir + ".");
                return;
            }
            if (!ConfigureTexture(albedoPath, TextureImporterType.Default, sRgb: true, notes, fails)) return;
            notes.Add("albedo: " + albedoPath + " (sRGB, Default)");

            bool normalOk = false;
            if (!string.IsNullOrEmpty(normalPath))
                normalOk = ConfigureTexture(normalPath, TextureImporterType.NormalMap, sRgb: false, notes, fails);
            if (normalOk) notes.Add("normal: " + normalPath + " (NormalMap, linear)");
            else notes.Add("NO usable normal map — _BumpMap deliberately left EMPTY rather than bound to a " +
                           "Default-typed JPEG, which decodes as DXT5nm and renders scrambled lighting.");

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normal = normalOk ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            if (albedo == null) { fails.Add("albedo " + albedoPath + " would not load as a Texture2D."); return; }

            // ── (3) author a PER-BODY material and remap every material name ──────
            Material mat = EnsureMaterial(albedo, normal, notes, fails);
            if (mat == null) return;

            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) { fails.Add("ModelImporter vanished before the remap step."); return; }

            List<string> names = MaterialNames(importer, notes);
            if (names.Count == 0)
            {
                fails.Add("found NO material name to remap — not as an FBX sub-asset, not in the importer's " +
                          "existing external-object map, and not on any renderer of the imported prefab. " +
                          "Without a name there is no remap key, so nothing can be bound.");
                return;
            }
            foreach (string n in names)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
                notes.Add("remapped material '" + n + "' -> " + MatPath);
            }
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName     = ModelImporterMaterialName.BasedOnMaterialName;
            importer.SaveAndReimport();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(notes, fails, normalOk);
        }

        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>Reads the OUTCOME off the imported model — never off the .meta text.</summary>
        private static void Verify(List<string> notes, List<string> fails, bool expectNormal)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (go == null) { fails.Add("imported model asset would not load from " + FbxPath); return; }

            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs.Length == 0) { fails.Add("no SkinnedMeshRenderer — the body did not import as a skinned mesh."); }

            int bones = 0, verts = 0;
            foreach (var s in smrs)
            {
                if (s.sharedMesh == null) { fails.Add("SkinnedMeshRenderer '" + s.name + "' has a NULL sharedMesh — dead rig."); continue; }
                verts += s.sharedMesh.vertexCount;
                bones += s.bones != null ? s.bones.Length : 0;
                if (s.bones == null || s.bones.Length == 0)
                    fails.Add("SkinnedMeshRenderer '" + s.name + "' binds ZERO bones — the skin did not attach to the skeleton.");
            }
            notes.Add("mesh: " + smrs.Length + " skinned renderer(s), " + verts + " verts, " + bones + " bound bone(s)");

            var animator = go.GetComponentInChildren<Animator>(true);
            Avatar avatar = animator != null ? animator.avatar : null;
            if (avatar == null) fails.Add("no Avatar on the imported model — a humanoid mesh with no avatar is the 'sliding statue' path.");
            else
            {
                if (!avatar.isValid) fails.Add("Avatar '" + avatar.name + "' is INVALID.");
                if (!avatar.isHuman) fails.Add("Avatar '" + avatar.name + "' is not HUMAN — it cannot retarget onto the SkeletonHumanoid controller the family uses.");
                notes.Add("avatar: '" + avatar.name + "' isValid=" + avatar.isValid + " isHuman=" + avatar.isHuman);
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
                                     .Where(c => c != null && !c.name.StartsWith("__preview__")).ToList();
            if (clips.Count == 0) fails.Add("the FBX imported ZERO AnimationClips — the AnimStacks did not come through.");
            foreach (var c in clips)
                notes.Add("clip: '" + c.name + "' " + c.length.ToString("F2") + "s, frameRate=" + c.frameRate);

            // material slots — a null slot renders engine-default MAGENTA
            int slots = 0, nulls = 0, noBase = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    slots++;
                    if (m == null) { nulls++; continue; }
                    Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                    if (t == null) { noBase++; continue; }
                    string tp = AssetDatabase.GetAssetPath(t);
                    if (!tp.Replace('\\', '/').Contains("/" + Model + ".fbm/"))
                        fails.Add("body binds base map '" + tp + "', which is OUTSIDE its own '" + Model +
                                  ".fbm/' folder — that is the shared-texture defect (resources-art.md §3b rule B).");
                    else notes.Add("bound base map: " + tp);

                    if (expectNormal && m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") == null)
                        fails.Add("a normal map was typed as NormalMap but the material's _BumpMap is empty.");
                }
            if (nulls > 0)  fails.Add(nulls + " material slot(s) are NULL — those renderers draw engine-default magenta.");
            if (noBase > 0) fails.Add(noBase + " material slot(s) carry NO base map — that body part renders untextured.");
            notes.Add("materials: " + slots + " slot(s), " + nulls + " null, " + noBase + " without a base map");
        }

        // ─────────────────────────────────────────────────────────────────────────
        private static Material EnsureMaterial(Texture2D albedo, Texture2D normal, List<string> notes, List<string> fails)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { fails.Add("URP/Lit shader not found — is the project on URP?"); return null; }

            if (!AssetDatabase.IsValidFolder(MatRoot)) AssetDatabase.CreateFolder(ContentRoot, "Materials");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            bool created = false;
            if (mat == null)
            {
                // Named after the MODEL, never after the texture: a texture-named material is
                // exactly what the importer's legacy name search collides on across bodies.
                mat = new Material(shader) { name = Model + "_Body" };
                AssetDatabase.CreateAsset(mat, MatPath);
                created = true;
            }
            if (mat.shader != shader) mat.shader = shader;

            // BOTH slot names. URP renders from _BaseMap, but TripoMaterialFixer's runtime rebuild
            // probes _MainTex first and only then _BaseMap — write one and not the other and the
            // body is textured for the renderer but blank for the fixer, which then paints a
            // miss-tint OVER real art.
            if (mat.HasProperty("_BaseMap"))    mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_MainTex"))    mat.SetTexture("_MainTex", albedo);
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))      mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            notes.Add((created ? "created " : "updated ") + MatPath +
                      " (URP/Lit, _BaseMap+_MainTex set, _BumpMap " + (normal != null ? "set" : "EMPTY") + ")");
            return mat;
        }

        /// <summary>
        /// Every material name that needs a remap entry, gathered from THREE places because
        /// any one of them can legitimately be empty: (a) FBX sub-assets — present only while
        /// materialLocation is InPrefab; (b) the importer's existing external-object map —
        /// survives a flip to External; (c) the names on the imported prefab's renderers — the
        /// ground truth of what the mesh asks for, and the only source that still works when
        /// both others are empty. A sub-asset-only probe cannot repair the state the
        /// postprocessor creates.
        /// </summary>
        private static List<string> MaterialNames(ModelImporter importer, List<string> notes)
        {
            var names = new List<string>();

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (o is Material m && !string.IsNullOrEmpty(m.name) && !names.Contains(m.name))
                { names.Add(m.name); notes.Add("material name (fbx sub-asset): " + m.name); }

            foreach (var kv in importer.GetExternalObjectMap())
                if (kv.Key.type == typeof(Material) && !string.IsNullOrEmpty(kv.Key.name) && !names.Contains(kv.Key.name))
                { names.Add(kv.Key.name); notes.Add("material name (existing remap): " + kv.Key.name); }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (go != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string n = m.name.Replace(" (Instance)", "");
                        if (!string.IsNullOrEmpty(n) && !names.Contains(n))
                        { names.Add(n); notes.Add("material name (prefab renderer): " + n); }
                    }

            return names;
        }

        // ─────────────────────────────────────────────────────────────────────────
        private static bool ConfigureTexture(string path, TextureImporterType type, bool sRgb,
                                             List<string> notes, List<string> fails)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { fails.Add(path + " has no TextureImporter."); return false; }

            bool dirty = false;
            if (ti.textureType != type) { ti.textureType = type; dirty = true; }
            if (ti.sRGBTexture != sRgb) { ti.sRGBTexture = sRgb; dirty = true; }
            if (!ti.mipmapEnabled)      { ti.mipmapEnabled = true; dirty = true; }
            if (dirty) ti.SaveAndReimport();

            var reread = AssetImporter.GetAtPath(path) as TextureImporter;
            if (reread == null || reread.textureType != type)
            {
                fails.Add(path + " could not be typed as " + type + " (still " +
                          (reread != null ? reread.textureType.ToString() : "null") + ").");
                return false;
            }
            return true;
        }

        private static IEnumerable<string> ImagesIn(string dir)
        {
            if (!Directory.Exists(dir)) yield break;
            foreach (string p in Directory.GetFiles(dir))
            {
                string e = Path.GetExtension(p).ToLowerInvariant();
                if (e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".tga" || e == ".psd")
                    yield return p.Replace('\\', '/');
            }
        }

        private static bool HasImage(string dir) => ImagesIn(dir).Any();

        private static string PickImage(string dir, string[] tokens)
        {
            foreach (string p in ImagesIn(dir))
            {
                string lower = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                foreach (string t in tokens) if (lower.Contains(t)) return p;
            }
            return null;
        }
    }
}
