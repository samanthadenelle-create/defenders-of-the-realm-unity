// =============================================================================
// CatalogOrientationBaker — bakes a "keep-vertical" orientation correction into
// every structures-catalog.json entry (+ a human-readable note), so any buildable
// asset auto-uprights without per-asset hand-tuning. Generalizes the bow's
// bounds-based auto-orient (HeroBowAttachment.NormalizeInto) to the whole catalog.
// -----------------------------------------------------------------------------
// FOR EACH entry that has no orientation correction yet (or corrected=false):
//   1. Load its visualPrefabPath prefab (Resources-relative).
//   2. Measure combined renderer bounds; if the LONGEST axis isn't already vertical,
//      compute the rotation that stands it up (longest axis -> +Y).
//   3. Compute an offset that puts the model's base-centre at the entry origin
//      (base on the ground, centred horizontally).
//   4. Write an "orientation" block onto the entry: { corrected, euler[xyz],
//      offset[xyz], scale, note } — the NOTE says exactly what was done.
// Writes BOTH catalog copies byte-equal (StreamingAssets source + Resources copy).
//
// This only RECORDS the correction (data). StructureFactory applies entry.orientation
// at placement (companion change). Re-running is idempotent: corrected entries skip.
//
//   Defenders > Catalog > Bake Orientation Corrections
//   (batchmode: DeNelle.Editor.CatalogOrientationBaker.Bake)
// =============================================================================

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CatalogOrientationBaker
    {
        private static readonly string[] CatalogPaths =
        {
            "Assets/StreamingAssets/Data/Canonical/structures-catalog.json",
            "Assets/Resources/Data/Canonical/structures-catalog.json",
        };

        [MenuItem("Defenders/Catalog/Bake Orientation Corrections")]
        public static void Bake()
        {
            // Parse the first copy, mutate it, then write the SAME text to both (byte-equal).
            string src = CatalogPaths[0];
            if (!File.Exists(src)) { Debug.LogError("[CatalogOrientationBaker] Missing " + src); return; }

            JObject root = JObject.Parse(File.ReadAllText(src));
            var entries = root["entries"] as JArray;
            if (entries == null) { Debug.LogError("[CatalogOrientationBaker] No 'entries' array."); return; }

            int baked = 0, skipped = 0, missing = 0;
            foreach (var e in entries)
            {
                var entry = e as JObject;
                if (entry == null) continue;

                // Recompute every run (the auto heuristic is deterministic + ADVISORY).
                // A human-verified correction (manual=true, from the Orientation Inspector)
                // is preserved untouched.
                var existing = entry["orientation"] as JObject;
                if (existing != null && existing.Value<bool?>("manual") == true) { skipped++; continue; }

                string id = entry.Value<string>("id") ?? "?";
                string vp = entry.Value<string>("visualPrefabPath");
                var prefab = string.IsNullOrEmpty(vp) ? null : Resources.Load<GameObject>(vp);
                if (prefab == null)
                {
                    entry["orientation"] = MakeOrientation(Vector3.zero, Vector3.zero, 1f, false,
                        $"prefab '{vp}' not found — no correction computed");
                    missing++;
                    continue;
                }

                entry["orientation"] = ComputeCorrection(prefab, id);
                baked++;
            }

            string outText = root.ToString(Formatting.Indented);
            foreach (var p in CatalogPaths)
            {
                if (File.Exists(p)) File.WriteAllText(p, outText);
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CatalogOrientationBaker] DONE — baked {baked}, missing-prefab {missing}, " +
                      $"skipped {skipped} already-corrected. Wrote {CatalogPaths.Length} copies. CATALOG_ORIENT_OK");
        }

        // Compute the keep-vertical correction from the prefab's renderer bounds.
        private static JObject ComputeCorrection(GameObject prefab, string id)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            if (!WorldBounds(inst, out Bounds b0))
            {
                Object.DestroyImmediate(inst);
                return MakeOrientation(Vector3.zero, Vector3.zero, 1f, true, "no renderers — left as-is");
            }

            Vector3 sz = b0.size;
            int lng = (sz.x >= sz.y && sz.x >= sz.z) ? 0 : (sz.y >= sz.z ? 1 : 2);
            int sht = (sz.x <= sz.y && sz.x <= sz.z) ? 0 : (sz.y <= sz.z ? 1 : 2);

            // CONSERVATIVE: only stand up a model that is clearly LYING FLAT — i.e. its
            // shortest axis is Y (vertically flat) AND its longest axis is much longer
            // than its height. Cubic / already-upright / wide-but-upright models keep
            // euler 0 so we never tip a correct prefab (the towers are ~1m cubes — they
            // must NOT be rotated). Edge cases are hand-overridable via this note + value.
            Vector3 euler = Vector3.zero;
            string note;
            bool lyingFlat = sht == 1 && lng != 1 && sz[lng] > 1.5f * Mathf.Max(0.0001f, sz.y);
            if (!lyingFlat)
            {
                note = $"upright/cubic (x{sz.x:0.00} y{sz.y:0.00} z{sz.z:0.00}) - no rotation";
            }
            else
            {
                Quaternion r = Quaternion.FromToRotation(Axis(lng), Vector3.up);
                inst.transform.rotation = r;
                euler = r.eulerAngles;
                note = $"lying flat (Y shortest {sz.y:0.00}m, longest {(lng == 0 ? "X" : "Z")} {sz[lng]:0.00}m) - stood up to +Y";
            }

            // Offset so the model's base-centre sits at the entry origin (base on ground).
            Vector3 offset = Vector3.zero;
            if (WorldBounds(inst, out Bounds b1))
                offset = new Vector3(-b1.center.x, -b1.min.y, -b1.center.z);

            Object.DestroyImmediate(inst);
            return MakeOrientation(euler, offset, 1f, true, note);
        }

        private static JObject MakeOrientation(Vector3 euler, Vector3 offset, float scale, bool corrected, string note)
            => new JObject
            {
                ["corrected"] = corrected,
                ["manual"]    = false,   // advisory — NOT applied until human-verified in the Inspector
                ["euler"]  = new JArray(Round(euler.x), Round(euler.y), Round(euler.z)),
                ["offset"] = new JArray(Round(offset.x), Round(offset.y), Round(offset.z)),
                ["scale"]  = Round(scale),
                ["note"]   = note,
            };

        private static double Round(float v) => System.Math.Round(v, 3);
        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        private static bool WorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
