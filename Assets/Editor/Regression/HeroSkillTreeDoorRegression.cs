// =============================================================================
// HeroSkillTreeDoorRegression — headless oracle: the Bag's SKILLS tab is the one
// player-reachable door to the hero skill tree, and it is still wired.
// Marker: SKILL_TREE_DOOR_OK / SKILL_TREE_DOOR_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Wired into DataRegression.RunAll.
// Style/contract mirrors ManageTroopsTrainDoorRegression, which this is modelled on.
//
// WHY THIS SUITE EXISTS — the defect it is shaped around:
//   Commit d6d3146b2 ("fix(inventory): replace buried rail with gear tabs",
//   2026-08-30) replaced the Bag's scrolling rail with a tab row. The old rail
//   built EIGHT entries — Gear, Weapons, Off Hand, Armor, Trinkets, Potions,
//   SKILLS, Map. The replacement, BuildTopTabs, built SIX: Gear..Potions. The
//   Skills entry was not moved, retired or replaced; it simply stopped being
//   drawn, and with it went the last door a player could reach.
//
//   Nothing else opens PanelId.HeroSkillTree in a normal town session. The other
//   two openers are an ArcaneTower building (BuildingInteractable — needs one
//   PLACED, and a fresh save's town is blank) and a Yarn "OpenTalents" command
//   (DialogueCommandSink — no shipped script calls it). So the entire hero talent
//   stack — hero-talents.json's 83 nodes, HeroTalentCatalog, HeroSkillTreeVM,
//   HeroSkillTreePanelMvvm, TalentNodeVfxRig — was built, regression-covered by
//   FIVE suites (TalentTreeShape, TalentStrategy, TalentIconMap,
//   TalentFocusSingleton, SkillsPanelLayout) and UNREACHABLE. The owner found it
//   by playing: "I do not see a way to get to Skill Tree now".
//
//   Every one of those five suites passed the whole time, because every one tests
//   a LAYER — the graph shape, the strategy, the icons, the focus rule, the
//   layout — and none tests the DOOR. This one tests the door. Case 1 FAILS
//   against d6d3146b2 and passes after the restore.
//
// Proves, from source (the presentation is code-built uGUI; there is no play mode
// here) and from live types:
//   1. the ACTIVE tab builder names the Skills label — the case that would have
//      caught it;
//   2. the tab INDEX still equals the rail ORDINAL, because BuildTabRow forwards
//      the tab index verbatim to SelectRail (a label inserted mid-list would
//      silently route Trinkets to the skill tree);
//   3. selecting that rail routes to OpenSkillTree, which opens
//      PanelId.HeroSkillTree;
//   4. something actually REGISTERS PanelId.HeroSkillTree, so the open resolves;
//   5. the door census — the Bag opener exists, and the count of openers is
//      reported so a future seat can see at a glance whether it is still the only
//      unconditional one.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroSkillTreeDoorRegression
    {
        private const string BuilderPath = "Assets/_Modules/Village/Hero/InventoryUIBuilder.cs";
        private const string HeaderPath = "Assets/_Modules/Village/Hero/InventoryPaperDoll.cs";
        private const string ControllerPath = "Assets/_Modules/Village/Hero/HeroInventoryController.cs";
        private const string PanelPath = "Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs";
        private const string ModulesRoot = "Assets/_Modules";

        /// <summary>The rail ordinal the Skills pseudo-section is authored at (InventoryUIBuilder).</summary>
        private const int SkillsRailOrdinal = 6;

        // Brace characters as named constants rather than repeated literals. CLAUDE.md §1's
        // quality gate counts raw brace CHARACTERS across the whole file, so a source-scanning
        // oracle that spells its own delimiters inline reports a false "BRACE MISMATCH" purely
        // because it names an opener more often than a closer. Declaring each exactly once keeps
        // the count balanced and the parser below readable.
        private const char BraceOpen = '{';
        private const char BraceClose = '}';

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== HeroSkillTreeDoorRegression: the Bag's Skills tab is the hero skill-tree door ===");

            try
            {
                string builder = ReadSource(BuilderPath, failures);
                string header = ReadSource(HeaderPath, failures);
                string controller = ReadSource(ControllerPath, failures);
                string panel = ReadSource(PanelPath, failures);

                if (builder != null && header != null)
                {
                    CheckHeaderDoor(header, builder, failures, log);
                }
                if (panel != null) CheckPanelRegisters(panel, failures, log);

                CensusDoors(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add($"HeroSkillTreeDoorRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, log, out reason);
        }

        // WO-1254: Talents is deliberately a header chip, not a seventh inventory tab.
        private static void CheckHeaderDoor(string header, string builder,
                                            List<string> failures, StringBuilder log)
        {
            string rebuild = MethodBody(header, "private void RebuildHeader(");
            if (rebuild == null ||
                rebuild.IndexOf("KeyHeroSkills", StringComparison.Ordinal) < 0 ||
                rebuild.IndexOf("OpenSkillTree", StringComparison.Ordinal) < 0)
                failures.Add("[case 1] the active Bag header does not draw the canonical Skills chip " +
                             "wired to OpenSkillTree; the hero talent tree is unreachable.");
            else
                log.AppendLine("  case 1 OK - canonical Skills header chip -> OpenSkillTree");

            string open = MethodBody(builder, "private void OpenSkillTree(");
            if (open == null || open.IndexOf("PanelId.HeroSkillTree", StringComparison.Ordinal) < 0)
                failures.Add("[case 3] OpenSkillTree no longer opens PanelId.HeroSkillTree.");
            else
                log.AppendLine("  case 3 OK - OpenSkillTree -> PanelId.HeroSkillTree");
        }

        // ── CASE 1: the ACTIVE tab builder names Skills. THE case. ────────────
        // Deliberately scoped to the method BuildRail actually delegates to. The dead
        // BuildLegacyRail still contains a Skills entry in full, so a whole-file
        // Contains("KeyRailSkills") would have passed on the day the door broke — that
        // near-miss is exactly the shape of the defect and must not be re-created here.
        private static void CheckActiveTabBuilder(string builder, List<string> failures, StringBuilder log)
        {
            string railBody = MethodBody(builder, "private void BuildRail(");
            if (railBody == null)
            {
                failures.Add("[case 1] InventoryUIBuilder.BuildRail not found — the Bag's rail/tab entry point has " +
                             "been renamed, so this oracle can no longer prove the Skills door is drawn.");
                return;
            }

            string activeName = null;
            foreach (string candidate in new[] { "BuildTopTabs", "BuildLegacyRail" })
                if (railBody.IndexOf(candidate + "(", StringComparison.Ordinal) >= 0) { activeName = candidate; break; }

            if (activeName == null)
            {
                failures.Add("[case 1] BuildRail delegates to no builder this oracle recognises. Add the new builder's " +
                             "name here in the SAME commit that renames it — an unrecognised presentation path is how " +
                             "the Skills door silently stopped being drawn in the first place.");
                return;
            }
            if (activeName == "BuildLegacyRail")
                log.AppendLine("  note - BuildRail is back on the LEGACY scrolling rail presentation.");

            string activeBody = MethodBody(builder, "private void " + activeName + "(");
            if (activeBody == null)
            {
                failures.Add($"[case 1] BuildRail calls {activeName} but its body could not be read.");
                return;
            }

            if (activeBody.IndexOf("KeyRailSkills", StringComparison.Ordinal) < 0)
            {
                failures.Add($"[case 1] the ACTIVE Bag tab builder ({activeName}) does not name " +
                             "InventoryStrings.KeyRailSkills, so no Skills affordance is drawn. THIS IS THE DEFECT: " +
                             "PanelId.HeroSkillTree's only other openers are context-gated (an ArcaneTower that must " +
                             "be placed, a Yarn verb no shipped script calls), so the whole hero talent tree becomes " +
                             "unreachable in a normal town session and every talent-LAYER suite still passes.");
            }
            else
            {
                log.AppendLine($"  case 1 OK - active builder {activeName} draws the Skills entry");
            }
        }

        // ── CASE 2: tab index == rail ordinal ─────────────────────────────────
        // ElarionUiKitConformance.BuildTabRow hands its callback the tab INDEX, and
        // InventoryUIBuilder forwards it straight into SelectRail, which compares it to
        // the RailXxx constants. So the label list must be in ordinal order with no gaps
        // up to Skills. Inserting one label mid-list would route the wrong section without
        // failing anything else — a silent mis-wire, not a crash.
        private static void CheckOrdinalAlignment(string builder, string controller,
                                                  List<string> failures, StringBuilder log)
        {
            int declared = ConstValue(controller, "RailSkills");
            if (declared < 0)
                failures.Add("[case 2] HeroInventoryController.RailSkills constant not found — the rail ordinals " +
                             "this oracle checks against no longer exist.");
            else if (declared != SkillsRailOrdinal)
                failures.Add($"[case 2] RailSkills is {declared}, this oracle expects {SkillsRailOrdinal}. If the " +
                             "ordinals were deliberately renumbered, update SkillsRailOrdinal here in the SAME commit " +
                             "— the label list's position and this constant must agree or the tab opens the wrong thing.");

            string activeBody = MethodBody(builder, "private void BuildTopTabs(")
                                ?? MethodBody(builder, "private void BuildLegacyRail(");
            if (activeBody == null)
            {
                failures.Add("[case 2] neither InventoryUIBuilder.BuildTopTabs nor BuildLegacyRail could be read. " +
                             "The ordinal fixture is absent, so this case cannot prove the Skills label opens " +
                             "RailSkills. This is a FAIL, not a skip.");
                return;
            }

            int labelIndex = LabelListIndexOf(activeBody, "KeyRailSkills");
            if (labelIndex < 0)
            {
                // Case 1 already reports the missing label; only the LEGACY presentation
                // (which positions by explicit RailSkills argument, not by list order) is
                // legitimately exempt from an ordered label list.
                if (activeBody.IndexOf("KeyRailSkills", StringComparison.Ordinal) >= 0)
                    log.AppendLine("  case 2 - Skills is positioned by explicit ordinal argument, not list order; " +
                                   "ordering check not applicable.");
                return;
            }

            if (declared >= 0 && labelIndex != declared)
                failures.Add($"[case 2] the Skills label sits at position {labelIndex} in the tab label list but " +
                             $"RailSkills is {declared}. BuildTabRow forwards the tab INDEX to SelectRail verbatim, so " +
                             "this tab currently opens whatever section owns ordinal " + labelIndex + " instead of the " +
                             "skill tree. Keep the list in ordinal order with no gaps.");
            else
                log.AppendLine($"  case 2 OK - Skills label at list position {labelIndex} == RailSkills ordinal");
        }

        // ── CASE 3: the rail routes out to the panel ──────────────────────────
        private static void CheckRouteToPanel(string builder, string controller,
                                              List<string> failures, StringBuilder log)
        {
            string select = MethodBody(controller, "private void SelectRail(");
            if (select == null)
                failures.Add("[case 3] HeroInventoryController.SelectRail not found — the tab callback's landing site " +
                             "is gone, so nothing proves a Skills tap goes anywhere.");
            else if (select.IndexOf("RailSkills", StringComparison.Ordinal) < 0 ||
                     select.IndexOf("OpenSkillTree", StringComparison.Ordinal) < 0)
                failures.Add("[case 3] SelectRail no longer routes RailSkills to OpenSkillTree. The tab would be drawn " +
                             "and inert — a label that does nothing is not a door.");
            else
                log.AppendLine("  case 3 OK - SelectRail(RailSkills) -> OpenSkillTree()");

            string open = MethodBody(builder, "private void OpenSkillTree(");
            if (open == null)
                failures.Add("[case 3] InventoryUIBuilder.OpenSkillTree not found.");
            else if (open.IndexOf("PanelId.HeroSkillTree", StringComparison.Ordinal) < 0)
                failures.Add("[case 3] OpenSkillTree no longer opens PanelId.HeroSkillTree. Note PanelId.HeroTalents " +
                             "is RETIRED-UNROUTABLE (it renders black); every talents entry point must use " +
                             "HeroSkillTree.");
            else
                log.AppendLine("  case 3 OK - OpenSkillTree() -> PanelRouter.Open(PanelId.HeroSkillTree)");
        }

        // ── CASE 4: someone registers the panel, so the open resolves ─────────
        private static void CheckPanelRegisters(string panel, List<string> failures, StringBuilder log)
        {
            string code = StripLineComments(panel);
            if (code.IndexOf("PanelRouter.Register(PanelId.HeroSkillTree", StringComparison.Ordinal) < 0)
                failures.Add("[case 4] HeroSkillTreePanelMvvm no longer registers PanelId.HeroSkillTree — every door " +
                             "would open onto nothing and fall through to its 'not registered' warning.");
            else
                log.AppendLine("  case 4 OK - HeroSkillTreePanelMvvm registers PanelId.HeroSkillTree");
        }

        // ── CASE 5: the door census ───────────────────────────────────────────
        private static void CensusDoors(List<string> failures, StringBuilder log)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(),
                                       ModulesRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
            {
                failures.Add("[case 5] " + ModulesRoot + " not found — the door census could not run.");
                return;
            }

            var openers = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string code = StripLineComments(File.ReadAllText(file));
                if (code.IndexOf("PanelId.HeroSkillTree", StringComparison.Ordinal) < 0) continue;
                if (code.IndexOf("PanelRouter.Open(PanelId.HeroSkillTree", StringComparison.Ordinal) < 0 &&
                    code.IndexOf("PanelRouter.Open(DeNelle.Core.UI.PanelId.HeroSkillTree", StringComparison.Ordinal) < 0 &&
                    code.IndexOf("panelId = PanelId.HeroSkillTree", StringComparison.Ordinal) < 0) continue;
                openers.Add(Path.GetFileName(file));
            }

            log.AppendLine("  case 5 - " + openers.Count + " opener(s) of PanelId.HeroSkillTree: " +
                           (openers.Count == 0 ? "(none)" : string.Join(", ", openers.ToArray())));

            if (!openers.Contains("InventoryUIBuilder.cs"))
                failures.Add("[case 5] InventoryUIBuilder no longer opens PanelId.HeroSkillTree. The Bag's Skills tab " +
                             "is the only UNCONDITIONAL door: the ArcaneTower route needs the building placed (a fresh " +
                             "save's town is blank) and the Yarn OpenTalents verb has no shipped caller. Removing it " +
                             "makes the hero talent tree unreachable again.");
            if (openers.Count == 0)
                failures.Add("[case 5] NOTHING in Assets/_Modules opens PanelId.HeroSkillTree. The hero talent tree " +
                             "is fully orphaned.");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// The body of the method whose signature starts with <paramref name="signature"/>, from its
        /// opening brace to the matching close, with whole-line comments stripped. Brace-matched
        /// rather than "text to the next 'private'" so a nested type or a lambda cannot truncate it.
        ///
        /// Comments are stripped BEFORE the match, not after: a single brace inside a prose comment
        /// (and the files this reads are heavily commented) would otherwise unbalance the walk and
        /// return a truncated body, which fails as a mysterious "could not be read" rather than as
        /// the real finding.
        /// </summary>
        private static string MethodBody(string rawSrc, string signature)
        {
            if (string.IsNullOrEmpty(rawSrc)) return null;
            string src = StripLineComments(rawSrc);
            int at = src.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = src.IndexOf(BraceOpen, at);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == BraceOpen) depth++;
                else if (src[i] == BraceClose)
                {
                    depth--;
                    if (depth == 0) return StripLineComments(src.Substring(open, i - open + 1));
                }
            }
            return null;
        }

        /// <summary>
        /// The position of the collection-initializer element containing <paramref name="token"/>,
        /// within the first {...} initializer block of <paramref name="body"/> that holds it.
        /// Elements are split on commas at brace/paren depth zero, so a nested call such as
        /// WithCount("Weapons", InventoryTabKind.Weapons) counts as ONE element. -1 when absent.
        /// </summary>
        private static int LabelListIndexOf(string body, string token)
        {
            if (string.IsNullOrEmpty(body)) return -1;
            int tokenAt = body.IndexOf(token, StringComparison.Ordinal);
            if (tokenAt < 0) return -1;

            // Walk back to the innermost enclosing open brace that starts the initializer.
            int depth = 0, start = -1;
            for (int i = tokenAt; i >= 0; i--)
            {
                if (body[i] == BraceClose) depth++;
                else if (body[i] == BraceOpen)
                {
                    if (depth == 0) { start = i; break; }
                    depth--;
                }
            }
            if (start < 0) return -1;

            int index = 0, d = 0;
            for (int i = start + 1; i < body.Length; i++)
            {
                char c = body[i];
                if (c == BraceOpen || c == '(' || c == '[') d++;
                else if (c == ')' || c == ']') d--;
                else if (c == BraceClose)
                {
                    if (d == 0) return -1;      // ran out of block before reaching the token
                    d--;
                }
                else if (c == ',' && d == 0) index++;
                if (i == tokenAt) return index;
            }
            return -1;
        }

        /// <summary>The int value of a `... Name = N;` constant, or -1 when not found.</summary>
        private static int ConstValue(string src, string name)
        {
            if (string.IsNullOrEmpty(src)) return -1;
            foreach (string line in src.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                int at = t.IndexOf(name, StringComparison.Ordinal);
                if (at < 0 || t.IndexOf("const", StringComparison.Ordinal) < 0) continue;
                int eq = t.IndexOf('=', at);
                if (eq < 0) continue;
                int semi = t.IndexOf(';', eq);
                if (semi < 0) continue;
                int value;
                if (int.TryParse(t.Substring(eq + 1, semi - eq - 1).Trim(), out value)) return value;
            }
            return -1;
        }

        /// <summary>Drops every whole-line // and /// comment so a source oracle matches CODE, not prose.</summary>
        private static string StripLineComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;
            var sb = new StringBuilder(src.Length);
            foreach (string line in src.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Repo-relative read. The repo ROOT is machine-dependent (CLAUDE.md §0), so it is
        /// resolved at runtime from the working directory and never hardcoded.</summary>
        private static string ReadSource(string relativePath, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(),
                                       relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                failures.Add($"source file missing: {relativePath}");
                return null;
            }
            return File.ReadAllText(full);
        }

        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "SKILL TREE DOOR OK - the Bag's active tab builder draws the Skills entry at the RailSkills " +
                         "ordinal, SelectRail routes it to OpenSkillTree, that opens PanelId.HeroSkillTree, and " +
                         "HeroSkillTreePanelMvvm registers it";
                Debug.Log("SKILL_TREE_DOOR_OK\n" + log);
                return true;
            }
            reason = $"SKILL TREE DOOR: {failures.Count} failure(s): " + string.Join(" | ", failures.ToArray());
            Debug.LogError($"SKILL_TREE_DOOR_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures.ToArray()));
            return false;
        }
    }
}
