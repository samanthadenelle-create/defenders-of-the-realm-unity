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
using DeNelle.Core.Platform;
using DeNelle.Core.State;

namespace DeNelle.Wallet
{
    /// <summary>Wallet-side subscriber for the SKR skin's Connect Wallet button (WO-603)
    /// + the skin-independent login-surface connect handler (WO-847).</summary>
    public static class WalletSkinBootstrap
    {
        private static WalletService _wallet;
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

            // Pi skin (the live default): leave the corner-button wallet path entirely unwired.
            if (!CurrencySkinResolver.IsSkr) return;

            CurrencySkinResolver.WalletConnectRequested -= OnConnectRequested; // idempotent
            CurrencySkinResolver.WalletConnectRequested += OnConnectRequested;
            FlowTrace.Step("Skin", "SKR skin active — wallet-connect handler installed (WalletSkinBootstrap).");
        }

        private static void OnConnectRequested() => ConnectAsync().Forget();

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
