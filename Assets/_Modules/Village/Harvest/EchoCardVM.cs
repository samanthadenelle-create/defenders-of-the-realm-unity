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
// WO-1108 (2026-08-16): the WO-811 SIXTH row -- the "Repair structures" TASK chip --
// is RETIRED. Repair is PASSIVE now: every owned Echo mends, driven by roster COUNT
// (EchoBonusCalculator.RepairFractionsPerSecond), so there is no task to pick and no
// per-Echo repair status line. TaskChips() == ResourceChips() (five rows); the
// RepairTaskChip/AssignRepair members are GONE and re-adding either fails the picker
// oracle. Stored "repair:N" tokens read-migrate to Harvest at the Echo's affinity.
//
// STATE line semantics: live from the shared EchoBonusCalculator --
//   harvesting  -> "Gathering Wood - Lv 3 - +65% (best -- this Echo's calling)"
//   idle        -> "Idle - waiting for your word."
//   (WO-1108: there is no "repairing" state line any more -- repair is passive across
//    the whole roster, so it is not a per-Echo assignment and never a card state.)
// Identity (name / element / flavor / portrait) is read from EchoRosterCatalog.ByIndex
// (the six named spirits), NOT hardcoded. ASCII-only separators ('-' not the
// middle-dot) -- glyph-safe on the shipped TMP font; states + resource identity read
// as TEXT, never by color alone (colorblind owner).
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;

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
            if (EchoRepairService.Instance != null) EchoRepairService.Instance.Changed += OnServiceChanged;   // WO-811: honest repair tail re-binds
            EchoAssignments.Changed += OnServiceChanged;
        }

        public void Dispose()
        {
            if (EchoService.Instance != null) EchoService.Instance.Changed -= OnServiceChanged;
            if (EchoRepairService.Instance != null) EchoRepairService.Instance.Changed -= OnServiceChanged;
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

        /// <summary>The live STATE line: gathered resource + current specialization bonus %
        /// (from the shared EchoBonusCalculator), or the idle ask. State carried in TEXT
        /// (colorblind-safe). The % excludes pair synergy (own line) + the hidden tri (never shown).
        /// <para>
        /// ECON-SWEEP 2026-08-16 (defect 4) — THE "Lv N" CHIP IS GONE FROM THIS LINE. No production
        /// code has ever raised an Echo's level: <c>EchoAssignments.SetLevel</c> has zero callers
        /// outside the regression harness, so every Echo in a shipped build is Lv 1 forever. The
        /// level-up feed source is still an UNANSWERED owner pin (WORK_ORDER_738, "Owner pins",
        /// item 2: "What raises an echo's level?"), so inventing one here is a design decision that
        /// is not mine to make. Printing a number that can never move reads as progression the game
        /// does not have. The level DATA is untouched -- <see cref="EchoBonusCalculator"/> still
        /// consumes it and the token still persists it -- so the day a raise path is ruled, the
        /// readout comes back with it. Do not restore this chip before that ruling.
        /// </para></summary>
        public string StateText
        {
            get
            {
                var ro = EchoBonusCalculator.ReadoutFor(EchoIndex);
                if (ro.Lane == LaneType.Idle)
                    return "Idle - waiting for your word.";
                // WO-1108: the LaneType.Repair branch is GONE. Repair stopped being an
                // assignment (every owned Echo mends passively), so ro.Lane can never read
                // Repair -- a stored "repair:N" read-migrates to Harvest at the Echo's
                // affinity. The repair STATUS is no longer a per-Echo state line.
                string what;
                if (ro.Lane == LaneType.Harvest)
                {
                    string token = EchoAssignments.ResourceTokenOf(EchoIndex);
                    string res = EchoAssignments.ResourceLabelFor(token);
                    what = string.IsNullOrEmpty(res) ? "Gathering" : "Gathering " + res;

                    // WO-953 faucet honesty: when the assigned resource's existence gate
                    // is CLOSED (its collector building was never built -- the WO-834
                    // phantom-income gate, surfaced READ-ONLY here), the status says so
                    // in WORDS instead of implying income that is not arriving. The
                    // assignment itself stays valid and starts paying when the building
                    // lands -- exactly the WO-811 honest-status pattern.
                    if (TryGetFaucetNeed(token, out string needsBuilding))
                        return $"{what} - waiting on a {needsBuilding}";   // no "Lv N": see the summary above
                }
                else
                {
                    // Legacy-stored non-harvest lane (no longer pickable) -- still honest.
                    what = EchoAssignments.LabelFor(EchoAssignments.LaneOf(EchoIndex));
                }
                string s = $"{what} - +{Mathf.RoundToInt(ro.BonusPct)}%";   // no "Lv N": see the summary above
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

        /// <summary>The action-row prompt (one ask, one row) -- names the Echo's short name
        /// (the part before the comma) from the roster catalog. WO-811: reworded from the
        /// WO-830 "gather?" ask because the picker now offers gather AND repair.</summary>
        public string AskText
        {
            get
            {
                var entry = EchoRosterCatalog.ByIndex(EchoIndex);
                string name = entry != null ? entry.DisplayName : "this Echo";
                int comma = name.IndexOf(',');
                if (comma > 0) name = name.Substring(0, comma);
                return "What should " + name + " tend to?";
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
                // WO-953 faucet honesty: a resource whose existence gate is CLOSED carries
                // a WORDS cue naming the building that opens it ("NEEDS: Forge"). The chip
                // stays fully tappable -- affinity is a bonus and the gate is a cue, never
                // a lock (owner ruling: "assignment stays allowed"). Order per WO-883:
                // " (now)" stays the LAST token so the selected cue is never split.
                string needsCue = TryGetFaucetNeed(res, out string needsName)
                    ? " - NEEDS: " + needsName : "";
                string label = EchoAssignments.ResourceLabelFor(res)
                             + (preferred ? " - best" : "")
                             + needsCue
                             + (sel ? " (now)" : "");
                string note = "";
                chips[i] = new ResourceChip(res, label, note, sel, preferred);
            }
            return chips;
        }

        /// <summary>The full task-picker row set the View renders. WO-1108: this is now EXACTLY
        /// <see cref="ResourceChips"/> -- the WO-811 "Repair structures" chip is RETIRED because
        /// repair is passive across every owned Echo (there is nothing to pick), so the card
        /// offers five resources and no sixth row. Kept as a distinct method (rather than folded
        /// into ResourceChips) so the View's binding seam and the picker oracle are unchanged.</summary>
        public ResourceChip[] TaskChips()
        {
            return ResourceChips();
        }

        // ── The assign verb (the ONLY mutation this card performs) ─────────────

        /// <summary>Assign this Echo to harvest <paramref name="resourceToken"/> via the
        /// WO-658/830 seam. EchoAssignments traces + persists + raises Changed (card + HUD refresh).</summary>
        public void AssignResource(string resourceToken)
        {
            FlowTrace.Step("Echo", $"Card: harvest-resource requested echo={EchoIndex} resource='{resourceToken}'.");
            EchoAssignments.AssignHarvest(EchoIndex, resourceToken);
        }

        // =====================================================================
        //  WO-953 — faucet honesty (the existence gate surfaced in WORDS)
        // ---------------------------------------------------------------------
        //  Her live defect: echo 1 assigned to iron (Player.log "AssignLane: echo 1
        //  'idle' -> 'iron:1'") while "[Flow:Harvest] existence gate CLOSED for
        //  'forge' ... NEVER BUILT" -- three silent screens between her and the
        //  cause. These helpers SURFACE ResourceBuildingHarvester's gate verdict
        //  (READ-ONLY -- the phantom-income gate itself is correct by design and
        //  untouched) so the picker + status can say "NEEDS: Forge" in words.
        //  All static + data-decidable, so the headless oracle can pin them.
        // =====================================================================

        // Edge-log memory so the cue traces on FLIP only (the harvester's _lastGate
        // pattern) -- ResourceChips() runs per frame while the silo fills.
        private static readonly Dictionary<string, bool> s_lastCueShown =
            new Dictionary<string, bool>(4);

        /// <summary>
        /// The resource-building progression id whose WO-834 existence gate covers a
        /// picker resource token — food→farm, wood→lumbermill, iron→forge. Null for
        /// gold/crystals (no collector building exists for them, so no gate to surface).
        /// </summary>
        public static string FaucetBuildingIdFor(string resourceToken)
        {
            switch (resourceToken)
            {
                case EchoAssignments.ResFood: return ResourceBuildingProgression.FarmId;
                case EchoAssignments.ResWood: return ResourceBuildingProgression.LumbermillId;
                case EchoAssignments.ResIron: return ResourceBuildingProgression.ForgeId;
                default:                      return null;
            }
        }

        /// <summary>
        /// True when <paramref name="resourceToken"/>'s existence gate is CLOSED, with
        /// <paramref name="buildingDisplayName"/> naming the building that opens it
        /// (resolved via <see cref="NeededBuildingDisplayName"/> — QR-5.7 safe).
        /// Reads the same inputs the gate itself reads (live collector registry +
        /// the persisted ever-built ledger) through the PURE
        /// <see cref="ResourceBuildingHarvester.MayHarvest"/> rule — surfacing only,
        /// never deciding. False (no cue) for ungated resources or on any read failure
        /// (Guard'd — a broken read must never slap a false NEEDS on a paying chip).
        /// </summary>
        public static bool TryGetFaucetNeed(string resourceToken, out string buildingDisplayName)
        {
            buildingDisplayName = null;
            string bid = FaucetBuildingIdFor(resourceToken);
            if (string.IsNullOrEmpty(bid)) return false;

            bool closed = Guard.Try("Echo", "read harvest existence gate", () =>
            {
                var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                IReadOnlyList<string> ever = s != null && s.EverBuiltStructureIds != null
                    ? (IReadOnlyList<string>)s.EverBuiltStructureIds
                    : Array.Empty<string>();
                bool live = ResourceCollectorRegistry.Get(bid) != null;
                return !ResourceBuildingHarvester.MayHarvest(
                    ResourceBuildingHarvester.CatalogIdsForBuilding(bid), ever, live);
            }, fallback: false);

            if (closed) buildingDisplayName = NeededBuildingDisplayName(bid);

            // Trace on flip only (never per-frame): the cue appearing/clearing is the
            // player-felt state change a capture needs to show.
            if (!s_lastCueShown.TryGetValue(resourceToken, out bool was) || was != closed)
            {
                s_lastCueShown[resourceToken] = closed;
                FlowTrace.Step("Echo",
                    closed
                        ? $"faucet cue SHOWN for '{resourceToken}': existence gate CLOSED -> 'NEEDS: {buildingDisplayName}' (assignment stays allowed; pays when the building lands)"
                        : $"faucet cue CLEARED for '{resourceToken}': existence gate OPEN.");
            }
            return closed;
        }

        /// <summary>
        /// The PLAYER-FACING name of the building that opens <paramref name="buildingId"/>'s
        /// gate. WARNING - QR-5.7 NAME INVERSION: in canon-strings.json the key 'forge' names the
        /// ARMORER storefront and 'workshop' names "Forge" (the weapons building) — so the
        /// bare 'forge' progression id must NEVER be fed to canon-strings (it would tell
        /// the player to build an armor shop for iron). For iron we resolve the COLLECTOR
        /// card's own catalog displayName ("Forge" on collector_forge — the exact word on
        /// the build-palette card the player must find), falling back to the progression
        /// def. farm/lumbermill resolve via canon-strings (their keys are not inverted),
        /// then the same fallbacks.
        /// </summary>
        public static string NeededBuildingDisplayName(string buildingId)
        {
            // 1. canon-strings — SKIPPED for 'forge' (the QR-5.7 inversion trap).
            if (buildingId != ResourceBuildingProgression.ForgeId)
            {
                string canon = VillageStrings.Canon(buildingId);
                if (!string.IsNullOrEmpty(canon) && !canon.StartsWith("[[missing", StringComparison.Ordinal))
                    return canon;
            }

            // 2. The collector card's live catalog displayName (what the build palette
            //    shows — the word the player can actually go find).
            foreach (var cid in ResourceBuildingHarvester.CatalogIdsForBuilding(buildingId))
            {
                var e = DeNelle.Core.Catalog.CatalogRegistry.Get(cid);
                if (e != null && !string.IsNullOrEmpty(e.displayName)) return e.displayName;
            }

            // 3. The progression def's own display name (catalog cold — headless/boot).
            var def = ResourceBuildingProgression.Find(buildingId);
            if (def != null && !string.IsNullOrEmpty(def.DisplayName)) return def.DisplayName;
            return buildingId;
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
