// =============================================================================
// GameGuidePanel — the Game Guide / tutorial codex VIEW (WO-588). A DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// An optional, always-available help codex opened from Settings. Code-built uGUI
// (NO UXML — §8): the SHARED Obsidian chrome (black panel + gold trim + ONE Close,
// ElarionUiKit.BuildObsidianPanel) with a LEFT vertical tab rail (one button per
// section, scrollable) and a RIGHT scrollable body (selected section's title +
// body paragraphs + a Tips list). All content/logic live in GuideVM /
// GuideContentCatalog — this View only renders + raises SelectTab.
//
// Registers PanelId.GameGuide (PanelRouter) + the modal arbiter (PanelManager) so
// it swaps cleanly with every other panel (one-modal-at-a-time). Renders correctly
// with ff.blinkchrome ON and OFF (the procedural Obsidian panel is flag-agnostic).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class GameGuidePanel : MonoBehaviour
    {
        private GuideVM _vm;
        private GameObject _ui;
        private RectTransform _railContent;   // scrollable left tab rail content
        private RectTransform _bodyContent;   // scrollable right body content

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        // ── Registration (mirror HeroSkillTreePanelMvvm / EquipmentPanel) ─────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Game Guide", Close, () => IsOpen);
            PanelRouter.Register(PanelId.GameGuide, Open);
        }

        private void OnDestroy()
        {
            if (_vm != null) { _vm.Changed -= Render; _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.GameGuide, Open);
        }

        // ── Open: build chrome, construct + bind the VM ───────────────────────────

        public void Open()
        {
            Close();
            BuildChrome();

            _vm = new GuideVM();
            _vm.Changed += Render;
            Render();

            if (!PanelManager.NotifyOpened(_panelHandle))
                return; // rejected (e.g. in battle) — NotifyOpened already invoked Close.

            Debug.Log("[GameGuidePanel] Opened — Game Guide codex bound to GuideVM (" + _vm.Count + " sections).");
        }

        private void Close()
        {
            if (_vm != null) { _vm.Changed -= Render; _vm = null; }
            _railContent = null;
            _bodyContent = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelManager.NotifyClosed(_panelHandle);
        }

        // ── Chrome (presentation only) ────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("GameGuidePanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // SHARED Obsidian chrome (procedural — black panel + gold trim + gold header + ONE Close).
            // No frameName so it renders identically with ff.blinkchrome ON and OFF.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Game Guide",
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), Close,
                headerX0: 0.06f, headerX1: 0.80f);
            Transform content = chrome.content.transform;

            // Left vertical tab rail (scrollable) + right scrollable body.
            _railContent = AddVerticalScroll(content,
                new Vector2(0.035f, 0.045f), new Vector2(0.295f, 0.885f), padding: 8, spacing: 6f);
            _bodyContent = AddVerticalScroll(content,
                new Vector2(0.320f, 0.045f), new Vector2(0.965f, 0.885f), padding: 16, spacing: 10f);
        }

        // ── Render: repaint rail + body from vm.* ONLY ────────────────────────────

        private void Render()
        {
            if (_vm == null) return;
            RebuildRail();
            RebuildBody();

            // Force a layout pass so the vertical layout + content size resolve immediately.
            Canvas.ForceUpdateCanvases();
            if (_railContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_railContent);
            if (_bodyContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_bodyContent);
        }

        private void RebuildRail()
        {
            ClearChildren(_railContent);
            if (_railContent == null || _vm == null) return;

            var tabs = _vm.Tabs;
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                bool selected = i == _vm.SelectedIndex;
                BuildTabButton(_railContent, tabs[i], selected, () => _vm.SelectTab(index));
            }
        }

        private void RebuildBody()
        {
            ClearChildren(_bodyContent);
            if (_bodyContent == null || _vm == null) return;

            if (!_vm.HasSelection)
            {
                Paragraph(_bodyContent, "No guide content available.",
                    ElarionUi.FontBody, ElarionUi.ParchmentDim, bold: false);
                return;
            }

            // Title (+ subtle "coming soon" tag for not-yet-built systems).
            Paragraph(_bodyContent, _vm.SelectedTitle, ElarionUi.FontTitle, ElarionUi.Gilt, bold: true);
            if (_vm.SelectedIsComing)
                Paragraph(_bodyContent, "(coming soon)", ElarionUi.FontLabel, ElarionUi.ParchmentDim, bold: false);

            // Body paragraphs.
            var body = _vm.SelectedBody;
            for (int i = 0; i < body.Count; i++)
                Paragraph(_bodyContent, body[i], ElarionUi.FontBody, ElarionUi.Parchment, bold: false);

            // Tips list.
            var tips = _vm.SelectedTips;
            if (tips.Count > 0)
            {
                Paragraph(_bodyContent, "Tips", ElarionUi.FontHead, ElarionUi.Gilt, bold: true);
                for (int i = 0; i < tips.Count; i++)
                    Paragraph(_bodyContent, "•  " + tips[i], ElarionUi.FontLabel, ElarionUi.ParchmentDim, bold: false);
            }
        }

        // ── Builders ──────────────────────────────────────────────────────────────

        // A scrollable vertical region: transparent viewport (+ mask) + ScrollRect +
        // top-anchored content with a VerticalLayoutGroup + ContentSizeFitter. Returns
        // the content RectTransform — parent rows under it; they stack + scroll.
        private static RectTransform AddVerticalScroll(Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, int padding, float spacing)
        {
            var viewport = ElarionUiKit.AddImage(parent, "Viewport", anchorMin, anchorMax,
                new Color(0f, 0f, 0f, 0.18f));
            var vImg = viewport.GetComponent<Image>();
            if (vImg != null) vImg.raycastTarget = true;   // eat drag-scroll
            viewport.AddComponent<RectMask2D>();

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = crt;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            return crt;
        }

        // One tab-rail button: a fixed-height plate (gold-warm when selected, quiet glass
        // otherwise) with a wrapped label. Tapping fires onTap (vm.SelectTab).
        private void BuildTabButton(Transform parent, string label, bool selected, System.Action onTap)
        {
            var go = new GameObject("Tab", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = selected
                ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.42f)
                : ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(img);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            if (onTap != null) btn.onClick.AddListener(() => onTap());

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 52f;
            le.preferredHeight = 52f;
            le.flexibleWidth = 1f;

            var lblGo = new GameObject("Label", typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(8f, 2f);
            lrt.offsetMax = new Vector2(-6f, -2f);
            var t = lblGo.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = label ?? string.Empty;
            t.fontSize = ElarionUi.FontLabel;
            t.color = selected ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
            t.alignment = TextAlignmentOptions.Left;
            t.enableWordWrapping = true;
            t.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
        }

        // A wrapped TMP paragraph sized by the parent VerticalLayoutGroup (preferred height).
        private static void Paragraph(Transform parent, string text, int size, Color color, bool bold)
        {
            var go = new GameObject("Para", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text ?? string.Empty;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
        }

        private static void ClearChildren(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var c = host.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }
    }
}
