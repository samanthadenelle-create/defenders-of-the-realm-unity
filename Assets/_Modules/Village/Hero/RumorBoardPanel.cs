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
        private GameObject _contentRoot;
        private TMPro.TextMeshProUGUI _statusText;
        private bool _subscribed;

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

            _ui = new GameObject("RumorBoardPanelUI");
            _ui.transform.SetParent(null, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _ui.AddComponent<GraphicRaycaster>();

            // Backdrop (tap to close)
            var backdrop = new GameObject("Backdrop", typeof(Image), typeof(Button));
            backdrop.transform.SetParent(_ui.transform, false);
            var bdRect = backdrop.GetComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.offsetMin = Vector2.zero;
            bdRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = ElarionUi.Scrim;
            backdrop.GetComponent<Button>().onClick.AddListener(Close);

            // Panel frame
            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_ui.transform, false);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.08f, 0.1f);
            pr.anchorMax = new Vector2(0.92f, 0.9f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = ElarionUi.PanelStoneDark;

            CreateHeader(panel.transform, "Brom's Rumor Board");

            // Close button
            CreateBigButton(panel.transform, "Close", new Vector2(0.5f, 0.94f), Close,
                new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.55f));

            // Scroll-free content area (rows laid out top-down by anchor math).
            _contentRoot = new GameObject("Content");
            _contentRoot.transform.SetParent(panel.transform, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.03f, 0.08f);
            cr.anchorMax = new Vector2(0.97f, 0.87f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            // Status line
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel.transform, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.02f, 0.01f);
            sRect.anchorMax = new Vector2(0.98f, 0.07f);
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
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

            var svc = QuestService.Instance;
            var catalog = QuestCatalog.Quests; // empty list if json missing — safe to enumerate

            // Bucket the catalog: active vs available (not active, not completed).
            var active = new List<QuestDef>();
            var available = new List<QuestDef>();
            if (catalog != null)
            {
                foreach (var def in catalog)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (svc != null && svc.IsActive(def.Id)) { active.Add(def); continue; }
                    if (svc != null && svc.IsCompleted(def.Id)) continue; // done — off the board
                    available.Add(def);
                }
            }

            float y = 0.98f;

            CreateSectionLabel(_contentRoot.transform, "— In Progress —", ref y);
            if (active.Count == 0)
                CreateFlavorRow(_contentRoot.transform, "Nothing underway. Pick up a thread below.", ref y);
            foreach (var def in active)
                CreateActiveRow(_contentRoot.transform, def, svc, ref y);

            y -= 0.02f;
            CreateSectionLabel(_contentRoot.transform, "— Rumors & Requests —", ref y);
            if (available.Count == 0)
                CreateFlavorRow(_contentRoot.transform, "You've answered every call. For now.", ref y);
            foreach (var def in available)
                CreateAvailableRow(_contentRoot.transform, def, ref y);
        }

        // ── Row builders ─────────────────────────────────────────────────────────

        private void CreateSectionLabel(Transform parent, string txt, ref float y)
        {
            var go = new GameObject("Section", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.0f, y - 0.05f);
            r.anchorMax = new Vector2(1.0f, y);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = txt;
            t.fontSize = 16;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            y -= 0.06f;
        }

        private void CreateFlavorRow(Transform parent, string txt, ref float y)
        {
            var go = new GameObject("Flavor", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, y - 0.05f);
            r.anchorMax = new Vector2(0.98f, y);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = txt;
            t.fontSize = 13;
            t.fontStyle = TMPro.FontStyles.Italic;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            y -= 0.055f;
        }

        private void CreateActiveRow(Transform parent, QuestDef def, QuestService svc, ref float y)
        {
            var row = MakeRowFrame(parent, "Active_" + def.Id, ref y,
                new Color(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.85f), 0.11f);

            CreateTitle(row.transform, def.Title ?? def.Id);

            var stage = svc != null ? svc.GetStage(def.Id) : null;
            string objective = stage != null && !string.IsNullOrEmpty(stage.ObjectiveText)
                ? stage.ObjectiveText
                : "…";
            CreateHook(row.transform, objective, ElarionUi.ParchmentDim);
        }

        private void CreateAvailableRow(Transform parent, QuestDef def, ref float y)
        {
            var row = MakeRowFrame(parent, "Avail_" + def.Id, ref y,
                new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.85f), 0.13f);

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
            blt.text = "ACCEPT";
            blt.fontSize = 13;
            blt.color = ElarionUi.Ink;
            blt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        // Shared row frame: an Image strip that consumes `height` of normalized Y
        // starting at the current cursor, advancing `y` past it.
        private GameObject MakeRowFrame(Transform parent, string name, ref float y, Color bg, float height)
        {
            var row = new GameObject(name, typeof(Image));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.0f, y - height);
            rr.anchorMax = new Vector2(1.0f, y - 0.01f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            row.GetComponent<Image>().color = bg;
            y -= (height + 0.01f);
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
