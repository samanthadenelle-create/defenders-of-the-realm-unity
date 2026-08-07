// =============================================================================
// ArmyMusterService — "Muster army": ONE action that enqueues EVERY troop in an
// ArmyComposition onto the EXISTING Obsidian TRAIN channel (WO-897).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// NO SECOND QUEUE. This is a BATCH ENQUEUE over the sanctioned single-troop train
// path — BarracksService.EnqueueTraining, which spends the ledger cost, honours the
// army cap (incl. in-flight jobs) and calls BuildTimerService.Enqueue(JobKind.TrainTroop,
// ...) on ChannelId.Train. The muster adds exactly one thing on top: the QUEUE-DEPTH
// budget (owner ruling WO-911 Q4 — five items total per line) and a REPORT naming what
// did and did not fit.
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
        /// <summary>Summed resource cost of every unit.</summary>
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
        /// <summary>True when the ledger covers <see cref="Cost"/> in full.</summary>
        public bool Affordable;
        /// <summary>ASCII names of the short resources ("Wood, Iron"); empty when affordable.</summary>
        public string ShortOf;
    }

    /// <summary>
    /// Turns an <see cref="ArmyComposition"/> into Train-channel jobs in one action, and projects
    /// its cost/time for the panel. Reuses <see cref="BarracksService.EnqueueTraining(string,int,out string)"/>
    /// — it never forks the enqueue, the spend or the army-cap rule.
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
        public static (int Wood, int Iron, int Food) WalletBalances()
        {
            return (Ledger.ResourceLedger.Balance(Ledger.HarvestResource.Wood),
                    Ledger.ResourceLedger.Balance(Ledger.HarvestResource.Iron),
                    Ledger.ResourceLedger.Balance(Ledger.HarvestResource.Food));
        }

        public static MusterPreview Preview(ArmyComposition comp)
        {
            var p = new MusterPreview();
            p.TrainSlots = TrainSlots();
            p.LineDepth = TrainLineDepth();
            p.LineRoom = ArmyMusterPlanner.RoomInLine(p.LineDepth, ArmyMusterPlanner.TrainQueueDepthCap);

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
                    p.Cost.Wood += def.CostWood * row.Count;
                    p.Cost.Iron += def.CostIron * row.Count;
                    p.Cost.Food += def.CostFood * row.Count;
                    for (int i = 0; i < row.Count; i++) durations.Add(def.BuildSeconds);
                }
            }

            p.TotalSeconds = ArmyMusterPlanner.BatchSeconds(durations, p.TrainSlots);
            p.WouldFit = p.TotalUnits < p.LineRoom ? p.TotalUnits : p.LineRoom;

            // Affordability against the SAME wallet the spend charges (the GameState ledger),
            // never EconomyService's divergent in-session pool - see BarracksService's wallet note.
            var lines = LedgerLines(p.Cost);
            p.Affordable = Ledger.ResourceLedger.CanAfford(lines);
            p.ShortOf = p.Affordable ? "" : ShortOf(lines);
            return p;
        }

        // ── The one action ────────────────────────────────────────────────────

        /// <summary>
        /// MUSTER: enqueues every troop in <paramref name="comp"/> onto the existing Train channel,
        /// in composition order, one job per unit — spending resources through the normal train
        /// rules. Stops adding to a line that has hit the five-item cap and REPORTS the remainder;
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

            // THE reuse point: the same call the single-troop train button makes. It spends the
            // ledger cost, honours the army cap incl. in-flight jobs, and enqueues one
            // JobKind.TrainTroop job per unit on ChannelId.Train with a unique id.
            string stopReason;
            outcome.Queued = BarracksService.EnqueueTraining(row.TroopId, allowed, out stopReason);

            if (outcome.Queued < row.Count)
            {
                // Attribute the shortfall to the RIGHT blocker: the per-unit gate that stopped the
                // train call (army full / short resources / locked) if it stopped short, otherwise
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
            if (cost.Wood > 0) list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Wood, cost.Wood));
            if (cost.Iron > 0) list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Iron, cost.Iron));
            if (cost.Food > 0) list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Food, cost.Food));
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
