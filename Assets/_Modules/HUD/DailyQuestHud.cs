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

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class DailyQuestHud : MonoBehaviour
    {
        private ElarionUiKit.PanelChrome _chrome;
        private GameObject _canvas;
        private Transform _listHost;     // bodyLeft — dark list well
        private Transform _detailHost;   // bodyRight — parchment detail well
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

            _chrome.root.SetActive(false);   // built hidden; Toggle() shows it
        }

        // ── Repaint (renders from vm.* ONLY — strict MVVM, Silo E) ────────────────
        private void Repaint()
        {
            if (_listHost == null || _detailHost == null || _vm == null) return;
            ClearZone(_listHost);
            ClearZone(_detailHost);

            var quests = _vm.Quests;

            if (_vm.IsEmpty)
            {
                var none = ElarionUiKit.Label(_listHost, "No daily quests today.",
                    0.40f, 0.60f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0.05f, 0.95f);
                none.fontStyle = FontStyles.Italic;
                ElarionUiKit.FitBlock(none, ElarionUi.FontFloorMobile);
                ElarionUiKit.BuildParchmentDetailEmpty(_detailHost, "No daily quests today",
                    "Fresh quests arrive with the new day.");
            }
            else
            {
                // Quest rows (dark well, left) — the Jeweler/Crafting master-list grammar:
                // kit Obsidian buttons, selected = Yellow face, readable right-aligned state.
                const float rowH = 0.16f, gap = 0.02f;
                float top = 0.98f;
                for (int i = 0; i < quests.Count; i++)
                {
                    var item = quests[i];
                    string key = item.Id;
                    bool selected = key == _vm.SelectedId;
                    var rowBtn = ElarionUiKit.BuildObsidianButton(_listHost, item.Name,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => _vm.Select(key));   // command -> VM raises Changed -> Repaint
                    // State text carries the state (colorblind law); color = reinforcement.
                    ElarionUiKit.AddRowStateSuffix(rowBtn,
                        item.Equipped ? "+ Done" : _vm.ProgressText(key),
                        item.Equipped ? ElarionUi.Affordable : ElarionUi.ParchmentDim);
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
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
