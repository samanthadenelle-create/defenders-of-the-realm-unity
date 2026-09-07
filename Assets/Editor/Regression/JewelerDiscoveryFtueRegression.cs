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

            // =================================================================
            //  WO-1600 - WHERE the card may appear, WHEN it must leave, and the
            //  geometry that keeps it inside its own plate.
            //
            //  All five checks below are RED against the source that shipped in
            //  build 2026.09.07.359651 (the owner's 13:23 Title-screen frame):
            //  that version gated only on `scene.Contains("Dungeon")`, never
            //  heard the reset, laid its fractions straight on chrome.content,
            //  declared TextOverflowModes.Overflow, and emitted no trace at all.
            // =================================================================

            // (1) THE SCENE GATE, POSITIVE. An exclusion list is only ever as long
            //     as the last bug - "Dungeon" did not describe the Title screen.
            if (!ftue.Contains("HubScenes.IsHub(sceneName)") ||
                !ftue.Contains("HubScenes.SuppressTownHud(sceneName)") ||
                !ftue.Contains("HasLiveHero"))
                f.Add("the discovery card is not gated to a HOME HUB with a live hero - it can " +
                      "raise itself over the Title / HeroSelect / a raid target, which is exactly " +
                      "the owner's 2026-09-07 13:23 frame");

            // (2) THE STANDING CARD. This component is DontDestroyOnLoad, so a card
            //     raised before START NEW rides the reset into the new save unless the
            //     reset is heard AND a failing gate dismisses what is already open.
            if (!ftue.Contains("GameStateService.NewGameStarted += OnNewGameStarted") ||
                !ftue.Contains("GameStateService.NewGameStarted -= OnNewGameStarted"))
                f.Add("the FTUE does not subscribe/unsubscribe GameStateService.NewGameStarted - a " +
                      "card standing when the player presses START NEW survives the reset that " +
                      "erased the earned-stone history it reports");
            if (!ftue.Contains("if (_modal != null) Close();"))
                f.Add("TryPresent does not DISMISS an open card when the gate stops holding - it " +
                      "only ever refuses to open a new one, so a mis-placed card is permanent");

            // (3) GEOMETRY. chrome.content is the kit's own "unprotected legacy class":
            //     the shared Close is seated there at the default bottom band as a fixed
            //     360x120 box, and a raw 0.10-0.29 fraction lands on top of it.
            //     chrome.layout.body is the zone whose floor the kit already raises above
            //     that band (BuildObsidianPanel's close-band reservation, WO-714 P6).
            if (!ftue.Contains("_modal.chrome.layout.body"))
                f.Add("the card lays its copy and its verb on raw chrome.content fractions instead " +
                      "of the reserved chrome.layout.body well - that is what put OPEN CRAFTING: " +
                      "JEWELER on top of the modal's own CLOSE");

            // (4) NO OVERFLOW. It was DEAD code (FitBlock sets Truncate one statement
            //     later) and it misdirected the reader about what spilled; pinned absent
            //     so nobody restores it as a "fix" for copy that does not fit.
            if (ftue.Contains("TextOverflowModes.Overflow"))
                f.Add("the discovery body still declares TextOverflowModes.Overflow - FitBlock " +
                      "overwrites it with Truncate, so the line proves nothing and teaches the " +
                      "next reader the wrong cause");

            // (5) THE TRACE (§12). The gate must say, on every evaluation, which scene it
            //     saw and which carrier decided it - so the next occurrence is one read.
            if (!ftue.Contains("FlowTrace.Step(\"JewelerFtue\"") ||
                !ftue.Contains("carrier=GameState.EverAcquiredItemIds"))
                f.Add("TryPresent is not instrumented with the [Flow:JewelerFtue] scene/unlocked/" +
                      "completed line naming the EverAcquiredItemIds carrier");

            if (f.Count > 0) { reason = "JEWELER_DISCOVERY_FTUE_FAIL: " + string.Join(" | ", f); return false; }
            reason = "JEWELER DISCOVERY FTUE OK - dungeon-earned first stone guarantees once, later completed runs roll pinned 15%, locked Jeweler is refused, rare-drop copy is complete, completion waits for an accepted polish action, and the card is gated to a home hub with a live hero, dismissed on New Game, and seated in the reserved body well clear of the shared Close";
            return true;
        }

        private static string Read(string path, List<string> f)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            f.Add("missing " + path); return string.Empty;
        }
    }
}
