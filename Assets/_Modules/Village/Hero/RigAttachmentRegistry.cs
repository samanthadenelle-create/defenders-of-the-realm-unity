// =============================================================================
// RigAttachmentRegistry -- runtime loader for instantiation-time, rig-agnostic
// weapon/shield ATTACH OVERRIDES (WO-510 slice 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT THIS IS:
//   The MODEL is pristine read-only data -- we NEVER rename its bones or modify
//   the asset (that breaks anim bindings + reverts on re-import). Attachment
//   resolution is a RUNTIME concern applied ONCE at instantiation (weapon/shield
//   attach), through ONE code path every character flows through. The only
//   per-model artifact is an OPTIONAL one-line JSON pointer naming the attach
//   transform. The override is AUTHORITATIVE; the humanoid avatar auto-map is the
//   FALLBACK (owned by EquipmentController, NOT this registry).
//
//   This registry LOADS Assets/OffsetForge/rig-profiles.json and exposes
//   TryResolve(root, rigId, leftHand, out anchor, out how) so the equip/attach
//   path can resolve the attach bone by an authored hierarchy PATH (or unique
//   bone name) BEFORE falling back to animator.GetBoneTransform. It reads a tiny
//   self-contained mirror of the schema (no cross-assembly reference, no deps).
//
// JSON SCHEMA:
//   { "profiles": [ { "rigId": "Knight",
//                     "rightHand": "<transform path or unique name>",
//                     "leftHand":  "<...>" } ] }
//   The hand values are a transform HIERARCHY PATH (e.g.
//   "Armature/Hips/.../RightHand", unambiguous vs duplicate names) OR a plain
//   unique bone name (matched against any descendant transform).
//
// SOURCE OF THE JSON (in priority order -- MIRRORS AttachmentOffsetRegistry):
//   1) Application.dataPath/OffsetForge/rig-profiles.json -- editor authoring path.
//   2) Resources/OffsetForge/rig-profiles (TextAsset) -- optional build-safe copy.
//   Loaded ONCE and cached; Reload() is exposed for tooling/tests.
//
// SAFE BY DEFAULT: a missing/empty/invalid file yields an empty profile set, so a
// rig with NO authored override returns false from TryResolve (how="none") and the
// caller keeps its existing avatar behaviour (ZERO regression).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Static, cache-once loader for the attach OVERRIDE file (rig-profiles.json).
    /// Keyed by rigId (the model/prefab name). Returns false for any unknown rig or
    /// empty hand field so callers fall back to the humanoid avatar (no regression).
    /// </summary>
    public static class RigAttachmentRegistry
    {
        // Editor authoring path (sibling of offsets.json).
        private const string DataPathRelative = "OffsetForge/rig-profiles.json";
        // Optional Resources fallback path (no extension for Resources.Load).
        private const string ResourcesPath = "OffsetForge/rig-profiles";

        // ---- JSON mirror (JsonUtility-compatible) ----------------------------------
        [Serializable]
        private class JsonProfile
        {
            public string rigId;
            public string rightHand;
            public string leftHand;
        }

        [Serializable]
        private class JsonProfileTable
        {
            public List<JsonProfile> profiles = new List<JsonProfile>();
        }

        // rigId -> (rightHand path/name, leftHand path/name).
        private static Dictionary<string, JsonProfile> s_map;
        private static bool s_loaded;

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;
            s_map = new Dictionary<string, JsonProfile>(StringComparer.OrdinalIgnoreCase);

            string json = ReadJson();
            if (string.IsNullOrEmpty(json))
            {
                FlowTrace.Step("Offset", "no rig-profiles.json found (dataPath/Resources) -- override registry empty (avatar fallback).");
                return;
            }

            JsonProfileTable table = null;
            try { table = JsonUtility.FromJson<JsonProfileTable>(json); }
            catch (Exception ex)
            {
                FlowTrace.Warn("Offset", $"failed to parse rig-profiles.json: {ex.Message} -- override registry empty.");
                return;
            }

            if (table == null || table.profiles == null) return;
            int n = 0;
            foreach (var p in table.profiles)
            {
                if (p == null || string.IsNullOrEmpty(p.rigId)) continue;
                s_map[p.rigId] = p;
                n++;
            }
            FlowTrace.Step("Offset", $"loaded {n} rig attach override profile(s) from rig-profiles.json.");
        }

        // Read the raw JSON from the data-path file first, then a Resources copy.
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
                FlowTrace.Warn("Offset", $"rig-profiles dataPath read failed: {ex.Message} -- trying Resources.");
            }

            var ta = Resources.Load<TextAsset>(ResourcesPath);
            return ta != null ? ta.text : null;
        }

        /// <summary>
        /// Resolve the attach anchor for <paramref name="rigId"/>/<paramref name="leftHand"/>
        /// from the authored override profile. Tier-1 ONLY -- the caller owns the avatar/name
        /// fallback. Outcomes (via <paramref name="how"/>, never silent):
        ///   no profile / empty hand field -> false, how="none".
        ///   profile path present but NOT in root's hierarchy -> false, how="missing:&lt;path&gt;".
        ///   found -> anchor set, how="json-override", true.
        /// Resolution: exact full-path match first (split on '/', walk from root), then a
        /// fallback exact-name match on any descendant transform.
        /// </summary>
        public static bool TryResolve(GameObject root, string rigId, bool leftHand, out Transform anchor, out string how)
        {
            anchor = null;
            how = "none";
            if (root == null || string.IsNullOrEmpty(rigId)) return false;

            EnsureLoaded();
            if (!s_map.TryGetValue(rigId, out var profile) || profile == null) return false;

            string path = leftHand ? profile.leftHand : profile.rightHand;
            if (string.IsNullOrEmpty(path)) return false;   // no override for this hand -> avatar fallback

            Transform found = FindByPath(root.transform, path) ?? FindByName(root.transform, path);
            if (found == null)
            {
                how = "missing:" + path;
                return false;
            }

            anchor = found;
            how = "json-override";
            return true;
        }

        // Exact full hierarchy-path match: split on '/', walk from root by child name.
        private static Transform FindByPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            Transform current = root;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                Transform next = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    var c = current.GetChild(i);
                    if (string.Equals(c.name, part, StringComparison.Ordinal)) { next = c; break; }
                }
                if (next == null) return null;
                current = next;
            }
            return current == root ? null : current;
        }

        // Fallback exact-name match on any descendant transform (first match wins).
        private static Transform FindByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root) continue;
                if (string.Equals(t.name, name, StringComparison.Ordinal)) return t;
            }
            return null;
        }

        /// <summary>Force a re-read on next access (tooling/tests after the Forge re-saves).</summary>
        public static void Reload()
        {
            s_loaded = false;
            s_map = null;
        }
    }
}
