// =============================================================================
// ManageTroopsTrainDoorRegression — headless oracle for PROD-013: the Manage
// screen's TROOPS tab is the ONE door to troop training, and it actually opens.
// Marker: MANAGE_TRAIN_DOOR_OK / MANAGE_TRAIN_DOOR_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Wired into DataRegression.RunAll.
// Style/contract mirrors the other Run(out reason) oracles (ArmyMusterRegression,
// ObsidianQueueRegression, UpgradeQueueFullSurfaceRegression).
//
// WHY THIS SUITE EXISTS — the defect it is shaped around:
//   PROD-002 (commit 233613615, 2026-08-18) closed the barracks talk-door on the
//   stated premise that "Manage owns training". It did not: ManageScreenVM
//   .BuildTroopsBrowse emitted UPGRADE rows only, and the door it replaced was the
//   last player-reachable entrance. The whole training stack — ChannelId.Train,
//   JobKind.TrainTroop, BarracksService.EnqueueTraining, TroopTrainingPanel,
//   ArmyMusterService — was built, regression-covered and UNREACHABLE. The owner
//   found it by playing: "under manage i see option to upgrade the troops, but i
//   dont se a way to train troops".
//
//   Every existing suite passed the whole time, because every one of them tested a
//   LAYER (the queue engine, the muster planner, the roster) and none of them tested
//   the DOOR. This one drives the real ManageScreenVM against real live services and
//   asserts what a player can actually tap. Case 1 is the case that would have caught
//   it; it FAILS against pre-PROD-013 code and passes after.
//
// Proves, with REAL types, real catalogs and real services (no play mode):
//   1. the Troops tab emits at least one TRAIN row for a trainable troop;
//   2. TRAIN and UPGRADE rows are BOTH present and labelled distinguishably;
//   3. an ARMIES / muster entry exists (the v38 loadout bank has a door too);
//   4. activating a Train row produces a real JobKind.TrainTroop job on ChannelId.Train;
//   5. the door stays SINGLE — the source still routes through BarracksService and
//      the barracks talk-door stays closed (canon CLAUDE.md §7: one Queues entry).
//   7. (WO-1389, 2026-09-05) the UPGRADE face has a DESTINATION: the Footman choice's
//      NextUnlockText reads "L3 unlocks Sweeping Cut" (BarracksProgression.NextAbilityLine
//      over troop-upgrades.json) and the panel composes it into the upgrade sub-line.
//      Measured RED FIRST: before WO-1389 TroopChoiceVM had no NextUnlockText and the
//      button said "UPGRADE TO L2" with a time under it and nothing about what L3 buys.
//      Mutation that reds it: `choice.NextUnlockText = "";` in ManageScreenVM.FillUpgradeFacts.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.UI;

namespace DeNelle.Editor
{
    public static class ManageTroopsTrainDoorRegression
    {
        private const string VmPath = "Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs";
        private const string InteractablePath = "Assets/_Modules/Village/Buildings/BuildingInteractable.cs";
        private const string PanelPath = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== ManageTroopsTrainDoorRegression: Manage -> Troops is the ONE training door (PROD-013) ===");

            try
            {
                RunLiveDoorChecks(failures, log);
                CheckSingleDoorSource(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add($"ManageTroopsTrainDoorRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, log, out reason);
        }

        // ── 1-4. the LIVE door: drive the real VM against real services ────────
        private static void RunLiveDoorChecks(List<string> failures, StringBuilder log)
        {
            var priorInstance = GameStateService.Instance;
            // The live spend path calls GameStateService.Save(), which writes the editor
            // PlayerPrefs save slot. Back it up and restore it so a regression run can never
            // eat a developer's editor save.
            string priorSave = PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey, null);

            GameObject gssGo = null, svcGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (manage-train-door oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    // NOT A SKIP (the UpgradeQueueFullSurfaceRegression ruling): a suite that
                    // green-passes on an unreachable seam asserts nothing, most eagerly on the
                    // day the seam breaks. The seam being unreflectable is itself the defect.
                    failures.Add("[manage-train-door] GameStateService state seam is not reflectable, so the LIVE " +
                                 "checks (train row present, train+upgrade distinguishable, muster entry, real " +
                                 "TrainTroop enqueue) could not run. This is a FAIL, not a skip.");
                    return;
                }

                svcGo = new GameObject("BuildTimerService (manage-train-door oracle)");
                var svc = svcGo.AddComponent<BuildTimerService>();
                // Awake does not run on AddComponent outside play mode, so the singleton is
                // installed explicitly. If this seam moves the suite FAILS rather than skipping.
                if (!InstallQueueInstance(svc))
                {
                    failures.Add("[manage-train-door] BuildTimerService.Instance backing field is not reflectable — " +
                                 "the oracle cannot install the queue singleton the Troops tab reads. FAIL, not a skip.");
                    return;
                }

                // A founded town with a working barracks and a full purse: the state the owner
                // was in when she reported the defect. No balance is asserted here — the point
                // is only that the DOOR exists, so every gate is deliberately wide open.
                throwaway.Onboarded = true;
                throwaway.BarracksLevel = 3;
                // The VM derives visible categories from THIS town's placed layout. BarracksLevel
                // is account progression, not proof that a barracks stands in the current town.
                // Keep the fixture honest: this is the "founded town with a working barracks"
                // described above, not an impossible empty-town/account-level hybrid.
                throwaway.BaseLayout = new List<PlacedStructureData>
                {
                    new PlacedStructureData("barracks", 2, 2, 0, 1),
                };
                throwaway.Wood = 100000;
                throwaway.Iron = 100000;
                var bal = throwaway.Resources;
                bal.Food = 100000;
                bal.Crystals = 100000;
                // 2026-09-04 WO-1387: Coins is NOT load-bearing for the door any more - training
                // charges nothing (owner: "training free ... just time"). Left rich only because this
                // fixture is "every gate wide open"; the zero-gold train is pinned by
                // TrainingCostsTimeOnlyRegression, not here.
                bal.Coins = 100000;
                throwaway.Resources = bal;
                throwaway.ObsidianQueue = ObsidianQueueState.Empty();

                var vm = new ManageScreenVM();
                vm.SelectTab(ManageTab.Troops);
                vm.Rebuild();

                var rows = new List<BrowseRowVM>(vm.BrowseRows);
                log.AppendLine($"  Troops tab produced {rows.Count} browse row(s):");
                for (int i = 0; i < rows.Count; i++)
                    log.AppendLine($"    [{rows[i].ActionText}] \"{rows[i].Label}\"  ({rows[i].CostText}) - {rows[i].StateText}");

                // ── CASE 1: at least one TRAIN row. THE case. ──────────────────
                var trainRows = rows.FindAll(r => r != null &&
                                                  string.Equals(r.ActionText, "Train", StringComparison.Ordinal));
                if (trainRows.Count == 0)
                {
                    failures.Add("[case 1] the Manage > Troops tab emitted NO row with ActionText \"Train\". This is " +
                                 "THE PROD-013 defect: the barracks talk-door is closed (BuildingInteractable._noTalkDoor) " +
                                 "so this tab is the only entrance to training, and a player cannot train at all. " +
                                 "See ManageScreenVM.BuildTroopsBrowse.");
                }
                else
                {
                    log.AppendLine($"  case 1 OK - {trainRows.Count} Train row(s)");
                }

                // ── CASE 2: UPGRADE rows survive, and the two are distinguishable ──
                var upgradeRows = rows.FindAll(r => r != null &&
                                                    string.Equals(r.ActionText, "Upgrade", StringComparison.Ordinal));
                if (upgradeRows.Count == 0)
                    failures.Add("[case 2] no row with ActionText \"Upgrade\" — adding Train must be ADDITIVE. " +
                                 "Train and Upgrade are different actions on the same troop and both belong on this tab.");

                foreach (var r in trainRows)
                    if (!r.Label.StartsWith("Train ", StringComparison.Ordinal))
                        failures.Add($"[case 2] a Train row is labelled \"{r.Label}\" — it does not lead with the verb, " +
                                     "so a player cannot tell it apart from the Upgrade row for the same troop.");
                foreach (var r in upgradeRows)
                    if (!r.Label.StartsWith("Upgrade ", StringComparison.Ordinal))
                        failures.Add($"[case 2] an Upgrade row is labelled \"{r.Label}\" — it does not lead with the verb, " +
                                     "so a player cannot tell it apart from the Train row for the same troop.");

                // No two rows may share a label: identical text on two different actions is the
                // mistake this case exists to prevent, whatever the verbs happen to be.
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in rows)
                    if (r != null && !string.IsNullOrEmpty(r.Label) && !seen.Add(r.Label))
                        failures.Add($"[case 2] two browse rows carry the IDENTICAL label \"{r.Label}\" — " +
                                     "two different actions reading the same is unactionable for the player.");

                if (upgradeRows.Count > 0)
                    log.AppendLine($"  case 2 OK - {upgradeRows.Count} Upgrade row(s), all verb-led and distinct");

                // ── CASE 3: the ARMIES / muster entry (WO-897, save schema v38) ──
                var musterRow = rows.Find(r => r != null && r.Label != null &&
                                               r.Label.IndexOf("Armies", StringComparison.OrdinalIgnoreCase) >= 0);
                if (musterRow == null)
                    failures.Add("[case 3] no ARMIES/muster entry on the Troops tab. The v38 army loadout bank " +
                                 "(3 named composition slots) ships and its only other door was the closed barracks " +
                                 "Yarn verb <<ShowMusterUI>> — without this row it is unreachable too.");
                else if (musterRow.Activate == null)
                    failures.Add("[case 3] the Armies entry has a null Activate — a row that does nothing is not a door.");
                else
                    log.AppendLine($"  case 3 OK - muster entry \"{musterRow.Label}\" [{musterRow.ActionText}]");

                // -- CASE 7 (WO-1389): the UPGRADE face names what the next level BUYS --
                // Read from the SAME Rebuild() as cases 1-3, before case 4 mutates the queue.
                // The fixture's troop level is the state default (L1), so the Footman's next
                // authored ability is the L3 one; the oracle asks the ONE composer for the exact
                // sentence rather than retyping "Sweeping Cut" (that string is troop-upgrades.json's).
                const string taughtTroop = "troop-footman";
                var footman = vm.TroopChoices.Find(c => c != null && string.Equals(c.Id, taughtTroop, StringComparison.Ordinal));
                if (footman == null)
                {
                    failures.Add($"[case 7] the Troops tab emitted no TroopChoiceVM for '{taughtTroop}' - the StarterArmyGrant " +
                                 "unit the post-raid beat teaches on is not on the card rail at all.");
                }
                else
                {
                    string expected = BarracksProgression.NextAbilityLine(taughtTroop, footman.Level) ?? "";
                    if (string.IsNullOrEmpty(expected))
                        failures.Add($"[case 7] BarracksProgression.NextAbilityLine('{taughtTroop}', L{footman.Level}) returned nothing - " +
                                     "troop-upgrades.json authors abilities at L3/L5/L7 for the Footman, so the composer lost them.");
                    else if (!expected.StartsWith("L", StringComparison.Ordinal) || expected.IndexOf(" unlocks ", StringComparison.Ordinal) < 0)
                        failures.Add($"[case 7] the next-unlock sentence reads \"{expected}\" - the shape is \"L<n> unlocks <Ability>\" " +
                                     "so the button has a destination the player can read, not a whole flavour line.");
                    if (!string.Equals(footman.NextUnlockText, expected, StringComparison.Ordinal))
                        failures.Add($"[case 7] Footman NextUnlockText is \"{footman.NextUnlockText}\" but the composer says \"{expected}\" - " +
                                     "the UPGRADE TO L<n> face has lost its destination (WO-1389 pressure point 3: " +
                                     "\"the button has a destination, not just a number\").");
                    else
                        log.AppendLine($"  case 7 OK - Footman L{footman.Level} next-unlock line \"{footman.NextUnlockText}\"");
                }

                // ── CASE 4: activating a Train row makes a REAL job on the REAL line ──
                if (trainRows.Count > 0)
                {
                    int before = svc.QueueDepth(ChannelId.Train);
                    trainRows[0].Activate?.Invoke();

                    var jobs = new List<BuildJobData>();
                    jobs.AddRange(svc.ActiveJobsOf(ChannelId.Train));
                    jobs.AddRange(svc.PendingJobsOf(ChannelId.Train));

                    var trainJob = jobs.Find(j => !string.IsNullOrEmpty(j.StructureId) &&
                                                  j.StructureId.StartsWith(BarracksService.TrainPrefix, StringComparison.Ordinal));
                    if (trainJob.StructureId == null)
                    {
                        failures.Add($"[case 4] tapping \"{trainRows[0].Label}\" did NOT put a job on the Train line " +
                                     $"(depth {before} -> {svc.QueueDepth(ChannelId.Train)}). The CTA must route through " +
                                     "BarracksService.EnqueueTraining, which mints a job id prefixed " +
                                     $"\"{BarracksService.TrainPrefix}\". Notice was: \"{vm.Notice}\"");
                    }
                    else
                    {
                        if (trainJob.Kind != (int)JobKind.TrainTroop)
                            failures.Add($"[case 4] the enqueued job's Kind is {trainJob.Kind}, expected " +
                                         $"{(int)JobKind.TrainTroop} (JobKind.TrainTroop) — a training job on the wrong " +
                                         "kind will be applied by the wrong IJobEffect at completion.");
                        if (trainJob.Channel != (int)ChannelId.Train)
                            failures.Add($"[case 4] the enqueued job's Channel is {trainJob.Channel}, expected " +
                                         $"{(int)ChannelId.Train} (ChannelId.Train) — training must ride the Train line, " +
                                         "never the shared Builder line.");
                        log.AppendLine($"  case 4 OK - \"{trainRows[0].Label}\" enqueued jobId={trainJob.StructureId} " +
                                       $"kind={trainJob.Kind} channel={trainJob.Channel} (depth {before} -> " +
                                       $"{svc.QueueDepth(ChannelId.Train)})");
                    }
                }
            }
            finally
            {
                if (svcGo != null) UnityEngine.Object.DestroyImmediate(svcGo);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorInstance);
                if (priorSave != null) PlayerPrefs.SetString(SaveSchema.PlayerPrefsKey, priorSave);
                else PlayerPrefs.DeleteKey(SaveSchema.PlayerPrefsKey);
            }
        }

        // ── 5. the door stays SINGLE (canon CLAUDE.md §7: one Queues entry) ────
        private static void CheckSingleDoorSource(List<string> failures, StringBuilder log)
        {
            string vm = ReadSource(VmPath, failures);
            if (vm != null)
            {
                string code = StripLineComments(vm);
                if (code.IndexOf("BarracksService.EnqueueTraining", StringComparison.Ordinal) < 0)
                    failures.Add("[case 5] ManageScreenVM no longer calls BarracksService.EnqueueTraining — a forked " +
                                 "enqueue would bypass the unlock gate, the army cap and the resource spend.");
                if (code.IndexOf("TroopDialogueCommands.ShowMusterUI", StringComparison.Ordinal) < 0)
                    failures.Add("[case 5] ManageScreenVM no longer routes the Armies entry to " +
                                 "TroopDialogueCommands.ShowMusterUI — the muster panel has one opener, not two.");
                if (code.IndexOf("BuildTimerService.Instance.Enqueue", StringComparison.Ordinal) >= 0)
                    failures.Add("[case 5] ManageScreenVM enqueues on BuildTimerService DIRECTLY — training must go " +
                                 "through BarracksService so the charge and the cap cannot be skipped.");
            }

            string inter = ReadSource(InteractablePath, failures);
            if (inter != null)
            {
                string code = StripLineComments(inter);
                if (code.IndexOf("\"barracks\"", StringComparison.Ordinal) < 0)
                    failures.Add("[case 5] \"barracks\" is no longer in BuildingInteractable._noTalkDoor — the barracks " +
                                 "talk-door has been REOPENED. That is a SECOND training door and it contradicts " +
                                 "PROD-002 + canon §7 (one entry). If training is unreachable, fix ManageScreenVM.");
                else
                    log.AppendLine("  case 5 OK - barracks talk-door still closed; Manage is the single door");
            }

            // ── CASE 6 (added 2026-09-04, WO-1382): the door has TWO VERBS and NO MODE SWITCH ──
            // Owner ruling 2026-09-04 22:50, verbatim: "Delete this entirely: TRAIN | UPGRADE
            // OPTIONS. That should not be a mode switch." and "The review recommends removing
            // _troopMode entirely rather than trying to make the segmented control prettier. I
            // strongly agree." The old boxed TRAIN face was a view toggle that never enqueued
            // anything, sitting above the real priced CTA - three things said TRAIN and one
            // trained. This pin fails the build if the toggle field returns, or if either verb
            // face ("TRAIN 1 <NAME>" / "UPGRADE TO L<n>") is no longer authored on the panel.
            string panel = ReadSource(PanelPath, failures);
            if (panel != null)
            {
                string code = StripLineComments(panel);
                if (code.IndexOf("_troopMode", StringComparison.Ordinal) >= 0)
                    failures.Add("[case 6] ManageScreenPanel carries a _troopMode field again - the TRAIN | UPGRADE " +
                                 "OPTIONS mode switch was deleted by owner ruling (WO-1382): \"That should not be a " +
                                 "mode switch.\" Two verbs, two buttons, no toggle.");
                if (code.IndexOf("\"TRAIN 1 \"", StringComparison.Ordinal) < 0)
                    failures.Add("[case 6] the primary verb face \"TRAIN 1 <NAME>\" is not authored on the panel - the " +
                                 "Troops card must carry ONE priced TRAIN verb with the unit count in its label.");
                if (code.IndexOf("\"UPGRADE TO L\"", StringComparison.Ordinal) < 0)
                    failures.Add("[case 6] the secondary verb face \"UPGRADE TO L<n>\" is not authored on the panel - " +
                                 "upgrade is a second BUTTON on the same card, never a mode.");
                if (failures.Count == 0 || !failures.Exists(f => f.StartsWith("[case 6]", StringComparison.Ordinal)))
                    log.AppendLine("  case 6 OK - no _troopMode; TRAIN 1 <NAME> and UPGRADE TO L<n> are both authored");

                // -- CASE 7 (source half, WO-1389): the View actually PAINTS the destination --
                // The VM half above proves the sentence is composed; this proves the panel reads
                // it into the UPGRADE face's sub-line (a composed-but-unpainted line is invisible).
                if (code.IndexOf("NextUnlockText", StringComparison.Ordinal) < 0)
                    failures.Add("[case 7] ManageScreenPanel never reads TroopChoiceVM.NextUnlockText - the next-unlock " +
                                 "sentence is composed by the VM but never painted under the UPGRADE face.");
                else
                    log.AppendLine("  case 7 OK (source) - ManageScreenPanel composes NextUnlockText into the UPGRADE sub-line");
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        /// <summary>
        /// Installs the BuildTimerService singleton without play mode. Instance is an auto-property
        /// with a private setter, so the compiler-generated backing field is the seam; the property
        /// setter is tried first so a hand-written field would also be found.
        /// </summary>
        private static bool InstallQueueInstance(BuildTimerService svc)
        {
            var t = typeof(BuildTimerService);
            var prop = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.GetSetMethod(true) != null)
            {
                prop.GetSetMethod(true).Invoke(null, new object[] { svc });
                return ReferenceEquals(BuildTimerService.Instance, svc);
            }
            var f = t.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? t.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return ReferenceEquals(BuildTimerService.Instance, svc);
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
                reason = "MANAGE TRAIN DOOR OK - the Troops tab emits Train rows + Upgrade rows (verb-led, distinct) " +
                         "+ an Armies/muster entry, a Train tap lands a real JobKind.TrainTroop job on ChannelId.Train, " +
                         "and Manage is still the SINGLE door (barracks talk-door closed, no forked enqueue)";
                Debug.Log("MANAGE_TRAIN_DOOR_OK\n" + log);
                return true;
            }
            reason = $"MANAGE TRAIN DOOR: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"MANAGE_TRAIN_DOOR_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
