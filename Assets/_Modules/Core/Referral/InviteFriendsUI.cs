// =============================================================================
// InviteFriendsUI — WO6: Referral Share + Claim Panel (UIElements).
// -----------------------------------------------------------------------------
// Procedural UIElements panel — no UXML dependency.
// Open via InviteFriendsUI.Instance.Open().
//
// Layout:
//   ┌─────────────────────────────────────────────┐
//   │  👥  Invite Friends                  [✕]   │
//   │  ─────────────────────────────────────────  │
//   │  Your referral code:                        │
//   │  [ XYZABC ]                  [Copy]         │
//   │                                             │
//   │  [   Share on 𝕏 (Twitter)   ]              │
//   │  ─────────────────────────────────────────  │
//   │  Have a code? Enter it below:               │
//   │  [_____________________________]            │
//   │  [      Claim Reward           ]            │
//   │  ─────────────────────────────────────────  │
//   │  <status line>                              │
//   └─────────────────────────────────────────────┘
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Referral;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Core.Referral
{
    [DisallowMultipleComponent]
    public sealed class InviteFriendsUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField] private UIDocument _document;

        // ── Singleton ─────────────────────────────────────────────────────────

        private static InviteFriendsUI _instance;
        public  static InviteFriendsUI Instance => _instance;

        // ── Colours ───────────────────────────────────────────────────────────

        private static readonly Color BgColor       = new Color(0.08f, 0.07f, 0.12f, 0.97f);
        private static readonly Color BorderColor    = new Color(0.55f, 0.25f, 1.00f, 0.90f);
        private static readonly Color HeaderColor    = new Color(0.75f, 0.55f, 1.00f, 1.00f);
        private static readonly Color SubtleColor    = new Color(0.60f, 0.60f, 0.70f, 1.00f);
        private static readonly Color SuccessColor   = new Color(0.38f, 1.00f, 0.60f, 1.00f);
        private static readonly Color ErrorColor     = new Color(1.00f, 0.38f, 0.38f, 1.00f);
        private static readonly Color PrimaryColor   = new Color(0.45f, 0.20f, 0.85f, 1.00f);
        private static readonly Color XColor         = new Color(0.05f, 0.05f, 0.05f, 1.00f);
        private static readonly Color CodeBgColor    = new Color(0.14f, 0.12f, 0.20f, 1.00f);
        private static readonly Color InputBgColor   = new Color(0.14f, 0.12f, 0.20f, 1.00f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label         _codeDisplayLabel;
        private TextField     _claimField;
        private Button        _claimBtn;
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
            if (ReferralService.Instance != null)
            {
                ReferralService.Instance.OnCodeReady    += HandleCodeReady;
                ReferralService.Instance.OnClaimSuccess += HandleClaimSuccess;
                ReferralService.Instance.OnClaimFailed  += HandleClaimFailed;
            }
        }

        private void OnDisable()
        {
            if (ReferralService.Instance != null)
            {
                ReferralService.Instance.OnCodeReady    -= HandleCodeReady;
                ReferralService.Instance.OnClaimSuccess -= HandleClaimSuccess;
                ReferralService.Instance.OnClaimFailed  -= HandleClaimFailed;
            }
            HidePanel();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Open()
        {
            BuildPanel();
            ShowPanel();

            // Kick off code fetch if not already cached.
            if (ReferralService.Instance != null)
            {
                SetCodeDisplayLoading(true);
                ReferralService.Instance.EnsureCodeAsync().Forget();
            }
        }

        // ── Panel construction ────────────────────────────────────────────────

        private void BuildPanel()
        {
            if (_document == null) return;
            var rootVE = _document.rootVisualElement;
            rootVE.Clear();

            _root = new VisualElement();
            _root.style.position        = Position.Absolute;
            _root.style.left            = 0; _root.style.top    = 0;
            _root.style.right           = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _root.style.alignItems      = Align.Center;
            _root.style.justifyContent  = Justify.Center;

            var card = MakeCard();

            // ── Header ────────────────────────────────────────────────────────
            var headerRow = MakeRow(Justify.SpaceBetween, Align.Center, 0, 16);
            var title = new Label("👥  Invite Friends");
            title.style.fontSize = 17;
            title.style.color    = HeaderColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(title);
            var closeBtn = new Button(Close) { text = "✕" };
            ApplyIconBtn(closeBtn);
            headerRow.Add(closeBtn);
            card.Add(headerRow);
            card.Add(MakeDivider());

            // ── My code section ───────────────────────────────────────────────
            var codeSectionLabel = MakeSectionLabel("Your referral code");
            card.Add(codeSectionLabel);

            var codeRow = MakeRow(Justify.SpaceBetween, Align.Center, 6, 12);

            _codeDisplayLabel = new Label("Loading…");
            _codeDisplayLabel.style.fontSize    = 22;
            _codeDisplayLabel.style.color       = HeaderColor;
            _codeDisplayLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _codeDisplayLabel.style.backgroundColor = CodeBgColor;
            _codeDisplayLabel.style.paddingLeft  = 16; _codeDisplayLabel.style.paddingRight  = 16;
            _codeDisplayLabel.style.paddingTop   = 8;  _codeDisplayLabel.style.paddingBottom = 8;
            _codeDisplayLabel.style.borderTopLeftRadius     = 6;
            _codeDisplayLabel.style.borderTopRightRadius    = 6;
            _codeDisplayLabel.style.borderBottomLeftRadius  = 6;
            _codeDisplayLabel.style.borderBottomRightRadius = 6;
            _codeDisplayLabel.style.flexGrow = 1;
            codeRow.Add(_codeDisplayLabel);

            var copyBtn = new Button(CopyCode) { text = "Copy" };
            StyleSecondaryBtn(copyBtn);
            copyBtn.style.marginLeft = 8;
            codeRow.Add(copyBtn);
            card.Add(codeRow);

            // Share on X button
            var shareBtn = new Button(ShareOnX) { text = "Share on  𝕏" };
            StyleXBtn(shareBtn);
            card.Add(shareBtn);

            card.Add(MakeDivider());

            // ── Claim section ─────────────────────────────────────────────────
            card.Add(MakeSectionLabel("Have a friend's code? Claim your reward:"));

            if (ReferralService.Instance != null && ReferralService.Instance.HasClaimed)
            {
                var alreadyLabel = new Label("✓  Referral reward already claimed.");
                alreadyLabel.style.color      = SuccessColor;
                alreadyLabel.style.fontSize   = 12;
                alreadyLabel.style.marginTop  = 6;
                alreadyLabel.style.marginBottom = 12;
                card.Add(alreadyLabel);
            }
            else
            {
                _claimField = new TextField();
                _claimField.value = string.Empty;
                _claimField.style.marginTop    = 8;
                _claimField.style.marginBottom = 8;
                _claimField.style.height       = 38;
                StyleTextField(_claimField);
                _claimField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                        SubmitClaim();
                });
                card.Add(_claimField);

                _claimBtn = new Button(SubmitClaim) { text = "Claim Reward" };
                StylePrimaryBtn(_claimBtn);
                card.Add(_claimBtn);
            }

            card.Add(MakeDivider());

            // ── Status ────────────────────────────────────────────────────────
            _statusLabel = new Label(" ");
            _statusLabel.style.color      = SubtleColor;
            _statusLabel.style.fontSize   = 12;
            _statusLabel.style.marginTop  = 8;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_statusLabel);

            _root.Add(card);
            rootVE.Add(_root);
        }

        // ── Interactions ──────────────────────────────────────────────────────

        private void CopyCode()
        {
            var code = ReferralService.Instance?.MyCode ?? string.Empty;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            SetStatus("Code copied to clipboard!", SubtleColor);
        }

        private void ShareOnX()
        {
            if (ReferralService.Instance == null) return;
            ReferralService.Instance.ShareOnX();
            SetStatus("Opening X…", SubtleColor);
        }

        private void SubmitClaim()
        {
            if (_busy || ReferralService.Instance == null) return;
            var code = _claimField?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(code)) { SetStatus("Enter a referral code.", ErrorColor); return; }

            SetBusy(true);
            SetStatus("Checking code…", SubtleColor);
            ReferralService.Instance.ClaimAsync(code).Forget();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleCodeReady(string code, string url)
        {
            SetCodeDisplayLoading(false);
            if (_codeDisplayLabel != null) _codeDisplayLabel.text = code;
        }

        private void HandleClaimSuccess(int crystals, string message)
        {
            SetBusy(false);
            SetStatus(message, SuccessColor);
            if (_claimField != null) _claimField.value = string.Empty;
            // Rebuild to show "already claimed" state.
            CloseAfterDelay(2.5f).Forget();
        }

        private void HandleClaimFailed(string reason)
        {
            SetBusy(false);
            SetStatus(reason, ErrorColor);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetCodeDisplayLoading(bool loading)
        {
            if (_codeDisplayLabel == null) return;
            _codeDisplayLabel.text = loading ? "Loading…" : (ReferralService.Instance?.MyCode ?? "—");
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_claimBtn != null)
            {
                _claimBtn.SetEnabled(!busy);
                _claimBtn.text = busy ? "Checking…" : "Claim Reward";
            }
        }

        private void SetStatus(string msg, Color color)
        {
            if (_statusLabel == null) return;
            _statusLabel.text        = msg;
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
            card.style.borderTopColor       = BorderColor; card.style.borderRightColor  = BorderColor;
            card.style.borderBottomColor    = BorderColor; card.style.borderLeftColor   = BorderColor;
            card.style.borderTopWidth       = 2;           card.style.borderRightWidth  = 2;
            card.style.borderBottomWidth    = 2;           card.style.borderLeftWidth   = 2;
            card.style.borderTopLeftRadius     = 10;
            card.style.borderTopRightRadius    = 10;
            card.style.borderBottomLeftRadius  = 10;
            card.style.borderBottomRightRadius = 10;
            card.style.paddingTop    = 20; card.style.paddingBottom = 20;
            card.style.paddingLeft   = 28; card.style.paddingRight  = 28;
            card.style.minWidth      = 360;
            card.style.maxWidth      = 460;
            return card;
        }

        private static VisualElement MakeRow(Justify justify, Align align, int marginTop, int marginBottom)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.justifyContent = justify;
            row.style.alignItems     = align;
            row.style.marginTop      = marginTop;
            row.style.marginBottom   = marginBottom;
            return row;
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

        private static Label MakeSectionLabel(string text)
        {
            var l = new Label(text);
            l.style.fontSize     = 12;
            l.style.color        = SubtleColor;
            l.style.marginTop    = 12;
            l.style.marginBottom = 4;
            return l;
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
            tf.style.borderTopColor       = BorderColor; tf.style.borderRightColor  = BorderColor;
            tf.style.borderBottomColor    = BorderColor; tf.style.borderLeftColor   = BorderColor;
            tf.style.borderTopWidth       = 1;           tf.style.borderRightWidth  = 1;
            tf.style.borderBottomWidth    = 1;           tf.style.borderLeftWidth   = 1;
            tf.style.borderTopLeftRadius     = 6;
            tf.style.borderTopRightRadius    = 6;
            tf.style.borderBottomLeftRadius  = 6;
            tf.style.borderBottomRightRadius = 6;
            tf.style.paddingLeft  = 10;
            tf.style.paddingRight = 10;
            tf.style.color    = Color.white;
            tf.style.fontSize = 14;
        }

        private static void StylePrimaryBtn(Button btn)
        {
            btn.style.backgroundColor      = PrimaryColor;
            btn.style.color                = Color.white;
            btn.style.paddingTop           = 10; btn.style.paddingBottom = 10;
            btn.style.marginBottom         = 10;
            btn.style.borderTopLeftRadius     = 6;
            btn.style.borderTopRightRadius    = 6;
            btn.style.borderBottomLeftRadius  = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.borderTopWidth    = 0; btn.style.borderRightWidth  = 0;
            btn.style.borderBottomWidth = 0; btn.style.borderLeftWidth   = 0;
            btn.style.fontSize          = 14;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private static void StyleSecondaryBtn(Button btn)
        {
            btn.style.backgroundColor      = new Color(0.22f, 0.18f, 0.32f, 1f);
            btn.style.color                = HeaderColor;
            btn.style.paddingTop           = 8;  btn.style.paddingBottom = 8;
            btn.style.paddingLeft          = 14; btn.style.paddingRight  = 14;
            btn.style.borderTopLeftRadius     = 6;
            btn.style.borderTopRightRadius    = 6;
            btn.style.borderBottomLeftRadius  = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.borderTopColor       = BorderColor; btn.style.borderRightColor  = BorderColor;
            btn.style.borderBottomColor    = BorderColor; btn.style.borderLeftColor   = BorderColor;
            btn.style.borderTopWidth       = 1;           btn.style.borderRightWidth  = 1;
            btn.style.borderBottomWidth    = 1;           btn.style.borderLeftWidth   = 1;
            btn.style.fontSize             = 13;
        }

        private static void StyleXBtn(Button btn)
        {
            btn.style.backgroundColor      = XColor;
            btn.style.color                = Color.white;
            btn.style.paddingTop           = 10; btn.style.paddingBottom = 10;
            btn.style.marginTop            = 10; btn.style.marginBottom  = 10;
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
