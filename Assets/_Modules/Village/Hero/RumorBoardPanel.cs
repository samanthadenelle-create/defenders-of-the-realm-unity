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
        // EYES-SWEEP 2026-07-06 (#4): right-hand detail pane — tapping a rumor row reads its full
        // text here; the empty state renders an authored line (no silent blanks law).
        private TMPro.TextMeshProUGUI _detailTitle;
        private TMPro.TextMeshProUGUI _detailBody;
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
                frameName: RpgUiCatalog.FrameQuest, medallionIcon: "quest");
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
            // EYES-SWEEP 2026-07-06 (#4): the old centered column (0.14–0.86) left the right half of
            // the wide Quest frame as a large BLANK region. Two-pane layout now: rumor list LEFT,
            // detail pane RIGHT (built below) with an authored empty state.
            vpr.anchorMin = new Vector2(0.03f, 0.08f);
            vpr.anchorMax = new Vector2(0.56f, 0.82f); // WO-454: leave room for the tab strip above
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
            // SWEEP 9413 R2 (#5): bottom padding = one row so the last row scrolls fully clear of
            // the viewport mask instead of being sliced mid-glyph at max scroll.
            vlg.padding = new RectOffset(6, 6, 6, 176);
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

            // Detail pane (EYES-SWEEP 2026-07-06 #4): right half of the body. A dark plate + title +
            // body text; ShowDetail fills it when a row is tapped; the empty state is an AUTHORED
            // line — a blank region is never rendered (no silent blanks law).
            var detailGo = new GameObject("DetailPane", typeof(Image));
            detailGo.transform.SetParent(bodyHost, false);
            var dRect = detailGo.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.585f, 0.08f);
            dRect.anchorMax = new Vector2(0.97f, 0.82f);
            dRect.offsetMin = Vector2.zero; dRect.offsetMax = Vector2.zero;
            var dImg = detailGo.GetComponent<Image>();
            // SWEEP 9413 R2 (#1): the slot_item plate rendered as a large TAN slab under the copy —
            // wrong family for this well. Obsidian-dark fill, consistent with the row plates; the
            // title/body stay top-anchored so short copy reads intentional over the dark pane.
            dImg.color = new Color(0f, 0f, 0f, 0.35f);
            dImg.raycastTarget = false;

            var dTitleGo = new GameObject("DetailTitle", typeof(TMPro.TextMeshProUGUI));
            dTitleGo.transform.SetParent(detailGo.transform, false);
            var dtRect = dTitleGo.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0.06f, 0.86f);
            dtRect.anchorMax = new Vector2(0.94f, 0.97f);
            dtRect.offsetMin = Vector2.zero; dtRect.offsetMax = Vector2.zero;
            _detailTitle = dTitleGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_detailTitle);
            _detailTitle.fontStyle = TMPro.FontStyles.Bold;
            _detailTitle.color = ElarionUi.Gilt;
            _detailTitle.alignment = TMPro.TextAlignmentOptions.Left;

            var dBodyGo = new GameObject("DetailBody", typeof(TMPro.TextMeshProUGUI));
            dBodyGo.transform.SetParent(detailGo.transform, false);
            var dbRect = dBodyGo.GetComponent<RectTransform>();
            dbRect.anchorMin = new Vector2(0.06f, 0.05f);
            dbRect.anchorMax = new Vector2(0.94f, 0.84f);
            dbRect.offsetMin = Vector2.zero; dbRect.offsetMax = Vector2.zero;
            _detailBody = dBodyGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_detailBody);
            _detailBody.color = ElarionUi.Parchment;
            _detailBody.alignment = TMPro.TextAlignmentOptions.TopLeft;
            _detailBody.textWrappingMode = TMPro.TextWrappingModes.Normal;
            ShowDetailEmpty();

            // Status line
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(bodyHost, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.03f, 0.01f);
            sRect.anchorMax = new Vector2(0.97f, 0.07f);
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
            _detailTitle = null;
            _detailBody = null;
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
                string tabKey = key;
                float x0 = i * (w + gap);
                // OWNER 2026-07-06 ("theme buttons to UI common"): tabs were bespoke flat Image
                // plates (gold/stone Color fills + hand-rolled TMP label). Now the ONE kit button —
                // active = Yellow (gold), rest Gray — labels fitted by the kit (FitSingleLine).
                ElarionUiKit.BuildObsidianButton(_tabStrip.transform, TabLabels[i],
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    isActive ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(x0, 0f), new Vector2(x0 + w, 1f),
                    () => SetTab(tabKey));
            }
        }

        private void SetTab(string tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;
            if (_ui != null) BuildTabStrip(_panelRoot ?? _ui.transform);
            ShowDetailEmpty();   // the previous selection may not exist under the new tab
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

            string title = q.Label ?? q.TemplateId ?? q.Slot;
            CreateTitle(row.transform, title);

            string progress = q.Completed
                ? $"Complete  ({q.Target}/{q.Target})"
                : $"{q.Progress}/{q.Target}";
            CreateHook(row.transform, progress, q.Completed ? ElarionUi.Gilt : ElarionUi.ParchmentDim);
            MakeRowSelectable(row, title,
                (q.Completed ? "Complete." : "In progress — " + progress + ".") +
                "\n\nDaily quests reset with the day. Finish them for a steady trickle of rewards.");
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
            // OWNER 2026-07-06 ("theme buttons to UI common"): was a bespoke flat plate (gold/stone
            // Image color + hand-rolled TMP). Now the ONE kit button — tracked = Green (the pack's
            // affirmative, plus the TRACKED label so state is never color-only), untracked = Gold.
            // SWEEP 9413 R2 (#1): at card size the 0.80–0.99 band ellipsized the label ("TR…" —
            // the kit's FontFloor won't shrink below 30). Wider band + shorter words: PIN/PINNED.
            bool isTracked = svc != null && svc.TrackedId == def.Id;
            string tid = def.Id;
            ElarionUiKit.BuildObsidianButton(row.transform,
                isTracked ? "PINNED" : "PIN",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                isTracked ? ElarionUiKit.ObsidianButtonColor.Green
                          : ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.66f, 0.18f), new Vector2(0.985f, 0.82f),
                () => OnTrack(tid));

            MakeRowSelectable(row, def.Title ?? def.Id,
                "Current objective:\n" + objective +
                "\n\nThis rumor is underway. TRACK pins it to your HUD.");
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

            // ACCEPT button → StartQuest. OWNER F8 2026-07-06 t=171 ("accept buttons should be
            // themed obsidian"): was a bespoke flat GREEN rectangle (ElarionUi.Affordable Image
            // color + hand-rolled TMP — a green-hue-only affordance, colorblind law). Now the ONE
            // kit gold CTA with a fitted label, like every other panel.
            // SWEEP 9413 R2 (#1): widened band (was 0.80–0.99 → "A…" ellipsis at card size).
            string id = def.Id;
            ElarionUiKit.BuildObsidianButton(row.transform, "ACCEPT",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.66f, 0.18f), new Vector2(0.985f, 0.82f),
                () => OnAccept(id));

            MakeRowSelectable(row, def.Title ?? def.Id,
                hook + "\n\nAccept this rumor to add it to your ledger.");
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

        // EYES-SWEEP 2026-07-06 (#4): tapping a row reads its rumor in the right-hand detail
        // pane. The row's own Image becomes the click target; the TRACK/ACCEPT kit buttons sit
        // on top and keep their own clicks.
        private void MakeRowSelectable(GameObject row, string title, string body)
        {
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = row.GetComponent<Image>();
            ElarionUiKit.StyleButtonColors(btn);
            btn.onClick.AddListener(() => ShowDetail(title, body));
        }

        // Fill the detail pane with the selected rumor's full text.
        private void ShowDetail(string title, string body)
        {
            if (_detailTitle == null || _detailBody == null) return;
            _detailTitle.text = title ?? "";
            ElarionUiKit.FitSingleLine(_detailTitle);
            _detailBody.text = body ?? "";
            _detailBody.fontSize = 14;
            ElarionUiKit.FitBlock(_detailBody, 10f, 15f);
        }

        // Authored empty state — the pane must never render as a blank region (no silent blanks).
        private void ShowDetailEmpty()
        {
            if (_detailTitle == null || _detailBody == null) return;
            _detailTitle.text = "The Board Awaits";
            ElarionUiKit.FitSingleLine(_detailTitle);
            _detailBody.text = "Select a rumor to read the full tale.\n\n" +
                "Whispers gather here from every corner of Elarion — pick one up, and Brom will " +
                "point you where the trouble started.";
            _detailBody.fontSize = 14;
            ElarionUiKit.FitBlock(_detailBody, 10f, 15f);
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
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
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

        // ── Status / content helpers ──────────────────────────────────────────────
        // (The old CreateHeader / CreateBigButton bespoke-chrome helpers were removed —
        //  the header + Close are now the shared Obsidian kit chrome, not hand-rolled.)

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
