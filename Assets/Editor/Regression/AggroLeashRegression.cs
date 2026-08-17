// =============================================================================
// AggroLeashRegression - the BAIT ALLOWANCE oracle (owner live-play 2026-08-16:
// "i was trying to target and bait an enemy out and i think we need to allow
//  aggro targets to extend leash alot more").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - registered into DataRegression.RunAll by the orchestrator (registration line
// lives with the orchestrator; this file only provides Run).
//
// WHAT THIS PINS, AND WHY EACH ONE IS HERE
//   1. [tuning]      aggro-tuning.json loads through AggroTuning, is versioned, and the
//                    chase bounds are ACTUALLY WIDER than the constants they replaced
//                    (arena 16m, dungeon wake 6m / anchor leash 10m). A "fix" that ships
//                    the old numbers back as data would otherwise pass silently.
//   2. [bounded]     ...and are NEVER infinite. Removing the leash entirely is a WORSE
//                    bug (an enemy follows the player across the map and into town), so
//                    "still bounded" is an assertion, not a comment.
//   3. [invariant]   the arena engage-contact radius always EXCEEDS the arena leash. If it
//                    ever inverts, a leashed (still-in-play) enemy reads as out-of-contact
//                    and the disengage watchdog resolves a LIVE FIGHT AS A LOSS.
//   4. [dual-copy]   the Resources and StreamingAssets copies are byte-identical + ASCII
//                    (the canonical-data law; a drifted pair ships a retune to the editor
//                    but not the device).
//   5. [chase]       EnemyBrain.ShouldHoldChase: an ENGAGED mob pursues PAST the old
//                    notice ring (the actual bug), breaks past the new bound, and a
//                    chaseLeash <= 0 reverts to the exact pre-fix behaviour.
//   6. [dormant]     the WO-770.11 / WO-797 guarantees are untouched: a mob that never
//                    engaged stays dormant, and the hero at the captured 8.1m entry seat
//                    still does NOT wake the junction room (the entrance-camp defect).
//   7. [wave-clear]  the second-order check the owner asked for: nothing that decides a
//                    wave is CLEAR depends on where an enemy stands. WaveManager clears on
//                    _liveEnemies.Count == 0 (kill-based), so a longer chase cannot strand
//                    a wave. Source-linted, comments AND string literals stripped.
//   8. [wired]       the constants are really gone: BattleArena reads AggroTuning instead
//                    of 16f/18f/7f, EnemyBrain's pursuit widening is gated on the engaged
//                    latch, and the latch is cleared on pool reset. Source-linted with
//                    comments AND string literals stripped, so prose cannot fake a pass.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class AggroLeashRegression
    {
        // The constants this fix REPLACED. The new bounds must beat them, or nothing changed.
        private const float OldArenaLeash      = 16f;
        private const float OldRoomWakeRadius  = 6f;
        private const float OldAnchorLeash     = 10f;

        // Sanity ceiling: generous is the point, unbounded is a different (worse) bug.
        private const float MaxSaneLeash = 150f;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            void Fail(string s) => failures.Add("AGGRO_LEASH FAIL: " + s);

            try
            {
                CheckTuning(Fail);
                CheckDualCopy(Fail);
                CheckChaseHold(Fail);
                CheckDormantGuarantees(Fail);
                CheckSourceWiring(Fail);
            }
            catch (Exception ex)
            {
                Fail("threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = string.Join("\n", failures);
                return false;
            }

            reason = "AGGRO_LEASH OK - bait allowance live: arena leash " +
                     AggroTuning.ArenaChaseLeashRadius.ToString("0.#") + "m (was " +
                     OldArenaLeash.ToString("0.#") + "m), brain chase leash " +
                     AggroTuning.BrainChaseLeashRadius.ToString("0.#") + "m (was the " +
                     OldRoomWakeRadius.ToString("0.#") + "m wake / " + OldAnchorLeash.ToString("0.#") +
                     "m anchor ring), both BOUNDED; contact " +
                     AggroTuning.EffectiveArenaEngageContactRadius.ToString("0.#") +
                     "m > leash; dual-copy identical; dormancy + entrance-camp guards intact; " +
                     "wave-clear is kill-based (position-independent).";
            return true;
        }

        // 1/2/3 - the data actually widened, stayed finite, and keeps its invariant.
        private static void CheckTuning(Action<string> Fail)
        {
            AggroTuning.Reload();

            if (AggroTuning.Version < 1)
                Fail("[tuning] aggro-tuning.json version " + AggroTuning.Version + " (expected >= 1)");

            float arena = AggroTuning.ArenaChaseLeashRadius;
            float brain = AggroTuning.BrainChaseLeashRadius;
            float slack = AggroTuning.BrainEngagedPursuitSlack;
            float disengage = AggroTuning.ArenaDisengageSeconds;

            // [tuning] - wider than what it replaced, or the owner's finding is unaddressed.
            if (arena <= OldArenaLeash)
                Fail("[tuning] arena chaseLeashRadius " + arena.ToString("0.#") + "m must EXCEED the old " +
                     OldArenaLeash.ToString("0.#") + "m const - the whole point is that a bait is no longer snapped back");
            if (brain <= OldAnchorLeash)
                Fail("[tuning] brain chaseLeashRadius " + brain.ToString("0.#") + "m must EXCEED the old " +
                     OldAnchorLeash.ToString("0.#") + "m anchor leash - a dungeon bait would still die at the notice ring");
            if (slack <= OldRoomWakeRadius)
                Fail("[tuning] brain engagedPursuitSlack " + slack.ToString("0.#") + "m must EXCEED the old pursuit " +
                     "bound (max(slack, wake) = " + OldRoomWakeRadius.ToString("0.#") + "m) or an engaged mob still pins on the room face");

            // [bounded] - generous, never infinite. This is the "do NOT remove the leash" pin.
            if (float.IsInfinity(arena) || float.IsNaN(arena) || arena > MaxSaneLeash)
                Fail("[bounded] arena chaseLeashRadius " + arena + " is unbounded/insane - an unleashed enemy follows the player into town");
            if (float.IsInfinity(brain) || float.IsNaN(brain) || brain > MaxSaneLeash)
                Fail("[bounded] brain chaseLeashRadius " + brain + " is unbounded/insane - an unleashed mob follows the player out of the dungeon");
            if (float.IsInfinity(slack) || float.IsNaN(slack) || slack > MaxSaneLeash)
                Fail("[bounded] brain engagedPursuitSlack " + slack + " is unbounded/insane");
            if (disengage <= 0f || disengage > 120f)
                Fail("[bounded] arena disengageSeconds " + disengage + " must be positive and well under the 240s battle timeout");

            // [invariant] - contact ring must stay above the leash or live fights resolve as losses.
            float contact = AggroTuning.EffectiveArenaEngageContactRadius;
            if (contact <= arena)
                Fail("[invariant] engage-contact radius " + contact.ToString("0.#") + "m must EXCEED the arena leash " +
                     arena.ToString("0.#") + "m - a leashed enemy would read out-of-contact and the watchdog would " +
                     "force-resolve a live fight as a LOSS");
        }

        // 4 - canonical dual-copy law.
        private static void CheckDualCopy(Action<string> Fail)
        {
            string res = Path.Combine(Application.dataPath, "Resources/Data/Canonical/aggro-tuning.json");
            string sa  = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/aggro-tuning.json");
            if (!File.Exists(res)) { Fail("[dual-copy] missing Resources copy: " + res); return; }
            if (!File.Exists(sa))  { Fail("[dual-copy] missing StreamingAssets copy: " + sa); return; }

            byte[] a = File.ReadAllBytes(res);
            byte[] b = File.ReadAllBytes(sa);
            if (a.Length != b.Length)
            {
                Fail("[dual-copy] aggro-tuning.json copies differ in length (" + a.Length + " vs " + b.Length + ")");
                return;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) { Fail("[dual-copy] aggro-tuning.json copies differ at byte " + i); return; }
                if (a[i] > 127)   { Fail("[dual-copy] aggro-tuning.json contains non-ASCII bytes"); return; }
                if (a[i] == 0)    { Fail("[dual-copy] aggro-tuning.json contains a NUL byte"); return; }
            }
        }

        // 5 - the pure chase decision. THE bug, replayed.
        private static void CheckChaseHold(Action<string> Fail)
        {
            float chase = AggroTuning.BrainChaseLeashRadius;

            // (a) THE BUG: an ENGAGED mob with the hero just past the old notice ring. Before
            //     the fix this leashed out (wake radius doubled as the chase cap) - the bait died.
            float justPastOldRing = OldAnchorLeash + 2f;
            if (!EnemyBrain.ShouldHoldChase(engaged: true, wantsEngage: false, heroPresent: true,
                                            heroDistanceFromHome: justPastOldRing, chaseLeash: chase))
                Fail("[chase] an engaged mob must KEEP chasing at " + justPastOldRing.ToString("0.#") +
                     "m from home (past the old " + OldAnchorLeash.ToString("0.#") +
                     "m ring) - this is exactly the bait the owner could not perform");

            float justPastOldWake = OldRoomWakeRadius + 2f;
            if (!EnemyBrain.ShouldHoldChase(true, false, true, justPastOldWake, chase))
                Fail("[chase] an engaged ROOM mob must keep chasing at " + justPastOldWake.ToString("0.#") +
                     "m from its room footprint (past the old " + OldRoomWakeRadius.ToString("0.#") + "m wake gate)");

            // (b) STILL BOUNDED: past the new bound the chase breaks and the mob goes home.
            if (EnemyBrain.ShouldHoldChase(true, false, true, chase + 0.5f, chase))
                Fail("[chase] the chase must BREAK past the bound (" + chase.ToString("0.#") +
                     "m) - an unbounded chase follows the player across the map and into town");
            if (EnemyBrain.ShouldHoldChase(true, false, true, 5000f, chase))
                Fail("[chase] a hero 5km away must never hold the chase");

            // (c) boundary: exactly AT the bound counts as still engaged (matches the > test).
            if (!EnemyBrain.ShouldHoldChase(true, false, true, chase, chase))
                Fail("[chase] distance exactly equal to the chase leash must count as INSIDE the bound");

            // (d) fully revertible from data: chaseLeash <= 0 == pre-fix behaviour EXACTLY.
            if (EnemyBrain.ShouldHoldChase(true, false, true, 1f, 0f))
                Fail("[chase] chaseLeash 0 must restore the legacy behaviour (notice ring == chase cap)");
            if (EnemyBrain.ShouldHoldChase(true, false, true, 1f, -5f))
                Fail("[chase] a negative chaseLeash must be treated as disabled, not as an infinite chase");

            // (e) no hero, no chase - a mob may never chase a null.
            if (EnemyBrain.ShouldHoldChase(true, true, heroPresent: false, heroDistanceFromHome: 1f, chaseLeash: chase))
                Fail("[chase] an absent hero must always break the chase");

            // (f) the measuring stick itself: inside the room counts as 0, outside is planar XZ.
            var room = new Bounds(new Vector3(0f, 2f, 12f), new Vector3(6f, 4f, 6f));
            float inside = EnemyBrain.HeroDistanceFromHome(true, room, Vector3.zero, new Vector3(0f, 0f, 12f));
            if (inside > 0.01f)
                Fail("[chase] a hero INSIDE the room must measure 0m from home (got " + inside.ToString("0.00") + ")");
            float anchored = EnemyBrain.HeroDistanceFromHome(false, room, new Vector3(0f, 0f, 0f), new Vector3(3f, 99f, 4f));
            if (Mathf.Abs(anchored - 5f) > 0.01f)
                Fail("[chase] anchor distance must be PLANAR (expected 5m for a 3/4 offset, got " + anchored.ToString("0.00") + ")");
        }

        // 6 - the guarantees the previous fixes bought must survive this one.
        private static void CheckDormantGuarantees(Action<string> Fail)
        {
            float chase = AggroTuning.BrainChaseLeashRadius;

            // WO-770.11: a mob that NEVER engaged stays dormant no matter how close the bound is.
            if (EnemyBrain.ShouldHoldChase(engaged: false, wantsEngage: false, heroPresent: true,
                                           heroDistanceFromHome: 1f, chaseLeash: chase))
                Fail("[dormant] a mob that never engaged must stay dormant - the far-room beeline (WO-770.11) would return");

            // WO-797 entrance camp: the captured entry seat is 8.1m from the junction footprint,
            // beyond the 6m wake - it must STILL not wake the room. The notice ring is untouched.
            var junction = new Bounds(new Vector3(0f, 2f, 12f), new Vector3(6f, 4f, 6f));
            var entrySeat = new Vector3(0f, 0f, 0.9f);
            float d = EnemyBrain.HeroDistanceFromHome(true, junction, Vector3.zero, entrySeat);
            if (Mathf.Abs(d - 8.1f) > 0.05f)
                Fail("[dormant] entry-seat -> junction footprint distance " + d.ToString("0.00") + " (expected ~8.1) - precondition drift");
            if (EnemyBrain.ShouldWake(junction, OldRoomWakeRadius, true, entrySeat))
                Fail("[dormant] the hero at the entry seat must NOT wake the junction room - the WO-797 " +
                     "'all enemies gathered at the entrance' camp would return");

            // The legacy anchor leash decision is byte-for-byte unchanged (notice ring only).
            if (!EnemyBrain.ShouldLeashOut(Vector3.zero, OldAnchorLeash, true, new Vector3(50f, 0f, 0f)))
                Fail("[dormant] ShouldLeashOut must still leash a far hero out - the notice ring was not supposed to change");
            if (EnemyBrain.ShouldLeashOut(Vector3.zero, 0f, true, new Vector3(500f, 0f, 0f)))
                Fail("[dormant] radius 0 (default) must NEVER leash - unleashed village/overworld enemies stay unaffected");
        }

        // 7/8 - source lint. Comments AND string literals are stripped first, so prose
        // (of which these files have plenty) can never fake a pass.
        private static void CheckSourceWiring(Action<string> Fail)
        {
            string arena = StripCode(Read("_Modules/Village/Arena/BattleArena.cs"), Fail, "BattleArena.cs");
            string brain = StripCode(Read("_Modules/Village/Enemies/EnemyBrain.cs"), Fail, "EnemyBrain.cs");
            string waves = StripCode(Read("_Modules/Village/Waves/WaveManager.cs"), Fail, "WaveManager.cs");
            if (arena == null || brain == null || waves == null) return;

            // [wired] the arena constants are GONE and the data is read instead.
            if (Regex.IsMatch(arena, @"LeashRadius\s*=\s*16f") ||
                Regex.IsMatch(arena, @"EngageContactRadius\s*=\s*18f") ||
                Regex.IsMatch(arena, @"DisengageResolveSeconds\s*=\s*7f"))
                Fail("[wired] BattleArena still hardcodes the 16f/18f/7f leash constants - the owner cannot tune them without a rebuild");
            if (arena.IndexOf("AggroTuning.ArenaChaseLeashRadius", StringComparison.Ordinal) < 0)
                Fail("[wired] BattleArena does not read AggroTuning.ArenaChaseLeashRadius - the arena leash is not data-driven");
            if (arena.IndexOf("AggroTuning.EffectiveArenaEngageContactRadius", StringComparison.Ordinal) < 0)
                Fail("[wired] BattleArena must read the INVARIANT-ENFORCING contact radius, not the raw JSON value");

            // [wired] the brain's widening is gated on the engaged latch, and the latch resets.
            if (brain.IndexOf("_chaseEngaged", StringComparison.Ordinal) < 0)
                Fail("[wired] EnemyBrain has no _chaseEngaged latch - the notice ring is still the chase cap");
            if (!Regex.IsMatch(brain, @"PursuitSlack\s*=>[\s\S]{0,240}_chaseEngaged"))
                Fail("[wired] EnemyBrain.PursuitSlack must widen ONLY while _chaseEngaged - an ungated widening re-opens the WO-797 entrance camp");
            if (!Regex.IsMatch(brain, @"ResetForPool[\s\S]{0,4000}_chaseEngaged\s*=\s*false"))
                Fail("[wired] EnemyBrain.ResetForPool must clear _chaseEngaged - a pooled body would inherit the previous life's chase");
            if (brain.IndexOf("AggroTuning.BrainChaseLeashRadius", StringComparison.Ordinal) < 0)
                Fail("[wired] EnemyBrain does not read AggroTuning.BrainChaseLeashRadius");

            // [wave-clear] the second-order guard: wave completion is KILL-based, never positional.
            // A longer chase moves enemies far from their spawn, so if clear-detection depended on
            // position at all, this fix would stall waves. It does not - pin that.
            if (waves.IndexOf("_liveEnemies.Count == 0", StringComparison.Ordinal) < 0)
                Fail("[wave-clear] WaveManager no longer clears on _liveEnemies.Count == 0 - if wave completion " +
                     "became positional, a longer chase could strand a wave");
        }

        private static string Read(string relative)
        {
            string p = Path.Combine(Application.dataPath, relative);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        // Strip // and /* */ comments AND "..." / '...' literals, so a match can only come
        // from real code. Deliberately simple + conservative: it blanks the contents, never
        // reflows the file.
        private static string StripCode(string src, Action<string> Fail, string label)
        {
            if (src == null) { Fail("[wired] could not read " + label); return null; }
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
