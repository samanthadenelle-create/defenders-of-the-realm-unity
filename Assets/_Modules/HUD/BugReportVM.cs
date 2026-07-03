// =============================================================================
// BugReportVM — WO-596: the player bug-report view-model (MVVM strict).
// -----------------------------------------------------------------------------
// Pure-C# state + commands for the player-facing bug-report form. The View
// (BugReportView) binds and renders ONLY — all state, the payload build, the
// salted-uid hashing, and the POST live here.
//
// Owner-ratified design (WO-596):
//   * The SUBMIT BUTTON IS THE CONSENT — no extra "send?" dialog, no silent
//     collection; the form shows what will be sent.
//   * Auto-attached on submit: recent FlowTrace tail, scene, session id, app
//     version, platform, Pi uid IF signed in — as a SALTED HASH (a raw uid
//     never leaves the client in a bug report; username is never sent).
//   * Screenshot is untickable (default ON) and already privacy-scrubbed at
//     capture time (PrivacySensitiveUi hid identity UI for the frame).
//
// Transport: POST to the LIVE -v2 project's api/bug-report (sits beside
// api/trace — same host WebTrace uses). WebGL-safe: UnityWebRequest coroutine,
// no threads. On failure (non-WebGL) the report is saved locally under
// persistentDataPath/BugReports as the offline fallback (send-on-boot = slice 2).
// =============================================================================
using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    /// <summary>WO-596 — state + commands for the player bug-report form.
    /// The View binds <see cref="Changed"/> and renders; it never builds payloads.</summary>
    public sealed class BugReportVM
    {
        // Same host as WebTrace.TraceEndpoint — the LIVE -v2 Vercel project. Carries
        // no secret (the Neon connstring stays server-side, per WO-429).
        private const string Endpoint = "https://defenders-of-the-realm-v2.vercel.app/api/bug-report";
        private const int NoteMaxChars = 1000;
        // Client-side salt for the uid hash: the server can correlate repeat reporters,
        // but a raw Pi uid never rides a bug report (owner privacy directive 2026-07-02).
        private const string UidSalt = "eoa-bugreport-v1|";

        // One anonymous id per app session (mirrors WebTrace.MakeSessionId — no PII).
        private static readonly string s_sessionId =
            Guard.Try("BugReport", "session id",
                () => "br-" + Guid.NewGuid().ToString("N").Substring(0, 12), fallback: "br-anon");

        public enum Stage { Capturing, Ready, Sending, Sent, Failed }

        public Stage  State { get; private set; } = Stage.Capturing;
        public string Note  { get; private set; } = "";
        public bool   IncludeScreenshot { get; private set; } = true;
        public byte[] ScreenshotJpg { get; private set; }
        public string[] TraceTail { get; private set; } = Array.Empty<string>();
        public string SceneName { get; private set; } = "?";
        /// <summary>Set on a failed POST — the View surfaces it as the failure toast.</summary>
        public string LastError { get; private set; }

        /// <summary>Raised on every state change; the View repaints from it.</summary>
        public event Action Changed;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Bind the clean-frame capture result (screenshot bytes + trace tail).</summary>
        public void AttachCapture(BreakCaptureHarness.ReportCapture cap)
        {
            if (cap != null)
            {
                ScreenshotJpg = cap.ScreenshotJpg;
                TraceTail     = cap.TraceTail ?? Array.Empty<string>();
                SceneName     = string.IsNullOrEmpty(cap.Scene) ? "?" : cap.Scene;
            }
            if (State == Stage.Capturing) State = Stage.Ready;
            Raise();
        }

        public void SetNote(string note)
        {
            Note = note ?? "";
            if (Note.Length > NoteMaxChars) Note = Note.Substring(0, NoteMaxChars);
            // No Raise() — the input field is the source of this value; repainting
            // mid-keystroke would fight the caret.
        }

        /// <summary>The untickable "include screenshot" toggle (default ON).</summary>
        public void ToggleScreenshot()
        {
            IncludeScreenshot = !IncludeScreenshot;
            Raise();
        }

        public bool CanSubmit => State == Stage.Ready || State == Stage.Failed;

        /// <summary>
        /// Submit the report (the button IS the consent). Coroutine — run via the View's
        /// StartCoroutine. Transitions Sending → Sent | Failed and raises Changed at each.
        /// </summary>
        public System.Collections.IEnumerator Submit()
        {
            if (!CanSubmit) yield break;
            State = Stage.Sending;
            LastError = null;
            Raise();

            byte[] body = Guard.Try("BugReport", "build payload", BuildPayload, fallback: null);
            bool withShot = IncludeScreenshot && ScreenshotJpg != null;
            FlowTrace.Step("BugReport",
                $"submit — note={Note.Length}ch tail={TraceTail.Length} screenshot={(withShot ? ScreenshotJpg.Length + "B" : "none")} " +
                $"scene='{SceneName}' session={s_sessionId} piHash={(PiUidHash() != null ? "yes" : "no")}");

            if (body == null)
            {
                State = Stage.Failed;
                LastError = "payload build failed";
                FlowTrace.Fail("BugReport", "submit aborted — payload build failed");
                Raise();
                yield break;
            }

            using (var req = new UnityWebRequest(Endpoint, "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    State = Stage.Sent;
                    FlowTrace.Step("BugReport", $"result — SENT ({req.responseCode}): {req.downloadHandler.text}");
                }
                else
                {
                    State = Stage.Failed;
                    LastError = $"{req.responseCode} {req.error}";
                    FlowTrace.Warn("BugReport", $"result — POST FAILED ({req.responseCode}): {req.error}");
                    SaveLocalFallback(body);
                }
            }
            Raise();
        }

        // ── Payload ─────────────────────────────────────────────────────────────
        // Endpoint contract (api/bug-report.js):
        //   { note, sceneName, sessionId, version, platform,
        //     piUid?          (SALTED SHA-256 HASH, never the raw uid),
        //     traceTail[]     (recent [Flow:*]/error lines, oldest first),
        //     screenshotB64?  (JPEG ≤ 300KB, base64; omitted when toggled off/absent) }

        private byte[] BuildPayload()
        {
            var sb = new StringBuilder(1024 + (IncludeScreenshot && ScreenshotJpg != null ? (ScreenshotJpg.Length * 4) / 3 : 0));
            sb.Append("{\"note\":").Append(Q(Note))
              .Append(",\"sceneName\":").Append(Q(SceneName))
              .Append(",\"sessionId\":").Append(Q(s_sessionId))
              .Append(",\"version\":").Append(Q(Application.version ?? "unknown"))
              .Append(",\"platform\":").Append(Q(Application.platform.ToString()));

            string piHash = PiUidHash();
            if (piHash != null)
                sb.Append(",\"piUid\":").Append(Q(piHash));

            sb.Append(",\"traceTail\":[");
            for (int i = 0; i < TraceTail.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Q(TraceTail[i]));
            }
            sb.Append(']');

            if (IncludeScreenshot && ScreenshotJpg != null)
                sb.Append(",\"screenshotB64\":").Append(Q(Convert.ToBase64String(ScreenshotJpg)));

            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>Salted SHA-256 hex of the signed-in Pi uid, or null when not signed in.
        /// READ-only view of PiSignInController state — this VM never touches sign-in.</summary>
        private static string PiUidHash()
        {
            return Guard.Try("BugReport", "pi uid hash", () =>
            {
                string uid = DeNelle.Core.Platform.PiSignInController.SignedInUid;
                if (string.IsNullOrEmpty(uid)) return null;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(UidSalt + uid));
                    var hex = new StringBuilder(h.Length * 2);
                    foreach (byte b in h) hex.Append(b.ToString("x2"));
                    return hex.ToString();
                }
            }, fallback: null);
        }

        // Offline fallback (WO-596: "local persistentDataPath/BugReports copy may stay").
        // Standalone/editor only — the WebGL sandbox filesystem is unreliable.
        private void SaveLocalFallback(byte[] body)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            Guard.Try("BugReport", "local fallback save", () =>
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "BugReports");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                System.IO.File.WriteAllBytes(path, body);
                FlowTrace.Step("BugReport", $"POST failed — report saved locally: {path}");
            });
        }

        /// <summary>Minimal JSON string quote/escape (same shape as WebTrace.Q).</summary>
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

        private void Raise()
        {
            try { Changed?.Invoke(); }
            catch (Exception e) { FlowTrace.Fail("BugReport", $"Changed handler threw: {e.Message}"); }
        }
    }
}
