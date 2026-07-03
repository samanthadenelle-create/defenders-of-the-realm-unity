// =============================================================================
// ClanChatPanel — toggleable team-chat panel for the Clans stub. Press Y to
// open/close (standard chat hotkey across MMOs / co-op games).
// -----------------------------------------------------------------------------
// Reads ClanService.Instance directly because DeNelle.HUD already references
// DeNelle.Core — no reflection needed (unlike AdminOverlay).
//
// Layout:
//   • Header — clan tag + name + "Create" / "Leave" button.
//   • When not in a clan — inline create form (name + tag fields).
//   • Scrollable message list — oldest at top, newest at bottom.
//   • Phrase chip rail — one tap = post that phrase.
//   • "Custom…" button reveals a TextField + Send for ≤140 char free text.
//
// Single-player only. The network bridge will swap ClanService for a thin
// remote wrapper later (§7.1 of the React design doc).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Services;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ClanChatPanel : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _header;
        private VisualElement _body;
        private ScrollView _messageScroll;
        private VisualElement _chipRail;
        private VisualElement _composer;
        private VisualElement _createForm;
        private Label _headerTitle;
        private Button _headerButton;
        private TextField _customField;
        private VisualElement _customRow;

        private bool _visible;
        private bool _customOpen;
        private TextField _createNameField;
        private TextField _createTagField;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Build();
            if (ClanService.Instance != null)
                ClanService.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (ClanService.Instance != null)
                ClanService.Instance.Changed -= Repaint;
        }

        // Mobile-first: the 'Y' keyboard open is REMOVED. The panel opens via Toggle()
        // (public), reached by its on-screen / world-interactable path. No Update key poll.

        public void Toggle() => SetVisible(!_visible);

        // Modal arbiter membership (eyes-on pass 2026-07-03: the open chat squatted OVER the
        // Talents/Upgrade modals in every bot capture — it never told PanelManager it was open,
        // so nothing ever closed it; and it exposed NO close affordance at all).
        private PanelHandle _panelHandle;
        private PanelHandle Handle =>
            _panelHandle ??= PanelManager.Register("Clan Chat", () => SetVisible(false), () => _visible);

        private void SetVisible(bool on)
        {
            if (on) FlowTrace.Step("ClanChat", "SetVisible(true) — opening clan chat panel.");
            _visible = on;
            if (on && _panel == null)
                FlowTrace.Fail("ClanChat",
                    "SetVisible(true): _panel is NULL — Build never produced a panel (no PanelSettings/root?). Open is a no-op.");
            if (_panel != null)
                _panel.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) { PanelManager.NotifyOpened(Handle); Repaint(); }
            else PanelManager.NotifyClosed(Handle);
        }

        // ── UI construction ──────────────────────────────────────────────────

        private void Build()
        {
            using var _ = FlowTrace.Enter("ClanChat", "Build");
            // V (WO-465 class): this UIDocument adopts no PanelSettings. With none set,
            // rootVisualElement is null and the old `if (_root == null) return;` produced an
            // invisible panel with no symptom. Fail-loud so a run reports why it never showed.
            if (_doc != null && _doc.panelSettings == null)
                FlowTrace.Fail("ClanChat",
                    "Build: UIDocument has NO PanelSettings — rootVisualElement will be null and the " +
                    "clan chat panel renders invisible. Wire a PanelSettings on the ClanChat UIDocument.");
            _root = _doc != null ? _doc.rootVisualElement : null;
            if (_root == null)
            {
                FlowTrace.Fail("ClanChat",
                    "Build: rootVisualElement is NULL (no UIDocument/PanelSettings) — clan chat UI cannot be built; panel will be empty.");
                return;
            }
            _root.pickingMode = PickingMode.Ignore; // don't block HUD beneath
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0;  _root.style.bottom = 0;

            _panel = new VisualElement();
            _panel.name = "ClanChatPanel";
            _panel.style.position = Position.Absolute;
            _panel.style.left = 16;
            _panel.style.bottom = 16;
            _panel.style.width = 360;
            _panel.style.maxHeight = 460;
            _panel.style.backgroundColor = new StyleColor(ElarionUi.PanelStoneDark);
            _panel.style.borderTopLeftRadius = 12; _panel.style.borderTopRightRadius = 12;
            _panel.style.borderBottomLeftRadius = 12; _panel.style.borderBottomRightRadius = 12;
            _panel.style.borderLeftWidth = 2; _panel.style.borderRightWidth = 2;
            _panel.style.borderTopWidth = 2; _panel.style.borderBottomWidth = 2;
            var panelRim = new StyleColor(new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
            _panel.style.borderLeftColor   = panelRim;
            _panel.style.borderRightColor  = panelRim;
            _panel.style.borderTopColor    = panelRim;
            _panel.style.borderBottomColor = panelRim;
            _panel.style.paddingLeft = 12; _panel.style.paddingRight = 12;
            _panel.style.paddingTop = 10;  _panel.style.paddingBottom = 10;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.display = DisplayStyle.None;
            _panel.pickingMode = PickingMode.Position;
            _root.Add(_panel);

            BuildHeader();
            BuildBody();
            Repaint();
        }

        private void BuildHeader()
        {
            _header = new VisualElement();
            _header.style.flexDirection = FlexDirection.Row;
            _header.style.justifyContent = Justify.SpaceBetween;
            _header.style.alignItems = Align.Center;
            _header.style.marginBottom = 8;
            _panel.Add(_header);

            _headerTitle = new Label("Clan Chat");
            _headerTitle.style.color = new StyleColor(ElarionUi.Gilt);
            _headerTitle.style.fontSize = 14;
            _headerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerTitle.style.flexGrow = 1;
            _header.Add(_headerTitle);

            _headerButton = new Button(OnHeaderButton) { text = "Create" };
            _headerButton.style.fontSize = 11;
            _headerButton.style.paddingLeft = 10; _headerButton.style.paddingRight = 10;
            _headerButton.style.paddingTop = 3;   _headerButton.style.paddingBottom = 3;
            _header.Add(_headerButton);

            // Close affordance (audit + fleet 2026-07-03: this panel had NO close at all —
            // once opened it could only be dismissed by re-toggling the opener). Named per
            // the close convention so the popup oracle finds it.
            var closeBtn = new Button(() => SetVisible(false)) { text = "Close", name = "CloseButton" };
            closeBtn.style.fontSize = 11;
            closeBtn.style.marginLeft = 6;
            closeBtn.style.paddingLeft = 10; closeBtn.style.paddingRight = 10;
            closeBtn.style.paddingTop = 3;   closeBtn.style.paddingBottom = 3;
            _header.Add(closeBtn);
        }

        private void BuildBody()
        {
            _body = new VisualElement();
            _body.style.flexDirection = FlexDirection.Column;
            _body.style.flexGrow = 1;
            _panel.Add(_body);

            // Create form (visible only when not in a clan)
            _createForm = new VisualElement();
            _createForm.style.flexDirection = FlexDirection.Column;
            _createForm.style.marginBottom = 6;
            _createForm.style.display = DisplayStyle.None;
            _body.Add(_createForm);

            _createNameField = new TextField("Clan name") { value = "Ember Wardens" };
            _createNameField.style.marginBottom = 4;
            _createForm.Add(_createNameField);

            _createTagField = new TextField("Tag (2–4)") { value = "EMBR" };
            _createTagField.style.marginBottom = 4;
            _createForm.Add(_createTagField);

            var confirmRow = new VisualElement();
            confirmRow.style.flexDirection = FlexDirection.Row;
            confirmRow.style.justifyContent = Justify.FlexEnd;
            _createForm.Add(confirmRow);

            var confirmBtn = new Button(OnConfirmCreate) { text = "Found Clan" };
            confirmRow.Add(confirmBtn);

            // Message scroll
            _messageScroll = new ScrollView(ScrollViewMode.Vertical);
            _messageScroll.style.flexGrow = 1;
            _messageScroll.style.minHeight = 160;
            _messageScroll.style.maxHeight = 260;
            _messageScroll.style.marginBottom = 6;
            _messageScroll.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.25f));
            _messageScroll.style.borderTopLeftRadius = 6; _messageScroll.style.borderTopRightRadius = 6;
            _messageScroll.style.borderBottomLeftRadius = 6; _messageScroll.style.borderBottomRightRadius = 6;
            _messageScroll.style.paddingLeft = 6; _messageScroll.style.paddingRight = 6;
            _messageScroll.style.paddingTop = 4;  _messageScroll.style.paddingBottom = 4;
            _body.Add(_messageScroll);

            // Phrase chip rail (wraps; one-tap send)
            _chipRail = new VisualElement();
            _chipRail.style.flexDirection = FlexDirection.Row;
            _chipRail.style.flexWrap = Wrap.Wrap;
            _chipRail.style.marginBottom = 4;
            _body.Add(_chipRail);

            // Composer (custom message)
            _composer = new VisualElement();
            _composer.style.flexDirection = FlexDirection.Column;
            _body.Add(_composer);

            var customToggleRow = new VisualElement();
            customToggleRow.style.flexDirection = FlexDirection.Row;
            customToggleRow.style.justifyContent = Justify.FlexStart;
            _composer.Add(customToggleRow);

            var customToggleBtn = new Button(ToggleCustomRow) { text = "Custom…" };
            customToggleBtn.style.fontSize = 11;
            customToggleRow.Add(customToggleBtn);

            _customRow = new VisualElement();
            _customRow.style.flexDirection = FlexDirection.Row;
            _customRow.style.marginTop = 4;
            _customRow.style.display = DisplayStyle.None;
            _composer.Add(_customRow);

            _customField = new TextField { maxLength = ClanService.CustomTextMaxChars };
            _customField.style.flexGrow = 1;
            _customField.style.marginRight = 6;
            _customRow.Add(_customField);

            var sendBtn = new Button(OnSendCustom) { text = "Send" };
            _customRow.Add(sendBtn);
        }

        // ── Repaint ──────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_panel == null || !_visible) return;
            var svc = ClanService.Instance;
            if (svc == null) return;

            if (svc.InClan)
            {
                var clan = svc.Current;
                var tag  = string.IsNullOrEmpty(clan.Tag) ? "" : $"[{clan.Tag}] ";
                _headerTitle.text = $"{tag}{clan.Name}";
                _headerButton.text = "Leave";
                _createForm.style.display = DisplayStyle.None;
                _messageScroll.style.display = DisplayStyle.Flex;
                _chipRail.style.display = DisplayStyle.Flex;
                _composer.style.display = DisplayStyle.Flex;
                RebuildMessages(svc);
                RebuildChips(svc);
            }
            else
            {
                _headerTitle.text = "Clan Chat — no clan";
                _headerButton.text = "Create";
                _createForm.style.display = DisplayStyle.Flex;
                _messageScroll.style.display = DisplayStyle.None;
                _chipRail.style.display = DisplayStyle.None;
                _composer.style.display = DisplayStyle.None;
            }
        }

        private void RebuildMessages(ClanService svc)
        {
            _messageScroll.Clear();
            var msgs = svc.Messages;
            if (msgs == null || msgs.Count == 0)
            {
                FlowTrace.Step("ClanChat",
                    "RebuildMessages: no messages yet — showing visible 'Send a phrase…' hint (expected empty, not a failure).");
                var hint = new Label("Send a phrase below to start the chat.");
                hint.style.color = new StyleColor(ElarionUi.ParchmentDim);
                hint.style.fontSize = 11;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                hint.style.marginTop = 6; hint.style.marginBottom = 6;
                _messageScroll.Add(hint);
            }
            else
            {
                foreach (var m in msgs)
                {
                    if (m == null) continue;
                    _messageScroll.Add(BuildMessageRow(m, svc.AccountId));
                }
            }
            // Auto-scroll to bottom on the next layout pass.
            _messageScroll.schedule.Execute(() =>
            {
                _messageScroll.scrollOffset =
                    new Vector2(0, float.MaxValue);
            }).StartingIn(0);
        }

        private static VisualElement BuildMessageRow(ChatMessage m, string localAccountId)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 4;

            var meta = new Label((m.SenderId == localAccountId ? "You" : (m.SenderName ?? "?"))
                                 + (m.IsCustom ? " · custom" : ""));
            meta.style.color = new StyleColor(ElarionUi.ParchmentDim);
            meta.style.fontSize = 10;
            row.Add(meta);

            var body = new Label(m.Text ?? "…");
            body.style.color = new StyleColor(ElarionUi.Parchment);
            body.style.fontSize = 12;
            body.style.whiteSpace = WhiteSpace.Normal;
            row.Add(body);

            return row;
        }

        private void RebuildChips(ClanService svc)
        {
            _chipRail.Clear();
            // Cap the chip rail at a comfortable density — full catalogue (24)
            // wraps cleanly into 3 short rows; no further pagination.
            var phrases = ChatPhraseCatalog.Phrases;
            // R (WORST offender): a null/empty phrase catalogue previously left the chip rail
            // utterly blank with NO fallback and NO trace — the chat read as broken. Show a
            // visible placeholder AND self-report (data-empty) instead of a silent empty rail.
            if (phrases == null || phrases.Count == 0)
            {
                FlowTrace.Warn("ClanChat",
                    $"RebuildChips: ChatPhraseCatalog.Phrases is {(phrases == null ? "null" : "empty")} — " +
                    "no quick-phrase chips to show; rendering visible 'Custom… to chat' fallback (data-empty).");
                var fallback = new Label("No quick phrases — use Custom… below to chat.");
                fallback.style.color = new StyleColor(ElarionUi.ParchmentDim);
                fallback.style.fontSize = 11;
                fallback.style.unityFontStyleAndWeight = FontStyle.Italic;
                fallback.style.width = new Length(100, LengthUnit.Percent);
                fallback.style.marginTop = 4; fallback.style.marginBottom = 4;
                _chipRail.Add(fallback);
                return;
            }
            int chipCount = 0;
            string lastCategory = null;
            foreach (var p in phrases)
            {
                if (p == null) continue;
                if (p.Category != lastCategory)
                {
                    lastCategory = p.Category;
                    var divider = new Label(ResolveCategoryLabel(p.Category));
                    divider.style.color = new StyleColor(new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f));
                    divider.style.fontSize = 10;
                    divider.style.unityFontStyleAndWeight = FontStyle.Bold;
                    divider.style.width = new Length(100, LengthUnit.Percent);
                    divider.style.marginTop = 4;
                    divider.style.marginBottom = 2;
                    _chipRail.Add(divider);
                }
                _chipRail.Add(BuildChip(p));
                chipCount++;
            }
            // All phrases were null entries -> rail still empty. Keep the never-blank contract.
            if (chipCount == 0)
            {
                FlowTrace.Warn("ClanChat",
                    "RebuildChips: every phrase entry was null — rendering visible 'Custom… to chat' fallback (data-empty).");
                var fallback = new Label("No quick phrases — use Custom… below to chat.");
                fallback.style.color = new StyleColor(ElarionUi.ParchmentDim);
                fallback.style.fontSize = 11;
                fallback.style.unityFontStyleAndWeight = FontStyle.Italic;
                fallback.style.width = new Length(100, LengthUnit.Percent);
                fallback.style.marginTop = 4; fallback.style.marginBottom = 4;
                _chipRail.Add(fallback);
            }
        }

        private static string ResolveCategoryLabel(string key)
        {
            foreach (var c in ChatPhraseCatalog.Categories)
                if (c != null && c.Key == key) return c.Label;
            return key ?? "Phrases";
        }

        private VisualElement BuildChip(ChatPhraseDef p)
        {
            var label = string.IsNullOrEmpty(p.Emoji) ? p.Text : $"{p.Emoji} {p.Text}";
            var chip = new Button(() => OnSendPhrase(p.Id)) { text = label };
            chip.style.fontSize = 11;
            chip.style.marginRight = 4;
            chip.style.marginBottom = 4;
            chip.style.paddingLeft = 8; chip.style.paddingRight = 8;
            chip.style.paddingTop = 2;  chip.style.paddingBottom = 2;
            chip.style.backgroundColor = new StyleColor(ElarionUi.AetherDim);
            chip.style.color = new StyleColor(ElarionUi.Parchment);
            chip.style.borderTopLeftRadius = 10; chip.style.borderTopRightRadius = 10;
            chip.style.borderBottomLeftRadius = 10; chip.style.borderBottomRightRadius = 10;
            return chip;
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnHeaderButton()
        {
            var svc = ClanService.Instance;
            if (svc == null) return;
            if (svc.InClan) svc.LeaveClan();
            else _createForm.style.display = DisplayStyle.Flex;
        }

        private void OnConfirmCreate()
        {
            var svc = ClanService.Instance;
            if (svc == null) return;
            var name = _createNameField != null ? _createNameField.value : "Ember Wardens";
            var tag  = _createTagField  != null ? _createTagField.value  : "EMBR";
            svc.CreateClan(name, tag);
        }

        private void OnSendPhrase(string phraseId)
        {
            var svc = ClanService.Instance;
            if (svc == null || !svc.InClan) return;
            svc.AddTemplatedMessage(phraseId);
        }

        private void ToggleCustomRow()
        {
            _customOpen = !_customOpen;
            if (_customRow != null)
                _customRow.style.display = _customOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_customOpen && _customField != null)
                _customField.Focus();
        }

        private void OnSendCustom()
        {
            var svc = ClanService.Instance;
            if (svc == null || !svc.InClan) return;
            var text = _customField != null ? _customField.value : null;
            svc.AddCustomMessage(text);
            if (_customField != null) _customField.value = string.Empty;
        }
    }
}
