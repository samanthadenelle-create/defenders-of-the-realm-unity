// =============================================================================
// TroopTrainingPanel — the Barracks "train troops" UI (WO-453 troop-training flow).
// Code-built uGUI (NO UXML), routed through the SHARED presentation kit
// (DeNelle.Core.UI.ElarionUiKit) so it reads as the SAME designed game as the town
// HUD + the ShopPanel store: dark-glass panels + gold-rune frames, scroll list.
// -----------------------------------------------------------------------------
// MIRRORS ShopPanel: BuildModalCanvas (sortingOrder 31000 + overrideSorting, above
// the world-HUD band) + tap-outside Scrim + a framed dark-glass panel + a header,
// then a VerticalLayoutGroup + ContentSizeFitter + per-row LayoutElement scroll list
// (the proven anti-collapse rendering mechanism — do NOT revert it) with FinalizeScroll
// forcing the layout pass on the build frame.
//
// One row per TroopCatalog.All troop: name + owned count (ArmyStorage) + cost +
// "Train x1" / "Train x5" buttons. An army-cap indicator (SlotsUsed / MaxArmySize)
// sits under the header. Train is disabled per-row when the cap is full OR the cost
// is unaffordable. Training routes through TroopDialogueCommands.TrainOne (which wires
// the ArmyStorage seam delegates to TroopCatalog + EconomyService), then re-builds.
//
// Open()/Close() API — opened by TroopDialogueCommands.ShowTrainingUI (the
// <<ShowTrainingUI>> Yarn command), which self-heals a host if none exists.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.State;

namespace DeNelle.Village.Hero
{
    public sealed class TroopTrainingPanel : MonoBehaviour
    {
        private GameObject _ui;
        private GameObject _contentRoot;
        private RectTransform _scrollContent;
        private TMPro.TextMeshProUGUI _statusText;
        private TMPro.TextMeshProUGUI _capText;
        private TMPro.TextMeshProUGUI _ecoText;
        private System.Action<ResourceSnapshot> _ecoHandler;

        // Fixed PIXEL height per row (the ShopPanel rendering fix: rows are laid out by a
        // VerticalLayoutGroup at a fixed LayoutElement height, content auto-grows to fit).
        private const float RowHeightPx = 64f;
        private const float RowGapPx    = 3f;

        public void Open()
        {
            Close();

            // Modal canvas + tap-outside scrim, both from the shared kit. Pin sortingOrder
            // 31000 + overrideSorting (mirrors ShopPanel) so the panel + its scrim render
            // ABOVE the world-HUD band but below the true top overlays.
            _ui = ElarionUiKit.BuildModalCanvas("TroopTrainingPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.92f),
                                                   deep: true, packSpriteName: null);
            var panel = panelGo.transform;

            ElarionUiKit.Header(panel, "Barracks — Train", x0: 0.04f, x1: 0.96f, y0: 0.9f, y1: 0.97f);

            // Live economy readout (gold ink, mirrors the store).
            CreateEconomyReadout(panel);

            // Army-cap indicator (SlotsUsed / MaxArmySize).
            CreateCapReadout(panel);

            // Close (X) — top-right ornate pack-frame danger button.
            ElarionUiKit.ButtonPack(panel, "X", ElarionUiKit.ButtonKind.Danger,
                                    new Vector2(0.9f, 0.9f), new Vector2(0.985f, 0.985f), Close);

            // Content area (the scroll list lives here).
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.02f, 0.08f);
            cr.anchorMax = new Vector2(0.98f, 0.78f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            // Status line.
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.02f, 0.01f);
            sRect.anchorMax = new Vector2(0.98f, 0.07f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;
            SetStatus("Train troops to defend Elarion and raid enemy camps.");

            Rebuild();

            Debug.Log("[TroopTrainingPanel] Opened — barracks troop training.");
        }

        private void CreateEconomyReadout(Transform parent)
        {
            var go = new GameObject("EcoReadout", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.855f);
            r.anchorMax = new Vector2(0.98f, 0.9f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
            _ecoText = t;
            UpdateEcoText();
            if (EconomyService.Instance != null)
            {
                _ecoHandler = _ => UpdateEcoText();
                EconomyService.Instance.OnChanged += _ecoHandler;
            }
        }

        private void CreateCapReadout(Transform parent)
        {
            var go = new GameObject("CapReadout", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.805f);
            r.anchorMax = new Vector2(0.98f, 0.85f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.Parchment;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
            _capText = t;
            UpdateCapText();
        }

        private void UpdateEcoText()
        {
            if (_ecoText == null || EconomyService.Instance == null) return;
            var e = EconomyService.Instance;
            _ecoText.text = $"Wood: {e.Wood}   Iron: {e.Iron}   Food: {e.Food}   Crystals: {e.Crystals}";
        }

        private void UpdateCapText()
        {
            if (_capText == null) return;
            var army = Army();
            if (army == null) { _capText.text = "Army: —"; return; }
            int used = army.SlotsUsed(TroopDialogueCommands.SlotOf);
            _capText.text = $"Army: {used} / {army.MaxArmySize} slots used";
        }

        // The persisted army roster (GameState.Army), null when no save service is live.
        private static ArmyStorage Army()
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.State != null ? svc.State.Army : null;
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        // Re-build the troop list + the cap/economy readouts after every train.
        private void Rebuild()
        {
            ClearContent();
            UpdateEcoText();
            UpdateCapText();

            var army = Army();

            // One row per catalog troop (Footman, Archer). Count first so the scroll
            // content sizes to fit them all (the ShopPanel mechanism).
            var troops = new List<TroopDef>();
            foreach (var d in TroopCatalog.All) if (d != null) troops.Add(d);

            var listRoot = BuildScrollContent(troops.Count);
            foreach (var d in troops)
                CreateTroopRow(listRoot, d, army);

            FinalizeScroll();
        }

        // A troop row: dark-glass Cell tile (LayoutElement-sized for the scroll layout),
        // name + owned count, cost, and Train x1 / Train x5 buttons. Train buttons are
        // disabled when the army cap is full or the player can't afford the troop.
        private void CreateTroopRow(Transform parent, TroopDef def, ArmyStorage army)
        {
            var row = new GameObject("TroopRow_" + def.Id, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);

            int owned = OwnedCount(army, def.Id);
            string name = string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName;

            ElarionUiKit.Label(row.transform, $"{name}  (owned {owned})", 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.04f, 0.55f);

            // Cost tinted by affordability (gold-green affordable, danger-red not).
            var cost = CostOf(def);
            bool affordable = EconomyService.Instance == null || EconomyService.Instance.CanAfford(cost);
            bool hasRoom = army == null || army.CanTrain(def.Id, TroopDialogueCommands.SlotOf);
            bool canTrain = affordable && hasRoom;

            Color priceColor = EconomyService.Instance == null ? ElarionUi.Gilt
                             : (affordable ? ElarionUi.Affordable : ElarionUi.Danger);
            ElarionUiKit.Label(row.transform, CostString(def), 0.15f, 0.85f, priceColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.04f, 0.28f, bold: true);

            // Train x1 (primary CTA).
            var b1 = ElarionUiKit.ButtonPack(row.transform, "Train", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.62f, 0.18f), new Vector2(0.79f, 0.82f), () => TrainAndRefresh(def.Id, 1));
            if (b1 != null) b1.interactable = canTrain;

            // Train x5 (batch).
            var b5 = ElarionUiKit.ButtonPack(row.transform, "x5", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.81f, 0.18f), new Vector2(0.97f, 0.82f), () => TrainAndRefresh(def.Id, 5));
            if (b5 != null) b5.interactable = canTrain;
        }

        private void TrainAndRefresh(string troopId, int qty)
        {
            int trained = TroopDialogueCommands.Train(troopId, qty);
            var def = TroopCatalog.Find(troopId);
            string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : troopId;
            if (trained > 0)
            {
                SetStatus($"Trained {trained}x {name}.");
                // Push the fresh economy snapshot to the town HUD too (mirrors ShopPanel).
                var eco = EconomyService.Instance;
                if (eco != null)
                    DeNelle.Core.CoreServices.Hud?.SetResources(eco.Wood, eco.Iron, eco.Food, eco.Crystals);
                GameStateService.Instance?.Save();
            }
            else
            {
                SetStatus($"Couldn't train {name} — army cap full or not enough resources.");
            }
            Rebuild();
        }

        private static int OwnedCount(ArmyStorage army, string troopId)
        {
            if (army == null || army.Owned == null) return 0;
            int n = 0;
            foreach (var t in army.Owned)
                if (t != null && t.TroopDefId == troopId) n++;
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

        // --- Scroll list (the ShopPanel anti-collapse rendering mechanism) ---

        private Transform BuildScrollContent(int rowCount)
        {
            var well = ElarionUiKit.Well(_contentRoot.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null) wImg.raycastTarget = false;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible; ScrollRect needs a raycast graphic

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f); // height driven by the ContentSizeFitter

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(3, 3, 3, 3);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _scrollContent = cr;
            return content.transform;
        }

        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            Canvas.ForceUpdateCanvases();
            var contentArea = _contentRoot != null ? _contentRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        private void ClearContent()
        {
            _scrollContent = null;
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        public void Close()
        {
            if (_ecoHandler != null && EconomyService.Instance != null)
                EconomyService.Instance.OnChanged -= _ecoHandler;
            _ecoHandler = null;
            _ecoText = null;
            _capText = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _scrollContent = null;
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}
