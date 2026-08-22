// =============================================================================
// RegressionMarkerRegression [regression-marker]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.   Markers: REGRESSION_MARKER_OK / _FAIL.
//
// THE ORACLE THAT KILLS A WHOLE CLASS OF INVISIBLE GATE FAILURE.
//
// Project law is "judge by the MARKER, never the exit code" (memory
// `gates-report-success-without-proving-it`). That law only holds while a marker
// identifies WHICH suite produced it and while the thing the gate runs is the
// thing the gate claims to run. On 2026-08-02 both halves were broken at once:
//
//   * THREE classes emitted the identical bare literal `REGRESSION_OK` -
//     DataRegression.RunAll (the real gate, ~90 registered oracle suites),
//     SessionRegression.RunAll (6 checks), and Assets/Editor/RegressionSuite.cs
//     (22 cases). A log containing REGRESSION_OK did not say which one ran.
//   * tools/regression/checkin_gate.ps1 invoked the 22-case LEGACY suite and
//     judged it by that shared marker, so every "REGRESSION_OK" a RESULT file
//     cited from the check-in path was the SMALL suite's pass. Roughly 64 oracle
//     suites had never run in the automated check-in path at all.
//   * Several suite files existed on disk with a full Run(out reason) contract
//     and were never registered anywhere - a file that never runs.
//
// This suite asserts the invariants that make those three states impossible,
// by SCANNING SOURCE under Assets/Editor (no scene, no play mode, no runtime
// singletons - it is decidable from text, which is why it can be trusted to run
// in the same batch it is guarding):
//
//   RULE 1  [marker-uniqueness]  No two distinct ORACLE files emit the same
//           `*_OK` marker literal in live code. Scoped to oracle files
//           (*Regression.cs / *Oracle.cs / *Audit.cs + RegressionSuite.cs) so
//           scene builders that happen to share a NAV_OK token are not dragged in.
//           KnownDuplicateMarkers is a NAMED, SHRINKING allowlist for pre-existing
//           debt - never a place to park a new collision.
//
//   RULE 2  [registration]  Every file under Assets/Editor/Regression that
//           exposes `public static bool Run(out string <name>)` is referenced in
//           DataRegression.RunAll. An unregistered oracle is a file that never
//           runs. A suite may opt out ONLY by saying so in its own header (see
//           StandaloneOptOutTokens) - the way RepairProbeRegression does.
//
//   RULE 3  [gate-grep]  Every marker literal a gate .ps1 actually greps for
//           (Select-String / -Pattern lines under tools/ and .claude/skills/)
//           is emitted by exactly ONE class under Assets/Editor. Zero owners =
//           the gate can never pass. Two owners = the gate cannot tell which
//           suite passed, which is the 2026-08-02 bug itself.
//
//   RULE 4  [hollow-pass ratchet]  A registered suite must be ABLE to go red, and
//           must not answer OK out of a dependency-missing guard without having
//           asserted anything ("no-op and report OK"). Many suites need runtime
//           state (GameStateService.Instance, GearLoadout.Current, the economy
//           ledger) that is NULL in editor batchmode; the tempting shape is
//           `if (x == null) { reason = "skipped"; return true; }`, which
//           green-passes forever and defeats the oracle.
//
//           ⚠ REWRITTEN 2026-08-21 (WO-1138). The detector used to inspect a
//           ~4-LINE WINDOW around the `return`. On 2026-08-21 it caught ONE hollow
//           pass in CosmeticApplyRegression.cs; a human then read the SAME FILE and
//           found FIVE MORE, all real, all invisible for one reason - their guarding
//           `if` sat further than four lines from the return. Coverage was a function
//           of CODE FORMATTING. Detection now lives in HollowPassScanner and walks
//           CONTROL FLOW (brace depth + statement boundaries), so guard-to-return
//           distance is irrelevant; and HollowPassFixtures.SelfTest runs FIRST, on
//           the real 2026-08-21 evidence, because a sweep by an unproven detector is
//           a hollow pass with the whole tree inside it.
//
//           Pre-existing debt is ledgered PER SITE in KnownHollowSites - never per
//           FILE, which is what made the old baseline hide new hollow passes too.
//           Legit cases opt out with `hollow-pass-ok` INSIDE the guard block.
//
// Self-reference: this file is EXCLUDED from the RULE 1 emitter scan (it names
// other suites' markers in its own allowlists, which would read as emitting them)
// and is subject to RULE 2 like every other suite - its own registration line in
// DataRegression.RunAll is what satisfies it.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.RegressionMarkerRegression.RunStandalone
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RegressionMarkerRegression
    {
        // ---------------------------------------------------------------------
        //  Scan scope
        // ---------------------------------------------------------------------
        private const string SelfFileName = "RegressionMarkerRegression.cs";
        private const string RegistryFileName = "DataRegression.cs";

        // Declared as a balanced PAIR on one line on purpose: a lone opening-brace char
        // literal trips the CLAUDE.md rule-1 naive brace counter + the CompileGate scan.
        private const char OpenBrace = '{', CloseBrace = '}';

        // Gate-script roots (relative to the project root). node_modules is skipped.
        private static readonly string[] GateScriptRoots = { "tools", ".claude/skills" };

        // A file counts as an ORACLE (RULE 1 scope) by name.
        private static bool IsOracleFile(string fileName)
        {
            if (string.Equals(fileName, SelfFileName, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(fileName, "RegressionSuite.cs", StringComparison.OrdinalIgnoreCase)) return true;
            return fileName.EndsWith("Regression.cs", StringComparison.Ordinal)
                || fileName.EndsWith("Oracle.cs", StringComparison.Ordinal)
                || fileName.EndsWith("Audit.cs", StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------
        //  RULE 1 allowlist - PRE-EXISTING duplicate marker literals.
        //  SHRINK THIS LIST. Do not grow it. Each entry names live debt.
        // ---------------------------------------------------------------------
        //  DUNGEON_EXIT_OK - DungeonExitRegression.cs and
        //  DungeonExitReachableRegression.cs both emit it. They are two different
        //  suites (one asserts the exit exists, one asserts it is reachable) and
        //  both are registered in DataRegression.RunAll. Their DataRegression tags
        //  collided too ("[dungeon-exit]" twice) - that half was fixed 2026-08-02
        //  (the second is now "[dungeon-exit-reachable]"); renaming the marker
        //  literal itself means editing those suite bodies and is owed.
        private static readonly HashSet<string> KnownDuplicateMarkers = new HashSet<string>(StringComparer.Ordinal)
        {
            "DUNGEON_EXIT_OK",
        };

        // ---------------------------------------------------------------------
        //  RULE 2 opt-out - a suite that DECLARES itself standalone in its header.
        // ---------------------------------------------------------------------
        private static readonly string[] StandaloneOptOutTokens =
        {
            "NOT wired into DataRegression",     // RepairProbeRegression's declaration
            "regression-registry: standalone",   // explicit opt-out token for new files
        };

        // ---------------------------------------------------------------------
        //  RULE 4 baseline - files that ALREADY answer OK out of a guard.
        //  RATCHET: a NEW file doing this fails. These are owed cleanups.
        // ---------------------------------------------------------------------
        //  EMPTIED 2026-08-16. All six baselined files (HeroLocomotionClip, OfflineHarvest,
        //  VillageEconomy, ModalArbiterRegistration, UiMvvmConformance, UiObsidianConformance)
        //  were converted from a silent `return true` to a DECLARED stand-down via
        //  RegressionOutcome.Skip, which the ratchet accepts and which DataRegression counts
        //  in the third (skipped) column instead of the green one. So did 18 more sites the
        //  ratchet had never seen: seven that guarded on a NEGATED CALL rather than a null
        //  test, and eleven that stood down with a bare `return;` out of a void section.
        //
        //  A baseline entry is a hole in the net, not a note in a ledger: while a file sat
        //  in this list, EVERY hollow pass in it was invisible, including new ones. It is
        //  empty because the debt was paid, and it must stay empty - a new entry here is a
        //  suite excused from the only rule that proves it asserts anything. Declare the
        //  stand-down with RegressionOutcome.Skip / PartialSkip instead; that is honest and
        //  visible, and it costs one line.
        private static readonly HashSet<string> KnownHollowPassFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
        };

        private const string HollowPassOptOut = HollowPassScanner.OptOutToken;

        // ---------------------------------------------------------------------
        //  RULE 4b LEDGER  --  PER-SITE, NEVER PER-FILE (WO-1138, 2026-08-21)
        // ---------------------------------------------------------------------
        // When the ratchet was widened from a 4-line window to a control-flow walk, the
        // sweep surfaced 26 pre-existing hollow passes that the window had never been able
        // to see. They are recorded HERE, one row each, and every row names the SITE.
        //
        // ⛔ THIS IS NOT KnownHollowPassFiles WEARING A NEW HAT, AND THE DIFFERENCE IS THE
        // WHOLE POINT. A FILE-level baseline made every hollow pass in that file invisible
        // - including ones added afterwards. A SITE row excuses exactly one guard: add a
        // second hollow pass to any file below and it fails on the next run.
        //
        // The key is (file, arm, CONDENSED GUARD CONDITION) - deliberately NOT a line
        // number, because a line number rots on the first reformat and would hand this rule
        // back the formatting-dependence WO-1138 exists to delete. Editing the guard's own
        // condition drops the row, which is correct: a changed guard is a site that must be
        // looked at again.
        //
        // EVERY ROW IS OWED A RESOLUTION under the three-way rule. Rows are removed by
        // FIXING the site, never by editing this table to match reality:
        //   fixture-absent            -> FAIL, naming the missing path
        //   harness-capability-absent -> RegressionOutcome.PartialSkip (the visible third column)
        //   content/art-absent        -> assert THROUGH the proven fallback
        // A row whose site is gone is reported in the reason line as owed removal.
        //
        // Dungeon rows are flagged: those files were under an active bake lane on
        // 2026-08-21 and were deliberately not edited by this work order.
        private struct HollowSite
        {
            public string File, Arm, Guard, Owed;
        }

        private static HollowSite L(string file, string arm, string guard, string owed)
        {
            return new HollowSite { File = file, Arm = arm, Guard = guard, Owed = owed };
        }

        private static int IndexOfSite(List<HollowSite> sites, string file, string arm, string guard)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                if (string.Equals(sites[i].File, file, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sites[i].Arm, arm, StringComparison.Ordinal) &&
                    string.Equals(sites[i].Guard, guard, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private static readonly HollowSite[] KnownHollowSites =
        {
            L("AdPlacementCovenantRegression.cs", "A-missing-dependency", "string.IsNullOrEmpty(v)",
              "an empty JSON string value silently ends the walk"),
            L("BarracksBlankTownRegression.cs", "A-missing-dependency", "instField == null || stateField == null",
              "reflection seam moved -> fixture-absent, owes a FAIL"),
            L("BattleMonthlyRegression.cs", "A-missing-dependency", "cards == null",
              "no monthlyCards array -> the anti-inflation invariant is unmeasured"),
            L("BattleMonthlyRegression.cs", "A-missing-dependency", "!File.Exists(path)",
              "INVERTED: absence is the pass here; owes a per-site hollow-pass-ok naming that"),
            L("BreakableContainerChestRegression.cs", "A-missing-dependency", "chest == null",
              "chest type not resolved -> fixture-absent, owes a FAIL"),
            L("BuildMenuRealEconomyRegression.cs", "A-missing-dependency", "options == null || options.Count == 0",
              "the FUNDED-SPEND positive control asserts nothing with no options"),
            L("CollectorIncomeRegression.cs", "A-missing-dependency", "entries == null",
              "structures.json parsed but carried no entries -> fixture-absent"),
            L("CombatAtbRegression.cs", "B-says-skip", "float.IsNaN(regionHp) || float.IsNaN(garrisonBaseHp)",
              "divergence case stands down in words only -> owes PartialSkip"),
            L("DataWebRegression.cs", "A-missing-dependency", "!File.Exists(picksPath)",
              "opt-in editor tooling -> likely a legitimate per-site hollow-pass-ok"),
            L("DialogueRegression.cs", "A-missing-dependency", "entry == null",
              "a dialogue with NO entry node is skipped silently; that is itself a defect"),
            L("DungeonComposedPillarsRegression.cs", "A-missing-dependency", "arr == null",
              "DUNGEON LANE - not touched by WO-1138"),
            L("DungeonComposedPillarsRegression.cs", "A-missing-dependency", "t == null",
              "DUNGEON LANE - not touched by WO-1138"),
            L("DungeonRoomOwnershipRegression.cs", "A-missing-dependency", "layout?.rooms == null",
              "DUNGEON LANE - not touched by WO-1138"),
            L("ItemIdentityRegression.cs", "D-vacuous-against-absent-fixture", "vm != null",
              "every assertion hangs off the VM source loading -> owes a fixture-health assert"),
            L("ManaSpendRegression.cs", "D-vacuous-against-absent-fixture", "prod != null",
              "every assertion hangs off the producer source loading"),
            L("ManageTroopsTrainDoorRegression.cs", "D-vacuous-against-absent-fixture", "vm != null",
              "every assertion hangs off the VM source loading"),
            L("PromoRedeemEntryRegression.cs", "A-missing-dependency", "dead == null",
              "a tracked source file at a hardcoded path -> fixture-absent, owes a FAIL"),
            L("QuestCompletabilityRegression.cs", "A-missing-dependency", "isLive == null",
              "reflected member absent -> fixture-absent, owes a FAIL"),
            L("ShippedSurfaceGateRegression.cs", "D-vacuous-against-absent-fixture", "flags != null",
              "every assertion hangs off the flags source loading"),
            L("ShippedSurfaceGateRegression.cs", "D-vacuous-against-absent-fixture", "flags != null",
              "second section, same shape"),
            L("StructureTargetableRegression.cs", "A-missing-dependency", "setter == null",
              "SceneOwnership.SetEnemyOwned renamed -> owes PartialSkip or a FAIL"),
            L("TownBankCapRegression.cs", "A-missing-dependency", "arr == null",
              "packs.json parsed but carried no array -> fixture-absent"),
            L("TownBankCapRegression.cs", "A-missing-dependency", "!Directory.Exists(modules)",
              "the module root is tracked; its absence is a broken gate, not an option"),
            L("TutorialStepReachabilityRegression.cs", "A-missing-dependency", "kitBlock.Length == 0",
              "an empty extracted block skips the kit assertions silently"),
            L("UiCaptureCoverageRegression.cs", "D-vacuous-against-absent-fixture", "driver != null",
              "every assertion hangs off the capture driver resolving"),
            L("WalletIdentityRegression.cs", "A-missing-dependency", "js == null",
              "api/ auth source absent -> the note SAYS it is not silently passing, and it is"),
            L("VfxLoopFlagRegression.cs", "A-missing-dependency", "string.IsNullOrEmpty(path)",
              "a prefab with no asset path bumps stats.Unresolvable and returns - the SKIP IS " +
              "COUNTED, which is more honest than silence, but nothing asserts the count is small, " +
              "so a catalog that resolves NOTHING still reads green. Owes a zero/ratio guard on " +
              "stats.Unresolvable. (Surfaced by a concurrent lane's 2026-08-21 edit to this file.)"),
        };

        // ---------------------------------------------------------------------
        //  Regexes
        // ---------------------------------------------------------------------
        // An _OK marker literal appearing inside a string literal in live code.
        private static readonly Regex MarkerInLiteral = new Regex(
            "\"[^\"\\n]*?\\b([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*_OK)\\b", RegexOptions.Compiled);

        // A marker token anywhere in a PowerShell grep line.
        private static readonly Regex MarkerToken = new Regex(
            "\\b([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*_(?:OK|FAIL))\\b", RegexOptions.Compiled);

        private static readonly Regex RunSignature = new Regex(
            "public\\s+static\\s+bool\\s+Run\\s*\\(\\s*out\\s+string\\s+\\w+\\s*\\)", RegexOptions.Compiled);

        private static readonly Regex ClassDecl = new Regex(
            "\\b(?:public|internal)\\s+(?:static\\s+|sealed\\s+|partial\\s+)*class\\s+(\\w+)", RegexOptions.Compiled);

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Standalone batch entry - prints REGRESSION_MARKER_OK / _FAIL.</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("REGRESSION_MARKER_OK - " + reason);
            else Debug.LogError("REGRESSION_MARKER_FAIL - " + reason);
        }

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try
            {
                return RunCore(out reason);
            }
            catch (Exception ex)
            {
                reason = "REGRESSION MARKER: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        // =====================================================================
        //  Body
        // =====================================================================
        private static bool RunCore(out string reason)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                reason = "REGRESSION MARKER: could not resolve the project root from Application.dataPath";
                return false;
            }

            string editorDir = Path.Combine(projectRoot, "Assets", "Editor");
            string regressionDir = Path.Combine(editorDir, "Regression");
            if (!Directory.Exists(editorDir) || !Directory.Exists(regressionDir))
            {
                reason = "REGRESSION MARKER: Assets/Editor or Assets/Editor/Regression is missing - cannot verify";
                return false;
            }

            string registryPath = Path.Combine(regressionDir, RegistryFileName);
            if (!File.Exists(registryPath))
            {
                reason = "REGRESSION MARKER: " + RegistryFileName + " not found - the suite registry is gone";
                return false;
            }
            string registryBody = ExtractRunAllBody(ReadOrEmpty(registryPath));
            if (string.IsNullOrEmpty(registryBody))
            {
                reason = "REGRESSION MARKER: could not locate DataRegression.RunAll's body - registration cannot be verified";
                return false;
            }

            var failures = new List<string>();

            // Every .cs under Assets/Editor, with comments stripped once.
            var allEditorFiles = Directory.GetFiles(editorDir, "*.cs", SearchOption.AllDirectories);
            var codeByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rawByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in allEditorFiles)
            {
                string raw = ReadOrEmpty(p);
                rawByPath[p] = raw;
                codeByPath[p] = StripLineComments(raw);
            }

            // -----------------------------------------------------------------
            //  RULE 1 - marker uniqueness across oracle files
            // -----------------------------------------------------------------
            var markerOwners = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var allMarkerOwners = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            int oracleFileCount = 0;
            foreach (var kv in codeByPath)
            {
                string name = Path.GetFileName(kv.Key);
                bool isSelf = string.Equals(name, SelfFileName, StringComparison.OrdinalIgnoreCase);
                bool isOracle = IsOracleFile(name);
                if (isOracle) oracleFileCount++;

                foreach (Match m in MarkerInLiteral.Matches(kv.Value))
                {
                    string marker = m.Groups[1].Value;
                    if (!isSelf) Add(allMarkerOwners, marker, name);
                    if (isOracle) Add(markerOwners, marker, name);
                }
            }

            // NON-UNITY EMITTERS (2026-08-20). RULE 3 asks "does SOMEBODY emit the marker this
            // gate greps for?" — and the answer is not always a C# class. The content-ship gate
            // (tools/r2-ship.ps1) greps R2_PUSH_OK / R2_PARITY_OK, which are printed by
            // tools/r2_sync.py, a PYTHON tool. Scanning only Assets/Editor made those two read as
            // "NO class emits it - that gate stage can never pass", which is false: they pass every
            // time the content ship runs.
            //
            // ⛔ THIS IS A WIDENED SCAN, NOT AN ALLOWLIST, and the distinction is the point. An
            // allowlist would have silenced the two names and, with them, the actual protection: a
            // typo'd R2_PARITY_OKK in the ship script must STILL fail this suite. Crediting the real
            // emitter keeps the rule's teeth while telling the truth about who emits what.
            foreach (var toolRoot in GateScriptRoots)
            {
                string toolDir = Path.Combine(projectRoot, toolRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(toolDir)) continue;
                foreach (var py in Directory.GetFiles(toolDir, "*.py", SearchOption.AllDirectories))
                {
                    if (py.Replace('\\', '/').IndexOf("/node_modules/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string pyName = Path.GetFileName(py);
                    foreach (Match m in MarkerToken.Matches(ReadOrEmpty(py)))
                    {
                        string tok = m.Groups[1].Value;
                        if (tok.EndsWith("_FAIL", StringComparison.Ordinal)) continue;
                        Add(allMarkerOwners, tok, pyName);
                    }
                }
            }

            foreach (var kv in markerOwners)
            {
                if (kv.Value.Count < 2) continue;
                if (KnownDuplicateMarkers.Contains(kv.Key)) continue;
                failures.Add("marker '" + kv.Key + "' is emitted by " + kv.Value.Count +
                             " distinct oracle files (" + string.Join(", ", kv.Value.ToArray()) +
                             ") - a log carrying it cannot say WHICH suite passed. Give each a distinct marker.");
            }

            // -----------------------------------------------------------------
            //  RULE 2 - every Run(out string) oracle is registered
            //  RULE 4 - and can actually go red / does not answer OK from a guard
            // -----------------------------------------------------------------
            // RULE 4 SELF-TEST, and it runs FIRST. A sweep performed by an unproven
            // detector is a hollow pass with the whole tree inside it - which is exactly
            // what the retired 4-line window was, silently, until 2026-08-21.
            string detectorDetail;
            bool detectorOk = HollowPassFixtures.SelfTest(out detectorDetail);
            if (!detectorOk) failures.Add(detectorDetail);

            int registered = 0, optedOut = 0, hollowBaseline = 0;
            int emptyIterationAdvisories = 0, emptyIterationFiles = 0;   // RULE 5 (advisory)
            var newHollow = new List<string>();
            var ledger = new List<HollowSite>(KnownHollowSites);
            foreach (var p in Directory.GetFiles(regressionDir, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(p);
                string code = codeByPath.ContainsKey(p) ? codeByPath[p] : StripLineComments(ReadOrEmpty(p));
                string raw = rawByPath.ContainsKey(p) ? rawByPath[p] : ReadOrEmpty(p);
                // ⛔ FIXTURE FILES HOLD FAKE ORACLE SOURCE AS TEST DATA (added 2026-08-22).
                // HollowPassFixtures.cs stores sample suites - including a deliberately CLEAN one -
                // as verbatim string constants, so this text scan reads "public static class
                // CleanSuite { ... Run(out string ...) }" and demands it be registered. It is not an
                // oracle; it is the control the hollow-pass detector is measured against.
                //
                // A DECLARATION INSIDE A STRING LITERAL IS NOT A DECLARATION - the same class as
                // counting braces that live inside quoted source. The general fix is to strip
                // verbatim string literals before this scan; that is a wider change to a
                // load-bearing gate, so this is a NARROW, NAMED exclusion of one file and the
                // general case is left flagged rather than silently absorbed.
                if (name.Equals("HollowPassFixtures.cs", StringComparison.OrdinalIgnoreCase)) continue;
                if (!RunSignature.IsMatch(code)) continue;

                // Which class in this file owns the Run(out string) entry point?
                foreach (string cls in ClassesWithRunEntryPoint(code))
                {
                    bool inRegistry = Regex.IsMatch(registryBody, "\\b" + Regex.Escape(cls) + "\\.Run\\s*\\(\\s*out\\b");
                    if (inRegistry) { registered++; continue; }
                    if (StandaloneOptOutTokens.Any(t => raw.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    { optedOut++; continue; }
                    failures.Add("oracle '" + cls + "' (" + name + ") exposes Run(out string) but is NOT referenced in " +
                                 "DataRegression.RunAll - an unregistered oracle is a file that never runs. " +
                                 "Register it, or declare 'regression-registry: standalone' in its header.");
                }

                // RULE 4a - can it go red at all?
                bool canFail = code.Contains("return false")
                            || Regex.IsMatch(code, "return\\s+\\w+\\.Count\\s*==\\s*0")
                            || Regex.IsMatch(code, "return\\s+\\w+\\s*\\.\\s*Count\\s*==\\s*0");
                bool optedOutHollow = raw.IndexOf(HollowPassOptOut, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!canFail && !optedOutHollow && !KnownHollowPassFiles.Contains(name))
                    newHollow.Add(name + " has no failing path at all (no 'return false', no 'return <list>.Count == 0') - it can only ever report OK");

                // RULE 4b - does it answer OK out of a dependency-missing guard, ANYWHERE
                // in the enclosing block? Delegated to HollowPassScanner, which walks the
                // CONTROL FLOW (brace depth + statement boundaries) instead of a line
                // window. RAW source is passed deliberately: the scanner does its own
                // comment/literal masking, and it needs the comments intact to honour the
                // per-SITE opt-out and an "already reported elsewhere" note at the site.
                string scanErr;
                var hollow = HollowPassScanner.Scan(raw, name, out scanErr);
                if (!string.IsNullOrEmpty(scanErr))
                {
                    // A detector that cannot read its input has proven NOTHING. Reporting
                    // clean here would make this rule the very thing it hunts.
                    failures.Add("hollow-pass scan REFUSED " + name + ": " + scanErr +
                                 " - an unscannable oracle cannot be certified, so this is a FAILURE, " +
                                 "never a silent skip.");
                }
                foreach (var hp in hollow)
                {
                    int idx = IndexOfSite(ledger, name, hp.Arm, hp.Guard);
                    if (idx >= 0) { ledger.RemoveAt(idx); hollowBaseline++; continue; }
                    newHollow.Add(name + ":" + hp.Line + " [" + hp.Arm + "] guard '" + hp.Guard + "' - " + hp.Detail);
                }

                // RULE 5 - ADVISORY ONLY (see CountUnguardedDiscoveryLoops). Counted, never failed.
                int adv = CountUnguardedDiscoveryLoops(code);
                if (adv > 0) { emptyIterationAdvisories += adv; emptyIterationFiles++; }
            }
            foreach (var h in newHollow)
                failures.Add("hollow pass: " + h);

            // -----------------------------------------------------------------
            //  RULE 3 - gate scripts grep a marker somebody emits, unambiguously
            // -----------------------------------------------------------------
            int gateGreps = 0;
            foreach (var root in GateScriptRoots)
            {
                string dir = Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(dir)) continue;
                foreach (var ps1 in Directory.GetFiles(dir, "*.ps1", SearchOption.AllDirectories))
                {
                    if (ps1.Replace('\\', '/').IndexOf("/node_modules/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string scriptName = Path.GetFileName(ps1);
                    foreach (var line in ReadOrEmpty(ps1).Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.StartsWith("#")) continue;
                        if (l.IndexOf("Select-String", StringComparison.OrdinalIgnoreCase) < 0 &&
                            l.IndexOf("-Pattern", StringComparison.OrdinalIgnoreCase) < 0) continue;

                        foreach (Match m in MarkerToken.Matches(l))
                        {
                            string token = m.Groups[1].Value;
                            if (token.EndsWith("_FAIL", StringComparison.Ordinal)) continue;  // FAIL greps are diagnostics
                            gateGreps++;
                            if (!allMarkerOwners.ContainsKey(token))
                            {
                                failures.Add(scriptName + " greps for marker '" + token +
                                             "' but NO class under Assets/Editor emits it - that gate stage can never pass.");
                                continue;
                            }
                            var owners = allMarkerOwners[token];
                            if (owners.Count > 1 && !KnownDuplicateMarkers.Contains(token))
                                failures.Add(scriptName + " greps for marker '" + token + "' which " + owners.Count +
                                             " different files emit (" + string.Join(", ", owners.ToArray()) +
                                             ") - the gate cannot tell which suite it just judged.");
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                reason = "REGRESSION MARKER FAIL (" + failures.Count + "): " + string.Join(" | ", failures.ToArray());
                return false;
            }

            string ledgerNote = ledger.Count == 0
                ? "all " + KnownHollowSites.Length + " ledgered hollow-pass sites still present"
                : ledger.Count + " of " + KnownHollowSites.Length + " ledgered hollow-pass site(s) NO LONGER " +
                  "FOUND (fixed, or the guard was edited) - delete those rows: " + DescribeSites(ledger);

            reason = "REGRESSION MARKER OK -- " + oracleFileCount + " oracle files, " + markerOwners.Count +
                     " distinct _OK markers (0 undeclared collisions), " + registered +
                     " Run(out) oracles registered in DataRegression.RunAll (" + optedOut +
                     " declared standalone), " + gateGreps +
                     " gate-script marker grep(s) all resolve to exactly one emitter; hollow-pass ratchet " +
                     "(CONTROL-FLOW, no line window -- WO-1138) " + hollowBaseline + " ledgered / 0 new, " +
                     ledgerNote + "; " + detectorDetail + "; RULE 5 advisory: " + emptyIterationAdvisories +
                     " unguarded discovered-collection loop(s) across " + emptyIterationFiles +
                     " suite(s) could report OK having checked ZERO items (Shape B - see CountUnguardedDiscoveryLoops " +
                     "for why this is counted and not failed)";
            return true;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void Add(Dictionary<string, SortedSet<string>> map, string key, string value)
        {
            SortedSet<string> set;
            if (!map.TryGetValue(key, out set)) { set = new SortedSet<string>(StringComparer.Ordinal); map[key] = set; }
            set.Add(value);
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }

        /// <summary>Strips // line comments (string-literal aware enough for this scan).</summary>
        private static string StripLineComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new System.Text.StringBuilder(src.Length);
            bool inStr = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '/' && n == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    if (i < src.Length) sb.Append('\n');
                    continue;
                }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The brace-matched body of DataRegression.RunAll (the suite registry).</summary>
        // =====================================================================
        //  EXPECTED REGISTERED-SUITE COUNT  (audit finding G1)
        // =====================================================================
        // THE HOLE THIS CLOSES. DataRegression computes its headline number as
        //   suitesTotal = suitesGreen + suitesRed
        // where green counts "[tag]" lines in the log and red counts entries in
        // `failures`. 78 of the ~130 suites are registered inside Guard.Try(...)
        // with the return value DISCARDED. A suite that THROWS therefore appends
        // no [tag] line and adds no failure - it silently LEAVES THE DENOMINATOR,
        // and the marker still reads green at a smaller number
        // ("REGRESSION_OK 125/125 suites"). Nothing anywhere pinned the count, so
        // a vanished suite was indistinguishable from a suite that never existed.
        //
        // WHY THIS IS DERIVED AND NOT A LITERAL. Writing `const int Expected = 130`
        // would BE the defect it is meant to catch - the same shape as
        // SessionRegression's hardcoded "SESSION_GUARDS_OK 6/6 checks" (audit G8),
        // a count that is a LABEL rather than a measurement. So both sides are
        // measured: the expected count is counted from the SOURCE registration
        // call-sites between DataRegression's own START/END fences, and compared
        // against the count the RUN actually produced. Adding a suite moves both
        // numbers together and needs no edit here; a suite disappearing at runtime
        // moves only one, which is exactly the event we want to be loud.
        //
        // Counting rule: occurrences of ".Run(out ..." inside the fenced region of
        // RunAll's body, with line comments stripped first so a commented-out or
        // documented registration cannot inflate the expectation.
        public static bool TryGetExpectedSuiteCount(out int expected, out string detail)
        {
            expected = -1;
            detail = string.Empty;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string registryPath = Path.Combine(projectRoot, "Assets", "Editor", "Regression", RegistryFileName);
                if (!File.Exists(registryPath))
                {
                    detail = RegistryFileName + " not found at " + registryPath;
                    return false;
                }

                string body = ExtractRunAllBody(ReadOrEmpty(registryPath));
                if (string.IsNullOrEmpty(body))
                {
                    detail = "could not locate DataRegression.RunAll's body";
                    return false;
                }

                // ORDER MATTERS, and getting it wrong is a self-inflicted blind spot:
                // the fence markers THEMSELVES live inside `//` comments, so stripping
                // comments first deletes the very landmarks used to find the region.
                // Locate the fences in the RAW body, slice, and only THEN strip comments
                // from inside the slice (so a commented-out registration cannot inflate
                // the expectation).
                // Anchor on the "<<<" suffix, NOT the bare words. The fence block's own
                // instructional comment says "ADD NEW SUITE REGISTRATIONS ABOVE THE END
                // FENCE, NOT BELOW IT", so a bare IndexOf("END FENCE") matches that prose
                // ~12 lines in and slices a window containing no registrations at all -
                // which then reads as "0 call-sites" and looks like a broken regex rather
                // than a mis-anchored search. Only the real markers carry "<<<".
                // The markers also contain a non-ASCII em dash, so neither anchor spans it.
                int start = body.IndexOf("START FENCE <<<", StringComparison.Ordinal);
                int end = body.IndexOf("END FENCE <<<", StringComparison.Ordinal);
                if (start < 0 || end < 0 || end <= start)
                {
                    detail = "START/END FENCE markers not found in RunAll's body (or out of order) - " +
                             "the fenced registry region is what makes the count derivable";
                    return false;
                }

                string fenced = StripLineComments(body.Substring(start, end - start));
                expected = RunSiteInFence.Matches(fenced).Count;
                detail = "counted " + expected + " registration call-site(s) between the fences";
                return expected > 0;
            }
            catch (Exception ex)
            {
                detail = "threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>A registration call-site: `Something.Run(out var r)` / `Run(out r)`.</summary>
        private static readonly Regex RunSiteInFence = new Regex(
            @"\.Run\s*\(\s*out\s+(?:var\s+)?\w+\s*\)", RegexOptions.Compiled);

        private static string ExtractRunAllBody(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            int sig = src.IndexOf("public static void RunAll()", StringComparison.Ordinal);
            if (sig < 0) return string.Empty;
            int open = src.IndexOf(OpenBrace, sig);
            if (open < 0) return string.Empty;
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == OpenBrace) depth++;
                else if (src[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Names of the classes in one file that declare a Run(out string) entry point.
        /// A file may hold a helper class alongside the suite (TalentStrategyRegression.cs
        /// does), so the signature is attributed to the class whose declaration precedes it.
        /// </summary>
        private static List<string> ClassesWithRunEntryPoint(string code)
        {
            var result = new List<string>();
            var decls = ClassDecl.Matches(code).Cast<Match>().ToList();
            foreach (Match sig in RunSignature.Matches(code))
            {
                string owner = null;
                foreach (var d in decls)
                {
                    if (d.Index < sig.Index) owner = d.Groups[1].Value;
                    else break;
                }
                if (!string.IsNullOrEmpty(owner) && !result.Contains(owner)) result.Add(owner);
            }
            return result;
        }

        // =====================================================================
        //  WHERE THE OLD DETECTOR WENT (WO-1138, 2026-08-21)
        // =====================================================================
        // `FindHollowPassLines` used to live here: a ~4-LINE WINDOW ending at each
        // `return`. It has NOT been deleted - deleting it would have thrown away the only
        // measurement of what the widening bought. It now lives, frozen and executable, as
        // HollowPassFixtures.NarrowWindowFind, where the RULE 4 self-test RUNS it against
        // the six real CosmeticApplyRegression sites of 2026-08-21 and asserts it still
        // finds exactly ONE of them while HollowPassScanner finds all SIX.
        //
        // Detection now lives in HollowPassScanner and walks CONTROL FLOW - brace depth and
        // statement boundaries - so a guard 400 lines above its `return` is as visible as
        // one three lines above it. See that file's header for the arms, the exonerations,
        // and why none of it can rot the way a line count did.

        /// <summary>Ledger rows still unmatched after a sweep, for the reason line.</summary>
        private static string DescribeSites(List<HollowSite> sites)
        {
            var parts = new List<string>();
            foreach (var s in sites) parts.Add(s.File + " [" + s.Arm + "] '" + s.Guard + "'");
            return string.Join(", ", parts.ToArray());
        }

        // =====================================================================
        //  RULE 5 [empty-iteration ADVISORY]  --  Shape B, and the honest limit of a
        //  static scan.
        // =====================================================================
        // Shape B is a suite that iterates a collection which can be EMPTY, asserts
        // inside the loop, and then reports OK having checked nothing: "OK - 0 checked".
        // It is strictly worse than Shape A because there is no guard, no token and no
        // return-true to look at - NO TOKEN SCAN CATCHES IT AT ANY WINDOW LENGTH.
        //
        // WHY THIS IS ADVISORY AND NOT A FAILING RULE. Deciding "can this collection be
        // empty at runtime" is undecidable in general, and the decidable approximations
        // measured on this tree are hopeless as gates: "every assertion is inside a loop
        // with no zero-guard" matches 132 of ~150 registered suites, because iterating a
        // `static readonly string[]` that is never empty is the normal way to write an
        // oracle. Narrowing the source to DISCOVERED collections (Directory.GetFiles,
        // Enumerate*Sources, FindObjectsByType, GetComponentsInChildren) - the ones that
        // genuinely can come back empty - still lands on 23 files. Failing the gate on 23
        // files that were never proven wrong would be the same crime as a hollow pass,
        // pointed the other way: a number nobody can act on.
        //
        // THE REAL FIX, and it generalises to BOTH shapes: every suite DECLARES the
        // number of things it checked, and a declared zero is a failure. That is a
        // contract change across ~150 suites, so it is a work order, not a lane edit.
        // The four sites this audit could prove (CoreDataHub, EnemyPoolReset, and both
        // BuildMenuRealEconomy source-lint cases) got explicit zero-guards in their own
        // files instead - a real pin where the evidence existed, and an honest count
        // here where it did not.
        //
        // This method therefore COUNTS the discovered-collection loops that carry
        // assertions with no zero-guard and reports the number in the reason line. It
        // never fails. When the declared-count contract lands, this becomes the rule.
        private static readonly Regex DiscoverySource = new Regex(
            @"Directory\.(GetFiles|EnumerateFiles|GetDirectories)|Enumerate\w*Sources|EnumerateScripts|" +
            @"RuntimeSources|FindObjectsByType|GetComponentsInChildren|AssetDatabase\.Find",
            RegexOptions.Compiled);

        private static readonly Regex ForeachHeader = new Regex(
            @"^\s*foreach\s*\(\s*[\w<>,\[\]\.\s]+\s+\w+\s+in\s+(.+?)\s*\)\s*$", RegexOptions.Compiled);

        private static int CountUnguardedDiscoveryLoops(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                Match h = ForeachHeader.Match(lines[i].TrimEnd());
                if (!h.Success) continue;
                if (!DiscoverySource.IsMatch(h.Groups[1].Value)) continue;

                var body = new System.Text.StringBuilder();
                for (int j = i; j < Math.Min(lines.Length, i + 40); j++) body.Append(lines[j]).Append('\n');
                string b = body.ToString();
                if (b.IndexOf("failures.Add", StringComparison.Ordinal) < 0) continue;

                // A counter incremented in the body and compared against zero anywhere in
                // the file IS the zero-guard (that is the shape the four proven fixes use).
                bool guarded = false;
                foreach (Match inc in Regex.Matches(b, @"(\w+)\s*\+\+"))
                {
                    string id = inc.Groups[1].Value;
                    if (Regex.IsMatch(code, @"\b" + Regex.Escape(id) + @"\s*(==|<=|<|>)\s*0\b")) { guarded = true; break; }
                }
                if (!guarded) count++;
            }
            return count;
        }
    }
}
