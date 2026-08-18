// =============================================================================
// TripoAssetPostprocessor — auto-extracts embedded textures + materials from
// Tripo-generated FBXs so URP can render their colours.
// -----------------------------------------------------------------------------
// Owner question 2026-05-20: "colors won't work, textures are not working" on
// the pets / heroes / cathedral.
//
// Tripo's "Send to Unity" / direct FBX export embeds each model's textures as
// binary PNG/JPEG chunks inside the FBX file, and writes materials in legacy
// FbxSurfacePhong. Unity 6's URP renders Phong as either a washed-grey blob or
// a magenta error, and even if a runtime fixer rebuilds materials as URP/Lit,
// the source materials' _MainTex slot is null (Unity didn't extract the
// embedded textures), so the fixer falls back to a fixed tint and the model
// loses its real colours.
//
// This editor script hooks the FBX import pipeline so any FBX dropped into one
// of the project's Tripo target folders:
//
//   Assets/Resources/Pets/*.fbx     — the three companion pets
//   Assets/Resources/Heroes/*.fbx   — Mage / Knight / Ranger bodies
//   Assets/Models/Cathedral/*.fbx   — the Elarion central spire
//
// automatically:
//
//   1) sets the FBX importer to External materials + texture-based naming so
//      the materials become editable .mat assets next to the FBX,
//   2) on import-complete, calls ModelImporter.ExtractTextures(...) to write
//      every embedded PNG/JPEG into a sibling "Textures/" folder, then
//   3) re-links each material's _MainTex / _BaseMap to the extracted texture
//      via SearchAndRemapMaterials, then forces a one-shot ReimportAndRefresh
//      so Unity picks up the new bindings.
//
// Idempotent: a marker file written next to the FBX records the first
// successful extraction so re-imports (and Unity restarts) don't repeat the
// work. Delete the marker (or the sibling Textures/ folder) to force a fresh
// extraction.
//
// The runtime TripoMaterialFixer is still useful as a safety net — this
// postprocessor handles the design path, the fixer handles the edge cases.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// AssetPostprocessor that detects Tripo FBXs landing in the project's
    /// configured Tripo paths and runs a one-shot texture + material
    /// extraction so URP can render their colours.
    /// </summary>
    public sealed class TripoAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Folders the postprocessor watches. An FBX import whose path starts
        /// with any of these is treated as a Tripo asset.
        /// </summary>
        private static readonly string[] TargetFolders =
        {
            "Assets/Resources/Pets/",
            "Assets/Resources/Heroes/",
            "Assets/Models/Cathedral/",
            "Assets/Art/TripoStructures/",   // owner Tripo building models (farm/forge/etc.)
            DeNelle.Core.AssetRoots.StructureContent + "/",  // owner Tripo Portal_To_Dungeon (dungeon entrance)
            DeNelle.Core.AssetRoots.EnemyContent + "/",      // Tripo enemy bodies (Troll/Orc/Skeleton sets)
            // WHITE HERO ROOT (fleet [Flow:HeroBody] 2026-07-07): the Paladin package FBX embeds
            // 3 PNGs (diffuse/normal/specular) that were never extracted → NULL albedo → solid-white
            // hero. EXACT FILE prefix (StartsWith match) so the sibling Animations/ FBXs and the
            // Paladin_Hero source don't get marker/Textures churn on their next reimport.
            "Assets/HeroPackages/Knight/Knight_Hero.fbx",
        };

        /// <summary>Sentinel file appended next to a processed FBX so we only run once.</summary>
        private const string MarkerSuffix = ".tripo-extracted";

        /// <summary>
        /// Asset paths discovered during an import pass that still need texture
        /// extraction. Populated inside the import callback (where AssetDatabase
        /// mutations are illegal) and drained afterwards via delayCall.
        /// </summary>
        private static readonly HashSet<string> Pending = new HashSet<string>();

        /// <summary>Guards against scheduling more than one drain at a time.</summary>
        private static bool _drainScheduled;

        // ---------------------------------------------------------------------
        // OnPreprocessModel — runs before the FBX is parsed. Configure the
        // ModelImporter so materials land as editable external .mat assets
        // (one per material) and Unity uses material descriptions when
        // converting Phong → Standard/URP.
        // ---------------------------------------------------------------------
        private void OnPreprocessModel()
        {
            if (!IsTripoPath(assetPath)) return;
            if (HasMarker(assetPath)) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // Material handling — let Unity emit one .mat per source material,
            // named after the diffuse texture so the bindings survive a re-import.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialName = ModelImporterMaterialName.BasedOnTextureName;
            importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;

            // ⛔ ARRIVE UPRIGHT (owner ruling 2026-08-18: "fix before scene so we dont have to
            // rotate after bake" / "maybe cleaner is adding this step as a import Tripo step").
            // Tripo exports Z-up; Unity is Y-up. Without this, EVERY Tripo model imports lying down
            // and has to be stood back up by a hand-authored rot=(-90,0,0) in Offset Forge — a
            // per-asset correction, applied forever, by every consumer. bakeAxisConversion applies
            // the conversion to the MESH DATA at import, so the asset is correct at identity and
            // needs no offset at all.
            // Doing it HERE rather than as a manual pass is what stops the debt recurring: a Tripo
            // model dropped in tomorrow is upright on arrival instead of joining the backlog of
            // things someone has to remember to rotate.
            // ⚠ This runs only for NEW imports (the marker check above returns early on
            // already-processed assets), so it cannot double-correct an existing model that still
            // carries its -90. TripoAxisBake handles that backlog, flipping the importer flag and
            // retiring the offset in the SAME pass — both halves together, or a model ends up
            // upside down.
            importer.bakeAxisConversion = true;

            // Animation — leave whatever the user already configured. The
            // hero/pet pipelines may set animationType themselves.
        }

        // ---------------------------------------------------------------------
        // OnPostprocessModel — runs after the FBX has been imported, but still
        // INSIDE the import pass. We must not call ExtractTextures / CreateFolder
        // / ImportAsset here: ExtractTextures synchronously re-imports the model,
        // which re-enters this callback before any marker exists, recurses, and
        // blows the stack → SIGABRT (the crash loop observed 2026-05-21 on the
        // un-imported Mage/aether-sprite FBXs). Instead we only QUEUE the path
        // and drain it after the import finishes, where those calls are legal.
        // ---------------------------------------------------------------------
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsTripoPath(assetPath)) return;
            if (HasMarker(assetPath)) return;

            Pending.Add(assetPath);
            ScheduleDrain();
        }

        // ---------------------------------------------------------------------
        // Deferred extraction — runs via EditorApplication.delayCall, i.e. after
        // the current import pass has fully finished. AssetDatabase mutations and
        // ExtractTextures are legal here.
        // ---------------------------------------------------------------------
        private static void ScheduleDrain()
        {
            if (_drainScheduled) return;
            _drainScheduled = true;
            EditorApplication.delayCall += DrainPending;
        }

        private static void DrainPending()
        {
            _drainScheduled = false;
            if (Pending.Count == 0) return;

            var paths = new List<string>(Pending);
            Pending.Clear();

            foreach (var path in paths)
            {
                if (HasMarker(path)) continue;
                try
                {
                    ProcessOne(path);
                }
                catch (System.Exception ex)
                {
                    // Marker is written first inside ProcessOne, so a failure here
                    // does not cause a crash loop on the next launch.
                    Debug.LogError($"[TripoAssetPostprocessor] Extraction failed for " +
                                   $"{Path.GetFileName(path)}: {ex}");
                }
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Extracts the embedded textures for a single FBX. Writes the marker
        /// FIRST so that if ExtractTextures hard-crashes the editor, the next
        /// launch skips this asset instead of crash-looping on it.
        /// </summary>
        private static void ProcessOne(string assetPath)
        {
            // Marker first — crash-loop insurance (see method summary).
            WriteMarker(assetPath);

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            string assetDir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(assetDir)) return;

            string textureDir = assetDir + "/Textures";
            if (!AssetDatabase.IsValidFolder(textureDir))
            {
                AssetDatabase.CreateFolder(assetDir, "Textures");
            }

            // ExtractTextures re-imports the model; the marker above guarantees
            // the re-entrant OnPostprocessModel bails instead of recursing.
            bool textureExtracted = importer.ExtractTextures(textureDir);

            Debug.Log(textureExtracted
                ? $"[TripoAssetPostprocessor] Extracted embedded textures from " +
                  $"{Path.GetFileName(assetPath)} → {textureDir}"
                : $"[TripoAssetPostprocessor] No embedded textures found in " +
                  $"{Path.GetFileName(assetPath)} (already extracted, or none in source).");
        }

        // ---------------------------------------------------------------------
        // Path helpers
        // ---------------------------------------------------------------------

        /// <summary>True if the asset path starts with any TargetFolder.</summary>
        private static bool IsTripoPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.Replace('\\', '/');
            foreach (var folder in TargetFolders)
            {
                if (p.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Marker file path — sits next to the FBX with a fixed suffix.</summary>
        private static string MarkerPathFor(string assetPath)
        {
            return assetPath + MarkerSuffix;
        }

        /// <summary>True if a previous run dropped the marker for this asset.</summary>
        private static bool HasMarker(string assetPath)
        {
            return File.Exists(MarkerPathFor(assetPath));
        }

        /// <summary>Writes the marker so re-imports skip the extraction.</summary>
        private static void WriteMarker(string assetPath)
        {
            try
            {
                File.WriteAllText(MarkerPathFor(assetPath),
                    "Tripo textures extracted by TripoAssetPostprocessor. " +
                    "Delete this file (and the sibling Textures/ folder) to force re-extract.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TripoAssetPostprocessor] Could not write marker for " +
                                 $"{Path.GetFileName(assetPath)}: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------------
        // Menu helpers — let the integrator force a re-extract from the Editor.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Removes the .tripo-extracted markers for every Tripo path so the
        /// next import re-runs the extraction. Useful when a Tripo FBX has
        /// been re-generated (e.g. with new animations) and the embedded
        /// textures changed.
        /// </summary>
        /// <summary>
        /// F8 2026-07-08 ("showing as white though so needs tripo color", ArcaneSpire_1): run the
        /// one-shot extraction for a SINGLE never-extracted FBX without touching the rest of the
        /// Tripo set (ForceReextractAll clears every marker — needless churn on assets that render
        /// fine). Batchmode-safe: drains synchronously like ForceReextractAll.
        /// </summary>
        [MenuItem("Defenders/Tripo/Extract ArcaneSpire_1 (white fix)")]
        public static void ExtractArcaneSpire1()
        {
            const string path = DeNelle.Core.AssetRoots.StructureContent + "/ArcaneSpire_1.fbx";
            string marker = MarkerPathFor(path);
            if (File.Exists(marker)) File.Delete(marker);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            DrainPending();

            // PERSIST the binding (F8 2026-07-08 verify pass): ProcessOne only EXTRACTS — the
            // header's promised SearchAndRemapMaterials step was never in the code, and Unity 6
            // dropped External material location ("MaterialLocation.External is obsolete" import
            // warnings), so the OnPreprocessModel settings are transient and externalObjects
            // stayed {} — the model kept its embedded null-albedo material (still white on any
            // reimport). Remap by texture name against the generated Materials/*.mat (which DO
            // bind the extracted albedo) and SAVE, so the meta carries the binding durably.
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnTextureName,
                    ModelImporterMaterialSearch.Local);
                importer.SaveAndReimport();
                var map = importer.GetExternalObjectMap();
                Debug.Log("[TripoAssetPostprocessor] remap saved for " + path +
                          " — externalObjects=" + map.Count);
            }
            Debug.Log("[TripoAssetPostprocessor] single-asset extraction drained for " + path);
        }

        /// <summary>
        /// Fleet ticket 2026-07-07 ("[Flow:HeroBody] WHITE HERO ROOT", Paladin package): one-shot
        /// extraction for Assets/HeroPackages/Knight/Knight_Hero.fbx — it embeds 3 PNGs
        /// (Paladin diffuse/normal/specular, verified binary scan) that were never extracted because
        /// the FBX lives outside the original TargetFolders, so its one material ("Paladin_MAT")
        /// carries a NULL albedo and the hero renders solid white. Mirrors ExtractArcaneSpire1
        /// exactly: marker-delete + ForceUpdate import + synchronous DrainPending (batchmode-safe)
        /// + SearchAndRemapMaterials(BasedOnTextureName, Local) + SaveAndReimport so the meta's
        /// externalObjects carries the albedo binding durably. KnightPackage.prefab references the
        /// FBX materials, so the prefab (Resources/Heroes/KnightPackage.prefab) picks the texture up
        /// with no prefab edit.
        /// </summary>
        [MenuItem("Defenders/Tripo/Extract Knight_Hero Paladin package (white hero fix)")]
        public static void ExtractKnightHeroPackage()
        {
            const string path = "Assets/HeroPackages/Knight/Knight_Hero.fbx";
            string marker = MarkerPathFor(path);
            if (File.Exists(marker)) File.Delete(marker);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            DrainPending();

            // PERSIST the binding (same rationale as ExtractArcaneSpire1): ProcessOne only
            // EXTRACTS; Unity 6 dropped External material location, so without an explicit remap
            // the model keeps its embedded null-albedo material on every reimport. Remap by
            // texture name against the extracted Textures/*.png and SAVE so the meta carries it.
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnTextureName,
                    ModelImporterMaterialSearch.Local);
                importer.SaveAndReimport();
                var map = importer.GetExternalObjectMap();
                Debug.Log("[TripoAssetPostprocessor] remap saved for " + path +
                          " — externalObjects=" + map.Count);
            }
            Debug.Log("[TripoAssetPostprocessor] single-asset extraction drained for " + path);
        }

        [MenuItem("Defenders/Tripo/Force re-extract all textures")]
        public static void ForceReextractAll()
        {
            int cleared = 0;
            foreach (var folder in TargetFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) continue;
                string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') });
                foreach (var guid in fbxGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string marker = MarkerPathFor(path);
                    if (File.Exists(marker))
                    {
                        File.Delete(marker);
                        cleared++;
                    }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            // The ImportAsset calls above queued the assets via OnPostprocessModel
            // and scheduled a delayCall drain — but in batchmode -executeMethod the
            // editor quits before delayCall fires. Drain synchronously here (we are
            // outside any import callback, so it is legal) so the extraction runs.
            DrainPending();

            Debug.Log($"[TripoAssetPostprocessor] Force re-extract requested — cleared {cleared} marker(s).");
        }
    }
}
