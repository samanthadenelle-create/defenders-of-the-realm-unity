// =============================================================================
// WebTrace — WO-443: a remote-logging sink for the WEBGL build.
// -----------------------------------------------------------------------------
// The web grant-demo target can't be reproduced locally, so when "web tracing"
// is activated (FeatureFlags.WebTrace, or the URL ?trace=1) this sink streams a
// real player's diagnostic logs to the backend, which writes them to a Neon
// table with a 7-day TTL. It is the SECOND sink alongside BreakCaptureHarness's
// local break-log.jsonl — it ADDS a remote path, it does NOT change local
// behaviour.
//
// ⚠ THIS SINK IS LIVE — the "dormant by default" claim below WAS FALSE and cost a
// session on 2026-07-15 (a CLI read it, believed prod was untraced, and wrote a bogus
// WO instead of reading the data that was already in the DB). Verified from CODE:
//   1. FeatureFlags.WebTrace — FeatureFlags.cs:117 is `Get("webtrace", defaultOn: TRUE)`.
//      It is ON by default. (?trace=1 / PlayerPrefs ff.webtrace can also flip it per session.)
//   2. TraceEndpoint — line ~63 is SET to the prod /api/trace URL, not empty.
// BOTH GATES ARE OPEN, so a shipped WebGL player streams its FlowTrace to Neon
// (analytics_events, event_name='web_trace') right now. FlowTrace.Enabled defaults OFF in a
// release player (PII), and this sink sets it TRUE on activation (see Install) — that is the
// ONLY reason a ship build traces at all.
// READ PATH: the `[sig]` echo in Vercel runtime logs (api/trace.js logs each signal line), or
// the key-gated admin endpoint api/admin/db.js (?view=traces&session=...&order=asc) —
// DATABASE_URL is sensitive and cannot be pulled.
//
// Design rules (mirrors BreakCaptureHarness + §12 INSTRUMENT-don't-guess):
//   * Subscribes to Application.logMessageReceived (Log/Warning/Error/Exception
//     + condition + stack).
//   * BOUNDED ring buffer (cap RingCap, drop-oldest) — a log storm can't grow
//     memory unbounded.
//   * Flushes in batches every FlushSeconds OR when FlushThreshold entries queue,
//     via a SINGLE UnityWebRequest POST (JSON body) — mirrors GameStateService's
//     SendDelta (UploadHandlerRaw + DownloadHandlerBuffer + Content-Type) and its
//     TryAttachAuthHeaders auth pattern. On failure the batch is DROPPED (no
//     retry-storm). Never blocks the main thread.
//   * Reentrancy-guarded so the sink's own logs never feed back into the buffer.
//   * EVERYTHING wrapped in Guard.Try — the sink must NEVER throw into gameplay.
//
// SECURITY (per WO-429): NO connection string / secret in the client. Client →
// HTTPS → backend → Neon. The sessionId is an anonymous per-session id (no PII).
//
// The actual remote POST is WebGL-only (#if UNITY_WEBGL && !UNITY_EDITOR);
// standalone/editor already have break-log.jsonl, so off-WebGL this is a clean
// no-op. The backend half (POST /api/trace + the Neon web_traces table + the
// 7-day cron) lives in the React/Vercel repo — out of scope for this client.
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
// Force the engine logger even if a DeNelle.Core.Debug namespace exists
// (see memory: core-namespace-shadows-unityengine-statics).
using Debug = UnityEngine.Debug;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// WO-443 — WebGL remote diagnostic-log sink. Installs itself at startup, and
    /// only when activated AND a backend endpoint is configured does it buffer and
    /// batch-POST captured logs. Both gates are OPEN by default in a shipped WebGL
    /// build (FeatureFlags.WebTrace defaults ON; TraceEndpoint is the hardcoded PROD
    /// url) and Awake flips FlowTrace.Enabled=true on activation — so a WebGL player
    /// streams [Flow:*] to Neon by default. Dormant (no-op) only off-WebGL / in the
    /// editor, where break-log.jsonl already exists.
    /// </summary>
    public sealed class WebTrace : MonoBehaviour
    {
        // ── Config ────────────────────────────────────────────────────────────
        // The backend endpoint that receives the trace batch (POST /api/trace).
        // SET to the live PROD url below (NOT empty) → the sink is active on WebGL.
        // This is the single place to wire the URL; it carries NO secret (the Neon
        // connstring stays server-side only, per WO-429). Mirrors GameStateService's
        // BackendBase.
        private const string TraceEndpoint = "https://defenders-of-the-realm-v2.vercel.app/api/trace";

        // Bounded ring + batch cadence — sized so a log storm can't flood memory or
        // the endpoint.
        private const int   RingCap        = 500;   // max buffered entries (drop-oldest)
        private const int   FlushThreshold = 50;    // flush early once this many queue
        private const float FlushSeconds   = 5f;    // …otherwise flush on this cadence
        private const int   MaxBatch        = 200;  // entries per POST (cap the body size)

        // ── Singleton / install ───────────────────────────────────────────────
        private static WebTrace s_instance;
        private static bool s_installed;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly Queue<Entry> _ring = new Queue<Entry>(RingCap);
        private bool _inHandler;          // reentrancy guard (our own logs must not re-enter)
        private bool _active;             // resolved once on install: flag ON + endpoint set
        private bool _posting;            // a flush is in flight — don't overlap
        private string _sessionId;        // anonymous, per-session (no PII)
        private string _buildId;
        private static bool s_warnedNoEndpoint;

        /// <summary>
        /// One captured log line. Plain primitives only → trivially JSON-serialised.
        /// </summary>
        private struct Entry
        {
            public long   utcMs;
            public string kind;     // log | warning | error | exception | assert
            public string tag;      // [Flow:&lt;system&gt;] system if present, else ""
            public string message;
            public string stack;
            public string scene;
        }

        // =====================================================================
        //  Bootstrap
        // =====================================================================

        /// <summary>
        /// WO-443 — install the sink at startup. Honours the URL ?trace=1 one-session
        /// activation first, then installs ONLY when FeatureFlags.WebTrace is ON. The
        /// driver itself compiles on every platform; the remote POST is WebGL-gated
        /// below, so off-WebGL this stays a clean no-op even if the flag is on.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Guard.Try("WebTrace", "install", () =>
            {
                if (s_installed) return;
                s_installed = true;

                // Let support flip it on for this session via the WebGL URL first.
                DeNelle.Core.FeatureFlags.ApplyUrlActivationOnce();

                if (!DeNelle.Core.FeatureFlags.WebTrace)
                    return;   // not activated — stay fully dormant (no GameObject, no handler).

                var go = new GameObject("WebTrace (auto)");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                s_instance = go.AddComponent<WebTrace>();
            });
        }

        private void Awake()
        {
            Guard.Try("WebTrace", "awake", () =>
            {
                if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
                s_instance = this;

                _sessionId = MakeSessionId();
                _buildId   = MakeBuildId();

                // Endpoint gate: empty until the backend lands → no-op, one local note.
                if (string.IsNullOrEmpty(TraceEndpoint))
                {
                    _active = false;
                    if (!s_warnedNoEndpoint)
                    {
                        s_warnedNoEndpoint = true;
                        Debug.Log("[WebTrace] Activated but no TraceEndpoint is configured — " +
                                  "web tracing is dormant (no remote POST). Wire WebTrace.TraceEndpoint " +
                                  "when the backend POST /api/trace lands.");
                    }
                    return;
                }

                _active = true;
                // CRITICAL (2026-07-01): FlowTrace ships OFF in a release/WebGL build
                // (FlowTrace.Enabled = isEditor || isDebugBuild), so every FlowTrace.Step/Warn/Fail
                // is a no-op and its [Flow:*] lines never reach Debug.Log — which meant "web debugging
                // on" captured plain logs but NONE of the instrumentation (the Pi sign-in flow was
                // invisible). When web-tracing is deliberately active, turn FlowTrace ON so its lines
                // emit -> get captured here -> POST to /api/trace. Gated by ff.webtrace (opt-in), so a
                // build with tracing off is unaffected. (Pre-release: FlowTrace lines can carry ids;
                // ff.webtrace is config-flippable OFF for launch — see the header note above.)
                FlowTrace.Enabled = true;
                // Main-thread handler (mirrors BreakCaptureHarness) — the ring + flush
                // coroutine all run on the main thread, so no cross-thread access to _ring.
                Application.logMessageReceived += OnLog;
                StartCoroutine(FlushLoop());
                Debug.Log($"[WebTrace] Remote trace sink active (session={_sessionId}, build={_buildId}).");
            });
        }

        private void OnDestroy()
        {
            if (_active) Application.logMessageReceived -= OnLog;
            if (s_instance == this) s_instance = null;
        }

        // =====================================================================
        //  Capture
        // =====================================================================

        /// <summary>
        /// Application.logMessageReceived handler. Reentrancy-guarded so our own POST/
        /// status logs can't feed back into the ring. Drops the oldest entry when the
        /// ring is full (bounded). Wrapped so a malformed log can never throw into Unity's
        /// log pump.
        /// </summary>
        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!_active || _inHandler) return;
            _inHandler = true;
            try
            {
                var e = new Entry
                {
                    utcMs   = NowUtcMs(),
                    kind    = KindOf(type),
                    tag     = ExtractFlowTag(condition),
                    message = Truncate(condition, 2000),
                    stack   = Truncate(stackTrace, 4000),
                    scene   = SafeSceneName(),
                };

                while (_ring.Count >= RingCap) _ring.Dequeue();   // drop-oldest
                _ring.Enqueue(e);
            }
            catch
            {
                // Never throw into the log pump. (No FlowTrace here — that would re-enter.)
            }
            finally
            {
                _inHandler = false;
            }
        }

        // =====================================================================
        //  Flush
        // =====================================================================

        private IEnumerator FlushLoop()
        {
            var wait = new WaitForSecondsRealtime(FlushSeconds);
            while (_active)
            {
                // Flush on cadence, or earlier if the buffer crosses the threshold.
                float t = 0f;
                while (t < FlushSeconds && _ring.Count < FlushThreshold)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                }

                if (!_posting && _ring.Count > 0)
                    yield return StartCoroutine(Flush());
                else
                    yield return wait;
            }
        }

        /// <summary>
        /// WO-1324: Force an immediate flush from a critical event (hitch detection).
        /// Public so VfxPerformanceGate can invoke it when frame time exceeds budget.
        /// Does nothing if a flush is already in flight (no buffering of forced flushes).
        /// </summary>
        public static void ForceFlush()
        {
            Guard.Try("WebTrace", "force-flush", () =>
            {
                if (s_instance != null && !s_instance._posting && s_instance._ring.Count > 0)
                {
                    FlowTrace.Step("WebTrace", "force-flush triggered: " + s_instance._ring.Count +
                        " buffered entries will post immediately");
                    s_instance.StartCoroutine(s_instance.Flush());
                }
            });
        }

        private IEnumerator Flush()
        {
            _posting = true;

            // Drain up to MaxBatch entries into a local list (bounded body size).
            List<Entry> batch = null;
            Guard.Try("WebTrace", "drain", () =>
            {
                int n = Mathf.Min(_ring.Count, MaxBatch);
                batch = new List<Entry>(n);
                for (int i = 0; i < n && _ring.Count > 0; i++) batch.Add(_ring.Dequeue());
            });

            if (batch == null || batch.Count == 0)
            {
                _posting = false;
                yield break;
            }

            int batchCount = batch.Count;
            int remainingInRing = _ring.Count;

#if UNITY_WEBGL && !UNITY_EDITOR
            // The remote path is WebGL-only — standalone/editor already have break-log.jsonl.
            byte[] body = null;
            Guard.Try("WebTrace", "serialize", () => { body = BuildBody(batch); });

            if (body != null && body.Length > 0)
            {
                UnityWebRequest req = null;
                Guard.Try("WebTrace", "build request", () =>
                {
                    req = new UnityWebRequest(TraceEndpoint, "POST")
                    {
                        uploadHandler   = new UploadHandlerRaw(body),
                        downloadHandler = new DownloadHandlerBuffer(),
                    };
                    req.SetRequestHeader("Content-Type", "application/json");
                    // Mirror GameStateService.TryAttachAuthHeaders: identify the client so the
                    // endpoint can authorise/rate-limit. NO secret here — these are anonymous,
                    // non-PII identifiers; the Neon connstring stays server-side (WO-429).
                    req.SetRequestHeader("X-Trace-Session", _sessionId);
                    req.SetRequestHeader("X-Trace-Build", _buildId);
                });

                if (req != null)
                {
                    yield return req.SendWebRequest();

                    // WO-1324: Report every batch outcome, not just failures.
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        // On failure: DROP the batch (already dequeued) — no retry-storm.
                        // REPORT THE DROP so it is not silently lost in the logs.
                        // NOTE: FlowTrace.Warn has exactly ONE overload - (system, message). The
                        // former key argument ("post-fail") is folded into the message text so no
                        // information is lost; do not re-add a third argument (WebGL-only compile).
                        FlowTrace.Warn("WebTrace",
                            $"post-fail: trace POST failed ({req.responseCode}): {req.error} - batch of {batchCount} " +
                            $"entries dropped (session={_sessionId}, {remainingInRing} remain buffered)");
                    }
                    else
                    {
                        // Successful POST: trace it so the window is visible in the capture.
                        FlowTrace.Throttle("WebTrace", "post-ok", 5f,
                            $"trace batch posted: {batchCount} entries sent, {remainingInRing} remain buffered");
                    }

                    Guard.Try("WebTrace", "dispose", () => req.Dispose());
                }
            }
#else
            // Off-WebGL: nothing to send. The batch is intentionally dropped (local
            // capture already covers standalone/editor via BreakCaptureHarness).
            FlowTrace.Throttle("WebTrace", "non-webgl", 60f,
                $"WebTrace off-WebGL: batch of {batchCount} entries dropped (local capture via " +
                "BreakCaptureHarness covers this platform)");
            yield return null;
#endif

            _posting = false;
        }

        // =====================================================================
        //  Serialisation — hand-rolled JSON (no dependency on the save layer's
        //  Newtonsoft settings; primitives only, so this is small + safe).
        // =====================================================================

        private byte[] BuildBody(List<Entry> batch)
        {
            var sb = new StringBuilder(256 + batch.Count * 128);
            sb.Append("{\"sessionId\":").Append(Q(_sessionId))
              .Append(",\"buildId\":").Append(Q(_buildId))
              .Append(",\"entries\":[");
            for (int i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"utcMs\":").Append(e.utcMs)
                  .Append(",\"kind\":").Append(Q(e.kind))
                  .Append(",\"tag\":").Append(Q(e.tag))
                  .Append(",\"message\":").Append(Q(e.message))
                  .Append(",\"stack\":").Append(Q(e.stack))
                  .Append(",\"scene\":").Append(Q(e.scene))
                  .Append('}');
            }
            sb.Append("]}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>Minimal JSON string quote/escape (control chars + quotes + backslash).</summary>
        private static string Q(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string MakeSessionId()
        {
            // Anonymous, per-session — NO PII (not the device id, not the wallet).
            return Guard.Try("WebTrace", "session id",
                () => "wt-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                fallback: "wt-anon");
        }

        /// <summary>
        /// Build identity for a trace batch: <c>&lt;version&gt;@&lt;host&gt;</c> on web, bare version elsewhere.
        /// </summary>
        /// <remarks>
        /// WHY THE HOST (2026-07-15, the magenta-ground triage): <see cref="TraceEndpoint"/> is
        /// hardcoded to the PROD domain, so EVERY build — prod and every preview — posts into the
        /// same analytics_events table. This id was <c>Application.version</c> alone, which is
        /// "1.0" for all of them, so all 20 recorded sessions read <c>build=1.0</c> and a session
        /// could NOT be attributed to a deployment. Concretely: a preview whose ground rendered
        /// MAGENTA and a healthy prod were indistinguishable in the data, so the trace could not
        /// answer "is prod affected?" — the question that actually mattered.
        /// Qualifying with the host the player actually loaded makes every row attributable
        /// without changing where traces are POSTed (the hardcoded endpoint is deliberate — it
        /// always resolves, even from a host that has no /api/trace of its own).
        /// STILL NO PII (WO-429): a deployment hostname is not a user. Off-web
        /// <c>Application.absoluteURL</c> is empty → bare version, so desktop/editor are unchanged.
        /// Guarded: an unparseable URL must never throw out of Install and kill the sink.
        /// </remarks>
        private static string MakeBuildId()
        {
            string ver = string.IsNullOrEmpty(Application.version) ? "unknown" : Application.version;
            string host = Guard.Try("WebTrace", "build host",
                () =>
                {
                    string url = Application.absoluteURL;
                    return string.IsNullOrEmpty(url) ? null : new Uri(url).Host;
                },
                fallback: null);
            return string.IsNullOrEmpty(host) ? ver : ver + "@" + host;
        }

        private static long NowUtcMs()
            => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        private static string KindOf(LogType type)
        {
            switch (type)
            {
                case LogType.Error:     return "error";
                case LogType.Assert:    return "assert";
                case LogType.Warning:   return "warning";
                case LogType.Exception: return "exception";
                default:                return "log";
            }
        }

        /// <summary>Pulls the "&lt;system&gt;" out of a "[Flow:&lt;system&gt;] …" line, else "".</summary>
        private static string ExtractFlowTag(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            const string open = "[Flow:";
            int i = message.IndexOf(open, StringComparison.Ordinal);
            if (i < 0) return "";
            int start = i + open.Length;
            int end = message.IndexOf(']', start);
            return end > start ? message.Substring(start, end - start) : "";
        }

        private static string SafeSceneName()
            => Guard.Try("WebTrace", "scene name", () => SceneManager.GetActiveScene().name, fallback: "");

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max);
        }
    }
}
