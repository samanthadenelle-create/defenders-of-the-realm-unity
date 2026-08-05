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
                // The SDK reconnects here unconditionally once _didConnect is
                // set, with no "we meant to close" latch. Guard it.
                if (!_didConnect || _closed) return;
                _webSocket.Connect(awaitConnection: false);
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
            var client = await StartAssociation();
            try
            {
                // HARD SDK CONSTRAINT (MobileWalletAdapterClient.cs:62-69):
                // identityUri MUST be absolute, iconUri MUST be relative — the
                // client throws ArgumentException otherwise, BEFORE any I/O.
                return await client.Authorize(
                    new Uri(identityUri),
                    new Uri(iconUri, UriKind.Relative),
                    identityName,
                    cluster);
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
                var installed = new List<string>();

                using (var pm = _currentActivity.Call<AndroidJavaObject>("getPackageManager"))
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

                foreach (var preferred in PreferredWalletPackages)
                {
                    foreach (var have in installed)
                    {
                        if (string.Equals(have, preferred, StringComparison.OrdinalIgnoreCase))
                            return preferred;
                    }
                }

                if (installed.Count > 0)
                {
                    FlowTrace.Warn("Wallet",
                        "MWA: a wallet is installed but none is in the preference chain - using the implicit intent.");
                }
                return null;
            }
            catch (Exception ex)
            {
                // Package-visibility query is best effort. Never block connect.
                FlowTrace.Warn("Wallet",
                    $"MWA handler query failed ({ex.GetType().Name}: {ex.Message}) - using the implicit intent.");
                return null;
            }
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
            if (!_didConnect || _client == null) return;
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
        }
#endif
    }
}
