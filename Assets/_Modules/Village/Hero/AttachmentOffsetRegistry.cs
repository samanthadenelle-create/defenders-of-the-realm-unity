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
        // WRITABLE dev-override file (WO-577). In a built player Application.dataPath is read-only,
        // so the in-game Seating Editor persists here; this file is loaded AS AN OVERLAY on top of
        // the repo offsets.json (dev entries WIN per id), so a saved offset survives a reload + the
        // next launch of the same build. The owner also gets a JSON snippet to bake into the repo.
        private static string DevFilePath => Path.Combine(Application.persistentDataPath, "offsets-dev.json");

        /// <summary>The writable dev-override file path (Application.persistentDataPath/offsets-dev.json).</summary>
        public static string DevPath => DevFilePath;

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

            int baseN = ApplyTable(ReadBaseJson(), "base");
            int devN  = ApplyTable(ReadDevJson(),  "dev");   // dev overlay WINS per id
            FlowTrace.Step("Offset", $"loaded attachment offsets: {baseN} base + {devN} dev-override -> {s_map.Count} effective.");
        }

        // Parse a JSON table and upsert each entry into the map (later calls overwrite earlier =
        // the dev overlay winning). Returns the count applied. Null/empty/invalid -> 0 (safe).
        private static int ApplyTable(string json, string label)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            JsonTable table = null;
            try { table = JsonUtility.FromJson<JsonTable>(json); }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"failed to parse {label} offsets json: {ex.Message} -- skipped.");
                return 0;
            }
            if (table == null || table.offsets == null) return 0;
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
            return n;
        }

        // RC3b FIX (2026-07-04): read the RESOURCES mirror FIRST — it is the ONE copy that actually
        // SHIPS in a player build, so making it the canonical base means the runtime resolves the
        // SAME offsets file in the Editor AND in a build (the 3-month editor≠build root cause was the
        // reverse order: the Editor read the authoring dataPath file while the build silently fell to
        // a stale Resources mirror, so the owner's dialed offsets never shipped). The authoring file
        // (Assets/OffsetForge/offsets.json) is kept byte-synced into this mirror by the editor
        // OffsetForgeMirrorSync postprocessor, so Resources-first loses nothing in the Editor. The
        // dataPath file is retained ONLY as an Editor fallback for a not-yet-synced fresh edit.
        private static string ReadBaseJson()
        {
            var ta = Resources.Load<TextAsset>(ResourcesPath);
            if (ta != null && !string.IsNullOrEmpty(ta.text)) return ta.text;

            try
            {
                string full = Path.Combine(Application.dataPath, DataPathRelative);
                if (File.Exists(full))
                    return File.ReadAllText(full);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"dataPath fallback read failed: {ex.Message}.");
            }
            return null;
        }

        // Read the writable dev-override file (Application.persistentDataPath/offsets-dev.json).
        // Absent in a fresh build until the Seating Editor saves -> returns null (no overlay).
        private static string ReadDevJson()
        {
            try
            {
                if (File.Exists(DevFilePath)) return File.ReadAllText(DevFilePath);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"dev offsets read failed: {ex.Message} -- ignoring overlay.");
            }
            return null;
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

        // ── WRITER (WO-577 — in-game Seating Editor persistence) ─────────────────────

        /// <summary>
        /// Upsert one authored offset and persist it. ALWAYS writes the writable dev-override file
        /// (Application.persistentDataPath/offsets-dev.json) so the change survives in a built player;
        /// in the Editor it ALSO writes the repo file (Assets/OffsetForge/offsets.json) so the source
        /// of truth is updated directly. Reloads the cache so the next equip applies it immediately.
        /// Returns the dev path + a copy-pasteable single-entry JSON snippet (for baking into the
        /// repo offsets.json from a build, where the repo file is not writable).
        /// </summary>
        public static bool SaveOffset(string id, Vector3 pos, Vector3 euler, float scale,
                                      bool fullOverride, out string devPath, out string snippet)
        {
            devPath = DevFilePath;
            snippet = BuildSnippet(id, pos, euler, scale, fullOverride);
            if (string.IsNullOrEmpty(id))
            {
                FlowTrace.Fail("Offset", "SaveOffset: empty id -- nothing written.");
                return false;
            }

            bool devOk = UpsertFile(DevFilePath, id, pos, euler, scale, fullOverride, false);
#if UNITY_EDITOR
            // In the Editor, write straight into the committed repo file too.
            try
            {
                string repo = Path.Combine(Application.dataPath, DataPathRelative);
                UpsertFile(repo, id, pos, euler, scale, fullOverride, true);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"repo offsets.json write failed: {ex.Message} (dev file written).");
            }
#endif
            Reload();
            FlowTrace.Step("Offset", $"SaveOffset '{id}' -> dev='{DevFilePath}' (ok={devOk}); snippet logged for repo bake.");
            return devOk;
        }

        /// <summary>Remove an authored offset from the dev file (and the repo file in the Editor), then reload.</summary>
        public static bool RemoveOffset(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            bool any = RemoveFromFile(DevFilePath, id);
#if UNITY_EDITOR
            try { any |= RemoveFromFile(Path.Combine(Application.dataPath, DataPathRelative), id); }
            catch (Exception ex) { FlowTrace.Warn("Offset", $"repo offsets.json remove failed: {ex.Message}."); }
#endif
            Reload();
            FlowTrace.Step("Offset", $"RemoveOffset '{id}' (removed={any}).");
            return any;
        }

        // Read (or create) a JsonTable at path, upsert the entry, write it back (pretty). When
        // makeDir is true the parent directory is created first (repo path under Assets/).
        private static bool UpsertFile(string path, string id, Vector3 pos, Vector3 euler,
                                       float scale, bool fullOverride, bool makeDir)
        {
            try
            {
                if (makeDir)
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }

                JsonTable table = null;
                if (File.Exists(path))
                {
                    try { table = JsonUtility.FromJson<JsonTable>(File.ReadAllText(path)); }
                    catch { table = null; }
                }
                if (table == null) table = new JsonTable();
                if (table.offsets == null) table.offsets = new List<JsonEntry>();

                JsonEntry entry = table.offsets.Find(e => e != null &&
                    string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
                if (entry == null) { entry = new JsonEntry { id = id }; table.offsets.Add(entry); }
                entry.id = id;
                entry.pos = new JsonVec3 { x = pos.x, y = pos.y, z = pos.z };
                entry.rot = new JsonVec3 { x = euler.x, y = euler.y, z = euler.z };
                entry.scale = scale <= 0f ? 1f : scale;
                entry.fullOverride = fullOverride;

                File.WriteAllText(path, JsonUtility.ToJson(table, true));
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"UpsertFile '{path}' failed: {ex.Message}.");
                return false;
            }
        }

        private static bool RemoveFromFile(string path, string id)
        {
            try
            {
                if (!File.Exists(path)) return false;
                JsonTable table = JsonUtility.FromJson<JsonTable>(File.ReadAllText(path));
                if (table == null || table.offsets == null) return false;
                int before = table.offsets.Count;
                table.offsets.RemoveAll(e => e != null &&
                    string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
                if (table.offsets.Count == before) return false;
                File.WriteAllText(path, JsonUtility.ToJson(table, true));
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"RemoveFromFile '{path}' failed: {ex.Message}.");
                return false;
            }
        }

        // A copy-pasteable single-entry snippet matching offsets.json's schema (for baking back
        // into the repo file from a built player, where Assets/ is not writable).
        private static string BuildSnippet(string id, Vector3 pos, Vector3 euler, float scale, bool fullOverride)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(ci,
                "{{ \"id\": \"{0}\", \"rot\": {{ \"x\": {1:0.###}, \"y\": {2:0.###}, \"z\": {3:0.###} }}, " +
                "\"pos\": {{ \"x\": {4:0.####}, \"y\": {5:0.####}, \"z\": {6:0.####} }}, " +
                "\"scale\": {7:0.###}, \"fullOverride\": {8} }}",
                id, euler.x, euler.y, euler.z, pos.x, pos.y, pos.z,
                scale <= 0f ? 1f : scale, fullOverride ? "true" : "false");
        }
    }
}
