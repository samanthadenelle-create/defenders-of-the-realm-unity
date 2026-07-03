// =============================================================================
// QuestTrackerHud — far-RIGHT HUD that PINS the player's ONE tracked story quest.
// -----------------------------------------------------------------------------
// CANON-CORRECT (CLAUDE.md §8): code-built uGUI (Canvas + Image + TMP), NOT a
// UIDocument. The prior UIDocument version did not render (trace: active=False /
// hasRoot=False / rootResolved=0x0) — UIDocument HUDs are unreliable in this project.
// Builds its OWN ScreenSpaceOverlay Canvas (no PanelSettings/theme dependency), so it
// renders the same as the Rumor Board (which is uGUI).
//
// Shows the player-TRACKED quest (chosen via the board's Track button; falls back to
// the first active quest until one is picked). Click the card to POP OUT the full
// Rumor Board. Reads QuestService + QuestCatalog; repaints on QuestService.QuestChanged.
// Spawned by QuestTrackerHudBootstrap once a scene has a hero.
// =============================================================================

using DeNelle.Core.Quests;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class QuestTrackerHud : MonoBehaviour
    {
        private GameObject _ui;
        private RectTransform _card;   // the panel we rebuild on repaint
        private bool _subscribed;

        private void OnEnable()
        {
            Build();
            if (QuestService.Instance != null)
            {
                QuestService.Instance.QuestChanged += Repaint;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_subscribed && QuestService.Instance != null)
                QuestService.Instance.QuestChanged -= Repaint;
            _subscribed = false;
        }

        private void OnDestroy() { if (_ui != null) Destroy(_ui); }

        // ── Build the uGUI canvas + the right-side card ──────────────────────────
        private void Build()
        {
            if (_ui != null) return;

            _ui = new GameObject("QuestTrackerHudUI");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80; // above wave timer, below modals (matches daily chips band)

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _ui.AddComponent<GraphicRaycaster>();

            // Far-RIGHT, LOWERED (mid-right), tall + narrow ("minimized but taller"), CLICKABLE.
            var card = new GameObject("TrackerCard", typeof(Image), typeof(Button));
            card.transform.SetParent(_ui.transform, false);
            _card = card.GetComponent<RectTransform>();
            _card.anchorMin = new Vector2(1f, 0.34f);
            _card.anchorMax = new Vector2(1f, 0.58f);
            _card.pivot = new Vector2(1f, 0.5f);
            _card.sizeDelta = new Vector2(236f, 0f);      // sleeker: narrower; height from the anchor span
            _card.anchoredPosition = new Vector2(-10f, 0f);
            // Sleeker: translucent stone so it reads as a slim overlay, not a heavy block.
            var bg = ElarionUi.PanelStoneDark;
            card.GetComponent<Image>().color = new Color(bg.r, bg.g, bg.b, 0.72f);
            card.GetComponent<Button>().onClick.AddListener(OpenBoard); // click → pop the board

            Repaint();
        }

        private void OpenBoard()
        {
            if (!PanelRouter.Open(PanelId.RumorBoard))
                FlowTrace.Warn("HUD", "QuestTracker: RumorBoard opener not registered — cannot pop the board.");
        }

        // ── Paint the tracked quest into the card ────────────────────────────────
        private void Repaint()
        {
            if (_card == null) return;
            for (int i = _card.childCount - 1; i >= 0; i--) Destroy(_card.GetChild(i).gameObject);

            var svc = QuestService.Instance;
            if (svc == null) { _card.gameObject.SetActive(false); return; }

            var ids = svc.ActiveQuestIds();
            if (ids == null || ids.Count == 0) { _card.gameObject.SetActive(false); return; }

            // Player-tracked quest; fall back to an active quest until one is chosen.
            string tracked = svc.TrackedId;
            if (string.IsNullOrEmpty(tracked) || !svc.IsActive(tracked))
            {
                // WO-454 Phase 2: type-aware fallback — prefer a main/story quest over the
                // rest, otherwise the first active. Empty Type normalizes to "story", so a
                // catalog with no type data keeps the old "first active" behavior.
                tracked = null;
                string firstActive = null;
                foreach (var id in ids)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (firstActive == null) firstActive = id;
                    var d = QuestCatalog.FindQuest(id);
                    string ty = (d != null && !string.IsNullOrEmpty(d.Type))
                        ? d.Type.Trim().ToLowerInvariant() : "story";
                    if (ty == "main" || ty == "story") { tracked = id; break; }
                }
                if (tracked == null) tracked = firstActive;
            }
            if (tracked == null) { _card.gameObject.SetActive(false); return; }

            _card.gameObject.SetActive(true);

            AddLabel("◈ QUEST", 0.86f, 0.97f, 13, ElarionUi.Gilt, TMPro.FontStyles.Bold,
                TMPro.TextAlignmentOptions.Right);

            var def = QuestCatalog.FindQuest(tracked);
            string title = def != null && !string.IsNullOrEmpty(def.Title) ? def.Title : tracked;
            AddLabel(title, 0.66f, 0.85f, 15, ElarionUi.Parchment, TMPro.FontStyles.Bold,
                TMPro.TextAlignmentOptions.TopRight);

            var stage = svc.GetStage(tracked);
            string objective = stage != null && !string.IsNullOrEmpty(stage.ObjectiveText)
                ? stage.ObjectiveText : "…";
            AddLabel(objective, 0.04f, 0.64f, 12, ElarionUi.ParchmentDim, TMPro.FontStyles.Normal,
                TMPro.TextAlignmentOptions.TopRight);

            AddLabel("tap to open board", 0.0f, 0.05f, 10, ElarionUi.ParchmentDim, TMPro.FontStyles.Italic,
                TMPro.TextAlignmentOptions.BottomRight);
        }

        private void AddLabel(string text, float yMin, float yMax, int size, Color col,
            TMPro.FontStyles style, TMPro.TextAlignmentOptions align)
        {
            var go = new GameObject("L", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(_card, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.04f, yMin);
            r.anchorMax = new Vector2(0.96f, yMax);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t); // font-safe before .text (avoids the TMP GenerateTextMesh NRE)
            t.text = text;
            t.fontSize = size;
            t.color = col;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
            t.raycastTarget = false; // let the card's Button receive the click
        }
    }
}
