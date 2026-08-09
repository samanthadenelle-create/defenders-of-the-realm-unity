// =============================================================================
// ArmyMusterPanel — Armies loadout bank + one-tap muster (WO-897 + WO-934).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village
//
// CODE-BUILT uGUI (no UXML). Colourblind-safe TEXT state. ASCII only.
//
// Player loop (fun + value):
//   1. Pick one of 3 saved loadout slots (Raid Push / Wall Hold / Siege Prep)
//   2. Quick-fill a recipe OR step troops with [+] / [-]
//   3. Save to slot (persists)  OR  Muster army (auto-queues Train jobs)
//   4. Watch Obsidian Train queue fill — army prepares while you play
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    public sealed class ArmyMusterPanel : MonoBehaviour
    {
        private static readonly ArmyComposition s_composition = new ArmyComposition { Name = "Raid Push" };

        private GameObject _ui;
        private RectTransform _listContent;
        private Transform _detailHost;
        private Transform _footerHost;
        private Button _musterCta;
        private TextMeshProUGUI _musterCtaLabel;
        private ElarionUiKit.CurrencyChipHandle[] _wallet;
        private PanelHandle _panelHandle;

        private string _lastResultHeadline = "";
        private string _lastResultDetail = "";
        private int _activeSlot;

        private const float RowHeightPx = 112f;
        private const float RowGapPx = 8f;
        private static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color RowPlate = new Color(0.16f, 0.16f, 0.18f, 0.92f);

        public bool IsOpen => _ui != null;
        private static ArmyMusterPanel s_host;

        public static void Show()
        {
            if (!BarracksUnlock.IsUnlocked)
            {
                FlowTrace.Step("Muster", "ArmyMusterPanel.Show refused - the Barracks is not built yet.");
                ElarionUiKit.ShowToast("The Barracks is not built yet.", ElarionUiKit.ToastTone.Danger);
                return;
            }
            if (s_host == null) s_host = new GameObject("ArmyMusterPanelHost").AddComponent<ArmyMusterPanel>();
            s_host.Open();
        }

        public void Open()
        {
            FlowTrace.Step("Muster", "ArmyMusterPanel.Open - loadout bank + muster UI.");
            Close();

            // Hydrate working set from the active saved slot (or seed a fun recipe).
            var army = ArmyLoadoutService.Ensure();
            _activeSlot = army != null ? army.ActiveLoadoutIndex : 0;
            ArmyLoadoutService.LoadInto(_activeSlot, s_composition);
            if (s_composition.TotalUnits <= 0 && string.IsNullOrEmpty(_lastResultHeadline))
            {
                // First open with empty slot: stage Raid Push so the panel never feels blank.
                ArmyLoadoutService.ApplyRecipe(s_composition, 0);
            }

            _ui = ElarionUiKit.BuildModalCanvas("ArmyMusterPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Armies - Loadouts",
                new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f), Close,
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "sword");

            var layout = chrome.layout;
            Transform listHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            var scroll = ElarionUiKit.MakeScrollZone(listHost, RowGapPx, 6);
            _listContent = scroll != null ? scroll.content : null;

            _footerHost = layout != null && layout.footer != null
                ? (Transform)layout.footer : chrome.content.transform;

            _wallet = ElarionUiKit.BuildWalletRow(_footerHost, new[]
            {
                ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron,
                ElarionUiKit.CurrencyKind.Food,
            });

            // Primary CTA pinned low on the detail well.
            _musterCta = ElarionUiKit.Button(_detailHost, "Muster army", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.11f), OnMuster);
            if (_musterCta != null)
            {
                ElarionUiKit.PinCanonicalCtaSize(_musterCta);
                ElarionUiKit.ClampMinTouch(_musterCta);
                _musterCtaLabel = _musterCta.GetComponentInChildren<TextMeshProUGUI>();
            }

            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            BarracksService.Changed += Rebuild;
            ArmyMusterService.Mustered += Rebuild;

            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Armies", Close, () => IsOpen);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("Muster", "ArmyMusterPanel open rejected by PanelManager (battle-lock) - closed.");
                return;
            }

            Rebuild();
        }

        public void Close()
        {
            BarracksService.Changed -= Rebuild;
            ArmyMusterService.Mustered -= Rebuild;

            if (_ui != null && _panelHandle != null)
                PanelManager.NotifyClosed(_panelHandle);

            if (_ui != null) Destroy(_ui);
            _ui = null;
            _listContent = null;
            _detailHost = null;
            _footerHost = null;
            _musterCta = null;
            _musterCtaLabel = null;
        }

        private void OnDestroy()
        {
            BarracksService.Changed -= Rebuild;
            ArmyMusterService.Mustered -= Rebuild;
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnMuster()
        {
            // Auto-save active slot so muster never loses a staged plan.
            ArmyLoadoutService.SaveFrom(_activeSlot, s_composition);

            var report = ArmyMusterService.Muster(s_composition);
            _lastResultHeadline = report.Headline;
            _lastResultDetail = report.Detail;

            var tone = report.Complete ? ElarionUiKit.ToastTone.Confirm
                     : report.AnyQueued ? ElarionUiKit.ToastTone.Gold
                     : ElarionUiKit.ToastTone.Danger;
            ElarionUiKit.ShowToast(report.Summary, tone, 3.2f);

            foreach (var r in report.Rows)
                if (r.Queued > 0) s_composition.Add(r.TroopId, -r.Queued);

            // Keep the saved slot as the full plan; working set drops what queued.
            Rebuild();
        }

        private void OnSaveSlot()
        {
            ArmyLoadoutService.SaveFrom(_activeSlot, s_composition);
            ElarionUiKit.ShowToast(
                "Saved '" + s_composition.Name + "' to slot " + (_activeSlot + 1) + ".",
                ElarionUiKit.ToastTone.Confirm);
            Rebuild();
        }

        private void OnSelectSlot(int index)
        {
            if (index == _activeSlot)
            {
                // Re-tap reloads saved version (discard unsaved edits).
                ArmyLoadoutService.LoadInto(index, s_composition);
                ElarionUiKit.ShowToast("Reloaded " + ArmyLoadoutService.SlotName(index) + ".", ElarionUiKit.ToastTone.Info);
            }
            else
            {
                // Auto-save previous working set into the slot we leave (never lose work).
                ArmyLoadoutService.SaveFrom(_activeSlot, s_composition);
                _activeSlot = index;
                ArmyLoadoutService.LoadInto(index, s_composition);
                ElarionUiKit.ShowToast("Editing " + ArmyLoadoutService.SlotName(index) + ".", ElarionUiKit.ToastTone.Info);
            }
            Rebuild();
        }

        private void OnRecipe(int recipe)
        {
            string msg = ArmyLoadoutService.ApplyRecipe(s_composition, recipe);
            ElarionUiKit.ShowToast(msg, ElarionUiKit.ToastTone.Gold);
            Rebuild();
        }

        private void OnCycleName()
        {
            // Fun: cycle default names so the player can "feel" ownership without a soft keyboard.
            string[] names =
            {
                "Raid Push", "Wall Hold", "Siege Prep",
                "Night Watch", "Quick Strike", "Last Stand", "New Army",
            };
            string cur = s_composition.Name ?? "";
            int idx = 0;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], cur, System.StringComparison.OrdinalIgnoreCase))
                { idx = (i + 1) % names.Length; break; }
            s_composition.Name = names[idx];
            Rebuild();
        }

        // ── Render ────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_ui == null || _detailHost == null) return;
            UpdateWallet();
            BuildTroopLadder();
            BuildDetail();
            UpdateCta();
        }

        private void UpdateWallet()
        {
            if (_wallet == null || _wallet.Length < 3) return;
            var bal = ArmyMusterService.WalletBalances();
            _wallet[0]?.SetAmount(bal.Wood);
            _wallet[1]?.SetAmount(bal.Iron);
            _wallet[2]?.SetAmount(bal.Food);
        }

        private void BuildTroopLadder()
        {
            Transform host = _listContent != null ? (Transform)_listContent : null;
            if (host == null) return;

            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);

            var offered = new List<TroopDef>();
            var all = TroopCatalog.All;
            if (all != null)
                foreach (var def in all)
                    if (def != null && !string.IsNullOrEmpty(def.Id) && BarracksService.IsTroopUnlocked(def.Id))
                        offered.Add(def);

            if (offered.Count == 0)
            {
                var empty = ElarionUiKit.Label(host, "No troops unlocked yet - upgrade the Barracks.",
                    0f, 1f, ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center);
                var le = empty.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = RowHeightPx;
                le.minHeight = RowHeightPx;
                return;
            }

            Guard.TryEach("Muster", "troop-row", offered, def => BuildTroopRow(host, def));
        }

        private void BuildTroopRow(Transform parent, TroopDef def)
        {
            string id = def.Id;

            var row = new GameObject("MusterRow_" + id, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;

            var plate = row.GetComponent<Image>();
            var slot = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_talent_1");
            if (slot != null) { plate.sprite = slot; plate.type = Image.Type.Sliced; plate.fillCenter = true; }
            plate.color = RowPlate;

            string name = string.IsNullOrEmpty(def.DisplayName) ? id : def.DisplayName;
            // Cap note for siege maxOwned
            string capTag = def.MaxOwned == 1 ? " (max 1)" : "";
            var nameLabel = ElarionUiKit.Label(row.transform, name + capTag, 0.52f, 0.94f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0.04f, 0.62f, bold: true);
            nameLabel.raycastTarget = false;

            var costLabel = ElarionUiKit.Label(row.transform, PerUnitLine(def), 0.08f, 0.48f,
                ElarionUi.ParchmentDim, ElarionUi.FontMicro, TextAlignmentOptions.MidlineLeft, 0.04f, 0.62f);
            costLabel.raycastTarget = false;

            var minus = ElarionUiKit.Button(row.transform, "-", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.64f, 0.14f), new Vector2(0.745f, 0.86f), () => Step(id, -1));
            if (minus != null) ElarionUiKit.ClampMinTouch(minus);

            var count = ElarionUiKit.Label(row.transform, s_composition.CountOf(id).ToString(),
                0.14f, 0.86f, ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.755f, 0.845f, bold: true);
            count.raycastTarget = false;

            var plus = ElarionUiKit.Button(row.transform, "+", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.855f, 0.14f), new Vector2(0.96f, 0.86f), () => Step(id, 1));
            if (plus != null) ElarionUiKit.ClampMinTouch(plus);
        }

        private void Step(string troopId, int delta)
        {
            // Honour maxOwned in the composer so the UI can't stage 2 catapults.
            var def = TroopCatalog.Find(troopId);
            if (def != null && def.MaxOwned > 0 && delta > 0)
            {
                int want = s_composition.CountOf(troopId) + delta;
                if (want > def.MaxOwned)
                {
                    ElarionUiKit.ShowToast(
                        "Only " + def.MaxOwned + "x " +
                        (string.IsNullOrEmpty(def.DisplayName) ? troopId : def.DisplayName) +
                        " in a loadout.",
                        ElarionUiKit.ToastTone.Info);
                    return;
                }
            }
            s_composition.Add(troopId, delta);
            Rebuild();
        }

        private static string PerUnitLine(TroopDef def)
        {
            var cost = new ArmyCost { Wood = def.CostWood, Iron = def.CostIron, Food = def.CostFood };
            return cost.ToString() + " - " + ArmyMusterPlanner.FormatDuration(def.BuildSeconds) + " each";
        }

        private void BuildDetail()
        {
            if (_detailHost == null) return;

            for (int i = _detailHost.childCount - 1; i >= 0; i--)
            {
                var child = _detailHost.GetChild(i).gameObject;
                if (_musterCta != null && child == _musterCta.gameObject) continue;
                Destroy(child);
            }

            // ── Slot tabs (3 loadouts) ────────────────────────────────────────
            float tabW = 0.30f;
            float gap = 0.02f;
            for (int s = 0; s < ArmyLoadoutService.SlotCount; s++)
            {
                float x0 = 0.04f + s * (tabW + gap);
                float x1 = x0 + tabW;
                bool active = s == _activeSlot;
                int units = ArmyLoadoutService.SlotUnitCount(s);
                string label = (s + 1) + " " + ShortName(ArmyLoadoutService.SlotName(s));
                if (units > 0) label += " (" + units + ")";
                int capture = s;
                var kind = active ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet;
                var btn = ElarionUiKit.Button(_detailHost, label, kind,
                    new Vector2(x0, 0.90f), new Vector2(x1, 0.98f), () => OnSelectSlot(capture));
                if (btn != null) ElarionUiKit.ClampMinTouch(btn);
            }

            // ── Quick recipes ─────────────────────────────────────────────────
            ElarionUiKit.Button(_detailHost, "Raid", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.04f, 0.80f), new Vector2(0.27f, 0.88f), () => OnRecipe(0));
            ElarionUiKit.Button(_detailHost, "Hold", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.29f, 0.80f), new Vector2(0.52f, 0.88f), () => OnRecipe(1));
            ElarionUiKit.Button(_detailHost, "Siege", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.54f, 0.80f), new Vector2(0.77f, 0.88f), () => OnRecipe(2));
            ElarionUiKit.Button(_detailHost, "Clear", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.79f, 0.80f), new Vector2(0.96f, 0.88f), () => OnRecipe(3));

            // ── Name + save ───────────────────────────────────────────────────
            ElarionUiKit.Button(_detailHost, "Name: " + ShortName(s_composition.Name), ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.04f, 0.70f), new Vector2(0.62f, 0.78f), OnCycleName);
            ElarionUiKit.Button(_detailHost, "Save slot", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.64f, 0.70f), new Vector2(0.96f, 0.78f), OnSaveSlot);

            // ── Composition body ──────────────────────────────────────────────
            var preview = ArmyMusterService.Preview(s_composition);
            var body = new System.Text.StringBuilder();

            body.Append("STAGED: ").Append(s_composition.Name).Append("  (slot ")
                .Append(_activeSlot + 1).Append(")\n");

            if (preview.TotalUnits <= 0)
            {
                body.Append("\nEmpty plan.\n");
                body.Append("Tap Raid / Hold / Siege for a quick fill,\n");
                body.Append("or [+] troops on the left.\n");
                body.Append("Then Save slot and Muster army.\n");
            }
            else
            {
                foreach (var r in s_composition.Rows)
                {
                    if (r == null || r.Count <= 0) continue;
                    var def = TroopCatalog.Find(r.TroopId);
                    string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : r.TroopId;
                    body.Append("  ").Append(r.Count).Append("x ").Append(name).Append('\n');
                }
                body.Append("\nCost: ").Append(preview.Cost).Append('\n');
                body.Append("Time: ").Append(ArmyMusterPlanner.FormatDuration(preview.TotalSeconds))
                    .Append(" (").Append(preview.TrainSlots).Append(" train slot")
                    .Append(preview.TrainSlots == 1 ? "" : "s").Append(")\n");
                if (!preview.Affordable)
                    body.Append("Short of: ").Append(preview.ShortOf).Append('\n');
            }

            body.Append("\nTrain queue: ").Append(preview.LineDepth).Append(" of ")
                .Append(ArmyMusterPlanner.TrainQueueDepthCap).Append(" used, ")
                .Append(preview.LineRoom).Append(" free.\n");
            if (preview.WouldNotFit > 0)
                body.Append("Fits now: ").Append(preview.WouldFit).Append(" of ")
                    .Append(preview.TotalUnits).Append(" (rest stays staged).\n");

            if (!string.IsNullOrEmpty(_lastResultHeadline))
            {
                body.Append("\nLAST MUSTER\n").Append(_lastResultHeadline).Append('\n');
                if (!string.IsNullOrEmpty(_lastResultDetail)) body.Append(_lastResultDetail).Append('\n');
            }

            body.Append("\nTip: Muster auto-saves this slot. Fill the army, then Raids.");

            var text = ElarionUiKit.Label(_detailHost, body.ToString(), 0.13f, 0.68f,
                Ink, ElarionUi.FontLabel, TextAlignmentOptions.TopLeft, 0.05f, 0.95f);
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            ElarionUiKit.FitBlock(text, 28f, ElarionUi.FontLabel);
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Army";
            if (name.Length <= 12) return name;
            return name.Substring(0, 11) + ".";
        }

        private void UpdateCta()
        {
            if (_musterCta == null) return;
            var preview = ArmyMusterService.Preview(s_composition);

            string label;
            bool interactable;
            if (preview.TotalUnits <= 0)
            {
                label = preview.LineDepth > 0
                    ? "Queue busy - " + preview.LineDepth + " training"
                    : "Stage troops first";
                interactable = false;
            }
            else if (preview.LineRoom <= 0)
            {
                label = "Queue full - " + preview.LineDepth + " of " + ArmyMusterPlanner.TrainQueueDepthCap;
                interactable = false;
            }
            else if (preview.WouldNotFit > 0)
            {
                label = "Muster " + preview.WouldFit + " of " + preview.TotalUnits;
                interactable = true;
            }
            else
            {
                label = preview.LineDepth > 0
                    ? "Muster " + preview.TotalUnits + " (+" + preview.LineDepth + " queued)"
                    : "Muster " + preview.TotalUnits + " - auto train";
                interactable = true;
            }

            _musterCta.interactable = interactable;
            if (_musterCtaLabel != null) _musterCtaLabel.text = label;
        }
    }
}
