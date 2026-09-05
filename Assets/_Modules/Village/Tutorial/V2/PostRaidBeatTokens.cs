// =============================================================================
// PostRaidBeatTokens - the LIVE numbers inside the post-first-raid dialogue (WO-1389).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The post-raid beat (tutorial-steps.json ctx_post_raid -> dialogues.json
// tut_ctx_post_raid) says "Army 3 / 10", "Footman L3 unlocks Sweeping Cut" and
// "The Broken Garrison opens at 3 wins - Iron walls . 15 defenders". Every one of
// those numbers lives in a catalog or the save (ArmyStorage cap, troop-upgrades.json,
// scene-configs.json, GameState.RaidVictories). WO-1389's own draft copy carried
// "stone walls, 12 defenders" for a camp whose data reads Iron / 15 - the copied-state
// drift CLAUDE.md sec.2/5/16 keep paying for. So the SENTENCE is authored in
// dialogues.json and the NUMBERS are "{token}"s resolved here at surface time
// (DeNelle.Core.Dialogue.DialogueTextTokens, read by DialogueViewModel.OnLine).
//
// Every resolver reads the ONE existing formula for its number and never a second
// arithmetic: ArmyReadiness.Compute (the raid gate's own snapshot),
// BarracksProgression.NextAbilityLine (the Troops card's own line),
// RaidSelectionVM.NextLockedCamp + ScoutLine (the raid grid's own lock + scout line).
// A resolver that cannot answer returns a NAMED fallback sentence, never a blank, and
// says so in the trace; a throwing resolver is caught by DialogueTextTokens and leaves
// its token visible on screen ("{army.used}"), which is a greppable defect.
//
// Boots itself once per process (RuntimeInitializeOnLoadMethod), like the other
// Village bus emitters (TutorialSignalAdapters, DialogueCommandSink).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Dialogue;
using DeNelle.Core.State;
using DeNelle.Village.Hero;

namespace DeNelle.Village
{
    /// <summary>Registers the WO-1389 dialogue text tokens. Pure reads; no state of its own.</summary>
    public static class PostRaidBeatTokens
    {
        // Token keys (no braces). The dialogue authors "{army.used}" etc.; the regression
        // oracle scans tut_ctx_post_raid for every "{...}" and asserts each key is quoted here.
        public const string ArmyUsed      = "army.used";
        public const string ArmyCap       = "army.cap";
        public const string ArmyMissing   = "army.missing";
        public const string FootmanNext   = "footman.next";
        public const string CampNextName  = "camp.next.name";
        public const string CampNextWins  = "camp.next.wins";
        public const string CampNextScout = "camp.next.scout";
        /// <summary>The whole camp sentence, so a climbed ladder reads as a sentence and never
        /// as "{camp.next.name} opens at {camp.next.wins} wins" with holes in it.</summary>
        public const string CampNextLine  = "camp.next.line";

        /// <summary>The troop the beat teaches on - the StarterArmyGrant unit (troops.json id).</summary>
        public const string TaughtTroopId = "troop-footman";

        private static bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() => Register();

        /// <summary>Idempotent. Public so a headless oracle can register without a scene load.</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            DialogueTextTokens.Register(ArmyUsed,      () => ArmySnapshot(out int used, out _) ? used.ToString() : "0");
            DialogueTextTokens.Register(ArmyCap,       () => ArmySnapshot(out _, out int cap) ? cap.ToString() : "0");
            DialogueTextTokens.Register(ArmyMissing,   () => ArmySnapshot(out int used, out int cap) ? Mathf.Max(0, cap - used).ToString() : "0");
            DialogueTextTokens.Register(FootmanNext,   FootmanNextLine);
            DialogueTextTokens.Register(CampNextName,  () => { var d = NextCamp(); return d != null ? CampName(d) : "Every camp"; });
            DialogueTextTokens.Register(CampNextWins,  () => { var d = NextCamp(); return d != null ? d.unlockVictories.ToString() : "0"; });
            DialogueTextTokens.Register(CampNextScout, () => { var d = NextCamp(); string s = d != null ? RaidSelectionVM.ScoutLine(d) : null; return s ?? "its walls and garrison unscouted"; });
            DialogueTextTokens.Register(CampNextLine,  CampNextSentence);
            FlowTrace.Step("Tutorial", "PostRaidBeatTokens registered 8 dialogue text tokens (" + ArmyUsed + ", " +
                ArmyCap + ", " + ArmyMissing + ", " + FootmanNext + ", " + CampNextName + ", " + CampNextWins + ", " +
                CampNextScout + ", " + CampNextLine + ").");
        }

        // -- Army: the raid gate's own snapshot --------------------------------

        /// <summary>Roster slots in use (incl. wounded) / cap, from ArmyReadiness.Compute - the
        /// number the raid door judged. False (and a Warn) when there is no save to read.</summary>
        private static bool ArmySnapshot(out int used, out int cap)
        {
            used = 0; cap = 0;
            var st = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (st == null || st.Army == null)
            {
                FlowTrace.Warn("Tutorial", "post-raid token: no GameState/Army to read - army numbers fall back to 0.");
                return false;
            }
            var snap = ArmyReadiness.Compute(st);
            used = snap.RosterSlots;
            cap = snap.CapSlots;
            FlowTrace.Step("Tutorial", "post-raid token: army " + used + " / " + cap + ".");
            return true;
        }

        // -- Footman: the Troops card's own next-unlock line -------------------

        private static string FootmanNextLine()
        {
            int level = Mathf.Max(1, BarracksService.TroopLevel(TaughtTroopId));
            string line = BarracksProgression.NextAbilityLine(TaughtTroopId, level);
            if (string.IsNullOrEmpty(line))
            {
                FlowTrace.Warn("Tutorial", "post-raid token: '" + TaughtTroopId + "' L" + level +
                    " has no ability above its level (troop-upgrades.json) - falling back to a levels-only line.");
                return "grows stronger with every level";
            }
            FlowTrace.Step("Tutorial", "post-raid token: footman.next -> \"" + line + "\" (L" + level + ").");
            return line;
        }

        // -- Camp: the raid grid's own lock + scout line -----------------------

        private static SceneConfigDef NextCamp()
        {
            var st = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            int victories = st != null ? st.RaidVictories : 0;
            if (st == null)
                FlowTrace.Warn("Tutorial", "post-raid token: no GameState - treating the player as 0 raid victories.");
            return RaidSelectionVM.NextLockedCamp(victories);
        }

        private static string CampName(SceneConfigDef d) =>
            !string.IsNullOrEmpty(d.displayName) ? d.displayName : (d.id ?? "The next camp");

        /// <summary>"The Broken Garrison opens at 3 wins - Iron walls . 15 defenders", or the
        /// climbed-ladder sentence. The scout half is dropped (not blanked) when unauthored.</summary>
        private static string CampNextSentence()
        {
            var d = NextCamp();
            if (d == null)
            {
                FlowTrace.Step("Tutorial", "post-raid token: camp.next.line -> ladder climbed (no locked camp).");
                return "Every camp on the map is open to you - each one holds more than the last";
            }
            string scout = RaidSelectionVM.ScoutLine(d);
            string line = CampName(d) + " opens at " + d.unlockVictories +
                          (d.unlockVictories == 1 ? " win" : " wins") +
                          (string.IsNullOrEmpty(scout) ? "" : " - " + scout);
            if (string.IsNullOrEmpty(scout))
                FlowTrace.Warn("Tutorial", "post-raid token: camp '" + d.id + "' authors no wallTier/garrison - " +
                    "the camp sentence names the wins only.");
            FlowTrace.Step("Tutorial", "post-raid token: camp.next.line -> \"" + line + "\".");
            return line;
        }
    }
}
