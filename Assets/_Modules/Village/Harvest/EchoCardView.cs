// =============================================================================
// EchoCardView -- the WO-681 Echo select card (dumb skin; MVVM strict).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small Obsidian modal (master factory: ElarionUiKit.BuildObsidianModal +
// FrameCore -- UI_BLINK_TEMPLATE_CANON SS2, ONE shared Close) that introduces an
// Echo (real name/element/flavor/portrait from EchoRosterCatalog) and hosts the
// WO-738 functional lane picker (harvest/crafting/defense/exploration). The View
// reads NOTHING from services -- every string/state/sprite comes from EchoCardVM;
// the only outbound call is vm.AssignLane on a lane tap. PanelManager-registered
// (one modal at a time; battle-lock respected -- a rejected open never shows a half-card).
//
// Layout mirrors EchoWorkforceHud's fraction-in-content approach (same kit, same
// chrome family, code-built uGUI -- NO UXML, PIPELINE_STATE S8). The vertical lane
// picker keeps its bottom edge above the shared Close band (>= 0.25, the WO-555
// clearance lesson documented in EchoWorkforceHud.Build). Defense + Exploration lanes
// carry an HONEST "passive - active in raids/dungeons" note (TEXT-carried, never hue).
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
            // Whole modal in one call: canvas + scrim (tap-outside closes) + Obsidian
            // chrome + ONE shared Close. Compact card, centred; sorting mirrors the
            // Echo Harvest panel (above gameplay HUD, below the battle overlay).
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoCard", "ECHO",
                new Vector2(0.28f, 0.24f), new Vector2(0.72f, 0.76f),
                onClose: Close, sortingOrder: 4600,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;
            var content = built.chrome.content.transform;

            // Portrait socket (sprite-first, null-fallback -- the VM owns the roster-catalog load;
            // absent art just skips the image so the card is never blank).
            var portraitSprite = Guard.Try("Echo", "load echo portrait",
                () => _vm != null ? _vm.Portrait : null, fallback: null);
            if (portraitSprite != null)
            {
                var pg = new GameObject("EchoPortrait", typeof(Image));
                pg.transform.SetParent(content, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.06f, 0.80f);
                prt.anchorMax = new Vector2(0.22f, 0.93f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                _portrait = pg.GetComponent<Image>();
                _portrait.sprite = portraitSprite;
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
            }

            // Name line (gilt header weight), offset right of the portrait socket.
            _nameLabel = ElarionUiKit.Label(content, "", 0.885f, 0.945f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Center,
                0.24f, 0.94f, bold: true);
            ElarionUiKit.FitSingleLine(_nameLabel);

            // WHAT line -- the spirit's element + authored flavor (wraps).
            _whatLabel = ElarionUiKit.Label(content, "", 0.71f, 0.86f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: false);
            _whatLabel.textWrappingMode = TextWrappingModes.Normal;   // project TMP API (not the obsolete enableWordWrapping)

            // STATE line -- live lane/level/bonus as TEXT (colorblind-safe; no hue-only cue).
            _stateLabel = ElarionUiKit.Label(content, "", 0.635f, 0.70f,
                new Color(0.85f, 0.85f, 0.9f, 1f), ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: true);
            ElarionUiKit.FitSingleLine(_stateLabel);

            // The one ask.
            _askLabel = ElarionUiKit.Label(content, "", 0.585f, 0.63f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: false);

            // Lane-picker list host (transparent layout host -- no chrome of its own, SS6).
            // Vertical stack of the four functional lanes; bottom >= 0.25 clears the shared Close band.
            var rowGo = new GameObject("LanePicker", typeof(RectTransform));
            rowGo.transform.SetParent(content, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.08f, 0.26f);
            rowRt.anchorMax = new Vector2(0.92f, 0.575f);
            rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;
            _chipRow = rowGo.transform;
        }

        // ── refresh (VM -> View, one direction) ───────────────────────────────

        private void Refresh()
        {
            if (_vm == null || _modal == null || !_open) return;
            if (_nameLabel != null) _nameLabel.text = _vm.NameText;
            if (_whatLabel != null) _whatLabel.text = _vm.WhatText;
            if (_stateLabel != null) _stateLabel.text = _vm.StateText;
            if (_askLabel != null) _askLabel.text = _vm.AskText;
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

                // Honesty / preferred note UNDER the button (TEXT-carried; Defense/Exploration say
                // "passive - active in raids/dungeons" so the player is never misled -- never hue alone).
                if (hasNote)
                {
                    var note = ElarionUiKit.Label(rowGo.transform, chip.Note, 0f, 0.40f,
                        chip.Preferred ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                        ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: false);
                    note.textWrappingMode = TextWrappingModes.Normal;
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
