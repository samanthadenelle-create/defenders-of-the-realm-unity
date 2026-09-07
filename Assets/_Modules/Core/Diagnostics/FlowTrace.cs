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
    /// NEVER STRIP THE CALLS (owner ruling 2026-08-09) and Enabled DEFAULTS TRUE in every build,
    /// including release players — the trace you most need comes from a tester's device. Flip
    /// Enabled=false deliberately per-build/session if you must; the calls stay in the code.
    /// </summary>
    public static class FlowTrace
    {
        /// <summary>Master switch — <b>DEFAULTS TRUE IN EVERY BUILD, including release/WebGL</b>
        /// (owner ruling 2026-08-09). The old default gated it to editor/development builds; that
        /// rationale was written when this app was effectively a DEV VIEW, and it stopped holding
        /// the moment real testers started running shipped builds.
        /// <para/>
        /// ⚠ The retired rationale named a real cost, and it does not disappear: these lines reach a
        /// PRODUCTION log and can carry wallet ids, save-blob lengths and roster detail. The owner
        /// has weighed that against being able to triage a tester's device and ruled for tracing.
        /// Treat it as a live constraint on WHAT YOU LOG, not a reason to flip this back: do not add
        /// secrets, tokens or full save blobs to a trace line. Use <see cref="Mute"/> / <see cref="Only"/>
        /// to narrow a noisy category rather than disabling the master switch.</summary>
        // ⛔ ON EVERYWHERE, INCLUDING RELEASE PLAYERS (owner ruling 2026-08-09).
        // This used to read `Application.isEditor || Debug.isDebugBuild`, so a shipped release build
        // ran SILENT. That inverted the whole point of the instrument-first directive (CLAUDE.md
        // §12): the run you most need a trace from is the one on a TESTER'S device, doing something
        // you cannot reproduce — and that was precisely the build with tracing off. Keeping the calls
        // compiled in (never strip, same ruling) while defaulting them off preserved the cost and
        // discarded the benefit.
        // Still a runtime FIELD, so a build or a session can flip it off deliberately; what changed
        // is the DEFAULT. The !Enabled early-out in ShouldLog remains the zero-cost path for anyone
        // who does.
        public static bool Enabled = true;

        // --- pluggable SINK (owner spec 2026-06-18: "log OR weblog") --------------
        // ALL FlowTrace output is routed through Sink.{Info,Warn,Error} instead of
        // Debug.* directly, so the destination is swappable at runtime (Unity console
        // vs. a remote weblog) with NO change to the ~hundreds of call sites and NO
        // redeploy. Default = UnityLogSink (Debug.Log/LogWarning/LogError), i.e. the
        // exact shipped behaviour. Configure(...) (below) selects log-vs-weblog from
        // a config/remote source and can flip it back at any time. The sink call is
        // the only added indirection when on, and is never reached when Enabled=false
        // (the Allowed() / _active gates short-circuit first), so the off path stays
        // zero-alloc.
        /// <summary>Where every FlowTrace line goes. Swap at runtime via <see cref="Configure"/>
        /// (or assign directly). Never null — falls back to <see cref="UnityLogSink"/>.</summary>
        public static ITraceSink Sink
        {
            get => s_sink ?? s_defaultSink;
            set => s_sink = value ?? s_defaultSink;
        }
        private static readonly ITraceSink s_defaultSink = new UnityLogSink();
        private static ITraceSink s_sink = s_defaultSink;

        // --- per-category gating (INSTRUMENTATION_STANDARD §1.2) -----------------
        // The category is the existing first arg ("system"), so call sites never change.
        // s_only == null  => all categories allowed (default; no shipped-behaviour change).
        // s_only != null  => allow-list: only these categories log.
        // s_muted         => deny-set: these never log (applied after the allow-list).
        private static System.Collections.Generic.HashSet<string> s_only;
        private static readonly System.Collections.Generic.HashSet<string> s_muted = new System.Collections.Generic.HashSet<string>();

        // Thread-safety (security audit E-FTTHREAD): s_only, s_muted, s_nextAt and s_seen are
        // plain (non-thread-safe) collections that can be mutated from async/background callers
        // (FlowTrace is invoked all over the codebase, including off the main thread). A single
        // static lock guards every read+write of them. The hot-path Allowed() takes the lock
        // only AFTER the !Enabled early-out, so a shipped release build (Enabled=false) never
        // touches the lock and the off path stays zero-cost.
        private static readonly object s_traceLock = new object();

        /// <summary>Allow-list: log ONLY these systems (mutes all others). Pass none to clear.</summary>
        public static void Only(params string[] systems)
        {
            lock (s_traceLock)
            {
                s_only = (systems == null || systems.Length == 0)
                    ? null
                    : new System.Collections.Generic.HashSet<string>(systems);
            }
        }

        /// <summary>Deny-set: these systems never log (applied on top of any allow-list).</summary>
        public static void Mute(params string[] systems)
        {
            if (systems == null) return;
            lock (s_traceLock)
            {
                foreach (var s in systems) if (!string.IsNullOrEmpty(s)) s_muted.Add(s);
            }
        }

        /// <summary>Clear all category filters — every system logs again (still gated by Enabled).</summary>
        public static void AllOn()
        {
            lock (s_traceLock)
            {
                s_only = null;
                s_muted.Clear();
            }
        }

        /// <summary>O(1) gate: master switch + category allow-list + mute-set.</summary>
        private static bool Allowed(string system)
        {
            if (!Enabled) return false;
            // s_only/s_muted guarded by s_traceLock; the !Enabled early-out above keeps the
            // shipped release path (Enabled=false) lock-free and zero-cost.
            lock (s_traceLock)
            {
                if (s_only != null && !s_only.Contains(system)) return false;
                if (s_muted.Count > 0 && s_muted.Contains(system)) return false;
            }
            return true;
        }

        // --- thread-riding depth (owner 2026-06-18: "let it ride the thread all the way
        // down at every step"). A [ThreadStatic] nesting counter indents every line by call
        // depth so one run renders the FULL nested execution path top-to-bottom. Lightweight:
        // one int per thread, zero heap alloc, dark when Enabled=false.
        [System.ThreadStatic] private static int s_depth;

        // Smart deep depth (owner 2026-06-18: "4000 deep is simple math"). Keep a real
        // visual indent up to a sane cap; beyond it, emit a compact "[d<N>] " marker
        // instead of an N*2-space pad — so a 4000-deep thread costs an int + a tiny
        // string, never an 8000-char allocation. Shallow paths (the common case) are
        // unchanged. Pre-built pads for the capped range keep the hot path alloc-free.
        private const int MaxVisualDepth = 24;
        private static readonly string[] s_pads = BuildPads();
        private static string[] BuildPads()
        {
            var pads = new string[MaxVisualDepth + 1];
            for (int i = 0; i <= MaxVisualDepth; i++) pads[i] = new string(' ', i * 2);
            return pads;
        }
        private static string Pad()
        {
            int d = s_depth;
            if (d <= 0) return string.Empty;
            if (d <= MaxVisualDepth) return s_pads[d];           // pre-built, zero alloc
            return "[d" + d + "] ";                               // compact marker for extreme depth
        }

        /// <summary>Log a flow step you reached (indented by call depth — rides the thread).</summary>
        public static void Step(string system, string message)
        {
            if (Allowed(system)) Sink.Info($"[Flow:{system}] {Pad()}{message}");
        }

        /// <summary>Log a flow anomaly (missing ref, fallback taken, unexpected branch).</summary>
        public static void Warn(string system, string message)
        {
            if (Allowed(system)) Sink.Warn($"[Flow:{system}] {Pad()}{message}");
        }

        /// <summary>Log an error-level flow failure (exception caught, hard stop).</summary>
        public static void Fail(string system, string message)
        {
            if (Allowed(system)) Sink.Error($"[Flow:{system}] {Pad()}{message}");
        }

        /// <summary>
        /// CAPTURE-WORTHY BUT NOT A FAILURE (audit 2026-08-15). A state dump for an EXPECTED,
        /// normal-lifecycle event that must still reach <c>break-log.jsonl</c> for post-hoc reading
        /// — a hero death, a scene handoff, a queue drain.
        /// <para/>
        /// ⚠ WHY THIS EXISTS: <c>break-log</c>'s log-listener records only Error/Exception/Assert,
        /// so call sites that wanted a durable state dump reached for <see cref="Fail"/> — the only
        /// severity that survived to device. The result was a PERMANENT, EXPECTED error on the most
        /// common event in the game: the owner's F8 triage stream filled with her own deaths, and
        /// seats learned to ignore Hero failures. That degrades the instrument the whole
        /// instrument-first directive depends on. The fix is a THIRD severity, not a deleted trace.
        /// <para/>
        /// Behaviour: emits at INFO severity (so no listener, gate or daemon reads it as an error)
        /// AND records a <c>kind:"note"</c> row directly into break-log.jsonl via
        /// <see cref="BreakCaptureHarness.RecordNote"/>, bypassing the log sink entirely. The F8
        /// watch daemon skips <c>note</c> rows, so a normal-lifecycle capture never wakes a triage
        /// seat while the dump still lands in the file for anyone reading after the fact.
        /// <para/>
        /// Use <see cref="Fail"/> when something is actually WRONG. Use this when the event is
        /// expected and you want the state anyway.
        /// </summary>
        public static void Capture(string system, string message)
        {
            if (!Allowed(system)) return;
            Sink.Info($"[Flow:{system}] NOTE {Pad()}{message}");
            // Durable row in break-log.jsonl at a non-error kind. Best-effort: no harness (headless,
            // pre-boot, editor tooling) simply means the line above is the whole record.
            try { BreakCaptureHarness.RecordNote(system, message); } catch { /* a diagnostic never throws at its caller */ }
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
            lock (s_traceLock)
            {
                if (s_nextAt.TryGetValue(k, out float next) && now < next) return;
                s_nextAt[k] = now + everySeconds;
            }
            Sink.Info($"[Flow:{system}] {message}");
        }

        // --- once-only logging (for "did this run at all" checks) ---
        private static readonly HashSet<string> s_seen = new HashSet<string>();

        /// <summary>Log only the FIRST time this (system,key) is hit this play session.</summary>
        public static void Once(string system, string key, string message)
        {
            if (!Allowed(system)) return;
            string k = system + "/" + key;
            lock (s_traceLock)
            {
                if (!s_seen.Add(k)) return;
            }
            Sink.Info($"[Flow:{system}] {message}");
        }

        /// <summary>Reset once/throttle state — call on scene reload if you want fresh first-hit logs.</summary>
        public static void ResetSession()
        {
            lock (s_traceLock)
            {
                s_seen.Clear();
                s_nextAt.Clear();
            }
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
                    Sink.Warn($"[Flow:{_system}] {msg} (over {_warnAboveMs:F0}ms budget)");
                else
                    Sink.Info($"[Flow:{_system}] {msg}");
            }
        }

        // --- per-FRAME performance timing (WO-1483 / WO-1459) -----------------------
        // The 3-arg Measure above logs ONE LINE PER DISPOSE. On a frame path (7 sites x
        // 60 fps ~= 400 lines/s) that IS the spam — and on a device it evicts the boot
        // window out of the logcat ring, destroying the very evidence we instrumented
        // for (memory: logcat-ring-buffer-destroys-evidence). So the frame path gets its
        // OWN overload that does NOT log on dispose: it ACCUMULATES into a table, warns
        // at most once per `everySeconds` when a single pass blows its budget, and lets
        // PerfReporter roll the table up into ONE [Flow:Perf] "frame budget" line/sec.
        //
        // USAGE (first line of an Update/tick, so early returns are covered too):
        //   using var _ = FlowTrace.Measure("Perf", "HeroLocomotion.Update", 4f, 1f);
        //
        // Timing uses Stopwatch ticks, NOT Time.realtimeSinceStartup: that float loses
        // ~1ms of resolution after a few hours of uptime, which is useless against a 4ms
        // budget. Keys are the caller's string LITERAL (interned) — never build a key
        // string in the hot path.
        public static FrameScope Measure(string system, string what, float warnAboveMs, float everySeconds)
        {
            return new FrameScope(system, what, warnAboveMs, everySeconds);
        }

        /// <summary>One accumulated frame-path scope: total/worst/count over the roll-up window.</summary>
        public struct FrameSample
        {
            public string Sys;
            public string What;
            public double SumMs;
            public double MaxMs;
            public int Count;
        }

        private sealed class FrameAccum
        {
            public string Sys;
            public string What;
            public double SumMs;
            public double MaxMs;
            public int Count;
            public float LastWarnAt;
        }

        private static readonly Dictionary<string, FrameAccum> s_frameAccum =
            new Dictionary<string, FrameAccum>(32);
        private static readonly object s_frameLock = new object();
        private static readonly double s_msPerTick =
            1000.0 / System.Diagnostics.Stopwatch.Frequency;

        private static void RecordFrameSample(string system, string what, double ms,
                                              float warnAboveMs, float everySeconds)
        {
            bool warn = false;
            lock (s_frameLock)
            {
                if (!s_frameAccum.TryGetValue(what, out var acc))
                {
                    acc = new FrameAccum { Sys = system, What = what, LastWarnAt = float.NegativeInfinity };
                    s_frameAccum[what] = acc;
                }
                acc.SumMs += ms;
                acc.Count++;
                if (ms > acc.MaxMs) acc.MaxMs = ms;

                if (warnAboveMs > 0f && ms > warnAboveMs)
                {
                    float now = Time.realtimeSinceStartup;
                    if (everySeconds <= 0f || now - acc.LastWarnAt >= everySeconds)
                    {
                        acc.LastWarnAt = now;
                        warn = true;
                    }
                }
            }
            // Emit OUTSIDE the lock — the sink can be arbitrarily slow (WebTrace POSTs).
            if (warn)
                Sink.Warn($"[Flow:{system}] {what} took {ms:F1}ms (over {warnAboveMs:F0}ms frame budget)");
        }

        /// <summary>
        /// Drain the accumulated frame-path table into <paramref name="into"/> (cleared first)
        /// and reset it for the next window. Called by PerfReporter's 1s roll-up; safe to call
        /// from anywhere. Returns the number of scopes drained.
        /// </summary>
        public static int SnapshotAndResetFrameSamples(List<FrameSample> into)
        {
            if (into == null) return 0;
            into.Clear();
            lock (s_frameLock)
            {
                foreach (var kv in s_frameAccum)
                {
                    var a = kv.Value;
                    if (a.Count <= 0) continue;
                    into.Add(new FrameSample
                    {
                        Sys = a.Sys,
                        What   = a.What,
                        SumMs  = a.SumMs,
                        MaxMs  = a.MaxMs,
                        Count  = a.Count,
                    });
                    a.SumMs = 0.0;
                    a.MaxMs = 0.0;
                    a.Count = 0;
                }
            }
            return into.Count;
        }

        /// <summary>
        /// Disposable timing scope returned by the 4-arg <see cref="Measure(string,string,float,float)"/>.
        /// Accumulates instead of logging — see the comment block above. Readonly struct, no heap alloc.
        /// </summary>
        public readonly struct FrameScope : System.IDisposable
        {
            private readonly string _system;
            private readonly string _what;
            private readonly float  _warnAboveMs;
            private readonly float  _everySeconds;
            private readonly long   _startTicks;
            private readonly bool   _active;

            internal FrameScope(string system, string what, float warnAboveMs, float everySeconds)
            {
                _system       = system;
                _what         = what;
                _warnAboveMs  = warnAboveMs;
                _everySeconds = everySeconds;
                _active       = Enabled;
                _startTicks   = _active ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            }

            public void Dispose()
            {
                if (!_active) return;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - _startTicks) * s_msPerTick;
                RecordFrameSample(_system, _what, ms, _warnAboveMs, _everySeconds);
            }
        }

        // --- Enter: ride the thread all the way down --------------------------------
        // A scoped enter/exit trace that follows the execution thread down through every
        // layer. Each nested Enter indents deeper, so one run shows the WHOLE call path and
        // exactly where it stopped. Lightweight readonly struct, zero heap alloc.
        // USAGE:  using var _ = FlowTrace.Enter("Seam", "Cross to world");
        public static FlowScope Enter(string system, string what) => new FlowScope(system, what);

        /// <summary>Enter/exit scope from <see cref="Enter"/>: logs "-&gt; what", indents, and on
        /// Dispose logs "&lt;- what (Xms)" + de-indents. Rides the thread down.</summary>
        public readonly struct FlowScope : System.IDisposable
        {
            private readonly string _system;
            private readonly string _what;
            private readonly float _startMs;
            private readonly bool _active;

            internal FlowScope(string system, string what)
            {
                _system = system;
                _what = what;
                _active = Allowed(system);
                if (_active)
                {
                    Sink.Info($"[Flow:{system}] {Pad()}-> {what}");
                    s_depth++;
                    _startMs = Time.realtimeSinceStartup * 1000f;
                }
                else _startMs = 0f;
            }

            public void Dispose()
            {
                if (!_active) return;
                s_depth = s_depth > 0 ? s_depth - 1 : 0;
                float ms = Time.realtimeSinceStartup * 1000f - _startMs;
                Sink.Info($"[Flow:{_system}] {Pad()}<- {_what} ({ms:F1}ms)");
            }
        }

        // --- Try: catch + roll up (owner 2026-06-18: "catch try e debug(log)"). Every risky
        // op runs through here: a thrown exception ALWAYS LogErrors (independent of Enabled, so
        // a real failure can never be silenced) -> lands in BreakCaptureHarness -> rolls up as
        // a captured ticket, then flow continues with the fallback. The system self-detects;
        // the owner is NEVER the one to notice a break.
        public static void Try(string system, string what, System.Action action)
        {
            try { action(); }
            catch (System.Exception e)
            {
                Sink.Error($"[Flow:{system}] {Pad()}FAILED at '{what}': {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>Guarded compute: returns <paramref name="fallback"/> and rolls the exception up on throw.</summary>
        public static T Try<T>(string system, string what, System.Func<T> fn, T fallback = default)
        {
            try { return fn(); }
            catch (System.Exception e)
            {
                Sink.Error($"[Flow:{system}] {Pad()}FAILED at '{what}': {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return fallback;
            }
        }

        // --- Config-driven selection + runtime reversibility (owner spec 2026-06-18) -----
        // Configure() is the ONE entry point that decides, from a config/remote source,
        // whether tracing is ON, which SINK is active (Unity console vs. remote weblog),
        // its endpoint URL, and the category filters. It is REVERSIBLE at any time with
        // NO redeploy: call it again with a new TraceConfig (e.g. fetched from the remote
        // feature-flags service of WO-445) to flip Enabled off, swap back to the Unity
        // log, or mute a now-proven system. The remote/db source decides the values;
        // this method just applies them onto the existing toggles.
        //
        // WIRING NOTE: a remote flag/config service (WO-445 RemoteFlags) does NOT exist
        // yet. Until it lands, build a TraceConfig from PlayerPrefs / FeatureFlags /
        // hardcoded dev values and pass it here. When RemoteFlags lands, have it call
        // FlowTrace.Configure(TraceConfig.FromRemote(...)) on fetch + on refresh so a
        // server flip propagates with no rebuild (see TraceConfig below).
        /// <summary>
        /// Apply a <see cref="TraceConfig"/>: sets <see cref="Enabled"/>, selects the
        /// <see cref="Sink"/> (Unity log vs. remote weblog + its URL) and the category
        /// allow/mute filters from a config/remote source. Fully reversible — call again
        /// with a different config to flip back at runtime, no redeploy. Never throws.
        /// </summary>
        public static void Configure(TraceConfig cfg)
        {
            if (cfg == null) return;
            Try("FlowTrace", "Configure", () =>
            {
                Enabled = cfg.Enabled;

                // Sink selection. Web → WebTraceSink (configurable URL); else Unity log.
                if (cfg.UseWebSink && !string.IsNullOrEmpty(cfg.WebUrl))
                {
                    // Reuse one live web sink; just retarget its URL on reconfigure so a
                    // server-side URL change is picked up without a redeploy.
                    if (s_webSink == null) s_webSink = new WebTraceSink(cfg.WebUrl);
                    else s_webSink.SetEndpoint(cfg.WebUrl);
                    Sink = s_webSink;
                }
                else
                {
                    // Swap BACK to the Unity console log (reversibility) and flush any
                    // pending web batch so nothing is lost on the swap-down.
                    s_webSink?.Flush();
                    Sink = s_defaultSink;
                }

                // Category filters (allow-list / mute-set), also reversible.
                Only(cfg.Only);
                AllOnMuteApply(cfg.Mute);
            });
        }
        private static WebTraceSink s_webSink;

        // Apply a mute-set fresh: clear prior mutes (so a reconfigure that drops a system
        // from Mute un-mutes it), then add the new ones. Keeps the allow-list set by Only().
        private static void AllOnMuteApply(string[] mute)
        {
            lock (s_traceLock) { s_muted.Clear(); }
            if (mute != null) Mute(mute);   // Mute takes the lock itself
        }

        /// <summary>
        /// Plain trace settings, sourced from config / remote / db (WO-445). It carries
        /// no behaviour — <see cref="Configure"/> applies it. A remote-flags service builds
        /// one of these from the server's authoritative values and re-applies it on refresh,
        /// so trace on/off + log-vs-weblog + filters change with NO rebuild.
        /// </summary>
        public sealed class TraceConfig
        {
            /// <summary>Master on/off (maps to <see cref="Enabled"/>).</summary>
            public bool Enabled = true;
            /// <summary>True → route output to the remote <see cref="WebTraceSink"/>; false → Unity log.</summary>
            public bool UseWebSink;
            /// <summary>Remote weblog endpoint. From config/remote — NO hardcoded URL, NO secret.</summary>
            public string WebUrl;
            /// <summary>Category allow-list (null/empty = all). Maps to <see cref="Only"/>.</summary>
            public string[] Only;
            /// <summary>Category deny-set. Maps to <see cref="Mute"/>.</summary>
            public string[] Mute;
        }
    }

    // =========================================================================
    //  Trace sinks — the destination FlowTrace output is routed to.
    // =========================================================================

    /// <summary>
    /// Destination for FlowTrace output. Implementations decide where a line goes
    /// (Unity console, a remote weblog, a test buffer, …). Three levels mirror the
    /// Step/Warn/Fail mapping so a sink can route by severity.
    /// </summary>
    public interface ITraceSink
    {
        void Info(string line);
        void Warn(string line);
        void Error(string line);
    }

    /// <summary>
    /// Default sink: the Unity console (Debug.Log / LogWarning / LogError). This is the
    /// exact pre-existing behaviour — error lines still reach BreakCaptureHarness →
    /// break-log.jsonl. Zero config; always available as the reversible fallback.
    /// </summary>
    public sealed class UnityLogSink : ITraceSink
    {
        public void Info(string line)  => Debug.Log(line);
        public void Warn(string line)  => Debug.LogWarning(line);
        public void Error(string line) => Debug.LogError(line);
    }
}
