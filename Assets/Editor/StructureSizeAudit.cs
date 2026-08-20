// =============================================================================
// StructureSizeAudit — measure every structure's FITTED world size and rank the
// outliers, so "X looks bigger than everything else" is answered with numbers.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Batch:
//   -executeMethod DeNelle.Editor.StructureSizeAudit.Run
// Marker: STRUCTURE_SIZE_AUDIT_OK
//
// WHY MEASURED, NOT REASONED. The catalog's `_heightCadence` note is long, careful
// and argues its own case — including an explicit "DO NOT NORMALIZE collector_farm
// to 1.0". A note is a record of a past decision, not evidence about the current
// tree, and the owner is looking at a farm that reads oversized on her device right
// now. The only thing that settles that is the FITTED size, which is what the
// player actually sees: `YHeightVariable * repo.heightMul` after a UNIFORM fit.
//
// THE THING THE NOTE DOES NOT ADDRESS, and the reason this tool prints BOTH axes:
// heightMul fits BOUNDS uniformly, so it scales HEIGHT AND FOOTPRINT TOGETHER. The
// note justifies 1.4 on the grounds that windmill blades inflate the model's Y
// bounds, so the BODY reads small at 1.0 — a sound argument about HEIGHT. But the
// footprint has no blades to excuse it: at 1.4 the farm also claims ~40% more
// ground than a 1.0 building, and BuildModeController takes its grid claim from
// MeasureUprightFootprintMetres. So the two axes can disagree, and a tool that
// prints only one of them cannot tell you which.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class StructureSizeAudit
    {
        [MenuItem("Defenders/World/Audit structure sizes (fitted, measured)")]
        public static void Run()
        {
            var entries = CatalogRegistry.All();
            if (entries == null || entries.Count == 0)
            {
                Debug.LogError("STRUCTURE_SIZE_AUDIT_FAIL :: catalog is empty — nothing to measure.");
                return;
            }

            var rows = new List<(string id, string name, float mul, float height, float footprint)>();

            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (string.IsNullOrEmpty(e.visualPrefabPath)) continue;   // nothing to fit

                float mul = 1f;
                var repo = e.repo;
                if (repo != null && repo.heightMul > 0f) mul = repo.heightMul;

                float height = StructureFactory.YHeightVariable * mul;

                float footprint = 0f;
                try { footprint = StructureFactory.MeasureUprightFootprintMetres(e); }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SizeAudit] '{e.id}' footprint measure threw: {ex.GetType().Name} — reported as 0.");
                }

                rows.Add((e.id, e.displayName ?? e.id, mul, height, footprint));
            }

            // The BASE is the median of the 1.0-multiplier rows, not an authored constant. A median
            // cannot be skewed by the one outlier we are hunting, and it describes what the player
            // actually sees standing next to the farm.
            var baseRows = rows.Where(r => Mathf.Abs(r.mul - 1f) < 0.001f && r.footprint > 0f)
                               .Select(r => r.footprint).OrderBy(v => v).ToList();
            float baseFootprint = baseRows.Count > 0 ? baseRows[baseRows.Count / 2] : 0f;

            var sb = new StringBuilder();
            sb.AppendLine("--- STRUCTURE SIZE AUDIT (fitted world size, measured) ---");
            sb.AppendLine($"  base column = YHeightVariable {StructureFactory.YHeightVariable:0.##} m");
            sb.AppendLine($"  median footprint of heightMul=1.0 rows = {baseFootprint:0.00} m across");
            sb.AppendLine();
            sb.AppendLine($"  {"id",-26} {"heightMul",9} {"fitH(m)",8} {"footprint(m)",13} {"vs base",8}");

            foreach (var r in rows.OrderByDescending(r => r.footprint))
            {
                float ratio = baseFootprint > 0f && r.footprint > 0f ? r.footprint / baseFootprint : 0f;
                string flag = ratio >= 1.25f ? "  <== OUTLIER" : "";
                sb.AppendLine($"  {r.id,-26} {r.mul,9:0.00} {r.height,8:0.00} {r.footprint,13:0.00} " +
                              $"{ratio,7:0.00}x{flag}");
            }

            Debug.Log(sb.ToString());
            Debug.Log($"STRUCTURE_SIZE_AUDIT_OK {rows.Count} structure(s) measured");
        }
    }
}
