// =============================================================================
// TroopDialogueCommands — the Yarn ↔ army seam for the Barracks training flow
// (WO-453 troop-training flow). Holds the actual training logic the Barracks_MainMenu
// Yarn node drives: open the training panel, and train N of a troop into ArmyStorage.
// -----------------------------------------------------------------------------
// REGISTRATION (project canon — NOT [YarnCommand] attributes): every custom Yarn
// command in this project is registered on the ONE shared DialogueRunner via
// DialogueCommandBridge.RegisterCommands() → Reg(name, handler). The YarnSpinner
// source generator THROWS on a duplicate command name across IActionRegistration
// methods, so a SECOND attribute-based registrant would collide. The bridge therefore
// adds two entries that delegate here:
//     Reg("ShowTrainingUI", (Action)TroopDialogueCommands.ShowTrainingUI);
//     Reg("StartTraining",  (Action<string,int>)TroopDialogueCommands.StartTraining);
// This class is the single home for the logic; the bridge is just the wire.
//
// THE ARMY SEAM: ArmyStorage lives in DeNelle.Core and may not reference the Village
// TroopCatalog / EconomyService (CS0234 circular). So its catalog-dependent methods
// take seam delegates — a slot resolver + an affordability/spend callback — which this
// Village-side class supplies: SlotOf → TroopDef.Slots, and the train spend →
// EconomyService.TrySpend(new ResourceCost(CostWood, CostFood, CostIron)). ResourceCost
// ctor order is (wood, food, iron, crystals).
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// The training logic the Barracks Yarn node drives (open the panel; train troops
    /// into <see cref="ArmyStorage"/>). Registered on the shared runner by
    /// <see cref="DialogueCommandBridge"/>; also called directly by the training panel.
    /// </summary>
    public static class TroopDialogueCommands
    {
        // <<ShowTrainingUI>> — open the code-built barracks training panel, self-healing
        // a host if none exists (the panel builds its own Canvas). Mirrors the bridge's
        // OpenShop/OpenCraft/OpenEquip verb pattern.
        public static void ShowTrainingUI()
        {
            var panel = UnityEngine.Object.FindObjectOfType<DeNelle.Village.Hero.TroopTrainingPanel>();
            if (panel == null)
                panel = new GameObject("TroopTrainingPanelHost")
                    .AddComponent<DeNelle.Village.Hero.TroopTrainingPanel>();
            panel.Open();
            Debug.Log("[TroopDialogueCommands] ShowTrainingUI — opened the barracks training panel.");
        }

        // <<StartTraining troopId qty>> — train qty × troopId straight into the army
        // (used if a Yarn option trains without opening the panel). Returns nothing to
        // Yarn; the void shape matches the bridge's Action<string,int> registration.
        public static void StartTraining(string troopId, int qty)
        {
            int trained = Train(troopId, qty);
            Debug.Log($"[TroopDialogueCommands] StartTraining '{troopId}' x{qty} → trained {trained}.");
        }

        /// <summary>
        /// Trains up to <paramref name="qty"/> of <paramref name="troopId"/> into the
        /// persisted army, wiring the <see cref="ArmyStorage.TrainNow"/> seams to
        /// <see cref="TroopCatalog"/> (slot cost) + <see cref="EconomyService"/>
        /// (affordability/spend). Stops early when the cap fills or a spend fails (a
        /// failed train mutates nothing). Returns the number actually trained.
        /// </summary>
        public static int Train(string troopId, int qty)
        {
            if (string.IsNullOrEmpty(troopId) || qty <= 0) return 0;

            var army = Army();
            if (army == null)
            {
                Debug.LogWarning("[TroopDialogueCommands] no GameState army — cannot train.");
                return 0;
            }

            int trained = 0;
            for (int i = 0; i < qty; i++)
            {
                // Wire the Core-side army seam to the Village systems:
                //   slotOf    → TroopDef.Slots (1 for the starter troops)
                //   tryAfford → EconomyService.TrySpend(cost) — true AND spent, or false + no spend.
                var t = army.TrainNow(troopId, SlotOf, TryAfford);
                if (t == null) break;   // cap full OR unaffordable — TrainNow mutated nothing
                trained++;
            }
            return trained;
        }

        /// <summary>Slot-cost resolver seam: TroopDef.Slots, 1 when the def is unknown.</summary>
        public static int SlotOf(string id)
        {
            var d = TroopCatalog.Find(id);
            return d != null && d.Slots > 0 ? d.Slots : 1;
        }

        // Affordability/spend seam: spend the troop's resource cost through EconomyService.
        // Returns true AND has deducted the cost, or false + no spend (so TrainNow rolls
        // back cleanly). A missing economy or unknown def → no spend (false), so a troop
        // is never minted for free by accident.
        private static bool TryAfford(string id)
        {
            var d = TroopCatalog.Find(id);
            if (d == null) return false;
            if (EconomyService.Instance == null) return false;
            // ResourceCost ctor order: (wood, food, iron, crystals).
            return EconomyService.Instance.TrySpend(new ResourceCost(d.CostWood, d.CostFood, d.CostIron));
        }

        private static ArmyStorage Army()
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.State != null ? svc.State.Army : null;
        }
    }
}
