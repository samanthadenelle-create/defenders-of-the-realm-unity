using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;

namespace DeNelle.Core.HudModel
{
    /// <summary>Pure, single-line state copy for the two actionable Journey cards.</summary>
    public sealed class JourneyDeckSubtitleVM
    {
        public string QuestsSubtitle { get; }
        public string RaidsSubtitle { get; }

        public JourneyDeckSubtitleVM(int activeQuests, int readyToClaim,
                                     int armyUsed, int armyCap, int openCamps)
        {
            activeQuests = NonNegative(activeQuests);
            readyToClaim = NonNegative(readyToClaim);
            armyUsed = NonNegative(armyUsed);
            armyCap = NonNegative(armyCap);
            openCamps = NonNegative(openCamps);

            QuestsSubtitle = activeQuests + " active . " + readyToClaim + " ready to claim";
            RaidsSubtitle = "Army " + armyUsed + " / " + armyCap + " . " +
                (openCamps > 0
                    ? openCamps + (openCamps == 1 ? " camp open" : " camps open")
                    : "train to open a camp");
        }

        public static JourneyDeckSubtitleVM FromCurrentState()
        {
            int active = 0;
            int ready = 0;
            int used = 0;
            int cap = 0;
            int camps = 0;

            Guard.Try("Journey", "read Journey deck subtitle state", () =>
            {
                var quests = QuestService.Instance;
                if (quests != null) active = quests.ActiveQuestIds().Count;

                // WO-1521 - READ THE ONE AUTHORITY, never a second copy of the predicate.
                // This used to inline `quest.Completed && quest.ClaimedAtUnix == 0` over
                // today's set. That copy is why this card could say "1 ready to claim" while
                // Brom's Rumor Board said "The board is quiet": two surfaces, two lists, no
                // shared fact. DailyQuestService.ClaimableCount is now the single count and
                // RumorBoardVM projects a row from the SAME predicate.
                var daily = DailyQuestService.Instance;
                if (daily != null) ready = daily.ClaimableCount;

                used = PostureSignals.ArmyFillUsed;
                cap = PostureSignals.ArmyFillCap;
                camps = PostureSignals.RaidOpenCampCount;
            });

            return new JourneyDeckSubtitleVM(active, ready, used, cap, camps);
        }

        private static int NonNegative(int value) => value < 0 ? 0 : value;
    }
}
