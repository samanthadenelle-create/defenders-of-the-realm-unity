// =============================================================================
// EchoCardView -- the Echo select card (dumb skin; MVVM strict; WO-681 card,
// WO-830 per-Echo harvest RESOURCE PICKER, WO-852 fixed-band layout).
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
// ---------------------------------------------------------------------------
// WO-852 LAYOUT LAW (this is the whole point of the rewrite -- do not regress it)
// ---------------------------------------------------------------------------
// The pre-WO-852 card stacked SIX fraction-anchored text bands plus a picker that
// sliced its host into 1/n equal fractions. Two compounding failures, both proven
// by arithmetic against the real FrameCore zones:
//
//  (1) TOUCH-FLOOR OVERFLOW. Each 1/n picker slice resolved to ~34 ref px. The kit
//      touch floor (ElarionUiKit.ClampMinTouch / UiKitMinTouchGuard) then grows a
//      sub-floor button SYMMETRICALLY ABOUT ITS CENTRE to MinTouchPx (112) -- i.e.
//      ~39 px UP and ~39 px DOWN, past the slice on BOTH sides. Every chip therefore
//      overlapped its neighbours AND the top chip climbed into the info text. The
//      LAST-built chip ("Crystals") is the last sibling, so it won every overlapping
//      raycast -- exactly the owner's "only the bottom chip is tappable".
//  (2) FRACTION TEXT BANDS. A fraction band scales with the pane and under-heights
//      the TMP line box, which silently culls/collides glyphs (WO-832 Sec.4 /
//      WO-841 / RumorBoard; CANON_GROUND_TRUTH 2026-08-02: "text bands must be
//      sized in fixed pixels >= the font's line height, never as a fraction of a
//      parent").
//
// THE FIX: every band below is a FIXED REFERENCE-PIXEL band derived from the kit
// constants (never a literal, never a parent fraction), and the picker is a kit
// scroll well (ElarionUiKit.MakeScrollZone) whose rows carry their own pixel height.
//
// WHY A SCROLL WELL AND NOT "just fit five chips": the modal cannot hold them.
// FrameCore's body zone, after the factory close-band reservation, resolves to
// ~475 ref px at 1920x1080 and ~418 ref px at 2340x1080 (the tighter aspect --
// CanvasScaler 1080x1920 @ match 0.5). Five chips at the MinTouchPx=112 floor need
// 560 px BEFORE gaps, info lines or the affinity note. 560 > 418. Shrinking below
// the touch floor is not an option (mobile-first), so the picker scrolls and ~3
// chips are visible at rest. To buy back every pixel available the modal was also
// raised 0.10-0.90 -> 0.05-0.95.
//
// The info block was moved OUT of the body well into the frame's OWN designed
// zones, which is what they exist for:
//   name    -> chrome.title      (the header plate)
//   portrait-> layout.medallion  (the circular socket; was an empty crest)
//   what    -> layout.subHeader  (WO-839 meta band)
//   state   -> layout.footer     (line 1)  -- the status strip, seated by the
//   synergy -> layout.footer     (line 2)  -- factory above the shared Close
//   ask + picker -> layout.body
// Each host is null-guarded: on the PROCEDURAL path (frame art absent) subHeader and
// medallion do not exist, so those lines fall back into the body's fixed-pixel stack
// and the picker well simply starts lower. Never a fraction, either way.
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
        // -- WO-852 FIXED REF-PIXEL LAYOUT CONSTANTS ---------------------------
        // Every value derives from a KIT constant so a kit change moves the card with
        // it and a sub-floor literal can never creep back in. `public const` so the
        // EchoCardLayoutRegression oracle can pin them without reflection.
        //
        // WHY FIXED PIXELS: WO-832 Sec.4 / WO-841 -- a fraction band scales with the
        // pane and under-heights the font's line box, and TMP then culls/clips the
        // glyphs with no error. A band sized as a whole TMP line box (~1.25em + 2px
        // slack) at the kit's readable floor always seats its text.

        /// <summary>One TMP line box at the kit's auto-size floor (ElarionUiKit.FontFloor).</summary>
        public const float FloorLinePx = ElarionUiKit.FontFloor * 1.25f + 2f;   // 39.5
        /// <summary>The "What should X gather?" ask -- one FontLabel line box.</summary>
        public const float AskBandPx = ElarionUi.FontLabel * 1.25f + 2f;        // 52
        /// <summary>Every picker chip button is EXACTLY the kit touch floor tall -- never less
        /// (mobile-first) and never a fraction (that is what let ClampMinTouch overflow).</summary>
        public const float ChipButtonPx = ElarionUiKit.MinTouchPx;              // 112
        /// <summary>The affinity note UNDER a chip -- its own floor line box, so it can never
        /// eat into the button's touch height.</summary>
        public const float ChipNotePx = FloorLinePx;                            // 39.5
        /// <summary>Gap between picker rows (kit scroll-zone spacing).</summary>
        public const float RowGapPx = 8f;
        /// <summary>Gap between stacked fixed bands.</summary>
        public const float BandGapPx = 8f;
        /// <summary>Inset from the body well's top/bottom edge.</summary>
        public const float BodyPadPx = 6f;
        /// <summary>Inset inside the kit scroll well.</summary>
        public const int ScrollPadPx = 4;

        private GameObject _modal;
        private TextMeshProUGUI _titleLabel;    // the frame's header plate carries the Echo NAME
        private TextMeshProUGUI _whatLabel;
        private TextMeshProUGUI _stateLabel;
        private TextMeshProUGUI _synergyLabel;
        private TextMeshProUGUI _askLabel;
        private Transform _chipRow;             // the kit scroll-well CONTENT column
        private Image _portrait;
        private readonly List<GameObject> _chips = new List<GameObject>();
        // Hash of the RENDERED chip state -- the picker rebuilds only when this moves
        // (EchoService raises Changed every frame while the silo fills; see RebuildChips).
        private string _lastChipSig;

        private EchoCardVM _vm;
        private PanelHandle _panelHandle;
        private bool _open;

        // -- open / close ------------------------------------------------------

        /// <summary>Open (or re-target) the card for one Echo.</summary>
        public void OpenFor(int echoIndex)
        {
            using var _t = FlowTrace.Enter("Echo", "CardOpen");

            // Rebind the VM to the tapped Echo (a card may be re-opened for another wisp).
            if (_vm != null) { _vm.Changed -= Refresh; _vm.Dispose(); }
            _vm = new EchoCardVM(echoIndex);
            _vm.Changed += Refresh;
            _lastChipSig = null;   // re-targeted card: force one picker rebuild

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

        // -- fixed-pixel band pins (the WO-832 Sec.4 / WO-841 pattern) ---------
        // Re-hang a control on its parent's TOP or BOTTOM edge with a FIXED ref-pixel
        // band. X anchors/offsets are preserved; only the vertical seat changes, so a
        // band never scales with the pane and never under-heights its line box again.

        private static void PinBandFromTop(RectTransform rt, float topPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -(topPx + heightPx));
            rt.offsetMax = new Vector2(rt.offsetMax.x, -topPx);
        }

        private static void PinBandFromBottom(RectTransform rt, float bottomPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomPx);
            rt.offsetMax = new Vector2(rt.offsetMax.x, bottomPx + heightPx);
        }

        // -- build (master factory; no per-screen chrome) ----------------------

        private void Build()
        {
            // Same law as EchoRosterView (owner F8 2026-07-24): parent into the frame's
            // drop-zones so labels never paint over the FrameCore title plate or the
            // shared Close. Centred; MODAL band above the roster (31000).
            // WO-852: raised 0.10-0.90 -> 0.05-0.95. The body well is the binding
            // constraint for a five-chip picker at MinTouchPx, so the card takes every
            // vertical pixel the frame will give it (the factory close-band reservation
            // still keeps the body above the shared Close).
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoCard", "ECHO",
                new Vector2(0.18f, 0.05f), new Vector2(0.82f, 0.95f),
                onClose: Close, sortingOrder: 31010,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;

            var layout = built.chrome.layout;

            // NAME -> the frame's HEADER PLATE. WO-852: this used to be an 82px
            // FontHead band inside the body well; the plate is the designed home for a
            // card's identity and reclaiming those pixels is what let the picker reach
            // the touch floor. FitSingleLine keeps it from clipping at any aspect.
            _titleLabel = built.chrome.title;

            Transform body = layout != null && layout.body != null
                ? (Transform)layout.body
                : built.chrome.content.transform;

            // PORTRAIT -> the medallion socket (it otherwise renders the generic crest).
            // Null on the PROCEDURAL path (frame art absent) -- then the card simply has
            // no portrait rather than stealing body pixels from the picker.
            var portraitSprite = Guard.Try("Echo", "load echo portrait",
                () => _vm != null ? _vm.Portrait : null, fallback: null);
            if (portraitSprite != null && layout != null && layout.medallion != null)
            {
                var pg = ElarionUiKit.AddImage(layout.medallion, "EchoPortrait",
                    new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), Color.white, rounded: false);
                _portrait = pg.GetComponent<Image>();
                _portrait.sprite = portraitSprite;
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
            }

            // Running FIXED-PIXEL cursor down the body well. It only advances when a
            // line has no designed zone to live in (procedural fallback) -- never a
            // fraction, so the picker's start is deterministic at every aspect.
            float bodyTopPx = BodyPadPx;

            // WHAT (element + "Favors: <resource>") -> the WO-839 SUB-HEADER meta band.
            // FIXED FloorLinePx band at the kit's readable floor size: the subHeader zone
            // resolves to ~45-50 ref px, which seats one floor line box (39.5) but NOT a
            // FontLabel one (52) -- sizing the band in pixels is what makes that safe
            // instead of silently clipped (WO-832 Sec.4 lesson).
            Transform whatHost = (layout != null && layout.subHeader != null)
                ? (Transform)layout.subHeader : body;
            _whatLabel = ElarionUiKit.Label(whatHost, "", 0f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontFloorMobile, TextAlignmentOptions.Center,
                0.02f, 0.98f, bold: false);
            ElarionUiKit.FitSingleLine(_whatLabel);
            if (whatHost == body)
            {
                PinBandFromTop(_whatLabel.rectTransform, bodyTopPx, FloorLinePx);
                bodyTopPx += FloorLinePx + BandGapPx;
            }
            else PinBandFromTop(_whatLabel.rectTransform, 0f, FloorLinePx);

            // STATE + SYNERGY -> the frame's FOOTER strip (a status bar, seated above the
            // shared Close by the factory's sweep-9413 relocation). Two FIXED floor line
            // boxes: 39.5 + 6 + 39.5 = 85 px inside a ~115-126 px band.
            Transform statusHost = (layout != null && layout.footer != null)
                ? (Transform)layout.footer : body;

            _stateLabel = ElarionUiKit.Label(statusHost, "", 0f, 1f,
                new Color(0.85f, 0.85f, 0.9f, 1f), (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(_stateLabel);

            // WO-830: the DISCLOSED pair-synergy status ("Provisions synergy ... ACTIVE").
            _synergyLabel = ElarionUiKit.Label(statusHost, "", 0f, 1f,
                ElarionUi.Gilt, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.02f, 0.98f, bold: false);
            ElarionUiKit.FitSingleLine(_synergyLabel);

            if (statusHost == body)
            {
                PinBandFromTop(_stateLabel.rectTransform, bodyTopPx, FloorLinePx);
                bodyTopPx += FloorLinePx + BandGapPx;
                PinBandFromTop(_synergyLabel.rectTransform, bodyTopPx, FloorLinePx);
                bodyTopPx += FloorLinePx + BandGapPx;
            }
            else
            {
                // Bottom-pinned inside the footer band so the pair reads as one status
                // strip regardless of how tall the frame's footer resolves.
                PinBandFromBottom(_synergyLabel.rectTransform, 4f, FloorLinePx);
                PinBandFromBottom(_stateLabel.rectTransform, 4f + FloorLinePx + 6f, FloorLinePx);
            }

            // ASK -> the picker's own label, pinned at the top of the body well in a
            // FIXED FontLabel line box (52 px). It must NOT scroll away with the chips.
            _askLabel = ElarionUiKit.Label(body, "", 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.03f, 0.97f, bold: false);
            ElarionUiKit.FitSingleLine(_askLabel);
            PinBandFromTop(_askLabel.rectTransform, bodyTopPx, AskBandPx);
            bodyTopPx += AskBandPx + BandGapPx;

            // PICKER -> a kit scroll well filling the REST of the body. The well is
            // pinned in pixels at the top (below the ask) and at the bottom (body pad),
            // so it never overlaps the info block no matter how the pane resolves.
            // Rows inside carry their own pixel height (MakeScrollZone runs
            // childControlHeight:false), which is what guarantees ChipButtonPx.
            var pickerGo = new GameObject("ResourcePicker", typeof(RectTransform));
            pickerGo.transform.SetParent(body, false);
            var pickerRt = pickerGo.GetComponent<RectTransform>();
            pickerRt.anchorMin = new Vector2(0.03f, 0f);
            pickerRt.anchorMax = new Vector2(0.97f, 1f);
            pickerRt.offsetMin = new Vector2(0f, BodyPadPx);
            pickerRt.offsetMax = new Vector2(0f, -bodyTopPx);

            var scroll = ElarionUiKit.MakeScrollZone(pickerGo.transform, RowGapPx, ScrollPadPx);
            _chipRow = scroll != null && scroll.content != null
                ? (Transform)scroll.content
                : pickerGo.transform;
            if (scroll == null || scroll.content == null)
                FlowTrace.Warn("Echo", "Card: MakeScrollZone returned no content -- chips parented flat.");
        }

        // -- refresh (VM -> View, one direction) -------------------------------

        // Set a fitted label ONLY when the string actually moved. EchoService raises
        // Changed EVERY FRAME while the silo fills (AddToSilo -> "notify the HUD every
        // tick for a live fill bar"), so an unconditional assign + FitSingleLine re-armed
        // the kit's one-shot UiKitTextFitGuard on every label every frame -- the same
        // per-tick churn BuildingUpgradePanelMvvm had to signature-gate.
        private static void SetIfChanged(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            text ??= "";
            if (label.text == text) return;
            label.text = text;
            ElarionUiKit.FitSingleLine(label);
        }

        private void Refresh()
        {
            if (_vm == null || _modal == null || !_open) return;
            SetIfChanged(_titleLabel, _vm.NameText);
            SetIfChanged(_whatLabel, _vm.WhatText);
            SetIfChanged(_stateLabel, _vm.StateText);
            SetIfChanged(_synergyLabel, _vm.SynergyText);
            SetIfChanged(_askLabel, _vm.AskText);
            // Portrait can change if the card is re-opened for another Echo.
            if (_portrait != null && _vm.Portrait != null)
                _portrait.sprite = _vm.Portrait;
            RebuildChips();
        }

        private void RebuildChips()
        {
            if (_chipRow == null) return;

            var chips = _vm.ResourceChips();

            // WO-852 CHURN GATE (a hard prerequisite for the scroll well, not an
            // optimisation): EchoService.AddToSilo fires Changed once per FRAME while
            // harvesting. The old card destroyed + rebuilt all five chip rows on every
            // one of those ticks. Inside a kit scroll well that also resets the scroll
            // position every frame -- the owner could never scroll to Crystals. Rebuild
            // ONLY when the RENDERED chip state moves (label / selection / note), the
            // BuildingUpgradePanelMvvm _lastContentSig pattern.
            var sig = new System.Text.StringBuilder();
            for (int i = 0; i < chips.Length; i++)
                sig.Append(chips[i].Id).Append('|').Append(chips[i].Label).Append('|')
                   .Append(chips[i].Selected ? '1' : '0').Append('|')
                   .Append(chips[i].Note).Append(';');
            string chipSig = sig.ToString();
            if (_chips.Count > 0 && chipSig == _lastChipSig) return;
            _lastChipSig = chipSig;

            for (int i = _chips.Count - 1; i >= 0; i--)
            {
                if (_chips[i] == null) continue;
                // Detach BEFORE Destroy: Destroy is deferred to end-of-frame, and a stale
                // row left parented under the scroll column's VerticalLayoutGroup would be
                // measured for one frame and double the column height.
                _chips[i].transform.SetParent(null, false);
                Destroy(_chips[i]);
            }
            _chips.Clear();

            // Guard.TryEach: one bad chip logs + skips, never blanks the picker (SS12.2).
            Guard.TryEach("Echo", "build resource chip", chips, chip =>
            {
                bool hasNote = !string.IsNullOrEmpty(chip.Note);

                // WO-852: the row's height is a FIXED PIXEL SUM, never 1/n of the host.
                // The old 1/n slice resolved to ~34 px, and ClampMinTouch then grew the
                // button symmetrically about its centre to 112 -- overflowing ~39 px above
                // AND below the slice, which is what stacked the chips on each other and
                // pushed the top one into the info text.
                float rowPx = hasNote ? ChipButtonPx + ChipNotePx : ChipButtonPx;

                // Row container: sized by sizeDelta, per the kit scroll-column row law
                // (MakeScrollZone runs childControlHeight:false, so a row keeps its own
                // height; a LayoutElement would be read as preferred-height 0).
                var rowGo = new GameObject("ResourceRow_" + chip.Id, typeof(RectTransform));
                rowGo.transform.SetParent(_chipRow, false);
                var rrt = rowGo.GetComponent<RectTransform>();
                rrt.sizeDelta = new Vector2(0f, rowPx);
                _chips.Add(rowGo);

                // Button CELL: exactly ChipButtonPx (== ElarionUiKit.MinTouchPx) tall,
                // pinned to the row's top edge in pixels. Because the cell already meets
                // the floor, ClampMinTouch's guard is a no-op and can no longer grow the
                // button past its row into a neighbour.
                var cellGo = new GameObject("ChipCell", typeof(RectTransform));
                cellGo.transform.SetParent(rowGo.transform, false);
                var crt = cellGo.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 0f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                PinBandFromTop(crt, 0f, ChipButtonPx);

                string resId = chip.Id;   // capture for the closure
                // Selected resource = Gold face (plus the "(now)" TEXT cue -- never hue alone).
                var kind = chip.Selected ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet;
                ElarionUiKit.Button(cellGo.transform, chip.Label, kind,
                    Vector2.zero, Vector2.one, () => OnChipTapped(resId));

                // Affinity note UNDER the button -- its OWN fixed floor line box, pinned
                // to the row's bottom edge, so it can never borrow the button's touch
                // height (the old 0.42 fraction split did exactly that).
                if (hasNote)
                {
                    var note = ElarionUiKit.Label(rowGo.transform, chip.Note, 0f, 1f,
                        chip.Preferred ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                        (int)ElarionUi.FontFloorMobile, TextAlignmentOptions.Center,
                        0.03f, 0.97f, bold: false);
                    ElarionUiKit.FitSingleLine(note);
                    note.raycastTarget = false;
                    PinBandFromBottom(note.rectTransform, 0f, ChipNotePx);
                }
            });

            // The kit column only measures after a layout pass; force it so the first
            // frame already scrolls correctly (the RaidDeployScreen FinalizeScroll idiom).
            if (_chipRow is RectTransform contentRt)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            }
        }

        private void OnChipTapped(string resourceId)
        {
            FlowTrace.Step("Echo", $"Card: resource chip tapped '{resourceId}'.");
            _vm?.AssignResource(resourceId);
            // VM raises Changed via the seam -> Refresh re-binds STATE + selected chip.
        }
    }
}
