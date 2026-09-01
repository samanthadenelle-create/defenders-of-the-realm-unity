// =============================================================================
// GameGuidePanel — the Game Guide / tutorial codex VIEW (WO-588). A DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// An optional, always-available help codex opened from Settings. Code-built uGUI
// (NO UXML — §8), on the pack QUESTS reference (WO-714 Phase 2 W3 conformance,
// 2026-07-13): FrameQuest (Quest_Log_Panel) master-detail —
//   * bodyLeft  (dark well)      = the section list (kit Obsidian buttons in a
//                                  kit scroll zone; selected = Yellow face)
//   * bodyRight (parchment well) = the selected section's prose (title + body
//                                  paragraphs + Tips) in PARCHMENT INK, inside
//                                  a kit scroll zone — long sections scroll.
// The frame supplies ALL chrome + the ONE shared Close; the old hand-rolled tab
// plates (per-screen gold-alpha Image/Button) and hand-rolled ScrollRect plumbing
// are replaced by kit builders (BuildObsidianButton / MakeScrollZone).
// All content/logic live in GuideVM / GuideContentCatalog — this View only
// renders + raises SelectTab.
//
// Registers PanelId.GameGuide (PanelRouter) + the modal arbiter (PanelManager) so
// it swaps cleanly with every other panel (one-modal-at-a-time). Renders correctly
// with the frame art present AND absent (procedural fallback zones — never blank).
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
        private RectTransform _railContent;   // scrollable section-list content (dark well)
        private RectTransform _bodyContent;   // scrollable prose content (parchment well)

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

            Debug.Log("[GameGuidePanel] Opened - Game Guide codex bound to GuideVM (" + _vm.Count + " sections).");
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

        // ── Chrome (presentation only — the frame IS the chrome, canon §0) ────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("GameGuidePanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // The pack QUESTS reference (WO-714 W3): FrameQuest master-detail — the
            // codex is a list + readable-prose surface, exactly the Quest_Log grammar
            // (Table A: "Quest / codex / assault manifest -> FrameQuest"). The kit
            // supplies the split wells, medallion, title and the ONE shared Close.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Game Guide",
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Close,
                frameName: RpgUiCatalog.FrameQuest, medallionIcon: "quest");

            // Drop-zones only (canon §4): split wells when the frame resolves; on the
            // PROCEDURAL path (frame art absent) build fallback zones ABOVE the shared
            // Close band (chrome.content fractions were the unprotected class — sweep
            // 9413 R2 #8 — so the fallback bottoms stay at the proven 0.22 floor).
            var layout = chrome.layout;
            Transform railZone = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : FallbackZone(chrome.content.transform, "RailWell",
                    new Vector2(0.035f, 0.22f), new Vector2(0.295f, 0.885f));
            Transform proseZone = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : FallbackZone(chrome.content.transform, "ProseWell",
                    new Vector2(0.320f, 0.22f), new Vector2(0.965f, 0.885f));
            bool onParchment = layout != null && layout.bodyRight != null;
            _onParchment = onParchment;

            // Kit scroll zones (§1.14 MakeScrollZone — ONE call per zone; the hand-rolled
            // viewport/ScrollRect plumbing is gone). Rows/paragraphs parent to .content.
            _railContent = ElarionUiKit.MakeScrollZone(railZone, spacing: 8f, padding: 8).content;
            _bodyContent = ElarionUiKit.MakeScrollZone(proseZone, spacing: 12f, padding: 16).content;
        }

        // True when the prose well sits on the frame's parchment plate (dark ink);
        // false on the procedural fallback (dark panel — light parchment text).
        private bool _onParchment;

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
                // Fixed-height row host (the kit scroll column does not control child
                // height — §1.14 kit note, the captured PartyShop collapse), with the
                // kit Obsidian button filling it: selected = Yellow face (the Jeweler/
                // Crafting master-list grammar), quiet Gray otherwise.
                var host = new GameObject("TabRow", typeof(RectTransform), typeof(LayoutElement));
                host.transform.SetParent(_railContent, false);
                var hostRt = (RectTransform)host.transform;
                hostRt.sizeDelta = new Vector2(0f, 144f);
                var le = host.GetComponent<LayoutElement>();
                // FrameGuide's body is scaled relative to the reference surface; 120 local
                // units resolve to only 100 reference px at 1920x1080. Author above that scale
                // so every topic row still clears the global 112px mobile touch floor.
                le.preferredHeight = 144f;
                le.minHeight = 144f;
                var tabButton = ElarionUiKit.BuildObsidianButton(host.transform, tabs[i],
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    Vector2.zero, Vector2.one, () => _vm.SelectTab(index));
                // Prefab-backed Obsidian buttons carry a legacy fixed 120-unit root rect.
                // Explicitly make the interactive root consume the authored row host; otherwise
                // the visual can look tall while the real hit target remains below the mobile floor.
                if (tabButton != null && tabButton.transform is RectTransform tabRt)
                {
                    tabRt.anchorMin = Vector2.zero;
                    tabRt.anchorMax = Vector2.one;
                    tabRt.offsetMin = Vector2.zero;
                    tabRt.offsetMax = Vector2.zero;
                }
            }
        }

        private void RebuildBody()
        {
            ClearChildren(_bodyContent);
            if (_bodyContent == null || _vm == null) return;

            // Parchment-ink palette when the prose sits on the frame's parchment well
            // (dark-on-tan, the WO-693 detail-card ink); light parchment text on the
            // procedural dark fallback. Same grammar, legible on either plate.
            Color inkTitle = _onParchment ? ElarionUiKit.ParchmentInk    : ElarionUi.Gilt;
            Color inkBody  = _onParchment ? ElarionUiKit.ParchmentInk    : ElarionUi.Parchment;
            Color inkDim   = _onParchment ? ElarionUiKit.ParchmentInkDim : ElarionUi.ParchmentDim;

            if (!_vm.HasSelection)
            {
                Paragraph(_bodyContent, "No guide content available.",
                    ElarionUi.FontBody, inkDim, bold: false);
                return;
            }

            // Title (+ subtle "coming soon" tag for not-yet-built systems).
            Paragraph(_bodyContent, _vm.SelectedTitle, ElarionUi.FontHead, inkTitle, bold: true);
            if (_vm.SelectedIsComing)
                Paragraph(_bodyContent, "(coming soon)", ElarionUi.FontLabel, inkDim, bold: false, italic: true);

            // Body paragraphs.
            var body = _vm.SelectedBody;
            for (int i = 0; i < body.Count; i++)
                Paragraph(_bodyContent, body[i], ElarionUi.FontBody, inkBody, bold: false);

            // Tips list (ASCII "-" bullets — the build TMP font tofu's non-ASCII glyphs).
            var tips = _vm.SelectedTips;
            if (tips.Count > 0)
            {
                Paragraph(_bodyContent, "TIPS", ElarionUi.FontLabel, inkDim, bold: true);
                for (int i = 0; i < tips.Count; i++)
                    Paragraph(_bodyContent, "-  " + tips[i], ElarionUi.FontLabel, inkDim, bold: false);
            }
        }

        // ── Builders (layout plumbing only — chrome comes from the kit) ───────────

        // A wrapped TMP paragraph inside the kit scroll column. The column does NOT
        // control child height (§1.14), so each paragraph carries its own vertical
        // ContentSizeFitter (the DialogueView pattern) — the column's fitter sums the
        // real heights into a scrollable content height.
        private static void Paragraph(Transform parent, string text, int size, Color color,
            bool bold, bool italic = false)
        {
            var go = new GameObject("Para", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text ?? string.Empty;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            if (italic) t.fontStyle |= FontStyles.Italic;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        // Fallback drop-zone for the PROCEDURAL (frame-art-absent) path only —
        // bottoms at the proven 0.22 Close-band floor (sweep 9413 R2 #8).
        private static Transform FallbackZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
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
