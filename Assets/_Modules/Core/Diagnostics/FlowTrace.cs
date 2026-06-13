using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// Central root-cause trace layer. Owner directive 2026-06-13: STOP guessing at
    /// bugs — instrument the actual flow so one F8 playtest run shows exactly which
    /// step a system reached and where it stopped. The F8 BreakCaptureHarness captures
    /// all Debug.Log output to break-log.jsonl, so every FlowTrace line lands there.
    ///
    /// USAGE (one call per meaningful step in a flow):
    ///   FlowTrace.Step("Seam", "south gate fired Cross()");
    ///   FlowTrace.Warn("Enemy", "no model for family 'orc' — falling back to default");
    ///   FlowTrace.Throttle("Seam", "south-dist", 1f, $"heroDist={d:F1}m");   // ~1/sec
    ///   FlowTrace.Once("Roster", "spawned-sylas", "Sylas spawned");           // first time only
    ///
    /// Every line is prefixed [Flow:<system>] so logs are greppable per-system.
    /// Set Enabled=false (or strip the calls) once a system is proven stable.
    /// </summary>
    public static class FlowTrace
    {
        /// <summary>Master switch. Leave on while we are stabilising the loop.</summary>
        public static bool Enabled = true;

        /// <summary>Log a flow step you reached. Cheap; safe to leave in hot paths only via Throttle.</summary>
        public static void Step(string system, string message)
        {
            if (Enabled) Debug.Log($"[Flow:{system}] {message}");
        }

        /// <summary>Log a flow anomaly (missing ref, fallback taken, unexpected branch).</summary>
        public static void Warn(string system, string message)
        {
            if (Enabled) Debug.LogWarning($"[Flow:{system}] {message}");
        }

        /// <summary>Log an error-level flow failure (exception caught, hard stop).</summary>
        public static void Fail(string system, string message)
        {
            if (Enabled) Debug.LogError($"[Flow:{system}] {message}");
        }

        // --- throttled logging (for per-frame / per-spawn hot paths) ---
        private static readonly Dictionary<string, float> s_nextAt = new Dictionary<string, float>();

        /// <summary>
        /// Log at most once per <paramref name="everySeconds"/> for the given key. Use in Update()
        /// or per-mob loops so the log shows the trend (e.g. closest-approach distance) without spamming.
        /// </summary>
        public static void Throttle(string system, string key, float everySeconds, string message)
        {
            if (!Enabled) return;
            float now = Time.realtimeSinceStartup;
            string k = system + "/" + key;
            if (s_nextAt.TryGetValue(k, out float next) && now < next) return;
            s_nextAt[k] = now + everySeconds;
            Debug.Log($"[Flow:{system}] {message}");
        }

        // --- once-only logging (for "did this run at all" checks) ---
        private static readonly HashSet<string> s_seen = new HashSet<string>();

        /// <summary>Log only the FIRST time this (system,key) is hit this play session.</summary>
        public static void Once(string system, string key, string message)
        {
            if (!Enabled) return;
            string k = system + "/" + key;
            if (!s_seen.Add(k)) return;
            Debug.Log($"[Flow:{system}] {message}");
        }

        /// <summary>Reset once/throttle state — call on scene reload if you want fresh first-hit logs.</summary>
        public static void ResetSession()
        {
            s_seen.Clear();
            s_nextAt.Clear();
        }
    }
}
