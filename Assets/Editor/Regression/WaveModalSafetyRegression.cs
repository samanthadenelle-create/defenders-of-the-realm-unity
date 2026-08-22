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

            int phase = src.IndexOf("_phase = WavePhase.Active", StringComparison.Ordinal);
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
            if (!Regex.IsMatch(holdSrc,
                    @"ReassertTick\s*\(\s*\)[\s\S]{0,800}?Time\.timeScale\s*=\s*0f"))
                failures.Add("[pause-owner] WorldHold.ReassertTick no longer re-zeroes a stolen clock.");
            if (!Regex.IsMatch(holdSrc,
                    @"Time\.timeScale\s*=\s*s_scaleBeforeHold\s*>\s*0f\s*\?\s*s_scaleBeforeHold\s*:\s*1f"))
                failures.Add("[pause-restore] the release no longer restores the captured positive scale safely.");
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
