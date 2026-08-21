<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 45 — Jupiter Swap Execution (Phase 3)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-26
**Priority:** High (after WO-43 + WO-44)
**Scope:** Large / Real money — test thoroughly on devnet before mainnet
**Depends on:** WO-44 (`JupiterSwapService` + `RoutePlan` JSON in `SwapQuote`)

---

## Goal

Enable actual USDC → SKR swaps. This phase adds: (1) the `/v6/swap` transaction
request to Jupiter, (2) real wallet signing via Phantom deep-link (mobile) /
JS bridge (WebGL), and (3) the success/failure flow including SKR balance
refresh.

**Deploy to devnet first. Do not go to mainnet until all acceptance criteria
pass on devnet with real (devnet) wallets.**

---

## What changes from WO-44

| WO-44 (Phase 2) | WO-45 (Phase 3) |
|---|---|
| Confirm button logs "mock" message | Confirm button calls `ExecuteSwapAsync` |
| `WalletBridgeStub` fakes signing | Real wallet SDK signs + broadcasts |
| No balance update | SKR balance refreshed after confirmed swap |
| Panel stays open after confirm | Panel closes on success |

`JupiterSwapService`, `JupiterSwapPanelController`, all UXML/USS, and
`CoreServices` are the same — only the controller's `OnConfirmTapped` and the
new execution path in `JupiterSwapService` change.

---

## 1. `JupiterSwapService` additions — swap execution

**Add to existing `JupiterSwapService.cs`:**

```csharp
// Add at the top of the class:
private const string SwapUrl = "https://quote-api.jup.ag/v6/swap";

// Add this public method (called from JupiterSwapPanelController.OnConfirmTapped):

/// <summary>
/// Builds a swap transaction from the cached quote, sends it to the wallet
/// for signing, and broadcasts it to Solana. Returns true on confirmed
/// success, false on any failure.
/// </summary>
public async Task<bool> ExecuteSwapAsync(SwapQuote quote, string userPublicKey)
{
    if (quote == null || string.IsNullOrEmpty(userPublicKey))
    {
        Debug.LogWarning("[JupiterSwapService] ExecuteSwapAsync: missing quote or wallet key.");
        return false;
    }

    // ── Step 1: Get the serialised transaction from Jupiter ──────────────
    string swapBody = BuildSwapRequestBody(quote.RoutePlan, userPublicKey);
    string serialisedTx = await PostSwapRequest(swapBody);
    if (serialisedTx == null) return false;

    // ── Step 2: Sign + broadcast via the wallet bridge ───────────────────
    bool success = false;
    string errorMsg = null;

    WalletBridge.SignAndSendTransaction(
        serialisedTx,
        onSuccess: sig =>
        {
            Debug.Log($"[JupiterSwapService] Swap confirmed on-chain. Sig: {sig}");
            success = true;
        },
        onError: err =>
        {
            Debug.LogWarning($"[JupiterSwapService] Swap failed: {err}");
            errorMsg = err;
        });

    // WalletBridge callbacks may be synchronous (stub) or triggered on the
    // next frame (real deep-link). Yield until we have a result.
    float timeout = 30f;
    float elapsed = 0f;
    while (!success && errorMsg == null && elapsed < timeout)
    {
        await Task.Yield();
        elapsed += Time.deltaTime;
    }

    if (elapsed >= timeout)
        Debug.LogWarning("[JupiterSwapService] Wallet signing timed out after 30 s.");

    return success;
}

private string BuildSwapRequestBody(string routePlanJson, string userPublicKey)
{
    // routePlanJson IS the full /v6/quote response — Jupiter's /v6/swap
    // accepts it verbatim as the "quoteResponse" field.
    return $"{{" +
           $"\"quoteResponse\":{routePlanJson}," +
           $"\"userPublicKey\":\"{userPublicKey}\"," +
           $"\"wrapAndUnwrapSol\":true," +
           $"\"dynamicComputeUnitLimit\":true," +
           $"\"prioritizationFeeLamports\":\"auto\"" +
           $"}}";
}

private async Task<string> PostSwapRequest(string body)
{
    using var req = new UnityWebRequest(SwapUrl, "POST");
    req.uploadHandler   = new UploadHandlerRaw(
        System.Text.Encoding.UTF8.GetBytes(body));
    req.downloadHandler = new DownloadHandlerBuffer();
    req.SetRequestHeader("Content-Type", "application/json");

    var op = req.SendWebRequest();
    while (!op.isDone) await Task.Yield();

    if (req.result != UnityWebRequest.Result.Success)
    {
        Debug.LogWarning($"[JupiterSwapService] /v6/swap request failed " +
                         $"({req.responseCode}): {req.error}");
        return null;
    }

    // Parse out just the swapTransaction base64 field.
    try
    {
        var dto = JsonUtility.FromJson<SwapResponseDto>(req.downloadHandler.text);
        return dto.swapTransaction;
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[JupiterSwapService] Swap response parse error: {ex.Message}");
        return null;
    }
}

// Add inside the class, alongside the existing QuoteResponseDto:
[Serializable]
private sealed class SwapResponseDto
{
    public string swapTransaction;   // base64-encoded versioned transaction
    public string lastValidBlockHeight;
}
```

---

## 2. `JupiterSwapPanelController` — wire up confirm

**Edit `OnConfirmTapped` in `JupiterSwapPanelController.cs`:**

```csharp
// Replace the existing Phase-1 stub OnConfirmTapped with:
private async void OnConfirmTapped()
{
    if (_latestQuote == null) return;

    // Disable the button while the transaction is in-flight.
    SetConfirmEnabled(false);
    SetStatus("Sending to wallet for approval…", isError: false);

    // Resolve the real JupiterSwapService (Phase 3 replaces MockJupiterSwapService).
    var execService = _service as JupiterSwapService;
    if (execService == null)
    {
        // Mock path — still used in dev/offline.
        Debug.Log($"[JupiterSwapPanel] Mock confirm: {_currentInput:F2} USDC " +
                  $"→ {_latestQuote.SkrOut:F2} SKR.");
        SetStatus("Swap confirmed (mock).", isError: false);
        return;
    }

    bool ok = await execService.ExecuteSwapAsync(_latestQuote, ConnectedWalletKey);

    if (ok)
    {
        SetStatus("Swap complete! SKR added to your balance.", isError: false);
        // Give the player a moment to read the success message, then close.
        await Task.Delay(1800);
        _service.CloseSwapPanel();
        // Notify other systems that the SKR balance changed.
        SwapEvents.RaiseBalanceChanged();
    }
    else
    {
        SetStatus("Swap failed. Please try again.", isError: true);
        // Re-enable so the player can retry.
        SetConfirmEnabled(!string.IsNullOrEmpty(ConnectedWalletKey));
    }
}
```

---

## 3. `WalletBridge` — replace the stub

**Path:** `Assets/_Modules/Web3/WalletBridge.cs`

This is the real implementation. The platform (`#if`) determines the method:

```csharp
// =============================================================================
// WalletBridge — signs + sends a Jupiter swap transaction via the player's
// connected Solana wallet. WO-45.
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Web3
{
    /// <summary>
    /// Routes transaction signing to the appropriate wallet mechanism for
    /// each platform. Replace the stub sections with the real SDK calls
    /// once the wallet integration library is confirmed.
    /// </summary>
    public static class WalletBridge
    {
        // Set by the wallet connection flow (WO-45 wallet connect feature).
        public static string ConnectedPublicKey { get; private set; } = "";
        public static bool   IsConnected        => !string.IsNullOrEmpty(ConnectedPublicKey);

        public static void SetConnected(string publicKey) =>
            ConnectedPublicKey = publicKey ?? "";

        public static void Disconnect() => ConnectedPublicKey = "";

        // ── Sign + send ──────────────────────────────────────────────────────

        public static void SignAndSendTransaction(
            string base64Transaction,
            Action<string> onSuccess,
            Action<string> onError = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // ── Stub path: simulate signing without a real wallet ────────────
            // Useful in Editor and on-device QA builds. Remove the dev guard
            // once real wallet integration is confirmed.
            Debug.Log("[WalletBridge] DEV STUB — not sending a real transaction.");
            onSuccess?.Invoke("DEV_SIG_" + Guid.NewGuid().ToString("N")[..8]);

#elif UNITY_WEBGL
            // ── WebGL: JS bridge ─────────────────────────────────────────────
            // TODO: implement WalletBridgePlugin.jslib that calls
            //   window.solana.signAndSendTransaction(tx)  (Phantom browser ext)
            // and calls back into C# via SendMessage.
            // See: https://docs.phantom.app/solana/sending-a-transaction
            WalletBridgePluginWebGL.SignAndSend(base64Transaction, onSuccess, onError);

#elif UNITY_IOS || UNITY_ANDROID
            // ── Mobile: Phantom deep-link ────────────────────────────────────
            // TODO: build the phantom://v1/signAndSendTransaction deep-link,
            // open it with Application.OpenURL, and handle the callback URL
            // in an iOS Universal Link / Android App Link receiver.
            // See: https://docs.phantom.app/phantom-deeplinks/provider-methods/signandsendtransaction
            WalletBridgePluginMobile.SignAndSend(base64Transaction, onSuccess, onError);

#else
            Debug.LogError("[WalletBridge] No wallet bridge implemented for this platform.");
            onError?.Invoke("Unsupported platform.");
#endif
        }
    }
}
```

> **Note for implementor:** `WalletBridgePluginWebGL` and
> `WalletBridgePluginMobile` are new classes to create as part of WO-45.
> They are the seam that requires the platform wallet SDK decision to be made
> first. See §6 (Platform notes) below.

---

## 4. `SwapEvents` — balance-changed notification

**Path:** `Assets/_Modules/Web3/SwapEvents.cs`

Thin event bus so the SKR balance display (wherever it lives) can react to
a completed swap without `DeNelle.Web3` needing to reference HUD modules.

```csharp
using System;

namespace DeNelle.Web3
{
    /// <summary>
    /// Simple static events for swap lifecycle — decouples Web3 from HUD.
    /// Subscribe from VillageHudController or wherever the SKR counter lives.
    /// </summary>
    public static class SwapEvents
    {
        /// <summary>Fired after a swap transaction is confirmed on-chain.</summary>
        public static event Action OnBalanceChanged;

        public static void RaiseBalanceChanged() => OnBalanceChanged?.Invoke();
    }
}
```

Subscribe in the SKR balance controller:
```csharp
private void OnEnable()  => SwapEvents.OnBalanceChanged += RefreshSkrBalance;
private void OnDisable() => SwapEvents.OnBalanceChanged -= RefreshSkrBalance;
private void RefreshSkrBalance() { /* fetch + display updated SKR balance */ }
```

---

## 5. Platform fee routing — pre-launch checklist

Jupiter platform fees require a **fee account** — specifically, the SPL
associated token account (ATA) for the *output* token (SKR) owned by the
fee-collection wallet.

Before going live:

1. Decide the fee-collection wallet address.
2. Create the SKR ATA for that wallet on-chain:
   ```
   spl-token create-account <SKR_MINT> --owner <FEE_WALLET>
   ```
3. Set `SwapFeeConfig._feeWalletAddress` to the ATA address (not the wallet).
4. Set `SwapFeeConfig._platformFeeBps` to the desired rate (10–30 recommended).
5. Verify fees are received by doing a test swap on devnet first.

Reference: https://station.jup.ag/docs/apis/adding-fees

---

## 6. Platform notes — wallet SDK decision

Choose one (or both for multi-platform):

| Platform | Approach | Reference |
|---|---|---|
| WebGL | Phantom browser extension via `window.phantom.solana` JS interop | https://docs.phantom.app |
| iOS / Android | Phantom deep-link (`phantom://`) or WalletConnect v2 | https://docs.phantom.app/phantom-deeplinks |
| Desktop standalone | WalletConnect v2 QR code | https://walletconnect.com |

The `WalletBridge` class in §3 has `#if` guards for each platform. Implement
the TODO sections once the SDK is chosen. The rest of the codebase does not
change — only `WalletBridgePluginWebGL.cs` and/or `WalletBridgePluginMobile.cs`
need to be created.

---

## 7. Devnet test plan (run before mainnet)

1. Point `_skrMint` at a devnet test token.
2. Set `SwapFeeConfig._feeWalletAddress` to a devnet ATA.
3. Fund a devnet wallet with 10 USDC (airdrop via `solana airdrop`).
4. Open the swap panel in a dev build on the target platform.
5. Enter 5 USDC → confirm quota is reasonable → tap Swap Now.
6. Wallet signs → transaction broadcasts → check Solana Explorer (devnet).
7. Verify: (a) SKR received, (b) platform fee received at fee ATA,
   (c) game SKR balance updated, (d) panel closes cleanly.
8. Test failure case: reject signing in wallet → panel shows error, button
   re-enables, no crash.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Web3/JupiterSwapService.cs` | **Edit** — add `ExecuteSwapAsync`, `PostSwapRequest`, `SwapResponseDto` |
| `Assets/_Modules/Web3/JupiterSwapPanelController.cs` | **Edit** — replace `OnConfirmTapped` stub with real async flow |
| `Assets/_Modules/Web3/WalletBridge.cs` | **Create** — replaces `WalletBridgeStub` |
| `Assets/_Modules/Web3/WalletBridgePluginWebGL.cs` | **Create** (if targeting WebGL) |
| `Assets/_Modules/Web3/WalletBridgePluginMobile.cs` | **Create** (if targeting iOS/Android) |
| `Assets/_Modules/Web3/SwapEvents.cs` | **Create** — balance-changed event bus |
| SKR balance display controller (TBD) | **Edit** — subscribe to `SwapEvents.OnBalanceChanged` |
| `Assets/_Modules/Web3/WalletBridgeStub.cs` | **Delete** (or keep under `#if DEVELOPMENT_BUILD` guard) |

---

## Acceptance Criteria

- [ ] End-to-end devnet swap completes: USDC deducted, SKR received, fee
      routed to fee ATA — all confirmed on Solana Explorer
- [ ] Platform fee matches `SwapFeeConfig._platformFeeBps` to the cent
- [ ] Rejecting the wallet signature shows an error state; no exception thrown;
      button re-enables for retry
- [ ] Wallet signing timeout (>30 s) shows an error, does not hang the panel
- [ ] `SwapEvents.OnBalanceChanged` fires after a confirmed swap; any
      subscribed SKR counter updates
- [ ] Panel closes cleanly 1.8 s after a successful swap
- [ ] `WalletBridgeStub` is not present in a Release build
- [ ] No compile errors on all target platforms (WebGL / iOS / Android as
      applicable) — `#if` guards must be exhaustive
- [ ] Full mainnet test passes on a real device with a real wallet before
      feature flag is enabled for players

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `JupiterSwapService.cs:52,271` — /v6/swap execution shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
