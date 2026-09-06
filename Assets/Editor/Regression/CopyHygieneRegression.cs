// WO-1413: source-level guard for truthful player copy and capture fixtures.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class CopyHygieneRegression
    {
        private const string ModulesRoot = "Assets/_Modules";
        private const string Launcher = "Assets/Editor/UICaptureLaunch.cs";
        private const string CombatHud = "Assets/_Modules/HUD/Kit/HudKitController.cs";
        private const string HelpVm = "Assets/_Modules/HUD/HelpMenuVM.cs";
        private const string Pause = "Assets/_Modules/Settings/PauseController.cs";
        private const string Settings = "Assets/_Modules/Settings/SettingsController.cs";
        private const string EchoVm = "Assets/_Modules/Village/Harvest/EchoWorkforceVM.cs";
        private const string DailyChest = "Assets/_Modules/Village/Monetization/DailyChestController.cs";
        private const string DialogueResources = "Assets/Resources/Data/Canonical/dialogue/dialogues.json";
        private const string DialogueStreaming = "Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json";

        public static void RunAll()
        {
            if (Run(out string report)) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            var failures = new List<string>();
            try
            {
                // RED recipe: restore "Repair structures" in BuildOptionProbeDef.
                CaseCaptureFixtures(failures);

                // RED recipe: restore "SKILL I" as the first adaptive skill caption.
                CaseCombatFaces(failures);

                // RED recipe: add "Repair structures" to only one canonical dialogue twin.
                CaseDialogueTwins(failures);

                // RED recipe: change HelpMenuVM's reset face back to "Reset Hero & Pet".
                CaseRetiredCopy(failures);

                // RED recipe: move the Dev Tools candidate below its compile guard.
                CaseDevToolsReleaseGuard(failures);

                // RED recipe: restore the Settings _musicToggle field beside _musicSlider.
                CasePartOneSurfaces(failures);

                // RED recipe: relabel the shared Pause chrome Close as Resume.
                CasePauseExemption(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            report = failures.Count == 0
                ? "COPY_HYGIENE_OK: fixtures use live verbs and distinct chain parts; combat faces name equipped skills or EMPTY; part-one copy remains truthful; Pause exemption preserved"
                : "COPY_HYGIENE_FAIL: " + string.Join(" | ", failures);
            return failures.Count == 0;
        }

        private static void CaseCaptureFixtures(List<string> failures)
        {
            string code = Read(Launcher);
            if (code.Contains("Repair structures"))
                failures.Add("[dialogue-fixture] retired Repair structures option remains");
            if (!code.Contains("\"Gather resources\""))
                failures.Add("[dialogue-fixture] Gather resources replacement is missing");
            if (!code.Contains("\"Standing Watch Over the Western Fields - Part \" + i + \" of 2\""))
                failures.Add("[rumor-fixture] two-part watch chain is not distinguished in words");
        }

        private static void CaseCombatFaces(List<string> failures)
        {
            string code = Read(CombatHud);
            if (code.Contains("\"SKILL I\"") || code.Contains("\"SKILL II\"") || code.Contains("\"SKILL III\""))
                failures.Add("[combat-face] numbered placeholder face remains");
            if (Count(code, "BuildCombatDockSlot(") < 6 || Count(code, "BuildCombatDockSlot(2, \"EMPTY\"") != 1 ||
                Count(code, "BuildCombatDockSlot(3, \"EMPTY\"") != 1 || Count(code, "BuildCombatDockSlot(4, \"EMPTY\"") != 1)
                failures.Add("[combat-face] adaptive skill faces do not seed EMPTY exactly once each");
            if (!code.Contains("h.SetCaption(equipped && !string.IsNullOrWhiteSpace(s.Name) ? s.Name : \"EMPTY\")"))
                failures.Add("[combat-face] live assignable face is not sourced from equipped AbilitySlotRecord.Name");
        }

        private static void CaseDialogueTwins(List<string> failures)
        {
            byte[] a = File.ReadAllBytes(DialogueResources);
            byte[] b = File.ReadAllBytes(DialogueStreaming);
            if (!SameBytes(a, b)) failures.Add("[dialogue-twins] canonical dialogue copies differ byte-for-byte");
            string text = System.Text.Encoding.UTF8.GetString(a);
            if (text.Contains("Repair structures"))
                failures.Add("[dialogue-content] retired fixture phrase leaked into real dialogue data");
        }

        private static void CaseRetiredCopy(List<string> failures)
        {
            foreach (string path in Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string code = Read(path);
                // Lead correction at gate (2026-09-05): the case-insensitive "& PET" scan matched ordinary C#
                // ("&& pet.Id == id", "&& petUnits.Count") in three files and went RED on code, not copy - a
                // hollow trap. The retired COPY is "Hero & Pet" / "RESET HERO & PET": match case-sensitively
                // and never when the ampersand is half of "&&". RED recipe: put "Reset Hero & Pet" back in HelpMenu.
                if (ContainsRetiredPetPhrase(code))
                    failures.Add("[retired-pet] " + path + " contains the retired '& Pet' copy");
                if (code.IndexOf("to every node's yield", StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add("[retired-yield] " + path + " contains multiplier copy");
            }
        }

        private static void CaseDevToolsReleaseGuard(List<string> failures)
        {
            string code = Read(HelpVm);
            int guard = code.IndexOf("#if DEVELOPMENT_BUILD || UNITY_EDITOR || TESTER_BUILD", StringComparison.Ordinal);
            int face = code.IndexOf("\"Dev Tools\"", StringComparison.Ordinal);
            int end = face >= 0 ? code.IndexOf("#endif", face, StringComparison.Ordinal) : -1;
            if (guard < 0 || face < guard || end < face)
                failures.Add("[dev-tools] player Help candidate is not compile-stripped from release");
        }

        private static void CasePartOneSurfaces(List<string> failures)
        {
            string settings = Read(Settings);
            if (settings.Contains("_musicToggle") || settings.Contains("OnMusicOnOffChanged"))
                failures.Add("[settings] Music toggle remains beside the slider");

            string echo = Read(EchoVm);
            if (!echo.Contains("\"Echoes \" + owned + \"/\" + max + \" - harvest +\"") ||
                !echo.Contains("HarvestTogetherBonusPct"))
                failures.Add("[echo] workforce subtitle is not an additive calculator-backed readout");

            string chest = Read(DailyChest);
            int hidden = chest.IndexOf("ad CTA hidden until rewarded placement is ready", StringComparison.Ordinal);
            int build = hidden >= 0 ? chest.IndexOf("BuildObsidianButton", hidden, StringComparison.Ordinal) : -1;
            int earlyReturn = hidden >= 0 ? chest.IndexOf("return;", hidden, StringComparison.Ordinal) : -1;
            if (hidden < 0 || earlyReturn < hidden || (build >= 0 && earlyReturn > build))
                failures.Add("[daily-chest] unavailable ad row is not hidden before button construction");
        }

        private static void CasePauseExemption(List<string> failures)
        {
            // BATCH_STATE 8.9 supersedes the WO's one-verb acceptance for tonight: keep both the
            // primary Resume and kit-owned shared Close until the owner chooses which face retires.
            string code = Read(Pause);
            if (!code.Contains("BuildObsidianButton(body, \"Resume\"") ||
                !code.Contains("MedievalUiSkin.ApplyButton(resume, primary: true)") ||
                code.Contains("closeLabel.text = \"Resume\""))
                failures.Add("[pause-exemption] approved primary Resume plus untouched shared Close shape changed");
        }

        private static string Read(string path) => File.ReadAllText(path);

        /// <summary>True when the source carries the retired "& Pet" / "& PET" COPY - case-sensitive, and the
        /// ampersand must not be half of a C# "&&" (which is how the first version matched "&& pet.Id").</summary>
        private static bool ContainsRetiredPetPhrase(string code)
        {
            foreach (string needle in new[] { "& Pet", "& PET" })
            {
                int at = 0;
                while ((at = code.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
                {
                    bool doubledAmp = at > 0 && code[at - 1] == '&';
                    int end = at + needle.Length;
                    bool wordEnds = end >= code.Length || !char.IsLetterOrDigit(code[end]);
                    if (!doubledAmp && wordEnds) return true;
                    at = end;
                }
            }
            return false;
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            for (int at = 0; (at = text.IndexOf(token, at, StringComparison.Ordinal)) >= 0; at += token.Length)
                count++;
            return count;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
