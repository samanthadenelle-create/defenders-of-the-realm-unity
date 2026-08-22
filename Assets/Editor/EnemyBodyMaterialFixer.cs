// =============================================================================
// EnemyBodyMaterialFixer — one-shot importer repair for the AccuRig skeleton bodies.
//
// THE PROVEN DEFECT (captured by EnemyProvingHarness.RunBatch, Builds/EnemyCaps/_summary.txt):
//   7 enemy ids rendered with ONE texture —
//     Assets/EnemyContent/Skeleton_Mage.fbm/Material_Pbr_Diffuse.png
//   [hollow-warrior, hollow-rogue, hollow-acolyte, hollow-mage, hollow-reaper,
//    hollow-apprentice, orc-raider]
//   Only the two Mage-bodied ids were correct — it is the Mage's texture. The rest
//   render as the Mage's UV layout stretched over a different mesh (white/tan patches).
//
// ROOT CAUSE — INSTRUMENTED, NOT INFERRED (Builds/enemy-body-dump.log dumped every
// renderer -> material asset -> base map for all four bodies; the first two theories
// below were what the .meta text SUGGESTED, and the dump disproved both):
//   * TripoAssetPostprocessor.OnPreprocessModel watches AssetRoots.EnemyContent and, for
//     any FBX without a ".tripo-extracted" marker, FORCE-SETS on EVERY import:
//         materialLocation = External (legacy)   <- turns the remap table OFF
//         materialName     = BasedOnTextureName  <- identity is the TEXTURE's name
//         materialSearch   = RecursiveUp         <- resolves by searching the project
//   * Every AccuRig skeleton body names its material "Material_Pbr" and its diffuse
//     "Material_Pbr_Diffuse". So the search collapsed ALL FOUR bodies onto whichever
//     single project material carried that name — Materials/Material_Pbr.mat (holding
//     the MAGE's diffuse, guid bc721785eb19efe4dbdd379f136a7e68). Seven ids, one texture.
//   * The externalObjects remaps in the .meta were INERT: legacy External mode ignores
//     them. Rewriting them (EnemyMaterialRemap / SearchAndRemapMaterials) could not fix
//     this, and merely repointing them moved the collision instead of dissolving it —
//     observed live: all seven flipped to the WARRIOR's texture and the Mage BROKE.
//   * Tripo bodies are untouched by the same postprocessor because their textures carry
//     unique hashed names (tripo_mat_<hash>_Pbr_Diffuse) — nothing to collide with.
//     That is why the Orc/Troll set is the working precedent.
//   * Warrior and Rogue each ALREADY ship their own diffuse in their own .fbm folder,
//     unused. Skeleton_Healer had no .fbm at all, but its FBX does carry an embedded
//     PNG (verified: one PNG signature in the binary) that was never extracted.
//
// THE FIX (shape chosen to MATCH THE WORKING PRECEDENT, not to invent one):
//   The Orc/Troll bodies — which are NOT affected — are wired as
//   "per-body .mat asset + an externalObjects remap in the fbx.meta"
//   (e.g. Orc_Shaman.fbx.meta remaps tripo_mat_79fc0b70 -> Assets/EnemyContent/Orc_Shaman.mat).
//   So: give each skeleton body its OWN material bound to its OWN diffuse, and remap
//   the fbx importer at it.
//
//   ADD, never MUTATE: the shared Materials/Material_Pbr.mat is an ADDRESSABLE entry
//   (Assets/AddressableAssetsData/AssetGroups/Enemy_Art.asset, address
//   "Enemies/Materials/Material_Pbr"). Its content is CORRECT for the Mage, so it stays
//   exactly as it is and becomes the Mage's explicit material. Warrior / Rogue / Healer
//   get NEW sibling materials copied from it (same URP/Lit shader, same transparent +
//   alpha-premultiply keywords) with only the base map swapped.
//
//   The Mage also gets an EXPLICIT remap. It resolved correctly today only by
//   materialSearch coincidence — the exact mechanism that mis-wired the Healer.
//
//   AND the remap is only AUTHORITATIVE once the postprocessor stops overwriting the
//   importer, so each body also gets the postprocessor's own opt-out marker
//   (<Body>.fbx.tripo-extracted) plus materialLocation = InPrefab. Marker, location and
//   remap are ONE fix — any two of the three still render another body's texture.
//
// Batch entry:  DeNelle.Editor.EnemyBodyMaterialFixer.Run
// Marker:       ENEMY_BODY_MATERIAL_FIX_OK <n>/<n>   |   ENEMY_BODY_MATERIAL_FIX_FAIL <n>
// Prove after:  DeNelle.Editor.EnemyProvingHarness.RunBatch  (the PICTURE is the evidence)
//               DeNelle.Editor.EnemyBodyTextureRegression.RunAll (the standing guard)
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class EnemyBodyMaterialFixer
    {
        private const string EnemyContent = DeNelle.Core.AssetRoots.EnemyContent;
        private const string MaterialsDir = EnemyContent + "/Materials";

        /// <summary>The FBX material name every AccuRig skeleton body carries (read out of the FBX binaries).</summary>
        private const string BodyMaterialName = "Material_Pbr";

        /// <summary>The diffuse file name AccuRig embeds in every one of these FBXs.</summary>
        private const string DiffuseFileName = "Material_Pbr_Diffuse.png";

        /// <summary>
        /// The four AccuRig skeleton bodies that share the mis-wired material, and the
        /// material asset each one SHOULD own. The Mage keeps the pre-existing shared
        /// asset (it is his texture, and it is an addressable entry — do not orphan it).
        /// </summary>
        private static readonly (string body, string materialPath)[] Bodies =
        {
            ("Skeleton_Warrior", MaterialsDir + "/Skeleton_Warrior.mat"),
            ("Skeleton_Rogue",   MaterialsDir + "/Skeleton_Rogue.mat"),
            ("Skeleton_Healer",  MaterialsDir + "/Skeleton_Healer.mat"),
            ("Skeleton_Mage",    MaterialsDir + "/Material_Pbr.mat"),
        };

        /// <summary>The template every new per-body material is copied from (shader + surface keywords).</summary>
        private const string TemplateMaterial = MaterialsDir + "/Material_Pbr.mat";

        [MenuItem("Defenders/Art/Fix Enemy Body Materials")]
        public static void Run()
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ENEMY BODY MATERIAL FIX (per-body material bound to its own .fbm diffuse) ---");

            int done = 0;
            foreach (var (body, materialPath) in Bodies)
            {
                string fbx = $"{EnemyContent}/{body}.fbx";
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer == null)
                {
                    failures.Add($"{body}: no ModelImporter at '{fbx}' — body missing from the tree.");
                    continue;
                }

                // ── (1) make sure the body's OWN diffuse exists on disk ──────────────
                string fbm = $"{EnemyContent}/{body}.fbm";
                string texPath = $"{fbm}/{DiffuseFileName}";
                if (!File.Exists(texPath))
                {
                    log.AppendLine($"{body}: no own diffuse at '{texPath}' — extracting embedded textures.");
                    if (!Directory.Exists(fbm)) Directory.CreateDirectory(fbm);
                    importer.ExtractTextures(fbm);
                    AssetDatabase.Refresh();
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    failures.Add($"{body}: own diffuse still ABSENT at '{texPath}' after ExtractTextures — " +
                                 "cannot bind a body-correct base map; this body has no art of its own to point at.");
                    continue;
                }

                // ── (2) make sure the body's OWN material exists ─────────────────────
                var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (mat == null)
                {
                    if (!AssetDatabase.CopyAsset(TemplateMaterial, materialPath))
                    {
                        failures.Add($"{body}: could not copy '{TemplateMaterial}' -> '{materialPath}'.");
                        continue;
                    }
                    AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
                    mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                }
                if (mat == null)
                {
                    failures.Add($"{body}: material '{materialPath}' would not load after copy.");
                    continue;
                }

                // ── (3) bind the body's own diffuse (both URP and legacy slot names) ──
                bool changed = false;
                if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != tex) { mat.SetTexture("_BaseMap", tex); changed = true; }
                if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != tex) { mat.SetTexture("_MainTex", tex); changed = true; }
                if (changed)
                {
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssetIfDirty(mat);
                }
                log.AppendLine($"{body}: material '{materialPath}' baseMap -> '{texPath}' (rebound={changed})");

                // ── (4) point the importer at it — explicit remap, never a name SEARCH ──
                //
                // INSTRUMENTED, NOT GUESSED (Builds/enemy-body-dump.log): with
                // materialLocation = External (legacy, 0) the importer IGNORES the remap
                // table and SEARCHES the project for a .mat named after the material
                // (materialName 1) or after the TEXTURE (materialName 0). Every one of
                // these AccuRig bodies names its material 'Material_Pbr' and its texture
                // 'Material_Pbr_Diffuse', so ALL FOUR landed on whichever single project
                // material carried that name — first Materials/Material_Pbr.mat (the Mage's
                // texture), then Materials/Material_Pbr_Diffuse.mat (the Warrior's). The
                // shared texture was never really a material bug; it was a SEARCH bug.
                //
                // InPrefab (Use Embedded Materials) turns the search OFF and makes the
                // externalObjects remap authoritative — which is exactly how the unaffected
                // Orc bodies are wired (Orc_Shaman.fbx.meta: materialLocation 1 + a remap).
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), BodyMaterialName);
                var current = importer.GetExternalObjectMap();
                current.TryGetValue(id, out var currentObj);
                // The importer settings only STICK once TripoAssetPostprocessor stops
                // rewriting them on every import. Its own sanctioned opt-out is the
                // ".tripo-extracted" marker (OnPreprocessModel returns early when it
                // exists), and the claim it makes is TRUE for these four — their embedded
                // media is extracted, in their own .fbm folders. Written BEFORE the
                // reimport below, or the reimport clobbers the settings again.
                string marker = fbx + ".tripo-extracted";
                if (!File.Exists(marker))
                {
                    File.WriteAllText(marker,
                        "Tripo textures extracted (Unity's own <Body>.fbm extraction). This marker also " +
                        "stops TripoAssetPostprocessor.OnPreprocessModel from forcing materialLocation=" +
                        "External + materialName=BasedOnTextureName on this FBX. That search-by-texture-" +
                        "name is what bound every AccuRig skeleton body (all of whose diffuses are named " +
                        "Material_Pbr_Diffuse) to ONE shared project material. See EnemyBodyMaterialFixer.");
                    log.AppendLine($"{body}: wrote '{marker}' — the postprocessor no longer rewrites this importer.");
                }

                bool remapOk = ReferenceEquals(currentObj, mat);
                bool locationOk = importer.materialLocation == ModelImporterMaterialLocation.InPrefab;
                bool nameOk = importer.materialName == ModelImporterMaterialName.BasedOnMaterialName;
                if (!remapOk || !locationOk || !nameOk)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    importer.AddRemap(id, mat);
                    importer.SaveAndReimport();
                    log.AppendLine($"{body}: materialLocation=InPrefab, remapped '{BodyMaterialName}' -> '{materialPath}'");
                }
                else
                {
                    log.AppendLine($"{body}: importer already correct (InPrefab + remap).");
                }

                done++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── (5) read the result back off the REAL imported models ────────────────
            log.AppendLine("--- READBACK (what the imported model actually renders with) ---");
            var seen = new Dictionary<string, string>();
            foreach (var (body, _) in Bodies)
            {
                string fbx = $"{EnemyContent}/{body}.fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (go == null) { failures.Add($"{body}: model asset would not load for readback."); continue; }

                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) { log.AppendLine($"{body}: renderer '{r.name}' has a NULL material slot"); continue; }
                        var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                        string mp = AssetDatabase.GetAssetPath(m);
                        string tp = t == null ? "<none>" : AssetDatabase.GetAssetPath(t);
                        log.AppendLine($"{body}: renderer '{r.name}' mat '{m.name}' @ '{mp}' -> baseMap '{tp}'");
                        if (t == null) continue;
                        if (!tp.Contains($"{body}.fbm/") && !tp.Contains($"{body}.fbm\\")) continue;
                        if (!seen.ContainsKey(body)) seen[body] = tp;
                    }
                }
                if (!seen.ContainsKey(body))
                    failures.Add($"{body}: after the fix NO renderer binds a base map from '{body}.fbm/' — the remap did not take.");
            }

            Debug.Log(log.ToString());
            if (failures.Count > 0)
            {
                foreach (var f in failures) Debug.LogError("[enemy-body-material] " + f);
                Debug.Log($"ENEMY_BODY_MATERIAL_FIX_FAIL {failures.Count} defect(s)");
                return;
            }
            Debug.Log($"ENEMY_BODY_MATERIAL_FIX_OK {done}/{Bodies.Length}");
        }
    }
}
