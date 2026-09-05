// =============================================================================
// OfflineHarvestResult — the per-resource haul accrued while the player was away
// (WO-115). A plain value carrier raised to the welcome-back popup; it never
// touches GameState itself (the service banks; this only reports what it banked).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Resource buckets map 1:1 to MineNode.MineResource / the GameState wallet fields
// (Iron / Wood / Food / AetherCrystals) so the popup can render +N rows without
// re-deriving anything. Pet harvest (WO-111 Phase 4) folds into the same buckets
// when it lands — no shape change needed.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>
    /// The result of an offline-accrual claim: how much of each resource was
    /// banked, how long the player was away, and whether the cap clipped it.
    /// </summary>
    public sealed class OfflineHarvestResult
    {
        /// <summary>Iron banked this claim.</summary>
        public int Iron;
        /// <summary>Wood banked this claim.</summary>
        public int Wood;
        /// <summary>Food banked this claim (the retired "Stone" axis, repurposed — DEF-121).</summary>
        public int Food;
        /// <summary>Aether Crystals banked this claim.</summary>
        public int AetherCrystals;

        /// <summary>Real seconds since the last claim (BEFORE the cap clamp) — for the away-time line.</summary>
        public double AwaySeconds;
        /// <summary>True when <see cref="AwaySeconds"/> exceeded the offline cap (the gentle nudge).</summary>
        public bool WasCapped;

        // =====================================================================
        //  WO-1128 — WHICH CLOCK PRODUCED THIS WINDOW (the reconciliation half)
        // ---------------------------------------------------------------------
        //  The device clock cannot be verified and we do not try (WO-1128 §1).
        //  What we CAN do is record, per window, whether "now" came from the
        //  monotonic server anchor (ServerClock, unforgeable by a wall-clock edit)
        //  or from the raw device clock, and carry the window's own endpoints so
        //  the server can compare the client's DECLARED window against its OWN
        //  elapsed time on the next round trip (api/game/save.js §RECONCILE).
        //
        //  These fields are DIAGNOSTIC + DECLARATIVE, never punitive. Nothing in
        //  the client reduces a haul because the clock was unanchored — a cold
        //  launch is ALWAYS unanchored (Stopwatch dies with the process), and a
        //  player on a plane is not a cheater. Refuse server-side, never punish
        //  client-side.
        // =====================================================================

        /// <summary>
        /// True when <c>TimeSource.NowUnixMs()</c> was server-anchored for THIS claim
        /// (<see cref="DeNelle.Core.State.ServerClock.IsTrusted"/>). False on any cold
        /// launch before the first backend round trip — an expected, honest state.
        /// </summary>
        public bool ServerAnchored;

        /// <summary>Unix-ms this window started at (the persisted claim clock, or the pause edge).</summary>
        public double WindowStartUnixMs;

        /// <summary>Unix-ms "now" that closed this window — the value the clock advanced to.</summary>
        public double NowUnixMs;

        /// <summary>
        /// True when this haul rests on an unverifiable device clock and is therefore
        /// subject to server reconciliation on the next sync. Display/telemetry only.
        /// </summary>
        public bool IsProvisional => !ServerAnchored;

        /// <summary>Short trace/telemetry name of the clock this window trusted.</summary>
        public string ClockSource => ServerAnchored ? "server-anchored" : "device";

        /// <summary>Total units banked across every resource (popup-trigger gate: show only when &gt; 0).</summary>
        public int Total => Iron + Wood + Food + AetherCrystals;

        // =====================================================================
        //  WO-1231 — WHAT PASSIVE ECHO MENDING DID WITH THE SAME WINDOW
        // ---------------------------------------------------------------------
        //  Offline mending is not a haul, it is a SPEND: EchoRepairService banks an
        //  'echo-repair' share of the away window and completes real repairs, paying
        //  Wood and Iron out of the wallet. So a player could return to materials
        //  already gone with no report that it happened — the away summary was, by
        //  construction, only ever telling them half the story.
        //
        //  ⚠ THIS FIELD IS WHY THE POPUP'S TRIGGER GATE MOVED. `Total > 0` alone means
        //  a window in which mending spent 400 Wood and gathered nothing shows NO
        //  summary at all — the exact case where the player most needs one. The gate
        //  became "haul OR mend" here, and then (LANE G, 2026-09-04) moved onto THIS
        //  type as HasSummaryContent with FOUR axes: haul OR mend OR a finished queue
        //  job OR resources waiting in a collector. Neither the service nor the popup
        //  re-derives it any more — see the LANE G block below.
        // =====================================================================

        /// <summary>
        /// Passive Echo mending's share of the SAME away window (never null in practice —
        /// OfflineHarvestService attaches the live report once every consumer has applied).
        /// </summary>
        public EchoMendReport Mend;

        /// <summary>True when mending did something the player must be told about — it
        /// mended, it spent, or it stalled broke.</summary>
        public bool HasMendNews => Mend != null && Mend.HasContent;

        // =====================================================================
        //  LANE G (2026-09-04) — WHAT THE QUEUE FINISHED, WHAT THE COLLECTORS HOLD,
        //  AND THE ONE GATE THAT DECIDES WHETHER THE RETURNING PLAYER IS TOLD
        // ---------------------------------------------------------------------
        //  The economy map (docs/PROGRAM_RAID_ECONOMY_2026-09-04.md sec.7) opens the
        //  ideal returning session on two beats: "BUILD COMPLETE -> collect" and
        //  "Resources full -> collect". Measured at source before this change, the
        //  away summary could say neither. Its gate was the two-term expression
        //  `Total <= 0 && !HasMendNews` — written TWICE, once in
        //  OfflineHarvestService.OnClaimCompleted and once in WelcomeBackPopup.Show —
        //  so a player whose nodes were idle, whose Echoes were quiet, whose three
        //  overnight builds had finished and whose farm was sitting full got NO SCREEN
        //  AT ALL. A collector-only town scored zero on both terms.
        //
        //  ⚠ HasSummaryContent IS THE ONE GATE. It lives on the result so the service
        //  and the popup cannot disagree about what counts as news. Do not re-derive
        //  it at a call site — a second copy of the gate is the defect this block fixes
        //  (pinned by Editor/Regression/AwaySummaryReportRegression.cs, cases 1-4).
        //
        //  These are REPORT fields, never a second wallet route: the service records a
        //  finished job (it never completes or re-applies one) and READS a collector's
        //  pending (it never banks it — the COLLECT button carries the tap to the
        //  existing CollectorStatusGate.RequestCollectAll).
        // =====================================================================

        /// <summary>
        /// One queue job that finished inside the away window. Verb + Label come from the
        /// SHARED card seam (BuildTimerService.EntryFor) so the summary says the same words
        /// the queue card said while the job was running — never a second vocabulary.
        /// </summary>
        public sealed class OfflineJobLine
        {
            /// <summary>The card verb ("BUILD", "UPGRADE", "TRAIN", ...).</summary>
            public string Verb;
            /// <summary>Player-facing job name ("Barracks").</summary>
            public string Label;
            /// <summary>Unix-ms the job finished — window membership is tested on this, not on arrival order.</summary>
            public double FinishedUnixMs;
        }

        /// <summary>Queue jobs that finished inside THIS window, oldest first. Never null.</summary>
        public readonly List<OfflineJobLine> CompletedJobs = new List<OfflineJobLine>();

        /// <summary>How many queue jobs finished inside this window.</summary>
        public int CompletedJobCount => CompletedJobs.Count;

        /// <summary>
        /// What the collectors hold, grouped BY RESOURCE (owner felt-test rulings 2026-09-04
        /// 22:30: "the collectors need to be seperated" then "Wood Iron Stone different rows").
        /// One line per resource with a non-zero pending, in the HUD rail's fixed order
        /// (Wood, Iron, Stone, Crystals) — never one aggregate line, never one row per building.
        /// </summary>
        public sealed class OfflineCollectorLine
        {
            /// <summary>The game's canon resource word from ResourceBuildingProgression.LabelFor
            /// ("Wood" / "Iron" / "Stone" / "Crystals") — the same word the HUD rail says.</summary>
            public string Resource;
            /// <summary>Whole units held across every collector of this resource — reported, not banked.</summary>
            public int Pending;
            /// <summary>How many collectors of this resource are holding something.</summary>
            public int Collectors;
        }

        /// <summary>Per-resource lines in rail order, filled by OfflineHarvestService.AttachPendingCollectors.
        /// Never null. <see cref="PendingCollectorTotal"/> / <see cref="PendingCollectorCount"/> stay the
        /// SUMS over this list so the gate (<see cref="HasCollectorNews"/>) does not change shape.</summary>
        public readonly List<OfflineCollectorLine> PendingCollectors = new List<OfflineCollectorLine>();

        /// <summary>Units STILL HELD across every collector at reveal time — reported, not banked.</summary>
        public int PendingCollectorTotal;

        /// <summary>How many collectors are holding something (the row's singular/plural).</summary>
        public int PendingCollectorCount;

        /// <summary>True when at least one queue job finished inside this window.</summary>
        public bool HasJobNews => CompletedJobs.Count > 0;

        /// <summary>True when a collector is holding something the player could collect.</summary>
        public bool HasCollectorNews => PendingCollectorTotal > 0;

        /// <summary>
        /// THE ONE REVEAL GATE — haul OR mend OR a finished job OR resources waiting in a
        /// collector. Both OfflineHarvestService.OnClaimCompleted and WelcomeBackPopup.Show
        /// read this and nothing else.
        /// </summary>
        public bool HasSummaryContent => Total > 0 || HasMendNews || HasJobNews || HasCollectorNews;

        /// <summary>A zero haul — nothing accrued, no popup.</summary>
        public static OfflineHarvestResult None => new OfflineHarvestResult();

        /// <summary>Add an integer amount to the bucket for <paramref name="resource"/>.</summary>
        public void Add(MineResource resource, int amount)
        {
            if (amount <= 0) return;
            switch (resource)
            {
                case MineResource.Iron:          Iron += amount;           break;
                case MineResource.Wood:          Wood += amount;           break;
                case MineResource.Food:          Food += amount;          break;
                case MineResource.AetherCrystal: AetherCrystals += amount; break;
            }
        }
    }
}
