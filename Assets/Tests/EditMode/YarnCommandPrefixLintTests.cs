using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    // =============================================================================
    // YarnCommandPrefixLintTests (EditMode) — guards the "<<command: ...>>" Yarn
    // syntax regression that broke EVERY vendor / quest command.
    // -----------------------------------------------------------------------------
    // THE BUG THIS GUARDS (2026-06-12 class). Yarn dispatches a custom command with
    // the bare verb:  <<StartQuest village-intro>>  ->  NPCCommandBridge.StartQuest.
    // Someone wrote it as  <<command: StartQuest village-intro>>  (the literal token
    // "command:" prefixed). Yarn then parsed the command NAME as "command:" and threw
    // "No Command 'command:' has been registered" — so the vendor Buy/Sell, the quest
    // hand-ins, and every other <<...>> bridge call silently died. There is NO Yarn
    // "command:" keyword; the verb goes first, bare.
    //
    // WHAT THIS LINT FLAGS (string/regex over every .yarn under Assets/ — deterministic,
    // fast, no Yarn compile). A FAIL is any  <<command:  occurrence (with or without a
    // trailing space): `<<command:`, `<< command:`, `<<command :` are all caught.
    //
    // HOW TO FIX A FAILURE: drop the `command:` token. `<<command: StartQuest x>>`
    // becomes `<<StartQuest x>>`. The first word inside `<<...>>` IS the command name.
    //
    // CALIBRATION: the current tree has ZERO `<<command:` occurrences (re-grepped at
    // authoring time), so the main test is GREEN. The self-tests below prove the
    // detector FIRES on the bad shape and stays QUIET on the correct bare-verb shape.
    //
    // KNOWN LIMITS (honest): a pure source scan. It does not validate that the verb
    // after `<<` is a REGISTERED command (that needs the bridge's command table); it
    // only kills the one literal mistake that has actually shipped. Yarn keywords that
    // legitimately precede the verb (`if`, `set`, `declare`, `jump`, `stop`, `call`,
    // `wait`, `else`, `endif`, `local`, `enum`) are not "command:" and never trip this.
    // =============================================================================
    public class YarnCommandPrefixLintTests
    {
        // The forbidden shape: "<<" then optional ws then the literal token "command"
        // then optional ws then ":". Catches `<<command:`, `<< command:`, `<<command :`.
        private static readonly Regex CommandPrefix =
            new Regex(@"<<\s*command\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Test]
        public void NoYarnCommandColonPrefix()
        {
            var offenders = ScanYarnFiles();

            Assert.IsEmpty(offenders,
                "A Yarn script uses the INVALID `<<command: Verb ...>>` syntax. Yarn has no " +
                "`command:` keyword — it parses \"command:\" as the command NAME and throws " +
                "\"No Command 'command:' has been registered\", silently killing that vendor / " +
                "quest / bridge call. FIX: delete the `command:` token so the verb is bare, " +
                "e.g. `<<command: StartQuest x>>` -> `<<StartQuest x>>`.\n  " +
                string.Join("\n  ", offenders));
        }

        // SELF-TEST: the detector must FIRE on the bad shape (proves it would have caught
        // the regression) and must NOT fire on the correct bare-verb shape (no false pos).
        [Test]
        public void Detector_FiresOnBadShape_QuietOnGoodShape()
        {
            const string bad = "title: Vendor\n---\n<<command: StartQuest village-intro>>\n===\n";
            var hitsBad = FindOffendersInText("Synthetic_Bad.yarn", bad);
            Assert.IsNotEmpty(hitsBad,
                "Detector FAILED to flag `<<command: StartQuest ...>>` — it would not have " +
                "caught the 'No Command command:' regression.");

            const string good = "title: Vendor\n---\n<<StartQuest village-intro>>\n<<set $g to true>>\n" +
                                 "<<if $x>>\n<<jump Other>>\n<<stop>>\n===\n";
            var hitsGood = FindOffendersInText("Synthetic_Good.yarn", good);
            Assert.IsEmpty(hitsGood,
                "Detector mis-fired on a CORRECT bare-verb Yarn script (`<<StartQuest ...>>`, " +
                "`<<set ...>>`, `<<if ...>>`). The lint must only flag the literal `command:` token. " +
                "Offenders: " + string.Join(", ", hitsGood));
        }

        // SELF-TEST 2: the trailing-space + extra-whitespace variants are caught too.
        [Test]
        public void Detector_CatchesWhitespaceVariants()
        {
            foreach (var bad in new[] { "<<command:StartQuest x>>", "<< command: StartQuest x>>", "<<command :StartQuest x>>" })
            {
                var hits = FindOffendersInText("Synthetic_Variant.yarn", bad);
                Assert.IsNotEmpty(hits,
                    $"Detector missed the `command:` variant: '{bad}'. All `<<command:` spellings must fail.");
            }
        }

        // ── scan engine ────────────────────────────────────────────────────────────

        private static List<string> ScanYarnFiles()
        {
            var offenders = new List<string>();
            string assets = ProjectAssetsRoot();
            if (!Directory.Exists(assets)) return offenders;

            foreach (var file in Directory.GetFiles(assets, "*.yarn", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException) { continue; }
                offenders.AddRange(FindOffendersInText(Path.GetFileName(file), text));
            }
            return offenders;
        }

        // Reports "<file>:<line> — <trimmed line>" for every line bearing the bad shape.
        private static List<string> FindOffendersInText(string fileName, string text)
        {
            var offenders = new List<string>();
            if (!CommandPrefix.IsMatch(text)) return offenders;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (CommandPrefix.IsMatch(lines[i]))
                    offenders.Add($"{fileName}:{i + 1} — {lines[i].Trim()}");
            }
            return offenders;
        }

        // <project>/Assets — the robust pattern the sibling EditMode tests use.
        private static string ProjectAssetsRoot()
        {
            return UnityEngine.Application.dataPath; // == <project>/Assets
        }
    }
}
