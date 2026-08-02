// =============================================================================
// EchoCardView -- the Echo select card (dumb skin; MVVM strict; WO-681 card,
// WO-830 per-Echo harvest RESOURCE PICKER).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small Obsidian modal (master factory: ElarionUiKit.BuildObsidianModal +
// FrameCore -- UI_BLINK_TEMPLATE_CANON SS2, ONE shared Close) that introduces an
// Echo (real name/element/flavor/portrait from EchoRosterCatalog) and hosts the
// WO-830 RESOURCE PICKER: five chips (Wood/Iron/Food/Gold/Crystals --
// EchoAssignments.PickableResources). The dead Crafting chip is REMOVED and
// Defense/Exploration stay hidden (owner rulings 2026-07-24 + 2026-08-02). The
// card also shows the DISCLOSED pair-synergy status line (SynergyText); nothing
// on this card ever discloses the hidden tri-synergy (WO-830 Sec.3d). The View
// reads NOTHING from services -- every string/state/sprite comes from EchoCardVM;
// the only outbound call is vm.AssignResource on a chip tap. PanelManager-registered
// (one modal at a time; battle-lock respected -- a rejected open never shows a half-card).
//
// Layout mirrors EchoWorkforceHud's fraction-in-content approach (same kit, same
// chrome family, code-built uGUI -- NO UXML, PIPELINE_STATE S8). The vertical
// resource picker keeps its bottom edge above the shared Close band (the WO-555
// clearance lesson documented in EchoWorkforceHud.Build). With FIVE chips offered
// the modal is TALLER than the old two-lane card (0.10-0.90 vs 0.30-0.80).
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
    /// The Echo select card view: name/portrait socket + WHAT (element + affinity) +
    /// live STATE + synergy status + the "What should this Echo gather?" resource-picker
    /// rows. Dumb skin over <see cref="EchoCardVM"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoCardView : MonoBehaviour
    {
        private GameObject _modal;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _whatLabel;
        private TextMeshProUGUI _stateLabel;
        private TextMeshProUGUI _synergyLabel;
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
            // Centred; MODAL band above the roster (31000). WO-830: taller than the old
            // two-lane card -- the five-resource picker needs the vertical room while every
            // chip stays >= the mobile touch floor.
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoCard", "ECHO",
                new Vector2(0.22f, 0.10f), new Vector2(0.78f, 0.90f),
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

            // Body bands (0..1 of body well only -- no cross-stack, every band DISJOINT):
            //   portrait + name   0.86-0.98
            //   what (element+affinity) 0.79-0.85   single short line
            //   state             0.72-0.78   single line
            //   synergy status    0.65-0.71   single line (WO-830 disclosed pair line)
            //   ask               0.59-0.64   single line
            //   resource picker   0.05-0.57   five chips, above Close (body already reserved)
            var portraitSprite = Guard.Try("Echo", "load echo portrait",
                () => _vm != null ? _vm.Portrait : null, fallback: null);
            if (portraitSprite != null)
            {
                var pg = new GameObject("EchoPortrait", typeof(Image));
                pg.transform.SetParent(body, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.04f, 0.86f);
                prt.anchorMax = new Vector2(0.20f, 0.98f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                _portrait = pg.GetComponent<Image>();
                _portrait.sprite = portraitSprite;
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
            }

            _nameLabel = ElarionUiKit.Label(body, "", 0.87f, 0.97f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Center,
                0.22f, 0.96f, bold: true);
            ElarionUiKit.FitSingleLine(_nameLabel);

            // Element + "Favors: <resource>" -- the affinity disclosed in TEXT (WO-830).
            _whatLabel = ElarionUiKit.Label(body, "", 0.79f, 0.85f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(_whatLabel);

            _stateLabel = ElarionUiKit.Label(body, "", 0.72f, 0.78f,
                new Color(0.85f, 0.85f, 0.9f, 1f), ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: true);
            ElarionUiKit.FitSingleLine(_stateLabel);

            // WO-830: the DISCLOSED pair-synergy status ("Provisions synergy ... ACTIVE").
            _synergyLabel = ElarionUiKit.Label(body, "", 0.65f, 0.71f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(_synergyLabel);

            _askLabel = ElarionUiKit.Label(body, "", 0.59f, 0.64f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(_askLabel);

            var rowGo = new GameObject("ResourcePicker", typeof(RectTransform));
            rowGo.transform.SetParent(body, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.06f, 0.05f);
            rowRt.anchorMax = new Vector2(0.94f, 0.57f);
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
            if (_synergyLabel != null)
            {
                _synergyLabel.text = _vm.SynergyText;
                ElarionUiKit.FitSingleLine(_synergyLabel);
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

            var chips = _vm.ResourceChips();
            int n = Mathf.Max(1, chips.Length);
            int index = 0;
            // Guard.TryEach: one bad chip logs + skips, never blanks the picker (SS12.2).
            Guard.TryEach("Echo", "build resource chip", chips, chip =>
            {
                int i = index++;
                // Vertical stack: row 0 at the top, each row a full-width slice of the host.
                float rowH = 1f / n;
                float gap = 0.015f;
                float y1 = 1f - i * rowH;
                float y0 = y1 - rowH + gap;

                // Row container holds the tappable resource button + an affinity note under it.
                var rowGo = new GameObject("ResourceRow_" + chip.Id, typeof(RectTransform));
                rowGo.transform.SetParent(_chipRow, false);
                var rrt = rowGo.GetComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0f, y0);
                rrt.anchorMax = new Vector2(1f, y1);
                rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
                _chips.Add(rowGo);

                string resId = chip.Id;   // capture for the closure
                bool hasNote = !string.IsNullOrEmpty(chip.Note);
                // Button fills the top band (whole row when there is no note).
                float btnBottom = hasNote ? 0.42f : 0f;

                // Selected resource = Gold face (plus the "(now)" TEXT cue -- never hue alone).
                var kind = chip.Selected ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet;
                var btn = ElarionUiKit.Button(rowGo.transform, chip.Label, kind,
                    new Vector2(0f, btnBottom), new Vector2(1f, 1f),
                    () => OnChipTapped(resId));

                // Affinity note UNDER the button -- single line (never wraps into Close).
                if (hasNote)
                {
                    var note = ElarionUiKit.Label(rowGo.transform, chip.Note, 0f, 0.38f,
                        chip.Preferred ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                        ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
                    ElarionUiKit.FitSingleLine(note);
                }
            });
        }

        private void OnChipTapped(string resourceId)
        {
            FlowTrace.Step("Echo", $"Card: resource chip tapped '{resourceId}'.");
            _vm?.AssignResource(resourceId);
            // VM raises Changed via the seam -> Refresh re-binds STATE + selected chip.
        }
    }
}
