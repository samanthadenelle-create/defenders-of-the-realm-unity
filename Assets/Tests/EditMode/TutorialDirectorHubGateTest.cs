using System.IO;
using NUnit.Framework;
using DeNelle.Core;

namespace DeNelle.Tests.EditMode
{
    /// <summary>
    /// Regression guard for the SCRIPTED-COMPANION-MEETING-NEVER-STARTS bug
    /// (castle-start flow, 2026-06-12): the companion BODY appeared in MainCastle_Hall
    /// (StoryCompanionInjector already gates on <see cref="HubScenes.IsHub"/>), but the
    /// scripted YarnSpinner companion meeting never played. ROOT CAUSE: TutorialDirector
    /// — the FTUE master sequencer that hosts the companion meeting / brief hook — was
    /// hard-gated to the abandoned "Village2": its Awake() did
    ///     if (SceneManager.GetActiveScene().name != "Village2") Destroy(gameObject);
    /// so in MainCastle_Hall the director self-destructed in Awake and NEITHER the Yarn
    /// CompanionMeeting NOR the fast-path brief hook ever fired. The fix re-points that
    /// gate through HubScenes.IsHub (covers MainCastle_Hall AND Village2).
    ///
    /// <para>Why a SOURCE-level assertion: TutorialDirector's scene gate is a private
    /// Awake() that calls SceneManager.GetActiveScene() — it cannot be exercised in
    /// EditMode without loading a scene. The sibling <see cref="CompanionEntryHubGateTest"/>
    /// only checks the HubScenes data contract and would have stayed GREEN while
    /// TutorialDirector was still hardcoded to "Village2" — i.e. it is a PROXY, not a
    /// guard for THIS bug. This test reads the TutorialDirector source directly so it
    /// goes RED the moment someone re-hardcodes a "Village2"-only meeting gate and
    /// fails to route through HubScenes.IsHub.</para>
    /// </summary>
    public class TutorialDirectorHubGateTest
    {
        // Resolve off Application.dataPath (== <project>/Assets), the robust pattern the
        // sibling ModalPanelDisciplineTests already uses — not the working directory.
        private const string SourceRelPath =
            "_Modules/Village/Tutorial/TutorialDirector.cs";

        private static string ReadSource()
        {
            string full = Path.Combine(UnityEngine.Application.dataPath, SourceRelPath);
            Assert.IsTrue(File.Exists(full),
                "TutorialDirector.cs not found at " + full +
                " — update SourceRelPath if the FTUE sequencer moved.");
            return File.ReadAllText(full);
        }

        // Strip /* */ and // comments so the gate check sees CODE only — explanatory
        // comments legitimately mention "Village2" (documenting the fix); the BUG is a
        // "Village2" literal in actual gate CODE.
        private static string CodeOnly(string src)
        {
            src = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            src = System.Text.RegularExpressions.Regex.Replace(src, @"//[^\n]*", "");
            return src;
        }

        [Test]
        public void TutorialDirector_GatesOnHubScenes_NotHardcodedVillage2()
        {
            string src = ReadSource();

            Assert.IsTrue(src.Contains("HubScenes.IsHub"),
                "TutorialDirector must gate its FTUE / companion-meeting on HubScenes.IsHub " +
                "(so it runs in the castle hub MainCastle_Hall, not just Village2). Without " +
                "this the scripted YarnSpinner companion meeting never starts in the castle.");

            // The broken state was a literal "Village2" scene-name compare in the gate.
            // After the fix there is NO hardcoded "Village2" string anywhere in the
            // director — the canonical hub list owns the scene set. A re-introduced
            // "Village2" literal re-creates exactly the bug, so trip on it.
            Assert.IsFalse(CodeOnly(src).Contains("\"Village2\""),
                "TutorialDirector must NOT hardcode the scene name \"Village2\" in CODE — that is the " +
                "companion-meeting-never-starts regression (the director self-destructs in " +
                "MainCastle_Hall). Route the scene gate through HubScenes.IsHub instead.");
        }

        // Belt-and-braces: the HubScenes contract the gate now depends on MUST keep the
        // castle hub. (Mirrors CompanionEntryHubGateTest but pinned to THIS bug's gate.)
        [Test]
        public void HubGate_TheTutorialDirectorReliesOn_RecognizesTheCastleHub()
        {
            Assert.IsTrue(HubScenes.IsHub("MainCastle_Hall"),
                "MainCastle_Hall MUST be a hub — else TutorialDirector.Awake destroys the " +
                "director in the castle and the scripted companion meeting never starts.");
            Assert.IsTrue(HubScenes.IsHub("Village2"),
                "Village2 must stay a hub so widening the gate to the castle does not regress " +
                "the original village FTUE flow.");
        }
    }
}
