// =============================================================================
// OffsetForgeMirrorSync — RC3b FIX (2026-07-04, weapon-grip editor≠build).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// THE BUG THIS KILLS:
//   The owner dials weapon/structure attachment offsets into the AUTHORING file
//   Assets/OffsetForge/offsets.json. But a player build ships only what lives under
//   Resources — Assets/Resources/OffsetForge/offsets.json — and that MIRROR had drifted
//   stale (missing the authored weapon/structure entries), so AttachmentOffsetRegistry
//   resolved different offsets in a build than in the Editor. The owner's edits never
//   shipped. (AttachmentOffsetRegistry now reads the Resources mirror as canonical, so
//   keeping the mirror in lockstep with the authoring file is what makes editor==build.)
//
// THE FIX:
//   Whenever the authoring offsets.json is (re)imported, MERGE its entries by id into the
//   Resources mirror and re-import the mirror. Merge — NOT overwrite — so the mirror keeps
//   entries that legitimately live ONLY there and are consumed by builders that read the
//   mirror directly (e.g. `bridge_south`, read by CastleMoatBuilder — LOCKED seam canon),
//   and PRESERVES per-entry fields the authoring schema doesn't model (e.g. `scaleXyz` on
//   bridge_south). Authoring entries WIN for shared ids; mirror-only entries are untouched.
//
//   Newtonsoft is used (as CatalogOrientationBaker does) so unknown fields round-trip
//   verbatim — JsonUtility would silently drop `scaleXyz`.
// =============================================================================

using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class OffsetForgeMirrorSync : AssetPostprocessor
    {
        private const string AuthoringPath = "Assets/OffsetForge/offsets.json";
        private const string MirrorPath    = "Assets/Resources/OffsetForge/offsets.json";

        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported != null &&
                imported.Any(p => p != null &&
                    p.Replace('\\', '/').Equals(AuthoringPath, System.StringComparison.OrdinalIgnoreCase));
            if (!touched) return;
            Sync();
        }

        [MenuItem("Defenders/Gear/Sync Offset Forge Mirror")]
        public static void Sync()
        {
            if (!File.Exists(AuthoringPath))
            {
                Debug.LogWarning($"[OffsetForgeMirrorSync] authoring file missing: {AuthoringPath}");
                return;
            }

            JObject authoring;
            try { authoring = JObject.Parse(File.ReadAllText(AuthoringPath)); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[OffsetForgeMirrorSync] parse authoring failed: {ex.Message}");
                return;
            }
            var authEntries = authoring["offsets"] as JArray;
            if (authEntries == null) { Debug.LogWarning("[OffsetForgeMirrorSync] no 'offsets' array in authoring."); return; }

            // Start from the existing mirror so mirror-only ids (bridge_south) + their extra
            // fields (scaleXyz) survive; fall back to a fresh table if the mirror is absent.
            JObject mirror = null;
            if (File.Exists(MirrorPath))
            {
                try { mirror = JObject.Parse(File.ReadAllText(MirrorPath)); } catch { mirror = null; }
            }
            if (mirror == null) mirror = new JObject();
            var mirrorEntries = mirror["offsets"] as JArray;
            if (mirrorEntries == null) { mirrorEntries = new JArray(); mirror["offsets"] = mirrorEntries; }

            int upserted = 0;
            foreach (var ae in authEntries.OfType<JObject>())
            {
                string id = ae.Value<string>("id");
                if (string.IsNullOrEmpty(id)) continue;

                // Authoring wins for this id: replace the mirror entry's known fields but
                // PRESERVE any extra fields the mirror carried (e.g. scaleXyz) by merging.
                var existing = mirrorEntries.OfType<JObject>()
                    .FirstOrDefault(m => string.Equals(m.Value<string>("id"), id, System.StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    mirrorEntries.Add(ae.DeepClone());
                }
                else
                {
                    // Overwrite known keys from authoring; leave mirror-only keys (scaleXyz) intact.
                    foreach (var prop in ae.Properties())
                        existing[prop.Name] = prop.Value.DeepClone();
                }
                upserted++;
            }

            // NOTE: entries present ONLY in the mirror (e.g. bridge_south) are deliberately NOT
            // removed — they are consumed by builders that read the mirror directly and are not
            // part of the weapon authoring set.

            File.WriteAllText(MirrorPath, mirror.ToString(Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.ImportAsset(MirrorPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[OffsetForgeMirrorSync] synced {upserted} authoring entr(ies) -> {MirrorPath} " +
                      "(mirror-only ids like bridge_south preserved). OFFSET_MIRROR_SYNC_OK");
        }
    }
}
