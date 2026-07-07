// =============================================================================
// AttachmentOffsetRegistry -- runtime loader for the Offset Forge's authored
// attachment offsets (WO-490 slice 2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// LOAD ORDER (later wins per id):
//   1) Resources/OffsetForge/offsets.json — shipped defaults (build/editor parity).
//   2) Application.persistentDataPath/attachment-offsets.json — LOCAL USER SETTINGS;
//      every in-game Save writes here first. Survives reboots; always wins over shipped.
//   Legacy offsets-dev.json is migrated into attachment-offsets.json on first read.
//   A PlayerPrefs mirror (dotr.attachment-offsets) backs up the user file.
//
// Reload() is called before every equip and immediately after every Save so the live
// hero always reads the persisted config — never a stale cache.
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
        public bool fullOverride;
    }

    /// <summary>
    /// Loads attachment offsets: shipped defaults + local user settings overlay.
    /// User settings in persistentDataPath always win per id.
    /// </summary>
    public static class AttachmentOffsetRegistry
    {
        private const string DataPathRelative = "OffsetForge/offsets.json";
        private const string ResourcesPath = "OffsetForge/offsets";
        private const string UserFileName = "attachment-offsets.json";
        private const string LegacyDevFileName = "offsets-dev.json";
        private const string PlayerPrefsKey = "dotr.attachment-offsets";

        private static string UserFilePath =>
            Path.Combine(Application.persistentDataPath, UserFileName);
        private static string LegacyDevFilePath =>
            Path.Combine(Application.persistentDataPath, LegacyDevFileName);

        /// <summary>Writable local settings path (persistentDataPath/attachment-offsets.json).</summary>
        public static string DevPath => UserFilePath;

        [Serializable] private struct JsonVec3 { public float x; public float y; public float z; }

        [Serializable]
        private class JsonEntry
        {
            public string id;
            public JsonVec3 rot;
            public JsonVec3 pos;
            public float scale;
            public bool fullOverride;
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
            LoadFromDisk();
        }

        /// <summary>Re-read shipped defaults + local user settings from disk.</summary>
        public static void Reload()
        {
            s_loaded = false;
            s_map = null;
            LoadFromDisk();
        }

        private static void LoadFromDisk()
        {
            s_loaded = true;
            s_map = new Dictionary<string, AttachmentOffset>(StringComparer.OrdinalIgnoreCase);

            int baseN = ApplyTable(ReadBaseJson(), "base");
            int userN = ApplyTable(ReadUserJson(), "user");
            FlowTrace.Step("Offset",
                $"loaded attachment offsets: {baseN} shipped + {userN} local -> {s_map.Count} effective " +
                $"(user file='{UserFilePath}').");
        }

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

        // RC3b FIX (2026-07-04, banner restored 2026-07-07 — do NOT re-invert this order): read the
        // RESOURCES mirror FIRST — it is the ONE copy that actually SHIPS in a player build, so
        // Resources-first means the runtime resolves the SAME offsets file in the Editor AND in a
        // build. The 3-month editor≠build root cause was the reverse order: the Editor read the
        // authoring dataPath file while the build silently fell to a stale Resources mirror, so the
        // owner's dialed offsets never shipped. The authoring file (Assets/OffsetForge/offsets.json)
        // is kept byte-synced into the mirror by the editor OffsetForgeMirrorSync postprocessor;
        // the dataPath read below is ONLY an Editor fallback for a not-yet-synced fresh edit.
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

        // Local user settings — primary runtime authority. Migrates legacy offsets-dev.json once.
        private static string ReadUserJson()
        {
            try
            {
                if (File.Exists(UserFilePath))
                    return File.ReadAllText(UserFilePath);

                // Migrate legacy dev file into the canonical user settings path. Delete the legacy
                // file after a successful copy — otherwise a stale offsets-dev.json would win over
                // the FRESHER PlayerPrefs backup if the user file is ever lost (restore order below).
                if (File.Exists(LegacyDevFilePath))
                {
                    string legacy = File.ReadAllText(LegacyDevFilePath);
                    try
                    {
                        Directory.CreateDirectory(Application.persistentDataPath);
                        File.WriteAllText(UserFilePath, legacy);
                        MirrorToPlayerPrefs(legacy);
                        File.Delete(LegacyDevFilePath);
                        FlowTrace.Step("Offset",
                            $"migrated legacy '{LegacyDevFilePath}' -> '{UserFilePath}' (legacy removed).");
                    }
                    catch (Exception ex)
                    {
                        FlowTrace.Warn("Offset", $"legacy migration write failed: {ex.Message} — reading legacy in-memory.");
                    }
                    return legacy;
                }

                // Last resort: restore from PlayerPrefs backup if the file was lost.
                if (PlayerPrefs.HasKey(PlayerPrefsKey))
                {
                    string backup = PlayerPrefs.GetString(PlayerPrefsKey);
                    if (!string.IsNullOrEmpty(backup))
                    {
                        try
                        {
                            Directory.CreateDirectory(Application.persistentDataPath);
                            File.WriteAllText(UserFilePath, backup);
                            FlowTrace.Step("Offset", $"restored user offsets from PlayerPrefs backup -> '{UserFilePath}'.");
                        }
                        catch (Exception ex)
                        {
                            FlowTrace.Warn("Offset", $"PlayerPrefs restore write failed: {ex.Message} — using backup in-memory.");
                        }
                        return backup;
                    }
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"user offsets read failed: {ex.Message} -- ignored.");
            }
            return null;
        }

        public static bool TryGetOffset(string key, out AttachmentOffset offset)
        {
            offset = default;
            if (string.IsNullOrEmpty(key)) return false;
            EnsureLoaded();
            return s_map.TryGetValue(key, out offset);
        }

        /// <summary>
        /// Persist one offset to local user settings (always) + repo in Editor. Reloads immediately.
        /// </summary>
        public static bool SaveOffset(string id, Vector3 pos, Vector3 euler, float scale,
                                      bool fullOverride, out string devPath, out string snippet)
        {
            devPath = UserFilePath;
            snippet = BuildSnippet(id, pos, euler, scale, fullOverride);
            if (string.IsNullOrEmpty(id))
            {
                FlowTrace.Fail("Offset", "SaveOffset: empty id -- nothing written.");
                return false;
            }

            bool userOk = UpsertFile(UserFilePath, id, pos, euler, scale, fullOverride, false);
#if UNITY_EDITOR
            try
            {
                string repo = Path.Combine(Application.dataPath, DataPathRelative);
                UpsertFile(repo, id, pos, euler, scale, fullOverride, true);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"repo offsets.json write failed: {ex.Message} (user file written).");
            }
#endif
            if (userOk)
                MirrorUserFileToPlayerPrefs();
            Reload();
            FlowTrace.Step("Offset", $"SaveOffset '{id}' -> user='{UserFilePath}' (ok={userOk}); snippet logged for repo bake.");
            return userOk;
        }

        public static bool RemoveOffset(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            bool any = RemoveFromFile(UserFilePath, id);
#if UNITY_EDITOR
            try { any |= RemoveFromFile(Path.Combine(Application.dataPath, DataPathRelative), id); }
            catch (Exception ex) { FlowTrace.Warn("Offset", $"repo offsets.json remove failed: {ex.Message}."); }
#endif
            if (any)
                MirrorUserFileToPlayerPrefs();
            Reload();
            FlowTrace.Step("Offset", $"RemoveOffset '{id}' (removed={any}).");
            return any;
        }

        private static void MirrorToPlayerPrefs(string json)
        {
            try
            {
                PlayerPrefs.SetString(PlayerPrefsKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"PlayerPrefs mirror failed: {ex.Message}.");
            }
        }

        private static void MirrorUserFileToPlayerPrefs()
        {
            try
            {
                if (File.Exists(UserFilePath))
                    MirrorToPlayerPrefs(File.ReadAllText(UserFilePath));
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"PlayerPrefs mirror read failed: {ex.Message}.");
            }
        }

        private static bool UpsertFile(string path, string id, Vector3 pos, Vector3 euler,
                                       float scale, bool fullOverride, bool makeDir)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

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
