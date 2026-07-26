// =============================================================================
// DungeonStateResetRegression [dungeon-state-reset] — locks WO-770.9 (fixes D11):
// DungeonRuntimeState.OnEnable must reset the run IDENTITY (_dungeonId/_currentRoomId)
// and the progress lists, not just the flags — else a fresh session reads the PRIOR
// run's dungeon/room/read-lists in the window before StartRun overwrites them.
// Source-lint (edit-mode) wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonStateResetRegression
    {
        public static bool Run(out string reason)
        {
            string path = Path.Combine(Application.dataPath,
                "_Modules/Dungeons/State/DungeonRuntimeState.cs");
            if (!File.Exists(path))
            {
                reason = "DUNGEON STATE RESET: DungeonRuntimeState.cs not found — re-point this oracle";
                Debug.LogError("DUNGEON_STATE_RESET_FAIL: " + reason);
                return false;
            }

            string text = File.ReadAllText(path);
            // Isolate the OnEnable body so an unrelated clear elsewhere can't satisfy the check.
            var m = Regex.Match(text, @"void\s+OnEnable\s*\(\s*\)\s*\{(?<body>.*?)\n\s*\}",
                RegexOptions.Singleline);
            string body = m.Success ? m.Groups["body"].Value : "";

            var missing = new List<string>();
            if (!Regex.IsMatch(body, @"_dungeonId\s*=\s*string\.Empty"))      missing.Add("_dungeonId");
            if (!Regex.IsMatch(body, @"_currentRoomId\s*=\s*string\.Empty"))  missing.Add("_currentRoomId");
            if (!Regex.IsMatch(body, @"_loreStonesRead\s*\.\s*Clear\(\)"))    missing.Add("_loreStonesRead.Clear()");
            if (!Regex.IsMatch(body, @"_checkpointsReached\s*\.\s*Clear\(\)")) missing.Add("_checkpointsReached.Clear()");

            if (missing.Count > 0)
            {
                reason = "DUNGEON STATE RESET: DungeonRuntimeState.OnEnable does not clear " +
                         string.Join(", ", missing) + " — the D11 stale-read window (prior run leaking " +
                         "into a fresh session) is open again";
                Debug.LogError("DUNGEON_STATE_RESET_FAIL: " + reason);
                return false;
            }

            Debug.Log("DUNGEON_STATE_RESET_OK");
            reason = "DUNGEON STATE RESET OK — OnEnable clears run identity + progress lists (no stale-read window)";
            return true;
        }
    }
}
