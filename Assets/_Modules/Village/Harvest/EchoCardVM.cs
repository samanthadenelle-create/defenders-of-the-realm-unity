// =============================================================================
// EchoCardVM -- view-model for the WO-681 Echo select card (MVVM strict).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The VM owns ALL EchoService / EchoAssignments / GameState reads and the assign
// verb; the View (EchoCardView) is a dumb skin built through ElarionUiKit that
// binds these strings and calls AssignLane -- it never touches a service
// (SESSION_CANON_LOADER "MVVM strict"; ARCHITECTURE_PRINCIPLES SS2 presentation
// never touches the objects).
//
// STATE line semantics (WO-738): live from the shared EchoBonusCalculator --
//   assigned lane -> "Harvest - Lv 3 - +65%"   (lane label + level + current
//                    specialization bonus %; the readout math lives in the calculator)
//   idle          -> "Idle - waiting for your word."
// Identity (name / element / flavor / portrait) is read from EchoRosterCatalog.ByIndex
// (the six named spirits), NOT hardcoded. The lane picker offers the four functional
// lanes; Defense + Exploration carry an HONEST "passive - active in raids/dungeons" tag
// so the player is never misled that they pay off now (full agency, honestly labeled).
// ASCII-only separators ('-' not the middle-dot) -- glyph-safe on the shipped TMP
// font; states read as TEXT, never by color alone (colorblind owner).
// =============================================================================
using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// View-model for the Echo select card (WO-681). One instance per open card,
    /// bound to a specific echo index. Owns every service read; raises
    /// <see cref="Changed"/> when the underlying workforce state moves.
    /// </summary>
    public sealed class EchoCardVM : IDisposable
    {
        /// <summary>One pickable functional lane for the "What should this Echo focus on?" picker
        /// (WO-738: harvest / crafting / defense / exploration). All four are fully assignable;
        /// Defense + Exploration are HONESTLY labeled (via <see cref="Note"/>) as passive bonuses that
        /// only pay off in offline raids / dungeons -- state carried in TEXT, never hue (colorblind owner).</summary>
        public readonly struct LaneChip
        {
            public readonly string Id;        // functional lane token ("harvest"/"crafting"/"defense"/"exploration")
            public readonly string Label;     // main lane name (+ " (now)" when selected -- TEXT cue, never hue)
            public readonly string Note;      // honesty + preferred subtext (ASCII; may be "")
            public readonly bool Selected;    // this echo is currently assigned to this lane
            public readonly bool Preferred;   // this lane is the echo's element-preferred lane (its +bonus lands here)
            public LaneChip(string id, string label, string note, bool selected, bool preferred)
            {
                Id = id; Label = label; Note = note; Selected = selected; Preferred = preferred;
            }
        }

        /// <summary>Raised when any displayed value may have changed (View re-binds).</summary>
        public event Action Changed;

        /// <summary>Index of the Echo this card describes (0-based, &lt; EchoCount).</summary>
        public int EchoIndex { get; }

        public EchoCardVM(int echoIndex)
        {
            EchoIndex = Math.Max(0, echoIndex);
            if (EchoService.Instance != null) EchoService.Instance.Changed += OnServiceChanged;
            EchoAssignments.Changed += OnServiceChanged;
        }

        public void Dispose()
        {
            if (EchoService.Instance != null) EchoService.Instance.Changed -= OnServiceChanged;
            EchoAssignments.Changed -= OnServiceChanged;
        }

        private void OnServiceChanged() => Changed?.Invoke();

        // ── Displayed strings (View binds verbatim; ASCII only) ────────────────

        /// <summary>Card header name, e.g. "Echo 2 of 4 - Verdant Stag (Nature Echo)" -- the REAL
        /// spirit identity from the roster catalog (WO-738; no longer the stale "Spirit of the Tree").</summary>
        public string NameText
        {
            get
            {
                var svc = EchoService.Instance;
                int count = svc != null ? svc.EchoCount : 1;
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                string name = entry != null ? entry.DisplayName : "Echo";
                return $"Echo {EchoIndex + 1} of {count} - {name}";
            }
        }

        /// <summary>The element subtitle for this Echo ("Ice Elemental"), from the roster catalog.</summary>
        public string ElementText
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                return entry != null ? entry.Element : "";
            }
        }

        /// <summary>Short WHAT line under the name -- Element only (ASCII). Full Flavor/Lore
        /// belongs on the unlock dialogue; dumping it here stacked over the lane picker
        /// (owner F8 2026-07-24 Echo card screenshot).</summary>
        public string WhatText
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                string element = entry != null ? entry.Element : "";
                if (!string.IsNullOrEmpty(element)) return element;
                return "A spirit of Elarion -- gathers while you fight.";
            }
        }

        /// <summary>The live STATE line: assigned lane + level + current specialization bonus % (from the
        /// shared EchoBonusCalculator), or the idle ask. State carried in TEXT (colorblind-safe).</summary>
        public string StateText
        {
            get
            {
                var ro = EchoBonusCalculator.ReadoutFor(EchoIndex);
                if (ro.Lane == LaneType.Idle)
                    return "Idle - waiting for your word.";
                string laneLabel = EchoAssignments.LabelFor(EchoAssignments.LaneOf(EchoIndex));
                string s = $"{laneLabel} - Lv {ro.Level} - +{Mathf.RoundToInt(ro.BonusPct)}%";
                if (ro.PreferredMatch) s += " (best -- this Echo's calling)";
                return s;
            }
        }

        /// <summary>The action-row prompt (one ask, one row).</summary>
        public string AskText => "What should this Echo focus on?";

        /// <summary>The Echo's portrait sprite (roster catalog -> Sprite.Create; null-safe, cached).
        /// The View binds this to the portrait socket and skips the image when null.</summary>
        public Sprite Portrait
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                return entry != null ? EchoRosterCatalog.LoadPortrait(entry.PortraitName) : null;
            }
        }

        /// <summary>The four functional lane chips (harvest/crafting/defense/exploration). Selected state,
        /// the element-preferred "best" tag, and the Defense/Exploration passive-honesty tags are all
        /// carried AS TEXT (never hue) so the picker never misleads a colorblind player about what pays off now.</summary>
        public LaneChip[] LaneChips()
        {
            string current = EchoAssignments.LaneOf(EchoIndex);
            var entry = EchoRosterCatalog.ByIndex(EchoIndex);
            var lanes = EchoAssignments.Lanes;
            var chips = new LaneChip[lanes.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                string lane = lanes[i];
                bool sel = lane == current;
                bool preferred = entry != null && entry.PreferredLane == LaneTypeFor(lane);
                string label = EchoAssignments.LabelFor(lane) + (sel ? " (now)" : "");
                chips[i] = new LaneChip(lane, label, NoteFor(lane, preferred), sel, preferred);
            }
            return chips;
        }

        /// <summary>The honesty + preferred subtext for a lane chip (ASCII, TEXT-carried). Defense +
        /// Exploration are passive bonuses that only pay off in offline raids / dungeons -- say so plainly.</summary>
        private static string NoteFor(string lane, bool preferred)
        {
            string honesty = "";
            if (lane == EchoAssignments.LaneDefense)          honesty = "passive - active in raids";
            else if (lane == EchoAssignments.LaneExploration) honesty = "passive - active in dungeons";

            if (preferred)
                return string.IsNullOrEmpty(honesty) ? "best for this Echo" : "best for this Echo - " + honesty;
            return honesty;
        }

        /// <summary>Map a functional lane token to its LaneType (unknown/idle -> Idle).</summary>
        private static LaneType LaneTypeFor(string lane)
        {
            switch (lane)
            {
                case EchoAssignments.LaneHarvest:     return LaneType.Harvest;
                case EchoAssignments.LaneCrafting:    return LaneType.Crafting;
                case EchoAssignments.LaneDefense:     return LaneType.Defense;
                case EchoAssignments.LaneExploration: return LaneType.Exploration;
                default:                              return LaneType.Idle;
            }
        }

        // ── The assign verb (the ONLY mutation this card performs) ─────────────

        /// <summary>Assign this Echo to <paramref name="laneId"/> via the WO-658 seam.
        /// EchoAssignments traces + persists + raises Changed (card + HUD refresh).</summary>
        public void AssignLane(string laneId)
        {
            FlowTrace.Step("Echo", $"Card: assign requested echo={EchoIndex} lane='{laneId}'.");
            EchoAssignments.Assign(EchoIndex, laneId);
        }

        // ── First-meeting one-shot (WO-681 spec 3) ──────────────────────────────

        private const string FirstMeetingKey = "echo_first_meeting";

        /// <summary>True when this save has never met an Echo (plays the intro line once).</summary>
        public static bool NeedsFirstMeeting
        {
            get
            {
                var svc = GameStateService.Instance;
                var s = svc != null ? svc.State : null;
                if (s == null || s.SeenTutorials == null) return false;   // no state -> never force the beat
                return !(s.SeenTutorials.TryGetValue(FirstMeetingKey, out var seen) && seen);
            }
        }

        /// <summary>The authored one-line intro's dialogue id (dialogues.json).</summary>
        public static string FirstMeetingNode => FirstMeetingKey;

        /// <summary>Persist the one-shot flag (GameStateService.MarkTutorialSeen saves).</summary>
        public static void MarkFirstMeetingSeen()
        {
            GameStateService.Instance?.MarkTutorialSeen(FirstMeetingKey);
            FlowTrace.Step("Echo", "First-meeting beat marked seen (one-shot, SeenTutorials).");
        }

    }
}
