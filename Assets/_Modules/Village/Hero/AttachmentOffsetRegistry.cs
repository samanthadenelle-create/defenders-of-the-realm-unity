// =============================================================================
// AttachmentOffsetRegistry -- runtime loader for the Offset Forge's authored
// attachment offsets (WO-490 slice 2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT THIS IS:
//   The Offset Forge editor window (Tools > Offset Forge, Assets/OffsetForge/)
//   lets the owner dial in a model's attachment offset by eye and SAVE it to
//   Assets/OffsetForge/offsets.json. That file is an OffsetTable: a flat list of
//   { id, rot (euler degrees), pos (local position), scale } records, keyed by a
//   stable string id (defaulted to the model/prefab name, e.g. "sword_A").
//
//   This registry LOADS that same JSON at runtime and exposes TryGetOffset(key)
//   so the equip/attach path can apply the authored offset to a weapon prop the
//   instant it parents to the hand bone -- instead of euler-guessing. It reads the
//   EXACT file the tool writes (no forked format): a tiny self-contained mirror of
//   the OffsetForge.OffsetTable schema (id/rot/pos/scale) so DeNelle.Village does
//   not need a cross-assembly reference on OffsetForge.Runtime.
//
// CONVENTION (must match the tool's preview, OffsetForgeWindow.ApplyOffsetToInstance):
//   localRotation = Quaternion.Euler(rot);  localPosition = pos;  localScale = one*scale.
//   The apply site (EquipmentController) composes these onto the grip root.
//
// SOURCE OF THE JSON (in priority order):
//   1) Application.dataPath/OffsetForge/offsets.json  -- where the tool writes it.
//      Present in the Editor (where the owner validates) and in any build that
//      ships the Assets-relative file alongside the player. This is the primary.
//   2) Resources/OffsetForge/offsets.json (TextAsset) -- optional build-safe copy
//      if/when the file is mirrored under a Resources folder. Tried as a fallback.
//   Loaded ONCE and cached; Reload() is exposed for tooling/tests.
//
// SAFE BY DEFAULT: a missing/empty/invalid file yields an empty table, so a weapon
// with NO authored offset returns false from TryGetOffset and the caller keeps its
// existing behaviour (no regression).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>One authored attachment offset (mirrors OffsetForge.OffsetEntry).</summary>
    public struct AttachmentOffset
    {
        public Vector3 pos;        // local position
        public Vector3 eulerRot;   // local rotation, euler degrees
        public float scale;        // uniform scale (1 = unchanged)
        // WO-551: when true (native-only, opt-in per entry), the equip path SKIPS the geometric
        // true+seat and reproduces the Forge raw-pivot frame exactly (legacy replacement). Default
        // false => the offset is a NUDGE applied ON TOP of geometry. A missing JSON key reads false.
        public bool fullOverride;
    }

    /// <summary>
    /// Static, cache-once loader for the Offset Forge's offsets.json. Keyed by the
    /// id the tool saved (model/prefab name). Returns false for any unknown key so
    /// callers fall back to their existing behaviour.
    /// </summary>
    public static class AttachmentOffsetRegistry
    {
        // Where the Offset Forge editor window writes (OffsetForgeWindow._savePath default).
        private const string DataPathRelative = "OffsetForge/offsets.json";
        // Optional Resources fallback path (no extension for Resources.Load).
        private const string ResourcesPath = "OffsetForge/offsets";

        // ---- JSON mirror of OffsetForge.OffsetTable (JsonUtility-compatible) -------
        [Serializable] private struct JsonVec3 { public float x; public float y; public float z; }

        [Serializable]
        private class JsonEntry
        {
            public string id;
            public JsonVec3 rot;
            public JsonVec3 pos;
            public float scale;
            public bool fullOverride;   // WO-551: opt-in geometry bypass (default false / missing key).
        }

        [Serializable]
        private class JsonTable
        {
            public List<JsonEntry> offsets = new List<JsonEntry>();
        }

        private static Dictionary<string, AttachmentOffset> s_map;
        private static bool s_loaded;

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;
            s_map = new Dictionary<string, AttachmentOffset>(StringComparer.OrdinalIgnoreCase);

            string json = ReadJson();
            if (string.IsNullOrEmpty(json))
            {
                FlowTrace.Step("Offset", "no offsets.json found (dataPath/Resources) -- registry empty (identity preserved).");
                return;
            }

            JsonTable table = null;
            try { table = JsonUtility.FromJson<JsonTable>(json); }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"failed to parse offsets.json: {ex.Message} -- registry empty.");
                return;
            }

            if (table == null || table.offsets == null) return;
            int n = 0;
            foreach (var e in table.offsets)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                s_map[e.id] = new AttachmentOffset
                {
                    pos = new Vector3(e.pos.x, e.pos.y, e.pos.z),
                    eulerRot = new Vector3(e.rot.x, e.rot.y, e.rot.z),
                    scale = e.scale <= 0f ? 1f : e.scale,
                    fullOverride = e.fullOverride
                };
                n++;
            }
            FlowTrace.Step("Offset", $"loaded {n} attachment offset(s) from offsets.json.");
        }

        // Read the raw JSON from the tool's data-path file first, then a Resources copy.
        private static string ReadJson()
        {
            try
            {
                string full = Path.Combine(Application.dataPath, DataPathRelative);
                if (File.Exists(full))
                    return File.ReadAllText(full);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"dataPath read failed: {ex.Message} -- trying Resources.");
            }

            var ta = Resources.Load<TextAsset>(ResourcesPath);
            return ta != null ? ta.text : null;
        }

        /// <summary>
        /// True + fills <paramref name="offset"/> when the Offset Forge has an authored
        /// offset for <paramref name="key"/> (the saved id, e.g. a weapon mesh/prefab name).
        /// False (and a default offset) when none is stored -- caller keeps its current behaviour.
        /// </summary>
        public static bool TryGetOffset(string key, out AttachmentOffset offset)
        {
            offset = default;
            if (string.IsNullOrEmpty(key)) return false;
            EnsureLoaded();
            return s_map.TryGetValue(key, out offset);
        }

        /// <summary>Force a re-read on next access (tooling/tests after the Forge re-saves).</summary>
        public static void Reload()
        {
            s_loaded = false;
            s_map = null;
        }
    }
}
