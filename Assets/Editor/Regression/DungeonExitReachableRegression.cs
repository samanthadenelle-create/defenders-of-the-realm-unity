// =============================================================================
// DungeonExitReachableRegression [dungeon-exit] — locks WO-770.1 (the roach-motel
// fix): the rich dungeon must have an ALWAYS-OPEN return exit (so a hero who can't
// or won't beat the mini-boss is never trapped), plus a boss-gated back-door revealed
// on BossDefeated — both routing through DungeonController.ExitToVillage (banks loot).
// Source-lint (edit-mode, no PlayMode). Wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonExitReachableRegression
    {
        public static bool Run(out string reason)
        {
            string ctrl = Path.Combine(Application.dataPath, "_Modules/Dungeons/DungeonController.cs");
            string exit = Path.Combine(Application.dataPath, "_Modules/Dungeons/DungeonExitInteractable.cs");

            var fails = new List<string>();
            string ctrlTxt = File.Exists(ctrl) ? File.ReadAllText(ctrl) : "";
            string exitTxt = File.Exists(exit) ? File.ReadAllText(exit) : "";

            // (1) INJECTOR — HydrateExits exists AND is called from the Start hydrate sequence.
            if (!ctrlTxt.Contains("private void HydrateExits"))
                fails.Add("DungeonController has no HydrateExits() injector");
            if (!ctrlTxt.Contains("HydrateExits();"))
                fails.Add("DungeonController.Start never calls HydrateExits() (exits are never injected)");

            // (2) NORMAL exit — an always-open exit spawned + routed through ExitToVillage.
            if (!ctrlTxt.Contains("DungeonExitInteractable.Spawn"))
                fails.Add("HydrateExits does not spawn a DungeonExitInteractable (no in-world exit)");
            if (!ctrlTxt.Contains("ExitToVillage()"))
                fails.Add("the injected exits do not route through ExitToVillage (run/loot not banked)");

            // (3) BOSS back-door — placed at the Workshop, hidden, revealed on BossDefeated.
            if (!ctrlTxt.Contains("FindRoom(\"workshop\")"))
                fails.Add("no boss back-door placed at the Workshop room");
            if (!ctrlTxt.Contains("_bossBackDoor") || !ctrlTxt.Contains("BossDefeated"))
                fails.Add("the boss back-door is not gated/revealed on BossDefeated");

            // (4) COMPONENT — the exit can route through a supplied leave action (rich scene).
            if (!exitTxt.Contains("System.Action onLeave"))
                fails.Add("DungeonExitInteractable.Spawn takes no onLeave action (can't route to ExitToVillage)");
            if (!exitTxt.Contains("_onLeave.Invoke()"))
                fails.Add("DungeonExitInteractable.Leave never invokes the supplied leave action");

            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_EXIT_OK");
                reason = "DUNGEON EXIT OK — always-open exit + boss-gated back-door injected, both routing via ExitToVillage (no roach-motel)";
                return true;
            }
            reason = "dungeon-exit: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_EXIT_FAIL: " + reason);
            return false;
        }
    }
}
