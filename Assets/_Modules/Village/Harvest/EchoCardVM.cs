// =============================================================================
// EchoCardVM -- view-model for the Echo select card (MVVM strict; WO-681 card,
// WO-830 per-Echo harvest RESOURCE PICKER).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The VM owns ALL EchoService / EchoAssignments / GameState reads and the assign
// verb; the View (EchoCardView) is a dumb skin built through ElarionUiKit that
// binds these strings and calls AssignResource -- it never touches a service
// (SESSION_CANON_LOADER "MVVM strict"; ARCHITECTURE_PRINCIPLES SS2 presentation
// never touches the objects).
//
// WO-830 (owner ruling 2026-08-02): the card's PRIMARY interaction is a per-Echo
// RESOURCE PICKER -- Wood/Iron/Food/Gold/Crystals. The Echo's AFFINITY is a match
// BONUS (flagged " - best" IN the matching chip's LABEL since WO-883; it used to be a
// second text band under the chip, which duplicated the footer and was the row the
// picker's scroll fold cut in half), never a lock. The full "(best -- this Echo's
// calling)" phrasing survives in StateText, which is the footer's own line.
// The DISCLOSED pair-synergy status renders as its own line (SynergyText);
// the hidden tri-synergy is NEVER represented in any string here (Sec.3d).
// The dead Crafting chip is REMOVED (Sec.3e default); Defense/Exploration stay
// hidden (owner ruling 2026-07-24).
//
// STATE line semantics: live from the shared EchoBonusCalculator --
//   harvesting  -> "Gathering Wood - Lv 3 - +65% (best -- this Echo's calling)"
//   idle        -> "Idle - waiting for your word."
// Identity (name / element / flavor / portrait) is read from EchoRosterCatalog.ByIndex
// (the six named spirits), NOT hardcoded. ASCII-only separators ('-' not the
// middle-dot) -- glyph-safe on the shipped TMP font; states + resource identity read
// as TEXT, never by color alone (colorblind owner).
// =============================================================================
using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// View-model for the Echo select card (WO-681/830). One instance per open card,
    /// bound to a specific echo index. Owns every service read; raises
    /// <see cref="Changed"/> when the underlying workforce state moves.
    /// </summary>
    public sealed class EchoCardVM : IDisposable
    {
        /// <summary>One pickable HARVEST RESOURCE for the "What should this Echo gather?"
        /// picker (WO-830). Selected/affinity state is carried in TEXT, never hue
        /// (colorblind owner). Id is the persisted resource token ("wood".."crystals").</summary>
        public readonly struct ResourceChip
        {
            public readonly string Id;        // resource token ("wood"/"iron"/"food"/"gold"/"crystals")
            public readonly string Label;     // resource name (+ " - best" affinity cue, + " (now)" when selected -- TEXT, never hue)
            public readonly string Note;      // WO-883: RETIRED, always "" (the View still supports a note band; nothing feeds it)
            public readonly bool Selected;    // this echo currently gathers this resource
            public readonly bool Preferred;   // this resource is the echo's AFFINITY (its match bonus lands here)
            public ResourceChip(string id, string label, string note, bool selected, bool preferred)
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

        /// <summary>Card header name, e.g. "Echo 2 of 4 - Elowen, the Nature Echo" -- the REAL
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

        /// <summary>The element subtitle for this Echo ("Essence of a grove-warden"), from the roster catalog.</summary>
        public string ElementText
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                return entry != null ? entry.Element : "";
            }
        }

        /// <summary>Short WHAT line under the name -- Element + the Echo's AFFINITY ("Favors: Gold",
        /// WO-830 -- the calling is disclosed so the picker choice is informed). ASCII, single line.</summary>
        public string WhatText
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                if (entry == null) return "A spirit of Elarion -- gathers while you fight.";
                string favors = "Favors: " + EchoRosterCatalog.TargetLabel(entry.Affinity);
                return string.IsNullOrEmpty(entry.Element) ? favors : entry.Element + " - " + favors;
            }
        }

        /// <summary>The live STATE line: gathered resource + level + current specialization bonus %
        /// (from the shared EchoBonusCalculator), or the idle ask. State carried in TEXT
        /// (colorblind-safe). The % excludes pair synergy (own line) + the hidden tri (never shown).</summary>
        public string StateText
        {
            get
            {
                var ro = EchoBonusCalculator.ReadoutFor(EchoIndex);
                if (ro.Lane == LaneType.Idle)
                    return "Idle - waiting for your word.";
                string what;
                if (ro.Lane == LaneType.Harvest)
                {
                    string res = EchoAssignments.ResourceLabelFor(EchoAssignments.ResourceTokenOf(EchoIndex));
                    what = string.IsNullOrEmpty(res) ? "Gathering" : "Gathering " + res;
                }
                else
                {
                    // Legacy-stored non-harvest lane (no longer pickable) -- still honest.
                    what = EchoAssignments.LabelFor(EchoAssignments.LaneOf(EchoIndex));
                }
                string s = $"{what} - Lv {ro.Level} - +{Mathf.RoundToInt(ro.BonusPct)}%";
                if (ro.PreferredMatch) s += " (best -- this Echo's calling)";
                return s;
            }
        }

        /// <summary>WO-830: the DISCLOSED pair-synergy line. Active: names the pair + partner +
        /// bonus; inactive: the plain-text recipe to activate it. "" when no pair is defined.
        /// The hidden tri-synergy is NEVER mentioned here or anywhere (Sec.3d).</summary>
        public string SynergyText
        {
            get
            {
                var sy = EchoBonusCalculator.SynergyFor(EchoIndex);
                if (!sy.HasPair) return "";
                string pair = string.IsNullOrEmpty(sy.PairName) ? "Synergy" : sy.PairName + " synergy";
                if (sy.Active)
                    return $"{pair} with {sy.PartnerName}: ACTIVE (+{Mathf.RoundToInt(sy.BonusPct)}% all harvest)";
                string hint = string.IsNullOrEmpty(sy.PartnerResourceLabel)
                    ? sy.PartnerName
                    : $"{sy.PartnerName} ({sy.PartnerResourceLabel})";
                return $"{pair}: pair with {hint} to activate";
            }
        }

        /// <summary>The action-row prompt (one ask, one row) -- the WO-830 resource-picker ask,
        /// naming the Echo's short name (the part before the comma) from the roster catalog.</summary>
        public string AskText
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                string name = entry != null ? entry.DisplayName : "this Echo";
                int comma = name.IndexOf(',');
                if (comma > 0) name = name.Substring(0, comma);
                return "What should " + name + " gather?";
            }
        }

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

        /// <summary>The five live resource chips (WO-830 -- EchoAssignments.PickableResources).
        /// Selected state and the affinity "best" tag are carried AS TEXT (never hue) so the
        /// picker never misleads a colorblind player. Affinity is a bonus, never a lock: every
        /// chip is always tappable. WO-883: both cues live in <see cref="ResourceChip.Label"/>
        /// -- <see cref="ResourceChip.Note"/> is now always "" (see the body for why).</summary>
        public ResourceChip[] ResourceChips()
        {
            string current = EchoAssignments.ResourceTokenOf(EchoIndex);
            var entry = EchoRosterCatalog.ByIndex(EchoIndex);
            string affinityToken = entry != null ? EchoRosterCatalog.TargetToken(entry.Affinity) : "";
            var resources = EchoAssignments.PickableResources;
            var chips = new ResourceChip[resources.Length];
            for (int i = 0; i < resources.Length; i++)
            {
                string res = resources[i];
                bool sel = res == current;
                bool preferred = res == affinityToken;
                // WO-883: the affinity cue rides IN THE LABEL now; the separate per-chip note
                // is retired. It was the VERBATIM tail of StateText, so the footer repeated it
                // two lines down ("Gathering Food - Lv 1 - +5% (best -- this Echo's calling)"),
                // and its extra 39.5px band made ONE row taller than the other four -- which is
                // the row the picker's scroll fold sliced through mid-sentence on the owner's
                // 2026-08-04 capture (docs/ui-review/screens-2026-08-04/EchoCard_2340x1080.png).
                // With every row the same height the fold cuts a BUTTON, which reads as "scroll
                // me" rather than as broken text. Order matters: " (now)" stays the LAST token
                // so the selected cue is never split by the affinity cue. Both are TEXT, never
                // hue (colorblind owner), and both are ASCII.
                string label = EchoAssignments.ResourceLabelFor(res)
                             + (preferred ? " - best" : "")
                             + (sel ? " (now)" : "");
                string note = "";
                chips[i] = new ResourceChip(res, label, note, sel, preferred);
            }
            return chips;
        }

        // ── The assign verb (the ONLY mutation this card performs) ─────────────

        /// <summary>Assign this Echo to harvest <paramref name="resourceToken"/> via the
        /// WO-658/830 seam. EchoAssignments traces + persists + raises Changed (card + HUD refresh).</summary>
        public void AssignResource(string resourceToken)
        {
            FlowTrace.Step("Echo", $"Card: harvest-resource requested echo={EchoIndex} resource='{resourceToken}'.");
            EchoAssignments.AssignHarvest(EchoIndex, resourceToken);
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
