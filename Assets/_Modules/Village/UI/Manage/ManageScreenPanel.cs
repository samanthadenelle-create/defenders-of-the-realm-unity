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
using DeNelle.Core.Manage;   // WO-2001 - ManageTabId / ManageWorkspacePanel / ManageArt.
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
        // WO-2003 - the panel TITLE's anchors INSIDE the frame's header zone, shared by the launcher
        // and operational modes so the title clears BOTH header controls in BOTH states. FrameCore's
        // header zone is content x 0.24-0.88 (ElarionUiKit.cs:442), so local 0.30-0.78 resolves to
        // content 0.432-0.739: clear of the HEART face (ends 0.395) and of QUEUE (starts 0.795).
        // ⚠ RIGHT EDGE PULLED IN 2026-09-06: the title was CLIPPED under the QUEUE pill.
        // MEASURED, not inferred - the two rects genuinely overlapped. FrameCore's header zone is
        // content x 0.24-0.88 (ElarionUiKit.cs:443), so a local 0.78 resolved to content 0.739;
        // the chrome row spans content 0.055-0.945 and the pill sits at 0.72-0.95 OF THAT ROW, i.e.
        // content 0.696-0.900. 0.739 > 0.696, so the title ran 4.3% of the panel underneath it.
        // Local 0.68 resolves to content 0.675 and clears the pill by 0.021.
        // The left edge widens to 0.10 (content 0.304) so the title still reads centred over the
        // body now that the back arrow and the Heart face no longer sit beside it.
        private const float TitleLocalX0 = 0.10f, TitleLocalX1 = 0.68f;

        // The BACK / HEART / QUEUE chrome row, and the body's ceiling. It sits between the body and
        // the frame's own header zone (which starts at 0.900), so raising the body to meet it costs
        // nothing and hands the grid the strip that used to hold nothing. WO-1443, 2026-09-06.
        // 0.845-0.975 = 0.130 of the panel. At the measured reference (canvas ~921px, panel 96% =
        // ~884px) that is ~115px, clear of ElarionUiKit.MinTouchPx (112). AUTHORED to the floor,
        // not left to ClampMinTouch - the capture auditor caught the whole old chrome row at
        // 110.4px, 1.6px short, precisely because a nominal band was inset until it fell under.
        private const float WorkspaceHeaderY0 = 0.845f;
        private const float WorkspaceHeaderY1 = 0.975f;

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

        // =====================================================================
        //  WO-1393 (2026-09-05) - THE QUEUE DRAWER AS ITS OWN BAND ON THE TROOPS TAB.
        // ---------------------------------------------------------------------
        // PROVEN (docs/qa/UI_REVIEW_2026-09-05/10-troops-after-upgrade.png): OPEN QUEUE put the
        // full-body drawer OVER the selected-troop card; the UPGRADE TO L4 tap hit the drawer, the
        // top-right QUEUE tap closed nothing (the toggle was hidden while open), and the
        // "IN QUEUE - TRAINING" header rendered clipped under the drawer's own 200px rail.
        //
        // THE ARITHMETIC, read off the captured bands(px) line (Builds/manage-capture.log,
        // 2670x1200): well=533, strip 120 + gap 12 => LIST=401. A 260px workspace plus a usable
        // drawer (64px header + one 132px row + 20px scroll padding = 216) cannot share 401px
        // with the 120px TRAINING NOW band, and cannot share it at all if the whole workspace
        // stays in view (401 - 280 - 12 = 109 < 216). So on the Troops tab, while the drawer is
        // open:
        //   * the TRAINING NOW band collapses - the drawer SUPERSEDES it (it is the line's mirror);
        //   * the list band keeps a viewport of DrawerModeListKeepPx: the scroll padding plus
        //     everything ABOVE the card's CTA line (rail rows, portrait, name, level, facts),
        //     unsquashed - the row keeps its 260px inside the scroll content, so TRAIN / UPGRADE
        //     are one drag away and are NEVER under the drawer (ruling #6: the card stays
        //     readable; queue verbs live in the drawer);
        //   * the drawer is a sibling band BELOW that viewport, top-anchored at
        //     listTop + keep + BandGapPx, running to the well floor: 401 - 154 - 12 = 235px at
        //     2670x1200 (244 at 2340x1080, 314 at 1920x1080), which seats the header and the
        //     first row at rest; further rows and the slot offer scroll;
        //   * the drawer carries NO rail in this mode (the workspace rail and the strip's counts
        //     are already on screen; a 200px rail is exactly what clipped the header).
        // Other tabs keep the WO-1368 full-body drawer. Both modes put the slot offer LAST in the
        // drawer's scroll list, which is what frees the full-body list zone (0.02-0.86) so the
        // header sits inside the well under the rail at rest (0.84 * 437 - 20 = 347 >= 272).
        // ManageQueueDrawerRegression [queue-toggle-closes] / [drawer-clear-of-card] pin this.
        // =====================================================================
        /// <summary>Scroll padding + everything above the card's CTA line: 10 + 260 * (1 - 0.445)
        /// = 154.3px. The CTA's top edge is exactly the viewport's floor at rest.</summary>
        private const float DrawerModeListKeepPx = 10f + TroopWorkspacePx * (1f - TroopCtaY1);
        /// <summary>The least the drawer band may be given: header + one row + scroll padding.</summary>
        private const float DrawerModeMinPx = SectionHeaderPx + RowHeightPx + 20f;
        private const string TrainingNowPrefix = "TroopTrainingNow";   // band + its extra rows
        private const string BuildingNowPrefix = "BuildingNow";       // building band + its extra rows
        // WO-1422: Research rides its OWN channel, so it needs its own collapse prefix. Defence
        // deliberately has none - it reuses the Builder band verbatim (ruling 3.3), so
        // BuildingNowPrefix already collapses it and a second name for one queue is never minted.
        private const string ResearchNowPrefix = "ResearchNow";       // research band + its extra rows

        private ManageScreenVM _vm;
        // WO-1422: _browsePage is GONE with the pager it indexed. Its only reader was the paged
        // browse block in RenderList; leaving a field three call sites still reset would be state
        // nothing consumes - the same dead-code-that-looks-alive shape ruling 3.4 deletes.
        private string _selectedTroopId;
        private string _selectedBuildingId;
        private string _selectedDefenseId;      // WO-1422: one selection per TYPE (ruling 3.1)
        private string _selectedResearchKey;    // WO-1422: "<buildingId>:<perkId>" (ResearchKeyOf)
        private GameObject _ui;
        private RectTransform _listContent;
        private GameObject _operationalListBand;
        private RectTransform _operationalWell;
        private RectTransform _launcherHost;
        /// <summary>True while the HUB (mockup panel 1) owns the screen. The ONE authority - read
        /// it, never `_launcherHost.activeSelf`, and change it only through ShowLauncher /
        /// ShowWorkspace so ApplyScreenVisibility stays the single writer.</summary>
        private bool _hubShowing;
        /// <summary>The model's nav entry at the moment the hub went up. A different reference means
        /// the player asked for a screen; see ShowLauncher.</summary>
        private ManageNavEntry _hubNav;
        private RectTransform _launcherGrid;
        // WO-2001 - the three-tab workspace. It owns the WHOLE body well (the largest well this
        // chrome can offer) because the redesign's grid + selection band stack does not fit the
        // 533/542/612px wells the rail path was authored against; see ManageWorkspacePanel's
        // header, which hands that arithmetic to THIS work order by name.
        private RectTransform _workspaceHost;
        private DeNelle.Core.Manage.ManageWorkspacePanel _workspace;
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
        // WO-1393 - the drawer's band placement. The three px numbers are captured in the ONE
        // geometry pass (the same cursor that seats every band), so the drawer band is placed
        // from the measured well, never from a second copy of the arithmetic.
        private float _wellPx;
        private float _listBandTopPx;
        private float _listBandPx;
        private RectTransform _drawerList;        // the drawer's scroll zone host
        private RectTransform _drawerSlotOffer;   // Buy-Builder offer; the drawer list's LAST row
        // The overlay's header: a centred "QUEUE" title and a corner X (mockup panel 8). Both are
        // full-body mode only - band mode hides them (ApplyDrawerPlacement).
        // ⚠ The names are legacy. _drawerHeading read "BUILDERS / QUEUE" and _drawerHide was a HIDE
        // button seated over the first queue card's verb until WO-1443; the FIELDS were left named
        // as they were rather than renamed in the same change that fixed the geometry, so a reader
        // of ApplyDrawerPlacement still finds them. Rename them when panel 8's rows are built.
        private GameObject _drawerHeading;
        private GameObject _drawerHide;
        private bool _drawerBandMode;             // true while the drawer is seated as a band
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
        // WO-2001 - the per-key sprite cache MOVED to DeNelle.Core.Manage.ManageArt.LoadSprite.
        // There was one loader with two implementations (this file's and ManageArt's, which could
        // not call this one because it is `internal` to DeNelle.Village); that is duplicated state
        // of exactly the shape CLAUDE.md 2 / 5 / 16 keeps paying for. ManageArt is now the ONE
        // loader, the ONE Texture2D fallback and the ONE cache - and it also caches MISSES and
        // announces them once through FlowTrace, which this copy never did.
        private static readonly HashSet<string> ManageBuildingPortraitGaps =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "arcane-tower", "armorer", "barracks", "forge", "lumbermill", "farm",
            };

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
            // WO-2001 - the HOST's four verbs. The model owns every destination decision (canon 9
            // forbids the View deciding "which destination a prerequisite CTA should open"); these
            // are the only things the model cannot do for itself, because they leave its own graph.
            _vm.OpenQueueRequested = () => { if (!_queueDrawerOpen) ToggleQueueDrawer(); };
            _vm.OpenHeartRequested = OpenHeartSurface;
            _vm.CloseRequested = OnModelWantsOut;   // root BACK returns to the hub (mockup panel 1)
            _vm.OpenTownBuilderRequested = OpenTownBuilder;

            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged += OnQueueChanged;

            if (!Guard.Try("Manage", "build manage chrome", BuildChrome))
            {
                FlowTrace.Fail("Manage", "chrome build threw — screen not shown.");
                Close();
                return;
            }

            _vm.Rebuild();
            // ⭐ MANAGE OPENS ON THE HUB. Owner mockup panel 1 - "MANAGE (MAIN) - Simple entry with
            // three core options" - and CAPTURE_LOOP_GOAL.md 3.0c item 2 states in words that this
            // supersedes WO-2001's launcher retirement for this screen.
            // ⚠ WO-2001's "Entry" rule (open the last-used tab, never a chooser) is therefore
            // RETIRED, and the model's OpenDefaultScreen is still what picks the tab once a card is
            // tapped - the UI has not started deciding destinations. What changed is that the
            // player is asked WHICH destination first, because the owner's picture asks them.
            RenderLauncherCards();
            _vm.OpenDefaultScreen();
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
                // WO-1422: the destination is the Defence rail + card workspace. The old heading
                // this line echoed retired with the paged list (ruling 3.2 - it claimed "towers"
                // while the tab also lists walls, the mine, the caravan and three containers).
                FlowTrace.Step("Manage", "context open -> Defense destination (rail + selected card).");
                return;
            }
            if (string.Equals(requestedTab, "Buildings", StringComparison.OrdinalIgnoreCase))
            {
                ShowOperational(ManageTab.Buildings);
                FlowTrace.Step("Manage", "context open -> Buildings tab.");
                return;
            }
            if (string.Equals(requestedTab, "Research", StringComparison.OrdinalIgnoreCase))
            {
                ShowOperational(ManageTab.Research);
                FlowTrace.Step("Manage", "context open -> Research tab.");
                return;
            }
            // WO-1389: the post-first-raid beat's doors land on TROOPS, optionally with a troop
            // pre-selected ("Troops" or "Troops:troop-footman"). The Barracks lock is honoured
            // exactly as a card tap honours it: ShowOperational is not called for a locked tab.
            if (!string.IsNullOrEmpty(requestedTab) &&
                requestedTab.StartsWith("Troops", StringComparison.OrdinalIgnoreCase))
            {
                if (!BarracksUnlock.IsUnlocked)
                {
                    FlowTrace.Warn("Manage", "context open -> Troops REFUSED: the Barracks is locked; " +
                        "left on the launcher (the Troops card shows its own reason).");
                    return;
                }
                int colon = requestedTab.IndexOf(':');
                if (colon >= 0 && colon + 1 < requestedTab.Length)
                    _selectedTroopId = requestedTab.Substring(colon + 1);
                ShowOperational(ManageTab.Troops);   // raises manage.troops_shown once the rows exist
                FlowTrace.Step("Manage", "context open -> TRAIN & UPGRADE TROOPS (Troops tab" +
                    (string.IsNullOrEmpty(_selectedTroopId) ? "" : ", preselect '" + _selectedTroopId + "'") + ").");
                // WO-1389: a PRESELECTED troop is a selection that landed - the card is built and the
                // UPGRADE face exists - so it raises the same route-hop id a rail tap raises, AFTER
                // manage.troops_shown, so the post-raid beat's spotlight walks row -> UPGRADE face
                // in that order and never ends on a row the player has already passed.
                if (!string.IsNullOrEmpty(_selectedTroopId))
                {
                    // WO-2001: a PRESELECT is a DETAIL screen reached by BROWSING (no jump origin),
                    // so its BACK returns to the ARMY grid - ruling 28's ordinary case, which must
                    // not regress just because the door was contextual.
                    _vm?.OpenDetail(ManageTabId.Army, _selectedTroopId, null, null);
                    string preselected = _selectedTroopId;
                    Guard.Try("Manage", "raise preselect troop-selected signal", () =>
                        DeNelle.Core.Tutorial.TutorialSignals.Raise(
                            DeNelle.Core.Tutorial.TutorialSignals.ManageTroopSelectedPrefix + preselected));
                }
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
            if (_workspace != null) { _workspace.Clear(); _workspace = null; }
            _workspaceHost = null;
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
            _drawerList = null;
            _drawerSlotOffer = null;
            _drawerHeading = null;
            _drawerHide = null;
            _drawerBandMode = false;
            _wellPx = _listBandTopPx = _listBandPx = 0f;
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

            // ⛔ THE PANEL IS DELIBERATELY NEAR-FULL-BLEED, AND THAT IS A CAPACITY DECISION.
            // Owner mockup docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png: the GRID dominates every
            // panel and the chrome is thin. The measured gap on 2026-09-06 was one number -
            // MANAGE_FLOW_INVENTORY reported ARMY content=590px in a 190px viewport while screen 4
            // says in words "All 9 troops visible, no scrolling". At 0.05-0.95 the panel took 90% of
            // a ~921px reference canvas and the workspace well measured 533px, which cannot seat
            // three 150px rows (470px) once anything else is on screen. 0.02-0.98 lifts the panel to
            // 96%, and the well with it. This is the "(b) the modal's own chrome must go full-bleed"
            // half of the hand-back ManageWorkspacePanel's header has been naming since WO-2002.
            // ⛔ THE PANEL IS NARROW ON PURPOSE. Do not stretch it back across the canvas.
            // THE MEASUREMENT THAT FORCED IT: FrameCore is a 1210x1815 PORTRAIT frame
            // (ElarionUiKit.cs:437-458, pixel-measured 2026-07-03) and Manage was stretching it
            // across a 2670x1200 landscape canvas. Its body zone is x 0.055-0.945 - so at full
            // width the workspace band came out roughly 2400 x 450px, an 8:1 strip, and NO tile
            // shape can be right inside it. Round 4 proved both horns: square cells filling the
            // height left the grid a narrow strip with the panel black on either side (22% of the
            // width on ARMY); wide cells filling the width read as BARS at 793x134 (5.9:1).
            // The owner's mockup panels are content areas of roughly 2:1, and their tiles are
            // ~1:1 on BUILD and ~2.3:1 on ARMY. That is only reachable by making the PANEL the
            // shape the design assumes instead of asking the grid to absorb the difference:
            //   0.18-0.82 of the canvas width = 64% -> content ~1218px against a ~583px well.
            // ARMY 3 cols x 3 rows then lands near 399x188 (2.1:1) once the tab row leaves the
            // body, and BUILD 5x2 near 236x220 (1.07:1) - both inside the mockup's proportions.
            // The scrim owns the margins either side, which is what a modal is for.
            var chrome = ElarionUiKit.BuildObsidianPanel(
                _ui.transform, "MANAGE",
                new Vector2(0.18f, 0.02f), new Vector2(0.82f, 0.98f),
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
            // ⭐ RECLAIM THE DEAD STRIP BETWEEN THE BODY AND THE TITLE.
            // The frame's header zone starts at y 0.900 (ElarionUiKit.cs:442,
            // `z.header = new Vector4(0.24f, 0.900f, 0.88f, 0.972f)`), and the body stops at 0.835 -
            // so 6.5% of the panel between them belonged to nothing. WorkspaceHeaderY0 (0.865) puts
            // the BACK / HEART / QUEUE row there at 0.865-0.975 and hands the body everything below
            // it. The row is 0.11 x ~884px = ~97px... see WorkspaceHeaderY0's own note: it is sized
            // against MinTouchPx and the buttons inside it are authored to the band, not clamped.
            // MEASURED REASON: the grid band is the screen (mockup), and every px above it that
            // holds nothing is a px the tiles do not get.
            if (bodyRt != null && WorkspaceHeaderY0 > bodyTop && WorkspaceHeaderY0 - bodyFloor > 0.05f)
                bodyTop = WorkspaceHeaderY0;
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
            // ⛔ THE CHROME ROW SPANS THE FRAME'S OWN BODY X-ZONE, NOT 0..1 OF THE PANEL.
            // This is the fix for the clipped QUEUE pill, and it is MEASURED rather than inferred:
            // ElarionUiKit.cs:458 authors FrameCore's body as x 0.055-0.945, i.e. the ornate border
            // owns the outer 5.5% on each side. The row used to span 0..1, so a pill seated at
            // 0.925 of the ROW was at 0.925 of the PANEL - inside the border art, where the frame
            // clipped "QUEUE" mid-word and pushed its badge outside the panel entirely. Pulling the
            // pill in twice did not help because the row itself was the wrong rect; the second
            // attempt (0.775-0.925) was still reading a rendered edge instead of the frame's zone.
            // Anchoring the ROW to the body zone makes every child's fraction a fraction of the
            // usable content, which is what those fractions were always meant to be - and it lines
            // the back arrow up with the grid's left edge for free.
            _tabsHost = MakeZone(chrome.content.transform, "ManageHeaderActions",
                new Vector2(0.055f, WorkspaceHeaderY0), new Vector2(0.945f, WorkspaceHeaderY1));
            BuildTabs();

            // WO-1393: the drawer band is seated from these three numbers (ApplyDrawerPlacement).
            _wellPx = wellPx;
            _listBandTopPx = cursor;
            _listBandPx = listPx;
            var listBand = Band(well, "Band_List", ref cursor, listPx);
            _operationalListBand = listBand.gameObject;
            var scroll = ElarionUiKit.MakeScrollZone(listBand, spacing: 8f, padding: 10);
            _listContent = scroll != null ? scroll.content : null;
            if (_listContent == null)
                FlowTrace.Fail("Manage", "MakeScrollZone returned no content — the list host is missing.");

            BuildNotice(noticeBesideClose
                ? NoticeSeatBesideClose(chrome.content.transform, noticeX1)
                : Band(well, "Band_Notice", ref cursor, NoticeBandPx));

            // WO-2001 - the workspace host is created BEFORE the drawer so the drawer is a LATER
            // sibling and paints over it. QUEUE is an OVERLAY (the owner's flow), and the host is
            // deactivated outright while it is open, so nothing shows through and nothing under it
            // stays tappable.
            BuildWorkspaceHost(well);

            BuildQueueDrawer(well);

            BuildLauncher(well);
            // ⭐ THE BACK CONTROL IS A `<-` ARROW AT TOP-LEFT, not a BACK word-button.
            // docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png draws it on all eight numbered panels;
            // CAPTURE_LOOP_GOAL.md 3.0b states it. It is a SMALL SQUARE seat (0.035-0.115 of the
            // panel width) rather than the old 0.035-0.205 word slab, because in the mockup the
            // chrome is thin and the grid is the screen.
            // ⚠ ASCII "<-" AND NOT THE "left arrow" CHARACTER: this project's fonts render
            // non-ASCII as tofu (CLAUDE.md 7's canon-strings note; ManageScreenVM.Ascii exists for
            // exactly this). Assets/Resources/RpgUi/button/arrow.png was measured and rejected as
            // the face - it is a filled RIGHT-pointing PLAY triangle, and mirroring a play glyph
            // reads as "rewind", not "back". Recorded rather than silently substituted.
            // ⛔ THE BACK ARROW IS BUILT IN BuildTabs, NOT HERE. Do not construct it at chrome time.
            // MEASURED 2026-09-06 in ManageFlow_ARMY_gridtop: the arrow was GONE - the row started
            // with the HEART face and the screen had no visible way back. Cause, and it is mine:
            // round 5 re-parented the arrow from chrome.content to _tabsHost so its fractions would
            // mean fractions of usable content - and BuildTabs DESTROYS every child of _tabsHost on
            // entry (its rebuild loop), on the first Render. A control built once into a host that
            // is cleared every paint is a control that exists for exactly one frame.
            // It is now rebuilt with its siblings, which is what the heart face and the queue pill
            // already did and why they survived.
        }

        // =====================================================================
        //  WO-2001 - THE THREE-TAB WORKSPACE (BUILD / ARMY / RESEARCH)
        // ---------------------------------------------------------------------
        //  Manage opens DIRECTLY on the last-used tab (BUILD by default). The
        //  four-tile launcher is superseded and is never shown; BACK walks the
        //  model's screen graph and can no longer route through it.
        //
        //  ⛔ THE HOST GIVES THE RENDERER THE WHOLE BODY WELL. ManageWorkspacePanel's
        //  header states the arithmetic and hands the call here: the fixed band stack
        //  alone (header 120 + tabs 120 + selection floor 256 + gaps) is 532px against
        //  MEASURED Manage wells of 533 / 542 / 612px, so a grid cannot exist inside
        //  the old rail path's list band. It therefore takes the well itself, and the
        //  strip band, the browse list band and the header QUEUE toggle stand down.
        //
        //  ⚠ STILL SHORT, AND SAID OUT LOUD RATHER THAN PAPERED OVER. Even the whole
        //  well is not the ~1454px canon 3's twelve tiles imply. Two things close it
        //  and BOTH are named in the hand-back: (a) the renderer must skip the
        //  selection band when Selection.Visible is false and the grid band when the
        //  tab has no tiles - the shape this composer already emits, screen by screen;
        //  (b) this modal's own chrome must go full-bleed. Until (a) lands the grid is
        //  clamped and scrolls, and ManageWorkspacePanel reports the shortfall in px.
        //  Nothing here silently re-columns or shrinks a text band to fake it.
        // =====================================================================

        /// <summary>True once the workspace renderer owns the body well.</summary>
        private bool WorkspaceActive => _workspace != null && _workspaceHost != null;

        private void BuildWorkspaceHost(RectTransform well)
        {
            if (well == null) return;
            var go = new GameObject("ManageWorkspace", typeof(RectTransform));
            _workspaceHost = (RectTransform)go.transform;
            _workspaceHost.SetParent(well, false);
            _workspaceHost.anchorMin = Vector2.zero;
            _workspaceHost.anchorMax = Vector2.one;
            _workspaceHost.offsetMin = _workspaceHost.offsetMax = Vector2.zero;
            _workspace = new DeNelle.Core.Manage.ManageWorkspacePanel(_workspaceHost);
        }

        /// <summary>
        /// Show the workspace and stand the retired chrome down. The launcher host is built (its
        /// cards are still the source-of-record for the 2026-08-31 approved art and copy) but it is
        /// never made visible again - WO-2001 removes the required chooser, and BACK no longer has
        /// a launcher to return to.
        /// </summary>
        /// <summary>
        /// ⛔ THE ONE WRITER OF "WHICH SCREEN IS ON". Every SetActive on the hub host, the
        /// operational well and the workspace host goes through here, and nowhere else.
        ///
        /// <para>THE BUG THIS EXISTS TO END, measured 2026-09-06 in
        /// Builds/ui-capture/ManageFlow_ARMY_gridtop_2670x1200.png plus
        /// <c>MANAGE_FLOW_INVENTORY ARMY: tiles=9 rendered=9 viewport=586px content=0px</c>:
        /// the model composed nine tiles, the renderer reported building nine, and the grid content
        /// measured ZERO height, because the tiles were built into a subtree whose ANCESTOR was
        /// inactive. <c>_workspaceHost</c> is a child of <c>_operationalWell</c>; ShowLauncher
        /// deactivated the WELL, and RenderWorkspace only ever re-activated the HOST. A
        /// SetActive(true) on a child of an inactive parent changes nothing on screen and runs no
        /// layout - so every tab rendered the hub and Manage had three doors onto nothing.</para>
        ///
        /// <para>Two independent writers deciding one piece of state by last-write-wins is the
        /// whole defect. There is now one flag and one writer, and the well is always re-asserted
        /// with the host it contains.</para>
        /// </summary>
        private void ApplyScreenVisibility()
        {
            if (_launcherHost != null) _launcherHost.gameObject.SetActive(_hubShowing);
            // The WELL is the workspace host's PARENT. It must follow the same state, or the host's
            // own flag is meaningless - that is exactly what produced content=0px.
            if (_operationalWell != null) _operationalWell.gameObject.SetActive(!_hubShowing);
            if (_workspaceHost != null)
                _workspaceHost.gameObject.SetActive(!_hubShowing && !_queueDrawerOpen);
            // BACK belongs to the workspace: the hub is the root and CLOSE is its way out.
            if (_workspaceBack != null) _workspaceBack.gameObject.SetActive(!_hubShowing);
        }

        private void ShowWorkspace()
        {
            _hubShowing = false;
            ApplyScreenVisibility();
            if (_stripHost != null) _stripHost.gameObject.SetActive(false);
            if (_operationalListBand != null) _operationalListBand.SetActive(false);
            // ⛔ THE QUEUE PILL STAYS UP IN WORKSPACE MODE. Do not re-add a SetActive(false) here.
            // It used to be hidden because the workspace painted its own queue face; the owner's
            // mockup retires that face and makes this pill the one door, on every panel. Hiding it
            // now would strand the queue outright: measured 2026-09-06, with the workspace up the
            // operational OPEN QUEUE bands are in a list band that is SetActive(false), the activity
            // strip is retired, and the HUD Builders chip's door went in WO-911. This IS the door.
            if (_queueDrawerToggle != null) _queueDrawerToggle.gameObject.SetActive(true);
            // The title's RECT is set by ApplyWorkspaceTitle, not here - see its note. "MANAGE" is
            // only what stands in for the frame between Show and the first Render.
            ApplyWorkspaceTitle("MANAGE");
        }

        /// <summary>
        /// ⭐ WO-1443 section 1 - THE ONE HEADING ON THIS SCREEN, and it lives HERE, in the host
        /// chrome's panel title. Owner felt-test 2026-09-06, verbatim: <i>"remove the manage army and
        /// sub line replace the manage top"</i>.
        /// <para>Before this ruling the screen stacked THREE: this title read the bare word "MANAGE",
        /// and <see cref="DeNelle.Core.Manage.ManageWorkspacePanel"/> then painted the breadcrumb
        /// ("MANAGE / ARMY") and a sub line ("Every troop, unlocked or not.") down the top of the
        /// body. The breadcrumb MOVED UP into this title; the renderer paints neither, and
        /// <c>HeaderSubtitle</c> is deleted from the contract outright.</para>
        /// <para>⚠ The model still owns the string - this method binds
        /// <c>ManageWorkspaceVM.HeaderTitle</c> and never composes a breadcrumb of its own. A second
        /// place that knows how to spell "MANAGE / ARMY" is the duplicated state that produced this
        /// defect in the first place.</para>
        /// <para>⚠ FIT, AND IT IS NOT PROVEN. The title zone is content x 0.432-0.739 (~31% of the
        /// content width), and the longest breadcrumb is "MANAGE / RESEARCH / SCHOOL" at 26
        /// characters against the old "MANAGE" at 6. FitSingleLine autosizes 34-52pt, so it should
        /// seat - but that is arithmetic, not a capture. WO-1443 acceptance requires a headless
        /// capture with the PNG opened; until then treat the long breadcrumbs as UNVERIFIED.</para>
        /// </summary>
        private void ApplyWorkspaceTitle(string headerTitle)
        {
            if (_workspaceTitle == null) return;

            // ⛔ THE TITLE'S RECT IS NARROWED HERE, NOT IN ShowWorkspace. ONE WRITER, EVERY PATH.
            //
            // THE DEFECT, measured by the capture auditor on EVERY frame:
            //   BUTTON OVER TEXT 'ManageHeaderActions/ManageQueueDrawerToggle' (x 269.2..550.6)
            //   covers 'Zone_Header/Label' ("MANAGE / BUILD") (x -357.4..522.4) by 253.2x66.7 ref px
            // The load-bearing number is the LABEL'S OWN LEFT EDGE: -357.4, i.e. its rect reached
            // 357px LEFT OF THE PANEL. That is the kit frame's default full-width header rect - it
            // had never been narrowed on those frames. The visible TEXT is centred inside it and
            // looks clear (the last three captures confirm the title is not clipped), so this reads
            // as a rect overlap rather than an ink overlap - but a rect that wide will sit under any
            // control in the header row, and the auditor is right to fail it.
            //
            // WHY IT WAS NEVER NARROWED: the anchors were set in ShowWorkspace, which the CARD-TAP
            // path runs and the MODEL path does not. `panel.Open()` then `vm.EnterTab(tab)` -
            // the harness's own route (UICaptureLaunch.cs:7101-7111) and every deep-link door -
            // dismisses the hub inside RenderWorkspace and never touches ShowWorkspace at all.
            // ⚠ THIS IS THE SAME BUG AS THE DEAD SUBTREE, IN A DIFFERENT PLACE: one piece of state
            // owned by two paths, correct on the one that was tested by hand and wrong on the one
            // the harness uses. The cure is the same - the writer moves to where the value is set.
            //
            // ⛔ AND THERE IS NO SECOND QUEUE TOGGLE. Searched at source this round:
            // `ManageQueueDrawerToggle` is constructed exactly ONCE (BuildTabs), and its 281px width
            // in the audit matches the pill's authored 0.72-0.95 of a 1223px row exactly. The name
            // is legacy - it IS the pill. The "exactly one queue affordance" claim holds here; what
            // did not hold was my claim about the title's geometry.
            var titleRt = _workspaceTitle.rectTransform;
            titleRt.anchorMin = new Vector2(TitleLocalX0, titleRt.anchorMin.y);
            titleRt.anchorMax = new Vector2(TitleLocalX1, titleRt.anchorMax.y);
            titleRt.offsetMin = new Vector2(0f, titleRt.offsetMin.y);
            titleRt.offsetMax = new Vector2(0f, titleRt.offsetMax.y);

            string text = string.IsNullOrEmpty(headerTitle) ? "MANAGE" : headerTitle;
            _workspaceTitle.text = text;
            ElarionUiKit.FitSingleLine(_workspaceTitle, 34f, 52f);

            // Print the rect the auditor measures, so the next capture NAMES it rather than leaving
            // anyone to reason about a label they cannot see the bounds of. Same reason the queue
            // pill prints its own rect: four rounds of coordinate theories ended with one printed
            // rectangle, and this title has now produced a defect nobody could see either.
            var corners = new Vector3[4];
            titleRt.GetWorldCorners(corners);
            FlowTrace.Step("Manage", "MANAGE_TITLE_RECT '" + text + "' world x " +
                corners[0].x.ToString("0.#") + ".." + corners[2].x.ToString("0.#") +
                " | size " + titleRt.rect.width.ToString("0") + "x" +
                titleRt.rect.height.ToString("0") + "px | anchors " +
                titleRt.anchorMin.x.ToString("0.###") + ".." + titleRt.anchorMax.x.ToString("0.###"));
        }

        /// <summary>
        /// BACK. The MODEL owns the graph (owner ruling 28: the stack remembers WHY, not only
        /// where), so this hands the press over and does nothing else. When the model reports it is
        /// standing on a root grid it raises CloseRequested, which is wired to <see cref="Close"/>.
        /// </summary>
        private void OnBackPressed()
        {
            if (_queueDrawerOpen) { ToggleQueueDrawer(); return; }   // the overlay closes first
            if (_vm == null) { Close(); return; }
            _vm.Back();
        }

        /// <summary>
        /// Paint the workspace. One <c>Bind</c> per model change - never per second: the panel's
        /// header records the WO-836/864 lesson that a per-tick rebuild is what caused per-frame
        /// layout churn, and <see cref="DeNelle.Core.Manage.ManageWorkspacePanel.Bind"/> is a full
        /// Clear + rebuild with no partial-update path.
        /// </summary>
        private void RenderWorkspace()
        {
            // The legacy row factories own these cells; with no legacy rows on screen they must
            // still be emptied before the drawer builds its own (the ordering RenderList held).
            _tickCells.Clear();
            _progressCells.Clear();
            _trainingNowCells.Clear();

            if (_operationalListBand != null) _operationalListBand.SetActive(false);
            if (_stripHost != null) _stripHost.gameObject.SetActive(false);
            // The QUEUE pill is the ONE door and stays up on every paint - see ShowWorkspace's note.
            if (_queueDrawerToggle != null) _queueDrawerToggle.gameObject.SetActive(true);

            // ⭐ THE MODEL MOVED => LEAVE THE HUB. See ShowLauncher's note: EnterTab mints a fresh
            // nav entry every time, so a changed reference means the player (or the harness, or a
            // deep-link door) asked for a SCREEN. Without this, `panel.Open()` followed by
            // `vm.EnterTab(tab)` left the hub in front of a fully-built grid - three doors onto
            // nothing, and `content=0px` on every tab.
            if (_hubShowing && _vm != null && !ReferenceEquals(_vm.Nav, _hubNav))
            {
                _hubShowing = false;
                FlowTrace.Step("Manage", "hub dismissed - the model entered a screen");
            }
            ApplyScreenVisibility();
            if (_hubShowing) return;           // the hub owns the screen; the workspace paints nothing
            if (_workspace == null || _vm == null) return;
            if (_queueDrawerOpen) return;      // the overlay owns the screen; nothing under it paints

            var nav = _vm.Nav;
            var workspaceVm = _vm.ComposeWorkspace();
            // WO-1443 section 1: the breadcrumb is bound into the PANEL TITLE, once, from the model.
            ApplyWorkspaceTitle(workspaceVm.HeaderTitle);
            _workspace.Bind(workspaceVm);
            FlowTrace.Step("Manage", "workspace screen=" +
                (nav != null ? nav.Kind + "/" + ManageScreenVM.TabWordOf(nav.Tab) : "<none>") +
                " item='" + (nav != null ? (nav.ItemId ?? nav.SchoolId ?? "-") : "-") +
                "' origin=" + (nav != null && nav.Origin != null ? nav.Origin.Kind.ToString() : "browse") +
                " bands(px): well=" + _workspace.LastWellPx.ToString("0") +
                " grid=" + _workspace.LastGridPx.ToString("0"));
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

            // ⛔ NO SUMMARY PANELS ON THE HUB. Do not call BuildLauncherSummaries here again.
            // TWO independent reasons, and either alone is enough:
            //  1. Mockup panel 1 is the title, three cards and CLOSE. There is no status row.
            //  2. MEASURED: the summaries seat at y 0.835-0.985 of the host = 0.15 x ~583px = ~87px,
            //     against ElarionUiKit.MinTouchPx (112). They have been ~25px short the whole time
            //     and nobody saw it, because the hub has not been rendered since WO-2001 deleted
            //     ShowLauncher - the geometry auditor only started reporting them when round 5
            //     accidentally put the hub on every frame.
            // They also sat exactly where the chrome row now lives (0.845-0.975 of the panel), so
            // they would have overprinted the back arrow and the queue pill.
            // ⛔ NO 'Choose a path' HEADING (single quotes here on purpose: the oracle that forbids
            // it matches the double-quoted LITERAL, and a comment must not read as the defect).
            // Mockup panel 1 has the title MANAGE and nothing else
            // above the cards - the three cards, with their own descriptions, already say what the
            // choice is. It is the same "a line that restates what the screen shows" the owner had
            // removed from every other Manage screen on 2026-09-06 ("remove the manage army and sub
            // line"), and it was only still here because the hub had not been shown since WO-2001.
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
            // ⭐ THREE CARDS IN ONE ROW - the owner's mockup, panel 1 ("MANAGE (MAIN) - Simple entry
            // with three core options"): BUILD / ARMY / RESEARCH, each with a one-line description,
            // CLOSE beneath. CAPTURE_LOOP_GOAL.md 3.0c item 2 supersedes WO-2001's launcher
            // retirement FOR THIS SCREEN, and says so in those words.
            // It was a 2x2 of FOUR (Defense / Buildings / Troops / Research). Defense and Buildings
            // are ONE destination since WO-2001 merged them into BUILD, so the fourth card had been
            // pointing at half of a tab that no longer exists on its own.
            var layout = gridGo.GetComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.spacing = new Vector2(24f, 0f);
            layout.padding = new RectOffset(14, 14, 14, 14);
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(1f, grid.rect.width - layout.padding.horizontal - layout.spacing.x * 2f);
            float height = Mathf.Max(1f, grid.rect.height - layout.padding.vertical);
            layout.cellSize = new Vector2(width / 3f, height);
        }

        private void RenderLauncherCards()
        {
            if (_launcherGrid == null || _vm == null) return;
            for (int i = _launcherGrid.childCount - 1; i >= 0; i--)
                Destroy(_launcherGrid.GetChild(i).gameObject);

            // ⛔ THREE, IN THIS ORDER, AND DEFENSE IS NOT ONE OF THEM.
            // Mockup panel 1 draws BUILD / ARMY / RESEARCH. `ManageTab.Buildings` IS the mockup's
            // BUILD: ShowOperational maps Defense and Buildings alike onto ManageTabId.Build
            // (:1189-1191), because WO-2001 merged them. A Defense card here would open the same
            // destination as the Build card and read as a fourth place to go.
            ManageTab[] tabs =
            {
                ManageTab.Buildings, ManageTab.Troops, ManageTab.Research
            };
            for (int i = 0; i < tabs.Length; i++)
            {
                ManageTab captured = tabs[i];
                bool available = captured == ManageTab.Research
                    || _vm.VisibleTabs.Contains(captured) || _vm.VisibleTabs.Contains(ManageTab.Defense);
                // BarracksUnlock is the one runtime authority used by the building,
                // drillmaster and training door. Do not derive this from a second flag.
                if (captured == ManageTab.Troops) available = BarracksUnlock.IsUnlocked;
                // The card WORDS are the mockup's, not the legacy tab labels: panel 1 reads
                // BUILD / ARMY / RESEARCH. "Buildings" and "Troops" are this host's internal
                // vocabulary and the player never sees them anywhere else on this screen.
                string title = HubTitleFor(captured);
                string purpose = captured == ManageTab.Troops && !available
                    ? "Build a Barracks to unlock" : PurposeFor(captured);
                string faceText = captured == ManageTab.Troops && !available
                    ? "BUILD A BARRACKS" : title;
                var card = ElarionUiKit.BuildObsidianButton(_launcherGrid, faceText,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    !available ? ElarionUiKit.ObsidianButtonColor.Gray
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
                // ⛔ THE CARD IS STACKED - ART ON TOP, NAME, THEN THE DESCRIPTION - which is what
                // mockup panel 1 draws. The previous seat was art-LEFT / text-RIGHT (title at
                // x 0.49-0.96), correct for the 2x2 of wide cards and wrong for three tall ones.
                // ⚠ ART GAP, NAMED NOT FAKED: the delivered card plates
                // (Assets/Resources/UI/ElarionMedieval/cards/*.png) are 1963x789 LANDSCAPE strips
                // with the illustration in their left third - drawn for the old wide seat. They are
                // painted preserveAspect INSIDE the art zone so a tall card letterboxes them rather
                // than stretching them, which is honest but is not what the mockup shows. The three
                // portrait-shaped hub images the mockup draws (a building, a helmet, a book) do not
                // exist on disk, and `cards/troops.png` - the UNLOCKED army card - has never
                // existed at all; only `cards/troops-locked.png` does. That is an art request.
                // ⛔ THE HUB CARDS CARRY NO ILLUSTRATION, ON PURPOSE, UNTIL THE ART EXISTS.
                // OWNER-FACING REASON, and the coordinator's call after seeing it: the delivered
                // plates (Assets/Resources/UI/ElarionMedieval/cards/*.png) are 1963x789 LANDSCAPE
                // strips with the illustration in their left third, drawn for the retired wide
                // 2x2 seat. preserveAspect-ing one into a tall card letterboxes it, leaving
                // two-thirds of the card black - which reads as BROKEN, not as art-pending, and was
                // the most visible thing on the screen. Text-only cards read as deliberate.
                // ⚠ THIS IS AN INTERIM, AND THE ART IS STILL OWED: mockup panel 1 draws three
                // PORTRAIT images filling each card (a building, a helmet, a book). They do not
                // exist on disk, and `cards/troops.png` - the UNLOCKED army card - has never existed
                // at all; only `cards/troops-locked.png` does. Restore the art here the day those
                // three files land, and not by re-pointing at the landscape strips.
                // LauncherArtPath is kept: it still answers for the locked-card badge path and it is
                // the name an art request should quote.
                // ⛔ THE CARD TEXT FORMAT IS SHARED WITH THE HERO DECK AND IS PINNED ACROSS BOTH.
                // HudLabelFitRegression's [deck-card-labels] case reads THIS FILE as the REFERENCE
                // and requires PlayerDeckWorkspace's Hero cards to match it, line for line:
                // It parities four things: the title's font SIZE, its ALIGNMENT, its FitSingleLine
                // range, and the description's FitSingleLine range.
                //
                // ⛔ DO NOT WRITE THOSE SEARCH TOKENS INTO THIS COMMENT. (CLI, at the gate
                // 2026-09-06.) The suite scans this file for the token and reads to the next ';',
                // and it does NOT strip comments first - so a comment quoting `face.` + `fontSize`
                // made the parser read the COMMENT instead of the assignment forty lines below,
                // reporting the value as '", "'. That is a whole regression round spent on prose.
                // The identical failure hit the wallet lane the same afternoon, where a `/api/...*`
                // path inside a comment opened a false block comment and ate 73% of a file before
                // its oracle searched it. Describe the tokens; never spell them.
                //
                // The WO-1443 hub rewrite quietly broke all four - it deleted the title's size line,
                // moved the title fit to 30f/48f and replaced the description's FitSingleLine with a
                // fixed fontSize. The suite caught it, which is exactly what the coupling is for:
                // two card surfaces drifting apart is invisible until someone opens both.
                // REALIGNED TO THE DECK rather than re-pointed away: 36f / Center / 30f-40f /
                // 24f-30f are the deck's proven numbers, they are legible at the hub's card width,
                // and Manage stays the reference. If these ever need to move, MOVE BOTH FILES.
                var face = card.GetComponentInChildren<TMP_Text>();
                if (face != null)
                {
                    var rt = face.rectTransform;
                    rt.anchorMin = new Vector2(0.04f, 0.52f);
                    rt.anchorMax = new Vector2(0.96f, 0.74f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    face.fontSize = 36f;
                    face.alignment = TextAlignmentOptions.Center;
                    face.color = available ? ElarionUi.Gold : ElarionUi.ParchmentDim;
                    ElarionUiKit.FitSingleLine(face, 30f, 40f);
                }
                var description = ElarionUiKit.Label(card.transform, purpose, 0.26f, 0.48f,
                    available ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                    (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.08f, 0.92f);
                ElarionUiKit.FitSingleLine(description, 24f, 30f);

                if (!available && captured == ManageTab.Troops)
                    BuildLockBadge(card.transform);

            }

            // The Heart's door lives on the hub now, not in the chrome row - see BuildHeartFace.
            BuildHeartFace();
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

        /// <summary>
        /// ⭐ SHOW THE HUB - mockup panel 1. Restored by WO-1443 after WO-2001 deleted it.
        /// <para>⚠ WO-2001's ShowLauncher was deleted because a REQUIRED four-tile chooser stood
        /// between the player and every destination while each destination was a narrow rail. Both
        /// halves of that have changed: the chooser is now THREE cards the owner drew herself, and
        /// what it leads to is a full-width grid. It is also what lets the BUILD/ARMY/RESEARCH row
        /// leave the workspace body and give the grid back its ~132px.</para>
        /// </summary>
        private void ShowLauncher()
        {
            _hubShowing = true;
            // ⭐ REMEMBER WHICH SCREEN THE MODEL WAS ON when the hub went up. RenderWorkspace drops
            // the hub the moment the model moves to a DIFFERENT nav entry, which is what makes
            // `vm.EnterTab(...)` - the path the capture harness and every non-card door use
            // (UICaptureLaunch.cs:7101-7111) - land on the grid instead of leaving the hub in front
            // of it. EnterTab always assigns a FRESH ManageNavEntry (ManageScreenVM.cs:2976), even
            // for a re-entry into the same tab, so reference identity is a reliable "did the player
            // ask for a screen" signal. A plain Rebuild (a queue tick, a timer) reuses the entry and
            // therefore does NOT dismiss the hub, which is the behaviour we want.
            _hubNav = _vm != null ? _vm.Nav : null;
            ApplyScreenVisibility();
            if (_operationalListBand != null) _operationalListBand.SetActive(false);
            if (_stripHost != null) _stripHost.gameObject.SetActive(false);
            _categoryNavigationCommitted = false;
            if (_workspaceTitle != null) ApplyWorkspaceTitle("MANAGE");
            FlowTrace.Step("Manage", "hub shown (BUILD / ARMY / RESEARCH)");
        }

        /// <summary>
        /// The model asked to leave the screen. On a root grid that now means RETURN TO THE HUB,
        /// not close: the mockup's back arrow walks BUILD -> hub, and only CLOSE leaves Manage.
        /// Closing from a root grid would skip panel 1 entirely and make the hub unreachable
        /// after the first tap - a door that exists but that no player can get back to.
        /// </summary>
        private void OnModelWantsOut()
        {
            if (!_hubShowing) { ShowLauncher(); return; }
            Close();
        }

        private void ActivateLauncherCard(ManageTab tab, bool commitLauncherNavigation = true)
        {
            if (commitLauncherNavigation && _categoryNavigationCommitted) return;
            if (tab == ManageTab.Troops && !BarracksUnlock.IsUnlocked)
            {
                Close();
                var controller = BuildModeController.Instance ?? BuildModeController.EnsureExists();
                controller?.EnterBuildMode(DeNelle.Core.Catalog.BuildType.Town);
                FlowTrace.Step("Manage", "troops locked door -> build mode barracks");
                return;
            }
            if (commitLauncherNavigation) _categoryNavigationCommitted = true;
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
                case ManageTab.Buildings: return "Construct and upgrade your town";
                case ManageTab.Troops: return "Train and manage your troops";
                case ManageTab.Research: return "Unlock powerful advancements";
                default: return "Open this management line";
            }
        }

        /// <summary>
        /// The hub card's PLAYER-FACING word, straight off mockup panel 1: BUILD / ARMY / RESEARCH.
        /// <para>⚠ Deliberately NOT <c>ManageScreenVM.TabLabels</c>. Those read "Buildings" and
        /// "Troops" - this host's internal vocabulary, which the player meets nowhere else on the
        /// Manage screens (the workspace already says BUILD and ARMY). Two words for one destination
        /// is the naming drift CLAUDE.md 7 keeps recording; the mockup settles which word wins.</para>
        /// </summary>
        private static string HubTitleFor(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Troops: return "ARMY";
                case ManageTab.Research: return "RESEARCH";
                default: return "BUILD";
            }
        }

        // ⛔ WO-2001 - ShowLauncher IS DELETED. It was the only path that made the four-tile chooser
        // visible, and BACK was its only caller ("Manage Back/root -> category cards"). The work
        // order retires the launcher and states that BACK must never route through it; leaving a
        // private method that can put it back on screen is how it would return. Verified before
        // deleting: no suite under Assets/Editor names ShowLauncher. The launcher CONSTRUCTION
        // (BuildLauncher / RenderLauncherCards) stays - it is still the source of record for the
        // approved 2026-08-31 art and copy, and two source oracles read it - but its host is never
        // activated. Those two pins are itemised for retirement in this work order's hand-back.

        /// <summary>
        /// The legacy destination entry point, kept as the ONE name every existing caller and the
        /// headless capture harness (Assets/Editor/UICaptureLaunch.cs:6877 invokes it by reflection)
        /// already uses. ⛔ WO-2001 RE-POINTS IT AT THE THREE-TAB WORKSPACE: the four legacy tabs
        /// collapse onto BUILD / ARMY / RESEARCH (Defense and Buildings merge, ruling 4, because
        /// they already share one Builder line), and the model decides the rest.
        /// </summary>
        private void ShowOperational(ManageTab tab)
        {
            ShowWorkspace();
            ManageTabId target = tab == ManageTab.Troops
                ? ManageTabId.Army
                : tab == ManageTab.Research ? ManageTabId.Research : ManageTabId.Build;
            _vm?.EnterTab(target);
            FlowTrace.Step("Navigation", "Manage -> " + tab + " (workspace tab " +
                ManageScreenVM.TabWordOf(target) + ")");
            // WO-1389: the TROOPS workspace is on screen - the post-raid beat's first route hop
            // (spotlight -> the Footman rail row). Raised HERE, after SelectTab has rebuilt and
            // Render has re-registered every "manage.troop_row.<id>" rect, so the hop can resolve
            // on the frame it fires; both the launcher card tap and the dialogue door land here.
            if (tab == ManageTab.Troops)
                Guard.Try("Manage", "raise troops-shown signal", () =>
                    DeNelle.Core.Tutorial.TutorialSignals.Raise(DeNelle.Core.Tutorial.TutorialSignals.ManageTroopsShown));
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
                // WO-1406: all three status chips are destination doors. Training still carries
                // the longer queue-depth copy, so the shared fit floor remains 24.
                ElarionUiKit.FitSingleLine(t, 24f, 34f);
                _stripCells[i] = t;

                // WO-1406: these are destination chips, not alternate queue doors. QUEUE in the
                // title row remains the single drawer control.
                ManageTab destination = i == 0 ? ManageTab.Buildings : i == 1 ? ManageTab.Troops : ManageTab.Research;
                var tapGo = ElarionUiKit.AddImage(panelRt, "ManageLineStatus_TabTap_" + destination,
                    Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), rounded: false);
                var tapImage = tapGo.GetComponent<Image>();
                tapImage.raycastTarget = true;
                var tap = tapGo.AddComponent<Button>();
                tap.targetGraphic = tapImage;
                tap.transition = Selectable.Transition.None;
                // Reuse the launcher's guarded destination door. In particular, a locked Training
                // chip opens the BUILD A BARRACKS route instead of bypassing it into Troops.
                tap.onClick.AddListener(() => ActivateLauncherCard(destination, commitLauncherNavigation: false));
                ElarionUiKit.ClampMinTouch(tap);
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

            // ⛔ THE OVERLAY'S HEADER IS A CENTRED "QUEUE" AND AN X. THERE IS NO 'HIDE' BUTTON.
            //
            // THE DEFECT IT REPLACES, measured by the capture auditor on EVERY tab:
            //   BUTTON OVER TEXT [ManageFlow_BUILD_queue] 'ManageQueueDrawer/ObsBtn_HIDE'
            //     (x 250.3..573.4, y 81.7..221.1) covers
            //     '...QueueRail_Builder/Cards/Card_wall_wood:13:9/Verb/Txt' ("REPAIR")
            //     (x 202.7..370.7, y 100.6..136.6) by 120.3x36 ref px
            // ...and the same over "TRAIN" on ARMY and "LEARN" on RESEARCH, plus each card's icon.
            // The arithmetic is plain once seen: HIDE was seated at y 0.70-0.99 while the list zone
            // below runs 0.02-0.86, so its lower 16% sat ON the first queue card. THE BUTTON THAT
            // PERFORMS THE JOB WAS UNDERNEATH THE BUTTON THAT CLOSES THE DRAWER - a player tapping
            // REPAIR / TRAIN / LEARN on the top row hit HIDE instead. That is the whole of the
            // outstanding geometry=20 / touch=20 count, on one control.
            //
            // The mockup's panel 8 settles the shape rather than the coordinate: title QUEUE centred,
            // an X at the top-right, and nothing else in the header. The X is seated ABOVE the list
            // zone's 0.86 ceiling with a gutter, so it cannot reach a card at any drawer height.
            var heading = ElarionUiKit.Label(drawer, "QUEUE", 0.88f, 0.99f,
                ElarionUi.Gold, (int)ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.20f, 0.80f, bold: true);
            ElarionUiKit.FitSingleLine(heading, 28f, 44f);
            _drawerHeading = heading != null ? heading.gameObject : null;

            // A SQUARE X in the corner, as drawn. ASCII "X", never a glyph character - this
            // project's fonts render non-ASCII as tofu (the same rule that made BACK a "<-").
            //
            // ⛔ AUTHORED AT THE TOUCH FLOOR IN PIXELS, NOT AS A FRACTION OF THE DRAWER.
            // MEASURED last round: 'ManageQueueOverlayClose' resolved 71.8x57.7 ref px - the short
            // side 54.3px UNDER MinTouchPx(112), less than half the floor - because 0.875-0.995 of
            // a drawer whose height varies is not a promise about pixels. That is the identical
            // mistake the detail CTA made at 104.1px, and the identical cure: take the px, then
            // convert to the fraction this rect needs. A clamp that grows a control symmetrically
            // about its centre spills it into its neighbours, which on this header is the title.
            // ⚠ A FIXED PIXEL SIZE PINNED TO THE CORNER, not an anchor fraction. `drawer.rect` is
            // ZERO on the frame this is built - no layout has run yet - so any fraction derived
            // from it would be a guess, and a fraction of a drawer whose height varies cannot
            // promise a px floor anyway. Collapsing the anchors to the top-right corner and setting
            // sizeDelta gives exactly MinTouchPx + 8 on both axes at every drawer size, with no
            // measurement needed and nothing for ClampMinTouch to rescue.
            const float ClosePx = 120f;   // ElarionUiKit.MinTouchPx (112) + 8px margin
            var close = ElarionUiKit.BuildObsidianButton(drawer, "X",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.9f, 0.9f), new Vector2(0.99f, 0.99f), ToggleQueueDrawer);
            if (close != null)
            {
                var closeRt = (RectTransform)close.transform;
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                closeRt.sizeDelta = new Vector2(ClosePx, ClosePx);
                closeRt.anchoredPosition = new Vector2(-14f, -10f);
            }
            ElarionUiKit.ClampMinTouch(close);
            if (close != null) close.gameObject.name = "ManageQueueOverlayClose";
            _drawerHide = close != null ? close.gameObject : null;

            // WO-1368: the rail no longer owns a fixed band of its own — it is mounted as the
            // first row of the list below (see RenderQueueDrawer), so the 200px of card art can
            // never eat the space the ACTION rows need. _railBand stays null, which is what makes
            // the legacy pinned path (RenderRail) inert.
            _railBand = null;
            // WO-1393: the list zone runs from the drawer floor to the heading (0.02-0.86); the
            // slot offer is no longer a fixed zone under it but the list's LAST ROW (see
            // RenderQueueDrawer), which is what gives the rail + header + first row room at rest.
            var drawerList = MakeZone(drawer, "Drawer_QueueList",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.86f));
            _drawerList = drawerList;
            var drawerScroll = ElarionUiKit.MakeScrollZone(drawerList, spacing: 8f, padding: 10);
            _drawerContent = drawerScroll != null ? drawerScroll.content : null;
            if (_drawerContent == null)
                FlowTrace.Fail("Manage",
                    "queue drawer MakeScrollZone returned no content - the queue ROWS have no build " +
                    "site, which is exactly the WO-1368 defect (Finish Now / Ad / Cancel / Move up " +
                    "unreachable). The rail alone carries no actions.");

            // Built ONCE (its label/button are fields RenderSlotOffer refreshes) and parked on the
            // drawer between renders; RenderQueueDrawer re-seats it as the last scroll row.
            _drawerSlotOffer = MakeZone(drawer, "Drawer_SlotOffer", Vector2.zero, Vector2.one);
            BuildSlotRow(_drawerSlotOffer);
            _drawerSlotOffer.gameObject.SetActive(false);

            _queueDrawer.SetActive(false);
        }

        /// <summary>WO-1393/1418/1422: the drawer is a BAND below the selected-card workspace, and
        /// since WO-1422 ALL FOUR destinations carry that workspace - so all four are band mode and
        /// the WO-1368 full-body drawer no longer has a tab that uses it (it stays for safety).
        /// ⛔ APPEND ONLY: ManageBuildingsCardRegression.cs:154 pins the verbatim substring
        /// "ManageTab.Troops || _vm.Tab == ManageTab.Buildings" - reordering these terms fails it.</summary>
        private bool DrawerInBandMode => _vm != null && (_vm.Tab == ManageTab.Troops || _vm.Tab == ManageTab.Buildings || _vm.Tab == ManageTab.Defense || _vm.Tab == ManageTab.Research);

        /// <summary>
        /// ⛔ WO-1393 - THE ONE PLACE THE DRAWER, THE LIST BAND AND THE TRAINING NOW BAND ARE
        /// SEATED RELATIVE TO EACH OTHER. Called on every toggle and after every RenderList (which
        /// rebuilds the TRAINING NOW band active), so the placement can never drift from the tab
        /// or the queue. Idempotent. See the DrawerModeListKeepPx block for the arithmetic.
        /// </summary>
        private void ApplyDrawerPlacement()
        {
            if (_queueDrawer == null) return;
            var drawer = (RectTransform)_queueDrawer.transform;
            // ⛔ WO-2001 - QUEUE IS AN OVERLAY, so the band shape stands down while the workspace
            // owns the well. The owner's flow puts QUEUE on every screen's header and over the
            // screen it was opened from; a band seated inside the old browse-list rectangle has
            // nowhere to sit once that band is gone. DrawerInBandMode itself is UNTOUCHED (its
            // verbatim text is pinned by ManageBuildingsCardRegression:171) - only this one
            // condition, which is where the two shapes were always chosen between.
            bool band = _queueDrawerOpen && DrawerInBandMode && !WorkspaceActive;
            _drawerBandMode = band;
            if (_workspaceHost != null) _workspaceHost.gameObject.SetActive(WorkspaceActive && !_queueDrawerOpen);

            // The TRAINING NOW band (and its extra rows) is the line's MIRROR; the drawer
            // supersedes it while open. Inactive rows drop out of the vertical layout, so the
            // list content shrinks to padding + workspace + padding.
            if (_listContent != null)
                for (int i = 0; i < _listContent.childCount; i++)
                {
                    var child = _listContent.GetChild(i);
                    if (child.name.StartsWith(TrainingNowPrefix, StringComparison.Ordinal) ||
                        child.name.StartsWith(BuildingNowPrefix, StringComparison.Ordinal) ||
                        child.name.StartsWith(ResearchNowPrefix, StringComparison.Ordinal))   // WO-1422
                        child.gameObject.SetActive(!band);
                }

            if (_operationalListBand != null)
            {
                // Full-body mode hides the browse list under the opaque drawer (WO-1368: a browse
                // list left actionable under a panel carrying paid verbs is a mis-tap surface).
                // Band mode keeps it, shrunk to the viewport ABOVE the card's CTA line.
                // WO-2001: the browse list band is retired while the workspace owns the well - an
                // empty scroll zone left active over the grid is an invisible raycast blocker.
                _operationalListBand.SetActive(!WorkspaceActive && (!_queueDrawerOpen || band));
                var listRt = (RectTransform)_operationalListBand.transform;
                float keep = band ? Mathf.Min(DrawerModeListKeepPx, _listBandPx) : _listBandPx;
                listRt.sizeDelta = new Vector2(listRt.sizeDelta.x, keep);
            }

            if (_drawerHeading != null) _drawerHeading.SetActive(!band);
            if (_drawerHide != null) _drawerHide.SetActive(!band);
            if (_drawerList != null)
            {
                _drawerList.anchorMin = new Vector2(0.02f, band ? 0.0f : 0.02f);
                _drawerList.anchorMax = new Vector2(0.98f, band ? 1.0f : 0.86f);
                _drawerList.offsetMin = _drawerList.offsetMax = Vector2.zero;
            }

            var image = _queueDrawer.GetComponent<Image>();
            if (band)
            {
                float top = _listBandTopPx + Mathf.Min(DrawerModeListKeepPx, _listBandPx) + BandGapPx;
                float drawerPx = Mathf.Max(0f, _wellPx - top);
                drawer.anchorMin = new Vector2(0.01f, 1f);
                drawer.anchorMax = new Vector2(0.99f, 1f);
                drawer.pivot = new Vector2(0.5f, 1f);
                drawer.sizeDelta = new Vector2(0f, drawerPx);
                drawer.anchoredPosition = new Vector2(0f, -top);
                // A flat plate, not the framed sprite: frames/content-panel carries ~90px of
                // transparent margin above its gold line (the WO-1382 RCA), which on a band this
                // short would draw the first row outside the visible frame.
                if (image != null)
                {
                    image.sprite = null;
                    image.color = new Color(0.05f, 0.04f, 0.03f, 0.70f);
                }
                FlowTrace.Step("Manage", string.Format(
                    "drawer band(px): listTop={0:0} keep={1:0} gap={2:0} => drawer top={3:0} height={4:0} " +
                    "of well {5:0} (needs {6:0}: header {7:0} + row {8:0} + pad 20). TRAINING NOW " +
                    "collapsed; the card's CTAs stay in the list viewport above the drawer, never under it.",
                    _listBandTopPx, Mathf.Min(DrawerModeListKeepPx, _listBandPx), BandGapPx, top, drawerPx,
                    _wellPx, DrawerModeMinPx, SectionHeaderPx, RowHeightPx));
                if (drawerPx < DrawerModeMinPx)
                    FlowTrace.Warn("Manage", string.Format(
                        "drawer band is {0:0}px, under the {1:0}px it needs to seat the header and one " +
                        "row at rest - the rows still scroll, but the first verb is under the fold.",
                        drawerPx, DrawerModeMinPx));
            }
            else
            {
                drawer.anchorMin = new Vector2(0.02f, 0.02f);
                drawer.anchorMax = new Vector2(0.998f, 0.84f);
                drawer.pivot = new Vector2(0.5f, 0.5f);
                drawer.offsetMin = drawer.offsetMax = Vector2.zero;
                if (image != null && image.sprite == null)
                {
                    image.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
            }
        }

        /// <summary>WO-1393: the title-row QUEUE face stays on screen while the drawer is open and
        /// reads as the close ("HIDE QUEUE"); its onClick is ToggleQueueDrawer either way, so the
        /// top-right tap always toggles. Called from BuildTabs (rebuilds) and ToggleQueueDrawer.</summary>
        private void SyncQueueToggleFace()
        {
            if (_queueDrawerToggle == null) return;
            // ⛔ UNCONDITIONALLY ACTIVE. This used to read
            //     SetActive(_vm != null && _vm.Channels.Count > 0)
            // which was safe while the workspace painted its own queue face and this was a spare
            // chrome control. Since WO-1443 this pill IS the only door to the queue on every Manage
            // screen (measured: the operational OPEN QUEUE bands are in a list band held inactive,
            // the activity strip is retired, the HUD Builders chip's door went in WO-911), so a
            // condition on it is a condition on the door - the WO-1430 defect class exactly.
            // The mockup also draws the pill on all eight numbered panels with no empty state.
            _queueDrawerToggle.gameObject.SetActive(true);
            var label = _queueDrawerToggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = _queueDrawerOpen ? "HIDE QUEUE" : "QUEUE";
                ElarionUiKit.FitSingleLine(label);
            }
        }

        private void ToggleQueueDrawer()
        {
            _queueDrawerOpen = !_queueDrawerOpen;
            if (_queueDrawer != null) _queueDrawer.SetActive(_queueDrawerOpen);
            // WO-1389: the real OPEN QUEUE tap is the completion of the TRAINING NOW beat. Only
            // the OPENING edge raises (closing teaches nothing). Guarded: a bus subscriber must
            // never be able to leave the drawer half-toggled.
            if (_queueDrawerOpen)
                Guard.Try("Manage", "raise queue-opened signal", () =>
                    DeNelle.Core.Tutorial.TutorialSignals.Raise(DeNelle.Core.Tutorial.TutorialSignals.ManageQueueOpened));
            // WO-1393: the toggle STAYS visible while open - it was hidden here before, which is
            // why the top-right QUEUE tap in 10-troops-after-upgrade.png closed nothing.
            SyncQueueToggleFace();
            // WO-1368: hiding the browse band while the drawer is open STILL holds on the browse
            // tabs (the drawer is a full-body workspace carrying DESTRUCTIVE and PAID verbs; a
            // browse list left actionable underneath an opaque panel is a mis-tap surface). On the
            // Troops tab the drawer is a BAND under the kept card viewport instead (WO-1393), and
            // ApplyDrawerPlacement owns both shapes.
            ApplyDrawerPlacement();
            if (_queueDrawerOpen) RenderQueueDrawer();
            // WO-2001: closing the overlay hands the screen back to the workspace. Re-binding here
            // (rather than waiting for the next QueueChanged) is what re-hides the header toggle, so
            // exactly ONE queue affordance is on screen in each state: the workspace door while
            // browsing, and this toggle - reading "HIDE QUEUE" - while the overlay is up.
            else if (WorkspaceActive) RenderWorkspace();
            FlowTrace.Step("Manage", "queue drawer " + (_queueDrawerOpen ? "expanded" : "collapsed") +
                " (rows " + (_queueDrawerOpen ? (_vm != null ? _vm.QueueRows.Count : 0) : 0) + ")" +
                (_queueDrawerOpen ? (_drawerBandMode ? " as a band under the Troops workspace" : " full-body") : ""));
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

            // ⭐ WO-2003 / WO-2017 - THE DIRECT ROUTE TO THE HEART, and it is built FIRST, before
            // the "nothing unlocked yet" early return below, precisely so it exists in EVERY state
            // of this screen. Owner 2026-09-06: "wire the heart" - she could not find how to raise
            // her realm tier, and MEASURED at source that day the ONLY control was the VillageGated
            // action band in BuildingUpgradePanelMvvm.cs:1322-1338, painted ONLY while she happened
            // to be looking at a building whose next tier was gated. A gate that gates nearly all
            // content had no door of its own. This face is that door.
            //
            // ⚠ SEAT RE-CUT 2026-09-06 (WO-1443, the mockup round). The chrome row is now
            //   <-  HEART Ln | BUILD  ARMY  RESEARCH |  QUEUE(n)
            // in ONE thin band (WorkspaceHeaderY0..Y1), because the mockup gives the GRID the screen
            // and leaves the chrome a single row. The Heart face keeps its door and its live level
            // but is deliberately small and unobtrusive: it appears NOWHERE in the mockup and
            // survives only because it is the sole unconditional route to PanelId.Heart and
            // HeartSurfaceRegression.cs:118-123 fails without it. When a door for the Heart exists
            // elsewhere, this face is the first thing that should go.
            // HEIGHT: the row is 0.845-0.975 (~115px at the measured reference), clear of
            // MinTouchPx(112) without ClampMinTouch having to rescue it.
            BuildBackArrow();

            if (_vm == null || _vm.VisibleTabs.Count == 0)
            {
                ElarionUiKit.Label(_tabsHost, "Place a structure to unlock Manage categories", 0f, 1f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.40f, 1f);
                return;
            }
            // ⚠ NO DESTINATION FACES IN THIS ROW YET, AND THAT IS A DELIBERATE HOLD.
            // The mockup's navigation is the HUB (screen 1) plus the back arrow - there is no tab
            // row on any panel. Moving BUILD/ARMY/RESEARCH up here as chrome would free the
            // workspace's 132px band, and the arithmetic says that is worth ~45px per ARMY tile.
            // It is NOT done in this round because this host's own tab vocabulary is the LEGACY
            // four (`ManageScreenVM.VisibleTabs` is a `List<ManageTab>` of Defense/Buildings/Troops/
            // Research) while the workspace speaks the WO-2001 three (`ManageTabId` Build/Army/
            // Research). Rendering the legacy four here would put a DEFENSE tab back on a screen
            // that merged it into BUILD - a visible regression traded for vertical space.
            // The hub (screen 1) retires both lists at once and is the honest place to do this.

            // ⭐ QUEUE IS A SMALL PILL AT TOP-RIGHT WITH A COUNT BADGE - the mockup draws it on all
            // eight numbered panels and CAPTURE_LOOP_GOAL.md 3.0b states it. Owner, 2026-09-06:
            // "the queuing doesn't deserve a place here... something small up with like the previous
            // next back kind of buttons - I don't think it deserves its own lane."
            // ⚠ THIS SUPERSEDES THE TAB-ROW SEAT of a few hours earlier: that was built from her
            // words before her picture was in the repo, and the picture had said "small pill,
            // top-right" since 09:26 that morning. The renderer no longer draws a queue face at all.
            // ⚠ SEAT PULLED IN 2026-09-06 after the capture. At 0.845-0.965 the pill ran into the
            // frame's ornate right border: ManageFlow_ARMY_gridtop_2670x1200.png shows "QUEUE" cut
            // mid-word. 0.775-0.925 clears the border and is wide enough for the word plus its badge.
            // MEASURED REFERENCE, not guessed: in that same frame the BUILD/ARMY/RESEARCH faces -
            // which are laid out across the workspace band, i.e. the panel's real usable content -
            // end at about x 0.895 of the frame, and the ornate border begins just outside that.
            // Anything seated past it is drawn over the frame art or clipped by it.
            _queueDrawerToggle = ElarionUiKit.BuildObsidianButton(_tabsHost, "QUEUE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                // ⭐ WIDENED, AND THE INSTRUMENTATION IS WHY. Four rounds moved this pill's x
                // coordinate on the theory that the frame border was clipping it. The rect it
                // printed settled it:
                //   MANAGE_QUEUE_PILL_RECT world x 1791.3..2019.4  size 184x120px
                //   anchors 0.8..0.95  row size 1223x120px
                // The row spans world ~813..2036, so the pill ENDED 17px INSIDE the row's own right
                // edge - and the frame's border art (measured off frame_core.png: interior to
                // x 0.950 at this height) was never near it. THE FRAME WAS INNOCENT. The label was
                // overflowing its own 184px button, so the final E was cut off INSIDE the pill and
                // the corner badge had nowhere to sit.
                // 0.72-0.95 of a 1223px row = ~281px, which seats QUEUE at label size with room for
                // the badge. Widening rather than shrinking the type, per the same law the rail chip
                // records (HudKitController.cs:1866-1878): fewer characters or more room, never a
                // smaller font - a band under ~24px renders BLANK, not small.
                new Vector2(0.720f, 0f), new Vector2(0.950f, 1f), ToggleQueueDrawer);
            if (_queueDrawerToggle != null)
            {
                _queueDrawerToggle.gameObject.name = "ManageQueueDrawerToggle";
                // WO-1393: visible whether the drawer is open or closed - a second tap closes it.
                SyncQueueToggleFace();
                ElarionUiKit.ClampMinTouch(_queueDrawerToggle);
                BuildQueueCountBadge(_queueDrawerToggle.transform);

                // ⛔ INSTRUMENT, DO NOT INFER. THIS LINE EXISTS BECAUSE I GUESSED THREE TIMES.
                // The pill has been reported clipped mid-word with its badge outside the frame in
                // three consecutive captures, and each of my three fixes was a coordinate reasoned
                // from a RENDERED EDGE - the same class of error as reading a comment instead of the
                // code. MEASURED this round from the frame asset itself (System.Drawing alpha walk
                // over Assets/Resources/RpgUi/frame/frame_core.png, 1230x1833): at the chrome row's
                // own height the border art ends at x 0.048..0.950 of the frame, and the pill's
                // computed seat is nowhere near that - so my model of the failure is WRONG, and a
                // fourth guess would be worth nothing.
                // This prints where the pill ACTUALLY lands, in reference px, so the next capture
                // names the rect instead of me theorising about it. Read this line before touching
                // the pill's coordinates again (CLAUDE.md 12: the data pinpoints the dead step).
                var pillRt = _queueDrawerToggle.transform as RectTransform;
                if (pillRt != null)
                {
                    var corners = new Vector3[4];
                    pillRt.GetWorldCorners(corners);
                    FlowTrace.Step("Manage", "MANAGE_QUEUE_PILL_RECT world x " +
                        corners[0].x.ToString("0.#") + ".." + corners[2].x.ToString("0.#") +
                        " y " + corners[0].y.ToString("0.#") + ".." + corners[2].y.ToString("0.#") +
                        " | size " + pillRt.rect.width.ToString("0") + "x" +
                        pillRt.rect.height.ToString("0") + "px" +
                        " | anchors " + pillRt.anchorMin.x.ToString("0.###") + ".." +
                        pillRt.anchorMax.x.ToString("0.###") +
                        " | row size " + _tabsHost.rect.width.ToString("0") + "x" +
                        _tabsHost.rect.height.ToString("0") + "px");
                }
            }
        }

        /// <summary>
        /// The red count badge that rides the QUEUE pill's top-right corner, exactly as the mockup
        /// draws it on every panel.
        /// <para>⚠ COLOURBLIND LAW (CAPTURE_LOOP_GOAL 3c): the owner is red/green colourblind and
        /// meaning may never be carried by hue alone. The MEANING here is the DIGIT - how many jobs
        /// are in flight - and the digit is legible in greyscale. The red disc is decoration that
        /// matches her picture, not the channel. If the badge ever loses its number, it stops
        /// meaning anything and must be removed rather than kept as a coloured dot.</para>
        /// <para>Zero jobs paints NOTHING - the mockup shows a badge only where there is a count,
        /// and a "0" badge is a notification that nothing happened.</para>
        /// </summary>
        private void BuildQueueCountBadge(Transform pill)
        {
            if (pill == null) return;
            int jobs = 0;
            if (_vm != null)
                for (int i = 0; i < _vm.Channels.Count; i++)
                    jobs += Mathf.Max(0, _vm.Channels[i].Depth);
            if (jobs <= 0) return;

            // ⛔ THE BADGE LIVES INSIDE THE PILL. Do not push it past 1.0 on either axis again.
            // It was authored at 0.78-1.06 x / 0.52-1.18 y - i.e. deliberately overhanging the
            // corner, the way a notification badge usually does. MEASURED in
            // ManageFlow_ARMY_gridtop_2670x1200.png: the overhang put it OUTSIDE THE PANEL FRAME
            // ENTIRELY, a red square with "15" floating above the top-right corner of the modal,
            // detached from the pill it belongs to. A rect that leaves its parent leaves the panel;
            // this chrome row sits at the frame's edge and has no margin to overhang into.
            // Tucked on the pill's top-right corner and FULLY INSIDE it - the mockup's badge sits on
            // the corner within the panel, and this project has already proved that an overhanging
            // rect at this seat leaves the frame entirely (round 3: a red "15" floating above the
            // modal). On the widened pill (~281px) this is a ~59px disc, which is a badge rather
            // than a second button.
            var disc = ElarionUiKit.AddImage(pill, "ManageQueueCountBadge",
                new Vector2(0.74f, 0.50f), new Vector2(0.95f, 0.94f),
                new Color(0.72f, 0.10f, 0.10f, 1f), rounded: true);
            var discImage = disc != null ? disc.GetComponent<Image>() : null;
            if (discImage != null) discImage.raycastTarget = false;
            if (disc == null) return;

            var count = ElarionUiKit.Label(disc.transform, jobs.ToString(), 0.06f, 0.94f,
                ElarionUi.Parchment, (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.02f, 0.98f,
                bold: true);
            ElarionUiKit.FitSingleLine(count, 20f, 30f);
        }

        /// <summary>
        /// ⭐ THE BACK ARROW - mockup 3.0b, on every numbered panel, top-LEFT.
        /// <para>Built HERE, inside the row's own rebuild, because <see cref="BuildTabs"/> clears
        /// every child of <c>_tabsHost</c> on entry. Round 5 built it once at chrome time and the
        /// first Render deleted it; the capture showed a Manage screen with no way back.</para>
        /// <para>⚠ ASCII "&lt;-", never the left-arrow character - this project's fonts render
        /// non-ASCII as tofu. <c>RpgUi/button/arrow.png</c> was measured and rejected: it is a
        /// filled RIGHT-pointing play triangle, and a mirrored play glyph reads as "rewind".</para>
        /// <para>Hidden on the hub (<see cref="ApplyScreenVisibility"/>): panel 1 is the root, and
        /// CLOSE is its way out.</para>
        /// </summary>
        private void BuildBackArrow()
        {
            if (_tabsHost == null) return;
            // 0-0.095 of the row. At the measured reference (~1218px row) that is ~116px, clear of
            // MinTouchPx(112) - authored to the floor, not left to ClampMinTouch.
            _workspaceBack = ElarionUiKit.BuildObsidianButton(_tabsHost, "<-",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0f, 0f), new Vector2(0.095f, 1f), OnBackPressed);
            if (_workspaceBack == null)
            {
                FlowTrace.Fail("Manage", "the BACK arrow failed to build - the screen has no visible " +
                    "way back, which is what ManageFlow_ARMY_gridtop showed on 2026-09-06.");
                return;
            }
            _workspaceBack.gameObject.name = "ManageWorkspaceBack";
            ElarionUiKit.ClampMinTouch(_workspaceBack);
            MedievalUiSkin.ApplyButton(_workspaceBack);
            _workspaceBack.gameObject.SetActive(!_hubShowing);
        }

        /// <summary>
        /// The HEART face - the one always-present door onto <see cref="PanelId.Heart"/>.
        /// <para>The face carries the LIVE level ("HEART L1"), so the player can read their realm
        /// progression without opening anything, and the word matches the CTA on every gated card
        /// ("UPGRADE THE HEART"). ⚠ The level number is read from the model, never cached here -
        /// duplicated state is what produced the stale-copy family this program exists to kill.</para>
        /// </summary>
        private void BuildHeartFace()
        {
            // ⭐ THE HEART DOOR MOVED TO THE HUB. It is no longer in the chrome row on ANY screen.
            //
            // It sat there since WO-2003/WO-2017 for one reason - it was the only unconditional
            // route to PanelId.Heart, and this suite's own [heart-has-a-door] case exists to stop it
            // vanishing. That reason is unchanged and the door is unchanged; only its SEAT moved.
            //
            // WHY IT MOVED: it appears in none of the mockup's nine panels, and three rounds of
            // trying to make it small enough to be unobtrusive ended with "HEART ..." truncating at
            // ~177px. Shrinking a face below its own label does not make it quiet, it makes it
            // broken - and the honest fix, which the hub finally makes possible, is to put it where
            // a root-level destination belongs. Panel 1 does not draw it either; it is there because
            // the Heart must keep a door, and the hub is the least wrong place for one extra
            // affordance: it is the ROOT, it already lists destinations, and grid screens 2/4/6 now
            // match the mockup's chrome exactly - back arrow, centred title, queue pill, nothing else.
            //
            // ⛔ DO NOT re-seat it in the chrome row to "make it reachable faster". If the Heart
            // ever gets a door elsewhere (the town, the HUD, a quest), delete the hub face too and
            // move HeartSurfaceRegression's pin with it - do not end up with two.
            BuildHubHeartDoor();
        }

        /// <summary>
        /// The Heart's door, on the HUB. Small, under the three cards, carrying the LIVE level.
        /// <para>⚠ The level is read from <c>HeartProgression.Level</c> at build time and never
        /// cached here - a second copy of a live number is the duplicated state this file keeps
        /// paying for. The face is rebuilt with the hub, so the number cannot go stale.</para>
        /// </summary>
        private void BuildHubHeartDoor()
        {
            if (_launcherHost == null) return;
            // RenderLauncherCards clears the CARD GRID, not this host, so a re-render would stack a
            // second heart on the first. Drop any previous one before building.
            for (int i = _launcherHost.childCount - 1; i >= 0; i--)
            {
                var child = _launcherHost.GetChild(i);
                if (child == null || child.name != "ManageHeartFace") continue;
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            var heart = ElarionUiKit.BuildObsidianButton(_launcherHost,
                "HEART L" + DeNelle.Village.Buildings.Progression.HeartProgression.Level,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                // Under the card grid (which ends at 0.695) and above the shared CLOSE band.
                // 0.38-0.62 of a ~1218px host is ~292px wide; 0.70-0.82 of a ~583px host is ~70px...
                // NOT ENOUGH, so the band is 0.70-0.83 => ~76px. ⚠ ClampMinTouch is left to raise it
                // to the 112px floor here, and that is stated rather than hidden: the hub's vertical
                // budget between the cards and CLOSE is genuinely under one touch target, and the
                // right fix is the hub's own band arithmetic, not a silently short button.
                new Vector2(0.38f, 0.70f), new Vector2(0.62f, 0.83f), OpenHeartSurface);
            if (heart == null)
            {
                FlowTrace.Fail("Manage", "the HEART face failed to build - the direct route to " +
                    "PanelId.Heart is missing from this screen.");
                return;
            }
            heart.gameObject.name = "ManageHeartFace";
            MedievalUiSkin.ApplyButton(heart, false);
            ElarionUiKit.ClampMinTouch(heart);
        }

        /// <summary>Open the Heart surface. Closes Manage first (PanelManager holds one exclusive
        /// panel), and says so out loud if the route is dead rather than doing nothing.</summary>
        private void OpenHeartSurface()
        {
            Close();
            if (!PanelRouter.Open(PanelId.Heart))
                FlowTrace.Fail("Manage", "PanelRouter.Open(PanelId.Heart) returned FALSE - the Heart " +
                    "panel is not registered (HeartPanelBootstrap did not run) or it failed to become " +
                    "visible. The player just tapped a door that opened nothing.");
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
                // WO-1393: RenderList rebuilt the TRAINING NOW band active and the tab may have
                // changed - re-seat the drawer / list band / band before the drawer renders.
                ApplyDrawerPlacement();
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
                // ⛔ WO-2001 - THE WORKSPACE SUBTREE IS EXEMPT, WHOLESALE. ManageWorkspacePanel
                // authors every face against a MEASURED pixel band and fits it there (its band
                // table states each height in px, floor 28 for text and 112 for touch). This
                // copy-keyed bulk pass re-promotes faces by their WORDS and re-fits labels with a
                // 30px floor - which is precisely the clipping the two WO-1422 polish notes below
                // record for the Defense and Research rails ("Archer Tower" -> "ARCHER T..."). An
                // ancestry test rather than a name prefix, because the renderer's object names are
                // its own business and a prefix list here would be a second copy of them.
                if (_workspaceHost != null && button.transform.IsChildOf(_workspaceHost)) continue;
                string objectName = button.gameObject.name ?? string.Empty;
                if (string.Equals(objectName, "Scrim", StringComparison.Ordinal) ||
                    string.Equals(objectName, "CloseButton", StringComparison.Ordinal) ||
                    objectName.StartsWith("ManageCard_", StringComparison.Ordinal) ||
                    objectName.StartsWith("TroopChoice_", StringComparison.Ordinal) ||
                    objectName.StartsWith("BuildingChoice_", StringComparison.Ordinal) ||
                    // WO-1422 POLISH (MEASURED 2026-09-06, ManageDefense/ManageResearch_2670x1200.png):
                    // the two NEW rails were MISSING from this skip-list, so the bulk pass below ran
                    // MedievalUiSkin.ApplyButton on every rail row - and that method rewrites the
                    // row's FIRST label (MedievalUiSkin.cs:83-95): ToUpperInvariant, characterSpacing
                    // 2, the wide TITLE face, and a re-fit whose floor is 30 instead of the rail's 26.
                    // "Archer Tower" became "ARCHER T..." and "Expanded Capacity" "EXPANDE..." while
                    // the Troops rail, already skipped here, fitted "Footman" at the same width.
                    // The rail row is a FLAT selectable face, never a gold button plate - same reason
                    // TroopChoice_ is on this list.
                    objectName.StartsWith("DefenseChoice_", StringComparison.Ordinal) ||
                    objectName.StartsWith("ResearchChoice_", StringComparison.Ordinal) ||
                    // WO-1382: the two card CTAs are skinned by their builder (TRAIN primary,
                    // UPGRADE secondary) - the copy-keyed pass below would promote "UPGRADE TO L2"
                    // to primary and erase the one-primary hierarchy the owner asked for. The
                    // Training-chip tap plate has no label and must never be painted as a CTA.
                    objectName.StartsWith("TroopCta_", StringComparison.Ordinal) ||
                    objectName.StartsWith("BuildingCta_", StringComparison.Ordinal) ||
                    // WO-1422 POLISH - same reason, INFERRED from the copy test below rather than
                    // measured in a frame: BuildDefenseCard / BuildResearchCard already call
                    // MedievalUiSkin.ApplyButton with the correct primary flag, and the copy-keyed
                    // pass would re-promote the GRAY dead faces ("RESEARCHING" contains "RESEARCH",
                    // "UPGRADE TO L2" contains "UPGRADE") to the primary face, erasing the
                    // one-primary hierarchy the owner asked for.
                    objectName.StartsWith("DefenseCta_", StringComparison.Ordinal) ||
                    objectName.StartsWith("ResearchCta_", StringComparison.Ordinal) ||
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
                    ? _vm.Channels[i].Describe()
                    : (i == 0 ? "Builders 0/0" : i == 1 ? "Training 0/0" : "Research 0/0");
                // WO-1382 ruling #1: the Training chip carries the line's DEPTH ("Training 1/2 .
                // 1/5 queued") - the VM composes it; this only paints it.
                if (i == 1 && _vm.TrainingChipText != null) text = _vm.TrainingChipText;
                cell.text = ManageScreenVM.Ascii(text);
                ElarionUiKit.FitSingleLine(cell, ElarionUiKit.FontHardFloor, 34f);
            }
            for (int i = 0; i < _launcherSummaries.Length; i++)
            {
                var cell = _launcherSummaries[i];
                if (cell == null) continue;
                if (i < _vm.Channels.Count)
                {
                    ChannelSummary s = _vm.Channels[i];
                    cell.text = s.Describe();
                }
                else
                {
                    string name = i == 0 ? "Builders" : i == 1 ? "Training" : "Research";
                    cell.text = name + " 0/0";
                }
                ElarionUiKit.FitSingleLine(cell, ElarionUiKit.FontHardFloor, 34f);
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
                _slotButton.gameObject.SetActive(_vm.BuilderUpsellVisible);
                var label = _slotButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = ManageScreenVM.Ascii(_vm.BuilderUpsellButtonText ?? "");
                    ElarionUiKit.FitSingleLine(label);
                }
                FlowTrace.Step("Manage", "builder upsell shown=" + _vm.BuilderUpsellVisible +
                    " price='" + (_vm.BuilderUpsellButtonText ?? "") + "'");
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

            // WO-1393: the slot offer is built once and rides the list as its LAST row - park it
            // back on the drawer before the clear below so its label/button fields survive.
            if (_drawerSlotOffer != null && _queueDrawer != null)
            {
                _drawerSlotOffer.SetParent(_queueDrawer.transform, false);
                _drawerSlotOffer.gameObject.SetActive(false);
            }
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
                // the rows that carry every action. WO-1393: NOT in band mode - the Troops
                // workspace rail and the strip's counts are already on screen, and a 200px rail
                // in a ~235px band is exactly what clipped the header under it.
                if (!_drawerBandMode)
                    MountRail(MakeRowHost("Drawer_QueueRail", _railBandPx), forceRebuild: true);

                AddSectionHeader("IN QUEUE - " + BuildTimerService.ChannelWord(channel).ToUpperInvariant());
                if (_vm.QueueRows.Count == 0)
                    AddNoteRow("Nothing is queued on this line. Start an upgrade to see it here.");
                else
                    for (int i = 0; i < _vm.QueueRows.Count; i++) AddQueueRow(_vm.QueueRows[i]);

                // WO-1393: the Buy-Builder offer is the list's LAST row in both modes (it scrolls;
                // it no longer owns a fixed zone that starved the rows of height).
                if (_drawerSlotOffer != null)
                {
                    var offerRow = MakeRowHost("Drawer_SlotOfferRow", SlotBandPx);
                    _drawerSlotOffer.SetParent(offerRow, false);
                    _drawerSlotOffer.anchorMin = new Vector2(0.035f, 0f);
                    _drawerSlotOffer.anchorMax = new Vector2(0.965f, 1f);
                    _drawerSlotOffer.offsetMin = _drawerSlotOffer.offsetMax = Vector2.zero;
                    _drawerSlotOffer.gameObject.SetActive(true);
                }

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

        /// <summary>
        /// ⛔ WO-2001 - THE BODY IS NOW THE THREE-TAB WORKSPACE. Everything below the delegation is
        /// the LEGACY rail + selected-card path (WO-1418 / WO-1422). It is retained deliberately and
        /// NOT deleted here for two stated reasons: (1) eight suites read these method bodies as
        /// SOURCE TEXT and a silent deletion would take them red inside a lane that cannot run the
        /// gate, and (2) the per-destination cards are the proven detail surfaces the redesign's
        /// DETAIL screens are modelled on. ⚠ It is nevertheless DEAD CODE UNDER GREEN PINS - the
        /// exact shape ManageQueueDrawerRegression:103-113 exists to catch - so its removal, and the
        /// pin moves that must precede it, are itemised in this work order's hand-back. Do not leave
        /// it here indefinitely.
        /// </summary>
        private void RenderList()
        {
            if (WorkspaceActive) { RenderWorkspace(); return; }
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

            if (_vm.Tab == ManageTab.Buildings)
            {
                RenderBuildingsDestination(channel);
                return;
            }

            if (_vm.Tab == ManageTab.Troops)
            {
                RenderTroopsDestination(channel);
                MakeRowHost("ListTailSpacer", ListTailPx);
                return;
            }

            // WO-1422 (owner ruling 2026-09-06): Defence and Research take the SAME rail + card +
            // NOW band + footer shape as Buildings. The paged text list they used to share is
            // retired with its pager and its row painter - see ruling 3.4.
            if (_vm.Tab == ManageTab.Defense)
            {
                RenderDefenseDestination(channel);
                return;
            }

            if (_vm.Tab == ManageTab.Research)
            {
                RenderResearchDestination(channel);
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
            // ⛔ WO-1422 - THE PAGED BROWSE PATH IS RETIRED AND IS NOT COMING BACK.
            // Every one of the four Manage destinations now branches above into its own rail +
            // card workspace, so the page-count sentence, its two page doors, AddBrowseRow and
            // BuildBrowseRowContent had no reachable call site left. They are DELETED rather than
            // parked: a private method with zero callers is dead code that LOOKS like a shipped
            // feature - the exact failure ManageQueueDrawerRegression's [rows-have-a-home] case was
            // written to catch for the drawer's own row builder. The VM's BrowseRows STAYS (three
            // suites drive it, and the Troops "Saved army compositions" row still reads it); only
            // the PANEL stopped painting it.
            // ⚠ Nothing in THIS method may name the drawer's row builder, not even in a comment:
            // [rows-not-inline] bans that token anywhere inside RenderList's body.
            if (_vm.BrowseRows.Count == 0)
                AddNoteRow(BrowseEmptyState(_vm.Tab));

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

        // =====================================================================
        //  WO-1422 - THE DEFENCE AND RESEARCH DESTINATIONS
        // ---------------------------------------------------------------------
        // ⛔ PLACEMENT IS LOAD-BEARING. Both live AFTER FindSummary and BEFORE
        // RenderBuildingsDestination. ManageQueueDrawerRegression.cs:90 and
        // ManageBuildingsCardRegression.cs:158 both scope their bans to
        // Body(panel, "private void RenderList()", "private string FindSummary"); anything defined
        // inside that window enters the ban. ManageBuildingsCardRegression.cs:141 scopes the
        // Buildings destination to Body("RenderBuildingsDestination(", "AddBuildingWorkspaceRow("),
        // so nothing may be inserted between those two either. This gap is the one seat that is
        // outside every window. Do not move these methods.
        // =====================================================================

        /// <summary>
        /// WO-1422 ruling 3.1/3.3: the Defence destination is the Buildings shape - one rail row
        /// per placed upgradable TYPE (never per instance: wall_wood alone would make the rail
        /// unbounded), one selected card, and the SHARED Builder band, named BUILDING NOW.
        /// Defence and Buildings ride ONE queue; a second name for it would be duplicated state.
        /// </summary>
        private void RenderDefenseDestination(ChannelId channel)
        {
            if (_vm == null) return;
            if (_vm.DefenseChoices.Count == 0)
            {
                // The no-fixture Defence capture (RunManageDefenseCaptureHeadless) renders exactly
                // this path, and two suites pin the "Build defense" door off it.
                AddNoteRow(BrowseEmptyState(ManageTab.Defense));
                AddActionNoteRow("Need another tower?", "Build defense", OpenDefenseBuilder);
                MakeRowHost("ListTailSpacer", ListTailPx);
                FlowTrace.Warn("Manage", "defense destination has no upgradable placed types - " +
                    "empty state + the Build defense door are the whole screen");
                return;
            }

            DefenseChoiceVM selected = null;
            for (int i = 0; i < _vm.DefenseChoices.Count; i++)
                if (string.Equals(_vm.DefenseChoices[i].Id, _selectedDefenseId, StringComparison.OrdinalIgnoreCase))
                { selected = _vm.DefenseChoices[i]; break; }
            if (selected == null)
            {
                // DefenseChoiceVM carries no Locked flag (lane A contract); "has something to do"
                // is Activate != null, which is null only at Max.
                for (int i = 0; i < _vm.DefenseChoices.Count; i++)
                    if (_vm.DefenseChoices[i] != null && _vm.DefenseChoices[i].Activate != null)
                    { selected = _vm.DefenseChoices[i]; break; }
                if (selected == null) selected = _vm.DefenseChoices[0];
                _selectedDefenseId = selected.Id;
            }

            AddDefenseWorkspaceRow(selected);
            AddBuildingNowBand();   // ruling 3.3 - the ONE Builder rail, verbatim
            AddActionNoteRow("Need another tower?", "Build defense", OpenDefenseBuilder);
            if (!string.IsNullOrEmpty(_vm.RepairOfferText))
                AddActionNoteRow(_vm.RepairOfferText, "Repair", () => { _vm.RepairAll(); FlushNotice(); });
            MakeRowHost("ListTailSpacer", ListTailPx);
            FlowTrace.Step("Manage", "defense destination: rail=" + _vm.DefenseChoices.Count +
                " selected=" + selected.Id + " placed=" + selected.PlacedCount +
                " level=" + selected.Level + " jobs=" + _vm.QueueRows.Count +
                " (channel=" + channel + ", shared with Buildings)");
        }

        /// <summary>
        /// WO-1422 ruling 3.6/3.7: the Research destination is the Buildings shape - one rail row
        /// per authored PERK (never per building: a per-building card would need three or four
        /// verbs in one CTA band and no card grammar here supports that), one selected card
        /// showing the whole tree including Researched and Researching, and its own RESEARCHING NOW
        /// band. Research has no LEVEL, so the card's level slot carries the tier requirement.
        /// </summary>
        private void RenderResearchDestination(ChannelId channel)
        {
            if (_vm == null) return;
            if (_vm.ResearchChoices.Count == 0)
            {
                AddNoteRow(BrowseEmptyState(ManageTab.Research));
                MakeRowHost("ListTailSpacer", ListTailPx);
                FlowTrace.Warn("Manage", "research destination has no perk choices - no owned " +
                    "building authors a tier ladder in this town");
                return;
            }

            ResearchChoiceVM selected = null;
            for (int i = 0; i < _vm.ResearchChoices.Count; i++)
                if (string.Equals(ResearchKeyOf(_vm.ResearchChoices[i]), _selectedResearchKey, StringComparison.OrdinalIgnoreCase))
                { selected = _vm.ResearchChoices[i]; break; }
            if (selected == null)
            {
                for (int i = 0; i < _vm.ResearchChoices.Count; i++)
                    if (_vm.ResearchChoices[i] != null && !_vm.ResearchChoices[i].Locked)
                    { selected = _vm.ResearchChoices[i]; break; }
                if (selected == null) selected = _vm.ResearchChoices[0];
                _selectedResearchKey = ResearchKeyOf(selected);
            }

            AddResearchWorkspaceRow(selected);
            AddResearchNowBand();
            MakeRowHost("ListTailSpacer", ListTailPx);
            FlowTrace.Step("Manage", "research destination: rail=" + _vm.ResearchChoices.Count +
                " selected=" + ResearchKeyOf(selected) + " state=" + (selected.StateWord ?? "<null>") +
                " jobs=" + _vm.QueueRows.Count + " (channel=" + channel + ")");
        }

        /// <summary>
        /// The Research selection key. ⛔ SHAPE IS A CROSS-LANE CONTRACT: "&lt;buildingId&gt;:&lt;perkId&gt;",
        /// which is BuildingPerkService.Key's shape (BuildingPerkService.cs:68) and the shape the
        /// capture fixture writes into _selectedResearchKey. Compose it in exactly one place.
        /// </summary>
        private static string ResearchKeyOf(ResearchChoiceVM choice) =>
            choice == null ? null : (choice.BuildingId ?? "") + ":" + (choice.PerkId ?? "");

        private void RenderBuildingsDestination(ChannelId channel)
        {
            if (_vm == null) return;
            if (_vm.BuildingChoices.Count == 0)
            {
                AddNoteRow("No placed buildings are available.");
                AddActionNoteRow("Need another town structure?", "Open build", OpenTownBuilder);
                MakeRowHost("ListTailSpacer", ListTailPx);
                FlowTrace.Warn("Manage", "buildings destination has no placed building choices");
                return;
            }

            BuildingChoiceVM selected = null;
            for (int i = 0; i < _vm.BuildingChoices.Count; i++)
                if (string.Equals(_vm.BuildingChoices[i].Id, _selectedBuildingId, StringComparison.OrdinalIgnoreCase))
                { selected = _vm.BuildingChoices[i]; break; }
            if (selected == null)
            {
                for (int i = 0; i < _vm.BuildingChoices.Count; i++)
                    if (!_vm.BuildingChoices[i].Locked) { selected = _vm.BuildingChoices[i]; break; }
                if (selected == null) selected = _vm.BuildingChoices[0];
                _selectedBuildingId = selected.Id;
            }

            AddBuildingWorkspaceRow(selected);
            AddBuildingNowBand();
            AddActionNoteRow("Need another town structure?", "Open build", OpenTownBuilder);
            MakeRowHost("ListTailSpacer", ListTailPx);
            FlowTrace.Step("Manage", "buildings destination: rail=" + _vm.BuildingChoices.Count +
                " selected=" + selected.Id + " jobs=" + _vm.QueueRows.Count);
        }

        private void AddBuildingWorkspaceRow(BuildingChoiceVM selected)
        {
            var workspace = MakeRowHost("BuildingSplitWorkspace", TroopWorkspacePx);
            var railZone = MakeZone(workspace, "BuildingSelectorRail", new Vector2(0f, 0f), new Vector2(0.26f, 1f));
            var railPlate = ElarionUiKit.AddImage(railZone, "RailPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            railPlate.GetComponent<Image>().raycastTarget = false;
            var railScroll = ElarionUiKit.MakeScrollZone(railZone, spacing: 6f, padding: 8);
            if (railScroll == null || railScroll.content == null)
                FlowTrace.Fail("Manage", "building rail MakeScrollZone returned no content - the rail has no build site.");
            else
            {
                int selectedIndex = 0;
                _rowParent = railScroll.content;
                try
                {
                    for (int i = 0; i < _vm.BuildingChoices.Count; i++)
                    {
                        var choice = _vm.BuildingChoices[i];
                        if (choice == null) continue;
                        bool isSelected = string.Equals(choice.Id, selected.Id, StringComparison.OrdinalIgnoreCase);
                        if (isSelected) selectedIndex = i;
                        Guard.Try("Manage", "building rail row " + choice.Id, () => BuildBuildingRailRow(choice, isSelected));
                    }
                    // The tail gives every selected row enough scroll range to align its TOP edge
                    // with the viewport. Without it the last row stops mid-pitch and the row above
                    // is left half-visible at the top of this short, two-row rail.
                    MakeRowHost("BuildingRailTailSpacer", TroopWorkspacePx - TroopRailRowPx);
                }
                finally { _rowParent = null; }

                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(railScroll.content);
                if (railScroll.scroll != null)
                {
                    var viewport = railScroll.scroll.viewport;
                    float viewportPx = viewport != null ? viewport.rect.height : TroopWorkspacePx;
                    float maxScrollPx = Mathf.Max(0f, railScroll.content.rect.height - viewportPx);
                    float selectedTopPx = Mathf.Min(maxScrollPx, selectedIndex * (TroopRailRowPx + 6f));
                    railScroll.scroll.StopMovement();
                    railScroll.scroll.verticalNormalizedPosition = maxScrollPx > 0.5f
                        ? 1f - selectedTopPx / maxScrollPx
                        : 1f;
                    FlowTrace.Step("Manage", "building rail aligned row=" + selectedIndex +
                        " topPx=" + selectedTopPx.ToString("0") + " maxPx=" + maxScrollPx.ToString("0"));
                }
            }

            var card = MakeZone(workspace, "BuildingSelectedCard", new Vector2(0.275f, 0f), new Vector2(1f, 1f));
            BuildBuildingCard(card, selected);
        }

        private void BuildBuildingRailRow(BuildingChoiceVM choice, bool isSelected)
        {
            var row = MakeRowHost("BuildingChoiceRow_" + choice.Id, TroopRailRowPx);
            var faceGo = ElarionUiKit.AddImage(row, "BuildingChoice_" + choice.Id, Vector2.zero, Vector2.one,
                isSelected ? new Color(0.24f, 0.18f, 0.08f, 0.90f) : new Color(0f, 0f, 0f, 0.28f), rounded: false);
            var face = faceGo.GetComponent<Image>();
            face.raycastTarget = true;
            var button = faceGo.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() =>
            {
                _selectedBuildingId = choice.Id;
                FlowTrace.Step("Manage", "building rail selected=" + choice.Id);
                Render();
            });
            if (isSelected)
            {
                var outline = faceGo.AddComponent<Outline>();
                outline.effectColor = ElarionUi.Gold;
                outline.effectDistance = new Vector2(4f, -4f);
                outline.useGraphicAlpha = false;
            }

            var medallion = MakeZone(faceGo.transform, "Medallion", new Vector2(0.03f, 0.08f), new Vector2(0.27f, 0.92f));
            var portrait = ElarionUiKit.Portrait(medallion, BuildingSprite(choice), active: isSelected);
            if (choice.Locked && portrait?.image != null) portrait.image.color = new Color(0.42f, 0.42f, 0.42f, 1f);
            if (choice.Locked) BuildLockBadge(medallion);

            var name = ElarionUiKit.Label(faceGo.transform, ManageScreenVM.Ascii(choice.Name ?? ""), 0.52f, 0.96f,
                choice.Locked ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.30f, 0.84f, bold: true);
            ElarionUiKit.FitSingleLine(name, 26f, 38f);
            string railState = choice.Locked
                ? ManageScreenVM.Ascii(choice.LockText ?? "Locked")
                : "Level " + choice.Level +
                  (string.Equals(choice.StateWord, "Max", StringComparison.Ordinal) ? " . Max" :
                   string.Equals(choice.StateWord, "Building", StringComparison.Ordinal) ? " . Building" : "");
            var sub = ElarionUiKit.Label(faceGo.transform, railState,
                0.06f, 0.48f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.30f, 0.84f);
            ElarionUiKit.FitSingleLine(sub, 22f, 30f);
            var chevron = ElarionUiKit.Label(faceGo.transform, ">", 0.10f, 0.90f,
                isSelected ? ElarionUi.Gold : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontBody, TextAlignmentOptions.Center, 0.84f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(chevron, 30f, 50f);
            ElarionUiKit.ClampMinTouch(button);
        }

        private static Sprite BuildingSprite(BuildingChoiceVM choice)
        {
            if (choice == null) return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "hammer");

            // Dedicated namespace: unlike legacy Portraits/<id>, every file here is guaranteed
            // to depict the STRUCTURE. Creative can drop a base sheet or optional tier sheet
            // without replacing an NPC portrait or requiring another code edit.
            Sprite art = LoadManageBuildingSprite(choice.Id, choice.Level);
            var entry = string.IsNullOrEmpty(choice.CatalogEntryId)
                ? null
                : DeNelle.Core.Catalog.CatalogRegistry.Get(choice.CatalogEntryId);

            // Keep the shared Build palette as the normal fallback, except for the six measured
            // ids whose current palette route resolves a person rather than a building.
            if (art == null && entry != null && !ManageBuildingPortraitGaps.Contains(choice.Id))
                art = DeNelle.Village.BuildPaletteUI.ResolveEntryArtPublic(entry);
            if (art == null && entry != null)
                art = DeNelle.Core.UI.ConceptIconResolver.ResolveAny(entry.id, entry.type.ToString());
            if (art == null)
                FlowTrace.Warn("Manage", "building art unresolved id=" + (choice.Id ?? "<null>") +
                    " catalogEntryId=" + (choice.CatalogEntryId ?? "<null>") +
                    " - add Portraits/Buildings/<id>; neutral hammer used");
            return art ?? RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "hammer");
        }

        private BuildingChoiceVM FindBuildingChoice(string buildingId)
        {
            if (_vm == null || string.IsNullOrEmpty(buildingId)) return null;
            for (int i = 0; i < _vm.BuildingChoices.Count; i++)
            {
                var choice = _vm.BuildingChoices[i];
                if (choice != null && string.Equals(choice.Id, buildingId, StringComparison.OrdinalIgnoreCase))
                    return choice;
            }
            return null;
        }

        private static Sprite LoadManageBuildingSprite(string buildingId, int level)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            string root = "Portraits/Buildings/" + buildingId;
            string tierKey = level > 1 ? root + "-" + level : null;
            Sprite art = LoadManageBuildingSpriteAt(tierKey);
            return art ?? LoadManageBuildingSpriteAt(root);
        }

        /// <summary>
        /// WO-2017 - INTERNAL, not private: <see cref="HeartPanel"/> loads the Heart's own portrait
        /// through this exact path so the Heart cannot become the one art route with its own loader,
        /// its own Texture2D fallback and its own cache-miss behaviour.
        ///
        /// <para>⛔ WO-2001 - THIS IS NOW A ONE-LINE FORWARDER, NOT AN IMPLEMENTATION. The body it
        /// used to hold was duplicated verbatim in <see cref="DeNelle.Core.Manage.ManageArt.LoadSprite"/>
        /// (see that file's header: it could not call this one, because `internal` does not cross
        /// the DeNelle.Core / DeNelle.Village assembly line). Two copies of one behaviour is the
        /// failure CLAUDE.md 2 / 5 / 16 records three times over, so the Core copy WINS - it is
        /// reachable from both assemblies, it caches MISSES as well as hits, and it announces a
        /// miss once per key through FlowTrace instead of silently returning null.</para>
        ///
        /// <para>Kept as a method rather than deleted so the Village callers (and
        /// <see cref="HeartPanel"/>) keep one name for the route; the sprite name suffix
        /// "_manage_building" moved to ManageArt's "_manage" with it.</para>
        /// </summary>
        internal static Sprite LoadManageBuildingSpriteAt(string resourceKey)
            => DeNelle.Core.Manage.ManageArt.LoadSprite(resourceKey);

        private void BuildBuildingCard(RectTransform card, BuildingChoiceVM selected)
        {
            var plate = ElarionUiKit.AddImage(card, "CardPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            plate.GetComponent<Image>().raycastTarget = false;
            ElarionUiKit.GoldPerimeter(card);

            var medallion = MakeZone(card, "BuildingPortrait", new Vector2(0.02f, 0.59f), new Vector2(0.16f, 0.99f));
            var portrait = ElarionUiKit.Portrait(medallion, BuildingSprite(selected), active: true);
            if (selected.Locked && portrait?.image != null) portrait.image.color = new Color(0.42f, 0.42f, 0.42f, 1f);
            if (selected.Locked) BuildLockBadge(medallion);

            var name = ElarionUiKit.Label(card, ManageScreenVM.Ascii((selected.Name ?? "").ToUpperInvariant()),
                0.835f, 1f, ElarionUi.Gold, (int)ElarionUi.FontTitle,
                TextAlignmentOptions.Left, 0.19f, 0.74f, bold: true);
            ElarionUiKit.FitSingleLine(name, 30f, 48f);
            var level = ElarionUiKit.Label(card, "LEVEL " + selected.Level, 0.835f, 1f, ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.75f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(level, 26f, 36f);

            var desc = ElarionUiKit.Label(card, ManageScreenVM.Ascii(selected.Description ?? ""), 0.70f, 0.83f,
                ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.19f, 0.72f);
            ElarionUiKit.FitSingleLine(desc, ElarionUiKit.FontHardFloor, 30f);
            var badge = ElarionUiKit.AddImage(card, "BuildingStateBadge", new Vector2(0.74f, 0.70f),
                new Vector2(0.98f, 0.83f), new Color(0.12f, 0.25f, 0.08f, 0.82f), rounded: false);
            badge.GetComponent<Image>().raycastTarget = false;
            var state = ElarionUiKit.Label(badge.transform, ManageScreenVM.Ascii(selected.StateWord ?? ""), 0f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(state, 20f, 28f);

            if (string.Equals(selected.StateWord, "Max", StringComparison.Ordinal)) return;

            // WO-1423 - a LOCKED card lifts its cost/fact row to 0.565-0.70 (35.1px at
            // TroopWorkspacePx = 260) to free 0.45-0.56 (28.6px) for the lock SENTENCE, which is the
            // exact band arithmetic BuildResearchCard already proved on device. Both clear the ~24px
            // floor below which TMP culls a whole line.
            float factY0 = selected.Locked ? 0.565f : 0.54f;
            float factY1 = selected.Locked ? 0.70f : 0.695f;
            ElarionUiKit.CostRow(card, selected.UpgradeCostParts, new Vector2(0.02f, factY0),
                new Vector2(0.72f, factY1), ElarionUi.Parchment, prefix: "Upgrade:",
                fontPx: (int)ElarionUi.FontMicro);
            string readiness = selected.UpgradeReady ? "Ready" : "Short";
            string factText = string.IsNullOrEmpty(selected.UpgradeTimeText)
                ? readiness : selected.UpgradeTimeText + " . " + readiness;
            var fact = ElarionUiKit.Label(card, factText, factY0, factY1, ElarionUi.Parchment,
                (int)ElarionUi.FontMicro, TextAlignmentOptions.Right, 0.73f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(fact, 20f, 28f);

            // The "After upgrade" band (0.445-0.535) is the band the lock sentence takes over, so a
            // locked card paints one or the other, never both stacked on the same pixels.
            if (!selected.Locked)
            {
                var benefit = ElarionUiKit.Label(card,
                    string.IsNullOrEmpty(selected.AfterUpgradeText) ? "" : "After upgrade: " + ManageScreenVM.Ascii(selected.AfterUpgradeText),
                    0.445f, 0.535f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                    TextAlignmentOptions.Left, 0.02f, 0.98f);
                ElarionUiKit.FitSingleLine(benefit, ElarionUiKit.FontHardFloor, 26f);
            }

            // Locked and in-progress choices still explain what the next tier costs and buys.
            // Only their CTA face changes; Max is the sole state without a next-tier fact row.
            if (selected.Locked)
            {
                // ⛔ WO-1423 - THE DEAD END THE OWNER HIT ("some items are locked till village level 1,
                // which there is no way to trigger"). This branch used to paint the DISABLED-face
                // helper (the same one the "Building" state still uses below) wearing
                // "UNLOCKS AT VILLAGE LEVEL"+n, and RETURN before the door below was ever built - so
                // the ONE card that names the gate was the ONE card with no route to the control that
                // opens it. (The helper's NAME is deliberately not written in this branch: the
                // WO-1423 oracle fails the branch if that call ever comes back.) A named lock with
                // no door is worse than no lock at all: it teaches the player the game is stuck.
                //
                // Same treatment the locked RESEARCH card got (BuildResearchCard, this file): the
                // requirement is a BODY TEXT LINE - prose belongs in the body, a sentence never fits a
                // button face - and the card carries exactly ONE FULL-WIDTH LIVE door via
                // selected.ViewDetails. The door is full width REGARDLESS of DoorLabel: a ladder that
                // authors no perks (the Farm) can still be Heart-gated, and this door is the GATE
                // door, not the PERKS door.
                //
                // ⚠ CORRECTED WO-2003 (2026-09-06): this said the door led to "OpenUpgradePanel, whose
                // action band renders 'Raise Village Tier N'". Both halves have moved. ViewDetails is
                // authored BY STATE in ManageScreenVM.BuildBuildingChoices - a LOCKED card's
                // ViewDetails now opens PanelId.Heart (the Heart surface itself), an unlocked card's
                // still opens the building's upgrade page - and that band's face is now
                // "Raise Heart Level to N" (BuildingUpgradePanelMvvm.cs:425). The View is unchanged
                // and still just invokes selected.ViewDetails; only the destination the VM authors
                // moved, which is the point of the dumb-UI rule.
                var lockLine = ElarionUiKit.Label(card,
                    ManageScreenVM.Ascii(string.IsNullOrEmpty(selected.LockReason) ? "Locked." : selected.LockReason),
                    0.45f, 0.56f, ElarionUi.Parchment, (int)ElarionUi.FontMicro,
                    TextAlignmentOptions.Left, 0.02f, 0.98f, bold: true);
                if (lockLine != null)
                {
                    lockLine.gameObject.name = "BuildingLockReason";
                    ElarionUiKit.FitSingleLine(lockLine, ElarionUiKit.FontHardFloor, 28f);
                }
                var lockedDoor = ElarionUiKit.BuildObsidianButton(card,
                    ManageScreenVM.Ascii(string.IsNullOrEmpty(selected.LockCtaLabel) ? "OPEN" : selected.LockCtaLabel),
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.02f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1),
                    () => Guard.Try("Manage", "open building village gate", () => selected.ViewDetails?.Invoke()));
                if (lockedDoor != null)
                {
                    lockedDoor.gameObject.name = "BuildingCta_Locked";
                    lockedDoor.interactable = selected.ViewDetails != null;
                    MedievalUiSkin.ApplyButton(lockedDoor, false);
                }
                ElarionUiKit.ClampMinTouch(lockedDoor);
                return;
            }
            if (string.Equals(selected.StateWord, "Building", StringComparison.Ordinal))
            {
                BuildDisabledBuildingFace(card, "BuildingCta_Building", "BUILDING");
                return;
            }

            // WO-1422 ruling 3.5 (owner: "Keep one door, but name what's behind it"): the second
            // door is no longer the developer word VIEW DETAILS. It carries the VM's DoorLabel -
            // "PERKS" on a ladder that authors perks - and is HIDDEN when DoorLabel is null (the
            // Farm authors zero perks, so the Farm card is ONE full-width CTA; that is the
            // feature). ⛔ The GameObject NAME stays BuildingCta_Details - ManageBuildingsCardRegression
            // pins it, and renaming it breaks the pin for no player-visible gain.
            bool hasDoor = !string.IsNullOrEmpty(selected.DoorLabel);
            var upgrade = ElarionUiKit.BuildObsidianButton(card, "UPGRADE TO L" + selected.NextTier,
                ElarionUiKit.ObsidianButtonStyle.Style1,
                selected.UpgradeReady ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.02f, TroopCtaY0), new Vector2(hasDoor ? 0.48f : 0.98f, TroopCtaY1),
                () => Guard.Try("Manage", "upgrade building", () => selected.Activate?.Invoke()));
            if (upgrade != null)
            {
                upgrade.gameObject.name = "BuildingCta_Upgrade";
                upgrade.interactable = selected.UpgradeReady;
                MedievalUiSkin.ApplyButton(upgrade, true);
            }
            ElarionUiKit.ClampMinTouch(upgrade);

            if (!hasDoor)
            {
                FlowTrace.Step("Manage", "building card " + selected.Id +
                    " has no second door (DoorLabel null) - UPGRADE is full width");
                return;
            }

            var details = ElarionUiKit.BuildObsidianButton(card,
                ManageScreenVM.Ascii(selected.DoorLabel.ToUpperInvariant()),
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.52f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1),
                () => Guard.Try("Manage", "open building door " + selected.DoorLabel, () => selected.ViewDetails?.Invoke()));
            if (details != null) details.gameObject.name = "BuildingCta_Details";
            ElarionUiKit.ClampMinTouch(details);
        }

        private static void BuildDisabledBuildingFace(RectTransform card, string objectName, string text)
        {
            var face = ElarionUiKit.BuildObsidianButton(card, text,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.02f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1), null);
            if (face == null) return;
            face.gameObject.name = objectName;
            face.interactable = false;
            MedievalUiSkin.ApplyButton(face, false);
            ElarionUiKit.ClampMinTouch(face);
        }

        private void AddBuildingNowBand()
        {
            var band = MakeRowHost("BuildingNowBand", TrainingNowBandPx);
            var bandPlate = ElarionUiKit.AddImage(band, "BandPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            bandPlate.GetComponent<Image>().raycastTarget = false;
            int hiddenJobs = Mathf.Max(0, _vm.QueueRows.Count - 1);
            var title = ElarionUiKit.Label(band, "BUILDING NOW", hiddenJobs > 0 ? 0.53f : 0.15f, 0.85f, ElarionUi.Gold,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.01f, 0.165f, bold: true);
            ElarionUiKit.FitSingleLine(title, 22f, 32f);
            if (hiddenJobs > 0)
            {
                var more = ElarionUiKit.Label(band, "+" + hiddenJobs + " more", 0.12f, 0.48f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                    TextAlignmentOptions.Left, 0.01f, 0.165f, bold: true);
                ElarionUiKit.FitSingleLine(more, ElarionUiKit.FontHardFloor, 28f);
            }
            var open = ElarionUiKit.BuildObsidianButton(band, "OPEN QUEUE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(PrimaryX0, BandCtrlY0), new Vector2(PrimaryX1, BandCtrlY1), ToggleQueueDrawer);
            if (open != null) open.gameObject.name = "BuildingOpenQueue";
            ElarionUiKit.ClampMinTouch(open);

            if (_vm.QueueRows.Count == 0)
            {
                var empty = ElarionUiKit.Label(band, "No builder at work", 0.15f, 0.85f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.Left,
                    0.18f, ClusterX1 + 0.01f);
                ElarionUiKit.FitSingleLine(empty, 24f, 34f);
                return;
            }

            var first = _vm.QueueRows[0];
            if (first != null)
            {
                // WO-1422 POLISH (MEASURED 2026-09-06, ManageDefense_2670x1200.png): this ONE band
                // serves Buildings AND Defence (ruling 3.3), and on Defence it read "Tower Ground
                // Archer..." beside an art-less medallion. A placed-structure upgrade is not a
                // BuildingTierCatalog row, so QueueRowVM.BuildingId is empty and FindBuildingChoice
                // can never match it. The Defence tab therefore resolves the job against
                // DefenseChoices - exactly the way ResearchJobSprite matches ResearchChoices, which
                // is the band that already reads "Warding Runes" with its real perk icon.
                // ⛔ The Buildings expression below stays VERBATIM (ManageBuildingsCardRegression
                // pins `BuildingSprite(FindBuildingChoice(first.BuildingId))` as a literal).
                DefenseChoiceVM defenseJob = _vm.Tab == ManageTab.Defense ? FindDefenseChoiceForJob(first) : null;
                Sprite jobArt = defenseJob != null
                    ? DefenseSprite(defenseJob)
                    : BuildingSprite(FindBuildingChoice(first.BuildingId));
                string jobLabel = defenseJob != null ? ManageScreenVM.Ascii(defenseJob.Name ?? "") : null;
                // §12 — the next capture PROVES which arm fired instead of leaving us to infer it
                // from pixels. A resolve used to be silent, so "resolved and painted" was
                // indistinguishable from "resolved but Name was blank and the raw key came back".
                //   tab=Defense resolved=<id> art=yes label='Archer Tower'  -> fixed.
                //   tab=Defense resolved=<none> art=no label=<fallback:...> -> still broken, and
                //     FindDefenseChoiceForJob's Warn above names the id it could not place.
                //   tab=Defense resolved=<id> label=<fallback:...>          -> resolved but the
                //     choice's Name was blank; the resolver is fine, the projection is not.
                //   tab=Buildings ...                                       -> the Defence branch
                //     was NOT reached, i.e. the tab check is the suspect, not the resolver.
                // Printed UNCONDITIONALLY so that last case is observable rather than merely
                // described - a tab-gated trace can never prove the tab gate.
                FlowTrace.Step("Manage", "BUILDING NOW band: tab=" + _vm.Tab + " jobId='" +
                        (first.JobId ?? "<null>") + "' buildingId='" + (first.BuildingId ?? "") +
                        "' resolved=" + (defenseJob != null ? defenseJob.Id : "<none>") +
                        " art=" + (jobArt != null ? "yes" : "no") +
                        " label=" + (!string.IsNullOrEmpty(jobLabel)
                            ? "'" + jobLabel + "'"
                            : "<fallback:" + ManageScreenVM.Ascii(first.Label ?? "") + ">"));
                Guard.Try("Manage", "building now job 1", () => BuildTroopTrainingNowJob(band, 1, first,
                    0.175f, 0.205f, 0.21f, 0.27f, 0.28f, 0.45f, 0.46f, 0.60f, 0.61f, ClusterX1 + 0.01f,
                    jobArt, jobLabel));
            }
            if (hiddenJobs > 0)
                FlowTrace.Step("Manage", "building now capped inside band: painted=1 hidden=" + hiddenJobs);
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
            button.onClick.AddListener(() =>
            {
                _selectedTroopId = choice.Id;
                // WO-1389: the REAL rail tap is a route hop of the post-raid beat ("Pick a troop" ->
                // the UPGRADE face). Raised BEFORE Render so the spotlight's next target (the CTA
                // face, registered by BuildTroopCard) is rebuilt on the same frame it is asked for.
                Guard.Try("Manage", "raise troop-selected signal", () =>
                    DeNelle.Core.Tutorial.TutorialSignals.Raise(
                        DeNelle.Core.Tutorial.TutorialSignals.ManageTroopSelectedPrefix + choice.Id));
                Render();
            });
            // WO-1389: spotlightable by id ("manage.troop_row.<troopId>"; the footman row is the
            // KnownIds contract). Idempotent; every Render re-registers the fresh rect.
            DeNelle.Core.UI.TutorialHighlightRegistry.Register("manage.troop_row." + choice.Id, (RectTransform)faceGo.transform);
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
            //   name + LEVEL   0.845-1.000 -> 40px   (a 30px title line needs ~35px)
            //   army + badge   0.740-0.840 -> 26px   (WO-1422 polish; 18px CULLED both labels)
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
                0.845f, 1.0f, ElarionUi.Gold, (int)ElarionUi.FontTitle,
                TextAlignmentOptions.Left, 0.19f, 0.74f, bold: true);
            ElarionUiKit.FitSingleLine(name, 30f, 48f);
            var level = ElarionUiKit.Label(card, "LEVEL " + selected.Level, 0.845f, 1.0f, ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.75f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(level, 26f, 36f);
            // WO-1422 ruling 3.10.1: the army summary gives up its right edge (x1 0.98 -> 0.72) so
            // the state-word badge has a seat. ⚠ DELIBERATE DEVIATION from "the same zone Buildings
            // uses" (0.74,0.70)-(0.98,0.83): on the Troops card that rect overprints BOTH the army
            // line (y 0.745-0.815) and the top of the status label (y 0.585-0.735, x 0.71-0.98),
            // because the Buildings card has no army line. The badge takes the army band's right
            // half instead - same x, y clipped to the army band - which overlaps nothing.
            //
            // ⚠ WO-1422 POLISH (MEASURED 2026-09-06, ManageTroops_1920x1080.png): the badge PLATE
            // painted and its WORD did not, and the army summary beside it was missing too. Neither
            // was a text bug - the band was 0.745-0.815 = 0.07 x TroopWorkspacePx(260) = 18.2px, and
            // TMP's Ellipsis overflow CULLS THE WHOLE LINE when the line at fontSizeMin cannot seat
            // in the rect (ElarionUiKitObsidian.cs:3110-3116 states this as the proven cause of the
            // "bare plate" class). FitSingleLine's floor is FontHardFloor=20, whose line is ~23-24px,
            // so nothing in an 18px band can ever render. The band is now 0.74-0.84 = 26px and the
            // NAME band gives up the 0.025 it can spare (it keeps 40.3px, well over the ~35px a
            // 30px title line needs). ⛔ Never re-shrink a text band below ~24px on this card.
            var army = ElarionUiKit.Label(card, ManageScreenVM.Ascii(_vm.TroopArmySummaryText ?? ""),
                0.74f, 0.84f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.19f, 0.72f);
            ElarionUiKit.FitSingleLine(army, 18f, 26f);
            if (!string.IsNullOrEmpty(selected.StateWord))
            {
                var troopBadge = ElarionUiKit.AddImage(card, "TroopStateBadge", new Vector2(0.74f, 0.74f),
                    new Vector2(0.98f, 0.84f), new Color(0.12f, 0.25f, 0.08f, 0.82f), rounded: false);
                troopBadge.GetComponent<Image>().raycastTarget = false;
                var troopState = ElarionUiKit.Label(troopBadge.transform, ManageScreenVM.Ascii(selected.StateWord), 0f, 1f,
                    ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
                ElarionUiKit.FitSingleLine(troopState, 18f, 26f);
            }

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
                ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.02f, 0.49f, bold: true);
            ElarionUiKit.FitSingleLine(trainFact, 22f, 26f);

            // WO-1422 ruling 3.10.2 - THE MEASURED TRUNCATION FIX. On the device the destination
            // sentence rode the UPGRADE face's sub-line as the THIRD clause and was cut mid-word:
            // "4m 30s . Ready . L3 unlocks Sweepi...". It now has its own row, at Buildings'
            // wording ("After upgrade: <benefit>"), and the sub-line keeps only cost + state.
            // ⚠ DELIBERATE DEVIATION from the WO's literal y 0.445-0.535: that band is already the
            // train-fact row on this card (0.455-0.575) and Buildings has no such row. The split is
            // HORIZONTAL instead - each sentence sits directly above the button it explains, which
            // is WO-1382 ruling #4's actual requirement: train fact over TRAIN (x 0.02-0.49),
            // upgrade benefit over UPGRADE (x 0.51-0.98). No font was shrunk to make it fit.
            var troopBenefit = ElarionUiKit.Label(card,
                string.IsNullOrEmpty(selected.NextUnlockText) ? "" : "After upgrade: " + ManageScreenVM.Ascii(selected.NextUnlockText),
                0.455f, 0.575f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.51f, 0.98f);
            ElarionUiKit.FitSingleLine(troopBenefit, ElarionUiKit.FontHardFloor, 26f);

            // WO-1422 ruling 3.10.3 / 3.5: the Troops card's CTA band carries TWO faces at the
            // touch floor (113.1px tall, 0.02-0.48 and 0.52-0.98) and has NO third slot. Ruling
            // 3.10's own escape hatch says drop the SECOND DOOR before the touch height, so a
            // DoorLabel authored here is REPORTED, never squeezed in.
            if (!string.IsNullOrEmpty(selected.DoorLabel))
                FlowTrace.Warn("Manage", "troop " + selected.Id + " authors DoorLabel='" + selected.DoorLabel +
                    "' but the Troops CTA band already holds TRAIN + UPGRADE at the 112px floor - " +
                    "the door is NOT painted (WO-1422 ruling 3.10 escape hatch). A third face needs a " +
                    "taller card, which is the Phase 2 unification WO.");

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
                DeNelle.Core.UI.TutorialHighlightRegistry.Register("manage.troop_cta_train", (RectTransform)train.transform);   // WO-1389
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
                // ⛔ WO-1422 ruling 3.10.2: NextUnlockText NO LONGER rides this sub-line. WO-1389
                // appended it as a third clause ("1m 30s . Ready . L3 unlocks Sweeping Cut") and
                // the device frame proved it truncates. It is painted as its own row above this
                // face (troopBenefit) - the panel still READS the field, which is what
                // ManageTroopsTrainDoorRegression case 7 asserts.
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
                DeNelle.Core.UI.TutorialHighlightRegistry.Register("manage.troop_cta_upgrade", (RectTransform)upgrade.transform);   // WO-1389
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
            if (open != null)
            {
                open.gameObject.name = "TroopOpenQueue";
                DeNelle.Core.UI.TutorialHighlightRegistry.Register("manage.open_queue", (RectTransform)open.transform);   // WO-1389
            }
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
                    // WO-1422 POLISH (MEASURED 2026-09-06, ManageTroops_1920x1080.png): at alpha
                    // 0.28 over the dark screen the extra rows had no visible plate at all, so
                    // "2. Archer x1" read as SPILLING BELOW the TRAINING NOW band rather than as
                    // its continuation. Same plate colour as the band above it - one band, two rows.
                    var plate = ElarionUiKit.AddImage(row, "RowPlate", Vector2.zero, Vector2.one,
                        new Color(0.05f, 0.04f, 0.03f, 0.70f));
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
            float barX0, float barX1, float timeX0, float timeX1, Sprite artOverride = null,
            string labelOverride = null)
        {
            var number = ElarionUiKit.Label(row, ordinal + ".", 0.15f, 0.85f, ElarionUi.Gold,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Center, numX0, numX1, bold: true);
            ElarionUiKit.FitSingleLine(number, 24f, 36f);

            var medallion = MakeZone(row, "Medallion", new Vector2(medX0, 0.12f), new Vector2(medX1, 0.88f));
            Sprite art = artOverride ?? (!string.IsNullOrEmpty(r.IconRole) ? RpgUiCatalog.Get(r.IconRole, r.IconKey) : null);
            ElarionUiKit.Portrait(medallion, art, active: !r.Queued);

            // WO-1422 POLISH: labelOverride wins when the CALLER can name the job better than the
            // queue row can. A placed-structure upgrade carries no BuildingTierCatalog identity
            // (ManageScreenVM.cs:765-772 leaves BuildingId empty and falls back to
            // ObsidianQueueHud.FormatJobTarget), so the Builder band printed the raw job key
            // title-cased - "Tower Ground Archer..." - measured in ManageDefense_2670x1200.png.
            var name = ElarionUiKit.Label(row,
                string.IsNullOrEmpty(labelOverride) ? ManageScreenVM.Ascii(r.Label ?? "") : labelOverride,
                0.15f, 0.85f, ElarionUi.Parchment,
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

        // =====================================================================
        //  WO-1422 — THE DEFENCE WORKSPACE: rail (one row per TYPE) + selected-defence card
        // ---------------------------------------------------------------------
        // ⛔ ONE ROW PER TYPE, NEVER PER PLACED INSTANCE (ruling 3.1). wall_wood is upgradable and
        // a town carries many segments, so a per-instance rail is UNBOUNDED. The card names the
        // type, says how many are placed and at what level, and its CTA upgrades the FIRST placed
        // instance at the LOWEST level - which is exactly what the JobKey already targeted before
        // this ticket, so this is presentation, not behaviour.
        // =====================================================================

        private void AddDefenseWorkspaceRow(DefenseChoiceVM selected)
        {
            var workspace = MakeRowHost("DefenseSplitWorkspace", TroopWorkspacePx);
            var railZone = MakeZone(workspace, "DefenseSelectorRail", new Vector2(0f, 0f), new Vector2(0.26f, 1f));
            var railPlate = ElarionUiKit.AddImage(railZone, "RailPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            railPlate.GetComponent<Image>().raycastTarget = false;
            var railScroll = ElarionUiKit.MakeScrollZone(railZone, spacing: 6f, padding: 8);
            if (railScroll == null || railScroll.content == null)
                FlowTrace.Fail("Manage", "defense rail MakeScrollZone returned no content - the rail has no build site.");
            else
            {
                int selectedIndex = 0;
                _rowParent = railScroll.content;
                try
                {
                    for (int i = 0; i < _vm.DefenseChoices.Count; i++)
                    {
                        var choice = _vm.DefenseChoices[i];
                        if (choice == null) continue;
                        bool isSelected = string.Equals(choice.Id, selected.Id, StringComparison.OrdinalIgnoreCase);
                        if (isSelected) selectedIndex = i;
                        Guard.Try("Manage", "defense rail row " + choice.Id, () => BuildDefenseRailRow(choice, isSelected));
                    }
                    MakeRowHost("DefenseRailTailSpacer", TroopWorkspacePx - TroopRailRowPx);
                }
                finally { _rowParent = null; }

                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(railScroll.content);
                if (railScroll.scroll != null)
                {
                    var viewport = railScroll.scroll.viewport;
                    float viewportPx = viewport != null ? viewport.rect.height : TroopWorkspacePx;
                    float maxScrollPx = Mathf.Max(0f, railScroll.content.rect.height - viewportPx);
                    float selectedTopPx = Mathf.Min(maxScrollPx, selectedIndex * (TroopRailRowPx + 6f));
                    railScroll.scroll.StopMovement();
                    railScroll.scroll.verticalNormalizedPosition = maxScrollPx > 0.5f
                        ? 1f - selectedTopPx / maxScrollPx
                        : 1f;
                    FlowTrace.Step("Manage", "defense rail aligned row=" + selectedIndex +
                        " topPx=" + selectedTopPx.ToString("0") + " maxPx=" + maxScrollPx.ToString("0"));
                }
            }

            var card = MakeZone(workspace, "DefenseSelectedCard", new Vector2(0.275f, 0f), new Vector2(1f, 1f));
            BuildDefenseCard(card, selected);
        }

        private void BuildDefenseRailRow(DefenseChoiceVM choice, bool isSelected)
        {
            var row = MakeRowHost("DefenseChoiceRow_" + choice.Id, TroopRailRowPx);
            var faceGo = ElarionUiKit.AddImage(row, "DefenseChoice_" + choice.Id, Vector2.zero, Vector2.one,
                isSelected ? new Color(0.24f, 0.18f, 0.08f, 0.90f) : new Color(0f, 0f, 0f, 0.28f), rounded: false);
            var face = faceGo.GetComponent<Image>();
            face.raycastTarget = true;
            var button = faceGo.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() =>
            {
                _selectedDefenseId = choice.Id;
                FlowTrace.Step("Manage", "defense rail selected=" + choice.Id);
                Render();
            });
            if (isSelected)
            {
                var outline = faceGo.AddComponent<Outline>();
                outline.effectColor = ElarionUi.Gold;
                outline.effectDistance = new Vector2(4f, -4f);
                outline.useGraphicAlpha = false;
            }

            var medallion = MakeZone(faceGo.transform, "Medallion", new Vector2(0.03f, 0.08f), new Vector2(0.27f, 0.92f));
            ElarionUiKit.Portrait(medallion, DefenseSprite(choice), active: isSelected);

            var name = ElarionUiKit.Label(faceGo.transform, ManageScreenVM.Ascii(choice.Name ?? ""), 0.52f, 0.96f,
                ElarionUi.Parchment, (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.30f, 0.84f, bold: true);
            ElarionUiKit.FitSingleLine(name, 26f, 38f);
            // The rail sub-line states the LEVEL the card acts on (the lowest placed) and, when the
            // type has more than one instance, how many - so "one row" never reads as "one tower".
            string railState = "Level " + choice.Level +
                (string.Equals(choice.StateWord, "Max", StringComparison.Ordinal) ? " . Max" :
                 string.Equals(choice.StateWord, "Building", StringComparison.Ordinal) ? " . Building" : "") +
                (choice.PlacedCount > 1 ? " . x" + choice.PlacedCount : "");
            var sub = ElarionUiKit.Label(faceGo.transform, railState,
                0.06f, 0.48f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.30f, 0.84f);
            ElarionUiKit.FitSingleLine(sub, 22f, 30f);
            var chevron = ElarionUiKit.Label(faceGo.transform, ">", 0.10f, 0.90f,
                isSelected ? ElarionUi.Gold : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontBody, TextAlignmentOptions.Center, 0.84f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(chevron, 30f, 50f);
            ElarionUiKit.ClampMinTouch(button);
        }

        /// <summary>
        /// WO-1422 ruling 3.8 — DEFENCE TIER ART. Assets/Resources/Portraits/ holds three tier
        /// sheets each for archer-tower / ballista / catapult / arcane-spire / wizard-tower plus
        /// the wall, mine, caravan and storage sheets, and NO code path could reach the tier
        /// suffix: ResolveEntryArtPublic never appends one, and LoadManageBuildingSprite probes
        /// only Portraits/Buildings/. This probes, in order: the LEVEL-SUFFIXED key, the base key,
        /// the shared Build palette (which owns the alias table, e.g. wall_wood -> Wooden_Wall),
        /// the concept resolver, then warns and falls back to the neutral hammer.
        /// ⚠ This is NOT BuildingSprite and is deliberately not bound by its [building-art-palette-first]
        /// ban - and it does not consult ManageBuildingPortraitGaps, which is a Buildings-only list.
        /// </summary>
        private static Sprite DefenseSprite(DefenseChoiceVM choice)
        {
            if (choice == null) return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "hammer");

            Sprite art = Guard.Try<Sprite>("Manage", "defense art " + (choice.Id ?? "<null>"), () =>
            {
                // ResolveBuildingPortraitKey has ALREADY appended the level suffix, so the base key
                // is recovered by stripping it - never by re-slugging the id a second way.
                string key = choice.PortraitKey ?? "";
                string levelSuffix = "-" + choice.Level;
                string root = choice.Level > 1 && key.EndsWith(levelSuffix, StringComparison.Ordinal)
                    ? key.Substring(0, key.Length - levelSuffix.Length)
                    : key;
                string tierKey = choice.Level > 1 ? root + "-" + choice.Level : null;
                Sprite found = LoadManageBuildingSpriteAt(tierKey) ?? LoadManageBuildingSpriteAt(root);
                if (found != null) return found;

                // CatalogRegistry is NOT populated under -executeMethod unless the caller hydrates
                // it, so Get can legitimately return null here - guard it, never assume.
                var entry = string.IsNullOrEmpty(choice.CatalogEntryId)
                    ? null
                    : DeNelle.Core.Catalog.CatalogRegistry.Get(choice.CatalogEntryId);
                if (entry == null) return null;
                return DeNelle.Village.BuildPaletteUI.ResolveEntryArtPublic(entry)
                       ?? DeNelle.Core.UI.ConceptIconResolver.ResolveAny(entry.id, entry.type.ToString());
            }, null);

            if (art == null)
                FlowTrace.Warn("Manage", "defense art unresolved id=" + (choice.Id ?? "<null>") +
                    " portraitKey=" + (choice.PortraitKey ?? "<null>") +
                    " level=" + choice.Level + " catalogEntryId=" + (choice.CatalogEntryId ?? "<null>") +
                    " - add Portraits/<key>[-level]; neutral hammer used");
            return art ?? RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "hammer");
        }

        /// <summary>
        /// The SELECTED DEFENCE card. Same grammar as the Buildings card, plus the "n placed"
        /// sub-line ruling 3.1 requires so one rail row never reads as one structure. Defence has
        /// no second door (ruling 3.5: DoorLabel is null), so the CTA is FULL WIDTH.
        /// </summary>
        private void BuildDefenseCard(RectTransform card, DefenseChoiceVM selected)
        {
            var plate = ElarionUiKit.AddImage(card, "CardPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            plate.GetComponent<Image>().raycastTarget = false;
            ElarionUiKit.GoldPerimeter(card);

            var medallion = MakeZone(card, "DefensePortrait", new Vector2(0.02f, 0.59f), new Vector2(0.16f, 0.99f));
            ElarionUiKit.Portrait(medallion, DefenseSprite(selected), active: true);

            var name = ElarionUiKit.Label(card, ManageScreenVM.Ascii((selected.Name ?? "").ToUpperInvariant()),
                0.855f, 1f, ElarionUi.Gold, (int)ElarionUi.FontTitle,
                TextAlignmentOptions.Left, 0.19f, 0.72f, bold: true);
            ElarionUiKit.FitSingleLine(name, 30f, 48f);
            var level = ElarionUiKit.Label(card, "LEVEL " + selected.Level, 0.855f, 1f, ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.74f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(level, 26f, 36f);

            // ⚠ WO-1422 POLISH (MEASURED 2026-09-06, ManageDefense_2670x1200.png): the card jumped
            // from the name straight to "Upgrade: 540 240" - NEITHER this line NOR the description
            // painted. The VM was innocent (ManageScreenVM.cs:1383-1384 substitutes "A village
            // structure." for a blank, :1413-1415 always composes PlacedText); both labels were
            // authored into 0.07 x TroopWorkspacePx(260) = 18.2px bands, and TMP's Ellipsis overflow
            // CULLS THE WHOLE LINE when the fontSizeMin line (FontHardFloor 20 -> ~23-24px) cannot
            // seat in the rect - the cause ElarionUiKitObsidian.cs:3110-3116 records for the "bare
            // plate" class. Buildings' own description band (0.70-0.83 = 33.8px) is the only height
            // proven to render on this card, so BOTH sentences now share it: ruling 3.1 asks for a
            // card SUB-LINE, not a second band, and the placed tally leads so an ellipsis can only
            // ever eat the flavour half. ⛔ Never re-author a text band on this card below ~24px.
            string defenseSubLine = ManageScreenVM.Ascii(selected.PlacedText ?? "");
            string defenseDesc = ManageScreenVM.Ascii(selected.Description ?? "");
            if (defenseSubLine.Length > 0 && defenseDesc.Length > 0) defenseSubLine += " - " + defenseDesc;
            else if (defenseSubLine.Length == 0) defenseSubLine = defenseDesc;
            var desc = ElarionUiKit.Label(card, defenseSubLine, 0.70f, 0.83f,
                ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.19f, 0.72f);
            ElarionUiKit.FitSingleLine(desc, ElarionUiKit.FontHardFloor, 30f);

            // The state WORD is the only carrier of state - the owner is red/green colourblind.
            var badge = ElarionUiKit.AddImage(card, "DefenseStateBadge", new Vector2(0.74f, 0.70f),
                new Vector2(0.98f, 0.83f), new Color(0.12f, 0.25f, 0.08f, 0.82f), rounded: false);
            badge.GetComponent<Image>().raycastTarget = false;
            var state = ElarionUiKit.Label(badge.transform, ManageScreenVM.Ascii(selected.StateWord ?? ""), 0f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(state, 20f, 28f);

            if (string.Equals(selected.StateWord, "Max", StringComparison.Ordinal)) return;

            ElarionUiKit.CostRow(card, selected.UpgradeCostParts, new Vector2(0.02f, 0.54f),
                new Vector2(0.72f, 0.695f), ElarionUi.Parchment, prefix: "Upgrade:",
                fontPx: (int)ElarionUi.FontMicro);
            string readiness = selected.UpgradeReady ? "Ready" : "Short";
            string factText = string.IsNullOrEmpty(selected.UpgradeTimeText)
                ? readiness : selected.UpgradeTimeText + " . " + readiness;
            var fact = ElarionUiKit.Label(card, factText, 0.54f, 0.695f, ElarionUi.Parchment,
                (int)ElarionUi.FontMicro, TextAlignmentOptions.Right, 0.73f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(fact, 20f, 28f);

            var benefit = ElarionUiKit.Label(card,
                string.IsNullOrEmpty(selected.AfterUpgradeText) ? "" : "After upgrade: " + ManageScreenVM.Ascii(selected.AfterUpgradeText),
                0.445f, 0.535f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.02f, 0.98f);
            ElarionUiKit.FitSingleLine(benefit, ElarionUiKit.FontHardFloor, 26f);

            if (string.Equals(selected.StateWord, "Building", StringComparison.Ordinal))
            {
                BuildCardFace(card, "DefenseCta_Building", "BUILDING", 0.02f, 0.98f);
                return;
            }

            var upgrade = ElarionUiKit.BuildObsidianButton(card, "UPGRADE TO L" + selected.NextLevel,
                ElarionUiKit.ObsidianButtonStyle.Style1,
                selected.UpgradeReady ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.02f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1),
                () => Guard.Try("Manage", "upgrade defense", () => selected.Activate?.Invoke()));
            if (upgrade != null)
            {
                upgrade.gameObject.name = "DefenseCta_Upgrade";
                upgrade.interactable = selected.UpgradeReady && selected.Activate != null;
                MedievalUiSkin.ApplyButton(upgrade, true);
            }
            ElarionUiKit.ClampMinTouch(upgrade);
        }

        /// <summary>
        /// WO-1422 POLISH — the BUILDING NOW medallion + name on the DEFENCE tab. A placed-structure
        /// upgrade job is keyed by <c>PlacedUpgradeKey.Compose(itemId, cellX, cellZ)</c>
        /// ("tower_ground_archer@3_7"), and <c>QueueRowVM.BuildingId</c> is populated only when the
        /// job resolves to a <c>BuildingTierCatalog</c> row — which a tower never does — so the band
        /// fell back to the title-cased job key and neutral art.
        /// ⛔ THE KEY SHAPE IS NOT TRUSTED, AND THE FIRST PASS OF THIS HELPER TRUSTED IT TOO MUCH.
        /// It derived the item id from <c>PlacedUpgradeKey.TryParse</c> ALONE, which accepts only
        /// the live <c>itemId@cellX_cellZ</c> grammar (PlacedUpgradeKey.cs:42 — no '@', no parse).
        /// The Manage capture fixture seeds the Builder job as <c>"tower_ground_archer:7:0"</c>
        /// (Assets/Editor/UICaptureLaunch.cs:7252), a COLON key, so TryParse returned false, every
        /// remaining arm missed, and ManageDefense_2670x1200.png printed the title-cased raw key
        /// beside empty art — the exact defect this helper was written to close.
        /// FOUR derivations are now tried, all OrdinalIgnoreCase:
        ///   1. <c>choice.JobKey</c> vs <c>r.JobId</c> — the literal string the card's own CTA
        ///      composed for this instance; an exact hit needs no id grammar at all.
        ///   2. the parsed placed key (live '@' shape).
        ///   3. the PREFIX CUT at the first ':' or '@' — the SAME cut
        ///      <c>ManageScreenVM.NormalizeBuildingJobId</c> already applies (ManageScreenVM.cs:1653),
        ///      with underscores KEPT because <c>DefenseChoiceVM.Id</c> is the raw BaseLayout itemId
        ///      while that method hyphenates for the tier catalog.
        ///   4. <c>r.BuildingId</c>, for the fixture that seeds a bare catalog id.
        /// On a miss this returns null, the caller's existing fallback art runs, and a Warn names
        /// the id. It never throws.
        /// </summary>
        private DefenseChoiceVM FindDefenseChoiceForJob(QueueRowVM r)
        {
            if (r == null || _vm == null) return null;

            string itemId;
            if (string.IsNullOrEmpty(r.JobId) ||
                !DeNelle.Village.Buildings.Progression.PlacedUpgradeKey.TryParse(r.JobId, out itemId, out _, out _))
                itemId = null;

            // The shape-agnostic cut. Never rendered — only matched against authored ids.
            string cutId = null;
            if (!string.IsNullOrEmpty(r.JobId))
            {
                int suffix = r.JobId.IndexOfAny(new[] { ':', '@' });
                cutId = (suffix > 0 ? r.JobId.Substring(0, suffix) : r.JobId).Trim();
                if (cutId.Length == 0) cutId = null;
            }

            for (int i = 0; i < _vm.DefenseChoices.Count; i++)
            {
                var choice = _vm.DefenseChoices[i];
                if (choice == null || string.IsNullOrEmpty(choice.Id)) continue;
                if ((!string.IsNullOrEmpty(choice.JobKey) && !string.IsNullOrEmpty(r.JobId) &&
                        string.Equals(choice.JobKey, r.JobId, StringComparison.OrdinalIgnoreCase)) ||
                    (itemId != null && string.Equals(choice.Id, itemId, StringComparison.OrdinalIgnoreCase)) ||
                    (cutId != null && string.Equals(choice.Id, cutId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(r.BuildingId) && string.Equals(choice.Id, r.BuildingId, StringComparison.OrdinalIgnoreCase)))
                    return choice;
            }

            // ⚠ A MISS IS NOT ALWAYS A DEFECT. The Builder line is SHARED, so the first job on the
            // Defence tab can legitimately be a town building (Farm -> L2); the tier catalog already
            // named it, BuildingId is populated, and the caller's Buildings expression resolves it
            // correctly. Warning on that normal state would spam a §12 Warn on every Render, which
            // is how a real signal gets tuned out. Warn ONLY for the genuinely unnameable job.
            if (string.IsNullOrEmpty(r.BuildingId))
                FlowTrace.Warn("Manage", "defence job id '" + (r.JobId ?? "<null>") + "' (parsed item '" +
                    (itemId ?? "<none>") + "', cut item '" + (cutId ?? "<none>") +
                    "') does not resolve to an authored type in DefenseChoices (" +
                    _vm.DefenseChoices.Count + " known) AND carries no BuildingId - the BUILDING NOW row " +
                    "keeps the band's fallback name and art");
            else
                FlowTrace.Step("Manage", "defence tab BUILDING NOW job '" + r.JobId + "' is a catalog " +
                    "building (buildingId=" + r.BuildingId + "), not a placed defence - Buildings art path used");
            return null;
        }

        // =====================================================================
        //  WO-1422 — THE RESEARCH WORKSPACE: rail (one row per PERK) + selected-perk card
        // ---------------------------------------------------------------------
        // ⛔ ONE ROW PER PERK (ruling 3.6), not per building: a per-building card would need three
        // or four verbs inside the single CTA band and no card grammar here supports that. The
        // owning building becomes the row's SUB-LINE, which is what kills the developer-shaped
        // "Lumber Mill - Improved Logging" label. Research has NO LEVEL: the card's level slot
        // carries "TIER n" (ruling 3.7) and never paints "LEVEL 0".
        // =====================================================================

        private void AddResearchWorkspaceRow(ResearchChoiceVM selected)
        {
            var workspace = MakeRowHost("ResearchSplitWorkspace", TroopWorkspacePx);
            var railZone = MakeZone(workspace, "ResearchSelectorRail", new Vector2(0f, 0f), new Vector2(0.26f, 1f));
            var railPlate = ElarionUiKit.AddImage(railZone, "RailPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            railPlate.GetComponent<Image>().raycastTarget = false;
            var railScroll = ElarionUiKit.MakeScrollZone(railZone, spacing: 6f, padding: 8);
            if (railScroll == null || railScroll.content == null)
                FlowTrace.Fail("Manage", "research rail MakeScrollZone returned no content - the rail has no build site.");
            else
            {
                int selectedIndex = 0;
                string selectedKey = ResearchKeyOf(selected);
                _rowParent = railScroll.content;
                try
                {
                    for (int i = 0; i < _vm.ResearchChoices.Count; i++)
                    {
                        var choice = _vm.ResearchChoices[i];
                        if (choice == null) continue;
                        bool isSelected = string.Equals(ResearchKeyOf(choice), selectedKey, StringComparison.OrdinalIgnoreCase);
                        if (isSelected) selectedIndex = i;
                        Guard.Try("Manage", "research rail row " + ResearchKeyOf(choice), () => BuildResearchRailRow(choice, isSelected));
                    }
                    MakeRowHost("ResearchRailTailSpacer", TroopWorkspacePx - TroopRailRowPx);
                }
                finally { _rowParent = null; }

                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(railScroll.content);
                if (railScroll.scroll != null)
                {
                    var viewport = railScroll.scroll.viewport;
                    float viewportPx = viewport != null ? viewport.rect.height : TroopWorkspacePx;
                    float maxScrollPx = Mathf.Max(0f, railScroll.content.rect.height - viewportPx);
                    float selectedTopPx = Mathf.Min(maxScrollPx, selectedIndex * (TroopRailRowPx + 6f));
                    railScroll.scroll.StopMovement();
                    railScroll.scroll.verticalNormalizedPosition = maxScrollPx > 0.5f
                        ? 1f - selectedTopPx / maxScrollPx
                        : 1f;
                    FlowTrace.Step("Manage", "research rail aligned row=" + selectedIndex +
                        " topPx=" + selectedTopPx.ToString("0") + " maxPx=" + maxScrollPx.ToString("0"));
                }
            }

            var card = MakeZone(workspace, "ResearchSelectedCard", new Vector2(0.275f, 0f), new Vector2(1f, 1f));
            BuildResearchCard(card, selected);
        }

        private void BuildResearchRailRow(ResearchChoiceVM choice, bool isSelected)
        {
            string key = ResearchKeyOf(choice);
            var row = MakeRowHost("ResearchChoiceRow_" + key, TroopRailRowPx);
            var faceGo = ElarionUiKit.AddImage(row, "ResearchChoice_" + key, Vector2.zero, Vector2.one,
                isSelected ? new Color(0.24f, 0.18f, 0.08f, 0.90f) : new Color(0f, 0f, 0f, 0.28f), rounded: false);
            var face = faceGo.GetComponent<Image>();
            face.raycastTarget = true;
            var button = faceGo.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() =>
            {
                _selectedResearchKey = key;
                FlowTrace.Step("Manage", "research rail selected=" + key);
                Render();
            });
            if (isSelected)
            {
                var outline = faceGo.AddComponent<Outline>();
                outline.effectColor = ElarionUi.Gold;
                outline.effectDistance = new Vector2(4f, -4f);
                outline.useGraphicAlpha = false;
            }

            var medallion = MakeZone(faceGo.transform, "Medallion", new Vector2(0.03f, 0.08f), new Vector2(0.27f, 0.92f));
            var portrait = ElarionUiKit.Portrait(medallion, ResearchSprite(choice), active: isSelected);
            if (choice.Locked && portrait?.image != null) portrait.image.color = new Color(0.42f, 0.42f, 0.42f, 1f);
            if (choice.Locked) BuildLockBadge(medallion);

            // NAME over SUB-LINE - the perk's own name, then the building that owns it. This is
            // what retires the "Lumber Mill - Improved Logging" developer label (ruling 3.6).
            var name = ElarionUiKit.Label(faceGo.transform, ManageScreenVM.Ascii(choice.Name ?? ""), 0.52f, 0.96f,
                choice.Locked ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.30f, 0.84f, bold: true);
            ElarionUiKit.FitSingleLine(name, 26f, 38f);
            // ⚠ ORDER IS LOAD-BEARING (measured 2026-09-06, seeker-357569-research.png): with the
            // building name leading, EVERY row of a building read "Cathedral of Magic ...." and the
            // ellipsis ate the only term that differs between rows. The STATE leads now, so the
            // discriminator always survives and the ellipsis eats the shared half - the same rule
            // the description band below already documents. The rail zone is 0..0.26 by design and
            // is NOT widened. Both terms are kept; only their order changed.
            string railBuilding = ManageScreenVM.Ascii(choice.BuildingName ?? "");
            string railSub = choice.StateWord ?? "";
            if (!string.IsNullOrEmpty(railBuilding))
                railSub = string.IsNullOrEmpty(railSub) ? railBuilding : railSub + " . " + railBuilding;
            var sub = ElarionUiKit.Label(faceGo.transform, railSub,
                0.06f, 0.48f, ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.30f, 0.84f);
            ElarionUiKit.FitSingleLine(sub, 22f, 30f);
            var chevron = ElarionUiKit.Label(faceGo.transform, ">", 0.10f, 0.90f,
                isSelected ? ElarionUi.Gold : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontBody, TextAlignmentOptions.Center, 0.84f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(chevron, 30f, 50f);
            ElarionUiKit.ClampMinTouch(button);
        }

        /// <summary>
        /// WO-1422 ruling 3.6 — PERK ART. Assets/Resources/HudIcons/BuildingUpgrades/ holds 15 .jpg
        /// plus Upgrade.png covering all 17 authored perks, and it is the folder
        /// BuildingUpgradePanelMvvm.cs:2025 already loads from.
        /// ⚠ BuildingPerkDef.IconId's doc comment (Assets/_Modules/Core/State/BuildingTierCatalog.cs)
        /// names Resources/HudItems/BuildingUpgrades/ - THAT FOLDER DOES NOT EXIST. The comment is
        /// wrong; the loader below is the truth. (The comment lives in another file and is flagged
        /// in this lane's hand-back rather than edited here.)
        /// </summary>
        private static Sprite ResearchSprite(ResearchChoiceVM choice)
        {
            if (choice == null || string.IsNullOrEmpty(choice.IconName))
                return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "hammer");

            // LoadManageBuildingSpriteAt is reused for its Texture2D->Sprite fallback and its cache:
            // these are .jpg files whose import type is not guaranteed to be Sprite.
            Sprite art = Guard.Try<Sprite>("Manage", "research art " + choice.IconName,
                () => LoadManageBuildingSpriteAt("HudIcons/BuildingUpgrades/" + choice.IconName), null);
            if (art == null)
                FlowTrace.Warn("Manage", "research perk art unresolved perk=" + ResearchKeyOf(choice) +
                    " iconName=" + choice.IconName +
                    " - expected Resources/HudIcons/BuildingUpgrades/<IconName>; neutral hammer used");
            return art ?? RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "hammer");
        }

        /// <summary>
        /// WO-1422 ruling 3.9 — the RESEARCHING NOW medallion. A Research job carries no BuildingId
        /// (QueueRowVM.BuildingId is populated only on the Builder channel), so the perk is read
        /// back off the job id, whose shape is "building-research:&lt;buildingId&gt;:&lt;perkId&gt;".
        /// ⛔ THE THIRD SEGMENT IS NOT TRUSTED: it is matched against the CATALOG-DERIVED
        /// ResearchChoices, never used as art key on its own. A shipped fixture carried the perk id
        /// `warding`, which is authored nowhere - so a segment that looks like a perk id can be one
        /// that does not exist. On a miss this returns null (the band's existing fallback art runs)
        /// and warns naming the id. It never throws.
        /// </summary>
        private Sprite ResearchJobSprite(QueueRowVM r)
        {
            if (r == null || string.IsNullOrEmpty(r.JobId)) return null;
            string[] parts = r.JobId.Split(':');
            if (parts.Length >= 3 && _vm != null)
            {
                for (int i = 0; i < _vm.ResearchChoices.Count; i++)
                {
                    var choice = _vm.ResearchChoices[i];
                    if (choice == null) continue;
                    if (string.Equals(choice.BuildingId, parts[1], StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(choice.PerkId, parts[2], StringComparison.OrdinalIgnoreCase))
                        return ResearchSprite(choice);
                }
            }
            FlowTrace.Warn("Manage", "research job id '" + r.JobId + "' does not resolve to an authored " +
                "perk in ResearchChoices (" + (_vm != null ? _vm.ResearchChoices.Count : 0) + " known) - " +
                "the RESEARCHING NOW medallion falls back to the band's default art");
            return null;
        }

        /// <summary>
        /// The SELECTED PERK card (ruling 3.7). The WHOLE tree is shown, including the two states
        /// the retired list HID: an owned perk (Researched) and an in-flight one (Researching).
        /// Researched -> no CTA. Researching -> one non-interactable RESEARCHING face. Locked -> the
        /// CanResearch reason VERBATIM as a BODY TEXT LINE, above ONE full-width live door to the
        /// prerequisite (the two-half-faces shape is retired - see the block comment on the locked
        /// branch). Available -> RESEARCH. The parameter is named `choice` because
        /// ManageProgressiveDisclosureRegression's migrated [research-locked-visible] case reads
        /// this body for `choice.Locked` and `BuildLockBadge(`.
        /// </summary>
        private void BuildResearchCard(RectTransform card, ResearchChoiceVM choice)
        {
            var plate = ElarionUiKit.AddImage(card, "CardPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            plate.GetComponent<Image>().raycastTarget = false;
            ElarionUiKit.GoldPerimeter(card);

            var medallion = MakeZone(card, "ResearchPortrait", new Vector2(0.02f, 0.59f), new Vector2(0.16f, 0.99f));
            var portrait = ElarionUiKit.Portrait(medallion, ResearchSprite(choice), active: true);
            if (choice.Locked && portrait?.image != null) portrait.image.color = new Color(0.42f, 0.42f, 0.42f, 1f);
            if (choice.Locked) BuildLockBadge(medallion);

            var name = ElarionUiKit.Label(card, ManageScreenVM.Ascii((choice.Name ?? "").ToUpperInvariant()),
                0.855f, 1f, ElarionUi.Gold, (int)ElarionUi.FontTitle,
                TextAlignmentOptions.Left, 0.19f, 0.72f, bold: true);
            ElarionUiKit.FitSingleLine(name, 30f, 48f);
            // ⛔ TierText, never the Buildings level line: a perk has no level, so reusing that
            // line would print a zero here (ruling 3.7). The literal is deliberately absent from
            // this whole method - [no-level-zero] bans it from every Research card path.
            var tier = ElarionUiKit.Label(card, ManageScreenVM.Ascii(choice.TierText ?? ""), 0.855f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.74f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(tier, 26f, 36f);

            // ⚠ WO-1422 POLISH (MEASURED 2026-09-06, ManageResearch_2670x1200.png): the card jumped
            // from the name straight to "Research: 6400" - neither the OWNING BUILDING line nor the
            // description painted, though the rail row beside it read "Barracks . Available". Not a
            // VM defect: both labels sat in 0.07 x TroopWorkspacePx(260) = 18.2px bands, and TMP's
            // Ellipsis overflow CULLS THE WHOLE LINE when the fontSizeMin line (FontHardFloor 20 ->
            // ~23-24px) cannot seat in the rect (ElarionUiKitObsidian.cs:3110-3116). They now share
            // Buildings' description band (0.70-0.83 = 33.8px), the only height on this card proven
            // to render, with the owning building leading so an ellipsis eats only the flavour half.
            // ⛔ Never re-author a text band on this card below ~24px.
            string researchLine = ManageScreenVM.Ascii(choice.BuildingName ?? "");
            string researchDesc = ManageScreenVM.Ascii(choice.Description ?? "");
            if (researchLine.Length > 0 && researchDesc.Length > 0) researchLine += " - " + researchDesc;
            else if (researchLine.Length == 0) researchLine = researchDesc;
            var desc = ElarionUiKit.Label(card, researchLine, 0.70f, 0.83f,
                ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro, TextAlignmentOptions.Left, 0.19f, 0.72f);
            ElarionUiKit.FitSingleLine(desc, ElarionUiKit.FontHardFloor, 30f);

            var badge = ElarionUiKit.AddImage(card, "ResearchStateBadge", new Vector2(0.74f, 0.70f),
                new Vector2(0.98f, 0.83f), new Color(0.12f, 0.25f, 0.08f, 0.82f), rounded: false);
            badge.GetComponent<Image>().raycastTarget = false;
            var state = ElarionUiKit.Label(badge.transform, ManageScreenVM.Ascii(choice.StateWord ?? ""), 0f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(state, 20f, 28f);

            // An owned perk is DONE: the word in the badge is the whole story, and a card with no
            // verb is the honest shape (the same delta WO-1418 made when it stopped hiding maxed
            // buildings). No cost row either - there is nothing left to pay.
            if (string.Equals(choice.StateWord, "Researched", StringComparison.Ordinal))
                return;

            // A LOCKED card needs one extra body line for the lock sentence, and the only slack on
            // this card is between the CTA band top (TroopCtaY1 = 0.445) and the cost row. The fact
            // row lifts by 0.025 ONLY when locked, which frees 0.45-0.56 below it. Arithmetic at
            // TroopWorkspacePx = 260: locked cost band 0.565-0.70 = 35.1px, which is 0.9px UNDER
            // AddCostText's preferredHeight of FontMicro+4 = 36 (CostFormat.cs:145). Safe: the
            // HorizontalLayoutGroup (childControlHeight true, childForceExpandHeight false) clamps
            // the child to 35.1, and CostText never sets overflowMode, so TMP's default Overflow
            // RENDERS it - a different path from the Ellipsis cull that blanks a short band.
            // Locked reason band 0.45-0.56 = 28.6px.
            // Available / Researching cards keep the 0.54-0.695 that was MEASURED to render.
            // ⛔ Never re-author a text band on this card below ~24px - TMP culls the whole line.
            float factY0 = choice.Locked ? 0.565f : 0.54f;
            float factY1 = choice.Locked ? 0.70f : 0.695f;
            ElarionUiKit.CostRow(card, choice.CostParts, new Vector2(0.02f, factY0),
                new Vector2(0.72f, factY1), ElarionUi.Parchment, prefix: "Research:",
                fontPx: (int)ElarionUi.FontMicro);
            string readiness = choice.Ready ? "Ready" : "Short";
            string factText = string.IsNullOrEmpty(choice.TimeText) ? readiness : choice.TimeText + " . " + readiness;
            var fact = ElarionUiKit.Label(card, factText, factY0, factY1, ElarionUi.Parchment,
                (int)ElarionUi.FontMicro, TextAlignmentOptions.Right, 0.73f, 0.98f, bold: true);
            ElarionUiKit.FitSingleLine(fact, 20f, 28f);

            if (string.Equals(choice.StateWord, "Researching", StringComparison.Ordinal))
            {
                BuildCardFace(card, "ResearchCta_Researching",
                    string.IsNullOrEmpty(choice.CtaLabel) ? "RESEARCHING" : choice.CtaLabel, 0.02f, 0.98f);
                return;
            }

            if (choice.Locked)
            {
                // ⚠ DEVICE FIX (measured 2026-09-06, owner's Seeker build 2026.09.06.357569,
                // seeker-357569-research.png). This branch used to paint TWO HALF-WIDTH faces and
                // BOTH ellipsized: "UPGRADE THE BUILDING T..." beside "UPGRADE CATHEDRAL OF M...".
                // The player could read neither WHY the perk was locked nor where the door went, and
                // the dead face was indistinguishable from the live one. Root cause is the CONTAINER,
                // not the width: CanResearch's reason is an authored SENTENCE ("Upgrade the building
                // to Tier 3 first."), and a sentence never fits a button face.
                // The reason is now a BODY TEXT LINE - prose in the body, where the description band
                // right above it already proved text renders - and the card carries exactly ONE
                // FULL-WIDTH live door wearing the SHORT CtaLabel, which is the single-CTA shape
                // BuildDefenseCard and BuildBuildingCard already use. Both halves of ruling 3.7 are
                // intact and more legible: the reason is verbatim and complete, the prerequisite is
                // still one tap away. [research-locked-visible] is satisfied by the LockReason paint
                // plus the door - it pins the reason reaching the screen, never a dead button.
                var lockLine = ElarionUiKit.Label(card,
                    ManageScreenVM.Ascii(string.IsNullOrEmpty(choice.LockReason) ? "Locked." : choice.LockReason),
                    0.45f, 0.56f, ElarionUi.Parchment, (int)ElarionUi.FontMicro,
                    TextAlignmentOptions.Left, 0.02f, 0.98f, bold: true);
                if (lockLine != null)
                {
                    lockLine.gameObject.name = "ResearchLockReason";
                    ElarionUiKit.FitSingleLine(lockLine, ElarionUiKit.FontHardFloor, 28f);
                }
                var door = ElarionUiKit.BuildObsidianButton(card,
                    ManageScreenVM.Ascii(string.IsNullOrEmpty(choice.CtaLabel) ? "OPEN" : choice.CtaLabel),
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.02f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1),
                    () => Guard.Try("Manage", "open research prerequisite", () => choice.Activate?.Invoke()));
                if (door != null)
                {
                    door.gameObject.name = "ResearchCta_Door";
                    door.interactable = choice.Activate != null;
                    MedievalUiSkin.ApplyButton(door, false);
                }
                ElarionUiKit.ClampMinTouch(door);
                return;
            }

            var research = ElarionUiKit.BuildObsidianButton(card,
                ManageScreenVM.Ascii(string.IsNullOrEmpty(choice.CtaLabel) ? "RESEARCH" : choice.CtaLabel),
                ElarionUiKit.ObsidianButtonStyle.Style1,
                choice.Ready ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.02f, TroopCtaY0), new Vector2(0.98f, TroopCtaY1),
                () => Guard.Try("Manage", "start research", () => choice.Activate?.Invoke()));
            if (research != null)
            {
                research.gameObject.name = "ResearchCta_Start";
                research.interactable = choice.Ready && choice.Activate != null;
                MedievalUiSkin.ApplyButton(research, true);
            }
            ElarionUiKit.ClampMinTouch(research);
        }

        /// <summary>
        /// A non-interactable CTA face at an ARBITRARY x-span. ⚠ Deliberately NOT a change to
        /// BuildDisabledBuildingFace, which is hardcoded full-width AND is a Body() boundary marker
        /// for ManageBuildingsCardRegression's card window - widening its signature would move that
        /// boundary. The y-span is always the shared CTA band, so the touch floor is the same
        /// 113.1px every other face on these cards clears.
        /// ⚠ The x-span is arbitrary but the ONLY live caller is the Researching state, at FULL
        /// width. The locked-Research caller that needed a HALF-width dead face is RETIRED
        /// (2026-09-06): its sentence ellipsized on a half face, so it became a body text line.
        /// Do not reintroduce a half-width face to hold a sentence.
        /// </summary>
        private static void BuildCardFace(RectTransform card, string objectName, string text, float x0, float x1)
        {
            var face = ElarionUiKit.BuildObsidianButton(card, ManageScreenVM.Ascii(text ?? ""),
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(x0, TroopCtaY0), new Vector2(x1, TroopCtaY1), null);
            if (face == null) return;
            face.gameObject.name = objectName;
            face.interactable = false;
            MedievalUiSkin.ApplyButton(face, false);
            ElarionUiKit.ClampMinTouch(face);
        }

        /// <summary>
        /// WO-1422 — the RESEARCHING NOW band: the Builder band's exact grammar on the Research
        /// channel. One painted job plus "+N more"; the extra jobs are NOT given their own rows
        /// (that is the Buildings shape, and [building-now-stays-in-band] is the pin that keeps it).
        /// The host name starts with ResearchNowPrefix so ApplyDrawerPlacement collapses it when
        /// the queue drawer opens over the card.
        /// </summary>
        private void AddResearchNowBand()
        {
            var band = MakeRowHost(ResearchNowPrefix + "Band", TrainingNowBandPx);
            var bandPlate = ElarionUiKit.AddImage(band, "BandPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.70f));
            bandPlate.GetComponent<Image>().raycastTarget = false;
            int hiddenJobs = Mathf.Max(0, _vm.QueueRows.Count - 1);
            var title = ElarionUiKit.Label(band, "RESEARCHING NOW", hiddenJobs > 0 ? 0.53f : 0.15f, 0.85f, ElarionUi.Gold,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.01f, 0.165f, bold: true);
            ElarionUiKit.FitSingleLine(title, 22f, 32f);
            if (hiddenJobs > 0)
            {
                var more = ElarionUiKit.Label(band, "+" + hiddenJobs + " more", 0.12f, 0.48f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontMicro,
                    TextAlignmentOptions.Left, 0.01f, 0.165f, bold: true);
                ElarionUiKit.FitSingleLine(more, ElarionUiKit.FontHardFloor, 28f);
            }
            var open = ElarionUiKit.BuildObsidianButton(band, "OPEN QUEUE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(PrimaryX0, BandCtrlY0), new Vector2(PrimaryX1, BandCtrlY1), ToggleQueueDrawer);
            if (open != null) open.gameObject.name = "ResearchOpenQueue";
            ElarionUiKit.ClampMinTouch(open);

            if (_vm.QueueRows.Count == 0)
            {
                var empty = ElarionUiKit.Label(band, "No research under way", 0.15f, 0.85f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.Left,
                    0.18f, ClusterX1 + 0.01f);
                ElarionUiKit.FitSingleLine(empty, 24f, 34f);
                return;
            }

            var first = _vm.QueueRows[0];
            if (first != null)
                Guard.Try("Manage", "research now job 1", () => BuildTroopTrainingNowJob(band, 1, first,
                    0.175f, 0.205f, 0.21f, 0.27f, 0.28f, 0.45f, 0.46f, 0.60f, 0.61f, ClusterX1 + 0.01f,
                    ResearchJobSprite(first)));
            if (hiddenJobs > 0)
                FlowTrace.Step("Manage", "research now capped inside band: painted=1 hidden=" + hiddenJobs);
        }

        private static string BrowseHeading(ManageTab tab)
        {
            switch (tab)
            {
                // WO-1422 ruling 3.2: the Defence arm is GONE with the paged path. The heading it
                // returned promised upgradable TOWERS, and that was a measured LIE - the tab also
                // lists walls, the crystal mine, the healing caravan and the three storage
                // containers. The lie dies with the surface that printed it, not by rewording it.
                // The literal itself is deliberately not repeated here: a re-pointed suite asserts
                // it is absent from this FILE, not merely unreachable.
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

        // ⛔ WO-1422 - AddBrowseRow AND BuildBrowseRowContent WERE DELETED HERE, DELIBERATELY.
        // They painted the paged text list ("Lumber Mill - Improved Logging  Ready - takes 11m 0s
        // [RESEARCH]") that Defence and Research used to share. All four destinations now build a
        // rail + selected card, so AddBrowseRow's ONE call site - the pager inside RenderList -
        // went away with it and both methods had zero callers. Dead code that looks like a shipped
        // feature is the exact failure ManageQueueDrawerRegression:103-113 was written to catch,
        // so they are gone rather than parked. The LOCK treatment they carried (r.Locked + the
        // BuildLockBadge padlock, WO-1390) MOVED to BuildResearchCard, which is the only surface
        // that ever showed a locked browse row. _vm.BrowseRows itself is UNCHANGED and still built
        // by the VM: three suites drive it, and the Troops "Saved army compositions" row reads it.
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
