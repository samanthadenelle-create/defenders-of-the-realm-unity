// =============================================================================
// TowerSwapMenu — WO7: Tower Swap UI (UIElements).
// -----------------------------------------------------------------------------
// Driven entirely by TowerSwapService. Do not call Open() directly from game
// code outside that service — the service owns the full lifecycle.
//
// Panel layout (all built in code; no UXML dependency):
//   ┌─────────────────────────────────────────┐
//   │  ⚡ Instant Tower Swap          [✕]     │
//   │  Current: <TowerName>  •  Cost: 2.5 USDC│
//   │  Wallet:  X.XX USDC   •  Y.YY SCR       │
//   │  ─────────────────────────────────────── │
//   │  [Arcane] [Frost] [Flame] [Arrow]        │  (current type greyed)
//   │  ─────────────────────────────────────── │
//   │  Pay with:  [USDC ●]  [SCR ○]            │
//   │  ─────────────────────────────────────── │
//   │  ⚠ Cooldown / wave-limit warning         │
//   │  ─────────────────────────────────────── │
//   │  [Cancel]          [Swap Now — 2.5 USDC] │
//   └─────────────────────────────────────────┘
//
// INSPECTOR SETUP:
//   • Assign _document (UIDocument on the same or child GO).
//   • The panel root VisualElement is shown/hidden at runtime; keep the
//     UIDocument disabled by default and TowerSwapMenu will toggle it.
//
// UIElements note: built procedurally so the panel ships without a .uxml asset.
// All USS classes are inline styles to avoid an external USS dependency.
// =============================================================================

using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Data;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// UIElements panel for the instant tower-swap confirmation flow.
    /// Call <see cref="Open"/> from <see cref="TowerSwapService"/>; the menu
    /// calls <paramref name="onConfirm"/> when the player presses Swap Now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerSwapMenu : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("UIDocument that will host the swap panel.")]
        [SerializeField] private UIDocument _document;

        // ── Colours ───────────────────────────────────────────────────────────
        // Every role SOURCES from ElarionUi (the ONE in-game theme) so the swap
        // panel reads as the SAME designed game as the town HUD / store / build-info
        // preview (warm dark stone + runic gold, aether-violet for the runic/selected
        // accent, theme green/red for state). The earlier violet-on-near-black local
        // palette is retired here.
        private static readonly Color BgColor          = ElarionUi.PanelStone;     // modal sheet
        private static readonly Color BorderColor       = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f); // runic-gold rim
        private static readonly Color HeaderColor       = ElarionUi.Gilt;          // title
        private static readonly Color SubtleColor       = ElarionUi.ParchmentDim;  // sub / flavour text
        private static readonly Color ChipColor         = ElarionUi.PanelStoneDark; // unselected chip
        private static readonly Color ChipSelectedColor = ElarionUi.Aether;        // selected chip (runic accent)
        private static readonly Color ChipDisabledColor = ElarionUi.Disabled;      // current/disabled chip
        private static readonly Color WarnColor         = ElarionUi.Gilt;          // cooldown / wave-limit warning
        private static readonly Color ErrorColor        = ElarionUi.Danger;        // error
        private static readonly Color SuccessColor      = ElarionUi.Affordable;    // success
        private static readonly Color CancelBgColor     = ElarionUi.PanelStone;    // neutral button
        private static readonly Color ConfirmBgColor    = ElarionUi.GoldButton;    // primary CTA

        // ── Runtime state ─────────────────────────────────────────────────────

        private VisualElement _root;

        // Populated during Open():
        private Tower                              _currentTower;
        private TowerData[]                        _swappableTowers;
        private double                             _cost;
        private Action<TowerData, DeNelle.Wallet.CurrencyKind> _onConfirm;

        private TowerData                          _selectedType;
        private DeNelle.Wallet.CurrencyKind        _selectedCurrency = DeNelle.Wallet.CurrencyKind.Usdc;

        // Cached dynamic elements (updated after user picks a type or currency):
        private Label        _walletLabel;
        private Label        _warningLabel;
        private Label        _statusLabel;
        private Button       _confirmBtn;
        private VisualElement _typeGrid;
        private VisualElement _currencyRow;
        private VisualElement _loadingOverlay;

        // Wallet balances fetched on Open() (populated by TowerSwapService
        // before the panel becomes interactive; default 0 until known).
        private double _balanceUsdc;
        private double _balanceSkr;

        private bool _onCooldown;
        private float _cooldownRemaining;
        private bool _waveLimitHit;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnDisable()
        {
            HidePanel();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the tower-swap panel for <paramref name="tower"/>.
        /// Called by <see cref="TowerSwapService.OnTowerLongPressed"/>.
        /// </summary>
        public void Open(
            Tower                                              tower,
            TowerData[]                                        swappableTowers,
            double                                             cost,
            bool                                               onCooldown,
            float                                              cooldownRemaining,
            bool                                               waveLimitHit,
            Action<TowerData, DeNelle.Wallet.CurrencyKind>    onConfirm)
        {
            _currentTower     = tower;
            _swappableTowers  = swappableTowers;
            _cost             = cost;
            _onConfirm        = onConfirm;
            _onCooldown       = onCooldown;
            _cooldownRemaining = cooldownRemaining;
            _waveLimitHit     = waveLimitHit;
            _selectedType     = null;
            _selectedCurrency = DeNelle.Wallet.CurrencyKind.Usdc;

            BuildPanel();
            ShowPanel();
        }

        /// <summary>Shows or hides a loading overlay with an optional message.</summary>
        public void SetLoading(bool loading, string message = "")
        {
            if (_loadingOverlay == null) return;
            _loadingOverlay.style.display = loading ? DisplayStyle.Flex : DisplayStyle.None;

            if (loading && _statusLabel != null)
                _statusLabel.text = message;
        }

        /// <summary>Displays an error message at the bottom of the panel.</summary>
        public void ShowError(string message)
        {
            SetLoading(false);
            if (_statusLabel == null) return;
            _statusLabel.text  = message;
            _statusLabel.style.color = ErrorColor;
        }

        /// <summary>Displays a success message then auto-closes after 1.5 s.</summary>
        public void ShowSuccess(string message)
        {
            SetLoading(false);
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
                _statusLabel.style.color = SuccessColor;
            }
            CloseAfterDelay(1.5f).Forget();
        }

        // ── Panel construction ────────────────────────────────────────────────

        private void BuildPanel()
        {
            if (_document == null) return;
            var rootVE = _document.rootVisualElement;
            rootVE.Clear();

            // Full-screen dimmer + centred card
            _root = new VisualElement();
            _root.style.position         = Position.Absolute;
            _root.style.left             = 0; _root.style.top    = 0;
            _root.style.right            = 0; _root.style.bottom = 0;
            _root.style.backgroundColor  = ElarionUi.Scrim;
            _root.style.alignItems       = Align.Center;
            _root.style.justifyContent   = Justify.Center;

            var card = new VisualElement();
            card.style.backgroundColor  = BgColor;
            card.style.borderTopColor    = BorderColor;
            card.style.borderRightColor  = BorderColor;
            card.style.borderBottomColor = BorderColor;
            card.style.borderLeftColor   = BorderColor;
            card.style.borderTopWidth    = 2; card.style.borderRightWidth  = 2;
            card.style.borderBottomWidth = 2; card.style.borderLeftWidth   = 2;
            ElarionUi.SetRadius(card, ElarionUi.RadiusLg);
            card.style.paddingTop    = 20; card.style.paddingBottom = 20;
            card.style.paddingLeft   = 24; card.style.paddingRight  = 24;
            card.style.minWidth      = 380;
            card.style.maxWidth      = 480;

            // ── Header row ──────────────────────────────────────────────────
            var headerRow = new VisualElement();
            headerRow.style.flexDirection  = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems     = Align.Center;
            headerRow.style.marginBottom   = 12;

            var title = new Label(ElarionUi.CrestGlyph + "  Instant Tower Swap");
            title.style.fontSize   = ElarionUi.FontHead;
            title.style.color      = HeaderColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1.5f;
            headerRow.Add(title);

            var closeBtn = new Button(Close) { text = "X" };
            ApplyIconButton(closeBtn);
            headerRow.Add(closeBtn);

            card.Add(headerRow);

            // ── Info row — current tower + cost ────────────────────────────
            string currentName = _currentTower?.Data?.towerName ?? "-";
            var infoRow = new VisualElement();
            infoRow.style.flexDirection  = FlexDirection.Row;
            infoRow.style.justifyContent = Justify.SpaceBetween;
            infoRow.style.marginBottom   = 8;

            var currentLabel = MakeSubLabel($"Current: {currentName}");
            var costLabel    = MakeSubLabel($"Cost: {_cost:F1} USDC");
            infoRow.Add(currentLabel);
            infoRow.Add(costLabel);
            card.Add(infoRow);

            // ── Wallet balance row ──────────────────────────────────────────
            _walletLabel = MakeSubLabel("Wallet: checking...");
            _walletLabel.style.marginBottom = 14;
            card.Add(_walletLabel);

            // ── Divider ─────────────────────────────────────────────────────
            card.Add(MakeDivider());

            // ── Tower type grid ─────────────────────────────────────────────
            var typeHeader = new Label("Choose new type");
            typeHeader.style.fontSize     = ElarionUi.FontLabel;
            typeHeader.style.color        = SubtleColor;
            typeHeader.style.marginTop    = 10;
            typeHeader.style.marginBottom = 6;
            card.Add(typeHeader);

            _typeGrid = new VisualElement();
            _typeGrid.style.flexDirection  = FlexDirection.Row;
            _typeGrid.style.flexWrap       = Wrap.Wrap;
            _typeGrid.style.marginBottom   = 14;
            BuildTypeGrid();
            card.Add(_typeGrid);

            // ── Divider ─────────────────────────────────────────────────────
            card.Add(MakeDivider());

            // ── Currency selector ────────────────────────────────────────────
            var currencyHeader = new Label("Pay with");
            currencyHeader.style.fontSize     = ElarionUi.FontLabel;
            currencyHeader.style.color        = SubtleColor;
            currencyHeader.style.marginTop    = 10;
            currencyHeader.style.marginBottom = 6;
            card.Add(currencyHeader);

            _currencyRow = new VisualElement();
            _currencyRow.style.flexDirection = FlexDirection.Row;
            _currencyRow.style.marginBottom  = 14;
            BuildCurrencyRow();
            card.Add(_currencyRow);

            // ── Cooldown / wave-limit warning ─────────────────────────────
            _warningLabel = new Label();
            _warningLabel.style.color        = WarnColor;
            _warningLabel.style.fontSize     = 12;
            _warningLabel.style.marginBottom = 10;
            _warningLabel.style.whiteSpace   = WhiteSpace.Normal;
            RefreshWarning();
            card.Add(_warningLabel);

            // ── Status line (errors / loading messages) ───────────────────
            _statusLabel = new Label(" ");
            _statusLabel.style.color        = SubtleColor;
            _statusLabel.style.fontSize     = 12;
            _statusLabel.style.marginBottom = 12;
            _statusLabel.style.whiteSpace   = WhiteSpace.Normal;
            card.Add(_statusLabel);

            // ── Divider ─────────────────────────────────────────────────────
            card.Add(MakeDivider());

            // ── Action buttons ────────────────────────────────────────────
            var btnRow = new VisualElement();
            btnRow.style.flexDirection  = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.SpaceBetween;
            btnRow.style.marginTop      = 12;

            var cancelBtn = new Button(Close) { text = "Cancel" };
            StyleActionButton(cancelBtn, CancelBgColor);
            btnRow.Add(cancelBtn);

            _confirmBtn = new Button(OnConfirmClicked) { text = ConfirmLabel() };
            StyleActionButton(_confirmBtn, ConfirmBgColor);
            _confirmBtn.SetEnabled(CanConfirm());
            btnRow.Add(_confirmBtn);

            card.Add(btnRow);

            // ── Loading overlay (shown during async ops) ──────────────────
            _loadingOverlay = new VisualElement();
            _loadingOverlay.style.position        = Position.Absolute;
            _loadingOverlay.style.left            = 0; _loadingOverlay.style.top    = 0;
            _loadingOverlay.style.right           = 0; _loadingOverlay.style.bottom = 0;
            _loadingOverlay.style.backgroundColor = new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.85f);
            _loadingOverlay.style.alignItems      = Align.Center;
            _loadingOverlay.style.justifyContent  = Justify.Center;
            _loadingOverlay.style.display         = DisplayStyle.None;
            _loadingOverlay.style.borderTopLeftRadius     = 10;
            _loadingOverlay.style.borderTopRightRadius    = 10;
            _loadingOverlay.style.borderBottomLeftRadius  = 10;
            _loadingOverlay.style.borderBottomRightRadius = 10;

            var spinLabel = new Label("Processing...");
            spinLabel.style.color    = HeaderColor;
            spinLabel.style.fontSize = 16;
            _loadingOverlay.Add(spinLabel);
            card.Add(_loadingOverlay);

            _root.Add(card);
            rootVE.Add(_root);
        }

        // ── Type grid ─────────────────────────────────────────────────────────

        private void BuildTypeGrid()
        {
            if (_typeGrid == null) return;
            _typeGrid.Clear();

            if (_swappableTowers == null) return;

            string currentName = _currentTower?.Data?.towerName;

            foreach (var td in _swappableTowers)
            {
                if (td == null) continue;
                bool isCurrent  = string.Equals(td.towerName, currentName, StringComparison.OrdinalIgnoreCase);
                bool isSelected = _selectedType == td;

                var chip = new Button();
                chip.text = td.towerName;
                chip.style.paddingTop    = 8;  chip.style.paddingBottom = 8;
                chip.style.paddingLeft   = 12; chip.style.paddingRight  = 12;
                chip.style.marginRight   = 6;  chip.style.marginBottom  = 6;
                chip.style.borderTopLeftRadius     = 6;
                chip.style.borderTopRightRadius    = 6;
                chip.style.borderBottomLeftRadius  = 6;
                chip.style.borderBottomRightRadius = 6;
                chip.style.borderTopWidth          = 1;
                chip.style.borderRightWidth        = 1;
                chip.style.borderBottomWidth       = 1;
                chip.style.borderLeftWidth         = 1;
                chip.style.fontSize = 13;

                if (isCurrent)
                {
                    chip.style.backgroundColor = ChipDisabledColor;
                    chip.style.color           = SubtleColor;
                    chip.style.borderTopColor    = SubtleColor;
                    chip.style.borderRightColor  = SubtleColor;
                    chip.style.borderBottomColor = SubtleColor;
                    chip.style.borderLeftColor   = SubtleColor;
                    chip.SetEnabled(false);
                }
                else if (isSelected)
                {
                    chip.style.backgroundColor = ChipSelectedColor;
                    chip.style.color           = ElarionUi.Parchment;
                    chip.style.borderTopColor    = ElarionUi.Gilt;
                    chip.style.borderRightColor  = ElarionUi.Gilt;
                    chip.style.borderBottomColor = ElarionUi.Gilt;
                    chip.style.borderLeftColor   = ElarionUi.Gilt;
                }
                else
                {
                    chip.style.backgroundColor = ChipColor;
                    chip.style.color           = ElarionUi.Parchment;
                    chip.style.borderTopColor    = BorderColor;
                    chip.style.borderRightColor  = BorderColor;
                    chip.style.borderBottomColor = BorderColor;
                    chip.style.borderLeftColor   = BorderColor;
                }

                var capturedTd = td;
                chip.clicked += () =>
                {
                    _selectedType = capturedTd;
                    RefreshGrid();
                    RefreshConfirmButton();
                };

                _typeGrid.Add(chip);
            }
        }

        private void RefreshGrid()
        {
            BuildTypeGrid();
        }

        // ── Currency row ──────────────────────────────────────────────────────

        private void BuildCurrencyRow()
        {
            if (_currencyRow == null) return;
            _currencyRow.Clear();

            foreach (var kind in new[] { DeNelle.Wallet.CurrencyKind.Usdc, DeNelle.Wallet.CurrencyKind.Skr })
            {
                bool isSelected = _selectedCurrency == kind;
                string label    = kind == DeNelle.Wallet.CurrencyKind.Usdc ? "USDC" : "SCR";

                var btn = new Button();
                btn.text = isSelected ? $"(*) {label}" : $"( ) {label}";
                btn.style.paddingTop    = 6;  btn.style.paddingBottom = 6;
                btn.style.paddingLeft   = 14; btn.style.paddingRight  = 14;
                btn.style.marginRight   = 8;
                btn.style.borderTopLeftRadius     = 20;
                btn.style.borderTopRightRadius    = 20;
                btn.style.borderBottomLeftRadius  = 20;
                btn.style.borderBottomRightRadius = 20;
                btn.style.borderTopWidth    = 1; btn.style.borderRightWidth  = 1;
                btn.style.borderBottomWidth = 1; btn.style.borderLeftWidth   = 1;
                btn.style.fontSize = 13;

                if (isSelected)
                {
                    btn.style.backgroundColor = ChipSelectedColor;
                    btn.style.color           = ElarionUi.Parchment;
                    btn.style.borderTopColor    = ElarionUi.Gilt;
                    btn.style.borderRightColor  = ElarionUi.Gilt;
                    btn.style.borderBottomColor = ElarionUi.Gilt;
                    btn.style.borderLeftColor   = ElarionUi.Gilt;
                }
                else
                {
                    btn.style.backgroundColor = ChipColor;
                    btn.style.color           = SubtleColor;
                    btn.style.borderTopColor    = new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f);
                    btn.style.borderRightColor  = new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f);
                    btn.style.borderBottomColor = new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f);
                    btn.style.borderLeftColor   = new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f);
                }

                var capturedKind = kind;
                btn.clicked += () =>
                {
                    _selectedCurrency = capturedKind;
                    _currencyRow.Clear();
                    BuildCurrencyRow();
                    RefreshConfirmButton();
                };

                _currencyRow.Add(btn);
            }
        }

        // ── Warning label ─────────────────────────────────────────────────────

        private void RefreshWarning()
        {
            if (_warningLabel == null) return;

            if (_waveLimitHit)
            {
                _warningLabel.text = "Wave limit reached — max 4 swaps per wave.";
                _warningLabel.style.display = DisplayStyle.Flex;
            }
            else if (_onCooldown)
            {
                int secs = Mathf.CeilToInt(_cooldownRemaining);
                _warningLabel.text = $"Tower on cooldown — {secs}s remaining.";
                _warningLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _warningLabel.text = string.Empty;
                _warningLabel.style.display = DisplayStyle.None;
            }
        }

        // ── Confirm button ────────────────────────────────────────────────────

        private bool CanConfirm() => _selectedType != null && !_onCooldown && !_waveLimitHit;

        private string ConfirmLabel()
        {
            string currency = _selectedCurrency == DeNelle.Wallet.CurrencyKind.Usdc ? "USDC" : "SCR";
            return _selectedType != null
                ? $"Swap Now — {_cost:F1} {currency}"
                : "Select a tower type";
        }

        private void RefreshConfirmButton()
        {
            if (_confirmBtn == null) return;
            _confirmBtn.text = ConfirmLabel();
            _confirmBtn.SetEnabled(CanConfirm());
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnConfirmClicked()
        {
            if (!CanConfirm() || _selectedType == null) return;
            _onConfirm?.Invoke(_selectedType, _selectedCurrency);
        }

        private void Close()
        {
            HidePanel();
        }

        // ── Wallet balance (called from TowerSwapService after balance fetch) ─

        /// <summary>
        /// Updates the displayed wallet balance. TowerSwapService calls this
        /// after <see cref="WalletService.GetBalance"/> returns while the panel
        /// is still open (it's not yet called from TowerSwapService v1 — the
        /// balance appears in the status label instead; this method is here for
        /// Phase-2 wiring).
        /// </summary>
        public void SetWalletBalance(double usdc, double skr)
        {
            _balanceUsdc = usdc;
            _balanceSkr  = skr;
            if (_walletLabel != null)
                _walletLabel.text = $"Wallet:  {usdc:F2} USDC   -   {skr:F2} SCR";
        }

        // ── Show / Hide ───────────────────────────────────────────────────────

        private void ShowPanel()
        {
            if (_document == null) return;
            _document.enabled = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        private void HidePanel()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_document != null) _document.enabled = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async UniTaskVoid CloseAfterDelay(float seconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
            Close();
        }

        private static Label MakeSubLabel(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 12;
            l.style.color    = SubtleColor;
            return l;
        }

        private static VisualElement MakeDivider()
        {
            var div = new VisualElement();
            div.style.height          = 1;
            div.style.backgroundColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.40f);
            div.style.marginTop       = 2;
            div.style.marginBottom    = 2;
            return div;
        }

        private static void ApplyIconButton(Button btn)
        {
            btn.style.backgroundColor = Color.clear;
            btn.style.borderTopWidth  = 0; btn.style.borderRightWidth  = 0;
            btn.style.borderBottomWidth = 0; btn.style.borderLeftWidth = 0;
            btn.style.color           = SubtleColor;
            // Fixed 120px touch box (no PanelSettings ref scaler, so device px), was ~24px
            // auto-size (VISUAL_TOUCH_CONTRAST_AUDIT 2026-07-14, P0).
            btn.style.width           = 120; btn.style.height = 120;
            btn.style.fontSize        = 34;
            btn.style.unityTextAlign  = TextAnchor.MiddleCenter;
            btn.style.paddingLeft     = 6; btn.style.paddingRight = 6;
        }

        private static void StyleActionButton(Button btn, Color bg)
        {
            btn.style.backgroundColor = bg;
            // Dark ink on the gold CTA fill (high contrast), parchment cream on stone.
            btn.style.color           = bg == ConfirmBgColor ? ElarionUi.Ink : ElarionUi.Parchment;
            btn.style.paddingTop      = 10; btn.style.paddingBottom = 10;
            btn.style.paddingLeft     = 20; btn.style.paddingRight  = 20;
            ElarionUi.SetRadius(btn, ElarionUi.RadiusMd);
            btn.style.borderTopWidth    = 0; btn.style.borderRightWidth  = 0;
            btn.style.borderBottomWidth = 0; btn.style.borderLeftWidth   = 0;
            btn.style.fontSize          = ElarionUi.FontBody;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
    }
}
