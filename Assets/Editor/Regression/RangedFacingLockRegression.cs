// =============================================================================
// RangedFacingLockRegression — pins WO-1105 R3/R4 (owner rulings 2026-08-16).
// -----------------------------------------------------------------------------
// regression-registry: registered by the committer (do NOT self-register here —
// DataRegression.cs is lane-fenced; the orchestrator adds the [ranged-facing] row).
//
// THREE OWNER RULINGS, ONE DAY, ALL ABOUT WHAT A RANGED CLASS DOES WITH A TARGET:
//
//   R3a — "can we add that when a ranger is targeting a enemy they lock facing
//         the enemy". An archer who shoots sideways reads as broken.
//   R3b — THE SHOT ROOTS THE SHOOTER. Facing lock is unconditional, but firing
//         is not free while moving: moving CANCELS the shot (she loses the shot,
//         not merely the animation), and a cancelled shot must SAY SO.
//   R4  — "can a person or enemy or hero move out of range during a bow shot or
//         mage attack? Should be able to." Range is evaluated at RESOLUTION.
//
// WHY THESE NEED PINNING RATHER THAN TRUSTING THE CODE TO STAY PUT:
//
// Every one of the three is a WIRING property, not an algorithm — and wiring is
// exactly what a later refactor drops without noticing, because nothing stops
// compiling. The facing slew (HeroLocomotion.SetLockFace / ApplyLockFaceYaw) has
// existed since WO-512 slice 3 and STILL never reached the ranger for two full
// releases, for two reasons this suite now makes loud:
//   (1) it was driven only from the MANUAL lock paths, so an AUTO-acquired target
//       — the ranger's normal case — never turned the body; and
//   (2) it was gated on FeatureFlags.LockOn, which is DEFAULT OFF.
// A feature that is "implemented" and unreachable is indistinguishable from one
// that was never written. Case 1 and Case 2 are the difference.
//
// SOURCE-LINT DIRECTION (see RegressionSourceText's header). Cases 1/2/3/5 are
// REQUIRED-pattern checks whose needles are C# identifiers, never quoted
// literals, so they read the FULLY stripped text: that way this file's own prose
// and the target files' large explanatory comment blocks — which necessarily NAME
// everything being asserted — can never satisfy an assertion. A hollow pass here
// would be worse than no suite at all.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RangedFacingLockRegression
    {
        private const string IndicatorPath  = "Assets/_Modules/Village/Hero/HeroTargetIndicator.cs";
        private const string LocomotionPath = "Assets/_Modules/Village/Hero/HeroLocomotion.cs";
        private const string AbilitiesPath  = "Assets/_Modules/Village/Hero/HeroAbilities.cs";
        private const string AbilitiesJson  = "Assets/Resources/Data/Canonical/abilities.json";
        private const string StreamingJson  = "Assets/StreamingAssets/Data/Canonical/abilities.json";

        /// <summary>Standalone batch entry — prints the RANGED_FACING_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RANGED_FACING_OK - " + reason);
            else Debug.LogError("RANGED_FACING_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([ranged-facing]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "facing-driven-by-target", () => Case1_FacingIsDrivenByCurrentTarget(failures, notes));
            Case(failures, "facing-releases",         () => Case2_FacingReleasesAndIsForced(failures, notes));
            Case(failures, "melee-unaffected",        () => Case3_MeleeClassesUnaffected(failures, notes));
            Case(failures, "planted-shot",            () => Case4_PlantedShotIsData(failures, notes));
            Case(failures, "cancel-is-legible",       () => Case5_CancelledShotSaysSo(failures, notes));
            Case(failures, "escape-at-resolution",    () => Case6_RangeCheckedAtResolution(failures, notes));

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures);
                return false;
            }
            reason = "6/6 cases pass — " + string.Join("; ", notes);
            return true;
        }

        // ---------------------------------------------------------------------
        // Case 1 — a ranged class's facing is DRIVEN by the acquired target, and
        //          the driver is keyed off the DERIVED predicate, not a class name.
        //
        //          The three needles together are the whole wiring claim:
        //            * DriveRangedFacing is CALLED from LateUpdate (not merely
        //              defined — an uncalled private method is the exact "shipped
        //              but unreachable" failure this suite exists for);
        //            * it resolves the class through TryGetRangedPrimary; and
        //            * it feeds SetLockFace, i.e. the ONE existing facing
        //              authority, rather than writing transform.rotation itself.
        // ---------------------------------------------------------------------
        private static void Case1_FacingIsDrivenByCurrentTarget(List<string> failures, List<string> notes)
        {
            string src = StrippedSource(IndicatorPath, failures, "facing-driven-by-target");
            if (src == null) return;

            // Counted, not merely present: ONE occurrence is the definition alone, i.e. a driver that
            // exists and never runs — precisely the "shipped but unreachable" shape this suite exists
            // to catch. A real wiring needs the definition PLUS at least one call site.
            if (Occurrences(Squeeze(src), Squeeze("DriveRangedFacing()")) < 2)
                failures.Add("[facing-driven-by-target] DriveRangedFacing appears fewer than twice in "
                    + "HeroTargetIndicator — it is defined but never CALLED. A defined-but-uncalled driver is "
                    + "indistinguishable from a feature that was never written (WO-512's lock-face sat "
                    + "unreachable for two releases for exactly this reason).");
            Require(src, "TryGetRangedPrimary", failures, "facing-driven-by-target",
                "the ranged test must be the DERIVED TryGetRangedPrimary seam, never a hardcoded class name");
            Require(src, "SetLockFace", failures, "facing-driven-by-target",
                "facing must route through the EXISTING HeroLocomotion lock-face authority, not a second writer");
            Require(src, "CurrentTarget", failures, "facing-driven-by-target",
                "the drive must read CurrentTarget (which is `_locked ?? NearestCandidate()`), so an "
                + "AUTO-acquired target turns the body just like a tap-locked one");

            // BANNED: a class-name shortcut anywhere in the facing path. Strings are stripped, so a
            // comment or a log line mentioning the ranger cannot trip this — only real code can.
            foreach (string banned in new[] { "HeroClass==\"ranger\"", "HeroClass==\"mage\"", "_heroClass==\"ranger\"" })
            {
                if (Squeeze(src).Contains(Squeeze(banned)))
                    failures.Add("[facing-driven-by-target] HeroTargetIndicator contains a hardcoded class-name "
                        + "check (" + banned + "). WO-1105 section 3c: the predicate is DERIVED, never a per-class table.");
            }

            notes.Add("facing driven off CurrentTarget via TryGetRangedPrimary -> SetLockFace");
        }

        // ---------------------------------------------------------------------
        // Case 2 — the lock RELEASES, and it is not hostage to a default-OFF flag.
        //
        //          Release: DriveRangedFacing must call ClearLockFace. Because it
        //          re-evaluates every LateUpdate off CurrentTarget, the four
        //          release conditions the owner named (target dies / leaves range
        //          / is cleared / hero dies) collapse into "CurrentTarget went
        //          null" — but the clear call still has to EXIST.
        //
        //          Reachability: HeroLocomotion's lockFacing gate must admit a
        //          path that does NOT require FeatureFlags.LockOn. If someone
        //          later re-tightens that gate to the flag alone, the ranger
        //          silently stops facing again and nothing else would catch it.
        // ---------------------------------------------------------------------
        private static void Case2_FacingReleasesAndIsForced(List<string> failures, List<string> notes)
        {
            string ind = StrippedSource(IndicatorPath, failures, "facing-releases");
            if (ind != null)
                Require(ind, "ClearLockFace", failures, "facing-releases",
                    "the ranged facing drive must RELEASE the lock-face when the target is gone");

            string loco = StrippedSource(LocomotionPath, failures, "facing-releases");
            if (loco == null) return;

            string sq = Squeeze(loco);

            Require(loco, "_lockFaceForce", failures, "facing-releases",
                "HeroLocomotion needs the force gate so archer facing does not wait on the default-OFF "
                + "FeatureFlags.LockOn camera experiment");

            if (!sq.Contains(Squeeze("_lockFaceForce||DeNelle.Core.FeatureFlags.LockOn"))
                && !sq.Contains(Squeeze("DeNelle.Core.FeatureFlags.LockOn||_lockFaceForce"))
                && !sq.Contains(Squeeze("_lockFaceForce||FeatureFlags.LockOn"))
                && !sq.Contains(Squeeze("FeatureFlags.LockOn||_lockFaceForce")))
            {
                failures.Add("[facing-releases] HeroLocomotion's lockFacing gate no longer ORs _lockFaceForce with "
                    + "FeatureFlags.LockOn. FeatureFlags.LockOn defaults OFF, so requiring it alone makes the "
                    + "ranger's facing lock unreachable in a normal session — the exact defect WO-1105 R3 fixed.");
            }

            // The force flag must be RESET on clear, or a released lock would keep overriding the flag
            // for the next target the manual path engages.
            int clearAt = loco.IndexOf("public void ClearLockFace", StringComparison.Ordinal);
            if (clearAt < 0)
                failures.Add("[facing-releases] HeroLocomotion.ClearLockFace is gone — HeroHealth's death freeze "
                    + "calls it to stop a downed hero re-facing a target.");
            else
            {
                string body = loco.Substring(clearAt, Math.Min(600, loco.Length - clearAt));
                if (!Squeeze(body).Contains(Squeeze("_lockFaceForce=false")))
                    failures.Add("[facing-releases] ClearLockFace does not reset _lockFaceForce — a stale force flag "
                        + "would keep overriding FeatureFlags.LockOn after the lock was released.");
            }

            notes.Add("release + force-gate pinned; ClearLockFace intact for the death freeze");
        }

        // ---------------------------------------------------------------------
        // Case 3 — MELEE CLASSES ARE UNAFFECTED, proven from the DATA rather than
        //          from the code's good intentions. The knight's basic must still
        //          fail the ranged discriminator, so IsRangedClass returns false
        //          for him and DriveRangedFacing returns before touching facing.
        //          If someone ever re-authors knight.q into a long-range 'strike',
        //          the knight silently inherits archer facing — this catches it.
        // ---------------------------------------------------------------------
        private static void Case3_MeleeClassesUnaffected(List<string> failures, List<string> notes)
        {
            JObject root = ReadJson(AbilitiesJson, failures, "melee-unaffected");
            if (root == null) return;

            var q = root.SelectToken("classes.knight.abilities.q") as JObject;
            if (q == null)
            {
                failures.Add("[melee-unaffected] classes.knight.abilities.q missing from " + AbilitiesJson);
                return;
            }

            string effect = (string)q["effect"] ?? string.Empty;
            if (effect.Trim().ToLowerInvariant() == "strike" || effect.Trim().ToLowerInvariant() == "drainshot")
            {
                failures.Add("[melee-unaffected] knight.q effect is now '" + effect + "', which is a PROJECTILE shape. "
                    + "TryGetRangedPrimary would accept the knight, so he would inherit the archer facing lock AND "
                    + "the planted-shot root. Melee classes must keep their current facing behaviour.");
            }

            // The indicator must actively bail for a non-ranged class rather than falling through.
            string src = StrippedSource(IndicatorPath, failures, "melee-unaffected");
            if (src != null)
            {
                Require(src, "IsRangedClass()", failures, "melee-unaffected",
                    "the drive must gate on IsRangedClass so melee facing is never touched");
            }

            notes.Add("knight.q effect='" + effect + "' fails the ranged discriminator (melee facing untouched)");
        }

        // ---------------------------------------------------------------------
        // Case 4 — THE SHOT ROOTS THE SHOOTER, and the root window is DATA.
        //
        //          castSeconds > 0 on the bow is the entire mechanism: HeroAbilities
        //          only routes a cast through the interruptible CastRoutine (the one
        //          that polls HeroLocomotion.WantsToMove and cancels) when
        //          def.CastSeconds > 0. At 0 the bow fires instantly and free
        //          move-and-shoot returns, which the owner ruled out because it
        //          trivialises melee threat. The value itself is HERS to tune —
        //          this pins that it EXISTS and is non-zero, never what it is.
        //
        //          Both catalog copies are checked: Resources wins at runtime, but a
        //          drifted StreamingAssets copy is how this setting comes back to
        //          bite on device.
        // ---------------------------------------------------------------------
        private static void Case4_PlantedShotIsData(List<string> failures, List<string> notes)
        {
            float seen = -1f;
            foreach (string path in new[] { AbilitiesJson, StreamingJson })
            {
                JObject root = ReadJson(path, failures, "planted-shot");
                if (root == null) continue;

                var tok = root.SelectToken("classes.ranger.abilities.q.castSeconds");
                if (tok == null)
                {
                    failures.Add("[planted-shot] classes.ranger.abilities.q.castSeconds is ABSENT in " + path
                        + ". Without it HeroAbilities.TryCast takes the INSTANT branch, the bow never roots, "
                        + "and moving cannot cancel the shot (owner ruling 2026-08-16).");
                    continue;
                }

                float v = (float)tok;
                if (v <= 0f)
                {
                    failures.Add("[planted-shot] ranger.q castSeconds is " + v.ToString("0.###") + " in " + path
                        + " — must be > 0 so the shot roots the shooter and moving cancels it.");
                    continue;
                }
                if (seen >= 0f && Math.Abs(seen - v) > 0.0001f)
                    failures.Add("[planted-shot] the two abilities.json copies DISAGREE on ranger.q castSeconds ("
                        + seen.ToString("0.###") + " vs " + v.ToString("0.###") + ") — Resources wins at runtime, "
                        + "so a drifted StreamingAssets copy is a device-only surprise.");
                seen = v;
            }

            if (seen > 0f) notes.Add("planted-shot window = " + seen.ToString("0.###") + "s (owner-tunable data)");
        }

        // ---------------------------------------------------------------------
        // Case 5 — A CANCELLED SHOT SAYS SO. No silent refusal (the owner hit the
        //          invisible-refusal pattern three separate times on 2026-08-16;
        //          losing a shot to your own movement is a RULE, and a player can
        //          only learn a rule she is told). The wind-up VFX disappearing is
        //          not a message. Pinned as: the move-cancel branch of CastRoutine
        //          reaches a player-visible toast, not only a FlowTrace line.
        // ---------------------------------------------------------------------
        private static void Case5_CancelledShotSaysSo(List<string> failures, List<string> notes)
        {
            string raw = ReadText(AbilitiesPath);
            if (raw == null)
            {
                failures.Add("[cancel-is-legible] cannot read " + AbilitiesPath);
                return;
            }
            string src = RegressionSourceText.StripCommentsAndStrings(raw);

            int at = src.IndexOf("CastRoutine", StringComparison.Ordinal);
            // Find the ROUTINE body (the last occurrence is the definition + body, not the two call sites).
            int def = src.LastIndexOf("IEnumerator CastRoutine", StringComparison.Ordinal);
            if (def < 0) def = at;
            if (def < 0)
            {
                failures.Add("[cancel-is-legible] HeroAbilities.CastRoutine is gone — that coroutine IS the "
                    + "root-and-cancel mechanism (it polls HeroLocomotion.WantsToMove).");
                return;
            }

            string body = src.Substring(def, Math.Min(4000, src.Length - def));

            if (!body.Contains("WantsToMove"))
                failures.Add("[cancel-is-legible] CastRoutine no longer polls HeroLocomotion.WantsToMove — "
                    + "moving must CANCEL the shot (owner: she loses the shot, not merely the animation).");

            if (!body.Contains("ShowToast"))
                failures.Add("[cancel-is-legible] the move-cancel branch of CastRoutine does not raise a "
                    + "player-visible toast. A FlowTrace line is evidence for US, not feedback for HER — "
                    + "owner ruling 2026-08-16: a refused or cancelled shot must SAY SO.");

            notes.Add("move-cancel is legible (WantsToMove poll + toast)");
        }

        // ---------------------------------------------------------------------
        // Case 6 — RANGE IS RE-EVALUATED AT RESOLUTION, not only at initiation.
        //
        //          The hero's ranged abilities fly a REAL projectile (ProjectileMover
        //          lerps to a world point captured at fire) and land damage in an
        //          arrival closure. That closure used to gate on IsAlive alone, so it
        //          held a live reference and connected wherever the foe had run to —
        //          "walk out of a shot" was impossible BY CONSTRUCTION, and the
        //          visual (landing at the stale point) already disagreed with the
        //          outcome. The escape check must sit INSIDE the arrival closure;
        //          a range test at fire time does not satisfy this ruling.
        // ---------------------------------------------------------------------
        private static void Case6_RangeCheckedAtResolution(List<string> failures, List<string> notes)
        {
            string src = StrippedSource(AbilitiesPath, failures, "escape-at-resolution");
            if (src == null) return;

            Require(src, "ShotEscapeRangeGrace", failures, "escape-at-resolution",
                "the resolution-time escape radius must be a named, documented factor applied to the measured "
                + "reach — never a metre literal (the WO-1035 units bug)");

            int launch = src.IndexOf("LaunchProjectile(foe.WorldPosition", StringComparison.Ordinal);
            if (launch < 0)
            {
                failures.Add("[escape-at-resolution] could not locate the strike arrival closure "
                    + "(LaunchProjectile(foe.WorldPosition ...) in HeroAbilities) — the resolution gate's home.");
                return;
            }
            string closure = src.Substring(launch, Math.Min(2500, src.Length - launch));

            if (!closure.Contains("InReach"))
                failures.Add("[escape-at-resolution] the projectile ARRIVAL closure does not re-test InReach. "
                    + "Without it the shot connects on the surviving reference at any distance, so a target "
                    + "cannot move out of a bow shot or a mage attack (owner ruling 2026-08-16).");

            notes.Add("arrival closure re-tests range with a named grace factor");
        }

        // ---------------------------------------------------------------------
        //  helpers
        // ---------------------------------------------------------------------

        /// <summary>Fully stripped source (comments AND string bodies blanked), or null on read failure.</summary>
        private static string StrippedSource(string path, List<string> failures, string label)
        {
            string raw = ReadText(path);
            if (raw == null)
            {
                failures.Add("[" + label + "] cannot read " + path);
                return null;
            }
            return RegressionSourceText.StripCommentsAndStrings(raw);
        }

        /// <summary>Whitespace-insensitive containment — C# formatting must not decide a pass.</summary>
        private static void Require(string src, string needle, List<string> failures, string label, string why)
        {
            if (!Squeeze(src).Contains(Squeeze(needle)))
                failures.Add("[" + label + "] missing '" + needle + "' — " + why);
        }

        /// <summary>Non-overlapping occurrence count of <paramref name="needle"/> in <paramref name="hay"/>.</summary>
        private static int Occurrences(string hay, string needle)
        {
            if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = hay.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static string Squeeze(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if (!char.IsWhiteSpace(c)) sb.Append(c);
            return sb.ToString();
        }

        private static JObject ReadJson(string path, List<string> failures, string label)
        {
            string json = ReadText(path);
            if (json == null)
            {
                failures.Add("[" + label + "] cannot read " + path);
                return null;
            }
            try { return JObject.Parse(json); }
            catch (Exception ex)
            {
                failures.Add("[" + label + "] " + path + " does not parse: " + ex.Message);
                return null;
            }
        }

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        private static void Case(List<string> failures, string label, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + label + "] threw: " + ex.Message); }
        }
    }
}
