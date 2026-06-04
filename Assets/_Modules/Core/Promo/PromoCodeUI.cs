// =============================================================================
// PromoCodeUI — WO5: Promo Code Entry Panel (UIElements).
// -----------------------------------------------------------------------------
// Procedural UIElements panel — no UXML dependency.
// Open via PromoCodeUI.Instance.Open() from any scene button.
//
// Layout:
//   ┌───────────────────────────────────────┐
//   │  🎁  Enter Promo Code          [✕]   │
//   │  ─────────────────────────────────── │
//   │  [_________________________]          │
//   │  [       Redeem Code       ]          │
//   │  ─────────────────────────────────── │
//   │  <status / reward message>            │
//   └───────────────────────────────────────┘
//
// INSPECTOR SETUP:
//   • Assign _document (UIDocument on the same or child GO).
//   • Wire a scene button to call PromoCodeUI.Instance.Open().
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Promo;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Core.Promo
{
    [DisallowMultipleComponent]
    public sealed class PromoCodeUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField] private UIDocument _document;

        // ── Singleton ─────────────────────────────────────────────────────────

        private static PromoCodeUI _instance;
        public  static PromoCodeUI Instance => _instance;

        // ── Colours ───────────────────────────────────────────────────────────

        private static readonly Color BgColor      = new Color(0.08f, 0.07f, 0.12f, 0.97f);
        private static readonly Color BorderColor   = new Color(0.55f, 0.25f, 1.00f, 0.90f);
        private static readonly Color HeaderColor   = new Color(0.75f, 0.55f, 1.00f, 1.00f);
        private static readonly Color SubtleColor   = new Color(0.60f, 0.60f, 0.70f, 1.00f);
        private static readonly Color SuccessColor  = new Color(0.38f, 1.00f, 0.60f, 1.00f);
        private static readonly Color ErrorColor    = new Color(1.00f, 0.38f, 0.38f, 1.00f);
        private static readonly Color ButtonBgColor = new Color(0.45f, 0.20f, 0.85f, 1.00f);
        private static readonly Color InputBgColor  = new Color(0.14f, 0.12f, 0.20f, 1.00f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private VisualElement _root;
        private TextField     _codeField;
        private Button        _redeemBtn;
        private Label         _statusLabel;
        private bool          _busy;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (PromoCodeService.Instance != null)
            {
                PromoCodeService.Instance.OnRedeemed    += HandleRedeemed;
                PromoCodeService.Instance.OnRedeemFailed += HandleFailed;
            }
        }

        private void OnDisable()
        {
            if (PromoCodeService.Instance != null)
            {
                PromoCodeService.Instance.OnRedeemed    -= HandleRedeemed;
                PromoCodeService.Instance.OnRedeemFailed -= HandleFailed;
            }
            HidePanel();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Open()
        {
            BuildPanel();
            ShowPanel();
        }

        // ── Panel construction ────────────────────────────────────────────────

        private void BuildPanel()
        {
            if (_document == null) return;
            var rootVE = _document.rootVisualElement;
            rootVE.Clear();

            // Dimmer
            _root = new VisualElement();
            _root.style.position        = Position.Absolute;
            _root.style.left            = 0; _root.style.top    = 0;
            _root.style.right           = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _root.style.alignItems      = Align.Center;
            _root.style.justifyContent  = Justify.Center;

            // Card
            var card = MakeCard();

            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection  = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems     = Align.Center;
            headerRow.style.marginBottom   = 16;

            var title = new Label("🎁  Enter Promo Code");
            title.style.fontSize = 17;
            title.style.color    = HeaderColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(title);

            var closeBtn = new Button(Close) { text = "✕" };
            ApplyIconBtn(closeBtn);
            headerRow.Add(closeBtn);
            card.Add(headerRow);

            card.Add(MakeDivider());

            // Code input
            _codeField = new TextField();
            _codeField.value = string.Empty;
            _codeField.style.marginTop    = 14;
            _codeField.style.marginBottom = 10;
            _codeField.style.height       = 40;
            StyleTextField(_codeField);

            // Submit on Enter key
            _codeField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    SubmitCode();
            });

            card.Add(_codeField);

            // Redeem button
            _redeemBtn = new Button(SubmitCode) { text = "Redeem Code" };
            StylePrimaryBtn(_redeemBtn);
            card.Add(_redeemBtn);

            card.Add(MakeDivider());

            // Status label
            _statusLabel = new Label(" ");
            _statusLabel.style.color      = SubtleColor;
            _statusLabel.style.fontSize   = 12;
            _statusLabel.style.marginTop  = 10;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.textOverflow = TextOverflow.Clip;
            card.Add(_statusLabel);

            _root.Add(card);
            rootVE.Add(_root);
        }

        // ── Submission ────────────────────────────────────────────────────────

        private void SubmitCode()
        {
            if (_busy) return;
            var code = _codeField?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Enter a code first.", ErrorColor);
                return;
            }

            if (PromoCodeService.Instance == null)
            {
                SetStatus("Service unavailable. Restart the game.", ErrorColor);
                return;
            }

            SetBusy(true);
            SetStatus("Validating…", SubtleColor);
            PromoCodeService.Instance.RedeemAsync(code).Forget();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleRedeemed(PromoReward reward)
        {
            SetBusy(false);
            string msg = string.IsNullOrEmpty(reward.Message)
                ? $"Code redeemed! You received {reward.Crystals} crystals + {reward.Coins} coins."
                : reward.Message;
            SetStatus(msg, SuccessColor);
            if (_codeField != null) _codeField.value = string.Empty;
            CloseAfterDelay(2.5f).Forget();
        }

        private void HandleFailed(string reason)
        {
            SetBusy(false);
            SetStatus(reason, ErrorColor);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_redeemBtn != null)
            {
                _redeemBtn.SetEnabled(!busy);
                _redeemBtn.text = busy ? "Checking…" : "Redeem Code";
            }
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusLabel == null) return;
            _statusLabel.text        = message;
            _statusLabel.style.color = color;
        }

        private void Close() => HidePanel();

        private void ShowPanel()
        {
            if (_document != null) _document.enabled = true;
            if (_root != null) _root.style.display   = DisplayStyle.Flex;
        }

        private void HidePanel()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_document != null) _document.enabled = false;
        }

        private async UniTaskVoid CloseAfterDelay(float seconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
            Close();
        }

        // ── Style helpers ─────────────────────────────────────────────────────

        private static VisualElement MakeCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor      = BgColor;
            card.style.borderTopColor       = BorderColor;
            card.style.borderRightColor     = BorderColor;
            card.style.borderBottomColor    = BorderColor;
            card.style.borderLeftColor      = BorderColor;
            card.style.borderTopWidth       = 2; card.style.borderRightWidth  = 2;
            card.style.borderBottomWidth    = 2; card.style.borderLeftWidth   = 2;
            card.style.borderTopLeftRadius     = 10;
            card.style.borderTopRightRadius    = 10;
            card.style.borderBottomLeftRadius  = 10;
            card.style.borderBottomRightRadius = 10;
            card.style.paddingTop    = 20; card.style.paddingBottom = 20;
            card.style.paddingLeft   = 28; card.style.paddingRight  = 28;
            card.style.minWidth      = 340;
            card.style.maxWidth      = 420;
            return card;
        }

        private static VisualElement MakeDivider()
        {
            var div = new VisualElement();
            div.style.height          = 1;
            div.style.backgroundColor = new Color(0.30f, 0.25f, 0.45f, 0.60f);
            div.style.marginTop       = 4;
            div.style.marginBottom    = 4;
            return div;
        }

        private static void ApplyIconBtn(Button btn)
        {
            btn.style.backgroundColor   = Color.clear;
            btn.style.borderTopWidth    = 0; btn.style.borderRightWidth  = 0;
            btn.style.borderBottomWidth = 0; btn.style.borderLeftWidth   = 0;
            btn.style.color             = SubtleColor;
            btn.style.fontSize          = 16;
            btn.style.paddingLeft       = 6; btn.style.paddingRight = 6;
        }

        private static void StyleTextField(TextField tf)
        {
            tf.style.backgroundColor      = InputBgColor;
            tf.style.borderTopColor       = BorderColor;
            tf.style.borderRightColor     = BorderColor;
            tf.style.borderBottomColor    = BorderColor;
            tf.style.borderLeftColor      = BorderColor;
            tf.style.borderTopWidth       = 1; tf.style.borderRightWidth  = 1;
            tf.style.borderBottomWidth    = 1; tf.style.borderLeftWidth   = 1;
            tf.style.borderTopLeftRadius     = 6;
            tf.style.borderTopRightRadius    = 6;
            tf.style.borderBottomLeftRadius  = 6;
            tf.style.borderBottomRightRadius = 6;
            tf.style.paddingLeft  = 10;
            tf.style.paddingRight = 10;
            tf.style.color = Color.white;
            tf.style.fontSize = 14;
        }

        private static void StylePrimaryBtn(Button btn)
        {
            btn.style.backgroundColor      = ButtonBgColor;
            btn.style.color                = Color.white;
            btn.style.paddingTop           = 11; btn.style.paddingBottom = 11;
            btn.style.marginTop            = 0;  btn.style.marginBottom  = 10;
            btn.style.borderTopLeftRadius     = 6;
            btn.style.borderTopRightRadius    = 6;
            btn.style.borderBottomLeftRadius  = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.borderTopWidth    = 0; btn.style.borderRightWidth  = 0;
            btn.style.borderBottomWidth = 0; btn.style.borderLeftWidth   = 0;
            btn.style.fontSize          = 14;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
    }
}
