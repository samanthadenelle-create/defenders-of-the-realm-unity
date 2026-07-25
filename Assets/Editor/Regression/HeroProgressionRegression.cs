// =============================================================================
// HeroProgressionRegression — headless "real object in, real response out" oracle
// for the hero XP / level / reward curves (village-hero silo).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core), so
// it exercises the REAL HeroProgression code the runtime runs — not a re-derivation.
//
// WHAT IT PROVES (all data/logic-decidable, no scene / no PlayMode):
//   1. XP CURVE (private static XpToNextFor, read by reflection = the SAME function the
//      level-up loop consults): the front-loaded quadratic is strictly increasing and
//      positive, and its authored anchors hold (L1->2=150, L2->3=1000, L3->4=2850,
//      L4->5=5700 — owner's tuned curve, HeroProgression file comment).
//   2. WISDOM CURVE (private static WisdomForLevel): +2/level through L8, +3/level after,
//      and the cumulative Wisdom earned reaching L20 (levels 2..20) == 50 — the owner's
//      v2 "specialize" budget (~70% of a whole tree). A drift here silently breaks the
//      talent-economy balance.
//   3. LIVE LEVEL-UP via the REAL public API on a REAL HeroProgression instance:
//      AddXp(150) yields exactly one level (L1->L2); DamageMultiplier is 1.0 at L1,
//      1.06 at L2 (+6%/level), and CAPS at the 3.0 ceiling at high level.
//
// This is a HEALTHY-INVARIANT guard (expected PASS): it locks the tuned curves so a
// future JSON/const edit that shifts them fails the gate instead of shipping a broken
// power/economy curve.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!HeroProgressionRegression.Run(out var heroProgReason)) failures.Add(heroProgReason); else log.AppendLine("[hero-prog] " + heroProgReason);
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class HeroProgressionRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- HERO PROGRESSION (XP curve + Wisdom curve + level-up rewards) ---");

            GameObject throwaway = null;
            try
            {
                // (1) XP CURVE — reflect the REAL private static XpToNextFor(int). Fail loud if
                //     the seam moved (never silently pass a vacuous test).
                var xpFn = typeof(HeroProgression).GetMethod(
                    "XpToNextFor", BindingFlags.NonPublic | BindingFlags.Static);
                if (xpFn == null)
                {
                    failures.Add("could not reflect HeroProgression.XpToNextFor(int) — the XP-curve seam was renamed/removed; re-point this oracle");
                }
                else
                {
                    float Xp(int lvl) => (float)xpFn.Invoke(null, new object[] { lvl });

                    // Authored anchors (HeroProgression comment): L1->2=150, L2->3=1000, L3->4=2850, L4->5=5700.
                    (int lvl, float want)[] anchors = { (1, 150f), (2, 1000f), (3, 2850f), (4, 5700f) };
                    foreach (var a in anchors)
                    {
                        float got = Xp(a.lvl);
                        if (Mathf.Abs(got - a.want) > 0.5f)
                            failures.Add($"XpToNextFor({a.lvl}) = {got} but the owner-tuned curve wants {a.want} (front-loaded quadratic drifted)");
                    }

                    // Strictly increasing + positive across the playable band.
                    float prev = -1f;
                    for (int lvl = 1; lvl <= 30; lvl++)
                    {
                        float v = Xp(lvl);
                        if (v <= 0f)
                            failures.Add($"XpToNextFor({lvl}) = {v} — non-positive XP threshold (curve broken)");
                        if (lvl > 1 && v <= prev)
                            failures.Add($"XpToNextFor not strictly increasing at level {lvl}: {v} <= prev {prev} (later waves must cost MORE, not less)");
                        prev = v;
                    }
                    log.AppendLine($"XP curve: L1={Xp(1)} L2={Xp(2)} L3={Xp(3)} L4={Xp(4)} L20={Xp(20)} (increasing, positive)");
                }

                // (2) WISDOM CURVE — reflect the REAL private static WisdomForLevel(int).
                var wisFn = typeof(HeroProgression).GetMethod(
                    "WisdomForLevel", BindingFlags.NonPublic | BindingFlags.Static);
                if (wisFn == null)
                {
                    failures.Add("could not reflect HeroProgression.WisdomForLevel(int) — the Wisdom-curve seam was renamed/removed; re-point this oracle");
                }
                else
                {
                    int Wis(int lvl) => (int)wisFn.Invoke(null, new object[] { lvl });

                    // Band boundary: <=8 -> 2, >8 -> 3.
                    if (Wis(8) != 2) failures.Add($"WisdomForLevel(8) = {Wis(8)} but the +2 band runs through L8 (want 2)");
                    if (Wis(9) != 3) failures.Add($"WisdomForLevel(9) = {Wis(9)} but the +3 band starts at L9 (want 3)");
                    for (int lvl = 2; lvl <= 8; lvl++)
                        if (Wis(lvl) != 2) failures.Add($"WisdomForLevel({lvl}) = {Wis(lvl)} (want 2 in the early band)");

                    // Cumulative Wisdom earned reaching L20 = sum over the levels GAINED (2..20).
                    // Owner's v2 budget = 50 (14 from L2..L8 @2 + 36 from L9..L20 @3).
                    int cumulative = 0;
                    for (int lvl = 2; lvl <= 20; lvl++) cumulative += Wis(lvl);
                    if (cumulative != 50)
                        failures.Add($"cumulative Wisdom reaching L20 = {cumulative} but the owner's 'specialize' budget wants 50 " +
                                     $"(too-generous or too-stingy curve breaks the talent economy)");
                    log.AppendLine($"Wisdom curve: W(8)=2 W(9)=3; cumulative L2..L20 = {cumulative} (want 50)");
                }

                // (2b) WISDOM IS LEVEL-UP-GATED (WO-763) — per-wave Wisdom must stay 0.
                //      The old flat +2/wave was the "lots of wisdom on every win" leak;
                //      arena-win + daily-quest direct grants were removed/redirected too.
                //      This asserts the wave leak stays closed (the checkable seam); if a
                //      future edit re-adds a per-wave Wisdom trickle this fails the gate.
                if (WaveFeedbackDirector.WisdomPerWave != 0)
                    failures.Add($"WaveFeedbackDirector.WisdomPerWave = {WaveFeedbackDirector.WisdomPerWave} but WO-763 requires 0 " +
                                 "(Wisdom is a level-up reward, not per-wave income — the 'earned not given' rule)");
                else
                    log.AppendLine("Wisdom source: per-wave = 0 (level-up-gated, WO-763)");

                // (3) LIVE LEVEL-UP through the REAL public API on a REAL instance.
                throwaway = new GameObject("HeroProgressionRegression_throwaway");
                var prog = throwaway.AddComponent<HeroProgression>();

                if (Mathf.Abs(prog.DamageMultiplier - 1f) > 0.001f)
                    failures.Add($"L1 DamageMultiplier = {prog.DamageMultiplier} (want 1.0 — no bonus at level 1)");
                if (prog.Level != 1)
                    failures.Add($"fresh HeroProgression Level = {prog.Level} (want 1)");

                int gained = prog.AddXp(150f);   // exactly the L1->L2 threshold
                if (gained != 1 || prog.Level != 2)
                    failures.Add($"AddXp(150) gained={gained} level={prog.Level} (want gained=1, level=2 — first level-up is cheap by design)");
                if (Mathf.Abs(prog.DamageMultiplier - 1.06f) > 0.001f)
                    failures.Add($"L2 DamageMultiplier = {prog.DamageMultiplier} (want 1.06 — +6%/level)");

                // Drive far up the curve; DamageMultiplier must CAP at the 3.0 ceiling.
                prog.AddXp(100_000_000f);
                if (prog.Level < 35)
                    failures.Add($"after a huge XP grant Level = {prog.Level} (expected >=35 to reach the multiplier cap — curve/loop may be stuck)");
                if (Mathf.Abs(prog.DamageMultiplier - 3f) > 0.001f)
                    failures.Add($"high-level DamageMultiplier = {prog.DamageMultiplier} (want the 3.0 cap — MaxDamageMultiplier not clamping)");
                log.AppendLine($"live: L1 mult=1.0, AddXp(150)->L2 mult=1.06, big grant -> L{prog.Level} mult={prog.DamageMultiplier} (cap 3.0)");
            }
            finally
            {
                if (throwaway != null) Object.DestroyImmediate(throwaway);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "HERO_PROGRESSION_OK");
                reason = "HERO PROGRESSION OK — XP curve increasing + anchored, Wisdom budget 50@L20, DamageMultiplier +6%/level capped at 3.0";
                return true;
            }
            reason = "hero-progression: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "HERO_PROGRESSION_FAIL: " + reason);
            return false;
        }
    }
}
