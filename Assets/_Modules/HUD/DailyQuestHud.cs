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
using DeNelle.Core.UI;
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
        // Quests we've already celebrated, so a toast fires only on the FIRST time
        // a quest reaches Completed (owner WO-35: "expected something on completion").
        private readonly System.Collections.Generic.HashSet<string> _celebrated =
            new System.Collections.Generic.HashSet<string>();
        private bool _initialized;
        public static DailyQuestHud Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _doc = GetComponent<UIDocument>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Show/hide the daily-quest list — driven by the TOWN ACTIONS "Quests" button
        /// (WO-411: quests are on-demand, not a free-floating top-right panel).</summary>
        public void Toggle()
        {
            if (_stack == null) return;
            bool show = _stack.style.display == DisplayStyle.None;
            _stack.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _stack.pickingMode = show ? PickingMode.Position : PickingMode.Ignore;
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
            _stack.style.display = DisplayStyle.None;   // hidden by default — shown by the Quests button (Toggle)
            _root.Add(_stack);

            Repaint();
        }

        private void Repaint()
        {
            if (_stack == null) return;
            _stack.Clear();

            var svc = DailyQuestService.Instance;
            var today = svc?.Today;
            Debug.Log($"[DailyQuestHud] Repaint — svc={svc != null}, today={today != null}");
            if (svc == null) return;
            if (today == null) return;

            var header = new Label("Daily Quests");
            header.style.color = new StyleColor(ElarionUi.Gilt);
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

            // Fire a completion toast the first time each quest reaches Completed.
            // The first paint seeds _celebrated silently (so quests already done
            // from a prior session don't toast on load); after that, new completions
            // pop a toast (owner WO-35).
            foreach (var q in today.Quests)
            {
                if (q == null || !q.Completed) continue;
                string key = q.TemplateId ?? q.Label ?? q.Slot;
                if (string.IsNullOrEmpty(key) || _celebrated.Contains(key)) continue;
                _celebrated.Add(key);
                if (_initialized) ShowCompletionToast(q);
            }
            _initialized = true;
        }

        // A brief centred "Daily Quest Complete" banner that auto-dismisses.
        private void ShowCompletionToast(DailyQuestInstance q)
        {
            if (_root == null) return;
            var toast = new Label("✓  Daily Quest Complete\n" + ResolveLabel(q));
            var s = toast.style;
            s.position = Position.Absolute;
            s.top = 130;
            s.left = Length.Percent(50);
            s.translate = new Translate(Length.Percent(-50), 0, 0);
            s.paddingLeft = 20; s.paddingRight = 20; s.paddingTop = 12; s.paddingBottom = 12;
            s.backgroundColor = new StyleColor(new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.96f));
            s.color = new StyleColor(ElarionUi.Parchment);
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.fontSize = 16;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.whiteSpace = WhiteSpace.Normal;
            s.borderTopLeftRadius = 10; s.borderTopRightRadius = 10;
            s.borderBottomLeftRadius = 10; s.borderBottomRightRadius = 10;
            s.borderTopWidth = 1; s.borderBottomWidth = 1; s.borderLeftWidth = 1; s.borderRightWidth = 1;
            var rim = new StyleColor(ElarionUi.Affordable);
            s.borderTopColor = rim; s.borderBottomColor = rim; s.borderLeftColor = rim; s.borderRightColor = rim;
            toast.pickingMode = PickingMode.Ignore;
            _root.Add(toast);
            // Auto-dismiss after a short dwell (UI Toolkit scheduler — no coroutine).
            toast.schedule.Execute(() => { if (toast != null) toast.RemoveFromHierarchy(); }).StartingIn(3200);
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
            label.style.color = new StyleColor(ElarionUi.Parchment);
            label.style.fontSize = 12;
            label.style.flexGrow = 1;
            titleRow.Add(label);

            var progress = new Label($"{q.Progress}/{q.Target}");
            progress.style.color = new StyleColor(ElarionUi.ParchmentDim);
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
            ? new Color(ElarionUi.Affordable.r * 0.35f, ElarionUi.Affordable.g * 0.35f, ElarionUi.Affordable.b * 0.35f, 0.92f)
            : new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.92f);

        private static Color RimFor(string slot) => slot switch
        {
            "combat"      => ElarionUi.Danger,
            "exploration" => ElarionUi.ManaBlue,
            "wildcard"    => ElarionUi.Aether,
            _ => ElarionUi.Gold,
        };
    }
}
