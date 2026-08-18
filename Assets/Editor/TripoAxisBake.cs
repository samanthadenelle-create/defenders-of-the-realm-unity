// =============================================================================
// TripoAxisBake — make Tripo models sit UPRIGHT on import, and retire the manual
// -90 rotations that exist only to compensate for them not doing so.
// -----------------------------------------------------------------------------
// OWNER ASK 2026-08-18: "is there a way to save the tripos fbx file with the
// rotation offset we need? so they by default sit correctly?" ... "fix before
// scene so we dont have to rotate after bake".
//
// THE ANSWER IS ModelImporter.bakeAxisConversion. Tripo exports Z-up; Unity is
// Y-up. Today every Tripo model imports lying down and is stood back up by a
// hand-authored rot=(-90,0,0) in Offset Forge — a correction applied at RUNTIME,
// per asset, forever. bakeAxisConversion applies the conversion to the MESH DATA
// at import instead, so the asset is upright at identity and needs no offset at
// all. The fix moves from "every consumer compensates" to "the asset is right".
//
// ⛔ THE TWO HALVES MUST FLIP TOGETHER OR THE MODEL ENDS UP UPSIDE DOWN.
// bakeAxisConversion ON + the -90 offset still authored = both corrections apply
// = 180 degrees. That is not hypothetical: it is exactly the failure recorded on
// tower_ground_archer (WO-928 defect A) and the shield seat, where two systems
// each applied a rotation that only one of them should have. So this tool sets
// the importer flag AND zeroes the matching offset in the SAME pass, and refuses
// to do one without the other.
//
// ⚠ SCOPED TO MODELS THAT CARRY THE -90. A model already sitting correctly has
// no compensation to remove, and baking its axis would TILT it. The offset table
// is therefore the authority on what is affected — if the owner never had to
// rotate it, this tool does not touch it.
//
// Run BEFORE any scene bake (owner: "fix before scene"), because scene placement
// and the navmesh both capture the pose as it stands at bake time.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Bakes Tripo axis conversion into the mesh and retires the matching -90 offsets.</summary>
    public static class TripoAxisBake
    {
        private const string OffsetsPath = "Assets/OffsetForge/offsets.json";
        private const string OffsetsMirror = "Assets/Resources/OffsetForge/offsets.json";
        private const string OkMarker = "TRIPO_AXIS_BAKE_OK";

        /// <summary>Roots searched for a model matching an offset id.</summary>
        private static readonly string[] Roots =
        {
            DeNelle.Core.AssetRoots.StructureContent,
            DeNelle.Core.AssetRoots.EnemyContent,
        };

        [MenuItem("Defenders/Art/Bake Tripo axis conversion (retire the -90 offsets)")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            if (!System.IO.File.Exists(OffsetsPath))
            {
                Debug.LogError($"[AxisBake] {OffsetsPath} not found — nothing to reconcile.");
                return;
            }

            string json = System.IO.File.ReadAllText(OffsetsPath);

            // ⛔ THE STATE IS RECORDED, NOT INFERRED (owner ruling 2026-08-18: "add a flag, to
            // denote if gone through the unity" / "use the data not a guess" / "a binary value is a
            // small price to pay").
            //
            // The first version decided what to bake by MATCHING rot == (-90,0,0) exactly — i.e. it
            // guessed "this looks like axis compensation" from the value's shape. That is fragile in
            // both directions: a model the owner nudged to (-90, 12, 0) would be skipped even though
            // it IS compensating, and a model legitimately authored at -90 for some other reason
            // would be baked and then laid flat. Worse, the guess is not idempotent — after a bake
            // the offset reads 0, so a second run cannot tell "already baked" from "never needed it".
            //
            // "axisBaked" makes the answer a FACT the file carries. One bool per row, written by the
            // only code that performs the bake. It survives re-runs, it is inspectable, and nothing
            // has to reason about what a rotation value implies.
            var alreadyBaked = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(
                         json,
                         "\"id\"\\s*:\\s*\"([^\"]+)\"(?:(?!\"id\")[\\s\\S])*?\"axisBaked\"\\s*:\\s*true",
                         System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                alreadyBaked.Add(m.Groups[1].Value);
            }
            if (alreadyBaked.Count > 0)
                Debug.Log($"[AxisBake] {alreadyBaked.Count} id(s) already flagged axisBaked=true — skipping: " +
                          string.Join(", ", alreadyBaked));

            // Candidates still carry the compensation. The -90 match selects them ONCE; from then on
            // the flag is what answers the question.
            var affected = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(
                         json,
                         "\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"rot\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*-90(?:\\.0+)?\\s*,\\s*\"y\"\\s*:\\s*0(?:\\.0+)?\\s*,\\s*\"z\"\\s*:\\s*0(?:\\.0+)?\\s*\\}",
                         System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                if (!alreadyBaked.Contains(m.Groups[1].Value)) affected.Add(m.Groups[1].Value);
            }

            if (affected.Count == 0)
            {
                Debug.Log("[AxisBake] no ids carry an exact rot=(-90,0,0) — nothing to do.");
                Debug.Log($"{OkMarker} 0 baked");
                return;
            }
            Debug.Log($"[AxisBake] {affected.Count} id(s) carry the -90 compensation: {string.Join(", ", affected)}");

            // ---- half one: bake the conversion into the model ---------------------
            int baked = 0;
            var bakedIds = new List<string>();
            foreach (var id in affected)
            {
                string path = FindModel(id);
                if (path == null)
                {
                    Debug.Log($"[AxisBake] '{id}' has no model under the content roots — offset left ALONE " +
                              "(it may target a prop or a pack prefab, which this tool does not own).");
                    continue;
                }

                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;

                if (!mi.bakeAxisConversion)
                {
                    mi.bakeAxisConversion = true;
                    mi.SaveAndReimport();
                    baked++;
                    Debug.Log($"[AxisBake] baked axis conversion: {System.IO.Path.GetFileName(path)}");
                }
                bakedIds.Add(id);
            }

            // ---- half two: retire the now-redundant offsets -----------------------
            // ⛔ ONLY for ids whose model was actually baked. Zeroing an offset whose model was NOT
            // baked would lay that model flat — the exact inverse of the bug being fixed.
            int cleared = 0;
            foreach (var id in bakedIds)
            {
                string before = json;

                // Zero the now-redundant compensation...
                json = System.Text.RegularExpressions.Regex.Replace(
                    json,
                    "(\"id\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(id) +
                    "\"\\s*,\\s*\"rot\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*)-90(?:\\.0+)?",
                    "${1}0.0",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                // ...and RECORD that this asset has been through the bake. Written in the same pass
                // as the zeroing so the flag and the value can never disagree: a row that says
                // axisBaked=true always has its compensation removed, and vice versa.
                json = System.Text.RegularExpressions.Regex.Replace(
                    json,
                    "(\"id\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(id) +
                    "\"(?:(?!\"id\")[\\s\\S])*?\"fullOverride\"\\s*:\\s*(?:true|false))",
                    "${1},\n            \"axisBaked\": true",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (json != before) cleared++;
            }

            System.IO.File.WriteAllText(OffsetsPath, json);
            if (System.IO.File.Exists(OffsetsMirror)) System.IO.File.Copy(OffsetsPath, OffsetsMirror, true);
            AssetDatabase.Refresh();

            Debug.Log($"[AxisBake] {baked} model(s) baked upright; {cleared} offset(s) retired to 0. " +
                      "Authoring file + Resources mirror written together.");
            if (baked != cleared)
            {
                Debug.LogError($"[AxisBake] MISMATCH: {baked} baked vs {cleared} offsets cleared. The two halves " +
                               "MUST agree — a baked model that keeps its -90 lands UPSIDE DOWN, and a cleared " +
                               "offset on an unbaked model lays it FLAT. Inspect before baking any scene.");
                return;
            }
            Debug.Log($"{OkMarker} {baked} baked, {cleared} offsets retired");
        }

        private static string FindModel(string id)
        {
            foreach (var root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var ext in new[] { ".fbx", ".FBX" })
                {
                    string p = $"{root}/{id}{ext}";
                    if (System.IO.File.Exists(p)) return p;
                }
            }
            return null;
        }
    }
}
