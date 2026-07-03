// =============================================================================
// LeaderboardPanel — toggleable leaderboard + player-profile modal (WO-129).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// WO-F conversion (2026-07-03, coverage matrix row #50): UIDocument/UITK card ->
// code-built uGUI on the Obsidian master frame (BuildObsidianModal: FrameCore +
// medallion + the ONE shared Close + scrim), per the HelpMenu reference recipe.
// Opens via Toggle() (the kit dock); reads LeaderboardService.Instance directly.
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
        private LeaderboardMetric _metric = LeaderboardMetric.BestWave;
        private PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Leaderboard", () => SetVisible(false), () => _visible);
        }

        private void OnEnable()
        {
            if (LeaderboardService.Instance != null)
                LeaderboardService.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (LeaderboardService.Instance != null)
                LeaderboardService.Instance.Changed -= Repaint;
        }

        private void OnDestroy()
        {
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
                Repaint();
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
                metric == _metric ? ElarionUiKit.ObsidianButtonColor.Yellow
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
            _metric = metric;
            BuildTabs();   // re-color active tab
            Repaint();
        }

        private void Repaint()
        {
            if (_modal == null || !_visible) return;
            var svc = LeaderboardService.Instance;
            if (svc == null) return;

            RebuildProfile(svc.GetLocalProfile());
            svc.FetchTopAsync(_metric, 20, RebuildList);

            _footer.text = svc.IsLocalStub
                ? $"Source: {svc.SourceLabel}. Scores are local; ranks shown are placeholder rivals until the online ladder is connected."
                : $"Source: {svc.SourceLabel}.";
        }

        private void RebuildProfile(PlayerProfile p)
        {
            for (int i = _profileHost.childCount - 1; i >= 0; i--)
                Destroy(_profileHost.GetChild(i).gameObject);
            if (p == null) return;

            var heroLine = string.IsNullOrEmpty(p.HeroClass) || p.HeroClass == "None"
                ? p.DisplayName
                : $"{p.DisplayName} - {p.HeroClass}";
            string code = string.IsNullOrEmpty(p.InviteCode) ? "" : $"   #{p.InviteCode}";
            MakeText(_profileHost, heroLine + code, 18, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            MakeText(_profileHost,
                $"Best Wave {p.BestWave}    Crystals {p.Crystals}    Magic {p.Magic}    Arena {p.ArenaWins}-{p.ArenaLosses}",
                14, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        }

        private void RebuildList(IReadOnlyList<LeaderboardEntry> rows)
        {
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            if (rows == null || rows.Count == 0)
            {
                // Empty-data roll-up: keep the visible placeholder + self-report so a blank
                // list reads as data-empty, not a silent failure.
                FlowTrace.Warn("Leaderboard",
                    $"RebuildList: fetch for metric '{_metric}' returned {(rows == null ? "null" : "0 rows")} — " +
                    "showing the visible 'No entries yet.' placeholder (data-empty).");
                MakeRow("—", "No entries yet.", "", false, 0);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var e = rows[i];
                MakeRow($"{e.Rank}", e.Name ?? "?", $"{e.Score}", e.IsLocalPlayer, i);
            }
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
