using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1211: boot reads never sign; writes use the shared auth authority.</summary>
    public static class BackendSaveAuthRegression
    {
        private const string StatePath = "Assets/_Modules/Core/State/GameStateService.cs";
        private const string SignerPath = "Assets/_Modules/Core/Web3/BackendRequestSigner.cs";
        private const string BootstrapPath = "Assets/_Modules/Wallet/WalletSkinBootstrap.cs";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BACKEND_SAVE_AUTH_OK - " + reason);
            else Debug.LogError("BACKEND_SAVE_AUTH_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                string state = Strip(File.ReadAllText(StatePath));
                string signer = Strip(File.ReadAllText(SignerPath));
                string load = Method(state, "LoadFromBackend");
                string save = Method(state, "SendCurrentSnapshot");
                string mint = Method(signer, "MintSessionForExplicitConnectAsync");
                string cached = Method(signer, "TryAttachCachedSession");

                Require(failures, load, "TryAttachCachedSession", "boot load no longer uses cached-only proof");
                Require(failures, load, "BackendRequestSigner.IsGuestIdentity", "boot load no longer routes guests through shared proof when enforcement is off");
                Reject(failures, load, "TryAttachAsync", "boot load may mint/sign again");
                Reject(failures, load, "SignMessageBase58", "boot load directly signs again");

                Require(failures, save, "BackendRequestSigner.TryAttachAsync", "save write bypasses shared auth");
                Require(failures, save, "BackendRequestSigner.IsGuestIdentity", "save no longer routes guests through shared proof when enforcement is off");
                if (!Regex.IsMatch(save, @"!await\s+DeNelle\.Core\.Web3\.BackendRequestSigner\.TryAttachAsync\s*\([^;]+?\)\s*\)\s*\{[^{}]*?return\s+false\s*;", RegexOptions.Singleline))
                    failures.Add("failed shared save auth is not structurally bound to refusal/requeue");

                // ⛔ WO-1211's "BOOT NEVER SIGNS" PIN IS RETIRED — OWNER RULING 2026-09-06 (WO-1441).
                // This block used to Reject MintSessionAsync/SignMessageBase58 inside
                // WarmUpSessionAsync, and Require the literal "first authenticated action will mint".
                // All three are gone, deliberately:
                //   * The RULE was reversed. Auto-resume now mints one handshake at boot, because it
                //     shows no connect prompt of its own, so that is ONE sheet per session - under
                //     the owner's stated shape, not over it. WO-1211 assumed it would be an EXTRA
                //     prompt; on this path it never was.
                //   * The METHOD is gone. Both connect paths mint now, so WarmUpSessionAsync had no
                //     callers and was deleted (an uncalled method is what caused this outage).
                //   * The PROSE PIN was actively harmful. Requiring "first authenticated action will
                //     mint" guarded a FALSE trace against correction for ten days - the sentence
                //     stopped being true at the WO-1157 fail-bounce - while every wallet holder's
                //     cloud save failed silently. An oracle that pins prose pins whatever the prose
                //     says, true or not. Pin BEHAVIOUR.
                // What replaces them is below: the mint must exist, and it must be REACHED.
                Require(failures, mint, "MintSessionAsync",
                    "the explicit-connect entry no longer mints - it is the only thing that creates a " +
                    "session for a player who never buys a pack (WO-1441)");

                // ⛔ THE PIN THAT WOULD HAVE CAUGHT THIS BUG IN ONE RUN. MintSessionForExplicitConnectAsync
                // existed with ZERO CALL SITES from the day it was written, which is exactly why no
                // session was ever created outside a purchase and cloud save was dark for every wallet
                // holder. A method whose absence is SILENT must have its CALL SITE pinned, never just
                // its body - a body-only oracle passes perfectly on code nothing runs.
                string bootstrap = Strip(File.ReadAllText(BootstrapPath));
                if (Regex.Matches(bootstrap, @"BackendRequestSigner\.MintSessionForExplicitConnectAsync").Count < 2)
                    failures.Add("the session mint is not wired on BOTH connect paths (corner button + " +
                                 "login/auto-resume) - a wallet holder who never buys a pack gets no " +
                                 "session and every cloud save refuses fail-closed (WO-1441)");
                Reject(failures, bootstrap, "WarmUpSessionAsync",
                    "the deleted no-op warm-up is back on a connect path - it does not mint, so the " +
                    "path that adopts it silently loses cloud save (WO-1441)");

                // ⛔ WO-1454 — A TRANSIENT 5xx MUST NOT DESTROY A STILL-VALID SESSION. The renewal
                // used to call ClearSession() on ANY non-Success result, so one 500/503/timeout
                // permanently darkened cloud save (save passes allowMint:false; nothing re-mints).
                // RED PROOF: against the pre-fix body this Reject regex matches
                // `Result.Success) { ... ClearSession` directly and the Require for the classifier
                // is absent, so both fire.
                string renew = Method(signer, "TryRenewSessionAsync");
                Require(failures, renew, "IsCredentialRefusal",
                    "session renewal no longer classifies the status before clearing - a transient 5xx " +
                    "destroys a still-valid token and cloud save goes dark permanently (WO-1454)");
                if (Regex.IsMatch(renew, @"Result\.Success\s*\)\s*\{[^{}]*?ClearSession", RegexOptions.Singleline))
                    failures.Add("session renewal clears the session on a bare non-Success result again - " +
                                 "only 401/403 may clear (WO-1454)");
                string refusal = Method(signer, "IsCredentialRefusal");
                Require(failures, refusal, "401", "the credential-refusal classifier no longer treats 401 as a refusal (WO-1454)");
                Require(failures, refusal, "403", "the credential-refusal classifier no longer treats 403 as a refusal (WO-1454)");
                if (Regex.IsMatch(refusal, @"\b5\d\d\b"))
                    failures.Add("the credential-refusal classifier admits a 5xx - api/auth/session.js returns 500 " +
                                 "when the renewal query throws (a deployment state, not a verdict on the player) " +
                                 "and clearing on it is the WO-1454 outage");

                // ⛔ WO-1455 — THE DEPTH WARNING MUST LATCH ON THE CROSSING, AND THE QUEUE MUST BE
                // BOUNDED. The old `Count % OfflineQueueDepthWarn == 0` test fires only when an
                // enqueue lands on an exact multiple; a live session reached depth 112 in silence.
                // RED PROOF: the pre-fix body contains the modulo and neither the latch nor the
                // coalescer, so all three checks fire.
                string enqueue = Method(state, "EnqueueOffline");
                if (Regex.IsMatch(enqueue, @"%\s*OfflineQueueDepthWarn"))
                    failures.Add("the offline-queue depth warning is back on an exact-multiple test - it is " +
                                 "structurally skippable and missed a depth of 112 in a live session (WO-1455)");
                Require(failures, enqueue, "_offlineQueueDepthWarned",
                    "the offline-queue depth warning no longer latches per crossing (WO-1455)");
                Require(failures, enqueue, "CoalesceOfflineQueue",
                    "the offline queue is unbounded again - it must coalesce to OfflineQueueMaxDepth, keeping " +
                    "the NEWEST marker per identity (WO-1455)");
                string coalesce = Method(state, "CoalesceOfflineQueue");
                Require(failures, coalesce, "OfflineQueueMaxDepth", "the coalescer enforces no cap (WO-1455)");
                Require(failures, coalesce, "FlowTrace.Warn", "an offline-queue drop is silent again (WO-1455)");

                Require(failures, cached, "X-Guest-Id", "guest proof was dropped from cached-only reads");
                Require(failures, cached, "SessionUsable", "wallet cached proof does not validate expiry/wallet");
                Require(failures, cached, "X-Session", "cached wallet session header is missing");
                Require(failures, cached, "X-Wallet", "cached session is no longer paired with wallet identity");

                Reject(failures, state, "TryAttachAuthHeaders", "GameStateService still owns a second auth rail");
                Reject(failures, state, "FetchNonce(", "GameStateService still owns nonce fetching");
                Reject(failures, state, "SignMessageBase58", "GameStateService still owns wallet signing");
                Reject(failures, state, "dotr-save:v1:", "GameStateService still constructs auth messages");
            }
            catch (Exception ex) { failures.Add("oracle threw " + ex.GetType().Name + ": " + ex.Message); }

            reason = failures.Count == 0
                ? "boot uses cached-only proof, guests retain headers, writes use the sole shared signer"
                : string.Join(" | ", failures.ToArray());
            return failures.Count == 0;
        }

        /// <summary>
        /// Removes comments so a lint cannot be satisfied — or tripped — by prose.
        /// <para>
        /// ⛔ LINE COMMENTS FIRST, BLOCK COMMENTS SECOND. THE ORDER IS THE WHOLE POINT (WO-1441).
        /// This ran block-first, and on 2026-09-06 a single <c>//</c> comment mentioning a URL path
        /// that ended in a star opened a FALSE block comment which ran to the next real
        /// <c>*/</c> hundreds of lines away. It silently deleted <b>73% of BackendRequestSigner.cs
        /// (29,611 of 40,342 chars)</b>, and the suite then reported
        /// <c>method not found: MintSessionForExplicitConnectAsync</c> plus "the renewal is gone" —
        /// for code that was present and correct the whole time. A stripper that eats the file
        /// reports MISSING FUNCTIONALITY, which is the most expensive wrong answer an oracle can
        /// give: it sends someone to fix working code.
        /// </para>
        /// <para>
        /// ⚠ STILL NOT A LEXER, AND IT DOES NOT PRETEND TO BE. A star-slash sequence inside a STRING
        /// LITERAL will still open a false block comment — order cannot fix that. The mitigation is
        /// the rule that these oracles' inputs avoid such literals; if that ever stops being enough,
        /// replace this with a real tokenizer rather than adding another regex. When an oracle here
        /// says a method is missing, CHECK THE FILE before believing it.
        /// </para>
        /// </summary>
        private static string Strip(string source)
        {
            source = Regex.Replace(source, @"//[^\r\n]*", "");
            return Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
        }

        private static string Method(string source, string name)
        {
            var declaration = Regex.Match(source,
                @"\b(?:public|private|internal|protected)\s+(?:static\s+)?(?:async\s+)?[\w<>]+\s+" +
                Regex.Escape(name) + @"\s*\(");
            if (!declaration.Success) throw new InvalidOperationException("method not found: " + name);
            int nameAt = declaration.Index;
            int open = source.IndexOf('{', nameAt);
            if (open < 0) throw new InvalidOperationException("method body not found: " + name);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            throw new InvalidOperationException("unterminated method: " + name);
        }

        private static void Require(List<string> failures, string source, string token, string message)
        { if (!source.Contains(token)) failures.Add(message); }

        private static void Reject(List<string> failures, string source, string token, string message)
        { if (source.Contains(token)) failures.Add(message); }
    }
}
