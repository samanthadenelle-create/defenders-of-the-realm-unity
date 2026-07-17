// =============================================================================
// TroopTrainingPanel — the Barracks "train troops" UI (WO-453 troop-training flow).
// A DUMB SKIN over the SHARED kit chrome: it INHERITS BuildObsidianPanel
// (FrameCrafting master-detail + zones + the ONE shared Close) and only DISPLAYS +
// routes commands. ALL logic (catalog, cost, cap, train) lives in the services it
// CALLS (TroopCatalog / ArmyStorage / EconomyService / TroopDialogueCommands) — the
// panel defines none of it.
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03): the old frameless dark-glass scroll list -> the
// owner-ratified FrameCrafting MASTER-DETAIL template (matches VillageCraftingPanel /
// CraftingPanelMvvm / JewelerPanelMvvm):
//   * bodyLeft  (dark well)      = troop rows (Obsidian buttons, selected=Yellow)
//   * bodyRight (parchment well) = the selected troop's detail in dark INK (owned,
//                                  army cap, cost, feedback) + the Train x1 / x5 CTAs
//   * footer    (action strip)   = the live economy readout (wood/iron/food/crystals)
// The ONE shared Close is the chrome's (no per-panel X / close_normal / bespoke close).
// Mobile-first: compact rows in the narrow left well, centered detail + compact CTAs.
//
// Code-built uGUI (NO UXML — §8). Open()/Close() API — opened by
// TroopDialogueCommands.ShowTrainingUI (the <<ShowTrainingUI>> command), which
// self-heals a host if none exists.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    public sealed class TroopTrainingPanel : MonoBehaviour
    {
        private GameObject _ui;
        private Transform _troopHost;    // bodyLeft — dark list well
        private Transform _detailHost;   // bodyRight — parchment detail well
        // WO-714 P2: the footer wallet is a row of kit CurrencyChips — the ONE currency
        // read (chip owns CompactNumber/icon/tag; no hand-formatted wallet string ever).
        private ElarionUiKit.CurrencyChipHandle[] _wallet;
        private System.Action<ResourceSnapshot> _ecoHandler;

        private string _selectedTroopId;
        // Static instruction (never mutates — transient train feedback is a kit toast, P5).
        private const string DetailHint = "Train troops to defend Elarion and raid enemy camps.";

        // Dark ink for text sitting ON the parchment detail well (family convention).
        private static readonly Color Ink     = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color InkDim  = new Color(0.34f, 0.28f, 0.20f, 1f);
        private static readonly Color InkGood = new Color(0.10f, 0.42f, 0.16f, 1f);
        private static readonly Color InkBad  = new Color(0.55f, 0.12f, 0.10f, 1f);

        public void Open()
        {
            // WO-724: instrument the train-UI open path (acceptance #5). The panel is only
            // reachable once the Barracks is unlocked (ff.barracks + founding-complete).
            FlowTrace.Step("Barracks", "TroopTrainingPanel.Open - building the train UI (kit chrome, no UXML).");

            Close();

            // Modal canvas + tap-outside scrim, both from the shared kit. Pin sortingOrder
            // 31000 + overrideSorting so the panel + its scrim render ABOVE the world-HUD band.
            _ui = ElarionUiKit.BuildModalCanvas("TroopTrainingPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // SHARED Obsidian chrome (FrameCrafting master-detail): black panel + gold trim +
            // gold header + medallion + the ONE shared Close — all built by the kit. The panel
            // adds NO chrome and NO close of its own.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Barracks — Train",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), Close,
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "sword");

            var layout = chrome.layout;
            _troopHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            // WO-714 P2: the footer wallet = the ONE kit wallet strip (CurrencyChip rows —
            // icon + tag + CompactNumber owned by the chip; no hand-formatted string).
            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer : chrome.content.transform;
            _wallet = ElarionUiKit.BuildWalletRow(footHost, new[]
            {
                ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron,
                ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            });

            // WO-714 P8: the ONE shared open ease (scale target = the panel rect).
            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            // The economy readout tracks the wallet live (unchanged seam; presentation refresh).
            if (EconomyService.Instance != null)
            {
                _ecoHandler = _ => Rebuild();
                EconomyService.Instance.OnChanged += _ecoHandler;
            }

            Rebuild();

            Debug.Log("[TroopTrainingPanel] Opened — barracks troop training.");
        }

        // The persisted army roster (GameState.Army), null when no save service is live.
        private static ArmyStorage Army()
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.State != null ? svc.State.Army : null;
        }

        // Re-project the whole master-detail from the live services after every train.
        private void Rebuild()
        {
            if (_troopHost == null || _detailHost == null) return;

            UpdateWallet();

            var army = Army();

            var troops = new List<TroopDef>();
            foreach (var d in TroopCatalog.All) if (d != null) troops.Add(d);

            // Keep the selection valid (first troop by default).
            if (troops.Count > 0)
            {
                bool found = false;
                foreach (var d in troops) if (d.Id == _selectedTroopId) { found = true; break; }
                if (!found) _selectedTroopId = troops[0].Id;
            }

            // Troop rows (dark well, left).
            for (int i = _troopHost.childCount - 1; i >= 0; i--)
                Destroy(_troopHost.GetChild(i).gameObject);

            if (troops.Count == 0)
            {
                MakeText(_troopHost, "No troops available.", 13, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.60f));
            }
            else
            {
                const float rowH = 0.115f, gap = 0.02f;
                float top = 0.98f;
                foreach (var d in troops)
                {
                    string id = d.Id;
                    bool selected = id == _selectedTroopId;
                    // WO-714 P10: a raw troop id is never player-visible.
                    string name = string.IsNullOrEmpty(d.DisplayName)
                        ? ElarionUiKit.SpacedDisplayName(d.Id) : d.DisplayName;
                    ElarionUiKit.BuildObsidianButton(_troopHost, name,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => { _selectedTroopId = id; Rebuild(); });
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
                }
            }

            // Detail (parchment well, right — dark ink).
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);

            var def = TroopCatalog.Find(_selectedTroopId);
            if (def != null) BuildDetail(def, army);
            else
                MakeText(_detailHost, "Select a troop.", 15, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.55f));
        }

        private void BuildDetail(TroopDef def, ArmyStorage army)
        {
            // WO-714 P10: a raw troop id is never player-visible.
            string name = string.IsNullOrEmpty(def.DisplayName)
                ? ElarionUiKit.SpacedDisplayName(def.Id) : def.DisplayName;
            int owned = OwnedCount(army, def.Id);

            // Title (ink, bold).
            MakeText(_detailHost, name, 20, Ink, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.99f));

            // Owned count.
            MakeText(_detailHost, "Owned:  " + owned, 14, InkDim, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.87f));

            // WO-724: wounded/recovering readout - wounded troops are blocked from deploy
            // (PlayerTroop.IsDeployable == !Wounded, consumed by ArmyStorage.GetDeployable);
            // surface how many of this type are recovering so the state is readable, not silent.
            int woundedOfType = WoundedCount(army, def.Id);
            if (woundedOfType > 0)
                MakeText(_detailHost, "Recovering:  " + woundedOfType, 13, InkBad, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.08f, 0.755f), new Vector2(0.92f, 0.81f));

            // Army-cap indicator (SlotsUsed / MaxArmySize).
            string capLine;
            if (army == null) capLine = "Army:  —";
            else capLine = $"Army:  {army.SlotsUsed(TroopDialogueCommands.SlotOf)} / {army.MaxArmySize} slots used";
            MakeText(_detailHost, capLine, 13, InkDim, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.79f));

            // Cost, tinted by affordability.
            var cost = CostOf(def);
            bool affordable = EconomyService.Instance == null || EconomyService.Instance.CanAfford(cost);
            bool hasRoom = army == null || army.CanTrain(def.Id, TroopDialogueCommands.SlotOf);
            bool canTrain = affordable && hasRoom;

            Color costColor = EconomyService.Instance == null ? InkDim : (affordable ? InkGood : InkBad);
            MakeText(_detailHost, "Cost:  " + CostString(def), 15, costColor, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.70f));

            // Static instruction line (WO-714 P5: transient train feedback is a kit toast —
            // no mutable status label that can go stale).
            MakeText(_detailHost, DetailHint, 13, InkDim, FontStyles.Italic,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.28f));

            // Train x1 + x5 — compact, centered CTAs (mobile-first), disabled when the cap is
            // full or the cost is unaffordable.
            var b1 = ElarionUiKit.BuildObsidianButton(_detailHost, "Train",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canTrain ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.10f, 0.03f), new Vector2(0.52f, 0.14f),
                () => TrainAndRefresh(def.Id, 1));
            if (b1 != null) b1.interactable = canTrain;

            var b5 = ElarionUiKit.BuildObsidianButton(_detailHost, "Train x5",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canTrain ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.56f, 0.03f), new Vector2(0.90f, 0.14f),
                () => TrainAndRefresh(def.Id, 5));
            if (b5 != null) b5.interactable = canTrain;
        }

        // WO-714 P2: amounts flow through the chips' SetAmount (count-tween; CompactNumber
        // formatting lives inside the chip — WO-697 law, currency-ellipsis forbidden).
        private void UpdateWallet()
        {
            if (_wallet == null || _wallet.Length < 4 || EconomyService.Instance == null) return;
            var e = EconomyService.Instance;
            if (_wallet[0] != null) _wallet[0].SetAmount(e.Wood);
            if (_wallet[1] != null) _wallet[1].SetAmount(e.Iron);
            if (_wallet[2] != null) _wallet[2].SetAmount(e.Food);
            if (_wallet[3] != null) _wallet[3].SetAmount(e.Crystals);
        }

        private void TrainAndRefresh(string troopId, int qty)
        {
            int trained = TroopDialogueCommands.Train(troopId, qty);
            var def = TroopCatalog.Find(troopId);
            // WO-714 P10: never toast a raw troop id.
            string name = def != null && !string.IsNullOrEmpty(def.DisplayName)
                ? def.DisplayName : ElarionUiKit.SpacedDisplayName(troopId);
            if (trained > 0)
            {
                // WO-714 P5: transient feedback through the ONE kit toast, never a stuck label.
                ElarionUiKit.ShowToast($"Trained {trained}x {name}.", ElarionUiKit.ToastTone.Confirm);
                // Push the fresh economy snapshot to the town HUD too (mirrors ShopPanel).
                var eco = EconomyService.Instance;
                if (eco != null)
                    DeNelle.Core.CoreServices.Hud?.SetResources(eco.Wood, eco.Iron, eco.Food, eco.Crystals);
                GameStateService.Instance?.Save();
            }
            else
            {
                ElarionUiKit.ShowToast($"Couldn't train {name} - army cap full or not enough resources.",
                    ElarionUiKit.ToastTone.Danger);
            }
            Rebuild();   // re-project owned counts / cap / affordability after the attempt
        }

        private static int OwnedCount(ArmyStorage army, string troopId)
        {
            if (army == null || army.Owned == null) return 0;
            int n = 0;
            foreach (var t in army.Owned)
                if (t != null && t.TroopDefId == troopId) n++;
            return n;
        }

        // WO-724: count owned troops of this type currently wounded (recovering) - blocked
        // from deploy until ArmyStorage.TickRecovery clears the flag.
        private static int WoundedCount(ArmyStorage army, string troopId)
        {
            if (army == null || army.Owned == null) return 0;
            int n = 0;
            foreach (var t in army.Owned)
                if (t != null && t.TroopDefId == troopId && t.Wounded) n++;
            return n;
        }

        private static ResourceCost CostOf(TroopDef def)
        {
            // ResourceCost ctor order is (wood, food, iron, crystals).
            return def == null ? new ResourceCost() : new ResourceCost(def.CostWood, def.CostFood, def.CostIron);
        }

        private string CostString(TroopDef def)
        {
            if (def == null) return "Free";
            var parts = new List<string>();
            if (def.CostWood > 0) parts.Add(def.CostWood + "W");
            if (def.CostIron > 0) parts.Add(def.CostIron + "I");
            if (def.CostFood > 0) parts.Add(def.CostFood + "F");
            return parts.Count == 0 ? "Free" : string.Join(" ", parts);
        }

        // ── uGUI helper (mirrors VillageCraftingPanel.MakeText) ───────────────────

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

        public void Close()
        {
            if (_ecoHandler != null && EconomyService.Instance != null)
                EconomyService.Instance.OnChanged -= _ecoHandler;
            _ecoHandler = null;
            _wallet = null;
            _troopHost = null;
            _detailHost = null;
            // WO-714 P8: eased fade/scale-out through the ONE kit FX (falls back to an
            // immediate Destroy when the FX is absent / not playing).
            if (_ui != null) ElarionUiKit.ClosePanelWithFx(_ui);
            _ui = null;
        }

        private void OnDestroy()
        {
            if (_ecoHandler != null && EconomyService.Instance != null)
                EconomyService.Instance.OnChanged -= _ecoHandler;
            _ecoHandler = null;
            if (_ui != null) Destroy(_ui);
        }
    }
}
