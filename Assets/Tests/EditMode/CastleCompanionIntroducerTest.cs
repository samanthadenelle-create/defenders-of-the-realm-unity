using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using DeNelle.Core;

namespace DeNelle.Tests.EditMode
{
    /// <summary>
    /// Regression guard for the WALK-UP COMPANION-INTRODUCER (owner decision 2026-06-12):
    /// the fragile auto-FTUE companion-intro chain (TutorialDirector fast-path hook +
    /// CompanionMeetingTrigger autostart + SylasFirstMeeting beat) was replaced by a
    /// proximity-Talk NPC — CastleCompanionIntroducerInjector — that the player walks up
    /// to in MainCastle_Hall; on Talk it plays the intro Yarn node and recruits the first
    /// companion.
    ///
    /// <para>This pins the load-bearing wiring so a refactor can't silently break it:
    ///   1. the injector GATES on HubScenes.IsHub (so it spawns in MainCastle_Hall);
    ///   2. the intro routes through TalkPromptRegistry + DialogueService.Play (the
    ///      proximity-Talk path), NOT a re-introduced auto-FTUE chain;
    ///   3. the three legacy auto-paths DEFER to it (check Active) so it is the SINGLE
    ///      intro+recruit trigger.</para>
    ///
    /// Source-level assertions (the proven pattern of TutorialDirectorHubGateTest /
    /// ModalPanelDisciplineTests): the injector's gate is a private Awake() calling
    /// SceneManager.GetActiveScene(), not exercisable in EditMode without loading a scene.
    /// </summary>
    public class CastleCompanionIntroducerTest
    {
        private const string InjectorRel =
            "_Modules/Village/NPCs/CastleCompanionIntroducerInjector.cs";
        private const string CompanionMeetingTriggerRel =
            "_Modules/Village/CompanionMeetingTrigger.cs";
        private const string SylasMeetingRel =
            "_Modules/Village/NPCs/SylasFirstMeeting.cs";
        // NOTE (WO-971, 2026-08-10): TutorialDirectorRel is GONE. The legacy FTUE
        // director was DELETED on the owner ruling "remove the original / only the new
        // wolf one stays" — there is no third legacy auto-intro path left to defer.

        private static string Read(string rel)
        {
            string full = Path.Combine(UnityEngine.Application.dataPath, rel);
            Assert.IsTrue(File.Exists(full), rel + " not found at " + full + " — update the path if it moved.");
            return File.ReadAllText(full);
        }

        // Strip block + line comments so assertions match CODE, not the (intentionally
        // explanatory) header comments.
        private static string CodeOnly(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", "");
            return src;
        }

        [Test]
        public void Introducer_GatesOnHubScenes_SoItSpawnsInTheCastle()
        {
            string src = CodeOnly(Read(InjectorRel));
            Assert.IsTrue(src.Contains("HubScenes.IsHub"),
                "CastleCompanionIntroducerInjector must gate spawning on HubScenes.IsHub so the walk-up " +
                "companion introducer appears in MainCastle_Hall (the castle-start hub), not just Village2. " +
                "Without this the player can never meet/recruit the first companion in the castle.");
        }

        [Test]
        public void Introducer_RoutesIntroThroughTalkAndDialogueService()
        {
            string src = CodeOnly(Read(InjectorRel));

            Assert.IsTrue(src.Contains("TalkPromptRegistry.Register"),
                "The introducer must REGISTER with TalkPromptRegistry so the proximity HUD Talk button " +
                "activates on approach — that is the walk-up trigger that replaced the auto-FTUE chain.");

            Assert.IsTrue(src.Contains("DialogueService.Play"),
                "On Talk the introducer must launch the intro via DialogueService.Play(node) — the Yarn " +
                "node fires <<command: RecruitCompanion>> to join the first companion.");

            Assert.IsTrue(src.Contains("\"SylasFirstMeeting\"") || src.Contains("IntroNode"),
                "The intro must route to the SylasFirstMeeting Yarn node (the authored intro+recruit beat).");
        }

        [Test]
        public void LegacyAutoPaths_DeferToTheIntroducer_SoItIsTheSingleTrigger()
        {
            // Each legacy auto-FTUE companion-intro path must check the introducer's Active
            // gate and stand down — else there'd be two intros / a double recruit.
            string flag = "CastleCompanionIntroducerInjector.Active";

            Assert.IsTrue(CodeOnly(Read(CompanionMeetingTriggerRel)).Contains(flag),
                "CompanionMeetingTrigger must defer to CastleCompanionIntroducerInjector.Active and NOT " +
                "auto-host the full CompanionMeeting when the walk-up introducer owns the intro.");

            Assert.IsTrue(CodeOnly(Read(SylasMeetingRel)).Contains(flag),
                "SylasFirstMeeting (the auto-firing beat) must defer to CastleCompanionIntroducerInjector.Active " +
                "so the recruit can't fire twice (NPC + this beat both calling RecruitCompanion).");

            // The third legacy path (TutorialDirector's fast-path companion hook) no longer
            // exists to defer — WO-971 deleted the legacy director outright, which is a
            // STRONGER guarantee than the assertion it replaces.
            Assert.IsFalse(File.Exists(Path.Combine(UnityEngine.Application.dataPath,
                    "_Modules/Village/Tutorial/TutorialDirector.cs")),
                "TutorialDirector.cs is back. The legacy FTUE director was REMOVED (WO-971, owner ruling " +
                "2026-08-10: 'remove the original', 'only the new wolf one stays'). Restoring it re-introduces " +
                "a second tutorial flow AND a third auto companion-intro path.");
        }

        [Test]
        public void HubGate_RecognizesTheCastleHub()
        {
            Assert.IsTrue(HubScenes.IsHub("MainCastle_Hall"),
                "MainCastle_Hall MUST be a hub — else the introducer injector never spawns in the castle.");
        }
    }
}
