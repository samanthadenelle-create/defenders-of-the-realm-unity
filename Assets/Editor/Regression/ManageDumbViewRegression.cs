// =============================================================================
// ManageDumbViewRegression [manage-dumb-view] -- WO-2002.
// -----------------------------------------------------------------------------
// THE ORACLE THAT MAKES CANON 9 ENFORCEABLE.
//
// 00_MANAGE_REDESIGN_CANON.md section 9 lists twelve things a Manage view MAY NOT do:
// calculate costs, inspect player resources to decide affordability, decide locks,
// determine Heart requirements, determine whether an item is max level, inspect
// queue service state, calculate queue capacity, derive labels from enum names,
// parse ids, decide which destination a prerequisite CTA opens, calculate upgrade
// deltas, mutate save data, and call Barracks / BuildTimer / Research / Heart
// services directly. CLI_DRIVING_PLAN.md section 2 Wave 1 is explicit about how that
// list survives contact with a codebase:
//
//     "WO-2002's 'views may not' list is the load-bearing part.
//      Enforce it with a source ORACLE, not with review."
//
// A rule nobody can check is a rule that decays, and this repo has the receipts -
// the stale WO-number block (CLAUDE.md 2), the retired dependency table (5), the
// copy-pasted R2 verify (16). All three were true when written. This suite is the
// alternative to a fourth.
//
// -----------------------------------------------------------------------------
// WHAT IT SCANS, AND WHY THAT IS THE HONEST SCOPE
// -----------------------------------------------------------------------------
// The COMMON renderer (Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs) and
// the contract it binds (ManageViewContract.cs). Those are the two files WO-2002
// creates; the three tab VMs that FEED them are model code and are deliberately
// out of scope - a VM is supposed to compute costs.
//
// ⚠ WHAT A GREEN HERE DOES NOT PROVE: that the three tabs actually route through
// this renderer. That is WO-2001's re-point and WO-2006/2008/2010's binding, and it
// needs its own case added to this suite when those land - the natural shape is
// "no file under Assets/_Modules/Village/UI/Manage/ contains a second tile builder".
// Stated rather than implied, so nobody reads this green as "the whole redesign is
// dumb-UI clean".
//
// Text is normalised with RegressionSourceText before matching:
//   * BANNED patterns run over StripCommentsAndStrings, so this suite's own prose
//     and the renderer's header - which NAMES every banned token in order to forbid
//     it - cannot read as a violation. That exact false alarm has fired four times
//     in one night in this tree (RegressionSourceText.cs:9-16).
//   * REQUIRED patterns run over StripComments, which leaves string literals intact.
//
// SELF-TESTED FIRST. Case [dumb-view-self-test] drives every banned pattern against
// a fixture it MUST match, before any of them is trusted against the real file. A
// regex nobody has seen match is not an oracle - the same stance
// HollowPassFixtures.SelfTest takes, and the same one ManageStateModelRegression's
// negative half takes.
//
// Marker: MANAGE_DUMB_VIEW_OK / MANAGE_DUMB_VIEW_FAIL <case>.
// EXPECTED ON ARRIVAL: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "manage-dumb-view suite", () => { if (!DeNelle.Editor.ManageDumbViewRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[manage-dumb-view] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Editor.Regression;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Source oracle: the common Manage renderer may hold no game rules.</summary>
    public static class ManageDumbViewRegression
    {
        private const string Tag = "[manage-dumb-view]";

        // The two files WO-2002 creates. PINNED PATHS on purpose: if one is renamed or
        // moved the suite goes RED rather than quietly scanning nothing, which is the
        // hollow pass RegressionMarkerRegression RULE 4 exists to stop.
        private const string PanelRel = "Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs";
        private const string ContractRel = "Assets/_Modules/Core/Manage/ManageViewContract.cs";

        // Floors that make "the file is there" mean something. A stub that satisfies the
        // banned-pattern scan by being empty must not pass.
        private const int PanelMinLines = 300;
        private const int ContractMinLines = 150;

        /// <summary>
        /// One forbidden shape: what it is, how it is spelled, a fixture proving the pattern
        /// really matches, and the ONE-LINE REVERT RECIPE that turns this case RED on demand.
        /// </summary>
        private sealed class BannedShape
        {
            public string Name;
            public string Pattern;
            public bool IgnoreCase;
            /// <summary>A line the pattern MUST match. Drives [dumb-view-self-test].</summary>
            public string Fixture;
            /// <summary>What canon 9 bullet this is, and the one edit that proves the case RED.</summary>
            public string Why;
        }

        // =====================================================================
        //  THE BANNED SET. One entry per canon 9 bullet, plus the shapes WO-2002
        //  lists under "Explicit prohibitions".
        //
        //  ⚠ TWO WORD COLLISIONS ARE DESIGNED AROUND, NOT DISCOVERED LATER:
        //   * "resources". Canon bans inspecting PLAYER resources; UnityEngine's
        //     Resources.Load is an unrelated API. The player-resource banks are
        //     banned BY NAME (TownBankCapacity / ResourceBank / Wallet) and
        //     Resources.Load is banned SEPARATELY and for a different reason (the
        //     renderer must go through ManageArt so there is one loader, one cache
        //     and one miss-trace).
        //   * "Count". `i < list.Count` is a loop; `x.Count < max` is a capacity
        //     check. Only the LEFT-HAND form is banned, which is why the pattern
        //     anchors the comparison AFTER .Count and never before it.
        // =====================================================================
        private static readonly BannedShape[] Banned =
        {
            new BannedShape
            {
                Name = "service-or-state-reference",
                Pattern = @"\b(GameState|GameStateService|BuildTimerService|BuildTimerConfig|BarracksService|" +
                          @"BarracksProgression|ResearchService|HeartProgression|VillageTierService|" +
                          @"ModifierService|BuildingUpgradeService|TownBankCapacity|ResourceBank|StorageCapsCatalog)\b",
                Fixture = "var lvl = HeartProgression.Level;",
                Why = "canon 9: no direct Barracks/BuildTimer/Research/Heart service calls. " +
                      "REVERT RECIPE (RED): add `var t = GameState.Current;` to any method in " +
                      "ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                Name = "affordability-or-gate-comparison",
                // ⚠ CARVE-OUT, MEASURED NOT GUESSED: `costs == null` is an existence test, not an
                // affordability test, and it fired on the renderer's own CostLine() on the first
                // sweep. The negative lookahead excludes a comparison against `null` and nothing
                // else - `gold >= cost` and `if (cost > 0)` both still match.
                Pattern = @"\b(cost|price|gold|wood|iron|stone|crystal|magic|heartlevel|maxlevel|" +
                          @"requirement|afford)\w*\s*(<=|>=|==|<|>)(?!\s*null\b)",
                IgnoreCase = true,
                Fixture = "if (gold >= cost) Buy();",
                Why = "canon 9: the view may not calculate costs, inspect resources, decide locks, " +
                      "determine Heart requirements or determine max level. " +
                      "REVERT RECIPE (RED): add `if (cost > 0) { }` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                // WO-2002 spells this prohibition `if (level == maxLevel)` - the gate token is on
                // the RIGHT of the operator, so the left-anchored pattern above cannot see it.
                // Both directions are needed; one alone leaves half the WO's own list unguarded.
                Name = "max-level-determination",
                Pattern = @"\b\w*[Ll]evel\s*(==|>=|<=|<|>)\s*\w*(MaxLevel|maxLevel|MaxTier|LevelCap)\b" +
                          @"|\b(MaxLevel|maxLevel|MaxTier|LevelCap)\s*(==|>=|<=|<|>)",
                Fixture = "if (level == maxLevel) HideUpgrade();",
                Why = "canon 9: the view may not determine whether an item is max level. " +
                      "ManageUpgradeTrack.Max is the model's verdict and ManageTileVisualState.Max " +
                      "is what the tile paints. " +
                      "REVERT RECIPE (RED): add `if (level == maxLevel) { }` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                // The second half of WO-2002's `if (heartLevel >= requirement)`, right-anchored for
                // the same reason as the entry above.
                // ⚠ CARVE-OUT, MEASURED NOT GUESSED: the operator set is deliberately the TWO-CHAR
                // forms only. A bare `<` or `>` right-anchored matches GENERIC TYPE ARGUMENTS -
                // `IReadOnlyList<ManageCostVM> costs` produced two hits and `i < costs.Count` a
                // third on the first sweep, all three of them ordinary code. Nothing is lost: a
                // single-char comparison with the gate token on the LEFT is still caught by
                // 'affordability-or-gate-comparison' above, and WO-2002's own two right-anchored
                // prohibitions (`heartLevel >= requirement`, `level == maxLevel`) both use a
                // two-char operator.
                Name = "gate-comparison-right-anchored",
                Pattern = @"(<=|>=|==)\s*\w*(cost|price|requirement|capacity|afford|heartlevel)\w*\b",
                IgnoreCase = true,
                Fixture = "if (heartLevel >= requirement) Unlock();",
                Why = "canon 9: the view may not determine Heart requirements or decide locks. " +
                      "ManageActionAvailability.PrerequisiteBlocked plus ManageAction.BlockerReason " +
                      "carry the verdict and the sentence. " +
                      "REVERT RECIPE (RED): add `if (n >= requirement) { }` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                Name = "queue-capacity-arithmetic",
                // ⚠ CARVE-OUT, MEASURED NOT GUESSED: `Count > 0` is an EMPTINESS test and fired on
                // the renderer's own `tab.Filters.Count > 0` on the first sweep. A CAPACITY check
                // compares against a limit, never against literal zero, so excluding a bare 0 costs
                // no teeth - `queue.Count < maxJobs` and `queue.Count < 5` both still match.
                Pattern = @"\.Count\s*(<=|>=|<|>)(?!\s*0\b)",
                Fixture = "if (queue.Count < maxJobs) Start();",
                Why = "canon 9: the view may not inspect queue service state or calculate queue " +
                      "capacity. ManageQueueVM.AtCapacity is the model's verdict. " +
                      "REVERT RECIPE (RED): add `if (faces.Count < 2) { }` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                Name = "id-parsing",
                Pattern = @"\w*[Ii]d\s*\.\s*(Split|StartsWith|EndsWith|Contains|IndexOf|Substring)\s*\(",
                Fixture = "if (itemId.StartsWith(\"collector_\")) { }",
                Why = "canon 9: the view may not parse IDs. ManageTileVM.Id is carried for the " +
                      "composer, never read for meaning. " +
                      "REVERT RECIPE (RED): add `if (tile.Id.Contains(\"x\")) { }` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                Name = "id-switch",
                Pattern = @"switch\s*\(\s*[A-Za-z_]\w*[Ii]d\b",
                Fixture = "switch (buildingId) { default: break; }",
                Why = "WO-2002 explicit prohibition `switch(itemId)`. A switch on a STATE enum is " +
                      "fine and is how ManageArt picks a frame; a switch on an ID is a rule engine. " +
                      "REVERT RECIPE (RED): add `switch (tileId) { }` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                Name = "label-derived-from-enum",
                Pattern = @"\b(VisualState|StyleRole|Badge|Availability|Ownership|UpgradeTrack|Kind)\s*\.\s*ToString\s*\(" +
                          @"|\bEnum\s*\.\s*(GetName|GetValues)\s*\(",
                Fixture = "label.text = tile.VisualState.ToString();",
                Why = "canon 9: the view may not derive labels from enum names. Every word is " +
                      "supplied (StateText / Label / BadgeText). " +
                      "REVERT RECIPE (RED): add `var s = Enum.GetName(typeof(ManageTabId), 0);`."
            },
            new BannedShape
            {
                Name = "state-inferred-from-a-null-callback",
                Pattern = @"\b(Activate|OpenQueue|Invoke)\s*(==|!=)\s*null",
                Fixture = "if (vm.Activate != null) ShowButton();",
                Why = "ManageStateModel.cs: 'Invoke being null is an implementation detail, NOT a " +
                      "state'. Read Visible/Enabled and call `cb?.Invoke()`. " +
                      "REVERT RECIPE (RED): change a `face.Activate?.Invoke()` site to " +
                      "`if (face.Activate != null) face.Activate();`."
            },
            new BannedShape
            {
                Name = "state-inferred-from-interactability",
                Pattern = @"(if|while)\s*\([^)]*\.interactable\b",
                Fixture = "if (btn.interactable) Highlight();",
                Why = "canon 7: 'The UI must never infer these states from button interactability.' " +
                      "Writing .interactable from an explicit Enabled field is correct; READING it " +
                      "back as state is not. " +
                      "REVERT RECIPE (RED): add `if (btn.interactable) { }` to BuildActionRow."
            },
            new BannedShape
            {
                Name = "destination-decided-by-the-view",
                Pattern = @"\bPanelId\s*\.|\.Route\b|\bManageRouteKind\b",
                Fixture = "Open(PanelId.Heart);",
                Why = "canon 9: the view may not decide which destination a prerequisite CTA opens. " +
                      "ManageVmProjection binds the route INTO ManageActionVM.Activate, which is why " +
                      "the renderer never sees a ManageRoute. " +
                      "REVERT RECIPE (RED): add `var k = action.Route.Kind;` to ManageWorkspacePanel.cs."
            },
            new BannedShape
            {
                Name = "save-mutation",
                Pattern = @"\bSaveSchema\b|\bMarkDirty\b|\bSaveGame\b|PlayerPrefs\s*\.\s*Set|\bTrySave\b",
                Fixture = "PlayerPrefs.SetInt(\"manage.tab\", 1);",
                Why = "canon 9: the view may not mutate save data. Last-used tab is model state " +
                      "(canon 2). REVERT RECIPE (RED): add `PlayerPrefs.SetInt(\"x\", 1);`."
            },
            new BannedShape
            {
                Name = "cost-or-delta-arithmetic",
                Pattern = @"\bTierCost\b|\bComputeCost\b|\bCostOf\s*\(|\bUpgradeDelta\b|\bNextTierCost\b",
                Fixture = "int c = BuildingUpgradeService.TierCost(id, 2);",
                Why = "canon 9: the view may not calculate costs or upgrade deltas. ManageCostVM and " +
                      "ManageStatVM.DeltaText arrive pre-computed. " +
                      "REVERT RECIPE (RED): add `int c = TierCost(1);` plus a local method of that name."
            },
            new BannedShape
            {
                Name = "duration-formatted-in-the-view",
                Pattern = @"\bTimeSpan\b|\bFromSeconds\s*\(|/\s*3600\b|%\s*60\b",
                Fixture = "string t = TimeSpan.FromSeconds(s).ToString();",
                Why = "a countdown is DERIVED TEXT, and canon 9 gives derived text to the model. " +
                      "ManageVmProjection.FormatDuration owns the one countdown grammar. " +
                      "REVERT RECIPE (RED): add `var ts = TimeSpan.FromSeconds(1);`."
            },
            new BannedShape
            {
                Name = "second-art-loader",
                Pattern = @"\bResources\s*\.\s*Load\b",
                Fixture = "var s = Resources.Load<Sprite>(key);",
                Why = "not a canon 9 bullet but the same disease (CLAUDE.md 16 - duplicated state): " +
                      "the renderer loads art through ManageArt.LoadSprite so there is ONE cache, ONE " +
                      "Texture2D fallback and ONE miss-trace. " +
                      "REVERT RECIPE (RED): add `Resources.Load<Sprite>(\"x\");` to PaintSprite."
            },
            new BannedShape
            {
                Name = "service-locator-reach",
                Pattern = @"\bCoreServices\s*\.|\bFindObjectsByType\b|\bFindFirstObjectByType\b|\bGetComponentInParent\s*<",
                Fixture = "var hud = CoreServices.Hud;",
                Why = "canon 9: the view is handed everything it renders. A renderer that goes " +
                      "LOOKING for a collaborator has started deciding. " +
                      "REVERT RECIPE (RED): add `var h = CoreServices.Hud;`."
            },
        };

        // The ONLY namespaces the renderer may import. Anything else is a new dependency
        // and must be argued for here rather than noticed later.
        private static readonly HashSet<string> AllowedUsings = new HashSet<string>(StringComparer.Ordinal)
        {
            "System",
            "System.Collections.Generic",
            "DeNelle.Core.Diagnostics",   // FlowTrace / Guard - CLAUDE.md 12 makes these mandatory, not optional
            "DeNelle.Core.UI",            // ElarionUiKit / ElarionUi - the shared visual primitives canon 9 allows
            "TMPro",
            "UnityEngine",
            "UnityEngine.UI",
        };

        // The contract types the renderer must actually bind. Proves it renders THIS
        // contract rather than having quietly grown its own parallel one.
        private static readonly string[] RequiredContractTypes =
        {
            "ManageWorkspaceVM", "ManageTabVM", "ManageTileVM", "ManageSelectionVM",
            "ManageActionVM", "ManageActivityVM", "ManageQueueVM",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== ManageDumbViewRegression (WO-2002) ===\n");
            try
            {
                SelfTestPatterns(failures, log);
                string panel = ReadPinned(PanelRel, PanelMinLines, failures, log);
                string contract = ReadPinned(ContractRel, ContractMinLines, failures, log);
                CheckBannedShapes(panel, failures, log);
                CheckUsings(panel, failures, log);
                CheckBindsTheContract(panel, failures, log);
                CheckNotAMonoBehaviour(panel, failures, log);
                CheckContractIsPure(contract, failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "MANAGE_DUMB_VIEW_OK the common Manage renderer holds none of canon 9's " +
                         Banned.Length + " forbidden shapes, imports only the allowlisted namespaces, " +
                         "binds the WO-2002 contract, is not a MonoBehaviour, and the contract itself " +
                         "is Unity-free";
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "MANAGE_DUMB_VIEW_FAIL " + string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // ── CASE  [dumb-view-self-test] ───────────────────────────────────────
        // Runs FIRST. Every banned pattern is driven against a fixture it MUST match.
        // A sweep by an unproven detector is a hollow pass with the whole file inside it
        // (the lesson HollowPassFixtures.SelfTest records at RegressionMarkerRegression.cs).
        //
        // REVERT RECIPE (RED): break any Pattern above (drop a `\b`, or empty it) - this
        // case names the entry whose fixture stopped matching, before it can wave the real
        // file through.
        private static void SelfTestPatterns(List<string> failures, StringBuilder log)
        {
            int proven = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Banned.Length; i++)
            {
                var b = Banned[i];
                if (!seen.Add(b.Name))
                    failures.Add(Tag + "[dumb-view-self-test] two banned shapes share the name '" +
                                 b.Name + "' - a failure would not say which fired");
                if (string.IsNullOrEmpty(b.Fixture))
                {
                    failures.Add(Tag + "[dumb-view-self-test] banned shape '" + b.Name +
                                 "' carries no fixture, so nobody has ever seen it match");
                    continue;
                }
                if (!Match(b, b.Fixture))
                {
                    failures.Add(Tag + "[dumb-view-self-test] banned shape '" + b.Name +
                                 "' does NOT match its own fixture (" + b.Fixture + ") - the pattern is " +
                                 "broken and would wave every real violation through");
                    continue;
                }
                if (string.IsNullOrEmpty(b.Why) || b.Why.IndexOf("REVERT RECIPE", StringComparison.Ordinal) < 0)
                    failures.Add(Tag + "[dumb-view-self-test] banned shape '" + b.Name +
                                 "' carries no REVERT RECIPE - a case nobody can prove RED is not evidence");
                proven++;
            }
            log.AppendLine("[dumb-view-self-test] patterns proven against their fixtures " +
                           proven + "/" + Banned.Length);
        }

        // ── CASE  [dumb-view-files-exist] ─────────────────────────────────────
        // A missing or stubbed file is a FAILURE, never a skip. Returns "" so the later
        // cases still run and report, instead of the suite short-circuiting to green.
        //
        // REVERT RECIPE (RED): rename ManageWorkspacePanel.cs; this case fires on the
        // pinned path rather than the suite silently scanning nothing.
        private static string ReadPinned(string rel, int minLines, List<string> failures, StringBuilder log)
        {
            string abs = Path.Combine(GetProjectRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(abs))
            {
                failures.Add(Tag + "[dumb-view-files-exist] " + rel + " is missing. WO-2002's contract " +
                             "and renderer are pinned paths; a rename must move this pin in the same change");
                return string.Empty;
            }
            string text = File.ReadAllText(abs);
            int lines = CountLines(text);
            if (lines < minLines)
                failures.Add(Tag + "[dumb-view-files-exist] " + rel + " is only " + lines + " lines (floor " +
                             minLines + ") - a stub would satisfy every banned-pattern scan by containing " +
                             "nothing, which is a hollow pass");
            log.AppendLine("[dumb-view-files-exist] " + rel + " " + lines + " lines");
            return text;
        }

        // ── CASE  [dumb-view-banned-shapes] ───────────────────────────────────
        // Each entry's own REVERT RECIPE is printed in its failure text, so the reader is
        // told how to prove the case RED at the moment it goes red.
        private static void CheckBannedShapes(string panel, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(panel)) return;
            string stripped = RegressionSourceText.StripCommentsAndStrings(panel);
            int clean = 0;
            for (int i = 0; i < Banned.Length; i++)
            {
                var b = Banned[i];
                var m = Regex.Match(stripped, b.Pattern,
                    b.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                if (!m.Success) { clean++; continue; }
                failures.Add(Tag + "[dumb-view-banned-shapes] " + PanelRel + ":" +
                             LineOf(stripped, m.Index) + " holds the forbidden shape '" + b.Name +
                             "' (matched '" + m.Value.Trim() + "'). " + b.Why);
            }
            log.AppendLine("[dumb-view-banned-shapes] clean " + clean + "/" + Banned.Length);
        }

        // ── CASE  [dumb-view-using-allowlist] ─────────────────────────────────
        // A new `using` is a new dependency and the cheapest possible early warning that
        // the renderer has started reaching for game rules.
        //
        // REVERT RECIPE (RED): add `using DeNelle.Village;` to ManageWorkspacePanel.cs.
        private static void CheckUsings(string panel, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(panel)) return;
            string stripped = RegressionSourceText.StripComments(panel);
            var found = new List<string>();
            foreach (Match m in Regex.Matches(stripped, @"^\s*using\s+(?!static\b)([A-Za-z_][\w\.]*)\s*;",
                         RegexOptions.Multiline))
            {
                string ns = m.Groups[1].Value;
                found.Add(ns);
                if (!AllowedUsings.Contains(ns))
                    failures.Add(Tag + "[dumb-view-using-allowlist] " + PanelRel + " imports '" + ns +
                                 "', which is not on the allowlist. Canon 9's whitelist is deliberately " +
                                 "explicit (WO-2002: 'Whitelist should be explicit if a harmless UI-only " +
                                 "service is required') - add it HERE with a reason, or do not depend on it");
            }
            log.AppendLine("[dumb-view-using-allowlist] usings " + string.Join(", ", found.ToArray()));
        }

        // ── CASE  [dumb-view-binds-the-contract] ──────────────────────────────
        // Proves the renderer renders THIS contract. Without it, a panel could pass every
        // banned-pattern scan while having grown a second, private set of view models -
        // which is exactly the "three independent UI systems" canon 10 forbids.
        //
        // REVERT RECIPE (RED): delete the ManageActivityVM parameter from BuildActivity.
        private static void CheckBindsTheContract(string panel, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(panel)) return;
            string stripped = RegressionSourceText.StripComments(panel);
            int bound = 0;
            for (int i = 0; i < RequiredContractTypes.Length; i++)
            {
                string t = RequiredContractTypes[i];
                if (Regex.IsMatch(stripped, @"\b" + t + @"\b")) { bound++; continue; }
                failures.Add(Tag + "[dumb-view-binds-the-contract] " + PanelRel + " never names " + t +
                             ". Canon 10 asks for ONE presentation path; a renderer that does not bind " +
                             "the shared contract is the start of a second one");
            }
            log.AppendLine("[dumb-view-binds-the-contract] bound " + bound + "/" + RequiredContractTypes.Length);
        }

        // ── CASE  [dumb-view-is-not-a-panel-type] ─────────────────────────────
        // The renderer is a plain class a host embeds, NOT a destination panel.
        // PanelDoorRegression.cs:20-29 defines panel-like as MonoBehaviour + a name ending
        // in "Panel", and FAILS any such type with no door - so making this a
        // MonoBehaviour would break that oracle for a naming reason and invite an
        // allowlist entry, which is how a real doorless panel would then hide.
        //
        // REVERT RECIPE (RED): change the class declaration to
        // `public sealed class ManageWorkspacePanel : MonoBehaviour`.
        private static void CheckNotAMonoBehaviour(string panel, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(panel)) return;
            string stripped = RegressionSourceText.StripCommentsAndStrings(panel);
            // The hex escape \x7B is the open-brace character. It is written as an escape, not
            // literally, so this file's own brace count stays balanced - CLAUDE.md 1's quality
            // gate counts RAW braces, and a lone open brace inside a regex character class reads
            // to that gate as a mismatched file. Measured: it took this file to 51/50 on the
            // first sweep.
            if (Regex.IsMatch(stripped, @"\bclass\s+\w+\s*:\s*[^\x7B\r\n]*\bMonoBehaviour\b"))
                failures.Add(Tag + "[dumb-view-is-not-a-panel-type] " + PanelRel + " declares a " +
                             "MonoBehaviour. The common renderer is embedded by a host, not routed to; " +
                             "a MonoBehaviour named *Panel with no door fails PanelDoorRegression and " +
                             "would need an allowlist entry it does not deserve");
            else
                log.AppendLine("[dumb-view-is-not-a-panel-type] plain class, PanelDoorRegression unaffected");
        }

        // ── CASE  [contract-is-pure] ──────────────────────────────────────────
        // The contract must be composable and testable with no Unity object graph, which
        // is what lets a VM be validated headless and lets a second renderer bind the same
        // records. A Sprite or a Color field would silently end that.
        //
        // REVERT RECIPE (RED): add `using UnityEngine;` to ManageViewContract.cs.
        private static void CheckContractIsPure(string contract, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(contract)) return;
            string stripped = RegressionSourceText.StripComments(contract);
            if (Regex.IsMatch(stripped, @"^\s*using\s+UnityEngine", RegexOptions.Multiline))
                failures.Add(Tag + "[contract-is-pure] " + ContractRel + " imports UnityEngine. The " +
                             "presentation contract addresses every visual as a STRING KEY so it can be " +
                             "composed and validated with no Unity object graph");
            else if (Regex.IsMatch(stripped, @"\b(Sprite|Texture2D|Color|GameObject|Transform)\b"))
                failures.Add(Tag + "[contract-is-pure] " + ContractRel + " names a Unity visual type. " +
                             "Asset keys are strings; a Sprite field makes the contract untestable outside " +
                             "play mode");
            else
                log.AppendLine("[contract-is-pure] no UnityEngine dependency");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static bool Match(BannedShape b, string text) =>
            Regex.IsMatch(text, b.Pattern, b.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++) if (text[i] == '\n') line++;
            return line;
        }

        private static int CountLines(string text)
        {
            int n = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        /// <summary>
        /// The repo root, derived from Application.dataPath rather than hardcoded. CLAUDE.md 0:
        /// the root is machine-dependent (C:\eoa on one seat, D:\eoa on another) and a doc that
        /// names one is how a seat follows canon to a path that does not exist.
        /// </summary>
        private static string GetProjectRoot() => Directory.GetParent(Application.dataPath).FullName;
    }
}
