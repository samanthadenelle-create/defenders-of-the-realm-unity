// =============================================================================
// BlinkPrefabMirror — the P0 CENTERPIECE (owner ruling 2026-07-03: "why recreate
// the wheel when someone shows us a fully functioning car").
//
// The Blink Obsidian pack ships complete, assembled uGUI prefabs
// (Prefabs_Obsidian/). The pack is GITIGNORED, so this tool mirrors the
// HUD-critical prefabs into COMMITTED Resources so the game can use the real,
// fully-functioning widgets on a fresh clone. Per prefab:
//
//   1. Discover its sprite/font/nested-prefab dependencies from the prefab
//      YAML's GUID refs + the pack's .meta GUID→file map (text-level, exact).
//   2. Ensure each dependency is mirrored into committed Resources/RpgUi —
//      reusing the canonical BlinkUiImporter table destination when one exists
//      (no duplicate textures), else a preserving copy under prefab_deps/ (or
//      font/ for fonts). AssetDatabase.CopyAsset keeps the pack's own import
//      settings (incl. 9-slice borders) under a NEW guid.
//   3. CopyAsset the prefab into Assets/Resources/RpgUi/prefabs/<Name>.prefab
//      and REMAP the copy's guid refs (deterministic text rewrite of the copied
//      YAML) to the mirrored assets' guids. Nested pack prefabs are mirrored
//      transitively and remapped the same way.
//   4. Validate: the mirrored prefab must contain ZERO remaining pack guids and
//      must load; a per-prefab table is logged.
//
// Fresh-clone safe: pack absent ⇒ Debug.LogWarning + no-op — the previously
// COMMITTED mirrored prefabs keep working (that is the point).
//
// Scope v1 = the HUD-critical set (HUDCore, PartyNameplate, TargetNameplate,
// CastBar1-3, QuestTracker, Chat, Minimap + ALL Prefabs_Obsidian/Buttons_Obsidian
// prefabs: DiabloHealth/Mana, Close_Button, Toggle1-3, Bar1-7, Rectangle*/
// Rounded*, Collapse/Expand). Full screens (Inventory/Merchant/…) = second pass.
//
// Run: Defenders > Art > Mirror Blink Prefabs
//      (or batchmode DeNelle.Editor.BlinkPrefabMirror.Run)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BlinkPrefabMirror
    {
        private const string PackRoot   = "Assets/Blink/Art/UI/Obsidian_UI";
        private const string PrefabRoot = PackRoot + "/Prefabs_Obsidian";
        private const string ResRoot    = "Assets/Resources/RpgUi";
        private const string PrefabDst  = ResRoot + "/prefabs";
        private const string DepsDst    = ResRoot + "/prefab_deps";
        private const string FontDst    = ResRoot + "/font";

        // Scope v1: HUD-critical root prefabs (Buttons_Obsidian/* is added wholesale in Run).
        private static readonly string[] RootScope =
        {
            "HUDCore", "PartyNameplate", "TargetNameplate",
            "CastBar1", "CastBar2", "CastBar3",
            "QuestTracker", "Chat", "Minimap",
        };

        [MenuItem("Defenders/Art/Mirror Blink Prefabs")]
        public static void MirrorMenu() { Run(); }

        public static void Run()
        {
            // Fresh-clone safety: pack absent ⇒ warn + no-op; committed mirrors keep working.
            if (!Directory.Exists(PrefabRoot))
            {
                Debug.LogWarning("[BlinkPrefabMirror] Blink prefabs not present (" + PrefabRoot +
                                 ") — skipping. Committed mirrored prefabs under " + PrefabDst + " remain in use.");
                return;
            }

            // Pack guid → pack asset path (from every .meta under the pack).
            var packGuidToPath = BuildPackGuidMap(PackRoot);

            // ── 1. Collect the prefab set (scope + transitively referenced pack prefabs) ──
            var queue = new Queue<string>();
            var prefabSet = new List<string>();      // pack prefab paths, dependency-discovered order
            var seenPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in RootScope)
            {
                string p = PrefabRoot + "/" + name + ".prefab";
                if (File.Exists(p)) { if (seenPrefabs.Add(p)) queue.Enqueue(p); }
                else Debug.LogWarning("[BlinkPrefabMirror] scope prefab missing in pack (skipped): " + p);
            }
            string buttonsDir = PrefabRoot + "/Buttons_Obsidian";
            if (Directory.Exists(buttonsDir))
                foreach (var p in Directory.GetFiles(buttonsDir, "*.prefab", SearchOption.TopDirectoryOnly))
                {
                    string norm = p.Replace('\\', '/');
                    if (seenPrefabs.Add(norm)) queue.Enqueue(norm);
                }

            var assetDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // pack guids of non-prefab deps
            var prefabTexts = new Dictionary<string, string>();                     // pack prefab path -> yaml text

            while (queue.Count > 0)
            {
                string prefabPath = queue.Dequeue();
                prefabSet.Add(prefabPath);
                string text = File.ReadAllText(prefabPath);
                prefabTexts[prefabPath] = text;

                foreach (var guid in ScanGuids(text))
                {
                    if (!packGuidToPath.TryGetValue(guid, out var depPath)) continue; // engine/package script etc.
                    if (depPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        string norm = depPath.Replace('\\', '/');
                        if (seenPrefabs.Add(norm)) queue.Enqueue(norm);
                    }
                    else assetDeps.Add(guid);
                }
            }

            // ── 2. Mirror non-prefab dependencies; build oldGuid → newGuid remap ──
            var remap = new Dictionary<string, string>();
            int depsMirrored = 0, depsReused = 0, depsFailed = 0;
            foreach (var oldGuid in assetDeps)
            {
                string src = packGuidToPath[oldGuid].Replace('\\', '/');
                string dst = MirrorDependency(src, ref depsMirrored, ref depsReused, ref depsFailed);
                if (dst == null) continue;
                string newGuid = AssetDatabase.AssetPathToGUID(dst);
                if (!string.IsNullOrEmpty(newGuid) && newGuid != oldGuid) remap[oldGuid] = newGuid;
            }

            // ── 3. Copy every prefab, then add prefab-guid remaps (nested refs) ──
            BlinkUiImporter.EnsureFolder(PrefabDst);
            var mirrored = new List<(string src, string dst)>();
            foreach (var src in prefabSet)
            {
                string dst = PrefabDst + "/" + Path.GetFileNameWithoutExtension(src) + ".prefab";
                if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(src, dst))
                {
                    Debug.LogWarning("[BlinkPrefabMirror] prefab copy failed: " + src + " -> " + dst);
                    continue;
                }
                mirrored.Add((src, dst));
                string oldGuid = AssetDatabase.AssetPathToGUID(src);
                string newGuid = AssetDatabase.AssetPathToGUID(dst);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid) && oldGuid != newGuid)
                    remap[oldGuid] = newGuid;
            }

            // ── 4. Rewrite each copied prefab's guid refs, reimport, validate ──
            var report = new StringBuilder();
            report.Append("[BlinkPrefabMirror] per-prefab mirror + validation:\n");
            report.Append("  PREFAB                        REMAPPED  UNRESOLVED  LOADS\n");
            int ok = 0, bad = 0;
            foreach (var (src, dst) in mirrored)
            {
                // Single deterministic text pass: rewrite every mapped "guid: <old>" → "guid: <new>".
                string original = File.ReadAllText(dst);
                string text = original;
                int remapped = 0;
                foreach (var kv in remap)
                {
                    int hits = CountOf(text, "guid: " + kv.Key);
                    if (hits > 0) { text = text.Replace("guid: " + kv.Key, "guid: " + kv.Value); remapped += hits; }
                }
                if (text != original) File.WriteAllText(dst, text, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);

                // Validation A: zero remaining pack guids in the mirrored YAML.
                int unresolved = 0;
                foreach (var guid in ScanGuids(text))
                    if (packGuidToPath.ContainsKey(guid)) unresolved++;

                // Validation B: the mirrored prefab loads.
                bool loads = AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null;

                bool pass = unresolved == 0 && loads;
                if (pass) ok++; else bad++;
                report.Append("  ").Append(Path.GetFileNameWithoutExtension(dst).PadRight(30))
                      .Append(remapped.ToString().PadRight(10))
                      .Append(unresolved.ToString().PadRight(12))
                      .Append(loads ? "yes" : "NO")
                      .Append(pass ? "" : "   << FAIL").Append('\n');
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Append("[BlinkPrefabMirror] done — " + mirrored.Count + " prefab(s) mirrored (" + ok + " valid, " +
                          bad + " failing), deps: " + depsMirrored + " mirrored, " + depsReused +
                          " reused canonical/existing, " + depsFailed + " failed.");
            if (bad > 0) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());
        }

        // Mirror one non-prefab pack dependency into committed Resources; returns dst path.
        private static string MirrorDependency(string src, ref int mirroredCount, ref int reusedCount, ref int failedCount)
        {
            string packRel = src.StartsWith(PackRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? src.Substring(PackRoot.Length + 1) : src;
            string ext = Path.GetExtension(src).ToLowerInvariant();
            string dst;

            if (ext == ".png" && BlinkUiImporter.TryCanonicalDst(packRel, out var canonical, out int border))
            {
                // Canonical table asset — reuse the importer's mirror (create it if not yet imported).
                dst = canonical;
                if (!File.Exists(dst))
                {
                    BlinkUiImporter.EnsureFolder(Path.GetDirectoryName(dst).Replace('\\', '/'));
                    if (!AssetDatabase.CopyAsset(src, dst)) { failedCount++; return Fail(src, dst); }
                    BlinkUiImporter.ForceSprite(dst, border);
                    mirroredCount++;
                }
                else reusedCount++;
                return dst;
            }

            // Non-table dependency: preserving copy (CopyAsset keeps the pack's import
            // settings — incl. its own 9-slice borders — under a new guid).
            string folder = (ext == ".ttf" || ext == ".otf" || ext == ".asset") ? FontDst : DepsDst;
            dst = folder + "/" + Sanitize(Path.GetFileNameWithoutExtension(src)) + ext;
            if (File.Exists(dst)) { reusedCount++; return dst; }
            BlinkUiImporter.EnsureFolder(folder);
            if (!AssetDatabase.CopyAsset(src, dst)) { failedCount++; return Fail(src, dst); }
            mirroredCount++;
            return dst;
        }

        private static string Fail(string src, string dst)
        {
            Debug.LogWarning("[BlinkPrefabMirror] dependency copy failed: " + src + " -> " + dst);
            return null;
        }

        private static string Sanitize(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        /// All 32-hex guids appearing as "guid: <hex32>" in a Unity YAML text.
        private static IEnumerable<string> ScanGuids(string text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while ((i = text.IndexOf("guid: ", i, StringComparison.Ordinal)) >= 0)
            {
                i += 6;
                if (i + 32 <= text.Length)
                {
                    string g = text.Substring(i, 32);
                    if (IsHex32(g) && seen.Add(g)) yield return g;
                }
            }
        }

        private static bool IsHex32(string s)
        {
            if (s.Length != 32) return false;
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        private static int CountOf(string text, string token)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { count++; i += token.Length; }
            return count;
        }

        // Pack guid → pack asset path, from every .meta under the pack (text-level, exact).
        private static Dictionary<string, string> BuildPackGuidMap(string root)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                string guid = null;
                try
                {
                    foreach (var line in File.ReadLines(meta))
                        if (line.StartsWith("guid:", StringComparison.Ordinal)) { guid = line.Substring(5).Trim(); break; }
                }
                catch { /* unreadable meta — skip */ }
                if (string.IsNullOrEmpty(guid)) continue;
                string asset = meta.Substring(0, meta.Length - ".meta".Length);
                if (Directory.Exists(asset)) continue; // folder metas don't resolve to assets we mirror
                map[guid] = asset.Replace('\\', '/');
            }
            return map;
        }
    }
}
