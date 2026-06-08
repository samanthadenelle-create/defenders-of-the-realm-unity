using UnityEngine;
using Yarn.Unity;

namespace DeNelle.DialogueUI
{
    // =============================================================================
    // NPCCommandBridge — DEAD / NEUTRALIZED (2026-06-08).
    // -----------------------------------------------------------------------------
    // This bridge has NO live runtime caller: nothing in the project ever attaches
    // it to a DialogueRunner (`Install()` was never called — the referenced builder
    // `PlaceNpcStation` does not exist). Its registrations DUPLICATED names already
    // registered by the single LIVE bridge, DeNelle.Village.DialogueCommandBridge
    // (installed by DialogueService.Host on the shared runner — the one path every
    // vendor / Sylas / structure / rumor-board node actually plays through).
    //
    // The YarnSpinner source generator STATICALLY scans every `Install(IActionRegistration)`
    // method and collects each AddCommandHandler/AddFunction NAME into a project-wide
    // table; a name appearing in two scanned methods (e.g. HasKeystone, OpenShop,
    // StartQuest, RecruitCompanion …) made the YarnProject importer throw
    // "An item with the same key has already been added" and broke ALL dialogue.
    //
    // FIX: every verb/function this bridge used to register is now registered ONCE
    // on DialogueCommandBridge (the live runner). The registrations here are removed
    // so the generator sees each name exactly once. The handler BEHAVIOUR is unchanged
    // (the bodies moved verbatim to DialogueCommandBridge). This empty Install is kept
    // only so any stale reference still compiles; it registers nothing.
    // =============================================================================
    [DisallowMultipleComponent]
    public sealed class NPCCommandBridge : MonoBehaviour
    {
        // Intentionally registers NOTHING — all verbs/functions moved to
        // DeNelle.Village.DialogueCommandBridge (the single live runner's bridge).
        public void Install(DialogueRunner runner)
        {
            Debug.LogWarning("[NPCCommandBridge] Neutralized — all Yarn verbs/functions " +
                             "are registered by DialogueCommandBridge on the shared runner.");
        }
    }
}
