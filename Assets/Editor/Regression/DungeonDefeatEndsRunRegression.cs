// =============================================================================
// DungeonDefeatEndsRunRegression [dungeon-defeat] — locks WO-770.3 (fixes D4):
// a LOST dungeon (ATB) fight must end the run and return to the Village, not be
// silently treated as a victory. Guards the whole carrier chain so the old
// `bool victory = true` hardcode can never creep back:
//   1. SceneRouter defines the Core-level BattleResultKind carrier + LastOutcome.
//   2. BattleController.HandleOutcome STAMPS LastOutcome before the hand-back.
//   3. DungeonController.ResolvePendingEncounter READS the carrier (no hardcode) and routes
//      through the shared SettleEncounter (770.3b), whose defeat path calls ExitToVillage().
// Source-lint (edit-mode, no PlayMode). Wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonDefeatEndsRunRegression
    {
        public static bool Run(out string reason)
        {
            string router = Path.Combine(Application.dataPath, "_Modules/Core/SceneRouter.cs");
            string battle = Path.Combine(Application.dataPath, "_Modules/BattleATB/BattleController.cs");
            string ctrl   = Path.Combine(Application.dataPath, "_Modules/Dungeons/DungeonController.cs");

            var fails = new List<string>();

            // (1) CARRIER — Core-level result enum + the field on the handoff.
            string routerTxt = File.Exists(router) ? File.ReadAllText(router) : "";
            if (!Regex.IsMatch(routerTxt, @"enum\s+BattleResultKind"))
                fails.Add("SceneRouter defines no BattleResultKind enum (the Core-level result carrier)");
            if (!Regex.IsMatch(routerTxt, @"BattleResultKind\s+LastOutcome"))
                fails.Add("BattleParams has no LastOutcome carrier field");

            // (2) WRITER — BattleController stamps the outcome before the hand-back.
            string battleTxt = File.Exists(battle) ? File.ReadAllText(battle) : "";
            if (!Regex.IsMatch(battleTxt, @"LastOutcome\s*=\s*"))
                fails.Add("BattleController.HandleOutcome does not stamp PendingBattle.LastOutcome");

            // (3) READER — DungeonController reads the carrier, the hardcode is GONE, and the ATB
            //     resume routes through the shared SettleEncounter authority (refactored in WO-770.3b).
            string ctrlTxt = File.Exists(ctrl) ? File.ReadAllText(ctrl) : "";
            // Isolate ResolvePendingEncounter so a match elsewhere can't mask a regression.
            var m = Regex.Match(ctrlTxt,
                @"void\s+ResolvePendingEncounter\s*\(\s*\)\s*\{(?<body>.*?)\n        \}",
                RegexOptions.Singleline);
            string body = m.Success ? m.Groups["body"].Value : ctrlTxt;
            if (Regex.IsMatch(body, @"bool\s+victory\s*=\s*true\s*;"))
                fails.Add("ResolvePendingEncounter still hardcodes `bool victory = true` — a lost fight is scored a win (D4)");
            if (!Regex.IsMatch(body, @"LastOutcome\s*==\s*BattleResultKind\.Victory"))
                fails.Add("ResolvePendingEncounter does not read the LastOutcome carrier to decide victory");
            if (!body.Contains("SettleEncounter("))
                fails.Add("ResolvePendingEncounter does not route through the shared SettleEncounter authority (770.3b)");

            // The locked defeat behavior (ExitToVillage on a loss) now lives in SettleEncounter —
            // assert it there (both battle paths funnel through this one routine).
            var ms = Regex.Match(ctrlTxt,
                @"void\s+SettleEncounter\s*\([^)]*\)\s*\{(?<body>.*?)\n        \}",
                RegexOptions.Singleline);
            string settleBody = ms.Success ? ms.Groups["body"].Value : "";
            if (!settleBody.Contains("ExitToVillage"))
                fails.Add("SettleEncounter does not ExitToVillage() on defeat (locked defeat behavior missing)");

            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_DEFEAT_OK");
                reason = "DUNGEON DEFEAT OK — carrier + stamp + read all present; a lost ATB fight ends the run to Village (no false win)";
                return true;
            }
            reason = "dungeon-defeat: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_DEFEAT_FAIL: " + reason);
            return false;
        }
    }
}
