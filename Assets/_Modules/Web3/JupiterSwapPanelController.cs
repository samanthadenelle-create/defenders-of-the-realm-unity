// =============================================================================
// JupiterSwapPanelController — drives JupiterSwapPanel.uxml (WO-43).
// -----------------------------------------------------------------------------
// Debounces user input, fires JupiterSwapService.GetQuoteAsync, refreshes the
// fee breakdown, and triggers ExecuteSwapAsync on confirm. Binds elements by the
// name contract documented in JupiterSwapPanel.uxml.
//
// v1 uses hardcoded English strings (the swap.* keys exist in en.json for a
// later localisation pass via CanonStrings).
// =============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using DeNelle.Core.Web3;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Web3
{
    [RequireComponent(typeof(JupiterSwapService))]
    public sealed class JupiterSwapPanelController : MonoBehaviour
    {
        // Element name constants — match JupiterSwapPanel.uxml.
        private const string OverlayName = "swap-overlay";
        private const string CloseBtnName = "swap-close-btn";
        private const string InputAmountName = "swap-input-amount";
        private const string SkrOutName = "swap-skr-out";
        private const string RateName = "swap-rate";
        private const string PlatformFeeName = "swap-platform-fee";
        private const string NetworkFeeName = "swap-network-fee";
        private const string StatusName = "swap-status";
        private const string ConfirmBtnName = "swap-confirm-btn";

        private const string ConfirmDisabledClass = "swap-confirm-btn--disabled";
        private const string StatusErrorClass = "swap-status--error";

        // How long to wait after the last keystroke before firing a quote request.
        private const float QuoteDebounceSeconds = 0.6f;

        [SerializeField] private UIDocument _document;

        // ── Bound elements ───────────────────────────────────────────────────
        private VisualElement _overlay;
        private Button _closeBtn;
        private TextField _inputAmount;
        private Label _skrOut;
        private Label _rate;
        private Label _platformFee;
        private Label _networkFee;
        private Label _status;
        private Button _confirmBtn;

        // ── State ────────────────────────────────────────────────────────────
        private JupiterSwapService _service;
        private SwapFeeConfig _feeConfig;
        private SwapQuote _latestQuote;
        private decimal _currentInput;
        private bool _quoteLoading;
        private CancellationTokenSource _debounceCts;
        private bool _bound;

        // The overlay tap-to-dismiss callback (kept so we can unregister it).
        private EventCallback<PointerDownEvent> _overlayTapCb;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            _service = GetComponent<JupiterSwapService>();
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindElements();
        }

        private void OnDisable()
        {
            if (_debounceCts != null)
            {
                _debounceCts.Cancel();
                _debounceCts.Dispose();
                _debounceCts = null;
            }
            UnbindElements();
        }

        // =====================================================================
        //  Public API (called by JupiterSwapService.OpenSwapPanel)
        // =====================================================================

        public void Initialise(decimal minimumSkr, SwapFeeConfig feeConfig)
        {
            // The document's root may only become available once the panel is
            // shown; (re)bind defensively here.
            if (!_bound) BindElements();

            _feeConfig = feeConfig;
            _latestQuote = null;
            _currentInput = 0m;

            if (_inputAmount != null) _inputAmount.value = "0";
            ClearQuoteDisplay();
            SetStatus("Enter an amount to see the rate.", isError: false);
            SetConfirmEnabled(false);

            // If a minimum is requested, pre-fill a rough USDC amount that would
            // cover it (refined on the first live quote).
            if (minimumSkr > 0 && _inputAmount != null)
                _inputAmount.value = Math.Ceiling(minimumSkr / 10m).ToString("F2");
        }

        // =====================================================================
        //  Element binding
        // =====================================================================

        private void BindElements()
        {
            using var _ = FlowTrace.Enter("Swap", "BindElements");
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                FlowTrace.Warn("Swap", "BindElements: UIDocument root is null — swap panel not bindable yet (may rebind when shown).");
                return;
            }

            _overlay = root.Q<VisualElement>(OverlayName);
            _closeBtn = root.Q<Button>(CloseBtnName);
            _inputAmount = root.Q<TextField>(InputAmountName);
            _skrOut = root.Q<Label>(SkrOutName);
            _rate = root.Q<Label>(RateName);
            _platformFee = root.Q<Label>(PlatformFeeName);
            _networkFee = root.Q<Label>(NetworkFeeName);
            _status = root.Q<Label>(StatusName);
            _confirmBtn = root.Q<Button>(ConfirmBtnName);

            // V: verify the interactive elements bound. A null confirm button / input means the swap
            // is unusable; a null status label means swap errors have no on-screen surface. Warn per
            // missing element so a capture pinpoints exactly which UXML name didn't resolve.
            if (_confirmBtn == null) FlowTrace.Warn("Swap", $"BindElements: confirm button '{ConfirmBtnName}' not found — swap cannot be confirmed.");
            if (_inputAmount == null) FlowTrace.Warn("Swap", $"BindElements: input field '{InputAmountName}' not found — no amount entry.");
            if (_status == null) FlowTrace.Warn("Swap", $"BindElements: status label '{StatusName}' not found — swap status/errors will be invisible.");

            if (_closeBtn != null) _closeBtn.clicked += OnCloseTapped;
            if (_confirmBtn != null) _confirmBtn.clicked += OnConfirmTapped;
            if (_inputAmount != null)
                _inputAmount.RegisterValueChangedCallback(OnInputChanged);

            // Tap the overlay background to dismiss (only when the dimmer itself
            // is the pointer target, not the sheet).
            if (_overlay != null)
            {
                _overlayTapCb = evt =>
                {
                    if (evt.target == _overlay) OnCloseTapped();
                };
                _overlay.RegisterCallback(_overlayTapCb);
            }

            _bound = _overlay != null || _confirmBtn != null || _inputAmount != null;
            if (_bound) FlowTrace.Step("Swap", "BindElements: swap panel bound.");
            else FlowTrace.Fail("Swap", "BindElements: NO swap element resolved — panel did not build (player sees nothing / cannot swap).");
        }

        private void UnbindElements()
        {
            if (_closeBtn != null) _closeBtn.clicked -= OnCloseTapped;
            if (_confirmBtn != null) _confirmBtn.clicked -= OnConfirmTapped;
            if (_inputAmount != null)
                _inputAmount.UnregisterValueChangedCallback(OnInputChanged);
            if (_overlay != null && _overlayTapCb != null)
                _overlay.UnregisterCallback(_overlayTapCb);
            _bound = false;
        }

        // =====================================================================
        //  Input -> debounced quote
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
            if (_debounceCts != null)
            {
                _debounceCts.Cancel();
                _debounceCts.Dispose();
            }
            _debounceCts = new CancellationTokenSource();
            _ = DebounceQuote(_debounceCts.Token);
        }

        private async Task DebounceQuote(CancellationToken ct)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(QuoteDebounceSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                return; // superseded by a newer keystroke.
            }

            if (ct.IsCancellationRequested) return;

            _quoteLoading = true;
            SwapQuote quote = await _service.GetQuoteAsync(SwapInputToken.USDC, _currentInput);
            _quoteLoading = false;

            if (ct.IsCancellationRequested) return;

            if (quote == null)
            {
                FlowTrace.Warn("Swap", $"DebounceQuote: GetQuoteAsync returned null for {_currentInput} USDC — rate unavailable.");
                SetStatus("Could not fetch rate. Check connection.", isError: true);
                return;
            }

            _latestQuote = quote;
            RefreshQuoteDisplay(quote);

            bool walletConnected = !string.IsNullOrEmpty(_service.ConnectedWalletKey);
            SetConfirmEnabled(walletConnected);
            SetStatus(walletConnected ? string.Empty : "Connect your wallet to swap.", isError: false);
        }

        // =====================================================================
        //  Display helpers
        // =====================================================================

        private void RefreshQuoteDisplay(SwapQuote q)
        {
            if (_skrOut != null) _skrOut.text = $"≈ {q.SkrOut:F2} SKR";
            if (_rate != null) _rate.text = $"1 USDC = {q.Rate:F4} SKR";
            if (_platformFee != null)
            {
                decimal feePct = (_feeConfig != null ? _feeConfig.PlatformFeeBps : 20) / 100m;
                _platformFee.text = $"{q.PlatformFee:F4} USDC ({feePct:F1}%)";
            }
            if (_networkFee != null) _networkFee.text = $"~{q.NetworkFee:F6} SOL";
        }

        private void ClearQuoteDisplay()
        {
            if (_skrOut != null) _skrOut.text = "—";
            if (_rate != null) _rate.text = "—";
            if (_platformFee != null) _platformFee.text = "—";
            if (_networkFee != null) _networkFee.text = "—";
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
            using var _ = FlowTrace.Enter("Swap", "OnConfirmTapped");

            if (_latestQuote == null || _quoteLoading)
            {
                FlowTrace.Warn("Swap", $"OnConfirmTapped: ignored — quote {(_latestQuote == null ? "null" : "present")}, loading={_quoteLoading}.");
                return;
            }

            string walletKey = _service.ConnectedWalletKey;
            if (string.IsNullOrEmpty(walletKey))
            {
                FlowTrace.Warn("Swap", "OnConfirmTapped: no connected wallet — swap blocked (player NOT charged).");
                SetStatus("Connect your wallet to swap.", isError: true);
                return;
            }

            SetConfirmEnabled(false);
            SetStatus("Sending to wallet for approval…", isError: false);

            bool ok;
            try
            {
                ok = await _service.ExecuteSwapAsync(_latestQuote, walletKey);
            }
            catch (Exception ex)
            {
                // async void: an unhandled throw here would otherwise crash the frame silently. Catch,
                // Fail loudly (outcome indeterminate = possible partial/charged swap), and re-enable.
                FlowTrace.Fail("Swap",
                    $"OnConfirmTapped: ExecuteSwapAsync THREW: {ex.GetType().Name}: {ex.Message} — swap outcome indeterminate.");
                SetStatus("Swap failed. Please try again.", isError: true);
                SetConfirmEnabled(true);
                return;
            }

            if (!ok)
            {
                FlowTrace.Fail("Swap", "OnConfirmTapped: ExecuteSwapAsync returned FALSE — swap failed.");
                SetStatus("Swap failed. Please try again.", isError: true);
                SetConfirmEnabled(true);
            }
            else
            {
                FlowTrace.Step("Swap", "OnConfirmTapped: swap executed OK.");
            }
        }
    }
}
