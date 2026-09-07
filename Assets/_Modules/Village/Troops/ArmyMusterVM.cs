// =============================================================================
// ArmyMusterVM — the Armies loadout-bank / training-order panel's PURE ViewModel
// (WO-1512). Extracted from ArmyMusterPanel, which had been holding the MODEL
// itself: `private static readonly ArmyComposition s_composition` lived on the
// View, and every command (train, save slot, select slot, quick-fill, rename,
// step a troop count) mutated it in place and called ArmyMusterService /
// ArmyLoadoutService directly.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS FILE EXISTS (ARCHITECTURE_PRINCIPLES.md §1/§2 — presentation is a
// separate layer that NEVER touches the objects): a View that OWNS the staged
// army owns game state. Two consequences, both real rather than theoretical:
//   1. The composition's lifetime was the View's static field, so it survived
//      panel close, scene load and Reset with no owner — nothing could observe,
//      test or reset it except the panel itself.
//   2. Nothing could unit-test the muster transaction without building uGUI.
// The VM now owns the composition and every verb; the View paints and routes
// taps. Pattern copied from ManageScreenPanel / TroopTrainingVM — commands on
// the VM, `Changed` back to the View, no new idiom invented.
//
// PURE C#: implements DeNelle.Core.UI.Mvvm.IPanelViewModel and carries NO
// UnityEngine UI types. Toast TONE is a VM-side enum (MusterTone) that the View
// maps to ElarionUiKit.ToastTone — the VM must not know what a toast looks like.
//
// ⚠ THE COMPOSITION IS STILL PROCESS-WIDE (Shared) ON PURPOSE. The staged plan
// deliberately survives closing the panel — the owner's loop is "stage, go play,
// come back and train". Moving it here does not change that behaviour; it moves
// the OWNERSHIP off the presentation layer so a future save-backed home has one
// obvious place to land.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village
{
    /// <summary>Neutral outcome tone for a VM command. The View maps this to its toast palette;
    /// the VM never names a colour or a UI type (§2).</summary>
    public enum MusterTone { Info, Good, Warn, Bad }

    /// <summary>What a VM command wants said on screen, and how loudly. Empty Message = say nothing.</summary>
    public readonly struct MusterCommandResult
    {
        public readonly string Message;
        public readonly MusterTone Tone;
        public readonly bool Changed;

        public MusterCommandResult(string message, MusterTone tone, bool changed = true)
        {
            Message = message;
            Tone = tone;
            Changed = changed;
        }

        public static readonly MusterCommandResult None = new MusterCommandResult(null, MusterTone.Info, false);
    }

    /// <summary>
    /// ViewModel for the Armies panel: owns the staged <see cref="ArmyComposition"/>, the active
    /// loadout slot, the last training-order receipt, and every verb the panel offers.
    /// </summary>
    public sealed class ArmyMusterVM : IPanelViewModel
    {
        private const string Sys = "Muster";

        // The staged plan. Process-wide so the panel can be closed and reopened mid-plan (see the
        // header) — but owned HERE, by the model layer, not by a View's static field.
        private static readonly ArmyComposition s_shared = new ArmyComposition { Name = "Raid Push" };

        private readonly ArmyComposition _composition;
        private int _activeSlot;
        private string _lastResultHeadline = "";
        private string _lastResultDetail = "";

        public event Action Changed;

        public string Title => "Armies - Loadouts";

        /// <summary>Live view of the staged plan. The View READS it to paint; it never mutates it
        /// (every mutation is a command below).</summary>
        public ArmyComposition Composition => _composition;

        public int ActiveSlot => _activeSlot;
        public string LastResultHeadline => _lastResultHeadline;
        public string LastResultDetail => _lastResultDetail;
        public string ArmyName => _composition.Name;
        public int SlotCount => ArmyLoadoutService.SlotCount;

        /// <summary>Wallet readout for the panel's currency chip.</summary>
        public int GoldBalance => ArmyMusterService.GoldBalance();

        /// <summary>The staged plan's cost/time/queue-fit projection, computed by the service.</summary>
        public MusterPreview Preview => ArmyMusterService.Preview(_composition);

        /// <summary>Live production build: the process-wide staged plan.</summary>
        public static ArmyMusterVM CreateDefault() => new ArmyMusterVM(s_shared);

        /// <summary>Test seam — inject an isolated composition so a suite never touches the shared plan.</summary>
        public ArmyMusterVM(ArmyComposition composition)
        {
            _composition = composition ?? new ArmyComposition { Name = "Raid Push" };
        }

        // ── lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Hydrate the working set from the saved ACTIVE slot on panel open, seeding a quick-fill
        /// when the slot is empty and no receipt is on screen (so the panel is never blank).
        /// </summary>
        public void HydrateFromActiveSlot()
        {
            var army = ArmyLoadoutService.Ensure();
            _activeSlot = army != null ? army.ActiveLoadoutIndex : 0;
            ArmyLoadoutService.LoadInto(_activeSlot, _composition);
            if (_composition.TotalUnits <= 0 && string.IsNullOrEmpty(_lastResultHeadline))
                ArmyLoadoutService.ApplyRecipe(_composition, 0);
            FlowTrace.Step(Sys, "ArmyMusterVM hydrated slot " + _activeSlot +
                                " units=" + _composition.TotalUnits);
            Raise();
        }

        public void Close() { /* the View owns its own teardown; nothing to detach here. */ }

        public void Dispose() { Changed = null; }

        // ── queries the View would otherwise have made against the catalog ────

        /// <summary>Troops the Barracks has unlocked, in catalog order. The UNLOCK GATE is a rule,
        /// so it is decided here — the View used to call BarracksService.IsTroopUnlocked itself.</summary>
        public List<TroopDef> OfferedTroops()
        {
            var offered = new List<TroopDef>();
            var all = TroopCatalog.All;
            if (all == null) return offered;
            foreach (var def in all)
                if (def != null && !string.IsNullOrEmpty(def.Id) && BarracksService.IsTroopUnlocked(def.Id))
                    offered.Add(def);
            return offered;
        }

        public int CountOf(string troopId) => _composition.CountOf(troopId);

        public string SlotName(int index) => ArmyLoadoutService.SlotName(index);

        /// <summary>Display name for a staged row's troop id (catalog lookup lives in the VM).</summary>
        public string DisplayNameOf(string troopId)
        {
            var def = TroopCatalog.Find(troopId);
            return def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : troopId;
        }

        // ── commands ──────────────────────────────────────────────────────────

        /// <summary>
        /// THE TRAINING ORDER. Auto-saves the active slot first (a staged plan is never lost to a
        /// train tap), musters through the service, then drops what actually queued from the working
        /// set — the saved slot keeps the FULL plan.
        /// </summary>
        public MusterCommandResult Muster()
        {
            ArmyLoadoutService.SaveFrom(_activeSlot, _composition);

            var report = ArmyMusterService.Muster(_composition);
            _lastResultHeadline = PlayerWords(report.Headline);
            _lastResultDetail = PlayerWords(report.Detail);

            var tone = report.Complete ? MusterTone.Good
                     : report.AnyQueued ? MusterTone.Warn
                     : MusterTone.Bad;

            foreach (var r in report.Rows)
                if (r.Queued > 0) _composition.Add(r.TroopId, -r.Queued);

            FlowTrace.Step(Sys, "ArmyMusterVM.Muster complete=" + report.Complete +
                                " anyQueued=" + report.AnyQueued);
            Raise();
            return new MusterCommandResult(PlayerWords(report.Summary), tone);
        }

        public MusterCommandResult SaveSlot()
        {
            ArmyLoadoutService.SaveFrom(_activeSlot, _composition);
            Raise();
            return new MusterCommandResult(
                "Saved '" + _composition.Name + "' to slot " + (_activeSlot + 1) + ".", MusterTone.Good);
        }

        /// <summary>Re-tap the ACTIVE slot to reload it (discard unsaved edits); tap another slot to
        /// auto-save the one you leave and edit the new one. Never lose work.</summary>
        public MusterCommandResult SelectSlot(int index)
        {
            if (index < 0 || index >= ArmyLoadoutService.SlotCount) return MusterCommandResult.None;

            string message;
            if (index == _activeSlot)
            {
                ArmyLoadoutService.LoadInto(index, _composition);
                message = "Reloaded " + ArmyLoadoutService.SlotName(index) + ".";
            }
            else
            {
                ArmyLoadoutService.SaveFrom(_activeSlot, _composition);
                _activeSlot = index;
                ArmyLoadoutService.LoadInto(index, _composition);
                message = "Editing " + ArmyLoadoutService.SlotName(index) + ".";
            }
            Raise();
            return new MusterCommandResult(message, MusterTone.Info);
        }

        public MusterCommandResult ApplyRecipe(int recipe)
        {
            string msg = ArmyLoadoutService.ApplyRecipe(_composition, recipe);
            Raise();
            return new MusterCommandResult(msg, MusterTone.Warn);
        }

        /// <summary>Cycle the default army names so the player can feel ownership without a soft
        /// keyboard. The name list is copy, so it lives with the model that carries the name.</summary>
        public MusterCommandResult CycleName()
        {
            string[] names =
            {
                "Raid Push", "Wall Hold", "Siege Prep",
                "Night Watch", "Quick Strike", "Last Stand", "New Army",
            };
            string cur = _composition.Name ?? "";
            int idx = 0;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], cur, StringComparison.OrdinalIgnoreCase))
                { idx = (i + 1) % names.Length; break; }
            _composition.Name = names[idx];
            Raise();
            return MusterCommandResult.None;
        }

        /// <summary>
        /// Step one troop's staged count. The maxOwned ceiling is a RULE and is enforced here —
        /// the View used to read TroopCatalog itself to decide whether the tap was legal.
        /// </summary>
        public MusterCommandResult Step(string troopId, int delta)
        {
            if (string.IsNullOrEmpty(troopId)) return MusterCommandResult.None;

            var def = TroopCatalog.Find(troopId);
            if (def != null && def.MaxOwned > 0 && delta > 0)
            {
                int want = _composition.CountOf(troopId) + delta;
                if (want > def.MaxOwned)
                {
                    string name = string.IsNullOrEmpty(def.DisplayName) ? troopId : def.DisplayName;
                    return new MusterCommandResult(
                        "Only " + def.MaxOwned + "x " + name + " in a loadout.", MusterTone.Info, changed: false);
                }
            }

            _composition.Add(troopId, delta);
            Raise();
            return MusterCommandResult.None;
        }

        // ── copy ──────────────────────────────────────────────────────────────

        /// <summary>
        /// OWNER RULING 2026-08-26 ("what dos muster army mean? Thats where im lost"): "muster" is
        /// archaic jargon for a TRAINING ORDER and must not reach the player. The rewrite stays a
        /// PLAYER-FACING STRING map — ArmyMusterService's identifiers are live and renaming them is a
        /// wide mechanical diff with zero player benefit. It moved from the panel to the VM with the
        /// rest of the transaction because the VM is what now produces the message. ASCII in, out.
        /// ⚠ ArmyMusterService.cs still AUTHORS the archaic word in its literals; this maps them.
        /// </summary>
        public static string PlayerWords(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("Nothing to muster", "Nothing to train");
            s = s.Replace("Mustered", "Training ordered");
            s = s.Replace("mustered", "training ordered");
            s = s.Replace("Muster", "Train");
            s = s.Replace("muster", "training");
            return s;
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
