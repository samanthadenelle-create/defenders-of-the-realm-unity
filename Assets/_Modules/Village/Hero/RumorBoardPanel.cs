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

        // Detail pane widgets (rebuilt content, persistent hosts).
        private TMPro.TextMeshProUGUI _detailTag;
        private TMPro.TextMeshProUGUI _detailTitle;
        private TMPro.TextMeshProUGUI _detailBody;
        private TMPro.TextMeshProUGUI _detailReward;
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

            // WO-810 responsive split — SCREENSHOT-CORRECTED (capture 2026-07-30): the Quest
            // frame is a BOOK. The body drop-zone is only the LEFT page; the tan parchment at
            // the right is frame art OUTSIDE the body zone (the old "big empty parchment"
            // defect was this exact geometry). So: the LIST fills the left page (body zone);
            // the DETAIL anchors to the PANEL over the right parchment page, on a solid
            // obsidian plate (the signed spec's dark pane — it covers the tan slab).
            bool portrait = Screen.height > Screen.width;
            Vector2 listMin, listMax, detailMin, detailMax;
            Transform detailHost;
            if (portrait)
            {
                listMin = new Vector2(0.03f, 0.48f); listMax = new Vector2(0.97f, 0.855f);
                detailMin = new Vector2(0.05f, 0.05f); detailMax = new Vector2(0.95f, 0.46f);
                detailHost = panel.transform;
            }
            else
            {
                listMin = new Vector2(0.03f, 0.07f); listMax = new Vector2(0.97f, 0.86f);
                detailMin = new Vector2(0.51f, 0.14f); detailMax = new Vector2(0.955f, 0.78f);
                detailHost = panel.transform;
            }

            BuildTabStrip(bodyHost);

            // ── LEFT: the scrollable card list (WO-795 — rows never stack/overlap) ──
            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(bodyHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = listMin;
            vpr.anchorMax = listMax;
            vpr.offsetMin = Vector2.zero;
            vpr.offsetMax = Vector2.zero;
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
            // (signed spec: dark pane, not the tan slab) — 0.35 alpha let the tan bleed through.
            dImg.color = new Color(0.05f, 0.045f, 0.04f, 0.92f);
            dImg.raycastTarget = false;

            // Tag row (type · state) — micro dim line above the title.
            _detailTag = MakeDetailLabel(detailGo.transform, "DetailTag",
                new Vector2(0.05f, 0.925f), new Vector2(0.95f, 0.985f),
                ElarionUi.ParchmentDim, 12, bold: false);

            _detailTitle = MakeDetailLabel(detailGo.transform, "DetailTitle",
                new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.925f),
                ElarionUi.Gilt, 18, bold: true);

            _detailBody = MakeDetailLabel(detailGo.transform, "DetailBody",
                new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.82f),
                ElarionUi.Parchment, 14, bold: false);
            _detailBody.alignment = TMPro.TextAlignmentOptions.TopLeft;
            _detailBody.textWrappingMode = TMPro.TextWrappingModes.Normal;

            _detailReward = MakeDetailLabel(detailGo.transform, "DetailReward",
                new Vector2(0.05f, 0.21f), new Vector2(0.95f, 0.29f),
                ElarionUi.Gilt, 13, bold: false);

            // Status line under everything.
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(bodyHost, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.03f, 0.005f);
            sRect.anchorMax = new Vector2(0.97f, 0.06f);
            sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_statusText);
            _statusText.fontSize = 14;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
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
            _detailTag = null;
            _detailTitle = null;
            _detailBody = null;
            _detailReward = null;
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
            if (_tabStrip != null) { Destroy(_tabStrip); _tabStrip = null; }

            _tabStrip = new GameObject("TabStrip", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            _tabStrip.transform.SetParent(parent, false);
            var sr = _tabStrip.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.03f, 0.87f);
            sr.anchorMax = new Vector2(0.97f, 0.95f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
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
            go.GetComponent<LayoutElement>().preferredHeight = 40f;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = 15;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.BottomLeft;
        }

        private void CreateFlavorRow(Transform parent, string txt)
        {
            var go = new GameObject("Flavor", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 34f;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = 13;
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
            row.GetComponent<LayoutElement>().preferredHeight = 96f;
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
            bool hasPip = !string.IsNullOrEmpty(pip);
            var titleGo = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
            titleGo.transform.SetParent(row.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.04f, 0.52f);
            trt.anchorMax = new Vector2(hasPip ? 0.74f : 0.96f, 0.94f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(tt);
            tt.text = title;
            tt.fontSize = 15;
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
                prt.anchorMin = new Vector2(0.74f, 0.55f);
                prt.anchorMax = new Vector2(0.96f, 0.94f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pt = pipGo.GetComponent<TMPro.TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(pt);
                pt.text = pip;
                pt.fontSize = 11;
                pt.color = pipGilt ? ElarionUi.Gilt : ElarionUi.ParchmentDim;
                pt.alignment = TMPro.TextAlignmentOptions.Right;
                pt.raycastTarget = false;
            }

            // Line 2: one-line hook, dim, ellipsized (never wraps into a third line).
            var hookGo = new GameObject("Hook", typeof(TMPro.TextMeshProUGUI));
            hookGo.transform.SetParent(row.transform, false);
            var hrt = hookGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.04f, 0.08f);
            hrt.anchorMax = new Vector2(0.96f, 0.50f);
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            var ht = hookGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(ht);
            ht.text = hook;
            ht.fontSize = 12;
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

            // CTA is rebuilt per selection (its word + state change with the row kind).
            if (_detailCtaGo != null) { Destroy(_detailCtaGo); _detailCtaGo = null; }

            if (_selectedId == null || _selectedKind == RowKind.None)
            {
                _detailTag.text = "";
                _detailTitle.text = "The Board Awaits";
                ElarionUiKit.FitSingleLine(_detailTitle);
                _detailBody.text = "Select a rumor to read the full tale.\n\n" +
                    "Whispers gather here from every corner of Elarion - pick one up, and Brom will " +
                    "point you where the trouble started.";
                _detailBody.fontSize = 14;
                ElarionUiKit.FitBlock(_detailBody, 10f, 15f);
                _detailReward.text = "";
                return;
            }

            if (_selectedKind == RowKind.Daily)
            {
                var d = FindDaily(_selectedId);
                _detailTag.text = "Daily Quest";
                _detailTitle.text = d.HasValue ? d.Value.Title : _selectedId;
                ElarionUiKit.FitSingleLine(_detailTitle);
                string prog = d.HasValue
                    ? (d.Value.Completed ? "Complete." : "In progress - " + d.Value.Progress + "/" + d.Value.Target + ".")
                    : "";
                _detailBody.text = prog +
                    "\n\nDaily quests reset with the day. Finish them for a steady trickle of rewards.";
                _detailBody.fontSize = 14;
                ElarionUiKit.FitBlock(_detailBody, 10f, 15f);
                _detailReward.text = "";
                return;   // dailies have no CTA — they advance through play
            }

            bool active = _selectedKind == RowKind.Active;
            string title = TitleOf(_selectedId);
            bool tracked = IsTracked(_selectedId);

            _detailTag.text = _vm.TypeFor(_selectedId) + " Quest  |  "
                + (active ? (tracked ? "In Progress - Tracked" : "In Progress") : "New");
            _detailTitle.text = title;
            ElarionUiKit.FitSingleLine(_detailTitle);

            _detailBody.text = active
                ? "Current objective:\n" + _vm.ObjectiveFor(_selectedId) +
                  "\n\nThis rumor is underway. Track pins it to your HUD so you always know the next step."
                : _vm.HookFor(_selectedId) +
                  "\n\nAccept this rumor to add it to your ledger. Brom will point you where the trouble started.";
            _detailBody.fontSize = 14;
            ElarionUiKit.FitBlock(_detailBody, 10f, 15f);

            string reward = _vm.RewardFor(_selectedId);
            _detailReward.text = string.IsNullOrEmpty(reward) ? "" : "Rewards:  " + reward;

            // PRIMARY CTA — pinned at the pane's bottom, full word, >= the touch floor.
            _detailCtaGo = new GameObject("DetailCta", typeof(RectTransform));
            _detailCtaGo.transform.SetParent(_detailPane, false);
            var crt = _detailCtaGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.05f, 0.03f);
            crt.anchorMax = new Vector2(0.60f, 0.18f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            string id = _selectedId;
            if (!active)
            {
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform, "Accept",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    Vector2.zero, Vector2.one, () => OnAccept(id));
            }
            else
            {
                // Tracked = affirmative Green + the word carries the state (never colour-only).
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform,
                    tracked ? "Pinned" : "Track",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    tracked ? ElarionUiKit.ObsidianButtonColor.Green
                            : ElarionUiKit.ObsidianButtonColor.Yellow,
                    Vector2.zero, Vector2.one, () => OnTrack(id));
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
                if (c != null) Destroy(c.gameObject);
            }
        }
    }
}
