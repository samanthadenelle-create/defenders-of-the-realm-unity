// =============================================================================
// TripoEnemyMaterialExtractor — extract the EMBEDDED textures/materials from a Tripo
// enemy FBX so URP can render it (fixes the "wight" = Demon/OgreMage magenta/untextured).
// -----------------------------------------------------------------------------
// ROOT CAUSE (owner playtest 2026-06-13, confirmed from the .meta): Demon.fbx and
// OgreMage.fbx import with `externalObjects: {}` — their embedded textures were never
// extracted, so the auto-generated material has no _BaseMap and the runtime
// TripoMaterialFixer has nothing to apply → magenta/untextured. The WORKING Tripo
// enemies (Orc_*.fbx) have a POPULATED externalObjects (extracted textures + remapped
// material). This tool brings Demon/OgreMage to the same state: pull the embedded
// textures into a sibling .fbm folder + reimport so the material links _BaseMap, then
// the existing FixTripoMaterials runtime path (EnemyFactory) renders it in URP.
//
// Reusable: ExtractFor(path) works for ANY Tripo FBX that imported with empty
// externalObjects. Run headless: DeNelle.Editor.TripoEnemyMaterialExtractor.ExtractWights.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TripoEnemyMaterialExtractor
    {
        private static readonly string[] WightFbx =
        {
            "Assets/Resources/Enemies/Demon.fbx",
            "Assets/Resources/Enemies/OgreMage.fbx",
            "Assets/Resources/Enemies/Troll.fbx",   // 2026-06-13: untextured (externalObjects:{}) — featured in the brute wave
        };

        [MenuItem("Defenders/Art/Extract Wight (Demon+OgreMage) Tripo Textures")]
        public static void ExtractWights()
        {
            int ok = 0;
            foreach (var p in WightFbx)
                if (ExtractFor(p)) ok++;
            // PERSIST: ExtractTextures + the reimport only modify the in-memory AssetDatabase;
            // without SaveAssets the externalObjects remap is dropped to the .meta on disk so the
            // NEXT Unity session (the player build) reimports the FBX raw → magenta again (Troll
            // regressed exactly this way 2026-06-13). SaveAssets flushes the .meta now.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TripoExtract] DONE — extracted {ok}/{WightFbx.Length} (saved). TRIPO_EXTRACT_OK");
        }

        /// <summary>
        /// ANIMATION FIX (owner priority 2026-06-13): the new Tripo enemies imported as
        /// animationType=Generic (2), so the SHARED humanoid controllers (LargeEnemy etc.)
        /// can't retarget onto them → they T-pose / slide. They DO have a skeleton
        /// (skeletonHasParents:1), so convert to Humanoid + build an avatar; if Unity can
        /// map the rig the walk/attack/death cycles retarget on. If an avatar does NOT build
        /// (isHuman=false), the skeleton isn't humanoid-mappable → owner runs it through AccuRIG.
        /// </summary>
        [MenuItem("Defenders/Art/Set Wight Enemies to Humanoid (animate via shared controller)")]
        public static void ConvertWightsToHumanoid()
        {
            foreach (var p in WightFbx)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { Debug.LogWarning($"[Humanoid] no ModelImporter at '{p}'."); continue; }

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();

                string name = Path.GetFileNameWithoutExtension(p);
                bool foundAvatar = false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (o is Avatar av)
                    {
                        foundAvatar = true;
                        Debug.Log($"[Humanoid] {name}: avatar built isValid={av.isValid} isHuman={av.isHuman} " +
                                  $"{(av.isValid && av.isHuman ? "-> WILL ANIMATE via the shared humanoid controller" : "-> avatar invalid, needs AccuRIG")}");
                    }
                }
                if (!foundAvatar)
                    Debug.LogWarning($"[Humanoid] {name}: NO avatar built — skeleton not humanoid-mappable; run it through AccuRIG.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Humanoid] DONE — HUMANOID_CONVERT_OK");
        }

        /// <summary>
        /// Revert the enemies whose skeleton is NOT humanoid-mappable (avatar isHuman=False)
        /// back to Generic, so they sit in a clean state (plain T-pose, no broken-humanoid
        /// retarget warnings) until the owner re-exports an AccuRIG'd (CC_Base) version that
        /// Unity CAN map to Humanoid. Troll mapped fine and is left Humanoid.
        /// </summary>
        [MenuItem("Defenders/Art/Revert Unmappable Wights (Demon+OgreMage) to Generic (await AccuRIG)")]
        public static void RevertUnmappableToGeneric()
        {
            string[] unmappable =
            {
                "Assets/Resources/Enemies/Demon.fbx",
                "Assets/Resources/Enemies/OgreMage.fbx",
            };
            foreach (var p in unmappable)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.SaveAndReimport();
                Debug.Log($"[Humanoid] reverted {Path.GetFileNameWithoutExtension(p)} -> Generic (await AccuRIG).");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Humanoid] REVERT DONE — REVERT_GENERIC_OK");
        }

        /// <summary>
        /// Ground-truth test: import Assets/Resources/Enemies/_WightCheck.fbx as Humanoid and
        /// report whether Unity can build a HUMAN avatar (isHuman). True = the rig is
        /// humanoid-mappable (AccuRIG worked) → it will animate on the shared controller.
        /// False = still a non-humanoid (raw Tripo) skeleton → needs a real AccuRIG pass.
        /// </summary>
        [MenuItem("Defenders/Art/Check _WightCheck.fbx is Humanoid (AccuRIG verify)")]
        public static void CheckWightHumanoid()
        {
            const string p = "Assets/Resources/Enemies/_WightCheck.fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[WightCheck] no ModelImporter at '{p}'."); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
            bool found = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Avatar av)
                {
                    found = true;
                    Debug.Log($"[WightCheck] avatar isValid={av.isValid} isHuman={av.isHuman} " +
                              $"{(av.isValid && av.isHuman ? "-> ACCURIG OK, WILL ANIMATE" : "-> NOT humanoid-mappable, needs real AccuRIG")}");
                }
            if (!found) Debug.LogWarning("[WightCheck] no avatar built — definitely not humanoid.");
            Debug.Log("[WightCheck] DONE — WIGHT_CHECK_OK");
        }

        /// <summary>
        /// Finalize the AccuRIG'd wight at Demon.fbx: import Humanoid (so it animates on the
        /// shared LargeEnemy controller), verify the avatar, and cap its basecolor to 1024 for
        /// the mobile/Seeker target. Run after copying the AccuRIG FBX + .fbm over Demon.
        /// </summary>
        [MenuItem("Defenders/Art/Finalize Wight (Demon) — Humanoid + 1024 cap")]
        public static void FinalizeDemonWight()
        {
            const string p = "Assets/Resources/Enemies/Demon.fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Finalize] no ModelImporter for Demon.fbx"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Avatar av)
                    Debug.Log($"[Finalize] Demon avatar isValid={av.isValid} isHuman={av.isHuman} " +
                              $"{(av.isValid && av.isHuman ? "-> ANIMATES" : "-> still not humanoid")}");

            // Cap the wight's textures to 1024 (mobile/Seeker memory).
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D",
                         new[] { "Assets/Resources/Enemies/Demon.fbm" }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(tp) is TextureImporter ti && ti.maxTextureSize > 1024)
                {
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                    Debug.Log($"[Finalize] capped {tp} -> 1024");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Finalize] DONE — FINALIZE_DEMON_OK");
        }

        /// <summary>
        /// Finalize the AccuRIG'd OgreMage: Humanoid + extract its EMBEDDED textures (its export
        /// shipped a 13.8MB FBX with embedded textures, empty .fbm) + cap to 1024 + verify isHuman.
        /// </summary>
        [MenuItem("Defenders/Art/Finalize OgreMage — Humanoid + extract + 1024 cap")]
        public static void FinalizeOgreMage()
        {
            const string p = "Assets/Resources/Enemies/OgreMage.fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Finalize] no ModelImporter for OgreMage.fbx"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();

            // textures are embedded → pull them into OgreMage.fbm so the material links _BaseMap.
            ExtractFor(p);

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Avatar av)
                    Debug.Log($"[Finalize] OgreMage avatar isValid={av.isValid} isHuman={av.isHuman} " +
                              $"{(av.isValid && av.isHuman ? "-> ANIMATES" : "-> still not humanoid")}");

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D",
                         new[] { "Assets/Resources/Enemies/OgreMage.fbm" }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(tp) is TextureImporter ti && ti.maxTextureSize > 1024)
                {
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                    Debug.Log($"[Finalize] capped {tp} -> 1024");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Finalize] DONE — FINALIZE_OGREMAGE_OK");
        }

        // =====================================================================
        // GREEN ORC_SHAMAN (QA-proven 2026-07-11): Player.log 97402/140579/232065
        // '[Flow:EnemyVisual] Material on Orc_Shaman: NO renderer/material (would render
        // blank/fallback)'. Ground truth from the metas: Orc_Shaman.fbx.meta carries an
        // externalObjects remap  tripo_mat_79fc0b70 -> guid 8c8396fdda11f8141933ef6893cdfef7
        // and NO asset with that guid exists anywhere in the project (the .mat was
        // deleted/never committed) — so the material slot resolves NULL and Unity renders
        // the unlit fallback (VisualFactory.cs:194-202). Orc_Berserker's remap target
        // (Orc_Berserker.mat) EXISTS — that is the healthy reference state.
        //
        // Fix = the proven f23d05ae single-asset extract+remap arch (ArcaneSpire_1 white
        // fix): extract the FBX-embedded diffuse to a real texture, author a URP/Lit
        // material from it, wire it via ModelImporter.AddRemap using the EXACT source
        // material name from importer.GetExternalObjectMap() / the imported material list,
        // SaveAndReimport, then VERIFY the remap took (externalObjects count + every
        // renderer slot bound).
        // Run headless: -executeMethod DeNelle.Editor.TripoEnemyMaterialExtractor.RepairOrcShaman
        // =====================================================================
        [MenuItem("Defenders/Art/Repair Orc_Shaman Material (green fix)")]
        public static void RepairOrcShaman()
        {
            bool ok = RepairNullMaterialSlot("Assets/Resources/Enemies/Orc_Shaman.fbx");

            // Same-class sweep (verify-only): report BOUND / MIS-BOUND for every other
            // orc + Tripo enemy FBX so the fix run names any sibling with the same rot.
            AuditEnemyMaterialBindings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TripoExtract] Orc_Shaman repair {(ok ? "SUCCEEDED" : "FAILED")} — " +
                      (ok ? "ORC_SHAMAN_REPAIR_OK" : "ORC_SHAMAN_REPAIR_FAIL"));
        }

        // Same dangling-remap class as the shaman, caught by the audit on the repair run
        // ('[MatAudit] Orc_Necromancer: MIS-BOUND — nullSlots=1/1, staleRemaps=[tripo_mat_1cd34ada]').
        // Run headless: -executeMethod DeNelle.Editor.TripoEnemyMaterialExtractor.RepairOrcNecromancer
        [MenuItem("Defenders/Art/Repair Orc_Necromancer Material (green fix)")]
        public static void RepairOrcNecromancer()
        {
            bool ok = RepairNullMaterialSlot("Assets/Resources/Enemies/Orc_Necromancer.fbx");
            AuditEnemyMaterialBindings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TripoExtract] Orc_Necromancer repair {(ok ? "SUCCEEDED" : "FAILED")} — " +
                      (ok ? "ORC_NECRO_REPAIR_OK" : "ORC_NECRO_REPAIR_FAIL"));
        }

        /// <summary>
        /// Repair ONE FBX whose externalObjects material remap points at a missing .mat
        /// (null slot → unlit fallback). Extract embedded textures → author a URP/Lit
        /// material next to the FBX → AddRemap with the exact source-material name →
        /// SaveAndReimport → verify every renderer slot bound. Returns true if verified.
        /// </summary>
        public static bool RepairNullMaterialSlot(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[TripoExtract] Repair: no ModelImporter at '{fbxPath}' — skipped.");
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(fbxPath);

            // 1) EXACT source-material names, from the importer's own map first (the meta
            //    already names 'tripo_mat_79fc0b70' for the Shaman); fall back to the
            //    imported Material sub-assets if the map carries no material entries.
            var srcNames = new System.Collections.Generic.List<string>();
            foreach (var kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type == typeof(Material) && !srcNames.Contains(kv.Key.name))
                    srcNames.Add(kv.Key.name);
            }
            if (srcNames.Count == 0)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                    if (o is Material m && !srcNames.Contains(m.name))
                        srcNames.Add(m.name);
            }
            if (srcNames.Count == 0)
            {
                Debug.LogWarning($"[TripoExtract] Repair {baseName}: no source material names found " +
                                 "(no map entries, no imported materials) — cannot remap.");
                return false;
            }
            Debug.Log($"[TripoExtract] Repair {baseName}: source material name(s) = " +
                      string.Join(", ", srcNames));

            // 2) Extract the embedded textures to the sibling .fbm (idempotent; the 15MB
            //    Shaman FBX embeds its PBR set) so the albedo exists as a real asset.
            ExtractFor(fbxPath);

            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string fbm = $"{dir}/{baseName}.fbm";

            // 3) Pick the diffuse/albedo from the extracted set (prefer basecolor-style
            //    names; any extracted texture beats none).
            Texture2D albedo = null, normal = null;
            if (AssetDatabase.IsValidFolder(fbm))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { fbm }))
                {
                    string tp = AssetDatabase.GUIDToAssetPath(guid);
                    string tn = Path.GetFileNameWithoutExtension(tp).ToLowerInvariant();
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                    if (tex == null) continue;
                    if (tn.Contains("basecolor") || tn.Contains("base_color") ||
                        tn.Contains("albedo") || tn.Contains("diffuse") || tn.Contains("color"))
                    {
                        if (albedo == null) albedo = tex;
                    }
                    else if (tn.Contains("normal"))
                    {
                        if (normal == null) normal = tex;
                    }
                    else if (albedo == null)
                    {
                        albedo = tex; // last-resort candidate; a named basecolor overrides it
                    }
                }
            }
            if (albedo == null)
                Debug.LogWarning($"[TripoExtract] Repair {baseName}: no extracted albedo found under " +
                                 $"'{fbm}' — authoring a plain URP/Lit material (untextured but LIT, " +
                                 "kills the unlit-green fallback).");

            // 4) Author (or refresh) the URP/Lit material next to the FBX — same shape as
            //    the healthy Orc_Berserker.mat the Berserker remap points at.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogWarning("[TripoExtract] Repair: 'Universal Render Pipeline/Lit' shader not found — abort.");
                return false;
            }
            string matPath = $"{dir}/{baseName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = lit;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (albedo != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);

            // 5) Rewire the remap: drop the stale (dangling-guid) entry, AddRemap the same
            //    EXACT source name to the authored material, and SAVE so the meta carries
            //    the binding durably (the ArcaneSpire_1 persistence lesson).
            foreach (var name in srcNames)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                importer.RemoveRemap(id);
                importer.AddRemap(id, mat);
            }
            importer.SaveAndReimport();

            // 6) VERIFY the remap took: externalObjects entries resolve + every renderer
            //    slot on the imported model is bound.
            int mapped = 0, dangling = 0;
            foreach (var kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type != typeof(Material)) continue;
                if (kv.Value != null) mapped++; else dangling++;
            }
            int slots = 0, nullSlots = 0;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (go != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var sm in r.sharedMaterials)
                    {
                        slots++;
                        if (sm == null) nullSlots++;
                    }
            }
            bool bound = mapped > 0 && dangling == 0 && slots > 0 && nullSlots == 0;
            Debug.Log($"[TripoExtract] Repair {baseName}: VERIFY externalObjects mapped={mapped} " +
                      $"dangling={dangling}, renderer slots bound={slots - nullSlots}/{slots} -> " +
                      $"{(bound ? "BOUND ok" : "MIS-BOUND")} (mat='{matPath}' albedo=" +
                      $"{(albedo != null ? albedo.name : "<none>")})");
            return bound;
        }

        /// <summary>
        /// VERIFY-ONLY audit of every Resources/Enemies FBX (the orc family + the Tripo
        /// brutes + skeletons) for the null-material-slot class: an externalObjects remap
        /// whose target asset no longer exists, or a renderer slot that resolves null —
        /// either renders Unity's unlit fallback. One line per model: BOUND ok / MIS-BOUND.
        /// Run headless: -executeMethod DeNelle.Editor.TripoEnemyMaterialExtractor.AuditEnemyMaterialBindings
        /// </summary>
        [MenuItem("Defenders/Art/Audit Enemy Material Bindings (null-slot scan)")]
        public static void AuditEnemyMaterialBindings()
        {
            const string enemyDir = "Assets/Resources/Enemies";
            int misBound = 0, total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { enemyDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                total++;
                string name = Path.GetFileNameWithoutExtension(path);

                // stale remaps: map entries whose target object failed to resolve.
                int mapped = 0;
                var stale = new System.Collections.Generic.List<string>();
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp != null)
                {
                    foreach (var kv in imp.GetExternalObjectMap())
                    {
                        if (kv.Key.type != typeof(Material)) continue;
                        if (kv.Value != null) mapped++;
                        else stale.Add(kv.Key.name);
                    }
                }

                // null renderer slots on the imported model.
                int slots = 0, nullSlots = 0;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var sm in r.sharedMaterials)
                        {
                            slots++;
                            if (sm == null) nullSlots++;
                        }
                }

                bool ok = stale.Count == 0 && nullSlots == 0 && slots > 0;
                if (!ok) misBound++;
                string line = ok
                    ? $"[MatAudit] {name}: BOUND ok (slots={slots}, externalObjects mapped={mapped})"
                    : $"[MatAudit] {name}: MIS-BOUND — nullSlots={nullSlots}/{slots}, " +
                      $"staleRemaps=[{string.Join(", ", stale)}] (renders unlit fallback)";
                if (ok) Debug.Log(line); else Debug.LogWarning(line);
            }
            Debug.Log($"[MatAudit] DONE — {total - misBound}/{total} bound, {misBound} mis-bound. MAT_AUDIT_OK");
        }

        /// <summary>Extract embedded textures for one FBX into its sibling .fbm folder and
        /// reimport so the material links _BaseMap. Returns true if extraction ran.</summary>
        public static bool ExtractFor(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null)
            {
                Debug.LogWarning($"[TripoExtract] no ModelImporter at '{fbxPath}' — skipped.");
                return false;
            }

            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(fbxPath);
            string fbm = $"{dir}/{baseName}.fbm";   // Unity's conventional embedded-texture folder

            // 1) pull the embedded textures out as real assets (idempotent — re-extract is fine).
            bool any = imp.ExtractTextures(fbm);
            Debug.Log($"[TripoExtract] {baseName}: ExtractTextures -> '{fbm}' extracted={any}");

            // 2) force a reimport so the auto material remaps its _BaseMap to the extracted texture.
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            // 3) report what the material now references so the headless run is verifiable.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(fbxPath); // first sub-material if any
            var obj = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var o in obj)
            {
                if (o is Material m)
                {
                    bool hasBase = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                    bool hasMain = m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null;
                    Debug.Log($"[TripoExtract] {baseName}: material '{m.name}' shader='{m.shader.name}' _BaseMap={hasBase} _MainTex={hasMain}");
                }
            }
            return any;
        }
    }
}
