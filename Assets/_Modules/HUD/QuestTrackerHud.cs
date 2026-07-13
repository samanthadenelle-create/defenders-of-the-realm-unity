// =============================================================================
// QuestTrackerHud — far-RIGHT HUD quest ICON that opens the Rumor Board.
// -----------------------------------------------------------------------------
// OWNER RULING 2026-07-06: "the on screen quest board should be minimized to just
// an icon on screen, and open to the panel on click." The old always-expanded
// tracker card (QUEST eyebrow + title + objective + "tap to open board") is GONE
// from the HUD — the board panel is now the reading surface. This widget is a
// single ~52px kit-dressed icon button at the same right-edge anchor; a subtle
// gold dot (luminance cue, not hue-meaning — owner is colorblind) marks that the
// tracked quest changed/updated since the player last opened the board.
//
// CANON-CORRECT (CLAUDE.md §8): code-built uGUI (Canvas + Image + TMP), NOT a
// UIDocument. The prior UIDocument version did not render (trace: active=False /
// hasRoot=False / rootResolved=0x0) — UIDocument HUDs are unreliable in this project.
// Builds its OWN ScreenSpaceOverlay Canvas (no PanelSettings/theme dependency).
//
// Data plumbing is UNCHANGED (presentation-only change): still reads QuestService +
// QuestCatalog, still resolves the tracked quest (type-aware WO-454 fallback), still
// repaints on QuestService.QuestChanged, still hides while any modal is open and
// when no quest is active. Click → PanelRouter.Open(PanelId.RumorBoard), the exact
// route the old "tap to open board" card used.
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
        private RectTransform _card;        // the icon-button medallion (visibility toggled on repaint)
        private GameObject _attentionDot;   // subtle gold update cue (luminance, not hue-meaning)
        private bool _subscribed;
        private string _lastSnapshot;       // tracked-id|objective — detects quest updates for the dot
        private bool _painted;              // first Repaint never arms the dot

        private void OnEnable()
        {
            Build();
            if (QuestService.Instance != null)
            {
                QuestService.Instance.QuestChanged += Repaint;
                _subscribed = true;
            }
            // MODAL DISCIPLINE (eyes-on pass 2026-07-03: the tracker card overlapped the open
            // Gear Shop / Talents frames in the bot captures — this widget predates the kit's
            // hud-areas occupancy, so it must observe the arbiter itself): hide while any
            // modal is open, exactly like the kit's `modal` posture stands the HUD down.
            PanelManager.OpenStateChanged += SyncModalVisibility;
            SyncModalVisibility();
        }

        private void OnDisable()
        {
            if (_subscribed && QuestService.Instance != null)
                QuestService.Instance.QuestChanged -= Repaint;
            _subscribed = false;
            PanelManager.OpenStateChanged -= SyncModalVisibility;
        }

        private void SyncModalVisibility()
        {
            if (_ui != null) _ui.SetActive(!PanelManager.AnyOpen);
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

            // OWNER 2026-07-06: minimized to a single ICON at the same right-edge anchor
            // (centre of the old card's 0.34–0.58 band), ~52px kit-dressed medallion, CLICKABLE.
            var card = new GameObject("TrackerIcon", typeof(Image), typeof(Button));
            card.transform.SetParent(_ui.transform, false);
            _card = card.GetComponent<RectTransform>();
            _card.anchorMin = new Vector2(1f, 0.46f);
            _card.anchorMax = new Vector2(1f, 0.46f);
            _card.pivot = new Vector2(1f, 0.5f);
            _card.sizeDelta = new Vector2(52f, 52f);
            _card.anchoredPosition = new Vector2(-10f, 0f);
            // Kit-dressed plate: Obsidian action-slot sprite; translucent stone square fallback.
            var plateImg = card.GetComponent<Image>();
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotAction);
            if (plate != null)
            {
                plateImg.sprite = plate;
                plateImg.color = Color.white;
            }
            else
            {
                var bg = ElarionUi.PanelStoneDark;
                plateImg.color = new Color(bg.r, bg.g, bg.b, 0.72f);
            }
            card.GetComponent<Button>().onClick.AddListener(OpenBoard); // click → pop the board

            // Quest concept icon (scroll/map glyph). Sprite from the catalog; if the pack is
            // not imported, a procedural gold ◈ glyph stands in — NEVER blank.
            var iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconQuest);
            if (iconSprite != null)
            {
                var icon = new GameObject("Icon", typeof(Image));
                icon.transform.SetParent(_card, false);
                var ir = icon.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.5f, 0.5f);
                ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = new Vector2(34f, 34f);
                var ii = icon.GetComponent<Image>();
                ii.sprite = iconSprite;
                ii.preserveAspect = true;
                ii.raycastTarget = false; // let the medallion's Button receive the click
            }
            else
            {
                var glyph = new GameObject("IconGlyph", typeof(TMPro.TextMeshProUGUI));
                glyph.transform.SetParent(_card, false);
                var gr = glyph.GetComponent<RectTransform>();
                gr.anchorMin = Vector2.zero;
                gr.anchorMax = Vector2.one;
                gr.offsetMin = Vector2.zero;
                gr.offsetMax = Vector2.zero;
                var gt = glyph.GetComponent<TMPro.TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(gt); // font-safe before .text (TMP GenerateTextMesh NRE)
                gt.text = "!"; // ASCII quest marker (RPG "!" convention; build-font-safe, no glyph tofu)
                gt.fontSize = 26;
                gt.color = ElarionUi.Gilt;
                gt.alignment = TMPro.TextAlignmentOptions.Center;
                gt.raycastTarget = false;
            }

            // Attention dot: small gold ● top-right of the medallion, shown only when the
            // tracked quest changes/updates after the first paint; cleared on open.
            // IMAGE dot, not a TMP glyph — U+25CF is unverified in the project font and a
            // tofu box here would violate the no-tofu law (the star-tofu lesson). A rotated
            // gold Image quad reads as a diamond stud; sprite-free, glyph-proof.
            var dot = new GameObject("AttentionDot", typeof(UnityEngine.UI.Image));
            dot.transform.SetParent(_card, false);
            var dr = dot.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(1f, 1f);
            dr.anchorMax = new Vector2(1f, 1f);
            dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(10f, 10f);
            dr.anchoredPosition = new Vector2(-4f, -4f);
            dr.localEulerAngles = new Vector3(0f, 0f, 45f);
            var di = dot.GetComponent<UnityEngine.UI.Image>();
            di.color = ElarionUi.Gilt;
            di.raycastTarget = false;
            _attentionDot = dot;
            _attentionDot.SetActive(false);

            Repaint();
        }

        private void OpenBoard()
        {
            if (_attentionDot != null) _attentionDot.SetActive(false); // the board is the reading surface — cue served
            if (!PanelRouter.Open(PanelId.RumorBoard))
                FlowTrace.Warn("HUD", "QuestTracker: RumorBoard opener not registered — cannot pop the board.");
        }

        // ── Repaint = visibility + update-cue only (owner 2026-07-06: the icon carries
        //    no text; the Rumor Board panel is the reading surface). Data plumbing —
        //    QuestService / QuestCatalog reads + the WO-454 type-aware tracked-quest
        //    fallback — is kept intact so the update cue (and any future reader) still
        //    resolves the same quest the old card pinned. ──────────────────────────
        private void Repaint()
        {
            if (_card == null) return;

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

            // Update cue: the tracked quest id or its current objective changed since the
            // last paint → light the gold dot. First paint never arms it (session start
            // is not "new"). Cleared in OpenBoard when the player reads the board.
            var stage = svc.GetStage(tracked);
            string objective = stage != null && !string.IsNullOrEmpty(stage.ObjectiveText)
                ? stage.ObjectiveText : "";
            string snapshot = tracked + "|" + objective;
            if (_painted && _attentionDot != null && snapshot != _lastSnapshot)
                _attentionDot.SetActive(true);
            _lastSnapshot = snapshot;
            _painted = true;
        }
    }
}
