# WORK ORDER 43 — Jupiter Swap UI Panel (Phase 1)

**Status:** CLOSED — DUPLICATE of WO-210 (owner-approved sweep 2026-08-09: file 210 is the re-mint of this WO, identical header)
**Date:** 2026-05-26
**Priority:** High
**Scope:** Small / Low Risk
**Depends on:** WO-41 (`CoreServices` pattern)

---

## Goal

Build the visual swap panel and clean architecture foundation so we can test
the UI flow before connecting real money movement. No live Jupiter API calls
in this phase — the controller wires up against a `MockJupiterSwapService`
that returns realistic-looking fake data instantly.

---

## Deliverables

- `DeNelle.Web3` asmdef
- `IJupiterService` interface + `SwapQuote` / `SwapInputToken` in `DeNelle.Core`
- `CoreServices.Jupiter` registration slot
- `SwapFeeConfig` ScriptableObject
- `JupiterSwapPanel.uxml` + `JupiterSwapPanel.uss`
- `JupiterSwapPanelController` (input debouncing, UI updates, open/close)
- `MockJupiterSwapService` (fake quote data — no HTTP)
- `WalletBridgeStub` (fake signing — no Solana SDK)

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
// Registered in CoreServices. Implemented by MockJupiterSwapService (WO-43)
// and then replaced by JupiterSwapService (WO-44).
// =============================================================================

using System.Threading.Tasks;

namespace DeNelle.Core.Web3
{
    public interface IJupiterService
    {
        /// <summary>Opens the swap panel. Pass minimumSkr > 0 to pre-fill a
        /// suggested amount that would cover that SKR requirement.</summary>
        void OpenSwapPanel(decimal minimumSkr = 0m);

        void CloseSwapPanel();

        /// <summary>Returns a quote for swapping <paramref name="inputAmount"/>
        /// of <paramref name="input"/> into SKR. Returns null on error.</summary>
        Task<SwapQuote> GetQuoteAsync(SwapInputToken input, decimal inputAmount);
    }

    public sealed class SwapQuote
    {
        public decimal SkrOut      { get; init; }
        public decimal Rate        { get; init; }
        public decimal PlatformFee { get; init; }
        public decimal NetworkFee  { get; init; }
        public int     SlippageBps { get; init; }
        /// <summary>Opaque route plan — forwarded verbatim to /v6/swap in WO-45.</summary>
        public string  RoutePlan   { get; init; }
    }

    /// <summary>v1 exposes USDC only; SOL is reserved for v2.</summary>
    public enum SwapInputToken
    {
        USDC = 0,
        SOL  = 1
    }
}
```

---

## 3. `CoreServices` additions — `DeNelle.Core`

**Existing file:** `Assets/_Modules/Core/CoreServices.cs`

Add alongside the existing `Hud` / `Audio` slots:

```csharp
using DeNelle.Core.Web3;

// ── Jupiter swap service ─────────────────────────────────────────────────
public static IJupiterService Jupiter { get; private set; }

public static void RegisterJupiter(IJupiterService svc)
{
    if (Jupiter != null && Jupiter != svc)
        Debug.LogWarning("[CoreServices] Replacing existing IJupiterService.");
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
**Asset:** Create via **Assets > Create > Defenders > Swap Fee Config**

```csharp
using UnityEngine;

namespace DeNelle.Web3
{
    [CreateAssetMenu(menuName = "Defenders/Swap Fee Config",
                     fileName = "SwapFeeConfig")]
    public sealed class SwapFeeConfig : ScriptableObject
    {
        [Tooltip("Platform fee in basis points. 20 = 0.2 %.")]
        [Range(0, 100)]
        [SerializeField] private int _platformFeeBps = 20;

        [Tooltip("Fee wallet address (SPL associated token account for SKR). " +
                 "Leave blank in Phase 1 — required before going live in WO-45.")]
        [SerializeField] private string _feeWalletAddress = "";

        [Tooltip("Slippage tolerance in bps. 50 = 0.5 %.")]
        [Range(10, 500)]
        [SerializeField] private int _slippageBps = 50;

        [Tooltip("Show SOL as an input option. Off for v1 / Phase 1.")]
        [SerializeField] private bool _enableSolInput = false;

        public int    PlatformFeeBps   => _platformFeeBps;
        public string FeeWalletAddress => _feeWalletAddress;
        public int    SlippageBps      => _slippageBps;
        public bool   EnableSolInput   => _enableSolInput;
    }
}
```

---

## 5. `JupiterSwapPanel.uxml` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/UI/JupiterSwapPanel.uxml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<!--
  JupiterSwapPanel.uxml — in-app SKR swap bottom-sheet (WO-43, Phase 1).
  ========================================================================
  Binding contract (names must not change across phases):
    swap-overlay        VisualElement — full-screen dimmer, tap to dismiss
    swap-sheet          VisualElement — the visible bottom panel
    swap-close-btn      Button        — explicit ×
    swap-input-amount   TextField     — user enters USDC amount
    swap-input-token    Label         — "USDC" (static v1)
    swap-skr-out        Label         — "≈ 420.00 SKR"
    swap-rate           Label         — "1 USDC = 12.34 SKR"
    swap-platform-fee   Label         — "0.04 USDC (0.2 %)"
    swap-network-fee    Label         — "~0.000005 SOL"
    swap-status         Label         — loading / error / info messages
    swap-confirm-btn    Button        — disabled until wallet connected
-->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="JupiterSwapPanel.uss" />

    <ui:VisualElement name="swap-overlay" class="swap-overlay">

        <ui:VisualElement name="swap-sheet" class="swap-sheet">

            <ui:VisualElement name="swap-header" class="swap-header">
                <ui:Label text="Swap to SKR" class="swap-title" />
                <ui:Button name="swap-close-btn" text="×" class="swap-close-btn" />
            </ui:VisualElement>

            <ui:VisualElement name="swap-input-row" class="swap-input-row">
                <ui:TextField name="swap-input-amount"
                              value="0"
                              class="swap-input-amount" />
                <ui:Label name="swap-input-token"
                          text="USDC"
                          class="swap-input-token" />
            </ui:VisualElement>

            <ui:VisualElement name="swap-output-row" class="swap-output-row">
                <ui:Label text="You receive" class="swap-output-label" />
                <ui:Label name="swap-skr-out" text="—" class="swap-skr-out" />
            </ui:VisualElement>

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

            <ui:Label name="swap-status" text="" class="swap-status" />

            <ui:Button name="swap-confirm-btn"
                       text="Swap Now"
                       class="swap-confirm-btn swap-confirm-btn--disabled" />

            <ui:Label text="Powered by Jupiter Aggregator"
                      class="swap-powered-by" />

        </ui:VisualElement>

    </ui:VisualElement>
</ui:UXML>
```

---

## 6. `JupiterSwapPanel.uss` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/UI/JupiterSwapPanel.uss`

```uss
/*
 * JupiterSwapPanel.uss — swap bottom-sheet. WO-43 Phase 1.
 * Palette: deep indigo base + amber accents (matches SelectScreen.uss).
 */

.swap-overlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.65);
    flex-direction: column;
    justify-content: flex-end;
    align-items: stretch;
}

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

.swap-powered-by {
    font-size: 11px;
    color: rgba(196, 181, 253, 0.35);
    -unity-text-align: middle-center;
}
```

---

## 7. `MockJupiterSwapService` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/MockJupiterSwapService.cs`

Returns synthetic quote data instantly (no HTTP, no wallet SDK). Replaced by
`JupiterSwapService` in WO-44.

```csharp
using System.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Web3;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Web3
{
    /// <summary>
    /// Phase-1 stub: returns fake SKR quotes so the UI can be tested end-to-end
    /// without a live Jupiter connection. Swap in JupiterSwapService (WO-44).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MockJupiterSwapService : MonoBehaviour, IJupiterService
    {
        // Fake exchange rate — 1 USDC = this many SKR.
        private const decimal FakeSKRPerUSDC = 12.34m;

        [SerializeField] private SwapFeeConfig _feeConfig;
        [SerializeField] private UIDocument    _document;

        private JupiterSwapPanelController _panelController;

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            CoreServices.RegisterJupiter(this);
        }

        private void Start()
        {
            _panelController = GetComponent<JupiterSwapPanelController>();
            SetPanelVisible(false);
        }

        private void OnDestroy() => CoreServices.UnregisterJupiter(this);

        // ── IJupiterService ──────────────────────────────────────────────────

        public void OpenSwapPanel(decimal minimumSkr = 0m)
        {
            SetPanelVisible(true);
            _panelController?.Initialise(minimumSkr, _feeConfig);
        }

        public void CloseSwapPanel() => SetPanelVisible(false);

        public Task<SwapQuote> GetQuoteAsync(SwapInputToken input, decimal inputAmount)
        {
            if (inputAmount <= 0) return Task.FromResult<SwapQuote>(null);

            int    feeBps      = _feeConfig != null ? _feeConfig.PlatformFeeBps : 20;
            int    slipBps     = _feeConfig != null ? _feeConfig.SlippageBps    : 50;
            decimal platformFee = inputAmount * feeBps / 10000m;
            decimal skrOut      = (inputAmount - platformFee) * FakeSKRPerUSDC;

            var quote = new SwapQuote
            {
                SkrOut      = skrOut,
                Rate        = FakeSKRPerUSDC,
                PlatformFee = platformFee,
                NetworkFee  = 0.000005m,
                SlippageBps = slipBps,
                RoutePlan   = "{\"mock\":true}"
            };
            return Task.FromResult(quote);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetPanelVisible(bool visible)
        {
            if (_document?.rootVisualElement != null)
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
```

---

## 8. `JupiterSwapPanelController` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/JupiterSwapPanelController.cs`

Unchanged between phases — only the service it calls changes.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DeNelle.Core.Web3;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Web3
{
    /// <summary>
    /// Drives JupiterSwapPanel.uxml. Calls IJupiterService.GetQuoteAsync
    /// (MockJupiterSwapService in WO-43, real service in WO-44).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class JupiterSwapPanelController : MonoBehaviour
    {
        // ── UXML element names ───────────────────────────────────────────────
        private const string CloseBtnName    = "swap-close-btn";
        private const string InputAmountName = "swap-input-amount";
        private const string SkrOutName      = "swap-skr-out";
        private const string RateName        = "swap-rate";
        private const string PlatformFeeName = "swap-platform-fee";
        private const string NetworkFeeName  = "swap-network-fee";
        private const string StatusName      = "swap-status";
        private const string ConfirmBtnName  = "swap-confirm-btn";
        private const string OverlayName     = "swap-overlay";

        private const string ConfirmDisabledClass = "swap-confirm-btn--disabled";
        private const string StatusErrorClass     = "swap-status--error";

        private const float QuoteDebounceSeconds = 0.6f;

        [SerializeField] private UIDocument _document;

        // ── Bound elements ───────────────────────────────────────────────────
        private Button    _closeBtn;
        private TextField _inputAmount;
        private Label     _skrOut, _rate, _platformFee, _networkFee, _status;
        private Button    _confirmBtn;

        // ── State ────────────────────────────────────────────────────────────
        private IJupiterService         _service;
        private SwapFeeConfig           _feeConfig;
        private SwapQuote               _latestQuote;
        private decimal                 _currentInput;
        private CancellationTokenSource _debounceCts;

        /// <summary>Set by the wallet bridge (WO-45) once the player connects.</summary>
        public string ConnectedWalletKey { get; set; } = "";

        // =====================================================================

        private void Awake()
        {
            _document = _document != null ? _document : GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Resolve the service from whichever MonoBehaviour on this GO
            // implements IJupiterService (Mock in WO-43, real in WO-44).
            _service = GetComponent<IJupiterService>() as IJupiterService
                       ?? (IJupiterService)GetComponent<MockJupiterSwapService>();

            if (_document?.rootVisualElement != null) BindElements();
        }

        private void OnDisable()
        {
            _debounceCts?.Cancel();
            if (_closeBtn    != null) _closeBtn.clicked    -= OnCloseTapped;
            if (_confirmBtn  != null) _confirmBtn.clicked  -= OnConfirmTapped;
            if (_inputAmount != null)
                _inputAmount.UnregisterValueChangedCallback(OnInputChanged);
        }

        // =====================================================================
        //  Public API
        // =====================================================================

        public void Initialise(decimal minimumSkr, SwapFeeConfig feeConfig)
        {
            _feeConfig    = feeConfig;
            _latestQuote  = null;
            _currentInput = 0m;

            if (_inputAmount != null) _inputAmount.SetValueWithoutNotify("0");
            ClearQuoteDisplay();
            SetStatus("Enter an amount to see the rate.", isError: false);
            SetConfirmEnabled(false);

            if (minimumSkr > 0 && _inputAmount != null)
            {
                // Pre-fill a round-number USDC suggestion to cover minimumSkr.
                decimal suggested = Math.Ceiling(minimumSkr / 10m);
                _inputAmount.SetValueWithoutNotify(suggested.ToString("F2"));
                // Kick off a quote immediately for the pre-filled amount.
                _currentInput = suggested;
                TriggerDebouncedQuote();
            }
        }

        // =====================================================================
        //  Binding
        // =====================================================================

        private void BindElements()
        {
            var root     = _document.rootVisualElement;
            var overlay  = root.Q<VisualElement>(OverlayName);
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

            overlay?.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == overlay) OnCloseTapped();
            });
        }

        // =====================================================================
        //  Input → debounced quote
        // =====================================================================

        private void OnInputChanged(ChangeEvent<string> evt)
        {
            SetConfirmEnabled(false);
            ClearQuoteDisplay();

            if (!decimal.TryParse(evt.newValue,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal amount) || amount <= 0)
            {
                SetStatus("Enter a valid amount.", isError: false);
                return;
            }

            _currentInput = amount;
            SetStatus("Getting rate…", isError: false);
            TriggerDebouncedQuote();
        }

        private void TriggerDebouncedQuote()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            _ = RunDebouncedQuote(_currentInput, _debounceCts.Token);
        }

        private async Task RunDebouncedQuote(decimal amount, CancellationToken ct)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(QuoteDebounceSeconds), ct); }
            catch (OperationCanceledException) { return; }

            if (ct.IsCancellationRequested || _service == null) return;

            var quote = await _service.GetQuoteAsync(SwapInputToken.USDC, amount);
            if (ct.IsCancellationRequested) return;

            if (quote == null)
            {
                SetStatus("Could not fetch rate. Check connection.", isError: true);
                return;
            }

            _latestQuote = quote;
            RefreshQuoteDisplay(quote);
            SetStatus("", isError: false);

            bool walletReady = !string.IsNullOrEmpty(ConnectedWalletKey);
            SetConfirmEnabled(walletReady);
            if (!walletReady) SetStatus("Connect your wallet to swap.", isError: false);
        }

        // =====================================================================
        //  Display
        // =====================================================================

        private void RefreshQuoteDisplay(SwapQuote q)
        {
            if (_skrOut      != null) _skrOut.text      = $"≈ {q.SkrOut:F2} SKR";
            if (_rate        != null) _rate.text        = $"1 USDC = {q.Rate:F4} SKR";
            if (_platformFee != null)
            {
                decimal pct = (_feeConfig?.PlatformFeeBps ?? 20) / 100m;
                _platformFee.text = $"{q.PlatformFee:F4} USDC ({pct:F1}%)";
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

        private void OnCloseTapped() => _service?.CloseSwapPanel();

        private void OnConfirmTapped()
        {
            if (_latestQuote == null) return;
            // Phase 1: log only. WO-45 wires the real swap execution.
            Debug.Log($"[JupiterSwapPanel] Confirm tapped — " +
                      $"{_currentInput:F2} USDC → {_latestQuote.SkrOut:F2} SKR (mock).");
            SetStatus("Swap confirmed (mock — no real transaction).", isError: false);
        }
    }
}
```

---

## 9. `WalletBridgeStub` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/WalletBridgeStub.cs`

Replaced by the real Solana wallet SDK in WO-45.

```csharp
using System;
using UnityEngine;

namespace DeNelle.Web3
{
    /// <summary>
    /// Phase-1/2 stub: fakes wallet signing so the rest of the flow can be
    /// tested without a live wallet. Replace with real SDK in WO-45.
    /// </summary>
    public static class WalletBridgeStub
    {
        public static void SignAndSendTransaction(
            string serialisedSwapJson,
            Action<string> onSuccess,
            Action<string> onError = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[WalletBridgeStub] Stub signing — no real transaction sent.");
            onSuccess?.Invoke("STUB_SIG_" + Guid.NewGuid().ToString("N")[..8]);
#else
            Debug.LogError("[WalletBridgeStub] Stub reached in release build. " +
                           "Wire the real wallet SDK before shipping.");
            onError?.Invoke("Wallet bridge not configured.");
#endif
        }
    }
}
```

---

## 10. Scene / GameObject setup

Create one GameObject in the scene (e.g., in the HeroSelect scene or a
persistent GameServices scene) with:

```
SwapServiceRoot  (GameObject)
  ├── UIDocument   (component — assign JupiterSwapPanel.uxml)
  ├── MockJupiterSwapService  (component — assign SwapFeeConfig asset)
  └── JupiterSwapPanelController  (component)
```

Start the panel hidden (controller's `Initialise` is called on `OpenSwapPanel`).

---

## Files to Create

| File | Action |
|---|---|
| `Assets/_Modules/Web3/DeNelle.Web3.asmdef` | Create |
| `Assets/_Modules/Core/Web3/IJupiterService.cs` | Create |
| `Assets/_Modules/Core/CoreServices.cs` | Edit — add Jupiter slot |
| `Assets/_Modules/Web3/SwapFeeConfig.cs` | Create |
| `Assets/_Modules/Web3/Generated/SwapFeeConfig.asset` | Create via menu |
| `Assets/_Modules/Web3/UI/JupiterSwapPanel.uxml` | Create |
| `Assets/_Modules/Web3/UI/JupiterSwapPanel.uss` | Create |
| `Assets/_Modules/Web3/MockJupiterSwapService.cs` | Create |
| `Assets/_Modules/Web3/JupiterSwapPanelController.cs` | Create |
| `Assets/_Modules/Web3/WalletBridgeStub.cs` | Create |

---

## Acceptance Criteria

- [ ] `DeNelle.Web3` compiles — references `DeNelle.Core` only
- [ ] `CoreServices.Jupiter` slot present; no compile errors in `DeNelle.Core`
- [ ] `SwapFeeConfig` asset created with default 20 bps fee
- [ ] Calling `CoreServices.Jupiter?.OpenSwapPanel()` from any scene shows the
      bottom-sheet panel
- [ ] Typing a USDC amount into the input field triggers a fake quote after
      ~0.6 s; SKR output + fee breakdown populate with the mock data
- [ ] Confirm button is disabled when `ConnectedWalletKey` is empty; pressing
      it while wired logs the mock-confirm message and no exception is thrown
- [ ] Panel closes on × button tap and on overlay tap
- [ ] No HTTP requests are made — confirmed by watching the Unity Profiler
      network section while using the panel
- [ ] Pet-select and hero-select screens are unchanged
