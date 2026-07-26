// =============================================================================
// DungeonToastRegression [dungeon-toast] — locks WO-770.7 (fixes D13/D14): dungeon
// feedback that used to fire into the void must be surfaced. Asserts:
//   1. A code-built (ElarionUiKit.ToastCard — NOT uxml, §8) DungeonToastView exists
//      with a static Show(string).
//   2. DungeonController subscribes BOTH Checkpoint.ToastRequested AND
//      CraftingPedestal.ToastRequested to it (>=2 AddListener wires).
//   3. Bryn is given the toast sink AND its FirstMeet[]/Idle[] pickers are now LIVE
//      (PickFirstMeetLine + PickIdleLine are called), closing the D14 dead-code gap.
// Source-lint (edit-mode, no PlayMode). Wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonToastRegression
    {
        public static bool Run(out string reason)
        {
            string root  = Path.Combine(Application.dataPath, "_Modules/Dungeons");
            string view  = Path.Combine(root, "UI/DungeonToastView.cs");
            string ctrl  = Path.Combine(root, "DungeonController.cs");
            string bryn  = Path.Combine(root, "Wanderer/Bryn.cs");

            var fails = new List<string>();
            string viewTxt = File.Exists(view) ? File.ReadAllText(view) : "";
            string ctrlTxt = File.Exists(ctrl) ? File.ReadAllText(ctrl) : "";
            string brynTxt = File.Exists(bryn) ? File.ReadAllText(bryn) : "";

            // (1) VIEW — code-built toast (Obsidian ToastCard), NOT uxml.
            if (!File.Exists(view))
                fails.Add("DungeonToastView.cs (the toast view) does not exist");
            else
            {
                if (!viewTxt.Contains("ElarionUiKit.ToastCard"))
                    fails.Add("DungeonToastView is not built via ElarionUiKit.ToastCard (code UI) — uxml does not work in builds");
                if (!Regex.IsMatch(viewTxt, @"static\s+void\s+Show\s*\("))
                    fails.Add("DungeonToastView has no static Show(string) entry point");
            }

            // (2) D13 — both silent events are surfaced.
            int wires = Regex.Matches(ctrlTxt, @"ToastRequested\.AddListener\(\s*DungeonToastView\.Show\s*\)").Count;
            if (wires < 2)
                fails.Add($"DungeonController wires only {wires} ToastRequested->DungeonToastView.Show (need checkpoint + crafting = 2)");

            // (3) D14 — Bryn gets the sink AND the previously-dead pickers are now called.
            if (!ctrlTxt.Contains("SetToastSink(DungeonToastView.Show)"))
                fails.Add("DungeonController does not give Bryn the toast sink (SetToastSink)");
            if (!brynTxt.Contains("PickFirstMeetLine"))
                fails.Add("Bryn does not call WandererDialogue.PickFirstMeetLine (still dead code — D14)");
            if (!brynTxt.Contains("PickIdleLine"))
                fails.Add("Bryn does not call WandererDialogue.PickIdleLine (still dead code — D14)");

            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_TOAST_OK");
                reason = "DUNGEON TOAST OK — code toast surfaces checkpoint + craft feedback; Bryn's first-meet/idle pickers are live";
                return true;
            }
            reason = "dungeon-toast: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_TOAST_FAIL: " + reason);
            return false;
        }
    }
}
