// =============================================================================
// DailyQuestHud — top-right stack of three quest chips showing today's
// daily-quest progress. Spawned by DailyQuestHudBootstrap once a scene has a
// hero (so it does NOT appear on Title / HeroSelect).
// -----------------------------------------------------------------------------
// Reads DailyQuestService.Today.Quests, paints one chip per slot, refreshes on
// the service's SetChanged event. Minimal — no claim flow, no reward toast yet
// (that's a follow-up task once economy reward dispense is wired).
// =============================================================================

using DeNelle.Core.Quests;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DailyQuestHud : MonoBehaviour
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
            if (DailyQuestService.Instance != null)
                DailyQuestService.Instance.SetChanged += Repaint;
        }

        private void OnDisable()
        {
            if (DailyQuestService.Instance != null)
                DailyQuestService.Instance.SetChanged -= Repaint;
        }

        private void Build()
        {
            _root = _doc.rootVisualElement;
            if (_root == null) return;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.justifyContent = Justify.FlexStart;
            _root.style.alignItems = Align.FlexEnd;
            _root.pickingMode = PickingMode.Ignore;

            _stack = new VisualElement { name = "DailyQuestStack" };
            _stack.style.marginTop = 96;          // below the wave timer
            _stack.style.marginRight = 16;
            _stack.style.width = 280;
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

            var svc = DailyQuestService.Instance;
            if (svc == null) return;
            var today = svc.Today;
            if (today == null) return;

            var header = new Label("Daily Quests");
            header.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            header.style.unityTextAlign = TextAnchor.MiddleRight;
            _stack.Add(header);

            foreach (var q in today.Quests)
            {
                if (q == null) continue;
                _stack.Add(BuildChip(q));
            }
        }

        private static VisualElement BuildChip(DailyQuestInstance q)
        {
            var chip = new VisualElement();
            chip.style.marginBottom = 4;
            chip.style.paddingLeft = 10; chip.style.paddingRight = 10;
            chip.style.paddingTop = 6;   chip.style.paddingBottom = 6;
            chip.style.minWidth = 240;
            chip.style.borderTopLeftRadius = 8;
            chip.style.borderTopRightRadius = 8;
            chip.style.borderBottomLeftRadius = 8;
            chip.style.borderBottomRightRadius = 8;
            chip.style.backgroundColor = new StyleColor(BgFor(q.Slot, q.Completed));
            chip.style.borderLeftWidth = 0; chip.style.borderRightWidth = 0;
            chip.style.borderTopWidth = 1;  chip.style.borderBottomWidth = 1;
            chip.style.borderTopColor    = new StyleColor(RimFor(q.Slot));
            chip.style.borderBottomColor = new StyleColor(RimFor(q.Slot));

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            chip.Add(titleRow);

            var label = new Label(ResolveLabel(q));
            label.style.color = new StyleColor(new Color(0.96f, 0.94f, 0.88f, 1f));
            label.style.fontSize = 12;
            label.style.flexGrow = 1;
            titleRow.Add(label);

            var progress = new Label($"{q.Progress}/{q.Target}");
            progress.style.color = new StyleColor(new Color(0.75f, 0.78f, 0.82f, 1f));
            progress.style.fontSize = 11;
            progress.style.marginLeft = 8;
            titleRow.Add(progress);

            var bar = new VisualElement();
            bar.style.marginTop = 4;
            bar.style.height = 4;
            bar.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            bar.style.borderTopLeftRadius = 2; bar.style.borderTopRightRadius = 2;
            bar.style.borderBottomLeftRadius = 2; bar.style.borderBottomRightRadius = 2;
            bar.style.flexDirection = FlexDirection.Row;
            chip.Add(bar);

            var fill = new VisualElement();
            fill.style.width = new Length(q.ProgressFraction * 100f, LengthUnit.Percent);
            fill.style.backgroundColor = new StyleColor(RimFor(q.Slot));
            fill.style.borderTopLeftRadius = 2; fill.style.borderTopRightRadius = 2;
            fill.style.borderBottomLeftRadius = 2; fill.style.borderBottomRightRadius = 2;
            bar.Add(fill);

            return chip;
        }

        private static string ResolveLabel(DailyQuestInstance q)
        {
            if (string.IsNullOrEmpty(q.Label)) return q.TemplateId;
            return q.Label.Replace("{target}", q.Target.ToString());
        }

        private static Color BgFor(string slot, bool done) => done
            ? new Color(0.16f, 0.30f, 0.18f, 0.92f)
            : slot switch
            {
                "combat"      => new Color(0.20f, 0.07f, 0.06f, 0.92f),
                "exploration" => new Color(0.06f, 0.13f, 0.20f, 0.92f),
                "wildcard"    => new Color(0.14f, 0.07f, 0.22f, 0.92f),
                _ => new Color(0.10f, 0.10f, 0.12f, 0.92f),
            };

        private static Color RimFor(string slot) => slot switch
        {
            "combat"      => new Color(0.92f, 0.45f, 0.28f, 1f),
            "exploration" => new Color(0.40f, 0.80f, 0.95f, 1f),
            "wildcard"    => new Color(0.78f, 0.55f, 1f, 1f),
            _ => new Color(0.6f, 0.6f, 0.6f, 1f),
        };
    }
}
