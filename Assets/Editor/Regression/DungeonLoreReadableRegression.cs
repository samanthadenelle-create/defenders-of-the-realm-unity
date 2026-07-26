// =============================================================================
// DungeonLoreReadableRegression [dungeon-lore] — locks WO-770.4 (fixes D6): the
// lore-stone triple gap (no input caller for Read(), no subscriber for
// ReadRequested, no view) must stay closed. Source-lint (edit-mode, no PlayMode),
// wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonLoreReadableRegression
    {
        public static bool Run(out string reason)
        {
            string root = Path.Combine(Application.dataPath, "_Modules/Dungeons");
            string lore  = Path.Combine(root, "LoreStone.cs");
            string ctrl  = Path.Combine(root, "DungeonController.cs");
            string modal = Path.Combine(root, "UI/LoreReadingModal.cs");

            var fails = new List<string>();

            // (1) INPUT — a tap must call LoreStone.Read via the shared interact button.
            if (!File.Exists(lore) || !File.ReadAllText(lore).Contains("MobileInteractButton.Request"))
                fails.Add("LoreStone has no input caller for Read() (MobileInteractButton.Request missing) — Read() is unreachable");

            // (2) SUBSCRIBER — DungeonController must wire ReadRequested to the modal.
            if (!File.Exists(ctrl) || !File.ReadAllText(ctrl).Contains("LoreReadingModal.Show"))
                fails.Add("DungeonController does not subscribe LoreStone.ReadRequested -> LoreReadingModal.Show");

            // (3) VIEW — a code-built (ElarionUiKit) reading modal must exist; NO uxml (CLAUDE.md §8).
            if (!File.Exists(modal))
                fails.Add("LoreReadingModal.cs (the reading view) does not exist");
            else if (!File.ReadAllText(modal).Contains("ElarionUiKit"))
                fails.Add("LoreReadingModal is not built via ElarionUiKit (code UI) — uxml does not work in builds");

            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_LORE_OK");
                reason = "DUNGEON LORE OK — lore stones readable: input (MobileInteractButton) + subscriber + code Obsidian modal all present";
                return true;
            }
            reason = "dungeon-lore: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_LORE_FAIL: " + reason);
            return false;
        }
    }
}
