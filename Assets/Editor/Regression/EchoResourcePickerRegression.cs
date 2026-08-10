// =============================================================================
// EchoResourcePickerRegression — WO-830/831 oracle for the card-facing layer:
// the resource-picker VM projection, the picker verb, the disclosed synergy line
// (and its NON-disclosure of the hidden tri), and the WO-831 emergence beat data.
// Headless, data-decidable, no play-mode. Sibling suite to
// EchoSpecializationRegression (which owns the math/economy groups).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// — registered into DataRegression.RunAll by the orchestrator.
//
// FIVE ASSERTION GROUPS:
//   1. Chip projection    — EchoCardVM.ResourceChips(): exactly the 5 resources in
//      PickableResources order; Selected on the assigned one (with the "(now)" TEXT
//      cue); Preferred + the "best" cue IN THE LABEL only on the affinity chip, and
//      NO chip carries a Note any more (WO-883 — the note band duplicated the footer
//      and was the one row the picker's scroll fold cut in half); ASCII-only.
//   2. Picker verb        — vm.AssignResource("iron") writes through the seam
//      (lane=harvest, resource=iron persisted); a bogus token is a logged no-op.
//   3. Card strings       — StateText names the ASSIGNED resource + the (best) flag
//      only when matched; WhatText discloses the affinity ("Favors: X"); AskText is
//      the gather ask; ALL card strings ASCII; NO card string ever contains the
//      hidden-tri vocabulary (the secret stays secret at the string layer too).
//   4. Synergy line       — SynergyText ACTIVE when the pair runs, recipe text when
//      not; names the partner; never mentions "hidden"/"tri"/"secret".
//   6. WO-811 repair task — TaskChips = 5 resources + the "Repair structures" chip
//      LAST; the repair chip never carries an affinity cue; AssignRepair persists
//      "repair:<level>" (level preserved) with the "(now)" TEXT cue on re-read; the
//      repair StateText is honest (never a Gathering claim).
//   5. WO-831 emergence   — every roster entry has a non-empty ASCII EmergeLine;
//      EchoUnlockDialogue.EmergeLineFor falls back to the shared default on a null
//      entry; EchoRosterCatalog.LoadEmergence returns null GRACEFULLY (no throw)
//      when the LFS art is absent — the Guard fallback contract that guarantees a
//      missing sprite never blocks an unlock.
//
// SAFETY: snapshots+restores the PlayerPrefs save blob and the prior
// GameStateService singleton; disposes the VM; DestroyImmediate's the throwaways.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EchoResourcePickerRegression
    {
        private const string SaveKey = "dotr-save";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            void Fail(string s) => failures.Add("ECHO_PICKER FAIL: " + s);

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorInstance = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            EchoCardVM vm = null;
            bool installed = false;

            try
            {
                // --- Group 5 needs no state (pure catalog/loader). ---------------
                CheckEmergenceData(Fail);

                // --- Groups 1-4 need a headless GameState (same seam as the sibling suite).
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GameStateService (echo-picker-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!TryInstallHeadlessState(gss, throwaway, out string installErr))
                {
                    notes.Add("groups 1-4 skipped (needs fleet — " + installErr + ")");
                }
                else
                {
                    installed = true;
                    var state = gss.State;
                    if (state == null)
                    {
                        notes.Add("groups 1-4 skipped (throwaway state did not install)");
                    }
                    else
                    {
                        state.EchoCount = 6;
                        EchoBalanceCatalog.Reload();

                        // Bind the VM to Elowen (index 1, affinity Wood) assigned to IRON —
                        // a deliberate NON-match so both flag states are exercised.
                        state.EchoLanes = "food:1,iron:2,gold:1,crystals:1,iron:1,crystals:1";
                        vm = new EchoCardVM(1);

                        CheckChipProjection(vm, Fail);
                        CheckPickerVerb(vm, state, Fail);
                        CheckCardStrings(vm, state, Fail);
                        CheckRepairTask(vm, state, Fail);   // WO-811: the repair chip + verb + honest status
                        CheckSynergyLine(state, Fail);
                    }
                }
            }
            catch (Exception ex)
            {
                Fail($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (vm != null) vm.Dispose();
                EchoLaneBonuses.Reset();
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                if (installed) TrySetInstanceStatic(priorInstance);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            if (failures.Count == 0)
            {
                reason = "ECHO PICKER OK — 5-chip projection + picker verb + card strings (affinity disclosed, "
                         + "tri never) + WO-811 repair chip/verb/status (text-cued, no affinity cue) "
                         + "+ synergy line + WO-831 emergence data/fallback all hold"
                         + (notes.Count > 0 ? " [" + string.Join("; ", notes) + "]" : "");
                return true;
            }
            reason = $"echo-picker: {failures.Count} failure(s): " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Group 1 — Chip projection (5 resources, Selected/Preferred as TEXT)
        // =====================================================================
        private static void CheckChipProjection(EchoCardVM vm, Action<string> Fail)
        {
            var chips = vm.ResourceChips();
            if (chips == null || chips.Length != 5)
            {
                Fail($"ResourceChips length {(chips == null ? 0 : chips.Length)} (expected 5)");
                return;
            }

            var expectedOrder = EchoAssignments.PickableResources;
            for (int i = 0; i < 5; i++)
            {
                if (chips[i].Id != expectedOrder[i])
                    Fail($"chip[{i}].Id='{chips[i].Id}' (expected '{expectedOrder[i]}' — PickableResources order)");
                if (!IsAscii(chips[i].Label) || !IsAscii(chips[i].Note ?? ""))
                    Fail($"chip[{i}] label/note contains non-ASCII characters");
            }

            // Elowen assigned IRON: iron chip Selected + "(now)" text cue; wood chip is the
            // affinity (Preferred + the calling note); nothing else flagged.
            for (int i = 0; i < 5; i++)
            {
                var c = chips[i];
                bool expectSel = c.Id == EchoAssignments.ResIron;
                bool expectPref = c.Id == EchoAssignments.ResWood;
                if (c.Selected != expectSel)
                    Fail($"chip '{c.Id}' Selected={c.Selected} (expected {expectSel})");
                if (c.Preferred != expectPref)
                    Fail($"chip '{c.Id}' Preferred={c.Preferred} (expected {expectPref} — affinity is Wood)");
                if (expectSel && !c.Label.Contains("(now)"))
                    Fail($"selected chip '{c.Id}' label '{c.Label}' lacks the '(now)' TEXT cue (colorblind law)");
                // WO-883: the affinity cue moved OUT of a separate note band and INTO the chip
                // label. The disclosure itself is NOT optional (colorblind law — the match
                // bonus can never be carried by hue), only its home changed.
                if (expectPref && c.Label.IndexOf("best", StringComparison.OrdinalIgnoreCase) < 0)
                    Fail($"affinity chip '{c.Id}' label '{c.Label}' does not disclose the match bonus in TEXT " +
                         "(the calling must be readable without colour)");
                if (!expectPref && c.Label.IndexOf("best", StringComparison.OrdinalIgnoreCase) >= 0)
                    Fail($"non-affinity chip '{c.Id}' label '{c.Label}' claims the match bonus (only the calling chip is flagged)");
                // The note band is RETIRED. It was the verbatim tail of StateText, so the
                // footer said it again two lines down, and it made ONE picker row 39.5px taller
                // than the other four — the row the scroll fold sliced mid-sentence on the
                // owner's 2026-08-04 capture (docs/ui-review/screens-2026-08-04/EchoCard_2340x1080.png).
                if (!string.IsNullOrEmpty(c.Note))
                    Fail($"chip '{c.Id}' carries a per-chip note '{c.Note}' again (WO-883 retired it: it duplicated " +
                         "the footer's StateText and gave the fold a half-line of text to cut — keep the cue in the label)");
            }
        }

        // =====================================================================
        //  Group 2 — The picker verb writes through the seam
        // =====================================================================
        private static void CheckPickerVerb(EchoCardVM vm, GameState state, Action<string> Fail)
        {
            vm.AssignResource(EchoAssignments.ResGold);
            if (EchoAssignments.LaneOf(1) != EchoAssignments.LaneHarvest)
                Fail($"after AssignResource('gold'): LaneOf(1)='{EchoAssignments.LaneOf(1)}' (expected 'harvest')");
            if (EchoAssignments.ResourceTokenOf(1) != EchoAssignments.ResGold)
                Fail($"after AssignResource('gold'): ResourceTokenOf(1)='{EchoAssignments.ResourceTokenOf(1)}' (expected 'gold')");
            if (EchoAssignments.LevelOf(1) != 2)
                Fail($"AssignResource changed the level: LevelOf(1)={EchoAssignments.LevelOf(1)} (expected 2 preserved)");
            var parts = (state.EchoLanes ?? "").Split(',');
            if (parts.Length <= 1 || parts[1] != "gold:2")
                Fail($"persisted token[1]='{(parts.Length > 1 ? parts[1] : "<none>")}' (expected 'gold:2')");

            // Bogus token: logged no-op.
            string before = state.EchoLanes;
            vm.AssignResource("mithril");
            if (state.EchoLanes != before)
                Fail("AssignResource('mithril') mutated the assignment (must be a logged no-op)");

            // Restore the iron pick for the string checks below.
            vm.AssignResource(EchoAssignments.ResIron);
        }

        // =====================================================================
        //  Group 3 — Card strings (affinity disclosed; the secret never worded)
        // =====================================================================
        private static void CheckCardStrings(EchoCardVM vm, GameState state, Action<string> Fail)
        {
            // Non-matched (iron pick, wood affinity): resource named, no (best) flag.
            string stateText = vm.StateText;
            if (!stateText.Contains("Iron"))
                Fail($"StateText '{stateText}' does not name the assigned resource 'Iron'");
            if (stateText.Contains("best"))
                Fail($"StateText '{stateText}' claims (best) on a NON-matched pick (iron vs wood affinity)");

            string whatText = vm.WhatText;
            if (!whatText.Contains("Favors: Wood"))
                Fail($"WhatText '{whatText}' does not disclose the affinity ('Favors: Wood')");

            string askText = vm.AskText;
            // WO-811: the ask was reworded from "gather?" to "tend to?" because the picker
            // now offers gather AND repair — the old word claimed only half the choices.
            if (!askText.Contains("tend"))
                Fail($"AskText '{askText}' is not the tend-to ask (WO-811 reword — the picker offers gather AND repair)");
            if (askText.Contains(","))
                Fail($"AskText '{askText}' carries the full comma name (expected the short name)");

            // Matched (wood pick): the (best) calling flag appears.
            vm.AssignResource(EchoAssignments.ResWood);
            stateText = vm.StateText;
            if (!stateText.Contains("Wood") || !stateText.Contains("best"))
                Fail($"matched StateText '{stateText}' must name Wood and carry the (best) calling flag");

            // ASCII + secrecy sweep over every card string.
            foreach (var s in new[] { vm.NameText, vm.ElementText, vm.WhatText, vm.StateText, vm.SynergyText, vm.AskText })
            {
                if (!IsAscii(s))
                    Fail($"card string contains non-ASCII characters: '{s}'");
                string low = (s ?? "").ToLowerInvariant();
                if (low.Contains("hidden") || low.Contains("tri-synergy") || low.Contains("secret"))
                    Fail($"card string leaks the hidden tri-synergy vocabulary: '{s}'");
            }
        }

        // =====================================================================
        //  Group 4 — The disclosed synergy line (active + recipe states)
        // =====================================================================
        private static void CheckSynergyLine(GameState state, Action<string> Fail)
        {
            // Provisions pair = Elowen (wood) + Aldwin (food). Both matched -> ACTIVE.
            state.EchoLanes = "food:1,wood:1,gold:1,crystals:1,iron:1,crystals:1";
            using (var vmActive = new VmScope(1))
            {
                string sy = vmActive.Vm.SynergyText;
                if (!sy.Contains("Provisions") || !sy.Contains("ACTIVE"))
                    Fail($"active SynergyText '{sy}' must name the Provisions pair as ACTIVE");
                if (!sy.Contains("Aldwin"))
                    Fail($"active SynergyText '{sy}' must name the partner (Aldwin)");
            }

            // Break the pair (Aldwin off food) -> the recipe text, naming partner + resource.
            state.EchoLanes = "wood:1,wood:1,gold:1,crystals:1,iron:1,crystals:1";
            using (var vmIdle = new VmScope(1))
            {
                string sy = vmIdle.Vm.SynergyText;
                if (sy.Contains("ACTIVE"))
                    Fail($"broken-pair SynergyText '{sy}' still claims ACTIVE");
                if (!sy.Contains("Aldwin") || !sy.Contains("Food"))
                    Fail($"broken-pair SynergyText '{sy}' must hint the partner and its resource (Aldwin / Food)");
            }
        }

        // =====================================================================
        //  Group 6 — WO-811: the repair task chip + verb + honest status strings
        // =====================================================================
        private static void CheckRepairTask(EchoCardVM vm, GameState state, Action<string> Fail)
        {
            // Chip projection: exactly ONE extra TASK row, appended LAST, resource order
            // ahead of it unchanged (the WO-830 contract is untouched).
            var all = vm.TaskChips();
            if (all == null || all.Length != 6)
            {
                Fail($"TaskChips length {(all == null ? 0 : all.Length)} (expected 6 — five resources + repair)");
                return;
            }
            var expectedOrder = EchoAssignments.PickableResources;
            for (int i = 0; i < 5; i++)
                if (all[i].Id != expectedOrder[i])
                    Fail($"TaskChips[{i}].Id='{all[i].Id}' (expected '{expectedOrder[i]}' — resource rows unchanged ahead of repair)");

            var chip = vm.RepairTaskChip();
            if (chip.Id != EchoAssignments.LaneRepair)
                Fail($"RepairTaskChip.Id='{chip.Id}' (expected 'repair')");
            if (!chip.Label.Contains("Repair structures"))
                Fail($"RepairTaskChip label '{chip.Label}' must carry the full 'Repair structures' text (no clip, no icon-only)");
            if (!IsAscii(chip.Label))
                Fail($"RepairTaskChip label '{chip.Label}' contains non-ASCII characters");
            if (chip.Selected)
                Fail("RepairTaskChip.Selected=true while the echo is harvest-assigned");
            if (chip.Preferred || chip.Label.IndexOf("best", StringComparison.OrdinalIgnoreCase) >= 0)
                Fail($"RepairTaskChip claims an affinity cue ('{chip.Label}') — Repairs was REMOVED as an affinity (WO-830 2026-08-02); repair never carries 'best'");

            // The verb: assign repair -> "repair:<level>" persisted with the level preserved
            // (the card-strings group left echo 1 on wood at level 2).
            vm.AssignRepair();
            if (EchoAssignments.LaneOf(1) != EchoAssignments.LaneRepair)
                Fail($"after AssignRepair: LaneOf(1)='{EchoAssignments.LaneOf(1)}' (expected 'repair')");
            var parts = (state.EchoLanes ?? "").Split(',');
            if (parts.Length <= 1 || parts[1] != "repair:2")
                Fail($"after AssignRepair: persisted token[1]='{(parts.Length > 1 ? parts[1] : "<none>")}' (expected 'repair:2' — level preserved); full='{state.EchoLanes}'");

            chip = vm.RepairTaskChip();
            if (!chip.Selected || !chip.Label.Contains("(now)"))
                Fail($"selected RepairTaskChip label '{chip.Label}' lacks the '(now)' TEXT cue (colorblind law)");

            // Status string: an honest repair line — TEXT, never a gather claim. In this
            // headless oracle EchoRepairService.Instance is null, so the plain assigned
            // line is the truthful render (the nothing-to-repair / waiting-for-materials
            // tails are the live service's to add and are asserted by its own traces).
            string stateText = vm.StateText;
            if (!stateText.Contains("Repair"))
                Fail($"repair StateText '{stateText}' does not read as a repair status");
            if (stateText.Contains("Gathering"))
                Fail($"repair StateText '{stateText}' still claims Gathering");
            if (!stateText.Contains("Lv 2"))
                Fail($"repair StateText '{stateText}' lost the level (expected 'Lv 2')");
            if (!IsAscii(stateText))
                Fail($"repair StateText '{stateText}' contains non-ASCII characters");

            // Restore the harvest pick so any later group starts from the WO-830 baseline.
            vm.AssignResource(EchoAssignments.ResIron);
        }

        /// <summary>Tiny disposable wrapper so each temporary VM always unsubscribes.</summary>
        private sealed class VmScope : IDisposable
        {
            public readonly EchoCardVM Vm;
            public VmScope(int index) { Vm = new EchoCardVM(index); }
            public void Dispose() { Vm.Dispose(); }
        }

        // =====================================================================
        //  Group 5 — WO-831 emergence data + graceful missing-art fallback
        // =====================================================================
        private static void CheckEmergenceData(Action<string> Fail)
        {
            var roster = EchoRosterCatalog.All;
            if (roster == null || roster.Length != 6)
            {
                Fail("roster unavailable for the emergence check");
                return;
            }
            foreach (var e in roster)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.EmergeLine))
                    Fail($"'{e.Id}' has no EmergeLine (WO-831)");
                else if (!IsAscii(e.EmergeLine))
                    Fail($"'{e.Id}' EmergeLine contains non-ASCII characters");

                // Missing art must return null GRACEFULLY (Guard contract), never throw --
                // this is the "a missing sprite NEVER blocks the unlock" guarantee. If the
                // LFS art IS present, a sprite is equally acceptable.
                try
                {
                    EchoRosterCatalog.LoadEmergence(e.PortraitName);
                }
                catch (Exception ex)
                {
                    Fail($"LoadEmergence('{e.PortraitName}') THREW {ex.GetType().Name} (must degrade, never throw)");
                }
            }

            // The pure fallback line: null entry -> the shared default (never blank).
            string def = EchoUnlockDialogue.EmergeLineFor(null);
            if (string.IsNullOrEmpty(def) || !IsAscii(def))
                Fail($"EmergeLineFor(null) = '{def}' (expected the non-empty ASCII shared default)");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static bool IsAscii(string s)
        {
            if (s == null) return true;
            foreach (char c in s) if (c > 127) return false;
            return true;
        }

        private static bool TryInstallHeadlessState(GameStateService svc, GameState state, out string err)
        {
            err = null;
            var stateField = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null)
            { err = "GameStateService._state field not found by reflection"; return false; }
            stateField.SetValue(svc, state);
            if (!TrySetInstanceStatic(svc))
            { err = "GameStateService._instance static not found by reflection"; return false; }
            return true;
        }

        private static bool TrySetInstanceStatic(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }
    }
}
