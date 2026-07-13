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
// STATE line semantics (WO-681 spec 1): live from the workforce --
//   assigned lane -> "Gathering wood - +N/min"   (per-echo share of the pooled
//                    EchoService.RatePerSecond; accrual math itself untouched)
//   idle          -> "Idle - waiting for your word."
// ASCII-only separators ('-' not the middle-dot) -- glyph-safe on the shipped TMP
// font; states read as TEXT, never by color alone (colorblind owner).
// =============================================================================
using System;
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
        /// <summary>One pickable gather lane for the "What should you gather?" row.</summary>
        public readonly struct LaneChip
        {
            public readonly string Id;        // "wood" / "iron" / "food"
            public readonly string Label;     // display text (selected state appended as TEXT)
            public readonly bool Selected;    // this echo currently gathers this lane
            public LaneChip(string id, string label, bool selected)
            {
                Id = id; Label = label; Selected = selected;
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

        /// <summary>Card header name, e.g. "Echo 2 of 4 - Spirit of the Tree".</summary>
        public string NameText
        {
            get
            {
                var svc = EchoService.Instance;
                int count = svc != null ? svc.EchoCount : 1;
                return $"Echo {EchoIndex + 1} of {count} - Spirit of the Tree";
            }
        }

        /// <summary>The WHAT line (WO-681 spec 1; final copy = owner pass, kept diegetic + ASCII).</summary>
        public string WhatText =>
            "An Echo - a spirit of the Tree. It gathers for Elarion while you fight, even while you're away.";

        /// <summary>The live STATE line: gathering lane + per-echo rate, or the idle ask.</summary>
        public string StateText
        {
            get
            {
                string lane = EchoAssignments.LaneOf(EchoIndex);
                if (lane == EchoAssignments.LaneIdle)
                    return "Idle - waiting for your word.";
                double perMin = PerEchoRatePerMinute();
                return $"Gathering {lane} - +{perMin:0.#}/min";
            }
        }

        /// <summary>The action-row prompt (one ask, one row).</summary>
        public string AskText => "What should you gather?";

        /// <summary>Resources path of the portrait sprite for the medallion/portrait socket
        /// (sprite-first, null-fallback at the View -- the Echo shares the Hollow's art).</summary>
        public string PortraitResourcePath => "Portraits/pet-house";

        /// <summary>The pickable lane chips (wood/iron/food) with the selected state AS TEXT.</summary>
        public LaneChip[] LaneChips()
        {
            string current = EchoAssignments.LaneOf(EchoIndex);
            var lanes = EchoAssignments.Lanes;
            var chips = new LaneChip[lanes.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                bool sel = lanes[i] == current;
                string label = EchoAssignments.LabelFor(lanes[i]) + (sel ? " (now)" : "");
                chips[i] = new LaneChip(lanes[i], label, sel);
            }
            return chips;
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

        // ── Internals ───────────────────────────────────────────────────────────

        /// <summary>This Echo's share of the pooled rate, per minute (display only --
        /// the pooled accrual itself is untouched, WO-681 "what NOT to touch").</summary>
        private double PerEchoRatePerMinute()
        {
            var svc = EchoService.Instance;
            if (svc == null) return 0.0;
            int count = Math.Max(1, svc.EchoCount);
            return svc.RatePerSecond / count * 60.0;
        }
    }
}
