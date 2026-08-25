// =============================================================================
// ArmyComposition — a named army PRESET: an ordered list of {troopId, count}
// rows (WO-897 "create armies and they will auto-queue the build-outs").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// PURE DATA + PURE MATH. This type knows nothing about the queue, the wallet or
// the UI — it is just the composition the player authored ("5 Spearmen, 3 Archers,
// 2 Outriders"). ArmyMusterService turns it into Train-channel jobs; ArmyMusterPlan
// (same file) is the display projection (total cost / total time / what fits).
//
// PERSISTENCE (WO-934): named slots live on ArmyStorage.Loadouts (Core DTO). This
// type is still the session WORKING SET the Armies UI edits; ArmyLoadoutService
// copies to/from the saved slots.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>One composition line: how many of a single troop type the army wants.</summary>
    [Serializable]
    public sealed class ArmyCompositionRow
    {
        /// <summary>TroopDef id (e.g. <c>troop-spearman</c>).</summary>
        public string TroopId;
        /// <summary>How many of this troop the army wants. Never negative.</summary>
        public int Count;

        public ArmyCompositionRow(string troopId, int count)
        {
            TroopId = troopId;
            Count = count < 0 ? 0 : count;
        }
    }

    /// <summary>
    /// A named army composition — the ordered rows the player authored. Muster order IS
    /// row order (WO-897 §1: "count entries per troop row, in composition order").
    /// </summary>
    [Serializable]
    public sealed class ArmyComposition
    {
        /// <summary>Player-facing preset name. ASCII only (TMP tofu rule).</summary>
        public string Name = "New Army";

        /// <summary>The composition rows, in muster order. Never null.</summary>
        public List<ArmyCompositionRow> Rows = new List<ArmyCompositionRow>();

        /// <summary>Total units the composition asks for (sum of every row's count).</summary>
        public int TotalUnits
        {
            get
            {
                int n = 0;
                if (Rows != null)
                    foreach (var r in Rows)
                        if (r != null && r.Count > 0) n += r.Count;
                return n;
            }
        }

        /// <summary>The row for <paramref name="troopId"/>, or null.</summary>
        public ArmyCompositionRow Find(string troopId)
        {
            if (string.IsNullOrEmpty(troopId) || Rows == null) return null;
            foreach (var r in Rows)
                if (r != null && string.Equals(r.TroopId, troopId, StringComparison.Ordinal)) return r;
            return null;
        }

        /// <summary>The count requested for <paramref name="troopId"/> (0 when absent).</summary>
        public int CountOf(string troopId)
        {
            var row = Find(troopId);
            return row != null ? row.Count : 0;
        }

        /// <summary>
        /// Sets <paramref name="troopId"/>'s count (clamped to >= 0), appending a row if new and
        /// REMOVING the row at 0 so an empty line never musters. Returns the stored count.
        /// </summary>
        public int Set(string troopId, int count)
        {
            if (string.IsNullOrEmpty(troopId)) return 0;
            if (Rows == null) Rows = new List<ArmyCompositionRow>();
            if (count < 0) count = 0;

            var row = Find(troopId);
            if (count == 0)
            {
                if (row != null) Rows.Remove(row);
                return 0;
            }
            if (row == null) Rows.Add(new ArmyCompositionRow(troopId, count));
            else row.Count = count;
            return count;
        }

        /// <summary>Adds <paramref name="delta"/> to a row's count (clamped to >= 0). Returns the new count.</summary>
        public int Add(string troopId, int delta) => Set(troopId, CountOf(troopId) + delta);

        /// <summary>Empties the composition (keeps the name).</summary>
        public void Clear() => Rows?.Clear();

        /// <summary>Deep-copy rows + name from another composition.</summary>
        public void CopyFrom(ArmyComposition other)
        {
            if (other == null) { Clear(); return; }
            Name = other.Name ?? "New Army";
            if (Rows == null) Rows = new List<ArmyCompositionRow>();
            else Rows.Clear();
            if (other.Rows == null) return;
            foreach (var r in other.Rows)
            {
                if (r == null || string.IsNullOrEmpty(r.TroopId) || r.Count <= 0) continue;
                Rows.Add(new ArmyCompositionRow(r.TroopId, r.Count));
            }
        }

        /// <summary>Build a working composition from a persisted loadout slot.</summary>
        public static ArmyComposition FromLoadout(ArmyLoadoutSlot slot)
        {
            var c = new ArmyComposition();
            if (slot == null)
            {
                c.Name = "New Army";
                return c;
            }
            c.Name = string.IsNullOrEmpty(slot.Name) ? "New Army" : slot.Name;
            if (slot.Rows != null)
            {
                foreach (var r in slot.Rows)
                {
                    if (r == null || string.IsNullOrEmpty(r.TroopId) || r.Count <= 0) continue;
                    c.Set(r.TroopId, r.Count);
                }
            }
            return c;
        }

        /// <summary>Snapshot this composition into a save DTO (does not touch GameState).</summary>
        public ArmyLoadoutSlot ToLoadout()
        {
            var slot = new ArmyLoadoutSlot
            {
                Name = string.IsNullOrEmpty(Name) ? "New Army" : Name,
                Rows = new List<ArmyLoadoutRow>(),
            };
            if (Rows != null)
            {
                foreach (var r in Rows)
                {
                    if (r == null || string.IsNullOrEmpty(r.TroopId) || r.Count <= 0) continue;
                    slot.Rows.Add(new ArmyLoadoutRow(r.TroopId, r.Count));
                }
            }
            return slot;
        }
    }

    /// <summary>Summed resource cost of a composition — the four resources troops.json prices in.</summary>
    public struct ArmyCost
    {
        public int Gold;

        /// <summary>True when nothing is priced (an empty composition).</summary>
        public bool IsZero => Gold == 0;

        /// <summary>ASCII, never colour-coded: "120 Wood, 40 Iron, 60 Food" / "Free".</summary>
        public override string ToString()
        {
            if (IsZero) return "Free";
            var sb = new System.Text.StringBuilder();
            if (Gold > 0) sb.Append(Gold).Append(" Gold");
            return sb.ToString();
        }
    }

    /// <summary>
    /// PURE muster math — no Unity, no services, no clock, so a regression suite drives it
    /// headlessly. Cost/time projection + the queue-DEPTH arithmetic the muster obeys.
    /// </summary>
    public static class ArmyMusterPlanner
    {
        /// <summary>
        /// OWNER RULING (WO-911 §8 Q4, 2026-08-06): the work queue is capped at FIVE items
        /// TOTAL PER LINE - per channel, not global. That is queue DEPTH (active + waiting),
        /// a different axis from concurrency/slot count (WO-911 §2d).
        ///
        /// NOTE FOR THE ORCHESTRATOR: the cap is NOT enforced by ObsidianQueueEngine today - the
        /// engine appends to PendingQueue with no depth limit. WO-911's queue screen owns the
        /// engine-side enforcement. This constant is the muster's own honouring of the ruling so a
        /// batch enqueue can never blow past it; when the engine-side cap lands, BOTH should read
        /// ONE constant (delete this one and point at that authority) - two numbers is how they drift.
        /// </summary>
        public const int TrainQueueDepthCap = 5;

        /// <summary>Free places left on a queue line: cap - what is already in it. Never negative.</summary>
        public static int RoomInLine(int inLineNow, int cap = TrainQueueDepthCap)
        {
            int room = cap - (inLineNow < 0 ? 0 : inLineNow);
            return room < 0 ? 0 : room;
        }

        /// <summary>
        /// Wall-clock seconds for a batch of jobs run on <paramref name="slots"/> parallel workers,
        /// FIFO: each job goes to the worker that frees soonest, and the batch is done when the last
        /// worker finishes. With 1 slot this is the plain sum; with N slots it is the parallel-aware
        /// makespan the "total time" readout needs (WO-897 §3 "confirm whether the Train channel runs
        /// one-at-a-time or parallel"). Pure - null/empty is 0, negative durations count as 0.
        /// </summary>
        public static double BatchSeconds(IList<double> durations, int slots)
        {
            if (durations == null || durations.Count == 0) return 0d;
            if (slots < 1) slots = 1;

            var ends = new double[slots];
            double last = 0d;
            for (int i = 0; i < durations.Count; i++)
            {
                double d = durations[i];
                if (d < 0d) d = 0d;

                int best = 0;
                for (int s = 1; s < slots; s++)
                    if (ends[s] < ends[best]) best = s;

                ends[best] += d;
                if (ends[best] > last) last = ends[best];
            }
            return last;
        }

        /// <summary>
        /// ASCII duration for a player-facing readout: "45s" / "12m 30s" / "2h 05m". Never uses a
        /// non-ASCII glyph (TMP renders those as tofu on device).
        /// </summary>
        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0d) return "0s";
            int total = (int)System.Math.Round(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            if (h > 0) return h + "h " + m.ToString("00") + "m";
            if (m > 0) return m + "m " + s.ToString("00") + "s";
            return s + "s";
        }
    }
}
