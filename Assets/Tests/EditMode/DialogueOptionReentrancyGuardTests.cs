// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// DialogueOptionReentrancyGuardTests (EditMode) — regression guard for the
// YarnSpinner dialogue-options CRASH (the game freezes during a dialogue that
// shows options, player.log shows thousands of repeated frames of
//   Yarn.Unity.Addons.ClassicRPG.OptionItem:Submit ()  (OptionItem.cs)
//   Yarn.Unity.Addons.ClassicRPG.OptionItem:Update ()  (OptionItem.cs)
// -> RPGDialoguePresenter.RunOptionsAsync continuation -> next options -> repeat,
// recursing until the stack blows / the app hangs).
// -----------------------------------------------------------------------------
// ROOT CAUSE the crash hit: OptionItem.Submit invokes onSubmit ->
// completion.TrySetResult, and the presenter's `await completion.Task` resumes
// SYNCHRONOUSLY INLINE (UniTask/YarnTask continuations run on the current call
// stack). So when one options view is followed by ANOTHER, the next options view
// is built and auto-selects its first item WITHIN the same Submit()/Update()
// call frame; because Keyboard.wasPressedThisFrame stays true for the whole
// frame, that brand-new OptionItem submits again -> next options -> unbounded
// recursion. The original per-INSTANCE `_submitted` flag cannot stop it: every
// cascade level is a DIFFERENT freshly-instantiated OptionItem.
//
// THE FIX has two halves and this suite pins BOTH on the only axis available in
// EditMode (the real runaway is a runtime recursion needing a live EventSystem +
// simulated Keyboard, too fragile to reproduce here — so we assert the STRUCTURE
// of the guard, exactly as ModalPanelDisciplineTests does for its cluster):
//   1. OptionItem holds a GLOBAL (static) "submit in flight" guard that is
//      checked inside Submit() and a same-frame backstop — a per-instance flag
//      alone is NOT sufficient and re-introducing only a per-instance guard must
//      fail here.
//   2. RPGDialoguePresenter.RunOptionsAsync RESETS that global guard once per
//      fresh options view (OptionItem.BeginOptionsView), so each option set
//      accepts exactly one submit while a re-entrant submit during the previous
//      selection's inline unwind is ignored.
//
// The test asmdef cannot reference the vendored Yarn addon assembly, so the
// assertions read the package source files directly (same approach as the
// ModalPanelDisciplineTests structural checks).
// =============================================================================

using System.IO;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class DialogueOptionReentrancyGuardTests
    {
        // OptionItem must carry a STATIC (global) in-flight submit guard. A purely
        // per-instance flag is what shipped before the fix and could NOT stop the
        // cascade (each recursion level is a new instance), so the presence of a
        // static guard referenced from Submit is the load-bearing invariant.
        [Test]
        public void OptionItem_HasStaticSubmitInFlightGuard()
        {
            string src = ReadPackageSource(
                "dev.yarnspinner.unity.addons.classicrpg/Runtime/Scripts/OptionItem.cs");

            Assert.IsTrue(
                System.Text.RegularExpressions.Regex.IsMatch(
                    src, @"static\s+bool\s+s_submitInFlight"),
                "OptionItem must declare a STATIC submit-in-flight guard. A per-instance " +
                "'_submitted' flag alone cannot stop the options-after-options recursion " +
                "because every cascade level is a different freshly-instantiated OptionItem.");
        }

        // Submit() must consult the global guard (and a same-frame backstop), not
        // just the per-instance flag. This is the check that re-introducing a
        // bare per-instance submit would FAIL.
        [Test]
        public void OptionItemSubmit_ChecksGlobalGuardAndSameFrameBackstop()
        {
            string src = ReadPackageSource(
                "dev.yarnspinner.unity.addons.classicrpg/Runtime/Scripts/OptionItem.cs");

            string submitBody = ExtractMethodBody(src, "private void Submit()");

            Assert.IsTrue(submitBody.Contains("s_submitInFlight"),
                "OptionItem.Submit must early-out / set the static s_submitInFlight guard so a " +
                "re-entrant submit raised by the inline continuation (which re-presents options on " +
                "this same call stack) is ignored. Without this the dialogue recurses and freezes.");

            Assert.IsTrue(submitBody.Contains("Time.frameCount"),
                "OptionItem.Submit must use a same-frame backstop (Time.frameCount) so a single held " +
                "Space cannot satisfy two consecutive option views within one frame.");

            Assert.IsTrue(submitBody.Contains("s_submitInFlight = true"),
                "OptionItem.Submit must SET s_submitInFlight before invoking onSubmit so the guard is " +
                "active during the inline continuation's unwind.");
        }

        // The presenter must reset the global guard once per fresh options view —
        // otherwise the FIRST submit latches the guard forever and no later option
        // set could ever be chosen (the inverse regression: dialogue stuck).
        [Test]
        public void Presenter_ResetsGuardPerFreshOptionsView()
        {
            string src = ReadPackageSource(
                "dev.yarnspinner.unity.addons.classicrpg/Runtime/Scripts/RPGDialoguePresenter.cs");

            Assert.IsTrue(src.Contains("OptionItem.BeginOptionsView"),
                "RPGDialoguePresenter.RunOptionsAsync must call OptionItem.BeginOptionsView() to reopen the " +
                "global submit gate for each new options view. The reset must happen per fresh view (not per " +
                "submit) so a re-entrant submit during the previous selection's inline unwind stays blocked, " +
                "while a genuinely new option set still accepts exactly one submit.");

            // The reset must precede the await on the completion source (i.e. it
            // gates the view that is about to be shown, not a stale one).
            int reset = src.IndexOf("OptionItem.BeginOptionsView", System.StringComparison.Ordinal);
            int await = src.IndexOf("await completion.Task", System.StringComparison.Ordinal);
            Assert.Greater(await, reset,
                "BeginOptionsView() must run BEFORE 'await completion.Task' so the gate is open for the " +
                "options view being presented.");
        }

        // BeginOptionsView is the contract surface the presenter depends on; pin it.
        [Test]
        public void OptionItem_ExposesBeginOptionsView()
        {
            string src = ReadPackageSource(
                "dev.yarnspinner.unity.addons.classicrpg/Runtime/Scripts/OptionItem.cs");

            Assert.IsTrue(
                System.Text.RegularExpressions.Regex.IsMatch(
                    src, @"public\s+static\s+void\s+BeginOptionsView\s*\("),
                "OptionItem must expose 'public static void BeginOptionsView()' for the presenter to reset " +
                "the global submit guard per options view.");
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static string ReadPackageSource(string relativePath)
        {
            // Application.dataPath -> <project>/Assets ; vendored packages live at
            // <project>/Packages.
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, "Packages", relativePath);
            Assert.IsTrue(File.Exists(path), "Expected source file not found: " + path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Returns the brace-balanced body of the first method whose signature line
        /// contains <paramref name="signature"/>. Used so structural assertions
        /// target a specific method instead of matching incidental text elsewhere
        /// in the file (e.g. comments).
        /// </summary>
        private static string ExtractMethodBody(string src, string signature)
        {
            int sigIdx = src.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(sigIdx, -1, "Could not find method signature: " + signature);

            int open = src.IndexOf('{', sigIdx);
            Assert.Greater(open, -1, "Could not find opening brace for: " + signature);

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') { depth++; }
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return src.Substring(open, i - open + 1);
                    }
                }
            }

            Assert.Fail("Unbalanced braces while extracting body for: " + signature);
            return string.Empty;
        }
    }
}
