// =============================================================================
// LeaderboardPanel — toggleable leaderboard + player-profile overlay (WO-129).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// Press L to open/close (standard "leaderboard" hotkey). Code-built UIToolkit
// (no UXML — UXML does not render in builds; project note §8). Reads the local
// stub-or-live LeaderboardService.Instance directly (DeNelle.HUD already refs
// DeNelle.Core — no reflection needed).
//
// Layout:
//   • Header — title + close (X).
//   • Profile card — "You", invite code, best wave / crystals / arena W-L.
//   • Metric tabs — Best Wave / Crystals / Arena Wins (re-fetch on tap).
//   • Ranked list — rank, name, score; the local player's row is highlighted.
//   • Footer — an HONEST source badge ("Local (offline)…") when the source is
//     the stub, so we never pretend the (undeployed) backend is live.
//
// When the backend deploys, LeaderboardService.SetSource(...) swaps the data
// source and this panel renders the live board unchanged.
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
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _profileCard;
        private ScrollView _listScroll;
        private Label _footer;
        private VisualElement _tabRail;

        private bool _visible;
        private LeaderboardMetric _metric = LeaderboardMetric.BestWave;
        private readonly Dictionary<LeaderboardMetric, Button> _tabButtons =
            new Dictionary<LeaderboardMetric, Button>();

        // Palette — sourced from the canonical Elarion town-HUD theme so the
        // leaderboard reads as ONE designed UI (dark stone + ornate gold rune frame).
        private static readonly Color PanelBg     = ElarionUi.PanelStoneDark;
        private static readonly Color Accent      = ElarionUi.Aether;
        private static readonly Color Gold        = ElarionUi.Gilt;
        private static readonly Color Body        = ElarionUi.Parchment;
        private static readonly Color Muted       = ElarionUi.ParchmentDim;
        private static readonly Color RowAlt      = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color LocalRowBg  = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.16f);

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Build();
            if (LeaderboardService.Instance != null)
                LeaderboardService.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (LeaderboardService.Instance != null)
                LeaderboardService.Instance.Changed -= Repaint;
        }

        // Mobile-first: the 'L' keyboard open is REMOVED. The panel opens via Toggle()
        // (public), reached by its on-screen / world-interactable path. No Update key poll.

        public void Toggle() => SetVisible(!_visible);

        private void SetVisible(bool on)
        {
            if (on) FlowTrace.Step("Leaderboard", "SetVisible(true) — opening leaderboard panel.");
            _visible = on;
            if (on && _panel == null)
                FlowTrace.Fail("Leaderboard",
                    "SetVisible(true): _panel is NULL — Build never produced a panel (no PanelSettings/root?). Open is a no-op.");
            if (_panel != null)
                _panel.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) Repaint();
        }

        // ── UI construction ──────────────────────────────────────────────────

        private void Build()
        {
            using var _ = FlowTrace.Enter("Leaderboard", "Build");
            // V (WO-465 class): this UIDocument adopts no PanelSettings of its own. With none set,
            // rootVisualElement is null and the old `if (_root == null) return;` silently produced an
            // invisible panel. Fail-loud so a run reports WHY the leaderboard never showed.
            if (_doc != null && _doc.panelSettings == null)
                FlowTrace.Fail("Leaderboard",
                    "Build: UIDocument has NO PanelSettings — rootVisualElement will be null and the " +
                    "leaderboard renders invisible. Wire a PanelSettings on the Leaderboard UIDocument.");
            _root = _doc != null ? _doc.rootVisualElement : null;
            if (_root == null)
            {
                FlowTrace.Fail("Leaderboard",
                    "Build: rootVisualElement is NULL (no UIDocument/PanelSettings) — leaderboard UI cannot be built; panel will be empty.");
                return;
            }
            _root.pickingMode = PickingMode.Ignore; // don't block HUD beneath
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0;  _root.style.bottom = 0;

            _panel = new VisualElement { name = "LeaderboardPanel" };
            _panel.style.position = Position.Absolute;
            // Centre-ish, leaning to screen centre.
            _panel.style.right = 16;
            _panel.style.top = 16;
            _panel.style.width = 380;
            _panel.style.maxHeight = 560;
            _panel.style.backgroundColor = new StyleColor(PanelBg);
            SetRadius(_panel, 12);
            SetBorder(_panel, 2, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
            _panel.style.paddingLeft = 12; _panel.style.paddingRight = 12;
            _panel.style.paddingTop = 10;  _panel.style.paddingBottom = 10;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.display = DisplayStyle.None;
            _panel.pickingMode = PickingMode.Position;
            _root.Add(_panel);

            BuildHeader();
            BuildProfileCard();
            BuildTabs();
            BuildList();
            BuildFooter();

            Repaint();
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            _panel.Add(header);

            var title = new Label("Leaderboard");
            title.style.color = new StyleColor(Gold);
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);

            var close = new Button(() => SetVisible(false)) { text = "X" };
            close.style.fontSize = 12;
            close.style.paddingLeft = 8; close.style.paddingRight = 8;
            close.style.paddingTop = 2;  close.style.paddingBottom = 2;
            header.Add(close);
        }

        private void BuildProfileCard()
        {
            _profileCard = new VisualElement();
            _profileCard.style.flexDirection = FlexDirection.Column;
            _profileCard.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.25f));
            SetRadius(_profileCard, 8);
            _profileCard.style.paddingLeft = 10; _profileCard.style.paddingRight = 10;
            _profileCard.style.paddingTop = 8;   _profileCard.style.paddingBottom = 8;
            _profileCard.style.marginBottom = 8;
            _panel.Add(_profileCard);
        }

        private void BuildTabs()
        {
            _tabRail = new VisualElement();
            _tabRail.style.flexDirection = FlexDirection.Row;
            _tabRail.style.marginBottom = 6;
            _panel.Add(_tabRail);

            AddTab(LeaderboardMetric.BestWave,  "Best Wave");
            AddTab(LeaderboardMetric.Crystals,  "Crystals");
            AddTab(LeaderboardMetric.ArenaWins, "Arena Wins");
        }

        private void AddTab(LeaderboardMetric metric, string label)
        {
            var btn = new Button(() => SelectMetric(metric)) { text = label };
            btn.style.flexGrow = 1;
            btn.style.fontSize = 11;
            btn.style.marginRight = 4;
            btn.style.paddingTop = 4; btn.style.paddingBottom = 4;
            _tabRail.Add(btn);
            _tabButtons[metric] = btn;
        }

        private void BuildList()
        {
            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.style.flexGrow = 1;
            _listScroll.style.minHeight = 200;
            _listScroll.style.maxHeight = 340;
            _listScroll.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.25f));
            SetRadius(_listScroll, 6);
            _listScroll.style.paddingLeft = 6; _listScroll.style.paddingRight = 6;
            _listScroll.style.paddingTop = 4;  _listScroll.style.paddingBottom = 4;
            _listScroll.style.marginBottom = 6;
            _panel.Add(_listScroll);
        }

        private void BuildFooter()
        {
            _footer = new Label();
            _footer.style.color = new StyleColor(Muted);
            _footer.style.fontSize = 10;
            _footer.style.unityFontStyleAndWeight = FontStyle.Italic;
            _footer.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_footer);
        }

        // ── Repaint ──────────────────────────────────────────────────────────

        private void SelectMetric(LeaderboardMetric metric)
        {
            _metric = metric;
            Repaint();
        }

        private void Repaint()
        {
            if (_panel == null || !_visible) return;
            var svc = LeaderboardService.Instance;
            if (svc == null) return;

            RebuildProfile(svc.GetLocalProfile());
            RefreshTabHighlight();

            svc.FetchTopAsync(_metric, 20, RebuildList);

            _footer.text = svc.IsLocalStub
                ? $"Source: {svc.SourceLabel}. Scores are local; ranks shown are placeholder rivals until the online ladder is connected."
                : $"Source: {svc.SourceLabel}.";
        }

        private void RefreshTabHighlight()
        {
            foreach (var kv in _tabButtons)
            {
                bool active = kv.Key == _metric;
                kv.Value.style.backgroundColor = new StyleColor(
                    active ? ElarionUi.Aether : ElarionUi.AetherDim);
                kv.Value.style.color = new StyleColor(active ? Gold : Muted);
                kv.Value.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void RebuildProfile(PlayerProfile p)
        {
            _profileCard.Clear();
            if (p == null) return;

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.justifyContent = Justify.SpaceBetween;
            nameRow.style.alignItems = Align.Center;
            _profileCard.Add(nameRow);

            var heroLine = string.IsNullOrEmpty(p.HeroClass) || p.HeroClass == "None"
                ? p.DisplayName
                : $"{p.DisplayName} - {p.HeroClass}";
            var nameLabel = new Label(heroLine);
            nameLabel.style.color = new StyleColor(Gold);
            nameLabel.style.fontSize = 14;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameRow.Add(nameLabel);

            if (!string.IsNullOrEmpty(p.InviteCode))
            {
                var code = new Label($"#{p.InviteCode}");
                code.style.color = new StyleColor(Muted);
                code.style.fontSize = 11;
                nameRow.Add(code);
            }

            var stats = new Label(
                $"Best Wave {p.BestWave}    Crystals {p.Crystals}    Magic {p.Magic}    " +
                $"Arena {p.ArenaWins}-{p.ArenaLosses}");
            stats.style.color = new StyleColor(Body);
            stats.style.fontSize = 11;
            stats.style.whiteSpace = WhiteSpace.Normal;
            stats.style.marginTop = 4;
            _profileCard.Add(stats);
        }

        private void RebuildList(IReadOnlyList<LeaderboardEntry> rows)
        {
            if (_listScroll == null) return;
            _listScroll.Clear();

            if (rows == null || rows.Count == 0)
            {
                // Empty-data roll-up: the fetch returned no rows. Keep the visible 'No entries yet.'
                // row (R) and self-report so a blank list reads as data-empty, not a silent failure.
                FlowTrace.Warn("Leaderboard",
                    $"RebuildList: fetch for metric '{_metric}' returned {(rows == null ? "null" : "0 rows")} — " +
                    "showing the visible 'No entries yet.' placeholder (data-empty).");
                var hint = new Label("No entries yet.");
                hint.style.color = new StyleColor(Muted);
                hint.style.fontSize = 11;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                hint.style.marginTop = 6;
                _listScroll.Add(hint);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
                _listScroll.Add(BuildRow(rows[i], i));
        }

        private VisualElement BuildRow(LeaderboardEntry e, int index)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4; row.style.paddingBottom = 4;
            row.style.paddingLeft = 6; row.style.paddingRight = 6;
            SetRadius(row, 4);
            if (e.IsLocalPlayer)
                row.style.backgroundColor = new StyleColor(LocalRowBg);
            else if ((index & 1) == 1)
                row.style.backgroundColor = new StyleColor(RowAlt);

            var rank = new Label($"{e.Rank}");
            rank.style.color = new StyleColor(e.IsLocalPlayer ? Gold : Muted);
            rank.style.fontSize = 12;
            rank.style.unityFontStyleAndWeight = FontStyle.Bold;
            rank.style.width = 28;
            row.Add(rank);

            var name = new Label(e.Name ?? "?");
            name.style.color = new StyleColor(e.IsLocalPlayer ? Gold : Body);
            name.style.fontSize = 12;
            name.style.unityFontStyleAndWeight = e.IsLocalPlayer ? FontStyle.Bold : FontStyle.Normal;
            name.style.flexGrow = 1;
            row.Add(name);

            var score = new Label($"{e.Score}");
            score.style.color = new StyleColor(e.IsLocalPlayer ? Gold : Accent);
            score.style.fontSize = 12;
            score.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(score);

            return row;
        }

        // ── Style helpers ────────────────────────────────────────────────────

        private static void SetRadius(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r; ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r; ve.style.borderBottomRightRadius = r;
        }

        private static void SetBorder(VisualElement ve, float w, Color c)
        {
            ve.style.borderLeftWidth = w; ve.style.borderRightWidth = w;
            ve.style.borderTopWidth = w; ve.style.borderBottomWidth = w;
            ve.style.borderLeftColor = new StyleColor(c); ve.style.borderRightColor = new StyleColor(c);
            ve.style.borderTopColor = new StyleColor(c); ve.style.borderBottomColor = new StyleColor(c);
        }
    }
}
