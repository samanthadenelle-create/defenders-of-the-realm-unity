// =============================================================================
// TowerEmpowermentReachabilityRegression — is the tier-4 EMPOWER affordance
// reachable from a real player surface, or is it orphaned?
// Marker: TOWER_EMPOWER_REACH_OK
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Wired into DeNelle.Editor.DataRegression.RunAll
// as the "tower-empower-reach" suite.
//
// WHAT THIS GUARDS, and why it is worth a suite of its own
// --------------------------------------------------------
// The tier-4 "Empowered" row of tower-perks.json and four authored combat behaviours
// (GlacialCore / TrueAim / ManaSurge / EternalEmber, all in TowerCombat.cs) sit behind
// exactly one gate: Tower.TryEmpower(). Three separate sweeps have now re-discovered
// that nothing a player can touch calls it, and each sweep re-derived the conclusion
// from scratch (one of them wrongly, see FIELD-CONFUSION below). That is the defect
// this suite retires: the reachability VERDICT becomes a machine-checked, declared
// fact instead of a finding somebody has to notice again.
//
// HOW IT DECIDES (it resolves the path; it does NOT string-match the button's file)
// --------------------------------------------------------------------------------
// A backward walk from the gate outward, bounded to MaxHops:
//   hop 0  the gate            Tower.TryEmpower()
//   hop 1  its callers         every .cs OUTSIDE Tower.cs that calls TryEmpower(
//   hop n  their referrers     every .cs outside a frontier type's OWN declaring file
//                              that names it, plus every .unity / .prefab that carries
//                              that file's meta GUID (a real component placement)
// The walk stops the moment it touches an ANCHOR: a reference from a file that ships
// in a release player build (not under Assets/Editor, not under a DevTools/Tests path,
// and not compiled out by a "#if UNITY_EDITOR || DEVELOPMENT_BUILD" fence), or any
// scene/prefab placement at all. No anchor inside MaxHops = the affordance is orphaned.
//
// Matching runs on source with COMMENTS AND STRING LITERALS STRIPPED, so a type named
// only in a doc-comment or a log message can never fake a reference (which is exactly
// how an orphan hides from a naive grep - TowerEmpowerButton is named in Tower.cs's
// warning text and in TowerData.cs's tooltips).
//
// FIELD-CONFUSION (case 4 exists solely to stop this recurring)
// -------------------------------------------------------------
// A TowerData asset carries the token "ability:" TWICE under different owners:
//     upgrades[].ability   -> SpecialAbility        (per-level, 0/1/4/5 are authored)
//     empowerment.ability  -> EmpowermentAbility    (the empower gate; None on all four)
// A sweep that grepped "ability:" read the upgrades ladder and reported the empowerment
// gate as authored when it is not. Case 4 parses the "empowerment:" BLOCK only, by
// indentation, so the two can never be conflated again.
//
// THE EXPECTATION SWITCH (read this before you "fix" a failure)
// -------------------------------------------------------------
// ExpectReachable declares what the project INTENDS today. It is false: empowerment is
// knowingly orphaned, pending an owner decision (see the BALANCE note below). While it
// is false the suite asserts the orphan state precisely and passes with a loud WAIVED
// line. The moment someone gives the affordance a real home, cases 1-3 start passing
// and THIS SUITE FAILS, telling them to flip ExpectReachable to true - which is the
// point: the wiring cannot land silently. With ExpectReachable true the suite is a
// plain reachability guard and fails if the affordance ever drops back to zero
// external references.
//
// BALANCE (owner felt-verify required before ExpectReachable flips)
// -----------------------------------------------------------------
// Empowerment currently reaches nobody, so making it reachable is a REAL power increase
// that has never been felt. What it turns on, verbatim from TowerCombat.cs / Tower.cs:
//   * tower-perks.json tier 4 "Empowered": damage x1.70 +10 flat, range +6, cooldown
//     x0.30 (i.e. a ~3.3x fire rate), signature "overcharge"
//   * GlacialCore / TrueAim / ManaSurge / EternalEmber, one per tower element
// No value in this suite is a tuning knob and nothing here retunes anything.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Source-lint + asset-lint: resolves whether <c>Tower.TryEmpower()</c> is reachable from a
    /// shipping player surface. Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class TowerEmpowermentReachabilityRegression
    {
        // ── The declared expectation. See "THE EXPECTATION SWITCH" in the header. ──
        private const bool ExpectReachable = false;

        // How far out from the gate the backward walk is allowed to travel. Six hops is
        // far beyond any real UI->service chain in this project; it exists so a cycle or a
        // pathological fan-out can never make the suite run long.
        private const int MaxHops = 6;

        private const string GateFileName   = "Tower.cs";
        private const string GateCall       = "TryEmpower";
        private const string EmpowerAbility = "ability";

        // The four TowerData assets that own the empowerment gate.
        private static readonly string[] TowerAssetNames =
        {
            "ArcherTower.asset", "DevTower.asset", "FrostTower.asset", "MageTower.asset",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TOWER EMPOWERMENT REACHABILITY ---");
            log.AppendLine("declared expectation: ExpectReachable=" + (ExpectReachable ? "true" : "false"));

            string assets = null;
            try { assets = Application.dataPath; } catch { }
            if (string.IsNullOrEmpty(assets) || !Directory.Exists(assets))
            {
                reason = "TOWER-EMPOWER-REACH: Application.dataPath is not a readable directory - suite cannot run.";
                return false;
            }

            // ── Index every .cs once. Path -> stripped source. ────────────────────
            var stripped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rawText  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] csFiles;
            try { csFiles = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories); }
            catch (Exception e) { reason = "TOWER-EMPOWER-REACH: could not enumerate .cs under Assets - " + e.Message; return false; }

            for (int i = 0; i < csFiles.Length; i++)
            {
                string p = csFiles[i];
                if (IsThirdParty(p)) continue;
                string raw = ReadOrNull(p);
                if (raw == null) continue;
                rawText[p]  = raw;
                stripped[p] = StripCommentsAndStrings(raw);
            }

            if (rawText.Count == 0)
            {
                reason = "TOWER-EMPOWER-REACH: indexed zero readable .cs files - the walk would pass vacuously.";
                return false;
            }
            log.AppendLine("indexed " + rawText.Count + " first-party .cs file(s)");

            // ── CASE 1: the gate exists and has at least one caller outside its own file.
            string gatePath = FindByFileName(rawText.Keys, GateFileName, "/Village/Buildings/");
            if (gatePath == null)
            {
                reason = "TOWER-EMPOWER-REACH case 1: could not locate the gate file " + GateFileName
                       + " under Village/Buildings - the empowerment gate moved or was deleted; retarget this suite.";
                return false;
            }
            if (stripped[gatePath].IndexOf(GateCall, StringComparison.Ordinal) < 0)
            {
                reason = "TOWER-EMPOWER-REACH case 1: " + GateFileName + " no longer declares "
                       + GateCall + "() - the empowerment gate is gone; retarget or retire this suite.";
                return false;
            }

            var callers = new List<string>();
            foreach (var kv in stripped)
            {
                if (string.Equals(kv.Key, gatePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (Regex.IsMatch(kv.Value, @"\b" + GateCall + @"\s*\("))
                    callers.Add(kv.Key);
            }
            log.AppendLine("case 1: " + callers.Count + " caller file(s) of " + GateCall + "() outside " + GateFileName
                         + (callers.Count > 0 ? " -> " + JoinNames(callers) : ""));

            if (callers.Count == 0)
            {
                // Zero callers is terminal in BOTH expectation modes: there is not even an
                // affordance left to reach, so the four abilities are unreachable outright.
                reason = "TOWER-EMPOWER-REACH case 1: NOTHING outside " + GateFileName + " calls " + GateCall
                       + "() (comments and string literals stripped). The empower affordance was deleted, so "
                       + "tower-perks.json tier 4 and TowerCombat's GlacialCore/TrueAim/ManaSurge/EternalEmber "
                       + "are dead code with no entry point at all.";
                return false;
            }

            // ── CASE 2: the backward walk - is any hop anchored in shipping code / a scene?
            var walk = WalkForAnchor(callers, stripped, rawText, assets, log);

            // ── CASE 3: even a reached button refuses unless empowerment data is authored.
            //    Parse the "empowerment:" BLOCK only (see FIELD-CONFUSION in the header).
            int authored = 0;
            var assetDetail = new List<string>();
            string towersDir = Path.Combine(assets, "Resources" + Path.DirectorySeparatorChar + "Towers");
            for (int i = 0; i < TowerAssetNames.Length; i++)
            {
                string p = Path.Combine(towersDir, TowerAssetNames[i]);
                int val = ReadEmpowermentAbility(p, out string note);
                if (val > 0) authored++;
                assetDetail.Add(TowerAssetNames[i] + "=" + (note ?? val.ToString()));
            }
            log.AppendLine("case 3: TowerData empowerment.ability -> " + string.Join(", ", assetDetail.ToArray())
                         + "  (authored=" + authored + "/" + TowerAssetNames.Length + ")");

            bool dataAuthored = authored > 0;
            bool reachable = walk.Anchored && dataAuthored;

            log.AppendLine("case 2: anchored=" + walk.Anchored + " hops=" + walk.Hops
                         + (walk.Anchored ? " via " + walk.AnchorDetail : " (" + walk.DeadEndDetail + ")"));
            log.AppendLine("RESOLVED reachable=" + reachable);

            // ── Verdict against the declared expectation. ─────────────────────────
            if (ExpectReachable)
            {
                if (!walk.Anchored)
                    failures.Add("case 2: the empower affordance (" + JoinNames(callers) + ") has NO shipping "
                               + "anchor within " + MaxHops + " hops - " + walk.DeadEndDetail
                               + ". ExpectReachable is true, so this is a REGRESSION: the affordance lost its home.");
                if (!dataAuthored)
                    failures.Add("case 3: every TowerData asset still has empowerment.ability = None ("
                               + string.Join(", ", assetDetail.ToArray()) + "), so Tower." + GateCall
                               + "() refuses for every tower even when the button is reached. NOTE: upgrades[].ability "
                               + "is a DIFFERENT field (SpecialAbility) and does not satisfy this.");
            }
            else
            {
                if (reachable)
                    failures.Add("case 0: the empower affordance is now REACHABLE (anchored via " + walk.AnchorDetail
                               + "; empowerment.ability authored on " + authored + " asset(s)) but ExpectReachable is "
                               + "still false. This suite fails ON PURPOSE so the wiring cannot land silently: flip "
                               + "ExpectReachable to true IN THE SAME COMMIT, and get the owner to felt-verify the new "
                               + "power first - tier 4 grants damage x1.70 +10, range +6, cooldown x0.30, plus one of "
                               + "GlacialCore/TrueAim/ManaSurge/EternalEmber per tower.");
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("TOWER-EMPOWER-REACH FAILED (").Append(failures.Count).Append("): ");
                for (int i = 0; i < failures.Count; i++) { if (i > 0) sb.Append(" | "); sb.Append(failures[i]); }
                sb.Append("  [trace] ").Append(log.ToString().Replace(Environment.NewLine, " ; "));
                reason = sb.ToString();
                return false;
            }

            if (!ExpectReachable)
            {
                Debug.LogWarning("[tower-empower-reach] WAIVED: tower EMPOWERMENT is knowingly ORPHANED. "
                    + JoinNames(callers) + " has no shipping anchor (" + walk.DeadEndDetail + ") and "
                    + "empowerment.ability is None on all " + TowerAssetNames.Length + " TowerData assets. "
                    + "tower-perks.json tier 4 and TowerCombat's GlacialCore/TrueAim/ManaSurge/EternalEmber "
                    + "cannot be reached by a player. Pending an owner decision - see this suite's header.");
                reason = "TOWER_EMPOWER_REACH_OK - orphan state PINNED as declared (ExpectReachable=false): "
                       + callers.Count + " gate caller(s), no shipping anchor in " + MaxHops + " hops, "
                       + authored + "/" + TowerAssetNames.Length + " TowerData assets author empowerment.ability. "
                       + "Wiring the affordance will FAIL this suite until ExpectReachable is flipped.";
                return true;
            }

            reason = "TOWER_EMPOWER_REACH_OK - empower affordance reachable: " + callers.Count
                   + " gate caller(s), anchored in " + walk.Hops + " hop(s) via " + walk.AnchorDetail + ", "
                   + authored + "/" + TowerAssetNames.Length + " TowerData assets author empowerment.ability.";
            return true;
        }

        // ── The backward walk ─────────────────────────────────────────────────────

        private struct WalkResult
        {
            public bool Anchored;
            public int Hops;
            public string AnchorDetail;
            public string DeadEndDetail;
        }

        /// <summary>
        /// Walk outward from <paramref name="frontier"/> looking for a reference that ships in a
        /// player build (or any scene/prefab placement). Bounded by <see cref="MaxHops"/>.
        /// </summary>
        private static WalkResult WalkForAnchor(List<string> frontier,
                                                Dictionary<string, string> stripped,
                                                Dictionary<string, string> rawText,
                                                string assetsRoot,
                                                StringBuilder log)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < frontier.Count; i++) seen.Add(frontier[i]);

            var current = new List<string>(frontier);
            // Index every script GUID a scene/prefab references ONCE (guid -> the file that
            // placed it). Re-reading the scene tree per frontier file would make the walk
            // quadratic over thousands of assets.
            var placedGuids = IndexPlacedScriptGuids(assetsRoot, out int sceneFileCount);
            log.AppendLine("walk: indexed " + placedGuids.Count + " placed script guid(s) across "
                         + sceneFileCount + " scene/prefab file(s)");

            var deadEnds = new List<string>();

            for (int hop = 1; hop <= MaxHops && current.Count > 0; hop++)
            {
                var next = new List<string>();
                for (int i = 0; i < current.Count; i++)
                {
                    string ownPath = current[i];
                    string typeName = Path.GetFileNameWithoutExtension(ownPath);

                    // (a) a real component placement in a scene or prefab is an anchor by itself.
                    string guid = ReadMetaGuid(ownPath);
                    if (!string.IsNullOrEmpty(guid) && placedGuids.TryGetValue(guid, out string placedIn))
                        return new WalkResult
                        {
                            Anchored = true,
                            Hops = hop,
                            AnchorDetail = typeName + " placed in " + placedIn,
                        };

                    // (b) any .cs outside this type's OWN file that names it.
                    int refs = 0;
                    var pattern = new Regex(@"\b" + Regex.Escape(typeName) + @"\b");
                    foreach (var kv in stripped)
                    {
                        if (string.Equals(kv.Key, ownPath, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!pattern.IsMatch(kv.Value)) continue;
                        refs++;

                        if (ShipsInPlayerBuild(kv.Key, rawText[kv.Key]))
                            return new WalkResult
                            {
                                Anchored = true,
                                Hops = hop,
                                AnchorDetail = typeName + " referenced by shipping file "
                                             + Path.GetFileName(kv.Key),
                            };

                        if (seen.Add(kv.Key)) next.Add(kv.Key);
                    }

                    if (refs == 0)
                        deadEnds.Add(typeName + " has ZERO external references (no .cs, no scene, no prefab)");
                }
                current = next;
            }

            return new WalkResult
            {
                Anchored = false,
                Hops = MaxHops,
                DeadEndDetail = deadEnds.Count > 0
                    ? string.Join("; ", deadEnds.ToArray())
                    : "every referrer within " + MaxHops + " hops is editor-only, DevTools/Tests, or fenced behind "
                    + "#if UNITY_EDITOR || DEVELOPMENT_BUILD",
            };
        }

        /// <summary>
        /// True when this file's code is compiled into a RELEASE player build: not editor-only,
        /// not a DevTools/Tests path, and not fenced behind a development-build directive.
        /// </summary>
        private static bool ShipsInPlayerBuild(string path, string raw)
        {
            string n = path.Replace('\\', '/');
            if (n.IndexOf("/Assets/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("/DevTools/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("/VfxParade/", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // A development-build fence anywhere in the file means its contents are not
            // guaranteed to ship. Conservative on purpose: an orphan must not be rescued by a
            // reference that only a dev build compiles (that is precisely how the empower
            // hotkey and the dev harness "reference" the legacy tower stack today).
            if (raw != null && Regex.IsMatch(raw, @"^\s*#if\b[^\r\n]*\bDEVELOPMENT_BUILD\b", RegexOptions.Multiline))
                return false;

            return true;
        }

        // ── Asset parsing ─────────────────────────────────────────────────────────

        /// <summary>
        /// The value of <c>empowerment.ability</c> in a TowerData .asset - parsed from the
        /// "empowerment:" BLOCK by indentation so it can never pick up an
        /// <c>upgrades[].ability</c> (a different enum entirely). Returns -1 with a note when
        /// the file or the block is missing.
        /// </summary>
        private static int ReadEmpowermentAbility(string path, out string note)
        {
            note = null;
            string body = ReadOrNull(path);
            if (body == null) { note = "MISSING"; return -1; }

            string[] lines = body.Replace("\r\n", "\n").Split('\n');
            int blockIndent = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                int indent = line.Length - trimmed.Length;

                if (blockIndent < 0)
                {
                    if (trimmed.StartsWith("empowerment:", StringComparison.Ordinal)) blockIndent = indent;
                    continue;
                }

                if (trimmed.Length == 0) continue;
                // Left the block: a line at or below the block's own indentation.
                if (indent <= blockIndent) break;

                if (trimmed.StartsWith(EmpowerAbility + ":", StringComparison.Ordinal))
                {
                    string v = trimmed.Substring(EmpowerAbility.Length + 1).Trim();
                    if (int.TryParse(v, out int parsed)) return parsed;
                    note = "UNPARSABLE(" + v + ")";
                    return -1;
                }
            }

            note = blockIndent < 0 ? "NO-EMPOWERMENT-BLOCK" : "NO-ABILITY-KEY";
            return -1;
        }

        // ── Small helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Every script GUID referenced by any scene or prefab, mapped to the first file that
        /// referenced it. Built once per run; a placement is what makes a MonoBehaviour real.
        /// </summary>
        private static Dictionary<string, string> IndexPlacedScriptGuids(string assetsRoot, out int fileCount)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<string>();
            TryAdd(files, assetsRoot, "*.unity");
            TryAdd(files, assetsRoot, "*.prefab");
            fileCount = files.Count;

            var guidPattern = new Regex(@"guid:\s*([0-9a-fA-F]{32})");
            for (int i = 0; i < files.Count; i++)
            {
                string body = ReadOrNull(files[i]);
                if (body == null) continue;
                string shortName = Path.GetFileName(files[i]);
                foreach (Match m in guidPattern.Matches(body))
                {
                    string g = m.Groups[1].Value;
                    if (!map.ContainsKey(g)) map[g] = shortName;
                }
            }
            return map;
        }

        private static void TryAdd(List<string> into, string root, string pattern)
        {
            try
            {
                var found = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
                for (int i = 0; i < found.Length; i++)
                    if (!IsThirdParty(found[i])) into.Add(found[i]);
            }
            catch { /* an unreadable subtree must not sink the suite; the count is logged */ }
        }

        /// <summary>Vendor art/tooling trees carry thousands of files and never wire our gameplay.</summary>
        private static bool IsThirdParty(string path)
        {
            string n = path.Replace('\\', '/');
            return n.IndexOf("/polyperfect/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/Quaternius/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/MeshBaker/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/Lana Studio/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/TextMesh Pro/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindByFileName(IEnumerable<string> paths, string fileName, string pathFragment)
        {
            string loose = null;
            foreach (var p in paths)
            {
                if (!string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase)) continue;
                if (p.Replace('\\', '/').IndexOf(pathFragment, StringComparison.OrdinalIgnoreCase) >= 0) return p;
                loose = loose ?? p;
            }
            return loose;
        }

        private static string ReadMetaGuid(string csPath)
        {
            string meta = ReadOrNull(csPath + ".meta");
            if (meta == null) return null;
            var m = Regex.Match(meta, @"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ReadOrNull(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        private static string JoinNames(List<string> paths)
        {
            var names = new List<string>();
            for (int i = 0; i < paths.Count; i++) names.Add(Path.GetFileName(paths[i]));
            return string.Join(", ", names.ToArray());
        }

        /// <summary>
        /// Blank out line comments, block comments, string literals, verbatim strings and char
        /// literals - replaced with spaces so offsets and line breaks survive. Preprocessor
        /// directives are deliberately KEPT: the walk reads #if fences to decide what ships.
        /// </summary>
        private static string StripCommentsAndStrings(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var outp = new StringBuilder(src.Length);
            int i = 0, n = src.Length;

            while (i < n)
            {
                char c = src[i];

                // line comment
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') { outp.Append(' '); i++; }
                    continue;
                }
                // block comment
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    outp.Append("  "); i += 2;
                    while (i < n && !(src[i] == '*' && i + 1 < n && src[i + 1] == '/'))
                    { outp.Append(src[i] == '\n' ? '\n' : ' '); i++; }
                    if (i < n) { outp.Append("  "); i += 2; }
                    continue;
                }
                // verbatim string  @"..."  ("" is an escaped quote)
                if (c == '@' && i + 1 < n && src[i + 1] == '"')
                {
                    outp.Append("  "); i += 2;
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { outp.Append("  "); i += 2; continue; }
                            outp.Append(' '); i++; break;
                        }
                        outp.Append(src[i] == '\n' ? '\n' : ' '); i++;
                    }
                    continue;
                }
                // interpolated + regular string  $"..."  "..."
                if (c == '"' || (c == '$' && i + 1 < n && src[i + 1] == '"'))
                {
                    if (c == '$') { outp.Append(' '); i++; }
                    outp.Append(' '); i++;
                    while (i < n && src[i] != '"')
                    {
                        if (src[i] == '\\' && i + 1 < n) { outp.Append("  "); i += 2; continue; }
                        outp.Append(src[i] == '\n' ? '\n' : ' '); i++;
                    }
                    if (i < n) { outp.Append(' '); i++; }
                    continue;
                }
                // char literal
                if (c == '\'')
                {
                    outp.Append(' '); i++;
                    while (i < n && src[i] != '\'')
                    {
                        if (src[i] == '\\' && i + 1 < n) { outp.Append("  "); i += 2; continue; }
                        outp.Append(' '); i++;
                    }
                    if (i < n) { outp.Append(' '); i++; }
                    continue;
                }

                outp.Append(c); i++;
            }

            return outp.ToString();
        }
    }
}
