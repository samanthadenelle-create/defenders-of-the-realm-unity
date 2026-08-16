// =============================================================================
// PetTaskController -- RETIRED IN PLACE (WO-1108 Lane B, 2026-08-16).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT THIS FILE IS NOW: a task-state holder with NO update loop and NO installer.
// Nothing attaches it. It is kept as a TYPE (not deleted) because
// EchoEngageDialogueRegression pins its shape by reflection + source-lint, and a
// removal that nothing guards is a removal that quietly comes back.
//
// HISTORY, both retirements intact:
//   * WO-1031 (2026-08-16) deleted the world ENGAGEMENT PROMPT -- the code-built
//     2-choice def, both trigger paths, the invented species->display-name table,
//     and its dialogue verb. ONE HOME FOR TASKING, AND IT IS THE ECHO TAB
//     (EchoCardView / EchoCardVM -> EchoAssignments). That ruling still stands.
//   * WO-1108 (this change) removed what WO-1031 left behind: after it, Update()
//     did EXACTLY ONE THING -- TickRepair() -- and PetTaskInstaller still bolted
//     that husk onto every spawned pet once a second, forever.
//
// WHY THE REPAIR LOOP HAD TO GO (the real defect, not tidying): WO-1108 Lane A
// makes structure repair PASSIVE and count-driven -- every owned Echo contributes,
// with EchoRepairService as the single scanner/spender against
// WallRepairController. This loop drove WallRepairController.RepairAll() from a
// SECOND, uncoordinated place, on its own 1.5s cadence, against the SAME walls and
// the SAME construction economy. Two spenders racing over one wallet is a
// double-spend and a non-deterministic repair rate, and it would have looked like
// a balance bug in Lane A's numbers rather than a leftover loop here. There is now
// exactly ONE repairer: EchoRepairService.
//
// AND WHY THE INSTALLER WENT WITH IT: PetTaskInstaller was a DontDestroyOnLoad
// poller that ran FindObjectsByType<Pet> every second for the entire session in
// order to AddComponent a husk. With the loop gone it had nothing to install.
//
// DO NOT re-add: a world-space tasking prompt (that is the Echo tab's job), a
// repair loop (that is EchoRepairService's job), or a poller that attaches this
// component. If an Echo ever needs a display name, READ IT FROM EchoRosterCatalog
// -- never hand-author a species -> name table again (WO-1031 sec. 2b/2d).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Pets;

namespace DeNelle.Village
{
    /// <summary>The task a pet is performing. Assigned from the Echo tab (WO-1031).</summary>
    public enum PetTask
    {
        /// <summary>Gather resources (the pet's existing PetHarvester loop).</summary>
        Harvest = 0,

        /// <summary>
        /// Legacy value. Structure repair is PASSIVE and count-driven since WO-1108 --
        /// every owned Echo contributes through <c>EchoRepairService</c>, and no pet
        /// runs a repair loop of its own. Kept only so stored/legacy values still parse;
        /// <see cref="PetTaskController.SetTask"/> refuses it loudly.
        /// </summary>
        Repair = 1,
    }

    /// <summary>
    /// Holds a deployed <see cref="Pet"/>'s task. RETIRED (WO-1108): no Update loop,
    /// no repair loop, and nothing attaches it any more -- see the file header.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetTaskController : MonoBehaviour
    {
        private Pet _pet;
        private PetHarvester _harvester;
        private PetTask _task = PetTask.Harvest;   // default = gather (matches the deploy-time PetHarvester)

        /// <summary>The task the pet is currently assigned.</summary>
        public PetTask Task => _task;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            _harvester = GetComponent<PetHarvester>();
            FlowTrace.Warn("Pet",
                $"PetTaskController attached to '{PetId()}' -- this component is RETIRED (WO-1108) and " +
                "nothing should be attaching it. It has no update loop; repair is passive and count-driven " +
                "via EchoRepairService, and tasking lives in the Echo tab. Find the attacher and remove it.");
        }

        // NO Update(). WO-1031 emptied it of everything but TickRepair; WO-1108 removed
        // that too (a second uncoordinated repairer of the same walls -- see the header).

        /// <summary>
        /// Assigns the pet's task. Only <see cref="PetTask.Harvest"/> has a backing loop;
        /// <see cref="PetTask.Repair"/> is refused loudly rather than silently accepted,
        /// because accepting it would imply a repair loop that no longer exists.
        /// </summary>
        public void SetTask(PetTask task)
        {
            if (_harvester == null) _harvester = GetComponent<PetHarvester>();

            if (task == PetTask.Repair)
            {
                FlowTrace.Warn("Pet",
                    $"SetTask(Repair) REFUSED for pet '{PetId()}' -- per-pet repair was retired by WO-1108. " +
                    "Repair is now passive: every owned Echo contributes, and EchoRepairService is the single " +
                    "scanner/spender against WallRepairController. The pet stays on Harvest.");
                return;
            }

            _task = task;
            Guard.Try("Pet", "enable harvest task", () =>
            {
                if (_harvester != null) _harvester.enabled = true;
            });
            FlowTrace.Step("Pet", $"harvest task active -- pet '{PetId()}' will gather via PetHarvester.");
        }

        private string PetId() => _pet != null ? _pet.PetId : "<null>";
    }

    // PetTaskInstaller REMOVED (WO-1108 Lane B). It was a DontDestroyOnLoad poller that
    // ran FindObjectsByType<Pet> once a second for the whole session purely to AddComponent
    // the husk above. With the repair loop gone there was nothing left to install, and the
    // poll cost + the "every pet secretly owns a repairer" surprise both go with it.
    // Do NOT restore it: if a pet ever needs per-instance task state again, the Echo tab
    // should attach it to the ONE pet it is tasking, not a poller to every pet in the world.
}
