// =============================================================================
// EchoCardView -- the WO-681 Echo select card (dumb skin; MVVM strict).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small Obsidian modal (master factory: ElarionUiKit.BuildObsidianModal +
// FrameCore -- UI_BLINK_TEMPLATE_CANON SS2, ONE shared Close) that introduces an
// Echo (real name/element/flavor/portrait from EchoRosterCatalog) and hosts the
// functional lane picker. Only the LIVE lanes are offered -- Harvest + Crafting
// (EchoAssignments.PickableLanes); Defense + Exploration are not shown because their
// unlock is not designed (owner ruling 2026-07-24; no stub/teaser rows). The View
// reads NOTHING from services -- every string/state/sprite comes from EchoCardVM;
// the only outbound call is vm.AssignLane on a lane tap. PanelManager-registered
// (one modal at a time; battle-lock respected -- a rejected open never shows a half-card).
//
// Layout mirrors EchoWorkforceHud's fraction-in-content approach (same kit, same
// chrome family, code-built uGUI -- NO UXML, PIPELINE_STATE S8). The vertical lane
// picker keeps its bottom edge above the shared Close band (>= 0.25, the WO-555
// clearance lesson documented in EchoWorkforceHud.Build). With only the two live lanes
// offered, the modal height is shrunk to hug that content (no dead black space).
//
// Reached via a TAP on an OWNED roster card (EchoRosterView) -> EchoCard.Open(echoIndex).
// Singleton view host on a DDOL GameObject.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>Static opener for the Echo select card (WO-681). Lazily creates the
    /// singleton view host; safe to call from any Village code.</summary>
    public static class EchoCard
    {
        private static EchoCardView s_view;

        /// <summary>Open the card for the Echo at <paramref name="echoIndex"/>.</summary>
        public static void Open(int echoIndex)
        {
            if (s_view == null)
            {
                var go = new GameObject("EchoCard");
                Object.DontDestroyOnLoad(go);
                s_view = go.AddComponent<EchoCardView>();
            }
            s_view.OpenFor(echoIndex);
        }
    }

    /// <summary>
    /// The Echo select card view: name/portrait socket + WHAT + live STATE + the
    /// "What should you gather?" lane-picker row. Dumb skin over <see cref="EchoCardVM"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoCardView : MonoBehaviour
    {
        private GameObject _modal;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _whatLabel;
        private TextMeshProUGUI _stateLabel;
        private TextMeshProUGUI _askLabel;
        private Transform _chipRow;
        private Image _portrait;
        private readonly List<GameObject> _chips = new List<GameObject>();

        private EchoCardVM _vm;
        private PanelHandle _panelHandle;
        private bool _open;

        // ── open / close ──────────────────────────────────────────────────────

        /// <summary>Open (or re-target) the card for one Echo.</summary>
        public void OpenFor(int echoIndex)
        {
            using var _t = FlowTrace.Enter("Echo", "CardOpen");

            // Rebind the VM to the tapped Echo (a card may be re-opened for another wisp).
            if (_vm != null) { _vm.Changed -= Refresh; _vm.Dispose(); }
            _vm = new EchoCardVM(echoIndex);
            _vm.Changed += Refresh;

            if (_modal == null) Build();
            if (_modal == null)
            {
                FlowTrace.Fail("Echo", "CardOpen: modal failed to build -- card not shown.");
                return;
            }

            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("EchoCard", Close, () => _open);

            _open = true;
            _modal.SetActive(true);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                // Battle-lock (WO-437): the arbiter rejected the open and already
                // invoked Close -- never show a half-open card.
                FlowTrace.Warn("Echo", "CardOpen rejected by PanelManager (battle-lock).");
                return;
            }
            Refresh();
            FlowTrace.Step("Echo", $"Card OPEN for echo {echoIndex}.");
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            if (_modal != null) _modal.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            FlowTrace.Step("Echo", "Card CLOSED.");
        }

        private void OnDestroy()
        {
            if (_vm != null) { _vm.Changed -= Refresh; _vm.Dispose(); _vm = null; }
        }

        // ── build (master factory; no per-screen chrome) ──────────────────────

        private void Build()
        {
            // Same law as EchoRosterView (owner F8 2026-07-24): parent into layout.body so
            // labels never paint over the FrameCore title plate or the shared Close.
            // Compact card, centred; MODAL band above the roster (31000). Height shrunk to
            // hug the two-live-lane content (owner ruling 2026-07-24) so it no longer floats
            // in dead black space.
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoCard", "ECHO",
                new Vector2(0.22f, 0.30f), new Vector2(0.78f, 0.80f),
                onClose: Close, sortingOrder: 31010,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;

            if (built.chrome.title != null)
            {
                built.chrome.title.text = "ECHO";
                ElarionUiKit.FitSingleLine(built.chrome.title);
            }

            Transform body = built.chrome.layout != null && built.chrome.layout.body != null
                ? built.chrome.layout.body
                : built.chrome.content.transform;

            // Body bands (0..1 of body well only -- no cross-stack):
            //   portrait + name   0.82-0.98
            //   what (element)    0.74-0.80   single short line
            //   state             0.66-0.72   single line
            //   ask               0.58-0.64   single line
            //   lane picker       0.06-0.54   two live lanes, above Close (body already reserved)
            var portraitSprite = Guard.Try("Echo", "load echo portrait",
                () => _vm != null ? _vm.Portrait : null, fallback: null);
            if (portraitSprite != null)
            {
                var pg = new GameObject("EchoPortrait", typeof(Image));
                pg.transform.SetParent(body, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.04f, 0.82f);
                prt.anchorMax = new Vector2(0.20f, 0.98f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                _portrait = pg.GetComponent<Image>();
                _portrait.sprite = portraitSprite;
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
            }

            _nameLabel = ElarionUiKit.Label(body, "", 0.84f, 0.97f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Center,
                0.22f, 0.96f, bold: true);
            ElarionUiKit.FitSingleLine(_nameLabel);

            // Element only -- full Flavor was painting the entire card (F8 capture).
            _whatLabel = ElarionUiKit.Label(body, "", 0.74f, 0.81f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(_whatLabel);

            _stateLabel = ElarionUiKit.Label(body, "", 0.66f, 0.73f,
                new Color(0.85f, 0.85f, 0.9f, 1f), ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: true);
            ElarionUiKit.FitSingleLine(_stateLabel);

            _askLabel = ElarionUiKit.Label(body, "", 0.58f, 0.65f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(_askLabel);

            var rowGo = new GameObject("LanePicker", typeof(RectTransform));
            rowGo.transform.SetParent(body, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.06f, 0.06f);
            rowRt.anchorMax = new Vector2(0.94f, 0.54f);
            rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;
            _chipRow = rowGo.transform;
        }

        // ── refresh (VM -> View, one direction) ───────────────────────────────

        private void Refresh()
        {
            if (_vm == null || _modal == null || !_open) return;
            if (_nameLabel != null)
            {
                _nameLabel.text = _vm.NameText;
                ElarionUiKit.FitSingleLine(_nameLabel);
            }
            if (_whatLabel != null)
            {
                _whatLabel.text = _vm.WhatText;
                ElarionUiKit.FitSingleLine(_whatLabel);
            }
            if (_stateLabel != null)
            {
                _stateLabel.text = _vm.StateText;
                ElarionUiKit.FitSingleLine(_stateLabel);
            }
            if (_askLabel != null)
            {
                _askLabel.text = _vm.AskText;
                ElarionUiKit.FitSingleLine(_askLabel);
            }
            // Portrait can change if the card is re-opened for another Echo.
            if (_portrait != null && _vm.Portrait != null)
                _portrait.sprite = _vm.Portrait;
            RebuildChips();
        }

        private void RebuildChips()
        {
            if (_chipRow == null) return;
            for (int i = _chips.Count - 1; i >= 0; i--)
                if (_chips[i] != null) Destroy(_chips[i]);
            _chips.Clear();

            var chips = _vm.LaneChips();
            int n = Mathf.Max(1, chips.Length);
            int index = 0;
            // Guard.TryEach: one bad chip logs + skips, never blanks the picker (SS12.2).
            Guard.TryEach("Echo", "build lane chip", chips, chip =>
            {
                int i = index++;
                // Vertical stack: row 0 at the top, each row a full-width slice of the host.
                float rowH = 1f / n;
                float gap = 0.03f;
                float y1 = 1f - i * rowH;
                float y0 = y1 - rowH + gap;

                // Row container holds the tappable lane button + an honesty/preferred note under it.
                var rowGo = new GameObject("LaneRow_" + chip.Id, typeof(RectTransform));
                rowGo.transform.SetParent(_chipRow, false);
                var rrt = rowGo.GetComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0f, y0);
                rrt.anchorMax = new Vector2(1f, y1);
                rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
                _chips.Add(rowGo);

                string laneId = chip.Id;   // capture for the closure
                bool hasNote = !string.IsNullOrEmpty(chip.Note);
                // Button fills the top band (whole row when there is no note).
                float btnBottom = hasNote ? 0.42f : 0f;

                // Selected lane = Gold (plus the "(now)" TEXT cue -- never hue alone).
                var kind = chip.Selected ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet;
                var btn = ElarionUiKit.Button(rowGo.transform, chip.Label, kind,
                    new Vector2(0f, btnBottom), new Vector2(1f, 1f),
                    () => OnChipTapped(laneId));

                // Honesty / preferred note UNDER the button -- single line (was wrapping into Close).
                if (hasNote)
                {
                    var note = ElarionUiKit.Label(rowGo.transform, chip.Note, 0f, 0.38f,
                        chip.Preferred ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                        ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
                    ElarionUiKit.FitSingleLine(note);
                }
            });
        }

        private void OnChipTapped(string laneId)
        {
            FlowTrace.Step("Echo", $"Card: lane chip tapped '{laneId}'.");
            _vm?.AssignLane(laneId);
            // VM raises Changed via the seam -> Refresh re-binds STATE + selected chip.
        }
    }
}
