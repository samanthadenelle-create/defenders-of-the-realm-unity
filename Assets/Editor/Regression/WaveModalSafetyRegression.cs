// WaveModalSafetyRegression [wave-modal-safety]
// Pins the device-proven contract: an active village siege cannot remain hidden
// behind an ordinary full-screen panel, while explicit Pause remains admissible
// and owns Time.timeScale until Resume restores the captured positive scale.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DeNelle.Core.Combat;
using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class WaveModalSafetyRegression
    {
        private const string WaveSrc = "Assets/_Modules/Village/Waves/WaveManager.cs";
        private const string PauseSrc = "Assets/_Modules/Settings/PauseController.cs";

        // WO-1149: Time.timeScale's single owner moved into Core so the money path could reach it.
        // The clock-lease invariants this suite pins are now read from these two files.
        private const string WorldHoldSrc         = "Assets/_Modules/Core/UI/WorldHold.cs";
        private const string WorldHoldWatchdogSrc = "Assets/_Modules/Core/UI/WorldHoldWatchdog.cs";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("WAVE_MODAL_SAFETY_OK - " + reason);
            else Debug.LogError("WAVE_MODAL_SAFETY_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                CheckWaveSource(failures);
                CheckPauseSource(failures);
                CheckArbiterMechanism(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "active village waves close ordinary modals before spawn and hold the " +
                         "battle admission gate; battle-allowed Pause remains admissible and " +
                         "reasserts its zero-scale lease until exact positive-scale restoration.";
                return true;
            }

            reason = "wave-modal-safety FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void CheckWaveSource(List<string> failures)
        {
            string src = StripComments(File.ReadAllText(WaveSrc));
            if (!src.Contains("BattleLock.RegisterProbe(_waveBattleProbe)"))
                failures.Add("[wave-lock] WaveManager does not register its cached active-wave probe.");
            if (Count(src, "BattleLock.UnregisterProbe(_waveBattleProbe)") < 2)
                failures.Add("[wave-lock] WaveManager must unregister its probe on disable and destroy.");
            if (!Regex.IsMatch(src,
                    @"_waveBattleProbe\s*=\s*\(\)\s*=>[\s\S]{0,180}?Instance\s*==\s*this[\s\S]{0,120}?_phase\s*==\s*WavePhase\.Active"))
                failures.Add("[wave-lock] the probe is not canonical-instance, enabled, Active-only combat.");

            // ⚠ REPOINTED 2026-09-02 (WO-1308), NOT WEAKENED. This used to look for the literal
            // `_phase = WavePhase.Active`. WO-1308 routed ALL NINE phase assignments through a
            // single recorder, `SetPhase(WavePhase, string site)`, so the last transition (from ->
            // to, site, unscaled time, frame) is always on the record when the battle-quiescence
            // gate asks why the battle-lock is still held - the owner's "the wolf is still here and
            // sitting in fight" (F8 seq 4663-4665) could not be diagnosed without it.
            //
            // The ORDERING PROPERTY this suite pins is untouched and is still asserted verbatim
            // below: ordinary modals must be closed AFTER the phase turns Active and BEFORE
            // OnWaveStarted/spawn work, so a siege can never damage the Heart behind a modal. Only
            // the address of "the phase turns Active" moved. An oracle left aimed at the old
            // spelling would have gone red against a correct tree and invited someone to "fix" it
            // by re-inlining the assignment - which would delete the transition record and put the
            // next stuck lock straight back to zero evidence.
            //
            // StartWave remains the ONLY writer of Active in WaveManager, so this still resolves to
            // exactly one site. Comments are stripped above, so the surrounding WO-1308 commentary
            // in WaveManager.cs cannot produce a false match.
            int phase = src.IndexOf("SetPhase(WavePhase.Active", StringComparison.Ordinal);
            int close = src.IndexOf("PanelManager.CloseAll()", phase >= 0 ? phase : 0, StringComparison.Ordinal);
            int eventAt = src.IndexOf("OnWaveStarted.Invoke", phase >= 0 ? phase : 0, StringComparison.Ordinal);
            if (phase < 0 || close < phase || eventAt < 0 || close > eventAt)
                failures.Add("[wave-close] ordinary modals are not closed after Active is set and before OnWaveStarted/spawn work.");
        }

        private static void CheckPauseSource(List<string> failures)
        {
            string src = StripComments(File.ReadAllText(PauseSrc));

            // ⚠ REPOINTED BY WO-1149 (2026-08-22), NOT WEAKENED. Every invariant below is the same
            // one this suite has always pinned; two of them changed ADDRESS because Time.timeScale
            // gained a single owner. PackStore.Purchase must freeze the world for a transaction (the
            // owner was killed mid purchase-test), and DeNelle.Wallet cannot reference
            // DeNelle.Settings — so the clock lease, its capture-and-restore rule and its late
            // reassertion moved down into DeNelle.Core.UI.WorldHold, with PauseController as a
            // client holding a named, reference-counted token.
            //
            // An oracle left aimed at the OLD address would have failed on a correct tree and, worse,
            // would have pushed somebody to "fix" it by restoring a SECOND writer of Time.timeScale —
            // which is the WO-1016 permanent-invisible-freeze shape. The behavioural half of these
            // rules is additionally measured live (not by regex) in TransactionWorldHoldRegression.
            string holdSrc      = StripComments(File.ReadAllText(WorldHoldSrc));
            string watchdogSrc  = StripComments(File.ReadAllText(WorldHoldWatchdogSrc));

            if (!Regex.IsMatch(watchdogSrc, @"DefaultExecutionOrder\s*\(\s*32000\s*\)"))
                failures.Add("[pause-owner] the WorldHold watchdog is not ordered late enough to defend the clock lease.");
            if (!Regex.IsMatch(watchdogSrc, @"void\s+LateUpdate\s*\(\s*\)[\s\S]{0,200}?ReassertTick"))
                failures.Add("[pause-owner] no LATE reassertion tick exists; a frozen UI can outlive its zero timeScale.");
            // ⚠ REPOINTED 2026-09-03 (WO-1353), NOT WEAKENED - THE SECOND RE-POINT OF THIS CASE.
            // This used to require the LITERAL `Time.timeScale = 0f` inside ReassertTick's body.
            // WO-1353 made WorldHold the owner of the whole world clock rather than only of
            // freezes: cosmetic slow-motion (hit stop, kill slow-mo, wave-clear dip, death ramp,
            // arena death cam) now takes a HOLD at its own scale instead of writing the global, and
            // the composed scale is the MINIMUM across live holds ("slowest wins"). So the reassert
            // no longer writes a hardcoded 0 - it writes EffectiveScale through the single writer,
            // ApplyEffective().
            //
            // ⛔ AND THE OLD SPELLING IS NOW ACTIVELY WRONG, WHICH IS WHY IT MUST NOT BE RESTORED.
            // With cosmetic dips held rather than stamped, a literal `Time.timeScale = 0f` in
            // ReassertTick would FREEZE THE GAME OUTRIGHT any time only a wave-clear dip was live -
            // a hard softlock in place of a 0.9 s celebration. The property to pin is "a stolen
            // clock is restored to what the live holds require", and for a pause hold that value is
            // provably 0: Acquire() delegates to AcquireScale(reason, 0f, ...), scales are clamped
            // with Mathf.Max(0f, scale), and EffectiveScale takes the MINIMUM - so a live pause hold
            // pins the composed scale to exactly 0 no matter what else is held.
            //
            // The four links of that chain are pinned separately below, so a break names its link.
            if (!Regex.IsMatch(holdSrc,
                    @"ReassertTick\s*\(\s*\)[\s\S]{0,800}?ApplyEffective\s*\(\s*\)"))
                failures.Add("[pause-owner] WorldHold.ReassertTick no longer re-asserts the clock. A " +
                             "stolen lease is never taken back, so a frozen UI outlives its freeze " +
                             "and the player sees a Paused screen over running gameplay.");
            if (!Regex.IsMatch(holdSrc,
                    @"ApplyEffective\s*\(\s*\)[\s\S]{0,400}?Time\.timeScale\s*=\s*want\s*;"))
                failures.Add("[pause-owner] ApplyEffective is no longer the single writer of " +
                             "Time.timeScale. Every reassert and every release routes through it; if " +
                             "it stops writing the clock, nothing does.");
            if (!Regex.IsMatch(holdSrc, @"if\s*\(\s*s_holds\[i\]\.Scale\s*<\s*min\s*\)"))
                failures.Add("[pause-owner] EffectiveScale no longer composes holds by MINIMUM. " +
                             "Slowest-wins is what makes a pause hold outrank a cosmetic dip; under " +
                             "last-wins a hit stop starting mid-pause would thaw the world under a " +
                             "Paused screen - the WO-1016 shape by another road.");
            if (!Regex.IsMatch(holdSrc,
                    @"Handle\s+Acquire\s*\(\s*string\s+reason\s*\)[\s\S]{0,300}?AcquireScale\s*\(\s*reason\s*,\s*0f"))
                failures.Add("[pause-owner] a pause/transaction hold is no longer acquired at scale 0, " +
                             "so the minimum across live holds is not pinned to 0 and a 'freeze' may " +
                             "leave the world running.");
            // ⚠ REPOINTED 2026-09-02 (owner flag 4656, the 0.28 clock leak), NOT WEAKENED.
            // This used to require the literal `Time.timeScale = s_scaleBeforeHold > 0f ? ... : 1f`
            // as ONE adjacent expression. The 0.28 fix hoisted the guard into a local `restore` and
            // applies it a few lines later, so the regex went red against a tree whose safety
            // property was fully intact - and the "fix" it invited was to inline the assignment
            // again and drop the new leak guard. The PROPERTY, not the spelling, is what matters:
            // the restored value must be provably positive on every branch.
            if (!Regex.IsMatch(holdSrc,
                    @"s_scaleBeforeHold\s*>\s*0f\s*\?\s*s_scaleBeforeHold\s*:\s*1f"))
                failures.Add("[pause-restore] the release no longer restores the captured positive scale safely. " +
                             "Some branch of the release can now write a non-positive timeScale, which is the " +
                             "WO-1016 permanent-invisible-freeze shape.");
            // ⚠ REPOINTED 2026-09-03 (WO-1353), NOT WEAKENED - AND THIS FORM IS STRICTLY STRONGER.
            // This used to require the literal `Time.timeScale = restore;`, i.e. that ONE named
            // local fed the clock. WO-1353 moved the guard into RestorableBaseline(), which is now
            // the ONLY producer of a zero-hold scale and whose FIRST statement is the positive
            // guard; the release path reaches the clock through ApplyEffective() instead.
            //
            // The old pin checked ONE of the writes. This checks ALL of them: every assignment to
            // Time.timeScale in the owner must take its value from the guarded baseline, from the
            // composed EffectiveScale, or from the literal 1f. That is the spelling-independent
            // form of "the guard cannot be computed and then bypassed" - a new unguarded write
            // anywhere in the file fails this, which the old single-line regex could not catch.
            var writes = Regex.Matches(holdSrc, @"Time\.timeScale\s*=\s*([^;]+);");
            var unguarded = new List<string>();
            foreach (Match m in writes)
            {
                string rhs = m.Groups[1].Value.Trim();
                // want     <- ApplyEffective / WatchdogTick, = EffectiveScale (guarded when 0 holds)
                // baseline <- RestoreIfDrifted, = RestorableBaseline() (guarded)
                // 1f       <- literal, trivially positive
                if (rhs == "want" || rhs == "baseline" || rhs == "1f") continue;
                unguarded.Add(rhs);
            }
            if (unguarded.Count > 0)
                failures.Add("[pause-restore] " + unguarded.Count + " assignment(s) to Time.timeScale in " +
                             WorldHoldSrc + " take an UNGUARDED value (" + string.Join(", ", unguarded) +
                             "). Every write must come from the guarded RestorableBaseline (via " +
                             "'want'/'baseline') or the literal 1f, or the positive guard can be " +
                             "computed and then bypassed - the WO-1016 permanent-invisible-freeze shape.");
            if (!Regex.IsMatch(holdSrc,
                    @"s_holds\.Count\s*==\s*0\s*\)\s*return\s+RestorableBaseline\s*\(\s*\)"))
                failures.Add("[pause-restore] EffectiveScale no longer routes the ZERO-HOLD case through " +
                             "RestorableBaseline, so the positive guard is no longer the only producer " +
                             "of a restored scale and a release can write a non-positive clock.");

            // ADDED 2026-09-02: pin the leak fix itself. WaveCelebrationManager's SlowMoDip was an
            // untracked coroutine that Unity dropped on host deactivation, stranding timeScale at
            // 0.28; WorldHold then LAUNDERED that leaked positive into its restore value, so every
            // later hold re-applied the slow motion. The grace window is what stops a stale baseline
            // being trusted forever. Without this assertion the whole fix can be reverted silently.
            if (!holdSrc.Contains("SuspectBaselineGraceSeconds"))
                failures.Add("[pause-restore] the suspect-baseline grace window is GONE. WorldHold will again " +
                             "trust an arbitrarily old captured timeScale and re-apply a leaked slow-motion " +
                             "value forever (owner flag 4656, 2026-09-02).");
            if (!Regex.IsMatch(src, @"void\s+Resume\s*\(\s*\)[\s\S]{0,600}?_hold\s*\.\s*Dispose\s*\(\s*\)"))
                failures.Add("[pause-restore] PauseController.Resume no longer releases its WorldHold, so the " +
                             "pause menu can close over a still-frozen world.");
            if (!src.Contains("PanelManager.RegisterBattleAllowed(\"Pause\""))
                failures.Add("[pause-admission] explicit Pause is no longer battle-allowed.");
        }

        private static void CheckArbiterMechanism(List<string> failures)
        {
            PanelManager.CloseAll();
            bool lockHeld = false;
            Func<bool> probe = () => lockHeld;
            BattleLock.RegisterProbe(probe);
            try
            {
                bool ordinaryOpen = true;
                var ordinary = PanelManager.Register("Regression ordinary",
                    () => ordinaryOpen = false, () => ordinaryOpen);
                if (!PanelManager.NotifyOpened(ordinary))
                    failures.Add("[arbiter] ordinary panel was rejected while the test lock was clear.");

                lockHeld = true;
                PanelManager.CloseAll();
                if (ordinaryOpen || PanelManager.AnyOpen)
                    failures.Add("[arbiter] CloseAll did not dismiss the ordinary panel at combat start.");

                ordinaryOpen = true;
                if (PanelManager.NotifyOpened(ordinary) || ordinaryOpen || PanelManager.AnyOpen)
                    failures.Add("[arbiter] ordinary panel was admitted while combat lock was held.");

                bool pauseOpen = true;
                var pause = PanelManager.RegisterBattleAllowed("Regression pause",
                    () => pauseOpen = false, () => pauseOpen);
                if (!PanelManager.NotifyOpened(pause) || !pauseOpen || !PanelManager.AnyOpen)
                    failures.Add("[arbiter] battle-allowed Pause was rejected by the combat lock.");
            }
            finally
            {
                PanelManager.CloseAll();
                BattleLock.UnregisterProbe(probe);
            }
        }

        private static int Count(string text, string needle)
        {
            int count = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        private static string StripComments(string text)
        {
            text = Regex.Replace(text, @"/\*[\s\S]*?\*/", "");
            return Regex.Replace(text, @"//.*", "");
        }
    }
}
