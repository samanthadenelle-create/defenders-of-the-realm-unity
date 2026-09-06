// =============================================================================
// RaidDeployZeroArmyRegression -- WO-1403 pins (Raid Deploy at zero troops).
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-05 (merged UI review section 2 #2, written to the default
// NO): the deploy screen must NOT offer an assault to a player with no army, and
// section 2 #1 (default YES): the scout report must say what a win PAYS.
//
// The defect this suite exists to keep dead, from the 09-05 07:02 capture
// (Builds/ui-capture/RaidDeploy_2670x1200.png): a full-size LIVE "BEGIN ASSAULT"
// sat under the sentence "No troops trained yet. Visit the Barracks." The loudest
// button said attack, the sentence said go somewhere else, and NEITHER was a door
// to the Barracks. Tapping it was a loss the screen invited.
//
// Shape: source-lint + pure-VM oracle (the DataRegression family -- headless,
// never throws, no scene, no screen build). Five cases:
//   [zero-army-vm]      RaidDeployVM binds the footer's word to Fielded: an army of
//                       0 reads TRAIN TROOPS with ShowAssault false; an army of 3
//                       reads BEGIN ASSAULT with ShowAssault true.
//   [zero-army-footer]  RaidDeployScreen.BuildDeployBar branches on ShowAssault, the
//                       "BEGIN ASSAULT" literal exists ONLY inside the troops>0
//                       branch, the zero branch builds "TRAIN TROOPS", the retired
//                       "Army Ready?" question is gone, and the WO's decision trace
//                       ("deploy footer fielded=<n> primary=<label>") is emitted from
//                       vm.PrimaryCtaLabel rather than a re-derived literal.
//   [zero-army-door]    Both CTAs route through ONE door, OpenTroopsDoor, which
//                       closes the deploy screen and calls
//                       PanelRouter.Open(PanelId.Manage, "Troops") -- the same tab
//                       string DialogueCommandSink.cs:199 uses and
//                       ManageScreenPanel.cs:375-378 honours.
//   [zero-army-command] The refusal also lives on the COMMAND (RaidDeployVM.Deploy),
//                       not only on a button that is no longer drawn, and it is
//                       checked BEFORE SceneRouter.GoRaid.
//   [zero-army-spoils]  The scout report's last line is the WO-1403 spoils estimate,
//                       and it comes from WO-1402's producer -- RaidSelectionVM
//                       .EstimateSpoils / .FormatSpoils -> RaidScoring.EstimateSpoils
//                       -> ProjectLoot -> ComputeLoot -- so the deploy screen and the
//                       selection row quote one camp with ONE string after the prefix.
//                       The pin is inverted on purpose: a DIRECT ComputeLoot /
//                       ProjectLoot call from RaidDeployVM is the regression.
//
// (!) ORDERING DEPENDENCY: this suite and RaidDeployVM.SpoilsLine both name symbols
// the WO-1402 lane introduced (RaidScoring.EstimateSpoils, RaidSelectionVM
// .EstimateSpoils / .FormatSpoils / .SpoilsPrefix). WO-1403 compiles only with those
// present -- 1402 lands first, or in the same commit.
//
// RED-FIRST -- the one-line revert that reds each case (verified by reading, not by
// running; the suite also does not COMPILE against the pre-lane tree because it
// names new VM members, so these reverts are the RED proof, not a build failure):
//   [zero-army-vm]      RaidDeployVM: `public bool ShowAssault => true;`
//   [zero-army-footer]  RaidDeployScreen: delete the `if (showAssault)` guard
//   [zero-army-door]    RaidDeployScreen: `PanelRouter.Open(PanelId.Manage)`
//                       (drop the "Troops" tab argument)
//   [zero-army-command] RaidDeployVM.Deploy: delete the `if (Fielded <= 0)` block
//   [zero-army-spoils]  RaidDeployVM.SpoilsLine: swap RaidSelectionVM.EstimateSpoils
//                       for a direct RaidScoring.ComputeLoot call (or delete
//                       `_scoutReport.Add(spoils)` in BuildScoutReport)
//
// REGISTRATION (DataRegression.RunAll is the sole-committer's lane). One line,
// immediately AFTER the "raid-deploy-ui suite" line (DataRegression.cs:570 at the
// time of writing) so the two deploy-screen suites sit together:
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "raid-deploy-zero-army suite", () => { if (!RaidDeployZeroArmyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-deploy-zero-army] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Editor
{
    public static class RaidDeployZeroArmyRegression
    {
        const string DeployScreenRel = "_Modules/Village/Hero/RaidDeployScreen.cs";
        const string DeployVmRel = "_Modules/Village/Hero/RaidDeployVM.cs";

        /// <summary>Runs all WO-1403 pins. True when green; reason always says why.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                CheckVmBinding(failures, notes);
                CheckFooterBranch(failures, notes);
                CheckTroopsDoor(failures, notes);
                CheckDeployCommandRefusal(failures, notes);
                CheckSpoilsLine(failures, notes);
            }
            catch (Exception ex)
            {
                failures.Add("RAID-DEPLOY-ZERO-ARMY oracle threw: " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "RAID-DEPLOY-ZERO-ARMY OK -- " + string.Join("; ", notes.ToArray());
                return true;
            }
            reason = "RAID-DEPLOY-ZERO-ARMY VIOLATION x" + failures.Count + " -- " +
                     string.Join(" | ", failures.ToArray());
            return false;
        }

        // -- fixtures ------------------------------------------------------------

        // The camp the two fixtures attack. Same authored shape as the WO-839 scout
        // fixture so the two suites describe the same imaginary camp.
        static SceneConfigDef Camp()
        {
            return new SceneConfigDef
            {
                id = "regression_raid_zero_army",
                displayName = "Regression Raid",
                sceneName = "RaidBase_regression",
                wallTier = "ReinforcedSteel",
                entranceCount = 2,
                garrison = new GarrisonDef
                {
                    composition = new List<GarrisonUnitDef>
                    {
                        new GarrisonUnitDef { enemyId = "orc-berserker", count = 4 },
                        new GarrisonUnitDef { enemyId = "shaman", count = 2 },
                    },
                    boss = "necromancer",
                },
                rewardMultiplier = 2.2f,
                shardDropChance = 0.2f,
            };
        }

        // Every camp whose spoils line has to FIT the four-line SCOUT REPORT well: the
        // regression fixture plus the four shipped camps at their authored rewardMultiplier
        // (scene-configs.json, 2026-09-05). Only id + multiplier reach the estimate, so the
        // rest of the def is deliberately absent -- this is a LENGTH budget, not a catalog pin
        // (RaidEscalationRegression A/B owns the catalog).
        static IEnumerable<SceneConfigDef> AllCamps()
        {
            yield return Camp();
            yield return new SceneConfigDef { id = "raider_camp_small",  rewardMultiplier = 1.0f };
            yield return new SceneConfigDef { id = "fortified_garrison", rewardMultiplier = 1.5f };
            yield return new SceneConfigDef { id = "mage_enclave",       rewardMultiplier = 2.2f };
            yield return new SceneConfigDef { id = "iron_bastion",       rewardMultiplier = 2.2f };
        }

        // An army object that EXISTS with an EMPTY roster -- deliberately not `null`.
        // A null army is a different path ("Army: -", already pinned by
        // RaidDeployUiRegression's null-def case); the WO's player HAS an army store
        // and has trained nothing into it.
        static ArmyStorage EmptyArmy() => new ArmyStorage { Owned = new List<PlayerTroop>() };

        static ArmyStorage ArmyOf(int n)
        {
            var a = new ArmyStorage { Owned = new List<PlayerTroop>() };
            for (int i = 1; i <= n; i++)
                a.Owned.Add(new PlayerTroop { Id = "troop-" + i, TroopDefId = "troop-footman" });
            return a;
        }

        // -- 1. [zero-army-vm] pure VM binding -----------------------------------
        static void CheckVmBinding(List<string> failures, List<string> notes)
        {
            const string Tag = "[zero-army-vm]";
            int before = failures.Count;
            var def = Camp();

            var zero = new RaidDeployVM(def, EmptyArmy(), null, null, null);
            try
            {
                if (zero.Fielded != 0)
                    failures.Add(Tag + " an empty roster reports Fielded=" + zero.Fielded + ", expected 0");
                if (zero.ShowAssault)
                    failures.Add(Tag + " ShowAssault is TRUE at zero troops -- the screen would draw " +
                                 "BEGIN ASSAULT under \"No troops trained yet\" again (WO-1403 ruling: " +
                                 "no assault with zero troops)");
                if (zero.PrimaryCtaLabel != RaidDeployVM.PrimaryTrainLabel)
                    failures.Add(Tag + " the zero-army primary reads '" + zero.PrimaryCtaLabel +
                                 "', expected '" + RaidDeployVM.PrimaryTrainLabel + "'");
            }
            finally { zero.Dispose(); }

            var three = new RaidDeployVM(def, ArmyOf(3), null, null, null);
            try
            {
                if (three.Fielded != 3)
                    failures.Add(Tag + " a 3-troop roster reports Fielded=" + three.Fielded + ", expected 3");
                if (!three.ShowAssault)
                    failures.Add(Tag + " ShowAssault is FALSE with 3 troops -- the player who CAN raid is " +
                                 "no longer offered the assault");
                if (three.PrimaryCtaLabel != RaidDeployVM.PrimaryAssaultLabel)
                    failures.Add(Tag + " the armed primary reads '" + three.PrimaryCtaLabel +
                                 "', expected '" + RaidDeployVM.PrimaryAssaultLabel + "'");
            }
            finally { three.Dispose(); }

            // Fielded is the SAME number the WO-1389 compare line speaks, by construction.
            if (RaidDeployVM.PrimaryTrainLabel != "TRAIN TROOPS")
                failures.Add(Tag + " PrimaryTrainLabel is '" + RaidDeployVM.PrimaryTrainLabel +
                             "' -- the WO-1403 word on the zero-army primary is \"TRAIN TROOPS\"");
            if (RaidDeployVM.PrimaryAssaultLabel != "BEGIN ASSAULT")
                failures.Add(Tag + " PrimaryAssaultLabel is '" + RaidDeployVM.PrimaryAssaultLabel +
                             "' -- WO-932 named the armed primary \"BEGIN ASSAULT\"");

            if (failures.Count == before)
                notes.Add("VM binds the primary to Fielded (0 -> TRAIN TROOPS / no assault; 3 -> BEGIN ASSAULT)");
        }

        // -- 2. [zero-army-footer] source-lint of BuildDeployBar -----------------
        static void CheckFooterBranch(List<string> failures, List<string> notes)
        {
            const string Tag = "[zero-army-footer]";
            int before = failures.Count;
            if (!TryReadAsset(DeployScreenRel, Tag, failures, out string text)) return;

            int start = text.IndexOf("private void BuildDeployBar(", StringComparison.Ordinal);
            if (start < 0)
            {
                failures.Add(Tag + " RaidDeployScreen.BuildDeployBar not found -- the CTA builder moved");
                return;
            }
            int end = text.IndexOf("private static void SeatFooterCtaAtCanonicalHeight(", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(start + 8000, text.Length);
            string bar = text.Substring(start, end - start);

            // (a) The branch itself. Deleting the guard is the one-line revert that reds this.
            int guard = bar.IndexOf("if (showAssault)", StringComparison.Ordinal);
            if (guard < 0)
            {
                failures.Add(Tag + " BuildDeployBar has no `if (showAssault)` branch -- the footer is no " +
                             "longer bound to the army, so a zero-troop player is offered the assault again " +
                             "(WO-1403). Everything below this check is unjudgeable; fix the branch first.");
                return;
            }
            if (bar.IndexOf("_vm.ShowAssault", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the branch does not read _vm.ShowAssault -- the View must ask the VM, " +
                             "never re-derive readiness on the presentation layer");

            int branchReturn = bar.IndexOf("return;", guard, StringComparison.Ordinal);
            if (branchReturn < 0)
            {
                failures.Add(Tag + " the showAssault branch never returns -- the zero-army primary would be " +
                             "drawn on top of BEGIN ASSAULT");
                return;
            }

            // (b) BEGIN ASSAULT exists exactly once, and only inside the troops>0 branch.
            int assault = bar.IndexOf("\"BEGIN ASSAULT\"", StringComparison.Ordinal);
            if (assault < 0)
                failures.Add(Tag + " BuildDeployBar no longer builds a \"BEGIN ASSAULT\" button at all -- " +
                             "the armed player lost the raid CTA");
            else
            {
                if (assault < guard || assault > branchReturn)
                    failures.Add(Tag + " the \"BEGIN ASSAULT\" literal at offset " + assault + " is OUTSIDE " +
                                 "the showAssault branch (" + guard + ".." + branchReturn + ") -- it would be " +
                                 "drawn at zero troops, which is exactly the 09-05 defect");
                int second = bar.IndexOf("\"BEGIN ASSAULT\"", assault + 1, StringComparison.Ordinal);
                if (second >= 0)
                    failures.Add(Tag + " \"BEGIN ASSAULT\" is built TWICE in BuildDeployBar -- one of them is " +
                                 "outside the army branch");
            }

            // (c) The zero branch draws the door, after the branch returns.
            int train = bar.IndexOf("\"TRAIN TROOPS\"", StringComparison.Ordinal);
            if (train < 0)
                failures.Add(Tag + " BuildDeployBar builds no \"TRAIN TROOPS\" button -- the zero-army player " +
                             "is left with the sentence and no door (WO-1403's whole point)");
            else if (train < branchReturn)
                failures.Add(Tag + " the \"TRAIN TROOPS\" literal sits INSIDE the showAssault branch -- the " +
                             "zero-army path draws nothing");
            if (bar.IndexOf("OnTrainTroops", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the TRAIN TROOPS button does not wire OnTrainTroops -- a dead tap");
            if (bar.IndexOf("OnEditArmy", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the secondary no longer wires OnEditArmy");

            // (d) The retired question, and the retired stub behind it.
            if (bar.IndexOf("Army Ready?", StringComparison.Ordinal) >= 0)
                failures.Add(Tag + " \"Army Ready?\" is back on the footer -- WO-1403 replaced the question " +
                             "with the verb EDIT ARMY (a button label is an action, not a quiz)");
            if (bar.IndexOf("OnAutoRecommend", StringComparison.Ordinal) >= 0)
                failures.Add(Tag + " the retired OnAutoRecommend toast handler is wired to the footer again");

            // (e) The WO's decision trace, taken from the VM's word.
            if (bar.IndexOf("\"deploy footer fielded=\"", StringComparison.Ordinal) < 0 &&
                bar.IndexOf("deploy footer fielded=", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " BuildDeployBar emits no `deploy footer fielded=<n> primary=<label>` " +
                             "FlowTrace -- the WO-1403 decision line. Without it a capture cannot say which " +
                             "footer the player was shown (CLAUDE.md section 12: instrument the decision)");
            if (bar.IndexOf("PrimaryCtaLabel", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the footer trace does not read _vm.PrimaryCtaLabel -- a re-derived " +
                             "literal can drift from the button it claims to describe");
            if (bar.IndexOf("FlowTrace.Step", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " BuildDeployBar carries no FlowTrace at all");

            if (failures.Count == before)
                notes.Add("footer branches on _vm.ShowAssault (BEGIN ASSAULT only inside it, TRAIN TROOPS " +
                          "outside it, no \"Army Ready?\", decision trace from PrimaryCtaLabel)");
        }

        // -- 3. [zero-army-door] source-lint of the ONE Barracks door ------------
        static void CheckTroopsDoor(List<string> failures, List<string> notes)
        {
            const string Tag = "[zero-army-door]";
            int before = failures.Count;
            if (!TryReadAsset(DeployScreenRel, Tag, failures, out string text)) return;

            int start = text.IndexOf("private void OpenTroopsDoor(", StringComparison.Ordinal);
            if (start < 0)
            {
                failures.Add(Tag + " RaidDeployScreen.OpenTroopsDoor not found -- the WO-1403 door to the " +
                             "Barracks is gone, or was split back into two routes");
                return;
            }
            int end = text.IndexOf("private void OnDeploy(", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(start + 3000, text.Length);
            string door = text.Substring(start, end - start);

            // The tab string is the contract: ManageScreenPanel.cs:375-378 matches on the
            // prefix "Troops", and DialogueCommandSink.cs:199 already opens that exact door.
            if (door.IndexOf("PanelRouter.Open(PanelId.Manage, \"Troops\")", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the door does not call PanelRouter.Open(PanelId.Manage, \"Troops\") -- " +
                             "TRAIN TROOPS must land ON the Troops tab, not on whatever tab Manage " +
                             "happened to remember");
            if (door.IndexOf("Close();", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the door does not Close() the deploy screen before opening Manage -- " +
                             "the close-to-nothing is what ARMS the WO-1400 return door (PanelManager.cs:374 " +
                             "then KEEPS it for Manage), so the player would lose the way back to the deck");
            if (door.IndexOf("FlowTrace", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the door emits no FlowTrace -- an F8 capture could not prove the tap " +
                             "routed anywhere (WO-1403 acceptance asks for the trace line)");
            if (door.IndexOf("ShowToast", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " a refused PanelRouter.Open leaves the player with a silent dead tap -- " +
                             "the door must say a word when it cannot open");

            // Both CTAs go through the ONE door (never two copies of the route).
            if (text.IndexOf("OnTrainTroops() => OpenTroopsDoor(", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " OnTrainTroops does not route through OpenTroopsDoor");
            if (text.IndexOf("OnEditArmy() => OpenTroopsDoor(", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " OnEditArmy does not route through OpenTroopsDoor");

            int routes = 0, at = 0;
            while ((at = text.IndexOf("PanelRouter.Open(PanelId.Manage", at, StringComparison.Ordinal)) >= 0)
            { routes++; at++; }
            if (routes != 1)
                failures.Add(Tag + " RaidDeployScreen opens Manage from " + routes + " places -- WO-1403 " +
                             "specifies ONE door (OpenTroopsDoor); a second copy is where the tab string drifts");

            if (failures.Count == before)
                notes.Add("one door: both CTAs -> OpenTroopsDoor -> Close() + PanelRouter.Open(Manage, \"Troops\"), traced");
        }

        // -- 4. [zero-army-command] the refusal lives on the command too ---------
        static void CheckDeployCommandRefusal(List<string> failures, List<string> notes)
        {
            const string Tag = "[zero-army-command]";
            int before = failures.Count;
            if (!TryReadAsset(DeployVmRel, Tag, failures, out string text)) return;

            int start = text.IndexOf("public void Deploy()", StringComparison.Ordinal);
            if (start < 0) { failures.Add(Tag + " RaidDeployVM.Deploy() not found"); return; }
            int end = text.IndexOf("public static RaidDeployVM CreateDefault(", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(start + 3000, text.Length);
            string body = text.Substring(start, end - start);

            int refusal = body.IndexOf("if (Fielded <= 0)", StringComparison.Ordinal);
            int go = body.IndexOf("SceneRouter.GoRaid(", StringComparison.Ordinal);
            if (refusal < 0)
                failures.Add(Tag + " RaidDeployVM.Deploy() does not refuse at Fielded <= 0 -- the WO-1403 " +
                             "ruling would live only on a button that is no longer drawn, so any stale " +
                             "handle or re-entrant tap still marches a zero army (and the raid-entry seam " +
                             "SPENDS a Heartfire charge on it: RaidDeployController.cs:167)");
            else if (go >= 0 && refusal > go)
                failures.Add(Tag + " the Fielded <= 0 refusal is AFTER SceneRouter.GoRaid -- the scene has " +
                             "already loaded by then");
            if (body.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the zero-army refusal is silent -- a refused command with no trace is " +
                             "the failure CLAUDE.md section 12 forbids");

            // The View's belt-and-braces copy, with a word for the player.
            if (!TryReadAsset(DeployScreenRel, Tag, failures, out string screen)) return;
            if (screen.IndexOf("No troops trained yet. Visit the Barracks.", StringComparison.Ordinal) < 0)
                failures.Add(Tag + " the empty-army sentence is gone from RaidDeployScreen -- it is the " +
                             "explanation the TRAIN TROOPS door answers");

            if (failures.Count == before)
                notes.Add("Deploy() refuses at Fielded<=0 before GoRaid, traced");
        }

        // -- 5. [zero-army-spoils] the scout report says what a win pays ---------
        static void CheckSpoilsLine(List<string> failures, List<string> notes)
        {
            const string Tag = "[zero-army-spoils]";
            int before = failures.Count;
            var def = Camp();

            // THE PIN THAT MATTERS: the deploy line and the selection row are the SAME
            // string after the prefix. Not "both look like an estimate" -- byte-identical,
            // because they come from one estimator and one formatter (WO-1402's). The camp
            // id here is not in RaidLootTunables' per-camp GOLD table; CoinsBaseFor answers
            // the Camp I base and traces Once rather than throwing, so the line still pays.
            string rowLine = RaidSelectionVM.FormatSpoils(RaidSelectionVM.EstimateSpoils(def));
            string line = RaidDeployVM.SpoilsLine(def);
            if (string.IsNullOrEmpty(rowLine))
            {
                failures.Add(Tag + " WO-1402's own producer (RaidSelectionVM.FormatSpoils o EstimateSpoils) " +
                             "pays nothing for a fully-authored camp -- check the RemoteTunables loot rail " +
                             "('raid.lootWoodBase' / 'raid.lootIronBase'); a zeroed knob silently removes the " +
                             "spoils line the owner ruled YES on, on BOTH screens");
                return;
            }
            if (string.IsNullOrEmpty(line)) { failures.Add(Tag + " RaidDeployVM.SpoilsLine returned nothing " +
                             "while the selection row reads '" + rowLine + "'"); return; }
            if (!line.StartsWith(RaidDeployVM.SpoilsPrefix, StringComparison.Ordinal))
                failures.Add(Tag + " the deploy line does not open with '" + RaidDeployVM.SpoilsPrefix +
                             "' -- got '" + line + "'");
            // The WORDS, spelled out here on purpose so a silent copy change reds rather than
            // slipping through the constant. It read "Spoils if you win: ~" until 2026-09-05,
            // when the fresh capture showed that prefix pushing the gold amount off the right
            // edge of the four-line SCOUT REPORT well (RaidDeploy_1920x1080.png: "...~1100
            // iron," and nothing more). The line must stay SHORT, so this pin is also a length
            // pin -- see the character budget below.
            // RED RECIPE: restore RaidDeployVM.SpoilsPrefix to "Spoils if you win: " and this
            // case fails on both this assertion and the 46-character budget.
            if (!line.StartsWith("Spoils: ~", StringComparison.Ordinal))
                failures.Add(Tag + " the deploy line is not the WO-1403 words + a range tilde -- got '" + line + "'");

            // THE LENGTH PIN. The well seats four lines and MUST NOT wrap into a fifth, so the
            // longest spoils line has a character budget: measured on the 2026-09-05 capture,
            // the 1920x1080 well fitted 42 characters of this line before the cut at x 0.08-0.92
            // (0.84 of the well); the report block was then widened to 0.05-0.96 (0.91), which
            // buys 42 * 0.91/0.84 = 45.5 -> budget 45. Anything longer clips silently -- no
            // exception, no trace, just a missing number on the screen where the player decides
            // whether the raid is worth it.
            // (!) A CHARACTER COUNT IS A PROXY FOR GLYPH WIDTH, not the thing itself: digits and
            // "~" are narrower than the lower-case letters this was measured on, so 45 is
            // deliberately conservative. The longest live line is 42. A fresh capture of
            // RaidDeploy_1920x1080.png ending in "gold" is the real acceptance; this pin exists
            // to stop the line GROWING again between captures.
            const int WellCharBudget = 45;
            foreach (var camp in AllCamps())
            {
                string l = RaidDeployVM.SpoilsLine(camp);
                if (!string.IsNullOrEmpty(l) && l.Length > WellCharBudget)
                    failures.Add(Tag + " the spoils line for '" + camp.id + "' is " + l.Length +
                                 " characters ('" + l + "'), over the " + WellCharBudget + "-character SCOUT " +
                                 "REPORT budget measured at 1920x1080. It will CLIP mid-number -- shorten the " +
                                 "prefix or the grammar in RaidSelectionVM.FormatSpoils (both screens share it), " +
                                 "never let it wrap into a fifth line the well has no room for");
            }
            string deployBody = line.StartsWith(RaidDeployVM.SpoilsPrefix, StringComparison.Ordinal)
                ? line.Substring(RaidDeployVM.SpoilsPrefix.Length) : line;
            string rowBody = rowLine.StartsWith(RaidSelectionVM.SpoilsPrefix, StringComparison.Ordinal)
                ? rowLine.Substring(RaidSelectionVM.SpoilsPrefix.Length) : rowLine;
            if (deployBody != rowBody)
                failures.Add(Tag + " the two screens quote the SAME camp differently: the selection row says '" +
                             rowBody + "', the deploy screen says '" + deployBody + "'. WO-1403 specifies the " +
                             "line comes from WO-1402's producer; a second estimator (a different star rung, a " +
                             "different rounding, a dropped currency) is the drift this pin exists to catch");
            if (RaidDeployVM.SpoilsLine(null) != null)
                failures.Add(Tag + " SpoilsLine(null) must be null (no def, no promise)");

            var vm = new RaidDeployVM(def, EmptyArmy(), null, null, null);
            try
            {
                var report = vm.ScoutReport;
                if (report == null || report.Count == 0)
                {
                    failures.Add(Tag + " ScoutReport is empty for a fully-authored camp");
                }
                else
                {
                    string last = report[report.Count - 1];
                    if (last != line)
                        failures.Add(Tag + " the scout report's LAST line is '" + last + "', expected the " +
                                     "spoils estimate '" + line + "' -- WO-1403 line 4 (owner ruling section " +
                                     "2 #1, default YES: show what a win pays, on the screen where the player " +
                                     "decides to raid)");
                    if (report.Count < 4)
                        failures.Add(Tag + " the scout report has only " + report.Count + " lines; a camp with " +
                                     "walls + garrison + boss + spoils must read 4");
                }
            }
            finally { vm.Dispose(); }

            // The INVERTED source pin. Calling the scorer's low-level maths from the deploy
            // VM is now the regression, not the contract: the deploy screen delegates, it
            // does not compute. (An earlier draft of WO-1403 did call ComputeLoot here at a
            // 2-star rung with its own rounding, which is precisely the mismatch above.)
            if (TryReadAsset(DeployVmRel, Tag, failures, out string vmText))
            {
                if (vmText.IndexOf("RaidSelectionVM.EstimateSpoils(", StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " RaidDeployVM does not call WO-1402's estimator " +
                                 "(RaidSelectionVM.EstimateSpoils) -- the WO specifies the line comes from " +
                                 "the same producer as the selection row");
                if (vmText.IndexOf("RaidSelectionVM.FormatSpoils(", StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " RaidDeployVM does not use WO-1402's formatter " +
                                 "(RaidSelectionVM.FormatSpoils) -- a second grammar for the same basket");
                if (vmText.IndexOf("RaidScoring.ComputeLoot(", StringComparison.Ordinal) >= 0)
                    failures.Add(Tag + " RaidDeployVM calls RaidScoring.ComputeLoot DIRECTLY -- that is a " +
                                 "second loot formula on the deploy screen and it will drift from the row " +
                                 "and from the settle credit. Go through RaidSelectionVM.EstimateSpoils");
                if (vmText.IndexOf("RaidScoring.ProjectLoot(", StringComparison.Ordinal) >= 0)
                    failures.Add(Tag + " RaidDeployVM calls RaidScoring.ProjectLoot DIRECTLY -- same reason " +
                                 "as ComputeLoot above");
            }

            if (failures.Count == before)
                notes.Add("scout report line 4 = '" + line + "' (" + line.Length + " chars, budget " +
                          WellCharBudget + "; WO-1402's producer; identical to the selection row, prefix included " +
                          "since 2026-09-05 -- the longer 'if you win' wording clipped the gold amount)");
        }

        // -- shared --------------------------------------------------------------

        /// <summary>
        /// Reads a source file with its line COMMENTS STRIPPED. This is not tidiness, it is
        /// correctness: every literal this suite hunts for -- "Army Ready?", "BEGIN ASSAULT",
        /// OnAutoRecommend, RaidScoring.ComputeLoot( -- is ALSO named in the comment that
        /// explains why it was retired or forbidden. A raw scan therefore reds on the file
        /// that is RIGHT, and the only way to green it would be to delete the explanation.
        /// Comments are documentation; this oracle judges CODE.
        ///
        /// Line comments only, and deliberately naive: verified 2026-09-05 that neither
        /// RaidDeployScreen.cs nor RaidDeployVM.cs contains a "//" inside a string literal
        /// (no URLs, no paths), so there is nothing here for it to eat by mistake. If one is
        /// ever added, this helper -- not the pins -- is what needs teaching.
        /// </summary>
        static string StripLineComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;
            var sb = new System.Text.StringBuilder(src.Length);
            foreach (var line in src.Split('\n'))
            {
                int slash = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(slash >= 0 ? line.Substring(0, slash) : line).Append('\n');
            }
            return sb.ToString();
        }

        static bool TryReadAsset(string relPath, string tag, List<string> failures, out string text)
        {
            text = null;
            string path = Path.Combine(Application.dataPath, relPath);
            if (!File.Exists(path)) { failures.Add(tag + " " + relPath + " not found at " + path); return false; }
            try { text = StripLineComments(File.ReadAllText(path)); }
            catch (Exception ex) { failures.Add(tag + " " + relPath + " unreadable (" + ex.Message + ")"); return false; }
            return true;
        }
    }
}
