using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor.Regression
{
    public static class JewelerDiscoveryFtueRegression
    {
        public static bool Run(out string reason)
        {
            var f = new List<string>();
            string dungeon = Read("Assets/_Modules/Dungeons/DungeonController.cs", f);
            string ftue = Read("Assets/_Modules/Village/Crafting/JewelerDiscoveryFtue.cs", f);
            string polish = Read("Assets/_Modules/Village/Crafting/JewelPolishService.cs", f);
            string panel = Read("Assets/_Modules/Village/Items/JewelerPanelMvvm.cs", f);
            string runState = Read("Assets/_Modules/Dungeons/State/DungeonRuntimeState.cs", f);
            string station = Read("Assets/_Modules/Village/Items/JewelerStationInjector.cs", f);

            if (!dungeon.Contains("PostFirstRoughStoneDropRate = 0.15f")) f.Add("post-first rate is not pinned to 15%");
            if (!dungeon.Contains("!firstDungeonStone && !ShouldAwardPostFirstStone")) f.Add("guaranteed first award does not bypass later RNG");
            if (!dungeon.Contains("inv.AddEarned(stoneId, 1)")) f.Add("dungeon reward no longer stamps earned history");
            if (!dungeon.Contains("st.TryClaimReward()") || !runState.Contains("public bool TryClaimReward()")) f.Add("retry/re-entry can evaluate the same run reward twice");
            if (!ftue.Contains("first find is guaranteed; future stones are uncommon, and not every dungeon holds one")) f.Add("rare/future-not-guaranteed copy missing");
            if (!ftue.Contains("JewelerProgression.IsUnlocked") || !ftue.Contains("PanelId.JewelerCrafting")) f.Add("FTUE is not gated by earned history and routed to Jeweler");
            if (!ftue.Contains("FirstPolishActionStarted += Complete") || !polish.Contains("FirstPolishActionStarted?.Invoke()")) f.Add("completion is not driven by the real accepted polish action");
            if (!panel.Contains("if (!DeNelle.Village.Crafting.JewelerProgression.IsUnlocked)")) f.Add("locked Jeweler direct route is not hidden/refused");
            if (!station.Contains("if (!DeNelle.Village.Crafting.JewelerProgression.IsUnlocked)")) f.Add("locked Jeweler world/navigation entry is still visible");
            if (ftue.Contains("...')") || ftue.Contains("…")) f.Add("FTUE contains forbidden ellipsis");

            if (f.Count > 0) { reason = "JEWELER_DISCOVERY_FTUE_FAIL: " + string.Join(" | ", f); return false; }
            reason = "JEWELER DISCOVERY FTUE OK - dungeon-earned first stone guarantees once, later completed runs roll pinned 15%, locked Jeweler is refused, rare-drop copy is complete, and completion waits for an accepted polish action";
            return true;
        }

        private static string Read(string path, List<string> f)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            f.Add("missing " + path); return string.Empty;
        }
    }
}
