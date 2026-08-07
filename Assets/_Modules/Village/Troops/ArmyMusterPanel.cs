// =============================================================================
// ArmyMusterPanel — the "Armies" surface (WO-897): compose an army, see its total
// cost + total time, and MUSTER it onto the existing Train queue in one action.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// CODE-BUILT uGUI over ElarionUiKit. NO UXML — UXML does not work in builds (canon §8).
//
// Layout (kit master-detail chrome, FrameCrafting):
//   bodyLeft  (dark well)      = scrollable ladder of UNLOCKED troops; each row is
//                                name / per-unit cost + time / a [-] N [+] stepper.
//   bodyRight (parchment well) = the composition readout: the rows, TOTAL COST,
//                                TOTAL TIME (parallel-aware), the Train-line state
//                                ("Queue: 2 of 5 used"), and the LAST MUSTER RESULT.
//   footer                     = the wallet row + the Muster CTA.
//
// COLOURBLIND-SAFE (owner is red/green colourblind): every state is carried by TEXT
// and COUNTS - "Queued 3 of 5 - 2 did not fit." - never by hue alone.
// ASCII ONLY in every visible string ("-" and "...", never an em-dash or ellipsis glyph).
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using Ledger = DeNelle.Village.Buildings.Progression;   // the GameState-backed wallet the spend charges

namespace DeNelle.Village
{
    /// <summary>
    /// The Armies panel: build an <see cref="ArmyComposition"/> and muster it onto the Train
    /// channel with one button. Opened via <see cref="Show"/> (self-heals its own host).
    /// </summary>
    public sealed class ArmyMusterPanel : MonoBehaviour
    {
        // The session's working composition (see ArmyComposition's header: not persisted yet).
        private static readonly ArmyComposition s_composition = new ArmyComposition { Name = "New Army" };

        private GameObject _ui;
        private RectTransform _listContent;
        private Transform _detailHost;
        private Button _musterCta;
        private TextMeshProUGUI _musterCtaLabel;
        private ElarionUiKit.CurrencyChipHandle[] _wallet;
        private PanelHandle _panelHandle;

        // The last muster's player-facing text, held so the readout survives a repaint.
        private string _lastResultHeadline = "";
        private string _lastResultDetail = "";

        private const float RowHeightPx = 112f;   // MinTouchPx floor - the stepper buttons live here
        private const float RowGapPx = 8f;

        private static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color RowPlate = new Color(0.16f, 0.16f, 0.18f, 0.92f);

        public bool IsOpen => _ui != null;

        // ── Entry point ───────────────────────────────────────────────────────

        /// <summary>
        /// Opens the Armies panel, self-healing a host if none exists (mirrors
        /// TroopDialogueCommands.ShowTrainingUI's verb pattern). Refuses with a toast while the
        /// Barracks is locked - there is nothing to muster before it exists.
        /// </summary>
        public static void Show()
        {
            if (!BarracksUnlock.IsUnlocked)
            {
                FlowTrace.Step("Muster", "ArmyMusterPanel.Show refused - the Barracks is not built yet.");
                ElarionUiKit.ShowToast("The Barracks is not built yet.", ElarionUiKit.ToastTone.Danger);
                return;
            }
            var panel = Object.FindAnyObjectByType<ArmyMusterPanel>();
            if (panel == null) panel = new GameObject("ArmyMusterPanelHost").AddComponent<ArmyMusterPanel>();
            panel.Open();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Open()
        {
            FlowTrace.Step("Muster", "ArmyMusterPanel.Open - building the armies UI (kit chrome, no UXML).");
            Close();

            _ui = ElarionUiKit.BuildModalCanvas("ArmyMusterPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Armies - Muster",
                new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.93f), Close,
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

            Transform footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer : chrome.content.transform;

            _wallet = ElarionUiKit.BuildWalletRow(footHost, new[]
            {
                ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron,
                ElarionUiKit.CurrencyKind.Food,
            });

            // The ONE action. Canonical CTA size (132px height floor) + touch clamp from the kit.
            _musterCta = ElarionUiKit.Button(_detailHost, "Muster army", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.12f), OnMuster);
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
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _listContent = null;
            _detailHost = null;
            _musterCta = null;
            _musterCtaLabel = null;
        }

        private void OnDestroy()
        {
            BarracksService.Changed -= Rebuild;
            ArmyMusterService.Mustered -= Rebuild;
        }

        // ── The one action ────────────────────────────────────────────────────

        private void OnMuster()
        {
            var report = ArmyMusterService.Muster(s_composition);
            _lastResultHeadline = report.Headline;
            _lastResultDetail = report.Detail;

            // Tone is a SECOND cue only - the counts are in the text, per the colourblind rule.
            var tone = report.Complete ? ElarionUiKit.ToastTone.Confirm
                     : report.AnyQueued ? ElarionUiKit.ToastTone.Gold
                     : ElarionUiKit.ToastTone.Danger;
            ElarionUiKit.ShowToast(report.Summary, tone, 3.2f);

            // Rows that fully queued leave the composition; a shortfall stays so the player can
            // retry it once the queue drains (never silently cleared, never silently kept whole).
            foreach (var r in report.Rows)
                if (r.Queued > 0) s_composition.Add(r.TroopId, -r.Queued);

            Rebuild();
        }

        // ── Render ────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_ui == null || _detailHost == null) return;

            UpdateWallet();
            BuildTroopLadder();
            BuildComposition();
            UpdateCta();
        }

        private void UpdateWallet()
        {
            if (_wallet == null || _wallet.Length < 3) return;
            _wallet[0]?.SetAmount(Ledger.ResourceLedger.Balance(Ledger.HarvestResource.Wood));
            _wallet[1]?.SetAmount(Ledger.ResourceLedger.Balance(Ledger.HarvestResource.Iron));
            _wallet[2]?.SetAmount(Ledger.ResourceLedger.Balance(Ledger.HarvestResource.Food));
        }

        private void BuildTroopLadder()
        {
            Transform host = _listContent != null ? (Transform)_listContent : null;
            if (host == null) return;

            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);

            // Only UNLOCKED troops are offered (WO-897 AC 3) - a locked troop cannot be mustered.
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

        /// <summary>One offered troop: name / per-unit cost + time on the left, a [-] N [+] stepper right.</summary>
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
            var nameLabel = ElarionUiKit.Label(row.transform, name, 0.52f, 0.94f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0.04f, 0.62f, bold: true);
            nameLabel.raycastTarget = false;

            var costLabel = ElarionUiKit.Label(row.transform, PerUnitLine(def), 0.08f, 0.48f,
                ElarionUi.ParchmentDim, ElarionUi.FontMicro, TextAlignmentOptions.MidlineLeft, 0.04f, 0.62f);
            costLabel.raycastTarget = false;

            // Stepper. The COUNT is text, the buttons are labelled "-" / "+" (ASCII), so the row
            // never relies on a colour to say how many are staged.
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
            s_composition.Add(troopId, delta);
            Rebuild();
        }

        /// <summary>"25 Wood, 10 Iron - 45s each" (ASCII, no glyphs).</summary>
        private static string PerUnitLine(TroopDef def)
        {
            var cost = new ArmyCost { Wood = def.CostWood, Iron = def.CostIron, Food = def.CostFood };
            return cost.ToString() + " - " + ArmyMusterPlanner.FormatDuration(def.BuildSeconds) + " each";
        }

        /// <summary>The parchment readout: composition rows, totals, queue-line state, last result.</summary>
        private void BuildComposition()
        {
            if (_detailHost == null) return;

            // Clear everything EXCEPT the CTA (it is a persistent child of this zone).
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
            {
                var child = _detailHost.GetChild(i).gameObject;
                if (_musterCta != null && child == _musterCta.gameObject) continue;
                Destroy(child);
            }

            var preview = ArmyMusterService.Preview(s_composition);
            var body = new System.Text.StringBuilder();

            body.Append("ARMY: ").Append(s_composition.Name).Append('\n');
            if (preview.TotalUnits <= 0)
            {
                body.Append("\nNo troops staged yet.\nUse [+] on the left to add troops,\nthen Muster army to queue them all at once.");
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
                body.Append("\nTotal cost: ").Append(preview.Cost).Append('\n');
                body.Append("Total time: ").Append(ArmyMusterPlanner.FormatDuration(preview.TotalSeconds))
                    .Append(" (").Append(preview.TrainSlots).Append(" slot")
                    .Append(preview.TrainSlots == 1 ? "" : "s").Append(")\n");
                if (!preview.Affordable)
                    body.Append("Short of: ").Append(preview.ShortOf).Append('\n');
            }

            // The queue line, always in TEXT with both numbers - never a colour or a bar alone.
            body.Append("\nTraining queue: ").Append(preview.LineDepth).Append(" of ")
                .Append(ArmyMusterPlanner.TrainQueueDepthCap).Append(" used, ")
                .Append(preview.LineRoom).Append(" free.\n");
            if (preview.WouldNotFit > 0)
                body.Append("Only ").Append(preview.WouldFit).Append(" of ").Append(preview.TotalUnits)
                    .Append(" will fit right now - ").Append(preview.WouldNotFit)
                    .Append(" will be left staged.\n");

            if (!string.IsNullOrEmpty(_lastResultHeadline))
            {
                body.Append("\nLAST MUSTER\n").Append(_lastResultHeadline).Append('\n');
                if (!string.IsNullOrEmpty(_lastResultDetail)) body.Append(_lastResultDetail).Append('\n');
            }

            var text = ElarionUiKit.Label(_detailHost, body.ToString(), 0.14f, 0.97f,
                Ink, ElarionUi.FontLabel, TextAlignmentOptions.TopLeft, 0.05f, 0.95f);
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            ElarionUiKit.FitBlock(text, 30f, ElarionUi.FontLabel);   // font floor 30 (mobile legibility)
        }

        /// <summary>
        /// The CTA reflects the LIVE Train queue (WO-897 §1): what it will queue now, and how many
        /// trainings are already in flight. Text-only state - no colour-only tell.
        /// </summary>
        private void UpdateCta()
        {
            if (_musterCta == null) return;
            var preview = ArmyMusterService.Preview(s_composition);

            string label;
            bool interactable;
            if (preview.TotalUnits <= 0)
            {
                label = preview.LineDepth > 0
                    ? "Mustering - " + preview.LineDepth + " in queue"
                    : "Add troops to muster";
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
                    ? "Muster " + preview.TotalUnits + " (" + preview.LineDepth + " in queue)"
                    : "Muster army - " + preview.TotalUnits;
                interactable = true;
            }

            _musterCta.interactable = interactable;
            if (_musterCtaLabel != null) _musterCtaLabel.text = label;
        }
    }
}
