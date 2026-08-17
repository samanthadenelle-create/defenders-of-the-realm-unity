> ⚠ **UNRESOLVED NUMBER COLLISION — WO-43 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_43_jupiter_swap_panel_SUPERSEDED.md`, `WORK_ORDER_43_jupiter_swap_panel_phase1.md`
> Both added in the same commit; both are already CLOSED and both redirect to **WO-210**, so WO-43 is effectively a vacant number. Low risk either way — but the tie is real.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 43 — In-App SKR Swap (Jupiter Aggregator)

**Status:** CLOSED — SUPERSEDED by WO-210 (owner-approved sweep 2026-08-09: filename already marked it; content re-minted as WO-210)
**Date:** 2026-05-26
**Author:** Owner direction — UX + revenue feature
**Priority:** Medium — monetisation / onboarding friction reduction
**Depends on:** WO-41 (`CoreServices` registry, `IJupiterService` slot)

---

## Goal

When a player doesn't have enough SKR to perform an action, a non-intrusive
swap panel slides up letting them convert USDC → SKR via Jupiter instantly,
without leaving the game. The game takes a configurable platform fee
(default 0.2%) on each swap.

**v1 scope:** USDC → SKR only. SOL input is architecture-ready but not
surfaced in the UI until v2.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  DeNelle.Core                                                    │
│    IJupiterService  (interface + SwapQuote record)               │
│    CoreServices.Jupiter { get; RegisterJupiter / Unregister }    │
└────────────────────────────┬────────────────────────────────────┘
                             │ implements
┌────────────────────────────▼────────────────────────────────────┐
│  DeNelle.Web3  (new asmdef, references DeNelle.Core only)        │
│    JupiterSwapService      — UnityWebRequest → Jupiter REST API  │
│    JupiterSwapPanelController — drives the UXML panel            │
│    SwapFeeConfig           — ScriptableObject (fee bps, wallet)  │
└─────────────────────────────────────────────────────────────────┘
```

No other assembly references `DeNelle.Web3`. Callers (Village, HeroSelect,
etc.) open the swap panel via `CoreServices.Jupiter?.OpenSwapPanel(decimal)`.

---

## 1. New asmdef — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/DeNelle.Web3.asmdef`

```json
{
  "name": "DeNelle.Web3",
  "rootNamespace": "DeNelle.Web3",
  "references": ["DeNelle.Core"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "autoReferenced": false,
  "defineConstraints": []
}
```

---

## 2. `IJupiterService` — `DeNelle.Core`

**Path:** `Assets/_Modules/Core/Web3/IJupiterService.cs`

```csharp
// =============================================================================
// IJupiterService — cross-assembly contract for the Jupiter swap panel.
// Registered in CoreServices; callers never reference DeNelle.Web3 directly.
// =============================================================================

using System.Threading.Tasks;

namespace DeNelle.Core.Web3
{
    /// <summary>
    /// Fetches swap quotes from Jupiter and opens the in-game swap panel.
    /// Implemented by <c>JupiterSwapService</c> in DeNelle.Web3.
    /// </summary>
    public interface IJupiterService
    {
        /// <summary>
        /// Opens the swap panel pre-filled to acquire at least
        /// <paramref name="minimumSkr"/> SKR. Pass 0 to open blank.
        /// </summary>
        void OpenSwapPanel(decimal minimumSkr = 0m);

        /// <summary>Closes the panel if it is currently visible.</summary>
        void CloseSwapPanel();

        /// <summary>
        /// Fetches a live quote without opening the panel.
        /// Returns null on network error.
        /// </summary>
        Task<SwapQuote?> GetQuoteAsync(SwapInputToken input, decimal inputAmount);
    }

    /// <summary>Quote returned by the Jupiter /v6/quote endpoint.</summary>
    public sealed class SwapQuote
    {
        /// <summary>SKR the player will receive (after fees).</summary>
        public decimal SkrOut      { get; init; }
        /// <summary>Exchange rate: 1 input token = N SKR.</summary>
        public decimal Rate        { get; init; }
        /// <summary>Platform fee taken by Defenders (in input-token units).</summary>
        public decimal PlatformFee { get; init; }
        /// <summary>Network + Jupiter route fees (in input-token units).</summary>
        public decimal NetworkFee  { get; init; }
        /// <summary>Slippage tolerance used for this quote, in bps.</summary>
        public int     SlippageBps { get; init; }
        /// <summary>Opaque route plan from Jupiter — forwarded verbatim to /swap.</summary>
        public string  RoutePlan   { get; init; }
    }

    /// <summary>Input token options for the swap panel. v1 exposes USDC only.</summary>
    public enum SwapInputToken
    {
        USDC = 0,
        SOL  = 1   // reserved for v2 — not shown in UI until explicitly enabled
    }
}
```

---

## 3. `CoreServices` additions — `DeNelle.Core`

**Existing file:** `Assets/_Modules/Core/CoreServices.cs`

Add alongside the existing `Hud` / `Audio` slots:

```csharp
// ── Jupiter swap service ─────────────────────────────────────────────────
public static IJupiterService Jupiter { get; private set; }

public static void RegisterJupiter(IJupiterService svc)
{
    if (Jupiter != null && Jupiter != svc)
        Debug.LogWarning("[CoreServices] Replacing existing IJupiterService registration.");
    Jupiter = svc;
}

public static void UnregisterJupiter(IJupiterService svc)
{
    if (Jupiter == svc) Jupiter = null;
}
```

---

## 4. `SwapFeeConfig` ScriptableObject — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/SwapFeeConfig.cs`
**Asset:** `Assets/_Modules/Web3/Generated/SwapFeeConfig.asset`

```csharp
using UnityEngine;

namespace DeNelle.Web3
{
    /// <summary>
    /// Project-level swap fee settings. Create one asset via
    /// Assets > Create > Defenders > Swap Fee Config.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Defenders/Swap Fee Config",
        fileName = "SwapFeeConfig")]
    public sealed class SwapFeeConfig : ScriptableObject
    {
        [Tooltip("Platform fee in basis points (100 bps = 1%). " +
                 "Recommended range: 10–30 (0.1 %–0.3 %). " +
                 "Jupiter supports 0–10000.")]
        [Range(0, 100)]
        [SerializeField] private int _platformFeeBps = 20;   // 0.2 % default

        [Tooltip("Solana wallet address that receives the platform fee.")]
        [SerializeField] private string _feeWalletAddress = "";

        [Tooltip("Slippage tolerance in basis points. 50 = 0.5 %.")]
        [Range(10, 500)]
        [SerializeField] private int _slippageBps = 50;

        [Tooltip("Show the SOL input option in the swap panel (v2 feature). " +
                 "Leave off for v1.")]
        [SerializeField] private bool _enableSolInput = false;

        public int    PlatformFeeBps    => _platformFeeBps;
        public string FeeWalletAddress  => _feeWalletAddress;
        public int    SlippageBps       => _slippageBps;
        public bool   EnableSolInput    => _enableSolInput;
    }
}
```

---

## 5. `JupiterSwapService` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/JupiterSwapService.cs`

```csharp
// =============================================================================
// JupiterSwapService — fetches quotes + swap transactions from Jupiter v6 API.
// Implements IJupiterService; registered with CoreServices on Awake.
// =============================================================================

using System;
using System.Text;
using System.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Web3;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace DeNelle.Web3
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class JupiterSwapService : MonoBehaviour, IJupiterService
    {
        // ── Jupiter REST endpoints ───────────────────────────────────────────
        private const string QuoteUrl = "https://quote-api.jup.ag/v6/quote";
        private const string SwapUrl  = "https://quote-api.jup.ag/v6/swap";

        // ── Token mint addresses ─────────────────────────────────────────────
        // USDC (mainnet)
        private const string MintUSDC = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
        // SOL (wrapped, mainnet)
        private const string MintSOL  = "So11111111111111111111111111111111111111112";
        // SKR — update this to the live mint address when deployed.
        [SerializeField]
        [Tooltip("SKR SPL token mint address (mainnet).")]
        private string _skrMint = "REPLACE_WITH_SKR_MINT_ADDRESS";

        [SerializeField] private SwapFeeConfig _feeConfig;
        [SerializeField] private UIDocument    _document;

        // ── Panel controller (resolved at runtime) ───────────────────────────
        private JupiterSwapPanelController _panelController;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            CoreServices.RegisterJupiter(this);
        }

        private void Start()
        {
            _panelController = GetComponent<JupiterSwapPanelController>();
            if (_panelController == null)
                Debug.LogWarning("[JupiterSwapService] No JupiterSwapPanelController " +
                                 "on this GameObject — panel will not open.");
            // Panel starts hidden.
            SetPanelVisible(false);
        }

        private void OnDestroy() => CoreServices.UnregisterJupiter(this);

        // =====================================================================
        //  IJupiterService
        // =====================================================================

        public void OpenSwapPanel(decimal minimumSkr = 0m)
        {
            SetPanelVisible(true);
            _panelController?.Initialise(minimumSkr, _feeConfig);
        }

        public void CloseSwapPanel() => SetPanelVisible(false);

        public async Task<SwapQuote?> GetQuoteAsync(SwapInputToken input, decimal inputAmount)
        {
            if (_feeConfig == null)
            {
                Debug.LogWarning("[JupiterSwapService] SwapFeeConfig not assigned.");
                return null;
            }

            string inputMint = input == SwapInputToken.SOL ? MintSOL : MintUSDC;
            // Jupiter amounts are in the token's smallest unit.
            // USDC = 6 decimals, SOL = 9 decimals.
            int decimals    = input == SwapInputToken.SOL ? 9 : 6;
            long amountUnits = (long)(inputAmount * (decimal)Math.Pow(10, decimals));

            string url = $"{QuoteUrl}" +
                         $"?inputMint={inputMint}" +
                         $"&outputMint={_skrMint}" +
                         $"&amount={amountUnits}" +
                         $"&slippageBps={_feeConfig.SlippageBps}" +
                         $"&platformFeeBps={_feeConfig.PlatformFeeBps}" +
                         $"&feeAccount={_feeConfig.FeeWalletAddress}";

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Accept", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JupiterSwapService] Quote request failed: {req.error}");
                return null;
            }

            return ParseQuote(req.downloadHandler.text, inputAmount, decimals);
        }

        // =====================================================================
        //  Quote parsing (minimal — no third-party JSON library required)
        // =====================================================================

        /// <summary>
        /// Parses the Jupiter /v6/quote JSON response into a <see cref="SwapQuote"/>.
        /// Uses <see cref="JsonUtility"/> with a private DTO to avoid adding
        /// a JSON library dependency.
        /// </summary>
        private SwapQuote ParseQuote(string json, decimal inputAmount, int inputDecimals)
        {
            try
            {
                var dto = JsonUtility.FromJson<QuoteResponseDto>(json);

                // SKR output — assumes SKR has 6 decimals; adjust if different.
                const int skrDecimals = 6;
                decimal skrOut = dto.outAmount / (decimal)Math.Pow(10, skrDecimals);

                decimal rate = inputAmount > 0 ? skrOut / inputAmount : 0m;

                // Platform fee (in input-token units)
                decimal platformFee = 0m;
                if (dto.platformFee != null && long.TryParse(dto.platformFee.amount, out long pfRaw))
                    platformFee = pfRaw / (decimal)Math.Pow(10, inputDecimals);

                return new SwapQuote
                {
                    SkrOut       = skrOut,
                    Rate         = rate,
                    PlatformFee  = platformFee,
                    NetworkFee   = 0.000005m,   // Solana base fee; refine with actual estimate
                    SlippageBps  = _feeConfig.SlippageBps,
                    RoutePlan    = json          // forwarded verbatim to /swap
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JupiterSwapService] Quote parse error: {ex.Message}");
                return null;
            }
        }

        // =====================================================================
        //  Swap transaction (signs via wallet bridge — see §7)
        // =====================================================================

        /// <summary>
        /// Requests a serialised swap transaction from Jupiter and forwards it
        /// to the wallet bridge for signing + submission.
        /// </summary>
        public async Task<bool> ExecuteSwapAsync(SwapQuote quote, string userPublicKey)
        {
            if (quote == null || string.IsNullOrEmpty(userPublicKey)) return false;

            // Build the /swap request body.
            string body = $"{{" +
                          $"\"quoteResponse\":{quote.RoutePlan}," +
                          $"\"userPublicKey\":\"{userPublicKey}\"," +
                          $"\"wrapAndUnwrapSol\":true" +
                          $"}}";

            using var req = new UnityWebRequest(SwapUrl, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JupiterSwapService] Swap request failed: {req.error}");
                return false;
            }

            // Forward the serialised transaction to the wallet bridge for signing.
            // WalletBridge is out of scope for WO-43 — stub call here.
            string swapJson = req.downloadHandler.text;
            WalletBridgeStub.SignAndSendTransaction(swapJson, onSuccess: sig =>
            {
                Debug.Log($"[JupiterSwapService] Swap confirmed. Signature: {sig}");
                CloseSwapPanel();
                // TODO: refresh SKR balance via SKR balance service (future WO)
            });

            return true;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private void SetPanelVisible(bool visible)
        {
            if (_document?.rootVisualElement != null)
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── JSON DTOs ────────────────────────────────────────────────────────

        [Serializable]
        private sealed class QuoteResponseDto
        {
            public long         outAmount;
            public PlatformFeeDto platformFee;
        }

        [Serializable]
        private sealed class PlatformFeeDto
        {
            public string amount;
            public int    feeBps;
        }
    }
}
```

---

## 6. `JupiterSwapPanel.uxml` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/UI/JupiterSwapPanel.uxml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<!--
  JupiterSwapPanel.uxml — in-app SKR swap sheet (WO-43).
  ========================================================================
  Slides up as an overlay. Driven by JupiterSwapPanelController.

  Binding contract (names must not change):
    swap-overlay        VisualElement — full-screen dimmer tap-to-dismiss
    swap-sheet          VisualElement — the visible panel
    swap-close-btn      Button        — explicit close ×
    swap-input-amount   TextField     — user enters USDC amount
    swap-input-token    Label         — "USDC" (static in v1)
    swap-skr-out        Label         — "≈ 420.00 SKR" updated on quote
    swap-rate           Label         — "1 USDC = 12.34 SKR"
    swap-platform-fee   Label         — "Service fee: 0.04 USDC (0.2 %)"
    swap-network-fee    Label         — "Network fee: ~0.000005 SOL"
    swap-status         Label         — loading / error / success messages
    swap-confirm-btn    Button        — disabled until quote is fresh
-->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="JupiterSwapPanel.uss" />

    <!-- Full-screen overlay dimmer -->
    <ui:VisualElement name="swap-overlay" class="swap-overlay">

        <!-- The bottom sheet -->
        <ui:VisualElement name="swap-sheet" class="swap-sheet">

            <!-- Header row -->
            <ui:VisualElement name="swap-header" class="swap-header">
                <ui:Label text="Swap to SKR" class="swap-title" />
                <ui:Button name="swap-close-btn" text="×" class="swap-close-btn" />
            </ui:VisualElement>

            <!-- Input row -->
            <ui:VisualElement name="swap-input-row" class="swap-input-row">
                <ui:TextField name="swap-input-amount"
                              value="0"
                              class="swap-input-amount"
                              keyboard-type="DecimalPad" />
                <ui:Label name="swap-input-token"
                          text="USDC"
                          class="swap-input-token" />
            </ui:VisualElement>

            <!-- Output row -->
            <ui:VisualElement name="swap-output-row" class="swap-output-row">
                <ui:Label text="You receive" class="swap-output-label" />
                <ui:Label name="swap-skr-out" text="—" class="swap-skr-out" />
            </ui:VisualElement>

            <!-- Fee breakdown -->
            <ui:VisualElement name="swap-fee-block" class="swap-fee-block">
                <ui:VisualElement class="swap-fee-row">
                    <ui:Label text="Exchange rate" class="swap-fee-key" />
                    <ui:Label name="swap-rate" text="—" class="swap-fee-val" />
                </ui:VisualElement>
                <ui:VisualElement class="swap-fee-row">
                    <ui:Label text="Service fee" class="swap-fee-key" />
                    <ui:Label name="swap-platform-fee" text="—" class="swap-fee-val" />
                </ui:VisualElement>
                <ui:VisualElement class="swap-fee-row">
                    <ui:Label text="Network fee" class="swap-fee-key" />
                    <ui:Label name="swap-network-fee" text="—" class="swap-fee-val" />
                </ui:VisualElement>
            </ui:VisualElement>

            <!-- Status / error line -->
            <ui:Label name="swap-status" text="" class="swap-status" />

            <!-- Confirm CTA -->
            <ui:Button name="swap-confirm-btn"
                       text="Swap Now"
                       class="swap-confirm-btn swap-confirm-btn--disabled" />

            <!-- Powered-by line -->
            <ui:Label text="Powered by Jupiter Aggregator"
                      class="swap-powered-by" />

        </ui:VisualElement>

    </ui:VisualElement>
</ui:UXML>
```

---

## 7. `JupiterSwapPanel.uss` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/UI/JupiterSwapPanel.uss`

```uss
/*
 * JupiterSwapPanel.uss — swap bottom-sheet overlay.  WO-43.
 * -----------------------------------------------------------
 * Colours: deep indigo base, amber accents — matches SelectScreen.uss
 * palette so the panel feels at home on the hero-select screen.
 */

/* Full-screen dimmer */
.swap-overlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.65);
    flex-direction: column;
    justify-content: flex-end;
    align-items: stretch;
}

/* Bottom sheet */
.swap-sheet {
    background-color: rgba(18, 12, 30, 0.98);
    border-top-left-radius: 20px;
    border-top-right-radius: 20px;
    border-top-width: 2px;
    border-color: rgb(245, 166, 35);
    padding: 20px 24px 32px 24px;
    flex-direction: column;
    align-items: stretch;
}

/* Header */
.swap-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
}

.swap-title {
    font-size: 18px;
    -unity-font-style: bold;
    color: rgb(237, 233, 250);
}

.swap-close-btn {
    width: 32px;
    height: 32px;
    font-size: 22px;
    border-radius: 16px;
    border-width: 1px;
    border-color: rgba(196, 181, 253, 0.30);
    background-color: rgba(255, 255, 255, 0.05);
    color: rgba(196, 181, 253, 0.70);
    -unity-text-align: middle-center;
}

.swap-close-btn:hover {
    border-color: rgba(245, 166, 35, 0.55);
    color: rgb(245, 166, 35);
}

/* Input row */
.swap-input-row {
    flex-direction: row;
    align-items: center;
    background-color: rgba(38, 26, 52, 0.90);
    border-radius: 12px;
    border-width: 1px;
    border-color: rgba(124, 58, 237, 0.35);
    padding: 0 16px;
    height: 56px;
    margin-bottom: 14px;
}

.swap-input-amount {
    flex-grow: 1;
    font-size: 22px;
    -unity-font-style: bold;
    color: rgb(237, 233, 250);
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    -unity-text-align: middle-left;
}

.swap-input-token {
    font-size: 14px;
    -unity-font-style: bold;
    color: rgba(196, 181, 253, 0.80);
    margin-left: 8px;
}

/* Output row */
.swap-output-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 18px;
}

.swap-output-label {
    font-size: 13px;
    color: rgba(196, 181, 253, 0.65);
}

.swap-skr-out {
    font-size: 22px;
    -unity-font-style: bold;
    color: rgb(245, 166, 35);
}

/* Fee breakdown */
.swap-fee-block {
    background-color: rgba(38, 26, 52, 0.60);
    border-radius: 10px;
    padding: 12px 14px;
    margin-bottom: 14px;
}

.swap-fee-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 6px;
}

.swap-fee-key {
    font-size: 12px;
    color: rgba(196, 181, 253, 0.60);
}

.swap-fee-val {
    font-size: 12px;
    color: rgba(237, 233, 250, 0.85);
}

/* Status line */
.swap-status {
    font-size: 12px;
    -unity-font-style: italic;
    color: rgba(245, 166, 35, 0.80);
    -unity-text-align: middle-center;
    min-height: 18px;
    margin-bottom: 10px;
}

.swap-status--error {
    color: rgba(255, 90, 90, 0.90);
}

/* Confirm button */
.swap-confirm-btn {
    height: 52px;
    font-size: 16px;
    -unity-font-style: bold;
    border-radius: 12px;
    border-width: 0;
    background-color: rgb(245, 166, 35);
    color: rgb(22, 14, 6);
    margin-bottom: 12px;
}

.swap-confirm-btn:hover {
    background-color: rgb(255, 186, 70);
}

.swap-confirm-btn--disabled {
    background-color: rgba(255, 255, 255, 0.08);
    color: rgba(237, 233, 250, 0.30);
}

/* Powered-by */
.swap-powered-by {
    font-size: 11px;
    color: rgba(196, 181, 253, 0.35);
    -unity-text-align: middle-center;
}
```

---

## 8. `JupiterSwapPanelController` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/JupiterSwapPanelController.cs`

```csharp
// =============================================================================
// JupiterSwapPanelController — drives JupiterSwapPanel.uxml.
// Debounces user input, fires GetQuoteAsync, refreshes the fee breakdown,
// and triggers ExecuteSwapAsync on confirm.
// =============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using DeNelle.Core.Web3;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Web3
{
    [RequireComponent(typeof(JupiterSwapService))]
    public sealed class JupiterSwapPanelController : MonoBehaviour
    {
        // Element name constants — match JupiterSwapPanel.uxml
        private const string OverlayName     = "swap-overlay";
        private const string CloseBtnName    = "swap-close-btn";
        private const string InputAmountName = "swap-input-amount";
        private const string SkrOutName      = "swap-skr-out";
        private const string RateName        = "swap-rate";
        private const string PlatformFeeName = "swap-platform-fee";
        private const string NetworkFeeName  = "swap-network-fee";
        private const string StatusName      = "swap-status";
        private const string ConfirmBtnName  = "swap-confirm-btn";

        private const string ConfirmDisabledClass = "swap-confirm-btn--disabled";
        private const string StatusErrorClass     = "swap-status--error";

        // How long to wait after the last keystroke before firing a quote request.
        private const float QuoteDebounceSeconds = 0.6f;

        [SerializeField] private UIDocument _document;

        // ── Bound elements ───────────────────────────────────────────────────
        private VisualElement _overlay;
        private Button        _closeBtn;
        private TextField     _inputAmount;
        private Label         _skrOut;
        private Label         _rate;
        private Label         _platformFee;
        private Label         _networkFee;
        private Label         _status;
        private Button        _confirmBtn;

        // ── State ────────────────────────────────────────────────────────────
        private JupiterSwapService  _service;
        private SwapFeeConfig       _feeConfig;
        private SwapQuote           _latestQuote;
        private decimal             _currentInput;
        private bool                _quoteLoading;
        private CancellationTokenSource _debounceCts;

        // ── Connected wallet public key (set by wallet bridge) ───────────────
        public string ConnectedWalletKey { get; set; } = "";

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            _service  = GetComponent<JupiterSwapService>();
            _document = _document != null ? _document : GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (_document?.rootVisualElement == null) return;
            BindElements();
        }

        private void OnDisable()
        {
            _debounceCts?.Cancel();
            if (_closeBtn   != null) _closeBtn.clicked   -= OnCloseTapped;
            if (_confirmBtn != null) _confirmBtn.clicked -= OnConfirmTapped;
            if (_inputAmount != null)
                _inputAmount.UnregisterValueChangedCallback(OnInputChanged);
        }

        // =====================================================================
        //  Public API (called by JupiterSwapService.OpenSwapPanel)
        // =====================================================================

        public void Initialise(decimal minimumSkr, SwapFeeConfig feeConfig)
        {
            _feeConfig   = feeConfig;
            _latestQuote = null;
            _currentInput = 0m;

            if (_inputAmount != null) _inputAmount.value = "0";
            ClearQuoteDisplay();
            SetStatus("Enter an amount to see the rate.", isError: false);
            SetConfirmEnabled(false);

            // If a minimum is requested, pre-fill a suggested USDC amount
            // that would cover it (rough estimate — will refine on first quote).
            if (minimumSkr > 0 && _inputAmount != null)
                _inputAmount.value = Math.Ceiling(minimumSkr / 10m).ToString("F2");
        }

        // =====================================================================
        //  Element binding
        // =====================================================================

        private void BindElements()
        {
            var root     = _document.rootVisualElement;
            _overlay     = root.Q<VisualElement>(OverlayName);
            _closeBtn    = root.Q<Button>(CloseBtnName);
            _inputAmount = root.Q<TextField>(InputAmountName);
            _skrOut      = root.Q<Label>(SkrOutName);
            _rate        = root.Q<Label>(RateName);
            _platformFee = root.Q<Label>(PlatformFeeName);
            _networkFee  = root.Q<Label>(NetworkFeeName);
            _status      = root.Q<Label>(StatusName);
            _confirmBtn  = root.Q<Button>(ConfirmBtnName);

            if (_closeBtn   != null) _closeBtn.clicked   += OnCloseTapped;
            if (_confirmBtn != null) _confirmBtn.clicked += OnConfirmTapped;
            if (_inputAmount != null)
                _inputAmount.RegisterValueChangedCallback(OnInputChanged);

            // Tap the overlay background to dismiss.
            _overlay?.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == _overlay) OnCloseTapped();
            });
        }

        // =====================================================================
        //  Input → debounced quote
        // =====================================================================

        private void OnInputChanged(ChangeEvent<string> evt)
        {
            SetConfirmEnabled(false);
            ClearQuoteDisplay();

            if (!decimal.TryParse(evt.newValue, out decimal amount) || amount <= 0)
            {
                SetStatus("Enter a valid amount.", isError: false);
                return;
            }

            _currentInput = amount;
            SetStatus("Getting rate…", isError: false);

            // Cancel any in-flight debounce and start a new one.
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            _ = DebounceQuote(_debounceCts.Token);
        }

        private async Task DebounceQuote(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(QuoteDebounceSeconds), ct)
                      .ContinueWith(_ => { }, ct);   // swallow cancellation

            if (ct.IsCancellationRequested) return;

            _quoteLoading = true;
            var quote = await _service.GetQuoteAsync(SwapInputToken.USDC, _currentInput);
            _quoteLoading = false;

            if (ct.IsCancellationRequested) return;

            if (quote == null)
            {
                SetStatus("Could not fetch rate. Check connection.", isError: true);
                return;
            }

            _latestQuote = quote;
            RefreshQuoteDisplay(quote);
            SetStatus("", isError: false);
            SetConfirmEnabled(!string.IsNullOrEmpty(ConnectedWalletKey));

            if (string.IsNullOrEmpty(ConnectedWalletKey))
                SetStatus("Connect your wallet to swap.", isError: false);
        }

        // =====================================================================
        //  Display helpers
        // =====================================================================

        private void RefreshQuoteDisplay(SwapQuote q)
        {
            if (_skrOut      != null) _skrOut.text      = $"≈ {q.SkrOut:F2} SKR";
            if (_rate        != null) _rate.text        = $"1 USDC = {q.Rate:F4} SKR";
            if (_platformFee != null)
            {
                decimal feePct = (_feeConfig?.PlatformFeeBps ?? 20) / 100m;
                _platformFee.text = $"{q.PlatformFee:F4} USDC ({feePct:F1}%)";
            }
            if (_networkFee  != null) _networkFee.text  = $"~{q.NetworkFee:F6} SOL";
        }

        private void ClearQuoteDisplay()
        {
            if (_skrOut      != null) _skrOut.text      = "—";
            if (_rate        != null) _rate.text        = "—";
            if (_platformFee != null) _platformFee.text = "—";
            if (_networkFee  != null) _networkFee.text  = "—";
        }

        private void SetStatus(string msg, bool isError)
        {
            if (_status == null) return;
            _status.text = msg;
            _status.EnableInClassList(StatusErrorClass, isError);
        }

        private void SetConfirmEnabled(bool enabled)
        {
            if (_confirmBtn == null) return;
            _confirmBtn.SetEnabled(enabled);
            _confirmBtn.EnableInClassList(ConfirmDisabledClass, !enabled);
        }

        // =====================================================================
        //  Actions
        // =====================================================================

        private void OnCloseTapped() => _service.CloseSwapPanel();

        private async void OnConfirmTapped()
        {
            if (_latestQuote == null || _quoteLoading) return;
            SetConfirmEnabled(false);
            SetStatus("Sending to wallet for approval…", isError: false);

            bool ok = await _service.ExecuteSwapAsync(_latestQuote, ConnectedWalletKey);
            if (!ok) SetStatus("Swap failed. Please try again.", isError: true);
        }
    }
}
```

---

## 9. `WalletBridgeStub` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/WalletBridgeStub.cs`

This is a placeholder. Replace with the actual Solana wallet SDK (Phantom
deep-link, WalletConnect, or a Unity Solana SDK) in a follow-up WO.

```csharp
using System;
using UnityEngine;

namespace DeNelle.Web3
{
    /// <summary>
    /// Stub wallet bridge — logs the serialised transaction and fires the
    /// success callback with a fake signature so the rest of the flow
    /// can be tested without a live wallet. Replace in WO-?? with the
    /// real Solana wallet SDK (Phantom deep-link / WalletConnect).
    /// </summary>
    public static class WalletBridgeStub
    {
        public static void SignAndSendTransaction(
            string serialisedSwapJson,
            Action<string> onSuccess,
            Action<string> onError = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[WalletBridgeStub] Would sign + send transaction. " +
                      "Replace this stub with the real wallet SDK.");
            Debug.Log($"[WalletBridgeStub] Payload length: {serialisedSwapJson.Length} chars");
            // Simulate a successful signing after a short delay.
            onSuccess?.Invoke("STUB_SIG_" + System.Guid.NewGuid().ToString("N")[..8]);
#else
            Debug.LogError("[WalletBridgeStub] WalletBridgeStub reached in a release " +
                           "build. Wire the real wallet SDK before shipping.");
            onError?.Invoke("Wallet bridge not configured.");
#endif
        }
    }
}
```

---

## 10. `en.json` additions

```json
"swap.title":        "Swap to SKR",
"swap.inputLabel":   "You send",
"swap.outputLabel":  "You receive",
"swap.rateLabel":    "Exchange rate",
"swap.feeLabel":     "Service fee",
"swap.networkLabel": "Network fee",
"swap.confirm":      "Swap Now",
"swap.poweredBy":    "Powered by Jupiter Aggregator",
"swap.statusEnter":  "Enter an amount to see the rate.",
"swap.statusLoading":"Getting rate…",
"swap.statusConnect":"Connect your wallet to swap.",
"swap.statusSigning":"Sending to wallet for approval…",
"swap.statusError":  "Could not fetch rate. Check connection.",
"swap.statusFailed": "Swap failed. Please try again."
```

*(Controller uses the hardcoded strings in §8 for v1 — migrate to
`CanonStrings.Locale()` in the same pass as other localisation cleanup.)*

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Web3/DeNelle.Web3.asmdef` | **Create** — new assembly |
| `Assets/_Modules/Core/Web3/IJupiterService.cs` | **Create** — interface + DTOs |
| `Assets/_Modules/Core/CoreServices.cs` | **Edit** — add `Jupiter` slot |
| `Assets/_Modules/Web3/SwapFeeConfig.cs` | **Create** — ScriptableObject |
| `Assets/_Modules/Web3/Generated/SwapFeeConfig.asset` | **Create via menu** — Defenders > Swap Fee Config |
| `Assets/_Modules/Web3/JupiterSwapService.cs` | **Create** — API + lifecycle |
| `Assets/_Modules/Web3/UI/JupiterSwapPanel.uxml` | **Create** — swap sheet layout |
| `Assets/_Modules/Web3/UI/JupiterSwapPanel.uss` | **Create** — swap sheet styles |
| `Assets/_Modules/Web3/JupiterSwapPanelController.cs` | **Create** — UI controller |
| `Assets/_Modules/Web3/WalletBridgeStub.cs` | **Create** — signing stub |
| `Assets/_Modules/Core/Localisation/en.json` | **Edit** — add swap string keys |

---

## Acceptance Criteria

- [ ] `DeNelle.Web3.asmdef` compiles — references `DeNelle.Core` only
- [ ] `CoreServices.Jupiter` slot present; no compile errors in `DeNelle.Core`
- [ ] `SwapFeeConfig` asset created with default 0.2 % fee (20 bps)
- [ ] `JupiterSwapService.GetQuoteAsync(USDC, 10)` returns a non-null
      `SwapQuote` in the Editor (requires internet; check console)
- [ ] Calling `CoreServices.Jupiter?.OpenSwapPanel()` from any scene shows the
      bottom-sheet overlay on top of the current screen
- [ ] Typing a USDC amount into the input field triggers a live quote after
      ~0.6 s debounce; SKR output + fee breakdown update correctly
- [ ] Confirm button is disabled when no wallet is connected
- [ ] In Editor/dev builds `WalletBridgeStub` fires the success callback and
      the panel closes; console shows the stub warning
- [ ] Panel dismisses on × button or overlay tap
- [ ] Pet-select and hero-select screens are unchanged (no asmdef changes there)
- [ ] SOL input option exists in `SwapInputToken` enum but is not shown in
      the v1 UI (`_enableSolInput = false` in SwapFeeConfig)

---

## Notes for Implementor

**SKR mint address** — `_skrMint` in `JupiterSwapService` is set to the
placeholder `REPLACE_WITH_SKR_MINT_ADDRESS`. This must be filled with the live
SPL token mint before any real swap can occur. The rest of the code will work
in the Editor with the stub wallet bridge regardless.

**Platform fee account** — Jupiter requires the `feeAccount` parameter to be a
token account (not a wallet address) for the *output* token (SKR). Before going
live, create an associated token account for the fee wallet that holds SKR, and
set that ATA address in `SwapFeeConfig._feeWalletAddress`. See Jupiter docs:
https://station.jup.ag/docs/apis/adding-fees

**Wallet signing (WalletBridgeStub)** — the real implementation depends on
which wallet SDK the project adopts. For mobile: Phantom deep-link via
`Application.OpenURL("phantom://v1/signAndSendTransaction?...")`. For WebGL:
JS bridge via `jslib` interop. Raise a separate WO to implement this once the
wallet strategy is confirmed.

**USDC decimals** — 6 decimal places (standard SPL). SKR decimal count is
assumed 6 in `ParseQuote`; verify against the live mint metadata and adjust
`skrDecimals` if different.
