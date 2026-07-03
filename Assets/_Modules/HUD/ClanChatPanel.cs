// =============================================================================
// ClanChatPanel — toggleable team-chat panel for the Clans stub.
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #51): UIDocument/UITK panel
// -> code-built uGUI on the Obsidian master frame (BuildObsidianModal: FrameCore
// + medallion + the ONE shared Close + tap-outside scrim), per the LeaderboardPanel
// / HelpMenu reference recipe. Row #51 flagged ClanChat as having NO close at all —
// the kit chrome's shared Close now fixes that AND it registers with PanelManager
// so opening closes other panels and the arbiter can dismiss it (was squatting over
// Talents/Upgrade in every bot capture). Opens via Toggle() (the kit HUD dock calls
// it directly); reads ClanService.Instance directly (DeNelle.HUD -> DeNelle.Core).
//
// Layout (in the frame's body well):
//   • Status strip — clan tag + name (or "no clan"); Create/Leave action button.
//   • Create form — name + tag input fields + "Found Clan" (only when not in a clan).
//   • Scrollable message list — oldest at top, newest at bottom.
//   • Phrase-chip rail — one-tap Obsidian buttons that post a templated phrase.
//   • Composer — "Custom..." reveals an input + Send for <=140 char free text.
//
// Single-player only. The network bridge will swap ClanService for a thin remote
// wrapper later (§7.1 of the React design doc).
// =============================================================================

using DeNelle.Core.Services;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class ClanChatPanel : MonoBehaviour
    {
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _statusHost;
        private Transform _actionHost;
        private Transform _createForm;
        private Transform _messageScrollHost;
        private Transform _listContent;    // message ScrollRect content (VerticalLayoutGroup)
        private Transform _chipContent;     // phrase-chip ScrollRect content
        private Transform _composerHost;
        private Transform _customRow;
        private TMP_InputField _createNameField;
        private TMP_InputField _createTagField;
        private TMP_InputField _customField;

        private bool _visible;
        private bool _customOpen;

        // Modal arbiter membership (eyes-on pass 2026-07-03: the open chat squatted OVER
        // the Talents/Upgrade modals in every bot capture — it never told PanelManager it
        // was open, so nothing ever closed it; and it exposed NO close affordance at all).
        private PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Clan Chat", () => SetVisible(false), () => _visible);
        }

        private void OnEnable()
        {
            if (ClanService.Instance != null)
                ClanService.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (ClanService.Instance != null)
                ClanService.Instance.Changed -= Repaint;
        }

        private void OnDestroy()
        {
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        // Mobile-first: the panel opens via Toggle() (public), called by the kit HUD chat
        // dock (HudKitController.OpenClanChat). No key poll, no 'Y' hotkey.
        public void Toggle() => SetVisible(!_visible);

        private void SetVisible(bool on)
        {
            if (on)
            {
                FlowTrace.Step("ClanChat", "SetVisible(true) — opening clan chat panel.");
                EnsureBuilt();
            }
            if (_modal == null || _modal.canvas == null) { _visible = false; return; }
            _visible = on;
            _modal.canvas.SetActive(on);
            if (on)
            {
                if (!PanelManager.NotifyOpened(_panelHandle))
                {
                    _visible = false;
                    _modal.canvas.SetActive(false);   // battle-lock reject — never force-show
                    return;
                }
                Repaint();
            }
            else
            {
                PanelManager.NotifyClosed(_panelHandle);
            }
        }

        // ── UI construction (kit modal, lazy on first open) ──────────────────
        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;
            using var _ = FlowTrace.Enter("ClanChat", "EnsureBuilt");

            _modal = ElarionUiKit.BuildObsidianModal("ClanChatUI", "Clan Chat",
                new Vector2(0.24f, 0.10f), new Vector2(0.76f, 0.92f), () => SetVisible(false),
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "crest");

            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? (Transform)_modal.chrome.layout.body
                : _modal.chrome.content.transform;

            // Status strip + Create/Leave action button (top of the well).
            _statusHost = ZoneRect(body, "StatusStrip", new Vector2(0.03f, 0.90f), new Vector2(0.72f, 1.00f));
            _actionHost = ZoneRect(body, "ActionHost",  new Vector2(0.73f, 0.90f), new Vector2(0.99f, 1.00f));

            // Create form (shown only when not in a clan) — occupies the message region.
            _createForm = ZoneRect(body, "CreateForm", new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.88f));
            _createNameField = MakeInputField(_createForm, "Clan name", "Ember Wardens", 24,
                new Vector2(0f, 0.78f), new Vector2(1f, 0.94f));
            _createTagField = MakeInputField(_createForm, "Tag (2-4)", "EMBR", 4,
                new Vector2(0f, 0.58f), new Vector2(1f, 0.74f));
            ElarionUiKit.BuildObsidianButton(_createForm, "Found Clan",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.30f, 0.40f), new Vector2(0.70f, 0.54f), OnConfirmCreate);

            // Message scroll list (shown only when in a clan).
            _messageScrollHost = ZoneRect(body, "MessageScroll", new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.88f));
            _listContent = BuildScrollColumn(_messageScrollHost);

            // Phrase-chip rail (Obsidian buttons, scrollable, category-grouped).
            var chipHost = ZoneRect(body, "ChipRail", new Vector2(0.03f, 0.11f), new Vector2(0.97f, 0.29f));
            _chipContent = BuildScrollColumn(chipHost);

            // Composer (Custom... toggle + input + Send).
            _composerHost = ZoneRect(body, "Composer", new Vector2(0.03f, 0.00f), new Vector2(0.97f, 0.10f));
            ElarionUiKit.BuildObsidianButton(_composerHost, "Custom...",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0f, 0.05f), new Vector2(0.20f, 0.95f), ToggleCustomRow);
            _customRow = ZoneRect(_composerHost, "CustomRow", new Vector2(0.21f, 0.05f), new Vector2(1f, 0.95f));
            _customField = MakeInputField(_customRow, "Say something...", "", ClanService.CustomTextMaxChars,
                new Vector2(0f, 0f), new Vector2(0.80f, 1f));
            ElarionUiKit.BuildObsidianButton(_customRow, "Send",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.82f, 0f), new Vector2(1f, 1f), OnSendCustom);
            _customRow.gameObject.SetActive(false);

            _modal.canvas.SetActive(false);   // built hidden; SetVisible shows it
        }

        // ── Repaint ──────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_modal == null || !_visible) return;
            var svc = ClanService.Instance;
            if (svc == null) return;

            // Action button (Create / Leave) — rebuilt so its label + color reflect state.
            for (int i = _actionHost.childCount - 1; i >= 0; i--)
                Destroy(_actionHost.GetChild(i).gameObject);
            ElarionUiKit.BuildObsidianButton(_actionHost, svc.InClan ? "Leave" : "Create",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                svc.InClan ? ElarionUiKit.ObsidianButtonColor.Red : ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0f, 0.05f), new Vector2(1f, 0.95f), OnHeaderButton);

            for (int i = _statusHost.childCount - 1; i >= 0; i--)
                Destroy(_statusHost.GetChild(i).gameObject);

            if (svc.InClan)
            {
                var clan = svc.Current;
                var tag  = string.IsNullOrEmpty(clan.Tag) ? "" : $"[{clan.Tag}] ";
                MakeText(_statusHost, $"{tag}{clan.Name}", 18, ElarionUi.Gilt, FontStyles.Bold,
                    TextAlignmentOptions.Left, Vector2.zero, Vector2.one);

                _createForm.gameObject.SetActive(false);
                _messageScrollHost.gameObject.SetActive(true);
                _chipContent.parent.parent.gameObject.SetActive(true);
                _composerHost.gameObject.SetActive(true);
                RebuildMessages(svc);
                RebuildChips(svc);
            }
            else
            {
                MakeText(_statusHost, "No clan yet", 18, ElarionUi.ParchmentDim, FontStyles.Italic,
                    TextAlignmentOptions.Left, Vector2.zero, Vector2.one);

                _createForm.gameObject.SetActive(true);
                _messageScrollHost.gameObject.SetActive(false);
                _chipContent.parent.parent.gameObject.SetActive(false);
                _composerHost.gameObject.SetActive(false);
            }
        }

        private void RebuildMessages(ClanService svc)
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            var msgs = svc.Messages;
            if (msgs == null || msgs.Count == 0)
            {
                FlowTrace.Step("ClanChat",
                    "RebuildMessages: no messages yet — showing visible 'Send a phrase...' hint (expected empty, not a failure).");
                AddMessageRow("", "Send a phrase below to start the chat.", true);
                return;
            }

            foreach (var m in msgs)
            {
                if (m == null) continue;
                var meta = (m.SenderId == svc.AccountId ? "You" : (m.SenderName ?? "?"))
                           + (m.IsCustom ? " - custom" : "");
                AddMessageRow(meta, m.Text ?? "...", false);
            }
        }

        private void AddMessageRow(string meta, string body, bool hint)
        {
            var rowGo = new GameObject("Msg", typeof(RectTransform), typeof(LayoutElement));
            rowGo.transform.SetParent(_listContent, false);
            var le = rowGo.GetComponent<LayoutElement>();
            le.preferredHeight = string.IsNullOrEmpty(meta) ? 40f : 48f;
            var rrt = rowGo.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(rrt.sizeDelta.x, le.preferredHeight);

            if (!string.IsNullOrEmpty(meta))
                MakeText(rowGo.transform, meta, 11, ElarionUi.ParchmentDim, FontStyles.Normal,
                    TextAlignmentOptions.TopLeft, new Vector2(0f, 0.66f), new Vector2(1f, 1f));

            var bodyColor = hint ? ElarionUi.ParchmentDim : ElarionUi.Parchment;
            var bodyStyle = hint ? FontStyles.Italic : FontStyles.Normal;
            MakeText(rowGo.transform, body, 13, bodyColor, bodyStyle,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 0f),
                new Vector2(1f, string.IsNullOrEmpty(meta) ? 1f : 0.66f));
        }

        private void RebuildChips(ClanService svc)
        {
            for (int i = _chipContent.childCount - 1; i >= 0; i--)
                Destroy(_chipContent.GetChild(i).gameObject);

            var phrases = ChatPhraseCatalog.Phrases;
            // Never-blank contract: a null/empty phrase catalogue previously left the rail
            // utterly blank with NO fallback and NO trace — the chat read as broken. Show a
            // visible placeholder AND self-report (data-empty).
            if (phrases == null || phrases.Count == 0)
            {
                FlowTrace.Warn("ClanChat",
                    $"RebuildChips: ChatPhraseCatalog.Phrases is {(phrases == null ? "null" : "empty")} — " +
                    "no quick-phrase chips; rendering visible 'Custom... to chat' fallback (data-empty).");
                AddChipFallback();
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
                    AddChipDivider(ResolveCategoryLabel(p.Category));
                }
                AddChip(p);
                chipCount++;
            }
            if (chipCount == 0)
            {
                FlowTrace.Warn("ClanChat",
                    "RebuildChips: every phrase entry was null — rendering visible 'Custom... to chat' fallback (data-empty).");
                AddChipFallback();
            }
        }

        private void AddChipDivider(string label)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_chipContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 16f;
            var drt = go.GetComponent<RectTransform>();
            drt.sizeDelta = new Vector2(drt.sizeDelta.x, 16f);
            MakeText(go.transform, label, 11,
                new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f),
                FontStyles.Bold, TextAlignmentOptions.Left, Vector2.zero, Vector2.one);
        }

        private void AddChip(ChatPhraseDef p)
        {
            var label = string.IsNullOrEmpty(p.Emoji) ? p.Text : $"{p.Emoji} {p.Text}";
            var host = new GameObject("Chip", typeof(RectTransform), typeof(LayoutElement));
            host.transform.SetParent(_chipContent, false);
            host.GetComponent<LayoutElement>().preferredHeight = 32f;
            var hrt = host.GetComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(hrt.sizeDelta.x, 32f);
            ElarionUiKit.BuildObsidianButton(host.transform, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                Vector2.zero, Vector2.one, () => OnSendPhrase(p.Id));
        }

        private void AddChipFallback()
        {
            var go = new GameObject("ChipFallback", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_chipContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 28f;
            var frt = go.GetComponent<RectTransform>();
            frt.sizeDelta = new Vector2(frt.sizeDelta.x, 28f);
            MakeText(go.transform, "No quick phrases — use Custom... below to chat.", 12,
                ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.Left, Vector2.zero, Vector2.one);
        }

        private static string ResolveCategoryLabel(string key)
        {
            foreach (var c in ChatPhraseCatalog.Categories)
                if (c != null && c.Key == key) return c.Label;
            return key ?? "Phrases";
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnHeaderButton()
        {
            var svc = ClanService.Instance;
            if (svc == null) return;
            if (svc.InClan) svc.LeaveClan();
            // else: the create form is already visible (not-in-clan state) — no-op.
        }

        private void OnConfirmCreate()
        {
            var svc = ClanService.Instance;
            if (svc == null) return;
            var name = _createNameField != null ? _createNameField.text : "Ember Wardens";
            var tag  = _createTagField  != null ? _createTagField.text  : "EMBR";
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
                _customRow.gameObject.SetActive(_customOpen);
            if (_customOpen && _customField != null)
                _customField.ActivateInputField();
        }

        private void OnSendCustom()
        {
            var svc = ClanService.Instance;
            if (svc == null || !svc.InClan) return;
            var text = _customField != null ? _customField.text : null;
            svc.AddCustomMessage(text);
            if (_customField != null) _customField.text = string.Empty;
        }

        // ── uGUI helpers (mirrors LeaderboardPanel) ──────────────────────────

        private static Transform ZoneRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }

        // Inline ScrollRect + VerticalLayoutGroup content column (canonical helper copied from
        // CosmeticShopPanel/LeaderboardPanel — the SME referenced it but omitted the definition,
        // gate CS0103). Returns the content transform rows/chips are added to.
        private static Transform BuildScrollColumn(Transform host)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(host, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return contentGo.transform;
        }

        // Inline TMP_InputField over a translucent rounded well (mirrors BugReportView).
        private static TMP_InputField MakeInputField(Transform parent, string placeholder,
            string initialValue, int maxLength, Vector2 min, Vector2 max)
        {
            var host = new GameObject("Input", typeof(Image), typeof(TMP_InputField));
            host.transform.SetParent(parent, false);
            var rt = (RectTransform)host.transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = host.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            ElarionUiKit.ApplyRounded(bg);

            var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(host.transform, false);
            var art = (RectTransform)areaGo.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(10f, 4f); art.offsetMax = new Vector2(-10f, -4f);

            var text = ElarionUiKit.Label(areaGo.transform, "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Left, 0f, 1f);
            var ph = ElarionUiKit.Label(areaGo.transform, placeholder, 0f, 1f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TextAlignmentOptions.Left, 0f, 1f);
            ph.fontStyle = FontStyles.Italic;

            var field = host.GetComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport  = art;
            field.textComponent = text;
            field.placeholder   = ph;
            field.lineType      = TMP_InputField.LineType.SingleLine;
            field.characterLimit = maxLength;
            field.text = initialValue ?? "";
            return field;
        }
    }
}
