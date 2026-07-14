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
// PRESENTATION-ONLY: this view binds DailyQuestService.Today and DISPLAYS it
// (plus the slot's catalog reward row — catalog reads are presentation binding,
// same as QuestTrackerHud's QuestCatalog reads). It owns no quest data or logic;
// it refreshes on the service's SetChanged event. View-side memory: _celebrated
// (completion toast fires exactly once per quest, owner WO-35) + _selectedKey
// (which row the detail card inspects).
//
// Toggled on-demand by the TOWN ACTIONS "Quests" button (WO-411, via Toggle());
// hidden while any modal is open (modal stand-down discipline).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Quests;
using DeNelle.Core.UI;
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
        private string _selectedKey;     // which quest the detail card inspects

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

        // ── Repaint (presentation binding) ───────────────────────────────────────
        private void Repaint()
        {
            if (_listHost == null || _detailHost == null) return;
            ClearZone(_listHost);
            ClearZone(_detailHost);

            var svc = DailyQuestService.Instance;
            var today = svc?.Today;
            if (svc == null || today == null) return;

            var quests = new List<DailyQuestInstance>();
            foreach (var q in today.Quests)
                if (q != null) quests.Add(q);

            if (quests.Count == 0)
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
                // Keep / default the selection (first quest until one is tapped).
                if (string.IsNullOrEmpty(_selectedKey) || FindIndex(quests, _selectedKey) < 0)
                    _selectedKey = KeyFor(quests[0]);

                // Quest rows (dark well, left) — the Jeweler/Crafting master-list grammar:
                // kit Obsidian buttons, selected = Yellow face, readable right-aligned state.
                const float rowH = 0.16f, gap = 0.02f;
                float top = 0.98f;
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    string key = KeyFor(q);
                    bool selected = key == _selectedKey;
                    var rowBtn = ElarionUiKit.BuildObsidianButton(_listHost, ResolveLabel(q),
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => { _selectedKey = key; Repaint(); });
                    // State text carries the state (colorblind law); color = reinforcement.
                    ElarionUiKit.AddRowStateSuffix(rowBtn,
                        q.Completed ? "+ Done" : q.Progress + " / " + q.Target,
                        q.Completed ? ElarionUi.Affordable : ElarionUi.ParchmentDim);
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
                }

                // Detail (parchment well, right — the WO-693 shared compact card).
                int sel = FindIndex(quests, _selectedKey);
                if (sel >= 0) BuildDetail(quests[sel]);
                else
                    ElarionUiKit.BuildParchmentDetailEmpty(_detailHost, "Select a quest",
                        "Tap a quest to inspect its progress and rewards.");
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

        // ── Parchment detail card (WO-693 grammar: name + flavor -> REWARDS -> PROGRESS) ──
        private void BuildDetail(DailyQuestInstance q)
        {
            // REWARDS — the slot's catalog grant, relayed generically ("+ Crystals +25").
            // All-ASCII glyphs; Good tone is reinforcement only (counts are the carrier).
            var rewards = new List<ElarionUiKit.DetailCardRow>();
            var slotReward = DailyQuestCatalog.RewardFor(q.Slot);
            if (slotReward != null)
            {
                if (slotReward.RewardCrystals > 0)
                    rewards.Add(new ElarionUiKit.DetailCardRow("+", "Crystals",
                        "+" + slotReward.RewardCrystals, ElarionUiKit.DetailRowTone.Good));
                if (slotReward.RewardFood > 0)
                    rewards.Add(new ElarionUiKit.DetailCardRow("+", "Food",
                        "+" + slotReward.RewardFood, ElarionUiKit.DetailRowTone.Good));
                if (slotReward.RewardGlimmer > 0)
                    rewards.Add(new ElarionUiKit.DetailCardRow("+", "Glimmer",
                        "+" + slotReward.RewardGlimmer, ElarionUiKit.DetailRowTone.Good));
                if (slotReward.RewardWisdom > 0)
                    rewards.Add(new ElarionUiKit.DetailCardRow("+", "Wisdom",
                        "+" + slotReward.RewardWisdom, ElarionUiKit.DetailRowTone.Good));
                if (slotReward.RewardRandomItem)
                    rewards.Add(new ElarionUiKit.DetailCardRow("*", "Bonus item", "1",
                        ElarionUiKit.DetailRowTone.Dim));
            }

            // PROGRESS — "OK done" / count toward target; glyph + counts carry the state.
            var progress = new List<ElarionUiKit.DetailCardRow>
            {
                new ElarionUiKit.DetailCardRow(
                    q.Completed ? "OK" : "*",
                    q.Completed ? "Complete" : "In progress",
                    q.Progress + " / " + q.Target,
                    q.Completed ? ElarionUiKit.DetailRowTone.Good : ElarionUiKit.DetailRowTone.Neutral),
            };

            ElarionUiKit.BuildParchmentDetailCard(_detailHost, new ElarionUiKit.DetailCardSpec
            {
                Title = ResolveLabel(q),
                Flavor = SlotFlavor(q.Slot),
                BestowsHeader = "REWARDS",
                Bestows = rewards,
                RequiresHeader = "PROGRESS",
                Requires = progress,
                // No CTA: completion rewards auto-dispense (DEF-223 reward bridge).
            });
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

        private static string KeyFor(DailyQuestInstance q)
            => q.Id ?? q.TemplateId ?? q.Slot ?? "";

        private static int FindIndex(List<DailyQuestInstance> quests, string key)
        {
            if (quests == null || string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < quests.Count; i++)
                if (KeyFor(quests[i]) == key) return i;
            return -1;
        }

        private static string ResolveLabel(DailyQuestInstance q)
        {
            if (string.IsNullOrEmpty(q.Label)) return q.TemplateId;
            return q.Label.Replace("{target}", q.Target.ToString());
        }

        /// <summary>One readable flavor line naming the quest's slot (text carries the
        /// category — the old slot-color rim is retired with the per-screen chrome).</summary>
        private static string SlotFlavor(string slot)
        {
            switch (slot)
            {
                case "combat":      return "Combat objective - resets daily.";
                case "exploration": return "Exploration objective - resets daily.";
                case "wildcard":    return "Wildcard objective - resets daily.";
                default:            return "Daily objective - resets daily.";
            }
        }

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
