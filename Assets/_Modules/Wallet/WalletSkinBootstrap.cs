// =============================================================================
// WalletSkinBootstrap — bridges the skin-neutral "Connect Wallet" button to the
// Solana wallet stack (WO-603).
// -----------------------------------------------------------------------------
// The corner auth button lives in DeNelle.Core (PiSignInController). Under the
// Solana/$SKR skin it presents as "Connect Wallet" and raises
// CurrencySkinResolver.WalletConnectRequested — but Core CANNOT reference
// DeNelle.Wallet (assembly direction is Wallet → Core). This bootstrapper is the
// Wallet-side subscriber: it installs at boot ONLY under the SKR skin, drives
// WalletService.Connect(), and (when the skin opts in) binds the connected wallet
// pubkey as the NeonDB identity key.
//
// Under the Pi skin this NEVER subscribes — the Pi sign-in path is untouched
// (zero regression). Follow-up (WO-603 RESULT): a richer connect UI (address /
// disconnect / network badge) is code-built later; WalletConnectDialog is UXML,
// which does not render in WebGL builds (CLAUDE.md §8).
// =============================================================================

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Web3;          // BackendRequestSigner.WarmUpSessionAsync - handshake at connect
using DeNelle.Core.Platform;
using DeNelle.Core.State;

namespace DeNelle.Wallet
{
    /// <summary>Wallet-side subscriber for the SKR skin's Connect Wallet button (WO-603)
    /// + the skin-independent login-surface connect handler (WO-847).</summary>
    public static class WalletSkinBootstrap
    {
        private static WalletService _wallet;

        /// <summary>
        /// The ONE live <see cref="WalletService"/> for the session, or null when nothing has
        /// connected yet. Read-only on purpose: this class owns the instance's lifecycle (create on
        /// connect, clear on disconnect) and every other surface BORROWS it.
        /// <para>
        /// ⛔ WHY THIS EXISTS (2026-08-24, the go-live P0). PackStore held its own
        /// `private WalletService _wallet` and the only way to fill it was
        /// `PackStore.SetWalletService(...)` — a public injector with **ZERO call sites in the whole
        /// project**. So the store's wallet reference was ALWAYS null. That is not a cosmetic gap:
        /// `PurchaseQuoteService.RefreshPricesAsync(null)` fails closed with "no signing wallet", so
        /// the store NEVER requested a quote, and every pack read "Price unavailable" no matter how
        /// connected the player's wallet was. Confirmed against production: zero
        /// /api/purchases/quote requests ever reached the server while the owner's wallet showed
        /// connected on screen.
        /// </para>
        /// <para>
        /// ⚠ The store's OWN copy stays authoritative once set (an explicitly injected service wins),
        /// so this is a FALLBACK adoption, not a second owner.
        /// </para>
        /// </summary>
        public static WalletService ConnectedWallet => _wallet;
        private static bool _connecting;

        /// <summary>Installs the wallet-connect handlers at boot. The LOGIN-surface
        /// handler registers under EVERY skin (WO-847); the corner-button event
        /// subscriber stays SKR-only (WO-603, zero Pi regression).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // WO-847: the Android wallet-first LOGIN surface must connect+bind under
            // every skin - identity binding on the login path is never left to the
            // optional skin config (skin.json bindIdentityOnAuth). Registered BEFORE
            // the skin gate so the bridge always has a handler.
            LoginWalletBridge.ConnectHandler = ConnectForLoginAsync;
            FlowTrace.Step("Wallet", "login wallet-connect handler registered (LoginWalletBridge, skin-independent).");

            TryAutoResumeAsync().Forget();

            // Pi skin (the live default): leave the corner-button wallet path entirely unwired.
            if (!CurrencySkinResolver.IsSkr) return;

            CurrencySkinResolver.WalletConnectRequested -= OnConnectRequested; // idempotent
            CurrencySkinResolver.WalletConnectRequested += OnConnectRequested;
            CurrencySkinResolver.WalletDisconnectRequested -= OnDisconnectRequested; // idempotent
            CurrencySkinResolver.WalletDisconnectRequested += OnDisconnectRequested;
            FlowTrace.Step("Skin", "SKR skin active — wallet-connect handler installed (WalletSkinBootstrap).");
        }

        /// <summary>
        /// Boot-time silent reconnect. Owner ruling 2026-08-17: *"yes it should auto connect, there
        /// is a menu option to reset"* — a returning player should never be asked to connect again.
        /// </summary>
        /// <remarks>
        /// ⚠ ONLY RUNS WHEN A SEALED SESSION ALREADY EXISTS. `MwaSessionStore.HasStoredSession` is
        /// the gate, and it is not a nicety: without it, a FIRST-TIME player — or anyone who chose
        /// Reset — would have the wallet app launched at them unprompted on every cold start. That
        /// is a far worse first impression than one Connect tap, and it is the exact behaviour the
        /// owner's "menu option to reset" is meant to give back. Reset clears the store
        /// (SolanaWalletProvider.Disconnect → MwaSessionStore.Clear), so the very next boot is
        /// silent again in the other direction: no stored session, no auto-connect, no wallet launch.
        ///
        /// FIRE-AND-FORGET, NEVER AWAITED BY BOOT. The association takes ~2.6s on a real Seeker
        /// (measured 2026-08-17), so awaiting this would stall the title screen for a wallet the
        /// player did not ask for yet. The manual Connect handler is registered BEFORE this starts,
        /// so a player who taps Connect during the attempt is served by the normal path — and
        /// `_connecting` makes the duplicate a no-op rather than a second association.
        ///
        /// FAILURE IS SILENT BY DESIGN. Every outcome lands on "the player taps Connect", which is
        /// exactly today's behaviour, so this can only ever remove a tap and never add a dead end.
        /// It is traced, not surfaced: a boot-time toast about a wallet the player has not asked
        /// about yet is noise.
        /// </remarks>
        private static async UniTaskVoid TryAutoResumeAsync()
        {
            if (!MwaSessionStore.HasStoredSession)
            {
                FlowTrace.Step("Wallet",
                    "auto-resume skipped — no sealed session (first run, or the player chose Reset). " +
                    "The wallet app is deliberately NOT launched; the player taps Connect.");
                return;
            }

            if (_connecting)
            {
                FlowTrace.Step("Wallet", "auto-resume skipped — a connect is already in progress.");
                return;
            }

            FlowTrace.Step("Wallet",
                "auto-resume: sealed session present — attempting a SILENT reconnect at boot " +
                "(no prompt; falls back to the Connect button on any failure).");

            // Explicit try/catch rather than Guard.Try: Guard has no async overload, and a
            // fire-and-forget UniTaskVoid that throws would otherwise surface as an unobserved
            // exception with no context. Caught AND LOGGED — never swallowed (§12).
            // AuthOutcome is a STRUCT (non-nullable), so `default` is the not-set sentinel — its
            // Success is false, which is exactly the "did not connect" branch we want on a throw.
            AuthOutcome outcome = default;
            try
            {
                outcome = await ConnectForLoginAsync();
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Wallet",
                    $"auto-resume threw ({ex.GetType().Name}: {ex.Message}) — falling back to the " +
                    "Connect button. Boot is unaffected; this path is fire-and-forget by design.");
                return;
            }

            if (outcome.Success)
                FlowTrace.Step("Wallet", "auto-resume SUCCEEDED — connected at boot with no player action.");
            else
                FlowTrace.Step("Wallet",
                    "auto-resume did not connect — falling back to the Connect button. " +
                    "This is not an error: the stored grant may be revoked or expired.");
        }

        private static void OnConnectRequested() => ConnectAsync().Forget();

        /// <summary>
        /// The RESET the owner ruled on 2026-08-17 ("there is a menu option to reset"), finally wired.
        /// Clears the sealed MWA session via WalletService.Disconnect, so the NEXT cold start does not
        /// auto-resume and the player is asked to Connect again - the documented other direction of
        /// TryAutoResumeAsync's gate.
        /// </summary>
        private static void OnDisconnectRequested() => DisconnectAsync().Forget();

        private static async UniTaskVoid DisconnectAsync()
        {
            // ⚠ Disconnecting MID-CONNECT would race the association and could clear a session that
            // is about to be written. Refuse and say so rather than half-doing it.
            if (_connecting)
            {
                FlowTrace.Warn("Wallet", "Disconnect requested while a connect is in progress - ignored.");
                return;
            }
            if (_wallet == null)
            {
                // Not an error: nothing is connected, and the caller's intent (end up disconnected)
                // is already satisfied. Say it plainly instead of failing.
                FlowTrace.Step("Wallet", "Disconnect requested with no WalletService instance - already disconnected.");
                CurrencySkinResolver.PublishWalletDisconnected();
                return;
            }

            FlowTrace.Step("Wallet", "Disconnect requested - clearing the sealed MWA session (no auto-resume next boot).");
            await _wallet.Disconnect();
            // WalletService.Disconnect already publishes the disconnected state in its finally block;
            // it is NOT repeated here. PublishWalletDisconnected is idempotent, but one owner per fact.
        }

        private static async UniTaskVoid ConnectAsync()
        {
            if (_connecting) { FlowTrace.Step("Wallet", "Connect already in progress — ignoring duplicate request."); return; }
            _connecting = true;
            try
            {
                // Auto-selects SolanaWalletProvider when the Solana Unity SDK is compiled in,
                // else the devnet StubWalletProvider — so this runs end-to-end with or without the SDK.
                if (_wallet == null) _wallet = new WalletService();

                var account = await _wallet.Connect();
                if (!account.IsValid)
                {
                    FlowTrace.Warn("Wallet", "SKR wallet connect cancelled/failed — no identity bound.");
                    return;
                }

                // ⭐ HANDSHAKE AT CONNECT, NOT AT PURCHASE (owner, 2026-08-24). The backend session
                // used to be minted lazily on the first authed call, so the prompts landed
                // 1-at-connect then TWO-at-first-purchase (session mint + payment). The player knows
                // the other shape: connect, then the auth handshake, and later ONE prompt to pay.
                // Same three signatures; this one does not interrupt the purchase.
                //
                // ⚠ BEST-EFFORT AND DELIBERATELY NOT AWAITED FOR CORRECTNESS: TryAttachSession still
                // mints on demand, so a declined or failed warm-up costs one later prompt and never a
                // broken purchase. Awaited here only so the two wallet dialogs are ORDERED - firing
                // them concurrently would stack two prompts on the player at once.
                try
                {
                    await BackendRequestSigner.WarmUpSessionAsync(account.Address);
                }
                catch (Exception warmEx)
                {
                    // Caught AND LOGGED, never swallowed (§12). Correctness is unaffected - the
                    // lazy mint still runs on the first authed call.
                    FlowTrace.Warn("Wallet",
                        $"session warm-up threw ({warmEx.GetType().Name}) - harmless; the first authed " +
                        "call will mint on demand. " + warmEx.Message);
                }

                var skin = CurrencySkinResolver.Active;
                if (skin != null && skin.BindIdentityOnAuth)
                {
                    string key = skin.ResolveIdentityKey(null, account.Address);
                    if (!string.IsNullOrEmpty(key))
                    {
                        // Attest ONLY when this really is the connected wallet address from a
                        // real signing provider. A skin may resolve the identity key to
                        // something else entirely (a Pi UID), which must never key a cloud save.
                        bool attested = _wallet.IsRealSigningWallet &&
                                        string.Equals(key, account.Address, StringComparison.Ordinal);
                        GameStateService.Instance?.BindWallet(key, attested);
                        FlowTrace.Step("Skin", $"Bound NeonDB identity key ({skin.IdentityKeyKind}) from wallet connect " +
                                               $"(cloud-attested={attested}).");
                    }
                    else
                    {
                        FlowTrace.Warn("Skin", "Wallet connected but address was empty — identity not bound.");
                    }
                }
            }
            catch (Exception e) { FlowTrace.Fail("Wallet", $"SKR wallet connect threw: {e.Message}"); }
            finally { _connecting = false; }
        }

        // =====================================================================
        //  WO-847 — the login surface's Connect Wallet (Android wallet-first)
        // =====================================================================

        /// <summary>
        /// Connect flow for the LOGIN surface (LoginWalletBridge). Same stack as the
        /// corner-button path (WalletService.Connect - MWA on device, stub in editor)
        /// but the identity bind is EXPLICIT and unconditional: the connected address
        /// keys the save via GameStateService.BindWallet (the :71 precedent), never
        /// gated behind the skin's BindIdentityOnAuth. Resolves the email-path
        /// AuthOutcome shape (UserId = wallet address) so the panel continues
        /// identically to a successful email sign-in.
        /// </summary>
        private static async Task<AuthOutcome> ConnectForLoginAsync()
        {
            if (_connecting)
            {
                FlowTrace.Step("Wallet", "Login connect requested while a connect is in progress - ignoring duplicate.");
                return AuthOutcome.Fail("A wallet connect is already in progress.");
            }
            _connecting = true;
            try
            {
                if (_wallet == null) _wallet = new WalletService();

                var account = await _wallet.Connect();
                if (!account.IsValid)
                {
                    // Tell the player WHICH failure this was. "Connect cancelled" after a
                    // 30s timeout is a lie that sends them looking for the wrong fix -
                    // WalletService.LastConnectError carries the real, actionable reason.
                    string why = string.IsNullOrEmpty(_wallet.LastConnectError)
                        ? "Connect cancelled."
                        : _wallet.LastConnectError;
                    FlowTrace.Warn("Wallet", "Login wallet connect did not complete - no identity bound: " + why);
                    return AuthOutcome.Fail(why);
                }

                var svc = GameStateService.Instance;
                if (svc == null)
                {
                    FlowTrace.Warn("Wallet", "Login connect: GameStateService null - save not re-keyed.");
                }
                else
                {
                    // ATTESTED bind: this address came from a real, key-holding signing
                    // wallet, which is the ONLY thing allowed to key a cloud save. The
                    // devnet stub reports false here, so an SDK-less build can never point
                    // every tester at one shared player_data row.
                    bool attested = _wallet.IsRealSigningWallet;
                    Guard.Try("Wallet", "BindWallet(wallet address, login path)",
                        () => svc.BindWallet(account.Address, attested));
                    FlowTrace.Step("Wallet", $"Login connect bound save identity to wallet {account.ShortAddress} " +
                                             $"(cloud-attested={attested}).");
                }

                return new AuthOutcome { Success = true, UserId = account.Address, Email = string.Empty, Error = string.Empty };
            }
            catch (Exception e)
            {
                FlowTrace.Fail("Wallet", $"Login wallet connect threw: {e.Message}");
                return AuthOutcome.Fail("Wallet connect failed. Please try again.");
            }
            finally { _connecting = false; }
        }
    }
}
