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
        // WO-1382 ruling #1 (2026-09-04): "Training becomes tappable and opens the existing queue
        // drawer." A tap target must clear MinTouchPx (112) WITHOUT ClampMinTouch growing it into
        // the list band below (a growth is a WO-1060 Assert A failure), so the strip is now a
        // touch-height band. Was 56 (one FontLabel line box + air); the 64px difference comes out
        // of the scrolling list on every tab, which is the honest price of a real tap target.
        private const float StripBandPx = 120f;     // band 1a: chips at 0.02-0.98 = 115px >= MinTouchPx
        private const float SlotBandPx = 120f;      // band 2: 0.96 * 120 = 115px button >= MinTouchPx
        private const float TabsBandPx = 0f;        // destination is already named in the title; Queue lives in that title row
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

        // =====================================================================
        //  WO-1382 (owner ruling 2026-09-04 22:50) — the TROOPS workspace bands, fixed px.
        // ---------------------------------------------------------------------
        // Rail (left, scrolls) + selected-troop card (right) share ONE row host; the
        // TRAINING NOW band is its own header row + one informational row per job. No mode
        // switch exists any more ("That should not be a mode switch"): the two verbs are two
        // buttons with two different words, TRAIN 1 <NAME> and UPGRADE TO L<n>.
        // =====================================================================
        // THE FOLD ARITHMETIC (measured off Builds/manage-capture.log, 2026-09-04): at 2670x1200
        // the list viewport is LIST=401 ref px (well 533 - fixed 132); the scroll zone pads 10 and
        // gaps rows by 8. Everything the "screen visibly reacts" ruling (#5) needs must sit above
        // that fold at scroll 0:  10 + 260 (workspace) + 8 + 120 (TRAINING NOW band with its first
        // job and OPEN QUEUE) = 398 <= 401. Only extra jobs (88 each) and the Saved-armies row
        // fall under the fold. 2340x1080 gives LIST=410 and 1920x1080 LIST=480, so it fits everywhere.
        private const float TroopWorkspacePx = 260f;      // rail + card row
        private const float TroopRailRowPx = 112f;        // one troop per rail row, == MinTouchPx
        private const float TrainingNowBandPx = 120f;     // label + first job + OPEN QUEUE, one row
        private const float TrainingNowRowPx = 88f;       // extra jobs, informational only - no control
        private const float TroopCtaY0 = 0.01f, TroopCtaY1 = 0.445f;   // 0.435 * 260 = 113.1px >= MinTouchPx
        private const float BandCtrlY0 = 0.03f, BandCtrlY1 = 0.97f;   // 0.94 * 120 = 112.8px >= MinTouchPx

        private ManageScreenVM _vm;
        private int _browsePage;
        private string _selectedTroopId;
        private GameObject _ui;
        private RectTransform _listContent;
        private GameObject _operationalListBand;
        private RectTransform _operationalWell;
        private RectTransform _launcherHost;
        private RectTransform _launcherGrid;
        private Button _workspaceBack;
        private TextMeshProUGUI _workspaceTitle;
        private readonly TextMeshProUGUI[] _launcherBadges = new TextMeshProUGUI[4];
        private bool _categoryNavigationCommitted;
        private RectTransform _railBand;            // non-null only while the rail is PINNED
        // WO-1368 — the drawer's OWN scroll content. The queue VERBS live here, never in the
        // browse list (see RenderQueueDrawer).
        private RectTransform _drawerContent;
        // WO-1368 — the row factory's current parent. Null => the browse list (_listContent).
        // Set for the duration of a drawer render so AddQueueRow &c. can be reused verbatim
        // instead of being forked into a second, drift-prone copy.
        private RectTransform _rowParent;
        private GameObject _queueDrawer;
        private Button _queueDrawerToggle;
        private bool _queueDrawerOpen;
        private RectTransform _tabsHost;
        private readonly TextMeshProUGUI[] _stripCells = new TextMeshProUGUI[3];
        private RectTransform _stripHost;
        private readonly TextMeshProUGUI[] _launcherSummaries = new TextMeshProUGUI[3];
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

        // WO-1382 — the TRAINING NOW band's "<n>s left" cells. Its own list, NOT _tickCells:
        // the queue-row tick writes the drawer's "Building - 2m 10s left (63% done)" grammar and
        // the band's cell is the short form the owner's mockup shows. Same 1 Hz tick, strings only.
        private readonly List<TrainingNowCell> _trainingNowCells = new List<TrainingNowCell>(8);

        private struct TrainingNowCell
        {
            public TextMeshProUGUI Text;
            public ChannelId Channel;
            public string JobId;
        }

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
            RenderLauncherCards();
            ShowLauncher();

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
                ShowOperational(ManageTab.Defense);
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
            _trainingNowCells.Clear();
            _rail = null;
            _listContent = null;
            _operationalListBand = null;
            _operationalWell = null;
            _launcherHost = null;
            _launcherGrid = null;
            _workspaceBack = null;
            _workspaceTitle = null;
            for (int i = 0; i < _launcherBadges.Length; i++) _launcherBadges[i] = null;
            _categoryNavigationCommitted = false;
            _railBand = null;
            _drawerContent = null;
            _rowParent = null;
            _queueDrawer = null;
            _queueDrawerToggle = null;
            _queueDrawerOpen = false;
            _railPinned = false;
            _tabsHost = null;
            for (int i = 0; i < _stripCells.Length; i++) _stripCells[i] = null;
            _stripHost = null;
            for (int i = 0; i < _launcherSummaries.Length; i++) _launcherSummaries[i] = null;
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
            // Presentation is shared with Pause/Settings rather than reimplemented per screen.
            // Commands, zones, timers, and authoritative queue state remain owned here.
            MedievalUiSkin.ApplyShell(chrome);
            _workspaceTitle = chrome.title;

            // The approved Manage modal is one continuous obsidian field. FrameCore is
            // border-heavy and its transparent centre exposed the world around the troop
            // workspace, especially below/right of the scroll content. Seat a full content
            // backing behind every drop-zone; the ornate outer frame remains untouched.
            if (chrome.content != null)
            {
                var fill = ElarionUiKit.AddImage(chrome.content.transform, "ManageBodyFill",
                    Vector2.zero, Vector2.one, ElarionUiKit.ObsidianFill, rounded: false);
                var fillImage = fill != null ? fill.GetComponent<Image>() : null;
                if (fillImage != null) fillImage.raycastTarget = false;
                if (fill != null) fill.transform.SetAsFirstSibling();
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
            _operationalWell = well;
            float wellPx = Mathf.Max(0f, (bodyTop - bodyFloor) * panelPx);

            // ── Band 1a: the ALL-THREE-LINES strip. Every channel stays glanceable on every tab,
            //    as TEXT, so the player never loses sight of a line the current tab does not own.
            //    It seats in the frame's own SUB-HEADER band when the frame has one (free real
            //    estate ABOVE the well — it costs the list nothing); otherwise it takes a band.
            // The approved Manage language treats the three production lines as real status
            // cards directly beneath the title. The legacy frame's sub-header seat is too shallow
            // and is partially covered by the ornate shell, which made the strip disappear in
            // Seeker captures. Spend an explicit body band so the summaries remain visible and
            // stable at every supported ratio.
            RectTransform subHeader = null;
            bool stripInBody = true;
            float stripPx = StripBandPx;

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
            // F8 2026-08-31: the queue rail and permanent-builder offer are secondary controls.
            // They live in an on-demand side drawer and spend no vertical tower-browse space.
            float fixedNoRail = stripCost + noticeCost;

            // The rail is the ONE elastic element: 200 fixed px of card art whose every fact is
            // already on the strip (line status) and on the rows below (per-job label, countdown,
            // controls). It keeps its own PINNED band only while the well can still seat a usable
            // list underneath; otherwise it is demoted into the scroll list as its first row —
            // deliberately scrolled, never overlapped, and said out loud in the trace below.
            _railPinned = true; // pinned inside the drawer, never injected into the browse list
            float fixedPx = fixedNoRail;
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
                         - TabsBandPx
                         - (noticeBesideClose ? 0f : NoticeBandPx);
            FlowTrace.Step("Manage", string.Format(
                "bands(px): canvas={0:0} panel={1:0} well={2:0} || strip={3:0}[{4}] rail={5:0}[{6}] " +
                "slot={7:0} tabs={8:0} notice={9:0}[{10}] gaps={11:0} => fixed={12:0} LIST={13:0} (floor {14:0})",
                canvasH, panelPx, wellPx, stripPx, stripInBody ? "body" : "sub-header",
                _railBandPx, "side-drawer",
                0f, TabsBandPx,
                noticeBesideClose ? NoticeCloseBandPx : NoticeBandPx,
                noticeBesideClose ? "close-band" : "body",
                gapsPx, fixedPx, listPx, MinListPx));

            // ── LAY THE BANDS. One cursor, top-down, gutter after every band. Nothing here can
            //    overlap anything else: each band's height is pixels it OWNS.
            float cursor = 0f;
            BuildStrip(stripInBody ? Band(well, "Band_ChannelStrip", ref cursor, StripBandPx) : subHeader);
            // The title already reads MANAGE - {DESTINATION}. Repeating that destination in a
            // full touch-height body band spent the first fold without adding information.
            // Queue is a title-row action in the approved reference, opposite Back.
            _tabsHost = MakeZone(chrome.content.transform, "ManageHeaderActions",
                new Vector2(0f, 0.835f), new Vector2(1f, 0.965f));
            BuildTabs();

            var listBand = Band(well, "Band_List", ref cursor, listPx);
            _operationalListBand = listBand.gameObject;
            var scroll = ElarionUiKit.MakeScrollZone(listBand, spacing: 8f, padding: 10);
            _listContent = scroll != null ? scroll.content : null;
            if (_listContent == null)
                FlowTrace.Fail("Manage", "MakeScrollZone returned no content — the list host is missing.");

            BuildNotice(noticeBesideClose
                ? NoticeSeatBesideClose(chrome.content.transform, noticeX1)
                : Band(well, "Band_Notice", ref cursor, NoticeBandPx));

            BuildQueueDrawer(well);

            BuildLauncher(well);
            _workspaceBack = ElarionUiKit.BuildObsidianButton(chrome.content.transform, "BACK",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.035f, 0.835f), new Vector2(0.205f, 0.965f), ShowLauncher);
            if (_workspaceBack != null)
            {
                _workspaceBack.gameObject.name = "ManageWorkspaceBack";
                ElarionUiKit.ClampMinTouch(_workspaceBack);
                MedievalUiSkin.ApplyButton(_workspaceBack);
                _workspaceBack.gameObject.SetActive(false);
            }
        }

        private void BuildLauncher(RectTransform operationalWell)
        {
            if (operationalWell == null || operationalWell.parent == null) return;
            var go = new GameObject("ManageCategoryLauncher", typeof(RectTransform), typeof(Image));
            _launcherHost = (RectTransform)go.transform;
            _launcherHost.SetParent(operationalWell.parent, false);
            _launcherHost.anchorMin = operationalWell.anchorMin;
            _launcherHost.anchorMax = operationalWell.anchorMax;
            _launcherHost.offsetMin = operationalWell.offsetMin;
            _launcherHost.offsetMax = operationalWell.offsetMax;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.012f, 0.014f, 0.018f, 0.995f);

            BuildLauncherSummaries();
            var pathHeading = ElarionUiKit.Label(_launcherHost, "Choose a path", 0.705f, 0.825f,
                ElarionUi.Parchment, (int)ElarionUi.FontTitle, TextAlignmentOptions.Center,
                0.04f, 0.96f, bold: true);
            pathHeading.fontSize = 52f;
            ElarionUiKit.FitSingleLine(pathHeading, 40f, 52f);

            var gridGo = new GameObject("ManageCategoryGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            var grid = (RectTransform)gridGo.transform;
            _launcherGrid = grid;
            grid.SetParent(_launcherHost, false);
            // The launcher shares chrome with the standard bottom Close. Reserve
            // that thumb band explicitly; measured captures proved .04 let row two
            // occupy the same glass as Close.
            grid.anchorMin = new Vector2(0.03f, 0.055f);
            grid.anchorMax = new Vector2(0.97f, 0.695f);
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            var layout = gridGo.GetComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.spacing = new Vector2(24f, 20f);
            layout.padding = new RectOffset(14, 14, 14, 14);
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(1f, grid.rect.width - layout.padding.horizontal - layout.spacing.x);
            float height = Mathf.Max(1f, grid.rect.height - layout.padding.vertical - layout.spacing.y);
            layout.cellSize = new Vector2(width * 0.5f, height * 0.5f);

        }

        private void RenderLauncherCards()
        {
            if (_launcherGrid == null || _vm == null) return;
            for (int i = _launcherGrid.childCount - 1; i >= 0; i--)
                Destroy(_launcherGrid.GetChild(i).gameObject);

            ManageTab[] tabs =
            {
                ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research
            };
            for (int i = 0; i < tabs.Length; i++)
            {
                ManageTab captured = tabs[i];
                bool available = captured == ManageTab.Defense || captured == ManageTab.Research
                    || _vm.VisibleTabs.Contains(captured);
                // BarracksUnlock is the one runtime authority used by the building,
                // drillmaster and training door. Do not derive this from a second flag.
                if (captured == ManageTab.Troops) available = BarracksUnlock.IsUnlocked;
                string title = ManageScreenVM.TabLabels[(int)captured];
                string purpose = captured == ManageTab.Troops && !available
                    ? "Build a Barracks to unlock" : PurposeFor(captured);
                var card = ElarionUiKit.BuildObsidianButton(_launcherGrid, title.ToUpperInvariant(),
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    !available ? ElarionUiKit.ObsidianButtonColor.Gray : captured == ManageTab.Defense
                        ? ElarionUiKit.ObsidianButtonColor.Red
                        : ElarionUiKit.ObsidianButtonColor.Yellow,
                    Vector2.zero, Vector2.one, () => ActivateLauncherCard(captured));
                if (card == null) continue;
                // Locked cards remain tappable so the refusal is explicit. Navigation
                // is blocked in ActivateLauncherCard; a disabled Button would fail silently.
                card.interactable = true;
                card.gameObject.name = "ManageCard_" + title;
                card.transition = Selectable.Transition.ColorTint;
                var colors = card.colors;
                colors.normalColor = available ? Color.white : new Color(0.42f, 0.42f, 0.42f, 1f);
                colors.highlightedColor = available ? new Color(1f, 0.94f, 0.78f, 1f) : colors.normalColor;
                colors.pressedColor = available ? new Color(0.78f, 0.68f, 0.48f, 1f) : new Color(0.50f, 0.50f, 0.50f, 1f);
                card.colors = colors;

                // The approved kit cards are text-safe layered faces: illustration and
                // border are art, while title, purpose, count and interaction remain live.
                // Put the sprite on the Button's own target graphic so its full rectangle
                // remains the hit target and ColorTint supplies focus/press feedback.
                var cardFace = card.GetComponent<Image>();
                var cardSprite = Resources.Load<Sprite>(LauncherArtPath(captured));
                if (cardFace != null && cardSprite != null)
                {
                    cardFace.sprite = cardSprite;
                    cardFace.type = Image.Type.Simple;
                    cardFace.preserveAspect = false; // kit card aspect is authored for this seat
                    cardFace.color = Color.white;
                    card.targetGraphic = cardFace;
                }

                var face = card.GetComponentInChildren<TMP_Text>();
                if (face != null)
                {
                    var rt = face.rectTransform;
                    rt.anchorMin = new Vector2(0.49f, 0.55f);
                    rt.anchorMax = new Vector2(0.96f, 0.90f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    face.fontSize = 36f;
                    face.alignment = TextAlignmentOptions.Center;
                    face.color = available ? ElarionUi.Gold : ElarionUi.ParchmentDim;
                    ElarionUiKit.FitSingleLine(face, 30f, 40f);
                }
                var description = ElarionUiKit.Label(card.transform, purpose, 0.26f, 0.52f,
                    available ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                    (int)ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.49f, 0.96f);
                ElarionUiKit.FitSingleLine(description, 24f, 30f);

                if (!available && captured == ManageTab.Troops)
                    BuildLockBadge(card.transform);

            }
        }

        private void BuildLauncherSummaries()
        {
            if (_launcherHost == null) return;
            const float gap = 0.018f;
            float w = (0.94f - gap * 2f) / 3f;
            for (int i = 0; i < _launcherSummaries.Length; i++)
            {
                float x0 = 0.03f + i * (w + gap);
                var panel = new GameObject("LauncherSummary_" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)panel.transform;
                rt.SetParent(_launcherHost, false);
                rt.anchorMin = new Vector2(x0, 0.835f);
                rt.anchorMax = new Vector2(x0 + w, 0.985f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var panelImage = panel.GetComponent<Image>();
                panelImage.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/status-panel-icon-socket");
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
                panelImage.color = Color.white;

                var iconGo = new GameObject("LineIcon", typeof(RectTransform), typeof(Image));
                var iconRt = (RectTransform)iconGo.transform;
                iconRt.SetParent(rt, false);
                iconRt.anchorMin = new Vector2(0.015f, 0.04f);
                iconRt.anchorMax = new Vector2(0.28f, 0.96f);
                iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
                var icon = iconGo.GetComponent<Image>();
                icon.sprite = Resources.Load<Sprite>(LauncherSummaryIconPath(i));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                _launcherSummaries[i] = ElarionUiKit.Label(rt, "", 0.08f, 0.92f,
                    ElarionUi.Parchment, (int)ElarionUi.FontMicro,
                    TextAlignmentOptions.Center, 0.28f, 0.97f, bold: true);
                ElarionUiKit.FitBlock(_launcherSummaries[i], 28f, 34f);
            }
        }

        private static string LauncherSummaryIconPath(int index)
        {
            switch (index)
            {
                case 0: return "UI/ElarionMedieval/icons/builder";
                case 1: return "UI/ElarionMedieval/icons/training";
                default: return "UI/ElarionMedieval/icons/research";
            }
        }

        private static string LauncherArtPath(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Defense: return "UI/ElarionMedieval/cards/defense";
                case ManageTab.Buildings: return "UI/ElarionMedieval/cards/buildings";
                case ManageTab.Troops: return "UI/ElarionMedieval/cards/troops-locked";
                case ManageTab.Research: return "UI/ElarionMedieval/cards/research";
                default: return "UI/ElarionMedieval/cards/buildings";
            }
        }

        private static void BuildLockBadge(Transform parent)
        {
            var plate = new GameObject("LockedPadlock", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)plate.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.345f, 0.20f);
            rt.anchorMax = new Vector2(0.50f, 0.76f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = plate.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/badges/lock-badge");
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void ActivateLauncherCard(ManageTab tab)
        {
            if (_categoryNavigationCommitted) return;
            if (tab == ManageTab.Troops && !BarracksUnlock.IsUnlocked)
            {
                ElarionUiKit.ShowToast("Build a Barracks to unlock Troops.", ElarionUiKit.ToastTone.Info);
                FlowTrace.Step("Manage", "Troops card blocked - BarracksUnlock.IsUnlocked=false");
                return;
            }
            _categoryNavigationCommitted = true;
            ShowOperational(tab);
        }

        private void RenderLauncherBadges()
        {
            if (_vm == null) return;
            for (int i = 0; i < _launcherBadges.Length; i++)
            {
                var badge = _launcherBadges[i];
                if (badge == null) continue;
                ChannelId channel = ManageScreenVM.ChannelOf((ManageTab)i);
                int depth = 0, cap = 5;
                for (int j = 0; j < _vm.Channels.Count; j++)
                    if (_vm.Channels[j].Channel == channel)
                    { depth = _vm.Channels[j].Depth; cap = _vm.Channels[j].DepthCap > 0 ? _vm.Channels[j].DepthCap : 5; break; }
                badge.text = depth + "/" + cap;
            }
        }

        private static string LockedPurposeFor(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Buildings: return "LOCKED - place a town building";
                case ManageTab.Troops: return "LOCKED - build a Barracks";
                case ManageTab.Research: return "LOCKED - build a research structure";
                default: return "LOCKED - place a defensive structure";
            }
        }

        private static string PurposeFor(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Defense: return "Towers, walls & gates";
                case ManageTab.Buildings: return "Town structures & upgrades";
                case ManageTab.Troops: return "Train and improve your army";
                case ManageTab.Research: return "Discover realm advancements";
                default: return "Open this management line";
            }
        }

        private void ShowLauncher()
        {
            _categoryNavigationCommitted = false;
            if (_launcherHost != null) _launcherHost.gameObject.SetActive(true);
            if (_operationalWell != null) _operationalWell.gameObject.SetActive(false);
            if (_stripHost != null) _stripHost.gameObject.SetActive(false);
            if (_workspaceBack != null) _workspaceBack.gameObject.SetActive(false);
            if (_workspaceTitle != null) _workspaceTitle.text = "MANAGE";
            FlowTrace.Step("Navigation", "Manage Back/root -> category cards");
        }

        private void ShowOperational(ManageTab tab)
        {
            _browsePage = 0;
            if (_launcherHost != null) _launcherHost.gameObject.SetActive(false);
            if (_operationalWell != null) _operationalWell.gameObject.SetActive(true);
            if (_stripHost != null) _stripHost.gameObject.SetActive(true);
            if (_workspaceBack != null) _workspaceBack.gameObject.SetActive(true);
            if (_workspaceTitle != null)
            {
                _workspaceTitle.text = "MANAGE - " + ManageScreenVM.TabLabels[(int)tab].ToUpperInvariant();
                var titleRt = _workspaceTitle.rectTransform;
                titleRt.anchorMin = new Vector2(0.22f, titleRt.anchorMin.y);
                titleRt.anchorMax = new Vector2(0.78f, titleRt.anchorMax.y);
                titleRt.offsetMin = new Vector2(0f, titleRt.offsetMin.y);
                titleRt.offsetMax = new Vector2(0f, titleRt.offsetMax.y);
                ElarionUiKit.FitSingleLine(_workspaceTitle, 34f, 52f);
            }
            _vm?.SelectTab(tab);
            FlowTrace.Step("Navigation", "Manage category card -> " + tab);
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
            _stripHost = host;
            const float gap = 0.014f;
            float w = (1f - gap * 2f) / 3f;
            for (int i = 0; i < _stripCells.Length; i++)
            {
                float x = i * (w + gap);
                var panel = new GameObject("ManageLineStatus_" + i, typeof(RectTransform), typeof(Image));
                var panelRt = (RectTransform)panel.transform;
                panelRt.SetParent(host, false);
                // 0.02-0.98 of the 120px band = 115px: the Training chip's tap target clears the
                // touch floor by construction, so ClampMinTouch never fires on it (WO-1382).
                panelRt.anchorMin = new Vector2(x, 0.02f);
                panelRt.anchorMax = new Vector2(x + w, 0.98f);
                panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
                var panelImage = panel.GetComponent<Image>();
                panelImage.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/status-panel-icon-socket");
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
                panelImage.color = Color.white;
                panelImage.raycastTarget = false;

                var iconGo = new GameObject("LineIcon", typeof(RectTransform), typeof(Image));
                var iconRt = (RectTransform)iconGo.transform;
                iconRt.SetParent(panelRt, false);
                iconRt.anchorMin = new Vector2(0.015f, 0.04f);
                iconRt.anchorMax = new Vector2(0.27f, 0.96f);
                iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
                var icon = iconGo.GetComponent<Image>();
                icon.sprite = Resources.Load<Sprite>(LauncherSummaryIconPath(i));
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var t = ElarionUiKit.Label(panelRt, "", 0.08f, 0.92f, ElarionUi.Parchment,
                                           (int)ElarionUi.FontMicro, TextAlignmentOptions.Center,
                                           0.27f, 0.97f, bold: true);
                // Builders / Research carry line occupancy only. The TRAINING chip is the one
                // exception (WO-1382 ruling #1): it shows the line's depth and is TAPPABLE - the
                // longer copy is why the fit floor is 24 here rather than the old 28.
                ElarionUiKit.FitSingleLine(t, 24f, 34f);
                _stripCells[i] = t;

                if (i == 1)
                {
                    // WO-1382 ruling #1: "Training becomes tappable and opens the existing queue
                    // drawer." A transparent kit plate carrying the Button, over the chip - the
                    // same door as the title-row QUEUE face (ToggleQueueDrawer), never a second
                    // queue surface. Named ManageLineStatus_* so the bulk medieval-skin pass skips
                    // it (it has no label of its own and would otherwise be painted as a gold CTA).
                    var tapGo = ElarionUiKit.AddImage(panelRt, "ManageLineStatus_TrainTap",
                        Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), rounded: false);
                    var tapImage = tapGo.GetComponent<Image>();
                    tapImage.raycastTarget = true;
                    var tap = tapGo.AddComponent<Button>();
                    tap.targetGraphic = tapImage;
                    tap.transition = Selectable.Transition.None;
                    tap.onClick.AddListener(ToggleQueueDrawer);
                    ElarionUiKit.ClampMinTouch(tap);
                }
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

        /// <summary>
        /// The opt-in home for queue inspection AND queue ADMINISTRATION.
        ///
        /// <para>⛔ WO-1368 — THE MONEY PATH LIVED NOWHERE FOR THREE DAYS. Commit 486cd7b17
        /// (2026-09-01) removed the only call to <see cref="AddQueueRow"/> — the method that builds
        /// <c>Finish Now</c>, <c>Ad</c>, <c>Cancel</c> and <c>Move up</c> — and moved queue actions
        /// to "the explicit header Queue drawer". But this drawer held only the DISPLAY-ONLY rail
        /// and the Buy-Builder offer, and <see cref="MountRail"/>'s own comment says the rail's
        /// cards are raycast-off because "every action lives on the rows". The rows it deferred to
        /// were deleted in the same change, so the crystal sink and the rewarded-ad surface were
        /// both unreachable while <c>queueRows=2</c> was being logged correctly all morning.
        /// (Owner, on the production candidate: "i dont see the watch ad or pay crtystals to
        /// complete early stuff".)</para>
        ///
        /// <para>⭐ The 2026-08-31 ruling this drawer exists for — "tower browsing leads; queue
        /// administration is OPT-IN" — is UNCHANGED and is why the verbs are not simply put back
        /// inline: inline queue rows made the browse list overflow at landscape height. The verbs
        /// return HERE, behind the QUEUE affordance, which is where <see cref="MountRail"/> already
        /// said they lived. <c>ManageQueueDrawerRegression</c> is re-pointed to pin that shape —
        /// rows drawer-only AND present — rather than to pin their absence.</para>
        ///
        /// <para>LAYOUT: heading / scrolling queue list / Buy-Builder offer. The rail is the FIRST
        /// ROW of that list rather than a fixed band, reusing the proven demoted-rail pattern
        /// (<see cref="RenderList"/>): it keeps its full fixed <see cref="QueueRailView.Height"/>,
        /// scrolls with the rows, and cannot overprint the row beneath it at any well height.</para>
        /// </summary>
        private void BuildQueueDrawer(RectTransform well)
        {
            if (well == null) return;

            _queueDrawer = new GameObject("ManageQueueDrawer", typeof(RectTransform), typeof(Image));
            var drawer = (RectTransform)_queueDrawer.transform;
            drawer.SetParent(well, false);
            // Expanded is a genuine workspace state, not a translucent fly-over. It owns the
            // full body beneath the persistent channel strip so the queue cards have mobile-safe
            // width and the browse list cannot remain visually/actionably alive underneath it.
            drawer.anchorMin = new Vector2(0.02f, 0.02f);
            drawer.anchorMax = new Vector2(0.998f, 0.84f);
            drawer.offsetMin = drawer.offsetMax = Vector2.zero;
            var drawerImage = _queueDrawer.GetComponent<Image>();
            drawerImage.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            drawerImage.type = Image.Type.Sliced;
            drawerImage.color = Color.white;

            var heading = ElarionUiKit.Label(drawer, "BUILDERS / QUEUE", 0.87f, 0.98f,
                ElarionUi.Gold, (int)ElarionUi.FontLabel, TextAlignmentOptions.Left,
                0.04f, 0.66f, bold: true);
            ElarionUiKit.FitSingleLine(heading);
            var hide = ElarionUiKit.BuildObsidianButton(drawer, "HIDE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.70f, 0.70f), new Vector2(0.97f, 0.99f), ToggleQueueDrawer);
            ElarionUiKit.ClampMinTouch(hide);

            // WO-1368: the rail no longer owns a fixed band of its own — it is mounted as the
            // first row of the list below (see RenderQueueDrawer), so the 200px of card art can
            // never eat the space the ACTION rows need. _railBand stays null, which is what makes
            // the legacy pinned path (RenderRail) inert.
            _railBand = null;
            var drawerList = MakeZone(drawer, "Drawer_QueueList",
                new Vector2(0.02f, 0.30f), new Vector2(0.98f, 0.86f));
            var drawerScroll = ElarionUiKit.MakeScrollZone(drawerList, spacing: 8f, padding: 10);
            _drawerContent = drawerScroll != null ? drawerScroll.content : null;
            if (_drawerContent == null)
                FlowTrace.Fail("Manage",
                    "queue drawer MakeScrollZone returned no content - the queue ROWS have no build " +
                    "site, which is exactly the WO-1368 defect (Finish Now / Ad / Cancel / Move up " +
                    "unreachable). The rail alone carries no actions.");

            BuildSlotRow(MakeZone(drawer, "Drawer_SlotOffer",
                new Vector2(0.035f, 0.03f), new Vector2(0.965f, 0.27f)));

            _queueDrawer.SetActive(false);
        }

        private void ToggleQueueDrawer()
        {
            _queueDrawerOpen = !_queueDrawerOpen;
            if (_queueDrawer != null) _queueDrawer.SetActive(_queueDrawerOpen);
            if (_operationalListBand != null) _operationalListBand.SetActive(!_queueDrawerOpen);
            if (_queueDrawerToggle != null) _queueDrawerToggle.gameObject.SetActive(!_queueDrawerOpen);
            // WO-1368: hiding the browse band while the drawer is open STILL holds, and now holds
            // for a stronger reason than when it was written. The drawer is a full-body workspace
            // (anchors 0.02-0.84 of the well) and it now carries DESTRUCTIVE and PAID verbs; a
            // browse list left actionable underneath an opaque panel is a mis-tap surface. Opt-in
            // is preserved by the QUEUE affordance, not by leaving both surfaces alive at once.
            if (_queueDrawerOpen) RenderQueueDrawer();
            FlowTrace.Step("Manage", "queue drawer " + (_queueDrawerOpen ? "expanded" : "collapsed") +
                " (rows " + (_queueDrawerOpen ? (_vm != null ? _vm.QueueRows.Count : 0) : 0) + ")");
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
            for (int i = _tabsHost.childCount - 1; i >= 0; i--)
            {
                var child = _tabsHost.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            if (_vm == null || _vm.VisibleTabs.Count == 0)
            {
                ElarionUiKit.Label(_tabsHost, "Place a structure to unlock Manage categories", 0f, 1f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, 0f, 1f);
                return;
            }
            _queueDrawerToggle = ElarionUiKit.BuildObsidianButton(_tabsHost, "QUEUE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.795f, 0f), new Vector2(0.965f, 1f), ToggleQueueDrawer);
            if (_queueDrawerToggle != null)
            {
                _queueDrawerToggle.gameObject.name = "ManageQueueDrawerToggle";
                _queueDrawerToggle.gameObject.SetActive(!_queueDrawerOpen && _vm.Channels.Count > 0);
                ElarionUiKit.ClampMinTouch(_queueDrawerToggle);
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
                // WO-1368 — AFTER RenderList, which clears the tick/progress cells. The drawer's
                // rows register their own countdown cells and must survive that clear.
                if (_queueDrawerOpen) RenderQueueDrawer();
                Canvas.ForceUpdateCanvases();
                if (_listContent != null)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
                if (_drawerContent != null)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_drawerContent);
                ApplyOperationalMedievalSkin();
            });
            FlushNotice();
            // Capacity is already explicit in the three persistent channel chips. Do not add
            // a duplicate session-complete sentence beside Close; that footer seat is reserved
            // for actionable command feedback only.
        }

        private void ApplyOperationalMedievalSkin()
        {
            if (_ui == null) return;
            var buttons = _ui.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null) continue;
                string objectName = button.gameObject.name ?? string.Empty;
                if (string.Equals(objectName, "Scrim", StringComparison.Ordinal) ||
                    string.Equals(objectName, "CloseButton", StringComparison.Ordinal) ||
                    objectName.StartsWith("ManageCard_", StringComparison.Ordinal) ||
                    objectName.StartsWith("TroopChoice_", StringComparison.Ordinal) ||
                    // WO-1382: the two card CTAs are skinned by their builder (TRAIN primary,
                    // UPGRADE secondary) - the copy-keyed pass below would promote "UPGRADE TO L2"
                    // to primary and erase the one-primary hierarchy the owner asked for. The
                    // Training-chip tap plate has no label and must never be painted as a CTA.
                    objectName.StartsWith("TroopCta_", StringComparison.Ordinal) ||
                    objectName.StartsWith("ManageLineStatus_", StringComparison.Ordinal)) continue;

                var label = button.GetComponentInChildren<TMP_Text>(true);
                string copy = label != null ? label.text ?? string.Empty : string.Empty;
                bool primary = copy.IndexOf("BUILD NEW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("BUILD DEFENSE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("OPEN BUILD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("TRAIN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("RESEARCH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("UPGRADE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("FINISH NOW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               copy.IndexOf("BUY BUILDER", StringComparison.OrdinalIgnoreCase) >= 0;
                MedievalUiSkin.ApplyButton(button, primary);
            }

            var trackSprite = Resources.Load<Sprite>("UI/ElarionMedieval/progress/progress-track-empty");
            if (trackSprite != null)
            {
                foreach (var image in _ui.GetComponentsInChildren<Image>(true))
                {
                    if (image == null || image.gameObject.name.IndexOf("Track", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    image.sprite = trackSprite;
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
            }
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
                    ? _vm.Channels[i].Name + " " + _vm.Channels[i].Busy + "/" + _vm.Channels[i].Slots
                    : (i == 0 ? "Builders 0/0" : i == 1 ? "Training 0/0" : "Research 0/0");
                // WO-1382 ruling #1: the Training chip carries the line's DEPTH ("Training 1/2 .
                // 1/5 queued") - the VM composes it; this only paints it.
                if (i == 1 && _vm.TrainingChipText != null) text = _vm.TrainingChipText;
                cell.text = ManageScreenVM.Ascii(text);
            }
            for (int i = 0; i < _launcherSummaries.Length; i++)
            {
                var cell = _launcherSummaries[i];
                if (cell == null) continue;
                if (i < _vm.Channels.Count)
                {
                    ChannelSummary s = _vm.Channels[i];
                    cell.text = s.Name + " " + s.Busy + "/" + s.Slots;
                }
                else
                {
                    string name = i == 0 ? "Builders" : i == 1 ? "Training" : "Research";
                    cell.text = name + " 0/0";
                }
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
            // WO-1368: inside the drawer _railBand is null by construction, so this is inert there
            // and RenderQueueDrawer owns the rail. Kept because the demoted/pinned split is still
            // real for the browse list.
            if (!_railPinned || _railBand == null) return;
            MountRail(_railBand, forceRebuild: false);
        }

        /// <summary>
        /// ⛔ WO-1368 — THE BUILD SITE FOR THE QUEUE VERBS. This is the only caller of
        /// <see cref="AddQueueRow"/>, and it is deliberately DRAWER-ONLY: the 2026-08-31 ruling
        /// keeps the browse list free of queue rows, and this method keeps the verbs from having
        /// nowhere at all to be built (the three-day state in which <c>Finish Now</c> and
        /// <c>Ad</c> existed in code and rendered nowhere).
        ///
        /// <para>Called AFTER <see cref="RenderList"/> in <see cref="Render"/>, because RenderList
        /// clears <c>_tickCells</c> / <c>_progressCells</c> — rows built before it would keep their
        /// buttons but silently lose their countdowns.</para>
        /// </summary>
        private void RenderQueueDrawer()
        {
            if (_drawerContent == null || _vm == null) return;

            for (int i = _drawerContent.childCount - 1; i >= 0; i--)
            {
                var child = _drawerContent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            // Redirect the shared row factory at the drawer for the length of this build. The
            // alternative — a second set of row builders — is the duplicated-state defect that
            // produced this ticket in the first place.
            _rowParent = _drawerContent;
            try
            {
                var channel = ManageScreenVM.ChannelOf(_vm.Tab);

                // The rail leads as the FIRST ROW: a status glance (ruling §7, display-only) above
                // the rows that carry every action.
                MountRail(MakeRowHost("Drawer_QueueRail", _railBandPx), forceRebuild: true);

                AddSectionHeader("IN QUEUE - " + BuildTimerService.ChannelWord(channel).ToUpperInvariant());
                if (_vm.QueueRows.Count == 0)
                    AddNoteRow("Nothing is queued on this line. Start an upgrade to see it here.");
                else
                    for (int i = 0; i < _vm.QueueRows.Count; i++) AddQueueRow(_vm.QueueRows[i]);

                MakeRowHost("DrawerTailSpacer", ListTailPx);
            }
            finally
            {
                // Restored in a finally so a throw inside a row build can never leave the BROWSE
                // list pointed at the drawer — that would silently move every later row.
                _rowParent = null;
            }

            // §12 — the acceptance evidence for this ticket. It names the BUILD SITE and the
            // controls, not just the VM's row count: queueRows tracked the real job count
            // perfectly all morning while no verb existed, so a count alone proves nothing.
            int finishable = 0, adOffers = 0, cancellable = 0;
            for (int i = 0; i < _vm.QueueRows.Count; i++)
            {
                var r = _vm.QueueRows[i];
                if (r == null || r.IsStackHeader) continue;
                if (r.FinishPrice > 0) finishable++;
                if (r.AdAvailable && DeNelle.Core.FeatureFlags.RewardedAdSkip) adOffers++;
                if (r.CanCancel) cancellable++;
            }
            FlowTrace.Step("Manage", string.Format(
                "queue drawer BUILT {0} row(s) into Drawer_QueueList: FinishNow={1} Ad={2} Cancel={3} " +
                "(rewardedAdSkip={4}). Zero rows with a non-empty queue, or zero FinishNow on a " +
                "priced job, is the WO-1368 defect returning.",
                _vm.QueueRows.Count, finishable, adOffers, cancellable,
                DeNelle.Core.FeatureFlags.RewardedAdSkip));
            if (_vm.QueueRows.Count > 0 && finishable == 0 && adOffers == 0 && cancellable == 0)
                FlowTrace.Warn("Manage",
                    "queue drawer built rows but NOT ONE carries a verb - Finish Now, Ad and Cancel " +
                    "are all withheld by the VM. The money path is unreachable from this screen.");
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
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                var child = _listContent.GetChild(i).gameObject;
                // Runtime keeps Unity's normal end-of-frame destruction semantics. The synchronous
                // edit-mode capture has no next frame before it renders, so deferred destruction
                // leaves the previous tab's rows painted above the requested destination and turns
                // screenshot evidence into a lie. Match BuildTabs' already-proven edit-mode rule.
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            _tickCells.Clear();
            // The progress cells point at bars that were just destroyed with their rows. The tick
            // already skips a Unity-null fill, so this never crashed — but without the clear the
            // list grew by every rebuild for the life of the open panel.
            _progressCells.Clear();
            _trainingNowCells.Clear();   // WO-1382: the band's cells die with its rows too

            var channel = ManageScreenVM.ChannelOf(_vm.Tab);

            if (_vm.Tab == ManageTab.Troops)
            {
                RenderTroopsDestination(channel);
                MakeRowHost("ListTailSpacer", ListTailPx);
                return;
            }

            // The DEMOTED rail (see the band budget): its own fixed-pixel row at the head of the
            // list, so it keeps its full 200px and simply scrolls away instead of overprinting.
            if (!_railPinned)
                MountRail(MakeRowHost("RailRow", _railBandPx), forceRebuild: true);

            var summary = FindSummary(channel);
            // The selected structure and its action lead the scroll content, keeping the primary
            // task above the queue history on a phone viewport.
            AddSectionHeader(BrowseHeading(_vm.Tab));
            if (_vm.BrowseRows.Count == 0)
                AddNoteRow(BrowseEmptyState(_vm.Tab));
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
                AddActionNoteRow("Need another tower?", "Build defense", OpenDefenseBuilder);
            else if (_vm.Tab == ManageTab.Buildings)
                AddActionNoteRow("Need another town structure?", "Open build", OpenTownBuilder);

            // ⛔ NO QUEUE ROWS HERE, AND THE VERBS ARE NOT MISSING — THEY ARE IN THE DRAWER.
            // Queue inspection and queue actions live in the explicit header Queue drawer
            // (RenderQueueDrawer). Repeating the same jobs inline beneath the upgrade catalogue
            // made the browse destination overflow at landscape height and contradicted the
            // approved Manage hierarchy: upgrades are the primary task; queue management is
            // opt-in. WO-1368: this sentence was true when it was written and the drawer it
            // pointed at contained NO ROWS for three days, so the money path (Finish Now / Ad)
            // could not be reached at all. The drawer now builds the rows; if you are here
            // because a verb is missing, read RenderQueueDrawer, do not re-add rows to this list
            // (ManageQueueDrawerRegression fails the build if you do).

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

        private void RenderTroopsDestination(ChannelId channel)
        {
            if (_vm == null) return;
            if (_vm.TroopChoices.Count == 0)
            {
                AddSectionHeader("TRAIN & UPGRADE TROOPS");
                AddNoteRow("No troop definitions are available.");
                return;
            }

            TroopChoiceVM selected = null;
            for (int i = 0; i < _vm.TroopChoices.Count; i++)
                if (string.Equals(_vm.TroopChoices[i].Id, _selectedTroopId, StringComparison.OrdinalIgnoreCase))
                { selected = _vm.TroopChoices[i]; break; }
            if (selected == null)
            {
                for (int i = 0; i < _vm.TroopChoices.Count; i++)
                    if (_vm.TroopChoices[i].Unlocked) { selected = _vm.TroopChoices[i]; break; }
                if (selected == null) selected = _vm.TroopChoices[0];
                _selectedTroopId = selected.Id;
            }

            // WO-1382 (owner ruling 2026-09-04 22:50): rail + card in ONE reserved row, then the
            // TRAINING NOW band (its own rows, built by AddTroopTrainingNowBand - never by
            // AddQueueRow, which is drawer-only by ManageQueueDrawerRegression's pin), then the
            // one Saved-armies row. Four verbs on the whole screen: BACK, TRAIN 1 <NAME>,
            // UPGRADE TO L<n>, OPEN QUEUE / OPEN ARMIES. Nothing here is a mode switch.
            AddTroopWorkspaceRow(selected);
            AddTroopTrainingNowBand();

            for (int i = 0; i < _vm.BrowseRows.Count; i++)
            {
                var row = _vm.BrowseRows[i];
                if (row == null || !string.Equals(row.ActionText, "Open", StringComparison.OrdinalIgnoreCase)) continue;
                AddActionNoteRow("Saved army compositions", "Open armies", row.Activate);
                break;
            }

            // §12 — the geometry and the verb count, PROVEN off a capture rather than eyeballed.
            FlowTrace.Step("Manage", string.Format(
                "troops workspace: {0} troop(s) in the rail, selected={1} (unlocked={2} trainReady={3} " +
                "upgradeReady={4} hasNext={5}), TRAINING NOW rows={6}. Bands(px): workspace={7:0} " +
                "railRow={8:0} band={9:0} extraRow={10:0}; above-the-fold = 10 + {7:0} + 8 + {9:0} = {11:0}. " +
                "Verbs on screen: TRAIN 1 / UPGRADE TO L / OPEN QUEUE.",
                _vm.TroopChoices.Count, selected.Id, selected.Unlocked, selected.TrainReady,
                selected.UpgradeReady, selected.HasNextLevel, _vm.QueueRows.Count,
                TroopWorkspacePx, TroopRailRowPx, TrainingNowBandPx, TrainingNowRowPx,
                10f + TroopWorkspacePx + 8f + TrainingNowBandPx));
        }

        // =====================================================================
        //  WO-1382 — THE TROOPS WORKSPACE: rail (left, scrolls) + selected-troop card (right)
        // ---------------------------------------------------------------------
        // ⚠ WHY THE ROW CARRIES NO ApplyRowSurface. The RCA on WO-1382 proved the owner's
        // "box around train": frames/content-panel (1672x941, spriteBorder 96) carries ~90px of
        // transparent margin above its gold line and ~140px below, so on any TALL row the 9-slice
        // draws its frame ~100px INSIDE the row's top and bottom edges and every child outside
        // that band looks like it is floating over a card. The sprite's .meta is shared by every
        // other consumer and is not re-authored here; the rail and the card sit on kit
        // AddImage plates instead, which draw edge-to-edge by construction.
        // =====================================================================

        private void AddTroopWorkspaceRow(TroopChoiceVM selected)
        {
            var workspace = MakeRowHost("TroopSplitWorkspace", TroopWorkspacePx);

            // ── RAIL: one row per troop def, vertical scroll, NO pager arrows (ruling #2) ──
            var railZone = MakeZone(workspace, "TroopSelectorRail", new Vector2(0f, 0f), new Vector2(0.26f, 1f));
            var railPlate = ElarionUiKit.AddImage(railZone, "RailPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            railPlate.GetComponent<Image>().raycastTarget = false;
            var railScroll = ElarionUiKit.MakeScrollZone(railZone, spacing: 6f, padding: 8);
            if (railScroll == null || railScroll.content == null)
            {
                FlowTrace.Fail("Manage", "troop rail MakeScrollZone returned no content - the rail has no build site.");
            }
            else
            {
                int selectedIndex = 0;
                // Redirect the shared row factory at the rail for the length of this build (the
                // drawer's proven idiom) so every rail row is a fixed-pixel MakeRowHost band.
                _rowParent = railScroll.content;
                try
                {
                    for (int i = 0; i < _vm.TroopChoices.Count; i++)
                    {
                        var choice = _vm.TroopChoices[i];
                        if (choice == null) continue;
                        bool isSelected = string.Equals(choice.Id, selected.Id, StringComparison.OrdinalIgnoreCase);
                        if (isSelected) selectedIndex = i;
                        Guard.Try("Manage", "troop rail row " + choice.Id, () => BuildTroopRailRow(choice, isSelected));
                    }
                }
                finally
                {
                    _rowParent = null;
                }

                // Keep the selected troop in view when the rail is longer than the row: a fresh
                // Render rebuilds the column at the top, and a selection on row 7 of 9 would
                // otherwise open scrolled away from itself.
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(railScroll.content);
                int count = _vm.TroopChoices.Count;
                if (railScroll.scroll != null)
                    railScroll.scroll.verticalNormalizedPosition = count > 1 ? 1f - selectedIndex / (float)(count - 1) : 1f;
            }

            // ── CARD: the selected troop, everything readable without a tap ──
            var card = MakeZone(workspace, "TroopSelectedCard", new Vector2(0.275f, 0f), new Vector2(1f, 1f));
            BuildTroopCard(card, selected);
        }

        /// <summary>
        /// One rail entry: portrait medallion + NAME + "Level n" (or "Locked . T2" + padlock, dimmed).
        /// Selected = gold outline AND a ">" chevron - state by shape and words, never hue alone
        /// (owner colourblind). The row is the tap target (>= MinTouchPx by its 120px band).
        /// </summary>
        private void BuildTroopRailRow(TroopChoiceVM choice, bool isSelected)
        {
            var row = MakeRowHost("TroopChoiceRow_" + choice.Id, TroopRailRowPx);
            // ⚠ The BUTTON's own object carries the TroopChoice_ name. The first capture (2026-09-04)
            // showed a gold plate slicing through every "Level 1": ApplyOperationalMedievalSkin
            // keys its skip-list off button.gameObject.name, the Button lived on a child called
            // "Face", so the bulk pass painted button-normal-empty (Simple, stretched) over the
            // whole row. A FLAT face (rounded: false) is the design; the name fixes the skip.
            var faceGo = ElarionUiKit.AddImage(row, "TroopChoice_" + choice.Id, Vector2.zero, Vector2.one,
                isSelected ? new Color(0.24f, 0.18f, 0.08f, 0.90f) : new Color(0f, 0f, 0f, 0.28f), rounded: false);
            var face = faceGo.GetComponent<Image>();
            face.raycastTarget = true;
            var button = faceGo.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => { _selectedTroopId = choice.Id; _browsePage = 0; Render(); });
            if (isSelected)
            {
                // Frames the WHOLE row (the face fills it). useGraphicAlpha off so the outline is
                // full gold and not dimmed by the face's own alpha.
                var outline = faceGo.AddComponent<Outline>();
                outline.effectColor = ElarionUi.Gold;
                outline.effectDistance = new Vector2(4f, -4f);
                outline.useGraphicAlpha = false;
            }

            // Two clear TEXT bands beside the medallion: name on the upper band (0.52-0.96), the
            // level / lock word on its own lower band (0.06-0.48). Nothing is drawn between them.
            var medallion = MakeZone(faceGo.transform, "Medallion", new Vector2(0.03f, 0.08f), new Vector2(0.27f, 0.92f));
            var portrait = ElarionUiKit.Portrait(medallion, TroopSprite(choice.IconId), active: isSelected);
            if (!choice.Unlocked && portrait?.image != null)
                portrait.image.color = new Color(0.42f, 0.42f, 0.42f, 1f);   // dim + padlock + tier WORD below
            if (!choice.Unlocked) BuildLockBadge(medallion);

            var name = ElarionUiKit.Label(faceGo.transform, ManageScreenVM.Ascii(choice.Name ?? ""), 0.52f, 0.96f,
                choice.Unlocked ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.30f, 0.84f, bold: true);
            ElarionUiKit.FitSingleLine(name, 26f, 38f);
            var sub = ElarionUiKit.Label(faceGo.transform,
                choice.Unlocked ? "Level " + choice.Level : "Locked . T" + choice.LockTier,
                0.06f, 0.48f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.30f, 0.84f);
            ElarionUiKit.FitSingleLine(sub, 22f, 30f);

            if (isSelected)
            {
                var chevron = ElarionUiKit.Label(faceGo.transform, ">", 0.10f, 0.90f, ElarionUi.Gold,
                    (int)ElarionUi.FontBody, TextAlignmentOptions.Center, 0.84f, 0.98f, bold: true);
                ElarionUiKit.FitSingleLine(chevron, 30f, 50f);
            }
            ElarionUiKit.ClampMinTouch(button);
        }

        /// <summary>Troop portrait art by icon id, with the kit's sword icon as the last resort.</summary>
        private static Sprite TroopSprite(string iconId)
        {
            return RpgUiCatalog.Get(RpgUiCatalog.RoleTroop, iconId)
                ?? RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
        }

        /// <summary>
        /// The SELECTED TROOP card (ruling #3/#4/#8): portrait medallion, NAME at title size with
        /// LEVEL n right-aligned in the same band, the status word, the description, the fact
        /// sentence "Train one: cost . time . state", TWO verb buttons on one line, and the
        /// upgrade fact sentence under them. A locked troop is selectable and shows ONE Gray
        /// non-interactable LOCKED . TIER n face instead ("Don't hide future content").
        /// </summary>
        private void BuildTroopCard(RectTransform card, TroopChoiceVM selected)
        {
            var plate = ElarionUiKit.AddImage(card, "CardPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            plate.GetComponent<Image>().raycastTarget = false;

            // Card bands at TroopWorkspacePx = 260 (see the fold arithmetic on the constants):
            //   name + LEVEL   0.745-1.000 -> 66px   (title line box <= 48 fits)
            //   desc + status  0.585-0.735 -> 39px   (one line, 24-30)
            //   train fact     0.455-0.575 -> 31px   (one line, 22-26)
            //   CTAs           0.010-0.445 -> 113px  >= MinTouchPx
            // Portrait medallion, top-left, spanning the name and description bands.
            var medallion = MakeZone(card, "TroopPortrait", new Vector2(0.02f, 0.59f), new Vector2(0.16f, 0.99f));
            var portrait = ElarionUiKit.Portrait(medallion, TroopSprite(selected.IconId), active: true);
            if (!selected.Unlocked && portrait?.image != null)
                portrait.image.color = new Color(0.42f, 0.42f, 0.42f, 1f);
            if (!selected.Unlocked) BuildLockBadge(medallion);

            // NAME band at title size + LEVEL n right-aligned, always on screen (it is the first
            // band of a row that starts at scroll 0 - the name can no longer scroll off the top).
            var name = ElarionUiKit.Label(card, ManageScreenVM.Ascii((selected.Name ?? "").ToUpperInvariant()),
                0.745f, 1.0f, ElarionUi.Gold, (int)ElarionUi.FontTitle,
                TextAlignmentOptions.Left, 0.19f, 0.74f, bold: true);
            ElarionUiKit.FitSingleLine(name, 30f, 48f);
            var level = ElarionUiKit.Label(card, "LEVEL " + selected.Level, 0.745f, 1.0f, ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.75f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(level, 26f, 36f);

            // Description left, status WORD ("Available" / "Requires Barracks Tier 2") right, one
            // band - words carry the state; the old green/red tint pair was the same colour to a
            // red/green colourblind owner.
            var desc = ElarionUiKit.Label(card, ManageScreenVM.Ascii(selected.Description ?? ""), 0.585f, 0.735f,
                ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.19f, 0.70f);
            ElarionUiKit.FitSingleLine(desc, 22f, 30f);
            var status = ElarionUiKit.Label(card, ManageScreenVM.Ascii(selected.Requirement ?? ""), 0.585f, 0.735f,
                ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Right, 0.71f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(status, 22f, 30f);

            if (!selected.Unlocked)
            {
                // Ruling #8: selectable, dim, the requirement in words, ONE Gray non-interactable
                // face, no Train / Upgrade buttons at all.
                var fact = ElarionUiKit.Label(card, ManageScreenVM.Ascii(selected.Requirement ?? ""), 0.455f, 0.575f,
                    ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.02f, 0.98f, bold: true);
                ElarionUiKit.FitSingleLine(fact, 22f, 26f);
                var lockedFace = ElarionUiKit.BuildObsidianButton(card, "LOCKED . TIER " + selected.LockTier,
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.02f, TroopCtaY0), new Vector2(0.48f, TroopCtaY1), null);
                if (lockedFace != null)
                {
                    lockedFace.gameObject.name = "TroopCta_Locked";
                    lockedFace.interactable = false;
                    MedievalUiSkin.ApplyButton(lockedFace, false);
                }
                return;
            }

            // The fact SENTENCE (ruling #4) - composed by the VM, painted here, directly ABOVE the
            // TRAIN button it explains.
            var trainFact = ElarionUiKit.Label(card, ManageScreenVM.Ascii(selected.TrainFactText ?? ""), 0.455f, 0.575f,
                ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.02f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(trainFact, 22f, 26f);

            // THE DOOR is unchanged: the VM's verb-led "Train <name>" row -> TrainTroop ->
            // BarracksService.EnqueueTraining -> the Train line. One job per tap, no count picker
            // (owner: "No count picker. At least for now."). The button face is the owner's
            // wording; the row's Activate is the same delegate the old browse row invoked.
            BrowseRowVM trainRow = FindTroopRow(selected.Id, "Train");
            BrowseRowVM upgradeRow = FindTroopRow(selected.Id, "Upgrade");

            bool trainOn = trainRow != null && selected.TrainReady;
            var train = ElarionUiKit.BuildObsidianButton(card,
                "TRAIN 1 " + ManageScreenVM.Ascii((selected.Name ?? "").ToUpperInvariant()),
                ElarionUiKit.ObsidianButtonStyle.Style1,
                trainOn ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.02f, TroopCtaY0), new Vector2(0.48f, TroopCtaY1),
                () => { Guard.Try("Manage", "train one", () => trainRow?.Activate?.Invoke()); });
            if (train != null)
            {
                train.gameObject.name = "TroopCta_Train";
                // Disabled + the sentence above says why (ruling #4). Never colour alone.
                train.interactable = trainOn;
                MedievalUiSkin.ApplyButton(train, true);
            }

            // The upgrade fact ("300 wood, 120 iron . Ready" / "Short 40 iron" / "At max level")
            // rides the UPGRADE face as its SUB-LINE through the panel's existing two-line CTA -
            // the 260px card has no spare band under the buttons, and the sentence stays with the
            // button it explains (ruling #4: "directly above or beneath the button").
            Button upgrade;
            if (selected.HasNextLevel)
            {
                bool upgradeOn = upgradeRow != null && selected.UpgradeReady;
                string upgradeSub = string.IsNullOrEmpty(selected.UpgradeCostText)
                    ? selected.UpgradeStateText
                    : selected.UpgradeCostText + " . " + selected.UpgradeStateText;
                upgrade = BuildTwoLineCta(card, "UPGRADE TO L" + (selected.Level + 1), upgradeSub,
                    ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.52f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1),
                    () => { Guard.Try("Manage", "upgrade troop", () => upgradeRow?.Activate?.Invoke()); });
                if (upgrade != null) upgrade.interactable = upgradeOn;
            }
            else
            {
                upgrade = BuildTwoLineCta(card, "MAX LEVEL", selected.UpgradeStateText,
                    ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.52f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1), null);
                if (upgrade != null) upgrade.interactable = false;
            }
            if (upgrade != null)
            {
                upgrade.gameObject.name = "TroopCta_Upgrade";
                MedievalUiSkin.ApplyButton(upgrade, false);   // the SECONDARY verb, by construction
            }
        }

        /// <summary>The VM's verb-led browse row for a troop ("Train"/"Upgrade"), or null.</summary>
        private BrowseRowVM FindTroopRow(string troopId, string actionText)
        {
            for (int i = 0; i < _vm.BrowseRows.Count; i++)
            {
                var candidate = _vm.BrowseRows[i];
                if (candidate == null) continue;
                if (!string.Equals(candidate.SubjectId, troopId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(candidate.ActionText, actionText, StringComparison.OrdinalIgnoreCase)) continue;
                return candidate;
            }
            return null;
        }

        // =====================================================================
        //  WO-1382 — THE TRAINING NOW BAND (ruling #5/#6): an informational MIRROR of the line.
        // ---------------------------------------------------------------------
        // ⛔ Built by THIS method from RenderTroopsDestination, NEVER by AddQueueRow and never
        // from RenderList: AddQueueRow is the drawer's build site for Finish Now / Ad / Cancel /
        // Move up (ManageQueueDrawerRegression pins both halves). Those verbs stay OUT of this
        // screen ("Keep advanced queue actions OUT of this screen"); the ONE door here is OPEN
        // QUEUE -> ToggleQueueDrawer. The band re-renders on QueueChanged (Rebuild -> Changed ->
        // Render), so the tap's consequence is visible without opening the drawer.
        // =====================================================================

        private void AddTroopTrainingNowBand()
        {
            // ONE 120px row carries the label, the FIRST job and OPEN QUEUE, so the band and at
            // least one job are above the fold at 2670x1200 (see the constants' arithmetic). The
            // first capture (2026-09-04) had a separate 128px header and the band fell below the
            // viewport - ruling #5 ("the screen visibly reacts") failed at scroll 0.
            var band = MakeRowHost("TroopTrainingNowBand", TrainingNowBandPx);
            var bandPlate = ElarionUiKit.AddImage(band, "BandPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            bandPlate.GetComponent<Image>().raycastTarget = false;
            var title = ElarionUiKit.Label(band, "TRAINING NOW", 0.15f, 0.85f, ElarionUi.Gold,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.01f, 0.165f, bold: true);
            ElarionUiKit.FitSingleLine(title, 22f, 32f);
            var open = ElarionUiKit.BuildObsidianButton(band, "OPEN QUEUE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(PrimaryX0, BandCtrlY0), new Vector2(PrimaryX1, BandCtrlY1), ToggleQueueDrawer);
            if (open != null) open.gameObject.name = "TroopOpenQueue";
            ElarionUiKit.ClampMinTouch(open);

            if (_vm.QueueRows.Count == 0)
            {
                var t = ElarionUiKit.Label(band, "Nothing training. Tap TRAIN to start.", 0.15f, 0.85f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.18f, ClusterX1 + 0.01f);
                ElarionUiKit.FitSingleLine(t, 24f, 34f);
                return;
            }

            // First job shares the band row, right of the label and left of the primary slot.
            var first = _vm.QueueRows[0];
            if (first != null)
                Guard.Try("Manage", "training now job 1", () => BuildTroopTrainingNowJob(band, 1, first,
                    0.175f, 0.205f, 0.21f, 0.27f, 0.28f, 0.45f, 0.46f, 0.60f, 0.61f, ClusterX1 + 0.01f));

            // Every further job is its own 88px informational row under the fold.
            for (int i = 1; i < _vm.QueueRows.Count; i++)
            {
                var r = _vm.QueueRows[i];
                if (r == null) continue;
                int ordinal = i + 1;
                Guard.Try("Manage", "training now row " + ordinal, () =>
                {
                    var row = MakeRowHost("TroopTrainingNowRow_" + ordinal, TrainingNowRowPx);
                    var plate = ElarionUiKit.AddImage(row, "RowPlate", Vector2.zero, Vector2.one,
                        new Color(0f, 0f, 0f, 0.28f));
                    plate.GetComponent<Image>().raycastTarget = false;
                    BuildTroopTrainingNowJob(row, ordinal, r,
                        0.005f, 0.05f, 0.055f, 0.115f, 0.13f, 0.46f, 0.48f, 0.78f, 0.80f, 0.99f);
                });
            }
        }

        /// <summary>
        /// One numbered, read-only job: "<n>." + portrait + name, then for the ACTIVE job the kit
        /// <see cref="ElarionUiKit.Bar"/> + "<n>s left" (ticked at 1 Hz), and for a pending job
        /// "Queued <ordinal>". The x-bands are passed in because the first job shares the band
        /// row with the label and OPEN QUEUE while later jobs own a full row. No control here.
        /// </summary>
        private void BuildTroopTrainingNowJob(RectTransform row, int ordinal, QueueRowVM r,
            float numX0, float numX1, float medX0, float medX1, float nameX0, float nameX1,
            float barX0, float barX1, float timeX0, float timeX1)
        {
            var number = ElarionUiKit.Label(row, ordinal + ".", 0.15f, 0.85f, ElarionUi.Gold,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, numX0, numX1, bold: true);
            ElarionUiKit.FitSingleLine(number, 24f, 36f);

            var medallion = MakeZone(row, "Medallion", new Vector2(medX0, 0.12f), new Vector2(medX1, 0.88f));
            Sprite art = !string.IsNullOrEmpty(r.IconRole) ? RpgUiCatalog.Get(r.IconRole, r.IconKey) : null;
            ElarionUiKit.Portrait(medallion, art, active: !r.Queued);

            var name = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.Label ?? ""), 0.15f, 0.85f, ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, nameX0, nameX1, bold: true);
            ElarionUiKit.FitSingleLine(name, 24f, 36f);

            bool running = !r.Queued && !r.IsStackHeader && r.Progress01 >= 0f && r.JobId != null;
            if (running)
            {
                var bar = ElarionUiKit.Bar(row, ElarionUiKit.BarKind.Castle,
                    new Vector2(barX0, 0.32f), new Vector2(barX1, 0.68f));
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
                    Queued = false,
                });

                var svc = BuildTimerService.Instance;
                double rem = svc != null ? svc.RemainingSeconds(r.Channel, r.JobId) : 0d;
                var left = ElarionUiKit.Label(row, ManageScreenVM.FormatTime(rem) + " left", 0.15f, 0.85f,
                    ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Right, timeX0, timeX1, bold: true);
                ElarionUiKit.FitSingleLine(left, 20f, 30f);
                _trainingNowCells.Add(new TrainingNowCell { Text = left, Channel = r.Channel, JobId = r.JobId });
            }
            else
            {
                string state = r.IsStackHeader
                    ? "Queued x" + r.StackCount
                    : "Queued " + ManageScreenVM.Ordinal(r.PendingIndex + 1);
                var queued = ElarionUiKit.Label(row, state, 0.15f, 0.85f, ElarionUi.ParchmentDim,
                    (int)ElarionUi.FontMicro, TextAlignmentOptions.Right, barX0, timeX1, bold: true);
                ElarionUiKit.FitSingleLine(queued, 22f, 30f);
            }
        }

        private static string BrowseHeading(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Defense: return "UPGRADABLE TOWERS - affordable first";
                case ManageTab.Buildings: return "BUILDING UPGRADES - affordable first";
                case ManageTab.Troops: return "TRAIN & UPGRADE TROOPS";
                case ManageTab.Research: return "RESEARCH PROJECTS";
                default: return "AVAILABLE ACTIONS";
            }
        }

        private static string BrowseEmptyState(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Defense: return "No defenses are ready to upgrade. Build your first tower or wall here.";
                case ManageTab.Buildings: return "No placed buildings are ready to upgrade.";
                case ManageTab.Troops: return "No trainable troops are available yet.";
                case ManageTab.Research: return "No research projects currently meet their requirements.";
                default: return "Nothing is available on this line yet.";
            }
        }

        // ── Row factories (fixed-pixel bands) ─────────────────────────────────

        private RectTransform MakeRowHost(string name, float heightPx)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            // WO-1368: ONE row factory, two destinations. _rowParent is non-null only for the
            // duration of RenderQueueDrawer, so the browse list is the default and cannot be
            // reached by accident; the drawer reuses every row builder verbatim rather than
            // forking a second copy that would drift.
            rt.SetParent(_rowParent != null ? _rowParent : _listContent, false);
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

        private void OpenTownBuilder()
        {
            Close();
            var controller = BuildModeController.Instance ?? BuildModeController.EnsureExists();
            controller?.EnterBuildMode(DeNelle.Core.Catalog.BuildType.Town);
        }

        private void AddActionNoteRow(string text, string action, Action onTap)
        {
            var row = MakeRowHost("ActionNote", RowHeightPx);
            ApplyRowSurface(row);
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
            ApplyRowSurface(row);

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
                // WO-1372: the VERB is the VM's (r.FinishVerbText) - Finish Now on every channel
                // that pays crystals, and the canon HIRE REINFORCEMENTS on a gold-priced training
                // job (creative canon §6). The View still only renders; it does not decide currency.
                // The literal below is a REAL fallback, not decoration: a row built by older code
                // (or a future one that forgets the field) must not render a BLANK primary face.
                string finishVerb = string.IsNullOrEmpty(r.FinishVerbText) ? "Finish Now" : r.FinishVerbText;
                var fin = BuildTwoLineCta(row, finishVerb, r.FinishCostText,
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

            // THE "Ad" CONTROL IS NEVER CONSTRUCTED while FeatureFlags.RewardedAdSkip is OFF —
            // absent, not present-and-disabled. The VM and BuildTimerService gate on the same
            // flag; this is the build site, so it is the one that guarantees absence. Its slot is
            // RESERVED by the even split (it simply is not counted while the flag is off).
            //
            // ⚠ CORRECTED 2026-09-04 (WO-1368 §15). The 2026-08-07 version of this comment called
            // the flag OFF and claimed the project contained no ad SDK at all. BOTH HALVES ARE
            // FALSE: FeatureFlags.RewardedAdSkip is declared defaultOn:true, and LevelPlay /
            // ironSource is integrated (canon records real, if tiny, ad revenue). A seat trusting
            // it would go hunting for a flag that is already on. If `Ad` is absent while a job is
            // queued, the flag is NOT the suspect — BuildTimerService.CanWatchAdToSkip ALSO
            // requires AdGateService.IsOffered(BuildSkipPlacementId) and a non-null
            // RewardedAdManager.Instance with IsAdReady, and either can withhold r.AdAvailable
            // while Finish Now renders perfectly. That gap is REPORTED, not widened here.
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
            BuildBrowseRowContent(row, r);
        }

        private void BuildBrowseRowContent(RectTransform row, BrowseRowVM r)
        {
            if (row == null || r == null) return;
            ApplyRowSurface(row);

            // Three disjoint x-columns: name+cost (0.02-0.50) | affordability (0.52-0.73) | CTA (0.76-0.98).
            // WO-1058: the CTA moved LEFT from 0.84 to PrimaryX0 so it occupies the SAME primary
            // slot as the queue row's "Finish Now". The affordability column was pulled back from
            // 0.82 to 0.73 in the same edit — leaving it at 0.82 would have put a text box under
            // the widened button ("BUTTON OVER TEXT", the WO-1060 oracle's own failure class).
            // WO-1390: a LOCKED prerequisite row (Research) reads dim + carries the same padlock the
            // Troops rail seats on a locked choice (BuildLockBadge), so "locked" is stated by words
            // AND a shape, never a tint alone (colourblind law). The name column gives up its right
            // edge to the badge; the CTA stays live because it is the DOOR to the prerequisite.
            bool locked = r.Locked;
            float nameX1 = locked ? 0.42f : 0.50f;
            var nameColor = locked ? ElarionUi.ParchmentDim : ElarionUi.Parchment;
            var name = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.Label ?? ""), 0.52f, 0.98f, nameColor,
                                          (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, nameX1, bold: true);
            ElarionUiKit.FitSingleLine(name);
            var cost = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.CostText ?? ""), 0.04f, 0.48f, ElarionUi.ParchmentDim,
                                          (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, nameX1);
            ElarionUiKit.FitSingleLine(cost);
            // Affordability is a SENTENCE ("Ready" / "Not enough Wood (400)") — never a tint alone.
            var state = ElarionUiKit.Label(row, ManageScreenVM.Ascii(r.StateText ?? ""), 0.20f, 0.80f, nameColor,
                                           (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.52f, ClusterX1 + 0.01f);
            ElarionUiKit.FitBlock(state);   // a shortfall sentence may need two lines inside its box
            if (locked)
            {
                // BuildLockBadge fills a fixed sub-rect of its parent (x 0.345-0.50, y 0.20-0.76),
                // so a host rect is sized to land the padlock in the gap between the name column
                // (ends 0.42) and the state column (starts 0.52): host x 0.306-0.693 puts the badge
                // at row x 0.44-0.50. Reuses the badge verbatim rather than a second padlock.
                var host = new GameObject("LockBadgeHost", typeof(RectTransform));
                var hrt = (RectTransform)host.transform;
                hrt.SetParent(row, false);
                hrt.anchorMin = new Vector2(0.306f, 0.0f);
                hrt.anchorMax = new Vector2(0.693f, 1.0f);
                hrt.offsetMin = hrt.offsetMax = Vector2.zero;
                BuildLockBadge(hrt);
            }

            var act = ElarionUiKit.BuildObsidianButton(row, r.ActionText ?? "Open",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                r.Affordable ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(PrimaryX0, RowCtrlY0), new Vector2(PrimaryX1, RowCtrlY1),
                () => { Guard.Try("Manage", "browse drill-in", () => r.Activate?.Invoke()); });
            ElarionUiKit.ClampMinTouch(act);
        }

        private static void ApplyRowSurface(RectTransform row)
        {
            if (row == null) return;
            var image = row.GetComponent<Image>() ?? row.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = new Color(0.92f, 0.88f, 0.76f, 0.96f);
            image.raycastTarget = false;
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

            // WO-1382: the TRAINING NOW band's short countdown ("32s left"), same tick, strings only.
            for (int i = 0; i < _trainingNowCells.Count; i++)
            {
                var cell = _trainingNowCells[i];
                if (cell.Text == null) continue;
                cell.Text.text = ManageScreenVM.FormatTime(svc.RemainingSeconds(cell.Channel, cell.JobId)) + " left";
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
