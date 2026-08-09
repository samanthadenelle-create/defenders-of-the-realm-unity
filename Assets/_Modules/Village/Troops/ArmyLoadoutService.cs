// =============================================================================
// ArmyLoadoutService — save/load/quick-fill for the 3 named army presets (WO-934).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village
//
// Bridges ArmyComposition (session working set) <-> ArmyStorage.Loadouts (persisted).
// Quick-fill recipes give the player FUN starting templates instead of an empty sheet:
//   Raid Push  — day-one Foots + Archers (push the base)
//   Wall Hold  — more tanks / reach when unlocked
//   Siege Prep  — catapult + escorts when unlocked
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>Village-side loadout bank API for the Armies panel.</summary>
    public static class ArmyLoadoutService
    {
        public const int SlotCount = ArmyLoadoutBank.SlotCount;

        private static ArmyStorage Army
        {
            get
            {
                var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                return s != null ? s.Army : null;
            }
        }

        /// <summary>Ensure bank exists; return army or null if no state.</summary>
        public static ArmyStorage Ensure()
        {
            var army = Army;
            if (army == null) return null;
            army.EnsureLoadouts();
            return army;
        }

        public static int ActiveIndex
        {
            get
            {
                var army = Ensure();
                return army != null ? army.ActiveLoadoutIndex : 0;
            }
            set
            {
                var army = Ensure();
                if (army == null) return;
                if (value < 0) value = 0;
                if (value >= SlotCount) value = SlotCount - 1;
                army.ActiveLoadoutIndex = value;
            }
        }

        public static string SlotName(int index)
        {
            var army = Ensure();
            var slot = army != null ? army.GetLoadout(index) : null;
            if (slot == null || string.IsNullOrEmpty(slot.Name))
                return ArmyLoadoutBank.DefaultName(index);
            return slot.Name;
        }

        public static int SlotUnitCount(int index)
        {
            var army = Ensure();
            var slot = army != null ? army.GetLoadout(index) : null;
            return slot != null ? slot.TotalUnits : 0;
        }

        /// <summary>Copy a saved slot into the working composition.</summary>
        public static void LoadInto(int index, ArmyComposition working)
        {
            if (working == null) return;
            var army = Ensure();
            if (army == null) return;
            army.ActiveLoadoutIndex = index;
            var slot = army.GetLoadout(index);
            working.CopyFrom(ArmyComposition.FromLoadout(slot));
            FlowTrace.Step("Muster",
                $"loadout load slot={index} name='{working.Name}' units={working.TotalUnits}.");
        }

        /// <summary>Write the working composition into a slot and persist.</summary>
        public static void SaveFrom(int index, ArmyComposition working)
        {
            if (working == null) return;
            var army = Ensure();
            if (army == null) return;
            army.EnsureLoadouts();
            if (index < 0 || index >= army.Loadouts.Count) return;
            var snap = working.ToLoadout();
            if (string.IsNullOrEmpty(snap.Name))
                snap.Name = ArmyLoadoutBank.DefaultName(index);
            army.Loadouts[index] = snap;
            army.ActiveLoadoutIndex = index;
            GameStateService.Instance?.Save();
            FlowTrace.Step("Muster",
                $"loadout save slot={index} name='{snap.Name}' units={snap.TotalUnits}.");
        }

        public static void Rename(int index, string name)
        {
            var army = Ensure();
            if (army == null) return;
            var slot = army.GetLoadout(index);
            if (slot == null) return;
            if (string.IsNullOrEmpty(name)) name = ArmyLoadoutBank.DefaultName(index);
            // ASCII / length clamp for TMP tofu + button fit.
            if (name.Length > 18) name = name.Substring(0, 18);
            slot.Name = name.Trim();
            GameStateService.Instance?.Save();
        }

        /// <summary>
        /// Quick-fill recipes that respect unlocks. Returns a short toast line.
        /// recipe: 0 = Raid Push, 1 = Wall Hold, 2 = Siege Prep, 3 = Clear.
        /// </summary>
        public static string ApplyRecipe(ArmyComposition working, int recipe)
        {
            if (working == null) return "No composition.";
            working.Clear();

            if (recipe == 3)
            {
                working.Name = "New Army";
                return "Cleared staging.";
            }

            switch (recipe)
            {
                case 0:
                    working.Name = "Raid Push";
                    AddIfUnlocked(working, "troop-footman", 4);
                    AddIfUnlocked(working, "troop-archer", 3);
                    AddIfUnlocked(working, "troop-spearman", 1);
                    break;
                case 1:
                    working.Name = "Wall Hold";
                    AddIfUnlocked(working, "troop-shieldguard", 2);
                    AddIfUnlocked(working, "troop-footman", 3);
                    AddIfUnlocked(working, "troop-spearman", 2);
                    AddIfUnlocked(working, "troop-archer", 2);
                    break;
                case 2:
                    working.Name = "Siege Prep";
                    AddIfUnlocked(working, "troop-catapult", 1);
                    AddIfUnlocked(working, "troop-footman", 3);
                    AddIfUnlocked(working, "troop-shieldguard", 1);
                    AddIfUnlocked(working, "troop-archer", 2);
                    break;
                default:
                    working.Name = "New Army";
                    return "Unknown recipe.";
            }

            // Cap by army free slots so the recipe never stages an impossible army.
            TrimToArmyRoom(working);

            if (working.TotalUnits <= 0)
                return "No unlocked troops for that recipe yet. Train day-one units first.";

            return "Staged " + working.Name + " - " + working.TotalUnits + " troops. Save or Muster.";
        }

        private static void AddIfUnlocked(ArmyComposition c, string troopId, int count)
        {
            if (c == null || count <= 0 || string.IsNullOrEmpty(troopId)) return;
            if (!BarracksService.IsTroopUnlocked(troopId)) return;
            c.Set(troopId, count);
        }

        /// <summary>Drop trailing units until total slots fit remaining army capacity.</summary>
        private static void TrimToArmyRoom(ArmyComposition c)
        {
            if (c == null || c.Rows == null) return;
            var army = Army;
            if (army == null) return;

            int room = army.SlotsRemaining(TroopDialogueCommands.SlotOf);
            // Also leave room for in-flight train slots (readiness).
            var ready = ArmyReadiness.Compute(GameStateService.Instance != null
                ? GameStateService.Instance.State : null);
            int free = ready.CapSlots - ready.RosterSlots - ready.QueuedSlots;
            if (free < room) room = free;
            if (room < 0) room = 0;

            int used = 0;
            var keep = new List<ArmyCompositionRow>();
            foreach (var r in c.Rows)
            {
                if (r == null || r.Count <= 0) continue;
                int slotsEach = TroopDialogueCommands.SlotOf(r.TroopId);
                if (slotsEach < 1) slotsEach = 1;
                int canTake = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    if (used + slotsEach > room) break;
                    used += slotsEach;
                    canTake++;
                }
                if (canTake > 0)
                    keep.Add(new ArmyCompositionRow(r.TroopId, canTake));
            }
            c.Rows.Clear();
            foreach (var k in keep) c.Rows.Add(k);
        }
    }
}
