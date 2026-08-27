// =============================================================================
// WO1232EnemyLevelSourceRegression - "HP / 25 is not a level system" (owner, 2026-08-26).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - already registered into DataRegression.RunAll (line unchanged by this pass).
//
// THE RULING THIS PINS
//   The owner felt-tested a wave-7 enemy that said "Lv 68" beside a Lv 5 hero. The first
//   pass pointed the two consumers at Enemy.Level - and then found that Enemy.Level is
//   ITSELF round(def.Hp / 25). There is NO authored level field on EnemyDef; every level
//   in the game was that one division. Owner verbatim: "HP / 25 is not a level system.
//   Dressing it up as one just produces very confident nonsense."
//   So the ruling REMOVES, it does not tune:
//     1. the numeric enemy level is GONE from the player-facing HUD; what shows instead is
//        the AUTHORED classification WORD - BOSS / ELITE - and NOTHING for an ordinary
//        enemy (silence is the default, not a blank label);
//     2. APEX is RESERVED. waves.json authors an apexBoss, but a third badge before a tier
//        is authored deliberately re-invents the fake precision this removed;
//     3. the RISKY / LETHAL banding is REMOVED, not retuned - "the Lv5 vs Lv36 comparison
//        is downstream of the fake level. Retuning thresholds just polishes the wrong
//        equation." Its INSTRUMENTATION stays (CLAUDE.md S12: a stripped trace turns a
//        logged failure back into a silent one);
//     4. Combat Rating (Low/Even/High/Deadly from HP, damage, cadence, armour, abilities,
//        encounter role) is the eventual replacement and is explicitly NOT built here.
//
// WHAT EACH CASE PINS, AND WHY IT IS HERE
//   1. [badge]      DRIVEN, not linted: real Enemy components with real defs seated report
//                   BOSS / ELITE / "" through the one public mapping (EnemyBadge). Includes
//                   the GOOD path - an ordinary enemy and a def-less enemy must produce an
//                   EMPTY string, because "ordinary shows nothing" is half the ruling.
//   2. [authored]   Driven over the SHIPPING catalog (enemies.json): every row's badge is
//                   exactly what its authored boss/role fields say, at least one BOSS exists
//                   (or the badge path is dead data), and most rows are silent. No APEX arm.
//   3. [no-number]  The number is gone from the model, the kit and the producer: TargetModel
//                   carries a string Badge and no int Level, the target frame writes the badge
//                   (no "Lv " literal survives in the kit), and the producer publishes the
//                   badge rather than en.Level.
//   4. [banding]    ThreatSkullPlate.DisplayEnabled is FALSE and driven proof follows: no
//                   RISKY / LETHAL copy remains, and NO file outside the plate (and this
//                   oracle) calls TierFor - the banding is off the player-facing path.
//   5. [traces]     ...and its FlowTrace instrumentation is STILL THERE, as is the target
//                   frame's. S12: instrumentation is permanent - flag it off, never strip it.
//   6. [guard]      No Assets/ source derives a level from a runtime maxHp again, no APEX
//                   badge sneaks in, and NO Combat Rating was stubbed under this ticket.
//
// MISSING-DEPENDENCY POLICY (hollow-pass ratchet, 2026-08-26)
//   Every input this oracle reads is TRACKED SOURCE: enemies.json, six .cs files, and two
//   private Enemy fields reached by reflection. None of them is an optional fixture and none
//   is a harness capability a batchmode run may lack. So an unreadable input is FIXTURE-ABSENT
//   and is reported RED at the guard, naming the path and naming what went unchecked
//   (FailMissingSource) - never a bare return, never a PartialSkip token. ReadRaw / ReadStripped
//   therefore assert NOTHING themselves; asserting on the caller behalf is exactly what left
//   bare `if (raw == null) return;` guards behind, and a guard that returns having asserted
//   nothing lands in the GREEN column reporting a case that ran zero checks.
//   Two other shapes appear in here and are deliberately NOT dependency guards, marked as such
//   at their own site: the sweep exclusions (rule predicates over files that are present and
//   legally name the banned token) and a regex that does not match (absence of a VIOLATION, the
//   good path - `scanned == 0` is what catches a sweep that examined nothing).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Hud;

namespace DeNelle.Editor
{
    public static class WO1232EnemyLevelSourceRegression
    {
        // The retired heuristic's divisor. Present ONLY so the guard can name what it bans.
        private const float RetiredDivisor = 25f;

        private const string CatalogPath = "Assets/Resources/Data/Canonical/enemies.json";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            void Fail(string s) => failures.Add("WO1232_ENEMY_LEVEL FAIL: " + s);

            var notes = new List<string>();
            var created = new List<GameObject>();

            try
            {
                CheckBadgeMapping(Fail, notes, created);
                CheckAuthoredCatalog(Fail, notes, created);
                CheckNoNumericLevel(Fail);
                CheckBandingOff(Fail, notes);
                CheckInstrumentationKept(Fail);
                CheckGuards(Fail, notes);
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

            reason = "WO1232_ENEMY_LEVEL OK - no numeric enemy level reaches the player: the target " +
                     "frame shows the AUTHORED word (BOSS / ELITE) and nothing at all for an ordinary " +
                     "enemy; the RISKY/LETHAL banding is off the player-facing path with its traces " +
                     "intact; no source derives a level from maxHp over " + RetiredDivisor.ToString("0") +
                     "; no APEX badge and no Combat Rating was built. " + string.Join(" ", notes);
            return true;
        }

        // ── 1 [badge] ─────────────────────────────────────────────────────────────
        // DRIVEN on real components. Both directions matter: a boss must SAY boss, and an
        // ordinary enemy must say NOTHING - a blank-but-present label is the failure mode
        // the ruling names ("Ordinary enemies get NO badge. Silence is the default").
        private static void CheckBadgeMapping(Action<string> Fail, List<string> notes,
                                              List<GameObject> created)
        {
            FieldInfo defField = typeof(Enemy).GetField("_def",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (defField == null)
            {
                Fail("[badge] fixture-absent: Enemy has no private _def field, so NO badge case ran " +
                     "at all - this oracle cannot seat an authored stat block. A quiet return here " +
                     "would read as a pass of a case that asserted nothing. Re-point the field name.");
                return;
            }

            var go = new GameObject("Enemy (WO-1232 badge oracle)");
            created.Add(go);
            var enemy = go.AddComponent<Enemy>();   // auto-adds NavMeshAgent + EnemyDamageable

            // def-less (hand-placed) enemy: ordinary, silent.
            defField.SetValue(enemy, null);
            Expect(Fail, enemy, EnemyTier.Ordinary, "", "a def-less hand-placed enemy");

            defField.SetValue(enemy, new EnemyDef { Id = "hollow-walker", Role = "grunt", Boss = false, Hp = 50f });
            Expect(Fail, enemy, EnemyTier.Ordinary, "", "an ordinary grunt");

            // The felt-test's own shape: a wave-scaled brute. 900 hp read as "Lv 36" and
            // LETHAL forever under the retired equation; it is an ORDINARY enemy and must
            // now say nothing whatsoever.
            defField.SetValue(enemy, new EnemyDef { Id = "hollow-brute", Role = "brute", Boss = false, Hp = 900f });
            Expect(Fail, enemy, EnemyTier.Ordinary, "", "hollow-brute (900 hp - the old 'Lv 36 LETHAL')");

            defField.SetValue(enemy, new EnemyDef { Id = "hollow-reaper", Role = "elite", Boss = false, Hp = 240f });
            Expect(Fail, enemy, EnemyTier.Elite, EnemyBadge.Elite, "role:\"elite\"");

            // Case-insensitive authoring must not silently drop a badge.
            defField.SetValue(enemy, new EnemyDef { Id = "case-test", Role = " Elite ", Boss = false, Hp = 240f });
            Expect(Fail, enemy, EnemyTier.Elite, EnemyBadge.Elite, "role:\" Elite \" (padded / mixed case)");

            defField.SetValue(enemy, new EnemyDef { Id = "necromancer", Role = "elite", Boss = true, Hp = 1700f });
            Expect(Fail, enemy, EnemyTier.Boss, EnemyBadge.Boss, "boss:true (outranks its elite role)");

            // The badge must be identity, not difficulty: inflating HP cannot change it.
            defField.SetValue(enemy, new EnemyDef { Id = "hollow-walker", Role = "grunt", Boss = false, Hp = 50f });
            var maxHpField = typeof(Enemy).GetField("_maxHp", BindingFlags.Instance | BindingFlags.NonPublic);
            var hpField = typeof(Enemy).GetField("_hp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (maxHpField == null || hpField == null)
            {
                // fixture-absent, asserted at the guard: without the HP seam the "badge is IDENTITY,
                // not HP" half of this case cannot run, and that half is the whole point of it.
                Fail("[badge] fixture-absent: Enemy has no _maxHp/_hp fields, so the HP-scaling " +
                     "invariance of the badge went UNCHECKED. Re-point the field names.");
            }
            else
            {
                foreach (float mult in new[] { 1f, 4f, 34f })   // x34 of 50 hp = the felt-test's 1700
                {
                    maxHpField.SetValue(enemy, 50f);
                    hpField.SetValue(enemy, 50f);
                    enemy.ApplyWaveScaling(mult, 1f, 1f);
                    string badge = EnemyBadge.For(enemy);
                    if (badge != "")
                        Fail("[badge] an ORDINARY enemy grew a badge ('" + badge + "') at HP scaling x" +
                             mult.ToString("0.##") + ". The badge is IDENTITY (authored boss/role) and " +
                             "must be completely independent of HP - an HP-driven badge is the WO-1232 " +
                             "defect returning under a new name.");
                }
                notes.Add("Badge held silent for an ordinary enemy across x1..x34 HP scaling.");
            }

            // ASCII only (TMP glyph landmine) and a WORD, never a tint (owner is colourblind).
            foreach (string word in new[] { EnemyBadge.Boss, EnemyBadge.Elite })
            {
                foreach (char c in word)
                    if (c < 32 || c > 126)
                    {
                        Fail("[badge] badge copy '" + word + "' is not ASCII - TMP falls back or drops " +
                             "the glyph and the player reads nothing.");
                        break;
                    }
                if (word.IndexOf('<') >= 0)
                    Fail("[badge] badge copy '" + word + "' carries rich-text markup. The tell must be a " +
                         "WORD, never a colour - the owner is red/green colourblind.");
            }

            // APEX is RESERVED - the mapping must not have grown a third arm.
            if (EnemyBadge.For(EnemyTier.Boss) != EnemyBadge.Boss ||
                EnemyBadge.For(EnemyTier.Elite) != EnemyBadge.Elite ||
                EnemyBadge.For(EnemyTier.Ordinary) != "")
                Fail("[badge] the EnemyTier -> word mapping changed shape. Exactly three arms exist: " +
                     "Boss -> BOSS, Elite -> ELITE, Ordinary -> nothing.");
            if (Enum.GetNames(typeof(EnemyTier)).Length != 3)
                Fail("[badge] EnemyTier has " + Enum.GetNames(typeof(EnemyTier)).Length + " members. APEX " +
                     "is RESERVED until a tier is authored deliberately (owner ruling WO-1232); a third " +
                     "badge invents the precision the ruling removed.");
        }

        private static void Expect(Action<string> Fail, Enemy enemy, EnemyTier tier, string badge, string what)
        {
            if (enemy.Tier != tier)
                Fail("[badge] " + what + " resolved Enemy.Tier=" + enemy.Tier + ", expected " + tier + ".");
            string got = EnemyBadge.For(enemy);
            if (got != badge)
                Fail("[badge] " + what + " produced badge '" + got + "', expected '" + badge + "'" +
                     (badge.Length == 0
                        ? " - an ordinary enemy must show NOTHING; silence is the default, not a label."
                        : "."));
        }

        // ── 2 [authored] ──────────────────────────────────────────────────────────
        // The shipping catalog, driven row by row: the badge is EXACTLY what the author wrote.
        private static void CheckAuthoredCatalog(Action<string> Fail, List<string> notes,
                                                 List<GameObject> created)
        {
            string raw = ReadRaw(CatalogPath);
            if (raw == null)
            {
                // fixture-absent: EVERY assertion in this case is driven off the shipping catalog.
                Fail(MissingSourceMessage("[authored]", CatalogPath,
                    "the per-row badge check over the shipping enemy catalog (and with it the " +
                    "at-least-one-BOSS and most-rows-stay-silent guarantees)"));
                return;
            }

            EnemyCatalog catalog;
            try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(raw); }
            catch (Exception e)
            {
                Fail("[authored] enemies.json did not deserialise: " + e.Message);
                return;
            }
            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                Fail("[authored] enemies.json parsed to ZERO rows - a case that checks nothing " +
                     "passes everything.");
                return;
            }

            FieldInfo defField = typeof(Enemy).GetField("_def",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (defField == null)
            {
                // fixture-absent (the reflection seam is tracked source too), asserted HERE rather
                // than delegated to [badge]: a guard that stays silent because it trusts another
                // case to go red is indistinguishable from a guard that found nothing wrong. The
                // duplicate line is cheap; a hollow green is not.
                Fail("[authored] fixture-absent: Enemy has no private _def field, so no catalog row " +
                     "could be seated and ZERO rows were badge-checked. Re-point the field name.");
                return;
            }

            var go = new GameObject("Enemy (WO-1232 catalog oracle)");
            created.Add(go);
            var enemy = go.AddComponent<Enemy>();

            int bosses = 0, elites = 0, silent = 0;
            foreach (EnemyDef def in catalog.Enemies)
            {
                if (def == null)
                {
                    // NOT a dependency guard: a null element means enemies.json literally authors
                    // null in its array. That is malformed DATA, so it is reported, never skipped.
                    Fail("[authored] enemies.json contains a NULL row - malformed catalog data; the " +
                         "row it should have described is unbadged and unverifiable.");
                    continue;
                }
                defField.SetValue(enemy, def);

                bool isElite = string.Equals((def.Role ?? "").Trim(), "elite",
                                             StringComparison.OrdinalIgnoreCase);
                string expected = def.Boss ? EnemyBadge.Boss : isElite ? EnemyBadge.Elite : "";
                string got = EnemyBadge.For(enemy);
                if (got != expected)
                    Fail("[authored] '" + def.Id + "' (boss=" + def.Boss + ", role='" + def.Role +
                         "', hp=" + def.Hp.ToString("0") + ") badged '" + got + "' but its AUTHORED " +
                         "fields say '" + expected + "'.");

                if (expected == EnemyBadge.Boss) bosses++;
                else if (expected == EnemyBadge.Elite) elites++;
                else silent++;
            }

            if (bosses == 0)
                Fail("[authored] NO row in enemies.json badges as BOSS. Either the authoring lost " +
                     "boss:true or the mapping broke - a badge path that can never fire is dead data.");
            if (silent == 0)
                Fail("[authored] EVERY row carries a badge. The ruling's default is SILENCE; a badge " +
                     "on everything carries as little information as the LETHAL skull it replaced.");

            notes.Add("Catalog rows " + catalog.Enemies.Count + ": " + bosses + " BOSS, " + elites +
                      " ELITE, " + silent + " silent (badges driven off the shipping enemies.json).");
        }

        // ── 3 [no-number] ─────────────────────────────────────────────────────────
        private static void CheckNoNumericLevel(Action<string> Fail)
        {
            // (a) The Core model carries a WORD, not a number.
            const string modelPath = "Assets/_Modules/Core/HudModel/HudModels.cs";
            string model = ReadStripped(modelPath);
            if (model == null)
                Fail(MissingSourceMessage("[no-number]", modelPath,
                    "the proof that TargetModel carries a string Badge and no int Level"));
            else
            {
                int t = model.IndexOf("class TargetModel", StringComparison.Ordinal);
                if (t < 0) Fail("[no-number] TargetModel not found in " + modelPath + ".");
                else
                {
                    // NB: the two patterns below spell an open-brace as \x7B on purpose. A raw brace
                    // inside a regex string unbalances THIS file's brace count, and CLAUDE.md §1's
                    // gate counts raw braces - it cannot tell code from a string.
                    string body = model.Substring(t, Math.Min(2500, model.Length - t));
                    if (Regex.IsMatch(body, @"int\s+Level\s*\x7B"))
                        Fail("[no-number] TargetModel still exposes an int Level. That number was " +
                             "maxHp/25; the owner removed it. A number returns here only when a REAL " +
                             "authored stat exists (Combat Rating - a separate spec).");
                    if (!Regex.IsMatch(body, @"string\s+Badge\s*\x7B"))
                        Fail("[no-number] TargetModel no longer carries the string Badge - the authored " +
                             "BOSS / ELITE word has nowhere to travel from producer to view.");
                }
            }

            // (b) The kit's target frame renders that word and no "Lv N" survives anywhere in it.
            const string kitPath = "Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs";
            string kitRaw = ReadRaw(kitPath);
            if (kitRaw == null)
                Fail(MissingSourceMessage("[no-number]", kitPath,
                    "the proof that the target frame renders the badge word and builds no Lv label"));
            else
            {
                // Comment-stripped, so the file may still EXPLAIN the retired "Lv N" - it just
                // may not BUILD one. (A raw scan would be faked by the explanation itself.)
                if (StripComments(kitRaw).Contains("\"Lv "))
                    Fail("[no-number] " + kitPath + " still builds a \"Lv \" label. The target frame's " +
                         "gold slot carries the authored badge word now, never a level number.");
                if (!Regex.IsMatch(Strip(kitRaw), @"badge\.text\s*=\s*targetBadge"))
                    Fail("[no-number] TargetFrameHandle.Set no longer writes the badge into its text " +
                         "slot - the word would never reach the screen.");
                if (!Regex.IsMatch(Strip(kitRaw), @"Set\s*\(\s*_bound\.Name\s*,\s*_bound\.Badge"))
                    Fail("[no-number] TargetFrameHandle.Bind no longer forwards TargetModel.Badge, so " +
                         "boss-ness dies at the model boundary exactly as it used to.");
            }

            // (c) The producer publishes the badge, not a level, and no longer grades a threat tier.
            const string prodPath = "Assets/_Modules/Village/HUD/HudModelProducers.cs";
            string prod = ReadStripped(prodPath);
            if (prod == null)
                Fail(MissingSourceMessage("[no-number]", prodPath,
                    "the proof that TargetProducer publishes the badge, reads no en.Level and grades no threat tier"));
            else
            {
                if (!Regex.IsMatch(prod, @"Model\.Target\.Set\s*\(\s*true\s*,\s*name\s*,\s*badge"))
                    Fail("[no-number] TargetProducer no longer publishes the badge to TargetModel.");
                if (Regex.IsMatch(prod, @"int\s+level\s*=\s*en\.Level"))
                    Fail("[no-number] TargetProducer reads en.Level for the display again. Enemy.Level " +
                         "IS round(def.Hp / 25) - re-surfacing it re-ships the 'Lv 68' the owner saw.");
                if (Regex.IsMatch(prod, @"ThreatSkullPlate\s*\.\s*TierFor\s*\("))
                    Fail("[no-number] TargetProducer grades a threat tier again (TierFor). The RISKY / " +
                         "LETHAL banding is REMOVED from the player-facing path, not retuned.");
                if (Regex.IsMatch(prod, @"name\s*=\s*\(?\s*threatTier"))
                    Fail("[no-number] the \"!\"/\"!!\" threat prefix is back on the target name.");
            }
        }

        // ── 4 [banding] ───────────────────────────────────────────────────────────
        private static void CheckBandingOff(Action<string> Fail, List<string> notes)
        {
            if (ThreatSkullPlate.DisplayEnabled)
                Fail("[banding] ThreatSkullPlate.DisplayEnabled is TRUE. That re-ships a warning graded " +
                     "on HP/25 - owner: 'The Lv5 vs Lv36 comparison is downstream of the fake level. " +
                     "Retuning thresholds just polishes the wrong equation.'");

            const string platePath = "Assets/_Modules/Village/Combat/ThreatSkullPlate.cs";
            string plateRaw = ReadRaw(platePath);
            if (plateRaw == null)
                Fail(MissingSourceMessage("[banding]", platePath,
                    "the proof that the RISKY / LETHAL copy is gone from the plate"));
            else
            {
                if (plateRaw.Contains("\"RISKY\"") || plateRaw.Contains("\"LETHAL\""))
                    Fail("[banding] the RISKY / LETHAL copy is back in ThreatSkullPlate. The words were " +
                         "DELETED, not commented out - a dormant copy is the same fake precision waiting " +
                         "to be switched on.");
            }

            // Nothing outside the plate itself (and this oracle) may call the retired grader.
            string root = Application.dataPath;
            if (!Directory.Exists(root)) { Fail("[banding] Assets/ not found at '" + root + "'."); return; }
            var callers = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = Rel(file).Replace('\\', '/');
                // These two skips are RULE PREDICATES, not dependency guards - both files are
                // present and readable, and each is legally allowed to name TierFor. The plate
                // DECLARES it (and is separately asserted above: DisplayEnabled must be false and
                // the RISKY / LETHAL copy must be gone), and this oracle QUOTES it. Nothing is
                // delegated into the dark: a violation in either file is caught by those checks.
                if (rel.EndsWith("ThreatSkullPlate.cs", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith("WO1232EnemyLevelSourceRegression.cs", StringComparison.OrdinalIgnoreCase)) continue;
                string src;
                try { src = Strip(File.ReadAllText(file)); }
                catch (Exception e) { Fail("[banding] could not read " + rel + ": " + e.Message); continue; }
                // Qualified deliberately: several unrelated systems own their own TierFor
                // (ArcaneAura, ArenaVM, BuildTimerConfig.TierForCost, WandererDialogue). Only a
                // call to THIS plate's retired grader is the violation.
                if (Regex.IsMatch(src, @"ThreatSkullPlate\s*\.\s*TierFor\s*\(")) callers.Add(rel);
            }
            foreach (string c in callers)
                Fail("[banding] " + c + " calls ThreatSkullPlate.TierFor. TierFor is DIAGNOSTIC ONLY - it " +
                     "grades a fake level (HP/25) against the hero's real one, which is what the owner " +
                     "ruled out. Nothing player-facing may consume it.");

            notes.Add("Threat display OFF (DisplayEnabled=false); TierFor has no caller outside the " +
                      "plate's own trace.");
        }

        // ── 5 [traces] ────────────────────────────────────────────────────────────
        // CLAUDE.md S12: the ruling removes a DISPLAY. It must not remove instrumentation.
        private static void CheckInstrumentationKept(Action<string> Fail)
        {
            const string platePath = "Assets/_Modules/Village/Combat/ThreatSkullPlate.cs";
            string plate = ReadRaw(platePath);
            if (plate == null)
                Fail(MissingSourceMessage("[traces]", platePath,
                    "the proof that the plate FlowTrace instrumentation survived the display removal"));
            else if (!plate.Contains("FlowTrace"))
                Fail("[traces] ThreatSkullPlate has NO FlowTrace left. CLAUDE.md S12: instrumentation is " +
                     "PERMANENT - flag the display off, never strip the traces. A stripped Warn/Fail turns " +
                     "a logged failure back into a silent one.");

            const string prodPath = "Assets/_Modules/Village/HUD/HudModelProducers.cs";
            string prod = ReadRaw(prodPath);
            if (prod == null)
                Fail(MissingSourceMessage("[traces]", prodPath,
                    "the proof that the target frame [Flow:HudTarget] source line survived"));
            else if (!prod.Contains("HudTarget") || !prod.Contains("target classification resolved"))
                Fail("[traces] the target frame's [Flow:HudTarget] line that NAMES what the player is " +
                     "about to read is gone. The next 'why does it say that' must be one log read.");
        }

        // ── 6 [guard] ─────────────────────────────────────────────────────────────
        // The one that stops this returning a third time - and proves the scope held.
        private static void CheckGuards(Action<string> Fail, List<string> notes)
        {
            string root = Application.dataPath;
            if (!Directory.Exists(root))
            {
                Fail("[guard] Assets/ not found at '" + root + "' - the sweep cannot pass without sweeping.");
                return;
            }

            var banned = new List<KeyValuePair<string, Regex>>
            {
                // The retired heuristic's fingerprints.
                new KeyValuePair<string, Regex>("maxHp-derived level",
                    new Regex(@"(?i)max\s*hp\s*/\s*25f")),
                new KeyValuePair<string, Regex>("maxHp/hp fallback probe",
                    new Regex(@"(?i)MaxHp\s*>\s*0\.001f\s*\?")),
                new KeyValuePair<string, Regex>("EnemyLevelStub",
                    new Regex(@"EnemyLevelStub")),
                // WO-1232 scope: neither of these may exist yet.
                new KeyValuePair<string, Regex>("APEX badge (RESERVED - not authored yet)",
                    new Regex(@"""APEX""")),
                new KeyValuePair<string, Regex>("Combat Rating (a SEPARATE, unbuilt spec)",
                    new Regex(@"CombatRating")),
            };

            int scanned = 0;
            var hits = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (Rel(file).Replace('\\', '/')
                        .EndsWith("WO1232EnemyLevelSourceRegression.cs", StringComparison.OrdinalIgnoreCase))
                    continue;   // this oracle names the banned shapes on purpose

                string rawText;
                try { rawText = File.ReadAllText(file); }
                catch (Exception e) { Fail("[guard] could not read " + file + ": " + e.Message); continue; }

                scanned++;
                // Comments AND string literals are stripped for the code shapes; the two
                // COPY bans need the literals, so they run on a comment-only strip.
                string stripped = Strip(rawText);
                string noComments = StripComments(rawText);
                foreach (KeyValuePair<string, Regex> b in banned)
                {
                    bool copyBan = b.Key.StartsWith("APEX", StringComparison.Ordinal);
                    Match m = b.Value.Match(copyBan ? noComments : stripped);
                    // No match = the file is CLEAN. This is the good path (a violation is absent),
                    // not a dependency guard: the file was read, scanned++ already counted it, and
                    // scanned == 0 below is what catches a sweep that examined nothing.
                    if (!m.Success) continue;
                    hits.Add(b.Key + " in " + Rel(file) + " ('" +
                             Excerpt(copyBan ? noComments : stripped, m.Index) + "')");
                }
            }

            if (scanned == 0)
                Fail("[guard] swept ZERO .cs files - a sweep that scans nothing passes everything.");

            foreach (string h in hits)
                Fail("[guard] banned shape present: " + h + ". Levels are not derived from HP any more; " +
                     "APEX stays RESERVED until a tier is authored; and Combat Rating needs its own spec " +
                     "and its own owner ruling before a line of it is written.");

            notes.Add("Swept " + scanned + " .cs files: no HP-derived level, no APEX badge, no Combat " +
                      "Rating stub.");
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        // NEITHER READER ASSERTS ANYTHING. They return null on a missing/unreadable file and it
        // is the CALLER that must FAIL at its own guard. That split is deliberate: while the helper
        // failed on the caller's behalf, every call site was left holding a bare
        // `if (raw == null) return;` - a guard that returns having asserted nothing AT THE GUARD,
        // which the hollow-pass ratchet correctly reads as a case passing while running zero checks.
        // Every call site below asserts through FailMissingSource.
        private static string ReadRaw(string relative)
        {
            string full = Path.Combine(ProjectRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return null;
            try { return File.ReadAllText(full); }
            catch (Exception) { return null; }
        }

        private static string ReadStripped(string relative)
        {
            string raw = ReadRaw(relative);
            return raw == null ? null : Strip(raw);
        }

        /// <summary>
        /// FIXTURE-ABSENT, and therefore a FAIL - never a skip and never a silent return.
        /// Every path this oracle reads (enemies.json plus the .cs files it lints) is TRACKED
        /// SOURCE committed to the repo: not an optional fixture, and not a harness capability a
        /// batchmode run may legitimately lack. If one cannot be read, the assertions it feeds are
        /// unverifiable and the case is meaningless - so the honest report is RED, naming the path
        /// and naming what went unchecked.
        /// </summary>
        // Returns the MESSAGE; the caller does the asserting. Deliberately NOT
        // `Fail(MissingSourceMessage(...))`: HollowPassScanner recognises `Fail(...)` /
        // `failures.Add(...)` at the guard's own depth, and cannot see an assertion made
        // inside a helper that was handed the delegate. Keeping the call visible at the
        // site is what makes the guard PROVABLY non-hollow rather than merely correct.
        // Do NOT re-wrap this into a void helper, and do NOT teach the scanner a
        // `Fail*` name whitelist - that would let a future non-asserting helper hide a
        // real hollow pass, which is the exact defect class this oracle exists to catch.
        private static string MissingSourceMessage(string label, string relative,
                                                   string whatWentUnchecked)
        {
            return (label + " fixture-absent: TRACKED SOURCE " + relative + " is missing or unreadable, " +
                 "so " + whatWentUnchecked + " was NOT checked. This is a FAIL rather than a stand-down " +
                 "because the file is committed to the repo - its absence means this oracle is broken, " +
                 "and a quiet return would land in the GREEN column having asserted nothing.");
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

        // Comments only — used where the ban is on player-visible COPY (a string literal).
        // The null arm is a pure-function total, NOT a dependency guard: every caller has already
        // proved its input non-null through FailMissingSource, and each one then asserts on the
        // returned CONTENT, so an empty string cannot silence anything.
        private static string StripComments(string src)
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
                sb.Append(c);
            }
            return sb.ToString();
        }

        // Comments AND string literals removed, so neither prose nor a log message can fake a
        // pass or a failure. Same shape as AggroLeashRegression.StripCode. Its null arm is a
        // pure-function total for the same reason StripComments gives above - not a guard.
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
