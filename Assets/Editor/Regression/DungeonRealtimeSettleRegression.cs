// =============================================================================
// DungeonRealtimeSettleRegression [dungeon-defeat-realtime] — locks WO-770.3b: the
// real-time BattleArena path (the DEFAULT, ff.dungeonrealtime ON) has NO scene
// round-trip, so it never reaches ResolvePendingEncounter. Without a bridge nothing
// clears the combat lock, credits the boss, or ends the run on a loss — leaving the
// default path unguarded while the ATB path (770.3) is locked. This asserts:
//   1. ONE shared settle authority (SettleEncounter) exists, with the 770.3 defeat
//      contract (ExitToVillage on a loss, no boss credit) baked in.
//   2. DungeonController subscribes AND unsubscribes BattleArena.OnBattleEnded (no leak).
//   3. BOTH paths route through SettleEncounter — the ATB resume AND the real-time handler
//      — so a win/loss behaves identically whichever path ran (parity).
// Source-lint (edit-mode, no PlayMode). Wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonRealtimeSettleRegression
    {
        public static bool Run(out string reason)
        {
            string ctrl = Path.Combine(Application.dataPath, "_Modules/Dungeons/DungeonController.cs");
            if (!File.Exists(ctrl))
            {
                reason = "DUNGEON REALTIME SETTLE: DungeonController.cs not found — re-point this oracle";
                Debug.LogError("DUNGEON_REALTIME_SETTLE_FAIL: " + reason);
                return false;
            }
            string text = File.ReadAllText(ctrl);
            var fails = new List<string>();

            // (1) ONE settle authority, with the 770.3 defeat contract inside it.
            var m = Regex.Match(text,
                @"void\s+SettleEncounter\s*\([^)]*\)\s*\{(?<body>.*?)\n        \}",
                RegexOptions.Singleline);
            if (!m.Success)
                fails.Add("no shared SettleEncounter(...) authority");
            else
            {
                string body = m.Groups["body"].Value;
                if (!body.Contains("ExitToVillage"))
                    fails.Add("SettleEncounter defeat path does not ExitToVillage (770.3 defeat contract missing)");
                if (!Regex.IsMatch(body, @"ResumeAfterEncounter\(false\)"))
                    fails.Add("SettleEncounter defeat path does not clear the lock without a boss credit (ResumeAfterEncounter(false))");
                if (!Regex.IsMatch(body, @"ResumeAfterEncounter\(true\)"))
                    fails.Add("SettleEncounter victory path does not settle the run (ResumeAfterEncounter(true))");
            }

            // (2) Subscribe AND unsubscribe the arena event — a persistent (DontDestroyOnLoad)
            //     singleton means a missing unsubscribe is a real leak / stale-controller bug.
            if (!text.Contains("OnBattleEnded += OnRealtimeBattleEnded"))
                fails.Add("DungeonController never subscribes BattleArena.OnBattleEnded (real-time path never settles)");
            if (!text.Contains("OnBattleEnded -= OnRealtimeBattleEnded"))
                fails.Add("DungeonController never unsubscribes BattleArena.OnBattleEnded (leak / stale-controller settle)");

            // (3) PARITY — both paths route through the one authority.
            if (!Regex.IsMatch(text, @"OnRealtimeBattleEnded[\s\S]{0,400}SettleEncounter\("))
                fails.Add("the real-time handler does not call SettleEncounter");
            if (!Regex.IsMatch(text, @"ResolvePendingEncounter[\s\S]{0,900}SettleEncounter\("))
                fails.Add("the ATB resume (ResolvePendingEncounter) does not call SettleEncounter — paths diverged");

            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_REALTIME_SETTLE_OK");
                reason = "DUNGEON REALTIME SETTLE OK — one SettleEncounter authority, arena hook sub/unsub, both paths in parity";
                return true;
            }
            reason = "dungeon-defeat-realtime: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_REALTIME_SETTLE_FAIL: " + reason);
            return false;
        }
    }
}
