// =============================================================================
// EnemyWarmOrderRegression [enemy-warm-order]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
// Markers: ENEMY_WARM_ORDER_OK / ENEMY_WARM_ORDER_FAIL.
//
// Pins the 2026-08-20 ordered enemy warm (owner: "can we set the enemies to
// loadin order of seeing them when they are placing buldings? We know the first
// two enemies, they came in as pills").
//
// THE DEFECT IT GUARDS: EnemyContentWarmer.WarmFamily existed and NOTHING called
// it ahead of time — the only call sites were spawn-time, so the bundle download
// began on the frame the body was built and the player watched it finish as a
// tinted capsule. UpcomingWaveWarmPlanner now rings that bell from town dead
// time (BuildModeController.Enter), per family, in encounter order.
//
// ⛔ THE ORDER IS THE FEATURE, so every case here asserts SEQUENCE, not set
// membership. A gate that accepts any permutation cannot fail the known-bad
// state ("warm them in whatever order the dictionary enumerates"), and a gate
// that cannot fail the known-bad state is not a gate.
//
//   CASE 1 — ENCOUNTER ORDER. For every wave 1..30, the planner's family list
//     must equal the first-appearance order derived INDEPENDENTLY here by walking
//     the composition's entries in order. The case also proves it is capable of
//     failing: it requires at least one wave whose true order is NOT the
//     alphabetically sorted order, so a "sort the families" simplification is
//     detectable rather than accidentally equal.
//
//   CASE 2 — LOOK-AHEAD IS SIDE-EFFECT FREE ON UnityEngine.Random. Computing a
//     future wave's roster early is only safe because WaveCompositionBuilder.Build
//     restores Random.state before returning. If that ever stops being true, every
//     later gameplay roll shifts the moment the player opens the build palette —
//     an invisible, unreproducible divergence. Pinned two ways: the serialized
//     state struct is compared before/after, AND the next three Random.value draws
//     from a fixed seed must be byte-identical with and without a planning pass.
//
//   CASE 3 — ONLY WHAT THE ENCOUNTER CONTAINS. The plan for a wave must be a
//     subset of that wave's own families. Wave 1 is 100% weak/hollow, so its plan
//     must NOT contain the Orc / Troll families that exist in the game and appear
//     only from wave 3+. That is the assertion that fails if someone "simplifies"
//     the planner to warm every discovered family — the ~64 MB pull the per-family
//     seam exists to avoid (owner ruling: "broken down to each family of enemy").
//
//   CASE 4 — NOTHING ON THE NEW PATH BLOCKS, AND THE HOOK IS STILL WIRED. A
//     control-flow property, so a source scan is the right instrument: the planner
//     must contain ZERO blocking Addressables waits (there is no bounded
//     synchronous wait in Addressables 2.9.1 — see EnemyContentWarmer's header),
//     must reach Addressables only through the per-family WarmFamily, and
//     BuildModeController must still call WarmForTown. Deleting the hook would
//     otherwise restore the defect while compiling and passing every other case.
//
// SOURCE-LINT DISCIPLINE: every lint runs on source with comments AND string
// literals blanked, so no prose in this file or the files under test can satisfy
// a check (that is also why this header may name the forbidden call freely).
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.EnemyWarmOrderRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class EnemyWarmOrderRegression
    {
        private const string PlannerSourcePath =
            "Assets/_Modules/Village/Waves/UpcomingWaveWarmPlanner.cs";
        private const string BuildModeSourcePath =
            "Assets/_Modules/Village/BuildMode/BuildModeController.cs";

        /// <summary>How many waves the order cases sweep. Wide enough to cross every band
        /// boundary in WaveCompositionBuilder (1-2 weak only, 3-5 +orc, 6+ +troll/ogre, and
        /// the every-5th elite cadence).</summary>
        private const int WavesSwept = 30;

        /// <summary>Families that CANNOT appear in wave 1's roster — the builder's own band
        /// schedule keeps waves 1-2 hollow-only. Their presence in a wave-1 plan means the
        /// planner stopped planning and started warming everything.</summary>
        private static readonly string[] FamiliesAbsentFromWave1 = { "Orc", "Troll" };

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("ENEMY_WARM_ORDER_OK\n" + reason);
            else    Debug.LogError("ENEMY_WARM_ORDER_FAIL\n" + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes    = new List<string>();

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-warm-order case 1",
                () => Case1_PlanIsEncounterOrder(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-warm-order case 2",
                () => Case2_LookaheadDoesNotDisturbRandom(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-warm-order case 3",
                () => Case3_OnlyTheFamiliesTheWaveContains(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-warm-order case 4",
                () => Case4_NonBlockingAndHooked(failures, notes));

            if (failures.Count == 0)
            {
                reason = string.Join("; ", notes);
                return true;
            }

            var sb = new StringBuilder();
            sb.Append(failures.Count).Append(" failure(s):");
            foreach (string f in failures) sb.Append("\n  - ").Append(f);
            if (notes.Count > 0) sb.Append("\n  (context: ").Append(string.Join("; ", notes)).Append(')');
            reason = sb.ToString();
            return false;
        }

        // =====================================================================
        //  CASE 1 — the plan IS the first-appearance order
        // =====================================================================
        private static void Case1_PlanIsEncounterOrder(List<string> failures, List<string> notes)
        {
            EnemyCatalog catalog = LoadCatalog(failures);
            if (catalog == null) return;
            int wavesChecked = 0;

            for (int waveId = 1; waveId <= WavesSwept; waveId++)
            {
                EnemyWaveComposition comp = WaveCompositionBuilder.Build(waveId, false, catalog);
                if (comp == null || comp.Entries.Count == 0)
                {
                    failures.Add("[case1] WaveCompositionBuilder.Build(" + waveId + ") produced an EMPTY " +
                                 "composition - this case cannot assert an order over nothing.");
                    return;
                }

                // INDEPENDENT expectation: walk the entries in the order the spawner releases
                // them, resolve each id to its model's family, keep the FIRST appearance. This
                // duplicates the RULE on purpose so the assertion is not "PlanFamilies equals
                // itself" - only the id->model->family lookups are shared.
                var expected = new List<string>();
                for (int i = 0; i < comp.Entries.Count; i++)
                {
                    EnemyDef def = catalog.Find(comp.Entries[i].EnemyId);
                    string fam = (def?.Family ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(fam)) continue;
                    if (!ContainsIgnoreCase(expected, fam)) expected.Add(fam);
                }

                if (expected.Count == 0)
                {
                    failures.Add("[case1] wave " + waveId + " resolved to NO families at all - every entry's " +
                                 "model lookup came back empty. The planner would warm nothing and every " +
                                 "enemy would arrive as a capsule.");
                    return;
                }

                List<string> actual = UpcomingWaveWarmPlanner.PlanFamiliesForWave(waveId, false, catalog);

                if (!SameSequence(expected, actual))
                {
                    failures.Add("[case1] wave " + waveId + " WARM ORDER MISMATCH: expected first-appearance " +
                                 "order [" + string.Join(" -> ", expected) + "] but the planner returned [" +
                                 string.Join(" -> ", actual) + "]. The family the player sees FIRST must be " +
                                 "requested FIRST - that ordering is the whole feature; a set-equal answer in " +
                                 "the wrong order still shows the first enemy as a pill.");
                    return;
                }

                wavesChecked++;
            }

            // DETERMINISTIC PROOF OF TEETH. Generated waves are allowed to change their family
            // mix, so they cannot be trusted to happen to produce a non-alphabetical order.
            // This authored roster deliberately starts troll -> hollow -> orc (and repeats
            // troll): any implementation that sorts, groups from a dictionary, or fails to
            // preserve first appearance returns a different sequence.
            var synthetic = new EnemyWaveComposition { WaveId = 999 };
            synthetic.Entries.Add(new WaveCompositionEntry("troll", 1, SpawnRole.FrontTank, EnemyRole.Tank));
            synthetic.Entries.Add(new WaveCompositionEntry("hollow-walker", 1, SpawnRole.Weak, EnemyRole.DPS));
            synthetic.Entries.Add(new WaveCompositionEntry("orc-berserker", 1, SpawnRole.Melee, EnemyRole.DPS));
            synthetic.Entries.Add(new WaveCompositionEntry("troll-mage", 1, SpawnRole.Archer, EnemyRole.Healer));
            var authoredOrder = new List<string> { "troll", "hollow", "orc" };
            List<string> syntheticActual = UpcomingWaveWarmPlanner.PlanFamilies(synthetic, catalog);
            if (!SameSequence(authoredOrder, syntheticActual))
            {
                failures.Add("[case1] deterministic mixed-family roster expected first-appearance order [" +
                             string.Join(" -> ", authoredOrder) + "] but planner returned [" +
                             string.Join(" -> ", syntheticActual) + "]. This fixture is deliberately " +
                             "non-alphabetical and repeats troll, so sorting or failing to dedupe is caught.");
                return;
            }

            // The FTUE roster comes from a DIFFERENT source (TutorialWaveSpawner bypasses the
            // wave loop entirely) and is the encounter the owner actually reported. Assert it
            // resolves to exactly one family, or the "first two enemies" warm nothing.
            List<string> tutorial = UpcomingWaveWarmPlanner.PlanTutorialFamilies(catalog);
            if (tutorial.Count != 1)
            {
                failures.Add("[case1] the FTUE teaching-wave plan resolved to " + tutorial.Count +
                             " famil(ies) [" + string.Join(", ", tutorial) + "], expected exactly 1. " +
                             "TutorialWaveSpawner spawns exactly 2 bodies of ONE id (PreferredEnemyId) - " +
                             "if that no longer maps to a family, the owner's 'first two enemies' get no " +
                             "warm at all and arrive as pills, which is the exact report this closes.");
                return;
            }

            // And the FTUE family must LEAD the plan while the tutorial is unfinished: those
            // bodies are met before any composed wave, so anything ahead of them is bandwidth
            // spent on art the player has not reached.
            List<string> ftuePlan = UpcomingWaveWarmPlanner.PlanEncounterFamilies(true, 1, false, catalog);
            if (ftuePlan.Count == 0 || !string.Equals(ftuePlan[0], tutorial[0], StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("[case1] with the FTUE pending, the plan is [" + string.Join(" -> ", ftuePlan) +
                             "] - it does NOT lead with the teaching-wave family '" + tutorial[0] +
                             "'. The teaching wave is the next thing the player meets; anything warmed " +
                             "ahead of it is served before the bodies that are already on screen.");
                return;
            }

            notes.Add("[case1] " + wavesChecked + " generated wave(s) matched first-appearance order; " +
                      "deterministic mixed roster preserved [troll -> hollow -> orc]; FTUE plan leads with '" +
                      tutorial[0] + "'");
        }

        // =====================================================================
        //  CASE 2 — early planning must not consume or shift UnityEngine.Random
        // =====================================================================
        private static void Case2_LookaheadDoesNotDisturbRandom(List<string> failures, List<string> notes)
        {
            // --- 2a: the state struct itself is unchanged -----------------------
            UnityEngine.Random.InitState(20260820);
            string before = JsonUtility.ToJson(UnityEngine.Random.state);

            UpcomingWaveWarmPlanner.PlanEncounterFamilies(true, 7, false, null);
            UpcomingWaveWarmPlanner.PlanFamiliesForWave(12, true, null);

            string after = JsonUtility.ToJson(UnityEngine.Random.state);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                failures.Add("[case2] planning the upcoming wave MUTATED UnityEngine.Random.state (" +
                             before + " -> " + after + "). WaveCompositionBuilder.Build restores the " +
                             "state before returning, and that property is the ONLY reason a future " +
                             "wave's roster can be computed early at all. Broken, every gameplay roll " +
                             "after the player opens the build palette silently diverges - an invisible, " +
                             "unreproducible bug with no trace line anywhere.");
                return;
            }

            // --- 2b: the OBSERVABLE draws are unchanged ------------------------
            // Comparing the serialized struct is necessary but not sufficient - a future
            // Random.State layout could compare equal while the generator has advanced. Draw
            // the real sequence with and without a planning pass and require them identical.
            UnityEngine.Random.InitState(4242);
            var baseline = new float[3];
            for (int i = 0; i < baseline.Length; i++) baseline[i] = UnityEngine.Random.value;

            UnityEngine.Random.InitState(4242);
            UpcomingWaveWarmPlanner.PlanEncounterFamilies(false, 9, false, null);
            var afterPlanning = new float[3];
            for (int i = 0; i < afterPlanning.Length; i++) afterPlanning[i] = UnityEngine.Random.value;

            for (int i = 0; i < baseline.Length; i++)
            {
                if (baseline[i] != afterPlanning[i])
                {
                    failures.Add("[case2] a planning pass CHANGED the next Random draws from a fixed seed " +
                                 "(draw " + i + ": " + baseline[i] + " -> " + afterPlanning[i] + "). The " +
                                 "look-ahead is not side-effect free, so warming in town would change what " +
                                 "the game rolls afterwards.");
                    return;
                }
            }

            notes.Add("[case2] look-ahead left Random.state and the next 3 draws byte-identical");
        }

        // =====================================================================
        //  CASE 3 — only the families the encounter actually contains
        // =====================================================================
        private static void Case3_OnlyTheFamiliesTheWaveContains(List<string> failures, List<string> notes)
        {
            EnemyCatalog catalog = LoadCatalog(failures);
            if (catalog == null) return;
            int checkedWaves = 0;

            for (int waveId = 1; waveId <= WavesSwept; waveId++)
            {
                EnemyWaveComposition comp = WaveCompositionBuilder.Build(waveId, false, catalog);
                if (comp == null || comp.Entries.Count == 0) continue;

                var contained = new List<string>();
                for (int i = 0; i < comp.Entries.Count; i++)
                {
                    EnemyDef def = catalog.Find(comp.Entries[i].EnemyId);
                    string fam = (def?.Family ?? string.Empty).Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(fam) && !ContainsIgnoreCase(contained, fam)) contained.Add(fam);
                }

                List<string> plan = UpcomingWaveWarmPlanner.PlanFamiliesForWave(waveId, false, catalog);

                foreach (string fam in plan)
                {
                    if (!ContainsIgnoreCase(contained, fam))
                    {
                        failures.Add("[case3] wave " + waveId + "'s plan asks for family '" + fam +
                                     "', which the wave DOES NOT CONTAIN (its families are [" +
                                     string.Join(", ", contained) + "]). Enemy content is ~64 MB and the " +
                                     "owner's ruling is per-family, on demand: warming a family the " +
                                     "encounter never spawns is exactly the pull this seam exists to avoid.");
                        return;
                    }
                }

                checkedWaves++;
            }

            // The teeth: wave 1 is hollow-only by the builder's own band schedule, and the Orc /
            // Troll families demonstrably EXIST in the game (they enter at waves 3+ / 6+). A
            // planner that warmed everything would drag them into wave 1's plan.
            List<string> wave1 = UpcomingWaveWarmPlanner.PlanFamiliesForWave(1, false, catalog);
            foreach (string banned in FamiliesAbsentFromWave1)
            {
                if (ContainsIgnoreCase(wave1, banned))
                {
                    failures.Add("[case3] the WAVE 1 plan contains '" + banned + "', a family that first " +
                                 "appears several waves later. The plan is [" + string.Join(" -> ", wave1) +
                                 "]. This is the signature of a planner that warms every discovered family " +
                                 "instead of the upcoming roster.");
                    return;
                }
            }

            // ...and prove those families are reachable at all, so the check above is not vacuous.
            List<string> lateWave = UpcomingWaveWarmPlanner.PlanFamiliesForWave(8, false, catalog);
            bool sawALaterFamily = false;
            foreach (string banned in FamiliesAbsentFromWave1)
                if (ContainsIgnoreCase(lateWave, banned)) sawALaterFamily = true;

            if (!sawALaterFamily)
            {
                failures.Add("[case3] none of [" + string.Join(", ", FamiliesAbsentFromWave1) + "] appear in " +
                             "the wave 8 plan either ([" + string.Join(" -> ", lateWave) + "]), so the " +
                             "wave-1 exclusion above proves nothing - those families may simply be " +
                             "unreachable. Re-pick the marker families against the current wave bands.");
                return;
            }

            notes.Add("[case3] " + checkedWaves + " wave plan(s) were subsets of their own rosters; wave 1 = [" +
                      string.Join(" -> ", wave1) + "] excludes the later families that wave 8 = [" +
                      string.Join(" -> ", lateWave) + "] does contain");
        }

        // =====================================================================
        //  CASE 4 — non-blocking by construction, and still wired to town
        // =====================================================================
        private static void Case4_NonBlockingAndHooked(List<string> failures, List<string> notes)
        {
            string planner = ReadBlanked(PlannerSourcePath, failures);
            string buildMode = ReadBlanked(BuildModeSourcePath, failures);
            if (planner == null || buildMode == null) return;

            // The blocking call is spelled in two halves so this SOURCE cannot match itself
            // even before blanking - belt and braces on top of the comment/string stripper.
            string blocking = "WaitFor" + "Completion";
            int hits = CountOccurrences(planner, blocking);
            if (hits != 0)
            {
                failures.Add("[case4] " + PlannerSourcePath + " contains " + hits + " occurrence(s) of the " +
                             "blocking Addressables wait. There is NO bounded synchronous wait in " +
                             "Addressables 2.9.1 (AsyncOperationBase's implementation is a bare " +
                             "uninterruptible spin), and blocking from an engine callback deadlocked the " +
                             "game for three minutes on 2026-08-20. This path runs while the player is in " +
                             "town placing buildings - it must never wait.");
                return;
            }

            // Reaching Addressables ONLY through the per-family door. A direct Addressables call
            // here would be a second, unruled fetch path outside the per-family seam.
            if (!planner.Contains("EnemyContentWarmer.WarmFamily"))
            {
                failures.Add("[case4] " + PlannerSourcePath + " no longer calls " +
                             "EnemyContentWarmer.WarmFamily - the per-family door is the ONLY sanctioned " +
                             "way this path may fetch enemy content (owner ruling: broken down to each " +
                             "family of enemy).");
                return;
            }
            if (planner.Contains("Addressables."))
            {
                failures.Add("[case4] " + PlannerSourcePath + " calls Addressables directly. Every fetch on " +
                             "this path must go through EnemyContentWarmer, which owns the async " +
                             "discipline, the retention rule and the dedupe; a second door reintroduces " +
                             "the class of bug the warmer was written to remove.");
                return;
            }

            // It must yield, i.e. actually be coroutine-driven from the player loop rather than
            // a for-loop that fires everything on one frame (which loses the ordering benefit).
            if (!planner.Contains("yield return"))
            {
                failures.Add("[case4] " + PlannerSourcePath + " contains no yield - the ordered warm is no " +
                             "longer driven from the player loop, so either it blocks or it issues every " +
                             "family on one frame and the first enemy's bundle competes with the last's.");
                return;
            }

            // THE HOOK. Without this call the planner is dead code and the defect returns while
            // every other case in this file still passes.
            if (!buildMode.Contains("UpcomingWaveWarmPlanner.WarmForTown"))
            {
                failures.Add("[case4] " + BuildModeSourcePath + " no longer calls " +
                             "UpcomingWaveWarmPlanner.WarmForTown. Town dead time (build mode, waves " +
                             "frozen) is the trigger point the owner asked for; without the call nothing " +
                             "warms early and the first enemies of a wave arrive as tinted capsules again.");
                return;
            }

            notes.Add("[case4] planner is block-free (0 blocking waits), fetches only via WarmFamily, " +
                      "yields between families, and BuildModeController still rings it");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static EnemyCatalog LoadCatalog(List<string> failures)
        {
            const string path = "Assets/Resources/Data/Canonical/enemies.json";
            try
            {
                EnemyCatalog catalog = JsonConvert.DeserializeObject<EnemyCatalog>(File.ReadAllText(path));
                if (catalog?.Enemies != null && catalog.Enemies.Count > 0) return catalog;
                failures.Add("[catalog] enemies.json deserialized with no enemy rows.");
            }
            catch (Exception e)
            {
                failures.Add("[catalog] could not load enemies.json: " + e.GetType().Name + ": " + e.Message);
            }
            return null;
        }

        private static bool ContainsIgnoreCase(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool SameSequence(List<string> a, List<string> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static string ReadBlanked(string relPath, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), relPath);
            if (!File.Exists(full))
            {
                failures.Add("[case4] source file NOT FOUND: " + relPath +
                             " - the lint cannot assert anything about a file that is not there.");
                return null;
            }
            return StripCommentsAndStrings(File.ReadAllText(full));
        }

        /// <summary>Blank out comments, string literals and char literals so a lint can never be
        /// satisfied by prose or by a tombstone naming the very token it forbids.</summary>
        private static string StripCommentsAndStrings(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, inVerbatim = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                char n = i + 1 < raw.Length ? raw[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } else if (c == '\n') sb.Append(c); continue; }
                if (inVerbatim)
                {
                    if (c == '"' && n == '"') { i++; continue; }
                    if (c == '"') { inVerbatim = false; sb.Append('"'); }
                    else if (c == '\n') sb.Append(c);
                    continue;
                }
                if (inStr)
                {
                    if (c == '\\' && n != '\0') { i++; continue; }
                    if (c == '"') { inStr = false; sb.Append('"'); }
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\' && n != '\0') { i++; continue; }
                    if (c == '\'') { inChar = false; sb.Append('\''); }
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '@' && n == '"') { inVerbatim = true; sb.Append("\""); i++; continue; }
                if (c == '$' && n == '"') { inStr = true; sb.Append('"'); i++; continue; }
                if (c == '"') { inStr = true; sb.Append('"'); continue; }
                if (c == '\'') { inChar = true; sb.Append('\''); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
