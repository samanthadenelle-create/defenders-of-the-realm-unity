// =============================================================================
// RumorBoardPanel (WO-304) — Brom's rumor board: the BROWSE / ACCEPT surface for
// the realm's story + vendor questlines ("The Dimming" arc).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// WHAT IT DOES:
//   A code-built screen-space overlay (uGUI + TMPro, NO UXML — works in player /
//   WebGL builds, CLAUDE.md §8) opened at Brom's Inn. It reads the live quest
//   ledger and presents two sections:
//     • AVAILABLE — quests in the QuestCatalog that are NOT yet active and NOT
//       completed. Shows the title + the stage-1 objective as the "hook", with
//       an ACCEPT button that calls QuestService.StartQuest(id).
//     • ACTIVE — quests currently in progress, with their current-stage
//       objective text (read-only; advanced through the vendors' own dialogue).
//
// READ-ONLY CONSUMER of QuestService / QuestCatalog (Core): never mutates their
// internals — the only write is StartQuest(id) through the public API. Repaints
// on QuestService.QuestChanged so accepting a quest moves it from Available →
// Active in place.
//
// LIFECYCLE / HOSTING:
//   Mirrors ShopPanel exactly — it builds its own Canvas, so NPCCommandBridge can
//   spawn it with a bare `host.AddComponent<RumorBoardPanel>()` and call Open().
//   No UIDocument / PanelSettings required.
//
// OPENED FROM BROM:
//   NPCCommandBridge registers an additive "OpenRumorBoard" Yarn command verb;
//   the existing NPC_Inn.yarn `RumorBoard` node can drop a
//   `<<command: OpenRumorBoard>>` to surface this board.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Quests;
using DeNelle.Core.UI;

namespace DeNelle.Village.Hero
{
    public sealed class RumorBoardPanel : MonoBehaviour
    {
        private GameObject _ui;
        private Transform _panelRoot;   // WO-562: the obsidian content panel (tab strip re-parents here)
        private GameObject _contentRoot;
        private TMPro.TextMeshProUGUI _statusText;
        private bool _subscribed;

        // WO-454 Phase 2: board tab filter. Story/Gear/Endgame read QuestCatalog by Type;
        // Daily reads DailyQuestService. "all" = the original ungrouped catalog view.
        private GameObject _tabStrip;
        private string _activeTab = "all";
        private static readonly string[] TabKeys    = { "all", "story", "daily", "gear", "endgame" };
        private static readonly string[] TabLabels  = { "All", "Story", "Daily", "Gear", "Endgame" };

        // WO-437: this panel used to bypass the modal arbiter (open via backdrop only),
        // so it could stack on top of another panel AND open mid-battle. Register a
        // PanelHandle so it routes through PanelManager: one-panel-at-a-time + battle-lock.
        private PanelHandle _handle;

        // ── Public API ──────────────────────────────────────────────────────────

        public void Open()
        {
            Close();

            if (_handle == null)
                _handle = PanelManager.Register("Rumor Board", Close, () => _ui != null);

            // WO-562: the ONE canonical obsidian modal (canvas + scrim + black panel + gold trim +
            // gold header + the shared Close) replaces the hand-rolled Canvas/backdrop/brown panel +
            // bespoke header + a custom red Close button.
            var modal = ElarionUiKit.BuildObsidianModal("RumorBoardPanelUI", "Brom's Rumor Board",
                new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f), Close, sortingOrder: 1000,
                frameName: RpgUiCatalog.FrameQuest);
            _ui = modal.canvas;
            var panel = modal.chrome.content;
            // WO-582: fit content into the frame's BODY drop-zone (the templated well) instead of
            // floating over the whole panel rect — keeps the list off the ornate Quest frame border.
            // Falls back to the panel rect when no frame is used. All content (tabs/list/status) now
            // anchors as fractions INSIDE the body zone.
            var bodyHost = (modal.chrome.layout != null && modal.chrome.layout.body != null)
                ? modal.chrome.layout.body : (RectTransform)panel.transform;
            _panelRoot = bodyHost;

            // WO-454 Phase 2: tab strip (All / Story / Daily / Gear / Endgame) just under the header.
            BuildTabStrip(bodyHost);

            // SCROLLABLE content area (TKT-3): the board overflowed because rows were placed by
            // normalized anchor math with no clipping/scroll. Now a uGUI ScrollRect — Viewport
            // (RectMask2D clips to the panel) + a vertically-laid-out Content (VerticalLayoutGroup +
            // ContentSizeFitter) that GROWS with the rows and scrolls when it exceeds the viewport.
            // Mirrors ShopPanel's scroll pattern.
            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(bodyHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = new Vector2(0.03f, 0.08f);
            vpr.anchorMax = new Vector2(0.97f, 0.82f); // WO-454: leave room for the tab strip above
            vpr.offsetMin = Vector2.zero;
            vpr.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // near-invisible catcher so drags scroll

            // Content: top-stretch so it grows DOWNWARD as rows are added; sized by ContentSizeFitter.
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
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
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

            // Status line
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(bodyHost, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.02f, 0.01f);
            sRect.anchorMax = new Vector2(0.98f, 0.07f);
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_statusText); // font-safe: a code-built TMP with no font NREs on first GenerateTextMesh
            _statusText.fontSize = 14;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            SetStatus("The talk of Elarion. Accept what calls to you.");

            // Repaint when a quest is started/advanced/completed.
            if (!_subscribed && QuestService.Instance != null)
            {
                QuestService.Instance.QuestChanged += Repaint;
                _subscribed = true;
            }

            Repaint();

            // Route through the modal arbiter: closes any other open panel, and the
            // WO-437 battle-lock rejects this open (and tears the UI back down) if a
            // battle is active. If rejected, _ui is already null here.
            if (!PanelManager.NotifyOpened(_handle)) return;

            Debug.Log("[RumorBoardPanel] Opened.");
        }

        public void Close()
        {
            if (_subscribed && QuestService.Instance != null)
                QuestService.Instance.QuestChanged -= Repaint;
            _subscribed = false;

            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _statusText = null;
            _tabStrip = null;
            PanelManager.NotifyClosed(_handle);
        }

        private void OnDestroy()
        {
            if (_subscribed && QuestService.Instance != null)
                QuestService.Instance.QuestChanged -= Repaint;
            _subscribed = false;
            if (_ui != null) Destroy(_ui);
        }

        // ── Paint ───────────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_contentRoot == null) return;
            ClearContent();

            // WO-454: the Daily tab is a reader over DailyQuestService (different runtime), not
            // the story catalog — render its slots and bail before the catalog grouping below.
            if (_activeTab == "daily") { RepaintDaily(); return; }

            var svc = QuestService.Instance;
            var catalog = QuestCatalog.Quests; // empty list if json missing — safe to enumerate

            // Bucket the catalog: active vs available (not active, not completed), filtered by tab.
            var active = new List<QuestDef>();
            var available = new List<QuestDef>();
            if (catalog != null)
            {
                foreach (var def in catalog)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (!MatchesTab(def, _activeTab)) continue; // WO-454: tab filter by Type
                    if (svc != null && svc.IsActive(def.Id)) { active.Add(def); continue; }
                    if (svc != null && svc.IsCompleted(def.Id)) continue; // done — off the board
                    available.Add(def);
                }
            }

            CreateSectionLabel(_contentRoot.transform, "— In Progress —");
            if (active.Count == 0)
                CreateFlavorRow(_contentRoot.transform, "Nothing underway. Pick up a thread below.");
            foreach (var def in active)
                CreateActiveRow(_contentRoot.transform, def, svc);

            CreateSectionLabel(_contentRoot.transform, "— Rumors & Requests —");
            if (available.Count == 0)
                CreateFlavorRow(_contentRoot.transform, "You've answered every call. For now.");
            foreach (var def in available)
                CreateAvailableRow(_contentRoot.transform, def);
        }

        // ── Tabs (WO-454 Phase 2) ─────────────────────────────────────────────────

        // Normalize a quest's free-string Type → a lowercase bucket; empty/null = "story"
        // so legacy quests with no Type field stay in the Story tab.
        private static string NormalizedType(QuestDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Type)) return "story";
            return def.Type.Trim().ToLowerInvariant();
        }

        // Does this quest belong under the given tab? "all" shows everything; "story" also
        // catches "main"/"side"/unknown (the default narrative bucket); gear/endgame are exact.
        private static bool MatchesTab(QuestDef def, string tab)
        {
            if (tab == "all") return true;
            string ty = NormalizedType(def);
            switch (tab)
            {
                case "gear":    return ty == "gear";
                case "endgame": return ty == "endgame";
                case "story":   return ty != "gear" && ty != "endgame"; // story/main/side/unknown
                default:        return true;
            }
        }

        // Builds (or rebuilds) the horizontal tab strip below the header; the active tab is gilt.
        private void BuildTabStrip(Transform parent)
        {
            if (_tabStrip != null) { Destroy(_tabStrip); _tabStrip = null; }

            _tabStrip = new GameObject("TabStrip", typeof(RectTransform));
            _tabStrip.transform.SetParent(parent, false);
            var sr = _tabStrip.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.03f, 0.83f);
            sr.anchorMax = new Vector2(0.97f, 0.885f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;

            int n = TabKeys.Length;
            float gap = 0.01f;
            float w = (1f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                string key = TabKeys[i];
                bool isActive = key == _activeTab;

                var btnGo = new GameObject("Tab_" + key, typeof(Button), typeof(Image));
                btnGo.transform.SetParent(_tabStrip.transform, false);
                var br = btnGo.GetComponent<RectTransform>();
                float x0 = i * (w + gap);
                br.anchorMin = new Vector2(x0, 0f);
                br.anchorMax = new Vector2(x0 + w, 1f);
                br.offsetMin = Vector2.zero;
                br.offsetMax = Vector2.zero;
                btnGo.GetComponent<Image>().color = isActive
                    ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.92f)
                    : new Color(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.85f);
                string tabKey = key;
                btnGo.GetComponent<Button>().onClick.AddListener(() => SetTab(tabKey));

                var lbl = new GameObject("L", typeof(TMPro.TextMeshProUGUI));
                lbl.transform.SetParent(btnGo.transform, false);
                var lr = lbl.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lt = lbl.GetComponent<TMPro.TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(lt);
                lt.text = TabLabels[i];
                lt.fontSize = 12;
                lt.fontStyle = isActive ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
                lt.color = isActive ? ElarionUi.Ink : ElarionUi.Parchment;
                lt.alignment = TMPro.TextAlignmentOptions.Center;
            }
        }

        private void SetTab(string tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;
            if (_ui != null) BuildTabStrip(_panelRoot ?? _ui.transform);
            Repaint();
        }

        // Daily tab: read-only view of DailyQuestService's rolled slots (its own runtime — the
        // board is just a unified READER; the daily runtime stays in DailyQuestService).
        private void RepaintDaily()
        {
            CreateSectionLabel(_contentRoot.transform, "— Daily Quests —");
            var dq = DailyQuestService.Instance;
            var set = dq != null ? dq.Today : null;
            if (set == null || set.Quests == null || set.Quests.Count == 0)
            {
                CreateFlavorRow(_contentRoot.transform, "No daily quests rolled yet. Check back later.");
                return;
            }
            foreach (var q in set.Quests)
            {
                if (q == null) continue;
                CreateDailyRow(_contentRoot.transform, q);
            }
        }

        private void CreateDailyRow(Transform parent, DailyQuestInstance q)
        {
            var row = MakeRowFrame(parent, "Daily_" + q.Id,
                new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.85f), 120f);

            CreateTitle(row.transform, q.Label ?? q.TemplateId ?? q.Slot);

            string progress = q.Completed
                ? $"Complete  ({q.Target}/{q.Target})"
                : $"{q.Progress}/{q.Target}";
            CreateHook(row.transform, progress, q.Completed ? ElarionUi.Gilt : ElarionUi.ParchmentDim);
        }

        // ── Row builders ─────────────────────────────────────────────────────────

        private void CreateSectionLabel(Transform parent, string txt)
        {
            var go = new GameObject("Section", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 46f;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = 16;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.BottomLeft;
        }

        private void CreateFlavorRow(Transform parent, string txt)
        {
            var go = new GameObject("Flavor", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40f;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = 13;
            t.fontStyle = TMPro.FontStyles.Italic;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Left;
        }

        private void CreateActiveRow(Transform parent, QuestDef def, QuestService svc)
        {
            var row = MakeRowFrame(parent, "Active_" + def.Id,
                new Color(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.85f), 150f);

            CreateTitle(row.transform, def.Title ?? def.Id);

            var stage = svc != null ? svc.GetStage(def.Id) : null;
            string objective = stage != null && !string.IsNullOrEmpty(stage.ObjectiveText)
                ? stage.ObjectiveText
                : "…";
            CreateHook(row.transform, objective, ElarionUi.ParchmentDim);

            // WO-454: Track → pin this quest to the far-right HUD slot, then close the board.
            bool isTracked = svc != null && svc.TrackedId == def.Id;
            var trackGo = new GameObject("Track", typeof(Button), typeof(Image));
            trackGo.transform.SetParent(row.transform, false);
            var tbr = trackGo.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0.80f, 0.18f);
            tbr.anchorMax = new Vector2(0.99f, 0.82f);
            tbr.offsetMin = Vector2.zero;
            tbr.offsetMax = Vector2.zero;
            trackGo.GetComponent<Image>().color = isTracked
                ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.92f)
                : new Color(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.92f);
            string tid = def.Id;
            trackGo.GetComponent<Button>().onClick.AddListener(() => OnTrack(tid));

            var tl = new GameObject("TL", typeof(TMPro.TextMeshProUGUI));
            tl.transform.SetParent(trackGo.transform, false);
            var tlr = tl.GetComponent<RectTransform>();
            tlr.anchorMin = Vector2.zero; tlr.anchorMax = Vector2.one;
            tlr.offsetMin = Vector2.zero; tlr.offsetMax = Vector2.zero;
            var tlt = tl.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(tlt);
            tlt.text = isTracked ? "TRACKED" : "TRACK";
            tlt.fontSize = 12;
            tlt.color = ElarionUi.Ink;
            tlt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        private void CreateAvailableRow(Transform parent, QuestDef def)
        {
            var row = MakeRowFrame(parent, "Avail_" + def.Id,
                new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.85f), 170f);

            CreateTitle(row.transform, def.Title ?? def.Id);

            // Hook text = the first stage's objective (the "what's this about").
            string hook = "A new thread waits to be picked up.";
            if (def.Stages != null && def.Stages.Count > 0 && def.Stages[0] != null
                && !string.IsNullOrEmpty(def.Stages[0].ObjectiveText))
                hook = def.Stages[0].ObjectiveText;
            CreateHook(row.transform, hook, ElarionUi.Parchment);

            // ACCEPT button → StartQuest.
            var btnGo = new GameObject("Accept", typeof(Button), typeof(Image));
            btnGo.transform.SetParent(row.transform, false);
            var br = btnGo.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.80f, 0.18f);
            br.anchorMax = new Vector2(0.99f, 0.82f);
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;
            btnGo.GetComponent<Image>().color = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.92f);
            string id = def.Id;
            btnGo.GetComponent<Button>().onClick.AddListener(() => OnAccept(id));

            var bl = new GameObject("BL", typeof(TMPro.TextMeshProUGUI));
            bl.transform.SetParent(btnGo.transform, false);
            var blr = bl.GetComponent<RectTransform>();
            blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one;
            blr.offsetMin = Vector2.zero; blr.offsetMax = Vector2.zero;
            var blt = bl.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(blt);
            blt.text = "ACCEPT";
            blt.fontSize = 13;
            blt.color = ElarionUi.Ink;
            blt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        // Shared row frame: an Image strip with a FIXED PIXEL height. The Content's
        // VerticalLayoutGroup stacks it under the previous row; ContentSizeFitter + the ScrollRect
        // handle overflow (scrollable). Internal children still anchor 0–1 within this row.
        private GameObject MakeRowFrame(Transform parent, string name, Color bg, float heightPx)
        {
            var row = new GameObject(name, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = heightPx;
            row.GetComponent<Image>().color = bg;
            return row;
        }

        private void CreateTitle(Transform parent, string txt)
        {
            var go = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.55f);
            r.anchorMax = new Vector2(0.79f, 0.95f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = 15;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.color = ElarionUi.Parchment;
            t.alignment = TMPro.TextAlignmentOptions.Left;
        }

        private void CreateHook(Transform parent, string txt, Color col)
        {
            var go = new GameObject("Hook", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.05f);
            r.anchorMax = new Vector2(0.79f, 0.55f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = 12;
            t.color = col;
            t.alignment = TMPro.TextAlignmentOptions.TopLeft;
            t.enableWordWrapping = true;
        }

        // ── Accept flow ───────────────────────────────────────────────────────────

        private void OnAccept(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var svc = QuestService.Instance;
            if (svc == null) { SetStatus("Quests aren't ready yet."); return; }

            svc.StartQuest(id); // moves Available → Active; raises QuestChanged → Repaint
            var def = QuestCatalog.FindQuest(id);
            SetStatus($"Accepted: {(def != null && !string.IsNullOrEmpty(def.Title) ? def.Title : id)}.");
            // QuestChanged repaints the board; if the service wasn't up to fire it,
            // repaint defensively so the row still moves to In Progress.
            if (!svc.IsActive(id)) Repaint();
        }

        // WO-454: pin an active quest to the far-right HUD tracker, then close the board
        // (owner flow: open board → select the quest you want → close → it shows on the right).
        private void OnTrack(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var svc = QuestService.Instance;
            if (svc == null) { SetStatus("Quests aren't ready yet."); return; }
            svc.SetTracked(id);   // persists + raises QuestChanged → HUD pin repaints
            Close();
        }

        // ── Chrome helpers (mirrors ShopPanel) ────────────────────────────────────

        private void CreateHeader(Transform parent, string txt)
        {
            var go = new GameObject("Header", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.89f);
            r.anchorMax = new Vector2(0.98f, 0.99f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = 24;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.text = txt;
        }

        private void CreateBigButton(Transform parent, string label, Vector2 anchor,
            System.Action onClick, Color? bg = null)
        {
            var go = new GameObject("Btn_" + label, typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(anchor.x - 0.08f, anchor.y - 0.03f);
            r.anchorMax = new Vector2(anchor.x + 0.08f, anchor.y + 0.03f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = bg ?? ElarionUi.PanelStone;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var txt = new GameObject("L", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(tt);
            tt.text = label;
            tt.fontSize = 15;
            tt.color = ElarionUi.Parchment;
            tt.alignment = TMPro.TextAlignmentOptions.Center;
        }

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
