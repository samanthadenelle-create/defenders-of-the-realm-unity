// =============================================================================
// BattleHud9Zone — RETIRED SHIM (P23 total demolition, HUD_OBSIDIAN A2/A4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// The 1,700-line WO-507 9-zone battle HUD is GONE. Its duties moved wholesale:
//   • TL hero plate / bars      -> HudKit vitals area (BuildNameplate(Player),
//     §1.1 fill contract — the 9/145 sprite-less-Filled defect of the old
//     FillBarLeft (:1701-1708 via AddImage :1571-1578) is structurally dead:
//     the file that created sprite-less Filled images no longer creates images).
//   • TC target block           -> HudKit targetInfo (BuildTargetFrame.Bind:
//     TargetModel !HasTarget => total Clear() — the :549 early-return that left
//     the dead target's bar under "No Target" is gone with the code).
//   • ML target cycle rows      -> HudKit status area (TargetCycleModel rows;
//     taps route HudCommands.CycleSelect -> HeroTargetIndicator.EngageLock,
//     registered by HudKitCommandBridge — the WO-512 routing preserved).
//   • BL square D-pad           -> BuildControllerCluster: FOUR ROUND buttons
//     -> HudMoveInput (read by HeroLocomotion's loose-reflection input OR).
//   • BC/BR ability + attack    -> HudKit actionBar/actionRail from
//     AbilityLoadoutModel + PlayerAttackController.TriggerBasicAttack.
//   • TR flee                   -> HudKit system area via HudCommands.Flee
//     (BattleArenaHud forwards BattleArena's handler on SetFleeHandler).
//   • Visibility gate           -> posture rows (hostile(activebattle)) —
//     no DontDestroyOnLoad canvas lingering into town.
//
// THIS SHIM keeps the public surface (Create/SetFleeHandler/Close) so the one
// compiling caller (BattleArenaHud) is untouched in shape: Create() registers
// the default flee handler and returns null (no object to manage), matching
// the documented "returns null so the caller no-ops cleanly" contract that
// already existed for the flag-OFF path.
// =============================================================================

using UnityEngine;
using DeNelle.Core.HUD;

namespace DeNelle.Village.Arena
{
    /// <summary>Retired shim — the HUD kit owns the battle HUD (see header).</summary>
    public sealed class BattleHud9Zone : MonoBehaviour
    {
        /// <summary>
        /// P23: the kit renders the battle HUD from models + posture rows; nothing to
        /// spawn. Registers the default FLEE handler (BattleArena.Existing.Flee) into
        /// the Core command sink and returns null — the pre-existing null contract.
        /// </summary>
        public static BattleHud9Zone Create()
        {
            HudCommands.RegisterFlee(DefaultFlee);
            Debug.Log("[BattleHud9Zone] retired shim: HUD kit owns the battle HUD; flee handler registered.");
            return null;
        }

        /// <summary>Override the FLEE handler (BattleArena wires its own retreat).</summary>
        public void SetFleeHandler(System.Action onFlee)
            => HudCommands.RegisterFlee(onFlee ?? (System.Action)DefaultFlee);

        /// <summary>Teardown seam (kept for callers) — clears the battle-scoped flee handler.</summary>
        public void Close()
        {
            HudCommands.RegisterFlee(null);
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        private static void DefaultFlee()
        {
            var arena = BattleArena.Existing;
            if (arena != null) arena.Flee();
            else Debug.Log("[BattleHud9Zone] FLEE - no BattleArena to flee from.");
        }
    }
}
