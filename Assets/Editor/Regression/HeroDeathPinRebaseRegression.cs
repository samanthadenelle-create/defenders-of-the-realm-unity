// =============================================================================
// HeroDeathPinRebaseRegression [hero-death-pin] — locks the F8 2026-08-10 fix
// (seq 2253/2254/2255, "when the player dies, he shakes then dies"):
//
//   The hero died INSIDE the BattleArena warp-space (~(4988, 5000)) and the death
//   freeze pinned the corpse there (HeroHealth.EnterDeathFreeze). The arena's
//   ReturnHomeWithFade then warped the hero ~7km home — and HeroHealth.LateUpdate's
//   pin watchdog read that LEGITIMATE move as a residual and re-pinned the corpse
//   back at the STALE arena spot, while BattleArena.VerifyReturnPose re-asserted
//   town. Two alternating writers = the visible death shake + a hero resting at
//   the wrong position.
//
//   THE FIX (one owner per concern): HeroLocomotion.WarpTo — the ONE sanctioned
//   teleport authority (arena stage/return warps, seam crossings, gate traversal,
//   hub spawn injection all route through it) — REBASES the pin to the landed pose
//   via HeroHealth.RebaseDeathPin. The pin then holds the NEW pose; an UNsanctioned
//   mover is still fought and named. Neither watchdog net is deleted (§12: the nets
//   caught this; "fix the writer, not this net").
//
// This suite pins the whole chain so the fix cannot silently regress:
//   1. HeroHealth defines the public RebaseDeathPin(Vector3, Quaternion, string)
//      seam, it no-ops when no pin is armed, and it rewrites BOTH pin pose fields.
//   2. HeroLocomotion.WarpTo actually CALLS the rebase (the hook at the authority —
//      without this call the seam is dead code and the shake returns).
//   3. The LateUpdate pin watchdog SURVIVES: it still re-asserts _deathPinPos and
//      still names a residual mover (permanent instrumentation, never "cleanup").
//   4. ExitDeathFreeze still releases the pin, so the respawn-moves-you flow
//      (canon 2026-08-02) is never fought by a stale pin.
//   5. BattleArena.VerifyReturnPose's drift net SURVIVES (it is the net that
//      caught this defect from the other side).
//
// Source-lint (edit-mode, no PlayMode), comment-stripped so prose can never satisfy
// a check. Never throws.
// Markers: HERO_DEATHPIN_OK / HERO_DEATHPIN_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HeroDeathPinRebaseRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class HeroDeathPinRebaseRegression
    {
        private const string HealthSrc = "Assets/_Modules/Village/Hero/HeroHealth.cs";
        private const string LocoSrc   = "Assets/_Modules/Village/Hero/HeroLocomotion.cs";
        private const string ArenaSrc  = "Assets/_Modules/Village/Arena/BattleArena.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_DEATHPIN_OK - " + reason);
            else Debug.LogError("HERO_DEATHPIN_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var fails = new List<string>();
            try
            {
                string health = ReadSource(HealthSrc, fails);
                string loco   = ReadSource(LocoSrc, fails);
                string arena  = ReadSource(ArenaSrc, fails);

                // ---- (1) the rebase seam exists, guards, and rewrites BOTH pose fields ----
                if (health != null)
                {
                    string code = StripComments(health);
                    var m = Regex.Match(code,
                        @"public\s+void\s+RebaseDeathPin\s*\(\s*Vector3\s+\w+\s*,\s*Quaternion\s+\w+\s*,\s*string\s+\w+\s*\)\s*\{(?<body>.*?)\n        \}",
                        RegexOptions.Singleline);
                    if (!m.Success)
                        fails.Add("HeroHealth no longer defines public RebaseDeathPin(Vector3, Quaternion, string) - " +
                                  "a sanctioned warp has no way to rebase the death pin, so the pin fights every " +
                                  "legitimate teleport of a dead hero (the 2026-08-10 shake verbatim)");
                    else
                    {
                        string body = m.Groups["body"].Value;
                        if (!Regex.IsMatch(body, @"if\s*\(\s*!\s*_deathPinActive\s*\)\s*return\s*;"))
                            fails.Add("RebaseDeathPin does not early-return while no pin is armed - every living-hero " +
                                      "warp would write stale pin state");
                        if (!Regex.IsMatch(body, @"_deathPinPos\s*="))
                            fails.Add("RebaseDeathPin does not rewrite _deathPinPos - the pin position never follows " +
                                      "the sanctioned warp");
                        if (!Regex.IsMatch(body, @"_deathPinRot\s*="))
                            fails.Add("RebaseDeathPin does not rewrite _deathPinRot - the pin would keep fighting the " +
                                      "warp's facing (the captured dYaw=31.27deg half of the shake)");
                    }

                    // ---- (3) the LateUpdate watchdog net SURVIVES (permanent instrumentation) ----
                    if (!Regex.IsMatch(code, @"transform\.position\s*=\s*_deathPinPos"))
                        fails.Add("HeroHealth.LateUpdate no longer re-asserts _deathPinPos - the death pin net was " +
                                  "deleted; an unsanctioned mover can shake a dead hero again with ZERO evidence " +
                                  "(never strip instrumentation, owner ruling 2026-08-09)");
                    if (!code.Contains("RESIDUAL move fought the death pin"))
                        fails.Add("the residual-mover FAIL line is gone from HeroHealth - the net that NAMED this " +
                                  "defect's writer no longer names the next one");

                    // ---- (4) the release path survives (respawn-moves-you stays un-fought) ----
                    var release = Regex.Match(code,
                        @"private\s+void\s+ExitDeathFreeze\s*\(\s*\)\s*\{(?<body>.*?)\n        \}",
                        RegexOptions.Singleline);
                    if (!release.Success || !Regex.IsMatch(release.Groups["body"].Value, @"_deathPinActive\s*=\s*false"))
                        fails.Add("ExitDeathFreeze no longer releases the death pin (_deathPinActive = false) - a " +
                                  "revived hero would be pinned to its corpse and the respawn-moves-you flow " +
                                  "(canon 2026-08-02) is fought by the stale pin");
                }

                // ---- (2) the hook at the ONE sanctioned-warp authority ----
                if (loco != null)
                {
                    string code = StripComments(loco);
                    var warp = Regex.Match(code,
                        @"public\s+void\s+WarpTo\s*\([^)]*\)\s*\{(?<body>.*?)\n        \}",
                        RegexOptions.Singleline);
                    if (!warp.Success)
                        fails.Add("HeroLocomotion.WarpTo not found - the sanctioned teleport authority moved without " +
                                  "re-pointing this oracle (and possibly without carrying the death-pin rebase hook)");
                    else if (!warp.Groups["body"].Value.Contains("RebaseDeathPin("))
                        fails.Add("HeroLocomotion.WarpTo does not call RebaseDeathPin - the seam exists but nothing " +
                                  "drives it, so the arena return warp of a dead hero is fought by the pin again " +
                                  "(F8 seq 2254: dPos=7096.803m re-pinned)");
                }

                // ---- (5) the arena-side drift net SURVIVES ----
                if (arena != null && !StripComments(arena).Contains("RETURN POSE DRIFT"))
                    fails.Add("BattleArena.VerifyReturnPose's RETURN POSE DRIFT net is gone - the arena-side watchdog " +
                              "that caught this defect (F8 seq 2255) was deleted; fix writers, never nets");
            }
            catch (Exception ex)
            {
                fails.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (fails.Count == 0)
            {
                reason = "HERO DEATH-PIN OK - RebaseDeathPin seam present (guarded, rewrites pos+rot), " +
                         "HeroLocomotion.WarpTo drives it, the LateUpdate pin net + residual FAIL line survive, " +
                         "ExitDeathFreeze releases the pin, and the arena's RETURN POSE DRIFT net survives - " +
                         "exactly one system decides where a dead hero rests across a sanctioned warp";
                return true;
            }
            reason = "hero-death-pin FAIL x" + fails.Count + ": " + string.Join(" | ", fails.ToArray());
            return false;
        }

        private static string ReadSource(string path, List<string> fails)
        {
            if (!File.Exists(path))
            {
                fails.Add("[source] " + path + " not found - the file moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                fails.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and block comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
