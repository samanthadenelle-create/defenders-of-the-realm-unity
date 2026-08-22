// =============================================================================
// CellarHollowImport — import + BIND the owner's AccuRig "Cellar Hollow" body.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/Art/Cellar Hollow/1 Import + Bind
// Batch: -executeMethod DeNelle.Editor.CellarHollowImport.Run
// Markers: CELLAR_HOLLOW_IMPORT_OK <n> checks | CELLAR_HOLLOW_IMPORT_FAIL <n>
// The rig/animation/render + texture-fork proof is the SIBLING file
// CellarHollowProof.cs (-executeMethod DeNelle.Editor.CellarHollowProof.Run).
//
// ── THE NAME, AND WHY IT IS NOT "cellar hollow" ──────────────────────────────
// The delivery ships as "cellar hollow.fbx" — WITH A SPACE. No other body under
// Assets/EnemyContent carries one, the roster's model keys are Skeleton_Warrior /
// Necromancer_NEW / Orc_Mage, and this project has ~111 hand-typed asset-path
// literals (WO-1129) that a space silently breaks. Imported as "Cellar_Hollow":
// the minimum transform of the owner's own name (space -> underscore, Pascal case),
// which keeps it recognisable as the delivery AND matches enemies.json's
// displayName "Cellar Hollow" for the id "cellar-hollow". It is deliberately NOT
// "Hollow_Cellar": inverting the character's name to force a family prefix would
// make the model key stop matching the thing the owner named.
// Consequence to honour: EnemyFactory.TryBasecolor probes
// "Enemies/TripoTex/<model>_basecolor" BY MODEL NAME, so any atlas fallback for this
// body must be named Cellar_Hollow_basecolor. This script does not rely on that tier —
// it binds TIER 1 (a real material on the mesh, from this body's own .fbm) — but the
// name is chosen so the fallback tier would resolve rather than silently miss, which
// is exactly what left Necromancer_NEW untextured.
//
// ── WHY A SCRIPT AND NOT A HAND-EDITED .meta ─────────────────────────────────
// docs/MASTER_CATALOG/resources-art.md §3b documents a trap already paid for three
// times today: TripoAssetPostprocessor.OnPreprocessModel claims EVERY FBX under
// Assets/EnemyContent and, absent a "<Body>.fbx.tripo-extracted" sentinel, force-sets
// materialLocation = External (legacy) + materialName = BasedOnTextureName +
// materialSearch = RecursiveUp on EVERY import. Legacy External IGNORES the .meta
// externalObjects remap table and resolves a body's material by SEARCHING the project
// for a .mat named after the TEXTURE — which is how seven enemy ids came to wear one
// skeleton's UV layout. A hand-written .meta is therefore NOT a fix; the next reimport
// rewrites it. The fix is three parts applied together, and this script applies all
// three deterministically:
//   (1) the sentinel (the postprocessor's own opt-out),
//   (2) materialLocation = InPrefab,
//   (3) an explicit per-body .mat remap bound to this body's OWN Cellar_Hollow.fbm art.
// Any TWO of the three still render another body's texture.
//
// ── THE TEXTURE FORK, AND WHY THE .fbm WINS ──────────────────────────────────
// The delivery carries two atlases for one creature:
//   rigged   "cellar hollow.fbm/tripo_mat_acabe1ac_Pbr_Diffuse.jpg"   (179,783 B)
//   unrigged "tripo_convert_*.fbm/cellar_hollow_basecolor.JPEG"       (112,608 B)
// Different md5, so they are NOT the same file — but they are the same BAKE (proved
// by eye before import, and re-proved on the mesh by CellarHollowProof's A/B render:
// identical island layout, identical content, different JPEG encode). Either would
// register; the embedded one is bound because it is the atlas the RIGGED mesh was
// exported with, it carries Tripo's per-model hash so it cannot collide by NAME with
// the Material_Pbr_Diffuse family, and extracting it into "<Body>.fbm/" is precisely
// what EnemyBodyTextureRegression rule (B) requires. Guessing by filename is what
// scrambled the seven skeletons and the two orcs — so the picture decides, not the name.
//
// ⚠ NORMAL MAPS. A JPEG bound to _BumpMap while the importer still types it Default is
// decoded as if it were DXT5nm and renders as scrambled lighting — which reads as a
// broken MESH rather than a wrong import setting. Seven pre-existing TripoTex normals
// are wrong this way. This script sets textureType = NormalMap BEFORE binding and
// REFUSES to bind a normal it could not type (it logs and leaves _BumpMap empty).
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>One-shot importer + binder for the owner's Cellar Hollow AccuRig body.</summary>
    public static class CellarHollowImport
    {
        public const string Model = "Cellar_Hollow";

        private const string ContentRoot = DeNelle.Core.AssetRoots.EnemyContent;
        private const string FbxPath     = ContentRoot + "/" + Model + ".fbx";
        private const string FbmDir      = ContentRoot + "/" + Model + ".fbm";
        private const string MatRoot     = ContentRoot + "/Materials";
        private const string MatPath     = MatRoot + "/" + Model + "_Body.mat";
        private const string Sentinel    = FbxPath + ".tripo-extracted";

        private const string OkMarker   = "CELLAR_HOLLOW_IMPORT_OK";
        private const string FailMarker = "CELLAR_HOLLOW_IMPORT_FAIL";

        // ─────────────────────────────────────────────────────────────────────────
        [MenuItem("Defenders/Art/Cellar Hollow/1 Import + Bind")]
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

            foreach (string n in notes) Debug.Log("[CellarHollow] " + n);

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
                fails.Add("no FBX at " + FbxPath + " — copy the owner's 'cellar hollow.fbx' there first " +
                          "(space-free name; see this file's header for why).");
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
            // Skeleton_Warrior.fbx.meta (read at source): animationType 3 (Human),
            // avatarSetup 1 (CreateFromThisModel), autoGenerateAvatarMappingIfUnspecified 1,
            // bakeAxisConversion 1. This FBX carries the SAME CC_Base bone naming
            // (189 CC_Base bones / 94 clusters / 1 skin / 4 AnimStacks, read out of the
            // binary before import), so the same settings apply.
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

            // ── this body's OWN art in <Body>.fbm ─────────────────────────────────
            // ⚠ MEASURED, NOT ASSUMED (first run, 2026-08-20): ExtractTextures returned TRUE
            // and NOTHING landed. This AccuRig delivery does NOT embed its media the way the
            // Tripo bodies do — it ships the two maps as LOOSE FILES in a sibling
            // "cellar hollow.fbm/" folder beside the FBX, which the FBX references by relative
            // path. A "True" from ExtractTextures therefore proves nothing here; the only
            // evidence is an image actually being in the folder, which is what is asserted
            // below. The delivery's own .fbm contents are staged into <Body>.fbm alongside the
            // renamed FBX, keeping the hashed names (they cannot collide with the
            // Material_Pbr_Diffuse family) and satisfying EnemyBodyTextureRegression rule (B).
            if (!HasImage(FbmDir))
            {
                importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
                bool extracted = importer != null && importer.ExtractTextures(FbmDir);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                notes.Add("ExtractTextures(" + FbmDir + ") -> " + extracted);
            }
            if (!HasImage(FbmDir))
            {
                fails.Add("no image in " + FbmDir + " — neither embedded extraction nor the staged sibling " +
                          "maps put art there, so there is no own-.fbm art to bind (tier 1 impossible).");
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
            if (smrs.Length == 0) fails.Add("no SkinnedMeshRenderer — the body did not import as a skinned mesh.");

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
            notes.Add("typed " + path + " as " + type + " (sRGB=" + sRgb + ")");
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

        /// <summary>First image in <paramref name="dir"/> whose file name contains any token.</summary>
        private static string PickImage(string dir, string[] tokens)
        {
            foreach (string p in ImagesIn(dir))
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                foreach (string t in tokens)
                    if (n.Contains(t)) return p;
            }
            return null;
        }
    }
}
