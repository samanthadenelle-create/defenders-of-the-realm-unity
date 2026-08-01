// =============================================================================
// DailyQuestHud — today's daily quests on the pack QUESTS reference (WO-714 W3).
// Spawned by DailyQuestHudBootstrap once a scene has a hero (so it does NOT
// appear on Title / HeroSelect).
// -----------------------------------------------------------------------------
// WO-714 Phase 2 W3 conformance (2026-07-13): the compact FrameCore card ->
// the pack QUESTS reference grammar (Quest_Log_Panel / FrameQuest master-detail,
// the owner-ratified FrameCrafting template shape):
//   * bodyLeft  (dark well)      = quest rows (kit Obsidian buttons, selected =
//                                  Yellow; right-aligned readable state
//                                  "+ Done" / "2 / 5" — counts carry the state,
//                                  color is reinforcement only, colorblind law)
//   * bodyRight (parchment well) = the WO-693 shared COMPACT DETAIL CARD
//                                  (ElarionUiKit.BuildParchmentDetailCard:
//                                  name + slot flavor -> REWARDS -> PROGRESS).
//                                  No CTA — rewards auto-dispense on completion
//                                  (DEF-223 reward bridge), there is no claim.
// The ONE shared Close is the chrome's; the frame supplies ALL chrome (no
// per-screen plates/rims/fills — the old BuildRow/BgFor/RimFor grammar is gone).
//
// WO-795 (2026-08-01): the quest list is a ScrollRect well (Viewport drag-catcher +
// RectMask2D; top-anchored Content = VerticalLayoutGroup + ContentSizeFitter, bottom
// pad one row); rows are fixed-height LayoutElement hosts (MinTouchPx). The old
// anchored-fraction rows + truncation break; are gone — a deep quest log scrolls.
//
// STRICT MVVM (Silo E, 2026-07-17): this View binds a DailyQuestVM and DISPLAYS
// vm.* only — it owns NO quest data or logic. The quest set, row SELECTION, and
// reward lookups all live in the VM (which subscribes to the service's SetChanged
// and raises Changed). View-side memory is presentation-only: _celebrated
// (completion toast fires exactly once per quest, owner WO-35).
//
// Toggled on-demand by the TOWN ACTIONS "Quests" button (WO-411, via Toggle());
// hidden while any modal is open (modal stand-down discipline).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;   // ScrollRect/RectMask2D/layout: the WO-795 scroll-list pattern

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class DailyQuestHud : MonoBehaviour
    {
        private ElarionUiKit.PanelChrome _chrome;
        private GameObject _canvas;
        private Transform _listHost;     // bodyLeft — dark list well
        private Transform _detailHost;   // bodyRight — parchment detail well
        private Transform _rowHost;      // WO-795 ScrollRect Content; quest rows rebuilt into it
        private ScrollRect _scroll;      // the quest-list scroller (built once in EnsureBuilt)

        // One quest row in reference px (1080x1920 canvas). MinTouchPx keeps the tap
        // target legal (WO-795: fixed-height layout rows replace anchored fractions).
        private const float RowPx = ElarionUiKit.MinTouchPx;
        // Quests we've already celebrated, so a toast fires only on the FIRST time
        // a quest reaches Completed (owner WO-35: "expected something on completion").
        private readonly HashSet<string> _celebrated = new HashSet<string>();
        private bool _initialized;
        private bool _visible;           // user toggle state (the card starts hidden)

        // Strict MVVM (Silo E): ALL quest state + selection + reward lookups live in the
        // VM; this View reads vm.* only and never touches DailyQuestService / DailyQuestCatalog.
        private DailyQuestVM _vm;

        public static DailyQuestHud Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            EnsureBuilt();
            if (_vm != null) _vm.Changed += Repaint;
            // MODAL DISCIPLINE (eyes-on pass 2026-07-03: the floating card must not
            // overlap open modal frames): hide while any modal is open, like the kit's
            // `modal` posture.
            PanelManager.OpenStateChanged += ApplyVisibility;
            Repaint();
        }

        private void OnDisable()
        {
            if (_vm != null) _vm.Changed -= Repaint;
            PanelManager.OpenStateChanged -= ApplyVisibility;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_vm != null) _vm.Changed -= Repaint;
            _vm?.Dispose();
            _vm = null;
            if (_canvas != null) Destroy(_canvas);
        }

        /// <summary>Show/hide the daily-quest panel — driven by the TOWN ACTIONS "Quests"
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

        // ── UI construction (kit FrameQuest master-detail on its own overlay canvas) ──
        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            // VM FIRST — it resolves DailyQuestService + DailyQuestCatalog itself, so this
            // View never touches a service; it owns the quest set, selection + reward lookups.
            _vm = DailyQuestVM.CreateDefault(() => { _visible = false; ApplyVisibility(); });

            _canvas = ElarionUiKit.BuildModalCanvas("DailyQuestHud", 80);
            var c = _canvas.GetComponent<Canvas>();
            if (c != null) c.overrideSorting = true;

            // The pack QUESTS reference: FrameQuest (Quest_Log_Panel) master-detail —
            // dark quest list LEFT, parchment detail RIGHT, medallion, ONE shared kit
            // Close (it hides the card, matching the Quests-button toggle). No backdrop
            // scrim — this stays a non-blocking HUD surface, not a PanelManager modal.
            _chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform, "Daily Quests",
                new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f),
                onClose: () => { _visible = false; ApplyVisibility(); },
                withBackdrop: false, frameName: RpgUiCatalog.FrameQuest, medallionIcon: "quest");

            // Drop-zones only (canon §4): split wells when the frame resolves; graceful
            // fallback zones on the procedural path (frame art absent — never blank).
            var layout = _chrome.layout;
            _listHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null
                    ? (Transform)layout.body
                    : MakeZone(_chrome.content.transform, "ListWell",
                        new Vector2(0.05f, 0.24f), new Vector2(0.48f, 0.86f)));
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : MakeZone(_chrome.content.transform, "DetailWell",
                    new Vector2(0.52f, 0.24f), new Vector2(0.95f, 0.86f));

            // WO-795 scroll well (RumorBoardPanel recipe): Viewport (near-invisible Image
            // drag-catcher + RectMask2D) filling the list well; Content = top-anchored
            // VerticalLayoutGroup + ContentSizeFitter. Rows are fixed-height LayoutElement
            // hosts, so EVERY quest lists and scrolls: no fraction math, no truncation.
            var viewportGo = new GameObject("QuestViewport",
                typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(_listHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = Vector2.zero;
            vpr.anchorMax = Vector2.one;
            vpr.offsetMin = Vector2.zero; vpr.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            var contentGo = new GameObject("QuestRows",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(0.5f, 1f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true; vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            // Bottom pad = one row so the last quest scrolls fully clear of the mask.
            vlg.padding = new RectOffset(6, 6, 6, (int)RowPx + 8);
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _scroll = viewportGo.GetComponent<ScrollRect>();
            _scroll.viewport = vpr;
            _scroll.content  = cr;
            _scroll.horizontal = false;
            _scroll.vertical   = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 25f;

            _rowHost = contentGo.transform;

            _chrome.root.SetActive(false);   // built hidden; Toggle() shows it
        }

        // ── Repaint (renders from vm.* ONLY — strict MVVM, Silo E) ────────────────
        private void Repaint()
        {
            if (_listHost == null || _detailHost == null || _rowHost == null || _vm == null) return;
            ClearZone(_rowHost);
            ClearZone(_detailHost);

            var quests = _vm.Quests;

            if (_vm.IsEmpty)
            {
                var none = ElarionUiKit.Label(_rowHost, "No daily quests today.",
                    0f, 1f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0.05f, 0.95f);
                none.fontStyle = FontStyles.Italic;
                ElarionUiKit.FitBlock(none, ElarionUi.FontFloorMobile);
                // _rowHost is layout-driven now; give the notice a row-sized slot.
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = RowPx;
                ElarionUiKit.BuildParchmentDetailEmpty(_detailHost, "No daily quests today",
                    "Fresh quests arrive with the new day.");
            }
            else
            {
                // Quest rows (dark well, left) — the Jeweler/Crafting master-list grammar:
                // kit Obsidian buttons, selected = Yellow face, readable right-aligned state.
                // WO-795: fixed-height LayoutElement hosts inside the ScrollRect Content;
                // the VerticalLayoutGroup stacks them and the ScrollRect scrolls them —
                // every quest lists, none is ever truncated (the old break; is gone).
                for (int i = 0; i < quests.Count; i++)
                {
                    var item = quests[i];
                    string key = item.Id;
                    bool selected = key == _vm.SelectedId;
                    var host = new GameObject("Row_" + key,
                        typeof(RectTransform), typeof(LayoutElement));
                    host.transform.SetParent(_rowHost, false);
                    var le = host.GetComponent<LayoutElement>();
                    le.preferredHeight = RowPx;
                    le.minHeight = RowPx;
                    var rowBtn = ElarionUiKit.BuildObsidianButton(host.transform, item.Name,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        Vector2.zero, Vector2.one,
                        () => _vm.Select(key));   // command -> VM raises Changed -> Repaint
                    // State text carries the state (colorblind law); color = reinforcement.
                    ElarionUiKit.AddRowStateSuffix(rowBtn,
                        item.Equipped ? "+ Done" : _vm.ProgressText(key),
                        item.Equipped ? ElarionUi.Affordable : ElarionUi.ParchmentDim);
                }

                // Detail (parchment well, right — the WO-693 shared compact card).
                ItemVM sel = default;
                bool found = false;
                foreach (var item in quests)
                    if (item.Id == _vm.SelectedId) { sel = item; found = true; break; }
                if (found) BuildDetail(sel);
                else
                    ElarionUiKit.BuildParchmentDetailEmpty(_detailHost, "Select a quest",
                        "Tap a quest to inspect its progress and rewards.");
            }

            // Fire a completion toast the first time each quest reaches Completed.
            // The first paint seeds _celebrated silently (so quests already done from a
            // prior session don't toast on load); after that, new completions pop a toast.
            foreach (var item in quests)
            {
                if (!item.Equipped) continue;
                string key = _vm.CelebrationKeyFor(item.Id);
                if (string.IsNullOrEmpty(key) || _celebrated.Contains(key)) continue;
                _celebrated.Add(key);
                if (_initialized) ShowCompletionToast(item.Name);
            }
            _initialized = true;
        }

        // ── Parchment detail card (WO-693 grammar: name + flavor -> REWARDS -> PROGRESS) ──
        private void BuildDetail(ItemVM item)
        {
            // REWARDS — the slot's grant (from vm.RewardFor), relayed generically ("+ Crystals +25").
            // All-ASCII glyphs; Good tone is reinforcement only (counts are the carrier).
            var rewards = new List<ElarionUiKit.DetailCardRow>();
            var r = _vm.RewardFor(item.Id);
            if (r.Crystals > 0)
                rewards.Add(new ElarionUiKit.DetailCardRow("+", "Crystals",
                    "+" + r.Crystals, ElarionUiKit.DetailRowTone.Good));
            if (r.Food > 0)
                rewards.Add(new ElarionUiKit.DetailCardRow("+", "Food",
                    "+" + r.Food, ElarionUiKit.DetailRowTone.Good));
            if (r.Glimmer > 0)
                rewards.Add(new ElarionUiKit.DetailCardRow("+", "Glimmer",
                    "+" + r.Glimmer, ElarionUiKit.DetailRowTone.Good));
            if (r.Wisdom > 0)
                rewards.Add(new ElarionUiKit.DetailCardRow("+", "Wisdom",
                    "+" + r.Wisdom, ElarionUiKit.DetailRowTone.Good));
            if (r.RandomItem)
                rewards.Add(new ElarionUiKit.DetailCardRow("*", "Bonus item", "1",
                    ElarionUiKit.DetailRowTone.Dim));

            // PROGRESS — "OK done" / count toward target; glyph + counts carry the state.
            bool completed = item.Equipped;
            var progress = new List<ElarionUiKit.DetailCardRow>
            {
                new ElarionUiKit.DetailCardRow(
                    completed ? "OK" : "*",
                    completed ? "Complete" : "In progress",
                    _vm.ProgressText(item.Id),
                    completed ? ElarionUiKit.DetailRowTone.Good : ElarionUiKit.DetailRowTone.Neutral),
            };

            ElarionUiKit.BuildParchmentDetailCard(_detailHost, new ElarionUiKit.DetailCardSpec
            {
                Title = item.Name,
                Flavor = _vm.FlavorFor(item.Id),
                BestowsHeader = "REWARDS",
                Bestows = rewards,
                RequiresHeader = "PROGRESS",
                Requires = progress,
                // No CTA: completion rewards auto-dispense (DEF-223 reward bridge).
            });
        }

        // A brief centered "Daily Quest Complete" toast that auto-dismisses (kit ToastCard).
        private void ShowCompletionToast(string label)
        {
            if (_canvas == null) return;
            var toast = ElarionUiKit.ToastCard(_canvas.transform,
                ElarionUiKit.ToastTone.Confirm, accentLeft: false, TextAnchor.MiddleCenter);
            if (toast == null || toast.card == null) return;
            var rt = toast.card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.30f, 0.78f);
            rt.anchorMax = new Vector2(0.70f, 0.86f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            if (toast.label != null) toast.label.text = "Daily Quest Complete\n" + label;
            StartCoroutine(DismissAfter(toast.card, 3.2f));
        }

        private System.Collections.IEnumerator DismissAfter(GameObject go, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (go != null) Destroy(go);
        }

        // ── presentation helpers ─────────────────────────────────────────────────

        // Clear a drop-zone's repaintable children, PRESERVING the factory's ZoneBacking
        // plate (the two-tone parchment-bleed fix paints it as the zone's first child —
        // destroying it would re-expose the baked art seam under the content).
        private static void ClearZone(Transform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i);
                if (child == null || child.name == "ZoneBacking") continue;
                Destroy(child.gameObject);
            }
        }

        // Fallback drop-zone for the PROCEDURAL (frame-art-absent) path only.
        private static Transform MakeZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }
    }
}
