// =============================================================================
// NewBodyAlbedoBinder — give the two "_NEW" enemy bodies a DETERMINISTIC albedo
// binding, so they are textured in edit mode, in a build, and before Start().
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/Art/Bind _NEW enemy body albedo
// Batch: -executeMethod DeNelle.Editor.NewBodyAlbedoBinder.RunBatch
// Marker: NEW_BODY_ALBEDO_OK <bound>/<expected>   |   NEW_BODY_ALBEDO_FAIL <n>
//
// THE DEFECT (measured — EnemyProvingHarness run of 2026-08-20, before-shots kept
// in Builds/EnemyCaps_before/):
//   hollow-brute -> Skeleton_Golem_NEW : TEX slots=1 noMainTex=1
//                   TXTR <no main texture on tripo_mat_af5bea34_Pbr>
//   necromancer  -> Necromancer_NEW    : TEX slots=1 noMainTex=1
//                   TXTR <no main texture on tripo_mat_82fc39ea_Pbr>
// Both PNGs are a pure-white featureless silhouette. These two matter more than
// most: WO-954 swapped TO these bodies precisely because the owner rejected the
// KayKit originals, so the replacements she asked for were the untextured ones.
//
// ── WHERE DOES AN ENEMY'S COLOUR ACTUALLY COME FROM? (the resolution order) ──
// This was the missing piece, and it is why the name-based theory of the bug was
// only half right. For a given model there are THREE sources, in this precedence:
//
//   1. THE IMPORTED MATERIAL ON THE MESH — whatever `externalObjects` (or, absent
//      a remap, the importer's name SEARCH) bound. Present from load, visible in
//      edit mode, survives into the build. This is the only one that is real art
//      guaranteed to match the mesh's own UVs, and it is the one that was empty.
//   2. TripoMaterialFixer's FALLBACK atlas — EnemyFactory.ResolveBasecolor ->
//      SetFallbackTexture("Enemies/TripoTex/<name>_basecolor"). Applies ONLY when
//      the source material has no map. RUNTIME-ONLY (Run() is driven from Start(),
//      which never executes in edit mode) and resolved through a Resources /
//      Addressables ADDRESS whose Resources half no longer exists.
//   3. The family MISS-TINT / EnemyBodyColorGuard — a flat colour painted over a
//      textureless, achromatic body. A deliberate floor, not a look.
//
// ⚠ SO THE NAME-RESOLUTION HALF WAS ALREADY FIXED, AND THIS IS NOT A SECOND COPY.
// EnemyFactory.ResolveBasecolor has stripped a trailing "_NEW" since 91ea3ca95.
// That fix is correct and stays. But it only ever reaches source (2), so the body
// stayed blank for every editor-side observer — the proving harness, the scene
// view, a prefab preview — and its runtime look hung on an address that the
// deletion of Assets/Resources/Enemies had already half-broken. Binding at source
// (1) is what makes the look independent of load path and lifecycle.
//
// ── WHICH ALBEDO, THOUGH? THE ANSWER DIFFERS PER BODY, AND THAT IS THE FINDING ──
// Resolved by looking at what is actually on disk rather than at what the names
// suggest:
//   • Necromancer_NEW.fbx CARRIES ITS OWN EMBEDDED ART. Unity's extraction writes
//     it to Necromancer_NEW.fbm/tripo_mat_82fc39ea_Pbr_Diffuse.jpg (+ _Normal).
//     Its own map, its own UVs — strictly better than the legacy TripoTex atlas,
//     and no UV-mismatch risk at all.
//   • Skeleton_Golem_NEW.fbm IS EMPTY. That FBX embeds no texture, so the legacy
//     TripoTex/Skeleton_Golem_basecolor.jpg is its only candidate, and whether it
//     FITS is a question only a render can answer. See the RESULT note below.
// Hence the rule this binder applies, in order: THE MODEL'S OWN .fbm DIFFUSE
// FIRST, the "_NEW"-stripped TripoTex atlas SECOND. Preferring embedded art is
// general and safe; falling back by stripped name is the two-row case.
//
// ── WHY A PLAIN AddRemap IS NOT ENOUGH (instrumented, not guessed) ──
// The first attempt set externalObjects and reimported, and the harness reported
// the binding had NOT taken: PATH came back as a freshly EXTRACTED
// Materials/tripo_mat_af5bea34_Pbr.mat. TripoAssetPostprocessor.OnPreprocessModel
// forces materialLocation=External + materialName=BasedOnTextureName on every
// Tripo path, and under External the importer IGNORES the remap table and SEARCHES
// the project for a .mat named after the texture. EnemyBodyMaterialFixer already
// documents this exact trap (that search is what bound four AccuRig skeletons to
// one shared Mage diffuse — "never really a material bug; it was a SEARCH bug").
// Its sanctioned opt-out is the ".tripo-extracted" marker file, which makes
// OnPreprocessModel return early. So: write the marker, set InPrefab +
// BasedOnMaterialName to turn the search OFF, THEN remap. Same three steps, same
// order, same reason as EnemyBodyMaterialFixer — deliberately not a new pattern.
//
// ── DOWNSTREAM, DELIBERATELY NO EDITS NEEDED ──
//   • SetFallbackTexture "only fills in when the source has no map", so
//     EnemyFactory's existing call becomes a no-op for these two. No double-apply.
//   • SetMissTint is texture-miss-only, so the family tint stops covering them.
//   • EnemyBodyColorGuard tests textureless+achromatic and now sees a texture, so
//     it stops repainting them — without touching that lane's file.
//
// SCOPE IS TWO ROWS ON PURPOSE. The "_NEW" rule below is general but only two
// models in the project exercise it, and the genuinely-bare orc bodies are being
// imported by another lane right now; a sweep would race that import. The oracle
// that keeps the general case honest is EnemyArtCoverageRegression, not a sweep.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Binds a real albedo onto the "_NEW" enemy meshes, at import time.</summary>
    public static class NewBodyAlbedoBinder
    {
        private const string ContentRoot = DeNelle.Core.AssetRoots.EnemyContent;
        private const string TexRoot     = ContentRoot + "/TripoTex";
        private const string MatRoot     = ContentRoot + "/Materials";
        private const string OkMarker    = "NEW_BODY_ALBEDO_OK";
        private const string FailMarker  = "NEW_BODY_ALBEDO_FAIL";

        /// <summary>TripoAssetPostprocessor.MarkerSuffix — its sanctioned opt-out.</summary>
        private const string TripoMarkerSuffix = ".tripo-extracted";

        /// <summary>The models this binder owns. See the scope note in the header.</summary>
        private static readonly string[] Models = { "Necromancer_NEW", "Skeleton_Golem_NEW" };

        [MenuItem("Defenders/Art/Bind _NEW enemy body albedo")]
        public static void RunMenu() => Run();

        public static void RunBatch()
        {
            int failures = Run();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        /// <summary>Returns the number of models that could not be bound.</summary>
        public static int Run()
        {
            int bound = 0, failed = 0;
            var notes = new List<string>();

            foreach (string model in Models)
            {
                string fbx = ContentRoot + "/" + model + ".fbx";
                if (!File.Exists(fbx))
                {
                    failed++;
                    notes.Add($"{model}: FBX not found at '{fbx}'");
                    continue;
                }

                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer == null)
                {
                    failed++;
                    notes.Add($"{model}: '{fbx}' has no ModelImporter");
                    continue;
                }

                string albedoPath = ResolveAlbedoPath(model, out string how);
                if (string.IsNullOrEmpty(albedoPath))
                {
                    failed++;
                    notes.Add($"{model}: NO albedo anywhere — neither an own '{model}.fbm' diffuse nor " +
                              $"'{TexRoot}/{StripNewSuffix(model)}_basecolor.jpg'");
                    continue;
                }
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                if (albedo == null)
                {
                    failed++;
                    notes.Add($"{model}: albedo '{albedoPath}' exists on disk but would not load as a Texture2D");
                    continue;
                }
                notes.Add($"{model}: albedo = '{albedoPath}' ({how})");

                // ── (1) the postprocessor opt-out, BEFORE any reimport ──────────────
                // Written first, or the reimport below re-runs OnPreprocessModel and
                // clobbers the importer settings straight back to External+ByTexture.
                string marker = fbx + TripoMarkerSuffix;
                if (!File.Exists(marker))
                {
                    File.WriteAllText(marker,
                        "Albedo bound explicitly by NewBodyAlbedoBinder (externalObjects -> a real .mat).\n" +
                        "This marker stops TripoAssetPostprocessor.OnPreprocessModel forcing\n" +
                        "materialLocation=External + materialName=BasedOnTextureName on this FBX. Under\n" +
                        "External the importer IGNORES the remap table and SEARCHES the project for a .mat\n" +
                        "named after the TEXTURE — the same search that bound four AccuRig skeletons to one\n" +
                        "shared diffuse. See EnemyBodyMaterialFixer for the original diagnosis.\n");
                    notes.Add($"{model}: wrote '{marker}' — the postprocessor no longer rewrites this importer");
                }

                // ── (2) enumerate the material NAMES to remap ───────────────────────
                // The name is the SourceAssetIdentifier the remap is keyed on, so it must
                // be read off the asset rather than hardcoded, or a re-export silently
                // stales the key and the binding quietly stops applying.
                List<string> embedded = MaterialNamesFor(fbx, importer, notes, model);
                if (embedded.Count == 0)
                {
                    failed++;
                    notes.Add($"{model}: found NO material name to remap — not as an FBX sub-asset, not in the " +
                              "importer's existing external-object map, and not on any renderer of the imported " +
                              "prefab. Without a name there is no remap key, so nothing can be bound");
                    continue;
                }

                // ── (3) author the material, then turn the SEARCH off and remap ─────
                int remappedHere = 0;
                foreach (string matName in embedded)
                {
                    Material asset = EnsureMaterial(model, matName, albedo, notes);
                    if (asset == null) continue;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), asset);
                    remappedHere++;
                    notes.Add($"{model}: remapped embedded material '{matName}' -> '{AssetDatabase.GetAssetPath(asset)}'");
                }
                if (remappedHere == 0)
                {
                    failed++;
                    notes.Add($"{model}: no material could be remapped");
                    continue;
                }

                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.materialName     = ModelImporterMaterialName.BasedOnMaterialName;
                importer.SaveAndReimport();
                bound++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (string n in notes) Debug.Log("[NewBodyAlbedo]   " + n);

            if (failed > 0)
                Debug.LogError($"{FailMarker} {failed} model(s) could not be bound — see the [NewBodyAlbedo] lines above.");
            else
                Debug.Log($"{OkMarker} {bound}/{Models.Length}");

            return failed;
        }

        /// <summary>
        /// Every material name this FBX needs a remap entry for, gathered from three
        /// places because ANY ONE OF THEM CAN BE EMPTY depending on how the model is
        /// currently configured — and the first attempt at this fix failed for exactly
        /// that reason ("FBX exposes no embedded Material to remap"):
        ///   (a) Material SUB-ASSETS of the FBX. Present only while materialLocation is
        ///       InPrefab. The moment TripoAssetPostprocessor forces External, Unity
        ///       EXTRACTS them to standalone .mat files and this list goes empty — so a
        ///       binder that reads only sub-assets cannot repair the very state the
        ///       postprocessor creates.
        ///   (b) The importer's EXISTING external-object map. Survives the flip to
        ///       External and keeps the original identifiers, which makes it the most
        ///       reliable source once anything has been remapped before.
        ///   (c) The material names on the imported prefab's renderers. The ground
        ///       truth of what the mesh actually asks for, and the only source that
        ///       still works when both of the others are empty.
        /// </summary>
        private static List<string> MaterialNamesFor(string fbx, ModelImporter importer,
                                                     List<string> notes, string model)
        {
            var names = new List<string>();

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var m = obj as Material;
                if (m != null && !names.Contains(m.name)) names.Add(m.name);
            }
            int fromSubAssets = names.Count;

            foreach (var kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type != typeof(Material)) continue;
                if (!names.Contains(kv.Key.name)) names.Add(kv.Key.name);
            }
            int fromMap = names.Count - fromSubAssets;

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (root != null)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        // Unity appends " (Instance)" to a runtime copy; the remap key is
                        // the clean authored name, so a suffixed name would never match.
                        string n = m.name.Replace(" (Instance)", "");
                        if (!names.Contains(n)) names.Add(n);
                    }
                }
            }
            int fromRenderers = names.Count - fromSubAssets - fromMap;

            notes.Add($"{model}: material names = [{string.Join(", ", names)}] " +
                      $"(sub-assets={fromSubAssets}, existing-remap={fromMap}, renderers={fromRenderers})");
            return names;
        }

        /// <summary>
        /// The albedo precedence, in order: the model's OWN extracted .fbm diffuse,
        /// then the "_NEW"-stripped atlas in TripoTex. Own art first is the general
        /// and safe rule — it cannot mismatch the mesh's UVs, because it shipped with
        /// the mesh. Returns null when neither exists.
        /// </summary>
        public static string ResolveAlbedoPath(string model, out string how)
        {
            how = null;
            if (string.IsNullOrEmpty(model)) return null;

            // (a) the model's own embedded art, as Unity extracts it
            string fbmDir = ContentRoot + "/" + model + ".fbm";
            if (Directory.Exists(fbmDir))
            {
                foreach (string f in Directory.GetFiles(fbmDir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".tga") continue;
                    string file = Path.GetFileNameWithoutExtension(f);
                    if (file.IndexOf("Diffuse", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        file.IndexOf("basecolor", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    how = "the model's OWN embedded art — UVs cannot mismatch";
                    return fbmDir + "/" + Path.GetFileName(f);
                }
            }

            // (b) the legacy-named authored atlas. "_NEW" DISAMBIGUATES A MESH FILE,
            // NOT A CHARACTER — the same rule EnemyFactory.ResolveBasecolor applies at
            // runtime, kept identical on purpose so the import-time binding and the
            // runtime fallback can never disagree about which atlas belongs to a body.
            string artName = StripNewSuffix(model);
            string atlas = TexRoot + "/" + artName + "_basecolor.jpg";
            if (File.Exists(atlas))
            {
                how = artName == model
                    ? "the TripoTex atlas of the same name"
                    : $"the TripoTex atlas under the LEGACY name '{artName}' (mesh file carries the _NEW suffix, the art does not)";
                return atlas;
            }
            return null;
        }

        /// <summary>Strip a trailing "_NEW" — mirrors EnemyFactory.ResolveBasecolor.</summary>
        public static string StripNewSuffix(string model)
        {
            if (string.IsNullOrEmpty(model)) return model;
            return model.EndsWith("_NEW", System.StringComparison.Ordinal)
                ? model.Substring(0, model.Length - 4)
                : model;
        }

        /// <summary>Create (or refresh) the URP/Lit material asset that carries the albedo.</summary>
        private static Material EnsureMaterial(string model, string matName, Texture2D albedo, List<string> notes)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[NewBodyAlbedo] URP/Lit shader not found — is the project on URP?");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(MatRoot))
                AssetDatabase.CreateFolder(ContentRoot, "Materials");

            // Named after the MODEL, not after the texture. A texture-named material is
            // exactly what the importer's name search collides on across bodies.
            string path = MatRoot + "/" + model + "_Body.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = false;
            if (mat == null)
            {
                mat = new Material(shader) { name = model + "_Body" };
                AssetDatabase.CreateAsset(mat, path);
                created = true;
            }
            if (mat.shader != shader) mat.shader = shader;

            // Set BOTH slot names. URP renders from _BaseMap, but TripoMaterialFixer's
            // rebuild probes _MainTex FIRST and only then _BaseMap. Writing one and not
            // the other is how a material ends up textured for the renderer and blank
            // for the fixer, which then "helpfully" paints a miss-tint over real art.
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", albedo);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);

            // The normal map, when the model shipped one beside its diffuse. Bound only
            // if the importer already types it as a NormalMap — binding a Default-typed
            // texture to _BumpMap renders a garbled surface, which looks like a mesh bug.
            Texture2D nrm = FindSiblingNormal(albedo);
            if (nrm != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.EnableKeyword("_NORMALMAP");
                notes.Add($"{model}: normal map -> '{AssetDatabase.GetAssetPath(nrm)}'");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            notes.Add($"{model}: {(created ? "created" : "updated")} '{path}' for embedded material '{matName}'");
            return mat;
        }

        /// <summary>
        /// The "_Normal"/"_normal" sibling of a diffuse, but ONLY when the importer has
        /// it typed as a NormalMap. A Default-typed JPEG bound to _BumpMap is decoded as
        /// if it were DXT5nm and renders as scrambled lighting — worse than no normal at
        /// all, and it reads as a broken mesh rather than a wrong import setting.
        /// </summary>
        private static Texture2D FindSiblingNormal(Texture2D albedo)
        {
            string p = AssetDatabase.GetAssetPath(albedo);
            if (string.IsNullOrEmpty(p)) return null;

            string dir = Path.GetDirectoryName(p)?.Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(p);
            string ext = Path.GetExtension(p);
            if (string.IsNullOrEmpty(dir)) return null;

            foreach (string suffix in new[] { "Diffuse", "diffuse", "basecolor", "Basecolor", "BaseColor" })
            {
                if (!stem.EndsWith(suffix, System.StringComparison.Ordinal)) continue;
                string root = stem.Substring(0, stem.Length - suffix.Length);
                foreach (string nrmSuffix in new[] { "Normal", "normal" })
                {
                    string cand = dir + "/" + root + nrmSuffix + ext;
                    if (!File.Exists(cand)) continue;
                    var ti = AssetImporter.GetAtPath(cand) as TextureImporter;
                    if (ti == null || ti.textureType != TextureImporterType.NormalMap) return null;
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(cand);
                }
            }
            return null;
        }
    }
}
