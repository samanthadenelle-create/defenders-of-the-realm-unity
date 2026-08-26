// =============================================================================
// ArmyMusterPanel — Armies loadout bank + one-tap TRAINING ORDER (WO-897 + WO-934,
// re-laid-out by WO-1230).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village
//
// CODE-BUILT uGUI (no UXML). Colourblind-safe TEXT state. ASCII only.
//
// Player loop (fun + value):
//   1. Pick one of 3 saved loadout slots (Raid Push / Wall Hold / Siege Prep)
//   2. Quick-fill a recipe OR step troops with [+] / [-]
//   3. Save to slot (persists)  OR  Train Army (auto-queues Train jobs)
//   4. Watch Obsidian Train queue fill — army prepares while you play
//
// =============================================================================
// WO-1230 — WHY THIS FILE OWNS A LAYOUT TABLE
// -----------------------------------------------------------------------------
// THE DEFECT (owner felt-test, Seeker 2026.08.26.342290, six measured collisions):
// the two command bands were parented straight to `chrome.content.transform` —
// the RAW frame content — while every other element went into a LAYOUT ZONE
// (layout.bodyLeft / bodyRight / footer). The bands therefore sat OUTSIDE the
// zone system and painted over whatever the zones had already placed: the slot
// buttons over the panel TITLE, and the action band over the FOOTER (the wallet)
// AND over the shared kit Close, which is a fixed 360x132 box seated bottom-CENTRE
// (ElarionUiKit.SeatSharedCloseInside / DefaultCloseZone) — that is the "Cl..."
// fragment behind "Save slot 1".
//
// THE FIX is not a nudge. Every element this panel draws is now declared ONCE, in
// ComputeBands / ComputeRowBands below, as an exclusive rect; Open() applies those
// rects to the kit zones it re-seats and to the two NEW band zones it adds, and
// ArmyMusterLayoutRegression measures the SAME table on a live canvas and fails if
// any two rects intersect. One table, one partition, no overlay.
//
// ⛔ THE BANDS ARE DERIVED FROM MinTouchPx, NOT FROM THE MOCKUP'S FRACTIONS. The
// approved mockup drew ~116 device px bands at 2670x1200, which is ~93 canvas
// units — BELOW the 112 floor. Authoring the mockup's number verbatim would have
// handed the layout to ClampMinTouch at runtime, and a clamp that grows a control
// after the fact is exactly how the hero-select overlap was created. The bands are
// therefore computed in reference px and the mockup's PROPORTIONS are honoured
// inside them.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    public sealed class ArmyMusterPanel : MonoBehaviour
    {
        private static readonly ArmyComposition s_composition = new ArmyComposition { Name = "Raid Push" };

        private GameObject _ui;
        private RectTransform _listContent;
        private Transform _detailHost;
        private Transform _detailBody;
        private RectTransform _selectorHost;
        private RectTransform _actionHost;
        private RectTransform _walletHost;
        private TextMeshProUGUI _rosterHint;
        private Button _musterCta;
        private TextMeshProUGUI _musterCtaLabel;
        private TextMeshProUGUI _musterCtaSub;
        private ElarionUiKit.CurrencyChipHandle[] _wallet;
        private PanelHandle _panelHandle;

        private string _lastResultHeadline = "";
        private string _lastResultDetail = "";
        private int _activeSlot;

        private float _panelW = 1f;
        private float _panelH = 1f;
        private float _rowW = 1f;
        private int _visibleRows = 3;

        private const float RowHeightPx = ElarionUiKit.MinTouchPx;   // 112 — a row IS a touch row
        private const float RowGapPx = 8f;
        private const float HintStripPx = 46f;                       // "+ N more (scroll)" lane
        private static readonly Color RowPlate = new Color(0.16f, 0.16f, 0.18f, 0.92f);
        /// <summary>WO-1230 collision 6: the summary well is CREAM on an all-obsidian UI because
        /// FrameCrafting bakes a parchment right-hand well and the kit paints a parchment plate over
        /// it. The panel re-tints that ONE plate (it does not touch the kit).</summary>
        private static readonly Color SummaryFill = new Color(0.075f, 0.070f, 0.082f, 0.98f);
        private static readonly Color CountFill = new Color(0.10f, 0.09f, 0.06f, 0.95f);

        public bool IsOpen => _ui != null;
        private static ArmyMusterPanel s_host;

        // =====================================================================
        //  WO-1230 LAYOUT TABLE — the single source of truth for every rect this
        //  panel draws, and the thing the layout oracle measures.
        // =====================================================================

        /// <summary>One named, exclusive rect in PANEL fractions (or ROW fractions for the row
        /// table). Public because the layout regression asserts on the same table the panel
        /// builds from — a re-derived copy could not fail (WO-1138 hollow pass).</summary>
        public struct BandRect
        {
            public string Name;
            public Rect Frac;
            public BandRect(string name, float xMin, float yMin, float xMax, float yMax)
            {
                Name = name;
                Frac = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
        }

        /// <summary>Panel anchors on the modal canvas (the values passed to BuildObsidianPanel).</summary>
        public const float PanelAnchorMinX = 0.06f;
        public const float PanelAnchorMinY = 0.05f;
        public const float PanelAnchorMaxX = 0.94f;
        public const float PanelAnchorMaxY = 0.95f;
        public const float PanelFracW = PanelAnchorMaxX - PanelAnchorMinX;
        public const float PanelFracH = PanelAnchorMaxY - PanelAnchorMinY;

        /// <summary>Owner ruling 2026-08-26 (shared with WO-1228): SIX lines, then scroll.</summary>
        public const int MaxVisibleRows = 6;

        /// <summary>The roster count field's font. The oracle measures "999" at this size against
        /// the authored count band, so the two can never drift.</summary>
        public const int CountFontSize = ElarionUi.FontBody;

        /// <summary>The widest count the acceptance criterion names (three digits, no wrap).</summary>
        public const string WidestCount = "999";

        /// <summary>
        /// Every panel-level element as an EXCLUSIVE rect, in fractions of the panel.
        /// <paramref name="panelW"/>/<paramref name="panelH"/> are the panel's size in canvas
        /// REFERENCE units, so the touch bands are real pixels and not a fraction that shrinks
        /// with the surface.
        /// </summary>
        public static BandRect[] ComputeBands(float panelW, float panelH)
        {
            panelW = Mathf.Max(1f, panelW);
            panelH = Mathf.Max(1f, panelH);

            // A command band is MinTouchPx plus a little breathing room, expressed as a fraction
            // of THIS panel. Clamped so a freak surface cannot eat the body well entirely.
            float band = Mathf.Clamp((ElarionUiKit.MinTouchPx + 8f) / panelH, 0.09f, 0.19f);
            float gap = Mathf.Clamp(12f / panelH, 0.006f, 0.020f);

            // The shared Close keeps its canonical 360x132 box (owner F8 x3 — one Close size on
            // every screen). WO-1230 moves it to the HEADER RIGHT, which is what frees the whole
            // bottom-centre lane the action band needs. The kit's close-band RESERVATION is not
            // touched; this panel re-seats its own zones outright.
            float closeH = Mathf.Clamp(ElarionUiKit.CanonCtaHeight / panelH, 0.10f, 0.22f);
            float closeW = Mathf.Clamp(ElarionUiKit.CanonCtaWidth / panelW, 0.10f, 0.30f);
            float closeTop = 0.992f;
            float closeBottom = closeTop - closeH;
            float closeRight = 0.955f;
            float closeLeft = closeRight - closeW;

            float selTop = closeBottom - gap;
            float selBottom = selTop - band;

            float actionBottom = 0.016f;
            float actionTop = actionBottom + band;

            float bodyTop = selBottom - gap;
            float bodyBottom = actionTop + gap;

            return new[]
            {
                // Title alone in its band — nothing overlays it (collision 2).
                new BandRect("Title", 0.100f, 0.900f, closeLeft - 0.014f, 0.978f),
                new BandRect("Close", closeLeft, closeBottom, closeRight, closeTop),

                // Loadout slots BELOW the title, Clear visually apart, wallet at the lane's end.
                new BandRect("Slot.Raid",  0.030f, selBottom, 0.200f, selTop),
                new BandRect("Slot.Hold",  0.215f, selBottom, 0.385f, selTop),
                new BandRect("Slot.Siege", 0.400f, selBottom, 0.570f, selTop),
                new BandRect("Clear",      0.640f, selBottom, 0.790f, selTop),
                new BandRect("Wallet",     0.815f, selBottom, 0.970f, selTop),

                new BandRect("Roster",  0.030f, bodyBottom, 0.560f, bodyTop),
                new BandRect("Summary", 0.585f, bodyBottom, 0.960f, bodyTop),

                // Bottom bar: three exclusive lanes, and the Close is no longer one of them.
                new BandRect("Name", 0.030f, actionBottom, 0.290f, actionTop),
                new BandRect("Save", 0.310f, actionBottom, 0.560f, actionTop),
                new BandRect("Cta",  0.585f, actionBottom, 0.960f, actionTop),
            };
        }

        /// <summary>
        /// One roster row's children as EXCLUSIVE rects, in fractions of the row.
        /// <paramref name="rowW"/> is the row's width in canvas reference units so the steppers
        /// are authored AT OR OVER MinTouchPx rather than clamped up into their neighbours.
        /// THE COUNT FIELD IS THE POINT OF THIS TABLE: it used to be authored x 0.70..0.73 — three
        /// percent of the row, ~38 device px — which is why the owner's 20 rendered as a 2 stacked
        /// over a 0 (collision 1, the worst one).
        /// </summary>
        public static BandRect[] ComputeRowBands(float rowW)
        {
            rowW = Mathf.Max(1f, rowW);
            // Fixed-pixel requirements expressed in row fractions. The old 0.22 ceiling made
            // both steppers only 106 px wide on portrait, so ClampMinTouch grew them into the
            // count. Work backwards from the right edge instead: two 120 px controls, a count
            // wide enough for "999" at CountFontSize, and explicit gaps between all three.
            float step = Mathf.Clamp((ElarionUiKit.MinTouchPx + 8f) / rowW, 0.08f, 0.32f);
            float countSpan = Mathf.Clamp(104f / rowW, 0.12f, 0.24f);
            float plusRight = 0.985f;
            float plusLeft = plusRight - step;
            float countRight = plusLeft - 0.010f;
            float countLeft = countRight - countSpan;
            float minusRight = countLeft - 0.010f;
            float minusLeft = minusRight - step;
            float textRight = minusLeft - 0.015f;

            return new[]
            {
                new BandRect("Row.Name",  0.030f, 0.52f, textRight,  0.94f),
                new BandRect("Row.Cost",  0.030f, 0.08f, textRight,  0.48f),
                new BandRect("Row.Minus", minusLeft, 0f, minusRight, 1f),
                new BandRect("Row.Count", countLeft, 0.12f, countRight, 0.88f),
                new BandRect("Row.Plus",  plusLeft, 0f, plusRight,   1f),
            };
        }

        /// <summary>Look one band up by name. Returns a zero rect when absent (never throws).</summary>
        public static Rect Band(BandRect[] bands, string name)
        {
            if (bands == null) return new Rect();
            for (int i = 0; i < bands.Length; i++)
                if (bands[i].Name == name) return bands[i].Frac;
            return new Rect();
        }

        // =====================================================================

        public static void Show()
        {
            if (!BarracksUnlock.IsUnlocked)
            {
                FlowTrace.Step("Muster", "ArmyMusterPanel.Show refused - the Barracks is not built yet.");
                ElarionUiKit.ShowToast("The Barracks is not built yet.", ElarionUiKit.ToastTone.Danger);
                return;
            }
            if (s_host == null) s_host = new GameObject("ArmyMusterPanelHost").AddComponent<ArmyMusterPanel>();
            s_host.Open();
        }

        public void Open()
        {
            FlowTrace.Step("Muster", "ArmyMusterPanel.Open - loadout bank + training order UI.");
            Close();

            // Hydrate working set from the active saved slot (or seed a fun recipe).
            var army = ArmyLoadoutService.Ensure();
            _activeSlot = army != null ? army.ActiveLoadoutIndex : 0;
            ArmyLoadoutService.LoadInto(_activeSlot, s_composition);
            if (s_composition.TotalUnits <= 0 && string.IsNullOrEmpty(_lastResultHeadline))
            {
                // First open with empty slot: stage Raid Push so the panel never feels blank.
                ArmyLoadoutService.ApplyRecipe(s_composition, 0);
            }

            _ui = ElarionUiKit.BuildModalCanvas("ArmyMusterPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Armies - Loadouts",
                new Vector2(PanelAnchorMinX, PanelAnchorMinY), new Vector2(PanelAnchorMaxX, PanelAnchorMaxY),
                Close, frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "sword");

            // ── WO-1230: resolve the panel's REAL size, then partition it ─────
            float canvasH = ElarionUiKit.PostScaleCanvasHeight(chrome.content.transform);
            float canvasW = canvasH * Mathf.Max(1, ElarionUiKit.SurfaceWidth)
                                    / Mathf.Max(1, ElarionUiKit.SurfaceHeight);
            _panelW = PanelFracW * canvasW;
            _panelH = PanelFracH * canvasH;
            var bands = ComputeBands(_panelW, _panelH);

            var layout = chrome.layout;
            Rect rRoster = Band(bands, "Roster");
            Rect rSummary = Band(bands, "Summary");

            // The kit zones are RE-SEATED onto the table (their backing plates are children, so
            // they follow). Nothing is parented to raw chrome.content without a rect of its own.
            Reseat(layout != null ? layout.header : null, Band(bands, "Title"));
            Reseat(layout != null ? layout.body : null, rRoster);
            Reseat(layout != null ? layout.bodyLeft : null, rRoster);
            Reseat(layout != null ? layout.bodyRight : null, rSummary);
            SeatClose(chrome.close, Band(bands, "Close"));

            Transform listHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            // Collision 6: re-tint the kit's parchment plate on the DETAIL well to obsidian.
            RetintZoneBacking(_detailHost, SummaryFill);

            // The detail well's content lives in its OWN container so the rebuild can clear the
            // content without also destroying the zone's backing plate.
            _detailBody = Zone(_detailHost, "SummaryContent", new Rect(0f, 0f, 1f, 1f));

            var scroll = ElarionUiKit.MakeScrollZone(listHost, RowGapPx, 6);
            _listContent = scroll != null ? scroll.content : null;

            // Reserve the bottom of the roster well for the "+ N more (scroll)" affordance so the
            // hint can never sit on top of a row.
            float rosterH = rRoster.height * _panelH;
            float rosterFullW = rRoster.width * _panelW;
            _rowW = Mathf.Max(1f, rosterFullW - 24f);           // scroll padding + bar gutter
            float hintFrac = rosterH > 1f ? Mathf.Clamp(HintStripPx / rosterH, 0.05f, 0.25f) : 0.12f;
            if (scroll != null && scroll.scroll != null)
            {
                var host = scroll.scroll.transform as RectTransform;
                if (host != null)
                {
                    host.anchorMin = new Vector2(0f, hintFrac);
                    host.anchorMax = Vector2.one;
                    host.offsetMin = Vector2.zero; host.offsetMax = Vector2.zero;
                }
            }
            _rosterHint = ElarionUiKit.Label(listHost, "", 0f, hintFrac,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Midline, 0.04f, 0.96f);
            _rosterHint.raycastTarget = false;
            ElarionUiKit.FitSingleLine(_rosterHint);

            float viewportH = Mathf.Max(1f, rosterH * (1f - hintFrac) - 12f);
            _visibleRows = Mathf.Clamp(Mathf.FloorToInt(viewportH / (RowHeightPx + RowGapPx)), 1, MaxVisibleRows);

            // ── The two NEW band zones (the WO-1230 root-cause fix) ───────────
            // They are real zones with exclusive rects, not overlays on raw content.
            _selectorHost = Zone(chrome.content.transform, "Zone_SelectorBand",
                Union(Band(bands, "Slot.Raid"), Band(bands, "Clear")));
            _walletHost = Zone(chrome.content.transform, "Zone_WalletChip", Band(bands, "Wallet"));
            _actionHost = Zone(chrome.content.transform, "Zone_ActionBand",
                Union(Band(bands, "Name"), Band(bands, "Cta")));

            // The wallet reads as ONE value: a word, then the chip right beside it (collision 5 -
            // the chip used to be stretched across the whole ~2000px footer, icon at one end and
            // the number at the other). The word is there because the owner is red/green
            // colourblind and a coin glyph must never be the only identity.
            var goldWord = ElarionUiKit.Label(_walletHost, "Gold", 0.60f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0.04f, 0.96f, bold: true);
            goldWord.raycastTarget = false;
            ElarionUiKit.FitSingleLine(goldWord);
            var chipHost = Zone(_walletHost, "GoldChip", new Rect(0f, 0f, 1f, 0.56f));
            _wallet = ElarionUiKit.BuildWalletRow(chipHost, new[]
            {
                ElarionUiKit.CurrencyKind.Gold,
            });

            FlowTrace.Step("Muster", string.Format(
                "ArmyMusterPanel layout: canvas={0:F0}x{1:F0} panel={2:F0}x{3:F0} " +
                "roster=({4:F3},{5:F3})-({6:F3},{7:F3}) rowW={8:F0} visibleRows={9} bands={10}",
                canvasW, canvasH, _panelW, _panelH,
                rRoster.xMin, rRoster.yMin, rRoster.xMax, rRoster.yMax,
                _rowW, _visibleRows, bands.Length));

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

            if (_ui != null && _panelHandle != null)
                PanelManager.NotifyClosed(_panelHandle);

            if (_ui != null) Destroy(_ui);
            _ui = null;
            _listContent = null;
            _detailHost = null;
            _detailBody = null;
            _selectorHost = null;
            _actionHost = null;
            _walletHost = null;
            _rosterHint = null;
            _musterCta = null;
            _musterCtaLabel = null;
            _musterCtaSub = null;
        }

        private void OnDestroy()
        {
            BarracksService.Changed -= Rebuild;
            ArmyMusterService.Mustered -= Rebuild;
        }

        // ── Zone plumbing ─────────────────────────────────────────────────────

        /// <summary>A transparent, exclusively-rected drop zone — the same shape the kit's own
        /// private Zone() builds, so a band is a ZONE and never an overlay on raw content.</summary>
        private static RectTransform Zone(Transform parent, string name, Rect frac)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(frac.xMin, frac.yMin);
            rt.anchorMax = new Vector2(frac.xMax, frac.yMax);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void Reseat(RectTransform zone, Rect frac)
        {
            if (zone == null || frac.width <= 0f || frac.height <= 0f) return;
            zone.anchorMin = new Vector2(frac.xMin, frac.yMin);
            zone.anchorMax = new Vector2(frac.xMax, frac.yMax);
            zone.offsetMin = Vector2.zero; zone.offsetMax = Vector2.zero;
        }

        /// <summary>Seat the ONE shared kit Close in the header-right band. Its canonical
        /// 360x132 box (owner F8 x3 - the same Close size on every screen) is preserved; only
        /// where it sits changes, and it sits in a rect the layout table owns.</summary>
        private static void SeatClose(Button close, Rect frac)
        {
            if (close == null || frac.width <= 0f) return;
            var rt = close.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = new Vector2(frac.xMax, frac.yMax);
            rt.anchorMax = new Vector2(frac.xMax, frac.yMax);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ElarionUiKit.CanonCtaWidth, ElarionUiKit.CanonCtaHeight);
        }

        private static void RetintZoneBacking(Transform zone, Color fill)
        {
            if (zone == null) return;
            var backing = zone.Find("ZoneBacking");
            if (backing == null) { FlowTrace.Warn("Muster", "summary zone has no ZoneBacking plate to re-tint."); return; }
            var img = backing.GetComponent<Image>();
            if (img != null) img.color = fill;
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                                   Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        /// <summary>Re-express a panel-fraction band as a fraction of the zone that holds it.</summary>
        private static Rect ToLocal(Rect zone, Rect band)
        {
            float w = Mathf.Max(1e-4f, zone.width);
            float h = Mathf.Max(1e-4f, zone.height);
            return Rect.MinMaxRect((band.xMin - zone.xMin) / w, (band.yMin - zone.yMin) / h,
                                   (band.xMax - zone.xMin) / w, (band.yMax - zone.yMin) / h);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnMuster()
        {
            // Auto-save active slot so the training order never loses a staged plan.
            ArmyLoadoutService.SaveFrom(_activeSlot, s_composition);

            var report = ArmyMusterService.Muster(s_composition);
            _lastResultHeadline = PlayerWords(report.Headline);
            _lastResultDetail = PlayerWords(report.Detail);

            var tone = report.Complete ? ElarionUiKit.ToastTone.Confirm
                     : report.AnyQueued ? ElarionUiKit.ToastTone.Gold
                     : ElarionUiKit.ToastTone.Danger;
            ElarionUiKit.ShowToast(PlayerWords(report.Summary), tone, 3.2f);

            foreach (var r in report.Rows)
                if (r.Queued > 0) s_composition.Add(r.TroopId, -r.Queued);

            // Keep the saved slot as the full plan; working set drops what queued.
            Rebuild();
        }

        /// <summary>
        /// OWNER RULING 2026-08-26 ("what dos muster army mean? Thats where im lost"): the word
        /// "muster" is archaic jargon for what is a TRAINING ORDER, and it must not reach the
        /// player. The rewrite happens HERE, at the presentation boundary, because ArmyMusterService
        /// is the MODEL: its identifiers (ArmyMusterService.Muster, the "Muster" FlowTrace tag) are
        /// live and a rename is a wide mechanical diff with zero player benefit — the ruling is
        /// PLAYER-FACING STRINGS ONLY. ASCII in, ASCII out.
        /// ⚠ ArmyMusterService.cs still AUTHORS the archaic word in three literals (lines 217, 332,
        /// 338 at the time of writing); this maps them on the way to the screen.
        /// </summary>
        private static string PlayerWords(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("Nothing to muster", "Nothing to train");
            s = s.Replace("Mustered", "Training ordered");
            s = s.Replace("mustered", "training ordered");
            s = s.Replace("Muster", "Train");
            s = s.Replace("muster", "training");
            return s;
        }

        private void OnSaveSlot()
        {
            ArmyLoadoutService.SaveFrom(_activeSlot, s_composition);
            ElarionUiKit.ShowToast(
                "Saved '" + s_composition.Name + "' to slot " + (_activeSlot + 1) + ".",
                ElarionUiKit.ToastTone.Confirm);
            Rebuild();
        }

        private void OnSelectSlot(int index)
        {
            if (index == _activeSlot)
            {
                // Re-tap reloads saved version (discard unsaved edits).
                ArmyLoadoutService.LoadInto(index, s_composition);
                ElarionUiKit.ShowToast("Reloaded " + ArmyLoadoutService.SlotName(index) + ".", ElarionUiKit.ToastTone.Info);
            }
            else
            {
                // Auto-save previous working set into the slot we leave (never lose work).
                ArmyLoadoutService.SaveFrom(_activeSlot, s_composition);
                _activeSlot = index;
                ArmyLoadoutService.LoadInto(index, s_composition);
                ElarionUiKit.ShowToast("Editing " + ArmyLoadoutService.SlotName(index) + ".", ElarionUiKit.ToastTone.Info);
            }
            Rebuild();
        }

        private void OnRecipe(int recipe)
        {
            string msg = ArmyLoadoutService.ApplyRecipe(s_composition, recipe);
            ElarionUiKit.ShowToast(msg, ElarionUiKit.ToastTone.Gold);
            Rebuild();
        }

        private void OnCycleName()
        {
            // Fun: cycle default names so the player can "feel" ownership without a soft keyboard.
            string[] names =
            {
                "Raid Push", "Wall Hold", "Siege Prep",
                "Night Watch", "Quick Strike", "Last Stand", "New Army",
            };
            string cur = s_composition.Name ?? "";
            int idx = 0;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], cur, System.StringComparison.OrdinalIgnoreCase))
                { idx = (i + 1) % names.Length; break; }
            s_composition.Name = names[idx];
            Rebuild();
        }

        // ── Render ────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_ui == null || _detailBody == null) return;
            UpdateWallet();
            BuildTroopLadder();
            BuildCommandBands();
            BuildDetail();
            UpdateCta();
        }

        private void UpdateWallet()
        {
            if (_wallet == null || _wallet.Length < 1) return;
            _wallet[0]?.SetAmount(ArmyMusterService.GoldBalance());
        }

        private void BuildTroopLadder()
        {
            Transform host = _listContent != null ? (Transform)_listContent : null;
            if (host == null) return;

            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);

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
                SetHint("");
                return;
            }

            Guard.TryEach("Muster", "troop-row", offered, def => BuildTroopRow(host, def));

            // Owner ruling 2026-08-26 (shared with WO-1228): SIX lines, then scroll. On this
            // landscape frame the well seats fewer, so the affordance says how many are below.
            int hidden = offered.Count - _visibleRows;
            SetHint(hidden > 0 ? "+ " + hidden + " more (scroll)" : "");
        }

        private void SetHint(string text)
        {
            if (_rosterHint == null) return;
            _rosterHint.text = text ?? "";
        }

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

            var rb = ComputeRowBands(_rowW);
            Rect rName = Band(rb, "Row.Name");
            Rect rCost = Band(rb, "Row.Cost");
            Rect rMinus = Band(rb, "Row.Minus");
            Rect rCount = Band(rb, "Row.Count");
            Rect rPlus = Band(rb, "Row.Plus");

            string name = string.IsNullOrEmpty(def.DisplayName) ? id : def.DisplayName;
            // Cap note for siege maxOwned
            string capTag = def.MaxOwned == 1 ? " (max 1)" : "";
            var nameLabel = ElarionUiKit.Label(row.transform, name + capTag, rName.yMin, rName.yMax,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.MidlineLeft,
                rName.xMin, rName.xMax, bold: true);
            nameLabel.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nameLabel);

            // Collision 3: "550 Gold - 1m 00s each" wrapped to a second line and pushed into the
            // row below. The string is now ONE compact line, and FitSingleLine makes a long one
            // ellipsize inside its own band instead of wrapping into a neighbour.
            var costLabel = ElarionUiKit.Label(row.transform, PerUnitLine(def), rCost.yMin, rCost.yMax,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft,
                rCost.xMin, rCost.xMax);
            costLabel.raycastTarget = false;
            ElarionUiKit.FitSingleLine(costLabel);

            var minus = ElarionUiKit.Button(row.transform, "-", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(rMinus.xMin, rMinus.yMin), new Vector2(rMinus.xMax, rMinus.yMax), () => Step(id, -1));
            if (minus != null) ElarionUiKit.ClampMinTouch(minus);

            // THE COUNT FIELD. Emphasis is a BORDER, never a hue (the owner is red/green
            // colourblind), and the field is wide enough for three digits with headroom for four.
            var countPlate = ElarionUiKit.AddImage(row.transform, "CountPlate",
                new Vector2(rCount.xMin, rCount.yMin), new Vector2(rCount.xMax, rCount.yMax), CountFill);
            var plateImg = countPlate.GetComponent<Image>();
            if (plateImg != null) plateImg.raycastTarget = false;
            var frameSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
            if (frameSprite != null)
            {
                var frameGo = ElarionUiKit.AddImage(countPlate.transform, "CountFrame",
                    Vector2.zero, Vector2.one, ElarionUi.Gilt, rounded: false);
                var frameImg = frameGo.GetComponent<Image>();
                frameImg.sprite = frameSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.fillCenter = false;
                frameImg.raycastTarget = false;
            }

            var count = ElarionUiKit.Label(countPlate.transform, s_composition.CountOf(id).ToString(),
                0.06f, 0.94f, ElarionUi.Parchment, CountFontSize, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: true);
            count.name = "CountLabel";
            count.raycastTarget = false;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            // 4 digits and beyond: FitSingleLine auto-sizes DOWN inside the 200px box and
            // ellipsizes at the font floor. It never wraps, so a 2-over-0 stack cannot recur.
            ElarionUiKit.FitSingleLine(count);

            var plus = ElarionUiKit.Button(row.transform, "+", ElarionUiKit.ButtonKind.Gold,
                new Vector2(rPlus.xMin, rPlus.yMin), new Vector2(rPlus.xMax, rPlus.yMax), () => Step(id, 1));
            if (plus != null) ElarionUiKit.ClampMinTouch(plus);
        }

        private void Step(string troopId, int delta)
        {
            // Honour maxOwned in the composer so the UI can't stage 2 catapults.
            var def = TroopCatalog.Find(troopId);
            if (def != null && def.MaxOwned > 0 && delta > 0)
            {
                int want = s_composition.CountOf(troopId) + delta;
                if (want > def.MaxOwned)
                {
                    ElarionUiKit.ShowToast(
                        "Only " + def.MaxOwned + "x " +
                        (string.IsNullOrEmpty(def.DisplayName) ? troopId : def.DisplayName) +
                        " in a loadout.",
                        ElarionUiKit.ToastTone.Info);
                    return;
                }
            }
            s_composition.Add(troopId, delta);
            Rebuild();
        }

        private static string PerUnitLine(TroopDef def)
        {
            var cost = new ArmyCost { Gold = def.CostGold };
            return cost.ToString() + " - " + CompactDuration(def.BuildSeconds);
        }

        /// <summary>ONE-LINE time grammar for a roster row ("45s", "1m", "1m30", "2h"). The long
        /// form (ArmyMusterPlanner.FormatDuration) stays exactly as it is for the summary column and
        /// its regression - this is a row-width presentation choice, not a new duration model.</summary>
        public static string CompactDuration(double seconds)
        {
            if (seconds <= 0d) return "0s";
            int total = (int)System.Math.Round(seconds);
            if (total < 60) return total + "s";
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            if (h > 0) return m > 0 ? h + "h" + m : h + "h";
            return s > 0 ? m + "m" + s : m + "m";
        }

        private void BuildDetail()
        {
            if (_detailBody == null) return;

            for (int i = _detailBody.childCount - 1; i >= 0; i--)
                Destroy(_detailBody.GetChild(i).gameObject);

            var preview = ArmyMusterService.Preview(s_composition);
            var body = new System.Text.StringBuilder();

            body.Append("STAGED: ").Append(s_composition.Name).Append("  (slot ")
                .Append(_activeSlot + 1).Append(")\n");

            if (preview.TotalUnits <= 0)
            {
                body.Append("\nEmpty plan.\n");
                body.Append("Tap Raid / Hold / Siege for a quick fill,\n");
                body.Append("or [+] troops on the left.\n");
                body.Append("Then Save slot and Train Army.\n");
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
                body.Append("\nCost: ").Append(preview.Cost).Append('\n');
                body.Append("Time: ").Append(ArmyMusterPlanner.FormatDuration(preview.TotalSeconds))
                    .Append(" (").Append(preview.TrainSlots).Append(" train slot")
                    .Append(preview.TrainSlots == 1 ? "" : "s").Append(")\n");
            }

            body.Append("\nTrain queue: ").Append(preview.LineDepth).Append(" of ")
                .Append(ArmyMusterPlanner.TrainQueueDepthCap).Append(" used, ")
                .Append(preview.LineRoom).Append(" free.\n");
            if (preview.WouldNotFit > 0)
                body.Append("Fits now: ").Append(preview.WouldFit).Append(" of ")
                    .Append(preview.TotalUnits).Append(" (rest stays staged).\n");

            if (!string.IsNullOrEmpty(_lastResultHeadline))
            {
                body.Append("\nLAST TRAINING ORDER\n").Append(_lastResultHeadline).Append('\n');
                if (!string.IsNullOrEmpty(_lastResultDetail)) body.Append(_lastResultDetail).Append('\n');
            }

            // OWNER RULING 2026-08-26 - the tip line, verbatim.
            body.Append("\nTip: Training auto-saves this slot. Fill the army, then Raids.");

            bool shortOf = preview.TotalUnits > 0 && !preview.Affordable;
            float textFloor = shortOf ? 0.17f : 0.04f;

            var text = ElarionUiKit.Label(_detailBody, body.ToString(), textFloor, 0.96f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.TopLeft, 0.05f, 0.95f);
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            ElarionUiKit.FitBlock(text, 28f, ElarionUi.FontLabel);

            if (shortOf)
            {
                // A FRAMED WORD-CHIP, never a colour. The owner is red/green colourblind, so the
                // shortfall has to survive a greyscale check: it does, because it is a word in a
                // box rather than a red tint on a number.
                var chip = ElarionUiKit.AddImage(_detailBody, "ShortOfChip",
                    new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.145f), CountFill);
                var chipImg = chip.GetComponent<Image>();
                if (chipImg != null) chipImg.raycastTarget = false;
                var chipFrame = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
                if (chipFrame != null)
                {
                    var fg = ElarionUiKit.AddImage(chip.transform, "ShortOfFrame",
                        Vector2.zero, Vector2.one, ElarionUi.Gilt, rounded: false);
                    var fi = fg.GetComponent<Image>();
                    fi.sprite = chipFrame; fi.type = Image.Type.Sliced; fi.fillCenter = false;
                    fi.raycastTarget = false;
                }
                var chipLabel = ElarionUiKit.Label(chip.transform, "SHORT OF: " + preview.ShortOf,
                    0.08f, 0.92f, ElarionUi.Parchment, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
                chipLabel.raycastTarget = false;
                ElarionUiKit.FitSingleLine(chipLabel);
            }
        }

        private void BuildCommandBands()
        {
            ClearChildren(_selectorHost);
            ClearChildren(_actionHost);
            if (_selectorHost == null || _actionHost == null) return;

            var bands = ComputeBands(_panelW, _panelH);
            Rect selZone = Union(Band(bands, "Slot.Raid"), Band(bands, "Clear"));
            Rect actZone = Union(Band(bands, "Name"), Band(bands, "Cta"));

            string[] selectors = { "Raid", "Hold", "Siege", "Clear" };
            string[] selectorBands = { "Slot.Raid", "Slot.Hold", "Slot.Siege", "Clear" };
            for (int i = 0; i < selectors.Length; i++)
            {
                int selection = i;
                bool active = i < ArmyLoadoutService.SlotCount && i == _activeSlot;
                var kind = active ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet;
                Rect r = ToLocal(selZone, Band(bands, selectorBands[i]));
                // ACTIVE is a WORD, not a hue - greyscale-safe slot state. Single line on
                // purpose: the kit button fits its face with NoWrap + Ellipsis, so a second
                // line would be at the mercy of the overflow mode.
                string face = active ? selectors[i] + " *ACTIVE*" : selectors[i];
                var button = ElarionUiKit.Button(_selectorHost, face, kind,
                    new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax),
                    () => { if (selection < ArmyLoadoutService.SlotCount) OnSelectSlot(selection); else OnRecipe(3); });
                if (button != null) ElarionUiKit.ClampMinTouch(button);
            }

            Rect rName = ToLocal(actZone, Band(bands, "Name"));
            Rect rSave = ToLocal(actZone, Band(bands, "Save"));
            Rect rCta = ToLocal(actZone, Band(bands, "Cta"));

            var name = ElarionUiKit.Button(_actionHost, "Name: " + ShortName(s_composition.Name),
                ElarionUiKit.ButtonKind.Quiet, new Vector2(rName.xMin, rName.yMin),
                new Vector2(rName.xMax, rName.yMax), OnCycleName);
            var save = ElarionUiKit.Button(_actionHost, "Save slot " + (_activeSlot + 1),
                ElarionUiKit.ButtonKind.Gold, new Vector2(rSave.xMin, rSave.yMin),
                new Vector2(rSave.xMax, rSave.yMax), OnSaveSlot);
            _musterCta = ElarionUiKit.Button(_actionHost, "Train Army", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(rCta.xMin, rCta.yMin), new Vector2(rCta.xMax, rCta.yMax), OnMuster);
            if (name != null) ElarionUiKit.ClampMinTouch(name);
            if (save != null) ElarionUiKit.ClampMinTouch(save);
            if (_musterCta != null)
            {
                ElarionUiKit.ClampMinTouch(_musterCta);
                _musterCtaLabel = _musterCta.GetComponentInChildren<TextMeshProUGUI>();
                if (_musterCtaLabel != null)
                {
                    // Two lines, two labels: the CTA says WHAT it does, the subline says what
                    // happens to the 15 units that do not start now - "5 start now, 15 stay
                    // staged" without the player reading the summary column.
                    var lrt = _musterCtaLabel.transform as RectTransform;
                    if (lrt != null)
                    {
                        lrt.anchorMin = new Vector2(lrt.anchorMin.x, 0.44f);
                        lrt.anchorMax = new Vector2(lrt.anchorMax.x, 0.94f);
                        lrt.offsetMin = new Vector2(lrt.offsetMin.x, 0f);
                        lrt.offsetMax = new Vector2(lrt.offsetMax.x, 0f);
                    }
                    _musterCtaSub = ElarionUiKit.Label(_musterCta.transform, "", 0.06f, 0.42f,
                        ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.04f, 0.96f);
                    _musterCtaSub.raycastTarget = false;
                    ElarionUiKit.FitSingleLine(_musterCtaSub);
                }
            }
        }

        private static void ClearChildren(Transform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Army";
            if (name.Length <= 12) return name;
            return name.Substring(0, 11) + ".";
        }

        private void UpdateCta()
        {
            if (_musterCta == null) return;
            var preview = ArmyMusterService.Preview(s_composition);

            // OWNER RULING 2026-08-26: the CTA reads "Train Army" on every state; the STATE lives
            // in the subline, so the button never becomes a sentence the player has to decode.
            string sub;
            bool interactable;
            if (preview.TotalUnits <= 0)
            {
                sub = preview.LineDepth > 0
                    ? "Queue busy - " + preview.LineDepth + " training"
                    : "Stage troops first";
                interactable = false;
            }
            else if (preview.LineRoom <= 0)
            {
                sub = "Queue full - " + preview.LineDepth + " of " + ArmyMusterPlanner.TrainQueueDepthCap;
                interactable = false;
            }
            else if (preview.WouldNotFit > 0)
            {
                sub = preview.WouldFit + " start now - " + preview.WouldNotFit + " stay staged";
                interactable = true;
            }
            else
            {
                sub = preview.LineDepth > 0
                    ? preview.TotalUnits + " start now - " + preview.LineDepth + " already training"
                    : preview.TotalUnits + " start now";
                interactable = true;
            }

            _musterCta.interactable = interactable;
            if (_musterCtaLabel != null) _musterCtaLabel.text = "Train Army";
            if (_musterCtaSub != null) _musterCtaSub.text = sub;
        }
    }
}
