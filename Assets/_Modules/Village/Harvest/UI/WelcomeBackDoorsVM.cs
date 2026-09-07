// =============================================================================
// WelcomeBackDoorsVM — WO-1408. THE RETURN SCREEN'S NEXT DOOR.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// THE MEASURED DEFECT (Builds/ui-capture/WelcomeBack_2670x1200.png, 2026-09-05
// 00:26; REVIEW_MERGED.md row 7, REVIEW_A E-2, REVIEW_B "the through-line"):
// the away summary reported per-resource rows and a single COLLECT, and COLLECT
// dropped the player on the HUD with nothing ready-looking to tap. The loudest
// thing left on that HUD is the store card, so a returning player's first reason
// to tap was to spend. The return moment is the ONE screen a player reads for
// sure, and it ended in a cul-de-sac.
//
// =============================================================================
//  (!) THIS TYPE DECIDES; THE POPUP ONLY DRAWS.
// =============================================================================
// Every destination decision lives here, as a PURE function of report data plus
// four injected live signals. That is what makes the doors oracle-drivable with
// no canvas, no scene and no PlayMode (WelcomeBackDoorsRegression) — the same
// reason HeartfireCharges keeps "now" as a parameter. The View must never grow a
// second opinion about where a row leads.
//
// =============================================================================
//  (!) A ROW EXISTS ONLY WHEN IT IS TRUE.
// =============================================================================
// There is no empty state, no "nothing finished" row and no disabled door. A
// window with no news produces ZERO rows and COLLECT stands alone. A row the
// player cannot act on is worse than no row: it teaches them the screen lies.
//
// =============================================================================
//  ⛔ THE TICKET'S "START WAVE" DOOR IS NOT BUILT, AND THAT IS DELIBERATE.
// =============================================================================
// WO-1408's sketch reads "Heartfire is full - a wave is ready" with a START WAVE
// door. Measured at source: DeNelle.Core.State.HeartfireCharges' own header says
// Heartfire has "exactly one source - the passage of time - and exactly one sink
// - marching", that "RAID ORDERS is dead" and that "MARCH survives as the verb".
// Heartfire is the RAID charge; it buys no wave, and no wave-start door exists to
// route to. Implementing the sketch literally would have minted a second meaning
// for a charge whose whole design is that it has one. So there is exactly ONE
// ready door — RAID → PanelId.JourneyDeck, the deck whose own subtitle is "Your
// quests, and the camps your army can raid" — and the START WAVE half is raised
// to the lead as a ruling rather than invented here (CLAUDE.md §11B-B).
//
// ASCII only — every string below reaches a mobile font atlas.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>One optional return-screen row: what happened, and the one place it leads.</summary>
    public sealed class WelcomeBackDoorRow
    {
        /// <summary>The left column — WHAT happened ("FINISHED WHILE AWAY", "ATTACKED").</summary>
        public string Label = string.Empty;

        /// <summary>The middle column — the specifics ("Footman x1, Arcane Spire L2").</summary>
        public string Detail = string.Empty;

        /// <summary>The door's face ("MANAGE &gt;"). ASCII '&gt;', never a typographic arrow.</summary>
        public string DoorText = string.Empty;

        /// <summary>Where the door leads. Routed through <see cref="PanelRouter"/>, never a
        /// direct type reference — this file lives in DeNelle.Village and several of these
        /// panels do not.</summary>
        public PanelId Door;

        /// <summary>The tab/subject handed to <c>PanelRouter.Open(id, context)</c>, or empty
        /// for a plain open. Only the strings ManageScreenPanel.Open(string) actually accepts.</summary>
        public string DoorContext = string.Empty;

        /// <summary>Short trace word for this row ("finished" / "attacked").</summary>
        public string TraceKind = string.Empty;
    }

    /// <summary>
    /// The optional rows and the optional "you are ready" line that sit between the away
    /// summary's resource table and its COLLECT button. Built by <see cref="Build"/>.
    /// </summary>
    public sealed class WelcomeBackDoorsVM
    {
        /// <summary>At most this many finished-job names are spelled out in the row detail;
        /// the rest collapse into "+N more" so a long night cannot overrun the row.</summary>
        public const int MaxNamedJobs = 3;

        /// <summary>The rows, in reading order. Never null; empty when nothing is true.</summary>
        public readonly List<WelcomeBackDoorRow> Rows = new List<WelcomeBackDoorRow>();

        /// <summary>The one line above COLLECT, or empty when nothing is ready.</summary>
        public string ReadyLine = string.Empty;

        /// <summary>The second, SMALL door beside COLLECT ("RAID"), or empty when none.</summary>
        public string ReadyDoorText = string.Empty;

        /// <summary>Where the ready door leads. Meaningful only while <see cref="HasReadyDoor"/>.</summary>
        public PanelId ReadyDoor = PanelId.JourneyDeck;

        /// <summary>True when a ready line AND its door were produced.</summary>
        public bool HasReadyDoor => !string.IsNullOrEmpty(ReadyDoorText);

        /// <summary>Trace word for the ready state: "none" or "raid".</summary>
        public string ReadyKind => HasReadyDoor ? "raid" : "none";

        /// <summary>How many rows carry a door (all of them — a row without one is never built).</summary>
        public int RowCount => Rows.Count;

        /// <summary>The ticket's one trace line, emitted once per open by the popup.</summary>
        public string TraceLine
        {
            get
            {
                int finished = 0, attacked = 0;
                for (int i = 0; i < Rows.Count; i++)
                {
                    if (Rows[i] == null) continue;
                    if (Rows[i].TraceKind == "finished") finished++;
                    else if (Rows[i].TraceKind == "attacked") attacked++;
                }
                return "rows finished=" + finished + " attacked=" + attacked + " ready='" + ReadyKind + "'";
            }
        }

        /// <summary>
        /// THE PURE SEAM. Report data in, rows out — no service lookup, no clock, no scene.
        /// </summary>
        /// <param name="result">The away report. Null yields an empty VM, never an exception.</param>
        /// <param name="raidCapable">PostureSignals.RaidCapable — the ONE raid gate. False means
        /// the RAID door would open onto a refusal, so no ready line is produced.</param>
        /// <param name="armyUsed">PostureSignals.ArmyFillUsed.</param>
        /// <param name="armyCap">PostureSignals.ArmyFillCap.</param>
        /// <param name="heartfireLit">PostureSignals.HeartfireLit — no charge, no march.</param>
        /// <param name="heartfireMax">PostureSignals.HeartfireMax.</param>
        public static WelcomeBackDoorsVM Build(OfflineHarvestResult result,
                                               bool raidCapable,
                                               int armyUsed,
                                               int armyCap,
                                               int heartfireLit,
                                               int heartfireMax)
        {
            var vm = new WelcomeBackDoorsVM();
            if (result == null) return vm;

            // ── ROW 1: FINISHED WHILE AWAY → the Manage screen, on the right tab ──
            // The job lines already exist (LANE G); what was missing was the DOOR. The tab
            // comes from the job's own card VERB, which is the shared BuildTimerService.EntryFor
            // vocabulary — never a second mapping table keyed off the label.
            if (result.CompletedJobCount > 0)
            {
                vm.Rows.Add(new WelcomeBackDoorRow
                {
                    Label = "FINISHED WHILE AWAY",
                    Detail = JobDetail(result.CompletedJobs),
                    DoorText = "MANAGE >",
                    Door = PanelId.Manage,
                    DoorContext = ManageTabFor(result.CompletedJobs),
                    TraceKind = "finished",
                });
            }

            // ── ROW 2: ATTACKED → the Defence Report ─────────────────────────────
            if (result.HasAttackNews)
            {
                vm.Rows.Add(new WelcomeBackDoorRow
                {
                    Label = "ATTACKED",
                    Detail = AttackDetail(result.AttackCount, result.AttackBreachName, result.AttackOutcomeWord),
                    DoorText = "REPORT >",
                    Door = PanelId.DefenseReport,
                    DoorContext = string.Empty,   // the panel lists newest-first; no subject to focus
                    TraceKind = "attacked",
                });
            }

            // =====================================================================
            //  !! THE READY LINE AND ITS RAID DOOR ARE RETIRED HERE.
            //     OWNER REVERSAL, 2026-09-07 01:13, on her own frame
            //     (Logs/device/screens/owner-harvest-20260907-011321.png), verbatim:
            //         "no idea why raid is listed here"
            // =====================================================================
            //  WO-1408 built this line + door to close a real cul-de-sac (COLLECT dropped the
            //  player on a HUD whose loudest control was the store card). The reasoning was sound;
            //  the PLACEMENT was wrong. This popup answers ONE question - what happened to my town
            //  while I was gone, and what do I collect - and a RAID invitation beside COLLECT reads
            //  as a second, competing primary action on a screen about harvesting. The owner did
            //  not recognise it as an offer at all; she read it as a stray row.
            //
            //  WHAT SURVIVES, DELIBERATELY: the ATTACKED row above. That row is NOT a raid
            //  invitation - it is the door onto a REPORT of something that already happened to
            //  her town, and it is drawn only when such a report exists. WO-1408's actual
            //  invariant ("a row exists only when it is true") is unchanged.
            //
            //  THE FIELDS ARE KEPT AND ALWAYS EMPTY, not deleted: WelcomeBackPopup.AddReadyBand
            //  reads HasReadyDoor and draws nothing when it is false, and the regression asserts
            //  the ABSENCE (case 3 [raid-door-retired]) rather than the shape. The four posture
            //  parameters are likewise kept - deleting them would change every call site and every
            //  fixture for a ruling that could be reversed again, and the ONE gate that used them
            //  is recorded here in prose so restoring it is a three-line edit, not an excavation:
            //      raidCapable && armyCap > 0 && armyUsed > 0 && heartfireLit > 0
            //          -> "Army <used> / <cap> ready[ - Heartfire <lit> / <max> lit] - a camp awaits"
            //          -> door "RAID" onto PanelId.JourneyDeck.
            //  Recorded as an owner reversal in WORK_ORDER_1408's RESULT.
            _ = raidCapable; _ = armyUsed; _ = armyCap; _ = heartfireLit; _ = heartfireMax;

            return vm;
        }

        /// <summary>
        /// "Footman x1, Arcane Spire L2" — the job LABELS, in the order they landed, capped at
        /// <see cref="MaxNamedJobs"/> with a "+N more" tail. The labels are the shared card seam's
        /// own words (OfflineJobLine.Label), so the row says what the queue card said.
        /// </summary>
        public static string JobDetail(IReadOnlyList<OfflineHarvestResult.OfflineJobLine> jobs)
        {
            if (jobs == null || jobs.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            int named = 0;
            for (int i = 0; i < jobs.Count && named < MaxNamedJobs; i++)
            {
                var j = jobs[i];
                if (j == null) continue;
                string label = string.IsNullOrEmpty(j.Label) ? "Job" : j.Label;
                if (named > 0) sb.Append(", ");
                sb.Append(label);
                named++;
            }
            int rest = jobs.Count - named;
            if (rest > 0) sb.Append(", +").Append(rest).Append(" more");
            return sb.ToString();
        }

        /// <summary>
        /// "1x - North Gate breached" / "2x - held". The breach NAME is preferred because it is
        /// the actionable half ("they came from the north"); the verdict word is the fallback when
        /// the crossing was open ground rather than a named gate.
        /// </summary>
        public static string AttackDetail(int count, string breachName, string outcomeWord)
        {
            if (count <= 0) return string.Empty;
            var sb = new StringBuilder();
            sb.Append(count).Append('x');
            if (!string.IsNullOrEmpty(breachName)) sb.Append(" - ").Append(breachName).Append(" breached");
            else if (!string.IsNullOrEmpty(outcomeWord)) sb.Append(" - ").Append(outcomeWord.ToLowerInvariant());
            return sb.ToString();
        }

        /// <summary>
        /// Which Manage tab the door lands on, from the jobs' own card VERBs.
        /// <para>⚠ THE STRINGS ARE NOT FREE. ManageScreenPanel.Open(string) accepts exactly
        /// "Defense", "Buildings", "Research" and "Troops[:id]" and IGNORES anything else (it
        /// then sits on the launcher, which is a door that half-opens). These three are the ones
        /// this row can produce.</para>
        /// <para>A TRAIN job wins over a build when both finished: troops are the thing the
        /// player came back to send somewhere, and the mixed case needs one answer, not a list.</para>
        /// </summary>
        public static string ManageTabFor(IReadOnlyList<OfflineHarvestResult.OfflineJobLine> jobs)
        {
            if (jobs == null || jobs.Count == 0) return "Buildings";
            bool research = false;
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (j == null || string.IsNullOrEmpty(j.Verb)) continue;
                string verb = j.Verb.ToUpperInvariant();
                if (verb.Contains("TRAIN")) return "Troops";
                if (verb.Contains("RESEARCH")) research = true;
            }
            return research ? "Research" : "Buildings";
        }
    }
}
