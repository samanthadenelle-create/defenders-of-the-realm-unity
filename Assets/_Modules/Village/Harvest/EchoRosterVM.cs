// =============================================================================
// EchoRosterVM -- the "pet box" roster ViewModel (MVVM, Silo F). Extends the shared
// EchoWorkforceVM snapshot with the 6-spirit roster grid + per-echo readout.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owns EVERYTHING EchoRosterView used to read inline: the EchoRosterCatalog identity
// table, the per-echo specialization readout (EchoBonusCalculator.ReadoutFor +
// EchoAssignments lane/label), the OWNED/LOCKED state, the portrait sprite, and the
// locked-card unlock-wave math (`index * wavesPerEcho`). The View becomes a dumb skin
// that lays out <see cref="Cards"/> and binds the base snapshot; it reads NO service.
//
// Two commands: OpenCard(index) (navigation -> the per-echo lane picker, injected so the
// VM never references a View type) and Assign(index, lane) (routes to the EchoAssignments
// seam -- the ONE mutation, exposed for completeness + tests).
// =============================================================================
using System;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>One roster card's fully-projected identity + status (mirrors EchoCardVM.ResourceChip's
    /// bespoke-struct pattern -- carries the portrait Sprite the same way EchoCardVM.Portrait does).</summary>
    public readonly struct EchoRosterCardVM
    {
        public readonly int Index;                 // 0-based echo index
        public readonly int Order;                 // 1-based roster order (grid placement)
        public readonly bool Owned;
        public readonly string DisplayName;        // owned -> real name; locked -> "Locked Echo"
        public readonly string Element;            // element subtitle
        public readonly Sprite Portrait;           // roster-catalog portrait (null-safe)
        public readonly string PortraitFallback;   // shown when Portrait is null (owned -> element; locked -> "?")
        public readonly string StatusText;         // owned -> element + lane/level/bonus; locked -> unlock wave

        public EchoRosterCardVM(int index, int order, bool owned, string displayName, string element,
                                Sprite portrait, string portraitFallback, string statusText)
        {
            Index = index; Order = order; Owned = owned;
            DisplayName = displayName; Element = element;
            Portrait = portrait; PortraitFallback = portraitFallback; StatusText = statusText;
        }
    }

    /// <summary>Roster ViewModel: the shared workforce snapshot (base) + the 6-spirit card grid.</summary>
    public sealed class EchoRosterVM : EchoWorkforceVM
    {
        private readonly Action<int> _onOpenCard;
        private EchoRosterCardVM[] _cards = Array.Empty<EchoRosterCardVM>();

        public EchoRosterVM(IEchoWorkforce model, Action<int> onOpenCard, Action onClose)
            : base(model, onClose)
        {
            _onOpenCard = onOpenCard;
            Title = "ECHOES OF ELARION";
            // The base ctor already ran the virtual Recompute() -> RebuildCards(), so Cards is
            // current here. Card projection does not depend on _onOpenCard (used only by the OpenCard command).
        }

        /// <summary>The ONLY resolution site: live workforce + the EchoCard picker opener.</summary>
        public static EchoRosterVM CreateDefault(Action onClose)
        {
            return new EchoRosterVM(new EchoServiceWorkforce(), EchoCard.Open, onClose);
        }

        /// <summary>The 6 roster cards (owned lit / locked dim), current as of the last snapshot.</summary>
        public System.Collections.Generic.IReadOnlyList<EchoRosterCardVM> Cards => _cards;

        /// <summary>The starter spirit's name (first-run banner). Empty when no starter.</summary>
        public string StarterName
        {
            get
            {
                var starter = EchoRosterCatalog.ByIndex(Owned - 1);
                return starter != null ? starter.DisplayName : "Your first spirit";
            }
        }

        protected override void Recompute()
        {
            base.Recompute();
            RebuildCards();
        }

        private void RebuildCards()
        {
            var roster = EchoRosterCatalog.All;
            if (roster == null) { _cards = Array.Empty<EchoRosterCardVM>(); return; }

            var list = new EchoRosterCardVM[roster.Length];
            for (int i = 0; i < roster.Length; i++)
            {
                var entry = roster[i];
                int index = entry != null ? entry.Order - 1 : i;   // 0-based, mirrors the old grid
                bool owned = index < Owned;
                var portrait = entry != null ? EchoRosterCatalog.LoadPortrait(entry.PortraitName) : null;
                string element = entry != null ? entry.Element : "";
                string name = owned ? (entry != null ? entry.DisplayName : "Echo") : "Locked Echo";
                string fallback = owned ? element : "?";
                string status = owned ? OwnedStatus(index, element) : LockedStatus(index);
                list[i] = new EchoRosterCardVM(index, entry != null ? entry.Order : (i + 1),
                                               owned, name, element, portrait, fallback, status);
            }
            _cards = list;
        }

        /// <summary>Owned card status: short resource/level/bonus only (colorblind-safe TEXT).
        /// WO-830: a harvesting Echo names the ASSIGNED RESOURCE ("Wood - Lv 1 - +55% (best)")
        /// so the roster reads the whole workforce at a glance; a legacy non-harvest lane keeps
        /// its lane label. Does NOT prefix the catalog Element ("Essence of a fallen keeper") --
        /// that line is lore for the unlock dialogue; on the roster card it stacked over the
        /// display name (owner F8 2026-07-24 pet screen). The % excludes the pair synergy
        /// (disclosed on the card) and the hidden tri (never shown -- WO-830 Sec.3d).</summary>
        private static string OwnedStatus(int index, string element)
        {
            var ro = EchoBonusCalculator.ReadoutFor(index);
            if (ro.Lane == LaneType.Idle)
                return "Idle -- tap to assign";

            string what = ro.Lane == LaneType.Harvest
                ? EchoAssignments.ResourceLabelFor(EchoAssignments.ResourceTokenOf(index))
                : EchoAssignments.LabelFor(EchoAssignments.LaneOf(index));
            if (string.IsNullOrEmpty(what)) what = "Harvest";
            string line = what + " - Lv " + ro.Level + " - +" + Mathf.RoundToInt(ro.BonusPct) + "%";
            if (ro.PreferredMatch) line += " (best)";
            return line;
        }

        /// <summary>Locked card status: single line (no "Locked\n" stack under the name).</summary>
        private string LockedStatus(int index)
        {
            int unlockWave = index * Mathf.Max(1, PerEcho);
            return "Unlocks at wave " + unlockWave;
        }

        // -- Commands -----------------------------------------------------------

        /// <summary>Open the per-echo lane picker for an owned card (navigation; injected opener).</summary>
        public void OpenCard(int index) => _onOpenCard?.Invoke(index);

        /// <summary>Assign an Echo to a lane via the EchoAssignments seam (the ONE mutation).</summary>
        public bool Assign(int index, string laneId) => EchoAssignments.Assign(index, laneId);
    }
}
