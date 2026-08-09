// =============================================================================
// ArmyLoadoutBank — persisted named army composition presets (WO-934).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// Pure save DTOs on the ArmyStorage wire. Village converts these to/from
// ArmyComposition for the Armies panel + muster. Lives in Core so GameState.Army
// can own them without a Village reference.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>How many named loadout slots the player gets (fixed, not a paid unlock).</summary>
    public static class ArmyLoadoutBank
    {
        public const int SlotCount = 3;

        public static string DefaultName(int index)
        {
            switch (index)
            {
                case 0: return "Raid Push";
                case 1: return "Wall Hold";
                case 2: return "Siege Prep";
                default: return "Loadout " + (index + 1);
            }
        }
    }

    /// <summary>One line in a saved loadout: troop def id + desired count.</summary>
    [Serializable]
    public sealed class ArmyLoadoutRow
    {
        [JsonProperty("troopId")] public string TroopId;
        [JsonProperty("count")] public int Count;

        public ArmyLoadoutRow() { }

        public ArmyLoadoutRow(string troopId, int count)
        {
            TroopId = troopId;
            Count = count < 0 ? 0 : count;
        }
    }

    /// <summary>One named loadout slot (player-facing preset).</summary>
    [Serializable]
    public sealed class ArmyLoadoutSlot
    {
        [JsonProperty("name")] public string Name = "New Army";
        [JsonProperty("rows")] public List<ArmyLoadoutRow> Rows = new List<ArmyLoadoutRow>();

        /// <summary>Total units requested across all rows.</summary>
        [JsonIgnore]
        public int TotalUnits
        {
            get
            {
                int n = 0;
                if (Rows == null) return 0;
                foreach (var r in Rows)
                    if (r != null && r.Count > 0) n += r.Count;
                return n;
            }
        }

        /// <summary>Deep copy for safe UI edits without mutating save until Save.</summary>
        public ArmyLoadoutSlot Clone()
        {
            var c = new ArmyLoadoutSlot
            {
                Name = Name ?? ArmyLoadoutBank.DefaultName(0),
                Rows = new List<ArmyLoadoutRow>(),
            };
            if (Rows != null)
            {
                foreach (var r in Rows)
                {
                    if (r == null || string.IsNullOrEmpty(r.TroopId) || r.Count <= 0) continue;
                    c.Rows.Add(new ArmyLoadoutRow(r.TroopId, r.Count));
                }
            }
            return c;
        }

        public void ClearRows()
        {
            if (Rows != null) Rows.Clear();
            else Rows = new List<ArmyLoadoutRow>();
        }
    }
}
