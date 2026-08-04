// =============================================================================
// RumorBoardPanel (WO-304 · WO-810 layout rework) — Brom's rumor board: the
// BROWSE / ACCEPT surface for the realm's story + vendor questlines.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// WO-810 (owner-signed wireframe, 2026-07-30): a clear MASTER-DETAIL board.
//   • Filter chips: ONE horizontally-scrollable row of full-label pill chips —
//     never five squeezed plates ("Endgame" used to clip). Selected chip =
//     gilt fill + underline bar + a leading "*" marker (shape, never colour
//     alone — colourblind law; the spec's diamond glyph is non-ASCII and the
//     bundled font tofus non-Latin symbols, so the marker is ASCII "*").
//   • Left ~42%: the LIST. In Progress collapses to one quiet dim line when
//     empty (no section slab). Rows are two-line CARDS (title + state pip /
//     one-line hook) with NO buttons in the row — the whole card selects.
//     Selected card = gilt left border + warmer fill. Scrolls (WO-795).
//   • Right ~58%: the DETAIL, always bound to the selection (first available
//     auto-selected on open/tab change, so it is never blank). Tag row · gilt
//     title · full tale · rewards · the PRIMARY CTA pinned at the bottom with
//     a full word: Accept (available) / Track (active) / Pinned (tracked).
//     The Accept CTA living HERE (not in each row) is the load-bearing move —
//     it un-crushes the list and kills the "ACC…" truncation for good.
//   • Portrait (H > W): the same rules, panes stacked (list top, detail below).
//
// READ-ONLY consumer of RumorBoardVM (strict MVVM): the View renders VM
// projections and routes taps to Accept/Track/SetTab; it never touches
// QuestService / QuestCatalog / DailyQuestService.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Hero
{
    public sealed class RumorBoardPanel : MonoBehaviour
    {
        private GameObject _ui;
        private Transform _panelRoot;
        private GameObject _contentRoot;
        private TMPro.TextMeshProUGUI _statusText;

        // Detail pane widgets (rebuilt content, persistent hosts). WO-810 follow-up
        // (2026-08-02): the tag + reward TEXT lines became CHIP ROWS (bordered tag
        // chips / reward chips — the signed spec's missing sections).
        private RectTransform _detailTagRow;
        private TMPro.TextMeshProUGUI _detailTitle;
        private TMPro.TextMeshProUGUI _detailBody;
        private RectTransform _detailRewardRow;
        private GameObject _detailCtaGo;
        private RectTransform _detailPane;

        private RumorBoardVM _vm;
        private GameObject _tabStrip;
        private PanelHandle _handle;

        // WO-810 selection model: the detail pane always binds this. Kind disambiguates the
        // CTA (Accept vs Track vs none) without re-deriving from the VM lists on every tap.
        private enum RowKind { None, Available, Active, Daily }
        private string _selectedId;
        private RowKind _selectedKind = RowKind.None;

        // ── Chip metrics (iteration 3 — the store-listing capture RCA) ───────────
        // MEASURED capture 2026-08-03 (RumorBoard_1920x1080.png): the detail pane is
        // x 968..1711 (744 ref px) and the reward row ran 0.05-0.95 of it = 669 px. The four
        // worst-case chips asked for text.Length*18+28 = 1090 px — a per-character budget that
        // over-estimates a FontMicro glyph by ~37% (measured: ~13.1 px/char) — so the
        // HorizontalLayoutGroup shrank every chip to 60.6% of its ask (measured chip edges at
        // x 1152 / 1254 / 1366 / 1674 match that ratio exactly) while the labels, authored
        // NoWrap + Overflow, kept painting at FULL width straight through their neighbours:
        // "Crystals 220Food 90Magic 45em: relic_drowned_ledger". Two changes kill it:
        //   1. a chip is sized from its label's MEASURED preferred width, never a guess;
        //   2. a chip label is FITTED (bounded auto-size -> ellipsis), so even if a row does
        //      run short the text can never paint outside its own chip again.
        private const float ChipPadPx     = 16f;   // 8 per side around the measured label
        private const float ChipSpacingPx = 6f;    // reward row gap (tag row keeps 8)
        private const float ChipHeightPx  = 48f;   // inside the 56px band (44px label rect)
        // Fitted floor for the REWARD row only: four chips at FontMicro(32) sit ~98% full in a
        // 714px row, so a modest relaxation absorbs any font-metric drift without ever
        // ellipsizing a reward's NAME. Well above the kit's FontHardFloor (20).
        private const float ChipMinFontPx = 26f;

        // ── Public API ──────────────────────────────────────────────────────────

        public void Open()
        {
            Close();

            if (_handle == null)
                _handle = PanelManager.Register("Rumor Board", Close, () => _ui != null);

            _vm = RumorBoardVM.CreateDefault(Close);
            _vm.Changed += Repaint;

            var modal = ElarionUiKit.BuildObsidianModal("RumorBoardPanelUI", "Brom's Rumor Board",
                new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f), Close, sortingOrder: 1000,
                frameName: RpgUiCatalog.FrameQuest, medallionIcon: "quest");
            _ui = modal.canvas;
            var panel = modal.chrome.content;
            var bodyHost = (modal.chrome.layout != null && modal.chrome.layout.body != null)
                ? modal.chrome.layout.body : (RectTransform)panel.transform;
            _panelRoot = bodyHost;

            // WO-810 follow-up (2026-08-02, pixel-verified RCA): the panes now PARENT to the
            // kit's MEASURED FrameQuest drop-zones — chrome.layout.bodyLeft (the dark list
            // well) and chrome.layout.bodyRight (the parchment detail well) — at full 0..1
            // anchors, instead of hand-tuned panel fractions. That seats the list, the
            // detail plate AND the Accept CTA inside the frame's designed wells, and the
            // bodyRight floor already reserves the shared Close band (Accept lifts clear of
            // Close). Fallbacks keep panel fractions for a frameless procedural panel
            // (detail floor RAISED to 0.30 so the CTA still clears Close) and for portrait
            // (stacked panes — the zones are a landscape split).
            bool portrait = Screen.height > Screen.width;
            var zoneLeft  = modal.chrome.layout != null ? modal.chrome.layout.bodyLeft  : null;
            var zoneRight = modal.chrome.layout != null ? modal.chrome.layout.bodyRight : null;
            Vector2 listMin, listMax, detailMin, detailMax;
            Transform listHost, detailHost;
            // Pixel inset the list top must leave for the FIXED-height tab strip (E3) when
            // the list fills a zone: strip height (MinTouchPx + 24) + an 8px breathing gap.
            // Bottom inset reserves the status line's 40px band + gap (iteration 2 defect 4).
            float listTopInsetPx = 0f;
            float listBottomInsetPx = 0f;
            if (portrait)
            {
                listMin = new Vector2(0.03f, 0.48f); listMax = new Vector2(0.97f, 0.855f);
                detailMin = new Vector2(0.05f, 0.05f); detailMax = new Vector2(0.95f, 0.46f);
                listHost = bodyHost;
                detailHost = panel.transform;
            }
            else
            {
                if (zoneLeft != null)
                {
                    // List fills the dark left well; its top hangs from the SAME y 0.95 line
                    // the strip hangs from, inset by the strip's fixed height (px, not
                    // fraction — the strip no longer scales with the body).
                    listHost = zoneLeft;
                    listMin = Vector2.zero;
                    listMax = new Vector2(1f, 0.95f);
                    listTopInsetPx = ElarionUiKit.MinTouchPx + 32f;
                    listBottomInsetPx = 52f;   // the status line's 44px band + gap at the column bottom
                }
                else
                { listHost = bodyHost; listMin = new Vector2(0.03f, 0.07f); listMax = new Vector2(0.97f, 0.70f); }

                if (zoneRight != null)
                { detailHost = zoneRight; detailMin = Vector2.zero; detailMax = Vector2.one; }
                else
                { detailHost = panel.transform; detailMin = new Vector2(0.51f, 0.30f); detailMax = new Vector2(0.955f, 0.78f); }
            }

            BuildTabStrip(bodyHost);

            // ── LEFT: the scrollable card list (WO-795 — rows never stack/overlap) ──
            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(listHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = listMin;
            vpr.anchorMax = listMax;
            vpr.offsetMin = new Vector2(0f, listBottomInsetPx);
            vpr.offsetMax = new Vector2(0f, -listTopInsetPx);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            _contentRoot = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _contentRoot.transform.SetParent(viewportGo.transform, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(0.5f, 1f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;
            var vlg = _contentRoot.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true; vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            // Bottom pad = one card so the last row scrolls fully clear of the mask.
            vlg.padding = new RectOffset(6, 6, 6, 104);
            var csf = _contentRoot.GetComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content  = cr;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            // ── RIGHT: the detail pane (obsidian-dark; always bound to the selection) ──
            var detailGo = new GameObject("DetailPane", typeof(Image));
            detailGo.transform.SetParent(detailHost, false);
            _detailPane = detailGo.GetComponent<RectTransform>();
            _detailPane.anchorMin = detailMin;
            _detailPane.anchorMax = detailMax;
            _detailPane.offsetMin = Vector2.zero; _detailPane.offsetMax = Vector2.zero;
            var dImg = detailGo.GetComponent<Image>();
            // Solid obsidian: the plate must COVER the frame's tan parchment page beneath it
            // (signed spec: dark pane, not the tan slab). WO-810 follow-up: alpha 0.92 -> 1f —
            // the LINEAR-SPACE blend of 0.92-alpha dark over tan read KHAKI (pixel-proven).
            dImg.color = new Color(0.05f, 0.045f, 0.04f, 1f);
            dImg.raycastTarget = false;

            // Iteration 2 (capture RumorBoard_daily defect 3): the kit RAISES the bodyRight
            // zone floor above the shared Close band (close-band reservation), leaving the
            // frame art's baked tan parchment page visible BETWEEN the zone bottom and the
            // page bottom. Cover that remainder with an alpha-1 obsidian strip hung from the
            // zone's bottom edge. Children of Zone_BodyRight render UNDER the later-sibling
            // Close (kit builds close after the zones), so this can never occlude Close.
            if (!portrait && zoneRight != null)
            {
                var under = new GameObject("DetailUnderPlate", typeof(Image));
                under.transform.SetParent(zoneRight, false);
                under.transform.SetAsFirstSibling();
                var urt = under.GetComponent<RectTransform>();
                urt.anchorMin = new Vector2(0f, 0f);
                urt.anchorMax = new Vector2(1f, 0f);
                urt.pivot = new Vector2(0.5f, 1f);
                urt.offsetMin = Vector2.zero; urt.offsetMax = Vector2.zero;
                urt.sizeDelta = new Vector2(0f, 165f);   // the reserved-band remainder (~155-160 ref px)
                urt.anchoredPosition = Vector2.zero;      // top edge = the zone's bottom edge
                var underImg = under.GetComponent<Image>();
                underImg.color = new Color(0.05f, 0.045f, 0.04f, 1f);
                underImg.raycastTarget = false;
            }

            // Iteration 2 (capture RumorBoard_daily defect 2): the detail stack uses
            // FIXED-PIXEL bands hung from the pane's top/bottom — fraction bands scaled with
            // the zone height, under-heighted their text, and TMP CULLED it whole (the empty
            // gold chip outlines). Bands: top-down 8 + 56 (tag chips) + 8 + 68 (title) + 8;
            // bottom-up 8 + 132 (CTA) + 8 + 56 (reward chips) + 8; body fills between.
            _detailTagRow = MakeChipRow(detailGo.transform, "DetailTagRow",
                topPx: 8f, heightPx: 56f);

            _detailTitle = MakeDetailLabel(detailGo.transform, "DetailTitle",
                new Vector2(0.05f, 1f), new Vector2(0.95f, 1f),
                ElarionUi.Gilt, ElarionUi.FontHead, bold: true);
            var titleRt = _detailTitle.rectTransform;
            titleRt.offsetMin = new Vector2(0f, -140f);
            titleRt.offsetMax = new Vector2(0f, -72f);

            _detailBody = MakeDetailLabel(detailGo.transform, "DetailBody",
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
                ElarionUi.Parchment, ElarionUi.FontLabel, bold: false);
            _detailBody.alignment = TMPro.TextAlignmentOptions.TopLeft;
            _detailBody.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var bodyRt = _detailBody.rectTransform;
            bodyRt.offsetMin = new Vector2(0f, 212f);
            bodyRt.offsetMax = new Vector2(0f, -148f);

            // Reward chip row (crystals / food / magic / items as chips — signed spec),
            // bottom-hung just above the fixed-height CTA band. Iteration 3 (store-listing
            // capture): this row runs 0.02-0.98 of the pane, not 0.05-0.95 — at 1920x1080 the
            // detail pane is only ~744 ref px wide and the worst-case rumor pays FOUR chips,
            // so the row needs every pixel it can honestly take (669 -> 714 ref px).
            _detailRewardRow = MakeChipRow(detailGo.transform, "DetailRewardRow",
                topPx: 0f, heightPx: 56f, fromBottomPx: 148f,
                sideFrac: 0.02f, spacingPx: ChipSpacingPx);

            // Status line — iteration 2 (capture RumorBoard_daily defect 4): the footer-zone
            // seat rendered the line full-width across the Close band (the kit re-seats the
            // footer right above Close). It now sits at the BOTTOM OF THE LIST COLUMN
            // (bodyLeft zone, fixed 44px band, FontMicro; the viewport is inset above it),
            // so it can never touch Close or the parchment band. Fraction fallback for the
            // zoneless/portrait paths keeps the old in-body seat.
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            var sRect = statusGo.GetComponent<RectTransform>();
            if (!portrait && zoneLeft != null)
            {
                statusGo.transform.SetParent(zoneLeft, false);
                sRect.anchorMin = new Vector2(0f, 0f);
                sRect.anchorMax = new Vector2(1f, 0f);
                sRect.pivot = new Vector2(0.5f, 0f);
                sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
                sRect.sizeDelta = new Vector2(0f, 44f);   // one FontMicro line + margin (never culled)
                sRect.anchoredPosition = new Vector2(0f, 4f);
            }
            else
            {
                statusGo.transform.SetParent(bodyHost, false);
                sRect.anchorMin = new Vector2(0.03f, 0.005f);
                sRect.anchorMax = new Vector2(0.97f, 0.06f);
                sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
            }
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_statusText);
            _statusText.fontSize = ElarionUi.FontMicro;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            _statusText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            SetStatus(_vm.Status);

            Repaint();

            if (!PanelManager.NotifyOpened(_handle)) return;

            Debug.Log("[RumorBoardPanel] Opened (WO-810 master-detail).");
        }

        public void Close()
        {
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }

            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _statusText = null;
            _detailTagRow = null;
            _detailTitle = null;
            _detailBody = null;
            _detailRewardRow = null;
            _detailCtaGo = null;
            _detailPane = null;
            _tabStrip = null;
            _selectedId = null;
            _selectedKind = RowKind.None;
            PanelManager.NotifyClosed(_handle);
        }

        private void OnDestroy()
        {
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
        }

        // ── Paint ───────────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_contentRoot == null || _vm == null) return;
            ClearContent();

            if (_vm.IsDailyTab)
            {
                RepaintDaily();
            }
            else
            {
                // WO-810: empty In Progress = ONE quiet dim line, no section slab; populated =
                // a compact section of cards above the Rumors list.
                if (_vm.ActiveQuests.Count == 0)
                {
                    CreateFlavorRow(_contentRoot.transform, "In Progress - nothing underway.");
                }
                else
                {
                    CreateSectionLabel(_contentRoot.transform, "- In Progress -");
                    foreach (var item in _vm.ActiveQuests)
                        CreateCard(_contentRoot.transform, item.Id, item.Name,
                            _vm.ObjectiveFor(item.Id),
                            item.Equipped ? "[Tracked]" : "", RowKind.Active,
                            item.Equipped);
                }

                CreateSectionLabel(_contentRoot.transform, "- Rumors & Requests -");
                if (_vm.AvailableQuests.Count == 0)
                    CreateFlavorRow(_contentRoot.transform, "You've answered every call. For now.");
                foreach (var item in _vm.AvailableQuests)
                    CreateCard(_contentRoot.transform, item.Id, item.Name,
                        _vm.HookFor(item.Id), "[New]", RowKind.Available, false);
            }

            // WO-810 auto-select: the detail is never blank while anything is listable.
            EnsureSelection();
            RenderDetail();
        }

        // Keep (or establish) a valid selection: prefer the current one if it still exists
        // under this tab; else first available, first active, first daily; else none.
        private void EnsureSelection()
        {
            if (_vm == null) { _selectedId = null; _selectedKind = RowKind.None; return; }

            if (_vm.IsDailyTab)
            {
                var daily = _vm.DailyQuests;
                if (_selectedKind == RowKind.Daily && FindDaily(_selectedId).HasValue) return;
                if (daily != null && daily.Count > 0) { _selectedId = daily[0].Id; _selectedKind = RowKind.Daily; }
                else { _selectedId = null; _selectedKind = RowKind.None; }
                return;
            }

            if (_selectedId != null)
            {
                foreach (var i in _vm.AvailableQuests) if (i.Id == _selectedId) { _selectedKind = RowKind.Available; return; }
                foreach (var i in _vm.ActiveQuests) if (i.Id == _selectedId) { _selectedKind = RowKind.Active; return; }
            }
            if (_vm.AvailableQuests.Count > 0) { _selectedId = _vm.AvailableQuests[0].Id; _selectedKind = RowKind.Available; return; }
            if (_vm.ActiveQuests.Count > 0) { _selectedId = _vm.ActiveQuests[0].Id; _selectedKind = RowKind.Active; return; }
            _selectedId = null;
            _selectedKind = RowKind.None;
        }

        private RumorBoardVM.DailyRow? FindDaily(string id)
        {
            var daily = _vm != null ? _vm.DailyQuests : null;
            if (daily == null || id == null) return null;
            foreach (var d in daily) if (d.Id == id) return d;
            return null;
        }

        // ── Filter chips (WO-810 — scrollable pills, full labels, never clipped) ──

        private void BuildTabStrip(Transform parent)
        {
            if (_tabStrip != null) { SafeDestroy(_tabStrip); _tabStrip = null; }

            _tabStrip = new GameObject("TabStrip", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            _tabStrip.transform.SetParent(parent, false);
            var sr = _tabStrip.GetComponent<RectTransform>();
            // WO-810 follow-up E3: FIXED-height strip hung from the body top (pivot-top at
            // y 0.95) replacing the 0.87-0.95 fraction band — chips are >= the touch floor
            // tall on every screen instead of scaling with the body height.
            sr.anchorMin = new Vector2(0.03f, 0.95f);
            sr.anchorMax = new Vector2(0.97f, 0.95f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
            sr.sizeDelta = new Vector2(0f, ElarionUiKit.MinTouchPx + 24f);
            _tabStrip.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            // Horizontal chip content: each chip a fixed-width host (>= the touch floor);
            // when the row is wider than the strip (portrait) it scrolls sideways.
            var content = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(_tabStrip.transform, false);
            var ccr = content.GetComponent<RectTransform>();
            ccr.anchorMin = new Vector2(0f, 0f);
            ccr.anchorMax = new Vector2(0f, 1f);
            ccr.pivot = new Vector2(0f, 0.5f);
            ccr.offsetMin = Vector2.zero;
            ccr.offsetMax = Vector2.zero;
            var hlg = content.GetComponent<HorizontalLayoutGroup>();
            // SCREENSHOT-CORRECTED (2026-07-30): childControlWidth must be TRUE for the chip
            // hosts' LayoutElement.preferredWidth to apply — with it false the HLG read the
            // hosts' zero sizeDelta and the chips piled onto each other ("Endg..." clip).
            hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
            hlg.spacing = 10f;
            var csf = content.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var strip = _tabStrip.GetComponent<ScrollRect>();
            strip.viewport = sr;
            strip.content = ccr;
            strip.horizontal = true;
            strip.vertical = false;
            strip.movementType = ScrollRect.MovementType.Clamped;
            strip.scrollSensitivity = 25f;

            string activeTab = _vm != null ? _vm.ActiveTab : "all";
            for (int i = 0; i < RumorBoardVM.TabKeys.Length; i++)
            {
                string key = RumorBoardVM.TabKeys[i];
                bool isActive = key == activeTab;
                string tabKey = key;

                var host = new GameObject("Chip_" + key, typeof(RectTransform), typeof(LayoutElement));
                host.transform.SetParent(content.transform, false);
                host.GetComponent<LayoutElement>().preferredWidth = 220f;   // >= MinTouchPx with margin

                // Selected = gilt (Yellow) + a leading "*" marker + an underline bar —
                // shape + luminance carry the state, never colour alone.
                string label = (isActive ? "* " : "") + RumorBoardVM.TabLabels[i];
                ElarionUiKit.BuildObsidianButton(host.transform, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    isActive ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    Vector2.zero, Vector2.one,
                    () => SetTab(tabKey));

                if (isActive)
                {
                    var bar = new GameObject("Underline", typeof(Image));
                    bar.transform.SetParent(host.transform, false);
                    var brt = bar.GetComponent<RectTransform>();
                    brt.anchorMin = new Vector2(0.08f, 0f);
                    brt.anchorMax = new Vector2(0.92f, 0.06f);
                    brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                    var bi = bar.GetComponent<Image>();
                    bi.color = ElarionUi.Gilt;
                    bi.raycastTarget = false;
                }
            }
        }

        private void SetTab(string tab)
        {
            if (_vm == null || _vm.ActiveTab == tab) return;
            _selectedId = null;                    // WO-810: the new tab auto-selects its first row
            _selectedKind = RowKind.None;
            _vm.SetTab(tab);                       // raises Changed -> Repaint -> EnsureSelection
            if (_ui != null) BuildTabStrip(_panelRoot ?? _ui.transform);
        }

        // ── Daily tab ───────────────────────────────────────────────────────────

        private void RepaintDaily()
        {
            CreateSectionLabel(_contentRoot.transform, "- Daily Quests -");
            var daily = _vm.DailyQuests;
            if (daily == null || daily.Count == 0)
            {
                CreateFlavorRow(_contentRoot.transform, "No daily quests rolled yet. Check back later.");
                return;
            }
            foreach (var q in daily)
            {
                string hook = q.Completed ? $"Complete  ({q.Target}/{q.Target})" : $"{q.Progress}/{q.Target}";
                CreateCard(_contentRoot.transform, q.Id, q.Title, hook,
                    q.Completed ? "[Done]" : "", RowKind.Daily, q.Completed);
            }
        }

        // ── Cards (WO-810 — two lines, no buttons, whole card selects) ───────────

        private void CreateSectionLabel(Transform parent, string txt)
        {
            var go = new GameObject("Section", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 56f;   // WO-810 follow-up E5
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = ElarionUi.FontLabel;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.BottomLeft;
        }

        private void CreateFlavorRow(Transform parent, string txt)
        {
            var go = new GameObject("Flavor", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 48f;   // WO-810 follow-up E5
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = ElarionUi.FontMicro;
            t.fontStyle = TMPro.FontStyles.Italic;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Left;
        }

        private void CreateCard(Transform parent, string id, string title, string hook,
                                string pip, RowKind kind, bool pipGilt)
        {
            bool selected = id == _selectedId;

            var row = new GameObject("Card_" + id, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            // WO-810 follow-up E5: cards are select-targets — floor them at the kit touch floor.
            row.GetComponent<LayoutElement>().preferredHeight = ElarionUiKit.MinTouchPx;
            var img = row.GetComponent<Image>();
            // Selected = warmer fill; unselected = the dark stone plate.
            img.color = selected
                ? new Color(ElarionUi.PanelStone.r * 1.35f, ElarionUi.PanelStone.g * 1.30f,
                            ElarionUi.PanelStone.b * 1.10f, 0.95f)
                : new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g,
                            ElarionUi.PanelStoneDark.b, 0.85f);

            // Selected card carries a gilt left border (shape marker, not colour alone).
            if (selected)
            {
                var edge = new GameObject("SelEdge", typeof(Image));
                edge.transform.SetParent(row.transform, false);
                var ert = edge.GetComponent<RectTransform>();
                ert.anchorMin = new Vector2(0f, 0f);
                ert.anchorMax = new Vector2(0f, 1f);
                ert.pivot = new Vector2(0f, 0.5f);
                ert.offsetMin = Vector2.zero;
                ert.offsetMax = new Vector2(6f, 0f);
                var ei = edge.GetComponent<Image>();
                ei.color = ElarionUi.Gilt;
                ei.raycastTarget = false;
            }

            // Line 1: title (bold parchment) + right-aligned state pip.
            // Iteration 2 (capture RumorBoard_daily defect 1): FIXED-PIXEL bands — the old
            // fraction band (0.52-0.94 of the 112px card = 47px) under-heighted the FontBody
            // (50) line and TMP CULLED it whole, leaving titleless rows in the edit-mode
            // capture AND play mode alike. 62px holds one FontBody line; the hook band below
            // holds one FontMicro line. Same fixed-RowPx pattern DailyQuestHud proved out.
            bool hasPip = !string.IsNullOrEmpty(pip);
            var titleGo = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
            titleGo.transform.SetParent(row.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.04f, 1f);
            trt.anchorMax = new Vector2(hasPip ? 0.74f : 0.96f, 1f);
            trt.offsetMin = new Vector2(0f, -66f);
            trt.offsetMax = new Vector2(0f, -4f);
            var tt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(tt);
            tt.text = title;
            tt.fontSize = ElarionUi.FontBody;
            tt.fontStyle = TMPro.FontStyles.Bold;
            tt.color = ElarionUi.Parchment;
            tt.alignment = TMPro.TextAlignmentOptions.Left;
            tt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tt.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            tt.raycastTarget = false;

            if (hasPip)
            {
                var pipGo = new GameObject("Pip", typeof(TMPro.TextMeshProUGUI));
                pipGo.transform.SetParent(row.transform, false);
                var prt = pipGo.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.74f, 1f);
                prt.anchorMax = new Vector2(0.96f, 1f);
                prt.offsetMin = new Vector2(0f, -56f);
                prt.offsetMax = new Vector2(0f, -8f);
                var pt = pipGo.GetComponent<TMPro.TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(pt);
                pt.text = pip;
                pt.fontSize = ElarionUi.FontMicro;
                pt.color = pipGilt ? ElarionUi.Gilt : ElarionUi.ParchmentDim;
                pt.alignment = TMPro.TextAlignmentOptions.Right;
                pt.raycastTarget = false;
            }

            // Line 2: one-line hook, dim, ellipsized (never wraps into a third line).
            var hookGo = new GameObject("Hook", typeof(TMPro.TextMeshProUGUI));
            hookGo.transform.SetParent(row.transform, false);
            var hrt = hookGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.04f, 0f);
            hrt.anchorMax = new Vector2(0.96f, 0f);
            hrt.offsetMin = new Vector2(0f, 4f);
            hrt.offsetMax = new Vector2(0f, 44f);
            var ht = hookGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(ht);
            ht.text = hook;
            ht.fontSize = ElarionUi.FontMicro;
            ht.color = ElarionUi.ParchmentDim;
            ht.alignment = TMPro.TextAlignmentOptions.Left;
            ht.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            ht.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            ht.raycastTarget = false;

            // The whole card is the select target — no buttons live in rows (WO-810).
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            string rid = id;
            RowKind rkind = kind;
            btn.onClick.AddListener(() => SelectRow(rid, rkind));
        }

        private void SelectRow(string id, RowKind kind)
        {
            if (_selectedId == id && _selectedKind == kind) return;
            _selectedId = id;
            _selectedKind = kind;
            Repaint();   // repaints the card highlight + rebinds the detail
        }

        // ── Detail pane (WO-810 — always bound to the selection) ─────────────────

        private void RenderDetail()
        {
            if (_detailTitle == null || _detailBody == null || _vm == null) return;

            // Chip rows + CTA are rebuilt per selection (their content/state change per row).
            ClearChildren(_detailTagRow);
            ClearChildren(_detailRewardRow);
            if (_detailCtaGo != null) { SafeDestroy(_detailCtaGo); _detailCtaGo = null; }

            if (_selectedId == null || _selectedKind == RowKind.None)
            {
                _detailTitle.text = "The Board Awaits";
                ElarionUiKit.FitSingleLine(_detailTitle);
                _detailBody.text = "Select a rumor to read the full tale.\n\n" +
                    "Whispers gather here from every corner of Elarion - pick one up, and Brom will " +
                    "point you where the trouble started.";
                _detailBody.fontSize = ElarionUi.FontLabel;
                ElarionUiKit.FitBlock(_detailBody, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);
                return;
            }

            if (_selectedKind == RowKind.Daily)
            {
                var d = FindDaily(_selectedId);
                MakeChip(_detailTagRow, "Daily Quest", ElarionUi.ParchmentDim);
                if (d.HasValue)
                    MakeChip(_detailTagRow, d.Value.Completed ? "Complete" : "In Progress",
                        d.Value.Completed ? ElarionUi.Gilt : ElarionUi.ParchmentDim);
                _detailTitle.text = d.HasValue ? d.Value.Title : _selectedId;
                ElarionUiKit.FitSingleLine(_detailTitle);
                string prog = d.HasValue
                    ? "Objectives:\n- " + (d.Value.Completed
                        ? "Complete (" + d.Value.Target + "/" + d.Value.Target + ")"
                        : d.Value.Progress + "/" + d.Value.Target)
                    : "";
                _detailBody.text = prog +
                    "\n\nDaily quests reset with the day. Finish them for a steady trickle of rewards.";
                _detailBody.fontSize = ElarionUi.FontLabel;
                ElarionUiKit.FitBlock(_detailBody, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);
                return;   // dailies have no CTA — they advance through play
            }

            bool active = _selectedKind == RowKind.Active;
            string title = TitleOf(_selectedId);
            bool tracked = IsTracked(_selectedId);

            // Tag chips (signed spec): quest type + state — bordered shape + the word, never
            // colour alone (colourblind law).
            MakeChip(_detailTagRow, _vm.TypeFor(_selectedId) + " Quest", ElarionUi.ParchmentDim);
            MakeChip(_detailTagRow,
                active ? (tracked ? "Tracked" : "In Progress") : "New",
                active && tracked ? ElarionUi.Gilt : ElarionUi.ParchmentDim);

            _detailTitle.text = title;
            ElarionUiKit.FitSingleLine(_detailTitle);

            // Body: the tale + an ASCII-bullet objectives section (signed spec).
            _detailBody.text = active
                ? "Objectives:\n- " + _vm.ObjectiveFor(_selectedId) +
                  "\n\nThis rumor is underway. Track pins it to your HUD so you always know the next step."
                : _vm.HookFor(_selectedId) +
                  "\n\nObjectives:\n- " + _vm.HookFor(_selectedId) +
                  "\n\nAccept this rumor to add it to your ledger. Brom will point you where the trouble started.";
            _detailBody.fontSize = ElarionUi.FontLabel;
            ElarionUiKit.FitBlock(_detailBody, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);

            // Reward chips (signed spec): one gilt-bordered chip per authored reward part.
            // The VM hands over READY-TO-DRAW parts — resolved item display names included —
            // so the View no longer re-parses a joined string to find the chip boundaries.
            var rewardParts = _vm.RewardPartsFor(_selectedId);
            if (rewardParts != null)
                foreach (var part in rewardParts)
                    if (!string.IsNullOrEmpty(part))
                        MakeChip(_detailRewardRow, part, ElarionUi.Gilt, ChipMinFontPx);

            // CTA row — pinned at the pane's bottom, full words, >= the touch floor. The
            // pane now lives inside the bodyRight zone, whose floor already clears Close.
            _detailCtaGo = new GameObject("DetailCta", typeof(RectTransform));
            _detailCtaGo.transform.SetParent(_detailPane, false);
            var crt = _detailCtaGo.GetComponent<RectTransform>();
            // Fixed-pixel CTA band (iteration 2): CanonCtaHeight bottom-hung — matches the
            // reward/tag bands and never scales below the touch floor with the zone height.
            crt.anchorMin = new Vector2(0.05f, 0f);
            crt.anchorMax = new Vector2(0.95f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, ElarionUiKit.CanonCtaHeight);
            crt.anchoredPosition = new Vector2(0f, 8f);

            string id = _selectedId;
            if (!active)
            {
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform, "Accept",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0f, 0f), new Vector2(0.52f, 1f), () => OnAccept(id));
                // Signed spec: Track rides SECONDARY beside Accept — accept-and-pin in one
                // visit (Track on an available rumor accepts it first, then pins + closes).
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform, "Track",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.56f, 0f), new Vector2(1f, 1f), () => { OnAccept(id); OnTrack(id); });
            }
            else
            {
                // Tracked = affirmative Green + the word carries the state (never colour-only).
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform,
                    tracked ? "Pinned" : "Track",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    tracked ? ElarionUiKit.ObsidianButtonColor.Green
                            : ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0f, 0f), new Vector2(0.52f, 1f), () => OnTrack(id));
            }
        }

        private string TitleOf(string id)
        {
            foreach (var i in _vm.AvailableQuests) if (i.Id == id) return i.Name;
            foreach (var i in _vm.ActiveQuests) if (i.Id == id) return i.Name;
            return id;
        }

        private bool IsTracked(string id)
        {
            foreach (var i in _vm.ActiveQuests) if (i.Id == id) return i.Equipped;
            return false;
        }

        private TMPro.TextMeshProUGUI MakeDetailLabel(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, Color color, float size, bool bold)
        {
            var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = size;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            t.color = color;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.raycastTarget = false;
            return t;
        }

        // ── Chip rows (WO-810 follow-up — the signed spec's tag/reward chips) ────

        /// <summary>A left-aligned horizontal chip host as a FIXED-PIXEL band (iteration 2:
        /// fraction bands scaled with the zone and under-heighted the chips — TMP culled
        /// their labels whole). Top-hung at <paramref name="topPx"/> below the pane top, or
        /// bottom-hung at <paramref name="fromBottomPx"/> above the pane bottom when &gt;= 0.
        /// Chips are rebuilt into it per selection (RenderDetail).</summary>
        private static RectTransform MakeChipRow(Transform parent, string name,
            float topPx, float heightPx, float fromBottomPx = -1f,
            float sideFrac = 0.05f, float spacingPx = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            bool fromBottom = fromBottomPx >= 0f;
            rt.anchorMin = new Vector2(sideFrac, fromBottom ? 0f : 1f);
            rt.anchorMax = new Vector2(1f - sideFrac, fromBottom ? 0f : 1f);
            rt.pivot = new Vector2(0.5f, fromBottom ? 0f : 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, heightPx);
            rt.anchoredPosition = new Vector2(0f, fromBottom ? fromBottomPx : -topPx);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = spacingPx;
            return rt;
        }

        /// <summary>One bordered chip: dim-gilt border frame, obsidian fill, micro label.
        /// Shape (the border) + the word carry the meaning — never colour alone.
        /// The chip is sized from its label's MEASURED width and the label is FITTED to the
        /// chip, so a crowded row shrinks text INSIDE the borders instead of painting it
        /// across the next chip (see the ChipPadPx block for the captured proof).</summary>
        private static void MakeChip(RectTransform row, string text, Color ink, float minFontPx = 0f)
        {
            if (row == null || string.IsNullOrEmpty(text)) return;

            var chip = new GameObject("Chip", typeof(Image), typeof(LayoutElement));
            chip.transform.SetParent(row, false);
            var borderImg = chip.GetComponent<Image>();
            borderImg.color = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.55f);
            borderImg.raycastTarget = false;
            var le = chip.GetComponent<LayoutElement>();
            // Height 48 inside the 56px row: the fill inset leaves a 44px label rect, which
            // seats the FontMicro (~40px) line box AND the fitted floor's (~33px) — never
            // culled vertically, which is why Ellipsis is safe on this label (below).
            le.preferredHeight = ChipHeightPx;
            le.flexibleWidth = 0f;   // a chip never absorbs slack — it stays word-sized

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(chip.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.05f, 0.045f, 0.04f, 1f);
            fillImg.raycastTarget = false;

            var lblGo = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            lblGo.transform.SetParent(fill.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var t = lblGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = ElarionUi.FontMicro;
            t.color = ink;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.raycastTarget = false;

            // MEASURE the label (TMP's own metrics, rect-independent — valid at build time,
            // before any layout pass) and size the chip to it. The old text.Length*18+28 guess
            // is what over-asked the row into the shrink that garbled the capture.
            float textW = t.GetPreferredValues(text).x;
            if (textW <= 1f) textW = text.Length * 14f;   // font atlas not ready — honest estimate
            le.preferredWidth = textW + ChipPadPx;
            le.minWidth = 0f;   // a too-full row still shrinks rather than spilling off the pane

            // FIT the label to whatever width the chip ends up with: bounded auto-size, then
            // Ellipsis. The old Overflow was the run-together's second half — a shrunken chip
            // let its text paint over the neighbours. Ellipsis's vertical-cull trap does not
            // apply here: the 44px label rect seats the floor's line box with room to spare.
            ElarionUiKit.FitSingleLine(t, minFontPx > 0f ? minFontPx : ElarionUiKit.FontFloor,
                                       ElarionUi.FontMicro);
        }

        // ── Commands ────────────────────────────────────────────────────────────

        private void OnAccept(string id)
        {
            if (_vm == null) return;
            _vm.Accept(id);     // StartQuest + status; the VM raises Changed -> Repaint
            SetStatus(_vm.Status);
        }

        private void OnTrack(string id)
        {
            // The VM pins it (SetTracked) then invokes onClose -> this View's Close.
            _vm?.Track(id);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        private void ClearContent()
        {
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) SafeDestroy(c.gameObject);
            }
        }

        /// <summary>Destroys immediately in edit mode (the UICaptureLaunch headless-screenshot
        /// path repaints without Play — runtime Destroy is edit-illegal), normally in play.</summary>
        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static void ClearChildren(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var c = host.GetChild(i);
                if (c != null) SafeDestroy(c.gameObject);
            }
        }
    }
}
