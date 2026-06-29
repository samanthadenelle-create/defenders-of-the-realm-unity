// =============================================================================
// IPopulationService — the Core-defined contract for the Population growth system
// (WORK_ORDER_587). Resolved cross-assembly via CoreServices.Population.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Population
//
// Population is a milestone-driven counter that EARNS additional Echo workforce
// SLOTS (quests / outpost reclamation / wave victories raise XP + counters;
// village upgrades raise the cap). The concrete PopulationService (DeNelle.Village)
// COORDINATES the existing EchoService — it never owns a second echo economy.
//
// Consumers (HUD, VMs) read CurrentXP / PopulationCap / EchoSlotsUnlocked and
// listen to Changed + EchoSlotUnlocked. ALWAYS null-check: CoreServices.Population?.
// =============================================================================

using System;

namespace DeNelle.Core.Population
{
    /// <summary>
    /// Read surface + the single mutation entry point (<see cref="AddPopulationXP"/>)
    /// for the Population growth system. Implemented by PopulationService
    /// (DeNelle.Village), registered into <c>CoreServices.Population</c>.
    /// </summary>
    public interface IPopulationService
    {
        /// <summary>Accumulated population XP (earned from quests / outposts / waves).</summary>
        int CurrentXP { get; }

        /// <summary>The population cap (raised by village / housing level). Derived, not earned.</summary>
        int PopulationCap { get; }

        /// <summary>How many Echo workforce slots are unlocked (1..5: 3 organic + 2 flex).</summary>
        int EchoSlotsUnlocked { get; }

        /// <summary>Raised whenever XP, the cap, or the unlocked-slot count changes (views repaint).</summary>
        event Action Changed;

        /// <summary>Raised once when a NEW echo slot is unlocked (arg = the unlocked slot number, 2..5).</summary>
        event Action<int> EchoSlotUnlocked;

        /// <summary>
        /// Add population XP from an earned <paramref name="source"/> ("quest" / "outpost" /
        /// "wave" / "village-upgrade"). Logs the source via FlowTrace (§12 self-reporting),
        /// advances the relevant counter, then re-evaluates the data-driven milestones and
        /// unlocks the next echo slot when one is met (≤5). Null-safe no-op before save load.
        /// </summary>
        void AddPopulationXP(int amount, string source);
    }
}
