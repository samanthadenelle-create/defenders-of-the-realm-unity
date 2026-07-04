// =============================================================================
// DailyQuestHud — compact, centered daily-quest card showing today's daily-quest
// progress. Spawned by DailyQuestHudBootstrap once a scene has a hero (so it does
// NOT appear on Title / HeroSelect).
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #54): UIDocument/UITK widget
// -> code-built uGUI on the Obsidian master frame (BuildObsidianPanel: FrameCore
// + medallion + the ONE shared kit Close), per the LeaderboardPanel / HelpMenu
// reference recipe. Mobile-first: a small CENTERED card (thumb-zone), not a
// full-height top-right bar.
//
// PRESENTATION-ONLY: this view binds DailyQuestService.Today and DISPLAYS it. It
// owns no quest data or logic; it refreshes on the service's SetChanged event.
// The only view-side memory it keeps is _celebrated (which quests it has already
// toasted) so a completion toast fires exactly once per quest (owner WO-35).
//
// Toggled on-demand by the TOWN ACTIONS "Quests" button (WO-411, via Toggle());
// hidden while any modal is open (modal stand-down discipline).
// =============================================================================

using DeNelle.Core.Quests;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class DailyQuestHud : MonoBehaviour
    {
        private ElarionUiKit.PanelChrome _chrome;
        private GameObject _canvas;
        private Transform _rowHost;
        // Quests we've already celebrated, so a toast fires only on the FIRST time
        // a quest reaches Completed (owner WO-35: "expected something on completion").
        private readonly System.Collections.Generic.HashSet<string> _celebrated =
            new System.Collections.Generic.HashSet<string>();
        private bool _initialized;
        private bool _visible;   // user toggle state (the card starts hidden)

        public static DailyQuestHud Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            EnsureBuilt();
            if (DailyQuestService.Instance != null)
                DailyQuestService.Instance.SetChanged += Repaint;
            // MODAL DISCIPLINE (eyes-on pass 2026-07-03: the floating card must not
            // overlap open modal frames): hide while any modal is open, like the kit's
            // `modal` posture.
            PanelManager.OpenStateChanged += ApplyVisibility;
            Repaint();
        }

        private void OnDisable()
        {
            if (DailyQuestService.Instance != null)
                DailyQuestService.Instance.SetChanged -= Repaint;
            PanelManager.OpenStateChanged -= ApplyVisibility;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas);
        }

        /// <summary>Show/hide the daily-quest card — driven by the TOWN ACTIONS "Quests"
        /// button (WO-411: quests are on-demand, not a free-floating top-right panel).</summary>
        public void Toggle()
        {
            _visible = !_visible;
            ApplyVisibility();
            if (_visible) Repaint();
        }

        // Effective visibility = user toggled ON *and* no modal is open.
        private void ApplyVisibility()
        {
            if (_chrome == null || _chrome.root == null) return;
            _chrome.root.SetActive(_visible && !PanelManager.AnyOpen);
        }

        // ── UI construction (kit panel on its own overlay canvas, lazy) ──────────
        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _canvas = ElarionUiKit.BuildModalCanvas("DailyQuestHud", 80);
            var c = _canvas.GetComponent<Canvas>();
            if (c != null) c.overrideSorting = true;

            // Mobile-first: a compact card centered both horizontally AND vertically —
            // NOT a full-height top-right bar. No backdrop scrim (non-blocking HUD card).
            // Close routes to the kit's ONE shared sleek Close button (never an X); it
            // hides the card, matching the Quests-button toggle.
            _chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform, "Daily Quests",
                new Vector2(0.34f, 0.32f), new Vector2(0.66f, 0.68f),
                onClose: () => { _visible = false; ApplyVisibility(); },
                withBackdrop: false, frameName: RpgUiCatalog.FrameCore, medallionIcon: "quest");

            var body = _chrome.layout != null && _chrome.layout.body != null
                ? (Transform)_chrome.layout.body
                : MakeZone(_chrome.content.transform, "Body",
                    new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.86f));

            _rowHost = MakeZone(body, "QuestRows", Vector2.zero, Vector2.one);

            _chrome.root.SetActive(false);   // built hidden; Toggle() shows it
        }

        // ── Repaint (presentation binding) ───────────────────────────────────────
        private void Repaint()
        {
            if (_rowHost == null) return;
            for (int i = _rowHost.childCount - 1; i >= 0; i--)
                Destroy(_rowHost.GetChild(i).gameObject);

            var svc = DailyQuestService.Instance;
            var today = svc?.Today;
            if (svc == null || today == null) return;

            // Count valid quests so we can stack them compactly and centered.
            var quests = new System.Collections.Generic.List<DailyQuestInstance>();
            foreach (var q in today.Quests)
                if (q != null) quests.Add(q);

            if (quests.Count == 0)
            {
                MakeText(_rowHost, "No daily quests today.", 13, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            }
            else
            {
                const float gap = 0.04f;
                float rowH = (1f - gap * (quests.Count - 1)) / quests.Count;
                rowH = Mathf.Min(rowH, 0.30f);   // cap so few quests stay compact, not stretched
                float top = 1f;
                foreach (var q in quests)
                {
                    BuildRow(_rowHost, q, new Vector2(0f, top - rowH), new Vector2(1f, top));
                    top -= rowH + gap;
                }
            }

            // Fire a completion toast the first time each quest reaches Completed.
            // The first paint seeds _celebrated silently (so quests already done from a
            // prior session don't toast on load); after that, new completions pop a toast.
            foreach (var q in quests)
            {
                if (!q.Completed) continue;
                string key = q.TemplateId ?? q.Label ?? q.Slot;
                if (string.IsNullOrEmpty(key) || _celebrated.Contains(key)) continue;
                _celebrated.Add(key);
                if (_initialized) ShowCompletionToast(q);
            }
            _initialized = true;
        }

        // One compact quest row: rounded plate + name + progress "n/m" + a thin fill bar.
        private void BuildRow(Transform parent, DailyQuestInstance q, Vector2 min, Vector2 max)
        {
            var plate = MakeImage(parent, "Quest", min, max, BgFor(q.Completed), rounded: true);

            MakeText(plate.transform, ResolveLabel(q), 13, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft, new Vector2(0.06f, 0.42f), new Vector2(0.74f, 0.96f));
            MakeText(plate.transform, $"{q.Progress}/{q.Target}", 12, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.MidlineRight, new Vector2(0.74f, 0.42f), new Vector2(0.94f, 0.96f));

            // Progress bar (track + slot-tinted fill).
            var bar = MakeImage(plate.transform, "Bar",
                new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.30f),
                new Color(1f, 1f, 1f, 0.10f), rounded: true);
            float frac = Mathf.Clamp01(q.ProgressFraction);
            MakeImage(bar.transform, "Fill",
                new Vector2(0f, 0f), new Vector2(frac, 1f), RimFor(q.Slot), rounded: true);
        }

        // A brief centered "Daily Quest Complete" toast that auto-dismisses (kit ToastCard).
        private void ShowCompletionToast(DailyQuestInstance q)
        {
            if (_canvas == null) return;
            var toast = ElarionUiKit.ToastCard(_canvas.transform,
                ElarionUiKit.ToastTone.Confirm, accentLeft: false, TextAnchor.MiddleCenter);
            if (toast == null || toast.card == null) return;
            var rt = toast.card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.30f, 0.78f);
            rt.anchorMax = new Vector2(0.70f, 0.86f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            if (toast.label != null) toast.label.text = "Daily Quest Complete\n" + ResolveLabel(q);
            StartCoroutine(DismissAfter(toast.card, 3.2f));
        }

        private System.Collections.IEnumerator DismissAfter(GameObject go, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (go != null) Destroy(go);
        }

        // ── presentation helpers ─────────────────────────────────────────────────

        private static string ResolveLabel(DailyQuestInstance q)
        {
            if (string.IsNullOrEmpty(q.Label)) return q.TemplateId;
            return q.Label.Replace("{target}", q.Target.ToString());
        }

        private static Color BgFor(bool done) => done
            ? new Color(ElarionUi.Affordable.r * 0.35f, ElarionUi.Affordable.g * 0.35f, ElarionUi.Affordable.b * 0.35f, 0.92f)
            : new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.92f);

        private static Color RimFor(string slot) => slot switch
        {
            "combat"      => ElarionUi.Danger,
            "exploration" => ElarionUi.ManaBlue,
            "wildcard"    => ElarionUi.Aether,
            _ => ElarionUi.Gold,
        };

        // ── uGUI construction helpers (mirror LeaderboardPanel) ──────────────────

        private static Transform MakeZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max,
            Color color, bool rounded)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (rounded) ElarionUiKit.ApplyRounded(img);
            return img;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
