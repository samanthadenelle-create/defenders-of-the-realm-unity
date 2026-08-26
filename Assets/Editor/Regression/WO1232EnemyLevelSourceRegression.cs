// =============================================================================
// WO1232EnemyLevelSourceRegression - the "Lv 68 next to a Lv 5 hero" oracle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - registered into DataRegression.RunAll by the orchestrator (the registration line
// lives with the orchestrator; this file only provides Run).
//
// THE DEFECT THIS PINS (owner felt-test 2026-08-26, Seeker 2026.08.26.342290):
//   an enemy displayed "Lv 68" while the hero was Lv 5 on wave 7. Enemy.Level had been
//   the truthful, authored, per-archetype value since WO-611 F3, but TWO consumers were
//   never migrated and still ran the retired HP/25 heuristic against the RUNTIME maxHp:
//       HudModelHost.EnemyLevelStub          (deleted by WO-1232)
//       ThreatSkullPlate.EnemyThreatLevel    (now reads Enemy.Level)
//   Because wave scaling inflates maxHp every wave, the number CREPT: 1700 HP read as
//   "level 68". Worse, EnemyThreatLevel feeds delta = enemyDifficulty - playerLevel and
//   the RiskyDelta/LethalDelta bands, so EVERY wave enemy showed a LETHAL tell and the
//   warning carried zero information.
//
// WHAT EACH CASE PINS, AND WHY IT IS HERE
//   1. [independent]  a live Enemy whose maxHp is inflated ACROSS A RANGE of wave-scaling
//                     multipliers reports the SAME threat level every time. This is the
//                     actual invariant: the displayed level is a function of the authored
//                     band, NOT of runtime HP. It also asserts the value is not the old
//                     maxHp/25 answer, so a "fix" that merely re-tuned the divisor fails.
//   2. [authored]     Enemy.Level still derives from the AUTHORED def (def.Hp, pre-scaling)
//                     inside Configure - source-linted on comment/string-stripped source,
//                     because WO-1232 must not have quietly moved that authority.
//   3. [guard]        NO source under Assets/ derives a level from a runtime maxHp again:
//                     no `maxHp / 25f`, no `MaxHp > 0.001f ? ... : Hp` probe, no
//                     EnemyLevelStub. Comment- AND string-stripped, so prose cannot fake
//                     a pass and prose cannot fake a FAIL either.
//   4. [target-frame] HudModelProducers.TargetProducer publishes en.Level and carries the
//                     WO-1232 FlowTrace line that NAMES the source (CLAUDE.md S12), so the
//                     next regression of this is one log read rather than a felt-test.
//   5. [banding]      DOCUMENTS what RiskyDelta/LethalDelta now mean against REAL levels:
//                     an on-level enemy must read as a FAIR fight (tier 0). It deliberately
//                     does NOT assert the thresholds' values - threat banding is player-felt
//                     and is an owner ruling (WO-1232 step 3); this case only guarantees the
//                     bands stay ORDERED and sane so a retune cannot invert them.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class WO1232EnemyLevelSourceRegression
    {
        // The retired heuristic's divisor. Present ONLY so the oracle can compute the wrong
        // answer and assert we are not returning it.
        private const float RetiredDivisor = 25f;

        // The felt-test's own numbers: a Lv 5 hero, an enemy inflated to 1700 maxHp.
        private const float FeltTestInflatedMaxHp = 1700f;
        private const int   FeltTestHeroLevel     = 5;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            void Fail(string s) => failures.Add("WO1232_ENEMY_LEVEL FAIL: " + s);

            var notes = new List<string>();
            var created = new List<GameObject>();

            try
            {
                CheckIndependentOfMaxHp(Fail, notes, created);
                CheckAuthoredSource(Fail);
                CheckNoHpDerivedLevelAnywhere(Fail, notes);
                CheckTargetFrame(Fail);
                CheckBandingOrder(Fail, notes);
            }
            catch (Exception ex)
            {
                Fail("threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                foreach (var go in created)
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            if (failures.Count > 0)
            {
                reason = string.Join("\n", failures);
                return false;
            }

            reason = "WO1232_ENEMY_LEVEL OK - the displayed level and the threat tell both read " +
                     "Enemy.Level (authored per-def band) and are INDEPENDENT of runtime maxHp; " +
                     "no Assets/ source derives a level from maxHp over " +
                     RetiredDivisor.ToString("0") + " any more; target frame publishes en.Level and " +
                     "traces its source; threat bands ordered (risky " + ThreatSkullPlate.RiskyDelta +
                     " < lethal " + ThreatSkullPlate.LethalDelta + "). " + string.Join(" ", notes);
            return true;
        }

        // ── 1 [independent] ───────────────────────────────────────────────────────
        // The invariant, driven on a REAL Enemy through the REAL public scaling entry point.
        private static void CheckIndependentOfMaxHp(Action<string> Fail, List<string> notes,
                                                    List<GameObject> created)
        {
            FieldInfo levelField = typeof(Enemy).GetField("_level",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo maxHpField = typeof(Enemy).GetField("_maxHp",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hpField = typeof(Enemy).GetField("_hp",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (levelField == null || maxHpField == null || hpField == null)
            {
                Fail("[independent] Enemy is missing _level/_maxHp/_hp - this oracle cannot seat " +
                     "an authored level, so it must NOT pass hollow. Re-point the field names.");
                return;
            }

            const int AuthoredLevel = 7;      // a plausible authored band (hollow-warrior ~6)
            const float AuthoredHp  = 175f;   // its pre-scaling HP

            var go = new GameObject("Enemy (WO-1232 level oracle)");
            created.Add(go);
            var enemy = go.AddComponent<Enemy>();   // auto-adds NavMeshAgent + EnemyDamageable
            levelField.SetValue(enemy, AuthoredLevel);
            maxHpField.SetValue(enemy, AuthoredHp);
            hpField.SetValue(enemy, AuthoredHp);

            if (enemy.Level != AuthoredLevel)
            {
                Fail("[independent] Enemy.Level read " + enemy.Level + " with an authored " +
                     AuthoredLevel + " seated - the property no longer surfaces the authored band.");
                return;
            }

            // Drive the REAL wave-scaling entry point across the whole plausible range, plus
            // the felt-test's exact 1700 HP. The threat level must not move at all.
            var multipliers = new[] { 1f, 2f, 4f, 8f, FeltTestInflatedMaxHp / AuthoredHp };
            float runningHp = AuthoredHp;
            foreach (float mult in multipliers)
            {
                maxHpField.SetValue(enemy, AuthoredHp);
                hpField.SetValue(enemy, AuthoredHp);
                enemy.ApplyWaveScaling(mult, 1f, 1f);
                runningHp = (float)maxHpField.GetValue(enemy);

                int threat = ThreatSkullPlate.EnemyThreatLevel(enemy);
                if (threat != AuthoredLevel)
                {
                    Fail("[independent] ThreatSkullPlate.EnemyThreatLevel returned " + threat +
                         " for an enemy with authored level " + AuthoredLevel + " at maxHp " +
                         runningHp.ToString("0") + " (x" + mult.ToString("0.##") + "). The threat " +
                         "tell is STILL a function of runtime HP - that is the WO-1232 defect, and " +
                         "it makes every scaled wave enemy read as LETHAL.");
                    return;
                }

                // ...and it must not be the retired answer, so a re-tuned divisor cannot pass.
                int retiredAnswer = Mathf.Max(1, Mathf.RoundToInt(runningHp / RetiredDivisor));
                if (retiredAnswer != AuthoredLevel && threat == retiredAnswer)
                {
                    Fail("[independent] the threat level equals the RETIRED maxHp/" +
                         RetiredDivisor.ToString("0") + " answer (" + retiredAnswer + ") at maxHp " +
                         runningHp.ToString("0") + ". The heuristic was re-tuned, not deleted.");
                    return;
                }
            }

            // The felt-test itself, stated in its own numbers: 1700 HP beside a Lv 5 hero.
            maxHpField.SetValue(enemy, FeltTestInflatedMaxHp);
            hpField.SetValue(enemy, FeltTestInflatedMaxHp);
            int feltThreat = ThreatSkullPlate.EnemyThreatLevel(enemy);
            int feltRetired = Mathf.Max(1, Mathf.RoundToInt(FeltTestInflatedMaxHp / RetiredDivisor));
            if (feltThreat != AuthoredLevel)
                Fail("[independent] the owner's own case still fails: an enemy at " +
                     FeltTestInflatedMaxHp.ToString("0") + " maxHp with authored level " +
                     AuthoredLevel + " reported " + feltThreat + " (the retired heuristic would " +
                     "say " + feltRetired + " - the reported 'Lv 68').");

            notes.Add("Threat level held at " + AuthoredLevel + " across x1..x" +
                      (FeltTestInflatedMaxHp / AuthoredHp).ToString("0.#") + " HP scaling (the " +
                      "retired heuristic would have said " + feltRetired + " at " +
                      FeltTestInflatedMaxHp.ToString("0") + " HP vs a Lv " + FeltTestHeroLevel + " hero).");
        }

        // ── 2 [authored] ──────────────────────────────────────────────────────────
        // Enemy.Configure must still source _level from the AUTHORED def, never from the
        // runtime pool. Source-linted rather than driven, because Configure reaches VFX /
        // NavMesh seams that are not available in a batchmode edit-mode suite - and a
        // swallowed exception there would be exactly the hollow pass this file forbids.
        private static void CheckAuthoredSource(Action<string> Fail)
        {
            string src = ReadStripped("Assets/_Modules/Village/Enemies/Enemy.cs", Fail, "[authored]");
            if (src == null) return;

            // _level = ... def.Hp ...   (the authored band; ApplyWaveScaling must not touch it)
            if (!Regex.IsMatch(src, @"_level\s*=[^;]*def\.Hp"))
                Fail("[authored] Enemy.Configure no longer assigns _level from the AUTHORED def.Hp. " +
                     "If the level moved to a real authored field that is fine - re-point this case " +
                     "at it deliberately; do not let it drift back onto the runtime HP.");

            // ApplyWaveScaling must NEVER write _level, or the number creeps again by another route.
            int idx = src.IndexOf("ApplyWaveScaling", StringComparison.Ordinal);
            if (idx >= 0)
            {
                int end = Math.Min(src.Length, idx + 2000);
                string body = src.Substring(idx, end - idx);
                if (Regex.IsMatch(body, @"_level\s*="))
                    Fail("[authored] ApplyWaveScaling writes _level. Wave scaling inflates HP by " +
                         "design; the moment it also moves the LEVEL, the WO-1232 defect is back " +
                         "through a different door.");
            }
        }

        // ── 3 [guard] ─────────────────────────────────────────────────────────────
        // The one that stops this returning a third time.
        private static void CheckNoHpDerivedLevelAnywhere(Action<string> Fail, List<string> notes)
        {
            string root = Application.dataPath;   // <project>/Assets, editor-safe in batchmode
            if (!Directory.Exists(root))
            {
                Fail("[guard] Assets/ not found at '" + root +
                     "' - the sweep cannot pass without actually sweeping.");
                return;
            }

            // The retired heuristic's two fingerprints, on comment/string-stripped source:
            //   a) a level rounded out of a runtime maxHp
            //   b) the "maxHp if it's set, else current hp" probe that fed it
            var banned = new List<KeyValuePair<string, Regex>>
            {
                new KeyValuePair<string, Regex>("maxHp-derived level",
                    new Regex(@"(?i)max\s*hp\s*/\s*25f")),
                new KeyValuePair<string, Regex>("maxHp/hp fallback probe",
                    new Regex(@"(?i)MaxHp\s*>\s*0\.001f\s*\?")),
                new KeyValuePair<string, Regex>("EnemyLevelStub",
                    new Regex(@"EnemyLevelStub")),
            };

            int scanned = 0;
            var hits = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                // This oracle itself names the retired shapes on purpose.
                if (file.Replace('\\', '/').EndsWith("WO1232EnemyLevelSourceRegression.cs",
                                                     StringComparison.OrdinalIgnoreCase))
                    continue;

                string raw;
                try { raw = File.ReadAllText(file); }
                catch (Exception e) { Fail("[guard] could not read " + file + ": " + e.Message); continue; }

                scanned++;
                string stripped = Strip(raw);
                foreach (KeyValuePair<string, Regex> b in banned)
                {
                    Match m = b.Value.Match(stripped);
                    if (!m.Success) continue;
                    hits.Add(b.Key + " in " + Rel(file) + " ('" + Excerpt(stripped, m.Index) + "')");
                }
            }

            if (scanned == 0)
                Fail("[guard] swept ZERO .cs files - a sweep that scans nothing passes everything.");

            foreach (string h in hits)
                Fail("[guard] a level is still derived from runtime HP: " + h + ". There is exactly " +
                     "ONE level authority (Enemy.Level, authored per def); an HP-derived copy creeps " +
                     "upward with wave scaling and mis-drives the danger tell.");

            notes.Add("Swept " + scanned + " .cs files for HP-derived levels: none.");
        }

        // ── 4 [target-frame] ──────────────────────────────────────────────────────
        private static void CheckTargetFrame(Action<string> Fail)
        {
            const string path = "Assets/_Modules/Village/HUD/HudModelProducers.cs";
            string src = ReadStripped(path, Fail, "[target-frame]");
            if (src == null) return;

            if (!Regex.IsMatch(src, @"int\s+level\s*=\s*en\.Level"))
                Fail("[target-frame] TargetProducer no longer reads en.Level for the displayed 'Lv N'.");

            if (!Regex.IsMatch(src, @"TierFor\s*\(\s*level\s*,"))
                Fail("[target-frame] the target frame's difficulty tell no longer grades the SAME " +
                     "level it displays - the two surfaces would disagree again.");

            // S12: the level's SOURCE must be traceable without a felt-test. The FlowTrace call
            // is checked on RAW source (its payload is a string literal, which Strip removes).
            string raw = ReadRaw(path, Fail, "[target-frame]");
            if (raw == null) return;
            if (!raw.Contains("HudTarget") || !raw.Contains("target level resolved"))
                Fail("[target-frame] the WO-1232 FlowTrace line that NAMES the level's source is " +
                     "gone. CLAUDE.md S12: instrumentation is permanent - flag it off, never strip it.");
        }

        // ── 5 [banding] ───────────────────────────────────────────────────────────
        private static void CheckBandingOrder(Action<string> Fail, List<string> notes)
        {
            int risky = ThreatSkullPlate.RiskyDelta;
            int lethal = ThreatSkullPlate.LethalDelta;

            if (risky < 1)
                Fail("[banding] RiskyDelta " + risky + " is <= 0 - an on-level or WEAKER enemy would " +
                     "raise a warning, which is the no-information failure in the other direction.");
            if (lethal <= risky)
                Fail("[banding] LethalDelta " + lethal + " must exceed RiskyDelta " + risky +
                     " or the caution band collapses and every warning is LETHAL.");

            // The player-felt guarantee, stated against REAL levels: an on-level fight is fair.
            if (ThreatSkullPlate.TierFor(FeltTestHeroLevel, FeltTestHeroLevel) != 0)
                Fail("[banding] an on-level enemy does not read as a fair fight (tier " +
                     ThreatSkullPlate.TierFor(FeltTestHeroLevel, FeltTestHeroLevel) + ").");

            notes.Add("Bands against REAL levels: fair < +" + risky + ", risky +" + risky + ".." +
                      (lethal - 1) + ", lethal >= +" + lethal + " (values are an OWNER ruling - this " +
                      "case pins their ORDER, never their numbers).");
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private static string ReadRaw(string relative, Action<string> Fail, string label)
        {
            string full = Path.Combine(ProjectRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) { Fail(label + " could not find " + relative); return null; }
            try { return File.ReadAllText(full); }
            catch (Exception e) { Fail(label + " could not read " + relative + ": " + e.Message); return null; }
        }

        private static string ReadStripped(string relative, Action<string> Fail, string label)
        {
            string raw = ReadRaw(relative, Fail, label);
            return raw == null ? null : Strip(raw);
        }

        // <project>/ (the parent of Assets) — editor-safe in batchmode, where the process
        // working directory is not guaranteed to be the project root.
        private static string ProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }

        private static string Rel(string full)
        {
            string cwd = ProjectRoot();
            string f = full.Replace('\\', '/');
            string c = cwd.Replace('\\', '/').TrimEnd('/') + "/";
            return f.StartsWith(c, StringComparison.OrdinalIgnoreCase) ? f.Substring(c.Length) : f;
        }

        private static string Excerpt(string src, int index)
        {
            int start = Math.Max(0, index - 30);
            int len = Math.Min(70, src.Length - start);
            return src.Substring(start, len).Replace('\n', ' ').Replace('\r', ' ').Trim();
        }

        // Comments AND string literals removed, so neither prose nor a log message can fake a
        // pass or a failure. Same shape as AggroLeashRegression.StripCode.
        private static string Strip(string src)
        {
            if (src == null) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    sb.Append('\n');
                    continue;
                }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i++;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++;
                    while (i < src.Length && src[i] != quote)
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
