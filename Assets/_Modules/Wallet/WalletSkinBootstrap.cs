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
using Cysharp.Threading.Tasks;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;
using DeNelle.Core.State;

namespace DeNelle.Wallet
{
    /// <summary>Wallet-side subscriber for the SKR skin's Connect Wallet button (WO-603).</summary>
    public static class WalletSkinBootstrap
    {
        private static WalletService _wallet;
        private static bool _connecting;

        /// <summary>Installs the wallet-connect handler at boot — ONLY under the SKR skin.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Pi skin (the live default): leave the wallet path entirely unwired.
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
                        GameStateService.Instance?.BindWallet(key);
                        FlowTrace.Step("Skin", $"Bound NeonDB identity key ({skin.IdentityKeyKind}) from wallet connect.");
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
    }
}
