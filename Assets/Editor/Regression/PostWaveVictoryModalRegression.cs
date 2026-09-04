using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class PostWaveVictoryModalRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string vm = File.ReadAllText("Assets/_Modules/Village/UI/EndState/EndStateVM.cs");
            string view = File.ReadAllText("Assets/_Modules/Village/UI/EndState/EndStateView.cs");
            string celebration = File.ReadAllText("Assets/_Modules/Village/Waves/WaveCelebrationManager.cs");
            string wave = File.ReadAllText("Assets/_Modules/Village/Waves/WaveManager.cs");

            Need(vm, "Compact = false", "wave result is not the full Obsidian modal", failures);
            Need(vm, "Prepare for Wave {waveNumber + 1}", "next action is missing", failures);
            Need(vm, "HoldWorld = true", "wave result does not hold the countdown", failures);
            Need(view, "WorldHold.AcquirePlayerOwned(\"wave-results\")", "shared hold is not acquired", failures);
            Need(view, "_worldHold?.Dispose()", "shared hold is not released", failures);
            Need(vm, "TryGetWaveUnlockFor", "authoritative unlock is not reported", failures);
            Need(wave, "AwardWaveClearUnlocks(cleared)", "unlock is not persisted before presentation", failures);
            Need(celebration, "Significance01", "celebration significance curve is missing", failures);
            if (!(DeNelle.Village.WaveCelebrationManager.Significance01(1) <
                  DeNelle.Village.WaveCelebrationManager.Significance01(7)))
                failures.Add("Wave 7 must be materially stronger than Wave 1");
            if (vm.Contains("Wave {waveNumber} Cleared...")) failures.Add("player copy contains ellipsis");

            reason = failures.Count == 0
                ? "POST_WAVE_VICTORY_MODAL_OK - framed, held, authoritative, significance-scaled, no ellipsis"
                : string.Join(" | ", failures);
            return failures.Count == 0;
        }

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log(reason); else Debug.LogError(reason);
        }

        private static void Need(string src, string needle, string message, List<string> failures)
        {
            if (src.IndexOf(needle, StringComparison.Ordinal) < 0) failures.Add(message);
        }
    }
}
