// =============================================================================
// JupiterSwapPanelController — the VIEW for JupiterSwapPanel.uxml (WO-43).
// -----------------------------------------------------------------------------
// MVVM (Silo G, DANGER / money path): a DUMB UXML skin that binds a SwapVM. ALL
// logic — the keystroke debounce, the quote fetch, the fee-breakdown projection,
// and the guarded confirm/execute — lives in the VM. This View only:
//   * binds the UXML elements by the name contract in JupiterSwapPanel.uxml,
//   * forwards taps/keystrokes to the VM as commands,
//   * repaints the element text/flags from the VM on Changed.
// It reads NO service/game state directly (it holds the service ONLY to build the
// VM's backend seam at Awake — SwapVM.CreateDefault).
//
// ⚠ MONEY PATH: the confirm guards (not-charged guarantees + indeterminate Fail
// path) are preserved VERBATIM inside SwapVM.ConfirmAsync — zero transaction
// behaviour changed. The async-void handler below just awaits that guarded task.
//
// v1 uses hardcoded English strings (the swap.* keys exist in en.json for a
// later localisation pass via CanonStrings).
// =============================================================================

using System;
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

        // ── The ViewModel (owns debounce / quote / confirm + all display state) ──
        private JupiterSwapService _service;
        private SwapVM _vm;
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
            // Build the VM over the service's backend seam. The View never calls the
            // service directly after this — all logic goes through the VM.
            _vm = SwapVM.CreateDefault(_service);
            _vm.Changed += Render;
        }

        private void OnEnable()
        {
            BindElements();
        }

        private void OnDisable()
        {
            _vm?.CancelPendingQuote();
            UnbindElements();
        }

        private void OnDestroy()
        {
            if (_vm != null)
            {
                _vm.Changed -= Render;
                _vm.Dispose();
                _vm = null;
            }
        }

        // =====================================================================
        //  Public API (called by JupiterSwapService.OpenSwapPanel)
        // =====================================================================

        public void Initialise(decimal minimumSkr, SwapFeeConfig feeConfig)
        {
            // The document's root may only become available once the panel is
            // shown; (re)bind defensively here.
            if (!_bound) BindElements();

            // Reset the VM state + fee source (order mirrors the original controller).
            _vm.BeginInitialise(feeConfig != null ? feeConfig.PlatformFeeBps : 20);

            if (_inputAmount != null) _inputAmount.value = "0";   // fires OnInputChanged -> VM
            _vm.ApplyInitialiseBaseline();   // clears the quote + "enter an amount" + confirm off
            Render();

            // If a minimum is requested, pre-fill a rough USDC amount that would
            // cover it (refined on the first live quote). Setting the value fires
            // OnInputChanged -> the VM's debounced quote.
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

            // Paint the current VM state onto the freshly-bound elements.
            Render();
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
        //  Render — repaint elements from the VM ONLY
        // =====================================================================

        private void Render()
        {
            if (_vm == null) return;
            if (_skrOut != null) _skrOut.text = _vm.SkrOutText;
            if (_rate != null) _rate.text = _vm.RateText;
            if (_platformFee != null) _platformFee.text = _vm.PlatformFeeText;
            if (_networkFee != null) _networkFee.text = _vm.NetworkFeeText;
            if (_status != null)
            {
                _status.text = _vm.StatusText;
                _status.EnableInClassList(StatusErrorClass, _vm.StatusIsError);
            }
            if (_confirmBtn != null)
            {
                _confirmBtn.SetEnabled(_vm.ConfirmEnabled);
                _confirmBtn.EnableInClassList(ConfirmDisabledClass, !_vm.ConfirmEnabled);
            }
        }

        // =====================================================================
        //  Commands (forward to the VM)
        // =====================================================================

        private void OnInputChanged(ChangeEvent<string> evt) => _vm?.OnInputChanged(evt.newValue);

        private void OnCloseTapped() => _vm?.Close();

        private async void OnConfirmTapped()
        {
            // The guarded money path lives in the VM; its internal try/catch means this
            // async-void handler can never crash the frame on an unhandled throw.
            if (_vm != null) await _vm.ConfirmAsync();
        }
    }
}
