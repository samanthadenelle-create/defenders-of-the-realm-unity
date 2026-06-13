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

        // --- per-category gating (INSTRUMENTATION_STANDARD §1.2) -----------------
        // The category is the existing first arg ("system"), so call sites never change.
        // s_only == null  => all categories allowed (default; no shipped-behaviour change).
        // s_only != null  => allow-list: only these categories log.
        // s_muted         => deny-set: these never log (applied after the allow-list).
        private static System.Collections.Generic.HashSet<string> s_only;
        private static readonly System.Collections.Generic.HashSet<string> s_muted = new System.Collections.Generic.HashSet<string>();

        /// <summary>Allow-list: log ONLY these systems (mutes all others). Pass none to clear.</summary>
        public static void Only(params string[] systems)
        {
            s_only = (systems == null || systems.Length == 0)
                ? null
                : new System.Collections.Generic.HashSet<string>(systems);
        }

        /// <summary>Deny-set: these systems never log (applied on top of any allow-list).</summary>
        public static void Mute(params string[] systems)
        {
            if (systems == null) return;
            foreach (var s in systems) if (!string.IsNullOrEmpty(s)) s_muted.Add(s);
        }

        /// <summary>Clear all category filters — every system logs again (still gated by Enabled).</summary>
        public static void AllOn()
        {
            s_only = null;
            s_muted.Clear();
        }

        /// <summary>O(1) gate: master switch + category allow-list + mute-set.</summary>
        private static bool Allowed(string system)
        {
            if (!Enabled) return false;
            if (s_only != null && !s_only.Contains(system)) return false;
            if (s_muted.Count > 0 && s_muted.Contains(system)) return false;
            return true;
        }

        /// <summary>Log a flow step you reached. Cheap; safe to leave in hot paths only via Throttle.</summary>
        public static void Step(string system, string message)
        {
            if (Allowed(system)) Debug.Log($"[Flow:{system}] {message}");
        }

        /// <summary>Log a flow anomaly (missing ref, fallback taken, unexpected branch).</summary>
        public static void Warn(string system, string message)
        {
            if (Allowed(system)) Debug.LogWarning($"[Flow:{system}] {message}");
        }

        /// <summary>Log an error-level flow failure (exception caught, hard stop).</summary>
        public static void Fail(string system, string message)
        {
            if (Allowed(system)) Debug.LogError($"[Flow:{system}] {message}");
        }

        // --- throttled logging (for per-frame / per-spawn hot paths) ---
        private static readonly Dictionary<string, float> s_nextAt = new Dictionary<string, float>();

        /// <summary>
        /// Log at most once per <paramref name="everySeconds"/> for the given key. Use in Update()
        /// or per-mob loops so the log shows the trend (e.g. closest-approach distance) without spamming.
        /// </summary>
        public static void Throttle(string system, string key, float everySeconds, string message)
        {
            if (!Allowed(system)) return;
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
            if (!Allowed(system)) return;
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

        // --- performance timing -------------------------------------------------
        // Owner: "we log everything even performance depending on what part of the class
        // we call." A scoped stopwatch: wrap a block to log how long it took, and WARN when
        // it exceeds a budget (so a frame-hitch / slow load surfaces as a tagged log line,
        // not a guessed-at stutter). The [Flow:<system>] tag names the part being measured.
        //
        // USAGE:  using (FlowTrace.Measure("Store", "ShowBuy", warnAboveMs: 16f)) { ...work... }
        //   -> "[Flow:Store] ShowBuy took 7.4ms"   (or a Warn if it ran past 16ms = one frame)
        public static Scope Measure(string system, string what, float warnAboveMs = 0f)
        {
            return new Scope(system, what, warnAboveMs);
        }

        /// <summary>Disposable timing scope returned by <see cref="Measure"/>. Logs elapsed ms on dispose.</summary>
        public readonly struct Scope : System.IDisposable
        {
            private readonly string _system;
            private readonly string _what;
            private readonly float _warnAboveMs;
            private readonly float _startMs;
            private readonly bool _active;

            internal Scope(string system, string what, float warnAboveMs)
            {
                _system = system;
                _what = what;
                _warnAboveMs = warnAboveMs;
                _active = Enabled;
                _startMs = _active ? Time.realtimeSinceStartup * 1000f : 0f;
            }

            public void Dispose()
            {
                if (!_active) return;
                float ms = Time.realtimeSinceStartup * 1000f - _startMs;
                string msg = $"{_what} took {ms:F1}ms";
                if (_warnAboveMs > 0f && ms > _warnAboveMs)
                    Debug.LogWarning($"[Flow:{_system}] {msg} (over {_warnAboveMs:F0}ms budget)");
                else
                    Debug.Log($"[Flow:{_system}] {msg}");
            }
        }
    }
}
