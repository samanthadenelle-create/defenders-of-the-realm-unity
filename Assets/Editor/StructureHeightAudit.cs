// =============================================================================
// StructureHeightAudit (WO-751) - READ-ONLY audit of every structures-catalog
// entry's fit-to-HEIGHT relationship. For each entry it loads the visualPrefabPath
// prefab, measures its combined-renderer Y-extent (raw bounds at identity/scale-1 -
// exactly the value VisualFactory.Fit divides into), resolves the effective target
// height (WO-764: YHeightVariable * repo.heightMul, multiplier default 1.0),
// computes the resulting uniform scale (targetH / measuredY - the factor
// StructureFactory actually applies), and prints one table row per entry:
//
//   id | measuredY | targetH | source(default/override) | scale | FLAG
//
// FLAG fires when the scale is wild (>3x or <0.3x) so the owner can spot a prefab
// whose native size fights the target (a candidate for its own override or a
// re-import). Read-only: this tool NEVER writes the catalog - it is the report the
// owner tunes overrides FROM (companion to StructureFactory's fit-to-height change).
//
//   Defenders > Build > Audit Structure Heights
//   (batchmode: DeNelle.Editor.StructureHeightAudit.AuditBatch)
//
// Emits STRUCTURE_HEIGHT_AUDIT_OK at the end for headless scraping. Guard.Try wraps
// each entry so one bad prefab logs + is skipped, never aborting the sweep (sec 12).
// =============================================================================

using System.Globalization;
using System.IO;
using System.Text;
using DeNelle.Core.Diagnostics;   // Guard - one bad entry never aborts the sweep (sec 12)
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class StructureHeightAudit
    {
        // MIRROR of StructureFactory.YHeightVariable (DeNelle.Village). The Editor
        // assembly does not reference DeNelle.Village, so this is duplicated by necessity -
        // it MUST stay equal to StructureFactory.YHeightVariable (WO-764 = 4 m base ceiling). If
        // that const changes, change this too (both are the ONE global base height).
        private const float YHeightVariable = 4f;

        // Wild-scale flag thresholds (WO-751 acceptance): a fit that up-scales past 3x or
        // down-scales below 0.3x means the prefab's native Y fights the target height.
        private const float WildHigh = 3.0f;
        private const float WildLow  = 0.3f;

        private const string CatalogPath =
            "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";

        [MenuItem("Defenders/Build/Audit Structure Heights")]
        public static void Audit() => Run();

        /// <summary>Headless batch entry (batchmode -executeMethod). Same read-only sweep.</summary>
        public static void AuditBatch() => Run();

        private static void Run()
        {
            if (!File.Exists(CatalogPath))
            {
                Debug.LogError($"[StructureHeightAudit] Missing catalog: {CatalogPath}");
                Debug.Log("STRUCTURE_HEIGHT_AUDIT_OK");   // still emit so headless completes
                return;
            }

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(CatalogPath)); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[StructureHeightAudit] Catalog parse failed: {ex.Message}");
                Debug.Log("STRUCTURE_HEIGHT_AUDIT_OK");
                return;
            }

            var entries = root["entries"] as JArray;
            int version = root.Value<int?>("version") ?? -1;
            if (entries == null)
            {
                Debug.LogError("[StructureHeightAudit] No 'entries' array.");
                Debug.Log("STRUCTURE_HEIGHT_AUDIT_OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[StructureHeightAudit] catalog v{version}, {entries.Count} entries. " +
                          $"YHeightVariable={YHeightVariable:0.##}m (mirror of StructureFactory).");
            sb.AppendLine("id                        | measuredY | targetH | source   | scale | flag");
            sb.AppendLine("--------------------------|-----------|---------|----------|-------|-----");

            int measured = 0, missing = 0, noRenderers = 0, flagged = 0;

            foreach (var e in entries)
            {
                var entry = e as JObject;
                if (entry == null) continue;

                string id = entry.Value<string>("id") ?? "?";
                Guard.Try("StructureHeightAudit", $"audit '{id}'", () =>
                {
                    string vp = entry.Value<string>("visualPrefabPath");
                    var repo = entry["repo"] as JObject;
                    float mult = repo != null ? (repo.Value<float?>("heightMul") ?? 1f) : 1f;
                    if (mult <= 0f) mult = 1f;
                    bool isOverride = !Mathf.Approximately(mult, 1f);
                    float targetH = YHeightVariable * mult;
                    string source = isOverride ? "override" : "default";

                    if (string.IsNullOrEmpty(vp))
                    {
                        sb.AppendLine(Row(id, -1f, targetH, source, -1f, "NO-PREFAB-PATH"));
                        missing++;
                        return;
                    }

                    var prefab = Resources.Load<GameObject>(vp);
                    if (prefab == null)
                    {
                        sb.AppendLine(Row(id, -1f, targetH, source, -1f, $"MISSING '{vp}'"));
                        missing++;
                        return;
                    }

                    float measuredY = MeasurePrefabYExtent(prefab);
                    if (measuredY <= 0.0001f)
                    {
                        sb.AppendLine(Row(id, measuredY, targetH, source, -1f, "NO-RENDERERS"));
                        noRenderers++;
                        return;
                    }

                    // Exactly VisualFactory.Fit's factor: localScale *= target / measure(=Y).
                    float scale = targetH / measuredY;
                    string flag = (scale > WildHigh || scale < WildLow) ? "WILD" : "";
                    if (flag == "WILD") flagged++;
                    sb.AppendLine(Row(id, measuredY, targetH, source, scale, flag));
                    measured++;
                });
            }

            sb.AppendLine("--------------------------|-----------|---------|----------|-------|-----");
            sb.AppendLine($"measured={measured}  missing-prefab={missing}  no-renderers={noRenderers}  WILD-flags={flagged}");
            Debug.Log(sb.ToString());
            Debug.Log("STRUCTURE_HEIGHT_AUDIT_OK");
        }

        private static string Row(string id, float measuredY, float targetH, string source, float scale, string flag)
        {
            string m = measuredY < 0f ? "  --  " : measuredY.ToString("0.00", CultureInfo.InvariantCulture);
            string s = scale < 0f ? " -- " : scale.ToString("0.00", CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture,
                "{0,-25} | {1,9} | {2,7:0.00} | {3,-8} | {4,5} | {5}",
                id, m, targetH, source, s, flag);
        }

        /// <summary>Combined-renderer world-bounds Y-extent of <paramref name="prefab"/> at
        /// identity rotation / unit scale - the exact value VisualFactory.Fit divides the
        /// target height by to compute the applied scale. Instantiated off-scene, measured,
        /// destroyed. Returns 0 when the prefab has no renderers.</summary>
        private static float MeasurePrefabYExtent(GameObject prefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            float y = 0f;
            try
            {
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    y = b.size.y;
                }
            }
            finally { Object.DestroyImmediate(inst); }
            return y;
        }
    }
}
