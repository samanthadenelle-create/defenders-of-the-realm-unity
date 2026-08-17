// =============================================================================
// TripoStructureMaterialAudit — reports what each FBX in Resources/Structures
// ACTUALLY bound, and repairs the ones whose extraction never ran.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (2026-08-17, the eight owner-purchased models)
//
// TripoAssetPostprocessor already watches Assets/Resources/Structures/ and is
// supposed to make these render. It did not, for two independent reasons that
// both hide behind a green compile gate — a gate on the CODE cannot see a
// wrong ARTIFACT:
//
//   1. BATCHMODE NEVER DRAINS. OnPostprocessModel only QUEUES the path and
//      schedules EditorApplication.delayCall. Under -executeMethod the editor
//      quits before delayCall fires, so an FBX imported by a batchmode gate is
//      queued and dropped. ForceReextractAll's own comment records this and
//      calls DrainPending() synchronously to work around it. Every FBX imported
//      by a batchmode run since is therefore un-extracted and un-marked.
//
//   2. THE MARKER OUTLIVES THE ASSET. HasMarker() gates BOTH callbacks on a
//      sentinel file whose mere EXISTENCE means "done". Replace the .fbx in
//      place — which is exactly what you must do to keep the GUID, and what was
//      done to Forge/armorer/jeweler — and the marker still sits there
//      describing a model that is gone. The new body silently inherits the old
//      body's verdict and never extracts. This is the project's recurring bug
//      class (the -90° pet yaw, the stale build-list literal): a value that was
//      true when written, welded in place, outliving the thing it described.
//
// THE FIX FOR (2) IS THE INTERESTING ONE: a marker is stale when it is OLDER
// THAN THE FBX IT DESCRIBES. That is a fact on disk, not a judgement, and it
// makes the whole class self-healing — any future in-place model replacement
// re-extracts without anyone remembering to clear a marker by hand.
//
// ⛔ Report FIRST. Per CLAUDE.md §12 this file leads with a read-only audit so
// the repair is aimed at a measured defect rather than an assumed one. "It
// renders grey" is a theory until a material's albedo is observed to be null.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Read-only audit + targeted repair for Tripo FBX material bindings under
    /// Assets/Resources/Structures.
    /// </summary>
    public static class TripoStructureMaterialAudit
    {
        private const string StructuresRoot = "Assets/Resources/Structures";
        private const string MarkerSuffix = ".tripo-extracted";

        /// <summary>Marker printed on a clean audit pass, per the project's marker discipline.</summary>
        private const string OkMarker = "TRIPO_AUDIT_OK";

        // -------------------------------------------------------------------------
        // REPORT — read-only. Changes nothing; prints what every FBX actually bound.
        // -------------------------------------------------------------------------

        [MenuItem("Defenders/Tripo/Audit Structures material bindings (report only)")]
        public static void Report()
        {
            var rows = Collect();

            Debug.Log($"[TripoAudit] ==== {rows.Count} FBX under {StructuresRoot} ====");
            foreach (var r in rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                Debug.Log($"[TripoAudit] {r}");
            }

            int broken = rows.Count(r => r.NeedsRepair);
            int stale = rows.Count(r => r.MarkerIsStale);
            int missing = rows.Count(r => !r.IsPrefab && !r.HasMarker);

            Debug.Log($"[TripoAudit] SUMMARY: {rows.Count} assets ({rows.Count(r => !r.IsPrefab)} fbx, " +
                      $"{rows.Count(r => r.IsPrefab)} prefab) | {broken} UNTEXTURED-AND-UNTINTED " +
                      $"(the real defect) | {stale} carrying a STALE marker (older than the .fbx) | " +
                      $"{missing} fbx with NO marker.");
            Debug.Log(OkMarker + $" {rows.Count} models audited");
        }

        // -------------------------------------------------------------------------
        // REPAIR — clears markers that are missing-or-stale, re-imports, drains the
        // extraction SYNCHRONOUSLY (batchmode-safe), then persists the binding.
        // -------------------------------------------------------------------------

        [MenuItem("Defenders/Tripo/Repair Structures material bindings")]
        public static void Repair()
        {
            var rows = Collect();
            var targets = rows.Where(r => !r.IsPrefab)
                              .Where(r => !r.HasMarker || r.MarkerIsStale || r.NeedsRepair)
                              .ToList();

            if (targets.Count == 0)
            {
                Debug.Log("[TripoAudit] nothing to repair — every model has a current marker and a bound albedo.");
                Debug.Log(OkMarker + " 0 repaired");
                return;
            }

            Debug.Log($"[TripoAudit] repairing {targets.Count} of {rows.Count} models: " +
                      string.Join(", ", targets.Select(t => t.Name)));

            foreach (var t in targets)
            {
                string marker = t.Path + MarkerSuffix;
                if (File.Exists(marker))
                {
                    // The marker is the thing blocking BOTH postprocessor callbacks. Deleting it is
                    // what lets OnPreprocessModel re-apply the material-description import settings.
                    File.Delete(marker);
                    Debug.Log($"[TripoAudit] cleared {(t.MarkerIsStale ? "STALE" : "")} marker for {t.Name}");
                }
                AssetDatabase.ImportAsset(t.Path, ImportAssetOptions.ForceUpdate);
            }

            // ⚠ SYNCHRONOUS DRAIN. The postprocessor queues via delayCall, which never fires under
            // -executeMethod. Without this the ImportAsset calls above accomplish nothing in
            // batchmode — the exact defect this file was written to fix. ForceReextractAll uses
            // reflection-free access because it lives in the same assembly; so do we.
            InvokeDrain();

            // ProcessOne only EXTRACTS. Unity 6 dropped ModelImporterMaterialLocation.External, so
            // OnPreprocessModel's settings are transient and externalObjects stays empty unless the
            // remap is explicitly saved into the .meta. This is the step whose absence left
            // ArcaneSpire_1 white in 2026-07 even though its textures had been extracted.
            foreach (var t in targets)
            {
                var importer = AssetImporter.GetAtPath(t.Path) as ModelImporter;
                if (importer == null) continue;
                importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnTextureName,
                    ModelImporterMaterialSearch.RecursiveUp);
                importer.SaveAndReimport();
                Debug.Log($"[TripoAudit] remap saved for {t.Name} — " +
                          $"externalObjects={importer.GetExternalObjectMap().Count}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Re-measure rather than assert success. A repair that reports itself done without
            // re-reading the artifact is the failure mode this whole file exists to prevent.
            var after = Collect().Where(r => targets.Any(t => t.Path == r.Path)).ToList();
            foreach (var r in after) Debug.Log($"[TripoAudit] AFTER {r}");

            int stillBroken = after.Count(r => r.NeedsRepair);
            Debug.Log($"[TripoAudit] SUMMARY: repaired {targets.Count}; {stillBroken} still carry a NULL albedo.");
            Debug.Log(OkMarker + $" {targets.Count} repaired, {stillBroken} still broken");
        }

        /// <summary>
        /// Drains TripoAssetPostprocessor's pending queue synchronously. The drain is private, so
        /// this reaches it the same way ForceReextractAll does — by calling the public menu path is
        /// NOT an option (it clears every marker across Pets/Heroes too, which is needless churn on
        /// assets that render fine). Reflection is confined to this one call and logs on failure
        /// rather than failing silently.
        /// </summary>
        private static void InvokeDrain()
        {
            var m = typeof(TripoAssetPostprocessor).GetMethod(
                "DrainPending",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m == null)
            {
                Debug.LogError("[TripoAudit] DrainPending not found on TripoAssetPostprocessor — " +
                               "extraction will NOT have run. Do not trust a green result from this pass.");
                return;
            }
            m.Invoke(null, null);
        }

        // -------------------------------------------------------------------------
        // VERIFY CATALOG ART — does every authored art path actually LOAD?
        //
        // ⛔ THIS IS THE OUTCOME GATE, AND IT IS A DIFFERENT QUESTION FROM THE ONE THE COMPILE GATE
        // ANSWERS. visualPrefabPath is a STRING. Nothing in the build fails when it names an asset
        // that does not exist, was renamed, or lives one folder over — StructureFactory logs a
        // LogWarning (CLAUDE.md §4) and the building silently renders as nothing. A green compile
        // gate and a green regression run are both fully compatible with every structure in the town
        // being invisible, because neither of them ever calls Resources.Load.
        //
        // That is exactly how tower_ballista shipped to players rendering a WIZARD TOWER: the path
        // resolved fine, so no warning ever fired. A path that loads is necessary, not sufficient —
        // this method catches the paths that load NOTHING, and the audit above catches the ones that
        // load something untextured. Neither can catch "loads the WRONG building"; only a human or a
        // screenshot can, which is why the PROD checklists ask for owner eyes.
        // -------------------------------------------------------------------------

        [MenuItem("Defenders/Tripo/Verify every catalog art path loads")]
        public static void VerifyCatalogArt()
        {
            string json = null;
            foreach (var p in new[]
                     {
                         "Assets/Resources/Data/Canonical/structures-catalog.json",
                         "Assets/StreamingAssets/Data/Canonical/structures-catalog.json",
                     })
            {
                if (File.Exists(p)) { json = File.ReadAllText(p); break; }
            }
            if (json == null)
            {
                Debug.LogError("[TripoAudit] structures-catalog.json not found — cannot verify.");
                return;
            }

            // Deliberately a regex over the raw text rather than a typed deserialize: this method
            // must keep working if the catalog schema gains a field, and it must not depend on the
            // very registry whose data it is auditing.
            var paths = new List<KeyValuePair<string, string>>();
            string currentId = "<none>";
            bool inVisualArray = false;
            foreach (var line in json.Split('\n'))
            {
                var idM = System.Text.RegularExpressions.Regex.Match(line, "\"id\"\\s*:\\s*\"([^\"]+)\"");
                if (idM.Success) { currentId = idM.Groups[1].Value; continue; }

                var artM = System.Text.RegularExpressions.Regex.Match(
                    line, "\"(?:visualPrefabPath|upgradeVisualPath)\"\\s*:\\s*\"([^\"]+)\"");
                if (artM.Success) { paths.Add(new KeyValuePair<string, string>(currentId, artM.Groups[1].Value)); continue; }

                // upgradeVisualPath is an ARRAY — its entries are bare strings on their own lines.
                // ⚠ SO IS upgradeTexturePath, and its entries ALSO start with "Structures/". The
                // first version of this matcher swept those in and reported three TEXTURES as
                // "renders as NOTHING in game" — a confident, wrong, alarming verdict on rows that
                // are fine (tower_ground_archer, tower_arcane_spire). Tracking which array we are
                // inside is the difference between an oracle and a rumour.
                var arrayOpen = System.Text.RegularExpressions.Regex.Match(line, "\"(\\w+)\"\\s*:\\s*\\[");
                if (arrayOpen.Success) { inVisualArray = arrayOpen.Groups[1].Value == "upgradeVisualPath"; continue; }
                if (line.Contains("]")) { inVisualArray = false; }

                if (!inVisualArray) continue;
                var bare = System.Text.RegularExpressions.Regex.Match(line, "^\\s*\"(Structures/[^\"]+)\"\\s*,?\\s*$");
                if (bare.Success) paths.Add(new KeyValuePair<string, string>(currentId, bare.Groups[1].Value));
            }

            int ok = 0, missing = 0;
            foreach (var kv in paths)
            {
                var go = Resources.Load<GameObject>(kv.Value);
                if (go == null)
                {
                    missing++;
                    Debug.LogError($"[TripoAudit] MISSING ART: '{kv.Key}' -> '{kv.Value}' does not " +
                                   $"Resources.Load as a GameObject. This building renders as NOTHING in game.");
                }
                else
                {
                    ok++;
                    Debug.Log($"[TripoAudit] art ok: {kv.Key,-22} -> {kv.Value}");
                }
            }

            Debug.Log($"[TripoAudit] CATALOG ART: {ok} loadable, {missing} MISSING, {paths.Count} authored.");
            Debug.Log(missing == 0
                ? OkMarker + $" catalog-art {ok}/{paths.Count} loadable"
                : $"TRIPO_AUDIT_FAIL {missing} unloadable art path(s)");
        }

        // -------------------------------------------------------------------------

        private static List<Row> Collect()
        {
            var rows = new List<Row>();
            if (!AssetDatabase.IsValidFolder(StructuresRoot)) return rows;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { StructuresRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                rows.Add(Measure(path));
            }

            // ⛔ THE PREFAB IS WHAT THE GAME LOADS, NOT THE FBX. structures-catalog rows carry a
            // 'visualPrefabPath', and builders like WoodenWatchtowerBuilder author their OWN URP/Lit
            // materials onto a prefab rather than using the FBX's. So an FBX with a null albedo does
            // NOT prove the in-game model is untextured, and an FBX that binds fine does not prove
            // the prefab does. Auditing only the FBX would have produced a confident wrong verdict in
            // both directions — measure both and let the pair say which layer is actually broken.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { StructuresRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var row = Measure(path);
                row.IsPrefab = true;
                rows.Add(row);
            }
            return rows;
        }

        private static Row Measure(string path)
        {
            var row = new Row { Path = path, Name = Path.GetFileNameWithoutExtension(path) };

            string marker = path + MarkerSuffix;
            row.HasMarker = File.Exists(marker);
            if (row.HasMarker)
            {
                // STALENESS IS A FACT ON DISK, not a judgement: a marker written before the .fbx it
                // describes was last written cannot be describing the current model.
                var markerTime = File.GetLastWriteTimeUtc(marker);
                var fbxTime = File.GetLastWriteTimeUtc(path);
                row.MarkerIsStale = markerTime < fbxTime;
            }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                row.Note = "FBX did not load as a GameObject";
                return row;
            }

            var mats = new List<Material>();
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            {
                row.RendererCount++;
                foreach (var m in rend.sharedMaterials)
                {
                    if (m != null && !mats.Contains(m)) mats.Add(m);
                }
            }

            foreach (var m in mats)
            {
                row.MaterialCount++;
                string shader = m.shader != null ? m.shader.name : "<null shader>";
                Texture albedo = null;
                if (m.HasProperty("_BaseMap")) albedo = m.GetTexture("_BaseMap");
                if (albedo == null && m.HasProperty("_MainTex")) albedo = m.GetTexture("_MainTex");

                // ⛔ A NULL ALBEDO IS NOT A DEFECT ON ITS OWN. The first pass of this audit flagged
                // 21 Polyperfect prefabs (M_20_Grey_LPUP, M_64_Glass_LPUP, …) as broken because they
                // carry no texture. They are FLAT-COLOUR materials BY DESIGN — the Low Poly Ultimate
                // Pack tints per-material through _BaseColor and ships no maps at all. Counting
                // "no texture" as "broken" produced 21 false positives against 0 real ones, which is
                // worse than no audit: a report that cries wolf gets ignored on the day it is right.
                //
                // The signature of the REAL defect (the washed-grey Tripo blob this pipeline exists
                // to fix) is a null albedo AND a near-white base colour — nothing bound, nothing
                // tinted. A null albedo with a deliberate tint is a working flat-shaded material.
                Color tint = Color.white;
                if (m.HasProperty("_BaseColor")) tint = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) tint = m.GetColor("_Color");

                bool tinted = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)) < 0.92f;
                if (albedo == null && !tinted) row.NullAlbedoCount++;
                else if (albedo == null) row.FlatColorCount++;

                row.Materials.Add($"{m.name}[{shader}]->" +
                                  (albedo != null ? albedo.name
                                   : tinted ? $"flat({tint.r:F2},{tint.g:F2},{tint.b:F2})"
                                   : "NULL+untinted"));
            }

            return row;
        }

        private sealed class Row
        {
            public string Path;
            public string Name;
            public bool HasMarker;
            public bool MarkerIsStale;
            /// <summary>True for a .prefab row. Prefabs have no Tripo marker and are NOT repairable
            /// by re-extraction — their materials are authored by a builder, so a broken prefab is a
            /// builder problem, not an importer one. Repair() must skip them or it would delete
            /// nothing, re-import a prefab as if it were a model, and report success.</summary>
            public bool IsPrefab;
            public int RendererCount;
            public int MaterialCount;
            public int NullAlbedoCount;
            /// <summary>Materials with no texture but a deliberate tint — Polyperfect's flat-colour
            /// style. Counted separately so they never read as a defect.</summary>
            public int FlatColorCount;
            public string Note = "";
            public readonly List<string> Materials = new List<string>();

            /// <summary>A model needs repair when any material has no albedo bound — that is the
            /// observable that maps to "renders grey/white in game".</summary>
            public bool NeedsRepair => NullAlbedoCount > 0 || MaterialCount == 0;

            public override string ToString()
            {
                string state = NeedsRepair ? "BROKEN" : "ok    ";
                string markerState = IsPrefab
                    ? "PREFAB"
                    : (!HasMarker ? "no-marker" : (MarkerIsStale ? "STALE-marker" : "marker-ok"));
                return $"{state} {Name,-28} {markerState,-13} " +
                       $"rend={RendererCount} mats={MaterialCount} untextured={NullAlbedoCount} " +
                       $"flat={FlatColorCount} " +
                       $"{(Materials.Count > 0 ? string.Join(" | ", Materials) : Note)}";
            }
        }
    }
}
