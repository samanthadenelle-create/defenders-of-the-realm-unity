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
    /// <summary>One roster card's fully-projected identity + status (mirrors EchoCardVM.LaneChip's
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

        /// <summary>Owned card status: element + live lane/level/bonus readout (colorblind-safe TEXT).</summary>
        private static string OwnedStatus(int index, string element)
        {
            var ro = EchoBonusCalculator.ReadoutFor(index);
            string line2;
            if (ro.Lane == LaneType.Idle)
            {
                line2 = "Idle -- tap to assign";
            }
            else
            {
                string laneLabel = EchoAssignments.LabelFor(EchoAssignments.LaneOf(index));
                line2 = laneLabel + " - Lv " + ro.Level + " - +" + Mathf.RoundToInt(ro.BonusPct) + "%";
                if (ro.PreferredMatch) line2 += " (best)";
            }
            return element + "\n" + line2;
        }

        /// <summary>Locked card status: the real unlock cadence (order K at (K-1)*perEcho waves).</summary>
        private string LockedStatus(int index)
        {
            int unlockWave = index * Mathf.Max(1, PerEcho);
            return "Locked\nUnlocks at wave " + unlockWave;
        }

        // -- Commands -----------------------------------------------------------

        /// <summary>Open the per-echo lane picker for an owned card (navigation; injected opener).</summary>
        public void OpenCard(int index) => _onOpenCard?.Invoke(index);

        /// <summary>Assign an Echo to a lane via the EchoAssignments seam (the ONE mutation).</summary>
        public bool Assign(int index, string laneId) => EchoAssignments.Assign(index, laneId);
    }
}
