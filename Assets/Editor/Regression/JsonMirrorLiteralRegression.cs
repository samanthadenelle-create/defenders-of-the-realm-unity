// =============================================================================
// JsonMirrorLiteralRegression [json-only-source]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
//
// WO-1170 SECTION 6 - the standing oracle, and it is the ONLY thing left open on that
// ticket (sites 1-3 landed; sites 4, 5 and 6 are WITHDRAWN as mis-specified, recorded in
// the WO). Owner, 2026-08-24, verbatim: "We need to not have anything pulled other than
// from json" / "Otherwise we always expose risks like that and it's sloppy development".
//
// THE RULE IT ENFORCES (WO-1170 section 1):
//   A canonical JSON file is the ONLY place its data may be written. A fallback may be
//   GENERATED from that JSON and gated on a content hash. It may NEVER be hand-maintained.
//
// ⛔ WHY A COMMENT CANNOT BE THE MECHANISM, which is the whole reason this file exists:
//   Tower.cs used to carry "the hard-coded fallback table - identical to the shipped JSON".
//   That sentence was an ASSERTION WITH NOTHING ENFORCING IT: the day someone tuned
//   tower-perks.json the two disagreed, and a parse failure would have quietly reverted
//   every tower in the game to last month's balance - during an incident, which is exactly
//   when nobody can tell the fallback from the fault. WO-1170 section 5 names two sanctioned
//   outcomes and only two: codegen + hash gate, or delete and fail LOUDLY. "Keep the two in
//   sync" is not a third option; it is the defect.
//
// WHAT IT PROVES (every case asserts the GOOD path, never only the failure - a
// failure-only oracle is how this repo once shipped a guard that aborted every good run
// while exiting 0):
//   (1) [generated-pairs]  - each converted site still has ALL FOUR of: the canonical JSON,
//                            the generator, the generated *.g.cs (carrying its recorded
//                            SourceSha256), and a consumer that NAMES the generated type.
//                            Positively asserted, and the pair COUNT is asserted too, so a
//                            table that quietly empties cannot pass by covering nothing.
//   (2) [no-hand-mirror]   - repo scan of RUNTIME source (Assets/_Modules, excluding *.g.cs):
//                            a file that names a canonical JSON file must not also DECLARE a
//                            static fallback/built-in member initialised to a literal. Hits
//                            must match the allowlist exactly; a NEW one FAILS by name.
//   (3) [allowlist-live]   - every allowlist row must STILL be a live hit. A row whose file
//                            was deleted or already fixed FAILS, so the allowlist can never
//                            silently grow stale and start excusing something else.
//   (4) [no-orphan-codegen]- every *FallbackGenerator.cs has its *.g.cs and vice versa. A new
//                            generated fallback that arrives without its generator (or a
//                            generator whose output was deleted) is caught here.
//
// ⚠ WHAT IT DELIBERATELY DOES NOT TRY TO DO. A general "is this C# literal semantically the
//   same data as that JSON" detector is not buildable at acceptable precision - measured on
//   this tree, a shared-string-token heuristic flags 150 files, nearly all of them DTO
//   property names, and a gate that cries wolf 150 times is worse than no gate. So this
//   suite detects the SHAPE every historic offender actually had: a static member NAMED for
//   a fallback, initialised to a literal, in a file that names a canonical JSON. That shape
//   caught Tower.BuiltInFallback, BuildCategoryRegistry's three tables and
//   StakeRewardsResolver.DefaultTiers when they existed, and today it flags exactly two rows,
//   both allowlisted below with their reason.
//
// Marker: JSON_ONLY_SOURCE_OK / JSON_ONLY_SOURCE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.JsonMirrorLiteralRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into DataRegression.RunAll
// is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class JsonMirrorLiteralRegression
    {
        private const string CanonicalDir = "Assets/Resources/Data/Canonical";
        private const string RuntimeRoot  = "Assets/_Modules";
        private const string EditorRoot   = "Assets/Editor";

        /// <summary>One converted site: the JSON, its generator, its generated file, its consumer.</summary>
        private sealed class GeneratedPair
        {
            public string Json;
            public string Generator;
            public string Generated;
            public string Consumer;
            public string TypeName;
        }

        // WO-1137 (site 0) + WO-1170 sites 1-3. Every one of these was a hand-written table
        // before it was generated; the pair is what replaced the comment with a mechanism.
        private static readonly GeneratedPair[] Pairs =
        {
            new GeneratedPair {
                Json      = CanonicalDir + "/structures-catalog.json",
                Generator = EditorRoot + "/CatalogFallbackGenerator.cs",
                Generated = RuntimeRoot + "/Village/Catalog/Generated/CatalogFallbackData.g.cs",
                Consumer  = RuntimeRoot + "/Village/Catalog/CatalogBootstrap.cs",
                TypeName  = "CatalogFallbackData",
            },
            new GeneratedPair {
                Json      = CanonicalDir + "/build-categories.json",
                Generator = EditorRoot + "/BuildCategoryFallbackGenerator.cs",
                Generated = RuntimeRoot + "/Village/Catalog/Generated/BuildCategoryFallbackData.g.cs",
                Consumer  = RuntimeRoot + "/Village/Catalog/BuildCategoryRegistry.cs",
                TypeName  = "BuildCategoryFallbackData",
            },
            new GeneratedPair {
                Json      = CanonicalDir + "/tower-perks.json",
                Generator = EditorRoot + "/TowerPerkFallbackGenerator.cs",
                Generated = RuntimeRoot + "/Village/Buildings/Generated/TowerPerkFallbackData.g.cs",
                Consumer  = RuntimeRoot + "/Village/Buildings/Tower.cs",
                TypeName  = "TowerPerkFallbackData",
            },
            new GeneratedPair {
                Json      = CanonicalDir + "/stake-rewards.json",
                Generator = EditorRoot + "/StakeRewardsFallbackGenerator.cs",
                Generated = RuntimeRoot + "/Core/Platform/Generated/StakeRewardsFallbackData.g.cs",
                Consumer  = RuntimeRoot + "/Core/Platform/StakeRewardsResolver.cs",
                TypeName  = "StakeRewardsFallbackData",
            },
        };

        /// <summary>
        /// The two live hits that are NOT WO-1170 offences, each with the reason it is not.
        /// ⛔ Adding a row here is a decision, not a formality: it must be a fallback that
        /// carries NO table (WO-1170's harm is a wrong TABLE substituting different game
        /// rules) and that says so out loud when it is taken. Case [allowlist-live] fails if
        /// a row here stops being a hit, so this list can never quietly excuse something else.
        /// </summary>
        private static readonly Dictionary<string, string> Allowed =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    RuntimeRoot + "/Village/Enemies/AggroTuning.cs",
                    "Fallback() is 'new TuningDoc()' - the DTO's own scalar field defaults, not a table, and " +
                    "Load() emits FlowTrace.Warn naming the two leash numbers before it is ever taken. Nothing " +
                    "is mirrored: there is no per-id row here to drift."
                },
                {
                    RuntimeRoot + "/Village/World/HarvestTuning.cs",
                    "Fallback() is 'new TuningDoc()' - the same scalar-default shape as AggroTuning, and Load() " +
                    "warns with the yield/cooldown/siteBase numbers before it is taken. No table, nothing to drift."
                },
            };

        // The SHAPE every historic offender had: a static member named for a fallback, whose
        // initialiser opens a literal. 'LoadGeneratedFallback' is the SANCTIONED shape (it
        // reads the *.g.cs) and is excluded by name below, not by this pattern.
        private static readonly Regex FallbackDecl = new Regex(
            @"static\s+(?:readonly\s+)?[\w<>,\[\]\s\.]+?\b(\w*(?:Fallback|BuiltIn|Builtin|Hardcoded)\w*)\s*" +
            @"(?:=\s*new|\(\s*\)\s*(?:=>\s*new|\x7B))",   // \x7B = the open brace, written escaped so the
            // repo brace-balance check (CLAUDE.md 1) is not skewed by a literal brace in a string.
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex LineComment  = new Regex(@"//[^\n]*", RegexOptions.CultureInvariant);
        private static readonly Regex BlockComment = new Regex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("JSON_ONLY_SOURCE_OK - " + reason);
            else Debug.LogError("JSON_ONLY_SOURCE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            int scanned = 0;
            int hitCount = 0;
            try
            {
                Case(failures, "generated-pairs", () => CaseGeneratedPairs(failures));
                Case(failures, "no-orphan-codegen", () => CaseNoOrphanCodegen(failures));

                var hits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Case(failures, "scan", () => scanned = ScanRuntime(hits, failures));
                hitCount = hits.Count;
                Case(failures, "no-hand-mirror", () => CaseNoHandMirror(hits, failures));
                Case(failures, "allowlist-live", () => CaseAllowlistLive(hits, failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "JSON ONLY SOURCE OK - " + Pairs.Length + " canonical JSON files fall back through a " +
                         "GENERATED *.g.cs (json + generator + generated + consumer all present, each consumer " +
                         "naming its generated type), every generator has its output and every generated file " +
                         "has its generator, and a scan of " + scanned + " runtime source file(s) found " +
                         hitCount + " hand-written fallback declaration(s) - all " + Allowed.Count +
                         " allowlisted scalar-default sites, none of them a table.";
                return true;
            }
            reason = "json-only-source FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  (1) generated-pairs
        // =====================================================================

        private static void CaseGeneratedPairs(List<string> failures)
        {
            if (Pairs.Length == 0)
            {
                failures.Add("[generated-pairs] the pair table is EMPTY - this case would certify nothing.");
                return;
            }
            int ok = 0;
            foreach (var p in Pairs)
            {
                bool good = true;
                if (!File.Exists(p.Json))
                {
                    failures.Add("[generated-pairs] canonical source '" + p.Json + "' is MISSING - " + p.TypeName +
                                 " is generated FROM it, so the fallback has no authority behind it.");
                    good = false;
                }
                if (!File.Exists(p.Generator))
                {
                    failures.Add("[generated-pairs] generator '" + p.Generator + "' is MISSING - " + p.TypeName +
                                 " can no longer be regenerated, which turns it back into a hand-maintained file " +
                                 "the moment anyone edits " + Path.GetFileName(p.Json) + " (WO-1170 section 5).");
                    good = false;
                }
                if (!File.Exists(p.Generated))
                {
                    failures.Add("[generated-pairs] generated fallback '" + p.Generated + "' is MISSING - run " +
                                 "the generator; do NOT hand-write a replacement table.");
                    good = false;
                }
                else
                {
                    string gen = File.ReadAllText(p.Generated);
                    if (gen.Length == 0)
                        failures.Add("[generated-pairs] '" + p.Generated + "' is EMPTY.");
                    if (gen.IndexOf("SourceSha256", StringComparison.Ordinal) < 0)
                    {
                        failures.Add("[generated-pairs] '" + p.Generated + "' records no SourceSha256 - the hash " +
                                     "IS the gate (WO-1170 section 1: generated AND hash-gated). Without it the " +
                                     "file is just a checked-in copy that drifts silently.");
                        good = false;
                    }
                }
                if (!File.Exists(p.Consumer))
                {
                    failures.Add("[generated-pairs] consumer '" + p.Consumer + "' is MISSING.");
                    good = false;
                }
                else if (File.ReadAllText(p.Consumer).IndexOf(p.TypeName, StringComparison.Ordinal) < 0)
                {
                    failures.Add("[generated-pairs] '" + p.Consumer + "' no longer names '" + p.TypeName +
                                 "' - it has stopped loading the generated fallback. Either it fails loudly now " +
                                 "(fine - delete the pair row and say so) or a hand-written table came back " +
                                 "(not fine - that is the WO-1170 defect returning).");
                    good = false;
                }
                if (good) ok++;
            }
            if (ok != Pairs.Length)
                failures.Add("[generated-pairs] only " + ok + " of " + Pairs.Length +
                             " converted sites are fully intact.");
        }

        // =====================================================================
        //  (4) no-orphan-codegen
        // =====================================================================

        private static void CaseNoOrphanCodegen(List<string> failures)
        {
            if (!Directory.Exists(EditorRoot))
            {
                failures.Add("[no-orphan-codegen] " + EditorRoot + " does not exist - the generators cannot be " +
                             "enumerated (this is a FAILURE, not a skip).");
                return;
            }
            if (!Directory.Exists(RuntimeRoot))
            {
                failures.Add("[no-orphan-codegen] " + RuntimeRoot + " does not exist - the generated files cannot " +
                             "be enumerated (this is a FAILURE, not a skip).");
                return;
            }

            var generators = new List<string>(Directory.GetFiles(EditorRoot, "*FallbackGenerator.cs", SearchOption.AllDirectories));
            var generated  = new List<string>(Directory.GetFiles(RuntimeRoot, "*FallbackData.g.cs", SearchOption.AllDirectories));
            if (generators.Count == 0)
                failures.Add("[no-orphan-codegen] NO *FallbackGenerator.cs found under " + EditorRoot +
                             " - WO-1170's whole sanctioned outcome 1 has vanished.");
            if (generated.Count == 0)
                failures.Add("[no-orphan-codegen] NO *FallbackData.g.cs found under " + RuntimeRoot + ".");

            var generatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string g in generated)
                generatedNames.Add(Path.GetFileName(g).Replace(".g.cs", ""));   // e.g. CatalogFallbackData

            foreach (string gen in generators)
            {
                // CatalogFallbackGenerator.cs -> expects CatalogFallbackData.g.cs
                string stem = Path.GetFileName(gen).Replace("Generator.cs", "Data");
                if (!generatedNames.Contains(stem))
                    failures.Add("[no-orphan-codegen] generator '" + gen + "' has no matching '" + stem +
                                 ".g.cs' under " + RuntimeRoot + " - either its output was deleted (the consumer " +
                                 "is now falling back to nothing) or it was never run.");
            }

            var generatorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string g in generators)
                generatorNames.Add(Path.GetFileName(g).Replace("Generator.cs", "Data"));
            foreach (string g in generated)
            {
                string stem = Path.GetFileName(g).Replace(".g.cs", "");
                if (!generatorNames.Contains(stem))
                    failures.Add("[no-orphan-codegen] generated file '" + g + "' has NO generator " +
                                 "('" + stem.Replace("Data", "Generator") + ".cs') - a *.g.cs nobody can " +
                                 "regenerate is a hand-maintained file wearing a generated file's name, which is " +
                                 "precisely what WO-1170 forbids.");
            }
        }

        // =====================================================================
        //  The scan
        // =====================================================================

        /// <summary>Returns the number of runtime files scanned; fills <paramref name="hits"/> with path -> member.</summary>
        private static int ScanRuntime(Dictionary<string, string> hits, List<string> failures)
        {
            if (!Directory.Exists(CanonicalDir))
            {
                failures.Add("[scan] " + CanonicalDir + " does not exist - there is no canonical JSON set to " +
                             "compare source against (this is a FAILURE, not a skip).");
                return 0;
            }
            var canonical = new List<string>();
            foreach (string j in Directory.GetFiles(CanonicalDir, "*.json", SearchOption.AllDirectories))
                canonical.Add(Path.GetFileName(j));
            if (canonical.Count == 0)
            {
                failures.Add("[scan] " + CanonicalDir + " holds NO .json files - the scan would match nothing " +
                             "and pass vacuously.");
                return 0;
            }

            if (!Directory.Exists(RuntimeRoot))
            {
                failures.Add("[scan] " + RuntimeRoot + " does not exist - no runtime source to scan.");
                return 0;
            }

            int scanned = 0;
            foreach (string path in Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) continue;
                string norm = path.Replace('\\', '/');
                string src = File.ReadAllText(path);
                scanned++;

                bool namesCanonical = false;
                foreach (string name in canonical)
                    if (src.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) { namesCanonical = true; break; }
                if (!namesCanonical) continue;

                // Comments explain the rule; only CODE can break it.
                string code = BlockComment.Replace(LineComment.Replace(src, ""), "");
                foreach (Match m in FallbackDecl.Matches(code))
                {
                    string member = m.Groups[1].Value;
                    // The SANCTIONED shape: reading the generated *.g.cs is the fix, not the defect.
                    if (member.IndexOf("Generated", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (hits.ContainsKey(norm)) hits[norm] = hits[norm] + "," + member;
                    else hits[norm] = member;
                }
            }
            if (scanned == 0)
                failures.Add("[scan] scanned ZERO runtime .cs files under " + RuntimeRoot +
                             " - the no-hand-mirror case below would pass by covering nothing.");
            return scanned;
        }

        // =====================================================================
        //  (2) no-hand-mirror
        // =====================================================================

        private static void CaseNoHandMirror(Dictionary<string, string> hits, List<string> failures)
        {
            foreach (var kv in hits)
            {
                if (Allowed.ContainsKey(kv.Key)) continue;
                failures.Add("[no-hand-mirror] '" + kv.Key + "' names a canonical JSON file AND declares the " +
                             "hand-written fallback member(s) '" + kv.Value + "'. WO-1170 (owner 2026-08-24: " +
                             "\"We need to not have anything pulled other than from json\") allows exactly two " +
                             "outcomes: GENERATE the fallback from the JSON and hash-gate it (the *FallbackData" +
                             ".g.cs pattern - see Assets/Editor/CatalogFallbackGenerator.cs), or DELETE it and " +
                             "fail loudly with a worded reason (FlowTrace.Fail). A hand-written table plus a " +
                             "\"keep the two in sync\" comment is not a third option - that comment is what let " +
                             "Tower.cs promise a table \"identical to the shipped JSON\" with nothing enforcing " +
                             "it. If this member carries NO table (scalar defaults only) and warns out loud " +
                             "before it is taken, add it to the allowlist in this file WITH that reason.");
            }
        }

        // =====================================================================
        //  (3) allowlist-live - a stale exemption is itself a defect
        // =====================================================================

        private static void CaseAllowlistLive(Dictionary<string, string> hits, List<string> failures)
        {
            if (Allowed.Count == 0)
            {
                // An empty allowlist is LEGAL - it means every runtime fallback is generated.
                // It is asserted rather than returned on, so this case can never reach the
                // green column by having nothing to check: with no exemptions, there must be
                // no hits either.
                if (hits.Count > 0)
                    failures.Add("[allowlist-live] the allowlist is EMPTY but the scan found " + hits.Count +
                                 " hand-written fallback declaration(s) - see [no-hand-mirror].");
                return;
            }
            foreach (var kv in Allowed)
            {
                if (!File.Exists(kv.Key))
                {
                    failures.Add("[allowlist-live] allowlisted file '" + kv.Key + "' NO LONGER EXISTS - delete " +
                                 "the row. A dead exemption is an exemption nobody is reading, and the next hit " +
                                 "in a renamed file would look like it had been considered.");
                    continue;
                }
                if (!hits.ContainsKey(kv.Key))
                    failures.Add("[allowlist-live] allowlisted file '" + kv.Key + "' no longer trips the " +
                                 "detector - the hand-written fallback it excused is gone (good) or the detector " +
                                 "stopped seeing it (bad). Either way this row now excuses nothing: remove it, or " +
                                 "find out why the pattern stopped matching before trusting this suite again.");
            }
        }
    }
}
