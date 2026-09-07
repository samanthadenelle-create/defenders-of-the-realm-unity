// =============================================================================
// ArmyMusterService — "Muster army": ONE action that enqueues EVERY troop in an
// ArmyComposition onto the EXISTING Obsidian TRAIN channel (WO-897).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// NO SECOND QUEUE. This is a BATCH ENQUEUE over the sanctioned single-troop train
// path — BarracksService.EnqueueTraining, which honours the
// army cap (incl. in-flight jobs) and calls BuildTimerService.Enqueue(JobKind.TrainTroop,
// ...) on ChannelId.Train. The muster adds exactly one thing on top: the QUEUE-DEPTH
// budget (owner ruling WO-911 Q4 — five items total per line) and a REPORT naming what
// did and did not fit.
//
// ⚠ NOTHING ON THIS PATH SPENDS ANYTHING (WO-1387 for the seam, WO-1586 for the
// projection). Training is priced in TIME; gold appears ONLY on the skip verb
// (BuildTimerService.TryInstantFinish / HIRE REINFORCEMENTS). Header comments here used
// to say the enqueue "spends the ledger cost" long after it stopped doing so — that is
// exactly the "comments lie" trap MASTER_CATALOG is built around, and it is what let a
// gold projection sit in Preview() unnoticed until the owner hit it on 2026-09-07.
//
// NO SILENT TRUNCATION (CLAUDE.md §12). Every unit that is not queued is counted,
// attributed to a named blocker, surfaced as ASCII TEXT (never colour — the owner is
// red/green colourblind) and logged via FlowTrace.Warn. Guard.Try wraps each row so one
// bad composition entry can never abort the whole muster.
//
// COLLAPSE (owner ruling WO-911 Q12): identical PENDING troop trains collapse into one
// xN card at PUBLISH time (BuildTimerService's status publisher), keyed by troop id. The
// muster mints one job per unit with a UNIQUE job id (BarracksService.TrainPrefix +
// troopId + ":" + uid), which is exactly the shape that collapses on the card AND stays
// individually cancellable by id — cancel is keyed by structure id, not index.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Jobs;
using DeNelle.Core.Diagnostics;
using Ledger = DeNelle.Village.Buildings.Progression;   // the GameState-backed wallet (see BarracksService)

namespace DeNelle.Village
{
    /// <summary>What happened to ONE composition row when the army was mustered.</summary>
    public struct MusterRowOutcome
    {
        /// <summary>The TroopDef id this row asked for.</summary>
        public string TroopId;
        /// <summary>Player-facing troop name (falls back to the id).</summary>
        public string DisplayName;
        /// <summary>Units the row asked for.</summary>
        public int Requested;
        /// <summary>Units actually enqueued onto the Train channel.</summary>
        public int Queued;
        /// <summary>Units that did NOT make it (Requested - Queued). Never silently dropped.</summary>
        public int NotQueued => Requested - Queued > 0 ? Requested - Queued : 0;
        /// <summary>ASCII sentence naming the blocker; null when the row queued in full.</summary>
        public string Reason;
    }

    /// <summary>
    /// The result of a muster: per-row outcomes plus a player-facing ASCII summary. Read
    /// <see cref="Headline"/> + <see cref="Detail"/> straight into the UI — the counts are in the
    /// TEXT, so the "3 of 5 queued, 2 did not fit" tell never depends on a colour.
    /// </summary>
    public sealed class MusterReport
    {
        /// <summary>Per-row outcomes, in composition order. Never null.</summary>
        public List<MusterRowOutcome> Rows = new List<MusterRowOutcome>();
        /// <summary>Total units the composition asked for.</summary>
        public int TotalRequested;
        /// <summary>Total units enqueued onto the Train channel.</summary>
        public int TotalQueued;
        /// <summary>Total units that did not fit. &gt; 0 means <see cref="Detail"/> names every one.</summary>
        public int TotalNotQueued => TotalRequested - TotalQueued > 0 ? TotalRequested - TotalQueued : 0;
        /// <summary>True when at least one unit reached the queue.</summary>
        public bool AnyQueued => TotalQueued > 0;
        /// <summary>True when the whole composition landed.</summary>
        public bool Complete => TotalRequested > 0 && TotalQueued == TotalRequested;

        /// <summary>ASCII one-liner: "Queued 3 of 5 - 2 did not fit."</summary>
        public string Headline = "";
        /// <summary>ASCII per-row shortfall lines: "2x Archer - training queue is full (5 max)."</summary>
        public string Detail = "";

        /// <summary>Headline + detail, one string, for a toast / log line.</summary>
        public string Summary => string.IsNullOrEmpty(Detail) ? Headline : Headline + " " + Detail;
    }

    /// <summary>
    /// Display projection of a composition BEFORE mustering: total cost, total time
    /// (parallel-aware over the Train channel's slots) and how much of it fits in the
    /// queue line right now.
    /// </summary>
    public struct MusterPreview
    {
        /// <summary>Units the composition asks for.</summary>
        public int TotalUnits;
        /// <summary>
        /// Summed resource cost of every unit. WO-1387/WO-1586: this is ALWAYS ZERO - training is
        /// priced in TIME and nothing else, so it prints "Free". The field stays because
        /// <see cref="ArmyCost"/> is the panel's cost grammar and a future non-gold price (should the
        /// owner ever rule one) has an obvious home; nothing may write a gold term into it here.
        /// </summary>
        public ArmyCost Cost;
        /// <summary>Wall-clock seconds for the batch across the Train channel's parallel slots.</summary>
        public double TotalSeconds;
        /// <summary>Train-channel worker slots the estimate assumed.</summary>
        public int TrainSlots;
        /// <summary>Items already in the Train line (active + waiting).</summary>
        public int LineDepth;
        /// <summary>Free places left in the Train line (cap - depth).</summary>
        public int LineRoom;
        /// <summary>Units that would fit in the line right now (min of TotalUnits and LineRoom).</summary>
        public int WouldFit;
        /// <summary>Units that would NOT fit right now. &gt; 0 = the panel must say so in TEXT.</summary>
        public int WouldNotFit => TotalUnits - WouldFit > 0 ? TotalUnits - WouldFit : 0;
        /// <summary>
        /// WO-1586: "the plan FITS", never "you have the gold". Training costs TIME only
        /// (WO-1387), so the only things that can refuse a plan are the ARMY CAP and the
        /// TRAIN LINE. A wallet reading has no vote here.
        /// </summary>
        public bool Affordable;
        /// <summary>ASCII name of what the plan does not fit into ("Army room", "Queue space");
        /// empty when it fits. NEVER "Gold" - see <see cref="Affordable"/>.</summary>
        public string ShortOf;
        /// <summary>Army slots the staged plan totals (troop Slots x count), owned units included.</summary>
        public int PlanSlots;
        /// <summary>Army slots the plan asks for that are NOT already filled by owned/in-flight troops.
        /// A READING for the trace, NOT the cap input: Muster() enqueues every staged unit, so the cap
        /// is judged on <see cref="PlanSlots"/>. Read this to tell a rebalance from a recruitment drive.</summary>
        public int NewArmySlots;
        /// <summary>Army slots still free right now (cap - roster - in-flight training).</summary>
        public int ArmyRoom;
        /// <summary>Units of the plan already covered by troops the player OWNS (roster + in-flight).
        /// Informational: staging these costs nothing, which is the WO-1586 rebalance case.</summary>
        public int AlreadyOwned;
        /// <summary>Units of the plan that would have to be TRAINED (the ones the time estimate is for).</summary>
        public int ToTrain;
    }

    /// <summary>
    /// Turns an <see cref="ArmyComposition"/> into Train-channel jobs in one action, and projects
    /// its cost/time for the panel. Reuses <see cref="BarracksService.EnqueueTraining(string,int,out string)"/>
    /// — it never forks the enqueue or the army-cap rule. There is no spend to fork (WO-1387).
    /// </summary>
    public static class ArmyMusterService
    {
        /// <summary>Raised after a muster changed the queue, so a panel can repaint.</summary>
        public static event Action Mustered;

        // ── Queue-line reads (read-only; the muster never mutates the engine) ──

        /// <summary>
        /// Items currently IN the Train line = running jobs + waiting jobs. This is queue DEPTH
        /// (the Q4 axis), NOT the slot/concurrency count (WO-911 §2d — do not conflate them).
        /// 0 with no queue service.
        /// </summary>
        public static int TrainLineDepth()
        {
            var queue = BuildTimerService.Instance;
            if (queue == null) return 0;
            int active = queue.ActiveJobsOf(ChannelId.Train)?.Count ?? 0;
            int pending = queue.PendingJobsOf(ChannelId.Train)?.Count ?? 0;
            return active + pending;
        }

        /// <summary>Free places left in the Train line under the five-per-line cap.</summary>
        public static int TrainLineRoom() =>
            ArmyMusterPlanner.RoomInLine(TrainLineDepth(), ArmyMusterPlanner.TrainQueueDepthCap);

        /// <summary>Parallel worker slots on the Train channel (drives the parallel-aware time estimate).</summary>
        public static int TrainSlots()
        {
            var queue = BuildTimerService.Instance;
            int slots = queue != null ? queue.SlotCount(ChannelId.Train) : 1;
            return slots < 1 ? 1 : slots;
        }

        // ── Preview (what the footer shows before you press Muster) ───────────

        /// <summary>
        /// Projects <paramref name="comp"/>: total cost, parallel-aware total time, and how much of
        /// it fits in the Train line right now. Never mutates anything. Unlocked-only — a locked
        /// troop row contributes nothing (the panel only OFFERS unlocked troops, WO-897 AC 3).
        /// </summary>
        /// <summary>
        /// The three wallet balances the Armies panel shows, read HERE rather than in the view.
        /// -------------------------------------------------------------------------------
        /// UI-MVVM conformance: a View must not reach into game state itself. ArmyMusterPanel was
        /// calling ResourceLedger.Balance directly for its footer chips, which the conformance
        /// oracle flagged. Routing it through the service keeps ONE reader of the ledger on this
        /// path - the same one Preview() already uses for affordability - so the footer can never
        /// disagree with the CTA about whether you can pay.
        /// </summary>
        public static int GoldBalance()
        {
            var state = DeNelle.Core.State.GameStateService.Instance?.State;
            return state?.Resources.Coins ?? 0;
        }

        public static MusterPreview Preview(ArmyComposition comp)
        {
            var p = new MusterPreview();
            p.TrainSlots = TrainSlots();
            p.LineDepth = TrainLineDepth();
            p.LineRoom = ArmyMusterPlanner.RoomInLine(p.LineDepth, ArmyMusterPlanner.TrainQueueDepthCap);

            var state = DeNelle.Core.State.GameStateService.Instance?.State;

            var durations = new List<double>();
            if (comp != null && comp.Rows != null)
            {
                foreach (var row in comp.Rows)
                {
                    if (row == null || row.Count <= 0 || string.IsNullOrEmpty(row.TroopId)) continue;
                    if (!BarracksService.IsTroopUnlocked(row.TroopId)) continue;
                    var def = TroopCatalog.Find(row.TroopId);
                    if (def == null) continue;

                    p.TotalUnits += row.Count;
                    p.PlanSlots += TroopDialogueCommands.SlotOf(row.TroopId) * row.Count;
                    for (int i = 0; i < row.Count; i++) durations.Add(def.BuildSeconds);

                    // WO-1586 §12 instrumentation: OWNED vs TO-TRAIN, per row, so a capture answers
                    // "was this a rebalance between troops she already has, or a new training order?"
                    // without anyone theorising. It is a READING, not a discount - pressing Train Army
                    // still enqueues every staged unit, so the time estimate must cover them all.
                    int owned = state != null && state.Army != null ? state.Army.CountOfDef(row.TroopId) : 0;
                    owned += BarracksService.CountInFlightTrainOf(row.TroopId);
                    int covered = owned < row.Count ? owned : row.Count;
                    p.AlreadyOwned += covered;
                    p.ToTrain += row.Count - covered;
                    p.NewArmySlots += TroopDialogueCommands.SlotOf(row.TroopId) * (row.Count - covered);
                    FlowTrace.Step("Muster",
                        $"preview row '{row.TroopId}' count={row.Count} owned+inflight={owned} " +
                        $"covered={covered} toTrain={row.Count - covered} " +
                        $"seconds={def.BuildSeconds:0}/unit goldCharged=0 (WO-1387: training is TIME only).");
                }
            }

            p.TotalSeconds = ArmyMusterPlanner.BatchSeconds(durations, p.TrainSlots);
            p.WouldFit = p.TotalUnits < p.LineRoom ? p.TotalUnits : p.LineRoom;

            // ── WO-1586: THE PLAN IS JUDGED BY WHAT IT FITS INTO, NOT BY THE WALLET ──
            // This block used to read `p.Cost.Gold += def.CostGold * row.Count` above and then
            // `p.Affordable = state.Resources.Coins >= p.Cost.Gold`, which is what put "SHORT OF:
            // Gold" on the Armies panel for a player rebalancing troops she already owned (owner,
            // 2026-09-07: "everytime showed as need gold. But we agreed the one need for gold was
            // if you didnt want to wait on troops to train"). BarracksService.EnqueueTraining has
            // charged NOTHING since WO-1387, so the projection was quoting a price the action never
            // took. Cost stays a zero ArmyCost (it prints "Free"); gold lives on the SKIP verb only
            // (BuildTimerService.TryInstantFinish / HIRE REINFORCEMENTS).
            int rosterSlots = 0, queuedSlots = 0, capSlots = 0;
            if (state != null && state.Army != null)
            {
                var readiness = ArmyReadiness.Compute(state);
                rosterSlots = readiness.RosterSlots;
                queuedSlots = readiness.QueuedSlots;
                capSlots = readiness.CapSlots;
            }
            p.ArmyRoom = capSlots - rosterSlots - queuedSlots;
            if (p.ArmyRoom < 0) p.ArmyRoom = 0;

            // THE CAP IS MEASURED AGAINST **PlanSlots**, NOT NewArmySlots - because Muster() enqueues
            // EVERY staged unit through BarracksService.EnqueueTraining, which refuses each one on
            // `rosterSlots + committed + unitSlots > cap` (BarracksService.cs:385). Projecting the
            // cheaper NewArmySlots here would say "fits" and then have the button return "Queued 0 of
            // 10 - army is full", which is the projection/action disagreement §12 exists to kill. The
            // gold chip was FALSE because nothing charges gold; an army-room chip is TRUE, because the
            // cap really is checked. NewArmySlots/AlreadyOwned stay as the owned-vs-train READING for
            // the trace, and are deliberately not the cap input.
            bool fitsArmy = state == null || state.Army == null || p.PlanSlots <= p.ArmyRoom;
            bool fitsQueue = p.TotalUnits <= 0 || p.LineRoom > 0;
            p.Affordable = fitsArmy && fitsQueue;

            var shortOf = new System.Text.StringBuilder();
            if (!fitsArmy) shortOf.Append("Army room");
            if (!fitsQueue) shortOf.Append(shortOf.Length > 0 ? ", " : "").Append("Queue space");
            p.ShortOf = shortOf.ToString();

            FlowTrace.Step("Muster",
                $"preview plan units={p.TotalUnits} slots={p.PlanSlots} newSlots={p.NewArmySlots} owned={p.AlreadyOwned} " +
                $"toTrain={p.ToTrain} seconds={p.TotalSeconds:0} | army {rosterSlots}+{queuedSlots}/{capSlots} " +
                $"(room {p.ArmyRoom}) line {p.LineDepth}/{ArmyMusterPlanner.TrainQueueDepthCap} " +
                $"-> Affordable={p.Affordable} ShortOf=\"{p.ShortOf}\" Cost={p.Cost} " +
                $"(coins={(state != null ? state.Resources.Coins : 0)} - NOT a gate).");
            return p;
        }

        // ── The one action ────────────────────────────────────────────────────

        /// <summary>
        /// MUSTER: enqueues every troop in <paramref name="comp"/> onto the existing Train channel,
        /// in composition order, one job per unit, through the normal train rules. It charges
        /// NOTHING — the price is the queue time (WO-1387). Stops adding to a line that has hit the
        /// five-item cap and REPORTS the remainder;
        /// it never silently drops a unit. Safe to call with a null/empty composition.
        /// </summary>
        public static MusterReport Muster(ArmyComposition comp)
        {
            var report = new MusterReport();

            if (comp == null || comp.TotalUnits <= 0)
            {
                report.Headline = "Nothing to muster - the army is empty.";
                FlowTrace.Warn("Muster", "Muster called with an empty composition - nothing enqueued.");
                return report;
            }

            var queue = BuildTimerService.Instance;
            if (queue == null)
            {
                report.TotalRequested = comp.TotalUnits;
                report.Headline = "Queued 0 of " + report.TotalRequested + " - the training queue is not running.";
                FlowTrace.Fail("Muster",
                    $"Muster '{comp.Name}' aborted: BuildTimerService.Instance is NULL - " +
                    $"{report.TotalRequested} unit(s) NOT enqueued and NOTHING was charged.");
                return report;
            }

            int cap = ArmyMusterPlanner.TrainQueueDepthCap;
            int depthBefore = TrainLineDepth();
            int room = ArmyMusterPlanner.RoomInLine(depthBefore, cap);

            FlowTrace.Step("Muster",
                $"Muster '{comp.Name}': {comp.TotalUnits} unit(s) over {comp.Rows.Count} row(s); " +
                $"Train line depth {depthBefore}/{cap} (room {room}, slots {TrainSlots()}).");

            foreach (var row in comp.Rows)
            {
                if (row == null || row.Count <= 0 || string.IsNullOrEmpty(row.TroopId)) continue;

                // §12: guard EACH row - one bad entry logs + is skipped, it never aborts the muster.
                int roomForRow = room;
                MusterRowOutcome outcome = default;
                // STATEMENT lambda (braces) on purpose: `() => outcome = MusterRow(...)` is an
                // EXPRESSION lambda whose value is the assignment result, so it binds to
                // Guard.Try's Func<T> overload and returns MusterRowOutcome instead of bool.
                // The braces make it an Action and select the bool-returning overload.
                bool ok = Guard.Try("Muster", "muster row '" + row.TroopId + "'",
                    () => { outcome = MusterRow(row, roomForRow, cap); });
                if (!ok)
                {
                    outcome = new MusterRowOutcome
                    {
                        TroopId = row.TroopId,
                        DisplayName = NameOf(row.TroopId),
                        Requested = row.Count,
                        Queued = 0,
                        Reason = "Could not be queued - see the log.",
                    };
                }

                room -= outcome.Queued;
                if (room < 0) room = 0;
                report.TotalRequested += outcome.Requested;
                report.TotalQueued += outcome.Queued;
                report.Rows.Add(outcome);
            }

            Summarize(report, cap, depthBefore);

            if (report.AnyQueued) Guard.Try("Muster", "raise Mustered", () => Mustered?.Invoke());
            return report;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        /// <summary>
        /// Enqueues one composition row through the SANCTIONED single-troop path, clipped to the
        /// room left in the queue line. Returns the outcome (never throws for a normal refusal —
        /// a refusal is data, not an exception).
        /// </summary>
        private static MusterRowOutcome MusterRow(ArmyCompositionRow row, int room, int cap)
        {
            var outcome = new MusterRowOutcome
            {
                TroopId = row.TroopId,
                DisplayName = NameOf(row.TroopId),
                Requested = row.Count,
                Queued = 0,
            };

            if (room <= 0)
            {
                outcome.Reason = "the training queue is full (" + cap + " max)";
                return outcome;
            }

            int allowed = row.Count < room ? row.Count : room;

            // THE reuse point: the same call the single-troop train button makes. It charges NOTHING
            // (WO-1387), honours the army cap incl. in-flight jobs, and enqueues one
            // JobKind.TrainTroop job per unit on ChannelId.Train with a unique id.
            string stopReason;
            outcome.Queued = BarracksService.EnqueueTraining(row.TroopId, allowed, out stopReason);

            if (outcome.Queued < row.Count)
            {
                // Attribute the shortfall to the RIGHT blocker: the per-unit gate that stopped the
                // train call (army full / owned limit / locked - never a price) if it stopped short, otherwise
                // the queue-depth cap that clipped `allowed` in the first place.
                if (outcome.Queued < allowed && !string.IsNullOrEmpty(stopReason))
                    outcome.Reason = Lower(stopReason);
                else
                    outcome.Reason = "the training queue is full (" + cap + " max)";
            }

            return outcome;
        }

        /// <summary>
        /// Builds the ASCII, colour-free tell: the count headline plus a named line per shortfall,
        /// and a FlowTrace.Warn naming EXACTLY what was dropped (a silent cap is forbidden).
        /// </summary>
        private static void Summarize(MusterReport report, int cap, int depthBefore)
        {
            if (report.TotalRequested <= 0)
            {
                report.Headline = "Nothing to muster - the army is empty.";
                return;
            }

            if (report.Complete)
            {
                report.Headline = "Mustered - " + report.TotalQueued + " of " + report.TotalRequested +
                                  " queued for training.";
                FlowTrace.Step("Muster",
                    $"muster COMPLETE: {report.TotalQueued}/{report.TotalRequested} unit(s) enqueued on the Train channel " +
                    $"(line was {depthBefore}/{cap}).");
                return;
            }

            report.Headline = "Queued " + report.TotalQueued + " of " + report.TotalRequested +
                              " - " + report.TotalNotQueued + " did not fit.";

            var detail = new System.Text.StringBuilder();
            var dropped = new System.Text.StringBuilder();
            foreach (var r in report.Rows)
            {
                if (r.NotQueued <= 0) continue;
                if (detail.Length > 0) detail.Append(' ');
                detail.Append(r.NotQueued).Append("x ").Append(r.DisplayName)
                      .Append(" - ").Append(string.IsNullOrEmpty(r.Reason) ? "not queued" : r.Reason).Append('.');
                if (dropped.Length > 0) dropped.Append("; ");
                dropped.Append(r.NotQueued).Append("x ").Append(r.TroopId)
                       .Append(" (").Append(string.IsNullOrEmpty(r.Reason) ? "not queued" : r.Reason).Append(')');
            }
            report.Detail = detail.ToString();

            // §12: the drop is NAMED in the trace, unit-for-unit. "No silent caps" is a standing rule -
            // if this line is missing from a capture, the muster truncated without telling anyone.
            FlowTrace.Warn("Muster",
                $"muster PARTIAL: {report.TotalQueued}/{report.TotalRequested} enqueued; " +
                $"{report.TotalNotQueued} NOT queued -> {dropped} " +
                $"(Train line was {depthBefore}/{cap}). Surfaced to the player as: \"{report.Headline} {report.Detail}\"");
        }

        /// <summary>Player-facing troop name for <paramref name="troopId"/> (falls back to the id).</summary>
        private static string NameOf(string troopId)
        {
            var def = TroopCatalog.Find(troopId);
            if (def != null && !string.IsNullOrEmpty(def.DisplayName)) return def.DisplayName;
            return string.IsNullOrEmpty(troopId) ? "Troop" : troopId;
        }

        /// <summary>The cost as ledger lines (Wood/Iron/Food) — the wallet the spend actually charges.</summary>
        private static List<Ledger.ResourceCost> LedgerLines(ArmyCost cost)
        {
            var list = new List<Ledger.ResourceCost>(3);
            return list;
        }

        /// <summary>ASCII names of the resources the ledger cannot cover ("Wood, Iron"); empty when it can.</summary>
        private static string ShortOf(List<Ledger.ResourceCost> lines)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in lines)
                if (Ledger.ResourceLedger.Balance(line.Resource) < line.Amount)
                    sb.Append(sb.Length > 0 ? ", " : "").Append(line.Resource);
            return sb.ToString();
        }

        /// <summary>Lower-cases the first letter of a sentence so it reads inside "2x Archer - ...".</summary>
        private static string Lower(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return sentence;
            string s = sentence.TrimEnd('.');
            if (s.Length == 0) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }
    }
}
