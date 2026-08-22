// =============================================================================
// DefenseReportLedger — the ring-buffered store of DefenseOutcomeRecords (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Defense
//
// ONE owner of GameState.DefenseReports. Append, read, mark-read, trim.
//
// PERSISTENCE: this class NEVER opens a second save path. It mutates the live
// GameState and calls the EXISTING GameStateService.Save() — the same single writer
// every other system uses. A second save trigger is how two systems end up racing
// the same blob (the WO-1147 lesson, one authority per piece of state).
//
// THE TRIM IS TRACED, NEVER SILENT (the WaveDamageReport truncation precedent):
// a report that vanished without a line is indistinguishable from one that was never
// written, and "the base is never attacked" is exactly the bug class this WO exists
// to close.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Core.Defense
{
    /// <summary>Append/read/trim over the persisted defence-report ring buffer.</summary>
    public static class DefenseReportLedger
    {
        /// <summary>How many reports are kept. Oldest beyond this are dropped (traced).
        /// Config, not save state — the buffer depth is a product decision, not player progress.</summary>
        public const int MaxRetained = 10;

        private static GameState State
        {
            get
            {
                var svc = GameStateService.Instance;
                return svc != null ? svc.State : null;
            }
        }

        /// <summary>Ensures the list exists on the live state and every record is well-formed.
        /// Returns null only when there is no state at all (headless boot before the service).</summary>
        private static List<DefenseOutcomeRecord> EnsureList()
        {
            var s = State;
            if (s == null) return null;
            if (s.DefenseReports == null) s.DefenseReports = new List<DefenseOutcomeRecord>();
            for (int i = 0; i < s.DefenseReports.Count; i++)
                s.DefenseReports[i] = DefenseOutcomeRecord.Normalize(s.DefenseReports[i]);
            return s.DefenseReports;
        }

        /// <summary>
        /// Appends a finished report, newest-last, trimming the oldest beyond
        /// <see cref="MaxRetained"/>. Saves through the existing GameStateService path.
        /// Returns false (traced) when there is no state to write to — never throws.
        /// </summary>
        public static bool Append(DefenseOutcomeRecord record)
        {
            if (record == null)
            {
                FlowTrace.Warn("Siege", "ledger append refused: null record.");
                return false;
            }

            bool ok = Guard.Try("Siege", "defense report append", () =>
            {
                var list = EnsureList();
                if (list == null)
                {
                    FlowTrace.Warn("Siege",
                        $"report {record.Id} NOT persisted -- no GameStateService.State " +
                        "(the assault resolved but there is nowhere to write it).");
                    return;
                }

                DefenseOutcomeRecord.Normalize(record);
                list.Add(record);

                int trimmed = 0;
                while (list.Count > MaxRetained)
                {
                    list.RemoveAt(0);   // oldest first
                    trimmed++;
                }

                FlowTrace.Step("Siege",
                    $"report {record.Id} appended ({list.Count}/{MaxRetained}); trimmed {trimmed}; " +
                    $"outcome={record.Outcome} breaches={record.Breaches.Count} losses={record.Rows.Count}.");

                var svc = GameStateService.Instance;
                if (svc != null) svc.Save();
            });

            if (!ok) FlowTrace.Fail("Siege", $"ledger append THREW for report {record.Id} (see Guard line).");
            return ok;
        }

        /// <summary>All retained reports, OLDEST FIRST (the stored order). Never null.</summary>
        public static IReadOnlyList<DefenseOutcomeRecord> All()
        {
            var list = EnsureList();
            return list ?? (IReadOnlyList<DefenseOutcomeRecord>)new List<DefenseOutcomeRecord>();
        }

        /// <summary>Retained reports NEWEST FIRST — the order the panel lists them in.</summary>
        public static List<DefenseOutcomeRecord> NewestFirst()
        {
            var list = EnsureList();
            var outList = new List<DefenseOutcomeRecord>();
            if (list == null) return outList;
            for (int i = list.Count - 1; i >= 0; i--) outList.Add(list[i]);
            return outList;
        }

        /// <summary>Finds a report by id. Null when absent (never throws).</summary>
        public static DefenseOutcomeRecord TryGet(string reportId)
        {
            if (string.IsNullOrEmpty(reportId)) return null;
            var list = EnsureList();
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Id == reportId) return list[i];
            return null;
        }

        /// <summary>How many retained reports the player has not opened. Drives the door badge.</summary>
        public static int UnreadCount()
        {
            var list = EnsureList();
            if (list == null) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].Read) n++;
            return n;
        }

        /// <summary>Marks one report read and persists. No-op (returns false) when absent or
        /// already read, so a repeated panel open does not churn the save.</summary>
        public static bool MarkRead(string reportId)
        {
            var r = TryGet(reportId);
            if (r == null || r.Read) return false;
            r.Read = true;
            var svc = GameStateService.Instance;
            if (svc != null) svc.Save();
            FlowTrace.Step("Siege", $"report {reportId} marked read (unread now {UnreadCount()}).");
            return true;
        }

        /// <summary>Drops every retained report. Used by New Game reseeding and by oracles.</summary>
        public static void Clear()
        {
            var s = State;
            if (s == null) return;
            s.DefenseReports = new List<DefenseOutcomeRecord>();
        }
    }
}
