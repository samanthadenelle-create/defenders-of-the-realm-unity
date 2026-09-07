// =============================================================================
// HeartUnlockBundleRegression [heart-bundle] -- WO-2004 requirements lane.
// -----------------------------------------------------------------------------
// WHAT THIS SUITE ASSERTS, IN ONE SENTENCE: a Heart Level's BUNDLE (its cost, its
// prerequisites and what it opens) comes from DATA, and a level the data forgot
// is a NAMED FAILURE AND A REFUSAL -- not a quietly empty answer, and above all
// not a free level.
//
// -----------------------------------------------------------------------------
// THE DEFECT THIS EXISTS FOR (measured at source 2026-09-07, not inferred)
// -----------------------------------------------------------------------------
// HeartProgressionCatalog.CostToReach returns 0 for a level with no authored row,
// and VillageTierService.TryUpgrade SKIPS THE SPEND ENTIRELY when the cost is 0:
//
//     int cost = NextCost();
//     if (cost > 0) { ...TrySpend... }      // <- 0 means "no spend", not "no sale"
//     s.VillageTier = Current + 1;          // <- granted regardless
//
// So a `maxLevel: 3` with only two authored rows emitted a correct, well-worded
// FlowTrace.Fail naming level 3 -- and then HANDED THE PLAYER THE REALM FOR
// NOTHING. That is the exact shape CLAUDE.md section 12 forbids: the instrument
// fired, the failure was named, and the system carried on as if it had not. A
// named Fail is only a refusal if something refuses.
//
// Three further things this suite pins, each of which the program has already
// been bitten by elsewhere:
//   * THE RULE LIVES AT THE SOLE WRITER. There are TWO doors into a Heart raise --
//     HeartProgression.TryRaise (the Heart surface) and BuildingUpgradeVM.Select
//     (VillageTierRowId) -> VillageTierService.TryUpgrade (BuildingUpgradeVM.cs:1045).
//     A check placed only on the model would leave the second door open.
//   * NO SECOND UNLOCK TABLE. WO-2004's first acceptance line is "no duplicated
//     Heart-level unlock tables; one authoritative progression table". The unlock
//     lists are DERIVED (HeartProgression.UnlocksAt walks building-tiers.json ->
//     troops.json -> population-milestones.json). The moment someone authors an
//     `unlockBuildings` array into heart-progression.json, the duplication is back
//     and the two copies begin to drift. Case 4 fails on the KEY, before anyone
//     can rely on it.
//   * THE FIXTURE RUNS THE PRODUCTION PARSER. HeartProgressionCatalog.LoadForTests
//     routes through the same ParseOrEmpty the shipped loader uses, so what this
//     suite observes is what the game does -- not what a test-only parser does
//     ("measuring something is not the same as measuring the right thing",
//     CLAUDE.md section 11B).
//
// -----------------------------------------------------------------------------
// RED RECIPES -- how to prove this suite still asserts something
// -----------------------------------------------------------------------------
//   * delete `requiresBuildings` from a level row in heart-progression.json
//        -> [bundle-requirements-are-data]
//   * make HeartProgression.RequirementsFor return Array.Empty unconditionally
//        -> [bundle-requirements-are-data]
//   * delete the `blocked` guard from VillageTierService.TryUpgrade
//        -> [bundle-enforced-at-sole-writer]  (and the free-realm hole reopens)
//   * make HeartProgressionCatalog.HasAuthoredRow return true always
//        -> [bundle-missing-row-is-named-fail]
//   * drop the FlowTrace.Fail from ResolveBundle's unauthored branch
//        -> [bundle-missing-row-is-named-fail]
//   * drop the FlowTrace.Step from ResolveBundle's success branch
//        -> [bundle-resolve-is-traced]
//   * author `"unlockBuildings": []` onto a level row
//        -> [bundle-no-second-table]
//
// Registered in DataRegression.RunAll beside the heart-surface suite:
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "heart-bundle suite", () => { if (!DeNelle.Editor.Regression.HeartUnlockBundleRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[heart-bundle] " + r); });
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class HeartUnlockBundleRegression
    {
        private const string ResourcesCopy = "Assets/Resources/Data/Canonical/heart-progression.json";
        private const string StreamingCopy = "Assets/StreamingAssets/Data/Canonical/heart-progression.json";
        private const string ServicePath = "Assets/_Modules/Village/Buildings/Progression/VillageTierService.cs";
        private const string ModelPath = "Assets/_Modules/Village/Buildings/Progression/HeartProgression.cs";

        /// <summary>The ONLY keys a level row may carry. An unlock list here is the duplicated table
        /// WO-2004 exists to prevent; a duration/reward field here is an authored promise with no
        /// production reader (see the file's own _authoringNotes for why each is absent).</summary>
        private static readonly HashSet<string> AllowedLevelKeys =
            new HashSet<string>(StringComparer.Ordinal) { "level", "costCrystal", "requiresBuildings" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== HeartUnlockBundleRegression (WO-2004 requirements lane) ===\n");

            // The fixture cases install an alternate catalog and a capturing trace sink. BOTH must be
            // put back whatever happens, or every suite that runs after this one reads this one's
            // fixture ladder and traces into a dead sink.
            var priorSink = FlowTrace.Sink;
            try
            {
                CheckRequirementsComeFromData(failures, log);
                CheckMissingRowIsNamedFailAndRefusal(failures, log);
                CheckResolveIsTraced(failures, log);
                CheckEnforcedAtSoleWriter(failures, log);
                CheckNoSecondUnlockTable(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                FlowTrace.Sink = priorSink;
                HeartProgressionCatalog.Reload();   // drop any fixture, re-read the real file
                FlowTrace.AllOn();                  // the default filter state, restored explicitly
            }

            if (failures.Count == 0)
            {
                reason = "HEART_BUNDLE_OK every Heart Level's cost, prerequisites and unlocks resolve "
                       + "from heart-progression.json through one traced seam; an unauthored level is a "
                       + "named Fail AND a refusal at the sole writer; no unlock table is duplicated "
                       + "into the ladder file";
                Debug.Log(reason + "\n" + log);
                return true;
            }
            reason = "HEART_BUNDLE_FAIL: " + string.Join("; ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // ------------------------------------------------------------------------------------
        // CASE 1 -- the requirement is DATA, and the reader turns it into a real verdict.
        // ------------------------------------------------------------------------------------
        // Two halves, and both are needed. The SHIPPED half proves the authored shape is actually
        // in the file the game reads (an authored key nobody ships is not data-driven, it is a
        // parser feature). The FIXTURE half proves the reader does something with it -- a reader
        // that always returns "satisfied" would pass the shipped half forever, because every
        // shipped row is deliberately empty.
        private static void CheckRequirementsComeFromData(List<string> failures, StringBuilder log)
        {
            var root = ReadCanonicalJson(ResourcesCopy, failures);
            if (root != null)
            {
                var levels = root["levels"] as JArray;
                if (levels == null || levels.Count == 0)
                {
                    failures.Add("[bundle-requirements-are-data] heart-progression.json has no levels array");
                }
                else
                {
                    foreach (var t in levels)
                    {
                        var o = t as JObject;
                        if (o == null) continue;
                        int lvl = o["level"] != null ? o["level"].Value<int>() : -1;
                        if (o["requiresBuildings"] == null)
                            failures.Add("[bundle-requirements-are-data] level " + lvl + " has no "
                                       + "requiresBuildings key - a Heart Level's prerequisites would then "
                                       + "be unexpressible in data, which is the state WO-2004 closed");
                        else if (!(o["requiresBuildings"] is JArray))
                            failures.Add("[bundle-requirements-are-data] level " + lvl
                                       + " requiresBuildings is not an array");
                    }
                }
            }

            // FIXTURE: one authored prerequisite that CANNOT be satisfied by construction.
            // ⚠ THE DEMANDED LEVEL IS RepoProps.MaxStructureLevel + 1, NOT A CONVENIENT SMALL NUMBER.
            // The obvious fixture ("barracks Level 2, and a headless run has no GameState so TierOf
            // returns 0") would make this case depend on RUN ORDER: DataRegression.RunAll executes
            // dozens of suites before this one, several of which boot a GameState / base layout, and a
            // real barracks tier of 2 would flip Satisfied to true and fail the case for a reason that
            // has nothing to do with the seam. One above the per-structure ceiling is unreachable by
            // design, so UNSATISFIED is a property of the ladder rather than of what ran first.
            // (The const is READ, never restated - CLAUDE.md §8: never re-hardcode a level ceiling.)
            int unreachableLevel = DeNelle.Core.Catalog.RepoProps.MaxStructureLevel + 1;
            string fixtureJson =
                "{\"version\":1,\"maxLevel\":2,\"levels\":["
                + "{\"level\":1,\"costCrystal\":250,\"requiresBuildings\":[{\"id\":\"barracks\",\"level\":"
                + unreachableLevel + "}]},"
                + "{\"level\":2,\"costCrystal\":500,\"requiresBuildings\":[]}]}";

            try
            {
                HeartProgressionCatalog.LoadForTests(fixtureJson);

                var authored = HeartProgressionCatalog.RequirementsFor(1);
                if (authored == null || authored.Count != 1 || authored[0] == null
                    || authored[0].Id != "barracks" || authored[0].Level != unreachableLevel)
                {
                    failures.Add("[bundle-requirements-are-data] HeartProgressionCatalog.RequirementsFor(1) "
                               + "did not return the one authored {barracks," + unreachableLevel + "} row "
                               + "from the fixture - the parse side of the seam is not reading "
                               + "requiresBuildings");
                }

                var resolved = HeartProgression.RequirementsFor(1);
                if (resolved == null || resolved.Count != 1)
                {
                    failures.Add("[bundle-requirements-are-data] HeartProgression.RequirementsFor(1) returned "
                               + (resolved == null ? "null" : resolved.Count.ToString())
                               + " requirements for a fixture authoring exactly one - the READER is not "
                               + "projecting authored rows into player-facing requirements");
                }
                else
                {
                    var r = resolved[0];
                    if (r.Satisfied)
                        failures.Add("[bundle-requirements-are-data] a barracks-Level-" + unreachableLevel
                                   + " requirement reads SATISFIED at building tier " + r.CurrentLevel
                                   + " - that level is one ABOVE RepoProps.MaxStructureLevel and cannot be "
                                   + "held, so the reader is not comparing against ModifierService.TierOf "
                                   + "and every authored prerequisite would pass vacuously");
                    if (string.IsNullOrEmpty(r.Text)
                        || r.Text.IndexOf("Level " + unreachableLevel, StringComparison.Ordinal) < 0)
                        failures.Add("[bundle-requirements-are-data] the requirement's player text does not "
                                   + "name the level it demands (got '" + r.Text + "')");
                    if (r.RequiredLevel != unreachableLevel)
                        failures.Add("[bundle-requirements-are-data] requirement RequiredLevel is "
                                   + r.RequiredLevel + ", expected the authored " + unreachableLevel);
                }

                // The verdict must reach the ACTION, not just the display.
                string blocked = HeartProgression.BlockedReason(1);
                if (blocked == null)
                    failures.Add("[bundle-requirements-are-data] BlockedReason(1) is null while an authored "
                               + "prerequisite is unmet - the Heart would be raisable straight past its own "
                               + "authored gate");

                var bundle = HeartProgression.ResolveBundle(1);
                if (!bundle.IsAuthored || bundle.CostCrystal != 250 || bundle.Requirements.Count != 1
                    || bundle.FirstUnmet() == null)
                {
                    failures.Add("[bundle-requirements-are-data] the level-1 bundle does not carry the "
                               + "fixture's authored cost/requirements (authored=" + bundle.IsAuthored
                               + " cost=" + bundle.CostCrystal + " reqs=" + bundle.Requirements.Count
                               + " firstUnmet=" + (bundle.FirstUnmet() ?? "<none>") + ")");
                }

                log.AppendLine("requirements: authored key present on every shipped level row; the fixture's "
                             + "one unreachable barracks row parses, resolves against ModifierService.TierOf, reads "
                             + "UNSATISFIED and blocks the raise");
            }
            finally
            {
                HeartProgressionCatalog.Reload();
            }
        }

        // ------------------------------------------------------------------------------------
        // CASE 2 -- a level the data forgot is NAMED, and it is REFUSED.
        // ------------------------------------------------------------------------------------
        // This is the case the whole suite is for. "Named" alone was already true before
        // 2026-09-07 and was worthless: the Fail line was emitted and the level was granted free.
        // Both halves are asserted here, in one case, on purpose -- splitting them would let the
        // trace half stay green while the refusal half rots.
        private static void CheckMissingRowIsNamedFailAndRefusal(List<string> failures, StringBuilder log)
        {
            const string holeJson =
                "{\"version\":1,\"maxLevel\":3,\"levels\":["
                + "{\"level\":1,\"costCrystal\":250,\"requiresBuildings\":[]},"
                + "{\"level\":2,\"costCrystal\":500,\"requiresBuildings\":[]}]}";

            var sink = new CapturingSink();
            try
            {
                FlowTrace.AllOn();
                FlowTrace.Sink = sink;
                HeartProgressionCatalog.LoadForTests(holeJson);

                if (HeartProgressionCatalog.HasAuthoredRow(3))
                    failures.Add("[bundle-missing-row-is-named-fail] HasAuthoredRow(3) is TRUE for a fixture "
                               + "with no level-3 row - the guard the sole writer depends on is blind");

                sink.Clear();
                var bundle = HeartProgression.ResolveBundle(3);

                if (bundle.IsAuthored)
                    failures.Add("[bundle-missing-row-is-named-fail] the level-3 bundle reports IsAuthored - "
                               + "an unauthored level would be indistinguishable from a free, "
                               + "unconditional one");

                if (!sink.SawErrorContaining("Heart Level 3"))
                    failures.Add("[bundle-missing-row-is-named-fail] resolving an UNAUTHORED Heart Level 3 "
                               + "emitted no error-level trace naming the level. It came back EMPTY AND "
                               + "SILENT, which reads exactly like 'this level opens nothing' "
                               + "(CLAUDE.md section 12). Errors seen: " + sink.Describe());

                string blocked = HeartProgression.BlockedReason(3);
                if (blocked == null)
                {
                    failures.Add("[bundle-missing-row-is-named-fail] BlockedReason(3) is NULL for an "
                               + "unauthored level. NextCost() returns 0 for it and "
                               + "VillageTierService.TryUpgrade skips the spend when the cost is 0, so this "
                               + "is the FREE-REALM hole: the Fail is named and the Heart is raised for "
                               + "nothing. A named failure that still grants the thing is not a refusal.");
                }

                log.AppendLine("data hole: maxLevel 3 with two rows -> HasAuthoredRow(3) false, bundle "
                             + "unauthored, an error trace names Heart Level 3, and BlockedReason refuses "
                             + "the raise");
            }
            finally
            {
                HeartProgressionCatalog.Reload();
            }
        }

        // ------------------------------------------------------------------------------------
        // CASE 3 -- the bundle resolve is INSTRUMENTED (CLAUDE.md section 12).
        // ------------------------------------------------------------------------------------
        private static void CheckResolveIsTraced(List<string> failures, StringBuilder log)
        {
            const string goodJson =
                "{\"version\":1,\"maxLevel\":1,\"levels\":["
                + "{\"level\":1,\"costCrystal\":250,\"requiresBuildings\":[]}]}";

            var sink = new CapturingSink();
            try
            {
                FlowTrace.AllOn();
                FlowTrace.Sink = sink;
                HeartProgressionCatalog.LoadForTests(goodJson);

                sink.Clear();
                HeartProgression.ResolveBundle(1);

                if (!sink.SawInfoContaining("bundle resolve"))
                    failures.Add("[bundle-resolve-is-traced] ResolveBundle(1) emitted no [Flow:Heart] step "
                               + "naming the resolve. Without it a preview that comes back empty cannot be "
                               + "told apart from a level that genuinely opens nothing - which is the read "
                               + "the next Heart ticket will need. Info lines seen: " + sink.DescribeInfo());

                if (!sink.SawInfoContaining("Heart Level 1"))
                    failures.Add("[bundle-resolve-is-traced] the resolve trace does not name WHICH level it "
                               + "resolved - an unlabelled line cannot be read back from a device log");

                log.AppendLine("instrumentation: ResolveBundle emits one [Flow:Heart] step naming the level, "
                             + "its cost, its prerequisite count and its unlock count");
            }
            finally
            {
                HeartProgressionCatalog.Reload();
            }
        }

        // ------------------------------------------------------------------------------------
        // CASE 4 -- the rule is enforced at the SOLE WRITER, not only at the model.
        // ------------------------------------------------------------------------------------
        // A source lint, deliberately: the alternative is driving a real raise, which needs a
        // GameState, an EconomyService and a crystal balance in a batchmode editor run. What must
        // not regress here is structural (WHERE the check sits), and structure is exactly what a
        // source assertion can prove honestly.
        private static void CheckEnforcedAtSoleWriter(List<string> failures, StringBuilder log)
        {
            string service = StripComments(ReadSource(ServicePath, failures));
            string model = StripComments(ReadSource(ModelPath, failures));

            if (service != null && service.IndexOf("HeartProgression.BlockedReason(", StringComparison.Ordinal) < 0)
                failures.Add("[bundle-enforced-at-sole-writer] VillageTierService.TryUpgrade no longer "
                           + "consults HeartProgression.BlockedReason. It is the SOLE WRITER of the stored "
                           + "tier and there are TWO doors into it - the Heart surface and "
                           + "BuildingUpgradeVM.Select(VillageTierRowId) (BuildingUpgradeVM.cs:1045). "
                           + "Checking only the model leaves the second door able to buy a Heart Level "
                           + "whose prerequisites are unmet, and reopens the free-realm hole for an "
                           + "unauthored row.");

            if (model != null && model.IndexOf("HeartProgressionCatalog.HasAuthoredRow(", StringComparison.Ordinal) < 0)
                failures.Add("[bundle-enforced-at-sole-writer] HeartProgression.BlockedReason no longer asks "
                           + "HeartProgressionCatalog whether the level is authored at all - the data-hole "
                           + "branch is gone and an unauthored level becomes free again");

            log.AppendLine("enforcement: VillageTierService.TryUpgrade refuses via "
                         + "HeartProgression.BlockedReason before it reaches the spend");
        }

        // ------------------------------------------------------------------------------------
        // CASE 5 -- no second unlock table. WO-2004's first acceptance line.
        // ------------------------------------------------------------------------------------
        private static void CheckNoSecondUnlockTable(List<string> failures, StringBuilder log)
        {
            var root = ReadCanonicalJson(ResourcesCopy, failures);
            if (root == null) return;

            var levels = root["levels"] as JArray;
            if (levels == null)
            {
                // ⛔ NEVER A SILENT RETURN OUT OF A MISSING DEPENDENCY (HollowPassScanner arm A,
                // caught on this file 2026-09-07 at Builds/reg-wave10b.log). The fixture here is the
                // shipped canonical file, and it is PRESENT - so this is the fixture-absent limb of
                // the three-way rule, not a harness limit: FAIL, naming the path and the key. A bare
                // `return` would have handed the caller a green verdict for a ladder file with no
                // ladder in it, which is precisely the shape this suite exists to catch elsewhere.
                failures.Add("[bundle-no-second-table] " + ResourcesCopy + " has no 'levels' array - "
                           + "the Heart ladder file parsed but carries no ladder, so this case could "
                           + "check NOTHING. There is deliberately no hand-written fallback ladder "
                           + "(WO-1170), so this is a hard failure and never a skip.");
                return;
            }

            foreach (var t in levels)
            {
                var o = t as JObject;
                if (o == null)
                {
                    failures.Add("[bundle-no-second-table] " + ResourcesCopy + " has a 'levels' entry "
                               + "that is not an object - it cannot be checked for a duplicated unlock "
                               + "table, so it is named rather than skipped past.");
                    continue;
                }
                int lvl = o["level"] != null ? o["level"].Value<int>() : -1;
                foreach (var prop in o.Properties())
                {
                    if (AllowedLevelKeys.Contains(prop.Name)) continue;
                    failures.Add("[bundle-no-second-table] level " + lvl + " authors an unexpected key '"
                               + prop.Name + "'. WO-2004's first acceptance line is 'no duplicated "
                               + "Heart-level unlock tables; one authoritative progression table' - what a "
                               + "Heart Level opens is DERIVED by HeartProgression.UnlocksAt from "
                               + "building-tiers.json / troops.json / population-milestones.json. A list "
                               + "here becomes a SECOND authority that immediately begins to drift from the "
                               + "first. If the key is genuinely new and genuinely read, add it to "
                               + "AllowedLevelKeys in the same change as its production reader.");
                }
            }

            // The mirror must carry the same shape; [ladder-mirrored] in HeartSurfaceRegression pins
            // byte parity, but a suite that reads only one copy would go green on a half-edit if that
            // case were ever narrowed.
            var mirror = ReadCanonicalJson(StreamingCopy, failures);
            if (mirror != null && !JToken.DeepEquals(root, mirror))
                failures.Add("[bundle-no-second-table] the two canonical copies of heart-progression.json "
                           + "do not parse to the same document - Resources and StreamingAssets have "
                           + "diverged");

            log.AppendLine("shape: every level row carries only {level, costCrystal, requiresBuildings}, and "
                         + "both canonical copies parse identically");
        }

        // ------------------------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------------------------

        /// <summary>A trace sink that records what it was handed, so a suite can assert on the
        /// instrument itself rather than on its side effects. Restored in Run's finally.</summary>
        private sealed class CapturingSink : ITraceSink
        {
            private readonly List<string> _info = new List<string>();
            private readonly List<string> _warn = new List<string>();
            private readonly List<string> _error = new List<string>();

            public void Info(string line) { _info.Add(line); }
            public void Warn(string line) { _warn.Add(line); }
            public void Error(string line) { _error.Add(line); }

            public void Clear() { _info.Clear(); _warn.Clear(); _error.Clear(); }

            public bool SawErrorContaining(string needle) => Contains(_error, needle);
            public bool SawInfoContaining(string needle) => Contains(_info, needle);

            public string Describe() => _error.Count == 0 ? "<none>" : string.Join(" | ", _error);
            public string DescribeInfo() => _info.Count == 0 ? "<none>" : string.Join(" | ", _info);

            private static bool Contains(List<string> lines, string needle)
            {
                for (int i = 0; i < lines.Count; i++)
                    if (lines[i] != null && lines[i].IndexOf(needle, StringComparison.Ordinal) >= 0) return true;
                return false;
            }
        }

        private static JObject ReadCanonicalJson(string path, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(),
                path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                failures.Add("[heart-bundle] missing canonical copy " + path
                           + " - there is deliberately NO hand-written fallback ladder (WO-1170), so a "
                           + "missing file is a hard failure and never a skip");
                return null;
            }
            try
            {
                return JObject.Parse(File.ReadAllText(full));
            }
            catch (Exception ex)
            {
                failures.Add("[heart-bundle] " + path + " does not parse: " + ex.Message);
                return null;
            }
        }

        private static string ReadSource(string path, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(),
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return File.ReadAllText(full);
            failures.Add("[heart-bundle] source not found: " + path + " - FAIL, not a skip");
            return null;
        }

        /// <summary>Strips // and /* */ so a rule EXPLAINED in a comment cannot be read as a rule KEPT.</summary>
        private static string StripComments(string src)
        {
            if (src == null) return null;
            src = System.Text.RegularExpressions.Regex.Replace(src, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(src, @"//[^\n]*", "");
        }
    }
}
