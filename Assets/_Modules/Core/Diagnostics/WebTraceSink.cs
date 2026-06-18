// =============================================================================
// WebTraceSink — owner spec 2026-06-18: the "weblog" half of the pluggable
// FlowTrace.Sink. An ITraceSink that batches trace lines and POSTs them to a
// CONFIGURABLE remote URL (UnityWebRequest, fire-and-forget), so a real web
// player's FlowTrace output can be triaged off-device.
// -----------------------------------------------------------------------------
// This is the DIRECT-OUTPUT sink for FlowTrace (Step/Warn/Fail/… → Sink.* →
// here). It is a SIBLING to WebTrace.cs (WO-443), which captures the WHOLE Unity
// log pump (Application.logMessageReceived) — that one mirrors break-log.jsonl;
// THIS one is FlowTrace's own routed output, selected via FlowTrace.Configure.
// They share the same plumbing pattern (bounded buffer, batch POST, no-secret
// anonymous headers, never-throw) but are independent: enabling one does not
// require the other.
//
// Design rules (mirror WebTrace.cs + §12 INSTRUMENT-don't-guess):
//   * CONFIGURABLE endpoint — passed in / SetEndpoint(); NO hardcoded URL, NO
//     secret/PII in code (per WO-429). Empty URL → never posts.
//   * BOUNDED buffer (drop-oldest) — a log storm can't grow memory unbounded.
//   * Flush on a SIZE threshold or a TIME threshold (checked on each append).
//   * Fire-and-forget POST, WebGL-safe. Off-WebGL (editor/standalone) the remote
//     path is compiled out and lines ALSO echo to the Unity log so nothing is lost.
//   * On a failed/again-unconfigured post the batch FALLS BACK to the Unity log
//     (never silently dropped in a way that hides data) and NEVER throws on the
//     game thread.
//   * Reentrancy: the sink's OWN diagnostics use Debug.* directly (never FlowTrace),
//     so they can't feed back into the buffer.
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
// Force the engine logger even if a DeNelle.Core.Debug namespace exists
// (see memory: core-namespace-shadows-unityengine-statics).
using Debug = UnityEngine.Debug;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// Remote "weblog" sink for <see cref="FlowTrace.Sink"/>. Batches lines and
    /// fire-and-forget POSTs them to a configurable URL. WebGL-safe; falls back to
    /// the Unity log when unconfigured, off-WebGL, or a post fails. Never throws.
    /// </summary>
    public sealed class WebTraceSink : ITraceSink
    {
        // ── Tunables (sized so a storm can't flood memory or the endpoint) ──────
        private const int   RingCap        = 500;  // max buffered lines (drop-oldest)
        private const int   FlushThreshold = 50;   // flush once this many queue
        private const float FlushSeconds   = 5f;   // …otherwise flush on this cadence
        private const int   MaxBatch       = 200;  // lines per POST (cap body size)

        // ── Config ──────────────────────────────────────────────────────────────
        private string _endpoint;                  // configurable; empty → no remote post
        private readonly string _sessionId;        // anonymous, per-session (no PII)
        private readonly string _buildId;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly List<string> _buffer = new List<string>(64);
        private readonly object _lock = new object();
        private float _nextFlushAt;
        private static bool s_warnedNoEndpoint;

        // A hidden driver MonoBehaviour to run the actual UnityWebRequest coroutine
        // (a static sink has no MonoBehaviour of its own). Created lazily, only when
        // a configured endpoint actually needs a post on WebGL. The driver type is
        // compiled ONLY under WebGL (below), so this field must be too.
#if UNITY_WEBGL && !UNITY_EDITOR
        private static WebTraceSinkDriver s_driver;
#endif

        /// <param name="endpoint">Remote URL to POST batches to. From config/remote —
        /// no hardcoded value, no secret. Empty/null = buffer + Unity-log fallback only.</param>
        public WebTraceSink(string endpoint)
        {
            _endpoint  = endpoint ?? string.Empty;
            _sessionId = MakeSessionId();
            _buildId   = string.IsNullOrEmpty(Application.version) ? "unknown" : Application.version;
            _nextFlushAt = SafeNow() + FlushSeconds;
        }

        /// <summary>Retarget the endpoint at runtime (e.g. a remote-config URL change) —
        /// no redeploy. Empty/null falls back to Unity-log-only.</summary>
        public void SetEndpoint(string endpoint)
        {
            lock (_lock) { _endpoint = endpoint ?? string.Empty; }
        }

        // ── ITraceSink ──────────────────────────────────────────────────────────
        public void Info(string line)  => Append(line);
        public void Warn(string line)  => Append(line);
        public void Error(string line) => Append(line);

        private void Append(string line)
        {
            // Never throw on the game thread; the sink failing must not break the app.
            try
            {
                // Off-WebGL we can't usefully POST (no real web player); echo to the
                // Unity log so the line is never lost, and still buffer for parity.
#if !UNITY_WEBGL || UNITY_EDITOR
                Debug.Log(line);
#endif
                bool flushNow;
                lock (_lock)
                {
                    if (_buffer.Count >= RingCap) _buffer.RemoveAt(0);   // drop-oldest
                    _buffer.Add(line);
                    flushNow = _buffer.Count >= FlushThreshold || SafeNow() >= _nextFlushAt;
                }
                if (flushNow) Flush();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WebTraceSink] append skipped: " + e.Message);
            }
        }

        /// <summary>Drain the buffer and dispatch a batch POST (fire-and-forget). Safe to
        /// call any time (e.g. before swapping the sink back to the Unity log).</summary>
        public void Flush()
        {
            try
            {
                List<string> batch;
                lock (_lock)
                {
                    _nextFlushAt = SafeNow() + FlushSeconds;
                    if (_buffer.Count == 0) return;
                    int n = Mathf.Min(_buffer.Count, MaxBatch);
                    batch = _buffer.GetRange(0, n);
                    _buffer.RemoveRange(0, n);
                }

                if (string.IsNullOrEmpty(_endpoint))
                {
                    // No endpoint configured → do NOT lose the data silently: surface it on
                    // the Unity log (once-warned that the remote path is dormant).
                    if (!s_warnedNoEndpoint)
                    {
                        s_warnedNoEndpoint = true;
                        Debug.Log("[WebTraceSink] no endpoint configured — remote trace dormant; " +
                                  "lines fall back to the Unity log. Wire a URL via FlowTrace.Configure.");
                    }
                    FallbackToLog(batch);
                    return;
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                Post(batch);   // fire-and-forget on the driver
#else
                // Off-WebGL the lines already echoed to the Unity log in Append(); nothing
                // to send. (Standalone/editor are covered by BreakCaptureHarness.)
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WebTraceSink] flush skipped: " + e.Message);
            }
        }

        private static void FallbackToLog(List<string> batch)
        {
            for (int i = 0; i < batch.Count; i++) Debug.Log(batch[i]);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void Post(List<string> batch)
        {
            var driver = Driver();
            if (driver == null) { FallbackToLog(batch); return; }   // can't post → don't lose data

            byte[] body = BuildBody(batch);
            if (body == null || body.Length == 0) return;

            string url = _endpoint;
            driver.Send(url, body, _sessionId, _buildId, batch);
        }

        private static WebTraceSinkDriver Driver()
        {
            try
            {
                if (s_driver != null) return s_driver;
                var go = new GameObject("WebTraceSink (auto)");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                s_driver = go.AddComponent<WebTraceSinkDriver>();
                return s_driver;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WebTraceSink] driver create failed: " + e.Message);
                return null;
            }
        }
#endif

        // ── Serialisation — hand-rolled JSON (primitives only; small + safe) ─────
        private byte[] BuildBody(List<string> batch)
        {
            var sb = new StringBuilder(128 + batch.Count * 96);
            sb.Append("{\"sessionId\":").Append(Q(_sessionId))
              .Append(",\"buildId\":").Append(Q(_buildId))
              .Append(",\"lines\":[");
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Q(batch[i]));
            }
            sb.Append("]}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

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

        private static string MakeSessionId()
        {
            try { return "wts-" + System.Guid.NewGuid().ToString("N").Substring(0, 12); }
            catch { return "wts-anon"; }
        }

        private static float SafeNow()
        {
            try { return Time.realtimeSinceStartup; } catch { return 0f; }
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Hidden MonoBehaviour that runs the actual UnityWebRequest POST coroutine for
    /// <see cref="WebTraceSink"/> (a static sink owns no MonoBehaviour). Fire-and-forget;
    /// on failure the batch falls back to the Unity log so data is never silently lost.
    /// </summary>
    internal sealed class WebTraceSinkDriver : MonoBehaviour
    {
        public void Send(string url, byte[] body, string sessionId, string buildId, List<string> batch)
        {
            try { StartCoroutine(PostRoutine(url, body, sessionId, buildId, batch)); }
            catch (System.Exception e) { Debug.LogWarning("[WebTraceSink] send skipped: " + e.Message); }
        }

        private System.Collections.IEnumerator PostRoutine(
            string url, byte[] body, string sessionId, string buildId, List<string> batch)
        {
            UnityWebRequest req = null;
            try
            {
                req = new UnityWebRequest(url, "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(body),
                    downloadHandler = new DownloadHandlerBuffer(),
                };
                req.SetRequestHeader("Content-Type", "application/json");
                // Anonymous, non-PII identifiers so the endpoint can authorise/rate-limit.
                // NO secret here — the connstring stays server-side (WO-429).
                req.SetRequestHeader("X-Trace-Session", sessionId);
                req.SetRequestHeader("X-Trace-Build", buildId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WebTraceSink] build request failed: " + e.Message);
                for (int i = 0; i < batch.Count; i++) Debug.Log(batch[i]);   // don't lose data
                yield break;
            }

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Failed POST → fall back to the Unity log (never a silent loss), throttled note.
                Debug.LogWarning($"[WebTraceSink] POST failed ({req.responseCode}): {req.error} — falling back to log.");
                for (int i = 0; i < batch.Count; i++) Debug.Log(batch[i]);
            }

            try { req.Dispose(); } catch { /* never throw on cleanup */ }
        }
    }
#endif
}
