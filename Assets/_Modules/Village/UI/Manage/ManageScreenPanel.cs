// =============================================================================
// ManageScreenPanel — the unified MANAGE / QUEUES screen (WO-911, absorbs WO-905).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// ONE screen, opened by ONE bar face, holding all THREE production lines.
// It SUPERSEDES the old ObsidianQueueHud modal and the undiscoverable
// Builders-chip double-tap (WO-911 §3c / B4).
//
// CONSTRUCTION LAW (non-negotiable, learned the hard way):
//   • UXML DOES NOT WORK IN BUILDS — this is code-built uGUI via ElarionUiKit.
//   • ASCII ONLY in every TMP string. LiberationSans-SDF renders anything else as
//     tofu, so: "->" not an arrow, "..." not an ellipsis glyph, "x5" not a
//     multiplication sign. ManageScreenVM.Ascii() is the belt-and-braces filter.
//   • NEVER convey meaning by COLOUR ALONE — the owner is red/green colourblind.
//     Every state on this screen is a SENTENCE ("Queued - 3rd in line",
//     "Short 150 wood", "Extra slot: locked - awaken a 3rd Echo"). Button tints
//     are decoration on top of text that already says it.
//   • Fixed-pixel row bands (LayoutElement preferredHeight AND rt.sizeDelta.y),
//     never fractions of parent — the documented root cause of the WO-841/852
//     culling bugs, and the scroll column does not control child height.
//   • MinTouchPx (112) on every tappable row and control.
//
// CHEAP TICK (WO-836/864 lesson): the 1s tick rewrites only the countdown STRINGS
// on rows already built. Rows are rebuilt ONLY on BuildTimerService.QueueChanged
// or a tab change — never per second, which is what caused per-frame layout churn
// and fit-guard re-arm in the old queue HUD.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Jobs;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// The Manage / Queues screen. Registered on <see cref="PanelId.Manage"/> and on the legacy
    /// <see cref="ObsidianQueueGate.ToggleRequested"/> verb (which the re-pointed bar face raises),
    /// so there is exactly ONE door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManageScreenPanel : MonoBehaviour
    {
        // =====================================================================
        //  THE BAND TABLE — fixed reference pixels, summed against the measured well
        // ---------------------------------------------------------------------
        // BUILD-1 DEFECT (owner felt-test 2026-08-07, WO-905 §2.7 #5): every band was a
        // FRACTION of the body well, and the well is SMALL. At 2670x1200 the FrameCore body
        // resolves to ~533 reference px, so the old fractions gave the rail header 0.055 of it
        // (~29px) and the tab row 0.09 (~48px) — while ClampMinTouch grows every kit button to
        // MinTouchPx (112) and QueueRailView pins itself at a FIXED 200px. Three elements each
        // 2-7x taller than the band they were given, all painting over each other: exactly the
        // reported overprinting.
        //
        // THE LAW (the same one EndStateView holds): every band owns a FIXED PIXEL height, the
        // heights are SUMMED, the sum is subtracted from the measured well, and the scrolling
        // list takes the REMAINDER. If the fixed bands alone exceed the well we say so in px
        // (FlowTrace.Warn) and shrink deliberately — bands never share pixels.
        //
        // Stacking order (WO-905 §2.7 / §2.6), each in its own band, gutter between every pair:
        //   1 rails (status strip + the active line's card rail)
        //   2 extra-slot / Buy-slot row
        //   3 content tabs
        //   4 scrolling list   <- the flexible band; absorbs the remainder
        //   5 Close            <- the kit's shared Close, in its reserved bottom band
        // =====================================================================
        private const float RowHeightPx = 132f;     // >= MinTouchPx (112) with room for three text lines
        private const float SectionHeaderPx = 64f;
        private const float StripBandPx = 56f;      // band 1a: one FontLabel(40) line box (~46px) + air
        private const float SlotBandPx = 120f;      // band 2: 0.96 * 120 = 115px button >= MinTouchPx
        private const float TabsBandPx = 120f;      // band 3: same MinTouch arithmetic as the slot row
        private const float NoticeBandPx = 56f;     // in-body fallback seat for the notice line
        private const float NoticeCloseBandPx = 96f;// beside-the-Close seat (two lines of FontLabel)
        private const float BandGapPx = 12f;        // guaranteed gutter — no two bands ever touch
        private const float MinListPx = 240f;       // band 4 floor: one 132px row under its 64px header
        private const float CloseBandY0 = 0.050f;   // ElarionUiKit's DefaultCloseZone.y (the Close band)
        private const float CloseGapY = 0.020f;     // body floor clears the Close box by this much
        private const float RowCtrlY0 = 0.06f;      // 0.88 * RowHeightPx = 116px >= MinTouchPx (112),
        private const float RowCtrlY1 = 0.94f;      // so an in-row button is never GROWN out of its row

        // =====================================================================
        //  WO-1058 — ONE PRIMARY SLOT PER ROW. THE X-BANDS ARE THE WHOLE FIX.
        // ---------------------------------------------------------------------
        // The owner asked to "reuse the same button and make it finish now so you don't have to
        // move", and ruled the double-tap a FEATURE ("they can double click and be done"). The
        // arithmetic said the same gesture was ALSO destructive: `Upgrade` sat at 0.84-0.98 on a
        // browse row and `Cancel` at 0.885-0.98 on a queue row — the same strip of glass — and
        // starting a job INSERTS a queue row above the browse list, sliding a different row under
        // a finger that has not moved.
        //
        // THE INVARIANT, and it is the only thing that makes a sanctioned double-tap safe:
        //   EVERY row type puts exactly ONE control in PrimaryX0..PrimaryX1, it is ALWAYS the
        //   action the player wants (Upgrade / Finish Now / Expand), and it is NEVER destructive
        //   and NEVER free — the price is printed on the face BEFORE the finger arrives.
        // So whichever row slides under the second tap, the worst outcome is a priced action the
        // player could read, and `Cancel` is unreachable from that strip by construction.
        //
        // ⛔ NOT solved by a confirm dialog, a cooldown or a tap lockout (§2.2 forbids all three —
        //    the fast path IS the feature), and NOT by raising BuildTimerConfig.freeBuildSlots to
        //    guarantee a RUNNING job (queueDepthPerLine and freeBuildSlots are different axes and
        //    that config says so in its own comment).
        //
        // Left of the primary sits a DEAD GAP that nothing may occupy, then the secondary cluster.
        // The cluster is laid by EVEN SPLIT (ClusterSlot) rather than hand-authored bands: at the
        // narrowest supported aspect (1920x1080) the list row resolves to ~1490 reference px, so
        // three controls sharing 0.455-0.72 get 0.0817 each = ~122px — over MinTouchPx (112), so
        // ClampMinTouch is a NO-OP. Hand-authored uneven bands could not clear the floor for three
        // controls, and a clamp that fires is exactly WO-1056's root cause on the panel next door.
        //
        // ⚠ ClusterX0 is 0.455 and NOT the ticket's literal 0.40: the row's TEXT column owns
        //   x <= 0.44 (name / state / refund), and a control authored at 0.40 would sit ON that
        //   text — the "BUTTON OVER TEXT" failure the WO-1060 oracle exists to catch. The ticket's
        //   §2.3 table was authored without the text column in view; the ORDER it specifies
        //   (Ad, Cancel, Move up, then the primary) is preserved exactly, so Cancel is never
        //   adjacent to the primary slot.
        // =====================================================================
        private const float PrimaryX0 = 0.76f;      // THE primary slot — identical on every row
        private const float PrimaryX1 = 0.98f;      // 0.22 * ~1490px = ~328px: "Finish Now" fits flat
        private const float PrimaryGuardX = 0.04f;  // dead gap — nothing tappable may enter it
        private const float ClusterX0 = 0.455f;     // secondary cluster starts clear of the text column
        private const float ClusterX1 = PrimaryX0 - PrimaryGuardX;   // 0.72
        private const float ClusterGapX = 0.010f;

        // Queue-row TEXT bands (WO-1058 clipping pass). Each band now HOLDS its line box
        // (~1.16 * fontSize) instead of crowding it: the name line was authored at FontLabel(40)
        // — a ~46px box — inside a 0.72-1.00 band that resolves to 37px, so every title bled ~5px
        // over its band into the row above. Re-banded, NOT re-heighted: RowHeightPx stays 132
        // because vertical is the scarce axis in landscape.
        //   name   0.679-0.996 -> 41.8px, holds a 36px line box (41.8)   OK
        //   state  0.386-0.671 -> 37.6px, holds a 32px line box (37.1)   OK
        //   refund 0.093-0.378 -> 37.6px, holds a 32px line box (37.1)   OK
        //   bar    0.012-0.085 ->  9.6px  (a progress strip should be thin)
        // 126.6px of 132 spent, ~1px gutter between bands. Both text sizes stay at or above the
        // kit's FontFloor (30) — this shrinks TEXT to fit its authored band, never a CONTROL.
        private const float QueueNameFontPx = 36f;
        private const float QueueLineFontPx = 32f;  // == ElarionUi.FontMicro, an authored role
        private const float QRowNameY0 = 0.679f, QRowNameY1 = 0.996f;
        private const float QRowStateY0 = 0.386f, QRowStateY1 = 0.671f;
        private const float QRowRefundY0 = 0.093f, QRowRefundY1 = 0.378f;
        private const float QRowBarY0 = 0.012f, QRowBarY1 = 0.085f;

        /// <summary>Tail spacer under the last list row (WO-1058). At max scroll the last row then
        /// clears the viewport's RectMask2D completely instead of being sliced mid-glyph at the
        /// Close band edge — the "content runs under Close" the owner photographed. It lives INSIDE
        /// the scrolling content, so the panel's fixed-band budget is untouched.</summary>
        private const float ListTailPx = 28f;

        private ManageScreenVM _vm;
        private int _browsePage;
        private GameObject _ui;
        private RectTransform _listContent;
        private RectTransform _railBand;            // non-null only while the rail is PINNED
        private RectTransform _tabsHost;
        private readonly TextMeshProUGUI[] _stripCells = new TextMeshProUGUI[3];
        private TextMeshProUGUI _slotLabel;
        private TextMeshProUGUI _noticeLabel;
        private Button _slotButton;
        private PanelHandle _panelHandle;
        private QueueRailView _rail;
        private ChannelId _railChannel = ChannelId.Builder;
        private bool _railPinned;                   // false => the rail rides the scroll list
        private float _railBandPx = 200f;           // QueueRailView.HeightOf(Options.Default)
        private float _tickAt;

        // Live countdown cells: the cheap tick rewrites ONLY these strings.
        private readonly List<TickCell> _tickCells = new List<TickCell>(16);

        private struct TickCell
        {
            public TextMeshProUGUI Text;
            public ChannelId Channel;
            public string JobId;
            public bool Queued;
            public int PendingIndex;
        }

        /// <summary>WO-898 item 1 — progress bars advanced by the same 1 Hz tick as the timers.</summary>
        private readonly List<ProgressCell> _progressCells = new List<ProgressCell>(16);

        private struct ProgressCell
        {
            public ElarionUiKit.BarHandle Handle;
            public ChannelId Channel;
            public string JobId;
            public bool Queued;
        }

        /// <summary>True while the screen is up (the panel is built on open, destroyed on close).</summary>
        public bool IsOpen => _ui != null;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Manage", Close, () => IsOpen);
            PanelRouter.Register(PanelId.Manage, (Action)Open);
            PanelRouter.Register(PanelId.Manage, (Action<string>)Open);

            // The re-pointed bar face raises the EXISTING gate verb, so this screen is the single
            // door onto the queues and HudKitController keeps calling ObsidianQueueGate.RequestToggle
            // (the oracle at ObsidianQueueRegression that requires that call still passes).
            ObsidianQueueGate.ToggleRequested += Toggle;
        }

        private void OnDestroy()
        {
            ObsidianQueueGate.ToggleRequested -= Toggle;
            PanelRouter.Unregister(PanelId.Manage, (Action)Open);
            PanelRouter.Unregister(PanelId.Manage, (Action<string>)Open);
            if (_vm != null) _vm.Changed -= Render;
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged -= OnQueueChanged;
            if (_ui != null) Destroy(_ui);
            _ui = null;
        }

        /// <summary>Open if closed, close if open.</summary>
        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        /// <summary>Build and show the screen.</summary>
        public void Open()
        {
            Close();                                  // never stack two canvases

            _vm = new ManageScreenVM();
            _vm.Changed += Render;

            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged += OnQueueChanged;

            if (!Guard.Try("Manage", "build manage chrome", BuildChrome))
            {
                FlowTrace.Fail("Manage", "chrome build threw — screen not shown.");
                Close();
                return;
            }

            _vm.Rebuild();

            // WO-465: a panel that never notifies reads as an invisible scrim and PanelRouter
            // reports the open as failed.
            if (!PanelManager.NotifyOpened(_panelHandle))
                FlowTrace.Warn("Manage", "PanelManager refused the open (another exclusive panel holds the screen).");
            FlowTrace.Step("Manage", "Manage/Queues screen opened.");
        }

        /// <summary>Contextual doorway used by Build Collections to land directly on Defense.</summary>
        public void Open(string requestedTab)
        {
            Open();
            if (_vm == null) return;
            if (string.Equals(requestedTab, "Defense", StringComparison.OrdinalIgnoreCase))
            {
                _vm.SelectTab(ManageTab.Defense);
                FlowTrace.Step("Manage", "context open -> UPGRADABLE TOWERS (Defense tab).");
            }
        }

        /// <summary>Tear the screen down.</summary>
        public void Close()
        {
            if (_vm != null) { _vm.Changed -= Render; _vm = null; }
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged -= OnQueueChanged;

            _tickCells.Clear();
            _rail = null;
            _listContent = null;
            _railBand = null;
            _railPinned = false;
            _tabsHost = null;
            for (int i = 0; i < _stripCells.Length; i++) _stripCells[i] = null;
            _slotLabel = null;
            _noticeLabel = null;
            _slotButton = null;
            _sessionCompleteShown = false;   // WO-1027: the "you're set" line is per-open state

            if (_ui != null) { Destroy(_ui); _ui = null; }
            PanelManager.NotifyClosed(_panelHandle);
        }

        private void OnQueueChanged()
        {
            // A job started / finished / was added / removed / reordered: the SHAPE moved, so the
            // rows must be rebuilt. This is the only rebuild trigger besides a tab change.
            if (IsOpen) _vm?.Rebuild();
        }

        // =====================================================================
        //  CHROME
        // =====================================================================

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("ManageScreenUI", 31200);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(
                _ui.transform, "MANAGE",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                Close, frameName: RpgUiCatalog.FrameCore);
            if (chrome == null)
            {
                FlowTrace.Fail("Manage", "BuildObsidianPanel returned no chrome — the screen has no host.");
                return;
            }

            // =================================================================
            //  ONE OWNED GEOMETRY PASS — measure the well, then spend it
            // -----------------------------------------------------------------
            // Read the panel height the DETERMINISTIC way: a live rect read on the canvas's
            // creation frame returns RAW SCREEN pixels (the CanvasScaler has not applied yet).
            // PostScaleCanvasHeight replays the scaler's own math, so every number below is in
            // the reference-px space the anchors will really resolve against.
            // =================================================================
            float canvasH = ElarionUiKit.PostScaleCanvasHeight(
                chrome.root != null ? chrome.root.transform : _ui.transform);
            float panelFracH = 0.90f, panelFracW = 0.92f;
            if (chrome.root != null)
            {
                var rootRt = (RectTransform)chrome.root.transform;
                panelFracH = Mathf.Max(0.05f, rootRt.anchorMax.y - rootRt.anchorMin.y);
                panelFracW = Mathf.Max(0.05f, rootRt.anchorMax.x - rootRt.anchorMin.x);
            }
            float panelPx = Mathf.Max(1f, canvasH * panelFracH);
            float panelWpx = Mathf.Max(1f, CanvasWidthPx(canvasH) * panelFracW);

            // The kit reserves the bottom of EVERY framed panel for the ONE shared Close (a fixed
            // CanonCtaHeight box growing up from y=0.050) and then parks the frame's designed
            // FOOTER band above it. This screen uses NO footer zone, so that relocated band is
            // dead space between the list and the Close — reclaim it by dropping the body floor
            // straight onto the Close band + a gap. That is the whole of band 5's arithmetic.
            float closeBandTop = CloseBandY0 + ElarionUiKit.CanonCtaHeight / panelPx;
            float bodyFloor = closeBandTop + CloseGapY;

            RectTransform bodyRt = chrome.layout != null ? chrome.layout.body : null;
            float bodyTop = bodyRt != null ? bodyRt.anchorMax.y : 0.835f;
            if (bodyRt != null && bodyTop - bodyFloor > 0.05f)
            {
                bodyRt.anchorMin = new Vector2(bodyRt.anchorMin.x, bodyFloor);
                bodyRt.anchorMax = new Vector2(bodyRt.anchorMax.x, bodyTop);
                bodyRt.offsetMin = new Vector2(bodyRt.offsetMin.x, 0f);
                bodyRt.offsetMax = new Vector2(bodyRt.offsetMax.x, 0f);
            }
            // Parent to layout.body, NOT chrome.content — the proven idiom (WO-778): content dropped
            // straight onto chrome.content clips under the title band and the shared Close button.
            // Without a layout (procedural fallback frame) mint the same well by hand so the band
            // cursor below still measures from a real body top.
            RectTransform well = bodyRt ?? MakeZone(
                chrome.content != null ? chrome.content.transform : _ui.transform, "Zone_Body_Manage",
                new Vector2(0.055f, bodyFloor), new Vector2(0.945f, bodyTop));
            float wellPx = Mathf.Max(0f, (bodyTop - bodyFloor) * panelPx);

            // ── Band 1a: the ALL-THREE-LINES strip. Every channel stays glanceable on every tab,
            //    as TEXT, so the player never loses sight of a line the current tab does not own.
            //    It seats in the frame's own SUB-HEADER band when the frame has one (free real
            //    estate ABOVE the well — it costs the list nothing); otherwise it takes a band.
            RectTransform subHeader = chrome.layout != null ? chrome.layout.subHeader : null;
            bool stripInBody = subHeader == null;
            float stripPx = stripInBody
                ? StripBandPx
                : (subHeader.anchorMax.y - subHeader.anchorMin.y) * panelPx;

            // ── Band 5b: the NOTICE line. Same reclaim: the Close band is CanonCtaHeight tall and
            //    the Close box is only CanonCtaWidth wide and centred, so the column to its LEFT is
            //    dead space. Seat the notice there when it clears the box; fall back to a body band.
            //    ⚠ Not a toast. ElarionUiKit.ShowToast renders at sorting order ~720 and this modal
            //    sorts at 31200, so a toast raised from here would be drawn UNDERNEATH the screen the
            //    player is looking at — i.e. a refusal would LOOK like a silent no-op, which is exactly
            //    the failure §12 forbids and exactly the bug WO-911 is fixing on the Finish button.
            float noticeX1 = 0.5f - (0.5f * ElarionUiKit.CanonCtaWidth / panelWpx) - 0.02f;
            bool noticeBesideClose = chrome.content != null && noticeX1 >= 0.24f;

            // ── THE SUM. Every band costs its height PLUS the gutter that follows it; the list is
            //    last (or second-last when the notice is in-body), so it pays no trailing gutter.
            _railBandPx = QueueRailView.HeightOf(QueueRailView.Options.Default);
            float stripCost = stripInBody ? StripBandPx + BandGapPx : 0f;
            float noticeCost = noticeBesideClose ? 0f : NoticeBandPx + BandGapPx;
            float fixedNoRail = stripCost + SlotBandPx + BandGapPx + TabsBandPx + BandGapPx + noticeCost;

            // The rail is the ONE elastic element: 200 fixed px of card art whose every fact is
            // already on the strip (line status) and on the rows below (per-job label, countdown,
            // controls). It keeps its own PINNED band only while the well can still seat a usable
            // list underneath; otherwise it is demoted into the scroll list as its first row —
            // deliberately scrolled, never overlapped, and said out loud in the trace below.
            _railPinned = (wellPx - fixedNoRail - (_railBandPx + BandGapPx)) >= MinListPx;
            float fixedPx = fixedNoRail + (_railPinned ? _railBandPx + BandGapPx : 0f);
            float listPx = wellPx - fixedPx;

            if (!_railPinned)
                FlowTrace.Warn("Manage", string.Format(
                    "rail NOT pinned: it needs {0:0}px + {1:0}px gutter, and pinning it would leave the " +
                    "list {2:0}px (floor {3:0}px). Demoted to the FIRST ROW OF THE SCROLL LIST — it scrolls, " +
                    "nothing overlaps, and the three-line status strip stays pinned above.",
                    _railBandPx, BandGapPx, wellPx - fixedNoRail - (_railBandPx + BandGapPx), MinListPx));
            if (listPx < 0f)
            {
                FlowTrace.Warn("Manage", string.Format(
                    "BAND OVERFLOW: the fixed bands need {0:0}px but the well is only {1:0}px — short by " +
                    "{2:0}px. The list is clamped to 0 rather than letting bands overprint each other.",
                    fixedPx, wellPx, fixedPx - wellPx));
                listPx = 0f;
            }
            else if (listPx < MinListPx)
                FlowTrace.Warn("Manage", string.Format(
                    "list well is {0:0}px, under the {1:0}px floor (one {2:0}px row under its {3:0}px header) — " +
                    "the list still scrolls, but fewer than one row is visible at rest.",
                    listPx, MinListPx, RowHeightPx, SectionHeaderPx));

            // §12: the geometry is PROVEN by a capture, not by an eyeball. One line, every band.
            float gapsPx = fixedPx
                         - (stripInBody ? StripBandPx : 0f)
                         - (_railPinned ? _railBandPx : 0f)
                         - SlotBandPx - TabsBandPx
                         - (noticeBesideClose ? 0f : NoticeBandPx);
            FlowTrace.Step("Manage", string.Format(
                "bands(px): canvas={0:0} panel={1:0} well={2:0} || strip={3:0}[{4}] rail={5:0}[{6}] " +
                "slot={7:0} tabs={8:0} notice={9:0}[{10}] gaps={11:0} => fixed={12:0} LIST={13:0} (floor {14:0})",
                canvasH, panelPx, wellPx, stripPx, stripInBody ? "body" : "sub-header",
                _railBandPx, _railPinned ? "pinned" : "in-list",
                SlotBandPx, TabsBandPx,
                noticeBesideClose ? NoticeCloseBandPx : NoticeBandPx,
                noticeBesideClose ? "close-band" : "body",
                gapsPx, fixedPx, listPx, MinListPx));

            // ── LAY THE BANDS. One cursor, top-down, gutter after every band. Nothing here can
            //    overlap anything else: each band's height is pixels it OWNS.
            float cursor = 0f;
            BuildStrip(stripInBody ? Band(well, "Band_ChannelStrip", ref cursor, StripBandPx) : subHeader);
            if (_railPinned) _railBand = Band(well, "Band_Rail", ref cursor, _railBandPx);
            BuildSlotRow(Band(well, "Band_SlotRow", ref cursor, SlotBandPx));

            _tabsHost = Band(well, "Band_Tabs", ref cursor, TabsBandPx);
            BuildTabs();

            var listBand = Band(well, "Band_List", ref cursor, listPx);
            var scroll = ElarionUiKit.MakeScrollZone(listBand, spacing: 8f, padding: 10);
            _listContent = scroll != null ? scroll.content : null;
            if (_listContent == null)
                FlowTrace.Fail("Manage", "MakeScrollZone returned no content — the list host is missing.");

            BuildNotice(noticeBesideClose
                ? NoticeSeatBesideClose(chrome.content.transform, noticeX1)
                : Band(well, "Band_Notice", ref cursor, NoticeBandPx));
        }

        /// <summary>
        /// Post-scale canvas WIDTH in the same reference-px space as
        /// <see cref="ElarionUiKit.PostScaleCanvasHeight"/> — one scaleFactor drives both axes, so
        /// the post-scale canvas keeps the screen's aspect. DERIVED, never read off a live rect on
        /// the creation frame (that returns raw screen px).
        /// </summary>
        private static float CanvasWidthPx(float canvasH)
        {
            float sw = ElarionUiKit.SurfaceWidth, sh = ElarionUiKit.SurfaceHeight;
            if (sw < 1f || sh < 1f) return canvasH * (1080f / 1920f);   // headless: kit portrait reference
            return canvasH * (sw / sh);
        }

        /// <summary>
        /// Seat the next band under the previous one and advance the cursor by its height PLUS the
        /// guaranteed gutter. Top-anchored, top pivot, explicit <c>sizeDelta.y</c> — the height is
        /// REFERENCE PIXELS, never a fraction of the parent. Fractional bands are what shipped in
        /// build 1: a 112px MinTouch button inside a 23px fraction band overprinted its neighbours.
        /// </summary>
        private static RectTransform Band(RectTransform parent, string name, ref float cursorPx,
                                          float heightPx, float x0 = 0.01f, float x1 = 0.99f)
        {
            float h = Mathf.Max(0f, heightPx);
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(x0, 1f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, h);
            rt.anchoredPosition = new Vector2(0f, -cursorPx);
            cursorPx += h + BandGapPx;
            return rt;
        }

        /// <summary>The notice seat in the reclaimed Close band, left of the centred Close box.</summary>
        private static RectTransform NoticeSeatBesideClose(Transform content, float x1)
        {
            var go = new GameObject("Band_Notice", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            rt.anchorMin = new Vector2(0.04f, CloseBandY0);
            rt.anchorMax = new Vector2(x1, CloseBandY0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, NoticeCloseBandPx);
            rt.anchoredPosition = new Vector2(0f, 8f);
            return rt;
        }

        /// <summary>Band 1a — the three lines as three evenly spaced TEXT columns. One long
        /// run-on label is what wrapped and collided in build 1; a column per channel cannot.</summary>
        private void BuildStrip(RectTransform host)
        {
            if (host == null) return;
            const float gap = 0.01f;
            float w = (1f - gap * 2f) / 3f;
            for (int i = 0; i < _stripCells.Length; i++)
            {
                float x = i * (w + gap);
                var align = i == 0 ? TextAlignmentOptions.Left
                          : i == 1 ? TextAlignmentOptions.Center
                                   : TextAlignmentOptions.Right;
                var t = ElarionUiKit.Label(host, "", 0f, 1f, ElarionUi.Parchment,
                                           (int)ElarionUi.FontLabel, align, x, x + w);
                // NoWrap + ellipsis + autosize to the 30px floor: the text can shrink INSIDE its
                // column but can never wrap out of its band onto the band below.
                ElarionUiKit.FitSingleLine(t);
                _stripCells[i] = t;
            }
        }

        /// <summary>Band 2 — the extra-slot sentence and the Buy-slot button, on their OWN row
        /// below the rails (WO-905 §2.7 #2: nothing floats over the rail text).</summary>
        private void BuildSlotRow(RectTransform band)
        {
            if (band == null) return;
            _slotLabel = ElarionUiKit.Label(band, "", 0f, 1f, ElarionUi.ParchmentDim,
                                            (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.01f, 0.62f);
            ElarionUiKit.FitSingleLine(_slotLabel);
            _slotButton = ElarionUiKit.BuildObsidianButton(band, ManageScreenVM.BuyBuilderButtonCopy,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.66f, 0.02f), new Vector2(0.99f, 0.98f),
                () => { _vm?.BuySlot(ManageScreenVM.ChannelOf(_vm.Tab)); FlushNotice(); });
            ElarionUiKit.ClampMinTouch(_slotButton);
        }

        private void BuildNotice(RectTransform band)
        {
            if (band == null) return;
            _noticeLabel = ElarionUiKit.Label(band, "", 0f, 1f, ElarionUi.Gold,
                                              (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.01f, 0.99f);
            // A notice may run to two lines — FitBlock wraps and truncates INSIDE the band.
            ElarionUiKit.FitBlock(_noticeLabel);
        }

        private static RectTransform MakeZone(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private void BuildTabs()
        {
            if (_tabsHost == null) return;
            for (int i = _tabsHost.childCount - 1; i >= 0; i--) Destroy(_tabsHost.GetChild(i).gameObject);

            var labels = ManageScreenVM.TabLabels;
            int n = _vm != null ? _vm.VisibleTabs.Count : 0;
            if (n == 0)
            {
                ElarionUiKit.Label(_tabsHost, "Place a structure to unlock Manage categories", 0f, 1f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, 0f, 1f);
                return;
            }
            const float gap = 0.012f;
            float w = (1f - gap * (n - 1)) / n;

            for (int i = 0; i < n; i++)
            {
                ManageTab tab = _vm.VisibleTabs[i];
                int index = (int)tab;
                bool selected = _vm != null && _vm.Tab == tab;
                float x = i * (w + gap);

                var btn = ElarionUiKit.BuildObsidianButton(_tabsHost, labels[index],
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(x, 0.02f), new Vector2(x + w, 0.98f),
                    () => { _browsePage = 0; _vm?.SelectTab((ManageTab)index); });
                ElarionUiKit.ClampMinTouch(btn);
                if (btn != null)
                {
                    btn.name = "Tab_" + labels[index] + (selected ? "_Selected" : "");
                    if (selected)
                    {
                        // A stable underline is the non-colour selection signal. It reads as a
                        // conventional tab without adding brackets or another word to the label.
                        var underline = new GameObject("SelectedUnderline", typeof(Image));
                        underline.transform.SetParent(btn.transform, false);
                        var ur = (RectTransform)underline.transform;
                        ur.anchorMin = new Vector2(0.16f, 0.04f);
                        ur.anchorMax = new Vector2(0.84f, 0.04f);
                        ur.pivot = new Vector2(0.5f, 0f);
                        ur.sizeDelta = new Vector2(0f, 6f);
                        ur.anchoredPosition = Vector2.zero;
                        var ui = underline.GetComponent<Image>();
                        ui.color = ElarionUi.Gold;
                        ui.raycastTarget = false;
                    }
                }
            }
        }

        // =====================================================================
        //  RENDER
        // =====================================================================

        private void Render()
        {
            if (_vm == null || _ui == null) return;
            Guard.Try("Manage", "render manage rows", () =>
            {
                RenderStrip();
                RenderSlotOffer();
                RenderRail();
                BuildTabs();
                RenderList();
                Canvas.ForceUpdateCanvases();
                if (_listContent != null)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            });
            FlushNotice();
            RenderSessionComplete();
        }

        // =====================================================================
        //  WO-1027 §3.3 — THE SESSION-COMPLETE SIGNAL (the quiet inverse of the ache)
        // =====================================================================
        // CoC never told a player she was DONE, and that is a genuine gap: a player who does not
        // know she is finished leaves hunting for a missed thing instead of leaving satisfied.
        //
        // ⚠ ITS PREDICATE IS STRICTER THAN THE BAR NUMERAL'S, on purpose. The Manage face goes
        // quiet at "no line is idle" (something is cooking everywhere); this line waits for
        // AllLinesLoaded() — every line at FULL crew, nothing left to start. Telling a player she
        // is set while a slot sits free would be a lie, and a wrong session-complete signal is
        // worse than none at all.
        //
        // It is A SENTENCE. Not a colour, not a checkmark glyph, not a toast (ruling (c) is
        // REJECTED and nothing here fires on entering town). It reuses the existing notice seat,
        // so no band is added and the panel's pixel budget is untouched — and it NEVER stomps a
        // real notice, which is the one message the player actually asked for.
        private const string SessionCompleteText = "Every line is loaded - you are set for now.";
        private bool _sessionCompleteShown;

        private void RenderSessionComplete()
        {
            if (_noticeLabel == null) return;
            bool set = ObsidianQueueGate.Status.AllLinesLoaded();
            if (set == _sessionCompleteShown) return;      // transition only

            if (set)
            {
                if (!string.IsNullOrEmpty(_noticeLabel.text)) return;   // a live notice wins
                _noticeLabel.text = SessionCompleteText;
                _sessionCompleteShown = true;
                FlowTrace.Step("Manage", "session complete: all 3 lines loaded, no free slots");
                return;
            }

            if (string.Equals(_noticeLabel.text, SessionCompleteText, StringComparison.Ordinal))
                _noticeLabel.text = "";
            _sessionCompleteShown = false;
        }

        private void RenderStrip()
        {
            for (int i = 0; i < _stripCells.Length; i++)
            {
                var cell = _stripCells[i];
                if (cell == null) continue;
                string text = i < _vm.Channels.Count
                    ? _vm.Channels[i].Describe()
                    : (i == 0 && _vm.Channels.Count == 0 ? "Queues unavailable." : "");
                cell.text = ManageScreenVM.Ascii(text);
            }
        }

        private void RenderSlotOffer()
        {
            if (_slotLabel != null)
            {
                _slotLabel.text = ManageScreenVM.Ascii(_vm.SlotOfferText ?? "");
                ElarionUiKit.FitSingleLine(_slotLabel);
            }
            if (_slotButton != null)
            {
                // Always the store SKU, never a crystal price. Button stays VISIBLE so the
                // route is reachable even after the SKU is owned (store shows Owned).
                var label = _slotButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = ManageScreenVM.BuyBuilderButtonCopy;
                    ElarionUiKit.FitSingleLine(label);
                }
            }
        }

        private void RenderRail()
        {
            // PINNED path only. When the well could not afford a 200px pinned band (see the budget
            // in BuildChrome) the rail rides the scroll list instead and RenderList mounts it.
            if (!_railPinned || _railBand == null) return;
            MountRail(_railBand, forceRebuild: false);
        }

        /// <summary>
        /// Build (or re-sync) the WO-864 rail into <paramref name="mount"/>. The rail pins itself to
        /// the TOP of its mount at a FIXED <see cref="QueueRailView.Height"/> — which is precisely
        /// why its host must be a pixel band: build 1 handed it a 0.2-of-body fraction (~82px) and
        /// 200px of rail painted straight over the tab row below it.
        /// </summary>
        private void MountRail(RectTransform mount, bool forceRebuild)
        {
            if (mount == null) return;
            var channel = ManageScreenVM.ChannelOf(_vm.Tab);

            // Rebuild the rail only when the TAB's channel actually changed (Defense -> Buildings
            // keeps the same Builders rail and must not thrash it). A rail living in the scroll list
            // is destroyed with the rows every render, so that path always rebuilds.
            if (!forceRebuild && _rail != null && _railChannel == channel) { _rail.Sync(); return; }

            for (int i = mount.childCount - 1; i >= 0; i--) Destroy(mount.GetChild(i).gameObject);
            _railChannel = channel;
            Guard.Try("Manage", "build queue rail", () =>
            {
                // Reuses the WO-864 rail component verbatim through its host-agnostic contract.
                // The rail is DECORATION here: its cards are raycast-off, so the collapsed xN card
                // physically cannot be a cancel target (ruling Q12). Every action lives on the rows.
                _rail = QueueRailView.Build(mount, channel, QueueRailView.Options.Default);
            });
        }

        private void RenderList()
        {
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);
            _tickCells.Clear();
            // The progress cells point at bars that were just destroyed with their rows. The tick
            // already skips a Unity-null fill, so this never crashed — but without the clear the
            // list grew by every rebuild for the life of the open panel.
            _progressCells.Clear();

            var channel = ManageScreenVM.ChannelOf(_vm.Tab);

            // The DEMOTED rail (see the band budget): its own fixed-pixel row at the head of the
            // list, so it keeps its full 200px and simply scrolls away instead of overprinting.
            if (!_railPinned)
                MountRail(MakeRowHost("RailRow", _railBandPx), forceRebuild: true);

            var summary = FindSummary(channel);
            // The selected structure and its action lead the scroll content, keeping the primary
            // task above the queue history on a phone viewport.
            AddSectionHeader(_vm.Tab == ManageTab.Defense
                ? "UPGRADABLE TOWERS - affordable first"
                : "UPGRADES - what you can afford first");
            if (_vm.BrowseRows.Count == 0)
                AddNoteRow(_vm.Tab == ManageTab.Defense
                    ? "No defenses are ready to upgrade. Build your first tower or wall here."
                    : "Nothing to upgrade on this tab yet.");
            else
            {
                const int pageSize = 4;
                int pageCount = Mathf.CeilToInt(_vm.BrowseRows.Count / (float)pageSize);
                _browsePage = Mathf.Clamp(_browsePage, 0, pageCount - 1);
                int first = _browsePage * pageSize;
                int end = Mathf.Min(first + pageSize, _vm.BrowseRows.Count);
                AddNoteRow("Showing " + (first + 1) + "-" + end + " of " + _vm.BrowseRows.Count +
                           " - page " + (_browsePage + 1) + " of " + pageCount);
                for (int i = first; i < end; i++) AddBrowseRow(_vm.BrowseRows[i]);
                if (_browsePage > 0)
                    AddActionNoteRow("Earlier placed structures", "Previous page", () => { _browsePage--; Render(); });
                if (end < _vm.BrowseRows.Count)
                    AddActionNoteRow((_vm.BrowseRows.Count - end) + " more placed structures", "Next page", () => { _browsePage++; Render(); });
            }

            if (_vm.Tab == ManageTab.Defense)
                AddActionNoteRow("Need another tower?", "Build new defense", OpenDefenseBuilder);
            else
                AddActionNoteRow("Need something that is not placed here?", "Build new", Close);

            AddSectionHeader("IN QUEUE - " + summary);
            if (_vm.QueueRows.Count == 0) AddNoteRow("Nothing queued on this line.");
            else for (int i = 0; i < _vm.QueueRows.Count; i++) AddQueueRow(_vm.QueueRows[i]);

            if (!string.IsNullOrEmpty(_vm.RepairOfferText))
                AddActionNoteRow(_vm.RepairOfferText, "Repair", () => { _vm.RepairAll(); FlushNotice(); });

            // WO-1058 — TAIL SPACER. The list is a scroller inside a RectMask2D whose floor sits
            // just above the shared Close, so at max scroll the last row used to end MID-GLYPH on
            // that mask edge and read as "the content runs under Close" (owner frame 2026-08-22).
            // An empty tail row lets the last real row clear the mask completely. It costs the
            // panel NO height — the fixed-band budget in BuildChrome is untouched.
            MakeRowHost("ListTailSpacer", ListTailPx);

            // §12 — the geometry is PROVEN by a capture, not by an eyeball. One line naming the
            // invariant this ticket exists to hold, so a screenshot can be checked against numbers.
            FlowTrace.Step("Manage", string.Format(
                "row bands: PRIMARY x{0:F3}-{1:F3} (never destructive: Upgrade / Finish Now / Expand / Repair) | " +
                "dead gap {2:F3}-{3:F3} | secondary cluster {4:F3}-{5:F3} (Ad, Cancel, Move up, even split) | " +
                "text column x<=0.44. queueRows={6} browseRows={7}",
                PrimaryX0, PrimaryX1, ClusterX1, PrimaryX0, ClusterX0, ClusterX1,
                _vm.QueueRows.Count, _vm.BrowseRows.Count));
        }

        private string FindSummary(ChannelId channel)
        {
            for (int i = 0; i < _vm.Channels.Count; i++)
                if (_vm.Channels[i].Channel == channel) return _vm.Channels[i].Describe();
            return BuildTimerService.ChannelWord(channel);
        }

        // ── Row factories (fixed-pixel bands) ─────────────────────────────────

        private RectTransform MakeRowHost(string name, float heightPx)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_listContent, false);
            var le = go.GetComponent<LayoutElement>();
            // BOTH the LayoutElement AND sizeDelta — the scroll column has childControlHeight off,
            // so a row that only sets one of them collapses to zero.
            le.preferredHeight = heightPx;
            le.minHeight = heightPx;
            le.flexibleWidth = 1f;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, heightPx);
            return rt;
        }

        /// <summary>
        /// WO-1058 — resolve the <paramref name="index"/>'th of <paramref name="count"/> SECONDARY
        /// controls inside <see cref="ClusterX0"/>..<see cref="ClusterX1"/>, evenly split with a
        /// fixed gutter. Even split is deliberate: it is the only division under which three
        /// controls all clear MinTouchPx at the narrowest supported aspect, so ClampMinTouch never
        /// fires and never inflates one control into its neighbour.
        /// </summary>
        private static void ClusterSlot(int index, int count, out Vector2 aMin, out Vector2 aMax)
        {
            if (count < 1) count = 1;
            float w = ((ClusterX1 - ClusterX0) - ClusterGapX * (count - 1)) / count;
            float x = ClusterX0 + index * (w + ClusterGapX);
            aMin = new Vector2(x, RowCtrlY0);
            aMax = new Vector2(x + w, RowCtrlY1);
        }

        private void AddSectionHeader(string text)
        {
            var row = MakeRowHost("SectionHeader", SectionHeaderPx);
            var t = ElarionUiKit.Label(row, ManageScreenVM.Ascii(text), 0f, 1f, ElarionUi.Gold,
                                       (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.01f, 0.99f, bold: true);
            ElarionUiKit.FitSingleLine(t);
        }

        private void AddNoteRow(string text)
        {
            var row = MakeRowHost("Note", SectionHeaderPx);
            var t = ElarionUiKit.Label(row, ManageScreenVM.Ascii(text), 0f, 1f, ElarionUi.ParchmentDim,
                                       (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.99f);
            ElarionUiKit.FitSingleLine(t);
        }

        private void OpenDefenseBuilder()
        {
            Close();
            var controller = BuildModeController.Instance ?? BuildModeController.EnsureExists();
            controller?.EnterBuildMode(DeNelle.Core.Catalog.BuildType.Defense);
        }

        private void AddActionNoteRow(string text, string action, Action onTap)
        {
            var row = MakeRowHost("ActionNote", RowHeightPx);
            var t = ElarionUiKit.Label(row, ManageScreenVM.Ascii(text), 0f, 1f, ElarionUi.Parchment,
                                       (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.74f);
            ElarionUiKit.FitSingleLine(t);
            var b = ElarionUiKit.BuildObsidianButton(row, action,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                // WO-1058: the Repair offer's CTA already sat on the primary band; it now names it
                // by constant so a future move of the slot moves every row type at once.
                new Vector2(PrimaryX0, RowCtrlY0), new Vector2(PrimaryX1, RowCtrlY1), () => onTap?.Invoke());
            ElarionUiKit.ClampMinTouch(b);
        }

        private void AddQueueRow(QueueRowVM r)
        {
            var row = MakeRowHost("QueueRow", RowHeightPx);

            // A stack CHILD is indented so the parent/child relationship reads structurally, not
            // by colour — the expanded items visibly belong to the xN header above them.
            float x0 = r.IsStackChild ? 0.06f : 0.02f;
            string label = (r.IsStackChild ? "- " : "") + ManageScreenVM.Ascii(r.Label ?? "");

            // THE ROW IS TWO COLUMNS, and they do not share pixels: TEXT left of x=0.44, CONTROLS
            // right of x=0.455. The left column is three stacked text lines (name / state / refund)
            // — build 1 put the refund line UNDER the button block at x 0.46-0.98 and the two
            // overprinted on every cancellable row.
            // WO-898 item 1 re-band. The three text lines shift UP inside the same row height to
            // free a strip at the bottom for the progress bar. Re-banding beats growing the row:
            // the list well is measured and clamps to 0px when the bands no longer fit, which
            // degrades to "headers and no rows" with only a trace line to explain it.
            //
            // WO-1058 CLIPPING PASS: the three bands below were authored at FontLabel(40) — a
            // ~46px line box — inside 34-37px bands, so every line bled over its band edge and the
            // owner's 2026-08-22 frame shows the title sheared. The bands are re-seated (see the
            // QRow* block) and each label is now capped at a size whose line box FITS, which is a
            // TEXT change, never a control one: MinTouchPx and the CTA boxes are untouched.
            var name = ElarionUiKit.Label(row, label, QRowNameY0, QRowNameY1, ElarionUi.Parchment,
                                          (int)QueueNameFontPx, TextAlignmentOptions.Left, x0, 0.44f, bold: true);
            ElarionUiKit.FitSingleLine(name, 0f, QueueNameFontPx);
            var state = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.StateText ?? ""), QRowStateY0, QRowStateY1,
                                           ElarionUi.ParchmentDim, (int)QueueLineFontPx,
                                           TextAlignmentOptions.Left, x0, 0.44f);
            ElarionUiKit.FitSingleLine(state, 0f, QueueLineFontPx);

            // The bar itself. Drawn only for a job with a known duration (Progress01 >= 0), and
            // deliberately NOT for a collapsed stack header, which stands for several jobs at
            // different points and would have to lie about one number.
            //
            // COLOURBLIND LAW: the fill is never the only signal - StateText already carries the
            // percentage in words ("Building - 2m 10s left (63% done)"), so the row reads correctly
            // with the bar ignored entirely.
            if (r.Progress01 >= 0f && !r.IsStackHeader)
            {
                var bar = ElarionUiKit.Bar(row, ElarionUiKit.BarKind.Castle,
                                           new Vector2(x0, QRowBarY0), new Vector2(0.44f, QRowBarY1));
                if (bar?.fill != null)
                {
                    bar.fill.fillAmount = Mathf.Clamp01(r.Progress01);
                    bar.fill.raycastTarget = false;
                }
                if (bar?.track != null) _progressCells.Add(new ProgressCell
                {
                    Handle = bar,
                    Channel = r.Channel,
                    JobId = r.JobId,
                    Queued = r.Queued,
                });
            }

            if (r.JobId != null && state != null)
                _tickCells.Add(new TickCell
                {
                    Text = state,
                    Channel = r.Channel,
                    JobId = r.JobId,
                    Queued = r.Queued,
                    PendingIndex = r.PendingIndex,
                });

            if (r.IsStackHeader)
            {
                // ⚠ RULING Q12 — A COLLAPSED xN CARD HAS NO CANCEL AND NO PAID FINISH.
                // Owner, verbatim: "can not cancel on a collapsed card, must expand then select
                // item to cancel and others automatically move up." A destructive or paid verb must
                // never act on an ambiguous aggregate (the same principle as Q11). The ONLY control
                // here is the expander; cancel appears on the individual children it reveals, and
                // the remaining items close the gap by themselves.
                //
                // WO-1058: the expander IS this row's primary, so it takes the PRIMARY SLOT like
                // every other row's primary. It used to start at 0.62 and straddle the slot — a
                // second tap landing on a stack header then hit an ambiguous strip. Now the whole
                // slot is one harmless, non-spending verb.
                string key = r.StackKey;
                var expand = ElarionUiKit.BuildObsidianButton(row, r.Expanded ? "Collapse" : "Expand x" + r.StackCount,
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(PrimaryX0, RowCtrlY0), new Vector2(PrimaryX1, RowCtrlY1),
                    () => _vm?.ToggleStack(key));
                ElarionUiKit.ClampMinTouch(expand);
                return;
            }

            string jobId = r.JobId;
            var channel = r.Channel;

            // FINISH NOW — offered on Builder, Train AND Research, on RUNNING and QUEUED jobs
            // alike (rulings Q5 + the "all channels" rule), and ALWAYS SHOWN while the job exists,
            // including when the player cannot afford it. The price is on the face as TEXT.
            if (r.FinishPrice > 0)
            {
                // TWO-LINE CTA (owner felt-test 2026-08-08): verb on top, cost UNDERNEATH in a
                // smaller font. The old face was "Finish 5c" / "Finish 5c (short)" — "5c" assumed
                // the player already knew that c meant crystals AND that the price scales with the
                // time remaining, and "(short)" silently meant "you cannot afford this" while
                // reading like part of the price. Both strings are the VM's (FinishCostText); this
                // only renders them.
                //
                // WO-1058: this is THE PRIMARY SLOT — the same strip of glass the browse row's
                // `Upgrade` occupies, so the owner's "tap, tap again" gesture lands on the verb she
                // wants without moving her finger. The verb reads "Finish Now" (not "Finish")
                // because in the primary slot it is answering the question the previous tap asked;
                // the cost line under it is unchanged and is what makes the second tap non-blind.
                var fin = BuildTwoLineCta(row, "Finish Now", r.FinishCostText,
                    r.CanAffordFinish ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(PrimaryX0, RowCtrlY0), new Vector2(PrimaryX1, RowCtrlY1),
                    () => { _vm?.FinishNow(channel, jobId); FlushNotice(); });
                ElarionUiKit.ClampMinTouch(fin);
            }

            // ── THE SECONDARY CLUSTER (WO-1058) ──────────────────────────────────────────
            // Everything that is NOT the primary lives LEFT of the dead gap, evenly split so no
            // control is authored under MinTouchPx. Order is fixed — Ad, Cancel, Move up — which
            // puts `Move up` between `Cancel` and the primary slot: the destructive control is
            // never adjacent to the one the player is double-tapping.
            bool wantAd = r.AdAvailable && DeNelle.Core.FeatureFlags.RewardedAdSkip;
            int clusterCount = (wantAd ? 1 : 0) + (r.CanCancel ? 1 : 0) + (r.CanBumpUp ? 1 : 0);
            int clusterIdx = 0;
            Vector2 slotMin, slotMax;

            // RELEASE BLOCKER GATE (2026-08-07): the "Ad" control is NEVER CONSTRUCTED while
            // FeatureFlags.RewardedAdSkip is OFF (the shipping state - no ad SDK is wired anywhere
            // in the project). Absent, not present-and-disabled. The VM and BuildTimerService gate
            // on the same flag; this is the build site, so it is the one that guarantees absence.
            // Its slot is RESERVED by the even split (it simply is not counted while the flag is
            // off), never drawn "disabled".
            if (wantAd)
            {
                ClusterSlot(clusterIdx++, clusterCount, out slotMin, out slotMax);
                var ad = ElarionUiKit.BuildObsidianButton(row, "Ad",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                    slotMin, slotMax,
                    () => { _vm?.WatchAd(channel, jobId); FlushNotice(); });
                ElarionUiKit.ClampMinTouch(ad);
            }

            if (r.CanCancel)
            {
                // Refund is 100% flat (ruling Q1) and the face SAYS what comes back, so the player
                // never has to infer it from a colour or a number that appears after the fact.
                // WO-1058 moved the BOX, not the promise: same Red face, same refund line, and it
                // is now the FURTHEST control from the primary slot instead of sitting inside it.
                ClusterSlot(clusterIdx++, clusterCount, out slotMin, out slotMax);
                var cancel = ElarionUiKit.BuildObsidianButton(row, "Cancel",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                    slotMin, slotMax,
                    () => { _vm?.Cancel(channel, jobId); FlushNotice(); });
                ElarionUiKit.ClampMinTouch(cancel);

                // Third line of the TEXT column (never under the buttons — see the two-column note).
                var refund = ElarionUiKit.Label(row, "Refund: " + ManageScreenVM.Ascii(r.RefundText ?? "nothing"),
                                                QRowRefundY0, QRowRefundY1, ElarionUi.ParchmentDim,
                                                (int)QueueLineFontPx, TextAlignmentOptions.Left, x0, 0.44f);
                ElarionUiKit.FitSingleLine(refund, 0f, QueueLineFontPx);
            }

            if (r.CanBumpUp)
            {
                int idx = r.PendingIndex;
                ClusterSlot(clusterIdx++, clusterCount, out slotMin, out slotMax);
                var up = ElarionUiKit.BuildObsidianButton(row, "Move up",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    slotMin, slotMax,
                    () => { _vm?.BumpUp(channel, jobId, idx); FlushNotice(); });
                ElarionUiKit.ClampMinTouch(up);
            }
        }

        // =====================================================================
        //  Two-line CTA (verb over cost)
        // =====================================================================

        /// <summary>Verb line size. Below FontBody(50) because it shares the box with a second
        /// line, and comfortably over the kit's mobile floor (<c>ElarionUiKit.FontFloor</c> = 30).</summary>
        private const float CtaVerbPx = 42f;

        /// <summary>Cost line size — SMALLER than the verb, as the owner asked, but still 2px over
        /// the floor. "Smaller" means smaller than the verb, never small enough to fail the floor.</summary>
        private const float CtaSubPx = 32f;

        // Band split inside the button box. At RowHeightPx 132 the control band (RowCtrlY0..Y1 =
        // 0.88) resolves to 116 reference px, so:
        //   verb 0.50-0.96 -> 0.46 * 116 = 53.4px, holding a 42px line box (~48px)   OK
        //   cost 0.06-0.46 -> 0.40 * 116 = 46.4px, holding a 32px line box (~37px)   OK
        // 99.8px of the 116 is spent, leaving ~16px of air top and bottom. The button's own touch
        // floor is unaffected: 116 >= MinTouchPx (112), so ClampMinTouch never grows it.
        private const float CtaVerbY0 = 0.50f, CtaVerbY1 = 0.96f;
        private const float CtaSubY0  = 0.06f, CtaSubY1  = 0.46f;

        /// <summary>
        /// An Obsidian CTA carrying a VERB over a smaller SUB-LINE, e.g. "Finish" / "5 crystals".
        ///
        /// Built here rather than in the kit because no kit button has a sub-label affordance — its
        /// <c>BuildObsidianButton</c> stamps ONE label across the whole face and FitSingleLine's it.
        /// This reuses that button whole (art, tint feedback, contrast law, touch floor) and only
        /// RESEATS the label it already made into the upper band, then adds the second line beneath
        /// in the SAME ink — so the sub-line inherits the kit's face-vs-label contrast rule instead
        /// of re-deriving it. If a two-line CTA is ever wanted elsewhere, THIS is the thing to lift
        /// into the kit; until then a second caller is the trigger, not a guess.
        ///
        /// COLOURBLIND LAW: the affordable/unaffordable difference is carried by the sub-line's TEXT
        /// ("5 crystals" vs "Short 3 crystals"). The Yellow/Gray face is a redundant second signal,
        /// never the only one — the owner is red/green colourblind.
        ///
        /// Both lines are floored at <c>ElarionUiKit.FontFloor</c> (30): FitSingleLine may shrink
        /// each toward the floor to fit the width, but can never take either below it — it
        /// ellipsizes instead of going sub-legible.
        /// </summary>
        private Button BuildTwoLineCta(Transform parent, string verb, string subLine,
            ElarionUiKit.ObsidianButtonColor color, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var btn = ElarionUiKit.BuildObsidianButton(parent, ManageScreenVM.Ascii(verb ?? ""),
                ElarionUiKit.ObsidianButtonStyle.Style1, color, anchorMin, anchorMax, onClick);
            if (btn == null) return null;

            // No sub-line to add: leave the kit's single centred label exactly as built.
            string sub = ManageScreenVM.Ascii(subLine ?? "");
            if (string.IsNullOrEmpty(sub)) return btn;

            var primary = btn.GetComponentInChildren<TMP_Text>();
            if (primary == null)
            {
                // The button exists but carries no label — the verb would be invisible and the cost
                // would have nothing to sit under. Say so rather than silently shipping a blank face.
                FlowTrace.Warn("Manage",
                    "two-line CTA '" + verb + "': the kit button has no TMP label, so the cost line '" +
                    sub + "' was not drawn. The face shows art only.");
                return btn;
            }

            var prt = primary.rectTransform;
            prt.anchorMin = new Vector2(prt.anchorMin.x, CtaVerbY0);
            prt.anchorMax = new Vector2(prt.anchorMax.x, CtaVerbY1);
            prt.offsetMin = new Vector2(prt.offsetMin.x, 0f);
            prt.offsetMax = new Vector2(prt.offsetMax.x, 0f);
            primary.fontSize = CtaVerbPx;
            ElarionUiKit.FitSingleLine(primary, ElarionUiKit.FontFloor, CtaVerbPx);

            var cost = ElarionUiKit.Label(btn.transform, sub, CtaSubY0, CtaSubY1,
                                          primary.color, (int)CtaSubPx,
                                          TextAlignmentOptions.Center, 0.04f, 0.96f);
            cost.raycastTarget = false;                 // the whole face stays one tap target
            ElarionUiKit.FitSingleLine(cost, ElarionUiKit.FontFloor, CtaSubPx);
            return btn;
        }

        private void AddBrowseRow(BrowseRowVM r)
        {
            var row = MakeRowHost("BrowseRow", RowHeightPx);

            // Three disjoint x-columns: name+cost (0.02-0.50) | affordability (0.52-0.73) | CTA (0.76-0.98).
            // WO-1058: the CTA moved LEFT from 0.84 to PrimaryX0 so it occupies the SAME primary
            // slot as the queue row's "Finish Now". The affordability column was pulled back from
            // 0.82 to 0.73 in the same edit — leaving it at 0.82 would have put a text box under
            // the widened button ("BUTTON OVER TEXT", the WO-1060 oracle's own failure class).
            var name = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.Label ?? ""), 0.52f, 0.98f, ElarionUi.Parchment,
                                          (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.50f, bold: true);
            ElarionUiKit.FitSingleLine(name);
            var cost = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.CostText ?? ""), 0.04f, 0.48f, ElarionUi.ParchmentDim,
                                          (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.50f);
            ElarionUiKit.FitSingleLine(cost);
            // Affordability is a SENTENCE ("Ready" / "Not enough Wood (400)") — never a tint alone.
            var state = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.StateText ?? ""), 0.20f, 0.80f, ElarionUi.Parchment,
                                           (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.52f, ClusterX1 + 0.01f);
            ElarionUiKit.FitBlock(state);   // a shortfall sentence may need two lines inside its box

            var act = ElarionUiKit.BuildObsidianButton(row, r.ActionText ?? "Open",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                r.Affordable ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(PrimaryX0, RowCtrlY0), new Vector2(PrimaryX1, RowCtrlY1),
                () => { Guard.Try("Manage", "browse drill-in", () => r.Activate?.Invoke()); });
            ElarionUiKit.ClampMinTouch(act);
        }

        // =====================================================================
        //  NOTICES + THE CHEAP TICK
        // =====================================================================

        private void FlushNotice()
        {
            if (_vm == null || string.IsNullOrEmpty(_vm.Notice)) return;
            string msg = ManageScreenVM.Ascii(_vm.Notice);
            bool broke = _vm.NoticeIsBrokeCase;
            _vm.ClearNotice();

            // In-panel first (the toast sorts below this modal), and traced either way so a headless
            // capture proves the outcome the player was shown.
            if (_noticeLabel != null) _noticeLabel.text = msg;
            FlowTrace.Step("Manage", "notice: " + msg);

            if (broke)
            {
                // The owner's broke-case rule: never a silent no-op — offer the route to crystals.
                // The store panel takes the screen, so the notice above is already on record.
                FlowTrace.Step("Manage", "broke case -> routing to the crystal store.");
                _vm.OpenCrystalStore();
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Time.unscaledTime < _tickAt) return;
            _tickAt = Time.unscaledTime + 1f;

            // CHEAP TICK: strings only. No row is destroyed, no layout is rebuilt, the rail
            // self-syncs. Rows come back only on QueueChanged.
            var svc = BuildTimerService.Instance;
            if (svc == null) return;
            for (int i = 0; i < _tickCells.Count; i++)
            {
                var cell = _tickCells[i];
                if (cell.Text == null) continue;
                double rem = svc.RemainingSeconds(cell.Channel, cell.JobId);
                // Ordinal("3rd"), matching the VM's build-time string. The tick used to write a raw
                // int here, so every row silently lost its ordinal one second after being built.
                cell.Text.text = cell.Queued
                    ? "Queued - " + ManageScreenVM.Ordinal(cell.PendingIndex + 1) + " in line (" + ManageScreenVM.FormatTime(rem) + " of work)"
                    : "Building - " + ManageScreenVM.FormatTime(rem) + " left" + ManageScreenVM.PercentSuffix(svc, cell.Channel, cell.JobId);
            }

            // WO-898 item 1: advance the fills on the same tick as the timers.
            for (int i = 0; i < _progressCells.Count; i++)
            {
                var pc = _progressCells[i];
                if (pc.Handle?.fill == null) continue;
                if (pc.Queued) continue;   // a queued job is 0% until it starts
                pc.Handle.fill.fillAmount = ManageScreenVM.ProgressOfLive(svc, pc.Channel, pc.JobId);
            }
            // Unity's null operator, NOT ?. — a rail destroyed by a list rebuild is C#-non-null but
            // Unity-null, and ?. would call Sync() straight into a MissingReferenceException.
            if (_rail != null) _rail.Sync();
        }
    }
}
