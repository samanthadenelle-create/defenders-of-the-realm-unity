// =============================================================================
// DataWebRegression — the data-catalog / web-platform sync gate.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Headless, no-scene, no-PlayMode, data-only.
// Markers: DATAWEB_OK / DATAWEB_FAIL (FAIL via Debug.LogError so it lands in
// break-log.jsonl per docs/INSTRUMENTATION_STANDARD.md §4/§5).
//
// The canonical catalogs live in TWO copies (CanonicalJson.cs header + docs/
// MASTER_CATALOG/data-catalogs.md §1): Assets/Resources/Data/Canonical (WebGL-safe,
// WINS at load) and Assets/StreamingAssets/Data/Canonical (desktop fallback +
// source). "Keep them in sync" is the documented-but-UNENFORCED law — this file
// enforces it. What CoreDataHubRegression already covers is deliberately NOT
// duplicated here (top-level Resources-copy EXISTENCE + non-empty CanonicalJson
// reads); this gate adds the four gaps:
//
//   (1) DUAL-COPY DRIFT — every *.json present in BOTH roots (recursive, incl.
//       dialogue/ dungeons/ tutorial/) is content-compared (BOM-stripped,
//       CRLF-normalized). Drift = FAIL naming the file + both sizes. An EOL/BOM-
//       only difference is logged as a note, not failed (semantically identical).
//       KNOWN RED AT AUTHORING (2026-07-12): 6 files drift — weapons.json
//       (256KB streaming vs 19KB Resources!), armor.json, daily-quests.json,
//       skin.json, stake-rewards.json, tower-perks.json. Since Resources WINS at
//       runtime, the game is silently playing the SMALLER/older copy. That is the
//       exact defect class this gate exists to catch — sync the copies to go green.
//
//   (2) WEBGL-BROKEN-BY-OMISSION — two arms:
//       (2a) every "Data/Canonical/….json" path literal in RUNTIME code (all *.cs
//            under Assets/ excluding any Editor/ folder — editor code never ships)
//            must have a Resources copy on disk, because on WebGL StreamingAssets
//            File IO throws and CanonicalJson returns null (empty catalog, the
//            "loads but combat won't play" class). Constructed-path PREFIX
//            literals (no .json suffix, e.g. "Data/Canonical/dungeons") are
//            logged as skipped — they can't be statically resolved.
//       (2b) the historically StreamingAssets-only six (enemy-roles / towers /
//            walls / realm-map / heart / audio-mix — data-catalogs.md §2 flagged
//            them as "WebGL would get null") are pinned by name: each must keep
//            its Resources mirror. They were mirrored since the doc was written,
//            so this is green today and flips red if any is ever un-mirrored.
//       Plus: every StreamingAssets SUBDIRECTORY json must have a Resources
//       mirror (CoreDataHubRegression asserts top-level only).
//
//   (3) PARSE — every *.json under BOTH canonical roots (recursive) parses via
//       Newtonsoft JToken.Parse. *.jsonl is excluded by extension (the documented
//       orientation-recipes case; it lives under Resources/Data, outside the
//       canonical roots anyway). Note: CanonicalJsonIntegrityTest (NUnit, not part
//       of this headless suite) parses the StreamingAssets side only — the
//       RESOURCES side (the copy that actually wins at runtime) had no parse gate
//       until this one.
//
//   (4) VERSION — every top-level-object catalog in the StreamingAssets root must
//       carry a top-level "version" field, EXCEPT the verified versionless-by-
//       design set (canon-strings.json, en.json, garrison-recipes.json,
//       themes.json — flat string maps / recipe lists that never carried one).
//       NOTE: armor.json + weapons.json were once flagged as version-less
//       (data-catalogs.md §2 table) — verified 2026-07-12 they NOW carry
//       version, so they are asserted like every other catalog (stale doc, not a
//       stale check). A cross-copy VERSION MISMATCH is also failed by name (a
//       clearer message than the raw byte drift it always accompanies).
//
// Allowlists (each verified from code/disk, see inline comments):
//   • VersionlessByDesign — canon-strings/en/garrison-recipes/themes.
//   • NonDualCopyByDesign — skr_*, battle_*, *.sample.json (read on a
//     StreamingAssets-direct path per CoreDataHubRegression's exclusion; no
//     Resources mirror expected).
//   • LiteralAllowlist — "Data/Canonical/__does_not_exist__.json" (the negative-
//     path oracle literal in CoreCatalogRegression; editor-only anyway) and
//     castle-south-recipe (documented editor-only recipe — it actually lives at
//     Resources/Data/castle-south-recipe.json OUTSIDE the canonical root, so it
//     can never match the literal regex; kept for explicitness).
//
// Wire into the suite from DataRegression.RunAll (one line — orchestrator does it):
//   if (!DataWebRegression.Run(out var dataWebReason)) failures.Add(dataWebReason); else log.AppendLine("[data-web] " + dataWebReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DataWebRegression
    {
        // ── Allowlists (verified, see header) ───────────────────────────────────

        // Catalogs that never carried a top-level "version" (flat string maps /
        // plain recipe lists) — verified by parsing 2026-07-12.
        private static readonly HashSet<string> VersionlessByDesign =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "canon-strings.json",
            "en.json",
            "garrison-recipes.json",
            "themes.json",
        };

        // Files read on a NON-dual-copy path (StreamingAssets-direct) — same
        // exclusion CoreDataHubRegression uses; no Resources mirror expected.
        private static bool IsNonDualCopyByDesign(string fileName)
        {
            var n = fileName.ToLowerInvariant();
            return n.EndsWith(".sample.json") || n.StartsWith("skr_") || n.StartsWith("battle_");
        }

        // Path literals in code that must NOT be asserted as WebGL load surface.
        private static readonly HashSet<string> LiteralAllowlist =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // CoreCatalogRegression's deliberate negative-path probe (editor-only,
            // but pinned here so a future runtime copy of that probe can't fail us).
            "__does_not_exist__.json",
            // Documented editor-only recipe; lives OUTSIDE the canonical root
            // (Resources/Data/castle-south-recipe.json) — explicitness only.
            "castle-south-recipe.json",
        };

        // The historically StreamingAssets-only six (data-catalogs.md §2: "WebGL
        // would get null"). Mirrored since; pinned so un-mirroring flips red.
        private static readonly string[] KnownSixMustStayMirrored =
        {
            "enemy-roles.json",
            "towers.json",
            "walls.json",
            "realm-map.json",
            "heart.json",
            "audio-mix.json",
        };

        // ── Entry point ─────────────────────────────────────────────────────────

        /// <summary>
        /// Proves the dual-copy canonical-JSON law holds for the web platform:
        /// no content drift between the two roots, no WebGL-broken-by-omission
        /// catalog, every file parses, version fields present where owed.
        /// Deterministic, self-contained, no scene / no PlayMode.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DATA/WEB (canonical dual-copy sync + WebGL load surface) ---");

            string streamingRoot = Path.Combine(Application.streamingAssetsPath, "Data/Canonical");
            string resourcesRoot = Path.Combine(Application.dataPath, "Resources/Data/Canonical");

            if (!Directory.Exists(streamingRoot))
                failures.Add($"StreamingAssets canonical root missing: {streamingRoot}");
            if (!Directory.Exists(resourcesRoot))
                failures.Add($"Resources canonical root missing: {resourcesRoot}");

            if (failures.Count == 0)
            {
                CheckDualCopyDrift(streamingRoot, resourcesRoot, failures, log);
                CheckWebglOmission(streamingRoot, resourcesRoot, failures, log);
                CheckAllParse(streamingRoot, resourcesRoot, failures, log);
                CheckVersionFields(streamingRoot, resourcesRoot, failures, log);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "DATAWEB_OK");
                reason = "DATA/WEB OK — dual copies in sync, WebGL load surface complete, all canonical JSON parses, version fields present";
                return true;
            }
            reason = "data-web: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "DATAWEB_FAIL: " + reason);
            return false;
        }

        // ── (1) Dual-copy content diff ──────────────────────────────────────────

        private static void CheckDualCopyDrift(string streamingRoot, string resourcesRoot,
                                               List<string> failures, StringBuilder log)
        {
            int compared = 0, drifted = 0;
            foreach (var sPath in CanonicalJsonFiles(streamingRoot))
            {
                string rel = RelativePath(streamingRoot, sPath);
                string rPath = Path.Combine(resourcesRoot, rel);
                if (!File.Exists(rPath)) continue;   // existence is (2)'s / CoreDataHub's job

                compared++;
                byte[] sRaw = File.ReadAllBytes(sPath);
                byte[] rRaw = File.ReadAllBytes(rPath);
                string sNorm = Normalize(sRaw);
                string rNorm = Normalize(rRaw);

                if (!string.Equals(sNorm, rNorm, StringComparison.Ordinal))
                {
                    drifted++;
                    failures.Add($"DUAL-COPY DRIFT '{rel}': StreamingAssets ({sRaw.Length} B) != Resources ({rRaw.Length} B) — " +
                                 "Resources WINS at runtime, so the game plays the Resources copy; sync the two (the documented CanonicalJson sync rule)");
                    log.AppendLine($"  DRIFT {rel} | streaming={sRaw.Length}B resources={rRaw.Length}B");
                }
                else if (!BytesEqual(sRaw, rRaw))
                {
                    // Same content, different bytes: EOL/BOM only — semantically identical.
                    log.AppendLine($"  note: '{rel}' differs only in EOL/BOM (content identical) — not failed");
                }
            }
            log.AppendLine($"dual-copy diff: compared {compared} paired file(s), {drifted} drifted");
        }

        // ── (2) WebGL broken-by-omission ────────────────────────────────────────

        private static void CheckWebglOmission(string streamingRoot, string resourcesRoot,
                                               List<string> failures, StringBuilder log)
        {
            // (2a) Runtime call-site literal scan: every "Data/Canonical/….json"
            // literal in non-Editor code must resolve on the WebGL (Resources) surface.
            var literalRegex = new Regex("\"Data/Canonical/([A-Za-z0-9_\\-./]+)\"", RegexOptions.Compiled);
            var referenced = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefixLiterals = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            int scannedFiles = 0;

            foreach (var cs in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                string norm = cs.Replace('\\', '/');
                // Editor-only code never ships to WebGL — any folder named Editor
                // (asmdef convention: Assets/Editor, <Module>/Editor) is out of scope.
                if (norm.Contains("/Editor/")) continue;
                scannedFiles++;

                string text;
                try { text = File.ReadAllText(cs); }
                catch (Exception ex)
                {
                    failures.Add($"call-site scan could not read '{norm}': {ex.GetType().Name}: {ex.Message}");
                    continue;
                }
                foreach (Match m in literalRegex.Matches(text))
                {
                    string rel = m.Groups[1].Value;
                    if (rel.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) referenced.Add(rel);
                    else prefixLiterals.Add(rel);   // constructed path — statically unresolvable
                }
            }

            foreach (var rel in referenced)
            {
                string fileName = Path.GetFileName(rel);
                if (LiteralAllowlist.Contains(fileName))
                { log.AppendLine($"  literal '{rel}' allowlisted (documented editor-only / negative probe) — skipped"); continue; }
                if (IsNonDualCopyByDesign(fileName))
                { log.AppendLine($"  literal '{rel}' non-dual-copy by design (skr_/battle_/sample) — skipped"); continue; }

                string rPath = Path.Combine(resourcesRoot, rel);
                if (!File.Exists(rPath))
                    failures.Add($"WEBGL OMISSION: runtime code loads 'Data/Canonical/{rel}' via CanonicalJson but NO Resources copy exists " +
                                 $"({rPath}) — CanonicalJson.Read returns null on WebGL (empty catalog, the 'loads but combat won't play' class)");
                else
                    log.AppendLine($"  literal '{rel}' -> Resources copy OK");
            }
            foreach (var p in prefixLiterals)
                log.AppendLine($"  prefix literal 'Data/Canonical/{p}' (path constructed at runtime) — cannot assert statically, skipped");
            log.AppendLine($"call-site scan: {scannedFiles} runtime .cs file(s), {referenced.Count} .json literal(s), {prefixLiterals.Count} prefix literal(s)");

            // (2b) The known six stay mirrored (green today; red if un-mirrored).
            foreach (var six in KnownSixMustStayMirrored)
            {
                string rPath = Path.Combine(resourcesRoot, six);
                if (!File.Exists(rPath))
                    failures.Add($"WEBGL OMISSION: '{six}' lost its Resources mirror ({rPath}) — it regresses to the documented " +
                                 "StreamingAssets-only state (null on WebGL, data-catalogs.md §2)");
                else
                    log.AppendLine($"  known-six '{six}' mirrored OK");
            }

            // Subdirectory mirrors (CoreDataHubRegression asserts TOP-LEVEL only).
            int subChecked = 0;
            foreach (var sPath in CanonicalJsonFiles(streamingRoot))
            {
                string rel = RelativePath(streamingRoot, sPath);
                if (rel.IndexOf('/') < 0) continue;   // top-level = CoreDataHub's lane
                string fileName = Path.GetFileName(rel);
                if (IsNonDualCopyByDesign(fileName)) continue;
                subChecked++;
                string rPath = Path.Combine(resourcesRoot, rel);
                if (!File.Exists(rPath))
                    failures.Add($"WEBGL OMISSION: subdirectory catalog '{rel}' has a StreamingAssets source but NO Resources mirror " +
                                 $"({rPath}) — null on WebGL (subdirs are outside CoreDataHubRegression's top-level sweep)");
            }
            log.AppendLine($"subdirectory mirror check: {subChecked} file(s)");
        }

        // ── (3) Every canonical *.json parses ───────────────────────────────────

        private static void CheckAllParse(string streamingRoot, string resourcesRoot,
                                          List<string> failures, StringBuilder log)
        {
            int parsed = 0, broken = 0;
            foreach (var root in new[] { streamingRoot, resourcesRoot })
            {
                string label = root == streamingRoot ? "StreamingAssets" : "Resources";
                foreach (var path in CanonicalJsonFiles(root))
                {
                    string rel = RelativePath(root, path);
                    parsed++;
                    try { JToken.Parse(Normalize(File.ReadAllBytes(path))); }
                    catch (Exception ex)
                    {
                        broken++;
                        failures.Add($"PARSE FAIL {label}/'{rel}': {ex.GetType().Name}: {FirstLine(ex.Message)}");
                    }
                }
            }
            log.AppendLine($"parse: {parsed} file(s) across both roots, {broken} broken (*.jsonl excluded by extension)");
        }

        // ── (4) Version-field presence + cross-copy agreement ───────────────────

        private static void CheckVersionFields(string streamingRoot, string resourcesRoot,
                                               List<string> failures, StringBuilder log)
        {
            int checkedCount = 0;
            foreach (var sPath in CanonicalJsonFiles(streamingRoot))
            {
                string rel = RelativePath(streamingRoot, sPath);
                string fileName = Path.GetFileName(rel);
                if (IsNonDualCopyByDesign(fileName)) continue;
                if (VersionlessByDesign.Contains(fileName))
                { log.AppendLine($"  '{rel}' versionless by design — skipped"); continue; }

                JObject sObj = TryParseObject(sPath);
                if (sObj == null) continue;   // parse failures already reported by (3)
                checkedCount++;

                var sVer = sObj["version"];
                if (sVer == null)
                {
                    failures.Add($"VERSION MISSING '{rel}': no top-level \"version\" field — every canonical catalog outside the " +
                                 "versionless-by-design set carries one (add it, or extend VersionlessByDesign with a reason)");
                    continue;
                }

                // Cross-copy agreement (a clearer name for the drift it accompanies).
                string rPath = Path.Combine(resourcesRoot, rel);
                if (File.Exists(rPath))
                {
                    JObject rObj = TryParseObject(rPath);
                    var rVer = rObj != null ? rObj["version"] : null;
                    if (rObj != null && !JToken.DeepEquals(sVer, rVer))
                        failures.Add($"VERSION MISMATCH '{rel}': StreamingAssets version={sVer} vs Resources version={(rVer != null ? rVer.ToString() : "<missing>")} " +
                                     "— the copies diverged (Resources wins at runtime)");
                }
            }
            log.AppendLine($"version fields: {checkedCount} catalog(s) checked, {VersionlessByDesign.Count} allowlisted");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        // Recursive *.json under a canonical root. Explicit extension filter so a
        // *.jsonl (orientation-recipes class) can never slip in via pattern quirks.
        private static IEnumerable<string> CanonicalJsonFiles(string root)
        {
            foreach (var path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    yield return path;
        }

        private static string RelativePath(string root, string fullPath)
        {
            string rel = fullPath.Substring(root.Length).TrimStart('\\', '/');
            return rel.Replace('\\', '/');
        }

        // UTF-8 decode with BOM strip + CRLF -> LF: the semantic content of the file.
        private static string Normalize(byte[] raw)
        {
            int offset = (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) ? 3 : 0;
            string text = Encoding.UTF8.GetString(raw, offset, raw.Length - offset);
            return text.Replace("\r\n", "\n");
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static JObject TryParseObject(string path)
        {
            try { return JToken.Parse(Normalize(File.ReadAllBytes(path))) as JObject; }
            catch { return null; }   // (3) already reported the parse failure with detail
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl).TrimEnd('\r');
        }
    }
}
