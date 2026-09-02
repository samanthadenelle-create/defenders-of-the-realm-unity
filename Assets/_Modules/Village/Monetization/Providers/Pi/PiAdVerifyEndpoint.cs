// =============================================================================
// PiAdVerifyEndpoint — WO-1320. The client half of the rewarded-ad verification.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village.PiAds   Namespace: DeNelle.Village.Monetization
//
// ONE CALL, and only one:
//   POST /api/pi/ads-verify   { adId }   ->  200 { success, granted, code }
//
// ⛔ THE HOST IS ABSOLUTE ON PURPOSE - do NOT "fix" it to a relative path. Under Pi the
//    app is served through Pi's proxy at <app>.pinet.com, so a relative "/api/..." would
//    POST to the PROXY, not to Vercel, and would never reach our backend. Same literal
//    and same reasoning as PiPaymentEndpoints.BackendBase and PiSignInController.VerifyUrl.
//
// ⛔ NO API KEY IS EVER SENT FROM HERE. PI_NETWORK_API_KEY authorises the server-to-server
//    call to api.minepi.com and exists ONLY in the Vercel environment. If a future edit
//    needs a key on this side, the design is wrong.
//
// ⛔ EVERY FAILURE IS A REFUSAL. Unparseable body, transport error, non-200, `success:false`
//    - all of them return granted:false. There is deliberately no "assume granted on a
//    network blip" branch: the whole reason this endpoint exists is that the client is not
//    trusted to assert its own reward, and a client that grants when it cannot reach the
//    verifier has simply reinstated the untrusted path with extra steps.
//
// This runs on a PHONE inside Pi Browser with no debugger attachable, so the trace lines
// are the only evidence that will ever exist. They are the feature, not decoration
// (CLAUDE.md sec.12).
// =============================================================================

using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Monetization
{
    /// <summary>The backend's verdict on one rewarded ad. <see cref="Granted"/> is the only grant key.</summary>
    public readonly struct PiAdVerifyResult
    {
        /// <summary>TRUE only when the server confirmed mediator_ack_status == "granted".</summary>
        public readonly bool Granted;

        /// <summary>Short machine code for the trace (PI_ADS_GRANTED, PI_ADS_ACK_PENDING, ...).</summary>
        public readonly string Code;

        public PiAdVerifyResult(bool granted, string code)
        {
            Granted = granted;
            Code = string.IsNullOrEmpty(code) ? "UNKNOWN" : code;
        }

        public static PiAdVerifyResult Refused(string code) => new PiAdVerifyResult(false, code);
    }

    internal static class PiAdVerifyEndpoint
    {
        internal const string TraceSystem = "PiAds";

        // See the header: absolute by design.
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";
        private const string VerifyUrl = BackendBase + "/api/pi/ads-verify";

        // Short by intent. The server already spends up to ~3s on its own bounded ack retry, and
        // the player is sitting on a spinner after an ad they just watched. Past this we refuse
        // and say so rather than hold the button open.
        private const int TimeoutSeconds = 20;

        /// <summary>
        /// Ask the backend whether Pi actually granted this ad. Never throws; every failure path
        /// answers granted:false with a code that says which one it was.
        /// </summary>
        internal static async UniTask<PiAdVerifyResult> VerifyAsync(string adId)
        {
            if (string.IsNullOrEmpty(adId))
            {
                FlowTrace.Warn(TraceSystem, "PI_ADS_VERIFY_SKIPPED reason=no-adid - nothing to verify, so nothing is granted.");
                return PiAdVerifyResult.Refused("ADID_MISSING");
            }

            FlowTrace.Step(TraceSystem, $"PI_ADS_VERIFY_POST adId={PiAdGrantDecision.Mask(adId)}");

            string body = "{\"adId\":" + Json(adId) + "}";
            byte[] raw = Encoding.UTF8.GetBytes(body);

            long status = 0;
            string text = null;

            using (var req = new UnityWebRequest(VerifyUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(raw),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            })
            {
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Accept", "application/json");

                // No silent failures (CLAUDE.md sec.12): a transport throw is LOGGED, then the
                // empty body falls through to the refusal below.
                try { await req.SendWebRequest(); }
                catch (Exception ex)
                {
                    FlowTrace.Warn(TraceSystem,
                        $"PI_ADS_VERIFY_TRANSPORT {ex.GetType().Name} (HTTP {req.responseCode}) - {ex.Message}");
                }

                status = req.responseCode;
                text = req.downloadHandler != null ? req.downloadHandler.text : null;
            }

            if (string.IsNullOrEmpty(text))
            {
                FlowTrace.Warn(TraceSystem,
                    $"PI_ADS_VERIFY_RESULT granted=False code=NO_BODY http={status} - refusing the reward. " +
                    "An unreachable verifier is a refusal, never an assumed grant.");
                return PiAdVerifyResult.Refused("NO_BODY");
            }

            VerifyWire wire = null;
            try { wire = JsonUtility.FromJson<VerifyWire>(text); }
            catch (Exception e)
            {
                FlowTrace.Warn(TraceSystem,
                    $"PI_ADS_VERIFY_RESULT granted=False code=BAD_JSON http={status} ({e.GetType().Name}).");
                return PiAdVerifyResult.Refused("BAD_JSON");
            }

            // ⛔ BOTH the HTTP status AND the body's `granted` must agree. Unlike the payment
            // rail - where an absent `ok` had to be tolerated because a 200 alone settles the
            // money - here the body is the ONLY carrier of the verdict, so an absent or false
            // `granted` correctly means "no". Fail closed is the right default in this direction.
            bool httpOk = status >= 200 && status < 300;
            bool granted = httpOk && wire != null && wire.granted;
            string code = wire != null && !string.IsNullOrEmpty(wire.code) ? wire.code : "HTTP_" + status;

            if (granted)
                FlowTrace.Step(TraceSystem, $"PI_ADS_VERIFY_RESULT granted=True code={code} http={status}");
            else
                FlowTrace.Warn(TraceSystem,
                    $"PI_ADS_VERIFY_RESULT granted=False code={code} http={status} - no reward will be given.");

            return new PiAdVerifyResult(granted, code);
        }

        /// <summary>Minimal JSON string literal. Escapes the two characters that can break a body.</summary>
        private static string Json(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        [Serializable] private class VerifyWire
        {
            public bool success;
            public bool granted;
            public string code;
        }
    }
}
