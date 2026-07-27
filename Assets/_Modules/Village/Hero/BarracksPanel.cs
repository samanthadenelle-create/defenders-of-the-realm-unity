// =============================================================================
// BarracksPanel — the WO-771.9 Barracks & troop UPGRADE UI (code-built uGUI).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// A DUMB SKIN over the shared kit chrome (BuildObsidianPanel FrameCrafting master-
// detail + the ONE shared Close), mirroring TroopTrainingPanel. WO-744 strict MVVM:
// it BINDS a BarracksPanelVM (IPanelView) and reads NO game state — ALL rules
// (afford / unlock / in-flight / spend / enqueue / progression) live in the VM +
// BarracksService; this view invents none and never names EconomyService /
// FindAnyObjectByType / a catalog.
//
//   * bodyLeft  (dark well, scrollable) = a "Barracks Level" card at the top + one
//     row per troop (level + lock chip). Locked troops stay VISIBLE (ladder education).
//   * bodyRight (parchment well)        = the selected entry's detail:
//       - Barracks card -> level, next-unlock troop(s), cost + time, Upgrade CTA.
//       - A troop        -> level/max, Reach + Strength bars, next ability
//         (locked/unlocked), upgrade cost + time, Upgrade CTA.
//   * footer                            = the ONE kit wallet row (wood/iron/food/crystal).
//
// Code-built uGUI, NO UXML (canon §8). Self-installs via ShowBarracksUI() (host
// resolution lives in BarracksPanelVM.ResolveOrCreateHost so this View stays free of
// FindAnyObjectByType), gated on BarracksUnlock.IsUnlocked. Re-renders on the VM's
// Changed event (the VM owns the BarracksService + economy subscriptions).
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    public sealed class BarracksPanel : MonoBehaviour, IPanelView
    {
        private BarracksPanelVM _vm;

        private GameObject _ui;
        private Transform _listHost;          // bodyLeft — dark well
        private RectTransform _listContent;   // scroll content the rows parent into
        private Transform _detailHost;        // bodyRight — parchment well
        private ElarionUiKit.CurrencyChipHandle[] _wallet;
        private PanelHandle _panelHandle;
        private string _selectedId = BarracksPanelVM.BarracksSelId;

        // Ink for text on the parchment detail well (family convention, mirrors TroopTrainingPanel).
        private static readonly Color Ink     = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color InkDim  = new Color(0.34f, 0.28f, 0.20f, 1f);
        private static readonly Color InkGood = new Color(0.10f, 0.42f, 0.16f, 1f);
        private static readonly Color InkBad  = new Color(0.55f, 0.12f, 0.10f, 1f);

        private static readonly Color RowSelected = new Color(0.42f, 0.34f, 0.14f, 0.95f);
        private static readonly Color RowUnlocked = new Color(0.16f, 0.16f, 0.18f, 0.92f);
        private static readonly Color LockedTint  = new Color(0.52f, 0.52f, 0.55f, 0.80f);
        private static readonly Color BarTrack     = new Color(0.20f, 0.16f, 0.12f, 0.55f);
        private static readonly Color BarReach      = new Color(0.30f, 0.55f, 0.80f, 1f);
        private static readonly Color BarStrength   = new Color(0.78f, 0.42f, 0.20f, 1f);

        private const string SlotTalentPlate = "slot_talent_1";
        private const float RowHeightPx = 80f;
        private const float RowGapPx    = 6f;

        public bool IsOpen => _ui != null;

        /// <summary>
        /// Self-installing entry point — opens the Barracks upgrade panel, creating a host if
        /// none exists. Gated on <see cref="BarracksUnlock.IsUnlocked"/> (ff.barracks + founding-
        /// complete) exactly like the train UI; a locked call toasts + no-ops. Host resolution is
        /// delegated to the VM so this View names no scene lookup.
        /// </summary>
        public static void ShowBarracksUI()
        {
            if (!BarracksUnlock.IsUnlocked)
            {
                FlowTrace.Step("Barracks", "ShowBarracksUI refused - Barracks locked.");
                ElarionUiKit.ShowToast("The Barracks is not built yet.", ElarionUiKit.ToastTone.Danger);
                return;
            }
            var panel = BarracksPanelVM.ResolveOrCreateHost();
            if (panel != null) panel.Toggle();
        }

        /// <summary>Opens the panel if closed, closes it if open (hub-panel toggle).</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            FlowTrace.Step("Barracks", "BarracksPanel.Open - building the upgrade UI (kit chrome, no UXML).");
            Close();

            _ui = ElarionUiKit.BuildModalCanvas("BarracksPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Barracks - Upgrade",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), Close,
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "sword");

            var layout = chrome.layout;
            _listHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            var scroll = ElarionUiKit.MakeScrollZone(_listHost, RowGapPx, 6);
            _listContent = scroll != null ? scroll.content : null;

            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer : chrome.content.transform;
            _wallet = ElarionUiKit.BuildWalletRow(footHost, new[]
            {
                ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron,
                ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            });

            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            // WO-744: the VM resolves the economy handle itself (CreateDefault), owns the
            // BarracksService + economy subscriptions, and owns the upgrade commands. The View binds it.
            _vm = BarracksPanelVM.CreateDefault(Close);
            Bind(_vm);

            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("BarracksUpgrade", Close, () => IsOpen);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("Barracks", "BarracksPanel open rejected by PanelManager (battle-lock) — closed.");
                return;
            }

            Rebuild();
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as BarracksPanelVM;
            if (_vm == null) return;
            _vm.Changed += Rebuild;
            Rebuild();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Rebuild;
        }

        private void Rebuild()
        {
            if (_ui == null || _detailHost == null || _vm == null) return;

            UpdateWallet();

            // ── bodyLeft: barracks card + troop ladder ──
            var rowHost = _listContent != null ? (Transform)_listContent : _listHost;
            if (rowHost != null)
            {
                for (int i = rowHost.childCount - 1; i >= 0; i--) Destroy(rowHost.GetChild(i).gameObject);

                BuildBarracksRow(rowHost);
                Guard.TryEach("Barracks", "upgrade-row", _vm.TroopRows, r => BuildTroopRow(rowHost, r));
            }

            // ── bodyRight: detail ──
            for (int i = _detailHost.childCount - 1; i >= 0; i--) Destroy(_detailHost.GetChild(i).gameObject);
            if (_selectedId == BarracksPanelVM.BarracksSelId) BuildBarracksDetail(_vm.BarracksDetail);
            else BuildTroopDetail(_vm.TroopDetail(_selectedId), _selectedId);
        }

        // ── bodyLeft rows ──────────────────────────────────────────────────────

        private void BuildBarracksRow(Transform parent)
        {
            var vm = _vm.BarracksRow;
            bool selected = _selectedId == BarracksPanelVM.BarracksSelId;
            var row = MakeRowPlate(parent, "BarracksRow", selected, false);
            AddRowButton(row, () => { _selectedId = BarracksPanelVM.BarracksSelId; Rebuild(); }, selected);

            MakeText(row.transform, "Barracks", 15, selected ? ElarionUi.Gilt : ElarionUi.Parchment,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Vector2(0.06f, 0.50f), new Vector2(0.70f, 0.92f));
            MakeText(row.transform, "Level " + vm.Level + " / " + vm.Max, 12,
                new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.85f),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Vector2(0.06f, 0.10f), new Vector2(0.70f, 0.50f));

            MakeText(row.transform, vm.Chip, 12, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.MidlineRight, new Vector2(0.72f, 0.30f), new Vector2(0.96f, 0.70f));
        }

        private void BuildTroopRow(Transform parent, BarracksTroopRowVM def)
        {
            string id = def.Id;
            bool selected = _selectedId == id;
            bool unlocked = def.Unlocked;
            var row = MakeRowPlate(parent, "TroopRow_" + id, selected, !unlocked);
            AddRowButton(row, () => { _selectedId = id; Rebuild(); }, selected);
            float dim = unlocked ? 1f : 0.5f;

            Color nameCol = selected ? ElarionUi.Gilt : ElarionUi.Parchment;
            MakeText(row.transform, string.IsNullOrEmpty(def.Name) ? id : def.Name, 14,
                new Color(nameCol.r, nameCol.g, nameCol.b, dim), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Vector2(0.06f, 0.50f), new Vector2(0.66f, 0.92f));

            if (unlocked)
            {
                MakeText(row.transform, "Lv " + def.Level + " / " + def.MaxLevel, 12,
                    new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.85f),
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Vector2(0.06f, 0.10f), new Vector2(0.66f, 0.50f));
                string chip = def.Upgrading ? "..." : "";
                if (!string.IsNullOrEmpty(chip))
                    MakeText(row.transform, chip, 12, ElarionUi.Gilt, FontStyles.Bold,
                        TextAlignmentOptions.MidlineRight, new Vector2(0.70f, 0.30f), new Vector2(0.96f, 0.70f));
            }
            else
            {
                MakeText(row.transform, "L" + def.UnlockLevel + " LOCK", 12,
                    new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.9f),
                    FontStyles.Bold, TextAlignmentOptions.MidlineRight, new Vector2(0.66f, 0.28f), new Vector2(0.96f, 0.72f));
            }
        }

        private GameObject MakeRowPlate(Transform parent, string name, bool selected, bool locked)
        {
            var row = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx; le.minHeight = RowHeightPx;
            var plate = row.GetComponent<Image>();
            var slot = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, SlotTalentPlate);
            if (slot != null) { plate.sprite = slot; plate.type = Image.Type.Sliced; plate.fillCenter = true; }
            Color baseCol = selected ? RowSelected : RowUnlocked;
            if (locked)
                baseCol = new Color(baseCol.r * LockedTint.r, baseCol.g * LockedTint.g, baseCol.b * LockedTint.b, baseCol.a * LockedTint.a);
            plate.color = baseCol;
            return row;
        }

        private void AddRowButton(GameObject row, UnityEngine.Events.UnityAction onClick, bool selected)
        {
            var btn = row.GetComponent<Button>();
            btn.targetGraphic = row.GetComponent<Image>();
            ElarionUiKit.StyleButtonColors(btn);
            btn.onClick.AddListener(onClick);
            if (selected)
            {
                var bar = new GameObject("SelBar", typeof(Image));
                bar.transform.SetParent(row.transform, false);
                var brt = bar.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0.08f); brt.anchorMax = new Vector2(0.02f, 0.92f);
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                var bImg = bar.GetComponent<Image>();
                bImg.color = ElarionUi.Gilt; bImg.raycastTarget = false;
            }
        }

        // ── bodyRight detail ─────────────────────────────────────────────────────

        private void BuildBarracksDetail(BarracksDetailVM d)
        {
            MakeText(_detailHost, "Barracks", 20, Ink, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.99f));
            MakeText(_detailHost, "Level " + d.Level + " / " + d.Max, 14, InkDim, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.89f));

            if (d.HasNext)
            {
                MakeText(_detailHost, "Next: " + d.NextName, 15, Ink, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.79f));
                if (!string.IsNullOrEmpty(d.UnlocksNames))
                    MakeText(_detailHost, "Unlocks: " + d.UnlocksNames, 13, InkDim, FontStyles.Italic,
                        TextAlignmentOptions.Center, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.69f));

                MakeText(_detailHost, "Cost: " + d.CostText, 15, d.Affordable ? InkGood : InkBad, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.58f));
                MakeText(_detailHost, "Time: " + d.TimeText, 13, InkDim, FontStyles.Normal,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.49f));

                if (!d.CanUpgrade && !string.IsNullOrEmpty(d.BlockReason))
                    MakeText(_detailHost, d.BlockReason, 13, InkBad, FontStyles.Bold,
                        TextAlignmentOptions.Center, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.30f));

                var b = ElarionUiKit.BuildObsidianButton(_detailHost, "Upgrade Barracks",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    d.CanUpgrade ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.15f),
                    () => DoBarracksUpgrade());
                if (b != null) b.interactable = d.CanUpgrade;
            }
            else
            {
                MakeText(_detailHost, "The Barracks is at its maximum level.", 14, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.45f), new Vector2(0.94f, 0.55f));
            }
        }

        private void BuildTroopDetail(BarracksTroopDetailVM d, string troopId)
        {
            if (!d.Exists)
            {
                MakeText(_detailHost, "Select a troop.", 15, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.55f));
                return;
            }

            MakeText(_detailHost, string.IsNullOrEmpty(d.Name) ? troopId : d.Name, 20, Ink, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.99f));
            MakeText(_detailHost, d.Unlocked ? ("Level " + d.Level + " / " + d.MaxLevel) : "Locked", 14,
                d.Unlocked ? InkDim : InkBad, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.89f));

            // Reach + Strength bars (multipliers + fill fractions projected by the VM).
            BuildStatBar("Reach", d.ReachMult, d.ReachFill, BarReach, 0.70f);
            BuildStatBar("Strength", d.StrengthMult, d.StrengthFill, BarStrength, 0.60f);

            // Next ability (locked/unlocked).
            MakeText(_detailHost, d.NextAbilityText, 12,
                d.HasNextAbility ? InkDim : InkGood, FontStyles.Italic,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.56f));

            if (!d.Unlocked)
            {
                MakeText(_detailHost, "Unlocks at Barracks Level " + d.UnlockLevel + ".", 13, InkBad,
                    FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.30f));
                return;
            }

            if (d.HasNextLevel)
            {
                MakeText(_detailHost, "Cost: " + d.CostText, 15, d.Affordable ? InkGood : InkBad, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.44f));
                MakeText(_detailHost, "Time: " + d.TimeText, 13, InkDim, FontStyles.Normal,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.36f));
                if (!d.CanUpgrade && !string.IsNullOrEmpty(d.BlockReason))
                    MakeText(_detailHost, d.BlockReason, 12, InkBad, FontStyles.Bold,
                        TextAlignmentOptions.Center, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.28f));

                var b = ElarionUiKit.BuildObsidianButton(_detailHost, "Upgrade",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    d.CanUpgrade ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.15f),
                    () => DoTroopUpgrade(troopId));
                if (b != null) b.interactable = d.CanUpgrade;
            }
            else
            {
                MakeText(_detailHost, "This troop is fully upgraded.", 14, InkGood, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.40f));
            }
        }

        private void BuildStatBar(string label, float mult, float fill01, Color fillCol, float yCenter)
        {
            float y0 = yCenter - 0.035f, y1 = yCenter + 0.035f;
            MakeText(_detailHost, label + "  x" + mult.ToString("0.00"), 12, Ink, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Vector2(0.06f, y1 + 0.005f), new Vector2(0.94f, y1 + 0.045f));

            var track = new GameObject("BarTrack_" + label, typeof(Image));
            track.transform.SetParent(_detailHost, false);
            var trt = track.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.06f, y0); trt.anchorMax = new Vector2(0.94f, y1);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tImg = track.GetComponent<Image>(); tImg.color = BarTrack; tImg.raycastTarget = false;

            var fill = new GameObject("BarFill_" + label, typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var fImg = fill.GetComponent<Image>(); fImg.color = fillCol; fImg.raycastTarget = false;
        }

        private void DoBarracksUpgrade()
        {
            if (_vm == null) return;
            var r = _vm.UpgradeBarracks();
            if (r.Success)
                ElarionUiKit.ShowToast("Barracks upgrade started.", ElarionUiKit.ToastTone.Confirm);
            else
                ElarionUiKit.ShowToast(r.FailReason ?? "Can't upgrade the Barracks.", ElarionUiKit.ToastTone.Danger);
        }

        private void DoTroopUpgrade(string troopId)
        {
            if (_vm == null) return;
            var r = _vm.UpgradeTroop(troopId);
            if (r.Success)
                ElarionUiKit.ShowToast("Troop upgrade started.", ElarionUiKit.ToastTone.Confirm);
            else
                ElarionUiKit.ShowToast(r.FailReason ?? "Can't upgrade this troop.", ElarionUiKit.ToastTone.Danger);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void UpdateWallet()
        {
            if (_wallet == null || _wallet.Length < 4 || _vm == null) return;
            if (_wallet[0] != null) _wallet[0].SetAmount(_vm.Wood);
            if (_wallet[1] != null) _wallet[1].SetAmount(_vm.Iron);
            if (_wallet[2] != null) _wallet[2].SetAmount(_vm.Food);
            if (_wallet[3] != null) _wallet[3].SetAmount(_vm.Crystals);
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
            t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }

        public void Close()
        {
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _wallet = null; _listHost = null; _listContent = null; _detailHost = null;
            if (_ui != null) ElarionUiKit.ClosePanelWithFx(_ui);
            _ui = null;
        }

        private void OnDestroy()
        {
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            Unbind();
            _vm?.Dispose();
            _vm = null;
            if (_ui != null) Destroy(_ui);
        }
    }
}
