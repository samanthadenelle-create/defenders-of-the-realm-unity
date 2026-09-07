// =============================================================================
// TargetedLocalAssociationScenario — MWA local association that can TARGET a
// specific wallet app (owner ruling 2026-08-05: "Seeker should use seeker
// wallet ... make sure that's the primary before trying to use another one").
// -----------------------------------------------------------------------------
// WHY THIS EXISTS
// The Solana Unity SDK's own LocalAssociationScenario builds an IMPLICIT intent
// (ACTION_VIEW + BROWSABLE + solana-wallet:/v1/associate/local?...). Android
// then picks the winner among every installed handler — on the owner's Seeker
// that winner is Jupiter, and the Seeker's own wallet is never offered. The
// official MWA clientlib exposes NO wallet-targeting API and the SDK's
// LocalAssociationIntentCreator has NO seam, so the single missing line
//
//     intent.Call<AndroidJavaObject>("setPackage", pkg);
//
// cannot be injected — hence this clone.
//
// SAFE BY CONSTRUCTION: Intent.setPackage() narrows *delivery* only. The action,
// category, and data URI are byte-identical to the SDK's, it is still launched
// with startActivityForResult (so the wallet's getCallingPackage() still
// resolves to us, which the MWA identity check depends on), and the websocket
// association is unchanged. Nothing the protocol depends on moves.
//
// If NO preferred wallet is installed we fall through to the implicit intent —
// today's behaviour plus the system chooser. A missing preferred wallet is
// NEVER a hard failure.
//
// ── DIFFERENCES FROM THE SDK CLONE (deliberate) ──────────────────────────────
//  1. queryIntentActivities() enumerates the installed MWA handlers and a
//     PREFERENCE CHAIN (data, not logic — see PreferredWalletPackages) selects
//     the target package.
//  2. setPackage() before startActivityForResult.
//  3. FlowTrace at every step (project §12 — instrument, don't guess).
//  4. The SDK's action QUEUE + Newtonsoft `Response<object>` round-trip is
//     dropped. MobileWalletAdapterClient.Receive() already resolves the pending
//     request Task (result on success, SetException on a JSON-RPC error), so
//     re-deserializing the envelope here bought nothing but a Newtonsoft
//     dependency. This class exposes the two operations we actually use.
//
//     >> AND IT SIDESTEPS A REAL SDK BUG: "dequeue after close". <<
//     LocalAssociationScenario.cs:132-138 (pinned v1.2.9):
//
//         private void ExecuteNextAction(Response<object> response = null)
//         {
//             if (_actions.Count == 0 || response is { Failed: true })
//                 CloseAssociation(response);     // no `return`, no `else`
//             var action = _actions.Dequeue();    // executes REGARDLESS
//             action.Invoke(_client);
//         }
//
//     On the final response the queue is empty, CloseAssociation() is entered,
//     and control falls through into Dequeue() on an empty Queue<T> ->
//     InvalidOperationException. It is thrown from inside the websocket message
//     callback (HandleEncryptedSessionPayload, :98-110) which has NO try/catch,
//     so it tears down the message pump. Same fall-through on the `Failed` branch:
//     a wallet-side error both closes the association AND dequeues.
//     The damage lands hardest on the MULTI-action flows - SignMessage and
//     SignAllTransactions (SolanaMobileWalletAdapter.cs:145-187 / :95-135) - which
//     is why "connect looks fine, signing breaks later" is the reported shape.
//     We do NOT patch the vendored package (a Library/PackageCache edit is lost on
//     the next resolve). Having no queue means this control flow does not exist
//     here, and SolanaWalletProvider.SignMessageBase58 now routes through THIS
//     class unconditionally instead of Web3.Wallet, so the buggy pump is never
//     entered on any path we own.
//  5. A _closed latch on OnClose. The SDK reconnects the socket from its own
//     OnClose handler with no "we meant to close" guard — a latent respawn
//     after a completed association.
//
// ── REQUIRES THE <queries> BLOCK ─────────────────────────────────────────────
// queryIntentActivities + setPackage BOTH need Android 11+ package visibility:
// Assets/Plugins/Android/MobileWalletAdapter.androidlib/AndroidManifest.xml.
// That manifest was NOT reaching the APK until the AGP sourceSets fix in the
// sibling build.gradle (2026-08-05) — without it this class sees zero handlers
// and permanently falls back to the implicit intent.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using UnityEngine;

#if SOLANA_SDK
// All of the following are PUBLIC and in the GLOBAL namespace in the pinned SDK
// (magicblock-labs/Solana.Unity-SDK v1.2.9) — verified at source 2026-08-05:
//   MobileWalletAdapterSession, MobileWalletAdapterWebSocket,
//   MobileWalletAdapterClient, IAdapterOperations, AuthorizationResult,
//   SignedResult, WebSocketsTransportContract, AssociationContract,
//   LocalAssociationIntentCreator.
// Solana.Unity.SDK carries IMessageSender / Response<T>; NativeWebSocket
// carries IWebSocket + the static WebSocket.Create factory.
using NativeWebSocket;
using WebSocket = NativeWebSocket.WebSocket;
using WebSocketState = NativeWebSocket.WebSocketState;
#endif

namespace DeNelle.Wallet
{
    /// <summary>
    /// A Mobile Wallet Adapter local-association scenario that prefers a
    /// specific wallet package, falling back to the implicit intent when none of
    /// the preferred wallets is installed.
    /// </summary>
    public sealed class TargetedLocalAssociationScenario
    {
        /// <summary>
        /// The wallet preference chain, HIGHEST first. DATA, not logic — reorder
        /// this array to change which wallet wins; no control flow to review.
        /// <para>
        /// Rank 1 is the owner's binding ruling (2026-08-05): on a Seeker, the
        /// Seeker's own wallet (Seed Vault-backed) is the primary and must be
        /// tried before anything else. Jupiter is LAST on purpose — it is the
        /// incumbent that Android was silently electing, and the one that
        /// rejected us with ERROR_AUTHORIZATION_FAILED all night.
        /// </para>
        /// <para>
        /// NOTE we do NOT import the Seed Vault SDK. Solana Mobile's own
        /// guidance: "If you are building a mobile dApp, you should just use
        /// Mobile Wallet Adapter." The Seeker wallet IS an MWA wallet fronting
        /// Seed Vault, so it is reached through this exact association.
        /// </para>
        /// </summary>
        public static readonly string[] PreferredWalletPackages =
        {
            "com.solanamobile.wallet",         // Seeker native wallet / Seed Vault — RANK 1 (owner ruling)
            "app.phantom",                     // Phantom
            "com.solflare.mobile",             // Solflare
            "app.backpack.mobile.standalone",  // Backpack
            "ag.jup.jupiter.android",          // Jupiter — LAST: the incumbent that rejected us
        };

        /// <summary>
        /// UTC of the last time a wallet retired its one-shot association endpoint on us, or
        /// <see cref="DateTime.MinValue"/> if it has never happened this process.
        /// <para>
        /// WO-1420 item 2. STATIC because the reader is <c>WalletService.Connect</c>'s exception
        /// handler, which has no reference to the scenario instance the transport built for that
        /// attempt, and because the fact is about the SESSION ("a wallet refused us just now"), not
        /// about one object. Written on a WebSocket callback thread (15249 in capture seq 4683) and
        /// read on the main thread; a stale or torn read only ever costs one imprecise word in a log
        /// line, so no lock is warranted — but do NOT grow this into behaviour state, where that
        /// would stop being true.
        /// </para>
        /// <para>
        /// ⚠ DELIBERATELY OUTSIDE <c>#if SOLANA_SDK</c>, unlike every other member below. The reader
        /// is compiled on every target, so guarding this would break the editor and desktop builds;
        /// only the WRITER needs the SDK. Without it the value stays MinValue and the correlation
        /// note simply never appears, which is correct — there is no MWA association to close.
        /// </para>
        /// <para>
        /// ⚠ CORRELATION, NOT CAUSATION. A close inside the attempt's window means the wallet closed
        /// the endpoint during this connect; it does not prove that is why the connect failed. The
        /// trace is worded "a wallet closed ... during this attempt" for exactly that reason.
        /// </para>
        /// </summary>
        public static DateTime LastAssociationCloseUtc { get; private set; } = DateTime.MinValue;

        /// <summary>Records an endpoint retirement. Internal: only the association transport may
        /// assert this fact, and only from its own OnClose callback.</summary>
        internal static void NoteAssociationClosed() => LastAssociationCloseUtc = DateTime.UtcNow;

        /// <summary>
        /// Ceiling on "wallet activity launched -> signed transaction back" (WO-1579). This is the
        /// TIMEOUT POLICY; <see cref="DeNelle.Core.UI.WorldHold.StuckHoldSeconds"/> is a LAST RESORT
        /// and its own header says so, so the two must never be equal.
        ///
        /// <para>⚠ JUDGEMENT CALL, not a derived number, and recorded as one. 90s is chosen to sit
        /// strictly BELOW the 180s hold ceiling with the whole second half of that budget left as
        /// headroom, so this policy always fires first and the watchdog stays the backstop it is
        /// documented to be - if both fired in the same window a legitimate slow approval would be
        /// reported as a stuck hold. It is far above the 9s association handshake
        /// (<c>clientTimeoutSeconds</c>) because THIS window contains a human tapping Approve in
        /// another app, not a machine handshake. Raise or lower it with the hold ceiling in view,
        /// never on its own.</para>
        /// </summary>
        public const float SignTimeoutSeconds = 90f;

        /// <summary>
        /// The one PLAYER-FACING sentence for a sign-leg timeout. Curated, not an exception message:
        /// StoreCommerceStateRegression pins that raw exception text never reaches the money screen,
        /// and this is the seam PackStore matches on to say "nothing was charged" truthfully. It can
        /// say that because a timeout here happens strictly BEFORE any submission
        /// (SolanaWalletProvider.SubmitSignedTransaction needs a signed payload that never arrived),
        /// which no other failure on that path can claim.
        /// </summary>
        public const string SignTimeoutMessage =
            "Your wallet did not return a signed transaction in time. Nothing was charged. Try again when ready.";

        private static readonly TimeSpan SignTimeout = TimeSpan.FromSeconds(SignTimeoutSeconds);

#if SOLANA_SDK
        // ── Association state ────────────────────────────────────────────────
        private readonly TimeSpan _clientTimeout;
        private readonly MobileWalletAdapterSession _session;
        private readonly int _port;
        private readonly IWebSocket _webSocket;
        private readonly AndroidJavaObject _currentActivity;

        private MobileWalletAdapterClient _client;
        private TaskCompletionSource<MobileWalletAdapterClient> _clientReady;
        private bool _didConnect;

        /// <summary>
        /// WO-913: the project ships runInBackground=0, so the wallet launch freezes
        /// the Unity main thread mid-handshake. We force it ON for the association and
        /// put this value back in CloseAssociation.
        /// </summary>
        private bool _runInBackgroundToRestore;
        private bool _closed;

        /// <summary>
        /// Opens the local websocket endpoint the wallet will dial back into.
        /// Mirrors the SDK's constructor: random high port, fresh ECDH session,
        /// HELLO_REQ on open, key exchange on the first frame.
        /// </summary>
        /// <param name="clientTimeoutSeconds">
        /// Ceiling on "activity launched -> encrypted channel established".
        /// The SDK's default is 9 (its parameter is misnamed `clientTimeoutMs`
        /// but is fed to TimeSpan.FromSeconds).
        /// </param>
        public TargetedLocalAssociationScenario(int clientTimeoutSeconds = 9)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            _clientTimeout = TimeSpan.FromSeconds(clientTimeoutSeconds);
            _port = UnityEngine.Random.Range(
                WebSocketsTransportContract.WebsocketsLocalPortMin,
                WebSocketsTransportContract.WebsocketsLocalPortMax + 1);
            _session = new MobileWalletAdapterSession();

            var webSocketUri =
                WebSocketsTransportContract.WebsocketsLocalScheme + "://" +
                WebSocketsTransportContract.WebsocketsLocalHost + ":" + _port +
                WebSocketsTransportContract.WebsocketsLocalPath;

            _webSocket = WebSocket.Create(webSocketUri, WebSocketsTransportContract.WebsocketsProtocol);

            _webSocket.OnOpen += () =>
            {
                if (_didConnect) return;
                _didConnect = true;
                FlowTrace.Step("Wallet", $"MWA socket open on port {_port} - sending HELLO_REQ.");
                _webSocket.Send(_session.CreateHelloReq());
            };
            _webSocket.OnClose += _ =>
            {
                // A local-association wallet owns a one-shot loopback endpoint. Once it
                // closes that endpoint, reconnecting to the same port can never resume the
                // request. NativeWebSocket invokes OnClose again for each refused reconnect,
                // producing an unbounded ThreadPoolWorkQueue.Dispatch loop on IL2CPP.
                // Initial connection retries already belong to TryConnectWs; after an open,
                // let the pending MWA request complete/fail and let the caller decide whether
                // to begin a fresh association.
                if (!_closed)
                {
                    FlowTrace.Warn("Wallet", "MWA wallet closed its one-shot association endpoint; not reconnecting to the retired port.");
                    // WO-1420 item 2: record it so WalletService's catch can NAME the cause in the
                    // same line. This fires on a WebSocket callback thread (15249 in the capture)
                    // while the failure is reported on the main thread, so a triage previously had
                    // to correlate two threads by timestamp to learn why a connect died.
                    NoteAssociationClosed();
                }
            };
            _webSocket.OnError += e => FlowTrace.Warn("Wallet", $"MWA socket error: {e}");
            _webSocket.OnMessage += ReceivePublicKeyHandler;
        }

        // =====================================================================
        //  Public operations
        // =====================================================================

        /// <summary>
        /// Runs a full association and issues an MWA <c>authorize</c> request.
        /// Returns the wallet's <see cref="AuthorizationResult"/>, or throws
        /// (the JSON-RPC error message is surfaced verbatim by the SDK client).
        /// </summary>
        public async Task<AuthorizationResult> Authorize(
            string identityUri, string iconUri, string identityName, string cluster)
        {
            LogIdentity("authorize", identityUri, iconUri, identityName);

            var client = await StartAssociation();
            try
            {
                // HARD SDK CONSTRAINT (MobileWalletAdapterClient.cs:62-69):
                // identityUri MUST be absolute, iconUri MUST be relative — the
                // client throws ArgumentException otherwise, BEFORE any I/O.
                // WO-913 instrumentation: the 2026-08-06 device capture went silent
                // for 82s between "encrypted channel up" and the owner killing the
                // app -- with NO exception and NO result. These three lines split
                // that silence into request-never-sent / response-never-arrived.
                FlowTrace.Step("Wallet", "MWA client ready - sending authorize request.");
                var authResult = await client.Authorize(
                    new Uri(identityUri),
                    new Uri(iconUri, UriKind.Relative),
                    identityName,
                    cluster);
                FlowTrace.Step("Wallet", "MWA authorize response received from wallet.");
                return authResult;
            }
            finally
            {
                await CloseAssociation();
            }
        }

        /// <summary>
        /// Runs an association and issues an MWA <c>reauthorize</c> against a grant
        /// from a PREVIOUS session, so a returning player is reconnected without an
        /// approval sheet. Returns the wallet's <see cref="AuthorizationResult"/>,
        /// or throws when the wallet declines (revoked/expired token) - which the
        /// caller treats as an ordinary "authorize fresh instead", never a dead end.
        /// <para>
        /// The <paramref name="authToken"/> is a CAPABILITY GRANT and is never
        /// logged: <see cref="LogIdentity"/> prints only the method label and the
        /// dapp identity triplet. See MwaSessionStore for the storage rationale.
        /// </para>
        /// </summary>
        public async Task<AuthorizationResult> Reauthorize(
            string identityUri, string iconUri, string identityName, string authToken)
        {
            if (string.IsNullOrEmpty(authToken))
                throw new ArgumentException("reauthorize needs a stored auth token", nameof(authToken));

            LogIdentity("reauthorize", identityUri, iconUri, identityName);

            var client = await StartAssociation();
            try
            {
                FlowTrace.Step("Wallet", "MWA client ready - sending REAUTHORIZE request (silent resume).");
                var result = await client.Reauthorize(
                    new Uri(identityUri),
                    new Uri(iconUri, UriKind.Relative),
                    identityName,
                    authToken);
                FlowTrace.Step("Wallet", "MWA reauthorize response received from wallet.");
                return result;
            }
            finally
            {
                await CloseAssociation();
            }
        }

        /// <summary>
        /// Runs a full association, (re)authorizes, then asks the wallet to sign
        /// <paramref name="message"/> for <paramref name="addressBytes"/>.
        /// Used when <c>Web3.Wallet</c> is absent because we authorized through
        /// this scenario instead of the SDK's Web3 facade.
        /// </summary>
        public async Task<byte[]> SignMessage(
            string identityUri, string iconUri, string identityName, string cluster,
            string authToken, byte[] message, byte[] addressBytes)
        {
            LogIdentity(string.IsNullOrEmpty(authToken) ? "authorize+sign_messages" : "reauthorize+sign_messages",
                identityUri, iconUri, identityName);

            var client = await StartAssociation();
            try
            {
                if (string.IsNullOrEmpty(authToken))
                {
                    await client.Authorize(
                        new Uri(identityUri), new Uri(iconUri, UriKind.Relative), identityName, cluster);
                }
                else
                {
                    await client.Reauthorize(
                        new Uri(identityUri), new Uri(iconUri, UriKind.Relative), identityName, authToken);
                }

                var signed = await client.SignMessages(
                    messages: new List<byte[]> { message },
                    addresses: new List<byte[]> { addressBytes });

                // SignedPayloadsBytes is a List<byte[]> (verified at source) and
                // is NULL — not empty — when the wallet returned no payloads.
                if (signed == null || signed.SignedPayloadsBytes == null || signed.SignedPayloadsBytes.Count == 0)
                    return null;
                return signed.SignedPayloadsBytes[0];
            }
            finally
            {
                await CloseAssociation();
            }
        }

        /// <summary>
        /// Runs a targeted MWA association, restores the existing authorization grant, and asks
        /// that exact wallet to sign one serialized transaction. The game never receives a key;
        /// only the wallet-returned signed wire payload crosses this seam.
        /// </summary>
        public async Task<byte[]> SignTransaction(
            string identityUri, string iconUri, string identityName, string cluster,
            string authToken, byte[] serializedTransaction)
        {
            if (serializedTransaction == null || serializedTransaction.Length == 0)
                throw new ArgumentException("A serialized transaction is required.", nameof(serializedTransaction));

            LogIdentity(string.IsNullOrEmpty(authToken)
                    ? "authorize+sign_transactions"
                    : "reauthorize+sign_transactions",
                identityUri, iconUri, identityName);

            var client = await StartAssociation();
            try
            {
                // ⛔ WO-1579 - ONE DEADLINE OVER THE WHOLE AUTHORIZE+SIGN LEG. READ THIS BEFORE
                // REMOVING IT. Before today, StartAssociation above was bounded (_clientTimeout, 9s,
                // the Task.WhenAny at :~526) and everything BELOW it was not: Authorize/Reauthorize
                // and SignTransactions awaited a wallet app that may never answer, with no ceiling
                // at all. That is not a theoretical leak - PackStore.Purchase takes a WorldHold as
                // its first statement, so an unbounded await here freezes the world for its entire
                // duration, and the owner's Seeker (F8 seq 4690-4693, 2026-09-07) sat at
                // timeScale 0.00 for 7869 SECONDS before the watchdog force-released it, 19s BEFORE
                // this round trip finally returned.
                //
                // Task.Delay runs on the THREAD-POOL TIMER, which keeps counting while the Android
                // Activity is paused and our wallet sheet owns the screen. So the deadline is real
                // wall clock even though no Unity frame runs, and the continuation lands on the
                // first foreground frame - which is precisely when the player is back to be told.
                //
                // ⚠ AND THAT IS WHY THIS IS NOT THE ONLY THING THAT FIRES. The continuation needs
                // the main thread, so on a dwell longer than WorldHold.StuckHoldSeconds the world
                // hold's WALL-clock ceiling (WO-1579) is judged on the resume frame FIRST and this
                // deadline resolves a stage later. That order is correct - the world must not stay
                // frozen waiting on us - but it means a very long wallet dwell produces a stuck-hold
                // report BEFORE this timeout's own line. Read them as one event, not two defects.
                //
                // NOTHING IS SUBMITTED WHEN THIS FIRES. The transaction reaches the chain only via
                // SolanaWalletProvider.SubmitSignedTransaction, which cannot run without a signed
                // payload from below. So a timeout here is an unambiguous "no charge", never an
                // indeterminate one - that is why the caller may return Failure and say so plainly.
                var work = SignLeg(client, identityUri, iconUri, identityName, cluster, authToken,
                                   serializedTransaction);
                using var deadline = new CancellationTokenSource();
                var timer = Task.Delay(SignTimeout, deadline.Token);
                var completed = await Task.WhenAny(work, timer);
                if (completed != work)
                {
                    // The inner task is ORPHANED, not abandoned: it must not raise an UNOBSERVED
                    // TaskException later. This continuation is EXPECTED to fire on most timeouts,
                    // not just exotic ones - the `finally` below tears the association socket down
                    // underneath this still-pending request, and a pending MWA request faults when
                    // its transport closes. So read this line as "the orphan finished, as designed",
                    // never as a second defect. It is logged rather than swallowed (CLAUDE.md §12).
                    work.ContinueWith(t => FlowTrace.Warn("Wallet",
                            $"MWA sign leg finished FAULTED after its {SignTimeout.TotalSeconds:0}s deadline " +
                            $"had already been reported to the player " +
                            $"({t.Exception?.GetBaseException().GetType().Name}). Expected: the association " +
                            "was closed under it. Observed here so it is not an unhandled task exception. " +
                            "Nothing was submitted."),
                        TaskContinuationOptions.OnlyOnFaulted);

                    FlowTrace.Fail("Wallet",
                        $"MWA authorize+sign timed out after {SignTimeout.TotalSeconds:0}s - the wallet " +
                        "app never returned a signed transaction. NOTHING WAS SUBMITTED, so no payment " +
                        "can have settled. The world hold the purchase took is released by the caller's " +
                        "`using` on this return path.");
                    throw new TimeoutException(SignTimeoutMessage);
                }

                deadline.Cancel();          // never leave a timer outliving a good signature
                return await work;          // rethrows a real wallet-side fault unchanged
            }
            finally
            {
                await CloseAssociation();
            }
        }

        /// <summary>
        /// The authorize/reauthorize + sign pair as ONE awaitable, so <see cref="SignTransaction"/>
        /// can put a single deadline across both. Split out for exactly that reason: two separate
        /// windows would let a wallet spend the full ceiling twice.
        /// </summary>
        private static async Task<byte[]> SignLeg(
            MobileWalletAdapterClient client, string identityUri, string iconUri, string identityName,
            string cluster, string authToken, byte[] serializedTransaction)
        {
            if (string.IsNullOrEmpty(authToken))
            {
                await client.Authorize(
                    new Uri(identityUri), new Uri(iconUri, UriKind.Relative), identityName, cluster);
            }
            else
            {
                await client.Reauthorize(
                    new Uri(identityUri), new Uri(iconUri, UriKind.Relative), identityName, authToken);
            }

            var signed = await client.SignTransactions(
                new List<byte[]> { serializedTransaction });
            if (signed == null || signed.SignedPayloadsBytes == null ||
                signed.SignedPayloadsBytes.Count == 0)
                return null;
            return signed.SignedPayloadsBytes[0];
        }

        /// <summary>
        /// Prints the EXACT dapp identity triplet that is about to go on the wire,
        /// plus the absolute URL the wallet will resolve the icon to.
        /// <para>
        /// WHY (2026-08-06): the owner's Seeker showed the SDK's branding on the
        /// approval sheet and there was no way to tell, from a device log, WHICH of
        /// the three identity fields the wallet had actually received. MWA wallets
        /// render name + icon straight from the authorize request's `identity`
        /// object (MobileWalletAdapterClient.cs:70-86 -> JsonRequest.cs:18-37,
        /// serialized as "name"/"uri"/"icon"), and every one of those properties is
        /// NullValueHandling.Ignore - a blank field is silently OMITTED from the
        /// JSON, and the wallet then draws its own default. So a missing field is
        /// invisible on the wire and invisible in the logs. This line makes it
        /// visible, and FAILS loudly rather than shipping a nameless request.
        /// </para>
        /// ASCII only - this goes to logcat / Player.log / the F8 break-log.
        /// </summary>
        private static void LogIdentity(string method, string identityUri, string iconUri, string identityName)
        {
            // A blank name or uri is the exact condition that makes a wallet fall
            // back to its own branding. Never let it pass unremarked.
            if (string.IsNullOrEmpty(identityName))
                FlowTrace.Fail("Wallet", "MWA identity NAME is empty - the wallet will show its own default branding.");
            if (string.IsNullOrEmpty(identityUri))
                FlowTrace.Fail("Wallet", "MWA identity URI is empty - the wallet cannot verify us and will decline.");

            // The MWA spec resolves `icon` RELATIVE to `uri`. A leading slash is a
            // root-relative reference, which append-style wallet resolvers turn into
            // a doubled slash and a 404. Warn, never block - a bad icon costs only
            // the branding, and hard-failing a connect over artwork would be worse.
            if (!string.IsNullOrEmpty(iconUri) && iconUri[0] == '/')
            {
                FlowTrace.Warn("Wallet",
                    "MWA icon path starts with '/' - it must be RELATIVE to the identity uri or wallets may resolve it to a doubled slash.");
            }

            var resolved = "<unresolved>";
            try
            {
                if (!string.IsNullOrEmpty(identityUri) && !string.IsNullOrEmpty(iconUri))
                    resolved = new Uri(new Uri(identityUri), iconUri).AbsoluteUri;
            }
            catch (Exception ex)
            {
                // Never let a display-only string break a connect. Report and move on.
                FlowTrace.Warn("Wallet", $"MWA icon URL resolve failed ({ex.GetType().Name}: {ex.Message}).");
            }

            FlowTrace.Step("Wallet",
                $"MWA {method} identity -> name='{identityName}' uri='{identityUri}' icon='{iconUri}' resolvedIcon='{resolved}'");
        }

        // =====================================================================
        //  Association plumbing
        // =====================================================================

        /// <summary>
        /// Launches the wallet activity (TARGETED when possible) and waits for
        /// the encrypted channel to come up. Returns the ready client.
        /// </summary>
        private async Task<MobileWalletAdapterClient> StartAssociation()
        {
            _clientReady = new TaskCompletionSource<MobileWalletAdapterClient>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // =================================================================
            // WO-913 ROOT CAUSE (proven from the 2026-08-06 device capture).
            // -----------------------------------------------------------------
            // ProjectSettings runInBackground = 0. Launching the wallet PAUSES our
            // activity (wm_pause_activity userLeaving=true, 20ms after the line
            // below), which FREEZES the Unity main thread - and every await in this
            // class resumes on Unity's SynchronizationContext.
            //
            // The capture shows it exactly: the last main-thread log (tid 27275) is
            // "MWA association -> package=", 20ms before the pause. The socket open
            // (tid 27635) and key exchange (tid 27521) still ran because
            // WebSocketSharp owns its own threads - so the handshake COMPLETED while
            // the thread that had to send `authorize` was stopped dead. The wallet
            // sheet then sat blank waiting for a request that could never come, and
            // timed out.
            //
            // Keep the main thread pumping for the duration of the association. The
            // previous value is restored in CloseAssociation.
            // =================================================================
            _runInBackgroundToRestore = Application.runInBackground;
            Application.runInBackground = true;
            FlowTrace.Step("Wallet",
                $"MWA main-thread pump held ON for the handshake (was runInBackground={_runInBackgroundToRestore}).");

            var pkg = ResolvePreferredWalletPackage();
            var intent = LocalAssociationIntentCreator.CreateAssociationIntent(_session.AssociationToken, _port);

            if (!string.IsNullOrEmpty(pkg))
            {
                // THE missing line the SDK has no seam for.
                intent.Call<AndroidJavaObject>("setPackage", pkg);
                FlowTrace.Step("Wallet", $"MWA association -> package={pkg}");
            }
            else
            {
                FlowTrace.Step("Wallet", "MWA association -> package=<implicit> (no preferred wallet visible)");
            }

            try
            {
                _currentActivity.Call("startActivityForResult", intent, 0);
            }
            catch (Exception ex) when (!string.IsNullOrEmpty(pkg))
            {
                // The target vanished between the query and the launch (or the
                // <queries> block is missing so the package is invisible).
                // NEVER hard-fail on a preferred wallet — retry implicitly.
                FlowTrace.Warn("Wallet",
                    $"MWA targeted launch to {pkg} failed ({ex.GetType().Name}) - retrying with the implicit intent.");
                var fallback = LocalAssociationIntentCreator.CreateAssociationIntent(_session.AssociationToken, _port);
                _currentActivity.Call("startActivityForResult", fallback, 0);
            }

            _currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(TryConnectWs));

            var completed = await Task.WhenAny(_clientReady.Task, Task.Delay(_clientTimeout));
            if (completed != _clientReady.Task)
            {
                FlowTrace.Fail("Wallet",
                    $"MWA association timed out after {_clientTimeout.TotalSeconds:0}s - no wallet dialed back on port {_port}.");
                await CloseAssociation(); // also latches _closed so OnClose stops re-dialing
                throw new TimeoutException(
                    "The wallet app never connected back. Make sure a Solana wallet is installed and unlocked.");
            }

            // Faulted key exchange rethrows here - tear the socket down first so
            // a failed handshake never leaves a listening port behind.
            if (_clientReady.Task.IsFaulted) await CloseAssociation();
            return await _clientReady.Task;
        }

        /// <summary>
        /// Enumerates every installed handler of the <c>solana-wallet</c>
        /// association scheme and returns the highest-ranked preferred package,
        /// or null when none is visible (caller then uses the implicit intent).
        /// </summary>
        private string ResolvePreferredWalletPackage()
        {
            try
            {
                var installed = QueryInstalledWalletPackages(_currentActivity);
                string reason;
                string resolved = ResolveWalletPackage(installed, out reason);
                FlowTrace.Step("Wallet", "MWA package resolved=" +
                    (string.IsNullOrEmpty(resolved) ? "<implicit>" : resolved) + " reason=" + reason + ".");
                return resolved;
            }
            catch (Exception ex)
            {
                // Package-visibility query is best effort. Never block connect.
                FlowTrace.Warn("Wallet",
                    $"MWA handler query failed ({ex.GetType().Name}: {ex.Message}) - using the implicit intent.");
                return null;
            }
        }

        /// <summary>Installed MWA package ids for the player-facing picker.</summary>
        public static IReadOnlyList<string> GetInstalledWalletPackages()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && SOLANA_SDK
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    return QueryInstalledWalletPackages(activity).ToArray();
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Wallet", "MWA picker handler query failed (" +
                    ex.GetType().Name + ": " + ex.Message + ").");
                return Array.Empty<string>();
            }
#else
            return Array.Empty<string>();
#endif
        }

        /// <summary>
        /// Player-facing switch entry point. The caller must present the ruled
        /// kingdom warning first; this method independently refuses an unconfirmed
        /// or currently uninstalled selection and clears the sealed session on change.
        /// </summary>
        public static bool TrySwitchPreferredWallet(
            string packageName, bool kingdomSwitchConfirmed, out string reason)
        {
            return WalletPreferenceStore.TrySetPreferredPackage(
                packageName,
                GetInstalledWalletPackages(),
                kingdomSwitchConfirmed,
                out reason);
        }

        /// <summary>
        /// Pure resolution seam used by runtime and tests. Stored installed choice
        /// wins; otherwise the original Seeker-first chain is byte-for-byte intact.
        /// </summary>
        public static string ResolveWalletPackage(IReadOnlyList<string> installed, out string reason)
        {
            string stored = WalletPreferenceStore.StoredPackage;
            if (!string.IsNullOrEmpty(stored))
            {
                foreach (string have in installed ?? Array.Empty<string>())
                {
                    if (string.Equals(have, stored, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "stored choice";
                        return have;
                    }
                }
                FlowTrace.Warn("Wallet", "MWA stored package " + stored +
                    " is not installed; falling back to the Seeker-first chain.");
            }

            for (int rank = 0; rank < PreferredWalletPackages.Length; rank++)
            {
                string preferred = PreferredWalletPackages[rank];
                foreach (string have in installed ?? Array.Empty<string>())
                {
                    if (string.Equals(have, preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "chain rank " + (rank + 1);
                        // Preserve the exact package string used before WO-1196.
                        return preferred;
                    }
                }
            }

            reason = installed != null && installed.Count > 0
                ? "installed handlers are outside the known chain; implicit chooser"
                : "no visible handler; implicit fallback";
            return null;
        }

        private static List<string> QueryInstalledWalletPackages(AndroidJavaObject activity)
        {
            var installed = new List<string>();
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var probe = new AndroidJavaObject("android.content.Intent"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    probe.Call<AndroidJavaObject>("setAction", "android.intent.action.VIEW");
                    probe.Call<AndroidJavaObject>("addCategory", "android.intent.category.BROWSABLE");

                    // Scheme-only probe URI — same shape the SDK launches, minus
                    // the per-session association token.
                    var url = AssociationContract.SchemeMobileWalletAdapter + ":/" +
                              AssociationContract.LocalPathSuffix;
                    using (var uriData = uriClass.CallStatic<AndroidJavaObject>("parse", url))
                    {
                        probe.Call<AndroidJavaObject>("setData", uriData);
                    }

                    using (var list = pm.Call<AndroidJavaObject>("queryIntentActivities", probe, 0))
                    {
                        var n = list == null ? 0 : list.Call<int>("size");
                        for (var i = 0; i < n; i++)
                        {
                            using (var resolveInfo = list.Call<AndroidJavaObject>("get", i))
                            using (var activityInfo = resolveInfo.Get<AndroidJavaObject>("activityInfo"))
                            {
                                var name = activityInfo.Get<string>("packageName");
                                if (!string.IsNullOrEmpty(name)) installed.Add(name);
                            }
                        }
                    }
                }

            FlowTrace.Step("Wallet",
                "MWA handlers visible: " + (installed.Count == 0 ? "<none>" : string.Join(", ", installed)));
            return installed;
        }

        /// <summary>Dials the local socket until the wallet accepts, or the timeout elapses.</summary>
        private async void TryConnectWs()
        {
            var remaining = _clientTimeout;
            while (_webSocket.State != WebSocketState.Open && !_didConnect && remaining.TotalSeconds > 0 && !_closed)
            {
                await _webSocket.Connect(awaitConnection: false);
                var delta = TimeSpan.FromMilliseconds(500);
                remaining -= delta;
                await Task.Delay(delta);
            }
        }

        /// <summary>First frame after HELLO_REQ: derive the ECDH secret and build the client.</summary>
        private void ReceivePublicKeyHandler(byte[] helloRsp)
        {
            try
            {
                _session.GenerateSessionEcdhSecret(helloRsp);
                var sender = new MobileWalletAdapterWebSocket(_webSocket, _session);
                _client = new MobileWalletAdapterClient(sender);

                _webSocket.OnMessage -= ReceivePublicKeyHandler;
                _webSocket.OnMessage += HandleEncryptedSessionPayload;

                FlowTrace.Step("Wallet", "MWA session key exchange complete - encrypted channel up.");
                _clientReady.TrySetResult(_client);
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("Wallet", $"MWA key exchange FAILED: {ex.GetType().Name}: {ex.Message}");
                _clientReady.TrySetException(ex);
            }
        }

        /// <summary>
        /// Every subsequent frame: decrypt and hand to the client, which resolves
        /// the pending request Task (result, or SetException on a JSON-RPC error).
        /// </summary>
        private void HandleEncryptedSessionPayload(byte[] payload)
        {
            // WO-913: this guard used to drop frames SILENTLY - a §12 violation, and
            // indistinguishable from "the wallet never replied" in a capture. If the
            // authorize response ever lands here while the guard is shut, say so.
            if (!_didConnect || _client == null)
            {
                FlowTrace.Warn("Wallet",
                    $"MWA frame DROPPED before the channel was ready (didConnect={_didConnect}, client={(_client != null)}, bytes={payload?.Length ?? 0}).");
                return;
            }
            FlowTrace.Step("Wallet", $"MWA encrypted frame received ({payload?.Length ?? 0} bytes) - decoding.");
            try
            {
                var decrypted = _session.DecryptSessionPayload(payload);
                var message = System.Text.Encoding.UTF8.GetString(decrypted);
                _client.Receive(message);
            }
            catch (Exception ex)
            {
                // §12: no silent failures.
                FlowTrace.Fail("Wallet", $"MWA payload decode FAILED: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Tears the association down exactly once.</summary>
        private async Task CloseAssociation()
        {
            if (_closed) return;
            _closed = true;
            try
            {
                _webSocket.OnMessage -= HandleEncryptedSessionPayload;
                await _webSocket.Close();
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Wallet", $"MWA association close: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // Restore only AFTER the socket is down. By this point the association
                // is finished, so nothing is left pending that a frozen main thread
                // could strand. Restoring any earlier re-opens the exact freeze above.
                Application.runInBackground = _runInBackgroundToRestore;
                FlowTrace.Step("Wallet",
                    $"MWA main-thread pump restored to runInBackground={_runInBackgroundToRestore}.");
            }
        }
#endif
    }
}
