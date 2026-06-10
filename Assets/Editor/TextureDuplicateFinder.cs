// =============================================================================
// TextureDuplicateFinder (WO-408) — find duplicate / near-duplicate textures so
// the same art shipped under two paths can be de-duped (each duplicate is dead
// weight in the WebGL .data payload).
// -----------------------------------------------------------------------------
// READ-ONLY. Writes a report to Builds/TextureAudit/texture-duplicates.csv and a
// human-readable .txt summary. Changes NOTHING — de-duping is a manual follow-up
// (delete the redundant asset, repoint references) the owner approves per group.
//
// Two detection passes:
//   1. EXACT (source bytes): SHA-256 of the source file -> identical files,
//      even under different names. These are 100% safe to collapse.
//   2. NEAR (imported pixel signature): a small average-hash (aHash) of the
//      imported, downscaled texture -> visually-identical art that differs only
//      by compression/format/source encoding (e.g. a PNG and a JPG of the same
//      image). Flagged for human review, not auto-collapsed.
//
// Report columns:
//   group,kind,path,onDiskBytes,onDiskMB,wastedBytesInGroup
// (kind = "exact" | "near"; the first member of a group is the keeper, the rest
//  are the wasted copies; wastedBytesInGroup = sum of the non-keeper sizes.)
//
// Batchmode entry point: DeNelle.Editor.TextureDuplicateFinder.Run
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TextureDuplicateFinder
    {
        private const string OutputDir = "Builds/TextureAudit";

        // aHash grid side. 8 -> a 64-bit signature; near-dupe if Hamming distance
        // <= NearThreshold bits. Tunable.
        private const int AHashSide = 8;
        private const int NearThreshold = 5;

        private static readonly string[] SkipFragments =
        {
            "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Editor/",
        };

        [MenuItem("Defenders/Build/Find Duplicate Textures")]
        public static void Run()
        {
            string projRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDirFull = Path.Combine(projRoot, OutputDir);
            Directory.CreateDirectory(outDirFull);

            string[] guids = AssetDatabase.FindAssets("t:Texture2D");

            // Gather entries (skip non-shipping demo/editor art).
            var entries = new List<Entry>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || IsSkipped(path)) continue;
                if (AssetImporter.GetAtPath(path) as TextureImporter == null) continue;

                string full = Path.Combine(projRoot, path);
                long bytes = 0;
                try { if (File.Exists(full)) bytes = new FileInfo(full).Length; } catch { }

                entries.Add(new Entry { path = path, fullPath = full, bytes = bytes });
            }

            // ── Pass 1: exact source-byte SHA-256 ───────────────────────────────
            var byHash = new Dictionary<string, List<Entry>>();
            foreach (var e in entries)
            {
                e.sha = Sha256(e.fullPath);
                if (e.sha == null) continue;
                if (!byHash.TryGetValue(e.sha, out var list))
                    byHash[e.sha] = list = new List<Entry>();
                list.Add(e);
            }

            // ── Pass 2: near-dupe aHash on imported pixels (only on entries not
            //    already in an exact group, to avoid double-reporting) ───────────
            var exactDupPaths = new HashSet<string>();
            foreach (var kv in byHash)
                if (kv.Value.Count > 1)
                    foreach (var e in kv.Value) exactDupPaths.Add(e.path);

            var nearCandidates = new List<Entry>();
            foreach (var e in entries)
            {
                if (exactDupPaths.Contains(e.path)) continue;
                e.aHash = AverageHash(e.path);
                if (e.aHash != 0UL) nearCandidates.Add(e);
            }
            var nearGroups = ClusterNear(nearCandidates);

            // ── Write CSV ───────────────────────────────────────────────────────
            var csv = new StringBuilder();
            csv.AppendLine("group,kind,path,onDiskBytes,onDiskMB,wastedBytesInGroup");

            int groupId = 0;
            long totalWasted = 0;

            foreach (var kv in byHash)
            {
                if (kv.Value.Count <= 1) continue;
                groupId++;
                long wasted = GroupWasted(kv.Value);
                totalWasted += wasted;
                foreach (var e in kv.Value)
                    csv.Append(groupId).Append(",exact,")
                       .Append(Csv(e.path)).Append(',')
                       .Append(e.bytes).Append(',')
                       .Append((e.bytes / 1048576.0).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                       .Append(wasted).AppendLine();
            }

            foreach (var grp in nearGroups)
            {
                if (grp.Count <= 1) continue;
                groupId++;
                long wasted = GroupWasted(grp);
                totalWasted += wasted;
                foreach (var e in grp)
                    csv.Append(groupId).Append(",near,")
                       .Append(Csv(e.path)).Append(',')
                       .Append(e.bytes).Append(',')
                       .Append((e.bytes / 1048576.0).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                       .Append(wasted).AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine($"# duplicate_groups,{groupId}");
            csv.AppendLine($"# total_wasted_MB,{(totalWasted / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)}");

            string csvPath = Path.Combine(outDirFull, "texture-duplicates.csv");
            File.WriteAllText(csvPath, csv.ToString());

            Debug.Log($"[TextureDuplicateFinder] {groupId} duplicate group(s), " +
                      $"~{(totalWasted / 1048576.0):F1} MB reclaimable -> {csvPath}");
            Debug.Log($"TEXTURE_DUPES_OK :: groups={groupId} wastedMB={(totalWasted / 1048576.0):F1}");
        }

        // Keeper = the first (we sort by path for determinism); wasted = the rest.
        private static long GroupWasted(List<Entry> group)
        {
            group.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
            long wasted = 0;
            for (int i = 1; i < group.Count; i++) wasted += group[i].bytes;
            return wasted;
        }

        // Greedy clustering by Hamming distance on the 64-bit aHash.
        private static List<List<Entry>> ClusterNear(List<Entry> items)
        {
            var groups = new List<List<Entry>>();
            var used = new bool[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                if (used[i]) continue;
                var grp = new List<Entry> { items[i] };
                used[i] = true;
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (used[j]) continue;
                    if (Hamming(items[i].aHash, items[j].aHash) <= NearThreshold)
                    {
                        grp.Add(items[j]);
                        used[j] = true;
                    }
                }
                if (grp.Count > 1) groups.Add(grp);
            }
            return groups;
        }

        private static int Hamming(ulong a, ulong b)
        {
            ulong x = a ^ b;
            int count = 0;
            while (x != 0) { count++; x &= (x - 1); }
            return count;
        }

        /// <summary>Average-hash: downscale imported texture to AHashSide^2 grey, bit per &gt; mean.</summary>
        private static ulong AverageHash(string assetPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return 0UL;

            int n = AHashSide;
            RenderTexture rt = null;
            RenderTexture prev = RenderTexture.active;
            Texture2D small = null;
            try
            {
                rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                small = new Texture2D(n, n, TextureFormat.RGBA32, false);
                small.ReadPixels(new Rect(0, 0, n, n), 0, 0);
                small.Apply();

                var px = small.GetPixels();
                var lum = new float[px.Length];
                float sum = 0f;
                for (int i = 0; i < px.Length; i++)
                {
                    lum[i] = 0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b;
                    sum += lum[i];
                }
                float mean = sum / px.Length;

                ulong hash = 0UL;
                for (int i = 0; i < px.Length && i < 64; i++)
                    if (lum[i] > mean) hash |= (1UL << i);
                return hash;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TextureDuplicateFinder] aHash failed {assetPath}: {e.Message}");
                return 0UL;
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (small != null) UnityEngine.Object.DestroyImmediate(small);
            }
        }

        private static string Sha256(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath)) return null;
                using var sha = SHA256.Create();
                using var fs = File.OpenRead(fullPath);
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
            catch { return null; }
        }

        private static bool IsSkipped(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var frag in SkipFragments)
                if (p.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string Csv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        private sealed class Entry
        {
            public string path;
            public string fullPath;
            public long bytes;
            public string sha;
            public ulong aHash;
        }
    }
}
