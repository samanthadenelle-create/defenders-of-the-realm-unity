// =============================================================================
// DungeonReturnSceneRegression [dungeon-return] — locks WO-770.2 (fixes D3): a
// dungeon encounter must round-trip back to the CURRENT dungeon scene, never a
// hardcoded one. The old bug set ReturnScene = SceneRouter.DungeonHealersCottage
// on BOTH the real-time and legacy battle paths, so a Folk's Granary fight dumped
// the player into the Cottage. Source-lint (edit-mode, no PlayMode) wired into
// DataRegression.RunAll. Never throws.
// =============================================================================
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonReturnSceneRegression
    {
        public static bool Run(out string reason)
        {
            string path = Path.Combine(Application.dataPath,
                "_Modules/Dungeons/EncounterTrigger.cs");
            if (!File.Exists(path))
            {
                reason = "DUNGEON RETURN: EncounterTrigger.cs not found — re-point this oracle";
                Debug.LogError("DUNGEON_RETURN_FAIL: " + reason);
                return false;
            }

            string text = File.ReadAllText(path);

            // Strip comment-only lines so a doc mention of the old const isn't flagged.
            var codeLines = new System.Collections.Generic.List<string>();
            foreach (var raw in text.Split('\n'))
            {
                string t = raw.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                codeLines.Add(raw);
            }
            string code = string.Join("\n", codeLines);

            // (1) The hardcoded return must be GONE from live code.
            bool hardcoded = Regex.IsMatch(code,
                @"ReturnScene\s*=\s*SceneRouter\.Dungeon\w+");
            // (2) The fix must source the return from the ACTIVE scene.
            bool usesActive = code.Contains("GetActiveScene");

            if (hardcoded)
            {
                reason = "DUNGEON RETURN: EncounterTrigger still hardcodes ReturnScene to a fixed " +
                         "dungeon (WO-770.2/D3 regressed) — a non-Cottage fight would return to the wrong scene";
                Debug.LogError("DUNGEON_RETURN_FAIL: " + reason);
                return false;
            }
            if (!usesActive)
            {
                reason = "DUNGEON RETURN: EncounterTrigger no longer computes ReturnScene from " +
                         "SceneManager.GetActiveScene() — the round-trip destination is unverified";
                Debug.LogError("DUNGEON_RETURN_FAIL: " + reason);
                return false;
            }

            Debug.Log("DUNGEON_RETURN_OK");
            reason = "DUNGEON RETURN OK — EncounterTrigger returns to the active dungeon scene, no fixed hardcode";
            return true;
        }
    }
}
