// =============================================================================
// QuestTrackerHud — far-RIGHT HUD panel that PINS the player's ONE current active
// story quest (title + current-stage objective). Owner design (2026-06-20): the
// Rumor Board pop-up is the full browse/accept list; this HUD keeps just the single
// active quest in view, far-right, live-updating as quests are accepted/advanced.
// -----------------------------------------------------------------------------
// Reads QuestService active quests + QuestCatalog titles/stages, repaints on the
// service's QuestChanged event. Code-built UIElements (works in player builds;
// only .uxml ASSETS fail to render — CLAUDE.md §8). Spawned by
// QuestTrackerHudBootstrap once a scene has a hero.
// =============================================================================

using DeNelle.Core.Quests;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class QuestTrackerHud : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _stack;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Build();
            if (QuestService.Instance != null)
                QuestService.Instance.QuestChanged += Repaint;
        }

        private void OnDisable()
        {
            if (QuestService.Instance != null)
                QuestService.Instance.QuestChanged -= Repaint;
        }

        private void Build()
        {
            _root = _doc.rootVisualElement;
            if (_root == null) return;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.justifyContent = Justify.FlexStart;
            _root.style.alignItems = Align.FlexEnd;      // top-RIGHT (owner: pin the active quest far-right)
            _root.pickingMode = PickingMode.Ignore;

            _stack = new VisualElement { name = "QuestTrackerStack" };
            _stack.style.marginTop = 96;          // clear of the top HUD widgets
            _stack.style.marginRight = 16;
            _stack.style.width = 300;
            _stack.style.flexDirection = FlexDirection.Column;
            _stack.style.alignItems = Align.FlexEnd;
            _stack.pickingMode = PickingMode.Ignore;
            _root.Add(_stack);

            Repaint();
        }

        private void Repaint()
        {
            if (_stack == null) return;
            _stack.Clear();

            var svc = QuestService.Instance;
            if (svc == null) return;

            var ids = svc.ActiveQuestIds();
            if (ids == null || ids.Count == 0) return; // nothing active → empty (no header noise)

            // WO-454: pin the player-TRACKED quest (chosen via the board's Track button). If none
            // is chosen yet — or the tracked quest is no longer active — fall back to the first
            // active quest as the default until the player picks one. The board remains the full list.
            string tracked = svc.TrackedId;
            if (string.IsNullOrEmpty(tracked) || !svc.IsActive(tracked))
            {
                tracked = null;
                foreach (var id in ids) { if (!string.IsNullOrEmpty(id)) { tracked = id; break; } }
            }
            if (tracked == null) return;

            var header = new Label("Quest");
            header.style.color = new StyleColor(ElarionUi.Gilt);
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            header.style.unityTextAlign = TextAnchor.MiddleRight;
            _stack.Add(header);

            _stack.Add(BuildCard(tracked, svc));
        }

        private static VisualElement BuildCard(string id, QuestService svc)
        {
            var def = QuestCatalog.FindQuest(id);
            var stage = svc.GetStage(id);

            var card = new VisualElement();
            card.style.marginBottom = 4;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.paddingTop = 6;   card.style.paddingBottom = 6;
            card.style.minWidth = 260;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.backgroundColor = new StyleColor(ElarionUi.PanelStoneDark);
            card.style.borderLeftWidth = 0; card.style.borderRightWidth = 0;
            card.style.borderTopWidth = 1;  card.style.borderBottomWidth = 1;
            var rim = new StyleColor(ElarionUi.Gold);
            card.style.borderTopColor = rim;
            card.style.borderBottomColor = rim;

            var title = new Label(def != null && !string.IsNullOrEmpty(def.Title) ? def.Title : id);
            title.style.color = new StyleColor(ElarionUi.Parchment);
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(title);

            string objective = stage != null && !string.IsNullOrEmpty(stage.ObjectiveText)
                ? stage.ObjectiveText
                : "…";
            var obj = new Label(objective);
            obj.style.color = new StyleColor(ElarionUi.ParchmentDim);
            obj.style.fontSize = 12;
            obj.style.marginTop = 2;
            obj.style.whiteSpace = WhiteSpace.Normal;
            card.Add(obj);

            return card;
        }
    }
}
