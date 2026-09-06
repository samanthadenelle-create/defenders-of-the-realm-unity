#if UNITY_EDITOR
using System;
using System.IO;
using DeNelle.Core.HudModel;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1404: Journey subtitles carry actionable state and fit one line.</summary>
    public static class JourneyDeckSubtitleRegression
    {
        public static bool Run(out string result)
        {
            try
            {
                var fixture = new JourneyDeckSubtitleVM(2, 1, 3, 10, 1);

                // RED recipe: revert PlayerDeckWorkspace to the old literal / RaidsCardPurpose.
                string deck = File.ReadAllText("Assets/_Modules/HUD/PlayerDeckWorkspace.cs");
                RequireContains(deck, "TraceJourneySubtitle(\"Quests\", journey.QuestsSubtitle)", "Quests binding");
                RequireContains(deck, "TraceJourneySubtitle(\"Raids\", journey.RaidsSubtitle)", "Raids binding");
                Forbid(deck, "\"Read active quests and realm rumors\"");
                Forbid(deck, "\"Choose a camp and deploy your army\"");

                string timer = File.ReadAllText("Assets/_Modules/Village/Buildings/BuildTimerService.cs");
                // RED recipe: remove the IsLocked guard from the open-camp loop.
                RequireContains(timer, "if (_journeyRaidProjection.IsLocked(id)) continue;", "camp escalation lock");
                // RED recipe: remove the unchanged-input early return in PublishJourneyOpenCamps.
                RequireContains(timer,
                    "if (!projectionChanged && _journeyRaidDeployableInput == deployableBodies) return;",
                    "camp projection cache");
                // RED recipe: change <= back to < in the newly authored open-camp predicate.
                RequireContains(timer, "GarrisonCount(def) <= deployableBodies", "camp deployable predicate");

                // RED recipe: remove the Journey fixture's SetArmyFill(0, 10) publish.
                string capture = File.ReadAllText("Assets/Editor/UICaptureLaunch.cs");
                RequireContains(capture, "PostureSignals.SetArmyFill(0, 10)", "Journey capture army cap");

                // RED recipe: remove armyUsed/armyCap from the Raids composer.
                RequireContains(fixture.RaidsSubtitle, "3 / 10", "Raids army state");
                // RED recipe: replace the open-camp branch with the old training verb phrase.
                RequireContains(fixture.RaidsSubtitle, "1 camp", "Raids open-camp state");
                // RED recipe: restore "Read active quests and realm rumors".
                RequireContains(fixture.QuestsSubtitle, "active", "Quests active state");

                // RED recipe: append "..." in either composer.
                ForbidBoth(fixture, "...");
                // RED recipe: restore either old card verb.
                ForbidBoth(fixture, "Choose");
                ForbidBoth(fixture, "Read");

                // RED recipe: return blank when no camp passes the deployment predicate.
                var zero = new JourneyDeckSubtitleVM(0, 0, 0, 10, 0);
                RequireContains(zero.RaidsSubtitle, "Army 0 / 10", "zero-army state");
                RequireContains(zero.RaidsSubtitle, "train to open a camp", "zero-camp remedy");

                result = "JOURNEY_DECK_SUBTITLE_OK fixture='" + fixture.QuestsSubtitle +
                         "' / '" + fixture.RaidsSubtitle + "'";
                return true;
            }
            catch (Exception ex)
            {
                result = "JOURNEY_DECK_SUBTITLE_FAIL " + ex.Message;
                return false;
            }
        }

        private static void RequireContains(string value, string token, string label)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(label + " missing '" + token + "' in '" + value + "'.");
        }

        private static void ForbidBoth(JourneyDeckSubtitleVM vm, string token)
        {
            if (vm.QuestsSubtitle.IndexOf(token, StringComparison.Ordinal) >= 0 ||
                vm.RaidsSubtitle.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Legacy Journey subtitle token returned: " + token);
        }

        private static void Forbid(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Legacy Journey source token returned: " + token);
        }
    }
}
#endif
