// =============================================================================
// LeaderboardPanel — toggleable leaderboard + player-profile modal (WO-129).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// WO-F conversion (2026-07-03, coverage matrix row #50): UIDocument/UITK card ->
// code-built uGUI on the Obsidian master frame (BuildObsidianModal: FrameCore +
// medallion + the ONE shared Close + scrim), per the HelpMenu reference recipe.
// Opens via Toggle() (the kit dock). Strict MVVM (Silo E): binds a LeaderboardVM
// and reads vm.* only — all LeaderboardService access + the async fetch live in the VM.
//
// Layout (in the frame's body well):
//   • Profile strip — "You", invite code, best wave / crystals / arena W-L.
//   • Metric tabs — Best Wave / Crystals / Arena Wins (re-fetch on tap;
//     active tab = Yellow Obsidian button).
//   • Ranked scroll list — rank, name, score; the local player's row is gold.
//   • Footer — an HONEST source badge ("Local (offline)…") when the source is
//     the stub, so we never pretend the (undeployed) backend is live.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Services;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _profileHost;
        private Transform _tabHost;
        private Transform _listContent;   // ScrollRect content (VerticalLayoutGroup)
        private TextMeshProUGUI _footer;

        private bool _visible;
        private PanelHandle _panelHandle;

        // Strict MVVM (Silo E): ALL leaderboard state + the async fetch live in the VM;
        // this View reads vm.* only and never touches LeaderboardService.
        private LeaderboardVM _vm;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Leaderboard", () => SetVisible(false), () => _visible);
        }

        private void OnDestroy()
        {
            if (_vm != null) _vm.Changed -= Render;
            _vm?.Dispose();
            _vm = null;
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        public void Toggle() => SetVisible(!_visible);

        private void SetVisible(bool on)
        {
            if (on)
            {
                FlowTrace.Step("Leaderboard", "SetVisible(true) — opening leaderboard panel.");
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
                _vm?.Refresh();   // re-pull profile + rows (raises Changed -> Render)
                Render();
            }
            else
            {
                PanelManager.NotifyClosed(_panelHandle);
            }
        }

        // ── UI construction (kit modal, lazy on first open) ─────────────────
        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            _modal = ElarionUiKit.BuildObsidianModal("LeaderboardUI", "Leaderboard",
                new Vector2(0.26f, 0.10f), new Vector2(0.74f, 0.92f), () => SetVisible(false),
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "combat");

            // VM FIRST — it resolves LeaderboardService itself + owns the async fetch.
            _vm = LeaderboardVM.CreateDefault(() => SetVisible(false));
            _vm.Changed += Render;

            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? (Transform)_modal.chrome.layout.body
                : _modal.chrome.content.transform;

            // Profile strip (top of the well).
            _profileHost = ZoneRect(body, "ProfileStrip", new Vector2(0.03f, 0.86f), new Vector2(0.97f, 1.00f));
            // Metric tabs.
            _tabHost = ZoneRect(body, "TabRail", new Vector2(0.03f, 0.76f), new Vector2(0.97f, 0.85f));
            BuildTabs();
            // Ranked scroll list.
            BuildList(body);
            // Footer (source honesty badge).
            var footHost = ZoneRect(body, "Footer", new Vector2(0.03f, 0.00f), new Vector2(0.97f, 0.07f));
            _footer = MakeText(footHost, "", 12, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

            _modal.canvas.SetActive(false);   // built hidden; SetVisible shows it
        }

        private void BuildTabs()
        {
            // Rebuilt whole on metric switch so the active tab re-colors (Yellow = active).
            for (int i = _tabHost.childCount - 1; i >= 0; i--)
                Destroy(_tabHost.GetChild(i).gameObject);

            AddTab(LeaderboardMetric.BestWave,  "Best Wave",  0);
            AddTab(LeaderboardMetric.Crystals,  "Crystals",   1);
            AddTab(LeaderboardMetric.ArenaWins, "Arena Wins", 2);
        }

        private void AddTab(LeaderboardMetric metric, string label, int index)
        {
            float x0 = 0.005f + index * (1f / 3f);
            float x1 = x0 + (1f / 3f) - 0.01f;
            ElarionUiKit.BuildObsidianButton(_tabHost, label,
                ElarionUiKit.ObsidianButtonStyle.Style1,
                (_vm != null && metric == _vm.Metric) ? ElarionUiKit.ObsidianButtonColor.Yellow
                                                      : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(x0, 0.05f), new Vector2(x1, 0.95f),
                () => SelectMetric(metric));
        }

        private void BuildList(Transform body)
        {
            // ScrollRect + masked viewport + a VerticalLayoutGroup content column
            // (same inline composition as ElarionUiKitDemo — the kit ships no scroll
            // widget yet; kit ask logged in the matrix).
            var scrollHost = ZoneRect(body, "ListScroll", new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.75f));
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(scrollHost, false);
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
            layout.spacing = 3f;
            layout.padding = new RectOffset(8, 8, 6, 6);
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

            _listContent = contentGo.transform;
        }

        // ── Repaint ──────────────────────────────────────────────────────────

        private void SelectMetric(LeaderboardMetric metric)
        {
            _vm?.SelectMetric(metric);   // VM re-fetches + raises Changed -> Render
        }

        // Repaints purely from vm.* — no LeaderboardService reads (strict MVVM, Silo E).
        private void Render()
        {
            if (_modal == null || !_visible || _vm == null) return;

            BuildTabs();        // re-color the active tab from vm.Metric
            RebuildProfile();
            RebuildRows();
            if (_footer != null) _footer.text = _vm.FooterText;
        }

        private void RebuildProfile()
        {
            for (int i = _profileHost.childCount - 1; i >= 0; i--)
                Destroy(_profileHost.GetChild(i).gameObject);
            if (_vm == null || string.IsNullOrEmpty(_vm.ProfileHeroLine)) return;

            MakeText(_profileHost, _vm.ProfileHeroLine, 18, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            MakeText(_profileHost, _vm.ProfileStatsLine,
                14, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        }

        private void RebuildRows()
        {
            if (_listContent == null || _vm == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            foreach (var row in _vm.Rows)
                MakeRow(row.Rank, row.Name, row.Score, row.IsLocal, row.Index);
        }

        private void MakeRow(string rank, string name, string score, bool isLocal, int index)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_listContent, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 34f;
            var bg = rowGo.GetComponent<Image>();
            bg.color = isLocal
                ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.16f)
                : ((index & 1) == 1 ? new Color(1f, 1f, 1f, 0.04f) : Color.clear);

            Color fg = isLocal ? ElarionUi.Gilt : ElarionUi.Parchment;
            var style = isLocal ? FontStyles.Bold : FontStyles.Normal;
            MakeText(rowGo.transform, rank, 14, isLocal ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(0.10f, 1f));
            MakeText(rowGo.transform, name, 14, fg, style,
                TextAlignmentOptions.Left, new Vector2(0.11f, 0f), new Vector2(0.78f, 1f));
            MakeText(rowGo.transform, score, 14, isLocal ? ElarionUi.Gilt : ElarionUi.Aether,
                FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(0.78f, 0f), new Vector2(1f, 1f));
        }

        // ── uGUI helpers ─────────────────────────────────────────────────────

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
    }
}
